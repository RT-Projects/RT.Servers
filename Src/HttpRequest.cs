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
    /// Encapsulates an HTTP request, including its method, URL and headers. <see cref="HttpServer"/> generates this when it receives an
    /// HTTP request and passes it to the relevant request handler.
    /// </summary>
    public class HttpRequest
    {
        /// <summary>Contains the original URL of the request (without domain, but with query parameters).</summary>
        protected string _url;
        /// <summary>Contains the original domain of the request (not including the port number).</summary>
        protected string _domain;

        private NameValuesCollection<string> _getFields = null;     // will be initialised by Get getter
        private NameValuesCollection<string> _postFields = new NameValuesCollection<string>();
        private Dictionary<string, FileUpload> _fileUploads = new Dictionary<string, FileUpload>();

        /// <summary>
        /// Stores the domain name from the Host header, without the port number.
        /// </summary>
        public virtual string Domain { get { return _domain; } }

        internal void SetDomain(string domain)
        {
            _domain = domain;
        }

        /// <summary>Contains the port number at which the server was contacted for this request.</summary>
        public int Port { get; internal set; }

        /// <summary>Specifies the HTTP protocol version that was used for this request.</summary>
        public HttpProtocolVersion HttpVersion { get; internal set; }

        /// <summary>Specifies the HTTP request method (GET, POST or HEAD) that was used for this request.</summary>
        public HttpMethod Method { get; internal set; }

        /// <summary>Contains the HTTP request headers that were received and understood by <see cref="HttpServer"/>.</summary>
        public HttpRequestHeaders Headers { get; internal set; }

        /// <summary>Identifies the client that sent this request.</summary>
        public IPAddress ClientIPAddress { get; internal set; }

        /// <summary>Identifies the immediate source of this request, which might be the client itself, or an HTTP proxy.</summary>
        public IPEndPoint SourceIP { get; internal set; }

        /// <summary>
        /// A default constructor that initialises all fields to their defaults.
        /// </summary>
        internal HttpRequest()
        {
            Headers = new HttpRequestHeaders();
        }

        /// <summary>Initialises this HTTP request from the specified HTTP request.</summary>
        protected HttpRequest(HttpRequest copyFrom)
        {
            _url = copyFrom._url;
            _domain = copyFrom._domain;
            _getFields = copyFrom._getFields;
            _postFields = copyFrom._postFields;
            _fileUploads = copyFrom._fileUploads;
            Port = copyFrom.Port;
            HttpVersion = copyFrom.HttpVersion;
            Method = copyFrom.Method;
            Headers = copyFrom.Headers;
            ClientIPAddress = copyFrom.ClientIPAddress;
            SourceIP = copyFrom.SourceIP;
        }

        /// <summary>
        /// The URL of the request, not including the domain, but including GET query parameters if any.
        /// </summary>
        public virtual string Url
        {
            get { return _url; }
        }

        internal void SetUrl(string newUrl)
        {
            _url = newUrl;
            _getFields = null;
        }

        /// <summary>
        /// The URL of the request, not including the domain or any GET query parameters.
        /// </summary>
        public string UrlWithoutQuery
        {
            get
            {
                var url = Url;
                return url.Contains('?') ? url.Remove(url.IndexOf('?')) : url;
            }
        }

        /// <summary>
        /// Returns the raw GET query parameters, if any. <see cref="UrlWithoutQuery"/> + <see cref="Query"/> is always equal to <see cref="Url"/>.
        /// </summary>
        public string Query
        {
            get { return _url.Contains('?') ? _url.Substring(_url.IndexOf('?')) : ""; }
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
        internal void ParsePostBody(Stream body, string tempPath = null, long storeFileUploadInFileAtSize = 16*1024*1024)
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
        internal static NameValuesCollection<string> ParseQueryValueParameters(TextReader s)
        {
            var ret = new NameValuesCollection<string>();
            if (s == null)
                return ret;

            char[] buffer = new char[65536];
            int charsRead = s.Read(buffer, 0, buffer.Length);
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
            while (charsRead > 0)
            {
                while (bufferIndex < charsRead)
                {
                    int i = bufferIndex;
                    while (i < charsRead && buffer[i] != '&' && buffer[i] != '=')
                        i++;
                    if (i == charsRead)
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
                charsRead = s.Read(buffer, 0, buffer.Length);
                bufferIndex = 0;
            }

            if (curValue != null)
                fnAdd(curKey, curValue);

            s.Close();
            return ret;
        }

        /// <summary>Gets the full URL of the request, including the protocol, domain, port number (if different from 80), path and query parameters.</summary>
        public string FullUrl
        {
            get
            {
                return "http://" + _domain + (Port != 80 ? ":" + Port : "") + _url;
            }
        }

        /// <summary>Gets the full URL of the request, including the protocol, domain, port number (if different from 80) and path, but wihout any query parameters.</summary>
        public string FullUrlWithoutQuery
        {
            get
            {
                return "http://" + _domain + (Port != 80 ? ":" + Port : "") + (_url.Contains('?') ? _url.Remove(_url.IndexOf('?')) : _url);
            }
        }

        /// <summary>Applies the specified modifications to this request's URL and returns the result.</summary>
        /// <param name="qsAddOrReplace">Replaces existing query-string parameters, or adds them if they are not already in the URL.</param>
        /// <param name="qsRemove">Removes the specified query-string parameters.</param>
        /// <returns>The resulting URL after the transformation, without domain but with a leading slash.</returns>
        public string SameUrlExcept(Dictionary<string, string> qsAddOrReplace = null, string[] qsRemove = null)
        {
            var newQs = Get
                .Where(g => (qsRemove == null || !qsRemove.Contains(g.Key)) && (qsAddOrReplace == null || !qsAddOrReplace.ContainsKey(g.Key)))
                .SelectMany(qs => qs.Value.Select(q => new KeyValuePair<string, string>(qs.Key, q)));
            if (qsAddOrReplace != null)
                newQs = newQs.Concat(qsAddOrReplace);
            return newQs.Any()
                ? FullUrlWithoutQuery + '?' + newQs.Select(q => q.Key.UrlEscape() + '=' + q.Value.UrlEscape()).JoinString("&")
                : FullUrlWithoutQuery;
        }

        /// <summary>Adds or replaces given query-string parameters in this request's URL and returns the result.</summary>
        /// <param name="qsAddOrReplace">An even-numbered array of strings where each element at even indexes is a key and each element at odd indexes is a value.</param>
        /// <returns>The resulting URL after the transformation, without domain but with a leading slash.</returns>
        public string SameUrlExceptSet(params string[] qsAddOrReplace)
        {
            var dict = new Dictionary<string, string>();
            if ((qsAddOrReplace.Length & 1) == 1)
                throw new ArgumentException("Expected an even number of strings — one pair per query string argument.", "qsAddOrReplace");
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
            return FullUrlWithoutQuery + (qs == "" ? "" : "?" + qs);
        }
    }
}
