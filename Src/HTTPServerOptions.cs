using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Servers
{
    public class HTTPServerOptions
    {
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
        /// The minimum size of a POST request at which the server will store the content of the request in a file instead of in memory. Default is 1 MB.
        /// </summary>
        public long UseFileUploadAtSize = 1024 * 1024;

        /// <summary>
        /// The temporary directory to use for POST requests and file uploads. Default is Path.GetTempPath().
        /// </summary>
        public string TempDir = Path.GetTempPath();

        /// <summary>
        /// Maps from file extension to MIME type. Used by FileSystemHandler().
        /// Use the key "*" for the default MIME type. Otherwise the default is "application/octet-stream".
        /// </summary>
        public Dictionary<string, string> MIMETypes = new Dictionary<string, string>();

        public HTTPServerOptions()
        {
            // Plain text
            MIMETypes["txt"] = "text/plain";

            // HTML and dependancies
            MIMETypes["htm"] = "text/html";
            MIMETypes["html"] = "text/html";
            MIMETypes["xhtml"] = "application/xhtml+xml";
            MIMETypes["css"] = "text/css";
            MIMETypes["js"] = "text/javascript";
            
            // Images
            MIMETypes["gif"] = "image/gif"; 
            MIMETypes["png"] = "image/png";
            MIMETypes["jpg"] = "image/jpeg";
            MIMETypes["jpeg"] = "image/jpeg";
            MIMETypes["bmp"] = "image/bmp";

            // Default
            MIMETypes["*"] = "application/octet-stream";
        }
    }
}
