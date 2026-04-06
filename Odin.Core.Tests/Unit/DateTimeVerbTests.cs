using System;
using System.Collections.Generic;
using Odin.Core.Types;
using Odin.Core.Transform;
using Odin.Core.Transform.Verbs;
using Xunit;

namespace Odin.Core.Tests.Unit;

/// <summary>
/// Tests for datetime, generation, and geo verbs.
/// Ported from Rust SDK collection_verbs.rs tests.
/// </summary>
public class DateTimeVerbTests
{
    private readonly VerbRegistry _registry = new VerbRegistry();
    private readonly VerbContext _ctx = new VerbContext();

    private DynValue Invoke(string verb, params DynValue[] args)
        => _registry.Invoke(verb, args, _ctx);

    private static DynValue S(string v) => DynValue.String(v);
    private static DynValue I(long v) => DynValue.Integer(v);
    private static DynValue F(double v) => DynValue.Float(v);
    private static DynValue B(bool v) => DynValue.Bool(v);
    private static DynValue Null() => DynValue.Null();
    private static DynValue D(string v) => DynValue.Date(v);
    private static DynValue TS(string v) => DynValue.Timestamp(v);

    // =========================================================================
    // today / now
    // =========================================================================

    [Fact]
    public void Today_ReturnsDateString()
    {
        var result = Invoke("today");
        var s = result.AsString();
        Assert.NotNull(s);
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", s!);
    }

    [Fact]
    public void Now_ReturnsTimestampString()
    {
        var result = Invoke("now");
        var s = result.AsString();
        Assert.NotNull(s);
        Assert.Contains("T", s!);
    }

    // =========================================================================
    // formatDate
    // =========================================================================

    [Fact]
    public void FormatDate_BasicPattern()
    {
        var result = Invoke("formatDate", D("2024-03-15"), S("YYYY/MM/DD"));
        Assert.Equal("2024/03/15", result.AsString());
    }

    [Fact]
    public void FormatDate_YearOnly()
    {
        var result = Invoke("formatDate", D("2024-06-15"), S("YYYY"));
        Assert.Equal("2024", result.AsString());
    }

    [Fact]
    public void FormatDate_MonthDay()
    {
        var result = Invoke("formatDate", D("2024-01-05"), S("MM-DD"));
        Assert.Equal("01-05", result.AsString());
    }

    [Fact]
    public void FormatDate_NullInput()
    {
        Assert.True(Invoke("formatDate", Null(), S("YYYY-MM-DD")).IsNull);
    }

    [Fact]
    public void FormatDate_NullPattern()
    {
        Assert.True(Invoke("formatDate", D("2024-01-01"), Null()).IsNull);
    }

    // =========================================================================
    // parseDate
    // =========================================================================

    [Fact]
    public void ParseDate_BasicPattern()
    {
        var result = Invoke("parseDate", S("2024-03-15"), S("YYYY-MM-DD"));
        Assert.Equal("2024-03-15", result.AsString());
    }

    [Fact]
    public void ParseDate_SlashPattern()
    {
        var result = Invoke("parseDate", S("15/03/2024"), S("DD/MM/YYYY"));
        Assert.Equal("2024-03-15", result.AsString());
    }

    [Fact]
    public void ParseDate_NullInput()
    {
        Assert.True(Invoke("parseDate", Null(), S("YYYY-MM-DD")).IsNull);
    }

    // =========================================================================
    // formatTime
    // =========================================================================

    [Fact]
    public void FormatTime_FromTimestamp()
    {
        var result = Invoke("formatTime", TS("2024-03-15T14:30:00.000Z"));
        Assert.Equal("14:30:00", result.AsString());
    }

    [Fact]
    public void FormatTime_NullInput()
    {
        Assert.True(Invoke("formatTime", Null()).IsNull);
    }

    // =========================================================================
    // formatTimestamp
    // =========================================================================

    [Fact]
    public void FormatTimestamp_BasicPattern()
    {
        var result = Invoke("formatTimestamp", TS("2024-03-15T14:30:00.000Z"), S("YYYY-MM-DD HH:mm:ss"));
        Assert.Equal("2024-03-15 14:30:00", result.AsString());
    }

