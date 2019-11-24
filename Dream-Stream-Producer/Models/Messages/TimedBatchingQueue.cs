using System.Threading;

namespace Producer.Models.Messages
{
    class TimedBatchingQueue
    {
        public Timer Timer { get; set; }
        public MessageContainer MessageContainer { get; set; }
    }
}
