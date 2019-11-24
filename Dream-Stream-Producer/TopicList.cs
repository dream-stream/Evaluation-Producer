using System;
using System.Threading.Tasks;
using dotnet_etcd;

namespace Producer
{
    public class TopicList
    {
        public const string TopicListPrefix = "TopicList/";
        public static async Task<int> GetPartitionCount(EtcdClient client, string topic)
        {
            var key = $"{TopicListPrefix}{topic}";
            Console.WriteLine($"Requesting Topic partition count for {key}");
            var partitionsString = await client.GetValAsync(key);
            if (int.TryParse(partitionsString, out var partitions)) return partitions;
            throw new Exception("Topic does not exists");
        }
    }
}