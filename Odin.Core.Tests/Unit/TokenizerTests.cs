using System.Collections.Generic;
using System.Linq;
using Odin.Core.Parsing;
using Odin.Core.Types;
using Xunit;

namespace Odin.Core.Tests.Unit;

public class TokenizerTests
{
    private static List<Token> Tokenize(string source)
    {
        return Tokenizer.Tokenize(source, ParseOptions.Default);
    }

    private static List<Token> NonTrivialTokens(string source)
    {
        return Tokenize(source)
            .Where(t => t.TokenType != TokenType.Newline &&
                        t.TokenType != TokenType.Comment &&
                        t.TokenType != TokenType.Eof)
            .ToList();
    }

    // ─────────────────────────────────────────────────────────────────
    // Basic Token Types
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Tokenize_EmptyString_ReturnsOnlyEof()
    {
        var tokens = Tokenize("");
        Assert.Single(tokens);
        Assert.Equal(TokenType.Eof, tokens[0].TokenType);
    }

    [Fact]
    public void Tokenize_SimpleAssignment_ProducesPathEqualsString()
    {
        var tokens = NonTrivialTokens("name = \"Alice\"");
        Assert.Equal(3, tokens.Count);
        Assert.Equal(TokenType.Path, tokens[0].TokenType);
        Assert.Equal("name", tokens[0].Value);
        Assert.Equal(TokenType.Equals, tokens[1].TokenType);
        Assert.Equal(TokenType.QuotedString, tokens[2].TokenType);
        Assert.Equal("Alice", tokens[2].Value);
    }

    [Fact]
    public void Tokenize_IntegerPrefix_ProducesIntegerToken()
    {
        var tokens = NonTrivialTokens("x = ##42");
        Assert.Equal(TokenType.IntegerPrefix, tokens[2].TokenType);
        Assert.Equal("42", tokens[2].Value);
    }

    [Fact]
    public void Tokenize_NumberPrefix_ProducesNumberToken()
    {
        var tokens = NonTrivialTokens("x = #3.14");
        Assert.Equal(TokenType.NumberPrefix, tokens[2].TokenType);
        Assert.Equal("3.14", tokens[2].Value);
    }

    [Fact]
    public void Tokenize_CurrencyPrefix_ProducesCurrencyToken()
    {
        var tokens = NonTrivialTokens("x = #$99.99");
        Assert.Equal(TokenType.CurrencyPrefix, tokens[2].TokenType);
        Assert.Equal("99.99", tokens[2].Value);
    }

    [Fact]
    public void Tokenize_CurrencyWithCode_IncludesCode()
    {
        var tokens = NonTrivialTokens("x = #$99.99:USD");
        Assert.Equal(TokenType.CurrencyPrefix, tokens[2].TokenType);
        Assert.Equal("99.99:USD", tokens[2].Value);
    }

    [Fact]
    public void Tokenize_PercentPrefix_ProducesPercentToken()
    {
        var tokens = NonTrivialTokens("x = #%50");
        Assert.Equal(TokenType.PercentPrefix, tokens[2].TokenType);
        Assert.Equal("50", tokens[2].Value);
    }

    [Fact]
    public void Tokenize_BooleanLiteral_True()
    {
        var tokens = NonTrivialTokens("x = true");
        Assert.Equal(TokenType.BooleanLiteral, tokens[2].TokenType);
        Assert.Equal("true", tokens[2].Value);
    }

    [Fact]
    public void Tokenize_BooleanLiteral_False()
    {
        var tokens = NonTrivialTokens("x = false");
        Assert.Equal(TokenType.BooleanLiteral, tokens[2].TokenType);
        Assert.Equal("false", tokens[2].Value);
    }

    [Fact]
    public void Tokenize_NullValue()
    {
        var tokens = NonTrivialTokens("x = ~");
        Assert.Equal(TokenType.Null, tokens[2].TokenType);
    }

    [Fact]
    public void Tokenize_ReferenceValue()
    {
        var tokens = NonTrivialTokens("x = @other.path");
        Assert.Equal(TokenType.ReferencePrefix, tokens[2].TokenType);
        Assert.Equal("other.path", tokens[2].Value);
    }

