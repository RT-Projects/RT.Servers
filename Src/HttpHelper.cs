using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using RT.Util.ExtensionMethods;
using RT.Util;

namespace RT.Servers
{
    /// <summary>Contains helper methods to deal with various things in HTTP.</summary>
    public static class HttpHelper
    {
        internal static readonly string[] EmptyStrings = new string[0];

        /// <summary>
        ///     Parses the specified string as a URL query string. The string is expected to be either empty or start with a <c>'?'</c>.</summary>
        /// <param name="input">
        ///     The string to parse.</param>
        /// <returns>
        ///     A list containing the key/value pairs extracted from the string.</returns>
        public static List<KeyValuePair<string, string>> ParseQueryString(string input)
        {
            if (input == null)
                throw new ArgumentNullException("input");
            using (var rdr = new StringReader(input))
            {
                var c = rdr.Read();
                if (c == -1)
                    return new List<KeyValuePair<string, string>>(0);
                if (c != '?')
                    throw new ArgumentException("Query string did not start with a question mark");
                return ParseQueryValueParameters(rdr);
            }
        }

        /// <summary>
        ///     Decodes a URL-encoded stream of UTF-8 characters into key-value pairs.</summary>
        /// <param name="s">
        ///     Stream to read from.</param>
        internal static List<KeyValuePair<string, string>> ParseQueryValueParameters(TextReader s)
        {
            if (s == null)
                return new List<KeyValuePair<string, string>>(0);
            var result = new List<KeyValuePair<string, string>>(6);

            char[] buffer = new char[65536];
            int charsRead = s.Read(buffer, 0, buffer.Length);
            int bufferIndex = 0;
            string curKey = "";
            string curValue = null;

            bool inKey = true;
            while (charsRead > 0)
            {
                while (bufferIndex < charsRead)
                {
                    int i = bufferIndex;
                    while (i < charsRead && buffer[i] != '&' && buffer[i] != '=')
                        i++;
                    if (i == charsRead)
                    {
                        if (inKey)
                            curKey += new string(buffer, bufferIndex, i - bufferIndex);
                        else
                            curValue += new string(buffer, bufferIndex, i - bufferIndex);
                        bufferIndex = i;
                    }
                    else if (buffer[i] == (byte) '=')
                    {
                        if (inKey)
                        {
                            curKey += new string(buffer, bufferIndex, i - bufferIndex);
                            curValue = "";
                            inKey = false;
                        }
                        else
                            curValue += new string(buffer, bufferIndex, i - bufferIndex) + "=";
                        bufferIndex = i + 1;
                    }
                    else if (buffer[i] == (byte) '&')
                    {
                        if (inKey)
                            curKey += new string(buffer, bufferIndex, i - bufferIndex) + "&";
                        else
                        {
                            curValue += new string(buffer, bufferIndex, i - bufferIndex);
                            result.Add(Ut.KeyValuePair(curKey.UrlUnescape(), curValue.UrlUnescape()));
                            curKey = "";
                            curValue = null;
                            inKey = true;
                        }
                        bufferIndex = i + 1;
                    }
                }
                charsRead = s.Read(buffer, 0, buffer.Length);
                bufferIndex = 0;
            }

            if (curValue != null)
                result.Add(Ut.KeyValuePair(curKey.UrlUnescape(), curValue.UrlUnescape()));

            return result;
        }

        internal static string MakeQueryString(bool? hasQuery, IEnumerable<KeyValuePair<string, string>> query)
        {
            var sb = new StringBuilder(128);
            AppendQueryString(sb, hasQuery, query, true);
            return sb.ToString();
        }

        internal static void AppendQueryString(StringBuilder sb, bool? hasQuery, IEnumerable<KeyValuePair<string, string>> query, bool first)
        {
            if (first && hasQuery == true)
            {
                // Always append the question mark, even if there are no query parameters to be appended
                sb.Append('?');
                foreach (var kvp in query)
                {
                    if (first)
                        first = false;
                    else
                        sb.Append('&');
                    sb.Append(kvp.Key.UrlEscape());
                    sb.Append('=');
                    sb.Append(kvp.Value.UrlEscape());
                }
            }
            else if (hasQuery != false)
            {
                char separator = first ? '?' : '&';
                foreach (var kvp in query)
                {
                    sb.Append(separator);
                    separator = '&';
                    sb.Append(kvp.Key.UrlEscape());
                    sb.Append('=');
                    sb.Append(kvp.Value.UrlEscape());
                }
            }
        }

        internal static void AppendQueryString(StringBuilder sb, string queryStringFirst, bool first)
        {
            if (first)
                sb.Append(queryStringFirst);
            else if (queryStringFirst.Length > 0)
            {
                sb.Append('&');
                sb.Append(queryStringFirst, 1, queryStringFirst.Length - 1);
            }
        }
    }
}
