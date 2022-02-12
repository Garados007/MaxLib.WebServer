using System;

namespace MaxLib.WebServer.Builder.Tools
{
    /// <summary>
    /// A converter that converts the data after it was received from <see cref="ParamAttributeBase"
    /// /> in the form that can be used by the method. The selection is static. It will look on the
    /// types only one time and will always convert using the same function.
    /// </summary>
    public interface IConverter
    {
        /// <summary>
        /// The static function that can convert the value so it can be used by the web method. This
        /// method will only be called once during loading process and the returned method will be
        /// used all the time.<br/>
        /// This method returns <c>null</c> if this <see cref="IConverter" /> has no conversion
        /// method for the two types.
        /// </summary>
        /// <param name="source">The source type that will be received.</param>
        /// <param name="target">The type that is expected from the calling method.</param>
        /// <returns>the method that can be used for the conversion.</returns>
        Func<object?, object?>? GetConverter(Type source, Type target);
    }
}