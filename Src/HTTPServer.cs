﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Servers
{
    public class HTTPServer
    {
        public HTTPServer() { Opt = new HTTPServerOptions(); }  // use default values
        public HTTPServer(HTTPServerOptions Options) { Opt = Options; }

        public HTTPRequestHandler this[string index]
        {
            get { return RequestHandlers[index.ToLowerInvariant()]; }
            set { RequestHandlers[index.ToLowerInvariant()] = value; }
        }

        public HTTPServerOptions Options { get { return Opt; } }
        public bool IsListening { get { return ListeningThread != null && ListeningThread.IsAlive; } }

        private TcpListener Listener;
        private Thread ListeningThread;
        private Dictionary<string, HTTPRequestHandler> RequestHandlers = new Dictionary<string, HTTPRequestHandler>();
        private HTTPServerOptions Opt;

        public void StopListening()
        {
            if (!IsListening)
                return;
            ListeningThread.Abort();
            ListeningThread = null;
            Listener.Stop();
            Listener = null;
        }

        public void StartListening(int Port, bool Blocking)
        {
            if (IsListening && !Blocking)
                return;
            if (IsListening)
                StopListening();

            Listener = new TcpListener(System.Net.IPAddress.Any, Port);
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

        public HTTPResponse FileSystemHandler(string BaseDir, HTTPRequest Req)
        {
            string p = BaseDir.EndsWith("" + Path.DirectorySeparatorChar) ? BaseDir.Remove(BaseDir.Length - 1) : BaseDir;
            string BaseURL = Req.URL.Substring(0, Req.URL.Length - Req.RestURL.Length);
            string URL = Req.RestURL.Contains('?') ? Req.RestURL.Remove(Req.RestURL.IndexOf('?')) : Req.RestURL;
            string[] URLPieces = URL.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            string SoFar = "";
            string SoFarURL = "";
            for (int i = -1; i < URLPieces.Length; i++)
            {
                if (i >= 0)
                {
                    string Piece = URLPieces[i].URLUnescape();
                    SoFar += Path.DirectorySeparatorChar + Piece;
                    SoFarURL += "/" + Piece;
                }
                if (File.Exists(p + SoFar))
                {
                    FileStream FileStream = File.Open(p + SoFar, FileMode.Open, FileAccess.Read, FileShare.Read);
                    HTTPResponseHeaders Headers = new HTTPResponseHeaders();
                    Headers.ContentLength = FileStream.Length;
                    string Extension = SoFar.Contains('.') ? SoFar.Substring(SoFar.LastIndexOf('.') + 1) : "*";
                    Headers.ContentType = Opt.MIMETypes.ContainsKey(Extension) ? Opt.MIMETypes[Extension] :
                        Opt.MIMETypes.ContainsKey("*") ? Opt.MIMETypes["*"] : "application/octet-stream";
                    return new HTTPResponse() { Content = FileStream, Headers = Headers, Status = HTTPStatusCode._200_OK };
                }
                else if (!Directory.Exists(p + SoFar))
                {
                    return GenericError(HTTPStatusCode._404_NotFound, "\"" + BaseURL + SoFarURL + "\" doesn't exist.");
                }
            }
            // If this point is reached, it's a directory
            if (!Req.URL.EndsWith("/"))
                return GenericRedirect(Req.URL + "/");

            if (Opt.DirectoryListingStyle == DirectoryListingStyle.XMLplusXSL)
            {
                return new HTTPResponse()
                {
                    Headers = new HTTPResponseHeaders() { ContentType = "application/xml; charset=utf-8" },
                    Status = HTTPStatusCode._200_OK,
                    Content = new DynamicContentStream(DirectoryHandlerXMLplusXSL(p + SoFar, BaseURL + SoFarURL))
                };
            }
            /*
            else if (Opt.DirectoryListingStyle == DirectoryListingStyle.HTML)
            {
                return new HTTPResponse()
                {
                    Headers = new HTTPResponseHeaders() { ContentType = "text/html" },
                    Status = HTTPStatusCode._200_OK,
                    Content = new DynamicContentStream(DirectoryHandlerHTML(p + SoFar, BaseURL + SoFarURL))
                };
            }
            */
            else
                return GenericError(HTTPStatusCode._500_InternalServerError);
        }

        public static IEnumerable<string> DirectoryHandlerXMLplusXSL(string LocalPath, string URL)
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
            Dirs.Sort((DirectoryInfo a, DirectoryInfo b) => { return a.Name.CompareTo(b.Name); });
            Files.Sort((FileInfo a, FileInfo b) => { return a.Name.CompareTo(b.Name); });

            yield return "<?xml version=\"1.0\" encoding=\"utf-8\"?>";
            yield return "<?xml-stylesheet href=\"/$/directory-listing/xsl\" type=\"text/xsl\" ?>";
            yield return "<directory url=\"" + URL.URLEscape() + "\" img=\"/$/directory-listing/icons/folderbig\" numdirs=\"" + (Dirs.Count) + "\" numfiles=\"" + (Files.Count) + "\">";

            foreach (var d in Dirs)
                yield return "<dir link=\"" + d.Name.URLEscape() + "/\" img=\"/$/directory-listing/icons/folder\">" + d.Name.HTMLEscape() + "</dir>";
            foreach (var f in Files)
            {
                string Ext = f.Name.Contains('.') ? f.Name.Substring(f.Name.LastIndexOf('.') + 1) : "";
                yield return "<file link=\"" + f.Name.URLEscape() + "\" size=\"" + f.Length + "\" nicesize=\"" + PrettySize(f.Length);
                yield return "\" img=\"/$/directory-listing/icons/" + HTTPInternalObjects.GetDirectoryListingIconStr(Ext) + "\">" + f.Name.HTMLEscape() + "</file>";
            }

            yield return "</directory>";
        }

        public static HTTPResponse GenericRedirect(string NewURL)
        {
            return new HTTPResponse()
            {
                Headers = new HTTPResponseHeaders() { Location = NewURL },
                Status = HTTPStatusCode._301_MovedPermanently
            };
        }

        private void ListeningThreadFunction()
        {
            while (true)
            {
                Socket Socket = Listener.AcceptSocket();
                new Thread(delegate() { ReadingThreadFunction(Socket); }).Start();
            }
        }

        private void ReadingThreadFunction(Socket Socket)
        {
            if (Opt.IdleTimeout != 0)
                Socket.ReceiveTimeout = Opt.IdleTimeout;
            string HeadersSoFar = "";
            while (true)
            {
                byte[] Buffer = new byte[65536];
                SocketError ErrorCode;
                int BytesReceived;
                try { BytesReceived = Socket.Receive(Buffer, 0, 65536, SocketFlags.None, out ErrorCode); }
                catch (SocketException) { Socket.Close(); return; }

                if (ErrorCode != SocketError.Success)
                {
                    Socket.Close();
                    return;
                }

                if (BytesReceived == 0)
                    continue;

                // Stop soon if the headers become too large.
                if (HeadersSoFar.Length + BytesReceived > Opt.MaxSizeHeaders)
                {
                    Socket.Close();
                    return;
                }

                HeadersSoFar += Encoding.ASCII.GetString(Buffer, 0, BytesReceived);
                if (HeadersSoFar.Contains("\r\n\r\n"))
                {
                    int i = HeadersSoFar.IndexOf("\r\n\r\n");
                    string Headers = HeadersSoFar.Remove(i);
                    Console.WriteLine(Headers);
                    Console.WriteLine();
                    try
                    {
                        HTTPResponse Response = HandleRequestAfterHeaders(Socket, Headers, Buffer, i + 4, BytesReceived - i - 4);
                        bool ConnectionKeepAlive = false;
                        try
                        {
                            ConnectionKeepAlive = OutputResponse(Socket, Response);
                        }
                        finally
                        {
                            if (Response.Content != null)
                                Response.Content.Close();
                        }
                        if (ConnectionKeepAlive && Socket.Connected)
                        {
                            HeadersSoFar = "";
                            continue;
                        }
                    }
                    catch (SocketException)
                    {
                    }

                    // Workaround. It seems that closing the socket immediately doesn't necessarily flush all the data. We want to make sure that all the data is received.
                    // if (Socket.Connected)
                        // Thread.Sleep(1);

                    Socket.Close();
                    return;
                }
            }
        }

        private void SendHeaders(Socket Socket, HTTPResponse Response)
        {
            string HeadersStr = "HTTP/1.1 " + ((int) Response.Status) + " " + GetStatusCodeName(Response.Status) + "\r\n" +
                Response.Headers.ToString() + "\r\n";
            Console.WriteLine(HeadersStr);
            Socket.Send(Encoding.ASCII.GetBytes(HeadersStr));
        }

        private bool OutputResponse(Socket Socket, HTTPResponse Response)
        {
            // If no status is given, by default assume 200 OK
            if (Response.Status == HTTPStatusCode.None)
                Response.Status = HTTPStatusCode._200_OK;

            // If no Content-Type is given, use default
            if (Response.Headers.ContentType == null)
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

            bool UseGzip = GzipRequested && !(ContentLengthKnown && ContentLength <= 256);
            bool UseKeepAlive = KeepAliveRequested;

            // Set the appropriate headers
            if (UseGzip)
                Response.Headers.ContentEncoding = HTTPContentEncoding.Gzip;
            if (UseKeepAlive)
                Response.Headers.Connection = HTTPConnection.KeepAlive;

            // If we know the content length and it is smaller than the in-memory gzip threshold, gzip and output everything now
            if (UseGzip && ContentLengthKnown && ContentLength < Opt.GzipInMemoryUpToSize)
            {
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
                byte[] ResultBuffer = ms.ToArray();
                Response.Headers.ContentLength = ResultBuffer.Length;
                SendHeaders(Socket, Response);
                if (Response.OriginalRequest.Method == HTTPMethod.HEAD)
                    return UseKeepAlive;
                Socket.Send(ResultBuffer);
                return UseKeepAlive;
            }

            Stream Output;

            if (UseGzip && !UseKeepAlive)
            {
                // In this case, send the headers first, then instantiate the GZipStream.
                // Otherwise we run the risk that the GzipStream might write to the socket before the headers are sent.
                // Also note that we are not sending a Content-Length header; even if we know the content length
                // of the uncompressed file, we cannot predict the length of the compressed output yet
                SendHeaders(Socket, Response);
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

                // If the content length is zero, we can exit as quickly as possible
                // (no need to instantiate an output stream)
                if ((ContentLengthKnown && ContentLength == 0) || Response.OriginalRequest.Method == HTTPMethod.HEAD)
                    return UseKeepAlive;

                Output = new StreamOnSocket(Socket);
            }

            // Finally output the actual content
            int BufferSize = 65536;
            byte[] Buffer = new byte[BufferSize];
            int BytesRead = Response.Content.Read(Buffer, 0, BufferSize);
            while (BytesRead > 0)
            {
                Output.Write(Buffer, 0, BytesRead);
                BytesRead = Response.Content.Read(Buffer, 0, BufferSize);
            }
            Output.Close();
            return UseKeepAlive;
        }

        private HTTPResponse HandleRequestAfterHeaders(Socket Socket, string Headers, byte[] BufferWithContentSoFar, int ContentOffset, int ContentLengthSoFar)
        {
            string[] Lines = Headers.Split(new string[] { "\r\n" }, StringSplitOptions.None);
            if (Lines.Length < 2)
                return GenericError(HTTPStatusCode._400_BadRequest);

            // Parse the method line
            Match Match = Regex.Match(Lines[0], @"^(GET|POST|HEAD) ([^ ]+) HTTP/1.1$");
            if (!Match.Success)
                return GenericError(HTTPStatusCode._501_NotImplemented);

            HTTPRequest Req = new HTTPRequest()
            {
                Method = Match.Groups[1].Value == "HEAD" ? HTTPMethod.HEAD :
                         Match.Groups[1].Value == "POST" ? HTTPMethod.POST : HTTPMethod.GET,
                URL = Match.Groups[2].Value
            };

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
                            return GenericError(HTTPStatusCode._400_BadRequest);
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

            if (Req.Handler == null)
                return GenericError(HTTPStatusCode._404_NotFound);

            if (Req.Method == HTTPMethod.POST)
            {
                // Some validity checks
                if (Req.Headers.ContentLength == null)
                    return GenericError(HTTPStatusCode._411_LengthRequired);
                if (Req.Headers.ContentLength.Value > Opt.MaxSizePostContent)
                    return GenericError(HTTPStatusCode._413_RequestEntityTooLarge);

                // Read the contents of the POST request
                if (Req.Headers.ContentLength.Value < Opt.UseFileUploadAtSize)
                {
                    if (ContentLengthSoFar >= Req.Headers.ContentLength.Value)
                        Req.Content = new MemoryStream(BufferWithContentSoFar, ContentOffset, Req.Headers.ContentLength.Value, false);
                    else
                    {
                        // Receive the POST request content into an in-memory buffer
                        byte[] Buffer = new byte[Req.Headers.ContentLength.Value];
                        if (ContentLengthSoFar > 0)
                            Array.Copy(BufferWithContentSoFar, ContentOffset, Buffer, 0, ContentLengthSoFar);
                        while (ContentLengthSoFar < Req.Headers.ContentLength)
                        {
                            SocketError ErrorCode;
                            int BytesReceived = Socket.Receive(Buffer, ContentLengthSoFar, Req.Headers.ContentLength.Value - ContentLengthSoFar, SocketFlags.None, out ErrorCode);
                            if (ErrorCode != SocketError.Success)
                                throw new SocketException();
                            ContentLengthSoFar += BytesReceived;
                        }
                        Req.Content = new MemoryStream(Buffer, 0, Req.Headers.ContentLength.Value);
                    }
                }
                else
                {
                    // Store the POST request content in a temporary file
                    Random r = new Random();
                    int Counter = r.Next(1000);
                    string Dir = Opt.TempDir + (Opt.TempDir.EndsWith(Path.DirectorySeparatorChar.ToString()) ? "" : Path.DirectorySeparatorChar.ToString());
                    FileStream f;
                    string Filename;
                    // This seemingly bizarre construct tries to prevent race conditions between several threads trying to create the same file.
                    while (true)
                    {
                        if (Counter > 100000)
                            return GenericError(HTTPStatusCode._500_InternalServerError);
                        try
                        {
                            Filename = Opt.TempDir + "http_upload_" + Counter;
                            f = File.Open(Filename, FileMode.CreateNew, FileAccess.Write);
                            break;
                        }
                        catch (IOException)
                        {
                            Counter += r.Next(1000);
                        }
                    }
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
                    Req.Content = File.Open(Filename, FileMode.Open, FileAccess.Read, FileShare.Read);
                }
            }

            HTTPResponse Resp = Req.Handler(Req);
            Resp.OriginalRequest = Req;
            return Resp;
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
            else if (HeaderName == "accept-ranges" && ValueLower == "bytes")
                Req.Headers.AcceptRanges = HTTPAcceptRanges.Bytes;
            else if (HeaderName == "connection" && ValueLower == "close")
                Req.Headers.Connection = HTTPConnection.Close;
            else if (HeaderName == "connection" && (ValueLower == "keep-alive" || ValueLower == "keepalive"))
                Req.Headers.Connection = HTTPConnection.KeepAlive;
            else if (HeaderName == "content-length" && int.TryParse(HeaderValue, out IntOutput))
                Req.Headers.ContentLength = IntOutput;
            else if (HeaderName == "content-type")
            {
                if (Req.Method == HTTPMethod.POST && ValueLower != "application/x-www-form-urlencoded" && ValueLower != "multipart/form-data")
                    throw new InvalidRequestException(GenericError(HTTPStatusCode._501_NotImplemented));
                Req.Headers.ContentType = ValueLower;
            }
            else if (HeaderName == "cookie")
            {
                Cookie PrevCookie = new Cookie() { Name = null };
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
                        try
                        {
                            PrevCookie.Expires = DateTime.Parse(Value);
                            Req.Headers.Cookie[PrevCookie.Name] = PrevCookie;
                        }
                        catch { }   // just ignore invalid Expires specs
                    }
                    else
                    {
                        PrevCookie = new Cookie() { Name = Key, Value = Value };
                        Req.Headers.Cookie[Key] = PrevCookie;
                    }
                }
            }
            else if (HeaderName == "host")
            {
                // Can't have more than one "Host" header
                if (Req.Headers.Host != null)
                    throw new InvalidRequestException(GenericError(HTTPStatusCode._400_BadRequest));

                // For performance reasons, we check if we have a handler for this domain/URL as soon as possible.
                // If we find out that we don't, stop processing here and immediately output an error
                if (Req.URL.StartsWith("/$/"))
                    Req.Handler = InternalHandler;
                else
                {
                    string Host = HeaderValue.ToLowerInvariant();
                    if (Host[Host.Length - 1] == '.')
                        Host = Host.Remove(Host.Length - 1);
                    string URL = Req.URL.Contains('?') ? Req.URL.Remove(Req.URL.IndexOf('?')) : Req.URL;
                    while (Req.Handler == null)
                    {
                        if (RequestHandlers.ContainsKey(Host + URL))
                        {
                            Req.RestURL = Req.URL.Substring(URL.Length);
                            Req.Handler = RequestHandlers[Host + URL];
                            break;
                        }
                        if (!URL.Contains('/'))
                            break;
                        URL = URL.Remove(URL.LastIndexOf('/'));
                    }
                    while (Req.Handler == null)
                    {
                        if (RequestHandlers.ContainsKey(Host))
                        {
                            Req.Handler = RequestHandlers[Host];
                            break;
                        }
                        if (!Host.Contains('.'))
                            break;
                        Host = Host.Substring(Host.IndexOf('.') + 1);
                    }
                    if (Req.Handler == null)
                        throw new InvalidRequestException(GenericError(HTTPStatusCode._404_NotFound));
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
                return new HTTPResponse()
                {
                    Headers = new HTTPResponseHeaders() { ContentType = "application/xml; charset=utf-8" },
                    Content = new MemoryStream(HTTPInternalObjects.DirectoryListingXSL())
                };
            }
            else if (Req.URL.StartsWith("/$/directory-listing/icons/"))
            {
                string Rest = Req.URL.Substring(27);
                return new HTTPResponse()
                {
                    Headers = new HTTPResponseHeaders() { ContentType = "image/png" },
                    Content = new MemoryStream(HTTPInternalObjects.GetDirectoryListingIcon(Rest))
                };
            }

            return GenericError(HTTPStatusCode._404_NotFound);
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

        public static HTTPResponse GenericError(HTTPStatusCode StatusCode)
        {
            return GenericError(StatusCode, new HTTPResponseHeaders(), null);
        }
        public static HTTPResponse GenericError(HTTPStatusCode StatusCode, string Message)
        {
            return GenericError(StatusCode, new HTTPResponseHeaders(), Message);
        }
        public static HTTPResponse GenericError(HTTPStatusCode StatusCode, HTTPResponseHeaders Headers)
        {
            return GenericError(StatusCode, Headers, null);
        }
        public static HTTPResponse GenericError(HTTPStatusCode StatusCode, HTTPResponseHeaders Headers, string Message)
        {
            string StatusCodeName = ("" + ((int) StatusCode) + " " + GetStatusCodeName(StatusCode)).HTMLEscape();
            Headers.ContentType = "text/html; charset=utf-8";
            string ContentStr = "<html><head><title>HTTP " + StatusCodeName + "</title></head><body><h1>" + StatusCodeName + "</h1>" +
                (Message != null ? "<p>" + Message.HTMLEscape() + "</p>" : "") + "</body></html>";
            return new HTTPResponse()
            {
                Status = StatusCode,
                Headers = Headers,
                Content = new MemoryStream(ContentStr.ToUTF8())
            };
        }

        private static string GetStatusCodeName(HTTPStatusCode StatusCode)
        {
            if (StatusCode == HTTPStatusCode._100_Continue) return "Continue";
            if (StatusCode == HTTPStatusCode._101_SwitchingProtocols) return "Switching Protocols";
            if (StatusCode == HTTPStatusCode._200_OK) return "OK";
            if (StatusCode == HTTPStatusCode._201_Created) return "Created";
            if (StatusCode == HTTPStatusCode._202_Accepted) return "Accepted";
            if (StatusCode == HTTPStatusCode._203_NonAuthoritativeInformation) return "Non-Authoritative Information";
            if (StatusCode == HTTPStatusCode._204_NoContent) return "No Content";
            if (StatusCode == HTTPStatusCode._205_ResetContent) return "Reset Content";
            if (StatusCode == HTTPStatusCode._206_PartialContent) return "Partial Content";
            if (StatusCode == HTTPStatusCode._300_MultipleChoices) return "Multiple Choices";
            if (StatusCode == HTTPStatusCode._301_MovedPermanently) return "Moved Permanently";
            if (StatusCode == HTTPStatusCode._302_Found) return "Found";
            if (StatusCode == HTTPStatusCode._303_SeeOther) return "See Other";
            if (StatusCode == HTTPStatusCode._304_NotModified) return "Not Modified";
            if (StatusCode == HTTPStatusCode._305_UseProxy) return "Use Proxy";
            if (StatusCode == HTTPStatusCode._306__Unused) return "(Unused)";
            if (StatusCode == HTTPStatusCode._307_TemporaryRedirect) return "Temporary Redirect";
            if (StatusCode == HTTPStatusCode._400_BadRequest) return "Bad Request";
            if (StatusCode == HTTPStatusCode._401_Unauthorized) return "Unauthorized";
            if (StatusCode == HTTPStatusCode._402_PaymentRequired) return "Payment Required";
            if (StatusCode == HTTPStatusCode._403_Forbidden) return "Forbidden";
            if (StatusCode == HTTPStatusCode._404_NotFound) return "Not Found";
            if (StatusCode == HTTPStatusCode._405_MethodNotAllowed) return "Method Not Allowed";
            if (StatusCode == HTTPStatusCode._406_NotAcceptable) return "Not Acceptable";
            if (StatusCode == HTTPStatusCode._407_ProxyAuthenticationRequired) return "Proxy Authentication Required";
            if (StatusCode == HTTPStatusCode._408_RequestTimeout) return "Request Timeout";
            if (StatusCode == HTTPStatusCode._409_Conflict) return "Conflict";
            if (StatusCode == HTTPStatusCode._410_Gone) return "Gone";
            if (StatusCode == HTTPStatusCode._411_LengthRequired) return "Length Required";
            if (StatusCode == HTTPStatusCode._412_PreconditionFailed) return "Precondition Failed";
            if (StatusCode == HTTPStatusCode._413_RequestEntityTooLarge) return "Request Entity Too Large";
            if (StatusCode == HTTPStatusCode._414_RequestURITooLong) return "Request URI Too Long";
            if (StatusCode == HTTPStatusCode._415_UnsupportedMediaType) return "Unsupported Media Type";
            if (StatusCode == HTTPStatusCode._416_RequestedRangeNotSatisfiable) return "Requested Range Not Satisfiable";
            if (StatusCode == HTTPStatusCode._417_ExpectationFailed) return "Expectation Failed";
            if (StatusCode == HTTPStatusCode._500_InternalServerError) return "Internal Server Error";
            if (StatusCode == HTTPStatusCode._501_NotImplemented) return "Not Implemented";
            if (StatusCode == HTTPStatusCode._502_BadGateway) return "Bad Gateway";
            if (StatusCode == HTTPStatusCode._503_ServiceUnavailable) return "Service Unavailable";
            if (StatusCode == HTTPStatusCode._504_GatewayTimeout) return "Gateway Timeout";
            if (StatusCode == HTTPStatusCode._505_HTTPVersionNotSupported) return "HTTP Version Not Supported";
            return "Unknown Error";
        }

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
