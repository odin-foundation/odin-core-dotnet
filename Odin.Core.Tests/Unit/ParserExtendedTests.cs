using System;
using Odin.Core;
using Odin.Core.Types;
using Xunit;

namespace Odin.Core.Tests.Unit;

public class ParserExtendedTests
{
    // ─────────────────────────────────────────────────────────────────
    // String Values
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_EmptyString()
    {
        var doc = Core.Odin.Parse("x = \"\"");
        Assert.Equal("", doc.GetString("x"));
    }

    [Fact]
    public void Parse_StringWithEscapedQuotes()
    {
        var doc = Core.Odin.Parse("x = \"say \\\"hello\\\"\"");
        Assert.Equal("say \"hello\"", doc.GetString("x"));
    }

    [Fact]
    public void Parse_StringWithEscapedBackslash()
    {
        var doc = Core.Odin.Parse("x = \"C:\\\\path\\\\file\"");
        Assert.Equal("C:\\path\\file", doc.GetString("x"));
    }

    [Fact]
    public void Parse_StringWithNewlineEscape()
    {
        var doc = Core.Odin.Parse("x = \"line1\\nline2\"");
        Assert.Equal("line1\nline2", doc.GetString("x"));
    }

    [Fact]
    public void Parse_StringWithTabEscape()
    {
        var doc = Core.Odin.Parse("x = \"col1\\tcol2\"");
        Assert.Equal("col1\tcol2", doc.GetString("x"));
    }

    [Fact]
    public void Parse_StringWithCarriageReturnEscape()
    {
        var doc = Core.Odin.Parse("x = \"a\\rb\"");
        Assert.Equal("a\rb", doc.GetString("x"));
    }

    [Fact]
    public void Parse_StringWithUnicodeEscape()
    {
        var doc = Core.Odin.Parse("x = \"\\u0041\"");
        Assert.Equal("A", doc.GetString("x"));
    }

    [Fact]
    public void Parse_StringWithWhitespaceOnly()
    {
        var doc = Core.Odin.Parse("x = \"   \"");
        Assert.Equal("   ", doc.GetString("x"));
    }

    [Fact]
    public void Parse_StringWithEmoji()
    {
        var doc = Core.Odin.Parse("x = \"hello \U0001F30D\"");
        Assert.Equal("hello \U0001F30D", doc.GetString("x"));
    }

    [Fact]
    public void Parse_StringWithCJK()
    {
        var doc = Core.Odin.Parse("x = \"\u65E5\u672C\u8A9E\"");
        Assert.Equal("\u65E5\u672C\u8A9E", doc.GetString("x"));
    }

    [Fact]
    public void Parse_StringWithArabic()
    {
        var doc = Core.Odin.Parse("x = \"\u0645\u0631\u062D\u0628\u0627\"");
        Assert.Equal("\u0645\u0631\u062D\u0628\u0627", doc.GetString("x"));
    }

    // ─────────────────────────────────────────────────────────────────
    // Integer Values
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_ZeroInteger()
    {
        var doc = Core.Odin.Parse("x = ##0");
        Assert.Equal(0L, doc.GetInteger("x"));
    }

    [Fact]
    public void Parse_NegativeInteger()
    {
        var doc = Core.Odin.Parse("x = ##-42");
        Assert.Equal(-42L, doc.GetInteger("x"));
    }

    [Fact]
    public void Parse_LargePositiveInteger()
    {
        var doc = Core.Odin.Parse("x = ##9223372036854775807");
        Assert.NotNull(doc.Get("x"));
        Assert.True(doc.Get("x")!.IsInteger);
    }

    [Fact]
    public void Parse_LargeNegativeInteger()
    {
        var doc = Core.Odin.Parse("x = ##-9223372036854775808");
        Assert.NotNull(doc.Get("x"));
        Assert.True(doc.Get("x")!.IsInteger);
    }

    // ─────────────────────────────────────────────────────────────────
    // Number Values
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_ZeroNumber()
    {
        var doc = Core.Odin.Parse("x = #0.0");
        Assert.NotNull(doc.Get("x"));
        Assert.True(doc.Get("x")!.IsNumber);
        Assert.True(Math.Abs(doc.GetNumber("x")!.Value) < 0.001);
    }

