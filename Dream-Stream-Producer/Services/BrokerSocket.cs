using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Producer.Services
{
    public class BrokerSocket
    {
        private ClientWebSocket _clientWebSocket;
        private readonly SemaphoreSlim _lock;
        
        public string ConnectedTo { get; set; }

        private const int MaxRetries = 15;

        public BrokerSocket()
        {
            _clientWebSocket = new ClientWebSocket();
            _clientWebSocket.Options.SetBuffer(1024 * 1000, 1024 * 1000);
            _lock = new SemaphoreSlim(1, 1);
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
                catch (InvalidOperationException e)
                {
                    if (_clientWebSocket.State != WebSocketState.Open)
                        await _clientWebSocket.ConnectAsync(new Uri(ConnectedTo), CancellationToken.None);
                    break;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    _clientWebSocket.Dispose();
                    _clientWebSocket = new ClientWebSocket();
                    if (++retries > MaxRetries) throw new Exception($"Failed to connect to WebSocket {connectionString} after {retries} retries.", e);
                    Console.WriteLine($"Trying to connect to {connectionString} retry {retries}");
                    Thread.Sleep(500 * retries);
                }
            }
        }

        public async Task SendMessage(byte[] message)
        {
            await _lock.WaitAsync();
            try
            {
                await _clientWebSocket.SendAsync(new ArraySegment<byte>(message, 0, message.Length),
                    WebSocketMessageType.Binary, false, CancellationToken.None);
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task DeleteConnection()
        {
            try
            {
                await _clientWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Normal closure", CancellationToken.None);
                Console.WriteLine("Closed the connection");
                _clientWebSocket.Abort();
                Console.WriteLine("Aborted the connection");
                _clientWebSocket.Dispose();
                Console.WriteLine("Disposed the clientWebSocket");
            }
            catch (Exception e)
            {
                Console.WriteLine("Socket has already been closed");
            }
        }

        public bool IsOpen()
        {
            return _clientWebSocket.State == WebSocketState.Open;
        }
    }
}
