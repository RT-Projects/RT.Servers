using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using RT.TagSoup;
using RT.Util.Streams;
using RT.Util.ExtensionMethods;

namespace RT.Servers
{
    /// <summary>
    /// Encapsulates all supported HTTP response headers. A request handler can set these
    /// appropriately to cause the server to emit the required headers. See Remarks
    /// for a list of headers which are set by default.
    /// </summary>
    /// <remarks>
    /// By default, ContentType is set to "text/html; charset=utf-8".
    /// </remarks>
    public class HttpResponseHeaders
    {

#pragma warning disable 1591    // Missing XML comment for publicly visible type or member

        public HttpAcceptRanges AcceptRanges;
        public int? Age; // in seconds
        public string[] Allow;  // usually: { "GET", "HEAD", "POST" }
        public HttpCacheControl CacheControl;
        public HttpConnection Connection;
        public HttpContentEncoding ContentEncoding;
        public string ContentLanguage;
        public long? ContentLength;
        public HttpContentDisposition ContentDisposition;
        public string ContentMD5;
        public HttpContentRange? ContentRange;
        public string ContentType = "text/html; charset=utf-8";
        public DateTime? Date;
        public string ETag;
        public DateTime? LastModified;
        public string Location; // used in redirection
        public string Server;
        public List<Cookie> SetCookie;
        public HttpTransferEncoding TransferEncoding;

#pragma warning restore 1591    // Missing XML comment for publicly visible type or member

