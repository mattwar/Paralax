using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Paralax
{
    /// <summary>
    /// A stream that limits the range of bytes that can be read from an underlying stream.
    /// </summary>
    internal class SubStream : Stream
    {
        private readonly Stream stream;
        private readonly long start;
        private readonly long length;

        public SubStream(Stream stream, long start, long length)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            if (start >= stream.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(start));
            }

            if (start + length > stream.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            this.stream = stream;
            this.start = start;
            this.length = length;
        }

        public override long Position
        {
            get { return this.stream.Position - this.start; }
            set { this.stream.Position = value + this.start; }
        }

        public override long Length => this.length;
        public override bool CanRead => this.stream.CanRead;
        public override bool CanSeek => this.stream.CanSeek;
        public override bool CanWrite => false;

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Flush() => this.stream.Flush();

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                default:
                    return this.stream.Seek(this.start + offset, origin) - this.start;
                case SeekOrigin.Current:
                    return this.stream.Seek(offset, origin) - this.start;
                case SeekOrigin.End:
                    // my brain hurts trying to figure this one out... 
                    throw new NotSupportedException();
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            // don't read beyond our sub range
            if (this.Position >= this.Length)
            {
                return 0;
            }

            // adjust count if it goes beyond sub range
            if (this.Position + count > this.Length)
            {
                count = (int)(this.Length - this.Position);
            }

            return this.stream.Read(buffer, offset, count);
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            // don't read beyond our sub range
            if (this.Position >= this.Length)
            {
                return Task.FromResult(0);
            }

            // adjust count if it goes beyond sub range
            if (this.Position + count > this.Length)
            {
                count = (int)(this.Length - this.Position);
            }

            return base.ReadAsync(buffer, offset, count, cancellationToken);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}