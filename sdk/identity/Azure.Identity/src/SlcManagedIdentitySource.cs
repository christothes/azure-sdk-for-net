// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;
using Azure.Core;

namespace Azure.Identity
{
    internal class SlcManagedIdentitySource : ManagedIdentitySource
    {
        public SlcManagedIdentitySource(ManagedIdentityClientOptions options) : base(options)
        {
        }

        public static ManagedIdentitySource TryCreate(ManagedIdentityClientOptions options)
        {
            return new SlcManagedIdentitySource(options);
        }

        public override ValueTask<AccessToken> GetTokenAsync(bool async, TokenRequestContext context, CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }

        protected override Request CreateRequest(string[] scopes)
        {
            throw new System.NotImplementedException();
        }
    }
}
