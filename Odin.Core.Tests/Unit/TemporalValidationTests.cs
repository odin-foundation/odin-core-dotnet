using Odin.Core;
using Odin.Core.Types;
using Xunit;

namespace Odin.Core.Tests.Unit;

public class TemporalValidationTests
{
    // ─────────────────────────────────────────────────────────────────
    // Timestamp — valid components
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Timestamp_ValidComponents_Parses()
    {
        var doc = Core.Odin.Parse("ts = 2024-06-15T23:59:59Z");
        Assert.True(doc.Get("ts")!.IsTimestamp);
        Assert.Equal("2024-06-15T23:59:59Z", ((OdinTimestamp)doc.Get("ts")!).Raw);
    }

    [Fact]
    public void Timestamp_PositiveOffset_Parses()
    {
        var doc = Core.Odin.Parse("ts = 2024-06-15T10:30:00+05:30");
        Assert.Equal("2024-06-15T10:30:00+05:30", ((OdinTimestamp)doc.Get("ts")!).Raw);
    }

    [Fact]
    public void Timestamp_NegativeOffset_Parses()
    {
        var doc = Core.Odin.Parse("ts = 2024-06-15T10:30:00-08:00");
        Assert.Equal("2024-06-15T10:30:00-08:00", ((OdinTimestamp)doc.Get("ts")!).Raw);
    }

    [Fact]
    public void Timestamp_Milliseconds_Parses()
    {
        var doc = Core.Odin.Parse("ts = 2024-06-15T10:30:00.123Z");
        Assert.Equal("2024-06-15T10:30:00.123Z", ((OdinTimestamp)doc.Get("ts")!).Raw);
    }

    [Fact]
    public void Timestamp_LeapSecond_Allowed()
    {
        var doc = Core.Odin.Parse("ts = 2016-12-31T23:59:60Z");
        Assert.Equal("2016-12-31T23:59:60Z", ((OdinTimestamp)doc.Get("ts")!).Raw);
    }

    // ─────────────────────────────────────────────────────────────────
    // Timestamp — invalid components
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Timestamp_BadDatePortion_Throws_P001()
    {
        var ex = Assert.Throws<OdinParseException>(() => Core.Odin.Parse("ts = 2024-13-40T10:30:00Z"));
        Assert.Equal("P001", ex.Code);
    }

    [Fact]
    public void Timestamp_HourTooLarge_Throws_P001()
    {
        var ex = Assert.Throws<OdinParseException>(() => Core.Odin.Parse("ts = 2024-06-15T25:30:00Z"));
        Assert.Equal("P001", ex.Code);
    }

    [Fact]
    public void Timestamp_MinuteTooLarge_Throws_P001()
    {
        var ex = Assert.Throws<OdinParseException>(() => Core.Odin.Parse("ts = 2024-06-15T10:61:00Z"));
        Assert.Equal("P001", ex.Code);
    }

    [Fact]
    public void Timestamp_SecondTooLarge_Throws_P001()
    {
        var ex = Assert.Throws<OdinParseException>(() => Core.Odin.Parse("ts = 2024-06-15T10:30:61Z"));
        Assert.Equal("P001", ex.Code);
    }

    [Fact]
    public void Timestamp_OffsetOutOfRange_Throws_P001()
    {
        var ex = Assert.Throws<OdinParseException>(() => Core.Odin.Parse("ts = 2024-06-15T10:30:00+25:00"));
        Assert.Equal("P001", ex.Code);
    }

    [Fact]
    public void Timestamp_FullyMalformed_Throws_P001()
    {
        var ex = Assert.Throws<OdinParseException>(() => Core.Odin.Parse("ts = 2024-13-40T99:99:99Z"));
        Assert.Equal("P001", ex.Code);
    }

    // ─────────────────────────────────────────────────────────────────
    // Time — valid components
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Time_Valid_Parses()
    {
        var doc = Core.Odin.Parse("t = T14:30:00");
        Assert.True(doc.Get("t")!.IsTime);
        Assert.Equal("T14:30:00", ((OdinTime)doc.Get("t")!).Value);
    }

    [Fact]
    public void Time_NoSeconds_Parses()
    {
        var doc = Core.Odin.Parse("t = T14:30");
        Assert.Equal("T14:30", ((OdinTime)doc.Get("t")!).Value);
    }

    [Fact]
    public void Time_Milliseconds_Parses()
    {
        var doc = Core.Odin.Parse("t = T14:30:00.123");
        Assert.Equal("T14:30:00.123", ((OdinTime)doc.Get("t")!).Value);
    }

    [Fact]
    public void Time_EndOfDayMidnight_Allowed()
    {
        var doc = Core.Odin.Parse("t = T24:00:00");
        Assert.Equal("T24:00:00", ((OdinTime)doc.Get("t")!).Value);
    }

    [Fact]
    public void Time_LeapSecond_Allowed()
    {
        var doc = Core.Odin.Parse("t = T23:59:60");
        Assert.Equal("T23:59:60", ((OdinTime)doc.Get("t")!).Value);
    }

    // ─────────────────────────────────────────────────────────────────
    // Time — invalid components
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Time_HourTooLarge_Throws_P001()
    {
        var ex = Assert.Throws<OdinParseException>(() => Core.Odin.Parse("t = T25:00:00"));
        Assert.Equal("P001", ex.Code);
    }

    [Fact]
    public void Time_Hour24NonZeroMinutes_Throws_P001()
    {
        var ex = Assert.Throws<OdinParseException>(() => Core.Odin.Parse("t = T24:30:00"));
        Assert.Equal("P001", ex.Code);
    }

    [Fact]
    public void Time_MinuteTooLarge_Throws_P001()
    {
        var ex = Assert.Throws<OdinParseException>(() => Core.Odin.Parse("t = T14:61:00"));
        Assert.Equal("P001", ex.Code);
    }

    [Fact]
    public void Time_SecondTooLarge_Throws_P001()
    {
        var ex = Assert.Throws<OdinParseException>(() => Core.Odin.Parse("t = T14:30:61"));
        Assert.Equal("P001", ex.Code);
    }
}
