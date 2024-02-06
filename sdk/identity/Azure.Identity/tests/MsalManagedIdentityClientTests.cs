// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using Azure.Core.TestFramework;
using Azure.Identity.Tests.Mock;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.AppConfig;
using NUnit.Framework;

namespace Azure.Identity.Tests
{
    public class MsalManagedIdentityClientTests
    {
        [Test]
        public async Task SLCMiCredTest()
        {
            var cred = new ManagedIdentityCredential();
            await cred.GetTokenAsync(new Core.TokenRequestContext (new[] { "https://storage.azure.com/.default" }), default);
        }
    }
}
