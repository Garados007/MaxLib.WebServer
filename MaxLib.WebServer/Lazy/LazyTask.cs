using System.Data;
using System;
using System.Collections.Generic;

#nullable enable

namespace MaxLib.WebServer.Lazy
{
    [Serializable]
    public class LazyTask
    {
        public Server Server { get; }

        public HttpConnection Connection { get; }

        public HttpRequestHeader Header { get; }

        public Dictionary<object?, object?> Information { get; }
        
        public object? this[object? identifer]
        {
            get => Information[identifer];
            set => Information[identifer] = value;
        }

        public LazyTask(WebProgressTask task)
        {
            _ = task ?? throw new ArgumentNullException(nameof(task));
            Server = task.Server ?? throw new ArgumentNullException(nameof(task.Server));
            Connection = task.Connection ?? throw new ArgumentNullException(nameof(task.Connection));
            Header = task.Request;
            Information = task.Document.Information;
        }
    }
}
