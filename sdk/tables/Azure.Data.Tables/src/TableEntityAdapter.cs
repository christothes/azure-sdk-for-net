// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using Azure;
using Azure.Core;

namespace Azure.Data.Tables
{
    /// <summary>
    ///
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class TableEntityAdapter<T> : ITableEntity, INestedEntity where T : class
    {
        /// <summary>
        ///
        /// </summary>
        /// <value></value>
        public T Entity { get; set; }

        object INestedEntity.EntityObject { get => Entity; set => Entity = value as T; }

        /// <summary>
        ///
        /// </summary>
        /// <value></value>
        public string PartitionKey { get; set; }

        /// <summary>
        ///
        /// </summary>
        /// <value></value>
        public string RowKey { get; set; }

        /// <summary>
        ///
        /// </summary>
        /// <value></value>
        public DateTimeOffset? Timestamp { get; set; }

        /// <summary>
        ///
        /// </summary>
        /// <value></value>
        public ETag ETag { get; set; }
    }
}
