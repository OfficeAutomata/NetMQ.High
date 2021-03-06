﻿using System;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;

namespace NetMQ.High.Tests
{
    [TestFixture]
    class ClientTimeoutServerTests
    {
        class DelayedHandler : IAsyncHandler
        {
            readonly int delay;
            public DelayedHandler(int delay) => this.delay = delay;
            public void HandleOneWay(ulong messageId, uint connectionId, string service, byte[] body) { }
            public async Task<byte[]> HandleRequestAsync(ulong messageId, uint connectionId, string service, byte[] body)
            {
                await Task.Delay(delay);
                var text = $"Delayed for {delay} milliseconds";
                return Encoding.ASCII.GetBytes(text);
            }
        }

        [Test]
        public void RequestResponse_BelowTimeout_ReturnsTextReply()
        {
            var handler = new DelayedHandler(1000);
            using (var server = new AsyncServer(handler))
            {
                server.Init();
                server.Bind("tcp://*:6666");
                using (var client = new ClientTimeout("tcp://localhost:6666", 2000))
                {
                    client.Init();
                    var message = Encoding.ASCII.GetBytes("World");
                    var reply = client.SendRequestAsync("Hello", message).Result;
                    var text = Encoding.ASCII.GetString(reply);
                    Assert.That(text == "Delayed for 1000 milliseconds");
                }
            }
        }

        [Test]
        public void RequestResponse_AboveTimeout_ThrowsTimeoutException()
        {
            var handler = new DelayedHandler(2000);
            using (var server = new AsyncServer(handler))
            {
                server.Init();
                server.Bind("tcp://*:6666");
                using (var client = new ClientTimeout("tcp://localhost:6666", 1000))
                {
                    client.Init();
                    var message = Encoding.ASCII.GetBytes("World");
                    Assert.Throws<TimeoutException>(
                        async () => await client.SendRequestAsync("Hello", message));
                }
            }
        }


        [Test]
        public void SendRequestAsync_NotConnected_NotThrows()
        {
            using (var client = new ClientTimeout("inproc://test", 2000))
            {
                client.Init();
                Task.Delay(1000).Wait(); // Simulate delay to press a button in UI
                Assert.DoesNotThrow(
                    () => client.SendRequestAsync("serice", Encoding.ASCII.GetBytes("World")));
            }
        }

        [Test]
        public void SendRequestAsync_NotConnected_Disposed_Blocks()
        {
            using (var client = new ClientTimeout("inproc://test", 2000))
            {
                client.Init();
                client.Dispose();
                Task.Delay(1000).Wait(); // Simulate delay to press a button in UI

                // Block on m_outgoingQueue.Enqueue(outgoingMessage) // Enqueue an item to the queue, will block if the queue is full.
                client.SendRequestAsync("serice", Encoding.ASCII.GetBytes("World"));
            }
        }
    }
}