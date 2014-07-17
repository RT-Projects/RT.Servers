using System;
using System.Collections.Generic;
using System.Text;
using RT.Util.ExtensionMethods;

namespace RT.Servers
{
    /// <summary>
    ///     Encapsulates all supported HTTP response headers. A request handler can set these appropriately to cause the
    ///     server to emit the required headers. See Remarks for a list of headers which are set by default.</summary>
    /// <remarks>
    ///     By default, ContentType is set to "text/html; charset=utf-8".</remarks>
    [Serializable]
    public sealed class HttpResponseHeaders
    {

#pragma warning disable 1591    // Missing XML comment for publicly visible type or member

        public HttpAcceptRanges? AcceptRanges;
        public int? Age; // in seconds
        public string[] Allow;  // usually: { "GET", "HEAD", "POST" }
        public HttpCacheControl[] CacheControl;
        public HttpConnection Connection;
        public HttpContentEncoding ContentEncoding = HttpContentEncoding.Identity;
        public string ContentLanguage;
        public long? ContentLength;
        public HttpContentDisposition? ContentDisposition;
        public string ContentMD5;
        public HttpContentRange? ContentRange;
        public string ContentType;
        public DateTime? Date;
        public WValue ETag;
        public DateTime? Expires;
        public DateTime? LastModified;
        public string Location; // used in redirection
        public string Pragma;
        public string Server;
        public List<Cookie> SetCookie;
        public HttpTransferEncoding? TransferEncoding;
        public string Upgrade;

#pragma warning restore 1591    // Missing XML comment for publicly visible type or member

        /// <summary>Provides a means to specify HTTP headers that are not defined in this class.</summary>
        public Dictionary<string, string> AdditionalHeaders;

        /// <summary>
        ///     Returns the HTTP-compliant ASCII representation of all response headers that have been set.</summary>
        /// <returns>
        ///     The HTTP-compliant ASCII representation of all response headers that have been set.</returns>
        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            switch (AcceptRanges)
            {
                case HttpAcceptRanges.Bytes:
                    b.Append("Accept-Ranges: bytes\r\n");
                    break;
            }
            if (Age != null)
                b.Append("Age: " + Age.Value + "\r\n");
            if (Allow != null)
                b.Append("Allow: " + string.Join(", ", Allow));
            if (CacheControl != null && CacheControl.Length > 0)
            {
                b.Append("Cache-Control: ");
                for (int i = 0; i < CacheControl.Length; i++)
                {
                    if (i > 0)
                        b.Append(", ");
                    b.Append(CacheControl[i].ToString());
                }
                b.Append("\r\n");
            }
            if (Connection != 0)
            {
                var list = new List<string>();
                if (Connection.HasFlag(HttpConnection.Close))
                    list.Add("close");
                if (Connection.HasFlag(HttpConnection.KeepAlive))
                    list.Add("keep-alive");
                if (Connection.HasFlag(HttpConnection.Upgrade))
                    list.Add("upgrade");
                b.Append("Connection: {0}\r\n".Fmt(list.JoinString(", ")));
            }
            if (ContentEncoding != HttpContentEncoding.Identity)
                b.Append("Content-Encoding: " + ContentEncoding.ToString().ToLowerInvariant() + "\r\n");
            if (ContentLanguage != null)
                b.Append("Content-Language: " + ContentLanguage + "\r\n");
            if (ContentLength != null)
                b.Append("Content-Length: " + ContentLength.Value + "\r\n");
            if (ContentDisposition != null)
                switch (ContentDisposition.Value.Mode)
                {
                    case HttpContentDispositionMode.Attachment:
                        if (ContentDisposition.Value.Filename == null)
                            b.Append("Content-Disposition: attachment\r\n");
                        else
                            b.Append("Content-Disposition: attachment; filename=" + ContentDisposition.Value.Filename + "\r\n");
                        break;
                }
            if (ContentMD5 != null)
                b.Append("Content-MD5: " + ContentMD5 + "\r\n");
            if (ContentRange != null)
                b.Append("Content-Range: bytes " + ContentRange.Value.From + "-" + ContentRange.Value.To + "/" + ContentRange.Value.Total + "\r\n");
            if (ContentType != null)
                b.Append("Content-Type: " + ContentType + "\r\n");
            if (Date != null)
                b.Append("Date: " + Date.Value.ToUniversalTime().ToString("r" /* = RFC1123 */) + "\r\n");
            if (ETag.Value != null)
                b.Append("ETag: " + ETag.ToString() + "\r\n");
            if (Expires != null)
                b.Append("Expires: " + Expires.Value.ToUniversalTime().ToString("r" /* = RFC1123 */) + "\r\n");
            if (LastModified != null)
                b.Append("Last-Modified: " + LastModified.Value.ToUniversalTime().ToString("r" /* = RFC1123 */) + "\r\n");
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
                    b.Append("Set-Cookie: " + c.Name + "=" + c.Value.UrlEscape());
                    if (c.Domain != null)
                        b.Append("; Domain=" + c.Domain);
                    if (c.Path != null)
                        b.Append("; Path=" + c.Path);
                    if (c.Expires != null)
                        b.Append("; Expires=" + c.Expires.Value.ToUniversalTime().ToString("r" /* = RFC1123 */));
                    b.Append(c.HttpOnly ? "; HttpOnly\r\n" : "\r\n");
                }
            }
            switch (TransferEncoding)
            {
                case HttpTransferEncoding.Chunked:
                    b.Append("Transfer-Encoding: chunked\r\n");
                    break;
            }
            if (Upgrade != null)
                b.Append("Upgrade: " + Upgrade + "\r\n");
            if (AdditionalHeaders != null)
                foreach (var kvp in AdditionalHeaders)
                    b.Append("{0}: {1}\r\n".Fmt(kvp.Key, kvp.Value));
            return b.ToString();
        }
    }
}
