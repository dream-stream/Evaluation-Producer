using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MessagePack;
using Producer;
using Producer.Models.Messages;
using Producer.Serialization;
using Producer.Services;
using Xunit;
using Xunit.Abstractions;

namespace UnitTest
{
    public class SerializationTests
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public SerializationTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        [Fact]
        public void SerializeWholeMessageVsHeaderAndMessage()
        {
            var stopWatch1 = new Stopwatch();
            var messages = MessageGenerator.GenerateMessages(200);
            var header = new MessageHeader { Topic = "Topic1", Partition = 1 };
            var serializer = new Serializer();
            var container = new MessageContainer
            {
                Header = header,
                Messages = messages.ToList()
            };

            stopWatch1.Start();
            var data = serializer.Serialize(container);
            serializer.Deserialize<MessageContainer>(data);
            stopWatch1.Stop();

            _testOutputHelper.WriteLine($"Whole message: {stopWatch1.Elapsed}");
        }

        [Fact]
        public void Serialization2()
        {
            var stopWatch = new Stopwatch();
            var messages = MessageGenerator.GenerateMessages(200);
            var header = new MessageHeader { Topic = "Topic1", Partition = 1 };
            var serializer = new Serializer();
            var container = new MessageContainer
            {
                Header = header,
                Messages = messages.ToList()
            };

            stopWatch.Start();
            var data2 = serializer.Serialize(container.Messages);
            var dataHeader = serializer.Serialize(container);

            serializer.Deserialize<MessageContainer>(dataHeader);
            stopWatch.Stop();

            _testOutputHelper.WriteLine($"Serialized twice message: {stopWatch.Elapsed}");
        }

        [Fact]
        public async Task ConcurrencyTest1() //TODO Test2 does not have a fair comparison
        {
            var socket = new BrokerSocket();
            var messages = MessageGenerator.GenerateMessages(200);
            var headers = MessageGenerator.GenerateMessageHeaders(messages, "Topic1", 1);
            var stopWatch = new Stopwatch();
            var container = new MessageContainer[headers.Length];
            var semaphore = new Semaphore(1, 1);

            for (var i = 0; i < headers.Length; i++)
            {
                container[i] = new MessageContainer
                {
                    Header = headers[i],
                    Messages = messages.ToList()
                };
            }

            var data = new byte[headers.Length][];
            for (int i = 0; i < container.Length; i++)
            {
                data[i] = LZ4MessagePackSerializer.Serialize(container[i]);
            }

            var tasks = new Task[data.Length];

            for (int i = 0; i < data.Length; i++)
            {
                var i1 = i;
                tasks[i] = new Task(async () =>
                {
                    semaphore.WaitOne();
                    await socket.SendMessage(data[i1]);
                    semaphore.Release();
                });
            }

            stopWatch.Start();
            await socket.ConnectToBroker("ws://localhost:5000/ws");

            for (int i = 0; i < data.Length; i++)
            {
                tasks[i].Start();
            }

            stopWatch.Stop();
            await Task.Delay(2*1000);
            await socket.DeleteConnection();
            _testOutputHelper.WriteLine($"Time: {stopWatch.ElapsedTicks}");
        }

        [Fact]
        public async Task ConcurrencyTest2() //TODO Not a fair comparison
        {
            var messages = MessageGenerator.GenerateMessages(200);
            var headers = MessageGenerator.GenerateMessageHeaders(messages, "Topic1", 1);
            var stopWatch = new Stopwatch();
            var container = new MessageContainer[headers.Length];
            var semaphore = new Semaphore(1, 1);

            for (var i = 0; i < headers.Length; i++)
            {
                container[i] = new MessageContainer
                {
                    Header = headers[i],
                    Messages = messages.ToList()
                };
            }

            var data = new byte[headers.Length][];
            for (int i = 0; i < container.Length; i++)
            {
                data[i] = LZ4MessagePackSerializer.Serialize(container[i]);
            }

            var tasks = new Task[data.Length];
            var sockets = new BrokerSocket[data.Length];


            for (int i = 0; i < data.Length; i++)
            {
                var i1 = i;
                sockets[i] = new BrokerSocket();
                tasks[i] = new Task(async () =>
                {
                    await sockets[i1].SendMessage(data[i1]);
                });
            }

            stopWatch.Start();
            for (int i = 0; i < data.Length; i++)
            {
                await sockets[i].ConnectToBroker("ws://localhost:5000/ws");
                tasks[i].Start();
            }

            stopWatch.Stop();
            await Task.Delay(2 * 1000);

            for (int i = 0; i < data.Length; i++)
            {
                await sockets[i].DeleteConnection();
            }

            _testOutputHelper.WriteLine($"Time: {stopWatch.ElapsedTicks}");
        }

        [Fact]
        public async Task ThreadingTest()
        {
            var myThreadValue = 0L;
            var myTaskValue = 0L;


            for (int i = 0; i < 10; i++)
            {
                var task = Task.Run(() =>
                {
                    ThreadPool.QueueUserWorkItem(x =>
                    {
                        for (var i = 0; i < 100000000; i++)
                        {
                            Interlocked.Increment(ref myThreadValue);

                            if(i == 100000000 - 1)
                                _testOutputHelper.WriteLine("Made it through");
                        }
                    });

                    Task.Run(async () =>
                    {
                        for (var i = 0; i < 100000000; i++)
                        {
                            Interlocked.Increment(ref myTaskValue);
                            if (i == 100000000 - 1)
                                _testOutputHelper.WriteLine("Made it through");
                        }

                    });
                });
                await Task.WhenAll(task);
                await Task.Delay(100);
                _testOutputHelper.WriteLine($"{i} {myThreadValue}");
                _testOutputHelper.WriteLine($"{i} {myTaskValue}");
            }

            _testOutputHelper.WriteLine("Out");

            



            await Task.Delay(1000);

            _testOutputHelper.WriteLine($"{myThreadValue}");
            _testOutputHelper.WriteLine($"{myTaskValue}");

            _testOutputHelper.WriteLine("Long delay");
            await Task.Delay(30000);
            _testOutputHelper.WriteLine("Done");
            _testOutputHelper.WriteLine($"{myThreadValue}");
            _testOutputHelper.WriteLine($"{myTaskValue}");

        }
    }
}
