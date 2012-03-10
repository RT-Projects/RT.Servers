using System;
using System.Collections.Generic;
using System.Linq;
using RT.Util.ExtensionMethods;

namespace RT.Servers
{
    public sealed class UrlPathRequest : HttpRequest
    {
        /// <summary>Initialises a new UrlPathRequest from the specified HTTP request.</summary>
        public UrlPathRequest(HttpRequest copyFrom, string baseUrl, string restUrl, string baseDomain, string restDomain)
            : base(copyFrom)
        {
            BaseUrl = baseUrl;
            RestUrl = restUrl;
            BaseDomain = baseDomain;
            RestDomain = restDomain;
        }

        /// <summary>
        /// Contains the part of the URL that follows the path where the request handler is hooked.
        /// <see cref="BaseUrl"/> + RestUrl is equal to <see cref="Url"/>.
        /// </summary>
        /// <example>
        ///     Consider the following example code:
        ///     <code>
        ///         HttpServer MyServer = new HttpServer();
        ///         MyServer.AddHandler(new HttpRequestHandlerHook { Path = "/homepages", Handler = MyHandler });
        ///     </code>
        ///     In the above example, an HTTP request for the URL <c>http://www.mydomain.com/homepages/a/adam</c>
        ///     would have BaseURL set to <c>/homepages</c> and RestURL set to <c>/a/adam</c>. Note the leading slashes.
        /// </example>
        public string RestUrl { get; internal set; }

        /// <summary>
        /// Contains the part of the URL to which the request handler is hooked.
        /// BaseUrl + <see cref="RestUrl"/> is equal to <see cref="Url"/>.
        /// For an example, see <see cref="RestUrl"/>.
        /// </summary>
        public string BaseUrl { get; internal set; }

        /// <summary>
        /// Contains the part of the domain that precedes the domain where the request handler is hooked.
        /// RestDomain + <see cref="BaseDomain"/> is equal to <see cref="Domain"/>.
        /// </summary>
        /// <example>
        ///     Consider the following example code:
        ///     <code>
        ///         HttpServer MyServer = new HttpServer();
        ///         MyServer.AddHandler(new HttpRequestHandlerHook { Domain = "homepages.mydomain.com", Handler = MyHandler });
        ///     </code>
        ///     In the above example, an HTTP request for the URL <c>http://peter.schmidt.homepages.mydomain.com/</c>
        ///     would have the RestDomain field set to the value <c>peter.schmidt.</c>. Note the trailing dot.
        /// </example>
        public string RestDomain { get; internal set; }

        /// <summary>
        /// Contains the part of the domain to which the request handler is hooked.
        /// <see cref="RestDomain"/> + BaseDomain is equal to <see cref="Domain"/>.
        /// For an example see <see cref="RestDomain"/>.
        /// </summary>
        public string BaseDomain { get; internal set; }

        /// <summary>
        /// The <see cref="RestUrl"/> (q.v.) of the request, not including the domain or any GET query parameters.
        /// </summary>
        public string RestUrlWithoutQuery
        {
            get { return RestUrl.Contains('?') ? RestUrl.Remove(RestUrl.IndexOf('?')) : RestUrl; }
        }

        /// <summary>Applies the specified modifications to this request's URL and returns the result.</summary>
        /// <param name="qsAddOrReplace">Replaces existing query-string parameters, or adds them if they are not already in the URL.</param>
        /// <param name="qsRemove">Removes the specified query-string parameters.</param>
        /// <param name="restUrl">Replaces the <see cref="RestUrlWithoutQuery"/> with the specified new value.</param>
        /// <param name="baseUrl">Replaces the <see cref="BaseUrl"/> with the specified new value.</param>
        /// <returns>The resulting URL after the transformation, without domain but with a leading slash.</returns>
        public new string SameUrlExcept(Dictionary<string, string> qsAddOrReplace = null, string[] qsRemove = null, string restUrl = null, string baseUrl = null)
        {
            var url = (baseUrl ?? BaseUrl) + (restUrl ?? RestUrlWithoutQuery);
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
