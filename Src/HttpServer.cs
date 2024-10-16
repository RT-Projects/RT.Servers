﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using RT.Servers.SharpZipLib.GZip;
using RT.Util;
using RT.Util.ExtensionMethods;
using RT.Util.Streams;

[assembly: InternalsVisibleTo("RT.Servers.Tests")]

namespace RT.Servers
{
    /// <summary>Provides an HTTP server.</summary>
    public class HttpServer
    {
        /// <summary>
        ///     Constructs an HTTP server with the specified configuration settings.</summary>
        /// <param name="options">
        ///     Specifies the configuration settings to use for this <see cref="HttpServer"/>, or null to set all
        ///     configuration values to default values.</param>
        public HttpServer(HttpServerOptions options = null)
        {
            _opt = options ?? new HttpServerOptions();
            Stats = new Statistics(this);
        }

        /// <summary>
        ///     Constructs an HTTP server with the specified configuration settings and using the specified port number for
        ///     unsecured HTTP traffic.</summary>
        /// <param name="port">
        ///     The port number to use.</param>
        /// <param name="options">
        ///     Specifies the configuration settings to use for this <see cref="HttpServer"/>, or null to set all
        ///     configuration values to default values.</param>
        public HttpServer(int port, HttpServerOptions options = null)
        {
            _opt = options ?? new HttpServerOptions();
            _opt.Endpoints.Clear();
            _opt.AddEndpoint("*", null, port);
            Stats = new Statistics(this);
        }

        /// <summary>
        ///     Gets or sets the configuration settings currently in effect for this server.</summary>
        /// <remarks>
        ///     When set while the server is running, most of the new settings take effect immediately. The following do not:
        ///     <see cref="HttpServerOptions.Endpoints"/>, <see cref="HttpServerOptions.CertificatePath"/>.</remarks>
        public HttpServerOptions Options
        {
            get
            {
                return _opt;
            }
            set
            {
                if (value == null)
                    throw new ArgumentNullException("value");
                value.CheckValid();
                _opt = value;
            }
        }

        /// <summary>Gets an object containing various server performance statistics.</summary>
        public Statistics Stats { get; private set; }

        /// <summary>
        ///     Gets or sets a logger to log all HTTP requests to.</summary>
        /// <remarks>
        ///     <para>
        ///         Do not modify properties of the logger while the server is running as doing so is not thread-safe.
        ///         Reassigning a new logger, however, should be safe (as assignment is atomic).</para></remarks>
        public LoggerBase Log
        {
            get { return _log; }
            set
            {
                _log = value ?? throw new ArgumentNullException("value");
            }
        }
        private LoggerBase _log = new NullLogger();

        /// <summary>Gets a value indicating whether the server is currently running (listening).</summary>
        public bool IsListening { get; private set; }

        /// <summary>
        ///     Wait on this event after starting the server to be notified of when the server has fully shut down. This event
        ///     is initially un-signalled; starting the server resets it, stopping the server sets it as soon as the last
        ///     active connection is terminated. Starting the server again before the previous shutdown is complete will
        ///     result in this event not being raised at all for the previous shutdown.</summary>
        public readonly ManualResetEvent ShutdownComplete = new(false);

        private readonly Dictionary<string, HttpEndpoint> _listeningSockets = [];
        private readonly HashSet<ConnectionHandler> _activeConnectionHandlers = [];
        private HttpServerOptions _opt;

        /// <summary>
        ///     Specifies the HTTP request handler for this server.</summary>
        /// <remarks>
        ///     Returning null from this handler is a bug, and will cause a generic 500 Internal Server Error. All exceptions
        ///     leaving this handler will be handled by the server, unless <see
        ///     cref="HttpServerOptions.OutputExceptionInformation"/> is configured to do otherwise. All exceptions are passed
        ///     to the <see cref="ErrorHandler"/>, which may return an arbitrary response as a result. See Remarks on <see
        ///     cref="ErrorHandler"/> for further information.</remarks>
        public Func<HttpRequest, HttpResponse> Handler { get; set; }

        /// <summary>
        ///     Specifies a request handler that is invoked whenever <see cref="Handler"/> throws an exception.</summary>
        /// <remarks>
        ///     If <c>null</c>, a default handler will be used. This default handler is also used if the error handler returns
        ///     null or throws an exception. The default error handler will use HTTP status 500 except if the <see
        ///     cref="Handler"/> threw an <see cref="HttpException"/>, in which case the exception's HTTP status is used
        ///     instead.</remarks>
        public Func<HttpRequest, Exception, HttpResponse> ErrorHandler { get; set; }

        /// <summary>
        ///     Specifies a method to be invoked whenever an exception occurs while reading from the response stream.</summary>
        /// <remarks>
        ///     Regardless of what this method does, the server will close the socket, cutting off the incomplete response.</remarks>
        public Action<HttpRequest, Exception, HttpResponse> ResponseExceptionHandler { get; set; }

        /// <summary>
        ///     Determines whether exceptions in <see cref="Handler"/>, <see cref="ErrorHandler"/> and the response stream get
        ///     propagated to the debugger. Setting this to <c>true</c> will cause exceptions to bring down the server.</summary>
        /// <remarks>
        ///     <para>
        ///         If <c>false</c>, all exceptions are handled. <see cref="HttpException"/> determines its own HTTP response
        ///         status code, all other exception types lead to 500 Internal Server Error. Use this setting in RELEASE
        ///         mode.</para>
        ///     <para>
        ///         If <c>true</c>, only <see cref="HttpException"/> is handled. All other exceptions are left unhandled so
        ///         that the Visual Studio debugger is triggered when they occur, enabling debugging. Use this setting in
        ///         DEBUG mode only.</para></remarks>
        public bool PropagateExceptions { get; set; }