    [Fact]
    public void Tokenize_BinaryPrefix()
    {
        var tokens = NonTrivialTokens("x = ^SGVsbG8=");
        Assert.Equal(TokenType.BinaryPrefix, tokens[2].TokenType);
        Assert.Equal("SGVsbG8=", tokens[2].Value);
    }

    // ─────────────────────────────────────────────────────────────────
    // Modifiers
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Tokenize_RequiredModifier()
    {
        var tokens = NonTrivialTokens("x = !\"val\"");
        Assert.Equal(TokenType.Modifier, tokens[2].TokenType);
        Assert.Equal("!", tokens[2].Value);
    }

    [Fact]
    public void Tokenize_ConfidentialModifier()
    {
        var tokens = NonTrivialTokens("x = *\"val\"");
        Assert.Equal(TokenType.Modifier, tokens[2].TokenType);
        Assert.Equal("*", tokens[2].Value);
    }

    [Fact]
    public void Tokenize_DeprecatedModifier()
    {
        var tokens = NonTrivialTokens("x = -\"val\"");
        Assert.Equal(TokenType.Modifier, tokens[2].TokenType);
        Assert.Equal("-", tokens[2].Value);
    }

    [Fact]
    public void Tokenize_AllModifiers_InOrder()
    {
        var tokens = NonTrivialTokens("x = !-*\"val\"");
        Assert.Equal(TokenType.Modifier, tokens[2].TokenType);
        Assert.Equal("!", tokens[2].Value);
        Assert.Equal(TokenType.Modifier, tokens[3].TokenType);
        Assert.Equal("-", tokens[3].Value);
        Assert.Equal(TokenType.Modifier, tokens[4].TokenType);
        Assert.Equal("*", tokens[4].Value);
        Assert.Equal(TokenType.QuotedString, tokens[5].TokenType);
    }

    // ─────────────────────────────────────────────────────────────────
    // Headers
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Tokenize_MetadataHeader()
    {
        var tokens = NonTrivialTokens("{$}");
        Assert.Single(tokens);
        Assert.Equal(TokenType.Header, tokens[0].TokenType);
        Assert.Equal("$", tokens[0].Value);
    }

    [Fact]
    public void Tokenize_SectionHeader()
    {
        var tokens = NonTrivialTokens("{Customer}");
        Assert.Single(tokens);
        Assert.Equal(TokenType.Header, tokens[0].TokenType);
        Assert.Equal("Customer", tokens[0].Value);
    }

    [Fact]
    public void Tokenize_TypeDefinitionHeader()
    {
        var tokens = NonTrivialTokens("{@Person}");
        Assert.Single(tokens);
        Assert.Equal(TokenType.Header, tokens[0].TokenType);
        Assert.Equal("@Person", tokens[0].Value);
    }

    [Fact]
    public void Tokenize_EmptyHeader()
    {
        var tokens = NonTrivialTokens("{}");
        Assert.Single(tokens);
        Assert.Equal(TokenType.Header, tokens[0].TokenType);
        Assert.Equal("", tokens[0].Value);
    }

    [Fact]
    public void Tokenize_UnterminatedHeader_Throws()
    {
        var ex = Assert.Throws<OdinParseException>(() => Tokenize("{Customer"));
        Assert.Equal(ParseErrorCode.InvalidHeaderSyntax, ex.ErrorCode);
    }

    [Fact]
    public void Tokenize_HeaderWithNewline_Throws()
    {
        var ex = Assert.Throws<OdinParseException>(() => Tokenize("{Cus\ntomer}"));
        Assert.Equal(ParseErrorCode.InvalidHeaderSyntax, ex.ErrorCode);
    }

    // ─────────────────────────────────────────────────────────────────
    // Comments
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Tokenize_Comment()
    {
        var tokens = Tokenize("; this is a comment");
        Assert.Equal(TokenType.Comment, tokens[0].TokenType);
        Assert.Contains("this is a comment", tokens[0].Value);
    }

    [Fact]
    public void Tokenize_InlineComment_SeparateToken()
    {
        var tokens = Tokenize("x = ##42 ; comment");
        Assert.Contains(tokens, t => t.TokenType == TokenType.Comment);
    }

