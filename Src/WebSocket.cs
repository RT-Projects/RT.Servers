using System;
using System.Collections.Generic;
using System.Security.Permissions;
using RT.Util.ExtensionMethods;
using RT.Json;

namespace RT.Servers
{
    /// <summary>
    ///     Provides the base class for your application to implement a WebSocket connection. Derive from this class and then
    ///     pass an instance of your derived class to <see cref="HttpResponse.WebSocket"/>.</summary>
    public abstract class WebSocket : MarshalByRefObject
    {
        private WebSocketServerSide _serverSideSocket;
        private object _locker = new object();

        internal void takeSocket(WebSocketServerSide serverSideSocket)
        {
            if (_serverSideSocket != null)
                throw new InvalidOperationException("You cannot re-use a WebSocket instance for multiple separate HTTP requests. Instead, construct a new WebSocket from within the request handler.");

            _serverSideSocket = serverSideSocket;
            onBeginConnection();
        }

        /// <summary>See base.</summary>
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
        ///             All exceptions thrown by your code are swallowed by default. Wrap your override in a try/catch in
        ///             order to handle or log your exceptions.</description></item></list></remarks>
        protected virtual void onBeginConnection() { }

        internal void OnEndConnection()
        {
            onEndConnection();
        }

        /// <summary>
        ///     When overridden in a derived class, handles the event when the current WebSocket connection is closed.</summary>
        /// <remarks>
        ///     All exceptions thrown by your code are swallowed by default. Wrap your override in a try/catch in order to
        ///     handle or log your exceptions.</remarks>
        protected virtual void onEndConnection() { }

        /// <summary>
        ///     When overridden in a derived class, handles an incoming text message from the client.</summary>
        /// <param name="msg">
        ///     The text message received from the client.</param>
        /// <remarks>
        ///     All exceptions thrown by your code are swallowed by default. Wrap your handler in a try/catch in order to
        ///     handle or log your exceptions.</remarks>
        protected virtual void onTextMessageReceived(string msg) { }
        internal void OnTextMessageReceived(string msg) { onTextMessageReceived(msg); }

        /// <summary>
        ///     When overridden in a derived class, handles an incoming binary message from the client.</summary>
        /// <param name="msg">
        ///     The message received from the client.</param>
        /// <remarks>
        ///     All exceptions thrown by your code are swallowed by default. Wrap your handler in a try/catch in order to
        ///     handle or log your exceptions.</remarks>
        protected virtual void onBinaryMessageReceived(byte[] msg) { }
        internal void OnBinaryMessageReceived(byte[] msg) { onBinaryMessageReceived(msg); }

        /// <summary>
        ///     Sends a binary message to the client.</summary>
        /// <param name="binaryMessage">
        ///     The binary message to send.</param>
        public void SendMessage(byte[] binaryMessage)
        {
            if (binaryMessage == null)
                throw new ArgumentNullException("binaryMessage");
            lock (_locker)
                _serverSideSocket.SendMessage(opcode: 2, payload: binaryMessage);
        }

        /// <summary>
        ///     Sends to the client a binary message that is provided as a sequence of chunks.</summary>
        /// <param name="fragmentedBinaryMessage">
        ///     The binary message to send.</param>
        public void SendMessage(IEnumerable<byte[]> fragmentedBinaryMessage)
        {
            if (fragmentedBinaryMessage == null)
                throw new ArgumentNullException("fragmentedBinaryMessage");
            var first = true;
            lock (_locker)
            {
                foreach (var fragment in fragmentedBinaryMessage)
                {
                    _serverSideSocket.SendMessageFragment((byte) (first ? 2 : 0), fragment);
                    first = false;
                }
                _serverSideSocket.SendMessageFragmentEnd((byte) (first ? 2 : 0));
            }
        }

        /// <summary>
        ///     Sends a text message to the client.</summary>
        /// <param name="textMessage">
        ///     The text message to send.</param>
        public void SendMessage(string textMessage)
        {
            if (textMessage == null)
                throw new ArgumentNullException("textMessage");
            lock (_locker)
                _serverSideSocket.SendMessage(opcode: 1, payload: textMessage.ToUtf8());
        }

        /// <summary>
        ///     Sends to the client a text message that is provided as a sequence of chunks.</summary>
        /// <param name="fragmentedTextMessage">
        ///     The text message to send.</param>
        public void SendMessage(IEnumerable<string> fragmentedTextMessage)
        {
            if (fragmentedTextMessage == null)
                throw new ArgumentNullException("fragmentedTextMessage");
            var first = true;
            lock (_locker)
            {
                foreach (var fragment in fragmentedTextMessage)
                {
                    _serverSideSocket.SendMessageFragment((byte) (first ? 1 : 0), fragment.ToUtf8());
                    first = false;
                }
                _serverSideSocket.SendMessageFragmentEnd((byte) (first ? 1 : 0));
            }
        }

        /// <summary>
        ///     Sends a text message containing a JSON object to the client.</summary>
        /// <param name="json">
        ///     The JSON object to send.</param>
        public void SendMessage(JsonValue json)
        {
            SendMessage(JsonValue.ToString(json));
        }

        /// <summary>
        ///     Sends a message to the client.</summary>
        /// <param name="opcode">
        ///     Specifies the opcode byte in the message header. Currently the only valid values defined in the protocol are
        ///     <c>1</c> for text messages and <c>2</c> for binary messages.</param>
        /// <param name="payload">
        ///     The message to send as a sequence of bytes.</param>
        public void SendMessage(byte opcode, byte[] payload)
        {
            lock (_locker)
                _serverSideSocket.SendMessage(opcode, payload);
        }

        /// <summary>Closes the WebSocket connection.</summary>
        public void Close()
        {
            _serverSideSocket.Close();
            OnEndConnection();
        }
    }
}
