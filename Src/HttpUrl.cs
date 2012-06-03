using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RT.Util.ExtensionMethods;

namespace RT.Servers
{
    /// <summary>Supports base functionality of an HTTP URL.</summary>
    public interface IHttpUrl
    {
        /// <summary>Specifies whether the protocol used is HTTPS (secure) or not.</summary>
        bool Https { get; }
        /// <summary>Specifies the part of the domain that precedes the <see cref="BaseDomain"/>.
        /// Use <c><see cref="Subdomain"/> + <see cref="BaseDomain"/></c> to obtain the entire domain.</summary>
        /// <example>
        ///     <para>Consider the following example code:</para>
        ///     <code>
        ///         var myResolver = new UrlPathResolver();
        ///         myResolver.Add(new UrlPathHook(myHandler, domain: "homepages.mydomain.com"));
        ///     </code>
        ///     <para>In the above example, an HTTP request for the URL <c>http://peter.schmidt.homepages.mydomain.com/</c>
        ///     would have <see cref="Subdomain"/> set to the value <c>peter.schmidt.</c> (note the trailing dot) and
        ///     <see cref="BaseDomain"/> to <c>homepages.mydomain.com</c>.</para>
        /// </example>
        string Subdomain { get; }
        /// <summary>Specifies the part of the domain to which the handler is hooked by a <see cref="UrlPathHook"/>,
        /// or empty if no <see cref="UrlPathResolver"/> is used. See <see cref="Subdomain"/> for details.</summary>
        string BaseDomain { get; }
        /// <summary>Specifies the port.</summary>
        /// <remarks>The default for HTTP is 80. The default for HTTPS is 443.</remarks>
        int Port { get; }
        /// <summary>Specifies the part of the path to which the handler is hooked by a <see cref="UrlPathHook"/>,
        /// or empty if no <see cref="UrlPathResolver"/> is used. See <see cref="Subpath"/> for details.</summary>
        string BasePath { get; }
        /// <summary>Specifies the part of the URL that follows the path where the request handler is hooked.
        /// Use <c><see cref="BasePath"/> + <see cref="Subpath"/></c> to obtain the full path.</summary>
        /// <example>
        ///     <para>Consider the following example code:</para>
        ///     <code>
        ///         var myResolver = new UrlPathResolver();
        ///         myResolver.Add(new UrlPathHook(myHandler, path: "/homepages"));
        ///     </code>
        ///     <para>In the above example, an HTTP request for the URL <c>http://www.mydomain.com/homepages/a/adam</c>
        ///     would have <see cref="BasePath"/> set to <c>/homepages</c> and <see cref="Subpath"/> set to <c>/a/adam</c>.
        ///     Note the leading slashes.</para>
        /// </example>
        string Subpath { get; }
        /// <summary>Specifies whether the path is followed by a query string (the part that begins with a <c>?</c> character).</summary>
        bool HasQuery { get; }
        /// <summary>Enumerates the query parameters as name/value pairs. Implementors must return an
        /// empty sequence if no query string is present, not null.</summary>
        IEnumerable<KeyValuePair<string, string>> Query { get; }
        /// <summary>Gets the query string including the question mark character (<c>?</c>).
        /// Implementors must return an empty string if no query string is present, not null.</summary>
        string QueryString { get; }
        /// <summary>Gets the first query-string value with the specified name, or null if no query parameter uses the specified name.</summary>
        /// <param name="name">The name (key) by which a query parameter is identified.</param>
        /// <returns>The value of the first matching query parameter or null.</returns>
        string this[string name] { get; }
        /// <summary>Enumerates the values of the query-string parameters with the specified name (key).
        /// Implementors must return an empty sequence (rather than null) if no parameter has the specified name.</summary>
        /// <param name="name">The name (key) by which query parameters are identified.</param>
        /// <returns>A collection containing the values of the matching query-string parameters.</returns>
        IEnumerable<string> QueryValues(string name);

        /// <summary>Adds the query string to the specified <c>StringBuilder</c> instance.</summary>
        /// <param name="sb">An instance of <c>StringBuilder</c> to which the query string is added.</param>
        /// <param name="first">True to begin with a question mark (<c>?</c>), false to begin with an ampersand (<c>&amp;</c>).</param>
        void AppendQueryString(StringBuilder sb, bool first);
    }

