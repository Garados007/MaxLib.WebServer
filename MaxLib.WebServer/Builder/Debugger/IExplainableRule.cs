using System.Collections.Generic;

namespace MaxLib.WebServer.Builder.Debugger
{
    /// <summary>
    /// An additive, opt-in capability a <see cref="Tools.RuleAttributeBase" /> can implement to
    /// give <see cref="RoutingDryRun" /> a precise reason for a rejection instead of the generic
    /// "rule rejected the request" fallback. Kept as a separate interface (rather than changing
    /// <see cref="Tools.RuleAttributeBase.CanWorkWith(WebProgressTask, Dictionary{string, object?})"
    /// />'s signature) since that method is a public contract already implemented by third-party
    /// rule attributes.
    /// </summary>
    public interface IExplainableRule
    {
        /// <summary>
        /// Works exactly like <see cref="Tools.RuleAttributeBase.CanWorkWith(WebProgressTask,
        /// Dictionary{string, object?})" />, but additionally explains a rejection.
        /// </summary>
        /// <param name="task">The task that is currently executed.</param>
        /// <param name="vars">The variables collected so far for this request.</param>
        /// <param name="reason">
        /// Set to a human-readable explanation when this returns <c>false</c>; otherwise
        /// <c>null</c>.
        /// </param>
        bool CanWorkWith(WebProgressTask task, Dictionary<string, object?> vars, out string? reason);
    }
}
