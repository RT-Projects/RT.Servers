using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using RT.Util;
using RT.Util.ExtensionMethods;
using RT.Util.Streams;
using ICSharpCode.SharpZipLib.GZip;

namespace RT.Servers
{
    /// <summary>
    /// Provides an HTTP server.
    /// </summary>
    public partial class HttpServer
    {
        /// <summary>
        /// Constructs an HTTP server with the specified configuration settings.
        /// </summary>
        /// <param name="options">Specifies the configuration settings to use for this <see cref="HttpServer"/>, or null to set all configuration values to default values.</param>
        public HttpServer(HttpServerOptions options = null) { _opt = options ?? new HttpServerOptions(); }

        /// <summary>
        /// Returns the configuration settings currently in effect for this server.
        /// </summary>
        public HttpServerOptions Options { get { return _opt; } }

        /// <summary>
        /// Returns a boolean specifying whether the server is currently running (listening) in non-blocking mode.
        /// If the server is running in blocking mode, this will return false even if it is listening.
        /// </summary>
        public bool IsListeningThreadActive { get { return _listeningThread != null && _listeningThread.IsAlive; } }

        private TcpListener _listener;
        private Thread _listeningThread;
        private HttpServerOptions _opt;
        private List<readingThreadRunner> _activeReadingThreads = new List<readingThreadRunner>();

        /// <summary>
        /// Returns the number of currently active threads that are processing a request.
        /// </summary>
        public int ActiveHandlers { get { lock (_activeReadingThreads) { return _activeReadingThreads.Count; } } }

        /// <summary>Add request handlers here. See the documentation for <see cref="HttpRequestHandlerHook"/> for more information.
        /// If you wish to make changes to this list while the server is running, use a lock around it.</summary>
        public List<HttpRequestHandlerHook> RequestHandlerHooks = new List<HttpRequestHandlerHook>();

        /// <summary>If set, various debug events will be logged to here.</summary>
        public LoggerBase Log;

        /// <summary>
        /// If the HTTP server is listening in non-blocking mode, shuts the HTTP server down, optionally either
        /// gracefully (allowing still-running requests to complete) or brutally (aborting requests no matter where
        /// they are in their processing). If the HTTP server is listening in blocking mode, nothing happens.
        /// Blocking or non-blocking mode is determined by the parameter to <see cref="StartListening(bool)"/>.
        /// </summary>
        /// <param name="brutal">If true, requests currently executing in separate threads are aborted brutally.</param>
        public void StopListening(bool brutal = false)
        {
            if (!IsListeningThreadActive)
                return;
            _listeningThread.Abort();
            _listeningThread = null;
            _listener.Stop();
            _listener = null;

            lock (_activeReadingThreads)
            {
                if (brutal)
                    foreach (var thr in _activeReadingThreads)
                        if (thr.currentThread != null)
                            thr.currentThread.Abort();
                _activeReadingThreads = new List<readingThreadRunner>();
            }
        }

        /// <summary>
        /// Runs the HTTP server.
        /// </summary>
        /// <param name="blocking">If true, the method will continually wait for and handle incoming requests
        /// and never return. In this mode, <see cref="StopListening(bool)"/> cannot be used. If false, a separate thread
        /// is spawned in which the server will handle incoming requests, and control is returned immediately.
        /// You can then use <see cref="StopListening(bool)"/> to abort this thread either gracefully or brutally.</param>
        public void StartListening(bool blocking)
        {
            if (IsListeningThreadActive && !blocking)
                return;
            if (IsListeningThreadActive)
                StopListening();

            _listener = new TcpListener(System.Net.IPAddress.Any, _opt.Port);
            _listener.Start();
            if (blocking)
            {
                listeningThreadFunction();
            }
            else
            {
                _listeningThread = new Thread(listeningThreadFunction);
                _listeningThread.Start();
            }
        }

        /// <summary>
        /// Returns an <see cref="HttpRequestHandler"/> that serves static files from a specified directory on the
        /// local file system and that lists the contents of directories within the specified directory.
        /// The MIME type used for the returned files is determined from <see cref="HttpServerOptions.MimeTypes"/>.
        /// </summary>
        /// <param name="baseDir">The base directory from which to serve files.</param>
        /// <returns>An <see cref="HttpRequestHandler"/> that can be used to create an <see cref="HttpRequestHandlerHook"/>
        /// and then added to <see cref="RequestHandlerHooks"/>.</returns>
        /// <example>
        ///     The following code will instantiate an <see cref="HttpServer"/> which will serve files from the <c>D:\UserFiles</c> directory
        ///     on the local file system. For example, a request for the URL <c>http://www.mydomain.com/users/adam/report.txt</c>
        ///     will serve the file stored at the location <c>D:\UserFiles\adam\report.txt</c>. A request for the URL
        ///     <c>http://www.mydomain.com/users/adam/</c> will list all the files in the directory <c>D:\UserFiles\adam</c>.
        ///     <code>
        ///         HttpServer MyServer = new HttpServer();
        ///         var handler = MyServer.CreateFileSystemHandler(@"D:\UserFiles");
        ///         var hook = new HttpRequestHandlerHook("/users", handler);
        ///         MyServer.RequestHandlerHooks.Add(hook);
        ///     </code>
        /// </example>
        public HttpRequestHandler CreateFileSystemHandler(string baseDir)
        {
            return req => FileSystemResponse(baseDir, req);
        }

        /// <summary>
        /// Creates a handler which will serve the file specified in <paramref name="filePath"/>.
        /// Use in a <see cref="HttpRequestHandlerHook"/> and add to <see cref="RequestHandlerHooks"/>.
        /// See also: <see cref="CreateFileSystemHandler"/>.
        /// </summary>
        public HttpRequestHandler CreateFileHandler(string filePath)
        {
            return req => FileResponse(filePath);
        }

        /// <summary>
        /// Creates a handler which will redirect the browser to <paramref name="newUrl"/>.
        /// To be used in conjunction with <see cref="HttpRequestHandlerHook"/> to add to <see cref="RequestHandlerHooks"/>.
        /// </summary>
        public HttpRequestHandler CreateRedirectHandler(string newUrl)
        {
            return req => RedirectResponse(newUrl);
        }

