using System.IO;
using System.Net.Sockets;
using System.Text;

namespace Servers
{
    /// <summary>
    /// Use this if you need to write to a stream but actually want the output sent to a socket.
    /// </summary>
    public class StreamOnSocket : Stream
    {
        protected Socket Socket;
        public StreamOnSocket(Socket Socket)
        {
            this.Socket = Socket;
        }

        public override bool CanRead { get { return false; } }
        public override bool CanSeek { get { return false; } }
        public override bool CanWrite { get { return true; } }
        public override void Flush() { }

        public override void Write(byte[] buffer, int offset, int count)
        {
            Socket.Send(buffer, offset, count, SocketFlags.None);
        }

        // Stuff you can't do
        public override long Length { get { throw new System.NotSupportedException(); } }
        public override long Position
        {
            get { throw new System.NotSupportedException(); }
            set { throw new System.NotSupportedException(); }
        }
        public override int Read(byte[] buffer, int offset, int count) { throw new System.NotSupportedException(); }
        public override long Seek(long offset, SeekOrigin origin) { throw new System.NotSupportedException(); }
        public override void SetLength(long value) { throw new System.NotSupportedException(); }
    }

    /// <summary>
    /// Same as StreamOnSocket, but performs the HTTP Transfer-Encoding: chunked.
    /// </summary>
    public class StreamOnSocketChunked : StreamOnSocket
    {
        public StreamOnSocketChunked(Socket Socket)
            : base(Socket)
        {
            this.Socket = Socket;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            Socket.Send(Encoding.ASCII.GetBytes(count.ToString("X") + "\r\n"));
            Socket.Send(buffer, offset, count, SocketFlags.None);
            Socket.Send(new byte[] { 13, 10 }); // "\r\n"
        }
        public override void Close()
        {
            Socket.Send(new byte[] { (byte) '0', 13, 10, 13, 10 }); // "0\r\n\r\n"
        }
    }
}
