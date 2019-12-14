using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
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
        private readonly ConcurrentDictionary<string, BrokerSocket> _brokerSocketsDict = new ConcurrentDictionary<string, BrokerSocket>();
        private EtcdClient _client;
        private const int MaxRetries = 10;
        private readonly Semaphore _brokerSocketHandlerLock = new Semaphore(1,1);

        private static readonly Counter MessagesPublished = Metrics.CreateCounter("messages_published", "Number of messages added to batch.", new CounterConfiguration
        {
            LabelNames = new []{"TopicPartition"}
        });
        private static readonly Counter BatchMessagesPublished = Metrics.CreateCounter("batch_messages_published", "Number of batches sent.", new CounterConfiguration
        {
            LabelNames = new[] { "BrokerConnection" }
        });

        private static readonly Counter MessagesPublishedSizeInBytes = Metrics.CreateCounter("messages_published_size_in_bytes", "", new CounterConfiguration
        {
            LabelNames = new[] { "TopicPartition" }
        });

        private static readonly Gauge MessageBatchSize = Metrics.CreateGauge("message_batch_size", "The size of the last sent batch.");
        private bool _taken;

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
                    await brokerSocket.DeleteConnection();
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
                await SendMessage(header, messages);
            });
            var timer = new Timer(callback, null, Timeout.Infinite, Timeout.Infinite);

            _batchingService.CreateBatch(header, message, timer);
        }

        private async Task TryToSendWithRetries(MessageHeader header, MessageContainer messages)
        {
            var retries = 0;
            while (retries < MaxRetries)
            {
                try
                {
                    if (await SendMessage(header, messages)) break;
                    Console.WriteLine($"SendMessage retry {++retries}");
                    await Task.Delay(500 * retries);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Failed to send, probably a repartitioning - {e.Message}");
                }
            }
        }

        private async Task<bool> SendMessage(MessageHeader header, MessageContainer messages)
        {
            try
            {
                if (_brokerSocketsDict.TryGetValue($"{header.Topic}/{header.Partition}", out var brokerSocket))
                {
                    if (brokerSocket == null) throw new Exception("Failed to get brokerSocket");
                    if (!brokerSocket.IsOpen())
                    {
                        await ForceUpdateBrokers();
                        return false;
                    }
                    var message = _serializer.Serialize<IMessage>(messages);
                    await brokerSocket.SendMessage(message);
                    //Console.WriteLine($"Sent batched messages to socket {brokerSocket.ConnectedTo} with topic {header.Topic} with partition {header.Partition}");
                    MessagesPublished.WithLabels($"{header.Topic}/{header.Partition}").Inc(messages.Messages.Count);
                    MessagesPublishedSizeInBytes.WithLabels($"{header.Topic}/{header.Partition}").Inc(message.Length);
                    MessageBatchSize.Set(messages.Messages.Count);

                    BatchMessagesPublished.WithLabels(brokerSocket.ConnectedTo).Inc();
                    BatchMessagesPublished.WithLabels($"{header.Topic}/{header.Partition}").Inc();

                    return true;
                }

                Console.WriteLine("Failed to send message");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);

                await ForceUpdateBrokers();
                return false;
            }
            
            throw new Exception("Failed to get brokerSocket");
        }

        private async Task ForceUpdateBrokers()
        {
            if (_taken == false)
            {
                Console.WriteLine("Force update");
                _taken = true;
                _brokerSockets = await BrokerSocketHandler.UpdateBrokerSockets(_client, _brokerSockets);
                if (_brokerSockets.Length == 0)
                    _brokerSocketsDict.Clear();
                else
                    await BrokerSocketHandler.UpdateBrokerSocketsDictionary(_client, _brokerSocketsDict, _brokerSockets);
                await Task.Delay(1000);
                _taken = false;
            }
        }
    }
}