        /// <summary>
        /// Returns an <see cref="HttpResponse"/> that returns a file from the local file system,
        /// which is derived from the specified base directory and the URL of the specified request.
        /// </summary>
        /// <param name="baseDir">Base directory in which to search for the file.</param>
        /// <param name="req">HTTP request from the client.</param>
        /// <returns>An <see cref="HttpResponse"/> encapsulating the file transfer.</returns>
        public HttpResponse FileSystemResponse(string baseDir, HttpRequest req)
        {
            string p = baseDir.EndsWith(Path.DirectorySeparatorChar.ToString()) ? baseDir.Remove(baseDir.Length - 1) : baseDir;
            string baseUrl = req.Url.Substring(0, req.Url.Length - req.RestUrl.Length);
            string url = req.RestUrl.Contains('?') ? req.RestUrl.Remove(req.RestUrl.IndexOf('?')) : req.RestUrl;
            string[] urlPieces = url.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            string soFar = "";
            string soFarUrl = "";
            for (int i = 0; i < urlPieces.Length; i++)
            {
                string piece = urlPieces[i].UrlUnescape();
                string nextSoFar = soFar + Path.DirectorySeparatorChar + piece;

                if (File.Exists(p + nextSoFar))
                {
                    DirectoryInfo parentDir = new DirectoryInfo(p + soFar);
                    foreach (var fileInf in parentDir.GetFiles(piece))
                    {
                        soFarUrl += "/" + fileInf.Name.UrlEscape();
                        break;
                    }

                    if (req.Url != baseUrl + soFarUrl)
                        return RedirectResponse(baseUrl + soFarUrl);

                    return FileResponse(p + nextSoFar);
                }
                else if (Directory.Exists(p + nextSoFar))
                {
                    DirectoryInfo parentDir = new DirectoryInfo(p + soFar);
                    foreach (var dirInfo in parentDir.GetDirectories(piece))
                    {
                        soFarUrl += "/" + dirInfo.Name.UrlEscape();
                        break;
                    }
                }
                else
                {
                    return ErrorResponse(HttpStatusCode._404_NotFound, "\"" + baseUrl + soFarUrl + "/" + piece + "\" doesn't exist.");
                }
                soFar = nextSoFar;
            }

            // If this point is reached, it's a directory
            string trueDirURL = baseUrl + soFarUrl + "/";
            if (req.Url != trueDirURL)
                return RedirectResponse(trueDirURL);

            if (_opt.DirectoryListingStyle == DirectoryListingStyle.XmlPlusXsl)
            {
                return new HttpResponse
                {
                    Headers = new HttpResponseHeaders { ContentType = "application/xml; charset=utf-8" },
                    Status = HttpStatusCode._200_OK,
                    Content = new DynamicContentStream(GenerateDirectoryXml(p + soFar, trueDirURL))
                };
            }
            else
                return ErrorResponse(HttpStatusCode._500_InternalServerError);
        }

        /// <summary>
        /// Generates an <see cref="HttpResponse"/> that causes the server to return the specified
        /// file from the local file system. The content type is inferred from the <see cref="HttpServerOptions.MimeTypes"/>
        /// field in <see cref="Options"/>.
        /// </summary>
        /// <param name="filePath">Full path and filename of the file to return.</param>
        /// <returns><see cref="HttpResponse"/> object that encapsulates the return of the specified file.</returns>
        public HttpResponse FileResponse(string filePath)
        {
            FileInfo f = new FileInfo(filePath);
            string extension = f.Extension.Length > 1 ? f.Extension.Substring(1) : "";
            string mimeType = _opt.MimeTypes.ContainsKey(extension) ? _opt.MimeTypes[extension] : _opt.MimeTypes.ContainsKey("*") ? _opt.MimeTypes["*"] : "detect";

            if (mimeType == "detect")
            {
                // Look at the first 1 KB. If there are special control characters in it, it's likely a binary file. Otherwise, output as text/plain.
                byte[] buf = new byte[1024];
                int bytesRead;
                using (var s = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    bytesRead = s.Read(buf, 0, 1024);
                    s.Close();
                }
                bool plainText = true;
                for (int i = 0; i < bytesRead && plainText; i++)
                {
                    if (buf[i] < 32 && buf[i] != 9 && buf[i] != 10 && buf[i] != 13)
                        plainText = false;
                }
                mimeType = plainText ? "text/plain; charset=utf-8" : "application/octet-stream";
            }
            return FileResponse(filePath, mimeType);
        }

        /// <summary>
        /// Generates an <see cref="HttpResponse"/> that causes the server to return the specified
        /// file from the local file system using the specified MIME content type.
        /// </summary>
        /// <param name="filePath">Full path and filename of the file to return.</param>
        /// <param name="contentType">MIME type to use in the Content-Type header.</param>
        /// <returns><see cref="HttpResponse"/> object that encapsulates the return of the specified file.</returns>
        public static HttpResponse FileResponse(string filePath, string contentType)
        {
            try
            {
                FileStream fileStream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                return new HttpResponse
                {
                    Status = HttpStatusCode._200_OK,
                    Content = fileStream,
                    Headers = new HttpResponseHeaders { ContentType = contentType }
                };
            }
            catch (FileNotFoundException)
            {
                return ErrorResponse(HttpStatusCode._404_NotFound,
                    "The requested file does not exist.");
            }
            catch (IOException e)
            {
                return ErrorResponse(HttpStatusCode._500_InternalServerError,
                    "File could not be opened in the file system: " + e.Message);
            }
        }

        /// <summary>
        /// Returns the specified string to the client, designating it as a specific MIME type.
        /// </summary>
        /// <param name="content">Content to return to the client.</param>
        /// <param name="contentType">MIME type of the content.</param>
        /// <returns>An <see cref="HttpResponse"/> object encapsulating the return of the string.</returns>
        public static HttpResponse StringResponse(string content, string contentType)
        {
            return new HttpResponse
            {
                Content = new MemoryStream(content.ToUtf8()),
                Headers = new HttpResponseHeaders { ContentType = contentType },
                Status = HttpStatusCode._200_OK
            };
        }

        /// <summary>
        /// Returns the specified string to the client. The MIME type is assumed to be "text/html; charset=utf-8".
        /// </summary>
        /// <param name="content">Content to return to the client.</param>
        /// <returns>An <see cref="HttpResponse"/> object encapsulating the return of the string.</returns>
        public HttpResponse stringResponse(string content)
        {
            return new HttpResponse
            {
                Content = new MemoryStream(content.ToUtf8()),
                Headers = new HttpResponseHeaders { ContentType = "text/html; charset=utf-8" },
                Status = HttpStatusCode._200_OK
            };
        }

