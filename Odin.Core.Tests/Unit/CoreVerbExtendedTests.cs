using System;
using System.Collections.Generic;
using Odin.Core.Types;
using Odin.Core.Transform;
using Odin.Core.Transform.Verbs;
using Xunit;

namespace Odin.Core.Tests.Unit;

/// <summary>
/// Extended core verb tests ported from the Rust SDK for cross-language parity.
/// Covers logic, coercion, string, comparison, type checking, and conditional verbs.
/// </summary>
public class CoreVerbExtendedTests
{
    private readonly VerbRegistry _registry = new VerbRegistry();
    private readonly VerbContext _ctx = new VerbContext();

    private DynValue Invoke(string verb, params DynValue[] args)
        => _registry.Invoke(verb, args, _ctx);

    private static DynValue S(string v) => DynValue.String(v);
    private static DynValue I(long v) => DynValue.Integer(v);
    private static DynValue F(double v) => DynValue.Float(v);
    private static DynValue B(bool v) => DynValue.Bool(v);
    private static DynValue Null() => DynValue.Null();
    private static DynValue Arr(params DynValue[] items) => DynValue.Array(new List<DynValue>(items));
    private static DynValue Obj(params (string, DynValue)[] pairs)
    {
        var list = new List<KeyValuePair<string, DynValue>>();
        foreach (var (k, v) in pairs)
            list.Add(new KeyValuePair<string, DynValue>(k, v));
        return DynValue.Object(list);
    }

    // =========================================================================
    // Logic verbs — additional edge cases (from Rust extended_tests_2)
    // =========================================================================

    [Fact] public void And_StringArgs_Throws() => Assert.Throws<InvalidOperationException>(() => Invoke("and", S("true"), S("true")));
    [Fact] public void And_NullArgs_Throws() => Assert.Throws<InvalidOperationException>(() => Invoke("and", Null(), B(true)));
    [Fact] public void And_EmptyArgs_Throws() => Assert.Throws<InvalidOperationException>(() => Invoke("and"));
    [Fact] public void And_FalseTrue() => Assert.Equal(false, Invoke("and", B(false), B(true)).AsBool());

    [Fact] public void Or_NonBool_Throws() => Assert.Throws<InvalidOperationException>(() => Invoke("or", I(1), I(0)));
    [Fact] public void Or_NullArgs_Throws() => Assert.Throws<InvalidOperationException>(() => Invoke("or", Null(), Null()));
    [Fact] public void Or_EmptyArgs_Throws() => Assert.Throws<InvalidOperationException>(() => Invoke("or"));
    [Fact] public void Or_TrueTrue() => Assert.Equal(true, Invoke("or", B(true), B(true)).AsBool());

    [Fact] public void Xor_NullArgs_Throws() => Assert.Throws<InvalidOperationException>(() => Invoke("xor", Null(), B(true)));
    [Fact] public void Xor_EmptyArgs_Throws() => Assert.Throws<InvalidOperationException>(() => Invoke("xor"));
    [Fact] public void Xor_FalseTrue() => Assert.Equal(true, Invoke("xor", B(false), B(true)).AsBool());

    [Fact] public void Not_Object() => Assert.Equal(false, Invoke("not", Obj()).AsBool());
    [Fact] public void Not_NonzeroFloat() => Assert.Equal(false, Invoke("not", F(1.5)).AsBool());
    [Fact] public void Not_FalseString() => Assert.Equal(true, Invoke("not", S("false")).AsBool());

    // =========================================================================
    // Equality — additional edge cases
    // =========================================================================

    [Fact]
    public void Eq_ArraysEqual()
        => Assert.Equal(true, Invoke("eq", Arr(I(1), I(2)), Arr(I(1), I(2))).AsBool());

    [Fact]
    public void Eq_ArraysNotEqual()
        => Assert.Equal(false, Invoke("eq", Arr(I(1)), Arr(I(2))).AsBool());

    [Fact]
    public void Eq_NullNull_Extended()
        => Assert.Equal(true, Invoke("eq", Null(), Null()).AsBool());

    [Fact]
    public void Eq_IntStringNoMatch()
        => Assert.Equal(false, Invoke("eq", I(42), S("abc")).AsBool());

    [Fact] public void Ne_StringsEqual() => Assert.Equal(false, Invoke("ne", S("a"), S("a")).AsBool());
    [Fact] public void Ne_StringsDiffer() => Assert.Equal(true, Invoke("ne", S("a"), S("b")).AsBool());
    [Fact] public void Ne_NullNull_Extended() => Assert.Equal(false, Invoke("ne", Null(), Null()).AsBool());
    [Fact] public void Ne_NullString() => Assert.Equal(true, Invoke("ne", Null(), S("x")).AsBool());

    // =========================================================================
    // Comparison — mixed types and edge cases
    // =========================================================================

    [Fact] public void Lt_IntFloat() => Assert.Equal(true, Invoke("lt", I(3), F(3.5)).AsBool());
    [Fact] public void Lt_FloatInt() => Assert.Equal(false, Invoke("lt", F(3.5), I(3)).AsBool());
    [Fact] public void Lt_Negative() => Assert.Equal(true, Invoke("lt", I(-5), I(-3)).AsBool());

    [Fact] public void Lte_FloatsEqual() => Assert.Equal(true, Invoke("lte", F(3.14), F(3.14)).AsBool());
    [Fact] public void Lte_FloatsLess() => Assert.Equal(true, Invoke("lte", F(1.0), F(2.0)).AsBool());
    [Fact] public void Lte_TooFew_Throws() => Assert.Throws<InvalidOperationException>(() => Invoke("lte", I(1)));

    [Fact] public void Gt_Floats() => Assert.Equal(true, Invoke("gt", F(5.5), F(3.3)).AsBool());
    [Fact] public void Gt_NegativeInts() => Assert.Equal(true, Invoke("gt", I(-1), I(-5)).AsBool());
    [Fact] public void Gt_TooFew_Throws() => Assert.Throws<InvalidOperationException>(() => Invoke("gt", I(1)));

