using System;
using System.IO;
using System.Net.Sockets;
using RT.Util;
using RT.Util.ExtensionMethods;

namespace RT.Servers
{
    internal sealed class WebSocketServerSide : MarshalByRefObject
    {
        private Stream _socketStream;
        private byte[] _currentFrameBuffer;
        private int _currentFrameBufferCount;
        private byte[] _currentMessage;
        private byte _currentMessageOpcode;
        private WebSocket _client;

        internal WebSocketServerSide(Stream socketStream, WebSocket client)
        {
            _socketStream = socketStream;
            _currentFrameBuffer = new byte[256];
            _currentFrameBufferCount = 0;
            _currentMessage = null;
            _client = client;
            beginRead();
        }

        private void beginRead()
        {
            {
                if (_currentFrameBufferCount == _currentFrameBuffer.Length)
                    Array.Resize(ref _currentFrameBuffer, _currentFrameBufferCount * 2);
                try { _socketStream.BeginRead(_currentFrameBuffer, _currentFrameBufferCount, _currentFrameBuffer.Length - _currentFrameBufferCount, receiveData, null); }
                catch (SocketException) { goto error; }
                catch (IOException) { goto error; }
                catch (ObjectDisposedException) { goto error; }
            }
            return;

            error:
            {
                try { _socketStream.Close(); }
                catch { }
                _client.OnEndConnection();
            }
        }

        private void receiveData(IAsyncResult ar)
        {
            try
            {
                var bytesRead = _socketStream.EndRead(ar);
                if (bytesRead == 0)
                {
                    _client.OnEndConnection();
                    return;
                }

                _currentFrameBufferCount += bytesRead;
                if (_currentFrameBufferCount < 6)
                    goto readMore;

                // Frames coming from a client must be masked.
                if ((_currentFrameBuffer[1] & 0x80) == 0)
                {
                    _socketStream.Close();
                    _client.OnEndConnection();
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

                // Check if the message is complete
                if ((_currentFrameBuffer[0] & 0x80) == 0x80)
                {
#if DEBUG
                    // In DEBUG mode, propagate exceptions so that the debugger will trigger.
                    if (processMessage())
                        return;
#else
                    // In RELEASE mode, catch exceptions and ignore them.
                    // It is recommended that you catch and log all exceptions in your handler.
                    try
                    {
                        if (processMessage())
                            return;
                    }
                    catch (Exception)
                    {
                    }
#endif

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
            catch (ObjectDisposedException)
            {
                // If this happens, the socket has already been closed and OnEndConnection() has already been called.
            }
            catch (SocketException)
            {
                _socketStream.Close();
                _client.OnEndConnection();
            }
            catch (IOException)
            {
                _socketStream.Close();
                _client.OnEndConnection();
            }
        }

        private bool processMessage()
        {
            switch (_currentMessageOpcode)
            {
                case 0x01:  // text
                    _client.OnTextMessageReceived(_currentMessage.FromUtf8());
                    return false;

                case 0x02:  // binary
                    _client.OnBinaryMessageReceived(_currentMessage);
                    return false;

                case 0x08:  // close
                    _socketStream.Close();
                    _client.OnEndConnection();
                    return true;

                case 0x09:  // ping
                    _socketStream.Write(_pong);
                    return false;

                default:
                    return false;
            }
        }

        private static readonly byte[] _pong = new byte[] { 0x8a, 0 };
        private static readonly byte[] _finalFrameZeroPayload = new byte[] { 0x80, 0 };

        public void SendMessage(byte opcode, byte[] payload)
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

        public void SendMessageFragment(byte opcode, byte[] fragment)
        {
            var first = true;
            var i = 0;
            while (i < fragment.Length)
            {
                var framePayloadLength = Math.Min(125, fragment.Length - i);
                _socketStream.WriteByte((byte) (first ? opcode : 0));
                _socketStream.WriteByte((byte) framePayloadLength);
                _socketStream.Write(fragment, i, framePayloadLength);
                i += framePayloadLength;
                first = false;
            }
        }

        public void SendMessageFragmentEnd(byte opcode)
        {
            _socketStream.Write(opcode == 0 ? _finalFrameZeroPayload : new byte[] { (byte) (0x80 | opcode), 0 });
        }

        public void Close()
        {
            _socketStream.Close();
        }
    }
}
