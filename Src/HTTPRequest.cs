using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using RT.Util.ExtensionMethods;

namespace RT.Servers
{
    /// <summary>
    /// An HTTP request handler is a function that takes an HTTP request and returns an HTTP response.
    /// </summary>
    /// <param name="Request">The HTTP request to be processed.</param>
    /// <returns>The HTTP response generated from the HTTP request.</returns>
    public delegate HTTPResponse HTTPRequestHandler(HTTPRequest Request);

    /// <summary>
    /// Encapsulates all supported HTTP request headers. These will be set by the server when it receives the request.
    /// </summary>
    public struct HTTPRequestHeaders
    {
#pragma warning disable 1591    // Missing XML comment for publicly visible type or member
        public string[] Accept;
        public string[] AcceptCharset;
        public HTTPContentEncoding[] AcceptEncoding;
        public string[] AcceptLanguage;
        public HTTPConnection Connection;
        public long? ContentLength;                 // required only for POST
        public HTTPPOSTContentType ContentType;     // required only for POST
        public string ContentMultipartBoundary;     // required only for POST and only if ContentType == HTTPPOSTContentType.MultipartFormData
        public Dictionary<string, Cookie> Cookie;
        public string Host;
        public DateTime? IfModifiedSince;
        public string IfNoneMatch;
        public HTTPRange[] Range;
        public string UserAgent;
#pragma warning restore 1591    // Missing XML comment for publicly visible type or member

        /// <summary>Stores the header values pertaining to headers not supported by <see cref="HTTPRequestHeaders"/> as raw strings.</summary>
        public Dictionary<string, string> UnrecognisedHeaders;
    }

    /// <summary>
    /// Encapsulates an HTTP request, including its method, URL and headers. <see cref="HTTPServer"/> generates this when it receives an 
    /// HTTP request and passes it to the relevant <see cref="HTTPRequestHandler"/>.
    /// </summary>
    public struct HTTPRequest
    {
        private struct FieldsCache
        {
            public Dictionary<string, string> ValueCache;
            public Dictionary<string, List<string>> ArrayCache;
            public Dictionary<string, FileUpload> FileCache;
        }
        private string _URL;
        private FieldsCache GETFieldsCache;
        private FieldsCache POSTFieldsCache;

        /// <summary>
        /// Contains the part of the URL that follows the path where the request handler is hooked.
        /// </summary>
        /// <example>
        ///     Consider the following example code:
        ///     <code>
        ///         HTTPServer MyServer = new HTTPServer();
        ///         MyServer.AddHandler(new HTTPRequestHandlerHook { Path = "/homepages", Handler = MyHandler });
        ///     </code>
        ///     In the above example, an HTTP request for the URL <c>http://www.mydomain.com/homepages/a/adam</c>
        ///     would have the RestURL field set to the value <c>/a/adam</c>. Note the leading slash.
        /// </example>
        public string RestURL;

        /// <summary>
        /// Stores the domain name from the Host header, without the port number.
        /// </summary>
        public string Domain;

        /// <summary>
        /// Contains the part of the domain that precedes the domain where the request handler is hooked.
        /// </summary>
        /// <example>
        ///     Consider the following example code:
        ///     <code>
        ///         HTTPServer MyServer = new HTTPServer();
        ///         MyServer.AddHandler(new HTTPRequestHandlerHook { Domain = "homepages.mydomain.com", Handler = MyHandler });
        ///     </code>
        ///     In the above example, an HTTP request for the URL <c>http://peter.schmidt.homepages.mydomain.com/</c>
        ///     would have the RestDomain field set to the value <c>peter.schmidt.</c>. Note the trailing dot.
        /// </example>
        public string RestDomain;

        /// <summary>
        /// The HTTP request method (GET, POST or HEAD).
        /// </summary>
        public HTTPMethod Method;

        /// <summary>
        /// The HTTP request headers that were received and understood by <see cref="HTTPServer"/>.
        /// </summary>
        public HTTPRequestHeaders Headers;

        /// <summary>
        /// The directory to use for temporary files if the request is a POST request and contains a file upload.
        /// This can be set before <see cref="FileUploads"/>, <see cref="POST"/> or <see cref="POSTArr"/> is called for the first time.
        /// After the first call to any of these, file uploads will already have been processed.
        /// </summary>
        public string TempDir;

        /// <summary>
        /// Contains a stream providing read access to the content of a POST request.
        /// NULL if the request is a GET or HEAD.
        /// </summary>
        internal Stream Content;

