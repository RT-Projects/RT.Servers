using System;
using System.Text.RegularExpressions;
using RT.Util;
using RT.Util.ExtensionMethods;

namespace RT.Servers
{

#pragma warning disable 1591    // Missing XML comment for publicly visible type or member

    /// <summary>Contains values for the supported HTTP protocol versions.</summary>
    public enum HttpProtocolVersion
    {
        Http10,
        Http11
    }

    /// <summary>Contains values for the supported HTTP methods.</summary>
    public enum HttpMethod
    {
        Get,
        Post,
        Head,
        Delete,
        Put,
        Patch
    }

    /// <summary>Contains values for the supported values of the Accept-Ranges HTTP request header.</summary>
    public enum HttpAcceptRanges
    {
        None,
        Bytes
    }

    /// <summary>Contains values for the supported values of the Connection HTTP request or response header.</summary>
    [Flags]
    public enum HttpConnection
    {
        Close = 1 << 0,
        KeepAlive = 1 << 1,
        Upgrade = 1 << 2
    }

    /// <summary>
    ///     Contains values for the Cache-Control HTTP request or response header. None of these currently have any effect.</summary>
    public enum HttpCacheControlState
    {
        // Request
        MaxStale,   // IntParameter = in seconds, optional
        MinFresh,   // IntParameter = in seconds
        OnlyIfCached,

        // Response
        MustRevalidate,
        ProxyRevalidate,
        Public,
        SMaxAge,    // IntParameter = in seconds

        // Both
        MaxAge,     // IntParameter = in seconds
        NoCache,    // StringParameter = field name, optional and response only
        NoStore,
        NoTransform,
        Private,    // StringParameter = field name, optional
    }

    /// <summary>
    ///     Contains values for the supported values of the Content-Disposition HTTP entity header, minus the filename.</summary>
    /// <seealso cref="HttpContentDisposition"/>
    public enum HttpContentDispositionMode
    {
        Attachment,
    }

    /// <summary>
    ///     Contains values for the supported values of the Accept-Encoding HTTP request header and the Content-Encoding HTTP
    ///     response header.</summary>
    public enum HttpContentEncoding
    {
        Identity,
        Gzip,
        Compress,
        Deflate
    }

    /// <summary>Contains values for the supported values of the Transfer-Encoding HTTP response header.</summary>
    public enum HttpTransferEncoding
    {
        Chunked
    }

    /// <summary>
    ///     Contains values for the supported values of the Content-Type HTTP request header when used in HTTP POST/PUT/PATCH
    ///     requests.</summary>
    public enum HttpPostContentType
    {
        ApplicationXWwwFormUrlEncoded,
        MultipartFormData
    }

#pragma warning restore 1591    // Missing XML comment for publicly visible type or member

    /// <summary>
    ///     Implements parse routines for the HttpEnums. These routines are preferred to <see cref="Enum.Parse(System.Type,
    ///     string)"/> because no reflection is involved.</summary>
    public static class HttpEnumsParser
    {
        /// <summary>Parses the Content-Encoding header. Returns null if the value is not valid.</summary>
        public static HttpContentEncoding? ParseHttpContentEncoding(string value)
        {
            if (value.EqualsNoCase("gzip")) return HttpContentEncoding.Gzip;
            else if (value.EqualsNoCase("compress")) return HttpContentEncoding.Compress;
            else if (value.EqualsNoCase("deflate")) return HttpContentEncoding.Deflate;
            else return null;
        }

        /// <summary>
        ///     Parses the Connection header. Throws an exception if the value is not valid. As long as exactly one of the
        ///     valid values is contained in a comma-separated version returns the value and ignores all other values.</summary>
        public static HttpConnection ParseHttpConnection(string value)
        {
            HttpConnection result = 0;
            foreach (var str in Regex.Split(value.Trim(), @"\s*,\s*"))
            {
                if (str.EqualsNoCase("close"))
                    result |= HttpConnection.Close;
                else if (str.EqualsNoCase("keep-alive"))
                    result |= HttpConnection.KeepAlive;
                else if (str.EqualsNoCase("upgrade"))
                    result |= HttpConnection.Upgrade;
            }
            return result;
        }
    }

    /// <summary>Contains possible values for the <see cref="HttpResponseContent.UseGzip"/> option.</summary>
    public enum UseGzipOption
    {
        /// <summary>
        ///     Specifies that the server should look at a chunk in the middle of the file to determine whether it is worth
        ///     gzipping.</summary>
        AutoDetect,
        /// <summary>Specifies not to use gzip for this response.</summary>
        DontUseGzip,
        /// <summary>Specifies to use gzip (if the client requested it).</summary>
        AlwaysUseGzip
    }

    /// <summary>Specifies possible protocols to hook to in a <see cref="UrlHook"/>.</summary>
    [Flags]
    public enum Protocols
    {
        /// <summary>The hook will never match.</summary>
        None = 0,

        /// <summary>The hook responds to unencrypted HTTP.</summary>
        Http = 1,
        /// <summary>The hook responds to HTTP encrypted via an SSL transport layer.</summary>
        Https = 2,

        /// <summary>The hook responds to all supported protocols.</summary>
        All = Http | Https
    }

    /// <summary>
    ///     Specifies possible values for the SameSite option on HTTP cookies.</summary>
    /// <remarks>
    ///     See: https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/Set-Cookie/SameSite</remarks>
    public enum HttpCookieSameSite
    {
        /// <summary>
        ///     Cookies are allowed to be sent with top-level navigations and will be sent along with GET request initiated by
        ///     third party website. This is the default value in modern browsers.</summary>
        Lax,
        /// <summary>
        ///     Cookies will only be sent in a first-party context and not be sent along with requests initiated by third
        ///     party websites.</summary>
        Strict,
        /// <summary>Cookies will be sent in all contexts, i.e sending cross-origin is allowed.</summary>
        None
    }
}
