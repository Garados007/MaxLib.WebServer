using System.Collections.Generic;

#nullable enable

namespace MaxLib.WebServer.Lazy
{
    public delegate IEnumerable<HttpDataSource> LazyEventHandler(LazyTask task);
}
