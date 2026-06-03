using System.Collections.Generic;
using Odin.Core.Types;
using Odin.Core.Transform;
using Odin.Core.Transform.Verbs;
using Xunit;

namespace Odin.Core.Tests.Unit;

/// <summary>
/// Tests for HTML/XML markup and template string verbs (escapeHtml, unescapeHtml,
/// escapeXml, stripTags, template).
/// </summary>
public class StringMarkupVerbTests
{
    private readonly VerbRegistry _registry = new VerbRegistry();
    private readonly VerbContext _ctx = new VerbContext();

    private DynValue Invoke(string verb, params DynValue[] args)
        => _registry.Invoke(verb, args, _ctx);

    private static DynValue S(string v) => DynValue.String(v);
    private static DynValue I(long v) => DynValue.Integer(v);
    private static DynValue Null() => DynValue.Null();
    private static DynValue Obj(params (string key, DynValue value)[] pairs)
    {
        var list = new List<KeyValuePair<string, DynValue>>();
        foreach (var (k, v) in pairs) list.Add(new KeyValuePair<string, DynValue>(k, v));
        return DynValue.Object(list);
    }

    // =========================================================================
    // escapeHtml
    // =========================================================================

    [Fact]
    public void EscapeHtml_Basic()
        => Assert.Equal("&lt;p&gt;1 &amp; 2&lt;/p&gt;", Invoke("escapeHtml", S("<p>1 & 2</p>")).AsString());

    [Fact]
    public void EscapeHtml_Apostrophe()
        => Assert.Equal("it&#39;s", Invoke("escapeHtml", S("it's")).AsString());

    [Fact]
    public void EscapeHtml_Quote()
        => Assert.Equal("&quot;x&quot;", Invoke("escapeHtml", S("\"x\"")).AsString());

    [Fact]
    public void EscapeHtml_Empty()
        => Assert.Equal("", Invoke("escapeHtml", S("")).AsString());

    [Fact]
    public void EscapeHtml_NoArgs()
        => Assert.True(Invoke("escapeHtml").IsNull);

    // =========================================================================
    // escapeXml
    // =========================================================================

    [Fact]
    public void EscapeXml_ApostropheAsApos()
        => Assert.Equal("x = &apos;a&apos; &amp; b", Invoke("escapeXml", S("x = 'a' & b")).AsString());

    [Fact]
    public void EscapeXml_Angles()
        => Assert.Equal("&lt;a href=&quot;u&quot;&gt;", Invoke("escapeXml", S("<a href=\"u\">")).AsString());

    [Fact]
    public void EscapeXml_NoSpecials()
        => Assert.Equal("no specials", Invoke("escapeXml", S("no specials")).AsString());

    [Fact]
    public void EscapeXml_NoArgs()
        => Assert.True(Invoke("escapeXml").IsNull);

    // =========================================================================
    // unescapeHtml
    // =========================================================================

    [Fact]
    public void UnescapeHtml_NamedEntities()
        => Assert.Equal("<p>1 & 2</p>", Invoke("unescapeHtml", S("&lt;p&gt;1 &amp; 2&lt;/p&gt;")).AsString());

    [Fact]
    public void UnescapeHtml_NumericAndHexRefs()
        => Assert.Equal("AB", Invoke("unescapeHtml", S("&#65;&#x42;")).AsString());

    [Fact]
    public void UnescapeHtml_RoundTripsEscapeHtml()
    {
        var escaped = Invoke("escapeHtml", S("<b>'a' & \"b\"</b>"));
        Assert.Equal("<b>'a' & \"b\"</b>", Invoke("unescapeHtml", escaped).AsString());
    }

    [Fact]
    public void UnescapeHtml_NoArgs()
        => Assert.True(Invoke("unescapeHtml").IsNull);

    // =========================================================================
    // stripTags
    // =========================================================================

    [Fact]
    public void StripTags_RemovesTags()
        => Assert.Equal("Hello world", Invoke("stripTags", S("<p>Hello <b>world</b></p>")).AsString());

    [Fact]
    public void StripTags_NoTags()
        => Assert.Equal("no tags here", Invoke("stripTags", S("no tags here")).AsString());

    [Fact]
    public void StripTags_NoArgs()
        => Assert.True(Invoke("stripTags").IsNull);

    // =========================================================================
    // template
    // =========================================================================

    [Fact]
    public void Template_FillsPlaceholders()
        => Assert.Equal("Hi Ada, you are 36",
            Invoke("template", S("Hi {name}, you are {age}"), Obj(("name", S("Ada")), ("age", I(36)))).AsString());

    [Fact]
    public void Template_MissingKeyIsEmpty()
        => Assert.Equal("ab", Invoke("template", S("a{missing}b"), Obj(("name", S("Ada")))).AsString());

    [Fact]
    public void Template_TrimsBraceWhitespace()
        => Assert.Equal("Ada", Invoke("template", S("{ name }"), Obj(("name", S("Ada")))).AsString());

    [Fact]
    public void Template_TooFewArgs()
        => Assert.True(Invoke("template", S("{name}")).IsNull);
}
