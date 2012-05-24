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
using NUnit.Direct;
using NUnit.Framework;
using RT.Util;
using RT.Util.ExtensionMethods;
using RT.Util.Streams;

[assembly: Timeout(40000)]

#pragma warning disable 1591    // Missing XML comment for publicly visible type or member

namespace RT.Servers
{
    // Things this doesn't yet test:
    // * Expect: 100-continue / 100 Continue

    [TestFixture]
    public sealed class ServersTestSuite
    {
        static void Main(string[] args)
        {
            bool wait = !args.Contains("--no-wait");

            NUnitDirect.RunTestsOnAssembly(Assembly.GetEntryAssembly());

            if (wait)
            {
                Console.WriteLine("Press Enter to exit.");
                Console.ReadLine();
            }
        }

        private int _port = 12347;

        [Test]
        public void TestParseGet()
        {
            string testQueryString1 = "apple=bravery&cooking=dinner&elephant=foxtrot&ghost=hangman";
            string testQueryString2 = "apple=" + "!@#$".UrlEscape() + "&cooking=" + "%^&*".UrlEscape() + "&elephant=" + "()=+".UrlEscape() + "&ghost=" + "абвгд".UrlEscape();
            string testQueryString3 = "apple[]=" + "!@#$".UrlEscape() + "&apple%5b%5d=" + "%^&*".UrlEscape() + "&apple%5b]=" + "()=+".UrlEscape() + "&ghost[%5d=" + "абвгд".UrlEscape();

            for (int chunksize = 1; chunksize < Math.Max(Math.Max(testQueryString1.Length, testQueryString2.Length), testQueryString3.Length); chunksize++)
            {
                var dic = HttpRequest.ParseQueryValueParameters(new StreamReader(new SlowStream(new MemoryStream(Encoding.UTF8.GetBytes(testQueryString1)), chunksize)));
                Assert.AreEqual(4, dic.Count);
                Assert.IsTrue(dic.ContainsKey("apple"));
                Assert.IsTrue(dic.ContainsKey("cooking"));
                Assert.IsTrue(dic.ContainsKey("elephant"));
                Assert.IsTrue(dic.ContainsKey("ghost"));
                Assert.AreEqual("bravery", dic["apple"].Value);
                Assert.AreEqual("dinner", dic["cooking"].Value);
                Assert.AreEqual("foxtrot", dic["elephant"].Value);
                Assert.AreEqual("hangman", dic["ghost"].Value);

                dic = HttpRequest.ParseQueryValueParameters(new StreamReader(new SlowStream(new MemoryStream(Encoding.UTF8.GetBytes(testQueryString2)), chunksize)));
                Assert.AreEqual(4, dic.Count);
                Assert.IsTrue(dic.ContainsKey("apple"));
                Assert.IsTrue(dic.ContainsKey("cooking"));
                Assert.IsTrue(dic.ContainsKey("elephant"));
                Assert.IsTrue(dic.ContainsKey("ghost"));
                Assert.AreEqual("!@#$", dic["apple"].Value);
                Assert.AreEqual("%^&*", dic["cooking"].Value);
                Assert.AreEqual("()=+", dic["elephant"].Value);
                Assert.AreEqual("абвгд", dic["ghost"].Value);

                dic = HttpRequest.ParseQueryValueParameters(new StreamReader(new SlowStream(new MemoryStream(Encoding.UTF8.GetBytes(testQueryString3)), chunksize)));
                Assert.AreEqual(2, dic.Count);
                Assert.IsTrue(dic.ContainsKey("apple[]"));
                Assert.IsTrue(dic.ContainsKey("ghost[]"));
                Assert.AreEqual(3, dic["apple[]"].Count);
                Assert.IsTrue(dic["apple[]"].Contains("!@#$"));
                Assert.IsTrue(dic["apple[]"].Contains("%^&*"));
                Assert.IsTrue(dic["apple[]"].Contains("()=+"));
                Assert.AreEqual(1, dic["ghost[]"].Count);
                Assert.IsTrue(dic["ghost[]"].Contains("абвгд"));
            }
        }

