namespace MaxLib.WebServer.Builder
{
    /// <summary>
    /// This defines the MIME Type of the result. If you use this attribute the MIME type for all datasources will
    /// be overwritten with this.
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.ReturnValue, Inherited = true, AllowMultiple = false)]
    public sealed class MimeAttribute : System.Attribute
    {
        public string Mime { get; }


        /// <summary>
        /// Defines the MIME Type of the result. If you use this attribute the MIME type for all datasources will
        /// be overwritten with this.
        /// </summary>
        /// <param name="mime">The mime type to use</param>
        public MimeAttribute(string mime)
        {
            Mime = mime;
        }
    }
}