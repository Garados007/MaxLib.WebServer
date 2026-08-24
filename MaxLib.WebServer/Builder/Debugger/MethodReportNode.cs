using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MaxLib.WebServer.Builder.Debugger
{
    /// <summary>
    /// The build report for a single scanned method.
    /// </summary>
    public sealed class MethodReportNode : BuildReportNode
    {
        /// <summary>
        /// The full name of the type the method was declared on.
        /// </summary>
        public string DeclaringType { get; init; } = "";

        /// <summary>
        /// The rule attributes (e.g. <see cref="PathAttribute" />, <see cref="MethodAttribute"
        /// />) found on the method, rendered via their <see cref="object.ToString" />.
        /// </summary>
        public List<string> Rules { get; } = [];

        /// <summary>
        /// The method's dispatch priority, set only when a <see cref="PriorityAttribute" /> is
        /// explicitly present - empty when the method uses the inherited default (<see
        /// cref="WebServicePriority.Normal" />).
        /// </summary>
        public string Priority { get; set; } = "";

        /// <summary>
        /// The report for every scanned parameter, in declaration order.
        /// </summary>
        public List<ParameterReportNode> Parameters { get; } = [];

        /// <summary>
        /// The report for the method's return value conversion, if the scan got that far.
        /// </summary>
        public ResultReportNode? Result { get; set; }

        /// <summary>
        /// Set when <see cref="BuildReasonCode.MethodConstructorThrew" /> applies: the message of
        /// the exception the declaring type's constructor threw.
        /// </summary>
        public string? ExceptionMessage { get; set; }

        /// <summary>
        /// Set when <see cref="BuildReasonCode.MethodConstructorThrew" /> applies: the stack trace
        /// of the exception the declaring type's constructor threw.
        /// </summary>
        public string? ExceptionStackTrace { get; set; }

        /// <summary>
        /// Set when <see cref="BuildReasonCode.MethodConstructorThrew" /> applies: the exception
        /// the declaring type's constructor threw. Not serialized, since an arbitrary
        /// <see cref="Exception" /> subclass may not be safely reflectable.
        /// </summary>
        [JsonIgnore]
        public Exception? Exception { get; set; }
    }
}
