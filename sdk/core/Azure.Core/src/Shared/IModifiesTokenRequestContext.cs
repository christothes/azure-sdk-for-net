// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Azure.Core.Pipeline
{
    /// <summary>
    ///
    /// </summary>
    public interface IModifiesTokenRequestContext
    {
        /// <summary>
        ///
        /// </summary>
        /// <param name="context"></param>
        TokenRequestContext ModifyTokenRequestContext(TokenRequestContext context);
    }
}
