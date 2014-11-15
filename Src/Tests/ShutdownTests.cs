using System;
using System.Net.Sockets;
using System.Threading;
using NUnit.Framework;
using RT.Util.ExtensionMethods;

namespace RT.Servers.Tests
{
    [TestFixture]
    public sealed class ShutdownTests
    {
        [Test]
        public void TestAbortedEndReceive()
        {
            var instance = new HttpServer(ProgramServersTests.Port)
            {
                Handler = new UrlResolver(
                    new UrlMapping(req => HttpResponse.PlainText("bunch of text"), path: "/static")
                ).Handle
            };
            try
            {
                instance.StartListening();

                TcpClient cl = new TcpClient();
                cl.Connect("localhost", ProgramServersTests.Port);
                cl.ReceiveTimeout = 1000; // 1 sec
                Socket sck = cl.Client;
                sck.Send("GET /static HTTP/1.1\r\nHost: localhost\r\n".ToUtf8());
                Thread.Sleep(500);
                instance.StopListening(true); // server must not throw after this; that’s the point of the test
                Assert.IsTrue(instance.ShutdownComplete.WaitOne(TimeSpan.FromSeconds(1))); // must shut down within at most 1 second
                sck.Close();
            }
            finally
            {
                instance.StopListening(brutal: true);
            }
        }

        [Test]
        public void TestKeepaliveShutdownGentle()
        {
            var instance = new HttpServer(ProgramServersTests.Port)
            {
                Handler = new UrlResolver(
                    new UrlMapping(req => HttpResponse.PlainText("bunch of text"), path: "/static")
                ).Handle
            };
            try
            {
                instance.StartListening();

                TcpClient cl = new TcpClient();
                cl.Connect("localhost", ProgramServersTests.Port);
                cl.ReceiveTimeout = 1000; // 1 sec
                Socket sck = cl.Client;
                sck.Send("GET /static HTTP/1.1\r\nHost: localhost\r\n".ToUtf8());
                Thread.Sleep(500);
                Assert.AreEqual(1, instance.Stats.ActiveHandlers);
                sck.Send("Connection: keep-alive\r\n\r\n".ToUtf8());

                TestUtil.ReadResponseUntilContent(sck);
                Thread.Sleep(500);
                Assert.AreEqual(0, instance.Stats.ActiveHandlers);
                Assert.AreEqual(1, instance.Stats.KeepAliveHandlers);
                instance.StopListening();
                Assert.IsTrue(instance.ShutdownComplete.WaitOne(TimeSpan.FromSeconds(1))); // must shut down within at most 1 second
                Assert.AreEqual(0, instance.Stats.ActiveHandlers);
                Assert.AreEqual(0, instance.Stats.KeepAliveHandlers);
                sck.Close();
            }
            finally
            {
                instance.StopListening(brutal: true);
            }
        }

        [Test]
        public void TestActiveShutdownGentle()
        {
            var instance = new HttpServer(ProgramServersTests.Port)
            {
                Handler = new UrlResolver(
                    new UrlMapping(req => HttpResponse.PlainText("bunch of text"), path: "/static")
                ).Handle
            };
            try
            {
                instance.StartListening();

                TcpClient cl = new TcpClient();
                cl.Connect("localhost", ProgramServersTests.Port);
                cl.ReceiveTimeout = 10000; // 10 sec
                Socket sck = cl.Client;
                sck.Send("GET /static HTTP/1.1\r\nHost: localhost\r\n".ToUtf8());
                Thread.Sleep(500);

                Assert.AreEqual(1, instance.Stats.ActiveHandlers);
                instance.StopListening();
                Assert.IsFalse(instance.ShutdownComplete.WaitOne(TimeSpan.FromSeconds(3))); // must still be running 3 seconds later
                Assert.IsFalse(instance.ShutdownComplete.WaitOne(TimeSpan.FromSeconds(0.1)));
                Assert.AreEqual(1, instance.Stats.ActiveHandlers);

                // Complete the request
                sck.Send("Connection: keep-alive\r\n\r\n".ToUtf8());
                TestUtil.ReadResponseUntilContent(sck);

                // Should be shut down now
                Assert.IsTrue(instance.ShutdownComplete.WaitOne(TimeSpan.FromSeconds(1))); // must shut down within at most 1 second
                Assert.AreEqual(0, instance.Stats.ActiveHandlers);
                Assert.AreEqual(0, instance.Stats.KeepAliveHandlers);
                sck.Close();
            }
            finally
            {
                instance.StopListening(brutal: true);
            }
        }

        [Test]
        public void TestActiveShutdownBrutal()
        {
            var instance = new HttpServer(ProgramServersTests.Port)
            {
                Handler = new UrlResolver(
                    new UrlMapping(req => HttpResponse.PlainText("bunch of text"), path: "/static")
                ).Handle
            };
            try
            {
                instance.StartListening();

                TcpClient cl = new TcpClient();
                cl.Connect("localhost", ProgramServersTests.Port);
                cl.ReceiveTimeout = 10000; // 10 sec
                Socket sck = cl.Client;
                sck.Send("GET /static HTTP/1.1\r\nHost: localhost\r\n".ToUtf8());
                Thread.Sleep(500);

                Assert.AreEqual(1, instance.Stats.ActiveHandlers);
                instance.StopListening(true);
                Assert.IsTrue(instance.ShutdownComplete.WaitOne(TimeSpan.FromSeconds(1))); // must shut down within at most 1 second
                Assert.AreEqual(0, instance.Stats.ActiveHandlers);
                Assert.AreEqual(0, instance.Stats.KeepAliveHandlers);
                sck.Close();
            }
            finally
            {
                instance.StopListening(brutal: true);
            }
        }
    }
}