    [Fact]
    public void FormatTimestamp_DefaultIso()
    {
        var result = Invoke("formatTimestamp", TS("2024-03-15T14:30:00.000Z"));
        Assert.Contains("2024-03-15", result.AsString()!);
        Assert.Contains("T", result.AsString()!);
    }

    [Fact]
    public void FormatTimestamp_NullInput()
    {
        Assert.True(Invoke("formatTimestamp", Null()).IsNull);
    }

    // =========================================================================
    // parseTimestamp
    // =========================================================================

    [Fact]
    public void ParseTimestamp_IsoFormat()
    {
        var result = Invoke("parseTimestamp", S("2024-03-15T14:30:00Z"));
        var s = result.AsString();
        Assert.NotNull(s);
        Assert.Contains("2024-03-15", s!);
    }

    [Fact]
    public void ParseTimestamp_NullInput()
    {
        Assert.True(Invoke("parseTimestamp", Null()).IsNull);
    }

    // =========================================================================
    // addDays
    // =========================================================================

    [Fact]
    public void AddDays_Positive()
    {
        var result = Invoke("addDays", D("2024-01-01"), I(10));
        Assert.Equal("2024-01-11", result.AsString());
    }

    [Fact]
    public void AddDays_Negative()
    {
        var result = Invoke("addDays", D("2024-01-11"), I(-10));
        Assert.Equal("2024-01-01", result.AsString());
    }

    [Fact]
    public void AddDays_CrossMonth()
    {
        var result = Invoke("addDays", D("2024-01-30"), I(5));
        Assert.Equal("2024-02-04", result.AsString());
    }

    [Fact]
    public void AddDays_Zero()
    {
        var result = Invoke("addDays", D("2024-03-15"), I(0));
        Assert.Equal("2024-03-15", result.AsString());
    }

    [Fact]
    public void AddDays_NullDate()
    {
        Assert.True(Invoke("addDays", Null(), I(5)).IsNull);
    }

    [Fact]
    public void AddDays_CrossYear()
    {
        var result = Invoke("addDays", D("2024-12-30"), I(5));
        Assert.Equal("2025-01-04", result.AsString());
    }

    // =========================================================================
    // addMonths
    // =========================================================================

    [Fact]
    public void AddMonths_Positive()
    {
        var result = Invoke("addMonths", D("2024-01-15"), I(3));
        Assert.Equal("2024-04-15", result.AsString());
    }

    [Fact]
    public void AddMonths_Negative()
    {
        var result = Invoke("addMonths", D("2024-04-15"), I(-3));
        Assert.Equal("2024-01-15", result.AsString());
    }

    [Fact]
    public void AddMonths_CrossYear()
    {
        var result = Invoke("addMonths", D("2024-11-15"), I(3));
        Assert.Equal("2025-02-15", result.AsString());
    }

    [Fact]
    public void AddMonths_NullDate()
    {
        Assert.True(Invoke("addMonths", Null(), I(1)).IsNull);
    }

    // =========================================================================
    // addYears
    // =========================================================================

    [Fact]
    public void AddYears_Positive()
    {
        var result = Invoke("addYears", D("2024-03-15"), I(2));
        Assert.Equal("2026-03-15", result.AsString());
    }

    [Fact]
    public void AddYears_Negative()
    {
        var result = Invoke("addYears", D("2024-03-15"), I(-1));
        Assert.Equal("2023-03-15", result.AsString());
    }

    [Fact]
    public void AddYears_NullDate()
    {
        Assert.True(Invoke("addYears", Null(), I(1)).IsNull);
    }

    // =========================================================================
    // dateDiff
    // =========================================================================

    [Fact]
    public void DateDiff_SameDate()
    {
        Assert.Equal(0, Invoke("dateDiff", D("2024-01-01"), D("2024-01-01")).AsInt64());
    }