        /// <summary>
        /// Generates XML that represents the contents of a directory on the local file system.
        /// </summary>
        /// <param name="localPath">Full path of a directory to list the contents of.</param>
        /// <param name="url">URL (not including a domain) that points at the directory.</param>
        /// <returns>XML that represents the contents of the specified directory.</returns>
        public static IEnumerable<string> GenerateDirectoryXml(string localPath, string url)
        {
            if (!Directory.Exists(localPath))
                throw new FileNotFoundException("Directory does not exist.", localPath);

            List<DirectoryInfo> dirs = new List<DirectoryInfo>();
            List<FileInfo> files = new List<FileInfo>();
            DirectoryInfo dirInfo = new DirectoryInfo(localPath);
            foreach (var d in dirInfo.GetDirectories())
                dirs.Add(d);
            foreach (var f in dirInfo.GetFiles())
                files.Add(f);
            dirs.Sort((a, b) => a.Name.CompareTo(b.Name));
            files.Sort((a, b) => a.Name.CompareTo(b.Name));

            yield return "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n";
            yield return "<?xml-stylesheet href=\"/$/directory-listing/xsl\" type=\"text/xsl\" ?>\n";
            yield return "<directory url=\"" + url.HtmlEscape() + "\" unescapedurl=\"" + url.UrlUnescape().HtmlEscape() + "\" img=\"/$/directory-listing/icons/folderbig\" numdirs=\"" + (dirs.Count) + "\" numfiles=\"" + (files.Count) + "\">\n";

            foreach (var d in dirs)
                yield return "  <dir link=\"" + d.Name.UrlEscape() + "/\" img=\"/$/directory-listing/icons/folder\">" + d.Name.HtmlEscape() + "</dir>\n";
            foreach (var f in files)
            {
                string extension = f.Name.Contains('.') ? f.Name.Substring(f.Name.LastIndexOf('.') + 1) : "";
                yield return "  <file link=\"" + f.Name.UrlEscape() + "\" size=\"" + f.Length + "\" nicesize=\"" + PrettySize(f.Length);
                yield return "\" img=\"/$/directory-listing/icons/" + HttpInternalObjects.GetDirectoryListingIconStr(extension) + "\">" + f.Name.HtmlEscape() + "</file>\n";
            }

            yield return "</directory>\n";
        }

        /// <summary>
        /// Generates an <see cref="HttpResponse"/> that redirects the client to a new URL, using the HTTP status code 301 Moved Permanently.
        /// </summary>
        /// <param name="newUrl">URL to redirect the client to.</param>
        /// <returns><see cref="HttpResponse"/> encapsulating a redirect to the specified URL, using the HTTP status code 301 Moved Permanently.</returns>
        public static HttpResponse RedirectResponse(string newUrl)
        {
            return new HttpResponse
            {
                Headers = new HttpResponseHeaders { Location = newUrl },
                Status = HttpStatusCode._301_MovedPermanently
            };
        }

        /// <summary>
        /// Generates a simple <see cref="HttpResponse"/> with the specified HTTP status code, headers and message. Generally used for error.
        /// </summary>
        /// <param name="statusCode">HTTP status code to use in the response.</param>
        /// <param name="headers">Headers to use in the <see cref="HttpResponse"/>, or null to use default values.</param>
        /// <param name="message">Message to display along with the HTTP status code.</param>
        /// <returns>A minimalist <see cref="HttpResponse"/> with the specified HTTP status code, headers and message.</returns>
        public static HttpResponse ErrorResponse(HttpStatusCode statusCode, string message = null, HttpResponseHeaders headers = null)
        {
            if (headers == null)
                headers = new HttpResponseHeaders();

            string statusCodeName = string.Concat(((int) statusCode).ToString(), " ", statusCode.ToText()).HtmlEscape();
            headers.ContentType = "text/html; charset=utf-8";

            // We sometimes output error messages as soon as possible, even if we should normally wait for more data, esp. the POST content.
            // This would interfere with Connection: keep-alive, so the best solution here is to close the connection in such an error condition.
            headers.Connection = HttpConnection.Close;

            string contentStr =
                "<!DOCTYPE HTML PUBLIC \"-//W3C//DTD HTML 4.01//EN\" \"http://www.w3.org/TR/html4/strict.dtd\">\n" +
                "<html>\n <head>\n  <title>HTTP " + statusCodeName + "</title>\n </head>\n <body>\n  <h1>" + statusCodeName + "</h1>\n" +
                (message != null ? "  <p>" + message.HtmlEscape() + "</p>" : "") + "\n </body>\n</html>";
            return new HttpResponse
            {
                Status = statusCode,
                Headers = headers,
                Content = new MemoryStream(contentStr.ToUtf8())
            };
        }

        private void listeningThreadFunction()
        {
            while (true)
            {
                Socket socket = _listener.AcceptSocket();
                HandleRequest(socket, false);
            }
        }

        private class readingThreadRunner
        {
            private Socket socket;
            private HttpServer super;
            private Stopwatch sw;
            private string stopWatchFilename;
            private byte[] nextRead;
            private int nextReadOffset;
            private int nextReadLength;
            private byte[] buffer;
            private string headersSoFar;
            private SocketError errorCode;

            public Thread currentThread;

            public readingThreadRunner(Socket socket_, HttpServer super_)
            {
                socket = socket_;
                super = super_;
                currentThread = null;

                stopWatchFilename = socket.RemoteEndPoint.ToString().Replace(':', '_');
                sw = new StopwatchDummy();
                sw.Log("Start readingThreadRunner");

                nextRead = null;
                nextReadOffset = 0;
                nextReadLength = 0;

                buffer = new byte[65536];
                sw.Log("Allocate buffer");

                headersSoFar = "";

                beginReceive();
            }

            private void beginReceive()
            {
                if (nextRead != null)
                {
                    processData();
                    return;
                }

                nextRead = buffer;
                nextReadOffset = 0;

                sw.Log("beginReceive()");
                try { socket.BeginReceive(buffer, 0, 65536, SocketFlags.None, out errorCode, endReceive, this); }
                catch (SocketException) { socket.Close(); return; }
            }

