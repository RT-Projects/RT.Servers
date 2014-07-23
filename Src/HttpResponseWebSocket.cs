using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RT.Servers
{
    public sealed class HttpResponseWebSocket : HttpResponse
    {
        /// <summary>The HTTP status code. For example, 200 OK, 404 Not Found, 500 Internal Server Error. Default is 200 OK.</summary>
        public override HttpStatusCode Status { get { return HttpStatusCode._101_SwitchingProtocols; } }

        public HttpResponseWebSocket(WebSocket websocket, string subprotocol = null, HttpResponseHeaders headers = null)
            : base(headers ?? new HttpResponseHeaders())
        {
            Subprotocol = subprotocol;
            Websocket = websocket;
        }

        public string Subprotocol { get; private set; }
        public WebSocket Websocket { get; private set; }
    }
}
