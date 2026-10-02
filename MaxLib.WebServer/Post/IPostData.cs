using System;
using System.Threading.Tasks;

#nullable enable

namespace MaxLib.WebServer.Post
{
    /// <remarks>
    /// Custom IPostData implementations and EncodingProviders must throw exceptions with a non-throwing
    /// ToString(); they are passed to the logger as-is. SetAsync must be called at most once per
    /// instance; a second call leaks the previous call's temp files. Dispose is not thread-safe: await
    /// SetAsync before calling Dispose, otherwise temp files may leak.
    /// </remarks>
    public interface IPostData : IDisposable
    {
        string MimeType { get; }

        Task SetAsync(WebProgressTask task, IO.ContentStream content, string options);
    }
}