using System;
using System.Collections.Generic;
using Odin.Core.Types;
using Odin.Core.Transform;
using Odin.Core.Transform.Verbs;
using Xunit;

namespace Odin.Core.Tests.Unit;

/// <summary>
/// Tests for collection, array, object, aggregation, and statistical verbs.
/// Ported from Rust SDK collection_verbs.rs tests/extended_tests/extended_tests_2.
/// </summary>
public class CollectionVerbTests
{
    private readonly VerbRegistry _registry = new VerbRegistry();
    private readonly VerbContext _ctx = new VerbContext();

    private DynValue Invoke(string verb, params DynValue[] args)
        => _registry.Invoke(verb, args, _ctx);

    // Shorthand helpers (matching Rust test helpers)
    private static DynValue S(string v) => DynValue.String(v);
    private static DynValue I(long v) => DynValue.Integer(v);
    private static DynValue F(double v) => DynValue.Float(v);
    private static DynValue B(bool v) => DynValue.Bool(v);
    private static DynValue Null() => DynValue.Null();
    private static DynValue Arr(params DynValue[] items) => DynValue.Array(new List<DynValue>(items));
    private static DynValue Obj(params (string key, DynValue value)[] pairs)
    {
        var list = new List<KeyValuePair<string, DynValue>>();
        foreach (var (k, v) in pairs) list.Add(new KeyValuePair<string, DynValue>(k, v));
        return DynValue.Object(list);
    }

    // =========================================================================
    // flatten
    // =========================================================================

    [Fact]
    public void Flatten_Nested()
    {
        var data = Arr(Arr(I(1), I(2)), Arr(I(3)));
        var result = Invoke("flatten", data);
        Assert.Equal(Arr(I(1), I(2), I(3)).AsArray()!.Count, result.AsArray()!.Count);
        Assert.Equal(1, result.AsArray()![0].AsInt64());
        Assert.Equal(3, result.AsArray()![2].AsInt64());
    }

    [Fact]
    public void Flatten_Mixed()
    {
        var data = Arr(I(1), Arr(I(2), I(3)), I(4));
        var result = Invoke("flatten", data);
        Assert.Equal(4, result.AsArray()!.Count);
    }

    [Fact]
    public void Flatten_Empty()
    {
        var result = Invoke("flatten", Arr());
        Assert.Empty(result.AsArray()!);
    }

    [Fact]
    public void Flatten_SingleLevelOnly()
    {
        var data = Arr(Arr(Arr(I(1))));
        var result = Invoke("flatten", data);
        // Only flattens one level
        Assert.Single(result.AsArray()!);
        Assert.NotNull(result.AsArray()![0].AsArray());
    }

    [Fact]
    public void Flatten_AllScalars()
    {
        var data = Arr(I(1), I(2), I(3));
        var result = Invoke("flatten", data);
        Assert.Equal(3, result.AsArray()!.Count);
    }

    [Fact]
    public void Flatten_WithNulls()
    {
        var data = Arr(Null(), Arr(I(1)));
        var result = Invoke("flatten", data);
        Assert.Equal(2, result.AsArray()!.Count);
        Assert.True(result.AsArray()![0].IsNull);
    }

    // =========================================================================
    // distinct / unique
    // =========================================================================

    [Fact]
    public void Distinct_RemovesDuplicates()
    {
        var data = Arr(I(1), I(2), I(1), I(3), I(2));
        var result = Invoke("distinct", data);
        Assert.Equal(3, result.AsArray()!.Count);
    }

    [Fact]
    public void Distinct_Empty()
    {
        Assert.Empty(Invoke("distinct", Arr()).AsArray()!);
    }

    [Fact]
    public void Unique_IsDistinctAlias()
    {
        var data = Arr(S("a"), S("b"), S("a"));
        var r1 = Invoke("unique", data);
        Assert.Equal(2, r1.AsArray()!.Count);
    }

    [Fact]
    public void Distinct_SingleElement()
    {
        Assert.Single(Invoke("distinct", Arr(I(42))).AsArray()!);
    }

    [Fact]
    public void Distinct_AllSame()
    {
        var data = Arr(S("a"), S("a"), S("a"));
        Assert.Single(Invoke("distinct", data).AsArray()!);
    }

    [Fact]
    public void Distinct_PreservesOrder()
    {
        var data = Arr(I(3), I(1), I(2), I(1), I(3));
        var result = Invoke("distinct", data);
        Assert.Equal(3, result.AsArray()![0].AsInt64());
        Assert.Equal(1, result.AsArray()![1].AsInt64());
        Assert.Equal(2, result.AsArray()![2].AsInt64());
    }

    [Fact]
    public void Distinct_WithNulls()
    {
        var data = Arr(Null(), I(1), Null(), I(2));
        var result = Invoke("distinct", data);
        Assert.Equal(3, result.AsArray()!.Count);
    }

    // =========================================================================
    // sort
    // =========================================================================

    [Fact]
    public void Sort_Integers()
    {
        var data = Arr(I(3), I(1), I(2));
        var result = Invoke("sort", data);
        Assert.Equal(1, result.AsArray()![0].AsInt64());
        Assert.Equal(2, result.AsArray()![1].AsInt64());
        Assert.Equal(3, result.AsArray()![2].AsInt64());
    }

    [Fact]
    public void Sort_Strings()
    {
        var data = Arr(S("banana"), S("apple"), S("cherry"));
        var result = Invoke("sort", data);
        Assert.Equal("apple", result.AsArray()![0].AsString());
        Assert.Equal("banana", result.AsArray()![1].AsString());
        Assert.Equal("cherry", result.AsArray()![2].AsString());
    }

    [Fact]
    public void Sort_Empty()
    {
        Assert.Empty(Invoke("sort", Arr()).AsArray()!);
    }

    [Fact]
    public void Sort_Single()
    {
        var result = Invoke("sort", Arr(I(42)));
        Assert.Equal(42, result.AsArray()![0].AsInt64());
    }

    [Fact]
    public void Sort_Floats()
    {
        var data = Arr(F(3.1), F(1.5), F(2.7));
        var result = Invoke("sort", data);
        Assert.Equal(1.5, result.AsArray()![0].AsDouble());
        Assert.Equal(2.7, result.AsArray()![1].AsDouble());
        Assert.Equal(3.1, result.AsArray()![2].AsDouble());
    }

    [Fact]
    public void Sort_AlreadySorted()
    {
        var data = Arr(I(1), I(2), I(3));
        var result = Invoke("sort", data);
        Assert.Equal(1, result.AsArray()![0].AsInt64());
        Assert.Equal(3, result.AsArray()![2].AsInt64());
    }

    [Fact]
    public void Sort_ReverseOrder()
    {
        var data = Arr(I(3), I(2), I(1));
        var result = Invoke("sort", data);
        Assert.Equal(1, result.AsArray()![0].AsInt64());
    }

    [Fact]
    public void Sort_WithDuplicates()
    {
        var data = Arr(I(2), I(1), I(2), I(1));
        var result = Invoke("sort", data);
        Assert.Equal(1, result.AsArray()![0].AsInt64());
        Assert.Equal(1, result.AsArray()![1].AsInt64());
        Assert.Equal(2, result.AsArray()![2].AsInt64());
        Assert.Equal(2, result.AsArray()![3].AsInt64());
    }

    // =========================================================================
    // sortDesc
    // =========================================================================

    [Fact]
    public void SortDesc_Integers()
    {
        var data = Arr(I(1), I(3), I(2));
        var result = Invoke("sortDesc", data);
        Assert.Equal(3, result.AsArray()![0].AsInt64());
        Assert.Equal(2, result.AsArray()![1].AsInt64());
        Assert.Equal(1, result.AsArray()![2].AsInt64());
    }

    [Fact]
    public void SortDesc_Empty()
    {
        Assert.Empty(Invoke("sortDesc", Arr()).AsArray()!);
    }

