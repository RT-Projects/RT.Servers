using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Servers
{
    public delegate HTTPResponse HTTPRequestHandler(HTTPRequest Request);

    public struct HTTPRequestHeaders
    {
        public string[] Accept;
        public string[] AcceptCharset;
        public HTTPContentEncoding[] AcceptEncoding;
        public string[] AcceptLanguage;
        public HTTPConnection Connection;
        public long? ContentLength;                 // required only for POST
        public HTTPPOSTContentType ContentType;     // required only for POST
        public string ContentMultipartBoundary;     // required only for POST and only if ContentType == HTTPPOSTContentType.ApplicationXWWWFormURLEncoded
        public Dictionary<string, Cookie> Cookie;
        public string Host;
        public DateTime? IfModifiedSince;
        public string IfNoneMatch;
        public HTTPRange[] Range;
        public string UserAgent;
        public Dictionary<string, string> UnrecognisedHeaders;
    }

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

        public string RestURL;  // the part of the URL that follows the path where the handler is hooked
        public HTTPMethod Method;
        public HTTPRequestHeaders Headers;
        public Stream Content;

        internal HTTPRequestHandler Handler;  // used only internally
        public string TempDir;

        public string URL
        {
            get { return _URL; }
            set { _URL = value; GETFieldsCache = new FieldsCache(); POSTFieldsCache = new FieldsCache(); }
        }
        public string URLWithoutQuery
        {
            get { return _URL.Contains('?') ? _URL.Remove(_URL.IndexOf('?')) : _URL; }
        }
        public string RestURLWithoutQuery
        {
            get { return RestURL.Contains('?') ? RestURL.Remove(RestURL.IndexOf('?')) : RestURL; }
        }

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
            FieldsCache fc = new FieldsCache()
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
                                fc.FileCache[CurrentFieldName] = new FileUpload()
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
                    if (BytesRead == 0)
                        break;
                    WriteIndex += BytesRead;
                } while (WriteIndex < Headers.ContentMultipartBoundary.Length + 8);
                BytesRead = WriteIndex;
            }

            return fc;
        }

        private FieldsCache ParseQueryParameters(Stream s)
        {
            FieldsCache fc = new FieldsCache()
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

    public class InvalidRequestException : Exception
    {
        public HTTPResponse Response;
        public InvalidRequestException(HTTPResponse Response) { this.Response = Response; }
    }
}
