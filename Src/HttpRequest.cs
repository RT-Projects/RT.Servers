using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using RT.Util.ExtensionMethods;

namespace RT.Servers
{
    /// <summary>
    /// An HTTP request handler is a function that takes an HTTP request (<see cref="HttpRequest"/>) and returns an HTTP response (<see cref="HttpResponse"/>).
    /// </summary>
    /// <param name="request">The HTTP request to be processed.</param>
    /// <returns>The HTTP response generated from the HTTP request.</returns>
    public delegate HttpResponse HttpRequestHandler(HttpRequest request);

    /// <summary>
    /// Encapsulates all supported HTTP request headers. These will be set by the server when it receives the request.
    /// </summary>
    public class HttpRequestHeaders
    {
#pragma warning disable 1591    // Missing XML comment for publicly visible type or member
        public string[] Accept;
        public string[] AcceptCharset;
        public HttpContentEncoding[] AcceptEncoding;
        public string[] AcceptLanguage;
        public HttpConnection Connection;
        public long? ContentLength;                 // required only for POST
        public HttpPostContentType ContentType;     // required only for POST
        public string ContentMultipartBoundary;     // required only for POST and only if ContentType == HttpPostContentType.MultipartFormData
        public Dictionary<string, Cookie> Cookie;
        public Dictionary<string, string> Expect;
        public string Host;
        public DateTime? IfModifiedSince;
        public string IfNoneMatch;
        public HttpRange[] Range;
        public string UserAgent;
#pragma warning restore 1591    // Missing XML comment for publicly visible type or member

        /// <summary>Stores the header values pertaining to headers not supported by <see cref="HttpRequestHeaders"/> as raw strings.</summary>
        public Dictionary<string, string> UnrecognisedHeaders;
    }

    /// <summary>
    /// Encapsulates an HTTP request, including its method, URL and headers. <see cref="HttpServer"/> generates this when it receives an
    /// HTTP request and passes it to the relevant <see cref="HttpRequestHandler"/>.
    /// </summary>
    public class HttpRequest
    {
        private struct FieldsCache
        {
            public Dictionary<string, string> ValueCache;
            public Dictionary<string, List<string>> ArrayCache;
            public Dictionary<string, FileUpload> FileCache;
        }
        private string _url;
        private FieldsCache GetFieldsCache;
        private FieldsCache PostFieldsCache;

        /// <summary>
        /// Contains the part of the URL that follows the path where the request handler is hooked.
        /// <see cref="BaseUrl"/> + RestURL is equal to <see cref="Url"/>.
        /// </summary>
        /// <example>
        ///     Consider the following example code:
        ///     <code>
        ///         HttpServer MyServer = new HttpServer();
        ///         MyServer.AddHandler(new HttpRequestHandlerHook { Path = "/homepages", Handler = MyHandler });
        ///     </code>
        ///     In the above example, an HTTP request for the URL <c>http://www.mydomain.com/homepages/a/adam</c>
        ///     would have the RestURL field set to the value <c>/a/adam</c>. Note the leading slash.
        /// </example>
        public string RestUrl;

        /// <summary>
        /// Contains the part of the URL to which the request handler is hooked.
        /// BaseURL + <see cref="RestUrl"/> is equal to <see cref="Url"/>.
        /// For an example, see <see cref="RestUrl"/>.
        /// </summary>
        public string BaseUrl;

        /// <summary>
        /// Stores the domain name from the Host header, without the port number.
        /// </summary>
        public string Domain;

        /// <summary>
        /// Contains the part of the domain that precedes the domain where the request handler is hooked.
        /// RestDomain + <see cref="BaseDomain"/> is equal to <see cref="Domain"/>.
        /// </summary>
        /// <example>
        ///     Consider the following example code:
        ///     <code>
        ///         HttpServer MyServer = new HttpServer();
        ///         MyServer.AddHandler(new HttpRequestHandlerHook { Domain = "homepages.mydomain.com", Handler = MyHandler });
        ///     </code>
        ///     In the above example, an HTTP request for the URL <c>http://peter.schmidt.homepages.mydomain.com/</c>
        ///     would have the RestDomain field set to the value <c>peter.schmidt.</c>. Note the trailing dot.
        /// </example>
        public string RestDomain;

        /// <summary>
        /// Contains the part of the domain to which the request handler is hooked.
        /// <see cref="RestDomain"/> + BaseDomain is equal to <see cref="Domain"/>.
        /// For an example see <see cref="RestDomain"/>.
        /// </summary>
        public string BaseDomain;

