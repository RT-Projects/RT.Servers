using System;
using System.Net;
using System.Net.Sockets;
using RT.Util.ExtensionMethods;
using RT.Serialization;

namespace RT.Servers
{
    /// <summary>Contains endpoint information that specifies where the HTTP server will listen for incoming connections.</summary>
    public class HttpEndpoint : IEquatable<HttpEndpoint>, IClassifyObjectProcessor
    {
        /// <summary>
        ///     The IP address of the interface to which the HTTP server should bind, or <c>null</c> to let the server listen
        ///     on all network interfaces.</summary>
        /// <remarks>
        ///     This is a string rather than System.Net.IPAddress so that Classify can serialize it. If the contents don’t
        ///     parse, <c>null</c> is assumed.</remarks>
        public string BindAddress { get; set; }

        /// <summary>The port on which the server should listen for connections.</summary>
        public int Port { get; set; }

        /// <summary>
        ///     Specifies whether this should be a secured (HTTPS) connection. The X509 certificate defined in the <see
        ///     cref="HttpServerOptions"/> must not be <c>null</c>.</summary>
        public bool Secure { get; set; }

        // used to store socket in HttpServer
        [ClassifyIgnore]
        internal Socket Socket { get; set; }

        private HttpEndpoint() // for Classify
        {
        }

        /// <summary>
        ///     Creates an instance of HttpEndpoint.</summary>
        /// <param name="bindAddress">
        ///     The hostname/address to listen on, or null to listen on all interfaces.</param>
        /// <param name="port">
        ///     The port to listen on.</param>
        /// <param name="secure">
        ///     Specifies whether this is a secure (HTTPS) endpoint.</param>
        public HttpEndpoint(string bindAddress, int port, bool secure = false)
        {
            IPAddress addr = null;
            if (bindAddress != null && !IPAddress.TryParse(bindAddress, out addr))
                throw new ArgumentException("'{0}' is not a valid IP address to listen on. Pass null to listen on all interfaces.".Fmt(bindAddress));

            BindAddress = bindAddress == null ? null : addr.ToString();
            Port = port;
            Secure = secure;
        }

        /// <summary>
        ///     Compares two <see cref="HttpEndpoint"/> objects for equality.</summary>
        /// <param name="other">
        ///     The other object to compare against.</param>
        public bool Equals(HttpEndpoint other)
        {
            return other.BindAddress == BindAddress && other.Port == Port && other.Secure == Secure;
        }

        /// <summary>Returns a human-readable representation of this endpoint.</summary>
        public override string ToString()
        {
            return (BindAddress ?? "*") + ":" + Port + (Secure ? "/secure" : "");
        }

        /// <summary>Returns a hash value for this object.</summary>
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (BindAddress == null ? 0 : BindAddress.GetHashCode() * 63689) + Port;
                if (Secure)
                    hash *= 3;
                return hash;
            }
        }

        void IClassifyObjectProcessor.BeforeSerialize() { }

        void IClassifyObjectProcessor.AfterDeserialize()
        {
            IPAddress addr = null;
            if (BindAddress != null && IPAddress.TryParse(BindAddress, out addr))
                BindAddress = addr.ToString();
            else
                BindAddress = null;
        }
    }
}
