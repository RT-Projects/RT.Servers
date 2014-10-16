using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RT.Servers
{
    /// <summary>
    ///     Encapsulates a response to an HTTP request that indicates to the client that they should switch to the WebSocket
    ///     protocol.</summary>
    public sealed class HttpResponseWebSocket : HttpResponse
    {
        /// <summary>The HTTP status code. For example, 200 OK, 404 Not Found, 500 Internal Server Error. Default is 200 OK.</summary>
        public override HttpStatusCode Status { get { return HttpStatusCode._101_SwitchingProtocols; } }

        /// <summary>
        ///     Constructor.</summary>
        /// <param name="websocket">
        ///     The <see cref="WebSocket"/> implementation to use for the remainder of the connection.</param>
        /// <param name="subprotocol">
        ///     The server’s selection of a subprotocol, if the client specified any subprotocols in the request.</param>
        /// <param name="headers">
        ///     Optional HTTP response headers.</param>
        public HttpResponseWebSocket(WebSocket websocket, string subprotocol = null, HttpResponseHeaders headers = null)
            : base(headers ?? new HttpResponseHeaders())
        {
            Subprotocol = subprotocol;
            Websocket = websocket;
        }

        /// <summary>The server’s selection of a subprotocol, if the client specified any subprotocols in the request.</summary>
        public string Subprotocol { get; private set; }
        /// <summary>The <see cref="WebSocket"/> implementation to use for the remainder of the connection.</summary>
        public WebSocket Websocket { get; private set; }
    }
}