    // ─────────────────────────────────────────────────────────────────
    // Strings
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Tokenize_EmptyString()
    {
        var tokens = NonTrivialTokens("x = \"\"");
        Assert.Equal(TokenType.QuotedString, tokens[2].TokenType);
        Assert.Equal("", tokens[2].Value);
    }

    [Fact]
    public void Tokenize_EscapedQuotes()
    {
        var tokens = NonTrivialTokens("x = \"say \\\"hello\\\"\"");
        Assert.Equal("say \"hello\"", tokens[2].Value);
    }

    [Fact]
    public void Tokenize_EscapedBackslash()
    {
        var tokens = NonTrivialTokens("x = \"C:\\\\path\"");
        Assert.Equal("C:\\path", tokens[2].Value);
    }

    [Fact]
    public void Tokenize_EscapedNewline()
    {
        var tokens = NonTrivialTokens("x = \"a\\nb\"");
        Assert.Equal("a\nb", tokens[2].Value);
    }

    [Fact]
    public void Tokenize_EscapedTab()
    {
        var tokens = NonTrivialTokens("x = \"a\\tb\"");
        Assert.Equal("a\tb", tokens[2].Value);
    }

    [Fact]
    public void Tokenize_EscapedCarriageReturn()
    {
        var tokens = NonTrivialTokens("x = \"a\\rb\"");
        Assert.Equal("a\rb", tokens[2].Value);
    }

    [Fact]
    public void Tokenize_EscapedForwardSlash()
    {
        var tokens = NonTrivialTokens("x = \"a\\/b\"");
        Assert.Equal("a/b", tokens[2].Value);
    }

    [Fact]
    public void Tokenize_EscapedNull()
    {
        var tokens = NonTrivialTokens("x = \"a\\0b\"");
        Assert.Equal("a\0b", tokens[2].Value);
    }

    [Fact]
    public void Tokenize_UnicodeEscape_4digit()
    {
        var tokens = NonTrivialTokens("x = \"\\u0041\"");
        Assert.Equal("A", tokens[2].Value);
    }

    [Fact]
    public void Tokenize_UnicodeEscape_8digit()
    {
        var tokens = NonTrivialTokens("x = \"\\U0001F600\"");
        // Should produce the grinning face emoji
        Assert.Equal("\U0001F600", tokens[2].Value);
    }

    [Fact]
    public void Tokenize_UnterminatedString_Throws()
    {
        var ex = Assert.Throws<OdinParseException>(() => Tokenize("x = \"unterminated"));
        Assert.Equal(ParseErrorCode.UnterminatedString, ex.ErrorCode);
    }

    [Fact]
    public void Tokenize_StringWithNewline_Throws()
    {
        var ex = Assert.Throws<OdinParseException>(() => Tokenize("x = \"line1\nline2\""));
        Assert.Equal(ParseErrorCode.UnterminatedString, ex.ErrorCode);
    }

    [Fact]
    public void Tokenize_InvalidEscapeSequence_Throws()
    {
        var ex = Assert.Throws<OdinParseException>(() => Tokenize("x = \"\\q\""));
        Assert.Equal(ParseErrorCode.InvalidEscapeSequence, ex.ErrorCode);
    }

    [Fact]
    public void Tokenize_IncompleteUnicodeEscape_Throws()
    {
        Assert.Throws<OdinParseException>(() => Tokenize("x = \"\\u00\""));
    }

    // ─────────────────────────────────────────────────────────────────
    // Dates and Timestamps
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Tokenize_DateLiteral()
    {
        var tokens = NonTrivialTokens("x = 2024-06-15");
        Assert.Equal(TokenType.DateLiteral, tokens[2].TokenType);
        Assert.Equal("2024-06-15", tokens[2].Value);
    }

    [Fact]
    public void Tokenize_TimestampLiteral()
    {
        var tokens = NonTrivialTokens("x = 2024-06-15T14:30:00Z");
        Assert.Equal(TokenType.TimestampLiteral, tokens[2].TokenType);
        Assert.Equal("2024-06-15T14:30:00Z", tokens[2].Value);
    }

    [Fact]
    public void Tokenize_TimeLiteral()
    {
        var tokens = NonTrivialTokens("x = T14:30:00");
        Assert.Equal(TokenType.TimeLiteral, tokens[2].TokenType);
    }

