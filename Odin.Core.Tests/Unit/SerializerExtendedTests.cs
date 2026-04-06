using System;
using System.Text;
using Odin.Core;
using Odin.Core.Types;
using Xunit;

namespace Odin.Core.Tests.Unit;

public class SerializerExtendedTests
{
    // ─────────────────────────────────────────────────────────────────
    // Stringify Round-Trips
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void RoundTrip_Currency()
    {
        var doc = Core.Odin.Parse("price = #$49.99");
        var output = Core.Odin.Stringify(doc, new StringifyOptions { IncludeMetadata = false });
        var reparsed = Core.Odin.Parse(output);
        Assert.True(reparsed.Get("price")!.IsCurrency);
    }

    [Fact]
    public void RoundTrip_Percent()
    {
        var doc = Core.Odin.Parse("rate = #%0.15");
        var output = Core.Odin.Stringify(doc, new StringifyOptions { IncludeMetadata = false });
        var reparsed = Core.Odin.Parse(output);
        Assert.True(reparsed.Get("rate")!.IsPercent);
    }

    [Fact]
    public void RoundTrip_Date()
    {
        var doc = Core.Odin.Parse("dob = 2024-06-15");
        var output = Core.Odin.Stringify(doc, new StringifyOptions { IncludeMetadata = false });
        var reparsed = Core.Odin.Parse(output);
        var val = reparsed.Get("dob") as OdinDate;
        Assert.NotNull(val);
        Assert.Equal(2024, val!.Year);
    }

    [Fact]
    public void RoundTrip_Reference()
    {
        var doc = Core.Odin.Parse("ref = @other.path");
        var output = Core.Odin.Stringify(doc, new StringifyOptions { IncludeMetadata = false });
        var reparsed = Core.Odin.Parse(output);
        Assert.Equal("other.path", reparsed.Get("ref")!.AsReference());
    }

    [Fact]
    public void RoundTrip_Binary()
    {
        var doc = Core.Odin.Parse("data = ^SGVsbG8=");
        var output = Core.Odin.Stringify(doc, new StringifyOptions { IncludeMetadata = false });
        var reparsed = Core.Odin.Parse(output);
        Assert.True(reparsed.Get("data")!.IsBinary);
    }

    [Fact]
    public void RoundTrip_NegativeInteger()
    {
        var doc = Core.Odin.Parse("x = ##-42");
        var output = Core.Odin.Stringify(doc, new StringifyOptions { IncludeMetadata = false });
        var reparsed = Core.Odin.Parse(output);
        Assert.Equal(-42L, reparsed.GetInteger("x"));
    }

    [Fact]
    public void RoundTrip_NegativeNumber()
    {
        var doc = Core.Odin.Parse("x = #-3.14");
        var output = Core.Odin.Stringify(doc, new StringifyOptions { IncludeMetadata = false });
        var reparsed = Core.Odin.Parse(output);
        Assert.Equal(-3.14, reparsed.GetNumber("x"));
    }

    [Fact]
    public void RoundTrip_EmptyString()
    {
        var doc = Core.Odin.Parse("x = \"\"");
        var output = Core.Odin.Stringify(doc, new StringifyOptions { IncludeMetadata = false });
        var reparsed = Core.Odin.Parse(output);
        Assert.Equal("", reparsed.GetString("x"));
    }

    [Fact]
    public void RoundTrip_StringWithEscapes()
    {
        var doc = Core.Odin.Parse("x = \"line1\\nline2\\ttab\"");
        var output = Core.Odin.Stringify(doc, new StringifyOptions { IncludeMetadata = false });
        var reparsed = Core.Odin.Parse(output);
        Assert.Equal("line1\nline2\ttab", reparsed.GetString("x"));
    }

    [Fact]
    public void RoundTrip_StringWithQuotes()
    {
        var doc = Core.Odin.Parse("x = \"say \\\"hello\\\"\"");
        var output = Core.Odin.Stringify(doc, new StringifyOptions { IncludeMetadata = false });
        var reparsed = Core.Odin.Parse(output);
        Assert.Equal("say \"hello\"", reparsed.GetString("x"));
    }

