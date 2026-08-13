using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;

#nullable enable

namespace MaxLib.WebServer.Test
{
    // WebServerLog.SetLoggerFactory can only succeed once for the lifetime of the test
    // process - a second call always throws. Tests below that need a specific factory
    // bypass that guard entirely by assigning the internal WebServerLog.LoggerFactory
    // property directly (visible here via [InternalsVisibleTo] on MaxLib.WebServer), and
    // each such test declares its own dedicated throwaway type so its static logger
    // field resolves fresh against that test's factory. No two tests ever share a type,
    // so there is nothing to reset between tests, and real library types are never
    // touched here since other tests in this assembly may have already initialized their
    // static loggers against a different (or the default) factory.
    [TestClass]
    public class TestWebServerLog
    {
        [TestMethod]
        public void ForwardsLogCallToMatchingCategoryLevelAndEventId()
        {
            var factory = new RecordingLoggerFactory();
            WebServerLog.LoggerFactory = factory;

            SampleTypeA.Log();

            Assert.AreEqual(1, factory.Entries.Count);
            var entry = factory.Entries[0];
            // ILoggerFactoryExtensions.CreateLogger(Type) renders nested types with '.' instead
            // of the '+' that Type.FullName uses.
            Assert.AreEqual(typeof(SampleTypeA).FullName!.Replace('+', '.'), entry.Category);
            Assert.AreEqual(LogLevel.Error, entry.Level);
            Assert.AreEqual("sample", entry.EventId.Name);
            Assert.AreEqual("Something failed", entry.Message);
        }

        [TestMethod]
        public void PassesRealExceptionInsteadOfStringifyingIt()
        {
            var factory = new RecordingLoggerFactory();
            WebServerLog.LoggerFactory = factory;

            var thrown = SampleTypeB.LogWithException();

            Assert.AreEqual(1, factory.Entries.Count);
            Assert.AreSame(thrown, factory.Entries[0].Exception);
        }

        [TestMethod]
        public void SecondCallToSetLoggerFactoryThrows()
        {
            WebServerLog.SetLoggerFactory(new RecordingLoggerFactory());

            Assert.ThrowsExactly<InvalidOperationException>(
                () => WebServerLog.SetLoggerFactory(new RecordingLoggerFactory())
            );
        }

        static class SampleTypeA
        {
            static readonly ILogger logger = WebServerLog.LoggerFactory.CreateLogger(typeof(SampleTypeA));
            static readonly EventId SampleEventId = new(0, "sample");

            public static void Log() => logger.LogError(SampleEventId, "Something failed");
        }

        static class SampleTypeB
        {
            static readonly ILogger logger = WebServerLog.LoggerFactory.CreateLogger(typeof(SampleTypeB));
            static readonly EventId SampleEventId = new(0, "sample");

            public static Exception LogWithException()
            {
                var exception = new InvalidOperationException("boom");
                logger.LogError(SampleEventId, exception, "Something failed");
                return exception;
            }
        }

        sealed class RecordingLoggerFactory : ILoggerFactory
        {
            public List<(string Category, LogLevel Level, EventId EventId, Exception? Exception, string Message)> Entries { get; }
                = new List<(string Category, LogLevel Level, EventId EventId, Exception? Exception, string Message)>();

            public ILogger CreateLogger(string categoryName) => new RecordingLogger(categoryName, Entries);

            public void AddProvider(ILoggerProvider provider) { }

            public void Dispose() { }
        }

        sealed class RecordingLogger : ILogger
        {
            readonly string category;
            readonly List<(string Category, LogLevel Level, EventId EventId, Exception? Exception, string Message)> entries;

            public RecordingLogger(string category,
                List<(string Category, LogLevel Level, EventId EventId, Exception? Exception, string Message)> entries)
            {
                this.category = category;
                this.entries = entries;
            }

            public IDisposable BeginScope<TState>(TState state) where TState : notnull => NoopDisposable.Instance;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                entries.Add((category, logLevel, eventId, exception, formatter(state, exception)));
            }
        }

        sealed class NoopDisposable : IDisposable
        {
            public static readonly NoopDisposable Instance = new NoopDisposable();

            public void Dispose() { }
        }
    }
}
