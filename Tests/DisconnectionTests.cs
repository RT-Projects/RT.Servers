using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RT.Util.ExtensionMethods;

namespace RT.Servers.Tests;

[TestClass]
public sealed class DisconnectionTests
{
    private static IEnumerable<string> enumInfinite()
    {
        while (true)
        {
            yield return "blah!";
            Thread.Sleep(0);
        }
    }

    [TestMethod]
    public void TestMidResponseSocketClosure()
    {
        var instance = new HttpServer(TestHelpers.Port + 1)
        {
            Handler = new UrlResolver(
                new UrlMapping(req => { return HttpResponse.Create(enumInfinite(), "text/plain"); }, path: "/infinite-and-slow")
            ).Handle
        };
        try
        {
            instance.StartListening();

            static void thread()
            {
                for (var i = 0; i < 20; i++)
                {
                    var cl = new TcpClient();
                    cl.Connect("localhost", TestHelpers.Port + 1);
                    cl.ReceiveTimeout = 1000; // 1 sec
                    Socket sck = cl.Client;
                    sck.Send("GET /infinite-and-slow HTTP/1.1\r\nHost: localhost\r\nConnection: keep-alive\r\n\r\n".ToUtf8());
                    Thread.Sleep(100);
                    sck.Close();
                    GC.Collect();
                }
            }
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

    [TestMethod]
    public void TestHalfOpenConnection()
    {
        var instance = new HttpServer(TestHelpers.Port + 2, new HttpServerOptions { OutputExceptionInformation = true }) { Handler = req => HttpResponse.PlainText(" thingy stuff ") };
        try
        {
            instance.StartListening();

            // A proper request ending in a half closed connection
            using (var cl = new TcpClient())
            {
                cl.ReceiveTimeout = 1000; // 1 sec
                cl.Connect("localhost", TestHelpers.Port + 2);
                cl.Client.Send("GET /static HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n".ToUtf8());
                cl.Client.Shutdown(SocketShutdown.Send);
                var response = Encoding.UTF8.GetString(cl.Client.ReceiveAllBytes());
                var code = (HttpStatusCode) int.Parse(response.Substring("HTTP/1.1 ".Length, 3));
                var parts = response.Split("\r\n\r\n");
                Assert.AreEqual(HttpStatusCode._200_OK, code);
                Assert.AreEqual(" thingy stuff ", parts[1]);
            }

            // An incomplete request ending in a half closed connection
            using (var cl = new TcpClient())
            {
                cl.Connect("localhost", TestHelpers.Port + 2);
                cl.Client.Send("GET /static HTTP/1.1\r\nHost: localhost\r\nConnection: close".ToUtf8());
                cl.Client.Shutdown(SocketShutdown.Send);
                var response = Encoding.UTF8.GetString(cl.Client.ReceiveAllBytes());
                // the test is that it doesn't wait forever
            }

            // A malformed request ending in a half closed connection
            using (var cl = new TcpClient())
            {
                cl.Connect("localhost", TestHelpers.Port + 2);
                cl.Client.Send("xz".ToUtf8());
                cl.Client.Shutdown(SocketShutdown.Send);
                var response = Encoding.UTF8.GetString(cl.Client.ReceiveAllBytes());
                // the test is that it doesn't wait forever
            }
        }
        finally
        {
            instance.StopListening(brutal: true);
        }
    }

    private static void assertClosedByServer(Socket sck)
    {
        // If the server hasn’t closed the connection, this throws a SocketException with a different error code because of the ReceiveTimeout
        try { sck.ReceiveAllBytes(); }
        catch (SocketException e) when (e.SocketErrorCode == SocketError.ConnectionReset) { }
    }

    private static void testIdleTimeout(int port, bool sleep, string text, string path, int receiveTimeout, Action<HttpServer, TcpClient> test)
    {
        // The server must close the connection by itself once the idle timeout has elapsed.
        // This waits for things to happen instead of sleeping for a fixed time because the
        // other tests running in parallel can delay the server.
        var instance = new HttpServer(port, new HttpServerOptions { IdleTimeout = 1000 })
        {
            Handler = new UrlResolver(new UrlMapping(req =>
            {
                if (sleep)
                    Thread.Sleep(3000);
                return HttpResponse.PlainText(text);
            }, path: path)).Handle
        };
        try
        {
            instance.StartListening();
            using var cl = new TcpClient();
            cl.ReceiveTimeout = receiveTimeout;
            cl.Connect("localhost", port);
            test(instance, cl);
        }
        finally
        {
            instance.StopListening(brutal: true);
        }
    }

    [TestMethod]
    public void TestIdleTimeoutNeverSendsAnything()
    {
        // A connection on which the client never sends anything
        testIdleTimeout(TestHelpers.Port + 38, false, "bunch of text", "/static", 1000, (instance, cl) =>
        {
            Assert.IsTrue(TestHelpers.WaitUntil(() => instance.Stats.ActiveHandlers == 1, TimeSpan.FromSeconds(10)));
            Assert.IsTrue(TestHelpers.WaitUntil(() => instance.Stats.ActiveHandlers == 0, TimeSpan.FromSeconds(10)));
            assertClosedByServer(cl.Client);
        });
    }

    [TestMethod]
    public void TestIdleTimeoutIncompleteRequest()
    {
        // A connection on which the client sends an incomplete request and then goes quiet
        testIdleTimeout(TestHelpers.Port + 39, false, "bunch of text", "/static", 1000, (instance, cl) =>
        {
            cl.Client.Send("GET /static HTTP/1.1\r\nHost: localhost\r\n".ToUtf8());
            Assert.IsTrue(TestHelpers.WaitUntil(() => instance.Stats.ActiveHandlers == 1, TimeSpan.FromSeconds(10)));
            Assert.IsTrue(TestHelpers.WaitUntil(() => instance.Stats.ActiveHandlers == 0, TimeSpan.FromSeconds(10)));
            assertClosedByServer(cl.Client);
        });
    }

    [TestMethod]
    public void TestIdleTimeoutAfterKeepAlive()
    {
        testIdleTimeout(TestHelpers.Port + 40, false, "bunch of text", "/static", 1000, (instance, cl) =>
        {
            cl.Client.Send("GET /static HTTP/1.1\r\nHost: localhost\r\nConnection: keep-alive\r\n\r\n".ToUtf8());
            TestHelpers.ReadResponseUntilContent(cl.Client);
            Assert.IsTrue(TestHelpers.WaitUntil(() => instance.Stats.KeepAliveHandlers == 1, TimeSpan.FromSeconds(10)));
            Assert.IsTrue(TestHelpers.WaitUntil(() => instance.Stats.KeepAliveHandlers == 0, TimeSpan.FromSeconds(10)));
            Assert.AreEqual(0, instance.Stats.ActiveHandlers);
            assertClosedByServer(cl.Client);
        });
    }

    [TestMethod]
    public void TestIdleTimeoutTestSlowHandler()
    {
        testIdleTimeout(TestHelpers.Port + 41, true, "slow text", "/slow", 10000, (instance, cl) =>
        {
            // The idle timeout must not cut off a connection whose request handler is merely taking a long time
            cl.Client.Send("GET /slow HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n".ToUtf8());
            var response = Encoding.UTF8.GetString(cl.Client.ReceiveAllBytes());
            var code = (HttpStatusCode) int.Parse(response.Substring("HTTP/1.1 ".Length, 3));
            var parts = response.Split("\r\n\r\n");
            Assert.AreEqual(HttpStatusCode._200_OK, code);
            Assert.HasCount(2, parts);
            Assert.AreEqual("slow text", parts[1]);
        });
    }

    [TestMethod]
    public void TestIdleTimeoutTestSlowHandlerAndClient()
    {
        // Same as TestIdleTimeoutTestSlowHandler(), but the request arrives a while after the connection is opened, so the server is already waiting for it
        testIdleTimeout(TestHelpers.Port + 42, true, "slow text", "/slow", 10000, (instance, cl) =>
        {
            Thread.Sleep(500);
            cl.Client.Send("GET /slow HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n".ToUtf8());
            var response = Encoding.UTF8.GetString(cl.Client.ReceiveAllBytes());
            var code = (HttpStatusCode) int.Parse(response.Substring("HTTP/1.1 ".Length, 3));
            var parts = response.Split("\r\n\r\n");
            Assert.AreEqual(HttpStatusCode._200_OK, code);
            Assert.HasCount(2, parts);
            Assert.AreEqual("slow text", parts[1]);
        });
    }
}
