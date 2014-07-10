using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using RT.TagSoup;
using RT.Util;
using RT.Util.ExtensionMethods;
using RT.Util.Json;
using RT.Util.Streams;
using RT.Servers.SharpZipLib.GZip;
using System.Text;

namespace RT.Servers
{
    /// <summary>
    ///     Encapsulates an HTTP response, to be sent by <see cref="HttpServer"/> to the HTTP client that sent the original
    ///     request. A request handler must return an HttpResponse object to the <see cref="HttpServer"/> when handling a
    ///     request.</summary>
    public sealed class HttpResponseContent : HttpResponse
    {
        /// <summary>The HTTP status code. For example, 200 OK, 404 Not Found, 500 Internal Server Error. Default is 200 OK.</summary>
        public override HttpStatusCode Status { get { return _status; } }
        private HttpStatusCode _status;

        private Func<Stream> _contentStreamDelegate;

        /// <summary>Retrieves the stream object containing the response body.</summary>
        public override Stream GetContentStream()
        {
            return _contentStreamDelegate == null ? null : _contentStreamDelegate();
        }

        /// <summary>Specifies whether gzip should be used.</summary>
        public UseGzipOption UseGzip = UseGzipOption.AutoDetect;

        public HttpResponseContent(HttpStatusCode status, HttpResponseHeaders headers, Func<Stream> contentStreamDelegate = null)
            : base(headers)
        {
            _status = status;
            _contentStreamDelegate = contentStreamDelegate;
        }

        /// <summary>
        ///     Modifies the <see cref="UseGzip"/> option in this response object and returns the same object.</summary>
        /// <param name="option">
        ///     The new value for the <see cref="UseGzip"/> option.</param>
        public HttpResponseContent Set(UseGzipOption option)
        {
            UseGzip = option;
            return this;
        }
    }
}
