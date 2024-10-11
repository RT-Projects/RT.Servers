using System;
using System.Collections.Generic;
using System.IO;
using RT.Serialization;

namespace RT.Servers
{
    /// <summary>Contains configuration settings for a <see cref="FileSystemHandler"/>.</summary>
    [Serializable]
    public class FileSystemOptions
    {
        /// <summary>
        ///     Maps from file extension to MIME type. Use the key "*" to specify a default (fallback) MIME type. Use the
        ///     value "detect" to specify that <see cref="FileSystemHandler"/> should examine the file and decide between
        ///     "text/plain; charset=utf-8" and "application/octet-stream", depending on whether the file is text or binary.</summary>
        public Dictionary<string, string> MimeTypeOverrides;

        /// <summary>Returns a default MIME type for the specified extension.</summary>
        public static string GetDefaultMimeType(string extension)
        {
            return extension switch
            {
                // Plain text and data formats
                "txt" => "text/plain; charset=utf-8",
                "csv" => "text/csv; charset=utf-8",
                "json" => "application/json; charset=utf-8",

                // HTML and dependencies
                "htm" => "text/html; charset=utf-8",
                "html" => "text/html; charset=utf-8",
                "css" => "text/css; charset=utf-8",
                "js" => "text/javascript; charset=utf-8",

                // XML and stuff
                "xhtml" => "application/xhtml+xml; charset=utf-8",
                "xml" => "application/xml; charset=utf-8",
                "xsl" => "application/xml; charset=utf-8",

                // Images
                "gif" => "image/gif",
                "png" => "image/png",
                "jp2" => "image/jp2",
                "jpg" => "image/jpeg",
                "jpeg" => "image/jpeg",
                "bmp" => "image/bmp",
                "svg" => "image/svg+xml",
                "ico" => "image/x-icon",

                // Audio
                "mp3" => "audio/mpeg",
                "ogg" => "audio/ogg",
                "oga" => "audio/ogg",
                "wav" => "audio/wav",

                // Video
                "avi" => "video/x-msvideo",
                "divx" => "video/divx",
                "flv" => "video/flv",
                "m2ts" => "video/mp2t",
                "ts" => "video/mp2t",
                "m4v" => "video/m4v",
                "mkv" => "video/x-matroska",
                "mov" => "video/mov",
                "mp4" => "video/mp4",
                "mpeg" => "video/mpeg",
                "mpg" => "video/mpeg",
                "mts" => "video/mp2t",
                "ogv" => "video/ogg",
                "webm" => "video/webm",
                "wmv" => "video/wmv",

                // Fonts
                "ttf" => "font/ttf",
                "otf" => "font/otf",
                "sfnt" => "font/sfnt",
                "woff" => "font-woff",
                "woff2" => "font/woff2",
                "eot" => "application/vnd.ms-fontobject",

                // Etc.
                "pdf" => "application/pdf",
                "wasm" => "application/wasm",

                _ => null,
            };
        }

        /// <summary>Returns the MIME type for the specified local file.</summary>
        public virtual string GetMimeType(string localFilePath)
        {
            var extension = Path.GetExtension(localFilePath);
            if (extension.Length > 1)
                extension = extension.Substring(1).ToLowerInvariant();

            if (MimeTypeOverrides != null && MimeTypeOverrides.TryGetValue(extension, out var mime))
                return mime;

            return GetDefaultMimeType(extension);
        }

        /// <summary>
        ///     Specifies which way directory listings should be generated. Default is <see
        ///     cref="RT.Servers.DirectoryListingStyle.XmlPlusXsl"/>.</summary>
        public DirectoryListingStyle DirectoryListingStyle = DirectoryListingStyle.XmlPlusXsl;

        /// <summary>
        ///     If directory listings are permitted, this handler is invoked to confirm that listing this specific directory
        ///     is permitted. The handler should return <c>null</c> to allow directory listing, or an appropriate response if
        ///     listing is not allowed. A null handler is identical to a handler that always returns null.</summary>
        [ClassifyIgnore]
        public Func<HttpRequest, HttpResponse> DirectoryListingAuth = null;

        /// <summary>
        ///     Specifies the value for the CacheControl max-age header on the files served by the file system handler, in
        ///     seconds. Set to null to prevent this header being sent, which will result in the browser caching the file
        ///     indefinitely. The If-Modified-Since mechanism is always used regardless of this setting.</summary>
        public int? MaxAge = 3600;

        /// <summary>Specifies a method to modify response headers before they go out to the client.</summary>
        public Action<HttpResponseHeaders, FileSystemResponseType> ResponseHeaderProcessor = null;
    }

    /// <summary>
    ///     Controls which style of directory listing should be used by <see cref="FileSystemHandler"/> to list the contents
    ///     of directories.</summary>
    public enum DirectoryListingStyle
    {
        /// <summary>Specifies that directory listing is forbidden (returns a 401 Unauthorised error).</summary>
        Forbidden,
        /// <summary>Specifies a directory style that uses an XML file with an XSL style sheet.</summary>
        XmlPlusXsl
    }

    /// <summary>Identifies a type of response generated by <see cref="FileSystemHandler"/>.</summary>
    public enum FileSystemResponseType
    {
        /// <summary>The response serves a file from the local file system.</summary>
        File,
        /// <summary>The response generates a directory listing.</summary>
        Directory,
        /// <summary>The response is a redirect (usually for adjusting capitalization or expanding wildcards).</summary>
        Redirect,
        /// <summary>The response serves an internal file as part of a directory listing, such as the XSL template or an icon.</summary>
        Internal
    }
}
