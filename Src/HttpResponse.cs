using System;
using System.Collections.Generic;
using System.IO;
using RT.TagSoup;
using RT.Util;
using RT.Util.ExtensionMethods;
using RT.Util.Json;
using RT.Util.Streams;

namespace RT.Servers
{
    /// <summary>
    ///     Encapsulates a response to an HTTP request. Concrete classes implementing this are <see
    ///     cref="HttpResponseContent"/> and <see cref="HttpResponseWebSocket"/>.</summary>
    public abstract class HttpResponse : MarshalByRefObject
    {
        /// <summary>Constructor.</summary>
        protected HttpResponse(HttpResponseHeaders headers) { _headers = headers; }

        /// <summary>The HTTP status code. For example, 200 OK, 404 Not Found, 500 Internal Server Error. Default is 200 OK.</summary>
        public abstract HttpStatusCode Status { get; }

        /// <summary>The HTTP response headers which are to be sent back to the HTTP client as part of this HTTP response.</summary>
        public HttpResponseHeaders Headers { get { return _headers; } }
        private HttpResponseHeaders _headers;

        /// <summary>Returns <c>null</c>. Overridden only by <see cref="HttpResponseContent"/>.</summary>
        public virtual Stream GetContentStream() { return null; }

        /// <summary>
        ///     Returns the specified file from the local file system using the specified MIME content type to the client.</summary>
        /// <param name="filePath">
        ///     Full path and filename of the file to return.</param>
        /// <param name="contentType">
        ///     MIME type to use in the Content-Type header. If null, the first kilobyte will be looked at to choose between
        ///     the plaintext and the octet-stream content type.</param>
        /// <param name="maxAge">
        ///     Specifies the value for the CacheControl max-age header on the file served. Set to null to prevent this header
        ///     being sent.</param>
        /// <param name="ifModifiedSince">
        ///     If specified, a 304 Not Modified will be served if the file's last modified timestamp is at or before this
        ///     time.</param>
        public static HttpResponseContent File(string filePath, string contentType, int? maxAge = 3600, DateTime? ifModifiedSince = null)
        {
            try
            {
                var timestamp = System.IO.File.GetLastWriteTimeUtc(filePath).TruncatedToSeconds();
                if (timestamp <= ifModifiedSince)
                    return NotModified();

                var getFileStream = Ut.Lambda(() => System.IO.File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));

                // Do a limited amount of content-type guessing if necessary.
                if (contentType == null)
                    using (var stream = getFileStream())
                    {
                        // Look at the first 1 KB. If there are special control characters in it, it's likely a binary file. Otherwise, output as text/plain.
                        byte[] buf = new byte[1024];
                        int bytesRead = stream.FillBuffer(buf, 0, buf.Length);
                        contentType = "text/plain; charset=utf-8";
                        for (int i = 0; i < bytesRead; i++)
                            if (buf[i] < 32 && buf[i] != 9 && buf[i] != 10 && buf[i] != 13)
                            {
                                contentType = "application/octet-stream";
                                break;
                            }
                    }

                return create(getFileStream, contentType, HttpStatusCode._200_OK, new HttpResponseHeaders
                {
                    LastModified = timestamp,
                    CacheControl = maxAge == null ? null : new[] { new HttpCacheControl { State = HttpCacheControlState.MaxAge, IntParameter = maxAge.Value } },
                });
            }
            catch (FileNotFoundException)
            {
                throw new HttpNotFoundException();
            }
            catch (IOException e)
            {
                throw new HttpException(HttpStatusCode._500_InternalServerError, "File could not be opened in the file system: " + e.Message);
            }
        }

        /// <summary>
        ///     Redirects the client to a new URL, using the HTTP status code 302 Found and making the response uncacheable.</summary>
        /// <param name="newUrl">
        ///     URL to redirect the client to.</param>
        public static HttpResponseContent Redirect(string newUrl)
        {
            return new HttpResponseContent(
                HttpStatusCode._302_Found,
                new HttpResponseHeaders
                {
                    Location = newUrl,
                    CacheControl = new[] { new HttpCacheControl { State = HttpCacheControlState.Private }, new HttpCacheControl { State = HttpCacheControlState.MaxAge, IntParameter = 0 } },
                });
        }

