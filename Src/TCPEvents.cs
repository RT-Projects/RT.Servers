
namespace RT.Servers.TcpEvents
{
    /// <summary>
    /// Provides a callback function to call when a <see cref="TcpClientWithEvents"/> receives data.
    /// </summary>
    /// <param name="sender">The <see cref="TcpClientWithEvents"/> that received the data.</param>
    /// <param name="data">A buffer containing the data received. The buffer may be larger than the received data.</param>
    /// <param name="bytesReceived">The number of bytes received. The data is located at the beginning of the Data array.</param>
    public delegate void DataEventHandler(object sender, byte[] data, int bytesReceived);

    /// <summary>
    /// Provides a callback function to call when a <see cref="TcpServer"/> receives a new incoming TCP connection.
    /// </summary>
    /// <param name="sender">The <see cref="TcpServer"/> that received the incoming connection.</param>
    /// <param name="socket">An object encapsulating the new connection.</param>
    public delegate void ConnectionEventHandler(object sender, TcpClientWithEvents socket);
}