    [Fact]
    public void DateDiff_PositiveDiff()
    {
        Assert.Equal(10, Invoke("dateDiff", D("2024-01-01"), D("2024-01-11")).AsInt64());
    }

    [Fact]
    public void DateDiff_NegativeDiff()
    {
        Assert.Equal(-10, Invoke("dateDiff", D("2024-01-11"), D("2024-01-01")).AsInt64());
    }

    [Fact]
    public void DateDiff_CrossYear()
    {
        var result = Invoke("dateDiff", D("2023-12-31"), D("2024-01-01"));
        Assert.Equal(1, result.AsInt64());
    }

    [Fact]
    public void DateDiff_NullInput()
    {
        Assert.True(Invoke("dateDiff", Null(), D("2024-01-01")).IsNull);
    }

    // =========================================================================
    // addHours / addMinutes / addSeconds
    // =========================================================================

    [Fact]
    public void AddHours_Positive()
    {
        var result = Invoke("addHours", TS("2024-01-01T00:00:00.000Z"), I(5));
        Assert.Contains("05:00:00", result.AsString()!);
    }

    [Fact]
    public void AddHours_NullInput()
    {
        Assert.True(Invoke("addHours", Null(), I(1)).IsNull);
    }

    [Fact]
    public void AddMinutes_Positive()
    {
        var result = Invoke("addMinutes", TS("2024-01-01T00:00:00.000Z"), I(90));
        Assert.Contains("01:30:00", result.AsString()!);
    }

    [Fact]
    public void AddMinutes_NullInput()
    {
        Assert.True(Invoke("addMinutes", Null(), I(30)).IsNull);
    }

    [Fact]
    public void AddSeconds_Positive()
    {
        var result = Invoke("addSeconds", TS("2024-01-01T00:00:00.000Z"), I(3661));
        Assert.Contains("01:01:01", result.AsString()!);
    }

    [Fact]
    public void AddSeconds_NullInput()
    {
        Assert.True(Invoke("addSeconds", Null(), I(10)).IsNull);
    }

    // =========================================================================
    // startOfDay / endOfDay
    // =========================================================================

    [Fact]
    public void StartOfDay_ReturnsTimestamp()
    {
        var result = Invoke("startOfDay", D("2024-03-15"));
        Assert.Contains("00:00:00", result.AsString()!);
    }

    [Fact]
    public void StartOfDay_NullInput()
    {
        Assert.True(Invoke("startOfDay", Null()).IsNull);
    }

    [Fact]
    public void EndOfDay_ReturnsTimestamp()
    {
        var result = Invoke("endOfDay", D("2024-03-15"));
        Assert.Contains("23:59:59", result.AsString()!);
    }

    [Fact]
    public void EndOfDay_NullInput()
    {
        Assert.True(Invoke("endOfDay", Null()).IsNull);
    }

    // =========================================================================
    // startOfMonth / endOfMonth
    // =========================================================================

    [Fact]
    public void StartOfMonth_ReturnsFirstDay()
    {
        var result = Invoke("startOfMonth", D("2024-03-15"));
        Assert.Equal("2024-03-01", result.AsString());
    }

    [Fact]
    public void StartOfMonth_AlreadyFirstDay()
    {
        Assert.Equal("2024-01-01", Invoke("startOfMonth", D("2024-01-01")).AsString());
    }

    [Fact]
    public void StartOfMonth_NullInput()
    {
        Assert.True(Invoke("startOfMonth", Null()).IsNull);
    }

    [Fact]
    public void EndOfMonth_March()
    {
        Assert.Equal("2024-03-31", Invoke("endOfMonth", D("2024-03-15")).AsString());
    }

    [Fact]
    public void EndOfMonth_February_LeapYear()
    {
        Assert.Equal("2024-02-29", Invoke("endOfMonth", D("2024-02-10")).AsString());
    }

    [Fact]
    public void EndOfMonth_February_NonLeapYear()
    {
        Assert.Equal("2023-02-28", Invoke("endOfMonth", D("2023-02-10")).AsString());
    }

