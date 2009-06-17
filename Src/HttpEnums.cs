using System;
using System.Text.RegularExpressions;
using RT.Util.ExtensionMethods;

namespace RT.Servers
{

#pragma warning disable 1591    // Missing XML comment for publicly visible type or member

    /// <summary>
    /// Contains values for the supported HTTP methods.
    /// </summary>
    public enum HttpMethod
    {
        Get,
        Post,
        Head
    }

    /// <summary>
    /// Contains values for the supported values of the Accept-Ranges HTTP request header.
    /// </summary>
    public enum HttpAcceptRanges
    {
        None,
        Bytes
    }

    /// <summary>
    /// Contains values for the supported values of the Connection HTTP request or response header.
    /// </summary>
    public enum HttpConnection
    {
        None,
        Close,
        KeepAlive
    }

    /// <summary>
    /// Contains values for the Cache-Control HTTP request or response header. None of these currently have any effect.
    /// </summary>
    public enum HttpCacheControlState
    {
        None,

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

        PostCheck, // IntParameter
        PreCheck,  // IntParameter
    }

    /// <summary>
    /// Contains values for the supported values of the Content-Disposition HTTP entity header, minus the filename.
    /// </summary>
    /// <seealso cref="HttpContentDisposition"/>
    public enum HttpContentDispositionMode
    {
        None,
        Attachment,
    }

    /// <summary>
    /// Contains values for the supported values of the Accept-Encoding HTTP request header and the Content-Encoding HTTP response header.
    /// </summary>
    public enum HttpContentEncoding
    {
        Identity,
        Gzip,
        Compress,
        Deflate
    }

    /// <summary>
    /// Contains values for the supported values of the Transfer-Encoding HTTP response header.
    /// </summary>
    public enum HttpTransferEncoding
    {
        None,
        Chunked
    }

    /// <summary>
    /// Controls which style of directory listing should be used by <see cref="HttpServer.FileSystemResponse"/> to list the contents of directories.
    /// </summary>
    public enum DirectoryListingStyle
    {
        XmlPlusXsl
    }

    /// <summary>
    /// Contains values for the supported values of the Content-Type HTTP request header when used in HTTP POST requests.
    /// </summary>
    public enum HttpPostContentType
    {
        None,
        ApplicationXWwwFormUrlEncoded,
        MultipartFormData
    }

#pragma warning restore 1591    // Missing XML comment for publicly visible type or member

    /// <summary>
    /// Implements parse routines for the HttpEnums. These routines are preferred to <see cref="Enum.Parse(System.Type, string)"/>
    /// because no reflection is involved.
    /// </summary>
    public static class HttpEnumsParser
    {
        private static bool eq(string s1, string s2)
        {
            return string.Equals(s1, s2, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Parses the Content-Encoding header. Throws an exception if the value is not valid.
        /// </summary>
        public static HttpContentEncoding ParseHttpContentEncoding(string value)
        {
            if (eq(value, "gzip")) return HttpContentEncoding.Gzip;
            else if (eq(value, "compress")) return HttpContentEncoding.Compress;
            else if (eq(value, "deflate")) return HttpContentEncoding.Deflate;
            else throw new ArgumentException(@"""Content-Encoding"" value ""{0}"" is not valid. Valid values: ""gzip"", ""compress"", ""deflate"".".Fmt(value));
        }

        /// <summary>
        /// Parses the Connection header. Throws an exception if the value is not valid.
        /// As long as exactly one of the valid values is contained in a comma-separated version returns the value
        /// and ignores all other values.
        /// </summary>
        public static HttpConnection ParseHttpConnection(string value)
        {
            bool hasClose = false, hasKeepalive = false;
            foreach (var str in Regex.Split(value.Trim(), @"\s*,\s*"))
            {
                hasClose |= eq(str, "close");
                hasKeepalive |= eq(str, "keep-alive");
            }
            if (hasClose == hasKeepalive)
                throw new ArgumentException(@"""Connection"" value ""{0}"" could not be parsed.".Fmt(value));
            else if (hasClose)
                return HttpConnection.Close;
            else
                return HttpConnection.KeepAlive;
        }
    }
}