    [Fact]
    public void Parse_NegativeNumber()
    {
        var doc = Core.Odin.Parse("x = #-3.14");
        Assert.Equal(-3.14, doc.GetNumber("x"));
    }

    [Fact]
    public void Parse_VerySmallNumber()
    {
        var doc = Core.Odin.Parse("x = #0.000001");
        Assert.True(doc.GetNumber("x")!.Value > 0.0);
    }

    [Fact]
    public void Parse_VeryLargeNumber()
    {
        var doc = Core.Odin.Parse("x = #999999999.999");
        Assert.True(doc.GetNumber("x")!.Value > 999999999.0);
    }

    // ─────────────────────────────────────────────────────────────────
    // Currency Values
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_ZeroCurrency()
    {
        var doc = Core.Odin.Parse("x = #$0.00");
        Assert.True(doc.Get("x")!.IsCurrency);
    }

    [Fact]
    public void Parse_LargeCurrency()
    {
        var doc = Core.Odin.Parse("x = #$1000000.00");
        Assert.True(doc.Get("x")!.IsCurrency);
    }

    [Fact]
    public void Parse_CurrencyWithCode()
    {
        var doc = Core.Odin.Parse("x = #$99.99:USD");
        var val = doc.Get("x") as OdinCurrency;
        Assert.NotNull(val);
        Assert.Equal("USD", val!.CurrencyCode);
    }

    // ─────────────────────────────────────────────────────────────────
    // Percent Values
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_ZeroPercent()
    {
        var doc = Core.Odin.Parse("x = #%0");
        Assert.True(doc.Get("x")!.IsPercent);
    }

    [Fact]
    public void Parse_HundredPercent()
    {
        var doc = Core.Odin.Parse("x = #%100");
        Assert.True(doc.Get("x")!.IsPercent);
    }

    [Fact]
    public void Parse_FractionalPercent()
    {
        var doc = Core.Odin.Parse("x = #%0.5");
        Assert.True(doc.Get("x")!.IsPercent);
    }

    [Fact]
    public void Parse_OverHundredPercent()
    {
        var doc = Core.Odin.Parse("x = #%200");
        Assert.True(doc.Get("x")!.IsPercent);
    }

    // ─────────────────────────────────────────────────────────────────
    // Boolean Values
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_BooleanTrueValue()
    {
        var doc = Core.Odin.Parse("x = true");
        Assert.Equal(true, doc.GetBoolean("x"));
    }

    [Fact]
    public void Parse_BooleanFalseValue()
    {
        var doc = Core.Odin.Parse("x = false");
        Assert.Equal(false, doc.GetBoolean("x"));
    }

    // ─────────────────────────────────────────────────────────────────
    // Date/Time Values
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_DateFullDate()
    {
        var doc = Core.Odin.Parse("x = 2024-01-15");
        var val = doc.Get("x") as OdinDate;
        Assert.NotNull(val);
        Assert.Equal(2024, val!.Year);
        Assert.Equal(1, val.Month);
        Assert.Equal(15, val.Day);
    }

    [Fact]
    public void Parse_DateLeapYearDay()
    {
        var doc = Core.Odin.Parse("x = 2024-02-29");
        var val = doc.Get("x") as OdinDate;
        Assert.NotNull(val);
        Assert.Equal(29, val!.Day);
    }

    [Fact]
    public void Parse_Timestamp()
    {
        var doc = Core.Odin.Parse("x = 2024-06-15T14:30:00Z");
        var val = doc.Get("x");
        Assert.NotNull(val);
        Assert.True(val!.IsTimestamp);
    }

    // ─────────────────────────────────────────────────────────────────
    // Binary Values
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_BinaryBase64()
    {
        var doc = Core.Odin.Parse("x = ^SGVsbG8=");
        var val = doc.Get("x");
        Assert.NotNull(val);
        Assert.True(val!.IsBinary);
    }

    // ─────────────────────────────────────────────────────────────────
    // Reference Values
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_ReferenceSimplePath()
    {
        var doc = Core.Odin.Parse("x = @other");
        Assert.Equal("other", doc.Get("x")!.AsReference());
    }