    [Fact]
    public void SortDesc_Strings()
    {
        var data = Arr(S("a"), S("c"), S("b"));
        var result = Invoke("sortDesc", data);
        Assert.Equal("c", result.AsArray()![0].AsString());
        Assert.Equal("b", result.AsArray()![1].AsString());
        Assert.Equal("a", result.AsArray()![2].AsString());
    }

    [Fact]
    public void SortDesc_Floats()
    {
        var data = Arr(F(1.1), F(3.3), F(2.2));
        var result = Invoke("sortDesc", data);
        Assert.Equal(3.3, result.AsArray()![0].AsDouble());
        Assert.Equal(2.2, result.AsArray()![1].AsDouble());
        Assert.Equal(1.1, result.AsArray()![2].AsDouble());
    }

    [Fact]
    public void SortDesc_Single()
    {
        var result = Invoke("sortDesc", Arr(I(5)));
        Assert.Equal(5, result.AsArray()![0].AsInt64());
    }

    // =========================================================================
    // sortBy
    // =========================================================================

    [Fact]
    public void SortBy_Field()
    {
        var data = Arr(
            Obj(("n", I(3))),
            Obj(("n", I(1))),
            Obj(("n", I(2)))
        );
        var result = Invoke("sortBy", data, S("n"));
        Assert.Equal(1, result.AsArray()![0].Get("n")!.AsInt64());
        Assert.Equal(3, result.AsArray()![2].Get("n")!.AsInt64());
    }

    [Fact]
    public void SortBy_StringField()
    {
        var data = Arr(
            Obj(("name", S("Charlie"))),
            Obj(("name", S("Alice"))),
            Obj(("name", S("Bob")))
        );
        var result = Invoke("sortBy", data, S("name"));
        Assert.Equal("Alice", result.AsArray()![0].Get("name")!.AsString());
        Assert.Equal("Bob", result.AsArray()![1].Get("name")!.AsString());
        Assert.Equal("Charlie", result.AsArray()![2].Get("name")!.AsString());
    }

    [Fact]
    public void SortBy_EmptyArray()
    {
        var result = Invoke("sortBy", Arr(), S("x"));
        Assert.Empty(result.AsArray()!);
    }

    [Fact]
    public void SortBy_MissingField()
    {
        var data = Arr(Obj(("a", I(2))), Obj(("b", I(1))));
        var result = Invoke("sortBy", data, S("a"));
        Assert.Equal(2, result.AsArray()!.Count);
    }

    // =========================================================================
    // map / pluck
    // =========================================================================

    [Fact]
    public void Map_ExtractsField()
    {
        var data = Arr(Obj(("name", S("Alice"))), Obj(("name", S("Bob"))));
        var result = Invoke("map", data, S("name"));
        Assert.Equal("Alice", result.AsArray()![0].AsString());
        Assert.Equal("Bob", result.AsArray()![1].AsString());
    }

    [Fact]
    public void Map_MissingFieldGivesNull()
    {
        var data = Arr(Obj(("a", I(1))));
        var result = Invoke("map", data, S("b"));
        Assert.True(result.AsArray()![0].IsNull);
    }

    [Fact]
    public void Pluck_IsMapAlias()
    {
        var data = Arr(Obj(("x", I(1))), Obj(("x", I(2))));
        var result = Invoke("pluck", data, S("x"));
        Assert.Equal(1, result.AsArray()![0].AsInt64());
        Assert.Equal(2, result.AsArray()![1].AsInt64());
    }

    [Fact]
    public void Map_EmptyArray()
    {
        Assert.Empty(Invoke("map", Arr(), S("x")).AsArray()!);
    }

    [Fact]
    public void Map_AllMissingFields()
    {
        var data = Arr(Obj(("a", I(1))), Obj(("a", I(2))));
        var result = Invoke("map", data, S("z"));
        Assert.True(result.AsArray()![0].IsNull);
        Assert.True(result.AsArray()![1].IsNull);
    }

    [Fact]
    public void Pluck_EmptyArray()
    {
        Assert.Empty(Invoke("pluck", Arr(), S("x")).AsArray()!);
    }

    // =========================================================================
    // indexOf
    // =========================================================================

    [Fact]
    public void IndexOf_Found()
    {
        var data = Arr(S("a"), S("b"), S("c"));
        Assert.Equal(1, Invoke("indexOf", data, S("b")).AsInt64());
    }

    [Fact]
    public void IndexOf_NotFound()
    {
        var data = Arr(I(1), I(2));
        Assert.Equal(-1, Invoke("indexOf", data, I(99)).AsInt64());
    }

    [Fact]
    public void IndexOf_FirstOccurrence()
    {
        var data = Arr(I(1), I(2), I(1));
        Assert.Equal(0, Invoke("indexOf", data, I(1)).AsInt64());
    }

    [Fact]
    public void IndexOf_EmptyArray()
    {
        Assert.Equal(-1, Invoke("indexOf", Arr(), I(1)).AsInt64());
    }

    [Fact]
    public void IndexOf_StringValue()
    {
        var data = Arr(S("hello"), S("world"));
        Assert.Equal(1, Invoke("indexOf", data, S("world")).AsInt64());
    }

    // =========================================================================
    // at
    // =========================================================================

    [Fact]
    public void At_ValidIndex()
    {
        var data = Arr(S("a"), S("b"), S("c"));
        Assert.Equal("b", Invoke("at", data, I(1)).AsString());
    }

    [Fact]
    public void At_OutOfBounds()
    {
        var data = Arr(I(1));
        Assert.True(Invoke("at", data, I(5)).IsNull);
    }

    [Fact]
    public void At_FirstElement()
    {
        var data = Arr(S("first"), S("second"));
        Assert.Equal("first", Invoke("at", data, I(0)).AsString());
    }

    [Fact]
    public void At_LastElement()
    {
        var data = Arr(I(1), I(2), I(3));
        Assert.Equal(3, Invoke("at", data, I(2)).AsInt64());
    }

    [Fact]
    public void At_EmptyArray()
    {
        Assert.True(Invoke("at", Arr(), I(0)).IsNull);
    }

    [Fact]
    public void At_NegativeIndex()
    {
        var data = Arr(I(10), I(20), I(30));
        Assert.Equal(30, Invoke("at", data, I(-1)).AsInt64());
    }

    // =========================================================================
    // slice
    // =========================================================================

    [Fact]
    public void Slice_Middle()
    {
        var data = Arr(I(10), I(20), I(30), I(40), I(50));
        var result = Invoke("slice", data, I(1), I(4));
        Assert.Equal(3, result.AsArray()!.Count);
        Assert.Equal(20, result.AsArray()![0].AsInt64());
        Assert.Equal(40, result.AsArray()![2].AsInt64());
    }

    [Fact]
    public void Slice_EmptyRange()
    {
        var data = Arr(I(1), I(2));
        Assert.Empty(Invoke("slice", data, I(1), I(1)).AsArray()!);
    }

    [Fact]
    public void Slice_ClampsEnd()
    {
        var data = Arr(I(1), I(2));
        var result = Invoke("slice", data, I(0), I(100));
        Assert.Equal(2, result.AsArray()!.Count);
    }

    [Fact]
    public void Slice_FullArray()
    {
        var data = Arr(I(1), I(2), I(3));
        var result = Invoke("slice", data, I(0), I(3));
        Assert.Equal(3, result.AsArray()!.Count);
    }

    [Fact]
    public void Slice_EmptyArray()
    {
        Assert.Empty(Invoke("slice", Arr(), I(0), I(0)).AsArray()!);
    }

    [Fact]
    public void Slice_SingleElement()
    {
        var data = Arr(I(10), I(20), I(30));
        var result = Invoke("slice", data, I(1), I(2));
        Assert.Single(result.AsArray()!);
        Assert.Equal(20, result.AsArray()![0].AsInt64());
    }

    [Fact]
    public void Slice_StartPastLength()
    {
        var data = Arr(I(1));
        Assert.Empty(Invoke("slice", data, I(5), I(10)).AsArray()!);
    }

    // =========================================================================
    // reverse
    // =========================================================================

