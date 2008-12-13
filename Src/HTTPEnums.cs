
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

}
