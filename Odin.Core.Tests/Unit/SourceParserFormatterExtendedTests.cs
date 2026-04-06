using System.Collections.Generic;
using Odin.Core.Transform;
using Odin.Core.Types;
using Xunit;

namespace Odin.Core.Tests.Unit;

/// <summary>
/// Extended source parser and formatter tests ported from Rust for cross-language consistency.
/// Covers deeper edge cases for JSON, CSV, XML, YAML, fixed-width, flat parsers
/// and JSON, CSV, XML, fixed-width, flat, ODIN formatters.
/// </summary>
public class SourceParserFormatterExtendedTests
{
    // ─────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────

    private static string GetStr(DynValue obj, string key)
    {
        var entries = obj.AsObject();
        if (entries == null) return "";
        for (int i = 0; i < entries.Count; i++)
            if (entries[i].Key == key) return entries[i].Value.AsString() ?? "";
        return "";
    }

    private static DynValue? GetField(DynValue obj, string key)
    {
        var entries = obj.AsObject();
        if (entries == null) return null;
        for (int i = 0; i < entries.Count; i++)
            if (entries[i].Key == key) return entries[i].Value;
        return null;
    }

    // ═════════════════════════════════════════════════════════════════
    //  JSON Source Parser Extended
    // ═════════════════════════════════════════════════════════════════

    [Fact]
    public void JsonExt_ArrayOfObjects()
    {
        var result = JsonSourceParser.Parse("[{\"id\": 1}, {\"id\": 2}, {\"id\": 3}]");
        var arr = result.AsArray()!;
        Assert.Equal(3, arr.Count);
        Assert.Equal(1L, GetField(arr[0], "id")!.AsInt64());
        Assert.Equal(3L, GetField(arr[2], "id")!.AsInt64());
    }

    [Fact]
    public void JsonExt_NullValuesInObject()
    {
        var result = JsonSourceParser.Parse("{\"a\": null, \"b\": null}");
        Assert.True(GetField(result, "a")!.IsNull);
        Assert.True(GetField(result, "b")!.IsNull);
    }

    [Fact]
    public void JsonExt_NegativeNumbers()
    {
        var result = JsonSourceParser.Parse("{\"neg\": -42, \"negf\": -3.14}");
        Assert.Equal(-42L, GetField(result, "neg")!.AsInt64());
        Assert.Equal(-3.14, GetField(result, "negf")!.AsDouble());
    }

    [Fact]
    public void JsonExt_StringWithEscapes()
    {
        var result = JsonSourceParser.Parse("{\"msg\": \"line1\\nline2\\ttab\"}");
        Assert.Contains("\n", GetStr(result, "msg"));
        Assert.Contains("\t", GetStr(result, "msg"));
    }

    [Fact]
    public void JsonExt_DeeplyNestedFiveLevels()
    {
        var result = JsonSourceParser.Parse("{\"l1\": {\"l2\": {\"l3\": {\"l4\": {\"l5\": \"deep\"}}}}}");
        var l1 = GetField(result, "l1")!;
        var l2 = GetField(l1, "l2")!;
        var l3 = GetField(l2, "l3")!;
        var l4 = GetField(l3, "l4")!;
        Assert.Equal("deep", GetStr(l4, "l5"));
    }

    [Fact]
    public void JsonExt_MixedArray()
    {
        var result = JsonSourceParser.Parse("[1, \"two\", true, null, 3.14]");
        var arr = result.AsArray()!;
        Assert.Equal(5, arr.Count);
        Assert.Equal(1L, arr[0].AsInt64());
        Assert.Equal("two", arr[1].AsString());
        Assert.Equal(true, arr[2].AsBool());
        Assert.True(arr[3].IsNull);
        Assert.Equal(3.14, arr[4].AsDouble());
    }

    [Fact]
    public void JsonExt_LargeInteger()
    {
        var result = JsonSourceParser.Parse("{\"big\": 9007199254740992}");
        Assert.Equal(9007199254740992L, GetField(result, "big")!.AsInt64());
    }

    [Fact]
    public void JsonExt_ZeroValues()
    {
        var result = JsonSourceParser.Parse("{\"i\": 0, \"f\": 0.0}");
        Assert.Equal(0L, GetField(result, "i")!.AsInt64());
    }

