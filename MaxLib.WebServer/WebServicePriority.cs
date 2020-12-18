namespace MaxLib.WebServer
{
    /// <summary>
    /// The priority level at wich a <see cref="WebService" /> should be executed.
    /// </summary>
    public enum WebServicePriority : int
    {
        /// <summary>
        /// The highest priority possible. You can expect the service with this level to be 
        /// executed first. It is recommended to use <see cref="VeryHigh" /> or <see cref="High" />
        /// instead.
        /// </summary>
        God = int.MinValue,
        /// <summary>
        /// A very high priority. Use this if you intend to execute this service before others.
        /// </summary>
        VeryHigh = -1_000_000,
        /// <summary>
        /// A high priority. This is a lower priority then <see cref="VeryHigh" /> but higher
        /// than <see cref="Normal" />.
        /// </summary>
        High = -100,
        /// <summary>
        /// The normal default priority.
        /// </summary>
        Normal = 0,
        /// <summary>
        /// A low priority. This is a higher priority then <see cref="VeryLow" /> but lower
        /// than <see cref="Normal" />.
        /// </summary>
        Low = 100,
        /// <summary>
        /// A very low priority. Use this if you intend to execute this service after others.
        /// </summary>
        VeryLow = 1_0000_000,
        /// <summary>
        /// The lowest priority possible. You can expect the service with this level to be
        /// executed last. It is recommended to use <see cref="VeryLow" /> or <see cref="Low" />
        /// instead.
        /// </summary>
        Last = int.MaxValue,
    }
}