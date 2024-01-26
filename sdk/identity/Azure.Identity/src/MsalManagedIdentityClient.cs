// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
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

            // if (options is IMsalPublicClientInitializerOptions initializerOptions)
            // {
            //     _beforeBuildClient = initializerOptions.BeforeBuildClient;
            //     IsProofOfPossessionRequired = initializerOptions.IsProofOfPossessionRequired;
            // };
        }

        protected ValueTask<IManagedIdentityApplication> CreateClientAsync(bool enableCae, ManagedIdentityId managedIdentityId, bool async, CancellationToken cancellationToken)
        {
            return CreateClientCoreAsync(enableCae, managedIdentityId, async, cancellationToken);
        }

        protected virtual ValueTask<IManagedIdentityApplication> CreateClientCoreAsync(bool enableCae, ManagedIdentityId managedIdentityId, bool async, CancellationToken cancellationToken)
        {
            string[] clientCapabilities =
                enableCae ? cp1Capabilities : Array.Empty<string>();

            ManagedIdentityApplicationBuilder miAppBuilder = ManagedIdentityApplicationBuilder
                .Create(managedIdentityId)
                .WithHttpClientFactory(new HttpPipelineClientFactory(Pipeline.HttpPipeline))
                .WithLogging(LogMsal, enablePiiLogging: IsSupportLoggingEnabled);

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