    [Fact]
    public void JsonExt_NestedArrays()
    {
        var result = JsonSourceParser.Parse("[[1, 2], [3, 4]]");
        var outer = result.AsArray()!;
        Assert.Equal(2, outer.Count);
        var inner = outer[0].AsArray()!;
        Assert.Equal(1L, inner[0].AsInt64());
        Assert.Equal(2L, inner[1].AsInt64());
    }

    [Fact]
    public void JsonExt_WhitespaceVariations()
    {
        var result = JsonSourceParser.Parse("  {  \"a\"  :  1  }  ");
        Assert.Equal(1L, GetField(result, "a")!.AsInt64());
    }

    [Fact]
    public void JsonExt_SingleValue_String()
    {
        var result = JsonSourceParser.Parse("\"hello\"");
        Assert.Equal("hello", result.AsString());
    }

    [Fact]
    public void JsonExt_SingleValue_Number()
    {
        var result = JsonSourceParser.Parse("42");
        Assert.Equal(42L, result.AsInt64());
    }

    [Fact]
    public void JsonExt_SingleValue_Boolean()
    {
        var result = JsonSourceParser.Parse("true");
        Assert.Equal(true, result.AsBool());
    }

    [Fact]
    public void JsonExt_SingleValue_Null()
    {
        var result = JsonSourceParser.Parse("null");
        Assert.True(result.IsNull);
    }

    // ═════════════════════════════════════════════════════════════════
    //  CSV Source Parser Extended
    // ═════════════════════════════════════════════════════════════════

    [Fact]
    public void CsvExt_SingleColumn()
    {
        var result = CsvSourceParser.Parse("name\nAlice\nBob", null);
        var arr = result.AsArray()!;
        Assert.Equal(2, arr.Count);
        Assert.Equal("Alice", GetStr(arr[0], "name"));
    }

    [Fact]
    public void CsvExt_ManyRows()
    {
        var input = "id,val\n";
        for (int i = 0; i < 50; i++)
            input += $"{i},data{i}\n";
        var result = CsvSourceParser.Parse(input, null);
        Assert.Equal(50, result.AsArray()!.Count);
    }

    [Fact]
    public void CsvExt_SemicolonDelimiter()
    {
        var config = new SourceConfig { Format = "csv", Options = new Dictionary<string, string> { { "delimiter", ";" } } };
        var result = CsvSourceParser.Parse("a;b\n1;2", config);
        var arr = result.AsArray()!;
        Assert.Single(arr);
    }

    [Fact]
    public void CsvExt_HeaderOnlyNoData()
    {
        var result = CsvSourceParser.Parse("name,age", null);
        var arr = result.AsArray()!;
        Assert.Empty(arr);
    }

    [Fact]
    public void CsvExt_NullInference()
    {
        var result = CsvSourceParser.Parse("val\nnull", null);
        var arr = result.AsArray()!;
        Assert.True(GetField(arr[0], "val")!.IsNull);
    }

    [Fact]
    public void CsvExt_MixedTypeInference()
    {
        var result = CsvSourceParser.Parse("a,b,c,d\n42,3.14,true,hello", null);
        var arr = result.AsArray()!;
        var row = arr[0];
        Assert.Equal(42L, GetField(row, "a")!.AsInt64());
        Assert.Equal(3.14, GetField(row, "b")!.AsDouble());
        Assert.Equal(true, GetField(row, "c")!.AsBool());
        Assert.Equal("hello", GetStr(row, "d"));
    }

    [Fact]
    public void CsvExt_EscapedQuoteInField()
    {
        var result = CsvSourceParser.Parse("val\n\"he said \"\"hi\"\"\"", null);
        var arr = result.AsArray()!;
        Assert.Contains("\"hi\"", GetStr(arr[0], "val"));
    }

    [Fact]
    public void CsvExt_FieldWithNewline()
    {
        var result = CsvSourceParser.Parse("val\n\"line1\nline2\"", null);
        var arr = result.AsArray()!;
        Assert.Contains("\n", GetStr(arr[0], "val"));
    }

