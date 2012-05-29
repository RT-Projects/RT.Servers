using System;
using System.Collections.Generic;
using System.Linq;
using RT.Util.ExtensionMethods;

namespace RT.Servers
{
    /// <summary>
    /// Encapsulates an <see cref="HttpRequest"/> which was resolved by the <see cref="UrlPathResolver"/> and contains the
    /// results of this resolution.
    /// </summary>
    public sealed class UrlPathRequest : HttpRequest
    {
        private string _restUrl;
        private string _restDomain;

        /// <summary>Initialises a new UrlPathRequest from the specified HTTP request.</summary>
        public UrlPathRequest(HttpRequest copyFrom, string baseUrl, string restUrl, string baseDomain, string restDomain)
            : base(copyFrom)
        {
            BaseUrl = baseUrl;
            _restUrl = restUrl;
            BaseDomain = baseDomain;
            _restDomain = restDomain;
        }

        /// <summary>
        /// Contains the part of the URL that follows the path where the request handler is hooked.
        /// <see cref="BaseUrl"/> + Url is equal to <see cref="OriginalUrl"/>.
        /// </summary>
        /// <example>
        ///     Consider the following example code:
        ///     <code>
        ///         var myResolver = new UrlPathResolver();
        ///         myResolver.Add(new UrlPathHook(myHandler, path: "/homepages"));
        ///     </code>
        ///     In the above example, an HTTP request for the URL <c>http://www.mydomain.com/homepages/a/adam</c>
        ///     would have <see cref="BaseUrl"/> set to <c>/homepages</c> and Url set to <c>/a/adam</c>. Note the leading slashes.
        /// </example>
        public override string Url { get { return _restUrl; } }

        /// <summary>
        /// Contains the part of the URL to which the request handler is hooked.
        /// BaseUrl + <see cref="Url"/> is equal to <see cref="OriginalUrl"/>.
        /// For an example, see <see cref="Url"/>.
        /// </summary>
        public string BaseUrl { get; internal set; }

        /// <summary>Gets the part of the full URL after the domain name (including query parameters).</summary>
        public new string OriginalUrl { get { return BaseUrl + OriginalUrl; } }

        /// <summary>Gets the part of the full URL after the domain name, but without the query parameters.</summary>
        public new string OriginalUrlWithoutQuery { get { return BaseUrl + OriginalUrlWithoutQuery; } }

        /// <summary>
        /// Contains the part of the domain that precedes the domain where the request handler is hooked.
        /// Domain + <see cref="BaseDomain"/> is equal to <see cref="FullDomain"/>.
        /// </summary>
        /// <example>
        ///     <para>Consider the following example code:</para>
        ///     <code>
        ///         var myResolver = new UrlPathResolver();
        ///         myResolver.Add(new UrlPathHook(myHandler, domain: "homepages.mydomain.com"));
        ///     </code>
        ///     <para>In the above example, an HTTP request for the URL <c>http://peter.schmidt.homepages.mydomain.com/</c>
        ///     would have the Domain set to the value <c>peter.schmidt.</c> (note the trailing dot) and the
        ///     <see cref="BaseDomain"/> to <c>homepages.mydomain.com</c>.</para>
        /// </example>
        public string Domain { get { return _restDomain; } }

        /// <summary>
        /// Contains the part of the domain to which the request handler is hooked.
        /// <see cref="Domain"/> + BaseDomain is equal to <see cref="FullDomain"/>.
        /// For an example see <see cref="Domain"/>.
        /// </summary>
        public string BaseDomain { get; private set; }

        /// <summary>Gets the full domain name.</summary>
        public new string OriginalDomain { get { return BaseDomain + _restDomain; } }

        /// <summary>Applies the specified modifications to this request's URL and returns the result.</summary>
        /// <param name="qsAddOrReplace">Replaces existing query-string parameters, or adds them if they are not already in the URL.</param>
        /// <param name="qsRemove">Removes the specified query-string parameters.</param>
        /// <param name="restUrl">Replaces the <see cref="UrlWithoutQuery"/> with the specified new value.</param>
        /// <param name="baseUrl">Replaces the <see cref="BaseUrl"/> with the specified new value.</param>
        /// <returns>The resulting URL after the transformation, without domain but with a leading slash.</returns>
        public string SameUrlExcept(Dictionary<string, string> qsAddOrReplace = null, string[] qsRemove = null, string restUrl = null, string baseUrl = null)
        {
            var url = (baseUrl ?? BaseUrl) + (restUrl ?? OriginalUrlWithoutQuery);
            var newQs = Get
                .Where(g => (qsRemove == null || !qsRemove.Contains(g.Key)) && (qsAddOrReplace == null || !qsAddOrReplace.ContainsKey(g.Key)))
                .SelectMany(qs => qs.Value.Select(q => new KeyValuePair<string, string>(qs.Key, q)));
            if (qsAddOrReplace != null)
                newQs = newQs.Concat(qsAddOrReplace);
            return newQs.Any()
                ? url + '?' + newQs.Select(q => q.Key.UrlEscape() + '=' + q.Value.UrlEscape()).JoinString("&")
                : url;
        }
    }
}
