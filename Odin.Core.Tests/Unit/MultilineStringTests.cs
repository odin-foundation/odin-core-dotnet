using System.Linq;
using Odin.Core;
using Odin.Core.Parsing;
using Odin.Core.Types;
using Xunit;

namespace Odin.Core.Tests.Unit;

// Triple-quoted multiline string literals: tokenization and parsing.
public class MultilineStringTests
{
    private static System.Collections.Generic.List<Token> Tokenize(string source) =>
        Tokenizer.Tokenize(source, ParseOptions.Default);

    // ─────────────────────────────────────────────────────────────────
    // Happy path
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Multiline_SpansNewlines_RetainedVerbatim()
    {
        var doc = Core.Odin.Parse("field = \"\"\"hello\nworld\"\"\"");
        Assert.Equal("hello\nworld", doc.GetString("field"));
    }

    [Fact]
    public void Multiline_SingleLine_ParsesAsString()
    {
        var doc = Core.Odin.Parse("field = \"\"\"one line\"\"\"");
        Assert.Equal("one line", doc.GetString("field"));
    }

    [Fact]
    public void Multiline_EmitsMultilineToken()
    {
        var tokens = Tokenize("field = \"\"\"a\nb\"\"\"");
        var ml = tokens.First(t => t.TokenType == TokenType.MultilineString);
        Assert.Equal("a\nb", ml.Value);
    }

    // ─────────────────────────────────────────────────────────────────
    // Edge cases
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Multiline_Empty_ProducesEmptyString()
    {
        var doc = Core.Odin.Parse("field = \"\"\"\"\"\"");
        Assert.Equal("", doc.GetString("field"));
    }

    [Fact]
    public void Multiline_LeadingTrailingNewline_RetainedVerbatim()
    {
        var doc = Core.Odin.Parse("field = \"\"\"\ninner\n\"\"\"");
        Assert.Equal("\ninner\n", doc.GetString("field"));
    }

    [Fact]
    public void Multiline_BackslashAndEmbeddedQuotes_KeptVerbatim()
    {
        var doc = Core.Odin.Parse("field = \"\"\"C:\\path say \"hi\" done\"\"\"");
        Assert.Equal("C:\\path say \"hi\" done", doc.GetString("field"));
    }

    // ─────────────────────────────────────────────────────────────────
    // Error
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Multiline_Unterminated_ThrowsP004()
    {
        var ex = Assert.Throws<OdinParseException>(() =>
            Core.Odin.Parse("field = \"\"\"never closed\n"));
        Assert.Equal("P004", ex.Code);
    }
}