    [Fact]
    public void Reverse_Array()
    {
        var data = Arr(I(1), I(2), I(3));
        var result = Invoke("reverse", data);
        Assert.Equal(3, result.AsArray()![0].AsInt64());
        Assert.Equal(1, result.AsArray()![2].AsInt64());
    }

    [Fact]
    public void Reverse_Empty()
    {
        Assert.Empty(Invoke("reverse", Arr()).AsArray()!);
    }

    [Fact]
    public void Reverse_Single()
    {
        var result = Invoke("reverse", Arr(I(1)));
        Assert.Equal(1, result.AsArray()![0].AsInt64());
    }

    [Fact]
    public void Reverse_Strings()
    {
        var data = Arr(S("a"), S("b"), S("c"));
        var result = Invoke("reverse", data);
        Assert.Equal("c", result.AsArray()![0].AsString());
        Assert.Equal("a", result.AsArray()![2].AsString());
    }

    [Fact]
    public void Reverse_TwoElements()
    {
        var data = Arr(I(1), I(2));
        var result = Invoke("reverse", data);
        Assert.Equal(2, result.AsArray()![0].AsInt64());
        Assert.Equal(1, result.AsArray()![1].AsInt64());
    }

    // =========================================================================
    // every
    // =========================================================================

    [Fact]
    public void Every_AllTruthy()
    {
        var data = Arr(I(1), I(2), I(3));
        Assert.True(Invoke("every", data).AsBool());
    }

    [Fact]
    public void Every_NotAll()
    {
        var data = Arr(I(1), I(0), I(3));
        Assert.False(Invoke("every", data).AsBool());
    }

    [Fact]
    public void Every_EmptyIsTrue()
    {
        Assert.True(Invoke("every", Arr()).AsBool());
    }

    [Fact]
    public void Every_WithFieldName()
    {
        var data = Arr(Obj(("v", I(10))), Obj(("v", I(20))));
        Assert.True(Invoke("every", data, S("v")).AsBool());
    }

    [Fact]
    public void Every_WithFieldName_NotAll()
    {
        var data = Arr(Obj(("v", I(0))), Obj(("v", I(10))));
        Assert.False(Invoke("every", data, S("v")).AsBool());
    }

    // =========================================================================
    // some
    // =========================================================================

    [Fact]
    public void Some_OneMatches()
    {
        var data = Arr(I(0), I(0), I(1));
        Assert.True(Invoke("some", data).AsBool());
    }

    [Fact]
    public void Some_NoneMatch()
    {
        var data = Arr(I(0), Null(), S(""));
        Assert.False(Invoke("some", data).AsBool());
    }

    [Fact]
    public void Some_EmptyIsFalse()
    {
        Assert.False(Invoke("some", Arr()).AsBool());
    }

    [Fact]
    public void Some_WithFieldName()
    {
        var data = Arr(Obj(("v", I(0))), Obj(("v", I(10))));
        Assert.True(Invoke("some", data, S("v")).AsBool());
    }

    // =========================================================================
    // find
    // =========================================================================

    [Fact]
    public void Find_FirstTruthy()
    {
        var data = Arr(Null(), I(0), S("found"), I(99));
        var result = Invoke("find", data);
        Assert.Equal("found", result.AsString());
    }

    [Fact]
    public void Find_NoMatchReturnsNull()
    {
        var data = Arr(Null(), I(0), S(""));
        Assert.True(Invoke("find", data).IsNull);
    }

    [Fact]
    public void Find_WithFieldName()
    {
        var data = Arr(
            Obj(("n", S("a")), ("v", I(0))),
            Obj(("n", S("b")), ("v", I(1)))
        );
        var result = Invoke("find", data, S("v"));
        Assert.Equal("b", result.Get("n")!.AsString());
    }

    [Fact]
    public void Find_EmptyArray()
    {
        Assert.True(Invoke("find", Arr()).IsNull);
    }

    // =========================================================================
    // findIndex
    // =========================================================================

    [Fact]
    public void FindIndex_Found()
    {
        var data = Arr(Null(), I(0), I(1));
        Assert.Equal(2, Invoke("findIndex", data).AsInt64());
    }

    [Fact]
    public void FindIndex_NotFound()
    {
        var data = Arr(Null(), I(0));
        Assert.Equal(-1, Invoke("findIndex", data).AsInt64());
    }

    [Fact]
    public void FindIndex_EmptyArray()
    {
        Assert.Equal(-1, Invoke("findIndex", Arr()).AsInt64());
    }

    // =========================================================================
    // includes
    // =========================================================================

    [Fact]
    public void Includes_Present()
    {
        var data = Arr(I(1), I(2), I(3));
        Assert.True(Invoke("includes", data, I(2)).AsBool());
    }

    [Fact]
    public void Includes_Absent()
    {
        var data = Arr(I(1), I(2));
        Assert.False(Invoke("includes", data, I(99)).AsBool());
    }

    [Fact]
    public void Includes_Empty()
    {
        Assert.False(Invoke("includes", Arr(), I(1)).AsBool());
    }

    [Fact]
    public void Includes_StringValue()
    {
        var data = Arr(S("hello"), S("world"));
        Assert.True(Invoke("includes", data, S("hello")).AsBool());
    }

    [Fact]
    public void Includes_BoolValue()
    {
        var data = Arr(B(true), B(false));
        Assert.True(Invoke("includes", data, B(true)).AsBool());
    }

    // =========================================================================
    // concatArrays
    // =========================================================================

    [Fact]
    public void ConcatArrays_TwoArrays()
    {
        var a = Arr(I(1), I(2));
        var b = Arr(I(3), I(4));
        var result = Invoke("concatArrays", a, b);
        Assert.Equal(4, result.AsArray()!.Count);
        Assert.Equal(1, result.AsArray()![0].AsInt64());
        Assert.Equal(4, result.AsArray()![3].AsInt64());
    }

    [Fact]
    public void ConcatArrays_WithEmpty()
    {
        var a = Arr(I(1));
        var result = Invoke("concatArrays", a, Arr());
        Assert.Single(result.AsArray()!);
    }

    [Fact]
    public void ConcatArrays_BothEmpty()
    {
        Assert.Empty(Invoke("concatArrays", Arr(), Arr()).AsArray()!);
    }

    [Fact]
    public void ConcatArrays_FirstEmpty()
    {
        var result = Invoke("concatArrays", Arr(), Arr(I(1)));
        Assert.Single(result.AsArray()!);
    }

    [Fact]
    public void ConcatArrays_MixedTypes()
    {
        var a = Arr(I(1), S("two"));
        var b = Arr(B(true), Null());
        Assert.Equal(4, Invoke("concatArrays", a, b).AsArray()!.Count);
    }

    // =========================================================================
    // zip
    // =========================================================================

    [Fact]
    public void Zip_EqualLength()
    {
        var a = Arr(I(1), I(2));
        var b = Arr(S("a"), S("b"));
        var result = Invoke("zip", a, b);
        Assert.Equal(2, result.AsArray()!.Count);
        var first = result.AsArray()![0].AsArray()!;
        Assert.Equal(1, first[0].AsInt64());
        Assert.Equal("a", first[1].AsString());
    }

    [Fact]
    public void Zip_UnequalPadsWithNull()
    {
        var a = Arr(I(1), I(2), I(3));
        var b = Arr(S("a"));
        var result = Invoke("zip", a, b);
        // .NET implementation pads with null for shorter arrays
        Assert.Equal(3, result.AsArray()!.Count);
    }

    [Fact]
    public void Zip_BothEmpty()
    {
        Assert.Empty(Invoke("zip", Arr(), Arr()).AsArray()!);
    }

    [Fact]
    public void Zip_SingleElements()
    {
        var result = Invoke("zip", Arr(I(1)), Arr(S("a")));
        Assert.Single(result.AsArray()!);
        var pair = result.AsArray()![0].AsArray()!;
        Assert.Equal(1, pair[0].AsInt64());
        Assert.Equal("a", pair[1].AsString());
    }

