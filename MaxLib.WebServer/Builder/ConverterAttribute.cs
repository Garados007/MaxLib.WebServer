using System;

namespace MaxLib.WebServer.Builder
{
    /// <summary>
    /// Specify the <see cref="Tools.IConverter" /> which should be used to convert the type of
    /// the variable.
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Parameter, Inherited = true, AllowMultiple = false)]
    public sealed class ConverterAttribute : System.Attribute
    {
        /// <summary>
        /// The converter to use.
        /// </summary>
        public Type Converter { get; }

        /// <summary>
        /// Specify the <see cref="Tools.IConverter" /> which should be used to convert the type of
        /// the variable.
        /// </summary>
        /// <param name="converter">The converter to use.</param>
        public ConverterAttribute(Type converter)
        {
            Converter = converter;
        }
    }
}