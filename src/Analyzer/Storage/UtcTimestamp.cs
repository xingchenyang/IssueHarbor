using System.Globalization;

namespace IssueHarbor.Analyzer.Storage;

public static class UtcTimestamp
{
    public static string Format(DateTimeOffset value) => value
        .ToUniversalTime()
        .ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
}
