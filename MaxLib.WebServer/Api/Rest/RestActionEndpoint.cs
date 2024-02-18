using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

#nullable enable

namespace MaxLib.WebServer.Api.Rest
{
    [Obsolete("The ApiService and the RestApiService classes are no longer maintained and will be removed in a future update. Use the Builder system instead.")]
    public class RestActionEndpoint : RestEndpoint
    {
        public Func<Dictionary<string, object?>, Task<HttpDataSource?>> HandleRequest { get; set; }

        public RestActionEndpoint(Func<Dictionary<string, object?>, Task<HttpDataSource?>> handleRequest)
            => HandleRequest = handleRequest;

        public override Task<HttpDataSource?> GetSource(Dictionary<string, object?> args)
        {
            _ = args ?? throw new ArgumentNullException(nameof(args));
            var task = HandleRequest?.Invoke(args);
            if (task == null)
                return Task.FromResult<HttpDataSource?>(null);
            else return task;
        }

        public static RestActionEndpoint Create(Func<Dictionary<string, object?>, Task<HttpDataSource?>> handler)
            => new RestActionEndpoint(handler);

        public static RestActionEndpoint Create(Func<Dictionary<string, object?>, Task<Stream>> handler)
            => new RestActionEndpoint(async args =>
            {
                var result = await handler(args).ConfigureAwait(false);
                if (result == null)
                    return null;
                return new HttpStreamDataSource(result);
            });

        public static RestActionEndpoint Create(Func<Dictionary<string, object?>, Task<string>> handler)
            => new RestActionEndpoint(async args =>
            {
                var result = await handler(args).ConfigureAwait(false);
                if (result == null)
                    return null;
                return new HttpStringDataSource(result);
            });

        public static RestActionEndpoint Create(Func<Task<HttpDataSource>> handler)
            => new RestActionEndpoint(async args =>
            {
                var result = await handler().ConfigureAwait(false);
                if (result == null)
                    return null;
                return result;
            });

        public static RestActionEndpoint Create(Func<Task<Stream>> handler)
            => new RestActionEndpoint(async args =>
            {
                var result = await handler().ConfigureAwait(false);
                if (result == null)
                    return null;
                return new HttpStreamDataSource(result);
            });

        public static RestActionEndpoint Create(Func<Task<string>> handler)
            => new RestActionEndpoint(async args =>
            {
                var result = await handler().ConfigureAwait(false);
                if (result == null)
                    return null;
                return new HttpStringDataSource(result);
            });

        public static RestActionEndpoint Create<T>(Func<T, Task<HttpDataSource>> handler, string argName)
            => new RestActionEndpoint(async args =>
            {
                if (!GetValue<T>(args, argName, out T arg))
                    return null;
                var result = await handler(arg).ConfigureAwait(false);
                if (result == null)
                    return null;
                return result;
            });

        public static RestActionEndpoint Create<T>(Func<T, Task<Stream>> handler, string argName)
            => new RestActionEndpoint(async args =>
            {
                if (!GetValue<T>(args, argName, out T arg))
                    return null;
                var result = await handler(arg).ConfigureAwait(false);
                if (result == null)
                    return null;
                return new HttpStreamDataSource(result);
            });

        public static RestActionEndpoint Create<T>(Func<T, Task<string>> handler, string argName)
            => new RestActionEndpoint(async args =>
            {
                if (!GetValue<T>(args, argName, out T arg))
                    return null;
                var result = await handler(arg).ConfigureAwait(false);
                if (result == null)
                    return null;
                return new HttpStringDataSource(result);
            });

        public static RestActionEndpoint Create<T1, T2>(Func<T1, T2, Task<HttpDataSource>> handler, string argName1, string argName2)
            => new RestActionEndpoint(async args =>
            {
                if (!GetValue<T1>(args, argName1, out T1 arg1) ||
                    !GetValue<T2>(args, argName2, out T2 arg2))
                    return null;
                var result = await handler(arg1, arg2).ConfigureAwait(false);
                if (result == null)
                    return null;
                return result;
            });

        public static RestActionEndpoint Create<T1, T2>(Func<T1, T2, Task<Stream>> handler, string argName1, string argName2)
            => new RestActionEndpoint(async args =>
            {
                if (!GetValue<T1>(args, argName1, out T1 arg1) ||
                    !GetValue<T2>(args, argName2, out T2 arg2))
                    return null;
                var result = await handler(arg1, arg2).ConfigureAwait(false);
                if (result == null)
                    return null;
                return new HttpStreamDataSource(result);
            });

