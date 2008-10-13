using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Collections;
using RT.Util.ExtensionMethods;
using RT.Util.Streams;
using RT.Util;

namespace RT.Servers
{
    /// <summary>
    /// Encapsulates the various ways in which a URL can map to a request handler. Use <see cref="HTTPServer.AddHandler"/>
    /// to hook a handler to a specific <see cref="HTTPServer"/> instance.
    /// </summary>
    public class HTTPRequestHandlerHook
    {
        /// <summary>If null (default), the handler applies to all domain names. Otherwise, the handler applies to this
        /// domain and all subdomains or to this domain only, depending on the value of <see cref="SpecificDomain"/>.</summary>
        public string Domain;

        /// <summary>If null (default), the handler applies to all ports; otherwise to the specified port only.</summary>
        public int? Port;

        /// <summary>If null (default), the handler applies to all URL paths. Otherwise, the handler applies to this
        /// path and all subpaths or to this path only, depending on the value of <see cref="SpecificPath"/>.</summary>
        public string Path;

        /// <summary>If false (default), the handler applies to all subdomains of the domain specified by
        /// <see cref="Domain"/>. Otherwise it applies to the specific domain only.</summary>
        public bool SpecificDomain;

        /// <summary>If false (default), the handler applies to all subpaths of the path specified by
        /// <see cref="Path"/>. Otherwise it applies to the specific path only.</summary>
        public bool SpecificPath;

        /// <summary>The request handler to hook.</summary>
        public HTTPRequestHandler Handler;
    }

    /// <summary>
    /// Provides an HTTP server.
    /// </summary>
    public partial class HTTPServer
    {
        /// <summary>
        /// Constructs an HTTP server with all configuration values set to default values.
        /// </summary>
        public HTTPServer() { Opt = new HTTPServerOptions(); }

        /// <summary>
        /// Constructs an HTTP server with the specified configuration settings.
        /// </summary>
        /// <param name="Options">Specifies the configuration settings to use for this <see cref="HTTPServer"/>.</param>
        public HTTPServer(HTTPServerOptions Options)
        {
            Opt = Options;
        }

        /// <summary>Adds a handler for a specified combination of domain, path and port.</summary>
        /// <param name="HandlerHook">A <see cref="HTTPRequestHandlerHook"/> structure containing the desired parameters.</param>
        public void AddHandler(HTTPRequestHandlerHook HandlerHook)
        {
            if (HandlerHook.Domain == null && HandlerHook.SpecificDomain)
                throw new ArgumentException("If the SpecificDomain option is set to true, a domain must be specified with the Domain option.");
            if (HandlerHook.Domain != null && !Regex.IsMatch(HandlerHook.Domain, @"^[-.a-z0-9]+$"))
                throw new ArgumentException("The domain specified with the Domain option must not contain any characters other than lower-case a-z, 0-9, hypen (-) or period (.).");
            if (HandlerHook.Domain != null && (HandlerHook.Domain.Contains(".-") || HandlerHook.Domain.Contains("-.") || HandlerHook.Domain.StartsWith("-") || HandlerHook.Domain.EndsWith("-")))
                throw new ArgumentException("The domain specified with the Domain option must not contain a domain name beginning or ending with a hyphen (-).");
            if (HandlerHook.Domain != null && !HandlerHook.SpecificDomain && HandlerHook.Domain.StartsWith("."))
                throw new ArgumentException(@"If the SpecificDomain option is set to false (or not specified), the domain specified with the Domain option must not begin with a period (.). It will, however, be treated as a domain. For example, if you specify the domain ""cream.net"", only domains ending in "".cream.net"" and the domain ""cream.net"" itself are considered. The domain ""scream.net"" would not be considered a match. If you wish to hook the handler to every domain, simply omit the Domain option entirely.");
            if (HandlerHook.Domain != null && (HandlerHook.Domain.StartsWith(".") || HandlerHook.Domain.EndsWith(".")))
                throw new ArgumentException(@"The domain specified with the Domain option must not begin or end with a period (.).");

            if (HandlerHook.Path == null && HandlerHook.SpecificPath)
                throw new ArgumentException("If the SpecificPath option is set to true, a path must be specified with the Path option.");
            if (HandlerHook.Path != null && !Regex.IsMatch(HandlerHook.Path, @"^/[-;/:@=&$_\.\+!*'\(\),a-zA-Z0-9]*$"))
                throw new ArgumentException("The path specified with the Path option must not contain any characters that are invalid in URLs, or the question mark (?) character, and it must begin with a slash (/).");
            if (HandlerHook.Path != null && !HandlerHook.SpecificPath && HandlerHook.Path.EndsWith("/"))
                throw new ArgumentException(@"If the SpecificPath option is set to false (or not specified), the path specified with the Path option must not end with a slash (/). It will, however, be treated as a directory. For example, if you specify the path ""/files"", only URLs beginning with ""/files/"" and the URL ""/files"" itself are considered. The URL ""/fileshare"" would not be considered a match. If you wish to hook the handler to the root directory of the domain, simply omit the Path option entirely.");

            if (HandlerHook.Handler == null)
                throw new ArgumentException("The handler specified by the Handler option cannot be null.");
            if (HandlerHook.Path != null && !HandlerHook.Path.StartsWith("/"))
                throw new ArgumentException("A path specified by the Path option must begin with the slash character (\"/\").");
            if (HandlerHook.Port != null && (HandlerHook.Port.Value < 1 || HandlerHook.Port.Value > 65535))
                throw new ArgumentException("The Port option must contain an integer in the range 1 to 65535.");

            RequestHandlerHooks.Add(HandlerHook);
        }

        /// <summary>
        /// Returns the configuration settings currently in effect for this server.
        /// </summary>
        public HTTPServerOptions Options { get { return Opt; } }

        /// <summary>
        /// Returns a boolean specifying whether the server is currently running (listening).
        /// </summary>
        public bool IsListening { get { return ListeningThread != null && ListeningThread.IsAlive; } }

        private TcpListener Listener;
        private Thread ListeningThread;
        private List<HTTPRequestHandlerHook> RequestHandlerHooks = new List<HTTPRequestHandlerHook>();
        private HTTPServerOptions Opt;

        /// <summary>If set, various debug events will be logged to here.</summary>
        public LoggerBase Log;

        /// <summary>
        /// Shuts the HTTP server down. This method is only useful if <see cref="StartListening"/>
        /// was called with the Blocking parameter set to false.
        /// </summary>
        public void StopListening()
        {
            if (!IsListening)
                return;
            ListeningThread.Abort();
            ListeningThread = null;
            Listener.Stop();
            Listener = null;
        }

        /// <summary>
        /// Runs the HTTP server.
        /// </summary>
        /// <param name="Blocking">If true, the method will continually wait for and handle incoming requests and never return.
        /// If false, a separate thread is spawned in which the server will handle incoming requests,
        /// and control is returned immediately.</param>
        public void StartListening(bool Blocking)
        {
            if (IsListening && !Blocking)
                return;
            if (IsListening)
                StopListening();

            Listener = new TcpListener(System.Net.IPAddress.Any, Opt.Port);
            Listener.Start();
            if (Blocking)
            {
                ListeningThreadFunction();
            }
            else
            {
                ListeningThread = new Thread(ListeningThreadFunction);
                ListeningThread.Start();
            }
        }

