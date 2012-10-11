using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using NUnit.Framework;
using RT.Util;
using RT.Util.ExtensionMethods;

namespace RT.Servers.Tests
{
    [TestFixture]
    public sealed class SpecialHeadersTests
    {
        [Test]
        public void TestClientIPAddress()
        {
            HttpRequest request = null;
            var instance = new HttpServer(new HttpServerOptions { Port = ProgramServersTests.Port })
            {
                Handler = new UrlPathResolver(
                    new UrlPathHook(req => { request = req; return HttpResponse.PlainText("blah"); }, path: "/static")
                ).Handle
            };
            try
            {
                instance.StartListening();

                TcpClient cl = new TcpClient();
                cl.Connect("localhost", ProgramServersTests.Port);
                cl.ReceiveTimeout = 1000; // 1 sec
                Socket sck = cl.Client;

                sck.Send("GET /static HTTP/1.1\r\nHost: localhost\r\nConnection: keep-alive\r\n\r\n".ToUtf8());
                TestUtil.ReadResponseUntilContent(sck);
                Assert.IsTrue(IPAddress.IsLoopback(request.ClientIPAddress));
                Assert.IsTrue(IPAddress.IsLoopback(request.SourceIP.Address));
                Assert.IsNull(request.Headers["X-Forwarded-For"]);

                sck.Send("GET /static HTTP/1.1\r\nHost: localhost\r\nConnection: keep-alive\r\nX-Forwarded-For: 12.34.56.78\r\n\r\n".ToUtf8());
                TestUtil.ReadResponseUntilContent(sck);
                Assert.AreEqual(IPAddress.Parse("12.34.56.78"), request.ClientIPAddress);
                Assert.IsTrue(IPAddress.IsLoopback(request.SourceIP.Address));
                Assert.IsNotNull(request.Headers["X-Forwarded-For"]);

                sck.Send("GET /static HTTP/1.1\r\nHost: localhost\r\nConnection: keep-alive\r\nX-Forwarded-For: 2a00:1450:400c:c01::93, 12.34.56.78\r\n\r\n".ToUtf8());
                TestUtil.ReadResponseUntilContent(sck);
                Assert.AreEqual(IPAddress.Parse("2a00:1450:400c:c01::93"), request.ClientIPAddress);
                Assert.AreEqual(IPAddress.Parse("2a00:1450:400c:c01::93"), request.Headers.XForwardedFor[0]);
                Assert.AreEqual(IPAddress.Parse("12.34.56.78"), request.Headers.XForwardedFor[1]);
                Assert.IsTrue(IPAddress.IsLoopback(request.SourceIP.Address));

                sck.Close();
                instance.StopListening(true); // server must not throw after this; that’s the point of the test
            }
            finally
            {
                instance.StopListening(brutal: true);
            }
        }
    }
}
