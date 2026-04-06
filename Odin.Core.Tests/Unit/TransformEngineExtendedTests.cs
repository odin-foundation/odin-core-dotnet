using System.Collections.Generic;
using System.Linq;
using Odin.Core;
using Odin.Core.Transform;
using Odin.Core.Types;
using Xunit;

namespace Odin.Core.Tests.Unit;

public class TransformEngineExtendedTests
{
    // Ensure the Odin static constructor runs (wires VerbRegistry, SourceParser)
    static TransformEngineExtendedTests()
    {
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(global::Odin.Core.Odin).TypeHandle);
    }

    // ─────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────

    private static OdinTransform MkTransform(List<FieldMapping> mappings)
    {
        return new OdinTransform
        {
            Target = new TargetConfig { Format = "json" },
            Segments = new List<TransformSegment>
            {
                new TransformSegment { Mappings = mappings }
            }
        };
    }

    private static OdinTransform MkCustom(List<TransformSegment> segments)
    {
        return new OdinTransform
        {
            Target = new TargetConfig { Format = "json" },
            Segments = segments
        };
    }

    private static TransformSegment RootSeg(List<FieldMapping> mappings)
        => new TransformSegment { Mappings = mappings };

    private static TransformSegment NamedSeg(string name, List<FieldMapping> mappings)
        => new TransformSegment { Name = name, Path = name, Mappings = mappings };

    private static TransformSegment PassSeg(int pass, List<FieldMapping> mappings)
        => new TransformSegment { Pass = pass, Mappings = mappings };

    private static FieldMapping CopyField(string target, string src)
        => new FieldMapping { Target = target, Expression = FieldExpression.Copy(src) };

    private static FieldMapping LitField(string target, OdinValue val)
        => new FieldMapping { Target = target, Expression = FieldExpression.Literal(val) };

    private static FieldMapping VerbField(string target, string verb, List<VerbArg> args)
        => new FieldMapping
        {
            Target = target,
            Expression = FieldExpression.Transform(new VerbCall { Verb = verb, Args = args })
        };

    private static FieldMapping ModField(string target, string src, OdinModifiers mods)
        => new FieldMapping
        {
            Target = target,
            Expression = FieldExpression.Copy(src),
            Modifiers = mods
        };

    private static VerbArg RefArg(string path) => VerbArg.Ref(path);
    private static VerbArg LitStr(string s) => VerbArg.Lit(new OdinString(s));
    private static VerbArg LitInt(long n) => VerbArg.Lit(new OdinInteger(n));
    private static VerbArg LitBool(bool b) => VerbArg.Lit(new OdinBoolean(b));
    private static VerbArg LitNull() => VerbArg.Lit(new OdinNull());
    private static VerbArg Nested(string verb, List<VerbArg> args)
        => VerbArg.NestedCall(new VerbCall { Verb = verb, Args = args });

    private static DynValue Obj(params (string k, DynValue v)[] entries)
    {
        var list = new List<KeyValuePair<string, DynValue>>();
        foreach (var (k, v) in entries)
            list.Add(new KeyValuePair<string, DynValue>(k, v));
        return DynValue.Object(list);
    }

    private static DynValue Arr(params DynValue[] items)
        => DynValue.Array(new List<DynValue>(items));

    private static DynValue S(string s) => DynValue.String(s);
    private static DynValue I(long i) => DynValue.Integer(i);
    private static DynValue F(double f) => DynValue.Float(f);
    private static DynValue B(bool b) => DynValue.Bool(b);
    private static DynValue N() => DynValue.Null();

    // =========================================================================
    // 1. Strict type checking / verb-on-wrong-type (~30 tests)
    // =========================================================================

    [Fact]
    public void Ext_UpperOnIntegerErrors()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "upper", new List<VerbArg> { RefArg("@.val") }) });
        var r = TransformEngine.Execute(t, Obj(("val", I(42))));
        Assert.False(r.Success);
        Assert.NotEmpty(r.Errors);
    }

    [Fact]
    public void Ext_LowerOnIntegerErrors()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "lower", new List<VerbArg> { RefArg("@.val") }) });
        var r = TransformEngine.Execute(t, Obj(("val", I(99))));
        Assert.False(r.Success);
        Assert.NotEmpty(r.Errors);
    }

    [Fact]
    public void Ext_UpperOnBooleanErrors()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "upper", new List<VerbArg> { RefArg("@.val") }) });
        var r = TransformEngine.Execute(t, Obj(("val", B(true))));
        Assert.False(r.Success);
    }

    [Fact]
    public void Ext_LowerOnBooleanErrors()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "lower", new List<VerbArg> { RefArg("@.val") }) });
        var r = TransformEngine.Execute(t, Obj(("val", B(false))));
        Assert.False(r.Success);
    }

    [Fact]
    public void Ext_AddIntegers()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "add", new List<VerbArg> { RefArg("@.a"), RefArg("@.b") }) });
        var r = TransformEngine.Execute(t, Obj(("a", I(10)), ("b", I(20))));
        Assert.True(r.Success);
        var val = r.Output!.Get("Out");
        Assert.True(val!.AsInt64() == 30 || val.AsDouble() == 30.0);
    }

    [Fact]
    public void Ext_AddFloats()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "add", new List<VerbArg> { RefArg("@.a"), RefArg("@.b") }) });
        var r = TransformEngine.Execute(t, Obj(("a", F(1.5)), ("b", F(2.5))));
        Assert.True(r.Success);
        Assert.True(System.Math.Abs(r.Output!.Get("Out")!.AsDouble().GetValueOrDefault() - 4.0) < 0.001);
    }

    [Fact]
    public void Ext_SubtractIntegers()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "subtract", new List<VerbArg> { RefArg("@.a"), RefArg("@.b") }) });
        var r = TransformEngine.Execute(t, Obj(("a", I(50)), ("b", I(30))));
        Assert.True(r.Success);
        var val = r.Output!.Get("Out");
        Assert.True(val!.AsInt64() == 20 || val.AsDouble() == 20.0);
    }

    [Fact]
    public void Ext_MultiplyIntegers()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "multiply", new List<VerbArg> { RefArg("@.a"), RefArg("@.b") }) });
        var r = TransformEngine.Execute(t, Obj(("a", I(6)), ("b", I(7))));
        Assert.True(r.Success);
        var val = r.Output!.Get("Out");
        Assert.True(val!.AsInt64() == 42 || val.AsDouble() == 42.0);
    }

    [Fact]
    public void Ext_DivideIntegers()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "divide", new List<VerbArg> { RefArg("@.a"), RefArg("@.b") }) });
        var r = TransformEngine.Execute(t, Obj(("a", I(10)), ("b", I(3))));
        Assert.True(r.Success);
        Assert.NotNull(r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_AbsNegative()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "abs", new List<VerbArg> { RefArg("@.val") }) });
        var r = TransformEngine.Execute(t, Obj(("val", I(-42))));
        Assert.True(r.Success);
        var val = r.Output!.Get("Out");
        Assert.True(val!.AsInt64() == 42 || val.AsDouble() == 42.0);
    }

    [Fact]
    public void Ext_AbsPositiveUnchanged()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "abs", new List<VerbArg> { RefArg("@.val") }) });
        var r = TransformEngine.Execute(t, Obj(("val", I(42))));
        Assert.True(r.Success);
        var val = r.Output!.Get("Out");
        Assert.True(val!.AsInt64() == 42 || val.AsDouble() == 42.0);
    }

    [Fact]
    public void Ext_AbsFloat()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "abs", new List<VerbArg> { RefArg("@.val") }) });
        var r = TransformEngine.Execute(t, Obj(("val", F(-3.14))));
        Assert.True(r.Success);
        Assert.True(System.Math.Abs(r.Output!.Get("Out")!.AsDouble().GetValueOrDefault() - 3.14) < 0.001);
    }

    [Fact]
    public void Ext_RoundFloat()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "round", new List<VerbArg> { RefArg("@.val") }) });
        var r = TransformEngine.Execute(t, Obj(("val", F(3.7))));
        Assert.True(r.Success);
        var val = r.Output!.Get("Out");
        Assert.True(val!.AsInt64() == 4 || val.AsDouble() == 4.0);
    }

    [Fact]
    public void Ext_RoundNegative()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "round", new List<VerbArg> { RefArg("@.val") }) });
        var r = TransformEngine.Execute(t, Obj(("val", F(-2.3))));
        Assert.True(r.Success);
        var val = r.Output!.Get("Out");
        Assert.True(val!.AsInt64() == -2 || val.AsDouble() == -2.0);
    }

    [Fact]
    public void Ext_CoerceStringFromInt()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "coerceString", new List<VerbArg> { RefArg("@.val") }) });
        var r = TransformEngine.Execute(t, Obj(("val", I(42))));
        Assert.True(r.Success);
        Assert.Equal(S("42"), r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_CoerceStringFromBool()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "coerceString", new List<VerbArg> { RefArg("@.val") }) });
        var r = TransformEngine.Execute(t, Obj(("val", B(true))));
        Assert.True(r.Success);
        Assert.Equal(S("true"), r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_CoerceStringFromFloat()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "coerceString", new List<VerbArg> { RefArg("@.val") }) });
        var r = TransformEngine.Execute(t, Obj(("val", F(3.14))));
        Assert.True(r.Success);
        Assert.NotNull(r.Output!.Get("Out")!.AsString());
    }

    [Fact]
    public void Ext_CoerceNumberFromString()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "coerceNumber", new List<VerbArg> { RefArg("@.val") }) });
        var r = TransformEngine.Execute(t, Obj(("val", S("42"))));
        Assert.True(r.Success);
        var val = r.Output!.Get("Out");
        Assert.True(val!.AsDouble() == 42.0 || val.AsInt64() == 42);
    }

    [Fact]
    public void Ext_CoerceNumberFromFloatString()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "coerceNumber", new List<VerbArg> { RefArg("@.val") }) });
        var r = TransformEngine.Execute(t, Obj(("val", S("3.14"))));
        Assert.True(r.Success);
        Assert.True(System.Math.Abs(r.Output!.Get("Out")!.AsDouble().GetValueOrDefault() - 3.14) < 0.001);
    }

    [Fact]
    public void Ext_CoerceBooleanFromStringTrue()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "coerceBoolean", new List<VerbArg> { RefArg("@.val") }) });
        var r = TransformEngine.Execute(t, Obj(("val", S("true"))));
        Assert.True(r.Success);
        Assert.Equal(B(true), r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_CoerceBooleanFromStringFalse()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "coerceBoolean", new List<VerbArg> { RefArg("@.val") }) });
        var r = TransformEngine.Execute(t, Obj(("val", S("false"))));
        Assert.True(r.Success);
        Assert.Equal(B(false), r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_CoerceBooleanFromInt1()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "coerceBoolean", new List<VerbArg> { RefArg("@.val") }) });
        var r = TransformEngine.Execute(t, Obj(("val", I(1))));
        Assert.True(r.Success);
        Assert.Equal(B(true), r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_CoerceBooleanFromInt0()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "coerceBoolean", new List<VerbArg> { RefArg("@.val") }) });
        var r = TransformEngine.Execute(t, Obj(("val", I(0))));
        Assert.True(r.Success);
        Assert.Equal(B(false), r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_CoerceIntegerFromFloat()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "coerceInteger", new List<VerbArg> { RefArg("@.val") }) });
        var r = TransformEngine.Execute(t, Obj(("val", F(3.9))));
        Assert.True(r.Success);
        Assert.NotNull(r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_CoerceIntegerFromString()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "coerceInteger", new List<VerbArg> { RefArg("@.val") }) });
        var r = TransformEngine.Execute(t, Obj(("val", S("99"))));
        Assert.True(r.Success);
        Assert.Equal(99L, r.Output!.Get("Out")!.AsInt64());
    }

    [Fact]
    public void Ext_TrimOnNumberErrors()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "trim", new List<VerbArg> { RefArg("@.val") }) });
        var r = TransformEngine.Execute(t, Obj(("val", I(42))));
        Assert.False(r.Success);
        Assert.NotEmpty(r.Errors);
    }

    [Fact]
    public void Ext_CapitalizeString()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "capitalize", new List<VerbArg> { RefArg("@.val") }) });
        var r = TransformEngine.Execute(t, Obj(("val", S("hello world"))));
        Assert.True(r.Success);
        Assert.StartsWith("H", r.Output!.Get("Out")!.AsString()!);
    }

    [Fact]
    public void Ext_LengthString()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "length", new List<VerbArg> { RefArg("@.val") }) });
        var r = TransformEngine.Execute(t, Obj(("val", S("hello"))));
        Assert.True(r.Success);
        Assert.Equal(5L, r.Output!.Get("Out")!.AsInt64());
    }

    [Fact]
    public void Ext_LengthArray()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "length", new List<VerbArg> { RefArg("@.val") }) });
        var r = TransformEngine.Execute(t, Obj(("val", Arr(I(1), I(2), I(3)))));
        Assert.True(r.Success);
        Assert.Equal(3L, r.Output!.Get("Out")!.AsInt64());
    }

    [Fact]
    public void Ext_SubstringBasic()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "substring", new List<VerbArg> { RefArg("@.val"), LitInt(0), LitInt(3) }) });
        var r = TransformEngine.Execute(t, Obj(("val", S("hello"))));
        Assert.True(r.Success);
        Assert.Equal(S("hel"), r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_ReplaceBasic()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "replace", new List<VerbArg> { RefArg("@.val"), LitStr("world"), LitStr("earth") }) });
        var r = TransformEngine.Execute(t, Obj(("val", S("hello world"))));
        Assert.True(r.Success);
        Assert.Equal(S("hello earth"), r.Output!.Get("Out"));
    }

    // =========================================================================
    // 2. Conditional operators (~25 tests)
    // =========================================================================

    [Fact]
    public void Ext_IfElseTrueReturnsThen()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "ifElse", new List<VerbArg> { LitBool(true), LitStr("yes"), LitStr("no") }) });
        var r = TransformEngine.Execute(t, Obj());
        Assert.True(r.Success);
        Assert.Equal(S("yes"), r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_IfElseFalseReturnsElse()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "ifElse", new List<VerbArg> { LitBool(false), LitStr("yes"), LitStr("no") }) });
        var r = TransformEngine.Execute(t, Obj());
        Assert.True(r.Success);
        Assert.Equal(S("no"), r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_IfElseRefConditionTrue()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "ifElse", new List<VerbArg> { RefArg("@.active"), LitStr("active"), LitStr("inactive") }) });
        var r = TransformEngine.Execute(t, Obj(("active", B(true))));
        Assert.True(r.Success);
        Assert.Equal(S("active"), r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_IfElseRefConditionFalse()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "ifElse", new List<VerbArg> { RefArg("@.active"), LitStr("active"), LitStr("inactive") }) });
        var r = TransformEngine.Execute(t, Obj(("active", B(false))));
        Assert.True(r.Success);
        Assert.Equal(S("inactive"), r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_IfElseNullIsFalsy()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "ifElse", new List<VerbArg> { RefArg("@.missing"), LitStr("found"), LitStr("missing") }) });
        var r = TransformEngine.Execute(t, Obj());
        Assert.True(r.Success);
        Assert.Equal(S("missing"), r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_IfElseZeroIsFalsy()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "ifElse", new List<VerbArg> { RefArg("@.val"), LitStr("nonzero"), LitStr("zero") }) });
        var r = TransformEngine.Execute(t, Obj(("val", I(0))));
        Assert.True(r.Success);
        Assert.Equal(S("zero"), r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_IfElseNonEmptyStringTruthy()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "ifElse", new List<VerbArg> { RefArg("@.val"), LitStr("truthy"), LitStr("falsy") }) });
        var r = TransformEngine.Execute(t, Obj(("val", S("hello"))));
        Assert.True(r.Success);
        Assert.Equal(S("truthy"), r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_IfElseEmptyStringFalsy()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "ifElse", new List<VerbArg> { RefArg("@.val"), LitStr("truthy"), LitStr("falsy") }) });
        var r = TransformEngine.Execute(t, Obj(("val", S(""))));
        Assert.True(r.Success);
        Assert.Equal(S("falsy"), r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_IfNullWithNull()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "ifNull", new List<VerbArg> { RefArg("@.missing"), LitStr("default") }) });
        var r = TransformEngine.Execute(t, Obj());
        Assert.True(r.Success);
        Assert.Equal(S("default"), r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_IfNullWithValue()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "ifNull", new List<VerbArg> { RefArg("@.val"), LitStr("default") }) });
        var r = TransformEngine.Execute(t, Obj(("val", S("present"))));
        Assert.True(r.Success);
        Assert.Equal(S("present"), r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_IfEmptyWithEmptyString()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "ifEmpty", new List<VerbArg> { RefArg("@.val"), LitStr("was_empty") }) });
        var r = TransformEngine.Execute(t, Obj(("val", S(""))));
        Assert.True(r.Success);
        Assert.Equal(S("was_empty"), r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_IfEmptyWithNonEmpty()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "ifEmpty", new List<VerbArg> { RefArg("@.val"), LitStr("was_empty") }) });
        var r = TransformEngine.Execute(t, Obj(("val", S("content"))));
        Assert.True(r.Success);
        Assert.Equal(S("content"), r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_CoalesceFirstNonNull()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "coalesce", new List<VerbArg> { RefArg("@.a"), RefArg("@.b"), RefArg("@.c") }) });
        var r = TransformEngine.Execute(t, Obj(("c", S("third"))));
        Assert.True(r.Success);
        Assert.Equal(S("third"), r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_CoalesceAllNull()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "coalesce", new List<VerbArg> { RefArg("@.a"), RefArg("@.b") }) });
        var r = TransformEngine.Execute(t, Obj());
        Assert.True(r.Success);
        Assert.True(r.Output!.Get("Out")!.IsNull);
    }

    [Fact]
    public void Ext_CoalesceFirstPresent()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "coalesce", new List<VerbArg> { RefArg("@.a"), RefArg("@.b") }) });
        var r = TransformEngine.Execute(t, Obj(("a", S("first")), ("b", S("second"))));
        Assert.True(r.Success);
        Assert.Equal(S("first"), r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_CondFirstMatch()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "cond", new List<VerbArg> { LitBool(true), LitStr("A"), LitBool(false), LitStr("B") }) });
        var r = TransformEngine.Execute(t, Obj());
        Assert.True(r.Success);
        Assert.Equal(S("A"), r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_CondSecondMatch()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "cond", new List<VerbArg> { LitBool(false), LitStr("A"), LitBool(true), LitStr("B") }) });
        var r = TransformEngine.Execute(t, Obj());
        Assert.True(r.Success);
        Assert.Equal(S("B"), r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_CondNoMatchReturnsNull()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "cond", new List<VerbArg> { LitBool(false), LitStr("A"), LitBool(false), LitStr("B") }) });
        var r = TransformEngine.Execute(t, Obj());
        Assert.True(r.Success);
        Assert.True(r.Output!.Get("Out")!.IsNull);
    }

    [Fact]
    public void Ext_CondWithDefault()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "cond", new List<VerbArg> { LitBool(false), LitStr("A"), LitStr("default") }) });
        var r = TransformEngine.Execute(t, Obj());
        Assert.True(r.Success);
        Assert.Equal(S("default"), r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_IfElseWithVerbInThen()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "ifElse", new List<VerbArg> { LitBool(true), Nested("upper", new List<VerbArg> { LitStr("hello") }), LitStr("no") }) });
        var r = TransformEngine.Execute(t, Obj());
        Assert.True(r.Success);
        Assert.Equal(S("HELLO"), r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_IfElseWithVerbInElse()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "ifElse", new List<VerbArg> { LitBool(false), LitStr("yes"), Nested("lower", new List<VerbArg> { LitStr("WORLD") }) }) });
        var r = TransformEngine.Execute(t, Obj());
        Assert.True(r.Success);
        Assert.Equal(S("world"), r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_IfNullWithLiteralNull()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "ifNull", new List<VerbArg> { LitNull(), LitStr("fallback") }) });
        var r = TransformEngine.Execute(t, Obj());
        Assert.True(r.Success);
        Assert.Equal(S("fallback"), r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_BetweenInRange()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "between", new List<VerbArg> { RefArg("@.val"), LitInt(1), LitInt(10) }) });
        var r = TransformEngine.Execute(t, Obj(("val", I(5))));
        Assert.True(r.Success);
        Assert.Equal(B(true), r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_BetweenOutOfRange()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "between", new List<VerbArg> { RefArg("@.val"), LitInt(1), LitInt(10) }) });
        var r = TransformEngine.Execute(t, Obj(("val", I(15))));
        Assert.True(r.Success);
        Assert.Equal(B(false), r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_IsStringTrue()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "isString", new List<VerbArg> { RefArg("@.val") }) });
        var r = TransformEngine.Execute(t, Obj(("val", S("hello"))));
        Assert.True(r.Success);
        Assert.Equal(B(true), r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_IsStringFalse()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "isString", new List<VerbArg> { RefArg("@.val") }) });
        var r = TransformEngine.Execute(t, Obj(("val", I(42))));
        Assert.True(r.Success);
        Assert.Equal(B(false), r.Output!.Get("Out"));
    }

    // =========================================================================
    // 3. Verb chaining / nested verbs (~25 tests)
    // =========================================================================

    [Fact]
    public void Ext_UpperTrimChain()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "upper", new List<VerbArg> { Nested("trim", new List<VerbArg> { RefArg("@.val") }) }) });
        var r = TransformEngine.Execute(t, Obj(("val", S("  hello  "))));
        Assert.True(r.Success);
        Assert.Equal(S("HELLO"), r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_LowerTrimChain()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "lower", new List<VerbArg> { Nested("trim", new List<VerbArg> { RefArg("@.val") }) }) });
        var r = TransformEngine.Execute(t, Obj(("val", S("  WORLD  "))));
        Assert.True(r.Success);
        Assert.Equal(S("world"), r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_ConcatUpperLower()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "concat", new List<VerbArg> {
            Nested("upper", new List<VerbArg> { RefArg("@.first") }),
            LitStr(" "),
            Nested("lower", new List<VerbArg> { RefArg("@.last") })
        }) });
        var r = TransformEngine.Execute(t, Obj(("first", S("john")), ("last", S("DOE"))));
        Assert.True(r.Success);
        Assert.Equal(S("JOHN doe"), r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_TripleNesting()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "upper", new List<VerbArg> {
            Nested("concat", new List<VerbArg> {
                Nested("trim", new List<VerbArg> { RefArg("@.a") }),
                Nested("trim", new List<VerbArg> { RefArg("@.b") })
            })
        }) });
        var r = TransformEngine.Execute(t, Obj(("a", S(" hello ")), ("b", S(" world "))));
        Assert.True(r.Success);
        Assert.Equal(S("HELLOWORLD"), r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_AddMultiplyNested()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "add", new List<VerbArg> {
            Nested("multiply", new List<VerbArg> { RefArg("@.a"), RefArg("@.b") }),
            RefArg("@.c")
        }) });
        var r = TransformEngine.Execute(t, Obj(("a", I(3)), ("b", I(4)), ("c", I(5))));
        Assert.True(r.Success);
        var val = r.Output!.Get("Out");
        Assert.True(val!.AsInt64() == 17 || val.AsDouble() == 17.0);
    }

    [Fact]
    public void Ext_ConcatThreeFields()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "concat", new List<VerbArg> { RefArg("@.a"), LitStr("-"), RefArg("@.b"), LitStr("-"), RefArg("@.c") }) });
        var r = TransformEngine.Execute(t, Obj(("a", S("x")), ("b", S("y")), ("c", S("z"))));
        Assert.True(r.Success);
        Assert.Equal(S("x-y-z"), r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_ReplaceThenUpper()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "upper", new List<VerbArg> {
            Nested("replace", new List<VerbArg> { RefArg("@.val"), LitStr("world"), LitStr("earth") })
        }) });
        var r = TransformEngine.Execute(t, Obj(("val", S("hello world"))));
        Assert.True(r.Success);
        Assert.Equal(S("HELLO EARTH"), r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_SubstringThenUpper()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "upper", new List<VerbArg> {
            Nested("substring", new List<VerbArg> { RefArg("@.val"), LitInt(0), LitInt(5) })
        }) });
        var r = TransformEngine.Execute(t, Obj(("val", S("hello world"))));
        Assert.True(r.Success);
        Assert.Equal(S("HELLO"), r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_IfElseNestedVerbTrue()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "ifElse", new List<VerbArg> {
            RefArg("@.flag"),
            Nested("upper", new List<VerbArg> { RefArg("@.name") }),
            Nested("lower", new List<VerbArg> { RefArg("@.name") })
        }) });
        var r = TransformEngine.Execute(t, Obj(("flag", B(true)), ("name", S("Test"))));
        Assert.True(r.Success);
        Assert.Equal(S("TEST"), r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_IfElseNestedVerbFalse()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "ifElse", new List<VerbArg> {
            RefArg("@.flag"),
            Nested("upper", new List<VerbArg> { RefArg("@.name") }),
            Nested("lower", new List<VerbArg> { RefArg("@.name") })
        }) });
        var r = TransformEngine.Execute(t, Obj(("flag", B(false)), ("name", S("Test"))));
        Assert.True(r.Success);
        Assert.Equal(S("test"), r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_CoalesceWithVerbFallback()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "coalesce", new List<VerbArg> {
            RefArg("@.missing"),
            Nested("upper", new List<VerbArg> { LitStr("default") })
        }) });
        var r = TransformEngine.Execute(t, Obj());
        Assert.True(r.Success);
        Assert.Equal(S("DEFAULT"), r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_PadLeft()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "padLeft", new List<VerbArg> { RefArg("@.val"), LitInt(5), LitStr("0") }) });
        var r = TransformEngine.Execute(t, Obj(("val", S("42"))));
        Assert.True(r.Success);
        Assert.Equal(S("00042"), r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_PadRight()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "padRight", new List<VerbArg> { RefArg("@.val"), LitInt(5), LitStr("_") }) });
        var r = TransformEngine.Execute(t, Obj(("val", S("hi"))));
        Assert.True(r.Success);
        Assert.Equal(S("hi___"), r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_Split()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "split", new List<VerbArg> { RefArg("@.val"), LitStr(",") }) });
        var r = TransformEngine.Execute(t, Obj(("val", S("a,b,c"))));
        Assert.True(r.Success);
        Assert.Equal(3, r.Output!.Get("Out")!.AsArray()!.Count);
    }

    [Fact]
    public void Ext_Join()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "join", new List<VerbArg> { RefArg("@.val"), LitStr("-") }) });
        var r = TransformEngine.Execute(t, Obj(("val", Arr(S("a"), S("b"), S("c")))));
        Assert.True(r.Success);
        Assert.Equal(S("a-b-c"), r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_ContainsTrue()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "contains", new List<VerbArg> { RefArg("@.val"), LitStr("llo") }) });
        var r = TransformEngine.Execute(t, Obj(("val", S("hello"))));
        Assert.True(r.Success);
        Assert.Equal(B(true), r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_ContainsFalse()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "contains", new List<VerbArg> { RefArg("@.val"), LitStr("xyz") }) });
        var r = TransformEngine.Execute(t, Obj(("val", S("hello"))));
        Assert.True(r.Success);
        Assert.Equal(B(false), r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_StartsWithTrue()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "startsWith", new List<VerbArg> { RefArg("@.val"), LitStr("hel") }) });
        var r = TransformEngine.Execute(t, Obj(("val", S("hello"))));
        Assert.True(r.Success);
        Assert.Equal(B(true), r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_EndsWithTrue()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "endsWith", new List<VerbArg> { RefArg("@.val"), LitStr("llo") }) });
        var r = TransformEngine.Execute(t, Obj(("val", S("hello"))));
        Assert.True(r.Success);
        Assert.Equal(B(true), r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_RepeatString()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "repeat", new List<VerbArg> { RefArg("@.val"), LitInt(3) }) });
        var r = TransformEngine.Execute(t, Obj(("val", S("ab"))));
        Assert.True(r.Success);
        Assert.Equal(S("ababab"), r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_ReverseString()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "reverseString", new List<VerbArg> { RefArg("@.val") }) });
        var r = TransformEngine.Execute(t, Obj(("val", S("hello"))));
        Assert.True(r.Success);
        Assert.Equal(S("olleh"), r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_WordCount()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "wordCount", new List<VerbArg> { RefArg("@.val") }) });
        var r = TransformEngine.Execute(t, Obj(("val", S("hello beautiful world"))));
        Assert.True(r.Success);
        Assert.Equal(3L, r.Output!.Get("Out")!.AsInt64());
    }

    [Fact]
    public void Ext_ConcatNullAndString()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "concat", new List<VerbArg> { RefArg("@.missing"), LitStr(" test") }) });
        var r = TransformEngine.Execute(t, Obj());
        Assert.True(r.Success);
        Assert.NotNull(r.Output!.Get("Out")!.AsString());
    }

    // =========================================================================
    // 4. Constants (~10 tests)
    // =========================================================================

    [Fact]
    public void Ext_ConstantStringInOutput()
    {
        var t = MkTransform(new List<FieldMapping> { CopyField("Version", "$const.ver") });
        t.Constants["ver"] = new OdinString("1.0");
        var r = TransformEngine.Execute(t, Obj());
        Assert.True(r.Success);
        Assert.Equal(S("1.0"), r.Output!.Get("Version"));
    }

    [Fact]
    public void Ext_ConstantInteger()
    {
        var t = MkTransform(new List<FieldMapping> { CopyField("Max", "$const.maxRetries") });
        t.Constants["maxRetries"] = new OdinInteger(3);
        var r = TransformEngine.Execute(t, Obj());
        Assert.True(r.Success);
        Assert.Equal(I(3), r.Output!.Get("Max"));
    }

    [Fact]
    public void Ext_ConstantBoolean()
    {
        var t = MkTransform(new List<FieldMapping> { CopyField("Debug", "$const.debug") });
        t.Constants["debug"] = new OdinBoolean(true);
        var r = TransformEngine.Execute(t, Obj());
        Assert.True(r.Success);
        Assert.Equal(B(true), r.Output!.Get("Debug"));
    }

    [Fact]
    public void Ext_ConstantInVerb()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "concat", new List<VerbArg> { RefArg("$const.prefix"), LitStr(" "), RefArg("@.name") }) });
        t.Constants["prefix"] = new OdinString("Hello");
        var r = TransformEngine.Execute(t, Obj(("name", S("World"))));
        Assert.True(r.Success);
        Assert.Equal(S("Hello World"), r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_MultipleConstants()
    {
        var t = MkTransform(new List<FieldMapping> { CopyField("A", "$const.x"), CopyField("B", "$const.y"), CopyField("C", "$const.z") });
        t.Constants["x"] = new OdinString("alpha");
        t.Constants["y"] = new OdinString("beta");
        t.Constants["z"] = new OdinString("gamma");
        var r = TransformEngine.Execute(t, Obj());
        Assert.True(r.Success);
        Assert.Equal(S("alpha"), r.Output!.Get("A"));
        Assert.Equal(S("beta"), r.Output!.Get("B"));
        Assert.Equal(S("gamma"), r.Output!.Get("C"));
    }

    [Fact]
    public void Ext_MissingConstantReturnsNull()
    {
        var t = MkTransform(new List<FieldMapping> { CopyField("Out", "$const.missing") });
        var r = TransformEngine.Execute(t, Obj());
        Assert.True(r.Success);
        var v = r.Output!.Get("Out");
        Assert.True(v == null || v.IsNull);
    }

    // =========================================================================
    // 5. Transform features: nested output, objects, segments (~25 tests)
    // =========================================================================

    [Fact]
    public void Ext_NestedOutputPath()
    {
        var t = MkTransform(new List<FieldMapping> { CopyField("a.b.c", "@.val") });
        var r = TransformEngine.Execute(t, Obj(("val", S("deep"))));
        Assert.True(r.Success);
        Assert.Equal(S("deep"), r.Output!.Get("a")!.Get("b")!.Get("c"));
    }

    [Fact]
    public void Ext_MultipleFieldsSameNestedParent()
    {
        var t = MkTransform(new List<FieldMapping> { CopyField("person.name", "@.name"), CopyField("person.age", "@.age") });
        var r = TransformEngine.Execute(t, Obj(("name", S("Alice")), ("age", I(30))));
        Assert.True(r.Success);
        var person = r.Output!.Get("person");
        Assert.Equal(S("Alice"), person!.Get("name"));
        Assert.Equal(I(30), person.Get("age"));
    }

    [Fact]
    public void Ext_ObjectExpression()
    {
        var t = MkTransform(new List<FieldMapping> { new FieldMapping {
            Target = "Info",
            Expression = FieldExpression.Object(new List<FieldMapping> { CopyField("name", "@.name"), CopyField("city", "@.city") })
        }});
        var r = TransformEngine.Execute(t, Obj(("name", S("Bob")), ("city", S("NYC"))));
        Assert.True(r.Success);
        Assert.Equal(S("Bob"), r.Output!.Get("Info")!.Get("name"));
        Assert.Equal(S("NYC"), r.Output!.Get("Info")!.Get("city"));
    }

    [Fact]
    public void Ext_ObjectExpressionWithVerb()
    {
        var t = MkTransform(new List<FieldMapping> { new FieldMapping {
            Target = "Info",
            Expression = FieldExpression.Object(new List<FieldMapping> { VerbField("upperName", "upper", new List<VerbArg> { RefArg("@.name") }) })
        }});
        var r = TransformEngine.Execute(t, Obj(("name", S("alice"))));
        Assert.True(r.Success);
        Assert.Equal(S("ALICE"), r.Output!.Get("Info")!.Get("upperName"));
    }

    [Fact]
    public void Ext_ObjectExpressionEmpty()
    {
        var t = MkTransform(new List<FieldMapping> { new FieldMapping {
            Target = "Empty",
            Expression = FieldExpression.Object(new List<FieldMapping>())
        }});
        var r = TransformEngine.Execute(t, Obj());
        Assert.True(r.Success);
        Assert.Empty(r.Output!.Get("Empty")!.AsObject()!);
    }

    [Fact]
    public void Ext_NamedSegmentCreatesNamespace()
    {
        var t = MkCustom(new List<TransformSegment> { NamedSeg("Customer", new List<FieldMapping> { CopyField("Name", "@.name") }) });
        var r = TransformEngine.Execute(t, Obj(("name", S("Alice"))));
        Assert.True(r.Success);
        Assert.Equal(S("Alice"), r.Output!.Get("Customer")!.Get("Name"));
    }

    [Fact]
    public void Ext_MultipleNamedSegments()
    {
        var t = MkCustom(new List<TransformSegment> {
            NamedSeg("Header", new List<FieldMapping> { LitField("Type", new OdinString("Invoice")) }),
            NamedSeg("Body", new List<FieldMapping> { CopyField("Amount", "@.amount") })
        });
        var r = TransformEngine.Execute(t, Obj(("amount", I(100))));
        Assert.True(r.Success);
        Assert.Equal(S("Invoice"), r.Output!.Get("Header")!.Get("Type"));
        Assert.Equal(I(100), r.Output!.Get("Body")!.Get("Amount"));
    }

    [Fact]
    public void Ext_LoopBasic()
    {
        var seg = new TransformSegment
        {
            Name = "Items", Path = "Items",
            SourcePath = "@.items", IsArray = true,
            Mappings = new List<FieldMapping> { CopyField("name", "@_item.name") }
        };
        var t = MkCustom(new List<TransformSegment> { seg });
        var source = Obj(("items", Arr(Obj(("name", S("A"))), Obj(("name", S("B"))))));
        var r = TransformEngine.Execute(t, source);
        Assert.True(r.Success);
        Assert.Equal(2, r.Output!.Get("Items")!.AsArray()!.Count);
    }

    [Fact]
    public void Ext_LoopWithVerb()
    {
        var seg = new TransformSegment
        {
            Name = "Items", Path = "Items",
            SourcePath = "@.items", IsArray = true,
            Mappings = new List<FieldMapping> { VerbField("upper_name", "upper", new List<VerbArg> { RefArg("@_item.name") }) }
        };
        var t = MkCustom(new List<TransformSegment> { seg });
        var source = Obj(("items", Arr(Obj(("name", S("alice"))))));
        var r = TransformEngine.Execute(t, source);
        Assert.True(r.Success);
        Assert.Equal(S("ALICE"), r.Output!.Get("Items")!.AsArray()![0].Get("upper_name"));
    }

    [Fact]
    public void Ext_LoopEmptyArray()
    {
        var seg = new TransformSegment
        {
            Name = "Items", Path = "Items",
            SourcePath = "@.items", IsArray = true,
            Mappings = new List<FieldMapping> { CopyField("name", "@_item.name") }
        };
        var t = MkCustom(new List<TransformSegment> { seg });
        var r = TransformEngine.Execute(t, Obj(("items", Arr())));
        Assert.True(r.Success);
    }

    [Fact]
    public void Ext_LiteralTypes()
    {
        var t = MkTransform(new List<FieldMapping> {
            LitField("S", new OdinString("str")),
            LitField("N", new OdinInteger(42)),
            LitField("F", new OdinNumber(3.14)),
            LitField("B", new OdinBoolean(true)),
            LitField("Z", new OdinNull())
        });
        var r = TransformEngine.Execute(t, Obj());
        Assert.True(r.Success);
        Assert.Equal(S("str"), r.Output!.Get("S"));
        Assert.Equal(I(42), r.Output!.Get("N"));
        Assert.Equal(B(true), r.Output!.Get("B"));
        Assert.True(r.Output!.Get("Z")!.IsNull);
    }

    [Fact]
    public void Ext_CopyEntireSource()
    {
        var t = MkTransform(new List<FieldMapping> { CopyField("All", "@") });
        var r = TransformEngine.Execute(t, Obj(("x", I(1)), ("y", I(2))));
        Assert.True(r.Success);
        Assert.Equal(I(1), r.Output!.Get("All")!.Get("x"));
        Assert.Equal(I(2), r.Output!.Get("All")!.Get("y"));
    }

    [Fact]
    public void Ext_CopyArrayField()
    {
        var t = MkTransform(new List<FieldMapping> { CopyField("Tags", "@.tags") });
        var r = TransformEngine.Execute(t, Obj(("tags", Arr(S("a"), S("b"), S("c")))));
        Assert.True(r.Success);
        Assert.Equal(3, r.Output!.Get("Tags")!.AsArray()!.Count);
        Assert.Equal(S("a"), r.Output!.Get("Tags")!.AsArray()![0]);
    }

    [Fact]
    public void Ext_DeeplyNestedSource()
    {
        var t = MkTransform(new List<FieldMapping> { CopyField("Out", "@.a.b.c.d") });
        var source = Obj(("a", Obj(("b", Obj(("c", Obj(("d", S("deep")))))))));
        var r = TransformEngine.Execute(t, source);
        Assert.True(r.Success);
        Assert.Equal(S("deep"), r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_FormattedJsonOutput()
    {
        var t = MkTransform(new List<FieldMapping> { LitField("Name", new OdinString("Test")), LitField("Value", new OdinInteger(42)) });
        var r = TransformEngine.Execute(t, Obj());
        Assert.True(r.Success);
        Assert.NotNull(r.Formatted);
        Assert.Contains("Name", r.Formatted!);
        Assert.Contains("42", r.Formatted!);
    }

    // =========================================================================
    // 6. Segment conditions (~15 tests)
    // =========================================================================

    [Fact]
    public void Ext_SegmentConditionTruthyInteger()
    {
        var seg = new TransformSegment
        {
            Condition = "@.flag",
            Mappings = new List<FieldMapping> { LitField("A", new OdinString("found")) }
        };
        var t = MkCustom(new List<TransformSegment> { seg });
        var r = TransformEngine.Execute(t, Obj(("flag", I(1))));
        Assert.True(r.Success);
        Assert.Equal(S("found"), r.Output!.Get("A"));
    }

    [Fact]
    public void Ext_SegmentConditionFalsyZero()
    {
        var seg = new TransformSegment
        {
            Condition = "@.flag",
            Mappings = new List<FieldMapping> { LitField("A", new OdinString("found")) }
        };
        var t = MkCustom(new List<TransformSegment> { seg });
        var r = TransformEngine.Execute(t, Obj(("flag", I(0))));
        Assert.True(r.Success);
        var a = r.Output!.Get("A");
        Assert.True(a == null || a.IsNull);
    }

    [Fact]
    public void Ext_SegmentConditionMissingField()
    {
        var seg = new TransformSegment
        {
            Condition = "@.doesNotExist",
            Mappings = new List<FieldMapping> { LitField("A", new OdinString("found")) }
        };
        var t = MkCustom(new List<TransformSegment> { seg });
        var r = TransformEngine.Execute(t, Obj());
        Assert.True(r.Success);
        var a = r.Output!.Get("A");
        Assert.True(a == null || a.IsNull);
    }

    [Fact]
    public void Ext_SegmentConditionNonEmptyStringRuns()
    {
        var seg = new TransformSegment
        {
            Condition = "@.val",
            Mappings = new List<FieldMapping> { LitField("X", new OdinString("ok")) }
        };
        var t = MkCustom(new List<TransformSegment> { seg });
        var r = TransformEngine.Execute(t, Obj(("val", S("hello"))));
        Assert.True(r.Success);
        Assert.Equal(S("ok"), r.Output!.Get("X"));
    }

    [Fact]
    public void Ext_SegmentConditionEmptyStringSkips()
    {
        var seg = new TransformSegment
        {
            Condition = "@.val",
            Mappings = new List<FieldMapping> { LitField("X", new OdinString("ok")) }
        };
        var t = MkCustom(new List<TransformSegment> { seg });
        var r = TransformEngine.Execute(t, Obj(("val", S(""))));
        Assert.True(r.Success);
        var x = r.Output!.Get("X");
        Assert.True(x == null || x.IsNull);
    }

    // =========================================================================
    // 7. Multi-pass transforms (~20 tests)
    // =========================================================================

    [Fact]
    public void Ext_Pass1Then2()
    {
        var t = MkCustom(new List<TransformSegment> { PassSeg(1, new List<FieldMapping> { LitField("P1", new OdinString("first")) }), PassSeg(2, new List<FieldMapping> { LitField("P2", new OdinString("second")) }) });
        var r = TransformEngine.Execute(t, Obj());
        Assert.True(r.Success);
        Assert.Equal(S("first"), r.Output!.Get("P1"));
        Assert.Equal(S("second"), r.Output!.Get("P2"));
    }

    [Fact]
    public void Ext_PassNoneRunsAfterNumbered()
    {
        var t = MkCustom(new List<TransformSegment> {
            RootSeg(new List<FieldMapping> { LitField("Default", new OdinString("last")) }),
            PassSeg(1, new List<FieldMapping> { LitField("P1", new OdinString("first")) })
        });
        var r = TransformEngine.Execute(t, Obj());
        Assert.True(r.Success);
        Assert.Equal(S("first"), r.Output!.Get("P1"));
        Assert.Equal(S("last"), r.Output!.Get("Default"));
    }

    [Fact]
    public void Ext_ThreePasses()
    {
        var t = MkCustom(new List<TransformSegment> {
            PassSeg(3, new List<FieldMapping> { LitField("C", new OdinString("3")) }),
            PassSeg(1, new List<FieldMapping> { LitField("A", new OdinString("1")) }),
            PassSeg(2, new List<FieldMapping> { LitField("B", new OdinString("2")) })
        });
        var r = TransformEngine.Execute(t, Obj());
        Assert.True(r.Success);
        Assert.Equal(S("1"), r.Output!.Get("A"));
        Assert.Equal(S("2"), r.Output!.Get("B"));
        Assert.Equal(S("3"), r.Output!.Get("C"));
    }

    [Fact]
    public void Ext_LaterOverwritesEarlier()
    {
        var t = MkCustom(new List<TransformSegment> {
            PassSeg(1, new List<FieldMapping> { LitField("Val", new OdinString("old")) }),
            PassSeg(2, new List<FieldMapping> { LitField("Val", new OdinString("new")) })
        });
        var r = TransformEngine.Execute(t, Obj());
        Assert.True(r.Success);
        Assert.Equal(S("new"), r.Output!.Get("Val"));
    }

    [Fact]
    public void Ext_MultipleSegmentsSamePass()
    {
        var t = MkCustom(new List<TransformSegment> {
            PassSeg(1, new List<FieldMapping> { LitField("A", new OdinString("a")) }),
            PassSeg(1, new List<FieldMapping> { LitField("B", new OdinString("b")) })
        });
        var r = TransformEngine.Execute(t, Obj());
        Assert.True(r.Success);
        Assert.Equal(S("a"), r.Output!.Get("A"));
        Assert.Equal(S("b"), r.Output!.Get("B"));
    }

    [Fact]
    public void Ext_PassWithConditionTrue()
    {
        var seg = PassSeg(1, new List<FieldMapping> { LitField("A", new OdinString("active")) });
        seg.Condition = "@.active";
        var t = MkCustom(new List<TransformSegment> { seg });
        var r = TransformEngine.Execute(t, Obj(("active", B(true))));
        Assert.True(r.Success);
        Assert.Equal(S("active"), r.Output!.Get("A"));
    }

    [Fact]
    public void Ext_PassWithConditionSkipped()
    {
        var seg = PassSeg(1, new List<FieldMapping> { LitField("A", new OdinString("active")) });
        seg.Condition = "@.active";
        var t = MkCustom(new List<TransformSegment> { seg });
        var r = TransformEngine.Execute(t, Obj(("active", B(false))));
        Assert.True(r.Success);
        var a = r.Output!.Get("A");
        Assert.True(a == null || a.IsNull);
    }

    [Fact]
    public void Ext_SinglePassWorksNormally()
    {
        var t = MkCustom(new List<TransformSegment> { PassSeg(1, new List<FieldMapping> { LitField("X", new OdinString("y")) }) });
        var r = TransformEngine.Execute(t, Obj());
        Assert.True(r.Success);
        Assert.Equal(S("y"), r.Output!.Get("X"));
    }

    [Fact]
    public void Ext_NamedSegmentInPass()
    {
        var seg = NamedSeg("Header", new List<FieldMapping> { LitField("Type", new OdinString("Invoice")) });
        seg.Pass = 1;
        var t = MkCustom(new List<TransformSegment> { seg });
        var r = TransformEngine.Execute(t, Obj());
        Assert.True(r.Success);
        Assert.Equal(S("Invoice"), r.Output!.Get("Header")!.Get("Type"));
    }

    [Fact]
    public void Ext_CopyFromSourceInPass()
    {
        var t = MkCustom(new List<TransformSegment> { PassSeg(1, new List<FieldMapping> { CopyField("Name", "@.name") }) });
        var r = TransformEngine.Execute(t, Obj(("name", S("Alice"))));
        Assert.True(r.Success);
        Assert.Equal(S("Alice"), r.Output!.Get("Name"));
    }

    [Fact]
    public void Ext_VerbInPass()
    {
        var t = MkCustom(new List<TransformSegment> { PassSeg(1, new List<FieldMapping> { VerbField("Upper", "upper", new List<VerbArg> { RefArg("@.name") }) }) });
        var r = TransformEngine.Execute(t, Obj(("name", S("alice"))));
        Assert.True(r.Success);
        Assert.Equal(S("ALICE"), r.Output!.Get("Upper"));
    }

    [Fact]
    public void Ext_FourPassesAllProduceOutput()
    {
        var segs = Enumerable.Range(1, 4).Select(p =>
            PassSeg(p, new List<FieldMapping> { LitField($"P{p}", new OdinString($"val{p}")) })
        ).ToList();
        var t = MkCustom(segs);
        var r = TransformEngine.Execute(t, Obj());
        Assert.True(r.Success);
        for (int p = 1; p <= 4; p++)
            Assert.Equal(S($"val{p}"), r.Output!.Get($"P{p}"));
    }

    [Fact]
    public void Ext_PassWithAccumulator()
    {
        var seg1 = PassSeg(1, new List<FieldMapping> {
            VerbField("_", "accumulate", new List<VerbArg> { LitStr("total"), LitStr("add"), LitInt(10) })
        });
        var seg2 = PassSeg(2, new List<FieldMapping> { CopyField("Total", "$accumulator.total") });
        var t = MkCustom(new List<TransformSegment> { seg1, seg2 });
        t.Accumulators["total"] = new AccumulatorDef { Name = "total", Initial = new OdinInteger(0), Persist = true };
        var r = TransformEngine.Execute(t, Obj());
        Assert.True(r.Success);
    }

    [Fact]
    public void Ext_AccumulatorNonPersistResets()
    {
        var seg1 = PassSeg(1, new List<FieldMapping> { LitField("P1", new OdinInteger(1)) });
        var seg2 = PassSeg(2, new List<FieldMapping> { CopyField("Counter", "$accumulator.counter") });
        var t = MkCustom(new List<TransformSegment> { seg1, seg2 });
        t.Accumulators["counter"] = new AccumulatorDef { Name = "counter", Initial = new OdinInteger(0), Persist = false };
        var r = TransformEngine.Execute(t, Obj());
        Assert.True(r.Success);
        Assert.Equal(I(0), r.Output!.Get("Counter"));
    }

    [Fact]
    public void Ext_AccumulatorPersistSurvives()
    {
        var seg1 = PassSeg(1, new List<FieldMapping> { LitField("P1", new OdinInteger(1)) });
        var seg2 = PassSeg(2, new List<FieldMapping> { CopyField("Counter", "$accumulator.persist_counter") });
        var t = MkCustom(new List<TransformSegment> { seg1, seg2 });
        t.Accumulators["persist_counter"] = new AccumulatorDef { Name = "persist_counter", Initial = new OdinInteger(42), Persist = true };
        var r = TransformEngine.Execute(t, Obj());
        Assert.True(r.Success);
        Assert.Equal(I(42), r.Output!.Get("Counter"));
    }

    // =========================================================================
    // 8. Confidential enforcement (~25 tests)
    // =========================================================================

    [Fact]
    public void Ext_ConfRedactString()
    {
        var t = MkTransform(new List<FieldMapping> { ModField("SSN", "@.ssn", new OdinModifiers { Confidential = true }) });
        t.EnforceConfidential = ConfidentialMode.Redact;
        var r = TransformEngine.Execute(t, Obj(("ssn", S("123-45-6789"))));
        Assert.True(r.Success);
        Assert.True(r.Output!.Get("SSN")!.IsNull);
    }

    [Fact]
    public void Ext_ConfRedactInteger()
    {
        var t = MkTransform(new List<FieldMapping> { ModField("Pin", "@.pin", new OdinModifiers { Confidential = true }) });
        t.EnforceConfidential = ConfidentialMode.Redact;
        var r = TransformEngine.Execute(t, Obj(("pin", I(1234))));
        Assert.True(r.Success);
        Assert.True(r.Output!.Get("Pin")!.IsNull);
    }

    [Fact]
    public void Ext_ConfRedactBoolean()
    {
        var t = MkTransform(new List<FieldMapping> { ModField("Flag", "@.flag", new OdinModifiers { Confidential = true }) });
        t.EnforceConfidential = ConfidentialMode.Redact;
        var r = TransformEngine.Execute(t, Obj(("flag", B(true))));
        Assert.True(r.Success);
        Assert.True(r.Output!.Get("Flag")!.IsNull);
    }

    [Fact]
    public void Ext_ConfRedactFloat()
    {
        var t = MkTransform(new List<FieldMapping> { ModField("Salary", "@.salary", new OdinModifiers { Confidential = true }) });
        t.EnforceConfidential = ConfidentialMode.Redact;
        var r = TransformEngine.Execute(t, Obj(("salary", F(75000.50))));
        Assert.True(r.Success);
        Assert.True(r.Output!.Get("Salary")!.IsNull);
    }

    [Fact]
    public void Ext_ConfRedactNullStaysNull()
    {
        var t = MkTransform(new List<FieldMapping> { ModField("Val", "@.missing", new OdinModifiers { Confidential = true }) });
        t.EnforceConfidential = ConfidentialMode.Redact;
        var r = TransformEngine.Execute(t, Obj());
        Assert.True(r.Success);
        Assert.True(r.Output!.Get("Val")!.IsNull);
    }

    [Fact]
    public void Ext_ConfMaskString()
    {
        var t = MkTransform(new List<FieldMapping> { ModField("SSN", "@.ssn", new OdinModifiers { Confidential = true }) });
        t.EnforceConfidential = ConfidentialMode.Mask;
        var r = TransformEngine.Execute(t, Obj(("ssn", S("123-45-6789"))));
        Assert.True(r.Success);
        var val = r.Output!.Get("SSN");
        if (val!.Type == DynValueType.String)
        {
            var masked = val.AsString()!;
            Assert.True(masked.All(c => c == '*'));
            Assert.Equal(11, masked.Length);
        }
    }

    [Fact]
    public void Ext_ConfMaskIntegerBecomesNull()
    {
        var t = MkTransform(new List<FieldMapping> { ModField("Pin", "@.pin", new OdinModifiers { Confidential = true }) });
        t.EnforceConfidential = ConfidentialMode.Mask;
        var r = TransformEngine.Execute(t, Obj(("pin", I(1234))));
        Assert.True(r.Success);
        Assert.True(r.Output!.Get("Pin")!.IsNull);
    }

    [Fact]
    public void Ext_ConfMaskBooleanBecomesNull()
    {
        var t = MkTransform(new List<FieldMapping> { ModField("Flag", "@.flag", new OdinModifiers { Confidential = true }) });
        t.EnforceConfidential = ConfidentialMode.Mask;
        var r = TransformEngine.Execute(t, Obj(("flag", B(true))));
        Assert.True(r.Success);
        Assert.True(r.Output!.Get("Flag")!.IsNull);
    }

    [Fact]
    public void Ext_ConfMaskEmptyString()
    {
        var t = MkTransform(new List<FieldMapping> { ModField("Val", "@.val", new OdinModifiers { Confidential = true }) });
        t.EnforceConfidential = ConfidentialMode.Mask;
        var r = TransformEngine.Execute(t, Obj(("val", S(""))));
        Assert.True(r.Success);
        Assert.Equal("", r.Output!.Get("Val")!.AsString());
    }

    [Fact]
    public void Ext_ConfNoEnforcementPassesThrough()
    {
        var t = MkTransform(new List<FieldMapping> { ModField("SSN", "@.ssn", new OdinModifiers { Confidential = true }) });
        var r = TransformEngine.Execute(t, Obj(("ssn", S("123-45-6789"))));
        Assert.True(r.Success);
        Assert.Equal(S("123-45-6789"), r.Output!.Get("SSN"));
    }

    [Fact]
    public void Ext_ConfNonConfidentialUnchangedWithRedact()
    {
        var t = MkTransform(new List<FieldMapping> {
            ModField("SSN", "@.ssn", new OdinModifiers { Confidential = true }),
            CopyField("Name", "@.name")
        });
        t.EnforceConfidential = ConfidentialMode.Redact;
        var r = TransformEngine.Execute(t, Obj(("ssn", S("xxx")), ("name", S("Alice"))));
        Assert.True(r.Success);
        Assert.True(r.Output!.Get("SSN")!.IsNull);
        Assert.Equal(S("Alice"), r.Output!.Get("Name"));
    }

    [Fact]
    public void Ext_ConfRedactMultipleFields()
    {
        var t = MkTransform(new List<FieldMapping> {
            ModField("SSN", "@.ssn", new OdinModifiers { Confidential = true }),
            ModField("DOB", "@.dob", new OdinModifiers { Confidential = true }),
            CopyField("Name", "@.name")
        });
        t.EnforceConfidential = ConfidentialMode.Redact;
        var r = TransformEngine.Execute(t, Obj(("ssn", S("111-22-3333")), ("dob", S("1990-01-01")), ("name", S("Bob"))));
        Assert.True(r.Success);
        Assert.True(r.Output!.Get("SSN")!.IsNull);
        Assert.True(r.Output!.Get("DOB")!.IsNull);
        Assert.Equal(S("Bob"), r.Output!.Get("Name"));
    }

    [Fact]
    public void Ext_ConfRequiredModifierRecorded()
    {
        var t = MkTransform(new List<FieldMapping> { ModField("Name", "@.name", new OdinModifiers { Required = true }) });
        var r = TransformEngine.Execute(t, Obj(("name", S("Alice"))));
        Assert.True(r.Success);
        Assert.True(r.OutputModifiers.ContainsKey("Name"));
        Assert.True(r.OutputModifiers["Name"].Required);
    }

    [Fact]
    public void Ext_ConfDeprecatedModifierRecorded()
    {
        var t = MkTransform(new List<FieldMapping> { ModField("Old", "@.old", new OdinModifiers { Deprecated = true }) });
        var r = TransformEngine.Execute(t, Obj(("old", S("legacy"))));
        Assert.True(r.Success);
        Assert.True(r.OutputModifiers["Old"].Deprecated);
    }

    [Fact]
    public void Ext_ConfAllModifiersRecorded()
    {
        var t = MkTransform(new List<FieldMapping> { ModField("Secret", "@.secret", new OdinModifiers { Required = true, Confidential = true, Deprecated = true }) });
        var r = TransformEngine.Execute(t, Obj(("secret", S("data"))));
        Assert.True(r.Success);
        var m = r.OutputModifiers["Secret"];
        Assert.True(m.Required && m.Confidential && m.Deprecated);
    }

    [Fact]
    public void Ext_ConfRedactWithRequiredModifier()
    {
        var t = MkTransform(new List<FieldMapping> { ModField("SSN", "@.ssn", new OdinModifiers { Required = true, Confidential = true }) });
        t.EnforceConfidential = ConfidentialMode.Redact;
        var r = TransformEngine.Execute(t, Obj(("ssn", S("123-45-6789"))));
        Assert.True(r.Success);
        Assert.True(r.Output!.Get("SSN")!.IsNull);
        Assert.True(r.OutputModifiers["SSN"].Required && r.OutputModifiers["SSN"].Confidential);
    }

    [Fact]
    public void Ext_ConfMaskLongString()
    {
        var t = MkTransform(new List<FieldMapping> { ModField("Data", "@.data", new OdinModifiers { Confidential = true }) });
        t.EnforceConfidential = ConfidentialMode.Mask;
        var longStr = new string('a', 100);
        var r = TransformEngine.Execute(t, Obj(("data", S(longStr))));
        Assert.True(r.Success);
        if (r.Output!.Get("Data")!.Type == DynValueType.String)
        {
            var val = r.Output!.Get("Data")!.AsString()!;
            Assert.Equal(100, val.Length);
            Assert.True(val.All(c => c == '*'));
        }
    }

    [Fact]
    public void Ext_ConfRedactFloatBecomesNull()
    {
        var t = MkTransform(new List<FieldMapping> { ModField("Rate", "@.rate", new OdinModifiers { Confidential = true }) });
        t.EnforceConfidential = ConfidentialMode.Redact;
        var r = TransformEngine.Execute(t, Obj(("rate", F(99.99))));
        Assert.True(r.Success);
        Assert.True(r.Output!.Get("Rate")!.IsNull);
    }

    [Fact]
    public void Ext_ConfMaskSingleCharString()
    {
        var t = MkTransform(new List<FieldMapping> { ModField("Val", "@.val", new OdinModifiers { Confidential = true }) });
        t.EnforceConfidential = ConfidentialMode.Mask;
        var r = TransformEngine.Execute(t, Obj(("val", S("X"))));
        Assert.True(r.Success);
        Assert.Equal(S("*"), r.Output!.Get("Val"));
    }

    [Fact]
    public void Ext_ConfRedactAfterVerbTransform()
    {
        var t = MkTransform(new List<FieldMapping> { new FieldMapping {
            Target = "SSN",
            Expression = FieldExpression.Transform(new VerbCall { Verb = "upper", Args = new List<VerbArg> { RefArg("@.ssn") } }),
            Modifiers = new OdinModifiers { Confidential = true }
        }});
        t.EnforceConfidential = ConfidentialMode.Redact;
        var r = TransformEngine.Execute(t, Obj(("ssn", S("abc"))));
        Assert.True(r.Success);
        Assert.True(r.Output!.Get("SSN")!.IsNull);
    }

    [Fact]
    public void Ext_ConfThreeConfidentialFieldsAllRedacted()
    {
        var t = MkTransform(new List<FieldMapping> {
            ModField("A", "@.a", new OdinModifiers { Confidential = true }),
            ModField("B", "@.b", new OdinModifiers { Confidential = true }),
            ModField("C", "@.c", new OdinModifiers { Confidential = true })
        });
        t.EnforceConfidential = ConfidentialMode.Redact;
        var r = TransformEngine.Execute(t, Obj(("a", S("1")), ("b", S("2")), ("c", S("3"))));
        Assert.True(r.Success);
        Assert.True(r.Output!.Get("A")!.IsNull);
        Assert.True(r.Output!.Get("B")!.IsNull);
        Assert.True(r.Output!.Get("C")!.IsNull);
    }

    // =========================================================================
    // 9. Error handling (~30 tests)
    // =========================================================================

    [Fact]
    public void Ext_ErrUnknownVerb()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "nonExistentVerb", new List<VerbArg> { RefArg("@.val") }) });
        var r = TransformEngine.Execute(t, Obj(("val", S("x"))));
        Assert.False(r.Success);
        Assert.NotEmpty(r.Errors);
        Assert.Contains("nonExistentVerb", r.Errors[0].Message);
    }

    [Fact]
    public void Ext_ErrUnknownVerbStillHasOutput()
    {
        var t = MkTransform(new List<FieldMapping> {
            VerbField("Bad", "noSuchVerb", new List<VerbArg> { RefArg("@.val") }),
            LitField("Good", new OdinString("ok"))
        });
        var r = TransformEngine.Execute(t, Obj(("val", S("x"))));
        Assert.Equal(S("ok"), r.Output!.Get("Good"));
    }

    [Fact]
    public void Ext_ErrMultipleUnknownVerbs()
    {
        var t = MkTransform(new List<FieldMapping> {
            VerbField("A", "bad1", new List<VerbArg> { RefArg("@.x") }),
            VerbField("B", "bad2", new List<VerbArg> { RefArg("@.y") })
        });
        var r = TransformEngine.Execute(t, Obj(("x", S("a")), ("y", S("b"))));
        Assert.False(r.Success);
        Assert.True(r.Errors.Count >= 2);
    }

    [Fact]
    public void Ext_ErrMissingSourceFieldReturnsNull()
    {
        var t = MkTransform(new List<FieldMapping> { CopyField("Out", "@.nonexistent") });
        var r = TransformEngine.Execute(t, Obj());
        Assert.True(r.Success);
        var v = r.Output!.Get("Out");
        Assert.True(v == null || v.IsNull);
    }

    [Fact]
    public void Ext_ErrMissingDeeplyNestedField()
    {
        var t = MkTransform(new List<FieldMapping> { CopyField("Out", "@.a.b.c.d.e") });
        var r = TransformEngine.Execute(t, Obj(("a", Obj())));
        Assert.True(r.Success);
        var v = r.Output!.Get("Out");
        Assert.True(v == null || v.IsNull);
    }

    [Fact]
    public void Ext_ErrEmptyTransformEmptySource()
    {
        var t = MkTransform(new List<FieldMapping>());
        var r = TransformEngine.Execute(t, Obj());
        Assert.True(r.Success);
    }

    [Fact]
    public void Ext_ErrEmptyTransformWithSource()
    {
        var t = MkTransform(new List<FieldMapping>());
        var r = TransformEngine.Execute(t, Obj(("name", S("Alice"))));
        Assert.True(r.Success);
    }

    [Fact]
    public void Ext_ErrNestedVerbError()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "upper", new List<VerbArg> {
            Nested("nonExistent", new List<VerbArg> { RefArg("@.val") })
        }) });
        var r = TransformEngine.Execute(t, Obj(("val", S("x"))));
        Assert.False(r.Success);
        Assert.NotEmpty(r.Errors);
    }

    [Fact]
    public void Ext_ErrVerbNoArgsConcat()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "concat", new List<VerbArg>()) });
        var r = TransformEngine.Execute(t, Obj());
        Assert.True(r.Success);
        Assert.Equal(S(""), r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_ErrCopyFromNonObjectPath()
    {
        var t = MkTransform(new List<FieldMapping> { CopyField("Out", "@.name.sub") });
        var r = TransformEngine.Execute(t, Obj(("name", S("Alice"))));
        Assert.True(r.Success);
        var v = r.Output!.Get("Out");
        Assert.True(v == null || v.IsNull);
    }

    [Fact]
    public void Ext_ErrArrayIndexOnNonArray()
    {
        var t = MkTransform(new List<FieldMapping> { CopyField("Out", "@.name[0]") });
        var r = TransformEngine.Execute(t, Obj(("name", S("Alice"))));
        Assert.True(r.Success);
    }

    [Fact]
    public void Ext_ErrArrayIndexOutOfBounds()
    {
        var t = MkTransform(new List<FieldMapping> { CopyField("Out", "@.items[99]") });
        var r = TransformEngine.Execute(t, Obj(("items", Arr(S("a")))));
        Assert.True(r.Success);
        var v = r.Output!.Get("Out");
        Assert.True(v == null || v.IsNull);
    }

    [Fact]
    public void Ext_ErrMultipleErrorsDontHalt()
    {
        var t = MkTransform(new List<FieldMapping> {
            VerbField("A", "bad1", new List<VerbArg> { RefArg("@.x") }),
            LitField("B", new OdinString("ok")),
            VerbField("C", "bad2", new List<VerbArg> { RefArg("@.y") }),
            LitField("D", new OdinInteger(42))
        });
        var r = TransformEngine.Execute(t, Obj(("x", S("a")), ("y", S("b"))));
        Assert.Equal(S("ok"), r.Output!.Get("B"));
        Assert.Equal(I(42), r.Output!.Get("D"));
    }

    [Fact]
    public void Ext_ErrCopyWithNullSourceData()
    {
        var t = MkTransform(new List<FieldMapping> { CopyField("Out", "@.val") });
        var r = TransformEngine.Execute(t, N());
        Assert.True(r.Success);
        var v = r.Output!.Get("Out");
        Assert.True(v == null || v.IsNull);
    }

    [Fact]
    public void Ext_ErrVerbOnNullInput()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "upper", new List<VerbArg> { RefArg("@.missing") }) });
        var r = TransformEngine.Execute(t, Obj());
        Assert.True(r.Success);
    }

    [Fact]
    public void Ext_ErrConstantRefMissing()
    {
        var t = MkTransform(new List<FieldMapping> { CopyField("Out", "$const.undefined") });
        var r = TransformEngine.Execute(t, Obj());
        Assert.True(r.Success);
        var v = r.Output!.Get("Out");
        Assert.True(v == null || v.IsNull);
    }

    [Fact]
    public void Ext_ErrLoopOnNonArraySource()
    {
        var seg = new TransformSegment
        {
            Name = "Items", Path = "Items",
            SourcePath = "@.notArray", IsArray = true,
            Mappings = new List<FieldMapping> { CopyField("x", "@_item") }
        };
        var t = MkCustom(new List<TransformSegment> { seg });
        var r = TransformEngine.Execute(t, Obj(("notArray", S("scalar"))));
        Assert.True(r.Success);
    }

    [Fact]
    public void Ext_ErrLoopOnMissingSource()
    {
        var seg = new TransformSegment
        {
            Name = "Items", Path = "Items",
            SourcePath = "@.missing", IsArray = true,
            Mappings = new List<FieldMapping> { CopyField("x", "@_item") }
        };
        var t = MkCustom(new List<TransformSegment> { seg });
        var r = TransformEngine.Execute(t, Obj());
        Assert.True(r.Success);
    }

    [Fact]
    public void Ext_ErrDiscriminatorMismatchSkips()
    {
        var seg = new TransformSegment
        {
            Name = "TypeA", Path = "TypeA",
            SegmentDiscriminator = new Discriminator { Path = "@.type", Value = "A" },
            Mappings = new List<FieldMapping> { LitField("Found", new OdinString("A")) }
        };
        var t = MkCustom(new List<TransformSegment> { seg });
        var r = TransformEngine.Execute(t, Obj(("type", S("B"))));
        Assert.True(r.Success);
        var v = r.Output!.Get("TypeA");
        Assert.True(v == null);
    }

    [Fact]
    public void Ext_ErrDiscriminatorMatchProcesses()
    {
        var seg = new TransformSegment
        {
            Name = "TypeA", Path = "TypeA",
            SegmentDiscriminator = new Discriminator { Path = "@.type", Value = "A" },
            Mappings = new List<FieldMapping> { LitField("Found", new OdinString("yes")) }
        };
        var t = MkCustom(new List<TransformSegment> { seg });
        var r = TransformEngine.Execute(t, Obj(("type", S("A"))));
        Assert.True(r.Success);
        Assert.Equal(S("yes"), r.Output!.Get("TypeA")!.Get("Found"));
    }

    [Fact]
    public void Ext_ErrLiteralNullExplicit()
    {
        var t = MkTransform(new List<FieldMapping> { LitField("Out", new OdinNull()) });
        var r = TransformEngine.Execute(t, Obj());
        Assert.True(r.Success);
        Assert.True(r.Output!.Get("Out")!.IsNull);
    }

    [Fact]
    public void Ext_ErrSuccessTrueWhenAllMappingsSucceed()
    {
        var t = MkTransform(new List<FieldMapping> { LitField("A", new OdinString("a")), LitField("B", new OdinInteger(1)) });
        var r = TransformEngine.Execute(t, Obj());
        Assert.True(r.Success);
        Assert.Empty(r.Errors);
    }

    [Fact]
    public void Ext_ErrSuccessFalseOnVerbError()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "totallyFake", new List<VerbArg> { RefArg("@.x") }) });
        var r = TransformEngine.Execute(t, Obj(("x", S("a"))));
        Assert.False(r.Success);
    }

    [Fact]
    public void Ext_ErrResultHasFormattedOutput()
    {
        var t = MkTransform(new List<FieldMapping> { LitField("X", new OdinString("y")) });
        var r = TransformEngine.Execute(t, Obj());
        Assert.True(r.Success);
        Assert.NotNull(r.Formatted);
        Assert.Contains("X", r.Formatted!);
    }

    [Fact]
    public void Ext_ErrMixedSuccessAndErrors()
    {
        var t = MkTransform(new List<FieldMapping> {
            LitField("Good", new OdinString("ok")),
            VerbField("Bad", "doesNotExist", new List<VerbArg> { LitStr("x") })
        });
        var r = TransformEngine.Execute(t, Obj());
        Assert.False(r.Success);
        Assert.NotEmpty(r.Errors);
        Assert.Equal(S("ok"), r.Output!.Get("Good"));
    }

    [Fact]
    public void Ext_ErrCopyIntegerPreservesType()
    {
        var t = MkTransform(new List<FieldMapping> { CopyField("Out", "@.val") });
        var r = TransformEngine.Execute(t, Obj(("val", I(42))));
        Assert.True(r.Success);
        Assert.Equal(I(42), r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_ErrCopyBooleanPreservesType()
    {
        var t = MkTransform(new List<FieldMapping> { CopyField("Out", "@.val") });
        var r = TransformEngine.Execute(t, Obj(("val", B(true))));
        Assert.True(r.Success);
        Assert.Equal(B(true), r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_ErrCopyFloatPreservesType()
    {
        var t = MkTransform(new List<FieldMapping> { CopyField("Out", "@.val") });
        var r = TransformEngine.Execute(t, Obj(("val", F(3.14))));
        Assert.True(r.Success);
        Assert.True(System.Math.Abs(r.Output!.Get("Out")!.AsDouble().GetValueOrDefault() - 3.14) < 0.001);
    }

    [Fact]
    public void Ext_ErrCopyNullPreservesNull()
    {
        var t = MkTransform(new List<FieldMapping> { CopyField("Out", "@.val") });
        var r = TransformEngine.Execute(t, Obj(("val", N())));
        Assert.True(r.Success);
        Assert.True(r.Output!.Get("Out")!.IsNull);
    }

    // =========================================================================
    // 10. typeOf in engine context (~6 tests)
    // =========================================================================

    [Fact]
    public void Ext_TypeOfString()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "typeOf", new List<VerbArg> { RefArg("@.val") }) });
        var r = TransformEngine.Execute(t, Obj(("val", S("hello"))));
        Assert.True(r.Success);
        Assert.Equal(S("string"), r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_TypeOfInteger()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "typeOf", new List<VerbArg> { RefArg("@.val") }) });
        var r = TransformEngine.Execute(t, Obj(("val", I(42))));
        Assert.True(r.Success);
        var v = r.Output!.Get("Out")!.AsString()!;
        Assert.True(v == "number" || v == "integer");
    }

    [Fact]
    public void Ext_TypeOfBoolean()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "typeOf", new List<VerbArg> { RefArg("@.val") }) });
        var r = TransformEngine.Execute(t, Obj(("val", B(true))));
        Assert.True(r.Success);
        Assert.Equal(S("boolean"), r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_TypeOfNull()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "typeOf", new List<VerbArg> { RefArg("@.missing") }) });
        var r = TransformEngine.Execute(t, Obj());
        Assert.True(r.Success);
        Assert.Equal(S("null"), r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_TypeOfArray()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "typeOf", new List<VerbArg> { RefArg("@.val") }) });
        var r = TransformEngine.Execute(t, Obj(("val", Arr(I(1)))));
        Assert.True(r.Success);
        Assert.Equal(S("array"), r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_TypeOfObject()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "typeOf", new List<VerbArg> { RefArg("@.val") }) });
        var r = TransformEngine.Execute(t, Obj(("val", Obj(("x", I(1))))));
        Assert.True(r.Success);
        Assert.Equal(S("object"), r.Output!.Get("Out"));
    }

    // =========================================================================
    // 11. Comparison verbs in engine (~10 tests)
    // =========================================================================

    [Fact]
    public void Ext_EqSameStrings()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "eq", new List<VerbArg> { RefArg("@.a"), RefArg("@.b") }) });
        var r = TransformEngine.Execute(t, Obj(("a", S("x")), ("b", S("x"))));
        Assert.True(r.Success);
        Assert.Equal(B(true), r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_EqDifferentStrings()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "eq", new List<VerbArg> { RefArg("@.a"), RefArg("@.b") }) });
        var r = TransformEngine.Execute(t, Obj(("a", S("x")), ("b", S("y"))));
        Assert.True(r.Success);
        Assert.Equal(B(false), r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_NeDifferent()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "ne", new List<VerbArg> { RefArg("@.a"), RefArg("@.b") }) });
        var r = TransformEngine.Execute(t, Obj(("a", S("x")), ("b", S("y"))));
        Assert.True(r.Success);
        Assert.Equal(B(true), r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_NotTrue()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "not", new List<VerbArg> { RefArg("@.val") }) });
        var r = TransformEngine.Execute(t, Obj(("val", B(true))));
        Assert.True(r.Success);
        Assert.Equal(B(false), r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_NotFalse()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "not", new List<VerbArg> { RefArg("@.val") }) });
        var r = TransformEngine.Execute(t, Obj(("val", B(false))));
        Assert.True(r.Success);
        Assert.Equal(B(true), r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_AndTrueTrue()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "and", new List<VerbArg> { RefArg("@.a"), RefArg("@.b") }) });
        var r = TransformEngine.Execute(t, Obj(("a", B(true)), ("b", B(true))));
        Assert.True(r.Success);
        Assert.Equal(B(true), r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_AndTrueFalse()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "and", new List<VerbArg> { RefArg("@.a"), RefArg("@.b") }) });
        var r = TransformEngine.Execute(t, Obj(("a", B(true)), ("b", B(false))));
        Assert.True(r.Success);
        Assert.Equal(B(false), r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_OrFalseTrue()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "or", new List<VerbArg> { RefArg("@.a"), RefArg("@.b") }) });
        var r = TransformEngine.Execute(t, Obj(("a", B(false)), ("b", B(true))));
        Assert.True(r.Success);
        Assert.Equal(B(true), r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_LtTrue()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "lt", new List<VerbArg> { RefArg("@.a"), RefArg("@.b") }) });
        var r = TransformEngine.Execute(t, Obj(("a", I(1)), ("b", I(2))));
        Assert.True(r.Success);
        Assert.Equal(B(true), r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_GtTrue()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "gt", new List<VerbArg> { RefArg("@.a"), RefArg("@.b") }) });
        var r = TransformEngine.Execute(t, Obj(("a", I(5)), ("b", I(3))));
        Assert.True(r.Success);
        Assert.Equal(B(true), r.Output!.Get("Out"));
    }

    // =========================================================================
    // 12. Lookup tables (~5 tests)
    // =========================================================================

    [Fact]
    public void Ext_LookupTableMatch()
    {
        var t = MkCustom(new List<TransformSegment> { RootSeg(new List<FieldMapping> {
            VerbField("Color", "lookup", new List<VerbArg> { LitStr("colors.name"), RefArg("@.code") })
        }) });
        t.Tables["colors"] = new LookupTable
        {
            Name = "colors",
            Columns = new List<string> { "code", "name" },
            Rows = new List<List<DynValue>> {
                new List<DynValue> { S("R"), S("Red") },
                new List<DynValue> { S("G"), S("Green") },
                new List<DynValue> { S("B"), S("Blue") }
            }
        };
        var r = TransformEngine.Execute(t, Obj(("code", S("G"))));
        Assert.True(r.Success);
        Assert.Equal(S("Green"), r.Output!.Get("Color"));
    }

    [Fact]
    public void Ext_LookupTableNoMatchReturnsNull()
    {
        var t = MkCustom(new List<TransformSegment> { RootSeg(new List<FieldMapping> {
            VerbField("Color", "lookup", new List<VerbArg> { LitStr("colors.name"), RefArg("@.code") })
        }) });
        t.Tables["colors"] = new LookupTable
        {
            Name = "colors",
            Columns = new List<string> { "code", "name" },
            Rows = new List<List<DynValue>> { new List<DynValue> { S("R"), S("Red") } }
        };
        var r = TransformEngine.Execute(t, Obj(("code", S("X"))));
        Assert.True(r.Success);
        Assert.True(r.Output!.Get("Color")!.IsNull);
    }

    [Fact]
    public void Ext_LookupTableWithDefault()
    {
        var t = MkCustom(new List<TransformSegment> { RootSeg(new List<FieldMapping> {
            VerbField("Color", "lookup", new List<VerbArg> { LitStr("colors.name"), RefArg("@.code") })
        }) });
        t.Tables["colors"] = new LookupTable
        {
            Name = "colors",
            Columns = new List<string> { "code", "name" },
            Rows = new List<List<DynValue>> { new List<DynValue> { S("R"), S("Red") } },
            Default = S("Unknown")
        };
        var r = TransformEngine.Execute(t, Obj(("code", S("X"))));
        Assert.True(r.Success);
        Assert.Equal(S("Unknown"), r.Output!.Get("Color"));
    }

    // =========================================================================
    // 13. Additional engine tests (~15 tests)
    // =========================================================================

    [Fact]
    public void Ext_IsNullTrue()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "isNull", new List<VerbArg> { RefArg("@.missing") }) });
        var r = TransformEngine.Execute(t, Obj());
        Assert.True(r.Success);
        Assert.Equal(B(true), r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_IsNullFalse()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "isNull", new List<VerbArg> { RefArg("@.val") }) });
        var r = TransformEngine.Execute(t, Obj(("val", S("x"))));
        Assert.True(r.Success);
        Assert.Equal(B(false), r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_IsNumberTrue()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "isNumber", new List<VerbArg> { RefArg("@.val") }) });
        var r = TransformEngine.Execute(t, Obj(("val", I(42))));
        Assert.True(r.Success);
        Assert.Equal(B(true), r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_AddStringNumbers()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "add", new List<VerbArg> { RefArg("@.a"), RefArg("@.b") }) });
        var r = TransformEngine.Execute(t, Obj(("a", S("5")), ("b", S("3"))));
        Assert.True(r.Success);
        var val = r.Output!.Get("Out");
        Assert.True(val!.AsDouble() == 8.0 || val.AsInt64() == 8);
    }

    [Fact]
    public void Ext_LteEqual()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "lte", new List<VerbArg> { RefArg("@.a"), RefArg("@.b") }) });
        var r = TransformEngine.Execute(t, Obj(("a", I(5)), ("b", I(5))));
        Assert.True(r.Success);
        Assert.Equal(B(true), r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_GteEqual()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "gte", new List<VerbArg> { RefArg("@.a"), RefArg("@.b") }) });
        var r = TransformEngine.Execute(t, Obj(("a", I(5)), ("b", I(5))));
        Assert.True(r.Success);
        Assert.Equal(B(true), r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_OrFalseFalse()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "or", new List<VerbArg> { RefArg("@.a"), RefArg("@.b") }) });
        var r = TransformEngine.Execute(t, Obj(("a", B(false)), ("b", B(false))));
        Assert.True(r.Success);
        Assert.Equal(B(false), r.Output!.Get("Out"));
    }

    [Fact]
    public void Ext_TitleCase()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "titleCase", new List<VerbArg> { RefArg("@.val") }) });
        var r = TransformEngine.Execute(t, Obj(("val", S("hello world"))));
        Assert.True(r.Success);
        var v = r.Output!.Get("Out")!.AsString()!;
        Assert.StartsWith("H", v);
        Assert.Contains("W", v);
    }

    [Fact]
    public void Ext_CamelCase()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "camelCase", new List<VerbArg> { RefArg("@.val") }) });
        var r = TransformEngine.Execute(t, Obj(("val", S("hello world"))));
        Assert.True(r.Success);
        Assert.StartsWith("h", r.Output!.Get("Out")!.AsString()!);
    }

    [Fact]
    public void Ext_SnakeCase()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "snakeCase", new List<VerbArg> { RefArg("@.val") }) });
        var r = TransformEngine.Execute(t, Obj(("val", S("helloWorld"))));
        Assert.True(r.Success);
        Assert.Contains("_", r.Output!.Get("Out")!.AsString()!);
    }

    [Fact]
    public void Ext_KebabCase()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "kebabCase", new List<VerbArg> { RefArg("@.val") }) });
        var r = TransformEngine.Execute(t, Obj(("val", S("helloWorld"))));
        Assert.True(r.Success);
        Assert.Contains("-", r.Output!.Get("Out")!.AsString()!);
    }

    [Fact]
    public void Ext_Base64EncodeDecode()
    {
        var t = MkTransform(new List<FieldMapping> {
            VerbField("Encoded", "base64Encode", new List<VerbArg> { RefArg("@.val") }),
            VerbField("Decoded", "base64Decode", new List<VerbArg> { Nested("base64Encode", new List<VerbArg> { RefArg("@.val") }) })
        });
        var r = TransformEngine.Execute(t, Obj(("val", S("hello"))));
        Assert.True(r.Success);
        Assert.Equal(S("hello"), r.Output!.Get("Decoded"));
    }

    [Fact]
    public void Ext_MultipleMappingsToSameTargetLastWins()
    {
        var t = MkTransform(new List<FieldMapping> {
            LitField("Val", new OdinString("first")),
            LitField("Val", new OdinString("second"))
        });
        var r = TransformEngine.Execute(t, Obj());
        Assert.True(r.Success);
        Assert.Equal(S("second"), r.Output!.Get("Val"));
    }

    [Fact]
    public void Ext_Truncate()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Out", "truncate", new List<VerbArg> { RefArg("@.val"), LitInt(5) }) });
        var r = TransformEngine.Execute(t, Obj(("val", S("hello world"))));
        Assert.True(r.Success);
        Assert.True(r.Output!.Get("Out")!.AsString()!.Length <= 8);
    }

    [Fact]
    public void Ext_EmptySegmentsProducesEmptyOutput()
    {
        var t = MkCustom(new List<TransformSegment>());
        var r = TransformEngine.Execute(t, Obj(("x", I(1))));
        Assert.True(r.Success);
    }

    // =========================================================================
    // 14. Additional verb coverage from Rust extended_tests_2 (~30 tests)
    // =========================================================================

    [Fact]
    public void Ext2_UpperOnStringField()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Name", "upper", new List<VerbArg> { RefArg("@.name") }) });
        var r = TransformEngine.Execute(t, Obj(("name", S("alice"))));
        Assert.True(r.Success);
        Assert.Equal(S("ALICE"), r.Output!.Get("Name"));
    }

    [Fact]
    public void Ext2_LowerOnStringField()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Name", "lower", new List<VerbArg> { RefArg("@.name") }) });
        var r = TransformEngine.Execute(t, Obj(("name", S("HELLO"))));
        Assert.True(r.Success);
        Assert.Equal(S("hello"), r.Output!.Get("Name"));
    }

    [Fact]
    public void Ext2_UpperOnNumericString()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Val", "upper", new List<VerbArg> { RefArg("@.num") }) });
        var r = TransformEngine.Execute(t, Obj(("num", S("abc123"))));
        Assert.True(r.Success);
        Assert.Equal(S("ABC123"), r.Output!.Get("Val"));
    }

    [Fact]
    public void Ext2_TrimString()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Val", "trim", new List<VerbArg> { RefArg("@.name") }) });
        var r = TransformEngine.Execute(t, Obj(("name", S("  hello  "))));
        Assert.True(r.Success);
        Assert.Equal(S("hello"), r.Output!.Get("Val"));
    }

    [Fact]
    public void Ext2_ConcatMultipleStrings()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Full", "concat", new List<VerbArg> { RefArg("@.first"), LitStr(" "), RefArg("@.last") }) });
        var r = TransformEngine.Execute(t, Obj(("first", S("John")), ("last", S("Doe"))));
        Assert.True(r.Success);
        Assert.Equal(S("John Doe"), r.Output!.Get("Full"));
    }

    [Fact]
    public void Ext2_AddTwoIntegers()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Sum", "add", new List<VerbArg> { RefArg("@.a"), RefArg("@.b") }) });
        var r = TransformEngine.Execute(t, Obj(("a", I(10)), ("b", I(20))));
        Assert.True(r.Success);
        Assert.Equal(I(30), r.Output!.Get("Sum"));
    }

    [Fact]
    public void Ext2_AddIntegerAndFloat()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Sum", "add", new List<VerbArg> { RefArg("@.a"), RefArg("@.b") }) });
        var r = TransformEngine.Execute(t, Obj(("a", I(10)), ("b", F(2.5))));
        Assert.True(r.Success);
        var val = r.Output!.Get("Sum");
        Assert.True(System.Math.Abs(val!.AsDouble().GetValueOrDefault() - 12.5) < 0.1 || val.AsInt64() == 12);
    }

    [Fact]
    public void Ext2_MultiplyIntegers()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Product", "multiply", new List<VerbArg> { RefArg("@.a"), RefArg("@.b") }) });
        var r = TransformEngine.Execute(t, Obj(("a", I(7)), ("b", I(6))));
        Assert.True(r.Success);
        Assert.Equal(I(42), r.Output!.Get("Product"));
    }

    [Fact]
    public void Ext2_SubtractIntegers()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Diff", "subtract", new List<VerbArg> { RefArg("@.a"), RefArg("@.b") }) });
        var r = TransformEngine.Execute(t, Obj(("a", I(100)), ("b", I(42))));
        Assert.True(r.Success);
        Assert.Equal(I(58), r.Output!.Get("Diff"));
    }

    [Fact]
    public void Ext2_CoerceStringFromInteger()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Val", "coerceString", new List<VerbArg> { RefArg("@.num") }) });
        var r = TransformEngine.Execute(t, Obj(("num", I(42))));
        Assert.True(r.Success);
        Assert.Equal(S("42"), r.Output!.Get("Val"));
    }

    [Fact]
    public void Ext2_CoerceNumberFromStringDecimal()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Val", "coerceNumber", new List<VerbArg> { RefArg("@.num") }) });
        var r = TransformEngine.Execute(t, Obj(("num", S("3.14"))));
        Assert.True(r.Success);
        Assert.True(System.Math.Abs(r.Output!.Get("Val")!.AsDouble().GetValueOrDefault() - 3.14) < 0.001);
    }

    [Fact]
    public void Ext2_CoerceBooleanFromStringTrue()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Val", "coerceBoolean", new List<VerbArg> { RefArg("@.flag") }) });
        var r = TransformEngine.Execute(t, Obj(("flag", S("true"))));
        Assert.True(r.Success);
        Assert.Equal(B(true), r.Output!.Get("Val"));
    }

    [Fact]
    public void Ext2_IsNullOnNullValue()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Val", "isNull", new List<VerbArg> { RefArg("@.missing") }) });
        var r = TransformEngine.Execute(t, Obj());
        Assert.True(r.Success);
        Assert.Equal(B(true), r.Output!.Get("Val"));
    }

    [Fact]
    public void Ext2_IsNullOnPresentValue()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Val", "isNull", new List<VerbArg> { RefArg("@.name") }) });
        var r = TransformEngine.Execute(t, Obj(("name", S("Alice"))));
        Assert.True(r.Success);
        Assert.Equal(B(false), r.Output!.Get("Val"));
    }

    [Fact]
    public void Ext2_SubstringVerb()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Val", "substring", new List<VerbArg> { RefArg("@.text"), LitInt(0), LitInt(5) }) });
        var r = TransformEngine.Execute(t, Obj(("text", S("hello world"))));
        Assert.True(r.Success);
        Assert.Equal(S("hello"), r.Output!.Get("Val"));
    }

    [Fact]
    public void Ext2_LengthOfString()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Len", "length", new List<VerbArg> { RefArg("@.text") }) });
        var r = TransformEngine.Execute(t, Obj(("text", S("hello"))));
        Assert.True(r.Success);
        Assert.Equal(I(5), r.Output!.Get("Len"));
    }

    [Fact]
    public void Ext2_AbsNegativeInteger()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Val", "abs", new List<VerbArg> { RefArg("@.num") }) });
        var r = TransformEngine.Execute(t, Obj(("num", I(-42))));
        Assert.True(r.Success);
        Assert.Equal(I(42), r.Output!.Get("Val"));
    }

    [Fact]
    public void Ext2_RoundFloat()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Val", "round", new List<VerbArg> { RefArg("@.num") }) });
        var r = TransformEngine.Execute(t, Obj(("num", F(3.7))));
        Assert.True(r.Success);
        var val = r.Output!.Get("Val");
        Assert.True(val!.AsInt64() == 4 || val.AsDouble() == 4.0);
    }

    [Fact]
    public void Ext2_NotVerb()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Val", "not", new List<VerbArg> { RefArg("@.flag") }) });
        var r = TransformEngine.Execute(t, Obj(("flag", B(true))));
        Assert.True(r.Success);
        Assert.Equal(B(false), r.Output!.Get("Val"));
    }

    [Fact]
    public void Ext2_CapitalizeVerb()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Val", "capitalize", new List<VerbArg> { RefArg("@.name") }) });
        var r = TransformEngine.Execute(t, Obj(("name", S("hello"))));
        Assert.True(r.Success);
        Assert.Equal(S("Hello"), r.Output!.Get("Val"));
    }

    [Fact]
    public void Ext2_ReplaceVerb()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Val", "replace", new List<VerbArg> { RefArg("@.text"), LitStr("world"), LitStr("earth") }) });
        var r = TransformEngine.Execute(t, Obj(("text", S("hello world"))));
        Assert.True(r.Success);
        Assert.Equal(S("hello earth"), r.Output!.Get("Val"));
    }

    // =========================================================================
    // 15. Confidential enforcement via engine (ext2 parity) (~15 tests)
    // =========================================================================

    [Fact]
    public void Ext2_ConfidentialRedactString()
    {
        var t = MkTransform(new List<FieldMapping> { ModField("SSN", "@.ssn", new OdinModifiers { Confidential = true }) });
        t.EnforceConfidential = ConfidentialMode.Redact;
        var r = TransformEngine.Execute(t, Obj(("ssn", S("123-45-6789"))));
        Assert.True(r.Success);
        Assert.True(r.Output!.Get("SSN")!.IsNull);
    }

    [Fact]
    public void Ext2_ConfidentialRedactInteger()
    {
        var t = MkTransform(new List<FieldMapping> { ModField("PIN", "@.pin", new OdinModifiers { Confidential = true }) });
        t.EnforceConfidential = ConfidentialMode.Redact;
        var r = TransformEngine.Execute(t, Obj(("pin", I(1234))));
        Assert.True(r.Success);
        Assert.True(r.Output!.Get("PIN")!.IsNull);
    }

    [Fact]
    public void Ext2_ConfidentialRedactBoolean()
    {
        var t = MkTransform(new List<FieldMapping> { ModField("Secret", "@.flag", new OdinModifiers { Confidential = true }) });
        t.EnforceConfidential = ConfidentialMode.Redact;
        var r = TransformEngine.Execute(t, Obj(("flag", B(true))));
        Assert.True(r.Success);
        Assert.True(r.Output!.Get("Secret")!.IsNull);
    }

    [Fact]
    public void Ext2_ConfidentialRedactFloat()
    {
        var t = MkTransform(new List<FieldMapping> { ModField("Balance", "@.bal", new OdinModifiers { Confidential = true }) });
        t.EnforceConfidential = ConfidentialMode.Redact;
        var r = TransformEngine.Execute(t, Obj(("bal", F(1234.56))));
        Assert.True(r.Success);
        Assert.True(r.Output!.Get("Balance")!.IsNull);
    }

    [Fact]
    public void Ext2_ConfidentialMaskStringBecomesAsterisks()
    {
        var t = MkTransform(new List<FieldMapping> { ModField("SSN", "@.ssn", new OdinModifiers { Confidential = true }) });
        t.EnforceConfidential = ConfidentialMode.Mask;
        var r = TransformEngine.Execute(t, Obj(("ssn", S("123-45-6789"))));
        Assert.True(r.Success);
        var val = r.Output!.Get("SSN");
        if (val!.Type == DynValueType.String)
        {
            Assert.Equal(11, val.AsString()!.Length);
            Assert.True(val.AsString()!.All(c => c == '*'));
        }
    }

    [Fact]
    public void Ext2_ConfidentialMaskIntegerBecomesNull()
    {
        var t = MkTransform(new List<FieldMapping> { ModField("PIN", "@.pin", new OdinModifiers { Confidential = true }) });
        t.EnforceConfidential = ConfidentialMode.Mask;
        var r = TransformEngine.Execute(t, Obj(("pin", I(1234))));
        Assert.True(r.Success);
        Assert.True(r.Output!.Get("PIN")!.IsNull);
    }

    [Fact]
    public void Ext2_ConfidentialMaskBooleanBecomesNull()
    {
        var t = MkTransform(new List<FieldMapping> { ModField("Flag", "@.flag", new OdinModifiers { Confidential = true }) });
        t.EnforceConfidential = ConfidentialMode.Mask;
        var r = TransformEngine.Execute(t, Obj(("flag", B(false))));
        Assert.True(r.Success);
        Assert.True(r.Output!.Get("Flag")!.IsNull);
    }

    [Fact]
    public void Ext2_ConfidentialNoEnforcementPassesThrough()
    {
        var t = MkTransform(new List<FieldMapping> { ModField("SSN", "@.ssn", new OdinModifiers { Confidential = true }) });
        var r = TransformEngine.Execute(t, Obj(("ssn", S("123-45-6789"))));
        Assert.True(r.Success);
        Assert.Equal(S("123-45-6789"), r.Output!.Get("SSN"));
    }

    [Fact]
    public void Ext2_ConfidentialModifierRecordedWithRedact()
    {
        var t = MkTransform(new List<FieldMapping> { ModField("SSN", "@.ssn", new OdinModifiers { Confidential = true }) });
        t.EnforceConfidential = ConfidentialMode.Redact;
        var r = TransformEngine.Execute(t, Obj(("ssn", S("123"))));
        Assert.True(r.Success);
        Assert.True(r.OutputModifiers.ContainsKey("SSN"));
        Assert.True(r.OutputModifiers["SSN"].Confidential);
    }

    [Fact]
    public void Ext2_ConfidentialMixedFieldsOnlyConfidentialRedacted()
    {
        var t = MkTransform(new List<FieldMapping> {
            CopyField("Name", "@.name"),
            ModField("SSN", "@.ssn", new OdinModifiers { Confidential = true }),
            CopyField("Email", "@.email")
        });
        t.EnforceConfidential = ConfidentialMode.Redact;
        var r = TransformEngine.Execute(t, Obj(("name", S("Alice")), ("ssn", S("123-45-6789")), ("email", S("a@b.com"))));
        Assert.True(r.Success);
        Assert.Equal(S("Alice"), r.Output!.Get("Name"));
        Assert.True(r.Output!.Get("SSN")!.IsNull);
        Assert.Equal(S("a@b.com"), r.Output!.Get("Email"));
    }

    [Fact]
    public void Ext2_ConfidentialRequiredAndConfidentialBothRecorded()
    {
        var t = MkTransform(new List<FieldMapping> { ModField("Key", "@.key", new OdinModifiers { Required = true, Confidential = true }) });
        t.EnforceConfidential = ConfidentialMode.Redact;
        var r = TransformEngine.Execute(t, Obj(("key", S("secret"))));
        Assert.True(r.Success);
        Assert.True(r.Output!.Get("Key")!.IsNull);
        Assert.True(r.OutputModifiers["Key"].Required);
        Assert.True(r.OutputModifiers["Key"].Confidential);
    }

    [Fact]
    public void Ext2_ConfidentialDeprecatedAndConfidential()
    {
        var t = MkTransform(new List<FieldMapping> { ModField("Old", "@.old", new OdinModifiers { Confidential = true, Deprecated = true }) });
        t.EnforceConfidential = ConfidentialMode.Redact;
        var r = TransformEngine.Execute(t, Obj(("old", S("legacy"))));
        Assert.True(r.Success);
        Assert.True(r.Output!.Get("Old")!.IsNull);
        Assert.True(r.OutputModifiers["Old"].Confidential);
        Assert.True(r.OutputModifiers["Old"].Deprecated);
    }

    [Fact]
    public void Ext2_ConfidentialRedactNullStaysNull()
    {
        var t = MkTransform(new List<FieldMapping> { ModField("Val", "@.missing", new OdinModifiers { Confidential = true }) });
        t.EnforceConfidential = ConfidentialMode.Redact;
        var r = TransformEngine.Execute(t, Obj());
        Assert.True(r.Success);
        Assert.True(r.Output!.Get("Val")!.IsNull);
    }

    [Fact]
    public void Ext2_ConfidentialMaskFloatBecomesNull()
    {
        var t = MkTransform(new List<FieldMapping> { ModField("Amt", "@.amt", new OdinModifiers { Confidential = true }) });
        t.EnforceConfidential = ConfidentialMode.Mask;
        var r = TransformEngine.Execute(t, Obj(("amt", F(99.99))));
        Assert.True(r.Success);
        Assert.True(r.Output!.Get("Amt")!.IsNull);
    }

    // =========================================================================
    // 16. Multi-pass (ext2 parity) (~12 tests)
    // =========================================================================

    [Fact]
    public void Ext2_TwoPassesWithAccumulator()
    {
        var t = MkCustom(new List<TransformSegment> {
            PassSeg(1, new List<FieldMapping> { LitField("P1", new OdinString("pass1")) }),
            PassSeg(2, new List<FieldMapping> { LitField("P2", new OdinString("pass2")) })
        });
        t.Accumulators["counter"] = new AccumulatorDef { Name = "counter", Initial = new OdinInteger(0), Persist = false };
        var r = TransformEngine.Execute(t, Obj());
        Assert.True(r.Success);
        Assert.Equal(S("pass1"), r.Output!.Get("P1"));
        Assert.Equal(S("pass2"), r.Output!.Get("P2"));
    }

    [Fact]
    public void Ext2_PassNoneRunsLast()
    {
        var t = MkCustom(new List<TransformSegment> {
            RootSeg(new List<FieldMapping> { LitField("Last", new OdinString("none")) }),
            PassSeg(1, new List<FieldMapping> { LitField("First", new OdinString("one")) })
        });
        var r = TransformEngine.Execute(t, Obj());
        Assert.True(r.Success);
        Assert.Equal(S("one"), r.Output!.Get("First"));
        Assert.Equal(S("none"), r.Output!.Get("Last"));
    }

    [Fact]
    public void Ext2_FivePasses()
    {
        var segs = Enumerable.Range(1, 5).Select(p =>
            PassSeg(p, new List<FieldMapping> { LitField($"P{p}", new OdinInteger(p)) })
        ).ToList();
        var t = MkCustom(segs);
        var r = TransformEngine.Execute(t, Obj());
        Assert.True(r.Success);
        for (int p = 1; p <= 5; p++)
            Assert.Equal(I(p), r.Output!.Get($"P{p}"));
    }

    [Fact]
    public void Ext2_PassOrderingReverseInput()
    {
        var t = MkCustom(new List<TransformSegment> {
            PassSeg(3, new List<FieldMapping> { LitField("Z", new OdinInteger(3)) }),
            PassSeg(1, new List<FieldMapping> { LitField("A", new OdinInteger(1)) })
        });
        var r = TransformEngine.Execute(t, Obj());
        Assert.True(r.Success);
        Assert.Equal(I(1), r.Output!.Get("A"));
        Assert.Equal(I(3), r.Output!.Get("Z"));
    }

    [Fact]
    public void Ext2_MultipleSegmentsSamePass()
    {
        var t = MkCustom(new List<TransformSegment> {
            PassSeg(1, new List<FieldMapping> { LitField("A", new OdinString("x")) }),
            PassSeg(1, new List<FieldMapping> { LitField("B", new OdinString("y")) }),
            PassSeg(1, new List<FieldMapping> { LitField("C", new OdinString("z")) })
        });
        var r = TransformEngine.Execute(t, Obj());
        Assert.True(r.Success);
        Assert.Equal(S("x"), r.Output!.Get("A"));
        Assert.Equal(S("y"), r.Output!.Get("B"));
        Assert.Equal(S("z"), r.Output!.Get("C"));
    }

    [Fact]
    public void Ext2_PassWithConditionTrue()
    {
        var seg = PassSeg(1, new List<FieldMapping> { LitField("Hit", new OdinString("yes")) });
        seg.Condition = "@.active";
        var t = MkCustom(new List<TransformSegment> { seg });
        var r = TransformEngine.Execute(t, Obj(("active", B(true))));
        Assert.True(r.Success);
        Assert.Equal(S("yes"), r.Output!.Get("Hit"));
    }

    [Fact]
    public void Ext2_PassWithConditionFalseSkips()
    {
        var seg = PassSeg(1, new List<FieldMapping> { LitField("Hit", new OdinString("yes")) });
        seg.Condition = "@.active";
        var t = MkCustom(new List<TransformSegment> { seg });
        var r = TransformEngine.Execute(t, Obj(("active", B(false))));
        Assert.True(r.Success);
        var v = r.Output!.Get("Hit");
        Assert.True(v == null || v.IsNull);
    }

    [Fact]
    public void Ext2_PassWithConditionNullSkips()
    {
        var seg = PassSeg(1, new List<FieldMapping> { LitField("Hit", new OdinString("yes")) });
        seg.Condition = "@.missing";
        var t = MkCustom(new List<TransformSegment> { seg });
        var r = TransformEngine.Execute(t, Obj());
        Assert.True(r.Success);
        var v = r.Output!.Get("Hit");
        Assert.True(v == null || v.IsNull);
    }

    [Fact]
    public void Ext2_PassWithConditionEmptyStringSkips()
    {
        var seg = new TransformSegment
        {
            Pass = 1,
            Condition = "@.val",
            Mappings = new List<FieldMapping> { LitField("Hit", new OdinString("yes")) }
        };
        var t = MkCustom(new List<TransformSegment> { seg });
        var r = TransformEngine.Execute(t, Obj(("val", S(""))));
        Assert.True(r.Success);
        var v = r.Output!.Get("Hit");
        Assert.True(v == null || v.IsNull);
    }

    [Fact]
    public void Ext2_PassWithConditionNonzeroIntRuns()
    {
        var seg = new TransformSegment
        {
            Condition = "@.count",
            Mappings = new List<FieldMapping> { LitField("Hit", new OdinString("yes")) }
        };
        var t = MkCustom(new List<TransformSegment> { seg });
        var r = TransformEngine.Execute(t, Obj(("count", I(5))));
        Assert.True(r.Success);
        Assert.Equal(S("yes"), r.Output!.Get("Hit"));
    }

    [Fact]
    public void Ext2_PassWithConditionZeroIntSkips()
    {
        var seg = new TransformSegment
        {
            Condition = "@.count",
            Mappings = new List<FieldMapping> { LitField("Hit", new OdinString("yes")) }
        };
        var t = MkCustom(new List<TransformSegment> { seg });
        var r = TransformEngine.Execute(t, Obj(("count", I(0))));
        Assert.True(r.Success);
        var v = r.Output!.Get("Hit");
        Assert.True(v == null || v.IsNull);
    }

    // =========================================================================
    // 17. Nested verb expressions (ext2 parity) (~15 tests)
    // =========================================================================

    [Fact]
    public void Ext2_NestedVerbUpperOfConcat()
    {
        var t = MkTransform(new List<FieldMapping> { new FieldMapping {
            Target = "Val",
            Expression = FieldExpression.Transform(new VerbCall {
                Verb = "upper",
                Args = new List<VerbArg> { VerbArg.NestedCall(new VerbCall {
                    Verb = "concat",
                    Args = new List<VerbArg> { RefArg("@.a"), RefArg("@.b") }
                })}
            })
        }});
        var r = TransformEngine.Execute(t, Obj(("a", S("hello")), ("b", S("world"))));
        Assert.True(r.Success);
        Assert.Equal(S("HELLOWORLD"), r.Output!.Get("Val"));
    }

    [Fact]
    public void Ext2_NestedVerbTrimOfUpper()
    {
        var t = MkTransform(new List<FieldMapping> { new FieldMapping {
            Target = "Val",
            Expression = FieldExpression.Transform(new VerbCall {
                Verb = "trim",
                Args = new List<VerbArg> { VerbArg.NestedCall(new VerbCall {
                    Verb = "upper",
                    Args = new List<VerbArg> { RefArg("@.name") }
                })}
            })
        }});
        var r = TransformEngine.Execute(t, Obj(("name", S("  hello  "))));
        Assert.True(r.Success);
        Assert.Equal(S("HELLO"), r.Output!.Get("Val"));
    }

    [Fact]
    public void Ext2_NestedVerbLengthOfConcat()
    {
        var t = MkTransform(new List<FieldMapping> { new FieldMapping {
            Target = "Len",
            Expression = FieldExpression.Transform(new VerbCall {
                Verb = "length",
                Args = new List<VerbArg> { VerbArg.NestedCall(new VerbCall {
                    Verb = "concat",
                    Args = new List<VerbArg> { LitStr("abc"), LitStr("def") }
                })}
            })
        }});
        var r = TransformEngine.Execute(t, Obj());
        Assert.True(r.Success);
        Assert.Equal(I(6), r.Output!.Get("Len"));
    }

    [Fact]
    public void Ext2_NestedVerbAddOfMultiply()
    {
        var t = MkTransform(new List<FieldMapping> { new FieldMapping {
            Target = "Val",
            Expression = FieldExpression.Transform(new VerbCall {
                Verb = "add",
                Args = new List<VerbArg> {
                    VerbArg.NestedCall(new VerbCall {
                        Verb = "multiply",
                        Args = new List<VerbArg> { RefArg("@.a"), LitInt(2) }
                    }),
                    RefArg("@.b")
                }
            })
        }});
        var r = TransformEngine.Execute(t, Obj(("a", I(5)), ("b", I(3))));
        Assert.True(r.Success);
        Assert.Equal(I(13), r.Output!.Get("Val"));
    }

    [Fact]
    public void Ext2_NestedVerbCoalesceWithNull()
    {
        var t = MkTransform(new List<FieldMapping> { new FieldMapping {
            Target = "Val",
            Expression = FieldExpression.Transform(new VerbCall {
                Verb = "coalesce",
                Args = new List<VerbArg> { RefArg("@.missing"), LitStr("default") }
            })
        }});
        var r = TransformEngine.Execute(t, Obj());
        Assert.True(r.Success);
        Assert.Equal(S("default"), r.Output!.Get("Val"));
    }

    [Fact]
    public void Ext2_IfElseTrueBranch()
    {
        var t = MkTransform(new List<FieldMapping> { new FieldMapping {
            Target = "Val",
            Expression = FieldExpression.Transform(new VerbCall {
                Verb = "ifElse",
                Args = new List<VerbArg> { RefArg("@.flag"), LitStr("yes"), LitStr("no") }
            })
        }});
        var r = TransformEngine.Execute(t, Obj(("flag", B(true))));
        Assert.True(r.Success);
        Assert.Equal(S("yes"), r.Output!.Get("Val"));
    }

    [Fact]
    public void Ext2_IfElseFalseBranch()
    {
        var t = MkTransform(new List<FieldMapping> { new FieldMapping {
            Target = "Val",
            Expression = FieldExpression.Transform(new VerbCall {
                Verb = "ifElse",
                Args = new List<VerbArg> { RefArg("@.flag"), LitStr("yes"), LitStr("no") }
            })
        }});
        var r = TransformEngine.Execute(t, Obj(("flag", B(false))));
        Assert.True(r.Success);
        Assert.Equal(S("no"), r.Output!.Get("Val"));
    }

    // =========================================================================
    // 18. Error handling (ext2 parity) (~15 tests)
    // =========================================================================

    [Fact]
    public void Ext2_MissingSourceFieldReturnsNull()
    {
        var t = MkTransform(new List<FieldMapping> { CopyField("Val", "@.missing") });
        var r = TransformEngine.Execute(t, Obj(("other", S("x"))));
        Assert.True(r.Success);
        var v = r.Output!.Get("Val");
        Assert.True(v == null || v.IsNull);
    }

    [Fact]
    public void Ext2_DeeplyNestedMissingField()
    {
        var t = MkTransform(new List<FieldMapping> { CopyField("Val", "@.a.b.c.d.e") });
        var r = TransformEngine.Execute(t, Obj(("a", Obj())));
        Assert.True(r.Success);
        var v = r.Output!.Get("Val");
        Assert.True(v == null || v.IsNull);
    }

    [Fact]
    public void Ext2_EmptySourceObject()
    {
        var t = MkTransform(new List<FieldMapping> { CopyField("Name", "@.name") });
        var r = TransformEngine.Execute(t, Obj());
        Assert.True(r.Success);
        var v = r.Output!.Get("Name");
        Assert.True(v == null || v.IsNull);
    }

    [Fact]
    public void Ext2_CopyFromArrayIndex()
    {
        var t = MkTransform(new List<FieldMapping> { CopyField("First", "@.items[0]") });
        var r = TransformEngine.Execute(t, Obj(("items", Arr(S("alpha"), S("beta")))));
        Assert.True(r.Success);
        Assert.Equal(S("alpha"), r.Output!.Get("First"));
    }

    [Fact]
    public void Ext2_CopyFromArrayOutOfBounds()
    {
        var t = MkTransform(new List<FieldMapping> { CopyField("Val", "@.items[99]") });
        var r = TransformEngine.Execute(t, Obj(("items", Arr(S("only")))));
        Assert.True(r.Success);
        var v = r.Output!.Get("Val");
        Assert.True(v == null || v.IsNull);
    }

    [Fact]
    public void Ext2_MultipleMappingsToSameTargetLastWins()
    {
        var t = MkTransform(new List<FieldMapping> {
            LitField("Val", new OdinString("first")),
            LitField("Val", new OdinString("second"))
        });
        var r = TransformEngine.Execute(t, Obj());
        Assert.True(r.Success);
        Assert.Equal(S("second"), r.Output!.Get("Val"));
    }

    [Fact]
    public void Ext2_NestedTargetPathCreatesObjects()
    {
        var t = MkTransform(new List<FieldMapping> { CopyField("info.name", "@.name"), CopyField("info.age", "@.age") });
        var r = TransformEngine.Execute(t, Obj(("name", S("Alice")), ("age", I(30))));
        Assert.True(r.Success);
        Assert.Equal(S("Alice"), r.Output!.Get("info")!.Get("name"));
        Assert.Equal(I(30), r.Output!.Get("info")!.Get("age"));
    }

    [Fact]
    public void Ext2_DeeplyNestedTargetPath()
    {
        var t = MkTransform(new List<FieldMapping> { CopyField("a.b.c", "@.val") });
        var r = TransformEngine.Execute(t, Obj(("val", S("deep"))));
        Assert.True(r.Success);
        Assert.Equal(S("deep"), r.Output!.Get("a")!.Get("b")!.Get("c"));
    }

    [Fact]
    public void Ext2_LiteralIntegerMapping()
    {
        var t = MkTransform(new List<FieldMapping> { LitField("Val", new OdinInteger(99)) });
        var r = TransformEngine.Execute(t, Obj());
        Assert.True(r.Success);
        Assert.Equal(I(99), r.Output!.Get("Val"));
    }

    [Fact]
    public void Ext2_LiteralBooleanMapping()
    {
        var t = MkTransform(new List<FieldMapping> { LitField("Val", new OdinBoolean(false)) });
        var r = TransformEngine.Execute(t, Obj());
        Assert.True(r.Success);
        Assert.Equal(B(false), r.Output!.Get("Val"));
    }

    [Fact]
    public void Ext2_LiteralNullMapping()
    {
        var t = MkTransform(new List<FieldMapping> { LitField("Val", new OdinNull()) });
        var r = TransformEngine.Execute(t, Obj());
        Assert.True(r.Success);
        Assert.True(r.Output!.Get("Val")!.IsNull);
    }

    [Fact]
    public void Ext2_LoopOverEmptyArray()
    {
        var seg = new TransformSegment
        {
            Name = "Items", Path = "Items",
            SourcePath = "@.items",
            Mappings = new List<FieldMapping> { CopyField("Name", "@_item.name") }
        };
        var t = MkCustom(new List<TransformSegment> { seg });
        var r = TransformEngine.Execute(t, Obj(("items", Arr())));
        Assert.True(r.Success);
    }

    [Fact]
    public void Ext2_LoopOverArrayWithVerb()
    {
        var seg = new TransformSegment
        {
            Name = "Results", Path = "Results",
            SourcePath = "@.items", IsArray = true,
            Mappings = new List<FieldMapping> { VerbField("Lower", "lower", new List<VerbArg> { RefArg("@_item.val") }) }
        };
        var t = MkCustom(new List<TransformSegment> { seg });
        var r = TransformEngine.Execute(t, Obj(("items", Arr(Obj(("val", S("HELLO"))), Obj(("val", S("WORLD")))))));
        Assert.True(r.Success);
        var results = r.Output!.Get("Results")!.AsArray()!;
        Assert.Equal(2, results.Count);
        Assert.Equal(S("hello"), results[0].Get("Lower"));
        Assert.Equal(S("world"), results[1].Get("Lower"));
    }

    [Fact]
    public void Ext2_EqVerbTrue()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Val", "eq", new List<VerbArg> { RefArg("@.a"), RefArg("@.b") }) });
        var r = TransformEngine.Execute(t, Obj(("a", S("x")), ("b", S("x"))));
        Assert.True(r.Success);
        Assert.Equal(B(true), r.Output!.Get("Val"));
    }

    [Fact]
    public void Ext2_EqVerbFalse()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Val", "eq", new List<VerbArg> { RefArg("@.a"), RefArg("@.b") }) });
        var r = TransformEngine.Execute(t, Obj(("a", S("x")), ("b", S("y"))));
        Assert.True(r.Success);
        Assert.Equal(B(false), r.Output!.Get("Val"));
    }

    [Fact]
    public void Ext2_NeVerb()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Val", "ne", new List<VerbArg> { RefArg("@.a"), RefArg("@.b") }) });
        var r = TransformEngine.Execute(t, Obj(("a", I(1)), ("b", I(2))));
        Assert.True(r.Success);
        Assert.Equal(B(true), r.Output!.Get("Val"));
    }

    [Fact]
    public void Ext2_TypeOfString()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("T", "typeOf", new List<VerbArg> { RefArg("@.val") }) });
        var r = TransformEngine.Execute(t, Obj(("val", S("hello"))));
        Assert.True(r.Success);
        Assert.Equal(S("string"), r.Output!.Get("T"));
    }

    [Fact]
    public void Ext2_TypeOfBoolean()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("T", "typeOf", new List<VerbArg> { RefArg("@.val") }) });
        var r = TransformEngine.Execute(t, Obj(("val", B(true))));
        Assert.True(r.Success);
        Assert.Equal(S("boolean"), r.Output!.Get("T"));
    }

    [Fact]
    public void Ext2_TypeOfNull()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("T", "typeOf", new List<VerbArg> { RefArg("@.val") }) });
        var r = TransformEngine.Execute(t, Obj());
        Assert.True(r.Success);
        Assert.Equal(S("null"), r.Output!.Get("T"));
    }

    [Fact]
    public void Ext2_IfNullWithNull()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Val", "ifNull", new List<VerbArg> { RefArg("@.missing"), LitStr("fallback") }) });
        var r = TransformEngine.Execute(t, Obj());
        Assert.True(r.Success);
        Assert.Equal(S("fallback"), r.Output!.Get("Val"));
    }

    [Fact]
    public void Ext2_IfNullWithValue()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Val", "ifNull", new List<VerbArg> { RefArg("@.name"), LitStr("fallback") }) });
        var r = TransformEngine.Execute(t, Obj(("name", S("Alice"))));
        Assert.True(r.Success);
        Assert.Equal(S("Alice"), r.Output!.Get("Val"));
    }

    [Fact]
    public void Ext2_DivideIntegers()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Val", "divide", new List<VerbArg> { RefArg("@.a"), RefArg("@.b") }) });
        var r = TransformEngine.Execute(t, Obj(("a", I(10)), ("b", I(3))));
        Assert.True(r.Success);
        var val = r.Output!.Get("Val");
        Assert.NotNull(val);
    }

    [Fact]
    public void Ext2_GtVerb()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Val", "gt", new List<VerbArg> { RefArg("@.a"), RefArg("@.b") }) });
        var r = TransformEngine.Execute(t, Obj(("a", I(10)), ("b", I(5))));
        Assert.True(r.Success);
        Assert.Equal(B(true), r.Output!.Get("Val"));
    }

    [Fact]
    public void Ext2_LtVerb()
    {
        var t = MkTransform(new List<FieldMapping> { VerbField("Val", "lt", new List<VerbArg> { RefArg("@.a"), RefArg("@.b") }) });
        var r = TransformEngine.Execute(t, Obj(("a", I(3)), ("b", I(5))));
        Assert.True(r.Success);
        Assert.Equal(B(true), r.Output!.Get("Val"));
    }
}
