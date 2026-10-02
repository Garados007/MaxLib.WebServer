using System;
using System.Globalization;

namespace MaxLib.WebServer.Builder.Converter
{
    public class SystemConverter : Tools.IConverter
    {
        public Func<object?, object?>? GetConverter(Type source, Type target)
        {
            ArgumentNullException.ThrowIfNull(target);
            // simple conversion
            if (source == target || target.IsAssignableFrom(source))
                return value => value;

            // Convert.ChangeType throws InvalidCastException for enums, and string and enums are both IConvertible,
            // so enums must be handled before the IConvertible branch below
            if (target.IsEnum && source == typeof(string))
                return value =>
                {
                    var parsed = Enum.Parse(target, (string)value!, ignoreCase: true);
                    // Enum.Parse accepts numeric strings outside the enum's named members (e.g. "99"); reject them like unknown names.
                    // Exception: a [Flags] enum's comma-separated syntax ("Read,Write") is accepted if every bit it sets
                    // is covered by a named member.
                    if (!Enum.IsDefined(target, parsed) &&
                        !(Attribute.IsDefined(target, typeof(FlagsAttribute)) && IsValidFlagsCombination(target, parsed)))
                        throw new ArgumentException(
                            $"'{value}' is not a defined member of enum {target}.", nameof(value));
                    return parsed;
                };

            // check if IConvertible is implemented
            var iConvertible = typeof(IConvertible);
            if (iConvertible.IsAssignableFrom(source) && iConvertible.IsAssignableFrom(target))
                return value => Convert.ChangeType(value, target, CultureInfo.InvariantCulture);
            
            // unknown conversion
            return null;
        }

        // True if every bit set in `parsedValue` is also set in the OR of all of `enumType`'s named member values.
        private static bool IsValidFlagsCombination(Type enumType, object parsedValue)
        {
            ulong combined;
            ulong allFlags = 0;
            try
            {
                combined = Convert.ToUInt64(parsedValue, CultureInfo.InvariantCulture);
                foreach (var member in Enum.GetValues(enumType))
                    allFlags |= Convert.ToUInt64(member, CultureInfo.InvariantCulture);
            }
            catch (OverflowException)
            {
                // a negative underlying value (e.g. the sign bit used as a flag) is not supported; treat as invalid
                return false;
            }
            return (combined & ~allFlags) == 0;
        }
    }
}