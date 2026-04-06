using System;
using System.Text;
using Odin.Core;
using Odin.Core.Types;
using Xunit;

namespace Odin.Core.Tests.Unit;

public class ParserEdgeCaseTests
{
    // ─────────────────────────────────────────────────────────────────
    // String Edge Cases
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_StringWithOnlySpaces()
    {
        var doc = Core.Odin.Parse("x = \"   \"");
        Assert.Equal("   ", doc.GetString("x"));
    }

    [Fact]
    public void Parse_StringWithAllEscapes()
    {
        var doc = Core.Odin.Parse("x = \"\\n\\r\\t\\\\\\\"\"");
        Assert.Equal("\n\r\t\\\"", doc.GetString("x"));
    }

    [Fact]
    public void Parse_StringWithNullEscape()
    {
        var doc = Core.Odin.Parse("x = \"a\\0b\"");
        Assert.Equal("a\0b", doc.GetString("x"));
    }

    [Fact]
    public void Parse_StringWithForwardSlashEscape()
    {
        var doc = Core.Odin.Parse("x = \"a\\/b\"");
        Assert.Equal("a/b", doc.GetString("x"));
    }

    [Fact]
    public void Parse_StringWithMultipleUnicodeEscapes()
    {
        var doc = Core.Odin.Parse("x = \"\\u0041\\u0042\\u0043\"");
        Assert.Equal("ABC", doc.GetString("x"));
    }

    [Fact]
    public void Parse_StringWithSurrogatePair()
    {
        var doc = Core.Odin.Parse("x = \"\\uD83C\\uDF0D\"");
        Assert.Equal("\U0001F30D", doc.GetString("x"));
    }

    [Fact]
    public void Parse_StringWith8DigitUnicode()
    {
        var doc = Core.Odin.Parse("x = \"\\U0001F600\"");
        Assert.Equal("\U0001F600", doc.GetString("x"));
    }

    [Fact]
    public void Parse_VeryLongString()
    {
        var longStr = new string('x', 50000);
        var doc = Core.Odin.Parse($"val = \"{longStr}\"");
        Assert.Equal(longStr, doc.GetString("val"));
    }

    [Fact]
    public void Parse_StringWithSpecialChars()
    {
        var doc = Core.Odin.Parse("x = \"!@#$%^&*()\"");
        Assert.Equal("!@#$%^&*()", doc.GetString("x"));
    }

    [Fact]
    public void Parse_StringWithEquals()
    {
        var doc = Core.Odin.Parse("x = \"a = b\"");
        Assert.Equal("a = b", doc.GetString("x"));
    }

    [Fact]
    public void Parse_StringWithBraces()
    {
        var doc = Core.Odin.Parse("x = \"{hello}\"");
        Assert.Equal("{hello}", doc.GetString("x"));
    }

    [Fact]
    public void Parse_StringWithSemicolon()
    {
        var doc = Core.Odin.Parse("x = \"value ; not a comment\"");
        Assert.Equal("value ; not a comment", doc.GetString("x"));
    }

    // ─────────────────────────────────────────────────────────────────
    // Integer Edge Cases
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_IntegerZero()
    {
        var doc = Core.Odin.Parse("x = ##0");
        Assert.Equal(0L, doc.GetInteger("x"));
    }

    [Fact]
    public void Parse_IntegerOne()
    {
        var doc = Core.Odin.Parse("x = ##1");
        Assert.Equal(1L, doc.GetInteger("x"));
    }

    [Fact]
    public void Parse_IntegerNegativeOne()
    {
        var doc = Core.Odin.Parse("x = ##-1");
        Assert.Equal(-1L, doc.GetInteger("x"));
    }

    [Fact]
    public void Parse_IntegerMaxInt32()
    {
        var doc = Core.Odin.Parse("x = ##2147483647");
        Assert.Equal(2147483647L, doc.GetInteger("x"));
    }

    [Fact]
    public void Parse_IntegerMinInt32()
    {
        var doc = Core.Odin.Parse("x = ##-2147483648");
        Assert.Equal(-2147483648L, doc.GetInteger("x"));
    }

