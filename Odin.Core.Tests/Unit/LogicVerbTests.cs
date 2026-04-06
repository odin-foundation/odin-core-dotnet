using System;
using System.Collections.Generic;
using Odin.Core.Types;
using Odin.Core.Transform;
using Odin.Core.Transform.Verbs;
using Xunit;

namespace Odin.Core.Tests.Unit;

/// <summary>
/// Tests for logic, type-checking, comparison, conditional, and coercion verbs.
/// Ported from Rust SDK extended_tests in verbs/mod.rs.
/// </summary>
public class LogicVerbTests
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
    // and
    // =========================================================================

    [Fact]
    public void And_TrueTrue() => Assert.True(Invoke("and", B(true), B(true)).AsBool()!.Value);
    [Fact]
    public void And_TrueFalse() => Assert.False(Invoke("and", B(true), B(false)).AsBool()!.Value);
    [Fact]
    public void And_FalseTrue() => Assert.False(Invoke("and", B(false), B(true)).AsBool()!.Value);
    [Fact]
    public void And_FalseFalse() => Assert.False(Invoke("and", B(false), B(false)).AsBool()!.Value);
    [Fact]
    public void And_TooFewArgs() => Assert.Throws<InvalidOperationException>(() => Invoke("and", B(true)));
    [Fact]
    public void And_NonBool() => Assert.Throws<InvalidOperationException>(() => Invoke("and", I(1), I(0)));

    // =========================================================================
    // or
    // =========================================================================

    [Fact]
    public void Or_TrueTrue() => Assert.True(Invoke("or", B(true), B(true)).AsBool()!.Value);
    [Fact]
    public void Or_TrueFalse() => Assert.True(Invoke("or", B(true), B(false)).AsBool()!.Value);
    [Fact]
    public void Or_FalseTrue() => Assert.True(Invoke("or", B(false), B(true)).AsBool()!.Value);
    [Fact]
    public void Or_FalseFalse() => Assert.False(Invoke("or", B(false), B(false)).AsBool()!.Value);
    [Fact]
    public void Or_TooFewArgs() => Assert.Throws<InvalidOperationException>(() => Invoke("or", B(true)));

    // =========================================================================
    // not
    // =========================================================================

    [Fact]
    public void Not_True() => Assert.False(Invoke("not", B(true)).AsBool()!.Value);
    [Fact]
    public void Not_False() => Assert.True(Invoke("not", B(false)).AsBool()!.Value);
    [Fact]
    public void Not_Null() => Assert.True(Invoke("not", Null()).AsBool()!.Value);
    [Fact]
    public void Not_ZeroInt() => Assert.True(Invoke("not", I(0)).AsBool()!.Value);
    [Fact]
    public void Not_NonzeroInt() => Assert.False(Invoke("not", I(5)).AsBool()!.Value);
    [Fact]
    public void Not_ZeroFloat() => Assert.True(Invoke("not", F(0.0)).AsBool()!.Value);
    [Fact]
    public void Not_EmptyString() => Assert.True(Invoke("not", S("")).AsBool()!.Value);
    [Fact]
    public void Not_FalseString() => Assert.True(Invoke("not", S("false")).AsBool()!.Value);
    [Fact]
    public void Not_NonemptyString() => Assert.False(Invoke("not", S("hello")).AsBool()!.Value);
    [Fact]
    public void Not_EmptyArray() => Assert.True(Invoke("not", Arr()).AsBool()!.Value);
    [Fact]
    public void Not_NonemptyArray() => Assert.False(Invoke("not", Arr(I(1))).AsBool()!.Value);
    [Fact]
    public void Not_NoArgs() => Assert.False(Invoke("not").AsBool()!.Value);

    // =========================================================================
    // xor
    // =========================================================================

    [Fact]
    public void Xor_TrueTrue() => Assert.False(Invoke("xor", B(true), B(true)).AsBool()!.Value);
    [Fact]
    public void Xor_TrueFalse() => Assert.True(Invoke("xor", B(true), B(false)).AsBool()!.Value);
    [Fact]
    public void Xor_FalseTrue() => Assert.True(Invoke("xor", B(false), B(true)).AsBool()!.Value);
    [Fact]
    public void Xor_FalseFalse() => Assert.False(Invoke("xor", B(false), B(false)).AsBool()!.Value);
    [Fact]
    public void Xor_TooFewArgs() => Assert.Throws<InvalidOperationException>(() => Invoke("xor", B(true)));
    [Fact]
    public void Xor_NonBool() => Assert.Throws<InvalidOperationException>(() => Invoke("xor", I(1), I(0)));

    // =========================================================================
    // eq
    // =========================================================================

    [Fact]
    public void Eq_IntsEqual() => Assert.True(Invoke("eq", I(5), I(5)).AsBool()!.Value);
    [Fact]
    public void Eq_IntsNotEqual() => Assert.False(Invoke("eq", I(5), I(6)).AsBool()!.Value);
    [Fact]
    public void Eq_StringsEqual() => Assert.True(Invoke("eq", S("abc"), S("abc")).AsBool()!.Value);
    [Fact]
    public void Eq_StringsNotEqual() => Assert.False(Invoke("eq", S("abc"), S("xyz")).AsBool()!.Value);
    [Fact]
    public void Eq_IntFloatCross() => Assert.True(Invoke("eq", I(5), F(5.0)).AsBool()!.Value);
    [Fact]
    public void Eq_StringIntCoercion() => Assert.True(Invoke("eq", S("42"), I(42)).AsBool()!.Value);
    [Fact]
    public void Eq_Nulls() => Assert.True(Invoke("eq", Null(), Null()).AsBool()!.Value);
    [Fact]
    public void Eq_NullVsString() => Assert.False(Invoke("eq", Null(), S("")).AsBool()!.Value);
    [Fact]
    public void Eq_Bools() => Assert.True(Invoke("eq", B(true), B(true)).AsBool()!.Value);
    [Fact]
    public void Eq_BoolsDifferent() => Assert.False(Invoke("eq", B(true), B(false)).AsBool()!.Value);
    [Fact]
    public void Eq_TooFew() => Assert.Throws<InvalidOperationException>(() => Invoke("eq", I(1)));
    [Fact]
    public void Eq_FloatsEqual() => Assert.True(Invoke("eq", F(3.14), F(3.14)).AsBool()!.Value);
    [Fact]
    public void Eq_FloatsNotEqual() => Assert.False(Invoke("eq", F(3.14), F(2.71)).AsBool()!.Value);

    // =========================================================================
    // ne
    // =========================================================================

    [Fact]
    public void Ne_Equal() => Assert.False(Invoke("ne", I(5), I(5)).AsBool()!.Value);
    [Fact]
    public void Ne_NotEqual() => Assert.True(Invoke("ne", I(5), I(6)).AsBool()!.Value);
    [Fact]
    public void Ne_CrossType() => Assert.False(Invoke("ne", I(5), F(5.0)).AsBool()!.Value);
    [Fact]
    public void Ne_TooFew() => Assert.Throws<InvalidOperationException>(() => Invoke("ne", I(1)));
    [Fact]
    public void Ne_Strings() => Assert.True(Invoke("ne", S("abc"), S("xyz")).AsBool()!.Value);
    [Fact]
    public void Ne_Nulls() => Assert.False(Invoke("ne", Null(), Null()).AsBool()!.Value);

    // =========================================================================
    // lt
    // =========================================================================

    [Fact]
    public void Lt_IntsLess() => Assert.True(Invoke("lt", I(3), I(5)).AsBool()!.Value);
    [Fact]
    public void Lt_IntsEqual() => Assert.False(Invoke("lt", I(5), I(5)).AsBool()!.Value);
    [Fact]
    public void Lt_IntsGreater() => Assert.False(Invoke("lt", I(7), I(5)).AsBool()!.Value);
    [Fact]
    public void Lt_Floats() => Assert.True(Invoke("lt", F(1.5), F(2.5)).AsBool()!.Value);
    [Fact]
    public void Lt_Strings() => Assert.True(Invoke("lt", S("abc"), S("xyz")).AsBool()!.Value);
    [Fact]
    public void Lt_TooFew() => Assert.Throws<InvalidOperationException>(() => Invoke("lt", I(1)));

    // =========================================================================
    // lte
    // =========================================================================

    [Fact]
    public void Lte_Less() => Assert.True(Invoke("lte", I(3), I(5)).AsBool()!.Value);
    [Fact]
    public void Lte_Equal() => Assert.True(Invoke("lte", I(5), I(5)).AsBool()!.Value);
    [Fact]
    public void Lte_Greater() => Assert.False(Invoke("lte", I(7), I(5)).AsBool()!.Value);
    [Fact]
    public void Lte_Strings() => Assert.True(Invoke("lte", S("abc"), S("abc")).AsBool()!.Value);
    [Fact]
    public void Lte_FloatsLess() => Assert.True(Invoke("lte", F(1.0), F(2.0)).AsBool()!.Value);
    [Fact]
    public void Lte_FloatsEqual() => Assert.True(Invoke("lte", F(2.0), F(2.0)).AsBool()!.Value);

    // =========================================================================
    // gt
    // =========================================================================

    [Fact]
    public void Gt_Greater() => Assert.True(Invoke("gt", I(7), I(5)).AsBool()!.Value);
    [Fact]
    public void Gt_Equal() => Assert.False(Invoke("gt", I(5), I(5)).AsBool()!.Value);
    [Fact]
    public void Gt_Less() => Assert.False(Invoke("gt", I(3), I(5)).AsBool()!.Value);
    [Fact]
    public void Gt_Strings() => Assert.True(Invoke("gt", S("xyz"), S("abc")).AsBool()!.Value);
    [Fact]
    public void Gt_Floats() => Assert.True(Invoke("gt", F(3.0), F(2.0)).AsBool()!.Value);

    // =========================================================================
    // gte
    // =========================================================================

    [Fact]
    public void Gte_Greater() => Assert.True(Invoke("gte", I(7), I(5)).AsBool()!.Value);
    [Fact]
    public void Gte_Equal() => Assert.True(Invoke("gte", I(5), I(5)).AsBool()!.Value);
    [Fact]
    public void Gte_Less() => Assert.False(Invoke("gte", I(3), I(5)).AsBool()!.Value);
    [Fact]
    public void Gte_Floats() => Assert.True(Invoke("gte", F(5.0), F(5.0)).AsBool()!.Value);
    [Fact]
    public void Gte_Strings() => Assert.True(Invoke("gte", S("xyz"), S("abc")).AsBool()!.Value);

    // =========================================================================
    // between
    // =========================================================================

    [Fact]
    public void Between_InRange() => Assert.True(Invoke("between", I(5), I(1), I(10)).AsBool()!.Value);
    [Fact]
    public void Between_AtMin() => Assert.True(Invoke("between", I(1), I(1), I(10)).AsBool()!.Value);
    [Fact]
    public void Between_AtMax() => Assert.True(Invoke("between", I(10), I(1), I(10)).AsBool()!.Value);
    [Fact]
    public void Between_Below() => Assert.False(Invoke("between", I(0), I(1), I(10)).AsBool()!.Value);
    [Fact]
    public void Between_Above() => Assert.False(Invoke("between", I(11), I(1), I(10)).AsBool()!.Value);
    [Fact]
    public void Between_Floats() => Assert.True(Invoke("between", F(5.5), F(1.0), F(10.0)).AsBool()!.Value);
    [Fact]
    public void Between_Strings() => Assert.True(Invoke("between", S("dog"), S("cat"), S("fox")).AsBool()!.Value);
    [Fact]
    public void Between_TooFew() => Assert.Throws<InvalidOperationException>(() => Invoke("between", I(5), I(1)));

    // =========================================================================
    // isNull
    // =========================================================================

    [Fact]
    public void IsNull_Null() => Assert.True(Invoke("isNull", Null()).AsBool()!.Value);
    [Fact]
    public void IsNull_String() => Assert.False(Invoke("isNull", S("hi")).AsBool()!.Value);
    [Fact]
    public void IsNull_Int() => Assert.False(Invoke("isNull", I(0)).AsBool()!.Value);
    [Fact]
    public void IsNull_EmptyArgs() => Assert.True(Invoke("isNull").AsBool()!.Value);
    [Fact]
    public void IsNull_Bool() => Assert.False(Invoke("isNull", B(false)).AsBool()!.Value);
    [Fact]
    public void IsNull_EmptyString() => Assert.False(Invoke("isNull", S("")).AsBool()!.Value);

    // =========================================================================
    // isString
    // =========================================================================

    [Fact]
    public void IsString_String() => Assert.True(Invoke("isString", S("hello")).AsBool()!.Value);
    [Fact]
    public void IsString_Empty() => Assert.True(Invoke("isString", S("")).AsBool()!.Value);
    [Fact]
    public void IsString_Int() => Assert.False(Invoke("isString", I(5)).AsBool()!.Value);
    [Fact]
    public void IsString_Null() => Assert.False(Invoke("isString", Null()).AsBool()!.Value);
    [Fact]
    public void IsString_NoArgs() => Assert.False(Invoke("isString").AsBool()!.Value);
    [Fact]
    public void IsString_Bool() => Assert.False(Invoke("isString", B(true)).AsBool()!.Value);
    [Fact]
    public void IsString_Float() => Assert.False(Invoke("isString", F(3.14)).AsBool()!.Value);

    // =========================================================================
    // isNumber
    // =========================================================================

    [Fact]
    public void IsNumber_Int() => Assert.True(Invoke("isNumber", I(42)).AsBool()!.Value);
    [Fact]
    public void IsNumber_Float() => Assert.True(Invoke("isNumber", F(3.14)).AsBool()!.Value);
    [Fact]
    public void IsNumber_String() => Assert.False(Invoke("isNumber", S("42")).AsBool()!.Value);
    [Fact]
    public void IsNumber_Null() => Assert.False(Invoke("isNumber", Null()).AsBool()!.Value);
    [Fact]
    public void IsNumber_Bool() => Assert.False(Invoke("isNumber", B(true)).AsBool()!.Value);
    [Fact]
    public void IsNumber_NoArgs() => Assert.False(Invoke("isNumber").AsBool()!.Value);

    // =========================================================================
    // isBoolean
    // =========================================================================

    [Fact]
    public void IsBoolean_True() => Assert.True(Invoke("isBoolean", B(true)).AsBool()!.Value);
    [Fact]
    public void IsBoolean_False() => Assert.True(Invoke("isBoolean", B(false)).AsBool()!.Value);
    [Fact]
    public void IsBoolean_String() => Assert.False(Invoke("isBoolean", S("true")).AsBool()!.Value);
    [Fact]
    public void IsBoolean_NoArgs() => Assert.False(Invoke("isBoolean").AsBool()!.Value);
    [Fact]
    public void IsBoolean_Int() => Assert.False(Invoke("isBoolean", I(1)).AsBool()!.Value);
    [Fact]
    public void IsBoolean_Null() => Assert.False(Invoke("isBoolean", Null()).AsBool()!.Value);

    // =========================================================================
    // isArray
    // =========================================================================

    [Fact]
    public void IsArray_Array() => Assert.True(Invoke("isArray", Arr(I(1))).AsBool()!.Value);
    [Fact]
    public void IsArray_EmptyArray() => Assert.True(Invoke("isArray", Arr()).AsBool()!.Value);
    [Fact]
    public void IsArray_StringLike() => Assert.True(Invoke("isArray", S("[1,2]")).AsBool()!.Value);
    [Fact]
    public void IsArray_Int() => Assert.False(Invoke("isArray", I(5)).AsBool()!.Value);
    [Fact]
    public void IsArray_NoArgs() => Assert.False(Invoke("isArray").AsBool()!.Value);
    [Fact]
    public void IsArray_Object() => Assert.False(Invoke("isArray", Obj(("a", I(1)))).AsBool()!.Value);
    [Fact]
    public void IsArray_Null() => Assert.False(Invoke("isArray", Null()).AsBool()!.Value);

    // =========================================================================
    // isObject
    // =========================================================================

    [Fact]
    public void IsObject_Object() => Assert.True(Invoke("isObject", Obj(("a", I(1)))).AsBool()!.Value);
    [Fact]
    public void IsObject_StringLike() => Assert.True(Invoke("isObject", S("{}")).AsBool()!.Value);
    [Fact]
    public void IsObject_Array() => Assert.False(Invoke("isObject", Arr()).AsBool()!.Value);
    [Fact]
    public void IsObject_NoArgs() => Assert.False(Invoke("isObject").AsBool()!.Value);
    [Fact]
    public void IsObject_Null() => Assert.False(Invoke("isObject", Null()).AsBool()!.Value);
    [Fact]
    public void IsObject_String() => Assert.False(Invoke("isObject", S("hello")).AsBool()!.Value);

    // =========================================================================
    // isDate
    // =========================================================================

    [Fact]
    public void IsDate_Valid() => Assert.True(Invoke("isDate", S("2024-01-15")).AsBool()!.Value);
    [Fact]
    public void IsDate_Timestamp() => Assert.True(Invoke("isDate", S("2024-01-15T10:30:00")).AsBool()!.Value);
    [Fact]
    public void IsDate_Invalid() => Assert.False(Invoke("isDate", S("not-a-date")).AsBool()!.Value);
    [Fact]
    public void IsDate_Int() => Assert.False(Invoke("isDate", I(20240115)).AsBool()!.Value);
    [Fact]
    public void IsDate_Null() => Assert.False(Invoke("isDate", Null()).AsBool()!.Value);
    [Fact]
    public void IsDate_NoArgs() => Assert.False(Invoke("isDate").AsBool()!.Value);
    [Fact]
    public void IsDate_ShortString() => Assert.False(Invoke("isDate", S("2024")).AsBool()!.Value);

    // =========================================================================
    // typeOf
    // =========================================================================

    [Fact]
    public void TypeOf_Null() => Assert.Equal("null", Invoke("typeOf", Null()).AsString());
    [Fact]
    public void TypeOf_Bool() => Assert.Equal("boolean", Invoke("typeOf", B(true)).AsString());
    [Fact]
    public void TypeOf_String() => Assert.Equal("string", Invoke("typeOf", S("hi")).AsString());
    [Fact]
    public void TypeOf_Integer() => Assert.Equal("integer", Invoke("typeOf", I(42)).AsString());
    [Fact]
    public void TypeOf_Float() => Assert.Equal("number", Invoke("typeOf", F(3.14)).AsString());
    [Fact]
    public void TypeOf_Array() => Assert.Equal("array", Invoke("typeOf", Arr()).AsString());
    [Fact]
    public void TypeOf_Object() => Assert.Equal("object", Invoke("typeOf", Obj()).AsString());
    [Fact]
    public void TypeOf_NoArgs() => Assert.Equal("null", Invoke("typeOf").AsString());

    // =========================================================================
    // cond
    // =========================================================================

    [Fact]
    public void Cond_FirstTrue()
        => Assert.Equal("yes", Invoke("cond", B(true), S("yes"), B(false), S("no")).AsString());

    [Fact]
    public void Cond_SecondTrue()
        => Assert.Equal("no", Invoke("cond", B(false), S("yes"), B(true), S("no")).AsString());

    [Fact]
    public void Cond_Default()
        => Assert.Equal("default", Invoke("cond", B(false), S("yes"), S("default")).AsString());

    [Fact]
    public void Cond_NoMatch()
        => Assert.True(Invoke("cond", B(false), S("yes"), B(false), S("no")).IsNull);

    [Fact]
    public void Cond_Empty()
        => Assert.True(Invoke("cond").IsNull);

    [Fact]
    public void Cond_TruthyInt()
        => Assert.Equal("yes", Invoke("cond", I(1), S("yes"), S("no")).AsString());

    [Fact]
    public void Cond_FalsyZero()
        => Assert.Equal("no", Invoke("cond", I(0), S("yes"), S("no")).AsString());

    // =========================================================================
    // assert
    // =========================================================================

    [Fact]
    public void Assert_Truthy()
    {
        var result = Invoke("assert", B(true));
        Assert.True(result.AsBool()!.Value);
    }

    [Fact]
    public void Assert_Falsy()
        => Assert.Throws<InvalidOperationException>(() => Invoke("assert", B(false)));

    [Fact]
    public void Assert_FalsyWithMessage()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Invoke("assert", B(false), S("custom message")));
        Assert.Contains("custom message", ex.Message);
    }

    [Fact]
    public void Assert_NoArgs()
        => Assert.Throws<InvalidOperationException>(() => Invoke("assert"));

    [Fact]
    public void Assert_TruthyString()
    {
        var result = Invoke("assert", S("hello"));
        Assert.Equal("hello", result.AsString());
    }

    [Fact]
    public void Assert_FalsyNull()
        => Assert.Throws<InvalidOperationException>(() => Invoke("assert", Null()));

    [Fact]
    public void Assert_FalsyEmptyString()
        => Assert.Throws<InvalidOperationException>(() => Invoke("assert", S("")));

    // =========================================================================
    // ifElse
    // =========================================================================

    [Fact]
    public void IfElse_True()
        => Assert.Equal("yes", Invoke("ifElse", B(true), S("yes"), S("no")).AsString());

    [Fact]
    public void IfElse_False()
        => Assert.Equal("no", Invoke("ifElse", B(false), S("yes"), S("no")).AsString());

    [Fact]
    public void IfElse_TruthyInt()
        => Assert.Equal("yes", Invoke("ifElse", I(1), S("yes"), S("no")).AsString());

    [Fact]
    public void IfElse_FalsyZero()
        => Assert.Equal("no", Invoke("ifElse", I(0), S("yes"), S("no")).AsString());

    [Fact]
    public void IfElse_NullIsFalsy()
        => Assert.Equal("no", Invoke("ifElse", Null(), S("yes"), S("no")).AsString());

    [Fact]
    public void IfElse_TooFew()
        => Assert.Throws<InvalidOperationException>(() => Invoke("ifElse", B(true), S("yes")));

    [Fact]
    public void IfElse_TruthyString()
        => Assert.Equal("yes", Invoke("ifElse", S("hello"), S("yes"), S("no")).AsString());

    [Fact]
    public void IfElse_FalsyEmptyString()
        => Assert.Equal("no", Invoke("ifElse", S(""), S("yes"), S("no")).AsString());

    // =========================================================================
    // ifNull
    // =========================================================================

    [Fact]
    public void IfNull_NotNull()
        => Assert.Equal("val", Invoke("ifNull", S("val"), S("default")).AsString());

    [Fact]
    public void IfNull_IsNull()
        => Assert.Equal("default", Invoke("ifNull", Null(), S("default")).AsString());

    [Fact]
    public void IfNull_TooFew()
        => Assert.Throws<InvalidOperationException>(() => Invoke("ifNull", Null()));

    [Fact]
    public void IfNull_EmptyStringNotNull()
        => Assert.Equal("", Invoke("ifNull", S(""), S("default")).AsString());

    [Fact]
    public void IfNull_ZeroNotNull()
        => Assert.Equal(0, Invoke("ifNull", I(0), S("default")).AsInt64());

    [Fact]
    public void IfNull_FalseNotNull()
        => Assert.False(Invoke("ifNull", B(false), S("default")).AsBool()!.Value);

    // =========================================================================
    // ifEmpty
    // =========================================================================

    [Fact]
    public void IfEmpty_NotEmpty()
        => Assert.Equal("val", Invoke("ifEmpty", S("val"), S("default")).AsString());

    [Fact]
    public void IfEmpty_EmptyString()
        => Assert.Equal("default", Invoke("ifEmpty", S(""), S("default")).AsString());

    [Fact]
    public void IfEmpty_Null()
        => Assert.Equal("default", Invoke("ifEmpty", Null(), S("default")).AsString());

    [Fact]
    public void IfEmpty_IntNotEmpty()
        => Assert.Equal(0, Invoke("ifEmpty", I(0), S("default")).AsInt64());

    [Fact]
    public void IfEmpty_TooFew()
        => Assert.Throws<InvalidOperationException>(() => Invoke("ifEmpty", S("")));

    [Fact]
    public void IfEmpty_BoolNotEmpty()
        => Assert.False(Invoke("ifEmpty", B(false), S("default")).AsBool()!.Value);

    // =========================================================================
    // coerceString
    // =========================================================================

    [Fact]
    public void CoerceString_FromString()
        => Assert.Equal("hi", Invoke("coerceString", S("hi")).AsString());

    [Fact]
    public void CoerceString_FromInt()
        => Assert.Equal("42", Invoke("coerceString", I(42)).AsString());

    [Fact]
    public void CoerceString_FromFloat()
        => Assert.Equal("3.14", Invoke("coerceString", F(3.14)).AsString());

    [Fact]
    public void CoerceString_FromBool()
        => Assert.Equal("true", Invoke("coerceString", B(true)).AsString());

    [Fact]
    public void CoerceString_FromNull()
        => Assert.True(Invoke("coerceString", Null()).IsNull);

    [Fact]
    public void CoerceString_NoArgs()
        => Assert.Throws<InvalidOperationException>(() => Invoke("coerceString"));

    [Fact]
    public void CoerceString_FromBoolFalse()
        => Assert.Equal("false", Invoke("coerceString", B(false)).AsString());

    // =========================================================================
    // coerceNumber
    // =========================================================================

    [Fact]
    public void CoerceNumber_FromInt()
        => Assert.Equal(42.0, Invoke("coerceNumber", I(42)).AsDouble());

    [Fact]
    public void CoerceNumber_FromFloat()
        => Assert.Equal(3.14, Invoke("coerceNumber", F(3.14)).AsDouble());

    [Fact]
    public void CoerceNumber_FromString()
        => Assert.Equal(3.14, Invoke("coerceNumber", S("3.14")).AsDouble());

    [Fact]
    public void CoerceNumber_FromBoolTrue()
        => Assert.Equal(1.0, Invoke("coerceNumber", B(true)).AsDouble());

    [Fact]
    public void CoerceNumber_FromBoolFalse()
        => Assert.Equal(0.0, Invoke("coerceNumber", B(false)).AsDouble());

    [Fact]
    public void CoerceNumber_FromNull()
        => Assert.True(Invoke("coerceNumber", Null()).IsNull);

    [Fact]
    public void CoerceNumber_InvalidString()
        => Assert.Throws<InvalidOperationException>(() => Invoke("coerceNumber", S("abc")));

    [Fact]
    public void CoerceNumber_NoArgs()
        => Assert.Throws<InvalidOperationException>(() => Invoke("coerceNumber"));

    // =========================================================================
    // coerceInteger
    // =========================================================================

    [Fact]
    public void CoerceInteger_FromInt()
        => Assert.Equal(42, Invoke("coerceInteger", I(42)).AsInt64());

    [Fact]
    public void CoerceInteger_FromFloat()
        => Assert.Equal(3, Invoke("coerceInteger", F(3.7)).AsInt64());

    [Fact]
    public void CoerceInteger_FromString()
        => Assert.Equal(42, Invoke("coerceInteger", S("42")).AsInt64());

    [Fact]
    public void CoerceInteger_FromStringFloat()
        => Assert.Equal(3, Invoke("coerceInteger", S("3.9")).AsInt64());

    [Fact]
    public void CoerceInteger_FromBool()
        => Assert.Equal(1, Invoke("coerceInteger", B(true)).AsInt64());

    [Fact]
    public void CoerceInteger_FromBoolFalse()
        => Assert.Equal(0, Invoke("coerceInteger", B(false)).AsInt64());

    [Fact]
    public void CoerceInteger_FromNull()
        => Assert.True(Invoke("coerceInteger", Null()).IsNull);

    [Fact]
    public void CoerceInteger_InvalidString()
        => Assert.Throws<InvalidOperationException>(() => Invoke("coerceInteger", S("abc")));

    [Fact]
    public void CoerceInteger_NoArgs()
        => Assert.Throws<InvalidOperationException>(() => Invoke("coerceInteger"));

    // =========================================================================
    // coerceBoolean
    // =========================================================================

    [Fact]
    public void CoerceBoolean_FromTrue() => Assert.True(Invoke("coerceBoolean", B(true)).AsBool()!.Value);
    [Fact]
    public void CoerceBoolean_FromFalse() => Assert.False(Invoke("coerceBoolean", B(false)).AsBool()!.Value);
    [Fact]
    public void CoerceBoolean_FromStringTrue() => Assert.True(Invoke("coerceBoolean", S("true")).AsBool()!.Value);
    [Fact]
    public void CoerceBoolean_FromStringFalse() => Assert.False(Invoke("coerceBoolean", S("false")).AsBool()!.Value);
    [Fact]
    public void CoerceBoolean_FromStringZero() => Assert.False(Invoke("coerceBoolean", S("0")).AsBool()!.Value);
    [Fact]
    public void CoerceBoolean_FromStringEmpty() => Assert.False(Invoke("coerceBoolean", S("")).AsBool()!.Value);
    [Fact]
    public void CoerceBoolean_FromStringNo() => Assert.False(Invoke("coerceBoolean", S("no")).AsBool()!.Value);
    [Fact]
    public void CoerceBoolean_FromStringYes() => Assert.True(Invoke("coerceBoolean", S("yes")).AsBool()!.Value);
    [Fact]
    public void CoerceBoolean_FromIntNonzero() => Assert.True(Invoke("coerceBoolean", I(1)).AsBool()!.Value);
    [Fact]
    public void CoerceBoolean_FromIntZero() => Assert.False(Invoke("coerceBoolean", I(0)).AsBool()!.Value);
    [Fact]
    public void CoerceBoolean_FromNull() => Assert.False(Invoke("coerceBoolean", Null()).AsBool()!.Value);
    [Fact]
    public void CoerceBoolean_FromStringN() => Assert.False(Invoke("coerceBoolean", S("n")).AsBool()!.Value);
    [Fact]
    public void CoerceBoolean_FromStringOff() => Assert.False(Invoke("coerceBoolean", S("off")).AsBool()!.Value);
    [Fact]
    public void CoerceBoolean_NoArgs()
        => Assert.Throws<InvalidOperationException>(() => Invoke("coerceBoolean"));

    // =========================================================================
    // coerceDate
    // =========================================================================

    [Fact]
    public void CoerceDate_Valid()
        => Assert.Equal("2024-01-15", Invoke("coerceDate", S("2024-01-15")).AsString());

    [Fact]
    public void CoerceDate_Timestamp()
        => Assert.Equal("2024-01-15", Invoke("coerceDate", S("2024-01-15T10:30:00")).AsString());

    [Fact]
    public void CoerceDate_Invalid()
        => Assert.Throws<InvalidOperationException>(() => Invoke("coerceDate", S("not-a-date")));

    [Fact]
    public void CoerceDate_Null()
        => Assert.True(Invoke("coerceDate", Null()).IsNull);

    [Fact]
    public void CoerceDate_NoArgs()
        => Assert.Throws<InvalidOperationException>(() => Invoke("coerceDate"));

    // =========================================================================
    // coerceTimestamp
    // =========================================================================

    [Fact]
    public void CoerceTimestamp_Valid()
        => Assert.Equal("2024-01-15T10:30:00", Invoke("coerceTimestamp", S("2024-01-15T10:30:00")).AsString());

    [Fact]
    public void CoerceTimestamp_DateOnly()
        => Assert.Equal("2024-01-15T00:00:00", Invoke("coerceTimestamp", S("2024-01-15")).AsString());

    [Fact]
    public void CoerceTimestamp_Invalid()
        => Assert.Throws<InvalidOperationException>(() => Invoke("coerceTimestamp", S("not-valid")));

    [Fact]
    public void CoerceTimestamp_Null()
        => Assert.True(Invoke("coerceTimestamp", Null()).IsNull);

    [Fact]
    public void CoerceTimestamp_NoArgs()
        => Assert.Throws<InvalidOperationException>(() => Invoke("coerceTimestamp"));

    // =========================================================================
    // tryCoerce
    // =========================================================================

    [Fact]
    public void TryCoerce_Integer()
        => Assert.Equal(42, Invoke("tryCoerce", S("42")).AsInt64());

    [Fact]
    public void TryCoerce_Float()
        => Assert.Equal(3.14, Invoke("tryCoerce", S("3.14")).AsDouble());

    [Fact]
    public void TryCoerce_BoolTrue()
        => Assert.True(Invoke("tryCoerce", S("true")).AsBool()!.Value);

    [Fact]
    public void TryCoerce_BoolFalse()
        => Assert.False(Invoke("tryCoerce", S("false")).AsBool()!.Value);

    [Fact]
    public void TryCoerce_Date()
    {
        var result = Invoke("tryCoerce", S("2024-01-15"));
        Assert.Equal("2024-01-15", result.AsString());
    }

    [Fact]
    public void TryCoerce_PlainString()
        => Assert.Equal("hello", Invoke("tryCoerce", S("hello")).AsString());

    [Fact]
    public void TryCoerce_NoArgs()
        => Assert.True(Invoke("tryCoerce").IsNull);

    [Fact]
    public void TryCoerce_PassthroughInt()
        => Assert.Equal(99, Invoke("tryCoerce", I(99)).AsInt64());

    [Fact]
    public void TryCoerce_PassthroughBool()
        => Assert.True(Invoke("tryCoerce", B(true)).AsBool()!.Value);

    // =========================================================================
    // toArray
    // =========================================================================

    [Fact]
    public void ToArray_FromArray()
    {
        var result = Invoke("toArray", Arr(I(1), I(2)));
        Assert.Equal(2, result.AsArray()!.Count);
    }

    [Fact]
    public void ToArray_FromScalar()
    {
        var result = Invoke("toArray", I(42));
        var arr = result.AsArray()!;
        Assert.Single(arr);
        Assert.Equal(42, arr[0].AsInt64());
    }

    [Fact]
    public void ToArray_NoArgs()
    {
        var result = Invoke("toArray");
        Assert.Empty(result.AsArray()!);
    }

    [Fact]
    public void ToArray_FromString()
    {
        var result = Invoke("toArray", S("hello"));
        var arr = result.AsArray()!;
        Assert.Single(arr);
        Assert.Equal("hello", arr[0].AsString());
    }

    [Fact]
    public void ToArray_FromNull()
    {
        var result = Invoke("toArray", Null());
        var arr = result.AsArray()!;
        Assert.Single(arr);
        Assert.True(arr[0].IsNull);
    }

    // =========================================================================
    // toObject
    // =========================================================================

    [Fact]
    public void ToObject_FromPairs()
    {
        var input = Arr(Arr(S("a"), I(1)), Arr(S("b"), I(2)));
        var result = Invoke("toObject", input);
        var obj = result.AsObject()!;
        Assert.Equal(2, obj.Count);
        Assert.Equal("a", obj[0].Key);
        Assert.Equal(1, obj[0].Value.AsInt64());
        Assert.Equal("b", obj[1].Key);
        Assert.Equal(2, obj[1].Value.AsInt64());
    }

    [Fact]
    public void ToObject_Null()
        => Assert.True(Invoke("toObject", Null()).IsNull);

    [Fact]
    public void ToObject_AlreadyObject()
    {
        var input = Obj(("x", I(1)));
        var result = Invoke("toObject", input);
        Assert.Equal(1, result.AsObject()![0].Value.AsInt64());
    }

    [Fact]
    public void ToObject_NoArgs()
        => Assert.Throws<InvalidOperationException>(() => Invoke("toObject"));

    [Fact]
    public void ToObject_InvalidPairs()
        => Assert.Throws<InvalidOperationException>(() => Invoke("toObject", Arr(I(1), I(2))));

    // =========================================================================
    // coalesce
    // =========================================================================

    [Fact]
    public void Coalesce_FirstNonNull()
        => Assert.Equal("val", Invoke("coalesce", Null(), S(""), S("val")).AsString());

    [Fact]
    public void Coalesce_FirstIsGood()
        => Assert.Equal("val", Invoke("coalesce", S("val"), Null()).AsString());

    [Fact]
    public void Coalesce_AllNull()
        => Assert.True(Invoke("coalesce", Null(), Null()).IsNull);

    [Fact]
    public void Coalesce_AllEmpty()
        => Assert.True(Invoke("coalesce", S(""), S("")).IsNull);

    [Fact]
    public void Coalesce_IntFirst()
        => Assert.Equal(0, Invoke("coalesce", I(0), S("fallback")).AsInt64());

    [Fact]
    public void Coalesce_SkipsNullAndEmpty()
        => Assert.Equal("c", Invoke("coalesce", Null(), S(""), S("c")).AsString());

    [Fact]
    public void Coalesce_BoolFirst()
        => Assert.False(Invoke("coalesce", B(false), S("fallback")).AsBool()!.Value);
}
