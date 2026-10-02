using System.IO;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

#nullable enable

namespace MaxLib.WebServer.IO
{
    /// <summary>
    /// This stream maps a single <see cref="NetworkReader" /> instance and allows to read a
    /// specific length of data from it.
    /// </summary>
    public class ContentStream : Stream
    {
        static readonly ILogger logger = WebServerLog.LoggerFactory.CreateLogger<ContentStream>();
        static readonly EventId ReadEventId = new(0, "Read");

        private NetworkReader reader;
        private readonly CancellationTokenSource? timeoutSource;

        /// <summary>
        /// The count of bytes that are already read.
        /// </summary>
        public long ReadData { get; private set; }
        /// <summary>
        /// The maximum number of bytes that are allowed to read
        /// </summary>
        public long FullLength { get; }
        /// <summary>
        /// The number of bytes that is unread
        /// </summary>
        public long UnreadData => FullLength - ReadData;

        /// <summary>
        /// The cancellation token that is triggered once the read timeout configured via the
        /// <c>timeout</c> constructor parameter elapses. <see cref="CancellationToken.None" /> if
        /// no timeout was configured. All <c>*Async</c> methods on this stream (including
        /// <see cref="DisposeAsync" />) observe this token in addition to any token passed in
        /// explicitly, so a client that stalls mid-upload can't block a reader on this stream
        /// forever, however that reader is layered on top of it.
        /// </summary>
        public CancellationToken TimeoutToken => timeoutSource?.Token ?? CancellationToken.None;

        public ContentStream(NetworkReader reader, long maximum)
            : this(reader, maximum, null)
        {
        }

        /// <summary>
        /// Creates a new content stream that is additionally cancelled once <paramref
        /// name="timeout" /> elapses (measured from construction), protecting against a client
        /// that stalls mid-upload from blocking a reader on this stream indefinitely. Pass
        /// <c>null</c> to not apply any such timeout.
        /// </summary>
        public ContentStream(NetworkReader reader, long maximum, TimeSpan? timeout)
        {
            this.reader = reader;
            FullLength = maximum;
            if (timeout.HasValue)
                timeoutSource = new CancellationTokenSource(timeout.Value);
        }

        /// <summary>
        /// Combines <see cref="TimeoutToken" /> with a caller-supplied token, avoiding the
        /// allocation of a linked <see cref="CancellationTokenSource" /> when the caller didn't
        /// actually pass one in (the overwhelming common case for every caller in this library).
        /// </summary>
        private readonly struct EffectiveToken : IDisposable
        {
            public CancellationToken Token { get; }
            private readonly CancellationTokenSource? linked;

            public EffectiveToken(CancellationToken timeoutToken, CancellationToken callerToken)
            {
                if (callerToken == default || callerToken == timeoutToken)
                {
                    Token = timeoutToken;
                    linked = null;
                }
                else
                {
                    linked = CancellationTokenSource.CreateLinkedTokenSource(timeoutToken, callerToken);
                    Token = linked.Token;
                }
            }

            public void Dispose() => linked?.Dispose();
        }

        private EffectiveToken Link(CancellationToken cancellationToken)
            => new(TimeoutToken, cancellationToken);

        public override bool CanRead => UnreadData > 0;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => FullLength;

        public override long Position
        {
            get => ReadData;
            set => throw new InvalidOperationException("cannot seek on this stream");
        }

        public override void Flush()
        {
        }

        /// <summary>
        /// Discards any unread data and ignore any timeouts.
        /// </summary>
        public virtual void Discard()
        {
            var buffer = new byte[64 * 1024];
            while (UnreadData > 0)
            {
                var length = reader.Read(buffer, 0, (int)Math.Min(buffer.Length, UnreadData));
                if (length == 0)
                    break; // the connection reached EOF before the declared length; nothing more will ever arrive
                ReadData += length;
            }
        }

        /// <summary>
        /// Discards any unread data, bounded by <see cref="TimeoutToken" /> if a timeout was
        /// configured. Never closes the underlying <see cref="NetworkReader" />, even if
        /// cancelled — this may run before a response has been sent on this connection (e.g.
        /// while still parsing the request), so the connection must stay usable regardless of
        /// the outcome.
        /// </summary>
        public virtual Task DiscardAsync()
            => DiscardAsync(CancellationToken.None);

        /// <inheritdoc cref="DiscardAsync()" />
        public virtual async Task DiscardAsync(CancellationToken cancellationToken)
        {
            using var token = Link(cancellationToken);
            token.Token.ThrowIfCancellationRequested();
            var buffer = new byte[64 * 1024];
            while (UnreadData > 0)
            {
                var length = await reader.ReadAsync(
                    buffer,
                    0,
                    (int)Math.Min(buffer.Length, UnreadData),
                    token.Token
                )
                    .ConfigureAwait(false);
                if (length == 0)
                    break; // the connection reached EOF before the declared length; nothing more will ever arrive
                ReadData += length;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            ArgumentNullException.ThrowIfNull(buffer);
            if (offset < 0 || offset > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0 || count + offset > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(count));
            if (count > UnreadData)
                count = (int)UnreadData;
            var length = reader.Read(buffer, offset, count);
            ReadData += length;
            return length;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(buffer);
            if (offset < 0 || offset > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0 || count + offset > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(count));

            using var token = Link(cancellationToken);
            token.Token.ThrowIfCancellationRequested();
            if (count > UnreadData)
                count = (int)UnreadData;
            var length = await reader.ReadAsync(buffer, offset, count, token.Token)
                .ConfigureAwait(false);
            ReadData += length;
            return length;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            using var token = Link(cancellationToken);
            token.Token.ThrowIfCancellationRequested();
            var count = buffer.Length;
            if (count > UnreadData)
                count = (int)UnreadData;
            var length = await reader.ReadAsync(buffer[..count], token.Token).ConfigureAwait(false);
            ReadData += length;
            return length;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new InvalidOperationException("Cannot seek on this stream");
        }

        public override void SetLength(long value)
        {
            throw new InvalidOperationException("Cannot set length on this stream");
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new InvalidOperationException("Cannot write on this stream");
        }

        /// <remarks>
        /// The synchronous Dispose drains unread body data without honoring the read timeout and can
        /// block on a stalled client; prefer DisposeAsync.
        /// </remarks>
        protected override void Dispose(bool disposing)
        {
            try
            {
                Discard();
            }
            finally
            {
                reader.Dispose();
                timeoutSource?.Dispose();
                base.Dispose(disposing);
            }
        }

#pragma warning disable CA2215 // deliberate: would call sync Dispose again, which would block on a potentially stalled client
        public override async ValueTask DisposeAsync()
        {
            GC.SuppressFinalize(this);
            try
            {
                await DiscardAsync().ConfigureAwait(false);
            }
            finally
            {
                reader.Dispose();
                timeoutSource?.Dispose();
            }
        }
#pragma warning restore CA2215
    }
}
