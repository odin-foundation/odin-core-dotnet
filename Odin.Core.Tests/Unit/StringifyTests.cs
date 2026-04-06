using Odin.Core;
using Odin.Core.Types;
using Xunit;

namespace Odin.Core.Tests.Unit;

public class StringifyTests
{
    // ─────────────────────────────────────────────────────────────────
    // Round-Trip: Parse -> Stringify -> Parse
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void RoundTrip_SimpleString()
    {
        var input = "name = \"Alice\"\n";
        var doc = Core.Odin.Parse(input);
        var output = Core.Odin.Stringify(doc, new StringifyOptions { IncludeMetadata = false });
        var reparsed = Core.Odin.Parse(output);

        Assert.Equal("Alice", reparsed.GetString("name"));
    }

    [Fact]
    public void RoundTrip_Integer()
    {
        var input = "count = ##42\n";
        var doc = Core.Odin.Parse(input);
        var output = Core.Odin.Stringify(doc, new StringifyOptions { IncludeMetadata = false });
        var reparsed = Core.Odin.Parse(output);

        Assert.Equal(42L, reparsed.GetInteger("count"));
    }

    [Fact]
    public void RoundTrip_Boolean()
    {
        var input = "active = true\n";
        var doc = Core.Odin.Parse(input);
        var output = Core.Odin.Stringify(doc, new StringifyOptions { IncludeMetadata = false });
        var reparsed = Core.Odin.Parse(output);

        Assert.Equal(true, reparsed.GetBoolean("active"));
    }

    [Fact]
    public void RoundTrip_Null()
    {
        var input = "value = ~\n";
        var doc = Core.Odin.Parse(input);
        var output = Core.Odin.Stringify(doc, new StringifyOptions { IncludeMetadata = false });
        var reparsed = Core.Odin.Parse(output);

        Assert.True(reparsed.Get("value")!.IsNull);
    }

    [Fact]
    public void RoundTrip_Number()
    {
        var input = "pi = #3.14\n";
        var doc = Core.Odin.Parse(input);
        var output = Core.Odin.Stringify(doc, new StringifyOptions { IncludeMetadata = false });
        var reparsed = Core.Odin.Parse(output);

        Assert.Equal(3.14, reparsed.GetNumber("pi"));
    }

    // ─────────────────────────────────────────────────────────────────
    // Stringify Simple Document
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Stringify_SimpleDocument()
    {
        var builder = Core.Odin.Builder();
        builder.SetString("name", "Bob");
        builder.SetInteger("age", 25);
        var doc = builder.Build();

        var output = Core.Odin.Stringify(doc, new StringifyOptions { IncludeMetadata = false });

        Assert.Contains("name = \"Bob\"", output);
        Assert.Contains("age = ##25", output);
    }

    // ─────────────────────────────────────────────────────────────────
    // Stringify with Modifiers
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Stringify_WithRequiredModifier()
    {
        var builder = Core.Odin.Builder();
        builder.Set("name", OdinValues.String("Alice").WithModifiers(new OdinModifiers { Required = true }));
        var doc = builder.Build();

        var output = Core.Odin.Stringify(doc, new StringifyOptions { IncludeMetadata = false });
        Assert.Contains("!", output);
        Assert.Contains("\"Alice\"", output);
    }

    [Fact]
    public void Stringify_WithConfidentialModifier()
    {
        var builder = Core.Odin.Builder();
        builder.Set("ssn", OdinValues.String("123").WithModifiers(new OdinModifiers { Confidential = true }));
        var doc = builder.Build();

        var output = Core.Odin.Stringify(doc, new StringifyOptions { IncludeMetadata = false });
        Assert.Contains("*", output);
    }

    [Fact]
    public void Stringify_WithDeprecatedModifier()
    {
        var builder = Core.Odin.Builder();
        builder.Set("old", OdinValues.String("legacy").WithModifiers(new OdinModifiers { Deprecated = true }));
        var doc = builder.Build();

        var output = Core.Odin.Stringify(doc, new StringifyOptions { IncludeMetadata = false });
        Assert.Contains("-", output);
    }

    // ─────────────────────────────────────────────────────────────────
    // Stringify with Metadata
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Stringify_IncludesMetadata_WhenEnabled()
    {
        var builder = Core.Odin.Builder();
        builder.Metadata("odin", "1.0.0");
        builder.SetString("name", "test");
        var doc = builder.Build();

        var output = Core.Odin.Stringify(doc, new StringifyOptions { IncludeMetadata = true });
        Assert.Contains("{$}", output);
        Assert.Contains("odin = \"1.0.0\"", output);
    }

    [Fact]
    public void Stringify_ExcludesMetadata_WhenDisabled()
    {
        var builder = Core.Odin.Builder();
        builder.Metadata("odin", "1.0.0");
        builder.SetString("name", "test");
        var doc = builder.Build();

        var output = Core.Odin.Stringify(doc, new StringifyOptions { IncludeMetadata = false });
        Assert.DoesNotContain("{$}", output);
    }

    // ─────────────────────────────────────────────────────────────────
    // Stringify Various Types
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Stringify_CurrencyValue()
    {
        var builder = Core.Odin.Builder();
        builder.SetCurrency("price", 99.99, 2);
        var doc = builder.Build();

        var output = Core.Odin.Stringify(doc, new StringifyOptions { IncludeMetadata = false });
        Assert.Contains("#$", output);
        Assert.Contains("99.99", output);
    }

    [Fact]
    public void Stringify_NullValue()
    {
        var builder = Core.Odin.Builder();
        builder.SetNull("value");
        var doc = builder.Build();

        var output = Core.Odin.Stringify(doc, new StringifyOptions { IncludeMetadata = false });
        Assert.Contains("~", output);
    }

    [Fact]
    public void Stringify_ReferenceValue()
    {
        var builder = Core.Odin.Builder();
        builder.Set("ref", OdinValues.Reference("other.path"));
        var doc = builder.Build();

        var output = Core.Odin.Stringify(doc, new StringifyOptions { IncludeMetadata = false });
        Assert.Contains("@other.path", output);
    }

    // ─────────────────────────────────────────────────────────────────
    // Stringify with Section Headers
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Stringify_WithSections()
    {
        var builder = Core.Odin.Builder();
        builder.SetString("Customer.Name", "Alice");
        builder.SetInteger("Customer.Age", 30);
        var doc = builder.Build();

        var output = Core.Odin.Stringify(doc, new StringifyOptions { IncludeMetadata = false });
        Assert.Contains("{Customer}", output);
        Assert.Contains("Name = \"Alice\"", output);
    }
}
