﻿using System;
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
            Variables.AmountOfMessagesVariable = 100000;
            Variables.BatchTimerVariable = 20;
            Variables.BatchingSizeVariable = 40000;
            Variables.IsDev = true;
            const string topic = "Topic3";

            _producer = await ProducerService.Setup("http://localhost");
            var messages = MessageGenerator.GenerateMessages(Variables.AmountOfMessagesVariable);
            var messageHeaders = await _producer.GetMessageHeaders(messages, topic);
            AppDomain.CurrentDomain.ProcessExit += ShutdownFunction;
            
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

                await Task.Delay(500); //Delay added for test of timer on batches
            }
        }

        private static async void ShutdownFunction(object sender, EventArgs e)
        {
            _run = false;
            await _producer.CloseConnections();
            Console.WriteLine("Process is exiting!");
        }
    }
}
