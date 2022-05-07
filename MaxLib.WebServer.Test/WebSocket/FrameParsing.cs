using MaxLib.WebServer.WebSocket;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace MaxLib.WebServer.Test.WebSocket
{
    [TestClass]
    public class FrameParsing
    {
        [TestMethod]
        public async Task ReadSingleFrameUnmaskedTextMessage()
        {
            var m = new MemoryStream(new byte[] { 0x81, 0x05, 0x48, 0x65, 0x6c, 0x6c, 0x6f });
            var frame = await Frame.TryRead(m).ConfigureAwait(false);
            Assert.IsNotNull(frame);
            Assert.IsTrue(frame!.FinalFrame);
            Assert.AreEqual(OpCode.Text, frame.OpCode);
            Assert.IsFalse(frame.HasMaskingKey);
            Assert.AreEqual("Hello", frame.TextPayload);
        }

        [TestMethod]
        public async Task ReadSingleFrameMaskedTextMessage()
        {
            var m = new MemoryStream(new byte[] { 0x81, 0x85, 0x37, 0xfa, 0x21, 0x3d, 0x7f, 0x9f, 0x4d, 0x51, 0x58 });
            var frame = await Frame.TryRead(m).ConfigureAwait(false);
            Assert.IsNotNull(frame);
            Assert.IsTrue(frame!.FinalFrame);
            Assert.AreEqual(OpCode.Text, frame.OpCode);
            Assert.IsTrue(frame.HasMaskingKey);
            frame.UnapplyMask();
            Assert.IsFalse(frame.HasMaskingKey);
            Assert.AreEqual("Hello", frame.TextPayload);
        }

        [TestMethod]
        public async Task ReadFragmentedUnmaskedTextMessage()
        {
            var m = new MemoryStream(new byte[] { 0x01, 0x03, 0x48, 0x65, 0x6c });
            var frame = await Frame.TryRead(m).ConfigureAwait(false);
            Assert.IsNotNull(frame);
            Assert.IsFalse(frame!.FinalFrame);
            Assert.AreEqual(OpCode.Text, frame.OpCode);
            Assert.IsFalse(frame.HasMaskingKey);
            Assert.AreEqual("Hel", frame.TextPayload);

            m = new MemoryStream(new byte[] { 0x80, 0x02, 0x6c, 0x6f });
            frame = await Frame.TryRead(m).ConfigureAwait(false);
            Assert.IsNotNull(frame);
            Assert.IsTrue(frame!.FinalFrame);
            Assert.AreEqual(OpCode.Continuation, frame.OpCode);
            Assert.IsFalse(frame.HasMaskingKey);
            Assert.AreEqual("lo", frame.TextPayload);
        }


        [TestMethod]
        public async Task ReadUnmaskedPingAndMaskedPongMessage()
        {
            var m = new MemoryStream(new byte[] { 0x89, 0x05, 0x48, 0x65, 0x6c, 0x6c, 0x6f });
            var frame = await Frame.TryRead(m).ConfigureAwait(false);
            Assert.IsNotNull(frame);
            Assert.IsTrue(frame!.FinalFrame);
            Assert.AreEqual(OpCode.Ping, frame.OpCode);
            Assert.IsFalse(frame.HasMaskingKey);
            Assert.AreEqual("Hello", frame.TextPayload);

            m = new MemoryStream(new byte[] { 0x8a, 0x85, 0x37, 0xfa, 0x21, 0x3d, 0x7f, 0x9f, 0x4d, 0x51, 0x58 });
            frame = await Frame.TryRead(m).ConfigureAwait(false);
            Assert.IsNotNull(frame);
            Assert.IsTrue(frame!.FinalFrame);
            Assert.AreEqual(OpCode.Pong, frame.OpCode);
            Assert.IsTrue(frame.HasMaskingKey);
            frame.UnapplyMask();
            Assert.IsFalse(frame.HasMaskingKey);
            Assert.AreEqual("Hello", frame.TextPayload);
        }

        [TestMethod]
        public async Task Read256ByteUnmaskedMessage()
        {
            Memory<byte> data = new byte[4 + 256];
            (new byte[] { 0x82, 0x7E, 0x01, 0x00 }).CopyTo(data[..4]);
            var m = new MemoryStream(data.ToArray());
            var frame = await Frame.TryRead(m).ConfigureAwait(false);
            Assert.IsNotNull(frame);
            Assert.IsTrue(frame!.FinalFrame);
            Assert.AreEqual(OpCode.Binary, frame.OpCode);
            Assert.IsFalse(frame.HasMaskingKey);
            Assert.AreEqual(256, frame.Payload.Length);
        }

        [TestMethod]
        public async Task Read64KiByteUnmaskedMessage()
        {
            Memory<byte> data = new byte[10 + 65536];
            (new byte[] { 0x82, 0x7F, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00 }).CopyTo(data[..10]);
            var m = new MemoryStream(data.ToArray());
            var frame = await Frame.TryRead(m).ConfigureAwait(false);
            Assert.IsNotNull(frame);
            Assert.IsTrue(frame!.FinalFrame);
            Assert.AreEqual(OpCode.Binary, frame.OpCode);
            Assert.IsFalse(frame.HasMaskingKey);
            Assert.AreEqual(65536, frame.Payload.Length);
        }
    }
}
