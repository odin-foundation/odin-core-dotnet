using System.Collections.Generic;
using Odin.Core.Types;
using Odin.Core.Transform;
using Odin.Core.Transform.Verbs;
using Xunit;

namespace Odin.Core.Tests.Unit;

/// <summary>
/// Tests for set-operation, indexing, expansion, windowing, and conditional
/// aggregation array verbs (intersection, union, difference, symmetricDifference,
/// countBy, keyBy, explode, window, countIf, sumIf, avgIf).
/// </summary>
public class CollectionVerbNewTests
{
    private readonly VerbRegistry _registry = new VerbRegistry();
    private readonly VerbContext _ctx = new VerbContext();

    private DynValue Invoke(string verb, params DynValue[] args)
        => _registry.Invoke(verb, args, _ctx);

    private static DynValue S(string v) => DynValue.String(v);
    private static DynValue I(long v) => DynValue.Integer(v);
    private static DynValue Null() => DynValue.Null();
    private static DynValue Arr(params DynValue[] items) => DynValue.Array(new List<DynValue>(items));
    private static DynValue Obj(params (string key, DynValue value)[] pairs)
    {
        var list = new List<KeyValuePair<string, DynValue>>();
        foreach (var (k, v) in pairs) list.Add(new KeyValuePair<string, DynValue>(k, v));
        return DynValue.Object(list);
    }

    private static DynValue Orders() => Arr(
        Obj(("status", S("paid")), ("amount", I(100))),
        Obj(("status", S("open")), ("amount", I(200))),
        Obj(("status", S("paid")), ("amount", I(300))));

    private static long[] Ints(DynValue v)
    {
        var arr = v.AsArray()!;
        var result = new long[arr.Count];
        for (int i = 0; i < arr.Count; i++) result[i] = arr[i].AsInt64()!.Value;
        return result;
    }

    // =========================================================================
    // intersection
    // =========================================================================

    [Fact]
    public void Intersection_DistinctInBoth_OrderOfA()
        => Assert.Equal(new long[] { 2, 3 },
            Ints(Invoke("intersection", Arr(I(1), I(2), I(2), I(3)), Arr(I(2), I(3), I(4)))));

    [Fact]
    public void Intersection_NoOverlap()
        => Assert.Empty(Invoke("intersection", Arr(I(1), I(2)), Arr(I(9))).AsArray()!);

    [Fact]
    public void Intersection_TooFewArgs()
        => Assert.Empty(Invoke("intersection", Arr(I(1))).AsArray()!);

    // =========================================================================
    // union
    // =========================================================================

    [Fact]
    public void Union_DistinctAFirstThenNewFromB()
        => Assert.Equal(new long[] { 1, 2, 3 },
            Ints(Invoke("union", Arr(I(1), I(2), I(2)), Arr(I(2), I(3)))));

    [Fact]
    public void Union_Disjoint()
        => Assert.Equal(new long[] { 1, 2, 3, 4 },
            Ints(Invoke("union", Arr(I(1), I(2)), Arr(I(3), I(4)))));

    [Fact]
    public void Union_EmptyFirstYieldsDistinctSecond()
        => Assert.Equal(new long[] { 2, 3 },
            Ints(Invoke("union", Arr(), Arr(I(2), I(3), I(3)))));

    // =========================================================================
    // difference
    // =========================================================================

    [Fact]
    public void Difference_OnlyInA_Distinct()
        => Assert.Equal(new long[] { 1 },
            Ints(Invoke("difference", Arr(I(1), I(1), I(2), I(3)), Arr(I(2), I(3), I(4)))));

    [Fact]
    public void Difference_NoOverlapKeepsAllDistinct()
        => Assert.Equal(new long[] { 1, 2, 3 },
            Ints(Invoke("difference", Arr(I(1), I(2), I(3)), Arr(I(9), I(8)))));

    // =========================================================================
    // symmetricDifference
    // =========================================================================

    [Fact]
    public void SymmetricDifference_AOnlyThenBOnly()
        => Assert.Equal(new long[] { 1, 4 },
            Ints(Invoke("symmetricDifference", Arr(I(1), I(2), I(3)), Arr(I(2), I(3), I(4)))));

    [Fact]
    public void SymmetricDifference_DisjointReturnsAll()
        => Assert.Equal(new long[] { 1, 2, 3, 4 },
            Ints(Invoke("symmetricDifference", Arr(I(1), I(2)), Arr(I(3), I(4)))));

    [Fact]
    public void SymmetricDifference_DedupesInputs()
        => Assert.Equal(new long[] { 1, 3 },
            Ints(Invoke("symmetricDifference", Arr(I(1), I(1), I(2)), Arr(I(2), I(3)))));

    // =========================================================================
    // countBy
    // =========================================================================

    [Fact]
    public void CountBy_Field_SortedKeys()
    {
        var items = Arr(
            Obj(("region", S("east"))),
            Obj(("region", S("west"))),
            Obj(("region", S("east"))));
        var result = Invoke("countBy", items, S("region"));
        var obj = result.AsObject()!;
        Assert.Equal("east", obj[0].Key);
        Assert.Equal(2, obj[0].Value.AsInt64());
        Assert.Equal("west", obj[1].Key);
        Assert.Equal(1, obj[1].Value.AsInt64());
    }

    [Fact]
    public void CountBy_NoField_CountsValues()
    {
        var result = Invoke("countBy", Arr(S("a"), S("b"), S("a"), S("a")));
        Assert.Equal(3, result.Get("a")!.AsInt64());
        Assert.Equal(1, result.Get("b")!.AsInt64());
    }

