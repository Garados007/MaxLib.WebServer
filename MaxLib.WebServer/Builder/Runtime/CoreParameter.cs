using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MaxLib.WebServer.Builder.Tools;

namespace MaxLib.WebServer.Builder.Runtime
{
    public class CoreParameter : IParameter
    {
        public Func<WebProgressTask, Tools.Result<object?>> Getter { get; }

        public CoreParameter(Func<WebProgressTask, Tools.Result<object?>> getter)
        {
            Getter = getter;
        }

        public Result<object?> GetValue(WebProgressTask task, Dictionary<string, object?> vars)
        {
            return Getter(task);
        }

        private static readonly (Type type, Func<WebProgressTask, Tools.Result<object?>> getter)[] coreTypes =
        {
            (typeof(WebProgressTask), x => new Result<object?>(x)),
            (typeof(HttpDocument), x => new Result<object?>(x.Document)),
            (typeof(Server), x => new Result<object?>(x.Server)),
            (typeof(HttpConnection), x => new Result<object?>(x.Connection)),
            (typeof(Sessions.Session), x => new Result<object?>(x.Session)),
            (typeof(HttpRequestHeader), x => new Result<object?>(x.Request)),
            (typeof(HttpResponseHeader), x => new Result<object?>(x.Response)),
            (typeof(HttpLocation), x => new Result<object?>(x.Request.Location)),
            (typeof(HttpPost), x => new Result<object?>(x.Request.Post)),
            (typeof(HttpConnectionType), x => new Result<object?>(x.Request.FieldConnection)),
            (typeof(HttpCookie), x => new Result<object?>(x.Request.Cookie)),
            (typeof(Post.IPostData), x => x.Request.Post.Data == null ? new Result<object?>() : new Result<object?>(x.Request.Post.Data)),
            (typeof(Task<Post.IPostData>), x => x.Request.Post.DataAsync == null ? new Result<object?>() : new Result<object?>(x.Request.Post.DataAsync)),
            (typeof(Post.MultipartFormData), x => x.Request.Post.Data is Post.MultipartFormData data ? new Result<object?>(data) : new Result<object?>()),
            (typeof(Post.UnknownPostData), x => x.Request.Post.Data is Post.UnknownPostData data ? new Result<object?>(data) : new Result<object?>()),
            (typeof(Post.UrlEncodedData), x => x.Request.Post.Data is Post.UrlEncodedData data ? new Result<object?>(data) : new Result<object?>()),
            (typeof(Monitoring.Monitor), x => new Result<object?>(x.Monitor)),
            (typeof(Monitoring.IWatch), x => new Result<object?>(x.Monitor.Current)),
            (typeof(System.Net.IPEndPoint), x => x.Connection?.NetworkClient?.Client.RemoteEndPoint is System.Net.IPEndPoint data ? new Result<object?>(data) : new Result<object?>()),
            (typeof(System.Net.IPAddress), x => x.Connection?.NetworkClient?.Client.RemoteEndPoint is System.Net.IPEndPoint data ? new Result<object?>(data.Address) : new Result<object?>()),
        };

        public static CoreParameter? GetCoreParameter(Type target)
        {
            foreach (var (type, getter) in coreTypes)
                if (target.IsAssignableFrom(type))
                    return new CoreParameter(getter);
            return null;
        }
    }
}