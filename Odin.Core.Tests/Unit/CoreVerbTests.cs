using System;
using System.Collections.Generic;
using Odin.Core.Types;
using Odin.Core.Transform;
using Odin.Core.Transform.Verbs;
using Xunit;

namespace Odin.Core.Tests.Unit;

public class CoreVerbTests
{
    private readonly VerbRegistry _registry = new VerbRegistry();
    private readonly VerbContext _ctx = new VerbContext();

    private DynValue Invoke(string verb, params DynValue[] args)
        => _registry.Invoke(verb, args, _ctx);

    // ─────────────────────────────────────────────────────────────────
    // concat
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Concat_TwoStrings()
        => Assert.Equal("helloworld", Invoke("concat", DynValue.String("hello"), DynValue.String("world")).AsString());

    [Fact]
    public void Concat_MultipleArgs()
        => Assert.Equal("abc", Invoke("concat", DynValue.String("a"), DynValue.String("b"), DynValue.String("c")).AsString());

    [Fact]
    public void Concat_SkipsNull()
        => Assert.Equal("hello", Invoke("concat", DynValue.String("hello"), DynValue.Null()).AsString());

    [Fact]
    public void Concat_AllNull()
        => Assert.Equal("", Invoke("concat", DynValue.Null(), DynValue.Null()).AsString());

    [Fact]
    public void Concat_NumbersCoerced()
        => Assert.Equal("42", Invoke("concat", DynValue.Integer(42)).AsString());

    [Fact]
    public void Concat_EmptyArgs()
        => Assert.Equal("", Invoke("concat").AsString());

    // ─────────────────────────────────────────────────────────────────
    // upper
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Upper_HappyPath()
        => Assert.Equal("HELLO", Invoke("upper", DynValue.String("hello")).AsString());

    [Fact]
    public void Upper_NullPassthrough()
        => Assert.True(Invoke("upper", DynValue.Null()).IsNull);

    [Fact]
    public void Upper_NoArgs_Throws()
        => Assert.Throws<InvalidOperationException>(() => Invoke("upper"));

    [Fact]
    public void Upper_IntegerThrows()
        => Assert.Throws<InvalidOperationException>(() => Invoke("upper", DynValue.Integer(42)));

    [Fact]
    public void Upper_EmptyString()
        => Assert.Equal("", Invoke("upper", DynValue.String("")).AsString());

    [Fact]
    public void Upper_MixedCase()
        => Assert.Equal("HELLO WORLD", Invoke("upper", DynValue.String("Hello World")).AsString());

    // ─────────────────────────────────────────────────────────────────
    // lower
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Lower_HappyPath()
        => Assert.Equal("hello", Invoke("lower", DynValue.String("HELLO")).AsString());

    [Fact]
    public void Lower_NullPassthrough()
        => Assert.True(Invoke("lower", DynValue.Null()).IsNull);

    [Fact]
    public void Lower_NoArgs_Throws()
        => Assert.Throws<InvalidOperationException>(() => Invoke("lower"));

    [Fact]
    public void Lower_IntegerThrows()
        => Assert.Throws<InvalidOperationException>(() => Invoke("lower", DynValue.Integer(42)));

    // ─────────────────────────────────────────────────────────────────
    // trim, trimLeft, trimRight
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Trim_HappyPath()
        => Assert.Equal("hello", Invoke("trim", DynValue.String("  hello  ")).AsString());

    [Fact]
    public void Trim_NullPassthrough()
        => Assert.True(Invoke("trim", DynValue.Null()).IsNull);

    [Fact]
    public void Trim_NoArgs_Throws()
        => Assert.Throws<InvalidOperationException>(() => Invoke("trim"));

    [Fact]
    public void TrimLeft_HappyPath()
        => Assert.Equal("hello  ", Invoke("trimLeft", DynValue.String("  hello  ")).AsString());

    [Fact]
    public void TrimLeft_NullPassthrough()
        => Assert.True(Invoke("trimLeft", DynValue.Null()).IsNull);