        /// <summary>
        /// Returns an <see cref="HTTPRequestHandler"/> that serves static files from a specified directory on the
        /// local file system and that lists the contents of directories within the specified directory.
        /// The MIME type used for the returned files is determined from <see cref="HTTPServerOptions.MIMETypes"/>.
        /// </summary>
        /// <param name="BaseDir">The base directory from which to serve files.</param>
        /// <returns>An <see cref="HTTPRequestHandler"/> that can be assigned to the indexing property of this <see cref="HTTPServer"/>.</returns>
        /// <example>
        ///     <code>
        ///         HTTPServer MyServer = new HTTPServer();
        ///         MyServer["www.mydomain.com/users"] = MyServer.FileSystemHandler(@"D:\UserFiles");
        ///     </code>
        ///     The above code will instantiate an <see cref="HTTPServer"/> which will serve files from the <c>D:\UserFiles</c> directory
        ///     on the local file system. For example, a request for the URL <c>http://www.mydomain.com/users/adam/report.txt</c>
        ///     will serve the file stored at the location <c>D:\UserFiles\adam\report.txt</c>. A request for the URL
        ///     <c>http://www.mydomain.com/users/adam/</c> will list all the files in the directory <c>D:\UserFiles\adam</c>.
        /// </example>
        public HTTPRequestHandler CreateFileSystemHandler(string BaseDir)
        {
            return Req => FileSystemResponse(BaseDir, Req);
        }

        /// <summary>
        /// Creates a handler which will serve the file specified in <paramref name="Filepath"/>.
        /// To be used in conjunction with <see cref="AddHandler"/>.
        /// See also: <see cref="CreateFileSystemHandler"/>.
        /// </summary>
        public HTTPRequestHandler CreateFileHandler(string Filepath)
        {
            return Req => FileResponse(Filepath);
        }

        /// <summary>
        /// Creates a handler which will redirect the browser to <paramref name="NewURL"/>.
        /// To be used in conjunction with <see cref="AddHandler"/>.
        /// </summary>
        public HTTPRequestHandler CreateRedirectHandler(string NewURL)
        {
            return Req => RedirectResponse(NewURL);
        }

        /// <summary>
        /// Returns an <see cref="HTTPResponse"/> that returns a file from the local file system,
        /// which is derived from the specified base directory and the URL of the specified request.
        /// </summary>
        /// <param name="BaseDir">Base directory in which to search for the file.</param>
        /// <param name="Req">HTTP request from the client.</param>
        /// <returns>An <see cref="HTTPResponse"/> encapsulating the file transfer.</returns>
        public HTTPResponse FileSystemResponse(string BaseDir, HTTPRequest Req)
        {
            string p = BaseDir.EndsWith("" + Path.DirectorySeparatorChar) ? BaseDir.Remove(BaseDir.Length - 1) : BaseDir;
            string BaseURL = Req.URL.Substring(0, Req.URL.Length - Req.RestURL.Length);
            string URL = Req.RestURL.Contains('?') ? Req.RestURL.Remove(Req.RestURL.IndexOf('?')) : Req.RestURL;
            string[] URLPieces = URL.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            string SoFar = "";
            string SoFarURL = "";
            for (int i = 0; i < URLPieces.Length; i++)
            {
                string Piece = URLPieces[i].URLUnescape();
                string NextSoFar = SoFar + Path.DirectorySeparatorChar + Piece;

                if (File.Exists(p + NextSoFar))
                {
                    DirectoryInfo ParentDir = new DirectoryInfo(p + SoFar);
                    foreach (var FileInf in ParentDir.GetFiles(Piece))
                    {
                        SoFarURL += "/" + FileInf.Name.URLEscape();
                        break;
                    }

                    if (Req.URL != BaseURL + SoFarURL)
                        return RedirectResponse(BaseURL + SoFarURL);

                    return FileResponse(p + NextSoFar);
                }
                else if (Directory.Exists(p + NextSoFar))
                {
                    DirectoryInfo ParentDir = new DirectoryInfo(p + SoFar);
                    foreach (var DirInfo in ParentDir.GetDirectories(Piece))
                    {
                        SoFarURL += "/" + DirInfo.Name.URLEscape();
                        break;
                    }
                }
                else
                {
                    return ErrorResponse(HTTPStatusCode._404_NotFound, "\"" + BaseURL + SoFarURL + "/" + Piece + "\" doesn't exist.");
                }
                SoFar = NextSoFar;
            }

            // If this point is reached, it's a directory
            string TrueDirURL = BaseURL + SoFarURL + "/";
            if (Req.URL != TrueDirURL)
                return RedirectResponse(TrueDirURL);

            if (Opt.DirectoryListingStyle == DirectoryListingStyle.XMLplusXSL)
            {
                return new HTTPResponse
                {
                    Headers = new HTTPResponseHeaders { ContentType = "application/xml; charset=utf-8" },
                    Status = HTTPStatusCode._200_OK,
                    Content = new DynamicContentStream(GenerateDirectoryXML(p + SoFar, TrueDirURL))
                };
            }
            else
                return ErrorResponse(HTTPStatusCode._500_InternalServerError);
        }

        /// <summary>
        /// Generates an <see cref="HTTPResponse"/> that causes the server to return the specified
        /// file from the local file system. The content type is inferred from the <see cref="HTTPServerOptions.MIMETypes"/>
        /// field in <see cref="Options"/>.
        /// </summary>
        /// <param name="Filepath">Full path and filename of the file to return.</param>
        /// <returns><see cref="HTTPResponse"/> object that encapsulates the return of the specified file.</returns>
        public HTTPResponse FileResponse(string Filepath)
        {
            FileInfo f = new FileInfo(Filepath);
            string Extension = f.Extension.Length > 1 ? f.Extension.Substring(1) : "*";
            return FileResponse(Filepath,
                Opt.MIMETypes.ContainsKey(Extension) ? Opt.MIMETypes[Extension] :
                Opt.MIMETypes.ContainsKey("*") ? Opt.MIMETypes["*"] : "application/octet-stream");
        }

        /// <summary>
        /// Generates an <see cref="HTTPResponse"/> that causes the server to return the specified
        /// file from the local file system using the specified MIME content type.
        /// </summary>
        /// <param name="Filepath">Full path and filename of the file to return.</param>
        /// <param name="ContentType">MIME type to use in the Content-Type header.</param>
        /// <returns><see cref="HTTPResponse"/> object that encapsulates the return of the specified file.</returns>
        public static HTTPResponse FileResponse(string Filepath, string ContentType)
        {
            if (!File.Exists(Filepath))
                return ErrorResponse(HTTPStatusCode._404_NotFound, "The requested file does not exist.");

            try
            {
                FileStream FileStream = File.Open(Filepath, FileMode.Open, FileAccess.Read, FileShare.Read);
                return new HTTPResponse
                {
                    Status = HTTPStatusCode._200_OK,
                    Content = FileStream,
                    Headers = new HTTPResponseHeaders { ContentType = ContentType }
                };
            }
            catch (IOException e)
            {
                return ErrorResponse(HTTPStatusCode._500_InternalServerError,
                    "File could not be opened in the file system: " + e.Message);
            }
        }

        /// <summary>
        /// Returns the specified string to the client, designating it as a specific MIME type.
        /// </summary>
        /// <param name="Content">Content to return to the client.</param>
        /// <param name="ContentType">MIME type of the content.</param>
        /// <returns>An <see cref="HTTPResponse"/> object encapsulating the return of the string.</returns>
        public static HTTPResponse StringResponse(string Content, string ContentType)
        {
            return new HTTPResponse
            {
                Content = new MemoryStream(Content.ToUTF8()),
                Headers = new HTTPResponseHeaders { ContentType = ContentType },
                Status = HTTPStatusCode._200_OK
            };
        }

