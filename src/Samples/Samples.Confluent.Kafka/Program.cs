using System;
using System.Threading;
using Avro.Specific;
using Confluent.Kafka;
using Confluent.Kafka.SyncOverAsync;
using Confluent.SchemaRegistry;
using YCherkes.SchemaRegistry.Serdes.Avro;

namespace Samples.Confluent.Kafka
{
    class Program
    {
        public static void Main()
        {
            var schemaRegistryConfig = new SchemaRegistryConfig
            {
                Url = "http://localhost:8081"
            };

            using var schemaRegistry = new CachedSchemaRegistryClient(schemaRegistryConfig);

            var consumerConfig = new ConsumerConfig
            {
                GroupId = "test-consumer-group",
                BootstrapServers = "localhost:9092",
                // Note: The AutoOffsetReset property determines the start offset in the event
                // there are not yet any committed offsets for the consumer group for the
                // topic/partitions of interest. By default, offsets are committed
                // automatically, so in this example, consumption will only start from the
                // earliest message in the topic 'my-topic' the first time you run the program.
                AutoOffsetReset = AutoOffsetReset.Earliest
            };

            using var consumer = new ConsumerBuilder<Ignore, ISpecificRecord>(consumerConfig)
                .SetValueDeserializer(new MultiSchemaAvroDeserializer(schemaRegistry).AsSyncOverAsync())
                .Build();

            consumer.Subscribe("my-topic");

            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) => {
                e.Cancel = true; // prevent the process from terminating.
                cts.Cancel();
            };

            try
            {
                while (true)
                {
                    try
                    {
                        var consumeResult = consumer.Consume(cts.Token);
                        Console.WriteLine($"Consumed message type '{consumeResult.Message.Value?.GetType()}' at: '{consumeResult.TopicPartitionOffset}'.");
                    }
                    catch (ConsumeException e)
                    {
                        Console.WriteLine($"Error occurred: {e.Error.Reason}");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Ensure the consumer leaves the group cleanly and final offsets are committed.
                consumer.Close();
            }
        }
    }
}
