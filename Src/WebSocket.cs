using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Security.Cryptography;
using RT.Util.ExtensionMethods;
using RT.Util.Json;
using RT.Util;

namespace RT.Servers
{
    public abstract class WebSocket : MarshalByRefObject
    {
        private Stream _socketStream;
        private byte[] _currentFrameBuffer;
        private int _currentFrameBufferCount;
        private byte[] _currentMessage;
        private byte _currentMessageOpcode;

        internal void takeSocket(Stream socketStream)
        {
            if (_socketStream != null)
                throw new InvalidOperationException("You cannot re-use a WebSocket instance for multiple separate HTTP requests. Instead, construct a new WebSocket from within the request handler.");

            _socketStream = socketStream;
            _currentFrameBuffer = new byte[256];
            _currentFrameBufferCount = 0;
            _currentMessage = null;
            BeginConnection();
            beginRead();
        }

        private void beginRead()
        {
            if (_currentFrameBufferCount == _currentFrameBuffer.Length)
                Array.Resize(ref _currentFrameBuffer, _currentFrameBufferCount * 2);
            _socketStream.BeginRead(_currentFrameBuffer, _currentFrameBufferCount, _currentFrameBuffer.Length - _currentFrameBufferCount, receiveData, null);
        }

        private void receiveData(IAsyncResult ar)
        {
            try
            {
                var bytesRead = _socketStream.EndRead(ar);
                if (bytesRead == 0)
                {
                    EndConnection();
                    return;
                }

                _currentFrameBufferCount += bytesRead;
                if (_currentFrameBufferCount < 6)
                    goto readMore;

                // Frames coming from a client must be masked.
                if ((_currentFrameBuffer[1] & 0x80) == 0)
                {
                    _socketStream.Close();
                    EndConnection();
                    return;
                }

                var payloadLenRaw = _currentFrameBuffer[1] & 0x7f;
                if (payloadLenRaw == 126 && _currentFrameBufferCount < 8)
                    goto readMore;
                if (payloadLenRaw == 127 && _currentFrameBufferCount < 14)
                    goto readMore;

                var payloadLen =
                    payloadLenRaw == 126 ? (int) Ut.BytesToUShort(_currentFrameBuffer, 2, true) :
                    payloadLenRaw == 127 ? (int) Ut.BytesToULong(_currentFrameBuffer, 2, true) :
                    payloadLenRaw;
                Console.WriteLine("payloadLen = " + payloadLen);
                var i =
                    payloadLenRaw == 126 ? 4 :
                    payloadLenRaw == 127 ? 10 : 2;
                var maskingKeyIndex = i;
                i += 4;
                if (_currentFrameBufferCount < i + payloadLen)
                    goto readMore;
                var payloadIndex = i;

                // The frame is complete. Unmask the payload data...
                for (var j = 0; i < _currentFrameBufferCount; i++, j++)
                    _currentFrameBuffer[i] ^= _currentFrameBuffer[maskingKeyIndex + (j % 4)];

                // ... and then append the payload data to the current message.
                int dest;
                if (_currentMessage == null)
                {
                    _currentMessage = new byte[payloadLen];
                    _currentMessageOpcode = (byte) (_currentFrameBuffer[0] & 0xf);
                    dest = 0;
                }
                else
                {
                    dest = _currentMessage.Length;
                    Array.Resize(ref _currentMessage, _currentMessage.Length + payloadLen);
                }
                Buffer.BlockCopy(_currentFrameBuffer, payloadIndex, _currentMessage, dest, payloadLen);
                Console.WriteLine("Message now " + _currentMessage.Length);

                // Check if the message is complete
                if ((_currentFrameBuffer[0] & 0x80) == 0x80)
                {
                    Console.WriteLine("Message complete");
                    switch (_currentMessageOpcode)
                    {
                        case 0x01:  // text
                            TextMessageReceived(_currentMessage.FromUtf8());
                            break;
                        case 0x02:  // binary
                            BinaryMessageReceived(_currentMessage);
                            break;
                        case 0x08:  // close
                            _socketStream.Close();
                            EndConnection();
                            return;
                        case 0x09:  // ping
                            sendMessage(0x0a /* pong */, new byte[] { });
                            break;
                    }

                    // We’ve used the message, start a new one
                    _currentMessage = null;
                }

                // We’ve used the frame buffer, reinitialize it
                _currentFrameBufferCount = 0;
                if (_currentFrameBuffer.Length > 256)
                    _currentFrameBuffer = new byte[256];

                readMore:
                beginRead();
            }
            catch (SocketException)
            {
                _socketStream.Close();
                EndConnection();
            }
        }

