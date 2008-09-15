using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using Servers.TCPEvents;

namespace Servers
{
    public class TCPSocket
    {
        private Socket Socket;
        private Thread ReadingThread;

        public TCPSocket(Socket Socket)
        {
            this.Socket = Socket;
            ReadingThread = new Thread(ReadingThreadFunction);
            ReadingThread.Start();
        }

        public bool IsActive { get { return ReadingThread != null && ReadingThread.IsAlive; } }
        public event DataEventHandler IncomingData;
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

        public void Close()
        {
            if (ReadingThread != null && ReadingThread.IsAlive)
                ReadingThread.Abort();
            Socket.Close();
            Socket = null;
            ReadingThread = null;
        }

        public int Send(byte[] buffer) { return Socket.Send(buffer); }
        public int Send(IList<ArraySegment<byte>> buffers) { return Socket.Send(buffers); }
        public int Send(byte[] buffer, SocketFlags socketFlags) { return Socket.Send(buffer, socketFlags); }
        public int Send(IList<ArraySegment<byte>> buffers, SocketFlags socketFlags) { return Socket.Send(buffers, socketFlags); }
        public int Send(byte[] buffer, int size, SocketFlags socketFlags) { return Socket.Send(buffer, size, socketFlags); }
        public int Send(IList<ArraySegment<byte>> buffers, SocketFlags socketFlags, out SocketError errorCode) { return Socket.Send(buffers, socketFlags, out errorCode); }
        public int Send(byte[] buffer, int offset, int size, SocketFlags socketFlags) { return Socket.Send(buffer, offset, size, socketFlags); }
        public int Send(byte[] buffer, int offset, int size, SocketFlags socketFlags, out SocketError errorCode) { return Socket.Send(buffer, offset, size, socketFlags, out errorCode); }
    }
}
