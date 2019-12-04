using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using dotnet_etcd;
using Producer.Models.Messages;
using Producer.Serialization;
using Prometheus;

namespace Producer.Services
{
    public class ProducerService : IProducer
    {
        private readonly ISerializer _serializer;
        private readonly BatchingService _batchingService;
        private BrokerSocket[] _brokerSockets;
        private readonly Dictionary<string, BrokerSocket> _brokerSocketsDict = new Dictionary<string, BrokerSocket>();
        private EtcdClient _client;
        private const int MaxRetries = 5;
        private readonly Semaphore _brokerSocketHandlerLock = new Semaphore(1,1);

        private static readonly Counter MessagesBatched = Metrics.CreateCounter("messages_batched", "Number of messages added to batch.", new CounterConfiguration
        {
            LabelNames = new []{"TopicPartition"}
        });
        private static readonly Counter MessageBatchesSent = Metrics.CreateCounter("message_batches_sent", "Number of batches sent.", new CounterConfiguration
        {
            LabelNames = new[] { "BrokerConnection" }
        });

        public ProducerService(ISerializer serializer, BatchingService batchingService)
        {
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _batchingService = batchingService ?? throw new ArgumentNullException(nameof(batchingService));
        }

        public static async Task<ProducerService> Setup(string etcdConnectionString)
        {
            var producer = new ProducerService(new Serializer(), new BatchingService(Variables.BatchingSizeVariable));
            var etcdClient = new EtcdClient(etcdConnectionString);
            await producer.InitSockets(etcdClient);

            return producer;
        }

        public async Task<MessageHeader[]> GetMessageHeaders(Message[] messages, string topic)
        {
            var partitionCount = await TopicList.GetPartitionCount(_client, topic);
            return MessageGenerator.GenerateMessageHeaders(messages, topic, partitionCount);
        }


        private async Task InitSockets(EtcdClient client)
        {
            _client = client;
            _brokerSockets = await BrokerSocketHandler.UpdateBrokerSockets(client, _brokerSockets);
            await BrokerSocketHandler.UpdateBrokerSocketsDictionary(client, _brokerSocketsDict, _brokerSockets);
            client.WatchRange(BrokerSocketHandler.BrokerTablePrefix, async events =>
            {
                _brokerSocketHandlerLock.WaitOne();
                _brokerSockets = await BrokerSocketHandler.BrokerTableChangedHandler(events, _brokerSockets);
                _brokerSocketHandlerLock.Release();
            });
            client.WatchRange(BrokerSocketHandler.TopicTablePrefix, events =>
            {
                _brokerSocketHandlerLock.WaitOne();
                BrokerSocketHandler.TopicTableChangedHandler(events, _brokerSocketsDict, _brokerSockets);
                _brokerSocketHandlerLock.Release();
            });
        }

        public async Task CloseConnections()
        {
            foreach (var brokerSocket in _brokerSockets)
            {
                if (brokerSocket != null)
                    await brokerSocket.CloseConnection();
            }
            _client.Dispose();
        }

        public async Task Publish(MessageHeader header, Message message)
        {
            
            if (_batchingService.TryBatchMessage(header, message, out var queueFull))
            {
                if (queueFull == null) return;
                var messages = _batchingService.GetMessages(queueFull);
                await TryToSendWithRetries(header, messages);

                return;
            }

            var callback = new TimerCallback(async x =>
            {
                var messages = _batchingService.GetMessages(header);
                await TryToSendWithRetries(header, messages);
            });
            var timer = new Timer(callback, null, TimeSpan.FromSeconds(Variables.BatchTimerVariable), TimeSpan.FromSeconds(Variables.BatchTimerVariable));

            _batchingService.CreateBatch(header, message, timer);
        }

        private async Task TryToSendWithRetries(MessageHeader header, MessageContainer messages)
        {
            var retries = 0;
            while (retries < MaxRetries)
            {
                if (await SendMessage(messages, header)) break;
                Console.WriteLine($"SendMessage retry {++retries}");
                Thread.Sleep(500 * retries);
            }
        }

        private async Task<bool> SendMessage(MessageContainer messages, MessageHeader header)
        {
            MessagesBatched.WithLabels($"{header.Topic}/{header.Partition}").Inc(messages.Messages.Count);
            if (_brokerSocketsDict.TryGetValue($"{header.Topic}/{header.Partition}", out var brokerSocket))
            {
                if(brokerSocket == null) throw new Exception("Failed to get brokerSocket");
                if(!brokerSocket.IsOpen()) return false;
                var message = _serializer.Serialize<IMessage>(messages);
                await brokerSocket.SendMessage(message);
                //Console.WriteLine($"Sent batched messages to socket {brokerSocket.ConnectedTo} with topic {header.Topic} with partition {header.Partition}");
                MessageBatchesSent.WithLabels(brokerSocket.ConnectedTo).Inc();
                MessageBatchesSent.WithLabels($"{header.Topic}/{header.Partition}").Inc();
                return true;
            }

            throw new Exception("Failed to get brokerSocket");
        }
    }
}