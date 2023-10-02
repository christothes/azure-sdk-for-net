// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Azure.Core.Pipeline
{
    internal class ReadClientRequestIdPolicy : HttpPipelineSynchronousPolicy
    {
        public const string MessagePropertyKey = "x-ms-client-request-id";

        protected ReadClientRequestIdPolicy()
        {
        }

        public static ReadClientRequestIdPolicy Shared { get; } = new ReadClientRequestIdPolicy();

        public override void OnSendingRequest(HttpMessage message)
        {
            string ClientRequestIdHeaderName = ClientRequestIdPolicy.ClientRequestIdHeader;
            if (message.TryGetProperty(typeof(CustomClientRequestIdHeaderNameValueKey), out object? propertyValue) && propertyValue is string customerHeaderName)
            {
                ClientRequestIdHeaderName = customerHeaderName;
            }

            if (message.Request.Headers.TryGetValue(ClientRequestIdHeaderName, out string? value))
            {
                message.Request.ClientRequestId = value;
            }
            else if (message.TryGetProperty(MessagePropertyKey, out propertyValue))
            {
                if (propertyValue is string stringValue)
                {
                    message.Request.ClientRequestId = stringValue;
                }
                else
                {
                    throw new ArgumentException($"{MessagePropertyKey} http message property must be a string but was {propertyValue?.GetType()}");
                }
            }
        }
    }
}
