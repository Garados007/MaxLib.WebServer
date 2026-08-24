namespace MaxLib.WebServer.Builder.Debugger
{
    /// <summary>
    /// A single scanned candidate (type, method, parameter or result) encountered while the
    /// <see cref="Tools.Generator" /> walked a type for building services, together with the
    /// outcome of that scan.
    /// </summary>
    public abstract class BuildReportNode
    {
        /// <summary>
        /// The display name of the scanned candidate (type/method/parameter name).
        /// </summary>
        public string Name { get; init; } = "";

        /// <summary>
        /// The outcome of the scan.
        /// </summary>
        public BuildReportStatus Status { get; set; } = BuildReportStatus.Accepted;

        /// <summary>
        /// The specific reason for <see cref="Status" />.
        /// </summary>
        public BuildReasonCode Reason { get; set; } = BuildReasonCode.Accepted;

        /// <summary>
        /// A human-readable explanation of <see cref="Reason" />.
        /// </summary>
        public string Message { get; set; } = "";
    }
}
