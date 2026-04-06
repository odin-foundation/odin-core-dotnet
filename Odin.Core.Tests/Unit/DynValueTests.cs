using System.Collections.Generic;
using System.Text.Json;
using Odin.Core.Types;
using Xunit;

namespace Odin.Core.Tests.Unit;

public class DynValueTests
{
    // ─────────────────────────────────────────────────────────────────
    // Factory Methods & Type Checking
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Null_IsNull()
    {
        var v = DynValue.Null();
        Assert.Equal(DynValueType.Null, v.Type);
        Assert.True(v.IsNull);
    }

    [Fact]
    public void Bool_True()
    {
        var v = DynValue.Bool(true);
        Assert.Equal(DynValueType.Bool, v.Type);
        Assert.Equal(true, v.AsBool());
    }

    [Fact]
    public void Bool_False()
    {
        var v = DynValue.Bool(false);
        Assert.Equal(false, v.AsBool());
    }

    [Fact]
    public void Integer_StoresValue()
    {
        var v = DynValue.Integer(42);
        Assert.Equal(DynValueType.Integer, v.Type);
        Assert.Equal(42L, v.AsInt64());
        Assert.Equal(42.0, v.AsDouble());
    }

    [Fact]
    public void Float_StoresValue()
    {
        var v = DynValue.Float(3.14);
        Assert.Equal(DynValueType.Float, v.Type);
        Assert.Equal(3.14, v.AsDouble());
        Assert.Null(v.AsInt64());
    }

    [Fact]
    public void FloatRaw_StoresRawString()
    {
        var v = DynValue.FloatRaw("1234567890.123456789");
        Assert.Equal(DynValueType.FloatRaw, v.Type);
        Assert.Equal("1234567890.123456789", v.AsString());
        Assert.NotNull(v.AsDouble());
    }

    [Fact]
    public void String_StoresValue()
    {
        var v = DynValue.String("hello");
        Assert.Equal(DynValueType.String, v.Type);
        Assert.Equal("hello", v.AsString());
    }

    [Fact]
    public void Currency_StoresValue()
    {
        var v = DynValue.Currency(99.99, 2, "USD");
        Assert.Equal(DynValueType.Currency, v.Type);
        Assert.Equal(99.99, v.AsDouble());
    }

    [Fact]
    public void CurrencyRaw_StoresRawString()
    {
        var v = DynValue.CurrencyRaw("100.50", 2, "EUR");
        Assert.Equal(DynValueType.CurrencyRaw, v.Type);
        Assert.Equal("100.50", v.AsString());
    }

    [Fact]
    public void Percent_StoresValue()
    {
        var v = DynValue.Percent(0.15);
        Assert.Equal(DynValueType.Percent, v.Type);
        Assert.Equal(0.15, v.AsDouble());
    }

    [Fact]
    public void Reference_StoresPath()
    {
        var v = DynValue.Reference("policy.id");
        Assert.Equal(DynValueType.Reference, v.Type);
        Assert.Equal("policy.id", v.AsString());
    }

    [Fact]
    public void Binary_StoresBase64()
    {
        var v = DynValue.Binary("SGVsbG8=");
        Assert.Equal(DynValueType.Binary, v.Type);
        Assert.Equal("SGVsbG8=", v.AsString());
    }

    [Fact]
    public void Date_StoresDateString()
    {
        var v = DynValue.Date("2024-06-15");
        Assert.Equal(DynValueType.Date, v.Type);
        Assert.Equal("2024-06-15", v.AsString());
    }

    [Fact]
    public void Timestamp_StoresTimestampString()
    {
        var v = DynValue.Timestamp("2024-06-15T14:30:00Z");
        Assert.Equal(DynValueType.Timestamp, v.Type);
        Assert.Equal("2024-06-15T14:30:00Z", v.AsString());
    }

    [Fact]
    public void Time_StoresTimeString()
    {
        var v = DynValue.Time("14:30:00");
        Assert.Equal(DynValueType.Time, v.Type);
        Assert.Equal("14:30:00", v.AsString());
    }

    [Fact]
    public void Duration_StoresDurationString()
    {
        var v = DynValue.Duration("PT30M");
        Assert.Equal(DynValueType.Duration, v.Type);
        Assert.Equal("PT30M", v.AsString());
    }

    [Fact]
    public void Array_StoresItems()
    {
        var items = new List<DynValue> { DynValue.Integer(1), DynValue.String("two") };
        var v = DynValue.Array(items);
        Assert.Equal(DynValueType.Array, v.Type);
        Assert.NotNull(v.AsArray());
        Assert.Equal(2, v.AsArray()!.Count);
    }