    // ═════════════════════════════════════════════════════════════════
    //  XML Source Parser Extended
    // ═════════════════════════════════════════════════════════════════

    [Fact]
    public void XmlExt_MultipleAttributes()
    {
        var result = XmlSourceParser.Parse("<item id=\"1\" type=\"product\"><name>Widget</name></item>");
        var root = GetField(result, "item")!;
        Assert.Equal("1", GetStr(root, "@id"));
        Assert.Equal("product", GetStr(root, "@type"));
    }

    [Fact]
    public void XmlExt_DeeplyNested()
    {
        var result = XmlSourceParser.Parse("<a><b><c><d><e>deep</e></d></c></b></a>");
        var a = GetField(result, "a")!;
        var b = GetField(a, "b")!;
        var c = GetField(b, "c")!;
        var d = GetField(c, "d")!;
        Assert.Equal("deep", GetStr(d, "e"));
    }

    [Fact]
    public void XmlExt_EmptyRoot()
    {
        var result = XmlSourceParser.Parse("<root></root>");
        var root = GetField(result, "root");
        Assert.NotNull(root);
    }

    [Fact]
    public void XmlExt_MultipleRepeatedElements()
    {
        var result = XmlSourceParser.Parse("<root><a>1</a><a>2</a><b>x</b><b>y</b></root>");
        var root = GetField(result, "root")!;
        var aField = GetField(root, "a");
        Assert.NotNull(aField);
        Assert.Equal(DynValueType.Array, aField!.Type);
        Assert.Equal(2, aField.AsArray()!.Count);
    }

    [Fact]
    public void XmlExt_SelfClosingMultiple()
    {
        var result = XmlSourceParser.Parse("<root><br/><hr/></root>");
        var root = GetField(result, "root")!;
        Assert.True(GetField(root, "br")!.IsNull);
        Assert.True(GetField(root, "hr")!.IsNull);
    }

    [Fact]
    public void XmlExt_SpecialCharsInText()
    {
        var result = XmlSourceParser.Parse("<msg>Price: 5 &lt; 10 &amp; 3 &gt; 1</msg>");
        var msg = GetField(result, "msg");
        Assert.NotNull(msg);
        var text = msg!.AsString() ?? GetStr(result, "msg");
        Assert.Contains("<", text);
        Assert.Contains("&", text);
    }

    [Fact]
    public void XmlExt_NumericTextContent()
    {
        var result = XmlSourceParser.Parse("<root><count>42</count></root>");
        var root = GetField(result, "root")!;
        var count = GetStr(root, "count");
        Assert.Equal("42", count);
    }

    // ═════════════════════════════════════════════════════════════════
    //  YAML Source Parser Extended
    // ═════════════════════════════════════════════════════════════════

    [Fact]
    public void YamlExt_DeeplyNested()
    {
        var result = YamlSourceParser.Parse("a:\n  b:\n    c:\n      d: deep");
        var a = GetField(result, "a")!;
        var b = GetField(a, "b")!;
        var c = GetField(b, "c")!;
        Assert.Equal("deep", GetStr(c, "d"));
    }

    [Fact]
    public void YamlExt_BooleanVariants()
    {
        var result = YamlSourceParser.Parse("a: yes\nb: no\nc: on\nd: off\ne: true\nf: false");
        Assert.Equal(true, GetField(result, "a")!.AsBool());
        Assert.Equal(false, GetField(result, "b")!.AsBool());
        Assert.Equal(true, GetField(result, "c")!.AsBool());
        Assert.Equal(false, GetField(result, "d")!.AsBool());
        Assert.Equal(true, GetField(result, "e")!.AsBool());
        Assert.Equal(false, GetField(result, "f")!.AsBool());
    }

    [Fact]
    public void YamlExt_ArrayOfScalars()
    {
        var result = YamlSourceParser.Parse("items:\n  - 1\n  - 2\n  - 3");
        var items = GetField(result, "items")!.AsArray()!;
        Assert.Equal(3, items.Count);
    }

    [Fact]
    public void YamlExt_MixedTypes()
    {
        var result = YamlSourceParser.Parse("s: hello\ni: 42\nf: 3.14\nb: true\nn: null");
        Assert.Equal("hello", GetStr(result, "s"));
        Assert.Equal(42L, GetField(result, "i")!.AsInt64());
        Assert.Equal(3.14, GetField(result, "f")!.AsDouble());
        Assert.Equal(true, GetField(result, "b")!.AsBool());
        Assert.True(GetField(result, "n")!.IsNull);
    }