        /// <summary>
        /// Returns the specified string to the client. The MIME type is assumed to be "text/html; charset=utf-8".
        /// </summary>
        /// <param name="Content">Content to return to the client.</param>
        /// <returns>An <see cref="HTTPResponse"/> object encapsulating the return of the string.</returns>
        public HTTPResponse StringResponse(string Content)
        {
            return new HTTPResponse
            {
                Content = new MemoryStream(Content.ToUTF8()),
                Headers = new HTTPResponseHeaders { ContentType = "text/html; charset=utf-8" },
                Status = HTTPStatusCode._200_OK
            };
        }

        /// <summary>
        /// Generates XML that represents the contents of a directory on the local file system.
        /// </summary>
        /// <param name="LocalPath">Full path of a directory to list the contents of.</param>
        /// <param name="URL">URL (not including a domain) that points at the directory.</param>
        /// <returns>XML that represents the contents of the specified directory.</returns>
        public static IEnumerable<string> GenerateDirectoryXML(string LocalPath, string URL)
        {
            if (!Directory.Exists(LocalPath))
                throw new FileNotFoundException("Directory does not exist.", LocalPath);

            List<DirectoryInfo> Dirs = new List<DirectoryInfo>();
            List<FileInfo> Files = new List<FileInfo>();
            DirectoryInfo Inf = new DirectoryInfo(LocalPath);
            foreach (var d in Inf.GetDirectories())
                Dirs.Add(d);
            foreach (var f in Inf.GetFiles())
                Files.Add(f);
            Dirs.Sort((a, b) => a.Name.CompareTo(b.Name));
            Files.Sort((a, b) => a.Name.CompareTo(b.Name));

            yield return "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n";
            yield return "<?xml-stylesheet href=\"/$/directory-listing/xsl\" type=\"text/xsl\" ?>\n";
            yield return "<directory url=\"" + URL.HTMLEscape() + "\" unescapedurl=\"" + URL.URLUnescape().HTMLEscape() + "\" img=\"/$/directory-listing/icons/folderbig\" numdirs=\"" + (Dirs.Count) + "\" numfiles=\"" + (Files.Count) + "\">\n";

            foreach (var d in Dirs)
                yield return "  <dir link=\"" + d.Name.URLEscape() + "/\" img=\"/$/directory-listing/icons/folder\">" + d.Name.HTMLEscape() + "</dir>\n";
            foreach (var f in Files)
            {
                string Ext = f.Name.Contains('.') ? f.Name.Substring(f.Name.LastIndexOf('.') + 1) : "";
                yield return "  <file link=\"" + f.Name.URLEscape() + "\" size=\"" + f.Length + "\" nicesize=\"" + PrettySize(f.Length);
                yield return "\" img=\"/$/directory-listing/icons/" + HTTPInternalObjects.GetDirectoryListingIconStr(Ext) + "\">" + f.Name.HTMLEscape() + "</file>\n";
            }

            yield return "</directory>\n";
        }

        /// <summary>
        /// Generates an <see cref="HTTPResponse"/> that redirects the client to a new URL, using the HTTP status code 301 Moved Permanently.
        /// </summary>
        /// <param name="NewURL">URL to redirect the client to.</param>
        /// <returns><see cref="HTTPResponse"/> encapsulating a redirect to the specified URL, using the HTTP status code 301 Moved Permanently.</returns>
        public static HTTPResponse RedirectResponse(string NewURL)
        {
            return new HTTPResponse
            {
                Headers = new HTTPResponseHeaders { Location = NewURL },
                Status = HTTPStatusCode._301_MovedPermanently
            };
        }

        /// <summary>
        /// Generates a simple <see cref="HTTPResponse"/> with the specified HTTP status code. Generally used for error.
        /// </summary>
        /// <param name="StatusCode">HTTP status code to use in the response.</param>
        /// <returns>A minimalist <see cref="HTTPResponse"/> with the specified HTTP status code.</returns>
        public static HTTPResponse ErrorResponse(HTTPStatusCode StatusCode)
        {
            return ErrorResponse(StatusCode, new HTTPResponseHeaders(), null);
        }

        /// <summary>
        /// Generates a simple <see cref="HTTPResponse"/> with the specified HTTP status code and message. Generally used for error.
        /// </summary>
        /// <param name="StatusCode">HTTP status code to use in the response.</param>
        /// <param name="Message">Message to display along with the HTTP status code.</param>
        /// <returns>A minimalist <see cref="HTTPResponse"/> with the specified HTTP status code and message.</returns>
        public static HTTPResponse ErrorResponse(HTTPStatusCode StatusCode, string Message)
        {
            return ErrorResponse(StatusCode, new HTTPResponseHeaders(), Message);
        }

        /// <summary>
        /// Generates a simple <see cref="HTTPResponse"/> with the specified HTTP status code and headers. Generally used for error.
        /// </summary>
        /// <param name="StatusCode">HTTP status code to use in the response.</param>
        /// <param name="Headers">Headers to use in the <see cref="HTTPResponse"/>.</param>
        /// <returns>A minimalist <see cref="HTTPResponse"/> with the specified HTTP status code and headers.</returns>
        public static HTTPResponse ErrorResponse(HTTPStatusCode StatusCode, HTTPResponseHeaders Headers)
        {
            return ErrorResponse(StatusCode, Headers, null);
        }

        /// <summary>
        /// Generates a simple <see cref="HTTPResponse"/> with the specified HTTP status code, headers and message. Generally used for error.
        /// </summary>
        /// <param name="StatusCode">HTTP status code to use in the response.</param>
        /// <param name="Headers">Headers to use in the <see cref="HTTPResponse"/>.</param>
        /// <param name="Message">Message to display along with the HTTP status code.</param>
        /// <returns>A minimalist <see cref="HTTPResponse"/> with the specified HTTP status code, headers and message.</returns>
        public static HTTPResponse ErrorResponse(HTTPStatusCode StatusCode, HTTPResponseHeaders Headers, string Message)
        {
            string StatusCodeName = ("" + ((int) StatusCode) + " " + HTTPInternalObjects.GetStatusCodeName(StatusCode)).HTMLEscape();
            Headers.ContentType = "text/html; charset=utf-8";

            // var x = new HTML(new HEAD(new TITLE("HTTP " + StatusCodeName)), new BODY(new H1(StatusCodeName), Message != null ? new P(Message) : null));
            string ContentStr = // x.ToEnumerable();
                "<html><head><title>HTTP " + StatusCodeName + "</title></head><body><h1>" + StatusCodeName + "</h1>" +
                (Message != null ? "<p>" + Message.HTMLEscape() + "</p>" : "") + "</body></html>";
            return new HTTPResponse
            {
                Status = StatusCode,
                Headers = Headers,
                Content = new MemoryStream(ContentStr.ToUTF8())
            };
        }

        private void ListeningThreadFunction()
        {
            while (true)
            {
                Socket Socket = Listener.AcceptSocket();
                new Thread(() => ReadingThreadFunction(Socket)).Start();
            }
        }