    [Fact]
    public void EndOfMonth_NullInput()
    {
        Assert.True(Invoke("endOfMonth", Null()).IsNull);
    }

    // =========================================================================
    // startOfYear / endOfYear
    // =========================================================================

    [Fact]
    public void StartOfYear_ReturnsJanFirst()
    {
        Assert.Equal("2024-01-01", Invoke("startOfYear", D("2024-06-15")).AsString());
    }

    [Fact]
    public void StartOfYear_NullInput()
    {
        Assert.True(Invoke("startOfYear", Null()).IsNull);
    }

    [Fact]
    public void EndOfYear_ReturnsDecThirtyFirst()
    {
        Assert.Equal("2024-12-31", Invoke("endOfYear", D("2024-06-15")).AsString());
    }

    [Fact]
    public void EndOfYear_NullInput()
    {
        Assert.True(Invoke("endOfYear", Null()).IsNull);
    }

    // =========================================================================
    // dayOfWeek
    // =========================================================================

    [Fact]
    public void DayOfWeek_Monday()
    {
        // 2024-01-01 is Monday
        var result = Invoke("dayOfWeek", D("2024-01-01"));
        Assert.Equal(1, result.AsInt64()); // Monday = 1 in .NET DayOfWeek
    }

    [Fact]
    public void DayOfWeek_Sunday()
    {
        // 2024-01-07 is Sunday
        Assert.Equal(0, Invoke("dayOfWeek", D("2024-01-07")).AsInt64()); // Sunday = 0
    }

    [Fact]
    public void DayOfWeek_NullInput()
    {
        Assert.True(Invoke("dayOfWeek", Null()).IsNull);
    }

    // =========================================================================
    // weekOfYear
    // =========================================================================

    [Fact]
    public void WeekOfYear_JanFirst()
    {
        var result = Invoke("weekOfYear", D("2024-01-01"));
        Assert.True(result.AsInt64() >= 1);
    }

    [Fact]
    public void WeekOfYear_MidYear()
    {
        var result = Invoke("weekOfYear", D("2024-06-15"));
        Assert.True(result.AsInt64() > 20 && result.AsInt64() < 30);
    }

    [Fact]
    public void WeekOfYear_NullInput()
    {
        Assert.True(Invoke("weekOfYear", Null()).IsNull);
    }

    // =========================================================================
    // quarter
    // =========================================================================

    [Fact]
    public void Quarter_Q1()
    {
        Assert.Equal(1, Invoke("quarter", D("2024-01-15")).AsInt64());
        Assert.Equal(1, Invoke("quarter", D("2024-03-31")).AsInt64());
    }

    [Fact]
    public void Quarter_Q2()
    {
        Assert.Equal(2, Invoke("quarter", D("2024-04-01")).AsInt64());
        Assert.Equal(2, Invoke("quarter", D("2024-06-30")).AsInt64());
    }

    [Fact]
    public void Quarter_Q3()
    {
        Assert.Equal(3, Invoke("quarter", D("2024-07-01")).AsInt64());
    }

    [Fact]
    public void Quarter_Q4()
    {
        Assert.Equal(4, Invoke("quarter", D("2024-12-31")).AsInt64());
    }

    [Fact]
    public void Quarter_NullInput()
    {
        Assert.True(Invoke("quarter", Null()).IsNull);
    }

    // =========================================================================
    // isLeapYear
    // =========================================================================

    [Fact]
    public void IsLeapYear_2024()
    {
        Assert.True(Invoke("isLeapYear", I(2024)).AsBool());
    }

    [Fact]
    public void IsLeapYear_2023()
    {
        Assert.False(Invoke("isLeapYear", I(2023)).AsBool());
    }

    [Fact]
    public void IsLeapYear_2000()
    {
        Assert.True(Invoke("isLeapYear", I(2000)).AsBool());
    }

    [Fact]
    public void IsLeapYear_1900()
    {
        Assert.False(Invoke("isLeapYear", I(1900)).AsBool());
    }

