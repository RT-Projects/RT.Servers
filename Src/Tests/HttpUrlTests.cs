using NUnit.Framework;

namespace RT.Servers.Tests
{
    [TestFixture]
    public sealed class HttpUrlTests
    {
        [Test]
        public void TestUrlModification()
        {
            IHttpUrl url;

            // Test the odd preservation of the question mark
            Assert.AreEqual("http://example.com/foo?", new HttpUrl("example.com", "/foo?").ToFull());
            Assert.AreEqual("http://example.com/foo", new HttpUrl("example.com", "/foo?").WithoutQuery("blah").ToFull()); // dubious: could keep the question mark if there was no such query name
            Assert.AreEqual("http://example.com/foo", new HttpUrl("example.com", "/foo").WithQuery("blah", "s").WithoutQuery("blah").ToFull());
            Assert.AreEqual("http://example.com/foo", new HttpUrl("example.com", "/foo?").WithQuery("blah", "s").WithoutQuery("blah").ToFull());

            // WithQuery Single + Blank URL
            url = new HttpUrl("example.com", "/foo").WithQuery("blah", "thingy");
            Assert.IsTrue(url.HasQuery);
            Assert.AreEqual("http://example.com/foo?blah=thingy", url.ToFull());

            url = new HttpUrl("example.com", "/foo").WithQuery("blah", (string) null);
            Assert.IsFalse(url.HasQuery);
            Assert.AreEqual("http://example.com/foo", url.ToFull());

            // WithQuery Single + one other argument
            url = new HttpUrl("example.com", "/foo?q=s").WithQuery("blah", "thingy");
            Assert.IsTrue(url.HasQuery);
            Assert.AreEqual("http://example.com/foo?q=s&blah=thingy", url.ToFull());

            url = new HttpUrl("example.com", "/foo?q=s").WithQuery("blah", (string) null);
            Assert.IsTrue(url.HasQuery);
            Assert.AreEqual("http://example.com/foo?q=s", url.ToFull());

            // WithQuery Single + just the same argument
            url = new HttpUrl("example.com", "/foo?blah=s").WithQuery("blah", "thingy");
            Assert.IsTrue(url.HasQuery);
            Assert.AreEqual("http://example.com/foo?blah=thingy", url.ToFull());

            url = new HttpUrl("example.com", "/foo?blah=s").WithQuery("blah", (string) null);
            Assert.IsFalse(url.HasQuery);
            Assert.AreEqual("http://example.com/foo", url.ToFull());
        }
    }
}
