// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;

namespace Azure.Data.Tables
{
    internal static class TableEntityExtensions
    {
        /// <summary>
        /// Returns a new Dictionary with the appropriate Odata type annotation for a given propertyName value pair.
        /// The default case is intentionally unhandled as this means that no type annotation for the specified type is required.
        /// This is because the type is naturally serialized in a way that the table service can interpret without hints.
        /// </summary>
        internal static Dictionary<string, object> ToOdataAnnotatedDictionary<T>(this T entity) where T : class, ITableEntity
        {
            if (entity is IDictionary<string, object> dictEntity)
            {
                return dictEntity.ToOdataAnnotatedDictionary();
            }

            var properties = entity.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var annotatedDictionary = new Dictionary<string, object>(properties.Length * 2);

            foreach (var prop in properties)
            {
                var ignoreAttribute = prop.GetCustomAttribute<IgnoreDataMemberAttribute>(false);
                if (ignoreAttribute != null)
                {
                    // do not serialize this property.
                    continue;
                }
                var dataMemberAttribute = prop.GetCustomAttribute<DataMemberAttribute>(false);
                if (dataMemberAttribute == null && !prop.GetGetMethod(true).IsPublic)
                {
                    // skip internal properties that do not have a DataMember attribute.
                    continue;
                }
                string serailizedPropertyName = dataMemberAttribute?.Name switch
                {
                    null => prop.Name,
                    var name => name
                };
                // Remove the ETag and Timestamp properties, as they do not need to be serialized
                if (prop.Name == TableConstants.PropertyNames.ETag || prop.Name == TableConstants.PropertyNames.Timestamp)
                {
                    continue;
                }

                annotatedDictionary[serailizedPropertyName] = prop.GetValue(entity);

                switch (annotatedDictionary[serailizedPropertyName])
                {
                    case byte[]:
                    case BinaryData:
                        annotatedDictionary[serailizedPropertyName.ToOdataTypeString()] = TableConstants.Odata.EdmBinary;
                        break;
                    case long:
                        annotatedDictionary[serailizedPropertyName.ToOdataTypeString()] = TableConstants.Odata.EdmInt64;
                        // Int64 / long should be serialized as string.
                        annotatedDictionary[serailizedPropertyName] = annotatedDictionary[serailizedPropertyName].ToString();
                        break;
                    case double:
                        annotatedDictionary[serailizedPropertyName.ToOdataTypeString()] = TableConstants.Odata.EdmDouble;
                        break;
                    case Guid:
                        annotatedDictionary[serailizedPropertyName.ToOdataTypeString()] = TableConstants.Odata.EdmGuid;
                        break;
                    case DateTimeOffset:
                        annotatedDictionary[serailizedPropertyName.ToOdataTypeString()] = TableConstants.Odata.EdmDateTime;
                        break;
                    case DateTime:
                        annotatedDictionary[serailizedPropertyName.ToOdataTypeString()] = TableConstants.Odata.EdmDateTime;
                        break;
                    case Enum enumValue:
                        // serialize enum as string
                        annotatedDictionary[serailizedPropertyName] = enumValue.ToString();
                        break;
                }
            }

            return annotatedDictionary;
        }
    }
}
