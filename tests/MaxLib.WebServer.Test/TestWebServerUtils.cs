using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

#nullable enable

namespace MaxLib.WebServer.Test
{
    [TestClass]
    public class TestWebServerUtils
    {
        [TestMethod]
        public void DefaultsToIecPrefixesForShortNames()
        {
            Assert.AreEqual("512 B", WebServerUtils.GetVolumeString(512, true, 4));
            Assert.AreEqual("1 KiB", WebServerUtils.GetVolumeString(1024, true, 4));
            Assert.AreEqual("1 MiB", WebServerUtils.GetVolumeString(1024 * 1024, true, 4));
            Assert.AreEqual("1 GiB", WebServerUtils.GetVolumeString(1024L * 1024 * 1024, true, 4));
        }

        [TestMethod]
        public void DefaultsToIecPrefixesForLongNames()
        {
            Assert.AreEqual("1 Kibibyte", WebServerUtils.GetVolumeString(1024, false, 4));
            Assert.AreEqual("1 Mebibyte", WebServerUtils.GetVolumeString(1024 * 1024, false, 4));
        }

        [TestMethod]
        public void IecPrefixesDivideByOneThousandTwentyFourPerStep()
        {
            // 1536 bytes = 1.5 KiB, confirming steps use base-1024, not base-1000
            Assert.AreEqual("1.5 KiB", WebServerUtils.GetVolumeString(1536, true, 4, PrefixKind.IEC));
        }

        [TestMethod]
        public void SiPrefixesUseShortNames()
        {
            Assert.AreEqual("512 B", WebServerUtils.GetVolumeString(512, true, 4, PrefixKind.SI));
            Assert.AreEqual("1 KB", WebServerUtils.GetVolumeString(1000, true, 4, PrefixKind.SI));
            Assert.AreEqual("1 MB", WebServerUtils.GetVolumeString(1000 * 1000, true, 4, PrefixKind.SI));
            Assert.AreEqual("1 GB", WebServerUtils.GetVolumeString(1000L * 1000 * 1000, true, 4, PrefixKind.SI));
        }

        [TestMethod]
        public void SiPrefixesUseLongNames()
        {
            Assert.AreEqual("1 Kilobyte", WebServerUtils.GetVolumeString(1000, false, 4, PrefixKind.SI));
            Assert.AreEqual("1 Megabyte", WebServerUtils.GetVolumeString(1000 * 1000, false, 4, PrefixKind.SI));
        }

        [TestMethod]
        public void SiPrefixesDivideByOneThousandPerStep()
        {
            // 1500 bytes = 1.5 KB, confirming steps use base-1000, not base-1024
            Assert.AreEqual("1.5 KB", WebServerUtils.GetVolumeString(1500, true, 4, PrefixKind.SI));
        }

        [TestMethod]
        public void ThrowsForNegativeByteCount()
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(
                () => WebServerUtils.GetVolumeString(-1, true, 4)
            );
        }

