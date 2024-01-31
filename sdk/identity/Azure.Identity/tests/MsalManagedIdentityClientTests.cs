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
        public async Task Ctor()
        {
            var client = new MsalManagedIdentityClient(CredentialPipeline.GetInstance(null), new TokenCredentialOptions());
            await client.CreateClientCoreAsync(false, ManagedIdentityId.SystemAssigned, false, default);
        }
    }
}
