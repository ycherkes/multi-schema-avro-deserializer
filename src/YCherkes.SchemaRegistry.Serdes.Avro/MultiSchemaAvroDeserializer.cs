using Avro.Specific;
using Confluent.Kafka;
using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;
using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace YCherkes.SchemaRegistry.Serdes.Avro
{
    public class MultiSchemaAvroDeserializer : IAsyncDeserializer<ISpecificRecord>
    {
        private const byte MagicByte = 0;
        private readonly Dictionary<string, IAsyncDeserializer<ISpecificRecord>> _deserializersBySchemaName;
        private readonly ConcurrentDictionary<int, IAsyncDeserializer<ISpecificRecord>> _deserializersBySchemaId;
        private readonly ISchemaRegistryClient _schemaRegistryClient;

        public MultiSchemaAvroDeserializer(ISchemaRegistryClient schemaRegistryClient, AvroDeserializerConfig avroDeserializerConfig = null)
            : this(schemaRegistryClient,
                  AppDomain.CurrentDomain
                  .GetAssemblies()
                  .SelectMany(a => a.GetTypes())
                  .Where(t => t.IsClass && !t.IsAbstract && typeof(ISpecificRecord).IsAssignableFrom(t) && GetSchema(t) != null),
                   avroDeserializerConfig,
                   checkTypes: false)
        {
        }

        public MultiSchemaAvroDeserializer(Func<IEnumerable<Type>> typeProvider, ISchemaRegistryClient schemaRegistryClient, AvroDeserializerConfig avroDeserializerConfig = null)
            : this(schemaRegistryClient,
                  (typeProvider ?? throw new ArgumentNullException(nameof(typeProvider)))(),
                   avroDeserializerConfig)
        {
        }

        public MultiSchemaAvroDeserializer(IEnumerable<Type> types, ISchemaRegistryClient schemaRegistryClient, AvroDeserializerConfig avroDeserializerConfig = null)
            : this(schemaRegistryClient, types, avroDeserializerConfig)
        {   
        }

        private MultiSchemaAvroDeserializer(ISchemaRegistryClient schemaRegistryClient, IEnumerable<Type> types, AvroDeserializerConfig avroDeserializerConfig, bool checkTypes = true)
        {
            this._schemaRegistryClient = schemaRegistryClient ?? throw new ArgumentNullException(nameof(schemaRegistryClient));

            var typeArray = (types ?? throw new ArgumentNullException(nameof(types))).ToArray();

            if (typeArray.Length == 0)
            {
                throw new ArgumentException("Type collection must contain at least one item.", nameof(types));
            }

            if (checkTypes && !typeArray.All(t => t.IsClass && !t.IsAbstract && typeof(ISpecificRecord).IsAssignableFrom(t) && GetSchema(t) != null))
            {
                throw new ArgumentOutOfRangeException(nameof(types));
            }

            _deserializersBySchemaId = new ConcurrentDictionary<int, IAsyncDeserializer<ISpecificRecord>>();
            _deserializersBySchemaName = typeArray.ToDictionary(t => GetSchema(t).Fullname, t => CreateDeserializer(t, avroDeserializerConfig));
        }

        public async Task<ISpecificRecord> DeserializeAsync(ReadOnlyMemory<byte> data, bool isNull, SerializationContext context)
        {
            var deserializer = await GetDeserializer(data);

            return deserializer == null ? null : await deserializer.DeserializeAsync(data, isNull, context);
        }

        private IAsyncDeserializer<ISpecificRecord> CreateDeserializer(Type specificType, AvroDeserializerConfig avroDeserializerConfig)
        {
            var constructedDeserializerType = typeof(DeserializerWrapper<>).MakeGenericType(specificType);
            return (IAsyncDeserializer<ISpecificRecord>)Activator.CreateInstance(constructedDeserializerType, _schemaRegistryClient, avroDeserializerConfig);
        }

        private async Task<IAsyncDeserializer<ISpecificRecord>> GetDeserializer(ReadOnlyMemory<byte> data)
        {
            var schemaId = GetSchemaId(data.Span);

            if (_deserializersBySchemaId.TryGetValue(schemaId, out var deserializer))
            {
                return deserializer;
            }

            var confluentSchema = await _schemaRegistryClient.GetSchemaAsync(schemaId);
            var avroSchema = global::Avro.Schema.Parse(confluentSchema.SchemaString);

            _ = _deserializersBySchemaName.TryGetValue(avroSchema.Fullname, out deserializer);
            _ = _deserializersBySchemaId.TryAdd(schemaId, deserializer);

            return deserializer;
        }

        private static global::Avro.Schema GetSchema(IReflect type)
        {
            return (global::Avro.Schema)type.GetField("_SCHEMA", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
        }

        private static int GetSchemaId(ReadOnlySpan<byte> data)
        {
            if (data.Length < 5)
            {
                throw new InvalidDataException($"Expecting data framing of length 5 bytes or more but total data size is {data.Length} bytes");
            }

            if (data[0] != MagicByte)
            {
                throw new InvalidDataException($"Expecting data with Confluent Schema Registry framing. Magic byte was {data[0]}, expecting {MagicByte}");
            }

            var schemaId = BinaryPrimitives.ReadInt32BigEndian(data.Slice(1));

            return schemaId;
        }

        private class DeserializerWrapper<T> : IAsyncDeserializer<ISpecificRecord> where T: ISpecificRecord
        {
            private readonly AvroDeserializer<T> _avroDeserializer;

            public DeserializerWrapper(ISchemaRegistryClient schemaRegistryClient, AvroDeserializerConfig avroDeserializerConfig = null)
            {
                _avroDeserializer = new AvroDeserializer<T>(schemaRegistryClient, avroDeserializerConfig);
            }

            public async Task<ISpecificRecord> DeserializeAsync(ReadOnlyMemory<byte> data, bool isNull, SerializationContext context)
            {
                return await _avroDeserializer.DeserializeAsync(data, isNull, context);
            }
        }
    }
}
