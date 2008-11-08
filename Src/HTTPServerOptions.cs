using System.Collections.Generic;
using System.IO;
using System;

namespace RT.Servers
{
    /// <summary>
    /// Contains configuration settings for an <see cref="HTTPServer"/>.
    /// </summary>
    [Serializable]
    public class HTTPServerOptions
    {
        /// <summary>
        /// The port on which the HTTP server should listen. Default is 80.
        /// </summary>
        public int Port = 80;

        /// <summary>
        /// Timeout in milliseconds for idle connections. Set to 0 for no timeout. Default is 10000 (10 seconds).
        /// </summary>
        public int IdleTimeout = 10000;

        /// <summary>
        /// Maximum allowed size for the headers of a request, in bytes. Default is 256 KB.
        /// </summary>
        public int MaxSizeHeaders = 256 * 1024;

        /// <summary>
        /// Maximum allowed size for the content of a POST request, in bytes. Default is 1 GB.
        /// </summary>
        public long MaxSizePostContent = 1024 * 1024 * 1024;

        /// <summary>
        /// The minimum size (in bytes) of a POST request at which the server will store the content of the request in a file instead of in memory. Default is 1 MB.
        /// </summary>
        public long UseFileUploadAtSize = 1024 * 1024;

        /// <summary>
        /// The maximum size (in bytes) of a response at which the server will gzip the entire content in-memory (assuming gzip is requested in the HTTP request).
        /// Default is 1 MB. Content larger than this size will be gzipped in chunks (if requested).
        /// </summary>
        public long GzipInMemoryUpToSize = 1024 * 1024;

        /// <summary>
        /// The temporary directory to use for POST requests and file uploads. Default is <see cref="Path.GetTempPath"/>.
        /// </summary>
        public string TempDir = Path.GetTempPath();

        /// <summary>
        /// Determines whether exceptions thrown by content handlers are caught and output to the client.
        /// If false, exceptions thrown by content handlers are not caught by <see cref="HTTPServer"/>.
        /// </summary>
        public bool ReturnExceptionsToClient = true;

        /// <summary>
        /// Maps from file extension to MIME type. Used by <see cref="HTTPServer.FileSystemResponse"/>
        /// (and hence by <see cref="HTTPServer.CreateFileSystemHandler"/>).
        /// Use the key "*" for the default MIME type. Otherwise the default is "application/octet-stream".
        /// </summary>
        public Dictionary<string, string> MIMETypes = new Dictionary<string, string>
        {
            // Plain text
            { "txt", "text/plain; charset=utf-8" },
            { "csv", "text/csv; charset=utf-8" },

            // HTML and dependancies
            { "htm", "text/html; charset=utf-8" },
            { "html", "text/html; charset=utf-8" },
            { "css", "text/css; charset=utf-8" },
            { "js", "text/javascript; charset=utf-8" },

            // XML and stuff
            { "xhtml", "application/xhtml+xml; charset=utf-8" },
            { "xml", "application/xml; charset=utf-8" },
            { "xsl", "application/xml; charset=utf-8" },

            // Images
            { "gif", "image/gif" },
            { "png", "image/png" },
            { "jp2", "image/jp2" },
            { "jpg", "image/jpeg" },
            { "jpeg", "image/jpeg" },
            { "bmp", "image/bmp" },

            // Default
            { "*", "application/octet-stream" }
        };

        /// <summary>
        /// Content-Type to return when handler provides none. Default is "text/html; charset=utf-8".
        /// </summary>
        public string DefaultContentType = "text/html; charset=utf-8";

        /// <summary>
        /// Enum specifying which way directory listings should be generated. Default is <see cref="RT.Servers.DirectoryListingStyle.XMLplusXSL"/>.
        /// </summary>
        public DirectoryListingStyle DirectoryListingStyle = DirectoryListingStyle.XMLplusXSL;
    }
}