    [Fact]
    public void Object_StoresEntries()
    {
        var entries = new List<KeyValuePair<string, DynValue>>
        {
            new("name", DynValue.String("Alice")),
            new("age", DynValue.Integer(30)),
        };
        var v = DynValue.Object(entries);
        Assert.Equal(DynValueType.Object, v.Type);
        Assert.NotNull(v.AsObject());
        Assert.Equal(2, v.AsObject()!.Count);
    }

    // ─────────────────────────────────────────────────────────────────
    // Typed Accessors Return Null for Wrong Types
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void AsString_ReturnsNull_ForNonStringTypes()
    {
        Assert.Null(DynValue.Null().AsString());
        Assert.Null(DynValue.Bool(true).AsString());
        Assert.Null(DynValue.Integer(1).AsString());
        Assert.Null(DynValue.Float(1.0).AsString());
    }

    [Fact]
    public void AsInt64_ReturnsNull_ForNonInteger()
    {
        Assert.Null(DynValue.String("42").AsInt64());
        Assert.Null(DynValue.Float(42.0).AsInt64());
        Assert.Null(DynValue.Null().AsInt64());
    }

    [Fact]
    public void AsDouble_ReturnsNull_ForNonNumeric()
    {
        Assert.Null(DynValue.String("hello").AsDouble());
        Assert.Null(DynValue.Null().AsDouble());
        Assert.Null(DynValue.Bool(true).AsDouble());
    }

    [Fact]
    public void AsBool_ReturnsNull_ForNonBool()
    {
        Assert.Null(DynValue.String("true").AsBool());
        Assert.Null(DynValue.Integer(1).AsBool());
    }

    [Fact]
    public void AsArray_ReturnsNull_ForNonArray()
    {
        Assert.Null(DynValue.String("[]").AsArray());
        Assert.Null(DynValue.Null().AsArray());
    }

    [Fact]
    public void AsObject_ReturnsNull_ForNonObject()
    {
        Assert.Null(DynValue.String("{}").AsObject());
    }

    // ─────────────────────────────────────────────────────────────────
    // Object/Array Access
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Get_ReturnsFieldFromObject()
    {
        var v = DynValue.Object(new List<KeyValuePair<string, DynValue>>
        {
            new("name", DynValue.String("Bob")),
        });
        var field = v.Get("name");
        Assert.NotNull(field);
        Assert.Equal("Bob", field!.AsString());
    }

    [Fact]
    public void Get_ReturnsNull_ForMissingKey()
    {
        var v = DynValue.Object(new List<KeyValuePair<string, DynValue>>());
        Assert.Null(v.Get("missing"));
    }

    [Fact]
    public void Get_ReturnsNull_ForNonObject()
    {
        Assert.Null(DynValue.String("test").Get("key"));
    }

    [Fact]
    public void GetIndex_ReturnsArrayElement()
    {
        var v = DynValue.Array(new List<DynValue> { DynValue.Integer(10), DynValue.Integer(20) });
        Assert.Equal(10L, v.GetIndex(0)!.AsInt64());
        Assert.Equal(20L, v.GetIndex(1)!.AsInt64());
    }

    [Fact]
    public void GetIndex_ReturnsNull_ForOutOfRange()
    {
        var v = DynValue.Array(new List<DynValue> { DynValue.Integer(1) });
        Assert.Null(v.GetIndex(-1));
        Assert.Null(v.GetIndex(1));
    }

    // ─────────────────────────────────────────────────────────────────
    // Equality
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Equality_NullValues()
    {
        Assert.Equal(DynValue.Null(), DynValue.Null());
    }

    [Fact]
    public void Equality_BoolValues()
    {
        Assert.Equal(DynValue.Bool(true), DynValue.Bool(true));
        Assert.NotEqual(DynValue.Bool(true), DynValue.Bool(false));
    }

    [Fact]
    public void Equality_IntegerValues()
    {
        Assert.Equal(DynValue.Integer(42), DynValue.Integer(42));
        Assert.NotEqual(DynValue.Integer(42), DynValue.Integer(43));
    }

    [Fact]
    public void Equality_FloatValues()
    {
        Assert.Equal(DynValue.Float(3.14), DynValue.Float(3.14));
    }

    [Fact]
    public void Equality_StringValues()
    {
        Assert.Equal(DynValue.String("hello"), DynValue.String("hello"));
        Assert.NotEqual(DynValue.String("hello"), DynValue.String("world"));
    }