        public virtual void BeginConnection() { }
        public virtual void EndConnection() { }

        public virtual void TextMessageReceived(string msg) { }
        public virtual void BinaryMessageReceived(byte[] msg) { }

        public void SendMessage(byte[] binaryMessage)
        {
            if (binaryMessage == null)
                throw new ArgumentNullException("binaryMessage");
            sendMessage(opcode: 2, payload: binaryMessage);
        }

        public void SendMessage(IEnumerable<byte[]> fragmentedBinaryMessage)
        {
            if (fragmentedBinaryMessage == null)
                throw new ArgumentNullException("fragmentedBinaryMessage");
            sendMessage(isText: false, payload: fragmentedBinaryMessage);
        }

        public void SendMessage(string textMessage)
        {
            if (textMessage == null)
                throw new ArgumentNullException("textMessage");
            sendMessage(opcode: 1, payload: textMessage.ToUtf8());
        }

        public void SendMessage(IEnumerable<string> fragmentedTextMessage)
        {
            if (fragmentedTextMessage == null)
                throw new ArgumentNullException("fragmentedTextMessage");
            sendMessage(isText: true, payload: fragmentedTextMessage.Select(StringExtensions.ToUtf8));
        }

        public void SendMessage(JsonValue json)
        {
            sendMessage(opcode: 1, payload: JsonValue.ToString(json).ToUtf8());
        }

        private byte[] finalFrameZeroPayload = new byte[] { 0x80, 0 };

        private void sendMessage(byte opcode, byte[] payload)
        {
            var lengthLength = payload.Length < 126 ? 1 : payload.Length < 65536 ? 2 : 8;
            var frame = new byte[1 + lengthLength + payload.Length];
            frame[0] = (byte) (opcode | 0x80);
            var i = 1;
            if (payload.Length < 126)
                frame[i++] = (byte) payload.Length;
            else if (payload.Length < 65536)
            {
                frame[i++] = 126;
                frame[i++] = (byte) (payload.Length & 0xFF);
                frame[i++] = (byte) (payload.Length >> 8);
            }
            else
            {
                frame[i++] = 127;
                frame[i++] = (byte) (payload.Length & 0xFF);
                frame[i++] = (byte) ((payload.Length >> 8) & 0xFF);
                frame[i++] = (byte) ((payload.Length >> 16) & 0xFF);
                frame[i++] = (byte) ((payload.Length >> 24) & 0xFF);
                i += 4;
            }

            Buffer.BlockCopy(payload, 0, frame, i, payload.Length);
            _socketStream.Write(frame, 0, frame.Length);
        }

        private void sendMessage(bool isText, IEnumerable<byte[]> payload)
        {
            byte[] frame = null;
            var first = true;
            foreach (var piece in payload)
            {
                var i = 0;
                while (i < piece.Length)
                {
                    if (frame == null)
                        frame = new byte[2];
                    var framePayloadLength = Math.Min(125, piece.Length - i);
                    _socketStream.WriteByte((byte) (first ? (isText ? 1 : 2) : 0));
                    _socketStream.WriteByte((byte) framePayloadLength);
                    _socketStream.Write(piece, i, framePayloadLength);
                    i += framePayloadLength;
                    first = false;
                }
            }
            _socketStream.Write(finalFrameZeroPayload);
        }
    }
}
