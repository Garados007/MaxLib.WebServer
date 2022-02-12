using System;

namespace MaxLib.WebServer.Builder
{
    /// <summary>
    /// Specify the <see cref="Tools.IDataConverter" /> which should be used to convert the type of
    /// the variable.
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.ReturnValue, Inherited = true, AllowMultiple = false)]
    public sealed class DataConverterAttribute : System.Attribute
    {
        /// <summary>
        /// The converter to use.
        /// </summary>
        public Type Converter { get; }

        /// <summary>
        /// Specify the <see cref="Tools.IDataConverter" /> which should be used to convert the type of
        /// the variable.
        /// </summary>
        /// <param name="converter">The converter to use.</param>
        public DataConverterAttribute(Type converter)
        {
            Converter = converter;
        }
    }
}