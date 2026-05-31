using System.Collections.Generic;
using Odin.Core;
using Odin.Core.Types;
using Xunit;

namespace Odin.Core.Tests.Unit;

// Tests for transform XML formatter features: namespaces (:ns) and emitTypeHints.
public class XmlFormatterFeaturesTests
{
    // ─────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────

    private static DynValue Obj(params (string k, DynValue v)[] entries)
    {
        var list = new List<KeyValuePair<string, DynValue>>();
        foreach (var (k, v) in entries)
            list.Add(new KeyValuePair<string, DynValue>(k, v));
        return DynValue.Object(list);
    }

    private static string Run(string transform, DynValue source)
    {
        var r = Core.Odin.ExecuteTransform(transform, source);
        Assert.True(r.Success, "transform failed: " + string.Join("; ", r.Errors));
        Assert.NotNull(r.Formatted);
        return r.Formatted!;
    }

    // ─────────────────────────────────────────────────────────────────
    // 1. emitTypeHints default (true)
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void EmitTypeHintsDefault_EmitsTypeAndOdinNamespace()
    {
        var transform = @"
{$}
odin = ""1.0.0""
transform = ""1.0.0""
direction = ""odin->xml""
target.format = ""xml""

{Record}
Count = ""@.count""
Price = ""@.price""
";
        var source = Obj(
            ("count", DynValue.Integer(42)),
            ("price", DynValue.Currency(9.99, 2, "USD")));
        var xml = Run(transform, source);

        Assert.Contains("odin:type=\"integer\"", xml);
        Assert.Contains("xmlns:odin", xml);
        // The transform XmlFormatter emits a type attribute for the currency value.
        Assert.Contains("odin:type", xml);
    }

    // A currency value renders odin:type="currency" plus odin:currencyCode.
    [Fact]
    public void EmitTypeHintsDefault_EmitsCurrencyTypeAndCurrencyCode()
    {
        var transform = @"
{$}
odin = ""1.0.0""
transform = ""1.0.0""
direction = ""odin->xml""
target.format = ""xml""

{Record}
Price = ""@.total""
";
        var doc = Core.Odin.Parse("total = #$9.99:USD");
        var r = Core.Odin.TransformDocument(transform, doc);
        Assert.True(r.Success, "transform failed: " + string.Join("; ", r.Errors));
        Assert.NotNull(r.Formatted);
        var xml = r.Formatted!;

        Assert.Contains("odin:type=\"currency\"", xml);
        Assert.Contains("odin:currencyCode=\"USD\"", xml);
        Assert.Contains(">9.99<", xml);
    }

    // A code-less currency renders odin:type="currency" with preserved decimals and no currencyCode.
    [Fact]
    public void EmitTypeHintsDefault_CodelessCurrency_RendersCurrencyWithDecimals()
    {
        var transform = @"
{$}
odin = ""1.0.0""
transform = ""1.0.0""
direction = ""odin->xml""
target.format = ""xml""

{Record}
Price = ""@.total""
";
        var doc = Core.Odin.Parse("total = #$50.00");
        var r = Core.Odin.TransformDocument(transform, doc);
        Assert.True(r.Success, "transform failed: " + string.Join("; ", r.Errors));
        Assert.NotNull(r.Formatted);
        var xml = r.Formatted!;

        Assert.Contains("odin:type=\"currency\"", xml);
        Assert.Contains(">50.00<", xml);
        Assert.DoesNotContain("odin:currencyCode", xml);
    }

    // ─────────────────────────────────────────────────────────────────
    // 2. emitTypeHints = false
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void EmitTypeHintsFalse_SuppressesOdinAttributes()
    {
        var transform = @"
{$}
odin = ""1.0.0""
transform = ""1.0.0""
direction = ""odin->xml""
target.format = ""xml""
target.emitTypeHints = ?false

{Record}
Count = ""@.count""
Price = ""@.price""
";
        var source = Obj(
            ("count", DynValue.Integer(42)),
            ("price", DynValue.Currency(9.99, 2, "USD")));
        var xml = Run(transform, source);

        Assert.DoesNotContain("odin:type", xml);
        Assert.DoesNotContain("odin:currencyCode", xml);
        Assert.DoesNotContain("xmlns:odin", xml);
        Assert.DoesNotContain("odin:", xml);
        // Values still present
        Assert.Contains("42", xml);
        Assert.Contains("9.99", xml);
    }

    // ─────────────────────────────────────────────────────────────────
    // 3. :ns prefixing + root xmlns
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void NamespacePrefix_QualifiesElementAndDeclaresRootXmlns()
    {
        var transform = @"
{$}
odin = ""1.0.0""
transform = ""1.0.0""
direction = ""odin->xml""
target.format = ""xml""

{$target.namespace}
p = ""urn:x""

{Doc}
Qualified = @.a :ns p
Plain = @.b
";
        var source = Obj(
            ("a", DynValue.String("one")),
            ("b", DynValue.String("two")));
        var xml = Run(transform, source);

        Assert.Contains("xmlns:p=\"urn:x\"", xml);
        Assert.Contains("<p:Qualified>", xml);
        Assert.Contains("<Plain>", xml);
        Assert.DoesNotContain("<p:Plain>", xml);
    }

    // ─────────────────────────────────────────────────────────────────
    // 4. multiple namespaces
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void MultipleNamespaces_AllDeclaredOnRoot()
    {
        var transform = @"
{$}
odin = ""1.0.0""
transform = ""1.0.0""
direction = ""odin->xml""
target.format = ""xml""

{$target.namespace}
a = ""urn:aaa""
b = ""urn:bbb""

{Doc}
First = @.x :ns a
Second = @.y :ns b
";
        var source = Obj(
            ("x", DynValue.String("1")),
            ("y", DynValue.String("2")));
        var xml = Run(transform, source);

        Assert.Contains("xmlns:a=\"urn:aaa\"", xml);
        Assert.Contains("xmlns:b=\"urn:bbb\"", xml);
        Assert.Contains("<a:First>", xml);
        Assert.Contains("<b:Second>", xml);
    }

    // ─────────────────────────────────────────────────────────────────
    // 5. namespace + emitTypeHints=false together
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void NamespaceWithEmitTypeHintsFalse_PrefixedButNoOdinAttributes()
    {
        var transform = @"
{$}
odin = ""1.0.0""
transform = ""1.0.0""
direction = ""odin->xml""
target.format = ""xml""
target.emitTypeHints = ?false

{$target.namespace}
p = ""urn:x""

{Doc}
Amount = @.amount :ns p
";
        var source = Obj(
            ("amount", DynValue.Currency(9.99, 2, "USD")));
        var xml = Run(transform, source);

        Assert.Contains("xmlns:p=\"urn:x\"", xml);
        Assert.Contains("<p:Amount", xml);
        Assert.DoesNotContain("odin:", xml);
        Assert.Contains("9.99", xml);
    }
}
