using System.Collections.Generic;
using System.IO;
using RT.Util;
using RT.Util.ExtensionMethods;

namespace RT.Servers
{
    /// <summary>
    /// Internal class containing global static objects.
    /// </summary>
    internal static class HttpInternalObjects
    {
        private static object lockObj = new object();

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
            { HttpStatusCode._414_RequestUriTooLong, "Request URI Too Long" },
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
            lock (lockObj)
            {
                int counter = Rnd.Next(1000);
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
                        counter += Rnd.Next(1000);
                    }
                }
            }
        }
    }
}
