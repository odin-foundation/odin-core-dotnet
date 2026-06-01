using Odin.Core;
using Odin.Core.Types;
using Xunit;

namespace Odin.Core.Tests.Unit;

public class ChainOverlayTests
{
    // ─────────────────────────────────────────────────────────────────
    // Replace
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Replace_LaterValueWins()
    {
        var doc = Core.Odin.CollapseChain("{p}\nv = \"1\"\n\n---\n\n{p}\nv = \"2\"");
        Assert.Equal("2", doc.GetString("p.v"));
    }

    [Fact]
    public void Replace_KeepsUntouchedPaths()
    {
        var doc = Core.Odin.CollapseChain(
            "{person}\nname = \"John\"\nage = ##30\ncity = \"Austin\"\n\n---\n\n{person}\nage = ##31\nstate = \"TX\"");
        Assert.Equal("John", doc.GetString("person.name"));
        Assert.Equal(31L, doc.GetInteger("person.age"));
        Assert.Equal("Austin", doc.GetString("person.city"));
        Assert.Equal("TX", doc.GetString("person.state"));
    }

    // ─────────────────────────────────────────────────────────────────
    // Null removal
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Null_RemovesField()
    {
        var doc = Core.Odin.CollapseChain(
            "{person}\nname = \"John\"\ntemporary = \"gone\"\n\n---\n\n{person}\ntemporary = ~");
        Assert.Equal("John", doc.GetString("person.name"));
        Assert.Null(doc.Get("person.temporary"));
    }

    [Fact]
    public void Null_RemovesSubtree()
    {
        var doc = Core.Odin.CollapseChain(
            "{p}\na.b = \"x\"\na.c = \"y\"\nkeep = \"z\"\n\n---\n\n{p}\na = ~");
        Assert.Equal("z", doc.GetString("p.keep"));
        Assert.Null(doc.Get("p.a.b"));
        Assert.Null(doc.Get("p.a.c"));
    }

    [Fact]
    public void Null_ReassignAfterRemoval()
    {
        var doc = Core.Odin.CollapseChain("{p}\nx = \"old\"\n\n---\n\n{p}\nx = ~\n\n---\n\n{p}\nx = \"new\"");
        Assert.Equal("new", doc.GetString("p.x"));
    }

    // ─────────────────────────────────────────────────────────────────
    // Array clear
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ArrayClear_RemovesAllElements()
    {
        var doc = Core.Odin.CollapseChain(
            "{p}\ntags[0] = \"x\"\ntags[1] = \"y\"\nkeep = \"z\"\n\n---\n\n{p}\ntags[] = ~");
        Assert.Equal("z", doc.GetString("p.keep"));
        Assert.Null(doc.Get("p.tags[0]"));
        Assert.Null(doc.Get("p.tags[1]"));
    }

    [Fact]
    public void ArrayClear_RepopulateAfterClear()
    {
        var doc = Core.Odin.CollapseChain(
            "{p}\ntags[0] = \"x\"\ntags[1] = \"y\"\n\n---\n\n{p}\ntags[] = ~\n\n---\n\n{p}\ntags[0] = \"new\"");
        Assert.Equal("new", doc.GetString("p.tags[0]"));
        Assert.Null(doc.Get("p.tags[1]"));
    }

    // ─────────────────────────────────────────────────────────────────
    // Metadata isolation
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Metadata_CarriesOnlyFinalDocument()
    {
        var doc = Core.Odin.CollapseChain(
            "{$}\nid = \"first\"\nrole = \"base\"\n\n{p}\nn = \"A\"\n\n---\n\n{$}\nid = \"second\"\n\n{p}\nn = \"B\"");
        Assert.Equal("B", doc.GetString("p.n"));
        Assert.Equal("second", doc.GetString("$.id"));
        Assert.Null(doc.Get("$.role"));
    }

    // ─────────────────────────────────────────────────────────────────
    // Multi-document chains
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void MultiDocument_ThreeChainResolvesToLast()
    {
        var doc = Core.Odin.CollapseChain(
            "{p}\nv = \"1\"\nstable = \"keep\"\n\n---\n\n{p}\nv = \"2\"\n\n---\n\n{p}\nv = \"3\"");
        Assert.Equal("3", doc.GetString("p.v"));
        Assert.Equal("keep", doc.GetString("p.stable"));
    }

    [Fact]
    public void SingleDocument_PassesThroughUnchanged()
    {
        var doc = Core.Odin.CollapseChain("{p}\nname = \"A\"\nage = ##5");
        Assert.Equal("A", doc.GetString("p.name"));
        Assert.Equal(5L, doc.GetInteger("p.age"));
    }

    [Fact]
    public void AcceptsPreParsedDocumentArray()
    {
        var docs = Core.Odin.ParseDocuments("{p}\nv = \"1\"\n\n---\n\n{p}\nv = \"2\"");
        var collapsed = Core.Odin.CollapseChain(docs);
        Assert.Equal("2", collapsed.GetString("p.v"));
    }
}