        /// <summary>
        /// Returns the HTTP-compliant ASCII representation of all response headers that have been set.
        /// </summary>
        /// <returns>The HTTP-compliant ASCII representation of all response headers that have been set.</returns>
        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            if (AcceptRanges != HttpAcceptRanges.None)
                b.Append("Accept-Ranges: bytes\r\n");
            if (Age != null)
                b.Append("Age: " + Age.Value + "\r\n");
            if (Allow != null)
                b.Append("Allow: " + string.Join(", ", Allow));
            if (CacheControl.State != HttpCacheControlState.None)
            {
                b.Append("Cache-Control: ");
                if (CacheControl.State == HttpCacheControlState.MaxAge && CacheControl.IntParameter != null)
                    b.Append("max-age=" + CacheControl.IntParameter.Value);
                else if (CacheControl.State == HttpCacheControlState.MaxStale && CacheControl.IntParameter != null)
                    b.Append("max-stale=" + CacheControl.IntParameter.Value);
                else if (CacheControl.State == HttpCacheControlState.MaxStale)
                    b.Append("max-stale");
                else if (CacheControl.State == HttpCacheControlState.MinFresh && CacheControl.IntParameter != null)
                    b.Append("min-fresh=" + CacheControl.IntParameter.Value);
                else if (CacheControl.State == HttpCacheControlState.MustRevalidate)
                    b.Append("must-revalidate");
                else if (CacheControl.State == HttpCacheControlState.NoCache)
                    b.Append("no-cache");
                else if (CacheControl.State == HttpCacheControlState.NoStore)
                    b.Append("no-store");
                else if (CacheControl.State == HttpCacheControlState.NoTransform)
                    b.Append("no-transform");
                else if (CacheControl.State == HttpCacheControlState.OnlyIfCached)
                    b.Append("only-if-cached");
                else if (CacheControl.State == HttpCacheControlState.Private && CacheControl.StringParameter != null)
                    b.Append("private=\"" + CacheControl.StringParameter + "\"");
                else if (CacheControl.State == HttpCacheControlState.ProxyRevalidate)
                    b.Append("proxy-revalidate");
                else if (CacheControl.State == HttpCacheControlState.Public)
                    b.Append("public");
                else if (CacheControl.State == HttpCacheControlState.SMaxAge && CacheControl.IntParameter != null)
                    b.Append("s-maxage=" + CacheControl.IntParameter.Value);
                b.Append("\r\n");
            }
            if (Connection != HttpConnection.None)
            {
                if (Connection == HttpConnection.Close)
                    b.Append("Connection: close\r\n");
                else
                    b.Append("Connection: keep-alive\r\n");
            }
            if (ContentEncoding != HttpContentEncoding.Identity)
                b.Append("Content-Encoding: " + ContentEncoding.ToString().ToLowerInvariant() + "\r\n");
            if (ContentLanguage != null)
                b.Append("Content-Language: " + ContentLanguage + "\r\n");
            if (ContentLength != null)
                b.Append("Content-Length: " + ContentLength.Value + "\r\n");
            if (ContentDisposition.Mode != HttpContentDispositionMode.None && ContentDisposition.Filename != null)
                b.Append("Content-Disposition: attachment; filename=" + ContentDisposition.Filename + "\r\n");
            if (ContentMD5 != null)
                b.Append("Content-MD5: " + ContentMD5 + "\r\n");
            if (ContentRange != null)
                b.Append("Content-Range: bytes " + ContentRange.Value.From + "-" + ContentRange.Value.To + "/" + ContentRange.Value.Total + "\r\n");
            if (ContentType != null)
                b.Append("Content-Type: " + ContentType + "\r\n");
            if (Date != null)
                b.Append("Date: " + Date.Value.ToString("r" /* = RFC1123 */) + "\r\n");
            if (ETag != null)
                b.Append("ETag: " + ETag + "\r\n");
            if (LastModified != null)
                b.Append("Last-Modified: " + LastModified.Value.ToString("r" /* = RFC1123 */) + "\r\n");
            if (Location != null)
                b.Append("Location: " + Location + "\r\n");
            if (Server != null)
                b.Append("Server: " + Server + "\r\n");
            if (SetCookie != null)
            {
                foreach (Cookie c in SetCookie)
                {
                    b.Append("Set-Cookie: " + c.Name + "=" + c.Value);
                    if (c.Domain != null)
                        b.Append("; domain=" + c.Domain);
                    if (c.Path != null)
                        b.Append("; path=" + c.Path);
                    if (c.Expires != null)
                        b.Append("; expires=" + c.Expires.Value.ToString("r"));
                    b.Append(c.HttpOnly ? "; httponly\r\n" : "\r\n");
                }
            }
            if (TransferEncoding != HttpTransferEncoding.None)
                b.Append("Transfer-Encoding: chunked\r\n");
            return b.ToString();
        }
    }

    /// <summary>
    /// Encapsulates an HTTP response, to be sent by <see cref="HttpServer"/> to the HTTP client that sent the original request.
    /// A request handler must return an HttpResponse object to the <see cref="HttpServer"/> when handling a request.
    /// </summary>
    public class HttpResponse
    {
        /// <summary>
        /// The HTTP status code. For example, 200 OK, 404 Not Found, 500 Internal Server Error.
        /// Default is 200 OK.
        /// </summary>
        public HttpStatusCode Status = HttpStatusCode._200_OK;

        /// <summary>
        /// The HTTP response headers which are to be sent back to the HTTP client as part of this HTTP response.
        /// If not set or modified, will default to a standard set of headers - see <see cref="HttpResponseHeaders"/>.
        /// </summary>
        public HttpResponseHeaders Headers = new HttpResponseHeaders();

        /// <summary>
        /// A stream object providing read access to the content returned. For static files, use <see cref="FileStream"/>.
        /// For objects cached in memory, use <see cref="MemoryStream"/>.
        /// For dynamic websites, consider using <see cref="RT.Util.Streams.DynamicContentStream"/>.
        /// </summary>
        public Stream Content;

        /// <summary>
        /// Internal field for <see cref="HttpServer"/> to access the original request that this is the response for.
        /// </summary>
        internal HttpRequest OriginalRequest;

        /// <summary>
        /// Default constructor which does not initialise the <see cref="Content"/>. Headers are
        /// always created and set to default values.
        /// </summary>
        public HttpResponse()
        {
        }

        /// <summary>
        /// Initialises <see cref="Content"/> to serve the specified enumerable using a
        /// <see cref="DynamicContentStream"/>. Headers are created and set to default values.
        /// </summary>
        public HttpResponse(IEnumerable<string> enumerable)
        {
            Content = new DynamicContentStream(enumerable);
        }

        /// <summary>
        /// Initialises <see cref="Content"/> to serve the specified string by converting it to UTF-8
        /// and then using a <see cref="MemoryStream"/>. Headers are created and set to default values.
        /// </summary>
        public HttpResponse(string content)
        {
            Content = new MemoryStream(content.ToUtf8());
        }

        /// <summary>
        /// Initialises <see cref="Content"/> to serve the specified HTML using a
        /// <see cref="DynamicContentStream"/>. Headers are created and set to default values.
        /// </summary>
        public HttpResponse(Tag html)
        {
            Content = new DynamicContentStream(html.ToEnumerable());
        }

        /// <summary>
        /// Initialises <see cref="Content"/> to serve the specified enumerable using a
        /// <see cref="DynamicContentStream"/>.
        /// </summary>
        public HttpResponse(IEnumerable<string> enumerable, HttpResponseHeaders headers)
        {
            Content = new DynamicContentStream(enumerable);
            Headers = headers;
        }

        /// <summary>
        /// Initialises <see cref="Content"/> to serve the specified string by converting it to UTF-8
        /// and then using a <see cref="MemoryStream"/>.
        /// </summary>
        public HttpResponse(string content, HttpResponseHeaders headers)
        {
            Content = new MemoryStream(content.ToUtf8());
            Headers = headers;
        }

        /// <summary>
        /// Initialises <see cref="Content"/> to serve the specified HTML using a
        /// <see cref="DynamicContentStream"/>.
        /// </summary>
        public HttpResponse(Tag html, HttpResponseHeaders headers)
        {
            Content = new DynamicContentStream(html.ToEnumerable());
            Headers = headers;
        }
    }
}