    /// <summary>Encapsulates information about a URL that identifies a resource on an HTTP server.</summary>
    public sealed class HttpUrl : IHttpUrl
    {
        /// <summary>Implements <see cref="IHttpUrl.Https"/>.</summary>
        public bool Https { get; set; }
        /// <summary>Implements <see cref="IHttpUrl.Subdomain"/>.</summary>
        public string Subdomain { get; set; }
        /// <summary>Implements <see cref="IHttpUrl.BaseDomain"/>.</summary>
        public string BaseDomain { get; set; }
        /// <summary>Implements <see cref="IHttpUrl.Port"/>.</summary>
        public int Port { get; set; }
        /// <summary>Implements <see cref="IHttpUrl.BasePath"/>.</summary>
        public string BasePath { get; set; }
        /// <summary>Implements <see cref="IHttpUrl.Subpath"/>.</summary>
        public string Subpath { get; set; }

        private bool _hasQuery;
        private IEnumerable<KeyValuePair<string, string>> _query;
        private string _queryString;

        /// <summary>Constructor.</summary>
        internal HttpUrl() { }

        /// <summary>Creates a new instance based on the specified other URL.</summary>
        /// <param name="source">URL to copy information from.</param>
        public HttpUrl(IHttpUrl source)
        {
            if (source == null)
                throw new ArgumentNullException();
            Https = source.Https;
            Subdomain = source.Subdomain;
            BaseDomain = source.BaseDomain;
            Port = source.Port;
            BasePath = source.BasePath;
            Subpath = source.Subpath;
            _hasQuery = source.HasQuery;
            _query = source.Query;
            _queryString = null;
        }

        /// <summary>Implements <see cref="IHttpUrl.this[string]"/>.</summary>
        public string this[string name]
        {
            get
            {
                if (_hasQuery) // would work without the condition too, but slower
                    foreach (var kvp in Query)
                        if (kvp.Key == name)
                            return kvp.Value;
                return null;
            }
        }

        /// <summary>Implements <see cref="IHttpUrl.QueryValues"/>.</summary>
        public IEnumerable<string> QueryValues(string name)
        {
            if (_hasQuery) // would work without the condition too, but slower
                foreach (var kvp in Query)
                    if (kvp.Key == name)
                        yield return kvp.Value;
        }

        /// <summary>Implements <see cref="IHttpUrl.HasQuery"/>.</summary>
        public bool HasQuery
        {
            get { return _hasQuery; }
            set
            {
                _hasQuery = value;
                if (_hasQuery)
                {
                    if (_queryString.Length == 0)
                        _queryString = "?";
                }
                else
                {
                    _query = Enumerable.Empty<KeyValuePair<string, string>>();
                    _queryString = "";
                }
            }
        }

        /// <summary>Implements <see cref="IHttpUrl.Query"/>.</summary>
        public IEnumerable<KeyValuePair<string, string>> Query
        {
            get
            {
                if (_query == null)
                    _query = HttpUrlUtils.ParseQueryString(_queryString);
                return _query;
            }
            set
            {
                if (value == null)
                    throw new ArgumentException();
                _query = value;
                _hasQuery = true;
                _queryString = null;
            }
        }

        /// <summary>Implements <see cref="IHttpUrl.QueryString"/>.</summary>
        public string QueryString
        {
            get
            {
                if (_queryString == null)
                    _queryString = HttpUrlUtils.MakeQueryString(_hasQuery, _query);
                return _queryString;
            }
            set
            {
                if (value == null || (value.Length > 0 && !value.StartsWith('?')))
                    throw new ArgumentException();
                _queryString = value;
                _hasQuery = _queryString.Length > 0;
                _query = null;
            }
        }

        /// <summary>Implements <see cref="IHttpUrl.AppendQueryString"/>.</summary>
        public void AppendQueryString(StringBuilder sb, bool first)
        {
            if (_queryString != null)
                HttpUrlUtils.AppendQueryString(sb, _queryString, first);
            else
                HttpUrlUtils.AppendQueryString(sb, _hasQuery, _query, first);
        }

