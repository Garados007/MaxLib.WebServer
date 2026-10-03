using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MaxLib.WebServer.Builder.Debugger;

namespace MaxLib.WebServer.Builder.Tools
{
    /// <remarks>
    /// Rule and parameter attributes must not throw from ToString(); build reports use it for labels.
    /// Converters and converter attributes must not throw from ToString(); build reports and logs use
    /// it. Exceptions thrown by Service constructors and custom converters must have a non-throwing
    /// ToString(), Message and StackTrace; they are passed to the logger as-is and read when building
    /// reports.
    /// </remarks>
    public static class Generator
    {
#region Logs

        static readonly ILogger logger = WebServerLog.LoggerFactory.CreateLogger(typeof(Generator));
        static readonly EventId GenerateClassEventId = new(0, "generate class");

        /// <summary>
        /// The flags that specify the errors the generator will report to the log output.
        /// </summary>
        public static GeneratorLogFlag LogBuildWarnings { get; set; } = GeneratorLogFlag.Default;

        /// <summary>
        /// Wraps <paramref name="value" /> in a marker a <see cref="BuildReportNode.Message" />
        /// consumer can recognize and style by <paramref name="kind" /> (e.g. the debugger's web
        /// UI renders <c>type</c>/<c>method</c>/<c>param</c>/<c>return</c>/<c>attr</c> identifiers
        /// with distinct styling). Plain text outside a marker is rendered as-is.
        /// </summary>
        private static string Mark(string kind, object? value) => $"{{{{{kind}|{value}}}}}";

        private static void LogTypeIgnored(Type type, TypeReportNode? report)
        {
            const GeneratorLogFlag code = GeneratorLogFlag.TypeIgnoredByAttribute;
            if ((LogBuildWarnings & code) == code)
                logger.LogInformation(GenerateClassEventId,
                    "[{Code:X4}] Type {Type} ignored because of the {Attribute} attribute",
                    (int)code,
                    type,
                    nameof(IgnoreAttribute)
                );
            if (report != null)
            {
                report.Status = BuildReportStatus.Ignored;
                report.Reason = BuildReasonCode.TypeIgnoredByAttribute;
                report.Message = $"Type {Mark("type", type)} ignored because of the {Mark("attr", nameof(IgnoreAttribute))} attribute";
            }
        }

        private static void LogTypeAbstract(Type type, bool set, TypeReportNode? report)
        {
            const GeneratorLogFlag code = GeneratorLogFlag.TypeAbstract;
            if (!set)
                return;
            if ((LogBuildWarnings & code) == code)
                logger.LogInformation(GenerateClassEventId,
                    "[{Code:X4}] Type {Type} ignored because it's abstract",
                    (int)code,
                    type
                );
            if (report != null)
            {
                report.Status = BuildReportStatus.Ignored;
                report.Reason = BuildReasonCode.TypeAbstract;
                report.Message = $"Type {Mark("type", type)} ignored because it's abstract";
            }
        }

        private static void LogTypeGeneric(Type type, bool set, TypeReportNode? report)
        {
            const GeneratorLogFlag code = GeneratorLogFlag.TypeGeneric;
            if (!set)
                return;
            if ((LogBuildWarnings & code) == code)
                logger.LogInformation(GenerateClassEventId,
                    "[{Code:X4}] Type {Type} ignored because it's generic",
                    (int)code,
                    type
                );
            if (report != null)
            {
                report.Status = BuildReportStatus.Ignored;
                report.Reason = BuildReasonCode.TypeGeneric;
                report.Message = $"Type {Mark("type", type)} ignored because it's generic";
            }
        }

        private static void LogTypeNoConstructor(Type type, TypeReportNode? report)
        {
            const GeneratorLogFlag code = GeneratorLogFlag.TypeNoConstructor;
            if ((LogBuildWarnings & code) == code)
                logger.LogInformation(GenerateClassEventId,
                    "[{Code:X4}] Type {Type} ignored because it has no parameterless constructor",
                    (int)code,
                    type
                );
            if (report != null)
            {
                report.Status = BuildReportStatus.Ignored;
                report.Reason = BuildReasonCode.TypeNoConstructor;
                report.Message = $"Type {Mark("type", type)} ignored because it has no parameterless constructor";
            }
        }