    [Fact] public void Gte_Floats() => Assert.Equal(true, Invoke("gte", F(5.0), F(5.0)).AsBool());
    [Fact] public void Gte_Strings() => Assert.Equal(true, Invoke("gte", S("b"), S("a")).AsBool());
    [Fact] public void Gte_StringsEqual() => Assert.Equal(true, Invoke("gte", S("a"), S("a")).AsBool());
    [Fact] public void Gte_TooFew_Throws() => Assert.Throws<InvalidOperationException>(() => Invoke("gte", I(1)));

    [Fact] public void Between_NegativeRange() => Assert.Equal(true, Invoke("between", I(-5), I(-10), I(0)).AsBool());
    [Fact] public void Between_FloatOutside() => Assert.Equal(false, Invoke("between", F(0.5), F(1.0), F(10.0)).AsBool());

    // =========================================================================
    // Conditional — extended
    // =========================================================================

    [Fact]
    public void Cond_MultiplePairsThirdTrue()
        => Assert.Equal("c", Invoke("cond", B(false), S("a"), B(false), S("b"), B(true), S("c")).AsString());

    [Fact]
    public void Cond_TruthyInteger()
        => Assert.Equal("yes", Invoke("cond", I(1), S("yes"), S("no")).AsString());

    [Fact]
    public void Cond_AllFalseWithDefault()
        => Assert.Equal("default", Invoke("cond", B(false), S("a"), B(false), S("b"), S("default")).AsString());

    [Fact]
    public void IfElse_TruthyString()
        => Assert.Equal("yes", Invoke("ifElse", S("x"), S("yes"), S("no")).AsString());

    [Fact]
    public void IfElse_FalsyEmptyStr()
        => Assert.Equal("no", Invoke("ifElse", S(""), S("yes"), S("no")).AsString());

    [Fact]
    public void IfNull_IntValue()
        => Assert.Equal(0L, Invoke("ifNull", I(0), S("default")).AsInt64());

    [Fact]
    public void IfNull_EmptyStringNotNull()
        => Assert.Equal("", Invoke("ifNull", S(""), S("default")).AsString());

    [Fact]
    public void IfEmpty_BoolNotEmpty()
        => Assert.Equal(false, Invoke("ifEmpty", B(false), S("default")).AsBool());

    [Fact]
    public void IfEmpty_FloatNotEmpty()
    {
        var result = Invoke("ifEmpty", F(0.0), S("default"));
        Assert.Equal(0.0, result.AsDouble());
    }

    // =========================================================================
    // Switch — additional
    // =========================================================================

    [Fact]
    public void Switch_IntMatch()
        => Assert.Equal("two", Invoke("switch", I(2), I(1), S("one"), I(2), S("two")).AsString());

    [Fact]
    public void Switch_IntDefault()
        => Assert.Equal("other", Invoke("switch", I(3), I(1), S("one"), S("other")).AsString());

    [Fact]
    public void Switch_SingleValueNoPairs()
        => Assert.True(Invoke("switch", S("x")).IsNull);

    // =========================================================================
    // Type checking — additional edge cases
    // =========================================================================

    [Fact] public void IsNull_Bool() => Assert.Equal(false, Invoke("isNull", B(false)).AsBool());
    [Fact] public void IsNull_Float() => Assert.Equal(false, Invoke("isNull", F(0.0)).AsBool());
    [Fact] public void IsNull_Array() => Assert.Equal(false, Invoke("isNull", Arr()).AsBool());
    [Fact] public void IsNull_Object_Extended() => Assert.Equal(false, Invoke("isNull", Obj()).AsBool());

    [Fact] public void IsString_Float() => Assert.Equal(false, Invoke("isString", F(3.14)).AsBool());
    [Fact] public void IsString_Bool() => Assert.Equal(false, Invoke("isString", B(true)).AsBool());
    [Fact] public void IsString_Array() => Assert.Equal(false, Invoke("isString", Arr()).AsBool());

    [Fact] public void IsNumber_Object() => Assert.Equal(false, Invoke("isNumber", Obj()).AsBool());
    [Fact] public void IsNumber_Array() => Assert.Equal(false, Invoke("isNumber", Arr()).AsBool());

    [Fact] public void IsBoolean_Int() => Assert.Equal(false, Invoke("isBoolean", I(1)).AsBool());
    [Fact] public void IsBoolean_Null() => Assert.Equal(false, Invoke("isBoolean", Null()).AsBool());
    [Fact] public void IsBoolean_Float() => Assert.Equal(false, Invoke("isBoolean", F(1.0)).AsBool());

    [Fact] public void IsArray_PlainString() => Assert.Equal(false, Invoke("isArray", S("hello")).AsBool());
    [Fact] public void IsArray_Null() => Assert.Equal(false, Invoke("isArray", Null()).AsBool());

    [Fact] public void IsObject_PlainString() => Assert.Equal(false, Invoke("isObject", S("hello")).AsBool());
    [Fact] public void IsObject_Null() => Assert.Equal(false, Invoke("isObject", Null()).AsBool());
    [Fact] public void IsObject_Int() => Assert.Equal(false, Invoke("isObject", I(42)).AsBool());
    [Fact] public void IsObject_Empty() => Assert.Equal(true, Invoke("isObject", Obj()).AsBool());

    [Fact] public void IsDate_ShortString() => Assert.Equal(false, Invoke("isDate", S("2024")).AsBool());
    [Fact] public void IsDate_Empty() => Assert.Equal(false, Invoke("isDate", S("")).AsBool());
    [Fact] public void IsDate_Bool() => Assert.Equal(false, Invoke("isDate", B(true)).AsBool());
    [Fact] public void IsDate_NoArgs() => Assert.Equal(false, Invoke("isDate").AsBool());
    [Fact] public void IsDate_ValidDate() => Assert.Equal(true, Invoke("isDate", S("2024-01-15")).AsBool());
    [Fact] public void IsDate_DateType() => Assert.Equal(true, Invoke("isDate", DynValue.Date("2024-06-15")).AsBool());

