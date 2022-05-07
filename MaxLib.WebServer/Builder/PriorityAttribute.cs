using System;

namespace MaxLib.WebServer.Builder
{
    /// <summary>
    /// Sets the Priority the method will run.
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public sealed class PriorityAttribute : System.Attribute
    {
        public WebServicePriority Priority { get; set; }

        /// <summary>
        /// Sets the Priority the method will run.
        /// </summary>
        /// <param name="priority">The priority this method will run</param>
        public PriorityAttribute(WebServicePriority priority)
        {
            Priority = priority;
        }
    }
}