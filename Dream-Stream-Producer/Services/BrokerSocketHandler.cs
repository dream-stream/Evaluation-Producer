using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using dotnet_etcd;
using Mvccpb;

namespace Producer.Services
{
    public class BrokerSocketHandler
    {
        public const string TopicTablePrefix = "Topic/";
        public const string BrokerTablePrefix = "Broker/";

        public static async Task<BrokerSocket[]> UpdateBrokerSockets(EtcdClient client, BrokerSocket[] brokerSockets)
        {
            var rangeResponse = await client.GetRangeValAsync(BrokerTablePrefix);
            var maxBrokerNumber = rangeResponse.Keys.Max(GetBrokerNumber);
            brokerSockets = new BrokerSocket[maxBrokerNumber + 1];
            foreach (var (key, _) in rangeResponse) await AddBroker(key, brokerSockets);
            return brokerSockets;
        }

        public static async Task UpdateBrokerSocketsDictionary(EtcdClient client, Dictionary<string, BrokerSocket> brokerSocketsDict, BrokerSocket[] brokerSockets)
        {
            var rangeVal = await client.GetRangeValAsync(TopicTablePrefix);
            foreach (var (key, value) in rangeVal) AddToBrokerSocketsDictionary(brokerSocketsDict, brokerSockets, key, value);
        }

        private static void AddToBrokerSocketsDictionary(IDictionary<string, BrokerSocket> brokerSocketsDict, BrokerSocket[] brokerSockets, string key, string value)
        {
            var topicAndPartition = key.Substring(TopicTablePrefix.Length);
            var brokerNumber = GetBrokerNumber(value);

            brokerSocketsDict[topicAndPartition] = brokerSockets[brokerNumber];
        }

        public static async Task<BrokerSocket[]> BrokerTableChangedHandler(WatchEvent[] watchEvents, BrokerSocket[] brokerSockets)
        {
            foreach (var watchEvent in watchEvents)
            {
                switch (watchEvent.Type)
                {
                    case Event.Types.EventType.Put:
                        brokerSockets = await AddBroker(watchEvent.Key, brokerSockets);
                        break;
                    case Event.Types.EventType.Delete:
                        brokerSockets = await RemoveBroker(watchEvent, brokerSockets);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            return brokerSockets;
        }

        public static void TopicTableChangedHandler(WatchEvent[] watchEvents, Dictionary<string, BrokerSocket> brokerSocketsDict, BrokerSocket[] brokerSockets)
        {
            foreach (var watchEvent in watchEvents)
            {
                switch (watchEvent.Type)
                {
                    case Event.Types.EventType.Put:
                        AddToBrokerSocketsDictionary(brokerSocketsDict, brokerSockets, watchEvent.Key, watchEvent.Value);
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

        public static async Task<BrokerSocket[]> RemoveBroker(WatchEvent watchEvent, BrokerSocket[] brokerSockets)
        {
            var brokerNumber = GetBrokerNumber(watchEvent.Key);
            await brokerSockets[brokerNumber].CloseConnection();
            brokerSockets[brokerNumber] = null;
            Console.WriteLine($"Removed Broker {brokerNumber}");
            PrintBrokerSockets(brokerSockets);
            return brokerSockets;
        }

        public static async Task<BrokerSocket[]> AddBroker(string keyString, BrokerSocket[] brokerSockets)
        {
            var brokerNumber = GetBrokerNumber(keyString);
            var brokerName = GetBrokerName(keyString);
            if (brokerSockets.Length <= brokerNumber)
            {
                Array.Resize(ref brokerSockets, brokerNumber + 1);
                Console.WriteLine("Resized brokerSockets");
            }
            await CreateStartAndAddBroker(brokerName, brokerNumber, brokerSockets);
            Console.WriteLine($"Added Broker {brokerName}");
            PrintBrokerSockets(brokerSockets);

            return brokerSockets;
        }

        private static void PrintBrokerSockets(BrokerSocket[] brokerSockets)
        {
            Console.WriteLine("Current BrokerSockets:");
            Array.ForEach(brokerSockets, socket =>
            {
                if (socket != null) Console.WriteLine($"{socket.ConnectedTo} - StateIsOpen: {socket.IsOpen()}");
            });
        }

        private static async Task CreateStartAndAddBroker(string brokerName, int brokerNumber, BrokerSocket[] brokerSockets)
        {
            var brokerSocket = new BrokerSocket();
            var connectionString = Variables.IsDev ? "ws://localhost:5041/ws" : $"ws://{brokerName}.broker.default.svc.cluster.local/ws";
            await brokerSocket.ConnectToBroker(connectionString);
            brokerSockets[brokerNumber] = brokerSocket;
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