    [Fact]
    public void TrimRight_HappyPath()
        => Assert.Equal("  hello", Invoke("trimRight", DynValue.String("  hello  ")).AsString());

    [Fact]
    public void TrimRight_NullPassthrough()
        => Assert.True(Invoke("trimRight", DynValue.Null()).IsNull);

    // ─────────────────────────────────────────────────────────────────
    // coalesce
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Coalesce_ReturnsFirstNonNull()
        => Assert.Equal("hello", Invoke("coalesce", DynValue.Null(), DynValue.String("hello")).AsString());

    [Fact]
    public void Coalesce_SkipsEmptyString()
        => Assert.Equal("hello", Invoke("coalesce", DynValue.String(""), DynValue.String("hello")).AsString());

    [Fact]
    public void Coalesce_AllNull()
        => Assert.True(Invoke("coalesce", DynValue.Null(), DynValue.Null()).IsNull);

    [Fact]
    public void Coalesce_ReturnsInteger()
        => Assert.Equal(42L, Invoke("coalesce", DynValue.Null(), DynValue.Integer(42)).AsInt64());

    [Fact]
    public void Coalesce_ReturnsFirstValue()
        => Assert.Equal("first", Invoke("coalesce", DynValue.String("first"), DynValue.String("second")).AsString());

    // ─────────────────────────────────────────────────────────────────
    // ifNull
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void IfNull_NullReturnsDefault()
        => Assert.Equal("default", Invoke("ifNull", DynValue.Null(), DynValue.String("default")).AsString());

    [Fact]
    public void IfNull_NonNullReturnsFirst()
        => Assert.Equal("value", Invoke("ifNull", DynValue.String("value"), DynValue.String("default")).AsString());

    [Fact]
    public void IfNull_TooFewArgs_Throws()
        => Assert.Throws<InvalidOperationException>(() => Invoke("ifNull", DynValue.Null()));

    // ─────────────────────────────────────────────────────────────────
    // ifEmpty
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void IfEmpty_NullReturnsDefault()
        => Assert.Equal("default", Invoke("ifEmpty", DynValue.Null(), DynValue.String("default")).AsString());

    [Fact]
    public void IfEmpty_EmptyStringReturnsDefault()
        => Assert.Equal("default", Invoke("ifEmpty", DynValue.String(""), DynValue.String("default")).AsString());

    [Fact]
    public void IfEmpty_NonEmptyReturnsFirst()
        => Assert.Equal("value", Invoke("ifEmpty", DynValue.String("value"), DynValue.String("default")).AsString());

    [Fact]
    public void IfEmpty_TooFewArgs_Throws()
        => Assert.Throws<InvalidOperationException>(() => Invoke("ifEmpty", DynValue.Null()));

    // ─────────────────────────────────────────────────────────────────
    // ifElse
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void IfElse_TruthyReturnsThen()
        => Assert.Equal("yes", Invoke("ifElse", DynValue.Bool(true), DynValue.String("yes"), DynValue.String("no")).AsString());

    [Fact]
    public void IfElse_FalsyReturnsElse()
        => Assert.Equal("no", Invoke("ifElse", DynValue.Bool(false), DynValue.String("yes"), DynValue.String("no")).AsString());

    [Fact]
    public void IfElse_NullIsFalsy()
        => Assert.Equal("no", Invoke("ifElse", DynValue.Null(), DynValue.String("yes"), DynValue.String("no")).AsString());

    [Fact]
    public void IfElse_NonZeroIsTruthy()
        => Assert.Equal("yes", Invoke("ifElse", DynValue.Integer(1), DynValue.String("yes"), DynValue.String("no")).AsString());

    [Fact]
    public void IfElse_ZeroIsFalsy()
        => Assert.Equal("no", Invoke("ifElse", DynValue.Integer(0), DynValue.String("yes"), DynValue.String("no")).AsString());

    [Fact]
    public void IfElse_TooFewArgs_Throws()
        => Assert.Throws<InvalidOperationException>(() => Invoke("ifElse", DynValue.Bool(true), DynValue.String("yes")));