        internal void SetUrlPath(string urlPath)
        {
            if (urlPath == null || urlPath.Contains(' ') || urlPath.Length == 0 || urlPath[0] != '/')
                throw new ArgumentException();
            int start = urlPath.IndexOf('?');
            if (start < 0)
            {
                Subpath = urlPath;
                _hasQuery = false;
                _query = Enumerable.Empty<KeyValuePair<string, string>>();
                _queryString = "";
            }
            else
            {
                Subpath = urlPath.Substring(0, start);
                _hasQuery = true;
                _query = null;
                _queryString = urlPath.Substring(start);
            }
            BasePath = "";
        }

        internal void SetHost(string host)
        {
            if (host == null)
                throw new ArgumentException();
            var colonPos = host.IndexOf(':');
            if (colonPos < 0)
            {
                Subdomain = host;
                Port = Https ? 443 : 80;
            }
            else
            {
                Subdomain = host.Substring(0, colonPos);
                int port;
                if (!int.TryParse(host.Substring(colonPos + 1), out port))
                    throw new ArgumentException();
                Port = port;
            }
            BaseDomain = "";
        }

        internal void AssertComplete()
        {
            if (Subdomain == null || BaseDomain == null || BasePath == null || Subpath == null)
                throw new InvalidOperationException("HttpUrl is incomplete.");
            if (_query == null && _queryString == null)
                throw new InvalidOperationException("HttpUrl is incomplete.");
        }
    }

    /// <summary>Provides extension functionality on the <see cref="IHttpUrl"/> type.</summary>
    public static class IHttpUrlExtensions
    {
        /// <summary>Creates an <see cref="HttpUrl"/> instance containing a copy of the specified URL.</summary>
        public static HttpUrl ToUrl(this IHttpUrl url)
        {
            return new HttpUrl(url);
        }

        /// <summary>Returns the full path and query string of the specified URL (the part that follows the domain).</summary>
        /// <remarks>This is intended to be used as <c>href</c> attribute values in <c>&lt;a&gt;</c> tags
        /// as it works well for an absolute path within the same domain.</remarks>
        public static string ToHref(this IHttpUrl url)
        {
            var sb = new StringBuilder(128);
            sb.Append(url.BasePath);
            sb.Append(url.Subpath);
            url.AppendQueryString(sb, first: true);
            return sb.ToString();
        }

        /// <summary>Returns the full and complete URL as a single string.</summary>
        public static string ToFull(this IHttpUrl url)
        {
            var sb = new StringBuilder(256);
            sb.Append(url.Https ? "https://" : "http://");
            sb.Append(url.Subdomain);
            sb.Append(url.BaseDomain);
            if ((!url.Https && url.Port != 80) || (url.Https && url.Port != 443))
            {
                sb.Append(':');
                sb.Append(url.Port);
            }
            sb.Append(url.BasePath);
            sb.Append(url.Subpath);
            url.AppendQueryString(sb, first: true);
            return sb.ToString();
        }

