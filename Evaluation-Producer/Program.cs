using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Confluent.Kafka;
using MessagePack;
using Producer;
using Producer.Models.Messages;
using Producer.Services;
using Prometheus;

namespace Evaluation_Producer
{
    class Program
    {
        private static readonly Gauge ProducerRunTime = Metrics.CreateGauge("producer_last_run_time", "The amount of ms it took for the producer to publish the number of messages set in the AmountOfMessagesVariable.", new GaugeConfiguration
        {
            LabelNames = new []{ "ApplicationType" }
        });

        private static readonly Counter MessagesPublished = Metrics.CreateCounter("main_messages_published", "The amount of messages published from the producer.", new CounterConfiguration
        {
            LabelNames = new[] { "ApplicationType" }
        });

        static async Task Main(string[] args)
        {
            Console.WriteLine("Starting Evaluation-Producer");
            EnvironmentVariables.SetFromEnvironmentVariables();
            EnvironmentVariables.PrintProperties();
            var messages = MessageGenerator.GenerateMessages(EnvironmentVariables.AmountOfMessagesVariable);

            var metricServer = new MetricServer(80);
            metricServer.Start();

            Console.WriteLine($"Starting {EnvironmentVariables.ApplicationType} Producer");
            switch (EnvironmentVariables.ApplicationType)
            {
                case "Dream-Stream":
                    await DreamStream(messages, EnvironmentVariables.TopicName);
                    break;
                case "Kafka":
                    //await KafkaAwait(messages, EnvironmentVariables.TopicName);
                    await KafkaFlush(messages, EnvironmentVariables.TopicName);
                    break;
                case "Nats-Streaming":
                    break;
                default:
                    throw new NotImplementedException($"The method {EnvironmentVariables.ApplicationType} has not been implemented");
            }
        }

        private static async Task KafkaFlush(Message[] messages, string topicName)
        {
            var delay = EnvironmentVariables.DelayInMillisecond;
            var config = KafkaConfig();

            var stopwatch = new Stopwatch();
            var lastRun = -1;
            const int kafkaSlowFactor = 4;

            using var p = new ProducerBuilder<string, Message>(config).SetValueSerializer(new MySerializer()).Build();
            while (true)
            {
                var time = DateTime.Now;
                if (time.Second != lastRun)
                {
                    var loadPercentage = EnvironmentVariables.Scenario[time.Minute];
                    lastRun = time.Second;

                    stopwatch.Reset();
                    stopwatch.Start();
                    for (var i = 0; i < (messages.Length / 100 * loadPercentage) / kafkaSlowFactor; i++)
                    {
                        p.Produce(topicName, new Message<string, Message> { Key = messages[i].Address, Value = messages[i] }, KafkaProduceHandler);
                    }
                    stopwatch.Stop();
                    ProducerRunTime.WithLabels("Dream-Stream").Set(stopwatch.ElapsedMilliseconds);
                    MessagesPublished.WithLabels("Dream-Stream").Inc(messages.Length);
                }

                await Task.Delay(delay); //Delay added for test of timer on batches
            }
        }

        private static void KafkaProduceHandler(DeliveryReport<string, Message> r)
        {
            //Console.WriteLine($"Delivered message to {r.TopicPartitionOffset}");
            if(r.Error.IsError)
                Console.WriteLine($"Delivery Error: {r.Error.Reason}");
        }

        private static ProducerConfig KafkaConfig()
        {
            var list = new List<string>();
            for (var i = 0; i < 3; i++) list.Add($"kf-kafka-{i}.kf-hs-kafka.default.svc.cluster.local:9093");
            var bootstrapServers = string.Join(',', list);
            var config = new ProducerConfig
            {
                BootstrapServers = bootstrapServers,
                LingerMs = 100
            };
            return config;
        }

        private static async Task KafkaAwait(Message[] messages, string topicName)
        {
            var delay = EnvironmentVariables.DelayInMillisecond;
            var config = KafkaConfig();

            using var producer = new ProducerBuilder<string, string>(config).Build();
            var stopwatch = new Stopwatch();
            ProducerRunTime.WithLabels("Kafka").Set(0);
            MessagesPublished.WithLabels("Kafka").Inc(0);
            while (true)
            {
                stopwatch.Reset();
                stopwatch.Start();
                foreach (var message in messages)
                {
                    try
                    {
                        var dr = await producer.ProduceAsync(topicName, new Message<string, string> { Key = message.Address, Value = JsonSerializer.Serialize(message) });
                        //Console.WriteLine($"Delivered '{dr.Value}' to '{dr.TopicPartitionOffset}'");
                    }
                    catch (ProduceException<Null, string> e)
                    {
                        Console.WriteLine($"Delivery failed Null: {e.Error.Reason}");
                    }
                    catch (ProduceException<string, string> e)
                    {
                        Console.WriteLine($"Delivery failed string: {e.Error.Reason}");
                    }
                }
                stopwatch.Stop();
                ProducerRunTime.WithLabels("Kafka").Set(stopwatch.ElapsedMilliseconds);
                MessagesPublished.WithLabels("Kafka").Inc(messages.Length);
                await Task.Delay(delay); //Delay added for test of timer on batches
            }
        }


        private static async Task DreamStream(Message[] messages, string topic)
        {
            Variables.AmountOfMessagesVariable = EnvironmentVariables.AmountOfMessagesVariable;
            Variables.BatchTimerVariable = EnvironmentVariables.BatchTimerVariable;
            Variables.BatchingSizeVariable = EnvironmentVariables.BatchingSizeVariable;
            var delay = EnvironmentVariables.DelayInMillisecond;
            var producer = await ProducerService.Setup("http://etcd");
            var messageHeaders = await producer.GetMessageHeaders(messages, topic);
            var stopwatch = new Stopwatch();
            var lastRun = -1;
            
            while (true)
            {
                var time = DateTime.Now;
                if (time.Second != lastRun)
                {
                    var loadPercentage = EnvironmentVariables.Scenario[time.Minute];
                    lastRun = time.Second;

                    stopwatch.Reset();
                    stopwatch.Start();

                    var tasks = Enumerable.Range(0, messageHeaders.Length / 100 * loadPercentage)
                        .Select(i => producer.Publish(messageHeaders[i], messages[i]));
                    await Task.WhenAll(tasks);

                    stopwatch.Stop();
                    ProducerRunTime.WithLabels("Dream-Stream").Set(stopwatch.ElapsedMilliseconds);
                    MessagesPublished.WithLabels("Dream-Stream").Inc(messages.Length);
                }

                await Task.Delay(delay); //Delay added for test of timer on batches
            }
        }
    }

    internal class MySerializer : ISerializer<Message>
    {
        public byte[] Serialize(Message data, SerializationContext context)
        {
            return LZ4MessagePackSerializer.Serialize(data);
        }
    }
}
