using System.Linq;
using Odin.Core;
using Odin.Core.Parsing;
using Odin.Core.Transform;
using Odin.Core.Types;
using Xunit;

namespace Odin.Core.Tests.Unit;

// Wave-3 transform conformance: bare/header-inline segment directives, field
// modifiers (:validate/:enum/:range, :object/:raw/:array, :if compare), counters,
// computation sinks, XML :cdata, and fixed-width lineWidth padding.
public class TransformModifierConformanceTests
{
    private static DynValue Json(string json) => JsonSourceParser.Parse(json);

    private static TransformResult RunJson(string transformText, string inputJson)
    {
        var transform = Core.Odin.ParseTransform(transformText);
        return TransformEngine.Execute(transform, Json(inputJson));
    }

    private static TransformResult RunOdin(string transformText, string inputOdin)
    {
        var transform = Core.Odin.ParseTransform(transformText);
        var doc = Core.Odin.Parse(inputOdin);
        return TransformEngine.ExecuteDocument(transform, doc);
    }

    // ─────────────────────────────────────────────────────────────────
    // Parser: bare and header-inline segment directives
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void BareDirectiveLine_ParsesAsSyntheticAssignment()
    {
        const string text = @"{$}
odin = ""1.0.0""

{rows[]}
:loop items
:counter idx
sku = ""@.sku""
";
        var doc = OdinParser.Parse(text);
        Assert.True(doc.Assignments.ContainsKey("rows[]._loop"));
        Assert.True(doc.Assignments.ContainsKey("rows[]._counter"));
        Assert.Equal("items", ((OdinString)doc.Assignments["rows[]._loop"]).Value);
        Assert.Equal("idx", ((OdinString)doc.Assignments["rows[]._counter"]).Value);
    }

    [Fact]
    public void BareDirectiveLine_LoopAsAlias_CapturesFullOperand()
    {
        const string text = @"{$}
odin = ""1.0.0""

{rows[]}
:loop vehicles :as v
";
        var doc = OdinParser.Parse(text);
        Assert.Equal("vehicles :as v", ((OdinString)doc.Assignments["rows[]._loop"]).Value);
    }

    [Fact]
    public void HeaderInlineLoop_CapturesToBrace()
    {
        const string text = @"{$}
odin = ""1.0.0""

{rows[] :loop items}
sku = ""@.sku""
";
        var doc = OdinParser.Parse(text);
        Assert.True(doc.Assignments.ContainsKey("rows[]._loop"));
        Assert.Equal("items", ((OdinString)doc.Assignments["rows[]._loop"]).Value);
    }

    // ─────────────────────────────────────────────────────────────────
    // :loop + :counter execution
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Counter_ReadableByNameAndAccumulator()
    {
        const string transform = @"{$}
odin = ""1.0.0""
direction = ""json->json""

{rows[]}
:loop items
:counter rownum
sku = ""@.sku""
n = ""@rownum""
m = ""@$accumulator.rownum""
";
        var r = RunJson(transform, @"{""items"":[{""sku"":""A""},{""sku"":""B""},{""sku"":""C""}]}");
        Assert.True(r.Success);
        var rows = r.Output!.Get("rows")!.AsArray()!;
        Assert.Equal(3, rows.Count);
        Assert.Equal(0L, rows[0].Get("n")!.AsInt64());
        Assert.Equal(0L, rows[0].Get("m")!.AsInt64());
        Assert.Equal(2L, rows[2].Get("n")!.AsInt64());
        Assert.Equal(2L, rows[2].Get("m")!.AsInt64());
    }

    // ─────────────────────────────────────────────────────────────────
    // Computation-only sink (loop) omitted from output
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ComputationSinkLoop_AccumulatesButIsOmitted()
    {
        const string transform = @"{$}
odin = ""1.0.0""
direction = ""json->json""

{$accumulator}
total = ##0

{_sumItems[]}
:loop items
_ = ""%accumulate total @.amount""

{Summary}
total = ""@$accumulator.total""
";
        var r = RunJson(transform, @"{""items"":[{""amount"":10},{""amount"":20},{""amount"":30}]}");
        Assert.True(r.Success);
        Assert.Null(r.Output!.Get("_sumItems"));
        Assert.Equal(60L, r.Output!.Get("Summary")!.Get("total")!.AsInt64());
    }

    // ─────────────────────────────────────────────────────────────────
    // Field :if path = value comparison
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void FieldIf_Comparison_EmitsOnlyWhenHolds()
    {
        const string transform = @"{$}
odin = ""1.0.0""
direction = ""json->json""

{Quote}
discount = ""@policy.discount :if @policy.tier = gold""
surcharge = ""@policy.surcharge :if @policy.tier = bronze""
";
        var r = RunJson(transform, @"{""policy"":{""tier"":""gold"",""discount"":15,""surcharge"":40}}");
        Assert.True(r.Success);
        var quote = r.Output!.Get("Quote")!;
        Assert.Equal(15L, quote.Get("discount")!.AsInt64());
        Assert.Null(quote.Get("surcharge"));
    }

