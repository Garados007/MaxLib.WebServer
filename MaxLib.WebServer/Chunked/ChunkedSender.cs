using MaxLib.WebServer.Lazy;
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
            return !OnlyWithLazy || (task.Document.DataSources.Count > 0 &&
                task.Document.DataSources.Any((s) => s is LazySource ||
                    (s is Remote.MarshalSource ms && ms.IsLazy)
                )) || task.Document.DataSources.Any(s => s.Length() is null);
        }

        public override async Task ProgressTask(WebProgressTask task)
        {
            ArgumentNullException.ThrowIfNull(task);
            var header = task.Response;
            var stream = task.NetworkStream;
            if (stream is null)
                return;
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
                await writer.WriteAsync(e.Key).ConfigureAwait(false);
                await writer.WriteAsync(": ").ConfigureAwait(false);
                await writer.WriteLineAsync(e.Value).ConfigureAwait(false);
            }
            foreach (var cookie in task.Request.Cookie.AddedCookies) //Cookies
            {
                await writer.WriteAsync("Set-Cookie: ").ConfigureAwait(false);
                await writer.WriteLineAsync(cookie.ToString()).ConfigureAwait(false);
            }
            await writer.WriteLineAsync().ConfigureAwait(false);
            try { await writer.FlushAsync().ConfigureAwait(false); await stream.FlushAsync().ConfigureAwait(false); }
            catch (ObjectDisposedException)
            {
                WebServerLog.Add(ServerLogType.Information, GetType(), "Send", "Connection closed by remote host.");
                return;
            }
            catch (IOException)
            {
                WebServerLog.Add(ServerLogType.Information, GetType(), "Send", "Connection closed by remote host.");
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
                WebServerLog.Add(ServerLogType.Information, GetType(), "Send", "Connection closed by remote host.");
                return;
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
            else if (source is Remote.MarshalSource ms && ms.IsLazy)
            {
                var lazySources = ms.GetAllSources();
                if (lazySources != null)
                    foreach (var s in lazySources)
                        await SendChunk(writer, stream, s).ConfigureAwait(false);
            }
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
                        _ = Task.Run(async () =>
                        {
                            await source.WriteStream(sink).ConfigureAwait(false);
                            sink.FinishWrite();
                        });
#pragma warning disable CA2000 // sink already has its own using-block; HttpChunkedStream.Dispose() disposing it a second time is safe, adding a using here isn't
                        await SendChunk(writer, stream, new HttpChunkedStream(sink)).ConfigureAwait(false);
#pragma warning restore CA2000
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
