using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using RT.Util.Collections;
using RT.Util.ExtensionMethods;

namespace RT.Servers
{
    /// <summary>
    ///     Encapsulates all supported HTTP request headers. These will be set by the server when it receives the request.</summary>
    [Serializable]
    public sealed class HttpRequestHeaders : IDictionary<string, string>
    {
#pragma warning disable 1591    // Missing XML comment for publicly visible type or member
        public ListSorted<QValue<string>> Accept;
        public ListSorted<QValue<string>> AcceptCharset;
        public ListSorted<QValue<HttpContentEncoding>> AcceptEncoding;
        public ListSorted<QValue<string>> AcceptLanguage;
        public HttpConnection Connection;
        public long? ContentLength;                 // required only for POST
        public HttpPostContentType? ContentType;     // required only for POST
        public string ContentMultipartBoundary;     // required only for POST and only if ContentType == HttpPostContentType.MultipartFormData
        public Dictionary<string, Cookie> Cookie = new Dictionary<string, Cookie>();
        public Dictionary<string, string> Expect;
        public string Host;
        public DateTime? IfModifiedSince;
        public List<WValue> IfNoneMatch;
        public List<HttpRange> Range;
        public string Upgrade;
        public string UserAgent;
        public List<IPAddress> XForwardedFor;
#pragma warning restore 1591    // Missing XML comment for publicly visible type or member

        private Dictionary<string, string> _headers = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

