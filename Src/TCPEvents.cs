
namespace RT.Servers.TCPEvents
{
    /// <summary>
    /// Provides a callback function to call when a <see cref="TCPClient"/> receives data.
    /// </summary>
    /// <param name="Sender">The <see cref="TCPClient"/> that received the data.</param>
    /// <param name="Data">A buffer containing the data received. The buffer may be larger than the received data.</param>
    /// <param name="BytesReceived">The number of bytes received. The data is located at the beginning of the Data array.</param>
    public delegate void DataEventHandler(object Sender, byte[] Data, int BytesReceived);

    /// <summary>
    /// Provides a callback function to call when a <see cref="TCPServer"/> receives a new incoming TCP connection.
    /// </summary>
    /// <param name="Sender">The <see cref="TCPServer"/> that received the incoming connection.</param>
    /// <param name="Socket">An object encapsulating the new connection.</param>
    public delegate void ConnectionEventHandler(object Sender, TCPClient Socket);
}
