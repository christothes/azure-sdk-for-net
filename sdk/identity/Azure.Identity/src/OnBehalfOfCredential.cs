// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Core.Pipeline;
using Microsoft.Identity.Client;

namespace Azure.Identity
{
    /// <summary>
    ///
    /// </summary>
    public class OnBehalfOfCredential : TokenCredential, ISupportsTokenCaching
    {
        private readonly IConfidentialClientApplication _confidentialClient;
        private readonly UserAssertion _userAssertion;

        /// <summary>
        /// Indicates that callers to GetToken should not expect that the credential will perform caching.
        /// </summary>
        /// <value></value>
        public bool BypassCache { get; set; }

        /// <summary>
        ///
        /// </summary>
        /// <param name="clientId"></param>
        /// <param name="clientSecret"></param>
        /// <param name="userAccessToken"></param>
        public OnBehalfOfCredential(string clientId, string clientSecret, string userAccessToken)
        {
            _confidentialClient = ConfidentialClientApplicationBuilder.Create(clientId).WithClientSecret(clientSecret).Build();

            _userAssertion = new UserAssertion(userAccessToken);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="clientId"></param>
        /// <param name="clientSecret"></param>
        public OnBehalfOfCredential(string clientId, string clientSecret)
        {
            _confidentialClient = ConfidentialClientApplicationBuilder.Create(clientId).WithClientSecret(clientSecret).Build();
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="requestContext"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            return GetTokenInternalAsync(requestContext, false, cancellationToken).EnsureCompleted();
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="requestContext"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            return GetTokenInternalAsync(requestContext, true, cancellationToken);
        }

        internal async ValueTask<AccessToken> GetTokenInternalAsync(TokenRequestContext requestContext, bool async, CancellationToken cancellationToken)
        {
            AuthenticationResult result;
            if (async)
            {
                result = await _confidentialClient.AcquireTokenOnBehalfOf(requestContext.Scopes, _userAssertion ?? UserAssertionScope._userAssertion.Value).ExecuteAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
#pragma warning disable AZC0107 // DO NOT call public asynchronous method in synchronous scope.
                result = _confidentialClient.AcquireTokenOnBehalfOf(requestContext.Scopes, _userAssertion ?? UserAssertionScope._userAssertion.Value).ExecuteAsync(cancellationToken).EnsureCompleted();
#pragma warning restore AZC0107 // DO NOT call public asynchronous method in synchronous scope.
            }

            return new AccessToken(result.AccessToken, result.ExpiresOn);
        }
    }
}
