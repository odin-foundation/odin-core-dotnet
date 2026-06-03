using System.Collections.Generic;
using Odin.Core.Types;
using Odin.Core.Transform;
using Odin.Core.Transform.Verbs;
using Xunit;

namespace Odin.Core.Tests.Unit;

/// <summary>
/// Tests for web-safe encoding, keyed hashing, URL/query parsing, and canonical
/// serialization verbs (base64url, hmac, parseUrl, buildUrl, parseQuery,
/// buildQuery, stableStringify, canonicalHash).
/// </summary>
public class EncodingVerbTests
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

    // =========================================================================
    // base64urlEncode / base64urlDecode
    // =========================================================================

    [Fact]
    public void Base64UrlEncode_UrlSafeNoPadding()
        => Assert.Equal("aGVsbG8gd29ybGQ_Pj4", Invoke("base64urlEncode", S("hello world?>>")).AsString());

    [Fact]
    public void Base64UrlDecode_Basic()
        => Assert.Equal("hello world?>>", Invoke("base64urlDecode", S("aGVsbG8gd29ybGQ_Pj4")).AsString());

    [Fact]
    public void Base64UrlDecode_ToleratesStandardPadding()
        => Assert.Equal("Hello", Invoke("base64urlDecode", S("SGVsbG8=")).AsString());

    [Fact]
    public void Base64UrlDecode_Empty()
        => Assert.Equal("", Invoke("base64urlDecode", S("")).AsString());

    [Fact]
    public void Base64UrlEncode_Null()
        => Assert.True(Invoke("base64urlEncode", Null()).IsNull);

    [Fact]
    public void Base64UrlDecode_Null()
        => Assert.True(Invoke("base64urlDecode", Null()).IsNull);

    [Fact]
    public void Base64Url_Roundtrip()
    {
        var enc = Invoke("base64urlEncode", S("data/with+special?>"));
        Assert.Equal("data/with+special?>", Invoke("base64urlDecode", enc).AsString());
    }

    // =========================================================================
    // hmac
    // =========================================================================

    [Fact]
    public void Hmac_DefaultSha256_LowercaseHex()
        => Assert.Equal("8b5f48702995c1598c573db1e21866a9b825d4a794d169d7060a03605796360b",
            Invoke("hmac", S("message"), S("secret")).AsString());

    [Fact]
    public void Hmac_Sha1()
        => Assert.Equal("0caf649feee4953d87bf903ac1176c45e028df16",
            Invoke("hmac", S("message"), S("secret"), S("sha1")).AsString());

    [Fact]
    public void Hmac_Deterministic()
        => Assert.Equal(Invoke("hmac", S("m"), S("k")).AsString(),
            Invoke("hmac", S("m"), S("k")).AsString());

    [Fact]
    public void Hmac_MissingKeyIsNull()
        => Assert.True(Invoke("hmac", S("message")).IsNull);

    [Fact]
    public void Hmac_UnknownAlgorithmIsNull()
        => Assert.True(Invoke("hmac", S("message"), S("secret"), S("nope")).IsNull);

    // =========================================================================
    // parseUrl
    // =========================================================================

    [Fact]
    public void ParseUrl_SplitsParts_SortedQuery()
    {
        var result = Invoke("parseUrl", S("https://example.com:8080/a/b?z=1&a=2#frag"));
        Assert.Equal("https", result.Get("scheme")!.AsString());
        Assert.Equal("example.com", result.Get("host")!.AsString());
        Assert.Equal(8080, result.Get("port")!.AsInt64());
        Assert.Equal("/a/b", result.Get("path")!.AsString());
        Assert.Equal("frag", result.Get("fragment")!.AsString());
        var query = result.Get("query")!.AsObject()!;
        Assert.Equal("a", query[0].Key);
        Assert.Equal("2", query[0].Value.AsString());
        Assert.Equal("z", query[1].Key);
    }

    [Fact]
    public void ParseUrl_NoPortIsNull()
    {
        var result = Invoke("parseUrl", S("https://example.com/x"));
        Assert.True(result.Get("port")!.IsNull);
        Assert.Equal("/x", result.Get("path")!.AsString());
        Assert.Equal("", result.Get("fragment")!.AsString());
    }

    [Fact]
    public void ParseUrl_InvalidIsNull()
        => Assert.True(Invoke("parseUrl", S("not a url")).IsNull);

    [Fact]
    public void ParseUrl_NullInput()
        => Assert.True(Invoke("parseUrl", Null()).IsNull);

    // =========================================================================
    // buildUrl
    // =========================================================================

    [Fact]
    public void BuildUrl_AssemblesWithSortedQuery()
    {
        var parts = Obj(
            ("scheme", S("https")),
            ("host", S("example.com")),
            ("port", I(8080)),
            ("path", S("/a/b")),
            ("query", Obj(("z", I(1)), ("a", I(2)))),
            ("fragment", S("frag")));
        Assert.Equal("https://example.com:8080/a/b?a=2&z=1#frag",
            Invoke("buildUrl", parts).AsString());
    }

    [Fact]
    public void BuildUrl_MissingSchemeIsNull()
        => Assert.True(Invoke("buildUrl", Obj(("host", S("example.com")))).IsNull);

    [Fact]
    public void BuildUrl_NonObjectIsNull()
        => Assert.True(Invoke("buildUrl", S("x")).IsNull);

    [Fact]
    public void BuildUrl_RoundTripsParseUrl()
    {
        var parsed = Invoke("parseUrl", S("https://example.com:8080/a/b?a=2&z=1#frag"));
        Assert.Equal("https://example.com:8080/a/b?a=2&z=1#frag",
            Invoke("buildUrl", parsed).AsString());
    }

    // =========================================================================
    // parseQuery
    // =========================================================================

    [Fact]
    public void ParseQuery_SortedKeys()
    {
        var result = Invoke("parseQuery", S("z=1&a=2"));
        var obj = result.AsObject()!;
        Assert.Equal("a", obj[0].Key);
        Assert.Equal("2", obj[0].Value.AsString());
        Assert.Equal("z", obj[1].Key);
    }

    [Fact]
    public void ParseQuery_LeadingQuestionMark()
    {
        var result = Invoke("parseQuery", S("?a=2"));
        Assert.Equal("2", result.Get("a")!.AsString());
    }

    [Fact]
    public void ParseQuery_NullInput()
        => Assert.True(Invoke("parseQuery", Null()).IsNull);

    // =========================================================================
    // buildQuery
    // =========================================================================

    [Fact]
    public void BuildQuery_SortedKeys()
        => Assert.Equal("a=2&z=1", Invoke("buildQuery", Obj(("z", I(1)), ("a", I(2)))).AsString());

    [Fact]
    public void BuildQuery_SkipsNullValues()
        => Assert.Equal("a=1", Invoke("buildQuery", Obj(("a", I(1)), ("b", Null()))).AsString());

    [Fact]
    public void BuildQuery_NonObjectIsNull()
        => Assert.True(Invoke("buildQuery", S("x")).IsNull);

    // =========================================================================
    // stableStringify
    // =========================================================================

    [Fact]
    public void StableStringify_SortsKeysRecursively()
    {
        var doc = Obj(
            ("b", I(2)),
            ("a", I(1)),
            ("nested", Obj(("y", I(2)), ("x", I(1)))));
        Assert.Equal("{\"a\":1,\"b\":2,\"nested\":{\"x\":1,\"y\":2}}",
            Invoke("stableStringify", doc).AsString());
    }

    [Fact]
    public void StableStringify_Array()
        => Assert.Equal("[3,1,2]", Invoke("stableStringify", Arr(I(3), I(1), I(2))).AsString());

    [Fact]
    public void StableStringify_Scalar()
        => Assert.Equal("42", Invoke("stableStringify", I(42)).AsString());

    [Fact]
    public void StableStringify_NoArgs()
        => Assert.True(Invoke("stableStringify").IsNull);

    // =========================================================================
    // canonicalHash
    // =========================================================================

    [Fact]
    public void CanonicalHash_OrderIndependent()
    {
        var a = Obj(("b", I(2)), ("a", I(1)));
        var b = Obj(("a", I(1)), ("b", I(2)));
        Assert.Equal(Invoke("canonicalHash", a).AsString(), Invoke("canonicalHash", b).AsString());
    }

    [Fact]
    public void CanonicalHash_ExactDigest()
        => Assert.Equal("43258cff783fe7036d8a43033f830adfc60ec037382473548ac742b888292777",
            Invoke("canonicalHash", Obj(("b", I(2)), ("a", I(1)))).AsString());

    [Fact]
    public void CanonicalHash_NoArgs()
        => Assert.True(Invoke("canonicalHash").IsNull);
}
