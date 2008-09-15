using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Servers
{
    public class DynamicContentStream : Stream
    {
        private long BytesGenerated = 0;
        private IEnumerator<string> Enumerator = null;
        private byte[] LastUnprocessedBytes = null;
        private int LastUnprocessedBytesIndex = 0;
        private bool Buffered = true;

        public DynamicContentStream(IEnumerable<string> Enumerable)
        {
            this.Enumerator = Enumerable.GetEnumerator();
        }
        public DynamicContentStream(IEnumerable<string> Enumerable, bool Buffered)
        {
            this.Enumerator = Enumerable.GetEnumerator();
            this.Buffered = Buffered;
        }

        public override bool CanRead { get { return true; } }
        public override bool CanSeek { get { return false; } }
        public override bool CanWrite { get { return false; } }
        public override void Flush() { }

        public override long Position
        {
            get
            {
                return BytesGenerated;
            }
            set
            {
                throw new NotSupportedException();
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (LastUnprocessedBytes != null && LastUnprocessedBytes.Length > 0)
            {
                if (LastUnprocessedBytes.Length - LastUnprocessedBytesIndex > count)
                {
                    Array.Copy(LastUnprocessedBytes, LastUnprocessedBytesIndex, buffer, offset, count);
                    LastUnprocessedBytesIndex += count;
                    return count;
                }
                else
                {
                    int HowMany = LastUnprocessedBytes.Length - LastUnprocessedBytesIndex;
                    Array.Copy(LastUnprocessedBytes, LastUnprocessedBytesIndex, buffer, offset, HowMany);
                    LastUnprocessedBytes = null;
                    LastUnprocessedBytesIndex = 0;
                    return HowMany;
                }
            }

            if (Buffered)
            {
                StringBuilder b = new StringBuilder();
                long BytesSoFar = 0;
                while (BytesSoFar < count)
                {
                    if (!Enumerator.MoveNext())
                        break;
                    b.Append(Enumerator.Current);
                    BytesSoFar += Encoding.UTF8.GetByteCount(Enumerator.Current);
                }
                if (b.Length == 0)
                    return 0;

                byte[] BigBuffer = Encoding.UTF8.GetBytes(b.ToString());
                if (BigBuffer.Length > count)
                {
                    Array.Copy(BigBuffer, 0, buffer, offset, count);
                    LastUnprocessedBytes = BigBuffer;
                    LastUnprocessedBytesIndex = count;
                    return count;
                }
                else
                {
                    Array.Copy(BigBuffer, 0, buffer, offset, BigBuffer.Length);
                    LastUnprocessedBytes = null;
                    return BigBuffer.Length;
                }
            }
            else
            {
                if (!Enumerator.MoveNext())
                    return 0;
                byte[] Encoded = Encoding.UTF8.GetBytes(Enumerator.Current);
                if (Encoded.Length > count)
                {
                    Array.Copy(Encoded, 0, buffer, 0, count);
                    LastUnprocessedBytes = Encoded;
                    LastUnprocessedBytesIndex = count;
                    return count;
                }
                else
                {
                    Array.Copy(Encoded, 0, buffer, 0, Encoded.Length);
                    return Encoded.Length;
                }
            }
        }

        // Things you can't do
        public override long Length { get { throw new NotSupportedException(); } }
        public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }
        public override void SetLength(long value) { throw new NotSupportedException(); }
        public override void Write(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }
    }
}
