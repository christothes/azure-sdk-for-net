// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using Azure.Core;

namespace Azure.Data.Tables
{
    internal static class TableEntityExtensions
    {
        public enum Foo
        {
            One,
            Two
        }

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

            var properties = typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public);
            var annotatedDictionary = new Dictionary<string, object>(properties.Length * 2);

            foreach (var prop in properties)
            {
                annotatedDictionary[prop.Name] = prop.GetValue(entity);

                switch (annotatedDictionary[prop.Name])
                {
                    case byte[] _:
                        annotatedDictionary[prop.Name.ToOdataTypeString()] = TableConstants.Odata.EdmBinary;
                        break;
                    case long _:
                        annotatedDictionary[prop.Name.ToOdataTypeString()] = TableConstants.Odata.EdmInt64;
                        // Int64 / long should be serialized as string.
                        annotatedDictionary[prop.Name] = annotatedDictionary[prop.Name].ToString();
                        break;
                    case double _:
                        annotatedDictionary[prop.Name.ToOdataTypeString()] = TableConstants.Odata.EdmDouble;
                        break;
                    case Guid _:
                        annotatedDictionary[prop.Name.ToOdataTypeString()] = TableConstants.Odata.EdmGuid;
                        break;
                    case DateTimeOffset _:
                        annotatedDictionary[prop.Name.ToOdataTypeString()] = TableConstants.Odata.EdmDateTime;
                        break;
                    case DateTime _:
                        annotatedDictionary[prop.Name.ToOdataTypeString()] = TableConstants.Odata.EdmDateTime;
                        break;
                    case Enum enumValue:
                        // serialize enum as string
                        annotatedDictionary[prop.Name] = enumValue.ToString();
                        break;
                }
            }

            // Remove the ETag property, as it does not need to be serialized
            annotatedDictionary.Remove(TableConstants.PropertyNames.ETag);

