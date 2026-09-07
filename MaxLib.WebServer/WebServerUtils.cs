using System;
using System.Globalization;
using System.Net;

#nullable enable

namespace MaxLib.WebServer
{
    public static class WebServerUtils
    {
        public static string EncodeUri(string uri)
            => WebUtility.UrlEncode(uri);

        public static string DecodeUri(string uri)
            => WebUtility.UrlDecode(uri);

        /// <summary>
        /// Removes every <c>\r</c>/<c>\n</c> from <paramref name="value"/>. Use this at a sink
        /// that writes raw HTTP header names/values (or any other single-line wire text) built
        /// from untrusted or application-supplied text, to prevent CRLF/header injection -
        /// unlike <see cref="EncodeUri(string)"/>, this leaves every other character (including
        /// <c>/</c>) untouched, since header text generally isn't otherwise URI-encoded.
        /// </summary>
        public static string RemoveCrLf(string value)
        {
            ArgumentNullException.ThrowIfNull(value);
            return value.IndexOfAny(['\r', '\n']) == -1
                ? value
                : value.Replace("\r", "", StringComparison.Ordinal)
                    .Replace("\n", "", StringComparison.Ordinal);
        }

        private static readonly ReadOnlyMemory<string> iecSn = new[] { "B", "KiB", "MiB", "GiB", "TiB", "PiB", "EiB", "ZiB", "YiB", "RiB", "QiB" };
        private static readonly ReadOnlyMemory<string> iecLn = new[] { "Byte", "Kibibyte", "Mebibyte", "Gibibyte", "Tebibyte", "Pebibyte", "Exbibyte", "Zebibyte", "Yobibyte", "Robibyte", "Quebibyte" };
        private static readonly ReadOnlyMemory<string> siSn = new[] { "B", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB", "RB", "QB" };
        private static readonly ReadOnlyMemory<string> siLn = new[] { "Byte", "Kilobyte", "Megabyte", "Gigabyte", "Terabyte", "Petabyte", "Exabyte", "Zettabyte", "Yottabyte", "Ronnabyte", "Quettabyte" };

        /// <summary>
        /// Formats a byte count into a human-readable string with the specified unit prefix system.
        /// </summary>
        /// <param name="byteCount">The byte count to format. A <see cref="long"/> byte count (e.g.
        /// from <see cref="System.IO.FileInfo.Length" />) can no longer reach the largest supported
        /// prefixes (Ronna-/Quetta-), so this accepts a <see cref="double"/> instead, which can.</param>
        /// <param name="shortVersion">If true, uses short unit names (e.g., "KB", "MB"), otherwise
        /// uses long unit names (e.g., "Kilobyte", "Megabyte").</param>
        /// <param name="digits">The number of significant digits to include in the formatted
        /// string. Using a value of 15 or larger is unreliable due to precision
        /// limitations.</param>
        /// <param name="kind">The unit prefix system to use (IEC or SI).</param>
        /// <returns>A human-readable string representing the byte count with the specified unit
        /// prefix.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="byteCount"/> is
        /// negative or not finite</exception>
        /// <remarks>
        /// The method calculates the appropriate unit prefix based on the byte count and formats
        /// the number accordingly. It supports both binary (IEC) and decimal (SI) unit systems,
        /// allowing for flexible representation of data sizes. <br/>
        ///
        /// Since a <see cref="double"/> only has about 15-17 significant decimal digits of
        /// precision, very large or very precise values may lose precision. However, it is
        /// sufficient for most practical use cases.
        /// </remarks>
        public static string GetVolumeString(double byteCount, bool shortVersion, int digits,
            PrefixKind kind = PrefixKind.IEC)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(byteCount);
            if(!double.IsFinite(byteCount))
                throw new ArgumentOutOfRangeException(nameof(byteCount), "Byte count must be a finite number.");
            var names = kind == PrefixKind.SI
                ? (shortVersion ? siSn.Span : siLn.Span)
                : (shortVersion ? iecSn.Span : iecLn.Span);
            var divisor = kind == PrefixKind.SI ? 1000 : 1024;
            // select the appropriate unit prefix based on the byte count
            var step = 0;
            while (byteCount >= divisor && step < names.Length - 1)
            {
                step++;
                byteCount /= divisor;
            }
            // count the leading (integer-part) digits of byteCount, e.g. 3 for 123.45
            var leadingDigits = 1;
            for (var remaining = byteCount; remaining >= 10; remaining /= 10)
                leadingDigits++;
            // clamp digits to a reasonable range based on leading digits
            digits = Math.Clamp(digits, leadingDigits, leadingDigits + 3 * step);
            // generate format mask for the number of digits requested
            var mask = "#,#0";
            if (digits > leadingDigits) // update mask to include decimal places if needed
                mask += "." + new string('#', digits - leadingDigits);
            // return formatted string with the appropriate unit prefix
            return $"{byteCount.ToString(mask, CultureInfo.InvariantCulture)} {names[step]}";
        }

        public static string GetDateString(DateTime date)
            => date.ToUniversalTime().ToString("r");

        public static DateTime GetDateFromString(string date)
            => DateTime.TryParse(date,
                CultureInfo.InvariantCulture.DateTimeFormat,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.RoundtripKind,
                out DateTime dateTime)
            ? dateTime
            : new DateTime();


        public static bool BytesEqual(byte[] ba1, byte[] ba2)
        {
            _ = ba1 ?? throw new ArgumentNullException(nameof(ba1));
            _ = ba2 ?? throw new ArgumentNullException(nameof(ba2));
            if (ba1.Length != ba2.Length)
                return false;
            for (int i = 0; i < ba1.Length; ++i)
                if (ba1[i] != ba2[i])
                    return false;
            return true;
        }
    }
}
