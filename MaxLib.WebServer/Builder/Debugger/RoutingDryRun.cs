using System;
using System.Collections.Generic;
using System.IO;

namespace MaxLib.WebServer.Builder.Debugger
{
    /// <summary>
    /// Answers "which of my registered services would accept this request, and why did the
    /// others reject it (or never see it)?" without ever invoking a service's
    /// <see cref="WebService.ProgressTask(WebProgressTask)" />/side effects - only the read-only
    /// <c>CanWorkWith</c> path is exercised, against a synthetic request.
    /// </summary>
    public static class RoutingDryRun
    {
        /// <summary>
        /// Simulates <paramref name="task" /> against every service registered in
        /// <paramref name="group" />, in priority order.
        /// </summary>
        public static RoutingReport Run(WebServiceGroup group, WebProgressTask task)
        {
            ArgumentNullException.ThrowIfNull(group);
            ArgumentNullException.ThrowIfNull(task);

            var report = new RoutingReport
            {
                RequestSummary = $"{task.Request.ProtocolMethod} {task.Request.Url}",
                Stage = group.Stage,
            };

            var vars = new Dictionary<string, object?>();
            var matched = false;
            foreach (var service in group.GetAll<WebService>())
            {
                RoutingReportNode node;
                if (matched && group.SingleExecution)
                {
                    node = new RoutingReportNode
                    {
                        Label = DescribeService(service),
                        Source = DescribeSource(service),
                        Priority = service.Priority,
                        Outcome = RoutingOutcome.NotReached,
                    };
                }
                else
                {
                    node = EvaluateService(service, task, vars);
                    if (node.Outcome == RoutingOutcome.Accepted)
                    {
                        matched = true;
                        report.MatchedLabel = FindMatchedLabel(node);
                    }
                }
                report.Services.Add(node);
            }

            if (task.Monitor.Enabled)
            {
                using var writer = new StringWriter();
                task.Monitor.WriteTo(writer);
                report.MonitorTrace = writer.ToString();
            }
            return report;
        }

        /// <summary>
        /// Builds a synthetic request from <paramref name="method" />/<paramref name="url" />/
        /// <paramref name="headers" /> and simulates it against <paramref name="group" />, just
        /// like <see cref="Run(WebServiceGroup, WebProgressTask)" />.
        /// </summary>
        public static RoutingReport Run(WebServiceGroup group, string method, string url,
            IReadOnlyDictionary<string, string>? headers = null)
        {
            ArgumentNullException.ThrowIfNull(method);
            ArgumentNullException.ThrowIfNull(url);
            using var task = new WebProgressTask();
            task.Request.ProtocolMethod = method;
            task.Request.Url = url;
            if (headers != null)
                foreach (var (key, value) in headers)
                    task.Request.HeaderParameter[key] = value;
            task.EnableMonitoring();
            return Run(group, task);
        }

        private static string FindMatchedLabel(RoutingReportNode node)
        {
            foreach (var child in node.Children)
                if (child.Outcome == RoutingOutcome.Accepted)
                    return FindMatchedLabel(child);
            return node.Label;
        }

        private static string DescribeService(WebService service) => service switch
        {
            Runtime.MethodService method => $"{method.Method.DeclaringType?.Name}.{method.Method.Name}()",
            Runtime.ServiceGroup group when group.Rules.Count > 0 =>
                string.Join(", ", group.Rules),
            _ => service.GetType().Name,
        };

        /// <summary>
        /// The user type <paramref name="service" /> was generated from, if the builder can
        /// supply one. Null for a hand-written <see cref="WebService" />, and for a
        /// <see cref="Runtime.MethodService" /> - its <see cref="DescribeService" /> label
        /// already names the declaring type and method.
        /// </summary>
        private static string? DescribeSource(WebService service) => service switch
        {
            Runtime.ServiceGroup { SourceType: not null } group => group.SourceType!.FullName,
            _ => null,
        };