        /// <summary>
        ///     Redirects the client to a new URL, using the HTTP status code 302 Found and making the response uncacheable.</summary>
        /// <param name="newUrl">
        ///     URL to redirect the client to.</param>
        public static HttpResponseContent Redirect(IHttpUrl newUrl)
        {
            return Redirect(newUrl.ToFull());
        }

        /// <summary>Generates a 304 Not Modified response.</summary>
        public static HttpResponseContent NotModified()
        {
            return new HttpResponseContent(HttpStatusCode._304_NotModified, new HttpResponseHeaders());
        }

        /// <summary>
        ///     Returns a response to the client consisting of an empty body.</summary>
        /// <param name="status">
        ///     HTTP status code to use in the response.</param>
        /// <param name="headers">
        ///     Headers to use in the response, or null to use default values.</param>
        public static HttpResponseContent Empty(HttpStatusCode status = HttpStatusCode._200_OK, HttpResponseHeaders headers = null)
        {
            return new HttpResponseContent(status, headers ?? new HttpResponseHeaders());
        }

        /// <summary>
        ///     Returns the specified tag content to the client.</summary>
        /// <param name="content">
        ///     Content to return to the client.</param>
        /// <param name="status">
        ///     HTTP status code to use in the response.</param>
        /// <param name="headers">
        ///     Headers to use in the response, or null to use default values.</param>
        /// <param name="buffered">
        ///     If true (default), the output is buffered for performance; otherwise, all text is transmitted as soon as
        ///     possible.</param>
        public static HttpResponseContent Html(Tag content, HttpStatusCode status = HttpStatusCode._200_OK, HttpResponseHeaders headers = null, bool buffered = true)
        {
            return create(() => new DynamicContentStream(content.ToEnumerable(), buffered), "text/html; charset=utf-8", status, headers);
        }

        /// <summary>
        ///     Returns the specified content to the client with the MIME type “text/html; charset=utf-8”.</summary>
        /// <param name="content">
        ///     Content to return to the client.</param>
        /// <param name="status">
        ///     HTTP status code to use in the response.</param>
        /// <param name="headers">
        ///     Headers to use in the response, or null to use default values.</param>
        public static HttpResponseContent Html(string content, HttpStatusCode status = HttpStatusCode._200_OK, HttpResponseHeaders headers = null)
        {
            return Create(content, "text/html; charset=utf-8", status, headers);
        }

        /// <summary>
        ///     Returns the specified content to the client as a single concatenated piece of text with the MIME type
        ///     “text/html; charset=utf-8”.</summary>
        /// <param name="content">
        ///     Content to return to the client.</param>
        /// <param name="status">
        ///     HTTP status code to use in the response.</param>
        /// <param name="headers">
        ///     Headers to use in the response, or null to use default values.</param>
        /// <param name="buffered">
        ///     If true (default), the output is buffered for performance; otherwise, all text is transmitted as soon as
        ///     possible.</param>
        public static HttpResponseContent Html(IEnumerable<string> content, HttpStatusCode status = HttpStatusCode._200_OK, HttpResponseHeaders headers = null, bool buffered = true)
        {
            return create(() => new DynamicContentStream(content, buffered), "text/html; charset=utf-8", status, headers);
        }

        /// <summary>
        ///     Returns the contents of the specified byte array to the client.</summary>
        /// <param name="content">
        ///     Content to return to the client.</param>
        /// <param name="status">
        ///     HTTP status code to use in the response.</param>
        /// <param name="headers">
        ///     Headers to use in the response, or null to use default values.</param>
        public static HttpResponseContent Html(byte[] content, HttpStatusCode status = HttpStatusCode._200_OK, HttpResponseHeaders headers = null)
        {
            return create(() => new MemoryStream(content), "text/html; charset=utf-8", status, headers);
        }

        /// <summary>
        ///     Returns the contents of the specified stream to the client.</summary>
        /// <param name="content">
        ///     Content to return to the client.</param>
        /// <param name="status">
        ///     HTTP status code to use in the response.</param>
        /// <param name="headers">
        ///     Headers to use in the response, or null to use default values.</param>
        public static HttpResponseContent Html(Stream content, HttpStatusCode status = HttpStatusCode._200_OK, HttpResponseHeaders headers = null)
        {
            return Create(content, "text/html; charset=utf-8", status, headers);
        }

