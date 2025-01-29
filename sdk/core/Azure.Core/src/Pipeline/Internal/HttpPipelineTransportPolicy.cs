// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace Azure.Core.Pipeline
{
    internal class HttpPipelineTransportPolicy : HttpPipelinePolicy
    {
        private volatile HttpPipelineTransport _transport;
        private readonly HttpMessageSanitizer _sanitizer;
        private readonly RequestFailedDetailsParser? _errorParser;

        public HttpPipelineTransportPolicy(HttpPipelineTransport transport, HttpMessageSanitizer sanitizer, RequestFailedDetailsParser? failureContentExtractor = null)
        {
            _transport = transport;
            _sanitizer = sanitizer;
            _errorParser = failureContentExtractor;
        }

        public override async ValueTask ProcessAsync(HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline)
        {
            Debug.Assert(pipeline.IsEmpty);
            var transport = _transport;
            await transport.ProcessAsync(message).ConfigureAwait(false);

            message.Response.RequestFailedDetailsParser = _errorParser;
            message.Response.Sanitizer = _sanitizer;
            message.Response.IsError = message.ResponseClassifier.IsErrorResponse(message);
        }

        public override void Process(HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline)
        {
            Debug.Assert(pipeline.IsEmpty);

            var transport = _transport;
            transport.Process(message);

            message.Response.RequestFailedDetailsParser = _errorParser;
            message.Response.Sanitizer = _sanitizer;
            message.Response.IsError = message.ResponseClassifier.IsErrorResponse(message);
        }

        internal void UpdateTransportWithClientCertificate(X509Certificate2 certificate, HttpPipelineTransportOptions options)
        {
#pragma warning disable CA1416 // Validate platform compatibility
            options.ClientCertificates.Clear();
            options.ClientCertificates.Add(certificate);
#pragma warning restore CA1416 // Validate platform compatibility
            _transport = HttpClientTransport.Create(options);
        }
    }
}