    [Fact]
    public void Parse_ReferenceDottedPath()
    {
        var doc = Core.Odin.Parse("x = @section.field");
        Assert.Equal("section.field", doc.Get("x")!.AsReference());
    }

    // ─────────────────────────────────────────────────────────────────
    // Modifier Combinations
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_RequiredAndConfidential()
    {
        var doc = Core.Odin.Parse("x = !*\"secret\"");
        var val = doc.Get("x");
        Assert.NotNull(val);
        Assert.True(val!.IsRequired);
        Assert.True(val.IsConfidential);
    }

    [Fact]
    public void Parse_AllThreeModifiers()
    {
        var doc = Core.Odin.Parse("x = !-*\"legacy_secret\"");
        var val = doc.Get("x");
        Assert.NotNull(val);
        Assert.True(val!.IsRequired);
        Assert.True(val.IsDeprecated);
        Assert.True(val.IsConfidential);
    }

    [Fact]
    public void Parse_RequiredInteger()
    {
        var doc = Core.Odin.Parse("x = !##42");
        var val = doc.Get("x");
        Assert.NotNull(val);
        Assert.True(val!.IsRequired);
        Assert.True(val.IsInteger);
    }

    [Fact]
    public void Parse_ConfidentialNumber()
    {
        var doc = Core.Odin.Parse("x = *#3.14");
        var val = doc.Get("x");
        Assert.NotNull(val);
        Assert.True(val!.IsConfidential);
        Assert.True(val.IsNumber);
    }

    [Fact]
    public void Parse_DeprecatedBoolean()
    {
        var doc = Core.Odin.Parse("x = -true");
        var val = doc.Get("x");
        Assert.NotNull(val);
        Assert.True(val!.IsDeprecated);
        Assert.True(val.IsBoolean);
    }

    [Fact]
    public void Parse_RequiredNull()
    {
        var doc = Core.Odin.Parse("x = !~");
        var val = doc.Get("x");
        Assert.NotNull(val);
        Assert.True(val!.IsRequired);
        Assert.True(val.IsNull);
    }

    // ─────────────────────────────────────────────────────────────────
    // Section Headers
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_MultipleSections()
    {
        var input = "{Customer}\nName = \"Alice\"\n{Order}\nId = ##1";
        var doc = Core.Odin.Parse(input);
        Assert.Equal("Alice", doc.GetString("Customer.Name"));
        Assert.Equal(1L, doc.GetInteger("Order.Id"));
    }

    [Fact]
    public void Parse_NestedSectionPath()
    {
        var doc = Core.Odin.Parse("{Customer}\nName = \"Alice\"\nAge = ##30");
        Assert.True(doc.Has("Customer.Name"));
        Assert.True(doc.Has("Customer.Age"));
    }

    [Fact]
    public void Parse_MetadataAndSections()
    {
        var input = "{$}\nodin = \"1.0.0\"\n\n{Customer}\nName = \"Alice\"";
        var doc = Core.Odin.Parse(input);
        Assert.Equal("1.0.0", doc.GetString("$.odin"));
        Assert.Equal("Alice", doc.GetString("Customer.Name"));
    }

    [Fact]
    public void Parse_MultipleTypesInSection()
    {
        var input = "{S}\na = \"str\"\nb = ##42\nc = #3.14\nd = true\ne = ~\nf = #$99.99\ng = #%50";
        var doc = Core.Odin.Parse(input);
        Assert.Equal("str", doc.GetString("S.a"));
        Assert.Equal(42L, doc.GetInteger("S.b"));
        Assert.True(doc.Get("S.c")!.IsNumber);
        Assert.Equal(true, doc.GetBoolean("S.d"));
        Assert.True(doc.Get("S.e")!.IsNull);
        Assert.True(doc.Get("S.f")!.IsCurrency);
        Assert.True(doc.Get("S.g")!.IsPercent);
    }

    // ─────────────────────────────────────────────────────────────────
    // Comments
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_CommentsIgnored()
    {
        var doc = Core.Odin.Parse("; this is a comment\nname = \"Alice\"");
        Assert.Equal("Alice", doc.GetString("name"));
    }