    [Fact]
    public void RoundTrip_StringWithBackslash()
    {
        var doc = Core.Odin.Parse("x = \"C:\\\\path\"");
        var output = Core.Odin.Stringify(doc, new StringifyOptions { IncludeMetadata = false });
        var reparsed = Core.Odin.Parse(output);
        Assert.Equal("C:\\path", reparsed.GetString("x"));
    }

    // ─────────────────────────────────────────────────────────────────
    // Stringify All Types via Builder
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Stringify_AllTypes()
    {
        var builder = Core.Odin.Builder();
        builder.SetString("s", "hello");
        builder.SetInteger("i", 42);
        builder.SetNumber("n", 3.14);
        builder.SetBoolean("b", true);
        builder.SetNull("null_val");
        builder.SetCurrency("c", 99.99, 2);
        builder.Set("ref", OdinValues.Reference("other"));
        var doc = builder.Build();

        var output = Core.Odin.Stringify(doc, new StringifyOptions { IncludeMetadata = false });
        Assert.Contains("\"hello\"", output);
        Assert.Contains("##42", output);
        Assert.Contains("#3.14", output);
        Assert.Contains("true", output);
        Assert.Contains("~", output);
        Assert.Contains("#$", output);
        Assert.Contains("@other", output);
    }

    // ─────────────────────────────────────────────────────────────────
    // Stringify Section Headers
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Stringify_MultipleSections()
    {
        var builder = Core.Odin.Builder();
        builder.SetString("Customer.Name", "Alice");
        builder.SetString("Order.Id", "123");
        var doc = builder.Build();

        var output = Core.Odin.Stringify(doc, new StringifyOptions { IncludeMetadata = false });
        Assert.Contains("{Customer}", output);
        Assert.Contains("{Order}", output);
    }

    [Fact]
    public void Stringify_SectionWithMultipleFields()
    {
        var builder = Core.Odin.Builder();
        builder.SetString("Customer.Name", "Alice");
        builder.SetInteger("Customer.Age", 30);
        builder.SetBoolean("Customer.Active", true);
        var doc = builder.Build();

        var output = Core.Odin.Stringify(doc, new StringifyOptions { IncludeMetadata = false });
        Assert.Contains("{Customer}", output);
        Assert.Contains("Name = \"Alice\"", output);
    }

    // ─────────────────────────────────────────────────────────────────
    // Stringify with Modifiers
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Stringify_AllModifiers()
    {
        var builder = Core.Odin.Builder();
        builder.Set("x", OdinValues.String("val").WithModifiers(
            new OdinModifiers { Required = true, Confidential = true, Deprecated = true }));
        var doc = builder.Build();

        var output = Core.Odin.Stringify(doc, new StringifyOptions { IncludeMetadata = false });
        Assert.Contains("!", output);
        Assert.Contains("*", output);
        Assert.Contains("-", output);
    }

    // ─────────────────────────────────────────────────────────────────
    // Stringify Options
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Stringify_DefaultOptions_IncludesMetadata()
    {
        var builder = Core.Odin.Builder();
        builder.Metadata("odin", "1.0.0");
        builder.SetString("name", "test");
        var doc = builder.Build();

        var output = Core.Odin.Stringify(doc);
        Assert.Contains("{$}", output);
    }

    [Fact]
    public void Stringify_PreserveOrder()
    {
        var builder = Core.Odin.Builder();
        builder.SetString("z", "last");
        builder.SetString("a", "first");
        var doc = builder.Build();

        var output = Core.Odin.Stringify(doc, new StringifyOptions { IncludeMetadata = false, PreserveOrder = true });
        var zPos = output.IndexOf("z =", StringComparison.Ordinal);
        var aPos = output.IndexOf("a =", StringComparison.Ordinal);
        Assert.True(zPos < aPos, "z should appear before a when preserving insertion order");
    }

    [Fact]
    public void Stringify_SortedOrder()
    {
        var builder = Core.Odin.Builder();
        builder.SetString("z", "last");
        builder.SetString("a", "first");
        var doc = builder.Build();

        var output = Core.Odin.Stringify(doc, new StringifyOptions { IncludeMetadata = false, PreserveOrder = false });
        var zPos = output.IndexOf("z =", StringComparison.Ordinal);
        var aPos = output.IndexOf("a =", StringComparison.Ordinal);
        Assert.True(aPos < zPos, "a should appear before z when sorting alphabetically");
    }

