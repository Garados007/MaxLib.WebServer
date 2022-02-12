using System;
using System.Collections.Generic;

namespace MaxLib.WebServer.Builder.Runtime
{
    public interface IParameter
    {
        Tools.Result<object?> GetValue(WebProgressTask task, Dictionary<string, object?> vars);
    }
}