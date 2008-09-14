using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.IO;

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
                    SoFar += Path.DirectorySeparatorChar + URLPieces[i];
                    SoFarURL += "/" + URLPieces[i];
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
                return new HTTPResponse() { Headers = new HTTPResponseHeaders() { Location = Req.URL + "/" }, Status = HTTPStatusCode._301_MovedPermanently };

            return new HTTPResponse()
            {
                Headers = new HTTPResponseHeaders() { ContentType = "text/html" },
                Status = HTTPStatusCode._200_OK,
                Content = new DynamicContentStream(DynamicDirectoryHandler(p + SoFar, BaseURL + SoFarURL))
            };
        }

        public static IEnumerable<string> DynamicDirectoryHandler(string LocalPath, string URL)
        {
            yield return "<html>";
            yield return "  <head>";
            yield return "    <title>" + URL + " - Directory Listing</title>";
            yield return "  </head>";
            yield return "  <body>";
            yield return "  </body>";
            yield return "</html>";
            yield break;
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
                int BytesReceived = Socket.Receive(Buffer, 0, 65536, SocketFlags.None, out ErrorCode);

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
                    HTTPResponse Response = HandleRequestAfterHeaders(Socket, Headers, Buffer, i + 4, BytesReceived - i - 4);
                    try
                    {
                        try
                        {
                            OutputResponse(Socket, Response);
                        }
                        finally
                        {
                            if (Response.Content != null)
                                Response.Content.Close();
                        }
                        if (Response.Headers.Connection == HTTPConnection.KeepAlive && Socket.Connected)
                        {
                            HeadersSoFar = "";
                            continue;
                        }
                    }
                    catch (SocketException)
                    {
                    }
                    Socket.Close();
                    return;
                }
            }
        }

        private void OutputResponse(Socket Socket, HTTPResponse Response)
        {
            if (Response.Content == null)
                Response.Headers.ContentLength = 0;
            else if (Response.Headers.ContentLength == null)
            {
                // If we can deduce the length from the stream, supply it
                try { Response.Headers.ContentLength = Response.Content.Length; }
                catch (NotSupportedException) { Response.Headers.TransferEncoding = HTTPTransferEncoding.Chunked; }
            }
            string HeadersStr = "HTTP/1.1 " + ((int) Response.Status) + " " + GetStatusCodeName(Response.Status) + "\r\n" +
                Response.Headers.ToString() + "\r\n";
            Console.WriteLine(HeadersStr);
            Socket.Send(Encoding.ASCII.GetBytes(HeadersStr));

            if (Response.Headers.ContentLength != null && Response.Headers.ContentLength.Value == 0)
                return;

            byte[] Buffer = new byte[65536];
            int BytesRead = Response.Content.Read(Buffer, 0, 65536);
            while (BytesRead > 0)
            {
                if (Response.Headers.TransferEncoding == HTTPTransferEncoding.Chunked)
                    Socket.Send(Encoding.ASCII.GetBytes(BytesRead.ToString("X") + "\r\n"));
                Socket.Send(Buffer, BytesRead, SocketFlags.None);
                if (Response.Headers.TransferEncoding == HTTPTransferEncoding.Chunked)
                    Socket.Send(new byte[] { 13, 10 }); // "\r\n"
                BytesRead = Response.Content.Read(Buffer, 0, 65536);
            }
            if (Response.Headers.TransferEncoding == HTTPTransferEncoding.Chunked)
                Socket.Send(new byte[] { (byte) '0', 13, 10, 13, 10 }); // "0\r\n\r\n"
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
            if (Req.Method == HTTPMethod.POST && Req.Headers.ContentLength == null)
                return GenericError(HTTPStatusCode._411_LengthRequired);
            if (Req.Method == HTTPMethod.POST && Req.Headers.ContentLength.Value > Opt.MaxSizePostContent)
                return GenericError(HTTPStatusCode._413_RequestEntityTooLarge);
            if (Req.Handler == null)
                return GenericError(HTTPStatusCode._404_NotFound);

            if (Req.Method == HTTPMethod.POST && Req.Headers.ContentLength.Value < Opt.UseFileUploadAtSize)
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
            else if (Req.Method == HTTPMethod.POST)
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

            return Req.Handler(Req);
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
                if (Req.Method == HTTPMethod.POST || ValueLower != "application/x-www-form-urlencoded")
                    throw new InvalidRequestException(GenericError(HTTPStatusCode._501_NotImplemented));
                Req.Headers.ContentType = ValueLower;
            }
            else if (HeaderName == "cookie")
            {
                Cookie Cookie = new Cookie();
                string[] Params = Regex.Split(HeaderValue, @"\s*;\s*");
                for (int i = 0; i < Params.Length; i++)
                {
                    DateTime Output;
                    string[] KeyValue = Regex.Split(Params[i], @"\s*=\s*");
                    if (i == 0 && KeyValue.Length > 1)
                    {
                        Cookie.Name = KeyValue[0];
                        Cookie.Value = KeyValue[1];
                    }
                    else if (KeyValue[0].ToLowerInvariant() == "path" && KeyValue.Length > 1)
                        Cookie.Path = KeyValue[1];
                    else if (KeyValue[0].ToLowerInvariant() == "domain" && KeyValue.Length > 1)
                        Cookie.Domain = KeyValue[1].ToLowerInvariant();
                    else if (KeyValue[0].ToLowerInvariant() == "expires" && KeyValue.Length > 1 && DateTime.TryParse(KeyValue[1], out Output))
                        Cookie.Expires = Output;
                    else if (Params[i].ToLowerInvariant() == "httponly")
                        Cookie.HttpOnly = true;
                }
                if (Req.Headers.Cookie == null)
                    Req.Headers.Cookie = new List<Cookie>();
                Req.Headers.Cookie.Add(Cookie);
            }
            else if (HeaderName == "host")
            {
                // Can't have more than one "Host" header
                if (Req.Headers.Host != null)
                    throw new InvalidRequestException(GenericError(HTTPStatusCode._400_BadRequest));

                // For performance reasons, we check if we have a handler for this domain/URL as soon as possible.
                // If we find out that we don't, stop processing here and immediately output an error
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
            string StatusCodeName = "" + ((int) StatusCode) + " " + GetStatusCodeName(StatusCode);
            Headers.ContentType = "text/html; charset=utf-8";
            string ContentStr = "<html><head><title>HTTP " + StatusCodeName + "</title></head><body><h1>" + StatusCodeName + "</h1>" + (Message != null ? "<p>" + Message + "</p>" : "") + "</body></html>";
            byte[] ContentBuffer = Encoding.UTF8.GetBytes(ContentStr);
            return new HTTPResponse()
            {
                Status = StatusCode,
                Headers = Headers,
                Content = new MemoryStream(ContentBuffer)
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
    }
}