        /// <summary>
        /// The HTTP request method (GET, POST or HEAD).
        /// </summary>
        public HttpMethod Method;

        /// <summary>
        /// The HTTP request headers that were received and understood by <see cref="HttpServer"/>.
        /// </summary>
        public HttpRequestHeaders Headers = new HttpRequestHeaders();

        /// <summary>
        /// The directory to use for temporary files if the request is a POST request and contains a file upload.
        /// This can be set before <see cref="FileUploads"/>, <see cref="Post"/> or <see cref="PostArr"/> is called for the first time.
        /// After the first call to any of these, file uploads will already have been processed.
        /// </summary>
        public string TempDir;

        /// <summary>
        /// Identifies the client that sent this request.
        /// </summary>
        public IPEndPoint OriginIP;

        /// <summary>
        /// Contains a stream providing read access to the content of a POST request.
        /// NULL if the request is a GET or HEAD.
        /// </summary>
        internal Stream Content;

        /// <summary>
        /// Contains the delegate function used to handle this request.
        /// </summary>
        internal HttpRequestHandler Handler;

        /// <summary>
        /// Contains the path and filename of a temporary file that has been used to store the POST request content, if any.
        /// <see cref="HttpServer"/> uses this field to keep track of it and delete the file when it is no longer needed.
        /// </summary>
        internal string TemporaryFile;

        /// <summary>
        /// A default constructor that initialises all fields to their defaults.
        /// </summary>
        public HttpRequest()
        {
        }

        /// <summary>
        /// DO NOT USE THIS CONSTRUCTOR except in unit testing.
        /// </summary>
        /// <param name="content">DO NOT USE THIS CONSTRUCTOR except in unit testing.</param>
        public HttpRequest(Stream content)
        {
            Content = content;
        }

        /// <summary>
        /// The URL of the request, not including the domain, but including GET query parameters if any.
        /// </summary>
        public string Url
        {
            get { return _url; }
            set { _url = value; GetFieldsCache = new FieldsCache(); }
        }

        /// <summary>
        /// The URL of the request, not including the domain or any GET query parameters.
        /// </summary>
        public string UrlWithoutQuery
        {
            get { return _url.Contains('?') ? _url.Remove(_url.IndexOf('?')) : _url; }
        }

        /// <summary>
        /// The <see cref="RestUrl"/> (q.v.) of the request, not including the domain or any GET query parameters.
        /// </summary>
        public string RestUrlWithoutQuery
        {
            get { return RestUrl.Contains('?') ? RestUrl.Remove(RestUrl.IndexOf('?')) : RestUrl; }
        }

        /// <summary>
        /// Provides access to GET query parameters that are individual values.
        /// </summary>
        /// <seealso cref="GetArr"/>
        public Dictionary<string, string> Get
        {
            get
            {
                if (!_url.Contains('?'))
                    return new Dictionary<string, string>();
                if (GetFieldsCache.ValueCache == null)
                    GetFieldsCache = parseQueryParameters(new MemoryStream(Encoding.ASCII.GetBytes(_url.Substring(_url.IndexOf('?') + 1))));
                return GetFieldsCache.ValueCache;
            }
        }

        /// <summary>
        /// Provides access to GET query parameters that are arrays.
        /// </summary>
        /// <seealso cref="Get"/>
        public Dictionary<string, List<string>> GetArr
        {
            get
            {
                if (!_url.Contains('?'))
                    return new Dictionary<string, List<string>>();
                if (GetFieldsCache.ArrayCache == null)
                    GetFieldsCache = parseQueryParameters(new MemoryStream(Encoding.ASCII.GetBytes(_url.Substring(_url.IndexOf('?') + 1))));
                return GetFieldsCache.ArrayCache;
            }
        }

        /// <summary>
        /// Provides access to POST query parameters that are individual values (empty if the request is not a POST request).
        /// </summary>
        /// <seealso cref="PostArr"/>
        public Dictionary<string, string> Post
        {
            get
            {
                if (Content == null)
                    return new Dictionary<string, string>();
                if (PostFieldsCache.ValueCache == null)
                    PostFieldsCache = parsePostParameters();
                return PostFieldsCache.ValueCache;
            }
        }