    [Fact]
    public void IsLeapYear_FromDate()
    {
        Assert.True(Invoke("isLeapYear", D("2024-06-15")).AsBool());
    }

    // =========================================================================
    // isBefore / isAfter / isBetween
    // =========================================================================

    [Fact]
    public void IsBefore_True()
    {
        Assert.True(Invoke("isBefore", D("2024-01-01"), D("2024-01-02")).AsBool());
    }

    [Fact]
    public void IsBefore_False()
    {
        Assert.False(Invoke("isBefore", D("2024-01-02"), D("2024-01-01")).AsBool());
    }

    [Fact]
    public void IsBefore_Equal()
    {
        Assert.False(Invoke("isBefore", D("2024-01-01"), D("2024-01-01")).AsBool());
    }

    [Fact]
    public void IsBefore_NullInput()
    {
        Assert.True(Invoke("isBefore", Null(), D("2024-01-01")).IsNull);
    }

    [Fact]
    public void IsAfter_True()
    {
        Assert.True(Invoke("isAfter", D("2024-01-02"), D("2024-01-01")).AsBool());
    }

    [Fact]
    public void IsAfter_False()
    {
        Assert.False(Invoke("isAfter", D("2024-01-01"), D("2024-01-02")).AsBool());
    }

    [Fact]
    public void IsAfter_Equal()
    {
        Assert.False(Invoke("isAfter", D("2024-01-01"), D("2024-01-01")).AsBool());
    }

    [Fact]
    public void IsAfter_NullInput()
    {
        Assert.True(Invoke("isAfter", Null(), D("2024-01-01")).IsNull);
    }

    [Fact]
    public void IsBetween_Inside()
    {
        Assert.True(Invoke("isBetween", D("2024-06-15"), D("2024-01-01"), D("2024-12-31")).AsBool());
    }

    [Fact]
    public void IsBetween_OnStart()
    {
        Assert.True(Invoke("isBetween", D("2024-01-01"), D("2024-01-01"), D("2024-12-31")).AsBool());
    }

    [Fact]
    public void IsBetween_OnEnd()
    {
        Assert.True(Invoke("isBetween", D("2024-12-31"), D("2024-01-01"), D("2024-12-31")).AsBool());
    }

    [Fact]
    public void IsBetween_Outside()
    {
        Assert.False(Invoke("isBetween", D("2025-01-01"), D("2024-01-01"), D("2024-12-31")).AsBool());
    }

    [Fact]
    public void IsBetween_NullInput()
    {
        Assert.True(Invoke("isBetween", Null(), D("2024-01-01"), D("2024-12-31")).IsNull);
    }

    // =========================================================================
    // toUnix / fromUnix
    // =========================================================================

    [Fact]
    public void ToUnix_Epoch()
    {
        Assert.Equal(0, Invoke("toUnix", TS("1970-01-01T00:00:00.000Z")).AsInt64());
    }

    [Fact]
    public void ToUnix_KnownDate()
    {
        var result = Invoke("toUnix", D("2024-01-01"));
        Assert.True(result.AsInt64() > 0);
    }

    [Fact]
    public void ToUnix_NullInput()
    {
        Assert.True(Invoke("toUnix", Null()).IsNull);
    }

    [Fact]
    public void FromUnix_Epoch()
    {
        var result = Invoke("fromUnix", I(0));
        Assert.Contains("1970-01-01", result.AsString()!);
    }

    [Fact]
    public void FromUnix_KnownTimestamp()
    {
        // 1704067200 = 2024-01-01T00:00:00Z
        var result = Invoke("fromUnix", I(1704067200));
        Assert.Contains("2024-01-01", result.AsString()!);
    }

    [Fact]
    public void ToUnix_FromUnix_Roundtrip()
    {
        var ts = TS("2024-06-15T12:30:00.000Z");
        var unix = Invoke("toUnix", ts);
        var back = Invoke("fromUnix", unix);
        Assert.Contains("2024-06-15", back.AsString()!);
    }

    // =========================================================================
    // daysBetweenDates
    // =========================================================================

