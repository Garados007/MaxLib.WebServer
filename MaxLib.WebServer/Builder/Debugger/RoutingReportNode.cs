using System.Collections.Generic;

namespace MaxLib.WebServer.Builder.Debugger
{
    /// <summary>
    /// The routing outcome for a single candidate <see cref="WebService" />, and (for a
    /// <see cref="Runtime.ServiceGroup" />) its nested candidates.
    /// </summary>
    public sealed class RoutingReportNode
    {
        /// <summary>
        /// A human-readable label for the candidate (e.g. its method name or class-level rules).
        /// </summary>
        public string Label { get; init; } = "";

        /// <summary>
        /// The user type or method this candidate maps to, if the builder can supply one (a
        /// <see cref="Runtime.ServiceGroup" />'s originating type, or a
        /// <see cref="Runtime.MethodService" />'s declaring type and method). Null for a
        /// hand-written <see cref="WebService" /> with no such mapping.
        /// </summary>
        public string? Source { get; init; }

        /// <summary>
        /// The dispatch priority of the candidate.
        /// </summary>
        public WebServicePriority Priority { get; init; }

        /// <summary>
        /// The outcome of evaluating this candidate.
        /// </summary>
        public RoutingOutcome Outcome { get; set; }

        /// <summary>
        /// One entry per reason the candidate rejected the request (a failing rule and/or an
        /// unresolvable parameter). Empty when <see cref="Outcome" /> is
        /// <see cref="RoutingOutcome.Accepted" /> or <see cref="RoutingOutcome.NotReached" />.
        /// </summary>
        public List<string> Reasons { get; } = [];

        /// <summary>
        /// The nested candidates, if this node represents a <see cref="Runtime.ServiceGroup" />.
        /// Empty for a leaf service.
        /// </summary>
        public List<RoutingReportNode> Children { get; } = [];
    }
}