    [Fact]
    public void Parse_InlineCommentIgnored()
    {
        var doc = Core.Odin.Parse("name = \"Alice\" ; inline comment");
        Assert.Equal("Alice", doc.GetString("name"));
    }

    [Fact]
    public void Parse_CommentOnlyLines()
    {
        var doc = Core.Odin.Parse("; comment1\n; comment2\nname = \"test\"");
        Assert.Equal("test", doc.GetString("name"));
    }

    // ─────────────────────────────────────────────────────────────────
    // Whitespace Handling
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_TrailingSpaces()
    {
        var doc = Core.Odin.Parse("x = ##42   ");
        Assert.Equal(42L, doc.GetInteger("x"));
    }

    [Fact]
    public void Parse_LeadingSpaces()
    {
        var doc = Core.Odin.Parse("   x = ##42");
        Assert.Equal(42L, doc.GetInteger("x"));
    }

    [Fact]
    public void Parse_SpacesAroundEquals()
    {
        var doc = Core.Odin.Parse("x   =   ##42");
        Assert.Equal(42L, doc.GetInteger("x"));
    }

    [Fact]
    public void Parse_TabIndentation()
    {
        var doc = Core.Odin.Parse("\tx = ##42");
        Assert.Equal(42L, doc.GetInteger("x"));
    }

    [Fact]
    public void Parse_BlankLinesBetween()
    {
        var doc = Core.Odin.Parse("a = ##1\n\n\nb = ##2");
        Assert.Equal(1L, doc.GetInteger("a"));
        Assert.Equal(2L, doc.GetInteger("b"));
    }

    [Fact]
    public void Parse_CrlfLineEndings()
    {
        var doc = Core.Odin.Parse("x = ##42\r\n");
        Assert.Equal(42L, doc.GetInteger("x"));
    }

    [Fact]
    public void Parse_MixedLineEndings()
    {
        var doc = Core.Odin.Parse("a = ##1\nb = ##2\r\nc = ##3");
        Assert.Equal(1L, doc.GetInteger("a"));
        Assert.Equal(3L, doc.GetInteger("c"));
    }

    [Fact]
    public void Parse_TrailingNewlines()
    {
        var doc = Core.Odin.Parse("x = ##42\n\n\n\n");
        Assert.Equal(42L, doc.GetInteger("x"));
    }

    [Fact]
    public void Parse_NoTrailingNewline()
    {
        // Should not crash
        var doc = Core.Odin.Parse("x = ##42");
        Assert.Equal(42L, doc.GetInteger("x"));
    }

    // ─────────────────────────────────────────────────────────────────
    // Multiple Documents
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ParseDocuments_ThreeDocuments()
    {
        var input = "a = ##1\n---\nb = ##2\n---\nc = ##3";
        var docs = Core.Odin.ParseDocuments(input);
        Assert.Equal(3, docs.Count);
        Assert.Equal(1L, docs[0].GetInteger("a"));
        Assert.Equal(2L, docs[1].GetInteger("b"));
        Assert.Equal(3L, docs[2].GetInteger("c"));
    }

    [Fact]
    public void ParseDocuments_SingleDocument()
    {
        var docs = Core.Odin.ParseDocuments("x = ##42");
        Assert.Single(docs);
        Assert.Equal(42L, docs[0].GetInteger("x"));
    }

    // ─────────────────────────────────────────────────────────────────
    // Document API
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Document_GetMissing_ReturnsNull()
    {
        var doc = Core.Odin.Parse("x = ##1");
        Assert.Null(doc.Get("missing"));
    }

    [Fact]
    public void Document_GetString_WrongType_ReturnsNull()
    {
        var doc = Core.Odin.Parse("x = ##42");
        Assert.Null(doc.GetString("x"));
    }

    [Fact]
    public void Document_GetInteger_WrongType_ReturnsNull()
    {
        var doc = Core.Odin.Parse("x = \"hello\"");
        Assert.Null(doc.GetInteger("x"));
    }

    [Fact]
    public void Document_GetBoolean_WrongType_ReturnsNull()
    {
        var doc = Core.Odin.Parse("x = ##42");
        Assert.Null(doc.GetBoolean("x"));
    }