    // =========================================================================
    // groupBy
    // =========================================================================

    [Fact]
    public void GroupBy_Field()
    {
        var data = Arr(
            Obj(("color", S("red")), ("n", I(1))),
            Obj(("color", S("blue")), ("n", I(2))),
            Obj(("color", S("red")), ("n", I(3)))
        );
        var result = Invoke("groupBy", data, S("color"));
        var obj = result.AsObject()!;
        Assert.Equal(2, obj.Count);
        Assert.Equal("red", obj[0].Key);
        Assert.Equal("blue", obj[1].Key);
    }

    [Fact]
    public void GroupBy_EmptyArray()
    {
        var result = Invoke("groupBy", Arr(), S("key"));
        Assert.Empty(result.AsObject()!);
    }

    [Fact]
    public void GroupBy_SingleGroup()
    {
        var data = Arr(
            Obj(("type", S("A")), ("v", I(1))),
            Obj(("type", S("A")), ("v", I(2)))
        );
        var result = Invoke("groupBy", data, S("type"));
        Assert.Single(result.AsObject()!);
    }

    [Fact]
    public void GroupBy_MissingFieldUsesNullKey()
    {
        var data = Arr(
            Obj(("v", I(1))),
            Obj(("type", S("A")), ("v", I(2)))
        );
        var result = Invoke("groupBy", data, S("type"));
        Assert.Equal(2, result.AsObject()!.Count);
    }

    [Fact]
    public void GroupBy_IntegerField()
    {
        var data = Arr(
            Obj(("score", I(10))),
            Obj(("score", I(20))),
            Obj(("score", I(10)))
        );
        var result = Invoke("groupBy", data, S("score"));
        Assert.Equal(2, result.AsObject()!.Count);
    }

    // =========================================================================
    // partition
    // =========================================================================

    [Fact]
    public void Partition_Splits()
    {
        var data = Arr(I(1), I(0), I(3), Null());
        var result = Invoke("partition", data);
        var parts = result.AsArray()!;
        Assert.Equal(2, parts.Count);
        Assert.Equal(2, parts[0].AsArray()!.Count); // truthy: 1, 3
        Assert.Equal(2, parts[1].AsArray()!.Count); // falsy: 0, null
    }

    [Fact]
    public void Partition_AllTruthy()
    {
        var data = Arr(I(1), I(2));
        var result = Invoke("partition", data);
        Assert.Equal(2, result.AsArray()![0].AsArray()!.Count);
        Assert.Empty(result.AsArray()![1].AsArray()!);
    }

    [Fact]
    public void Partition_AllFalsy()
    {
        var data = Arr(I(0), Null());
        var result = Invoke("partition", data);
        Assert.Empty(result.AsArray()![0].AsArray()!);
        Assert.Equal(2, result.AsArray()![1].AsArray()!.Count);
    }

    [Fact]
    public void Partition_Empty()
    {
        var result = Invoke("partition", Arr());
        Assert.Empty(result.AsArray()![0].AsArray()!);
        Assert.Empty(result.AsArray()![1].AsArray()!);
    }

    // =========================================================================
    // take
    // =========================================================================

    [Fact]
    public void Take_FirstN()
    {
        var data = Arr(I(1), I(2), I(3), I(4));
        var result = Invoke("take", data, I(2));
        Assert.Equal(2, result.AsArray()!.Count);
        Assert.Equal(1, result.AsArray()![0].AsInt64());
        Assert.Equal(2, result.AsArray()![1].AsInt64());
    }

    [Fact]
    public void Take_MoreThanLength()
    {
        var data = Arr(I(1));
        var result = Invoke("take", data, I(100));
        Assert.Single(result.AsArray()!);
    }

    [Fact]
    public void Take_Zero()
    {
        Assert.Empty(Invoke("take", Arr(I(1), I(2), I(3)), I(0)).AsArray()!);
    }

    [Fact]
    public void Take_EmptyArray()
    {
        Assert.Empty(Invoke("take", Arr(), I(5)).AsArray()!);
    }

    [Fact]
    public void Take_ExactLength()
    {
        var data = Arr(I(1), I(2));
        Assert.Equal(2, Invoke("take", data, I(2)).AsArray()!.Count);
    }

    // =========================================================================
    // drop
    // =========================================================================

    [Fact]
    public void Drop_FirstN()
    {
        var data = Arr(I(1), I(2), I(3), I(4));
        var result = Invoke("drop", data, I(2));
        Assert.Equal(2, result.AsArray()!.Count);
        Assert.Equal(3, result.AsArray()![0].AsInt64());
    }

    [Fact]
    public void Drop_All()
    {
        var data = Arr(I(1), I(2));
        Assert.Empty(Invoke("drop", data, I(10)).AsArray()!);
    }

    [Fact]
    public void Drop_Zero()
    {
        var data = Arr(I(1), I(2), I(3));
        Assert.Equal(3, Invoke("drop", data, I(0)).AsArray()!.Count);
    }

    [Fact]
    public void Drop_EmptyArray()
    {
        Assert.Empty(Invoke("drop", Arr(), I(5)).AsArray()!);
    }

    [Fact]
    public void Drop_ExactLength()
    {
        Assert.Empty(Invoke("drop", Arr(I(1), I(2)), I(2)).AsArray()!);
    }

    // =========================================================================
    // chunk
    // =========================================================================

    [Fact]
    public void Chunk_Even()
    {
        var data = Arr(I(1), I(2), I(3), I(4));
        var result = Invoke("chunk", data, I(2));
        Assert.Equal(2, result.AsArray()!.Count);
        Assert.Equal(2, result.AsArray()![0].AsArray()!.Count);
        Assert.Equal(2, result.AsArray()![1].AsArray()!.Count);
    }

    [Fact]
    public void Chunk_Uneven()
    {
        var data = Arr(I(1), I(2), I(3));
        var result = Invoke("chunk", data, I(2));
        Assert.Equal(2, result.AsArray()!.Count);
        Assert.Equal(2, result.AsArray()![0].AsArray()!.Count);
        Assert.Single(result.AsArray()![1].AsArray()!);
    }

    [Fact]
    public void Chunk_SingleElement()
    {
        var result = Invoke("chunk", Arr(I(1)), I(1));
        Assert.Single(result.AsArray()!);
        Assert.Single(result.AsArray()![0].AsArray()!);
    }

    [Fact]
    public void Chunk_SizeLargerThanArray()
    {
        var data = Arr(I(1), I(2));
        var result = Invoke("chunk", data, I(10));
        Assert.Single(result.AsArray()!);
        Assert.Equal(2, result.AsArray()![0].AsArray()!.Count);
    }

    [Fact]
    public void Chunk_EmptyArray()
    {
        Assert.Empty(Invoke("chunk", Arr(), I(3)).AsArray()!);
    }

    [Fact]
    public void Chunk_SizeOne()
    {
        var data = Arr(I(1), I(2), I(3));
        var result = Invoke("chunk", data, I(1));
        Assert.Equal(3, result.AsArray()!.Count);
    }

    // =========================================================================
    // range
    // =========================================================================

    [Fact]
    public void Range_Basic()
    {
        var result = Invoke("range", I(0), I(5));
        Assert.Equal(5, result.AsArray()!.Count);
        Assert.Equal(0, result.AsArray()![0].AsInt64());
        Assert.Equal(4, result.AsArray()![4].AsInt64());
    }

    [Fact]
    public void Range_WithStep()
    {
        var result = Invoke("range", I(0), I(10), I(3));
        Assert.Equal(4, result.AsArray()!.Count);
        Assert.Equal(0, result.AsArray()![0].AsInt64());
        Assert.Equal(9, result.AsArray()![3].AsInt64());
    }

    [Fact]
    public void Range_NegativeStep()
    {
        var result = Invoke("range", I(5), I(0), I(-2));
        Assert.Equal(3, result.AsArray()!.Count);
        Assert.Equal(5, result.AsArray()![0].AsInt64());
        Assert.Equal(1, result.AsArray()![2].AsInt64());
    }

