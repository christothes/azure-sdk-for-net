// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Security.Cryptography.X509Certificates;
using Azure.Core;
using Azure.Core.Pipeline;
using Microsoft.Identity.Client;

namespace Azure.Identity
{
    internal class CredentialPipeline
    {
        private static readonly Lazy<CredentialPipeline> s_singleton = new Lazy<CredentialPipeline>(() => new CredentialPipeline(new TokenCredentialOptions()));
        private static readonly ConcurrentDictionary<X509Certificate2, CredentialPipeline> s_certifcatePipelines = new ConcurrentDictionary<X509Certificate2, CredentialPipeline>();

        private static readonly IScopeHandler _defaultScopeHandler = new ScopeHandler();

        private CredentialPipeline(TokenCredentialOptions options)
        {
            HttpPipeline = HttpPipelineBuilder.Build(new HttpPipelineOptions(options) { RequestFailedDetailsParser = new ManagedIdentityRequestFailedDetailsParser() });
            Diagnostics = new ClientDiagnostics(options);
        }

        private CredentialPipeline(TokenCredentialOptions options, X509Certificate2 clientCertifcate)
        {
            HttpPipeline = HttpPipelineBuilder.Build(
                new HttpPipelineOptions(options) { RequestFailedDetailsParser = new ManagedIdentityRequestFailedDetailsParser() },
                new HttpPipelineTransportOptions { ClientCertificates = { clientCertifcate } });
            Diagnostics = new ClientDiagnostics(options);
        }

        public CredentialPipeline(HttpPipeline httpPipeline, ClientDiagnostics diagnostics)
        {
            HttpPipeline = httpPipeline;
            Diagnostics = diagnostics;
        }

        public static CredentialPipeline GetInstance(TokenCredentialOptions options)
        {
            return options is null ? s_singleton.Value : new CredentialPipeline(options);
        }

        public static CredentialPipeline GetInstance(TokenCredentialOptions options, X509Certificate2 clientCertifcate)
        {
            return options switch
            {
                null => s_certifcatePipelines.GetOrAdd(clientCertifcate, cert => new CredentialPipeline(new TokenCredentialOptions(), cert)),
                _ => new CredentialPipeline(options, clientCertifcate)
            };
        }

        public HttpPipeline HttpPipeline { get; }

        public ClientDiagnostics Diagnostics { get; }

        public IConfidentialClientApplication CreateMsalConfidentialClient(string tenantId, string clientId, string clientSecret)
        {
            return ConfidentialClientApplicationBuilder.Create(clientId).WithHttpClientFactory(new HttpPipelineClientFactory(HttpPipeline)).WithTenantId(tenantId).WithClientSecret(clientSecret).Build();
        }

        public CredentialDiagnosticScope StartGetTokenScope(string fullyQualifiedMethod, TokenRequestContext context)
        {
            IScopeHandler scopeHandler = ScopeGroupHandler.Current ?? _defaultScopeHandler;

            CredentialDiagnosticScope scope = new CredentialDiagnosticScope(Diagnostics, fullyQualifiedMethod, context, scopeHandler);
            scope.Start();
            return scope;
        }
#if PREVIEW_FEATURE_FLAG
        public CredentialDiagnosticScope StartGetTokenScope(string fullyQualifiedMethod, PopTokenRequestContext context)
        {
            IScopeHandler scopeHandler = ScopeGroupHandler.Current ?? _defaultScopeHandler;

            CredentialDiagnosticScope scope = new CredentialDiagnosticScope(Diagnostics, fullyQualifiedMethod, context, scopeHandler);
            scope.Start();
            return scope;
        }
#endif
        public CredentialDiagnosticScope StartGetTokenScopeGroup(string fullyQualifiedMethod, TokenRequestContext context)
        {
            var scopeHandler = new ScopeGroupHandler(fullyQualifiedMethod);

            CredentialDiagnosticScope scope = new CredentialDiagnosticScope(Diagnostics, fullyQualifiedMethod, context, scopeHandler);
            scope.Start();
            return scope;
        }

        private class CredentialResponseClassifier : ResponseClassifier
        {
            public override bool IsRetriableResponse(HttpMessage message)
            {
                return base.IsRetriableResponse(message) || message.Response.Status == 404;
            }
        }

        private class ScopeHandler : IScopeHandler
        {
            public DiagnosticScope CreateScope(ClientDiagnostics diagnostics, string name) => diagnostics.CreateScope(name);
            public void Start(string name, in DiagnosticScope scope) => scope.Start();
            public void Dispose(string name, in DiagnosticScope scope) => scope.Dispose();
            public void Fail(string name, in DiagnosticScope scope, Exception exception) => scope.Failed(exception);
        }
    }
}