        private void ReadingThreadFunction(Socket Socket)
        {
            Stopwatch sw = new StopwatchDummy();
            string StopWatchFilename = Socket.RemoteEndPoint.ToString().Replace(':', '_');
            sw.Log("Start ReadingThreadFunction()");

            byte[] NextRead = null;
            int NextReadOffset = 0;
            int NextReadLength = 0;

            byte[] Buffer = new byte[65536];
            sw.Log("Allocate buffer");

            try
            {
                try
                {
                    if (Opt.IdleTimeout != 0)
                        Socket.ReceiveTimeout = Opt.IdleTimeout;
                    string HeadersSoFar = "";
                    sw.Log("Stuff before while(true) loop");
                    while (true)
                    {
                        sw.Log("Start of while(true) loop");
                        if (NextRead == null)
                        {
                            SocketError ErrorCode;
                            try { NextReadLength = Socket.Receive(Buffer, 0, 65536, SocketFlags.None, out ErrorCode); }
                            catch (SocketException) { Socket.Close(); return; }
                            sw.Log("Socket.Receive()");

                            if (ErrorCode != SocketError.Success)
                            {
                                Socket.Close();
                                return;
                            }

                            if (NextReadLength == 0)
                                continue;

                            NextRead = Buffer;
                            NextReadOffset = 0;
                            sw.Log("Stuff after Socket.Receive()");
                        }

                        // Stop soon if the headers become too large.
                        if (HeadersSoFar.Length + NextReadLength > Opt.MaxSizeHeaders)
                        {
                            Socket.Close();
                            return;
                        }

                        int PrevHeadersLength = HeadersSoFar.Length;
                        sw.Log("Stuff before HeadersSoFar += Encoding.ASCII.GetString(...)");
                        HeadersSoFar += Encoding.ASCII.GetString(NextRead, NextReadOffset, NextReadLength);
                        sw.Log("HeadersSoFar += Encoding.ASCII.GetString(...)");
                        bool Cont = HeadersSoFar.Contains("\r\n\r\n");
                        sw.Log(@"HeadersSoFar.Contains(""\r\n\r\n"")");
                        if (!Cont)
                        {
                            NextRead = null;
                            continue;
                        }

                        int SepIndex = HeadersSoFar.IndexOf("\r\n\r\n");
                        sw.Log(@"int SepIndex = HeadersSoFar.IndexOf(""\r\n\r\n"")");
                        HeadersSoFar = HeadersSoFar.Remove(SepIndex);
                        sw.Log(@"HeadersSoFar = HeadersSoFar.Remove(SepIndex)");

                        if (Log != null) Log.Info(HeadersSoFar);

                        NextReadOffset += SepIndex + 4 - PrevHeadersLength;
                        NextReadLength -= SepIndex + 4 - PrevHeadersLength;
                        sw.Log("Stuff before HandleRequestAfterHeaders()");
                        HTTPResponse Response = HandleRequestAfterHeaders(Socket, HeadersSoFar, NextRead, ref NextReadOffset, ref NextReadLength, sw);
                        sw.Log("Returned from HandleRequestAfterHeaders()");
                        if (NextReadLength == 0)
                            NextRead = null;
                        bool ConnectionKeepAlive = false;
                        try
                        {
                            sw.Log("Stuff before OutputResponse()");
                            ConnectionKeepAlive = OutputResponse(Socket, Response, sw);
                            sw.Log("Returned from OutputResponse()");
                        }
                        finally
                        {
                            if (Response.Content != null)
                            {
                                sw.Log("Stuff before Response.Content.Close()");
                                Response.Content.Close();
                                sw.Log("Response.Content.Close()");
                            }
                        }
                        if (ConnectionKeepAlive && Socket.Connected)
                        {
                            HeadersSoFar = "";
                            sw.Log("Reusing connection");
                            continue;
                        }
                        sw.Log("Stuff before Socket.Close()");
                        Socket.Close();
                        sw.Log("Socket.Close()");
                        return;
                    }
                }
                catch (SocketException)
                {
                    sw.Log("Socket Exception!");
                }
            }
            finally
            {
                sw.SaveToFile(@"C:\temp\log\log_" + StopWatchFilename);
            }
        }

        private void SendHeaders(Socket Socket, HTTPResponse Response)
        {
            string HeadersStr = "HTTP/1.1 " + ((int) Response.Status) + " " + HTTPInternalObjects.GetStatusCodeName(Response.Status) + "\r\n" +
                Response.Headers.ToString() + "\r\n";
            if (Log != null) Log.Info(HeadersStr);
            Socket.Send(Encoding.ASCII.GetBytes(HeadersStr));
        }

