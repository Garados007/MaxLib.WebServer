namespace MaxLib.WebServer.Builder.Debugger
{
    /// <summary>
    /// The build report for a method's return value conversion.
    /// </summary>
    public sealed class ResultReportNode : BuildReportNode
    {
        /// <summary>
        /// The full name of the method's declared return type.
        /// </summary>
        public string ReturnType { get; init; } = "";

        /// <summary>
        /// The name of the <see cref="Tools.IDataConverter" /> that was actually used to convert
        /// the result - either from a <see cref="DataConverterAttribute" /> or, if none is set,
        /// the inherited default (<see cref="Converter.DataConverter" />). Also set to the
        /// attempted converter's type name when a <see cref="DataConverterAttribute" /> is
        /// present but invalid or fails to construct. Null when no converter was ever
        /// successfully resolved and none was explicitly requested either (see <see
        /// cref="BuildReasonCode.ResultNoConverter" />) - there's nothing meaningful to report.
        /// </summary>
        public string? Converter { get; set; }
    }
}
