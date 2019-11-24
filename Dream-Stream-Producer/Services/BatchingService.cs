using System;
using System.Collections.Generic;
using System.Threading;
using Producer.Models.Messages;

namespace Producer.Services
{
    public class BatchingService
    {
        private readonly Dictionary<MessageHeader, TimedBatchingQueue> _messages;
        private readonly int _batchingSize;

        public BatchingService(int batchingSize)
        {
            _batchingSize = batchingSize;
            _messages = new Dictionary<MessageHeader, TimedBatchingQueue>();
        }

        public bool TryBatchMessage(MessageHeader header, Message message, out MessageHeader queueFull)
        {
            if (!_messages.TryGetValue(header, out var list))
            {
                queueFull = null;
                return false;
            }

            list.MessageContainer.Messages.Add(message);
            if (list.MessageContainer.Messages.Count == 1)
            {
                list.Timer.Change(TimeSpan.FromSeconds(Variables.BatchTimerVariable), TimeSpan.FromSeconds(Variables.BatchTimerVariable));
            }

            queueFull = list.MessageContainer.Messages.Count == _batchingSize ? header : null;
            return true;
        }

        public MessageContainer GetMessages(MessageHeader header)
        {
            if (!_messages.TryGetValue(header, out var list))
                throw new KeyNotFoundException(
                    $"Messages for {nameof(header.Topic)}: {header.Topic} and {nameof(header.Partition)}: {header.Partition} not found");
            
            var returnList = new MessageContainer {Header = header, Messages = new List<Message>(list.MessageContainer.Messages)};
            list.MessageContainer.Messages.Clear();
            list.Timer.Change(Timeout.Infinite, Timeout.Infinite);
            return returnList;
        }

        public void CreateBatch(MessageHeader header, Message message, Timer timer)
        {
            Console.WriteLine($"Creating Queue {header.Topic}, {header.Partition}");
            _messages.Add(header, new TimedBatchingQueue
            {
                Timer = timer,
                MessageContainer = new MessageContainer
                {
                    Header = header,
                    Messages = new List<Message> {message}
                }
            });
        }
    }
}