    [Fact] public void TypeOf_Reference() => Assert.Equal("reference", Invoke("typeOf", DynValue.Reference("ref")).AsString());
    [Fact] public void TypeOf_Binary() => Assert.Equal("binary", Invoke("typeOf", DynValue.Binary("data")).AsString());
    [Fact] public void TypeOf_DateValue() => Assert.Equal("date", Invoke("typeOf", DynValue.Date("2024-01-01")).AsString());

    [Fact] public void IsFinite_Zero() => Assert.Equal(true, Invoke("isFinite", F(0.0)).AsBool());
    [Fact] public void IsFinite_Negative() => Assert.Equal(true, Invoke("isFinite", F(-999.0)).AsBool());
    [Fact] public void IsFinite_NegInfinity() => Assert.Equal(false, Invoke("isFinite", F(double.NegativeInfinity)).AsBool());

    [Fact] public void IsNaN_Infinity() => Assert.Equal(false, Invoke("isNaN", F(double.PositiveInfinity)).AsBool());
    [Fact] public void IsNaN_NegInfinity() => Assert.Equal(false, Invoke("isNaN", F(double.NegativeInfinity)).AsBool());

    // =========================================================================
    // Coercion — additional edge cases
    // =========================================================================

    [Fact] public void CoerceString_BoolFalse() => Assert.Equal("false", Invoke("coerceString", B(false)).AsString());
    [Fact] public void CoerceString_ZeroInt() => Assert.Equal("0", Invoke("coerceString", I(0)).AsString());
    [Fact] public void CoerceString_NegativeInt() => Assert.Equal("-5", Invoke("coerceString", I(-5)).AsString());

    [Fact] public void CoerceNumber_IntString() => Assert.Equal(42.0, Invoke("coerceNumber", S("42")).AsDouble());
    [Fact] public void CoerceNumber_NegativeString() => Assert.Equal(-3.14, Invoke("coerceNumber", S("-3.14")).AsDouble());
    [Fact] public void CoerceNumber_ZeroString() => Assert.Equal(0.0, Invoke("coerceNumber", S("0")).AsDouble());

    [Fact] public void CoerceBoolean_N() => Assert.Equal(false, Invoke("coerceBoolean", S("n")).AsBool());
    [Fact] public void CoerceBoolean_Off() => Assert.Equal(false, Invoke("coerceBoolean", S("off")).AsBool());
    [Fact] public void CoerceBoolean_On() => Assert.Equal(true, Invoke("coerceBoolean", S("on")).AsBool());
    [Fact] public void CoerceBoolean_1String() => Assert.Equal(true, Invoke("coerceBoolean", S("1")).AsBool());
    [Fact] public void CoerceBoolean_FloatNonzero() => Assert.Equal(true, Invoke("coerceBoolean", F(0.5)).AsBool());
    [Fact] public void CoerceBoolean_FloatZero() => Assert.Equal(false, Invoke("coerceBoolean", F(0.0)).AsBool());

    [Fact] public void CoerceInteger_NegativeFloat() => Assert.Equal(-3L, Invoke("coerceInteger", F(-3.9)).AsInt64());
    [Fact] public void CoerceInteger_BoolFalse() => Assert.Equal(0L, Invoke("coerceInteger", B(false)).AsInt64());
    [Fact] public void CoerceInteger_LargeFloat() => Assert.Equal(10_000_000_000L, Invoke("coerceInteger", F(1e10)).AsInt64());

    // =========================================================================
    // Core string verbs — additional edge cases
    // =========================================================================

    [Fact] public void Concat_Float() => Assert.Equal("pi=3.14", Invoke("concat", S("pi="), F(3.14)).AsString());
    [Fact] public void Concat_Single() => Assert.Equal("only", Invoke("concat", S("only")).AsString());
    [Fact] public void Concat_AllNulls() => Assert.Equal("", Invoke("concat", Null(), Null()).AsString());

    [Fact] public void Upper_IntError_Extended() => Assert.Throws<InvalidOperationException>(() => Invoke("upper", I(42)));
    [Fact] public void Lower_IntError_Extended() => Assert.Throws<InvalidOperationException>(() => Invoke("lower", I(42)));

    [Fact] public void Trim_AllWhitespace() => Assert.Equal("", Invoke("trim", S("   ")).AsString());
    [Fact] public void Trim_IntError() => Assert.Throws<InvalidOperationException>(() => Invoke("trim", I(5)));

    [Fact] public void TrimLeft_NoLeading() => Assert.Equal("hello", Invoke("trimLeft", S("hello")).AsString());
    [Fact] public void TrimLeft_IntError() => Assert.Throws<InvalidOperationException>(() => Invoke("trimLeft", I(5)));
    [Fact] public void TrimRight_NoTrailing() => Assert.Equal("hello", Invoke("trimRight", S("hello")).AsString());
    [Fact] public void TrimRight_IntError() => Assert.Throws<InvalidOperationException>(() => Invoke("trimRight", I(5)));

    [Fact] public void Coalesce_EmptyThenInt() => Assert.Equal(5L, Invoke("coalesce", S(""), I(5)).AsInt64());
    [Fact] public void Coalesce_NullThenBool() => Assert.Equal(true, Invoke("coalesce", Null(), B(true)).AsBool());
    [Fact] public void Coalesce_Empty() => Assert.True(Invoke("coalesce").IsNull);

    // =========================================================================
    // String verbs — titleCase
    // =========================================================================

    [Fact] public void TitleCase_EmptyString() => Assert.Equal("", Invoke("titleCase", S("")).AsString());
    [Fact] public void TitleCase_SingleWord() => Assert.Equal("Hello", Invoke("titleCase", S("hello")).AsString());
    [Fact] public void TitleCase_AlreadyTitled() => Assert.Equal("Hello World", Invoke("titleCase", S("Hello World")).AsString());
    [Fact] public void TitleCase_AllUpper() => Assert.Equal("HELLO WORLD", Invoke("titleCase", S("HELLO WORLD")).AsString());
    [Fact] public void TitleCase_WithNumbers() => Assert.Equal("Hello 42 World", Invoke("titleCase", S("hello 42 world")).AsString());
    [Fact] public void TitleCase_Null() => Assert.True(Invoke("titleCase", Null()).IsNull);

