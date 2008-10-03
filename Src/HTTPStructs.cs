using System;

namespace RT.Servers
{
    /// <summary>
    /// Encapsulates an HTTP cookie.
    /// </summary>
    public struct Cookie
    {
#pragma warning disable 1591    // Missing XML comment for publicly visible type or member
        public string Name;
        public string Value;
        public string Path;
        public string Domain;
        public DateTime? Expires;
        public bool HttpOnly;
#pragma warning restore 1591    // Missing XML comment for publicly visible type or member
    }

    /// <summary>
    /// Encapsulates the possible values of the Cache-Control HTTP request or response header.
    /// </summary>
    public struct HTTPCacheControl
    {
        /// <summary>Contains possible values of the Cache-Control header.</summary>
        public HTTPCacheControlState State;
        /// <summary>Some values of the Cache-Control header have an integer parameter.</summary>
        public int? IntParameter;
        /// <summary>Some values of the Cache-Control header have a string parameter.</summary>
        public string StringParameter;
    }

    /// <summary>
    /// Encapsulates the possible values of the Content-Disposition HTTP response header.
    /// </summary>
    public struct HTTPContentDisposition
    {
        /// <summary>Supports only two values ("None" and "Attachment").</summary>
        public HTTPContentDispositionMode Mode;
        /// <summary>If Mode is "Attachment", contains the filename of the attachment.</summary>
        public string Filename;
    }

    /// <summary>
    /// Encapsulates the possible values of the Content-Range HTTP response header.
    /// </summary>
    public struct HTTPContentRange
    {
        /// <summary>First byte index of the range. The first byte in the file has index 0.</summary>
        public long From;
        /// <summary>Last byte index of the range. For example, a range from 0 to 0 includes one byte.</summary>
        public long To;
        /// <summary>Total size of the file (not of the range).</summary>
        public long Total;
    }

    /// <summary>
    /// Encapsulates one of the ranges specified in a Range HTTP request header.
    /// </summary>
    public struct HTTPRange
    {
        /// <summary>First byte index of the range. The first byte in the file has index 0.</summary>
        public long? From;
        /// <summary>Last byte index of the range. For example, a range from 0 to 0 includes one byte.</summary>
        public long? To;
    }

    /// <summary>
    /// Contains all relevant information about a file upload contained in an HTTP POST request.
    /// </summary>
    public struct FileUpload
    {
        /// <summary>The path and filename of the temporary file in the local file system where the uploaded file is stored.</summary>
        public string LocalTempFilename;
        /// <summary>The filename of the uploaded file as supplied by the client.</summary>
        public string Filename;
        /// <summary>The MIME type of the uploaded file.</summary>
        public string ContentType;
    }
}