        private bool OutputResponse(Socket Socket, HTTPResponse Response, Stopwatch sw)
        {
            sw.Log("OutputResponse() - enter");

            try
            {
                // If no status is given, by default assume 200 OK
                if (Response.Status == HTTPStatusCode.None)
                    Response.Status = HTTPStatusCode._200_OK;

                // If no Content-Type is given and there is no Location header, use default
                if (Response.Headers.ContentType == null && Response.Headers.Location == null)
                    Response.Headers.ContentType = Opt.DefaultContentType;

                bool KeepAliveRequested = Response.OriginalRequest.Headers.Connection == HTTPConnection.KeepAlive;
                bool GzipRequested = false;
                if (Response.OriginalRequest.Headers.AcceptEncoding != null)
                    foreach (HTTPContentEncoding hce in Response.OriginalRequest.Headers.AcceptEncoding)
                        GzipRequested = GzipRequested || (hce == HTTPContentEncoding.Gzip);
                bool ContentLengthKnown = false;
                long ContentLength = 0;

                // Find out if we know the content length
                if (Response.Content == null)
                {
                    ContentLength = 0;
                    ContentLengthKnown = true;
                }
                else if (Response.Headers.ContentLength != null)
                {
                    ContentLength = Response.Headers.ContentLength.Value;
                    ContentLengthKnown = true;
                }
                else
                {
                    // See if we can deduce the content length from the stream
                    try
                    {
                        ContentLength = Response.Content.Length;
                        ContentLengthKnown = true;
                    }
                    catch (NotSupportedException) { }
                }

                bool UseKeepAlive = KeepAliveRequested;
                if (UseKeepAlive)
                    Response.Headers.Connection = HTTPConnection.KeepAlive;

                // If we know the content length and the stream can seek, then we can support Ranges - but it's not worth it for less than 1 MB
                if (ContentLengthKnown && ContentLength < 1024 * 1024 && Response.Status == HTTPStatusCode._200_OK && Response.Content.CanSeek)
                {
                    Response.Headers.AcceptRanges = HTTPAcceptRanges.Bytes;

                    // If the client requested a range, then serve it
                    if (Response.Status == HTTPStatusCode._200_OK && Response.OriginalRequest.Headers.Range != null)
                    {
                        // Construct a canonical set of satisfiable ranges
                        var Ranges = new SortedList<long, long>();
                        foreach (var r in Response.OriginalRequest.Headers.Range)
                        {
                            long rFrom = r.From == null || r.From.Value < 0 ? 0 : r.From.Value;
                            long rTo = r.To == null || r.To.Value >= ContentLength ? ContentLength - 1 : r.To.Value;
                            if (Ranges.ContainsKey(rFrom))
                                Ranges[rFrom] = Math.Max(Ranges[rFrom], rTo);
                            else
                                Ranges.Add(rFrom, rTo);
                        }

                        // If one of the ranges spans the complete file, don't bother with ranges
                        if (!Ranges.ContainsKey(0) || Ranges[0] < ContentLength - 1)
                        {
                            // Make a copy of this so that we can modify Ranges while iterating over it
                            var RangeFroms = new List<long>(Ranges.Keys);

                            long PrevFrom = 0;
                            bool HavePrevFrom = false;
                            foreach (long From in RangeFroms)
                            {
                                if (!HavePrevFrom)
                                {
                                    PrevFrom = From;
                                    HavePrevFrom = true;
                                }
                                else if (Ranges[PrevFrom] >= From)
                                {
                                    Ranges[PrevFrom] = Math.Max(Ranges[PrevFrom], Ranges[From]);
                                    Ranges.Remove(From);
                                }
                            }

                            // Note that "ContentLength" here refers to the total size of the file.
                            // The functions ServeSingleRange() and ServeRanges() will automatically
                            // set a Content-Length header that specifies the size of just the range(s).

                            // Also note that if Ranges.Count is 0, we want to fall through and handle the request without ranges
                            if (Ranges.Count == 1)
                            {
                                ServeSingleRange(Socket, Response, Ranges, ContentLength);
                                return UseKeepAlive;
                            }
                            else if (Ranges.Count > 1)
                            {
                                ServeRanges(Socket, Response, Ranges, ContentLength);
                                return UseKeepAlive;
                            }
                        }
                    }
                }

                bool UseGzip = GzipRequested && !(ContentLengthKnown && ContentLength <= 1024);
                if (UseGzip)
                    Response.Headers.ContentEncoding = HTTPContentEncoding.Gzip;

                sw.Log("OutputResponse() - find out things");

                // If we know the content length and it is smaller than the in-memory gzip threshold, gzip and output everything now
                if (UseGzip && ContentLengthKnown && ContentLength < Opt.GzipInMemoryUpToSize)
                {
                    sw.Log("OutputResponse() - using in-memory gzip");
                    // In this case, do all the gzipping before sending the headers.
                    // After all we want to include the new (compressed) Content-Length.
                    MemoryStream ms = new MemoryStream();
                    GZipStream gz = new GZipStream(ms, CompressionMode.Compress);
                    byte[] ContentReadBuffer = new byte[65536];
                    int Bytes = Response.Content.Read(ContentReadBuffer, 0, 65536);
                    while (Bytes > 0)
                    {
                        gz.Write(ContentReadBuffer, 0, Bytes);
                        Bytes = Response.Content.Read(ContentReadBuffer, 0, 65536);
                    }
                    gz.Close();
                    sw.Log("OutputResponse() - finished gzipping");
                    byte[] ResultBuffer = ms.ToArray();
                    Response.Headers.ContentLength = ResultBuffer.Length;
                    SendHeaders(Socket, Response);
                    sw.Log("OutputResponse() - finished sending headers");
                    if (Response.OriginalRequest.Method == HTTPMethod.HEAD)
                        return UseKeepAlive;
                    Socket.Send(ResultBuffer);
                    sw.Log("OutputResponse() - finished sending response");
                    return UseKeepAlive;
                }

                sw.Log("OutputResponse() - using something other than in-memory gzip");

                Stream Output;

                if (UseGzip && !UseKeepAlive)
                {
                    // In this case, send the headers first, then instantiate the GZipStream.
                    // Otherwise we run the risk that the GzipStream might write to the socket before the headers are sent.
                    // Also note that we are not sending a Content-Length header; even if we know the content length
                    // of the uncompressed file, we cannot predict the length of the compressed output yet
                    SendHeaders(Socket, Response);
                    sw.Log("OutputResponse() - sending headers");
                    if (Response.OriginalRequest.Method == HTTPMethod.HEAD)
                        return UseKeepAlive;
                    StreamOnSocket str = new StreamOnSocket(Socket);
                    Output = new GZipStream(str, CompressionMode.Compress);
                }
                else if (UseGzip)
                {
                    // In this case, combine Gzip with chunked Transfer-Encoding. No Content-Length header
                    Response.Headers.TransferEncoding = HTTPTransferEncoding.Chunked;
                    SendHeaders(Socket, Response);
                    sw.Log("OutputResponse() - sending headers");
                    if (Response.OriginalRequest.Method == HTTPMethod.HEAD)
                        return UseKeepAlive;
                    StreamOnSocket str = new StreamOnSocketChunked(Socket);
                    Output = new GZipStream(str, CompressionMode.Compress);
                }
                else if (UseKeepAlive && !ContentLengthKnown)
                {
                    // Use chunked encoding without Gzip
                    Response.Headers.TransferEncoding = HTTPTransferEncoding.Chunked;
                    SendHeaders(Socket, Response);
                    sw.Log("OutputResponse() - sending headers");
                    if (Response.OriginalRequest.Method == HTTPMethod.HEAD)
                        return UseKeepAlive;
                    Output = new StreamOnSocketChunked(Socket);
                }
                else
                {
                    // No Gzip, no chunked, but if we know the content length, supply it
                    // (if we don't, then we're not using keep-alive here)
                    if (ContentLengthKnown)
                        Response.Headers.ContentLength = ContentLength;

                    SendHeaders(Socket, Response);
                    sw.Log("OutputResponse() - sending headers");

                    // If the content length is zero, we can exit as quickly as possible
                    // (no need to instantiate an output stream)
                    if ((ContentLengthKnown && ContentLength == 0) || Response.OriginalRequest.Method == HTTPMethod.HEAD)
                        return UseKeepAlive;

                    Output = new StreamOnSocket(Socket);
                }

                sw.Log("OutputResponse() - instantiating output stream");

                // Finally output the actual content
                int BufferSize = 65536;
                byte[] Buffer = new byte[BufferSize];
                sw.Log("OutputResponse() - Allocate buffer");
                int BytesRead;
                while (true)
                {
                    if (Opt.ReturnExceptionsToClient)
                    {
                        try { BytesRead = Response.Content.Read(Buffer, 0, BufferSize); }
                        catch (Exception e)
                        {
                            SendExceptionToClient(Output, Response.Headers.ContentType, e);
                            return false;
                        }
                    }
                    else
                        BytesRead = Response.Content.Read(Buffer, 0, BufferSize);
                    sw.Log("OutputResponse() - Response.Content.Read()");
                    if (BytesRead == 0) break;
                    Output.Write(Buffer, 0, BytesRead);
                    sw.Log("OutputResponse() - Output.Write()");
                }
                Output.Close();
                sw.Log("OutputResponse() - Output.Close()");
                return UseKeepAlive;
            }
            finally
            {
                sw.Log("OutputResponse() - stuff before finally clause");
                try
                {
                    if (Response.OriginalRequest.TemporaryFile != null)
                        File.Delete(Response.OriginalRequest.TemporaryFile);
                }
                catch (Exception) { }
                sw.Log("OutputResponse() - finally clause");
            }
        }

        private void ServeSingleRange(Socket Socket, HTTPResponse Response, SortedList<long, long> Ranges, long TotalFileSize)
        {
            foreach (var r in Ranges)
            {
                Response.Status = HTTPStatusCode._206_PartialContent;
                // Note: this is the length of just the range
                Response.Headers.ContentLength = r.Value - r.Key + 1;
                // Note: here "ContentLength" is the length of the complete file
                Response.Headers.ContentRange = new HTTPContentRange { From = r.Key, To = r.Value, Total = TotalFileSize };
                SendHeaders(Socket, Response);
                if (Response.OriginalRequest.Method == HTTPMethod.HEAD)
                    return;
                byte[] Buffer = new byte[65536];

                Response.Content.Seek(r.Key, SeekOrigin.Begin);
                long BytesMissing = r.Value - r.Key + 1;
                int BytesRead = Response.Content.Read(Buffer, 0, (int) Math.Min(65536, BytesMissing));
                while (BytesRead > 0)
                {
                    Socket.Send(Buffer, 0, BytesRead, SocketFlags.None);
                    BytesMissing -= BytesRead;
                    BytesRead = (BytesMissing > 0) ? Response.Content.Read(Buffer, 0, (int) Math.Min(65536, BytesMissing)) : 0;
                }
                return;
            }
        }

