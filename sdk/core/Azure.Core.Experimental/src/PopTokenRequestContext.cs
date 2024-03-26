// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http;

namespace Azure.Core
{
    /// <summary>
    /// Contains the details of an authentication token request.
    /// </summary>
    public readonly struct PopTokenRequestContext
    {
        /// <summary>
        /// Creates a new TokenRequest with the specified scopes.
        /// </summary>
        /// <param name="scopes">The scopes required for the token.</param>
        /// <param name="parentRequestId">The <see cref="Request.ClientRequestId"/> of the request requiring a token for authentication, if applicable.</param>
        /// <param name="claims">Additional claims to be included in the token.</param>
        /// <param name="tenantId">The tenant ID to be included in the token request.</param>
        /// <param name="isCaeEnabled">Indicates whether to enable Continuous Access Evaluation (CAE) for the requested token.</param>
        /// <param name="isProofOfPossessionEnabled">Indicates whether to enable Proof of Possession (PoP) for the requested token.</param>
        /// <param name="proofOfPossessionNonce">The nonce value required for PoP token requests.</param>
        /// <param name="request">The request to be authorized with a PoP token.</param>
        /// <param name="properties"></param>
        public PopTokenRequestContext(
            string[] scopes,
            string? parentRequestId = default,
            string? claims = default,
            string? tenantId = default,
            bool isCaeEnabled = false,
            bool isProofOfPossessionEnabled = false,
            string? proofOfPossessionNonce = default,
            Request? request = default,
            IEnumerable<(Type Key, object Value)>? properties = default)
        {
            var props = new ArrayBackedPropertyBag<ulong, object>();
            if (properties != null)
            {
                foreach (var property in properties)
                {
                    if (!props.TryAdd((ulong)property.Key.TypeHandle.Value, property.Value, out _))
                    {
                        throw new InvalidOperationException("A property with the same key already exists.");
                    }
                }
                _propertyBag = props;
            }
            Scopes = scopes;
            ParentRequestId = parentRequestId;
            Claims = claims;
            TenantId = tenantId;
            IsCaeEnabled = isCaeEnabled;
            IsProofOfPossessionEnabled = isProofOfPossessionEnabled;
            ProofOfPossessionNonce = proofOfPossessionNonce;
            _request = request;
        }

        /// <summary>
        /// Creates a new TokenRequestContext from this instance.
        /// </summary>
        /// <returns>A <see cref="TokenRequestContext"/>.</returns>
        public TokenRequestContext ToTokenRequestContext()
        {
            return new TokenRequestContext(Scopes, ParentRequestId, Claims, TenantId, IsCaeEnabled);
        }

        /// <param name="context">The <see cref="TokenRequestContext"/> to use for creation of this instance.</param>
        /// <param name="request">The <see cref="Request"/> to be authenticated.</param>
        /// <param name="isProofOfPossessionEnabled">If <c>true</c> enables Proof of Possession (PoP) for the requested token.</param>
        /// <returns>A <see cref="PopTokenRequestContext"/>.</returns>
        public static PopTokenRequestContext FromTokenRequestContext(TokenRequestContext context, Request? request = default, bool? isProofOfPossessionEnabled = false)
        {
            return new PopTokenRequestContext(context.Scopes, context.ParentRequestId, context.Claims, context.TenantId, context.IsCaeEnabled, isProofOfPossessionEnabled ?? false);
        }

        /// <summary>
        /// Creates a new TokenRequestContext from this instance.
        /// </summary>
        /// <param name="context"></param>
        public static implicit operator TokenRequestContext(PopTokenRequestContext context) => context.ToTokenRequestContext();

        /// <summary>
        /// The scopes required for the token.
        /// </summary>
        public string[] Scopes { get; }

        /// <summary>
        /// The <see cref="Request.ClientRequestId"/> of the request requiring a token for authentication, if applicable.
        /// </summary>
        public string? ParentRequestId { get; }

        /// <summary>
        /// Additional claims to be included in the token. See <see href="https://openid.net/specs/openid-connect-core-1_0-final.html#ClaimsParameter">https://openid.net/specs/openid-connect-core-1_0-final.html#ClaimsParameter</see> for more information on format and content.
        /// </summary>
        public string? Claims { get; }

        /// <summary>
        /// The tenant ID to be included in the token request.
        /// </summary>
        public string? TenantId { get; }

        /// <summary>
        /// Indicates whether to enable Continuous Access Evaluation (CAE) for the requested token.
        /// </summary>
        /// <remarks>
        /// If a resource API implements CAE and your application declares it can handle CAE, your app receives CAE tokens for that resource.
        /// For this reason, if you declare your app CAE-ready, your app must handle the CAE claim challenge for all resource APIs that accept Microsoft Identity access tokens.
        /// If you don't handle CAE responses in these API calls, your app could end up in a loop retrying an API call with a token that is still in the returned lifespan of the token but has been revoked due to CAE.
        /// </remarks>
        public bool IsCaeEnabled { get; }

        /// <summary>
        /// Indicates whether to enable Proof of Possession (PoP) for the requested token.
        /// </summary>
        public bool IsProofOfPossessionEnabled { get; }

        private readonly ArrayBackedPropertyBag<ulong, object> _propertyBag;

        /// <summary>
        /// Gets a property that is stored with this <see cref="HttpMessage"/> instance and can be used for modifying pipeline behavior.
        /// </summary>
        /// <param name="type">The property type.</param>
        /// <param name="value">The property value.</param>
        /// <remarks>
        /// The key value is of type <c>Type</c> for a couple of reasons. Primarily, it allows values to be stored such that though the accessor methods
        /// are public, storing values keyed by internal types make them inaccessible to other assemblies. This protects internal values from being overwritten
        /// by external code. Secondly, <c>Type</c> comparisons are faster than string comparisons.
        /// </remarks>
        /// <returns><c>true</c> if property exists, otherwise. <c>false</c>.</returns>
        public bool TryGetProperty(Type type, out object? value) =>
            _propertyBag.TryGetValue((ulong)type.TypeHandle.Value, out value);

        /// <summary>
        /// The nonce value required for PoP token requests. This is typically retrieved from teh WWW-Authenticate header of a 401 challenge response.
        /// This is used in combination with <see cref="Uri"/> and <see cref="HttpMethod"/> to generate the PoP token.
        /// </summary>
        internal string? ProofOfPossessionNonce { get; }

        private readonly Request? _request;

        /// <summary>
        /// The HTTP method of the request. This is used in combination with <see cref="Uri"/> and <see cref="ProofOfPossessionNonce"/> to generate the PoP token.
        /// </summary>
        internal HttpMethod? HttpMethod => new(_request!.Method.ToString());

        /// <summary>
        /// The URI of the request. This is used in combination with <see cref="HttpMethod"/> and <see cref="ProofOfPossessionNonce"/> to generate the PoP token.
        /// </summary>
        internal Uri? Uri => _request?.Uri.ToUri();
    }
}