    // ─────────────────────────────────────────────────────────────────
    // and, or, not, xor
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void And_TrueTrue()
        => Assert.Equal(true, Invoke("and", DynValue.Bool(true), DynValue.Bool(true)).AsBool());

    [Fact]
    public void And_TrueFalse()
        => Assert.Equal(false, Invoke("and", DynValue.Bool(true), DynValue.Bool(false)).AsBool());

    [Fact]
    public void And_FalseFalse()
        => Assert.Equal(false, Invoke("and", DynValue.Bool(false), DynValue.Bool(false)).AsBool());

    [Fact]
    public void And_NonBoolThrows()
        => Assert.Throws<InvalidOperationException>(() => Invoke("and", DynValue.String("true"), DynValue.Bool(true)));

    [Fact]
    public void And_TooFewArgs_Throws()
        => Assert.Throws<InvalidOperationException>(() => Invoke("and", DynValue.Bool(true)));

    [Fact]
    public void Or_TrueFalse()
        => Assert.Equal(true, Invoke("or", DynValue.Bool(true), DynValue.Bool(false)).AsBool());

    [Fact]
    public void Or_FalseFalse()
        => Assert.Equal(false, Invoke("or", DynValue.Bool(false), DynValue.Bool(false)).AsBool());

    [Fact]
    public void Or_TooFewArgs_Throws()
        => Assert.Throws<InvalidOperationException>(() => Invoke("or", DynValue.Bool(true)));

    [Fact]
    public void Not_True()
        => Assert.Equal(false, Invoke("not", DynValue.Bool(true)).AsBool());

    [Fact]
    public void Not_False()
        => Assert.Equal(true, Invoke("not", DynValue.Bool(false)).AsBool());

    [Fact]
    public void Not_Null()
        => Assert.Equal(true, Invoke("not", DynValue.Null()).AsBool());

    [Fact]
    public void Not_Zero()
        => Assert.Equal(true, Invoke("not", DynValue.Integer(0)).AsBool());

    [Fact]
    public void Not_NonZero()
        => Assert.Equal(false, Invoke("not", DynValue.Integer(5)).AsBool());

    [Fact]
    public void Not_EmptyString()
        => Assert.Equal(true, Invoke("not", DynValue.String("")).AsBool());

    [Fact]
    public void Not_NonEmptyString()
        => Assert.Equal(false, Invoke("not", DynValue.String("hello")).AsBool());

    [Fact]
    public void Not_EmptyArray()
        => Assert.Equal(true, Invoke("not", DynValue.Array(new List<DynValue>())).AsBool());

    [Fact]
    public void Not_NoArgs()
        => Assert.Equal(false, Invoke("not").AsBool());

    [Fact]
    public void Xor_TrueFalse()
        => Assert.Equal(true, Invoke("xor", DynValue.Bool(true), DynValue.Bool(false)).AsBool());

    [Fact]
    public void Xor_TrueTrue()
        => Assert.Equal(false, Invoke("xor", DynValue.Bool(true), DynValue.Bool(true)).AsBool());

    [Fact]
    public void Xor_FalseFalse()
        => Assert.Equal(false, Invoke("xor", DynValue.Bool(false), DynValue.Bool(false)).AsBool());

    // ─────────────────────────────────────────────────────────────────
    // eq, ne
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Eq_SameIntegers()
        => Assert.Equal(true, Invoke("eq", DynValue.Integer(42), DynValue.Integer(42)).AsBool());

    [Fact]
    public void Eq_DifferentIntegers()
        => Assert.Equal(false, Invoke("eq", DynValue.Integer(42), DynValue.Integer(43)).AsBool());

    [Fact]
    public void Eq_SameStrings()
        => Assert.Equal(true, Invoke("eq", DynValue.String("hello"), DynValue.String("hello")).AsBool());

    [Fact]
    public void Eq_DifferentStrings()
        => Assert.Equal(false, Invoke("eq", DynValue.String("hello"), DynValue.String("world")).AsBool());

    [Fact]
    public void Eq_NullNull()
        => Assert.Equal(true, Invoke("eq", DynValue.Null(), DynValue.Null()).AsBool());