    [Fact]
    public void Range_EmptyWhenStartGeEnd()
    {
        Assert.Empty(Invoke("range", I(5), I(3)).AsArray()!);
    }

    [Fact]
    public void Range_SingleElement()
    {
        var result = Invoke("range", I(0), I(1));
        Assert.Single(result.AsArray()!);
        Assert.Equal(0, result.AsArray()![0].AsInt64());
    }

    [Fact]
    public void Range_NegativeValues()
    {
        var result = Invoke("range", I(-3), I(0));
        Assert.Equal(3, result.AsArray()!.Count);
        Assert.Equal(-3, result.AsArray()![0].AsInt64());
    }

    [Fact]
    public void Range_SameStartEnd()
    {
        Assert.Empty(Invoke("range", I(5), I(5)).AsArray()!);
    }

    [Fact]
    public void Range_StepOfTwo()
    {
        var result = Invoke("range", I(0), I(6), I(2));
        Assert.Equal(3, result.AsArray()!.Count);
        Assert.Equal(0, result.AsArray()![0].AsInt64());
        Assert.Equal(4, result.AsArray()![2].AsInt64());
    }

    // =========================================================================
    // compact
    // =========================================================================

    [Fact]
    public void Compact_RemovesNullsAndEmpty()
    {
        var data = Arr(I(1), Null(), S(""), I(2), S("ok"));
        var result = Invoke("compact", data);
        Assert.Equal(3, result.AsArray()!.Count);
    }

    [Fact]
    public void Compact_AllNull()
    {
        Assert.Empty(Invoke("compact", Arr(Null(), Null())).AsArray()!);
    }

    [Fact]
    public void Compact_NoNulls()
    {
        var data = Arr(I(1), I(2), I(3));
        Assert.Equal(3, Invoke("compact", data).AsArray()!.Count);
    }

    [Fact]
    public void Compact_EmptyArray()
    {
        Assert.Empty(Invoke("compact", Arr()).AsArray()!);
    }

    [Fact]
    public void Compact_OnlyEmptyStrings()
    {
        Assert.Empty(Invoke("compact", Arr(S(""), S(""), S(""))).AsArray()!);
    }

    [Fact]
    public void Compact_KeepsNonEmptyStrings()
    {
        var data = Arr(S(""), S("hello"), Null(), S("world"));
        var result = Invoke("compact", data);
        Assert.Equal(2, result.AsArray()!.Count);
    }

    // =========================================================================
    // dedupe
    // =========================================================================

    [Fact]
    public void Dedupe_ConsecutiveDuplicates()
    {
        var data = Arr(I(1), I(1), I(2), I(2), I(3));
        var result = Invoke("dedupe", data);
        Assert.Equal(3, result.AsArray()!.Count);
    }

    [Fact]
    public void Dedupe_NoDuplicates()
    {
        var data = Arr(I(1), I(2), I(3));
        Assert.Equal(3, Invoke("dedupe", data).AsArray()!.Count);
    }

    [Fact]
    public void Dedupe_NonConsecutiveDuplicatesKept()
    {
        var data = Arr(I(1), I(2), I(1));
        Assert.Equal(3, Invoke("dedupe", data).AsArray()!.Count);
    }

    [Fact]
    public void Dedupe_EmptyArray()
    {
        Assert.Empty(Invoke("dedupe", Arr()).AsArray()!);
    }

    // =========================================================================
    // filter (truthiness-based in .NET SDK)
    // =========================================================================

    [Fact]
    public void Filter_TruthyElements()
    {
        var data = Arr(I(1), Null(), I(0), S("hello"), S(""), B(true), B(false));
        var result = Invoke("filter", data);
        // Truthy: 1, "hello", true
        Assert.Equal(3, result.AsArray()!.Count);
    }

    [Fact]
    public void Filter_EmptyArray()
    {
        Assert.Empty(Invoke("filter", Arr()).AsArray()!);
    }

    [Fact]
    public void Filter_AllTruthy()
    {
        var data = Arr(I(1), I(2), I(3));
        Assert.Equal(3, Invoke("filter", data).AsArray()!.Count);
    }

    [Fact]
    public void Filter_AllFalsy()
    {
        var data = Arr(Null(), I(0), S(""));
        Assert.Empty(Invoke("filter", data).AsArray()!);
    }

    [Fact]
    public void Filter_ByFieldName()
    {
        var data = Arr(
            Obj(("active", B(true)), ("name", S("A"))),
            Obj(("active", B(false)), ("name", S("B"))),
            Obj(("active", B(true)), ("name", S("C")))
        );
        var result = Invoke("filter", data, S("active"));
        Assert.Equal(2, result.AsArray()!.Count);
    }

    // =========================================================================
    // keys
    // =========================================================================

    [Fact]
    public void Keys_OfObject()
    {
        var data = Obj(("a", I(1)), ("b", I(2)));
        var result = Invoke("keys", data);
        Assert.Equal(2, result.AsArray()!.Count);
        Assert.Equal("a", result.AsArray()![0].AsString());
        Assert.Equal("b", result.AsArray()![1].AsString());
    }

    [Fact]
    public void Keys_EmptyObject()
    {
        Assert.Empty(Invoke("keys", Obj()).AsArray()!);
    }

    [Fact]
    public void Keys_SingleKey()
    {
        var result = Invoke("keys", Obj(("only", I(1))));
        Assert.Single(result.AsArray()!);
        Assert.Equal("only", result.AsArray()![0].AsString());
    }

    [Fact]
    public void Keys_PreservesOrder()
    {
        var data = Obj(("z", I(1)), ("a", I(2)), ("m", I(3)));
        var result = Invoke("keys", data);
        Assert.Equal("z", result.AsArray()![0].AsString());
        Assert.Equal("a", result.AsArray()![1].AsString());
        Assert.Equal("m", result.AsArray()![2].AsString());
    }

    // =========================================================================
    // values
    // =========================================================================

    [Fact]
    public void Values_OfObject()
    {
        var data = Obj(("a", I(1)), ("b", I(2)));
        var result = Invoke("values", data);
        Assert.Equal(2, result.AsArray()!.Count);
        Assert.Equal(1, result.AsArray()![0].AsInt64());
        Assert.Equal(2, result.AsArray()![1].AsInt64());
    }

    [Fact]
    public void Values_EmptyObject()
    {
        Assert.Empty(Invoke("values", Obj()).AsArray()!);
    }

    [Fact]
    public void Values_MixedTypes()
    {
        var data = Obj(("a", I(1)), ("b", S("two")), ("c", B(true)));
        Assert.Equal(3, Invoke("values", data).AsArray()!.Count);
    }

    // =========================================================================
    // entries
    // =========================================================================

    [Fact]
    public void Entries_OfObject()
    {
        var data = Obj(("x", I(1)));
        var result = Invoke("entries", data);
        Assert.Single(result.AsArray()!);
        var pair = result.AsArray()![0].AsArray()!;
        Assert.Equal("x", pair[0].AsString());
        Assert.Equal(1, pair[1].AsInt64());
    }

    [Fact]
    public void Entries_EmptyObject()
    {
        Assert.Empty(Invoke("entries", Obj()).AsArray()!);
    }

    [Fact]
    public void Entries_MultiplePairs()
    {
        var data = Obj(("a", I(1)), ("b", I(2)));
        var result = Invoke("entries", data);
        Assert.Equal(2, result.AsArray()!.Count);
        Assert.Equal("a", result.AsArray()![0].AsArray()![0].AsString());
        Assert.Equal("b", result.AsArray()![1].AsArray()![0].AsString());
    }

    // =========================================================================
    // has
    // =========================================================================

    [Fact]
    public void Has_ExistingKey()
    {
        Assert.True(Invoke("has", Obj(("a", I(1))), S("a")).AsBool());
    }

    [Fact]
    public void Has_MissingKey()
    {
        Assert.False(Invoke("has", Obj(("a", I(1))), S("z")).AsBool());
    }

