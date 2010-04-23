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
using NUnit.Direct;
using NUnit.Framework;
using RT.Util;
using RT.Util.ExtensionMethods;
using RT.Util.Streams;

namespace RT.Servers
{
    // Things this doesn't yet test:
    // * Expect: 100-continue / 100 Continue

    [TestFixture]
    public class ServersTestSuite
    {
        static void Main(string[] args)
        {
            NUnitDirect.RunTestsOnAssembly(Assembly.GetEntryAssembly());
            Console.WriteLine("Press Enter to exit.");
            Console.ReadLine();
        }

        private int _port = 12345;

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
            Directory.CreateDirectory(@"C:\temp\testresults");
            string InputStr = @"-----------------------------265001916915724
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
            byte[] TestCase = Encoding.UTF8.GetBytes(InputStr);

            for (int cs = 1; cs < InputStr.Length; cs++)
            {
                HttpRequest r = new HttpRequest
                {
                    Headers = new HttpRequestHeaders
                    {
                        ContentLength = InputStr.Length,
                        ContentMultipartBoundary = "---------------------------265001916915724",
                        ContentType = HttpPostContentType.MultipartFormData
                    },
                    Method = HttpMethod.Post,
                    Url = "/",
                    RestUrl = "/",
                    TempDir = @"C:\temp\testresults"
                };

                using (Stream f = new SlowStream(new MemoryStream(TestCase), cs))
                {
                    r.ParsePostBody(f);
                    var Gets = r.Get;
                    var Posts = r.Post;
                    var Files = r.FileUploads;

                    Assert.IsTrue(Files.ContainsKey("documentfile"));
                    Assert.AreEqual("temp.htm", Files["documentfile"].Filename);
                    Assert.AreEqual("text/html", Files["documentfile"].ContentType);
                    Assert.IsTrue(File.Exists(Files["documentfile"].LocalTempFilename));

                    string FileContent = Encoding.UTF8.GetString(File.ReadAllBytes(Files["documentfile"].LocalTempFilename));
                    File.Delete(Files["documentfile"].LocalTempFilename);
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
                        FileContent);

                    Assert.AreEqual(0, Gets.Count);
                    Assert.AreEqual(2, Posts.Count);
                    Assert.AreEqual(1, Files.Count);

                    Assert.IsTrue(Posts.ContainsKey("y"));
                    Assert.AreEqual(@"This is what should be found in ""y"" at the end of the test.", Posts["y"].Value);
                    Assert.IsTrue(Posts.ContainsKey("What a wonderful day it is today; so wonderful in fact, that I'm inclined to go out and meet friends"));
                    Assert.AreEqual("\r\n<CRLF>(this)<CRLF>\r\n",
                        Posts["What a wonderful day it is today; so wonderful in fact, that I'm inclined to go out and meet friends"].Value);
                }
            }

