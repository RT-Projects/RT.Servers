using System;
using RT.Util.ExtensionMethods;
using System.IO;
using System.Net.Sockets;
using System.Text;

namespace RT.Servers
{
    /// <summary>
    ///     Performs the HTTP Transfer-Encoding “chunked”. This is a write-only stream.</summary>
    /// <param name="inner">
    ///     The underlying stream to write all the output to.</param>
    /// <param name="leaveInnerOpen">
    ///     If true, the inner stream is not closed when this stream is closed.</param>
    sealed class ChunkedEncodingStream(Stream inner, bool leaveInnerOpen = false) : Stream
    {
        /// <summary>
        ///     Writes the specified data to the underlying <see cref="Socket"/> as a single chunk.</summary>
        /// <param name="buffer">
        ///     Buffer containing the data to be written.</param>
        /// <param name="offset">
        ///     Buffer offset starting at which data is obtained.</param>
        /// <param name="count">
        ///     Number of bytes to read from buffer and send to the socket.</param>
        public override void Write(byte[] buffer, int offset, int count)
        {
            inner.Write(Encoding.UTF8.GetBytes(count.ToString("X") + "\r\n"));
            inner.Write(buffer, offset, count);
            inner.Write([13, 10]); // "\r\n"
        }

        /// <summary>
        ///     Closes this <see cref="ChunkedEncodingStream"/>. It is important that this is called because it outputs the
        ///     trailing null chunk to the socket, indicating the end of the data.</summary>
        public override void Close()
        {
            inner.Write([(byte) '0', 13, 10, 13, 10]); // "0\r\n\r\n"
            inner.Flush();
            if (!leaveInnerOpen)
                inner.Close();
        }

        // Stuff you can't do
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override void Flush() { }

        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
        }
        public override int Read(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }
        public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }
        public override void SetLength(long value) { throw new NotSupportedException(); }
    }

    /// <summary>
    ///     Reads a fixed amount of data from a stream. The client code encounters the end of this stream after said fixed
    ///     amount of data is retrieved, even if the underlying stream does not actually end there. This class also allows you
    ///     to provide data from a buffer to “prepend” to the data from the stream (in case you already read a bit too much
    ///     from the underlying stream).</summary>
    sealed class Substream : Stream
    {
        private readonly Stream _inner;
        private long _maxBytesToRead;
        private byte[] _lastRead;
        private int _lastReadOffset;
        private int _lastReadCount;
        private bool _myBuffer;

        /// <summary>
        ///     Constructs a <see cref="Substream"/> object.</summary>
        /// <param name="inner">
        ///     The underlying stream to read from.</param>
        /// <param name="maxBytesToRead">
        ///     Maximum number of bytes to read from the socket. After this, the stream pretends to have reached the end.</param>
        public Substream(Stream inner, long maxBytesToRead)
        {
            _inner = inner;
            _maxBytesToRead = maxBytesToRead;
            _lastRead = null;
            _lastReadOffset = 0;
            _lastReadCount = 0;
            _myBuffer = false;
        }

        /// <summary>
        ///     Constructs a <see cref="Substream"/> object that reads from a given bit of initial data, and then continues on
        ///     to read from the underlying stream.</summary>
        /// <param name="inner">
        ///     The underlying stream to read from.</param>
        /// <param name="maxBytesToRead">
        ///     Maximum number of bytes to read from the initial data plus the underlying stream. After this, the stream
        ///     pretends to have reached the end.</param>
        /// <param name="initialBuffer">
        ///     Buffer containing the initial data. The buffer is not copied, so make sure you don't modify the contents of
        ///     the buffer before it is consumed by reading.</param>
        /// <param name="initialBufferOffset">
        ///     Offset into the buffer where the initial data starts.</param>
        /// <param name="initialBufferCount">
        ///     Number of bytes of initial data in the buffer.</param>
        public Substream(Stream inner, long maxBytesToRead, byte[] initialBuffer, int initialBufferOffset, int initialBufferCount)
        {
            _inner = inner;
            _myBuffer = false;

            if (initialBufferCount <= 0)
            {
                _lastRead = null;
                _lastReadOffset = 0;
                _lastReadCount = 0;
                _maxBytesToRead = maxBytesToRead;
            }
            else if (initialBufferCount > maxBytesToRead)
            {
                _lastRead = initialBuffer;
                _lastReadOffset = initialBufferOffset;
                // The conversion to int here is safe because we know it's smaller than initialBufferCount, which is an int
                _lastReadCount = (int) maxBytesToRead;
                _maxBytesToRead = 0;
            }
            else
            {
                _lastRead = initialBuffer;
                _lastReadOffset = initialBufferOffset;
                _lastReadCount = initialBufferCount;
                _maxBytesToRead = maxBytesToRead - initialBufferCount;
            }
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override void Flush() { }
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set { throw new NotSupportedException(); } }
        public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }
        public override void SetLength(long value) { throw new NotSupportedException(); }
        public override void Write(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }

        /// <summary>
        ///     Reads up to the specified number of bytes from the underlying socket.</summary>
        /// <param name="buffer">
        ///     Buffer to write received data into.</param>
        /// <param name="offset">
        ///     Offset into the buffer where to start writing.</param>
        /// <param name="count">
        ///     Maximum number of bytes in the buffer to write to.</param>
        /// <returns>
        ///     Number of bytes actually written to the buffer.</returns>
        public override int Read(byte[] buffer, int offset, int count)
        {
            // If we have something left from the last socket-receive operation, return as much of that as possible
            if (_lastRead != null && _lastReadCount > 0)
            {
                if (count >= _lastReadCount)
                {
                    Buffer.BlockCopy(_lastRead, _lastReadOffset, buffer, offset, _lastReadCount);
                    if (_myBuffer)
                    {
                        var tmp = _lastReadCount;
                        _lastReadCount = 0;
                        return tmp;
                    }
                    _lastRead = null;
                    return _lastReadCount;
                }
                else
                {
                    Buffer.BlockCopy(_lastRead, _lastReadOffset, buffer, offset, count);
                    _lastReadOffset += count;
                    _lastReadCount -= count;
                    return count;
                }
            }
            else
            {
                // If we have read as many bytes as we are supposed to, simulate the end of the stream
                if (_maxBytesToRead <= 0)
                    return 0;

                if (!_myBuffer)
                {
                    // Read at most _maxBytesToRead bytes from the socket
                    _lastRead = new byte[(int) Math.Min(65536, _maxBytesToRead)];
                    _myBuffer = true;
                }
                _lastReadOffset = 0;
                _lastReadCount = _inner.Read(_lastRead, _lastReadOffset, (int) Math.Min(_lastRead.Length, _maxBytesToRead));
                _maxBytesToRead -= _lastReadCount;

                // Socket error?
                if (_lastReadCount == 0)
                {
                    _lastRead = null;
                    _maxBytesToRead = 0;
                    return 0;
                }

                // We’ve populated _lastRead; use a tail-recursive call to actually return the data
                return Read(buffer, offset, count);
            }
        }
    }
}
