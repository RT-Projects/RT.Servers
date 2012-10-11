using System;
using System.Net.Sockets;
using System.Text;
using NUnit.Framework;
using RT.Util;
using RT.Util.ExtensionMethods;
using RT.Util.Streams;

namespace RT.Servers.Tests
{
    [TestFixture]
    public sealed class UrlPathResolverTests
    {
        [Test]
        public void TestNestedUrlPathResolving()
        {
            var url = new HttpUrl();
            url.SetHost("www.example.com");
            url.SetRestUrl("/docgen/member/Color/ToString?thingy=stuff");
            url.AssertComplete();

            Assert.AreEqual("", url.BaseDomain);
            Assert.AreEqual("www.example.com", url.Subdomain);

            Assert.AreEqual("/docgen/member/Color/ToString", url.Path);
            Assert.IsTrue(url.ParentPaths.SequenceEqual(new string[0]));

            bool okProp = false, okDocGen = false;

            var resolverDocGen = new UrlPathResolver(
                new UrlPathHook(path: "/member", handler: req =>
                {
                    Assert.AreEqual("/Color/ToString", req.Url.Path);
                    Assert.AreEqual("/docgen/member/blah/xyz?thingy=stuff", req.Url.WithPath("/blah/xyz").ToHref());

                    var url2 = req.Url.WithPathOnly("/blah/xyz");
                    Assert.AreEqual("/docgen/member/blah/xyz", url2.ToHref());
                    Assert.AreEqual("/blah/xyz", url2.Path);
                    Assert.AreEqual("http://www.example.com/docgen/member/blah/xyz", url2.ToFull());

                    url2 = req.Url.WithPath("/blah/xyz");
                    Assert.AreEqual("/docgen/member/blah/xyz?thingy=stuff", url2.ToHref());
                    Assert.AreEqual("/blah/xyz", url2.Path);

                    url2 = req.Url.WithPathParent().WithPath("/blah/xyz");
                    Assert.AreEqual("/docgen/blah/xyz?thingy=stuff", url2.ToHref());
                    Assert.AreEqual("/blah/xyz", url2.Path);

                    url2 = req.Url.WithPathParent().WithPathParent().WithPath("/blah/xyz");
                    Assert.AreEqual("/blah/xyz?thingy=stuff", url2.ToHref());
                    Assert.AreEqual("/blah/xyz", url2.Path);

                    okDocGen = true;
                    return HttpResponse.Empty();
                })
            );

            var resolverPropeller = new UrlPathResolver(
                new UrlPathHook(path: "/docgen", handler: req =>
                {
                    Assert.AreEqual("/member/Color/ToString", req.Url.Path);
                    Assert.AreEqual("/docgen/blah/xyz?thingy=stuff", req.Url.WithPath("/blah/xyz").ToHref());

                    var url2 = req.Url.WithPathOnly("/blah/xyz");
                    Assert.AreEqual("/docgen/blah/xyz", url2.ToHref());
                    Assert.AreEqual("/blah/xyz", url2.Path);
                    Assert.AreEqual("http://www.example.com/docgen/blah/xyz", url2.ToFull());

                    url2 = req.Url.WithPath("/blah/xyz");
                    Assert.AreEqual("/docgen/blah/xyz?thingy=stuff", url2.ToHref());
                    Assert.AreEqual("/blah/xyz", url2.Path);

                    url2 = req.Url.WithPathParent().WithPath("/blah/xyz");
                    Assert.AreEqual("/blah/xyz?thingy=stuff", url2.ToHref());
                    Assert.AreEqual("/blah/xyz", url2.Path);

                    okProp = true;
                    return resolverDocGen.Handle(req);
                })
            );

            resolverPropeller.Handle(new HttpRequest() { Url = url });

            Assert.IsTrue(okProp);
            Assert.IsTrue(okDocGen);
        }

        [Test]
        public void TestUrlPathResolverDomainCase()
        {
            var instance = new HttpServer(new HttpServerOptions { Port = ProgramServersTests.Port, OutputExceptionInformation = true });
            try
            {
                bool ok;
                instance.Handler = new UrlPathResolver(
                    new UrlPathHook(domain: "example.com", handler: req => { ok = true; return HttpResponse.Empty(); })
                ).Handle;

                var getResponse = Ut.Lambda((string host) =>
                {
                    TcpClient cl = new TcpClient();
                    cl.Connect("localhost", ProgramServersTests.Port);
                    cl.ReceiveTimeout = 1000; // 1 sec
                    cl.Client.Send(("GET /static HTTP/1.1\r\nHost: " + host + "\r\nConnection: close\r\n\r\n").ToUtf8());
                    var response = Encoding.UTF8.GetString(new SocketReaderStream(cl.Client, long.MaxValue).ReadAllBytes());
                    cl.Close();
                    var code = (HttpStatusCode) int.Parse(response.Substring("HTTP/1.1 ".Length, 3));
                    var parts = response.Split("\r\n\r\n");
                    return Tuple.Create(code, parts[1]);
                });

                instance.StartListening();

                ok = false;
                getResponse("blah.com");
                Assert.IsFalse(ok);

                ok = false;
                getResponse("www.example.com");
                Assert.IsTrue(ok);

                ok = false;
                getResponse("WWW.EXAMPLE.COM");
                Assert.IsTrue(ok);

                ok = false;
                getResponse("www.exAmple.com");
                Assert.IsTrue(ok);
            }
            finally
            {
                instance.StopListening(brutal: true);
            }
        }
    }
}
