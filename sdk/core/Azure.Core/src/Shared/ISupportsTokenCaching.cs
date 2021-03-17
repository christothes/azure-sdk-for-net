// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Azure.Core
{
    /// <summary>
    ///
    /// </summary>
    public interface ISupportsTokenCaching
    {
        /// <summary>
        ///
        /// </summary>
        public bool BypassCache { get; set; }
    }
}