            private void endReceive(IAsyncResult res)
            {
                sw.Log("Start of endReceive()");
                currentThread = Thread.CurrentThread;
                try
                {
                    try { nextReadLength = socket.EndReceive(res); }
                    catch (SocketException) { socket.Close(); return; }
                    if (nextReadLength == 0 || errorCode != SocketError.Success) { socket.Close(); return; }
                    processData();
                }
                finally
                {
                    currentThread = null;
                }
            }

            private void processData()
            {
                sw.Log("Start of processData()");

                // Stop soon if the headers become too large.
                if (headersSoFar.Length + nextReadLength > super._opt.MaxSizeHeaders)
                {
                    socket.Close();
                    return;
                }

                int prevHeadersLength = headersSoFar.Length;
                sw.Log("Stuff before headersSoFar += Encoding.UTF8.GetString(...)");
                headersSoFar += Encoding.UTF8.GetString(nextRead, nextReadOffset, nextReadLength);
                sw.Log("headersSoFar += Encoding.UTF8.GetString(...)");
                bool cont = headersSoFar.Contains("\r\n\r\n");
                sw.Log(@"HeadersSoFar.Contains(""\r\n\r\n"")");
                if (!cont)
                {
                    nextRead = null;
                    beginReceive();
                    return;
                }

                int sepIndex = headersSoFar.IndexOf("\r\n\r\n");
                sw.Log(@"int SepIndex = HeadersSoFar.IndexOf(""\r\n\r\n"")");
                headersSoFar = headersSoFar.Remove(sepIndex);
                sw.Log(@"HeadersSoFar = HeadersSoFar.Remove(SepIndex)");

                if (super.Log != null)
                    lock (super.Log)
                        super.Log.Info(headersSoFar);

                nextReadOffset += sepIndex + 4 - prevHeadersLength;
                nextReadLength -= sepIndex + 4 - prevHeadersLength;
                sw.Log("Stuff before HandleRequestAfterHeaders()");
                HttpRequest originalRequest;
                HttpResponse response = super.handleRequestAfterHeaders(socket, headersSoFar, nextRead, ref nextReadOffset, ref nextReadLength, sw, out originalRequest);
                response.OriginalRequest = originalRequest;
                sw.Log("Returned from HandleRequestAfterHeaders()");
                if (nextReadLength == 0)
                    nextRead = null;
                bool connectionKeepAlive = false;
                try
                {
                    sw.Log("Stuff before OutputResponse()");
                    connectionKeepAlive = super.outputResponse(socket, response, sw);
                    sw.Log("Returned from OutputResponse()");
                }
                catch (SocketException e)
                {
                    sw.Log("Caught SocketException - closing ({0})".Fmt(e.Message));
                    socket.Close();
                    sw.Log("Socket.Close()");
                    return;
                }
                finally
                {
                    if (response.Content != null)
                    {
                        sw.Log("Stuff before Response.Content.Close()");
                        response.Content.Close();
                        sw.Log("Response.Content.Close()");
                    }
                }

                if (connectionKeepAlive && socket.Connected)
                {
                    headersSoFar = "";
                    sw.Log("Reusing connection");
                    beginReceive();
                    return;
                }

                sw.Log("Stuff before Socket.Close()");
                socket.Close();
                sw.Log("Socket.Close()");
                return;
            }
        }

        private void sendHeaders(Socket socket, HttpResponse response)
        {
            string headersStr = "HTTP/1.1 " + ((int) response.Status) + " " + response.Status.ToText() + "\r\n" +
                response.Headers.ToString() + "\r\n";
            if (Log != null) Log.Info(headersStr);
            socket.Send(Encoding.UTF8.GetBytes(headersStr));
        }

