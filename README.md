Avro deserializer for reading messages serialized with multiple schemas.
===========================================================================================

<p align="center">
 <a href="https://www.nuget.org/packages/YCherkes.SchemaRegistry.Serdes.Avro"><img src="https://img.shields.io/badge/Nuget-v1.0.2-blue?logo=nuget"></a>
</p>

To install YCherkes.SchemaRegistry.Serdes.Avro from within Visual Studio, search for YCherkes.SchemaRegistry.Serdes.Avro in the NuGet Package Manager UI, or run the following command in the Package Manager Console:

```
Install-Package YCherkes.SchemaRegistry.Serdes.Avro -Version 1.0.2
```

To add a reference to a dotnet core project, execute the following at the command line:

```
dotnet add package -v 1.0.2 YCherkes.SchemaRegistry.Serdes.Avro
```


### Basic Consumer Example

```csharp
using System;
using System.Threading;
using Avro.Specific;
using Confluent.Kafka;
using Confluent.Kafka.SyncOverAsync;
using Confluent.SchemaRegistry;
using YCherkes.SchemaRegistry.Serdes.Avro;

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
```
