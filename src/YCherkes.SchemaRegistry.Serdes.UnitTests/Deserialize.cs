using Avro.Specific;
using Confluent.Kafka;
using Confluent.Kafka.Examples.AvroSpecific;
using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;
using Moq;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using YCherkes.SchemaRegistry.Serdes.Avro;

namespace YCherkes.SchemaRegistry.Serdes.UnitTests
{
    public class Deserialize
    {
        private readonly ISchemaRegistryClient _schemaRegistryClient;
        private readonly Dictionary<string, int> _store = new();
        private readonly string _testTopic;

        public Deserialize()
        {
            _testTopic = "topic";
            var schemaRegistryMock = new Mock<ISchemaRegistryClient>();
#pragma warning disable 618
            schemaRegistryMock.Setup(x => x.ConstructValueSubjectName(_testTopic, It.IsAny<string>())).Returns($"{_testTopic}-value");
            schemaRegistryMock.Setup(x => x.RegisterSchemaAsync("topic-value", It.IsAny<string>())).ReturnsAsync(
                (string _, string schema) => _store.TryGetValue(schema, out int id) ? id : _store[schema] = _store.Count + 1
            );
#pragma warning restore 618
            schemaRegistryMock.Setup(x => x.GetSchemaAsync(It.IsAny<int>(), It.IsAny<string>()))
                .ReturnsAsync((int id, string _) => new Schema(_store.First(x => x.Value == id).Key, null, SchemaType.Avro));
            _schemaRegistryClient = schemaRegistryMock.Object;
        }

        [Fact]
        public void ISpecificRecord_MultiSchemaDeserializer()
        {
            var serializer = new AvroSerializer<ISpecificRecord>(_schemaRegistryClient);
            var deserializer = new MultiSchemaAvroDeserializer(_schemaRegistryClient);

            var user = new User
            {
                favorite_color = "blue",
                favorite_number = 100,
                name = "awesome"
            };

            var bytes = serializer.SerializeAsync(user, new SerializationContext(MessageComponentType.Value, _testTopic)).Result;
            var resultUser = deserializer.DeserializeAsync(bytes, false, new SerializationContext(MessageComponentType.Value, _testTopic)).Result as User;

            Assert.NotNull(resultUser);
            Assert.Equal(user.name, resultUser.name);
            Assert.Equal(user.favorite_color, resultUser.favorite_color);
            Assert.Equal(user.favorite_number, resultUser.favorite_number);
        }

        [Fact]
        public void Multiple_ISpecificRecords_MultiSchemaDeserializer()
        {
            var serializer = new AvroSerializer<ISpecificRecord>(_schemaRegistryClient);
            var deserializer = new MultiSchemaAvroDeserializer(_schemaRegistryClient);

            var user = new User
            {
                favorite_color = "blue",
                favorite_number = 100,
                name = "awesome"
            };

            var car = new Car
            {
                color = "blue",
                name = "great_brand"
            };

            var bytesUser = serializer.SerializeAsync(user, new SerializationContext(MessageComponentType.Value, _testTopic)).Result;
            var resultUser = deserializer.DeserializeAsync(bytesUser, false, new SerializationContext(MessageComponentType.Value, _testTopic)).Result as User;

            Assert.NotNull(resultUser);
            Assert.Equal(user.name, resultUser.name);
            Assert.Equal(user.favorite_color, resultUser.favorite_color);
            Assert.Equal(user.favorite_number, resultUser.favorite_number);

            var bytesCar = serializer.SerializeAsync(car, new SerializationContext(MessageComponentType.Value, _testTopic)).Result;
            var resultCar = deserializer.DeserializeAsync(bytesCar, false, new SerializationContext(MessageComponentType.Value, _testTopic)).Result as Car;

            Assert.NotNull(resultCar);
            Assert.Equal(car.name, resultCar.name);
            Assert.Equal(car.color, resultCar.color);
        }
    }
}