        /// <summary>
        /// Contains the delegate function used to handle this request.
        /// </summary>
        internal HTTPRequestHandler Handler;

        /// <summary>
        /// Contains the path and filename of a temporary file that has been used to store the POST request content, if any. 
        /// <see cref="HTTPServer"/> uses this field to keep track of it and delete the file when it is no longer needed.
        /// </summary>
        internal string TemporaryFile;

        /// <summary>
        /// DO NOT USE THIS CONSTRUCTOR except in unit testing.
        /// </summary>
        /// <param name="Content">DO NOT USE THIS CONSTRUCTOR except in unit testing.</param>
        public HTTPRequest(Stream Content)
        {
            this.Content = Content;

            // Default values. I don't understand why I have to set them here.
            _URL = null;
            GETFieldsCache = new FieldsCache();
            POSTFieldsCache = new FieldsCache();
            RestURL = null;
            Method = HTTPMethod.GET;
            Headers = new HTTPRequestHeaders();
            Handler = null;
            TemporaryFile = null;
            TempDir = null;
            Domain = null;
            RestDomain = null;
        }

        /// <summary>
        /// The URL of the request, not including the domain, but including GET query parameters if any.
        /// </summary>
        public string URL
        {
            get { return _URL; }
            set { _URL = value; GETFieldsCache = new FieldsCache(); }
        }

        /// <summary>
        /// The URL of the request, not including the domain or any GET query parameters.
        /// </summary>
        public string URLWithoutQuery
        {
            get { return _URL.Contains('?') ? _URL.Remove(_URL.IndexOf('?')) : _URL; }
        }

        /// <summary>
        /// The <see cref="RestURL"/> (q.v.) of the request, not including the domain or any GET query parameters.
        /// </summary>
        public string RestURLWithoutQuery
        {
            get { return RestURL.Contains('?') ? RestURL.Remove(RestURL.IndexOf('?')) : RestURL; }
        }

        /// <summary>
        /// Provides access to GET query parameters that are individual values.
        /// </summary>
        /// <seealso cref="GETArr"/>
        public Dictionary<string, string> GET
        {
            get
            {
                if (!_URL.Contains('?'))
                    return new Dictionary<string, string>();
                if (GETFieldsCache.ValueCache == null)
                    GETFieldsCache = ParseQueryParameters(new MemoryStream(Encoding.ASCII.GetBytes(_URL.Substring(_URL.IndexOf('?') + 1))));
                return GETFieldsCache.ValueCache;
            }
        }

        /// <summary>
        /// Provides access to GET query parameters that are arrays.
        /// </summary>
        /// <seealso cref="GET"/>
        public Dictionary<string, List<string>> GETArr
        {
            get
            {
                if (!_URL.Contains('?'))
                    return new Dictionary<string, List<string>>();
                if (GETFieldsCache.ArrayCache == null)
                    GETFieldsCache = ParseQueryParameters(new MemoryStream(Encoding.ASCII.GetBytes(_URL.Substring(_URL.IndexOf('?') + 1))));
                return GETFieldsCache.ArrayCache;
            }
        }

        /// <summary>
        /// Provides access to POST query parameters that are individual values (empty if the request is not a POST request).
        /// </summary>
        /// <seealso cref="POSTArr"/>
        public Dictionary<string, string> POST
        {
            get
            {
                if (Content == null)
                    return new Dictionary<string, string>();
                if (POSTFieldsCache.ValueCache == null)
                    POSTFieldsCache = ParsePOSTParameters();
                return POSTFieldsCache.ValueCache;
            }
        }

        /// <summary>
        /// Provides access to POST query parameters that are arrays (empty if the request is not a POST request).
        /// </summary>
        /// <seealso cref="POST"/>
        public Dictionary<string, List<string>> POSTArr
        {
            get
            {
                if (Content == null)
                    return new Dictionary<string, List<string>>();
                if (POSTFieldsCache.ArrayCache == null)
                    POSTFieldsCache = ParsePOSTParameters();
                return POSTFieldsCache.ArrayCache;
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
                if (POSTFieldsCache.FileCache == null)
                    POSTFieldsCache = ParsePOSTParameters();
                return POSTFieldsCache.FileCache;
            }
        }

