using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
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
    public sealed class RequestResponseTests
    {
        private void testRequest(string testName, int storeFileUploadInFileAtSize, string request, Action<string[], byte[]> verify)
        {
            var requestBytes = request.ToUtf8();
            for (int chunkSize = 0; chunkSize <= requestBytes.Length; chunkSize += Rnd.Next(2, 64).ClipMax(requestBytes.Length - chunkSize).ClipMin(2))
            {
                if (chunkSize == 0)
                    continue;
                Console.WriteLine("{0}; SFUIFAS {3}; length {2}; chunk size {1}", testName, chunkSize, requestBytes.Length, storeFileUploadInFileAtSize);
                TcpClient cl = new TcpClient();
                cl.Connect("localhost", ProgramServersTests.Port);
                cl.ReceiveTimeout = 1000; // 1 sec
                Socket sck = cl.Client;
                for (int j = 0; j < requestBytes.Length; j += chunkSize)
                {
                    sck.Send(requestBytes, j, Math.Min(requestBytes.Length - j, chunkSize), SocketFlags.None);
                    Thread.Sleep(25);
                }
                MemoryStream response = new MemoryStream();
                byte[] b = new byte[65536];
                int bytesRead = sck.Receive(b);
                Assert.IsTrue(bytesRead > 0);
                while (bytesRead > 0)
                {
                    response.Write(b, 0, bytesRead);
                    bytesRead = sck.Receive(b);
                }
                var content = response.ToArray();
                int pos = content.IndexOfSubarray(new byte[] { 13, 10, 13, 10 }, 0, content.Length);
                Assert.Greater(pos, -1);

                var headersRaw = content.Subarray(0, pos);
                content = content.Subarray(pos + 4);

                var headers = headersRaw.FromUtf8().Split(new string[] { "\r\n" }, StringSplitOptions.None);
                if (verify != null)
                    verify(headers, content);
            }
        }

        [Test, Timeout(5 * 60 * 1000)]
        public void TestBasicRequestHandling()
        {
            var store = 1024 * 1024;
            var instance = new HttpServer(ProgramServersTests.Port, new HttpServerOptions { StoreFileUploadInFileAtSize = store })
            {
                Handler = new UrlResolver(
                    new UrlMapping(handlerStatic, path: "/static"),
                    new UrlMapping(handlerDynamic, path: "/dynamic"),
                    new UrlMapping(handler64KFile, path: "/64kfile")
                ).Handle
            };
            try
            {
                instance.StartListening();

                testRequest("GET test #1", store, "GET / HTTP/1.1\r\nHost: localhost\r\n\r\n", (headers, content) =>
                {
                    Assert.AreEqual("HTTP/1.1 404 Not Found", headers[0]);
                    Assert.IsTrue(headers.Contains("Content-Type: text/html; charset=utf-8"));
                    Assert.IsTrue(headers.Any(x => x.StartsWith("Content-Length: ")));
                    Assert.IsTrue(content.FromUtf8().Contains("404"));
                });

                testRequest("GET test #2", store, "GET /static?x=y&z=%20&zig=%3D%3d HTTP/1.1\r\nHost: localhost\r\n\r\n", (headers, content) =>
                {
                    Assert.AreEqual("HTTP/1.1 200 OK", headers[0]);
                    Assert.IsTrue(headers.Contains("Content-Type: text/plain; charset=utf-8"));
                    Assert.IsTrue(headers.Contains("Content-Length: 41"));
                    Assert.AreEqual("GET:\nx => [\"y\"]\nz => [\" \"]\nzig => [\"==\"]\n", content.FromUtf8());
                });

                testRequest("GET test #3", store, "GET /static?x[]=1&x%5B%5D=%20&x%5b%5d=%3D%3d HTTP/1.1\r\nHost: localhost\r\n\r\n", (headers, content) =>
                {
                    Assert.AreEqual("HTTP/1.1 200 OK", headers[0]);
                    Assert.IsTrue(headers.Contains("Content-Type: text/plain; charset=utf-8"));
                    Assert.IsTrue(headers.Contains("Content-Length: 29"));
                    Assert.AreEqual("GET:\nx[] => [\"1\", \" \", \"==\"]\n", content.FromUtf8());
                });

                testRequest("GET test #4", store, "GET /dynamic?x=y&z=%20&zig=%3D%3d HTTP/1.1\r\nHost: localhost\r\n\r\n", (headers, content) =>
                {
                    Assert.AreEqual("HTTP/1.1 200 OK", headers[0]);
                    Assert.IsTrue(headers.Contains("Content-Type: text/plain; charset=utf-8"));
                    Assert.IsFalse(headers.Any(x => x.ToLowerInvariant().StartsWith("content-length:")));
                    Assert.IsFalse(headers.Any(x => x.ToLowerInvariant().StartsWith("accept-ranges:")));
                    Assert.AreEqual("GET:\nx => [\"y\"]\nz => [\" \"]\nzig => [\"==\"]\n", content.FromUtf8());
                });

                testRequest("GET test #5", store, "GET /64kfile HTTP/1.1\r\nHost: localhost\r\n\r\n", (headers, content) =>
                {
                    Assert.AreEqual("HTTP/1.1 200 OK", headers[0]);
                    Assert.IsTrue(headers.Contains("Content-Type: application/octet-stream"));
                    Assert.IsTrue(headers.Contains("Content-Length: 65536"));
                    Assert.IsTrue(headers.Contains("Accept-Ranges: bytes"));
                    Assert.AreEqual(65536, content.Length);
                    for (int i = 0; i < content.Length; i++)
                        Assert.AreEqual(content[i], (byte) (i % 256));
                });

                testRequest("GET test #6", store, "GET /64kfile HTTP/1.1\r\nHost: localhost\r\nRange: bytes=23459-38274\r\n\r\n", (headers, content) =>
                {
                    Assert.AreEqual("HTTP/1.1 206 Partial Content", headers[0]);
                    Assert.IsTrue(headers.Contains("Accept-Ranges: bytes"));
                    Assert.IsTrue(headers.Contains("Content-Range: bytes 23459-38274/65536"));
                    Assert.IsTrue(headers.Contains("Content-Type: application/octet-stream"));
                    Assert.IsTrue(headers.Contains("Content-Length: 14816"));
                    for (int i = 0; i < content.Length; i++)
                        Assert.AreEqual((byte) ((163 + i) % 256), content[i]);
                });

                testRequest("GET test #7", store, "GET /64kfile HTTP/1.1\r\nHost: localhost\r\nRange: bytes=65-65,67-67\r\n\r\n", (headers, content) =>
                {
                    Assert.AreEqual("HTTP/1.1 206 Partial Content", headers[0]);
                    Assert.IsTrue(headers.Contains("Accept-Ranges: bytes"));
                    Assert.IsTrue(headers.Any(x => Regex.IsMatch(x, @"^Content-Type: multipart/byteranges; boundary=[0-9A-F]+$")));
                    string boundary = headers.First(x => Regex.IsMatch(x, @"^Content-Type: multipart/byteranges; boundary=[0-9A-F]+$")).Substring(45);
                    Assert.IsTrue(headers.Contains("Content-Length: 284"));
                    byte[] expectedContent = ("--" + boundary + "\r\nContent-Range: bytes 65-65/65536\r\n\r\nA\r\n--" + boundary + "\r\nContent-Range: bytes 67-67/65536\r\n\r\nC\r\n--" + boundary + "--\r\n").ToUtf8();
                    Assert.AreEqual(expectedContent.Length, content.Length);
                    for (int i = 0; i < expectedContent.Length; i++)
                        Assert.AreEqual(expectedContent[i], content[i]);
                });

                testRequest("GET test #8", store, "GET /64kfile HTTP/1.1\r\nHost: localhost\r\nAccept-Encoding: gzip\r\n\r\n", (headers, content) =>
                {
                    Assert.AreEqual("HTTP/1.1 200 OK", headers[0]);
                    Assert.IsTrue(headers.Contains("Accept-Ranges: bytes"));
                    Assert.IsTrue(headers.Contains("Content-Type: application/octet-stream"));
                    Assert.IsTrue(headers.Contains("Content-Encoding: gzip"));
                    Assert.IsTrue(headers.Any(h => h.StartsWith("Content-Length")));
                    GZipStream gz = new GZipStream(new MemoryStream(content), CompressionMode.Decompress);
                    for (int i = 0; i < 65536; i++)
                        Assert.AreEqual(i % 256, gz.ReadByte());
                    Assert.AreEqual(-1, gz.ReadByte());
                });

                testRequest("GET test #9", store, "GET /dynamic HTTP/1.1\r\n\r\n", (headers, content) => Assert.AreEqual("HTTP/1.1 400 Bad Request", headers[0]));
                testRequest("GET test #10", store, "INVALID /request HTTP/1.1\r\n\r\n", (headers, content) => Assert.AreEqual("HTTP/1.1 400 Bad Request", headers[0]));
                testRequest("GET test #11", store, "GET  HTTP/1.1\r\n\r\n", (headers, content) => Assert.AreEqual("HTTP/1.1 400 Bad Request", headers[0]));
                testRequest("GET test #12", store, "!\r\n\r\n", (headers, content) => Assert.AreEqual("HTTP/1.1 400 Bad Request", headers[0]));
            }
            finally
            {
                instance.StopListening();
            }

            foreach (var storeFileUploadInFileAtSize in new[] { 5, 1024 })
            {
                instance = new HttpServer(ProgramServersTests.Port, new HttpServerOptions { StoreFileUploadInFileAtSize = storeFileUploadInFileAtSize })
                {
                    Handler = new UrlResolver(
                        new UrlMapping(handlerStatic, path: "/static"),
                        new UrlMapping(handlerDynamic, path: "/dynamic")
                    ).Handle
                };
                try
                {
                    instance.StartListening();

                    testRequest("POST test #1", storeFileUploadInFileAtSize, "POST /static HTTP/1.1\r\nHost: localhost\r\nContent-Length: 48\r\nContent-Type: application/x-www-form-urlencoded\r\n\r\nx=y&z=%20&zig=%3D%3d&a[]=1&a%5B%5D=2&%61%5b%5d=3", (headers, content) =>
                    {
                        Assert.AreEqual("HTTP/1.1 200 OK", headers[0]);
                        Assert.IsTrue(headers.Contains("Content-Type: text/plain; charset=utf-8"));
                        Assert.IsTrue(headers.Contains("Content-Length: 66"));
                        Assert.AreEqual("\nPOST:\nx => [\"y\"]\nz => [\" \"]\nzig => [\"==\"]\na[] => [\"1\", \"2\", \"3\"]\n", content.FromUtf8());
                    });

                    testRequest("POST test #2", storeFileUploadInFileAtSize, "POST /dynamic HTTP/1.1\r\nHost: localhost\r\nContent-Length: 20\r\nContent-Type: application/x-www-form-urlencoded\r\n\r\nx=y&z=%20&zig=%3D%3d", (headers, content) =>
                    {
                        Assert.AreEqual("HTTP/1.1 200 OK", headers[0]);
                        Assert.IsTrue(headers.Contains("Content-Type: text/plain; charset=utf-8"));
                        Assert.IsFalse(headers.Any(x => x.ToLowerInvariant().StartsWith("content-length:")));
                        Assert.IsFalse(headers.Any(x => x.ToLowerInvariant().StartsWith("accept-ranges:")));
                        Assert.AreEqual("\nPOST:\nx => [\"y\"]\nz => [\" \"]\nzig => [\"==\"]\n", content.FromUtf8());
                    });

                    string postContent = "--abc\r\nContent-Disposition: form-data; name=\"x\"\r\n\r\ny\r\n--abc\r\nContent-Disposition: form-data; name=\"z\"\r\n\r\n%3D%3d\r\n--abc\r\nContent-Disposition: form-data; name=\"y\"; filename=\"z\"\r\nContent-Type: application/weirdo\r\n\r\n%3D%3d\r\n--abc--\r\n";
                    string expectedResponse = "\nPOST:\nx => [\"y\"]\nz => [\"%3D%3d\"]\n\nFiles:\ny => { application/weirdo, z, \"%3D%3d\" (" + (storeFileUploadInFileAtSize < 6 ? "localfile" : "data") + ") }\n";

                    testRequest("POST test #3", storeFileUploadInFileAtSize, "POST /static HTTP/1.1\r\nHost: localhost\r\nContent-Length: " + postContent.Length + "\r\nContent-Type: multipart/form-data; boundary=abc\r\n\r\n" + postContent, (headers, content) =>
                    {
                        Assert.AreEqual("HTTP/1.1 200 OK", headers[0]);
                        Assert.IsTrue(headers.Contains("Content-Type: text/plain; charset=utf-8"));
                        Assert.AreEqual(expectedResponse, content.FromUtf8());
                        Assert.IsTrue(headers.Contains("Content-Length: " + expectedResponse.Length));
                    });

                    testRequest("POST test #4", storeFileUploadInFileAtSize, "POST /dynamic HTTP/1.1\r\nHost: localhost\r\nContent-Length: " + postContent.Length + "\r\nContent-Type: multipart/form-data; boundary=abc\r\n\r\n" + postContent, (headers, content) =>
                    {
                        Assert.AreEqual("HTTP/1.1 200 OK", headers[0]);
                        Assert.IsTrue(headers.Contains("Content-Type: text/plain; charset=utf-8"));
                        Assert.IsFalse(headers.Any(x => x.StartsWith("Content-Length: ")));
                        Assert.IsFalse(headers.Any(x => x.StartsWith("Accept-Ranges: ")));
                        Assert.AreEqual(expectedResponse, content.FromUtf8());
                    });

                    // Test that the server doesn't crash if a field name is missing
                    postContent = "--abc\r\nContent-Disposition: form-data; name=\"x\"\r\n\r\n\r\n--abc\r\nContent-Disposition: form-data\r\n\r\n%3D%3d\r\n--abc\r\nContent-Disposition: form-data; filename=\"z\"\r\nContent-Type: application/weirdo\r\n\r\n%3D%3d\r\n--abc--\r\n";
                    expectedResponse = "\nPOST:\nx => [\"\"]\n";

                    testRequest("POST test #5", storeFileUploadInFileAtSize, "POST /static HTTP/1.1\r\nHost: localhost\r\nContent-Length: " + postContent.Length + "\r\nContent-Type: multipart/form-data; boundary=abc\r\n\r\n" + postContent, (headers, content) =>
                    {
                        Assert.AreEqual("HTTP/1.1 200 OK", headers[0]);
                        Assert.IsTrue(headers.Contains("Content-Type: text/plain; charset=utf-8"));
                        Assert.IsTrue(headers.Any(x => x.StartsWith("Content-Length: ")));
                        Assert.IsFalse(headers.Any(x => x.StartsWith("Accept-Ranges: ")));
                        Assert.AreEqual(expectedResponse, content.FromUtf8());
                    });

                    testRequest("POST test #6", storeFileUploadInFileAtSize, "POST /dynamic HTTP/1.1\r\nHost: localhost\r\nContent-Length: " + postContent.Length + "\r\nContent-Type: multipart/form-data; boundary=abc\r\n\r\n" + postContent, (headers, content) =>
                    {
                        Assert.AreEqual("HTTP/1.1 200 OK", headers[0]);
                        Assert.IsTrue(headers.Contains("Content-Type: text/plain; charset=utf-8"));
                        Assert.IsFalse(headers.Any(x => x.StartsWith("Content-Length: ")));
                        Assert.IsFalse(headers.Any(x => x.StartsWith("Accept-Ranges: ")));
                        Assert.AreEqual(expectedResponse, content.FromUtf8());
                    });
                }
                finally
                {
                    instance.StopListening();
                }
            }
        }

        [Test]
        public void TestKeepaliveAndChunked()
        {
            HttpServer instance = new HttpServer(ProgramServersTests.Port) { Handler = handlerDynamic };
            try
            {
                instance.StartListening();
                Thread.Sleep(100);
                Assert.AreEqual(0, instance.Stats.ActiveHandlers);
                Assert.AreEqual(0, instance.Stats.KeepAliveHandlers);

                TcpClient cl = new TcpClient();
                cl.Connect("localhost", ProgramServersTests.Port);
                cl.ReceiveTimeout = 1000; // 1 sec
                Socket sck = cl.Client;

                // Run three consecutive requests within the same connection using Connection: Keep-alive
                keepaliveAndChunkedPrivate(sck);
                Thread.Sleep(300);
                Assert.AreEqual(0, instance.Stats.ActiveHandlers);
                Assert.AreEqual(1, instance.Stats.KeepAliveHandlers);
                keepaliveAndChunkedPrivate(sck);
                Thread.Sleep(300);
                Assert.AreEqual(0, instance.Stats.ActiveHandlers);
                Assert.AreEqual(1, instance.Stats.KeepAliveHandlers);
                keepaliveAndChunkedPrivate(sck);
                Thread.Sleep(300);
                Assert.AreEqual(0, instance.Stats.ActiveHandlers);
                Assert.AreEqual(1, instance.Stats.KeepAliveHandlers);

                instance.StopListening();
                Assert.IsTrue(instance.ShutdownComplete.WaitOne(TimeSpan.FromSeconds(1)));
                Assert.AreEqual(0, instance.Stats.ActiveHandlers);
                Assert.AreEqual(0, instance.Stats.KeepAliveHandlers);

                sck.Close();
                cl.Close();
            }
            finally
            {
                instance.StopListening(true);
            }
        }

        private void keepaliveAndChunkedPrivate(Socket sck)
        {
            sck.Send("GET /dynamic?aktion=list&showonly=scheduled&limitStart=0&filtermask_t=&filtermask_g=&filtermask_s=&size_max=*&size_min=*&lang=&archivemonth=200709&format_wmv=true&format_avi=true&format_hq=&format_mp4=&lang=&archivemonth=200709&format_wmv=true&format_avi=true&format_hq=&format_mp4=&orderby=time_desc&showonly=recordings HTTP/1.1\r\nHost: localhost\r\nConnection: keep-alive\r\n\r\n".ToUtf8());

            byte[] b = new byte[65536];
            int bytesRead = sck.Receive(b);
            Assert.IsTrue(bytesRead > 0);
            string response = Encoding.UTF8.GetString(b, 0, bytesRead);
            while (!response.Contains("\r\n\r\n"))
            {
                bytesRead = sck.Receive(b);
                Assert.IsTrue(bytesRead > 0);
                response += Encoding.UTF8.GetString(b, 0, bytesRead);
            }
            Assert.IsTrue(response.Contains("\r\n\r\n"));
            int pos = response.IndexOf("\r\n\r\n");
            string headersStr = response.Substring(0, pos);
            string[] headers = response.Split(new string[] { "\r\n" }, StringSplitOptions.None);
            Assert.IsTrue(headers.Contains("Connection: keep-alive"));
            Assert.IsTrue(headers.Contains("Transfer-Encoding: chunked"));
            Assert.IsTrue(headers.Contains("Content-Type: text/plain; charset=utf-8"));

            response = response.Substring(pos + 4);
            while (!response.EndsWith("\r\n0\r\n\r\n"))
            {
                bytesRead = sck.Receive(b);
                Assert.IsTrue(bytesRead > 0);
                response += Encoding.UTF8.GetString(b, 0, bytesRead);
            }

            string reconstruct = "";
            int chunkLen = 0;
            do
            {
                var m = Regex.Match(response, @"^([0-9a-fA-F]+)\r\n");
                Assert.IsTrue(m.Success);
                chunkLen = int.Parse(m.Groups[1].Value, NumberStyles.HexNumber);
                reconstruct += response.Substring(m.Length, chunkLen);
                Assert.AreEqual("\r\n", response.Substring(m.Length + chunkLen, 2));
                response = response.Substring(m.Length + chunkLen + 2);
            } while (chunkLen > 0);

            Assert.AreEqual("", response);
            Assert.AreEqual("GET:\naktion => [\"list\"]\nshowonly => [\"scheduled\", \"recordings\"]\nlimitStart => [\"0\"]\nfiltermask_t => [\"\"]\nfiltermask_g => [\"\"]\nfiltermask_s => [\"\"]\nsize_max => [\"*\"]\nsize_min => [\"*\"]\nlang => [\"\", \"\"]\narchivemonth => [\"200709\", \"200709\"]\nformat_wmv => [\"true\", \"true\"]\nformat_avi => [\"true\", \"true\"]\nformat_hq => [\"\", \"\"]\nformat_mp4 => [\"\", \"\"]\norderby => [\"time_desc\"]\n", reconstruct);
        }

        private static IEnumerable<string> generateGetPostFilesOutput(HttpRequest req)
        {
            if (req.Url.Query.Count() > 0)
                yield return "GET:\n";
            foreach (var key in req.Url.Query.Select(kvp => kvp.Key).Distinct())
                yield return key + " => [" + req.Url.QueryValues(key).JoinString(", ", "\"", "\"") + "]\n";
            if (req.Post.Count > 0)
                yield return "\nPOST:\n";
            foreach (var kvp in req.Post)
                yield return kvp.Key + " => " + kvp.Value + "\n";
            if (req.FileUploads.Count > 0)
                yield return "\nFiles:\n";
            foreach (var kvp in req.FileUploads)
            {
                yield return kvp.Key + " => { " + kvp.Value.ContentType + ", " + kvp.Value.Filename + ", \"";
                using (var stream = kvp.Value.GetStream())
                    yield return stream.ReadAllText(Encoding.UTF8);
                var field1 = kvp.Value.GetType().GetField("LocalFilename", BindingFlags.NonPublic | BindingFlags.Instance);
                yield return "\" (" + (field1 == null ? "null" : field1.GetValue(kvp.Value) == null ? "" : "localfile");
                var field2 = kvp.Value.GetType().GetField("Data", BindingFlags.NonPublic | BindingFlags.Instance);
                yield return (field2 == null ? "null" : field2.GetValue(kvp.Value) == null ? "" : "data") + ") }\n";
            }
        }

        private static HttpResponse handlerStatic(HttpRequest req)
        {
            // This passes a single string, which causes HttpResponse to UTF8ify it and use a MemoryStream
            return HttpResponse.PlainText(generateGetPostFilesOutput(req).JoinString(""));
        }

        private static HttpResponse handlerDynamic(HttpRequest req)
        {
            // This passes an IEnumerable<string>, which causes HttpResponse to use a DynamicContentStream
            return HttpResponse.PlainText(generateGetPostFilesOutput(req), buffered: false);
        }

        private static HttpResponse handler64KFile(HttpRequest req)
        {
            byte[] largeFile = new byte[65536];
            for (int i = 0; i < 65536; i++)
                largeFile[i] = (byte) (i % 256);
            return HttpResponse.Create(new MemoryStream(largeFile), "application/octet-stream");
        }
    }
}