        /// <summary>
        /// Records that <paramref name="type" /> was rejected by <see cref="Service.Build(Type)"
        /// /> because it doesn't inherit from <see cref="Service" />. Reported only through
        /// <paramref name="report" />; there is no corresponding <see cref="GeneratorLogFlag" />,
        /// so this is never written to the log.
        /// </summary>
        internal static void LogTypeNotService(Type type, TypeReportNode? report)
        {
            if (report != null)
            {
                report.Status = BuildReportStatus.Failed;
                report.Reason = BuildReasonCode.TypeNotService;
                report.Message = $"Type {Mark("type", type)} does not inherit from {Mark("type", nameof(Service))}";
            }
        }

        private static void LogMethodIgnored(MethodInfo method, MethodReportNode? report)
        {
            const GeneratorLogFlag code = GeneratorLogFlag.MethodIgnoredByAttribute;
            if ((LogBuildWarnings & code) == code)
                logger.LogInformation(GenerateClassEventId,
                    "[{Code:X4}] Method {Method} ignored because of the {Attribute} attribute",
                    (int)code,
                    method,
                    nameof(IgnoreAttribute)
                );
            if (report != null)
            {
                report.Status = BuildReportStatus.Ignored;
                report.Reason = BuildReasonCode.MethodIgnoredByAttribute;
                report.Message = $"Method {Mark("method", method)} ignored because of the {Mark("attr", nameof(IgnoreAttribute))} attribute";
            }
        }

        private static void LogMethodAbstract(MethodInfo method, bool set, MethodReportNode? report)
        {
            const GeneratorLogFlag code = GeneratorLogFlag.MethodAbstract;
            if (!set)
                return;
            if ((LogBuildWarnings & code) == code)
                logger.LogInformation(GenerateClassEventId,
                    "[{Code:X4}] Method {Method} ignored because it's abstract",
                    (int)code,
                    method
                );
            if (report != null)
            {
                report.Status = BuildReportStatus.Ignored;
                report.Reason = BuildReasonCode.MethodAbstract;
                report.Message = $"Method {Mark("method", method)} ignored because it's abstract";
            }
        }

        private static void LogMethodGeneric(MethodInfo method, bool set, MethodReportNode? report)
        {
            const GeneratorLogFlag code = GeneratorLogFlag.MethodGeneric;
            if (!set)
                return;
            if ((LogBuildWarnings & code) == code)
                logger.LogInformation(GenerateClassEventId,
                    "[{Code:X4}] Method {Method} ignored because it's generic",
                    (int)code,
                    method
                );
            if (report != null)
            {
                report.Status = BuildReportStatus.Ignored;
                report.Reason = BuildReasonCode.MethodGeneric;
                report.Message = $"Method {Mark("method", method)} ignored because it's generic";
            }
        }

        private static void LogMethodNotPublic(MethodInfo method, bool set, MethodReportNode? report)
        {
            const GeneratorLogFlag code = GeneratorLogFlag.MethodNotPublic;
            if (!set)
                return;
            if ((LogBuildWarnings & code) == code)
                logger.LogInformation(GenerateClassEventId,
                    "[{Code:X4}] Method {Method} ignored because it's not public",
                    (int)code,
                    method
                );
            if (report != null)
            {
                report.Status = BuildReportStatus.Ignored;
                report.Reason = BuildReasonCode.MethodNotPublic;
                report.Message = $"Method {Mark("method", method)} ignored because it's not public";
            }
        }

        private static void LogMethodDeclaredInObject(MethodInfo method, MethodReportNode? report)
        {
            const GeneratorLogFlag code = GeneratorLogFlag.MethodDeclaredInObject;
            if ((LogBuildWarnings & code) == code)
                logger.LogInformation(GenerateClassEventId,
                    "[{Code:X4}] Method {Method} ignored because is was declared in {ObjectType} or {ServiceType}",
                    (int)code,
                    method,
                    typeof(object),
                    typeof(Service)
                );
            if (report != null)
            {
                report.Status = BuildReportStatus.Ignored;
                report.Reason = BuildReasonCode.MethodDeclaredInObject;
                report.Message = $"Method {Mark("method", method)} ignored because is was declared in {Mark("type", typeof(object))} or {Mark("type", typeof(Service))}";
            }
        }

