// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Globalization;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;

namespace Azure.Identity
{
    internal abstract class ManagedIdentitySource
    {
        internal const string AuthenticationResponseInvalidFormatError = "Invalid response, the authentication response was not in the expected format.";
        internal const string UnexpectedResponse = "Managed Identity response was not in the expected format. See the inner exception for details.";
        private ManagedIdentityResponseClassifier _responseClassifier;

        protected ManagedIdentitySource(CredentialPipeline pipeline)
        {
            Pipeline = pipeline;
            _responseClassifier = new ManagedIdentityResponseClassifier();
        }

        protected internal CredentialPipeline Pipeline { get; }
        protected internal string ClientId { get; }

        public virtual async ValueTask<AccessToken> AuthenticateAsync(bool async, TokenRequestContext context, CancellationToken cancellationToken)
        {
            using HttpMessage message = CreateHttpMessage(CreateRequest(context.Scopes));
            if (async)
            {
                await Pipeline.HttpPipeline.SendAsync(message, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                Pipeline.HttpPipeline.Send(message, cancellationToken);
            }

            return await HandleResponseAsync(async, context, message.Response, cancellationToken).ConfigureAwait(false);
        }

        protected virtual async ValueTask<AccessToken> HandleResponseAsync(
            bool async,
            TokenRequestContext context,
            Response response,
            CancellationToken cancellationToken)
        {
            string message;
            Exception exception = null;
            try
            {
                using JsonDocument json = async
                    ? await JsonDocument.ParseAsync(response.ContentStream, default, cancellationToken).ConfigureAwait(false)
                    : JsonDocument.Parse(response.ContentStream);
                if (response.Status == 200)
                {
                    return GetTokenFromResponse(json.RootElement);
                }

                message = GetMessageFromResponse(json.RootElement);
            }
            catch (JsonException jex)
            {
                throw new CredentialUnavailableException(UnexpectedResponse, jex);
            }
            catch (Exception e)
            {
                exception = e;
                message = UnexpectedResponse;
            }

            var responseError = new ResponseError(null, message);
            throw async
                ? await Pipeline.Diagnostics.CreateRequestFailedExceptionAsync(response, responseError, innerException: exception).ConfigureAwait(false)
                : Pipeline.Diagnostics.CreateRequestFailedException(response, responseError, innerException: exception);
        }

        protected abstract Request CreateRequest(string[] scopes);

        protected virtual HttpMessage CreateHttpMessage(Request request)
        {
            return new HttpMessage(request, _responseClassifier);
        }

        protected static async Task<string> GetMessageFromResponse(Response response, bool async, CancellationToken cancellationToken)
        {
            if (response?.ContentStream == null || !response.ContentStream.CanRead || response.ContentStream.Length == 0)
            {
                return null;
            }
            response.ContentStream.Position = 0;
            using JsonDocument json = async
                ? await JsonDocument.ParseAsync(response.ContentStream, default, cancellationToken).ConfigureAwait(false)
                : JsonDocument.Parse(response.ContentStream);

            return GetMessageFromResponse(json.RootElement);
        }

        protected static string GetMessageFromResponse(in JsonElement root)
        {
            // Parse the error, if possible
            foreach (var prop in root.EnumerateObject())
            {
                if (prop.Name == "Message")
                {
                    return prop.Value.GetString();
                }
            }
            return null;
        }

        private static AccessToken GetTokenFromResponse(in JsonElement root)
        {
            string accessToken = null;
            DateTimeOffset? expiresOn = null;
            long? refreshIn = null;
            DateTimeOffset? refreshOn = null;

            foreach (JsonProperty prop in root.EnumerateObject())
            {
                switch (prop.Name)
                {
                    case "access_token":
                        accessToken = prop.Value.GetString();
                        break;

                    case "expires_on":
                        expiresOn = TryParseExpiresOn(prop.Value);
                        break;

                    case "refresh_in":
                        refreshIn = TryParseRefreshIn(prop.Value);
                        break;
                }
            }

            refreshOn = TryCalculateRefreshOn(expiresOn, refreshIn, accessToken);

            return accessToken != null && expiresOn.HasValue
                ? new AccessToken(accessToken, expiresOn.Value, refreshOn.Value)
                : throw new AuthenticationFailedException(AuthenticationResponseInvalidFormatError);
        }

        private static DateTimeOffset? TryParseExpiresOn(JsonElement jsonExpiresOn)
        {
            // first test if expiresOn is a unix timestamp either as a number or string
            if (jsonExpiresOn.ValueKind == JsonValueKind.Number && jsonExpiresOn.TryGetInt64(out long expiresOnSec) ||
                jsonExpiresOn.ValueKind == JsonValueKind.String && long.TryParse(jsonExpiresOn.GetString(), out expiresOnSec))
            {
                return DateTimeOffset.FromUnixTimeSeconds(expiresOnSec);
            }
            // otherwise if it is a json string try to parse as a datetime offset
            else if (jsonExpiresOn.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(jsonExpiresOn.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset expiresOn))
            {
                return expiresOn;
            }

            return null;
        }

        private static long? TryParseRefreshIn(JsonElement jsonRetryIn)
        {
            // first test if expiresOn is a unix timestamp either as a number or string
            if (jsonRetryIn.ValueKind == JsonValueKind.Number && jsonRetryIn.TryGetInt64(out long expiresOnSec) ||
                jsonRetryIn.ValueKind == JsonValueKind.String && long.TryParse(jsonRetryIn.GetString(), out expiresOnSec))
            {
                return expiresOnSec;
            }

            return null;
        }

        private static DateTimeOffset? TryCalculateRefreshOn(DateTimeOffset? expiresOn, double? refreshIn, string accessToken)
        {
            if (refreshIn.HasValue && accessToken != null && TokenHelper.TryParseValueFromToken(accessToken, "iat", out long issuedAtSec))
            {
                return DateTimeOffset.FromUnixTimeSeconds(issuedAtSec).AddSeconds(refreshIn.Value);
            }
            else if (expiresOn.HasValue)
            {
                // return the max of (expiresOn - now) / 2 and (2 hours from now)
                return DateTimeOffset.UtcNow.AddSeconds(Math.Max(expiresOn.Value.Subtract(DateTimeOffset.UtcNow).TotalSeconds / 2, 7200));
            }

            // return the max of expiresOn - 5 minutes and now
            return DateTimeOffset.UtcNow.AddSeconds(Math.Max(expiresOn.Value.Subtract(TimeSpan.FromMinutes(5)).ToUnixTimeSeconds(), 0));
        }

        private class ManagedIdentityResponseClassifier : ResponseClassifier
        {
            public override bool IsRetriableResponse(HttpMessage message)
            {
                return message.Response.Status switch
                {
                    404 => true,
                    502 => false,
                    _ => base.IsRetriableResponse(message)
                };
            }
        }
    }
}