    [Fact]
    public void Eq_CrossTypeIntegerFloat()
        => Assert.Equal(true, Invoke("eq", DynValue.Integer(42), DynValue.Float(42.0)).AsBool());

    [Fact]
    public void Eq_StringNumber()
        => Assert.Equal(true, Invoke("eq", DynValue.String("42"), DynValue.Integer(42)).AsBool());

    [Fact]
    public void Ne_DifferentValues()
        => Assert.Equal(true, Invoke("ne", DynValue.Integer(1), DynValue.Integer(2)).AsBool());

    [Fact]
    public void Ne_SameValues()
        => Assert.Equal(false, Invoke("ne", DynValue.Integer(1), DynValue.Integer(1)).AsBool());

    // ─────────────────────────────────────────────────────────────────
    // lt, lte, gt, gte
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Lt_IntLess()
        => Assert.Equal(true, Invoke("lt", DynValue.Integer(1), DynValue.Integer(2)).AsBool());

    [Fact]
    public void Lt_IntEqual()
        => Assert.Equal(false, Invoke("lt", DynValue.Integer(2), DynValue.Integer(2)).AsBool());

    [Fact]
    public void Lt_IntGreater()
        => Assert.Equal(false, Invoke("lt", DynValue.Integer(3), DynValue.Integer(2)).AsBool());

    [Fact]
    public void Lt_Strings()
        => Assert.Equal(true, Invoke("lt", DynValue.String("abc"), DynValue.String("def")).AsBool());

    [Fact]
    public void Lte_IntEqual()
        => Assert.Equal(true, Invoke("lte", DynValue.Integer(2), DynValue.Integer(2)).AsBool());

    [Fact]
    public void Lte_IntLess()
        => Assert.Equal(true, Invoke("lte", DynValue.Integer(1), DynValue.Integer(2)).AsBool());

    [Fact]
    public void Gt_IntGreater()
        => Assert.Equal(true, Invoke("gt", DynValue.Integer(3), DynValue.Integer(2)).AsBool());

    [Fact]
    public void Gt_IntEqual()
        => Assert.Equal(false, Invoke("gt", DynValue.Integer(2), DynValue.Integer(2)).AsBool());

    [Fact]
    public void Gte_IntEqual()
        => Assert.Equal(true, Invoke("gte", DynValue.Integer(2), DynValue.Integer(2)).AsBool());

    [Fact]
    public void Gte_IntGreater()
        => Assert.Equal(true, Invoke("gte", DynValue.Integer(3), DynValue.Integer(2)).AsBool());

    [Fact]
    public void Gte_IntLess()
        => Assert.Equal(false, Invoke("gte", DynValue.Integer(1), DynValue.Integer(2)).AsBool());

    // ─────────────────────────────────────────────────────────────────
    // between
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Between_InRange()
        => Assert.Equal(true, Invoke("between", DynValue.Integer(5), DynValue.Integer(1), DynValue.Integer(10)).AsBool());

    [Fact]
    public void Between_AtMin()
        => Assert.Equal(true, Invoke("between", DynValue.Integer(1), DynValue.Integer(1), DynValue.Integer(10)).AsBool());

    [Fact]
    public void Between_AtMax()
        => Assert.Equal(true, Invoke("between", DynValue.Integer(10), DynValue.Integer(1), DynValue.Integer(10)).AsBool());

    [Fact]
    public void Between_OutOfRange()
        => Assert.Equal(false, Invoke("between", DynValue.Integer(11), DynValue.Integer(1), DynValue.Integer(10)).AsBool());

    [Fact]
    public void Between_Strings()
        => Assert.Equal(true, Invoke("between", DynValue.String("c"), DynValue.String("a"), DynValue.String("z")).AsBool());

    // ─────────────────────────────────────────────────────────────────
    // isNull, isString, isNumber, isBoolean, isArray, isObject
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void IsNull_Null() => Assert.Equal(true, Invoke("isNull", DynValue.Null()).AsBool());

