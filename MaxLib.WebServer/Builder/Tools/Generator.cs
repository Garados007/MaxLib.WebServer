using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace MaxLib.WebServer.Builder.Tools
{
    public static class Generator
    {
#region Logs

        /// <summary>
        /// The flags that specify the errors the generator will report to the log output.
        /// </summary>
        public static GeneratorLogFlag LogBuildWarnings { get; set; } = GeneratorLogFlag.Default;

        private static void LogTypeAbstract(Type type, bool set)
        {
            const GeneratorLogFlag code = GeneratorLogFlag.TypeAbstract;
            if ((LogBuildWarnings & code) == code && set)
                WebServerLog.Add(
                    ServerLogType.Information,
                    typeof(Generator),
                    "generate class",
                    "[{0:X4}] Type {1} ignored because it's abstract",
                    (int)code,
                    type.FullName
                );
        }

        private static void LogTypeGeneric(Type type, bool set)
        {
            const GeneratorLogFlag code = GeneratorLogFlag.TypeGeneric;
            if ((LogBuildWarnings & code) == code && set)
                WebServerLog.Add(
                    ServerLogType.Information,
                    typeof(Generator),
                    "generate class",
                    "[{0:X4}] Type {1} ignored because it's generic",
                    (int)code,
                    type.FullName
                );
        }

        private static void LogTypeNoConstructor(Type type)
        {
            const GeneratorLogFlag code = GeneratorLogFlag.TypeNoConstructor;
            if ((LogBuildWarnings & code) == code)
                WebServerLog.Add(
                    ServerLogType.Information,
                    typeof(Generator),
                    "generate class",
                    "[{0:X4}] Type {1} ignored because it has no parameterless constructor",
                    (int)code,
                    type
                );
        }

        private static void LogMethodAbstract(MethodInfo method, bool set)
        {
            const GeneratorLogFlag code = GeneratorLogFlag.MethodAbstract;
            if ((LogBuildWarnings & code) == code && set)
                WebServerLog.Add(
                    ServerLogType.Information,
                    typeof(Generator),
                    "generate class",
                    "[{0:X4}] Method {1} ignored because it's abstract",
                    (int)code,
                    method
                );
        }

        private static void LogMethodGeneric(MethodInfo method, bool set)
        {
            const GeneratorLogFlag code = GeneratorLogFlag.MethodGeneric;
            if ((LogBuildWarnings & code) == code && set)
                WebServerLog.Add(
                    ServerLogType.Information,
                    typeof(Generator),
                    "generate class",
                    "[{0:X4}] Method {1} ignored because it's generic",
                    (int)code,
                    method
                );
        }

        private static void LogMethodNotPublic(MethodInfo method, bool set)
        {
            const GeneratorLogFlag code = GeneratorLogFlag.MethodNotPublic;
            if ((LogBuildWarnings & code) == code && set)
                WebServerLog.Add(
                    ServerLogType.Information,
                    typeof(Generator),
                    "generate class",
                    "[{0:X4}] Method {1} ignored because it's not public",
                    method
                );
        }

        private static void LogMethodDeclaredInObject(MethodInfo method)
        {
            const GeneratorLogFlag code = GeneratorLogFlag.MethodDeclaredInObject;
            if ((LogBuildWarnings & code) == code)
                WebServerLog.Add(
                    ServerLogType.Information,
                    typeof(Generator),
                    "generate class",
                    "[{0:X4}] Method {1} ignored because is was declared in {2} or {3}",
                    (int)code,
                    method,
                    typeof(object).FullName,
                    typeof(Service).FullName
                );
        }

        private static void LogParamMissingConvInstance(MethodInfo method, ParameterInfo param, ConverterAttribute attr)
        {
            const GeneratorLogFlag code = GeneratorLogFlag.ParamMissingConverterInstance;
            if ((LogBuildWarnings & code) == code)
                WebServerLog.Add(
                    ServerLogType.Information,
                    typeof(Generator),
                    "generate class",
                    "[{0:X4}] Method {1} ignored because parameter {2} has no converter instance set for attribute {3}",
                    (int)code,
                    method,
                    param,
                    attr
                );
        }

        private static void LogParamNoConverterFound(MethodInfo method, ParameterInfo param, Tools.IConverter converter)
        {
            const GeneratorLogFlag code = GeneratorLogFlag.ParamNoConverterFound;
            if ((LogBuildWarnings & code) == code)
                WebServerLog.Add(
                    ServerLogType.Information,
                    typeof(Generator),
                    "generate class",
                    "[{0:X4}] Method {1} ignored because converter {2} cannot convert the type of parameter {3}",
                    (int)code,
                    method,
                    converter,
                    param
                );
        }

        private static void LogParamNoCoreConverterFound(MethodInfo method, ParameterInfo param)
        {
            const GeneratorLogFlag code = GeneratorLogFlag.ParamNoCoreConverterFound;
            if ((LogBuildWarnings & code) == code)
                WebServerLog.Add(
                    ServerLogType.Information,
                    typeof(Generator),
                    "generate class",
                    "[{0:X4}] Method {1} ignored because the core generator cannot convert the type of parameter {2}",
                    (int)code,
                    method,
                    param
                );
        }

        private static void LogResultInvalidConverterType(MethodInfo method, DataConverterAttribute attr)
        {
            const GeneratorLogFlag code = GeneratorLogFlag.ResultInvalidConverterType;
            if ((LogBuildWarnings & code) == code)
                WebServerLog.Add(
                    ServerLogType.Information,
                    typeof(Generator),
                    "generate class",
                    "[{0:X4}] Method {1} ignored because the result data converter {2} has an invalid type provided",
                    (int)code,
                    method,
                    attr
                );
        }

        private static void LogResultCannotCreateConverterInstance(MethodInfo method, DataConverterAttribute attr)
        {
            const GeneratorLogFlag code = GeneratorLogFlag.ResultCannotCreateConverterInstance;
            if ((LogBuildWarnings & code) == code)
                WebServerLog.Add(
                    ServerLogType.Information,
                    typeof(Generator),
                    "generate class",
                    "[{0:X4}] Method {1} ignored because there cannot be created an instance for the result data converter {2}",
                    (int)code,
                    method,
                    attr.Converter.FullName
                );
        }

        private static void LogResultNoConverter(MethodInfo method)
        {
            const GeneratorLogFlag code = GeneratorLogFlag.ResultNoConverter;
            if ((LogBuildWarnings & code) == code)
                WebServerLog.Add(
                    ServerLogType.Information,
                    typeof(Generator),
                    "generate class",
                    "[{0:X4}] Method {1} ignored because for the result type is no suitable converter found or set",
                    (int)code,
                    method
                );
        }


#endregion Logs

        private static readonly Converter.SystemConverter systemConverter
            = new Builder.Converter.SystemConverter();
        private static readonly Converter.DataConverter dataConverter
            = new Builder.Converter.DataConverter();

        public static Runtime.ServiceGroup? GenerateClass(Type type)
        {
            var ignore = type.GetCustomAttribute<IgnoreAttribute>();
            if (ignore != null || type.IsAbstract || type.IsGenericType)
            {
                LogTypeAbstract(type, type.IsAbstract);
                LogTypeGeneric(type, type.IsGenericType);
                return null;
            }
            
            var constructor = type.GetConstructor(Type.EmptyTypes);
            if (constructor == null)
            {
                LogTypeNoConstructor(type);
                return null;
            }

            var rules = type.GetCustomAttributes<Tools.RuleAttributeBase>().ToList();
            var group = new Runtime.ServiceGroup(rules);

            foreach (var methodInfo in type.GetMethods())
            {
                var method = GenerateMethod(methodInfo);
                if (method != null)
                    group.Add(method);
            }

            foreach (var nested in type.GetNestedTypes())
            {
                var service = GenerateClass(nested);
                if (service != null)
                    group.Add(service);
            }

            return group;
        }

        public static Runtime.MethodService? GenerateMethod(MethodInfo method)
        {
            var ignore = method.GetCustomAttribute<IgnoreAttribute>();
            if (ignore != null || method.IsAbstract || method.IsGenericMethod || !method.IsPublic)
            {
                LogMethodAbstract(method, method.IsAbstract);
                LogMethodGeneric(method, method.IsGenericMethod);
                LogMethodNotPublic(method, !method.IsPublic);
                return null;
            }
            
            if (method.DeclaringType == typeof(object) || method.DeclaringType == typeof(Service))
            {
                LogMethodDeclaredInObject(method);
                return null;
            }
            
            var rules = method.GetCustomAttributes<Tools.RuleAttributeBase>().ToList();
            var parameter = new List<Runtime.IParameter>();
            foreach (var parInfo in method.GetParameters())
            {
                var par = GenerateParameter(method, parInfo);
                if (par == null)
                    return null;
                parameter.Add(par);
            }
            var result = GenerateResult(method);
            if (result == null)
                return null;
            
            var priority = method.GetCustomAttribute<PriorityAttribute>();
            return new Runtime.MethodService(rules, parameter, method, result,
                priority != null ? priority.Priority : WebServicePriority.Normal
            );
        }

        public static Runtime.IParameter? GenerateParameter(MethodInfo method, ParameterInfo parameter)
        {
            var convAttr = parameter.GetCustomAttribute<ConverterAttribute>(true);
            var paramAttr = parameter.GetCustomAttribute<ParamAttributeBase>(true);
            if (paramAttr != null)
            {
                Tools.IConverter converter;
                if (convAttr != null)
                {
                    if (convAttr.Instance == null)
                    {
                        LogParamMissingConvInstance(method, parameter, convAttr);
                        return null;
                    }
                    converter = convAttr.Instance;
                }
                else converter = systemConverter;
                var convFunc = converter.GetConverter(paramAttr.Type, parameter.ParameterType);
                if (convFunc == null)
                {
                    LogParamNoConverterFound(method, parameter, converter);
                    return null;
                }
                return new Runtime.Parameter(parameter.Name, paramAttr, convFunc);
            }
            else
            {
                var param = Runtime.CoreParameter.GetCoreParameter(parameter.ParameterType);
                if (param is null)
                    LogParamNoCoreConverterFound(method, parameter);
                return param;
            }
        }

        public static Func<WebProgressTask, object?, Task>? GenerateResult(MethodInfo method)
        {
            var convAttr = method.ReturnParameter.GetCustomAttribute<DataConverterAttribute>();
            IDataConverter converter;
            if (convAttr?.Instance != null)
                converter = convAttr.Instance;
            else if (convAttr != null)
            {
                if (!typeof(Tools.IDataConverter).IsAssignableFrom(convAttr.Converter))
                {
                    LogResultInvalidConverterType(method, convAttr);
                    return null;
                }
                var constructed = convAttr.Converter.GetConstructor(Type.EmptyTypes)?
                    .Invoke(Array.Empty<object>());
                if (constructed == null)
                {
                    LogResultCannotCreateConverterInstance(method, convAttr);
                    return null;
                }
                converter = (Tools.IDataConverter)constructed;
            }
            else converter = dataConverter;
            
            var resultMethod = GenerateResult(converter, method.ReturnType);
            if (resultMethod == null)
            {
                LogResultNoConverter(method);
                return null;
            }
            var mime = method.ReturnParameter.GetCustomAttribute<MimeAttribute>();
            if (mime != null)
            {
                return async (t, v) => 
                {
                    await resultMethod(t, v);
                    t.Document.PrimaryMime = mime.Mime;
                };
            }
            return resultMethod;
        }

        public static Func<WebProgressTask, object?, Task>? GenerateResult(IDataConverter converter, Type type)
        {
            if (type == typeof(void))
                return (_, __) => Task.CompletedTask;
            if (type == typeof(Task))
                return (_, task) => task as Task ?? Task.CompletedTask;
            if (type == typeof(ValueTask))
                return (_, task) => task is ValueTask t ? t.AsTask() : Task.CompletedTask;

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
            {
                var applier = ApplyResult(converter, type.GetGenericArguments()[0]);
                if (applier is null)
                    return null;
                var getResult = type.GetProperty("Result");
                return async (task, value) =>
                {
                    if (value is null)
                        return;
                    await (Task)value;
                    var result = getResult.GetValue(value);
                    applier(task, result);
                };
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ValueTask<>))
            {
                var applier = ApplyResult(converter, type.GetGenericArguments()[0]);
                if (applier is null)
                    return null;
                var getResult = type.GetProperty("Result");
                return async (task, value) =>
                {
                    if (value is null)
                        return;
                    await (ValueTask)value;
                    var result = getResult.GetValue(value);
                    applier(task, result);
                };
            }

            {
                var applier = ApplyResult(converter, type);
                if (applier is null)
                    return null;
                return (task, value) =>
                {
                    applier(task, value);
                    return Task.CompletedTask;
                };
            }
        }

        public static Action<WebProgressTask, object?>? ApplyResult(IDataConverter converter, Type type)
        {
            var conv = converter.GetConverter(type);
            if (conv is null)
                return null;
            return (task, value) => 
            {
                if (value != null)
                {
                    var source = conv(value);
                    if (source != null)
                        task.Document.DataSources.Add(source);
                    else
                    {
                        task.Document.DataSources.Add(new HttpStringDataSource("Cannot create data"));
                        task.Response.StatusCode = HttpStateCode.InternalServerError;
                    }
                }
            };
        }
    }
}