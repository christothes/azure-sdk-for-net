// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using System.IO;
using System.Text.Json;
using Azure.Core;

namespace Azure.Data.Tables.Performance
{
    [MemoryDiagnoser]
    public class GenericTableEntityCreationBenchmark
    {
        public const int ItemCount = 10000;
        private BenchmarkEntity entity = new BenchmarkEntity()
        {
            PartitionKey = "partition",
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
        private readonly List<Dictionary<string, object>> _items = new List<Dictionary<string, object>>(ItemCount);

        [GlobalSetup]
        public void Setup()
        {
            for (int i = 0; i < ItemCount; i++)
            {
                _items.Add(new Dictionary<string, object>
                    {
                        {"PartitionKey", "partition"},
                        {"RowKey", i.ToString("D2")},
                        {"SomeGuid", Guid.NewGuid().ToString()},
                        {"SomeString", $"This is table entity number {i:D2}"},
                        {"SomeInt", i},
                        {"SomeDateTime", new DateTime(2020, 1,1,1,1,0,DateTimeKind.Utc).AddMinutes(i).ToString("o") },
                        {"SomeBinary", Convert.ToBase64String(new byte[]{ 0x01, 0x02, 0x03, 0x04, 0x05 })},
                    });
            }
            var s = new MemoryStream();
            var w = new Utf8JsonWriter(s);
            entity.SerializeEntity(w);
        }

        [Benchmark(Baseline = true)]
        public void ToOdataDictionary()
        {
            var dict = entity.ToOdataAnnotatedDictionary();
            var stream = new MemoryStream();
            var writer = new Utf8JsonWriter(stream);
            writer.WriteStartObject();
            foreach (var item in dict)
            {
                writer.WritePropertyName(item.Key);
                writer.WriteObjectValue(item.Value);
            }
            writer.WriteEndObject();
        }

        [Benchmark]
        public void SerializeEntity()
        {
            var stream = new MemoryStream();
            var writer = new Utf8JsonWriter(stream);
            entity.SerializeEntity(writer);
    }

    //[Benchmark]
    public void ToTableEntity()
    {
        for (int i = 0; i < _items.Count; i++)
        {
            _items[i].ToTableEntity<BenchmarkEntity>();
        }
    }

    //[Benchmark]
    public void ToTableEntityList()
    {
        _items.ToTableEntityList<BenchmarkEntity>();
    }

    public class BenchmarkEntity : ITableEntity
    {
        public BenchmarkEntity()
        {
        }

        public BenchmarkEntity(string partitionKey, string rowKey)
        {
            PartitionKey = partitionKey;
            RowKey = rowKey;
        }

        public Guid SomeGuid { get; set; }
        public string SomeString { get; set; }
        public int SomeInt { get; set; }
        public DateTime SomeDateTime { get; set; }
        public byte[] SomeBinary { get; set; }
        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
        public long LongProperty { get; set; }
        public Foo MyFoo { get; set; }
    }

    public enum Foo
    {
        One,
        Two
    }
}
}
