﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RT.Util;
using RT.Util.ExtensionMethods;

namespace RT.Servers
{
    /// <summary>Supports base functionality of an HTTP URL.</summary>
    public interface IHttpUrl
    {
        /// <summary>Specifies whether the protocol used is HTTPS (secure) or not.</summary>
        bool Https { get; }

        /// <summary>
        ///     Specifies the port.</summary>
        /// <remarks>
        ///     The default for HTTP is 80. The default for HTTPS is 443.</remarks>
        int Port { get; }

        /// <summary>
        ///     Specifies parts of the domain which were resolved via a compatible URL resolver. Must not be modified
        ///     directly. Do not use this member directly; instead use a relevant extension method to manipulate the URL (such
        ///     as <see cref="IHttpUrlExtensions.WithDomainParent"/>).</summary>
        string[] ParentDomains { get; }

        /// <summary>
        ///     Specifies the domain part of the URL – that is, the part that comes before the first slash (or, if a URL
        ///     resolver is used, the part that comes before the hook domain). The protocol is not included. Whenever not
        ///     empty, Domain always ends with a dot unless it contains the full domain up to the TLD. Manipulate this part
        ///     using <see cref="IHttpUrlExtensions.WithDomain"/>.</summary>
        string Domain { get; }

        /// <summary>
        ///     Specifies parts of the URL path which were resolved via a compatible URL resolver. Must not be modified
        ///     directly. Do not use this member directly; instead use a relevant extension method to manipulate the URL (such
        ///     as <see cref="IHttpUrlExtensions.WithPathParent"/>).</summary>
        string[] ParentPaths { get; }

        /// <summary>
        ///     Specifies the path part of the URL – that is, the part that comes after the domain (or, if a URL resolver is
        ///     used, the part that comes after the hook path). The query string is not included. Whenever not empty, Path
        ///     always begins with a forward slash. Manipulate this part using <see cref="IHttpUrlExtensions.WithPath"/> or
        ///     <see cref="IHttpUrlExtensions.WithPathOnly"/>.</summary>
        string Path { get; }

        /// <summary>
        ///     Specifies whether the path is followed by a query string (the part that begins with a <c>?</c> character).</summary>
        bool HasQuery { get; }

        /// <summary>
        ///     Enumerates the query parameters as name/value pairs. Implementors must return an empty sequence if no query
        ///     string is present, not null.</summary>
        IEnumerable<KeyValuePair<string, string>> Query { get; }

        /// <summary>
        ///     Gets the query string including the question mark character (<c>?</c>). Implementors must return an empty
        ///     string if no query string is present, not null.</summary>
        string QueryString { get; }

        /// <summary>
        ///     Gets the first query-string value with the specified name, or null if no query parameter uses the specified
        ///     name.</summary>
        /// <param name="name">
        ///     The name (key) by which a query parameter is identified.</param>
        /// <returns>
        ///     The value of the first matching query parameter or null.</returns>
        string this[string name] { get; }

        /// <summary>
        ///     Enumerates the values of the query-string parameters with the specified name (key). Implementors must return
        ///     an empty sequence (rather than null) if no parameter has the specified name.</summary>
        /// <param name="name">
        ///     The name (key) by which query parameters are identified.</param>
        /// <returns>
        ///     A collection containing the values of the matching query-string parameters.</returns>
        IEnumerable<string> QueryValues(string name);

        /// <summary>
        ///     Adds the query string to the specified <c>StringBuilder</c> instance.</summary>
        /// <param name="sb">
        ///     An instance of <c>StringBuilder</c> to which the query string is added.</param>
        /// <param name="first">
        ///     True to begin with a question mark (<c>?</c>), false to begin with an ampersand (<c>&amp;</c>).</param>
        void AppendQueryString(StringBuilder sb, bool first);
    }

    /// <summary>Encapsulates information about a URL that identifies a resource on an HTTP server.</summary>
    [Serializable]
    public sealed class HttpUrl : IHttpUrl
    {
        /// <summary>Implements <see cref="IHttpUrl.Https"/>.</summary>
        public bool Https { get; set; }
        /// <summary>Implements <see cref="IHttpUrl.Port"/>.</summary>
        public int Port { get; set; }
        /// <summary>Implements <see cref="IHttpUrl.ParentDomains"/>.</summary>
        public string[] ParentDomains { get; set; }
        /// <summary>Implements <see cref="IHttpUrl.Domain"/>.</summary>
        public string Domain { get; set; }
        /// <summary>Implements <see cref="IHttpUrl.ParentPaths"/>.</summary>
        public string[] ParentPaths { get; set; }
        /// <summary>Implements <see cref="IHttpUrl.Path"/>.</summary>
        public string Path { get; set; }

