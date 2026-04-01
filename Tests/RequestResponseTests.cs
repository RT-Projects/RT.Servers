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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RT.Util;
using RT.Util.ExtensionMethods;

namespace RT.Servers.Tests;

[TestClass]
public sealed class RequestResponseTests
{
    private static void testRequest(int port, string testName, int storeFileUploadInFileAtSize, string request, Action<string[], byte[]> verify)
    {
        var instance = new HttpServer(port, new HttpServerOptions { StoreFileUploadInFileAtSize = storeFileUploadInFileAtSize })
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

            var requestBytes = request.ToUtf8();
            for (var chunkSize = 0; chunkSize <= requestBytes.Length; chunkSize += Rnd.Next(2, 64).ClipMax(requestBytes.Length - chunkSize).ClipMin(2))
            {
                if (chunkSize == 0)
                    continue;
                Console.WriteLine($"{testName}; Port={port}; SFUIFAS {storeFileUploadInFileAtSize}; length {requestBytes.Length}; chunk size {chunkSize}");
                var cl = new TcpClient();
                cl.Connect("localhost", port);
                cl.ReceiveTimeout = 1000; // 1 sec
                Socket sck = cl.Client;
                for (var j = 0; j < requestBytes.Length; j += chunkSize)
                {
                    sck.Send(requestBytes, j, Math.Min(requestBytes.Length - j, chunkSize), SocketFlags.None);
                    Thread.Sleep(25);
                }
                using var response = new MemoryStream();
                var b = new byte[65536];
                var bytesRead = sck.Receive(b);
                Assert.IsGreaterThan(0, bytesRead);
                while (bytesRead > 0)
                {
                    response.Write(b, 0, bytesRead);
                    bytesRead = sck.Receive(b);
                }
                var content = response.ToArray();
                var pos = content.IndexOfSubarray("\r\n\r\n"u8.ToArray(), 0, content.Length);
                Assert.IsGreaterThan(-1, pos);

                var headersRaw = content.Subarray(0, pos);
                content = content.Subarray(pos + 4);

                var headers = headersRaw.FromUtf8().Split(["\r\n"], StringSplitOptions.None);
                verify?.Invoke(headers, content);
            }
        }
        finally
        {
            instance.StopListening();
        }
    }

    [TestMethod]
    public void TestBasicRequestHandlingGet_01()
    {
        testRequest(TestHelpers.Port + 20, "GET test #1", 1024 * 1024, "GET / HTTP/1.1\r\nHost: localhost\r\n\r\n", (headers, content) =>
        {
            Assert.AreEqual("HTTP/1.1 404 Not Found", headers[0]);
            Assert.Contains("Content-Type: text/html; charset=utf-8", headers);
            Assert.Contains(x => x.StartsWith("Content-Length: "), headers);
            Assert.Contains("404", content.FromUtf8());
        });
    }

    [TestMethod]
    public void TestBasicRequestHandlingGet_02()
    {
        testRequest(TestHelpers.Port + 21, "GET test #2", 1024 * 1024, "GET /static?x=y&z=%20&zig=%3D%3d HTTP/1.1\r\nHost: localhost\r\n\r\n", (headers, content) =>
        {
            Assert.AreEqual("HTTP/1.1 200 OK", headers[0]);
            Assert.Contains("Content-Type: text/plain; charset=utf-8", headers);
            Assert.Contains("Content-Length: 41", headers);
            Assert.AreEqual("GET:\nx => [\"y\"]\nz => [\" \"]\nzig => [\"==\"]\n", content.FromUtf8());
        });
    }

    [TestMethod]
    public void TestBasicRequestHandlingGet_03()
    {
        testRequest(TestHelpers.Port + 22, "GET test #3", 1024 * 1024, "GET /static?x[]=1&x%5B%5D=%20&x%5b%5d=%3D%3d HTTP/1.1\r\nHost: localhost\r\n\r\n", (headers, content) =>
        {
            Assert.AreEqual("HTTP/1.1 200 OK", headers[0]);
            Assert.Contains("Content-Type: text/plain; charset=utf-8", headers);
            Assert.Contains("Content-Length: 29", headers);
            Assert.AreEqual("GET:\nx[] => [\"1\", \" \", \"==\"]\n", content.FromUtf8());
        });
    }

    [TestMethod]
    public void TestBasicRequestHandlingGet_04()
    {
        testRequest(TestHelpers.Port + 23, "GET test #4", 1024 * 1024, "GET /dynamic?x=y&z=%20&zig=%3D%3d HTTP/1.1\r\nHost: localhost\r\n\r\n", (headers, content) =>
        {
            Assert.AreEqual("HTTP/1.1 200 OK", headers[0]);
            Assert.Contains("Content-Type: text/plain; charset=utf-8", headers);
            Assert.DoesNotContain(x => x.StartsWith("content-length:", StringComparison.InvariantCultureIgnoreCase), headers);
            Assert.DoesNotContain(x => x.StartsWith("accept-ranges:", StringComparison.InvariantCultureIgnoreCase), headers);
            Assert.AreEqual("GET:\nx => [\"y\"]\nz => [\" \"]\nzig => [\"==\"]\n", content.FromUtf8());
        });
    }

    [TestMethod]
    public void TestBasicRequestHandlingGet_05()
    {
        testRequest(TestHelpers.Port + 24, "GET test #5", 1024 * 1024, "GET /64kfile HTTP/1.1\r\nHost: localhost\r\n\r\n", (headers, content) =>
        {
            Assert.AreEqual("HTTP/1.1 200 OK", headers[0]);
            Assert.Contains("Content-Type: application/octet-stream", headers);
            Assert.Contains("Content-Length: 65536", headers);
            Assert.Contains("Accept-Ranges: bytes", headers);
            Assert.HasCount(65536, content);
            for (var i = 0; i < content.Length; i++)
                Assert.AreEqual(content[i], (byte) (i % 256));
        });
    }

    [TestMethod]
    public void TestBasicRequestHandlingGet_06()
    {
        testRequest(TestHelpers.Port + 25, "GET test #6", 1024 * 1024, "GET /64kfile HTTP/1.1\r\nHost: localhost\r\nRange: bytes=23459-38274\r\n\r\n", (headers, content) =>
        {
            Assert.AreEqual("HTTP/1.1 206 Partial Content", headers[0]);
            Assert.Contains("Accept-Ranges: bytes", headers);
            Assert.Contains("Content-Range: bytes 23459-38274/65536", headers);
            Assert.Contains("Content-Type: application/octet-stream", headers);
            Assert.Contains("Content-Length: 14816", headers);
            for (var i = 0; i < content.Length; i++)
                Assert.AreEqual((byte) ((163 + i) % 256), content[i]);
        });
    }

    [TestMethod]
    public void TestBasicRequestHandlingGet_07()
    {
        testRequest(TestHelpers.Port + 26, "GET test #7", 1024 * 1024, "GET /64kfile HTTP/1.1\r\nHost: localhost\r\nRange: bytes=65-65,67-67\r\n\r\n", (headers, content) =>
        {
            Assert.AreEqual("HTTP/1.1 206 Partial Content", headers[0]);
            Assert.Contains("Accept-Ranges: bytes", headers);
            Assert.Contains(x => Regex.IsMatch(x, @"^Content-Type: multipart/byteranges; boundary=[0-9A-F]+$"), headers);
            var boundary = headers.First(x => Regex.IsMatch(x, @"^Content-Type: multipart/byteranges; boundary=[0-9A-F]+$")).Substring(45);
            Assert.Contains("Content-Length: 284", headers);
            var expectedContent = ("--" + boundary + "\r\nContent-Range: bytes 65-65/65536\r\n\r\nA\r\n--" + boundary + "\r\nContent-Range: bytes 67-67/65536\r\n\r\nC\r\n--" + boundary + "--\r\n").ToUtf8();
            Assert.HasCount(expectedContent.Length, content);
            for (var i = 0; i < expectedContent.Length; i++)
                Assert.AreEqual(expectedContent[i], content[i]);
        });
    }

    [TestMethod]
    public void TestBasicRequestHandlingGet_08()
    {
        testRequest(TestHelpers.Port + 27, "GET test #8", 1024 * 1024, "GET /64kfile HTTP/1.1\r\nHost: localhost\r\nAccept-Encoding: gzip\r\n\r\n", (headers, content) =>
        {
            Assert.AreEqual("HTTP/1.1 200 OK", headers[0]);
            Assert.Contains("Accept-Ranges: bytes", headers);
            Assert.Contains("Content-Type: application/octet-stream", headers);
            Assert.Contains("Content-Encoding: gzip", headers);
            Assert.Contains(h => h.StartsWith("Content-Length:"), headers);
            var gz = new GZipStream(new MemoryStream(content), CompressionMode.Decompress);
            for (var i = 0; i < 65536; i++)
                Assert.AreEqual(i % 256, gz.ReadByte());
            Assert.AreEqual(-1, gz.ReadByte());
        });
    }

    [TestMethod]
    public void TestBasicRequestHandlingGet_09()
    {
        testRequest(TestHelpers.Port + 28, "GET test #9", 1024 * 1024, "GET /dynamic HTTP/1.1\r\n\r\n", (headers, content) => Assert.AreEqual("HTTP/1.1 400 Bad Request", headers[0]));
    }

    [TestMethod]
    public void TestBasicRequestHandlingGet_10()
    {
        testRequest(TestHelpers.Port + 29, "GET test #10", 1024 * 1024, "INVALID /request HTTP/1.1\r\n\r\n", (headers, content) => Assert.AreEqual("HTTP/1.1 400 Bad Request", headers[0]));
    }

    [TestMethod]
    public void TestBasicRequestHandlingGet_11()
    {
        testRequest(TestHelpers.Port + 30, "GET test #11", 1024 * 1024, "GET  HTTP/1.1\r\n\r\n", (headers, content) => Assert.AreEqual("HTTP/1.1 400 Bad Request", headers[0]));
    }

    [TestMethod]
    public void TestBasicRequestHandlingGet_12()
    {
        testRequest(TestHelpers.Port + 31, "GET test #12", 1024 * 1024, "!\r\n\r\n", (headers, content) => Assert.AreEqual("HTTP/1.1 400 Bad Request", headers[0]));
    }

    [TestMethod]
    public void TestBasicRequestHandlingPost_1()
    {
        foreach (var storeFileUploadInFileAtSize in new[] { 5, 1024 })
            testRequest(TestHelpers.Port + 32, "POST test #1", storeFileUploadInFileAtSize, "POST /static HTTP/1.1\r\nHost: localhost\r\nContent-Length: 48\r\nContent-Type: application/x-www-form-urlencoded\r\n\r\nx=y&z=%20&zig=%3D%3d&a[]=1&a%5B%5D=2&%61%5b%5d=3", (headers, content) =>
            {
                Assert.AreEqual("HTTP/1.1 200 OK", headers[0]);
                Assert.Contains("Content-Type: text/plain; charset=utf-8", headers);
                Assert.Contains("Content-Length: 66", headers);
                Assert.AreEqual("\nPOST:\nx => [\"y\"]\nz => [\" \"]\nzig => [\"==\"]\na[] => [\"1\", \"2\", \"3\"]\n", content.FromUtf8());
            });
    }

    [TestMethod]
    public void TestBasicRequestHandlingPost_2()
    {
        foreach (var storeFileUploadInFileAtSize in new[] { 5, 1024 })
            testRequest(TestHelpers.Port + 33, "POST test #2", storeFileUploadInFileAtSize, "POST /dynamic HTTP/1.1\r\nHost: localhost\r\nContent-Length: 20\r\nContent-Type: application/x-www-form-urlencoded\r\n\r\nx=y&z=%20&zig=%3D%3d", (headers, content) =>
            {
                Assert.AreEqual("HTTP/1.1 200 OK", headers[0]);
                Assert.Contains("Content-Type: text/plain; charset=utf-8", headers);
                Assert.DoesNotContain(x => x.StartsWith("content-length:", StringComparison.InvariantCultureIgnoreCase), headers);
                Assert.DoesNotContain(x => x.StartsWith("accept-ranges:", StringComparison.InvariantCultureIgnoreCase), headers);
                Assert.AreEqual("\nPOST:\nx => [\"y\"]\nz => [\" \"]\nzig => [\"==\"]\n", content.FromUtf8());
            });
    }

    [TestMethod]
    public void TestBasicRequestHandlingPost_3()
    {
        foreach (var storeFileUploadInFileAtSize in new[] { 5, 1024 })
        {
            var postContent = "--abc\r\nContent-Disposition: form-data; name=\"x\"\r\n\r\ny\r\n--abc\r\nContent-Disposition: form-data; name=\"z\"\r\n\r\n%3D%3d\r\n--abc\r\nContent-Disposition: form-data; name=\"y\"; filename=\"z\"\r\nContent-Type: application/weirdo\r\n\r\n%3D%3d\r\n--abc--\r\n";
            var expectedResponse = "\nPOST:\nx => [\"y\"]\nz => [\"%3D%3d\"]\n\nFiles:\ny => { application/weirdo, z, \"%3D%3d\" (" + (storeFileUploadInFileAtSize < 6 ? "localfile" : "data") + ") }\n";

            testRequest(TestHelpers.Port + 34, "POST test #3", storeFileUploadInFileAtSize, "POST /static HTTP/1.1\r\nHost: localhost\r\nContent-Length: " + postContent.Length + "\r\nContent-Type: multipart/form-data; boundary=abc\r\n\r\n" + postContent, (headers, content) =>
            {
                Assert.AreEqual("HTTP/1.1 200 OK", headers[0]);
                Assert.Contains("Content-Type: text/plain; charset=utf-8", headers);
                Assert.AreEqual(expectedResponse, content.FromUtf8());
                Assert.IsTrue(headers.Contains("Content-Length: " + expectedResponse.Length));
            });
        }
    }

    [TestMethod]
    public void TestBasicRequestHandlingPost_4()
    {
        foreach (var storeFileUploadInFileAtSize in new[] { 5, 1024 })
        {
            var postContent = "--abc\r\nContent-Disposition: form-data; name=\"x\"\r\n\r\ny\r\n--abc\r\nContent-Disposition: form-data; name=\"z\"\r\n\r\n%3D%3d\r\n--abc\r\nContent-Disposition: form-data; name=\"y\"; filename=\"z\"\r\nContent-Type: application/weirdo\r\n\r\n%3D%3d\r\n--abc--\r\n";
            var expectedResponse = "\nPOST:\nx => [\"y\"]\nz => [\"%3D%3d\"]\n\nFiles:\ny => { application/weirdo, z, \"%3D%3d\" (" + (storeFileUploadInFileAtSize < 6 ? "localfile" : "data") + ") }\n";

            testRequest(TestHelpers.Port + 35, "POST test #4", storeFileUploadInFileAtSize, "POST /dynamic HTTP/1.1\r\nHost: localhost\r\nContent-Length: " + postContent.Length + "\r\nContent-Type: multipart/form-data; boundary=abc\r\n\r\n" + postContent, (headers, content) =>
            {
                Assert.AreEqual("HTTP/1.1 200 OK", headers[0]);
                Assert.Contains("Content-Type: text/plain; charset=utf-8", headers);
                Assert.DoesNotContain(x => x.StartsWith("Content-Length: "), headers);
                Assert.DoesNotContain(x => x.StartsWith("Accept-Ranges: "), headers);
                Assert.AreEqual(expectedResponse, content.FromUtf8());
            });
        }
    }

    [TestMethod]
    public void TestBasicRequestHandlingPost_5()
    {
        foreach (var storeFileUploadInFileAtSize in new[] { 5, 1024 })
        {
            // Test that the server doesn't crash if a field name is missing
            var postContent = "--abc\r\nContent-Disposition: form-data; name=\"x\"\r\n\r\n\r\n--abc\r\nContent-Disposition: form-data\r\n\r\n%3D%3d\r\n--abc\r\nContent-Disposition: form-data; filename=\"z\"\r\nContent-Type: application/weirdo\r\n\r\n%3D%3d\r\n--abc--\r\n";
            var expectedResponse = "\nPOST:\nx => [\"\"]\n";

            testRequest(TestHelpers.Port + 36, "POST test #5", storeFileUploadInFileAtSize, "POST /static HTTP/1.1\r\nHost: localhost\r\nContent-Length: " + postContent.Length + "\r\nContent-Type: multipart/form-data; boundary=abc\r\n\r\n" + postContent, (headers, content) =>
            {
                Assert.AreEqual("HTTP/1.1 200 OK", headers[0]);
                Assert.Contains("Content-Type: text/plain; charset=utf-8", headers);
                Assert.Contains(x => x.StartsWith("Content-Length: "), headers);
                Assert.DoesNotContain(x => x.StartsWith("Accept-Ranges: "), headers);
                Assert.AreEqual(expectedResponse, content.FromUtf8());
            });
        }
    }

    [TestMethod]
    public void TestBasicRequestHandlingPost_6()
    {
        foreach (var storeFileUploadInFileAtSize in new[] { 5, 1024 })
        {
            var postContent = "--abc\r\nContent-Disposition: form-data; name=\"x\"\r\n\r\n\r\n--abc\r\nContent-Disposition: form-data\r\n\r\n%3D%3d\r\n--abc\r\nContent-Disposition: form-data; filename=\"z\"\r\nContent-Type: application/weirdo\r\n\r\n%3D%3d\r\n--abc--\r\n";
            var expectedResponse = "\nPOST:\nx => [\"\"]\n";

            testRequest(TestHelpers.Port + 37, "POST test #6", storeFileUploadInFileAtSize, "POST /dynamic HTTP/1.1\r\nHost: localhost\r\nContent-Length: " + postContent.Length + "\r\nContent-Type: multipart/form-data; boundary=abc\r\n\r\n" + postContent, (headers, content) =>
            {
                Assert.AreEqual("HTTP/1.1 200 OK", headers[0]);
                Assert.Contains("Content-Type: text/plain; charset=utf-8", headers);
                Assert.DoesNotContain(x => x.StartsWith("Content-Length: "), headers);
                Assert.DoesNotContain(x => x.StartsWith("Accept-Ranges: "), headers);
                Assert.AreEqual(expectedResponse, content.FromUtf8());
            });
        }
    }

    [TestMethod]
    public void TestKeepaliveAndChunked()
    {
        var instance = new HttpServer(TestHelpers.Port + 7) { Handler = handlerDynamic };
        try
        {
            instance.StartListening();
            Thread.Sleep(100);
            Assert.AreEqual(0, instance.Stats.ActiveHandlers);
            Assert.AreEqual(0, instance.Stats.KeepAliveHandlers);

            var cl = new TcpClient();
            cl.Connect("localhost", TestHelpers.Port + 7);
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

    private static void keepaliveAndChunkedPrivate(Socket sck)
    {
        sck.Send("GET /dynamic?aktion=list&showonly=scheduled&limitStart=0&filtermask_t=&filtermask_g=&filtermask_s=&size_max=*&size_min=*&lang=&archivemonth=200709&format_wmv=true&format_avi=true&format_hq=&format_mp4=&lang=&archivemonth=200709&format_wmv=true&format_avi=true&format_hq=&format_mp4=&orderby=time_desc&showonly=recordings HTTP/1.1\r\nHost: localhost\r\nConnection: keep-alive\r\n\r\n".ToUtf8());

        var b = new byte[65536];
        var bytesRead = sck.Receive(b);
        Assert.IsGreaterThan(0, bytesRead);
        var response = Encoding.UTF8.GetString(b, 0, bytesRead);
        while (!response.Contains("\r\n\r\n"))
        {
            bytesRead = sck.Receive(b);
            Assert.IsGreaterThan(0, bytesRead);
            response += Encoding.UTF8.GetString(b, 0, bytesRead);
        }
        Assert.Contains("\r\n\r\n", response);
        var pos = response.IndexOf("\r\n\r\n");
        var headers = response.Split(["\r\n"], StringSplitOptions.None);
        Assert.IsTrue(headers.Contains("Connection: keep-alive"));
        Assert.IsTrue(headers.Contains("Transfer-Encoding: chunked"));
        Assert.IsTrue(headers.Contains("Content-Type: text/plain; charset=utf-8"));

        response = response.Substring(pos + 4);
        while (!response.EndsWith("\r\n0\r\n\r\n"))
        {
            bytesRead = sck.Receive(b);
            Assert.IsGreaterThan(0, bytesRead);
            response += Encoding.UTF8.GetString(b, 0, bytesRead);
        }

        var reconstruct = "";
        int chunkLen;
        do
        {
            var m = Regex.Match(response, @"^([0-9a-fA-F]+)\r\n");
            Assert.IsTrue(m.Success);
            chunkLen = int.Parse(m.Groups[1].Value, NumberStyles.HexNumber);
            reconstruct += response.Substring(m.Length, chunkLen);
            Assert.AreEqual("\r\n", response.Substring(m.Length + chunkLen, 2));
            response = response.Substring(m.Length + chunkLen + 2);
        }
        while (chunkLen > 0);

        Assert.AreEqual("", response);
        Assert.AreEqual("GET:\naktion => [\"list\"]\nshowonly => [\"scheduled\", \"recordings\"]\nlimitStart => [\"0\"]\nfiltermask_t => [\"\"]\nfiltermask_g => [\"\"]\nfiltermask_s => [\"\"]\nsize_max => [\"*\"]\nsize_min => [\"*\"]\nlang => [\"\", \"\"]\narchivemonth => [\"200709\", \"200709\"]\nformat_wmv => [\"true\", \"true\"]\nformat_avi => [\"true\", \"true\"]\nformat_hq => [\"\", \"\"]\nformat_mp4 => [\"\", \"\"]\norderby => [\"time_desc\"]\n", reconstruct);
    }

    private static IEnumerable<string> generateGetPostFilesOutput(HttpRequest req)
    {
        if (req.Url.Query.Any())
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
            var fldLocalFilename = kvp.Value.GetType().GetField("_localFilename", BindingFlags.NonPublic | BindingFlags.Instance);
            yield return "\" (" + (fldLocalFilename == null ? "null" : fldLocalFilename.GetValue(kvp.Value) == null ? "" : "localfile");
            var fldData = kvp.Value.GetType().GetField("_data", BindingFlags.NonPublic | BindingFlags.Instance);
            yield return (fldData == null ? "null" : fldData.GetValue(kvp.Value) == null ? "" : "data") + ") }\n";
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
        var largeFile = new byte[65536];
        for (var i = 0; i < 65536; i++)
            largeFile[i] = (byte) (i % 256);
        return HttpResponse.Create(new MemoryStream(largeFile), "application/octet-stream");
    }
}
