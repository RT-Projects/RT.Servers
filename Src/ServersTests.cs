using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using System.IO;
using Servers;
using RT.Util.Streams;

namespace ServersTests
{
    [TestFixture]
    public class ServersTestSuite
    {
        [Test]
        public void TestParsePOST()
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
                SlowStream.ChunkSize = cs;
                Stream f = new SlowStream(new MemoryStream(TestCase));
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
    }
}
