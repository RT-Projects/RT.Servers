using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RT.Servers
{
    /// <summary>
    ///     Encapsulates a mapping from URLs to a request handler. Add instances of this class to a <see cref="UrlResolver"/>
    ///     to map URL paths or domains to request handlers. This class is immutable.</summary>
    public sealed class UrlMapping : IEquatable<UrlMapping>, IComparable<UrlMapping>
    {
        /// <summary>Gets the hook that determines which URLs can trigger the <see cref="Handler"/>.</summary>
        public UrlHook Hook { get; private set; }

        /// <summary>
        ///     Gets a value indicating whether the <see cref="Handler"/> may return <c>null</c>, in which case it will be
        ///     skipped and another applicable handler executed instead. Skippable handlers may have a hook identical to that
        ///     of other handlers.</summary>
        public bool Skippable { get; private set; }

        /// <summary>Gets the request handler for this mapping.</summary>
        public Func<HttpRequest, HttpResponse> Handler { get; private set; }

        /// <summary>
        ///     Initialises a new <see cref="UrlMapping"/>.</summary>
        /// <param name="hook">
        ///     The URL properties to map from.</param>
        /// <param name="handler">
        ///     The request handler to map to.</param>
        /// <param name="skippable">
        ///     If <c>true</c>, the handler may be skipped if it returns <c>null</c>.</param>
        public UrlMapping(UrlHook hook, Func<HttpRequest, HttpResponse> handler, bool skippable = false)
        {
            if (hook == null)
                throw new ArgumentNullException("mapping");
            if (handler == null)
                throw new ArgumentNullException("handler");

            Hook = hook;
            Handler = handler;
            Skippable = skippable;
        }

        /// <summary>
        ///     Initialises a new <see cref="UrlMapping"/>.</summary>
        /// <param name="handler">
        ///     The request handler to map to.</param>
        /// <param name="domain">
        ///     If <c>null</c>, the mapping applies to all domain names. Otherwise, the mapping applies to this domain and all
        ///     subdomains or to this domain only, depending on the value of <paramref name="specificDomain"/>.</param>
        /// <param name="port">
        ///     If <c>null</c>, the mapping applies to all ports; otherwise to the specified port only.</param>
        /// <param name="path">
        ///     If <c>null</c>, the mapping applies to all URL paths. Otherwise, the mapping applies to this path and all
        ///     subpaths or to this path only, depending on the value of <paramref name="specificPath"/>.</param>
        /// <param name="specificDomain">
        ///     If <c>false</c>, the mapping applies to all subdomains of the domain specified by <paramref name="domain"/>.
        ///     Otherwise it applies to the specific domain only.</param>
        /// <param name="specificPath">
        ///     If <c>false</c>, the mapping applies to all subpaths of the path specified by <paramref name="path"/>.
        ///     Otherwise it applies to the specific path only.</param>
        /// <param name="skippable">
        ///     If <c>true</c>, the handler may be skipped if it returns <c>null</c>.</param>
        public UrlMapping(Func<HttpRequest, HttpResponse> handler, string domain = null, int? port = null, string path = null, bool specificDomain = false, bool specificPath = false, bool skippable = false)
        {
            if (handler == null)
                throw new ArgumentNullException("handler");

            Hook = new UrlHook(domain, port, path, specificDomain, specificPath);
            Handler = handler;
            Skippable = skippable;
        }

        /// <summary>
        ///     Compares this mapping to another one, and returns a value indicating which mapping should be checked for a
        ///     match first. The ordering is such that more specific mappings are checked first, so that they trigger even if
        ///     a more generic mapping encompasses the specific one. Hence skippable mappings are treated exactly the same,
        ///     and are only reordered with respect to the single non-skippable mapping that matches the exact same request.</summary>
        public int CompareTo(UrlMapping other)
        {
            var inner = Hook.CompareTo(other.Hook);
            if (inner != 0)
                return inner;
            return -Skippable.CompareTo(other.Skippable);
        }

        /// <summary>
        ///     Compares mappings for equality. Two mappings are equal if <see cref="Hook"/> and <see cref="Skippable"/> are
        ///     equal; <see cref="Handler"/> is ignored.</summary>
        /// <seealso cref="UrlHook.Equals(UrlHook)"/>
        public bool Equals(UrlMapping other)
        {
            return Hook.Equals(other.Hook) && (Skippable == other.Skippable);
        }

        /// <summary>
        ///     Compares mappings for equality. Two mappings are equal if <see cref="Hook"/> and <see cref="Skippable"/> are
        ///     equal; <see cref="Handler"/> is ignored.</summary>
        public override bool Equals(object obj)
        {
            var other = obj as UrlMapping;
            if (other == null)
                return false; // could be an actual null or just a different type
            return this.Equals(other);
        }

        /// <summary>
        ///     Computes a hash code suitable as a heuristic for the kind of equality defined by <see cref="Equals(object)"/>.</summary>
        public override int GetHashCode()
        {
            return unchecked(Hook.GetHashCode() + (Skippable ? 13421 : 8161));
        }

        /// <summary>Returns a debugging-friendly representation of this hook's match specification.</summary>
        public override string ToString()
        {
            return Hook.ToString() + (Skippable ? " (skippable)" : "");
        }
    }
}
