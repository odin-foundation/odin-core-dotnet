using System.Linq;
using Odin.Core;
using Odin.Core.Transform;
using Odin.Core.Types;
using Xunit;

namespace Odin.Core.Tests.Unit;

// Conditional segment behavior: verb-expression conditions, legacy infix
// back-compat, and if/elif/else chains.
public class TransformConditionalTests
{
    private static DynValue Json(string json) => JsonSourceParser.Parse(json);

    private static TransformResult Run(string transformText, string inputJson)
    {
        var transform = Core.Odin.ParseTransform(transformText);
        return TransformEngine.Execute(transform, Json(inputJson));
    }

    private static bool HasSection(TransformResult r, string name) =>
        r.Output?.Get(name) != null;

    // ─────────────────────────────────────────────────────────────────
    // Verb-expression conditions (truthy)
    // ─────────────────────────────────────────────────────────────────

    private const string AndLtDoc = @"{$}
odin = ""1.0.0""
transform = ""1.0.0""
direction = ""json->json""

{Quote}
DriverName = @driver.name

{HighRisk :if %and @driver.hasDui %lt @driver.age ##25}
flag = ""high-risk""
";

    [Fact]
    public void VerbCondition_IncludesSection_WhenTrue()
    {
        var r = Run(AndLtDoc, @"{""driver"":{""name"":""Pat"",""hasDui"":true,""age"":22}}");
        Assert.True(r.Success);
        Assert.True(HasSection(r, "HighRisk"));
    }

    [Fact]
    public void VerbCondition_OmitsSection_WhenFalse()
    {
        var r = Run(AndLtDoc, @"{""driver"":{""name"":""Sam"",""hasDui"":true,""age"":40}}");
        Assert.True(r.Success);
        Assert.True(HasSection(r, "Quote"));
        Assert.False(HasSection(r, "HighRisk"));
    }

    [Fact]
    public void VerbCondition_Or_Truthy()
    {
        const string doc = @"{$}
direction = ""json->json""

{Flagged :if %or @a @b}
v = ""yes""
";
        Assert.True(HasSection(Run(doc, @"{""a"":false,""b"":true}"), "Flagged"));
        Assert.False(HasSection(Run(doc, @"{""a"":false,""b"":false}"), "Flagged"));
    }

    [Fact]
    public void VerbCondition_Not_Truthy()
    {
        const string doc = @"{$}
direction = ""json->json""

{Clear :if %not @flagged}
v = ""ok""
";
        Assert.True(HasSection(Run(doc, @"{""flagged"":false}"), "Clear"));
        Assert.False(HasSection(Run(doc, @"{""flagged"":true}"), "Clear"));
    }

    [Fact]
    public void VerbCondition_Eq_BodyForm()
    {
        const string doc = @"{$}
direction = ""json->json""

{Dui}
_if = %eq @driver.state ""TX""
state = @driver.state
";
        Assert.True(HasSection(Run(doc, @"{""driver"":{""state"":""TX""}}"), "Dui"));
        Assert.False(HasSection(Run(doc, @"{""driver"":{""state"":""CA""}}"), "Dui"));
    }

    // ─────────────────────────────────────────────────────────────────
    // Legacy quoted-infix back-compat
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void LegacyInfixCondition_StillWorks()
    {
        const string doc = @"{$}
direction = ""json->json""

{Dui}
_if = ""@driver.has_dui = true""
state = @driver.state
";
        Assert.True(HasSection(Run(doc, @"{""driver"":{""has_dui"":true,""state"":""TX""}}"), "Dui"));
        Assert.False(HasSection(Run(doc, @"{""driver"":{""has_dui"":false,""state"":""TX""}}"), "Dui"));
    }

    [Fact]
    public void LegacyInfixCondition_NumericComparison()
    {
        const string doc = @"{$}
direction = ""json->json""

{premium}
_if = ""@amount > 100""
value = @amount
";
        Assert.True(HasSection(Run(doc, @"{""amount"":150}"), "premium"));
        Assert.False(HasSection(Run(doc, @"{""amount"":50}"), "premium"));
    }

    // ─────────────────────────────────────────────────────────────────
    // elif/else chains
    // ─────────────────────────────────────────────────────────────────

    private const string ChainDoc = @"{$}
odin = ""1.0.0""
transform = ""1.0.0""
direction = ""json->json""

{HighRisk :if %eq @driver.tier ""dui""}
band = ""high-risk""

{YoungDriver :elif %lt @driver.age ##25}
band = ""young-driver""

{Standard :else}
band = ""standard""
";

    private static string[] Bands(string inputJson)
    {
        var r = Run(ChainDoc, inputJson);
        Assert.True(r.Success);
        var obj = r.Output!.AsObject();
        return obj!.Select(kv => kv.Key).ToArray();
    }

    [Fact]
    public void Chain_TakesIfBranch_SkipsRest()
    {
        Assert.Equal(new[] { "HighRisk" }, Bands(@"{""driver"":{""tier"":""dui"",""age"":30}}"));
    }

    [Fact]
    public void Chain_FallsThroughToElif()
    {
        Assert.Equal(new[] { "YoungDriver" }, Bands(@"{""driver"":{""tier"":""std"",""age"":20}}"));
    }

    [Fact]
    public void Chain_FallsThroughToElse()
    {
        Assert.Equal(new[] { "Standard" }, Bands(@"{""driver"":{""tier"":""std"",""age"":40}}"));
    }

    [Fact]
    public void Chain_IfOnly_OmittedWhenFalse()
    {
        const string doc = @"{$}
direction = ""json->json""

{Only :if %eq @x ""y""}
v = ""1""
";
        Assert.False(HasSection(Run(doc, @"{""x"":""z""}"), "Only"));
        Assert.True(HasSection(Run(doc, @"{""x"":""y""}"), "Only"));
    }

    [Fact]
    public void OrphanElif_RaisesT012()
    {
        const string doc = @"{$}
direction = ""json->json""

{A}
x = ""1""

{B :elif %eq @y ""z""}
v = ""2""
";
        var r = Run(doc, @"{""y"":""q""}");
        Assert.False(r.Success);
        Assert.Contains(r.Errors, e => e.Code == "T012");
    }

    [Fact]
    public void OrphanElse_RaisesT012()
    {
        const string doc = @"{$}
direction = ""json->json""

{A}
x = ""1""

{B :else}
v = ""2""
";
        var r = Run(doc, @"{}");
        Assert.False(r.Success);
        Assert.Contains(r.Errors, e => e.Code == "T012");
    }
}
