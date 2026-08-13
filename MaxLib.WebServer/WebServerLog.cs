using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

#nullable enable

namespace MaxLib.WebServer
{
    public static class WebServerLog
    {
        internal static ILoggerFactory LoggerFactory { get; set; } = NullLoggerFactory.Instance;

        static bool factorySet;

        /// <summary>
        /// Sets the <see cref="ILoggerFactory"/> this library uses for all of its internal logging.
        /// Must be called before constructing anything else from this library - every class resolves
        /// its logger once, at first use. Can only be called once; a second call throws.
        /// </summary>
        public static void SetLoggerFactory(ILoggerFactory loggerFactory)
        {
            ArgumentNullException.ThrowIfNull(loggerFactory);
            if (factorySet)
                throw new InvalidOperationException(
                    "The logger factory has already been set and cannot be changed afterwards."
                );
            LoggerFactory = loggerFactory;
            factorySet = true;
        }
    }
}
