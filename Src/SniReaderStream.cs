﻿using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RT.Util.ExtensionMethods;

namespace RT.Servers
{
    internal class SniReaderStream : Stream
    {
        public override bool CanTimeout => _impl.CanTimeout;
        public override bool CanRead => _impl.CanRead;
        public override bool CanSeek => _impl.CanSeek;
        public override bool CanWrite => _impl.CanWrite;

        public override int ReadTimeout
        {
            get { return _impl.ReadTimeout; }
            set { _impl.ReadTimeout = value; }
        }

        public override int WriteTimeout
        {
            get { return _impl.WriteTimeout; }
            set { _impl.WriteTimeout = value; }
        }

        public override long Length
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public override long Position
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        private Stream _impl;
        private byte[] _prefix = new byte[0];
        private volatile int _prefixIndex = 0;
        private volatile bool _firstAction;

        public SniReaderStream(Stream impl)
        {
            _impl = impl;
        }

        private int ReadInternal(byte[] buffer, ref int offset, ref int count)
        {
            int preLength = 0;
            if (_prefixIndex < _prefix.Length)
            {
                preLength = Math.Min(_prefix.Length - _prefixIndex, count);
                Array.Copy(_prefix, _prefixIndex, buffer, offset, preLength);
                _prefixIndex += preLength;

                offset += preLength;
                count -= preLength;
            }
            return preLength;
        }

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            _firstAction = true;

            var read = ReadInternal(buffer, ref offset, ref count);
            if (read > 0)
            {
                var result = new FinishedResult() { AsyncState = state, IsCompleted = true, CompletedSynchronously = true, AmountRead = read };
                callback(result);
                return result;
            }

            return _impl.BeginRead(buffer, offset, count, callback, state);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            _firstAction = true;

            var read = ReadInternal(buffer, ref offset, ref count);
            if (read > 0)
                return read;

            return _impl.Read(buffer, offset, count);
        }

        public override int EndRead(IAsyncResult asyncResult)
        {
            _firstAction = true;

            var result = asyncResult as FinishedResult;
            if (result != null)
                return result.AmountRead;

            return _impl.EndRead(asyncResult);
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            _firstAction = true;

            var read = ReadInternal(buffer, ref offset, ref count);
            if (read > 0)
                return read;

            return await _impl.ReadAsync(buffer, offset, count, cancellationToken);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _firstAction = true;
            _impl.Write(buffer, offset, count);
        }

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            _firstAction = true;
            return _impl.BeginWrite(buffer, offset, count, callback, state);
        }

        public override void EndWrite(IAsyncResult asyncResult)
        {
            _firstAction = true;
            _impl.EndWrite(asyncResult);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            _firstAction = true;
            return _impl.WriteAsync(buffer, offset, count, cancellationToken);
        }

        public override void Flush()
        {
            _impl.Flush();
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            return _impl.FlushAsync(cancellationToken);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void WriteByte(byte value)
        {
            throw new NotImplementedException();
        }

        public override int ReadByte()
        {
            throw new NotImplementedException();
        }

        public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public override void Close()
        {
            _firstAction = true;
            _impl.Close();
        }

        protected override void Dispose(bool disposing)
        {
            _firstAction = true;
            _impl.Dispose();
        }

        public string PeekAtSniHost()
        {
            if (_firstAction)
                throw new InvalidOperationException("PeekAtSniHost must be the first action performed on this stream.");

            _firstAction = true;

            byte[] header = new byte[5];
            if (header.Length != _impl.FillBuffer(header, 0, header.Length))
                return null;

            _prefix = header;

            ByteBuffer buffer = new ByteBuffer(header);

            var identifier = buffer.ReadByte();
            if (identifier != 22) // 22 = SSL handshake
                return null;

            var protocol = $"{buffer.ReadByte()}.{buffer.ReadByte()}";
            var recordLength = buffer.ReadUInt16();

            byte[] record = new byte[recordLength];
            if (record.Length != _impl.FillBuffer(record, 0, record.Length))
                return null;

            _prefix = new byte[record.Length + header.Length];
            Array.Copy(header, 0, _prefix, 0, header.Length);
            Array.Copy(record, 0, _prefix, header.Length, record.Length);

            return ParseSniRecord(record);
        }

        private string ParseSniRecord(byte[] bytes)
        {
            ByteBuffer buffer = new ByteBuffer(bytes);

            var handshakeType = buffer.ReadByte();
            if (handshakeType != 1)
                return null;

            buffer.SkipBytes(3 + 2 + 32); // handshake length (3 bytes) + version number (2 bytes) + timestamp / random (32 bytes)

            byte sessionIdLength = buffer.ReadByte();
            buffer.SkipBytes(sessionIdLength); // session id

            ushort cipherSuitesLength = buffer.ReadUInt16();
            buffer.SkipBytes(cipherSuitesLength);

            byte compressionMethodsLength = buffer.ReadByte();
            buffer.SkipBytes(compressionMethodsLength);

            bool extensionsPresent = buffer.Position < buffer.Length;
            if (!extensionsPresent)
                return null;

            ushort extensionsLength = buffer.ReadUInt16();
            if (buffer.Position + extensionsLength != buffer.Length)
                return null;

            while (buffer.Position < buffer.Length)
            {
                ushort extensionType = buffer.ReadUInt16();
                ushort extensionDataLength = buffer.ReadUInt16();
                if (extensionType == 0)
                {
                    var sniListLength = buffer.ReadUInt16();

                    for (int i = 0; i < sniListLength; i++)
                    {
                        var sniType = buffer.ReadByte();
                        if (sniType != 0)
                            continue;

                        var sniLength = buffer.ReadUInt16();
                        byte[] sniBytes = buffer.ReadBytes(sniLength);
                        return Encoding.ASCII.GetString(sniBytes);
                    }
                }
                buffer.SkipBytes(extensionDataLength);
            }

            return null;
        }

        private class FinishedResult : IAsyncResult
        {
            public bool IsCompleted { get; set; }
            public WaitHandle AsyncWaitHandle { get; set; }
            public object AsyncState { get; set; }
            public bool CompletedSynchronously { get; set; }
            public int AmountRead { get; set; }
        }

        private class ByteBuffer
        {
            private readonly byte[] _buffer;

            public int Position { get; private set; }

            public int Length
            {
                get
                {
                    return this._buffer.Length;
                }
            }

            public ByteBuffer(byte[] buffer)
            {
                this._buffer = buffer;
            }

            public byte ReadByte()
            {
                byte[] numArray = this._buffer;
                int position = this.Position;
                this.Position = position + 1;
                int index = position;
                return numArray[index];
            }

            public void SkipBytes(int length)
            {
                Position += length;
            }

            public byte[] ReadBytes(int length)
            {
                byte[] numArray = new byte[length];
                Buffer.BlockCopy((Array) this._buffer, this.Position, (Array) numArray, 0, length);
                this.Position = this.Position + length;
                return numArray;
            }

            public ushort ReadUInt16()
            {
                var x = BitConverter.ToUInt16(_buffer, Position);
                Position += 2;
                if (BitConverter.IsLittleEndian)
                    return (ushort) ((ushort) ((x & 0xff) << 8) | ((x >> 8) & 0xff));
                return x;
            }
        }
    }

}
