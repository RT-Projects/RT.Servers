using System;
using System.Linq;
using System.Text.RegularExpressions;
using RT.Util;
using RT.Util.Serialization;
using RT.Util.Xml;

namespace RT.Servers
{
    /// <summary>
    ///     Encapsulates properties of a URL that can be mapped to a request handler using <see cref="UrlMapping"/>. This
    ///     class is immutable.</summary>
    [ClassifyIgnoreIfDefault, XmlIgnoreIfDefault]
    public sealed class UrlHook : IEquatable<UrlHook>, IComparable<UrlHook>, IXmlClassifyProcess, IClassifyXmlObjectProcessor
    {
        /// <summary>
        ///     Gets a value indicating what domain name the hook applies to. Returns <c>null</c> if it applies to all
        ///     domains.</summary>
        /// <seealso cref="SpecificDomain"/>
        public string Domain { get; private set; }

        /// <summary>Gets a value indicating what port the hook applies to. Returns <c>null</c> if it applies to all ports.</summary>
        public int? Port { get; private set; }

        /// <summary>
        ///     Gets a value indicating what URL path the hook applies to. Returns <c>null</c> if it applies to all paths.</summary>
        /// <seealso cref="SpecificPath"/>
        public string Path { get; private set; }

        /// <summary>
        ///     Gets a value indicating whether the hook applies to all subdomains of the domain specified by <see
        ///     cref="Domain"/> (<c>false</c>) or the specific domain only (<c>true</c>).</summary>
        public bool SpecificDomain { get; private set; }

        /// <summary>
        ///     Gets a value indicating whether the hook applies to all subpaths of the path specified by <see cref="Path"/>
        ///     (<c>false</c>) or to the specific path only (<c>true</c>).</summary>
        public bool SpecificPath { get; private set; }

        /// <summary>Gets a value indicating the protocol(s) to which the hook applies.</summary>
        public Protocols Protocols { get; private set; }

        /// <summary>
        ///     Initialises a new <see cref="UrlHook"/>.</summary>
        /// <param name="domain">
        ///     If <c>null</c>, the hook applies to all domain names. Otherwise, the hook applies to this domain and all
        ///     subdomains or to this domain only, depending on the value of <paramref name="specificDomain"/>.</param>
        /// <param name="port">
        ///     If <c>null</c>, the hook applies to all ports; otherwise to the specified port only.</param>
        /// <param name="path">
        ///     If <c>null</c>, the hook applies to all URL paths. Otherwise, the hook applies to this path and all subpaths
        ///     or to this path only, depending on the value of <paramref name="specificPath"/>.</param>
        /// <param name="specificDomain">
        ///     If <c>false</c>, the hook applies to all subdomains of the domain specified by <paramref name="domain"/>.
        ///     Otherwise it applies to the specific domain only.</param>
        /// <param name="specificPath">
        ///     If <c>false</c>, the hook applies to all subpaths of the path specified by <paramref name="path"/>. Otherwise
        ///     it applies to the specific path only.</param>
        /// <param name="protocols">
        ///     Specifies the protocol(s) to hook to. Default is all supported protocols.</param>
        public UrlHook(string domain = null, int? port = null, string path = null, bool specificDomain = false, bool specificPath = false, Protocols protocols = Protocols.All)
        {
            Domain = domain;
            Port = port;
            Path = path;
            SpecificDomain = specificDomain;
            SpecificPath = specificPath;
            Protocols = protocols;
            checkValues();
        }

        private void checkValues()
        {
            if (Protocols == Protocols.None)
                throw new ArgumentException("The Protocols parameter cannot be None.");
            if ((Protocols & ~Protocols.All) != 0)
                throw new ArgumentException("The Protocols parameter has an invalid value.");
            if (Domain == null && SpecificDomain)
                throw new ArgumentException("If SpecificDomain is true, Domain must not be null.");
            if (Domain != null && !Regex.IsMatch(Domain, @"^[-.a-z0-9]+$"))
                throw new ArgumentException("Domain must not contain any characters other than lower-case a-z, 0-9, hypen (-) or period (.).");
            if (Domain != null && (Domain.Contains(".-") || Domain.Contains("-.") || Domain.StartsWith("-") || Domain.EndsWith("-")))
                throw new ArgumentException("Domain must not contain a Domain name beginning or ending with a hyphen (-).");
            if (Domain != null && !SpecificDomain && Domain.StartsWith("."))
                throw new ArgumentException(@"If SpecificDomain is false, Domain must not begin with a period (.). It will, however, be treated as a domain. For example, if you specify the Domain ""cream.net"", only domains ending in "".cream.net"" and the domain ""cream.net"" itself are matched. The domain ""scream.net"" would not be considered a match. If you wish to hook to every domain, set Domain to null.");
            if (Domain != null && (Domain.StartsWith(".") || Domain.EndsWith(".")))
                throw new ArgumentException(@"Domain must not begin or end with a period (.).");

            if (Path == null && SpecificPath)
                throw new ArgumentException("If SpecificPath is true, Path must not be null.");
            if (Path != null && !Regex.IsMatch(Path, @"^/[-;/:@=&$_\.\+!*'\(\),a-zA-Z0-9]*$"))
                throw new ArgumentException("Path must not contain any characters that are invalid in URLs, or the question mark (?) character, and it must begin with a slash (/).");
            if (Path != null && !SpecificPath && Path.EndsWith("/"))
                throw new ArgumentException(@"If SpecificPath is false, Path must not end with a slash (/). It will, however, be treated as a directory. For example, if you specify the path ""/files"", only URLs beginning with ""/files/"" and the URL ""/files"" itself are matched. The URL ""/fileshare"" would not be considered a match. If you wish to hook to the root directory of the domain, set Path to null.");

            if (Path != null && !Path.StartsWith("/"))
                throw new ArgumentException("Path must be null or begin with the slash character (\"/\").");
            if (Port != null && (Port.Value < 1 || Port.Value > 65535))
                throw new ArgumentException("Port must be null or contain an integer in the range 1 to 65535.");
        }

