// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.AppConfig;

namespace Azure.Identity
{
    internal class MsalManagedIdentityClient
    {
        private readonly AsyncLockWithValue<IManagedIdentityApplication> _clientAsyncLock;
        private readonly AsyncLockWithValue<IManagedIdentityApplication> _clientWithCaeAsyncLock;
        internal string RedirectUrl { get; }
        internal bool IsProofOfPossessionRequired { get; }
        internal string[] cp1Capabilities = new[] { "CP1" };
        internal CredentialPipeline Pipeline { get; }
        protected internal bool IsSupportLoggingEnabled { get; }
        internal ManagedIdentityId managedIdentityId { get; }

        protected MsalManagedIdentityClient()
        { }

        public MsalManagedIdentityClient(CredentialPipeline pipeline, ManagedIdentityClientOptions options)
        {
            Pipeline = pipeline;
            IsSupportLoggingEnabled = options.Options?.IsUnsafeSupportLoggingEnabled ?? false;
            managedIdentityId = options.ClientId switch
            {
                null when options.ResourceIdentifier is not null => ManagedIdentityId.WithUserAssignedResourceId(options.ResourceIdentifier.ToString()),
                not null => ManagedIdentityId.WithUserAssignedClientId(options.ClientId),
                _ => ManagedIdentityId.SystemAssigned
            };

            _clientAsyncLock = new AsyncLockWithValue<IManagedIdentityApplication>();
            _clientWithCaeAsyncLock = new AsyncLockWithValue<IManagedIdentityApplication>();
        }

        public virtual async ValueTask<AuthenticationResult> AuthenticateAsync(TokenRequestContext context, bool async, CancellationToken cancellationToken) =>
            await AuthenticateCoreAsync(context, async, cancellationToken).ConfigureAwait(false);

        public virtual async ValueTask<AuthenticationResult> AuthenticateCoreAsync(TokenRequestContext context, bool async, CancellationToken cancellationToken)
        {
            var client = await CreateClientAsync(context.IsCaeEnabled, async, cancellationToken).ConfigureAwait(false);
            var builder = client.AcquireTokenForManagedIdentity(context.Scopes[0]);

            return await builder.ExecuteAsync(async, cancellationToken).ConfigureAwait(false);
        }

        internal virtual ValueTask<IManagedIdentityApplication> CreateClientAsync(bool enableCae, bool async, CancellationToken cancellationToken)
        {
            return CreateClientCoreAsync(enableCae, managedIdentityId, async, cancellationToken);
        }

        // TODO: Implement this method when static ManagedIdentityApplication.IsProofOfPossessionSupportedByClient is implemented
        public static bool IsProofOfPossessionSupportedByClient => BindingCertificate is not null;

        public static X509Certificate2 BindingCertificate
        {
            // TODO: remove these debug prints.
            get
            {
                var cert = ManagedIdentityApplication.GetBindingCertificate();
                if (cert is null)
                {
                    Console.WriteLine("*******************\n\nBinding certificate is not available.\n\n*******************");
                }
                else
                {
                    Console.WriteLine($"*******************\n\nBinding certificate is available. ({cert.Thumbprint})\n\n*******************");
                }
                return cert;
            }
        }

        internal virtual async ValueTask<IManagedIdentityApplication> CreateClientCoreAsync(bool enableCae, ManagedIdentityId managedIdentityId, bool async, CancellationToken cancellationToken)
        {
            using var asyncLock = enableCae ?
                await _clientWithCaeAsyncLock.GetLockOrValueAsync(async, cancellationToken).ConfigureAwait(false) :
                await _clientAsyncLock.GetLockOrValueAsync(async, cancellationToken).ConfigureAwait(false);

            if (asyncLock.HasValue)
            {
                return asyncLock.Value;
            }

            ManagedIdentityApplicationBuilder miAppBuilder = ManagedIdentityApplicationBuilder
                .Create(managedIdentityId)
                .WithHttpClientFactory(new HttpPipelineClientFactory(Pipeline.HttpPipeline))
                .WithExperimentalFeatures()
                .WithLogging(LogMsal, enablePiiLogging: IsSupportLoggingEnabled);

            if (enableCae)
            {
                miAppBuilder.WithClientCapabilities(cp1Capabilities);
            }

            return miAppBuilder.Build();
        }

        protected void LogMsal(LogLevel level, string message, bool isPii)
        {
            if (!isPii || IsSupportLoggingEnabled)
            {
                AzureIdentityEventSource.Singleton.LogMsal(level, message);
            }
        }
    }
}
