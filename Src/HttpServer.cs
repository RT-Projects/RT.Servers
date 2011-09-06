using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using ICSharpCode.SharpZipLib.GZip;
using RT.Util;
using RT.Util.ExtensionMethods;
using RT.Util.Streams;

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
        public HttpServer(HttpServerOptions options = null)
        {
            _opt = options ?? new HttpServerOptions();
            Stats = new Statistics(this);
        }

        /// <summary>
        /// Returns the configuration settings currently in effect for this server.
        /// </summary>
        public HttpServerOptions Options { get { return _opt; } }

        /// <summary>
        /// Gets an object containing various server performance statistics.
        /// </summary>
        public Statistics Stats { get; private set; }

        /// <summary>
        /// Returns a boolean specifying whether the server is currently running (listening) in non-blocking mode.
        /// If the server is running in blocking mode, this will return false even if it is listening.
        /// </summary>
        public bool IsListeningThreadActive { get { return _listeningThread != null && _listeningThread.IsAlive; } }

        private TcpListener _listener;
        private Thread _listeningThread;
        private HttpServerOptions _opt;
        private HashSet<readingThreadRunner> _activeReadingThreads = new HashSet<readingThreadRunner>();

        /// <summary>Add request handlers here. See the documentation for <see cref="HttpRequestHandlerHook"/> for more information.
        /// If you wish to make changes to this list while the server is running, take a lock on this list while making the changes.</summary>
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
                    {
                        thr.Abort = true;
                        if (thr.CurrentThread != null)
                            thr.CurrentThread.Abort();
                    }
                _activeReadingThreads.Clear(); // paranoia to be extra safe against leaks
                _activeReadingThreads = new HashSet<readingThreadRunner>();
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

            IPAddress addr;
            if (_opt.BindAddress == null || !IPAddress.TryParse(_opt.BindAddress, out addr))
                addr = IPAddress.Any;
            _listener = new TcpListener(addr, _opt.Port);
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

        private void listeningThreadFunction()
        {
            while (true)
            {
                Socket socket = _listener.AcceptSocket();
                HandleRequest(socket, false);
            }
        }

        private sealed class readingThreadRunner
        {
            private Socket _socket;
            private Stopwatch _sw;
            private string _stopWatchFilename;
            private byte[] _buffer;
            private int _bufferDataOffset;
            private int _bufferDataLength;
            private string _headersSoFar;
            private SocketError _errorCode;
            private LoggerBase _log;
            private HttpServer _server;
            private int _begunReceives = 0;
            private int _endedReceives = 0;

            public Thread CurrentThread;
            public bool Abort;
            public bool KeepAliveActive = false;

            public readingThreadRunner(Socket socket, HttpServer server, LoggerBase log)
            {
                _socket = socket;
                _server = server;
                _log = log;

                CurrentThread = null;
                Abort = false;

                _stopWatchFilename = _socket.RemoteEndPoint.ToString().Replace(':', '_');
                _sw = new StopwatchDummy();
                _sw.Log("ctor() - Start readingThreadRunner");

                _buffer = new byte[1024];
                _bufferDataOffset = 0;
                _bufferDataLength = 0;

                _headersSoFar = "";
                _sw.Log("ctor() - end");
            }

            public void Begin()
            {
                // Sometimes the whole lot happens synchronously, probably when the data is already fully received by the OS.
                // The cleanup method needs the runner to already be in the list of runners. In order for it to get into that list,
                // the cleanup method must never happen before the constructor returns. Hence, Begin() must be a separate method.
                try
                {
                    beginReceive();
                }
                finally
                {
                    // if there are no outstanding endReceives...
                    if (Interlocked.CompareExchange(ref _begunReceives, 0, 0) <= Interlocked.CompareExchange(ref _endedReceives, 0, 0))
                        cleanup();
                }
            }

            private void beginReceive()
            {
                // If there is data left to be processed, process it
                if (_bufferDataLength > 0)
                {
                    processHeaderData();
                    return;
                }

                // Need to receive more data from the socket
                _bufferDataOffset = 0;
                try
                {
                    _socket.BeginReceive(_buffer, 0, _buffer.Length, SocketFlags.None, out _errorCode, endReceive, this);
                    Interlocked.Increment(ref _begunReceives);
                }
                catch (SocketException) { _socket.Close(); return; }
            }

            private void endReceive(IAsyncResult res)
            {
                KeepAliveActive = false;
                Interlocked.Increment(ref _endedReceives);

                try
                {
                    if (Abort)
                        return;
                    CurrentThread = Thread.CurrentThread;
                    try
                    {
                        try { _bufferDataLength = _socket.EndReceive(res); }
                        catch (SocketException) { _socket.Close(); return; }
                        if (_bufferDataLength == 0 || _errorCode != SocketError.Success) { _socket.Close(); return; }
                        processHeaderData();
                    }
                    finally
                    {
                        CurrentThread = null;
                    }
                }
                finally
                {
                    // if there are no outstanding endReceives...
                    if (Interlocked.CompareExchange(ref _begunReceives, 0, 0) <= Interlocked.CompareExchange(ref _endedReceives, 0, 0))
                        cleanup();
                }
            }

            private void cleanup()
            {
                lock (_server._activeReadingThreads)
                    _server._activeReadingThreads.Remove(this);
            }

            private void processHeaderData()
            {
                _sw.Log("Start of processHeaderData()");

                // Stop soon if the headers become too large.
                if (_headersSoFar.Length + _bufferDataLength > _server.Options.MaxSizeHeaders)
                {
                    _socket.Close();
                    return;
                }

                int prevHeadersLength = _headersSoFar.Length;
                _sw.Log("Stuff before headersSoFar += Encoding.UTF8.GetString(...)");
                _headersSoFar += Encoding.UTF8.GetString(_buffer, _bufferDataOffset, _bufferDataLength);
                _sw.Log("headersSoFar += Encoding.UTF8.GetString(...)");
                bool cont = _headersSoFar.Contains("\r\n\r\n");
                _sw.Log(@"HeadersSoFar.Contains(""\r\n\r\n"")");
                if (!cont)
                {
                    _bufferDataLength = 0;
                    beginReceive();
                    return;
                }

                int sepIndex = _headersSoFar.IndexOf("\r\n\r\n");
                _sw.Log(@"int SepIndex = HeadersSoFar.IndexOf(""\r\n\r\n"")");
                _headersSoFar = _headersSoFar.Remove(sepIndex);
                _sw.Log(@"HeadersSoFar = HeadersSoFar.Remove(SepIndex)");

                if (_log != null)
                    lock (_log)
                        _log.Info(_headersSoFar);

                _bufferDataOffset += sepIndex + 4 - prevHeadersLength;
                _bufferDataLength -= sepIndex + 4 - prevHeadersLength;
                _sw.Log("Stuff before HandleRequestAfterHeaders()");
                HttpRequest originalRequest;
                HttpResponse response = handleRequestAfterHeaders(out originalRequest);
                _sw.Log("Returned from HandleRequestAfterHeaders()");
                bool connectionKeepAlive = false;
                try
                {
                    _sw.Log("Stuff before OutputResponse()");
                    connectionKeepAlive = outputResponse(response, originalRequest);
                    _sw.Log("Returned from OutputResponse()");
                }
                catch (SocketException e)
                {
                    _sw.Log("Caught SocketException - closing ({0})".Fmt(e.Message));
                    _socket.Close();
                    _sw.Log("Socket.Close()");
                    return;
                }
                finally
                {
                    if (response.Content != null)
                    {
                        _sw.Log("Stuff before Response.Content.Close()");
                        response.Content.Close();
                        _sw.Log("Response.Content.Close()");
                    }
                    if (response.CleanUpCallback != null)
                    {
                        _sw.Log("Stuff before Response.CleanUpCallback()");
                        response.CleanUpCallback();
                        _sw.Log("Response.CleanUpCallback()");
                    }
                }

                if (connectionKeepAlive && _socket.Connected)
                {
                    _headersSoFar = "";
                    _sw.Log("Reusing connection");
                    KeepAliveActive = true;
                    beginReceive();
                    return;
                }

                _sw.Log("Stuff before Socket.Close()");
                _socket.Close();
                _sw.Log("Socket.Close()");
                return;
            }

            private bool outputResponse(HttpResponse response, HttpRequest originalRequest)
            {
                _socket.NoDelay = false;
                _sw.Log("OutputResponse() - enter");

                try
                {
                    // If no Content-Type is given and there is no Location header, use default
                    if (response.Headers.ContentType == null && response.Headers.Location == null)
                        response.Headers.ContentType = _server.Options.DefaultContentType;

                    bool keepAliveRequested = originalRequest.Headers.Connection == HttpConnection.KeepAlive;
                    bool gzipRequested = false;
                    if (originalRequest.Headers.AcceptEncoding != null)
                        foreach (HttpContentEncoding hce in originalRequest.Headers.AcceptEncoding)
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

                    bool useKeepAlive = keepAliveRequested && originalRequest.HttpVersion == HttpProtocolVersion.Http11;
                    if (useKeepAlive)
                        response.Headers.Connection = HttpConnection.KeepAlive;

                    // Special case: empty body
                    if (contentLengthKnown && contentLength == 0)
                    {
                        // 304 Not Modified doesn’t need Content-Type. 
                        if (response.Status == HttpStatusCode._304_NotModified)
                        {
                            // Also omit ContentLength if not using keep-alive
                            if (!useKeepAlive)
                                response.Headers.ContentLength = null;
                            response.Headers.ContentType = null;
                        }
                        else
                            response.Headers.ContentLength = 0;
                        sendHeaders(response);
                        return useKeepAlive;
                    }

                    // If we know the content length and the stream can seek, then we can support Ranges - but it's not worth it for less than 16 KB
                    if (originalRequest.HttpVersion == HttpProtocolVersion.Http11 && contentLengthKnown && contentLength > 16 * 1024 && response.Status == HttpStatusCode._200_OK && response.Content.CanSeek)
                    {
                        response.Headers.AcceptRanges = HttpAcceptRanges.Bytes;

                        // If the client requested a range, then serve it
                        if (response.Status == HttpStatusCode._200_OK && originalRequest.Headers.Range != null)
                        {
                            // Construct a canonical set of satisfiable ranges
                            var ranges = new SortedList<long, long>();
                            foreach (var r in originalRequest.Headers.Range)
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
                                    var range = ranges.First();
                                    serveSingleRange(response, originalRequest, range.Key, range.Value, contentLength);
                                    return useKeepAlive;
                                }
                                else if (ranges.Count > 1)
                                {
                                    serveRanges(response, originalRequest, ranges, contentLength);
                                    return useKeepAlive;
                                }
                            }
                        }
                    }

                    bool useGzip = response.UseGzip != UseGzipOption.DontUseGzip && gzipRequested && !(contentLengthKnown && contentLength <= 1024) && originalRequest.HttpVersion == HttpProtocolVersion.Http11;

                    if (useGzip && response.UseGzip == UseGzipOption.AutoDetect && contentLengthKnown && contentLength >= _server.Options.GzipAutodetectThreshold && response.Content.CanSeek)
                    {
                        try
                        {
                            response.Content.Seek((contentLength - _server.Options.GzipAutodetectThreshold) / 2, SeekOrigin.Begin);
                            byte[] buf = new byte[_server.Options.GzipAutodetectThreshold];
                            response.Content.Read(buf, 0, _server.Options.GzipAutodetectThreshold);
                            MemoryStream ms = new MemoryStream();
                            GZipOutputStream gzTester = new GZipOutputStream(ms);
                            gzTester.SetLevel(1);
                            gzTester.Write(buf, 0, _server.Options.GzipAutodetectThreshold);
                            gzTester.Close();
                            ms.Close();
                            if (ms.ToArray().Length >= 0.99 * _server.Options.GzipAutodetectThreshold)
                                useGzip = false;
                            response.Content.Seek(0, SeekOrigin.Begin);
                        }
                        catch { }
                    }

                    if (useGzip)
                        response.Headers.ContentEncoding = HttpContentEncoding.Gzip;

                    _sw.Log("OutputResponse() - find out things");

                    // If we know the content length and it is smaller than the in-memory gzip threshold, gzip and output everything now
                    if (useGzip && contentLengthKnown && contentLength < _server.Options.GzipInMemoryUpToSize)
                    {
                        _sw.Log("OutputResponse() - using in-memory gzip");
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
                        _sw.Log("OutputResponse() - finished gzipping");
                        byte[] resultBuffer = ms.ToArray();
                        response.Headers.ContentLength = resultBuffer.Length;
                        sendHeaders(response);
                        _sw.Log("OutputResponse() - finished sending headers");
                        if (originalRequest.Method == HttpMethod.Head)
                            return useKeepAlive;
                        _socket.Send(resultBuffer);
                        _sw.Log("OutputResponse() - finished sending response");
                        return useKeepAlive;
                    }

                    _sw.Log("OutputResponse() - using something other than in-memory gzip");

                    Stream output;

                    if (useGzip && !useKeepAlive)
                    {
                        // In this case, send the headers first, then instantiate the GZipStream.
                        // Otherwise we run the risk that the GzipStream might write to the socket before the headers are sent.
                        // Also note that we are not sending a Content-Length header; even if we know the content length
                        // of the uncompressed file, we cannot predict the length of the compressed output yet
                        sendHeaders(response);
                        _sw.Log("OutputResponse() - sending headers");
                        if (originalRequest.Method == HttpMethod.Head)
                            return useKeepAlive;
                        SocketWriterStream str = new SocketWriterStream(_socket);
                        output = new GZipOutputStream(str);
                        ((GZipOutputStream) output).SetLevel(1);
                    }
                    else if (useGzip)
                    {
                        // In this case, combine Gzip with chunked Transfer-Encoding. No Content-Length header
                        response.Headers.TransferEncoding = HttpTransferEncoding.Chunked;
                        sendHeaders(response);
                        _sw.Log("OutputResponse() - sending headers");
                        if (originalRequest.Method == HttpMethod.Head)
                            return useKeepAlive;
                        SocketWriterStream str = new ChunkedSocketWriterStream(_socket);
                        output = new GZipOutputStream(str);
                        ((GZipOutputStream) output).SetLevel(1);
                    }
                    else if (useKeepAlive && !contentLengthKnown)
                    {
                        // Use chunked encoding without Gzip
                        response.Headers.TransferEncoding = HttpTransferEncoding.Chunked;
                        sendHeaders(response);
                        _sw.Log("OutputResponse() - sending headers");
                        if (originalRequest.Method == HttpMethod.Head)
                            return useKeepAlive;
                        output = new ChunkedSocketWriterStream(_socket);
                    }
                    else
                    {
                        // No Gzip, no chunked, but if we know the content length, supply it
                        // (if we don't, then we're not using keep-alive here)
                        if (contentLengthKnown)
                            response.Headers.ContentLength = contentLength;

                        sendHeaders(response);
                        _sw.Log("OutputResponse() - sending headers");

                        if (originalRequest.Method == HttpMethod.Head)
                            return useKeepAlive;

                        output = new SocketWriterStream(_socket);
                    }

                    _sw.Log("OutputResponse() - instantiating output stream");

                    // Finally output the actual content
                    byte[] buffer = new byte[65536];
                    int bufferSize = buffer.Length;
                    _sw.Log("OutputResponse() - Allocate buffer");
                    int bytesRead;
                    while (true)
                    {
                        if (_server.Options.ReturnExceptionsToClient)
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
                        _sw.Log("OutputResponse() - Response.Content.Read()");
                        if (bytesRead == 0) break;

                        // Performance optimisation: If we’re at the end of a body of known length, cause
                        // the last bit to be sent to the socket without the Nagle delay
                        try
                        {
                            if (response.Content.CanSeek && response.Content.Position == response.Content.Length)
                                _socket.NoDelay = true;
                        }
                        catch { }

                        output.Write(buffer, 0, bytesRead);

                        // If we are actually gzipping and chunking something of known length, we don’t want several TCP packets
                        // of just a few bytes from when the gzip and chunked stream finish. The chunked stream already sets this
                        // back to true just before outputting the real final bit.
                        _socket.NoDelay = false;

                        _sw.Log("OutputResponse() - Output.Write()");
                    }
                    output.Close();
                    _sw.Log("OutputResponse() - Output.Close()");
                    return useKeepAlive;
                }
                finally
                {
                    _sw.Log("OutputResponse() - stuff before finally clause");
                    try
                    {
                        if (originalRequest.FileUploads != null)
                            foreach (var fileUpload in originalRequest.FileUploads.Values.Where(fu => fu.LocalFilename != null && !fu.LocalFileMoved))
                                File.Delete(fileUpload.LocalFilename);
                    }
                    catch (Exception) { }
                    _sw.Log("OutputResponse() - finally clause");
                }
            }

            private void sendExceptionToClient(Stream output, string contentType, Exception exception)
            {
                if (exception is SocketException)
                    throw exception;

                byte[] outp = HttpResponse.ExceptionAsString(exception,
                    contentType.StartsWith("text/html") || contentType.StartsWith("application/xhtml")).ToUtf8();
                output.Write(outp, 0, outp.Length);
                output.Close();
            }

            private void sendHeaders(HttpResponse response)
            {
                string headersStr = "HTTP/1.1 " + ((int) response.Status) + " " + response.Status.ToText() + "\r\n" +
                    response.Headers.ToString() + "\r\n";
                if (_log != null)
                    lock (_log)
                        _log.Info(headersStr);
                _socket.Send(Encoding.UTF8.GetBytes(headersStr));
            }

            private void serveSingleRange(HttpResponse response, HttpRequest originalRequest, long rangeFrom, long rangeTo, long totalFileSize)
            {
                response.Status = HttpStatusCode._206_PartialContent;
                // Note: this is the length of just the range, not the complete file (that's totalFileSize)
                response.Headers.ContentLength = rangeTo - rangeFrom + 1;
                response.Headers.ContentRange = new HttpContentRange { From = rangeFrom, To = rangeTo, Total = totalFileSize };
                sendHeaders(response);
                if (originalRequest.Method == HttpMethod.Head)
                    return;
                byte[] buffer = new byte[65536];

                response.Content.Seek(rangeFrom, SeekOrigin.Begin);
                long bytesMissing = rangeTo - rangeFrom + 1;
                int bytesRead = response.Content.Read(buffer, 0, (int) Math.Min(65536, bytesMissing));
                while (bytesRead > 0)
                {
                    _socket.Send(buffer, 0, bytesRead, SocketFlags.None);
                    bytesMissing -= bytesRead;
                    bytesRead = (bytesMissing > 0) ? response.Content.Read(buffer, 0, (int) Math.Min(65536, bytesMissing)) : 0;
                }
            }

            private void serveRanges(HttpResponse response, HttpRequest originalRequest, SortedList<long, long> ranges, long totalFileSize)
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
                sendHeaders(response);
                if (originalRequest.Method == HttpMethod.Head)
                    return;

                byte[] buffer = new byte[65536];
                foreach (var r in ranges)
                {
                    _socket.Send(new byte[] { (byte) '-', (byte) '-' });
                    _socket.Send(boundary);
                    _socket.Send(("\r\nContent-Range: bytes " + r.Key.ToString() + "-" + r.Value.ToString() + "/" + totalFileSize.ToString() + "\r\n\r\n").ToUtf8());

                    response.Content.Seek(r.Key, SeekOrigin.Begin);
                    long bytesMissing = r.Value - r.Key + 1;
                    int bytesRead = response.Content.Read(buffer, 0, (int) Math.Min(65536, bytesMissing));
                    while (bytesRead > 0)
                    {
                        _socket.Send(buffer, 0, bytesRead, SocketFlags.None);
                        bytesMissing -= bytesRead;
                        bytesRead = (bytesMissing > 0) ? response.Content.Read(buffer, 0, (int) Math.Min(65536, bytesMissing)) : 0;
                    }
                    _socket.Send(new byte[] { 13, 10 });
                }
                _socket.Send(new byte[] { (byte) '-', (byte) '-' });
                _socket.Send(boundary);
                _socket.Send(new byte[] { (byte) '-', (byte) '-', 13, 10 });
            }

            private HttpResponse handleRequestAfterHeaders(out HttpRequest req)
            {
                _sw.Log("HandleRequestAfterHeaders() - enter");

                string[] lines = _headersSoFar.Split(new string[] { "\r\n" }, StringSplitOptions.None);
                req = new HttpRequest() { OriginIP = _socket.RemoteEndPoint as IPEndPoint };
                if (lines.Length < 2)
                    return HttpResponse.Error(HttpStatusCode._400_BadRequest, connectionClose: true);

                // Parse the method line
                _sw.Log("HandleRequestAfterHeaders() - Stuff before setting HttpRequest members");
                var line = lines[0];
                if (line.StartsWith("GET "))
                    req.Method = HttpMethod.Get;
                else if (line.StartsWith("HEAD "))
                    req.Method = HttpMethod.Head;
                else if (line.StartsWith("POST "))
                    req.Method = HttpMethod.Post;
                else
                    return HttpResponse.Error(HttpStatusCode._501_NotImplemented, connectionClose: true);

                if (line.EndsWith(" HTTP/1.0"))
                    req.HttpVersion = HttpProtocolVersion.Http10;
                else if (line.EndsWith(" HTTP/1.1"))
                    req.HttpVersion = HttpProtocolVersion.Http11;
                else
                    return HttpResponse.Error(HttpStatusCode._505_HttpVersionNotSupported, connectionClose: true);

                int start = req.Method == HttpMethod.Get ? 4 : 5;
                req.Url = line.Substring(start, line.Length - start - 9);
                if (req.Url.Contains(' '))
                    return HttpResponse.Error(HttpStatusCode._400_BadRequest, connectionClose: true);

                _sw.Log("HandleRequestAfterHeaders() - setting HttpRequest members");

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
                            var match = Regex.Match(lines[i], @"^([-A-Za-z0-9_]+)\s*:\s*(.*)$");
                            if (!match.Success)
                                return HttpResponse.Error(HttpStatusCode._400_BadRequest, connectionClose: true);
                            parseHeader(lastHeader, valueSoFar, req);
                            lastHeader = match.Groups[1].Value;
                            valueSoFar = match.Groups[2].Value.Trim();
                        }
                    }
                    parseHeader(lastHeader, valueSoFar, req);
                }
                catch (InvalidRequestException e)
                {
                    return e.Response;
                }

                _sw.Log("HandleRequestAfterHeaders() - Parse headers");

                if (req.Handler == null)
                    return HttpResponse.Error(HttpStatusCode._404_NotFound, connectionClose: true);

                if (req.Method == HttpMethod.Post)
                {
                    // This returns null in case of success and an error response in case of error
                    var result = processPostContent(_socket, req);
                    if (result != null)
                        return result;
                }

                _sw.Log("HandleRequestAfterHeaders() - Stuff before Req.Handler()");

                if (_server.Options.ReturnExceptionsToClient)
                {
                    try
                    {
                        HttpResponse resp = req.Handler(req);
                        _sw.Log("HandleRequestAfterHeaders() - Req.Handler()");
                        return resp;
                    }
                    catch (InvalidRequestException e)
                    {
                        _sw.Log("HandleRequestAfterHeaders() - InvalidRequestException()");
                        return e.Response;
                    }
                    catch (Exception e)
                    {
                        HttpResponse resp = HttpResponse.Exception(e);
                        _sw.Log("HandleRequestAfterHeaders() - ExceptionResponse()");
                        return resp;
                    }
                }
                else
                {
                    try
                    {
                        HttpResponse resp = req.Handler(req);
                        _sw.Log("HandleRequestAfterHeaders() - Req.Handler()");
                        return resp;
                    }
                    catch (InvalidRequestException e)
                    {
                        _sw.Log("HandleRequestAfterHeaders() - InvalidRequestException()");
                        return e.Response;
                    }
                }
            }

            private void parseHeader(string headerName, string headerValue, HttpRequest req)
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
                    string host = req.Headers.Host;
                    int port = 80;
                    if (host.Contains(":"))
                    {
                        int pos = host.IndexOf(":");
                        if (!int.TryParse(host.Substring(pos + 1), out port))
                            port = 80;
                        host = host.Remove(pos);
                    }
                    host = host.TrimEnd('.');

                    string url = req.Url.Contains('?') ? req.Url.Remove(req.Url.IndexOf('?')) : req.Url;

                    lock (_server.RequestHandlerHooks)
                    {
                        var hook = _server.RequestHandlerHooks.FirstOrDefault(hk => (hk.Port == null || hk.Port.Value == port) &&
                                (hk.Domain == null || hk.Domain == host || (!hk.SpecificDomain && host.EndsWith("." + hk.Domain))) &&
                                (hk.Path == null || hk.Path == url || (!hk.SpecificPath && url.StartsWith(hk.Path + "/"))));
                        if (hook == null)
                            throw new InvalidRequestException(HttpResponse.Error(HttpStatusCode._404_NotFound, connectionClose: true));

                        req.Handler = hook.Handler;
                        req.BaseUrl = hook.Path == null ? "" : hook.Path;
                        req.RestUrl = hook.Path == null ? req.Url : req.Url.Substring(hook.Path.Length);
                        req.Domain = host;
                        req.BaseDomain = hook.Domain == null ? "" : hook.Domain;
                        req.RestDomain = hook.Domain == null ? host : host.Remove(host.Length - hook.Domain.Length);
                        req.Port = port;
                    }
                }
                else if (nameLower == "expect")
                {
                    foreach (var kvp in req.Headers.Expect)
                        if (kvp.Key != "100-continue")
                            throw new InvalidRequestException(HttpResponse.Error(HttpStatusCode._417_ExpectationFailed, connectionClose: true));
                }
            }

            private HttpResponse processPostContent(Socket socket, HttpRequest req)
            {
                // Some validity checks
                if (req.Headers.ContentLength == null)
                    return HttpResponse.Error(HttpStatusCode._411_LengthRequired, connectionClose: true);
                if (req.Headers.ContentLength.Value > _server.Options.MaxSizePostContent)
                    return HttpResponse.Error(HttpStatusCode._413_RequestEntityTooLarge, connectionClose: true);
                if (req.Headers.ContentType == null)
                    return HttpResponse.Error(HttpStatusCode._501_NotImplemented, @"""Content-Type"" must be specified. Moreover, only ""application/x-www-form-urlencoded"" and ""multipart/form-data"" are supported.", connectionClose: true);

                // If "Expect: 100-continue" was specified, send a 100 Continue here
                if (req.Headers.Expect != null && req.Headers.Expect.ContainsKey("100-continue"))
                    socket.Send("HTTP/1.1 100 Continue\r\n\r\n".ToUtf8());

                // Read the contents of the POST request
                Stream contentStream;
                if (_bufferDataLength >= req.Headers.ContentLength.Value)
                {
                    contentStream = new MemoryStream(_buffer, _bufferDataOffset, (int) req.Headers.ContentLength.Value, false);
                    _bufferDataOffset += (int) req.Headers.ContentLength.Value;
                    _bufferDataLength -= (int) req.Headers.ContentLength.Value;
                }
                else
                {
                    contentStream = new SocketReaderStream(socket, req.Headers.ContentLength.Value, _buffer, _bufferDataOffset, _bufferDataLength);
                    _bufferDataOffset = 0;
                    _bufferDataLength = 0;
                }
                try
                {
                    req.ParsePostBody(contentStream, _server.Options.TempDir, _server.Options.StoreFileUploadInFileAtSize);
                }
                catch (SocketException) { }
                catch (EndOfStreamException) { }
                catch (IOException) { }

                // null means: no error
                return null;
            }
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
            Stats.AddRequestReceived();
            if (_opt.IdleTimeout != 0)
                incomingConnection.ReceiveTimeout = _opt.IdleTimeout;
            var readingRunner = new readingThreadRunner(incomingConnection, this, Log);
            lock (_activeReadingThreads)
                _activeReadingThreads.Add(readingRunner);
            readingRunner.Begin(); // the runner will remove itself from active threads when it's done
        }

        /// <summary>Keeps track of and exposes getters for various server performance statistics.</summary>
        public sealed class Statistics
        {
            private HttpServer _server;

            /// <summary>Constructor.</summary>
            public Statistics(HttpServer server) { _server = server; }

            /// <summary>Gets the number of connections which are currently alive, that is receiving data, waiting to receive data, or sending a response.</summary>
            public int ActiveHandlers { get { lock (_server._activeReadingThreads) { return _server._activeReadingThreads.Count; } } }

            /// <summary>Gets the number of request processing threads which have completed a request but are being kept alive.</summary>
            public int KeepAliveHandlers { get { lock (_server._activeReadingThreads) { return _server._activeReadingThreads.Where(r => r.KeepAliveActive).Count(); } } }

            private long _totalRequestsReceived = 0;
            /// <summary>Gets the total number of requests received by the server.</summary>
            public long TotalRequestsReceived { get { return Interlocked.Read(ref _totalRequestsReceived); } }
            /// <summary>Used internally to count a received request.</summary>
            public void AddRequestReceived() { Interlocked.Increment(ref _totalRequestsReceived); }

            // bytes sent/received
            // max open connections
            // request serve time stats
        }
    }
}
