// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Core;

namespace Azure.AI.MetricsAdvisor.Models
{
    /// <summary>
    /// The view mode of the <see cref="DataFeed"/>.
    /// </summary>
    [CodeGenModel("ViewMode")]
    public readonly partial struct DataFeedAccessMode
    {
        /// <summary>
<<<<<<< HEAD
        /// Indicates that the data feed is private.
=======
        /// Indicates that the view data feed is private.
>>>>>>> 9bb9be222f... Add remaining docstrings
        /// </summary>
        public static DataFeedAccessMode Private { get; } = new DataFeedAccessMode(PrivateValue);

        /// <summary>
        /// Indicates that the data feed is public.
        /// </summary>
        public static DataFeedAccessMode Public { get; } = new DataFeedAccessMode(PublicValue);
    }
}
