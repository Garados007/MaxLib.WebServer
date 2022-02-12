using System;
using System.Collections.Generic;

namespace MaxLib.WebServer.Builder.Tools
{
    [System.AttributeUsage(System.AttributeTargets.Parameter, Inherited = true, AllowMultiple = false)]
    public abstract class ParamAttributeBase : Attribute
    {
        /// <summary>
        /// This is the type this <see cref="ParamAttributeBase" /> will return after execution.
        /// This should always be the same type because the <see cref="IConverter" /> will pick
        /// a suitable converter because of this. The conversion process that will change the value
        /// to its format that can used by the method body will happen after this step.
        /// </summary>
        public abstract Type Type { get; }

        public abstract Tools.Result<object?> GetValue(WebProgressTask task,
            string field, Dictionary<string, object?> vars
        );
    }
}