// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Microsoft.Identity.Client.Extensibility;

namespace Azure.Identity
{
    internal class ManagedIdentityClient
    {
        internal const string MsiUnavailableError = "ManagedIdentityCredential authentication unavailable. No Managed Identity endpoint found.";
        internal Lazy<ManagedIdentitySource> _identitySource;

        protected ManagedIdentityClient()
        {
        }

        public ManagedIdentityClient(CredentialPipeline pipeline, string clientId = null)
            : this(new ManagedIdentityClientOptions { Pipeline = pipeline, ClientId = clientId })
        {
        }

        public ManagedIdentityClient(CredentialPipeline pipeline, ResourceIdentifier resourceId)
            : this(new ManagedIdentityClientOptions { Pipeline = pipeline, ResourceIdentifier = resourceId })
        {
        }

        public ManagedIdentityClient(ManagedIdentityClientOptions options)
        {
            if (options.ClientId != null && options.ResourceIdentifier != null)
            {
                throw new ArgumentException(
                    $"{nameof(ManagedIdentityClientOptions)} cannot specify both {nameof(options.ResourceIdentifier)} and {nameof(options.ClientId)}.");
            }

            options.AppTokenProviderCallback ??= AppTokenProviderImpl;
            _identitySource = new Lazy<ManagedIdentitySource>(() => SelectManagedIdentitySource(options));
        }

        internal CredentialPipeline Pipeline => _identitySource.Value.Pipeline;

        internal protected string ClientId => _identitySource.Value.ClientId;

        internal ResourceIdentifier ResourceIdentifier => _identitySource.Value.ResourceIdentifier;

        public async ValueTask<AccessToken> GetTokenAsync(bool async, TokenRequestContext context, CancellationToken cancellationToken) =>
            await _identitySource.Value.AuthenticateAsync(async, context, cancellationToken).ConfigureAwait(false);

        public virtual async ValueTask<AccessToken> GetTokenCoreAsync(bool async, TokenRequestContext context,
            CancellationToken cancellationToken)
        {
            return await _identitySource.Value.GetTokenAsync(async, context, cancellationToken).ConfigureAwait(false);
        }

        private async Task<AppTokenProviderResult> AppTokenProviderImpl(AppTokenProviderParameters parameters)
        {
            TokenRequestContext requestContext = new TokenRequestContext(parameters.Scopes.ToArray(), claims: parameters.Claims);

            AccessToken token = await GetTokenCoreAsync(true, requestContext, parameters.CancellationToken).ConfigureAwait(false);

            return new AppTokenProviderResult() { AccessToken = token.Token, ExpiresInSeconds = Math.Max(Convert.ToInt64((token.ExpiresOn - DateTimeOffset.UtcNow).TotalSeconds), 1) };
        }

        private static ManagedIdentitySource SelectManagedIdentitySource(ManagedIdentityClientOptions options)
        {
            return
                ServiceFabricManagedIdentitySource.TryCreate(options) ??
                AppServiceV2019ManagedIdentitySource.TryCreate(options) ??
                AppServiceV2017ManagedIdentitySource.TryCreate(options) ??
                CloudShellManagedIdentitySource.TryCreate(options) ??
                AzureArcManagedIdentitySource.TryCreate(options) ??
                TokenExchangeManagedIdentitySource.TryCreate(options) ??
                new ImdsManagedIdentitySource(options);
        }
    }
}
