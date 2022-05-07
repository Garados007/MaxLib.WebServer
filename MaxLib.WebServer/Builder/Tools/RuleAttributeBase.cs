using System;
using System.Collections.Generic;

namespace MaxLib.WebServer.Builder.Tools
{
    [System.AttributeUsage(System.AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = true)]
    public abstract class RuleAttributeBase : System.Attribute
    {
        public abstract bool CanWorkWith(WebProgressTask task, Dictionary<string, object?> vars);
    }
}