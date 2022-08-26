// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Azure.Core;
using Azure.Core.TestFramework;
using Azure.Storage;
using NUnit.Framework;

namespace Azure.Data.Tables.Tests
{
    public class CosmosGeoredundantPolicyTests
    {
        private static readonly Uri MockPrimaryUri = new Uri("https://myaccount.table.cosmos.azure.com");
        private static readonly List<string> SecondaryReadHosts = new() { "account-region1.table.cosmos.azure.com", "account-region2.table.cosmos.azure.com" };
        private static readonly List<string> SecondaryWriteHosts = new() { "account-region3.table.cosmos.azure.com", "account-region4.table.cosmos.azure.com" };

        [Test]
        public void OnSendingRequestFirstTry_ShouldUsePrimary_ShouldSetAlternateToSecondary([Values("GET", "POST")] string method)
        {
            var message = new HttpMessage(
                CreateMockRequest(MockPrimaryUri, method),
                new ResponseClassifier());

            var policy = new CosmosGeoredundantPolicy(SecondaryReadHosts, SecondaryWriteHosts);

            policy.OnSendingRequest(message);

            Assert.AreEqual(MockPrimaryUri.Host, message.Request.Uri.Host);
            Assert.IsTrue(message.TryGetProperty(CosmosGeoredundantPolicy.AlternateHostIndexKey, out object val) && (int?)val == 0);
        }

        [Test]
        public void OnSendingRequest_ShouldUsePrimary_NoAlternatesConfigured([Values("GET", "POST")] string method)
        {
            var message = new HttpMessage(
                CreateMockRequest(MockPrimaryUri, method),
                new ResponseClassifier());

            var policy = new CosmosGeoredundantPolicy(new List<string>(), new List<string>());

            policy.OnSendingRequest(message);
            policy.OnSendingRequest(message);
            policy.OnSendingRequest(message);
            policy.OnSendingRequest(message);

            Assert.AreEqual(MockPrimaryUri.Host, message.Request.Uri.Host);
            Assert.False(message.TryGetProperty(CosmosGeoredundantPolicy.AlternateHostIndexKey, out _));
        }

        [Test]
        public void OnSendingRequestSecondTry_ShouldUseSecondary([Values("GET", "POST")] string method)
        {
            var message = new HttpMessage(
                 CreateMockRequest(MockPrimaryUri, method),
                 new ResponseClassifier());

            var policy = new CosmosGeoredundantPolicy(SecondaryReadHosts, SecondaryWriteHosts);

            policy.OnSendingRequest(message);
            policy.OnSendingRequest(message);

            var hostList = method switch
            {
                "GET" => SecondaryReadHosts,
                _ => SecondaryWriteHosts
            };

            Assert.IsTrue(message.TryGetProperty(CosmosGeoredundantPolicy.AlternateHostIndexKey, out object val));
            int? currentIndex = (int?)val;

            Assert.AreEqual(hostList[currentIndex.Value - 1], message.Request.Uri.Host);
            Assert.AreEqual(1, currentIndex.Value);
        }

        [Test]
        public void OnSendingRequestThirdTry_ShouldUseTertiary([Values("GET", "POST")] string method)
        {
            var message = new HttpMessage(
                 CreateMockRequest(MockPrimaryUri, method),
                 new ResponseClassifier());

            var policy = new CosmosGeoredundantPolicy(SecondaryReadHosts, SecondaryWriteHosts);
            policy.OnSendingRequest(message);
            policy.OnSendingRequest(message);
            policy.OnSendingRequest(message);

            var hostList = method switch
            {
                "GET" => SecondaryReadHosts,
                _ => SecondaryWriteHosts
            };

            Assert.IsTrue(message.TryGetProperty(CosmosGeoredundantPolicy.AlternateHostIndexKey, out object val));
            int? currentIndex = (int?)val;

            Assert.AreEqual(hostList[currentIndex.Value - 1], message.Request.Uri.Host);
            Assert.AreEqual(2, currentIndex.Value);
        }

        [Test]
        public void OnSendingRequestMoreThanHostCount_ShouldCycleThroughHosts([Values("GET", "POST")] string method)
        {
            var message = new HttpMessage(
                 CreateMockRequest(MockPrimaryUri, method),
                 new ResponseClassifier());

            var hostList = method switch
            {
                "GET" => SecondaryReadHosts,
                _ => SecondaryWriteHosts
            };

            var policy = new CosmosGeoredundantPolicy(SecondaryReadHosts, SecondaryWriteHosts);

            for (int i = 0; i < 100; i++)
            {
                policy.OnSendingRequest(message);

                Assert.IsTrue(message.TryGetProperty(CosmosGeoredundantPolicy.AlternateHostIndexKey, out object val));
                int? currentIndex = (int?)val;

                if (i == 0)
                {
                    Assert.AreEqual(MockPrimaryUri.Host, message.Request.Uri.Host);
                }
                else
                {
                    Assert.AreEqual(hostList[(currentIndex.Value - 1) % hostList.Count], message.Request.Uri.Host, $"Index was {i}");
                }
                Assert.AreEqual(i, currentIndex.Value);
            }
        }

        private MockRequest CreateMockRequest(Uri uri, string method)
        {
            MockRequest mockRequest = new MockRequest();
            mockRequest.Uri.Reset(uri);
            mockRequest.Method = new RequestMethod(method);
            return mockRequest;
        }
    }
}