        private static void LogMethodConstructorThrew(MethodInfo method, Exception exception, MethodReportNode? report)
        {
            const GeneratorLogFlag code = GeneratorLogFlag.MethodConstructorThrew;
            if ((LogBuildWarnings & code) == code)
                logger.LogInformation(GenerateClassEventId,
                    exception,
                    "[{Code:X4}] Method {Method} ignored because the constructor of {Type} threw an exception",
                    (int)code,
                    method,
                    method.ReflectedType
                );
            if (report != null)
            {
                report.Status = BuildReportStatus.Failed;
                report.Reason = BuildReasonCode.MethodConstructorThrew;
                report.Message = $"Method {Mark("method", method)} ignored because the constructor of {Mark("type", method.ReflectedType)} threw an exception: {exception.Message}";
                report.ExceptionMessage = exception.Message;
                report.ExceptionStackTrace = exception.StackTrace;
                report.Exception = exception;
            }
        }

        private static void LogParamMissingConvInstance(MethodInfo method, ParameterInfo param, ConverterAttribute attr, ParameterReportNode? report)
        {
            const GeneratorLogFlag code = GeneratorLogFlag.ParamMissingConverterInstance;
            if ((LogBuildWarnings & code) == code)
                logger.LogInformation(GenerateClassEventId,
                    "[{Code:X4}] Method {Method} ignored because parameter {Parameter} has no converter instance set for attribute {Attribute}",
                    (int)code,
                    method,
                    param,
                    attr
                );
            if (report != null)
            {
                report.Status = BuildReportStatus.Failed;
                report.Reason = BuildReasonCode.ParamMissingConverterInstance;
                report.Message = $"Parameter {Mark("param", param.Name)} has no converter instance set for attribute {Mark("attr", attr)}";
            }
        }

        private static void LogParamNoConverterFound(MethodInfo method, ParameterInfo param, Tools.IConverter converter, ParameterReportNode? report)
        {
            const GeneratorLogFlag code = GeneratorLogFlag.ParamNoConverterFound;
            if ((LogBuildWarnings & code) == code)
                logger.LogInformation(GenerateClassEventId,
                    "[{Code:X4}] Method {Method} ignored because converter {Converter} cannot convert the type of parameter {Parameter}",
                    (int)code,
                    method,
                    converter,
                    param
                );
            if (report != null)
            {
                report.Status = BuildReportStatus.Failed;
                report.Reason = BuildReasonCode.ParamNoConverterFound;
                report.Message = $"Converter {Mark("type", converter)} cannot convert the type of parameter {Mark("param", param.Name)}";
            }
        }

        private static void LogParamNoCoreConverterFound(MethodInfo method, ParameterInfo param, ParameterReportNode? report)
        {
            const GeneratorLogFlag code = GeneratorLogFlag.ParamNoCoreConverterFound;
            if ((LogBuildWarnings & code) == code)
                logger.LogInformation(GenerateClassEventId,
                    "[{Code:X4}] Method {Method} ignored because the core generator cannot convert the type of parameter {Parameter}",
                    (int)code,
                    method,
                    param
                );
            if (report != null)
            {
                report.Status = BuildReportStatus.Failed;
                report.Reason = BuildReasonCode.ParamNoCoreConverterFound;
                report.Message = $"The core generator cannot convert the type of parameter {Mark("param", param.Name)}";
            }
        }

        private static void LogResultInvalidConverterType(MethodInfo method, DataConverterAttribute attr, ResultReportNode? report)
        {
            const GeneratorLogFlag code = GeneratorLogFlag.ResultInvalidConverterType;
            if ((LogBuildWarnings & code) == code)
                logger.LogInformation(GenerateClassEventId,
                    "[{Code:X4}] Method {Method} ignored because the result data converter {Attribute} has an invalid type provided",
                    (int)code,
                    method,
                    attr
                );
            if (report != null)
            {
                report.Status = BuildReportStatus.Failed;
                report.Reason = BuildReasonCode.ResultInvalidConverterType;
                report.Message = $"The result data converter {Mark("attr", attr)} has an invalid type provided";
            }
        }