        [Test]
        public void TestParsePost()
        {
            string inputStr = @"-----------------------------265001916915724
Content-Disposition: form-data; name=""y""

This is what should be found in ""y"" at the end of the test.
-----------------------------265001916915724
Content-Disposition: form-data; name=""What a wonderful day it is today; so wonderful in fact, that I'm inclined to go out and meet friends""


<CRLF>(this)<CRLF>

-----------------------------265001916915724
Content-Disposition: form-data; name=""documentfile""; filename=""temp.htm""
Content-Type: text/html

<html>
    <head>
    </head>
    <body>
        <form action='http://localhost:8988/index.php' method='post' enctype='multipart/form-data'>
            <input type='hidden' name='x' value='a b'>
            <textarea name='y'>a b</textarea>
            <input type='file' name='z'>
            <input type='submit'>
        </form>
    </body>
</html>
-----------------------------265001916915724--
";
            byte[] testCase = Encoding.UTF8.GetBytes(inputStr);

            var directoryNotToBeCreated = @"C:\serverstests";
            int i = 1;
            while (Directory.Exists(directoryNotToBeCreated))
            {
                i++;
                directoryNotToBeCreated = @"C:\serverstests_" + i;
            }

            for (int cs = 1; cs < testCase.Length; cs++)
            {
                HttpRequest r = new HttpRequest
                {
                    Headers = new HttpRequestHeaders
                    {
                        ContentLength = inputStr.Length,
                        ContentMultipartBoundary = "---------------------------265001916915724",
                        ContentType = HttpPostContentType.MultipartFormData
                    },
                    Method = HttpMethod.Post,
                    Url = "/"
                };

                using (Stream f = new SlowStream(new MemoryStream(testCase), cs))
                {
                    r.ParsePostBody(f, directoryNotToBeCreated);
                    var gets = r.Get;
                    var posts = r.Post;
                    var files = r.FileUploads;

                    Assert.IsTrue(files.ContainsKey("documentfile"));
                    Assert.AreEqual("temp.htm", files["documentfile"].Filename);
                    Assert.AreEqual("text/html", files["documentfile"].ContentType);

                    using (var stream = files["documentfile"].GetStream())
                    {
                        string fileContent = Encoding.UTF8.GetString(stream.ReadAllBytes());
                        Assert.AreEqual(@"<html>
    <head>
    </head>
    <body>
        <form action='http://localhost:8988/index.php' method='post' enctype='multipart/form-data'>
            <input type='hidden' name='x' value='a b'>
            <textarea name='y'>a b</textarea>
            <input type='file' name='z'>
            <input type='submit'>
        </form>
    </body>
</html>",
                            fileContent);
                    }

                    Assert.AreEqual(0, gets.Count);
                    Assert.AreEqual(2, posts.Count);
                    Assert.AreEqual(1, files.Count);

                    Assert.IsTrue(posts.ContainsKey("y"));
                    Assert.AreEqual(@"This is what should be found in ""y"" at the end of the test.", posts["y"].Value);
                    Assert.IsTrue(posts.ContainsKey("What a wonderful day it is today; so wonderful in fact, that I'm inclined to go out and meet friends"));
                    Assert.AreEqual("\r\n<CRLF>(this)<CRLF>\r\n",
                        posts["What a wonderful day it is today; so wonderful in fact, that I'm inclined to go out and meet friends"].Value);
                }
            }

            Assert.IsFalse(Directory.Exists(directoryNotToBeCreated));
        }