    [Fact]
    public void YamlExt_MultipleComments()
    {
        var result = YamlSourceParser.Parse("# first\nname: Alice\n# middle\nage: 30\n# end");
        Assert.Equal("Alice", GetStr(result, "name"));
        Assert.Equal(30L, GetField(result, "age")!.AsInt64());
    }

    [Fact]
    public void YamlExt_EmptyValue()
    {
        var result = YamlSourceParser.Parse("val:");
        Assert.True(GetField(result, "val")!.IsNull);
    }

    [Fact]
    public void YamlExt_SingleQuotedString()
    {
        var result = YamlSourceParser.Parse("val: 'hello world'");
        Assert.Equal("hello world", GetStr(result, "val"));
    }

    // ═════════════════════════════════════════════════════════════════
    //  Fixed-Width Source Parser Extended
    // ═════════════════════════════════════════════════════════════════

    [Fact]
    public void FixedExt_VariousWidths()
    {
        var config = new SourceConfig
        {
            Format = "fixed-width",
            Options = new Dictionary<string, string> { { "columns", "A:0:2;B:2:4;C:6:4;D:10:2" } }
        };
        var result = FixedWidthSourceParser.Parse("AB1234CDEF56", config);
        Assert.Equal("AB", GetStr(result, "A"));
        Assert.Equal("1234", GetStr(result, "B"));
        Assert.Equal("CDEF", GetStr(result, "C"));
        Assert.Equal("56", GetStr(result, "D"));
    }

    [Fact]
    public void FixedExt_TrimmingWithPadding()
    {
        var config = new SourceConfig
        {
            Format = "fixed-width",
            Options = new Dictionary<string, string> { { "columns", "A:0:10;B:10:10" } }
        };
        var result = FixedWidthSourceParser.Parse("Hello     World     ", config);
        Assert.Equal("Hello", GetStr(result, "A"));
        Assert.Equal("World", GetStr(result, "B"));
    }

    [Fact]
    public void FixedExt_FieldBeyondLine()
    {
        var config = new SourceConfig
        {
            Format = "fixed-width",
            Options = new Dictionary<string, string> { { "columns", "Present:0:2;Absent:50:10" } }
        };
        var result = FixedWidthSourceParser.Parse("AB", config);
        Assert.Equal("AB", GetStr(result, "Present"));
        Assert.Equal("", GetStr(result, "Absent"));
    }

    // ═════════════════════════════════════════════════════════════════
    //  Flat Source Parser Extended
    // ═════════════════════════════════════════════════════════════════

    [Fact]
    public void FlatExt_MixedTypes()
    {
        var result = FlatSourceParser.Parse("s=hello\ni=42\nf=3.14\nb=true\nn=~");
        Assert.Equal("hello", GetStr(result, "s"));
        Assert.Equal(42L, GetField(result, "i")!.AsInt64());
        Assert.Equal(3.14, GetField(result, "f")!.AsDouble());
        Assert.Equal(true, GetField(result, "b")!.AsBool());
        Assert.True(GetField(result, "n")!.IsNull);
    }

    [Fact]
    public void FlatExt_DeeplyDottedPath()
    {
        var result = FlatSourceParser.Parse("a.b.c.d=deep");
        var a = GetField(result, "a")!;
        var b = GetField(a, "b")!;
        var c = GetField(b, "c")!;
        Assert.Equal("deep", GetStr(c, "d"));
    }

    [Fact]
    public void FlatExt_MultipleArrayIndices()
    {
        var result = FlatSourceParser.Parse("items[0]=a\nitems[1]=b\nitems[2]=c");
        var items = GetField(result, "items")!.AsArray()!;
        Assert.Equal(3, items.Count);
    }

    [Fact]
    public void FlatExt_SpacesAroundEquals()
    {
        var result = FlatSourceParser.Parse("name = Alice\nage = 30");
        Assert.Equal("Alice", GetStr(result, "name"));
        Assert.Equal(30L, GetField(result, "age")!.AsInt64());
    }