        /// <summary>
        ///     Shuts the HTTP server down.</summary>
        /// <param name="brutal">
        ///     If true, currently executing requests will have their connections brutally closed. The server will be fully
        ///     shut down when the method returns. If false, all idle keepalive connections will be closed but active
        ///     connections will be allowed to end normally. In this case, use <see cref="ShutdownComplete"/> to wait until
        ///     all connections are closed.</param>
        /// <param name="blocking">
        ///     If true, will only return once all connections are closed. This might take a while unless the <paramref
        ///     name="brutal"/> option is true. Setting this to true is the same as waiting for <see cref="ShutdownComplete"/>
        ///     indefinitely.</param>
        public void StopListening(bool brutal = false, bool blocking = false)
        {
            IsListening = false;

            foreach (var socketkvp in _listeningSockets.ToArray())
            {
                if (socketkvp.Value != null && socketkvp.Value.Socket != null)
                {
                    socketkvp.Value.Socket.Close();
                    _listeningSockets.Remove(socketkvp.Key);
                }
            }

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

            if (blocking)
                ShutdownComplete.WaitOne();
        }

        /// <summary>
        ///     Runs the HTTP server.</summary>
        /// <param name="blocking">
        ///     Normally the method will return as soon as the listening socket is open. If this parameter is set to true,
        ///     however, this method will block and only return once the server is fully shut down by a call to <see
        ///     cref="StopListening"/>. This is equivalent to waiting for <see cref="ShutdownComplete"/> indefinitely.</param>
        public void StartListening(bool blocking = false)
        {
            _opt.CheckValid();

            if (IsListening)
                return;

            IsListening = true;
            ShutdownComplete.Reset();

            if (_opt.Endpoints.Count == 0)
                throw new InvalidOperationException("No endpoints are configured.");
            var dupes = _opt.Endpoints.Values.GroupBy(v => (v.BindAddress ?? "*") + ":" + v.Port).Where(g => g.Count() > 1);
            if (dupes.Any())
                throw new InvalidOperationException("Duplicate endpoint(s): " + dupes.Select(d => d.First().ToString()).JoinString(", "));

            foreach (var endpointKvp in _opt.Endpoints)
            {
                if (!IPAddress.TryParse(endpointKvp.Value.BindAddress, out var addr))
                    addr = IPAddress.Any;

                var ep = new IPEndPoint(addr, endpointKvp.Value.Port);
                var socket = new Socket(ep.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                endpointKvp.Value.Socket = socket;
                _listeningSockets.Add(endpointKvp.Key, endpointKvp.Value);
                socket.Bind(ep);

                try
                {
                    socket.Listen(int.MaxValue);
                }
                catch (SocketException)
                {
                    socket.Close();
                    throw;
                }

                // BeginAccept might complete synchronously as per MSDN, so call it on a pool thread
                ThreadPool.QueueUserWorkItem(delegate
                {
                    // If someone calls StopListening() immediately after StartListening(),
                    // this could be null or a disposed object, and we really don’t care about that.
                    try { socket.BeginAccept(r => acceptSocket(r, endpointKvp.Key), null); }
                    catch { }
                });
            }

            if (blocking)
                ShutdownComplete.WaitOne();
        }

        private void acceptSocket(IAsyncResult result, string key)
        {
#if DEBUG
            // Workaround for bug in .NET 4.0 and 4.5:
            // https://connect.microsoft.com/VisualStudio/feedback/details/535917
            new Thread(() =>
#endif
            {
                // Ensure that this callback is really due to a new connection (might be due to listening socket closure)
                if (!IsListening)
                    return;

                // Get the socket
                Socket socket = null;
                try { socket = _listeningSockets[key].Socket.EndAccept(result); }
                catch (SocketException) { } // can happen if the remote party has closed the socket while it was waiting for us to accept
                catch (ObjectDisposedException) { }
                catch (NullReferenceException) { if (_listeningSockets[key] != null) throw; } // can happen if StopListening is called at precisely the "wrong" time

                // Schedule the next socket accept
                if (_listeningSockets[key] != null)
                    try { _listeningSockets[key].Socket.BeginAccept(r => acceptSocket(r, key), null); }
                    catch (NullReferenceException) { if (_listeningSockets[key] != null) throw; } // can happen if StopListening is called at precisely the "wrong" time

                // Handle this connection
                if (socket == null)
                    return;

                if (PropagateExceptions)
                    HandleConnection(socket, _listeningSockets[key].Secure);
                else
                {
                    try
                    {
                        HandleConnection(socket, _listeningSockets[key].Secure);
                    }
                    catch (Exception e)
                    {
                        try { socket.Close(); }
                        catch { }
                        try
                        {
                            _log.Error("{0} ({1})".Fmt(e.Message, e.GetType().FullName));
                            _log.Error(e.StackTrace);
                        }
                        catch { }
                    }
                }
            }
#if DEBUG
).Start();
#endif
        }

        /// <summary>
        ///     Handles an incoming connection. This function can be used to let the server handle a TCP connection that was
        ///     received by some other component outside the HttpServer class. This function may or may not return
        ///     immediately; some requests may, theoretically, be handled completely synchronously if all the data has already
        ///     been received and buffered by the OS.</summary>
        /// <param name="incomingConnection">
        ///     The incoming connection to process.</param>
        /// <param name="secure">
        ///     True to use SSL, false otherwise.</param>
        public void HandleConnection(Socket incomingConnection, bool secure)
        {
            Stats.AddConnectionReceived();
            if (_opt.IdleTimeout != 0)
                incomingConnection.ReceiveTimeout = _opt.IdleTimeout;
            // The reader will add itself to the active connections, process the current connection, and remove from active connections when done
            new ConnectionHandler(incomingConnection, this, secure);
        }

        private HttpResponse defaultErrorHandler(Exception exception, Exception exInErrorHandler = null)
        {
            var statusCode = HttpStatusCode._500_InternalServerError;
            if (exception is HttpException exc)
                statusCode = exc.StatusCode;

            var statusCodeNameHtml = (((int) statusCode) + " " + statusCode.ToText()).HtmlEscape();
            var contentHtml = "<!DOCTYPE html>"
                + "<head><title>HTTP " + statusCodeNameHtml + "</title>"
                + "<body><h1>" + statusCodeNameHtml + "</h1>";

            if (exception is HttpException)
            {
                var userMessage = (exception as HttpException).UserMessage;
                if (!string.IsNullOrWhiteSpace(userMessage))
                    contentHtml += "<p>" + userMessage.HtmlEscape() + "</p>";
            }

            if (Options.OutputExceptionInformation)
                contentHtml = contentHtml
                    + "<hr>"
                    + (exInErrorHandler == null
                        ? "<h1>Exception in request handler</h1>" + exceptionToHtml(exception)
                        : "<h1>Exception in error handler</h1>" + exceptionToHtml(exInErrorHandler) + "<h1>while handling exception in request handler</h1>" + exceptionToHtml(exception));

            return HttpResponse.Html(contentHtml, statusCode);
        }

        private static string exceptionToHtml(Exception exception)
        {
            bool first = true;
            string exceptionHtml = "";
            while (exception != null)
            {
                var exc = "<h3>" + exception.GetType().FullName.HtmlEscape() + "</h3>";
                if (!string.IsNullOrWhiteSpace(exception.Message))
                    exc += "<p>" + exception.Message.HtmlEscape() + "</p>";
                exc += "<pre>" + exception.StackTrace.NullOr(st => st.HtmlEscape()) + "</pre>";
                exc += first ? "" : "<hr>";
                exceptionHtml = exc + exceptionHtml;
                exception = exception.InnerException;
                first = false;
            }
            return "<div class='exception'>" + exceptionHtml + "</div>";
        }

        private static string exceptionToPlaintext(Exception exception)
        {
            bool first = true;
            string exceptionText = "";
            while (exception != null)
            {
                var exc = exception.GetType().FullName + "\n\n";
                if (!string.IsNullOrWhiteSpace(exception.Message))
                    exc += exception.Message + "\n\n";
                exc += exception.StackTrace + "\n\n";
                exc += first ? "\n\n\n" : "\n----------------------------------------------------------------------\n";
                exceptionText = exc + exceptionText;
                exception = exception.InnerException;
                first = false;
            }
            return exceptionText;
        }

        private sealed class ConnectionHandler
        {
            public Socket Socket;
            public bool DisallowKeepAlive = false;
            public bool KeepAliveActive = false;

            private Stream _stream;
            private byte[] _buffer;
            private int _bufferDataOffset;
            private int _bufferDataLength;
            private string _headersSoFar;
            private HttpServer _server;
            private int _begunReceives = 0;
            private int _endedReceives = 0;
            private int _requestId;
            private DateTime _requestStart;
            private bool _secure;

            public ConnectionHandler(Socket socket, HttpServer server, bool secure)
            {
                if (server.PropagateExceptions)
                {
                    constructorTryBlock(socket, server, secure);
                    return;
                }

                try
                {
                    constructorTryBlock(socket, server, secure);
                }
                catch (Exception e)
                {
                    // Ideally, this catch clause should not be reached, but in practice, calls to Socket.Close() can cause
                    // unexpected SocketExceptions; some of the code can cause RemotingExceptions when the handler
                    // runs in another AppDomain; etc.
                    server.Log.Exception(e);

                    try { Socket.Close(); } catch { }
                }
            }

            private void constructorTryBlock(Socket socket, HttpServer server, bool secure)
            {
                Socket = socket;

                _requestId = Rnd.Next();
                _requestStart = DateTime.UtcNow;
                _server = server;
                _secure = secure;

                _server.Log.Info(4, "{0:X8} Incoming connection".Fmt(_requestId));

                _buffer = new byte[1024];
                _bufferDataOffset = 0;
                _bufferDataLength = 0;

                _headersSoFar = "";

                lock (server._activeConnectionHandlers)
                    server._activeConnectionHandlers.Add(this);

                _stream = new NetworkStream(socket, ownsSocket: true);
                if (_secure)
                {
                    var sniReader = new SniReaderStream(_stream);
                    var sniHost = sniReader.PeekAtSniHost();
                    var secureStream = new SslStream(sniReader);
                    _stream = secureStream;

                    secureStream.BeginAuthenticateAsServer(
                        // select the most appropriate certificate.
                        serverCertificate: server.Options.CertificateResolver?.Invoke(sniHost)
                            ?? server.Options.Certificates?.Get(sniHost, null)?.GetCertificate()
                            ?? server.Options.Certificate?.GetCertificate(),
                        enabledSslProtocols: SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12,
                        clientCertificateRequired: false,
                        checkCertificateRevocation: true,
                        asyncState: null,
                        asyncCallback: ar =>
                        {
                            if (_server.PropagateExceptions)
                            {
                                asyncCallbackTryBlock(ar, secureStream);
                                return;
                            }

                            try
                            {
                                asyncCallbackTryBlock(ar, secureStream);
                            }
                            catch (Exception e)
                            {
                                // Ideally, this catch clause should not be reached, but in practice, calls to Socket.Close() can cause
                                // unexpected SocketExceptions; some of the code can cause RemotingExceptions when the handler
                                // runs in another AppDomain; etc.
                                _server.Log.Exception(e);

                                try { Socket.Close(); } catch { }
                            }
                        });
                }
                else
                {
                    receiveMoreHeaderData();
                }
            }

            private void asyncCallbackTryBlock(IAsyncResult ar, SslStream secureStream)
            {
                try
                {
                    secureStream.EndAuthenticateAsServer(ar);
                }
                catch (IOException io) when (io.Message == "Authentication failed because the remote party has closed the transport stream.")
                {
                    // Very common exception that appears to be harmless
                    Socket.Close();
                    return;
                }
                catch (Exception e)
                {

                    Socket.Close();
                    _server.ResponseExceptionHandler?.Invoke(null, e, null);
                    return;
                }
                receiveMoreHeaderData();
            }

            /// <summary>
            ///     Initiates the process of receiving more header data. Invoked whenever the header buffer is empty and we
            ///     haven’t yet received all the headers belonging to the current request.</summary>
            private void receiveMoreHeaderData()
            {
                _bufferDataOffset = 0;

                // Try reading some data synchronously. Ideally we'd do it on the secure stream too, but because the Stream interface is
                // so limited, we can't determine whether the read will block, nor ask it to return immediately instead of blocking. Sigh...
                // See http://stackoverflow.com/questions/16550606/
                try
                {
                    if (_stream is NetworkStream stream && stream.DataAvailable)
                        _bufferDataLength = stream.Read(_buffer, 0, _buffer.Length);
                }
                catch (SocketException) { Socket.Close(); cleanupIfDone(); return; }
                catch (IOException) { Socket.Close(); cleanupIfDone(); return; }
                catch (ObjectDisposedException) { cleanupIfDone(); return; }

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
                        Interlocked.Increment(ref _begunReceives);
                        _stream.BeginRead(_buffer, 0, _buffer.Length, moreHeaderDataReceived, null);
                    }
                    catch (SocketException) { Socket.Close(); }
                    catch (IOException) { Socket.Close(); }
                    catch (ObjectDisposedException) { }
                }

                cleanupIfDone();
            }

