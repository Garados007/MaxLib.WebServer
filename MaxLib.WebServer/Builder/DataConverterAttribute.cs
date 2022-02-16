using System;

namespace MaxLib.WebServer.Builder
{
    /// <summary>
    /// Specify the <see cref="Tools.IDataConverter" /> which should be used to convert the type of
    /// the variable.
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.ReturnValue, Inherited = true, AllowMultiple = false)]
    public class DataConverterAttribute : System.Attribute
    {
        /// <summary>
        /// The converter to use.
        /// </summary>
        public Type Converter { get; }

        /// <summary>
        /// The instance of the current converter.
        /// </summary>
        public Tools.IDataConverter? Instance { get; protected set; }

        /// <summary>
        /// Specify the <see cref="Tools.IDataConverter" /> which should be used to convert the type of
        /// the variable.
        /// </summary>
        /// <param name="converter">The converter to use.</param>
        public DataConverterAttribute(Type converter)
            : this(converter, true)
        {
        }
        
        protected DataConverterAttribute(Type converter, bool createInstance)
        {
            Converter = converter;
            if (createInstance)
                try
                {
                    Instance = (Tools.IDataConverter)Activator.CreateInstance(converter);
                }
                catch
                {
                    Instance = null;
                }
        }
    }
}