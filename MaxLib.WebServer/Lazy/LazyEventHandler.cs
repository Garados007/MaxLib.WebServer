using System.Collections.Generic;

namespace MaxLib.WebServer.Lazy
{
    public delegate IEnumerable<HttpDataSource> LazyEventHandler(LazyTask task);
}
