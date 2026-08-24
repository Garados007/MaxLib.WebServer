using System.Collections.Generic;

namespace MaxLib.WebServer.Builder.Debugger
{
    /// <summary>
    /// The build report for a single scanned type.
    /// </summary>
    public sealed class TypeReportNode : BuildReportNode
    {
        /// <summary>
        /// The full name of the scanned type.
        /// </summary>
        public string TypeFullName { get; init; } = "";

        /// <summary>
        /// The class-level rule attributes (e.g. <see cref="PathAttribute" />) found on the type,
        /// rendered via their <see cref="object.ToString" />.
        /// </summary>
        public List<string> Rules { get; } = [];

        /// <summary>
        /// The report for every scanned public method, in reflection order.
        /// </summary>
        public List<MethodReportNode> Methods { get; } = [];

        /// <summary>
        /// The report for every scanned nested type.
        /// </summary>
        public List<TypeReportNode> NestedTypes { get; } = [];
    }
}