        /// <summary>
        /// Provides access to POST query parameters that are arrays (empty if the request is not a POST request).
        /// </summary>
        /// <seealso cref="Post"/>
        public Dictionary<string, List<string>> PostArr
        {
            get
            {
                if (Content == null)
                    return new Dictionary<string, List<string>>();
                if (PostFieldsCache.ArrayCache == null)
                    PostFieldsCache = parsePostParameters();
                return PostFieldsCache.ArrayCache;
            }
        }

        /// <summary>
        /// Contains information about file uploads included in a POST request. Empty if the request is not a POST request.
        /// </summary>
        public Dictionary<string, FileUpload> FileUploads
        {
            get
            {
                if (Content == null)
                    return new Dictionary<string, FileUpload>();
                if (PostFieldsCache.FileCache == null)
                    PostFieldsCache = parsePostParameters();
                return PostFieldsCache.FileCache;
            }
        }

        private FieldsCache parsePostParameters()
        {
            if (Headers.ContentType == HttpPostContentType.ApplicationXWwwFormUrlEncoded)
                return parseQueryParameters(Content);
            FieldsCache fc = new FieldsCache
            {
                ArrayCache = new Dictionary<string, List<string>>(),
                ValueCache = new Dictionary<string, string>(),
                FileCache = new Dictionary<string, FileUpload>()
            };

            // An excessively long boundary is going to screw up the following algorithm.
            // (Actually a limit of up to 65527 would work, but I think 1024 is more than enough.)
            if (Headers.ContentMultipartBoundary == null || Headers.ContentMultipartBoundary.Length > 1024)
                return fc;

            // Process POST request upload data

            byte[] buffer = new byte[65536];
            int bytesRead = Content.Read(buffer, 0, 65536);
            // We expect the input to begin with "--" followed by the boundary followed by "\r\n"
            string expecting = "--" + Headers.ContentMultipartBoundary + "\r\n";
            string stuffRead = Encoding.ASCII.GetString(buffer, 0, bytesRead);
            int prevLength = 0;
            while (stuffRead.Length < expecting.Length)
            {
                bytesRead = Content.Read(buffer, 0, 65536);
                prevLength = stuffRead.Length;
                stuffRead += Encoding.ASCII.GetString(buffer, 0, bytesRead);
            }
            if (stuffRead.Substring(0, expecting.Length) != expecting)
                return fc;
            int bufferIndex = bytesRead + expecting.Length - stuffRead.Length;
            bytesRead -= bufferIndex;

            // Now comes the main reading loop
            bool processingHeaders = true;
            string currentHeaders = "";
            string currentFieldName = null;
            Stream currentWritingStream = null;
            while (bufferIndex > 0 || bytesRead > 0)
            {
                int writeIndex = 0;
                if (bytesRead > 0)
                {
                    if (processingHeaders)
                    {
                        int prevCHLength = currentHeaders.Length;
                        currentHeaders += Encoding.ASCII.GetString(buffer, bufferIndex, bytesRead);
                        if (currentHeaders.Contains("\r\n\r\n"))
                        {
                            int pos = currentHeaders.IndexOf("\r\n\r\n");
                            currentHeaders = currentHeaders.Remove(pos);
                            bufferIndex += pos - prevCHLength + 4;
                            bytesRead -= pos - prevCHLength + 4;
                            string fileName = null;
                            string contentType = null;
                            foreach (string header in currentHeaders.Split(new string[] { "\r\n" }, StringSplitOptions.None))
                            {
                                Match m = Regex.Match(header, @"^content-disposition\s*:\s*form-data\s*;(.*)$", RegexOptions.IgnoreCase);
                                if (m.Success)
                                {
                                    string v = m.Groups[1].Value;
                                    while (v.Length > 0)
                                    {
                                        m = Regex.Match(v, @"^\s*(\w+)=""([^""]*)""\s*(?:;\s*|$)");
                                        if (!m.Success)
                                            m = Regex.Match(v, @"^\s*(\w+)=([^;]*)\s*(?:;\s*|$)");
                                        if (!m.Success)
                                            break;
                                        if (m.Groups[1].Value.ToLowerInvariant() == "name")
                                            currentFieldName = m.Groups[2].Value;
                                        else if (m.Groups[1].Value.ToLowerInvariant() == "filename")
                                            fileName = m.Groups[2].Value;
                                        v = v.Substring(m.Length);
                                    }
                                }
                                else
                                {
                                    m = Regex.Match(header, @"^content-type\s*:\s*(.*)$", RegexOptions.IgnoreCase);
                                    if (m.Success)
                                        contentType = m.Groups[1].Value;
                                }
                            }
                            if (fileName == null && currentFieldName != null)
                                currentWritingStream = new MemoryStream();
                            else if (fileName != null && currentFieldName != null)
                            {
                                string tempFile = HttpInternalObjects.RandomTempFilepath(TempDir, out currentWritingStream);
                                fc.FileCache[currentFieldName] = new FileUpload
                                {
                                    ContentType = contentType,
                                    Filename = fileName,
                                    LocalTempFilename = tempFile
                                };
                            }
                            processingHeaders = false;
                            continue;
                        }
                    }
                    else if (bytesRead >= Headers.ContentMultipartBoundary.Length + 8)   // processing content
                    {
                        // This will convert non-ASCII bytes to question marks, but that's OK because we use this only to find the boundary
                        string data = Encoding.ASCII.GetString(buffer, bufferIndex, bytesRead);
                        bool sepFound = false;
                        int sepIndex = 0;
                        bool end = false;
                        if (data.Contains("\r\n--" + Headers.ContentMultipartBoundary + "--\r\n"))
                        {
                            sepFound = true;
                            sepIndex = data.IndexOf("\r\n--" + Headers.ContentMultipartBoundary + "--\r\n");
                            end = true;
                        }
                        if (data.Contains("\r\n--" + Headers.ContentMultipartBoundary + "\r\n"))
                        {
                            int pos = data.IndexOf("\r\n--" + Headers.ContentMultipartBoundary + "\r\n");
                            if (!sepFound || pos < sepIndex)
                            {
                                sepFound = true;
                                sepIndex = data.IndexOf("\r\n--" + Headers.ContentMultipartBoundary + "\r\n");
                                end = false;
                            }
                        }

                        if (sepFound)
                        {
                            // Write the rest of the data to the output stream and then process the separator
                            if (sepIndex > 0) currentWritingStream.Write(buffer, bufferIndex, sepIndex);
                            currentWritingStream.Close();
                            // Note that CurrentWritingStream is either a MemoryStream or a FileStream.
                            // If it is a FileStream, then the relevant entry to fc.FileCache has already been made.
                            // Only if it is a MemoryStream, we need to process the stuff here.
                            if (currentWritingStream is MemoryStream && currentFieldName.EndsWith("[]"))
                                fc.ArrayCache.AddSafe(
                                    currentFieldName.Remove(currentFieldName.Length - 2),
                                    Encoding.UTF8.GetString(((MemoryStream) currentWritingStream).ToArray())
                                );
                            else if (currentWritingStream is MemoryStream)
                                fc.ValueCache[currentFieldName] = Encoding.UTF8.GetString(((MemoryStream) currentWritingStream).ToArray());

                            if (end)
                                break;

                            processingHeaders = true;
                            currentHeaders = "";
                            bufferIndex += sepIndex + Headers.ContentMultipartBoundary.Length + 6;
                            bytesRead -= sepIndex + Headers.ContentMultipartBoundary.Length + 6;
                            continue;
                        }
                        else
                        {
                            // Write some of the data to the output stream, but leave enough so that we can still recognise the boundary
                            int howMuchToWrite = bytesRead - Headers.ContentMultipartBoundary.Length - 8;
                            if (howMuchToWrite > 0)
                                currentWritingStream.Write(buffer, bufferIndex, howMuchToWrite);
                            byte[] newBuffer = new byte[65536];
                            Array.Copy(buffer, bufferIndex + howMuchToWrite, newBuffer, 0, bytesRead - howMuchToWrite);
                            buffer = newBuffer;
                            bufferIndex = 0;
                            bytesRead -= howMuchToWrite;
                            writeIndex = bytesRead;
                        }
                    }
                    else if (bufferIndex > 0)
                    {
                        byte[] newBuffer = new byte[65536];
                        Array.Copy(buffer, bufferIndex, newBuffer, 0, bytesRead);
                        buffer = newBuffer;
                        writeIndex = bytesRead;
                    }
                }
                bufferIndex = 0;
                // We need to read enough data to contain the boundary
                do
                {
                    bytesRead = Content.Read(buffer, writeIndex, 65536 - writeIndex);
                    if (bytesRead == 0) // premature end of content
                    {
                        if (currentWritingStream != null)
                            currentWritingStream.Close();
                        return fc;
                    }
                    writeIndex += bytesRead;
                } while (writeIndex < Headers.ContentMultipartBoundary.Length + 8);
                bytesRead = writeIndex;
            }

            return fc;
        }