    [Fact]
    public void Has_EmptyObject()
    {
        Assert.False(Invoke("has", Obj(), S("anything")).AsBool());
    }

    [Fact]
    public void Has_WithNullValue()
    {
        Assert.True(Invoke("has", Obj(("key", Null())), S("key")).AsBool());
    }

    // =========================================================================
    // get
    // =========================================================================

    [Fact]
    public void Get_SimpleKey()
    {
        var data = Obj(("a", I(42)));
        Assert.Equal(42, Invoke("get", data, S("a")).AsInt64());
    }

    [Fact]
    public void Get_MissingReturnsNull()
    {
        Assert.True(Invoke("get", Obj(("a", I(1))), S("z")).IsNull);
    }

    // =========================================================================
    // merge
    // =========================================================================

    [Fact]
    public void Merge_Objects()
    {
        var a = Obj(("a", I(1)), ("b", I(2)));
        var b = Obj(("b", I(99)), ("c", I(3)));
        var result = Invoke("merge", a, b);
        Assert.Equal(3, result.AsObject()!.Count);
    }

    [Fact]
    public void Merge_BothEmpty()
    {
        Assert.Empty(Invoke("merge", Obj(), Obj()).AsObject()!);
    }

    [Fact]
    public void Merge_NoOverlap()
    {
        var result = Invoke("merge", Obj(("a", I(1))), Obj(("b", I(2))));
        Assert.Equal(2, result.AsObject()!.Count);
    }

    [Fact]
    public void Merge_CompleteOverlap()
    {
        var result = Invoke("merge", Obj(("x", I(1))), Obj(("x", I(2))));
        Assert.Single(result.AsObject()!);
        Assert.Equal(2, result.AsObject()![0].Value.AsInt64());
    }

    // =========================================================================
    // sum
    // =========================================================================

    [Fact]
    public void Sum_Integers()
    {
        Assert.Equal(6, Invoke("sum", Arr(I(1), I(2), I(3))).AsInt64());
    }

    [Fact]
    public void Sum_Floats()
    {
        Assert.Equal(4.0, Invoke("sum", Arr(F(1.5), F(2.5))).AsDouble());
    }

    [Fact]
    public void Sum_Empty()
    {
        Assert.Equal(0, Invoke("sum", Arr()).AsInt64());
    }

    [Fact]
    public void Sum_SingleElement()
    {
        Assert.Equal(42, Invoke("sum", Arr(I(42))).AsInt64());
    }

    [Fact]
    public void Sum_NegativeNumbers()
    {
        Assert.Equal(-6, Invoke("sum", Arr(I(-1), I(-2), I(-3))).AsInt64());
    }

    [Fact]
    public void Sum_LargeFloats()
    {
        Assert.Equal(3e10, Invoke("sum", Arr(F(1e10), F(2e10))).AsDouble());
    }

    // =========================================================================
    // count
    // =========================================================================

    [Fact]
    public void Count_Elements()
    {
        Assert.Equal(3, Invoke("count", Arr(I(1), I(2), I(3))).AsInt64());
    }

    [Fact]
    public void Count_Empty()
    {
        Assert.Equal(0, Invoke("count", Arr()).AsInt64());
    }

    [Fact]
    public void Count_WithNulls()
    {
        Assert.Equal(3, Invoke("count", Arr(Null(), Null(), I(1))).AsInt64());
    }

    [Fact]
    public void Count_SingleElement()
    {
        Assert.Equal(1, Invoke("count", Arr(I(42))).AsInt64());
    }

    // =========================================================================
    // min
    // =========================================================================

    [Fact]
    public void Min_Integers()
    {
        Assert.Equal(1, Invoke("min", Arr(I(3), I(1), I(2))).AsInt64());
    }

    [Fact]
    public void Min_EmptyIsNull()
    {
        Assert.True(Invoke("min", Arr()).IsNull);
    }

    [Fact]
    public void Min_Floats()
    {
        Assert.Equal(1.2, Invoke("min", Arr(F(3.5), F(1.2), F(2.8))).AsDouble());
    }

    [Fact]
    public void Min_SingleElement()
    {
        Assert.Equal(42, Invoke("min", Arr(I(42))).AsInt64());
    }

    [Fact]
    public void Min_NegativeNumbers()
    {
        Assert.Equal(-5, Invoke("min", Arr(I(-1), I(-5), I(-2))).AsInt64());
    }

    // =========================================================================
    // max
    // =========================================================================

    [Fact]
    public void Max_Integers()
    {
        Assert.Equal(3, Invoke("max", Arr(I(3), I(1), I(2))).AsInt64());
    }

    [Fact]
    public void Max_EmptyIsNull()
    {
        Assert.True(Invoke("max", Arr()).IsNull);
    }

    [Fact]
    public void Max_SingleElement()
    {
        Assert.Equal(42, Invoke("max", Arr(I(42))).AsInt64());
    }

    [Fact]
    public void Max_NegativeNumbers()
    {
        Assert.Equal(-1, Invoke("max", Arr(I(-1), I(-5), I(-2))).AsInt64());
    }

    [Fact]
    public void Max_Floats()
    {
        Assert.Equal(9.9, Invoke("max", Arr(F(1.1), F(9.9), F(5.5))).AsDouble());
    }

    // =========================================================================
    // avg
    // =========================================================================

    [Fact]
    public void Avg_Basic()
    {
        Assert.Equal(20.0, Invoke("avg", Arr(I(10), I(20), I(30))).AsDouble());
    }

    [Fact]
    public void Avg_EmptyIsNull()
    {
        Assert.True(Invoke("avg", Arr()).IsNull);
    }

    [Fact]
    public void Avg_Single()
    {
        Assert.Equal(7.0, Invoke("avg", Arr(I(7))).AsDouble());
    }

    [Fact]
    public void Avg_Floats()
    {
        Assert.Equal(2.0, Invoke("avg", Arr(F(1.0), F(2.0), F(3.0))).AsDouble());
    }

    // =========================================================================
    // first
    // =========================================================================

    [Fact]
    public void First_OfArray()
    {
        Assert.Equal("a", Invoke("first", Arr(S("a"), S("b"))).AsString());
    }

    [Fact]
    public void First_EmptyIsNull()
    {
        Assert.True(Invoke("first", Arr()).IsNull);
    }

    [Fact]
    public void First_SingleElement()
    {
        Assert.Equal(42, Invoke("first", Arr(I(42))).AsInt64());
    }

    [Fact]
    public void First_NullElement()
    {
        Assert.True(Invoke("first", Arr(Null(), I(1))).IsNull);
    }

    // =========================================================================
    // last
    // =========================================================================

    [Fact]
    public void Last_OfArray()
    {
        Assert.Equal("c", Invoke("last", Arr(S("a"), S("b"), S("c"))).AsString());
    }

    [Fact]
    public void Last_EmptyIsNull()
    {
        Assert.True(Invoke("last", Arr()).IsNull);
    }

    [Fact]
    public void Last_SingleElement()
    {
        Assert.Equal(42, Invoke("last", Arr(I(42))).AsInt64());
    }

    [Fact]
    public void Last_NullAtEnd()
    {
        Assert.True(Invoke("last", Arr(I(1), Null())).IsNull);
    }

    // =========================================================================
    // cumsum
    // =========================================================================

    [Fact]
    public void Cumsum_Basic()
    {
        var result = Invoke("cumsum", Arr(I(1), I(2), I(3)));
        Assert.Equal(1, result.AsArray()![0].AsInt64());
        Assert.Equal(3, result.AsArray()![1].AsInt64());
        Assert.Equal(6, result.AsArray()![2].AsInt64());
    }

    [Fact]
    public void Cumsum_WithNull()
    {
        var result = Invoke("cumsum", Arr(I(1), Null(), I(3)));
        Assert.Equal(1, result.AsArray()![0].AsInt64());
        Assert.True(result.AsArray()![1].IsNull);
        Assert.Equal(4, result.AsArray()![2].AsInt64());
    }

