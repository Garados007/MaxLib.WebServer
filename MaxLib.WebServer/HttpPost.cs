﻿using System;
using System.Collections.Generic;
using MaxLib.WebServer.Post;

#nullable enable

namespace MaxLib.WebServer
{
    [Serializable]
    public class HttpPost
    {
        public string? MimeType { get; private set; }


        protected Lazy<IPostData>? LazyData { get; set; }
        public IPostData? Data => LazyData?.Value;

        public static Dictionary<string, Func<IPostData>> DataHandler { get; }
            = new Dictionary<string, Func<IPostData>>();

        static HttpPost()
        {
            DataHandler[WebServer.MimeType.ApplicationXWwwFromUrlencoded] =
                () => new UrlEncodedData();
            DataHandler[WebServer.MimeType.MultipartFormData] =
                () => new MultipartFormData();
        }

        public virtual void SetPost(ReadOnlyMemory<byte> post, string? mime)
        {
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
                LazyData = new Lazy<IPostData>(() =>
                {
                    var data = constructor();
                    data.Set(post, args);
                    return data;
                }, System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);
            else LazyData = new Lazy<IPostData>(new UnknownPostData(post, mime));
        }

        public HttpPost()
        {
        }

        public HttpPost(ReadOnlyMemory<byte> post, string? mime)
            : this()
            => SetPost(post, mime);


        public override string ToString()
        {
            return $"{MimeType}: {Data}";
        }
    }
}
