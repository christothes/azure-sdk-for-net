// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Learn.AppConfig.Models;

namespace Azure.Learn.AppConfig
{
    /// <summary>
    ///
    /// </summary>
    public class ConfigurationClient
    {
        private readonly Uri _endpoint;
        private readonly HttpPipeline _pipeline;
        private readonly ClientDiagnostics _clientDiagnostics;
        private readonly AzureAppConfigurationRestClient _restClient;

        /// <summary>Initializes a new instance of the <see cref="ConfigurationClient"/>.</summary>
        public ConfigurationClient(Uri endpoint, TokenCredential credential) : this(endpoint, credential, new ConfigurationClientOptions())
        {
        }

        /// <summary>Initializes a new instance of the <see cref="ConfigurationClient"/>.</summary>
#pragma warning disable CA1801 // Parameter is never used
        public ConfigurationClient(Uri endpoint, TokenCredential credential, ConfigurationClientOptions options)
        {
            Argument.AssertNotNull(endpoint, nameof(endpoint));
            Argument.AssertNotNull(credential, nameof(credential));
            Argument.AssertNotNull(options, nameof(options));

            _endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));

            // Add the authentication policy to our builder.
            _pipeline = HttpPipelineBuilder.Build(options, new BearerTokenAuthenticationPolicy(credential, GetDefaultScope(endpoint)));

            // Initialize the ClientDiagnostics.
            _clientDiagnostics = new ClientDiagnostics(options);

            _restClient = new AzureAppConfigurationRestClient(_clientDiagnostics, _pipeline, _endpoint.AbsoluteUri, options.Version);
        }
#pragma warning restore CA1801 // Parameter is never used

        /// <summary> Initializes a new instance of ConfigurationClient for mocking. </summary>
        protected ConfigurationClient()
        {
        }

        // ...

        /// <summary>Retrieve a <see cref="ConfigurationSetting"/> from the configuration store.</summary>
        public virtual Response<ConfigurationSetting> GetConfigurationSetting(string key, string label = null, CancellationToken cancellationToken = default)
        {
            Argument.AssertNotNullOrEmpty(key, nameof(key));
            
            return _restClient.GetKeyValue(key, label, cancellationToken: cancellationToken);
        }

        /// <summary>Retrieve a <see cref="ConfigurationSetting"/> from the configuration store.</summary>
        public virtual async Task<Response<ConfigurationSetting>> GetConfigurationSettingAsync(string key, string label = null, CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            throw new NotImplementedException();
        }

        // A helper method to construct the default scope based on the service endpoint.
        private static string GetDefaultScope(Uri uri)
                => $"{uri.GetComponents(UriComponents.SchemeAndServer, UriFormat.SafeUnescaped)}/.default";
    }
}
