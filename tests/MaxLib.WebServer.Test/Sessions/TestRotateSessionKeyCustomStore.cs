using MaxLib.WebServer.Sessions;
using MaxLib.WebServer.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MaxLib.WebServer.Test.Sessions
{
    // A session store written the way an external implementer has to write one: Session.Key
    // has an internal setter, so it cannot be assigned from outside the library.
    class DictionarySessionService : SessionServiceBase
    {
        public Dictionary<string, Session> Store { get; } = new();

        protected override ValueTask<Session> Get(string key)
        {
            if (!Store.TryGetValue(key, out var session))
                Store[key] = session = new Session();
            return new ValueTask<Session>(session);
        }

        protected override ValueTask<bool> IsKeyAvailable(string key)
            => new(!Store.ContainsKey(key));

        protected override ValueTask Remove(string key)
        {
            Store.Remove(key);
            return default;
        }
    }

    [TestClass]
    public class TestRotateSessionKeyCustomStore
    {
        [TestMethod]
        public async Task TestRotateSessionKeyRetiresTheOldKeyInACustomStore()
        {
            var server = new TestWebServer();
            var service = new DictionarySessionService();
            server.AddWebService(service);
            var test = new TestTask(server)
            {
                CurrentStage = ServerStage.ParseRequest,
                TerminationStage = ServerStage.ParseRequest,
            };

            await service.ProgressTask(test.Task).ConfigureAwait(false);
            Assert.AreEqual(1, service.Store.Count);
            var oldKey = new List<string>(service.Store.Keys)[0];

            await service.RotateSessionKey(test.Task).ConfigureAwait(false);

            Assert.IsFalse(service.Store.ContainsKey(oldKey),
                "the pre-rotation session id must no longer be stored after RotateSessionKey");
        }
    }
}