    [Fact]
    public void IsNull_NonNull() => Assert.Equal(false, Invoke("isNull", DynValue.String("hi")).AsBool());

    [Fact]
    public void IsNull_NoArgs() => Assert.Equal(true, Invoke("isNull").AsBool());

    [Fact]
    public void IsString_String() => Assert.Equal(true, Invoke("isString", DynValue.String("hi")).AsBool());

    [Fact]
    public void IsString_Integer() => Assert.Equal(false, Invoke("isString", DynValue.Integer(42)).AsBool());

    [Fact]
    public void IsString_NoArgs() => Assert.Equal(false, Invoke("isString").AsBool());

    [Fact]
    public void IsNumber_Integer() => Assert.Equal(true, Invoke("isNumber", DynValue.Integer(42)).AsBool());

    [Fact]
    public void IsNumber_Float() => Assert.Equal(true, Invoke("isNumber", DynValue.Float(3.14)).AsBool());

    [Fact]
    public void IsNumber_String() => Assert.Equal(false, Invoke("isNumber", DynValue.String("42")).AsBool());

    [Fact]
    public void IsBoolean_Bool() => Assert.Equal(true, Invoke("isBoolean", DynValue.Bool(true)).AsBool());

    [Fact]
    public void IsBoolean_String() => Assert.Equal(false, Invoke("isBoolean", DynValue.String("true")).AsBool());

    [Fact]
    public void IsArray_Array() => Assert.Equal(true, Invoke("isArray", DynValue.Array(new List<DynValue>())).AsBool());

    [Fact]
    public void IsArray_StringArray() => Assert.Equal(true, Invoke("isArray", DynValue.String("[1,2,3]")).AsBool());

    [Fact]
    public void IsArray_NonArray() => Assert.Equal(false, Invoke("isArray", DynValue.Integer(42)).AsBool());

    [Fact]
    public void IsObject_Object()
    {
        var obj = DynValue.Object(new List<KeyValuePair<string, DynValue>> { new("k", DynValue.String("v")) });
        Assert.Equal(true, Invoke("isObject", obj).AsBool());
    }

    [Fact]
    public void IsObject_StringObject() => Assert.Equal(true, Invoke("isObject", DynValue.String("{\"k\":\"v\"}")).AsBool());

    [Fact]
    public void IsObject_NonObject() => Assert.Equal(false, Invoke("isObject", DynValue.Integer(42)).AsBool());

    // ─────────────────────────────────────────────────────────────────
    // typeOf
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void TypeOf_Null() => Assert.Equal("null", Invoke("typeOf", DynValue.Null()).AsString());

    [Fact]
    public void TypeOf_Bool() => Assert.Equal("boolean", Invoke("typeOf", DynValue.Bool(true)).AsString());

    [Fact]
    public void TypeOf_String() => Assert.Equal("string", Invoke("typeOf", DynValue.String("hi")).AsString());

    [Fact]
    public void TypeOf_Integer() => Assert.Equal("integer", Invoke("typeOf", DynValue.Integer(1)).AsString());

    [Fact]
    public void TypeOf_Float() => Assert.Equal("number", Invoke("typeOf", DynValue.Float(1.5)).AsString());

    [Fact]
    public void TypeOf_Array() => Assert.Equal("array", Invoke("typeOf", DynValue.Array(new List<DynValue>())).AsString());

    [Fact]
    public void TypeOf_Object()
    {
        var obj = DynValue.Object(new List<KeyValuePair<string, DynValue>>());
        Assert.Equal("object", Invoke("typeOf", obj).AsString());
    }

    [Fact]
    public void TypeOf_NoArgs() => Assert.Equal("null", Invoke("typeOf").AsString());

    [Fact]
    public void TypeOf_Currency() => Assert.Equal("currency", Invoke("typeOf", DynValue.Currency(99.99)).AsString());

    [Fact]
    public void TypeOf_Date() => Assert.Equal("date", Invoke("typeOf", DynValue.Date("2024-01-01")).AsString());

