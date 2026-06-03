using System.Collections.Generic;
using Odin.Core.Types;
using Odin.Core.Transform;
using Odin.Core.Transform.Verbs;
using Xunit;

namespace Odin.Core.Tests.Unit;

/// <summary>
/// Tests for object reshaping verbs (pick, omit, fromEntries, invert, defaults,
/// renameKeys, compactObject).
/// </summary>
public class ObjectVerbTests
{
    private readonly VerbRegistry _registry = new VerbRegistry();
    private readonly VerbContext _ctx = new VerbContext();

    private DynValue Invoke(string verb, params DynValue[] args)
        => _registry.Invoke(verb, args, _ctx);

    private static DynValue S(string v) => DynValue.String(v);
    private static DynValue I(long v) => DynValue.Integer(v);
    private static DynValue B(bool v) => DynValue.Bool(v);
    private static DynValue Null() => DynValue.Null();
    private static DynValue Arr(params DynValue[] items) => DynValue.Array(new List<DynValue>(items));
    private static DynValue Obj(params (string key, DynValue value)[] pairs)
    {
        var list = new List<KeyValuePair<string, DynValue>>();
        foreach (var (k, v) in pairs) list.Add(new KeyValuePair<string, DynValue>(k, v));
        return DynValue.Object(list);
    }

    private static DynValue Rec() => Obj(("name", S("Ada")), ("role", S("admin")), ("active", B(true)));

    // =========================================================================
    // pick
    // =========================================================================

    [Fact]
    public void Pick_KeepsNamedKeys()
    {
        var result = Invoke("pick", Rec(), S("name"), S("role"));
        var obj = result.AsObject()!;
        Assert.Equal(2, obj.Count);
        Assert.Equal("name", obj[0].Key);
        Assert.Equal("Ada", obj[0].Value.AsString());
        Assert.Equal("role", obj[1].Key);
    }

    [Fact]
    public void Pick_SkipsAbsentKeys()
    {
        var result = Invoke("pick", Rec(), S("name"), S("zzz"));
        var obj = result.AsObject()!;
        Assert.Single(obj);
        Assert.Equal("name", obj[0].Key);
    }

    [Fact]
    public void Pick_NonObjectIsNull()
        => Assert.True(Invoke("pick", S("x"), S("name")).IsNull);

    [Fact]
    public void Pick_NoArgs()
        => Assert.True(Invoke("pick").IsNull);

    [Fact]
    public void Pick_NoKeysGivesEmptyObject()
        => Assert.Empty(Invoke("pick", Rec()).AsObject()!);

    // =========================================================================
    // omit
    // =========================================================================

    [Fact]
    public void Omit_DropsNamedKeys()
    {
        var result = Invoke("omit", Rec(), S("active"));
        var obj = result.AsObject()!;
        Assert.Equal(2, obj.Count);
        Assert.Equal("name", obj[0].Key);
        Assert.Equal("role", obj[1].Key);
    }

    [Fact]
    public void Omit_AbsentKeyKeepsAll()
    {
        var result = Invoke("omit", Rec(), S("zzz"));
        Assert.Equal(3, result.AsObject()!.Count);
    }

    [Fact]
    public void Omit_PreservesSourceOrder()
    {
        var result = Invoke("omit", Rec(), S("name"));
        var obj = result.AsObject()!;
        Assert.Equal("role", obj[0].Key);
        Assert.Equal("active", obj[1].Key);
    }

    [Fact]
    public void Omit_NonObjectIsNull()
        => Assert.True(Invoke("omit", S("x"), S("name")).IsNull);

    [Fact]
    public void Omit_NoArgs()
        => Assert.True(Invoke("omit").IsNull);

    // =========================================================================
    // fromEntries
    // =========================================================================

    [Fact]
    public void FromEntries_BuildsObject()
    {
        var pairs = Arr(Arr(S("name"), S("Ada")), Arr(S("role"), S("admin")));
        var result = Invoke("fromEntries", pairs);
        var obj = result.AsObject()!;
        Assert.Equal(2, obj.Count);
        Assert.Equal("Ada", result.Get("name")!.AsString());
        Assert.Equal("admin", result.Get("role")!.AsString());
    }

    [Fact]
    public void FromEntries_LastWinsOnDuplicateKey()
    {
        var pairs = Arr(Arr(S("k"), S("first")), Arr(S("k"), S("second")));
        var result = Invoke("fromEntries", pairs);
        Assert.Single(result.AsObject()!);
        Assert.Equal("second", result.Get("k")!.AsString());
    }

    [Fact]
    public void FromEntries_SkipsMalformedPairs()
    {
        var pairs = Arr(Arr(S("ok"), I(1)), Arr(S("lonely")));
        var result = Invoke("fromEntries", pairs);
        Assert.Single(result.AsObject()!);
        Assert.Equal(1, result.Get("ok")!.AsInt64());
    }

    [Fact]
    public void FromEntries_NonArrayIsNull()
        => Assert.True(Invoke("fromEntries", S("x")).IsNull);

    [Fact]
    public void FromEntries_EmptyArray()
        => Assert.Empty(Invoke("fromEntries", Arr()).AsObject()!);