    // =========================================================================
    // String verbs — contains
    // =========================================================================

    [Fact] public void Contains_EmptySubstring() => Assert.Equal(true, Invoke("contains", S("hello"), S("")).AsBool());
    [Fact] public void Contains_EmptyString() => Assert.Equal(false, Invoke("contains", S(""), S("a")).AsBool());
    [Fact] public void Contains_BothEmpty() => Assert.Equal(true, Invoke("contains", S(""), S("")).AsBool());
    [Fact] public void Contains_CaseSensitive() => Assert.Equal(false, Invoke("contains", S("Hello"), S("hello")).AsBool());
    [Fact] public void Contains_Unicode() => Assert.Equal(true, Invoke("contains", S("caf\u00e9"), S("f\u00e9")).AsBool());
    [Fact] public void Contains_NullFirstArg() => Assert.Equal(false, Invoke("contains", Null(), S("x")).AsBool());
    [Fact] public void Contains_HappyPath() => Assert.Equal(true, Invoke("contains", S("hello world"), S("world")).AsBool());

    // =========================================================================
    // String verbs — startsWith / endsWith
    // =========================================================================

    [Fact] public void StartsWith_EmptyPrefix() => Assert.Equal(true, Invoke("startsWith", S("hello"), S("")).AsBool());
    [Fact] public void StartsWith_FullMatch() => Assert.Equal(true, Invoke("startsWith", S("hello"), S("hello")).AsBool());
    [Fact] public void StartsWith_LongerPrefix() => Assert.Equal(false, Invoke("startsWith", S("hi"), S("hello")).AsBool());
    [Fact] public void StartsWith_Null() => Assert.Equal(false, Invoke("startsWith", Null(), S("x")).AsBool());
    [Fact] public void StartsWith_HappyPath() => Assert.Equal(true, Invoke("startsWith", S("hello"), S("hel")).AsBool());

    [Fact] public void EndsWith_EmptySuffix() => Assert.Equal(true, Invoke("endsWith", S("hello"), S("")).AsBool());
    [Fact] public void EndsWith_FullMatch() => Assert.Equal(true, Invoke("endsWith", S("hello"), S("hello")).AsBool());
    [Fact] public void EndsWith_NoMatch() => Assert.Equal(false, Invoke("endsWith", S("hello"), S("xyz")).AsBool());
    [Fact] public void EndsWith_Null() => Assert.Equal(false, Invoke("endsWith", Null(), S("x")).AsBool());
    [Fact] public void EndsWith_HappyPath() => Assert.Equal(true, Invoke("endsWith", S("hello"), S("llo")).AsBool());

    // =========================================================================
    // String verbs — replace / replaceRegex
    // =========================================================================

    [Fact] public void Replace_HappyPath() => Assert.Equal("hero world", Invoke("replace", S("hello world"), S("hello"), S("hero")).AsString());
    [Fact] public void Replace_NoMatch() => Assert.Equal("hello", Invoke("replace", S("hello"), S("xyz"), S("abc")).AsString());
    [Fact] public void Replace_Null() => Assert.True(Invoke("replace", Null(), S("a"), S("b")).IsNull);

    [Fact] public void ReplaceRegex_NoMatch() => Assert.Equal("hello", Invoke("replaceRegex", S("hello"), S("xyz"), S("abc")).AsString());
    [Fact] public void ReplaceRegex_EmptyReplacement() => Assert.Equal("helloworld", Invoke("replaceRegex", S("hello world"), S(" "), S("")).AsString());
    [Fact] public void ReplaceRegex_MultipleOccurrences() => Assert.Equal("bbbbbb", Invoke("replaceRegex", S("aaa"), S("a"), S("bb")).AsString());

    // =========================================================================
    // String verbs — padding
    // =========================================================================

    [Fact] public void PadLeft_AlreadyWide() => Assert.Equal("hello", Invoke("padLeft", S("hello"), I(3), S("0")).AsString());
    [Fact] public void PadLeft_ExactWidth() => Assert.Equal("hi", Invoke("padLeft", S("hi"), I(2), S("0")).AsString());
    [Fact] public void PadLeft_EmptyString() => Assert.Equal("xxx", Invoke("padLeft", S(""), I(3), S("x")).AsString());
    [Fact] public void PadLeft_DefaultChar() => Assert.Equal("   hi", Invoke("padLeft", S("hi"), I(5)).AsString());

    [Fact] public void PadRight_AlreadyWide() => Assert.Equal("hello", Invoke("padRight", S("hello"), I(3), S(".")).AsString());
    [Fact] public void PadRight_EmptyString() => Assert.Equal("----", Invoke("padRight", S(""), I(4), S("-")).AsString());
    [Fact] public void PadRight_SpaceChar() => Assert.Equal("hi   ", Invoke("padRight", S("hi"), I(5), S(" ")).AsString());

    [Fact] public void Pad_AlreadyWide() => Assert.Equal("hello", Invoke("pad", S("hello"), I(3), S("*")).AsString());
    [Fact] public void Pad_OddPadding() => Assert.Equal("-ab--", Invoke("pad", S("ab"), I(5), S("-")).AsString());
    [Fact] public void Pad_EmptyString() => Assert.Equal("xxxx", Invoke("pad", S(""), I(4), S("x")).AsString());

    // =========================================================================
    // String verbs — truncate
    // =========================================================================

