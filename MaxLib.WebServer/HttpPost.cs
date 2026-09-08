using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MaxLib.WebServer.Post;

#nullable enable

namespace MaxLib.WebServer
{
    [Serializable]
    public class HttpPost : IDisposable, IAsyncDisposable
    {
        public string? MimeType { get; private set; }

        internal IO.ContentStream? Content { get; private set; }

        private Lazy<Task<IPostData>>? LazyData;
        public Task<IPostData>? DataAsync => LazyData?.Value;
        public IPostData? Data => DataAsync?.Result;

        public static Dictionary<string, Func<IPostData>> DataHandler { get; }
            = new Dictionary<string, Func<IPostData>>();

        static HttpPost()
        {
            DataHandler[WebServer.MimeType.ApplicationXWwwFromUrlencoded] =
                () => new UrlEncodedData();
            DataHandler[WebServer.MimeType.MultipartFormData] =
                () => new MultipartFormData();
            DataHandler[WebServer.MimeType.ApplicationJson] =
                () => new RawPostData(WebServer.MimeType.ApplicationJson);
            DataHandler[WebServer.MimeType.ApplicationOctetStream] =
                () => new RawPostData(WebServer.MimeType.ApplicationOctetStream);
        }

        public virtual void SetPost(WebProgressTask task, IO.ContentStream content, string? mime)
        {
            Content = content;
            string args = "";
            if (mime != null)
            {
                var ind = mime.IndexOf(';', StringComparison.Ordinal);
                if (ind >= 0)
                {
                    args = mime[(ind + 1)..];
                    mime = mime[..ind];
                }
            }
            MimeType = mime;

            // an unrecognized (or missing) Content-Type still gets its body read and stored via
            // RawPostData, exactly like a registered mime type would - it is never left as an
            // unread reference to the live connection stream
            var constructor = mime != null && DataHandler.TryGetValue(mime, out Func<IPostData>? found)
                ? found
                : () => new RawPostData(mime);
            LazyData = new Lazy<Task<IPostData>>(() =>
            {
                return Task.Run(async () =>
                {
                    var data = constructor();
                    await data.SetAsync(task, content, args).ConfigureAwait(false);
                    return data;
                });
            }, System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);
        }

        public HttpPost()
        {
        }

#pragma warning disable CA2000 // ownership transfers through the chained ctor into Content, disposed by HttpPost.Dispose()
        public HttpPost(WebProgressTask task, ReadOnlyMemory<byte> content, string? mime)
            : this(
                task,
                new IO.ContentStream(
                    new IO.NetworkReader(new IO.SpanStream(content)),
                    content.Length
                ),
                mime
            )
        {

        }
#pragma warning restore CA2000

        public HttpPost(WebProgressTask task, IO.ContentStream content, string? mime)
            : this()
            => SetPost(task, content, mime);


        public override string ToString()
        {
            return $"{MimeType}: {Data}";
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            if (LazyData != null && LazyData.IsValueCreated)
            {
                Task.Run(async () =>
                {
                    var data = await LazyData.Value.ConfigureAwait(false);
                    data.Dispose();
                });
            }
            Content?.Dispose();
        }

        /// <summary>
        /// Disposes the resolved <see cref="IPostData" /> (if one was ever requested via <see
        /// cref="Data" />/<see cref="DataAsync" />) and the underlying request content, without
        /// blocking a thread on a synchronous socket read while doing so. Prefer this over <see
        /// cref="Dispose" />, which cannot wait for the (possibly still in-flight) <see
        /// cref="IPostData" /> to finish parsing before disposing it.
        /// </summary>
        /// <remarks>
        /// Only call this once nothing further needs the request's connection — in
        /// particular, only after any response on it has already been sent (see <see
        /// cref="Services.HttpSender" />, which does exactly this). If the underlying drain
        /// is cancelled (e.g. a read timeout), <see cref="IO.ContentStream.DisposeAsync" />
        /// closes that connection; doing so any earlier could prevent a response from ever
        /// reaching the client.
        /// </remarks>
        public async ValueTask DisposeAsync()
        {
            GC.SuppressFinalize(this);
            if (LazyData != null && LazyData.IsValueCreated)
            {
                var data = await LazyData.Value.ConfigureAwait(false);
                if (data is IAsyncDisposable asyncDisposable)
                    await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                else
                    data.Dispose();
            }
            if (Content != null)
                await Content.DisposeAsync().ConfigureAwait(false);
        }
    }
}
