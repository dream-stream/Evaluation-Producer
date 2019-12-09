using System.Threading.Tasks;
using dotnet_etcd;
using Producer.Models.Messages;

namespace Producer.Services
{
    public interface IProducer
    {
        Task Publish(MessageHeader header, Message message);
    }
}
