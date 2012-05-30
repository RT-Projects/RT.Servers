using System;
using System.Collections.Generic;
using System.IO;
using RT.TagSoup;
using RT.Util.ExtensionMethods;
using RT.Util.Streams;

namespace RT.Servers
{
    /// <summary>
    /// Encapsulates an HTTP response, to be sent by <see cref="HttpServer"/> to the HTTP client that sent the original request.
    /// A request handler must return an HttpResponse object to the <see cref="HttpServer"/> when handling a request.
    /// </summary>
    public sealed class HttpResponse
    {
        /// <summary>
        /// The HTTP status code. For example, 200 OK, 404 Not Found, 500 Internal Server Error.
        /// Default is 200 OK.
        /// </summary>
        public HttpStatusCode Status = HttpStatusCode._200_OK;

        /// <summary>
        /// The HTTP response headers which are to be sent back to the HTTP client as part of this HTTP response.
        /// If not set or modified, will default to a standard set of headers - see <see cref="HttpResponseHeaders"/>.
        /// </summary>
        public HttpResponseHeaders Headers = new HttpResponseHeaders();

        /// <summary>
        /// A stream object providing read access to the content returned. For static files, use <see cref="FileStream"/>.
        /// For objects cached in memory, use <see cref="MemoryStream"/>.
        /// For dynamic websites, consider using <see cref="RT.Util.Streams.DynamicContentStream"/>.
        /// </summary>
        public Stream Content;

        /// <summary>Specifies whether gzip should be used.</summary>
        public UseGzipOption UseGzip = UseGzipOption.AutoDetect;

        /// <summary>Specifies an action to perform when the request finishes.</summary>
        public Action CleanUpCallback;

        private HttpResponse() { }