        public static RestActionEndpoint Create<T1, T2>(Func<T1, T2, Task<string>> handler, string argName1, string argName2)
            => new RestActionEndpoint(async args =>
            {
                if (!GetValue<T1>(args, argName1, out T1 arg1) ||
                    !GetValue<T2>(args, argName2, out T2 arg2))
                    return null;
                var result = await handler(arg1, arg2).ConfigureAwait(false);
                if (result == null)
                    return null;
                return new HttpStringDataSource(result);
            });

        public static RestActionEndpoint Create<T1, T2, T3>(Func<T1, T2, T3, Task<HttpDataSource>> handler, string argName1, string argName2, string argName3)
            => new RestActionEndpoint(async args =>
            {
                if (!GetValue<T1>(args, argName1, out T1 arg1) ||
                    !GetValue<T2>(args, argName2, out T2 arg2) ||
                    !GetValue<T3>(args, argName3, out T3 arg3))
                    return null;
                var result = await handler(arg1, arg2, arg3).ConfigureAwait(false);
                if (result == null)
                    return null;
                return result;
            });

        public static RestActionEndpoint Create<T1, T2, T3>(Func<T1, T2, T3, Task<Stream>> handler, string argName1, string argName2, string argName3)
            => new RestActionEndpoint(async args =>
            {
                if (!GetValue<T1>(args, argName1, out T1 arg1) ||
                    !GetValue<T2>(args, argName2, out T2 arg2) ||
                    !GetValue<T3>(args, argName3, out T3 arg3))
                    return null;
                var result = await handler(arg1, arg2, arg3).ConfigureAwait(false);
                if (result == null)
                    return null;
                return new HttpStreamDataSource(result);
            });

        public static RestActionEndpoint Create<T1, T2, T3>(Func<T1, T2, T3, Task<string>> handler, string argName1, string argName2, string argName3)
            => new RestActionEndpoint(async args =>
            {
                if (!GetValue<T1>(args, argName1, out T1 arg1) ||
                    !GetValue<T2>(args, argName2, out T2 arg2) ||
                    !GetValue<T3>(args, argName3, out T3 arg3))
                    return null;
                var result = await handler(arg1, arg2, arg3).ConfigureAwait(false);
                if (result == null)
                    return null;
                return new HttpStringDataSource(result);
            });

        public static RestActionEndpoint Create(Delegate handler, string[] argsOrder)
            => new RestActionEndpoint(async args =>
            {
                var use = new object?[argsOrder?.Length ?? 0];
                for (int i = 0; i < use.Length; ++i)
                    if (args.TryGetValue(argsOrder![i], out object? value))
                        use[i] = value;
                var result = handler.DynamicInvoke(use);
                if (result is Task task)
                {
                    await task.ConfigureAwait(false);
                    if (!GetTaskValue(task, out result))
                        result = null;
                }
                if (result is HttpDataSource dataSource)
                    return dataSource;
                if (result is Stream stream)
                    return new HttpStreamDataSource(stream);
                if (result is string text)
                    return new HttpStringDataSource(text);
                var resText = result?.ToString() ?? "";
                if (result is IDisposable disposable)
                    disposable.Dispose();
                return new HttpStringDataSource(resText);
            });

        private static bool GetTaskValue(Task task, out object? value)
        {
            // thx to: https://stackoverflow.com/a/52500763/12469007
            value = default;
            var voidTaskType = typeof(Task<>).MakeGenericType(Type.GetType("System.Threading.Tasks.VoidTaskResult")!);
            if (voidTaskType.IsAssignableFrom(task.GetType()))
                return false;
            var property = task.GetType().GetProperty("Result", BindingFlags.Public | BindingFlags.Instance);
            if (property == null)
                return false;
            value = property.GetValue(task);
            return true;
        }

        private static bool GetValue<T>(Dictionary<string, object?> args, string name, [NotNullWhen(true)] out T value)
        {
            if (args.TryGetValue(name, out object? rawValue) && rawValue is T value_)
            {
                value = value_;
                return true;
            }
            else
            {
                value = default!;
                return false;
            }
        }
    }
}
