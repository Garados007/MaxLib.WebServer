using MaxLib.WebServer.Sessions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MaxLib.WebServer.Test.Sessions
{
    [TestClass]
    public class TestSession
    {
        [TestMethod]
        public void TestModifyingDataDuringEnumerationDoesNotThrow()
        {
            // a plain Dictionary throws when a key is added during enumeration (as RotateSessionKey does
            // with `foreach (var pair in task.Session)`); a ConcurrentDictionary tolerates it
            var session = new Session
            {
                ["a"] = 1,
                ["b"] = 2,
            };

            var seen = 0;
            foreach (var pair in session)
            {
                session[$"added-{seen}"] = seen;
                ++seen;
                if (seen > 10)
                    break; // avoid an unbounded loop if enumeration keeps including new entries
            }

            Assert.IsTrue(seen >= 2);
        }
    }
}