            /// <summary>
            ///     Completes the process of receiving more header data by passing it on to <see cref="processHeaderData"/>
            ///     for processing.</summary>
            private void moreHeaderDataReceived(IAsyncResult res)
            {
                if (_server.PropagateExceptions)
                {
                    try
                    {
                        moreHeaderDataReceivedTryBlock(res);
                    }
                    finally
                    {
                        cleanupIfDone();
                    }
                    return;
                }

                try
                {
                    moreHeaderDataReceivedTryBlock(res);
                }
                catch (Exception e)
                {
                    // Ideally, this catch clause should not be reached, but in practice, calls to Socket.Close() can cause
                    // unexpected SocketExceptions; some of the code can cause RemotingExceptions when the handler
                    // runs in another AppDomain; etc.
                    _server.Log.Exception(e);

                    try { Socket.Close(); } catch { }
                }
                finally
                {
                    cleanupIfDone();
                }
            }

            private void moreHeaderDataReceivedTryBlock(IAsyncResult res)
            {
                KeepAliveActive = false;
                Interlocked.Increment(ref _endedReceives);

                try
                {
                    _bufferDataLength = Socket.Connected ? _stream.EndRead(res) : 0;
                }
                catch (SocketException) { Socket.Close(); cleanupIfDone(); return; }
                catch (IOException) { Socket.Close(); cleanupIfDone(); return; }
                catch (ObjectDisposedException) { cleanupIfDone(); return; }

                if (_bufferDataLength == 0)
                {
                    Socket.Close(); // remote end closed the connection and there are no more bytes to receive
                    cleanupIfDone();
                }
                else
                    processHeaderData();
            }

