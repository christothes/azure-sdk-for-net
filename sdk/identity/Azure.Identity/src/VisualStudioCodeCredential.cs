// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Core.Pipeline;
using Microsoft.Identity.Client;

namespace Azure.Identity
{
    /// <summary>
    /// Enables authentication to Azure Active Directory using data from Visual Studio Code.
    /// </summary>
    public class VisualStudioCodeCredential : TokenCredential
    {
        private const string CredentialsSection = "VS Code Azure";
        private const string ClientId = "aebc6443-996d-45c2-90f0-388ff96faa56";
        private const string Common = "common";
        private readonly IVisualStudioCodeAdapter _vscAdapter;
        private readonly IFileSystemService _fileSystem;
        private readonly CredentialPipeline _pipeline;
        private readonly string _tenantId;
        private readonly MsalPublicClient _client;
        private readonly string[] _managementScope = new string[] {"https://management.core.windows.net/.default"};
        private string _discoveredTenantId;

        /// <summary>
        /// Creates a new instance of the <see cref="VisualStudioCodeCredential"/>.
        /// </summary>
        public VisualStudioCodeCredential() : this(default, default, default, default, default) { }

        /// <summary>
        /// Creates a new instance of the <see cref="VisualStudioCodeCredential"/> with the specified options.
        /// </summary>
        /// <param name="options">Options for configuring the credential.</param>
        public VisualStudioCodeCredential(VisualStudioCodeCredentialOptions options) : this(options, default, default, default, default) { }

        internal VisualStudioCodeCredential(VisualStudioCodeCredentialOptions options, CredentialPipeline pipeline, MsalPublicClient client, IFileSystemService fileSystem,
            IVisualStudioCodeAdapter vscAdapter)
        {
            _tenantId = options?.TenantId ?? Common;
            _pipeline = pipeline ?? CredentialPipeline.GetInstance(options);
            _client = client ?? new MsalPublicClient(_pipeline, options?.TenantId, ClientId, null, null);
            _fileSystem = fileSystem ?? FileSystemService.Default;
            _vscAdapter = vscAdapter ?? GetVscAdapter();
        }

        /// <inheritdoc />
        public override async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
            => await GetTokenImplAsync(requestContext, true, cancellationToken).ConfigureAwait(false);

        /// <inheritdoc />
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
            => GetTokenImplAsync(requestContext, false, cancellationToken).EnsureCompleted();

        private async ValueTask<AccessToken> GetTokenImplAsync(TokenRequestContext requestContext, bool async, CancellationToken cancellationToken)
        {
            using CredentialDiagnosticScope scope = _pipeline.StartGetTokenScope("VisualStudioCodeCredential.GetToken", requestContext);
            string tenant = default;
            string storedCredentials = default;
            AzureCloudInstance cloudInstance = default;
            try
            {
                GetUserSettings(out tenant, out var environmentName);

                if (string.Equals(tenant, Constants.AdfsTenantId, StringComparison.Ordinal))
                {
                    throw new CredentialUnavailableException("VisualStudioCodeCredential authentication unavailable. ADFS tenant / authorities are not supported.");
                }

                cloudInstance = GetAzureCloudInstance(environmentName);
                storedCredentials = GetStoredCredentials(environmentName);

                var result = await _client.AcquireTokenByRefreshTokenAsync(requestContext.Scopes, requestContext.Claims, storedCredentials, cloudInstance, tenant, async, cancellationToken)
                    .ConfigureAwait(false);
                return scope.Succeeded(new AccessToken(result.AccessToken, result.ExpiresOn));
            }
            catch (MsalUiRequiredException e)
            {
                // If we already had a specific tenant set, fail immediately.
                if (tenant != Common)
                {
                    throw scope.FailWrapAndThrow(new CredentialUnavailableException(
                        $"{nameof(VisualStudioCodeCredential)} authentication unavailable. Token acquisition failed. Ensure that you have authenticated in VSCode Azure Account.", e));
                }

                // If we've only tried the default common tenant, try again for each known tenantId
                try
                {
                    var result = await GetTokenImplTryAllTenantsAsync(tenant, cloudInstance, storedCredentials, requestContext, async, cancellationToken).ConfigureAwait(false);
                    return scope.Succeeded(new AccessToken(result.AccessToken, result.ExpiresOn));
                }
                catch (Exception ex)
                {
                    throw scope.FailWrapAndThrow(new CredentialUnavailableException(
                        $"{nameof(VisualStudioCodeCredential)} authentication unavailable. Token acquisition failed. Ensure that you have authenticated in VSCode Azure Account.", ex));
                }
            }
            catch (Exception e)
            {
                throw scope.FailWrapAndThrow(e);
            }
        }

