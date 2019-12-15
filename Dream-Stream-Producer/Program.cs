using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Producer.Services;

namespace Producer
{
    internal class Program
    {
        private static bool _run = true;
        private static ProducerService _producer;

        private static async Task Main()
        {
            Variables.AmountOfMessagesVariable = 25000;
            Variables.BatchTimerVariable = 20;
            Variables.BatchingSizeVariable = 2000;
            Variables.IsDev = true;
            const string topic = "Topic3";

            _producer = await ProducerService.Setup("http://localhost");
            var messages = MessageGenerator.GenerateMessages(Variables.AmountOfMessagesVariable);
            var messageHeaders = await _producer.GetMessageHeaders(messages, topic);
            
            while (_run)
            {
                var sw = new Stopwatch();
                sw.Reset();
                sw.Start();

                var tasks = Enumerable.Range(0, messageHeaders.Length) 
                    .Select(i => _producer.Publish(messageHeaders[i], messages[i]));
                await Task.WhenAll(tasks);

                sw.Stop();
                Console.WriteLine($"Sent messages in elapsed in {sw.ElapsedMilliseconds} milliseconds");

                await Task.Delay(1000); //Delay added for test of timer on batches
            }
        }
    }
}
