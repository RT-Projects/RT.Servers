using System;
using System.Net.Sockets;
using System.Text;
using NUnit.Framework;
using RT.Util;
using RT.Util.ExtensionMethods;

namespace RT.Servers.Tests
{
    [TestFixture]
    public sealed class UrlPathResolverTests
    {
        [Test]
        public void TestNestedResolve()
        {
            var url = new HttpUrl();
            url.SetHost("www.example.com");
            url.SetLocation("/docgen/member/Color/ToString?thingy=stuff");
            url.AssertComplete();

            Assert.AreEqual(0, url.ParentDomains.Length);
            Assert.AreEqual("www.example.com", url.Domain);

            Assert.AreEqual("/docgen/member/Color/ToString", url.Path);
            Assert.IsTrue(url.ParentPaths.SequenceEqual(new string[0]));

            bool okProp = false, okDocGen = false;

            var resolverDocGen = new UrlResolver(
                new UrlMapping(path: "/member", handler: req =>
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

            var resolverPropeller = new UrlResolver(
                new UrlMapping(path: "/docgen", handler: req =>
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
        public void TestDomainCase()
        {
            var instance = new HttpServer(ProgramServersTests.Port, new HttpServerOptions { OutputExceptionInformation = true });
            try
            {
                bool ok;
                instance.Handler = new UrlResolver(
                    new UrlMapping(domain: "example.com", handler: req => { ok = true; return HttpResponse.Empty(); })
                ).Handle;

                var getResponse = Ut.Lambda((string host) =>
                {
                    TcpClient cl = new TcpClient();
                    cl.Connect("localhost", ProgramServersTests.Port);
                    cl.ReceiveTimeout = 1000; // 1 sec
                    cl.Client.Send(("GET /static HTTP/1.1\r\nHost: " + host + "\r\nConnection: close\r\n\r\n").ToUtf8());
                    var response = Encoding.UTF8.GetString(cl.Client.ReceiveAllBytes());
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

        [Test]
        public void TestSkippableHandlers()
        {
            var url = new HttpUrl();
            url.SetHost("www.example.com");
            url.SetLocation("/docgen/member/Color/ToString?thingy=stuff");
            url.AssertComplete();

            Assert.AreEqual(0, url.ParentDomains.Length);
            Assert.AreEqual("www.example.com", url.Domain);

            var resolver = new UrlResolver(
                new UrlMapping(req =>
                {
                    Assert.AreEqual(1, req.Url.ParentDomains.Length);
                    Assert.AreEqual("www.example.com", req.Url.ParentDomains[0]);
                    Assert.AreEqual("", req.Url.Domain);
                    return null;
                }, "www.example.com", skippable: true),
                new UrlMapping(req =>
                {
                    Assert.AreEqual(1, req.Url.ParentDomains.Length);
                    Assert.AreEqual("example.com", req.Url.ParentDomains[0]);
                    Assert.AreEqual("www.", req.Url.Domain);
                    return null;
                }, "example.com", skippable: true),
                new UrlMapping(req =>
                {
                    Assert.AreEqual(0, req.Url.ParentDomains.Length);
                    Assert.AreEqual("www.example.com", req.Url.Domain);
                    return null;
                }, skippable: true),
                new UrlMapping(req =>
                {
                    Assert.AreEqual(0, req.Url.ParentDomains.Length);
                    Assert.AreEqual("www.example.com", req.Url.Domain);
                    return HttpResponse.PlainText("blah");
                }
            ));
            resolver.Handle(new HttpRequest { Url = url });
        }
    }
}
