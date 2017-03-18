using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using RT.Util.Serialization;

namespace RT.Servers
{
    /// <summary>Contains configuration settings for an <see cref="HttpServer"/>.</summary>
    [Serializable]
    public sealed class HttpServerOptions : IClassifyObjectProcessor
    {
        /// <summary>
        ///     A readonly dictionary of added server endpoints. See <see cref="AddEndpoint(string,string,int,bool)"/>, <see
        ///     cref="AddEndpoint(string,HttpEndpoint)"/> and <seealso cref="RemoveEndpoint"/> for adding and removing things
        ///     from this list.</summary>
        public IDictionary<string, HttpEndpoint> Endpoints { get { return _endpoints; } }

        /// <summary>
        ///     Adds an endpoint for the server to listen on.</summary>
        /// <param name="key">
        ///     Unique key used to identify this endpoint.</param>
        /// <param name="endpoint">
        ///     The endpoint object.</param>
        public HttpServerOptions AddEndpoint(string key, HttpEndpoint endpoint)
        {
            _endpoints.Add(key, endpoint);
            return this;
        }

        /// <summary>
        ///     Adds an endpoint for the server to listen on.</summary>
        /// <param name="key">
        ///     Unique key used to identify this endpoint.</param>
        /// <param name="bindAddress">
        ///     The hostname/address to listen on, or null to listen on all interfaces.</param>
        /// <param name="port">
        ///     The port to listen on.</param>
        /// <param name="secure">
        ///     Specifies whether this is a secure (HTTPS) endpoint.</param>
        public HttpServerOptions AddEndpoint(string key, string bindAddress, int port, bool secure = false)
        {
            AddEndpoint(key, new HttpEndpoint(bindAddress, port, secure));
            return this;
        }

        /// <summary>
        ///     Removes an endpoint from this option class.</summary>
        /// <param name="key">
        ///     The unique key used to removed from endpoint.</param>
        /// <returns>
        ///     <c>true</c> if the key was removed; <c>false</c> if the key did not exist.</returns>
        public bool RemoveEndpoint(string key)
        {
            return _endpoints.Remove(key);
        }

        // Backwards compatibility with old serialized HttpServerOptions instances
        [ClassifyIgnoreIfDefault]
        private string BindAddress = null;
        [ClassifyIgnoreIfDefault]
        private int? Port = null;
        [ClassifyIgnoreIfDefault]
        private int? SecurePort = null;
        [ClassifyIgnoreIfDefault]
        private string CertificatePath = null;
        [ClassifyIgnoreIfDefault]
        private string CertificatePassword = null;

        /// <summary>
        ///     Specifies the default X509 certificate to use in HTTPS. Use <see cref="Certificates"/> to override this for
        ///     specific domain names. If <see cref="CertificateResolver"/> is specified, this is ignored.</summary>
        public HttpServerCertificateInfo Certificate = null;

        /// <summary>
        ///     Specifies the X509 certificates to use for specific domain names. If <see cref="CertificateResolver"/> is
        ///     specified, this is ignored.</summary>
        public Dictionary<string, HttpServerCertificateInfo> Certificates = null;

        /// <summary>
        ///     A function which returns a certificate based on a host name (SNI). The input is <c>null</c> if the client does
        ///     not specify a host name in the TLS hello packet. If specified, this takes precendence over both <see
        ///     cref="Certificates"/> and <see cref="Certificate"/>. Return <c>null</c> to fall back to <see
        ///     cref="Certificates"/> and <see cref="Certificate"/>.</summary>
        [ClassifyIgnore]
        public Func<string, X509Certificate2> CertificateResolver = null;

        /// <summary>Timeout in milliseconds for idle connections. Set to 0 for no timeout. Default is 10000 (10 seconds).</summary>
        public int IdleTimeout = 10000;

        /// <summary>Maximum allowed size for the headers of a request, in bytes. Default is 256 KB.</summary>
        public int MaxSizeHeaders = 256 * 1024;

        /// <summary>Maximum allowed size for the content of a POST/PUT/PATCH request, in bytes. Default is 1 GB.</summary>
        public long MaxSizePostContent = 1024 * 1024 * 1024;

        /// <summary>
        ///     The maximum size (in bytes) at which file uploads in a POST/PUT/PATCH request are stored in memory. Any
        ///     uploads that exceed this limit are written to temporary files on disk. Default is 16 MB.</summary>
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
        ///     The temporary directory to use for file uploads in POST/PUT/PATCH requests. Default is <see
        ///     cref="Path.GetTempPath"/>.</summary>
        public string TempDir = Path.GetTempPath();

        /// <summary>
        ///     Determines whether the default error handler outputs exception information (including a stack trace) to the
        ///     client (browser) as HTML. The default error handler always outputs the HTTP status code description. It is
        ///     invoked when <see cref="HttpServer.ErrorHandler"/> is null or throws an exception.</summary>
        public bool OutputExceptionInformation = false;

        /// <summary>Content-Type to return when handler provides none. Default is "text/html; charset=utf-8".</summary>
        public string DefaultContentType = "text/html; charset=utf-8";

        private readonly Dictionary<string, HttpEndpoint> _endpoints = new Dictionary<string, HttpEndpoint>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        ///     Throws an exception if the settings are invalid.</summary>
        /// <remarks>
        ///     <para>
        ///         Possible reasons for invalid settings include:</para>
        ///     <list type="bullet">
        ///         <item><description>
        ///             There are no endpoints.</description></item>
        ///         <item><description>
        ///             There is an endpoint with <see cref="HttpEndpoint.Secure"/> set to <c>true</c>, but <see
        ///             cref="CertificatePath"/> is <c>null</c>.</description></item></list></remarks>
        public void CheckValid()
        {
            if (Endpoints.Count < 1)
                throw new ArgumentException("There are no endpoints specified. There is no port to listen on.");

            if (Endpoints.Values.Any(e => e.Secure) && CertificateResolver == null && Certificate == null && Certificates == null)
                throw new ArgumentException("Since there is an endpoint flagged 'Secure', a certificate must be specified in the options.");
        }

        void IClassifyObjectProcessor.BeforeSerialize() { }

        void IClassifyObjectProcessor.AfterDeserialize()
        {
            if (BindAddress != null || Port != null || SecurePort != null)
            {
                if (Port != null)
                    AddEndpoint("HTTP", BindAddress, Port.Value, secure: false);
                if (SecurePort != null)
                    AddEndpoint("HTTPS", BindAddress, SecurePort.Value, secure: true);

                BindAddress = null;
                Port = null;
                SecurePort = null;
            }

            if (Certificate == null && CertificatePath != null)
            {
                Certificate = new HttpServerCertificateInfo { Path = CertificatePath, Password = CertificatePassword };
                CertificatePath = null;
                CertificatePassword = null;
            }
        }
    }

    /// <summary>Encapsulates information about a server certificate.</summary>
    [Serializable]
    public sealed class HttpServerCertificateInfo
    {
        /// <summary>The path and filename of the certificate file on disk.</summary>
        public string Path;
        /// <summary>The password required to access the certificate stored in <see cref="Path"/>.</summary>
        public string Password;

        /// <summary>Instantiates and returns the <see cref="X509Certificate2"/>.</summary>
        public X509Certificate2 GetCertificate() => new X509Certificate2(Path, Password);
    }
}
