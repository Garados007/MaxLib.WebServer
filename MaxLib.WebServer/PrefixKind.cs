namespace MaxLib.WebServer
{
    /// <summary>
    /// The unit prefix system used by <see cref="WebServerUtils.GetVolumeString(double, bool, int, PrefixKind)" />
    /// to format a byte count.
    /// </summary>
    public enum PrefixKind
    {
        /// <summary>
        /// Binary prefixes (KiB, MiB, GiB, ...) as defined by IEC 80000-13. Each step is a
        /// factor of 1024.
        /// </summary>
        IEC,
        /// <summary>
        /// Decimal prefixes (KB, MB, GB, ...) as defined by the International System of Units.
        /// Each step is a factor of 1000.
        /// </summary>
        SI,
    }
}
