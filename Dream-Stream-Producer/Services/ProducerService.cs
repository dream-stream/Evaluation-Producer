using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using dotnet_etcd;
using MessagePack;
using Producer.Models.Messages;
using Prometheus;

namespace Producer.Services
{
    public class ProducerService : IProducer
    {
        private readonly BatchingService _batchingService;
        private HttpClient[] _brokerClients;
        private readonly Dictionary<string, HttpClient> _brokerUrlsDict = new Dictionary<string, HttpClient>();
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

        public ProducerService(BatchingService batchingService)
        {
            _batchingService = batchingService ?? throw new ArgumentNullException(nameof(batchingService));
        }

        public static async Task<ProducerService> Setup(string etcdConnectionString)
        {
            var producer = new ProducerService(new BatchingService(Variables.BatchingSizeVariable));
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
            _brokerClients = await BrokerHandler.UpdateBrokers(client, _brokerClients);
            await BrokerHandler.UpdateBrokerHttpClientsDictionary(client, _brokerUrlsDict, _brokerClients);
            client.WatchRange(BrokerHandler.BrokerTablePrefix, events =>
            {
                _brokerSocketHandlerLock.WaitOne();
                _brokerClients = BrokerHandler.BrokerTableChangedHandler(events, _brokerClients);
                _brokerSocketHandlerLock.Release();
            });
            client.WatchRange(BrokerHandler.TopicTablePrefix, events =>
            {
                _brokerSocketHandlerLock.WaitOne();
                BrokerHandler.TopicTableChangedHandler(events, _brokerUrlsDict, _brokerClients);
                _brokerSocketHandlerLock.Release();
            });
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
                try
                {
                    if (await SendMessage(messages, header)) break;
                    Console.WriteLine($"SendMessage retry {++retries}");
                    Thread.Sleep(500 * retries);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Failed to send, probably a repartitioning - {e.Message}");
                }
            }
        }

        private async Task<bool> SendMessage(MessageContainer messages, MessageHeader header)
        {
            if (_brokerUrlsDict.TryGetValue($"{header.Topic}/{header.Partition}", out var client))
            {
                var data = LZ4MessagePackSerializer.Serialize<IMessage>(messages);

                await client.PostAsync($"api/Broker?topic={header.Topic}&partition={header.Partition}&length={data.Length}&messageAmount={messages.Messages.Count}", 
                    new StreamContent(new MemoryStream(data)));

                return true;
            }

            return false;
        }
    }
}