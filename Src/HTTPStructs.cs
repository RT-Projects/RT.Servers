using System;

namespace Servers
{
    /// <summary>
    /// Encapsulates an HTTP cookie.
    /// </summary>
    public struct Cookie
    {
        public string Name;
        public string Value;
        public string Path;
        public string Domain;
        public DateTime? Expires;
        public bool HttpOnly;
    }

    /// <summary>
    /// Encapsulates the possible values of the Cache-Control HTTP request or response header.
    /// </summary>
    public struct HTTPCacheControl
    {
        public HTTPCacheControlState State;
        public int? IntParameter;
        public string StringParameter;
    }

    /// <summary>
    /// Encapsulates the possible values of the Content-Disposition HTTP response header.
    /// </summary>
    public struct HTTPContentDisposition
    {
        public HTTPContentDispositionMode Mode;
        public string Filename;
    }

    /// <summary>
    /// Encapsulates the possible values of the Content-Range HTTP response header.
    /// </summary>
    public struct HTTPContentRange
    {
        public long From;
        public long To;
        public long Total;
    }

    /// <summary>
    /// Encapsulates one of the ranges specified in a Range HTTP request header.
    /// </summary>
    public struct HTTPRange
    {
        public long? From;
        public long? To;
    }

    /// <summary>
    /// Contains all relevant information about a file upload contained in an HTTP POST request.
    /// </summary>
    public struct FileUpload
    {
        public string LocalTempFilename;
        public string Filename;
        public string ContentType;
    }
}