    [Fact] public void Truncate_ShorterThanLimit() => Assert.Equal("hi", Invoke("truncate", S("hi"), I(10)).AsString());
    [Fact] public void Truncate_ExactLength() => Assert.Equal("hello", Invoke("truncate", S("hello"), I(5)).AsString());
    [Fact] public void Truncate_ToZero() => Assert.Equal("", Invoke("truncate", S("hello"), I(0)).AsString());
    [Fact] public void Truncate_EmptyString() => Assert.Equal("", Invoke("truncate", S(""), I(5)).AsString());
    [Fact] public void Truncate_Null() => Assert.True(Invoke("truncate", Null(), I(5)).IsNull);

    // =========================================================================
    // String verbs — split / join
    // =========================================================================

    [Fact]
    public void Split_EmptyString()
    {
        var result = Invoke("split", S(""), S(","));
        var arr = result.AsArray();
        Assert.NotNull(arr);
        Assert.Single(arr);
        Assert.Equal("", arr[0].AsString());
    }

    [Fact]
    public void Split_NoDelimiterFound()
    {
        var result = Invoke("split", S("hello"), S(","));
        var arr = result.AsArray();
        Assert.NotNull(arr);
        Assert.Single(arr);
        Assert.Equal("hello", arr[0].AsString());
    }

    [Fact]
    public void Split_MultiCharDelimiter()
    {
        var result = Invoke("split", S("a::b::c"), S("::"));
        var arr = result.AsArray();
        Assert.NotNull(arr);
        Assert.Equal(3, arr.Count);
        Assert.Equal("a", arr[0].AsString());
        Assert.Equal("b", arr[1].AsString());
        Assert.Equal("c", arr[2].AsString());
    }

    [Fact]
    public void Join_EmptyArray()
        => Assert.Equal("", Invoke("join", Arr(), S(",")).AsString());

    [Fact]
    public void Join_SingleElement()
        => Assert.Equal("a", Invoke("join", Arr(S("a")), S(",")).AsString());

    [Fact]
    public void Join_EmptyDelimiter()
        => Assert.Equal("abc", Invoke("join", Arr(S("a"), S("b"), S("c")), S("")).AsString());

    [Fact]
    public void Join_WithIntegers()
        => Assert.Equal("1-2-3", Invoke("join", Arr(I(1), I(2), I(3)), S("-")).AsString());

    // =========================================================================
    // String verbs — mask
    // =========================================================================

    [Fact] public void Mask_ShowAll() => Assert.Equal("abc", Invoke("mask", S("abc"), S("***")).AsString());
    [Fact] public void Mask_ShowZero() => Assert.Equal("a-b-c", Invoke("mask", S("abc"), S("*-*-*")).AsString());
    [Fact] public void Mask_ShowExactLength() => Assert.Equal("abc", Invoke("mask", S("abc"), S("###")).AsString());
    [Fact] public void Mask_Null() => Assert.True(Invoke("mask", Null(), S("####")).IsNull);

    // =========================================================================
    // String verbs — reverseString
    // =========================================================================

    [Fact] public void Reverse_Empty() => Assert.Equal("", Invoke("reverseString", S("")).AsString());
    [Fact] public void Reverse_SingleChar() => Assert.Equal("a", Invoke("reverseString", S("a")).AsString());
    [Fact] public void Reverse_Palindrome() => Assert.Equal("racecar", Invoke("reverseString", S("racecar")).AsString());
    [Fact] public void Reverse_Regular() => Assert.Equal("cba", Invoke("reverseString", S("abc")).AsString());
    [Fact] public void Reverse_WithSpaces() => Assert.Equal("c b a", Invoke("reverseString", S("a b c")).AsString());
    [Fact] public void Reverse_Null() => Assert.True(Invoke("reverseString", Null()).IsNull);

    // =========================================================================
    // String verbs — repeat
    // =========================================================================

    [Fact] public void Repeat_ZeroTimes() => Assert.Equal("", Invoke("repeat", S("abc"), I(0)).AsString());
    [Fact] public void Repeat_OneTime() => Assert.Equal("abc", Invoke("repeat", S("abc"), I(1)).AsString());
    [Fact] public void Repeat_EmptyString() => Assert.Equal("", Invoke("repeat", S(""), I(5)).AsString());

    [Fact]
    public void Repeat_LargeCount()
    {
        var result = Invoke("repeat", S("x"), I(100)).AsString();
        Assert.Equal(100, result!.Length);
    }

    // =========================================================================
    // String verbs — case conversion
    // =========================================================================

    [Fact] public void CamelCase_Empty() => Assert.Equal("", Invoke("camelCase", S("")).AsString());
    [Fact] public void CamelCase_SingleWord() => Assert.Equal("hello", Invoke("camelCase", S("hello")).AsString());
    [Fact] public void CamelCase_FromSnake() => Assert.Equal("helloWorld", Invoke("camelCase", S("hello_world")).AsString());
    [Fact] public void CamelCase_FromKebab() => Assert.Equal("helloWorld", Invoke("camelCase", S("hello-world")).AsString());
    [Fact] public void CamelCase_FromPascal() => Assert.Equal("helloWorld", Invoke("camelCase", S("HelloWorld")).AsString());
    [Fact] public void CamelCase_MultipleWords() => Assert.Equal("theQuickBrownFox", Invoke("camelCase", S("the quick brown fox")).AsString());
    [Fact] public void CamelCase_Null() => Assert.True(Invoke("camelCase", Null()).IsNull);

    [Fact] public void SnakeCase_Empty() => Assert.Equal("", Invoke("snakeCase", S("")).AsString());
    [Fact] public void SnakeCase_SingleWord() => Assert.Equal("hello", Invoke("snakeCase", S("hello")).AsString());
    [Fact] public void SnakeCase_FromCamel() => Assert.Equal("hello_world", Invoke("snakeCase", S("helloWorld")).AsString());
    [Fact] public void SnakeCase_FromPascal() => Assert.Equal("hello_world", Invoke("snakeCase", S("HelloWorld")).AsString());
    [Fact] public void SnakeCase_FromKebab() => Assert.Equal("hello_world", Invoke("snakeCase", S("hello-world")).AsString());
    [Fact] public void SnakeCase_Spaces() => Assert.Equal("hello_world_test", Invoke("snakeCase", S("hello world test")).AsString());
    [Fact] public void SnakeCase_Null() => Assert.True(Invoke("snakeCase", Null()).IsNull);

