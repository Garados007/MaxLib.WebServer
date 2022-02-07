using System;
using System.Threading.Tasks;

#nullable enable

namespace MaxLib.WebServer.Post
{
    public interface IPostData : IDisposable
    {
        string MimeType { get; }

        Task SetAsync(WebProgressTask task, IO.ContentStream content, string options);
    }
}