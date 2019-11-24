using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Producer.Services
{
    public class BrokerSocket
    {
        private readonly ClientWebSocket _clientWebSocket;
        private readonly Semaphore _lock;

        public string ConnectedTo { get; set; }

        private const int MaxRetries = 15;

        public BrokerSocket()
        {
            _clientWebSocket = new ClientWebSocket();
            _lock = new Semaphore(1, 1);
        }

        public async Task ConnectToBroker(string connectionString)
        {
            var retries = 0;
            while (true)
            {
                try
                {
                    Console.WriteLine($"Connecting to {connectionString}");
                    await _clientWebSocket.ConnectAsync(new Uri(connectionString), CancellationToken.None);
                    ConnectedTo = connectionString;
                    break;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    if (retries++ > MaxRetries) throw new Exception($"Failed to connect to WebSocket {connectionString} after {retries} retries.", e);
                    Console.WriteLine($"Trying to connect to {connectionString} retry {retries}");
                    Thread.Sleep(500 * retries);
                }
            }
        }

        public async Task SendMessage(byte[] message)
        {
            _lock.WaitOne();
            await _clientWebSocket.SendAsync(new ArraySegment<byte>(message, 0, message.Length), WebSocketMessageType.Binary, false, CancellationToken.None);
            _lock.Release();
        }

        public async Task CloseConnection()
        {
            await _clientWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Normal closure", CancellationToken.None);
            Console.WriteLine("Closed the connection");
            _clientWebSocket.Abort();
            Console.WriteLine("Aborted the connection");
            _clientWebSocket.Dispose();
            Console.WriteLine("Disposed the clientWebSocket");
        }

        public bool IsOpen()
        {
            return _clientWebSocket.State == WebSocketState.Open;
        }
    }
}
