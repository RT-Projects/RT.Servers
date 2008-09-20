﻿using System.IO;
using System.Net.Sockets;
using System.Text;

namespace Servers
{
    /// <summary>
    /// Use this if you need to write to a <see cref="Stream"/> but actually want the output sent to a <see cref="Socket"/>.
    /// </summary>
    public class StreamOnSocket : Stream
    {
        protected Socket Socket;

        /// <summary>
        /// Constructs a <see cref="StreamOnSocket"/> object that can send output to a <see cref="Socket"/>.
        /// </summary>
        /// <param name="Socket">The socket to write all the output to.</param>
        public StreamOnSocket(Socket Socket)
        {
            this.Socket = Socket;
        }

        public override bool CanRead { get { return false; } }
        public override bool CanSeek { get { return false; } }
        public override bool CanWrite { get { return true; } }
        public override void Flush() { }

        /// <summary>
        /// Writes the specified data to the underlying <see cref="Socket"/>.
        /// </summary>
        /// <param name="buffer">Buffer containing the data to be written.</param>
        /// <param name="offset">Buffer offset starting at which data is obtained.</param>
        /// <param name="count">Number of bytes to read from <see cref="buffer"/> and send to the socket.</param>
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
        /// <summary>
        /// Constructs a <see cref="StreamOnSocketChunked"/> object that can send output to a
        /// <see cref="Socket"/> in HTTP "chunked" Transfer-Encoding.
        /// </summary>
        /// <param name="Socket">The socket to write all the output to.</param>
        public StreamOnSocketChunked(Socket Socket)
            : base(Socket)
        {
            this.Socket = Socket;
        }

        /// <summary>
        /// Writes the specified data to the underlying <see cref="Socket"/> as a single chunk.
        /// </summary>
        /// <param name="buffer">Buffer containing the data to be written.</param>
        /// <param name="offset">Buffer offset starting at which data is obtained.</param>
        /// <param name="count">Number of bytes to read from <see cref="buffer"/> and send to the socket.</param>
        public override void Write(byte[] buffer, int offset, int count)
        {
            Socket.Send(Encoding.ASCII.GetBytes(count.ToString("X") + "\r\n"));
            Socket.Send(buffer, offset, count, SocketFlags.None);
            Socket.Send(new byte[] { 13, 10 }); // "\r\n"
        }

        /// <summary>
        /// Closes this <see cref="StreamOnSocketChunked"/>. It is important that this is called
        /// because it outputs the trailing null chunk to the socket, indicating the end of the data.
        /// </summary>
        public override void Close()
        {
            Socket.Send(new byte[] { (byte) '0', 13, 10, 13, 10 }); // "0\r\n\r\n"
        }
    }
}
