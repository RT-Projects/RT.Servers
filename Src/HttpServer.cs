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
    public class HttpServer
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
        /// Gets a value indicating whether the server is currently running (listening).
        /// </summary>
        public bool IsListening { get; private set; }

        /// <summary>
        /// Wait on this event after starting the server to be notified of when the server has fully shut down. This event is
        /// initially un-signalled; starting the server resets it, stopping the server sets it as soon as the last active connection
        /// is terminated. Starting the server again before the previous shutdown is complete will result in this event not
        /// being raised at all for the previous shutdown.
        /// </summary>
        public readonly ManualResetEvent ShutdownComplete = new ManualResetEvent(false);

        private Socket _listeningSocket;
        private HttpServerOptions _opt;
        private HashSet<connectionHandler> _activeConnectionHandlers = new HashSet<connectionHandler>();

        /// <summary>Specifies the HTTP request handler for this server.</summary>
        public Func<HttpRequest, HttpResponse> Handler { get; set; }

        /// <summary>
        /// Shuts the HTTP server down.
        /// </summary>
        /// <param name="brutal">If true, currently executing requests will have their connections brutally closed. The server will be
        /// fully shut down when the method returns. If false, all idle keepalive connections will be closed but active connections will
        /// be allowed to end normally. In this case, use <see cref="ShutdownComplete"/> to wait until all connections are closed.</param>
        /// <param name="blocking">If true, will only return once all connections are closed. This might take a while unless the
        /// <paramref name="brutal"/> option is true. Setting this to true is the same as waiting for <see cref="ShutdownComplete"/> indefinitely.</param>
        public void StopListening(bool brutal = false, bool blocking = false)
        {
            IsListening = false;

            _listeningSocket.Close();
            _listeningSocket = null;

            lock (_activeConnectionHandlers)
            {
                foreach (var conn in _activeConnectionHandlers.ToArray())
                {
                    conn.DisallowKeepAlive = true;
                    if (brutal || conn.KeepAliveActive)
                    {
                        try { conn.Socket.Close(); }
                        catch (SocketException) { } // the socket may have been closed but not yet removed from active handlers
                        _activeConnectionHandlers.Remove(conn);
                    }
                }
                if (_activeConnectionHandlers.Count == 0)
                    ShutdownComplete.Set();
            }
        }

        /// <summary>
        /// Runs the HTTP server.
        /// </summary>
        /// <param name="blocking">Normally the method will return as soon as the listening socket is open. If this parameter is
        /// set to true, however, this method will block and only return once the server is fully shut down by a call to <see cref="StopListening"/>.
        /// This is equivalent to waiting for <see cref="ShutdownComplete"/> indefinitely.</param>
        public void StartListening(bool blocking = false)
        {
            if (IsListening)
                return;
            IsListening = true;
            ShutdownComplete.Reset();

            IPAddress addr;
            if (_opt.BindAddress == null || !IPAddress.TryParse(_opt.BindAddress, out addr))
                addr = IPAddress.Any;
            var ep = new IPEndPoint(addr, _opt.Port);

            _listeningSocket = new Socket(ep.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            _listeningSocket.Bind(ep);

            try
            {
                _listeningSocket.Listen(int.MaxValue);
            }
            catch (SocketException)
            {
                _listeningSocket.Close();
                throw;
            }

            // BeginAccept might complete synchronously as per MSDN, so call it on a pool thread
            ThreadPool.QueueUserWorkItem(delegate { _listeningSocket.BeginAccept(acceptSocket, null); });

            if (blocking)
                ShutdownComplete.WaitOne();
        }

        private void acceptSocket(IAsyncResult result)
        {
            // Ensure that this callback is really due to a new connection (might be due to listening socket closure)
            if (!IsListening)
                return;

            // Get the socket
            Socket socket = null;
            try { socket = _listeningSocket.EndAccept(result); }
            catch (SocketException) { } // can happen if the remote party has closed the socket while it was waiting for us to accept

            // Schedule the next socket accept
            _listeningSocket.BeginAccept(acceptSocket, null);

            // Handle this connection
            if (socket != null)
                HandleConnection(socket);
        }

        /// <summary>
        /// Handles an incoming connection. This function can be used to let the server handle a TCP connection
        /// that was received by some other component outside the HttpServer class. This function may or may
        /// not return immediately; some requests may, theoretically, be handled completely synchronously if all
        /// the data has already been received and buffered by the OS.
        /// </summary>
        /// <param name="incomingConnection">The incoming connection to process.</param>
        public void HandleConnection(Socket incomingConnection)
        {
            Stats.AddConnectionReceived();
            if (_opt.IdleTimeout != 0)
                incomingConnection.ReceiveTimeout = _opt.IdleTimeout;
            // The reader will add itself to the active connections, process the current connection, and remove from active connections when done
            new connectionHandler(incomingConnection, this);
        }

        private sealed class connectionHandler
        {
            public Socket Socket;
            public bool DisallowKeepAlive = false;
            public bool KeepAliveActive = false;

            private Stopwatch _sw;
            private byte[] _buffer;
            private int _bufferDataOffset;
            private int _bufferDataLength;
            private string _headersSoFar;
            private HttpServer _server;
            private int _begunReceives = 0;
            private int _endedReceives = 0;
            private Func<HttpRequest, HttpResponse> _handler;

            public connectionHandler(Socket socket, HttpServer server)
            {
                Socket = socket;
                _server = server;
                _handler = _server.Handler ?? (req => HttpResponse.Error(HttpStatusCode._404_NotFound, headers: new HttpResponseHeaders { Connection = HttpConnection.Close }));

                _sw = new StopwatchDummy();
                _sw.Log("ctor() - Start readingThreadRunner");

                _buffer = new byte[1024];
                _bufferDataOffset = 0;
                _bufferDataLength = 0;

                _headersSoFar = "";
                _sw.Log("ctor() - end");

                lock (server._activeConnectionHandlers)
                    server._activeConnectionHandlers.Add(this);

                receiveMoreHeaderData();
            }

            /// <summary>
            /// Initiates the process of receiving more header data. Invoked whenever the header buffer is empty and we haven’t yet
            /// received all the headers belonging to the current request.
            /// </summary>
            private void receiveMoreHeaderData()
            {
                _bufferDataOffset = 0;

                // Try reading some data synchronously
                try
                {
                    int available = Socket.Available;
                    if (available > 0)
                        _bufferDataLength = Socket.Receive(_buffer, Math.Min(available, _buffer.Length), SocketFlags.None);
                }
                catch (SocketException)
                {
                    Socket.Close();
                    cleanupIfDone();
                    return;
                }

                if (_bufferDataLength > 0)
                {
                    // Got some data synchronously, so process it
                    processHeaderData();
                }
                else
                {
                    // Couldn’t read synchronously, so begin an async read that waits for the data to arrive
                    try
                    {
                        Socket.BeginReceive(_buffer, 0, _buffer.Length, SocketFlags.None, moreHeaderDataReceived, null);
                        Interlocked.Increment(ref _begunReceives);
                    }
                    catch (SocketException)
                    {
                        Socket.Close();
                    }
                }

                cleanupIfDone();
            }

            /// <summary>
            /// Completes the process of receiving more header data by passing it on to <see cref="processHeaderData"/> for processing.
            /// </summary>
            private void moreHeaderDataReceived(IAsyncResult res)
            {
                KeepAliveActive = false;
                Interlocked.Increment(ref _endedReceives);

                try
                {
                    _bufferDataLength = Socket.EndReceive(res);
                }
                catch (SocketException)
                {
                    Socket.Close();
                    cleanupIfDone();
                    return;
                }

                if (_bufferDataLength == 0)
                    Socket.Close(); // remote end closed the connection and there are no more bytes to receive
                else
                    processHeaderData();
                cleanupIfDone();
            }

            /// <summary>
            /// Checks whether there are any outstanding async receives, and if not, cleans up / winds down this connection handler.
            /// </summary>
            private void cleanupIfDone()
            {
                if (Interlocked.CompareExchange(ref _begunReceives, 0, 0) <= Interlocked.CompareExchange(ref _endedReceives, 0, 0))
                {
                    // ... remove self from the active readers because this is the end of the callback chain
                    lock (_server._activeConnectionHandlers)
                    {
                        _server._activeConnectionHandlers.Remove(this);
                        if (!_server.IsListening && _server._activeConnectionHandlers.Count == 0)
                            _server.ShutdownComplete.Set();
                    }
                }
            }

            /// <summary>
            /// Starts or continues processing of any buffered header data. If none are buffered, will instead initiate the reception
            /// of more header data - a process which, when complete, will call this method to process whatever got received.
            /// </summary>
            private void processHeaderData()
            {
                _sw.Log("Start of processHeaderData()");

                // Request more header data if we have none
                if (_bufferDataLength == 0)
                {
                    receiveMoreHeaderData();
                    return;
                }

                // Stop soon if the headers become too large.
                if (_headersSoFar.Length + _bufferDataLength > _server.Options.MaxSizeHeaders)
                {
                    Socket.Close();
                    return;
                }

                // Keep receiving more headers until all the headers are received (and, possibly, non-header data that follows)
                int prevHeadersLength = _headersSoFar.Length;
                _sw.Log("Stuff before _headersSoFar += Encoding.UTF8.GetString(...)");
                _headersSoFar += Encoding.UTF8.GetString(_buffer, _bufferDataOffset, _bufferDataLength);
                _sw.Log("_headersSoFar += Encoding.UTF8.GetString(...)");
                int endOfHeadersIndex = _headersSoFar.IndexOf("\r\n\r\n");
                _sw.Log(@"_headersSoFar.Contains(""\r\n\r\n"")");
                if (endOfHeadersIndex < 0)
                {
                    _bufferDataLength = 0;
                    receiveMoreHeaderData();
                    return;
                }

                _sw.Log(@"int SepIndex = _headersSoFar.IndexOf(""\r\n\r\n"")");
                _headersSoFar = _headersSoFar.Remove(endOfHeadersIndex);
                _sw.Log(@"HeadersSoFar = _headersSoFar.Remove(SepIndex)");

                _bufferDataOffset += endOfHeadersIndex + 4 - prevHeadersLength;
                _bufferDataLength -= endOfHeadersIndex + 4 - prevHeadersLength;
                _sw.Log("Stuff before handleRequestAfterHeaders()");
                HttpRequest originalRequest;
                HttpResponse response = handleRequestAfterHeaders(out originalRequest);
                _sw.Log("Returned from handleRequestAfterHeaders()");
                bool connectionKeepAlive = false;
                try
                {
                    _sw.Log("Stuff before outputResponse()");
                    connectionKeepAlive = outputResponse(response, originalRequest);
                    _sw.Log("Returned from outputResponse()");
                }
                catch (SocketException e)
                {
                    _sw.Log("Caught SocketException - closing ({0})".Fmt(e.Message));
                    Socket.Close();
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

                // Reuse connection if allowed; close it otherwise
                if (connectionKeepAlive && Socket.Connected && !DisallowKeepAlive)
                {
                    _headersSoFar = "";
                    _sw.Log("Reusing connection");
                    KeepAliveActive = true;
                    processHeaderData();
                }
                else
                {
                    _sw.Log("Stuff before Socket.Close()");
                    Socket.Close();
                    _sw.Log("Socket.Close()");
                }
            }

            private bool outputResponse(HttpResponse response, HttpRequest originalRequest)
            {
                Socket.NoDelay = false;
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
                            if (response.Content.CanSeek)
                            {
                                contentLength = response.Content.Length;
                                contentLengthKnown = true;
                            }
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
                        Socket.Send(resultBuffer);
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
                        SocketWriterStream str = new SocketWriterStream(Socket);
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
                        SocketWriterStream str = new ChunkedSocketWriterStream(Socket);
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
                        output = new ChunkedSocketWriterStream(Socket);
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

                        output = new SocketWriterStream(Socket);
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
                                Socket.NoDelay = true;
                        }
                        catch { }

                        output.Write(buffer, 0, bytesRead);

                        // If we are actually gzipping and chunking something of known length, we don’t want several TCP packets
                        // of just a few bytes from when the gzip and chunked stream finish. The chunked stream already sets this
                        // back to true just before outputting the real final bit.
                        Socket.NoDelay = false;

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
                Socket.Send(Encoding.UTF8.GetBytes(headersStr));
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
                    Socket.Send(buffer, 0, bytesRead, SocketFlags.None);
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
                    Socket.Send(new byte[] { (byte) '-', (byte) '-' });
                    Socket.Send(boundary);
                    Socket.Send(("\r\nContent-Range: bytes " + r.Key.ToString() + "-" + r.Value.ToString() + "/" + totalFileSize.ToString() + "\r\n\r\n").ToUtf8());

                    response.Content.Seek(r.Key, SeekOrigin.Begin);
                    long bytesMissing = r.Value - r.Key + 1;
                    int bytesRead = response.Content.Read(buffer, 0, (int) Math.Min(65536, bytesMissing));
                    while (bytesRead > 0)
                    {
                        Socket.Send(buffer, 0, bytesRead, SocketFlags.None);
                        bytesMissing -= bytesRead;
                        bytesRead = (bytesMissing > 0) ? response.Content.Read(buffer, 0, (int) Math.Min(65536, bytesMissing)) : 0;
                    }
                    Socket.Send(new byte[] { 13, 10 });
                }
                Socket.Send(new byte[] { (byte) '-', (byte) '-' });
                Socket.Send(boundary);
                Socket.Send(new byte[] { (byte) '-', (byte) '-', 13, 10 });
            }

            private HttpResponse handleRequestAfterHeaders(out HttpRequest req)
            {
                _sw.Log("HandleRequestAfterHeaders() - enter");

                string[] lines = _headersSoFar.Split(new string[] { "\r\n" }, StringSplitOptions.None);
                req = new HttpRequest() { OriginIP = Socket.RemoteEndPoint as IPEndPoint };
                if (lines.Length < 2)
                    return HttpResponse.Error(HttpStatusCode._400_BadRequest, headers: new HttpResponseHeaders { Connection = HttpConnection.Close });

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
                    return HttpResponse.Error(HttpStatusCode._501_NotImplemented, headers: new HttpResponseHeaders { Connection = HttpConnection.Close });

                if (line.EndsWith(" HTTP/1.0"))
                    req.HttpVersion = HttpProtocolVersion.Http10;
                else if (line.EndsWith(" HTTP/1.1"))
                    req.HttpVersion = HttpProtocolVersion.Http11;
                else
                    return HttpResponse.Error(HttpStatusCode._505_HttpVersionNotSupported, headers: new HttpResponseHeaders { Connection = HttpConnection.Close });

                int start = req.Method == HttpMethod.Get ? 4 : 5;
                req.Url = line.Substring(start, line.Length - start - 9);
                if (req.Url.Contains(' '))
                    return HttpResponse.Error(HttpStatusCode._400_BadRequest, headers: new HttpResponseHeaders { Connection = HttpConnection.Close });

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
                                return HttpResponse.Error(HttpStatusCode._400_BadRequest, headers: new HttpResponseHeaders { Connection = HttpConnection.Close });
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

                if (req.Headers.Host == null)
                    return HttpResponse.Error(HttpStatusCode._400_BadRequest, headers: new HttpResponseHeaders { Connection = HttpConnection.Close });
                var colonPos = req.Headers.Host.IndexOf(':');
                if (colonPos != -1)
                {
                    req.Domain = req.Headers.Host.Substring(0, colonPos);
                    int port;
                    if (!int.TryParse(req.Headers.Host.Substring(colonPos + 1), out port))
                        return HttpResponse.Error(HttpStatusCode._400_BadRequest, headers: new HttpResponseHeaders { Connection = HttpConnection.Close });
                    req.Port = port;
                }
                else
                {
                    req.Domain = req.Headers.Host;
                    req.Port = 80;
                }

                _sw.Log("HandleRequestAfterHeaders() - Parse headers");

                if (req.Method == HttpMethod.Post)
                {
                    // This returns null in case of success and an error response in case of error
                    var result = processPostContent(Socket, req);
                    if (result != null)
                        return result;
                }

                _sw.Log("HandleRequestAfterHeaders() - Stuff before Req.Handler()");

                if (_server.Options.ReturnExceptionsToClient)
                {
                    try
                    {
                        HttpResponse resp = _handler(req);
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
                        HttpResponse resp = _handler(req);
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
                if (nameLower == "expect")
                {
                    foreach (var kvp in req.Headers.Expect)
                        if (kvp.Key != "100-continue")
                            throw new InvalidRequestException(HttpResponse.Error(HttpStatusCode._417_ExpectationFailed, headers: new HttpResponseHeaders { Connection = HttpConnection.Close }));
                }
            }

            private HttpResponse processPostContent(Socket socket, HttpRequest req)
            {
                // Some validity checks
                if (req.Headers.ContentLength == null)
                    return HttpResponse.Error(HttpStatusCode._411_LengthRequired, headers: new HttpResponseHeaders { Connection = HttpConnection.Close });
                if (req.Headers.ContentLength.Value > _server.Options.MaxSizePostContent)
                    return HttpResponse.Error(HttpStatusCode._413_RequestEntityTooLarge, headers: new HttpResponseHeaders { Connection = HttpConnection.Close });
                if (req.Headers.ContentType == null)
                {
                    if (req.Headers.ContentLength != 0)
                        return HttpResponse.Error(HttpStatusCode._400_BadRequest, @"""Content-Type"" must be specified. Moreover, only ""application/x-www-form-urlencoded"" and ""multipart/form-data"" are supported.", headers: new HttpResponseHeaders { Connection = HttpConnection.Close });
                    // Tolerate empty bodies without Content-Type (seems that jQuery generates those)
                    req.Headers.ContentType = HttpPostContentType.ApplicationXWwwFormUrlEncoded;
                }

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

        /// <summary>Keeps track of and exposes getters for various server performance statistics.</summary>
        public sealed class Statistics
        {
            private HttpServer _server;

            /// <summary>Constructor.</summary>
            public Statistics(HttpServer server) { _server = server; }

            /// <summary>Gets the number of connections which are currently alive, that is receiving data, waiting to receive data, or sending a response.</summary>
            public int ActiveHandlers { get { lock (_server._activeConnectionHandlers) { return _server._activeConnectionHandlers.Count; } } }

            /// <summary>Gets the number of request processing threads which have completed a request but are being kept alive.</summary>
            public int KeepAliveHandlers { get { lock (_server._activeConnectionHandlers) { return _server._activeConnectionHandlers.Where(r => r.KeepAliveActive).Count(); } } }

            private long _totalConnectionsReceived = 0;
            /// <summary>Gets the total number of connections received by the server.</summary>
            public long TotalConnectionsReceived { get { return Interlocked.Read(ref _totalConnectionsReceived); } }
            /// <summary>Used internally to count a received connection.</summary>
            internal void AddConnectionReceived() { Interlocked.Increment(ref _totalConnectionsReceived); }

            // requests received
            // bytes sent/received
            // max open connections
            // request serve time stats
        }
    }
}