    // ═════════════════════════════════════════════════════════════════
    //  JSON Formatter Extended
    // ═════════════════════════════════════════════════════════════════

    [Fact]
    public void JsonFmtExt_CompactObject()
    {
        var obj = DynValue.Object(new List<KeyValuePair<string, DynValue>>
        {
            new("a", DynValue.Integer(1)),
            new("b", DynValue.String("two")),
        });
        var config = new TargetConfig { Format = "json", Options = new Dictionary<string, string> { { "indent", "false" } } };
        var result = JsonFormatter.Format(obj, config);
        Assert.DoesNotContain("\n", result);
        Assert.Contains("\"a\"", result);
        Assert.Contains("\"two\"", result);
    }

    [Fact]
    public void JsonFmtExt_NestedObjects()
    {
        var inner = DynValue.Object(new List<KeyValuePair<string, DynValue>> { new("inner", DynValue.Integer(42)) });
        var outer = DynValue.Object(new List<KeyValuePair<string, DynValue>> { new("outer", inner) });
        var config = new TargetConfig { Format = "json", Options = new Dictionary<string, string> { { "indent", "false" } } };
        var result = JsonFormatter.Format(outer, config);
        Assert.Contains("\"outer\"", result);
        Assert.Contains("\"inner\"", result);
        Assert.Contains("42", result);
    }

    [Fact]
    public void JsonFmtExt_ArraySimple()
    {
        var arr = DynValue.Array(new List<DynValue> { DynValue.Integer(1), DynValue.Integer(2), DynValue.Integer(3) });
        var config = new TargetConfig { Format = "json", Options = new Dictionary<string, string> { { "indent", "false" } } };
        var result = JsonFormatter.Format(arr, config);
        Assert.Contains("1", result);
        Assert.Contains("3", result);
    }

    [Fact]
    public void JsonFmtExt_NullValue()
    {
        var result = JsonFormatter.Format(DynValue.Null(), null);
        Assert.Contains("null", result);
    }

    [Fact]
    public void JsonFmtExt_EmptyObject()
    {
        var obj = DynValue.Object(new List<KeyValuePair<string, DynValue>>());
        var config = new TargetConfig { Format = "json", Options = new Dictionary<string, string> { { "indent", "false" } } };
        var result = JsonFormatter.Format(obj, config);
        Assert.Equal("{}", result);
    }

    [Fact]
    public void JsonFmtExt_EmptyArray()
    {
        var arr = DynValue.Array(new List<DynValue>());
        var config = new TargetConfig { Format = "json", Options = new Dictionary<string, string> { { "indent", "false" } } };
        var result = JsonFormatter.Format(arr, config);
        Assert.Equal("[]", result);
    }

    [Fact]
    public void JsonFmtExt_IntegerValues()
    {
        var config = new TargetConfig { Format = "json", Options = new Dictionary<string, string> { { "indent", "false" } } };
        Assert.Equal("0", JsonFormatter.Format(DynValue.Integer(0), config));
        Assert.Equal("-99", JsonFormatter.Format(DynValue.Integer(-99), config));
    }

    [Fact]
    public void JsonFmtExt_FloatValue()
    {
        var config = new TargetConfig { Format = "json", Options = new Dictionary<string, string> { { "indent", "false" } } };
        var result = JsonFormatter.Format(DynValue.Float(3.14), config);
        Assert.StartsWith("3.14", result);
    }

    [Fact]
    public void JsonFmtExt_BoolValues()
    {
        var config = new TargetConfig { Format = "json", Options = new Dictionary<string, string> { { "indent", "false" } } };
        Assert.Equal("true", JsonFormatter.Format(DynValue.Bool(true), config));
        Assert.Equal("false", JsonFormatter.Format(DynValue.Bool(false), config));
    }

    // ═════════════════════════════════════════════════════════════════
    //  CSV Formatter Extended
    // ═════════════════════════════════════════════════════════════════