        // For Classify
        private UrlHook()
        {
            // Sensible default
            Protocols = Protocols.All;
        }

        /// <summary>
        ///     Compares this hook to another one such that more specific hooks are sorted before a more generic hook that
        ///     encompasses the specific one.</summary>
        public int CompareTo(UrlHook other)
        {
            int result;

            // "any port" hooks match last; other port hooks match in numeric port order
            result = (this.Port ?? int.MaxValue).CompareTo(other.Port ?? int.MaxValue);
            if (result != 0) return result;

            // specific single domains match first
            result = (this.SpecificDomain ? 0 : 1).CompareTo(other.SpecificDomain ? 0 : 1);
            if (result != 0) return result;

            // match more specified domains before less specified ones (e.g. blah.thingy.stuff matches before thingy.stuff)
            // match catch-all domain last
            if (this.Domain != null && other.Domain == null)
                return -1;
            else if (this.Domain == null && other.Domain != null)
                return 1;
            else if (this.Domain != other.Domain)
            {
                result = -this.Domain.Length.CompareTo(other.Domain.Length);
                if (result != 0) return result;
            }

            // specific single paths match first
            result = -this.SpecificPath.CompareTo(other.SpecificPath);
            if (result != 0) return result;

            // match more specific paths before less specific ones (e.g. /blah/thingy/stuff matches before /blah/thingy)
            // match catch-all path last
            if (this.Path != null && other.Path == null)
                return -1;
            else if (this.Path == null && other.Path != null)
                return 1;
            else if (this.Path != other.Path)
            {
                result = -this.Path.Length.CompareTo(other.Path.Length);
                if (result != 0) return result;
            }

            // more restricted sets of protocols match first
            return countBits((int) this.Protocols).CompareTo(countBits((int) other.Protocols));
        }

        /// <summary>Returns the number of 1-bits in the input integer.</summary>
        private int countBits(int input)
        {
            if (input < 0)
                throw new ArgumentException("Negative integers not supported.", "input");
            var result = 0;
            while (input != 0)
            {
                result++;
                input &= input - 1; // removes exactly one 1-bit (the least significant one)
            }
            return result;
        }

        /// <summary>Compares mappings for equality.</summary>
        public bool Equals(UrlHook other)
        {
            if (this.Path != other.Path) return false;
            if (this.SpecificPath != other.SpecificPath) return false;
            if (this.Domain != null || other.Domain != null) // string.Equals of two nulls is, unfortunately, false.
                if (!string.Equals(this.Domain, other.Domain, StringComparison.OrdinalIgnoreCase)) return false;
            if (this.SpecificDomain != other.SpecificDomain) return false;
            if (this.Port != other.Port) return false;
            if (this.Protocols != other.Protocols) return false;
            return true;
        }

        /// <summary>Compares mappings for equality.</summary>
        public override bool Equals(object obj)
        {
            var other = obj as UrlHook;
            if (other == null)
                return false; // could be an actual null or just a different type
            return this.Equals(other);
        }

        /// <summary>
        ///     Computes a hash code suitable as a heuristic for the kind of equality defined by <see cref="Equals(object)"/>.</summary>
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
            return (Protocols == Protocols.All ? "http[s]://" : Protocols == Protocols.Http ? "http://" : "https://")
                + (Domain == null ? "*" : (SpecificDomain ? Domain : "*." + Domain))
                + (Port == null ? null : (":" + Port))
                + (Path == null ? "/*" : (SpecificPath ? Path : Path + "/*"));
        }

        // Check validity after deserialization
        void IXmlClassifyProcess.AfterXmlDeclassify() { checkValues(); }
        void IClassifyObjectProcessor<System.Xml.Linq.XElement>.AfterDeserialize(System.Xml.Linq.XElement element) { checkValues(); }

        // These do nothing
        void IXmlClassifyProcess.BeforeXmlClassify() { }
        void IClassifyObjectProcessor<System.Xml.Linq.XElement>.AfterSerialize(System.Xml.Linq.XElement element) { }
        void IClassifyObjectProcessor<System.Xml.Linq.XElement>.BeforeSerialize() { }
        void IClassifyObjectProcessor<System.Xml.Linq.XElement>.BeforeDeserialize(System.Xml.Linq.XElement element) { }
    }
}
