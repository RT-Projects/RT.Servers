using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using RT.Util.ExtensionMethods;
using RT.Util.Streams;

namespace RT.Servers.Tests
{
    [TestFixture]
    public sealed class RequestParseTests
    {
        [Test]
        public void TestParseGet()
        {
            string testQueryString1 = "apple=bravery&cooking=dinner&elephant=foxtrot&ghost=hangman";
            string testQueryString2 = "apple=" + "!@#$".UrlEscape() + "&cooking=" + "%^&*".UrlEscape() + "&elephant=" + "()=+".UrlEscape() + "&ghost=" + "абвгд".UrlEscape();
            string testQueryString3 = "apple[]=" + "!@#$".UrlEscape() + "&apple%5b%5d=" + "%^&*".UrlEscape() + "&apple%5b]=" + "()=+".UrlEscape() + "&ghost[%5d=" + "абвгд".UrlEscape();

            for (int chunksize = 1; chunksize < Math.Max(Math.Max(testQueryString1.Length, testQueryString2.Length), testQueryString3.Length); chunksize++)
            {
                NameValuesCollection<string> dic;
                using (var reader = new StreamReader(new SlowStream(new MemoryStream(Encoding.UTF8.GetBytes(testQueryString1)), chunksize)))
                    dic = HttpHelper.ParseQueryValueParameters(reader).ToNameValuesCollection();
                Assert.AreEqual(4, dic.Count);
                Assert.IsTrue(dic.ContainsKey("apple"));
                Assert.IsTrue(dic.ContainsKey("cooking"));
                Assert.IsTrue(dic.ContainsKey("elephant"));
                Assert.IsTrue(dic.ContainsKey("ghost"));
                Assert.AreEqual("bravery", dic["apple"].Value);
                Assert.AreEqual("dinner", dic["cooking"].Value);
                Assert.AreEqual("foxtrot", dic["elephant"].Value);
                Assert.AreEqual("hangman", dic["ghost"].Value);

                using (var reader = new StreamReader(new SlowStream(new MemoryStream(Encoding.UTF8.GetBytes(testQueryString2)), chunksize)))
                    dic = HttpHelper.ParseQueryValueParameters(reader).ToNameValuesCollection();
                Assert.AreEqual(4, dic.Count);
                Assert.IsTrue(dic.ContainsKey("apple"));
                Assert.IsTrue(dic.ContainsKey("cooking"));
                Assert.IsTrue(dic.ContainsKey("elephant"));
                Assert.IsTrue(dic.ContainsKey("ghost"));
                Assert.AreEqual("!@#$", dic["apple"].Value);
                Assert.AreEqual("%^&*", dic["cooking"].Value);
                Assert.AreEqual("()=+", dic["elephant"].Value);
                Assert.AreEqual("абвгд", dic["ghost"].Value);

                using (var reader = new StreamReader(new SlowStream(new MemoryStream(Encoding.UTF8.GetBytes(testQueryString3)), chunksize)))
                    dic = HttpHelper.ParseQueryValueParameters(reader).ToNameValuesCollection();
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
                    Method = HttpMethod.Post
                };
                r.Url.SetRestUrl("/");
                r.Url.SetHost("example.com");

                using (Stream f = new SlowStream(new MemoryStream(testCase), cs))
                {
                    r.ParsePostBody(f, directoryNotToBeCreated);
                    var gets = r.Url.Query.ToList();
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
    }
}