    [Fact]
    public void Tokenize_DurationLiteral()
    {
        var tokens = NonTrivialTokens("x = P1Y6M");
        Assert.Equal(TokenType.DurationLiteral, tokens[2].TokenType);
        Assert.Equal("P1Y6M", tokens[2].Value);
    }

    [Fact]
    public void Tokenize_DurationWithTime()
    {
        var tokens = NonTrivialTokens("x = PT2H30M");
        Assert.Equal(TokenType.DurationLiteral, tokens[2].TokenType);
        Assert.Equal("PT2H30M", tokens[2].Value);
    }

    // ─────────────────────────────────────────────────────────────────
    // Document Separator
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Tokenize_DocumentSeparator()
    {
        var tokens = NonTrivialTokens("---");
        Assert.Single(tokens);
        Assert.Equal(TokenType.DocumentSeparator, tokens[0].TokenType);
        Assert.Equal("---", tokens[0].Value);
    }

    [Fact]
    public void Tokenize_DocumentSeparator_Between()
    {
        var tokens = Tokenize("a = ##1\n---\nb = ##2");
        Assert.Contains(tokens, t => t.TokenType == TokenType.DocumentSeparator);
    }

    // ─────────────────────────────────────────────────────────────────
    // Directives
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Tokenize_Directive()
    {
        var tokens = NonTrivialTokens("x = ##42 :required");
        Assert.Contains(tokens, t => t.TokenType == TokenType.Directive && t.Value == "required");
    }

    [Fact]
    public void Tokenize_MultipleDirectives()
    {
        var tokens = NonTrivialTokens("x = \"val\" :required :confidential");
        var directives = tokens.Where(t => t.TokenType == TokenType.Directive).ToList();
        Assert.Equal(2, directives.Count);
    }

    // ─────────────────────────────────────────────────────────────────
    // Verb Prefix
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Tokenize_VerbPrefix()
    {
        var tokens = NonTrivialTokens("x = %upper");
        Assert.Contains(tokens, t => t.TokenType == TokenType.VerbPrefix);
    }

    [Fact]
    public void Tokenize_CustomVerbPrefix()
    {
        var tokens = NonTrivialTokens("x = %&myverb");
        var verb = tokens.First(t => t.TokenType == TokenType.VerbPrefix);
        Assert.StartsWith("&", verb.Value);
    }

    // ─────────────────────────────────────────────────────────────────
    // Imports and Schema Directives
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Tokenize_ImportDirective()
    {
        var tokens = Tokenize("@import \"types.odin\"");
        Assert.Contains(tokens, t => t.TokenType == TokenType.Import);
    }

    [Fact]
    public void Tokenize_SchemaDirective()
    {
        var tokens = Tokenize("@schema \"schema.odin\"");
        Assert.Contains(tokens, t => t.TokenType == TokenType.Schema);
    }

    [Fact]
    public void Tokenize_ConditionalDirective()
    {
        var tokens = Tokenize("@if status == \"active\"");
        Assert.Contains(tokens, t => t.TokenType == TokenType.Conditional);
    }

    // ─────────────────────────────────────────────────────────────────
    // Token Position Tracking
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Token_HasCorrectLineNumber()
    {
        var tokens = Tokenize("a = ##1\nb = ##2");
        var bToken = tokens.First(t => t.TokenType == TokenType.Path && t.Value == "b");
        Assert.Equal(2, bToken.Line);
    }

    [Fact]
    public void Token_HasCorrectColumn()
    {
        var tokens = Tokenize("name = \"Alice\"");
        Assert.Equal(1, tokens[0].Column);
    }

    [Fact]
    public void Token_StartEndOffsets()
    {
        var tokens = Tokenize("x = ##42");
        var path = tokens.First(t => t.TokenType == TokenType.Path);
        Assert.Equal(0, path.Start);
        Assert.True(path.End > path.Start);
    }

    // ─────────────────────────────────────────────────────────────────
    // Hash Error Handling
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Tokenize_BareHash_Throws()
    {
        var ex = Assert.Throws<OdinParseException>(() => Tokenize("x = #"));
        Assert.Equal(ParseErrorCode.InvalidTypePrefix, ex.ErrorCode);
    }