    [Fact]
    public void DaysBetweenDates_SameDate()
    {
        Assert.Equal(0, Invoke("daysBetweenDates", D("2024-01-01"), D("2024-01-01")).AsInt64());
    }

    [Fact]
    public void DaysBetweenDates_Positive()
    {
        Assert.Equal(10, Invoke("daysBetweenDates", D("2024-01-01"), D("2024-01-11")).AsInt64());
    }

    [Fact]
    public void DaysBetweenDates_ReversedIsAbsolute()
    {
        Assert.Equal(10, Invoke("daysBetweenDates", D("2024-01-11"), D("2024-01-01")).AsInt64());
    }

    [Fact]
    public void DaysBetweenDates_NullInput()
    {
        Assert.True(Invoke("daysBetweenDates", Null(), D("2024-01-01")).IsNull);
    }

    // =========================================================================
    // isValidDate
    // =========================================================================

    [Fact]
    public void IsValidDate_Valid()
    {
        Assert.True(Invoke("isValidDate", S("2024-03-15")).AsBool());
    }

    [Fact]
    public void IsValidDate_Invalid()
    {
        Assert.False(Invoke("isValidDate", S("not-a-date")).AsBool());
    }

    [Fact]
    public void IsValidDate_Feb29_LeapYear()
    {
        Assert.True(Invoke("isValidDate", S("2024-02-29")).AsBool());
    }

    [Fact]
    public void IsValidDate_Feb29_NonLeapYear()
    {
        Assert.False(Invoke("isValidDate", S("2023-02-29")).AsBool());
    }

    [Fact]
    public void IsValidDate_InvalidMonth()
    {
        Assert.False(Invoke("isValidDate", S("2024-13-01")).AsBool());
    }

    [Fact]
    public void IsValidDate_InvalidDay()
    {
        Assert.False(Invoke("isValidDate", S("2024-01-32")).AsBool());
    }

    [Fact]
    public void IsValidDate_NullInput()
    {
        Assert.False(Invoke("isValidDate", Null()).AsBool());
    }

    // =========================================================================
    // formatLocaleDate
    // =========================================================================

    [Fact]
    public void FormatLocaleDate_EnUs()
    {
        var result = Invoke("formatLocaleDate", D("2024-03-15"), S("en-US"));
        Assert.NotNull(result.AsString());
        Assert.NotEmpty(result.AsString()!);
    }

    [Fact]
    public void FormatLocaleDate_NullDate()
    {
        Assert.True(Invoke("formatLocaleDate", Null(), S("en-US")).IsNull);
    }

    [Fact]
    public void FormatLocaleDate_NullLocale()
    {
        Assert.True(Invoke("formatLocaleDate", D("2024-03-15"), Null()).IsNull);
    }

    // =========================================================================
    // uuid
    // =========================================================================

    [Fact]
    public void Uuid_ReturnsString()
    {
        var result = Invoke("uuid");
        var s = result.AsString();
        Assert.NotNull(s);
        Assert.Equal(36, s!.Length); // UUID format: 8-4-4-4-12
    }

    [Fact]
    public void Uuid_HasHyphens()
    {
        var result = Invoke("uuid").AsString()!;
        Assert.Contains("-", result);
        var parts = result.Split('-');
        Assert.Equal(5, parts.Length);
    }

    [Fact]
    public void Uuid_TwoCallsAreDifferent()
    {
        var r1 = Invoke("uuid").AsString();
        var r2 = Invoke("uuid").AsString();
        Assert.NotEqual(r1, r2);
    }

    // =========================================================================
    // sequence / resetSequence
    // =========================================================================

    [Fact]
    public void Sequence_DefaultZero()
    {
        var ctx = new VerbContext();
        var result = _registry.Invoke("sequence", new[] { S("counter") }, ctx);
        Assert.Equal(0, result.AsInt64());
    }