    [Fact]
    public void Cumsum_Empty()
    {
        Assert.Empty(Invoke("cumsum", Arr()).AsArray()!);
    }

    [Fact]
    public void Cumsum_SingleElement()
    {
        Assert.Equal(5, Invoke("cumsum", Arr(I(5))).AsArray()![0].AsInt64());
    }

    [Fact]
    public void Cumsum_NegativeNumbers()
    {
        var result = Invoke("cumsum", Arr(I(5), I(-3), I(2)));
        Assert.Equal(5, result.AsArray()![0].AsInt64());
        Assert.Equal(2, result.AsArray()![1].AsInt64());
        Assert.Equal(4, result.AsArray()![2].AsInt64());
    }

    // =========================================================================
    // cumprod
    // =========================================================================

    [Fact]
    public void Cumprod_Basic()
    {
        var result = Invoke("cumprod", Arr(I(1), I(2), I(3)));
        Assert.Equal(1, result.AsArray()![0].AsInt64());
        Assert.Equal(2, result.AsArray()![1].AsInt64());
        Assert.Equal(6, result.AsArray()![2].AsInt64());
    }

    [Fact]
    public void Cumprod_WithZero()
    {
        var result = Invoke("cumprod", Arr(I(5), I(0), I(3)));
        Assert.Equal(5, result.AsArray()![0].AsInt64());
        Assert.Equal(0, result.AsArray()![1].AsInt64());
        Assert.Equal(0, result.AsArray()![2].AsInt64());
    }

    [Fact]
    public void Cumprod_SingleElement()
    {
        Assert.Equal(5, Invoke("cumprod", Arr(I(5))).AsArray()![0].AsInt64());
    }

    [Fact]
    public void Cumprod_Empty()
    {
        Assert.Empty(Invoke("cumprod", Arr()).AsArray()!);
    }

    // =========================================================================
    // diff
    // =========================================================================

    [Fact]
    public void Diff_Basic()
    {
        var result = Invoke("diff", Arr(I(10), I(20), I(15)));
        Assert.True(result.AsArray()![0].IsNull);
        Assert.Equal(10, result.AsArray()![1].AsInt64());
        Assert.Equal(-5, result.AsArray()![2].AsInt64());
    }

    [Fact]
    public void Diff_Empty()
    {
        Assert.Empty(Invoke("diff", Arr()).AsArray()!);
    }

    [Fact]
    public void Diff_SingleElement()
    {
        var result = Invoke("diff", Arr(I(5)));
        Assert.Single(result.AsArray()!);
        Assert.True(result.AsArray()![0].IsNull);
    }

    [Fact]
    public void Diff_Floats()
    {
        var result = Invoke("diff", Arr(F(1.0), F(3.0), F(6.0)));
        Assert.True(result.AsArray()![0].IsNull);
        Assert.Equal(2, result.AsArray()![1].AsInt64());
        Assert.Equal(3, result.AsArray()![2].AsInt64());
    }

    // =========================================================================
    // pctChange
    // =========================================================================

    [Fact]
    public void PctChange_Basic()
    {
        var result = Invoke("pctChange", Arr(I(100), I(110)));
        Assert.True(result.AsArray()![0].IsNull);
        Assert.True(Math.Abs(result.AsArray()![1].AsDouble()!.Value - 0.1) < 1e-10);
    }

    [Fact]
    public void PctChange_Empty()
    {
        Assert.Empty(Invoke("pctChange", Arr()).AsArray()!);
    }

    [Fact]
    public void PctChange_Single()
    {
        var result = Invoke("pctChange", Arr(I(100)));
        Assert.True(result.AsArray()![0].IsNull);
    }

    [Fact]
    public void PctChange_Doubling()
    {
        var result = Invoke("pctChange", Arr(I(100), I(200)));
        Assert.True(Math.Abs(result.AsArray()![1].AsDouble()!.Value - 1.0) < 1e-10);
    }

    [Fact]
    public void PctChange_WithZeroPrevious()
    {
        var result = Invoke("pctChange", Arr(I(0), I(100)));
        Assert.True(result.AsArray()![1].IsNull); // Division by zero -> null
    }

    // =========================================================================
    // shift
    // =========================================================================

    [Fact]
    public void Shift_Forward()
    {
        var result = Invoke("shift", Arr(I(1), I(2), I(3)), I(1));
        Assert.True(result.AsArray()![0].IsNull);
        Assert.Equal(1, result.AsArray()![1].AsInt64());
        Assert.Equal(2, result.AsArray()![2].AsInt64());
    }

    [Fact]
    public void Shift_Backward()
    {
        var result = Invoke("shift", Arr(I(1), I(2), I(3)), I(-1));
        Assert.Equal(2, result.AsArray()![0].AsInt64());
        Assert.Equal(3, result.AsArray()![1].AsInt64());
        Assert.True(result.AsArray()![2].IsNull);
    }

    [Fact]
    public void Shift_ZeroNoChange()
    {
        var result = Invoke("shift", Arr(I(1), I(2), I(3)), I(0));
        Assert.Equal(1, result.AsArray()![0].AsInt64());
        Assert.Equal(3, result.AsArray()![2].AsInt64());
    }

    // =========================================================================
    // lag
    // =========================================================================

    [Fact]
    public void Lag_DefaultPeriod()
    {
        var result = Invoke("lag", Arr(I(10), I(20), I(30)));
        Assert.True(result.AsArray()![0].IsNull);
        Assert.Equal(10, result.AsArray()![1].AsInt64());
        Assert.Equal(20, result.AsArray()![2].AsInt64());
    }

    [Fact]
    public void Lag_PeriodTwo()
    {
        var result = Invoke("lag", Arr(I(10), I(20), I(30), I(40)), I(2));
        Assert.True(result.AsArray()![0].IsNull);
        Assert.True(result.AsArray()![1].IsNull);
        Assert.Equal(10, result.AsArray()![2].AsInt64());
        Assert.Equal(20, result.AsArray()![3].AsInt64());
    }

    // =========================================================================
    // lead
    // =========================================================================

    [Fact]
    public void Lead_DefaultPeriod()
    {
        var result = Invoke("lead", Arr(I(10), I(20), I(30)));
        Assert.Equal(20, result.AsArray()![0].AsInt64());
        Assert.Equal(30, result.AsArray()![1].AsInt64());
        Assert.True(result.AsArray()![2].IsNull);
    }

    [Fact]
    public void Lead_PeriodTwo()
    {
        var result = Invoke("lead", Arr(I(10), I(20), I(30), I(40)), I(2));
        Assert.Equal(30, result.AsArray()![0].AsInt64());
        Assert.Equal(40, result.AsArray()![1].AsInt64());
        Assert.True(result.AsArray()![2].IsNull);
        Assert.True(result.AsArray()![3].IsNull);
    }

    // =========================================================================
    // rank
    // =========================================================================

    [Fact]
    public void Rank_Basic()
    {
        var result = Invoke("rank", Arr(I(10), I(30), I(20)));
        // Highest=rank 1 (descending). 30->1, 20->2, 10->3
        Assert.Equal(3, result.AsArray()![0].AsInt64()); // 10 -> rank 3
        Assert.Equal(1, result.AsArray()![1].AsInt64()); // 30 -> rank 1
        Assert.Equal(2, result.AsArray()![2].AsInt64()); // 20 -> rank 2
    }

    [Fact]
    public void Rank_TiedValues()
    {
        var result = Invoke("rank", Arr(I(10), I(10), I(30)));
        // Both 10s get same rank
        Assert.Equal(result.AsArray()![0].AsInt64(), result.AsArray()![1].AsInt64());
    }

    [Fact]
    public void Rank_SingleElement()
    {
        var result = Invoke("rank", Arr(I(42)));
        Assert.Equal(1, result.AsArray()![0].AsInt64());
    }

    // =========================================================================
    // fillMissing
    // =========================================================================