    [Fact] public void KebabCase_Empty() => Assert.Equal("", Invoke("kebabCase", S("")).AsString());
    [Fact] public void KebabCase_SingleWord() => Assert.Equal("hello", Invoke("kebabCase", S("hello")).AsString());
    [Fact] public void KebabCase_FromCamel() => Assert.Equal("hello-world", Invoke("kebabCase", S("helloWorld")).AsString());
    [Fact] public void KebabCase_FromSnake() => Assert.Equal("hello-world", Invoke("kebabCase", S("hello_world")).AsString());
    [Fact] public void KebabCase_FromPascal() => Assert.Equal("hello-world", Invoke("kebabCase", S("HelloWorld")).AsString());
    [Fact] public void KebabCase_Spaces() => Assert.Equal("hello-world-test", Invoke("kebabCase", S("hello world test")).AsString());
    [Fact] public void KebabCase_Null() => Assert.True(Invoke("kebabCase", Null()).IsNull);

    [Fact] public void PascalCase_Empty() => Assert.Equal("", Invoke("pascalCase", S("")).AsString());
    [Fact] public void PascalCase_SingleWord() => Assert.Equal("Hello", Invoke("pascalCase", S("hello")).AsString());
    [Fact] public void PascalCase_FromCamel() => Assert.Equal("HelloWorld", Invoke("pascalCase", S("helloWorld")).AsString());
    [Fact] public void PascalCase_FromSnake() => Assert.Equal("HelloWorld", Invoke("pascalCase", S("hello_world")).AsString());
    [Fact] public void PascalCase_FromKebab() => Assert.Equal("HelloWorld", Invoke("pascalCase", S("hello-world")).AsString());
    [Fact] public void PascalCase_MultipleWords() => Assert.Equal("TheQuickBrownFox", Invoke("pascalCase", S("the quick brown fox")).AsString());
    [Fact] public void PascalCase_Null() => Assert.True(Invoke("pascalCase", Null()).IsNull);

    // =========================================================================
    // String verbs — slugify
    // =========================================================================

    [Fact] public void Slugify_Empty() => Assert.Equal("", Invoke("slugify", S("")).AsString());
    [Fact] public void Slugify_AlreadySlug() => Assert.Equal("hello-world", Invoke("slugify", S("hello-world")).AsString());
    [Fact] public void Slugify_SpecialChars() => Assert.Equal("hello-world-1", Invoke("slugify", S("Hello, World! #1")).AsString());
    [Fact] public void Slugify_MultipleSpaces() => Assert.Equal("hello-world", Invoke("slugify", S("hello   world")).AsString());
    [Fact] public void Slugify_LeadingTrailingSpecial() => Assert.Equal("hello", Invoke("slugify", S("!!hello!!")).AsString());
    [Fact] public void Slugify_Numbers() => Assert.Equal("test-123-stuff", Invoke("slugify", S("Test 123 Stuff")).AsString());
    [Fact] public void Slugify_Null() => Assert.True(Invoke("slugify", Null()).IsNull);

    // =========================================================================
    // String verbs — matches / match
    // =========================================================================

    [Fact] public void Matches_HappyPath() => Assert.Equal(true, Invoke("matches", S("hello"), S("ell")).AsBool());
    [Fact] public void Matches_NoMatch() => Assert.Equal(false, Invoke("matches", S("hello"), S("xyz")).AsBool());
    [Fact] public void Matches_Null() => Assert.Equal(false, Invoke("matches", Null(), S("x")).AsBool());

    [Fact]
    public void Match_HappyPath()
    {
        var result = Invoke("match", S("hello world"), S("w\\w+"));
        Assert.Equal("world", result.AsString());
    }

    [Fact] public void Match_NoMatch() => Assert.True(Invoke("match", S("hello"), S("xyz")).IsNull);
    [Fact] public void Match_Null() => Assert.True(Invoke("match", Null(), S("x")).IsNull);

    // =========================================================================
    // String verbs — normalizeSpace
    // =========================================================================

    [Fact] public void NormalizeSpace_Empty() => Assert.Equal("", Invoke("normalizeSpace", S("")).AsString());
    [Fact] public void NormalizeSpace_OnlyWhitespace() => Assert.Equal("", Invoke("normalizeSpace", S("   ")).AsString());
    [Fact] public void NormalizeSpace_TabsAndNewlines() => Assert.Equal("hello world", Invoke("normalizeSpace", S("hello\t\nworld")).AsString());
    [Fact] public void NormalizeSpace_SingleWord() => Assert.Equal("hello", Invoke("normalizeSpace", S("hello")).AsString());
    [Fact] public void NormalizeSpace_Null() => Assert.True(Invoke("normalizeSpace", Null()).IsNull);

    // =========================================================================
    // String verbs — leftOf / rightOf
    // =========================================================================

    [Fact] public void LeftOf_NoDelimiter() => Assert.Equal("hello", Invoke("leftOf", S("hello"), S("@")).AsString());
    [Fact] public void LeftOf_AtStart() => Assert.Equal("", Invoke("leftOf", S("@hello"), S("@")).AsString());
    [Fact] public void LeftOf_Multiple() => Assert.Equal("a", Invoke("leftOf", S("a@b@c"), S("@")).AsString());
    [Fact] public void LeftOf_EmptyString() => Assert.Equal("", Invoke("leftOf", S(""), S("@")).AsString());
    [Fact] public void LeftOf_HappyPath() => Assert.Equal("user", Invoke("leftOf", S("user@example.com"), S("@")).AsString());

