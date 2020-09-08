using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using NUnit.Framework;
using RT.Util;
using RT.Util.ExtensionMethods;
using RT.Util.Streams;

namespace RT.Servers.Tests
{
    [TestFixture]
    public sealed class ErrorHandlerTests
    {
        [Test]
        public void TestErrorHandlerExceptions()
        {
            var instance = new HttpServer(ProgramServersTests.Port, new HttpServerOptions { OutputExceptionInformation = true });
            try
            {
                instance.StartListening();

                var getResponse = Ut.Lambda(() =>
                {
                    TcpClient cl = new TcpClient();
                    cl.Connect("localhost", ProgramServersTests.Port);
                    cl.ReceiveTimeout = 1000; // 1 sec
                    cl.Client.Send("GET /static HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n".ToUtf8());
                    var response = Encoding.UTF8.GetString(cl.Client.ReceiveAllBytes());
                    cl.Close();
                    var code = (HttpStatusCode) int.Parse(response.Substring("HTTP/1.1 ".Length, 3));
                    var parts = response.Split("\r\n\r\n");
                    return Tuple.Create(code, parts[1]);
                });

                // Test that we get a 404 response with no handlers
                var resp = getResponse();
                Assert.AreEqual(HttpStatusCode._404_NotFound, resp.Item1);

                // Test that having only an error handler works as expected
                instance.ErrorHandler = (req, err) => { return HttpResponse.Create("blah", "text/plain", HttpStatusCode._407_ProxyAuthenticationRequired); };
                resp = getResponse();
                Assert.AreEqual(HttpStatusCode._407_ProxyAuthenticationRequired, resp.Item1);
                Assert.AreEqual("blah", resp.Item2);

                // Test that having no error handler uses the default one
                instance.ErrorHandler = null;
                instance.Handler = req => { throw new HttpException(HttpStatusCode._305_UseProxy); };
                resp = getResponse();
                Assert.AreEqual(HttpStatusCode._305_UseProxy, resp.Item1);

                // Test that the exception and request are passed on to the error handler
                HttpRequest storedReq = null;
                Exception storedEx = null;
                bool ok = false;
                instance.Handler = req => { storedEx = new HttpException(HttpStatusCode._402_PaymentRequired); storedReq = req; throw storedEx; };
                instance.ErrorHandler = (req, ex) => { ok = object.ReferenceEquals(req, storedReq) && object.ReferenceEquals(ex, storedEx); return HttpResponse.Create("blah", "text/plain"); };
                resp = getResponse();
                Assert.IsTrue(ok);

                // Test that exception in error handler invokes the default one, and uses the *original* status code
                instance.Handler = req => { throw new HttpException(HttpStatusCode._201_Created); };
                instance.ErrorHandler = (req, ex) => { throw new HttpException(HttpStatusCode._403_Forbidden); };
                resp = getResponse();
                Assert.AreEqual(HttpStatusCode._201_Created, resp.Item1);

                // Test that a non-HttpException is properly handled
                ok = false;
                instance.Handler = req => { throw storedEx = new Exception("Blah!"); };
                instance.ErrorHandler = (req, ex) => { ok = object.ReferenceEquals(ex, storedEx); return HttpResponse.Create("blah", "text/plain"); };
                resp = getResponse();
                Assert.IsTrue(ok);
                Assert.AreEqual(HttpStatusCode._200_OK, resp.Item1);
                Assert.AreEqual("blah", resp.Item2);
                instance.ErrorHandler = null;
                resp = getResponse();
                Assert.AreEqual(HttpStatusCode._500_InternalServerError, resp.Item1);

                // Test that the main handler returning null results in a 500 error (via InvalidOperationException)
                instance.Handler = req => { return null; };
                instance.ErrorHandler = (req, ex) => { storedEx = ex; throw new HttpException(HttpStatusCode._203_NonAuthoritativeInformation); };
                resp = getResponse();
                Assert.IsTrue(storedEx is InvalidOperationException);
                Assert.AreEqual(HttpStatusCode._500_InternalServerError, resp.Item1);

                // Test that the error handler returning null invokes the default error handler
                instance.Handler = req => { throw new HttpException(HttpStatusCode._201_Created); };
                instance.ErrorHandler = (req, ex) => { return null; };
                resp = getResponse();
                Assert.AreEqual(HttpStatusCode._201_Created, resp.Item1);

                // Test that a malformed request passes through the error handler
                instance.ErrorHandler = (req, ex) => { storedEx = ex; throw new HttpException(HttpStatusCode._204_NoContent); };
                {
                    TcpClient cl = new TcpClient();
                    cl.Connect("localhost", ProgramServersTests.Port);
                    cl.ReceiveTimeout = 1000; // 1 sec
                    cl.Client.Send("xz\r\n\r\n".ToUtf8());
                    var response = Encoding.UTF8.GetString(cl.Client.ReceiveAllBytes());
                    cl.Close();
                    var code = (HttpStatusCode) int.Parse(response.Substring("HTTP/1.1 ".Length, 3));
                    var body = response.Split("\r\n\r\n")[1];
                    Assert.AreEqual(HttpStatusCode._400_BadRequest, code);
                    Assert.IsTrue(storedEx is HttpRequestParseException);
                    Assert.AreEqual(HttpStatusCode._400_BadRequest, (storedEx as HttpRequestParseException).StatusCode);
                }

                // Test that an exception in the response stream is sent to the response exception handler
                var excp = new Exception("Blam!");
                HttpResponse storedRsp1 = null, storedRsp2 = null;
                instance.Handler = req => { return storedRsp1 = HttpResponse.Create(new DynamicContentStream(enumerableWithException(excp)), "text/plain"); };
                bool ok1 = true, ok2 = false;
                instance.ErrorHandler = (req, ex) => { ok1 = false; return null; };
                instance.ResponseExceptionHandler = (req, ex, rsp) => { ok2 = true; storedEx = ex; storedRsp2 = rsp; };
                resp = getResponse();
                Assert.IsTrue(ok1 && ok2);
                Assert.IsTrue(object.ReferenceEquals(excp, storedEx));
                Assert.IsTrue(object.ReferenceEquals(storedRsp1, storedRsp2));

                // Test that an exception in the response stream didn't bring down the server
                ok = false;
                instance.Handler = req => { ok = true; throw new HttpException(HttpStatusCode._201_Created); };
                resp = getResponse();
                Assert.IsTrue(ok);
                Assert.AreEqual(HttpStatusCode._201_Created, resp.Item1);
            }
            finally
            {
                instance.StopListening(brutal: true);
            }
        }