        /// <summary>
        /// Decodes an ASCII-encoded stream of characters into key-value pairs.
        /// </summary>
        /// <param name="s">Stream to read from.</param>
        /// <returns>A dictionary containing only the single-valued keys. For array-valued keys, use <see cref="ParseQueryArrayParameters"/>.</returns>
        public static Dictionary<string, string> ParseQueryValueParameters(Stream s)
        {
            FieldsCache fc = parseQueryParameters(s);
            return fc.ValueCache;
        }

        /// <summary>
        /// Decodes an ASCII-encoded stream of characters into key-value pairs.
        /// </summary>
        /// <param name="s">Stream to read from.</param>
        /// <returns>A dictionary containing only the array-valued keys. For single-valued keys, use <see cref="ParseQueryValueParameters"/>.</returns>
        public static Dictionary<string, List<string>> ParseQueryArrayParameters(Stream s)
        {
            FieldsCache fc = parseQueryParameters(s);
            return fc.ArrayCache;
        }

        private static FieldsCache parseQueryParameters(Stream s)
        {
            FieldsCache fc = new FieldsCache
            {
                ValueCache = new Dictionary<string, string>(),
                ArrayCache = new Dictionary<string, List<string>>(),
                FileCache = new Dictionary<string, FileUpload>()
            };

            byte[] buffer = new byte[65536];
            int bytesRead = s.Read(buffer, 0, buffer.Length);
            int bufferIndex = 0;
            string curKey = "";
            string curValue = null;

            var fnAdd = new Action<string, string>((key, val) =>
            {
                key = key.UrlUnescape();
                val = val.UrlUnescape();
                if (key.EndsWith("[]"))
                    fc.ArrayCache.AddSafe(key.Remove(key.Length - 2), val);
                else
                    fc.ValueCache[key] = val;
            });

            bool inKey = true;
            while (bytesRead > 0)
            {
                while (bufferIndex < bytesRead)
                {
                    int i = bufferIndex;
                    while (i < bytesRead && buffer[i] != '&' && buffer[i] != '=')
                        i++;
                    if (i == bytesRead)
                    {
                        if (inKey)
                            curKey += Encoding.ASCII.GetString(buffer, bufferIndex, i - bufferIndex);
                        else
                            curValue += Encoding.ASCII.GetString(buffer, bufferIndex, i - bufferIndex);
                        bufferIndex = i;
                    }
                    else if (buffer[i] == (byte) '=')
                    {
                        if (inKey)
                        {
                            curKey += Encoding.ASCII.GetString(buffer, bufferIndex, i - bufferIndex);
                            curValue = "";
                            inKey = false;
                        }
                        else
                            curValue += Encoding.ASCII.GetString(buffer, bufferIndex, i - bufferIndex) + "=";
                        bufferIndex = i + 1;
                    }
                    else if (buffer[i] == (byte) '&')
                    {
                        if (inKey)
                            curKey += Encoding.ASCII.GetString(buffer, bufferIndex, i - bufferIndex) + "&";
                        else
                        {
                            curValue += Encoding.ASCII.GetString(buffer, bufferIndex, i - bufferIndex);
                            fnAdd(curKey, curValue);
                            curKey = "";
                            curValue = null;
                            inKey = true;
                        }
                        bufferIndex = i + 1;
                    }
                }
                bytesRead = s.Read(buffer, 0, buffer.Length);
                bufferIndex = 0;
            }

            if (curValue != null)
                fnAdd(curKey, curValue);

            s.Close();
            return fc;
        }
    }

    /// <summary>
    /// Exception thrown to indicate error during processing of the request. Usually this exception is caught to generate an HTTP 500 Internal Server Error.
    /// </summary>
    public class InvalidRequestException : Exception
    {
        /// <summary>
        /// Response to return when the exception is caught. Usually <see cref="HttpServer.ErrorResponse(HttpStatusCode)"/> is used to generate an HTTP 500 Internal Server Error.
        /// </summary>
        public HttpResponse Response;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="response">Response to return when the exception is caught.
        /// Usually <see cref="HttpServer.ErrorResponse(HttpStatusCode)"/> is used to generate an HTTP 500 Internal Server Error.</param>
        public InvalidRequestException(HttpResponse response) { Response = response; }
    }
}