    [Fact]
    public void Tokenize_HashWithInvalidFollower_Throws()
    {
        var ex = Assert.Throws<OdinParseException>(() => Tokenize("x = #abc"));
        Assert.Equal(ParseErrorCode.InvalidTypePrefix, ex.ErrorCode);
    }

    // ─────────────────────────────────────────────────────────────────
    // Whitespace Handling
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Tokenize_TabsSkipped()
    {
        var tokens = NonTrivialTokens("\tx = ##42");
        Assert.Equal(TokenType.Path, tokens[0].TokenType);
    }

    [Fact]
    public void Tokenize_SpacesSkipped()
    {
        var tokens = NonTrivialTokens("   x = ##42");
        Assert.Equal(TokenType.Path, tokens[0].TokenType);
    }

    [Fact]
    public void Tokenize_CrLf_ProducesNewline()
    {
        var tokens = Tokenize("x = ##1\r\ny = ##2");
        Assert.Contains(tokens, t => t.TokenType == TokenType.Newline);
    }

    // ─────────────────────────────────────────────────────────────────
    // Negative Numbers
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Tokenize_NegativeInteger()
    {
        var tokens = NonTrivialTokens("x = ##-42");
        Assert.Equal(TokenType.IntegerPrefix, tokens[2].TokenType);
        Assert.Equal("-42", tokens[2].Value);
    }

    [Fact]
    public void Tokenize_NegativeNumber()
    {
        var tokens = NonTrivialTokens("x = #-3.14");
        Assert.Equal(TokenType.NumberPrefix, tokens[2].TokenType);
        Assert.Equal("-3.14", tokens[2].Value);
    }

    // ─────────────────────────────────────────────────────────────────
    // Size Limits
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Tokenize_ExceedsMaxDocumentSize_Throws()
    {
        var opts = new ParseOptions { MaxDocumentSize = 10 };
        var ex = Assert.Throws<OdinParseException>(
            () => Tokenizer.Tokenize("x = \"a very long value\"", opts));
        Assert.Equal(ParseErrorCode.MaximumDocumentSizeExceeded, ex.ErrorCode);
    }

    [Fact]
    public void Tokenize_NullSource_Throws()
    {
        Assert.Throws<System.ArgumentNullException>(
            () => Tokenizer.Tokenize(null!, ParseOptions.Default));
    }

    [Fact]
    public void Tokenize_NullOptions_Throws()
    {
        Assert.Throws<System.ArgumentNullException>(
            () => Tokenizer.Tokenize("x = ##1", null!));
    }

    // ─────────────────────────────────────────────────────────────────
    // Pipe and Comma
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Tokenize_Pipe()
    {
        var tokens = NonTrivialTokens("|");
        Assert.Single(tokens);
        Assert.Equal(TokenType.Pipe, tokens[0].TokenType);
    }

    [Fact]
    public void Tokenize_Comma()
    {
        var tokens = NonTrivialTokens(",");
        Assert.Single(tokens);
        Assert.Equal(TokenType.Comma, tokens[0].TokenType);
    }

    // ─────────────────────────────────────────────────────────────────
    // Boolean Prefix
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Tokenize_BooleanPrefix()
    {
        var tokens = NonTrivialTokens("x = ?true");
        Assert.Contains(tokens, t => t.TokenType == TokenType.BooleanPrefix);
    }

    // ─────────────────────────────────────────────────────────────────
    // Path with Array Index
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Tokenize_PathWithArrayIndex()
    {
        var tokens = NonTrivialTokens("items[0] = \"a\"");
        Assert.Equal(TokenType.Path, tokens[0].TokenType);
        Assert.Equal("items[0]", tokens[0].Value);
    }

    [Fact]
    public void Tokenize_PathWithDot()
    {
        var tokens = NonTrivialTokens("section.field = \"val\"");
        Assert.Equal(TokenType.Path, tokens[0].TokenType);
        Assert.Equal("section.field", tokens[0].Value);
    }

    [Fact]
    public void Tokenize_NegativeArrayIndex_Throws()
    {
        var ex = Assert.Throws<OdinParseException>(() => Tokenize("items[-1] = \"a\""));
        Assert.Equal(ParseErrorCode.InvalidArrayIndex, ex.ErrorCode);
    }

