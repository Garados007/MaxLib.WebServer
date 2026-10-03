namespace MaxLib.WebServer.Builder
{
    /// <summary>
    /// Sets the Priority the method will run.<br/>
    /// For two methods with overlapping <see cref="PathAttribute" /> routes, this is only
    /// needed to break a tie once <see cref="PathAttribute" />'s own specificity ordering
    /// (an exact match over a prefix match, more literal segments over <c>{var}</c> ones) is
    /// also equal - which resolution order is otherwise not guaranteed.
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

        public override string ToString() => $"Priority: {Priority}";
    }
}