    [Fact]
    public void Document_With_ChainMultiple()
    {
        var doc = Core.Odin.Parse("a = ##1");
        var updated = doc.With("b", OdinValues.Integer(2))
                         .With("c", OdinValues.Integer(3));
        Assert.Equal(1L, updated.GetInteger("a"));
        Assert.Equal(2L, updated.GetInteger("b"));
        Assert.Equal(3L, updated.GetInteger("c"));
    }

    [Fact]
    public void Document_With_OverwriteExisting()
    {
        var doc = Core.Odin.Parse("x = ##1");
        var updated = doc.With("x", OdinValues.Integer(2));
        Assert.Equal(2L, updated.GetInteger("x"));
        // original unchanged
        Assert.Equal(1L, doc.GetInteger("x"));
    }

    [Fact]
    public void Document_Without_NonExistent()
    {
        var doc = Core.Odin.Parse("x = ##1");
        var updated = doc.Without("nonexistent");
        Assert.True(updated.Has("x"));
    }

    [Fact]
    public void Document_Paths_Empty()
    {
        var doc = Core.Odin.Parse("", new ParseOptions { AllowEmpty = true });
        Assert.Empty(doc.Paths());
    }

    [Fact]
    public void Document_Flatten_IncludesAllTypes()
    {
        var doc = Core.Odin.Parse("s = \"hello\"\ni = ##42\nb = true\nn = ~");
        var flat = doc.Flatten();
        Assert.Equal("hello", flat["s"]);
        Assert.Equal("42", flat["i"]);
        Assert.Equal("true", flat["b"]);
    }

    [Fact]
    public void Document_Resolve_SimpleValue()
    {
        var doc = Core.Odin.Parse("x = ##42");
        var val = doc.Resolve("x");
        Assert.NotNull(val);
        Assert.Equal(42L, val!.AsInt64());
    }

    [Fact]
    public void Document_Resolve_FollowsReference()
    {
        var doc = Core.Odin.Parse("x = @y\ny = ##42");
        var val = doc.Resolve("x");
        Assert.NotNull(val);
        Assert.Equal(42L, val!.AsInt64());
    }

    [Fact]
    public void Document_Resolve_CircularReference_Throws()
    {
        var doc = Core.Odin.Parse("x = @y\ny = @x");
        Assert.Throws<InvalidOperationException>(() => doc.Resolve("x"));
    }

    [Fact]
    public void Document_Resolve_UnresolvedReference_Throws()
    {
        var doc = Core.Odin.Parse("x = @missing");
        Assert.Throws<InvalidOperationException>(() => doc.Resolve("x"));
    }

    [Fact]
    public void Document_Resolve_MissingPath_ReturnsNull()
    {
        var doc = Core.Odin.Parse("x = ##1");
        Assert.Null(doc.Resolve("missing"));
    }

    // ─────────────────────────────────────────────────────────────────
    // Error Cases
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_UnterminatedString_ThrowsP004()
    {
        var ex = Assert.Throws<OdinParseException>(() =>
            Core.Odin.Parse("x = \"unterminated"));
        Assert.Equal(ParseErrorCode.UnterminatedString, ex.ErrorCode);
    }

    [Fact]
    public void Parse_EmptyInput_ReturnsEmpty()
    {
        var doc = Core.Odin.Parse("");
        Assert.Empty(doc.Assignments);
    }

    [Fact]
    public void Parse_CommentOnly_ReturnsEmpty()
    {
        var doc = Core.Odin.Parse("; just a comment");
        Assert.Empty(doc.Assignments);
    }

    // ─────────────────────────────────────────────────────────────────
    // Array Syntax
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_ArrayElements()
    {
        var doc = Core.Odin.Parse("items[0] = \"a\"\nitems[1] = \"b\"\nitems[2] = \"c\"");
        Assert.Equal("a", doc.GetString("items[0]"));
        Assert.Equal("b", doc.GetString("items[1]"));
        Assert.Equal("c", doc.GetString("items[2]"));
    }

