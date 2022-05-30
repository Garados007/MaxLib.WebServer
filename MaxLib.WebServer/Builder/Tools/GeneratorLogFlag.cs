namespace MaxLib.WebServer.Builder.Tools
{
    public enum GeneratorLogFlag : int
    {
        /// <summary>
        /// No warnings should be created and all errors should be ignored.
        /// </summary>
        None = 0x0000,
        /// <summary>
        /// The type is abstract and no instance can be created from it.
        /// </summary>
        TypeAbstract = 0x0001,
        /// <summary>
        /// The type is generic and the generator has no means to fill the generic variables
        /// </summary>
        TypeGeneric = 0x0002,
        /// <summary>
        /// No public parameterless constructor for the type found
        /// </summary>
        TypeNoConstructor = 0x0004,
        /// <summary>
        /// The method is abstract and has no implementation.
        /// </summary>
        MethodAbstract = 0x0008,
        /// <summary>
        /// The method is generic and the generator has no means to fill the generic variables
        /// </summary>
        MethodGeneric = 0x0010,
        /// <summary>
        /// The method is not public
        /// </summary>
        MethodNotPublic = 0x0020,
        /// <summary>
        /// The method was declared in <see cref="object" /> or in <see cref="Service" />.
        /// </summary>
        MethodDeclaredInObject = 0x0040,
        /// <summary>
        /// The method parameter has an converter attribute set which doesn't provide a converter
        /// instance. This is usually the cause of wrong implementation of the attribute itself.
        /// </summary>
        ParamMissingConverterInstance = 0x0080,
        /// <summary>
        /// The method parameter has an converter attribute set which converter has no way to
        /// convert the type of the parameter itself. Try to use another converter, fix the
        /// configuration of the attribute or the implementation of the converter.
        /// </summary>
        ParamNoConverterFound = 0x0100,
        /// <summary>
        /// The method parameter has no converter attribute set and the generator has no way to
        /// convert the type to suit the parameter. Try to use a converter attribute, a custom
        /// converter or a different parameter type.
        /// </summary>
        ParamNoCoreConverterFound = 0x0200,
        /// <summary>
        /// The converter type for the result converter is invalid. Try to implement <see
        /// cref="Tools.IDataConverter" /> in your type.
        /// </summary>
        ResultInvalidConverterType = 0x0400,
        /// <summary>
        /// The converter instance for the result converter cannot be created. This is the result of
        /// a missing type or no parameterless constructor was provided.
        /// </summary>
        ResultCannotCreateConverterInstance = 0x0800,
        /// <summary>
        /// The result converter (or the generator if no was provided) cannot convert the result
        /// type of the method to something the engine understands. Try to change your returning
        /// type or provide a custom converter that understands your type.
        /// </summary>
        ResultNoConverter = 0x1000,
        /// <summary>
        /// The default configuration generator logs. This will report anything that is more
        /// difficult to find and has a high probability to be miss configured.
        /// </summary>
        Default = ParamMissingConverterInstance | ParamNoConverterFound | ParamNoCoreConverterFound
            | ResultInvalidConverterType | ResultCannotCreateConverterInstance | ResultNoConverter,
        /// <summary>
        /// Reports everything
        /// </summary>
        All = 0x1fff,
    }
}