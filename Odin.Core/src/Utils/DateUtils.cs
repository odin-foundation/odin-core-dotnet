using System;
using System.Globalization;
using System.Text;

namespace Odin.Core.Utils;

/// <summary>Utility methods for ISO 8601 date/time parsing and conversion.</summary>
public static class DateUtils
{
    private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>Parse an ISO 8601 date string (YYYY-MM-DD) into components.</summary>
    public static (int year, byte month, byte day)? ParseDate(string raw)
    {
        if (raw.Length < 10) return null;
        var parts = raw.Split('-');
        if (parts.Length < 3) return null;

        if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var year))
            return null;
        if (!byte.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var month))
            return null;
        if (!byte.TryParse(parts[2].Substring(0, Math.Min(2, parts[2].Length)), NumberStyles.Integer, CultureInfo.InvariantCulture, out var day))
            return null;

        if (month < 1 || month > 12 || day < 1 || day > 31)
            return null;

        return (year, month, day);
    }

    /// <summary>Parse an ISO 8601 timestamp to epoch milliseconds.</summary>
    public static long? ParseTimestampToEpochMs(string raw)
    {
        if (DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal, out var dto))
        {
            return (long)(dto.UtcDateTime - UnixEpoch).TotalMilliseconds;
        }
        return null;
    }

    /// <summary>Convert epoch milliseconds to ISO 8601 timestamp string.</summary>
    public static string EpochMsToTimestamp(long epochMs)
    {
        var dt = UnixEpoch.AddMilliseconds(epochMs);
        return dt.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
    }

    /// <summary>Get today's date as YYYY-MM-DD.</summary>
    public static string Today()
    {
        var now = DateTime.UtcNow;
        return $"{now.Year:D4}-{now.Month:D2}-{now.Day:D2}";
    }

    /// <summary>Get current timestamp as ISO 8601.</summary>
    public static string Now()
    {
        return DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
    }

    /// <summary>Add days to a date string.</summary>
    public static string? AddDays(string dateStr, int days)
    {
        if (DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
        {
            var result = dt.AddDays(days);
            return $"{result.Year:D4}-{result.Month:D2}-{result.Day:D2}";
        }
        return null;
    }

    /// <summary>Add months to a date string.</summary>
    public static string? AddMonths(string dateStr, int months)
    {
        if (DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
        {
            var result = dt.AddMonths(months);
            return $"{result.Year:D4}-{result.Month:D2}-{result.Day:D2}";
        }
        return null;
    }

    /// <summary>Calculate difference in days between two date strings.</summary>
    public static long? DateDiffDays(string date1, string date2)
    {
        if (DateTime.TryParse(date1, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt1) &&
            DateTime.TryParse(date2, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt2))
        {
            return (long)(dt2 - dt1).TotalDays;
        }
        return null;
    }

    /// <summary>Validate that a date string represents a real date.</summary>
    public static bool IsValidDate(int year, int month, int day)
    {
        if (month < 1 || month > 12 || day < 1) return false;
        int maxDay = DaysInMonth(year, month);
        return day <= maxDay;
    }

    /// <summary>Get the number of days in a month.</summary>
    public static int DaysInMonth(int year, int month)
    {
        if (month == 2)
            return IsLeapYear(year) ? 29 : 28;
        return month switch
        {
            4 or 6 or 9 or 11 => 30,
            _ => 31,
        };
    }

    /// <summary>Check if a year is a leap year.</summary>
    public static bool IsLeapYear(int year)
    {
        return (year % 4 == 0 && year % 100 != 0) || (year % 400 == 0);
    }

    /// <summary>Format a date from components.</summary>
    public static string FormatDate(int year, int month, int day, string format)
    {
        try
        {
            var dt = new DateTime(year, month, day);
            return dt.ToString(format, CultureInfo.InvariantCulture);
        }
        catch
        {
            return $"{year:D4}-{month:D2}-{day:D2}";
        }
    }
}
