namespace MaxLib.WebServer.Builder.Debugger
{
    /// <summary>
    /// The outcome of a single candidate service during a <see cref="RoutingDryRun" />.
    /// </summary>
    public enum RoutingOutcome
    {
        /// <summary>
        /// The service would have handled the request.
        /// </summary>
        Accepted,
        /// <summary>
        /// The service was evaluated but rejected the request.
        /// </summary>
        Rejected,
        /// <summary>
        /// A higher-priority sibling already matched first (the stage only allows a single
        /// service to run), so this candidate was never evaluated.
        /// </summary>
        NotReached,
    }
}
