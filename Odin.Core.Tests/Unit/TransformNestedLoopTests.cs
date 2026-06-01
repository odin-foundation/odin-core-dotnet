using System.Collections.Generic;
using Odin.Core;
using Odin.Core.Transform;
using Odin.Core.Types;
using Xunit;

namespace Odin.Core.Tests.Unit;

// Nested :loop directives iterate a cross-product, binding each :as alias and
// resolving relative inner paths against the enclosing item.
public class TransformNestedLoopTests
{
    private static DynValue Json(string json) => JsonSourceParser.Parse(json);

    private static TransformResult Run(string transformText, string inputJson)
    {
        var transform = Core.Odin.ParseTransform(transformText);
        return TransformEngine.Execute(transform, Json(inputJson));
    }

    private static List<DynValue> Rows(TransformResult r)
    {
        var rows = r.Output!.Get("rows");
        Assert.NotNull(rows);
        var arr = rows!.AsArray();
        Assert.NotNull(arr);
        return arr!;
    }

    private const string TwoLevel = @"{$}
odin = ""1.0.0""
transform = ""1.0.0""
direction = ""json->json""
target.format = ""json""

{rows[]}
:loop vehicles :as veh
:loop .coverages :as cov
vin = ""@veh.vin""
code = ""@cov.code""
";

    private const string ThreeLevel = @"{$}
odin = ""1.0.0""
transform = ""1.0.0""
direction = ""json->json""
target.format = ""json""

{rows[]}
:loop regions :as r
:loop .stores :as s
:loop .items :as i
region = ""@r.name""
store = ""@s.id""
sku = ""@i.sku""
";

    private const string CounterDoc = @"{$}
odin = ""1.0.0""
transform = ""1.0.0""
direction = ""json->json""
target.format = ""json""

{rows[]}
:loop vehicles :as veh
:loop .coverages :as cov
:counter idx
vin = ""@veh.vin""
code = ""@cov.code""
cov_index = ""@idx""
";

    // ─────────────────────────────────────────────────────────────────
    // Happy path
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void TwoLevel_ProducesCrossProduct()
    {
        var r = Run(TwoLevel,
            @"{""vehicles"":[{""vin"":""A"",""coverages"":[{""code"":""L""},{""code"":""C""}]},{""vin"":""B"",""coverages"":[{""code"":""M""}]}]}");
        Assert.True(r.Success);
        var rows = Rows(r);
        Assert.Equal(3, rows.Count);
        Assert.Equal("A", rows[0].Get("vin")!.AsString());
        Assert.Equal("L", rows[0].Get("code")!.AsString());
        Assert.Equal("A", rows[1].Get("vin")!.AsString());
        Assert.Equal("C", rows[1].Get("code")!.AsString());
        Assert.Equal("B", rows[2].Get("vin")!.AsString());
        Assert.Equal("M", rows[2].Get("code")!.AsString());
    }

    [Fact]
    public void ThreeLevel_ProducesCrossProduct()
    {
        var r = Run(ThreeLevel,
            @"{""regions"":[{""name"":""West"",""stores"":[{""id"":""S1"",""items"":[{""sku"":""X""},{""sku"":""Y""}]}]},{""name"":""East"",""stores"":[{""id"":""S2"",""items"":[{""sku"":""Z""}]}]}]}");
        Assert.True(r.Success);
        var rows = Rows(r);
        Assert.Equal(3, rows.Count);
        Assert.Equal("West", rows[0].Get("region")!.AsString());
        Assert.Equal("S1", rows[0].Get("store")!.AsString());
        Assert.Equal("X", rows[0].Get("sku")!.AsString());
        Assert.Equal("Y", rows[1].Get("sku")!.AsString());
        Assert.Equal("East", rows[2].Get("region")!.AsString());
        Assert.Equal("Z", rows[2].Get("sku")!.AsString());
    }

    [Fact]
    public void Counter_BindsInnermostIndex_ResetsPerOuter()
    {
        var r = Run(CounterDoc,
            @"{""vehicles"":[{""vin"":""V1"",""coverages"":[{""code"":""A""},{""code"":""B""}]},{""vin"":""V2"",""coverages"":[{""code"":""C""}]}]}");
        Assert.True(r.Success);
        var rows = Rows(r);
        Assert.Equal(3, rows.Count);
        Assert.Equal(0L, rows[0].Get("cov_index")!.AsInt64());
        Assert.Equal(1L, rows[1].Get("cov_index")!.AsInt64());
        Assert.Equal(0L, rows[2].Get("cov_index")!.AsInt64()); // reset for V2
    }

    // ─────────────────────────────────────────────────────────────────
    // Edge cases
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void EmptyInner_OuterWithNoInnerArray_YieldsNoRows()
    {
        var r = Run(TwoLevel,
            @"{""vehicles"":[{""vin"":""V1"",""coverages"":[{""code"":""A""}]},{""vin"":""V2""},{""vin"":""V3"",""coverages"":[{""code"":""C""}]}]}");
        Assert.True(r.Success);
        var rows = Rows(r);
        Assert.Equal(2, rows.Count);
        Assert.Equal("V1", rows[0].Get("vin")!.AsString());
        Assert.Equal("V3", rows[1].Get("vin")!.AsString());
    }

    [Fact]
    public void OuterNonArray_YieldsEmptyRows_NoError()
    {
        var r = Run(TwoLevel, @"{""vehicles"":""not-an-array""}");
        Assert.True(r.Success);
        Assert.Empty(Rows(r));
    }

    [Fact]
    public void InnerNonArray_YieldsEmptyRows_NoError()
    {
        var r = Run(TwoLevel,
            @"{""vehicles"":[{""vin"":""A"",""coverages"":""nope""}]}");
        Assert.True(r.Success);
        Assert.Empty(Rows(r));
    }

    // ─────────────────────────────────────────────────────────────────
    // Error
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void MissingOuterSource_YieldsEmptyRows_NoError()
    {
        var r = Run(TwoLevel, @"{}");
        Assert.True(r.Success);
        Assert.Empty(Rows(r));
        Assert.Empty(r.Errors);
    }
}
