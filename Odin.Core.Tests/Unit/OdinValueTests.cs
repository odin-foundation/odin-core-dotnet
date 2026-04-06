using System;
using System.Collections.Generic;
using Odin.Core.Types;
using Xunit;

namespace Odin.Core.Tests.Unit;

public class OdinValueTests
{
    // ─────────────────────────────────────────────────────────────────
    // Creation and Type Discriminator
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void OdinNull_HasCorrectType()
    {
        var v = new OdinNull();
        Assert.Equal(OdinValueType.Null, v.Type);
        Assert.True(v.IsNull);
        Assert.False(v.IsString);
        Assert.Equal("~", v.ToString());
    }

    [Fact]
    public void OdinBoolean_True()
    {
        var v = new OdinBoolean(true);
        Assert.Equal(OdinValueType.Boolean, v.Type);
        Assert.True(v.IsBoolean);
        Assert.Equal(true, v.AsBool());
        Assert.Equal("true", v.ToString());
    }

    [Fact]
    public void OdinBoolean_False()
    {
        var v = new OdinBoolean(false);
        Assert.Equal(false, v.AsBool());
        Assert.Equal("false", v.ToString());
    }

    [Fact]
    public void OdinString_StoresValue()
    {
        var v = new OdinString("hello");
        Assert.Equal(OdinValueType.String, v.Type);
        Assert.True(v.IsString);
        Assert.Equal("hello", v.AsString());
        Assert.Equal("\"hello\"", v.ToString());
    }

    [Fact]
    public void OdinInteger_StoresValue()
    {
        var v = new OdinInteger(42);
        Assert.Equal(OdinValueType.Integer, v.Type);
        Assert.True(v.IsInteger);
        Assert.Equal(42L, v.AsInt64());
        Assert.Equal(42.0, v.AsDouble());
        Assert.Equal("##42", v.ToString());
    }

    [Fact]
    public void OdinInteger_WithRaw_PreservesRaw()
    {
        var v = new OdinInteger(100) { Raw = "100" };
        Assert.Equal("##100", v.ToString());
    }

    [Fact]
    public void OdinNumber_StoresValue()
    {
        var v = new OdinNumber(3.14);
        Assert.Equal(OdinValueType.Number, v.Type);
        Assert.True(v.IsNumber);
        Assert.Equal(3.14, v.AsDouble());
        Assert.Null(v.AsInt64());
    }

    [Fact]
    public void OdinCurrency_StoresValue()
    {
        var v = new OdinCurrency(99.99);
        Assert.Equal(OdinValueType.Currency, v.Type);
        Assert.True(v.IsCurrency);
        Assert.Equal(99.99m, v.AsDecimal());
        Assert.Equal((double)99.99m, v.AsDouble());
    }

    [Fact]
    public void OdinCurrency_WithCode()
    {
        var v = new OdinCurrency(100.00) { CurrencyCode = "USD" };
        Assert.Contains("USD", v.ToString());
    }

    [Fact]
    public void OdinPercent_StoresValue()
    {
        var v = new OdinPercent(0.15);
        Assert.Equal(OdinValueType.Percent, v.Type);
        Assert.True(v.IsPercent);
        Assert.Equal(0.15, v.AsDouble());
    }

    [Fact]
    public void OdinDate_StoresComponents()
    {
        var v = new OdinDate(2024, 6, 15);
        Assert.Equal(OdinValueType.Date, v.Type);
        Assert.True(v.IsDate);
        Assert.Equal(2024, v.Year);
        Assert.Equal(6, v.Month);
        Assert.Equal(15, v.Day);
        Assert.Equal("2024-06-15", v.Raw);
        Assert.Equal("2024-06-15", v.ToString());
    }

    [Fact]
    public void OdinTimestamp_StoresEpochMs()
    {
        var v = new OdinTimestamp(1718451000000L, "2024-06-15T14:30:00Z");
        Assert.Equal(OdinValueType.Timestamp, v.Type);
        Assert.True(v.IsTimestamp);
        Assert.Equal(1718451000000L, v.EpochMs);
        Assert.Equal("2024-06-15T14:30:00Z", v.Raw);
    }

    [Fact]
    public void OdinTime_StoresValue()
    {
        var v = new OdinTime("T14:30:00");
        Assert.Equal(OdinValueType.Time, v.Type);
        Assert.True(v.IsTime);
        Assert.Equal("T14:30:00", v.Value);
    }

    [Fact]
    public void OdinDuration_StoresValue()
    {
        var v = new OdinDuration("P1Y6M");
        Assert.Equal(OdinValueType.Duration, v.Type);
        Assert.True(v.IsDuration);
        Assert.Equal("P1Y6M", v.Value);
    }

