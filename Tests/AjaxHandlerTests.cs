using System.Linq;
using System.Net.Sockets;
using System.Text;
using NUnit.Framework;
using RT.Json;
using RT.Util.ExtensionMethods;

namespace RT.Servers.Tests
{
    [TestFixture]
    public sealed class AjaxHandlerTests
    {
        sealed class TempObject { public int Value; }

        sealed class Ajax
        {
            [AjaxMethod]
            public string HelloWorld() => "Hello, world!";

            [AjaxMethod]
            public string AddOne(int i) => $"{i + 1}";

            [AjaxMethod]
            public string ReturnUrl([AjaxRequest] HttpRequest req) => req.Url.ToFull();

            [AjaxMethod]
            public int AddOneToTempObject(TempObject tempObj) => tempObj.Value + 1;

            [AjaxConverter]
            public TempObject ConvertTempObject(int tempObj) => new TempObject { Value = tempObj };
        }

        [Test]
        public void TestErrorHandlerExceptions()
        {
            var instance = new HttpServer(ProgramServersTests.Port, new HttpServerOptions { OutputExceptionInformation = true });
            try
            {
                var ajaxHandler = new AjaxHandler<Ajax>();
                var api = new Ajax();
                instance.Handler = req => ajaxHandler.Handle(req, api);
                instance.StartListening();

                (HttpStatusCode status, string response) getResponse(string method, string payload)
                {
                    TcpClient cl = new TcpClient();
                    cl.Connect("localhost", ProgramServersTests.Port);
                    cl.ReceiveTimeout = 1000; // 1 sec
                    var payloadFull = "data=" + payload.ToUtf8().Select(b => "%" + b.ToString("X2")).JoinString();
                    cl.Client.Send($"POST /{method} HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\nContent-Type: application/x-www-form-urlencoded\r\nContent-Length: {payloadFull.Utf8Length()}\r\n\r\n{payloadFull}".ToUtf8());
                    var response = Encoding.UTF8.GetString(cl.Client.ReceiveAllBytes());
                    cl.Close();
                    var code = (HttpStatusCode) int.Parse(response.Substring("HTTP/1.1 ".Length, 3));
                    var parts = response.Split("\r\n\r\n");
                    return (code, parts[1]);
                }

                // Test that we get a 404 response for an invalid method
                var resp = getResponse("InvalidMethod", new JsonDict { }.ToString());
                Assert.AreEqual(HttpStatusCode._404_NotFound, resp.status);

                // Test that we get a 200 response for a valid method
                resp = getResponse("HelloWorld", new JsonDict { }.ToString());
                Assert.AreEqual(HttpStatusCode._200_OK, resp.status);
                Assert.IsTrue(JsonDict.TryParse(resp.response, out var json));
                Assert.AreEqual("Hello, world!", json["result"].GetString());
                Assert.AreEqual("ok", json["status"].GetString());

                // Test that we get a 400 response for an invalid request (missing parameter)
                resp = getResponse("AddOne", new JsonDict { }.ToString());
                Assert.AreEqual(HttpStatusCode._400_BadRequest, resp.status);
                Assert.IsTrue(JsonDict.TryParse(resp.response, out json));
                Assert.AreEqual("error", json["status"].GetString());

                // Test that we get the correct response for the AddOne method
                resp = getResponse("AddOne", new JsonDict { ["i"] = 47 }.ToString());
                Assert.AreEqual(HttpStatusCode._200_OK, resp.status);
                Assert.IsTrue(JsonDict.TryParse(resp.response, out json));
                Assert.AreEqual("48", json["result"].GetString());
                Assert.AreEqual("ok", json["status"].GetString());

                // Test that we get the correct response for the ReturnUrl method
                resp = getResponse("ReturnUrl", new JsonDict { }.ToString());
                Assert.AreEqual(HttpStatusCode._200_OK, resp.status);
                Assert.IsTrue(JsonDict.TryParse(resp.response, out json));
                Assert.AreEqual("http://localhost/ReturnUrl", json["result"].GetString());
                Assert.AreEqual("ok", json["status"].GetString());

                // Test AjaxConverter
                resp = getResponse("AddOneToTempObject", new JsonDict { ["tempObj"] = 47 }.ToString());
                Assert.AreEqual(HttpStatusCode._200_OK, resp.status);
                Assert.IsTrue(JsonDict.TryParse(resp.response, out json));
                Assert.AreEqual(48, json["result"].GetInt());
                Assert.AreEqual("ok", json["status"].GetString());
            }
            finally
            {
                instance.StopListening(brutal: true);
            }
        }
    }
}
