// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.AppConfig;

namespace Azure.Identity
{
    internal class  MsalManagedIdentityClient
    {
        internal string RedirectUrl { get; }
        internal bool IsProofOfPossessionRequired { get; }
        internal string[] cp1Capabilities = new[] { "CP1" };
        internal CredentialPipeline Pipeline { get; }
        protected internal bool IsSupportLoggingEnabled { get; }

        protected MsalManagedIdentityClient()
        { }

        public MsalManagedIdentityClient(CredentialPipeline pipeline, TokenCredentialOptions options)
        {
            Pipeline = pipeline;
            IsSupportLoggingEnabled = options?.IsUnsafeSupportLoggingEnabled ?? false;
        }

        protected ValueTask<IManagedIdentityApplication> CreateClientAsync(bool enableCae, ManagedIdentityId managedIdentityId, bool async, CancellationToken cancellationToken)
        {
            return CreateClientCoreAsync(enableCae, managedIdentityId, async, cancellationToken);
        }

        // TODO: Implement this method when static ManagedIdentityApplication.IsProofOfPossessionSupportedByClient is implemented
        public static bool IsProofOfPossessionSupportedByClient => ManagedIdentityApplication.GetBindingCertificate() is not null;

        public static X509Certificate2 BindingCertificate => ManagedIdentityApplication.GetBindingCertificate();

        internal virtual ValueTask<IManagedIdentityApplication> CreateClientCoreAsync(bool enableCae, ManagedIdentityId managedIdentityId, bool async, CancellationToken cancellationToken)
        {
            ManagedIdentityApplicationBuilder miAppBuilder = ManagedIdentityApplicationBuilder
                .Create(managedIdentityId)
                .WithHttpClientFactory(new HttpPipelineClientFactory(Pipeline.HttpPipeline))
                .WithLogging(LogMsal, enablePiiLogging: IsSupportLoggingEnabled);

            if (enableCae)
            {
                miAppBuilder.WithClientCapabilities(cp1Capabilities);
            }

            return new ValueTask<IManagedIdentityApplication>(miAppBuilder.Build());
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
