using MaxLib.WebServer.WebSocket;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;

#nullable enable

namespace MaxLib.WebServer.Test.WebSocket
{
    [TestClass]
    public class TestWebSocketConnection
    {
        [TestMethod]
        public void TestTryReassembleFragmentedPayloadConcatenatesFragmentsInOrder()
        {
            var queue = new Queue<Memory<byte>>();
            queue.Enqueue(new byte[] { 1, 2, 3 });
            queue.Enqueue(new byte[] { 4, 5 });

            var result = WebSocketConnection.TryReassembleFragmentedPayload(queue);

            Assert.IsNotNull(result);
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4, 5 }, result!.Value.ToArray());
            Assert.AreEqual(0, queue.Count, "the queue should be fully drained on success");
        }

        [TestMethod]
        public void TestTryReassembleFragmentedPayloadRejectsOversizedAccumulationInsteadOfCrashing()
        {
            // Two fragments individually far below any single-object allocation limit, but
            // whose combined length crosses int.MaxValue - the "many small fragments" route
            // described in ws-unbounded-fragment-accumulation-dos, reproduced here with just
            // two frames instead of an actual multi-gigabyte transfer. Both queue entries
            // share the same backing array (the rejection path never reads it, only sums
            // lengths), so this only allocates it once.
            var chunk = new byte[(int.MaxValue / 2) + 2];
            var queue = new Queue<Memory<byte>>();
            queue.Enqueue(chunk);
            queue.Enqueue(chunk);

            // this used to throw an unhandled OverflowException from `new byte[maxSize]`
            // instead of returning - see ws-fragment-reassembly-overflow-crash.md
            var result = WebSocketConnection.TryReassembleFragmentedPayload(queue);

            Assert.IsNull(result);
            Assert.AreEqual(2, queue.Count,
                "a rejected queue is left untouched for the caller to still inspect");
        }
    }
}
