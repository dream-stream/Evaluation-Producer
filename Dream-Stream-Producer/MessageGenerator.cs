using System;
using System.Security.Cryptography;
using MessagePack;
using Producer.Models.Messages;
using Producer.Services;

namespace Producer
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

        private static int GetPartition(byte[] message, long partitions)
        {
            using SHA512 sha512 = new SHA512Managed();
            var hash = sha512.ComputeHash(message);
            
            return (int)(Math.Abs(BitConverter.ToInt64(hash)) % partitions); //TODO Tryparse (Faster, better, stronger)
        }

        public static MessageHeader[] GenerateMessageHeaders(Message[] messages, string topic, long partitions)
        {
            var messageHeaders = new MessageHeader[messages.Length];
            for (var i = 0; i < messages.Length; i++)
            {
                messageHeaders[i] = new MessageHeader{Topic = topic, Partition = GetPartition(MessagePackSerializer.Serialize(messages[i].Address), partitions)};
            }

            return messageHeaders;
        }
    }
}