            /// <summary>
            ///     Checks whether there are any outstanding async receives, and if not, cleans up / winds down this
            ///     connection handler.</summary>
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
            ///     Starts or continues processing of any buffered header data. If none are buffered, will instead initiate
            ///     the reception of more header data - a process which, when complete, will call this method to process
            ///     whatever got received.</summary>
            private void processHeaderData()
            {
                start:;

                // Request more header data if we have none
                // (This only happens if we just finished a request and are in a keep-alive connection which has
                // already received some of the header data for the next request. In this case we must process
                // that data instead of waiting for more.)
                if (_bufferDataLength == 0)
                {
                    receiveMoreHeaderData();
                    return;
                }

                // Stop soon if the headers become too large.
                if (_headersSoFar.Length + _bufferDataLength > _server.Options.MaxSizeHeaders)
                {
                    Socket.Close();
                    cleanupIfDone();
                    return;
                }

                // Keep receiving more headers until all the headers are received (and, possibly, non-header data that follows)
                int prevHeadersLength = _headersSoFar.Length;
                _headersSoFar += Encoding.UTF8.GetString(_buffer, _bufferDataOffset, _bufferDataLength);
                int endOfHeadersIndex = _headersSoFar.IndexOf("\r\n\r\n");
                if (endOfHeadersIndex < 0)
                {
                    _bufferDataLength = 0;
                    receiveMoreHeaderData();
                    return;
                }

                _headersSoFar = _headersSoFar.Remove(endOfHeadersIndex);
                _bufferDataOffset += endOfHeadersIndex + 4 - prevHeadersLength;
                _bufferDataLength -= endOfHeadersIndex + 4 - prevHeadersLength;

                HttpRequest originalRequest = null;
                HttpResponse response = null;
                bool connectionKeepAlive = false;
                Stream contentStream = null;

                try
                {
                    try
                    {
                        if (_server.PropagateExceptions)
                        {
                            // Catch only *HTTP*Exception
                            try
                            {
                                response = handleRequestAfterHeaders(out originalRequest);
                                contentStream = response.GetContentStream();
                            }
                            catch (HttpException exInHandler)
                            {
                                response = exceptionToResponse(originalRequest, exInHandler);
                                contentStream = response.GetContentStream();
                            }
                        }
                        else
                        {
                            // Catch all exceptions
                            try
                            {
                                response = handleRequestAfterHeaders(out originalRequest);
                                contentStream = response.GetContentStream();
                            }
                            catch (Exception exInHandler)
                            {
                                response = exceptionToResponse(originalRequest, exInHandler);
                                contentStream = response.GetContentStream();
                            }
                        }
                    }
                    catch (SocketException) { Socket.Close(); return; }
                    catch (IOException) { Socket.Close(); return; }
                    catch (ObjectDisposedException) { return; }

                    var headers = response.Headers.Clone();
                    _server.Log.Info(2, "{0:X8} Handled: {1:000} {2}".Fmt(_requestId, (int) response.Status, headers.ContentType));

                    if (response is HttpResponseWebSocket responseWebsocket)
                    {
                        if (!originalRequest.Headers.Connection.HasFlag(HttpConnection.Upgrade) || !"websocket".EqualsIgnoreCase(originalRequest.Headers.Upgrade) || !originalRequest.Headers.TryGetValue("Sec-WebSocket-Key", out var webSocketKey))
                        {
                            response = HttpResponse.Html("<h1>Websocket expected</h1><p>The server expected a WebSocket upgrade request.</p>", HttpStatusCode._400_BadRequest);
                            goto notWebsocket;
                        }

                        WebSocket websocket;
                        try { websocket = responseWebsocket.Websocket; }
                        catch (Exception e)
                        {
                            response = exceptionToResponse(originalRequest, e);
                            goto notWebsocket;
                        }

                        headers.Connection = HttpConnection.Upgrade;
                        headers.Upgrade = "websocket";
                        headers.AdditionalHeaders ??= [];
                        headers.AdditionalHeaders.Add("Sec-WebSocket-Accept", Convert.ToBase64String(SHA1.Create().ComputeHash((webSocketKey + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11").ToUtf8())));
                        var subprotocol = responseWebsocket.Subprotocol;
                        if (subprotocol != null)
                            headers.AdditionalHeaders.Add("Sec-WebSocket-Protocol", subprotocol);
                        sendHeaders(HttpStatusCode._101_SwitchingProtocols, headers);

                        _server.Log.Info(3, "{0:X8} Switching to WebSocket: {1:0.##} ms".Fmt(_requestId, (DateTime.UtcNow - _requestStart).TotalMilliseconds));

                        // Hand the socket to the WebSocket implementation
                        try
                        {
                            websocket.takeSocket(new WebSocketServerSide(_stream, websocket, _server));
                        }
                        catch (Exception e)
                        {
                            _server.Log.Exception(e);
                        }

                        // Leave the socket open, but stop reading from it
                        return;
                    }

                    notWebsocket:
                    var responseContent = (HttpResponseContent) response;
                    try { connectionKeepAlive = outputResponse(responseContent, responseContent.Status, headers, contentStream, responseContent.UseGzip, originalRequest); }
                    catch (SocketException) { Socket.Close(); return; }
                    catch (IOException) { Socket.Close(); return; }
                    catch (ObjectDisposedException) { return; }
                    finally { contentStream?.Close(); }
                }
                finally
                {
                    if (originalRequest != null && originalRequest.CleanUpCallback != null)
                        originalRequest.CleanUpCallback();
                }

                _server.Log.Info(3, "{0:X8} Finished: {1:0.##} ms".Fmt(_requestId, (DateTime.UtcNow - _requestStart).TotalMilliseconds));

                // Reuse connection if allowed
                if (connectionKeepAlive && !DisallowKeepAlive)
                {
                    bool connected;
                    try { connected = Socket.Connected; }
                    catch (SocketException) { Socket.Close(); return; }
                    catch (ObjectDisposedException) { return; }

                    if (connected)
                    {
                        _headersSoFar = "";
                        KeepAliveActive = true;
                        _requestStart = DateTime.UtcNow;
                        goto start;
                    }
                    return;
                }

                // Otherwise, we are done with this socket
                Socket.Close();
            }

            private bool outputResponse(HttpResponse response, HttpStatusCode status, HttpResponseHeaders headers, Stream contentStream, UseGzipOption useGzipOption, HttpRequest originalRequest)
            {
                // IMPORTANT: Do not access properties of ‘response’. Use the other parameters instead, which contain copies.
                // The request handler can re-use HttpResponse objects between requests. Thus the properties of ‘response’ would
                // be accessed simultaneously from multiple instances of this method running in separate threads.
                // Additionally, HttpResponse is a MarshalByRefObject but HttpResponseHeaders is not, so ‘response’
                // may be a transparent proxy, in which case ‘response.Headers’ would retrieve a serialized copy.
                // ‘response’ is ONLY used to pass it to _server.ResponseExceptionHandler().

                Socket.NoDelay = false;

                try
                {
                    bool gzipRequested = false;
                    if (originalRequest.Headers.AcceptEncoding != null)
                        foreach (HttpContentEncoding hce in originalRequest.Headers.AcceptEncoding)
                            gzipRequested = gzipRequested || (hce == HttpContentEncoding.Gzip);
                    bool contentLengthKnown = false;
                    long contentLength = 0;

                    // Find out if we know the content length
                    if (contentStream == null)
                    {
                        contentLength = 0;
                        contentLengthKnown = true;
                    }
                    else if (headers.ContentLength != null)
                    {
                        contentLength = headers.ContentLength.Value;
                        contentLengthKnown = true;
                    }
                    else
                    {
                        // See if we can deduce the content length from the stream
                        try
                        {
                            if (contentStream.CanSeek)
                            {
                                contentLength = contentStream.Length;
                                contentLengthKnown = true;
                            }
                        }
                        catch (NotSupportedException) { }
                    }

                    bool useKeepAlive =
                        originalRequest.HttpVersion == HttpProtocolVersion.Http11 &&
                        originalRequest.Headers.Connection.HasFlag(HttpConnection.KeepAlive) &&
                        !headers.Connection.HasFlag(HttpConnection.Close);
                    headers.Connection = useKeepAlive ? HttpConnection.KeepAlive : HttpConnection.Close;
                    headers.ContentLength = null;

                    // Special cases: status codes that may not have a body
                    if (!status.MayHaveBody())
                    {
                        if (contentStream != null)
                            throw new InvalidOperationException("A response with the {0} status cannot have a body (GetContentStream must be null or return null).".Fmt(status));
                        if (headers.ContentType != null)
                            throw new InvalidOperationException("A response with the {0} status cannot have a Content-Type header.".Fmt(status));
                        sendHeaders(status, headers);
                        return useKeepAlive;
                    }

                    // Special case: empty body
                    if (contentLengthKnown && contentLength == 0)
                    {
                        headers.ContentLength = 0;
                        sendHeaders(status, headers);
                        return useKeepAlive;
                    }

                    // If no Content-Type is given, use default
                    headers.ContentType ??= _server.Options.DefaultContentType;

                    // If we know the content length and the stream can seek, then we can support Ranges - but it's not worth it for less than 16 KB
                    if (originalRequest.HttpVersion == HttpProtocolVersion.Http11 && contentLengthKnown && contentLength > 16 * 1024 && status == HttpStatusCode._200_OK && contentStream.CanSeek)
                    {
                        headers.AcceptRanges = HttpAcceptRanges.Bytes;

                        // If the client requested a range, then serve it
                        if (status == HttpStatusCode._200_OK && originalRequest.Headers.Range != null)
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
                                    serveSingleRange(headers, contentStream, originalRequest, range.Key, range.Value, contentLength);
                                    return useKeepAlive;
                                }
                                else if (ranges.Count > 1)
                                {
                                    serveRanges(headers, contentStream, originalRequest, ranges, contentLength);
                                    return useKeepAlive;
                                }
                            }
                        }
                    }

                    bool useGzip = useGzipOption != UseGzipOption.DontUseGzip && gzipRequested && !(contentLengthKnown && contentLength <= 1024) && originalRequest.HttpVersion == HttpProtocolVersion.Http11;

                    if (useGzip && useGzipOption == UseGzipOption.AutoDetect && contentLengthKnown && contentLength >= _server.Options.GzipAutodetectThreshold && contentStream.CanSeek)
                    {
                        try
                        {
                            contentStream.Seek((contentLength - _server.Options.GzipAutodetectThreshold) / 2, SeekOrigin.Begin);
                            byte[] buf = new byte[_server.Options.GzipAutodetectThreshold];
                            contentStream.Read(buf, 0, _server.Options.GzipAutodetectThreshold);
                            using (var ms = new MemoryStream())
                            {
                                using (var gzTester = new GZipOutputStream(ms))
                                {
                                    gzTester.SetLevel(1);
                                    gzTester.Write(buf, 0, _server.Options.GzipAutodetectThreshold);
                                }
                                if (ms.ToArray().Length >= 0.99 * _server.Options.GzipAutodetectThreshold)
                                    useGzip = false;
                            }
                            contentStream.Seek(0, SeekOrigin.Begin);
                        }
                        catch { }
                    }

                    headers.ContentEncoding = useGzip ? HttpContentEncoding.Gzip : HttpContentEncoding.Identity;

                    // If we know the content length and it is smaller than the in-memory gzip threshold, gzip and output everything now
                    if (useGzip && contentLengthKnown && contentLength < _server.Options.GzipInMemoryUpToSize)
                    {
                        // In this case, do all the gzipping before sending the headers.
                        // After all we want to include the new (compressed) Content-Length.
                        MemoryStream ms = new MemoryStream();
                        GZipOutputStream gz = new GZipOutputStream(ms);
                        gz.SetLevel(1);
                        byte[] contentReadBuffer = new byte[65536];
                        int bytes = contentStream.Read(contentReadBuffer, 0, 65536);
                        while (bytes > 0)
                        {
                            gz.Write(contentReadBuffer, 0, bytes);
                            bytes = contentStream.Read(contentReadBuffer, 0, 65536);
                        }
                        gz.Close();
                        byte[] resultBuffer = ms.ToArray();
                        headers.ContentLength = resultBuffer.Length;
                        sendHeaders(status, headers);
                        if (originalRequest.Method == HttpMethod.Head)
                            return useKeepAlive;
                        _stream.Write(resultBuffer);
                        return useKeepAlive;
                    }

                    Stream output;

                    if (useGzip && !useKeepAlive)
                    {
                        // In this case, send the headers first, then instantiate the GZipStream.
                        // Otherwise we run the risk that the GzipStream might write to the socket before the headers are sent.
                        // Also note that we are not sending a Content-Length header; even if we know the content length
                        // of the uncompressed file, we cannot predict the length of the compressed output yet
                        sendHeaders(status, headers);
                        if (originalRequest.Method == HttpMethod.Head)
                            return useKeepAlive;
                        var str = new GZipOutputStream(new DoNotCloseStream(_stream));
                        str.SetLevel(1);
                        output = str;
                    }
                    else if (useGzip)
                    {
                        // In this case, combine Gzip with chunked Transfer-Encoding. No Content-Length header
                        headers.TransferEncoding = HttpTransferEncoding.Chunked;
                        sendHeaders(status, headers);
                        if (originalRequest.Method == HttpMethod.Head)
                            return useKeepAlive;
                        var str = new GZipOutputStream(new ChunkedEncodingStream(_stream, leaveInnerOpen: true));
                        str.SetLevel(1);
                        output = str;
                    }
                    else if (useKeepAlive && !contentLengthKnown)
                    {
                        // Use chunked encoding without Gzip
                        headers.TransferEncoding = HttpTransferEncoding.Chunked;
                        sendHeaders(status, headers);
                        if (originalRequest.Method == HttpMethod.Head)
                            return useKeepAlive;
                        output = new ChunkedEncodingStream(_stream, leaveInnerOpen: true);
                    }
                    else
                    {
                        // No Gzip, no chunked, but if we know the content length, supply it
                        // (if we don't, then we're not using keep-alive here)
                        if (contentLengthKnown)
                            headers.ContentLength = contentLength;

                        sendHeaders(status, headers);

                        if (originalRequest.Method == HttpMethod.Head)
                            return useKeepAlive;

                        // We need DoNotCloseStream here because the later code needs to be able to
                        // close ‘output’ in case it’s a Gzip and/or Chunked stream; however, we don’t
                        // want to close the socket because it might be a keep-alive connection.
                        output = new DoNotCloseStream(_stream);
                    }

                    // Finally output the actual content
                    byte[] buffer = new byte[65536];
                    int bufferSize = buffer.Length;
                    int bytesRead;
                    while (true)
                    {
                        // There are no “valid” exceptions that may originate from the content stream, so the “Error500” setting
                        // actually propagates everything.
                        if (_server.PropagateExceptions)
                            bytesRead = contentStream.Read(buffer, 0, bufferSize);
                        else
                            try { bytesRead = contentStream.Read(buffer, 0, bufferSize); }
                            catch (Exception e)
                            {
                                if (e is not SocketException && _server.Options.OutputExceptionInformation)
                                    output.Write((headers.ContentType.StartsWith("text/html") ? exceptionToHtml(e) : exceptionToPlaintext(e)).ToUtf8());
                                _server.ResponseExceptionHandler?.Invoke(originalRequest, e, response);
                                output.Close();
                                return false;
                            }

                        if (bytesRead == 0)
                            break;

                        // Performance optimisation: If we’re at the end of a body of known length, cause
                        // the last bit to be sent to the socket without the Nagle delay
                        try
                        {
                            if (contentStream.CanSeek && contentStream.Position == contentStream.Length)
                                Socket.NoDelay = true;
                        }
                        catch { }

                        output.Write(buffer, 0, bytesRead);
                    }

                    // Important: If we are using Gzip and/or Chunked encoding, this causes the relevant
                    // streams to output the last bytes.
                    output.Close();

                    // Now re-enable the Nagle algorithm in case this is a keep-alive connection.
                    Socket.NoDelay = false;

                    return useKeepAlive;
                }
                finally
                {
                    try
                    {
                        if (originalRequest.FileUploads != null)
                            foreach (var fileUpload in originalRequest.FileUploads.Values.Where(fu => fu.LocalFilename != null && !fu.LocalFileMoved))
                                File.Delete(fileUpload.LocalFilename);
                    }
                    catch (Exception) { }
                }
            }

            private void sendHeaders(HttpStatusCode status, HttpResponseHeaders headers)
            {
                string headersStr = "HTTP/1.1 " + ((int) status) + " " + status.ToText() + "\r\n" +
                    headers.ToString() + "\r\n";
                _stream.Write(Encoding.UTF8.GetBytes(headersStr));
            }

            private void serveSingleRange(HttpResponseHeaders headers, Stream contentStream, HttpRequest originalRequest, long rangeFrom, long rangeTo, long totalFileSize)
            {
                // Note: this is the length of just the range, not the complete file (that's totalFileSize)
                headers.ContentLength = rangeTo - rangeFrom + 1;
                headers.ContentRange = new HttpContentRange { From = rangeFrom, To = rangeTo, Total = totalFileSize };
                sendHeaders(HttpStatusCode._206_PartialContent, headers);
                if (originalRequest.Method == HttpMethod.Head)
                    return;
                byte[] buffer = new byte[65536];

                contentStream.Seek(rangeFrom, SeekOrigin.Begin);
                long bytesMissing = rangeTo - rangeFrom + 1;
                int bytesRead = contentStream.Read(buffer, 0, (int) Math.Min(65536, bytesMissing));
                while (bytesRead > 0)
                {
                    _stream.Write(buffer, 0, bytesRead);
                    bytesMissing -= bytesRead;
                    bytesRead = (bytesMissing > 0) ? contentStream.Read(buffer, 0, (int) Math.Min(65536, bytesMissing)) : 0;
                }
            }

            private void serveRanges(HttpResponseHeaders headers, Stream contentStream, HttpRequest originalRequest, SortedList<long, long> ranges, long totalFileSize)
            {
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

                headers.ContentLength = cLength;
                headers.ContentType = "multipart/byteranges; boundary=" + Encoding.UTF8.GetString(boundary);
                sendHeaders(HttpStatusCode._206_PartialContent, headers);
                if (originalRequest.Method == HttpMethod.Head)
                    return;

                byte[] buffer = new byte[65536];
                foreach (var r in ranges)
                {
                    _stream.Write([(byte) '-', (byte) '-']);
                    _stream.Write(boundary);
                    _stream.Write(("\r\nContent-Range: bytes " + r.Key.ToString() + "-" + r.Value.ToString() + "/" + totalFileSize.ToString() + "\r\n\r\n").ToUtf8());

                    contentStream.Seek(r.Key, SeekOrigin.Begin);
                    long bytesMissing = r.Value - r.Key + 1;
                    int bytesRead = contentStream.Read(buffer, 0, (int) Math.Min(65536, bytesMissing));
                    while (bytesRead > 0)
                    {
                        _stream.Write(buffer, 0, bytesRead);
                        bytesMissing -= bytesRead;
                        bytesRead = (bytesMissing > 0) ? contentStream.Read(buffer, 0, (int) Math.Min(65536, bytesMissing)) : 0;
                    }
                    _stream.Write([13, 10]);
                }
                _stream.Write([(byte) '-', (byte) '-']);
                _stream.Write(boundary);
                _stream.Write([(byte) '-', (byte) '-', 13, 10]);
            }

            private HttpResponse errorParsingRequest(HttpRequest req, HttpStatusCode status, string userMessage = null)
            {
                try { throw new HttpRequestParseException(status, userMessage); }
                catch (HttpRequestParseException e) // thrown and caught so that StackTrace is non-null
                {
                    var response = exceptionToResponse(req, e);
                    response.Headers.Connection = HttpConnection.Close;
                    return response;
                }
            }

            private HttpResponse handleRequestAfterHeaders(out HttpRequest req)
            {
                string[] lines = _headersSoFar.Split(new string[] { "\r\n" }, StringSplitOptions.None);
                req = new HttpRequest { SourceIP = Socket.RemoteEndPoint as IPEndPoint };
                req.ClientIPAddress = req.SourceIP.Address;
                if (lines.Length < 2)
                    return errorParsingRequest(req, HttpStatusCode._400_BadRequest);

                // Parse the method line
                var line = lines[0];
                if (line.StartsWith("GET "))
                    req.Method = HttpMethod.Get;
                else if (line.StartsWith("HEAD "))
                    req.Method = HttpMethod.Head;
                else if (line.StartsWith("DELETE "))
                    req.Method = HttpMethod.Delete;
                else if (line.StartsWith("POST "))
                    req.Method = HttpMethod.Post;
                else if (line.StartsWith("PUT "))
                    req.Method = HttpMethod.Put;
                else if (line.StartsWith("PATCH "))
                    req.Method = HttpMethod.Patch;
                else
                    return errorParsingRequest(req, HttpStatusCode._501_NotImplemented);

                if (line.EndsWith(" HTTP/1.0"))
                    req.HttpVersion = HttpProtocolVersion.Http10;
                else if (line.EndsWith(" HTTP/1.1"))
                    req.HttpVersion = HttpProtocolVersion.Http11;
                else
                    return errorParsingRequest(req, HttpStatusCode._505_HttpVersionNotSupported);

                var url = new HttpUrl { Https = _secure };

                int start = req.Method.ToString().Length + 1;
                try { url.SetLocation(line.Substring(start, line.Length - start - 9)); }
                catch { return errorParsingRequest(req, HttpStatusCode._400_BadRequest); }

                // Parse the request headers
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
                            return errorParsingRequest(req, HttpStatusCode._400_BadRequest);
                        var error = parseHeader(lastHeader, valueSoFar, req);
                        if (error != null)
                            return error;
                        lastHeader = match.Groups[1].Value;
                        valueSoFar = match.Groups[2].Value.Trim();
                    }
                }
                var error2 = parseHeader(lastHeader, valueSoFar, req);
                if (error2 != null)
                    return error2;