        /// <summary>
        ///     Returns the specified content to the client with the MIME type “text/plain; charset=utf-8”.</summary>
        /// <param name="content">
        ///     Content to return to the client.</param>
        /// <param name="status">
        ///     HTTP status code to use in the response.</param>
        /// <param name="headers">
        ///     Headers to use in the response, or null to use default values.</param>
        public static HttpResponseContent PlainText(string content, HttpStatusCode status = HttpStatusCode._200_OK, HttpResponseHeaders headers = null)
        {
            return Create(content, "text/plain; charset=utf-8", status, headers);
        }

        /// <summary>
        ///     Returns the specified content to the client as a single concatenated piece of text with the MIME type
        ///     “text/plain; charset=utf-8”.</summary>
        /// <param name="content">
        ///     Content to return to the client.</param>
        /// <param name="status">
        ///     HTTP status code to use in the response.</param>
        /// <param name="headers">
        ///     Headers to use in the response, or null to use default values.</param>
        /// <param name="buffered">
        ///     If true (default), the output is buffered for performance; otherwise, all text is transmitted as soon as
        ///     possible.</param>
        public static HttpResponseContent PlainText(IEnumerable<string> content, HttpStatusCode status = HttpStatusCode._200_OK, HttpResponseHeaders headers = null, bool buffered = true)
        {
            return create(() => new DynamicContentStream(content, buffered), "text/plain; charset=utf-8", status, headers);
        }

        /// <summary>
        ///     Returns the contents of the specified byte array to the client.</summary>
        /// <param name="content">
        ///     Content to return to the client.</param>
        /// <param name="status">
        ///     HTTP status code to use in the response.</param>
        /// <param name="headers">
        ///     Headers to use in the response, or null to use default values.</param>
        public static HttpResponseContent PlainText(byte[] content, HttpStatusCode status = HttpStatusCode._200_OK, HttpResponseHeaders headers = null)
        {
            return create(() => new MemoryStream(content), "text/plain; charset=utf-8", status, headers);
        }

        /// <summary>
        ///     Returns the contents of the specified stream to the client.</summary>
        /// <param name="content">
        ///     Content to return to the client.</param>
        /// <param name="status">
        ///     HTTP status code to use in the response.</param>
        /// <param name="headers">
        ///     Headers to use in the response, or null to use default values.</param>
        public static HttpResponseContent PlainText(Stream content, HttpStatusCode status = HttpStatusCode._200_OK, HttpResponseHeaders headers = null)
        {
            return Create(content, "text/plain; charset=utf-8", status, headers);
        }

        /// <summary>
        ///     Returns the specified content to the client with the MIME type “text/javascript; charset=utf-8”.</summary>
        /// <param name="content">
        ///     Content to return to the client.</param>
        /// <param name="status">
        ///     HTTP status code to use in the response.</param>
        /// <param name="headers">
        ///     Headers to use in the response, or null to use default values.</param>
        public static HttpResponseContent JavaScript(string content, HttpStatusCode status = HttpStatusCode._200_OK, HttpResponseHeaders headers = null)
        {
            return Create(content, "text/javascript; charset=utf-8", status, headers);
        }

        /// <summary>
        ///     Returns the specified content to the client as a single concatenated piece of text with the MIME type
        ///     “text/javascript; charset=utf-8”.</summary>
        /// <param name="content">
        ///     Content to return to the client.</param>
        /// <param name="status">
        ///     HTTP status code to use in the response.</param>
        /// <param name="headers">
        ///     Headers to use in the response, or null to use default values.</param>
        /// <param name="buffered">
        ///     If true (default), the output is buffered for performance; otherwise, all text is transmitted as soon as
        ///     possible.</param>
        public static HttpResponseContent JavaScript(IEnumerable<string> content, HttpStatusCode status = HttpStatusCode._200_OK, HttpResponseHeaders headers = null, bool buffered = true)
        {
            return create(() => new DynamicContentStream(content, buffered), "text/javascript; charset=utf-8", status, headers);
        }

