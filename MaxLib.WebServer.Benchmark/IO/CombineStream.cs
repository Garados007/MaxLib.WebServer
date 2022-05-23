using System;
using System.IO;

namespace MaxLib.WebServer.IO
{
    public class CombineStream : Stream
    {
        private readonly Stream read, write;

        public CombineStream(Stream read, Stream write)
        {
            _ = read ?? throw new ArgumentNullException(nameof(read));
            _ = write ?? throw new ArgumentNullException(nameof(write));
            if (!read.CanRead)
                throw new ArgumentException("cannot read from stream", nameof(read));
            if (!write.CanWrite)
                throw new ArgumentException("cannot write to stream", nameof(write));
            this.read = read;
            this.write = write;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            read.Dispose();
            write.Dispose();
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
            write.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return read.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            write.Write(buffer, offset, count);
        }
    }
}