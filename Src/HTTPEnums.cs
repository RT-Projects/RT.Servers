
namespace Servers
{
    /// <summary>
    /// Contains values for the supported HTTP methods.
    /// </summary>
    public enum HTTPMethod
    {
        GET,
        POST,
        HEAD
    }

    /// <summary>
    /// Contains values for the supported values of the Accept-Ranges HTTP request header.
    /// </summary>
    public enum HTTPAcceptRanges
    {
        None,
        Bytes
    }

    /// <summary>
    /// Contains values for the supported values of the Connection HTTP request or response header.
    /// </summary>
    public enum HTTPConnection
    {
        None,
        Close,
        KeepAlive
    }

    /// <summary>
    /// Contains values for the Cache-Control HTTP request or response header. None of these currently have any effect.
    /// </summary>
    public enum HTTPCacheControlState
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
    /// <seealso cref="HTTPContentDisposition"/>
    public enum HTTPContentDispositionMode
    {
        None,
        Attachment,
    }

    /// <summary>
    /// Contains values for the supported values of the Accept-Encoding HTTP request header and the Content-Encoding HTTP response header.
    /// </summary>
    public enum HTTPContentEncoding
    {
        Identity,
        Gzip,
        Compress,
        Deflate
    }

    /// <summary>
    /// Contains values for the supported values of the Transfer-Encoding HTTP response header.
    /// </summary>
    public enum HTTPTransferEncoding
    {
        None,
        Chunked
    }

    /// <summary>
    /// Controls which style of directory listing should be used by the FileSystemHandler() to list the contents of directories.
    /// </summary>
    public enum DirectoryListingStyle
    {
        XMLplusXSL
    }

    /// <summary>
    /// Contains values for the supported values of the Content-Type HTTP request header when used in HTTP POST requests.
    /// </summary>
    public enum HTTPPOSTContentType
    {
        None,
        ApplicationXWWWFormURLEncoded,
        MultipartFormData
    }
}