        private FieldsCache ParsePOSTParameters()
        {
            if (Headers.ContentType == HTTPPOSTContentType.ApplicationXWWWFormURLEncoded)
                return ParseQueryParameters(Content);
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

            byte[] Buffer = new byte[65536];
            int BytesRead = Content.Read(Buffer, 0, 65536);
            // We expect the input to begin with "--" followed by the boundary followed by "\r\n"
            string Expecting = "--" + Headers.ContentMultipartBoundary + "\r\n";
            string StuffRead = Encoding.ASCII.GetString(Buffer, 0, BytesRead);
            int PrevLength = 0;
            while (StuffRead.Length < Expecting.Length)
            {
                BytesRead = Content.Read(Buffer, 0, 65536);
                PrevLength = StuffRead.Length;
                StuffRead += Encoding.ASCII.GetString(Buffer, 0, BytesRead);
            }
            if (StuffRead.Substring(0, Expecting.Length) != Expecting)
                return fc;
            int BufferIndex = BytesRead + Expecting.Length - StuffRead.Length;
            BytesRead -= BufferIndex;

            // Now comes the main reading loop
            bool ProcessingHeaders = true;
            string CurrentHeaders = "";
            string CurrentFieldName = null;
            Stream CurrentWritingStream = null;
            while (BufferIndex > 0 || BytesRead > 0)
            {
                int WriteIndex = 0;
                if (BytesRead > 0)
                {
                    if (ProcessingHeaders)
                    {
                        int PrevCHLength = CurrentHeaders.Length;
                        CurrentHeaders += Encoding.ASCII.GetString(Buffer, BufferIndex, BytesRead);
                        if (CurrentHeaders.Contains("\r\n\r\n"))
                        {
                            int Pos = CurrentHeaders.IndexOf("\r\n\r\n");
                            CurrentHeaders = CurrentHeaders.Remove(Pos);
                            BufferIndex += Pos - PrevCHLength + 4;
                            BytesRead -= Pos - PrevCHLength + 4;
                            string FileName = null;
                            string ContentType = null;
                            foreach (string Header in CurrentHeaders.Split(new string[] { "\r\n" }, StringSplitOptions.None))
                            {
                                Match m = Regex.Match(Header, @"^content-disposition\s*:\s*form-data\s*;(.*)$", RegexOptions.IgnoreCase);
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
                                            CurrentFieldName = m.Groups[2].Value;
                                        else if (m.Groups[1].Value.ToLowerInvariant() == "filename")
                                            FileName = m.Groups[2].Value;
                                        v = v.Substring(m.Length);
                                    }
                                }
                                else
                                {
                                    m = Regex.Match(Header, @"^content-type\s*:\s*(.*)$", RegexOptions.IgnoreCase);
                                    if (m.Success)
                                        ContentType = m.Groups[1].Value;
                                }
                            }
                            if (FileName == null && CurrentFieldName != null)
                                CurrentWritingStream = new MemoryStream();
                            else if (FileName != null && CurrentFieldName != null)
                            {
                                string TempFile = HTTPInternalObjects.RandomTempFilepath(TempDir, out CurrentWritingStream);
                                fc.FileCache[CurrentFieldName] = new FileUpload
                                {
                                    ContentType = ContentType,
                                    Filename = FileName,
                                    LocalTempFilename = TempFile
                                };
                            }
                            ProcessingHeaders = false;
                            continue;
                        }
                    }
                    else if (BytesRead >= Headers.ContentMultipartBoundary.Length + 8)   // processing content
                    {
                        // This will convert non-ASCII bytes to question marks, but that's OK because we use this only to find the boundary
                        string Data = Encoding.ASCII.GetString(Buffer, BufferIndex, BytesRead);
                        bool SepFound = false;
                        int SepIndex = 0;
                        bool End = false;
                        if (Data.Contains("\r\n--" + Headers.ContentMultipartBoundary + "--\r\n"))
                        {
                            SepFound = true;
                            SepIndex = Data.IndexOf("\r\n--" + Headers.ContentMultipartBoundary + "--\r\n");
                            End = true;
                        }
                        if (Data.Contains("\r\n--" + Headers.ContentMultipartBoundary + "\r\n"))
                        {
                            int Pos = Data.IndexOf("\r\n--" + Headers.ContentMultipartBoundary + "\r\n");
                            if (!SepFound || Pos < SepIndex)
                            {
                                SepFound = true;
                                SepIndex = Data.IndexOf("\r\n--" + Headers.ContentMultipartBoundary + "\r\n");
                                End = false;
                            }
                        }

                        if (SepFound)
                        {
                            // Write the rest of the data to the output stream and then process the separator
                            if (SepIndex > 0) CurrentWritingStream.Write(Buffer, BufferIndex, SepIndex);
                            CurrentWritingStream.Close();
                            // Note that CurrentWritingStream is either a MemoryStream or a FileStream.
                            // If it is a FileStream, then the relevant entry to fc.FileCache has already been made.
                            // Only if it is a MemoryStream, we need to process the stuff here.
                            if (CurrentWritingStream is MemoryStream && CurrentFieldName.EndsWith("[]"))
                            {
                                string ArrName = CurrentFieldName.Remove(CurrentFieldName.Length - 2);
                                if (!fc.ArrayCache.ContainsKey(ArrName))
                                    fc.ArrayCache[ArrName] = new List<string>();
                                fc.ArrayCache[ArrName].Add(Encoding.UTF8.GetString(((MemoryStream) CurrentWritingStream).ToArray()));
                            }
                            else if (CurrentWritingStream is MemoryStream)
                                fc.ValueCache[CurrentFieldName] = Encoding.UTF8.GetString(((MemoryStream) CurrentWritingStream).ToArray());

                            if (End)
                                break;

                            ProcessingHeaders = true;
                            CurrentHeaders = "";
                            BufferIndex += SepIndex + Headers.ContentMultipartBoundary.Length + 6;
                            BytesRead -= SepIndex + Headers.ContentMultipartBoundary.Length + 6;
                            continue;
                        }
                        else
                        {
                            // Write some of the data to the output stream, but leave enough so that we can still recognise the boundary
                            int HowMuchToWrite = BytesRead - Headers.ContentMultipartBoundary.Length - 8;
                            if (HowMuchToWrite > 0)
                                CurrentWritingStream.Write(Buffer, BufferIndex, HowMuchToWrite);
                            byte[] NewBuffer = new byte[65536];
                            Array.Copy(Buffer, BufferIndex + HowMuchToWrite, NewBuffer, 0, BytesRead - HowMuchToWrite);
                            Buffer = NewBuffer;
                            BufferIndex = 0;
                            BytesRead -= HowMuchToWrite;
                            WriteIndex = BytesRead;
                        }
                    }
                    else if (BufferIndex > 0)
                    {
                        byte[] NewBuffer = new byte[65536];
                        Array.Copy(Buffer, BufferIndex, NewBuffer, 0, BytesRead);
                        Buffer = NewBuffer;
                        WriteIndex = BytesRead;
                    }
                }
                BufferIndex = 0;
                // We need to read enough data to contain the boundary
                do
                {
                    BytesRead = Content.Read(Buffer, WriteIndex, 65536 - WriteIndex);
                    if (BytesRead == 0) // premature end of content
                    {
                        if (CurrentWritingStream != null)
                            CurrentWritingStream.Close();
                        return fc;
                    }
                    WriteIndex += BytesRead;
                } while (WriteIndex < Headers.ContentMultipartBoundary.Length + 8);
                BytesRead = WriteIndex;
            }

