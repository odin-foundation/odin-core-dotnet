#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using Odin.Core.Types;
using Odin.Core.Utils;

using Odin.Core.Transform;

namespace Odin.Core.Transform.Verbs;

/// <summary>
/// Date, time, and timestamp transformation verbs. Provides 31 verbs for date arithmetic,
/// formatting, comparison, and conversion operations. Dates are stored as "YYYY-MM-DD"
/// strings and timestamps as ISO 8601 strings within <see cref="DynValue"/>.
/// </summary>
internal static class DateTimeVerbs
{
    /// <summary>
    /// Registers all date/time verbs into the provided dictionary.
    /// </summary>
    /// <param name="reg">The verb registration dictionary.</param>
    public static void Register(Dictionary<string, Func<DynValue[], VerbContext, DynValue>> reg)
    {
        reg["today"] = Today;
        reg["now"] = Now;
        reg["formatDate"] = FormatDate;
        reg["parseDate"] = ParseDate;
        reg["formatTime"] = FormatTime;
        reg["formatTimestamp"] = FormatTimestamp;
        reg["parseTimestamp"] = ParseTimestamp;
        reg["addDays"] = AddDays;
        reg["addMonths"] = AddMonths;
        reg["addYears"] = AddYears;
        reg["dateDiff"] = DateDiff;
        reg["addHours"] = AddHours;
        reg["addMinutes"] = AddMinutes;
        reg["addSeconds"] = AddSeconds;
        reg["startOfDay"] = StartOfDay;
        reg["endOfDay"] = EndOfDay;
        reg["startOfMonth"] = StartOfMonth;
        reg["endOfMonth"] = EndOfMonth;
        reg["startOfYear"] = StartOfYear;
        reg["endOfYear"] = EndOfYear;
        reg["dayOfWeek"] = DayOfWeek;
        reg["weekOfYear"] = WeekOfYear;
        reg["quarter"] = Quarter;
        reg["isLeapYear"] = IsLeapYear;
        reg["isBefore"] = IsBefore;
        reg["isAfter"] = IsAfter;
        reg["isBetween"] = IsBetween;
        reg["toUnix"] = ToUnix;
        reg["fromUnix"] = FromUnix;
        reg["daysBetweenDates"] = DaysBetweenDates;
        reg["ageFromDate"] = AgeFromDate;
        reg["isValidDate"] = IsValidDate;
        reg["formatLocaleDate"] = FormatLocaleDate;
        reg["businessDays"] = BusinessDays;
        reg["nextBusinessDay"] = NextBusinessDay;
        reg["formatDuration"] = FormatDuration;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>Extract a date string from a DynValue (Date, Timestamp, or String types).</summary>
    private static string? ExtractDateStr(DynValue v)
    {
        if (v.IsNull) return null;
        return v.AsString();
    }

    /// <summary>Parse a date string into a DateTime, supporting dates and timestamps.</summary>
    private static DateTime? ParseDt(string? s)
    {
        if (s == null) return null;
        if (DateTime.TryParse(s, CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var dt))
        {
            return dt;
        }
        return null;
    }

    /// <summary>Format a DateTime as a date-only string.</summary>
    private static string FormatAsDate(DateTime dt)
    {
        return string.Format(CultureInfo.InvariantCulture, "{0:D4}-{1:D2}-{2:D2}", dt.Year, dt.Month, dt.Day);
    }

    /// <summary>Format a DateTime as an ISO 8601 timestamp string.</summary>
    private static string FormatAsTimestamp(DateTime dt)
    {
        return dt.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
    }

    /// <summary>Extract a long from a DynValue (Integer, Float, or parseable String).</summary>
    private static long? ExtractLong(DynValue v)
    {
        var i = v.AsInt64();
        if (i.HasValue) return i.Value;
        var d = v.AsDouble();
        if (d.HasValue) return (long)d.Value;
        var s = v.AsString();
        if (s != null && long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            return parsed;
        return null;
    }

    /// <summary>Extract an int from a DynValue.</summary>
    private static int? ExtractInt(DynValue v)
    {
        var l = ExtractLong(v);
        if (l.HasValue) return (int)l.Value;
        return null;
    }

    /// <summary>Apply simple YYYY/MM/DD pattern substitution on a format string.</summary>
    private static string ApplySimpleDateFormat(DateTime dt, string pattern)
    {
        var result = pattern;
        result = result.Replace("YYYY", dt.Year.ToString("D4", CultureInfo.InvariantCulture));
        result = result.Replace("YY", (dt.Year % 100).ToString("D2", CultureInfo.InvariantCulture));
        result = result.Replace("MM", dt.Month.ToString("D2", CultureInfo.InvariantCulture));
        result = result.Replace("DD", dt.Day.ToString("D2", CultureInfo.InvariantCulture));
        result = result.Replace("HH", dt.Hour.ToString("D2", CultureInfo.InvariantCulture));
        result = result.Replace("mm", dt.Minute.ToString("D2", CultureInfo.InvariantCulture));
        result = result.Replace("ss", dt.Second.ToString("D2", CultureInfo.InvariantCulture));
        return result;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Verb Implementations
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Returns the current UTC date as a "YYYY-MM-DD" string.</summary>
    private static DynValue Today(DynValue[] args, VerbContext ctx)
    {
        return DynValue.Date(DateUtils.Today());
    }

    /// <summary>Returns the current UTC timestamp as an ISO 8601 string.</summary>
    private static DynValue Now(DynValue[] args, VerbContext ctx)
    {
        return DynValue.Timestamp(DateUtils.Now());
    }

    /// <summary>
    /// Formats a date with a pattern string. args[0]=date, args[1]=pattern.
    /// Pattern uses simple substitution: YYYY, YY, MM, DD, HH, mm, ss.
    /// </summary>
    private static DynValue FormatDate(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2) return DynValue.Null();
        var dateStr = ExtractDateStr(args[0]);
        var pattern = args[1].AsString();
        if (dateStr == null || pattern == null) return DynValue.Null();

        var dt = ParseDt(dateStr);
        if (dt == null) return DynValue.Null();

        return DynValue.String(ApplySimpleDateFormat(dt.Value, pattern));
    }

    /// <summary>
    /// Parses a date string into a Date DynValue. args[0]=date string.
    /// </summary>
    private static DynValue ParseDate(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2) return DynValue.Null();
        var s = ExtractDateStr(args[0]);
        var pattern = args[1].AsString();
        if (s == null || pattern == null) return DynValue.Null();

        // Try pattern-based parsing first
        var dt = ParseWithFormat(s, pattern);
        if (dt == null)
        {
            // Fallback: try standard parse
            dt = ParseDt(s);
        }
        if (dt == null) return DynValue.Null();

        // Returns string type (matching TypeScript behavior)
        return DynValue.String(FormatAsDate(dt.Value));
    }

    /// <summary>Parse a date string using a simple format pattern (YYYY, MM, DD, HH, mm, ss).</summary>
    private static DateTime? ParseWithFormat(string s, string pattern)
    {
        // Convert ODIN-style pattern to .NET DateTimeExact pattern
        var netFmt = pattern
            .Replace("YYYY", "yyyy")
            .Replace("YY", "yy")
            .Replace("MM", "MM")
            .Replace("DD", "dd")
            .Replace("HH", "HH")
            .Replace("mm", "mm")
            .Replace("ss", "ss");

        if (DateTime.TryParseExact(s, netFmt, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt))
        {
            return dt;
        }
        return null;
    }

    /// <summary>
    /// Formats a time value with a pattern. args[0]=timestamp/time, args[1]=optional pattern.
    /// Returns HH:MM:SS by default.
    /// </summary>
    private static DynValue FormatTime(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var s = ExtractDateStr(args[0]);
        if (s == null) return DynValue.Null();

        var dt = ParseDt(s);
        if (dt == null) return DynValue.Null();

        if (args.Length >= 2)
        {
            var pattern = args[1].AsString();
            if (pattern != null)
                return DynValue.String(ApplySimpleDateFormat(dt.Value, pattern));
        }

        return DynValue.Time(dt.Value.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Formats a timestamp with a pattern. args[0]=timestamp, args[1]=optional pattern.
    /// Returns ISO 8601 by default.
    /// </summary>
    private static DynValue FormatTimestamp(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var s = ExtractDateStr(args[0]);
        if (s == null) return DynValue.Null();

        var dt = ParseDt(s);
        if (dt == null) return DynValue.Null();

        if (args.Length >= 2)
        {
            var pattern = args[1].AsString();
            if (pattern != null)
                return DynValue.String(ApplySimpleDateFormat(dt.Value, pattern));
        }

        return DynValue.Timestamp(FormatAsTimestamp(dt.Value));
    }

    /// <summary>
    /// Parses a timestamp string into a Timestamp DynValue. args[0]=timestamp string.
    /// </summary>
    private static DynValue ParseTimestamp(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var s = ExtractDateStr(args[0]);
        if (s == null) return DynValue.Null();

        var dt = ParseDt(s);
        if (dt == null) return DynValue.Null();

        return DynValue.Timestamp(FormatAsTimestamp(dt.Value));
    }

    /// <summary>
    /// Adds N days to a date. args[0]=date, args[1]=days (integer).
    /// </summary>
    private static DynValue AddDays(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2) return DynValue.Null();
        var dateStr = ExtractDateStr(args[0]);
        var days = ExtractInt(args[1]);
        if (dateStr == null || !days.HasValue) return DynValue.Null();

        var result = DateUtils.AddDays(dateStr, days.Value);
        return result != null ? DynValue.String(result) : DynValue.Null();
    }

    /// <summary>
    /// Adds N months to a date. args[0]=date, args[1]=months (integer).
    /// </summary>
    private static DynValue AddMonths(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2) return DynValue.Null();
        var dateStr = ExtractDateStr(args[0]);
        var months = ExtractInt(args[1]);
        if (dateStr == null || !months.HasValue) return DynValue.Null();

        var result = DateUtils.AddMonths(dateStr, months.Value);
        return result != null ? DynValue.String(result) : DynValue.Null();
    }

    /// <summary>
    /// Adds N years to a date. args[0]=date, args[1]=years (integer).
    /// </summary>
    private static DynValue AddYears(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2) return DynValue.Null();
        var dateStr = ExtractDateStr(args[0]);
        var years = ExtractInt(args[1]);
        if (dateStr == null || !years.HasValue) return DynValue.Null();

        var dt = ParseDt(dateStr);
        if (dt == null) return DynValue.Null();

        try
        {
            var result = dt.Value.AddYears(years.Value);
            return DynValue.String(FormatAsDate(result));
        }
        catch
        {
            return DynValue.Null();
        }
    }