        private void ServeRanges(Socket Socket, HTTPResponse Response, SortedList<long, long> Ranges, long TotalFileSize)
        {
            Response.Status = HTTPStatusCode._206_PartialContent;

            // Generate a random boundary token
            byte[] Boundary = new byte[64];
            lock (Ut.Rnd)
            {
                for (int i = 0; i < 64; i++)
                {
                    int r = Ut.Rnd.Next(16);
                    Boundary[i] = r < 10 ? ((byte) (r + '0')) : ((byte) (r + 'A' - 10));
                }
            }

            // Calculate the total content length
            long CLength = 0;
            foreach (var r in Ranges)
            {
                CLength += 68;                  // "--$boundary\r\n"
                CLength += 27 +                 // "Content-range: bytes $f-$l/$filesize\r\n\r\n"
                    r.Key.ToString().Length + r.Value.ToString().Length + TotalFileSize.ToString().Length;
                CLength += r.Key - r.Value + 1; // content
                CLength += 2;                   // "\r\n"
            }
            CLength += 70;                      // "--$boundary--\r\n"

            Response.Headers.ContentLength = CLength;
            Response.Headers.ContentType = "multipart/byteranges; boundary=" + Encoding.ASCII.GetString(Boundary);
            SendHeaders(Socket, Response);
            if (Response.OriginalRequest.Method == HTTPMethod.HEAD)
                return;

            byte[] Buffer = new byte[65536];
            foreach (var r in Ranges)
            {
                Socket.Send(new byte[] { (byte) '-', (byte) '-' });
                Socket.Send(Boundary);
                Socket.Send(("\r\nContent-Range: bytes " + r.Key.ToString() + "-" + r.Value.ToString() + "/" + TotalFileSize.ToString() + "\r\n\r\n").ToASCII());

                Response.Content.Seek(r.Key, SeekOrigin.Begin);
                long BytesMissing = r.Value - r.Key + 1;
                int BytesRead = Response.Content.Read(Buffer, 0, (int) Math.Min(65536, BytesMissing));
                while (BytesRead > 0)
                {
                    Socket.Send(Buffer, 0, BytesRead, SocketFlags.None);
                    BytesMissing -= BytesRead;
                    BytesRead = (BytesMissing > 0) ? Response.Content.Read(Buffer, 0, (int) Math.Min(65536, BytesMissing)) : 0;
                }
                Socket.Send(new byte[] { 13, 10 });
            }
            Socket.Send(new byte[] { (byte) '-', (byte) '-' });
            Socket.Send(Boundary);
            Socket.Send(new byte[] { (byte) '-', (byte) '-', 13, 10 });
        }

        private HTTPResponse HandleRequestAfterHeaders(Socket Socket, string Headers, byte[] BufferWithContentSoFar, ref int ContentOffset, ref int ContentLengthSoFar, Stopwatch sw)
        {
            sw.Log("HandleRequestAfterHeaders() - enter");

            string[] Lines = Headers.Split(new string[] { "\r\n" }, StringSplitOptions.None);
            if (Lines.Length < 2)
                return ErrorResponse(HTTPStatusCode._400_BadRequest);

            // Parse the method line
            Match Match = Regex.Match(Lines[0], @"^(GET|POST|HEAD) ([^ ]+) HTTP/1.1$");
            if (!Match.Success)
                return ErrorResponse(HTTPStatusCode._501_NotImplemented);

            sw.Log("HandleRequestAfterHeaders() - Stuff before new HTTPRequest()");
            HTTPRequest Req = new HTTPRequest
            {
                Method = Match.Groups[1].Value == "HEAD" ? HTTPMethod.HEAD :
                         Match.Groups[1].Value == "POST" ? HTTPMethod.POST : HTTPMethod.GET,
                URL = Match.Groups[2].Value,
                TempDir = Opt.TempDir   // this will only be used if there is a file upload in a POST request.
            };

            sw.Log("HandleRequestAfterHeaders() - new HTTPRequest()");

            // Parse the request headers
            try
            {
                string LastHeader = null;
                string ValueSoFar = null;
                for (int i = 1; i < Lines.Length; i++)
                {
                    if (Lines[i][0] == '\t' || Lines[i][0] == ' ')
                        ValueSoFar += " " + Lines[i].Trim();
                    else
                    {
                        Match = Regex.Match(Lines[i], @"^([-A-Za-z0-9_]+)\s*:\s*(.*)$");
                        if (!Match.Success)
                            return ErrorResponse(HTTPStatusCode._400_BadRequest);
                        ParseHeader(LastHeader, ValueSoFar, ref Req);
                        LastHeader = Match.Groups[1].Value.ToLowerInvariant();
                        ValueSoFar = Match.Groups[2].Value.Trim();
                    }
                }
                ParseHeader(LastHeader, ValueSoFar, ref Req);
            }
            catch (InvalidRequestException e)
            {
                return e.Response;
            }

            sw.Log("HandleRequestAfterHeaders() - Parse headers");

            if (Req.Handler == null)
                return ErrorResponse(HTTPStatusCode._404_NotFound);

            if (Req.Method == HTTPMethod.POST)
            {
                // Some validity checks
                if (Req.Headers.ContentLength == null)
                    return ErrorResponse(HTTPStatusCode._411_LengthRequired);
                if (Req.Headers.ContentLength.Value > Opt.MaxSizePostContent)
                    return ErrorResponse(HTTPStatusCode._413_RequestEntityTooLarge);

                // Read the contents of the POST request
                if (ContentLengthSoFar >= Req.Headers.ContentLength.Value)
                {
                    Req.Content = new MemoryStream(BufferWithContentSoFar, ContentOffset, (int) Req.Headers.ContentLength.Value, false);
                    ContentOffset += (int) Req.Headers.ContentLength.Value;
                    ContentLengthSoFar -= (int) Req.Headers.ContentLength.Value;
                }
                else if (Req.Headers.ContentLength.Value < Opt.UseFileUploadAtSize)
                {
                    // Receive the POST request content into an in-memory buffer
                    byte[] Buffer = new byte[Req.Headers.ContentLength.Value];
                    if (ContentLengthSoFar > 0)
                        Array.Copy(BufferWithContentSoFar, ContentOffset, Buffer, 0, ContentLengthSoFar);
                    while (ContentLengthSoFar < Req.Headers.ContentLength)
                    {
                        SocketError ErrorCode;
                        int BytesReceived = Socket.Receive(Buffer, ContentLengthSoFar, (int) Req.Headers.ContentLength.Value - ContentLengthSoFar, SocketFlags.None, out ErrorCode);
                        if (ErrorCode != SocketError.Success)
                            throw new SocketException();
                        ContentLengthSoFar += BytesReceived;
                    }
                    Req.Content = new MemoryStream(Buffer, 0, (int) Req.Headers.ContentLength.Value);
                    ContentLengthSoFar = 0;
                }
                else
                {
                    // Store the POST request content in a temporary file
                    Stream f;
                    try
                    {
                        Req.TemporaryFile = HTTPInternalObjects.RandomTempFilepath(Opt.TempDir, out f);
                    }
                    catch (IOException)
                    {
                        return ErrorResponse(HTTPStatusCode._500_InternalServerError);
                    }
                    try
                    {
                        if (ContentLengthSoFar > 0)
                            f.Write(BufferWithContentSoFar, ContentOffset, ContentLengthSoFar);
                        byte[] Buffer = new byte[65536];
                        while (ContentLengthSoFar < Req.Headers.ContentLength)
                        {
                            SocketError ErrorCode;
                            int BytesReceived = Socket.Receive(Buffer, 0, 65536, SocketFlags.None, out ErrorCode);
                            if (ErrorCode != SocketError.Success)
                                throw new SocketException();
                            if (BytesReceived > 0)
                            {
                                f.Write(Buffer, 0, BytesReceived);
                                ContentLengthSoFar += BytesReceived;
                            }
                        }
                        f.Close();
                        Req.Content = File.Open(Req.TemporaryFile, FileMode.Open, FileAccess.Read, FileShare.Read);
                        ContentLengthSoFar = 0;
                    }
                    catch (Exception e)
                    {
                        f.Close();
                        File.Delete(Req.TemporaryFile);
                        throw e;
                    }
                }
            }

            sw.Log("HandleRequestAfterHeaders() - Stuff before Req.Handler()");

            if (Opt.ReturnExceptionsToClient)
            {
                try
                {
                    HTTPResponse Resp = Req.Handler(Req);
                    sw.Log("HandleRequestAfterHeaders() - Req.Handler()");
                    Resp.OriginalRequest = Req;
                    return Resp;
                }
                catch (Exception e)
                {
                    HTTPResponse Resp = ExceptionResponse(e);
                    sw.Log("HandleRequestAfterHeaders() - ExceptionResponse()");
                    Resp.OriginalRequest = Req;
                    return Resp;
                }
            }
            else
            {
                HTTPResponse Resp = Req.Handler(Req);
                sw.Log("HandleRequestAfterHeaders() - Req.Handler()");
                Resp.OriginalRequest = Req;
                return Resp;
            }
        }