        /// <summary>
        ///     Parses the specified header and stores it in this instance. Returns whether the header was recognised.</summary>
        /// <param name="name">
        ///     Header name</param>
        /// <param name="value">
        ///     Header value</param>
        internal bool parseAndAddHeader(string name, string value)
        {
            string nameLower = name.ToLowerInvariant();
            int intOutput;
            bool recognised = false;

            try
            {
                if (nameLower == "accept")
                {
                    splitAndAddByQ(ref Accept, value);
                    recognised = true;
                }
                else if (nameLower == "accept-charset")
                {
                    splitAndAddByQ(ref AcceptCharset, value);
                    recognised = true;
                }
                else if (nameLower == "accept-encoding")
                {
                    splitAndAddByQ(ref AcceptEncoding, value, HttpEnumsParser.ParseHttpContentEncoding);
                    recognised = true;
                }
                else if (nameLower == "accept-language")
                {
                    splitAndAddByQ(ref AcceptLanguage, value);
                    recognised = true;
                }
                else if (nameLower == "connection")
                {
                    Connection = HttpEnumsParser.ParseHttpConnection(value);
                    recognised = true;
                }
                else if (nameLower == "content-length" && ContentLength == null && int.TryParse(value, out intOutput))
                {
                    ContentLength = intOutput;
                    recognised = true;
                }
                else if (nameLower == "content-type")
                {
                    var values = value.Split(';');
                    var firstValue = values[0].Trim();
                    if (string.Equals(firstValue, "application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
                    {
                        ContentType = HttpPostContentType.ApplicationXWwwFormUrlEncoded;
                        recognised = true;
                    }
                    else if (string.Equals(firstValue, "multipart/form-data", StringComparison.OrdinalIgnoreCase))
                    {
                        for (int i = 1; i < values.Length; i++)
                        {
                            var v = values[i].Trim();
                            if (v.StartsWith("boundary="))
                            {
                                ContentType = HttpPostContentType.MultipartFormData;
                                ContentMultipartBoundary = v.Substring("boundary=".Length);
                                recognised = true;
                            }
                        }
                    }
                }
                else if (nameLower == "cookie")
                {
                    parseAndAddCookies(ref Cookie, value);
                    recognised = true;
                }
                else if (nameLower == "host" && Host == null)
                {
                    Host = value.ToLowerInvariant();
                    recognised = true;
                }
                else if (nameLower == "expect")
                {
                    string hv = value;
                    Expect = new Dictionary<string, string>();
                    while (hv.Length > 0)
                    {
                        Match m = Regex.Match(hv, @"(^[^;=""]*?)\s*(;\s*|$)");
                        if (m.Success)
                        {
                            Expect.Add(m.Groups[1].Value.ToLowerInvariant(), "1");
                            hv = hv.Substring(m.Length);
                        }
                        else
                        {
                            m = Regex.Match(hv, @"^([^;=""]*?)\s*=\s*([^;=""]*?)\s*(;\s*|$)");
                            if (m.Success)
                            {
                                Expect.Add(m.Groups[1].Value.ToLowerInvariant(), m.Groups[2].Value.ToLowerInvariant());
                                hv = hv.Substring(m.Length);
                            }
                            else
                            {
                                m = Regex.Match(hv, @"^([^;=""]*?)\s*=\s*""([^""]*)""\s*(;\s*|$)");
                                if (m.Success)
                                {
                                    Expect.Add(m.Groups[1].Value.ToLowerInvariant(), m.Groups[2].Value);
                                    hv = hv.Substring(m.Length);
                                }
                                else
                                {
                                    Expect.Add(hv, "1");
                                    hv = "";
                                }
                            }
                        }
                    }
                    recognised = true;
                }
                else if (nameLower == "if-modified-since" && IfModifiedSince == null)
                {
                    DateTime output;
                    if (DateTime.TryParse(value, out output))
                    {
                        IfModifiedSince = output.ToUniversalTime();
                        recognised = true;
                    }
                }
                else if (nameLower == "if-none-match" && IfNoneMatch == null)
                {
                    IfNoneMatch = new List<WValue>();
                    Match m;
                    while ((m = Regex.Match(value, @"^\s*((W/)?""((?:\\.|[^""])*)""|(\*))\s*(?:,\s*|$)", RegexOptions.Singleline)).Success)
                    {
                        IfNoneMatch.Add(new WValue(m.Groups[3].Value.CLiteralUnescape() + m.Groups[4].Value, m.Groups[2].Length > 0));
                        value = value.Substring(m.Length);
                    }
                    recognised = true;
                }
                else if (nameLower == "range" && value.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase))
                {
                    parseAndAddRange(ref Range, value);
                    recognised = true;
                }
                else if (nameLower == "user-agent" && UserAgent == null)
                {
                    UserAgent = value;
                    recognised = true;
                }
                else if (nameLower == "upgrade" && Upgrade == null)
                {
                    Upgrade = value;
                    recognised = true;
                }
                else if (nameLower == "x-forwarded-for" && XForwardedFor == null)
                {
                    var items = value.Split(',').Select(s => IPAddress.Parse(s.Trim())).ToList();
                    if (items.Count > 0)
                    {
                        XForwardedFor = items;
                        recognised = true;
                    }
                }
            }
            catch
            {
                // Ignore absolutely any error; the header will just simply be unrecognised.
            }

            _headers[name] = value;

            return recognised;
        }

        /// <summary>Parses the cookie header and adds the cookies to the specified cookie dictionary.</summary>
        private static void parseAndAddCookies(ref Dictionary<string, Cookie> cookies, string cookieHeaderValue)
        {
            Cookie prevCookie = null;
            while (cookieHeaderValue.Length > 0)
            {
                string key, value;
                // permissible characters in cookie names are all characters 0x20-0x7E except: ()<>@,;:\"/[]?={}

                Match m = Regex.Match(cookieHeaderValue, @"^\s*([- !#-'*+.0-9A-Z^-z|~]+)=([^;]*)(;\s*|$)");
                if (m.Success)
                {
                    key = m.Groups[1].Value;
                    value = m.Groups[2].Value;
                }
                else
                {
                    m = Regex.Match(cookieHeaderValue, @"^\s*([- !#-'*+.0-9A-Z^-z|~]+)=""([^""]*)""\s*(;\s*|$)");
                    if (m.Success)
                    {
                        key = m.Groups[1].Value;
                        value = m.Groups[2].Value;
                    }
                    else
                    {
                        if (cookieHeaderValue.Contains(';'))
                        {
                            // Invalid syntax; try to continue parsing at the next ";"
                            cookieHeaderValue = cookieHeaderValue.Substring(cookieHeaderValue.IndexOf(';') + 1);
                            continue;
                        }
                        else
                            // Completely invalid syntax; ignore the rest of this header
                            return;
                    }
                }
                cookieHeaderValue = cookieHeaderValue.Substring(m.Groups[0].Length);

                if (key == "$Version")
                    continue;   // ignore that.

                if (cookies == null)
                    cookies = new Dictionary<string, Cookie>();

                if (key == "$Path" && prevCookie != null)
                    prevCookie.Path = value;
                else if (key == "$Domain" && prevCookie != null)
                    prevCookie.Domain = value;
                else if (key == "$Expires" && prevCookie != null)
                {
                    DateTime output;
                    if (DateTime.TryParse(cookieHeaderValue, out output))
                        prevCookie.Expires = output.ToUniversalTime();
                }
                else
                {
                    prevCookie = new Cookie { Name = key, Value = value.UrlUnescape() };
                    cookies[key] = prevCookie;
                }
            }
        }

        /// <summary>Parses the specified Range header and adds the ranges to the specified ranges list.</summary>
        private static void parseAndAddRange(ref List<HttpRange> ranges, string rangeHeaderValue)
        {
            foreach (var rangeStr in rangeHeaderValue.ToLowerInvariant().Split(','))
            {
                if (rangeStr == null || rangeStr.Length < 2)
                    return;
                Match m = Regex.Match(rangeStr, @"(\d*)-(\d*)");
                if (!m.Success)
                    return;
                if (ranges == null)
                    ranges = new List<HttpRange>();
                var range = new HttpRange();
                if (m.Groups[1].Length > 0)
                    range.From = int.Parse(m.Groups[1].Value);
                if (m.Groups[2].Length > 0)
                    range.To = int.Parse(m.Groups[2].Value);
                ranges.Add(range);
            }
        }

        private static void splitAndAddByQ(ref ListSorted<QValue<string>> parsedList, string headerValue)
        {
            if (parsedList == null)
                parsedList = new ListSorted<QValue<string>>();
            var split = Regex.Split(headerValue, @"\s*,\s*");
            foreach (string item in split)
            {
                float q = 0;
                string nItem = item;
                if (item.Contains(";"))
                {
                    var match = Regex.Match(item, @";\s*q=(\d+(\.\d+)?)");
                    if (match.Success)
                        q = 1 - float.Parse(match.Groups[1].Value);
                    nItem = item.Remove(item.IndexOf(';'));
                }
                parsedList.Add(new QValue<string>(q, nItem));
            }
        }

        private static void splitAndAddByQ<T>(ref ListSorted<QValue<T>> parsedList, string headerValue, Func<string, T> converter)
        {
            if (parsedList == null)
                parsedList = new ListSorted<QValue<T>>();
            var split = Regex.Split(headerValue, @"\s*,\s*");
            foreach (string item in split)
            {
                float q = 0;
                string nItem = item;
                if (item.Contains(";"))
                {
                    var match = Regex.Match(item, @";\s*q=(\d+(\.\d+)?)");
                    if (match.Success)
                        q = 1 - float.Parse(match.Groups[1].Value);
                    nItem = item.Remove(item.IndexOf(';'));
                }
                parsedList.Add(new QValue<T>(q, converter(nItem)));
            }
        }

        /// <summary>Gets the value of the specified header, or null if such header is not present. Setting is not supported.</summary>
        public string this[string key]
        {
            get
            {
                string result;
                if (_headers.TryGetValue(key, out result))
                    return result;
                else
                    return null;
            }
            set { throw new NotSupportedException(); }
        }

        /// <summary>Returns true.</summary>
        public bool IsReadOnly { get { return true; } }
        /// <summary>Gets the number of headers in this collection.</summary>
        public int Count { get { return _headers.Count; } }
        /// <summary>Gets all the header names in this collection.</summary>
        public ICollection<string> Keys { get { return _headers.Keys; } }
        /// <summary>Gets all the header values in this collection.</summary>
        public ICollection<string> Values { get { return _headers.Values; } }
        /// <summary>Determines if the specified header is present in this collection (case-insensitive).</summary>
        public bool ContainsKey(string key) { return _headers.ContainsKey(key); }
        /// <summary>Attempts to get the specified header’s value from this collection.</summary>
        public bool TryGetValue(string key, out string value) { return _headers.TryGetValue(key, out value); }
        /// <summary>Enumerates all headers and values in this collection.</summary>
        public IEnumerator<KeyValuePair<string, string>> GetEnumerator() { return _headers.GetEnumerator(); }

        void IDictionary<string, string>.Add(string key, string value) { throw new NotSupportedException(); }
        bool IDictionary<string, string>.Remove(string key) { throw new NotSupportedException(); }
        void ICollection<KeyValuePair<string, string>>.Clear() { throw new NotSupportedException(); }
        void ICollection<KeyValuePair<string, string>>.Add(KeyValuePair<string, string> item) { throw new NotSupportedException(); }
        bool ICollection<KeyValuePair<string, string>>.Remove(KeyValuePair<string, string> item) { throw new NotSupportedException(); }
        bool ICollection<KeyValuePair<string, string>>.Contains(KeyValuePair<string, string> item) { return ((ICollection<KeyValuePair<string, string>>) _headers).Contains(item); }
        void ICollection<KeyValuePair<string, string>>.CopyTo(KeyValuePair<string, string>[] array, int arrayIndex) { ((ICollection<KeyValuePair<string, string>>) _headers).CopyTo(array, arrayIndex); }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() { return GetEnumerator(); }
    }
}
