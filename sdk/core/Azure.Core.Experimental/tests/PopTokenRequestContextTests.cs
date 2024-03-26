// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net.Http;
using Azure.Core.TestFramework;
using NUnit.Framework;

namespace Azure.Core
{
    public class PopTokenRequestContextTests
    {
        [Test]
        public void CanPopulatePropertyBag()
        {
            using Request request = new MockRequest
            {
                Method = RequestMethod.Put
            };
            request.Uri.Reset(new Uri("https://foo"));

            var context = new PopTokenRequestContext(
                scopes: new string[] { "scope1", "scope2" },
                isCaeEnabled: true,
                isProofOfPossessionEnabled: true,
                properties: new (Type Key, object Value)[]
                {
                    (typeof(PopDetails), new PopDetails(request, "myNonce")),
                    (typeof(CaeDetails), new CaeDetails(allowCaeFallback: true))
                });

            ValidateContext(context);
        }

        [Test]
        public void CanPopulatePropertyBagWithHelper()
        {
            using Request request = new MockRequest
            {
                Method = RequestMethod.Put
            };
            request.Uri.Reset(new Uri("https://foo"));

            var context = new PopTokenRequestContext(
                new string[] { "scope1", "scope2" },
                "parentRequestId",
                "claims",
                "tenantId",
                isCaeEnabled: true,
                isProofOfPossessionEnabled: true);

            context = context.WithProperties(
                    (typeof(PopDetails), new PopDetails(request, "myNonce")),
                    (typeof(CaeDetails), new CaeDetails(allowCaeFallback: true)));

            ValidateContext(context);
        }

        private static void ValidateContext(PopTokenRequestContext context)
        {
            Assert.AreEqual(2, context.Scopes.Length);
            Assert.AreEqual("scope1", context.Scopes[0]);
            Assert.AreEqual("scope2", context.Scopes[1]);
            Assert.AreEqual("parentRequestId", context.ParentRequestId);
            Assert.AreEqual("claims", context.Claims);
            Assert.AreEqual("tenantId", context.TenantId);
            Assert.IsTrue(context.IsCaeEnabled);
            Assert.IsTrue(context.IsProofOfPossessionEnabled);

            Assert.True(context.TryGetProperty(typeof(PopDetails), out var popDetailsObj));
            var popDetails = (PopDetails)popDetailsObj;
            Assert.AreEqual(HttpMethod.Put, popDetails.HttpMethod);
            Assert.AreEqual(new Uri("https://foo"), popDetails.Uri);
            Assert.AreEqual("myNonce", popDetails.ProofOfPossessionNonce);

            Assert.True(context.TryGetProperty(typeof(CaeDetails), out var caeDetailsObj));
            var caeDetails = (CaeDetails)caeDetailsObj;
            Assert.IsTrue(caeDetails.AllowCaeFallback);
        }

        private struct PopDetails
        {
            public PopDetails(Request request, string proofOfPossessionNonce = null)
            {
                _request = request;
                ProofOfPossessionNonce = proofOfPossessionNonce;
            }

            private readonly Request _request;

            public string ProofOfPossessionNonce { get; }

            /// <summary>
            /// The HTTP method of the request. This is used in combination with <see cref="Uri"/> and <see cref="ProofOfPossessionNonce"/> to generate the PoP token.
            /// </summary>
            public HttpMethod HttpMethod => new(_request!.Method.ToString());

            /// <summary>
            /// The URI of the request. This is used in combination with <see cref="HttpMethod"/> and <see cref="ProofOfPossessionNonce"/> to generate the PoP token.
            /// </summary>
            public Uri Uri => _request?.Uri.ToUri();
        }

        private struct CaeDetails
        {
            public CaeDetails(bool allowCaeFallback)
            {
                AllowCaeFallback = allowCaeFallback;
            }

            public bool AllowCaeFallback { get; }
        }
    }

#pragma warning disable SA1402 // File may only contain a single type
    public static class TrcExtensions
#pragma warning restore SA1402 // File may only contain a single type
    {
        public static PopTokenRequestContext WithProperties(this PopTokenRequestContext context, params (Type Key, object Value)[] properties)
        {
            return new PopTokenRequestContext(
                context.Scopes,
                context.ParentRequestId,
                context.Claims,
                context.TenantId,
                context.IsCaeEnabled,
                context.IsProofOfPossessionEnabled,
                context.ProofOfPossessionNonce,
                properties: properties);
        }
    }
}
