namespace MaxLib.WebServer.Builder.Debugger
{
    /// <summary>
    /// The outcome of a single scanned type, method, parameter or result during a builder run.
    /// </summary>
    public enum BuildReportStatus
    {
        /// <summary>
        /// The node was successfully turned into a working part of the service tree.
        /// </summary>
        Accepted,
        /// <summary>
        /// The node was deliberately excluded (e.g. <see cref="IgnoreAttribute" />, abstract,
        /// generic, not public). This is usually intentional.
        /// </summary>
        Ignored,
        /// <summary>
        /// The node could not be built although it looked like it should have been (e.g. a
        /// missing converter or a constructor that threw). This is usually a misconfiguration.
        /// </summary>
        Failed,
    }
}
