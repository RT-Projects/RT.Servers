using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RT.Util.ExtensionMethods;

namespace RT.Servers
{
    public interface IHttpUrl
    {
        bool Https { get; }
        string Domain { get; }
        string DomainBase { get; }
        int Port { get; }
        string LocationBase { get; }
        string Location { get; }
        bool HasQuery { get; }
        IEnumerable<KeyValuePair<string, string>> Query { get; }
        string QueryString { get; }
        string this[string name] { get; }
        IEnumerable<string> QueryValues(string name);

        void AppendQueryString(StringBuilder sb, bool first);
    }

    public sealed class HttpUrl : IHttpUrl
    {
        public bool Https { get; set; }
        public string Domain { get; set; }
        public string DomainBase { get; set; }
        public int Port { get; set; }
        public string LocationBase { get; set; }
        public string Location { get; set; }

        private bool _hasQuery;
        private IEnumerable<KeyValuePair<string, string>> _query;
        private string _queryString;

        public HttpUrl()
        {
        }

        public HttpUrl(IHttpUrl source)
        {
            if (source == null)
                throw new ArgumentNullException();
            Https = source.Https;
            Domain = source.Domain;
            DomainBase = source.DomainBase;
            Port = source.Port;
            LocationBase = source.LocationBase;
            Location = source.Location;
            _hasQuery = source.HasQuery;
            _query = source.Query;
            _queryString = null;
        }

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

        public IEnumerable<string> QueryValues(string name)
        {
            if (_hasQuery) // would work without the condition too, but slower
                foreach (var kvp in Query)
                    if (kvp.Key == name)
                        yield return kvp.Value;
        }

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

        public void AppendQueryString(StringBuilder sb, bool first)
        {
            if (_queryString != null)
                HttpUrlUtils.AppendQueryString(sb, _queryString, first);
            else
                HttpUrlUtils.AppendQueryString(sb, _hasQuery, _query, first);
        }

        public void SetUrlPath(string urlPath)
        {
            if (urlPath == null || urlPath.Contains(' ') || urlPath.Length == 0 || urlPath[0] != '/')
                throw new ArgumentException();
            int start = urlPath.IndexOf('?');
            if (start < 0)
            {
                Location = urlPath;
                _hasQuery = false;
                _query = Enumerable.Empty<KeyValuePair<string, string>>();
                _queryString = "";
            }
            else
            {
                Location = urlPath.Substring(0, start);
                _hasQuery = true;
                _query = null;
                _queryString = urlPath.Substring(start);
            }
            LocationBase = "";
        }

        public void SetHost(string host)
        {
            if (host == null)
                throw new ArgumentException();
            var colonPos = host.IndexOf(':');
            if (colonPos < 0)
            {
                Domain = host;
                Port = Https ? 443 : 80;
            }
            else
            {
                Domain = host.Substring(0, colonPos);
                int port;
                if (!int.TryParse(host.Substring(colonPos + 1), out port))
                    throw new ArgumentException();
                Port = port;
            }
            DomainBase = "";
        }

        public void AssertComplete()
        {
            if (Domain == null || DomainBase == null || LocationBase == null || Location == null)
                throw new InvalidOperationException("HttpUrl is incomplete.");
            if (_query == null && _queryString == null)
                throw new InvalidOperationException("HttpUrl is incomplete.");
        }
    }

    public static class IHttpUrlExtensions
    {
        public static HttpUrl ToUrl(this IHttpUrl url)
        {
            return new HttpUrl(url);
        }

        public static string ToHref(this IHttpUrl url)
        {
            var sb = new StringBuilder(128);
            sb.Append(url.LocationBase);
            sb.Append(url.Location);
            url.AppendQueryString(sb, first: true);
            return sb.ToString();
        }

        public static string ToFull(this IHttpUrl url)
        {
            var sb = new StringBuilder(256);
            sb.Append(url.Https ? "https://" : "http://");
            sb.Append(url.Domain);
            sb.Append(url.DomainBase);
            if ((!url.Https && url.Port != 80) || (url.Https && url.Port != 443))
            {
                sb.Append(':');
                sb.Append(url.Port);
            }
            sb.Append(url.LocationBase);
            sb.Append(url.Location);
            url.AppendQueryString(sb, first: true);
            return sb.ToString();
        }

        public static IHttpUrl WithHttps(this IHttpUrl url, bool https) { return new UrlWithHttps(url, https); }
        public static IHttpUrl WithLocationDomain(this IHttpUrl url, string locationDomain) { return new UrlWithLocationDomain(url, locationDomain); }
        public static IHttpUrl WithLocationUrl(this IHttpUrl url, string locationUrl) { return new UrlWithLocationUrl(url, locationUrl); }
        public static IHttpUrl WithoutQuery(this IHttpUrl url) { return new UrlWithoutQueryAll(url); }
        public static IHttpUrl WithoutQuery(this IHttpUrl url, string name) { return new UrlWithQuerySingle(url, name, null); }
        public static IHttpUrl WithoutQuery(this IHttpUrl url, params string[] names) { return new UrlWithoutQueryMultiple(url, names); }
        public static IHttpUrl WithoutQuery(this IHttpUrl url, IEnumerable<string> names) { return new UrlWithoutQueryMultiple(url, names); }
        public static IHttpUrl WithoutQuery(this IHttpUrl url, HashSet<string> names) { return new UrlWithoutQueryMultiple(url, names); }
        public static IHttpUrl WithQuery(this IHttpUrl url, string name, string value) { return new UrlWithQuerySingle(url, name, value); }
        public static IHttpUrl WithQuery(this IHttpUrl url, string name, IEnumerable<string> values) { return new UrlWithQueryMultiple(url, name, values); }
        public static IHttpUrl WithQuery(this IHttpUrl url, string name, params string[] values) { return new UrlWithQueryMultiple(url, name, values); }
        public static IHttpUrl Where(this IHttpUrl url, Func<string, bool> predicate) { return new UrlWithQueryWhere(url, predicate); }
        public static IHttpUrl Where(this IHttpUrl url, Func<string, string, bool> nameValuePredicate) { return new UrlWithQueryWhereValues(url, nameValuePredicate); }
    }

    #region IHttpUrl manipulator classes

    internal abstract class UrlWithNoChanges : IHttpUrl
    {
        protected IHttpUrl _source;
        public UrlWithNoChanges(IHttpUrl source) { _source = source; }

        public virtual bool Https { get { return _source.Https; } }
        public virtual string Domain { get { return _source.Domain; } }
        public virtual string DomainBase { get { return _source.DomainBase; } }
        public virtual int Port { get { return _source.Port; } }
        public virtual string LocationBase { get { return _source.LocationBase; } }
        public virtual string Location { get { return _source.Location; } }
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

    internal class UrlWithLocationDomain : UrlWithNoChanges
    {
        private string _locationDomain;
        public UrlWithLocationDomain(IHttpUrl source, string locationDomain)
            : base(source)
        {
            if (locationDomain == null)
                throw new ArgumentNullException();
            _locationDomain = locationDomain;
        }
        public override string Domain { get { return _locationDomain; } }
    }

    internal class UrlWithLocationUrl : UrlWithNoChanges
    {
        private string _locationUrl;
        public UrlWithLocationUrl(IHttpUrl source, string locationUrl)
            : base(source)
        {
            if (locationUrl == null)
                throw new ArgumentNullException();
            _locationUrl = locationUrl;
        }
        public override string Location { get { return _locationUrl; } }
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
        private string _queryString = null;
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

