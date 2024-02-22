// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;
using Azure.Core;

namespace Azure.Identity
{
    internal class SlcManagedIdentitySource : ManagedIdentitySource
    {
        private readonly MsalManagedIdentityClient _msalClient;

        public SlcManagedIdentitySource(ManagedIdentityClientOptions options) : base(options)
        {
            _msalClient = new MsalManagedIdentityClient(
                options.Pipeline ?? CredentialPipeline.GetInstance(options.Options, MsalManagedIdentityClient.BindingCertificate),
                options);
        }

        public static ManagedIdentitySource TryCreate(ManagedIdentityClientOptions options)
        {
            // Currently, the only way to determine if the SLC is available is to check if the binding certificate is available on the host.
            // This may change in the future.
            var cred = MsalManagedIdentityClient.BindingCertificate != null ?
                new SlcManagedIdentitySource(options) :
                default;
            return cred;
        }

        public override ValueTask<AccessToken> AuthenticateAsync(bool async, TokenRequestContext context, CancellationToken cancellationToken)
        {
            return GetTokenAsync(async, context, cancellationToken);
        }

        public override async ValueTask<AccessToken> GetTokenAsync(bool async, TokenRequestContext context, CancellationToken cancellationToken)
        {
            var result = await _msalClient.AuthenticateAsync(context, async, cancellationToken).ConfigureAwait(false);
            return new AccessToken(result.AccessToken, result.ExpiresOn, result.TokenType);
        }

        protected override Request CreateRequest(string[] scopes)
        {
            throw new System.NotImplementedException();
        }
    }
}
