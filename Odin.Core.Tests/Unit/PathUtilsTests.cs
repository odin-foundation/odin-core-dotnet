using Odin.Core.Utils;
using Xunit;

namespace Odin.Core.Tests.Unit;

public class PathUtilsTests
{
    // ─────────────────────────────────────────────────────────────────
    // BuildPath
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void BuildPath_SingleSegment()
    {
        Assert.Equal("name", PathUtils.BuildPath("name"));
    }

    [Fact]
    public void BuildPath_MultipleSegments()
    {
        Assert.Equal("Customer.Address.City", PathUtils.BuildPath("Customer", "Address", "City"));
    }

    [Fact]
    public void BuildPath_EmptySegments()
    {
        Assert.Equal("", PathUtils.BuildPath());
    }

    // ─────────────────────────────────────────────────────────────────
    // BuildPathWithIndices
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void BuildPathWithIndices_NoIndex()
    {
        var result = PathUtils.BuildPathWithIndices(("Customer", null), ("Name", null));
        Assert.Equal("Customer.Name", result);
    }

    [Fact]
    public void BuildPathWithIndices_WithIndex()
    {
        var result = PathUtils.BuildPathWithIndices(("items", 0), ("name", null));
        Assert.Equal("items[0].name", result);
    }

    [Fact]
    public void BuildPathWithIndices_MultipleIndices()
    {
        var result = PathUtils.BuildPathWithIndices(("matrix", 1), ("row", 2));
        Assert.Equal("matrix[1].row[2]", result);
    }

    // ─────────────────────────────────────────────────────────────────
    // SplitPath
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void SplitPath_SimpleSegments()
    {
        var segments = PathUtils.SplitPath("Customer.Address.City");
        Assert.Equal(new[] { "Customer", "Address", "City" }, segments);
    }

    [Fact]
    public void SplitPath_SingleSegment()
    {
        var segments = PathUtils.SplitPath("name");
        Assert.Single(segments);
        Assert.Equal("name", segments[0]);
    }

    [Fact]
    public void SplitPath_WithArrayIndex()
    {
        var segments = PathUtils.SplitPath("items[0].name");
        Assert.Equal(new[] { "items[0]", "name" }, segments);
    }

    [Fact]
    public void SplitPath_EmptyString()
    {
        var segments = PathUtils.SplitPath("");
        Assert.Empty(segments);
    }

    // ─────────────────────────────────────────────────────────────────
    // ParentPath
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ParentPath_MultiLevel()
    {
        Assert.Equal("Customer.Address", PathUtils.ParentPath("Customer.Address.City"));
    }

    [Fact]
    public void ParentPath_TwoLevel()
    {
        Assert.Equal("Customer", PathUtils.ParentPath("Customer.Name"));
    }

    [Fact]
    public void ParentPath_SingleSegment_ReturnsNull()
    {
        Assert.Null(PathUtils.ParentPath("name"));
    }

    // ─────────────────────────────────────────────────────────────────
    // LeafName
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void LeafName_MultiLevel()
    {
        Assert.Equal("City", PathUtils.LeafName("Customer.Address.City"));
    }

    [Fact]
    public void LeafName_SingleSegment()
    {
        Assert.Equal("name", PathUtils.LeafName("name"));
    }

    [Fact]
    public void LeafName_WithArrayIndex()
    {
        Assert.Equal("items[0]", PathUtils.LeafName("data.items[0]"));
    }

    // ─────────────────────────────────────────────────────────────────
    // StartsWith
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void StartsWith_ExactMatch()
    {
        Assert.True(PathUtils.StartsWith("Customer", "Customer"));
    }

    [Fact]
    public void StartsWith_DotSeparated()
    {
        Assert.True(PathUtils.StartsWith("Customer.Name", "Customer"));
    }

    [Fact]
    public void StartsWith_ArrayIndex()
    {
        Assert.True(PathUtils.StartsWith("items[0].name", "items"));
    }

    [Fact]
    public void StartsWith_FalseForPartialSegment()
    {
        // "Cust" is a prefix string-wise but not a path-segment prefix
        Assert.False(PathUtils.StartsWith("Customer.Name", "Cust"));
    }

    [Fact]
    public void StartsWith_FalseForDifferentPath()
    {
        Assert.False(PathUtils.StartsWith("Order.Id", "Customer"));
    }

    // ─────────────────────────────────────────────────────────────────
    // ParseSegment
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ParseSegment_PlainName()
    {
        var (name, index) = PathUtils.ParseSegment("items");
        Assert.Equal("items", name);
        Assert.Null(index);
    }

    [Fact]
    public void ParseSegment_WithIndex()
    {
        var (name, index) = PathUtils.ParseSegment("items[3]");
        Assert.Equal("items", name);
        Assert.Equal(3, index);
    }

    [Fact]
    public void ParseSegment_ZeroIndex()
    {
        var (name, index) = PathUtils.ParseSegment("arr[0]");
        Assert.Equal("arr", name);
        Assert.Equal(0, index);
    }
}