        [TestMethod]
        public void ThrowsForNegativeFractionalByteCount()
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(
                () => WebServerUtils.GetVolumeString(-0.5, true, 4)
            );
        }

        [TestMethod]
        public void ThrowsForNaNByteCount()
        {
            // NaN is neither negative nor >= 0, so it slips past ThrowIfNegative and needs
            // its own IsFinite check.
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(
                () => WebServerUtils.GetVolumeString(double.NaN, true, 4)
            );
        }

        [TestMethod]
        public void ThrowsForInfiniteByteCount()
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(
                () => WebServerUtils.GetVolumeString(double.PositiveInfinity, true, 4)
            );
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(
                () => WebServerUtils.GetVolumeString(double.NegativeInfinity, true, 4)
            );
        }

        [TestMethod]
        public void ZeroBytesFormatsAsZeroBytes()
        {
            Assert.AreEqual("0 B", WebServerUtils.GetVolumeString(0, true, 4));
        }

        [TestMethod]
        public void DoesNotThrowForByteCountsBeyondLongMaxValue()
        {
            // long.MaxValue only reaches EiB/EB, well short of the unit table's end - this
            // exercises the step-clamping guard that keeps values beyond the unit table from
            // throwing.
            Assert.AreEqual("8 EiB", WebServerUtils.GetVolumeString(long.MaxValue, true, 4));
            Assert.AreEqual("9.223 EB", WebServerUtils.GetVolumeString(long.MaxValue, true, 4, PrefixKind.SI));
        }

        [TestMethod]
        public void ReachesTheRonnaAndQuettaPrefixesNowThatByteCountIsADouble()
        {
            // long.MaxValue (~9.22e18) can never reach these: 1024^9 (~1.24e27) and 1000^9
            // (~1e27) both already exceed it. Accepting a double instead of a long is what
            // makes these prefixes reachable at all.
            Assert.AreEqual("5 RiB", WebServerUtils.GetVolumeString(5 * Math.Pow(1024, 9), true, 4));
            Assert.AreEqual("7 QiB", WebServerUtils.GetVolumeString(7 * Math.Pow(1024, 10), true, 4));
            Assert.AreEqual("5 Robibyte", WebServerUtils.GetVolumeString(5 * Math.Pow(1024, 9), false, 4));
            Assert.AreEqual("7 Quebibyte", WebServerUtils.GetVolumeString(7 * Math.Pow(1024, 10), false, 4));
            Assert.AreEqual("5 RB", WebServerUtils.GetVolumeString(5 * Math.Pow(1000, 9), true, 4, PrefixKind.SI));
            Assert.AreEqual("7 QB", WebServerUtils.GetVolumeString(7 * Math.Pow(1000, 10), true, 4, PrefixKind.SI));
        }

        [TestMethod]
        public void ClampsToQuettaForByteCountsBeyondTheUnitTable()
        {
            // Even double.MaxValue (~1.8e308) must not throw or index past the table -
            // it clamps to the largest unit, QiB.
            StringAssert.EndsWith(WebServerUtils.GetVolumeString(double.MaxValue, true, 4), " QiB");
        }

        [TestMethod]
        public void FormatsByteCountsAMillionTimesLargerThanTheLargestSupportedPrefix()
        {
            // Once clamped at the largest step, the number itself keeps growing, so
            // formatting has to keep working (grouping, digit clamping, ...) rather than
            // just showing an oddly-scaled value.
            //
            // NOTE: this hardcodes QiB/QB (index 10, "Quebibyte"/"Quettabyte") as the
            // current largest prefix. If the unit tables are extended with new prefixes,
            // these expected values need to be updated to match - that is expected.
            Assert.AreEqual("1,000,000 QiB",
                WebServerUtils.GetVolumeString(1_000_000 * Math.Pow(1024, 10), true, 4));
            Assert.AreEqual("1,000,000 Quebibyte",
                WebServerUtils.GetVolumeString(1_000_000 * Math.Pow(1024, 10), false, 4));
            Assert.AreEqual("1,000,000 QB",
                WebServerUtils.GetVolumeString(1_000_000 * Math.Pow(1000, 10), true, 4, PrefixKind.SI));
            Assert.AreEqual("1,000,000 Quettabyte",
                WebServerUtils.GetVolumeString(1_000_000 * Math.Pow(1000, 10), false, 4, PrefixKind.SI));
        }

        [TestMethod]
        public void DigitsBelowLeadingDigitsIsClampedUpToLeadingDigits()
        {
            // 999 has 3 leading digits; requesting 0 digits must not truncate them away.
            Assert.AreEqual("999 B", WebServerUtils.GetVolumeString(999, true, 0, PrefixKind.SI));
        }

        [TestMethod]
        public void DigitsAboveTheStepUpperBoundIsClampedDown()
        {
            // leadingDigits(1) + 3 * step(2) = 7 significant digits is the max this step allows.
            Assert.AreEqual("1.177375 MiB", WebServerUtils.GetVolumeString(1_234_567, true, 100));
        }

        [TestMethod]
        public void GroupsIntegerDigitsWithThousandsSeparator()
        {
            // 1010 KiB stays in the KiB step (< 1024 KiB) but needs 4 leading digits,
            // which must still be grouped even though the format mask is no longer
            // hardcoded to the "0,000" 4-digit case.
            Assert.AreEqual("1,010 KiB", WebServerUtils.GetVolumeString(1024L * 1010, true, 4));
        }

        [TestMethod]
        public void RemoveCrLfLeavesOrdinaryTextUnchanged()
        {
            Assert.AreEqual("/some/path", WebServerUtils.RemoveCrLf("/some/path"));
        }

        [TestMethod]
        public void RemoveCrLfStripsEmbeddedCarriageReturnsAndLineFeeds()
        {
            // The classic response-splitting payload: an attacker-influenced value ending the
            // current header early and injecting a whole extra header/response.
            Assert.AreEqual("evilX-Injected: 1",
                WebServerUtils.RemoveCrLf("evil\r\nX-Injected: 1"));
            Assert.AreEqual("ab", WebServerUtils.RemoveCrLf("a\rb"));
            Assert.AreEqual("ab", WebServerUtils.RemoveCrLf("a\nb"));
        }

        [TestMethod]
        public void BytesEqualConstantTimeReturnsTrueForEqualSequences()
        {
            Assert.IsTrue(WebServerUtils.BytesEqualConstantTime([1, 2, 3], [1, 2, 3]));
            Assert.IsTrue(WebServerUtils.BytesEqualConstantTime([], []));
        }

        [TestMethod]
        public void BytesEqualConstantTimeReturnsFalseForDifferentContent()
        {
            Assert.IsFalse(WebServerUtils.BytesEqualConstantTime([1, 2, 3], [1, 2, 4]));
        }

        [TestMethod]
        public void BytesEqualConstantTimeReturnsFalseForDifferentLength()
        {
            Assert.IsFalse(WebServerUtils.BytesEqualConstantTime([1, 2, 3], [1, 2, 3, 4]));
        }
    }
}
