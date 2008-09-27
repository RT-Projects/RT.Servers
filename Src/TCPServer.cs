using System.Net;
using System.Net.Sockets;
using System.Threading;
using RT.Servers.TCPEvents;

namespace RT.Servers
{
    /// <summary>
    /// Provides a TCP server which can listens on a TCP port and invoke callback functions when
    /// a new incoming connection is received or when data is received on any active connection.
    /// </summary>
    public class TCPServer
    {
        /// <summary>Constructs a <see cref="TCPServer"/>. Use <see cref="StartListening"/>() to activate the server.</summary>
        public TCPServer() { }

        /// <summary>Determines whether the server is currently listening for incoming connections.</summary>
        public bool IsListening { get { return ListeningThread != null && ListeningThread.IsAlive; } }

        /// <summary>Event raised when a new connection comes in.</summary>
        public event ConnectionEventHandler IncomingConnection;

        /// <summary>Event raised when any active connection receives data.</summary>
        public event DataEventHandler IncomingData;

        private TcpListener Listener;
        private Thread ListeningThread;

        /// <summary>Disables the server, but does not terminate active connections.</summary>
        public void StopListening()
        {
            if (!IsListening)
                return;
            ListeningThread.Abort();
            ListeningThread = null;
            Listener.Stop();
            Listener = null;
        }

        /// <summary>Activates the TCP server and starts listening on the specified port.</summary>
        /// <param name="Port">TCP port to listen on.</param>
        /// <param name="Blocking">If true, the method will continually wait for incoming connections and never return.
        /// If false, a separate thread is spawned in which the server will listen for incoming connections, 
        /// and control is returned immediately.</param>
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
                TCPClient TCPSocket = new TCPClient(Socket);
                if (IncomingData != null)
                    TCPSocket.IncomingData += IncomingData;
                if (IncomingConnection != null)
                    new Thread(() => IncomingConnection(this, TCPSocket)).Start();
            }
        }
    }
}
