using System.Data;
using System;
using System.Collections.Generic;

namespace MaxLib.WebServer.Lazy
{
    [Serializable]
    public class LazyTask
    {
        public Server Server { get; }

        public HttpConnection Connection { get; }

        public HttpRequestHeader Header { get; }

        public Dictionary<object, object> Information { get; }
        
        public object this[object identifer]
        {
            get => Information[identifer];
            set => Information[identifer] = value;
        }

        public LazyTask(WebProgressTask task)
        {
            _ = task ?? throw new ArgumentNullException(nameof(task));
            Server = task.Server;
            Connection = task.Connection;
            Header = task.Document.RequestHeader;
            Information = task.Document.Information;
        }
    }
}
