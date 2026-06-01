using System.Linq;
using Odin.Core;
using Odin.Core.Transform;
using Odin.Core.Types;
using Xunit;

namespace Odin.Core.Tests.Unit;

// :literal segments emit their """…""" body verbatim with ${…} interpolation.
public class TransformLiteralBlockTests
{
    private static DynValue Json(string json) => JsonSourceParser.Parse(json);

    private static TransformResult Run(string transformText, string inputJson)
    {
        var transform = Core.Odin.ParseTransform(transformText);
        return TransformEngine.Execute(transform, Json(inputJson));
    }

    private const string Header = @"{$}
odin = ""1.0.0""
transform = ""1.0.0""
direction = ""json->fixed-width""
target.format = ""fixed-width""
";

    // ─────────────────────────────────────────────────────────────────
    // Happy path
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void LiteralBlock_PathInterpolation()
    {
        var doc = Header + "\n{HDR}\n:literal\n\"\"\"\nHDR|${@policy.number}\n\"\"\"\n";
        var r = Run(doc, @"{""policy"":{""number"":""P-100""}}");
        Assert.True(r.Success);
        Assert.Equal("HDR|P-100", r.Formatted!.TrimEnd());
    }

    [Fact]
    public void LiteralBlock_VerbInterpolation()
    {
        var doc = Header + "\n{HDR}\n:literal\n\"\"\"\nHDR|${%upper @policy.code}\n\"\"\"\n";
        var r = Run(doc, @"{""policy"":{""code"":""abc""}}");
        Assert.True(r.Success);
        Assert.Equal("HDR|ABC", r.Formatted!.TrimEnd());
    }

    [Fact]
    public void LiteralBlock_Loop_RendersOncePerItem()
    {
        var doc = Header + "\n{DET[]}\n:loop @items\n:literal\n\"\"\"\nDET|${@.sku}|${@.qty}\n\"\"\"\n";
        var r = Run(doc, @"{""items"":[{""sku"":""A1"",""qty"":""2""},{""sku"":""B2"",""qty"":""5""}]}");
        Assert.True(r.Success);
        var lines = r.Formatted!.TrimEnd().Replace("\r\n", "\n").Split('\n');
        Assert.Equal(2, lines.Length);
        Assert.Equal("DET|A1|2", lines[0]);
        Assert.Equal("DET|B2|5", lines[1]);
    }

    // ─────────────────────────────────────────────────────────────────
    // Edge cases — escapes
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void LiteralBlock_Escapes_EmitLiteralMarkers()
    {
        // \${ -> ${ (no interpolation), \$ -> $, then a real ${...}.
        var doc = Header + "\n{NOTE}\n:literal\n\"\"\"\nliteral:\\${@policy.number} dollar:\\$ value:${@policy.number}\n\"\"\"\n";
        var r = Run(doc, @"{""policy"":{""number"":""P-100""}}");
        Assert.True(r.Success);
        Assert.Equal("literal:${@policy.number} dollar:$ value:P-100", r.Formatted!.TrimEnd());
    }

    [Fact]
    public void LiteralBlock_InteriorBlankLine_Preserved()
    {
        var doc = Header + "\n{HDR}\n:literal\n\"\"\"\nfirst\n\nlast\n\"\"\"\n";
        var r = Run(doc, @"{}");
        Assert.True(r.Success);
        var lines = r.Formatted!.Replace("\r\n", "\n").Split('\n');
        Assert.Equal("first", lines[0]);
        Assert.Equal("", lines[1]);
        Assert.Equal("last", lines[2]);
    }

    // ─────────────────────────────────────────────────────────────────
    // Error — nested interpolation
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void LiteralBlock_NestedInterpolation_RaisesT014()
    {
        var doc = Header + "\n{HDR}\n:literal\n\"\"\"\nHDR|${@a ${@b}}\n\"\"\"\n";
        var r = Run(doc, @"{""a"":""x"",""b"":""y""}");
        Assert.Contains(r.Errors, e => e.Code == "T014");
    }
}
