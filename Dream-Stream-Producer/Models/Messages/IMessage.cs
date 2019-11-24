using MessagePack;

namespace Producer.Models.Messages
{
    [Union(0, typeof(MessageContainer))]
    [Union(3, typeof(Message))]
    [Union(4, typeof(MessageHeader))]
    public interface IMessage
    {
    }
}