            return fc;
        }

        private FieldsCache ParseQueryParameters(Stream s)
        {
            FieldsCache fc = new FieldsCache
            {
                ArrayCache = new Dictionary<string, List<string>>(),
                ValueCache = new Dictionary<string, string>(),
                FileCache = new Dictionary<string, FileUpload>()
            };
            int b = s.ReadByte();
            string CurKey = "";
            string CurValue = null;
            while (b != -1)
            {
                if (b == (int) '=')
                    CurValue = "";
                else if (b == (int) '&')
                {
                    CurKey = CurKey.URLUnescape();
                    CurValue = CurValue.URLUnescape();
                    if (CurKey.EndsWith("[]"))
                    {
                        CurKey = CurKey.Remove(CurKey.Length - 2);
                        if (!fc.ArrayCache.ContainsKey(CurKey))
                            fc.ArrayCache[CurKey] = new List<string>();
                        fc.ArrayCache[CurKey].Add(CurValue);
                    }
                    else
                        fc.ValueCache[CurKey] = CurValue;
                    CurKey = "";
                    CurValue = null;
                }
                else if (CurValue != null)
                    CurValue += (char) b;
                else
                    CurKey += (char) b;
                b = s.ReadByte();
            }
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
        /// Response to return when the exception is caught. Usually <see cref="HTTPServer.ErrorResponse(HTTPStatusCode)"/>() is used to generate an HTTP 500 Internal Server Error.
        /// </summary>
        public HTTPResponse Response;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="Response">Response to return when the exception is caught.
        /// Usually <see cref="HTTPServer.ErrorResponse(HTTPStatusCode)"/> is used to generate an HTTP 500 Internal Server Error.</param>
        public InvalidRequestException(HTTPResponse Response) { this.Response = Response; }
    }
}
