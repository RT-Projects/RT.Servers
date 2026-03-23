using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RT.Util.ExtensionMethods;

namespace RT.Servers;

internal class SniReaderStream(Stream impl) : Stream
{
    public override bool CanTimeout => impl.CanTimeout;
    public override bool CanRead => impl.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => impl.CanWrite;

    public override int ReadTimeout { get => impl.ReadTimeout; set => impl.ReadTimeout = value; }
    public override int WriteTimeout { get => impl.WriteTimeout; set => impl.WriteTimeout = value; }

    public override long Length => throw new NotImplementedException();
    public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    private byte[] _prefix = [];
    private volatile int _prefixIndex = 0;
    private volatile bool _firstAction;
    private string _protocol;

    public string Protocol => _protocol;

    private int readInternal(byte[] buffer, int offset, int count)
    {
        var preLength = 0;
        if (_prefixIndex < _prefix.Length)
        {
            preLength = Math.Min(_prefix.Length - _prefixIndex, count);
            Array.Copy(_prefix, _prefixIndex, buffer, offset, preLength);
            _prefixIndex += preLength;
        }
        return preLength;
    }

    public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
    {
        _firstAction = true;

        var read = readInternal(buffer, offset, count);
        if (read > 0 || count == 0)
        {
            var result = new FinishedResult() { AsyncState = state, IsCompleted = true, CompletedSynchronously = true, AmountRead = read };
            callback(result);
            return result;
        }

        return impl.BeginRead(buffer, offset, count, callback, state);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        _firstAction = true;

        var read = readInternal(buffer, offset, count);
        return read > 0 || count == 0 ? read : impl.Read(buffer, offset, count);
    }

    public override int EndRead(IAsyncResult asyncResult)
    {
        _firstAction = true;

        return asyncResult is FinishedResult result ? result.AmountRead : impl.EndRead(asyncResult);
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        _firstAction = true;

        var read = readInternal(buffer, offset, count);
        return read > 0 || count == 0 ? read : await impl.ReadAsync(buffer, offset, count, cancellationToken);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        _firstAction = true;
        impl.Write(buffer, offset, count);
    }

    public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
    {
        _firstAction = true;
        return impl.BeginWrite(buffer, offset, count, callback, state);
    }

    public override void EndWrite(IAsyncResult asyncResult)
    {
        _firstAction = true;
        impl.EndWrite(asyncResult);
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        _firstAction = true;
        return impl.WriteAsync(buffer, offset, count, cancellationToken);
    }

    public override void Flush()
    {
        impl.Flush();
    }

    public override Task FlushAsync(CancellationToken cancellationToken) => impl.FlushAsync(cancellationToken);
    public override long Seek(long offset, SeekOrigin origin) => throw new NotImplementedException();
    public override void SetLength(long value) => throw new NotImplementedException();
    public override void WriteByte(byte value) => throw new NotImplementedException();
    public override int ReadByte() => throw new NotImplementedException();
    public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken) => throw new NotImplementedException();

    public override void Close()
    {
        _firstAction = true;
        impl.Close();
    }

    protected override void Dispose(bool disposing)
    {
        _firstAction = true;
        impl.Dispose();
    }

    public string PeekAtSniHost()
    {
        if (_firstAction)
            throw new InvalidOperationException("PeekAtSniHost must be the first action performed on this stream.");

        _firstAction = true;

        var header = new byte[5];
        if (header.Length != impl.FillBuffer(header, 0, header.Length))
            return null;

        _prefix = header;

        var buffer = new ByteBuffer(header);

        var identifier = buffer.ReadByte();
        if (identifier != 22) // 22 = SSL handshake
            return null;

        _protocol = $"{buffer.ReadByte()}.{buffer.ReadByte()}";
        var recordLength = buffer.ReadUInt16();
        if (recordLength == 0)
            return null;

        var record = new byte[recordLength];
        if (record.Length != impl.FillBuffer(record, 0, record.Length))
            return null;

        _prefix = new byte[record.Length + header.Length];
        Array.Copy(header, 0, _prefix, 0, header.Length);
        Array.Copy(record, 0, _prefix, header.Length, record.Length);

        return parseSniRecord(record);
    }

    private string parseSniRecord(byte[] bytes)
    {
        var buffer = new ByteBuffer(bytes);

        var handshakeType = buffer.ReadByte();
        if (handshakeType != 1)
            return null;

        buffer.SkipBytes(3 + 2 + 32); // handshake length (3 bytes) + version number (2 bytes) + timestamp / random (32 bytes)

        var sessionIdLength = buffer.ReadByte();
        buffer.SkipBytes(sessionIdLength); // session id

        var cipherSuitesLength = buffer.ReadUInt16();
        buffer.SkipBytes(cipherSuitesLength);

        var compressionMethodsLength = buffer.ReadByte();
        buffer.SkipBytes(compressionMethodsLength);

        var extensionsPresent = buffer.Position < buffer.Length;
        if (!extensionsPresent)
            return null;

        var extensionsLength = buffer.ReadUInt16();
        if (buffer.Position + extensionsLength != buffer.Length)
            return null;

        while (buffer.Position < buffer.Length)
        {
            var extensionType = buffer.ReadUInt16();
            var extensionDataLength = buffer.ReadUInt16();
            if (extensionType == 0)
            {
                var sniListLength = buffer.ReadUInt16();

                for (var i = 0; i < sniListLength; i++)
                {
                    var sniType = buffer.ReadByte();
                    if (sniType != 0)
                        continue;

                    var sniLength = buffer.ReadUInt16();
                    var sniBytes = buffer.ReadBytes(sniLength);
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

    private class ByteBuffer(byte[] buffer)
    {
        public int Position { get; private set; }
        public int Length => buffer.Length;

        public byte ReadByte()
        {
            var numArray = buffer;
            var position = Position;
            Position = position + 1;
            var index = position;
            return numArray[index];
        }

        public void SkipBytes(int length)
        {
            Position += length;
        }

        public byte[] ReadBytes(int length)
        {
            var numArray = new byte[length];
            Buffer.BlockCopy(buffer, Position, numArray, 0, length);
            Position += length;
            return numArray;
        }

        public ushort ReadUInt16()
        {
            var x = BitConverter.ToUInt16(buffer, Position);
            Position += 2;
            return BitConverter.IsLittleEndian ? (ushort) ((ushort) ((x & 0xff) << 8) | ((x >> 8) & 0xff)) : x;
        }
    }
}