        private static void LogResultCannotCreateConverterInstance(MethodInfo method, DataConverterAttribute attr, ResultReportNode? report)
        {
            const GeneratorLogFlag code = GeneratorLogFlag.ResultCannotCreateConverterInstance;
            if ((LogBuildWarnings & code) == code)
                logger.LogInformation(GenerateClassEventId,
                    "[{Code:X4}] Method {Method} ignored because there cannot be created an instance for the result data converter {Converter}",
                    (int)code,
                    method,
                    attr.Converter
                );
            if (report != null)
            {
                report.Status = BuildReportStatus.Failed;
                report.Reason = BuildReasonCode.ResultCannotCreateConverterInstance;
                report.Message = $"There cannot be created an instance for the result data converter {Mark("type", attr.Converter)}";
            }
        }

        private static void LogResultNoConverter(MethodInfo method, ResultReportNode? report)
        {
            const GeneratorLogFlag code = GeneratorLogFlag.ResultNoConverter;
            if ((LogBuildWarnings & code) == code)
                logger.LogInformation(GenerateClassEventId,
                    "[{Code:X4}] Method {Method} ignored because for the result type is no suitable converter found or set",
                    (int)code,
                    method
                );
            if (report != null)
            {
                report.Status = BuildReportStatus.Failed;
                report.Reason = BuildReasonCode.ResultNoConverter;
                report.Message = $"For the result type {Mark("return", method.ReturnType)} is no suitable converter found or set";
            }
        }


#endregion Logs

        private static readonly Converter.SystemConverter systemConverter
            = new Builder.Converter.SystemConverter();
        private static readonly Converter.DataConverter dataConverter
            = new Builder.Converter.DataConverter();

        public static Runtime.ServiceGroup? GenerateClass(Type type)
        {
            ArgumentNullException.ThrowIfNull(type);
            return GenerateClassCore(type, null);
        }

        /// <summary>
        /// Builds a <see cref="Runtime.ServiceGroup" /> from <paramref name="type" /> just like
        /// <see cref="GenerateClass(Type)" />, but additionally returns a structured
        /// <paramref name="report" /> of the type, its methods, their parameters and results,
        /// including why anything that wasn't built got skipped or failed.
        /// </summary>
        public static Runtime.ServiceGroup? GenerateClass(Type type, out TypeReportNode report)
        {
            ArgumentNullException.ThrowIfNull(type);
            var node = new TypeReportNode
            {
                Name = type.Name,
                TypeFullName = type.FullName ?? type.Name,
            };
            var result = GenerateClassCore(type, node);
            report = node;
            return result;
        }

        /// <summary>
        /// The specificity of <paramref name="method" />'s own <see cref="PathAttribute" />
        /// rule, if it has one, else 0 - used to order overlapping routes that share the same
        /// <see cref="WebServicePriority" />. See <see cref="PathAttribute.Specificity" />.
        /// </summary>
        private static int MethodSpecificity(Runtime.MethodService method)
            => method.Rules.OfType<PathAttribute>().FirstOrDefault()?.Specificity ?? 0;

        private static Runtime.ServiceGroup? GenerateClassCore(Type type, TypeReportNode? report)
        {
            var ignore = type.GetCustomAttribute<IgnoreAttribute>();
            if (ignore != null)
            {
                LogTypeIgnored(type, report);
                return null;
            }
            if (type.IsAbstract || type.IsGenericType)
            {
                LogTypeAbstract(type, type.IsAbstract, report);
                LogTypeGeneric(type, type.IsGenericType, report);
                return null;
            }

            var constructor = type.GetConstructor(Type.EmptyTypes);
            if (constructor == null)
            {
                LogTypeNoConstructor(type, report);
                return null;
            }

            var rules = type.GetCustomAttributes<Tools.RuleAttributeBase>().ToList();
            var group = new Runtime.ServiceGroup(rules)
            {
                SourceType = type,
            };
            if (report != null)
                report.Rules.AddRange(rules.Select(r => r.ToString() ?? r.GetType().Name));

            var generatedMethods = new List<Runtime.MethodService>();
            foreach (var methodInfo in type.GetMethods())
            {
                MethodReportNode? methodReport = report != null
                    ? new MethodReportNode
                    {
                        Name = methodInfo.Name,
                        DeclaringType = methodInfo.DeclaringType?.FullName ?? "",
                    }
                    : null;
                var method = GenerateMethodCore(methodInfo, methodReport);
                if (method != null)
                    generatedMethods.Add(method);
                if (methodReport != null)
                    report!.Methods.Add(methodReport);
            }
            // Type.GetMethods() order is documented as unspecified. Adding the more specific
            // route first, for methods that end up sharing the same WebServicePriority, makes
            // its PriorityList place it ahead - see PathAttribute.Specificity.
            foreach (var method in generatedMethods.OrderByDescending(MethodSpecificity))
                group.Add(method);

            foreach (var nested in type.GetNestedTypes())
            {
                TypeReportNode? nestedReport = report != null
                    ? new TypeReportNode
                    {
                        Name = nested.Name,
                        TypeFullName = nested.FullName ?? nested.Name,
                    }
                    : null;
                var service = GenerateClassCore(nested, nestedReport);
                if (service != null)
                    group.Add(service);
                if (nestedReport != null)
                    report!.NestedTypes.Add(nestedReport);
            }

            if (report != null)
            {
                report.Status = BuildReportStatus.Accepted;
                report.Reason = BuildReasonCode.Accepted;
                report.Message = $"Type {Mark("type", type)} built successfully with {group.Count} contained service(s)";
            }
            return group;
        }

