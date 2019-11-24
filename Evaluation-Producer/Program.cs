using System;
using System.Threading.Tasks;
using Producer;
using Producer.Models.Messages;
using Producer.Services;
using Prometheus;

namespace Evaluation_Producer
{
    class Program
    {
        //private static readonly Counter MessagesBatched = Metrics.CreateCounter("messages_batched", "Number of messages added to batch.", new CounterConfiguration
        //{
        //    LabelNames = new[] { "TopicPartition" }
        //});
        static async Task Main(string[] args)
        {
            Console.WriteLine("Starting Evaluation-Producer");
            EnvironmentVariables.SetFromEnvironmentVariables();
            EnvironmentVariables.PrintProperties();
            var messages = MessageGenerator.GenerateMessages(EnvironmentVariables.AmountOfMessagesVariable);

            var metricServer = new MetricServer(80);
            metricServer.Start();

            switch (EnvironmentVariables.ApplicationType)
            {
                case "Dream-Stream":
                    await DreamStream(messages, EnvironmentVariables.TopicName);
                    break;
                case "Kafka":
                    break;
                case "Nats-Streaming":
                    break;
                default:
                    throw new NotImplementedException($"The method {EnvironmentVariables.ApplicationType} has not been implemented");
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
