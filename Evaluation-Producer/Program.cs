using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Confluent.Kafka;
using Producer;
using Producer.Models.Messages;
using Producer.Services;
using Prometheus;

namespace Evaluation_Producer
{
    class Program
    {
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
                    await Kafka(messages, EnvironmentVariables.TopicName);
                    break;
                case "Nats-Streaming":
                    break;
                default:
                    throw new NotImplementedException($"The method {EnvironmentVariables.ApplicationType} has not been implemented");
            }
        }

        private static async Task Kafka(Message[] messages, string topicName)
        {
            var list = new List<string>();
            for (var i = 0; i < 3; i++) list.Add($"kf-kafka-{i}.kf-hs-kafka.default.svc.cluster.local:9093");
            var bootstrapServers = string.Join(',', list);
            var config = new ProducerConfig { BootstrapServers = bootstrapServers };

            using var producer = new ProducerBuilder<string, string>(config).Build();

            while (true)
            {
                foreach (var message in messages)
                {
                    try
                    {
                        var dr = await producer.ProduceAsync(topicName, new Message<string, string> {Key = message.Address ,Value = JsonSerializer.Serialize(message) });
                        Console.WriteLine($"Delivered '{dr.Value}' to '{dr.TopicPartitionOffset}'");
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

                await Task.Delay(15 * 1000); //Delay added for test of timer on batches
            }
        }

        private static async Task DreamStream(Message[] messages, string topic)
        {
            Variables.AmountOfMessagesVariable = EnvironmentVariables.AmountOfMessagesVariable;
            Variables.BatchTimerVariable = EnvironmentVariables.BatchTimerVariable;
            Variables.BatchingSizeVariable = EnvironmentVariables.BatchTimerVariable;
            var producer = await ProducerService.Setup("http://etcd");
            var messageHeaders = await producer.GetMessageHeaders(messages, topic);
            while (true)
            {
                for (var i = 0; i < messageHeaders.Length; i++)
                {
                    await producer.Publish(messageHeaders[i], messages[i]);
                }

                await Task.Delay(15 * 1000); //Delay added for test of timer on batches
            }
        }
    }
}