        private bool outputResponse(Socket socket, HttpResponse response, Stopwatch sw)
        {
            sw.Log("OutputResponse() - enter");

            try
            {
                // If no Content-Type is given and there is no Location header, use default
                if (response.Headers.ContentType == null && response.Headers.Location == null)
                    response.Headers.ContentType = _opt.DefaultContentType;

                bool keepAliveRequested = response.OriginalRequest.Headers.Connection == HttpConnection.KeepAlive;
                bool gzipRequested = false;
                if (response.OriginalRequest.Headers.AcceptEncoding != null)
                    foreach (HttpContentEncoding hce in response.OriginalRequest.Headers.AcceptEncoding)
                        gzipRequested = gzipRequested || (hce == HttpContentEncoding.Gzip);
                bool contentLengthKnown = false;
                long contentLength = 0;

                // Find out if we know the content length
                if (response.Content == null)
                {
                    contentLength = 0;
                    contentLengthKnown = true;
                }
                else if (response.Headers.ContentLength != null)
                {
                    contentLength = response.Headers.ContentLength.Value;
                    contentLengthKnown = true;
                }
                else
                {
                    // See if we can deduce the content length from the stream
                    try
                    {
                        contentLength = response.Content.Length;
                        contentLengthKnown = true;
                    }
                    catch (NotSupportedException) { }
                }

                bool useKeepAlive = keepAliveRequested && response.OriginalRequest.HttpVersion == HttpProtocolVersion.Http11;
                if (useKeepAlive)
                    response.Headers.Connection = HttpConnection.KeepAlive;

                // If we know the content length and the stream can seek, then we can support Ranges - but it's not worth it for less than 16 KB
                if (response.OriginalRequest.HttpVersion == HttpProtocolVersion.Http11 && contentLengthKnown && contentLength > 16 * 1024 && response.Status == HttpStatusCode._200_OK && response.Content.CanSeek)
                {
                    response.Headers.AcceptRanges = HttpAcceptRanges.Bytes;

                    // If the client requested a range, then serve it
                    if (response.Status == HttpStatusCode._200_OK && response.OriginalRequest.Headers.Range != null)
                    {
                        // Construct a canonical set of satisfiable ranges
                        var ranges = new SortedList<long, long>();
                        foreach (var r in response.OriginalRequest.Headers.Range)
                        {
                            long rFrom = r.From == null || r.From.Value < 0 ? 0 : r.From.Value;
                            long rTo = r.To == null || r.To.Value >= contentLength ? contentLength - 1 : r.To.Value;
                            if (ranges.ContainsKey(rFrom))
                                ranges[rFrom] = Math.Max(ranges[rFrom], rTo);
                            else
                                ranges.Add(rFrom, rTo);
                        }

                        // If one of the ranges spans the complete file, don't bother with ranges
                        if (!ranges.ContainsKey(0) || ranges[0] < contentLength - 1)
                        {
                            // Make a copy of this so that we can modify Ranges while iterating over it
                            var rangeFroms = new List<long>(ranges.Keys);

                            long prevFrom = 0;
                            bool havePrevFrom = false;
                            foreach (long from in rangeFroms)
                            {
                                if (!havePrevFrom)
                                {
                                    prevFrom = from;
                                    havePrevFrom = true;
                                }
                                else if (ranges[prevFrom] >= from)
                                {
                                    ranges[prevFrom] = Math.Max(ranges[prevFrom], ranges[from]);
                                    ranges.Remove(from);
                                }
                            }

                            // Note that "ContentLength" here refers to the total size of the file.
                            // The functions ServeSingleRange() and ServeRanges() will automatically
                            // set a Content-Length header that specifies the size of just the range(s).

                            // Also note that if Ranges.Count is 0, we want to fall through and handle the request without ranges
                            if (ranges.Count == 1)
                            {
                                serveSingleRange(socket, response, ranges, contentLength);
                                return useKeepAlive;
                            }
                            else if (ranges.Count > 1)
                            {
                                serveRanges(socket, response, ranges, contentLength);
                                return useKeepAlive;
                            }
                        }
                    }
                }

                bool useGzip = response.UseGzip != UseGzipOption.DontUseGzip && gzipRequested && !(contentLengthKnown && contentLength <= 1024) && response.OriginalRequest.HttpVersion == HttpProtocolVersion.Http11;

                if (useGzip && response.UseGzip == UseGzipOption.AutoDetect && contentLengthKnown && contentLength >= _opt.GzipAutodetectThreshold && response.Content.CanSeek)
                {
                    try
                    {
                        response.Content.Seek((contentLength - _opt.GzipAutodetectThreshold) / 2, SeekOrigin.Begin);
                        byte[] buf = new byte[_opt.GzipAutodetectThreshold];
                        response.Content.Read(buf, 0, _opt.GzipAutodetectThreshold);
                        MemoryStream ms = new MemoryStream();
                        GZipOutputStream gzTester = new GZipOutputStream(ms);
                        gzTester.SetLevel(1);
                        gzTester.Write(buf, 0, _opt.GzipAutodetectThreshold);
                        gzTester.Close();
                        ms.Close();
                        if (ms.ToArray().Length >= 0.99 * _opt.GzipAutodetectThreshold)
                            useGzip = false;
                        response.Content.Seek(0, SeekOrigin.Begin);
                    }
                    catch { }
                }

                if (useGzip)
                    response.Headers.ContentEncoding = HttpContentEncoding.Gzip;

                sw.Log("OutputResponse() - find out things");

                // If we know the content length and it is smaller than the in-memory gzip threshold, gzip and output everything now
                if (useGzip && contentLengthKnown && contentLength < _opt.GzipInMemoryUpToSize)
                {
                    sw.Log("OutputResponse() - using in-memory gzip");
                    // In this case, do all the gzipping before sending the headers.
                    // After all we want to include the new (compressed) Content-Length.
                    MemoryStream ms = new MemoryStream();
                    GZipOutputStream gz = new GZipOutputStream(ms);
                    gz.SetLevel(1);
                    byte[] contentReadBuffer = new byte[65536];
                    int bytes = response.Content.Read(contentReadBuffer, 0, 65536);
                    while (bytes > 0)
                    {
                        gz.Write(contentReadBuffer, 0, bytes);
                        bytes = response.Content.Read(contentReadBuffer, 0, 65536);
                    }
                    gz.Close();
                    sw.Log("OutputResponse() - finished gzipping");
                    byte[] resultBuffer = ms.ToArray();
                    response.Headers.ContentLength = resultBuffer.Length;
                    sendHeaders(socket, response);
                    sw.Log("OutputResponse() - finished sending headers");
                    if (response.OriginalRequest.Method == HttpMethod.Head)
                        return useKeepAlive;
                    socket.Send(resultBuffer);
                    sw.Log("OutputResponse() - finished sending response");
                    return useKeepAlive;
                }

                sw.Log("OutputResponse() - using something other than in-memory gzip");

                Stream output;

                if (useGzip && !useKeepAlive)
                {
                    // In this case, send the headers first, then instantiate the GZipStream.
                    // Otherwise we run the risk that the GzipStream might write to the socket before the headers are sent.
                    // Also note that we are not sending a Content-Length header; even if we know the content length
                    // of the uncompressed file, we cannot predict the length of the compressed output yet
                    sendHeaders(socket, response);
                    sw.Log("OutputResponse() - sending headers");
                    if (response.OriginalRequest.Method == HttpMethod.Head)
                        return useKeepAlive;
                    StreamOnSocket str = new StreamOnSocket(socket);
                    output = new GZipOutputStream(str);
                    ((GZipOutputStream) output).SetLevel(1);
                }
                else if (useGzip)
                {
                    // In this case, combine Gzip with chunked Transfer-Encoding. No Content-Length header
                    response.Headers.TransferEncoding = HttpTransferEncoding.Chunked;
                    sendHeaders(socket, response);
                    sw.Log("OutputResponse() - sending headers");
                    if (response.OriginalRequest.Method == HttpMethod.Head)
                        return useKeepAlive;
                    StreamOnSocket str = new StreamOnSocketChunked(socket);
                    output = new GZipOutputStream(str);
                    ((GZipOutputStream) output).SetLevel(1);
                }
                else if (useKeepAlive && !contentLengthKnown)
                {
                    // Use chunked encoding without Gzip
                    response.Headers.TransferEncoding = HttpTransferEncoding.Chunked;
                    sendHeaders(socket, response);
                    sw.Log("OutputResponse() - sending headers");
                    if (response.OriginalRequest.Method == HttpMethod.Head)
                        return useKeepAlive;
                    output = new StreamOnSocketChunked(socket);
                }
                else
                {
                    // No Gzip, no chunked, but if we know the content length, supply it
                    // (if we don't, then we're not using keep-alive here)
                    if (contentLengthKnown)
                        response.Headers.ContentLength = contentLength;

                    sendHeaders(socket, response);
                    sw.Log("OutputResponse() - sending headers");

                    // If the content length is zero, we can exit as quickly as possible
                    // (no need to instantiate an output stream)
                    if ((contentLengthKnown && contentLength == 0) || response.OriginalRequest.Method == HttpMethod.Head)
                        return useKeepAlive;

                    output = new StreamOnSocket(socket);
                }

                sw.Log("OutputResponse() - instantiating output stream");

                // Finally output the actual content
                int bufferSize = 65536;
                byte[] buffer = new byte[bufferSize];
                sw.Log("OutputResponse() - Allocate buffer");
                int bytesRead;
                while (true)
                {
                    if (_opt.ReturnExceptionsToClient)
                    {
                        try { bytesRead = response.Content.Read(buffer, 0, bufferSize); }
                        catch (Exception e)
                        {
                            sendExceptionToClient(output, response.Headers.ContentType, e);
                            return false;
                        }
                    }
                    else
                        bytesRead = response.Content.Read(buffer, 0, bufferSize);
                    sw.Log("OutputResponse() - Response.Content.Read()");
                    if (bytesRead == 0) break;
                    output.Write(buffer, 0, bytesRead);
                    sw.Log("OutputResponse() - Output.Write()");
                }
                output.Close();
                sw.Log("OutputResponse() - Output.Close()");
                return useKeepAlive;
            }
            finally
            {
                sw.Log("OutputResponse() - stuff before finally clause");
                try
                {
                    if (response.OriginalRequest.TemporaryFile != null)
                        File.Delete(response.OriginalRequest.TemporaryFile);
                }
                catch (Exception) { }
                sw.Log("OutputResponse() - finally clause");
            }
        }