                if (req.Headers.Host == null)
                    return errorParsingRequest(req, HttpStatusCode._400_BadRequest);
                try { url.SetHost(req.Headers.Host); }
                catch { return errorParsingRequest(req, HttpStatusCode._400_BadRequest); }

                url.AssertComplete();
                _server.Log.Info(1, "{0:X8} Request: {1} {2}".Fmt(_requestId, req.Method, url.ToFull()));
                req.Url = url;

                // This returns null in case of success and an error response in case of error
                var result = processRequestBody(req);
                if (result != null)
                    return result;
                return requestToResponse(req);
            }

            private HttpResponse requestToResponse(HttpRequest req)
            {
                var handler = _server.Handler;
                return handler == null
                    ? throw new HttpNotFoundException()
                    : handler(req) ?? throw new InvalidOperationException("The response is null.");
            }

            private HttpResponse exceptionToResponse(HttpRequest req, Exception exInHandler)
            {
                Exception exInErrorHandler = null;
                var errorHandler = _server.ErrorHandler;
                if (errorHandler != null)
                {
                    try
                    {
                        var resp = errorHandler(req, exInHandler);
                        if (resp != null)
                            return resp;
                    }
                    catch (Exception ex)
                    {
                        exInErrorHandler = ex;
                    }
                }
                return _server.defaultErrorHandler(exInHandler, exInErrorHandler);
            }

