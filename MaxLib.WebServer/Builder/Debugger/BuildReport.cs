using System;
using System.Collections.Generic;
using System.Linq;

namespace MaxLib.WebServer.Builder.Debugger
{
    /// <summary>
    /// The full, structured record of a single <see cref="Service.Build{T}(out BuildReport)" />
    /// (or one of its overloads) run: what was scanned, what was built, and why anything that
    /// wasn't built got skipped or failed.
    /// </summary>
    public sealed class BuildReport
    {
        /// <summary>
        /// The point in time the build ran.
        /// </summary>
        public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// A display name for what was scanned (the type, assembly or app domain name).
        /// </summary>
        public string Source { get; init; } = "";

        /// <summary>
        /// The report for every top-level scanned type.
        /// </summary>
        public List<TypeReportNode> Roots { get; } = [];

        /// <summary>
        /// How many exported types were skipped during an assembly/app-domain scan because they
        /// don't inherit from <see cref="Service" />. These are not attached to <see cref="Roots"
        /// /> individually to avoid flooding the report with unrelated classes.
        /// </summary>
        public int SkippedNonServiceTypeCount { get; set; }

        /// <summary>
        /// Flattens the whole report tree depth-first for search, filtering or counting.
        /// </summary>
        public IEnumerable<BuildReportNode> Walk()
        {
            foreach (var root in Roots)
                foreach (var node in WalkType(root))
                    yield return node;
        }

        private static IEnumerable<BuildReportNode> WalkType(TypeReportNode type)
        {
            yield return type;
            foreach (var method in type.Methods)
                foreach (var node in WalkMethod(method))
                    yield return node;
            foreach (var nested in type.NestedTypes)
                foreach (var node in WalkType(nested))
                    yield return node;
        }

        private static IEnumerable<BuildReportNode> WalkMethod(MethodReportNode method)
        {
            yield return method;
            foreach (var parameter in method.Parameters)
                yield return parameter;
            if (method.Result != null)
                yield return method.Result;
        }

        /// <summary>
        /// Counts every node in the report tree with the given <paramref name="status" />.
        /// </summary>
        public int CountByStatus(BuildReportStatus status)
            => Walk().Count(node => node.Status == status);
    }
}