        /// <summary>Returns a new URL consisting of the specified URL but with the protocol changed.</summary>
        /// <param name="url">Source URL.</param>
        /// <param name="https">True to change the protocol to https; otherwise http.</param>
        public static IHttpUrl WithHttps(this IHttpUrl url, bool https) { return new UrlWithHttps(url, https); }
        /// <summary>Returns a new URL consisting of the specified URL but with the <see cref="IHttpUrl.Subdomain"/> changed.</summary>
        /// <param name="url">Source URL.</param>
        /// <param name="subdomain">New value for the <see cref="IHttpUrl.Subdomain"/> property.</param>
        public static IHttpUrl WithSubdomain(this IHttpUrl url, string subdomain) { return new UrlWithSubdomain(url, subdomain); }
        /// <summary>Returns a new URL consisting of the specified URL but with the <see cref="IHttpUrl.Subpath"/> changed.</summary>
        /// <param name="url">Source URL.</param>
        /// <param name="subpath">New value for the <see cref="IHttpUrl.Subpath"/> property.</param>
        public static IHttpUrl WithSubpath(this IHttpUrl url, string subpath) { return new UrlWithSubpath(url, subpath); }
        /// <summary>Returns a new URL consisting of the specified URL without the query string.</summary>
        public static IHttpUrl WithoutQuery(this IHttpUrl url) { return new UrlWithoutQueryAll(url); }
        /// <summary>Returns a new URL consisting of the specified URL with the specified query parameter removed.</summary>
        public static IHttpUrl WithoutQuery(this IHttpUrl url, string name) { return new UrlWithQuerySingle(url, name, null); }
        /// <summary>Returns a new URL consisting of the specified URL with the specified query parameters removed.</summary>
        public static IHttpUrl WithoutQuery(this IHttpUrl url, params string[] names) { return new UrlWithoutQueryMultiple(url, names); }
        /// <summary>Returns a new URL consisting of the specified URL with the specified query parameters removed.</summary>
        public static IHttpUrl WithoutQuery(this IHttpUrl url, IEnumerable<string> names) { return new UrlWithoutQueryMultiple(url, names); }
        /// <summary>Returns a new URL consisting of the specified URL with the specified query parameters removed.</summary>
        public static IHttpUrl WithoutQuery(this IHttpUrl url, HashSet<string> names) { return new UrlWithoutQueryMultiple(url, names); }
        /// <summary>Returns a new URL consisting of the specified URL with the specified query parameter replaced with the specified value.</summary>
        public static IHttpUrl WithQuery(this IHttpUrl url, string name, string value) { return new UrlWithQuerySingle(url, name, value); }
        /// <summary>Returns a new URL consisting of the specified URL with the specified query parameter replaced with the specified set of values.</summary>
        public static IHttpUrl WithQuery(this IHttpUrl url, string name, IEnumerable<string> values) { return new UrlWithQueryMultiple(url, name, values); }
        /// <summary>Returns a new URL consisting of the specified URL with the specified query parameter replaced with the specified set of values.</summary>
        public static IHttpUrl WithQuery(this IHttpUrl url, string name, params string[] values) { return new UrlWithQueryMultiple(url, name, values); }
        /// <summary>Returns a new URL consisting of the specified URL but containing only those query parameters whose name matches the specified predicate.</summary>
        public static IHttpUrl Where(this IHttpUrl url, Func<string, bool> predicate) { return new UrlWithQueryWhere(url, predicate); }
        /// <summary>Returns a new URL consisting of the specified URL but containing only those query parameters whose name/value pair matches the specified predicate.</summary>
        public static IHttpUrl Where(this IHttpUrl url, Func<string, string, bool> nameValuePredicate) { return new UrlWithQueryWhereValues(url, nameValuePredicate); }
    }

    #region IHttpUrl manipulator classes

    internal abstract class UrlWithNoChanges : IHttpUrl
    {
        protected IHttpUrl _source;
        public UrlWithNoChanges(IHttpUrl source) { _source = source; }

        public virtual bool Https { get { return _source.Https; } }
        public virtual string Subdomain { get { return _source.Subdomain; } }
        public virtual string BaseDomain { get { return _source.BaseDomain; } }
        public virtual int Port { get { return _source.Port; } }
        public virtual string BasePath { get { return _source.BasePath; } }
        public virtual string Subpath { get { return _source.Subpath; } }
        public virtual bool HasQuery { get { return _source.HasQuery; } }
        public virtual IEnumerable<KeyValuePair<string, string>> Query { get { return _source.Query; } }
        public virtual string QueryString { get { return _source.QueryString; } }
        public virtual string this[string name] { get { return _source[name]; } }
        public virtual IEnumerable<string> QueryValues(string name) { return _source.QueryValues(name); }
        public virtual void AppendQueryString(StringBuilder sb, bool first) { _source.AppendQueryString(sb, first); }
    }

    internal class UrlWithHttps : UrlWithNoChanges
    {
        private bool _https;
        public UrlWithHttps(IHttpUrl source, bool https)
            : base(source)
        {
            _https = https;
        }
        public override bool Https { get { return _https; } }
    }

    internal class UrlWithSubdomain : UrlWithNoChanges
    {
        private string _subdomain;
        public UrlWithSubdomain(IHttpUrl source, string subdomain)
            : base(source)
        {
            if (subdomain == null)
                throw new ArgumentNullException();
            _subdomain = subdomain;
        }
        public override string Subdomain { get { return _subdomain; } }
    }

    internal class UrlWithSubpath : UrlWithNoChanges
    {
        private string _subpath;
        public UrlWithSubpath(IHttpUrl source, string subpath)
            : base(source)
        {
            if (subpath == null)
                throw new ArgumentNullException();
            _subpath = subpath;
        }
        public override string Subpath { get { return _subpath; } }
    }