    [Fact]
    public void OdinReference_StoresPath()
    {
        var v = new OdinReference("policy.id");
        Assert.Equal(OdinValueType.Reference, v.Type);
        Assert.True(v.IsReference);
        Assert.Equal("policy.id", v.AsReference());
        Assert.Equal("@policy.id", v.ToString());
    }

    [Fact]
    public void OdinBinary_StoresData()
    {
        var data = new byte[] { 72, 101, 108, 108, 111 };
        var v = new OdinBinary(data);
        Assert.Equal(OdinValueType.Binary, v.Type);
        Assert.True(v.IsBinary);
        Assert.Equal(data, v.Data);
    }

    [Fact]
    public void OdinBinary_WithAlgorithm()
    {
        var v = new OdinBinary(new byte[] { 1, 2, 3 }) { Algorithm = "sha256" };
        Assert.Equal("sha256", v.Algorithm);
        Assert.Contains("sha256", v.ToString());
    }

    [Fact]
    public void OdinVerb_StoresNameAndArgs()
    {
        var args = new List<OdinValue> { new OdinString("arg1") };
        var v = new OdinVerb("upper", args);
        Assert.Equal(OdinValueType.Verb, v.Type);
        Assert.True(v.IsVerb);
        Assert.Equal("upper", v.Name);
        Assert.Single(v.Args);
        Assert.False(v.IsCustom);
    }

    [Fact]
    public void OdinArray_StoresItems()
    {
        var items = new List<OdinArrayItem>
        {
            OdinArrayItem.FromValue(new OdinString("a")),
            OdinArrayItem.FromValue(new OdinInteger(1)),
        };
        var v = new OdinArray(items);
        Assert.Equal(OdinValueType.Array, v.Type);
        Assert.True(v.IsArray);
        Assert.NotNull(v.AsArray());
        Assert.Equal(2, v.AsArray()!.Count);
    }

    [Fact]
    public void OdinObject_StoresFields()
    {
        var fields = new List<KeyValuePair<string, OdinValue>>
        {
            new("name", new OdinString("Alice")),
            new("age", new OdinInteger(30)),
        };
        var v = new OdinObject(fields);
        Assert.Equal(OdinValueType.Object, v.Type);
        Assert.True(v.IsObject);
        Assert.Equal(2, v.Fields.Count);
    }

    // ─────────────────────────────────────────────────────────────────
    // Composite Type Checks
    // ─────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(typeof(OdinInteger))]
    [InlineData(typeof(OdinNumber))]
    [InlineData(typeof(OdinCurrency))]
    [InlineData(typeof(OdinPercent))]
    public void IsNumeric_TrueForNumericTypes(Type type)
    {
        OdinValue v = type.Name switch
        {
            nameof(OdinInteger) => new OdinInteger(1),
            nameof(OdinNumber) => new OdinNumber(1.0),
            nameof(OdinCurrency) => new OdinCurrency(1.0),
            nameof(OdinPercent) => new OdinPercent(0.01),
            _ => throw new InvalidOperationException(),
        };
        Assert.True(v.IsNumeric);
    }

    [Fact]
    public void IsNumeric_FalseForString()
    {
        Assert.False(new OdinString("42").IsNumeric);
    }

    [Theory]
    [InlineData(typeof(OdinDate))]
    [InlineData(typeof(OdinTimestamp))]
    [InlineData(typeof(OdinTime))]
    [InlineData(typeof(OdinDuration))]
    public void IsTemporal_TrueForTemporalTypes(Type type)
    {
        OdinValue v = type.Name switch
        {
            nameof(OdinDate) => new OdinDate(2024, 1, 1),
            nameof(OdinTimestamp) => new OdinTimestamp(0, "1970-01-01T00:00:00Z"),
            nameof(OdinTime) => new OdinTime("T12:00:00"),
            nameof(OdinDuration) => new OdinDuration("PT1H"),
            _ => throw new InvalidOperationException(),
        };
        Assert.True(v.IsTemporal);
    }

    // ─────────────────────────────────────────────────────────────────
    // Typed Accessors Return Null for Wrong Type
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void AsString_ReturnsNull_ForNonString()
    {
        Assert.Null(new OdinInteger(42).AsString());
        Assert.Null(new OdinNull().AsString());
        Assert.Null(new OdinBoolean(true).AsString());
    }

    [Fact]
    public void AsInt64_ReturnsNull_ForNonInteger()
    {
        Assert.Null(new OdinString("42").AsInt64());
        Assert.Null(new OdinNumber(3.14).AsInt64());
    }

    [Fact]
    public void AsDouble_ReturnsNull_ForNonNumeric()
    {
        Assert.Null(new OdinString("hello").AsDouble());
        Assert.Null(new OdinNull().AsDouble());
    }

