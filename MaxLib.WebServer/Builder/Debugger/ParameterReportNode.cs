namespace MaxLib.WebServer.Builder.Debugger
{
    /// <summary>
    /// The build report for a single method parameter.
    /// </summary>
    public sealed class ParameterReportNode : BuildReportNode
    {
        /// <summary>
        /// The full name of the parameter's declared type.
        /// </summary>
        public string ParameterType { get; init; } = "";

        /// <summary>
        /// How the parameter's value is resolved: the short name of the <see
        /// cref="Tools.ParamAttributeBase" /> attribute applied to it plus the key it actually
        /// reads (e.g. <c>"Var: id"</c>, <c>"Get: bar"</c> - which can differ from the
        /// parameter's own name), or <c>"Core parameter"</c> if none is set and it was
        /// successfully resolved as a framework-injected type. Null if neither applies - e.g. an
        /// unattributed parameter whose type isn't a known framework type either, so there is no
        /// meaningful source to report (see <see cref="BuildReasonCode.ParamNoCoreConverterFound"
        /// />).
        /// </summary>
        public string? Source { get; set; }
    }
}
