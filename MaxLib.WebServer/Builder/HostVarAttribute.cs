using System;
using System.Collections.Generic;
using MaxLib.WebServer.Builder.Tools;

namespace MaxLib.WebServer.Builder
{
    /// <summary>
    /// Provides the currently fetched host name as a parameter.
    /// </summary>
    public class HostVarAttribute : Tools.ParamAttributeBase
    {
        public override Type Type => typeof(string);

        public override Result<object?> GetValue(WebProgressTask task, string field, Dictionary<string, object?> vars)
        {
            return new Result<object?>(task.Request.Host);
        }
    }
}