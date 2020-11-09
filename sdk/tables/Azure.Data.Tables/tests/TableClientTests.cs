// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Core.TestFramework;
using Azure.Data.Tables;
using Azure.Data.Tables.Models;
using Azure.Data.Tables.Sas;
using NUnit.Framework;

namespace Azure.Tables.Tests
{
    public class TableClientTests : ClientTestBase
    {
        public TableClientTests(bool isAsync) : base(isAsync)
        { }

        private const string TableName = "someTableName";
        private const string AccountName = "someaccount";
        private readonly Uri _url = new Uri($"https://someaccount.table.core.windows.net");
        private readonly Uri _urlHttp = new Uri($"http://someaccount.table.core.windows.net");
        private TableClient client { get; set; }
        private TableEntity entityWithoutPK = new TableEntity { { TableConstants.PropertyNames.RowKey, "row" } };
        private TableEntity entityWithoutRK = new TableEntity { { TableConstants.PropertyNames.PartitionKey, "partition" } };
        private TableEntity validEntity = new TableEntity { { TableConstants.PropertyNames.PartitionKey, "partition" }, { TableConstants.PropertyNames.RowKey, "row" } };

        [SetUp]
        public void TestSetup()
        {
            var service_Instrumented = InstrumentClient(new TableServiceClient(new Uri("https://example.com"), new TableClientOptions()));
            client = service_Instrumented.GetTableClient(TableName);
        }

        /// <summary>
        /// Validates the functionality of the TableServiceClient.
        /// </summary>
        [Test]
        public void ConstructorValidatesArguments()
        {
            Assert.That(() => new TableClient(_url, null, new TableSharedKeyCredential(AccountName, string.Empty)), Throws.InstanceOf<ArgumentNullException>(), "The constructor should validate the tableName.");

            Assert.That(() => new TableClient(null, TableName, new TableSharedKeyCredential(AccountName, string.Empty)), Throws.InstanceOf<ArgumentNullException>(), "The constructor should validate the url.");

            Assert.That(() => new TableClient(_url, TableName, new TableSharedKeyCredential(AccountName, string.Empty), new TableClientOptions()), Throws.Nothing, "The constructor should accept valid arguments.");

            Assert.That(() => new TableClient(_url, TableName, credential: null), Throws.InstanceOf<ArgumentNullException>(), "The constructor should validate the TablesSharedKeyCredential.");

            Assert.That(() => new TableClient(_urlHttp, TableName), Throws.InstanceOf<ArgumentException>(), "The constructor should validate the Uri is https when using a SAS token.");

            Assert.That(() => new TableClient(_url, TableName), Throws.Nothing, "The constructor should accept a null credential");

            Assert.That(() => new TableClient(_url, TableName, new TableSharedKeyCredential(AccountName, string.Empty)), Throws.Nothing, "The constructor should accept valid arguments.");

            Assert.That(() => new TableClient(_urlHttp, TableName, new TableSharedKeyCredential(AccountName, string.Empty)), Throws.Nothing, "The constructor should accept an http url.");
        }

        /// <summary>
        /// Validates the functionality of the TableClient.
        /// </summary>
        [Test]
        public void ServiceMethodsValidateArguments()
        {
            Assert.That(async () => await client.AddEntityAsync<TableEntity>(null), Throws.InstanceOf<ArgumentNullException>(), "The method should validate the entity is not null.");

            Assert.That(async () => await client.UpsertEntityAsync<TableEntity>(null, TableUpdateMode.Replace), Throws.InstanceOf<ArgumentNullException>(), "The method should validate the entity is not null.");
            Assert.That(async () => await client.UpsertEntityAsync(new TableEntity { PartitionKey = null, RowKey = "row" }, TableUpdateMode.Replace), Throws.InstanceOf<ArgumentException>(), $"The method should validate the entity has a {TableConstants.PropertyNames.PartitionKey}.");

            Assert.That(async () => await client.UpsertEntityAsync(new TableEntity { PartitionKey = "partition", RowKey = null }, TableUpdateMode.Replace), Throws.InstanceOf<ArgumentException>(), $"The method should validate the entity has a {TableConstants.PropertyNames.RowKey}.");

            Assert.That(async () => await client.UpdateEntityAsync<TableEntity>(null, new ETag("etag"), TableUpdateMode.Replace), Throws.InstanceOf<ArgumentNullException>(), "The method should validate the entity is not null.");
            Assert.That(async () => await client.UpdateEntityAsync(validEntity, default, TableUpdateMode.Replace), Throws.InstanceOf<ArgumentException>(), "The method should validate the eTag is not null.");

            Assert.That(async () => await client.UpdateEntityAsync(entityWithoutPK, new ETag("etag"), TableUpdateMode.Replace), Throws.InstanceOf<ArgumentException>(), $"The method should validate the entity has a {TableConstants.PropertyNames.PartitionKey}.");

            Assert.That(async () => await client.UpdateEntityAsync(entityWithoutRK, new ETag("etag"), TableUpdateMode.Replace), Throws.InstanceOf<ArgumentException>(), $"The method should validate the entity has a {TableConstants.PropertyNames.RowKey}.");
        }

        [Test]
        public void GetSasBuilderPopulatesPermissionsAndExpiry()
        {
            var expiry = DateTimeOffset.Now.AddDays(1);
            var permissions = TableSasPermissions.All;

            var sas = client.GetSasBuilder(permissions, expiry);

            Assert.That(sas.Permissions, Is.EqualTo(permissions.ToPermissionsString()));
            Assert.That(sas.ExpiresOn, Is.EqualTo(expiry));
        }