    [Fact] public void RightOf_NoDelimiter() => Assert.Equal("hello", Invoke("rightOf", S("hello"), S("@")).AsString());
    [Fact] public void RightOf_AtEnd() => Assert.Equal("", Invoke("rightOf", S("hello@"), S("@")).AsString());
    [Fact] public void RightOf_Multiple() => Assert.Equal("b@c", Invoke("rightOf", S("a@b@c"), S("@")).AsString());
    [Fact] public void RightOf_EmptyString() => Assert.Equal("", Invoke("rightOf", S(""), S("@")).AsString());
    [Fact] public void RightOf_HappyPath() => Assert.Equal("example.com", Invoke("rightOf", S("user@example.com"), S("@")).AsString());

    // =========================================================================
    // String verbs — wrap / center
    // =========================================================================

    [Fact] public void Wrap_WithPrefixSuffix() => Assert.Equal("<hello>", Invoke("wrap", S("hello"), S("<"), S(">")).AsString());
    [Fact] public void Wrap_SameChar() => Assert.Equal("'hello'", Invoke("wrap", S("hello"), S("'")).AsString());
    [Fact] public void Wrap_Null() => Assert.True(Invoke("wrap", Null(), S("<"), S(">")).IsNull);

    [Fact] public void Center_AlreadyWide() => Assert.Equal("hello", Invoke("center", S("hello"), I(3), S("-")).AsString());
    [Fact] public void Center_OddPadding() => Assert.Equal("-ab--", Invoke("center", S("ab"), I(5), S("-")).AsString());
    [Fact] public void Center_EmptyString() => Assert.Equal("****", Invoke("center", S(""), I(4), S("*")).AsString());

    // =========================================================================
    // String verbs — stripAccents
    // =========================================================================

    [Fact] public void StripAccents_Empty() => Assert.Equal("", Invoke("stripAccents", S("")).AsString());
    [Fact] public void StripAccents_NoAccents() => Assert.Equal("hello", Invoke("stripAccents", S("hello")).AsString());
    [Fact] public void StripAccents_Cafe() => Assert.Equal("cafe", Invoke("stripAccents", S("caf\u00e9")).AsString());
    [Fact] public void StripAccents_Upper() => Assert.Equal("AAA", Invoke("stripAccents", S("\u00c0\u00c1\u00c2")).AsString());
    [Fact] public void StripAccents_NTilde() => Assert.Equal("nN", Invoke("stripAccents", S("\u00f1\u00d1")).AsString());
    [Fact] public void StripAccents_Null() => Assert.True(Invoke("stripAccents", Null()).IsNull);

    // =========================================================================
    // String verbs — clean
    // =========================================================================

    [Fact] public void Clean_Empty() => Assert.Equal("", Invoke("clean", S("")).AsString());
    [Fact] public void Clean_NoControlChars() => Assert.Equal("hello world", Invoke("clean", S("hello world")).AsString());
    [Fact] public void Clean_PreservesNewlinesTabs() => Assert.Equal("a\nb\tc\r", Invoke("clean", S("a\nb\tc\r")).AsString());
    [Fact] public void Clean_RemovesNullBytes() => Assert.Equal("abcd", Invoke("clean", S("a\0b\u0001c\u0002d")).AsString());
    [Fact] public void Clean_Null() => Assert.True(Invoke("clean", Null()).IsNull);

    // =========================================================================
    // String verbs — wordCount / tokenize
    // =========================================================================

    [Fact] public void WordCount_Empty() => Assert.Equal(0L, Invoke("wordCount", S("")).AsInt64());
    [Fact] public void WordCount_OnlyWhitespace() => Assert.Equal(0L, Invoke("wordCount", S("   ")).AsInt64());
    [Fact] public void WordCount_SingleWord() => Assert.Equal(1L, Invoke("wordCount", S("hello")).AsInt64());
    [Fact] public void WordCount_MultipleSpaces() => Assert.Equal(2L, Invoke("wordCount", S("  hello   world  ")).AsInt64());
    [Fact] public void WordCount_WithTabs() => Assert.Equal(3L, Invoke("wordCount", S("a\tb\tc")).AsInt64());
    [Fact] public void WordCount_Null() => Assert.Equal(0L, Invoke("wordCount", Null()).AsInt64());

    [Fact]
    public void Tokenize_Empty()
    {
        var result = Invoke("tokenize", S(""));
        var arr = result.AsArray();
        Assert.NotNull(arr);
        Assert.Empty(arr);
    }

    [Fact]
    public void Tokenize_Whitespace()
    {
        var result = Invoke("tokenize", S("hello world test"));
        var arr = result.AsArray();
        Assert.NotNull(arr);
        Assert.Equal(3, arr.Count);
        Assert.Equal("hello", arr[0].AsString());
        Assert.Equal("world", arr[1].AsString());
        Assert.Equal("test", arr[2].AsString());
    }

    // =========================================================================
    // String verbs — levenshtein
    // =========================================================================

    [Fact] public void Levenshtein_Identical() => Assert.Equal(0L, Invoke("levenshtein", S("hello"), S("hello")).AsInt64());
    [Fact] public void Levenshtein_EmptyFirst() => Assert.Equal(5L, Invoke("levenshtein", S(""), S("hello")).AsInt64());
    [Fact] public void Levenshtein_EmptySecond() => Assert.Equal(5L, Invoke("levenshtein", S("hello"), S("")).AsInt64());
    [Fact] public void Levenshtein_BothEmpty() => Assert.Equal(0L, Invoke("levenshtein", S(""), S("")).AsInt64());
    [Fact] public void Levenshtein_SingleEdit() => Assert.Equal(1L, Invoke("levenshtein", S("kitten"), S("sitten")).AsInt64());
    [Fact] public void Levenshtein_Classic() => Assert.Equal(3L, Invoke("levenshtein", S("kitten"), S("sitting")).AsInt64());

    // =========================================================================
    // String verbs — soundex
    // =========================================================================

    [Fact] public void Soundex_Robert() => Assert.Equal("R163", Invoke("soundex", S("Robert")).AsString());
    [Fact] public void Soundex_Rupert() => Assert.Equal("R163", Invoke("soundex", S("Rupert")).AsString());
    [Fact] public void Soundex_Ashcraft() => Assert.Equal("A226", Invoke("soundex", S("Ashcraft")).AsString());
    [Fact] public void Soundex_Empty() => Assert.Equal("", Invoke("soundex", S("")).AsString());
    [Fact] public void Soundex_SingleLetter() => Assert.Equal("A000", Invoke("soundex", S("A")).AsString());
    [Fact] public void Soundex_Null() => Assert.True(Invoke("soundex", Null()).IsNull);