    // ─────────────────────────────────────────────────────────────────
    // cond
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Cond_FirstTruthy()
        => Assert.Equal("a", Invoke("cond", DynValue.Bool(true), DynValue.String("a"), DynValue.Bool(true), DynValue.String("b")).AsString());

    [Fact]
    public void Cond_SecondTruthy()
        => Assert.Equal("b", Invoke("cond", DynValue.Bool(false), DynValue.String("a"), DynValue.Bool(true), DynValue.String("b")).AsString());

    [Fact]
    public void Cond_DefaultValue()
        => Assert.Equal("default", Invoke("cond", DynValue.Bool(false), DynValue.String("a"), DynValue.String("default")).AsString());

    [Fact]
    public void Cond_NoMatch()
        => Assert.True(Invoke("cond", DynValue.Bool(false), DynValue.String("a")).IsNull);

    // ─────────────────────────────────────────────────────────────────
    // coerceString
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void CoerceString_Integer()
        => Assert.Equal("42", Invoke("coerceString", DynValue.Integer(42)).AsString());

    [Fact]
    public void CoerceString_Float()
        => Assert.Equal("3.14", Invoke("coerceString", DynValue.Float(3.14)).AsString());

    [Fact]
    public void CoerceString_Bool()
        => Assert.Equal("true", Invoke("coerceString", DynValue.Bool(true)).AsString());

    [Fact]
    public void CoerceString_Null()
        => Assert.True(Invoke("coerceString", DynValue.Null()).IsNull);

    [Fact]
    public void CoerceString_String()
        => Assert.Equal("hello", Invoke("coerceString", DynValue.String("hello")).AsString());

    // ─────────────────────────────────────────────────────────────────
    // coerceNumber
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void CoerceNumber_String()
        => Assert.Equal(42.0, Invoke("coerceNumber", DynValue.String("42")).AsDouble());

    [Fact]
    public void CoerceNumber_IntegerToFloat()
        => Assert.Equal(42.0, Invoke("coerceNumber", DynValue.Integer(42)).AsDouble());

    [Fact]
    public void CoerceNumber_FloatPassthrough()
        => Assert.Equal(3.14, Invoke("coerceNumber", DynValue.Float(3.14)).AsDouble());

    [Fact]
    public void CoerceNumber_BoolTrue()
        => Assert.Equal(1.0, Invoke("coerceNumber", DynValue.Bool(true)).AsDouble());

    [Fact]
    public void CoerceNumber_BoolFalse()
        => Assert.Equal(0.0, Invoke("coerceNumber", DynValue.Bool(false)).AsDouble());

    [Fact]
    public void CoerceNumber_Null()
        => Assert.True(Invoke("coerceNumber", DynValue.Null()).IsNull);

    [Fact]
    public void CoerceNumber_InvalidString_Throws()
        => Assert.Throws<InvalidOperationException>(() => Invoke("coerceNumber", DynValue.String("abc")));

    // ─────────────────────────────────────────────────────────────────
    // coerceInteger
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void CoerceInteger_String()
        => Assert.Equal(42L, Invoke("coerceInteger", DynValue.String("42")).AsInt64());

    [Fact]
    public void CoerceInteger_FloatTruncates()
        => Assert.Equal(3L, Invoke("coerceInteger", DynValue.Float(3.7)).AsInt64());

    [Fact]
    public void CoerceInteger_IntegerPassthrough()
        => Assert.Equal(42L, Invoke("coerceInteger", DynValue.Integer(42)).AsInt64());

    [Fact]
    public void CoerceInteger_BoolTrue()
        => Assert.Equal(1L, Invoke("coerceInteger", DynValue.Bool(true)).AsInt64());

    [Fact]
    public void CoerceInteger_Null()
        => Assert.True(Invoke("coerceInteger", DynValue.Null()).IsNull);

    [Fact]
    public void CoerceInteger_InvalidString_Throws()
        => Assert.Throws<InvalidOperationException>(() => Invoke("coerceInteger", DynValue.String("abc")));

    // ─────────────────────────────────────────────────────────────────
    // coerceBoolean
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void CoerceBoolean_TrueString()
        => Assert.Equal(true, Invoke("coerceBoolean", DynValue.String("true")).AsBool());

