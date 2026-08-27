using MaxLib.WebServer.Sessions;
using MaxLib.WebServer.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace MaxLib.WebServer.Test.Sessions
{
    class TestableSessionService : MemorySessionService
    {
        public ValueTask<string> ExposedGenerateSessionKey()
            => GenerateSessionKey();
    }

    [TestClass]
    public class TestSessionServiceBase
    {
        [TestMethod]
        public async Task TestGenerateSessionKeyHas128BitsOfEntropy()
        {
            var service = new TestableSessionService();
            var key = await service.ExposedGenerateSessionKey().ConfigureAwait(false);
            var bytes = Convert.FromBase64String(key);
            Assert.AreEqual(16, bytes.Length);
        }

        [TestMethod]
        public async Task TestGenerateSessionKeyIsNotPredictableAcrossCalls()
        {
            var service = new TestableSessionService();
            var keys = new string[1000];
            for (var i = 0; i < keys.Length; ++i)
                keys[i] = await service.ExposedGenerateSessionKey().ConfigureAwait(false);
            Assert.AreEqual(keys.Length, keys.Distinct().Count());
        }

        [TestMethod]
        public async Task TestGenerateSessionKeyIsThreadSafe()
        {
            var service = new TestableSessionService();
            var tasks = Enumerable.Range(0, 64)
                .Select(_ => Task.Run(async () => await service.ExposedGenerateSessionKey().ConfigureAwait(false)))
                .ToArray();
            var keys = await Task.WhenAll(tasks).ConfigureAwait(false);
            Assert.AreEqual(keys.Length, keys.Distinct().Count());
        }

        [TestMethod]
        public async Task TestProgressTaskIssuesASessionCookieWithA128BitKey()
        {
            var server = new TestWebServer();
            var service = new MemorySessionService();
            server.AddWebService(service);
            var test = new TestTask(server)
            {
                CurrentStage = ServerStage.ParseRequest,
                TerminationStage = ServerStage.ParseRequest,
            };

            await service.ProgressTask(test.Task).ConfigureAwait(false);

            var added = test.GetAddedCookies().ToArray();
            Assert.AreEqual(1, added.Length);
            Assert.AreEqual("Session", added[0].Item1);
            var bytes = Convert.FromBase64String(added[0].Item2.ValueString);
            Assert.AreEqual(16, bytes.Length);
        }

        [TestMethod]
        public async Task TestProgressTaskIgnoresAnUnknownClientSuppliedSessionId()
        {
            var server = new TestWebServer();
            var service = new MemorySessionService();
            server.AddWebService(service);
            var test = new TestTask(server)
            {
                CurrentStage = ServerStage.ParseRequest,
                TerminationStage = ServerStage.ParseRequest,
            };
            const string attackerChosenId = "FIXEDVALUE";
            test.Request.HeaderParameter.Add("Cookie", $"Session={attackerChosenId}");

            await service.ProgressTask(test.Task).ConfigureAwait(false);

            // the attacker-chosen id must never end up in the store, or the attacker could
            // fix a victim's session id in advance
            Assert.IsFalse(service.Sessions.ContainsKey(attackerChosenId));
            var added = test.GetAddedCookies().Single();
            Assert.AreEqual("Session", added.Item1);
            Assert.AreNotEqual(attackerChosenId, added.Item2.ValueString);
        }

        [TestMethod]
        public async Task TestProgressTaskReusesAnExistingSessionId()
        {
            var server = new TestWebServer();
            var service = new MemorySessionService();
            server.AddWebService(service);

            var first = new TestTask(server)
            {
                CurrentStage = ServerStage.ParseRequest,
                TerminationStage = ServerStage.ParseRequest,
            };
            await service.ProgressTask(first.Task).ConfigureAwait(false);
            var issuedKey = first.GetAddedCookies().Single().Item2.ValueString;
            first.Task.Session!["marker"] = "hello";

            var second = new TestTask(server)
            {
                CurrentStage = ServerStage.ParseRequest,
                TerminationStage = ServerStage.ParseRequest,
            };
            second.Request.HeaderParameter.Add("Cookie", $"Session={WebServerUtils.EncodeUri(issuedKey)}");

            await service.ProgressTask(second.Task).ConfigureAwait(false);

            Assert.AreEqual(0, second.GetAddedCookies().Count());
            Assert.AreEqual("hello", second.Task.Session!["marker"]);
        }

        [TestMethod]
        public async Task TestRotateSessionKeyMigratesDataAndChangesTheCookie()
        {
            var server = new TestWebServer();
            var service = new MemorySessionService();
            server.AddWebService(service);
            var test = new TestTask(server)
            {
                CurrentStage = ServerStage.ParseRequest,
                TerminationStage = ServerStage.ParseRequest,
            };
            await service.ProgressTask(test.Task).ConfigureAwait(false);
            var oldKey = test.GetAddedCookies().Single().Item2.ValueString;
            test.Task.Session!["marker"] = "hello";

            var rotated = await service.RotateSessionKey(test.Task).ConfigureAwait(false);

            var newKey = test.GetAddedCookies().Single().Item2.ValueString;
            Assert.AreNotEqual(oldKey, newKey);
            Assert.AreSame(rotated, test.Task.Session);
            Assert.AreEqual("hello", rotated["marker"]);
            Assert.IsTrue(service.Sessions.ContainsKey(newKey));
        }
    }
}