    [Fact]
    public void CsvFmtExt_BasicHeadersAndRows()
    {
        var val = DynValue.Array(new List<DynValue>
        {
            DynValue.Object(new List<KeyValuePair<string, DynValue>> { new("name", DynValue.String("Alice")), new("age", DynValue.Integer(30)) }),
            DynValue.Object(new List<KeyValuePair<string, DynValue>> { new("name", DynValue.String("Bob")), new("age", DynValue.Integer(25)) }),
        });
        var csv = CsvFormatter.Format(val, null);
        var lines = csv.Split('\n');
        Assert.Contains("name", lines[0]);
        Assert.Contains("age", lines[0]);
        Assert.Contains("Alice", csv);
        Assert.Contains("Bob", csv);
    }

    [Fact]
    public void CsvFmtExt_QuotingCommas()
    {
        var val = DynValue.Array(new List<DynValue>
        {
            DynValue.Object(new List<KeyValuePair<string, DynValue>> { new("msg", DynValue.String("hello, world")) }),
        });
        var csv = CsvFormatter.Format(val, null);
        Assert.Contains("\"hello, world\"", csv);
    }

    [Fact]
    public void CsvFmtExt_QuotingQuotes()
    {
        var val = DynValue.Array(new List<DynValue>
        {
            DynValue.Object(new List<KeyValuePair<string, DynValue>> { new("msg", DynValue.String("say \"hi\"")) }),
        });
        var csv = CsvFormatter.Format(val, null);
        Assert.Contains("\"\"hi\"\"", csv);
    }

    [Fact]
    public void CsvFmtExt_EmptyArray()
    {
        var csv = CsvFormatter.Format(DynValue.Array(new List<DynValue>()), null);
        Assert.True(csv.Length == 0 || csv.Trim().Length == 0);
    }

    [Fact]
    public void CsvFmtExt_NullAsEmpty()
    {
        var val = DynValue.Array(new List<DynValue>
        {
            DynValue.Object(new List<KeyValuePair<string, DynValue>> { new("a", DynValue.String("x")), new("b", DynValue.Null()) }),
        });
        var csv = CsvFormatter.Format(val, null);
        Assert.Contains("x,", csv);
    }

    // ═════════════════════════════════════════════════════════════════
    //  XML Formatter Extended
    // ═════════════════════════════════════════════════════════════════

    [Fact]
    public void XmlFmtExt_NestedObjects()
    {
        var val = DynValue.Object(new List<KeyValuePair<string, DynValue>>
        {
            new("person", DynValue.Object(new List<KeyValuePair<string, DynValue>>
            {
                new("address", DynValue.Object(new List<KeyValuePair<string, DynValue>>
                {
                    new("city", DynValue.String("NYC"))
                }))
            }))
        });
        var xml = XmlFormatter.Format(val, null);
        Assert.Contains("<person>", xml);
        Assert.Contains("<address>", xml);
        Assert.Contains("<city>NYC</city>", xml);
    }

    [Fact]
    public void XmlFmtExt_SpecialCharsEscaped()
    {
        var val = DynValue.Object(new List<KeyValuePair<string, DynValue>>
        {
            new("Root", DynValue.Object(new List<KeyValuePair<string, DynValue>>
            {
                new("msg", DynValue.String("a & b < c"))
            }))
        });
        var xml = XmlFormatter.Format(val, null);
        Assert.Contains("&amp;", xml);
        Assert.Contains("&lt;", xml);
    }

    [Fact]
    public void XmlFmtExt_IntegerElement()
    {
        var val = DynValue.Object(new List<KeyValuePair<string, DynValue>>
        {
            new("Root", DynValue.Object(new List<KeyValuePair<string, DynValue>>
            {
                new("count", DynValue.Integer(42))
            }))
        });
        var xml = XmlFormatter.Format(val, null);
        Assert.Contains("<count odin:type=\"integer\">42</count>", xml);
    }

    [Fact]
    public void XmlFmtExt_NullSelfClosing()
    {
        var val = DynValue.Object(new List<KeyValuePair<string, DynValue>>
        {
            new("Root", DynValue.Object(new List<KeyValuePair<string, DynValue>>
            {
                new("empty", DynValue.Null())
            }))
        });
        var xml = XmlFormatter.Format(val, null);
        Assert.Contains("<empty odin:type=\"null\"></empty>", xml);
    }