        public static Runtime.MethodService? GenerateMethod(MethodInfo method)
        {
            ArgumentNullException.ThrowIfNull(method);
            return GenerateMethodCore(method, null);
        }

        /// <summary>
        /// Builds a <see cref="Runtime.MethodService" /> from <paramref name="method" /> just like
        /// <see cref="GenerateMethod(MethodInfo)" />, but additionally returns a structured
        /// <paramref name="report" /> of the method, its parameters and its result, including why
        /// it might have been skipped or failed.
        /// </summary>
        public static Runtime.MethodService? GenerateMethod(MethodInfo method, out MethodReportNode report)
        {
            ArgumentNullException.ThrowIfNull(method);
            var node = new MethodReportNode
            {
                Name = method.Name,
                DeclaringType = method.DeclaringType?.FullName ?? "",
            };
            var result = GenerateMethodCore(method, node);
            report = node;
            return result;
        }

        private static Runtime.MethodService? GenerateMethodCore(MethodInfo method, MethodReportNode? report)
        {
            var priorityAttr = method.GetCustomAttribute<PriorityAttribute>();
            var priority = priorityAttr?.Priority ?? WebServicePriority.Normal;
            if (report != null && priorityAttr != null)
                report.Priority = priority.ToString();

            var ignore = method.GetCustomAttribute<IgnoreAttribute>();
            if (ignore != null)
            {
                LogMethodIgnored(method, report);
                return null;
            }
            if (method.IsAbstract || method.IsGenericMethod || !method.IsPublic)
            {
                LogMethodAbstract(method, method.IsAbstract, report);
                LogMethodGeneric(method, method.IsGenericMethod, report);
                LogMethodNotPublic(method, !method.IsPublic, report);
                return null;
            }

            if (method.DeclaringType == typeof(object) || method.DeclaringType == typeof(Service))
            {
                LogMethodDeclaredInObject(method, report);
                return null;
            }

            var rules = method.GetCustomAttributes<Tools.RuleAttributeBase>().ToList();
            if (report != null)
                report.Rules.AddRange(rules.Select(r => r.ToString() ?? r.GetType().Name));

            var parameter = new List<Runtime.IParameter>();
            foreach (var parInfo in method.GetParameters())
            {
                ParameterReportNode? paramReport = report != null
                    ? new ParameterReportNode
                    {
                        Name = parInfo.Name ?? "",
                        ParameterType = parInfo.ParameterType.FullName ?? parInfo.ParameterType.Name,
                    }
                    : null;
                var par = GenerateParameterCore(method, parInfo, paramReport);
                if (paramReport != null)
                    report!.Parameters.Add(paramReport);
                if (par == null)
                {
                    if (report != null)
                    {
                        report.Status = BuildReportStatus.Failed;
                        report.Reason = paramReport!.Reason;
                        report.Message = $"Method {Mark("method", method)} ignored because parameter {Mark("param", parInfo.Name)} could not be generated: {paramReport.Message}";
                    }
                    return null;
                }
                parameter.Add(par);
            }
            ResultReportNode? resultReport = report != null
                ? new ResultReportNode
                {
                    Name = "return",
                    ReturnType = method.ReturnType.FullName ?? method.ReturnType.Name,
                }
                : null;
            var result = GenerateResultCore(method, resultReport);
            if (resultReport != null)
                report!.Result = resultReport;
            if (result == null)
            {
                if (report != null)
                {
                    report.Status = BuildReportStatus.Failed;
                    report.Reason = resultReport!.Reason;
                    report.Message = $"Method {Mark("method", method)} ignored because the result could not be generated: {resultReport.Message}";
                }
                return null;
            }

            try
            {
                var service = new Runtime.MethodService(rules, parameter, method, result, priority);
                if (report != null)
                {
                    report.Status = BuildReportStatus.Accepted;
                    report.Reason = BuildReasonCode.Accepted;
                    report.Message = $"Method {Mark("method", method)} built successfully";
                }
                return service;
            }
            catch (Exception e)
            {
                var inner = e is TargetInvocationException tie && tie.InnerException != null
                    ? tie.InnerException
                    : e;
                LogMethodConstructorThrew(method, inner, report);
                return null;
            }
        }