    [Fact]
    public void Sequence_Increments()
    {
        var ctx = new VerbContext();
        var r1 = _registry.Invoke("sequence", new[] { S("counter") }, ctx);
        var r2 = _registry.Invoke("sequence", new[] { S("counter") }, ctx);
        var r3 = _registry.Invoke("sequence", new[] { S("counter") }, ctx);
        Assert.Equal(0, r1.AsInt64());
        Assert.Equal(1, r2.AsInt64());
        Assert.Equal(2, r3.AsInt64());
    }

    [Fact]
    public void Sequence_NamedCounters()
    {
        var ctx = new VerbContext();
        var a1 = _registry.Invoke("sequence", new[] { S("a") }, ctx);
        var b1 = _registry.Invoke("sequence", new[] { S("b") }, ctx);
        var a2 = _registry.Invoke("sequence", new[] { S("a") }, ctx);
        Assert.Equal(0, a1.AsInt64());
        Assert.Equal(0, b1.AsInt64());
        Assert.Equal(1, a2.AsInt64());
    }

    [Fact]
    public void ResetSequence_ResetsToZero()
    {
        var ctx = new VerbContext();
        _registry.Invoke("sequence", new[] { S("counter") }, ctx);
        _registry.Invoke("sequence", new[] { S("counter") }, ctx);
        _registry.Invoke("resetSequence", new[] { S("counter") }, ctx);
        var result = _registry.Invoke("sequence", new[] { S("counter") }, ctx);
        Assert.Equal(0, result.AsInt64());
    }

    [Fact]
    public void ResetSequence_ResetsToCustomValue()
    {
        var ctx = new VerbContext();
        _registry.Invoke("resetSequence", new[] { S("counter"), I(10) }, ctx);
        var result = _registry.Invoke("sequence", new[] { S("counter") }, ctx);
        Assert.Equal(10, result.AsInt64());
    }

    // =========================================================================
    // nanoid
    // =========================================================================

    [Fact]
    public void Nanoid_DefaultLength()
    {
        var result = Invoke("nanoid");
        Assert.Equal(21, result.AsString()!.Length);
    }

    [Fact]
    public void Nanoid_CustomLength()
    {
        var result = Invoke("nanoid", I(10));
        Assert.Equal(10, result.AsString()!.Length);
    }

    [Fact]
    public void Nanoid_TwoCallsAreDifferent()
    {
        var r1 = Invoke("nanoid").AsString();
        var r2 = Invoke("nanoid").AsString();
        Assert.NotEqual(r1, r2);
    }

    // =========================================================================
    // distance (Haversine)
    // =========================================================================

    [Fact]
    public void Distance_SamePointIsZero()
    {
        var result = Invoke("distance", F(40.0), F(-74.0), F(40.0), F(-74.0));
        Assert.True(Math.Abs(result.AsDouble()!.Value) < 0.001);
    }

    [Fact]
    public void Distance_NewYorkToLondon()
    {
        var result = Invoke("distance", F(40.7128), F(-74.0060), F(51.5074), F(-0.1278));
        var km = result.AsDouble()!.Value;
        Assert.True(km > 5000.0 && km < 6000.0); // ~5570 km
    }

    [Fact]
    public void Distance_NullInput()
    {
        Assert.True(Invoke("distance", Null(), F(0.0), F(0.0), F(0.0)).IsNull);
    }

    // =========================================================================
    // inBoundingBox
    // =========================================================================

    [Fact]
    public void InBoundingBox_Inside()
    {
        Assert.True(Invoke("inBoundingBox", F(5.0), F(5.0), F(0.0), F(0.0), F(10.0), F(10.0)).AsBool());
    }

    [Fact]
    public void InBoundingBox_Outside()
    {
        Assert.False(Invoke("inBoundingBox", F(15.0), F(5.0), F(0.0), F(0.0), F(10.0), F(10.0)).AsBool());
    }

    [Fact]
    public void InBoundingBox_OnEdge()
    {
        Assert.True(Invoke("inBoundingBox", F(0.0), F(0.0), F(0.0), F(0.0), F(10.0), F(10.0)).AsBool());
    }

    [Fact]
    public void InBoundingBox_NullInput()
    {
        Assert.True(Invoke("inBoundingBox", Null(), F(5.0), F(0.0), F(0.0), F(10.0), F(10.0)).IsNull);
    }

