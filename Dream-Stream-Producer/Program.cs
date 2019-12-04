using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Producer.Services;

namespace Producer
{
    internal class Program
    {
        private static async Task Main()
        {
            Variables.AmountOfMessagesVariable = 100000;
            Variables.BatchTimerVariable = 20;
            Variables.BatchingSizeVariable = 40000;
            Variables.IsDev = true;
            const string topic = "Topic3";

            var producer = await ProducerService.Setup("http://localhost");
            var messages = MessageGenerator.GenerateMessages(Variables.AmountOfMessagesVariable);
            var messageHeaders = await producer.GetMessageHeaders(messages, topic);

            while (true)
            {
                var sw = new Stopwatch();
                sw.Reset();
                sw.Start();

                var tasks = Enumerable.Range(0, messageHeaders.Length) 
                    .Select(i => producer.Publish(messageHeaders[i], messages[i]));
                await Task.WhenAll(tasks);

                sw.Stop();
                Console.WriteLine($"Sent messages in elapsed in {sw.ElapsedMilliseconds} milliseconds");

                await Task.Delay(500); //Delay added for test of timer on batches
            }
        }
    }
}
