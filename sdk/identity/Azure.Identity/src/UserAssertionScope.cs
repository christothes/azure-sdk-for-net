// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading;
using Microsoft.Identity.Client;

namespace Azure.Identity
{
    /// <summary>
    ///
    /// </summary>
    public class UserAssertionScope : IDisposable
    {
        internal static AsyncLocal<UserAssertion> _userAssertion = new AsyncLocal<UserAssertion>();

        /// <summary>
        ///
        /// </summary>
        /// <param name="userAccessToken"></param>
        public UserAssertionScope(string userAccessToken)
        {
            _userAssertion.Value = new UserAssertion(userAccessToken);
        }

        /// <summary>
        ///
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
            _userAssertion.Value = default;
        }
    }
}
