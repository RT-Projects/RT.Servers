using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using NUnit.Framework;
using RT.Util.ExtensionMethods;
using RT.Util.Streams;

namespace RT.Servers.Tests
{
    [TestFixture]
    public sealed class DisconnectionTests
    {
        private IEnumerable<string> enumInfinite()
        {
            while (true)
            {
                yield return "blah!";
                Thread.Sleep(0);
            }
        }

        [Test]
        public void TestMidResponseSocketClosure()
        {
            var instance = new HttpServer(new HttpServerOptions { Port = ProgramServersTests.Port })
            {
                Handler = new UrlPathResolver(
                    new UrlPathHook(req => { return HttpResponse.Create(enumInfinite(), "text/plain"); }, path: "/infinite-and-slow")
                ).Handle
            };
            try
            {
                instance.StartListening();

                ThreadStart thread = () =>
                {
                    for (int i = 0; i < 20; i++)
                    {
                        TcpClient cl = new TcpClient();
                        cl.Connect("localhost", ProgramServersTests.Port);
                        cl.ReceiveTimeout = 1000; // 1 sec
                        Socket sck = cl.Client;
                        sck.Send("GET /infinite-and-slow HTTP/1.1\r\nHost: localhost\r\nConnection: keep-alive\r\n\r\n".ToUtf8());
                        Thread.Sleep(100);
                        sck.Close();
                        GC.Collect();
                    }
                };
                var threads = Enumerable.Range(0, 10).Select(_ => new Thread(thread)).ToList();
                foreach (var t in threads)
                    t.Start();
                foreach (var t in threads)
                    t.Join();

                instance.StopListening(true); // server must not throw after this; that’s the point of the test
            }
            finally
            {
                instance.StopListening(brutal: true);
            }
        }

        [Test]
        public void TestHalfOpenConnection()
        {
            var instance = new HttpServer(new HttpServerOptions { Port = ProgramServersTests.Port, OutputExceptionInformation = true });
            instance.Handler = req => HttpResponse.PlainText(" thingy stuff ");
            try
            {
                instance.StartListening();

                // A proper request ending in a half closed connection
                using (var cl = new TcpClient())
                {
                    cl.ReceiveTimeout = 1000; // 1 sec
                    cl.Connect("localhost", ProgramServersTests.Port);
                    cl.Client.Send("GET /static HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n".ToUtf8());
                    cl.Client.Shutdown(SocketShutdown.Send);
                    var response = Encoding.UTF8.GetString(new SocketReaderStream(cl.Client, long.MaxValue).ReadAllBytes());
                    var code = (HttpStatusCode) int.Parse(response.Substring("HTTP/1.1 ".Length, 3));
                    var parts = response.Split("\r\n\r\n");
                    Assert.AreEqual(HttpStatusCode._200_OK, code);
                    Assert.AreEqual(" thingy stuff ", parts[1]);
                }

                // An incomplete request ending in a half closed connection
                using (var cl = new TcpClient())
                {
                    cl.Connect("localhost", ProgramServersTests.Port);
                    cl.Client.Send("GET /static HTTP/1.1\r\nHost: localhost\r\nConnection: close".ToUtf8());
                    cl.Client.Shutdown(SocketShutdown.Send);
                    var response = Encoding.UTF8.GetString(new SocketReaderStream(cl.Client, long.MaxValue).ReadAllBytes());
                    // the test is that it doesn't wait forever
                }

                // A malformed request ending in a half closed connection
                using (var cl = new TcpClient())
                {
                    cl.Connect("localhost", ProgramServersTests.Port);
                    cl.Client.Send("xz".ToUtf8());
                    cl.Client.Shutdown(SocketShutdown.Send);
                    var response = Encoding.UTF8.GetString(new SocketReaderStream(cl.Client, long.MaxValue).ReadAllBytes());
                    // the test is that it doesn't wait forever
                }
            }
            finally
            {
                instance.StopListening(brutal: true);
            }
        }
    }
}