        public static Runtime.IParameter? GenerateParameter(MethodInfo method, ParameterInfo parameter)
        {
            ArgumentNullException.ThrowIfNull(parameter);
            return GenerateParameterCore(method, parameter, null);
        }

        /// <summary>
        /// Builds a <see cref="Runtime.IParameter" /> from <paramref name="parameter" /> just like
        /// <see cref="GenerateParameter(MethodInfo, ParameterInfo)" />, but additionally returns a
        /// structured <paramref name="report" /> describing why it might have failed.
        /// </summary>
        public static Runtime.IParameter? GenerateParameter(MethodInfo method, ParameterInfo parameter, out ParameterReportNode report)
        {
            ArgumentNullException.ThrowIfNull(parameter);
            var node = new ParameterReportNode
            {
                Name = parameter.Name ?? "",
                ParameterType = parameter.ParameterType.FullName ?? parameter.ParameterType.Name,
            };
            var result = GenerateParameterCore(method, parameter, node);
            report = node;
            return result;
        }

        /// <summary>
        /// Describes how a parameter's value is resolved via <paramref name="paramAttr" />: the
        /// attribute plus the key it actually reads - which can differ from <paramref
        /// name="parameter" />'s own name via e.g. <c>[Var("foo")]</c>. Delegates to the
        /// attribute's own <see cref="object.ToString" /> when it has an explicit name to report
        /// (read generically via reflection on a conventional <c>string? Name</c> property, since
        /// attributes like <see cref="VarAttribute" />/<see cref="GetAttribute" />/<see
        /// cref="UrlEncodedPostAttribute" /> all expose it the same way but don't share an
        /// interface for it); otherwise falls back to the parameter's own name, since that's
        /// contextual information the attribute instance alone doesn't have.
        /// </summary>
        private static string DescribeParamSource(ParamAttributeBase paramAttr, ParameterInfo parameter)
        {
            var nameProperty = paramAttr.GetType().GetProperty("Name", typeof(string));
            if (nameProperty?.GetValue(paramAttr) is string { Length: > 0 })
                return paramAttr.ToString() ?? paramAttr.GetType().Name;
            var typeName = paramAttr.GetType().Name;
            if (typeName.EndsWith("Attribute", StringComparison.Ordinal))
                typeName = typeName[..^9];
            return $"{typeName}: {parameter.Name}";
        }

