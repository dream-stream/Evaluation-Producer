using System;
using Producer.Models.Messages;
using Producer.Services;

namespace Evaluation_Producer
{
    public static class MessageGenerator
    {
        public static Message[] GenerateMessages(int amount)
        {
            Console.WriteLine($"Generating {amount} unique Messages!");
            var messages = new Message[amount];

            for (var i = 0; i < amount; i++)
            {
                messages[i] = new Message
                {
                    Address = Addresses.GetAddress(),
                    LocationDescription = "First floor bathroom",
                    Measurement = 23.5,
                    SensorType = "Temperature",
                    Unit = "°C"
                };
            }
            return messages;
        }
    }
}