    [Fact]
    public void FromEntries_RoundTripsEntries()
    {
        var entries = Invoke("entries", Rec());
        var result = Invoke("fromEntries", entries);
        Assert.Equal(3, result.AsObject()!.Count);
        Assert.Equal("Ada", result.Get("name")!.AsString());
    }

    // =========================================================================
    // invert
    // =========================================================================

    [Fact]
    public void Invert_SwapsKeysAndValues()
    {
        var result = Invoke("invert", Obj(("a", S("x")), ("b", S("y"))));
        Assert.Equal("a", result.Get("x")!.AsString());
        Assert.Equal("b", result.Get("y")!.AsString());
    }

    [Fact]
    public void Invert_DuplicateValueLastKeyWins()
    {
        var result = Invoke("invert", Obj(("a", S("same")), ("b", S("same"))));
        Assert.Single(result.AsObject()!);
        Assert.Equal("b", result.Get("same")!.AsString());
    }

    [Fact]
    public void Invert_CoercesNonStringValues()
    {
        var result = Invoke("invert", Obj(("count", I(42))));
        Assert.Equal("count", result.Get("42")!.AsString());
    }

    [Fact]
    public void Invert_NonObjectIsNull()
        => Assert.True(Invoke("invert", S("x")).IsNull);

    [Fact]
    public void Invert_NoArgs()
        => Assert.True(Invoke("invert").IsNull);

    // =========================================================================
    // defaults
    // =========================================================================

    [Fact]
    public void Defaults_FillsOnlyMissingKeys()
    {
        var rec = Obj(("name", S("Ada")));
        var fallback = Obj(("name", S("Anon")), ("role", S("guest")));
        var result = Invoke("defaults", rec, fallback);
        Assert.Equal("Ada", result.Get("name")!.AsString());
        Assert.Equal("guest", result.Get("role")!.AsString());
    }

    [Fact]
    public void Defaults_NonObjectBaseReturnsDefaults()
    {
        var fallback = Obj(("name", S("Anon")), ("role", S("guest")));
        var result = Invoke("defaults", S("x"), fallback);
        Assert.Equal("Anon", result.Get("name")!.AsString());
        Assert.Equal("guest", result.Get("role")!.AsString());
    }

    [Fact]
    public void Defaults_PreservesBaseOrderThenNewKeys()
    {
        var rec = Obj(("name", S("Ada")));
        var fallback = Obj(("role", S("guest")));
        var obj = Invoke("defaults", rec, fallback).AsObject()!;
        Assert.Equal("name", obj[0].Key);
        Assert.Equal("role", obj[1].Key);
    }

    [Fact]
    public void Defaults_TooFewArgs()
        => Assert.True(Invoke("defaults", Obj(("a", I(1)))).IsNull);

    // =========================================================================
    // renameKeys
    // =========================================================================

    [Fact]
    public void RenameKeys_RenamesMappedKeys()
    {
        var rec = Obj(("fn", S("Ada")), ("keep", S("as-is")));
        var mapping = Obj(("fn", S("firstName")));
        var result = Invoke("renameKeys", rec, mapping);
        var obj = result.AsObject()!;
        Assert.Equal("firstName", obj[0].Key);
        Assert.Equal("Ada", obj[0].Value.AsString());
        Assert.Equal("keep", obj[1].Key);
    }

    [Fact]
    public void RenameKeys_PassesThroughUnmappedKeys()
    {
        var rec = Obj(("a", I(1)), ("b", I(2)));
        var result = Invoke("renameKeys", rec, Obj(("a", S("alpha"))));
        Assert.Equal("alpha", result.AsObject()![0].Key);
        Assert.Equal("b", result.AsObject()![1].Key);
    }

    [Fact]
    public void RenameKeys_NonObjectIsNull()
        => Assert.True(Invoke("renameKeys", S("x"), Obj(("a", S("b")))).IsNull);

    [Fact]
    public void RenameKeys_TooFewArgs()
        => Assert.True(Invoke("renameKeys", Obj(("a", I(1)))).IsNull);

    // =========================================================================
    // compactObject
    // =========================================================================

    [Fact]
    public void CompactObject_DropsEmptyValues()
    {
        var rec = Obj(
            ("name", S("Ada")),
            ("middle", Null()),
            ("nickname", S("")),
            ("zero", I(0)),
            ("flag", B(false)));
        var result = Invoke("compactObject", rec);
        var obj = result.AsObject()!;
        Assert.Equal(3, obj.Count);
        Assert.Equal("name", obj[0].Key);
        Assert.Equal("zero", obj[1].Key);
        Assert.Equal("flag", obj[2].Key);
    }

    [Fact]
    public void CompactObject_DropsEmptyArrayAndObject()
    {
        var rec = Obj(("arr", Arr()), ("obj", Obj()), ("keep", I(1)));
        var result = Invoke("compactObject", rec);
        Assert.Single(result.AsObject()!);
        Assert.Equal("keep", result.AsObject()![0].Key);
    }

    [Fact]
    public void CompactObject_NonObjectIsNull()
        => Assert.True(Invoke("compactObject", S("x")).IsNull);

    [Fact]
    public void CompactObject_NoArgs()
        => Assert.True(Invoke("compactObject").IsNull);
}