            try { Directory.Delete(@"C:\temp\testresults"); }
            catch { }
        }

        private void testRequest(string testName, string request, Action<string[], byte[]> verify)
        {
            var requestBytes = request.ToUtf8();
            for (int chunkSize = 0; chunkSize <= requestBytes.Length; chunkSize += Math.Max(1, Math.Min(requestBytes.Length - chunkSize, Rnd.Next(64))))
            {
                if (chunkSize == 0)
                    continue;
                Console.WriteLine("{0}; chunk size {1}", testName, chunkSize);
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
        public void TestSomeRequests()
        {
            HttpServer instance = new HttpServer(new HttpServerOptions { Port = _port });
            instance.RequestHandlerHooks.Add(new HttpRequestHandlerHook(handlerStatic, path: "/static"));
            instance.RequestHandlerHooks.Add(new HttpRequestHandlerHook(handlerDynamic, path: "/dynamic"));
            instance.RequestHandlerHooks.Add(new HttpRequestHandlerHook(handler64KFile, path: "/64kfile"));
            instance.StartListening(false);

            try
            {
                testRequest("GET test #1", "GET / HTTP/1.1\r\nHost: localhost\r\n\r\n", (headers, content) =>
                {
                    Assert.AreEqual("HTTP/1.1 404 Not Found", headers[0]);
                    Assert.IsTrue(headers.Contains("Connection: close"));
                    Assert.IsTrue(headers.Contains("Content-Type: text/html; charset=utf-8"));
                    Assert.IsTrue(headers.Any(x => x.StartsWith("Content-Length: ")));
                    Assert.IsTrue(content.FromUtf8().Contains("404"));
                });

                testRequest("GET test #2", "GET /static?x=y&z=%20&zig=%3D%3d HTTP/1.1\r\nHost: localhost\r\n\r\n", (headers, content) =>
                {
                    Assert.AreEqual("HTTP/1.1 200 OK", headers[0]);
                    Assert.IsTrue(headers.Contains("Content-Type: text/plain; charset=utf-8"));
                    Assert.IsTrue(headers.Contains("Content-Length: 41"));
                    Assert.AreEqual("GET:\nx => [\"y\"]\nz => [\" \"]\nzig => [\"==\"]\n", content.FromUtf8());
                });

                testRequest("GET test #3", "GET /static?x[]=1&x%5B%5D=%20&x%5b%5d=%3D%3d HTTP/1.1\r\nHost: localhost\r\n\r\n", (headers, content) =>
                {
                    Assert.AreEqual("HTTP/1.1 200 OK", headers[0]);
                    Assert.IsTrue(headers.Contains("Content-Type: text/plain; charset=utf-8"));
                    Assert.IsTrue(headers.Contains("Content-Length: 29"));
                    Assert.AreEqual("GET:\nx[] => [\"1\", \" \", \"==\"]\n", content.FromUtf8());
                });

                testRequest("GET test #4", "GET /dynamic?x=y&z=%20&zig=%3D%3d HTTP/1.1\r\nHost: localhost\r\n\r\n", (headers, content) =>
                {
                    Assert.AreEqual("HTTP/1.1 200 OK", headers[0]);
                    Assert.IsTrue(headers.Contains("Content-Type: text/plain; charset=utf-8"));
                    Assert.IsFalse(headers.Any(x => x.ToLowerInvariant().StartsWith("content-length:")));
                    Assert.IsFalse(headers.Any(x => x.ToLowerInvariant().StartsWith("accept-ranges:")));
                    Assert.AreEqual("GET:\nx => [\"y\"]\nz => [\" \"]\nzig => [\"==\"]\n", content.FromUtf8());
                });

                testRequest("GET test #5 (actually a POST)", "POST /static HTTP/1.1\r\nHost: localhost\r\n\r\n", (headers, content) =>
                {
                    Assert.AreEqual("HTTP/1.1 411 Length Required", headers[0]);
                    Assert.IsTrue(headers.Contains("Content-Type: text/html; charset=utf-8"));
                    Assert.IsTrue(headers.Contains("Connection: close"));
                    Assert.IsTrue(headers.Contains("Content-Length: " + content.Length));
                    Assert.IsTrue(content.FromUtf8().Contains("411"));
                });

                testRequest("GET test #6", "GET /64kfile HTTP/1.1\r\nHost: localhost\r\n\r\n", (headers, content) =>
                {
                    Assert.AreEqual("HTTP/1.1 200 OK", headers[0]);
                    Assert.IsTrue(headers.Contains("Content-Type: application/octet-stream"));
                    Assert.IsTrue(headers.Contains("Content-Length: 65536"));
                    Assert.IsTrue(headers.Contains("Accept-Ranges: bytes"));
                    Assert.AreEqual(65536, content.Length);
                    for (int i = 0; i < content.Length; i++)
                        Assert.AreEqual(content[i], (byte) (i % 256));
                });

                testRequest("GET test #7", "GET /64kfile HTTP/1.1\r\nHost: localhost\r\nRange: bytes=23459-38274\r\n\r\n", (headers, content) =>
                {
                    Assert.AreEqual("HTTP/1.1 206 Partial Content", headers[0]);
                    Assert.IsTrue(headers.Contains("Accept-Ranges: bytes"));
                    Assert.IsTrue(headers.Contains("Content-Range: bytes 23459-38274/65536"));
                    Assert.IsTrue(headers.Contains("Content-Type: application/octet-stream"));
                    Assert.IsTrue(headers.Contains("Content-Length: 14816"));
                    for (int i = 0; i < content.Length; i++)
                        Assert.AreEqual((byte) ((163 + i) % 256), content[i]);
                });

                testRequest("GET test #8", "GET /64kfile HTTP/1.1\r\nHost: localhost\r\nRange: bytes=65-65,67-67\r\n\r\n", (headers, content) =>
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

                testRequest("GET test #9", "GET /64kfile HTTP/1.1\r\nHost: localhost\r\nAccept-Encoding: gzip\r\n\r\n", (headers, content) =>
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

                testRequest("GET test #10", "GET /dynamic HTTP/1.1\r\n\r\n", (headers, content) => Assert.AreEqual("HTTP/1.1 400 Bad Request", headers[0]));
                testRequest("GET test #11", "INVALID /request HTTP/1.1\r\n\r\n", (headers, content) => Assert.AreEqual("HTTP/1.1 400 Bad Request", headers[0]));
                testRequest("GET test #12", "GET  HTTP/1.1\r\n\r\n", (headers, content) => Assert.AreEqual("HTTP/1.1 400 Bad Request", headers[0]));
                testRequest("GET test #13", "!\r\n\r\n", (headers, content) => Assert.AreEqual("HTTP/1.1 400 Bad Request", headers[0]));
            }
            finally
            {
                instance.StopListening();
            }

            foreach (var storeFileUploadInFileAtSize in new[] { 5, 1024 })
            {
                instance = new HttpServer(new HttpServerOptions { Port = _port, StoreFileUploadInFileAtSize = storeFileUploadInFileAtSize });
                instance.RequestHandlerHooks.Add(new HttpRequestHandlerHook(handlerStatic, path: "/static"));
                instance.RequestHandlerHooks.Add(new HttpRequestHandlerHook(handlerDynamic, path: "/dynamic"));
                instance.StartListening(false);

                try
                {
                    testRequest("POST test #1, StoreFileUploadInFileAtSize = " + storeFileUploadInFileAtSize, "POST /static HTTP/1.1\r\nHost: localhost\r\nContent-Length: 48\r\nContent-Type: application/x-www-form-urlencoded\r\n\r\nx=y&z=%20&zig=%3D%3d&a[]=1&a%5B%5D=2&%61%5b%5d=3", (headers, content) =>
                    {
                        Assert.AreEqual("HTTP/1.1 200 OK", headers[0]);
                        Assert.IsTrue(headers.Contains("Content-Type: text/plain; charset=utf-8"));
                        Assert.IsTrue(headers.Contains("Content-Length: 66"));
                        Assert.AreEqual("\nPOST:\nx => [\"y\"]\nz => [\" \"]\nzig => [\"==\"]\na[] => [\"1\", \"2\", \"3\"]\n", content.FromUtf8());
                    });

                    testRequest("POST test #2, StoreFileUploadInFileAtSize = " + storeFileUploadInFileAtSize, "POST /dynamic HTTP/1.1\r\nHost: localhost\r\nContent-Length: 20\r\nContent-Type: application/x-www-form-urlencoded\r\n\r\nx=y&z=%20&zig=%3D%3d", (headers, content) =>
                    {
                        Assert.AreEqual("HTTP/1.1 200 OK", headers[0]);
                        Assert.IsTrue(headers.Contains("Content-Type: text/plain; charset=utf-8"));
                        Assert.IsFalse(headers.Any(x => x.ToLowerInvariant().StartsWith("content-length:")));
                        Assert.IsFalse(headers.Any(x => x.ToLowerInvariant().StartsWith("accept-ranges:")));
                        Assert.AreEqual("\nPOST:\nx => [\"y\"]\nz => [\" \"]\nzig => [\"==\"]\n", content.FromUtf8());
                    });

                    string postContent = "--abc\r\nContent-Disposition: form-data; name=\"x\"\r\n\r\ny\r\n--abc\r\nContent-Disposition: form-data; name=\"z\"\r\n\r\n%3D%3d\r\n--abc\r\nContent-Disposition: form-data; name=\"y\"; filename=\"z\"\r\nContent-Type: application/weirdo\r\n\r\n%3D%3d\r\n--abc--\r\n";
                    string expectedResponse = "\nPOST:\nx => [\"y\"]\nz => [\"%3D%3d\"]\n\nFiles:\ny => { application/weirdo, z, \"%3D%3d\" }\n";

                    testRequest("POST test #3, StoreFileUploadInFileAtSize = " + storeFileUploadInFileAtSize, "POST /static HTTP/1.1\r\nHost: localhost\r\nContent-Length: " + postContent.Length + "\r\nContent-Type: multipart/form-data; boundary=abc\r\n\r\n" + postContent, (headers, content) =>
                    {
                        Assert.AreEqual("HTTP/1.1 200 OK", headers[0]);
                        Assert.IsTrue(headers.Contains("Content-Type: text/plain; charset=utf-8"));
                        Assert.IsTrue(headers.Contains("Content-Length: " + expectedResponse.Length));
                        Assert.AreEqual(expectedResponse, content.FromUtf8());
                    });

                    testRequest("POST test #4, StoreFileUploadInFileAtSize = " + storeFileUploadInFileAtSize, "POST /dynamic HTTP/1.1\r\nHost: localhost\r\nContent-Length: " + postContent.Length + "\r\nContent-Type: multipart/form-data; boundary=abc\r\n\r\n" + postContent, (headers, content) =>
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
            HttpServer instance = new HttpServer(new HttpServerOptions { Port = _port });
            instance.RequestHandlerHooks.Add(new HttpRequestHandlerHook(handlerDynamic, path: "/dynamic"));
            instance.StartListening(false);

            TcpClient cl = new TcpClient();
            cl.Connect("localhost", _port);
            cl.ReceiveTimeout = 1000; // 1 sec
            Socket sck = cl.Client;

            // Run three consecutive requests within the same connection using Connection: Keep-alive
            keepaliveAndChunkedPrivate(sck);
            keepaliveAndChunkedPrivate(sck);
            keepaliveAndChunkedPrivate(sck);

            sck.Close();
            cl.Close();
            instance.StopListening();
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
                yield return kvp.Key + " => { " + kvp.Value.ContentType + ", " + kvp.Value.Filename + ", ";
                string fileCont = File.ReadAllText(kvp.Value.LocalTempFilename, Encoding.UTF8);
                yield return "\"" + fileCont + "\" }\n";
            }
        }

        private HttpResponse handlerStatic(HttpRequest req)
        {
            return new HttpResponse
            {
                Status = HttpStatusCode._200_OK,
                Headers = new HttpResponseHeaders { ContentType = "text/plain; charset=utf-8" },
                Content = new MemoryStream(generateGetPostFilesOutput(req).JoinString("").ToUtf8())
            };
        }

        private HttpResponse handlerDynamic(HttpRequest req)
        {
            return new HttpResponse
            {
                Status = HttpStatusCode._200_OK,
                Headers = new HttpResponseHeaders { ContentType = "text/plain; charset=utf-8" },
                Content = new DynamicContentStream(generateGetPostFilesOutput(req), false)
            };
        }

        private HttpResponse handler64KFile(HttpRequest req)
        {
            byte[] largeFile = new byte[65536];
            for (int i = 0; i < 65536; i++)
                largeFile[i] = (byte) (i % 256);
            return new HttpResponse
            {
                Status = HttpStatusCode._200_OK,
                Headers = new HttpResponseHeaders { ContentType = "application/octet-stream" },
                Content = new MemoryStream(largeFile)
            };
        }
    }
}
