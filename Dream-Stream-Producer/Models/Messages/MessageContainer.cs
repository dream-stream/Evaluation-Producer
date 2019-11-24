using System.Collections.Generic;
using MessagePack;

namespace Producer.Models.Messages
{
    [MessagePackObject]
    public class MessageContainer : IMessage
    {
        [Key(1)]
        public MessageHeader Header { get; set; }
        [Key(2)]
        public List<Message> Messages { get; set; }
    }
}
