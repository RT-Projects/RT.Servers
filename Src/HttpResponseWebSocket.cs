namespace RT.Servers;

/// <summary>
///     Encapsulates a response to an HTTP request that indicates to the client that they should switch to the WebSocket
///     protocol.</summary>
/// <remarks>
///     Constructor.</remarks>
/// <param name="websocket">
///     The <see cref="WebSocket"/> implementation to use for the remainder of the connection.</param>
/// <param name="subprotocol">
///     The server’s selection of a subprotocol, if the client specified any subprotocols in the request.</param>
/// <param name="headers">
///     Optional HTTP response headers.</param>
public sealed class HttpResponseWebSocket(WebSocket websocket, string subprotocol = null, HttpResponseHeaders headers = null) : HttpResponse(headers ?? new HttpResponseHeaders())
{
    /// <summary>The HTTP status code. For example, 200 OK, 404 Not Found, 500 Internal Server Error. Default is 200 OK.</summary>
    public override HttpStatusCode Status => HttpStatusCode._101_SwitchingProtocols;

    /// <summary>The server’s selection of a subprotocol, if the client specified any subprotocols in the request.</summary>
    public string Subprotocol { get; private set; } = subprotocol;
    /// <summary>The <see cref="WebSocket"/> implementation to use for the remainder of the connection.</summary>
    public WebSocket Websocket { get; private set; } = websocket;
}
