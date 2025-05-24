using CsvHelper.Configuration;
using CsvHelper;
using CsvHelper.TypeConversion;
using System.Globalization;

namespace CryptoTracker.Import
{
    public class UtcDateTimeConverter : DateTimeConverter
    {
        public override object? ConvertFromString(string? text, IReaderRow row, MemberMapData memberMapData)
        {
            var culture = row.Context?.Configuration?.CultureInfo
                          ?? CultureInfo.InvariantCulture;

            if (!string.IsNullOrWhiteSpace(text)
                && DateTimeOffset.TryParse(
                       text,
                       culture,
                       DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                       out var dto))
            {
                return dto.ToOffset(TimeSpan.Zero);
            }

            return base.ConvertFromString(text, row, memberMapData);
        }
    }
}
