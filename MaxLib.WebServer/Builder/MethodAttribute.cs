using System;
using System.Collections.Generic;
using MaxLib.WebServer.Builder.Tools;

namespace MaxLib.WebServer.Builder
{
    public class MethodAttribute : RuleAttributeBase, Debugger.IExplainableRule
    {
        public string Method { get; }

        public MethodAttribute(string method)
        {
            ArgumentNullException.ThrowIfNull(method);
            Method = method.ToUpperInvariant();
        }

        public override bool CanWorkWith(WebProgressTask task, Dictionary<string, object?> vars)
        {
            ArgumentNullException.ThrowIfNull(task);
            return string.Equals(Method, task.Request.ProtocolMethod, StringComparison.OrdinalIgnoreCase);
        }

        public override string ToString() => $"Method: {Method}";

        bool Debugger.IExplainableRule.CanWorkWith(WebProgressTask task, Dictionary<string, object?> vars, out string? reason)
        {
            ArgumentNullException.ThrowIfNull(task);
            if (string.Equals(Method, task.Request.ProtocolMethod, StringComparison.OrdinalIgnoreCase))
            {
                reason = null;
                return true;
            }
            reason = $"expected HTTP method {Method}, request was {task.Request.ProtocolMethod}";
            return false;
        }
    }
}