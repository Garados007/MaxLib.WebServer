namespace MaxLib.WebServer.Builder.Debugger
{
    /// <summary>
    /// The specific reason why the <see cref="Tools.Generator" /> assigned a particular
    /// <see cref="BuildReportStatus" /> to a scanned type, method, parameter or result.
    /// </summary>
    public enum BuildReasonCode
    {
        /// <summary>
        /// The node was built successfully.
        /// </summary>
        Accepted = 0,
        /// <summary>
        /// The type has the <see cref="IgnoreAttribute" /> set.
        /// </summary>
        TypeIgnoredByAttribute,
        /// <summary>
        /// The type is abstract and no instance can be created from it.
        /// </summary>
        TypeAbstract,
        /// <summary>
        /// The type is generic and the generator has no means to fill the generic variables.
        /// </summary>
        TypeGeneric,
        /// <summary>
        /// No public parameterless constructor for the type was found.
        /// </summary>
        TypeNoConstructor,
        /// <summary>
        /// The type does not inherit from <see cref="Service" />.
        /// </summary>
        TypeNotService,
        /// <summary>
        /// The method has the <see cref="IgnoreAttribute" /> set.
        /// </summary>
        MethodIgnoredByAttribute,
        /// <summary>
        /// The method is abstract and has no implementation.
        /// </summary>
        MethodAbstract,
        /// <summary>
        /// The method is generic and the generator has no means to fill the generic variables.
        /// </summary>
        MethodGeneric,
        /// <summary>
        /// The method is not public.
        /// </summary>
        MethodNotPublic,
        /// <summary>
        /// The method was declared in <see cref="object" /> or in <see cref="Service" />.
        /// </summary>
        MethodDeclaredInObject,
        /// <summary>
        /// The constructor of the declaring type threw an exception when the generator tried to
        /// create an instance to bind the method to.
        /// </summary>
        MethodConstructorThrew,
        /// <summary>
        /// A parameter has a converter attribute set which doesn't provide a converter instance.
        /// </summary>
        ParamMissingConverterInstance,
        /// <summary>
        /// A parameter has a converter attribute set whose converter has no way to convert the
        /// type of the parameter itself.
        /// </summary>
        ParamNoConverterFound,
        /// <summary>
        /// A parameter has no converter attribute set and the generator has no way to convert the
        /// type to suit the parameter.
        /// </summary>
        ParamNoCoreConverterFound,
        /// <summary>
        /// The converter type for the result converter is invalid.
        /// </summary>
        ResultInvalidConverterType,
        /// <summary>
        /// The converter instance for the result converter cannot be created.
        /// </summary>
        ResultCannotCreateConverterInstance,
        /// <summary>
        /// No suitable converter was found or set for the result type of the method.
        /// </summary>
        ResultNoConverter,
    }
}