        private static RoutingReportNode EvaluateService(WebService service, WebProgressTask task,
            Dictionary<string, object?> vars)
        {
            var node = new RoutingReportNode
            {
                Label = DescribeService(service),
                Source = DescribeSource(service),
                Priority = service.Priority,
            };

            switch (service)
            {
                case Runtime.ServiceGroup group:
                    EvaluateServiceGroup(group, task, vars, node);
                    break;
                case Runtime.MethodService method:
                    EvaluateMethodService(method, task, vars, node);
                    break;
                default:
                    EvaluateGenericService(service, task, node);
                    break;
            }
            return node;
        }

        private static void EvaluateServiceGroup(Runtime.ServiceGroup group, WebProgressTask task,
            Dictionary<string, object?> vars, RoutingReportNode node)
        {
            foreach (var rule in group.Rules)
            {
                if (!EvaluateRule(rule, task, vars, out var reason))
                {
                    node.Outcome = RoutingOutcome.Rejected;
                    node.Reasons.Add(reason);
                    return;
                }
            }

            var matched = false;
            foreach (var child in group)
            {
                RoutingReportNode childNode;
                if (matched)
                {
                    childNode = new RoutingReportNode
                    {
                        Label = DescribeService(child),
                        Source = DescribeSource(child),
                        Priority = child.Priority,
                        Outcome = RoutingOutcome.NotReached,
                    };
                }
                else
                {
                    childNode = EvaluateService(child, task, vars);
                    if (childNode.Outcome == RoutingOutcome.Accepted)
                        matched = true;
                }
                node.Children.Add(childNode);
            }
            node.Outcome = matched ? RoutingOutcome.Accepted : RoutingOutcome.Rejected;
            if (!matched)
                node.Reasons.Add("none of the contained services matched the request");
        }

        private static void EvaluateMethodService(Runtime.MethodService method, WebProgressTask task,
            Dictionary<string, object?> vars, RoutingReportNode node)
        {
            var rulesOk = true;
            foreach (var rule in method.Rules)
            {
                if (!EvaluateRule(rule, task, vars, out var reason))
                {
                    rulesOk = false;
                    node.Reasons.Add(reason);
                }
            }

            var parametersOk = true;
            foreach (var parameter in method.Parameters)
            {
                var result = parameter.GetValue(task, vars);
                if (!result.HasValue)
                {
                    parametersOk = false;
                    node.Reasons.Add(DescribeUnresolvedParameter(method, parameter));
                }
            }

            node.Outcome = rulesOk && parametersOk ? RoutingOutcome.Accepted : RoutingOutcome.Rejected;
        }

        private static string DescribeUnresolvedParameter(Runtime.MethodService method, Runtime.IParameter parameter)
        {
            var name = (parameter as Runtime.Parameter)?.Name;
            if (string.IsNullOrEmpty(name))
            {
                var index = method.Parameters.IndexOf(parameter);
                var infos = method.Method.GetParameters();
                if (index >= 0 && index < infos.Length)
                    name = infos[index].Name;
            }
            return $"parameter '{name}' could not be resolved from the request";
        }

        private static void EvaluateGenericService(WebService service, WebProgressTask task, RoutingReportNode node)
        {
            var ok = service is WebService2 service2
                ? service2.CanWorkWith(task, out _)
                : service.CanWorkWith(task);
            node.Outcome = ok ? RoutingOutcome.Accepted : RoutingOutcome.Rejected;
            if (!ok)
                node.Reasons.Add("CanWorkWith() returned false");
        }

        private static bool EvaluateRule(Tools.RuleAttributeBase rule, WebProgressTask task,
            Dictionary<string, object?> vars, out string reason)
        {
            if (rule is IExplainableRule explainable)
            {
                var ok = explainable.CanWorkWith(task, vars, out var explained);
                reason = ok ? "" : explained ?? $"rule {rule} rejected the request";
                return ok;
            }
            var accepted = rule.CanWorkWith(task, vars);
            reason = accepted ? "" : $"rule {rule} rejected the request";
            return accepted;
        }
    }
}
