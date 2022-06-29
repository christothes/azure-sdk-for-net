// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Azure.Core;

namespace Azure.Data.Tables
{
    internal static class TableEntityExtensions
    {
        /// <summary>
        /// Returns a new Dictionary with the appropriate Odata type annotation for a given propertyName value pair.
        /// The default case is intentionally unhandled as this means that no type annotation for the specified type is required.
        /// This is because the type is naturally serialized in a way that the table service can interpret without hints.
        /// </summary>
        internal static IDictionary<string, object> ToOdataAnnotatedDictionary<T>(this T entity)
        {
            if (entity is IDictionary<string, object> dictEntity)
            {
                return dictEntity.ToOdataAnnotatedDictionary();
            }
            Type type = entity.GetType();
            var typeInfo = TablesTypeBinder.Shared.GetBinderInfo(type);
            var dictionary = new Dictionary<string, object>(typeInfo.MemberCount * 2);
            typeInfo.Serialize(entity, dictionary);
            return dictionary;
        }
    }
}
