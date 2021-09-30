using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Akka;
using Akka.Actor;
using Akka.Configuration;
using Akka.Streams;
using Akka.Streams.Dsl;
using Akka.Streams.Kafka.Dsl;
using Akka.Streams.Kafka.Helpers;
using Akka.Streams.Kafka.Messages;
using Akka.Streams.Kafka.Settings;
using Avro.Specific;
using Confluent.Kafka;
using Confluent.Kafka.SyncOverAsync;
using Confluent.SchemaRegistry;
using YCherkes.SchemaRegistry.Serdes.Avro;
using Config = Akka.Configuration.Config;

namespace Samples.Akka.Streams.Kafka
{
    class Program
    {
        public static async Task Main()
        {

            var topic = "my-topic";

            Config fallbackConfig = ConfigurationFactory.ParseString(@"
                    akka.suppress-json-serializer-warning=true
                    akka.loglevel = DEBUG
                ").WithFallback(ConfigurationFactory.FromResource<ConsumerSettings<object, object>>("Akka.Streams.Kafka.reference.conf"));

            var system = ActorSystem.Create("TestKafka", fallbackConfig);
            var materializer = system.Materializer();

            var schemaRegistryConfig = new SchemaRegistryConfig
            {
                Url = "http://localhost:8081"
            };

            using var schemaRegistry = new CachedSchemaRegistryClient(schemaRegistryConfig);

            var consumerSettings = ConsumerSettings<Null, ISpecificRecord>.Create(system, null, new MultiSchemaAvroDeserializer(schemaRegistry).AsSyncOverAsync())
                .WithBootstrapServers("localhost:9092")
                .WithGroupId("test-consumer-group")
                .WithProperties(new Dictionary<string, string>
                {
                    {"auto.offset.reset", "Earliest"}
                });

            var subscription = Subscriptions.Topics(topic);

            var committerDefaults = CommitterSettings.Create(system);

            // Comment for simple no-commit consumer
            DrainingControl<NotUsed> control = KafkaConsumer.CommittableSource(consumerSettings, subscription)
                .SelectAsync(1, msg =>
                    Business(msg.Record).ContinueWith(done => (ICommittable)msg.CommitableOffset))
                .ToMaterialized(
                    Committer.Sink(committerDefaults.WithMaxBatch(100)),
                    DrainingControl<NotUsed>.Create)
                .Run(materializer);

            Console.WriteLine("Press any key to stop consumer.");
            Console.ReadKey();

            // Comment for simple no-commit consumer
            await control.Stop();
            await system.Terminate();
        }

        private static Task Business(ConsumeResult<Null, ISpecificRecord> record)
        {
            Console.WriteLine($"Consumer: {record.Topic}/{record.Partition} {record.Offset}: {record.Message.Value}");
            return Task.CompletedTask;
        }
    }
}