    [Fact]
    public void Equality_DifferentTypes_NotEqual()
    {
        Assert.NotEqual(DynValue.Integer(1), DynValue.Float(1.0));
        Assert.NotEqual(DynValue.String("1"), DynValue.Integer(1));
    }

    [Fact]
    public void Equality_ArrayValues()
    {
        var a = DynValue.Array(new List<DynValue> { DynValue.Integer(1), DynValue.Integer(2) });
        var b = DynValue.Array(new List<DynValue> { DynValue.Integer(1), DynValue.Integer(2) });
        Assert.Equal(a, b);
    }

    [Fact]
    public void Equality_ObjectValues()
    {
        var a = DynValue.Object(new List<KeyValuePair<string, DynValue>> { new("k", DynValue.String("v")) });
        var b = DynValue.Object(new List<KeyValuePair<string, DynValue>> { new("k", DynValue.String("v")) });
        Assert.Equal(a, b);
    }

    [Fact]
    public void Equality_OperatorOverload()
    {
        Assert.True(DynValue.Integer(5) == DynValue.Integer(5));
        Assert.True(DynValue.Integer(5) != DynValue.Integer(6));
    }

    [Fact]
    public void Equality_NullReference()
    {
        DynValue? v = null;
        Assert.True(v == null);
        Assert.False(DynValue.Null() == null);
    }

    [Fact]
    public void GetHashCode_ConsistentForEqualValues()
    {
        var a = DynValue.String("test");
        var b = DynValue.String("test");
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    // ─────────────────────────────────────────────────────────────────
    // ToString
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ToString_Null()
    {
        Assert.Equal("null", DynValue.Null().ToString());
    }

    [Fact]
    public void ToString_Bool()
    {
        Assert.Equal("true", DynValue.Bool(true).ToString());
        Assert.Equal("false", DynValue.Bool(false).ToString());
    }

    [Fact]
    public void ToString_Integer()
    {
        Assert.Equal("42", DynValue.Integer(42).ToString());
    }

    [Fact]
    public void ToString_String()
    {
        Assert.Equal("\"hello\"", DynValue.String("hello").ToString());
    }

    // ─────────────────────────────────────────────────────────────────
    // FromJsonElement
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void FromJsonElement_Null()
    {
        using var doc = JsonDocument.Parse("null");
        var v = DynValue.FromJsonElement(doc.RootElement);
        Assert.True(v.IsNull);
    }

    [Fact]
    public void FromJsonElement_Boolean()
    {
        using var doc = JsonDocument.Parse("true");
        var v = DynValue.FromJsonElement(doc.RootElement);
        Assert.Equal(true, v.AsBool());
    }

    [Fact]
    public void FromJsonElement_Integer()
    {
        using var doc = JsonDocument.Parse("42");
        var v = DynValue.FromJsonElement(doc.RootElement);
        Assert.Equal(42L, v.AsInt64());
    }

    [Fact]
    public void FromJsonElement_Float()
    {
        using var doc = JsonDocument.Parse("3.14");
        var v = DynValue.FromJsonElement(doc.RootElement);
        Assert.Equal(3.14, v.AsDouble());
    }

    [Fact]
    public void FromJsonElement_String()
    {
        using var doc = JsonDocument.Parse("\"hello\"");
        var v = DynValue.FromJsonElement(doc.RootElement);
        Assert.Equal("hello", v.AsString());
    }

    [Fact]
    public void FromJsonElement_Array()
    {
        using var doc = JsonDocument.Parse("[1, 2, 3]");
        var v = DynValue.FromJsonElement(doc.RootElement);
        Assert.Equal(DynValueType.Array, v.Type);
        Assert.Equal(3, v.AsArray()!.Count);
    }

    [Fact]
    public void FromJsonElement_Object()
    {
        using var doc = JsonDocument.Parse("{\"name\": \"Alice\", \"age\": 30}");
        var v = DynValue.FromJsonElement(doc.RootElement);
        Assert.Equal(DynValueType.Object, v.Type);
        Assert.Equal("Alice", v.Get("name")!.AsString());
        Assert.Equal(30L, v.Get("age")!.AsInt64());
    }

    [Fact]
    public void ToJsonElement_RoundTrip()
    {
        var original = DynValue.Object(new List<KeyValuePair<string, DynValue>>
        {
            new("name", DynValue.String("Bob")),
            new("age", DynValue.Integer(25)),
        });
        var element = original.ToJsonElement();
        var back = DynValue.FromJsonElement(element);

        Assert.Equal("Bob", back.Get("name")!.AsString());
        Assert.Equal(25L, back.Get("age")!.AsInt64());
    }
}