    // ─────────────────────────────────────────────────────────────────
    // :object / :raw / :array
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void InlineObject_BuildsNestedObject()
    {
        const string transform = @"{$}
odin = ""1.0.0""
direction = ""json->json""

{Quote}
contact = "":object {name = @insured.name, phone = @insured.phone}""
";
        var r = RunJson(transform, @"{""insured"":{""name"":""John Doe"",""phone"":""512-555-1234""}}");
        Assert.True(r.Success);
        var contact = r.Output!.Get("Quote")!.Get("contact")!;
        Assert.Equal(DynValueType.Object, contact.Type);
        Assert.Equal("John Doe", contact.Get("name")!.AsString());
        Assert.Equal("512-555-1234", contact.Get("phone")!.AsString());
    }

    [Fact]
    public void RawJson_EmitsStructurally()
    {
        const string transform = @"{$}
odin = ""1.0.0""
direction = ""json->json""

{Document}
metadata = ""@document.jsonMetadata :raw""
";
        var r = RunJson(transform, @"{""document"":{""jsonMetadata"":""{\""version\"":2,\""active\"":true}""}}");
        Assert.True(r.Success);
        var meta = r.Output!.Get("Document")!.Get("metadata")!;
        Assert.Equal(DynValueType.Object, meta.Type);
        Assert.Equal(2L, meta.Get("version")!.AsInt64());
        Assert.Equal(true, meta.Get("active")!.AsBool());
    }

    [Fact]
    public void Array_WrapsValueInSingleElementArray()
    {
        const string transform = @"{$}
odin = ""1.0.0""
direction = ""json->json""

{Policy}
codes = ""@policy.primaryCode :array""
";
        var r = RunJson(transform, @"{""policy"":{""primaryCode"":""COLL""}}");
        Assert.True(r.Success);
        var codes = r.Output!.Get("Policy")!.Get("codes")!;
        Assert.Equal(DynValueType.Array, codes.Type);
        Assert.Single(codes.AsArray()!);
        Assert.Equal("COLL", codes.AsArray()![0].AsString());
    }

    // ─────────────────────────────────────────────────────────────────
    // :validate / :enum / :range (onValidation policies)
    // ─────────────────────────────────────────────────────────────────

    private const string ValidateTransform = @"{$}
odin = ""1.0.0""
direction = ""json->json""
target.onValidation = ""{0}""

{Record}
status = ""@record.status :enum A,P,C""
year = ""@record.year :range 1900..2100""
email = ""@record.email :validate \""^[^@]+@[^@]+$\""""
";

    [Fact]
    public void Validation_Warn_EmitsAndWarns()
    {
        var transform = ValidateTransform.Replace("{0}", "warn");
        var r = RunJson(transform, @"{""record"":{""status"":""Z"",""year"":1850,""email"":""not-an-email""}}");
        Assert.True(r.Success);
        Assert.Equal(3, r.Warnings.Count);
        var record = r.Output!.Get("Record")!;
        Assert.Equal("Z", record.Get("status")!.AsString());
        Assert.Equal(1850L, record.Get("year")!.AsInt64());
    }

    [Fact]
    public void Validation_Fail_ProducesT013Errors()
    {
        var transform = ValidateTransform.Replace("{0}", "fail");
        var r = RunJson(transform, @"{""record"":{""status"":""Z"",""year"":1850,""email"":""not-an-email""}}");
        Assert.False(r.Success);
        Assert.Contains(r.Errors, e => e.Code == "T013");
    }

    [Fact]
    public void Validation_Skip_DropsInvalidField()
    {
        var transform = ValidateTransform.Replace("{0}", "skip");
        var r = RunJson(transform, @"{""record"":{""status"":""A"",""year"":1850,""email"":""a@b""}}");
        Assert.True(r.Success);
        var record = r.Output!.Get("Record")!;
        Assert.Equal("A", record.Get("status")!.AsString());
        Assert.Null(record.Get("year"));
        Assert.Equal("a@b", record.Get("email")!.AsString());
    }

    // ─────────────────────────────────────────────────────────────────
    // XML :cdata
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Cdata_WrapsElementText()
    {
        const string transform = @"{$}
odin = ""1.0.0""
direction = ""odin->xml""
target.format = ""xml""
emitTypeHints = ?false

{Policy}
Description = ""@policy.description :cdata""
";
        const string input = @"{$}
odin = ""1.0.0""
{}
{policy}
description = ""premium < 500 & deductible > 0""
";
        var r = RunOdin(transform, input);
        Assert.True(r.Success);
        Assert.Contains("<![CDATA[premium < 500 & deductible > 0]]>", r.Formatted);
    }

    // ─────────────────────────────────────────────────────────────────
    // Fixed-width lineWidth padding
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void FixedWidth_LineWidth_PadsRecord()
    {
        const string transform = @"{$}
odin = ""1.0.0""
direction = ""odin->fixed-width""
target.format = ""fixed-width""

{$target}
lineWidth = ##20
padChar = "".""

{record}
code = @record.code :pos 0 :len 5 :rightPad "" ""
name = @record.name :pos 5 :len 8 :rightPad "" ""
";
        const string input = @"{$}
odin = ""1.0.0""
{}
{record}
code = ""AB""
name = ""WIDGET""
";
        var r = RunOdin(transform, input);
        Assert.True(r.Success);
        var line = (r.Formatted ?? "").Split('\n')[0];
        Assert.Equal(20, line.Length);
        Assert.EndsWith(".......", line);
    }
}
