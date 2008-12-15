using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using RT.Servers;
using RT.Util.ExtensionMethods;
using RT.Util.Streams;
using System.Globalization;
using System.IO.Compression;

namespace ServersTests
{
    // Things this doesn't yet test:
    // * Expect: 100-continue / 100 Continue

    [TestFixture]
    public class ServersTestSuite
    {
        private int _port = 12345;

        static void Main(string[] args)
        {
            var sts = new ServersTestSuite();
            sts.TestParsePost();
            sts.TestSomeRequests();
            sts.TestKeepaliveAndChunked();
            Console.WriteLine("Tests passed; press Enter to exit.");
            Console.ReadLine();
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
            byte[] TestCase = Encoding.ASCII.GetBytes(InputStr);

            for (int cs = 1; cs < InputStr.Length; cs++)
            {
                Stream f = new SlowStream(new MemoryStream(TestCase), cs);
                HttpRequest r = new HttpRequest(f)
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

                var Gets = r.Get;
                var Posts = r.Post;
                var Files = r.FileUploads;
                f.Close();

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
                Assert.AreEqual(@"This is what should be found in ""y"" at the end of the test.", Posts["y"]);
                Assert.IsTrue(Posts.ContainsKey("What a wonderful day it is today; so wonderful in fact, that I'm inclined to go out and meet friends"));
                Assert.AreEqual("\r\n<CRLF>(this)<CRLF>\r\n",
                    Posts["What a wonderful day it is today; so wonderful in fact, that I'm inclined to go out and meet friends"]);
            }
        }

        private struct TestResponse
        {
            public string[] Headers;
            public byte[] Content;
        }

        private TestResponse GetTestResponse(string Request)
        {
            TcpClient cl = new TcpClient();
            cl.Connect("localhost", _port);
            cl.ReceiveTimeout = 1000; // 1 sec
            Socket sck = cl.Client;
            sck.Send(Request.ToAscii());
            List<byte> response = new List<byte>();
            byte[] b = new byte[65536];
            int bytesRead = sck.Receive(b);
            Assert.IsTrue(bytesRead > 0);
            while (bytesRead > 0)
            {
                response.AddRange(b.Take(bytesRead));
                bytesRead = sck.Receive(b);
            }
            string asText = Encoding.ASCII.GetString(response.ToArray());
            Assert.IsTrue(asText.Contains("\r\n\r\n"));
            int pos = asText.IndexOf("\r\n\r\n");
            TestResponse result = new TestResponse
            {
                Headers = asText.Remove(pos).Split(new string[] { "\r\n" }, StringSplitOptions.None),
                Content = response.Skip(pos + 4).ToArray()
            };
            return result;
        }

