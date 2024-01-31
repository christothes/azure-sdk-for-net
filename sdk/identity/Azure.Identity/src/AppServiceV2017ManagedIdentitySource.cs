// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Azure.Identity
{
    internal class AppServiceV2017ManagedIdentitySource : AppServiceManagedIdentitySource
    {
        protected override string AppServiceMsiApiVersion => "2017-09-01";
        protected override string SecretHeaderName => "secret";
        protected override string ClientIdHeaderName => "clientid";

        public static ManagedIdentitySource TryCreate(ManagedIdentityClientOptions options)
        {
            var msiSecret = EnvironmentVariables.MsiSecret;
            return TryValidateEnvVars(EnvironmentVariables.MsiEndpoint, msiSecret, out Uri endpointUri)
                ? new AppServiceV2017ManagedIdentitySource(endpointUri, msiSecret, options)
                : null;
        }

        private AppServiceV2017ManagedIdentitySource(Uri endpoint, string secret,
            ManagedIdentityClientOptions options) : base(endpoint, secret, options)
        {
        }
    }
}
