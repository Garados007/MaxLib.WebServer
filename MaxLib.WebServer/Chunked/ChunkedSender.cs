using MaxLib.WebServer.Lazy;
using Microsoft.Extensions.Logging;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MaxLib.IO;

#nullable enable

namespace MaxLib.WebServer.Chunked
{
    public class ChunkedSender : Services.HttpSender
    {
        static readonly ILogger logger = WebServerLog.LoggerFactory.CreateLogger<ChunkedSender>();
        static readonly EventId SendEventId = new(0, "Send");
        static readonly EventId WriteStreamEventId = new(1, "Write Stream");

        /// <summary>
        /// Thrown by <see cref="SendChunk(StreamWriter, Stream, HttpDataSource)"/>'s unbounded-length path when the
        /// source's <c>WriteStream</c> fails after the response has started. Deliberately neither an
        /// <see cref="IOException"/> (the send loop treats that as a client disconnect) nor an <c>HttpException</c>
        /// (that would build a new response that can no longer reach the client), so it reaches
        /// <c>Server.ProcessTask</c>, which closes the connection and leaves the chunked body unterminated.
        /// </summary>
        internal sealed class WriteStreamFailedException : Exception
        {
            public WriteStreamFailedException(Exception innerException)
                : base("WriteStream failed while bridging an unbounded-length source through ChunkedSender.", innerException)
            {
            }
        }

        private sealed class WriteFaultBox
        {
            public volatile Exception? Exception;
        }

        public bool OnlyWithLazy { get; private set; }

        public ChunkedSender(bool onlyWithLazy = false) : base()
        {
            OnlyWithLazy = onlyWithLazy;
            if (onlyWithLazy)
                Priority = WebServicePriority.High;
        }

        public override bool CanWorkWith(WebProgressTask task)
        {
            ArgumentNullException.ThrowIfNull(task);
#pragma warning disable CS0618 // Remote.MarshalSource is obsolete; support kept until its removal
            return !OnlyWithLazy || (task.Document.DataSources.Count > 0 &&
                task.Document.DataSources.Any((s) => s is LazySource ||
                    (s is Remote.MarshalSource ms && ms.IsLazy)
                )) || task.Document.DataSources.Any(s => s.Length() is null);
#pragma warning restore CS0618
        }

