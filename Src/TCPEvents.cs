
namespace Servers.TCPEvents
{
    public delegate void DataEventHandler(object Sender, byte[] Data, int BytesReceived);
    public delegate void ConnectionEventHandler(object Sender, TCPSocket Socket);
}