        private bool _hasQuery;
        private IEnumerable<KeyValuePair<string, string>> _query;
        private string _queryString;

        /// <summary>Constructor.</summary>
        internal HttpUrl() { }

        /// <summary>
        ///     Constructor.</summary>
        /// <param name="httpHost">
        ///     The value of the HTTP "Host" header.</param>
        /// <param name="httpLocation">
        ///     The value of the HTTP resource location field.</param>
        public HttpUrl(string httpHost, string httpLocation)
        {
            SetHost(httpHost);
            SetLocation(httpLocation);
        }

        /// <summary>
        ///     Constructor.</summary>
        /// <param name="https">
        ///     Whether this is an HTTPS URL.</param>
        /// <param name="httpHost">
        ///     The value of the HTTP "Host" header.</param>
        /// <param name="httpLocation">
        ///     The value of the HTTP resource location field.</param>
        public HttpUrl(bool https, string httpHost, string httpLocation)
        {
            Https = https;
            SetHost(httpHost);
            SetLocation(httpLocation);
        }

        /// <summary>
        ///     Creates a new instance based on the specified other URL.</summary>
        /// <param name="source">
        ///     URL to copy information from.</param>
        public HttpUrl(IHttpUrl source)
        {
            if (source == null)
                throw new ArgumentNullException("source");
            Https = source.Https;
            Port = source.Port;
            ParentDomains = source.ParentDomains;
            Domain = source.Domain;
            ParentPaths = source.ParentPaths;
            Path = source.Path;
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
                    _query = HttpHelper.ParseQueryString(_queryString);
                return _query;
            }
            set
            {
                if (value == null)
                    throw new ArgumentNullException("value");
                _query = value;
                _hasQuery = value.Any();
                _queryString = null;
            }
        }