    [Fact]
    public void Parse_IntegerMaxInt64()
    {
        var doc = Core.Odin.Parse("x = ##9223372036854775807");
        Assert.True(doc.Get("x")!.IsInteger);
    }

    [Fact]
    public void Parse_IntegerMinInt64()
    {
        var doc = Core.Odin.Parse("x = ##-9223372036854775808");
        Assert.True(doc.Get("x")!.IsInteger);
    }

    // ─────────────────────────────────────────────────────────────────
    // Number Edge Cases
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_NumberZeroDotZero()
    {
        var doc = Core.Odin.Parse("x = #0.0");
        Assert.True(doc.Get("x")!.IsNumber);
    }

    [Fact]
    public void Parse_NumberNegativeZero()
    {
        var doc = Core.Odin.Parse("x = #-0.0");
        Assert.True(doc.Get("x")!.IsNumber);
    }

    [Fact]
    public void Parse_NumberWithManyDecimals()
    {
        var doc = Core.Odin.Parse("x = #3.14159265358979");
        Assert.True(Math.Abs(doc.GetNumber("x")!.Value - 3.14159265358979) < 0.0001);
    }

    [Fact]
    public void Parse_NumberScientificNotation()
    {
        var doc = Core.Odin.Parse("x = #1.5e10");
        Assert.True(doc.Get("x")!.IsNumber);
        Assert.True(doc.GetNumber("x")!.Value > 1e9);
    }

    [Fact]
    public void Parse_NumberSmallScientific()
    {
        var doc = Core.Odin.Parse("x = #1e-5");
        Assert.True(doc.Get("x")!.IsNumber);
    }

    // ─────────────────────────────────────────────────────────────────
    // Currency Edge Cases
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_CurrencyNegative()
    {
        var doc = Core.Odin.Parse("x = #$-100.00");
        Assert.True(doc.Get("x")!.IsCurrency);
    }

    [Fact]
    public void Parse_CurrencyWithEUR()
    {
        var doc = Core.Odin.Parse("x = #$50.00:EUR");
        var val = doc.Get("x") as OdinCurrency;
        Assert.NotNull(val);
        Assert.Equal("EUR", val!.CurrencyCode);
    }

    [Fact]
    public void Parse_CurrencyWithGBP()
    {
        var doc = Core.Odin.Parse("x = #$75.50:GBP");
        var val = doc.Get("x") as OdinCurrency;
        Assert.NotNull(val);
        Assert.Equal("GBP", val!.CurrencyCode);
    }

    [Fact]
    public void Parse_CurrencyWithJPY()
    {
        var doc = Core.Odin.Parse("x = #$1000:JPY");
        var val = doc.Get("x") as OdinCurrency;
        Assert.NotNull(val);
        Assert.Equal("JPY", val!.CurrencyCode);
    }

    [Fact]
    public void Parse_CurrencyZero()
    {
        var doc = Core.Odin.Parse("x = #$0.00");
        Assert.True(doc.Get("x")!.IsCurrency);
    }

    // ─────────────────────────────────────────────────────────────────
    // Date Edge Cases
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_DateFirstDayOfYear()
    {
        var doc = Core.Odin.Parse("x = 2024-01-01");
        var val = doc.Get("x") as OdinDate;
        Assert.NotNull(val);
        Assert.Equal(1, val!.Month);
        Assert.Equal(1, val.Day);
    }

    [Fact]
    public void Parse_DateLastDayOfYear()
    {
        var doc = Core.Odin.Parse("x = 2024-12-31");
        var val = doc.Get("x") as OdinDate;
        Assert.NotNull(val);
        Assert.Equal(12, val!.Month);
        Assert.Equal(31, val.Day);
    }

    [Fact]
    public void Parse_DateLeapYearFeb29()
    {
        var doc = Core.Odin.Parse("x = 2024-02-29");
        var val = doc.Get("x") as OdinDate;
        Assert.NotNull(val);
        Assert.Equal(29, val!.Day);
    }

