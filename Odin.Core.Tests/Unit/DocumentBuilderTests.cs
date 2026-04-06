using Odin.Core;
using Odin.Core.Types;
using Xunit;

namespace Odin.Core.Tests.Unit;

public class DocumentBuilderTests
{
    // ─────────────────────────────────────────────────────────────────
    // Empty Document
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Build_EmptyDocument()
    {
        var doc = Core.Odin.Builder().Build();
        Assert.Equal(0, doc.Assignments.Count);
        Assert.Equal(0, doc.Metadata.Count);
    }

    // ─────────────────────────────────────────────────────────────────
    // Metadata
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Build_WithStringMetadata()
    {
        var doc = Core.Odin.Builder()
            .Metadata("odin", "1.0.0")
            .Build();

        Assert.Equal("1.0.0", doc.GetString("$.odin"));
    }

    [Fact]
    public void Build_WithOdinValueMetadata()
    {
        var doc = Core.Odin.Builder()
            .Metadata("version", OdinValues.Integer(1))
            .Build();

        Assert.Equal(1L, doc.GetInteger("$.version"));
    }

    // ─────────────────────────────────────────────────────────────────
    // Set Various Types
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Build_SetString()
    {
        var doc = Core.Odin.Builder()
            .SetString("name", "Alice")
            .Build();

        Assert.Equal("Alice", doc.GetString("name"));
    }

    [Fact]
    public void Build_SetInteger()
    {
        var doc = Core.Odin.Builder()
            .SetInteger("age", 30)
            .Build();

        Assert.Equal(30L, doc.GetInteger("age"));
    }

    [Fact]
    public void Build_SetNumber()
    {
        var doc = Core.Odin.Builder()
            .SetNumber("pi", 3.14159)
            .Build();

        Assert.Equal(3.14159, doc.GetNumber("pi"));
    }

    [Fact]
    public void Build_SetBoolean()
    {
        var doc = Core.Odin.Builder()
            .SetBoolean("active", true)
            .Build();

        Assert.Equal(true, doc.GetBoolean("active"));
    }

    [Fact]
    public void Build_SetNull()
    {
        var doc = Core.Odin.Builder()
            .SetNull("value")
            .Build();

        Assert.True(doc.Get("value")!.IsNull);
    }

    [Fact]
    public void Build_SetCurrency()
    {
        var doc = Core.Odin.Builder()
            .SetCurrency("price", 49.99, 2)
            .Build();

        var val = doc.Get("price");
        Assert.NotNull(val);
        Assert.True(val!.IsCurrency);
        Assert.Equal(49.99m, val.AsDecimal());
    }

    [Fact]
    public void Build_SetCurrencyWithCode()
    {
        var doc = Core.Odin.Builder()
            .SetCurrency("amount", 100.00, 2, "USD")
            .Build();

        var val = doc.Get("amount") as OdinCurrency;
        Assert.NotNull(val);
        Assert.Equal("USD", val!.CurrencyCode);
    }

    [Fact]
    public void Build_SetOdinValue_Directly()
    {
        var doc = Core.Odin.Builder()
            .Set("ref", OdinValues.Reference("other.path"))
            .Build();

        Assert.True(doc.Get("ref")!.IsReference);
        Assert.Equal("other.path", doc.Get("ref")!.AsReference());
    }

    // ─────────────────────────────────────────────────────────────────
    // WithModifiers
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Build_WithModifiers()
    {
        var doc = Core.Odin.Builder()
            .SetString("ssn", "123-45-6789")
            .WithModifiers("ssn", new OdinModifiers { Confidential = true })
            .Build();

        Assert.True(doc.PathModifiers.ContainsKey("ssn"));
        Assert.True(doc.PathModifiers["ssn"].Confidential);
    }

    [Fact]
    public void Build_WithMultipleModifiers()
    {
        var doc = Core.Odin.Builder()
            .SetString("field", "value")
            .WithModifiers("field", new OdinModifiers { Required = true, Deprecated = true })
            .Build();

        var mods = doc.PathModifiers["field"];
        Assert.True(mods.Required);
        Assert.True(mods.Deprecated);
        Assert.False(mods.Confidential);
    }