        /// <summary>Implements <see cref="IHttpUrl.QueryString"/>.</summary>
        public string QueryString
        {
            get
            {
                if (_queryString == null)
                    _queryString = HttpHelper.MakeQueryString(_hasQuery, _query);
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
                HttpHelper.AppendQueryString(sb, _queryString, first);
            else
                HttpHelper.AppendQueryString(sb, _hasQuery, _query, first);
        }

        /// <summary>Sets the part of the URL after the host, consisting of the path and optionally the query parameters.</summary>
        internal void SetLocation(string location)
        {
            Ut.Assert(location != null && location.Length > 0 && location[0] == '/');
            ParentPaths = HttpHelper.EmptyStrings;
            int start = location.IndexOf('?');
            if (start < 0)
            {
                Path = location;
                _hasQuery = false;
                _query = Enumerable.Empty<KeyValuePair<string, string>>();
                _queryString = "";
            }
            else
            {
                Path = location.Substring(0, start);
                _hasQuery = true;
                _query = null;
                _queryString = location.Substring(start);
            }
        }

        /// <summary>Sets the host part of the URL, consisting of the domain and optionally the port number.</summary>
        internal void SetHost(string host)
        {
            Ut.Assert(host != null);
            var colonPos = host.IndexOf(':');
            if (colonPos < 0)
            {
                if (host.Length > 0 && host[host.Length - 1] == '.')
                    Domain = host.Substring(0, host.Length - 1);
                else
                    Domain = host;
                Port = Https ? 443 : 80;
            }
            else
            {
                if (colonPos > 0 && host[colonPos - 1] == '.')
                    Domain = host.Substring(0, colonPos - 1);
                else
                    Domain = host.Substring(0, colonPos);
                int port;
                if (!int.TryParse(host.Substring(colonPos + 1), out port))
                    throw new ArgumentException();
                Port = port;
            }
            ParentDomains = HttpHelper.EmptyStrings;
        }

        internal void AssertComplete()
        {
            if (ParentDomains == null || Domain == null || ParentPaths == null || Path == null)
                throw new InvalidOperationException("HttpUrl is incomplete.");
            if (_query == null && _queryString == null)
                throw new InvalidOperationException("HttpUrl is incomplete.");
        }

        /// <summary>Returns the full URL.</summary>
        public override string ToString()
        {
            return this.ToFull();
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

        /// <summary>
        ///     Returns the full path and query string of the specified URL (the part that follows the domain).</summary>
        /// <remarks>
        ///     This is intended to be used as <c>href</c> attribute values in <c>&lt;a&gt;</c> tags as it works well for an
        ///     absolute path within the same domain.</remarks>
        public static string ToHref(this IHttpUrl url)
        {
            var sb = new StringBuilder(128);
            for (int i = 0; i < url.ParentPaths.Length; i++)
                sb.Append(url.ParentPaths[i]);
            sb.Append(url.Path);
            url.AppendQueryString(sb, first: true);
            return sb.ToString();
        }

        /// <summary>Returns the full and complete URL as a single string.</summary>
        public static string ToFull(this IHttpUrl url)
        {
            var sb = new StringBuilder(256);
            sb.Append(url.Https ? "https://" : "http://");
            sb.Append(url.Domain);
            if (url.ParentDomains != null)
                for (int i = url.ParentDomains.Length - 1; i >= 0; i--)
                    sb.Append(url.ParentDomains[i]);
            if ((!url.Https && url.Port != 80) || (url.Https && url.Port != 443))
            {
                sb.Append(':');
                sb.Append(url.Port);
            }
            if (url.ParentPaths != null)
                for (int i = 0; i < url.ParentPaths.Length; i++)
                    sb.Append(url.ParentPaths[i]);
            sb.Append(url.Path);
            url.AppendQueryString(sb, first: true);
            return sb.ToString();
        }

        /// <summary>
        ///     Returns the full path (the part that comes after the domain) regardless of any URL resolvers in use. The query
        ///     string is not included.</summary>
        public static string GetFullPath(this IHttpUrl url)
        {
            var sb = new StringBuilder();
            if (url.ParentPaths != null)
                for (int i = 0; i < url.ParentPaths.Length; i++)
                    sb.Append(url.ParentPaths[i]);
            sb.Append(url.Path);
            return sb.ToString();
        }

        /// <summary>
        ///     Returns the full domain name (the part that comes before the first slash) regardless of any URL resolvers in
        ///     use. The protocol and the port number is not included.</summary>
        public static string GetFullDomain(this IHttpUrl url)
        {
            var sb = new StringBuilder();
            sb.Append(url.Domain);
            if (url.ParentDomains != null)
                for (int i = url.ParentDomains.Length - 1; i >= 0; i--)
                    sb.Append(url.ParentDomains[i]);
            return sb.ToString();
        }

        /// <summary>
        ///     Returns a new URL consisting of the specified URL but with the protocol changed.</summary>
        /// <param name="url">
        ///     Source URL.</param>
        /// <param name="https">
        ///     True to change the protocol to https; otherwise http.</param>
        public static IHttpUrl WithHttps(this IHttpUrl url, bool https) { return new UrlWithHttps(url, https); }
        /// <summary>
        ///     Returns a new URL with the <see cref="IHttpUrl.Domain"/> changed. The domain must be empty or end with a dot,
        ///     unless it is at the TLD level, in which case it must be non-empty and not end with a dot.</summary>
        public static IHttpUrl WithDomain(this IHttpUrl url, string domain) { return new UrlWithDomain(url, domain); }
        /// <summary>
        ///     Returns a new, equivalent URL such that the <see cref="IHttpUrl.Domain"/> includes the part matched by the most recent URL
        ///     resolver.</summary>
        public static IHttpUrl WithDomainParent(this IHttpUrl url) { return new UrlWithDomainParent(url); }
        /// <summary>Returns a new, equivalent URL whose <see cref="IHttpUrl.Domain"/> is empty (treating it like the result of a nested URL resolver).</summary>
        public static IHttpUrl WithDomainChild(this IHttpUrl url) { return new UrlWithDomainChild(url); }
        /// <summary>
        ///     Returns a new URL with the <see cref="IHttpUrl.Path"/> changed. The path must be empty or begin with a forward
        ///     slash, and must not contain a query string.</summary>
        public static IHttpUrl WithPath(this IHttpUrl url, string path) { return new UrlWithPath(url, path); }
        /// <summary>
        ///     Returns a new URL with the <see cref="IHttpUrl.Path"/> changed and the query string removed. The path must be
        ///     empty or begin with a forward slash, and must not contain a query string.</summary>
        public static IHttpUrl WithPathOnly(this IHttpUrl url, string path) { return new UrlWithPathOnly(url, path); }
        /// <summary>
        ///     Returns a new, equivalent URL such that the <see cref="IHttpUrl.Path"/> includes the part matched by the most
        ///     recent URL resolver.</summary>
        public static IHttpUrl WithPathParent(this IHttpUrl url) { return new UrlWithPathParent(url); }
        /// <summary>Returns a new, equivalent URL whose <see cref="IHttpUrl.Path"/> is empty (treating it like the result of a nested URL resolver).</summary>
        public static IHttpUrl WithPathChild(this IHttpUrl url) { return new UrlWithPathChild(url); }
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
        /// <summary>
        ///     Returns a new URL consisting of the specified URL with the specified query parameter replaced with the
        ///     specified value.</summary>
        public static IHttpUrl WithQuery(this IHttpUrl url, string name, string value) { return new UrlWithQuerySingle(url, name, value); }
        /// <summary>
        ///     Returns a new URL consisting of the specified URL with the specified query parameter replaced with the
        ///     specified set of values.</summary>
        public static IHttpUrl WithQuery(this IHttpUrl url, string name, IEnumerable<string> values) { return new UrlWithQueryMultiple(url, name, values); }
        /// <summary>
        ///     Returns a new URL consisting of the specified URL with the specified query parameter replaced with the
        ///     specified set of values.</summary>
        public static IHttpUrl WithQuery(this IHttpUrl url, string name, params string[] values) { return new UrlWithQueryMultiple(url, name, values); }
        /// <summary>
        ///     Returns a new URL consisting of the specified URL but containing only those query parameters whose name
        ///     matches the specified predicate.</summary>
        public static IHttpUrl Where(this IHttpUrl url, Func<string, bool> predicate) { return new UrlWithQueryWhere(url, predicate); }
        /// <summary>
        ///     Returns a new URL consisting of the specified URL but containing only those query parameters whose name/value
        ///     pair matches the specified predicate.</summary>
        public static IHttpUrl Where(this IHttpUrl url, Func<string, string, bool> nameValuePredicate) { return new UrlWithQueryWhereValues(url, nameValuePredicate); }

        /// <summary>
        ///     Changes a URL’s subpath or subdomain.</summary>
        /// <param name="url">
        ///     The URL to modify.</param>
        /// <param name="pathOrSubdomain">
        ///     The new subpath or subdomain, without any slashes or dots.</param>
        /// <param name="useSubdomain">
        ///     If <c>true</c>, the subdomain is changed; if <c>false</c> (default), the path is changed.</param>
        /// <param name="retainQueryParams">
        ///     If <c>true</c>, the query parameters are retained; if <c>false</c> (default), they are removed.</param>
        /// <returns>
        ///     The new URL.</returns>
        public static IHttpUrl With(this IHttpUrl url, string pathOrSubdomain, bool useSubdomain = false, bool retainQueryParams = false)
        {
            return WithParents(url, 0, pathOrSubdomain, useSubdomain, retainQueryParams);
        }

        /// <summary>
        ///     Changes a URL’s subpath or subdomain relative to the most recent URL resolver.</summary>
        /// <param name="url">
        ///     The URL to modify.</param>
        /// <param name="pathOrSubdomain">
        ///     The new subpath or subdomain, without any slashes or dots.</param>
        /// <param name="useSubdomain">
        ///     If <c>true</c>, the subdomain is changed; if <c>false</c> (default), the path is changed.</param>
        /// <param name="retainQueryParams">
        ///     If <c>true</c>, the query parameters are retained; if <c>false</c> (default), they are removed.</param>
        /// <returns>
        ///     The new URL.</returns>
        public static IHttpUrl WithParent(this IHttpUrl url, string pathOrSubdomain, bool useSubdomain = false, bool retainQueryParams = false)
        {
            return WithParents(url, 1, pathOrSubdomain, useSubdomain, retainQueryParams);
        }

        /// <summary>
        ///     Changes a URL’s subpath or subdomain relative to the specified number of URL resolvers.</summary>
        /// <param name="url">
        ///     The URL to modify.</param>
        /// <param name="levels">
        ///     The number of URL resolvers to rewind.</param>
        /// <param name="pathOrSubdomain">
        ///     The new subpath or subdomain, without any slashes or dots.</param>
        /// <param name="useSubdomain">
        ///     If <c>true</c>, the subdomain is changed; if <c>false</c> (default), the path is changed.</param>
        /// <param name="retainQueryParams">
        ///     If <c>true</c>, the query parameters are retained; if <c>false</c> (default), they are removed.</param>
        /// <returns>
        ///     The new URL.</returns>
        public static IHttpUrl WithParents(this IHttpUrl url, int levels, string pathOrSubdomain, bool useSubdomain = false, bool retainQueryParams = false)
        {
            pathOrSubdomain = pathOrSubdomain.Length == 0 ? "" : useSubdomain ? pathOrSubdomain + "." : "/" + pathOrSubdomain;
            for (int i = 0; i < levels; i++)
                url = useSubdomain ? url.WithDomainParent() : url.WithPathParent();
            return
                useSubdomain
                    ? retainQueryParams
                        ? url.WithDomain(pathOrSubdomain)
                        : url.WithDomain(pathOrSubdomain).WithoutQuery()
                : retainQueryParams
                    ? url.WithPath(pathOrSubdomain)
                    : url.WithPathOnly(pathOrSubdomain);
        }
    }

    #region IHttpUrl manipulator classes

    internal abstract class UrlWithNoChanges : IHttpUrl
    {
        protected IHttpUrl _source;
        public UrlWithNoChanges(IHttpUrl source) { _source = source; }

        public virtual bool Https { get { return _source.Https; } }
        public virtual int Port { get { return _source.Port; } }
        public virtual string[] ParentDomains { get { return _source.ParentDomains; } }
        public virtual string Domain { get { return _source.Domain; } }
        public virtual string[] ParentPaths { get { return _source.ParentPaths; } }
        public virtual string Path { get { return _source.Path; } }
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

    internal class UrlWithDomain : UrlWithNoChanges
    {
        private string _domain;
        public UrlWithDomain(IHttpUrl source, string domain)
            : base(source)
        {
            if (domain == null)
                throw new ArgumentNullException("domain");
            if (ParentDomains.Length == 0 && (domain == "" || domain.EndsWith(".")))
                throw new ArgumentException("At the TLD level the domain name must not be empty and must not end with a dot (.).");
            if (ParentDomains.Length > 0 && domain != "" && !domain.EndsWith("."))
                throw new ArgumentException("The domain name must be empty or end with a dot.");
            _domain = domain;
        }
        public override string Domain { get { return _domain; } }
    }

    internal class UrlWithPath : UrlWithNoChanges
    {
        private string _path;
        public UrlWithPath(IHttpUrl source, string path)
            : base(source)
        {
            if (path == null)
                throw new ArgumentNullException("path");
            if (path.Contains('?'))
                throw new ArgumentException("The Path must not contain a question mark. Did you forget to escape the URL?");
            if (path != "" && path[0] != '/')
                throw new ArgumentException("The Path must start with a forward slash.");
            _path = path;
        }
        public override string Path { get { return _path; } }
    }

    internal class UrlWithPathOnly : UrlWithoutQueryAll
    {
        private string _path;
        public UrlWithPathOnly(IHttpUrl source, string path)
            : base(source)
        {
            if (path == null)
                throw new ArgumentNullException("path");
            if (path != "" && !path.StartsWith("/"))
                throw new ArgumentException("The specified path must either be empty or begin with a slash ('/') character.", "path");
            _path = path;
        }
        public override string Path { get { return _path; } }
    }

    internal class UrlWithDomainParent : UrlWithNoChanges
    {
        private string _domain;
        private string[] _parentDomains;
        public UrlWithDomainParent(IHttpUrl source)
            : base(source)
        {
            if (source.ParentDomains.Length == 0)
                throw new ArgumentException();
            if (source.ParentDomains.Length == 1)
                _parentDomains = HttpHelper.EmptyStrings;
        }
        public override string Domain
        {
            get
            {
                if (_domain == null)
                    _domain = _source.ParentDomains[_source.ParentDomains.Length - 1] + _source.Domain;
                return _domain;
            }
        }
        public override string[] ParentDomains
        {
            get
            {
                if (_parentDomains == null)
                {
                    _parentDomains = new string[_source.ParentDomains.Length - 1];
                    Array.Copy(_source.ParentDomains, _parentDomains, _parentDomains.Length);
                }
                return _parentDomains;
            }
        }
    }

    internal class UrlWithDomainChild : UrlWithNoChanges
    {
        private readonly string[] _parentDomains;
        public UrlWithDomainChild(IHttpUrl source)
            : base(source)
        {
            _parentDomains = new string[source.ParentDomains.Length + 1];
            Array.Copy(source.ParentDomains, 0, _parentDomains, 0, source.ParentDomains.Length);
            _parentDomains[source.ParentDomains.Length] = source.Domain;
        }
        public override string Domain => "";
        public override string[] ParentDomains => _parentDomains;
    }

    internal class UrlWithPathParent : UrlWithNoChanges
    {
        private string _path;
        private string[] _parentPaths;
        public UrlWithPathParent(IHttpUrl source)
            : base(source)
        {
            if (source.ParentPaths.Length == 0)
                throw new ArgumentException();
            if (source.ParentPaths.Length == 1)
                _parentPaths = HttpHelper.EmptyStrings;
        }
        public override string Path
        {
            get
            {
                if (_path == null)
                    _path = _source.ParentPaths[_source.ParentPaths.Length - 1] + _source.Path;
                return _path;
            }
        }
        public override string[] ParentPaths
        {
            get
            {
                if (_parentPaths == null)
                {
                    _parentPaths = new string[_source.ParentPaths.Length - 1];
                    Array.Copy(_source.ParentPaths, _parentPaths, _parentPaths.Length);
                }
                return _parentPaths;
            }
        }
    }

    internal class UrlWithPathChild : UrlWithNoChanges
    {
        private readonly string[] _parentPaths;
        public UrlWithPathChild(IHttpUrl source)
            : base(source)
        {
            _parentPaths = new string[source.ParentPaths.Length + 1];
            Array.Copy(source.ParentPaths, 0, _parentPaths, 0, source.ParentPaths.Length);
            _parentPaths[source.ParentPaths.Length] = source.Path;
        }
        public override string Path => "";
        public override string[] ParentPaths => _parentPaths;
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
                    _queryString = HttpHelper.MakeQueryString(null, Query);
                return _queryString;
            }
        }
        public override bool HasQuery { get { return _source.HasQuery ? QueryString.Length > 0 : false; } }
        public override void AppendQueryString(StringBuilder sb, bool first)
        {
            if (!_source.HasQuery)
                return;
            if (_queryString != null)
                HttpHelper.AppendQueryString(sb, _queryString, first);
            else
                HttpHelper.AppendQueryString(sb, null, Query, first);
        }
    }

    internal class UrlWithoutQueryMultiple : UrlWithQueryRemovals
    {
        private HashSet<string> _names;
        public UrlWithoutQueryMultiple(IHttpUrl source, HashSet<string> names)
            : base(source)
        {
            if (names == null)
                throw new ArgumentNullException("names");
            _names = names;
        }
        public UrlWithoutQueryMultiple(IHttpUrl source, IEnumerable<string> names) : this(source, new HashSet<string>(names)) { }

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
                return _queryString ?? (_queryString = HttpHelper.MakeQueryString(null, Query));
            }
        }
        public override void AppendQueryString(StringBuilder sb, bool first)
        {
            if (HasQuery)
                HttpHelper.AppendQueryString(sb, QueryString, first);
        }
    }

    internal class UrlWithQuerySingle : UrlWithQueryChanges
    {
        private string _name, _value;
        public UrlWithQuerySingle(IHttpUrl source, string name, string value)
            : base(source)
        {
            if (name == null)
                throw new ArgumentException();
            _name = name;
            _value = value;
        }
        public override bool HasQuery { get { return _value != null ? true : !_source.HasQuery ? false : QueryString.Length > 0; } }
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

    internal class UrlWithQueryMultiple : UrlWithQueryChanges
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
}