    [Fact]
    public void XmlFmtExt_BoolElement()
    {
        var val = DynValue.Object(new List<KeyValuePair<string, DynValue>>
        {
            new("Root", DynValue.Object(new List<KeyValuePair<string, DynValue>>
            {
                new("yes", DynValue.Bool(true)),
                new("no", DynValue.Bool(false)),
            }))
        });
        var xml = XmlFormatter.Format(val, null);
        Assert.Contains("<yes odin:type=\"boolean\">true</yes>", xml);
        Assert.Contains("<no odin:type=\"boolean\">false</no>", xml);
    }

    // ═════════════════════════════════════════════════════════════════
    //  Fixed-Width Formatter Extended
    // ═════════════════════════════════════════════════════════════════

    [Fact]
    public void FixedFmtExt_StringLeftAligned()
    {
        var obj = DynValue.Object(new List<KeyValuePair<string, DynValue>> { new("name", DynValue.String("Hi")) });
        var config = new TargetConfig { Format = "fixed-width", Options = new Dictionary<string, string> { { "columns", "name:10" } } };
        var result = FixedWidthFormatter.Format(obj, config);
        Assert.Contains("Hi", result);
        Assert.True(result.TrimEnd('\n').Length >= 10);
    }

    [Fact]
    public void FixedFmtExt_TruncationString()
    {
        var obj = DynValue.Object(new List<KeyValuePair<string, DynValue>> { new("name", DynValue.String("VeryLongName")) });
        var config = new TargetConfig { Format = "fixed-width", Options = new Dictionary<string, string> { { "columns", "name:5" } } };
        var result = FixedWidthFormatter.Format(obj, config);
        Assert.True(result.TrimEnd('\n').Length <= 5);
    }

    [Fact]
    public void FixedFmtExt_MultipleRecords()
    {
        var val = DynValue.Array(new List<DynValue>
        {
            DynValue.Object(new List<KeyValuePair<string, DynValue>> { new("id", DynValue.Integer(1)) }),
            DynValue.Object(new List<KeyValuePair<string, DynValue>> { new("id", DynValue.Integer(2)) }),
            DynValue.Object(new List<KeyValuePair<string, DynValue>> { new("id", DynValue.Integer(3)) }),
        });
        var config = new TargetConfig { Format = "fixed-width", Options = new Dictionary<string, string> { { "columns", "id:5" } } };
        var result = FixedWidthFormatter.Format(val, config);
        Assert.Equal(3, result.Trim().Split('\n').Length);
    }

    [Fact]
    public void FixedFmtExt_MissingField()
    {
        var obj = DynValue.Object(new List<KeyValuePair<string, DynValue>> { new("a", DynValue.String("x")) });
        var config = new TargetConfig { Format = "fixed-width", Options = new Dictionary<string, string> { { "columns", "a:5;missing:5" } } };
        var result = FixedWidthFormatter.Format(obj, config);
        Assert.True(result.TrimEnd('\n').Length == 10);
    }

    // ═════════════════════════════════════════════════════════════════
    //  Flat Formatter Extended
    // ═════════════════════════════════════════════════════════════════

    [Fact]
    public void FlatFmtExt_NestedObject()
    {
        var inner = DynValue.Object(new List<KeyValuePair<string, DynValue>> { new("city", DynValue.String("Portland")) });
        var outer = DynValue.Object(new List<KeyValuePair<string, DynValue>> { new("address", inner) });
        var result = FlatFormatter.Format(outer, null);
        Assert.Contains("address.city=Portland", result);
    }

    [Fact]
    public void FlatFmtExt_ArrayValues()
    {
        var arr = DynValue.Array(new List<DynValue> { DynValue.String("a"), DynValue.String("b"), DynValue.String("c") });
        var obj = DynValue.Object(new List<KeyValuePair<string, DynValue>> { new("items", arr) });
        var result = FlatFormatter.Format(obj, null);
        Assert.Contains("items[0]=a", result);
        Assert.Contains("items[1]=b", result);
        Assert.Contains("items[2]=c", result);
    }

