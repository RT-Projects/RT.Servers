using System;
using System.IO;
using System.Security.Cryptography;
using RT.Util;

namespace RT.Servers
{
    /// <summary>
    /// Encapsulates an HTTP cookie.
    /// </summary>
    [Serializable]
    public sealed class Cookie
    {
#pragma warning disable 1591    // Missing XML comment for publicly visible type or member
        public string Name;
        public string Value;
        public string Path;
        public string Domain;
        public DateTime? Expires;
        public bool HttpOnly;
        public HttpCookieSameSite? SameSite;
        public bool Secure;
#pragma warning restore 1591    // Missing XML comment for publicly visible type or member
    }

    /// <summary>
    /// Encapsulates the possible values of the Cache-Control HTTP request or response header.
    /// </summary>
    [Serializable]
    public struct HttpCacheControl
    {
        /// <summary>Contains possible values of the Cache-Control header.</summary>
        public HttpCacheControlState State;
        /// <summary>Some values of the Cache-Control header have an integer parameter.</summary>
        public int? IntParameter;
        /// <summary>Some values of the Cache-Control header have a string parameter.</summary>
        public string StringParameter;

        /// <summary>Converts this structure to a valid element for the Cache-Control HTTP header. Throws if a required Int/String Parameter is missing.</summary>
        public override string ToString()
        {
            switch (State)
            {
                case HttpCacheControlState.MaxStale: return IntParameter == null ? "max-stale" : ("max-stale=" + IntParameter.Value);
                case HttpCacheControlState.MinFresh: return "min-fresh=" + IntParameter.Value;
                case HttpCacheControlState.OnlyIfCached: return "only-if-cached";
                case HttpCacheControlState.MustRevalidate: return "must-revalidate";
                case HttpCacheControlState.ProxyRevalidate: return "proxy-revalidate";
                case HttpCacheControlState.Public: return "public";
                case HttpCacheControlState.SMaxAge: return "s-maxage=" + IntParameter.Value;
                case HttpCacheControlState.MaxAge: return "max-age=" + IntParameter.Value;
                case HttpCacheControlState.NoCache: return StringParameter == null ? "no-cache" : ("no-cache=\"" + StringParameter + "\"");
                case HttpCacheControlState.NoStore: return "no-store";
                case HttpCacheControlState.NoTransform: return "no-transform";
                case HttpCacheControlState.Private: return StringParameter == null ? "private" : ("private=\"" + StringParameter + "\"");
                default: return null;
            }
        }

        /// <summary>Provides a ready-made cache control collection that disables the caching completely.</summary>
        public static HttpCacheControl[] NoCache { get { return new[] { new HttpCacheControl { State = HttpCacheControlState.NoCache } }; } }
    }

    /// <summary>
    /// Encapsulates the possible values of the Content-Disposition HTTP response header.
    /// </summary>
    [Serializable]
    public struct HttpContentDisposition
    {
        /// <summary>Currently supports only one value (“Attachment”).</summary>
        public HttpContentDispositionMode Mode;
        /// <summary>If Mode is “Attachment”, contains the filename of the attachment.</summary>
        public string Filename;
        /// <summary>Returns a value representing Content-Disposition: attachment, using the specified filename.</summary>
        /// <param name="filename">If null, the "filename" part is omitted from the header.</param>
        public static HttpContentDisposition Attachment(string filename = null)
        {
            return new HttpContentDisposition { Filename = filename, Mode = HttpContentDispositionMode.Attachment };
        }
    }

    /// <summary>
    /// Encapsulates the possible values of the Content-Range HTTP response header.
    /// </summary>
    [Serializable]
    public struct HttpContentRange
    {
        /// <summary>First byte index of the range. The first byte in the file has index 0.</summary>
        public long From;
        /// <summary>Last byte index of the range. For example, a range from 0 to 0 includes one byte.</summary>
        public long To;
        /// <summary>Total size of the file (not of the range).</summary>
        public long Total;
    }

    /// <summary>
    /// Encapsulates one of the ranges specified in a Range HTTP request header.
    /// </summary>
    [Serializable]
    public struct HttpRange
    {
        /// <summary>First byte index of the range. The first byte in the file has index 0.</summary>
        public long? From;
        /// <summary>Last byte index of the range. For example, a range from 0 to 0 includes one byte.</summary>
        public long? To;
    }

    /// <summary>
    /// Represents a file upload contained in an HTTP request with a body.
    /// </summary>
    [Serializable]
    public sealed class FileUpload
    {
        /// <summary>The MIME type of the uploaded file.</summary>
        public string ContentType { get; private set; }
        /// <summary>The filename of the uploaded file as supplied by the client.</summary>
        public string Filename { get; private set; }

        /// <summary>Use this if the file upload content is stored on disk.</summary>
        internal string LocalFilename;
        /// <summary>Specifies that the handler has moved the local file to a destination place. (Prevents the file from being deleted during clean-up.)</summary>
        internal bool LocalFileMoved;
        /// <summary>Use this if the file upload content is stored in memory.</summary>
        internal byte[] Data;

        /// <summary>Constructor.</summary>
        internal FileUpload(string contentType, string filename)
        {
            ContentType = contentType;
            Filename = filename;
            LocalFileMoved = false;
        }

        /// <summary>Moves the uploaded file to a file in the local file system.</summary>
        /// <remarks>Calling this method twice will move the file around, not create two copies.</remarks>
        public void SaveToFile(string localFilename)
        {
            LocalFileMoved = true;
            if (Data != null)
            {
                using (var f = File.Open(localFilename, FileMode.Create, FileAccess.Write, FileShare.Write))
                    f.Write(Data, 0, Data.Length);
                LocalFilename = localFilename;
                Data = null;
            }
            else
            {
                File.Move(LocalFilename, localFilename);
                localFilename = LocalFilename;
            }
        }

        /// <summary>Returns a Stream object for access to the file upload.</summary>
        /// <remarks>The caller is responsible for disposing of the Stream object.</remarks>
        public Stream GetStream()
        {
            if (Data != null)
                return new MemoryStream(Data, false);
            else
                return File.Open(LocalFilename, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        /// <summary>Returns the MD5 hash function of the upload contents.</summary>
        /// <returns>Result of the MD5 hash function as a string of hexadecimal digits.</returns>
        public string GetMd5()
        {
            if (Data != null)
            {
                using (var m = MD5.Create())
                    return m.ComputeHash(Data).ToHex();
            }
            else
            {
                using (var f = File.Open(LocalFilename, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var m = MD5.Create())
                    return m.ComputeHash(f).ToHex();
            }
        }
    }
}
