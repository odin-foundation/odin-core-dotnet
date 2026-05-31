using System.Text;
using Odin.Core;
using Odin.Core.Types;
using Xunit;

namespace Odin.Core.Tests.Unit;

/// <summary>
/// Core-format conformance: top-level metadata assignments, integer decimal
/// rejection, metadata references, and document-chain parsing.
/// </summary>
public class CoreFormatConformanceTests
{
    // ─────────────────────────────────────────────────────────────────
    // Top-level $.path metadata assignment + canonical round-trip
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_TopLevelMetaAssignment_RoutesToMetadata()
    {
        var doc = Core.Odin.Parse("$.odin = \"1.0.0\"\n$.id = \"doc1\"\nname = \"Alice\"");

        Assert.Equal("1.0.0", doc.Metadata["odin"].AsString());
        Assert.Equal("doc1", doc.Metadata["id"].AsString());
        Assert.Equal("Alice", doc.GetString("name"));
        Assert.False(doc.Assignments.ContainsKey("$.id"));
    }

    [Fact]
    public void Canonicalize_RoundTrips_TopLevelMetadata()
    {
        var doc = Core.Odin.Parse("{$}\nodin = \"1.0.0\"\nid = \"doc1\"\n\n{}\nname = \"Alice\"\nage = ##30");

        var canonical = Encoding.UTF8.GetString(Core.Odin.Canonicalize(doc));
        var reparsed = Core.Odin.Parse(canonical);
        var canonical2 = Encoding.UTF8.GetString(Core.Odin.Canonicalize(reparsed));

        Assert.Equal(canonical, canonical2);
        Assert.Equal("1.0.0", reparsed.Metadata["odin"].AsString());
        Assert.Equal("doc1", reparsed.Metadata["id"].AsString());
        Assert.Equal("Alice", reparsed.GetString("name"));
        Assert.Equal(30L, reparsed.GetInteger("age"));
    }

    // ─────────────────────────────────────────────────────────────────
    // Integer decimal rejection
    // ─────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("x = ##4.2")]
    [InlineData("x = ##-3.7")]
    public void Parse_IntegerWithFraction_Throws(string input)
    {
        var ex = Assert.Throws<OdinParseException>(() => Core.Odin.Parse(input));
        Assert.Equal("P006", ex.Code);
    }

    [Fact]
    public void Parse_IntegerExponentForm_IsValid()
    {
        var doc = Core.Odin.Parse("x = ##1e3");
        Assert.True(doc.Get("x")!.IsInteger);
        Assert.Equal(1000L, doc.GetInteger("x"));
    }

    [Fact]
    public void Parse_PlainInteger_StillWorks()
    {
        var doc = Core.Odin.Parse("x = ##42");
        Assert.Equal(42L, doc.GetInteger("x"));
    }

    // ─────────────────────────────────────────────────────────────────
    // @$.path metadata reference
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_MetaReference_WithLeadingDot()
    {
        var doc = Core.Odin.Parse("x = @$.id");
        var reference = Assert.IsType<OdinReference>(doc.Get("x"));
        Assert.Equal("$.id", reference.Path);
    }

    [Fact]
    public void Parse_MetaReference_NestedPath()
    {
        var doc = Core.Odin.Parse("x = @$.i18n.en.name");
        var reference = Assert.IsType<OdinReference>(doc.Get("x"));
        Assert.Equal("$.i18n.en.name", reference.Path);
    }

    [Fact]
    public void Parse_ConstReference_StillWorks()
    {
        var doc = Core.Odin.Parse("x = @$const.NAME");
        var reference = Assert.IsType<OdinReference>(doc.Get("x"));
        Assert.Equal("$const.NAME", reference.Path);
    }

    // ─────────────────────────────────────────────────────────────────
    // Document chain API
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ParseDocuments_SingleDocument_YieldsOne()
    {
        var docs = Core.Odin.ParseDocuments("{$}\nid = \"a\"\n\n{}\nname = \"Alice\"");
        Assert.Single(docs);
        Assert.Equal("a", docs[0].Metadata["id"].AsString());
        Assert.Equal("Alice", docs[0].GetString("name"));
    }

    [Fact]
    public void ParseDocuments_Chain_YieldsAllWithIndependentMetadata()
    {
        var input =
            "{$}\nodin = \"1.0.0\"\nid = \"base\"\n\n{person}\nage = ##30\n\n" +
            "---\n\n" +
            "{$}\nodin = \"1.0.0\"\nid = \"overlay\"\n\n{person}\nage = ##31";

        var docs = Core.Odin.ParseDocuments(input);

        Assert.Equal(2, docs.Count);
        Assert.Equal("base", docs[0].Metadata["id"].AsString());
        Assert.Equal(30L, docs[0].GetInteger("person.age"));
        Assert.Equal("overlay", docs[1].Metadata["id"].AsString());
        Assert.Equal(31L, docs[1].GetInteger("person.age"));
    }
}
