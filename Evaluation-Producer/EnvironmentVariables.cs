using System;

namespace Evaluation_Producer
{
    public static class EnvironmentVariables
    {
        public static void SetFromEnvironmentVariables()
        {
            AmountOfMessagesVariable = int.Parse(Environment.GetEnvironmentVariable("MESSAGE_AMOUNT") ?? "1000");
            BatchingSizeVariable = int.Parse(Environment.GetEnvironmentVariable("BATCHING_SIZE") ?? "23");
            BatchTimerVariable = int.Parse(Environment.GetEnvironmentVariable("BATCH_TIMER") ?? "10");
            ApplicationType = Environment.GetEnvironmentVariable("APPLICATION_TYPE") ?? "NOT_SET";
            TopicName = Environment.GetEnvironmentVariable("TOPIC_NAME") ?? "Topic";
        }

        public static void PrintProperties()
        {
            var obj = typeof(EnvironmentVariables);
            foreach (var descriptor in obj.GetProperties())
                Console.WriteLine($"{descriptor.Name}={descriptor.GetValue(obj)}");
        }

        public static string TopicName { get; set; }

        public static int AmountOfMessagesVariable { get; set; }
        public static int BatchingSizeVariable { get; set; } 
        public static int BatchTimerVariable { get; set; }
        public static string ApplicationType { get; set; }
    }
}