    [Fact]
    public void Parse_ArrayInSection()
    {
        var doc = Core.Odin.Parse("{Data}\nitems[0] = ##1\nitems[1] = ##2");
        Assert.Equal(1L, doc.GetInteger("Data.items[0]"));
        Assert.Equal(2L, doc.GetInteger("Data.items[1]"));
    }

    // ─────────────────────────────────────────────────────────────────
    // Large Document
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_ManyFields()
    {
        var lines = new System.Text.StringBuilder();
        for (int i = 0; i < 100; i++)
            lines.AppendLine($"field_{i} = ##{i}");
        // Should not crash
        var result = Core.Odin.Parse(lines.ToString());
        Assert.NotNull(result);
    }

    [Fact]
    public void Parse_LongString()
    {
        var longStr = new string('x', 10000);
        var doc = Core.Odin.Parse($"val = \"{longStr}\"");
        Assert.Equal(longStr, doc.GetString("val"));
    }

    [Fact]
    public void Parse_ManyManySections()
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < 50; i++)
            sb.AppendLine($"{{Section{i}}}\nfield = ##{i}");
        // Should not crash
        var result = Core.Odin.Parse(sb.ToString());
        Assert.NotNull(result);
    }

    // ─────────────────────────────────────────────────────────────────
    // Document Separator (---)
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ParseDocuments_WithMetadata()
    {
        var input = "{$}\nodin = \"1.0.0\"\n\nname = \"first\"\n---\nname = \"second\"";
        var docs = Core.Odin.ParseDocuments(input);
        Assert.Equal(2, docs.Count);
    }

    // ─────────────────────────────────────────────────────────────────
    // Parse Options
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ParseOptions_AllowEmpty()
    {
        var doc = Core.Odin.Parse("", new ParseOptions { AllowEmpty = true });
        Assert.Empty(doc.Assignments);
    }

    [Fact]
    public void ParseOptions_PreserveComments()
    {
        var doc = Core.Odin.Parse("; my comment\nname = \"test\"", new ParseOptions { PreserveComments = true });
        Assert.Equal("test", doc.GetString("name"));
        // Comments may or may not be preserved depending on parser implementation
        // At minimum, the parse should succeed
    }

    // ─────────────────────────────────────────────────────────────────
    // Type Queries
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_IsNumeric_ForInteger()
    {
        var doc = Core.Odin.Parse("x = ##42");
        Assert.True(doc.Get("x")!.IsNumeric);
    }

    [Fact]
    public void Parse_IsNumeric_ForNumber()
    {
        var doc = Core.Odin.Parse("x = #3.14");
        Assert.True(doc.Get("x")!.IsNumeric);
    }

    [Fact]
    public void Parse_IsNumeric_ForCurrency()
    {
        var doc = Core.Odin.Parse("x = #$99.99");
        Assert.True(doc.Get("x")!.IsNumeric);
    }

    [Fact]
    public void Parse_IsNumeric_ForPercent()
    {
        var doc = Core.Odin.Parse("x = #%50");
        Assert.True(doc.Get("x")!.IsNumeric);
    }

    [Fact]
    public void Parse_IsTemporal_ForDate()
    {
        var doc = Core.Odin.Parse("x = 2024-01-01");
        Assert.True(doc.Get("x")!.IsTemporal);
    }

    [Fact]
    public void Parse_IsTemporal_ForTimestamp()
    {
        var doc = Core.Odin.Parse("x = 2024-01-01T00:00:00Z");
        Assert.True(doc.Get("x")!.IsTemporal);
    }

    // ─────────────────────────────────────────────────────────────────
    // Document.Flatten
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Flatten_ReturnsAllPaths()
    {
        var doc = Core.Odin.Parse("a = \"x\"\nb = ##1\nc = true");
        var flat = doc.Flatten();
        Assert.Equal(3, flat.Count);
    }

    [Fact]
    public void Flatten_IncludeMetadata()
    {
        var doc = Core.Odin.Parse("{$}\nodin = \"1.0.0\"\n\nname = \"test\"");
        var flat = doc.Flatten(new FlattenOptions { IncludeMetadata = true });
        Assert.True(flat.ContainsKey("$.odin"));
    }

    [Fact]
    public void Flatten_Default_ContainsDataFields()
    {
        var doc = Core.Odin.Parse("{$}\nodin = \"1.0.0\"\n\nname = \"test\"");
        var flat = doc.Flatten();
        Assert.True(flat.ContainsKey("name"));
    }

    // ─────────────────────────────────────────────────────────────────
    // Document.Empty
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void EmptyDocument_HasNoPaths()
    {
        var doc = OdinDocument.Empty();
        Assert.Empty(doc.Paths());
    }

    [Fact]
    public void EmptyDocument_GetReturnsNull()
    {
        var doc = OdinDocument.Empty();
        Assert.Null(doc.Get("anything"));
    }

    [Fact]
    public void EmptyDocument_HasReturnsFalse()
    {
        var doc = OdinDocument.Empty();
        Assert.False(doc.Has("anything"));
    }

    // ─────────────────────────────────────────────────────────────────
    // Metadata Access via $. prefix
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Metadata_AccessViaDollarDotPrefix()
    {
        var doc = Core.Odin.Parse("{$}\nodin = \"1.0.0\"\n\nname = \"test\"");
        Assert.Equal("1.0.0", doc.GetString("$.odin"));
    }

    [Fact]
    public void Metadata_NonexistentReturnsNull()
    {
        var doc = Core.Odin.Parse("{$}\nodin = \"1.0.0\"\n\nname = \"test\"");
        Assert.Null(doc.Get("$.nonexistent"));
    }

    // ─────────────────────────────────────────────────────────────────
    // Reference Resolution
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Resolve_FollowsReferenceChain()
    {
        var doc = Core.Odin.Parse("a = @b\nb = @c\nc = ##42");
        var val = doc.Resolve("a");
        Assert.Equal(42L, val!.AsInt64());
    }

    [Fact]
    public void Resolve_CircularThrows()
    {
        var doc = Core.Odin.Parse("a = @b\nb = @a");
        Assert.Throws<InvalidOperationException>(() => doc.Resolve("a"));
    }

    [Fact]
    public void Resolve_UnresolvedThrows()
    {
        var doc = Core.Odin.Parse("a = @nonexistent");
        Assert.Throws<InvalidOperationException>(() => doc.Resolve("a"));
    }

    [Fact]
    public void Resolve_NonReference_ReturnsSelf()
    {
        var doc = Core.Odin.Parse("a = ##99");
        var val = doc.Resolve("a");
        Assert.Equal(99L, val!.AsInt64());
    }

    [Fact]
    public void Resolve_NullPath_ReturnsNull()
    {
        var doc = Core.Odin.Parse("a = ##1");
        Assert.Null(doc.Resolve("nonexistent"));
    }

    // ─────────────────────────────────────────────────────────────────
    // GetNumber
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void GetNumber_ReturnsDouble()
    {
        var doc = Core.Odin.Parse("x = #3.14");
        Assert.NotNull(doc.GetNumber("x"));
        Assert.True(Math.Abs(doc.GetNumber("x")!.Value - 3.14) < 0.001);
    }

    [Fact]
    public void GetNumber_MissingReturnsNull()
    {
        var doc = Core.Odin.Parse("x = ##1");
        Assert.Null(doc.GetNumber("missing"));
    }

    // ─────────────────────────────────────────────────────────────────
    // With chaining
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void With_Chaining_MultipleAdds()
    {
        var doc = OdinDocument.Empty()
            .With("a", OdinValues.Integer(1))
            .With("b", OdinValues.Integer(2))
            .With("c", OdinValues.Integer(3));
        Assert.Equal(3, doc.Paths().Count);
    }

    [Fact]
    public void Without_Chaining_MultipleRemoves()
    {
        var doc = Core.Odin.Parse("a = ##1\nb = ##2\nc = ##3");
        var result = doc.Without("a").Without("b");
        Assert.Single(result.Paths());
        Assert.True(result.Has("c"));
    }

    [Fact]
    public void With_OverwritesExisting()
    {
        var doc = Core.Odin.Parse("x = ##1");
        var updated = doc.With("x", OdinValues.Integer(99));
        Assert.Equal(99L, updated.GetInteger("x"));
    }
}
