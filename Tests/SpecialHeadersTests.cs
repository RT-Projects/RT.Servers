using System.Net;
using System.Net.Sockets;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RT.Util.ExtensionMethods;

namespace RT.Servers.Tests;

[TestClass]
public sealed class SpecialHeadersTests
{
    [TestMethod]
    public void TestClientIPAddress()
    {
        HttpRequest request = null;
        var instance = new HttpServer(TestHelpers.Port + 12)
        {
            Handler = new UrlResolver(
                new UrlMapping(req => { request = req; return HttpResponse.PlainText("blah"); }, path: "/static")
            ).Handle
        };
        try
        {
            instance.StartListening();

            var cl = new TcpClient();
            cl.Connect("localhost", TestHelpers.Port + 12);
            cl.ReceiveTimeout = 1000; // 1 sec
            Socket sck = cl.Client;

            sck.Send("GET /static HTTP/1.1\r\nHost: localhost\r\nConnection: keep-alive\r\n\r\n".ToUtf8());
            TestHelpers.ReadResponseUntilContent(sck);
            Assert.IsTrue(IPAddress.IsLoopback(request.ClientIPAddress));
            Assert.IsTrue(IPAddress.IsLoopback(request.SourceIP.Address));
            Assert.IsNull(request.Headers["X-Forwarded-For"]);

            sck.Send("GET /static HTTP/1.1\r\nHost: localhost\r\nConnection: keep-alive\r\nX-Forwarded-For: 12.34.56.78\r\n\r\n".ToUtf8());
            TestHelpers.ReadResponseUntilContent(sck);
            Assert.AreEqual(IPAddress.Parse("12.34.56.78"), request.ClientIPAddress);
            Assert.IsTrue(IPAddress.IsLoopback(request.SourceIP.Address));
            Assert.IsNotNull(request.Headers["X-Forwarded-For"]);

            sck.Send("GET /static HTTP/1.1\r\nHost: localhost\r\nConnection: keep-alive\r\nX-Forwarded-For: 12.34.56.78:63125\r\n\r\n".ToUtf8());
            TestHelpers.ReadResponseUntilContent(sck);
            Assert.AreEqual(IPAddress.Parse("12.34.56.78"), request.ClientIPAddress);
            Assert.IsTrue(IPAddress.IsLoopback(request.SourceIP.Address));
            Assert.IsNotNull(request.Headers["X-Forwarded-For"]);

            sck.Send("GET /static HTTP/1.1\r\nHost: localhost\r\nConnection: keep-alive\r\nX-Forwarded-For: 2a00:1450:400c:c01::93, 12.34.56.78\r\n\r\n".ToUtf8());
            TestHelpers.ReadResponseUntilContent(sck);
            Assert.AreEqual(IPAddress.Parse("2a00:1450:400c:c01::93"), request.ClientIPAddress);
            Assert.AreEqual(IPAddress.Parse("2a00:1450:400c:c01::93"), request.Headers.XForwardedFor[0]);
            Assert.AreEqual(IPAddress.Parse("12.34.56.78"), request.Headers.XForwardedFor[1]);
            Assert.IsTrue(IPAddress.IsLoopback(request.SourceIP.Address));

            sck.Send("GET /static HTTP/1.1\r\nHost: localhost\r\nConnection: keep-alive\r\nX-Forwarded-For: [2a00:1450:400c:c01::93]:63125, 12.34.56.78:63125\r\n\r\n".ToUtf8());
            TestHelpers.ReadResponseUntilContent(sck);
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