    [Fact]
    public void Parse_DateNonLeapYearFeb28()
    {
        var doc = Core.Odin.Parse("x = 2023-02-28");
        var val = doc.Get("x") as OdinDate;
        Assert.NotNull(val);
        Assert.Equal(28, val!.Day);
    }

    [Fact]
    public void Parse_DateApril30()
    {
        var doc = Core.Odin.Parse("x = 2024-04-30");
        var val = doc.Get("x") as OdinDate;
        Assert.NotNull(val);
        Assert.Equal(30, val!.Day);
    }

    [Fact]
    public void Parse_TimestampWithOffset()
    {
        var doc = Core.Odin.Parse("x = 2024-06-15T14:30:00+05:30");
        Assert.True(doc.Get("x")!.IsTimestamp);
    }

    [Fact]
    public void Parse_TimestampWithNegativeOffset()
    {
        var doc = Core.Odin.Parse("x = 2024-06-15T14:30:00-08:00");
        Assert.True(doc.Get("x")!.IsTimestamp);
    }

    [Fact]
    public void Parse_TimestampWithMilliseconds()
    {
        var doc = Core.Odin.Parse("x = 2024-06-15T14:30:00.123Z");
        Assert.True(doc.Get("x")!.IsTimestamp);
    }

    // ─────────────────────────────────────────────────────────────────
    // Binary Edge Cases
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_BinaryEmpty()
    {
        var doc = Core.Odin.Parse("x = ^");
        Assert.True(doc.Get("x")!.IsBinary);
    }

    [Fact]
    public void Parse_BinaryWithAlgorithm()
    {
        var doc = Core.Odin.Parse("x = ^sha256:SGVsbG8=");
        Assert.True(doc.Get("x")!.IsBinary);
    }

    // ─────────────────────────────────────────────────────────────────
    // Reference Edge Cases
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_ReferenceWithArrayIndex()
    {
        var doc = Core.Odin.Parse("x = @items[0]");
        Assert.True(doc.Get("x")!.IsReference);
        Assert.Equal("items[0]", doc.Get("x")!.AsReference());
    }

    [Fact]
    public void Parse_ReferenceDeepPath()
    {
        var doc = Core.Odin.Parse("x = @a.b.c.d.e");
        Assert.Equal("a.b.c.d.e", doc.Get("x")!.AsReference());
    }

    // ─────────────────────────────────────────────────────────────────
    // Modifier Combinations with Various Types
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_RequiredCurrency()
    {
        var doc = Core.Odin.Parse("x = !#$99.99");
        var val = doc.Get("x");
        Assert.True(val!.IsRequired);
        Assert.True(val.IsCurrency);
    }

    [Fact]
    public void Parse_ConfidentialBoolean()
    {
        var doc = Core.Odin.Parse("x = *true");
        var val = doc.Get("x");
        Assert.True(val!.IsConfidential);
        Assert.True(val.IsBoolean);
    }

    [Fact]
    public void Parse_DeprecatedNull()
    {
        var doc = Core.Odin.Parse("x = -~");
        var val = doc.Get("x");
        Assert.True(val!.IsDeprecated);
        Assert.True(val.IsNull);
    }

    [Fact]
    public void Parse_DeprecatedPercent()
    {
        var doc = Core.Odin.Parse("x = -#%50");
        var val = doc.Get("x");
        Assert.True(val!.IsDeprecated);
        Assert.True(val.IsPercent);
    }

    [Fact]
    public void Parse_RequiredConfidentialNumber()
    {
        var doc = Core.Odin.Parse("x = !*#3.14");
        var val = doc.Get("x");
        Assert.True(val!.IsRequired);
        Assert.True(val.IsConfidential);
        Assert.True(val.IsNumber);
    }

    [Fact]
    public void Parse_AllModifiersOnInteger()
    {
        var doc = Core.Odin.Parse("x = !-*##42");
        var val = doc.Get("x");
        Assert.True(val!.IsRequired);
        Assert.True(val.IsDeprecated);
        Assert.True(val.IsConfidential);
        Assert.True(val.IsInteger);
    }

