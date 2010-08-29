using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using RT.Util;
using RT.Util.Collections;
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
    /// Encapsulates a value with a Q rating, where Q is between 0 and 1. Provides a comparer such
    /// that the values with Q = 1 are the smallest.
    /// </summary>
    public struct QValue<T> : IComparable<QValue<T>>
    {
        private float _q;
        private T _value;

        /// <summary>Constructs a new q value</summary>
        public QValue(float q, T value)
        {
            _q = q;
            _value = value;
        }

        /// <summary>Gets the Q number</summary>
        public float Q
        {
            get { return _q; }
        }

        /// <summary>Gets the value via an implicit conversion</summary>
        public static implicit operator T(QValue<T> qv)
        {
            return qv._value;
        }

        /// <summary>Compares the Q number of this Q-value to the other one.</summary>
        public int CompareTo(QValue<T> other)
        {
            return -_q.CompareTo(other._q);
        }

        /// <summary>Converts the q value to a string.</summary>
        public override string ToString()
        {
            return "{0}; q={1:0.0}".Fmt(_value, _q);
        }
    }

    /// <summary>
    /// Encapsulates all supported HTTP request headers. These will be set by the server when it receives the request.
    /// </summary>
    public sealed class HttpRequestHeaders
    {
#pragma warning disable 1591    // Missing XML comment for publicly visible type or member
        public ListSorted<QValue<string>> Accept;
        public ListSorted<QValue<string>> AcceptCharset;
        public ListSorted<QValue<HttpContentEncoding>> AcceptEncoding;
        public ListSorted<QValue<string>> AcceptLanguage;
        public HttpConnection Connection;
        public long? ContentLength;                 // required only for POST
        public HttpPostContentType ContentType;     // required only for POST
        public string ContentMultipartBoundary;     // required only for POST and only if ContentType == HttpPostContentType.MultipartFormData
        public Dictionary<string, Cookie> Cookie = new Dictionary<string, Cookie>();
        public Dictionary<string, string> Expect;
        public string Host;
        public DateTime? IfModifiedSince;
        public string IfNoneMatch;
        public List<HttpRange> Range;
        public string UserAgent;
#pragma warning restore 1591    // Missing XML comment for publicly visible type or member

        /// <summary>Stores all the headers of the request as raw strings.</summary>
        public List<KeyValuePair<string, string>> AllHeaders = new List<KeyValuePair<string, string>>();
        /// <summary>Stores the header values pertaining to headers not supported by <see cref="HttpRequestHeaders"/> as raw strings.</summary>
        public List<KeyValuePair<string, string>> UnrecognisedHeaders = new List<KeyValuePair<string, string>>();

        /// <summary>
        /// Parses the specified header and stores it in this instance. If the header is not recognised in any way
        /// it will be stored in <see cref="UnrecognisedHeaders"/>. Returns whether the header was recognised.
        /// </summary>
        /// <param name="name">Header name</param>
        /// <param name="value">Header value</param>
        internal bool parseAndAddHeader(string name, string value)
        {
            string nameLower = name.ToLowerInvariant();
            int intOutput;
            bool recognised = false;

            try
            {
                if (nameLower == "accept")
                {
                    splitAndAddByQ(ref Accept, value);
                    recognised = true;
                }
                else if (nameLower == "accept-charset")
                {
                    splitAndAddByQ(ref AcceptCharset, value);
                    recognised = true;
                }
                else if (nameLower == "accept-encoding")
                {
                    splitAndAddByQ(ref AcceptEncoding, value, HttpEnumsParser.ParseHttpContentEncoding);
                    recognised = true;
                }
                else if (nameLower == "accept-language")
                {
                    splitAndAddByQ(ref AcceptLanguage, value);
                    recognised = true;
                }
                else if (nameLower == "connection" && Connection == HttpConnection.None)
                {
                    Connection = HttpEnumsParser.ParseHttpConnection(value);
                    recognised = true;
                }
                else if (nameLower == "content-length" && ContentLength == null && int.TryParse(value, out intOutput))
                {
                    ContentLength = intOutput;
                    recognised = true;
                }
                else if (nameLower == "content-type")
                {
                    var values = value.Split(';');
                    var firstValue = values[0].Trim();
                    if (string.Equals(firstValue, "application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
                    {
                        ContentType = HttpPostContentType.ApplicationXWwwFormUrlEncoded;
                        recognised = true;
                    }
                    else if (string.Equals(firstValue, "multipart/form-data", StringComparison.OrdinalIgnoreCase))
                    {
                        for (int i = 1; i < values.Length; i++)
                        {
                            var v = values[i].Trim();
                            if (v.StartsWith("boundary="))
                            {
                                ContentType = HttpPostContentType.MultipartFormData;
                                ContentMultipartBoundary = v.Substring("boundary=".Length);
                                recognised = true;
                            }
                        }
                    }
                }
                else if (nameLower == "cookie")
                {
                    parseAndAddCookies(ref Cookie, value);
                    recognised = true;
                }
                else if (nameLower == "host" && Host == null)
                {
                    Host = value.ToLowerInvariant();
                    recognised = true;
                }
                else if (nameLower == "expect")
                {
                    string hv = value;
                    Expect = new Dictionary<string, string>();
                    while (hv.Length > 0)
                    {
                        Match m = Regex.Match(hv, @"(^[^;=""]*?)\s*(;\s*|$)");
                        if (m.Success)
                        {
                            Expect.Add(m.Groups[1].Value.ToLowerInvariant(), "1");
                            hv = hv.Substring(m.Length);
                        }
                        else
                        {
                            m = Regex.Match(hv, @"^([^;=""]*?)\s*=\s*([^;=""]*?)\s*(;\s*|$)");
                            if (m.Success)
                            {
                                Expect.Add(m.Groups[1].Value.ToLowerInvariant(), m.Groups[2].Value.ToLowerInvariant());
                                hv = hv.Substring(m.Length);
                            }
                            else
                            {
                                m = Regex.Match(hv, @"^([^;=""]*?)\s*=\s*""([^""]*)""\s*(;\s*|$)");
                                if (m.Success)
                                {
                                    Expect.Add(m.Groups[1].Value.ToLowerInvariant(), m.Groups[2].Value);
                                    hv = hv.Substring(m.Length);
                                }
                                else
                                {
                                    Expect.Add(hv, "1");
                                    hv = "";
                                }
                            }
                        }
                    }
                    recognised = true;
                }
                else if (nameLower == "if-modified-since" && IfModifiedSince == null)
                {
                    DateTime output;
                    if (DateTime.TryParse(value, out output))
                    {
                        IfModifiedSince = output;
                        recognised = true;
                    }
                }
                else if (nameLower == "if-none-match" && IfNoneMatch == null)
                {
                    IfNoneMatch = value.ToLowerInvariant();
                    recognised = true;
                }
                else if (nameLower == "range" && value.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase))
                {
                    parseAndAddRange(ref Range, value);
                    recognised = true;
                }
                else if (nameLower == "user-agent" && UserAgent == null)
                {
                    UserAgent = value;
                    recognised = true;
                }
            }
            catch
            {
                // Ignore absolutely any error; the header will just simply be unrecognised.
            }

            AllHeaders.Add(new KeyValuePair<string, string>(name, value));

            if (!recognised)
                UnrecognisedHeaders.Add(new KeyValuePair<string, string>(name, value));

            return recognised;
        }

        /// <summary>
        /// Parses the cookie header and adds the cookies to the specified cookie dictionary.
        /// </summary>
        private static void parseAndAddCookies(ref Dictionary<string, Cookie> cookies, string cookieHeaderValue)
        {
            Cookie prevCookie = new Cookie { Name = null };
            while (cookieHeaderValue.Length > 0)
            {
                string key, value;
                Match m = Regex.Match(cookieHeaderValue, @"^\s*(\$?\w+)=([^;]*)(;\s*|$)");
                if (m.Success)
                {
                    key = m.Groups[1].Value;
                    value = m.Groups[2].Value;
                }
                else
                {
                    m = Regex.Match(cookieHeaderValue, @"^\s*(\$?\w+)=""([^""]*)""(;\s*|$)");
                    if (m.Success)
                    {
                        key = m.Groups[1].Value;
                        value = m.Groups[2].Value;
                    }
                    else
                    {
                        if (cookieHeaderValue.Contains(';'))
                        {
                            // Invalid syntax; try to continue parsing at the next ";"
                            cookieHeaderValue = cookieHeaderValue.Substring(cookieHeaderValue.IndexOf(';') + 1);
                            continue;
                        }
                        else
                            // Completely invalid syntax; ignore the rest of this header
                            return;
                    }
                }
                cookieHeaderValue = cookieHeaderValue.Substring(m.Groups[0].Length);

                if (key == "$Version")
                    continue;   // ignore that.

                if (cookies == null)
                    cookies = new Dictionary<string, Cookie>();

                if (key == "$Path" && prevCookie.Name != null)
                {
                    prevCookie.Path = value;
                    cookies[prevCookie.Name] = prevCookie;
                }
                else if (key == "$Domain" && prevCookie.Name != null)
                {
                    prevCookie.Domain = value;
                    cookies[prevCookie.Name] = prevCookie;
                }
                else if (key == "$Expires" && prevCookie.Name != null)
                {
                    DateTime output;
                    if (DateTime.TryParse(cookieHeaderValue, out output))
                    {
                        prevCookie.Expires = output;
                        cookies[prevCookie.Name] = prevCookie;
                    }
                }
                else
                {
                    prevCookie = new Cookie { Name = key, Value = value };
                    cookies[key] = prevCookie;
                }
            }
        }

        /// <summary>
        /// Parses the specified Range header and adds the ranges to the specified ranges list.
        /// </summary>
        private static void parseAndAddRange(ref List<HttpRange> ranges, string rangeHeaderValue)
        {
            foreach (var rangeStr in rangeHeaderValue.ToLowerInvariant().Split(','))
            {
                if (rangeStr == null || rangeStr.Length < 2)
                    return;
                Match m = Regex.Match(rangeStr, @"(\d*)-(\d*)");
                if (!m.Success)
                    return;
                if (ranges == null)
                    ranges = new List<HttpRange>();
                var range = new HttpRange();
                if (m.Groups[1].Length > 0)
                    range.From = int.Parse(m.Groups[1].Value);
                if (m.Groups[2].Length > 0)
                    range.To = int.Parse(m.Groups[2].Value);
                ranges.Add(range);
            }
        }

        private static void splitAndAddByQ(ref ListSorted<QValue<string>> parsedList, string headerValue)
        {
            if (parsedList == null)
                parsedList = new ListSorted<QValue<string>>();
            var split = Regex.Split(headerValue, @"\s*,\s*");
            foreach (string item in split)
            {
                float q = 0;
                string nItem = item;
                if (item.Contains(";"))
                {
                    var match = Regex.Match(item, @";\s*q=(\d+(\.\d+)?)");
                    if (match.Success)
                        q = 1 - float.Parse(match.Groups[1].Value);
                    nItem = item.Remove(item.IndexOf(';'));
                }
                parsedList.Add(new QValue<string>(q, nItem));
            }
        }

        private static void splitAndAddByQ<T>(ref ListSorted<QValue<T>> parsedList, string headerValue, Func<string, T> converter)
        {
            if (parsedList == null)
                parsedList = new ListSorted<QValue<T>>();
            var split = Regex.Split(headerValue, @"\s*,\s*");
            foreach (string item in split)
            {
                float q = 0;
                string nItem = item;
                if (item.Contains(";"))
                {
                    var match = Regex.Match(item, @";\s*q=(\d+(\.\d+)?)");
                    if (match.Success)
                        q = 1 - float.Parse(match.Groups[1].Value);
                    nItem = item.Remove(item.IndexOf(';'));
                }
                parsedList.Add(new QValue<T>(q, converter(nItem)));
            }
        }
    }

    /// <summary>
    /// Encapsulates an HTTP request, including its method, URL and headers. <see cref="HttpServer"/> generates this when it receives an
    /// HTTP request and passes it to the relevant <see cref="HttpRequestHandler"/>.
    /// </summary>
    public sealed class HttpRequest
    {
        private string _url;
        private NameValuesCollection<string> _getFields = null;     // will be initialised by Get getter
        private NameValuesCollection<string> _postFields = new NameValuesCollection<string>();
        private Dictionary<string, FileUpload> _fileUploads = new Dictionary<string, FileUpload>();

        /// <summary>
        /// Contains the part of the URL that follows the path where the request handler is hooked.
        /// <see cref="BaseUrl"/> + RestUrl is equal to <see cref="Url"/>.
        /// </summary>
        /// <example>
        ///     Consider the following example code:
        ///     <code>
        ///         HttpServer MyServer = new HttpServer();
        ///         MyServer.AddHandler(new HttpRequestHandlerHook { Path = "/homepages", Handler = MyHandler });
        ///     </code>
        ///     In the above example, an HTTP request for the URL <c>http://www.mydomain.com/homepages/a/adam</c>
        ///     would have BaseURL set to <c>/homepages</c> and RestURL set to <c>/a/adam</c>. Note the leading slashes.
        /// </example>
        public string RestUrl;

        /// <summary>
        /// Contains the part of the URL to which the request handler is hooked.
        /// BaseUrl + <see cref="RestUrl"/> is equal to <see cref="Url"/>.
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

        /// <summary>Specifies the HTTP protocol version that was used for this request.</summary>
        public HttpProtocolVersion HttpVersion;

        /// <summary>Specifies the HTTP request method (GET, POST or HEAD) that was used for thsi request.</summary>
        public HttpMethod Method;

        /// <summary>Contains the HTTP request headers that were received and understood by <see cref="HttpServer"/>.</summary>
        public HttpRequestHeaders Headers = new HttpRequestHeaders();

        /// <summary>
        /// Identifies the client that sent this request.
        /// </summary>
        public IPEndPoint OriginIP;

        /// <summary>
        /// Contains the delegate function used to handle this request.
        /// </summary>
        internal HttpRequestHandler Handler;

        /// <summary>
        /// A default constructor that initialises all fields to their defaults.
        /// </summary>
        public HttpRequest()
        {
        }

        /// <summary>
        /// The URL of the request, not including the domain, but including GET query parameters if any.
        /// </summary>
        public string Url
        {
            get { return _url; }
            set { _url = value; _getFields = null; }
        }

        /// <summary>
        /// The URL of the request, not including the domain or any GET query parameters.
        /// </summary>
        public string UrlWithoutQuery
        {
            get { return _url.Contains('?') ? _url.Remove(_url.IndexOf('?')) : _url; }
        }

        /// <summary>
        /// Returns the raw GET query parameters, if any
        /// </summary>
        public string Query
        {
            get { return _url.Contains('?') ? _url.Substring(_url.IndexOf('?') + 1) : null; }
        }

        /// <summary>
        /// The <see cref="RestUrl"/> (q.v.) of the request, not including the domain or any GET query parameters.
        /// </summary>
        public string RestUrlWithoutQuery
        {
            get { return RestUrl.Contains('?') ? RestUrl.Remove(RestUrl.IndexOf('?')) : RestUrl; }
        }

        /// <summary>
        /// Provides access to GET query parameters.
        /// </summary>
        public NameValuesCollection<string> Get
        {
            get
            {
                if (_getFields == null)
                {
                    _getFields = (_url.Contains('?')
                        ? ParseQueryValueParameters(new StringReader(_url.Substring(_url.IndexOf('?') + 1)))
                        : new NameValuesCollection<string>()).AsReadOnly();
                }
                return _getFields;
            }
        }

        /// <summary>
        /// Provides access to POST query parameters (empty if the request is not a POST request).
        /// </summary>
        public NameValuesCollection<string> Post { get { return _postFields; } }

        /// <summary>
        /// Contains information about file uploads included in a POST request. Empty if the request is not a POST request.
        /// </summary>
        public Dictionary<string, FileUpload> FileUploads { get { return _fileUploads; } }

        /// <summary>If this request is a POST request, replaces the body of the request with data from the specified stream.
        /// This will clear and reinitialise all the POST parameter values and file uploads.</summary>
        /// <param name="body">Stream to read new POST request body from.</param>
        /// <param name="tempPath">The temporary directory to use for file uploads. Default is <see cref="Path.GetTempPath"/>.</param>
        /// <param name="storeFileUploadInFileAtSize">The maximum size (in bytes) at which file uploads are stored in memory.
        /// Any uploads that exceed this limit are written to temporary files on disk. Default is 16 MB.</param>
        public void ParsePostBody(Stream body, string tempPath = null, long storeFileUploadInFileAtSize = 16*1024*1024)
        {
            _fileUploads.Clear();
            _postFields.Clear();

            if (Method != HttpMethod.Post)
                return;

            if (Headers.ContentType == HttpPostContentType.ApplicationXWwwFormUrlEncoded)
            {
                _postFields = ParseQueryValueParameters(new StreamReader(body, Encoding.UTF8));
                return;
            }

            // An excessively long boundary is going to screw up the following algorithm.
            // (Actually a limit of up to bufferSize - 8 would work, but I think 1024 is more than enough.)
            if (body == null || Headers.ContentMultipartBoundary == null || Headers.ContentMultipartBoundary.Length > 1024)
                return;

            if (tempPath == null)
                tempPath = Path.GetTempPath();

            // Instead of reallocating a new buffer multiple times, allocate at most two buffers and switch between them as necessary
            int bufferSize = 65536;
            byte[] buffer1 = new byte[bufferSize];
            byte[] buffer2 = null;
            byte[] buffer = buffer1;
            Action<int, int> switchBuffer = (offset, count) =>
            {
                if (buffer == buffer1)
                {
                    if (buffer2 == null)
                        buffer2 = new byte[bufferSize];
                    Buffer.BlockCopy(buffer, offset, buffer2, 0, count);
                    buffer = buffer2;
                }
                else
                {
                    Buffer.BlockCopy(buffer, offset, buffer1, 0, count);
                    buffer = buffer1;
                }
            };

            // Process POST request upload data
            int bytesRead = body.Read(buffer, 0, bufferSize);
            if (bytesRead == 0)    // premature end of request body
                return;

            // We expect the input to begin with "--" followed by the boundary followed by "\r\n"
            byte[] expecting = ("--" + Headers.ContentMultipartBoundary + "\r\n").ToUtf8();
            int bufferIndex = bytesRead;
            while (bufferIndex < expecting.Length)
            {
                bytesRead = body.Read(buffer, bufferIndex, buffer.Length - bufferIndex);
                if (bytesRead == 0)    // premature end of request body
                    return;
                bufferIndex += bytesRead;
            }
            if (!buffer.SubarrayEquals(0, expecting, 0, expecting.Length))
                return;
            bytesRead = bufferIndex - expecting.Length;
            bufferIndex = expecting.Length;

            // Now comes the main reading loop
            bool processingHeaders = true;
            string currentHeaders = "";
            string currentFieldName = null;
            Stream currentWritingStream = null;
            bool currentIsFileUpload = false;
            string currentFileUploadFilename = null;
            string currentFileUploadContentType = null;
            string currentFileUploadTempFilename = null;
            Decoder utf8Decoder = Encoding.UTF8.GetDecoder();
            char[] chArr = new char[1];
            byte[] lastBoundary = ("\r\n--" + Headers.ContentMultipartBoundary + "--\r\n").ToUtf8();
            byte[] middleBoundary = ("\r\n--" + Headers.ContentMultipartBoundary + "\r\n").ToUtf8();
            var inMemoryFileUploads = new SortedList<long, List<FileUpload>>();
            long inMemoryFileUploadsTotal = 0;
            while (bufferIndex > 0 || bytesRead > 0)
            {
                int writeIndex = 0;
                if (bytesRead > 0)
                {
                    if (processingHeaders)
                    {
                        bool newLineFound = false;
                        while (!newLineFound && bytesRead > 0)
                        {
                            int numCh = utf8Decoder.GetChars(buffer, bufferIndex, 1, chArr, 0);
                            bufferIndex++;
                            bytesRead--;
                            if (numCh != 0)
                                currentHeaders += chArr[0];
                            newLineFound = currentHeaders.EndsWith("\r\n\r\n");
                        }

                        if (newLineFound)
                        {
                            currentIsFileUpload = false;
                            currentFileUploadContentType = null;
                            currentFileUploadFilename = null;
                            currentFileUploadTempFilename = null;
                            currentFieldName = null;
                            currentWritingStream = null;
                            foreach (string header in currentHeaders.Split(new string[] { "\r\n" }, StringSplitOptions.None))
                            {
                                Match m;
                                if ((m = Regex.Match(header, @"^content-disposition\s*:\s*form-data\s*;(.*)$", RegexOptions.IgnoreCase)).Success)
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
                                            currentFileUploadFilename = m.Groups[2].Value;
                                        v = v.Substring(m.Length);
                                    }
                                }
                                else if ((m = Regex.Match(header, @"^content-type\s*:\s*(.*)$", RegexOptions.IgnoreCase)).Success)
                                    currentFileUploadContentType = m.Groups[1].Value;
                            }
                            if (currentFieldName != null)
                            {
                                currentWritingStream = new MemoryStream();
                                if (currentFileUploadFilename != null)
                                    currentIsFileUpload = true;
                            }
                            processingHeaders = false;
                            continue;
                        }
                    }
                    else if (bytesRead >= lastBoundary.Length)   // processing content
                    {
                        bool boundaryFound = false;
                        bool end = false;

                        int boundaryIndex = buffer.IndexOfSubarray(lastBoundary, bufferIndex, bufferIndex + bytesRead);
                        if (boundaryIndex != -1)
                        {
                            boundaryFound = true;
                            end = true;
                        }
                        int middleBoundaryIndex = buffer.IndexOfSubarray(middleBoundary, bufferIndex, bufferIndex + bytesRead);
                        if (middleBoundaryIndex != -1 && (!boundaryFound || middleBoundaryIndex < boundaryIndex))
                        {
                            boundaryFound = true;
                            boundaryIndex = middleBoundaryIndex;
                            end = false;
                        }

                        int howMuchToWrite = boundaryFound
                            // If we have encountered the boundary, write all the data up to it
                            ? howMuchToWrite = boundaryIndex - bufferIndex
                            // Write as much of the data to the output stream as possible, but leave enough so that we can still recognise the boundary
                            : howMuchToWrite = bytesRead - lastBoundary.Length;  // this is never negative because of the "if" we're in

                        // Write the aforementioned amount of data to the output stream
                        if (howMuchToWrite > 0 && currentWritingStream != null)
                        {
                            // If we're currently processing a file upload in memory, and it takes the total file uploads over the limit...
                            if (currentIsFileUpload && currentWritingStream is MemoryStream && ((MemoryStream) currentWritingStream).Length + inMemoryFileUploadsTotal + howMuchToWrite > storeFileUploadInFileAtSize)
                            {
                                var memory = (MemoryStream) currentWritingStream;
                                var inMemoryKeys = inMemoryFileUploads.Keys;
                                if (inMemoryKeys.Count > 0 && memory.Length < inMemoryKeys[inMemoryKeys.Count - 1])
                                {
                                    // ... switch the largest one to a temporary file
                                    var lastKey = inMemoryKeys[inMemoryKeys.Count - 1];
                                    var biggestUpload = inMemoryFileUploads[lastKey][0];
                                    inMemoryFileUploads[lastKey].RemoveAt(0);
                                    Stream fileStream;
                                    biggestUpload.LocalFilename = HttpInternalObjects.RandomTempFilepath(tempPath, out fileStream);
                                    fileStream.Write(biggestUpload.Data, 0, biggestUpload.Data.Length);
                                    fileStream.Close();
                                    fileStream.Dispose();
                                    inMemoryFileUploadsTotal -= biggestUpload.Data.LongLength;
                                    biggestUpload.Data = null;
                                    if (inMemoryFileUploads[lastKey].Count == 0)
                                        inMemoryFileUploads.Remove(lastKey);
                                }
                                else
                                {
                                    // ... switch this one to a temporary file
                                    currentFileUploadTempFilename = HttpInternalObjects.RandomTempFilepath(tempPath, out currentWritingStream);
                                    memory.WriteTo(currentWritingStream);
                                    memory.Close();
                                    memory.Dispose();
                                }
                            }
                            currentWritingStream.Write(buffer, bufferIndex, howMuchToWrite);
                        }

                        // If we encountered the boundary, add this field to _postFields or this upload to _fileUploads or inMemoryFileUploads
                        if (boundaryFound)
                        {
                            if (currentWritingStream != null)
                            {
                                currentWritingStream.Close();

                                if (!currentIsFileUpload)
                                    // It's a normal field
                                    _postFields[currentFieldName].Add(Encoding.UTF8.GetString(((MemoryStream) currentWritingStream).ToArray()));
                                else
                                {
                                    // It's a file upload
                                    var fileUpload = new FileUpload(currentFileUploadContentType, currentFileUploadFilename);
                                    if (currentFileUploadTempFilename != null)
                                        // The file upload has already been written to disk
                                        fileUpload.LocalFilename = currentFileUploadTempFilename;
                                    else
                                    {
                                        // The file upload is still in memory. Keep track of it in inMemoryFileUploads so that we can still write it to disk later if necessary
                                        var memory = (MemoryStream) currentWritingStream;
                                        fileUpload.Data = memory.ToArray();
                                        inMemoryFileUploads.AddSafe(fileUpload.Data.LongLength, fileUpload);
                                        inMemoryFileUploadsTotal += fileUpload.Data.LongLength;
                                    }
                                    _fileUploads[currentFieldName] = fileUpload;
                                }

                                currentWritingStream.Dispose();
                                currentWritingStream = null;
                            }

                            // If that was the final boundary, we are done
                            if (end)
                                break;

                            // Consume the boundary and go back to processing headers
                            bytesRead -= boundaryIndex - bufferIndex + middleBoundary.Length;
                            bufferIndex = boundaryIndex + middleBoundary.Length;
                            processingHeaders = true;
                            currentHeaders = "";
                            utf8Decoder.Reset();
                            continue;
                        }
                        else
                        {
                            // No boundary there. Received data has been written to the currentWritingStream above.
                            // Now copy the remaining little bit (which may contain part of the bounary) into a new buffer
                            switchBuffer(bufferIndex + howMuchToWrite, bytesRead - howMuchToWrite);
                            bytesRead -= howMuchToWrite;
                            writeIndex = bytesRead;
                        }
                    }
                    else if (bufferIndex > 0)
                    {
                        // We are processing content, but there is not enough data in the buffer to ensure that it doesn't contain part of the boundary.
                        // Therefore, just copy the data to a new buffer and continue receiving more
                        switchBuffer(bufferIndex, bytesRead);
                        writeIndex = bytesRead;
                    }
                }
                bufferIndex = 0;
                // We need to read enough data to contain the boundary
                do
                {
                    bytesRead = body.Read(buffer, writeIndex, bufferSize - writeIndex);
                    if (bytesRead == 0) // premature end of content
                    {
                        if (currentWritingStream != null)
                        {
                            currentWritingStream.Close();
                            currentWritingStream.Dispose();
                        }
                        return;
                    }
                    writeIndex += bytesRead;
                }
                while (writeIndex < lastBoundary.Length);
                bytesRead = writeIndex;
            }
        }

        /// <summary>
        /// Decodes a URL-encoded stream of UTF-8 characters into key-value pairs.
        /// </summary>
        /// <param name="s">Stream to read from.</param>
        public static NameValuesCollection<string> ParseQueryValueParameters(TextReader s)
        {
            var ret = new NameValuesCollection<string>();
            if (s == null)
                return ret;

            char[] buffer = new char[65536];
            int bytesRead = s.Read(buffer, 0, buffer.Length);
            int bufferIndex = 0;
            string curKey = "";
            string curValue = null;

            var fnAdd = new Action<string, string>((key, val) =>
            {
                key = key.UrlUnescape();
                val = val.UrlUnescape();
                ret[key].Add(val);
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
                            curKey += new string(buffer, bufferIndex, i - bufferIndex);
                        else
                            curValue += new string(buffer, bufferIndex, i - bufferIndex);
                        bufferIndex = i;
                    }
                    else if (buffer[i] == (byte) '=')
                    {
                        if (inKey)
                        {
                            curKey += new string(buffer, bufferIndex, i - bufferIndex);
                            curValue = "";
                            inKey = false;
                        }
                        else
                            curValue += new string(buffer, bufferIndex, i - bufferIndex) + "=";
                        bufferIndex = i + 1;
                    }
                    else if (buffer[i] == (byte) '&')
                    {
                        if (inKey)
                            curKey += new string(buffer, bufferIndex, i - bufferIndex) + "&";
                        else
                        {
                            curValue += new string(buffer, bufferIndex, i - bufferIndex);
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
            return ret;
        }

        /// <summary>Applies the specified modifications to this request's URL and returns the result.</summary>
        /// <param name="qsAddOrReplace">Replaces existing query-string parameters, or adds them if they are not already in the URL.</param>
        /// <param name="qsRemove">Removes the specified query-string parameters.</param>
        /// <param name="restUrl">Replaces the <see cref="RestUrlWithoutQuery"/> with the specified new value.</param>
        /// <param name="baseUrl">Replaces the <see cref="BaseUrl"/> with the specified new value.</param>
        /// <returns>The resulting URL after the transformation, without domain but with a leading slash.</returns>
        public string SameUrlExcept(Dictionary<string, string> qsAddOrReplace = null, string[] qsRemove = null, string restUrl = null, string baseUrl = null)
        {
            var url = (baseUrl ?? BaseUrl) + (restUrl ?? RestUrlWithoutQuery);
            var newQs = Get
                .Where(g => (qsRemove == null || !qsRemove.Contains(g.Key)) && (qsAddOrReplace == null || !qsAddOrReplace.ContainsKey(g.Key)))
                .SelectMany(qs => qs.Value.Select(q => new KeyValuePair<string, string>(qs.Key, q)));
            if (qsAddOrReplace != null)
                newQs = newQs.Concat(qsAddOrReplace);
            return newQs.Any()
                ? url + '?' + newQs.Select(q => q.Key.UrlEscape() + '=' + q.Value.UrlEscape()).JoinString("&")
                : url;
        }

        /// <summary>Adds or replaces given query-string parameters in this request's URL and returns the result.</summary>
        /// <param name="qsAddOrReplace">An even-numbered array of strings where each element at even indexes is a key and each element at odd indexes is a value.</param>
        /// <returns>The resulting URL after the transformation, without domain but with a leading slash.</returns>
        public string SameUrlExceptSet(params string[] qsAddOrReplace)
        {
            var dict = new Dictionary<string, string>();
            if ((qsAddOrReplace.Length & 1) == 1)
                throw new RTException("Expected an even number of strings — one pair per query string argument");
            for (int i = 0; i < qsAddOrReplace.Length; i += 2)
                dict.Add(qsAddOrReplace[i], qsAddOrReplace[i + 1]);
            return SameUrlExcept(dict);
        }

        /// <summary>Returns this request's URL, but with certain query-string parameters removed.</summary>
        /// <param name="predicate">Determines which query-string parameter keys are to be retained. All keys for which this predicate does not hold are removed from the URL.</param>
        /// <returns>The resulting URL after the transformation, without domain but with a leading slash.</returns>
        public string SameUrlWhere(Func<string, bool> predicate)
        {
            var qs = Get.Keys.Where(predicate).SelectMany(key => Get[key].Select(val => key.UrlEscape() + "=" + val.UrlEscape())).JoinString("&");
            return UrlWithoutQuery + (qs == "" ? "" : "?" + qs);
        }
    }

    /// <summary>
    /// Exception thrown to indicate error during processing of the request. Throw this exception to generate, for example, an HTTP 500 Internal Server Error.
    /// </summary>
    public sealed class InvalidRequestException : Exception
    {
        /// <summary>
        /// Response to return when the exception is caught. For example, use <see cref="HttpResponse.Error"/> to generate an HTTP 500 Internal Server Error.
        /// </summary>
        public HttpResponse Response;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="response">Response to return when the exception is caught.
        /// For example, use <see cref="HttpResponse.Error"/> to generate an HTTP 500 Internal Server Error.</param>
        public InvalidRequestException(HttpResponse response) { Response = response; }
    }
}
