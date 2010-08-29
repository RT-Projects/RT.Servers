using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RT.Servers
{
    /// <summary>Contains configuration settings for a <see cref="FileSystemHandler"/>.</summary>
    [Serializable]
    public class FileSystemOptions
    {
        /// <summary>Maps from file extension to MIME type. Use the key "*" to specify a default (fallback) MIME type.
        /// Use the value "detect" to specify that <see cref="FileSystemHandler"/> should examine the file and decide between
        /// "text/plain; charset=utf-8" and "application/octet-stream", depending on whether the file is text or binary.</summary>
        public Dictionary<string, string> MimeTypes = new Dictionary<string, string>
        {
            // Plain text
            { "txt", "text/plain; charset=utf-8" },
            { "csv", "text/csv; charset=utf-8" },

            // HTML and dependancies
            { "htm", "text/html; charset=utf-8" },
            { "html", "text/html; charset=utf-8" },
            { "css", "text/css; charset=utf-8" },
            { "js", "text/javascript; charset=utf-8" },

            // XML and stuff
            { "xhtml", "application/xhtml+xml; charset=utf-8" },
            { "xml", "application/xml; charset=utf-8" },
            { "xsl", "application/xml; charset=utf-8" },

            // Images
            { "gif", "image/gif" },
            { "png", "image/png" },
            { "jp2", "image/jp2" },
            { "jpg", "image/jpeg" },
            { "jpeg", "image/jpeg" },
            { "bmp", "image/bmp" },

            // Default
            { "*", "detect" }
        };

        /// <summary>
        /// Specifies which way directory listings should be generated. Default is <see cref="RT.Servers.DirectoryListingStyle.XmlPlusXsl"/>.
        /// </summary>
        public DirectoryListingStyle DirectoryListingStyle = DirectoryListingStyle.XmlPlusXsl;
    }

    /// <summary>Controls which style of directory listing should be used by <see cref="FileSystemHandler"/> to list the contents of directories.</summary>
    public enum DirectoryListingStyle
    {
        /// <summary>Specifies a directory style that uses an XML file with an XSL style sheet.</summary>
        XmlPlusXsl
    }
}