        [Test]
        public void TestSomeRequests()
        {
            HttpServer instance = new HttpServer(new HttpServerOptions { Port = _port });
            instance.RequestHandlerHooks.Add(new HttpRequestHandlerHook("/static", TestHandlerStatic));
            instance.RequestHandlerHooks.Add(new HttpRequestHandlerHook("/dynamic", TestHandlerDynamic));
            instance.RequestHandlerHooks.Add(new HttpRequestHandlerHook("/64kfile", TestHandler64KFile));
            instance.StartListening(false);
            try
            {
                TestResponse resp = GetTestResponse("GET / HTTP/1.1\r\nHost: localhost\r\n\r\n");
                Assert.AreEqual("HTTP/1.1 404 Not Found", resp.Headers[0]);
                Assert.IsTrue(resp.Headers.Contains("Connection: close"));
                Assert.IsTrue(resp.Headers.Contains("Content-Type: text/html; charset=utf-8"));
                Assert.IsTrue(resp.Headers.Any(x => x.StartsWith("Content-Length: ")));
                Assert.IsTrue(resp.Content.FromUtf8().Contains("404"));

                resp = GetTestResponse("GET /static?x=y&z=%20&zig=%3D%3d HTTP/1.1\r\nHost: localhost\r\n\r\n");
                Assert.AreEqual("HTTP/1.1 200 OK", resp.Headers[0]);
                Assert.IsTrue(resp.Headers.Contains("Content-Type: text/plain; charset=utf-8"));
                Assert.IsTrue(resp.Headers.Contains("Accept-Ranges: bytes"));
                Assert.IsTrue(resp.Headers.Contains("Content-Length: 35"));
                Assert.AreEqual("GET:\nx => \"y\"\nz => \" \"\nzig => \"==\"\n", resp.Content.FromUtf8());

                resp = GetTestResponse("GET /static?x[]=1&x%5B%5D=%20&x%5b%5d=%3D%3d HTTP/1.1\r\nHost: localhost\r\n\r\n");
                Assert.AreEqual("HTTP/1.1 200 OK", resp.Headers[0]);
                Assert.IsTrue(resp.Headers.Contains("Content-Type: text/plain; charset=utf-8"));
                Assert.IsTrue(resp.Headers.Contains("Accept-Ranges: bytes"));
                Assert.IsTrue(resp.Headers.Contains("Content-Length: 31"));
                Assert.AreEqual("\nGETArr:\nx => [\"1\", \" \", \"==\"]\n", resp.Content.FromUtf8());

                resp = GetTestResponse("GET /dynamic?x=y&z=%20&zig=%3D%3d HTTP/1.1\r\nHost: localhost\r\n\r\n");
                Assert.AreEqual("HTTP/1.1 200 OK", resp.Headers[0]);
                Assert.IsTrue(resp.Headers.Contains("Content-Type: text/plain; charset=utf-8"));
                Assert.IsFalse(resp.Headers.Any(x => x.ToLowerInvariant().StartsWith("content-length:")));
                Assert.IsFalse(resp.Headers.Any(x => x.ToLowerInvariant().StartsWith("accept-ranges:")));
                Assert.AreEqual("GET:\nx => \"y\"\nz => \" \"\nzig => \"==\"\n", resp.Content.FromUtf8());

                resp = GetTestResponse("POST /static HTTP/1.1\r\nHost: localhost\r\n\r\n");
                Assert.AreEqual("HTTP/1.1 411 Length Required", resp.Headers[0]);
                Assert.IsTrue(resp.Headers.Contains("Content-Type: text/html; charset=utf-8"));
                Assert.IsTrue(resp.Headers.Contains("Connection: close"));
                Assert.IsTrue(resp.Headers.Contains("Content-Length: " + resp.Content.Length));
                Assert.IsTrue(resp.Content.FromUtf8().Contains("411"));

                resp = GetTestResponse("GET /64kfile HTTP/1.1\r\nHost: localhost\r\n\r\n");
                Assert.AreEqual("HTTP/1.1 200 OK", resp.Headers[0]);
                Assert.IsTrue(resp.Headers.Contains("Content-Type: application/octet-stream"));
                Assert.IsTrue(resp.Headers.Contains("Content-Length: 65536"));
                Assert.IsTrue(resp.Headers.Contains("Accept-Ranges: bytes"));
                Assert.AreEqual(65536, resp.Content.Length);
                for (int i = 0; i < resp.Content.Length; i++)
                    Assert.AreEqual(resp.Content[i], (byte) (i % 256));

                resp = GetTestResponse("GET /64kfile HTTP/1.1\r\nHost: localhost\r\nRange: bytes=23459-38274\r\n\r\n");
                Assert.AreEqual("HTTP/1.1 206 Partial Content", resp.Headers[0]);
                Assert.IsTrue(resp.Headers.Contains("Accept-Ranges: bytes"));
                Assert.IsTrue(resp.Headers.Contains("Content-Range: bytes 23459-38274/65536"));
                Assert.IsTrue(resp.Headers.Contains("Content-Type: application/octet-stream"));
                Assert.IsTrue(resp.Headers.Contains("Content-Length: 14816"));
                for (int i = 0; i < resp.Content.Length; i++)
                    Assert.AreEqual((byte) ((163 + i) % 256), resp.Content[i]);

                resp = GetTestResponse("GET /64kfile HTTP/1.1\r\nHost: localhost\r\nRange: bytes=65-65,67-67\r\n\r\n");
                Assert.AreEqual("HTTP/1.1 206 Partial Content", resp.Headers[0]);
                Assert.IsTrue(resp.Headers.Contains("Accept-Ranges: bytes"));
                Assert.IsTrue(resp.Headers.Any(x => Regex.IsMatch(x, @"^Content-Type: multipart/byteranges; boundary=[0-9A-F]+$")));
                string boundary = resp.Headers.First(x => Regex.IsMatch(x, @"^Content-Type: multipart/byteranges; boundary=[0-9A-F]+$")).Substring(45);
                Assert.IsTrue(resp.Headers.Contains("Content-Length: 284"));
                byte[] expectedContent = ("--" + boundary + "\r\nContent-Range: bytes 65-65/65536\r\n\r\nA\r\n--" + boundary + "\r\nContent-Range: bytes 67-67/65536\r\n\r\nC\r\n--" + boundary + "--\r\n").ToAscii();
                Assert.AreEqual(expectedContent.Length, resp.Content.Length);
                for (int i = 0; i < expectedContent.Length; i++)
                    Assert.AreEqual(expectedContent[i], resp.Content[i]);

                resp = GetTestResponse("GET /64kfile HTTP/1.1\r\nHost: localhost\r\nAccept-Encoding: gzip\r\n\r\n");
                Assert.AreEqual("HTTP/1.1 200 OK", resp.Headers[0]);
                Assert.IsTrue(resp.Headers.Contains("Accept-Ranges: bytes"));
                Assert.IsTrue(resp.Headers.Contains("Content-Type: application/octet-stream"));
                Assert.IsTrue(resp.Headers.Contains("Content-Encoding: gzip"));
                Assert.IsTrue(resp.Headers.Contains("Content-Length: 1222"));
                GZipStream gz = new GZipStream(new MemoryStream(resp.Content), CompressionMode.Decompress);
                for (int i = 0; i < 65536; i++)
                    Assert.AreEqual(i % 256, gz.ReadByte());
                Assert.AreEqual(-1, gz.ReadByte());

                resp = GetTestResponse("GET /dynamic HTTP/1.1\r\n\r\n");
                Assert.AreEqual("HTTP/1.1 400 Bad Request", resp.Headers[0]);

                resp = GetTestResponse("INVALID /request HTTP/1.1\r\n\r\n");
                Assert.AreEqual("HTTP/1.1 400 Bad Request", resp.Headers[0]);

                resp = GetTestResponse("GET  HTTP/1.1\r\n\r\n");
                Assert.AreEqual("HTTP/1.1 400 Bad Request", resp.Headers[0]);

                resp = GetTestResponse("!\r\n\r\n");
                Assert.AreEqual("HTTP/1.1 400 Bad Request", resp.Headers[0]);
            }
            finally
            {
                instance.StopListening();
            }

            for (int i = 5; i <= 1024; i += 1019)
            {
                instance = new HttpServer(new HttpServerOptions { Port = _port, UseFileUploadAtSize = i });
                instance.RequestHandlerHooks.Add(new HttpRequestHandlerHook("/static", TestHandlerStatic));
                instance.RequestHandlerHooks.Add(new HttpRequestHandlerHook("/dynamic", TestHandlerDynamic));
                instance.StartListening(false);

                try
                {
                    TestResponse resp = GetTestResponse("POST /static HTTP/1.1\r\nHost: localhost\r\nContent-Length: 48\r\nContent-Type: application/x-www-form-urlencoded\r\n\r\nx=y&z=%20&zig=%3D%3d&a[]=1&a%5B%5D=2&%61%5b%5d=3");
                    Assert.AreEqual("HTTP/1.1 200 OK", resp.Headers[0]);
                    Assert.IsTrue(resp.Headers.Contains("Content-Type: text/plain; charset=utf-8"));
                    Assert.IsTrue(resp.Headers.Contains("Content-Length: 68"));
                    Assert.IsTrue(resp.Headers.Contains("Accept-Ranges: bytes"));
                    Assert.AreEqual("\nPOST:\nx => \"y\"\nz => \" \"\nzig => \"==\"\n\nPOSTArr:\na => [\"1\", \"2\", \"3\"]\n", resp.Content.FromUtf8());

                    resp = GetTestResponse("POST /dynamic HTTP/1.1\r\nHost: localhost\r\nContent-Length: 20\r\nContent-Type: application/x-www-form-urlencoded\r\n\r\nx=y&z=%20&zig=%3D%3d");
                    Assert.AreEqual("HTTP/1.1 200 OK", resp.Headers[0]);
                    Assert.IsTrue(resp.Headers.Contains("Content-Type: text/plain; charset=utf-8"));
                    Assert.IsFalse(resp.Headers.Any(x => x.ToLowerInvariant().StartsWith("content-length:")));
                    Assert.IsFalse(resp.Headers.Any(x => x.ToLowerInvariant().StartsWith("accept-ranges:")));
                    Assert.AreEqual("\nPOST:\nx => \"y\"\nz => \" \"\nzig => \"==\"\n", resp.Content.FromUtf8());

                    string postContent = "--abc\r\nContent-Disposition: form-data; name=\"x\"\r\n\r\ny\r\n--abc\r\nContent-Disposition: form-data; name=\"y\"; filename=\"z\"\r\nContent-Type: application/weirdo\r\n\r\n%3D%3d\r\n--abc--\r\n";
                    string expectedResponse = "\nPOST:\nx => \"y\"\n\nFiles:\ny => { application/weirdo, z, \"%3D%3d\" }\n";

                    resp = GetTestResponse("POST /static HTTP/1.1\r\nHost: localhost\r\nContent-Length: " + postContent.Length + "\r\nContent-Type: multipart/form-data; boundary=abc\r\n\r\n" + postContent);
                    Assert.AreEqual("HTTP/1.1 200 OK", resp.Headers[0]);
                    Assert.IsTrue(resp.Headers.Contains("Content-Type: text/plain; charset=utf-8"));
                    Assert.IsTrue(resp.Headers.Contains("Content-Length: " + expectedResponse.Length));
                    Assert.IsTrue(resp.Headers.Contains("Accept-Ranges: bytes"));
                    Assert.AreEqual(expectedResponse, resp.Content.FromUtf8());

                    resp = GetTestResponse("POST /dynamic HTTP/1.1\r\nHost: localhost\r\nContent-Length: " + postContent.Length + "\r\nContent-Type: multipart/form-data; boundary=abc\r\n\r\n" + postContent);
                    Assert.AreEqual("HTTP/1.1 200 OK", resp.Headers[0]);
                    Assert.IsTrue(resp.Headers.Contains("Content-Type: text/plain; charset=utf-8"));
                    Assert.IsFalse(resp.Headers.Any(x => x.StartsWith("Content-Length: ")));
                    Assert.IsFalse(resp.Headers.Any(x => x.StartsWith("Accept-Ranges: ")));
                    Assert.AreEqual(expectedResponse, resp.Content.FromUtf8());
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
            instance.RequestHandlerHooks.Add(new HttpRequestHandlerHook("/dynamic", TestHandlerDynamic));
            instance.StartListening(false);

            TcpClient cl = new TcpClient();
            cl.Connect("localhost", _port);
            cl.ReceiveTimeout = 1000; // 1 sec
            Socket sck = cl.Client;

            // Run three consecutive requests within the same connection using Connection: Keep-alive
            TestKeepaliveAndChunkedPrivate(sck);
            TestKeepaliveAndChunkedPrivate(sck);
            TestKeepaliveAndChunkedPrivate(sck);

            sck.Close();
            cl.Close();
            instance.StopListening();
        }

        private void TestKeepaliveAndChunkedPrivate(Socket sck)
        {
            sck.Send("GET /dynamic?aktion=list&showonly=scheduled&limitStart=0&filtermask_t=&filtermask_g=&filtermask_s=&size_max=*&size_min=*&lang=&archivemonth=200709&format_wmv=true&format_avi=true&format_hq=&format_mp4=&lang=&archivemonth=200709&format_wmv=true&format_avi=true&format_hq=&format_mp4=&orderby=time_desc&showonly=recordings HTTP/1.1\r\nHost: localhost\r\nConnection: keep-alive\r\n\r\n".ToAscii());

            byte[] b = new byte[65536];
            int bytesRead = sck.Receive(b);
            Assert.IsTrue(bytesRead > 0);
            string response = Encoding.ASCII.GetString(b, 0, bytesRead);
            while (!response.Contains("\r\n\r\n"))
            {
                bytesRead = sck.Receive(b);
                Assert.IsTrue(bytesRead > 0);
                response += Encoding.ASCII.GetString(b, 0, bytesRead);
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
                response += Encoding.ASCII.GetString(b, 0, bytesRead);
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
            Assert.AreEqual("GET:\naktion => \"list\"\nshowonly => \"recordings\"\nlimitStart => \"0\"\nfiltermask_t => \"\"\nfiltermask_g => \"\"\nfiltermask_s => \"\"\nsize_max => \"*\"\nsize_min => \"*\"\nlang => \"\"\narchivemonth => \"200709\"\nformat_wmv => \"true\"\nformat_avi => \"true\"\nformat_hq => \"\"\nformat_mp4 => \"\"\norderby => \"time_desc\"\n", reconstruct);
        }

        private IEnumerable<string> GenerateGetPostFilesOutput(HttpRequest req)
        {
            if (req.Get.Count > 0)
                yield return "GET:\n";
            foreach (var kvp in req.Get)
                yield return kvp.Key + " => \"" + kvp.Value + "\"\n";
            if (req.GetArr.Count > 0)
                yield return "\nGETArr:\n";
            foreach (var kvp in req.GetArr)
                yield return kvp.Key + " => [" + kvp.Value.Select(x => "\"" + x + "\"").Join(", ") + "]\n";
            if (req.Post.Count > 0)
                yield return "\nPOST:\n";
            foreach (var kvp in req.Post)
                yield return kvp.Key + " => \"" + kvp.Value + "\"\n";
            if (req.PostArr.Count > 0)
                yield return "\nPOSTArr:\n";
            foreach (var kvp in req.PostArr)
                yield return kvp.Key + " => [" + kvp.Value.Select(x => "\"" + x + "\"").Join(", ") + "]\n";
            if (req.FileUploads.Count > 0)
                yield return "\nFiles:\n";
            foreach (var kvp in req.FileUploads)
            {
                yield return kvp.Key + " => { " + kvp.Value.ContentType + ", " + kvp.Value.Filename + ", ";
                string fileCont = File.ReadAllText(kvp.Value.LocalTempFilename, Encoding.UTF8);
                yield return "\"" + fileCont + "\" }\n";
            }
        }

        private HttpResponse TestHandlerStatic(HttpRequest req)
        {
            return new HttpResponse
            {
                Status = HttpStatusCode._200_OK,
                Headers = new HttpResponseHeaders { ContentType = "text/plain; charset=utf-8" },
                Content = new MemoryStream(GenerateGetPostFilesOutput(req).Join("").ToUtf8())
            };
        }

        private HttpResponse TestHandlerDynamic(HttpRequest req)
        {
            return new HttpResponse
            {
                Status = HttpStatusCode._200_OK,
                Headers = new HttpResponseHeaders { ContentType = "text/plain; charset=utf-8" },
                Content = new DynamicContentStream(GenerateGetPostFilesOutput(req), false)
            };
        }

        private HttpResponse TestHandler64KFile(HttpRequest req)
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
