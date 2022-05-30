using System;
using System.Collections.Generic;
using System.Reflection;

namespace MaxLib.WebServer.Builder
{
    /// <summary>
    /// This is the base class that needs all Builder Services to be found by <see
    /// cref="Build(Type)"/>.
    /// </summary>
    /// <example>
    /// A Builder Service
    /// <code>
    /// using MaxLib.WebServer;
    /// using MaxLib.WebServer.Builder;
    /// using System.Threading.Tasks;
    /// 
    /// [Path("/foo")]
    /// public class MyService : Service
    /// {
    ///     [Path("/foo/{id}")]
    ///     public async Task&lt;HttpDataSource&gt; Foo([Var] int id)
    ///     {
    ///         var result = await Job(id);
    ///         return new HttpStringDataSource(result);
    ///     }
    /// }
    /// </code>
    /// </example>
    /// <example>
    /// How to import the builder service
    /// <code>
    /// var service = Service.Build&lt;MyService&gt;();
    /// webServer.AddWebService(service);
    /// </code>
    /// </example>
    public abstract class Service : IDisposable
    {
        /// <summary>
        /// The flags that specify the errors the generator will report to the log output.
        /// </summary>
        public static Tools.GeneratorLogFlag LogBuildWarnings
        {
            get => Tools.Generator.LogBuildWarnings;
            set => Tools.Generator.LogBuildWarnings = value;
        }

        /// <summary>
        /// Override this method if you want to include special dispose logic if you shutdown your
        /// server.
        /// </summary>
        public virtual void Dispose()
        {
        }

        /// <summary>
        /// Builds a <see cref="WebService"> from the specified <see cref="Service" /> type. <br/>
        /// This call will return null if <typeparamref name="T"/> has the <see
        /// cref="IgnoreAttribute" /> set, is abstract, is generic, is not public or has no
        /// parameterless constructor.
        /// </summary>
        /// <typeparam name="T">The type of the <see cref="Service" /> to build.</typeparam>
        /// <returns>the <see cref"WebService" /> if succeed; otherwise null</returns>
        public static WebService? Build<T>()
            where T : Service, new()
        {
            return Build(typeof(T));
        }

        /// <summary>
        /// Builds a <see cref="WebService"> from the specified <see cref="Service" /> type. <br/>
        /// This call will return null if <paramref name="type"/> is not a <see cref="Service" />,
        /// has the <see cref="IgnoreAttribute" /> set, is abstract, is generic, is not public or
        /// has no parameterless constructor.
        /// </summary>
        /// <param name="type">The <see cref="Service" /> that should be created</param>
        /// <returns>the <see cref"WebService" /> if succeed; otherwise null</returns>
        public static WebService? Build(Type type)
        {
            if (!typeof(Service).IsAssignableFrom(type))
                return null;
            return Tools.Generator.GenerateClass(type);
        }

        /// <summary>
        /// Search in <paramref name="assembly"/> for all suitable <see cref="Service" /> and create
        /// their services. This call will return null if no suitable <see cref="Service" /> was
        /// found.
        /// </summary>
        /// <param name="assembly">The assembly to search in.</param>
        /// <returns>
        /// the web service that contains all <see cref="WebService" /> for the <see cref="Service"
        /// /> or null if no suitable <see cref="Service" /> was found.
        /// </returns>
        public static WebService? Build(Assembly assembly)
        {
            var group = new Runtime.ServiceGroup(new List<Tools.RuleAttributeBase>());
            foreach (var type in assembly.GetExportedTypes())
            {
                var service = Build(type);
                if (service != null)
                    group.Add(service);
            }
            return group.Count == 0 ? null : group;
        }

        /// <summary>
        /// Search in <paramref name="appDomain"/> for all suitable <see cref="Service" /> and
        /// create their services. This call will return null if no suitable <see cref="Service" />
        /// was found.
        /// </summary>
        /// <param name="appDomain">
        /// The <see cref="AppDomain" /> where each loaded <see cref="Assembly" /> should be
        /// searched for suitable <see cref="Service" />.
        /// </param>
        /// <returns>
        /// the web service that contains all <see cref="WebService" /> for the <see cref="Service"
        /// /> or null if no suitable <see cref="Service" /> was found.
        /// </returns>
        public static WebService? Build(AppDomain appDomain)
        {
            var group = new Runtime.ServiceGroup(new List<Tools.RuleAttributeBase>());
            foreach (var assembly in appDomain.GetAssemblies())
            {
                var service = Build(assembly);
                if (service != null)
                    group.Add(service);
            }
            return group.Count == 0 ? null : group;
        }

        /// <summary>
        /// Search in the current <see cref="AppDomain" /> for all suitable <see cref="Service" />
        /// and create their services. This call will return null if no suitable <see cref="Service"
        /// /> was found.
        /// </summary>
        /// <returns>
        /// the web service that contains all <see cref="WebService" /> for the <see cref="Service"
        /// /> or null if no suitable <see cref="Service" /> was found.
        /// </returns>
        public static WebService? Build()
        {
            return Build(AppDomain.CurrentDomain);
        }
    }
}