        private static Runtime.IParameter? GenerateParameterCore(MethodInfo method, ParameterInfo parameter, ParameterReportNode? report)
        {
            var convAttr = parameter.GetCustomAttribute<ConverterAttribute>(true);
            var paramAttr = parameter.GetCustomAttribute<ParamAttributeBase>(true);
            if (report != null && paramAttr != null)
                report.Source = DescribeParamSource(paramAttr, parameter);
            if (paramAttr != null)
            {
                Tools.IConverter converter;
                if (convAttr != null)
                {
                    if (convAttr.Instance == null)
                    {
                        LogParamMissingConvInstance(method, parameter, convAttr, report);
                        return null;
                    }
                    converter = convAttr.Instance;
                }
                else converter = systemConverter;
                var convFunc = converter.GetConverter(paramAttr.Type, parameter.ParameterType);
                if (convFunc == null)
                {
                    LogParamNoConverterFound(method, parameter, converter, report);
                    return null;
                }
                if (report != null)
                    report.Message = $"Parameter {Mark("param", parameter.Name)} resolved via {Mark("attr", paramAttr.GetType().Name)}";
                return new Runtime.Parameter(parameter.Name ?? "", paramAttr, convFunc, parameter.ParameterType);
            }
            else
            {
                var param = Runtime.CoreParameter.GetCoreParameter(parameter.ParameterType);
                if (param is null)
                {
                    LogParamNoCoreConverterFound(method, parameter, report);
                    return null;
                }
                if (report != null)
                {
                    report.Source = "Core parameter";
                    report.Message = $"Parameter {Mark("param", parameter.Name)} resolved as core parameter";
                }
                return param;
            }
        }

        public static Func<WebProgressTask, object?, Task>? GenerateResult(MethodInfo method)
        {
            ArgumentNullException.ThrowIfNull(method);
            return GenerateResultCore(method, null);
        }

        /// <summary>
        /// Builds a result converter from <paramref name="method" />'s return type just like
        /// <see cref="GenerateResult(MethodInfo)" />, but additionally returns a structured
        /// <paramref name="report" /> describing why it might have failed.
        /// </summary>
        public static Func<WebProgressTask, object?, Task>? GenerateResult(MethodInfo method, out ResultReportNode report)
        {
            ArgumentNullException.ThrowIfNull(method);
            var node = new ResultReportNode
            {
                Name = "return",
                ReturnType = method.ReturnType.FullName ?? method.ReturnType.Name,
            };
            var result = GenerateResultCore(method, node);
            report = node;
            return result;
        }

        private static Func<WebProgressTask, object?, Task>? GenerateResultCore(MethodInfo method, ResultReportNode? report)
        {
            var convAttr = method.ReturnParameter.GetCustomAttribute<DataConverterAttribute>();
            IDataConverter converter;
            if (convAttr?.Instance != null)
                converter = convAttr.Instance;
            else if (convAttr != null)
            {
                if (!typeof(Tools.IDataConverter).IsAssignableFrom(convAttr.Converter))
                {
                    if (report != null)
                        report.Converter = convAttr.Converter.Name;
                    LogResultInvalidConverterType(method, convAttr, report);
                    return null;
                }
                var constructed = convAttr.Converter.GetConstructor(Type.EmptyTypes)?
                    .Invoke([]);
                if (constructed == null)
                {
                    if (report != null)
                        report.Converter = convAttr.Converter.Name;
                    LogResultCannotCreateConverterInstance(method, convAttr, report);
                    return null;
                }
                converter = (Tools.IDataConverter)constructed;
            }
            else converter = dataConverter;

            var resultMethod = GenerateResult(converter, method.ReturnType);
            if (resultMethod == null)
            {
                LogResultNoConverter(method, report);
                return null;
            }
            if (report != null)
            {
                report.Converter = converter.GetType().Name;
                report.Message = $"Result type {Mark("return", method.ReturnType)} resolved successfully";
            }
            var mime = method.ReturnParameter.GetCustomAttribute<MimeAttribute>();
            if (mime != null)
            {
                return async (t, v) =>
                {
                    await resultMethod(t, v).ConfigureAwait(false);
                    t.Document.PrimaryMime = mime.Mime;
                };
            }
            return resultMethod;
        }

        public static Func<WebProgressTask, object?, Task>? GenerateResult(IDataConverter converter, Type type)
        {
            ArgumentNullException.ThrowIfNull(type);
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
                var getResult = type.GetProperty("Result")!;
                return async (task, value) =>
                {
                    if (value is null)
                        return;
                    await ((Task)value).ConfigureAwait(false);
                    var result = getResult.GetValue(value);
                    applier(task, result);
                };
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ValueTask<>))
            {
                var applier = ApplyResult(converter, type.GetGenericArguments()[0]);
                if (applier is null)
                    return null;
                var getResult = type.GetProperty("Result")!;
                return async (task, value) =>
                {
                    if (value is null)
                        return;
                    await ((ValueTask)value).ConfigureAwait(false);
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
            ArgumentNullException.ThrowIfNull(converter);
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
