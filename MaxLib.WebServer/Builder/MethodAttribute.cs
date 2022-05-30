using System.Collections.Generic;
using MaxLib.WebServer.Builder.Tools;

namespace MaxLib.WebServer.Builder
{
    public class MethodAttribute : RuleAttributeBase
    {
        public string Method { get; }

        public MethodAttribute(string method)
        {
            Method = method.ToUpperInvariant();
        }

        public override bool CanWorkWith(WebProgressTask task, Dictionary<string, object?> vars)
        {
            return Method == task.Request.ProtocolMethod.ToUpperInvariant();
        }
    }
}