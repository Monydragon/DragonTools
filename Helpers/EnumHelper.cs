using System;

namespace DragonTools.Helpers
{
    public static class EnumHelper
    {
        /// <summary>
        /// Converts an enum value to a readable string by replacing underscores with spaces.
        /// </summary>
        /// <typeparam name="T">The enum type.</typeparam>
        /// <param name="enumValue">The enum value to convert.</param>
        /// <returns>A readable string representation of the enum value.</returns>
        public static string ToReadableString<T>(this T enumValue) where T : Enum
        {
            return enumValue.ToString().Replace("_", " ");
        }
    }
}
