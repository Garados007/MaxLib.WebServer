using System;

namespace MaxLib.WebServer.Builder
{
    /// <summary>
    /// Ignores this class or method for the web service builder
    /// </summary>
    [System.AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public sealed class IgnoreAttribute : System.Attribute
    {
    /// <summary>
    /// Ignores this class or method for the web service builder
    /// </summary>
        public IgnoreAttribute()
        {
        }
    }
}