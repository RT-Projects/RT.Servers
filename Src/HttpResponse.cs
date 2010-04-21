using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using RT.TagSoup;
using RT.Util.Streams;
using RT.Util.ExtensionMethods;
using System.Linq;

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
        public HttpCacheControl[] CacheControl;
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
        public DateTime? Expires;
        public DateTime? LastModified;
        public string Location; // used in redirection
        public string Pragma;
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
            if (CacheControl != null)
            {
                var coll = CacheControl.Where(c => c.State != HttpCacheControlState.None).SelectMany(c =>
                    c.State == HttpCacheControlState.MaxAge && c.IntParameter != null ? new[] { "max-age=" + c.IntParameter.Value } :
                    c.State == HttpCacheControlState.MaxStale && c.IntParameter != null ? new[] { "max-stale=" + c.IntParameter.Value } :
                    c.State == HttpCacheControlState.MaxStale ? new[] { "max-stale" } :
                    c.State == HttpCacheControlState.MinFresh && c.IntParameter != null ? new[] { "min-fresh=" + c.IntParameter.Value } :
                    c.State == HttpCacheControlState.MustRevalidate ? new[] { "must-revalidate" } :
                    c.State == HttpCacheControlState.NoCache ? new[] { "no-cache" } :
                    c.State == HttpCacheControlState.NoStore ? new[] { "no-store" } :
                    c.State == HttpCacheControlState.NoTransform ? new[] { "no-transform" } :
                    c.State == HttpCacheControlState.OnlyIfCached ? new[] { "only-if-cached" } :
                    c.State == HttpCacheControlState.PostCheck && c.IntParameter != null ? new[] { "post-check=" + c.IntParameter.Value } :
                    c.State == HttpCacheControlState.PreCheck && c.IntParameter != null ? new[] { "pre-check=" + c.IntParameter.Value } :
                    c.State == HttpCacheControlState.Private && c.StringParameter != null ? new[] { "private=\"" + c.StringParameter + "\"" } :
                    c.State == HttpCacheControlState.ProxyRevalidate ? new[] { "proxy-revalidate" } :
                    c.State == HttpCacheControlState.Public ? new[] { "public" } :
                    c.State == HttpCacheControlState.SMaxAge && c.IntParameter != null ? new[] { "s-maxage=" + c.IntParameter.Value } :
                    new string[0]
                );
                if (coll.Any())
                {
                    b.Append("Cache-Control: ");
                    b.Append(coll.JoinString(", "));
                    b.Append("\r\n");
                }
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
            if (Expires != null)
                b.Append("Expires: " + Expires.Value.ToString("r" /* = RFC1123 */) + "\r\n");
            if (LastModified != null)
                b.Append("Last-Modified: " + LastModified.Value.ToString("r" /* = RFC1123 */) + "\r\n");
            if (Location != null)
                b.Append("Location: " + Location + "\r\n");
            if (Pragma != null)
                b.Append("Pragma: " + Pragma + "\r\n");
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
        /// Specifies whether gzip should be used.
        /// </summary>
        public UseGzipOption UseGzip = UseGzipOption.AutoDetect;

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
        /// <see cref="DynamicContentStream"/>.
        /// </summary>
        public HttpResponse(IEnumerable<string> enumerable, HttpStatusCode status = HttpStatusCode._200_OK, HttpResponseHeaders headers = null)
        {
            Content = new DynamicContentStream(enumerable);
            Status = status;
            if (headers != null)
                Headers = headers;
        }

        /// <summary>
        /// Initialises <see cref="Content"/> to serve the specified string by converting it to UTF-8
        /// and then using a <see cref="MemoryStream"/>.
        /// </summary>
        public HttpResponse(string content, HttpStatusCode status = HttpStatusCode._200_OK, HttpResponseHeaders headers = null)
        {
            Content = new MemoryStream(content.ToUtf8());
            Status = status;
            if (headers != null)
                Headers = headers;
        }

        /// <summary>
        /// Initialises <see cref="Content"/> to serve the specified HTML using a
        /// <see cref="DynamicContentStream"/>.
        /// </summary>
        public HttpResponse(Tag html, HttpStatusCode status = HttpStatusCode._200_OK, HttpResponseHeaders headers = null)
        {
            Content = new DynamicContentStream(html.ToEnumerable());
            Status = status;
            if (headers != null)
                Headers = headers;
        }
    }
}