    // ─────────────────────────────────────────────────────────────────
    // Section Edge Cases
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_EmptySectionHeader()
    {
        var doc = Core.Odin.Parse("{}\nx = ##42");
        Assert.Equal(42L, doc.GetInteger("x"));
    }

    [Fact]
    public void Parse_SectionWithUnderscoreInName()
    {
        var doc = Core.Odin.Parse("{My_Section}\nval = ##1");
        Assert.Equal(1L, doc.GetInteger("My_Section.val"));
    }

    [Fact]
    public void Parse_ManySections_AllAccessible()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < 20; i++)
            sb.AppendLine($"{{S{i}}}\nval = ##{i}");
        var doc = Core.Odin.Parse(sb.ToString());
        Assert.Equal(0L, doc.GetInteger("S0.val"));
        Assert.Equal(19L, doc.GetInteger("S19.val"));
    }

    [Fact]
    public void Parse_SectionAfterMetadata()
    {
        var doc = Core.Odin.Parse("{$}\nodin = \"1.0.0\"\n\n{Data}\nname = \"test\"");
        Assert.Equal("1.0.0", doc.GetString("$.odin"));
        Assert.Equal("test", doc.GetString("Data.name"));
    }

    [Fact]
    public void Parse_MultipleSectionsWithSameField()
    {
        var doc = Core.Odin.Parse("{A}\nname = \"a\"\n{B}\nname = \"b\"");
        Assert.Equal("a", doc.GetString("A.name"));
        Assert.Equal("b", doc.GetString("B.name"));
    }

    // ─────────────────────────────────────────────────────────────────
    // Array Edge Cases
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_ArraySingleElement()
    {
        var doc = Core.Odin.Parse("items[0] = \"only\"");
        Assert.Equal("only", doc.GetString("items[0]"));
    }

    [Fact]
    public void Parse_ArrayManyElements()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < 50; i++)
            sb.AppendLine($"items[{i}] = ##{i}");
        var doc = Core.Odin.Parse(sb.ToString());
        Assert.Equal(0L, doc.GetInteger("items[0]"));
        Assert.Equal(49L, doc.GetInteger("items[49]"));
    }

    [Fact]
    public void Parse_ArrayNonContiguous_Throws()
    {
        var ex = Assert.Throws<OdinParseException>(() =>
            Core.Odin.Parse("items[0] = \"a\"\nitems[2] = \"c\""));
        Assert.Equal(ParseErrorCode.NonContiguousArrayIndices, ex.ErrorCode);
    }

    [Fact]
    public void Parse_ArrayInSectionWithTypes()
    {
        var doc = Core.Odin.Parse("{D}\nitems[0] = ##1\nitems[1] = ##2\nitems[2] = ##3");
        Assert.Equal(1L, doc.GetInteger("D.items[0]"));
        Assert.Equal(3L, doc.GetInteger("D.items[2]"));
    }

    [Fact]
    public void Parse_ArrayWithStringValues()
    {
        var doc = Core.Odin.Parse("names[0] = \"Alice\"\nnames[1] = \"Bob\"");
        Assert.Equal("Alice", doc.GetString("names[0]"));
        Assert.Equal("Bob", doc.GetString("names[1]"));
    }

    [Fact]
    public void Parse_ArrayWithMixedTypes()
    {
        var doc = Core.Odin.Parse("items[0] = \"str\"\nitems[1] = ##42\nitems[2] = true");
        Assert.Equal("str", doc.GetString("items[0]"));
        Assert.Equal(42L, doc.GetInteger("items[1]"));
        Assert.Equal(true, doc.GetBoolean("items[2]"));
    }

    // ─────────────────────────────────────────────────────────────────
    // Duplicate Path Handling
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_DuplicatePath_DefaultThrows()
    {
        var ex = Assert.Throws<OdinParseException>(() =>
            Core.Odin.Parse("x = ##1\nx = ##2"));
        Assert.Equal(ParseErrorCode.DuplicatePathAssignment, ex.ErrorCode);
    }

    [Fact]
    public void Parse_DuplicatePath_AllowDuplicates()
    {
        var doc = Core.Odin.Parse("x = ##1\nx = ##2",
            new ParseOptions { AllowDuplicates = true });
        // Second assignment overwrites first
        Assert.Equal(2L, doc.GetInteger("x"));
    }

    // ─────────────────────────────────────────────────────────────────
    // Comment Edge Cases
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_CommentAtStartOfLine()
    {
        var doc = Core.Odin.Parse("; comment\nx = ##1");
        Assert.Equal(1L, doc.GetInteger("x"));
    }

    [Fact]
    public void Parse_CommentAfterValue()
    {
        var doc = Core.Odin.Parse("x = ##42 ; this is 42");
        Assert.Equal(42L, doc.GetInteger("x"));
    }

    [Fact]
    public void Parse_ConsecutiveComments()
    {
        var doc = Core.Odin.Parse("; comment 1\n; comment 2\n; comment 3\nx = ##1");
        Assert.Equal(1L, doc.GetInteger("x"));
    }

    [Fact]
    public void Parse_CommentOnly_NoAssignments()
    {
        var doc = Core.Odin.Parse("; just a comment");
        Assert.Empty(doc.Assignments);
    }

    // ─────────────────────────────────────────────────────────────────
    // Multiple Documents
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ParseDocuments_Empty_ReturnsSingleEmpty()
    {
        var docs = Core.Odin.ParseDocuments("");
        Assert.Single(docs);
        Assert.Empty(docs[0].Assignments);
    }

    [Fact]
    public void ParseDocuments_FiveDocuments()
    {
        var input = "a = ##1\n---\nb = ##2\n---\nc = ##3\n---\nd = ##4\n---\ne = ##5";
        var docs = Core.Odin.ParseDocuments(input);
        Assert.Equal(5, docs.Count);
    }

    [Fact]
    public void ParseDocuments_SeparatorWithBlankLines()
    {
        var docs = Core.Odin.ParseDocuments("a = ##1\n\n---\n\nb = ##2");
        Assert.Equal(2, docs.Count);
    }

    [Fact]
    public void ParseDocuments_EachDocumentIndependent()
    {
        var docs = Core.Odin.ParseDocuments("{A}\nval = ##1\n---\n{B}\nval = ##2");
        Assert.Equal(1L, docs[0].GetInteger("A.val"));
        Assert.Equal(2L, docs[1].GetInteger("B.val"));
    }

    // ─────────────────────────────────────────────────────────────────
    // Whitespace Edge Cases
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_TabsAroundEquals()
    {
        var doc = Core.Odin.Parse("x\t=\t##42");
        Assert.Equal(42L, doc.GetInteger("x"));
    }

    [Fact]
    public void Parse_MixedTabsAndSpaces()
    {
        var doc = Core.Odin.Parse("  \t x  \t = \t ##42");
        Assert.Equal(42L, doc.GetInteger("x"));
    }

    [Fact]
    public void Parse_OnlyBlankLines()
    {
        var doc = Core.Odin.Parse("\n\n\n\n");
        Assert.Empty(doc.Assignments);
    }

    [Fact]
    public void Parse_WindowsLineEndings()
    {
        var doc = Core.Odin.Parse("a = ##1\r\nb = ##2\r\n");
        Assert.Equal(1L, doc.GetInteger("a"));
        Assert.Equal(2L, doc.GetInteger("b"));
    }

    [Fact]
    public void Parse_MacLineEndings()
    {
        // Old Mac style (\r only) - newline at \r, then standalone \n
        var doc = Core.Odin.Parse("a = ##1\r\n");
        Assert.Equal(1L, doc.GetInteger("a"));
    }

    // ─────────────────────────────────────────────────────────────────
    // Error Cases
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_UnterminatedString_HasCorrectErrorCode()
    {
        var ex = Assert.Throws<OdinParseException>(() => Core.Odin.Parse("x = \"open"));
        Assert.Equal(ParseErrorCode.UnterminatedString, ex.ErrorCode);
        Assert.Equal("P004", ex.Code);
    }

    [Fact]
    public void Parse_InvalidEscape_HasCorrectErrorCode()
    {
        var ex = Assert.Throws<OdinParseException>(() => Core.Odin.Parse("x = \"\\q\""));
        Assert.Equal(ParseErrorCode.InvalidEscapeSequence, ex.ErrorCode);
        Assert.Equal("P005", ex.Code);
    }

    [Fact]
    public void Parse_InvalidTypePrefix_BareHash()
    {
        var ex = Assert.Throws<OdinParseException>(() => Core.Odin.Parse("x = #"));
        Assert.Equal(ParseErrorCode.InvalidTypePrefix, ex.ErrorCode);
    }

    [Fact]
    public void Parse_InvalidHeaderSyntax()
    {
        var ex = Assert.Throws<OdinParseException>(() => Core.Odin.Parse("{unclosed"));
        Assert.Equal(ParseErrorCode.InvalidHeaderSyntax, ex.ErrorCode);
    }

    [Fact]
    public void Parse_NegativeArrayIndex()
    {
        var ex = Assert.Throws<OdinParseException>(() => Core.Odin.Parse("items[-1] = \"a\""));
        Assert.Equal(ParseErrorCode.InvalidArrayIndex, ex.ErrorCode);
    }

    // ─────────────────────────────────────────────────────────────────
    // Document API Edge Cases
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Document_With_NewValue_OnEmptyDoc()
    {
        var doc = OdinDocument.Empty()
            .With("x", OdinValues.Integer(42));
        Assert.Equal(42L, doc.GetInteger("x"));
    }

    [Fact]
    public void Document_Without_AllFields_LeavesEmpty()
    {
        var doc = Core.Odin.Parse("x = ##1")
            .Without("x");
        Assert.Empty(doc.Paths());
    }

    [Fact]
    public void Document_Paths_ReturnsAllPaths()
    {
        var doc = Core.Odin.Parse("a = ##1\nb = ##2\nc = ##3");
        var paths = doc.Paths();
        Assert.Equal(3, paths.Count);
        Assert.Contains("a", paths);
        Assert.Contains("b", paths);
        Assert.Contains("c", paths);
    }

    [Fact]
    public void Document_Flatten_HandlesNullValues()
    {
        var doc = Core.Odin.Parse("x = ~\ny = ##1");
        var flat = doc.Flatten();
        Assert.True(flat.ContainsKey("y"));
    }

    [Fact]
    public void Document_Resolve_ChainOfThree()
    {
        var doc = Core.Odin.Parse("a = @b\nb = @c\nc = ##99");
        var val = doc.Resolve("a");
        Assert.Equal(99L, val!.AsInt64());
    }

    [Fact]
    public void Document_Resolve_ThreeWayCircular_Throws()
    {
        var doc = Core.Odin.Parse("a = @b\nb = @c\nc = @a");
        Assert.Throws<InvalidOperationException>(() => doc.Resolve("a"));
    }

    // ─────────────────────────────────────────────────────────────────
    // Large Document Stress Tests
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_500Fields()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < 500; i++)
            sb.AppendLine($"field_{i} = ##{i}");
        var doc = Core.Odin.Parse(sb.ToString());
        Assert.Equal(499L, doc.GetInteger("field_499"));
    }

    [Fact]
    public void Parse_100Sections_EachWith5Fields()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < 100; i++)
        {
            sb.AppendLine($"{{Section{i}}}");
            for (int j = 0; j < 5; j++)
                sb.AppendLine($"f{j} = ##{i * 5 + j}");
        }
        var doc = Core.Odin.Parse(sb.ToString());
        Assert.Equal(0L, doc.GetInteger("Section0.f0"));
        Assert.Equal(499L, doc.GetInteger("Section99.f4"));
    }

    // ─────────────────────────────────────────────────────────────────
    // Import and Schema Directive Parsing
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_ImportDirective_RecordedInDocument()
    {
        var doc = Core.Odin.Parse("@import \"types.odin\"");
        Assert.NotEmpty(doc.Imports);
    }

    [Fact]
    public void Parse_ImportWithAlias()
    {
        var doc = Core.Odin.Parse("@import \"types.odin\" as types");
        Assert.NotEmpty(doc.Imports);
        Assert.Equal("types", doc.Imports[0].Alias);
    }

    [Fact]
    public void Parse_SchemaDirective_RecordedInDocument()
    {
        var doc = Core.Odin.Parse("@schema \"schema.odin\"");
        Assert.NotEmpty(doc.Schemas);
    }

    [Fact]
    public void Parse_ImportEmptyPath_Throws()
    {
        Assert.Throws<OdinParseException>(() => Core.Odin.Parse("@import"));
    }

    [Fact]
    public void Parse_ImportTrailingAs_Throws()
    {
        Assert.Throws<OdinParseException>(() => Core.Odin.Parse("@import \"file.odin\" as"));
    }

    // ─────────────────────────────────────────────────────────────────
    // OdinParseException Properties
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void OdinParseException_HasLineAndColumn()
    {
        try
        {
            Core.Odin.Parse("x = \"unterminated");
            Assert.Fail("Should have thrown");
        }
        catch (OdinParseException ex)
        {
            Assert.True(ex.Line >= 1);
            Assert.True(ex.Column >= 1);
        }
    }

    [Fact]
    public void OdinParseException_MessageContainsCode()
    {
        try
        {
            Core.Odin.Parse("x = \"unterminated");
            Assert.Fail("Should have thrown");
        }
        catch (OdinParseException ex)
        {
            Assert.Contains("Unterminated string", ex.Message);
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // Type System Queries
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Value_IsNumeric_TrueForAllNumericTypes()
    {
        Assert.True(Core.Odin.Parse("x = ##1").Get("x")!.IsNumeric);
        Assert.True(Core.Odin.Parse("x = #1.0").Get("x")!.IsNumeric);
        Assert.True(Core.Odin.Parse("x = #$1.00").Get("x")!.IsNumeric);
        Assert.True(Core.Odin.Parse("x = #%50").Get("x")!.IsNumeric);
    }

    [Fact]
    public void Value_IsNumeric_FalseForNonNumericTypes()
    {
        Assert.False(Core.Odin.Parse("x = \"str\"").Get("x")!.IsNumeric);
        Assert.False(Core.Odin.Parse("x = true").Get("x")!.IsNumeric);
        Assert.False(Core.Odin.Parse("x = ~").Get("x")!.IsNumeric);
    }

    [Fact]
    public void Value_IsTemporal_TrueForTemporalTypes()
    {
        Assert.True(Core.Odin.Parse("x = 2024-01-01").Get("x")!.IsTemporal);
        Assert.True(Core.Odin.Parse("x = 2024-01-01T00:00:00Z").Get("x")!.IsTemporal);
    }

    [Fact]
    public void Value_IsTemporal_FalseForNonTemporal()
    {
        Assert.False(Core.Odin.Parse("x = ##42").Get("x")!.IsTemporal);
        Assert.False(Core.Odin.Parse("x = \"str\"").Get("x")!.IsTemporal);
    }

    [Fact]
    public void Value_IsNull_OnlyForNull()
    {
        Assert.True(Core.Odin.Parse("x = ~").Get("x")!.IsNull);
        Assert.False(Core.Odin.Parse("x = ##0").Get("x")!.IsNull);
        Assert.False(Core.Odin.Parse("x = \"\"").Get("x")!.IsNull);
        Assert.False(Core.Odin.Parse("x = false").Get("x")!.IsNull);
    }

    [Fact]
    public void Value_IsBoolean_OnlyForBoolean()
    {
        Assert.True(Core.Odin.Parse("x = true").Get("x")!.IsBoolean);
        Assert.True(Core.Odin.Parse("x = false").Get("x")!.IsBoolean);
        Assert.False(Core.Odin.Parse("x = ##1").Get("x")!.IsBoolean);
    }

    [Fact]
    public void Value_IsString_OnlyForString()
    {
        Assert.True(Core.Odin.Parse("x = \"hello\"").Get("x")!.IsString);
        Assert.False(Core.Odin.Parse("x = ##42").Get("x")!.IsString);
    }

    [Fact]
    public void Value_IsReference_OnlyForReference()
    {
        Assert.True(Core.Odin.Parse("x = @other").Get("x")!.IsReference);
        Assert.False(Core.Odin.Parse("x = \"@not\"").Get("x")!.IsReference);
    }

    [Fact]
    public void Value_IsBinary_OnlyForBinary()
    {
        Assert.True(Core.Odin.Parse("x = ^SGVsbG8=").Get("x")!.IsBinary);
        Assert.False(Core.Odin.Parse("x = \"^not\"").Get("x")!.IsBinary);
    }

    // ─────────────────────────────────────────────────────────────────
    // AsXxx Conversion Methods
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Value_AsInt64_ForInteger()
    {
        var val = Core.Odin.Parse("x = ##42").Get("x");
        Assert.Equal(42L, val!.AsInt64());
    }

    [Fact]
    public void Value_AsInt64_NullForString()
    {
        var val = Core.Odin.Parse("x = \"hello\"").Get("x");
        Assert.Null(val!.AsInt64());
    }

    [Fact]
    public void Value_AsDouble_ForNumber()
    {
        var val = Core.Odin.Parse("x = #3.14").Get("x");
        Assert.True(Math.Abs(val!.AsDouble()!.Value - 3.14) < 0.001);
    }

    [Fact]
    public void Value_AsDouble_ForInteger()
    {
        var val = Core.Odin.Parse("x = ##42").Get("x");
        Assert.Equal(42.0, val!.AsDouble());
    }

    [Fact]
    public void Value_AsBool_ForBoolean()
    {
        Assert.True(Core.Odin.Parse("x = true").Get("x")!.AsBool());
        Assert.False(Core.Odin.Parse("x = false").Get("x")!.AsBool());
    }

    [Fact]
    public void Value_AsBool_NullForNonBoolean()
    {
        Assert.Null(Core.Odin.Parse("x = ##42").Get("x")!.AsBool());
    }

    [Fact]
    public void Value_AsString_ForString()
    {
        Assert.Equal("hello", Core.Odin.Parse("x = \"hello\"").Get("x")!.AsString());
    }

    [Fact]
    public void Value_AsReference_ForReference()
    {
        Assert.Equal("other", Core.Odin.Parse("x = @other").Get("x")!.AsReference());
    }

    [Fact]
    public void Value_AsReference_NullForNonReference()
    {
        Assert.Null(Core.Odin.Parse("x = \"hello\"").Get("x")!.AsReference());
    }

    // ─────────────────────────────────────────────────────────────────
    // OdinValues Factory
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void OdinValues_String_CreatesString()
    {
        var val = OdinValues.String("test");
        Assert.True(val.IsString);
        Assert.Equal("test", val.AsString());
    }

    [Fact]
    public void OdinValues_Integer_CreatesInteger()
    {
        var val = OdinValues.Integer(99);
        Assert.True(val.IsInteger);
        Assert.Equal(99L, val.AsInt64());
    }

    [Fact]
    public void OdinValues_Boolean_CreatesBoolean()
    {
        Assert.True(OdinValues.Boolean(true).AsBool());
        Assert.False(OdinValues.Boolean(false).AsBool());
    }

    [Fact]
    public void OdinValues_Null_CreatesNull()
    {
        Assert.True(OdinValues.Null().IsNull);
    }

    [Fact]
    public void OdinValues_Reference_CreatesReference()
    {
        var val = OdinValues.Reference("path.to.thing");
        Assert.True(val.IsReference);
        Assert.Equal("path.to.thing", val.AsReference());
    }
}