    // =========================================================================
    // toRadians / toDegrees
    // =========================================================================

    [Fact]
    public void ToRadians_180()
    {
        var result = Invoke("toRadians", F(180.0));
        Assert.True(Math.Abs(result.AsDouble()!.Value - Math.PI) < 1e-10);
    }

    [Fact]
    public void ToRadians_90()
    {
        var result = Invoke("toRadians", F(90.0));
        Assert.True(Math.Abs(result.AsDouble()!.Value - Math.PI / 2) < 1e-10);
    }

    [Fact]
    public void ToRadians_Zero()
    {
        var result = Invoke("toRadians", F(0.0));
        Assert.True(Math.Abs(result.AsDouble()!.Value) < 1e-10);
    }

    [Fact]
    public void ToRadians_IntegerInput()
    {
        var result = Invoke("toRadians", I(180));
        Assert.True(Math.Abs(result.AsDouble()!.Value - Math.PI) < 1e-10);
    }

    [Fact]
    public void ToDegrees_Pi()
    {
        var result = Invoke("toDegrees", F(Math.PI));
        Assert.True(Math.Abs(result.AsDouble()!.Value - 180.0) < 1e-10);
    }

    [Fact]
    public void ToDegrees_Zero()
    {
        var result = Invoke("toDegrees", F(0.0));
        Assert.True(Math.Abs(result.AsDouble()!.Value) < 1e-10);
    }

    // =========================================================================
    // bearing
    // =========================================================================

    [Fact]
    public void Bearing_North()
    {
        var result = Invoke("bearing", F(0.0), F(0.0), F(10.0), F(0.0));
        var deg = result.AsDouble()!.Value;
        Assert.True(deg < 1.0 || (deg - 360.0) > -1.0); // ~0 degrees
    }

    [Fact]
    public void Bearing_East()
    {
        var result = Invoke("bearing", F(0.0), F(0.0), F(0.0), F(10.0));
        Assert.True(Math.Abs(result.AsDouble()!.Value - 90.0) < 1.0);
    }

    [Fact]
    public void Bearing_NullInput()
    {
        Assert.True(Invoke("bearing", Null(), F(0.0), F(0.0), F(0.0)).IsNull);
    }

    // =========================================================================
    // midpoint
    // =========================================================================

    [Fact]
    public void Midpoint_SamePoint()
    {
        var result = Invoke("midpoint", F(40.0), F(-74.0), F(40.0), F(-74.0));
        var arr = result.AsArray()!;
        Assert.True(Math.Abs(arr[0].AsDouble()!.Value - 40.0) < 0.001);
        Assert.True(Math.Abs(arr[1].AsDouble()!.Value - (-74.0)) < 0.001);
    }

    [Fact]
    public void Midpoint_Equator()
    {
        var result = Invoke("midpoint", F(0.0), F(0.0), F(0.0), F(10.0));
        var arr = result.AsArray()!;
        Assert.True(Math.Abs(arr[0].AsDouble()!.Value) < 0.01); // lat ~0
        Assert.True(Math.Abs(arr[1].AsDouble()!.Value - 5.0) < 0.01); // lon ~5
    }

    [Fact]
    public void Midpoint_NullInput()
    {
        Assert.True(Invoke("midpoint", Null(), F(0.0), F(0.0), F(0.0)).IsNull);
    }

    // =========================================================================
    // ageFromDate
    // =========================================================================

    [Fact]
    public void AgeFromDate_ReturnsPositiveAge()
    {
        // Someone born 30 years ago
        var birthYear = DateTime.UtcNow.Year - 30;
        var birthDate = $"{birthYear}-01-01";
        var result = Invoke("ageFromDate", D(birthDate));
        Assert.True(result.AsInt64() >= 29 && result.AsInt64() <= 31);
    }

    [Fact]
    public void AgeFromDate_NullInput()
    {
        Assert.True(Invoke("ageFromDate", Null()).IsNull);
    }
}
