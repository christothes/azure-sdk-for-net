// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Azure.Core.Pipeline;
using Azure.Core.TestFramework;
using NUnit.Framework;

namespace Azure.Core.Tests
{
    public class ExponentialPolicyTest : RetryPolicyTestBase
    {
        public ExponentialPolicyTest(bool isAsync) : base(RetryMode.Exponential, isAsync) { }

        [Test]
        public async Task WaitsCorrectAmountBetweenTries()
        {
            int maxRetries = 4;
            var responseClassifier = new MockResponseClassifier(retriableCodes: new[] { 500 });
            var time = new ZeroDelayTimeProvider();
            var policy = new RetryPolicy(
                maxRetries,
                DelayStrategy.CreateExponentialDelayStrategy(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10)),
                time);
            MockTransport mockTransport = CreateMockTransport(_ => new MockResponse(500));

            var response = await SendGetRequest(mockTransport, policy, responseClassifier);

            for (int i = 0; i < maxRetries; i++)
            {
                AssertExponentialDelay(TimeSpan.FromSeconds(1 << i), time.ObservedDelays[i]);
            }

            Assert.AreEqual(500, response.Status);
        }

        [Test]
        public async Task WaitsCorrectAmountBetweenTries_Exceptions()
        {
            int maxRetries = 4;
            var responseClassifier = new MockResponseClassifier(retriableCodes: new[] { 500 }, exceptionFilter: ex => ex is IOException);
            var time = new ZeroDelayTimeProvider();
            var policy = new RetryPolicy(
                maxRetries,
                DelayStrategy.CreateExponentialDelayStrategy(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10)),
                time);
            int callCount = 0;
            MockTransport mockTransport = CreateMockTransport(_ =>
            {
                if (callCount++ == 1) throw new IOException();
                return new MockResponse(500);
            });
            var response = await SendGetRequest(mockTransport, policy, responseClassifier);
            var expectedDelaysInSeconds = new int[] { 1, 2, 4, 8 };

            for (int i = 0; i < expectedDelaysInSeconds.Length; i++)
            {
                AssertExponentialDelay(TimeSpan.FromSeconds(expectedDelaysInSeconds[i]), time.ObservedDelays[i]);
            }

            Assert.AreEqual(500, response.Status);
        }

        [Test]
        public async Task RespectsMaxDelay()
        {
            var responseClassifier = new MockResponseClassifier(retriableCodes: new[] { 500 });
            var policy = new RetryPolicyMock(RetryMode.Exponential, maxRetries: 6, delay: TimeSpan.FromSeconds(1), maxDelay: TimeSpan.FromSeconds(5));
            MockTransport mockTransport = CreateMockTransport();
            Task<Response> task = SendGetRequest(mockTransport, policy, responseClassifier);
            var expectedDelaysInSeconds = new int[] { 1, 2, 4, 5, 5, 5 };

            await mockTransport.RequestGate.Cycle(new MockResponse(500));

            for (int i = 0; i < 6; i++)
            {
                TimeSpan delay = await policy.DelayGate.Cycle();
                AssertExponentialDelay(TimeSpan.FromSeconds(expectedDelaysInSeconds[i]), delay);

                await mockTransport.RequestGate.Cycle(new MockResponse(500));
            }

            Response response = await task.TimeoutAfterDefault();
            Assert.AreEqual(500, response.Status);
        }

        [Theory]
        [TestCase(2, 3, 3)]
        [TestCase(3, 2, 3)]
        [TestCase(3, 10, 10)]
        public async Task UsesLargerOfDelayAndServerDelay(int delay, int retryAfter, int expected)
        {
            var responseClassifier = new MockResponseClassifier(retriableCodes: new[] { 500 });
            var policy = new RetryPolicyMock(RetryMode.Exponential, delay: TimeSpan.FromSeconds(delay), maxDelay: TimeSpan.FromSeconds(5));
            MockTransport mockTransport = CreateMockTransport();
            Task<Response> task = SendGetRequest(mockTransport, policy, responseClassifier);

            MockResponse mockResponse = new MockResponse(500);
            mockResponse.AddHeader(new HttpHeader("Retry-After", retryAfter.ToString()));

            await mockTransport.RequestGate.Cycle(mockResponse);

            TimeSpan retryDelay = await policy.DelayGate.Cycle();

            await mockTransport.RequestGate.Cycle(new MockResponse(501));

            Response response = await task.TimeoutAfterDefault();

            AssertExponentialDelay(TimeSpan.FromSeconds(expected), retryDelay);
            Assert.AreEqual(501, response.Status);
        }

        private void AssertExponentialDelay(TimeSpan expected, TimeSpan actual)
        {
            // Expect maximum 25% variance
            Assert.LessOrEqual(Math.Abs(expected.TotalMilliseconds / actual.TotalMilliseconds - 1), 0.25, "Expected {0} to be around {1}", actual, expected);
        }
    }
}