        /// <summary>
        ///     Returns the contents of the specified byte array to the client.</summary>
        /// <param name="content">
        ///     Content to return to the client.</param>
        /// <param name="status">
        ///     HTTP status code to use in the response.</param>
        /// <param name="headers">
        ///     Headers to use in the response, or null to use default values.</param>
        public static HttpResponseContent JavaScript(byte[] content, HttpStatusCode status = HttpStatusCode._200_OK, HttpResponseHeaders headers = null)
        {
            return create(() => new MemoryStream(content), "text/javascript; charset=utf-8", status, headers);
        }

        /// <summary>
        ///     Returns the contents of the specified stream to the client.</summary>
        /// <param name="content">
        ///     Content to return to the client.</param>
        /// <param name="status">
        ///     HTTP status code to use in the response.</param>
        /// <param name="headers">
        ///     Headers to use in the response, or null to use default values.</param>
        public static HttpResponseContent JavaScript(Stream content, HttpStatusCode status = HttpStatusCode._200_OK, HttpResponseHeaders headers = null)
        {
            return Create(content, "text/javascript; charset=utf-8", status, headers);
        }

        /// <summary>
        ///     Returns the specified content to the client with the MIME type “application/json; charset=utf-8”.</summary>
        /// <param name="content">
        ///     Content to return to the client.</param>
        /// <param name="status">
        ///     HTTP status code to use in the response.</param>
        /// <param name="headers">
        ///     Headers to use in the response, or null to use default values.</param>
        public static HttpResponseContent Json(JsonValue content, HttpStatusCode status = HttpStatusCode._200_OK, HttpResponseHeaders headers = null)
        {
            return Create(JsonValue.ToEnumerable(content), "application/json; charset=utf-8", status, headers);
        }

        /// <summary>
        ///     Returns the specified content to the client with the MIME type “text/css; charset=utf-8”.</summary>
        /// <param name="content">
        ///     Content to return to the client.</param>
        /// <param name="status">
        ///     HTTP status code to use in the response.</param>
        /// <param name="headers">
        ///     Headers to use in the response, or null to use default values.</param>
        public static HttpResponseContent Css(string content, HttpStatusCode status = HttpStatusCode._200_OK, HttpResponseHeaders headers = null)
        {
            return Create(content, "text/css; charset=utf-8", status, headers);
        }

        /// <summary>
        ///     Returns the specified content to the client as a single concatenated piece of text with the MIME type
        ///     “text/css; charset=utf-8”.</summary>
        /// <param name="content">
        ///     Content to return to the client.</param>
        /// <param name="status">
        ///     HTTP status code to use in the response.</param>
        /// <param name="headers">
        ///     Headers to use in the response, or null to use default values.</param>
        /// <param name="buffered">
        ///     If true (default), the output is buffered for performance; otherwise, all text is transmitted as soon as
        ///     possible.</param>
        public static HttpResponseContent Css(IEnumerable<string> content, HttpStatusCode status = HttpStatusCode._200_OK, HttpResponseHeaders headers = null, bool buffered = true)
        {
            return create(() => new DynamicContentStream(content, buffered), "text/css; charset=utf-8", status, headers);
        }

        /// <summary>
        ///     Returns the contents of the specified byte array to the client.</summary>
        /// <param name="content">
        ///     Content to return to the client.</param>
        /// <param name="status">
        ///     HTTP status code to use in the response.</param>
        /// <param name="headers">
        ///     Headers to use in the response, or null to use default values.</param>
        public static HttpResponseContent Css(byte[] content, HttpStatusCode status = HttpStatusCode._200_OK, HttpResponseHeaders headers = null)
        {
            return create(() => new MemoryStream(content), "text/css; charset=utf-8", status, headers);
        }

        /// <summary>
        ///     Returns the contents of the specified stream to the client.</summary>
        /// <param name="content">
        ///     Content to return to the client.</param>
        /// <param name="status">
        ///     HTTP status code to use in the response.</param>
        /// <param name="headers">
        ///     Headers to use in the response, or null to use default values.</param>
        public static HttpResponseContent Css(Stream content, HttpStatusCode status = HttpStatusCode._200_OK, HttpResponseHeaders headers = null)
        {
            return Create(content, "text/css; charset=utf-8", status, headers);
        }