    // ─────────────────────────────────────────────────────────────────
    // Canonicalize
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Canonicalize_ProducesBytes()
    {
        var doc = Core.Odin.Parse("name = \"Alice\"");
        var bytes = Core.Odin.Canonicalize(doc);
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);
    }

    [Fact]
    public void Canonicalize_DeterministicOutput()
    {
        var doc = Core.Odin.Parse("name = \"Alice\"\nage = ##30");
        var bytes1 = Core.Odin.Canonicalize(doc);
        var bytes2 = Core.Odin.Canonicalize(doc);
        Assert.Equal(bytes1, bytes2);
    }

    [Fact]
    public void Canonicalize_SortedKeys()
    {
        var builder = Core.Odin.Builder();
        builder.SetString("z", "last");
        builder.SetString("a", "first");
        var doc = builder.Build();

        var bytes = Core.Odin.Canonicalize(doc);
        var text = Encoding.UTF8.GetString(bytes);
        var aPos = text.IndexOf("a =", StringComparison.Ordinal);
        var zPos = text.IndexOf("z =", StringComparison.Ordinal);
        Assert.True(aPos < zPos, "a should appear before z in canonical output");
    }

    [Fact]
    public void Canonicalize_IdenticalForEquivalentDocuments()
    {
        var doc1 = Core.Odin.Builder()
            .SetString("z", "last")
            .SetString("a", "first")
            .Build();
        var doc2 = Core.Odin.Builder()
            .SetString("a", "first")
            .SetString("z", "last")
            .Build();

        var bytes1 = Core.Odin.Canonicalize(doc1);
        var bytes2 = Core.Odin.Canonicalize(doc2);
        Assert.Equal(bytes1, bytes2);
    }

    [Fact]
    public void Canonicalize_IncludesMetadata()
    {
        var doc = Core.Odin.Builder()
            .Metadata("odin", "1.0.0")
            .SetString("name", "test")
            .Build();
        var bytes = Core.Odin.Canonicalize(doc);
        var text = Encoding.UTF8.GetString(bytes);
        // Canonical form uses $.key prefix, not {$} section
        Assert.Contains("$.odin", text);
        Assert.DoesNotContain("{$}", text);
    }

    [Fact]
    public void Canonicalize_UTF8Encoded()
    {
        var doc = Core.Odin.Parse("x = \"\u65E5\u672C\u8A9E\"");
        var bytes = Core.Odin.Canonicalize(doc);
        var text = Encoding.UTF8.GetString(bytes);
        Assert.Contains("\u65E5\u672C\u8A9E", text);
    }

    // ─────────────────────────────────────────────────────────────────
    // Stringify Unicode
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void RoundTrip_UnicodeString()
    {
        var doc = Core.Odin.Parse("x = \"\u65E5\u672C\u8A9E\"");
        var output = Core.Odin.Stringify(doc, new StringifyOptions { IncludeMetadata = false });
        var reparsed = Core.Odin.Parse(output);
        Assert.Equal("\u65E5\u672C\u8A9E", reparsed.GetString("x"));
    }

    [Fact]
    public void RoundTrip_EmojiString()
    {
        var doc = Core.Odin.Parse("x = \"hello \U0001F30D\"");
        var output = Core.Odin.Stringify(doc, new StringifyOptions { IncludeMetadata = false });
        var reparsed = Core.Odin.Parse(output);
        Assert.Equal("hello \U0001F30D", reparsed.GetString("x"));
    }

    // ─────────────────────────────────────────────────────────────────
    // Stringify Edge Cases
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Stringify_EmptyDocument()
    {
        var doc = OdinDocument.Empty();
        var output = Core.Odin.Stringify(doc, new StringifyOptions { IncludeMetadata = false });
        Assert.Equal("", output);
    }

    [Fact]
    public void Stringify_MetadataOnly()
    {
        var doc = Core.Odin.Builder()
            .Metadata("odin", "1.0.0")
            .Build();
        var output = Core.Odin.Stringify(doc, new StringifyOptions { IncludeMetadata = true });
        Assert.Contains("{$}", output);
        Assert.Contains("odin", output);
    }

    [Fact]
    public void Stringify_CurrencyWithCode()
    {
        var doc = Core.Odin.Builder()
            .SetCurrency("price", 100.00, 2, "USD")
            .Build();
        var output = Core.Odin.Stringify(doc, new StringifyOptions { IncludeMetadata = false });
        Assert.Contains("#$", output);
        Assert.Contains("USD", output);
    }

    [Fact]
    public void Stringify_BinaryValue()
    {
        var data = Encoding.UTF8.GetBytes("Hello");
        var doc = Core.Odin.Builder()
            .Set("data", OdinValues.Binary(data))
            .Build();
        var output = Core.Odin.Stringify(doc, new StringifyOptions { IncludeMetadata = false });
        Assert.Contains("^", output);
    }

    [Fact]
    public void Stringify_PercentValue()
    {
        var doc = Core.Odin.Builder()
            .Set("rate", OdinValues.Percent(0.15))
            .Build();
        var output = Core.Odin.Stringify(doc, new StringifyOptions { IncludeMetadata = false });
        Assert.Contains("#%", output);
    }

    // ─────────────────────────────────────────────────────────────────
    // Stringify Escape Sequences
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Stringify_EscapesControlCharacters()
    {
        var doc = Core.Odin.Builder()
            .SetString("x", "a\nb\tc")
            .Build();
        var output = Core.Odin.Stringify(doc, new StringifyOptions { IncludeMetadata = false });
        Assert.Contains("\\n", output);
        Assert.Contains("\\t", output);
    }

    [Fact]
    public void Stringify_EscapesQuotes()
    {
        var doc = Core.Odin.Builder()
            .SetString("x", "say \"hello\"")
            .Build();
        var output = Core.Odin.Stringify(doc, new StringifyOptions { IncludeMetadata = false });
        Assert.Contains("\\\"", output);
    }

    [Fact]
    public void Stringify_EscapesBackslash()
    {
        var doc = Core.Odin.Builder()
            .SetString("x", "C:\\path")
            .Build();
        var output = Core.Odin.Stringify(doc, new StringifyOptions { IncludeMetadata = false });
        Assert.Contains("\\\\", output);
    }

    // ─────────────────────────────────────────────────────────────────
    // Round-Trip Completeness
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void RoundTrip_Timestamp()
    {
        var doc = Core.Odin.Parse("ts = 2024-06-15T14:30:00Z");
        var output = Core.Odin.Stringify(doc, new StringifyOptions { IncludeMetadata = false });
        var reparsed = Core.Odin.Parse(output);
        Assert.True(reparsed.Get("ts")!.IsTimestamp);
    }

    [Fact]
    public void RoundTrip_ZeroInteger()
    {
        var doc = Core.Odin.Parse("x = ##0");
        var output = Core.Odin.Stringify(doc, new StringifyOptions { IncludeMetadata = false });
        var reparsed = Core.Odin.Parse(output);
        Assert.Equal(0L, reparsed.GetInteger("x"));
    }

    [Fact]
    public void RoundTrip_LargeInteger()
    {
        var doc = Core.Odin.Parse("x = ##999999");
        var output = Core.Odin.Stringify(doc, new StringifyOptions { IncludeMetadata = false });
        var reparsed = Core.Odin.Parse(output);
        Assert.Equal(999999L, reparsed.GetInteger("x"));
    }

    [Fact]
    public void RoundTrip_BooleanFalse()
    {
        var doc = Core.Odin.Parse("x = false");
        var output = Core.Odin.Stringify(doc, new StringifyOptions { IncludeMetadata = false });
        var reparsed = Core.Odin.Parse(output);
        Assert.Equal(false, reparsed.GetBoolean("x"));
    }

    [Fact]
    public void RoundTrip_BooleanTrue()
    {
        var doc = Core.Odin.Parse("x = true");
        var output = Core.Odin.Stringify(doc, new StringifyOptions { IncludeMetadata = false });
        var reparsed = Core.Odin.Parse(output);
        Assert.Equal(true, reparsed.GetBoolean("x"));
    }

    [Fact]
    public void RoundTrip_WithSection()
    {
        var doc = Core.Odin.Parse("{Customer}\nName = \"Alice\"\nAge = ##30");
        var output = Core.Odin.Stringify(doc, new StringifyOptions { IncludeMetadata = false });
        var reparsed = Core.Odin.Parse(output);
        Assert.Equal("Alice", reparsed.GetString("Customer.Name"));
        Assert.Equal(30L, reparsed.GetInteger("Customer.Age"));
    }

    [Fact]
    public void RoundTrip_WithMetadata()
    {
        var doc = Core.Odin.Builder()
            .Metadata("odin", "1.0.0")
            .SetString("name", "test")
            .Build();
        var output = Core.Odin.Stringify(doc, new StringifyOptions { IncludeMetadata = true });
        var reparsed = Core.Odin.Parse(output);
        Assert.Equal("1.0.0", reparsed.GetString("$.odin"));
        Assert.Equal("test", reparsed.GetString("name"));
    }

    [Fact]
    public void RoundTrip_RequiredModifier()
    {
        var doc = Core.Odin.Parse("x = !\"required_value\"");
        var output = Core.Odin.Stringify(doc, new StringifyOptions { IncludeMetadata = false });
        var reparsed = Core.Odin.Parse(output);
        Assert.True(reparsed.Get("x")!.IsRequired);
    }

    [Fact]
    public void RoundTrip_ConfidentialModifier()
    {
        var doc = Core.Odin.Parse("x = *\"secret\"");
        var output = Core.Odin.Stringify(doc, new StringifyOptions { IncludeMetadata = false });
        var reparsed = Core.Odin.Parse(output);
        Assert.True(reparsed.Get("x")!.IsConfidential);
    }

    [Fact]
    public void RoundTrip_DeprecatedModifier()
    {
        var doc = Core.Odin.Parse("x = -\"old\"");
        var output = Core.Odin.Stringify(doc, new StringifyOptions { IncludeMetadata = false });
        var reparsed = Core.Odin.Parse(output);
        Assert.True(reparsed.Get("x")!.IsDeprecated);
    }

    [Fact]
    public void RoundTrip_MultipleFields()
    {
        var doc = Core.Odin.Parse("a = \"str\"\nb = ##42\nc = #3.14\nd = true\ne = ~");
        var output = Core.Odin.Stringify(doc, new StringifyOptions { IncludeMetadata = false });
        var reparsed = Core.Odin.Parse(output);
        Assert.Equal("str", reparsed.GetString("a"));
        Assert.Equal(42L, reparsed.GetInteger("b"));
        Assert.True(Math.Abs(reparsed.GetNumber("c")!.Value - 3.14) < 0.001);
        Assert.Equal(true, reparsed.GetBoolean("d"));
        Assert.True(reparsed.Get("e")!.IsNull);
    }

    [Fact]
    public void RoundTrip_CurrencyWithCode()
    {
        var doc = Core.Odin.Builder()
            .SetCurrency("price", 100.00, 2, "USD")
            .Build();
        var output = Core.Odin.Stringify(doc, new StringifyOptions { IncludeMetadata = false });
        var reparsed = Core.Odin.Parse(output);
        Assert.True(reparsed.Get("price")!.IsCurrency);
    }

    [Fact]
    public void Stringify_DateValue()
    {
        var doc = Core.Odin.Builder()
            .Set("dob", OdinValues.Date(2024, 6, 15))
            .Build();
        var output = Core.Odin.Stringify(doc, new StringifyOptions { IncludeMetadata = false });
        Assert.Contains("2024-06-15", output);
    }

    [Fact]
    public void Stringify_VerbValue()
    {
        var doc = Core.Odin.Builder()
            .Set("x", OdinValues.Verb("upper", new OdinValue[] { OdinValues.String("hello") }))
            .Build();
        var output = Core.Odin.Stringify(doc, new StringifyOptions { IncludeMetadata = false });
        Assert.Contains("%upper", output);
    }

    [Fact]
    public void Stringify_LongStringPreserved()
    {
        var longStr = new string('a', 1000);
        var doc = Core.Odin.Builder()
            .SetString("x", longStr)
            .Build();
        var output = Core.Odin.Stringify(doc, new StringifyOptions { IncludeMetadata = false });
        var reparsed = Core.Odin.Parse(output);
        Assert.Equal(longStr, reparsed.GetString("x"));
    }

    [Fact]
    public void Canonicalize_EmptyDocument()
    {
        var doc = OdinDocument.Empty();
        var bytes = Core.Odin.Canonicalize(doc);
        Assert.NotNull(bytes);
    }

    [Fact]
    public void Canonicalize_WithSections()
    {
        var doc = Core.Odin.Builder()
            .SetString("Customer.Name", "Alice")
            .SetInteger("Customer.Age", 30)
            .Build();
        var bytes = Core.Odin.Canonicalize(doc);
        var text = Encoding.UTF8.GetString(bytes);
        Assert.Contains("{Customer}", text);
    }

    [Fact]
    public void Canonicalize_WithModifiers()
    {
        var doc = Core.Odin.Builder()
            .Set("x", OdinValues.String("val").WithModifiers(new OdinModifiers { Required = true }))
            .Build();
        var bytes = Core.Odin.Canonicalize(doc);
        var text = Encoding.UTF8.GetString(bytes);
        Assert.Contains("!", text);
    }

    // ─────────────────────────────────────────────────────────────────
    // Stringify with special characters
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void RoundTrip_StringWithNewlines()
    {
        var doc = Core.Odin.Builder().SetString("x", "line1\nline2").Build();
        var text = Core.Odin.Stringify(doc);
        var reparsed = Core.Odin.Parse(text);
        Assert.Equal("line1\nline2", reparsed.GetString("x"));
    }

    [Fact]
    public void RoundTrip_StringWithTabs()
    {
        var doc = Core.Odin.Builder().SetString("x", "col1\tcol2").Build();
        var text = Core.Odin.Stringify(doc);
        var reparsed = Core.Odin.Parse(text);
        Assert.Equal("col1\tcol2", reparsed.GetString("x"));
    }

    [Fact]
    public void RoundTrip_BackslashInPath()
    {
        var doc = Core.Odin.Builder().SetString("x", "path\\to\\file").Build();
        var text = Core.Odin.Stringify(doc);
        var reparsed = Core.Odin.Parse(text);
        Assert.Equal("path\\to\\file", reparsed.GetString("x"));
    }

    [Fact]
    public void RoundTrip_QuotesInValue()
    {
        var doc = Core.Odin.Builder().SetString("x", "say \"hello\"").Build();
        var text = Core.Odin.Stringify(doc);
        var reparsed = Core.Odin.Parse(text);
        Assert.Equal("say \"hello\"", reparsed.GetString("x"));
    }

    [Fact]
    public void RoundTrip_NullValue()
    {
        var doc = Core.Odin.Builder().Set("x", OdinValues.Null()).Build();
        var text = Core.Odin.Stringify(doc);
        var reparsed = Core.Odin.Parse(text);
        Assert.True(reparsed.Get("x")!.IsNull);
    }

    [Fact]
    public void RoundTrip_ReferenceValue()
    {
        var doc = Core.Odin.Builder().Set("ref", OdinValues.Reference("other.path")).Build();
        var text = Core.Odin.Stringify(doc);
        var reparsed = Core.Odin.Parse(text);
        Assert.True(reparsed.Get("ref")!.IsReference);
    }

    [Fact]
    public void Stringify_MultipleFieldsSameSection()
    {
        var doc = Core.Odin.Builder()
            .SetString("S.a", "1")
            .SetString("S.b", "2")
            .SetString("S.c", "3")
            .Build();
        var text = Core.Odin.Stringify(doc);
        Assert.Contains("{S}", text);
        Assert.Contains("a = \"1\"", text);
        Assert.Contains("b = \"2\"", text);
        Assert.Contains("c = \"3\"", text);
    }

    [Fact]
    public void Stringify_TwoSections_BothAppear()
    {
        var doc = Core.Odin.Builder()
            .SetString("A.x", "1")
            .SetString("B.y", "2")
            .Build();
        var text = Core.Odin.Stringify(doc);
        Assert.Contains("{A}", text);
        Assert.Contains("{B}", text);
    }
}
