using System;

namespace MaxLib.WebServer.Builder
{
    /// <summary>
    /// Specify the <see cref="Tools.IConverter" /> which should be used to convert the type of
    /// the variable.
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Parameter, Inherited = true, AllowMultiple = false)]
    public class ConverterAttribute : System.Attribute
    {
        /// <summary>
        /// The converter to use.
        /// </summary>
        public Type Converter { get; }

        /// <summary>
        /// The instance of the current converter.
        /// </summary>
        public Tools.IConverter? Instance { get; protected set; }

        /// <summary>
        /// Specify the <see cref="Tools.IConverter" /> which should be used to convert the type of
        /// the variable.
        /// </summary>
        /// <param name="converter">The converter to use.</param>
        public ConverterAttribute(Type converter)
            : this(converter, true)
        {
        }

        protected ConverterAttribute(Type converter, bool createInstance)
        {
            Converter = converter;
            if (createInstance)
                try
                {
                    Instance = (Tools.IConverter)Activator.CreateInstance(converter);
                }
                catch
                {
                    Instance = null;
                }
        }
    }
}