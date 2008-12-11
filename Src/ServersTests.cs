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

namespace ServersTests
{
    // Things this doesn't yet test:
    // * GETArr/POSTArr
    // * Connection: keep-alive
    // * Transfer-Encoding: chunked
    // * Content-Encoding: gzip
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
                HTTPRequest r = new HTTPRequest(f)
                {
                    Headers = new HTTPRequestHeaders
                    {
                        ContentLength = InputStr.Length,
                        ContentMultipartBoundary = "---------------------------265001916915724",
                        ContentType = HTTPPOSTContentType.MultipartFormData
                    },
                    Method = HTTPMethod.POST,
                    URL = "/",
                    RestURL = "/",
                    TempDir = @"C:\temp\testresults"
                };

                var Gets = r.GET;
                var Posts = r.POST;
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
            cl.ReceiveTimeout = 1000000; // 1000 sec
            Socket sck = cl.Client;
            sck.Send(Request.ToASCII());
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
            HTTPServer instance = new HTTPServer(new HTTPServerOptions { Port = _port });
            instance.RequestHandlerHooks.Add(new HTTPRequestHandlerHook("/static", TestHandlerStatic));
            instance.RequestHandlerHooks.Add(new HTTPRequestHandlerHook("/dynamic", TestHandlerDynamic));
            instance.RequestHandlerHooks.Add(new HTTPRequestHandlerHook("/64kfile", TestHandler64KFile));
            instance.StartListening(false);
            try
            {
                TestResponse resp = GetTestResponse("GET / HTTP/1.1\r\nHost: localhost\r\n\r\n");
                Assert.AreEqual("HTTP/1.1 404 Not Found", resp.Headers[0]);
                Assert.IsTrue(resp.Headers.Contains("Connection: close"));
                Assert.IsTrue(resp.Headers.Contains("Content-Type: text/html; charset=utf-8"));
                Assert.IsTrue(resp.Headers.Any(x => x.StartsWith("Content-Length: ")));
                Assert.IsTrue(resp.Content.FromUTF8().Contains("404"));

                resp = GetTestResponse("GET /static?x=y&z=%20&zig=%3D%3d HTTP/1.1\r\nHost: localhost\r\n\r\n");
                Assert.AreEqual("HTTP/1.1 200 OK", resp.Headers[0]);
                Assert.IsTrue(resp.Headers.Contains("Content-Type: text/plain; charset=utf-8"));
                Assert.IsTrue(resp.Headers.Contains("Accept-Ranges: bytes"));
                Assert.IsTrue(resp.Headers.Contains("Content-Length: 35"));
                Assert.AreEqual("GET:\nx => \"y\"\nz => \" \"\nzig => \"==\"\n", resp.Content.FromUTF8());

                resp = GetTestResponse("GET /dynamic?x=y&z=%20&zig=%3D%3d HTTP/1.1\r\nHost: localhost\r\n\r\n");
                Assert.AreEqual("HTTP/1.1 200 OK", resp.Headers[0]);
                Assert.IsTrue(resp.Headers.Contains("Content-Type: text/plain; charset=utf-8"));
                Assert.IsFalse(resp.Headers.Any(x => x.ToLowerInvariant().StartsWith("content-length:")));
                Assert.IsFalse(resp.Headers.Any(x => x.ToLowerInvariant().StartsWith("accept-ranges:")));
                Assert.AreEqual("GET:\nx => \"y\"\nz => \" \"\nzig => \"==\"\n", resp.Content.FromUTF8());

                resp = GetTestResponse("POST /static HTTP/1.1\r\nHost: localhost\r\n\r\n");
                Assert.AreEqual("HTTP/1.1 411 Length Required", resp.Headers[0]);
                Assert.IsTrue(resp.Headers.Contains("Content-Type: text/html; charset=utf-8"));
                Assert.IsTrue(resp.Headers.Contains("Connection: close"));
                Assert.IsTrue(resp.Headers.Contains("Content-Length: " + resp.Content.Length));
                Assert.IsTrue(resp.Content.FromUTF8().Contains("411"));

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
                byte[] expectedContent = ("--" + boundary + "\r\nContent-Range: bytes 65-65/65536\r\n\r\nA\r\n--" + boundary + "\r\nContent-Range: bytes 67-67/65536\r\n\r\nC\r\n--" + boundary + "--\r\n").ToASCII();
                Assert.AreEqual(expectedContent.Length, resp.Content.Length);
                for (int i = 0; i < expectedContent.Length; i++)
                    Assert.AreEqual(expectedContent[i], resp.Content[i]);

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
                instance = new HTTPServer(new HTTPServerOptions { Port = _port, UseFileUploadAtSize = i });
                instance.RequestHandlerHooks.Add(new HTTPRequestHandlerHook("/static", TestHandlerStatic));
                instance.RequestHandlerHooks.Add(new HTTPRequestHandlerHook("/dynamic", TestHandlerDynamic));
                instance.StartListening(false);

                try
                {
                    TestResponse resp = GetTestResponse("POST /static HTTP/1.1\r\nHost: localhost\r\nContent-Length: 20\r\nContent-Type: application/x-www-form-urlencoded\r\n\r\nx=y&z=%20&zig=%3D%3d");
                    Assert.AreEqual("HTTP/1.1 200 OK", resp.Headers[0]);
                    Assert.IsTrue(resp.Headers.Contains("Content-Type: text/plain; charset=utf-8"));
                    Assert.IsTrue(resp.Headers.Contains("Content-Length: 37"));
                    Assert.IsTrue(resp.Headers.Contains("Accept-Ranges: bytes"));
                    Assert.AreEqual("\nPOST:\nx => \"y\"\nz => \" \"\nzig => \"==\"\n", resp.Content.FromUTF8());

                    resp = GetTestResponse("POST /dynamic HTTP/1.1\r\nHost: localhost\r\nContent-Length: 20\r\nContent-Type: application/x-www-form-urlencoded\r\n\r\nx=y&z=%20&zig=%3D%3d");
                    Assert.AreEqual("HTTP/1.1 200 OK", resp.Headers[0]);
                    Assert.IsTrue(resp.Headers.Contains("Content-Type: text/plain; charset=utf-8"));
                    Assert.IsFalse(resp.Headers.Any(x => x.ToLowerInvariant().StartsWith("content-length:")));
                    Assert.IsFalse(resp.Headers.Any(x => x.ToLowerInvariant().StartsWith("accept-ranges:")));
                    Assert.AreEqual("\nPOST:\nx => \"y\"\nz => \" \"\nzig => \"==\"\n", resp.Content.FromUTF8());

                    string postContent = "--abc\r\nContent-Disposition: form-data; name=\"x\"\r\n\r\ny\r\n--abc\r\nContent-Disposition: form-data; name=\"y\"; filename=\"z\"\r\nContent-Type: application/weirdo\r\n\r\n%3D%3d\r\n--abc--\r\n";
                    string expectedResponse = "\nPOST:\nx => \"y\"\n\nFiles:\ny => { application/weirdo, z, \"%3D%3d\" }\n";

                    resp = GetTestResponse("POST /static HTTP/1.1\r\nHost: localhost\r\nContent-Length: " + postContent.Length + "\r\nContent-Type: multipart/form-data; boundary=abc\r\n\r\n" + postContent);
                    Assert.AreEqual("HTTP/1.1 200 OK", resp.Headers[0]);
                    Assert.IsTrue(resp.Headers.Contains("Content-Type: text/plain; charset=utf-8"));
                    Assert.IsTrue(resp.Headers.Contains("Content-Length: " + expectedResponse.Length));
                    Assert.IsTrue(resp.Headers.Contains("Accept-Ranges: bytes"));
                    Assert.AreEqual(expectedResponse, resp.Content.FromUTF8());

                    resp = GetTestResponse("POST /dynamic HTTP/1.1\r\nHost: localhost\r\nContent-Length: " + postContent.Length + "\r\nContent-Type: multipart/form-data; boundary=abc\r\n\r\n" + postContent);
                    Assert.AreEqual("HTTP/1.1 200 OK", resp.Headers[0]);
                    Assert.IsTrue(resp.Headers.Contains("Content-Type: text/plain; charset=utf-8"));
                    Assert.IsFalse(resp.Headers.Any(x => x.StartsWith("Content-Length: ")));
                    Assert.IsFalse(resp.Headers.Any(x => x.StartsWith("Accept-Ranges: ")));
                    Assert.AreEqual(expectedResponse, resp.Content.FromUTF8());
                }
                finally
                {
                    instance.StopListening();
                }
            }
        }

        private IEnumerable<string> GenerateGetPostFilesOutput(HTTPRequest req)
        {
            if (req.GET.Count > 0)
                yield return "GET:\n";
            foreach (var kvp in req.GET)
                yield return kvp.Key + " => \"" + kvp.Value + "\"\n";
            if (req.GETArr.Count > 0)
                yield return "\nGETArr:\n";
            foreach (var kvp in req.GETArr)
                yield return kvp.Key + " => [" + kvp.Value.Select(x => "\"" + x + "\"").Join(", ") + "]\n";
            if (req.POST.Count > 0)
                yield return "\nPOST:\n";
            foreach (var kvp in req.POST)
                yield return kvp.Key + " => \"" + kvp.Value + "\"\n";
            if (req.POSTArr.Count > 0)
                yield return "\nPOSTArr:\n";
            foreach (var kvp in req.POSTArr)
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

        private HTTPResponse TestHandlerStatic(HTTPRequest req)
        {
            return new HTTPResponse
            {
                Status = HTTPStatusCode._200_OK,
                Headers = new HTTPResponseHeaders { ContentType = "text/plain; charset=utf-8" },
                Content = new MemoryStream(GenerateGetPostFilesOutput(req).Join("").ToUTF8())
            };
        }

        private HTTPResponse TestHandlerDynamic(HTTPRequest req)
        {
            return new HTTPResponse
            {
                Status = HTTPStatusCode._200_OK,
                Headers = new HTTPResponseHeaders { ContentType = "text/plain; charset=utf-8" },
                Content = new DynamicContentStream(GenerateGetPostFilesOutput(req))
            };
        }

        private HTTPResponse TestHandler64KFile(HTTPRequest req)
        {
            byte[] largeFile = new byte[65536];
            for (int i = 0; i < 65536; i++)
                largeFile[i] = (byte) (i % 256);
            return new HTTPResponse
            {
                Status = HTTPStatusCode._200_OK,
                Headers = new HTTPResponseHeaders { ContentType = "application/octet-stream" },
                Content = new MemoryStream(largeFile)
            };
        }
    }
}
