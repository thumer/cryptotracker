using CsvHelper.Configuration;
using CsvHelper;
using CsvHelper.TypeConversion;

namespace CryptoTracker.Import
{
    public class UtcDateTimeConverter : DateTimeConverter
    {
        public override object? ConvertFromString(string? text, IReaderRow row, MemberMapData memberMapData)
        {
            var culture = row.Context?.Configuration?.CultureInfo ?? System.Globalization.CultureInfo.InvariantCulture;
            if (text != null && DateTime.TryParse(text, culture, System.Globalization.DateTimeStyles.None, out DateTime parsedDateTime))
            {
                return new DateTimeOffset(DateTime.SpecifyKind(parsedDateTime, DateTimeKind.Utc));
            }

            return base.ConvertFromString(text, row, memberMapData);
        }
    }
}