    internal class UrlWithoutQueryAll : UrlWithNoChanges
    {
        public UrlWithoutQueryAll(IHttpUrl source) : base(source) { }
        public override bool HasQuery { get { return false; } }
        public override IEnumerable<KeyValuePair<string, string>> Query { get { return Enumerable.Empty<KeyValuePair<string, string>>(); } }
        public override string QueryString { get { return ""; } }
        public override string this[string name] { get { return null; } }
        public override IEnumerable<string> QueryValues(string name) { return Enumerable.Empty<string>(); }
        public override void AppendQueryString(StringBuilder sb, bool first) { }
    }

    internal abstract class UrlWithQueryRemovals : UrlWithNoChanges
    {
        private string _queryString = null;
        public UrlWithQueryRemovals(IHttpUrl source) : base(source) { }
        protected abstract string GetValue(string name);
        protected abstract IEnumerable<KeyValuePair<string, string>> GetQuery();
        public override IEnumerable<KeyValuePair<string, string>> Query { get { return _source.HasQuery ? GetQuery() : Enumerable.Empty<KeyValuePair<string, string>>(); } }
        public override string this[string name] { get { return _source.HasQuery ? GetValue(name) : null; } }
        public override string QueryString
        {
            get
            {
                if (!_source.HasQuery)
                    return "";
                if (_queryString == null)
                    _queryString = HttpUrlUtils.MakeQueryString(null, Query);
                return _queryString;
            }
        }
        public override bool HasQuery { get { return _source.HasQuery ? QueryString.Length > 0 : false; } }
        public override void AppendQueryString(StringBuilder sb, bool first)
        {
            if (!_source.HasQuery)
                return;
            if (_queryString != null)
                HttpUrlUtils.AppendQueryString(sb, _queryString, first);
            else
                HttpUrlUtils.AppendQueryString(sb, null, Query, first);
        }
    }

    internal class UrlWithoutQueryMultiple : UrlWithQueryRemovals
    {
        private HashSet<string> _names;
        public UrlWithoutQueryMultiple(IHttpUrl source, HashSet<string> names)
            : base(source)
        {
            if (names == null)
                throw new ArgumentNullException();
            _names = names;
        }
        public UrlWithoutQueryMultiple(IHttpUrl source, IEnumerable<string> names) : this(source, names.ToHashSet()) { }

        protected override string GetValue(string name)
        {
            return _names.Contains(name) ? null : _source[name];
        }

        protected override IEnumerable<KeyValuePair<string, string>> GetQuery()
        {
            return _source.Query.Where(kvp => !_names.Contains(kvp.Key));
        }

        public override IEnumerable<string> QueryValues(string name)
        {
            return _names.Contains(name) ? Enumerable.Empty<string>() : _source.QueryValues(name);
        }
    }

    internal class UrlWithQueryWhere : UrlWithQueryRemovals
    {
        private Func<string, bool> _nameFilter;
        public UrlWithQueryWhere(IHttpUrl source, Func<string, bool> nameFilter)
            : base(source)
        {
            if (nameFilter == null)
                throw new ArgumentException();
            _nameFilter = nameFilter;
        }

        protected override string GetValue(string name)
        {
            return _nameFilter(name) ? _source[name] : null;
        }

        protected override IEnumerable<KeyValuePair<string, string>> GetQuery()
        {
            return _source.Query.Where(kvp => _nameFilter(kvp.Key));
        }

        public override IEnumerable<string> QueryValues(string name)
        {
            if (HasQuery)
                foreach (var kvp in _source.Query)
                    if (kvp.Key == name && _nameFilter(kvp.Key))
                        yield return kvp.Value;
        }
    }

    internal class UrlWithQueryWhereValues : UrlWithQueryRemovals
    {
        private Func<string, string, bool> _nameValueFilter;
        public UrlWithQueryWhereValues(IHttpUrl source, Func<string, string, bool> nameValueFilter)
            : base(source)
        {
            if (nameValueFilter == null)
                throw new ArgumentException();
            _nameValueFilter = nameValueFilter;
        }

        protected override string GetValue(string name)
        {
            foreach (var kvp in GetQuery())
                if (kvp.Key == name && _nameValueFilter(kvp.Key, kvp.Value))
                    return kvp.Value;
            return null;
        }