    [Fact]
    public void FlatFmtExt_Sorted()
    {
        var obj = DynValue.Object(new List<KeyValuePair<string, DynValue>>
        {
            new("z", DynValue.String("last")),
            new("a", DynValue.String("first")),
            new("m", DynValue.String("middle")),
        });
        var result = FlatFormatter.Format(obj, null);
        int posA = result.IndexOf("a=");
        int posM = result.IndexOf("m=");
        int posZ = result.IndexOf("z=");
        Assert.True(posA < posM && posM < posZ);
    }

    [Fact]
    public void FlatFmtExt_IntegerValues()
    {
        var obj = DynValue.Object(new List<KeyValuePair<string, DynValue>>
        {
            new("count", DynValue.Integer(42)),
            new("neg", DynValue.Integer(-5)),
        });
        var result = FlatFormatter.Format(obj, null);
        Assert.Contains("count=42", result);
        Assert.Contains("neg=-5", result);
    }

    // ═════════════════════════════════════════════════════════════════
    //  ODIN Formatter Extended
    // ═════════════════════════════════════════════════════════════════

    [Fact]
    public void OdinFmtExt_ArrayValue()
    {
        var inner = DynValue.Object(new List<KeyValuePair<string, DynValue>>
        {
            new("Items", DynValue.Array(new List<DynValue> { DynValue.String("a"), DynValue.String("b") }))
        });
        var result = OdinFormatter.Format(inner, null);
        // ODIN formatter uses value-only array syntax: {Items[] : ~} with values on separate lines
        Assert.Contains("Items[]", result);
        Assert.Contains("\"a\"", result);
        Assert.Contains("\"b\"", result);
    }

    [Fact]
    public void OdinFmtExt_NestedSections()
    {
        var inner = DynValue.Object(new List<KeyValuePair<string, DynValue>>
        {
            new("A", DynValue.Object(new List<KeyValuePair<string, DynValue>>
            {
                new("x", DynValue.Integer(1))
            })),
            new("B", DynValue.Object(new List<KeyValuePair<string, DynValue>>
            {
                new("y", DynValue.Integer(2))
            }))
        });
        var result = OdinFormatter.Format(inner, null);
        Assert.Contains("##1", result);
        Assert.Contains("##2", result);
    }

    [Fact]
    public void OdinFmtExt_DeprecatedModifier()
    {
        var obj = DynValue.Object(new List<KeyValuePair<string, DynValue>> { new("val", DynValue.String("old")) });
        var mods = new Dictionary<string, OdinModifiers> { { "val", new OdinModifiers { Deprecated = true } } };
        var result = OdinFormatter.FormatWithModifiers(obj, null, mods);
        Assert.Contains("-", result);
    }

    [Fact]
    public void OdinFmtExt_AllThreeModifiers()
    {
        var obj = DynValue.Object(new List<KeyValuePair<string, DynValue>> { new("val", DynValue.String("x")) });
        var mods = new Dictionary<string, OdinModifiers> { { "val", new OdinModifiers { Required = true, Confidential = true, Deprecated = true } } };
        var result = OdinFormatter.FormatWithModifiers(obj, null, mods);
        Assert.Contains("!", result);
        Assert.Contains("*", result);
        Assert.Contains("-", result);
    }

    [Fact]
    public void OdinFmtExt_NoHeaderOption()
    {
        var obj = DynValue.Object(new List<KeyValuePair<string, DynValue>> { new("val", DynValue.String("test")) });
        var config = new TargetConfig { Format = "odin", Options = new Dictionary<string, string> { { "header", "false" } } };
        var result = OdinFormatter.Format(obj, config);
        Assert.DoesNotContain("{$}", result);
    }

    [Fact]
    public void OdinFmtExt_NullTilde()
    {
        var obj = DynValue.Object(new List<KeyValuePair<string, DynValue>> { new("val", DynValue.Null()) });
        var result = OdinFormatter.Format(obj, null);
        Assert.Contains("~", result);
    }

    [Fact]
    public void OdinFmtExt_BooleanPrefix()
    {
        var obj = DynValue.Object(new List<KeyValuePair<string, DynValue>>
        {
            new("t", DynValue.Bool(true)),
            new("f", DynValue.Bool(false)),
        });
        var result = OdinFormatter.Format(obj, null);
        Assert.Contains("?true", result);
        Assert.Contains("?false", result);
    }
}