    // =========================================================================
    // String verbs — substring / length
    // =========================================================================

    [Fact] public void Substring_HappyPath() => Assert.Equal("llo", Invoke("substring", S("hello"), I(2)).AsString());
    [Fact] public void Substring_WithLength() => Assert.Equal("ell", Invoke("substring", S("hello"), I(1), I(3)).AsString());
    [Fact] public void Substring_BeyondEnd() => Assert.Equal("lo", Invoke("substring", S("hello"), I(3), I(100)).AsString());
    [Fact] public void Substring_Null() => Assert.True(Invoke("substring", Null(), I(0)).IsNull);

    [Fact] public void Length_String() => Assert.Equal(5L, Invoke("length", S("hello")).AsInt64());
    [Fact] public void Length_EmptyString() => Assert.Equal(0L, Invoke("length", S("")).AsInt64());
    [Fact] public void Length_EmptyArray() => Assert.Equal(0L, Invoke("length", Arr()).AsInt64());
    [Fact] public void Length_Array() => Assert.Equal(3L, Invoke("length", Arr(I(1), I(2), I(3))).AsInt64());
    [Fact] public void Length_Null() => Assert.Equal(0L, Invoke("length", Null()).AsInt64());

    // =========================================================================
    // String verbs — capitalize
    // =========================================================================

    [Fact] public void Capitalize_HappyPath() => Assert.Equal("Hello", Invoke("capitalize", S("hello")).AsString());
    [Fact] public void Capitalize_Empty() => Assert.Equal("", Invoke("capitalize", S("")).AsString());
    [Fact] public void Capitalize_AlreadyCapitalized() => Assert.Equal("Hello", Invoke("capitalize", S("Hello")).AsString());
    [Fact] public void Capitalize_AllUpper() => Assert.Equal("Hello", Invoke("capitalize", S("HELLO")).AsString());
    [Fact] public void Capitalize_Null() => Assert.True(Invoke("capitalize", Null()).IsNull);

    // =========================================================================
    // Assert verb
    // =========================================================================

    [Fact] public void Assert_TruthyInt() => Assert.Equal(1L, Invoke("assert", I(1)).AsInt64());
    [Fact] public void Assert_FalsyZero_Throws() => Assert.Throws<InvalidOperationException>(() => Invoke("assert", I(0)));
    [Fact] public void Assert_TruthyString() => Assert.Equal("yes", Invoke("assert", S("yes")).AsString());
    [Fact] public void Assert_FalsyNull_Throws() => Assert.Throws<InvalidOperationException>(() => Invoke("assert", Null()));

    // =========================================================================
    // Coercion — tryCoerce / toArray / toObject / coerceDate / coerceTimestamp
    // =========================================================================

    [Fact] public void TryCoerce_Integer() => Assert.Equal(42L, Invoke("tryCoerce", S("42")).AsInt64());
    [Fact] public void TryCoerce_Float() => Assert.Equal(3.14, Invoke("tryCoerce", S("3.14")).AsDouble());
    [Fact] public void TryCoerce_BoolTrue() => Assert.Equal(true, Invoke("tryCoerce", S("true")).AsBool());
    [Fact] public void TryCoerce_BoolFalse() => Assert.Equal(false, Invoke("tryCoerce", S("false")).AsBool());
    [Fact] public void TryCoerce_Date() => Assert.Equal("2024-01-15", Invoke("tryCoerce", S("2024-01-15")).AsString());
    [Fact] public void TryCoerce_PlainString() => Assert.Equal("hello", Invoke("tryCoerce", S("hello")).AsString());
    [Fact] public void TryCoerce_NonString() => Assert.Equal(42L, Invoke("tryCoerce", I(42)).AsInt64());

    [Fact]
    public void ToArray_Single()
    {
        var result = Invoke("toArray", I(42));
        var arr = result.AsArray();
        Assert.NotNull(arr);
        Assert.Single(arr);
        Assert.Equal(42L, arr[0].AsInt64());
    }

    [Fact]
    public void ToArray_AlreadyArray()
    {
        var input = Arr(I(1), I(2));
        var result = Invoke("toArray", input);
        var arr = result.AsArray();
        Assert.NotNull(arr);
        Assert.Equal(2, arr.Count);
    }

    [Fact]
    public void ToArray_NoArgs()
    {
        var result = Invoke("toArray");
        var arr = result.AsArray();
        Assert.NotNull(arr);
        Assert.Empty(arr);
    }

    [Fact]
    public void CoerceDate_ValidDate()
    {
        var result = Invoke("coerceDate", S("2024-06-15"));
        Assert.Equal("2024-06-15", result.AsString());
    }

    [Fact]
    public void CoerceDate_Null()
    {
        var result = Invoke("coerceDate", Null());
        Assert.True(result.IsNull);
    }

    [Fact]
    public void CoerceDate_InvalidString_Throws()
        => Assert.Throws<InvalidOperationException>(() => Invoke("coerceDate", S("not-a-date")));

    [Fact]
    public void CoerceTimestamp_ValidTimestamp()
    {
        var result = Invoke("coerceTimestamp", S("2024-06-15T14:30:00"));
        Assert.NotNull(result.AsString());
        Assert.Contains("2024-06-15", result.AsString());
    }

    [Fact]
    public void CoerceTimestamp_BareDate()
    {
        var result = Invoke("coerceTimestamp", S("2024-06-15"));
        Assert.Contains("2024-06-15", result.AsString());
        Assert.Contains("00:00:00", result.AsString());
    }

    [Fact]
    public void CoerceTimestamp_Null()
    {
        var result = Invoke("coerceTimestamp", Null());
        Assert.True(result.IsNull);
    }
}
