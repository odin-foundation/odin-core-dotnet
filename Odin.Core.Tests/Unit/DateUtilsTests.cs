using Odin.Core.Utils;
using Xunit;

namespace Odin.Core.Tests.Unit;

public class DateUtilsTests
{
    // ─────────────────────────────────────────────────────────────────
    // ParseDate
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ParseDate_ValidDate()
    {
        var result = DateUtils.ParseDate("2024-06-15");
        Assert.NotNull(result);
        Assert.Equal(2024, result!.Value.year);
        Assert.Equal(6, result.Value.month);
        Assert.Equal(15, result.Value.day);
    }

    [Fact]
    public void ParseDate_InvalidFormat_ReturnsNull()
    {
        Assert.Null(DateUtils.ParseDate("2024"));
        Assert.Null(DateUtils.ParseDate("not-date"));
    }

    [Fact]
    public void ParseDate_TooShort_ReturnsNull()
    {
        Assert.Null(DateUtils.ParseDate("24-06-15"));
    }

    [Fact]
    public void ParseDate_InvalidMonth_ReturnsNull()
    {
        Assert.Null(DateUtils.ParseDate("2024-13-01"));
        Assert.Null(DateUtils.ParseDate("2024-00-01"));
    }

    [Fact]
    public void ParseDate_InvalidDay_ReturnsNull()
    {
        Assert.Null(DateUtils.ParseDate("2024-01-00"));
    }

    // ─────────────────────────────────────────────────────────────────
    // ParseTimestampToEpochMs
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ParseTimestampToEpochMs_UnixEpoch()
    {
        var result = DateUtils.ParseTimestampToEpochMs("1970-01-01T00:00:00Z");
        Assert.NotNull(result);
        Assert.Equal(0L, result!.Value);
    }

    [Fact]
    public void ParseTimestampToEpochMs_KnownTimestamp()
    {
        var result = DateUtils.ParseTimestampToEpochMs("2024-01-01T00:00:00Z");
        Assert.NotNull(result);
        Assert.True(result!.Value > 0);
    }

    [Fact]
    public void ParseTimestampToEpochMs_Invalid_ReturnsNull()
    {
        Assert.Null(DateUtils.ParseTimestampToEpochMs("not-a-timestamp"));
    }

    // ─────────────────────────────────────────────────────────────────
    // EpochMsToTimestamp
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void EpochMsToTimestamp_Zero()
    {
        var result = DateUtils.EpochMsToTimestamp(0);
        Assert.Equal("1970-01-01T00:00:00.000Z", result);
    }

    [Fact]
    public void EpochMsToTimestamp_RoundTrip()
    {
        var epochMs = DateUtils.ParseTimestampToEpochMs("2024-06-15T14:30:00Z");
        Assert.NotNull(epochMs);
        var back = DateUtils.EpochMsToTimestamp(epochMs!.Value);
        Assert.Contains("2024-06-15", back);
        Assert.Contains("14:30:00", back);
    }

    // ─────────────────────────────────────────────────────────────────
    // AddDays
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void AddDays_Positive()
    {
        var result = DateUtils.AddDays("2024-01-01", 10);
        Assert.Equal("2024-01-11", result);
    }

    [Fact]
    public void AddDays_Negative()
    {
        var result = DateUtils.AddDays("2024-01-11", -10);
        Assert.Equal("2024-01-01", result);
    }

    [Fact]
    public void AddDays_CrossMonth()
    {
        var result = DateUtils.AddDays("2024-01-30", 5);
        Assert.Equal("2024-02-04", result);
    }

    [Fact]
    public void AddDays_InvalidDate_ReturnsNull()
    {
        Assert.Null(DateUtils.AddDays("not-a-date", 1));
    }

    // ─────────────────────────────────────────────────────────────────
    // AddMonths
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void AddMonths_Positive()
    {
        var result = DateUtils.AddMonths("2024-01-15", 3);
        Assert.Equal("2024-04-15", result);
    }

    [Fact]
    public void AddMonths_CrossYear()
    {
        var result = DateUtils.AddMonths("2024-11-01", 3);
        Assert.Equal("2025-02-01", result);
    }

    [Fact]
    public void AddMonths_InvalidDate_ReturnsNull()
    {
        Assert.Null(DateUtils.AddMonths("invalid", 1));
    }

    // ─────────────────────────────────────────────────────────────────
    // DateDiffDays
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void DateDiffDays_SameDate()
    {
        var result = DateUtils.DateDiffDays("2024-06-15", "2024-06-15");
        Assert.Equal(0L, result);
    }

    [Fact]
    public void DateDiffDays_PositiveDifference()
    {
        var result = DateUtils.DateDiffDays("2024-01-01", "2024-01-11");
        Assert.Equal(10L, result);
    }

    [Fact]
    public void DateDiffDays_NegativeDifference()
    {
        var result = DateUtils.DateDiffDays("2024-01-11", "2024-01-01");
        Assert.Equal(-10L, result);
    }

    [Fact]
    public void DateDiffDays_InvalidDates_ReturnsNull()
    {
        Assert.Null(DateUtils.DateDiffDays("bad", "2024-01-01"));
        Assert.Null(DateUtils.DateDiffDays("2024-01-01", "bad"));
    }

    // ─────────────────────────────────────────────────────────────────
    // IsValidDate
    // ─────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(2024, 1, 1, true)]
    [InlineData(2024, 2, 29, true)]   // leap year
    [InlineData(2023, 2, 29, false)]  // not a leap year
    [InlineData(2024, 12, 31, true)]
    [InlineData(2024, 4, 30, true)]
    [InlineData(2024, 4, 31, false)]  // April has 30 days
    [InlineData(2024, 0, 1, false)]   // invalid month
    [InlineData(2024, 13, 1, false)]  // invalid month
    [InlineData(2024, 1, 0, false)]   // invalid day
    public void IsValidDate_Theory(int year, int month, int day, bool expected)
    {
        Assert.Equal(expected, DateUtils.IsValidDate(year, month, day));
    }

    // ─────────────────────────────────────────────────────────────────
    // DaysInMonth
    // ─────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(2024, 1, 31)]
    [InlineData(2024, 2, 29)]  // leap year
    [InlineData(2023, 2, 28)]  // non-leap year
    [InlineData(2024, 4, 30)]
    [InlineData(2024, 6, 30)]
    [InlineData(2024, 9, 30)]
    [InlineData(2024, 11, 30)]
    [InlineData(2024, 7, 31)]
    [InlineData(2024, 12, 31)]
    public void DaysInMonth_Theory(int year, int month, int expected)
    {
        Assert.Equal(expected, DateUtils.DaysInMonth(year, month));
    }

    // ─────────────────────────────────────────────────────────────────
    // IsLeapYear
    // ─────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(2024, true)]
    [InlineData(2000, true)]   // divisible by 400
    [InlineData(1900, false)]  // divisible by 100 but not 400
    [InlineData(2023, false)]
    [InlineData(2020, true)]
    public void IsLeapYear_Theory(int year, bool expected)
    {
        Assert.Equal(expected, DateUtils.IsLeapYear(year));
    }
}
