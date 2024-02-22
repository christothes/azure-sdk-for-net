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
        /// <param name="accessToken">The access token value.</param>
        /// <param name="expiresOn">The access token expiry date.</param>
        /// <param name="tokenType">The access token type.</param>
        public AccessToken(string accessToken, DateTimeOffset expiresOn, string tokenType)
        {
            Token = accessToken;
            ExpiresOn = expiresOn;
            TokenType = tokenType;
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
        /// Identifies the type of access token.
        /// </summary>
        public string TokenType { get; }

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
