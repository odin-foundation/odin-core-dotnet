using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Odin.Core;
using Odin.Core.Types;
using Odin.Core.Transform;
using Xunit;

namespace Odin.Core.Tests.Unit;

/// <summary>
/// End-to-end integration tests ported from the Rust SDK integration_tests.rs.
/// Covers: roundtrip, parse-all-types, modifiers, error handling, sections,
/// metadata, diff/patch, canonicalize, multi-document, builder, stringify,
/// schema parse/validate, transform integration, string escapes, arrays,
/// consistency, and verb expression integration.
/// </summary>
public class IntegrationTests
{
    // ═══════════════════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════════════════

    private static OdinDocument Roundtrip(string input)
    {
        var doc = Core.Odin.Parse(input);
        var output = Core.Odin.Stringify(doc);
        return Core.Odin.Parse(output);
    }

    private static void AssertConsistent(string input)
    {
        var d1 = Core.Odin.Parse(input);
        var t1 = Core.Odin.Stringify(d1);
        var d2 = Core.Odin.Parse(t1);
        var t2 = Core.Odin.Stringify(d2);
        Assert.Equal(t1, t2);
    }

    private static void AssertPatchRoundtrip(string a, string b)
    {
        var d1 = Core.Odin.Parse(a);
        var d2 = Core.Odin.Parse(b);
        var diff = Core.Odin.Diff(d1, d2);
        var patched = Core.Odin.Patch(d1, diff);
        var diff2 = Core.Odin.Diff(patched, d2);
        Assert.True(diff2.IsEmpty, "Patch did not produce identical document");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Roundtrip: Parse -> Stringify -> Parse
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void Roundtrip_SimpleString()
    {
        var d = Roundtrip("name = \"Alice\"\n");
        Assert.Equal("Alice", d.GetString("name"));
    }

    [Fact]
    public void Roundtrip_Integer()
    {
        var d = Roundtrip("count = ##42\n");
        Assert.Equal(42L, d.GetInteger("count"));
    }

    [Fact]
    public void Roundtrip_NegativeInteger()
    {
        var d = Roundtrip("x = ##-5\n");
        Assert.Equal(-5L, d.GetInteger("x"));
    }

    [Fact]
    public void Roundtrip_ZeroInteger()
    {
        var d = Roundtrip("x = ##0\n");
        Assert.Equal(0L, d.GetInteger("x"));
    }

    [Fact]
    public void Roundtrip_Number()
    {
        var d = Roundtrip("pi = #3.14\n");
        Assert.NotNull(d.GetNumber("pi"));
        Assert.True(Math.Abs(d.GetNumber("pi")!.Value - 3.14) < 0.001);
    }

    [Fact]
    public void Roundtrip_BooleanTrue()
    {
        var d = Roundtrip("active = true\n");
        Assert.Equal(true, d.GetBoolean("active"));
    }

    [Fact]
    public void Roundtrip_BooleanFalse()
    {
        var d = Roundtrip("active = false\n");
        Assert.Equal(false, d.GetBoolean("active"));
    }

    [Fact]
    public void Roundtrip_Null()
    {
        var d = Roundtrip("empty = ~\n");
        Assert.NotNull(d.Get("empty"));
        Assert.True(d.Get("empty")!.IsNull);
    }

    [Fact]
    public void Roundtrip_Currency()
    {
        var d = Roundtrip("price = #$99.99\n");
        Assert.True(d.Get("price")!.IsCurrency);
    }

    [Fact]
    public void Roundtrip_Percent()
    {
        var d = Roundtrip("rate = #%50\n");
        Assert.True(d.Get("rate")!.IsPercent);
    }

    [Fact]
    public void Roundtrip_Date()
    {
        var d = Roundtrip("born = 2024-01-15\n");
        Assert.True(d.Get("born")!.IsDate);
    }

    [Fact]
    public void Roundtrip_Timestamp()
    {
        var d = Roundtrip("ts = 2024-01-15T10:30:00Z\n");
        Assert.True(d.Get("ts")!.IsTimestamp);
    }

    [Fact]
    public void Roundtrip_Time()
    {
        var d = Roundtrip("t = T10:30:00\n");
        Assert.True(d.Get("t")!.IsTemporal);
    }

    [Fact]
    public void Roundtrip_Duration()
    {
        var d = Roundtrip("dur = P1Y2M3D\n");
        Assert.NotNull(d.Get("dur"));
    }

    [Fact]
    public void Roundtrip_Reference()
    {
        var d = Roundtrip("ref = @other.path\n");
        Assert.True(d.Get("ref")!.IsReference);
    }

    [Fact]
    public void Roundtrip_Binary()
    {
        var d = Roundtrip("data = ^SGVsbG8=\n");
        Assert.True(d.Get("data")!.IsBinary);
    }

    [Fact]
    public void Roundtrip_EmptyString()
    {
        var d = Roundtrip("x = \"\"\n");
        Assert.Equal("", d.GetString("x"));
    }

    [Fact]
    public void Roundtrip_StringWithSpaces()
    {
        var d = Roundtrip("x = \"hello world\"\n");
        Assert.Equal("hello world", d.GetString("x"));
    }

    [Fact]
    public void Roundtrip_StringWithEscape()
    {
        var d = Roundtrip("x = \"line\\nbreak\"\n");
        Assert.NotNull(d.GetString("x"));
    }

    [Fact]
    public void Roundtrip_LargeInteger()
    {
        var d = Roundtrip("x = ##1000000\n");
        Assert.Equal(1000000L, d.GetInteger("x"));
    }

    [Fact]
    public void Roundtrip_Section()
    {
        var d = Roundtrip("{Section}\nfield = \"value\"\n");
        Assert.Equal("value", d.GetString("Section.field"));
    }

    [Fact]
    public void Roundtrip_NestedSection()
    {
        var d = Roundtrip("{A}\n{A.B}\nfield = ##42\n");
        Assert.Equal(42L, d.GetInteger("A.B.field"));
    }

    [Fact]
    public void Roundtrip_Array()
    {
        var d = Roundtrip("items[0] = \"a\"\nitems[1] = \"b\"\n");
        Assert.Equal("a", d.GetString("items[0]"));
        Assert.Equal("b", d.GetString("items[1]"));
    }

    [Fact]
    public void Roundtrip_MultipleFields()
    {
        var d = Roundtrip("a = \"one\"\nb = ##2\nc = true\nd = ~\n");
        Assert.Equal("one", d.GetString("a"));
        Assert.Equal(2L, d.GetInteger("b"));
        Assert.Equal(true, d.GetBoolean("c"));
        Assert.True(d.Get("d")!.IsNull);
    }

    [Fact]
    public void Roundtrip_RequiredModifier()
    {
        var d = Roundtrip("name = !\"Alice\"\n");
        Assert.True(d.Get("name")!.IsRequired);
        Assert.Equal("Alice", d.GetString("name"));
    }

    [Fact]
    public void Roundtrip_ConfidentialModifier()
    {
        var d = Roundtrip("ssn = *\"123-45-6789\"\n");
        Assert.True(d.Get("ssn")!.IsConfidential);
    }

    [Fact]
    public void Roundtrip_DeprecatedModifier()
    {
        var d = Roundtrip("old = -\"legacy\"\n");
        Assert.True(d.Get("old")!.IsDeprecated);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Parse All Types
    // ═══════════════════════════════════════════════════════════════════════

    [Fact] public void Parse_String() { var d = Core.Odin.Parse("x = \"hello\"\n"); Assert.Equal("hello", d.GetString("x")); }
    [Fact] public void Parse_EmptyString() { var d = Core.Odin.Parse("x = \"\"\n"); Assert.Equal("", d.GetString("x")); }
    [Fact] public void Parse_IntegerPositive() { var d = Core.Odin.Parse("x = ##42\n"); Assert.Equal(42L, d.GetInteger("x")); }
    [Fact] public void Parse_IntegerNegative() { var d = Core.Odin.Parse("x = ##-10\n"); Assert.Equal(-10L, d.GetInteger("x")); }
    [Fact] public void Parse_IntegerZero() { var d = Core.Odin.Parse("x = ##0\n"); Assert.Equal(0L, d.GetInteger("x")); }
    [Fact] public void Parse_IntegerLarge() { var d = Core.Odin.Parse("x = ##999999999\n"); Assert.Equal(999999999L, d.GetInteger("x")); }

    [Fact]
    public void Parse_NumberDecimal()
    {
        var d = Core.Odin.Parse("x = #3.14\n");
        Assert.True(Math.Abs(d.GetNumber("x")!.Value - 3.14) < 0.001);
    }

    [Fact]
    public void Parse_NumberNegative()
    {
        var d = Core.Odin.Parse("x = #-1.5\n");
        Assert.True(Math.Abs(d.GetNumber("x")!.Value + 1.5) < 0.001);
    }

    [Fact]
    public void Parse_NumberZero()
    {
        var d = Core.Odin.Parse("x = #0.0\n");
        Assert.True(Math.Abs(d.GetNumber("x")!.Value) < 0.001);
    }

    [Fact] public void Parse_BoolTrue() { var d = Core.Odin.Parse("x = true\n"); Assert.Equal(true, d.GetBoolean("x")); }
    [Fact] public void Parse_BoolFalse() { var d = Core.Odin.Parse("x = false\n"); Assert.Equal(false, d.GetBoolean("x")); }
    [Fact] public void Parse_Null() { var d = Core.Odin.Parse("x = ~\n"); Assert.True(d.Get("x")!.IsNull); }
    [Fact] public void Parse_Currency() { var d = Core.Odin.Parse("x = #$100.00\n"); Assert.True(d.Get("x")!.IsCurrency); }
    [Fact] public void Parse_CurrencyZero() { var d = Core.Odin.Parse("x = #$0.00\n"); Assert.True(d.Get("x")!.IsCurrency); }
    [Fact] public void Parse_Percent() { var d = Core.Odin.Parse("x = #%75\n"); Assert.True(d.Get("x")!.IsPercent); }
    [Fact] public void Parse_PercentDecimal() { var d = Core.Odin.Parse("x = #%99.9\n"); Assert.True(d.Get("x")!.IsPercent); }
    [Fact] public void Parse_Date() { var d = Core.Odin.Parse("x = 2024-01-15\n"); Assert.True(d.Get("x")!.IsDate); }
    [Fact] public void Parse_DateLeap() { var d = Core.Odin.Parse("x = 2024-02-29\n"); Assert.True(d.Get("x")!.IsDate); }
    [Fact] public void Parse_TimestampUtc() { var d = Core.Odin.Parse("x = 2024-01-15T10:30:00Z\n"); Assert.True(d.Get("x")!.IsTimestamp); }
    [Fact] public void Parse_TimestampOffset() { var d = Core.Odin.Parse("x = 2024-01-15T10:30:00+05:30\n"); Assert.True(d.Get("x")!.IsTimestamp); }
    [Fact] public void Parse_TimestampNegOffset() { var d = Core.Odin.Parse("x = 2024-01-15T10:30:00-08:00\n"); Assert.True(d.Get("x")!.IsTimestamp); }
    [Fact] public void Parse_Time() { var d = Core.Odin.Parse("x = T10:30:00\n"); Assert.True(d.Get("x")!.IsTemporal); }
    [Fact] public void Parse_TimeMidnight() { var d = Core.Odin.Parse("x = T00:00:00\n"); Assert.True(d.Get("x")!.IsTemporal); }
    [Fact] public void Parse_DurationDays() { var d = Core.Odin.Parse("x = P30D\n"); Assert.NotNull(d.Get("x")); }
    [Fact] public void Parse_DurationHours() { var d = Core.Odin.Parse("x = PT24H\n"); Assert.NotNull(d.Get("x")); }
    [Fact] public void Parse_DurationFull() { var d = Core.Odin.Parse("x = P1Y2M3DT4H5M6S\n"); Assert.NotNull(d.Get("x")); }
    [Fact] public void Parse_Reference() { var d = Core.Odin.Parse("x = @other\n"); Assert.True(d.Get("x")!.IsReference); }
    [Fact] public void Parse_ReferenceDotted() { var d = Core.Odin.Parse("x = @path.to.thing\n"); Assert.True(d.Get("x")!.IsReference); }
    [Fact] public void Parse_Binary() { var d = Core.Odin.Parse("x = ^SGVsbG8=\n"); Assert.True(d.Get("x")!.IsBinary); }

    // ═══════════════════════════════════════════════════════════════════════
    // Modifier Tests
    // ═══════════════════════════════════════════════════════════════════════

    [Fact] public void Modifier_RequiredString() { var d = Core.Odin.Parse("x = !\"val\"\n"); Assert.True(d.Get("x")!.IsRequired); }
    [Fact] public void Modifier_ConfidentialString() { var d = Core.Odin.Parse("x = *\"secret\"\n"); Assert.True(d.Get("x")!.IsConfidential); }
    [Fact] public void Modifier_DeprecatedString() { var d = Core.Odin.Parse("x = -\"old\"\n"); Assert.True(d.Get("x")!.IsDeprecated); }

    [Fact]
    public void Modifier_RequiredInteger()
    {
        var d = Core.Odin.Parse("x = !##42\n");
        Assert.True(d.Get("x")!.IsRequired);
        Assert.Equal(42L, d.GetInteger("x"));
    }

    [Fact] public void Modifier_ConfidentialInteger() { var d = Core.Odin.Parse("x = *##42\n"); Assert.True(d.Get("x")!.IsConfidential); }
    [Fact] public void Modifier_RequiredBoolean() { var d = Core.Odin.Parse("x = !true\n"); Assert.True(d.Get("x")!.IsRequired); }
    [Fact] public void Modifier_ConfidentialNull() { var d = Core.Odin.Parse("x = *~\n"); Assert.True(d.Get("x")!.IsConfidential); }

    [Fact]
    public void Modifier_RequiredCurrency()
    {
        var d = Core.Odin.Parse("x = !#$99.99\n");
        Assert.True(d.Get("x")!.IsRequired);
        Assert.True(d.Get("x")!.IsCurrency);
    }

    [Fact]
    public void Modifier_CombinedRequiredConfidential()
    {
        var d = Core.Odin.Parse("x = !*\"val\"\n");
        var v = d.Get("x")!;
        Assert.True(v.IsRequired);
        Assert.True(v.IsConfidential);
    }

    [Fact]
    public void Modifier_CombinedAllThree()
    {
        var d = Core.Odin.Parse("x = !-*\"val\"\n");
        var v = d.Get("x")!;
        Assert.True(v.IsRequired);
        Assert.True(v.IsDeprecated);
        Assert.True(v.IsConfidential);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Error Handling
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void Error_ParseEmptyInput()
    {
        var d = Core.Odin.Parse("");
        Assert.Equal(0, d.Assignments.Count);
    }

    [Fact]
    public void Error_ParseOnlyWhitespace()
    {
        var d = Core.Odin.Parse("   \n\n  \n");
        Assert.Equal(0, d.Assignments.Count);
    }

    [Fact]
    public void Error_ParseOnlyComments()
    {
        var d = Core.Odin.Parse("; comment\n; another\n");
        Assert.Equal(0, d.Assignments.Count);
    }

    [Fact]
    public void Error_UnterminatedString()
    {
        Assert.ThrowsAny<Exception>(() => Core.Odin.Parse("x = \"unterminated\n"));
    }

    [Fact]
    public void Error_NegativeArrayIndex()
    {
        Assert.ThrowsAny<Exception>(() => Core.Odin.Parse("items[-1] = \"bad\"\n"));
    }

    [Fact]
    public void Error_NonContiguousArray()
    {
        Assert.ThrowsAny<Exception>(() => Core.Odin.Parse("items[0] = \"a\"\nitems[2] = \"c\"\n"));
    }

    [Fact]
    public void Error_MissingEquals()
    {
        Assert.ThrowsAny<Exception>(() => Core.Odin.Parse("x \"value\"\n"));
    }

    [Fact]
    public void Error_InvalidNumberPrefix()
    {
        Assert.ThrowsAny<Exception>(() => Core.Odin.Parse("x = #abc\n"));
    }

    [Fact]
    public void Error_InvalidIntegerPrefix()
    {
        Assert.ThrowsAny<Exception>(() => Core.Odin.Parse("x = ##abc\n"));
    }

    [Fact]
    public void Error_UnterminatedSection()
    {
        Assert.ThrowsAny<Exception>(() => Core.Odin.Parse("{Unterminated\nx = ##1\n"));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Section Tests
    // ═══════════════════════════════════════════════════════════════════════

    [Fact] public void Section_Simple() { var d = Core.Odin.Parse("{Person}\nname = \"Alice\"\n"); Assert.Equal("Alice", d.GetString("Person.name")); }
    [Fact] public void Section_Nested() { var d = Core.Odin.Parse("{A}\n{A.B}\nfield = ##1\n"); Assert.Equal(1L, d.GetInteger("A.B.field")); }

    [Fact]
    public void Section_Multiple()
    {
        var d = Core.Odin.Parse("{A}\nx = ##1\n{B}\ny = ##2\n");
        Assert.Equal(1L, d.GetInteger("A.x"));
        Assert.Equal(2L, d.GetInteger("B.y"));
    }

    [Fact]
    public void Section_WithArray()
    {
        var d = Core.Odin.Parse("{S}\nitems[0] = \"a\"\nitems[1] = \"b\"\n");
        Assert.Equal("a", d.GetString("S.items[0]"));
    }

    [Fact]
    public void Section_MultipleFields()
    {
        var d = Core.Odin.Parse("{Config}\na = ##1\nb = ##2\nc = ##3\n");
        Assert.Equal(1L, d.GetInteger("Config.a"));
        Assert.Equal(3L, d.GetInteger("Config.c"));
    }

    [Fact]
    public void Section_DeeplyNested()
    {
        var d = Core.Odin.Parse("{A}\n{A.B}\n{A.B.C}\nf = ##1\n");
        Assert.Equal(1L, d.GetInteger("A.B.C.f"));
    }

    [Fact]
    public void Section_WithModifiers()
    {
        var d = Core.Odin.Parse("{Secure}\npassword = *\"secret\"\nid = !##42\n");
        Assert.True(d.Get("Secure.password")!.IsConfidential);
        Assert.True(d.Get("Secure.id")!.IsRequired);
    }

    [Fact]
    public void Section_WithComments()
    {
        var d = Core.Odin.Parse("; top comment\n{Section}\n; field comment\nf = ##1\n");
        Assert.Equal(1L, d.GetInteger("Section.f"));
    }

    [Fact]
    public void Section_ManySections()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < 20; i++)
            sb.Append($"{{S{i}}}\nfield = ##{i}\n");
        var d = Core.Odin.Parse(sb.ToString());
        Assert.Equal(0L, d.GetInteger("S0.field"));
        Assert.Equal(19L, d.GetInteger("S19.field"));
    }

    [Fact]
    public void Section_WithArrays()
    {
        var d = Core.Odin.Parse("{List}\nitems[0] = \"first\"\nitems[1] = \"second\"\nitems[2] = \"third\"\n");
        Assert.Equal("first", d.GetString("List.items[0]"));
        Assert.Equal("third", d.GetString("List.items[2]"));
    }

    [Fact]
    public void Section_RootFieldBeforeSection()
    {
        var d = Core.Odin.Parse("top = ##1\n{S}\nbottom = ##2\n");
        Assert.Equal(1L, d.GetInteger("top"));
        Assert.Equal(2L, d.GetInteger("S.bottom"));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Metadata Tests
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void Metadata_ParseSection()
    {
        var d = Core.Odin.Parse("{$}\nodin = \"1.0.0\"\n\nname = \"doc\"\n");
        Assert.Equal("doc", d.GetString("name"));
    }

    [Fact]
    public void Metadata_Version()
    {
        var d = Core.Odin.Parse("{$}\nodin = \"1.0.0\"\n");
        Assert.NotNull(d.Metadata);
        Assert.True(d.Metadata.Count > 0);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Diff Integration
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void Diff_Identical()
    {
        var d1 = Core.Odin.Parse("x = ##1\n");
        var d2 = Core.Odin.Parse("x = ##1\n");
        var diff = Core.Odin.Diff(d1, d2);
        Assert.True(diff.IsEmpty);
    }

    [Fact]
    public void Diff_Added()
    {
        var d1 = Core.Odin.Parse("x = ##1\n");
        var d2 = Core.Odin.Parse("x = ##1\ny = ##2\n");
        var diff = Core.Odin.Diff(d1, d2);
        Assert.True(diff.Added.Count > 0);
    }

    [Fact]
    public void Diff_Removed()
    {
        var d1 = Core.Odin.Parse("x = ##1\ny = ##2\n");
        var d2 = Core.Odin.Parse("x = ##1\n");
        var diff = Core.Odin.Diff(d1, d2);
        Assert.True(diff.Removed.Count > 0);
    }

    [Fact]
    public void Diff_Changed()
    {
        var d1 = Core.Odin.Parse("x = ##1\n");
        var d2 = Core.Odin.Parse("x = ##2\n");
        var diff = Core.Odin.Diff(d1, d2);
        Assert.True(diff.Changed.Count > 0);
    }

    [Fact]
    public void Diff_PatchRoundtrip()
    {
        var d1 = Core.Odin.Parse("name = \"Alice\"\nage = ##25\n");
        var d2 = Core.Odin.Parse("name = \"Bob\"\nage = ##30\n");
        var diff = Core.Odin.Diff(d1, d2);
        var patched = Core.Odin.Patch(d1, diff);
        Assert.Equal("Bob", patched.GetString("name"));
        Assert.Equal(30L, patched.GetInteger("age"));
    }

    [Fact]
    public void Diff_PatchThenDiffEmpty()
    {
        var d1 = Core.Odin.Parse("x = ##1\ny = ##2\n");
        var d2 = Core.Odin.Parse("x = ##10\ny = ##20\n");
        var diff = Core.Odin.Diff(d1, d2);
        var patched = Core.Odin.Patch(d1, diff);
        var diff2 = Core.Odin.Diff(patched, d2);
        Assert.True(diff2.IsEmpty);
    }

    [Fact]
    public void Diff_EmptyToPopulated()
    {
        var d1 = Core.Odin.Parse("");
        var d2 = Core.Odin.Parse("x = ##1\n");
        var diff = Core.Odin.Diff(d1, d2);
        Assert.True(diff.Added.Count > 0);
    }

    [Fact]
    public void Diff_PopulatedToEmpty()
    {
        var d1 = Core.Odin.Parse("x = ##1\n");
        var d2 = Core.Odin.Parse("");
        var diff = Core.Odin.Diff(d1, d2);
        Assert.True(diff.Removed.Count > 0);
    }

    [Fact]
    public void Diff_TypeChange()
    {
        var d1 = Core.Odin.Parse("x = \"42\"\n");
        var d2 = Core.Odin.Parse("x = ##42\n");
        var diff = Core.Odin.Diff(d1, d2);
        Assert.True(diff.Changed.Count > 0);
    }

    [Fact]
    public void Diff_StringToString()
    {
        var d1 = Core.Odin.Parse("x = \"hello\"\n");
        var d2 = Core.Odin.Parse("x = \"world\"\n");
        var diff = Core.Odin.Diff(d1, d2);
        Assert.True(diff.Changed.Count > 0);
    }

    [Fact]
    public void Diff_BooleanChange()
    {
        var d1 = Core.Odin.Parse("x = true\n");
        var d2 = Core.Odin.Parse("x = false\n");
        var diff = Core.Odin.Diff(d1, d2);
        Assert.True(diff.Changed.Count > 0);
    }

    [Fact]
    public void Diff_MultipleAdds()
    {
        var d1 = Core.Odin.Parse("");
        var d2 = Core.Odin.Parse("a = ##1\nb = ##2\nc = ##3\n");
        var diff = Core.Odin.Diff(d1, d2);
        Assert.True(diff.Added.Count >= 3);
    }

    [Fact]
    public void Diff_MultipleRemoves()
    {
        var d1 = Core.Odin.Parse("a = ##1\nb = ##2\nc = ##3\n");
        var d2 = Core.Odin.Parse("");
        var diff = Core.Odin.Diff(d1, d2);
        Assert.True(diff.Removed.Count >= 3);
    }

    [Fact]
    public void Diff_PatchAddField()
    {
        var d1 = Core.Odin.Parse("x = ##1\n");
        var d2 = Core.Odin.Parse("x = ##1\ny = ##2\n");
        var diff = Core.Odin.Diff(d1, d2);
        var patched = Core.Odin.Patch(d1, diff);
        Assert.Equal(2L, patched.GetInteger("y"));
    }

    [Fact]
    public void Diff_PatchRemoveField()
    {
        var d1 = Core.Odin.Parse("x = ##1\ny = ##2\n");
        var d2 = Core.Odin.Parse("x = ##1\n");
        var diff = Core.Odin.Diff(d1, d2);
        var patched = Core.Odin.Patch(d1, diff);
        Assert.Null(patched.Get("y"));
    }

    [Fact]
    public void Diff_PatchChangeValue()
    {
        var d1 = Core.Odin.Parse("x = \"old\"\n");
        var d2 = Core.Odin.Parse("x = \"new\"\n");
        var diff = Core.Odin.Diff(d1, d2);
        var patched = Core.Odin.Patch(d1, diff);
        Assert.Equal("new", patched.GetString("x"));
    }

    [Fact]
    public void Diff_SectionFieldAdded()
    {
        var d1 = Core.Odin.Parse("{S}\na = ##1\n");
        var d2 = Core.Odin.Parse("{S}\na = ##1\nb = ##2\n");
        var diff = Core.Odin.Diff(d1, d2);
        Assert.True(diff.Added.Count > 0);
    }

    [Fact]
    public void Diff_SectionFieldRemoved()
    {
        var d1 = Core.Odin.Parse("{S}\na = ##1\nb = ##2\n");
        var d2 = Core.Odin.Parse("{S}\na = ##1\n");
        var diff = Core.Odin.Diff(d1, d2);
        Assert.True(diff.Removed.Count > 0);
    }

    [Fact]
    public void Diff_SectionFieldChanged()
    {
        var d1 = Core.Odin.Parse("{S}\na = ##1\n");
        var d2 = Core.Odin.Parse("{S}\na = ##99\n");
        var diff = Core.Odin.Diff(d1, d2);
        Assert.True(diff.Changed.Count > 0);
    }

    [Fact]
    public void Diff_PatchRoundtripComplex()
    {
        var d1 = Core.Odin.Parse("{A}\nx = ##1\ny = \"hello\"\n{B}\nz = true\n");
        var d2 = Core.Odin.Parse("{A}\nx = ##99\ny = \"world\"\n{B}\nz = false\nw = ##42\n");
        var diff = Core.Odin.Diff(d1, d2);
        var patched = Core.Odin.Patch(d1, diff);
        Assert.Equal(99L, patched.GetInteger("A.x"));
        Assert.Equal("world", patched.GetString("A.y"));
        Assert.Equal(false, patched.GetBoolean("B.z"));
    }

    [Fact]
    public void Diff_NullToValue()
    {
        var d1 = Core.Odin.Parse("x = ~\n");
        var d2 = Core.Odin.Parse("x = ##42\n");
        var diff = Core.Odin.Diff(d1, d2);
        Assert.True(diff.Changed.Count > 0);
    }

    [Fact]
    public void Diff_ValueToNull()
    {
        var d1 = Core.Odin.Parse("x = ##42\n");
        var d2 = Core.Odin.Parse("x = ~\n");
        var diff = Core.Odin.Diff(d1, d2);
        Assert.True(diff.Changed.Count > 0);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Extended Diff-Patch Roundtrip
    // ═══════════════════════════════════════════════════════════════════════

    [Fact] public void PatchRt_Add() => AssertPatchRoundtrip("x = ##1\n", "x = ##1\ny = ##2\n");
    [Fact] public void PatchRt_Remove() => AssertPatchRoundtrip("x = ##1\ny = ##2\n", "x = ##1\n");
    [Fact] public void PatchRt_ChangeInt() => AssertPatchRoundtrip("x = ##1\n", "x = ##99\n");
    [Fact] public void PatchRt_ChangeStr() => AssertPatchRoundtrip("x = \"old\"\n", "x = \"new\"\n");
    [Fact] public void PatchRt_ChangeBool() => AssertPatchRoundtrip("x = true\n", "x = false\n");
    [Fact] public void PatchRt_ChangeType() => AssertPatchRoundtrip("x = \"str\"\n", "x = ##42\n");
    [Fact] public void PatchRt_ToNull() => AssertPatchRoundtrip("x = ##42\n", "x = ~\n");
    [Fact] public void PatchRt_FromNull() => AssertPatchRoundtrip("x = ~\n", "x = ##42\n");

    [Fact]
    public void PatchRt_MultiField() => AssertPatchRoundtrip(
        "a = ##1\nb = ##2\nc = ##3\n",
        "a = ##10\nb = ##20\nd = ##4\n");

    [Fact]
    public void PatchRt_SectionChange() => AssertPatchRoundtrip(
        "{S}\nf = ##1\n",
        "{S}\nf = ##99\n");

    [Fact]
    public void PatchRt_SectionAddField() => AssertPatchRoundtrip(
        "{S}\na = ##1\n",
        "{S}\na = ##1\nb = ##2\n");

    // ═══════════════════════════════════════════════════════════════════════
    // Canonicalize Tests
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void Canonical_Deterministic()
    {
        var d = Core.Odin.Parse("b = ##2\na = ##1\n");
        var c1 = Core.Odin.Canonicalize(d);
        var c2 = Core.Odin.Canonicalize(d);
        Assert.Equal(c1, c2);
    }

    [Fact]
    public void Canonical_SortedFields()
    {
        var d = Core.Odin.Parse("z = \"z\"\na = \"a\"\nm = \"m\"\n");
        var c = Encoding.UTF8.GetString(Core.Odin.Canonicalize(d));
        var aPos = c.IndexOf("a =", StringComparison.Ordinal);
        var mPos = c.IndexOf("m =", StringComparison.Ordinal);
        var zPos = c.IndexOf("z =", StringComparison.Ordinal);
        Assert.True(aPos < mPos && mPos < zPos);
    }

    [Fact]
    public void Canonical_DifferentDocsDifferent()
    {
        var d1 = Core.Odin.Parse("x = ##1\n");
        var d2 = Core.Odin.Parse("x = ##2\n");
        Assert.NotEqual(Core.Odin.Canonicalize(d1), Core.Odin.Canonicalize(d2));
    }

    [Fact]
    public void Canonical_EmptyDoc()
    {
        var d = Core.Odin.Parse("");
        // Should not throw
        _ = Core.Odin.Canonicalize(d);
    }

    [Fact]
    public void Canonical_WithSection()
    {
        var d = Core.Odin.Parse("{S}\nf = \"v\"\n");
        var c = Encoding.UTF8.GetString(Core.Odin.Canonicalize(d));
        Assert.Contains("f", c);
    }

    [Fact]
    public void Canonical_KeyOrderingStable()
    {
        var d1 = Core.Odin.Parse("z = ##1\na = ##2\nm = ##3\n");
        var d2 = Core.Odin.Parse("a = ##2\nm = ##3\nz = ##1\n");
        Assert.Equal(Core.Odin.Canonicalize(d1), Core.Odin.Canonicalize(d2));
    }

    [Fact]
    public void Canonical_SameValueSameOutput()
    {
        var d1 = Core.Odin.Parse("x = ##42\n");
        var d2 = Core.Odin.Parse("x = ##42\n");
        Assert.Equal(Core.Odin.Canonicalize(d1), Core.Odin.Canonicalize(d2));
    }

    [Fact]
    public void Canonical_DifferentValuesDifferent()
    {
        var d1 = Core.Odin.Parse("x = \"a\"\n");
        var d2 = Core.Odin.Parse("x = \"b\"\n");
        Assert.NotEqual(Core.Odin.Canonicalize(d1), Core.Odin.Canonicalize(d2));
    }

    [Fact]
    public void Canonical_DifferentKeysDifferent()
    {
        var d1 = Core.Odin.Parse("a = ##1\n");
        var d2 = Core.Odin.Parse("b = ##1\n");
        Assert.NotEqual(Core.Odin.Canonicalize(d1), Core.Odin.Canonicalize(d2));
    }

    [Fact]
    public void Canonical_SectionOrdering()
    {
        var d1 = Core.Odin.Parse("{B}\nf = ##1\n{A}\nf = ##2\n");
        var d2 = Core.Odin.Parse("{A}\nf = ##2\n{B}\nf = ##1\n");
        Assert.Equal(Core.Odin.Canonicalize(d1), Core.Odin.Canonicalize(d2));
    }

    [Fact]
    public void Canonical_WithArrays()
    {
        var d = Core.Odin.Parse("items[0] = \"x\"\nitems[1] = \"y\"\n");
        // Should not throw
        _ = Core.Odin.Canonicalize(d);
    }

    [Fact]
    public void Canonical_ManyFields()
    {
        var sb = new StringBuilder();
        for (int i = 25; i >= 0; i--)
        {
            char ch = (char)('a' + i);
            sb.Append($"{ch} = ##{i}\n");
        }
        var d = Core.Odin.Parse(sb.ToString());
        var c = Encoding.UTF8.GetString(Core.Odin.Canonicalize(d));
        var aPos = c.IndexOf("a =", StringComparison.Ordinal);
        var zPos = c.IndexOf("z =", StringComparison.Ordinal);
        Assert.True(aPos < zPos);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Multi-Document Tests
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void MultiDoc_Single()
    {
        var docs = Core.Odin.ParseDocuments("x = ##1\n");
        Assert.Single(docs);
    }

    [Fact]
    public void MultiDoc_Two()
    {
        var docs = Core.Odin.ParseDocuments("x = ##1\n---\ny = ##2\n");
        Assert.Equal(2, docs.Count);
        Assert.Equal(1L, docs[0].GetInteger("x"));
        Assert.Equal(2L, docs[1].GetInteger("y"));
    }

    [Fact]
    public void MultiDoc_Three()
    {
        var docs = Core.Odin.ParseDocuments("a = ##1\n---\nb = ##2\n---\nc = ##3\n");
        Assert.Equal(3, docs.Count);
    }

    [Fact]
    public void MultiDoc_WithSections()
    {
        var docs = Core.Odin.ParseDocuments("{A}\nx = ##1\n---\n{B}\ny = ##2\n");
        Assert.Equal(2, docs.Count);
    }

    [Fact]
    public void MultiDoc_EmptyYieldsOne()
    {
        var docs = Core.Odin.ParseDocuments("");
        Assert.Single(docs);
    }

    [Fact]
    public void MultiDoc_Five()
    {
        var docs = Core.Odin.ParseDocuments("a = ##1\n---\nb = ##2\n---\nc = ##3\n---\nd = ##4\n---\ne = ##5\n");
        Assert.Equal(5, docs.Count);
    }

    [Fact]
    public void MultiDoc_WithDifferentSections()
    {
        var docs = Core.Odin.ParseDocuments("{A}\nf = ##1\n---\n{B}\nf = ##2\n---\n{C}\nf = ##3\n");
        Assert.Equal(3, docs.Count);
        Assert.Equal(1L, docs[0].GetInteger("A.f"));
        Assert.Equal(2L, docs[1].GetInteger("B.f"));
        Assert.Equal(3L, docs[2].GetInteger("C.f"));
    }

    [Fact]
    public void MultiDoc_ChainFieldsIndependent()
    {
        var docs = Core.Odin.ParseDocuments("x = ##1\ny = ##2\n---\nz = ##3\n");
        Assert.Equal(1L, docs[0].GetInteger("x"));
        Assert.Equal(2L, docs[0].GetInteger("y"));
        Assert.Null(docs[1].Get("x"));
        Assert.Equal(3L, docs[1].GetInteger("z"));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Builder Tests
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void Builder_String()
    {
        var d = Core.Odin.Builder().Set("x", OdinValues.String("hi")).Build();
        Assert.Equal("hi", d.GetString("x"));
    }

    [Fact]
    public void Builder_Integer()
    {
        var d = Core.Odin.Builder().Set("x", OdinValues.Integer(42)).Build();
        Assert.Equal(42L, d.GetInteger("x"));
    }

    [Fact]
    public void Builder_Number()
    {
        var d = Core.Odin.Builder().Set("x", OdinValues.Number(3.14)).Build();
        Assert.True(Math.Abs(d.GetNumber("x")!.Value - 3.14) < 0.001);
    }

    [Fact]
    public void Builder_Boolean()
    {
        var d = Core.Odin.Builder().Set("x", OdinValues.Boolean(true)).Build();
        Assert.Equal(true, d.GetBoolean("x"));
    }

    [Fact]
    public void Builder_Null()
    {
        var d = Core.Odin.Builder().SetNull("x").Build();
        Assert.True(d.Get("x")!.IsNull);
    }

    [Fact]
    public void Builder_Empty()
    {
        var d = Core.Odin.Builder().Build();
        Assert.Equal(0, d.Assignments.Count);
    }

    [Fact]
    public void Builder_SectionPath()
    {
        var d = Core.Odin.Builder().Set("S.f", OdinValues.String("v")).Build();
        Assert.Equal("v", d.GetString("S.f"));
    }

    [Fact]
    public void Builder_Overwrite()
    {
        var d = Core.Odin.Builder()
            .Set("x", OdinValues.String("a"))
            .Set("x", OdinValues.String("b"))
            .Build();
        Assert.Equal("b", d.GetString("x"));
    }

    [Fact]
    public void Builder_Multiple()
    {
        var d = Core.Odin.Builder()
            .SetString("a", "1")
            .SetInteger("b", 2)
            .SetBoolean("c", true)
            .Build();
        Assert.Equal("1", d.GetString("a"));
        Assert.Equal(2L, d.GetInteger("b"));
        Assert.Equal(true, d.GetBoolean("c"));
    }

    [Fact]
    public void Builder_Currency()
    {
        var d = Core.Odin.Builder().SetCurrency("price", 99.99).Build();
        Assert.True(d.Get("price")!.IsCurrency);
    }

    [Fact]
    public void Builder_Date()
    {
        var d = Core.Odin.Builder().Set("born", OdinValues.Date(2024, 1, 15)).Build();
        Assert.True(d.Get("born")!.IsDate);
    }

    [Fact]
    public void Builder_Reference()
    {
        var d = Core.Odin.Builder().Set("ref", OdinValues.Reference("other.path")).Build();
        Assert.True(d.Get("ref")!.IsReference);
    }

    [Fact]
    public void Builder_Binary()
    {
        var d = Core.Odin.Builder().Set("data", OdinValues.Binary(new byte[] { 72, 101, 108, 108, 111 })).Build();
        Assert.True(d.Get("data")!.IsBinary);
    }

    [Fact]
    public void Builder_ManyFields()
    {
        var b = Core.Odin.Builder();
        for (int i = 0; i < 50; i++)
            b = b.SetInteger($"field_{i}", i);
        var d = b.Build();
        Assert.Equal(0L, d.GetInteger("field_0"));
        Assert.Equal(49L, d.GetInteger("field_49"));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Builder -> Stringify -> Parse Roundtrip
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void BuilderRoundtrip_String()
    {
        var d = Core.Odin.Builder().SetString("name", "Alice").Build();
        var text = Core.Odin.Stringify(d);
        var d2 = Core.Odin.Parse(text);
        Assert.Equal("Alice", d2.GetString("name"));
    }

    [Fact]
    public void BuilderRoundtrip_Integer()
    {
        var d = Core.Odin.Builder().SetInteger("n", -5).Build();
        var text = Core.Odin.Stringify(d);
        var d2 = Core.Odin.Parse(text);
        Assert.Equal(-5L, d2.GetInteger("n"));
    }

    [Fact]
    public void BuilderRoundtrip_AllTypes()
    {
        var d = Core.Odin.Builder()
            .SetString("s", "test")
            .SetInteger("i", 42)
            .SetNumber("n", 3.14)
            .SetBoolean("b", false)
            .SetNull("null")
            .Build();
        var text = Core.Odin.Stringify(d);
        var d2 = Core.Odin.Parse(text);
        Assert.Equal("test", d2.GetString("s"));
        Assert.Equal(42L, d2.GetInteger("i"));
        Assert.Equal(false, d2.GetBoolean("b"));
        Assert.True(d2.Get("null")!.IsNull);
    }

    [Fact]
    public void BuilderRoundtrip_SectionPath()
    {
        var d = Core.Odin.Builder()
            .SetString("S.name", "test")
            .SetInteger("S.value", 42)
            .Build();
        var text = Core.Odin.Stringify(d);
        var d2 = Core.Odin.Parse(text);
        Assert.Equal("test", d2.GetString("S.name"));
        Assert.Equal(42L, d2.GetInteger("S.value"));
    }

    [Fact]
    public void BuilderRoundtrip_MultipleSections()
    {
        var d = Core.Odin.Builder()
            .SetInteger("A.x", 1)
            .SetInteger("B.y", 2)
            .Build();
        var text = Core.Odin.Stringify(d);
        var d2 = Core.Odin.Parse(text);
        Assert.Equal(1L, d2.GetInteger("A.x"));
        Assert.Equal(2L, d2.GetInteger("B.y"));
    }

    [Fact]
    public void BuilderRoundtrip_ManyTypes()
    {
        var d = Core.Odin.Builder()
            .SetString("str", "hello")
            .SetInteger("int", 42)
            .SetNumber("num", 3.14)
            .SetBoolean("bool_t", true)
            .SetBoolean("bool_f", false)
            .SetNull("null")
            .SetCurrency("curr", 9.99)
            .Set("ref", OdinValues.Reference("other"))
            .Build();
        var text = Core.Odin.Stringify(d);
        var d2 = Core.Odin.Parse(text);
        Assert.Equal("hello", d2.GetString("str"));
        Assert.Equal(42L, d2.GetInteger("int"));
        Assert.Equal(true, d2.GetBoolean("bool_t"));
        Assert.Equal(false, d2.GetBoolean("bool_f"));
        Assert.True(d2.Get("null")!.IsNull);
        Assert.True(d2.Get("curr")!.IsCurrency);
        Assert.True(d2.Get("ref")!.IsReference);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Stringify Tests
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void Stringify_IntegerPrefix()
    {
        var d = Core.Odin.Builder().SetInteger("x", 42).Build();
        var t = Core.Odin.Stringify(d);
        Assert.Contains("##42", t);
    }

    [Fact]
    public void Stringify_Boolean()
    {
        var d = Core.Odin.Builder().SetBoolean("x", true).Build();
        var t = Core.Odin.Stringify(d);
        Assert.Contains("true", t);
    }

    [Fact]
    public void Stringify_Null()
    {
        var d = Core.Odin.Builder().SetNull("x").Build();
        var t = Core.Odin.Stringify(d);
        Assert.Contains("~", t);
    }

    [Fact]
    public void Stringify_QuotedString()
    {
        var d = Core.Odin.Builder().SetString("x", "hello").Build();
        var t = Core.Odin.Stringify(d);
        Assert.Contains("\"hello\"", t);
    }

    [Fact]
    public void Stringify_EmptyDoc()
    {
        var d = Core.Odin.Builder().Build();
        var t = Core.Odin.Stringify(d);
        Assert.True(string.IsNullOrWhiteSpace(t));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Schema Parse & Validate
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void Schema_ParseWithTypes()
    {
        var s = Core.Odin.ParseSchema("{@Person}\nname = \"\"\nage = ##\n");
        Assert.True(s.Types.ContainsKey("Person"));
    }

    [Fact]
    public void Schema_ParseMultipleTypes()
    {
        var s = Core.Odin.ParseSchema("{@A}\nx = \"\"\n{@B}\ny = ##\n");
        Assert.True(s.Types.ContainsKey("A"));
        Assert.True(s.Types.ContainsKey("B"));
    }

    [Fact]
    public void Schema_ParseEmpty()
    {
        var s = Core.Odin.ParseSchema("");
        Assert.Empty(s.Types);
    }

    [Fact]
    public void Schema_ParseComments()
    {
        var s = Core.Odin.ParseSchema("; comment\n");
        Assert.Empty(s.Types);
    }

    [Fact]
    public void Schema_BooleanField()
    {
        var s = Core.Odin.ParseSchema("{@Config}\nenabled = ?\n");
        Assert.True(s.Types.ContainsKey("Config"));
    }

    [Fact]
    public void Schema_ValidateCorrectType()
    {
        var schema = Core.Odin.ParseSchema("{Person}\nname = \"\"\n");
        var doc = Core.Odin.Parse("Person.name = \"Alice\"\n");
        var result = Core.Odin.Validate(doc, schema);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Schema_ValidateWrongType()
    {
        var schema = Core.Odin.ParseSchema("{Person}\nname = \"\"\n");
        var doc = Core.Odin.Parse("Person.name = ##42\n");
        var result = Core.Odin.Validate(doc, schema);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Schema_ValidateEmptyDocPasses()
    {
        var schema = Core.Odin.ParseSchema("{Person}\nname = \"\"\n");
        var doc = Core.Odin.Parse("");
        var result = Core.Odin.Validate(doc, schema);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Schema_ValidateIntegerFieldCorrect()
    {
        var schema = Core.Odin.ParseSchema("{Person}\nage = ##\n");
        var doc = Core.Odin.Parse("Person.age = ##25\n");
        var result = Core.Odin.Validate(doc, schema);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Schema_ValidateIntegerFieldWrongType()
    {
        var schema = Core.Odin.ParseSchema("{Person}\nage = ##\n");
        var doc = Core.Odin.Parse("Person.age = \"twenty-five\"\n");
        var result = Core.Odin.Validate(doc, schema);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Schema_ValidateBooleanFieldCorrect()
    {
        var schema = Core.Odin.ParseSchema("{Config}\nenabled = ?\n");
        var doc = Core.Odin.Parse("Config.enabled = true\n");
        var result = Core.Odin.Validate(doc, schema);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Schema_ValidateBooleanFieldWrong()
    {
        var schema = Core.Odin.ParseSchema("{Config}\nenabled = ?\n");
        var doc = Core.Odin.Parse("Config.enabled = \"yes\"\n");
        var result = Core.Odin.Validate(doc, schema);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Schema_ValidateNumberFieldCorrect()
    {
        var schema = Core.Odin.ParseSchema("{Measure}\nweight = #\n");
        var doc = Core.Odin.Parse("Measure.weight = #72.5\n");
        var result = Core.Odin.Validate(doc, schema);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Schema_ValidateNumberFieldWrong()
    {
        var schema = Core.Odin.ParseSchema("{Measure}\nweight = #\n");
        var doc = Core.Odin.Parse("Measure.weight = \"heavy\"\n");
        var result = Core.Odin.Validate(doc, schema);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Schema_ValidateMultipleFieldsAllCorrect()
    {
        var schema = Core.Odin.ParseSchema("{Person}\nname = \"\"\nage = ##\nactive = ?\n");
        var doc = Core.Odin.Parse("Person.name = \"Alice\"\nPerson.age = ##30\nPerson.active = true\n");
        var result = Core.Odin.Validate(doc, schema);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Schema_ValidateMultipleFieldsOneWrong()
    {
        var schema = Core.Odin.ParseSchema("{Person}\nname = \"\"\nage = ##\n");
        var doc = Core.Odin.Parse("Person.name = \"Alice\"\nPerson.age = \"thirty\"\n");
        var result = Core.Odin.Validate(doc, schema);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Schema_ValidateExtraFieldsPass()
    {
        var schema = Core.Odin.ParseSchema("{Person}\nname = \"\"\n");
        var doc = Core.Odin.Parse("Person.name = \"Alice\"\nPerson.extra = ##42\n");
        var result = Core.Odin.Validate(doc, schema);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Schema_ValidateCurrencyCorrect()
    {
        var schema = Core.Odin.ParseSchema("{Order}\ntotal = #$\n");
        var doc = Core.Odin.Parse("Order.total = #$99.99\n");
        var result = Core.Odin.Validate(doc, schema);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Schema_ValidateCurrencyWrongType()
    {
        var schema = Core.Odin.ParseSchema("{Order}\ntotal = #$\n");
        var doc = Core.Odin.Parse("Order.total = ##99\n");
        var result = Core.Odin.Validate(doc, schema);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Schema_EmptyValidatesAnything()
    {
        var schema = Core.Odin.ParseSchema("");
        var doc = Core.Odin.Parse("x = ##42\ny = \"hello\"\n");
        var result = Core.Odin.Validate(doc, schema);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Schema_ValidateStringWhereIntExpected()
    {
        var schema = Core.Odin.ParseSchema("{Data}\ncount = ##\n");
        var doc = Core.Odin.Parse("Data.count = \"not a number\"\n");
        var result = Core.Odin.Validate(doc, schema);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Schema_ValidateIntWhereStringExpected()
    {
        var schema = Core.Odin.ParseSchema("{Data}\nname = \"\"\n");
        var doc = Core.Odin.Parse("Data.name = ##42\n");
        var result = Core.Odin.Validate(doc, schema);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Schema_ValidateBoolWhereNumberExpected()
    {
        var schema = Core.Odin.ParseSchema("{Data}\nval = #\n");
        var doc = Core.Odin.Parse("Data.val = true\n");
        var result = Core.Odin.Validate(doc, schema);
        Assert.False(result.IsValid);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Comment Tests
    // ═══════════════════════════════════════════════════════════════════════

    [Fact] public void Comment_LineIgnored() { var d = Core.Odin.Parse("; this is a comment\nx = ##1\n"); Assert.Equal(1L, d.GetInteger("x")); }
    [Fact] public void Comment_Multiple() { var d = Core.Odin.Parse("; c1\n; c2\n; c3\nx = ##1\n"); Assert.Equal(1L, d.GetInteger("x")); }

    [Fact]
    public void Comment_BetweenFields()
    {
        var d = Core.Odin.Parse("a = ##1\n; comment\nb = ##2\n");
        Assert.Equal(1L, d.GetInteger("a"));
        Assert.Equal(2L, d.GetInteger("b"));
    }

    [Fact]
    public void Comment_Inline()
    {
        var d = Core.Odin.Parse("x = ##42 ; inline\n");
        Assert.Equal(42L, d.GetInteger("x"));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // String Escape Tests
    // ═══════════════════════════════════════════════════════════════════════

    [Fact] public void Escape_Newline() { var d = Core.Odin.Parse("x = \"a\\nb\"\n"); Assert.Equal("a\nb", d.GetString("x")); }
    [Fact] public void Escape_Tab() { var d = Core.Odin.Parse("x = \"a\\tb\"\n"); Assert.Equal("a\tb", d.GetString("x")); }
    [Fact] public void Escape_Backslash() { var d = Core.Odin.Parse("x = \"a\\\\b\"\n"); Assert.Equal("a\\b", d.GetString("x")); }
    [Fact] public void Escape_Quote() { var d = Core.Odin.Parse("x = \"a\\\"b\"\n"); Assert.Equal("a\"b", d.GetString("x")); }
    [Fact] public void Escape_CarriageReturn() { var d = Core.Odin.Parse("x = \"a\\rb\"\n"); Assert.Equal("a\rb", d.GetString("x")); }

    [Fact]
    public void Escape_Multiple()
    {
        var d = Core.Odin.Parse("x = \"a\\n\\tb\\\\c\"\n");
        Assert.Equal("a\n\tb\\c", d.GetString("x"));
    }

    [Fact]
    public void Escape_UnicodeInString()
    {
        var d = Core.Odin.Parse("x = \"hello \U0001F30D\"\n");
        Assert.Equal("hello \U0001F30D", d.GetString("x"));
    }

    [Fact]
    public void Escape_CjkInString()
    {
        var d = Core.Odin.Parse("x = \"\u65E5\u672C\u8A9E\"\n");
        Assert.Equal("\u65E5\u672C\u8A9E", d.GetString("x"));
    }

    [Fact] public void Escape_EmptyString() { var d = Core.Odin.Parse("x = \"\"\n"); Assert.Equal("", d.GetString("x")); }
    [Fact] public void Escape_StringWithSpaces() { var d = Core.Odin.Parse("x = \"  spaces  \"\n"); Assert.Equal("  spaces  ", d.GetString("x")); }
    [Fact] public void Escape_StringWithSemicolon() { var d = Core.Odin.Parse("x = \"has ; semicolon\"\n"); Assert.Equal("has ; semicolon", d.GetString("x")); }
    [Fact] public void Escape_StringWithEquals() { var d = Core.Odin.Parse("x = \"a = b\"\n"); Assert.Equal("a = b", d.GetString("x")); }
    [Fact] public void Escape_StringWithBraces() { var d = Core.Odin.Parse("x = \"{not a section}\"\n"); Assert.Equal("{not a section}", d.GetString("x")); }
    [Fact] public void Escape_StringWithHash() { var d = Core.Odin.Parse("x = \"#not a number\"\n"); Assert.Equal("#not a number", d.GetString("x")); }

    // ═══════════════════════════════════════════════════════════════════════
    // Array Tests
    // ═══════════════════════════════════════════════════════════════════════

    [Fact] public void Array_SingleElement() { var d = Core.Odin.Parse("items[0] = \"only\"\n"); Assert.Equal("only", d.GetString("items[0]")); }

    [Fact]
    public void Array_ThreeElements()
    {
        var d = Core.Odin.Parse("a[0] = ##1\na[1] = ##2\na[2] = ##3\n");
        Assert.Equal(1L, d.GetInteger("a[0]"));
        Assert.Equal(3L, d.GetInteger("a[2]"));
    }

    [Fact]
    public void Array_StringArray()
    {
        var d = Core.Odin.Parse("tags[0] = \"red\"\ntags[1] = \"blue\"\n");
        Assert.Equal("red", d.GetString("tags[0]"));
        Assert.Equal("blue", d.GetString("tags[1]"));
    }

    [Fact]
    public void Array_MixedTypes()
    {
        var d = Core.Odin.Parse("mix[0] = \"str\"\nmix[1] = ##42\nmix[2] = true\n");
        Assert.Equal("str", d.GetString("mix[0]"));
        Assert.Equal(42L, d.GetInteger("mix[1]"));
        Assert.Equal(true, d.GetBoolean("mix[2]"));
    }

    [Fact]
    public void Array_InSection()
    {
        var d = Core.Odin.Parse("{Data}\nitems[0] = ##10\nitems[1] = ##20\n");
        Assert.Equal(10L, d.GetInteger("Data.items[0]"));
        Assert.Equal(20L, d.GetInteger("Data.items[1]"));
    }

    [Fact]
    public void Array_MultipleArrays()
    {
        var d = Core.Odin.Parse("a[0] = ##1\na[1] = ##2\nb[0] = \"x\"\nb[1] = \"y\"\n");
        Assert.Equal(1L, d.GetInteger("a[0]"));
        Assert.Equal("x", d.GetString("b[0]"));
    }

    [Fact]
    public void Array_LargeArray()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < 20; i++)
            sb.Append($"items[{i}] = ##{i}\n");
        var d = Core.Odin.Parse(sb.ToString());
        Assert.Equal(0L, d.GetInteger("items[0]"));
        Assert.Equal(19L, d.GetInteger("items[19]"));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Extended Roundtrip Tests
    // ═══════════════════════════════════════════════════════════════════════

    [Fact] public void RtExt_StringWithNewline() { var d = Roundtrip("x = \"line1\\nline2\"\n"); Assert.Contains("\n", d.GetString("x")); }
    [Fact] public void RtExt_StringWithTab() { var d = Roundtrip("x = \"col1\\tcol2\"\n"); Assert.Contains("\t", d.GetString("x")); }
    [Fact] public void RtExt_StringWithBackslash() { var d = Roundtrip("x = \"path\\\\to\\\\file\"\n"); Assert.Contains("\\", d.GetString("x")); }
    [Fact] public void RtExt_StringWithQuotes() { var d = Roundtrip("x = \"say \\\"hello\\\"\"\n"); Assert.Contains("\"", d.GetString("x")); }

    [Fact]
    public void RtExt_NegativeNumber()
    {
        var d = Roundtrip("x = #-99.5\n");
        Assert.True(Math.Abs(d.GetNumber("x")!.Value + 99.5) < 0.01);
    }

    [Fact] public void RtExt_VeryLargeInteger() { var d = Roundtrip("x = ##2147483647\n"); Assert.Equal(2147483647L, d.GetInteger("x")); }
    [Fact] public void RtExt_NegativeLargeInteger() { var d = Roundtrip("x = ##-2147483648\n"); Assert.Equal(-2147483648L, d.GetInteger("x")); }
    [Fact] public void RtExt_CurrencyCents() { var d = Roundtrip("x = #$0.01\n"); Assert.True(d.Get("x")!.IsCurrency); }
    [Fact] public void RtExt_CurrencyLarge() { var d = Roundtrip("x = #$999999.99\n"); Assert.True(d.Get("x")!.IsCurrency); }
    [Fact] public void RtExt_PercentZero() { var d = Roundtrip("x = #%0\n"); Assert.True(d.Get("x")!.IsPercent); }
    [Fact] public void RtExt_PercentHundred() { var d = Roundtrip("x = #%100\n"); Assert.True(d.Get("x")!.IsPercent); }
    [Fact] public void RtExt_DateEndOfYear() { var d = Roundtrip("x = 2024-12-31\n"); Assert.True(d.Get("x")!.IsDate); }
    [Fact] public void RtExt_DateStartOfYear() { var d = Roundtrip("x = 2024-01-01\n"); Assert.True(d.Get("x")!.IsDate); }
    [Fact] public void RtExt_TimestampWithMillis() { var d = Roundtrip("x = 2024-06-15T14:30:00.123Z\n"); Assert.True(d.Get("x")!.IsTimestamp); }
    [Fact] public void RtExt_DurationComplex() { var d = Roundtrip("x = P1Y2M3DT4H5M6S\n"); Assert.NotNull(d.Get("x")); }
    [Fact] public void RtExt_ReferenceSimple() { var d = Roundtrip("x = @target\n"); Assert.True(d.Get("x")!.IsReference); }
    [Fact] public void RtExt_ReferenceNested() { var d = Roundtrip("x = @a.b.c.d\n"); Assert.True(d.Get("x")!.IsReference); }

    [Fact]
    public void RtExt_SectionWithAllTypes()
    {
        var d = Roundtrip("{Data}\ns = \"text\"\ni = ##10\nn = #2.5\nb = true\nnull = ~\nc = #$50.00\n");
        Assert.Equal("text", d.GetString("Data.s"));
        Assert.Equal(10L, d.GetInteger("Data.i"));
        Assert.Equal(true, d.GetBoolean("Data.b"));
        Assert.True(d.Get("Data.null")!.IsNull);
    }

    [Fact]
    public void RtExt_MultipleSectionsWithData()
    {
        var d = Roundtrip("{A}\na1 = ##1\na2 = ##2\n{B}\nb1 = \"x\"\nb2 = \"y\"\n{C}\nc1 = true\n");
        Assert.Equal(1L, d.GetInteger("A.a1"));
        Assert.Equal("x", d.GetString("B.b1"));
        Assert.Equal(true, d.GetBoolean("C.c1"));
    }

    [Fact]
    public void RtExt_ArrayOfIntegers()
    {
        var d = Roundtrip("nums[0] = ##1\nnums[1] = ##2\nnums[2] = ##3\n");
        Assert.Equal(1L, d.GetInteger("nums[0]"));
        Assert.Equal(2L, d.GetInteger("nums[1]"));
        Assert.Equal(3L, d.GetInteger("nums[2]"));
    }

    [Fact]
    public void RtExt_ArrayOfStrings()
    {
        var d = Roundtrip("tags[0] = \"a\"\ntags[1] = \"b\"\ntags[2] = \"c\"\n");
        Assert.Equal("a", d.GetString("tags[0]"));
        Assert.Equal("c", d.GetString("tags[2]"));
    }

    [Fact]
    public void RtExt_MixedRootAndSections()
    {
        var d = Roundtrip("root_field = \"top\"\n{S}\nsection_field = ##42\n");
        Assert.Equal("top", d.GetString("root_field"));
        Assert.Equal(42L, d.GetInteger("S.section_field"));
    }

    [Fact] public void RtExt_ModifierRequiredNumber() { var d = Roundtrip("x = !#3.14\n"); Assert.True(d.Get("x")!.IsRequired); }
    [Fact] public void RtExt_ModifierConfidentialCurrency() { var d = Roundtrip("x = *#$100.00\n"); Assert.True(d.Get("x")!.IsConfidential); }
    [Fact] public void RtExt_ModifierDeprecatedBoolean() { var d = Roundtrip("x = -true\n"); Assert.True(d.Get("x")!.IsDeprecated); }

    // ═══════════════════════════════════════════════════════════════════════
    // Consistency Tests (Parse -> Stringify -> Parse -> Stringify stable)
    // ═══════════════════════════════════════════════════════════════════════

    [Fact] public void Stable_String() => AssertConsistent("x = \"hello\"\n");
    [Fact] public void Stable_Integer() => AssertConsistent("x = ##42\n");
    [Fact] public void Stable_NegInteger() => AssertConsistent("x = ##-5\n");
    [Fact] public void Stable_Number() => AssertConsistent("x = #3.14\n");
    [Fact] public void Stable_BooleanTrue() => AssertConsistent("x = true\n");
    [Fact] public void Stable_BooleanFalse() => AssertConsistent("x = false\n");
    [Fact] public void Stable_Null() => AssertConsistent("x = ~\n");
    [Fact] public void Stable_Currency() => AssertConsistent("x = #$99.99\n");
    [Fact] public void Stable_Percent() => AssertConsistent("x = #%50\n");
    [Fact] public void Stable_Date() => AssertConsistent("x = 2024-01-15\n");
    [Fact] public void Stable_Timestamp() => AssertConsistent("x = 2024-01-15T10:30:00Z\n");
    [Fact] public void Stable_Reference() => AssertConsistent("x = @other\n");
    [Fact] public void Stable_Binary() => AssertConsistent("x = ^SGVsbG8=\n");
    [Fact] public void Stable_Section() => AssertConsistent("{S}\nf = ##1\n");
    [Fact] public void Stable_NestedSection() => AssertConsistent("{A}\n{A.B}\nf = ##1\n");
    [Fact] public void Stable_Array() => AssertConsistent("items[0] = \"a\"\nitems[1] = \"b\"\n");
    [Fact] public void Stable_Required() => AssertConsistent("x = !\"val\"\n");
    [Fact] public void Stable_Confidential() => AssertConsistent("x = *\"secret\"\n");
    [Fact] public void Stable_Deprecated() => AssertConsistent("x = -\"old\"\n");

    [Fact]
    public void Stable_Complex() => AssertConsistent(
        "{$}\nodin = \"1.0.0\"\n\nname = \"test\"\nage = ##25\nactive = true\nprice = #$49.99\n{Address}\nstreet = \"123 Main\"\ncity = \"Portland\"\n");

    [Fact]
    public void Stable_MultiSection() => AssertConsistent("{A}\na = ##1\n{B}\nb = ##2\n{C}\nc = ##3\n");

    [Fact]
    public void Stable_ArrayInSection() => AssertConsistent("{S}\nitems[0] = \"x\"\nitems[1] = \"y\"\nitems[2] = \"z\"\n");

    // ═══════════════════════════════════════════════════════════════════════
    // Transform Integration
    // ═══════════════════════════════════════════════════════════════════════

    private static string Header() =>
        "{$}\nodin = \"1.0.0\"\ntransform = \"1.0.0\"\ndirection = \"json->json\"\ntarget.format = \"json\"\n\n";

    [Fact]
    public void Transform_SimpleCopy()
    {
        var tText = Header() + "{Output}\nName = \"@.name\"\n";
        var src = DynValue.Object(new List<KeyValuePair<string, DynValue>>
        {
            new("name", DynValue.String("Alice"))
        });
        var r = Core.Odin.ExecuteTransform(tText, src);
        Assert.True(r.Success);
    }

    [Fact]
    public void Transform_LiteralValue()
    {
        var tText = Header() + "{Output}\nStatus = \"active\"\n";
        var src = DynValue.Object(new List<KeyValuePair<string, DynValue>>());
        var r = Core.Odin.ExecuteTransform(tText, src);
        Assert.True(r.Success);
    }

    [Fact]
    public void Transform_NestedSource()
    {
        var tText = Header() + "{Output}\nCity = \"@.address.city\"\n";
        var src = DynValue.Object(new List<KeyValuePair<string, DynValue>>
        {
            new("address", DynValue.Object(new List<KeyValuePair<string, DynValue>>
            {
                new("city", DynValue.String("Portland"))
            }))
        });
        var r = Core.Odin.ExecuteTransform(tText, src);
        Assert.True(r.Success);
    }

    [Fact]
    public void Transform_MultiField()
    {
        var tText = Header() + "{Output}\nA = \"@.a\"\nB = \"@.b\"\n";
        var src = DynValue.Object(new List<KeyValuePair<string, DynValue>>
        {
            new("a", DynValue.String("x")),
            new("b", DynValue.Integer(42))
        });
        var r = Core.Odin.ExecuteTransform(tText, src);
        Assert.True(r.Success);
    }

    [Fact]
    public void Transform_ParseEmpty()
    {
        var t = Core.Odin.ParseTransform(Header());
        Assert.NotNull(t);
    }

    [Fact]
    public void Transform_ParseSingleMapping()
    {
        var tText = Header() + "{Output}\nName = \"@.name\"\n";
        var t = Core.Odin.ParseTransform(tText);
        Assert.True(t.Segments.Count > 0);
    }

    [Fact]
    public void Transform_ParseMultipleMappings()
    {
        var tText = Header() + "{Output}\nA = \"@.a\"\nB = \"@.b\"\nC = \"@.c\"\n";
        var t = Core.Odin.ParseTransform(tText);
        Assert.True(t.Segments[0].Mappings.Count >= 3);
    }

    [Fact]
    public void Transform_ParseDirection()
    {
        var t = Core.Odin.ParseTransform(Header());
        Assert.Equal("json->json", t.Metadata.Direction);
    }

    [Fact]
    public void Transform_ParseOdinToJsonDirection()
    {
        var tText = "{$}\nodin = \"1.0.0\"\ntransform = \"1.0.0\"\ndirection = \"odin->json\"\ntarget.format = \"json\"\n\n";
        var t = Core.Odin.ParseTransform(tText);
        Assert.Equal("odin->json", t.Metadata.Direction);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Stringify Parse Roundtrip Preserves Values
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void StringifyParseRoundtrip_PreservesValues()
    {
        var input = "name = \"Alice\"\nage = ##30\nactive = true\n";
        var d = Core.Odin.Parse(input);
        var text = Core.Odin.Stringify(d);
        var d2 = Core.Odin.Parse(text);
        Assert.Equal("Alice", d2.GetString("name"));
        Assert.Equal(30L, d2.GetInteger("age"));
        Assert.Equal(true, d2.GetBoolean("active"));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Type Value Roundtrip via Builder
    // ═══════════════════════════════════════════════════════════════════════

    private static OdinDocument BuilderRoundtrip(string key, OdinValue val)
    {
        var d = Core.Odin.Builder().Set(key, val).Build();
        var text = Core.Odin.Stringify(d);
        return Core.Odin.Parse(text);
    }

    [Fact] public void TypeRt_StringEmpty() { var d = BuilderRoundtrip("x", OdinValues.String("")); Assert.Equal("", d.GetString("x")); }
    [Fact] public void TypeRt_StringSpaces() { var d = BuilderRoundtrip("x", OdinValues.String("  ")); Assert.Equal("  ", d.GetString("x")); }

    [Fact]
    public void TypeRt_StringLong()
    {
        var s = new string('a', 500);
        var d = BuilderRoundtrip("x", OdinValues.String(s));
        Assert.Equal(500, d.GetString("x")!.Length);
    }

    [Fact] public void TypeRt_IntegerZero() { var d = BuilderRoundtrip("x", OdinValues.Integer(0)); Assert.Equal(0L, d.GetInteger("x")); }
    [Fact] public void TypeRt_IntegerOne() { var d = BuilderRoundtrip("x", OdinValues.Integer(1)); Assert.Equal(1L, d.GetInteger("x")); }
    [Fact] public void TypeRt_IntegerNegOne() { var d = BuilderRoundtrip("x", OdinValues.Integer(-1)); Assert.Equal(-1L, d.GetInteger("x")); }
    [Fact] public void TypeRt_IntegerLarge() { var d = BuilderRoundtrip("x", OdinValues.Integer(999999)); Assert.Equal(999999L, d.GetInteger("x")); }
    [Fact] public void TypeRt_IntegerNegLarge() { var d = BuilderRoundtrip("x", OdinValues.Integer(-999999)); Assert.Equal(-999999L, d.GetInteger("x")); }

    [Fact]
    public void TypeRt_NumberPi()
    {
        var d = BuilderRoundtrip("x", OdinValues.Number(3.14159));
        Assert.True(Math.Abs(d.GetNumber("x")!.Value - 3.14159) < 0.001);
    }

    [Fact]
    public void TypeRt_NumberZero()
    {
        var d = BuilderRoundtrip("x", OdinValues.Number(0.0));
        Assert.True(Math.Abs(d.GetNumber("x")!.Value) < 0.001);
    }

    [Fact]
    public void TypeRt_NumberNegative()
    {
        var d = BuilderRoundtrip("x", OdinValues.Number(-42.5));
        Assert.True(Math.Abs(d.GetNumber("x")!.Value + 42.5) < 0.1);
    }

    [Fact] public void TypeRt_BooleanTrue() { var d = BuilderRoundtrip("x", OdinValues.Boolean(true)); Assert.Equal(true, d.GetBoolean("x")); }
    [Fact] public void TypeRt_BooleanFalse() { var d = BuilderRoundtrip("x", OdinValues.Boolean(false)); Assert.Equal(false, d.GetBoolean("x")); }
    [Fact] public void TypeRt_NullVal() { var d = BuilderRoundtrip("x", OdinValues.Null()); Assert.True(d.Get("x")!.IsNull); }
    [Fact] public void TypeRt_CurrencySmall() { var d = BuilderRoundtrip("x", OdinValues.Currency(0.01, 2)); Assert.True(d.Get("x")!.IsCurrency); }
    [Fact] public void TypeRt_CurrencyLarge() { var d = BuilderRoundtrip("x", OdinValues.Currency(99999.99, 2)); Assert.True(d.Get("x")!.IsCurrency); }
    [Fact] public void TypeRt_PercentHalf() { var d = BuilderRoundtrip("x", OdinValues.Percent(0.5)); Assert.True(d.Get("x")!.IsPercent); }
    [Fact] public void TypeRt_PercentFull() { var d = BuilderRoundtrip("x", OdinValues.Percent(1.0)); Assert.True(d.Get("x")!.IsPercent); }
    [Fact] public void TypeRt_DateVal() { var d = BuilderRoundtrip("x", OdinValues.Date(2024, 6, 15)); Assert.True(d.Get("x")!.IsDate); }
    [Fact] public void TypeRt_ReferenceVal() { var d = BuilderRoundtrip("x", OdinValues.Reference("other")); Assert.True(d.Get("x")!.IsReference); }
    [Fact] public void TypeRt_BinaryVal() { var d = BuilderRoundtrip("x", OdinValues.Binary(new byte[] { 1, 2, 3 })); Assert.True(d.Get("x")!.IsBinary); }

    // ═══════════════════════════════════════════════════════════════════════
    // Large Document Handling
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void LargeDoc_ManyFields()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < 100; i++)
            sb.Append($"field_{i} = ##{i}\n");
        var d = Core.Odin.Parse(sb.ToString());
        Assert.Equal(0L, d.GetInteger("field_0"));
        Assert.Equal(99L, d.GetInteger("field_99"));
    }

    [Fact]
    public void LargeDoc_ManySections()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < 50; i++)
            sb.Append($"{{Section{i}}}\nvalue = ##{i}\n");
        var d = Core.Odin.Parse(sb.ToString());
        Assert.Equal(0L, d.GetInteger("Section0.value"));
        Assert.Equal(49L, d.GetInteger("Section49.value"));
    }

    [Fact]
    public void LargeDoc_LargeArray()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < 100; i++)
            sb.Append($"items[{i}] = ##{i}\n");
        var d = Core.Odin.Parse(sb.ToString());
        Assert.Equal(0L, d.GetInteger("items[0]"));
        Assert.Equal(99L, d.GetInteger("items[99]"));
    }

    [Fact]
    public void LargeDoc_LongStringValue()
    {
        var longStr = new string('x', 10000);
        var d = Core.Odin.Parse($"data = \"{longStr}\"\n");
        Assert.Equal(10000, d.GetString("data")!.Length);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Edge Cases
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void Edge_ParseAndStringifyEmpty()
    {
        var d = Core.Odin.Parse("");
        var text = Core.Odin.Stringify(d);
        var d2 = Core.Odin.Parse(text);
        Assert.Equal(0, d2.Assignments.Count);
    }

    [Fact]
    public void Edge_ImmutableDocument()
    {
        var d = Core.Odin.Parse("x = ##1\n");
        var d2 = d.With("y", OdinValues.Integer(2));
        // Original unchanged
        Assert.Null(d.Get("y"));
        // New document has both
        Assert.Equal(1L, d2.GetInteger("x"));
        Assert.Equal(2L, d2.GetInteger("y"));
    }

    [Fact]
    public void Edge_DocumentWithout()
    {
        var d = Core.Odin.Parse("x = ##1\ny = ##2\n");
        var d2 = d.Without("y");
        Assert.Null(d2.Get("y"));
        Assert.Equal(1L, d2.GetInteger("x"));
        // Original unchanged
        Assert.Equal(2L, d.GetInteger("y"));
    }

    [Fact]
    public void Edge_DocumentHas()
    {
        var d = Core.Odin.Parse("x = ##1\n");
        Assert.True(d.Has("x"));
        Assert.False(d.Has("y"));
    }

    [Fact]
    public void Edge_DocumentPaths()
    {
        var d = Core.Odin.Parse("a = ##1\nb = ##2\nc = ##3\n");
        var paths = d.Paths();
        Assert.Equal(3, paths.Count);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Multi-Step Workflows
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void Workflow_ParseValidateDiffPatch()
    {
        // Step 1: Parse two documents
        var d1 = Core.Odin.Parse("name = \"Alice\"\nage = ##25\n");
        var d2 = Core.Odin.Parse("name = \"Bob\"\nage = ##30\nactive = true\n");

        // Step 2: Validate against schema
        var schema = Core.Odin.ParseSchema("{Person}\nname = \"\"\nage = ##\n");
        Assert.True(Core.Odin.Validate(d1, schema).IsValid);
        Assert.True(Core.Odin.Validate(d2, schema).IsValid);

        // Step 3: Diff
        var diff = Core.Odin.Diff(d1, d2);
        Assert.False(diff.IsEmpty);

        // Step 4: Patch
        var patched = Core.Odin.Patch(d1, diff);
        Assert.Equal("Bob", patched.GetString("name"));
        Assert.Equal(30L, patched.GetInteger("age"));

        // Step 5: Verify diff is empty after patch
        var diff2 = Core.Odin.Diff(patched, d2);
        Assert.True(diff2.IsEmpty);
    }

    [Fact]
    public void Workflow_BuildStringifyParseCanonicalizeCompare()
    {
        // Build document
        var d1 = Core.Odin.Builder()
            .SetString("z_name", "Alice")
            .SetInteger("a_age", 25)
            .SetBoolean("m_active", true)
            .Build();

        // Stringify and parse back
        var text = Core.Odin.Stringify(d1);
        var d2 = Core.Odin.Parse(text);

        // Canonicalize both
        var c1 = Core.Odin.Canonicalize(d1);
        var c2 = Core.Odin.Canonicalize(d2);
        Assert.Equal(c1, c2);
    }

    [Fact]
    public void Workflow_TransformThenValidate()
    {
        // Parse a transform and execute
        var tText = Header() + "{Person}\nName = \"@.name\"\nAge = \"@.age\"\n";
        var src = DynValue.Object(new List<KeyValuePair<string, DynValue>>
        {
            new("name", DynValue.String("Alice")),
            new("age", DynValue.Integer(30))
        });
        var result = Core.Odin.ExecuteTransform(tText, src);
        Assert.True(result.Success);
    }

    [Fact]
    public void Workflow_ParseDocumentChain()
    {
        // Parse multi-document, diff each consecutive pair
        var docs = Core.Odin.ParseDocuments("x = ##1\n---\nx = ##2\n---\nx = ##3\n");
        Assert.Equal(3, docs.Count);

        var diff01 = Core.Odin.Diff(docs[0], docs[1]);
        Assert.True(diff01.Changed.Count > 0);

        var diff12 = Core.Odin.Diff(docs[1], docs[2]);
        Assert.True(diff12.Changed.Count > 0);
    }

    [Fact]
    public void Workflow_ExportToJson()
    {
        // ToJson groups by section; use a section header for proper export
        var d = Core.Odin.Parse("{Person}\nname = \"Alice\"\nage = ##30\n");
        var json = Core.Odin.ToJson(d);
        Assert.Contains("Alice", json);
        Assert.Contains("30", json);
    }

    [Fact]
    public void Workflow_ExportToXml()
    {
        var d = Core.Odin.Parse("{Person}\nname = \"Alice\"\n");
        var xml = Core.Odin.ToXml(d);
        Assert.Contains("Alice", xml);
    }
}
