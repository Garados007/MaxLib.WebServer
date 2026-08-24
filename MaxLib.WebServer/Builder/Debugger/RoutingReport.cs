using System.Collections.Generic;

namespace MaxLib.WebServer.Builder.Debugger
{
    /// <summary>
    /// The full result of a <see cref="RoutingDryRun" />: for a single synthetic request, which
    /// registered services would have accepted, rejected or never seen it, and why.
    /// </summary>
    public sealed class RoutingReport
    {
        /// <summary>
        /// A human-readable summary of the simulated request (e.g. "GET /foo/2").
        /// </summary>
        public string RequestSummary { get; init; } = "";

        /// <summary>
        /// The <see cref="ServerStage" /> that was simulated.
        /// </summary>
        public ServerStage Stage { get; init; }

        /// <summary>
        /// The report for every top-level registered service, in priority order.
        /// </summary>
        public List<RoutingReportNode> Services { get; } = [];

        /// <summary>
        /// The <see cref="RoutingReportNode.Label" /> of the service that would actually have
        /// handled the request, or <c>null</c> if none did.
        /// </summary>
        public string? MatchedLabel { get; set; }

        /// <summary>
        /// An optional raw <see cref="Monitoring.Monitor" /> trace of the dry run, for the parts
        /// of the request that already emit <see cref="Monitoring.IWatch" /> log lines.
        /// </summary>
        public string? MonitorTrace { get; set; }
    }
}
