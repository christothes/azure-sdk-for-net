// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
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
            var cred = MsalManagedIdentityClient.IsProofOfPossessionSupportedByClient ?
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
            return new AccessToken(result.AccessToken, result.ExpiresOn);
        }

        protected override Request CreateRequest(string[] scopes)
        {
            throw new System.NotImplementedException();
        }
    }
}