        private IEnumerable<string> enumerableWithException(Exception exception)
        {
            yield return "blah";
            throw exception;
        }

        [Test]
        public void TestErrorHandlerAndCleanUp()
        {
            var instance = new HttpServer(ProgramServersTests.Port);
            try
            {
                bool errorHandlerCalled = false;
                bool cleanUpCalled = false;

                instance.StartListening();
                instance.Handler = req =>
                {
                    req.CleanUpCallback = () =>
                    {
                        Assert.IsTrue(errorHandlerCalled);
                        cleanUpCalled = true;
                    };
                    throw new InvalidOperationException();
                };
                instance.ErrorHandler = (req, exc) =>
                {
                    Assert.IsFalse(cleanUpCalled);
                    errorHandlerCalled = true;
                    return HttpResponse.PlainText("Error");
                };

                TcpClient cl = new TcpClient();
                cl.Connect("localhost", ProgramServersTests.Port);
                cl.ReceiveTimeout = 1000000; // 1000 sec
                cl.Client.Send("GET / HTTP/1.1\r\nHost: localhost\r\n\r\n".ToUtf8());
                var response = Encoding.UTF8.GetString(cl.Client.ReceiveAllBytes());
                cl.Close();
                Assert.IsTrue(errorHandlerCalled);
                Assert.IsTrue(cleanUpCalled);
                Assert.IsTrue(response.EndsWith("\r\n\r\nError"));
            }
            finally
            {
                instance.StopListening(brutal: true);
            }
        }
    }
}
