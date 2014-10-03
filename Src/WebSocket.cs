using System;
using System.Collections.Generic;
using System.Runtime.Remoting;
using System.Security.Permissions;
using RT.Util.ExtensionMethods;
using RT.Util.Json;

namespace RT.Servers
{
    public abstract class WebSocket : MarshalByRefObject
    {
        private WebSocketServerSide _serverSideSocket;

        internal void takeSocket(WebSocketServerSide serverSideSocket)
        {
            if (_serverSideSocket != null)
                throw new InvalidOperationException("You cannot re-use a WebSocket instance for multiple separate HTTP requests. Instead, construct a new WebSocket from within the request handler.");

            _serverSideSocket = serverSideSocket;
            OnBeginConnection();
        }

        [SecurityPermissionAttribute(SecurityAction.Demand, Flags = SecurityPermissionFlag.Infrastructure)]
        public override object InitializeLifetimeService()
        {
            return null;
        }

        /// <summary>
        ///     When overridden in a derived class, handles an incoming WebSocket connection from a client.</summary>
        /// <remarks>
        ///     <list type="bullet">
        ///         <item><description>
        ///             This method is called only once per instance, and it is called when the socket has switched from the
        ///             HTTP handshake to the WebSocket protocol.</description></item>
        ///         <item><description>
        ///             All exceptions thrown by your code are swallowed by default. Wrap your handler in a try/catch in order
        ///             to handle or log your exceptions.</description></item></list></remarks>
        public void OnBeginConnection() { onBeginConnection(); }
        protected virtual void onBeginConnection() { }

        /// <summary>
        ///     When overridden in a derived class, handles the event when the current WebSocket connection is closed.</summary>
        /// <remarks>
        ///     All exceptions thrown by your code are swallowed by default. Wrap your handler in a try/catch in order to
        ///     handle or log your exceptions.</remarks>
        public void OnEndConnection()
        {
            RemotingServices.Disconnect(this);
            onEndConnection();
        }
        protected virtual void onEndConnection() { }

        /// <summary>
        ///     When overridden in a derived class, handles an incoming text message from the client.</summary>
        /// <remarks>
        ///     All exceptions thrown by your code are swallowed by default. Wrap your handler in a try/catch in order to
        ///     handle or log your exceptions.</remarks>
        public void OnTextMessageReceived(string msg) { onTextMessageReceived(msg); }
        protected virtual void onTextMessageReceived(string msg) { }

        /// <summary>
        ///     When overridden in a derived class, handles an incoming binary message from the client.</summary>
        /// <remarks>
        ///     All exceptions thrown by your code are swallowed by default. Wrap your handler in a try/catch in order to
        ///     handle or log your exceptions.</remarks>
        public void OnBinaryMessageReceived(byte[] msg) { onBinaryMessageReceived(msg); }
        protected virtual void onBinaryMessageReceived(byte[] msg) { }

        /// <summary>
        ///     Sends a binary message to the client.</summary>
        /// <param name="binaryMessage">
        ///     The binary message to send.</param>
        public void SendMessage(byte[] binaryMessage)
        {
            if (binaryMessage == null)
                throw new ArgumentNullException("binaryMessage");
            _serverSideSocket.SendMessage(opcode: 2, payload: binaryMessage);
        }

        public void SendMessage(IEnumerable<byte[]> fragmentedBinaryMessage)
        {
            if (fragmentedBinaryMessage == null)
                throw new ArgumentNullException("fragmentedBinaryMessage");
            var first = true;
            foreach (var fragment in fragmentedBinaryMessage)
            {
                _serverSideSocket.SendMessageFragment((byte) (first ? 2 : 0), fragment);
                first = false;
            }
            _serverSideSocket.SendMessageFragmentEnd((byte) (first ? 2 : 0));
        }

        public void SendMessage(string textMessage)
        {
            if (textMessage == null)
                throw new ArgumentNullException("textMessage");
            _serverSideSocket.SendMessage(opcode: 1, payload: textMessage.ToUtf8());
        }

        public void SendMessage(IEnumerable<string> fragmentedTextMessage)
        {
            if (fragmentedTextMessage == null)
                throw new ArgumentNullException("fragmentedTextMessage");
            var first = true;
            foreach (var fragment in fragmentedTextMessage)
            {
                _serverSideSocket.SendMessageFragment((byte) (first ? 1 : 0), fragment.ToUtf8());
                first = false;
            }
            _serverSideSocket.SendMessageFragmentEnd((byte) (first ? 1 : 0));
        }

        public void SendMessage(JsonValue json)
        {
            SendMessage(JsonValue.ToString(json));
        }

        public void SendMessage(byte opcode, byte[] payload)
        {
            _serverSideSocket.SendMessage(opcode, payload);
        }

        public void Close()
        {
            _serverSideSocket.Close();
            OnEndConnection();
        }
    }
}
