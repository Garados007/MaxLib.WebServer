using System;
using System.Threading.Tasks;

#nullable enable

namespace MaxLib.WebServer.Post
{
    public sealed class UnknownPostData : IPostData, IAsyncDisposable
    {
        public IO.ContentStream Data { get; private set; }

        public string MimeType { get; }

        public UnknownPostData(IO.ContentStream data, string? mime)
        {
            MimeType = mime ?? WebServer.MimeType.ApplicationOctetStream;
            Data = data;
        }

        public Task SetAsync(WebProgressTask task, IO.ContentStream content, string options)
        {
            Data = content;
            return Task.CompletedTask;
        }

        public override string ToString()
        {
            return $"[{Data.Length:#,#0} Bytes]";
        }

        /// <remarks>
        /// The synchronous Dispose drains the body without the read timeout; prefer DisposeAsync.
        /// </remarks>
        public void Dispose()
        {
            Data.Discard();
        }

        public async ValueTask DisposeAsync()
        {
            await Data.DiscardAsync().ConfigureAwait(false);
        }
    }
}