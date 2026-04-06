using System;
using System.Collections.Generic;
using Odin.Core.Types;
using Odin.Core.Transform;
using Odin.Core.Transform.Verbs;
using Xunit;

namespace Odin.Core.Tests.Unit;

/// <summary>
/// Tests for string manipulation, text analysis, and encoding verbs.
/// Ported from Rust SDK string_verbs.rs tests and extended_tests.
/// </summary>
public class StringVerbTests
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
    // capitalize
    // =========================================================================

    [Fact]
    public void Capitalize_Basic()
        => Assert.Equal("Hello", Invoke("capitalize", S("hello")).AsString());

    [Fact]
    public void Capitalize_EmptyString()
        => Assert.Equal("", Invoke("capitalize", S("")).AsString());

    [Fact]
    public void Capitalize_Null()
        => Assert.True(Invoke("capitalize", Null()).IsNull);

    [Fact]
    public void Capitalize_AlreadyCapitalized()
        => Assert.Equal("Hello", Invoke("capitalize", S("Hello")).AsString());

    [Fact]
    public void Capitalize_SingleChar()
        => Assert.Equal("H", Invoke("capitalize", S("h")).AsString());

    [Fact]
    public void Capitalize_AllUpper()
        => Assert.Equal("Hello", Invoke("capitalize", S("HELLO")).AsString());

    [Fact]
    public void Capitalize_NoArgs()
        => Assert.True(Invoke("capitalize").IsNull);

    [Fact]
    public void Capitalize_WithSpaces()
        => Assert.Equal("Hello world", Invoke("capitalize", S("hello world")).AsString());

    // =========================================================================
    // titleCase
    // =========================================================================

    [Fact]
    public void TitleCase_Basic()
        => Assert.Equal("Hello World", Invoke("titleCase", S("hello world")).AsString());

    [Fact]
    public void TitleCase_Empty()
        => Assert.Equal("", Invoke("titleCase", S("")).AsString());

    [Fact]
    public void TitleCase_Null()
        => Assert.True(Invoke("titleCase", Null()).IsNull);

    [Fact]
    public void TitleCase_SingleWord()
        => Assert.Equal("Hello", Invoke("titleCase", S("hello")).AsString());

    [Fact]
    public void TitleCase_AlreadyTitleCase()
        => Assert.Equal("Hello World", Invoke("titleCase", S("Hello World")).AsString());

    [Fact]
    public void TitleCase_MultipleSpaces()
        => Assert.Equal("Hello  World", Invoke("titleCase", S("hello  world")).AsString());

    [Fact]
    public void TitleCase_NoArgs()
        => Assert.True(Invoke("titleCase").IsNull);

    // =========================================================================
    // contains
    // =========================================================================

    [Fact]
    public void Contains_Found()
        => Assert.True(Invoke("contains", S("hello world"), S("world")).AsBool()!.Value);

    [Fact]
    public void Contains_NotFound()
        => Assert.False(Invoke("contains", S("hello"), S("xyz")).AsBool()!.Value);

    [Fact]
    public void Contains_EmptySubstring()
        => Assert.True(Invoke("contains", S("hello"), S("")).AsBool()!.Value);

    [Fact]
    public void Contains_EmptyString()
        => Assert.False(Invoke("contains", S(""), S("a")).AsBool()!.Value);

    [Fact]
    public void Contains_NullInput()
        => Assert.False(Invoke("contains", Null(), S("a")).AsBool()!.Value);

    [Fact]
    public void Contains_TooFewArgs()
        => Assert.False(Invoke("contains", S("hello")).AsBool()!.Value);

    [Fact]
    public void Contains_CaseSensitive()
        => Assert.False(Invoke("contains", S("Hello"), S("hello")).AsBool()!.Value);

    // =========================================================================
    // startsWith
    // =========================================================================

    [Fact]
    public void StartsWith_True()
        => Assert.True(Invoke("startsWith", S("hello world"), S("hello")).AsBool()!.Value);

    [Fact]
    public void StartsWith_False()
        => Assert.False(Invoke("startsWith", S("hello world"), S("world")).AsBool()!.Value);

    [Fact]
    public void StartsWith_EmptyPrefix()
        => Assert.True(Invoke("startsWith", S("hello"), S("")).AsBool()!.Value);

    [Fact]
    public void StartsWith_NullInput()
        => Assert.False(Invoke("startsWith", Null(), S("a")).AsBool()!.Value);

    [Fact]
    public void StartsWith_ExactMatch()
        => Assert.True(Invoke("startsWith", S("hello"), S("hello")).AsBool()!.Value);

    [Fact]
    public void StartsWith_TooFewArgs()
        => Assert.False(Invoke("startsWith", S("hello")).AsBool()!.Value);

    // =========================================================================
    // endsWith
    // =========================================================================

    [Fact]
    public void EndsWith_True()
        => Assert.True(Invoke("endsWith", S("hello world"), S("world")).AsBool()!.Value);

    [Fact]
    public void EndsWith_False()
        => Assert.False(Invoke("endsWith", S("hello world"), S("hello")).AsBool()!.Value);

    [Fact]
    public void EndsWith_EmptySuffix()
        => Assert.True(Invoke("endsWith", S("hello"), S("")).AsBool()!.Value);

    [Fact]
    public void EndsWith_NullInput()
        => Assert.False(Invoke("endsWith", Null(), S("a")).AsBool()!.Value);

    [Fact]
    public void EndsWith_ExactMatch()
        => Assert.True(Invoke("endsWith", S("hello"), S("hello")).AsBool()!.Value);

    // =========================================================================
    // replace
    // =========================================================================

    [Fact]
    public void Replace_Basic()
        => Assert.Equal("hello rust", Invoke("replace", S("hello world"), S("world"), S("rust")).AsString());

    [Fact]
    public void Replace_NoMatch()
        => Assert.Equal("hello", Invoke("replace", S("hello"), S("xyz"), S("abc")).AsString());

    [Fact]
    public void Replace_Multiple()
        => Assert.Equal("bbb", Invoke("replace", S("aaa"), S("a"), S("b")).AsString());

    [Fact]
    public void Replace_NullInput()
        => Assert.True(Invoke("replace", Null(), S("a"), S("b")).IsNull);

    [Fact]
    public void Replace_TooFewArgs()
    {
        var result = Invoke("replace", S("hello"), S("h"));
        Assert.Equal("hello", result.AsString());
    }

    [Fact]
    public void Replace_EmptySearch()
        => Assert.Throws<ArgumentException>(() => Invoke("replace", S("hello"), S(""), S("x")));

    // =========================================================================
    // replaceRegex
    // =========================================================================

    [Fact]
    public void ReplaceRegex_Basic()
        => Assert.Equal("hello world", Invoke("replaceRegex", S("hello   world"), S("\\s+"), S(" ")).AsString());

    [Fact]
    public void ReplaceRegex_DigitsRemoved()
        => Assert.Equal("abc", Invoke("replaceRegex", S("a1b2c3"), S("\\d"), S("")).AsString());

    [Fact]
    public void ReplaceRegex_NullInput()
        => Assert.True(Invoke("replaceRegex", Null(), S("\\d"), S("")).IsNull);

    [Fact]
    public void ReplaceRegex_InvalidPattern()
    {
        // Invalid regex should return original string
        var result = Invoke("replaceRegex", S("hello"), S("[invalid"), S("x"));
        Assert.Equal("hello", result.AsString());
    }

    // =========================================================================
    // padLeft
    // =========================================================================

    [Fact]
    public void PadLeft_Basic()
        => Assert.Equal("00042", Invoke("padLeft", S("42"), I(5), S("0")).AsString());

    [Fact]
    public void PadLeft_DefaultChar()
        => Assert.Equal("   42", Invoke("padLeft", S("42"), I(5)).AsString());

    [Fact]
    public void PadLeft_AlreadyLong()
        => Assert.Equal("hello", Invoke("padLeft", S("hello"), I(3), S("0")).AsString());

    [Fact]
    public void PadLeft_Null()
        => Assert.True(Invoke("padLeft", Null(), I(5)).IsNull);

    [Fact]
    public void PadLeft_ExactWidth()
        => Assert.Equal("hi", Invoke("padLeft", S("hi"), I(2)).AsString());

    // =========================================================================
    // padRight
    // =========================================================================

    [Fact]
    public void PadRight_Basic()
        => Assert.Equal("hi...", Invoke("padRight", S("hi"), I(5), S(".")).AsString());

    [Fact]
    public void PadRight_DefaultChar()
        => Assert.Equal("hi   ", Invoke("padRight", S("hi"), I(5)).AsString());

    [Fact]
    public void PadRight_AlreadyLong()
        => Assert.Equal("hello", Invoke("padRight", S("hello"), I(3), S(".")).AsString());

    [Fact]
    public void PadRight_Null()
        => Assert.True(Invoke("padRight", Null(), I(5)).IsNull);

    // =========================================================================
    // pad (center)
    // =========================================================================

    [Fact]
    public void Pad_Center()
        => Assert.Equal("**hi**", Invoke("pad", S("hi"), I(6), S("*")).AsString());

    [Fact]
    public void Pad_OddWidth()
    {
        var result = Invoke("pad", S("hi"), I(5), S("*")).AsString();
        Assert.Equal(5, result!.Length);
        Assert.Contains("hi", result);
    }

    [Fact]
    public void Pad_AlreadyLong()
        => Assert.Equal("hello", Invoke("pad", S("hello"), I(3), S("*")).AsString());

    [Fact]
    public void Pad_Null()
        => Assert.True(Invoke("pad", Null(), I(5)).IsNull);

    // =========================================================================
    // truncate
    // =========================================================================

    [Fact]
    public void Truncate_Basic()
        => Assert.Equal("hello", Invoke("truncate", S("hello world"), I(5)).AsString());

    [Fact]
    public void Truncate_WithEllipsis()
        => Assert.Equal("he...", Invoke("truncate", S("hello world"), I(5), S("...")).AsString());

    [Fact]
    public void Truncate_ShortEnough()
        => Assert.Equal("hi", Invoke("truncate", S("hi"), I(10)).AsString());

    [Fact]
    public void Truncate_Null()
        => Assert.True(Invoke("truncate", Null(), I(5)).IsNull);

    [Fact]
    public void Truncate_ZeroLength()
        => Assert.Equal("", Invoke("truncate", S("hello"), I(0)).AsString());

    // =========================================================================
    // split
    // =========================================================================

    [Fact]
    public void Split_Basic()
    {
        var result = Invoke("split", S("a,b,c"), S(","));
        var arr = result.AsArray()!;
        Assert.Equal(3, arr.Count);
        Assert.Equal("a", arr[0].AsString());
        Assert.Equal("b", arr[1].AsString());
        Assert.Equal("c", arr[2].AsString());
    }

    [Fact]
    public void Split_Null()
        => Assert.True(Invoke("split", Null(), S(",")).IsNull);

    [Fact]
    public void Split_EmptyDelimiter()
    {
        var result = Invoke("split", S("abc"), S(""));
        var arr = result.AsArray()!;
        Assert.Equal(3, arr.Count);
        Assert.Equal("a", arr[0].AsString());
    }

    [Fact]
    public void Split_NoMatch()
    {
        var result = Invoke("split", S("hello"), S(","));
        var arr = result.AsArray()!;
        Assert.Single(arr);
        Assert.Equal("hello", arr[0].AsString());
    }

    [Fact]
    public void Split_TooFewArgs()
        => Assert.True(Invoke("split", S("hello")).IsNull);

    // =========================================================================
    // join
    // =========================================================================

    [Fact]
    public void Join_Basic()
        => Assert.Equal("a, b, c", Invoke("join", Arr(S("a"), S("b"), S("c")), S(", ")).AsString());

    [Fact]
    public void Join_EmptyDelimiter()
        => Assert.Equal("abc", Invoke("join", Arr(S("a"), S("b"), S("c")), S("")).AsString());

    [Fact]
    public void Join_SingleItem()
        => Assert.Equal("hello", Invoke("join", Arr(S("hello")), S(",")).AsString());

    [Fact]
    public void Join_EmptyArray()
        => Assert.Equal("", Invoke("join", Arr(), S(",")).AsString());

    [Fact]
    public void Join_TooFewArgs()
        => Assert.True(Invoke("join", Arr(S("a"))).IsNull);

    // =========================================================================
    // mask
    // =========================================================================

    [Fact]
    public void Mask_Basic()
        => Assert.Equal("123-456-7890", Invoke("mask", S("1234567890"), S("###-###-####")).AsString());

    [Fact]
    public void Mask_CustomChar()
        => Assert.Equal("(123) 456-7890", Invoke("mask", S("1234567890"), S("(###) ###-####")).AsString());

    [Fact]
    public void Mask_ShortString()
    {
        // Input shorter than pattern — only processes as many input chars as available
        var result = Invoke("mask", S("abc"), S("##-##")).AsString();
        Assert.Equal("ab-c", result);
    }

    [Fact]
    public void Mask_Null()
        => Assert.True(Invoke("mask", Null(), S("###")).IsNull);

    [Fact]
    public void Mask_DefaultShowLast()
    {
        // mask requires 2 args (value, pattern) — 1 arg returns null
        var result = Invoke("mask", S("123456789"));
        Assert.True(result.IsNull);
    }

    // =========================================================================
    // reverseString
    // =========================================================================

    [Fact]
    public void ReverseString_Basic()
        => Assert.Equal("olleh", Invoke("reverseString", S("hello")).AsString());

    [Fact]
    public void ReverseString_Empty()
        => Assert.Equal("", Invoke("reverseString", S("")).AsString());

    [Fact]
    public void ReverseString_Null()
        => Assert.True(Invoke("reverseString", Null()).IsNull);

    [Fact]
    public void ReverseString_SingleChar()
        => Assert.Equal("a", Invoke("reverseString", S("a")).AsString());

    [Fact]
    public void ReverseString_Palindrome()
        => Assert.Equal("racecar", Invoke("reverseString", S("racecar")).AsString());

    // =========================================================================
    // repeat
    // =========================================================================

    [Fact]
    public void Repeat_Basic()
        => Assert.Equal("ababab", Invoke("repeat", S("ab"), I(3)).AsString());

    [Fact]
    public void Repeat_Zero()
        => Assert.Equal("", Invoke("repeat", S("ab"), I(0)).AsString());

    [Fact]
    public void Repeat_Null()
        => Assert.True(Invoke("repeat", Null(), I(3)).IsNull);

    [Fact]
    public void Repeat_One()
        => Assert.Equal("ab", Invoke("repeat", S("ab"), I(1)).AsString());

    [Fact]
    public void Repeat_NegativeCount()
        => Assert.Equal("", Invoke("repeat", S("ab"), I(-1)).AsString());

    // =========================================================================
    // substring
    // =========================================================================

    [Fact]
    public void Substring_WithLength()
        => Assert.Equal("ell", Invoke("substring", S("hello"), I(1), I(3)).AsString());

    [Fact]
    public void Substring_NoLength()
        => Assert.Equal("llo", Invoke("substring", S("hello"), I(2)).AsString());

    [Fact]
    public void Substring_StartBeyond()
        => Assert.Equal("", Invoke("substring", S("hello"), I(10)).AsString());

    [Fact]
    public void Substring_NegativeStart()
        => Assert.Equal("hello", Invoke("substring", S("hello"), I(-1)).AsString());

    [Fact]
    public void Substring_Null()
        => Assert.True(Invoke("substring", Null(), I(0), I(3)).IsNull);

    [Fact]
    public void Substring_ZeroLength()
        => Assert.Equal("", Invoke("substring", S("hello"), I(0), I(0)).AsString());

    [Fact]
    public void Substring_TooFewArgs()
        => Assert.True(Invoke("substring", S("hello")).IsNull);

    // =========================================================================
    // length
    // =========================================================================

    [Fact]
    public void Length_String()
        => Assert.Equal(5, Invoke("length", S("hello")).AsInt64());

    [Fact]
    public void Length_EmptyString()
        => Assert.Equal(0, Invoke("length", S("")).AsInt64());

    [Fact]
    public void Length_Array()
        => Assert.Equal(3, Invoke("length", Arr(I(1), I(2), I(3))).AsInt64());

    [Fact]
    public void Length_Null()
        => Assert.Equal(0, Invoke("length", Null()).AsInt64());

    [Fact]
    public void Length_NoArgs()
        => Assert.Equal(0, Invoke("length").AsInt64());

    [Fact]
    public void Length_Object()
        => Assert.Equal(2, Invoke("length", Obj(("a", I(1)), ("b", I(2)))).AsInt64());

    // =========================================================================
    // camelCase
    // =========================================================================

    [Fact]
    public void CamelCase_Basic()
        => Assert.Equal("helloWorld", Invoke("camelCase", S("hello world")).AsString());

    [Fact]
    public void CamelCase_FromSnake()
        => Assert.Equal("helloWorld", Invoke("camelCase", S("hello_world")).AsString());

    [Fact]
    public void CamelCase_FromKebab()
        => Assert.Equal("helloWorld", Invoke("camelCase", S("hello-world")).AsString());

    [Fact]
    public void CamelCase_Null()
        => Assert.True(Invoke("camelCase", Null()).IsNull);

    [Fact]
    public void CamelCase_Empty()
        => Assert.Equal("", Invoke("camelCase", S("")).AsString());

    [Fact]
    public void CamelCase_SingleWord()
        => Assert.Equal("hello", Invoke("camelCase", S("hello")).AsString());

    [Fact]
    public void CamelCase_ThreeWords()
        => Assert.Equal("theQuickBrown", Invoke("camelCase", S("the quick brown")).AsString());

    // =========================================================================
    // snakeCase
    // =========================================================================

    [Fact]
    public void SnakeCase_FromCamel()
        => Assert.Equal("hello_world", Invoke("snakeCase", S("helloWorld")).AsString());

    [Fact]
    public void SnakeCase_FromSpaces()
        => Assert.Equal("hello_world", Invoke("snakeCase", S("hello world")).AsString());

    [Fact]
    public void SnakeCase_Null()
        => Assert.True(Invoke("snakeCase", Null()).IsNull);

    [Fact]
    public void SnakeCase_Empty()
        => Assert.Equal("", Invoke("snakeCase", S("")).AsString());

    [Fact]
    public void SnakeCase_FromKebab()
        => Assert.Equal("hello_world", Invoke("snakeCase", S("hello-world")).AsString());

    [Fact]
    public void SnakeCase_AlreadySnake()
        => Assert.Equal("hello_world", Invoke("snakeCase", S("hello_world")).AsString());

    // =========================================================================
    // kebabCase
    // =========================================================================

    [Fact]
    public void KebabCase_FromCamel()
        => Assert.Equal("hello-world", Invoke("kebabCase", S("helloWorld")).AsString());

    [Fact]
    public void KebabCase_FromSpaces()
        => Assert.Equal("hello-world", Invoke("kebabCase", S("hello world")).AsString());

    [Fact]
    public void KebabCase_Null()
        => Assert.True(Invoke("kebabCase", Null()).IsNull);

    [Fact]
    public void KebabCase_Empty()
        => Assert.Equal("", Invoke("kebabCase", S("")).AsString());

    [Fact]
    public void KebabCase_FromSnake()
        => Assert.Equal("hello-world", Invoke("kebabCase", S("hello_world")).AsString());

    // =========================================================================
    // pascalCase
    // =========================================================================

    [Fact]
    public void PascalCase_Basic()
        => Assert.Equal("HelloWorld", Invoke("pascalCase", S("hello world")).AsString());

    [Fact]
    public void PascalCase_FromSnake()
        => Assert.Equal("HelloWorld", Invoke("pascalCase", S("hello_world")).AsString());

    [Fact]
    public void PascalCase_Null()
        => Assert.True(Invoke("pascalCase", Null()).IsNull);

    [Fact]
    public void PascalCase_Empty()
        => Assert.Equal("", Invoke("pascalCase", S("")).AsString());

    [Fact]
    public void PascalCase_SingleWord()
        => Assert.Equal("Hello", Invoke("pascalCase", S("hello")).AsString());

    // =========================================================================
    // slugify
    // =========================================================================

    [Fact]
    public void Slugify_Basic()
        => Assert.Equal("hello-world-test", Invoke("slugify", S("Hello World! Test")).AsString());

    [Fact]
    public void Slugify_Null()
        => Assert.True(Invoke("slugify", Null()).IsNull);

    [Fact]
    public void Slugify_AlreadySlug()
        => Assert.Equal("hello-world", Invoke("slugify", S("hello-world")).AsString());

    [Fact]
    public void Slugify_SpecialChars()
        => Assert.Equal("hello-world", Invoke("slugify", S("hello & world")).AsString());

    [Fact]
    public void Slugify_Accents()
        => Assert.Equal("cafe-naive", Invoke("slugify", S("caf\u00e9 na\u00efve")).AsString());

    [Fact]
    public void Slugify_MultipleSpaces()
        => Assert.Equal("hello-world", Invoke("slugify", S("hello   world")).AsString());

    // =========================================================================
    // match
    // =========================================================================

    [Fact]
    public void Match_Found()
        => Assert.Equal("123", Invoke("match", S("abc123def"), S("\\d+")).AsString());

    [Fact]
    public void Match_NotFound()
        => Assert.True(Invoke("match", S("abc"), S("\\d+")).IsNull);

    [Fact]
    public void Match_Null()
        => Assert.True(Invoke("match", Null(), S("\\d+")).IsNull);

    [Fact]
    public void Match_TooFewArgs()
        => Assert.True(Invoke("match", S("hello")).IsNull);

    [Fact]
    public void Match_FullMatch()
        => Assert.Equal("hello", Invoke("match", S("hello"), S("^hello$")).AsString());

    // =========================================================================
    // extract
    // =========================================================================

    [Fact]
    public void Extract_WithGroups()
    {
        var result = Invoke("extract", S("2024-01-15"), S("(\\d{4})-(\\d{2})-(\\d{2})"));
        var arr = result.AsArray()!;
        Assert.Equal(3, arr.Count);
        Assert.Equal("2024", arr[0].AsString());
        Assert.Equal("01", arr[1].AsString());
        Assert.Equal("15", arr[2].AsString());
    }

    [Fact]
    public void Extract_NoGroups()
        => Assert.Equal("123", Invoke("extract", S("abc123def"), S("\\d+")).AsString());

    [Fact]
    public void Extract_NotFound()
        => Assert.True(Invoke("extract", S("abc"), S("\\d+")).IsNull);

    [Fact]
    public void Extract_Null()
        => Assert.True(Invoke("extract", Null(), S("\\d+")).IsNull);

    // =========================================================================
    // normalizeSpace
    // =========================================================================

    [Fact]
    public void NormalizeSpace_Basic()
        => Assert.Equal("hello world", Invoke("normalizeSpace", S("  hello   world  ")).AsString());

    [Fact]
    public void NormalizeSpace_Tabs()
        => Assert.Equal("hello world", Invoke("normalizeSpace", S("\thello\t\tworld\t")).AsString());

    [Fact]
    public void NormalizeSpace_Null()
        => Assert.True(Invoke("normalizeSpace", Null()).IsNull);

    [Fact]
    public void NormalizeSpace_NoExtraSpace()
        => Assert.Equal("hello world", Invoke("normalizeSpace", S("hello world")).AsString());

    [Fact]
    public void NormalizeSpace_Empty()
        => Assert.Equal("", Invoke("normalizeSpace", S("")).AsString());

    [Fact]
    public void NormalizeSpace_OnlySpaces()
        => Assert.Equal("", Invoke("normalizeSpace", S("   ")).AsString());

    // =========================================================================
    // leftOf
    // =========================================================================

    [Fact]
    public void LeftOf_Basic()
        => Assert.Equal("hello", Invoke("leftOf", S("hello@world.com"), S("@")).AsString());

    [Fact]
    public void LeftOf_NotFound()
        => Assert.Equal("hello", Invoke("leftOf", S("hello"), S("@")).AsString());

    [Fact]
    public void LeftOf_Null()
        => Assert.True(Invoke("leftOf", Null(), S("@")).IsNull);

    [Fact]
    public void LeftOf_AtStart()
        => Assert.Equal("", Invoke("leftOf", S("@hello"), S("@")).AsString());

    [Fact]
    public void LeftOf_MultiOccurrence()
        => Assert.Equal("a", Invoke("leftOf", S("a@b@c"), S("@")).AsString());

    // =========================================================================
    // rightOf
    // =========================================================================

    [Fact]
    public void RightOf_Basic()
        => Assert.Equal("world.com", Invoke("rightOf", S("hello@world.com"), S("@")).AsString());

    [Fact]
    public void RightOf_NotFound()
        => Assert.Equal("hello", Invoke("rightOf", S("hello"), S("@")).AsString());

    [Fact]
    public void RightOf_Null()
        => Assert.True(Invoke("rightOf", Null(), S("@")).IsNull);

    [Fact]
    public void RightOf_AtEnd()
        => Assert.Equal("", Invoke("rightOf", S("hello@"), S("@")).AsString());

    [Fact]
    public void RightOf_MultiOccurrence()
        => Assert.Equal("b@c", Invoke("rightOf", S("a@b@c"), S("@")).AsString());

    // =========================================================================
    // wrap
    // =========================================================================

    [Fact]
    public void Wrap_SameChar()
        => Assert.Equal("\"hello\"", Invoke("wrap", S("hello"), S("\"")).AsString());

    [Fact]
    public void Wrap_DifferentChars()
        => Assert.Equal("(hello)", Invoke("wrap", S("hello"), S("("), S(")")).AsString());

    [Fact]
    public void Wrap_Null()
        => Assert.True(Invoke("wrap", Null(), S("\"")).IsNull);

    [Fact]
    public void Wrap_Empty()
        => Assert.Equal("\"\"", Invoke("wrap", S(""), S("\"")).AsString());

    // =========================================================================
    // center
    // =========================================================================

    [Fact]
    public void Center_Basic()
        => Assert.Equal("--hi--", Invoke("center", S("hi"), I(6), S("-")).AsString());

    [Fact]
    public void Center_DefaultPad()
    {
        var result = Invoke("center", S("hi"), I(6)).AsString();
        Assert.Equal(6, result!.Length);
        Assert.Contains("hi", result);
    }

    [Fact]
    public void Center_AlreadyLong()
        => Assert.Equal("hello", Invoke("center", S("hello"), I(3), S("-")).AsString());

    [Fact]
    public void Center_Null()
        => Assert.True(Invoke("center", Null(), I(6)).IsNull);

    [Fact]
    public void Center_OddWidth()
    {
        var result = Invoke("center", S("hi"), I(5), S("-")).AsString();
        Assert.Equal(5, result!.Length);
    }

    // =========================================================================
    // matches (regex boolean)
    // =========================================================================

    [Fact]
    public void Matches_True()
        => Assert.True(Invoke("matches", S("abc123"), S("\\d+")).AsBool()!.Value);

    [Fact]
    public void Matches_False()
        => Assert.False(Invoke("matches", S("abc"), S("\\d+")).AsBool()!.Value);

    [Fact]
    public void Matches_NullInput()
        => Assert.False(Invoke("matches", Null(), S("\\d+")).AsBool()!.Value);

    [Fact]
    public void Matches_TooFewArgs()
        => Assert.False(Invoke("matches", S("hello")).AsBool()!.Value);

    [Fact]
    public void Matches_InvalidRegex()
        => Assert.False(Invoke("matches", S("hello"), S("[invalid")).AsBool()!.Value);

    [Fact]
    public void Matches_FullPattern()
        => Assert.True(Invoke("matches", S("hello"), S("^hello$")).AsBool()!.Value);

    // =========================================================================
    // stripAccents
    // =========================================================================

    [Fact]
    public void StripAccents_Basic()
        => Assert.Equal("cafe naive n", Invoke("stripAccents", S("caf\u00e9 na\u00efve \u00f1")).AsString());

    [Fact]
    public void StripAccents_NoAccents()
        => Assert.Equal("hello", Invoke("stripAccents", S("hello")).AsString());

    [Fact]
    public void StripAccents_Null()
        => Assert.True(Invoke("stripAccents", Null()).IsNull);

    [Fact]
    public void StripAccents_Empty()
        => Assert.Equal("", Invoke("stripAccents", S("")).AsString());

    [Fact]
    public void StripAccents_Umlauts()
        => Assert.Equal("uber", Invoke("stripAccents", S("\u00fcber")).AsString());

    // =========================================================================
    // clean
    // =========================================================================

    [Fact]
    public void Clean_RemovesControlChars()
        => Assert.Equal("helloworld\n", Invoke("clean", S("hello\x00\x01world\n")).AsString());

    [Fact]
    public void Clean_KeepsPrintable()
        => Assert.Equal("hello world", Invoke("clean", S("hello world")).AsString());

    [Fact]
    public void Clean_Null()
        => Assert.True(Invoke("clean", Null()).IsNull);

    [Fact]
    public void Clean_Empty()
        => Assert.Equal("", Invoke("clean", S("")).AsString());

    [Fact]
    public void Clean_KeepsTabs()
        => Assert.Equal("\thello\t", Invoke("clean", S("\thello\t")).AsString());

    // =========================================================================
    // wordCount
    // =========================================================================

    [Fact]
    public void WordCount_Basic()
        => Assert.Equal(3, Invoke("wordCount", S("hello beautiful world")).AsInt64());

    [Fact]
    public void WordCount_Empty()
        => Assert.Equal(0, Invoke("wordCount", S("")).AsInt64());

    [Fact]
    public void WordCount_Null()
        => Assert.Equal(0, Invoke("wordCount", Null()).AsInt64());

    [Fact]
    public void WordCount_SingleWord()
        => Assert.Equal(1, Invoke("wordCount", S("hello")).AsInt64());

    [Fact]
    public void WordCount_ExtraSpaces()
        => Assert.Equal(2, Invoke("wordCount", S("  hello   world  ")).AsInt64());

    [Fact]
    public void WordCount_NoArgs()
        => Assert.Equal(0, Invoke("wordCount").AsInt64());

    // =========================================================================
    // tokenize
    // =========================================================================

    [Fact]
    public void Tokenize_Basic()
    {
        var result = Invoke("tokenize", S("hello beautiful world"));
        var arr = result.AsArray()!;
        Assert.Equal(3, arr.Count);
        Assert.Equal("hello", arr[0].AsString());
        Assert.Equal("beautiful", arr[1].AsString());
        Assert.Equal("world", arr[2].AsString());
    }

    [Fact]
    public void Tokenize_Empty()
    {
        var result = Invoke("tokenize", S(""));
        Assert.Empty(result.AsArray()!);
    }

    [Fact]
    public void Tokenize_Null()
    {
        var result = Invoke("tokenize", Null());
        Assert.Empty(result.AsArray()!);
    }

    [Fact]
    public void Tokenize_ExtraSpaces()
    {
        var result = Invoke("tokenize", S("  hello   world  "));
        var arr = result.AsArray()!;
        Assert.Equal(2, arr.Count);
    }

    // =========================================================================
    // levenshtein
    // =========================================================================

    [Fact]
    public void Levenshtein_Identical()
        => Assert.Equal(0, Invoke("levenshtein", S("hello"), S("hello")).AsInt64());

    [Fact]
    public void Levenshtein_OneEdit()
        => Assert.Equal(1, Invoke("levenshtein", S("hello"), S("hallo")).AsInt64());

    [Fact]
    public void Levenshtein_CompletelyDifferent()
        => Assert.Equal(3, Invoke("levenshtein", S("abc"), S("xyz")).AsInt64());

    [Fact]
    public void Levenshtein_EmptyFirst()
        => Assert.Equal(5, Invoke("levenshtein", S(""), S("hello")).AsInt64());

    [Fact]
    public void Levenshtein_EmptySecond()
        => Assert.Equal(5, Invoke("levenshtein", S("hello"), S("")).AsInt64());

    [Fact]
    public void Levenshtein_BothEmpty()
        => Assert.Equal(0, Invoke("levenshtein", S(""), S("")).AsInt64());

    [Fact]
    public void Levenshtein_Null()
        => Assert.Equal(0, Invoke("levenshtein", Null(), S("hello")).AsInt64());

    [Fact]
    public void Levenshtein_Insertion()
        => Assert.Equal(1, Invoke("levenshtein", S("hello"), S("helloo")).AsInt64());

    [Fact]
    public void Levenshtein_Deletion()
        => Assert.Equal(1, Invoke("levenshtein", S("hello"), S("hell")).AsInt64());

    // =========================================================================
    // soundex
    // =========================================================================

    [Fact]
    public void Soundex_Robert()
        => Assert.Equal("R163", Invoke("soundex", S("Robert")).AsString());

    [Fact]
    public void Soundex_Rupert()
        => Assert.Equal("R163", Invoke("soundex", S("Rupert")).AsString());

    [Fact]
    public void Soundex_Ashcraft()
        => Assert.Equal("A226", Invoke("soundex", S("Ashcraft")).AsString());

    [Fact]
    public void Soundex_Null()
        => Assert.True(Invoke("soundex", Null()).IsNull);

    [Fact]
    public void Soundex_Empty()
        => Assert.Equal("", Invoke("soundex", S("")).AsString());

    [Fact]
    public void Soundex_SingleChar()
        => Assert.Equal("A000", Invoke("soundex", S("A")).AsString());

    // =========================================================================
    // base64Encode / base64Decode
    // =========================================================================

    [Fact]
    public void Base64Encode_Basic()
        => Assert.Equal("SGVsbG8=", Invoke("base64Encode", S("Hello")).AsString());

    [Fact]
    public void Base64Decode_Basic()
        => Assert.Equal("Hello", Invoke("base64Decode", S("SGVsbG8=")).AsString());

    [Fact]
    public void Base64Encode_Empty()
        => Assert.Equal("", Invoke("base64Encode", S("")).AsString());

    [Fact]
    public void Base64Decode_Empty()
        => Assert.Equal("", Invoke("base64Decode", S("")).AsString());

    [Fact]
    public void Base64Encode_Null()
        => Assert.True(Invoke("base64Encode", Null()).IsNull);

    [Fact]
    public void Base64Decode_Null()
        => Assert.True(Invoke("base64Decode", Null()).IsNull);

    [Fact]
    public void Base64Decode_InvalidInput()
        => Assert.True(Invoke("base64Decode", S("not-valid-base64!!!")).IsNull);

    [Fact]
    public void Base64_Roundtrip()
    {
        var original = "Hello, World! \u00e9";
        var encoded = Invoke("base64Encode", S(original));
        var decoded = Invoke("base64Decode", encoded);
        Assert.Equal(original, decoded.AsString());
    }

    [Fact]
    public void Base64Encode_NoArgs()
        => Assert.True(Invoke("base64Encode").IsNull);

    // =========================================================================
    // urlEncode / urlDecode
    // =========================================================================

    [Fact]
    public void UrlEncode_Basic()
        => Assert.Equal("hello%20world%26foo%3Dbar", Invoke("urlEncode", S("hello world&foo=bar")).AsString());

    [Fact]
    public void UrlDecode_Basic()
        => Assert.Equal("hello world&foo=bar", Invoke("urlDecode", S("hello%20world%26foo%3Dbar")).AsString());

    [Fact]
    public void UrlEncode_Null()
        => Assert.True(Invoke("urlEncode", Null()).IsNull);

    [Fact]
    public void UrlDecode_Null()
        => Assert.True(Invoke("urlDecode", Null()).IsNull);

    [Fact]
    public void UrlEncode_NoSpecialChars()
        => Assert.Equal("hello", Invoke("urlEncode", S("hello")).AsString());

    [Fact]
    public void Url_Roundtrip()
    {
        var original = "name=John Doe&age=30";
        var encoded = Invoke("urlEncode", S(original));
        var decoded = Invoke("urlDecode", encoded);
        Assert.Equal(original, decoded.AsString());
    }

    [Fact]
    public void UrlEncode_NoArgs()
        => Assert.True(Invoke("urlEncode").IsNull);

    // =========================================================================
    // hexEncode / hexDecode
    // =========================================================================

    [Fact]
    public void HexEncode_Basic()
        => Assert.Equal("4869", Invoke("hexEncode", S("Hi")).AsString());

    [Fact]
    public void HexDecode_Basic()
        => Assert.Equal("Hi", Invoke("hexDecode", S("4869")).AsString());

    [Fact]
    public void HexEncode_Null()
        => Assert.True(Invoke("hexEncode", Null()).IsNull);

    [Fact]
    public void HexDecode_Null()
        => Assert.True(Invoke("hexDecode", Null()).IsNull);

    [Fact]
    public void HexDecode_InvalidHex()
        => Assert.True(Invoke("hexDecode", S("ZZZZ")).IsNull);

    [Fact]
    public void Hex_Roundtrip()
    {
        var original = "Hello, World!";
        var encoded = Invoke("hexEncode", S(original));
        var decoded = Invoke("hexDecode", encoded);
        Assert.Equal(original, decoded.AsString());
    }

    [Fact]
    public void HexEncode_NoArgs()
        => Assert.True(Invoke("hexEncode").IsNull);

    // =========================================================================
    // jsonEncode / jsonDecode
    // =========================================================================

    [Fact]
    public void JsonEncode_Object()
    {
        var result = Invoke("jsonEncode", Obj(("a", I(1)), ("b", S("two"))));
        Assert.Equal("{\"a\":1,\"b\":\"two\"}", result.AsString());
    }

    [Fact]
    public void JsonDecode_Object()
    {
        var result = Invoke("jsonDecode", S("{\"x\":42}"));
        Assert.Equal(42, result.Get("x")!.AsInt64());
    }

    [Fact]
    public void JsonEncode_Null()
        => Assert.Equal("null", Invoke("jsonEncode", Null()).AsString());

    [Fact]
    public void JsonDecode_Null()
        => Assert.True(Invoke("jsonDecode", Null()).IsNull);

    [Fact]
    public void JsonDecode_InvalidJson()
        => Assert.True(Invoke("jsonDecode", S("not json")).IsNull);

    [Fact]
    public void JsonEncode_Array()
    {
        var result = Invoke("jsonEncode", Arr(I(1), I(2), I(3)));
        Assert.Equal("[1,2,3]", result.AsString());
    }

    [Fact]
    public void JsonEncode_String()
        => Assert.Equal("\"hello\"", Invoke("jsonEncode", S("hello")).AsString());

    [Fact]
    public void JsonEncode_Integer()
        => Assert.Equal("42", Invoke("jsonEncode", I(42)).AsString());

    [Fact]
    public void JsonEncode_Bool()
        => Assert.Equal("true", Invoke("jsonEncode", B(true)).AsString());

    [Fact]
    public void JsonEncode_NoArgs()
        => Assert.True(Invoke("jsonEncode").IsNull);

    // =========================================================================
    // sha256
    // =========================================================================

    [Fact]
    public void Sha256_Basic()
    {
        var result = Invoke("sha256", S("hello")).AsString();
        Assert.Equal(64, result!.Length); // SHA-256 produces 64 hex chars
        Assert.Equal("2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824", result);
    }

    [Fact]
    public void Sha256_Null()
        => Assert.True(Invoke("sha256", Null()).IsNull);

    [Fact]
    public void Sha256_Empty()
    {
        var result = Invoke("sha256", S("")).AsString();
        Assert.Equal(64, result!.Length);
    }

    [Fact]
    public void Sha256_NoArgs()
        => Assert.True(Invoke("sha256").IsNull);

    // =========================================================================
    // sha1
    // =========================================================================

    [Fact]
    public void Sha1_Basic()
    {
        var result = Invoke("sha1", S("hello")).AsString();
        Assert.Equal(40, result!.Length); // SHA-1 produces 40 hex chars
    }

    [Fact]
    public void Sha1_Null()
        => Assert.True(Invoke("sha1", Null()).IsNull);

    [Fact]
    public void Sha1_NoArgs()
        => Assert.True(Invoke("sha1").IsNull);

    // =========================================================================
    // sha512
    // =========================================================================

    [Fact]
    public void Sha512_Basic()
    {
        var result = Invoke("sha512", S("hello")).AsString();
        Assert.Equal(128, result!.Length); // SHA-512 produces 128 hex chars
    }

    [Fact]
    public void Sha512_Null()
        => Assert.True(Invoke("sha512", Null()).IsNull);

    // =========================================================================
    // md5
    // =========================================================================

    [Fact]
    public void Md5_Basic()
    {
        var result = Invoke("md5", S("hello")).AsString();
        Assert.Equal(32, result!.Length); // MD5 produces 32 hex chars
        Assert.Equal("5d41402abc4b2a76b9719d911017c592", result);
    }

    [Fact]
    public void Md5_Null()
        => Assert.True(Invoke("md5", Null()).IsNull);

    [Fact]
    public void Md5_NoArgs()
        => Assert.True(Invoke("md5").IsNull);

    // =========================================================================
    // crc32
    // =========================================================================

    [Fact]
    public void Crc32_Basic()
    {
        var result = Invoke("crc32", S("hello")).AsString();
        Assert.Equal(8, result!.Length); // CRC32 produces 8 hex chars
    }

    [Fact]
    public void Crc32_Null()
        => Assert.True(Invoke("crc32", Null()).IsNull);

    [Fact]
    public void Crc32_NoArgs()
        => Assert.True(Invoke("crc32").IsNull);

    // =========================================================================
    // jsonPath
    // =========================================================================

    [Fact]
    public void JsonPath_BasicLookup()
    {
        var obj = Obj(("user", Obj(("name", S("Alice")))));
        var result = Invoke("jsonPath", obj, S("user.name"));
        Assert.Equal("Alice", result.AsString());
    }

    [Fact]
    public void JsonPath_ArrayIndex()
    {
        var obj = Obj(("items", Arr(S("a"), S("b"), S("c"))));
        var result = Invoke("jsonPath", obj, S("items[1]"));
        Assert.Equal("b", result.AsString());
    }

    [Fact]
    public void JsonPath_NotFound()
    {
        var obj = Obj(("a", I(1)));
        var result = Invoke("jsonPath", obj, S("b"));
        Assert.True(result.IsNull);
    }

    [Fact]
    public void JsonPath_FromJsonString()
    {
        var result = Invoke("jsonPath", S("{\"a\":42}"), S("a"));
        Assert.Equal(42, result.AsInt64());
    }

    [Fact]
    public void JsonPath_TooFewArgs()
        => Assert.True(Invoke("jsonPath", S("{}")).IsNull);

    [Fact]
    public void JsonPath_NullPath()
        => Assert.True(Invoke("jsonPath", Obj(("a", I(1))), Null()).IsNull);
}
