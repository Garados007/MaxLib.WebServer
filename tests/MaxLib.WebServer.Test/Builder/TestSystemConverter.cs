using System;
using MaxLib.WebServer.Builder.Converter;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#nullable enable

namespace MaxLib.WebServer.Test.Builder
{
    [TestClass]
    public class TestSystemConverter
    {
        private enum Color { Red, Green, Blue }

        [Flags]
        private enum Perm : byte { None = 0, Read = 1, Write = 2, Execute = 4 }

        [TestMethod]
        public void TestConvertsAStringToAnEnumByMemberName()
        {
            // Convert.ChangeType throws InvalidCastException for enums; they must be parsed by name instead
            var converter = new SystemConverter().GetConverter(typeof(string), typeof(Color));

            Assert.IsNotNull(converter);
            Assert.AreEqual(Color.Green, converter!("Green"));
        }

        [TestMethod]
        public void TestConvertsAStringToAnEnumCaseInsensitively()
        {
            var converter = new SystemConverter().GetConverter(typeof(string), typeof(Color));

            Assert.IsNotNull(converter);
            Assert.AreEqual(Color.Blue, converter!("blue"));
        }

        [TestMethod]
        public void TestInvalidEnumMemberNameThrows()
        {
            var converter = new SystemConverter().GetConverter(typeof(string), typeof(Color));

            Assert.IsNotNull(converter);
            Assert.ThrowsExactly<ArgumentException>(() => converter!("Purple"));
        }

        [TestMethod]
        public void TestOutOfRangeNumericStringIsRejectedNotSilentlyBound()
        {
            // Enum.Parse accepts any numeric string, so a value like "99" must not bind an undefined member
            var converter = new SystemConverter().GetConverter(typeof(string), typeof(Color));

            Assert.IsNotNull(converter);
            Assert.ThrowsExactly<ArgumentException>(() => converter!("99"));
        }

        [TestMethod]
        public void TestInRangeNumericStringMatchingADefinedMemberStillConverts()
        {
            var converter = new SystemConverter().GetConverter(typeof(string), typeof(Color));

            Assert.IsNotNull(converter);
            Assert.AreEqual(Color.Green, converter!("1"));
        }

        [TestMethod]
        public void TestFlagsEnumCombinationNotSeparatelyNamedStillConverts()
        {
            // the comma-separated flags syntax ("Read,Write") is valid for a [Flags] enum
            // even when the combination is not a named member
            var converter = new SystemConverter().GetConverter(typeof(string), typeof(Perm));

            Assert.IsNotNull(converter);
            Assert.AreEqual(Perm.Read | Perm.Write, converter!("Read,Write"));
        }

        [TestMethod]
        public void TestFlagsEnumOutOfRangeCombinationStillRejected()
        {
            // a bit outside the union of all named flags must still be rejected
            var converter = new SystemConverter().GetConverter(typeof(string), typeof(Perm));

            Assert.IsNotNull(converter);
            Assert.ThrowsExactly<ArgumentException>(() => converter!("99"));
        }

        [TestMethod]
        public void TestNonFlagsEnumCommaSeparatedCombinationStillRejected()
        {
            // Enum.Parse accepts the comma-separated syntax for any enum; the flags carve-out
            // must only apply to [Flags] enums
            var converter = new SystemConverter().GetConverter(typeof(string), typeof(Color));

            Assert.IsNotNull(converter);
            Assert.ThrowsExactly<ArgumentException>(() => converter!("Green,Blue"));
        }
    }
}