        /// <summary>
        /// Generates a 500 Internal Server Error response which formats the specified exception as HTML.
        /// </summary>
        /// <param name="e">Exception to format.</param>
        /// <returns>An HTTPResponse to use to respond to a client request which threw the exception.</returns>
        public static HTTPResponse ExceptionResponse(Exception e)
        {
            return new HTTPResponse
            {
                Status = HTTPStatusCode._500_InternalServerError,
                Headers = new HTTPResponseHeaders { ContentType = "text/html; charset=utf-8" },
                Content = new MemoryStream(ExceptionAsString(e, true).ToUTF8())
            };
        }

        private void SendExceptionToClient(Stream Output, string ContentType, Exception e)
        {
            if (e is SocketException) throw e;

            byte[] Outp = ExceptionAsString(e,
                ContentType.StartsWith("text/html") || ContentType.StartsWith("application/xhtml")).ToUTF8();
            Output.Write(Outp, 0, Outp.Length);
            Output.Close();
        }

        private static string ExceptionAsString(Exception e, bool HTML)
        {
            string ExceptionText = "";
            bool first = true;
            if (HTML)
            {
                ExceptionText += "<div class='exception'>";
                while (e != null)
                {
                    ExceptionText += first ? "" : "<hr />";
                    ExceptionText += "<h3>" + e.GetType().FullName.HTMLEscape() + "</h3>";
                    ExceptionText += "<p>" + e.Message.HTMLEscape() + "</p>";
                    ExceptionText += "<pre>" + e.StackTrace.HTMLEscape() + "</pre>";
                    e = e.InnerException;
                    first = false;
                }
                ExceptionText += "</div>";
            }
            else        // Assume plain text
            {
                while (e != null)
                {
                    ExceptionText += first ? "\n\n\n" : "----------------------------------------------------------------------\n";
                    ExceptionText += e.GetType().FullName + "\n\n";
                    ExceptionText += e.Message + "\n\n";
                    ExceptionText += e.StackTrace + "\n\n";
                    e = e.InnerException;
                    first = false;
                }
            }
            return ExceptionText;
        }

