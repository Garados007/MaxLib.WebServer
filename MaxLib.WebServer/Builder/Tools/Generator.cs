using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace MaxLib.WebServer.Builder.Tools
{
    public static class Generator
    {
        private static readonly Converter.SystemConverter systemConverter
            = new Builder.Converter.SystemConverter();
        private static readonly Converter.DataConverter dataConverter
            = new Builder.Converter.DataConverter();

        public static Runtime.ServiceGroup? GenerateClass(Type type)
        {
            var ignore = type.GetCustomAttribute<IgnoreAttribute>();
            if (ignore != null || type.IsAbstract || type.IsGenericType)
                return null;
            
            var constructor = type.GetConstructor(Type.EmptyTypes);
            if (constructor == null)
                return null;

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
                return null;
            
            if (method.DeclaringType == typeof(object) || method.DeclaringType == typeof(Service))
                return null;
            
            var rules = method.GetCustomAttributes<Tools.RuleAttributeBase>().ToList();
            var parameter = new List<Runtime.IParameter>();
            foreach (var parInfo in method.GetParameters())
            {
                var par = GenerateParameter(parInfo);
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

        public static Runtime.IParameter? GenerateParameter(ParameterInfo parameter)
        {
            var convAttr = parameter.GetCustomAttribute<ConverterAttribute>(true);
            var paramAttr = parameter.GetCustomAttribute<ParamAttributeBase>(true);
            if (paramAttr != null)
            {
                Tools.IConverter converter;
                if (convAttr != null)
                {
                    if (convAttr.Instance == null)
                        return null;
                    converter = convAttr.Instance;
                }
                else converter = systemConverter;
                var convFunc = converter.GetConverter(paramAttr.Type, parameter.ParameterType);
                if (convFunc == null)
                    return null;
                return new Runtime.Parameter(parameter.Name, paramAttr, convFunc);
            }
            else
            {
                var param = Runtime.CoreParameter.GetCoreParameter(parameter.ParameterType);
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
                    return null;
                var constructed = convAttr.Converter.GetConstructor(Type.EmptyTypes)?
                    .Invoke(Array.Empty<object>());
                if (constructed == null)
                    return null;
                converter = (Tools.IDataConverter)constructed;
            }
            else converter = dataConverter;
            
            var resultMethod = GenerateResult(converter, method.ReturnType);
            var mime = method.ReturnParameter.GetCustomAttribute<MimeAttribute>();
            if (mime != null && resultMethod != null)
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