    // ─────────────────────────────────────────────────────────────────
    // Multiple lines
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Tokenize_MultipleLines_AllTokenized()
    {
        var tokens = Tokenize("a = ##1\nb = ##2\nc = ##3");
        var paths = tokens.Where(t => t.TokenType == TokenType.Path).ToList();
        Assert.Equal(3, paths.Count);
    }

    [Fact]
    public void Tokenize_BlankLines_ProduceNewlineTokens()
    {
        var tokens = Tokenize("a = ##1\n\n\nb = ##2");
        var newlines = tokens.Where(t => t.TokenType == TokenType.Newline).ToList();
        Assert.True(newlines.Count >= 2);
    }

    // ─────────────────────────────────────────────────────────────────
    // Surrogate Pairs
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Tokenize_SurrogatePairEscape()
    {
        // U+1F30D (Earth Globe) via surrogate pair: \uD83C\uDF0D
        var tokens = NonTrivialTokens("x = \"\\uD83C\\uDF0D\"");
        Assert.Equal("\U0001F30D", tokens[2].Value);
    }

    // ─────────────────────────────────────────────────────────────────
    // Token ToString
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Token_ToString_IncludesTypeAndValue()
    {
        var token = new Token(TokenType.Path, 0, 4, 1, 1, "name");
        var str = token.ToString();
        Assert.Contains("Path", str);
        Assert.Contains("name", str);
    }

    [Fact]
    public void Token_ToString_IncludesPosition()
    {
        var token = new Token(TokenType.Path, 0, 4, 3, 5, "x");
        var str = token.ToString();
        Assert.Contains("3", str);
        Assert.Contains("5", str);
    }

    // ─────────────────────────────────────────────────────────────────
    // Edge Cases
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Tokenize_OnlyWhitespace_ReturnsEof()
    {
        var tokens = Tokenize("   \t  ");
        Assert.Single(tokens);
        Assert.Equal(TokenType.Eof, tokens[0].TokenType);
    }

    [Fact]
    public void Tokenize_OnlyNewlines()
    {
        var tokens = Tokenize("\n\n\n");
        // Should have newline tokens + Eof
        Assert.True(tokens.Count >= 2);
        Assert.Equal(TokenType.Eof, tokens.Last().TokenType);
    }

    [Fact]
    public void Tokenize_TrailingWhitespaceAfterValue()
    {
        var tokens = NonTrivialTokens("x = ##42   ");
        Assert.Equal(3, tokens.Count);
    }

    [Fact]
    public void Tokenize_BareAt_IsReferencePrefix()
    {
        // A bare @ in value position
        var tokens = Tokenize("x = @");
        var refTok = tokens.FirstOrDefault(t => t.TokenType == TokenType.ReferencePrefix);
        Assert.NotNull(refTok);
        Assert.Equal(string.Empty, refTok!.Value);
    }

    [Fact]
    public void Tokenize_HeaderWithArrayBrackets()
    {
        var tokens = NonTrivialTokens("{$table.data[code, name]}");
        Assert.Equal(TokenType.Header, tokens[0].TokenType);
    }

    [Fact]
    public void Tokenize_NumberWithScientificNotation()
    {
        var tokens = NonTrivialTokens("x = #1.5e10");
        Assert.Equal(TokenType.NumberPrefix, tokens[2].TokenType);
        Assert.Equal("1.5e10", tokens[2].Value);
    }

    [Fact]
    public void Tokenize_NegativeCurrency()
    {
        var tokens = NonTrivialTokens("x = #$-100.00");
        Assert.Equal(TokenType.CurrencyPrefix, tokens[2].TokenType);
    }

    [Fact]
    public void Tokenize_NegativePercent()
    {
        var tokens = NonTrivialTokens("x = #%-5.5");
        Assert.Equal(TokenType.PercentPrefix, tokens[2].TokenType);
    }

    [Fact]
    public void Tokenize_LongString_NoTruncation()
    {
        var longStr = new string('a', 5000);
        var tokens = NonTrivialTokens($"x = \"{longStr}\"");
        Assert.Equal(longStr, tokens[2].Value);
    }
}