    // ─────────────────────────────────────────────────────────────────
    // Imports, Schemas, Comments
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Build_WithImport()
    {
        var doc = Core.Odin.Builder()
            .AddImport("./common.odin", "common")
            .SetString("name", "test")
            .Build();

        Assert.Single(doc.Imports);
        Assert.Equal("./common.odin", doc.Imports[0].Path);
        Assert.Equal("common", doc.Imports[0].Alias);
    }

    [Fact]
    public void Build_WithSchema()
    {
        var doc = Core.Odin.Builder()
            .AddSchema("https://example.com/schema.odin")
            .Build();

        Assert.Single(doc.Schemas);
        Assert.Equal("https://example.com/schema.odin", doc.Schemas[0].Url);
    }

    [Fact]
    public void Build_WithComment()
    {
        var doc = Core.Odin.Builder()
            .AddComment("This is a comment", "name")
            .SetString("name", "test")
            .Build();

        Assert.Single(doc.Comments);
        Assert.Equal("This is a comment", doc.Comments[0].Text);
        Assert.Equal("name", doc.Comments[0].AssociatedPath);
    }

    // ─────────────────────────────────────────────────────────────────
    // Fluent Chaining
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Build_FluentChaining()
    {
        var doc = Core.Odin.Builder()
            .Metadata("odin", "1.0.0")
            .SetString("Customer.Name", "Alice")
            .SetInteger("Customer.Age", 30)
            .SetBoolean("Customer.Active", true)
            .SetNull("Customer.MiddleName")
            .Build();

        Assert.Equal("1.0.0", doc.GetString("$.odin"));
        Assert.Equal("Alice", doc.GetString("Customer.Name"));
        Assert.Equal(30L, doc.GetInteger("Customer.Age"));
        Assert.Equal(true, doc.GetBoolean("Customer.Active"));
        Assert.True(doc.Get("Customer.MiddleName")!.IsNull);
    }

    // ─────────────────────────────────────────────────────────────────
    // Build Round-Trip
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Build_RoundTrip_ThroughStringify()
    {
        var original = Core.Odin.Builder()
            .SetString("name", "Alice")
            .SetInteger("age", 30)
            .SetBoolean("active", true)
            .Build();

        var text = Core.Odin.Stringify(original, new StringifyOptions { IncludeMetadata = false });
        var reparsed = Core.Odin.Parse(text);

        Assert.Equal("Alice", reparsed.GetString("name"));
        Assert.Equal(30L, reparsed.GetInteger("age"));
        Assert.Equal(true, reparsed.GetBoolean("active"));
    }

    [Fact]
    public void Build_OverwriteExistingPath()
    {
        var doc = Core.Odin.Builder()
            .SetString("name", "Alice")
            .SetString("name", "Bob")
            .Build();

        Assert.Equal("Bob", doc.GetString("name"));
    }

    // ─────────────────────────────────────────────────────────────────
    // Document Operations
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Document_Paths_ReturnsAllKeys()
    {
        var doc = Core.Odin.Builder()
            .SetString("a", "1")
            .SetString("b", "2")
            .SetString("c", "3")
            .Build();

        var paths = doc.Paths();
        Assert.Equal(3, paths.Count);
        Assert.Contains("a", paths);
        Assert.Contains("b", paths);
        Assert.Contains("c", paths);
    }

    [Fact]
    public void Document_Flatten()
    {
        var doc = Core.Odin.Builder()
            .SetString("name", "Alice")
            .SetInteger("age", 30)
            .Build();

        var flat = doc.Flatten();
        Assert.Equal("Alice", flat["name"]);
        Assert.Equal("30", flat["age"]);
    }

    [Fact]
    public void Document_Empty_HasNoAssignments()
    {
        var doc = OdinDocument.Empty();
        Assert.Equal(0, doc.Assignments.Count);
        Assert.Equal(0, doc.Metadata.Count);
    }
}