        /// <summary>
        ///     Returns the specified content to the client with the specified MIME type.</summary>
        /// <param name="content">
        ///     Content to return to the client.</param>
        /// <param name="contentType">
        ///     MIME type to use. This overrides any MIME type specified in <paramref name="headers"/> (if any).</param>
        /// <param name="status">
        ///     HTTP status code to use in the response.</param>
        /// <param name="headers">
        ///     Headers to use in the response, or null to use default values.</param>
        public static HttpResponseContent Create(string content, string contentType, HttpStatusCode status = HttpStatusCode._200_OK, HttpResponseHeaders headers = null)
        {
            var utf8 = content.ToUtf8();
            return create(() => new MemoryStream(utf8), contentType, status, headers);
        }

        /// <summary>
        ///     Returns the specified content to the client as a single concatenated piece of text with the specified MIME
        ///     type.</summary>
        /// <param name="content">
        ///     Content to return to the client.</param>
        /// <param name="contentType">
        ///     MIME type to use. This overrides any MIME type specified in <paramref name="headers"/> (if any).</param>
        /// <param name="status">
        ///     HTTP status code to use in the response.</param>
        /// <param name="headers">
        ///     Headers to use in the response, or null to use default values.</param>
        /// <param name="buffered">
        ///     If true (default), the output is buffered for performance; otherwise, all text is transmitted as soon as
        ///     possible.</param>
        public static HttpResponseContent Create(IEnumerable<string> content, string contentType, HttpStatusCode status = HttpStatusCode._200_OK, HttpResponseHeaders headers = null, bool buffered = true)
        {
            return create(() => new DynamicContentStream(content, buffered), contentType, status, headers);
        }

        /// <summary>
        ///     Returns the contents of the specified byte array to the client with the specified MIME type.</summary>
        /// <param name="content">
        ///     Content to return to the client.</param>
        /// <param name="contentType">
        ///     MIME type to use. This overrides any MIME type specified in <paramref name="headers"/> (if any).</param>
        /// <param name="status">
        ///     HTTP status code to use in the response.</param>
        /// <param name="headers">
        ///     Headers to use in the response, or null to use default values.</param>
        public static HttpResponseContent Create(byte[] content, string contentType, HttpStatusCode status = HttpStatusCode._200_OK, HttpResponseHeaders headers = null)
        {
            return create(() => new MemoryStream(content), contentType, status, headers);
        }

        /// <summary>
        ///     Returns the contents of the specified stream to the client with the specified MIME type.</summary>
        /// <param name="content">
        ///     Content to return to the client.</param>
        /// <param name="contentType">
        ///     MIME type to use. This overrides any MIME type specified in <paramref name="headers"/> (if any).</param>
        /// <param name="status">
        ///     HTTP status code to use in the response.</param>
        /// <param name="headers">
        ///     Headers to use in the response, or null to use default values.</param>
        public static HttpResponseContent Create(Stream content, string contentType, HttpStatusCode status = HttpStatusCode._200_OK, HttpResponseHeaders headers = null)
        {
            var used = false;
            return create(content == null ? null : new Func<Stream>(() =>
            {
                if (used)
                    throw new InvalidOperationException("You cannot re-use an HttpResponse instance constructed from a Stream for multiple separate HTTP requests. Instead, construct a new Stream and HttpResponse from within the request handler.");
                used = true;
                return content;
            }), contentType, status, headers);
        }

        private static HttpResponseContent create(Func<Stream> getContentStream, string contentType, HttpStatusCode status, HttpResponseHeaders headers)
        {
            if (!status.MayHaveBody() && getContentStream != null)
                throw new InvalidOperationException("A response with the {0} status cannot have a body.".Fmt(status));
            if (!status.MayHaveBody() && (contentType != null || (headers != null && headers.ContentType != null)))
                throw new InvalidOperationException("A response with the {0} status cannot have a Content-Type header.".Fmt(status));

            headers = headers ?? new HttpResponseHeaders();
            headers.ContentType = contentType ?? headers.ContentType;
            return new HttpResponseContent(status, headers, getContentStream);
        }

        public static HttpResponseWebSocket WebSocket(Func<WebSocket> getWebsocket, string subprotocol = null, HttpResponseHeaders headers = null)
        {
            if (getWebsocket == null)
                throw new ArgumentNullException("getWebsocket");
            return new HttpResponseWebSocket(getWebsocket, subprotocol, headers);
        }
    }
}