            return annotatedDictionary;
        }

        private static readonly ConcurrentDictionary<Type, Action<ITableEntity, Utf8JsonWriter>> s_serializeExpressionCache = new ConcurrentDictionary<Type, Action<ITableEntity, Utf8JsonWriter>>();
        internal static readonly ConcurrentDictionary<Type, Dictionary<string, Action<ITableEntity, JsonElement>>> s_setterMethods = new ConcurrentDictionary<Type, Dictionary<string, Action<ITableEntity, JsonElement>>>();
        private static readonly ParameterExpression s_writerParameter = Expression.Parameter(typeof(Utf8JsonWriter), "writer");
        private static readonly ParameterExpression s_entityInterfaceParameter = Expression.Parameter(typeof(ITableEntity), "entityInterface");
        private static readonly ParameterExpression s_jsonElementParameter = Expression.Parameter(typeof(JsonElement), "element");
        private static readonly MethodInfo s_convertTob64 = typeof(Convert).GetMethod(nameof(Convert.ToBase64String), new[] { typeof(byte[]) });
        private static readonly MethodInfo s_int64ToString = typeof(long).GetMethod(nameof(long.ToString), Type.EmptyTypes);
        private static readonly MethodInfo s_guidToString = typeof(Guid).GetMethod(nameof(Guid.ToString), Type.EmptyTypes);
        private static readonly MethodInfo s_enumToString = typeof(Enum).GetMethod(nameof(Enum.ToString), Type.EmptyTypes);
        private static readonly ConstantExpression s_roundTripFormat = Expression.Constant("o");
        private static readonly MethodInfo s_dateTimeOffsetToString = typeof(DateTimeOffset).GetMethod(nameof(DateTimeOffset.ToString), new[] { typeof(string) });
        private static readonly MethodInfo s_dateTimeToString = typeof(DateTime).GetMethod(nameof(DateTimeOffset.ToString), new[] { typeof(string) });
        private static readonly MethodInfo s_longParse = typeof(long).GetMethod(nameof(long.Parse), new[] { typeof(string) });
        //private static readonly MethodInfo s_nullableDateTimeOffsetGetValue = typeof(DateTimeOffset?).GetProperty(nameof(Nullable<DateTimeOffset>.Value)).GetMethod;
        private static readonly MethodInfo s_dateTimeOffsetParse = typeof(DateTimeOffset).GetMethod(nameof(DateTimeOffset.Parse), new[] { typeof(string) });
        private static readonly MethodInfo s_writeStartObject = typeof(Utf8JsonWriter).GetMethod(nameof(Utf8JsonWriter.WriteStartObject), Type.EmptyTypes);
        private static readonly MethodInfo s_writeEndObject = typeof(Utf8JsonWriter).GetMethod(nameof(Utf8JsonWriter.WriteEndObject), Type.EmptyTypes);
        private static readonly MethodInfo s_writePropName = typeof(Utf8JsonWriter).GetMethod(nameof(Utf8JsonWriter.WritePropertyName), new[] { typeof(string) });
        private static readonly MethodInfo s_writeStringValue = typeof(Utf8JsonWriter).GetMethod(nameof(Utf8JsonWriter.WriteStringValue), new[] { typeof(string) });
        private static readonly MethodInfo s_writeNumberValueInt = typeof(Utf8JsonWriter).GetMethod(nameof(Utf8JsonWriter.WriteNumberValue), new[] { typeof(int) });
        private static readonly MethodInfo s_writeNumberValueDouble = typeof(Utf8JsonWriter).GetMethod(nameof(Utf8JsonWriter.WriteNumberValue), new[] { typeof(double) });
        private static readonly ConstantExpression s_capitolU = Expression.Constant("U");
        private static readonly MethodInfo s_jsonElementGetBytesFromBase64 = typeof(JsonElementExtensions).GetMethod(nameof(JsonElement.GetBytesFromBase64));
        private static readonly MethodInfo s_jsonElementGetDateTime = typeof(JsonElement).GetMethod(nameof(JsonElement.GetDateTime));
        private static readonly MethodInfo s_jsonElementGetDateTimeOffset = typeof(JsonElementExtensions).GetMethod(nameof(JsonElement.GetDateTimeOffset));
        private static readonly MethodInfo s_jsonElementGetDouble = typeof(JsonElement).GetMethod(nameof(JsonElement.GetDouble), Type.EmptyTypes);
        private static readonly MethodInfo s_jsonElementGetGuid = typeof(JsonElement).GetMethod(nameof(JsonElement.GetGuid), Type.EmptyTypes);
        private static readonly MethodInfo s_jsonElementGetString = typeof(JsonElement).GetMethod(nameof(JsonElement.GetString), Type.EmptyTypes);
        private static readonly MethodInfo s_jsonElementGetNumber = typeof(JsonElement).GetMethod(nameof(JsonElement.GetInt32), Type.EmptyTypes);
        private static readonly MethodInfo s_jsonElementGetBool = typeof(JsonElement).GetMethod(nameof(JsonElement.GetBoolean), Type.EmptyTypes);
        private static readonly MethodInfo s_jsonElementGetObject = typeof(JsonElementExtensions).GetMethod("GetObject");

        internal static void SerializeEntity<T>(this T entity, Utf8JsonWriter writer) where T : class, ITableEntity
        {
            Action<ITableEntity, Utf8JsonWriter> serializeEntity = s_serializeExpressionCache.GetOrAdd(typeof(T), (type) =>
            {
                return BuildSerializationExpression(entity);
            });

            serializeEntity(entity, writer);
        }

        internal static Action<ITableEntity, Utf8JsonWriter> BuildSerializationExpression<T>(T entity) where T : class, ITableEntity
        {
            var expressions = new List<Expression>();

            // cast the incoming interface to the concrete type
            ParameterExpression entityConcreteParameter = Expression.Variable(typeof(T), "entity");
            UnaryExpression castInterfaceToConcrete = Expression.Convert(s_entityInterfaceParameter, typeof(T));
            expressions.Add(Expression.Assign(entityConcreteParameter, castInterfaceToConcrete));

            //var consoleWL = typeof(Console).GetMethod(nameof(Console.WriteLine), new[] { typeof(int) });
            //var callConsoleWriteLine = Expression.Call(consoleWL, Expression.Call(entityConcreteParameter, typeof(T).GetProperty("SomeInt").GetMethod));
            //expressions.Add(callConsoleWriteLine);

            // write the startObject
            expressions.Add(Expression.Call(s_writerParameter, s_writeStartObject));

            // Get the entity properties
            PropertyInfo[] properties = typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public);

            // iterate through each property to build the serialization expressions for each
            // This includes the property name an its value and any required oData annotation properties
            foreach (PropertyInfo prop in properties)
            {
                // The ETag property does not need to be serialized
                if (prop.Name == TableConstants.PropertyNames.ETag)
                {
                    continue;
                }

                // WritePropertyName(<propertyName>)
                expressions.Add(Expression.Call(s_writerParameter, s_writePropName, Expression.Constant(prop.Name)));

                switch (prop.GetValue(entity))
                {
                    case string _:
                        // WriteStringValue(entity.GetStringProperty()))
                        MethodCallExpression getStringProperty = Expression.Call(entityConcreteParameter, typeof(T).GetProperty(prop.Name).GetMethod);
                        expressions.Add(Expression.Call(s_writerParameter, s_writeStringValue, getStringProperty));

                        break;
                    case byte[] _:
                        // WriteStringValue(Convert.ToBase64String(entity.GetByteProperty()))
                        MethodCallExpression getByteProperty = Expression.Call(entityConcreteParameter, typeof(T).GetProperty(prop.Name).GetMethod);
                        MethodCallExpression byteToBase64String = Expression.Call(s_convertTob64, getByteProperty);
                        expressions.Add(Expression.Call(s_writerParameter, s_writeStringValue, byteToBase64String));

                        // WritePropertyName(<propertyName>.@odata.type>)
                        expressions.Add(Expression.Call(s_writerParameter, s_writePropName, Expression.Constant(prop.Name.ToOdataTypeString())));

                        // WriteStringValue("Edm.Binary")
                        expressions.Add(Expression.Call(s_writerParameter, s_writeStringValue, Expression.Constant(TableConstants.Odata.EdmBinary)));

                        break;
                    case long _:
                        // Int64 / long should be serialized as string.
                        // WriteStringValue(entity.GetInt64Property.ToString())
                        MethodCallExpression getInt64Property = Expression.Call(entityConcreteParameter, typeof(T).GetProperty(prop.Name).GetMethod);
                        MethodCallExpression longToString = Expression.Call(getInt64Property, s_int64ToString);
                        expressions.Add(Expression.Call(s_writerParameter, s_writeStringValue, longToString));

                        // WritePropertyName(<propertyName>.@odata.type>)
                        expressions.Add(Expression.Call(s_writerParameter, s_writePropName, Expression.Constant(prop.Name.ToOdataTypeString())));

                        // WriteStringValue("Edm.Int64")
                        expressions.Add(Expression.Call(s_writerParameter, s_writeStringValue, Expression.Constant(TableConstants.Odata.EdmInt64)));
                        break;
                    case double _:
                        // WriteNumberValue(entity.GetDoubleProperty()))
                        MethodCallExpression getDoubleProperty = Expression.Call(entityConcreteParameter, typeof(T).GetProperty(prop.Name).GetMethod);
                        expressions.Add(Expression.Call(s_writerParameter, s_writeNumberValueDouble, getDoubleProperty));

                        // WritePropertyName(<propertyName>.@odata.type>)
                        expressions.Add(Expression.Call(s_writerParameter, s_writePropName, Expression.Constant(prop.Name.ToOdataTypeString())));

                        // WriteStringValue("Edm.Double")
                        expressions.Add(Expression.Call(s_writerParameter, s_writeStringValue, Expression.Constant(TableConstants.Odata.EdmDouble)));
                        break;
                    case Guid _:
                        // WriteStringValue(entity.GetGuidProperty.ToString())
                        MethodCallExpression getGuidProperty = Expression.Call(entityConcreteParameter, typeof(T).GetProperty(prop.Name).GetMethod);
                        MethodCallExpression guidToString = Expression.Call(getGuidProperty, s_guidToString);
                        expressions.Add(Expression.Call(s_writerParameter, s_writeStringValue, guidToString));

                        // WritePropertyName(<propertyName>.@odata.type>)
                        expressions.Add(Expression.Call(s_writerParameter, s_writePropName, Expression.Constant(prop.Name.ToOdataTypeString())));

                        // WriteStringValue("Edm.Guid")
                        expressions.Add(Expression.Call(s_writerParameter, s_writeStringValue, Expression.Constant(TableConstants.Odata.EdmGuid)));

                        break;
                    case DateTimeOffset _:
                        // WriteStringValue((DateTimeOffset)entity.GetDateTimeOffsetProperty.ToString("o"))
                        MethodCallExpression getDateTimeOffsetProperty = Expression.Call(entityConcreteParameter, typeof(T).GetProperty(prop.Name).GetMethod);
                        UnaryExpression castToDateTimeOffset = Expression.Convert(getDateTimeOffsetProperty, typeof(DateTimeOffset));
                        MethodCallExpression dateTimeOffsetToString = Expression.Call(castToDateTimeOffset, s_dateTimeOffsetToString, s_roundTripFormat);
                        expressions.Add(Expression.Call(s_writerParameter, s_writeStringValue, dateTimeOffsetToString));

                        // WritePropertyName(<propertyName>.@odata.type>)
                        expressions.Add(Expression.Call(s_writerParameter, s_writePropName, Expression.Constant(prop.Name.ToOdataTypeString())));

                        // WriteStringValue("Edm.DateTime")
                        expressions.Add(Expression.Call(s_writerParameter, s_writeStringValue, Expression.Constant(TableConstants.Odata.EdmDateTime)));

                        break;
                    case DateTime _:
                        // WriteStringValue((DateTime)entity.GetDateTimeProperty.ToString("o"))
                        MethodCallExpression getDateTimeProperty = Expression.Call(entityConcreteParameter, typeof(T).GetProperty(prop.Name).GetMethod);
                        UnaryExpression castToDateTime = Expression.Convert(getDateTimeProperty, typeof(DateTime));
                        MethodCallExpression dateTimeToString = Expression.Call(castToDateTime, s_dateTimeToString, s_roundTripFormat);
                        expressions.Add(Expression.Call(s_writerParameter, s_writeStringValue, dateTimeToString));

                        // WritePropertyName(<propertyName>.@odata.type>)
                        expressions.Add(Expression.Call(s_writerParameter, s_writePropName, Expression.Constant(prop.Name.ToOdataTypeString())));

                        // WriteStringValue("Edm.DateTime")
                        expressions.Add(Expression.Call(s_writerParameter, s_writeStringValue, Expression.Constant(TableConstants.Odata.EdmDateTime)));

                        break;
                    case Enum _:
                        // serialize enum as string
                        // WriteStringValue(entity.GetEnumProperty.ToString())
                        MethodCallExpression getEnumProperty = Expression.Call(entityConcreteParameter, typeof(T).GetProperty(prop.Name).GetMethod);
                        MethodCallExpression enumToString = Expression.Call(getEnumProperty, s_enumToString);
                        expressions.Add(Expression.Call(s_writerParameter, s_writeStringValue, enumToString));

                        break;
                    case int _:
                        // WriteNumberValue(entity.GetIntProperty()))
                        MethodCallExpression getIntProperty = Expression.Call(entityConcreteParameter, typeof(T).GetProperty(prop.Name).GetMethod);
                        expressions.Add(Expression.Call(s_writerParameter, s_writeNumberValueInt, getIntProperty));

                        break;
                }
            }

            // write the endObject
            expressions.Add(Expression.Call(s_writerParameter, s_writeEndObject));

            //expressions.ForEach(e => Console.WriteLine(e.ToString()));

            // Populate the expression block and compile it into a lambda
            BlockExpression expressionBlock = Expression.Block(new ParameterExpression[] { entityConcreteParameter }, expressions);
            return Expression.Lambda<Action<ITableEntity, Utf8JsonWriter>>(expressionBlock, s_entityInterfaceParameter, s_writerParameter).Compile();
        }

        internal static List<T> DeSerializeEntityResponse<T>(JsonElement element) where T : class, ITableEntity, new()
        {
            // populate the methodInfos dictionary
            s_setterMethods.GetOrAdd(typeof(T), (type) =>
            {
                return GetSetterMethodInfos<T>();
            });

            List<T> retval = new List<T>();

            // Loop through each top level object in the element
            foreach (JsonProperty topLevelObject in element.EnumerateObject())
            {
                // A table response will always contain a top level odata.metadata object
                if (topLevelObject.NameEquals("odata.metadata"))
                {
                    continue;
                }

                // The value contains the entity property collection
                if (topLevelObject.NameEquals("value"))
                {
                    // skip null values
                    if (topLevelObject.Value.ValueKind == JsonValueKind.Null)
                    {
                        continue;
                    }

                    // enumerate the array of entities
                    foreach (JsonElement item in topLevelObject.Value.EnumerateArray())
                    {
                        // { var entity = new T(); }
                        T entity = new T();

                        foreach (JsonProperty entityProperty in item.EnumerateObject())
                        {
                            // Call the property setter action for the propert named entityProperty.Name
                            if (s_setterMethods[typeof(T)].TryGetValue(entityProperty.Name, out var setterAction))
                            {
                                setterAction(entity, entityProperty.Value);
                            }
                            // if we don't find a property on the entity with the name entityPoperty.Name, ignore it unless it is 'odata.etag'
                            else if (entityProperty.Name.Equals(TableConstants.PropertyNames.EtagOdata, StringComparison.InvariantCulture))
                            {
                                s_setterMethods[typeof(T)][TableConstants.PropertyNames.ETag](entity, entityProperty.Value);
                            }
                        }
                        retval.Add(entity);
                    }
                }
            }

            return retval;
        }

        internal static Dictionary<string, Action<ITableEntity, JsonElement>> GetSetterMethodInfos<T>() where T : class, ITableEntity, new()
        {
            var expressions = new Expression[2];
            Dictionary<string, Action<ITableEntity, JsonElement>> dict = new Dictionary<string, Action<ITableEntity, JsonElement>>();

            // cast the incoming interface to the concrete type
            ParameterExpression entityConcreteParameter = Expression.Variable(typeof(T), "entity");
            UnaryExpression castInterfaceToConcrete = Expression.Convert(s_entityInterfaceParameter, typeof(T));
            expressions[0] = Expression.Assign(entityConcreteParameter, castInterfaceToConcrete);

            // Get the entity properties
            PropertyInfo[] properties = typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public);

            foreach (PropertyInfo prop in properties)
            {
                Expression getElementExpression;

                //Get the propert setter lambda for each property
                if (!s_typeActions.TryGetValue(prop.PropertyType, out getElementExpression))
                {
                    if (prop.PropertyType.IsEnum)
                    {
                        MethodInfo s_enumParse = typeof(Enum).GetMethod(nameof(Enum.Parse), new[] { typeof(string) }).MakeGenericMethod(prop.PropertyType);

                        getElementExpression = Expression.Call(s_enumParse, Expression.Call(s_jsonElementParameter, s_jsonElementGetString));
                    }
                    else
                    {
                        getElementExpression = Expression.Call(s_jsonElementParameter, s_jsonElementGetObject);
                    }
                }

                // { entity.set_PropName(element.Getx()) }
                expressions[1] = Expression.Call(entityConcreteParameter, prop.SetMethod, getElementExpression);

                Action<ITableEntity, JsonElement> setterAction = Expression.Lambda<Action<ITableEntity, JsonElement>>(
                    Expression.Block(new ParameterExpression[] { entityConcreteParameter }, expressions),
                    new ParameterExpression[] { s_entityInterfaceParameter, s_jsonElementParameter })
                 .Compile();

                dict[prop.Name] = setterAction;

            }

            return dict;
        }

        internal static readonly Dictionary<Type, Expression> s_typeActions = new Dictionary<Type, Expression>
        {
            // { element.GetBytesFromBase64() }
            {typeof(byte[]), Expression.Call(s_jsonElementGetBytesFromBase64, s_jsonElementParameter, s_capitolU)},
            // { long.Parse(element.GetString()) }
            {typeof(long), Expression.Call(s_longParse, Expression.Call(s_jsonElementParameter, s_jsonElementGetString))},
            {typeof(long?), Expression.Call(s_longParse, Expression.Call(s_jsonElementParameter, s_jsonElementGetString))},
            // { element.GetDouble() }
            {typeof(double), Expression.Call(s_jsonElementParameter, s_jsonElementGetDouble)},
            {typeof(double?), Expression.Call(s_jsonElementParameter, s_jsonElementGetDouble)},
            // { element.GetBoolean() }
            {typeof(bool), Expression.Call(s_jsonElementParameter, s_jsonElementGetBool)},
            {typeof(bool?), Expression.Call(s_jsonElementParameter, s_jsonElementGetBool)},
            // { element.GetGuid() }
            {typeof(Guid), Expression.Call(s_jsonElementParameter, s_jsonElementGetGuid)},
            {typeof(Guid?), Expression.Call(s_jsonElementParameter, s_jsonElementGetGuid)},
            // { element.GetDateTimeOffset() }
            {typeof(DateTimeOffset), Expression.Call(s_jsonElementGetDateTimeOffset, s_jsonElementParameter, s_capitolU)},
            {typeof(DateTimeOffset?), Expression.Convert(Expression.Call(s_dateTimeOffsetParse, Expression.Call(s_jsonElementParameter, s_jsonElementGetString)), typeof(Nullable<DateTimeOffset>))},
            // { element.GetDateTime() }
            {typeof(DateTime), Expression.Call(s_jsonElementParameter, s_jsonElementGetDateTime)},
            {typeof(DateTime?), Expression.Call(s_jsonElementParameter, s_jsonElementGetDateTime)},
            // { element.GetString() }
            {typeof(string), Expression.Call(s_jsonElementParameter, s_jsonElementGetString)},
            {typeof(ETag), Expression.New(typeof(ETag).GetConstructor(new[] { typeof(string) }), Expression.Call(s_jsonElementParameter, s_jsonElementGetString)) },
            // { element.GetNumber() }
            {typeof(int), Expression.Call(s_jsonElementParameter, s_jsonElementGetNumber)},
            {typeof(int?), Expression.Call(s_jsonElementParameter, s_jsonElementGetNumber)},
        };
    }
}
