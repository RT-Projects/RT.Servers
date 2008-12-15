using System.Collections.Generic;
using System.IO;
using RT.Util;
using RT.Util.ExtensionMethods;

namespace RT.Servers
{
    /// <summary>
    /// Internal class containing global static objects.
    /// </summary>
    public static class HttpInternalObjects
    {
        /// <summary>
        /// XSL to use for directory listings. This will be converted to UTF-8, whitespace-optimised and cached before being output.
        /// This is the file that is returned at the URL /$/directory-listing/xsl.
        /// </summary>
        private static string _directoryListingXslString = @"<?xml version=""1.0"" encoding=""UTF-8""?>

            <xsl:stylesheet version=""1.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"" xmlns=""http://www.w3.org/1999/xhtml"">
                <xsl:output method=""html""/>
                <xsl:template match=""/directory"">
                    <html>
                    <head>
                        <title>Directory listing</title>
                        <style type=""text/css"">
                            body { margin: 10pt; font-family: ""Verdana"", sans-serif; font-size: 10pt; }
                            table { border-collapse: collapse; }
                            .line { border-top: 2px solid #339; }
                            th { padding: 3pt 0; }
                            .filename { padding-right: 5pt; border-right: 1px solid #ccc; }
                            .size { padding: 0 5pt; }
                            .size.dir { text-align: center; font-style: italic; }
                            .size.file { text-align: right; }
                            .filerow:hover { background: #eef; }
                            .filerow .filename a { display: block; }
                            .summary { font-size: 80%; font-style: italic; color: #888; }
                        </style>
                    </head>
                    <body>
                        <table>
                            <tr class=""line"">
                                <table>
                                    <tr>
                                        <td rowspan=""2""><img src=""{@img}"" alt=""folder""/></td>
                                        <td style=""font-size: 180%"">Directory listing</td>
                                    </tr>
                                    <tr>
                                        <td><span title=""{@url}""><xsl:value-of select=""@unescapedurl""/></span></td>
                                    </tr>
                                </table>
                            </tr>

                            <tr class=""line""><td colspan=""2"">
                                <table style=""width: 100%"">
                                    <tr><th class=""filename"" colspan=""2"">Name</th><th>Size</th></tr>
                                    <xsl:if test=""@url!='/'"">
                                        <tr class=""filerow""><td></td><td class=""filename""><a href="".."">..</a></td><td class=""size dir"">Folder</td></tr>
                                    </xsl:if>
                                    <xsl:apply-templates select=""dir"" />
                                    <xsl:apply-templates select=""file"" />
                                    <tr style=""height: 3pt""><td colspan=""2""/><td/></tr>
                                </table>
                            </td></tr>

                            <tr class=""line summary"">
                                <td colspan=""2"">Folder contains <xsl:value-of select=""@numdirs""/> sub-folders and <xsl:value-of select=""@numfiles""/> files.</td>
                            </tr>
                        </table>
                    </body>
                    </html>
                </xsl:template>

                <xsl:template match=""dir"">
                    <tr class=""filerow"">
                        <td><img src=""{@img}"" alt=""folder""/></td>
                        <td class=""filename""><a href=""{@link}""><xsl:value-of select="".""/></a></td>
                        <td class=""size dir"">Folder</td>
                    </tr>
                </xsl:template>

                <xsl:template match=""file"">
                    <tr class=""filerow"">
                        <td><img src=""{@img}"" alt=""file""/></td>
                        <td class=""filename""><a href=""{@link}""><xsl:value-of select="".""/></a></td>
                        <td class=""size file""><xsl:value-of select=""@nicesize""/></td>
                    </tr>
                </xsl:template>

            </xsl:stylesheet>
        ";

        /// <summary>
        /// Caches the UTF-8-encoded version of the directory-listing XSL file.
        /// </summary>
        private static byte[] _directoryListingXslByteArray = null;

        /// <summary>
        /// Contains string representations of the HTTP status codes defined in HTTP/1.1.
        /// </summary>
        public static Dictionary<HttpStatusCode, string> StatusCodeNames = new Dictionary<HttpStatusCode, string>
        {
            { HttpStatusCode._100_Continue, "Continue" },
            { HttpStatusCode._101_SwitchingProtocols, "Switching Protocols" },
            { HttpStatusCode._200_OK, "OK" },
            { HttpStatusCode._201_Created, "Created" },
            { HttpStatusCode._202_Accepted, "Accepted" },
            { HttpStatusCode._203_NonAuthoritativeInformation, "Non-Authoritative Information" },
            { HttpStatusCode._204_NoContent, "No Content" },
            { HttpStatusCode._205_ResetContent, "Reset Content" },
            { HttpStatusCode._206_PartialContent, "Partial Content" },
            { HttpStatusCode._300_MultipleChoices, "Multiple Choices" },
            { HttpStatusCode._301_MovedPermanently, "Moved Permanently" },
            { HttpStatusCode._302_Found, "Found" },
            { HttpStatusCode._303_SeeOther, "See Other" },
            { HttpStatusCode._304_NotModified, "Not Modified" },
            { HttpStatusCode._305_UseProxy, "Use Proxy" },
            { HttpStatusCode._306__Unused, "(Unused)" },
            { HttpStatusCode._307_TemporaryRedirect, "Temporary Redirect" },
            { HttpStatusCode._400_BadRequest, "Bad Request" },
            { HttpStatusCode._401_Unauthorized, "Unauthorized" },
            { HttpStatusCode._402_PaymentRequired, "Payment Required" },
            { HttpStatusCode._403_Forbidden, "Forbidden" },
            { HttpStatusCode._404_NotFound, "Not Found" },
            { HttpStatusCode._405_MethodNotAllowed, "Method Not Allowed" },
            { HttpStatusCode._406_NotAcceptable, "Not Acceptable" },
            { HttpStatusCode._407_ProxyAuthenticationRequired, "Proxy Authentication Required" },
            { HttpStatusCode._408_RequestTimeout, "Request Timeout" },
            { HttpStatusCode._409_Conflict, "Conflict" },
            { HttpStatusCode._410_Gone, "Gone" },
            { HttpStatusCode._411_LengthRequired, "Length Required" },
            { HttpStatusCode._412_PreconditionFailed, "Precondition Failed" },
            { HttpStatusCode._413_RequestEntityTooLarge, "Request Entity Too Large" },
            { HttpStatusCode._414_RequestURITooLong, "Request URI Too Long" },
            { HttpStatusCode._415_UnsupportedMediaType, "Unsupported Media Type" },
            { HttpStatusCode._416_RequestedRangeNotSatisfiable, "Requested Range Not Satisfiable" },
            { HttpStatusCode._417_ExpectationFailed, "Expectation Failed" },
            { HttpStatusCode._500_InternalServerError, "Internal Server Error" },
            { HttpStatusCode._501_NotImplemented, "Not Implemented" },
            { HttpStatusCode._502_BadGateway, "Bad Gateway" },
            { HttpStatusCode._503_ServiceUnavailable, "Service Unavailable" },
            { HttpStatusCode._504_GatewayTimeout, "Gateway Timeout" },
            { HttpStatusCode._505_HttpVersionNotSupported, "HTTP Version Not Supported" }
        };

        /// <summary>
        /// Generates a random filename for a temporary file in the specified directory.
        /// </summary>
        /// <param name="tempDir">Directory to generate a temporary file in.</param>
        /// <param name="fStream">Will be set to a writable FileStream pointing at the newly-created, empty temporary file.</param>
        /// <returns>The full path and filename of the temporary file.</returns>
        public static string RandomTempFilepath(string tempDir, out Stream fStream)
        {
            string dir = tempDir + (tempDir.EndsWith(Path.DirectorySeparatorChar.ToString()) ? "" : Path.DirectorySeparatorChar.ToString());
            lock (Ut.Rnd)
            {
                int counter = Ut.Rnd.Next(1000);
                // This seemingly bizarre construct tries to prevent race conditions between several threads/processes trying to create the same file.
                while (true)
                {
                    if (counter > 100000)
                        throw new IOException("Could not generate a new temporary filename in the directory " + tempDir +
                            ". Make sure that the directory exists. You may need to clear out this directory if it is full.");
                    try
                    {
                        string filepath = dir + "http_upload_" + counter;
                        fStream = File.Open(filepath, FileMode.CreateNew, FileAccess.Write);
                        return filepath;
                    }
                    catch (IOException)
                    {
                        counter += Ut.Rnd.Next(1000);
                    }
                }
            }
        }

        /// <summary>
        /// Returns a byte array containing the UTF-8-encoded directory-listing XSL.
        /// </summary>
        /// <returns>A byte array containing the UTF-8-encoded directory-listing XSL.</returns>
        public static byte[] DirectoryListingXsl()
        {
            if (_directoryListingXslByteArray == null)
            {
                _directoryListingXslByteArray = _directoryListingXslString.ToUtf8();
                _directoryListingXslString = null; // free some memory?
            }
            return _directoryListingXslByteArray;
        }

        /// <summary>
        /// Returns a byte array representing an icon in PNG format that corresponds to the specified file extension.
        /// </summary>
        /// <param name="extension">The file extension for which to retrieve an icon in PNG format.</param>
        /// <returns>A byte array representing an icon in PNG format that corresponds to the specified file extension.</returns>
        public static byte[] GetDirectoryListingIcon(string extension)
        {
            if (extension == "folder") return Resources.folder_16;
            if (extension == "folderbig") return Resources.folder_48;

            if (extension == "bmp") return Resources.bmp_16;
            if (extension == "csv") return Resources.csv_16;
            if (extension == "doc") return Resources.doc_16;
            if (extension == "exe") return Resources.exe_16;
            if (extension == "faq") return Resources.faq_16;
            if (extension == "gz") return Resources.zip_16;
            if (extension == "jpg") return Resources.jpg_16;
            if (extension == "pdf") return Resources.pdf_16;
            if (extension == "pic") return Resources.pic_16;
            if (extension == "png") return Resources.png_16;
            if (extension == "pps") return Resources.pps_16;
            if (extension == "ppt") return Resources.ppt_16;
            if (extension == "txt") return Resources.txt_16;
            if (extension == "xls") return Resources.xls_16;
            if (extension == "zip") return Resources.zip_16;
            if (extension == "rar") return Resources.gz_16;

            return Resources.txt_16;
        }

        /// <summary>
        /// Returns a file extension whose icon is used in directory listings to represent files of the specified file extension.
        /// </summary>
        /// <param name="extension">The extension of the actual file for which to display an icon.</param>
        /// <returns>The file extension whose icon is used in directory listings to represent files of the specified file extension.</returns>
        public static string GetDirectoryListingIconStr(string extension)
        {
            if (extension == "folder") return extension;
            if (extension == "folderbig") return extension;

            if (extension == "bmp") return extension;
            if (extension == "csv") return extension;
            if (extension == "doc") return extension;
            if (extension == "exe") return extension;
            if (extension == "faq") return extension;
            if (extension == "gz") return "zip";
            if (extension == "jpg") return extension;
            if (extension == "pdf") return extension;
            if (extension == "pic") return extension;
            if (extension == "png") return extension;
            if (extension == "pps") return extension;
            if (extension == "ppt") return extension;
            if (extension == "txt") return extension;
            if (extension == "xls") return extension;
            if (extension == "zip") return extension;
            if (extension == "rar") return extension;

            return "txt";
        }
    }
}