    [Fact]
    public void CoerceBoolean_FalseString()
        => Assert.Equal(false, Invoke("coerceBoolean", DynValue.String("false")).AsBool());

    [Fact]
    public void CoerceBoolean_ZeroString()
        => Assert.Equal(false, Invoke("coerceBoolean", DynValue.String("0")).AsBool());

    [Fact]
    public void CoerceBoolean_EmptyString()
        => Assert.Equal(false, Invoke("coerceBoolean", DynValue.String("")).AsBool());

    [Fact]
    public void CoerceBoolean_NoString()
        => Assert.Equal(false, Invoke("coerceBoolean", DynValue.String("no")).AsBool());

    [Fact]
    public void CoerceBoolean_YesString()
        => Assert.Equal(true, Invoke("coerceBoolean", DynValue.String("yes")).AsBool());

    [Fact]
    public void CoerceBoolean_NonZeroInteger()
        => Assert.Equal(true, Invoke("coerceBoolean", DynValue.Integer(5)).AsBool());

    [Fact]
    public void CoerceBoolean_ZeroInteger()
        => Assert.Equal(false, Invoke("coerceBoolean", DynValue.Integer(0)).AsBool());

    [Fact]
    public void CoerceBoolean_Null()
        => Assert.Equal(false, Invoke("coerceBoolean", DynValue.Null()).AsBool());

    [Fact]
    public void CoerceBoolean_BoolPassthrough()
        => Assert.Equal(true, Invoke("coerceBoolean", DynValue.Bool(true)).AsBool());

    // ─────────────────────────────────────────────────────────────────
    // switch
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Switch_MatchesFirst()
        => Assert.Equal("one", Invoke("switch", DynValue.Integer(1), DynValue.Integer(1), DynValue.String("one"), DynValue.Integer(2), DynValue.String("two")).AsString());

    [Fact]
    public void Switch_MatchesSecond()
        => Assert.Equal("two", Invoke("switch", DynValue.Integer(2), DynValue.Integer(1), DynValue.String("one"), DynValue.Integer(2), DynValue.String("two")).AsString());

    [Fact]
    public void Switch_DefaultValue()
        => Assert.Equal("default", Invoke("switch", DynValue.Integer(3), DynValue.Integer(1), DynValue.String("one"), DynValue.String("default")).AsString());

    [Fact]
    public void Switch_NoMatchNoDefault()
        => Assert.True(Invoke("switch", DynValue.Integer(3), DynValue.Integer(1), DynValue.String("one")).IsNull);

    // ─────────────────────────────────────────────────────────────────
    // isFinite, isNaN
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void IsFinite_Integer() => Assert.Equal(true, Invoke("isFinite", DynValue.Integer(42)).AsBool());

    [Fact]
    public void IsFinite_Float() => Assert.Equal(true, Invoke("isFinite", DynValue.Float(3.14)).AsBool());

    [Fact]
    public void IsFinite_Infinity() => Assert.Equal(false, Invoke("isFinite", DynValue.Float(double.PositiveInfinity)).AsBool());

    [Fact]
    public void IsFinite_NaN() => Assert.Equal(false, Invoke("isFinite", DynValue.Float(double.NaN)).AsBool());

    [Fact]
    public void IsFinite_NoArgs() => Assert.Equal(false, Invoke("isFinite").AsBool());

    [Fact]
    public void IsNaN_NaN() => Assert.Equal(true, Invoke("isNaN", DynValue.Float(double.NaN)).AsBool());

    [Fact]
    public void IsNaN_Number() => Assert.Equal(false, Invoke("isNaN", DynValue.Float(42.0)).AsBool());

    [Fact]
    public void IsNaN_String() => Assert.Equal(true, Invoke("isNaN", DynValue.String("abc")).AsBool());

    // ─────────────────────────────────────────────────────────────────
    // Unknown verb throws
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void UnknownVerb_Throws()
        => Assert.Throws<InvalidOperationException>(() => Invoke("nonExistentVerb", DynValue.Null()));
}