        private void serveSingleRange(Socket socket, HttpResponse response, SortedList<long, long> ranges, long totalFileSize)
        {
            foreach (var r in ranges)
            {
                response.Status = HttpStatusCode._206_PartialContent;
                // Note: this is the length of just the range, not the complete file (that's TotalFileSize)
                response.Headers.ContentLength = r.Value - r.Key + 1;
                response.Headers.ContentRange = new HttpContentRange { From = r.Key, To = r.Value, Total = totalFileSize };
                sendHeaders(socket, response);
                if (response.OriginalRequest.Method == HttpMethod.Head)
                    return;
                byte[] buffer = new byte[65536];

                response.Content.Seek(r.Key, SeekOrigin.Begin);
                long bytesMissing = r.Value - r.Key + 1;
                int bytesRead = response.Content.Read(buffer, 0, (int) Math.Min(65536, bytesMissing));
                while (bytesRead > 0)
                {
                    socket.Send(buffer, 0, bytesRead, SocketFlags.None);
                    bytesMissing -= bytesRead;
                    bytesRead = (bytesMissing > 0) ? response.Content.Read(buffer, 0, (int) Math.Min(65536, bytesMissing)) : 0;
                }
                return;
            }
        }

        private void serveRanges(Socket socket, HttpResponse response, SortedList<long, long> ranges, long totalFileSize)
        {
            response.Status = HttpStatusCode._206_PartialContent;

            // Generate a random boundary token
            byte[] boundary = new byte[64];

            for (int i = 0; i < 64; i++)
            {
                int r = Rnd.Next(16);
                boundary[i] = r < 10 ? ((byte) (r + '0')) : ((byte) (r + 'A' - 10));
            }

            // Calculate the total content length
            long cLength = 0;
            foreach (var r in ranges)
            {
                cLength += 68;                  // "--{boundary}\r\n"
                cLength += 27 +                 // "Content-range: bytes {f}-{l}/{filesize}\r\n\r\n"
                    r.Key.ToString().Length + r.Value.ToString().Length + totalFileSize.ToString().Length;
                cLength += r.Key - r.Value + 1; // content
                cLength += 2;                   // "\r\n"
            }
            cLength += 70;                      // "--{boundary}--\r\n"

            response.Headers.ContentLength = cLength;
            response.Headers.ContentType = "multipart/byteranges; boundary=" + Encoding.UTF8.GetString(boundary);
            sendHeaders(socket, response);
            if (response.OriginalRequest.Method == HttpMethod.Head)
                return;

            byte[] buffer = new byte[65536];
            foreach (var r in ranges)
            {
                socket.Send(new byte[] { (byte) '-', (byte) '-' });
                socket.Send(boundary);
                socket.Send(("\r\nContent-Range: bytes " + r.Key.ToString() + "-" + r.Value.ToString() + "/" + totalFileSize.ToString() + "\r\n\r\n").ToUtf8());

                response.Content.Seek(r.Key, SeekOrigin.Begin);
                long bytesMissing = r.Value - r.Key + 1;
                int bytesRead = response.Content.Read(buffer, 0, (int) Math.Min(65536, bytesMissing));
                while (bytesRead > 0)
                {
                    socket.Send(buffer, 0, bytesRead, SocketFlags.None);
                    bytesMissing -= bytesRead;
                    bytesRead = (bytesMissing > 0) ? response.Content.Read(buffer, 0, (int) Math.Min(65536, bytesMissing)) : 0;
                }
                socket.Send(new byte[] { 13, 10 });
            }
            socket.Send(new byte[] { (byte) '-', (byte) '-' });
            socket.Send(boundary);
            socket.Send(new byte[] { (byte) '-', (byte) '-', 13, 10 });
        }