            private HttpResponse parseHeader(string headerName, string headerValue, HttpRequest req)
            {
                if (headerName == null)
                    return null;

                if (!req.Headers.parseAndAddHeader(headerName, headerValue))
                    return null; // the header was not recognised so just do nothing.

                string nameLower = headerName.ToLowerInvariant();

                // Special actions when we encounter certain headers
                if (nameLower == "expect")
                {
                    foreach (var kvp in req.Headers.Expect)
                        if (kvp.Key != "100-continue")
                            return errorParsingRequest(req, HttpStatusCode._417_ExpectationFailed);
                }
                else if (nameLower == "x-forwarded-for")
                {
                    req.ClientIPAddress = req.Headers.XForwardedFor[0];
                }

                return null;
            }

            private HttpResponse processRequestBody(HttpRequest req)
            {
                var contentLength = req.Headers.ContentLength ?? 0;

                // Some validity checks
                if (contentLength > _server.Options.MaxSizePostContent)
                    return errorParsingRequest(req, HttpStatusCode._413_RequestEntityTooLarge);
                if (req.Headers.ContentType == null)
                {
                    if (contentLength != 0)
                        return errorParsingRequest(req, HttpStatusCode._400_BadRequest, @"For requests with a body, ""Content-Type"" must be specified. Moreover, only ""application/x-www-form-urlencoded"" and ""multipart/form-data"" are supported.");
                    // Tolerate empty bodies without Content-Type (e.g. GET requests)
                    req.Headers.ContentType = HttpPostContentType.ApplicationXWwwFormUrlEncoded;
                }

                // If "Expect: 100-continue" was specified, send a 100 Continue here
                if (req.Headers.Expect != null && req.Headers.Expect.ContainsKey("100-continue"))
                    _stream.Write("HTTP/1.1 100 Continue\r\n\r\n".ToUtf8());

                // Read the contents of the request body
                Stream contentStream;
                if (contentLength == 0)
                {
                    contentStream = null;
                }
                else if (_bufferDataLength >= contentLength)
                {
                    // Conversion to int is safe because _bufferDataLength is int and we checked that contentLength is less or equal
                    contentStream = new MemoryStream(_buffer, _bufferDataOffset, (int) contentLength, false);
                    _bufferDataOffset += (int) contentLength;
                    _bufferDataLength -= (int) contentLength;
                }
                else
                {
                    contentStream = new Substream(_stream, contentLength, _buffer, _bufferDataOffset, _bufferDataLength);
                    _bufferDataOffset = 0;
                    _bufferDataLength = 0;
                }

                try
                {
                    req.ParsePostBody(contentStream, _server.Options.TempDir, _server.Options.StoreFileUploadInFileAtSize);
                }
                catch (SocketException) { }
                catch (IOException) { }

                // null means: no error
                return null;
            }
        }

        /// <summary>Keeps track of and exposes getters for various server performance statistics.</summary>
        public sealed class Statistics(HttpServer server)
        {
            /// <summary>
            ///     Gets the number of connections which are currently alive, that is receiving data, waiting to receive data,
            ///     or sending a response.</summary>
            public int ActiveHandlers { get { lock (server._activeConnectionHandlers) { return server._activeConnectionHandlers.Count(r => !r.KeepAliveActive); } } }

            /// <summary>
            ///     Gets the number of request processing threads which have completed a request but are being kept alive.</summary>
            public int KeepAliveHandlers { get { lock (server._activeConnectionHandlers) { return server._activeConnectionHandlers.Count(r => r.KeepAliveActive); } } }

            private long _totalConnectionsReceived = 0;
            /// <summary>Gets the total number of connections received by the server.</summary>
            public long TotalConnectionsReceived => Interlocked.Read(ref _totalConnectionsReceived);
            /// <summary>Used internally to count a received connection.</summary>
            internal void AddConnectionReceived() { Interlocked.Increment(ref _totalConnectionsReceived); }

            // requests received
            // bytes sent/received
            // max open connections
            // request serve time stats
        }
    }
}
