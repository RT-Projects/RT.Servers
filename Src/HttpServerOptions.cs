using System.Collections.Generic;
using System.IO;
using System;

namespace RT.Servers
{
    /// <summary>Contains configuration settings for an <see cref="HttpServer"/>.</summary>
    [Serializable]
    public sealed class HttpServerOptions
    {
        /// <summary>
        ///     The IP address of the interface to which the HTTP server should bind, or null to let the server listen on all
        ///     network interfaces.</summary>
        /// <remarks>
        ///     This is a string rather than System.Net.IPAddress so that it is reasonably XmlClassifyable. If the contents
        ///     don’t parse, null is assumed.</remarks>
        public string BindAddress = null;

        /// <summary>
        ///     The port on which the server should listen for unsecured HTTP connections. Default is 80. Set to <c>null</c>
        ///     to disable unsecured HTTP.</summary>
        public int? Port = 80;

        /// <summary>
        ///     The port on which the server should listen for secured HTTPS connections. Default is <c>null</c>, i.e. with
        ///     HTTPS disabled. The usual port for HTTPS is 443.</summary>
        public int? SecurePort = null;

        /// <summary>
        ///     Specifies the path and filename of the X509 certificate to use in HTTPS. Currently, only certificates not
        ///     protected by a password are supported.</summary>
        public string CertificatePath = null;

        /// <summary>The password required to access the certificate in <see cref="CertificatePath"/>.</summary>
        public string CertificatePassword = null;

        /// <summary>Timeout in milliseconds for idle connections. Set to 0 for no timeout. Default is 10000 (10 seconds).</summary>
        public int IdleTimeout = 10000;

        /// <summary>Maximum allowed size for the headers of a request, in bytes. Default is 256 KB.</summary>
        public int MaxSizeHeaders = 256 * 1024;

        /// <summary>Maximum allowed size for the content of a POST request, in bytes. Default is 1 GB.</summary>
        public long MaxSizePostContent = 1024 * 1024 * 1024;

        /// <summary>
        ///     The maximum size (in bytes) at which file uploads in a POST request are stored in memory. Any uploads that
        ///     exceed this limit are written to temporary files on disk. Default is 16 MB.</summary>
        public long StoreFileUploadInFileAtSize = 1024 * 1024;

        /// <summary>
        ///     The maximum size (in bytes) of a response at which the server will gzip the entire content in-memory (assuming
        ///     gzip is requested in the HTTP request). Default is 1 MB. Content larger than this size will be gzipped in
        ///     chunks (if requested).</summary>
        public long GzipInMemoryUpToSize = 1024 * 1024;

        /// <summary>
        ///     If a file is larger than this, then the server will read a chunk from the middle of the file and gzip it to
        ///     determine whether gzipping the whole file is worth it. Otherwise it will default to using gzip either way.</summary>
        public int GzipAutodetectThreshold = 1024 * 1024;

        /// <summary>
        ///     The temporary directory to use for file uploads in POST requests. Default is <see cref="Path.GetTempPath"/>.</summary>
        public string TempDir = Path.GetTempPath();

        /// <summary>
        ///     Determines whether the default error handler outputs exception information (including a stack trace) to the
        ///     client (browser) as HTML. The default error handler always outputs the HTTP status code description. It is
        ///     invoked when <see cref="HttpServer.ErrorHandler"/> is null or throws an exception.</summary>
        public bool OutputExceptionInformation = false;

        /// <summary>Content-Type to return when handler provides none. Default is "text/html; charset=utf-8".</summary>
        public string DefaultContentType = "text/html; charset=utf-8";
    }
}
