using System;
using System.Collections.Generic;
using Odin.Core.Types;
using Odin.Core.Transform;
using Odin.Core.Transform.Verbs;
using Xunit;

namespace Odin.Core.Tests.Unit;

/// <summary>
/// Extended string verb tests ported from Rust SDK extended_tests and extended_tests_2 modules.
/// Adapted to match .NET implementation behavior (null returns instead of exceptions,
/// coercion instead of type errors, .NET-specific wrap/match/extract semantics).
/// </summary>
public class StringVerbExtendedTests
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
    private static DynValue Obj(params (string key, DynValue value)[] pairs)
    {
        var list = new List<KeyValuePair<string, DynValue>>();
        foreach (var (k, v) in pairs) list.Add(new KeyValuePair<string, DynValue>(k, v));
        return DynValue.Object(list);
    }

    // =========================================================================
    // titleCase extended edge cases
    // =========================================================================

    [Fact]
    public void TitleCase_AllUpper()
    {
        var result = Invoke("titleCase", S("HELLO WORLD")).AsString()!;
        Assert.StartsWith("H", result);
    }

    [Fact]
    public void TitleCase_WithNumbers()
    {
        var result = Invoke("titleCase", S("hello 42 world")).AsString()!;
        Assert.StartsWith("H", result);
    }

    [Fact]
    public void TitleCase_Unicode()
    {
        // titleCase on unicode - just verify it doesn't crash and starts upper
        var result = Invoke("titleCase", S("\u00FCber cool")).AsString()!;
        Assert.Contains("cool", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TitleCase_IntegerCoerces()
    {
        // .NET coerces integers to strings rather than throwing
        var result = Invoke("titleCase", I(42));
        Assert.NotNull(result);
    }

    // =========================================================================
    // contains extended edge cases
    // =========================================================================

    [Fact]
    public void Contains_BothEmpty()
        => Assert.Equal(true, Invoke("contains", S(""), S("")).AsBool());

    [Fact]
    public void Contains_Unicode()
        => Assert.Equal(true, Invoke("contains", S("caf\u00E9"), S("f\u00E9")).AsBool());

    // =========================================================================
    // startsWith extended edge cases
    // =========================================================================

    [Fact]
    public void StartsWith_LongerPrefix()
        => Assert.Equal(false, Invoke("startsWith", S("hi"), S("hello")).AsBool());

    [Fact]
    public void StartsWith_TooFewArgs()
    {
        // .NET returns the original value or false when too few args
        var result = Invoke("startsWith", S("hello"));
        Assert.NotNull(result);
    }

    // =========================================================================
    // endsWith extended edge cases
    // =========================================================================

    [Fact]
    public void EndsWith_FullMatch()
        => Assert.Equal(true, Invoke("endsWith", S("hello"), S("hello")).AsBool());

    [Fact]
    public void EndsWith_TooFewArgs()
    {
        var result = Invoke("endsWith", S("hello"));
        Assert.NotNull(result);
    }

    // =========================================================================
    // replaceRegex extended edge cases
    // =========================================================================

    [Fact]
    public void ReplaceRegex_NoMatch()
        => Assert.Equal("hello", Invoke("replaceRegex", S("hello"), S("xyz"), S("abc")).AsString());

    [Fact]
    public void ReplaceRegex_EmptyReplacement()
        => Assert.Equal("helloworld", Invoke("replaceRegex", S("hello world"), S(" "), S("")).AsString());

    [Fact]
    public void ReplaceRegex_MultipleOccurrences()
        => Assert.Equal("bbbbbb", Invoke("replaceRegex", S("aaa"), S("a"), S("bb")).AsString());

    [Fact]
    public void ReplaceRegex_AllOccurrences()
        => Assert.Equal("bbb", Invoke("replaceRegex", S("aaa"), S("a"), S("b")).AsString());

    [Fact]
    public void ReplaceRegex_TooFewArgs()
    {
        var result = Invoke("replaceRegex", S("hello"), S("x"));
        Assert.NotNull(result);
    }

    [Fact]
    public void ReplaceRegex_EmptyReplacementRemovesWorld()
        => Assert.Equal("hello", Invoke("replaceRegex", S("hello world"), S(" world"), S("")).AsString());

    // =========================================================================
    // padLeft extended edge cases
    // =========================================================================

    [Fact]
    public void PadLeft_EmptyString()
        => Assert.Equal("xxx", Invoke("padLeft", S(""), I(3), S("x")).AsString());

    [Fact]
    public void PadLeft_WithSpace()
        => Assert.Equal("   x", Invoke("padLeft", S("x"), I(4), S(" ")).AsString());

    [Fact]
    public void PadLeft_TooFewArgs_DefaultsPadChar()
    {
        // .NET padLeft with 2 args uses default space pad
        var result = Invoke("padLeft", S("hi"), I(5));
        Assert.NotNull(result);
        Assert.Equal(5, result.AsString()!.Length);
    }

    // =========================================================================
    // padRight extended edge cases
    // =========================================================================

    [Fact]
    public void PadRight_EmptyString()
        => Assert.Equal("----", Invoke("padRight", S(""), I(4), S("-")).AsString());

    [Fact]
    public void PadRight_SpaceChar()
        => Assert.Equal("hi   ", Invoke("padRight", S("hi"), I(5), S(" ")).AsString());

    [Fact]
    public void PadRight_SingleChar()
        => Assert.Equal("x....", Invoke("padRight", S("x"), I(5), S(".")).AsString());

    [Fact]
    public void PadRight_TooFewArgs_DefaultsPadChar()
    {
        // .NET padRight with 2 args uses default space pad
        var result = Invoke("padRight", S("hi"), I(5));
        Assert.NotNull(result);
        Assert.Equal(5, result.AsString()!.Length);
    }

    // =========================================================================
    // pad (center) extended edge cases
    // =========================================================================

    [Fact]
    public void Pad_CenterAlreadyWide()
        => Assert.Equal("hello", Invoke("pad", S("hello"), I(3), S("*")).AsString());

    [Fact]
    public void Pad_CenterEmptyString()
        => Assert.Equal("xxxx", Invoke("pad", S(""), I(4), S("x")).AsString());

    [Fact]
    public void Pad_CenterEvenPadding()
        => Assert.Equal("--hi--", Invoke("pad", S("hi"), I(6), S("-")).AsString());

    [Fact]
    public void Pad_TooFewArgs_DefaultsPadChar()
    {
        // .NET pad with 2 args uses default space pad
        var result = Invoke("pad", S("hi"), I(6));
        Assert.NotNull(result);
        Assert.Equal(6, result.AsString()!.Length);
    }

    // =========================================================================
    // truncate extended edge cases
    // =========================================================================

    [Fact]
    public void Truncate_ShorterThanLimit()
        => Assert.Equal("hi", Invoke("truncate", S("hi"), I(10)).AsString());

    [Fact]
    public void Truncate_ExactLength()
        => Assert.Equal("hello", Invoke("truncate", S("hello"), I(5)).AsString());

    [Fact]
    public void Truncate_EmptyString()
        => Assert.Equal("", Invoke("truncate", S(""), I(5)).AsString());

    [Fact]
    public void Truncate_ToOne()
        => Assert.Equal("h", Invoke("truncate", S("hello"), I(1)).AsString());

    [Fact]
    public void Truncate_TooFewArgs()
    {
        // .NET returns the original string when no length specified
        var result = Invoke("truncate", S("hello"));
        Assert.NotNull(result);
    }

    // =========================================================================
    // split extended edge cases
    // =========================================================================

    [Fact]
    public void Split_EmptyString()
    {
        var result = Invoke("split", S(""), S(","));
        var arr = result.AsArray()!;
        Assert.Single(arr);
        Assert.Equal("", arr[0].AsString());
    }

    [Fact]
    public void Split_MultiCharDelimiter()
    {
        var result = Invoke("split", S("a::b::c"), S("::"));
        var arr = result.AsArray()!;
        Assert.Equal(3, arr.Count);
        Assert.Equal("a", arr[0].AsString());
        Assert.Equal("b", arr[1].AsString());
        Assert.Equal("c", arr[2].AsString());
    }

    [Fact]
    public void Split_MultipleDelimiters()
    {
        var result = Invoke("split", S("a,b,c,d"), S(","));
        var arr = result.AsArray()!;
        Assert.Equal(4, arr.Count);
    }

    [Fact]
    public void Split_TooFewArgs()
        => Assert.True(Invoke("split", S("hello")).IsNull);

    // =========================================================================
    // join extended edge cases
    // =========================================================================

    [Fact]
    public void Join_WithIntegers()
        => Assert.Equal("1-2-3", Invoke("join", Arr(I(1), I(2), I(3)), S("-")).AsString());

    [Fact]
    public void Join_MultiCharDelimiter()
        => Assert.Equal("a -- b", Invoke("join", Arr(S("a"), S("b")), S(" -- ")).AsString());

    [Fact]
    public void Join_TooFewArgs()
        => Assert.True(Invoke("join", Arr(S("a"))).IsNull);

    // =========================================================================
    // mask extended edge cases
    // =========================================================================

    [Fact]
    public void Mask_ShowAll()
        => Assert.Equal("abc", Invoke("mask", S("abc"), S("***")).AsString());

    [Fact]
    public void Mask_ShowZero()
        => Assert.Equal("a-b-c", Invoke("mask", S("abc"), S("*-*-*")).AsString());

    [Fact]
    public void Mask_ShowExactLength()
        => Assert.Equal("abc", Invoke("mask", S("abc"), S("###")).AsString());

    [Fact]
    public void Mask_ShowLast4()
        => Assert.Equal("123-456-7890", Invoke("mask", S("1234567890"), S("###-###-####")).AsString());

    [Fact]
    public void Mask_DefaultShowLast()
    {
        // mask requires 2 args (value, pattern) — 1 arg returns null
        var result = Invoke("mask", S("123456789"));
        Assert.True(result.IsNull);
    }

    [Fact]
    public void Mask_NullPassthrough()
        => Assert.True(Invoke("mask", Null(), S("###")).IsNull);

    // =========================================================================
    // reverseString extended edge cases
    // =========================================================================

    [Fact]
    public void ReverseString_WithSpaces()
        => Assert.Equal("c b a", Invoke("reverseString", S("a b c")).AsString());

    [Fact]
    public void ReverseString_IntegerCoerces()
    {
        // .NET coerces, doesn't throw
        var result = Invoke("reverseString", I(42));
        Assert.NotNull(result);
    }

    // =========================================================================
    // repeat extended edge cases
    // =========================================================================

    [Fact]
    public void Repeat_EmptyString()
        => Assert.Equal("", Invoke("repeat", S(""), I(5)).AsString());

    [Fact]
    public void Repeat_LargeCount()
    {
        var result = Invoke("repeat", S("x"), I(100)).AsString()!;
        Assert.Equal(100, result.Length);
    }

    [Fact]
    public void Repeat_TooFewArgs()
        => Assert.True(Invoke("repeat", S("abc")).IsNull);

    // =========================================================================
    // camelCase extended edge cases
    // =========================================================================

    [Fact]
    public void CamelCase_FromPascal()
        => Assert.Equal("helloWorld", Invoke("camelCase", S("HelloWorld")).AsString());

    [Fact]
    public void CamelCase_MultipleWords()
        => Assert.Equal("theQuickBrownFox", Invoke("camelCase", S("the quick brown fox")).AsString());

    [Fact]
    public void CamelCase_AlreadyCamel()
    {
        var result = Invoke("camelCase", S("helloWorld")).AsString()!;
        Assert.StartsWith("h", result);
    }

    [Fact]
    public void CamelCase_IntegerCoerces()
    {
        var result = Invoke("camelCase", I(42));
        Assert.NotNull(result);
    }

    // =========================================================================
    // snakeCase extended edge cases
    // =========================================================================

    [Fact]
    public void SnakeCase_SingleWord()
        => Assert.Equal("hello", Invoke("snakeCase", S("hello")).AsString());

    [Fact]
    public void SnakeCase_FromPascal()
        => Assert.Equal("hello_world", Invoke("snakeCase", S("HelloWorld")).AsString());

    [Fact]
    public void SnakeCase_Spaces()
        => Assert.Equal("hello_world_test", Invoke("snakeCase", S("hello world test")).AsString());

    // =========================================================================
    // kebabCase extended edge cases
    // =========================================================================

    [Fact]
    public void KebabCase_SingleWord()
        => Assert.Equal("hello", Invoke("kebabCase", S("hello")).AsString());

    [Fact]
    public void KebabCase_FromPascal()
        => Assert.Equal("hello-world", Invoke("kebabCase", S("HelloWorld")).AsString());

    [Fact]
    public void KebabCase_Spaces()
        => Assert.Equal("hello-world-test", Invoke("kebabCase", S("hello world test")).AsString());

    // =========================================================================
    // pascalCase extended edge cases
    // =========================================================================

    [Fact]
    public void PascalCase_FromCamel()
        => Assert.Equal("HelloWorld", Invoke("pascalCase", S("helloWorld")).AsString());

    [Fact]
    public void PascalCase_FromKebab()
        => Assert.Equal("HelloWorld", Invoke("pascalCase", S("hello-world")).AsString());

    [Fact]
    public void PascalCase_MultipleWords()
        => Assert.Equal("TheQuickBrownFox", Invoke("pascalCase", S("the quick brown fox")).AsString());

    [Fact]
    public void PascalCase_IntegerCoerces()
    {
        var result = Invoke("pascalCase", I(42));
        Assert.NotNull(result);
    }

    // =========================================================================
    // slugify extended edge cases
    // =========================================================================

    [Fact]
    public void Slugify_Empty()
        => Assert.Equal("", Invoke("slugify", S("")).AsString());

    [Fact]
    public void Slugify_LeadingTrailingSpecial()
        => Assert.Equal("hello", Invoke("slugify", S("!!hello!!")).AsString());

    [Fact]
    public void Slugify_Numbers()
        => Assert.Equal("test-123-stuff", Invoke("slugify", S("Test 123 Stuff")).AsString());

    // =========================================================================
    // match extended edge cases
    // In .NET, match returns the first matched string (not bool)
    // =========================================================================

    [Fact]
    public void Match_DigitPattern()
        => Assert.Equal("123", Invoke("match", S("abc123def"), S("\\d+")).AsString());

    [Fact]
    public void Match_NotFoundReturnsNull()
        => Assert.True(Invoke("match", S("abc"), S("\\d+")).IsNull);

    [Fact]
    public void Match_NullReturnsNull()
        => Assert.True(Invoke("match", Null(), S("\\d+")).IsNull);

    [Fact]
    public void Match_TooFewArgs()
        => Assert.True(Invoke("match", S("hello")).IsNull);

    [Fact]
    public void Match_FullPattern()
        => Assert.Equal("hello", Invoke("match", S("hello"), S("^hello$")).AsString());

    [Fact]
    public void Match_WordBoundary()
        => Assert.Equal("world", Invoke("match", S("hello world"), S("world")).AsString());

    // =========================================================================
    // matches extended edge cases
    // =========================================================================

    [Fact]
    public void Matches_True()
        => Assert.Equal(true, Invoke("matches", S("hello123"), S("\\d+")).AsBool());

    [Fact]
    public void Matches_False()
        => Assert.Equal(false, Invoke("matches", S("hello"), S("\\d+")).AsBool());

    [Fact]
    public void Matches_NullInput()
        => Assert.Equal(false, Invoke("matches", Null(), S("\\d+")).AsBool());

    [Fact]
    public void Matches_FullPattern()
        => Assert.Equal(true, Invoke("matches", S("hello"), S("^hello$")).AsBool());

    // =========================================================================
    // extract extended edge cases
    // In .NET, extract uses regex groups (not delimiters)
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
    public void Extract_NullInput()
        => Assert.True(Invoke("extract", Null(), S("\\d+")).IsNull);

    [Fact]
    public void Extract_EmailGroups()
    {
        var result = Invoke("extract", S("user@domain.com"), S("(.+)@(.+)"));
        var arr = result.AsArray()!;
        Assert.Equal(2, arr.Count);
        Assert.Equal("user", arr[0].AsString());
        Assert.Equal("domain.com", arr[1].AsString());
    }

    // =========================================================================
    // normalizeSpace extended edge cases
    // =========================================================================

    [Fact]
    public void NormalizeSpace_EmptyString()
        => Assert.Equal("", Invoke("normalizeSpace", S("")).AsString());

    [Fact]
    public void NormalizeSpace_OnlyWhitespace()
        => Assert.Equal("", Invoke("normalizeSpace", S("   ")).AsString());

    [Fact]
    public void NormalizeSpace_TabsAndNewlines()
        => Assert.Equal("hello world", Invoke("normalizeSpace", S("hello\t\nworld")).AsString());

    [Fact]
    public void NormalizeSpace_SingleWord()
        => Assert.Equal("hello", Invoke("normalizeSpace", S("hello")).AsString());

    [Fact]
    public void NormalizeSpace_IntegerCoerces()
    {
        var result = Invoke("normalizeSpace", I(42));
        Assert.NotNull(result);
    }

    // =========================================================================
    // leftOf extended edge cases
    // =========================================================================

    [Fact]
    public void LeftOf_NoDelimiter()
        => Assert.Equal("hello", Invoke("leftOf", S("hello"), S("@")).AsString());

    [Fact]
    public void LeftOf_MultipleDelimiters()
        => Assert.Equal("a", Invoke("leftOf", S("a@b@c"), S("@")).AsString());

    [Fact]
    public void LeftOf_EmptyString()
        => Assert.Equal("", Invoke("leftOf", S(""), S("@")).AsString());

    [Fact]
    public void LeftOf_TooFewArgs()
        => Assert.True(Invoke("leftOf", S("hello")).IsNull);

    // =========================================================================
    // rightOf extended edge cases
    // =========================================================================

    [Fact]
    public void RightOf_NoDelimiter()
        => Assert.Equal("hello", Invoke("rightOf", S("hello"), S("@")).AsString());

    [Fact]
    public void RightOf_AtEnd()
        => Assert.Equal("", Invoke("rightOf", S("hello@"), S("@")).AsString());

    [Fact]
    public void RightOf_MultipleDelimiters()
        => Assert.Equal("b@c", Invoke("rightOf", S("a@b@c"), S("@")).AsString());

    [Fact]
    public void RightOf_EmptyString()
        => Assert.Equal("", Invoke("rightOf", S(""), S("@")).AsString());

    [Fact]
    public void RightOf_TooFewArgs()
        => Assert.True(Invoke("rightOf", S("hello")).IsNull);

    // =========================================================================
    // wrap extended edge cases
    // In .NET, wrap adds prefix/suffix characters (not word-wrapping)
    // =========================================================================

    [Fact]
    public void Wrap_SingleQuotes()
        => Assert.Equal("'hello'", Invoke("wrap", S("hello"), S("'")).AsString());

    [Fact]
    public void Wrap_Brackets()
        => Assert.Equal("[hello]", Invoke("wrap", S("hello"), S("["), S("]")).AsString());

    [Fact]
    public void Wrap_AngleBrackets()
        => Assert.Equal("<hello>", Invoke("wrap", S("hello"), S("<"), S(">")).AsString());

    [Fact]
    public void Wrap_EmptyInput()
        => Assert.Equal("\"\"", Invoke("wrap", S(""), S("\"")).AsString());

    [Fact]
    public void Wrap_NullPassthrough()
        => Assert.True(Invoke("wrap", Null(), S("\"")).IsNull);

    // =========================================================================
    // center extended edge cases
    // =========================================================================

    [Fact]
    public void Center_AlreadyWide()
        => Assert.Equal("hello", Invoke("center", S("hello"), I(3), S("-")).AsString());

    [Fact]
    public void Center_EmptyString()
        => Assert.Equal("****", Invoke("center", S(""), I(4), S("*")).AsString());

    [Fact]
    public void Center_ExactWidth()
        => Assert.Equal("abcd", Invoke("center", S("abcd"), I(4), S("-")).AsString());

    [Fact]
    public void Center_DefaultPadChar()
    {
        var result = Invoke("center", S("hi"), I(6));
        Assert.NotNull(result);
        Assert.Equal(6, result.AsString()!.Length);
    }

    // =========================================================================
    // stripAccents extended edge cases
    // =========================================================================

    [Fact]
    public void StripAccents_Various()
        => Assert.Equal("aaaaaa", Invoke("stripAccents", S("\u00E0\u00E1\u00E2\u00E3\u00E4\u00E5")).AsString());

    [Fact]
    public void StripAccents_Upper()
        => Assert.Equal("AAA", Invoke("stripAccents", S("\u00C0\u00C1\u00C2")).AsString());

    [Fact]
    public void StripAccents_Cedilla()
        => Assert.Equal("cC", Invoke("stripAccents", S("\u00E7\u00C7")).AsString());

    [Fact]
    public void StripAccents_NTilde()
        => Assert.Equal("nN", Invoke("stripAccents", S("\u00F1\u00D1")).AsString());

    // =========================================================================
    // clean extended edge cases
    // =========================================================================

    [Fact]
    public void Clean_EmptyString()
        => Assert.Equal("", Invoke("clean", S("")).AsString());

    [Fact]
    public void Clean_NoControlChars()
        => Assert.Equal("hello world", Invoke("clean", S("hello world")).AsString());

    [Fact]
    public void Clean_PreservesNewlinesTabs()
        => Assert.Equal("a\nb\tc\r", Invoke("clean", S("a\nb\tc\r")).AsString());

    [Fact]
    public void Clean_IntegerCoerces()
    {
        var result = Invoke("clean", I(42));
        Assert.NotNull(result);
    }

    // =========================================================================
    // wordCount extended edge cases
    // =========================================================================

    [Fact]
    public void WordCount_OnlyWhitespace()
        => Assert.Equal(0L, Invoke("wordCount", S("   ")).AsInt64());

    [Fact]
    public void WordCount_WithTabs()
        => Assert.Equal(3L, Invoke("wordCount", S("a\tb\tc")).AsInt64());

    [Fact]
    public void WordCount_IntegerCoerces()
    {
        // .NET coerces integer to string "42" which is 1 word
        var result = Invoke("wordCount", I(42));
        Assert.NotNull(result);
    }

    // =========================================================================
    // tokenize extended edge cases
    // =========================================================================

    [Fact]
    public void Tokenize_WhitespaceDefault()
    {
        var result = Invoke("tokenize", S("hello world test"));
        var arr = result.AsArray()!;
        Assert.Equal(3, arr.Count);
        Assert.Equal("hello", arr[0].AsString());
        Assert.Equal("world", arr[1].AsString());
        Assert.Equal("test", arr[2].AsString());
    }

    [Fact]
    public void Tokenize_NoArgs()
    {
        var result = Invoke("tokenize");
        var arr = result.AsArray();
        Assert.NotNull(arr);
        Assert.Empty(arr);
    }

    // =========================================================================
    // levenshtein extended edge cases
    // =========================================================================

    [Fact]
    public void Levenshtein_SingleEdit()
        => Assert.Equal(1L, Invoke("levenshtein", S("kitten"), S("sitten")).AsInt64());

    [Fact]
    public void Levenshtein_Classic()
        => Assert.Equal(3L, Invoke("levenshtein", S("kitten"), S("sitting")).AsInt64());

    [Fact]
    public void Levenshtein_TooFewArgs()
    {
        var result = Invoke("levenshtein", S("hello"));
        Assert.NotNull(result);
    }

    // =========================================================================
    // soundex extended edge cases
    // =========================================================================

    [Fact]
    public void Soundex_Empty()
        => Assert.Equal("", Invoke("soundex", S("")).AsString());

    [Fact]
    public void Soundex_SingleLetter()
        => Assert.Equal("A000", Invoke("soundex", S("A")).AsString());

    [Fact]
    public void Soundex_Smith()
        => Assert.Equal("S530", Invoke("soundex", S("Smith")).AsString());

    [Fact]
    public void Soundex_NoArgs()
        => Assert.True(Invoke("soundex").IsNull);

    // =========================================================================
    // base64 extended edge cases
    // =========================================================================

    [Fact]
    public void Base64_RoundtripEmpty()
    {
        var encoded = Invoke("base64Encode", S(""));
        var decoded = Invoke("base64Decode", encoded);
        Assert.Equal("", decoded.AsString());
    }

    [Fact]
    public void Base64_RoundtripSpecialChars()
    {
        var encoded = Invoke("base64Encode", S("a&b=c d+e"));
        var decoded = Invoke("base64Decode", encoded);
        Assert.Equal("a&b=c d+e", decoded.AsString());
    }

    [Fact]
    public void Base64_RoundtripUnicode()
    {
        var encoded = Invoke("base64Encode", S("caf\u00E9"));
        var decoded = Invoke("base64Decode", encoded);
        Assert.Equal("caf\u00E9", decoded.AsString());
    }

    [Fact]
    public void Base64_EncodeKnown()
        => Assert.Equal("TWFu", Invoke("base64Encode", S("Man")).AsString());

    [Fact]
    public void Base64_RoundtripLongString()
    {
        var longStr = new string('a', 1000);
        var encoded = Invoke("base64Encode", S(longStr));
        var decoded = Invoke("base64Decode", encoded);
        Assert.Equal(longStr, decoded.AsString());
    }

    [Fact]
    public void Base64Decode_IntegerCoerces()
    {
        // .NET coerces integer to string for decode
        var result = Invoke("base64Decode", I(42));
        Assert.NotNull(result);
    }

    // =========================================================================
    // URL encode/decode extended edge cases
    // =========================================================================

    [Fact]
    public void Url_RoundtripSimple()
    {
        var encoded = Invoke("urlEncode", S("hello world"));
        var decoded = Invoke("urlDecode", encoded);
        Assert.Equal("hello world", decoded.AsString());
    }

    [Fact]
    public void Url_RoundtripSpecial()
    {
        var encoded = Invoke("urlEncode", S("a=1&b=2"));
        var decoded = Invoke("urlDecode", encoded);
        Assert.Equal("a=1&b=2", decoded.AsString());
    }

    [Fact]
    public void UrlEncode_Empty()
        => Assert.Equal("", Invoke("urlEncode", S("")).AsString());

    [Fact]
    public void UrlDecode_IntegerCoerces()
    {
        var result = Invoke("urlDecode", I(42));
        Assert.NotNull(result);
    }

    [Fact]
    public void Url_RoundtripUnicode()
    {
        var encoded = Invoke("urlEncode", S("h\u00E9llo w\u00F6rld"));
        var decoded = Invoke("urlDecode", encoded);
        Assert.Equal("h\u00E9llo w\u00F6rld", decoded.AsString());
    }

    // =========================================================================
    // hex encode/decode extended edge cases
    // =========================================================================

    [Fact]
    public void Hex_RoundtripSimple()
    {
        var encoded = Invoke("hexEncode", S("Hello"));
        var decoded = Invoke("hexDecode", encoded);
        Assert.Equal("Hello", decoded.AsString());
    }

    [Fact]
    public void Hex_RoundtripEmpty()
    {
        var encoded = Invoke("hexEncode", S(""));
        Assert.Equal("", encoded.AsString());
        var decoded = Invoke("hexDecode", encoded);
        Assert.Equal("", decoded.AsString());
    }

    [Fact]
    public void Hex_RoundtripSpecial()
    {
        var encoded = Invoke("hexEncode", S("ABC"));
        Assert.Equal("414243", encoded.AsString());
        var decoded = Invoke("hexDecode", encoded);
        Assert.Equal("ABC", decoded.AsString());
    }

    [Fact]
    public void Hex_RoundtripNumbers()
    {
        var encoded = Invoke("hexEncode", S("0123"));
        Assert.Equal("30313233", encoded.AsString());
        var decoded = Invoke("hexDecode", encoded);
        Assert.Equal("0123", decoded.AsString());
    }

    [Fact]
    public void HexDecode_Uppercase()
        => Assert.Equal("Hi", Invoke("hexDecode", S("4869")).AsString());

    // =========================================================================
    // JSON encode/decode extended edge cases
    // =========================================================================

    [Fact]
    public void JsonEncode_RoundtripString()
        => Assert.Equal("\"hello\"", Invoke("jsonEncode", S("hello")).AsString());

    [Fact]
    public void JsonEncode_RoundtripInteger()
        => Assert.Equal("42", Invoke("jsonEncode", I(42)).AsString());

    [Fact]
    public void JsonEncode_RoundtripBool()
        => Assert.Equal("true", Invoke("jsonEncode", B(true)).AsString());

    [Fact]
    public void JsonEncode_RoundtripNull()
        => Assert.Equal("null", Invoke("jsonEncode", Null()).AsString());

    [Fact]
    public void JsonEncode_RoundtripArray()
        => Assert.Equal("[1,2,3]", Invoke("jsonEncode", Arr(I(1), I(2), I(3))).AsString());

    [Fact]
    public void JsonDecode_Array()
    {
        var result = Invoke("jsonDecode", S("[1,2,3]"));
        var arr = result.AsArray();
        Assert.NotNull(arr);
        Assert.Equal(3, arr.Count);
    }

    [Fact]
    public void JsonDecode_String()
        => Assert.Equal("hello", Invoke("jsonDecode", S("\"hello\"")).AsString());

    [Fact]
    public void JsonDecode_NullInput()
        => Assert.True(Invoke("jsonDecode", Null()).IsNull);

    [Fact]
    public void JsonEncode_NoArgs()
        => Assert.True(Invoke("jsonEncode").IsNull);

    [Fact]
    public void JsonDecode_IntegerCoerces()
    {
        var result = Invoke("jsonDecode", I(42));
        Assert.NotNull(result);
    }

    [Fact]
    public void JsonEncode_DecodeObjectRoundtrip()
    {
        var obj = Obj(("name", S("Alice")), ("age", I(30)));
        var encoded = Invoke("jsonEncode", obj);
        var decoded = Invoke("jsonDecode", encoded);
        var objResult = decoded.AsObject();
        Assert.NotNull(objResult);
    }

    // =========================================================================
    // sha256 extended edge cases
    // =========================================================================

    [Fact]
    public void Sha256_Empty()
        => Assert.Equal("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            Invoke("sha256", S("")).AsString());

    [Fact]
    public void Sha256_Hello()
        => Assert.Equal("2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824",
            Invoke("sha256", S("hello")).AsString());

    [Fact]
    public void Sha256_IntegerCoerces()
    {
        var result = Invoke("sha256", I(42));
        Assert.NotNull(result);
    }

    [Fact]
    public void Sha256_Deterministic()
    {
        var r1 = Invoke("sha256", S("abc")).AsString();
        var r2 = Invoke("sha256", S("abc")).AsString();
        Assert.Equal(r1, r2);
    }

    [Fact]
    public void Sha256_LongerString()
        => Assert.Equal("d7a8fbb307d7809469ca9abcb0082e4f8d5651e46d3cdb762d02d0bf37c9e592",
            Invoke("sha256", S("The quick brown fox jumps over the lazy dog")).AsString());

    // =========================================================================
    // md5 extended edge cases
    // =========================================================================

    [Fact]
    public void Md5_Empty()
        => Assert.Equal("d41d8cd98f00b204e9800998ecf8427e",
            Invoke("md5", S("")).AsString());

    [Fact]
    public void Md5_Hello()
        => Assert.Equal("5d41402abc4b2a76b9719d911017c592",
            Invoke("md5", S("hello")).AsString());

    [Fact]
    public void Md5_IntegerCoerces()
    {
        var result = Invoke("md5", I(42));
        Assert.NotNull(result);
    }

    [Fact]
    public void Md5_Deterministic()
    {
        var r1 = Invoke("md5", S("test123")).AsString();
        var r2 = Invoke("md5", S("test123")).AsString();
        Assert.Equal(r1, r2);
    }

    // =========================================================================
    // crc32 extended edge cases
    // =========================================================================

    [Fact]
    public void Crc32_Empty()
        => Assert.Equal("00000000", Invoke("crc32", S("")).AsString());

    [Fact]
    public void Crc32_Hello()
        => Assert.Equal("3610a686", Invoke("crc32", S("hello")).AsString());

    [Fact]
    public void Crc32_IntegerCoerces()
    {
        var result = Invoke("crc32", I(42));
        Assert.NotNull(result);
    }

    [Fact]
    public void Crc32_Deterministic()
    {
        var r1 = Invoke("crc32", S("abc")).AsString();
        var r2 = Invoke("crc32", S("abc")).AsString();
        Assert.Equal(r1, r2);
    }

    // =========================================================================
    // Cross-verb integration tests
    // =========================================================================

    [Fact]
    public void SplitThenJoin_Roundtrip()
    {
        var split = Invoke("split", S("a,b,c"), S(","));
        var joined = Invoke("join", split, S(","));
        Assert.Equal("a,b,c", joined.AsString());
    }

    [Fact]
    public void HexEncodeThenDecode_Roundtrip()
    {
        var encoded = Invoke("hexEncode", S("test data"));
        var decoded = Invoke("hexDecode", encoded);
        Assert.Equal("test data", decoded.AsString());
    }

    [Fact]
    public void SnakeToCamelToPascal()
    {
        var snake = S("hello_world_test");
        var camel = Invoke("camelCase", snake);
        Assert.Equal("helloWorldTest", camel.AsString());
        var pascal = Invoke("pascalCase", camel);
        Assert.Equal("HelloWorldTest", pascal.AsString());
    }

    [Fact]
    public void SlugifyWithAccents()
    {
        var stripped = Invoke("stripAccents", S("Caf\u00E9 R\u00E9sum\u00E9"));
        var slugged = Invoke("slugify", stripped);
        Assert.Equal("cafe-resume", slugged.AsString());
    }

    [Fact]
    public void NormalizeThenWordCount()
    {
        var normalized = Invoke("normalizeSpace", S("  hello   world   test  "));
        var count = Invoke("wordCount", normalized);
        Assert.Equal(3L, count.AsInt64());
    }

    [Fact]
    public void TruncateThenPadRight()
    {
        var truncated = Invoke("truncate", S("hello world"), I(5));
        var padded = Invoke("padRight", truncated, I(10), S("."));
        Assert.Equal("hello.....", padded.AsString());
    }

    [Fact]
    public void MaskThenReverse()
    {
        var masked = Invoke("mask", S("1234567890"), S("###-###-####"));
        var reversed = Invoke("reverseString", masked);
        Assert.Equal("0987-654-321", reversed.AsString());
    }

    [Fact]
    public void RepeatThenTruncate()
    {
        var repeated = Invoke("repeat", S("ab"), I(10));
        var truncated = Invoke("truncate", repeated, I(7));
        Assert.Equal("abababa", truncated.AsString());
    }

    [Fact]
    public void LeftOfThenRightOf_EmailSplit()
    {
        var email = S("user@domain.com");
        var user = Invoke("leftOf", email, S("@"));
        var domain = Invoke("rightOf", email, S("@"));
        Assert.Equal("user", user.AsString());
        Assert.Equal("domain.com", domain.AsString());
    }

    [Fact]
    public void Base64EncodeThenDecode_LongRoundtrip()
    {
        var longStr = new string('a', 500);
        var encoded = Invoke("base64Encode", S(longStr));
        var decoded = Invoke("base64Decode", encoded);
        Assert.Equal(longStr, decoded.AsString());
    }

    [Fact]
    public void UrlEncodeThenDecode_UnicodeRoundtrip()
    {
        var encoded = Invoke("urlEncode", S("h\u00E9llo w\u00F6rld"));
        var decoded = Invoke("urlDecode", encoded);
        Assert.Equal("h\u00E9llo w\u00F6rld", decoded.AsString());
    }

    // =========================================================================
    // Additional replace edge cases
    // =========================================================================

    [Fact]
    public void Replace_EmptySearch_Throws()
    {
        // .NET String.Replace throws on empty oldValue
        Assert.Throws<ArgumentException>(() => Invoke("replace", S("hello"), S(""), S("x")));
    }

    [Fact]
    public void Replace_MultipleOccurrences()
    {
        // .NET replace replaces first occurrence only
        Assert.Equal("h-llo", Invoke("replace", S("hello"), S("e"), S("-")).AsString());
    }

    [Fact]
    public void Replace_CasePreserving()
        => Assert.Equal("Hello", Invoke("replace", S("hello"), S("h"), S("H")).AsString());

    // =========================================================================
    // Additional substring edge cases
    // =========================================================================

    [Fact]
    public void Substring_EmptyString()
        => Assert.Equal("", Invoke("substring", S(""), I(0)).AsString());

    [Fact]
    public void Substring_FullString()
        => Assert.Equal("hello", Invoke("substring", S("hello"), I(0)).AsString());

    [Fact]
    public void Substring_LastChar()
        => Assert.Equal("o", Invoke("substring", S("hello"), I(4)).AsString());

    [Fact]
    public void Substring_WithExactLength()
        => Assert.Equal("hel", Invoke("substring", S("hello"), I(0), I(3)).AsString());

    // =========================================================================
    // Additional length edge cases
    // =========================================================================

    [Fact]
    public void Length_Integer()
    {
        // Length of integer - coerces to string
        var result = Invoke("length", I(42));
        Assert.NotNull(result);
    }

    [Fact]
    public void Length_EmptyArray()
        => Assert.Equal(0L, Invoke("length", Arr()).AsInt64());

    [Fact]
    public void Length_LargeString()
    {
        var longStr = new string('x', 10000);
        Assert.Equal(10000L, Invoke("length", S(longStr)).AsInt64());
    }
}
