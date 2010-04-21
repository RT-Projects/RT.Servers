using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace RT.Servers
{
    /// <summary>
    /// Encapsulates the various ways in which a URL can map to a request handler. Add instances of this class to <see cref="HttpServer.RequestHandlerHooks"/>
    /// to hook a handler to a specific <see cref="HttpServer"/> instance. This class is immutable.
    /// </summary>
    public class HttpRequestHandlerHook
    {
        /// <summary>Gets a value indicating what domain name the handler applies to. Returns null if it applies to all domains.</summary>
        /// <seealso cref="SpecificDomain"/>
        public string Domain { get { return _domain; } }
        private string _domain;

        /// <summary>Gets a value indicating what port the handler applies to. Returns null if it applies to all ports.</summary>
        public int? Port { get { return _port; } }
        private int? _port;

        /// <summary>Gets a value indicating what URL path the handler applies to. Returns null if it applies to all paths.</summary>
        /// <seealso cref="SpecificPath"/>
        public string Path { get { return _path; } }
        private string _path;

        /// <summary>Gets a value indicating whether the handler applies to all subdomains of the domain specified by
        /// <see cref="Domain"/> (false) or the specific domain only (true).</summary>
        public bool SpecificDomain { get { return _specificDomain; } }
        private bool _specificDomain;

        /// <summary>Gets a value indicating whether the handler applies to all subpaths of the path specified by
        /// <see cref="Path"/> (false) or to the specific path only (true).</summary>
        public bool SpecificPath { get { return _specificPath; } }
        private bool _specificPath;

        /// <summary>Gets the request handler for this hook.</summary>
        public HttpRequestHandler Handler { get { return _handler; } }
        private HttpRequestHandler _handler;

        /// <summary>Initialises a new <see cref="HttpRequestHandlerHook"/>.</summary>
        /// <param name="domain">If null, the handler applies to all domain names. Otherwise, the handler applies to this
        /// domain and all subdomains or to this domain only, depending on the value of <paramref name="specificDomain"/>.</param>
        /// <param name="port">If null, the handler applies to all ports; otherwise to the specified port only.</param>
        /// <param name="path">If null, the handler applies to all URL paths. Otherwise, the handler applies to this
        /// path and all subpaths or to this path only, depending on the value of <paramref name="specificPath"/>.</param>
        /// <param name="specificDomain">If false, the handler applies to all subdomains of the domain specified by
        /// <paramref name="domain"/>. Otherwise it applies to the specific domain only.</param>
        /// <param name="specificPath">If false, the handler applies to all subpaths of the path specified by
        /// <paramref name="path"/>. Otherwise it applies to the specific path only.</param>
        /// <param name="handler">The request handler to hook.</param>
        public HttpRequestHandlerHook(HttpRequestHandler handler, string domain = null, int? port = null, string path = null, bool specificDomain = false, bool specificPath = false)
        {
            if (domain == null && specificDomain)
                throw new ArgumentException("If the specificDomain parameter is set to true, a non-null domain must be specified using the domain parameter.");
            if (domain != null && !Regex.IsMatch(domain, @"^[-.a-z0-9]+$"))
                throw new ArgumentException("The domain specified by the domain parameter must not contain any characters other than lower-case a-z, 0-9, hypen (-) or period (.).");
            if (domain != null && (domain.Contains(".-") || domain.Contains("-.") || domain.StartsWith("-") || domain.EndsWith("-")))
                throw new ArgumentException("The domain specified by the domain parameter must not contain a domain name beginning or ending with a hyphen (-).");
            if (domain != null && !specificDomain && domain.StartsWith("."))
                throw new ArgumentException(@"If the specificDomain parameter is set to false or not specified, the domain specified by the domain parameter must not begin with a period (.). It will, however, be treated as a domain. For example, if you specify the domain ""cream.net"", only domains ending in "".cream.net"" and the domain ""cream.net"" itself are matched. The domain ""scream.net"" would not be considered a match. If you wish to hook the handler to every domain, use one of the constructors that omit the domain parameter.");
            if (domain != null && (domain.StartsWith(".") || domain.EndsWith(".")))
                throw new ArgumentException(@"The domain specified by the domain parameter must not begin or end with a period (.).");

            if (path == null && specificPath)
                throw new ArgumentException("If the specificPath parameter is set to true, a non-null path must be specified using the path parameter.");
            if (path != null && !Regex.IsMatch(path, @"^/[-;/:@=&$_\.\+!*'\(\),a-zA-Z0-9]*$"))
                throw new ArgumentException("The path specified by the path parameter must not contain any characters that are invalid in URLs, or the question mark (?) character, and it must begin with a slash (/).");
            if (path != null && !specificPath && path.EndsWith("/"))
                throw new ArgumentException(@"If the specificPath parameter is set to false or not specified, the path specified by the path parameter must not end with a slash (/). It will, however, be treated as a directory. For example, if you specify the path ""/files"", only URLs beginning with ""/files/"" and the URL ""/files"" itself are matched. The URL ""/fileshare"" would not be considered a match. If you wish to hook the handler to the root directory of the domain, use one of the constructors that omit the path parameter.");

            if (handler == null)
                throw new ArgumentException("The handler specified by the handler parameter cannot be null.");
            if (path != null && !path.StartsWith("/"))
                throw new ArgumentException("A path specified by the path parameter must begin with the slash character (\"/\").");
            if (port != null && (port.Value < 1 || port.Value > 65535))
                throw new ArgumentException("The port parameter must contain an integer in the range 1 to 65535 or null.");

            _domain = domain;
            _port = port;
            _path = path;
            _specificDomain = specificDomain;
            _specificPath = specificPath;
            _handler = handler;
        }
    }
}