        public override async Task ProgressTask(WebProgressTask task)
        {
            ArgumentNullException.ThrowIfNull(task);
            var header = task.Response;
            var stream = task.NetworkStream;
            if (stream is null)
                return;
            try
            {
#pragma warning disable CA2000 // must not dispose: would close the still-needed connection stream, and StreamWriter's default no-BOM encoding must not be swapped just to add leaveOpen
                var writer = new StreamWriter(stream);
#pragma warning restore CA2000
                await writer.WriteAsync(header.HttpProtocol).ConfigureAwait(false);
                await writer.WriteAsync(" ").ConfigureAwait(false);
                await writer.WriteAsync(((int)header.StatusCode).ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
                await writer.WriteAsync(" ").ConfigureAwait(false);
                await writer.WriteLineAsync(StatusCodeText(header.StatusCode)).ConfigureAwait(false);
                for (int i = 0; i < header.HeaderParameter.Count; ++i) //Parameter
                {
                    var e = header.HeaderParameter.ElementAt(i);
                    if (e.Key == "Content-Length")
                        continue;
                    await writer.WriteAsync(WebServerUtils.RemoveCrLf(e.Key)).ConfigureAwait(false);
                    await writer.WriteAsync(": ").ConfigureAwait(false);
                    await writer.WriteLineAsync(WebServerUtils.RemoveCrLf(e.Value)).ConfigureAwait(false);
                }
                foreach (var cookie in task.Request.Cookie.AddedCookies) //Cookies
                {
                    await writer.WriteAsync("Set-Cookie: ").ConfigureAwait(false);
                    await writer.WriteLineAsync(cookie.Value.ToString()).ConfigureAwait(false);
                }
                await writer.WriteLineAsync().ConfigureAwait(false);
                try { await writer.FlushAsync().ConfigureAwait(false); await stream.FlushAsync().ConfigureAwait(false); }
                catch (ObjectDisposedException)
                {
                    logger.LogInformation(SendEventId, "Connection closed by remote host.");
                    return;
                }
                catch (IOException)
                {
                    logger.LogInformation(SendEventId, "Connection closed by remote host.");
                    return;
                }
                //send data
                try
                {
                    if (!(task.Document.Information.ContainsKey("Only Header") && (bool)task.Document.Information["Only Header"]!))
                    {
                        foreach (var s in task.Document.DataSources)
                            await SendChunk(writer, stream, s).ConfigureAwait(false);
                        await writer.WriteLineAsync("0").ConfigureAwait(false);
                        await writer.WriteLineAsync().ConfigureAwait(false);
                        await writer.FlushAsync().ConfigureAwait(false);
                        await stream.FlushAsync().ConfigureAwait(false);
                    }
                }
                catch (IOException)
                {
                    logger.LogInformation(SendEventId, "Connection closed by remote host.");
                    return;
                }
            }
            finally
            {
                // replaces HttpSender.ProgressTask entirely, so it must dispose the request's POST data itself
                // (temp files, unread body bytes on a keep-alive connection)
                await DisposeRequestPostAsync(task).ConfigureAwait(false);
            }
        }

        protected virtual async Task SendChunk(StreamWriter writer, Stream stream, HttpDataSource source)
        {
            ArgumentNullException.ThrowIfNull(writer);
            ArgumentNullException.ThrowIfNull(stream);
            ArgumentNullException.ThrowIfNull(source);
            if (source is LazySource lazySource)
                foreach (var s in lazySource.GetAllSources())
                    await SendChunk(writer, stream, s).ConfigureAwait(false);
#pragma warning disable CS0618 // Remote.MarshalSource is obsolete; support kept until its removal
            else if (source is Remote.MarshalSource ms && ms.IsLazy)
            {
                var lazySources = ms.GetAllSources();
                if (lazySources != null)
                    foreach (var s in lazySources)
                        await SendChunk(writer, stream, s).ConfigureAwait(false);
            }
#pragma warning restore CS0618
            else if (source is HttpChunkedStream)
            {
                await stream.FlushAsync().ConfigureAwait(false);
                await writer.FlushAsync().ConfigureAwait(false);
                await source.WriteStream(stream).ConfigureAwait(false);
                await stream.FlushAsync().ConfigureAwait(false);
                await writer.FlushAsync().ConfigureAwait(false);
            }
            else
            {
                var length = source.Length();
                if (length == null)
                    using (var sink = new BufferedSinkStream())
                    {
                        var fault = new WriteFaultBox();
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await source.WriteStream(sink).ConfigureAwait(false);
                            }
                            catch (Exception e)
                            {
                                // The reader must always observe completion, even if WriteStream fails before calling FinishWrite(),
                                // or it blocks forever. The captured exception is surfaced below once the reader has drained the written data.
                                fault.Exception = e;
                                try
                                {
                                    logger.LogError(WriteStreamEventId, e,
                                        "WriteStream failed while bridging an unbounded-length source through ChunkedSender");
                                }
                                catch (Exception)
                                {
                                    // logging itself must never crash this background task
                                }
                            }
                            finally
                            {
                                sink.FinishWrite();
                            }
                        });
#pragma warning disable CA2000 // sink already has its own using-block; HttpChunkedStream.Dispose() disposing it a second time is safe, adding a using here isn't
                        await SendChunk(writer, stream, new HttpChunkedStream(sink)).ConfigureAwait(false);
#pragma warning restore CA2000
                        if (fault.Exception is Exception writeException)
                            throw new WriteStreamFailedException(writeException);
                    }
                //using (var m = new MemoryStream())
                //{
                //    source.WriteStream(m);
                //    if (m.Length == 0)
                //        return;
                //    writer.WriteLine(m.Length.ToString("X"));
                //    writer.Flush();
                //    m.Position = 0;
                //    m.WriteTo(stream);
                //}
                else
                {
                    if (length.Value == 0) return;
                    await writer.WriteLineAsync(length.Value.ToString("X", CultureInfo.InvariantCulture)).ConfigureAwait(false);
                    await writer.FlushAsync().ConfigureAwait(false);
                    await source.WriteStream(stream).ConfigureAwait(false);
                }
                await stream.FlushAsync().ConfigureAwait(false);
                await writer.WriteLineAsync().ConfigureAwait(false);
                await writer.FlushAsync().ConfigureAwait(false);
            }
        }
    }
}
