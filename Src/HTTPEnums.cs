
namespace Servers
{
    public enum HTTPMethod
    {
        GET,
        POST,
        HEAD
    }

    public enum HTTPAcceptRanges
    {
        None,
        Bytes
    }

    public enum HTTPConnection
    {
        None,
        Close,
        KeepAlive
    }

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

    public enum HTTPContentDispositionMode
    {
        None,
        Attachment
    }

    public enum HTTPContentEncoding
    {
        Identity,
        Gzip,
        Compress,
        Deflate
    }

    public enum HTTPTransferEncoding
    {
        None,
        Chunked
    }

    public enum DirectoryListingStyle
    {
        HTML,
        XMLplusXSL
    }
}
