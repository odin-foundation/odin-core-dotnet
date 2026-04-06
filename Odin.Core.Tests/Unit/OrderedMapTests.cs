using System.Collections.Generic;
using System.Linq;
using Odin.Core.Types;
using Xunit;

namespace Odin.Core.Tests.Unit;

public class OrderedMapTests
{
    [Fact]
    public void Set_And_Get_ByKey()
    {
        var map = new OrderedMap<string, int>();
        map.Set("a", 1);
        map.Set("b", 2);

        Assert.Equal(1, map["a"]);
        Assert.Equal(2, map["b"]);
    }

    [Fact]
    public void Set_OverwritesExistingKey_InPlace()
    {
        var map = new OrderedMap<string, int>();
        map.Set("a", 1);
        map.Set("b", 2);
        map.Set("a", 10);

        Assert.Equal(10, map["a"]);
        // Order preserved: "a" should still be at index 0
        Assert.Equal("a", map.GetAt(0).Key);
        Assert.Equal(10, map.GetAt(0).Value);
    }

    [Fact]
    public void InsertionOrder_IsPreserved()
    {
        var map = new OrderedMap<string, int>();
        map.Set("c", 3);
        map.Set("a", 1);
        map.Set("b", 2);

        var keys = map.Keys;
        Assert.Equal(new[] { "c", "a", "b" }, keys);
    }

    [Fact]
    public void ContainsKey_ReturnsTrueForExistingKey()
    {
        var map = new OrderedMap<string, int>();
        map.Set("x", 42);

        Assert.True(map.ContainsKey("x"));
        Assert.False(map.ContainsKey("y"));
    }

    [Fact]
    public void TryGetValue_ReturnsCorrectResult()
    {
        var map = new OrderedMap<string, string>();
        map.Set("key", "value");

        Assert.True(map.TryGetValue("key", out var val));
        Assert.Equal("value", val);

        Assert.False(map.TryGetValue("missing", out _));
    }

    [Fact]
    public void Remove_ExistingKey_ReturnsTrue()
    {
        var map = new OrderedMap<string, int>();
        map.Set("a", 1);
        map.Set("b", 2);
        map.Set("c", 3);

        Assert.True(map.Remove("b"));
        Assert.Equal(2, map.Count);
        Assert.False(map.ContainsKey("b"));
        // Remaining order
        Assert.Equal("a", map.GetAt(0).Key);
        Assert.Equal("c", map.GetAt(1).Key);
    }

    [Fact]
    public void Remove_NonexistentKey_ReturnsFalse()
    {
        var map = new OrderedMap<string, int>();
        map.Set("a", 1);

        Assert.False(map.Remove("z"));
        Assert.Equal(1, map.Count);
    }

    [Fact]
    public void Clear_RemovesAllEntries()
    {
        var map = new OrderedMap<string, int>();
        map.Set("a", 1);
        map.Set("b", 2);
        map.Clear();

        Assert.Equal(0, map.Count);
        Assert.False(map.ContainsKey("a"));
    }

    [Fact]
    public void Count_ReflectsNumberOfEntries()
    {
        var map = new OrderedMap<string, int>();
        Assert.Equal(0, map.Count);

        map.Set("a", 1);
        Assert.Equal(1, map.Count);

        map.Set("b", 2);
        Assert.Equal(2, map.Count);

        map.Set("a", 10); // overwrite
        Assert.Equal(2, map.Count);
    }

    [Fact]
    public void Clone_CreatesIndependentCopy()
    {
        var original = new OrderedMap<string, int>();
        original.Set("a", 1);
        original.Set("b", 2);

        var clone = original.Clone();
        clone.Set("c", 3);
        clone.Set("a", 99);

        Assert.Equal(2, original.Count);
        Assert.Equal(1, original["a"]);
        Assert.Equal(3, clone.Count);
        Assert.Equal(99, clone["a"]);
    }

    [Fact]
    public void Enumeration_YieldsEntriesInOrder()
    {
        var map = new OrderedMap<string, int>();
        map.Set("x", 10);
        map.Set("y", 20);
        map.Set("z", 30);

        var enumerated = map.ToList();
        Assert.Equal(3, enumerated.Count);
        Assert.Equal("x", enumerated[0].Key);
        Assert.Equal(10, enumerated[0].Value);
        Assert.Equal("y", enumerated[1].Key);
        Assert.Equal("z", enumerated[2].Key);
    }

    [Fact]
    public void Indexer_Get_ThrowsForMissingKey()
    {
        var map = new OrderedMap<string, int>();
        Assert.Throws<KeyNotFoundException>(() => map["missing"]);
    }

    [Fact]
    public void Indexer_Set_WorksLikeSetMethod()
    {
        var map = new OrderedMap<string, int>();
        map["key"] = 42;
        Assert.Equal(42, map["key"]);
    }

    [Fact]
    public void Values_ReturnsAllValuesInOrder()
    {
        var map = new OrderedMap<string, int>();
        map.Set("a", 10);
        map.Set("b", 20);

        var values = map.Values;
        Assert.Equal(new[] { 10, 20 }, values);
    }

    [Fact]
    public void Entries_ReturnsReadOnlyList()
    {
        var map = new OrderedMap<string, int>();
        map.Set("a", 1);

        var entries = map.Entries;
        Assert.Single(entries);
        Assert.Equal("a", entries[0].Key);
    }

    [Fact]
    public void Constructor_WithCapacity()
    {
        var map = new OrderedMap<string, int>(16);
        map.Set("a", 1);
        Assert.Equal(1, map.Count);
    }

    [Fact]
    public void Constructor_FromEntries()
    {
        var entries = new List<KeyValuePair<string, int>>
        {
            new("x", 1),
            new("y", 2),
        };
        var map = new OrderedMap<string, int>(entries);

        Assert.Equal(2, map.Count);
        Assert.Equal(1, map["x"]);
        Assert.Equal(2, map["y"]);
    }

    [Fact]
    public void GetAt_ReturnsEntryByIndex()
    {
        var map = new OrderedMap<string, int>();
        map.Set("first", 100);
        map.Set("second", 200);

        var entry = map.GetAt(1);
        Assert.Equal("second", entry.Key);
        Assert.Equal(200, entry.Value);
    }

    [Fact]
    public void Remove_FirstEntry_ShiftsIndicesCorrectly()
    {
        var map = new OrderedMap<string, int>();
        map.Set("a", 1);
        map.Set("b", 2);
        map.Set("c", 3);

        map.Remove("a");

        Assert.Equal(2, map.Count);
        Assert.Equal(2, map["b"]);
        Assert.Equal(3, map["c"]);
        Assert.Equal("b", map.GetAt(0).Key);
    }
}
