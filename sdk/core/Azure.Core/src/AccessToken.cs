// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Azure.Core
{
    /// <summary>
    /// Represents an Azure service bearer access token with expiry information.
    /// </summary>
    public struct AccessToken
    {
        /// <summary>
        /// Creates a new instance of <see cref="AccessToken"/> using the provided <paramref name="accessToken"/> and <paramref name="expiresOn"/>.
        /// </summary>
        /// <param name="accessToken">The bearer access token value.</param>
        /// <param name="expiresOn">The bearer access token expiry date.</param>
        public AccessToken(string accessToken, DateTimeOffset expiresOn)
        {
            Token = accessToken;
            ExpiresOn = expiresOn;
            TokenType = "Bearer";
        }

        /// <summary>
        /// Creates a new instance of <see cref="AccessToken"/> using the provided <paramref name="accessToken"/> and <paramref name="expiresOn"/>.
        /// </summary>
        /// <param name="accessToken">The bearer access token value.</param>
        /// <param name="expiresOn">The bearer access token expiry date.</param>
        /// <param name="refreshOn">Specifies the time when the cached token should be proactively refreshed.</param>
        public AccessToken(string accessToken, DateTimeOffset expiresOn, DateTimeOffset? refreshOn)
        {
            Token = accessToken;
            ExpiresOn = expiresOn;
            RefreshOn = refreshOn;
            TokenType = "Bearer";
        }

        /// <summary>
        /// Creates a new instance of <see cref="AccessToken"/> using the provided <paramref name="accessToken"/> and <paramref name="expiresOn"/>.
        /// </summary>
        /// <param name="accessToken">The access token value.</param>
        /// <param name="expiresOn">The access token expiry date.</param>
        /// <param name="refreshOn">Specifies the time when the cached token should be proactively refreshed.</param>
        /// <param name="tokenType">The access token type.</param>
        public AccessToken(string accessToken, DateTimeOffset expiresOn, DateTimeOffset? refreshOn, string tokenType)
        {
            Token = accessToken;
            ExpiresOn = expiresOn;
            RefreshOn = refreshOn;
            TokenType = tokenType;
        }

        /// <summary>
        /// Creates a new instance of <see cref="AccessToken"/> using the provided <paramref name="accessToken"/> and <paramref name="expiresOn"/>.
        /// </summary>
        /// <param name="accessToken">The access token value.</param>
        /// <param name="expiresOn">The access token expiry date.</param>
        /// <param name="refreshOn">Specifies the time when the cached token should be proactively refreshed.</param>
        /// <param name="tokenType">The access token type.</param>
        /// <param name="bindingCertificateThumbprint">The binding certificate thumbprint.</param>
        public AccessToken(string accessToken, DateTimeOffset expiresOn, DateTimeOffset? refreshOn, string tokenType, string? bindingCertificateThumbprint)
        {
            Token = accessToken;
            ExpiresOn = expiresOn;
            RefreshOn = refreshOn;
            TokenType = tokenType;
            BindingCertificateThumbprint = bindingCertificateThumbprint;
        }

        /// <summary>
        /// Get the access token value.
        /// </summary>
        public string Token { get; }

        /// <summary>
        /// Gets the time when the provided token expires.
        /// </summary>
        public DateTimeOffset ExpiresOn { get; }

        /// <summary>
        /// Gets the time when the token should be refreshed.
        /// </summary>
        public DateTimeOffset? RefreshOn { get; }

        /// <summary>
        /// Identifies the type of access token.
        /// </summary>
        public string TokenType { get; }

        /// <summary>
        /// The thumbprint of the certificate used to sign the token. Only available for mTLS PoP tokens.
        /// </summary>
        public string? BindingCertificateThumbprint { get; }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            if (obj is AccessToken accessToken)
            {
                return accessToken.ExpiresOn == ExpiresOn && accessToken.Token == Token && accessToken.TokenType == TokenType;
            }

            return false;
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return HashCodeBuilder.Combine(Token, ExpiresOn, TokenType);
        }
    }
}