    [Fact]
    public void CountBy_NonArrayIsNull()
        => Assert.True(Invoke("countBy", S("x")).IsNull);

    // =========================================================================
    // keyBy
    // =========================================================================

    [Fact]
    public void KeyBy_IndexesByField_LastWins()
    {
        var users = Arr(
            Obj(("id", S("u1")), ("name", S("Ada"))),
            Obj(("id", S("u2")), ("name", S("Bo"))),
            Obj(("id", S("u1")), ("name", S("Ada2"))));
        var result = Invoke("keyBy", users, S("id"));
        Assert.Equal(2, result.AsObject()!.Count);
        Assert.Equal("Ada2", result.Get("u1")!.Get("name")!.AsString());
        Assert.Equal("Bo", result.Get("u2")!.Get("name")!.AsString());
    }

    [Fact]
    public void KeyBy_NonArrayIsNull()
        => Assert.True(Invoke("keyBy", S("x"), S("id")).IsNull);

    [Fact]
    public void KeyBy_TooFewArgs()
        => Assert.True(Invoke("keyBy", Arr()).IsNull);

    // =========================================================================
    // explode
    // =========================================================================

    [Fact]
    public void Explode_OneRowPerElement()
    {
        var orders = Arr(
            Obj(("id", S("o1")), ("tags", Arr(S("red"), S("blue")))));
        var result = Invoke("explode", orders, S("tags")).AsArray()!;
        Assert.Equal(2, result.Count);
        Assert.Equal("o1", result[0].Get("id")!.AsString());
        Assert.Equal("red", result[0].Get("tags")!.AsString());
        Assert.Equal("blue", result[1].Get("tags")!.AsString());
    }

    [Fact]
    public void Explode_EmptyArrayFieldEmitsRowUnchanged()
    {
        var orders = Arr(Obj(("id", S("o2")), ("tags", Arr())));
        var result = Invoke("explode", orders, S("tags")).AsArray()!;
        Assert.Single(result);
        Assert.Equal("o2", result[0].Get("id")!.AsString());
    }

    [Fact]
    public void Explode_MissingFieldEmitsRowOnce()
    {
        var plain = Arr(Obj(("id", S("p1"))), Obj(("id", S("p2"))));
        var result = Invoke("explode", plain, S("tags")).AsArray()!;
        Assert.Equal(2, result.Count);
        Assert.Equal("p1", result[0].Get("id")!.AsString());
        Assert.Equal("p2", result[1].Get("id")!.AsString());
    }

    [Fact]
    public void Explode_TooFewArgs()
        => Assert.Empty(Invoke("explode", Arr()).AsArray()!);

    // =========================================================================
    // window
    // =========================================================================

    [Fact]
    public void Window_Pairs()
    {
        var result = Invoke("window", Arr(I(1), I(2), I(3)), I(2)).AsArray()!;
        Assert.Equal(2, result.Count);
        Assert.Equal(new long[] { 1, 2 }, Ints(result[0]));
        Assert.Equal(new long[] { 2, 3 }, Ints(result[1]));
    }

    [Fact]
    public void Window_SizeOne()
        => Assert.Equal(3, Invoke("window", Arr(I(1), I(2), I(3)), I(1)).AsArray()!.Count);

    [Fact]
    public void Window_LargerThanArrayIsEmpty()
        => Assert.Empty(Invoke("window", Arr(I(1), I(2)), I(5)).AsArray()!);

    [Fact]
    public void Window_ZeroIsEmpty()
        => Assert.Empty(Invoke("window", Arr(I(1), I(2)), I(0)).AsArray()!);

    // =========================================================================
    // countIf
    // =========================================================================

    [Fact]
    public void CountIf_Matches()
        => Assert.Equal(2, Invoke("countIf", Orders(), S("status"), S("="), S("paid")).AsInt64());

    [Fact]
    public void CountIf_NoMatchIsZero()
        => Assert.Equal(0, Invoke("countIf", Orders(), S("status"), S("="), S("void")).AsInt64());

    [Fact]
    public void CountIf_TooFewArgsIsZero()
        => Assert.Equal(0, Invoke("countIf", Orders(), S("status")).AsInt64());

    // =========================================================================
    // sumIf
    // =========================================================================

    [Fact]
    public void SumIf_SumsNamedField()
        => Assert.Equal(400, Invoke("sumIf", Orders(), S("status"), S("="), S("paid"), S("amount")).AsInt64());

    [Fact]
    public void SumIf_NoMatchIsZero()
        => Assert.Equal(0, Invoke("sumIf", Orders(), S("status"), S("="), S("void"), S("amount")).AsInt64());

    [Fact]
    public void SumIf_TooFewArgsIsZero()
        => Assert.Equal(0, Invoke("sumIf", Orders(), S("status"), S("=")).AsInt64());

    // =========================================================================
    // avgIf
    // =========================================================================

    [Fact]
    public void AvgIf_AveragesNamedField()
        => Assert.Equal(200.0, Invoke("avgIf", Orders(), S("status"), S("="), S("paid"), S("amount")).AsDouble());

    [Fact]
    public void AvgIf_NoMatchIsNull()
        => Assert.True(Invoke("avgIf", Orders(), S("status"), S("="), S("void"), S("amount")).IsNull);

    [Fact]
    public void AvgIf_TooFewArgsIsNull()
        => Assert.True(Invoke("avgIf", Orders(), S("status")).IsNull);
}
