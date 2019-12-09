using System;
using System.Linq;

namespace Evaluation_Producer
{
    public static class EnvironmentVariables
    {
        public static void SetFromEnvironmentVariables()
        {
            AmountOfMessagesVariable = int.Parse(Environment.GetEnvironmentVariable("MESSAGE_AMOUNT") ?? "1000");
            BatchingSizeVariable = int.Parse(Environment.GetEnvironmentVariable("BATCHING_SIZE") ?? "23");
            BatchTimerVariable = int.Parse(Environment.GetEnvironmentVariable("BATCH_TIMER") ?? "10");
            ApplicationType = Environment.GetEnvironmentVariable("APPLICATION_TYPE") ?? "Dream-Stream";
            TopicName = Environment.GetEnvironmentVariable("TOPIC_NAME") ?? "Topic";
            DelayInMillisecond = int.Parse(Environment.GetEnvironmentVariable("DELAY_IN_MILLISECOND") ?? "15000");
            Scenario = (Environment.GetEnvironmentVariable("SCENARIO") ??
                @"100,100,100,100,100,100,100,100,100,100,
                  100,100,100,100,100,100,100,100,100,100,
                  100,100,100,100,100,100,100,100,100,100,
                  100,100,100,100,100,100,100,100,100,100,
                  100,100,100,100,100,100,100,100,100,100,
                  100,100,100,100,100,100,100,100,100,100")
                .Split(',').Select(n => Convert.ToInt32(n)).ToArray();
        }

        public static void PrintProperties()
        {
            var obj = typeof(EnvironmentVariables);
            foreach (var descriptor in obj.GetProperties())
                Console.WriteLine($"{descriptor.Name}={descriptor.GetValue(obj)}");
        }

        public static string TopicName { get; set; }
        public static int[] Scenario { get; set; }

        public static int AmountOfMessagesVariable { get; set; }
        public static int BatchingSizeVariable { get; set; } 
        public static int BatchTimerVariable { get; set; }
        public static string ApplicationType { get; set; }
        public static int DelayInMillisecond { get; set; }
    }
}