        // Expects HeaderName in lower-case
        private void ParseHeader(string HeaderName, string HeaderValue, ref HTTPRequest Req)
        {
            if (HeaderName == null)
                return;

            string ValueLower = HeaderValue.ToLowerInvariant();
            int IntOutput;

            if (HeaderName == "accept")
                Req.Headers.Accept = SplitAndSortByQ(HeaderValue);
            else if (HeaderName == "accept-charset")
                Req.Headers.AcceptCharset = SplitAndSortByQ(HeaderValue);
            else if (HeaderName == "accept-encoding")
            {
                string[] StrList = SplitAndSortByQ(HeaderValue.ToLowerInvariant());
                var List = new List<HTTPContentEncoding>();
                foreach (string Str in StrList)
                {
                    if (Str == "compress") List.Add(HTTPContentEncoding.Compress);
                    else if (Str == "deflate") List.Add(HTTPContentEncoding.Deflate);
                    else if (Str == "gzip") List.Add(HTTPContentEncoding.Gzip);
                }
                Req.Headers.AcceptEncoding = List.ToArray();
            }
            else if (HeaderName == "accept-language")
                Req.Headers.AcceptLanguage = SplitAndSortByQ(HeaderValue);
            else if (HeaderName == "connection" && ValueLower == "close")
                Req.Headers.Connection = HTTPConnection.Close;
            else if (HeaderName == "connection" && (ValueLower == "keep-alive" || ValueLower == "keepalive"))
                Req.Headers.Connection = HTTPConnection.KeepAlive;
            else if (HeaderName == "content-length" && int.TryParse(HeaderValue, out IntOutput))
                Req.Headers.ContentLength = IntOutput;
            else if (HeaderName == "content-type")
            {
                if (Req.Method == HTTPMethod.POST)
                {
                    if (ValueLower == "application/x-www-form-urlencoded")
                        Req.Headers.ContentType = HTTPPOSTContentType.ApplicationXWWWFormURLEncoded;
                    else
                    {
                        Match m = Regex.Match(ValueLower, @"^multipart/form-data\s*;\s*boundary=");
                        if (m.Success)
                        {
                            Req.Headers.ContentType = HTTPPOSTContentType.MultipartFormData;
                            Req.Headers.ContentMultipartBoundary = HeaderValue.Substring(m.Length);
                        }
                        else
                            throw new InvalidRequestException(ErrorResponse(HTTPStatusCode._501_NotImplemented));
                    }
                }
            }
            else if (HeaderName == "cookie")
            {
                Cookie PrevCookie = new Cookie { Name = null };
                while (HeaderValue.Length > 0)
                {
                    string Key, Value;
                    Match m = Regex.Match(HeaderValue, @"^\s*(\$?\w+)=([^;]*)(;\s*|$)");
                    if (m.Success)
                    {
                        Key = m.Groups[1].Value;
                        Value = m.Groups[2].Value;
                    }
                    else
                    {
                        m = Regex.Match(HeaderValue, @"^\s*(\$?\w+)=""([^""]*)""(;\s*|$)");
                        if (m.Success)
                        {
                            Key = m.Groups[1].Value;
                            Value = m.Groups[2].Value;
                        }
                        else
                        {
                            if (HeaderValue.Contains(';'))
                            {
                                // Invalid syntax; try to continue parsing at the next ";"
                                HeaderValue = HeaderValue.Substring(HeaderValue.IndexOf(';') + 1);
                                continue;
                            }
                            else
                                // Completely invalid syntax; ignore the rest of this header
                                return;
                        }
                    }
                    HeaderValue = HeaderValue.Substring(m.Groups[0].Length);

                    if (Key == "$Version")
                        continue;   // ignore that.

                    if (Req.Headers.Cookie == null)
                        Req.Headers.Cookie = new Dictionary<string, Cookie>();

                    if (Key == "$Path" && PrevCookie.Name != null)
                    {
                        PrevCookie.Path = Value;
                        Req.Headers.Cookie[PrevCookie.Name] = PrevCookie;
                    }
                    else if (Key == "$Domain" && PrevCookie.Name != null)
                    {
                        PrevCookie.Domain = Value;
                        Req.Headers.Cookie[PrevCookie.Name] = PrevCookie;
                    }
                    else if (Key == "$Expires" && PrevCookie.Name != null)
                    {
                        DateTime Output;
                        if (DateTime.TryParse(HeaderValue, out Output))
                        {
                            PrevCookie.Expires = Output;
                            Req.Headers.Cookie[PrevCookie.Name] = PrevCookie;
                        }
                    }
                    else
                    {
                        PrevCookie = new Cookie { Name = Key, Value = Value };
                        Req.Headers.Cookie[Key] = PrevCookie;
                    }
                }
            }
            else if (HeaderName == "host")
            {
                // Can't have more than one "Host" header
                if (Req.Headers.Host != null)
                    throw new InvalidRequestException(ErrorResponse(HTTPStatusCode._400_BadRequest));

                // For performance reasons, we check if we have a handler for this domain/URL as soon as possible.
                // If we find out that we don't, stop processing here and immediately output an error
                if (Req.URL.StartsWith("/$/"))
                    Req.Handler = InternalHandler;
                else
                {
                    string Host = ValueLower;
                    int Port = 80;
                    if (Host.Contains(":"))
                    {
                        int Pos = Host.IndexOf(":");
                        int.TryParse(Host.Substring(Pos + 1), out Port);
                        Host = Host.Remove(Pos);
                    }
                    Host = Host.TrimEnd('.');

                    string URL = Req.URL.Contains('?') ? Req.URL.Remove(Req.URL.IndexOf('?')) : Req.URL;

                    var Hook = RequestHandlerHooks.FirstOrDefault(hk => (hk.Port == null || hk.Port.Value == Port) &&
                            (hk.Domain == null || hk.Domain == Host || (!hk.SpecificDomain && Host.EndsWith("." + hk.Domain))) &&
                            (hk.Path == null || hk.Path == URL || (!hk.SpecificPath && URL.StartsWith(hk.Path + "/"))));
                    if (Hook == null)
                        throw new InvalidRequestException(ErrorResponse(HTTPStatusCode._404_NotFound));

                    Req.Handler = Hook.Handler;
                    Req.BaseURL = Hook.Path == null ? "" : Hook.Path;
                    Req.RestURL = Hook.Path == null ? URL : URL.Substring(Hook.Path.Length);
                    Req.Domain = Host;
                    Req.BaseDomain = Hook.Domain == null ? "" : Hook.Domain;
                    Req.RestDomain = Hook.Domain == null ? Host : Host.Remove(Host.Length - Hook.Domain.Length);
                }
                Req.Headers.Host = ValueLower;
            }
            else if (HeaderName == "if-modified-since")
            {
                DateTime Output;
                if (DateTime.TryParse(HeaderValue, out Output))
                    Req.Headers.IfModifiedSince = Output;
            }
            else if (HeaderName == "if-none-match")
                Req.Headers.IfNoneMatch = ValueLower;
            else if (HeaderName == "range" && ValueLower.StartsWith("bytes="))
            {
                string[] RangesStr = ValueLower.Split(',');
                HTTPRange[] Ranges = new HTTPRange[RangesStr.Length];
                for (int i = 0; i < RangesStr.Length; i++)
                {
                    if (RangesStr[i] == null || RangesStr[i].Length < 2)
                        return;
                    Match m = Regex.Match(RangesStr[i], @"(\d*)-(\d*)");
                    if (!m.Success)
                        return;
                    if (m.Groups[1].Length > 0)
                        Ranges[i].From = int.Parse(m.Groups[1].Value);
                    if (m.Groups[2].Length > 0)
                        Ranges[i].To = int.Parse(m.Groups[2].Value);
                }
                Req.Headers.Range = Ranges;
            }
            else if (HeaderName == "user-agent")
                Req.Headers.UserAgent = HeaderValue;
            else
            {
                if (Req.Headers.UnrecognisedHeaders == null)
                    Req.Headers.UnrecognisedHeaders = new Dictionary<string, string>();
                Req.Headers.UnrecognisedHeaders.Add(HeaderName, HeaderValue);
            }
        }

        private HTTPResponse InternalHandler(HTTPRequest Req)
        {
            if (Req.URL == "/$/directory-listing/xsl")
            {
                return new HTTPResponse
                {
                    Headers = new HTTPResponseHeaders { ContentType = "application/xml; charset=utf-8" },
                    Content = new MemoryStream(HTTPInternalObjects.DirectoryListingXSL())
                };
            }
            else if (Req.URL.StartsWith("/$/directory-listing/icons/"))
            {
                string Rest = Req.URL.Substring(27);
                return new HTTPResponse
                {
                    Headers = new HTTPResponseHeaders { ContentType = "image/png" },
                    Content = new MemoryStream(HTTPInternalObjects.GetDirectoryListingIcon(Rest))
                };
            }

            return ErrorResponse(HTTPStatusCode._404_NotFound);
        }

        private string[] SplitAndSortByQ(string Value)
        {
            var Split = Regex.Split(Value, @"\s*,\s*");
            var Items = new SortedList<float, List<string>>();
            foreach (string Item in Split)
            {
                float q = 0;
                string NItem = Item;
                if (Item.Contains(";"))
                {
                    var Match = Regex.Match(Item, @";\s*q=(\d+(\.\d+)?)");
                    if (Match.Success) q = 1 - float.Parse(Match.Groups[1].Value);
                    NItem = Item.Remove(Item.IndexOf(';'));
                }
                if (!Items.ContainsKey(q))
                    Items[q] = new List<string>();
                Items[q].Add(NItem);
            }
            var FinalList = new List<string>();
            foreach (var kvp in Items)
                FinalList.AddRange(kvp.Value);
            return FinalList.ToArray();
        }

        /// <summary>
        /// Returns a file size in user-readable format, using units like KB, MB, GB, TB.
        /// </summary>
        /// <param name="Size">Size of a file in bytes.</param>
        /// <returns>User-readable formatted file size.</returns>
        public static string PrettySize(long Size)
        {
            if (Size >= (1L << 40))
                return string.Format("{0:0.00} TB", (double) Size / (1L << 40));
            if (Size >= (1L << 30))
                return string.Format("{0:0.00} GB", (double) Size / (1L << 30));
            if (Size >= (1L << 20))
                return string.Format("{0:0.00} MB", (double) Size / (1L << 20));
            return string.Format("{0:0.00} KB", (double) Size / (1L << 10));
        }
    }
}
