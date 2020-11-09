// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;

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
        private static readonly ConcurrentDictionary<Type, Func<Stream, ITableEntity>> s_deSerializationExpressionCache = new ConcurrentDictionary<Type, Func<Stream, ITableEntity>>();
        private static readonly ParameterExpression s_writerParameter = Expression.Parameter(typeof(Utf8JsonWriter), "writer");
        private static readonly ParameterExpression s_entityInterfaceParameter = Expression.Parameter(typeof(ITableEntity), "entityInterface");
        private static readonly MethodInfo s_convertTob64 = typeof(Convert).GetMethod(nameof(Convert.ToBase64String), new[] { typeof(byte[]) });
        private static readonly MethodInfo s_int64ToString = typeof(long).GetMethod(nameof(long.ToString), Type.EmptyTypes);
        private static readonly MethodInfo s_guidToString = typeof(Guid).GetMethod(nameof(Guid.ToString), Type.EmptyTypes);
        private static readonly MethodInfo s_enumToString = typeof(Enum).GetMethod(nameof(Enum.ToString), Type.EmptyTypes);
        private static readonly ConstantExpression s_roundTripFormat = Expression.Constant("o");
        private static readonly MethodInfo s_dateTimeOffsetToString = typeof(DateTimeOffset).GetMethod(nameof(DateTimeOffset.ToString), new[] { typeof(string) });
        private static readonly MethodInfo s_dateTimeToString = typeof(DateTime).GetMethod(nameof(DateTimeOffset.ToString), new[] { typeof(string) });
        private static readonly MethodInfo s_writeStartObject = typeof(Utf8JsonWriter).GetMethod(nameof(Utf8JsonWriter.WriteStartObject), Type.EmptyTypes);
        private static readonly MethodInfo s_writeEndObject = typeof(Utf8JsonWriter).GetMethod(nameof(Utf8JsonWriter.WriteEndObject), Type.EmptyTypes);
        private static readonly MethodInfo s_writePropName = typeof(Utf8JsonWriter).GetMethod(nameof(Utf8JsonWriter.WritePropertyName), new[] { typeof(string) });
        private static readonly MethodInfo s_writeStringValue = typeof(Utf8JsonWriter).GetMethod(nameof(Utf8JsonWriter.WriteStringValue), new[] { typeof(string) });
        private static readonly MethodInfo s_writeNumberValueInt = typeof(Utf8JsonWriter).GetMethod(nameof(Utf8JsonWriter.WriteNumberValue), new[] { typeof(int) });
        private static readonly MethodInfo s_writeNumberValueDouble = typeof(Utf8JsonWriter).GetMethod(nameof(Utf8JsonWriter.WriteNumberValue), new[] { typeof(double) });
            private static readonly ParameterExpression s_entityConcreteParameter = Expression.Variable(typeof(Stream), "content");

        internal static void SerializeEntity<T>(this T entity, Utf8JsonWriter writer) where T : class, ITableEntity
        {
            Action<ITableEntity, Utf8JsonWriter> serializeEntity = s_serializeExpressionCache.GetOrAdd(typeof(T), (type) =>
            {
                return BuildSerializationExpression(entity);
            });

            serializeEntity(entity, writer);
        }

        internal static T DeSerializeEntity<T>(Stream content) where T : class, ITableEntity
        {
            Func<Stream, ITableEntity> deSerializeEntity = s_deSerializationExpressionCache.GetOrAdd(typeof(T), (type) =>
            {
                return BuildDeSerializationExpression(content);
            });

            return (T)deSerializeEntity(content);
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

        internal static Func<Stream, ITableEntity> BuildDeSerializationExpression<T>(Stream content) where T : class, ITableEntity
        {
            var expressions = new List<Expression>();

            BlockExpression expressionBlock = Expression.Block(new ParameterExpression[] { })
        }

    }
}