        private HttpResponse handleRequestAfterHeaders(Socket socket, string headers, byte[] bufferWithContentSoFar, ref int contentOffset, ref int contentLengthSoFar, Stopwatch sw, out HttpRequest req)
        {
            sw.Log("HandleRequestAfterHeaders() - enter");

            string[] lines = headers.Split(new string[] { "\r\n" }, StringSplitOptions.None);
            req = new HttpRequest() { OriginIP = socket.RemoteEndPoint as IPEndPoint };
            if (lines.Length < 2)
                return ErrorResponse(HttpStatusCode._400_BadRequest);

            // Parse the method line
            Match match = Regex.Match(lines[0], @"^(GET|POST|HEAD) ([^ ]+) HTTP/1.([01])$");
            if (!match.Success)
                return ErrorResponse(HttpStatusCode._501_NotImplemented);

            sw.Log("HandleRequestAfterHeaders() - Stuff before setting HttpRequest members");
            req.HttpVersion = match.Groups[3].Value == "0" ? HttpProtocolVersion.Http10 : HttpProtocolVersion.Http11;
            req.Method =
                match.Groups[1].Value == "HEAD" ? HttpMethod.Head :
                match.Groups[1].Value == "POST" ? HttpMethod.Post : HttpMethod.Get;
            req.Url = match.Groups[2].Value;
            req.TempDir = _opt.TempDir;   // this will only be used if there is a file upload in a POST request.

            sw.Log("HandleRequestAfterHeaders() - setting HttpRequest members");

            // Parse the request headers
            try
            {
                string lastHeader = null;
                string valueSoFar = null;
                for (int i = 1; i < lines.Length; i++)
                {
                    if (lines[i][0] == '\t' || lines[i][0] == ' ')
                        valueSoFar += " " + lines[i].Trim();
                    else
                    {
                        match = Regex.Match(lines[i], @"^([-A-Za-z0-9_]+)\s*:\s*(.*)$");
                        if (!match.Success)
                            return ErrorResponse(HttpStatusCode._400_BadRequest);
                        parseHeader(lastHeader, valueSoFar, ref req);
                        lastHeader = match.Groups[1].Value;
                        valueSoFar = match.Groups[2].Value.Trim();
                    }
                }
                parseHeader(lastHeader, valueSoFar, ref req);
            }
            catch (InvalidRequestException e)
            {
                return e.Response;
            }

            sw.Log("HandleRequestAfterHeaders() - Parse headers");

            if (req.Handler == null)
                return ErrorResponse(HttpStatusCode._404_NotFound);

            if (req.Method == HttpMethod.Post)
            {
                // Some validity checks
                if (req.Headers.ContentLength == null)
                    return ErrorResponse(HttpStatusCode._411_LengthRequired);
                if (req.Headers.ContentLength.Value > _opt.MaxSizePostContent)
                    return ErrorResponse(HttpStatusCode._413_RequestEntityTooLarge);
                if (req.Headers.ContentType == HttpPostContentType.None)
                    return ErrorResponse(HttpStatusCode._501_NotImplemented, @"""Content-Type"" must be specified. Moreover, only ""application/x-www-form-urlencoded"" and ""multipart/form-data"" are supported.");

                // If "Expect: 100-continue" was specified, send a 100 Continue here
                if (req.Headers.Expect != null && req.Headers.Expect.ContainsKey("100-continue"))
                    socket.Send("HTTP/1.1 100 Continue\r\n\r\n".ToUtf8());

                // Read the contents of the POST request
                if (contentLengthSoFar >= req.Headers.ContentLength.Value)
                {
                    req.Content = new MemoryStream(bufferWithContentSoFar, contentOffset, (int) req.Headers.ContentLength.Value, false);
                    contentOffset += (int) req.Headers.ContentLength.Value;
                    contentLengthSoFar -= (int) req.Headers.ContentLength.Value;
                }
                else if (req.Headers.ContentLength.Value < _opt.UseFileUploadAtSize)
                {
                    // Receive the POST request content into an in-memory buffer
                    byte[] buffer = new byte[req.Headers.ContentLength.Value];
                    if (contentLengthSoFar > 0)
                        Array.Copy(bufferWithContentSoFar, contentOffset, buffer, 0, contentLengthSoFar);
                    while (contentLengthSoFar < req.Headers.ContentLength)
                    {
                        SocketError errorCode;
                        int bytesReceived = socket.Receive(buffer, contentLengthSoFar, (int) req.Headers.ContentLength.Value - contentLengthSoFar, SocketFlags.None, out errorCode);
                        if (errorCode != SocketError.Success)
                            throw new SocketException();
                        contentLengthSoFar += bytesReceived;
                    }
                    req.Content = new MemoryStream(buffer, 0, (int) req.Headers.ContentLength.Value);
                    contentLengthSoFar = 0;
                }
                else
                {
                    // Store the POST request content in a temporary file
                    Stream f;
                    try
                    {
                        req.TemporaryFile = HttpInternalObjects.RandomTempFilepath(_opt.TempDir, out f);
                    }
                    catch (IOException)
                    {
                        return ErrorResponse(HttpStatusCode._500_InternalServerError);
                    }
                    try
                    {
                        if (contentLengthSoFar > 0)
                            f.Write(bufferWithContentSoFar, contentOffset, contentLengthSoFar);
                        byte[] buffer = new byte[65536];
                        while (contentLengthSoFar < req.Headers.ContentLength)
                        {
                            SocketError errorCode;
                            int bytesReceived = socket.Receive(buffer, 0, 65536, SocketFlags.None, out errorCode);
                            if (errorCode != SocketError.Success)
                                throw new SocketException();
                            if (bytesReceived > 0)
                            {
                                f.Write(buffer, 0, bytesReceived);
                                contentLengthSoFar += bytesReceived;
                            }
                        }
                        f.Close();
                        req.Content = File.Open(req.TemporaryFile, FileMode.Open, FileAccess.Read, FileShare.Read);
                        contentLengthSoFar = 0;
                    }
                    catch (Exception e)
                    {
                        f.Close();
                        File.Delete(req.TemporaryFile);
                        throw e;
                    }
                }
            }

            sw.Log("HandleRequestAfterHeaders() - Stuff before Req.Handler()");

            if (_opt.ReturnExceptionsToClient)
            {
                try
                {
                    HttpResponse resp = req.Handler(req);
                    sw.Log("HandleRequestAfterHeaders() - Req.Handler()");
                    return resp;
                }
                catch (InvalidRequestException e)
                {
                    sw.Log("HandleRequestAfterHeaders() - InvalidRequestException()");
                    return e.Response;
                }
                catch (Exception e)
                {
                    HttpResponse resp = ExceptionResponse(e);
                    sw.Log("HandleRequestAfterHeaders() - ExceptionResponse()");
                    return resp;
                }
            }
            else
            {
                try
                {
                    HttpResponse resp = req.Handler(req);
                    sw.Log("HandleRequestAfterHeaders() - Req.Handler()");
                    return resp;
                }
                catch (InvalidRequestException e)
                {
                    sw.Log("HandleRequestAfterHeaders() - InvalidRequestException()");
                    return e.Response;
                }
            }
        }

        /// <summary>
        /// Generates a 500 Internal Server Error response which formats the specified exception as HTML.
        /// </summary>
        /// <param name="e">Exception to format.</param>
        /// <returns>An HttpResponse to use to respond to a client request which threw the exception.</returns>
        public static HttpResponse ExceptionResponse(Exception e)
        {
            return new HttpResponse
            {
                Status = HttpStatusCode._500_InternalServerError,
                Headers = new HttpResponseHeaders { ContentType = "text/html; charset=utf-8" },
                Content = new MemoryStream(ExceptionAsString(e, true).ToUtf8())
            };
        }

        private void sendExceptionToClient(Stream output, string contentType, Exception exception)
        {
            if (exception is SocketException)
                throw exception;

            byte[] outp = ExceptionAsString(exception,
                contentType.StartsWith("text/html") || contentType.StartsWith("application/xhtml")).ToUtf8();
            output.Write(outp, 0, outp.Length);
            output.Close();
        }

        /// <summary>
        /// Generates a string describing the <paramref name="exception"/>, including the type, message
        /// and stack trace, and iterating over the InnerException chain.
        /// </summary>
        /// <param name="exception">The exception to be described.</param>
        /// <param name="html">If true, an HTML "DIV" tag will be returned with formatted info. Otherwise, a plaintext message is generated.</param>
        public static string ExceptionAsString(Exception exception, bool html)
        {
            bool first = true;
            if (html)
            {
                string exceptionText = "";
                while (exception != null)
                {
                    var exc = "<h3>" + exception.GetType().FullName.HtmlEscape() + "</h3>";
                    exc += "<p>" + exception.Message.HtmlEscape() + "</p>";
                    exc += "<pre>" + exception.StackTrace.HtmlEscape() + "</pre>";
                    exc += first ? "" : "<hr />";
                    exceptionText = exc + exceptionText;
                    exception = exception.InnerException;
                    first = false;
                }
                return "<div class='exception'>" + exceptionText + "</div>";
            }
            else        // Assume plain text
            {
                string exceptionText = "";
                while (exception != null)
                {
                    var exc = exception.GetType().FullName + "\n\n";
                    exc += exception.Message + "\n\n";
                    exc += exception.StackTrace + "\n\n";
                    exc += first ? "\n\n\n" : "\n----------------------------------------------------------------------\n";
                    exceptionText = exc + exceptionText;
                    exception = exception.InnerException;
                    first = false;
                }
                return exceptionText;
            }
        }

        private void parseHeader(string headerName, string headerValue, ref HttpRequest req)
        {
            if (headerName == null)
                return;

            if (!req.Headers.parseAndAddHeader(headerName, headerValue))
                return; // the header was not recognised so just do nothing. It has been added to UnrecognisedHeaders etc.

            string nameLower = headerName.ToLowerInvariant();

            // Special actions when we encounter certain headers
            if (nameLower == "host")
            {
                // For performance reasons, we check if we have a handler for this domain/URL as soon as possible.
                // If we find out that we don't, stop processing here and immediately output an error
                if (req.Url.StartsWith("/$/"))
                    req.Handler = internalHandler;
                else
                {
                    string host = req.Headers.Host;
                    int port = 80;
                    if (host.Contains(":"))
                    {
                        int pos = host.IndexOf(":");
                        int.TryParse(host.Substring(pos + 1), out port);
                        host = host.Remove(pos);
                    }
                    host = host.TrimEnd('.');

                    string url = req.Url.Contains('?') ? req.Url.Remove(req.Url.IndexOf('?')) : req.Url;

                    lock (RequestHandlerHooks)
                    {
                        var hook = RequestHandlerHooks.FirstOrDefault(hk => (hk.Port == null || hk.Port.Value == port) &&
                                (hk.Domain == null || hk.Domain == host || (!hk.SpecificDomain && host.EndsWith("." + hk.Domain))) &&
                                (hk.Path == null || hk.Path == url || (!hk.SpecificPath && url.StartsWith(hk.Path + "/"))));
                        if (hook == null)
                            throw new InvalidRequestException(ErrorResponse(HttpStatusCode._404_NotFound));

                        req.Handler = hook.Handler;
                        req.BaseUrl = hook.Path == null ? "" : hook.Path;
                        req.RestUrl = hook.Path == null ? req.Url : req.Url.Substring(hook.Path.Length);
                        req.Domain = host;
                        req.BaseDomain = hook.Domain == null ? "" : hook.Domain;
                        req.RestDomain = hook.Domain == null ? host : host.Remove(host.Length - hook.Domain.Length);
                    }
                }
            }
            else if (nameLower == "expect")
            {
                foreach (var kvp in req.Headers.Expect)
                    if (kvp.Key != "100-continue")
                        throw new InvalidRequestException(ErrorResponse(HttpStatusCode._417_ExpectationFailed));
            }
        }

        private HttpResponse internalHandler(HttpRequest req)
        {
            if (req.Url == "/$/directory-listing/xsl")
            {
                return new HttpResponse
                {
                    Headers = new HttpResponseHeaders { ContentType = "application/xml; charset=utf-8" },
                    Content = new MemoryStream(HttpInternalObjects.DirectoryListingXsl())
                };
            }
            else if (req.Url.StartsWith("/$/directory-listing/icons/"))
            {
                string rest = req.Url.Substring(27);
                return new HttpResponse
                {
                    Headers = new HttpResponseHeaders { ContentType = "image/png" },
                    Content = new MemoryStream(HttpInternalObjects.GetDirectoryListingIcon(rest))
                };
            }

            return ErrorResponse(HttpStatusCode._404_NotFound);
        }

        /// <summary>
        /// Returns a file size in user-readable format, using units like KB, MB, GB, TB.
        /// </summary>
        /// <param name="size">Size of a file in bytes.</param>
        /// <returns>User-readable formatted file size.</returns>
        public static string PrettySize(long size)
        {
            if (size >= (1L << 40))
                return string.Format("{0:0.00} TB", (double) size / (1L << 40));
            if (size >= (1L << 30))
                return string.Format("{0:0.00} GB", (double) size / (1L << 30));
            if (size >= (1L << 20))
                return string.Format("{0:0.00} MB", (double) size / (1L << 20));
            return string.Format("{0:0.00} KB", (double) size / (1L << 10));
        }

        /// <summary>
        /// Handles an incoming connection. This function can be used to let the server handle a TCP connection
        /// that was received by some other component outside the HttpServer class.
        /// </summary>
        /// <param name="incomingConnection">The incoming connection to process.</param>
        /// <param name="blocking">If true, returns after the request has been processed and the connection closed.
        /// If false, spawns a new thread and returns immediately.</param>
        public void HandleRequest(Socket incomingConnection, bool blocking)
        {
            if (_opt.IdleTimeout != 0)
                incomingConnection.ReceiveTimeout = _opt.IdleTimeout;
            var readingThreadRunner = new readingThreadRunner(incomingConnection, this);
            lock (_activeReadingThreads)
                _activeReadingThreads.Add(readingThreadRunner);
        }
    }
}