        /// <summary>Returns the specified file from the local file system using the specified MIME content type to the client.</summary>
        /// <param name="filePath">Full path and filename of the file to return.</param>
        /// <param name="contentType">MIME type to use in the Content-Type header. If null, the first kilobyte will be looked at to choose between the plaintext and the octet-stream content type.</param>
        /// <param name="maxAge">Specifies the value for the CacheControl max-age header on the file served. Set to null to prevent this header being sent.</param>
        /// <param name="ifModifiedSince">If specified, a 304 Not Modified will be served if the file's last modified timestamp is at or before this time.</param>
        public static HttpResponse File(string filePath, string contentType, int? maxAge = 3600, DateTime? ifModifiedSince = null)
        {
            try
            {
                var timestamp = System.IO.File.GetLastWriteTimeUtc(filePath).TruncatedToSeconds();
                if (timestamp <= ifModifiedSince)
                    return NotModified();

                var fileStream = System.IO.File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

                // Do a limited amount of content-type guessing if necessary.
                if (contentType == null)
                {
                    // Look at the first 1 KB. If there are special control characters in it, it's likely a binary file. Otherwise, output as text/plain.
                    byte[] buf = new byte[1024];
                    int bytesRead = fileStream.FillBuffer(buf, 0, buf.Length);
                    fileStream.Position = 0;
                    contentType = "text/plain; charset=utf-8";
                    for (int i = 0; i < bytesRead; i++)
                        if (buf[i] < 32 && buf[i] != 9 && buf[i] != 10 && buf[i] != 13)
                        {
                            contentType = "application/octet-stream";
                            break;
                        }
                }

                return new HttpResponse
                {
                    Status = HttpStatusCode._200_OK,
                    Content = fileStream,
                    Headers = new HttpResponseHeaders
                    {
                        ContentType = contentType,
                        LastModified = timestamp,
                        CacheControl = maxAge == null ? null : new[] { new HttpCacheControl { State = HttpCacheControlState.MaxAge, IntParameter = maxAge.Value } },
                    }
                };
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

        /// <summary>Redirects the client to a new URL, using the HTTP status code 302 Found and making the response uncacheable.</summary>
        /// <param name="newUrl">URL to redirect the client to.</param>
        public static HttpResponse Redirect(string newUrl)
        {
            return new HttpResponse
            {
                Headers = new HttpResponseHeaders
                {
                    Location = newUrl,
                    CacheControl = new[] { new HttpCacheControl { State = HttpCacheControlState.Private }, new HttpCacheControl { State = HttpCacheControlState.MaxAge, IntParameter = 0 } },
                },
                Status = HttpStatusCode._302_Found,
            };
        }

        /// <summary>Generates a 304 Not Modified response.</summary>
        public static HttpResponse NotModified()
        {
            return new HttpResponse { Status = HttpStatusCode._304_NotModified };
        }

        /// <summary>Returns a response to the client consisting of an empty body.</summary>
        /// <param name="status">HTTP status code to use in the response.</param>
        /// <param name="headers">Headers to use in the response, or null to use default values.</param>
        public static HttpResponse Empty(HttpStatusCode status = HttpStatusCode._200_OK, HttpResponseHeaders headers = null)
        {
            return new HttpResponse { Status = status, Headers = headers ?? new HttpResponseHeaders() };
        }


        /// <summary>Returns the specified tag content to the client.</summary>
        /// <param name="content">Content to return to the client.</param>
        /// <param name="status">HTTP status code to use in the response.</param>
        /// <param name="headers">Headers to use in the response, or null to use default values.</param>
        /// <param name="buffered">If true (default), the output is buffered for performance; otherwise, all text is transmitted as soon as possible.</param>
        public static HttpResponse Html(Tag content, HttpStatusCode status = HttpStatusCode._200_OK, HttpResponseHeaders headers = null, bool buffered = true)
        {
            return create(new DynamicContentStream(content.ToEnumerable(), buffered), "text/html; charset=utf-8", status, headers);
        }

        /// <summary>Returns the specified content to the client with the MIME type “text/html; charset=utf-8”.</summary>
        /// <param name="content">Content to return to the client.</param>
        /// <param name="status">HTTP status code to use in the response.</param>
        /// <param name="headers">Headers to use in the response, or null to use default values.</param>
        public static HttpResponse Html(string content, HttpStatusCode status = HttpStatusCode._200_OK, HttpResponseHeaders headers = null)
        {
            return create(new MemoryStream(content.ToUtf8()), "text/html; charset=utf-8", status, headers);
        }

        /// <summary>Returns the specified content to the client as a single concatenated piece of text with the MIME type “text/html; charset=utf-8”.</summary>
        /// <param name="content">Content to return to the client.</param>
        /// <param name="status">HTTP status code to use in the response.</param>
        /// <param name="headers">Headers to use in the response, or null to use default values.</param>
        /// <param name="buffered">If true (default), the output is buffered for performance; otherwise, all text is transmitted as soon as possible.</param>
        public static HttpResponse Html(IEnumerable<string> content, HttpStatusCode status = HttpStatusCode._200_OK, HttpResponseHeaders headers = null, bool buffered = true)
        {
            return create(new DynamicContentStream(content, buffered), "text/html; charset=utf-8", status, headers);
        }

        /// <summary>Returns the contents of the specified byte array to the client.</summary>
        /// <param name="content">Content to return to the client.</param>
        /// <param name="status">HTTP status code to use in the response.</param>
        /// <param name="headers">Headers to use in the response, or null to use default values.</param>
        public static HttpResponse Html(byte[] content, HttpStatusCode status = HttpStatusCode._200_OK, HttpResponseHeaders headers = null)
        {
            return create(new MemoryStream(content), "text/html; charset=utf-8", status, headers);
        }

        /// <summary>Returns the contents of the specified stream to the client.</summary>
        /// <param name="content">Content to return to the client.</param>
        /// <param name="status">HTTP status code to use in the response.</param>
        /// <param name="headers">Headers to use in the response, or null to use default values.</param>
        public static HttpResponse Html(Stream content, HttpStatusCode status = HttpStatusCode._200_OK, HttpResponseHeaders headers = null)
        {
            return create(content, "text/html; charset=utf-8", status, headers);
        }

        /// <summary>Returns the specified content to the client with the MIME type “text/plain; charset=utf-8”.</summary>
        /// <param name="content">Content to return to the client.</param>
        /// <param name="status">HTTP status code to use in the response.</param>
        /// <param name="headers">Headers to use in the response, or null to use default values.</param>
        public static HttpResponse PlainText(string content, HttpStatusCode status = HttpStatusCode._200_OK, HttpResponseHeaders headers = null)
        {
            return create(new MemoryStream(content.ToUtf8()), "text/plain; charset=utf-8", status, headers);
        }

        /// <summary>Returns the specified content to the client as a single concatenated piece of text with the MIME type “text/plain; charset=utf-8”.</summary>
        /// <param name="content">Content to return to the client.</param>
        /// <param name="status">HTTP status code to use in the response.</param>
        /// <param name="headers">Headers to use in the response, or null to use default values.</param>
        /// <param name="buffered">If true (default), the output is buffered for performance; otherwise, all text is transmitted as soon as possible.</param>
        public static HttpResponse PlainText(IEnumerable<string> content, HttpStatusCode status = HttpStatusCode._200_OK, HttpResponseHeaders headers = null, bool buffered = true)
        {
            return create(new DynamicContentStream(content, buffered), "text/plain; charset=utf-8", status, headers);
        }

        /// <summary>Returns the contents of the specified byte array to the client.</summary>
        /// <param name="content">Content to return to the client.</param>
        /// <param name="status">HTTP status code to use in the response.</param>
        /// <param name="headers">Headers to use in the response, or null to use default values.</param>
        public static HttpResponse PlainText(byte[] content, HttpStatusCode status = HttpStatusCode._200_OK, HttpResponseHeaders headers = null)
        {
            return create(new MemoryStream(content), "text/plain; charset=utf-8", status, headers);
        }

        /// <summary>Returns the contents of the specified stream to the client.</summary>
        /// <param name="content">Content to return to the client.</param>
        /// <param name="status">HTTP status code to use in the response.</param>
        /// <param name="headers">Headers to use in the response, or null to use default values.</param>
        public static HttpResponse PlainText(Stream content, HttpStatusCode status = HttpStatusCode._200_OK, HttpResponseHeaders headers = null)
        {
            return create(content, "text/plain; charset=utf-8", status, headers);
        }

        /// <summary>Returns the specified content to the client with the MIME type “text/javascript; charset=utf-8”.</summary>
        /// <param name="content">Content to return to the client.</param>
        /// <param name="status">HTTP status code to use in the response.</param>
        /// <param name="headers">Headers to use in the response, or null to use default values.</param>
        public static HttpResponse JavaScript(string content, HttpStatusCode status = HttpStatusCode._200_OK, HttpResponseHeaders headers = null)
        {
            return create(new MemoryStream(content.ToUtf8()), "text/javascript; charset=utf-8", status, headers);
        }

        /// <summary>Returns the specified content to the client as a single concatenated piece of text with the MIME type “text/javascript; charset=utf-8”.</summary>
        /// <param name="content">Content to return to the client.</param>
        /// <param name="status">HTTP status code to use in the response.</param>
        /// <param name="headers">Headers to use in the response, or null to use default values.</param>
        /// <param name="buffered">If true (default), the output is buffered for performance; otherwise, all text is transmitted as soon as possible.</param>
        public static HttpResponse JavaScript(IEnumerable<string> content, HttpStatusCode status = HttpStatusCode._200_OK, HttpResponseHeaders headers = null, bool buffered = true)
        {
            return create(new DynamicContentStream(content, buffered), "text/javascript; charset=utf-8", status, headers);
        }

        /// <summary>Returns the contents of the specified byte array to the client.</summary>
        /// <param name="content">Content to return to the client.</param>
        /// <param name="status">HTTP status code to use in the response.</param>
        /// <param name="headers">Headers to use in the response, or null to use default values.</param>
        public static HttpResponse JavaScript(byte[] content, HttpStatusCode status = HttpStatusCode._200_OK, HttpResponseHeaders headers = null)
        {
            return create(new MemoryStream(content), "text/javascript; charset=utf-8", status, headers);
        }

        /// <summary>Returns the contents of the specified stream to the client.</summary>
        /// <param name="content">Content to return to the client.</param>
        /// <param name="status">HTTP status code to use in the response.</param>
        /// <param name="headers">Headers to use in the response, or null to use default values.</param>
        public static HttpResponse JavaScript(Stream content, HttpStatusCode status = HttpStatusCode._200_OK, HttpResponseHeaders headers = null)
        {
            return create(content, "text/javascript; charset=utf-8", status, headers);
        }

        /// <summary>Returns the specified content to the client with the MIME type “text/css; charset=utf-8”.</summary>
        /// <param name="content">Content to return to the client.</param>
        /// <param name="status">HTTP status code to use in the response.</param>
        /// <param name="headers">Headers to use in the response, or null to use default values.</param>
        public static HttpResponse Css(string content, HttpStatusCode status = HttpStatusCode._200_OK, HttpResponseHeaders headers = null)
        {
            return create(new MemoryStream(content.ToUtf8()), "text/css; charset=utf-8", status, headers);
        }

        /// <summary>Returns the specified content to the client as a single concatenated piece of text with the MIME type “text/css; charset=utf-8”.</summary>
        /// <param name="content">Content to return to the client.</param>
        /// <param name="status">HTTP status code to use in the response.</param>
        /// <param name="headers">Headers to use in the response, or null to use default values.</param>
        /// <param name="buffered">If true (default), the output is buffered for performance; otherwise, all text is transmitted as soon as possible.</param>
        public static HttpResponse Css(IEnumerable<string> content, HttpStatusCode status = HttpStatusCode._200_OK, HttpResponseHeaders headers = null, bool buffered = true)
        {
            return create(new DynamicContentStream(content, buffered), "text/css; charset=utf-8", status, headers);
        }

        /// <summary>Returns the contents of the specified byte array to the client.</summary>
        /// <param name="content">Content to return to the client.</param>
        /// <param name="status">HTTP status code to use in the response.</param>
        /// <param name="headers">Headers to use in the response, or null to use default values.</param>
        public static HttpResponse Css(byte[] content, HttpStatusCode status = HttpStatusCode._200_OK, HttpResponseHeaders headers = null)
        {
            return create(new MemoryStream(content), "text/css; charset=utf-8", status, headers);
        }

        /// <summary>Returns the contents of the specified stream to the client.</summary>
        /// <param name="content">Content to return to the client.</param>
        /// <param name="status">HTTP status code to use in the response.</param>
        /// <param name="headers">Headers to use in the response, or null to use default values.</param>
        public static HttpResponse Css(Stream content, HttpStatusCode status = HttpStatusCode._200_OK, HttpResponseHeaders headers = null)
        {
            return create(content, "text/css; charset=utf-8", status, headers);
        }

        /// <summary>Returns the specified content to the client with the specified MIME type.</summary>
        /// <param name="content">Content to return to the client.</param>
        /// <param name="contentType">MIME type to use. This overrides any MIME type specified in <paramref name="headers"/> (if any).</param>
        /// <param name="status">HTTP status code to use in the response.</param>
        /// <param name="headers">Headers to use in the response, or null to use default values.</param>
        public static HttpResponse Create(string content, string contentType, HttpStatusCode status = HttpStatusCode._200_OK, HttpResponseHeaders headers = null)
        {
            return create(new MemoryStream(content.ToUtf8()), contentType, status, headers);
        }

        /// <summary>Returns the specified content to the client as a single concatenated piece of text with the specified MIME type.</summary>
        /// <param name="content">Content to return to the client.</param>
        /// <param name="contentType">MIME type to use. This overrides any MIME type specified in <paramref name="headers"/> (if any).</param>
        /// <param name="status">HTTP status code to use in the response.</param>
        /// <param name="headers">Headers to use in the response, or null to use default values.</param>
        /// <param name="buffered">If true (default), the output is buffered for performance; otherwise, all text is transmitted as soon as possible.</param>
        public static HttpResponse Create(IEnumerable<string> content, string contentType, HttpStatusCode status = HttpStatusCode._200_OK, HttpResponseHeaders headers = null, bool buffered = true)
        {
            return create(new DynamicContentStream(content, buffered), contentType, status, headers);
        }

        /// <summary>Returns the contents of the specified byte array to the client with the specified MIME type.</summary>
        /// <param name="content">Content to return to the client.</param>
        /// <param name="contentType">MIME type to use. This overrides any MIME type specified in <paramref name="headers"/> (if any).</param>
        /// <param name="status">HTTP status code to use in the response.</param>
        /// <param name="headers">Headers to use in the response, or null to use default values.</param>
        public static HttpResponse Create(byte[] content, string contentType, HttpStatusCode status = HttpStatusCode._200_OK, HttpResponseHeaders headers = null)
        {
            return create(new MemoryStream(content), contentType, status, headers);
        }

        /// <summary>Returns the contents of the specified stream to the client with the specified MIME type.</summary>
        /// <param name="content">Content to return to the client.</param>
        /// <param name="contentType">MIME type to use. This overrides any MIME type specified in <paramref name="headers"/> (if any).</param>
        /// <param name="status">HTTP status code to use in the response.</param>
        /// <param name="headers">Headers to use in the response, or null to use default values.</param>
        public static HttpResponse Create(Stream content, string contentType, HttpStatusCode status = HttpStatusCode._200_OK, HttpResponseHeaders headers = null)
        {
            return create(content, contentType, status, headers);
        }


        private static HttpResponse create(Stream content, string contentType, HttpStatusCode status, HttpResponseHeaders headers)
        {
            headers = headers ?? new HttpResponseHeaders();
            headers.ContentType = contentType;
            return new HttpResponse { Content = content, Status = status, Headers = headers };
        }

        /// <summary>Modifies the <see cref="UseGzip"/> option in this response object and returns the same object.</summary>
        /// <param name="option">The new value for the <see cref="UseGzip"/> option.</param>
        public HttpResponse Set(UseGzipOption option)
        {
            UseGzip = option;
            return this;
        }
    }
}
