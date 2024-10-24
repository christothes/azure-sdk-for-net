// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

// <auto-generated/>

#nullable disable

namespace Azure.ResourceManager.ResourceHealth.Models
{
    /// <summary> Article of event. </summary>
    internal partial class EventPropertiesArticle
    {
        /// <summary> Initializes a new instance of EventPropertiesArticle. </summary>
        internal EventPropertiesArticle()
        {
        }

        /// <summary> Initializes a new instance of EventPropertiesArticle. </summary>
        /// <param name="articleContent"> Article content of event. </param>
        internal EventPropertiesArticle(string articleContent)
        {
            ArticleContent = articleContent;
        }

        /// <summary> Article content of event. </summary>
        public string ArticleContent { get; }
    }
}