        private void testRequest(string testName, int storeFileUploadInFileAtSize, string request, Action<string[], byte[]> verify)
        {
            var requestBytes = request.ToUtf8();
            for (int chunkSize = 0; chunkSize <= requestBytes.Length; chunkSize += Rnd.Next(1, 64).ClipMax(requestBytes.Length - chunkSize).ClipMin(1))
            {
                if (chunkSize == 0)
                    continue;
                Console.WriteLine("{0}; SFUIFAS {3}; length {2}; chunk size {1}", testName, chunkSize, requestBytes.Length, storeFileUploadInFileAtSize);
                TcpClient cl = new TcpClient();
                cl.Connect("localhost", _port);
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

        [Test]
        public void TestAbortedEndReceive()
        {
            var instance = new HttpServer(new HttpServerOptions { Port = _port })
            {
                Handler = new UrlPathResolver(
                    new UrlPathHook(handlerStatic, path: "/static")
                ).Handle
            };
            try
            {
                instance.StartListening();

                TcpClient cl = new TcpClient();
                cl.Connect("localhost", _port);
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

        private static void readResponseUntilContent(Socket sck)
        {
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
        }

        [Test]
        public void TestKeepaliveShutdownGentle()
        {
            var instance = new HttpServer(new HttpServerOptions { Port = _port })
            {
                Handler = new UrlPathResolver(
                    new UrlPathHook(handlerStatic, path: "/static")
                ).Handle
            };
            try
            {
                instance.StartListening();

                TcpClient cl = new TcpClient();
                cl.Connect("localhost", _port);
                cl.ReceiveTimeout = 1000; // 1 sec
                Socket sck = cl.Client;
                sck.Send("GET /static HTTP/1.1\r\nHost: localhost\r\n".ToUtf8());
                Thread.Sleep(500);
                Assert.AreEqual(1, instance.Stats.ActiveHandlers);
                sck.Send("Connection: keep-alive\r\n\r\n".ToUtf8());

                readResponseUntilContent(sck);
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
            var instance = new HttpServer(new HttpServerOptions { Port = _port })
            {
                Handler = new UrlPathResolver(
                    new UrlPathHook(handlerStatic, path: "/static")
                ).Handle
            };
            try
            {
                instance.StartListening();

                TcpClient cl = new TcpClient();
                cl.Connect("localhost", _port);
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
                readResponseUntilContent(sck);

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
            var instance = new HttpServer(new HttpServerOptions { Port = _port })
            {
                Handler = new UrlPathResolver(
                    new UrlPathHook(handlerStatic, path: "/static")
                ).Handle
            };
            try
            {
                instance.StartListening();

                TcpClient cl = new TcpClient();
                cl.Connect("localhost", _port);
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

        [Test, Timeout(5 * 60 * 1000)]
        public void TestSomeRequests()
        {
            var store = 1024 * 1024;
            var instance = new HttpServer(new HttpServerOptions { Port = _port, StoreFileUploadInFileAtSize = store })
            {
                Handler = new UrlPathResolver(
                    new UrlPathHook(handlerStatic, path: "/static"),
                    new UrlPathHook(handlerDynamic, path: "/dynamic"),
                    new UrlPathHook(handler64KFile, path: "/64kfile")
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

                testRequest("GET test #5 (actually a POST)", store, "POST /static HTTP/1.1\r\nHost: localhost\r\n\r\n", (headers, content) =>
                {
                    Assert.AreEqual("HTTP/1.1 411 Length Required", headers[0]);
                    Assert.IsTrue(headers.Contains("Content-Type: text/html; charset=utf-8"));
                    Assert.IsTrue(headers.Contains("Connection: close"));
                    Assert.IsTrue(headers.Contains("Content-Length: " + content.Length));
                    Assert.IsTrue(content.FromUtf8().Contains("411"));
                });

                testRequest("GET test #6", store, "GET /64kfile HTTP/1.1\r\nHost: localhost\r\n\r\n", (headers, content) =>
                {
                    Assert.AreEqual("HTTP/1.1 200 OK", headers[0]);
                    Assert.IsTrue(headers.Contains("Content-Type: application/octet-stream"));
                    Assert.IsTrue(headers.Contains("Content-Length: 65536"));
                    Assert.IsTrue(headers.Contains("Accept-Ranges: bytes"));
                    Assert.AreEqual(65536, content.Length);
                    for (int i = 0; i < content.Length; i++)
                        Assert.AreEqual(content[i], (byte) (i % 256));
                });

                testRequest("GET test #7", store, "GET /64kfile HTTP/1.1\r\nHost: localhost\r\nRange: bytes=23459-38274\r\n\r\n", (headers, content) =>
                {
                    Assert.AreEqual("HTTP/1.1 206 Partial Content", headers[0]);
                    Assert.IsTrue(headers.Contains("Accept-Ranges: bytes"));
                    Assert.IsTrue(headers.Contains("Content-Range: bytes 23459-38274/65536"));
                    Assert.IsTrue(headers.Contains("Content-Type: application/octet-stream"));
                    Assert.IsTrue(headers.Contains("Content-Length: 14816"));
                    for (int i = 0; i < content.Length; i++)
                        Assert.AreEqual((byte) ((163 + i) % 256), content[i]);
                });

                testRequest("GET test #8", store, "GET /64kfile HTTP/1.1\r\nHost: localhost\r\nRange: bytes=65-65,67-67\r\n\r\n", (headers, content) =>
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

                testRequest("GET test #9", store, "GET /64kfile HTTP/1.1\r\nHost: localhost\r\nAccept-Encoding: gzip\r\n\r\n", (headers, content) =>
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

                testRequest("GET test #10", store, "GET /dynamic HTTP/1.1\r\n\r\n", (headers, content) => Assert.AreEqual("HTTP/1.1 400 Bad Request", headers[0]));
                testRequest("GET test #11", store, "INVALID /request HTTP/1.1\r\n\r\n", (headers, content) => Assert.AreEqual("HTTP/1.1 400 Bad Request", headers[0]));
                testRequest("GET test #12", store, "GET  HTTP/1.1\r\n\r\n", (headers, content) => Assert.AreEqual("HTTP/1.1 400 Bad Request", headers[0]));
                testRequest("GET test #13", store, "!\r\n\r\n", (headers, content) => Assert.AreEqual("HTTP/1.1 400 Bad Request", headers[0]));
            }
            finally
            {
                instance.StopListening();
            }

            foreach (var storeFileUploadInFileAtSize in new[] { 5, 1024 })
            {
                instance = new HttpServer(new HttpServerOptions { Port = _port, StoreFileUploadInFileAtSize = storeFileUploadInFileAtSize })
                {
                    Handler = new UrlPathResolver(
                        new UrlPathHook(handlerStatic, path: "/static"),
                        new UrlPathHook(handlerDynamic, path: "/dynamic")
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
            HttpServer instance = new HttpServer(new HttpServerOptions { Port = _port }) { Handler = handlerDynamic };
            try
            {
                instance.StartListening();
                Thread.Sleep(100);
                Assert.AreEqual(0, instance.Stats.ActiveHandlers);
                Assert.AreEqual(0, instance.Stats.KeepAliveHandlers);

                TcpClient cl = new TcpClient();
                cl.Connect("localhost", _port);
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

        private IEnumerable<string> generateGetPostFilesOutput(HttpRequest req)
        {
            if (req.Get.Count > 0)
                yield return "GET:\n";
            foreach (var kvp in req.Get)
                yield return kvp.Key + " => " + kvp.Value + "\n";
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

        private HttpResponse handlerStatic(HttpRequest req)
        {
            // This passes a single string, which causes HttpResponse to UTF8ify it and use a MemoryStream
            return HttpResponse.PlainText(generateGetPostFilesOutput(req).JoinString(""));
        }

        private HttpResponse handlerDynamic(HttpRequest req)
        {
            // This passes an IEnumerable<string>, which causes HttpResponse to use a DynamicContentStream
            return HttpResponse.PlainText(generateGetPostFilesOutput(req), buffered: false);
        }

        private HttpResponse handler64KFile(HttpRequest req)
        {
            byte[] largeFile = new byte[65536];
            for (int i = 0; i < 65536; i++)
                largeFile[i] = (byte) (i % 256);
            return HttpResponse.Create(new MemoryStream(largeFile), "application/octet-stream");
        }

        [Test]
        public void TestClientIPAddress()
        {
            HttpRequest request = null;
            var instance = new HttpServer(new HttpServerOptions { Port = _port })
            {
                Handler = new UrlPathResolver(
                    new UrlPathHook(req => { request = req; return HttpResponse.PlainText("blah"); }, path: "/static")
                ).Handle
            };
            try
            {
                instance.StartListening();

                TcpClient cl = new TcpClient();
                cl.Connect("localhost", _port);
                cl.ReceiveTimeout = 1000; // 1 sec
                Socket sck = cl.Client;

                sck.Send("GET /static HTTP/1.1\r\nHost: localhost\r\nConnection: keep-alive\r\n\r\n".ToUtf8());
                readResponseUntilContent(sck);
                Assert.IsTrue(IPAddress.IsLoopback(request.ClientIPAddress));
                Assert.IsTrue(IPAddress.IsLoopback(request.SourceIP.Address));
                Assert.IsNull(request.Headers["X-Forwarded-For"]);

                sck.Send("GET /static HTTP/1.1\r\nHost: localhost\r\nConnection: keep-alive\r\nX-Forwarded-For: 12.34.56.78\r\n\r\n".ToUtf8());
                readResponseUntilContent(sck);
                Assert.AreEqual(IPAddress.Parse("12.34.56.78"), request.ClientIPAddress);
                Assert.IsTrue(IPAddress.IsLoopback(request.SourceIP.Address));
                Assert.IsNotNull(request.Headers["X-Forwarded-For"]);

                sck.Send("GET /static HTTP/1.1\r\nHost: localhost\r\nConnection: keep-alive\r\nX-Forwarded-For: 2a00:1450:400c:c01::93, 12.34.56.78\r\n\r\n".ToUtf8());
                readResponseUntilContent(sck);
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
            var instance = new HttpServer(new HttpServerOptions { Port = _port })
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
                        cl.Connect("localhost", _port);
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
        public void TestErrorHandlerExceptions()
        {
            var instance = new HttpServer(new HttpServerOptions { Port = _port, OutputExceptionInformation = true });
            try
            {
                instance.StartListening();

                var getResponse = Ut.Lambda(() =>
                {
                    TcpClient cl = new TcpClient();
                    cl.Connect("localhost", _port);
                    cl.ReceiveTimeout = 1000; // 1 sec
                    cl.Client.Send("GET /static HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n".ToUtf8());
                    var response = Encoding.UTF8.GetString(new SocketReaderStream(cl.Client, long.MaxValue).ReadAllBytes());
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

                // Test that the main handler returning null results in a 500 error
                instance.Handler = req => { return null; };
                instance.ErrorHandler = (req, ex) => { storedEx = ex; throw new HttpException(HttpStatusCode._203_NonAuthoritativeInformation); };
                resp = getResponse();
                Assert.IsTrue(storedEx is HttpException && (storedEx as HttpException).StatusCode == HttpStatusCode._500_InternalServerError);
                Assert.AreEqual(HttpStatusCode._500_InternalServerError, resp.Item1);

                // Test that the error handler returning null invokes the default error handler
                instance.Handler = req => { throw new HttpException(HttpStatusCode._201_Created); };
                instance.ErrorHandler = (req, ex) => { return null; };
                resp = getResponse();
                Assert.AreEqual(HttpStatusCode._201_Created, resp.Item1);
            }
            finally
            {
                instance.StopListening(brutal: true);
            }
        }
    }
}
