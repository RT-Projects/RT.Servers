using System.Net;
using System.Net.Sockets;
using System.Threading;
using Servers.TCPEvents;

namespace Servers
{
    public class TCPServer
    {
        public TCPServer() { }

        public bool IsListening { get { return ListeningThread != null && ListeningThread.IsAlive; } }

        public event ConnectionEventHandler IncomingConnection;
        public event DataEventHandler IncomingData;

        private TcpListener Listener;
        private Thread ListeningThread;

        public void StopListening()
        {
            if (!IsListening)
                return;
            ListeningThread.Abort();
            ListeningThread = null;
            Listener.Stop();
            Listener = null;
        }

        public void StartListening(int Port, bool Blocking)
        {
            if (IsListening && !Blocking)
                return;
            if (IsListening && Blocking)
                StopListening();
            Listener = new TcpListener(IPAddress.Any, Port);
            Listener.Start();
            if (Blocking)
            {
                ListeningThreadFunction();
                Listener.Stop();
                Listener = null;
            }
            else
            {
                ListeningThread = new Thread(ListeningThreadFunction);
                ListeningThread.Start();
            }
        }

        private void ListeningThreadFunction()
        {
            while (true)
            {
                Socket Socket = Listener.AcceptSocket();
                TCPSocket TCPSocket = new TCPSocket(Socket);
                if (IncomingData != null)
                    TCPSocket.IncomingData += IncomingData;
                if (IncomingConnection != null)
                    new Thread(delegate() { IncomingConnection(this, TCPSocket); }).Start();
            }
        }
    }
}
