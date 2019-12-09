using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using dotnet_etcd;
using Mvccpb;

namespace Producer.Services
{
    public class BrokerHandler
    {
        public const string TopicTablePrefix = "Topic/";
        public const string BrokerTablePrefix = "Broker/";

        public static async Task<HttpClient[]> UpdateBrokers(EtcdClient client, HttpClient[] brokerHttpClients)
        {
            var rangeResponse = await client.GetRangeValAsync(BrokerTablePrefix);
            var maxBrokerNumber = rangeResponse.Keys.Max(GetBrokerNumber);
            brokerHttpClients = new HttpClient[maxBrokerNumber + 1];
            foreach (var (key, _) in rangeResponse) AddBroker(key, brokerHttpClients);
            return brokerHttpClients;
        }

        public static async Task UpdateBrokerHttpClientsDictionary(EtcdClient client, Dictionary<string, HttpClient> brokerHttpClientsDict, HttpClient[] brokerHttpClients)
        {
            var rangeVal = await client.GetRangeValAsync(TopicTablePrefix);
            foreach (var (key, value) in rangeVal) AddToBrokerHttpClientsDictionary(brokerHttpClientsDict, brokerHttpClients, key, value);
        }

        private static void AddToBrokerHttpClientsDictionary(IDictionary<string, HttpClient> brokerHttpClientsDict, HttpClient[] brokerHttpClients, string key, string value)
        {
            var topicAndPartition = key.Substring(TopicTablePrefix.Length);
            var brokerNumber = GetBrokerNumber(value);

            brokerHttpClientsDict[topicAndPartition] = brokerHttpClients[brokerNumber];
        }

        public static HttpClient[] BrokerTableChangedHandler(WatchEvent[] watchEvents, HttpClient[] brokerHttpClients)
        {
            foreach (var watchEvent in watchEvents)
            {
                switch (watchEvent.Type)
                {
                    case Event.Types.EventType.Put:
                        brokerHttpClients = AddBroker(watchEvent.Key, brokerHttpClients);
                        break;
                    case Event.Types.EventType.Delete:
                        brokerHttpClients = RemoveBroker(watchEvent, brokerHttpClients);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            return brokerHttpClients;
        }

        public static void TopicTableChangedHandler(WatchEvent[] watchEvents, Dictionary<string, HttpClient> brokerHttpClientsDict, HttpClient[] brokerHttpClients)
        {
            foreach (var watchEvent in watchEvents)
            {
                switch (watchEvent.Type)
                {
                    case Event.Types.EventType.Put:
                        AddToBrokerHttpClientsDictionary(brokerHttpClientsDict, brokerHttpClients, watchEvent.Key, watchEvent.Value);
                        break;
                    case Event.Types.EventType.Delete:
                        // Do nothing!!!
                        // todo At least for now we don't do this :D
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        public static HttpClient[] RemoveBroker(WatchEvent watchEvent, HttpClient[] brokerHttpClients)
        {
            var brokerNumber = GetBrokerNumber(watchEvent.Key);
            brokerHttpClients[brokerNumber] = null;
            Console.WriteLine($"Removed Broker {brokerNumber}");
            PrintBrokerHttpClients(brokerHttpClients);
            return brokerHttpClients;
        }

        public static HttpClient[] AddBroker(string keyString, HttpClient[] brokerHttpClients)
        {
            var brokerNumber = GetBrokerNumber(keyString);
            var brokerName = GetBrokerName(keyString);
            if (brokerHttpClients.Length <= brokerNumber)
            {
                Array.Resize(ref brokerHttpClients, brokerNumber + 1);
                Console.WriteLine("Resized brokerUrls");
            }
            var connectionString = Variables.IsDev ? "http://localhost:5041/" : $"http://{brokerName}.broker.default.svc.cluster.local/";
            var httpClient = new HttpClient { BaseAddress = new Uri(connectionString), Timeout = TimeSpan.FromSeconds(5)};
            brokerHttpClients[brokerNumber] = httpClient;
            Console.WriteLine($"Added Broker {brokerName}");
            PrintBrokerHttpClients(brokerHttpClients);

            return brokerHttpClients;
        }

        private static void PrintBrokerHttpClients(HttpClient[] brokerUrls)
        {
            Console.WriteLine("Current BrokerUrls:");
            Array.ForEach(brokerUrls, brokerUrl =>
            {
                if (brokerUrl != null) Console.WriteLine($"{brokerUrl.BaseAddress}");
            });
        }

        private static int GetBrokerNumber(string brokerString)
        {
            var brokerNumberString = brokerString.Split('-').Last();
            int.TryParse(brokerNumberString, out var brokerNumber);
            return brokerNumber;
        }

        private static string GetBrokerName(string brokerString)
        {
            return brokerString.Substring(BrokerTablePrefix.Length);
        }
    }
}