    [Fact]
    public void AsBool_ReturnsNull_ForNonBoolean()
    {
        Assert.Null(new OdinString("true").AsBool());
        Assert.Null(new OdinInteger(1).AsBool());
    }

    [Fact]
    public void AsDecimal_ReturnsNull_ForNonCurrency()
    {
        Assert.Null(new OdinNumber(3.14).AsDecimal());
        Assert.Null(new OdinInteger(42).AsDecimal());
    }

    [Fact]
    public void AsReference_ReturnsNull_ForNonReference()
    {
        Assert.Null(new OdinString("path").AsReference());
    }

    [Fact]
    public void AsArray_ReturnsNull_ForNonArray()
    {
        Assert.Null(new OdinString("[]").AsArray());
    }

    // ─────────────────────────────────────────────────────────────────
    // OdinValues Factory Methods
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Factory_Null()
    {
        var v = OdinValues.Null();
        Assert.True(v.IsNull);
    }

    [Fact]
    public void Factory_Boolean()
    {
        var v = OdinValues.Boolean(true);
        Assert.Equal(true, v.AsBool());
    }

    [Fact]
    public void Factory_String()
    {
        var v = OdinValues.String("test");
        Assert.Equal("test", v.AsString());
    }

    [Fact]
    public void Factory_Integer()
    {
        var v = OdinValues.Integer(99);
        Assert.Equal(99L, v.AsInt64());
    }

    [Fact]
    public void Factory_IntegerFromStr()
    {
        var v = OdinValues.IntegerFromStr("12345");
        Assert.Equal(12345L, v.AsInt64());
        Assert.Equal("12345", v.Raw);
    }

    [Fact]
    public void Factory_Number()
    {
        var v = OdinValues.Number(2.718);
        Assert.Equal(2.718, v.AsDouble());
    }

    [Fact]
    public void Factory_NumberWithPlaces()
    {
        var v = OdinValues.NumberWithPlaces(3.14, 2);
        Assert.Equal((byte)2, v.DecimalPlaces);
    }

    [Fact]
    public void Factory_Currency()
    {
        var v = OdinValues.Currency(99.99, 2);
        Assert.Equal(99.99m, v.AsDecimal());
        Assert.Equal((byte)2, v.DecimalPlaces);
    }

    [Fact]
    public void Factory_CurrencyWithCode()
    {
        var v = OdinValues.CurrencyWithCode(100.00, 2, "EUR");
        Assert.Equal("EUR", v.CurrencyCode);
    }

    [Fact]
    public void Factory_Percent()
    {
        var v = OdinValues.Percent(0.25);
        Assert.Equal(0.25, v.AsDouble());
    }

    [Fact]
    public void Factory_Date()
    {
        var v = OdinValues.Date(2024, 12, 25);
        Assert.Equal(2024, v.Year);
        Assert.Equal(12, v.Month);
        Assert.Equal(25, v.Day);
    }

    [Fact]
    public void Factory_DateFromStr_Valid()
    {
        var v = OdinValues.DateFromStr("2024-06-15");
        Assert.NotNull(v);
        Assert.Equal(2024, v!.Year);
        Assert.Equal(6, v.Month);
        Assert.Equal(15, v.Day);
    }

    [Fact]
    public void Factory_DateFromStr_Invalid_ReturnsNull()
    {
        Assert.Null(OdinValues.DateFromStr("not-a-date"));
        Assert.Null(OdinValues.DateFromStr("2024"));
    }

    [Fact]
    public void Factory_Timestamp()
    {
        var v = OdinValues.Timestamp(0L, "1970-01-01T00:00:00Z");
        Assert.Equal(0L, v.EpochMs);
    }

    [Fact]
    public void Factory_Time()
    {
        var v = OdinValues.Time("T09:30:00");
        Assert.Equal("T09:30:00", v.Value);
    }

    [Fact]
    public void Factory_Duration()
    {
        var v = OdinValues.Duration("PT30M");
        Assert.Equal("PT30M", v.Value);
    }

    [Fact]
    public void Factory_Reference()
    {
        var v = OdinValues.Reference("Customer.Name");
        Assert.Equal("Customer.Name", v.AsReference());
    }

    [Fact]
    public void Factory_Binary()
    {
        var data = new byte[] { 1, 2, 3 };
        var v = OdinValues.Binary(data);
        Assert.Equal(data, v.Data);
    }

    [Fact]
    public void Factory_BinaryWithAlgorithm()
    {
        var v = OdinValues.BinaryWithAlgorithm(new byte[] { 0xFF }, "sha256");
        Assert.Equal("sha256", v.Algorithm);
    }

