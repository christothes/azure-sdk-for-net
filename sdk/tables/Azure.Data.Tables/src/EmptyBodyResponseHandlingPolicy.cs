// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Core;
using Azure.Core.Pipeline;

namespace Azure.Data.Tables
{
    /// <summary>
    /// Pipeline policy that detects HTTP success responses (200, 201) with empty bodies and throws
    /// <see cref="RequestFailedException"/> instead of allowing downstream JSON/XML parsing to fail
    /// with an unhelpful <see cref="System.Text.Json.JsonException"/>.
    /// Cosmos DB Tables API may return HTTP 200 with Content-Length: 0 when throttling GET requests
    /// instead of returning 429.
    /// See https://github.com/Azure/azure-sdk-for-net/issues/55632
    /// </summary>
    internal sealed class EmptyBodyResponseHandlingPolicy : HttpPipelineSynchronousPolicy
    {
        public override void OnReceivedResponse(HttpMessage message)
        {
            base.OnReceivedResponse(message);

            if (message.Response.Status is 200 or 201 && !HasContent(message.Response))
            {
                throw new RequestFailedException(message.Response);
            }
        }

        private static bool HasContent(Response response)
        {
            if (response.ContentStream == null)
            {
                return false;
            }

            if (response.ContentStream.CanSeek)
            {
                return response.ContentStream.Length - response.ContentStream.Position > 0;
            }

            // Fall back to checking Content-Length header for non-seekable streams.
            if (response.Headers.TryGetValue("Content-Length", out string contentLength)
                && int.TryParse(contentLength, out int length))
            {
                return length > 0;
            }

            // If we can't determine, assume there's content and let the parser handle it.
            return true;
        }
    }
}
