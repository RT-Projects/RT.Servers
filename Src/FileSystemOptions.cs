using System;
using System.Collections.Generic;
using System.IO;
using RT.Util.Serialization;

namespace RT.Servers
{
    /// <summary>Contains configuration settings for a <see cref="FileSystemHandler"/>.</summary>
    [Serializable]
    public class FileSystemOptions
    {
        /// <summary>Maps from file extension to MIME type. Use the key "*" to specify a default (fallback) MIME type.
        /// Use the value "detect" to specify that <see cref="FileSystemHandler"/> should examine the file and decide between
        /// "text/plain; charset=utf-8" and "application/octet-stream", depending on whether the file is text or binary.</summary>
        public Dictionary<string, string> MimeTypeOverrides;

        /// <summary>Returns a default MIME type for the specified extension.</summary>
        public static string GetDefaultMimeType(string extension)
        {
            switch (extension)
            {
                // Plain text and data formats
                case "txt": return "text/plain; charset=utf-8";
                case "csv": return "text/csv; charset=utf-8";
                case "json": return "application/json; charset=utf-8";

                // HTML and dependencies
                case "htm": return "text/html; charset=utf-8";
                case "html": return "text/html; charset=utf-8";
                case "css": return "text/css; charset=utf-8";
                case "js": return "text/javascript; charset=utf-8";

                // XML and stuff
                case "xhtml": return "application/xhtml+xml; charset=utf-8";
                case "xml": return "application/xml; charset=utf-8";
                case "xsl": return "application/xml; charset=utf-8";

                // Images
                case "gif": return "image/gif";
                case "png": return "image/png";
                case "jp2": return "image/jp2";
                case "jpg": return "image/jpeg";
                case "jpeg": return "image/jpeg";
                case "bmp": return "image/bmp";
                case "svg": return "image/svg+xml";
                case "ico": return "image/x-icon";

                // Fonts
                case "ttf": return "font/ttf";
                case "otf": return "font/otf";
                case "sfnt": return "font/sfnt";
                case "woff": return "font-woff";
                case "woff2": return "font/woff2";
                case "eot": return "application/vnd.ms-fontobject";

                // Etc.
                case "pdf": return "application/pdf";
                case "mp3": return "audio/mpeg3";
                case "ogg": return "audio/ogg";

                default: return null;
            }
        }

        /// <summary>Returns the MIME type for the specified local file.</summary>
        public virtual string GetMimeType(string localFilePath)
        {
            var extension = Path.GetExtension(localFilePath);
            if (extension.Length > 1)
                extension = extension.Substring(1).ToLowerInvariant();

            string mime;
            if (MimeTypeOverrides != null && MimeTypeOverrides.TryGetValue(extension, out mime))
                return mime;

            return GetDefaultMimeType(extension);
        }

        /// <summary>
        /// Specifies which way directory listings should be generated. Default is <see cref="RT.Servers.DirectoryListingStyle.XmlPlusXsl"/>.
        /// </summary>
        public DirectoryListingStyle DirectoryListingStyle = DirectoryListingStyle.XmlPlusXsl;

        /// <summary>
        /// If directory listings are permitted, this handler is invoked to confirm that listing this specific directory is permitted. The handler should
        /// return <c>null</c> to allow directory listing, or an appropriate response if listing is not allowed. A null handler is identical to a
        /// handler that always returns null.
        /// </summary>
        [ClassifyIgnore]
        public Func<HttpRequest, HttpResponse> DirectoryListingAuth = null;

        /// <summary>
        /// Specifies the value for the CacheControl max-age header on the files served by the file system handler, in seconds. Set to null to prevent this header being
        /// sent, which will result in the browser revalidating the file on every request. The If-Modified-Since mechanism is always used regardless of this setting.
        /// </summary>
        public int? MaxAge = 3600;
    }

    /// <summary>Controls which style of directory listing should be used by <see cref="FileSystemHandler"/> to list the contents of directories.</summary>
    public enum DirectoryListingStyle
    {
        /// <summary>Specifies that directory listing is forbidden (returns a 401 Unauthorised error).</summary>
        Forbidden,
        /// <summary>Specifies a directory style that uses an XML file with an XSL style sheet.</summary>
        XmlPlusXsl
    }
}
