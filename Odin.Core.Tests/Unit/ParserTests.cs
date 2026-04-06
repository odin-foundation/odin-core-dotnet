using Odin.Core;
using Odin.Core.Types;
using Xunit;

namespace Odin.Core.Tests.Unit;

public class ParserTests
{
    // ─────────────────────────────────────────────────────────────────
    // Simple Assignments
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_SimpleStringAssignment()
    {
        var doc = Core.Odin.Parse("name = \"Alice\"");
        Assert.Equal("Alice", doc.GetString("name"));
    }

    [Fact]
    public void Parse_IntegerAssignment()
    {
        var doc = Core.Odin.Parse("age = ##42");
        Assert.Equal(42L, doc.GetInteger("age"));
        Assert.True(doc.Get("age")!.IsInteger);
    }

    [Fact]
    public void Parse_NumberAssignment()
    {
        var doc = Core.Odin.Parse("pi = #3.14");
        var val = doc.Get("pi");
        Assert.NotNull(val);
        Assert.True(val!.IsNumber);
        Assert.Equal(3.14, val.AsDouble());
    }

    [Fact]
    public void Parse_BooleanTrue()
    {
        var doc = Core.Odin.Parse("active = true");
        Assert.Equal(true, doc.GetBoolean("active"));
    }

    [Fact]
    public void Parse_BooleanFalse()
    {
        var doc = Core.Odin.Parse("active = false");
        Assert.Equal(false, doc.GetBoolean("active"));
    }

    [Fact]
    public void Parse_NullValue()
    {
        var doc = Core.Odin.Parse("value = ~");
        var val = doc.Get("value");
        Assert.NotNull(val);
        Assert.True(val!.IsNull);
    }

    [Fact]
    public void Parse_DateValue()
    {
        var doc = Core.Odin.Parse("dob = 2024-06-15");
        var val = doc.Get("dob");
        Assert.NotNull(val);
        Assert.True(val!.IsDate);
        var date = val as OdinDate;
        Assert.NotNull(date);
        Assert.Equal(2024, date!.Year);
        Assert.Equal(6, date.Month);
        Assert.Equal(15, date.Day);
    }

    [Fact]
    public void Parse_CurrencyValue()
    {
        var doc = Core.Odin.Parse("price = #$99.99");
        var val = doc.Get("price");
        Assert.NotNull(val);
        Assert.True(val!.IsCurrency);
    }

    [Fact]
    public void Parse_PercentValue()
    {
        var doc = Core.Odin.Parse("rate = #%0.15");
        var val = doc.Get("rate");
        Assert.NotNull(val);
        Assert.True(val!.IsPercent);
    }

    [Fact]
    public void Parse_ReferenceValue()
    {
        var doc = Core.Odin.Parse("ref = @other.path");
        var val = doc.Get("ref");
        Assert.NotNull(val);
        Assert.True(val!.IsReference);
        Assert.Equal("other.path", val.AsReference());
    }

    // ─────────────────────────────────────────────────────────────────
    // Header Sections
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_WithMetadataHeader()
    {
        var input = "{$}\nodin = \"1.0.0\"\n\nname = \"test\"";
        var doc = Core.Odin.Parse(input);

        Assert.Equal("1.0.0", doc.GetString("$.odin"));
        Assert.Equal("test", doc.GetString("name"));
    }

    [Fact]
    public void Parse_WithSectionHeader()
    {
        var input = "{Customer}\nName = \"Alice\"\nAge = ##30";
        var doc = Core.Odin.Parse(input);

        Assert.Equal("Alice", doc.GetString("Customer.Name"));
        Assert.Equal(30L, doc.GetInteger("Customer.Age"));
    }

    // ─────────────────────────────────────────────────────────────────
    // Modifiers
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_RequiredModifier()
    {
        var doc = Core.Odin.Parse("name = !\"Alice\"");
        var val = doc.Get("name");
        Assert.NotNull(val);
        Assert.True(val!.IsRequired);
    }

    [Fact]
    public void Parse_ConfidentialModifier()
    {
        var doc = Core.Odin.Parse("ssn = *\"123-45-6789\"");
        var val = doc.Get("ssn");
        Assert.NotNull(val);
        Assert.True(val!.IsConfidential);
    }

    [Fact]
    public void Parse_DeprecatedModifier()
    {
        var doc = Core.Odin.Parse("old = -\"legacy\"");
        var val = doc.Get("old");
        Assert.NotNull(val);
        Assert.True(val!.IsDeprecated);
    }

    // ─────────────────────────────────────────────────────────────────
    // Multiple Documents
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ParseDocuments_TwoDocuments()
    {
        var input = "name = \"first\"\n---\nname = \"second\"";
        var docs = Core.Odin.ParseDocuments(input);

        Assert.Equal(2, docs.Count);
        Assert.Equal("first", docs[0].GetString("name"));
        Assert.Equal("second", docs[1].GetString("name"));
    }

    // ─────────────────────────────────────────────────────────────────
    // Multiple Assignments
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_MultipleAssignments()
    {
        var input = "name = \"Alice\"\nage = ##30\nactive = true";
        var doc = Core.Odin.Parse(input);

        Assert.Equal("Alice", doc.GetString("name"));
        Assert.Equal(30L, doc.GetInteger("age"));
        Assert.Equal(true, doc.GetBoolean("active"));
    }

    // ─────────────────────────────────────────────────────────────────
    // Error Handling
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_UnterminatedString_Throws()
    {
        Assert.Throws<OdinParseException>(() =>
            Core.Odin.Parse("name = \"unterminated"));
    }

    [Fact]
    public void Parse_EmptyDocument_WithOption_Succeeds()
    {
        var doc = Core.Odin.Parse("", new ParseOptions { AllowEmpty = true });
        Assert.Equal(0, doc.Assignments.Count);
    }

    [Fact]
    public void Parse_EmptyDocument_Default_ReturnsEmpty()
    {
        // The parser allows empty documents by default
        var doc = Core.Odin.Parse("");
        Assert.Equal(0, doc.Assignments.Count);
    }

    [Fact]
    public void Parse_CommentOnlyDocument_WithAllowEmpty()
    {
        var doc = Core.Odin.Parse("; just a comment", new ParseOptions { AllowEmpty = true });
        Assert.Equal(0, doc.Assignments.Count);
    }

    // ─────────────────────────────────────────────────────────────────
    // Document API
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Document_Has_ReturnsTrueForExistingPath()
    {
        var doc = Core.Odin.Parse("name = \"test\"");
        Assert.True(doc.Has("name"));
        Assert.False(doc.Has("missing"));
    }

    [Fact]
    public void Document_With_AddsNewPath()
    {
        var doc = Core.Odin.Parse("name = \"Alice\"");
        var updated = doc.With("age", OdinValues.Integer(30));

        Assert.Equal(30L, updated.GetInteger("age"));
        Assert.Equal("Alice", updated.GetString("name"));
        // Original unmodified
        Assert.False(doc.Has("age"));
    }

    [Fact]
    public void Document_Without_RemovesPath()
    {
        var doc = Core.Odin.Parse("name = \"Alice\"\nage = ##30");
        var updated = doc.Without("age");

        Assert.False(updated.Has("age"));
        Assert.True(updated.Has("name"));
    }
}