    /// <summary>
    /// Returns the difference between two dates in the specified unit.
    /// args[0]=date1, args[1]=date2, args[2]=unit ("days", "months", or "years").
    /// Unknown unit pushes T011 INCOMPATIBLE_CONVERSION error.
    /// </summary>
    private static DynValue DateDiff(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2) return DynValue.Null();
        var s1 = ExtractDateStr(args[0]);
        var s2 = ExtractDateStr(args[1]);
        if (s1 == null || s2 == null) return DynValue.Null();

        var unit = args.Length >= 3 ? (args[2].AsString() ?? "days").ToLowerInvariant() : "days";

        switch (unit)
        {
            case "days":
            {
                var diff = DateUtils.DateDiffDays(s1, s2);
                return diff.HasValue ? DynValue.Integer(diff.Value) : DynValue.Null();
            }
            case "months":
            {
                var dt1 = ParseDt(s1);
                var dt2 = ParseDt(s2);
                if (dt1 == null || dt2 == null) return DynValue.Null();
                int months = (dt2.Value.Year - dt1.Value.Year) * 12
                           + (dt2.Value.Month - dt1.Value.Month);
                return DynValue.Integer(months);
            }
            case "years":
            {
                var dt1 = ParseDt(s1);
                var dt2 = ParseDt(s2);
                if (dt1 == null || dt2 == null) return DynValue.Null();
                return DynValue.Integer(dt2.Value.Year - dt1.Value.Year);
            }
            default:
                // T011: Incompatible conversion — unknown unit
                ctx.Errors.Add(new TransformError
                {
                    Code = TransformErrorCode.IncompatibleConversion.Code(),
                    Message = $"Incompatible conversion in 'dateDiff': unknown unit '{unit}' (expected 'days', 'months', or 'years')"
                });
                return DynValue.Null();
        }
    }

    /// <summary>
    /// Adds N hours to a timestamp. args[0]=timestamp, args[1]=hours.
    /// </summary>
    private static DynValue AddHours(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2) return DynValue.Null();
        var s = ExtractDateStr(args[0]);
        var hours = ExtractInt(args[1]);
        if (s == null || !hours.HasValue) return DynValue.Null();

        var dt = ParseDt(s);
        if (dt == null) return DynValue.Null();

        var result = dt.Value.AddHours(hours.Value);
        return DynValue.Timestamp(FormatAsTimestamp(result));
    }

    /// <summary>
    /// Adds N minutes to a timestamp. args[0]=timestamp, args[1]=minutes.
    /// </summary>
    private static DynValue AddMinutes(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2) return DynValue.Null();
        var s = ExtractDateStr(args[0]);
        var minutes = ExtractInt(args[1]);
        if (s == null || !minutes.HasValue) return DynValue.Null();

        var dt = ParseDt(s);
        if (dt == null) return DynValue.Null();

        var result = dt.Value.AddMinutes(minutes.Value);
        return DynValue.Timestamp(FormatAsTimestamp(result));
    }

    /// <summary>
    /// Adds N seconds to a timestamp. args[0]=timestamp, args[1]=seconds.
    /// </summary>
    private static DynValue AddSeconds(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2) return DynValue.Null();
        var s = ExtractDateStr(args[0]);
        var seconds = ExtractInt(args[1]);
        if (s == null || !seconds.HasValue) return DynValue.Null();

        var dt = ParseDt(s);
        if (dt == null) return DynValue.Null();

        var result = dt.Value.AddSeconds(seconds.Value);
        return DynValue.Timestamp(FormatAsTimestamp(result));
    }

    /// <summary>
    /// Returns the start of day (midnight) for a date/timestamp. args[0]=date.
    /// </summary>
    private static DynValue StartOfDay(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var s = ExtractDateStr(args[0]);
        if (s == null) return DynValue.Null();

        var dt = ParseDt(s);
        if (dt == null) return DynValue.Null();

        var result = new DateTime(dt.Value.Year, dt.Value.Month, dt.Value.Day, 0, 0, 0, DateTimeKind.Utc);
        return DynValue.Timestamp(FormatAsTimestamp(result));
    }

    /// <summary>
    /// Returns the end of day (23:59:59.999) for a date/timestamp. args[0]=date.
    /// </summary>
    private static DynValue EndOfDay(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var s = ExtractDateStr(args[0]);
        if (s == null) return DynValue.Null();

        var dt = ParseDt(s);
        if (dt == null) return DynValue.Null();

        var result = new DateTime(dt.Value.Year, dt.Value.Month, dt.Value.Day, 23, 59, 59, 999, DateTimeKind.Utc);
        return DynValue.Timestamp(FormatAsTimestamp(result));
    }

    /// <summary>
    /// Returns the first day of the month for a date. args[0]=date.
    /// </summary>
    private static DynValue StartOfMonth(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var s = ExtractDateStr(args[0]);
        if (s == null) return DynValue.Null();

        var dt = ParseDt(s);
        if (dt == null) return DynValue.Null();

        var result = new DateTime(dt.Value.Year, dt.Value.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        return DynValue.Date(FormatAsDate(result));
    }

    /// <summary>
    /// Returns the last day of the month for a date. args[0]=date.
    /// </summary>
    private static DynValue EndOfMonth(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var s = ExtractDateStr(args[0]);
        if (s == null) return DynValue.Null();

        var dt = ParseDt(s);
        if (dt == null) return DynValue.Null();

        int lastDay = DateUtils.DaysInMonth(dt.Value.Year, dt.Value.Month);
        var result = new DateTime(dt.Value.Year, dt.Value.Month, lastDay, 0, 0, 0, DateTimeKind.Utc);
        return DynValue.Date(FormatAsDate(result));
    }

    /// <summary>
    /// Returns January 1st of the year for a date. args[0]=date.
    /// </summary>
    private static DynValue StartOfYear(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var s = ExtractDateStr(args[0]);
        if (s == null) return DynValue.Null();

        var dt = ParseDt(s);
        if (dt == null) return DynValue.Null();

        var result = new DateTime(dt.Value.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return DynValue.Date(FormatAsDate(result));
    }

    /// <summary>
    /// Returns December 31st of the year for a date. args[0]=date.
    /// </summary>
    private static DynValue EndOfYear(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var s = ExtractDateStr(args[0]);
        if (s == null) return DynValue.Null();

        var dt = ParseDt(s);
        if (dt == null) return DynValue.Null();

        var result = new DateTime(dt.Value.Year, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        return DynValue.Date(FormatAsDate(result));
    }

    /// <summary>
    /// Returns the day of week (0=Sunday through 6=Saturday). args[0]=date.
    /// </summary>
    private static DynValue DayOfWeek(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var s = ExtractDateStr(args[0]);
        if (s == null) return DynValue.Null();

        var dt = ParseDt(s);
        if (dt == null) return DynValue.Null();

        return DynValue.Integer((long)dt.Value.DayOfWeek);
    }

    /// <summary>
    /// Returns the ISO week number of the year (1-53). args[0]=date.
    /// </summary>
    private static DynValue WeekOfYear(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var s = ExtractDateStr(args[0]);
        if (s == null) return DynValue.Null();

        var dt = ParseDt(s);
        if (dt == null) return DynValue.Null();

        var cal = CultureInfo.InvariantCulture.Calendar;
        int week = cal.GetWeekOfYear(dt.Value, CalendarWeekRule.FirstFourDayWeek, System.DayOfWeek.Monday);
        return DynValue.Integer(week);
    }

    /// <summary>
    /// Returns the quarter (1-4) for a date. args[0]=date.
    /// </summary>
    private static DynValue Quarter(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var s = ExtractDateStr(args[0]);
        if (s == null) return DynValue.Null();

        var dt = ParseDt(s);
        if (dt == null) return DynValue.Null();

        int quarter = (dt.Value.Month - 1) / 3 + 1;
        return DynValue.Integer(quarter);
    }

    /// <summary>
    /// Returns true if the year of a date is a leap year. args[0]=date or year number.
    /// </summary>
    private static DynValue IsLeapYear(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();

        // If it's a number, treat it as a year directly
        var yearNum = ExtractInt(args[0]);
        if (yearNum.HasValue)
            return DynValue.Bool(DateUtils.IsLeapYear(yearNum.Value));

        // Otherwise parse as a date string
        var s = ExtractDateStr(args[0]);
        if (s == null) return DynValue.Null();

        var dt = ParseDt(s);
        if (dt == null) return DynValue.Null();

        return DynValue.Bool(DateUtils.IsLeapYear(dt.Value.Year));
    }

    /// <summary>
    /// Returns true if date1 is before date2. args[0]=date1, args[1]=date2.
    /// </summary>
    private static DynValue IsBefore(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2) return DynValue.Null();
        var s1 = ExtractDateStr(args[0]);
        var s2 = ExtractDateStr(args[1]);
        if (s1 == null || s2 == null) return DynValue.Null();

        var dt1 = ParseDt(s1);
        var dt2 = ParseDt(s2);
        if (dt1 == null || dt2 == null) return DynValue.Null();

        return DynValue.Bool(dt1.Value < dt2.Value);
    }

    /// <summary>
    /// Returns true if date1 is after date2. args[0]=date1, args[1]=date2.
    /// </summary>
    private static DynValue IsAfter(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2) return DynValue.Null();
        var s1 = ExtractDateStr(args[0]);
        var s2 = ExtractDateStr(args[1]);
        if (s1 == null || s2 == null) return DynValue.Null();

        var dt1 = ParseDt(s1);
        var dt2 = ParseDt(s2);
        if (dt1 == null || dt2 == null) return DynValue.Null();

        return DynValue.Bool(dt1.Value > dt2.Value);
    }

    /// <summary>
    /// Returns true if date is between start and end (inclusive). args[0]=date, args[1]=start, args[2]=end.
    /// </summary>
    private static DynValue IsBetween(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 3) return DynValue.Null();
        var s = ExtractDateStr(args[0]);
        var sStart = ExtractDateStr(args[1]);
        var sEnd = ExtractDateStr(args[2]);
        if (s == null || sStart == null || sEnd == null) return DynValue.Null();

        var dt = ParseDt(s);
        var dtStart = ParseDt(sStart);
        var dtEnd = ParseDt(sEnd);
        if (dt == null || dtStart == null || dtEnd == null) return DynValue.Null();

        return DynValue.Bool(dt.Value >= dtStart.Value && dt.Value <= dtEnd.Value);
    }

    /// <summary>
    /// Converts a date or timestamp to Unix epoch seconds. args[0]=date/timestamp.
    /// </summary>
    private static DynValue ToUnix(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var s = ExtractDateStr(args[0]);
        if (s == null) return DynValue.Null();

        var dt = ParseDt(s);
        if (dt == null) return DynValue.Null();

        long epochSeconds = (long)(dt.Value - UnixEpoch).TotalSeconds;
        return DynValue.Integer(epochSeconds);
    }

    /// <summary>
    /// Converts Unix epoch seconds to an ISO 8601 timestamp string. args[0]=epoch seconds.
    /// </summary>
    private static DynValue FromUnix(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var seconds = ExtractLong(args[0]);
        if (!seconds.HasValue) return DynValue.Null();

        var dt = UnixEpoch.AddSeconds(seconds.Value);
        return DynValue.Timestamp(FormatAsTimestamp(dt));
    }

    /// <summary>
    /// Returns the absolute difference in days between two dates. args[0]=date1, args[1]=date2.
    /// </summary>
    private static DynValue DaysBetweenDates(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2) return DynValue.Null();
        var s1 = ExtractDateStr(args[0]);
        var s2 = ExtractDateStr(args[1]);
        if (s1 == null || s2 == null) return DynValue.Null();

        var diff = DateUtils.DateDiffDays(s1, s2);
        return diff.HasValue ? DynValue.Integer(Math.Abs(diff.Value)) : DynValue.Null();
    }

    /// <summary>
    /// Calculates age in whole years from a birth date to today. args[0]=birthDate.
    /// </summary>
    private static DynValue AgeFromDate(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var s = ExtractDateStr(args[0]);
        if (s == null) return DynValue.Null();

        var birthDt = ParseDt(s);
        if (birthDt == null) return DynValue.Null();

        var now = DateTime.UtcNow;
        int age = now.Year - birthDt.Value.Year;
        // Adjust if birthday hasn't occurred yet this year
        if (now.Month < birthDt.Value.Month ||
            (now.Month == birthDt.Value.Month && now.Day < birthDt.Value.Day))
        {
            age--;
        }
        return DynValue.Integer(age);
    }

    /// <summary>
    /// Returns true if the argument is a valid date string. args[0]=date string.
    /// </summary>
    private static DynValue IsValidDate(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Bool(false);
        var s = ExtractDateStr(args[0]);
        if (s == null) return DynValue.Bool(false);

        var parsed = DateUtils.ParseDate(s);
        if (!parsed.HasValue) return DynValue.Bool(false);

        var (year, month, day) = parsed.Value;
        return DynValue.Bool(DateUtils.IsValidDate(year, month, day));
    }

    /// <summary>
    /// Formats a date with locale-aware formatting. args[0]=date, args[1]=locale (e.g., "en-US"),
    /// args[2]=optional format ("short", "long", or custom pattern).
    /// </summary>
    private static DynValue FormatLocaleDate(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2) return DynValue.Null();
        var dateStr = ExtractDateStr(args[0]);
        var locale = args[1].AsString();
        if (dateStr == null || locale == null) return DynValue.Null();

        var dt = ParseDt(dateStr);
        if (dt == null) return DynValue.Null();

        CultureInfo culture;
        try
        {
            culture = new CultureInfo(locale);
        }
        catch
        {
            culture = CultureInfo.InvariantCulture;
        }

        string format = "d"; // default short date
        if (args.Length >= 3)
        {
            var fmtArg = args[2].AsString();
            if (fmtArg != null)
            {
                if (fmtArg == "short") format = "d";
                else if (fmtArg == "long") format = "D";
                else format = fmtArg;
            }
        }

        try
        {
            return DynValue.String(dt.Value.ToString(format, culture));
        }
        catch
        {
            return DynValue.String(FormatAsDate(dt.Value));
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // BusinessDays / NextBusinessDay / FormatDuration
    // ─────────────────────────────────────────────────────────────────────────

    private static DynValue BusinessDays(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2) return DynValue.Null();

        var dateStr = ExtractDateStr(args[0]);
        var dt = ParseDt(dateStr);
        if (!dt.HasValue) return DynValue.Null();

        var countLong = ExtractLong(args[1]);
        if (!countLong.HasValue) return DynValue.Null();
        int count = (int)countLong.Value;

        var date = dt.Value.Date;
        int direction = count >= 0 ? 1 : -1;
        int absCount = Math.Abs(count);
        int fullWeeks = absCount / 5;
        int remaining = absCount % 5;

        // O(1): 5 business days == 7 calendar days
        date = date.AddDays(direction * fullWeeks * 7);

        while (remaining > 0)
        {
            date = date.AddDays(direction);
            var dow = date.DayOfWeek;
            if (dow != System.DayOfWeek.Saturday && dow != System.DayOfWeek.Sunday)
                remaining--;
        }

        return DynValue.String(FormatAsDate(date));
    }

    private static DynValue NextBusinessDay(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 1) return DynValue.Null();

        var dateStr = ExtractDateStr(args[0]);
        var dt = ParseDt(dateStr);
        if (!dt.HasValue) return DynValue.Null();

        var date = dt.Value.Date;
        var dow = date.DayOfWeek;

        if (dow == System.DayOfWeek.Saturday)
            date = date.AddDays(2);
        else if (dow == System.DayOfWeek.Sunday)
            date = date.AddDays(1);

        return DynValue.String(FormatAsDate(date));
    }

    private static readonly System.Text.RegularExpressions.Regex DurationPattern =
        new System.Text.RegularExpressions.Regex(
            @"^P(?:(\d+)Y)?(?:(\d+)M)?(?:(\d+)D)?(?:T(?:(\d+)H)?(?:(\d+)M)?(?:(\d+(?:\.\d+)?)S)?)?$",
            System.Text.RegularExpressions.RegexOptions.Compiled);

    private static DynValue FormatDuration(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 1) return DynValue.Null();

        var input = args[0].AsString();
        if (string.IsNullOrEmpty(input)) return DynValue.Null();

        var match = DurationPattern.Match(input);
        if (!match.Success) return DynValue.Null();

        int years = match.Groups[1].Success ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
        int months = match.Groups[2].Success ? int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) : 0;
        int days = match.Groups[3].Success ? int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture) : 0;
        int hours = match.Groups[4].Success ? int.Parse(match.Groups[4].Value, CultureInfo.InvariantCulture) : 0;
        int minutes = match.Groups[5].Success ? int.Parse(match.Groups[5].Value, CultureInfo.InvariantCulture) : 0;
        double seconds = match.Groups[6].Success ? double.Parse(match.Groups[6].Value, CultureInfo.InvariantCulture) : 0.0;

        var parts = new List<string>();
        if (years > 0) parts.Add($"{years} {(years == 1 ? "year" : "years")}");
        if (months > 0) parts.Add($"{months} {(months == 1 ? "month" : "months")}");
        if (days > 0) parts.Add($"{days} {(days == 1 ? "day" : "days")}");
        if (hours > 0) parts.Add($"{hours} {(hours == 1 ? "hour" : "hours")}");
        if (minutes > 0) parts.Add($"{minutes} {(minutes == 1 ? "minute" : "minutes")}");
        if (seconds > 0)
        {
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            var secStr = (seconds == Math.Floor(seconds))
                ? ((int)seconds).ToString(CultureInfo.InvariantCulture)
                : seconds.ToString("F1", CultureInfo.InvariantCulture);
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            parts.Add($"{secStr} {(seconds == 1.0 ? "second" : "seconds")}");
        }

        if (parts.Count == 0) return DynValue.String("0 seconds");

        return DynValue.String(string.Join(", ", parts));
    }
}