        private async ValueTask<AuthenticationResult> GetTokenImplTryAllTenantsAsync(string originalTenant, AzureCloudInstance cloudInstance, string storedCredentials, TokenRequestContext requestContext,
            bool async, CancellationToken cancellationToken)
        {
            var exceptions = new List<Exception>();
            List<string> tenants;
            AuthenticationResult result = null;

            // Get a token for the management scope so that we can enumerate the known tenants for this credential.
            // If this throws, we'll let it throw and be caught upstream.
            var token = await _client.AcquireTokenByRefreshTokenAsync(_managementScope, null, storedCredentials, cloudInstance, originalTenant, async, cancellationToken).ConfigureAwait(false);
            tenants = await GetUserTenants(token.AccessToken, async, cancellationToken).ConfigureAwait(false);

            foreach (string tenant in tenants)
            {
                try
                {
                    result = await _client.AcquireTokenByRefreshTokenAsync(requestContext.Scopes, requestContext.Claims, storedCredentials, cloudInstance, tenant, async, cancellationToken)
                        .ConfigureAwait(false);
                    // store the discovered tenant based on this successful result.
                    _discoveredTenantId = tenant;
                    return result;
                }
                catch (Exception e)
                {
                    exceptions.Add(e);
                }
            }

            exceptions.Add(new Exception($"Alternate tenant discovery failed. Discovered {tenants.Count} tenants"));
            throw new AggregateException(exceptions);
        }

        private string GetStoredCredentials(string environmentName)
        {
            try
            {
                var storedCredentials = _vscAdapter.GetCredentials(CredentialsSection, environmentName);
                if (!IsRefreshTokenString(storedCredentials))
                {
                    throw new CredentialUnavailableException("Need to re-authenticate user in VSCode Azure Account.");
                }

                return storedCredentials;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException || ex is CredentialUnavailableException))
            {
                throw new CredentialUnavailableException("Stored credentials not found. Need to authenticate user in VSCode Azure Account.", ex);
            }
        }

        private static bool IsRefreshTokenString(string str)
        {
            for (var index = 0; index < str.Length; index++)
            {
                var ch = (uint)str[index];
                if ((ch < '0' || ch > '9') && (ch < 'A' || ch > 'Z') && (ch < 'a' || ch > 'z') && ch != '_' && ch != '-' && ch != '.')
                {
                    return false;
                }
            }

            return true;
        }

        private void GetUserSettings(out string tenant, out string environmentName)
        {
            var path = _vscAdapter.GetUserSettingsPath();
            tenant = _discoveredTenantId ?? _tenantId;
            environmentName = "AzureCloud";

            try
            {
                var content = _fileSystem.ReadAllText(path);
                var root = JsonDocument.Parse(content).RootElement;

                if (root.TryGetProperty("azure.tenant", out JsonElement tenantProperty))
                {
                    tenant = tenantProperty.GetString();
                }

                if (root.TryGetProperty("azure.cloud", out JsonElement environmentProperty))
                {
                    environmentName = environmentProperty.GetString();
                }
            }
            catch (IOException) { }
            catch (JsonException) { }
        }

        private async Task<List<string>> GetUserTenants(string token, bool async, CancellationToken cancellationToken)
        {
            if (async)
            {
            }

            var req = _pipeline.HttpPipeline.CreateRequest();
            req.Uri.Reset(new Uri("https://management.azure.com/tenants/?api-version=2020-01-01"));
            req.Headers.SetValue(HttpHeader.Names.Authorization, $"Bearer {token}");

#pragma warning disable IDE0008 // Use explicit type
#pragma warning disable AZC0110 // DO NOT use await keyword in possibly synchronous scope.
            var resp = await _pipeline.HttpPipeline.SendRequestAsync(req, cancellationToken).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(resp.ContentStream, default, cancellationToken).ConfigureAwait(false);
#pragma warning restore AZC0110 // DO NOT use await keyword in possibly synchronous scope.
#pragma warning restore IDE0008 // Use explicit type

            List<string> result = new List<string>();
            string nextLink = default;

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.NameEquals("value"))
                {
                    foreach (var item in property.Value.EnumerateArray())
                    {
                        foreach (var tenantProperty in item.EnumerateObject())
                        {
                            if (tenantProperty.NameEquals("tenantId"))
                            {
                                result.Add(tenantProperty.Value.GetString());
                                continue;
                            }
                        }
                    }
                }

                if (property.NameEquals("nextLink"))
                {
                    nextLink = property.Value.GetString();
                    continue;
                }
            }

            return result;
        }

        private static IVisualStudioCodeAdapter GetVscAdapter()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return new WindowsVisualStudioCodeAdapter();
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return new MacosVisualStudioCodeAdapter();
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return new LinuxVisualStudioCodeAdapter();
            }

            throw new PlatformNotSupportedException();
        }

        private static AzureCloudInstance GetAzureCloudInstance(string name) =>
            name switch
            {
                "AzureCloud" => AzureCloudInstance.AzurePublic,
                "AzureChina" => AzureCloudInstance.AzureChina,
                "AzureGermanCloud" => AzureCloudInstance.AzureGermany,
                "AzureUSGovernment" => AzureCloudInstance.AzureUsGovernment,
                _ => AzureCloudInstance.AzurePublic
            };
    }
}