    [Fact]
    public void Factory_Verb()
    {
        var args = new List<OdinValue> { OdinValues.String("x") };
        var v = OdinValues.Verb("upper", args);
        Assert.Equal("upper", v.Name);
        Assert.False(v.IsCustom);
    }

    [Fact]
    public void Factory_CustomVerb()
    {
        var v = OdinValues.CustomVerb("ns.fn", Array.Empty<OdinValue>());
        Assert.True(v.IsCustom);
    }

    [Fact]
    public void Factory_Array()
    {
        var items = new List<OdinArrayItem> { OdinArrayItem.FromValue(OdinValues.Integer(1)) };
        var v = OdinValues.Array(items);
        Assert.Single(v.Items);
    }

    [Fact]
    public void Factory_Object()
    {
        var fields = new List<KeyValuePair<string, OdinValue>>
        {
            new("k", OdinValues.String("v")),
        };
        var v = OdinValues.Object(fields);
        Assert.Single(v.Fields);
    }

    // ─────────────────────────────────────────────────────────────────
    // WithModifiers
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void WithModifiers_ReturnsNewValueWithModifiers()
    {
        var original = OdinValues.String("secret");
        var mods = new OdinModifiers { Confidential = true };
        var modified = original.WithModifiers(mods);

        Assert.True(modified.IsConfidential);
        Assert.False(original.IsConfidential);
        Assert.Equal("secret", modified.AsString());
    }

    [Fact]
    public void WithModifiers_AllCombinations()
    {
        var mods = new OdinModifiers { Required = true, Deprecated = true, Confidential = true };
        var v = OdinValues.Integer(42).WithModifiers(mods);

        Assert.True(v.IsRequired);
        Assert.True(v.IsDeprecated);
        Assert.True(v.IsConfidential);
    }

    [Fact]
    public void WithModifiers_NullModifiers_ClearsModifiers()
    {
        var v = OdinValues.String("x").WithModifiers(new OdinModifiers { Required = true });
        var cleared = v.WithModifiers(null);
        Assert.False(cleared.IsRequired);
    }

    [Fact]
    public void WithModifiers_PreservesType_ForEachSubclass()
    {
        // Test that WithModifiers returns the same subtype
        var mods = new OdinModifiers { Required = true };
        Assert.IsType<OdinNull>(new OdinNull().WithModifiers(mods));
        Assert.IsType<OdinBoolean>(new OdinBoolean(true).WithModifiers(mods));
        Assert.IsType<OdinString>(new OdinString("x").WithModifiers(mods));
        Assert.IsType<OdinInteger>(new OdinInteger(1).WithModifiers(mods));
        Assert.IsType<OdinNumber>(new OdinNumber(1.0).WithModifiers(mods));
        Assert.IsType<OdinCurrency>(new OdinCurrency(1.0).WithModifiers(mods));
        Assert.IsType<OdinPercent>(new OdinPercent(0.1).WithModifiers(mods));
        Assert.IsType<OdinDate>(new OdinDate(2024, 1, 1).WithModifiers(mods));
        Assert.IsType<OdinTimestamp>(new OdinTimestamp(0, "x").WithModifiers(mods));
        Assert.IsType<OdinTime>(new OdinTime("T00:00").WithModifiers(mods));
        Assert.IsType<OdinDuration>(new OdinDuration("PT1H").WithModifiers(mods));
        Assert.IsType<OdinReference>(new OdinReference("p").WithModifiers(mods));
        Assert.IsType<OdinBinary>(new OdinBinary(new byte[] { }).WithModifiers(mods));
        Assert.IsType<OdinVerb>(new OdinVerb("v", Array.Empty<OdinValue>()).WithModifiers(mods));
        Assert.IsType<OdinArray>(new OdinArray(Array.Empty<OdinArrayItem>()).WithModifiers(mods));
        Assert.IsType<OdinObject>(new OdinObject(Array.Empty<KeyValuePair<string, OdinValue>>()).WithModifiers(mods));
    }

    // ─────────────────────────────────────────────────────────────────
    // OdinArrayItem
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ArrayItem_FromValue_ReturnsValue()
    {
        var item = OdinArrayItem.FromValue(OdinValues.String("hi"));
        Assert.NotNull(item.AsValue());
        Assert.Equal("hi", item.AsValue()!.AsString());
        Assert.Null(item.AsRecord());
    }

    [Fact]
    public void ArrayItem_Record_ReturnsFields()
    {
        var fields = new List<KeyValuePair<string, OdinValue>>
        {
            new("name", OdinValues.String("Alice")),
        };
        var item = OdinArrayItem.Record(fields);
        Assert.NotNull(item.AsRecord());
        var record = Assert.Single(item.AsRecord()!);
        Assert.Equal("name", record.Key);
        Assert.Null(item.AsValue());
    }
}
