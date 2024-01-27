using CryptoTracker.Common;

namespace CryptoTracker
{
    public static class EnumExtensions
    {
        public static string GetDisplayName(this Enum enumValue)
        {
            var attribute = enumValue.GetType()
                .GetField(enumValue.ToString())
                .GetCustomAttributes(typeof(DisplayNameAttribute), false)
                .FirstOrDefault() as DisplayNameAttribute;

            return attribute?.DisplayName ?? enumValue.ToString();
        }
    }
}