        [Test]
        public void GetSasBuilderPopulatesRawPermissionsAndExpiry()
        {
            var expiry = DateTimeOffset.Now.AddDays(1);
            var permissions = TableSasPermissions.All;

            var sas = client.GetSasBuilder(permissions.ToPermissionsString(), expiry);

            Assert.That(sas.Permissions, Is.EqualTo(permissions.ToPermissionsString()));
            Assert.That(sas.ExpiresOn, Is.EqualTo(expiry));
        }

        /// <summary>
        /// Validates the functionality of the TableClient.
        /// </summary>
        [Test]
        public void CreatedTableEntityEnumEntitiesThrowNotSupported()
        {
            var entityToCreate = new TableEntity { PartitionKey = "partitionKey", RowKey = "01" };
            entityToCreate["MyFoo"] = Foo.Two;

            // Create the new entities.
            Assert.ThrowsAsync<NotSupportedException>(async () => await client.AddEntityAsync(entityToCreate).ConfigureAwait(false));
        }

        /// <summary>
        /// Validates the functionality of the TableClient.
        /// </summary>
        [Test]
        public void CreatedEnumPropertiesAreSerializedProperly()
        {
            var entity = new EnumEntity { PartitionKey = "partitionKey", RowKey = "01", Timestamp = DateTime.Now, MyFoo = Foo.Two, ETag = ETag.All };

            // Create the new entities.
            var dictEntity = entity.ToOdataAnnotatedDictionary();

            Assert.That(dictEntity["PartitionKey"], Is.EqualTo(entity.PartitionKey), "The entities should be equivalent");
            Assert.That(dictEntity["RowKey"], Is.EqualTo(entity.RowKey), "The entities should be equivalent");
            Assert.That(dictEntity["MyFoo"], Is.EqualTo(entity.MyFoo.ToString()), "The entities should be equivalent");
        }

        [Test]
        public void SerializeEntity()
        {
            EnumEntity entity = new EnumEntity()
            {
                PartitionKey = "partitionFoo",
                RowKey = "01",
                SomeGuid = Guid.NewGuid(),
                SomeString = "This is a table entity",
                SomeInt = 1234,
                SomeDateTime = DateTime.UtcNow,
                SomeBinary = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 },
                Timestamp = DateTimeOffset.Now,
                ETag = new ETag("foo"),
                MyFoo = Foo.Two
            };

            var stream = new MemoryStream();
            var writer = new Utf8JsonWriter(stream);

            entity.SerializeEntity(writer);

            writer.Flush();
            Assert.That(stream.Position > 0, "stream position should not be zero");

            stream.Position = 0;
            var sr = new StreamReader(stream);
            var json = sr.ReadToEnd();

            Assert.That(json, Is.Not.Empty, json);

            var doc = JsonDocument.Parse(json);
            var dictionary = new Dictionary<string, object>();
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                dictionary.Add(prop.Name, prop.Value.GetObject());
            }
            Assert.That(dictionary["PartitionKey"] as string, Is.EqualTo(entity.PartitionKey));
            Assert.That(dictionary["SomeInt"], Is.EqualTo(entity.SomeInt));
        }

        [Test]
        public void DeSerializeEntity()
        {
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);
            writer.Write(tableQueryResultJson);
            writer.Flush();
            stream.Position = 0;

            
        }

        private string tableQueryResultJson = @"""odata.metadata"": ""https://t487ba7ce49634c8eprim.table.core.windows.net/$metadata#testtableifprd13i"",
        ""value"": [
          {
            ""odata.etag"": ""W/\u0022datetime\u00272020-08-25T16%3A35%3A09.461291Z\u0027\u0022"",
            ""PartitionKey"": ""somPartition"",
            ""RowKey"": ""01"",
            ""Timestamp"": ""2020-08-25T16:35:09.461291Z"",
            ""SomeBinaryProperty@odata.type"": ""Edm.Binary"",
            ""SomeBinaryProperty"": ""AQIDBAU="",
            ""SomeDateProperty@odata.type"": ""Edm.DateTime"",
            ""SomeDateProperty"": ""2020-01-01T01:02:00Z"",
            ""SomeDoubleProperty0"": 1.0,
            ""SomeDoubleProperty1"": 1.1,
            ""SomeGuidProperty@odata.type"": ""Edm.Guid"",
            ""SomeGuidProperty"": ""0d391d16-97f1-4b9a-be68-4cc871f90001"",
            ""SomeInt64Property@odata.type"": ""Edm.Int64"",
            ""SomeInt64Property"": ""1"",
            ""SomeIntProperty"": 1,
            ""SomeStringProperty"": ""This is table entity number 01""
          }
        ]";

        public class EnumEntity : ITableEntity
        {
            public string PartitionKey { get; set; }
            public string RowKey { get; set; }
            public DateTimeOffset? Timestamp { get; set; }
            public ETag ETag { get; set; }
            public Foo MyFoo { get; set; }
            public string SomeString { get; set; }
            public int SomeInt { get; set; }
            public DateTime SomeDateTime { get; set; }
            public byte[] SomeBinary { get; set; }
            public Guid SomeGuid { get; set; }
            public long SomeLong { get; set; }
        }
        public enum Foo
        {
            One,
            Two
        }
    }
}
