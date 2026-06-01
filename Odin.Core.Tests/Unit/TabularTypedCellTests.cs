using Odin.Core;
using Odin.Core.Types;
using Xunit;

namespace Odin.Core.Tests.Unit;

// Typed tabular cells keep their column in every position; a non-string cell
// must not drop the trailing column or shift the row.
public class TabularTypedCellTests
{
    // ─────────────────────────────────────────────────────────────────
    // Happy path
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void IntegerFirstColumn_KeepsTrailingColumn()
    {
        var doc = Core.Odin.Parse("{rows[] : qty, name}\n##5, \"widget\"\n##12, \"gadget\"");
        Assert.Equal(5L, doc.GetInteger("rows[0].qty"));
        Assert.Equal("widget", doc.GetString("rows[0].name"));
        Assert.Equal(12L, doc.GetInteger("rows[1].qty"));
        Assert.Equal("gadget", doc.GetString("rows[1].name"));
    }

    [Fact]
    public void MixedTypedOrder_KeepsEveryColumn()
    {
        var doc = Core.Odin.Parse("{items[] : qty, name, price}\n##10, \"Widget\", #$5.99\n##5, \"Gadget\", #$12.50");
        Assert.Equal(10L, doc.GetInteger("items[0].qty"));
        Assert.Equal("Widget", doc.GetString("items[0].name"));
        Assert.Equal(5.99, doc.Get("items[0].price")!.AsDouble());
        Assert.Equal(5L, doc.GetInteger("items[1].qty"));
        Assert.Equal("Gadget", doc.GetString("items[1].name"));
        Assert.Equal(12.50, doc.Get("items[1].price")!.AsDouble());
    }

    // ─────────────────────────────────────────────────────────────────
    // Edge cases
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void NegativeIntegerCell_KeepsSignAndColumn()
    {
        var doc = Core.Odin.Parse("{temps[] : label, value}\n\"low\", ##-5\n\"high\", ##42");
        Assert.Equal("low", doc.GetString("temps[0].label"));
        Assert.Equal(-5L, doc.GetInteger("temps[0].value"));
        Assert.Equal("high", doc.GetString("temps[1].label"));
        Assert.Equal(42L, doc.GetInteger("temps[1].value"));
    }

    [Fact]
    public void AllTypedRow_NoStringCell_KeepsEveryColumn()
    {
        var doc = Core.Odin.Parse("{points[] : x, y, z}\n##1, ##2, ##3\n##-4, ##5, ##-6");
        Assert.Equal(1L, doc.GetInteger("points[0].x"));
        Assert.Equal(2L, doc.GetInteger("points[0].y"));
        Assert.Equal(3L, doc.GetInteger("points[0].z"));
        Assert.Equal(-4L, doc.GetInteger("points[1].x"));
        Assert.Equal(5L, doc.GetInteger("points[1].y"));
        Assert.Equal(-6L, doc.GetInteger("points[1].z"));
    }

    [Fact]
    public void SingleNamedIntegerColumn_ProducesObjectArray()
    {
        var doc = Core.Odin.Parse("{counts[] : value}\n##42\n##0");
        Assert.Equal(42L, doc.GetInteger("counts[0].value"));
        Assert.Equal(0L, doc.GetInteger("counts[1].value"));
    }

    [Fact]
    public void LargeIntegerCell_RetainsFullPrecision()
    {
        var doc = Core.Odin.Parse("{big[] : label, n}\n\"max\", ##9007199254740991");
        Assert.Equal("max", doc.GetString("big[0].label"));
        Assert.Equal(9007199254740991L, doc.GetInteger("big[0].n"));
    }
}
