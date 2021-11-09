using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MaxLib.WebServer.Post;

#nullable enable

namespace MaxLib.WebServer
{
    [Serializable]
    public class HttpPost : IDisposable
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
        }

        public virtual void SetPost(IO.ContentStream content, string? mime)
        {
            Content = content;
            string args = "";
            if (mime != null)
            {
                var ind = mime.IndexOf(';');
                if (ind >= 0)
                {
                    args = mime.Substring(ind + 1);
                    mime = mime.Remove(ind);
                }
            }

            if ((MimeType = mime) != null &&
                DataHandler.TryGetValue(mime!, out Func<IPostData> constructor)
            )
                LazyData = new Lazy<Task<IPostData>>(() =>
                {
                    return Task.Run(async () =>
                    {
                        var data = constructor();
                        await data.SetAsync(content, args).ConfigureAwait(false);
                        return data;
                    });
                }, System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);
            else LazyData = new Lazy<Task<IPostData>>(
                Task.FromResult<IPostData>(new UnknownPostData(content, mime))
            );
        }

        public HttpPost()
        {
        }

        public HttpPost(ReadOnlyMemory<byte> content, string? mime)
            : this(
                new IO.ContentStream(
                    new IO.NetworkReader(new IO.SpanStream(content)),
                    content.Length
                ), 
                mime
            )
        {

        }

        public HttpPost(IO.ContentStream content, string? mime)
            : this()
            => SetPost(content, mime);


        public override string ToString()
        {
            return $"{MimeType}: {Data}";
        }

        public void Dispose()
        {
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
    }
}
