using System;
using System.Collections.Generic;
using Odin.Core.Types;
using Odin.Core.Transform;
using Odin.Core.Transform.Verbs;
using Xunit;

namespace Odin.Core.Tests.Unit;

/// <summary>
/// Extended tests for collection, array, object, aggregation, and time-series verbs.
/// Ported from Rust SDK collection_verbs.rs extended_tests / extended_tests_2.
/// </summary>
public class CollectionVerbExtendedTests
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

    private static void AssertArrayLength(DynValue result, int expectedLen)
    {
        var arr = result.AsArray();
        Assert.NotNull(arr);
        Assert.Equal(expectedLen, arr!.Count);
    }

    // =========================================================================
    // flatten — extended
    // =========================================================================

    [Fact]
    public void Flatten_SingleLevelOnly()
    {
        var data = Arr(Arr(Arr(I(1))));
        var result = Invoke("flatten", data);
        // Only flattens one level
        Assert.Equal(Arr(Arr(I(1))), result);
    }

    [Fact]
    public void Flatten_AllScalars()
    {
        var data = Arr(I(1), I(2), I(3));
        Assert.Equal(Arr(I(1), I(2), I(3)), Invoke("flatten", data));
    }

    [Fact]
    public void Flatten_WithNulls()
    {
        var data = Arr(Null(), Arr(I(1)));
        Assert.Equal(Arr(Null(), I(1)), Invoke("flatten", data));
    }

    [Fact]
    public void Flatten_EmptyArray()
    {
        Assert.Equal(Arr(), Invoke("flatten", Arr()));
    }

    [Fact]
    public void Flatten_MixedNestedAndScalar()
    {
        var data = Arr(I(1), Arr(I(2), I(3)), I(4));
        Assert.Equal(Arr(I(1), I(2), I(3), I(4)), Invoke("flatten", data));
    }

    // =========================================================================
    // distinct / unique — extended
    // =========================================================================

    [Fact]
    public void Distinct_SingleElement()
    {
        Assert.Equal(Arr(I(42)), Invoke("distinct", Arr(I(42))));
    }

    [Fact]
    public void Distinct_AllSame()
    {
        Assert.Equal(Arr(S("a")), Invoke("distinct", Arr(S("a"), S("a"), S("a"))));
    }

    [Fact]
    public void Distinct_MixedTypes()
    {
        var result = Invoke("distinct", Arr(I(1), S("1"), B(true), Null()));
        AssertArrayLength(result, 4);
    }

    [Fact]
    public void Distinct_PreservesOrder()
    {
        Assert.Equal(Arr(I(3), I(1), I(2)), Invoke("distinct", Arr(I(3), I(1), I(2), I(1), I(3))));
    }

    [Fact]
    public void Distinct_WithNulls()
    {
        Assert.Equal(Arr(Null(), I(1), I(2)), Invoke("distinct", Arr(Null(), I(1), Null(), I(2))));
    }

    [Fact]
    public void Unique_IsAliasForDistinct()
    {
        Assert.Equal(Arr(I(1), I(2)), Invoke("unique", Arr(I(1), I(2), I(1))));
    }

    // =========================================================================
    // sort — extended
    // =========================================================================

    [Fact]
    public void Sort_MixedIntFloat()
    {
        var result = Invoke("sort", Arr(F(2.5), I(1), I(3)));
        var arr = result.AsArray()!;
        Assert.Equal(I(1), arr[0]);
        Assert.Equal(F(2.5), arr[1]);
        Assert.Equal(I(3), arr[2]);
    }

    [Fact]
    public void Sort_AlreadySorted()
    {
        Assert.Equal(Arr(I(1), I(2), I(3)), Invoke("sort", Arr(I(1), I(2), I(3))));
    }

    [Fact]
    public void Sort_ReverseOrder()
    {
        Assert.Equal(Arr(I(1), I(2), I(3)), Invoke("sort", Arr(I(3), I(2), I(1))));
    }

    [Fact]
    public void Sort_WithDuplicates()
    {
        Assert.Equal(Arr(I(1), I(1), I(2), I(2)), Invoke("sort", Arr(I(2), I(1), I(2), I(1))));
    }

    [Fact]
    public void Sort_StringsCaseSensitive()
    {
        // Uppercase A comes before lowercase b in ASCII
        Assert.Equal(Arr(S("Apple"), S("banana"), S("cherry")),
            Invoke("sort", Arr(S("banana"), S("Apple"), S("cherry"))));
    }

    // =========================================================================
    // sortDesc — extended
    // =========================================================================

    [Fact]
    public void SortDesc_Strings()
    {
        Assert.Equal(Arr(S("c"), S("b"), S("a")), Invoke("sortDesc", Arr(S("a"), S("c"), S("b"))));
    }

    [Fact]
    public void SortDesc_Floats()
    {
        Assert.Equal(Arr(F(3.3), F(2.2), F(1.1)), Invoke("sortDesc", Arr(F(1.1), F(3.3), F(2.2))));
    }

    [Fact]
    public void SortDesc_Single()
    {
        Assert.Equal(Arr(I(5)), Invoke("sortDesc", Arr(I(5))));
    }

    [Fact]
    public void SortDesc_PreservesAllElements()
    {
        Assert.Equal(Arr(I(5), I(4), I(3), I(1), I(1)),
            Invoke("sortDesc", Arr(I(3), I(1), I(4), I(1), I(5))));
    }

    // =========================================================================
    // sortBy — extended
    // =========================================================================

    [Fact]
    public void SortBy_StringField()
    {
        var data = Arr(
            Obj(("name", S("Charlie"))),
            Obj(("name", S("Alice"))),
            Obj(("name", S("Bob")))
        );
        var result = Invoke("sortBy", data, S("name"));
        var arr = result.AsArray()!;
        Assert.Equal(S("Alice"), arr[0].Get("name"));
        Assert.Equal(S("Bob"), arr[1].Get("name"));
        Assert.Equal(S("Charlie"), arr[2].Get("name"));
    }

    [Fact]
    public void SortBy_EmptyArray()
    {
        Assert.Equal(Arr(), Invoke("sortBy", Arr(), S("x")));
    }

    [Fact]
    public void SortBy_MissingField()
    {
        var data = Arr(Obj(("a", I(2))), Obj(("b", I(1))));
        var result = Invoke("sortBy", data, S("a"));
        AssertArrayLength(result, 2);
    }

    // =========================================================================
    // map / pluck — extended
    // =========================================================================

    [Fact]
    public void Map_EmptyArray()
    {
        Assert.Equal(Arr(), Invoke("map", Arr(), S("x")));
    }

    [Fact]
    public void Map_AllMissingFields()
    {
        var data = Arr(Obj(("a", I(1))), Obj(("a", I(2))));
        Assert.Equal(Arr(Null(), Null()), Invoke("map", data, S("z")));
    }

    [Fact]
    public void Pluck_EmptyArray()
    {
        Assert.Equal(Arr(), Invoke("pluck", Arr(), S("x")));
    }

    [Fact]
    public void Pluck_ExtractsField()
    {
        var data = Arr(
            Obj(("name", S("A")), ("age", I(10))),
            Obj(("name", S("B")), ("age", I(20)))
        );
        Assert.Equal(Arr(I(10), I(20)), Invoke("pluck", data, S("age")));
    }

    [Fact]
    public void Pluck_MissingFieldGivesNull()
    {
        var data = Arr(Obj(("x", I(1))));
        Assert.Equal(Arr(Null()), Invoke("pluck", data, S("y")));
    }

    // =========================================================================
    // indexOf — extended
    // =========================================================================

    [Fact]
    public void IndexOf_FirstOccurrence()
    {
        Assert.Equal(I(0), Invoke("indexOf", Arr(I(1), I(2), I(1)), I(1)));
    }

    [Fact]
    public void IndexOf_EmptyArray()
    {
        Assert.Equal(I(-1), Invoke("indexOf", Arr(), I(1)));
    }

    [Fact]
    public void IndexOf_StringValue()
    {
        Assert.Equal(I(1), Invoke("indexOf", Arr(S("hello"), S("world")), S("world")));
    }

    // =========================================================================
    // at — extended
    // =========================================================================

    [Fact]
    public void At_FirstElement()
    {
        Assert.Equal(S("first"), Invoke("at", Arr(S("first"), S("second")), I(0)));
    }

    [Fact]
    public void At_LastElement()
    {
        Assert.Equal(I(3), Invoke("at", Arr(I(1), I(2), I(3)), I(2)));
    }

    [Fact]
    public void At_EmptyArray()
    {
        Assert.True(Invoke("at", Arr(), I(0)).IsNull);
    }

    [Fact]
    public void At_NegativeIndex()
    {
        Assert.Equal(I(3), Invoke("at", Arr(I(1), I(2), I(3)), I(-1)));
    }

    // =========================================================================
    // slice — extended
    // =========================================================================

    [Fact]
    public void Slice_FullArray()
    {
        Assert.Equal(Arr(I(1), I(2), I(3)), Invoke("slice", Arr(I(1), I(2), I(3)), I(0), I(3)));
    }

    [Fact]
    public void Slice_EmptyArray()
    {
        Assert.Equal(Arr(), Invoke("slice", Arr(), I(0), I(0)));
    }

    [Fact]
    public void Slice_SingleElement()
    {
        Assert.Equal(Arr(I(20)), Invoke("slice", Arr(I(10), I(20), I(30)), I(1), I(2)));
    }

    [Fact]
    public void Slice_StartEqualToEndPastLength()
    {
        Assert.Equal(Arr(), Invoke("slice", Arr(I(1)), I(5), I(10)));
    }

    [Fact]
    public void Slice_NegativeStart()
    {
        var result = Invoke("slice", Arr(I(1), I(2), I(3), I(4), I(5)), I(-2));
        AssertArrayLength(result, 2);
    }

    // =========================================================================
    // reverse — extended
    // =========================================================================

    [Fact]
    public void Reverse_Strings()
    {
        Assert.Equal(Arr(S("c"), S("b"), S("a")), Invoke("reverse", Arr(S("a"), S("b"), S("c"))));
    }

    [Fact]
    public void Reverse_TwoElements()
    {
        Assert.Equal(Arr(I(2), I(1)), Invoke("reverse", Arr(I(1), I(2))));
    }

    [Fact]
    public void Reverse_EmptyArray()
    {
        Assert.Equal(Arr(), Invoke("reverse", Arr()));
    }

    // =========================================================================
    // every — extended
    // =========================================================================

    [Fact]
    public void Every_AllTruthy()
    {
        Assert.Equal(B(true), Invoke("every", Arr(I(1), I(2), I(3))));
    }

    [Fact]
    public void Every_HasFalse()
    {
        Assert.Equal(B(false), Invoke("every", Arr(I(1), B(false), I(3))));
    }

    [Fact]
    public void Every_EmptyIsTrue()
    {
        Assert.Equal(B(true), Invoke("every", Arr()));
    }

    [Fact]
    public void Every_FieldTruthy()
    {
        var data = Arr(Obj(("v", I(10))), Obj(("v", I(20))));
        Assert.Equal(B(true), Invoke("every", data, S("v")));
    }

    [Fact]
    public void Every_FieldFalsy()
    {
        var data = Arr(Obj(("v", I(10))), Obj(("v", I(0))));
        Assert.Equal(B(false), Invoke("every", data, S("v")));
    }

    // =========================================================================
    // some — extended
    // =========================================================================

    [Fact]
    public void Some_AllTruthy()
    {
        Assert.Equal(B(true), Invoke("some", Arr(I(1), I(2))));
    }

    [Fact]
    public void Some_AllFalsy()
    {
        Assert.Equal(B(false), Invoke("some", Arr(I(0), B(false), Null())));
    }

    [Fact]
    public void Some_EmptyIsFalse()
    {
        Assert.Equal(B(false), Invoke("some", Arr()));
    }

    [Fact]
    public void Some_FieldTruthy()
    {
        var data = Arr(Obj(("v", I(0))), Obj(("v", I(10))));
        Assert.Equal(B(true), Invoke("some", data, S("v")));
    }

    // =========================================================================
    // find — extended
    // =========================================================================

    [Fact]
    public void Find_EmptyArray()
    {
        Assert.True(Invoke("find", Arr()).IsNull);
    }

    [Fact]
    public void Find_ReturnsFirstTruthy()
    {
        Assert.Equal(I(2), Invoke("find", Arr(I(0), I(2), I(3))));
    }

    [Fact]
    public void Find_ByField()
    {
        var data = Arr(Obj(("v", I(0))), Obj(("v", I(10))));
        var result = Invoke("find", data, S("v"));
        Assert.Equal(I(10), result.Get("v"));
    }

    // =========================================================================
    // findIndex — extended
    // =========================================================================

    [Fact]
    public void FindIndex_EmptyArray()
    {
        Assert.Equal(I(-1), Invoke("findIndex", Arr()));
    }

    [Fact]
    public void FindIndex_FirstTruthy()
    {
        Assert.Equal(I(1), Invoke("findIndex", Arr(I(0), I(5), I(10))));
    }

    // =========================================================================
    // includes — extended
    // =========================================================================

    [Fact]
    public void Includes_StringValue()
    {
        Assert.Equal(B(true), Invoke("includes", Arr(S("hello"), S("world")), S("hello")));
    }

    [Fact]
    public void Includes_IntegerPresent()
    {
        Assert.Equal(B(true), Invoke("includes", Arr(I(1), I(2), I(3)), I(2)));
    }

    [Fact]
    public void Includes_FloatPresent()
    {
        Assert.Equal(B(true), Invoke("includes", Arr(F(1.5), F(2.5)), F(2.5)));
    }

    [Fact]
    public void Includes_BoolAbsent()
    {
        Assert.Equal(B(false), Invoke("includes", Arr(B(true)), B(false)));
    }

    [Fact]
    public void Includes_NullInArray()
    {
        Assert.Equal(B(true), Invoke("includes", Arr(I(1), Null(), I(3)), Null()));
    }

    // =========================================================================
    // concatArrays — extended
    // =========================================================================

    [Fact]
    public void ConcatArrays_BothEmpty()
    {
        Assert.Equal(Arr(), Invoke("concatArrays", Arr(), Arr()));
    }

    [Fact]
    public void ConcatArrays_FirstEmpty()
    {
        Assert.Equal(Arr(I(1)), Invoke("concatArrays", Arr(), Arr(I(1))));
    }

    [Fact]
    public void ConcatArrays_MixedTypes()
    {
        var result = Invoke("concatArrays", Arr(I(1), S("two")), Arr(B(true), Null()));
        AssertArrayLength(result, 4);
    }

    [Fact]
    public void ConcatArrays_NestedArrays()
    {
        var result = Invoke("concatArrays", Arr(Arr(I(1))), Arr(Arr(I(2))));
        Assert.Equal(Arr(Arr(I(1)), Arr(I(2))), result);
    }

    // =========================================================================
    // zip — extended
    // =========================================================================

    [Fact]
    public void Zip_BothEmpty()
    {
        Assert.Equal(Arr(), Invoke("zip", Arr(), Arr()));
    }

    [Fact]
    public void Zip_SingleElements()
    {
        Assert.Equal(Arr(Arr(I(1), S("a"))), Invoke("zip", Arr(I(1)), Arr(S("a"))));
    }

    [Fact]
    public void Zip_MixedTypes()
    {
        var result = Invoke("zip", Arr(I(1), S("two")), Arr(B(true), Null()));
        Assert.Equal(Arr(Arr(I(1), B(true)), Arr(S("two"), Null())), result);
    }

    [Fact]
    public void Zip_UnequalLengthPadsWithNull()
    {
        var result = Invoke("zip", Arr(I(1), I(2), I(3)), Arr(S("a")));
        AssertArrayLength(result, 3);
        var arr = result.AsArray()!;
        Assert.Equal(Arr(I(1), S("a")), arr[0]);
    }

    // =========================================================================
    // groupBy — extended
    // =========================================================================

    [Fact]
    public void GroupBy_EmptyArray()
    {
        var result = Invoke("groupBy", Arr(), S("key"));
        var obj = result.AsObject();
        Assert.NotNull(obj);
        Assert.True(obj!.Count == 0);
    }

    [Fact]
    public void GroupBy_SingleGroup()
    {
        var data = Arr(
            Obj(("type", S("A")), ("v", I(1))),
            Obj(("type", S("A")), ("v", I(2)))
        );
        var result = Invoke("groupBy", data, S("type"));
        var obj = result.AsObject()!;
        Assert.True(obj.Count == 1);
        Assert.Equal("A", obj[0].Key);
    }

    [Fact]
    public void GroupBy_MissingFieldUsesNullKey()
    {
        var data = Arr(
            Obj(("v", I(1))),
            Obj(("type", S("A")), ("v", I(2)))
        );
        var result = Invoke("groupBy", data, S("type"));
        var obj = result.AsObject()!;
        Assert.Equal(2, obj.Count);
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
        var obj = result.AsObject()!;
        Assert.Equal(2, obj.Count);
    }

    [Fact]
    public void GroupBy_BoolField()
    {
        var data = Arr(
            Obj(("active", B(true)), ("name", S("A"))),
            Obj(("active", B(false)), ("name", S("B"))),
            Obj(("active", B(true)), ("name", S("C")))
        );
        var result = Invoke("groupBy", data, S("active"));
        var obj = result.AsObject()!;
        Assert.Equal(2, obj.Count);
    }

    [Fact]
    public void GroupBy_AllSameKey()
    {
        var data = Arr(
            Obj(("k", S("x")), ("v", I(1))),
            Obj(("k", S("x")), ("v", I(2)))
        );
        var result = Invoke("groupBy", data, S("k"));
        var obj = result.AsObject()!;
        Assert.True(obj.Count == 1);
        Assert.Equal("x", obj[0].Key);
    }

    // =========================================================================
    // partition — extended
    // =========================================================================

    [Fact]
    public void Partition_AllMatch()
    {
        var data = Arr(I(1), I(2), I(3));
        var result = Invoke("partition", data);
        var parts = result.AsArray()!;
        Assert.Equal(3, parts[0].AsArray()!.Count);
        Assert.Empty(parts[1].AsArray()!);
    }

    [Fact]
    public void Partition_NoneMatch()
    {
        var data = Arr(I(0), B(false), Null());
        var result = Invoke("partition", data);
        var parts = result.AsArray()!;
        Assert.Empty(parts[0].AsArray()!);
        Assert.Equal(3, parts[1].AsArray()!.Count);
    }

    [Fact]
    public void Partition_EmptyArray()
    {
        var result = Invoke("partition", Arr());
        var parts = result.AsArray()!;
        Assert.Empty(parts[0].AsArray()!);
        Assert.Empty(parts[1].AsArray()!);
    }

    [Fact]
    public void Partition_ByField()
    {
        var data = Arr(Obj(("v", I(10))), Obj(("v", I(0))));
        var result = Invoke("partition", data, S("v"));
        var parts = result.AsArray()!;
        Assert.Single(parts[0].AsArray()!);
        Assert.Single(parts[1].AsArray()!);
    }

    // =========================================================================
    // take — extended
    // =========================================================================

    [Fact]
    public void Take_Zero()
    {
        Assert.Equal(Arr(), Invoke("take", Arr(I(1), I(2), I(3)), I(0)));
    }

    [Fact]
    public void Take_EmptyArray()
    {
        Assert.Equal(Arr(), Invoke("take", Arr(), I(5)));
    }

    [Fact]
    public void Take_ExactLength()
    {
        Assert.Equal(Arr(I(1), I(2)), Invoke("take", Arr(I(1), I(2)), I(2)));
    }

    [Fact]
    public void Take_MoreThanLength()
    {
        Assert.Equal(Arr(I(1)), Invoke("take", Arr(I(1)), I(10)));
    }

    [Fact]
    public void Take_FromMixedTypes()
    {
        Assert.Equal(Arr(I(1), S("two"), B(true)),
            Invoke("take", Arr(I(1), S("two"), B(true), Null()), I(3)));
    }

    // =========================================================================
    // drop — extended
    // =========================================================================

    [Fact]
    public void Drop_Zero()
    {
        Assert.Equal(Arr(I(1), I(2), I(3)), Invoke("drop", Arr(I(1), I(2), I(3)), I(0)));
    }

    [Fact]
    public void Drop_EmptyArray()
    {
        Assert.Equal(Arr(), Invoke("drop", Arr(), I(5)));
    }

    [Fact]
    public void Drop_ExactLength()
    {
        Assert.Equal(Arr(), Invoke("drop", Arr(I(1), I(2)), I(2)));
    }

    [Fact]
    public void Drop_MoreThanLength()
    {
        Assert.Equal(Arr(), Invoke("drop", Arr(I(1), I(2)), I(10)));
    }

    [Fact]
    public void Drop_FromMixedTypes()
    {
        Assert.Equal(Arr(I(1), B(false)), Invoke("drop", Arr(S("a"), I(1), B(false)), I(1)));
    }

    // =========================================================================
    // chunk — extended
    // =========================================================================

    [Fact]
    public void Chunk_SingleElement()
    {
        Assert.Equal(Arr(Arr(I(1))), Invoke("chunk", Arr(I(1)), I(1)));
    }

    [Fact]
    public void Chunk_SizeLargerThanArray()
    {
        Assert.Equal(Arr(Arr(I(1), I(2))), Invoke("chunk", Arr(I(1), I(2)), I(10)));
    }

    [Fact]
    public void Chunk_EmptyArray()
    {
        Assert.Equal(Arr(), Invoke("chunk", Arr(), I(3)));
    }

    [Fact]
    public void Chunk_SizeOne()
    {
        Assert.Equal(Arr(Arr(I(1)), Arr(I(2)), Arr(I(3))),
            Invoke("chunk", Arr(I(1), I(2), I(3)), I(1)));
    }

    [Fact]
    public void Chunk_SizeEqualsLength()
    {
        Assert.Equal(Arr(Arr(I(1), I(2), I(3))),
            Invoke("chunk", Arr(I(1), I(2), I(3)), I(3)));
    }

    [Fact]
    public void Chunk_SizeTwoOddLength()
    {
        Assert.Equal(Arr(Arr(I(1), I(2)), Arr(I(3), I(4)), Arr(I(5))),
            Invoke("chunk", Arr(I(1), I(2), I(3), I(4), I(5)), I(2)));
    }

    // =========================================================================
    // range — extended
    // =========================================================================

    [Fact]
    public void Range_SingleElement()
    {
        Assert.Equal(Arr(I(0)), Invoke("range", I(0), I(1)));
    }

    [Fact]
    public void Range_NegativeValues()
    {
        Assert.Equal(Arr(I(-3), I(-2), I(-1)), Invoke("range", I(-3), I(0)));
    }

    [Fact]
    public void Range_SameStartEnd()
    {
        Assert.Equal(Arr(), Invoke("range", I(5), I(5)));
    }

    [Fact]
    public void Range_StepOfTwo()
    {
        Assert.Equal(Arr(I(0), I(2), I(4)), Invoke("range", I(0), I(6), I(2)));
    }

    [Fact]
    public void Range_LargeStep()
    {
        Assert.Equal(Arr(I(0), I(5)), Invoke("range", I(0), I(10), I(5)));
    }

    [Fact]
    public void Range_Descending()
    {
        Assert.Equal(Arr(I(5), I(4), I(3), I(2), I(1)), Invoke("range", I(5), I(0), I(-1)));
    }

    [Fact]
    public void Range_StepThree()
    {
        Assert.Equal(Arr(I(1), I(4), I(7)), Invoke("range", I(1), I(10), I(3)));
    }

    // =========================================================================
    // compact — extended
    // =========================================================================

    [Fact]
    public void Compact_NoNulls()
    {
        Assert.Equal(Arr(I(1), I(2), I(3)), Invoke("compact", Arr(I(1), I(2), I(3))));
    }

    [Fact]
    public void Compact_EmptyArray()
    {
        Assert.Equal(Arr(), Invoke("compact", Arr()));
    }

    [Fact]
    public void Compact_OnlyEmptyStrings()
    {
        Assert.Equal(Arr(), Invoke("compact", Arr(S(""), S(""), S(""))));
    }

    [Fact]
    public void Compact_KeepsNonEmptyStrings()
    {
        Assert.Equal(Arr(S("hello"), S("world")),
            Invoke("compact", Arr(S(""), S("hello"), Null(), S("world"))));
    }

    [Fact]
    public void Compact_KeepsZerosAndFalse()
    {
        var result = Invoke("compact", Arr(I(0), B(false), Null(), S("")));
        var arr = result.AsArray()!;
        Assert.Equal(2, arr.Count); // 0 and false are kept
    }

    [Fact]
    public void Compact_KeepsFalseAndZero()
    {
        Assert.Equal(Arr(B(false), I(0), S("ok")),
            Invoke("compact", Arr(B(false), I(0), Null(), S(""), S("ok"))));
    }

    [Fact]
    public void Compact_AllValid()
    {
        Assert.Equal(Arr(I(1), S("a"), B(true)),
            Invoke("compact", Arr(I(1), S("a"), B(true))));
    }

    // =========================================================================
    // dedupe — extended
    // =========================================================================

    [Fact]
    public void Dedupe_EmptyArray()
    {
        Assert.Equal(Arr(), Invoke("dedupe", Arr()));
    }

    [Fact]
    public void Dedupe_NoDuplicates()
    {
        Assert.Equal(Arr(I(1), I(2), I(3)), Invoke("dedupe", Arr(I(1), I(2), I(3))));
    }

    [Fact]
    public void Dedupe_ConsecutiveDuplicates()
    {
        Assert.Equal(Arr(I(1), I(2), I(3)), Invoke("dedupe", Arr(I(1), I(1), I(2), I(2), I(3))));
    }

    [Fact]
    public void Dedupe_NonConsecutiveDuplicatesKept()
    {
        // dedupe only removes consecutive duplicates
        Assert.Equal(Arr(I(1), I(2), I(1)), Invoke("dedupe", Arr(I(1), I(2), I(1))));
    }

    // =========================================================================
    // cumsum — extended
    // =========================================================================

    [Fact]
    public void Cumsum_SingleElement()
    {
        Assert.Equal(Arr(I(5)), Invoke("cumsum", Arr(I(5))));
    }

    [Fact]
    public void Cumsum_Floats()
    {
        var result = Invoke("cumsum", Arr(F(1.5), F(2.5), F(3.0)));
        var arr = result.AsArray()!;
        Assert.Equal(F(1.5), arr[0]);
        // 1.5 + 2.5 = 4.0 -> Integer(4)
        Assert.Equal(I(4), arr[1]);
        // 4.0 + 3.0 = 7.0 -> Integer(7)
        Assert.Equal(I(7), arr[2]);
    }

    [Fact]
    public void Cumsum_NegativeNumbers()
    {
        Assert.Equal(Arr(I(5), I(2), I(4)), Invoke("cumsum", Arr(I(5), I(-3), I(2))));
    }

    [Fact]
    public void Cumsum_AllNulls()
    {
        Assert.Equal(Arr(Null(), Null()), Invoke("cumsum", Arr(Null(), Null())));
    }

    [Fact]
    public void Cumsum_MixedNullInt()
    {
        var result = Invoke("cumsum", Arr(I(1), Null(), I(3)));
        var arr = result.AsArray()!;
        Assert.Equal(I(1), arr[0]);
        Assert.True(arr[1].IsNull);
        Assert.Equal(I(4), arr[2]); // 1+3=4
    }

    // =========================================================================
    // cumprod — extended
    // =========================================================================

    [Fact]
    public void Cumprod_SingleElement()
    {
        Assert.Equal(Arr(I(5)), Invoke("cumprod", Arr(I(5))));
    }

    [Fact]
    public void Cumprod_Empty()
    {
        Assert.Equal(Arr(), Invoke("cumprod", Arr()));
    }

    [Fact]
    public void Cumprod_Floats()
    {
        var result = Invoke("cumprod", Arr(F(2.0), F(3.0), F(4.0)));
        var arr = result.AsArray()!;
        Assert.Equal(I(2), arr[0]);
        Assert.Equal(I(6), arr[1]);
        Assert.Equal(I(24), arr[2]);
    }

    [Fact]
    public void Cumprod_WithOnes()
    {
        Assert.Equal(Arr(I(1), I(1), I(1)), Invoke("cumprod", Arr(I(1), I(1), I(1))));
    }

    [Fact]
    public void Cumprod_WithNegative()
    {
        var result = Invoke("cumprod", Arr(I(2), I(-3)));
        var arr = result.AsArray()!;
        Assert.Equal(I(2), arr[0]);
        Assert.Equal(I(-6), arr[1]);
    }

    // =========================================================================
    // diff — extended
    // =========================================================================

    [Fact]
    public void Diff_EmptyArray()
    {
        Assert.Equal(Arr(), Invoke("diff", Arr()));
    }

    [Fact]
    public void Diff_SingleElement()
    {
        Assert.Equal(Arr(Null()), Invoke("diff", Arr(I(5))));
    }

    [Fact]
    public void Diff_Floats()
    {
        var result = Invoke("diff", Arr(F(1.0), F(3.0), F(6.0)));
        var arr = result.AsArray()!;
        Assert.True(arr[0].IsNull);
        Assert.Equal(I(2), arr[1]);
        Assert.Equal(I(3), arr[2]);
    }

    [Fact]
    public void Diff_Integers()
    {
        var result = Invoke("diff", Arr(I(10), I(20), I(50)));
        var arr = result.AsArray()!;
        Assert.True(arr[0].IsNull);
        Assert.Equal(I(10), arr[1]);
        Assert.Equal(I(30), arr[2]);
    }

    // =========================================================================
    // pctChange — extended
    // =========================================================================

    [Fact]
    public void PctChange_Empty()
    {
        Assert.Equal(Arr(), Invoke("pctChange", Arr()));
    }

    [Fact]
    public void PctChange_Single()
    {
        Assert.Equal(Arr(Null()), Invoke("pctChange", Arr(I(100))));
    }

    [Fact]
    public void PctChange_Doubling()
    {
        var result = Invoke("pctChange", Arr(I(100), I(200)));
        var arr = result.AsArray()!;
        Assert.True(arr[0].IsNull);
        Assert.True(Math.Abs(arr[1].AsDouble()!.Value - 1.0) < 1e-10);
    }

    [Fact]
    public void PctChange_WithZeroPrevious()
    {
        var result = Invoke("pctChange", Arr(I(0), I(100)));
        var arr = result.AsArray()!;
        Assert.True(arr[1].IsNull); // Division by zero -> null
    }

    [Fact]
    public void PctChange_Decrease()
    {
        var result = Invoke("pctChange", Arr(I(100), I(75)));
        var arr = result.AsArray()!;
        Assert.True(Math.Abs(arr[1].AsDouble()!.Value - (-0.25)) < 1e-10);
    }

    // =========================================================================
    // shift — extended
    // =========================================================================

    [Fact]
    public void Shift_ZeroNoChange()
    {
        Assert.Equal(Arr(I(1), I(2), I(3)), Invoke("shift", Arr(I(1), I(2), I(3)), I(0)));
    }

    [Fact]
    public void Shift_RightByOne()
    {
        Assert.Equal(Arr(Null(), I(1), I(2)), Invoke("shift", Arr(I(1), I(2), I(3)), I(1)));
    }

    [Fact]
    public void Shift_LeftByTwo()
    {
        Assert.Equal(Arr(I(3), I(4), Null(), Null()),
            Invoke("shift", Arr(I(1), I(2), I(3), I(4)), I(-2)));
    }

    // =========================================================================
    // lag — extended
    // =========================================================================

    [Fact]
    public void Lag_DefaultPeriodOne()
    {
        Assert.Equal(Arr(Null(), I(10), I(20)),
            Invoke("lag", Arr(I(10), I(20), I(30))));
    }

    [Fact]
    public void Lag_PeriodTwo()
    {
        Assert.Equal(Arr(Null(), Null(), I(10), I(20)),
            Invoke("lag", Arr(I(10), I(20), I(30), I(40)), I(2)));
    }

    [Fact]
    public void Lag_PeriodThree()
    {
        Assert.Equal(Arr(Null(), Null(), Null(), I(10), I(20)),
            Invoke("lag", Arr(I(10), I(20), I(30), I(40), I(50)), I(3)));
    }

    [Fact]
    public void Lag_EmptyArray()
    {
        Assert.Equal(Arr(), Invoke("lag", Arr()));
    }

    [Fact]
    public void Lag_SingleElement()
    {
        Assert.Equal(Arr(Null()), Invoke("lag", Arr(I(42))));
    }

    // =========================================================================
    // lead — extended
    // =========================================================================

    [Fact]
    public void Lead_DefaultPeriodOne()
    {
        Assert.Equal(Arr(I(20), I(30), Null()),
            Invoke("lead", Arr(I(10), I(20), I(30))));
    }

    [Fact]
    public void Lead_PeriodTwo()
    {
        Assert.Equal(Arr(I(30), I(40), Null(), Null()),
            Invoke("lead", Arr(I(10), I(20), I(30), I(40)), I(2)));
    }

    [Fact]
    public void Lead_EmptyArray()
    {
        Assert.Equal(Arr(), Invoke("lead", Arr()));
    }

    [Fact]
    public void Lead_SingleElement()
    {
        Assert.Equal(Arr(Null()), Invoke("lead", Arr(I(42))));
    }

    // =========================================================================
    // rank — extended
    // =========================================================================

    [Fact]
    public void Rank_BasicDescending()
    {
        // rank returns Integer values; highest = rank 1 (descending by default)
        var result = Invoke("rank", Arr(I(10), I(30), I(20)));
        var arr = result.AsArray()!;
        Assert.Equal(3, arr.Count);
        // 10 -> rank 3, 30 -> rank 1, 20 -> rank 2
        Assert.Equal(I(3), arr[0]);
        Assert.Equal(I(1), arr[1]);
        Assert.Equal(I(2), arr[2]);
    }

    [Fact]
    public void Rank_TiedValues()
    {
        var result = Invoke("rank", Arr(I(10), I(10), I(30)));
        var arr = result.AsArray()!;
        // Both 10s should have same rank
        Assert.Equal(arr[0], arr[1]);
    }

    [Fact]
    public void Rank_SingleElement()
    {
        var result = Invoke("rank", Arr(I(42)));
        var arr = result.AsArray()!;
        Assert.Equal(I(1), arr[0]);
    }

    [Fact]
    public void Rank_AllSameValue()
    {
        var result = Invoke("rank", Arr(I(5), I(5), I(5)));
        var arr = result.AsArray()!;
        Assert.Equal(I(1), arr[0]);
        Assert.Equal(I(1), arr[1]);
        Assert.Equal(I(1), arr[2]);
    }

    [Fact]
    public void Rank_Empty()
    {
        Assert.Equal(Arr(), Invoke("rank", Arr()));
    }

    // =========================================================================
    // fillMissing — extended
    // =========================================================================

    [Fact]
    public void FillMissing_NoNulls()
    {
        Assert.Equal(Arr(I(1), I(2), I(3)),
            Invoke("fillMissing", Arr(I(1), I(2), I(3)), I(0)));
    }

    [Fact]
    public void FillMissing_AllNullsValue()
    {
        Assert.Equal(Arr(I(99), I(99)),
            Invoke("fillMissing", Arr(Null(), Null()), I(99)));
    }

    [Fact]
    public void FillMissing_ForwardStrategy()
    {
        var result = Invoke("fillMissing", Arr(I(1), Null(), Null(), I(4), Null()), S("forward"));
        Assert.Equal(Arr(I(1), I(1), I(1), I(4), I(4)), result);
    }

    [Fact]
    public void FillMissing_BackwardStrategy()
    {
        var result = Invoke("fillMissing", Arr(Null(), Null(), I(3), Null(), I(5)), S("backward"));
        Assert.Equal(Arr(I(3), I(3), I(3), I(5), I(5)), result);
    }

    [Fact]
    public void FillMissing_EmptyArray()
    {
        Assert.Equal(Arr(), Invoke("fillMissing", Arr(), I(0)));
    }

    // =========================================================================
    // sample — extended
    // =========================================================================

    [Fact]
    public void Sample_ZeroCount()
    {
        Assert.Equal(Arr(), Invoke("sample", Arr(I(1), I(2), I(3)), I(0)));
    }

    [Fact]
    public void Sample_MoreThanLength()
    {
        var result = Invoke("sample", Arr(I(1), I(2)), I(10));
        AssertArrayLength(result, 2);
    }

    [Fact]
    public void Sample_EmptyArray()
    {
        Assert.Equal(Arr(), Invoke("sample", Arr(), I(5)));
    }

    [Fact]
    public void Sample_OneElement()
    {
        Assert.Equal(Arr(I(42)), Invoke("sample", Arr(I(42)), I(1)));
    }

    // =========================================================================
    // limit — extended (alias for take)
    // =========================================================================

    [Fact]
    public void Limit_Zero()
    {
        Assert.Equal(Arr(), Invoke("limit", Arr(I(1), I(2), I(3)), I(0)));
    }

    [Fact]
    public void Limit_ExactLength()
    {
        Assert.Equal(Arr(I(1), I(2)), Invoke("limit", Arr(I(1), I(2)), I(2)));
    }

    [Fact]
    public void Limit_Basic()
    {
        Assert.Equal(Arr(I(1), I(2)), Invoke("limit", Arr(I(1), I(2), I(3), I(4)), I(2)));
    }

    [Fact]
    public void Limit_MoreThanLength()
    {
        Assert.Equal(Arr(I(1)), Invoke("limit", Arr(I(1)), I(10)));
    }

    // =========================================================================
    // keys — extended
    // =========================================================================

    [Fact]
    public void Keys_EmptyObject()
    {
        Assert.Equal(Arr(), Invoke("keys", Obj()));
    }

    [Fact]
    public void Keys_SingleKey()
    {
        Assert.Equal(Arr(S("only")), Invoke("keys", Obj(("only", I(1)))));
    }

    [Fact]
    public void Keys_PreservesOrder()
    {
        Assert.Equal(Arr(S("z"), S("a"), S("m")),
            Invoke("keys", Obj(("z", I(1)), ("a", I(2)), ("m", I(3)))));
    }

    [Fact]
    public void Keys_MultipleKeys()
    {
        Assert.Equal(Arr(S("a"), S("b"), S("c")),
            Invoke("keys", Obj(("a", I(1)), ("b", I(2)), ("c", I(3)))));
    }

    // =========================================================================
    // values — extended
    // =========================================================================

    [Fact]
    public void Values_EmptyObject()
    {
        Assert.Equal(Arr(), Invoke("values", Obj()));
    }

    [Fact]
    public void Values_MixedTypes()
    {
        var result = Invoke("values", Obj(("a", I(1)), ("b", S("two")), ("c", B(true))));
        AssertArrayLength(result, 3);
    }

    [Fact]
    public void Values_MatchesOrder()
    {
        Assert.Equal(Arr(I(1), S("two"), B(true)),
            Invoke("values", Obj(("a", I(1)), ("b", S("two")), ("c", B(true)))));
    }

    // =========================================================================
    // entries — extended
    // =========================================================================

    [Fact]
    public void Entries_EmptyObject()
    {
        Assert.Equal(Arr(), Invoke("entries", Obj()));
    }

    [Fact]
    public void Entries_MultiplePairs()
    {
        var result = Invoke("entries", Obj(("a", I(1)), ("b", I(2))));
        var arr = result.AsArray()!;
        Assert.Equal(2, arr.Count);
        Assert.Equal(Arr(S("a"), I(1)), arr[0]);
        Assert.Equal(Arr(S("b"), I(2)), arr[1]);
    }

    [Fact]
    public void Entries_SinglePair()
    {
        Assert.Equal(Arr(Arr(S("key"), I(42))),
            Invoke("entries", Obj(("key", I(42)))));
    }

    [Fact]
    public void Entries_PreservesOrder()
    {
        var result = Invoke("entries", Obj(("z", I(1)), ("a", I(2))));
        var arr = result.AsArray()!;
        Assert.Equal(Arr(S("z"), I(1)), arr[0]);
        Assert.Equal(Arr(S("a"), I(2)), arr[1]);
    }

    // =========================================================================
    // has — extended
    // =========================================================================

    [Fact]
    public void Has_EmptyObject()
    {
        Assert.Equal(B(false), Invoke("has", Obj(), S("anything")));
    }

    [Fact]
    public void Has_WithNullValue()
    {
        Assert.Equal(B(true), Invoke("has", Obj(("key", Null())), S("key")));
    }

    [Fact]
    public void Has_MissingKey()
    {
        Assert.Equal(B(false), Invoke("has", Obj(("a", I(1))), S("b")));
    }

    [Fact]
    public void Has_KeyPresent()
    {
        Assert.Equal(B(true), Invoke("has", Obj(("x", I(99))), S("x")));
    }

    // =========================================================================
    // get — extended
    // =========================================================================

    [Fact]
    public void Get_TopLevel()
    {
        Assert.Equal(I(99), Invoke("get", Obj(("x", I(99))), S("x")));
    }

    [Fact]
    public void Get_MissingKey()
    {
        Assert.True(Invoke("get", Obj(("a", I(1))), S("b")).IsNull);
    }

    [Fact]
    public void Get_NullValue()
    {
        Assert.True(Invoke("get", Obj(("key", Null())), S("key")).IsNull);
    }

    // =========================================================================
    // merge — extended
    // =========================================================================

    [Fact]
    public void Merge_BothEmpty()
    {
        var result = Invoke("merge", Obj(), Obj());
        Assert.True(result.AsObject()!.Count == 0);
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
        var obj = result.AsObject()!;
        Assert.True(obj.Count == 1);
        Assert.Equal(I(2), obj[0].Value);
    }

    [Fact]
    public void Merge_SecondOverwritesFirst()
    {
        var result = Invoke("merge", Obj(("x", I(1)), ("y", I(2))), Obj(("y", I(99)), ("z", I(3))));
        var obj = result.AsObject()!;
        Assert.Equal(3, obj.Count);
    }

    [Fact]
    public void Merge_DisjointObjects()
    {
        var result = Invoke("merge", Obj(("a", I(1))), Obj(("b", I(2))));
        Assert.Equal(2, result.AsObject()!.Count);
    }

    // =========================================================================
    // Aggregation verbs — extended
    // =========================================================================

    [Fact]
    public void Sum_SingleElement()
    {
        Assert.Equal(I(42), Invoke("sum", Arr(I(42))));
    }

    [Fact]
    public void Sum_NegativeNumbers()
    {
        Assert.Equal(I(-6), Invoke("sum", Arr(I(-1), I(-2), I(-3))));
    }

    [Fact]
    public void Sum_WithNonNumericIgnored()
    {
        Assert.Equal(I(3), Invoke("sum", Arr(I(1), S("hello"), I(2), B(true))));
    }

    [Fact]
    public void Sum_LargeFloats()
    {
        var result = Invoke("sum", Arr(F(1e10), F(2e10)));
        Assert.True(Math.Abs(result.AsDouble()!.Value - 3e10) < 1e5);
    }

    [Fact]
    public void Count_WithNulls()
    {
        Assert.Equal(I(3), Invoke("count", Arr(Null(), Null(), I(1))));
    }

    [Fact]
    public void Count_SingleElement()
    {
        Assert.Equal(I(1), Invoke("count", Arr(I(42))));
    }

    [Fact]
    public void Count_Empty()
    {
        Assert.Equal(I(0), Invoke("count", Arr()));
    }

    [Fact]
    public void Min_SingleElement()
    {
        Assert.Equal(I(42), Invoke("min", Arr(I(42))));
    }

    [Fact]
    public void Min_NegativeNumbers()
    {
        Assert.Equal(I(-5), Invoke("min", Arr(I(-1), I(-5), I(-2))));
    }

    [Fact]
    public void Min_MixedIntFloat()
    {
        Assert.Equal(F(2.5), Invoke("min", Arr(I(5), F(2.5), I(3))));
    }

    [Fact]
    public void Max_SingleElement()
    {
        Assert.Equal(I(42), Invoke("max", Arr(I(42))));
    }

    [Fact]
    public void Max_NegativeNumbers()
    {
        Assert.Equal(I(-1), Invoke("max", Arr(I(-1), I(-5), I(-2))));
    }

    [Fact]
    public void Max_MixedIntFloat()
    {
        Assert.Equal(F(7.5), Invoke("max", Arr(I(5), F(7.5), I(3))));
    }

    [Fact]
    public void Max_Floats()
    {
        Assert.Equal(F(9.9), Invoke("max", Arr(F(1.1), F(9.9), F(5.5))));
    }

    [Fact]
    public void Avg_Floats()
    {
        Assert.Equal(F(2.0), Invoke("avg", Arr(F(1.0), F(2.0), F(3.0))));
    }

    [Fact]
    public void Avg_MixedIntFloat()
    {
        Assert.Equal(F(2.0), Invoke("avg", Arr(I(1), F(2.0), I(3))));
    }

    [Fact]
    public void Avg_WithNonNumericSkipped()
    {
        Assert.Equal(F(15.0), Invoke("avg", Arr(I(10), S("abc"), I(20))));
    }

    [Fact]
    public void First_SingleElement()
    {
        Assert.Equal(I(42), Invoke("first", Arr(I(42))));
    }

    [Fact]
    public void First_NullElement()
    {
        Assert.True(Invoke("first", Arr(Null(), I(1))).IsNull);
    }

    [Fact]
    public void First_EmptyArray()
    {
        Assert.True(Invoke("first", Arr()).IsNull);
    }

    [Fact]
    public void Last_SingleElement()
    {
        Assert.Equal(I(42), Invoke("last", Arr(I(42))));
    }

    [Fact]
    public void Last_NullAtEnd()
    {
        Assert.True(Invoke("last", Arr(I(1), Null())).IsNull);
    }

    [Fact]
    public void Last_EmptyArray()
    {
        Assert.True(Invoke("last", Arr()).IsNull);
    }

    // =========================================================================
    // Accumulator verbs — extended
    // =========================================================================

    [Fact]
    public void Accumulate_SumOp()
    {
        var ctx = new VerbContext();
        var reg = new VerbRegistry();
        reg.Invoke("accumulate", new[] { S("total"), S("sum"), I(10) }, ctx);
        var result = reg.Invoke("accumulate", new[] { S("total"), S("sum"), I(5) }, ctx);
        Assert.Equal(I(15), result);
    }

    [Fact]
    public void Accumulate_CountOp()
    {
        var ctx = new VerbContext();
        var reg = new VerbRegistry();
        reg.Invoke("accumulate", new[] { S("cnt"), S("count"), I(0) }, ctx);
        reg.Invoke("accumulate", new[] { S("cnt"), S("count"), I(0) }, ctx);
        var result = reg.Invoke("accumulate", new[] { S("cnt"), S("count"), I(0) }, ctx);
        Assert.Equal(I(3), result);
    }

    [Fact]
    public void Accumulate_MinOp()
    {
        var ctx = new VerbContext();
        var reg = new VerbRegistry();
        reg.Invoke("accumulate", new[] { S("m"), S("min"), I(10) }, ctx);
        reg.Invoke("accumulate", new[] { S("m"), S("min"), I(5) }, ctx);
        var result = reg.Invoke("accumulate", new[] { S("m"), S("min"), I(20) }, ctx);
        Assert.Equal(I(5), result);
    }

    [Fact]
    public void Accumulate_MaxOp()
    {
        var ctx = new VerbContext();
        var reg = new VerbRegistry();
        reg.Invoke("accumulate", new[] { S("m"), S("max"), I(10) }, ctx);
        reg.Invoke("accumulate", new[] { S("m"), S("max"), I(5) }, ctx);
        var result = reg.Invoke("accumulate", new[] { S("m"), S("max"), I(20) }, ctx);
        Assert.Equal(I(20), result);
    }

    [Fact]
    public void Accumulate_ConcatOp()
    {
        var ctx = new VerbContext();
        var reg = new VerbRegistry();
        reg.Invoke("accumulate", new[] { S("msg"), S("concat"), S("hello") }, ctx);
        var result = reg.Invoke("accumulate", new[] { S("msg"), S("concat"), S(" world") }, ctx);
        Assert.Equal(S("hello world"), result);
    }

    [Fact]
    public void Accumulate_FirstOp()
    {
        var ctx = new VerbContext();
        var reg = new VerbRegistry();
        reg.Invoke("accumulate", new[] { S("f"), S("first"), S("first") }, ctx);
        var result = reg.Invoke("accumulate", new[] { S("f"), S("first"), S("second") }, ctx);
        Assert.Equal(S("first"), result);
    }

    [Fact]
    public void Accumulate_LastOp()
    {
        var ctx = new VerbContext();
        var reg = new VerbRegistry();
        reg.Invoke("accumulate", new[] { S("l"), S("last"), S("first") }, ctx);
        var result = reg.Invoke("accumulate", new[] { S("l"), S("last"), S("second") }, ctx);
        Assert.Equal(S("second"), result);
    }

    [Fact]
    public void Set_StringValue()
    {
        var ctx = new VerbContext();
        var reg = new VerbRegistry();
        Assert.Equal(S("hello"), reg.Invoke("set", new[] { S("name"), S("hello") }, ctx));
    }

    [Fact]
    public void Set_NullValue()
    {
        var ctx = new VerbContext();
        var reg = new VerbRegistry();
        Assert.True(reg.Invoke("set", new[] { S("name"), Null() }, ctx).IsNull);
    }

    [Fact]
    public void Set_IntegerValue()
    {
        var ctx = new VerbContext();
        var reg = new VerbRegistry();
        Assert.Equal(I(42), reg.Invoke("set", new[] { S("counter"), I(42) }, ctx));
    }

    [Fact]
    public void Set_BoolValue()
    {
        var ctx = new VerbContext();
        var reg = new VerbRegistry();
        Assert.Equal(B(true), reg.Invoke("set", new[] { S("flag"), B(true) }, ctx));
    }

    // =========================================================================
    // Nested array operations (composition)
    // =========================================================================

    [Fact]
    public void Nested_SortThenTake()
    {
        var sorted = Invoke("sort", Arr(I(5), I(1), I(3), I(2), I(4)));
        Assert.Equal(Arr(I(1), I(2), I(3)), Invoke("take", sorted, I(3)));
    }

    [Fact]
    public void Nested_FlattenThenDistinct()
    {
        var flat = Invoke("flatten", Arr(Arr(I(1), I(2)), Arr(I(2), I(3))));
        Assert.Equal(Arr(I(1), I(2), I(3)), Invoke("distinct", flat));
    }

    [Fact]
    public void Nested_ConcatThenSort()
    {
        var combined = Invoke("concatArrays", Arr(I(3), I(1)), Arr(I(4), I(2)));
        Assert.Equal(Arr(I(1), I(2), I(3), I(4)), Invoke("sort", combined));
    }

    [Fact]
    public void Nested_ReverseThenFirst()
    {
        var reversed = Invoke("reverse", Arr(I(1), I(2), I(3)));
        Assert.Equal(I(3), Invoke("first", reversed));
    }

    [Fact]
    public void Nested_ChunkThenFirst()
    {
        var chunks = Invoke("chunk", Arr(I(1), I(2), I(3), I(4)), I(2));
        Assert.Equal(Arr(I(1), I(2)), Invoke("first", chunks));
    }

    [Fact]
    public void Nested_DropThenCount()
    {
        var dropped = Invoke("drop", Arr(I(1), I(2), I(3), I(4), I(5)), I(2));
        Assert.Equal(I(3), Invoke("count", dropped));
    }

    [Fact]
    public void Nested_CompactThenSum()
    {
        var compacted = Invoke("compact", Arr(I(1), Null(), I(2), S(""), I(3)));
        Assert.Equal(I(6), Invoke("sum", compacted));
    }

    // =========================================================================
    // rowNumber — extended
    // =========================================================================

    [Fact]
    public void RowNumber_Increments()
    {
        var ctx = new VerbContext();
        var reg = new VerbRegistry();
        Assert.Equal(I(1), reg.Invoke("rowNumber", Array.Empty<DynValue>(), ctx));
        Assert.Equal(I(2), reg.Invoke("rowNumber", Array.Empty<DynValue>(), ctx));
        Assert.Equal(I(3), reg.Invoke("rowNumber", Array.Empty<DynValue>(), ctx));
    }

    // =========================================================================
    // filter — basic truthiness (no operator)
    // =========================================================================

    [Fact]
    public void Filter_TruthyValues()
    {
        Assert.Equal(Arr(I(1), I(2), I(3)),
            Invoke("filter", Arr(I(0), I(1), Null(), I(2), S(""), I(3))));
    }

    [Fact]
    public void Filter_EmptyArray()
    {
        Assert.Equal(Arr(), Invoke("filter", Arr()));
    }

    [Fact]
    public void Filter_ByFieldTruthiness()
    {
        var data = Arr(Obj(("active", B(true))), Obj(("active", B(false))));
        var result = Invoke("filter", data, S("active"));
        AssertArrayLength(result, 1);
    }
}
