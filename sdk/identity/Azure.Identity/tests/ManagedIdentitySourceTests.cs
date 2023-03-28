// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Core.TestFramework;
using Azure.Identity.Tests.Mock;
using NUnit.Framework;

namespace Azure.Identity.Tests
{
    [TestFixture(true)]
    [TestFixture(false)]
    public class ManagedIdentitySourceTests
    {
        private bool IsAsync;
        private DateTimeOffset now;
        private DateTimeOffset expires;

        public ManagedIdentitySourceTests(bool isAsync)
        {
            IsAsync = isAsync;
        }

        [SetUp]
        public void Setup()
        {
            now = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            expires = now.AddHours(24);
        }

        [Test]
        public async Task TokenRefreshInIs12HoursFromNow()
        {
            var response = CreateMockResponse(200, TokenGenerator.GenerateToken(Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), "myUpn", expires.UtcDateTime, now), expires, TimeSpan.FromHours(12).TotalSeconds);
            var mockTransport = new MockTransport(response);
            var target = new MockIdentitySourceClient(CredentialPipeline.GetInstance(new TokenCredentialOptions() { Transport = mockTransport }));

            AccessToken token = await target.AuthenticateAsync(IsAsync, new TokenRequestContext(MockScopes.Default), default);

            Assert.Less(token.RefreshOn, token.ExpiresOn);
            Assert.AreEqual(now.AddHours(12), token.RefreshOn);
        }

        [Test]
        public async Task TokenRefreshInNotPresent()
        {
            var response = CreateMockResponse(200, TokenGenerator.GenerateToken(Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), "myUpn", expires.UtcDateTime, now), expires);
            var mockTransport = new MockTransport(response);
            var target = new MockIdentitySourceClient(CredentialPipeline.GetInstance(new TokenCredentialOptions() { Transport = mockTransport }));

            AccessToken token = await target.AuthenticateAsync(IsAsync, new TokenRequestContext(MockScopes.Default), default);

            Assert.Less(token.RefreshOn, token.ExpiresOn);
            Assert.LessOrEqual(token.RefreshOn, now.AddSeconds(5).AddSeconds((long)(expires - now).TotalSeconds / 2), "RefreshOn should be less than half the token lifetime (with a fudge factor of 5 seconds to account for 'now' calculation)");
        }

        private MockResponse CreateMockResponse(int responseCode, string token, DateTimeOffset expiresOn, double? refreshIn = null)
        {
            var response = new MockResponse(responseCode);
            string json = refreshIn.HasValue ?
            $"{{ \"access_token\": \"{token}\", \"expires_on\": \"{expiresOn.ToUnixTimeSeconds()}\", \"refresh_in\": \"{refreshIn.Value}\" }}" :
            $"{{ \"access_token\": \"{token}\", \"expires_on\": \"{expiresOn:o}\"}}";
            response.SetContent(json);
            return response;
        }

        private MockResponse CreateErrorMockResponse(int responseCode, string message)
        {
            var response = new MockResponse(responseCode);
            response.SetContent($"{{\"StatusCode\":400,\"Message\":\"{message}\",\"CorrelationId\":\"f3c9aec0-7fa2-4184-ad0f-0c68ce5fc748\"}}");
            return response;
        }

        private static MockResponse CreateInvalidJsonResponse(int status)
        {
            var response = new MockResponse(status);
            response.SetContent("invalid json");
            return response;
        }

        private class MockIdentitySourceClient : ManagedIdentitySource
        {
            public MockIdentitySourceClient(CredentialPipeline pipeline) : base(pipeline)
            {
            }

            protected override Request CreateRequest(string[] scopes)
            {
                var request = new MockRequest();
                request.Uri = new RequestUriBuilder();
                request.Uri.Reset(new Uri("http://169.254.169.254/metadata/identity/oauth2/token"));
                request.Method = RequestMethod.Get;
                return request;
            }
        }
    }
}