        protected override IEnumerable<KeyValuePair<string, string>> GetQuery()
        {
            return _source.Query.Where(kvp => _nameValueFilter(kvp.Key, kvp.Value));
        }

        public override IEnumerable<string> QueryValues(string name)
        {
            if (HasQuery)
                foreach (var kvp in _source.Query)
                    if (kvp.Key == name && _nameValueFilter(kvp.Key, kvp.Value))
                        yield return kvp.Value;
        }
    }

    internal abstract class UrlWithQueryChanges : UrlWithNoChanges
    {
        private string _queryString = null;
        public UrlWithQueryChanges(IHttpUrl source) : base(source) { }
        public override string QueryString
        {
            get
            {
                return _queryString ?? (_queryString = HttpUrlUtils.MakeQueryString(null, Query));
            }
        }
        public override void AppendQueryString(StringBuilder sb, bool first)
        {
            if (HasQuery)
                HttpUrlUtils.AppendQueryString(sb, QueryString, first);
        }
    }

    internal class UrlWithQuerySingle : UrlWithNoChanges
    {
        private string _name, _value;
        public UrlWithQuerySingle(IHttpUrl source, string name, string value)
            : base(source)
        {
            if (name == null || value == null)
                throw new ArgumentException();
            _name = name;
            _value = value;
        }
        public override bool HasQuery { get { return _value != null; } }
        public override IEnumerable<KeyValuePair<string, string>> Query
        {
            get
            {
                foreach (var kvp in _source.Query)
                    if (kvp.Key != _name)
                        yield return kvp;
                if (_value != null)
                    yield return new KeyValuePair<string, string>(_name, _value);
            }
        }
        public override string this[string name] { get { return name == _name ? _value : _source[name]; } }
        private string[] _valueAsArray;
        public override IEnumerable<string> QueryValues(string name)
        {
            return
                _name != name ? _source.QueryValues(name) :
                _value == null ? Enumerable.Empty<string>() :
                (_valueAsArray ?? (_valueAsArray = new[] { _value }));
        }
    }

    internal class UrlWithQueryMultiple : UrlWithNoChanges
    {
        private string _name;
        private IEnumerable<string> _values;
        public UrlWithQueryMultiple(IHttpUrl source, string name, IEnumerable<string> values)
            : base(source)
        {
            if (name == null)
                throw new ArgumentException();
            _name = name;
            _values = values ?? Enumerable.Empty<string>();
        }
        public override bool HasQuery { get { return _source.HasQuery || _values.Any(v => v != null); } }
        public override IEnumerable<KeyValuePair<string, string>> Query
        {
            get
            {
                foreach (var kvp in _source.Query)
                    if (kvp.Key != _name)
                        yield return kvp;
                foreach (var value in _values)
                    if (value != null)
                        yield return new KeyValuePair<string, string>(_name, value);
            }
        }
        public override string this[string name] { get { return name == _name ? _values.FirstOrDefault(v => v != null) : _source[name]; } }
        public override IEnumerable<string> QueryValues(string name)
        {
            return _name != name ? _source.QueryValues(name) : _values.Where(v => v != null);
        }
    }

    #endregion

    internal static class HttpUrlUtils
    {
        public static string MakeQueryString(bool? hasQuery, IEnumerable<KeyValuePair<string, string>> query)
        {
            var sb = new StringBuilder(128);
            AppendQueryString(sb, hasQuery, query, true);
            return sb.ToString();
        }

        public static void AppendQueryString(StringBuilder sb, bool? hasQuery, IEnumerable<KeyValuePair<string, string>> query, bool first)
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

        public static void AppendQueryString(StringBuilder sb, string queryStringFirst, bool first)
        {
            if (first)
                sb.Append(queryStringFirst);
            else if (queryStringFirst.Length > 0)
            {
                sb.Append('&');
                sb.Append(queryStringFirst, 1, queryStringFirst.Length - 1);
            }
        }

        public static IEnumerable<KeyValuePair<string, string>> ParseQueryString(string queryString)
        {
            //using (var reader = new StreamReader(body, Encoding.UTF8))
            //    _postFields = ParseQueryValueParameters(reader);

            throw new NotImplementedException();
        }
    }
}

