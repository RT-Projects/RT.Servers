using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using RT.Servers.TCPEvents;

namespace RT.Servers
{
    /// <summary>
    /// Provides a TCP client that can monitor an existing TCP connection (<see cref="Socket"/>)
    /// for incoming data and will raise events (callback functions) when data is received or the 
    /// connection is closed.
    /// </summary>
    public class TCPClient
    {
        private Socket Socket;
        private Thread ReadingThread;

        /// <summary>Constructs a TCP client based on an existing Socket.</summary>
        /// <param name="Socket">The Socket to monitor for incoming data.</param>
        public TCPClient(Socket Socket)
        {
            this.Socket = Socket;
            ReadingThread = new Thread(ReadingThreadFunction);
            ReadingThread.Start();
        }

        /// <summary>Specifies whether the TCP client is actively monitoring the Socket connection.</summary>
        public bool IsActive { get { return ReadingThread != null && ReadingThread.IsAlive; } }

        /// <summary>Event raised when data comes in.</summary>
        public event DataEventHandler IncomingData;
        
        /// <summary>Event raised when the connection is closed.</summary>
        public event EventHandler ConnectionClose;

        private void ReadingThreadFunction()
        {
            while (true)
            {
                byte[] Buffer = new byte[65536];
                int BytesReceived = Socket.Receive(Buffer);
                if (BytesReceived == 0)
                {
                    if (ConnectionClose != null)
                        ConnectionClose(this, new EventArgs());
                    return;
                }
                if (IncomingData != null)
                    IncomingData(this, Buffer, BytesReceived);
            }
        }

        /// <summary>Closes the Socket connection and stops monitoring the connection.</summary>
        public void Close()
        {
            if (ReadingThread != null && ReadingThread.IsAlive)
                ReadingThread.Abort();
            Socket.Close();
            Socket = null;
            ReadingThread = null;
        }

        /// <summary>Method directly forwarded to the underlying Socket.</summary>
        public int Send(byte[] buffer) { return Socket.Send(buffer); }
        /// <summary>Method directly forwarded to the underlying Socket.</summary>
        public int Send(IList<ArraySegment<byte>> buffers) { return Socket.Send(buffers); }
        /// <summary>Method directly forwarded to the underlying Socket.</summary>
        public int Send(byte[] buffer, SocketFlags socketFlags) { return Socket.Send(buffer, socketFlags); }
        /// <summary>Method directly forwarded to the underlying Socket.</summary>
        public int Send(IList<ArraySegment<byte>> buffers, SocketFlags socketFlags) { return Socket.Send(buffers, socketFlags); }
        /// <summary>Method directly forwarded to the underlying Socket.</summary>
        public int Send(byte[] buffer, int size, SocketFlags socketFlags) { return Socket.Send(buffer, size, socketFlags); }
        /// <summary>Method directly forwarded to the underlying Socket.</summary>
        public int Send(IList<ArraySegment<byte>> buffers, SocketFlags socketFlags, out SocketError errorCode) { return Socket.Send(buffers, socketFlags, out errorCode); }
        /// <summary>Method directly forwarded to the underlying Socket.</summary>
        public int Send(byte[] buffer, int offset, int size, SocketFlags socketFlags) { return Socket.Send(buffer, offset, size, socketFlags); }
        /// <summary>Method directly forwarded to the underlying Socket.</summary>
        public int Send(byte[] buffer, int offset, int size, SocketFlags socketFlags, out SocketError errorCode) { return Socket.Send(buffer, offset, size, socketFlags, out errorCode); }
    }
}
