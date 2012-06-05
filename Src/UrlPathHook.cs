using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace RT.Servers
{
    /// <summary>
    /// Encapsulates ways in which a URL path can map to a request handler. Add instances of this class to a <see cref="UrlPathResolver"/>
    /// to hook a handler to a URL path or domain. This class is immutable.
    /// </summary>
    public sealed class UrlPathHook : IEquatable<UrlPathHook>, IComparable<UrlPathHook>
    {
        /// <summary>Gets a value indicating what domain name the handler applies to. Returns null if it applies to all domains.</summary>
        /// <seealso cref="SpecificDomain"/>
        public string Domain { get; private set; }

        /// <summary>Gets a value indicating what port the handler applies to. Returns null if it applies to all ports.</summary>
        public int? Port { get; private set; }

        /// <summary>Gets a value indicating what URL path the handler applies to. Returns null if it applies to all paths.</summary>
        /// <seealso cref="SpecificPath"/>
        public string Path { get; private set; }

        /// <summary>Gets a value indicating whether the handler applies to all subdomains of the domain specified by
        /// <see cref="Domain"/> (false) or the specific domain only (true).</summary>
        public bool SpecificDomain { get; private set; }

        /// <summary>Gets a value indicating whether the handler applies to all subpaths of the path specified by
        /// <see cref="Path"/> (false) or to the specific path only (true).</summary>
        public bool SpecificPath { get; private set; }

        /// <summary>Gets a value indicating whether the handler may be skipped by returning null for response. Skippable handler
        /// hooks may be identical to other hooks.</summary>
        public bool Skippable { get; private set; }

        /// <summary>Gets the request handler for this hook.</summary>
        public Func<HttpRequest, HttpResponse> Handler { get; private set; }

        /// <summary>Initialises a new <see cref="UrlPathHook"/>.</summary>
        /// <param name="handler">The request handler to hook.</param>
        /// <param name="domain">If null, the handler applies to all domain names. Otherwise, the handler applies to this
        /// domain and all subdomains or to this domain only, depending on the value of <paramref name="specificDomain"/>.</param>
        /// <param name="port">If null, the handler applies to all ports; otherwise to the specified port only.</param>
        /// <param name="path">If null, the handler applies to all URL paths. Otherwise, the handler applies to this
        /// path and all subpaths or to this path only, depending on the value of <paramref name="specificPath"/>.</param>
        /// <param name="specificDomain">If false, the handler applies to all subdomains of the domain specified by
        /// <paramref name="domain"/>. Otherwise it applies to the specific domain only.</param>
        /// <param name="specificPath">If false, the handler applies to all subpaths of the path specified by
        /// <paramref name="path"/>. Otherwise it applies to the specific path only.</param>
        /// <param name="skippable">If true, the handler may be skipped by returning null for response.</param>
        public UrlPathHook(Func<HttpRequest, HttpResponse> handler, string domain = null, int? port = null, string path = null, bool specificDomain = false, bool specificPath = false, bool skippable = false)
        {
            if (domain == null && specificDomain)
                throw new ArgumentException("If the specificDomain parameter is set to true, a non-null domain must be specified using the domain parameter.");
            if (domain != null && !Regex.IsMatch(domain, @"^[-.a-z0-9]+$"))
                throw new ArgumentException("The domain specified by the domain parameter must not contain any characters other than lower-case a-z, 0-9, hypen (-) or period (.).");
            if (domain != null && (domain.Contains(".-") || domain.Contains("-.") || domain.StartsWith("-") || domain.EndsWith("-")))
                throw new ArgumentException("The domain specified by the domain parameter must not contain a domain name beginning or ending with a hyphen (-).");
            if (domain != null && !specificDomain && domain.StartsWith("."))
                throw new ArgumentException(@"If the specificDomain parameter is set to false or not specified, the domain specified by the domain parameter must not begin with a period (.). It will, however, be treated as a domain. For example, if you specify the domain ""cream.net"", only domains ending in "".cream.net"" and the domain ""cream.net"" itself are matched. The domain ""scream.net"" would not be considered a match. If you wish to hook the handler to every domain, set domain to null.");
            if (domain != null && (domain.StartsWith(".") || domain.EndsWith(".")))
                throw new ArgumentException(@"The domain specified by the domain parameter must not begin or end with a period (.).");

            if (path == null && specificPath)
                throw new ArgumentException("If the specificPath parameter is set to true, a non-null path must be specified using the path parameter.");
            if (path != null && !Regex.IsMatch(path, @"^/[-;/:@=&$_\.\+!*'\(\),a-zA-Z0-9]*$"))
                throw new ArgumentException("The path specified by the path parameter must not contain any characters that are invalid in URLs, or the question mark (?) character, and it must begin with a slash (/).");
            if (path != null && !specificPath && path.EndsWith("/"))
                throw new ArgumentException(@"If the specificPath parameter is set to false or not specified, the path specified by the path parameter must not end with a slash (/). It will, however, be treated as a directory. For example, if you specify the path ""/files"", only URLs beginning with ""/files/"" and the URL ""/files"" itself are matched. The URL ""/fileshare"" would not be considered a match. If you wish to hook the handler to the root directory of the domain, set path to null.");

            if (handler == null)
                throw new ArgumentException("The handler specified by the handler parameter cannot be null.");
            if (path != null && !path.StartsWith("/"))
                throw new ArgumentException("A path specified by the path parameter must begin with the slash character (\"/\").");
            if (port != null && (port.Value < 1 || port.Value > 65535))
                throw new ArgumentException("The port parameter must contain an integer in the range 1 to 65535 or null.");

            Domain = domain;
            Port = port;
            Path = path;
            SpecificDomain = specificDomain;
            SpecificPath = specificPath;
            Skippable = skippable;
            Handler = handler;
        }

        /// <summary>
        /// Compares this hook to another one, and returns a value indicating which hook should be checked for a match first. The
        /// ordering is such that more specific hooks are checked first, so that they trigger even if a more generic hook encompasses
        /// the specific one. Hence skippable hooks are treated exactly the same, and are only reordered with respect to the single
        /// non-skippable hook that matches the exact same request.
        /// </summary>
        public int CompareTo(UrlPathHook other)
        {
            int result;
            // "any port" handlers match last; other port handlers match in numeric port order
            result = (this.Port ?? int.MaxValue).CompareTo(other.Port ?? int.MaxValue);
            if (result != 0) return result;
            // specific single domains match first
            result = (this.SpecificDomain ? 0 : 1).CompareTo(other.SpecificDomain ? 0 : 1);
            if (result != 0) return result;
            // match more specified domains before less specified ones (e.g. blah.thingy.stuff matches before thingy.stuff)
            // match catch-all domain last
            if (this.Domain != null || other.Domain != null)
            {
                if (this.Domain != null && other.Domain == null)
                    return -1;
                else if (this.Domain == null && other.Domain != null)
                    return 1;
                else if (this.Domain != other.Domain)
                {
                    result = -this.Domain.Length.CompareTo(other.Domain.Length);
                    if (result != 0) return result;
                }
            }
            // specific single paths match first
            result = -this.SpecificPath.CompareTo(other.SpecificPath);
            if (result != 0) return result;
            // match more specific paths before less specific ones (e.g. /blah/thingy/stuff matches before /blah/thingy)
            // match catch-all path last
            if (this.Path == null)
                result = other.Path == null ? 0 : 1;
            else if (other.Path == null)
                result = -1;
            else
                result = -this.Path.Length.CompareTo(other.Path.Length);
            if (result != 0) return result;

            return -this.Skippable.CompareTo(other.Skippable);
        }

        /// <summary>Compares hooks for equality. Two hooks are equal if their match specification is the same; the handler is ignored.</summary>
        public bool Equals(UrlPathHook other)
        {
            if (this.Path != other.Path) return false;
            if (this.SpecificPath != other.SpecificPath) return false;
            if (this.Domain != null || other.Domain != null) // string.Equals of two nulls is, unfortunately, false.
                if (string.Equals(this.Domain, other.Domain, StringComparison.OrdinalIgnoreCase)) return false;
            if (this.SpecificDomain != other.SpecificDomain) return false;
            if (this.Port != other.Port) return false;
            if (this.Skippable != other.Skippable) return false;
            return true;
        }

        /// <summary>Compares hooks for equality. Two hooks are equal if their match specification is the same; the handler is ignored.</summary>
        public override bool Equals(object obj)
        {
            var other = obj as UrlPathHook;
            if (other == null)
                return false; // could be an actual null or just a different type
            return this.Equals(other);
        }

        /// <summary>Computes a hash code suitable as a heuristic for the kind of equality defined by <see cref="Equals(object)"/>.</summary>
        public override int GetHashCode()
        {
            return unchecked(
                (Path == null ? -47 : Path.GetHashCode()) * 1669 +
                (Domain == null ? -53 : Domain.GetHashCode()) * 2089 +
                (Port == null ? -59 : Port.Value) * 2819 +
                (SpecificPath ? 7919 : 5237) +
                (SpecificDomain ? 6709 : 4079)
            );
        }

        /// <summary>Returns a debugging-friendly representation of this hook's match specification.</summary>
        public override string ToString()
        {
            return (Domain == null ? "http://*" : ((SpecificDomain ? "http://" : "http://*.") + Domain))
                + (Port == null ? "" : (":" + Port))
                + (Path == null ? "/*" : (Path + (SpecificPath ? "" : "/*")))
                + (Skippable ? " (skippable)" : "");
        }
    }
}
