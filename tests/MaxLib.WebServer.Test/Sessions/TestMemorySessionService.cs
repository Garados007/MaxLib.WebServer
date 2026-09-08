using System;
using System.Threading.Tasks;
using MaxLib.WebServer.Sessions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MaxLib.WebServer.Test.Sessions
{
    [TestClass]
    public class TestMemorySessionService
    {
        [TestMethod]
        public async Task TestGetTreatsAnExpiredSessionAsGoneInsteadOfResurrectingIt()
        {
            var service = new TestableSessionService { MaxAge = TimeSpan.FromMinutes(1) };
            const string key = "expired-key";
            var stale = new Session { LastUsed = DateTime.UtcNow - TimeSpan.FromHours(1) };
            stale["marker"] = "stale-data";
            service.Sessions[key] = stale;

            var fresh = await service.ExposedGet(key).ConfigureAwait(false);

            Assert.AreNotSame(stale, fresh);
            Assert.AreEqual(0, fresh.Count);
            Assert.AreSame(fresh, service.Sessions[key]);
        }

        [TestMethod]
        public async Task TestGetKeepsAFreshSessionUntouched()
        {
            var service = new TestableSessionService { MaxAge = TimeSpan.FromMinutes(1) };
            const string key = "fresh-key";
            var live = new Session { LastUsed = DateTime.UtcNow };
            live["marker"] = "still-good";
            service.Sessions[key] = live;

            var result = await service.ExposedGet(key).ConfigureAwait(false);

            Assert.AreSame(live, result);
            Assert.AreEqual("still-good", result["marker"]);
        }

        [TestMethod]
        public void TestSweepRemovesOnlyExpiredSessionsAndReportsTheOldestSurvivor()
        {
            var service = new MemorySessionService { MaxAge = TimeSpan.FromMinutes(10) };
            var now = DateTime.UtcNow;
            service.Sessions["expired-1"] = new Session { LastUsed = now - TimeSpan.FromHours(1) };
            service.Sessions["expired-2"] = new Session { LastUsed = now - TimeSpan.FromDays(1) };
            service.Sessions["alive-older"] = new Session { LastUsed = now - TimeSpan.FromMinutes(5) };
            service.Sessions["alive-newer"] = new Session { LastUsed = now - TimeSpan.FromMinutes(1) };

            var oldestSurviving = service.Sweep();

            Assert.IsFalse(service.Sessions.ContainsKey("expired-1"));
            Assert.IsFalse(service.Sessions.ContainsKey("expired-2"));
            Assert.IsTrue(service.Sessions.ContainsKey("alive-older"));
            Assert.IsTrue(service.Sessions.ContainsKey("alive-newer"));
            Assert.AreEqual(service.Sessions["alive-older"].LastUsed, oldestSurviving);
        }

        [TestMethod]
        public void TestSweepReturnsNullWhenNoSessionSurvives()
        {
            var service = new MemorySessionService { MaxAge = TimeSpan.FromMinutes(1) };
            service.Sessions["expired"] = new Session { LastUsed = DateTime.UtcNow - TimeSpan.FromHours(1) };

            var oldestSurviving = service.Sweep();

            Assert.IsNull(oldestSurviving);
            Assert.AreEqual(0, service.Sessions.Count);
        }

        [TestMethod]
        public async Task TestAutomaticSweepEvictsAnAlreadyExpiredSessionShortlyAfterStarting()
        {
            var service = new MemorySessionService
            {
                MaxAge = TimeSpan.FromMilliseconds(1),
                AutomaticSweepMargin = TimeSpan.FromMilliseconds(1),
            };
            service.Sessions["stale"] = new Session { LastUsed = DateTime.UtcNow - TimeSpan.FromDays(1) };
            try
            {
                service.StartAutomaticSweep();
                var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
                while (service.Sessions.ContainsKey("stale") && DateTime.UtcNow < deadline)
                    await Task.Delay(20).ConfigureAwait(false);
            }
            finally
            {
                service.Dispose();
            }
            Assert.IsFalse(service.Sessions.ContainsKey("stale"));
        }

        [TestMethod]
        public void TestStopAutomaticSweepAndDisposeAreSafeWithoutEverStarting()
        {
            var service = new MemorySessionService();
            service.StopAutomaticSweep();
            service.Dispose();
        }
    }
}