    [Fact]
    public void FillMissing_ValueStrategy()
    {
        var result = Invoke("fillMissing", Arr(I(1), Null(), I(3)), I(0));
        Assert.Equal(1, result.AsArray()![0].AsInt64());
        Assert.Equal(0, result.AsArray()![1].AsInt64());
        Assert.Equal(3, result.AsArray()![2].AsInt64());
    }

    [Fact]
    public void FillMissing_Forward()
    {
        var result = Invoke("fillMissing", Arr(I(1), Null(), Null(), I(4)), S("forward"));
        Assert.Equal(1, result.AsArray()![0].AsInt64());
        Assert.Equal(1, result.AsArray()![1].AsInt64());
        Assert.Equal(1, result.AsArray()![2].AsInt64());
        Assert.Equal(4, result.AsArray()![3].AsInt64());
    }

    [Fact]
    public void FillMissing_Backward()
    {
        var result = Invoke("fillMissing", Arr(Null(), Null(), I(3), I(4)), S("backward"));
        Assert.Equal(3, result.AsArray()![0].AsInt64());
        Assert.Equal(3, result.AsArray()![1].AsInt64());
        Assert.Equal(3, result.AsArray()![2].AsInt64());
        Assert.Equal(4, result.AsArray()![3].AsInt64());
    }

    [Fact]
    public void FillMissing_NoNulls()
    {
        var result = Invoke("fillMissing", Arr(I(1), I(2), I(3)), I(0));
        Assert.Equal(3, result.AsArray()!.Count);
    }

    [Fact]
    public void FillMissing_AllNulls()
    {
        var result = Invoke("fillMissing", Arr(Null(), Null()), I(99));
        Assert.Equal(99, result.AsArray()![0].AsInt64());
        Assert.Equal(99, result.AsArray()![1].AsInt64());
    }

    [Fact]
    public void FillMissing_EmptyArray()
    {
        Assert.Empty(Invoke("fillMissing", Arr(), I(0)).AsArray()!);
    }

    // =========================================================================
    // sample
    // =========================================================================

    [Fact]
    public void Sample_TakesNElements()
    {
        var data = Arr(I(1), I(2), I(3), I(4));
        var result = Invoke("sample", data, I(2));
        Assert.Equal(2, result.AsArray()!.Count);
    }

    [Fact]
    public void Sample_ZeroCount()
    {
        Assert.Empty(Invoke("sample", Arr(I(1), I(2)), I(0)).AsArray()!);
    }

    [Fact]
    public void Sample_MoreThanLength()
    {
        var data = Arr(I(1), I(2));
        Assert.Equal(2, Invoke("sample", data, I(10)).AsArray()!.Count);
    }

    [Fact]
    public void Sample_EmptyArray()
    {
        Assert.Empty(Invoke("sample", Arr(), I(5)).AsArray()!);
    }

    // =========================================================================
    // limit (alias for take)
    // =========================================================================

    [Fact]
    public void Limit_BasicN()
    {
        var data = Arr(I(1), I(2), I(3));
        var result = Invoke("limit", data, I(2));
        Assert.Equal(2, result.AsArray()!.Count);
    }

    [Fact]
    public void Limit_Zero()
    {
        Assert.Empty(Invoke("limit", Arr(I(1), I(2)), I(0)).AsArray()!);
    }

    [Fact]
    public void Limit_ExactLength()
    {
        var data = Arr(I(1), I(2));
        Assert.Equal(2, Invoke("limit", data, I(2)).AsArray()!.Count);
    }

    // =========================================================================
    // rowNumber
    // =========================================================================

    [Fact]
    public void RowNumber_Increments()
    {
        var ctx = new VerbContext();
        var r1 = _registry.Invoke("rowNumber", Array.Empty<DynValue>(), ctx);
        var r2 = _registry.Invoke("rowNumber", Array.Empty<DynValue>(), ctx);
        Assert.Equal(1, r1.AsInt64());
        Assert.Equal(2, r2.AsInt64());
    }

    // =========================================================================
    // accumulate
    // =========================================================================

    [Fact]
    public void Accumulate_SumOp()
    {
        var ctx = new VerbContext();
        ctx.Accumulators["total"] = I(10);
        var result = _registry.Invoke("accumulate", new[] { S("total"), S("sum"), I(5) }, ctx);
        Assert.Equal(15, result.AsInt64());
    }

    [Fact]
    public void Accumulate_CountOp()
    {
        var ctx = new VerbContext();
        ctx.Accumulators["cnt"] = I(0);
        var result = _registry.Invoke("accumulate", new[] { S("cnt"), S("count"), I(0) }, ctx);
        Assert.Equal(1, result.AsInt64());
    }

    [Fact]
    public void Accumulate_FirstOp()
    {
        var ctx = new VerbContext();
        var r1 = _registry.Invoke("accumulate", new[] { S("f"), S("first"), S("hello") }, ctx);
        var r2 = _registry.Invoke("accumulate", new[] { S("f"), S("first"), S("world") }, ctx);
        Assert.Equal("hello", r1.AsString());
        Assert.Equal("hello", r2.AsString()); // first stays
    }

    [Fact]
    public void Accumulate_LastOp()
    {
        var ctx = new VerbContext();
        _registry.Invoke("accumulate", new[] { S("l"), S("last"), S("hello") }, ctx);
        var r2 = _registry.Invoke("accumulate", new[] { S("l"), S("last"), S("world") }, ctx);
        Assert.Equal("world", r2.AsString());
    }

    // =========================================================================
    // set
    // =========================================================================

    [Fact]
    public void Set_ReturnsValue()
    {
        var ctx = new VerbContext();
        var result = _registry.Invoke("set", new[] { S("counter"), I(42) }, ctx);
        Assert.Equal(42, result.AsInt64());
    }

    [Fact]
    public void Set_StringValue()
    {
        var ctx = new VerbContext();
        Assert.Equal("hello", _registry.Invoke("set", new[] { S("name"), S("hello") }, ctx).AsString());
    }

    // =========================================================================
    // Nested/chained operations
    // =========================================================================

    [Fact]
    public void Nested_SortThenTake()
    {
        var data = Arr(I(5), I(1), I(3), I(2), I(4));
        var sorted = Invoke("sort", data);
        var result = Invoke("take", sorted, I(3));
        Assert.Equal(1, result.AsArray()![0].AsInt64());
        Assert.Equal(3, result.AsArray()![2].AsInt64());
    }

    [Fact]
    public void Nested_FlattenThenDistinct()
    {
        var data = Arr(Arr(I(1), I(2)), Arr(I(2), I(3)));
        var flat = Invoke("flatten", data);
        var result = Invoke("distinct", flat);
        Assert.Equal(3, result.AsArray()!.Count);
    }

    [Fact]
    public void Nested_ConcatThenSort()
    {
        var a = Arr(I(3), I(1));
        var b = Arr(I(4), I(2));
        var combined = Invoke("concatArrays", a, b);
        var result = Invoke("sort", combined);
        Assert.Equal(1, result.AsArray()![0].AsInt64());
        Assert.Equal(4, result.AsArray()![3].AsInt64());
    }

    [Fact]
    public void Nested_ReverseThenFirst()
    {
        var data = Arr(I(1), I(2), I(3));
        var reversed = Invoke("reverse", data);
        Assert.Equal(3, Invoke("first", reversed).AsInt64());
    }

    [Fact]
    public void Nested_ChunkThenFirst()
    {
        var data = Arr(I(1), I(2), I(3), I(4));
        var chunks = Invoke("chunk", data, I(2));
        var first = Invoke("first", chunks);
        Assert.Equal(2, first.AsArray()!.Count);
    }

    [Fact]
    public void Nested_DropThenCount()
    {
        var data = Arr(I(1), I(2), I(3), I(4), I(5));
        var dropped = Invoke("drop", data, I(2));
        Assert.Equal(3, Invoke("count", dropped).AsInt64());
    }

    [Fact]
    public void Nested_CompactThenSum()
    {
        var data = Arr(I(1), Null(), I(2), S(""), I(3));
        var compacted = Invoke("compact", data);
        Assert.Equal(6, Invoke("sum", compacted).AsInt64());
    }
}
