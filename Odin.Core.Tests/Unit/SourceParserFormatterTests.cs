using System.Collections.Generic;
using Odin.Core.Transform;
using Odin.Core.Types;
using Xunit;

namespace Odin.Core.Tests.Unit;

/// <summary>
/// Tests for source parsers (JSON, CSV, XML, YAML, Fixed-width, Flat)
/// and output formatters (JSON, CSV, XML, Fixed-width, Flat, ODIN).
/// </summary>
public class SourceParserFormatterTests
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
    //  JSON Source Parser
    // ═════════════════════════════════════════════════════════════════

    [Fact]
    public void JsonSource_SimpleObject()
    {
        var result = JsonSourceParser.Parse("{\"name\": \"Alice\", \"age\": 30}");
        Assert.Equal(DynValueType.Object, result.Type);
        Assert.Equal("Alice", GetStr(result, "name"));
    }

    [Fact]
    public void JsonSource_NestedObject()
    {
        var result = JsonSourceParser.Parse("{\"person\": {\"name\": \"Bob\"}}");
        var person = GetField(result, "person");
        Assert.NotNull(person);
        Assert.Equal("Bob", GetStr(person!, "name"));
    }

    [Fact]
    public void JsonSource_Array()
    {
        var result = JsonSourceParser.Parse("[1, 2, 3]");
        Assert.Equal(DynValueType.Array, result.Type);
        Assert.Equal(3, result.AsArray()!.Count);
    }

    [Fact]
    public void JsonSource_NestedArray()
    {
        var result = JsonSourceParser.Parse("{\"items\": [{\"id\": 1}, {\"id\": 2}]}");
        var items = GetField(result, "items");
        Assert.NotNull(items);
        Assert.Equal(2, items!.AsArray()!.Count);
    }

    [Fact]
    public void JsonSource_Boolean()
    {
        var result = JsonSourceParser.Parse("{\"active\": true, \"deleted\": false}");
        Assert.Equal(true, GetField(result, "active")!.AsBool());
        Assert.Equal(false, GetField(result, "deleted")!.AsBool());
    }

    [Fact]
    public void JsonSource_Null()
    {
        var result = JsonSourceParser.Parse("{\"val\": null}");
        Assert.True(GetField(result, "val")!.IsNull);
    }

    [Fact]
    public void JsonSource_Integer()
    {
        var result = JsonSourceParser.Parse("{\"n\": 42}");
        Assert.Equal(42L, GetField(result, "n")!.AsInt64());
    }

    [Fact]
    public void JsonSource_Float()
    {
        var result = JsonSourceParser.Parse("{\"n\": 3.14}");
        Assert.Equal(3.14, GetField(result, "n")!.AsDouble());
    }

    [Fact]
    public void JsonSource_EscapedString()
    {
        var result = JsonSourceParser.Parse("{\"text\": \"hello\\nworld\"}");
        Assert.Contains("\n", GetStr(result, "text"));
    }

    [Fact]
    public void JsonSource_UnicodeEscape()
    {
        var result = JsonSourceParser.Parse("{\"text\": \"caf\\u00e9\"}");
        Assert.Equal("caf\u00e9", GetStr(result, "text"));
    }

    [Fact]
    public void JsonSource_EmptyObject()
    {
        var result = JsonSourceParser.Parse("{}");
        Assert.Equal(DynValueType.Object, result.Type);
        Assert.Empty(result.AsObject()!);
    }

    [Fact]
    public void JsonSource_EmptyArray()
    {
        var result = JsonSourceParser.Parse("[]");
        Assert.Equal(DynValueType.Array, result.Type);
        Assert.Empty(result.AsArray()!);
    }

    [Fact]
    public void JsonSource_NullInput_Throws()
    {
        Assert.Throws<System.ArgumentException>(() => JsonSourceParser.Parse(null!));
    }

    [Fact]
    public void JsonSource_EmptyString_Throws()
    {
        Assert.Throws<System.ArgumentException>(() => JsonSourceParser.Parse(""));
    }

    [Fact]
    public void JsonSource_MalformedJson_Throws()
    {
        Assert.Throws<System.FormatException>(() => JsonSourceParser.Parse("{invalid}"));
    }

    [Fact]
    public void JsonSource_DeeplyNested()
    {
        var result = JsonSourceParser.Parse("{\"a\": {\"b\": {\"c\": {\"d\": \"deep\"}}}}");
        var a = GetField(result, "a");
        var b = GetField(a!, "b");
        var c = GetField(b!, "c");
        Assert.Equal("deep", GetStr(c!, "d"));
    }

    // ═════════════════════════════════════════════════════════════════
    //  CSV Source Parser
    // ═════════════════════════════════════════════════════════════════

    [Fact]
    public void CsvSource_WithHeaders()
    {
        var result = CsvSourceParser.Parse("Name,Age\nAlice,30\nBob,25", null);
        var arr = result.AsArray();
        Assert.NotNull(arr);
        Assert.Equal(2, arr!.Count);
        Assert.Equal("Alice", GetStr(arr[0], "Name"));
        Assert.Equal("Bob", GetStr(arr[1], "Name"));
    }

    [Fact]
    public void CsvSource_CustomDelimiter()
    {
        var config = new SourceConfig { Format = "csv", Options = new Dictionary<string, string> { { "delimiter", "|" } } };
        var result = CsvSourceParser.Parse("Name|Age\nAlice|30", config);
        var arr = result.AsArray();
        Assert.NotNull(arr);
        Assert.True(arr!.Count >= 1);
        Assert.Equal("Alice", GetStr(arr[0], "Name"));
    }

    [Fact]
    public void CsvSource_NoHeader()
    {
        var config = new SourceConfig { Format = "csv", Options = new Dictionary<string, string> { { "hasHeader", "false" } } };
        var result = CsvSourceParser.Parse("Alice,30\nBob,25", config);
        var arr = result.AsArray();
        Assert.NotNull(arr);
        Assert.Equal(2, arr!.Count);
        // Without headers, each row is an array
        Assert.NotNull(arr[0].AsArray());
    }

    [Fact]
    public void CsvSource_QuotedField()
    {
        var result = CsvSourceParser.Parse("Name,City\n\"Smith, John\",Portland", null);
        var arr = result.AsArray()!;
        Assert.Equal("Smith, John", GetStr(arr[0], "Name"));
    }

    [Fact]
    public void CsvSource_EscapedQuote()
    {
        var result = CsvSourceParser.Parse("Name,Note\nAlice,\"She said \"\"hi\"\"\"", null);
        var arr = result.AsArray()!;
        Assert.Contains("\"hi\"", GetStr(arr[0], "Note"));
    }

    [Fact]
    public void CsvSource_EmptyFields()
    {
        var result = CsvSourceParser.Parse("A,B,C\n1,,3", null);
        var arr = result.AsArray()!;
        var b = GetField(arr[0], "B");
        Assert.NotNull(b);
        Assert.Equal("", b!.AsString());
    }

    [Fact]
    public void CsvSource_BooleanInference()
    {
        var result = CsvSourceParser.Parse("Val\ntrue\nfalse", null);
        var arr = result.AsArray()!;
        Assert.Equal(true, GetField(arr[0], "Val")!.AsBool());
        Assert.Equal(false, GetField(arr[1], "Val")!.AsBool());
    }

    [Fact]
    public void CsvSource_IntegerInference()
    {
        var result = CsvSourceParser.Parse("Val\n42", null);
        var arr = result.AsArray()!;
        Assert.Equal(42L, GetField(arr[0], "Val")!.AsInt64());
    }

    [Fact]
    public void CsvSource_FloatInference()
    {
        var result = CsvSourceParser.Parse("Val\n3.14", null);
        var arr = result.AsArray()!;
        Assert.Equal(3.14, GetField(arr[0], "Val")!.AsDouble());
    }

    [Fact]
    public void CsvSource_NullInference()
    {
        var result = CsvSourceParser.Parse("Val\nnull", null);
        var arr = result.AsArray()!;
        Assert.True(GetField(arr[0], "Val")!.IsNull);
    }

    [Fact]
    public void CsvSource_EmptyInput_ReturnsEmptyArray()
    {
        var result = CsvSourceParser.Parse("", null);
        var arr = result.AsArray();
        Assert.NotNull(arr);
        Assert.Empty(arr!);
    }

    // ═════════════════════════════════════════════════════════════════
    //  XML Source Parser
    // ═════════════════════════════════════════════════════════════════

    [Fact]
    public void XmlSource_SimpleElement()
    {
        var result = XmlSourceParser.Parse("<root><name>Alice</name></root>");
        var root = GetField(result, "root");
        Assert.NotNull(root);
        Assert.Equal("Alice", GetStr(root!, "name"));
    }

    [Fact]
    public void XmlSource_Attributes()
    {
        var result = XmlSourceParser.Parse("<root><person id=\"1\">Alice</person></root>");
        var root = GetField(result, "root");
        var person = GetField(root!, "person");
        Assert.NotNull(person);
        Assert.Equal("1", GetStr(person!, "@id"));
    }

    [Fact]
    public void XmlSource_NestedElements()
    {
        var result = XmlSourceParser.Parse("<root><address><city>Portland</city><state>OR</state></address></root>");
        var root = GetField(result, "root");
        var address = GetField(root!, "address");
        Assert.Equal("Portland", GetStr(address!, "city"));
        Assert.Equal("OR", GetStr(address!, "state"));
    }

    [Fact]
    public void XmlSource_RepeatedElements_BecomeArray()
    {
        var result = XmlSourceParser.Parse("<root><item>a</item><item>b</item><item>c</item></root>");
        var root = GetField(result, "root");
        var items = GetField(root!, "item");
        Assert.NotNull(items);
        Assert.Equal(DynValueType.Array, items!.Type);
        Assert.Equal(3, items.AsArray()!.Count);
    }

    [Fact]
    public void XmlSource_SelfClosing_IsNull()
    {
        var result = XmlSourceParser.Parse("<root><empty/></root>");
        var root = GetField(result, "root");
        var empty = GetField(root!, "empty");
        Assert.NotNull(empty);
        Assert.True(empty!.IsNull);
    }

    [Fact]
    public void XmlSource_EmptyElement_IsEmptyString()
    {
        var result = XmlSourceParser.Parse("<root><empty></empty></root>");
        var root = GetField(result, "root");
        Assert.Equal("", GetStr(root!, "empty"));
    }

    [Fact]
    public void XmlSource_NilAttribute()
    {
        var result = XmlSourceParser.Parse("<root><val nil=\"true\"/></root>");
        var root = GetField(result, "root");
        var val = GetField(root!, "val");
        Assert.NotNull(val);
        Assert.True(val!.IsNull);
    }

    [Fact]
    public void XmlSource_MixedContent()
    {
        var result = XmlSourceParser.Parse("<root><p>Hello <b>world</b></p></root>");
        var root = GetField(result, "root");
        var p = GetField(root!, "p");
        Assert.NotNull(p);
    }

    [Fact]
    public void XmlSource_NullInput_Throws()
    {
        Assert.Throws<System.ArgumentException>(() => XmlSourceParser.Parse(null!));
    }

    [Fact]
    public void XmlSource_EmptyInput_Throws()
    {
        Assert.Throws<System.ArgumentException>(() => XmlSourceParser.Parse(""));
    }

    [Fact]
    public void XmlSource_InvalidXml_Throws()
    {
        Assert.Throws<System.FormatException>(() => XmlSourceParser.Parse("<unclosed>"));
    }

    // ═════════════════════════════════════════════════════════════════
    //  Fixed-Width Source Parser
    // ═════════════════════════════════════════════════════════════════

    [Fact]
    public void FixedWidthSource_SingleRecord()
    {
        var config = new SourceConfig
        {
            Format = "fixed-width",
            Options = new Dictionary<string, string>
            {
                { "columns", "Name:0:10;Amount:10:5" }
            }
        };
        var result = FixedWidthSourceParser.Parse("Alice     00100", config);
        Assert.Equal(DynValueType.Object, result.Type);
        Assert.Equal("Alice", GetStr(result, "Name"));
        Assert.Equal("00100", GetStr(result, "Amount"));
    }

    [Fact]
    public void FixedWidthSource_MultipleRecords()
    {
        var config = new SourceConfig
        {
            Format = "fixed-width",
            Options = new Dictionary<string, string>
            {
                { "columns", "Name:0:5;Val:5:3" }
            }
        };
        var result = FixedWidthSourceParser.Parse("Alice100\nBob  200", config);
        Assert.Equal(DynValueType.Array, result.Type);
        var arr = result.AsArray()!;
        Assert.Equal(2, arr.Count);
    }

    [Fact]
    public void FixedWidthSource_Trimming()
    {
        var config = new SourceConfig
        {
            Format = "fixed-width",
            Options = new Dictionary<string, string>
            {
                { "columns", "Name:0:10" }
            }
        };
        var result = FixedWidthSourceParser.Parse("Alice     ", config);
        Assert.Equal("Alice", GetStr(result, "Name"));
    }

    [Fact]
    public void FixedWidthSource_NoColumns_Throws()
    {
        Assert.Throws<System.ArgumentException>(() =>
            FixedWidthSourceParser.Parse("test", new SourceConfig()));
    }

    [Fact]
    public void FixedWidthSource_EmptyInput_ReturnsEmptyArray()
    {
        var config = new SourceConfig
        {
            Format = "fixed-width",
            Options = new Dictionary<string, string> { { "columns", "Name:0:5" } }
        };
        var result = FixedWidthSourceParser.Parse("", config);
        Assert.Equal(DynValueType.Array, result.Type);
        Assert.Empty(result.AsArray()!);
    }

    // ═════════════════════════════════════════════════════════════════
    //  YAML Source Parser
    // ═════════════════════════════════════════════════════════════════

    [Fact]
    public void YamlSource_SimpleMapping()
    {
        var result = YamlSourceParser.Parse("name: Alice\nage: 30");
        Assert.Equal("Alice", GetStr(result, "name"));
        Assert.Equal(30L, GetField(result, "age")!.AsInt64());
    }

    [Fact]
    public void YamlSource_NestedMapping()
    {
        var result = YamlSourceParser.Parse("person:\n  name: Alice\n  age: 30");
        var person = GetField(result, "person");
        Assert.NotNull(person);
        Assert.Equal("Alice", GetStr(person!, "name"));
    }

    [Fact]
    public void YamlSource_Array()
    {
        var result = YamlSourceParser.Parse("items:\n  - a\n  - b\n  - c");
        var items = GetField(result, "items");
        Assert.NotNull(items);
        Assert.Equal(3, items!.AsArray()!.Count);
    }

    [Fact]
    public void YamlSource_BooleanTrue()
    {
        var result = YamlSourceParser.Parse("active: true");
        Assert.Equal(true, GetField(result, "active")!.AsBool());
    }

    [Fact]
    public void YamlSource_BooleanFalse()
    {
        var result = YamlSourceParser.Parse("active: false");
        Assert.Equal(false, GetField(result, "active")!.AsBool());
    }

    [Fact]
    public void YamlSource_BooleanYesNo()
    {
        var result = YamlSourceParser.Parse("a: yes\nb: no");
        Assert.Equal(true, GetField(result, "a")!.AsBool());
        Assert.Equal(false, GetField(result, "b")!.AsBool());
    }

    [Fact]
    public void YamlSource_NullValue()
    {
        var result = YamlSourceParser.Parse("val: null");
        Assert.True(GetField(result, "val")!.IsNull);
    }

    [Fact]
    public void YamlSource_TildeNull()
    {
        var result = YamlSourceParser.Parse("val: ~");
        Assert.True(GetField(result, "val")!.IsNull);
    }

    [Fact]
    public void YamlSource_QuotedString()
    {
        var result = YamlSourceParser.Parse("val: \"hello world\"");
        Assert.Equal("hello world", GetStr(result, "val"));
    }

    [Fact]
    public void YamlSource_IntegerValue()
    {
        var result = YamlSourceParser.Parse("val: 42");
        Assert.Equal(42L, GetField(result, "val")!.AsInt64());
    }

    [Fact]
    public void YamlSource_FloatValue()
    {
        var result = YamlSourceParser.Parse("val: 3.14");
        Assert.Equal(3.14, GetField(result, "val")!.AsDouble());
    }

    [Fact]
    public void YamlSource_EmptyInput()
    {
        var result = YamlSourceParser.Parse("");
        Assert.Equal(DynValueType.Object, result.Type);
    }

    [Fact]
    public void YamlSource_Comments()
    {
        var result = YamlSourceParser.Parse("# comment\nname: Alice\n# another comment");
        Assert.Equal("Alice", GetStr(result, "name"));
    }

    // ═════════════════════════════════════════════════════════════════
    //  Flat Source Parser
    // ═════════════════════════════════════════════════════════════════

    [Fact]
    public void FlatSource_SimpleKvp()
    {
        var result = FlatSourceParser.Parse("name=Alice\nage=30");
        Assert.Equal("Alice", GetStr(result, "name"));
        Assert.Equal(30L, GetField(result, "age")!.AsInt64());
    }

    [Fact]
    public void FlatSource_DottedPaths()
    {
        var result = FlatSourceParser.Parse("person.name=Alice\nperson.age=30");
        var person = GetField(result, "person");
        Assert.NotNull(person);
        Assert.Equal("Alice", GetStr(person!, "name"));
    }

    [Fact]
    public void FlatSource_ArrayBrackets()
    {
        var result = FlatSourceParser.Parse("items[0]=a\nitems[1]=b");
        var items = GetField(result, "items");
        Assert.NotNull(items);
        Assert.Equal(2, items!.AsArray()!.Count);
    }

    [Fact]
    public void FlatSource_Comments()
    {
        var result = FlatSourceParser.Parse("# comment\nname=Alice\n; another comment");
        Assert.Equal("Alice", GetStr(result, "name"));
    }

    [Fact]
    public void FlatSource_NullValue()
    {
        var result = FlatSourceParser.Parse("val=~");
        Assert.True(GetField(result, "val")!.IsNull);
    }

    [Fact]
    public void FlatSource_QuotedValue()
    {
        var result = FlatSourceParser.Parse("val=\"hello world\"");
        Assert.Equal("hello world", GetStr(result, "val"));
    }

    [Fact]
    public void FlatSource_BooleanInference()
    {
        var result = FlatSourceParser.Parse("a=true\nb=false");
        Assert.Equal(true, GetField(result, "a")!.AsBool());
        Assert.Equal(false, GetField(result, "b")!.AsBool());
    }

    [Fact]
    public void FlatSource_EmptyInput()
    {
        var result = FlatSourceParser.Parse("");
        Assert.Equal(DynValueType.Object, result.Type);
    }

    // ═════════════════════════════════════════════════════════════════
    //  JSON Formatter
    // ═════════════════════════════════════════════════════════════════

    [Fact]
    public void JsonFormatter_SimpleObject()
    {
        var obj = DynValue.Object(new List<KeyValuePair<string, DynValue>>
        {
            new("name", DynValue.String("Alice")),
            new("age", DynValue.Integer(30))
        });
        var json = JsonFormatter.Format(obj, null);
        Assert.Contains("\"name\"", json);
        Assert.Contains("Alice", json);
        Assert.Contains("30", json);
    }

    [Fact]
    public void JsonFormatter_PrettyPrint()
    {
        var obj = DynValue.Object(new List<KeyValuePair<string, DynValue>>
        {
            new("a", DynValue.Integer(1))
        });
        var json = JsonFormatter.Format(obj, null);
        Assert.Contains("\n", json); // Pretty by default
    }

    [Fact]
    public void JsonFormatter_Compact()
    {
        var obj = DynValue.Object(new List<KeyValuePair<string, DynValue>>
        {
            new("a", DynValue.Integer(1))
        });
        var config = new TargetConfig { Format = "json", Options = new Dictionary<string, string> { { "indent", "false" } } };
        var json = JsonFormatter.Format(obj, config);
        Assert.DoesNotContain("\n", json);
    }

    [Fact]
    public void JsonFormatter_NullValue()
    {
        var obj = DynValue.Object(new List<KeyValuePair<string, DynValue>>
        {
            new("val", DynValue.Null())
        });
        var json = JsonFormatter.Format(obj, null);
        Assert.Contains("null", json);
    }

    [Fact]
    public void JsonFormatter_BooleanValues()
    {
        var obj = DynValue.Object(new List<KeyValuePair<string, DynValue>>
        {
            new("a", DynValue.Bool(true)),
            new("b", DynValue.Bool(false))
        });
        var json = JsonFormatter.Format(obj, null);
        Assert.Contains("true", json);
        Assert.Contains("false", json);
    }

    [Fact]
    public void JsonFormatter_FloatValue()
    {
        var obj = DynValue.Object(new List<KeyValuePair<string, DynValue>>
        {
            new("val", DynValue.Float(3.14))
        });
        var json = JsonFormatter.Format(obj, null);
        Assert.Contains("3.14", json);
    }

    [Fact]
    public void JsonFormatter_ArrayValue()
    {
        var arr = DynValue.Array(new List<DynValue>
        {
            DynValue.Integer(1), DynValue.Integer(2), DynValue.Integer(3)
        });
        var json = JsonFormatter.Format(arr, null);
        Assert.Contains("[", json);
        Assert.Contains("1", json);
        Assert.Contains("3", json);
    }

    [Fact]
    public void JsonFormatter_NestedObject()
    {
        var inner = DynValue.Object(new List<KeyValuePair<string, DynValue>>
        {
            new("b", DynValue.String("val"))
        });
        var outer = DynValue.Object(new List<KeyValuePair<string, DynValue>>
        {
            new("a", inner)
        });
        var json = JsonFormatter.Format(outer, null);
        Assert.Contains("\"a\"", json);
        Assert.Contains("\"b\"", json);
        Assert.Contains("val", json);
    }

    [Fact]
    public void JsonFormatter_NullDynValue()
    {
        var json = JsonFormatter.Format(null!, null);
        Assert.Equal("null", json);
    }

    // ═════════════════════════════════════════════════════════════════
    //  CSV Formatter
    // ═════════════════════════════════════════════════════════════════

    [Fact]
    public void CsvFormatter_BasicOutput()
    {
        var rows = DynValue.Array(new List<DynValue>
        {
            DynValue.Object(new List<KeyValuePair<string, DynValue>>
            {
                new("Name", DynValue.String("Alice")),
                new("Age", DynValue.Integer(30))
            })
        });
        var csv = CsvFormatter.Format(rows, null);
        Assert.Contains("Name", csv);
        Assert.Contains("Alice", csv);
    }

    [Fact]
    public void CsvFormatter_WithHeader()
    {
        var rows = DynValue.Array(new List<DynValue>
        {
            DynValue.Object(new List<KeyValuePair<string, DynValue>>
            {
                new("A", DynValue.String("1")),
                new("B", DynValue.String("2"))
            })
        });
        var csv = CsvFormatter.Format(rows, null);
        var lines = csv.Split('\n');
        Assert.Contains("A", lines[0]);
        Assert.Contains("B", lines[0]);
    }

    [Fact]
    public void CsvFormatter_NoHeader()
    {
        var rows = DynValue.Array(new List<DynValue>
        {
            DynValue.Object(new List<KeyValuePair<string, DynValue>>
            {
                new("A", DynValue.String("val"))
            })
        });
        var config = new TargetConfig { Options = new Dictionary<string, string> { { "includeHeader", "false" } } };
        var csv = CsvFormatter.Format(rows, config);
        // Only data line, no header
        var lines = csv.Trim().Split('\n');
        Assert.True(lines.Length == 1, "Expected exactly one line");
    }

    [Fact]
    public void CsvFormatter_Quoting()
    {
        var rows = DynValue.Array(new List<DynValue>
        {
            DynValue.Object(new List<KeyValuePair<string, DynValue>>
            {
                new("Val", DynValue.String("hello,world"))
            })
        });
        var csv = CsvFormatter.Format(rows, null);
        Assert.Contains("\"hello,world\"", csv);
    }

    [Fact]
    public void CsvFormatter_NullHandling()
    {
        var rows = DynValue.Array(new List<DynValue>
        {
            DynValue.Object(new List<KeyValuePair<string, DynValue>>
            {
                new("Val", DynValue.Null())
            })
        });
        var csv = CsvFormatter.Format(rows, null);
        Assert.NotNull(csv);
    }

    [Fact]
    public void CsvFormatter_CustomDelimiter()
    {
        var rows = DynValue.Array(new List<DynValue>
        {
            DynValue.Object(new List<KeyValuePair<string, DynValue>>
            {
                new("A", DynValue.String("1")),
                new("B", DynValue.String("2"))
            })
        });
        var config = new TargetConfig { Options = new Dictionary<string, string> { { "delimiter", "|" } } };
        var csv = CsvFormatter.Format(rows, config);
        Assert.Contains("|", csv);
    }

    [Fact]
    public void CsvFormatter_BooleanOutput()
    {
        var rows = DynValue.Array(new List<DynValue>
        {
            DynValue.Object(new List<KeyValuePair<string, DynValue>>
            {
                new("Active", DynValue.Bool(true))
            })
        });
        var csv = CsvFormatter.Format(rows, null);
        Assert.Contains("true", csv);
    }

    // ═════════════════════════════════════════════════════════════════
    //  XML Formatter
    // ═════════════════════════════════════════════════════════════════

    [Fact]
    public void XmlFormatter_SimpleObject()
    {
        var obj = DynValue.Object(new List<KeyValuePair<string, DynValue>>
        {
            new("Root", DynValue.Object(new List<KeyValuePair<string, DynValue>>
            {
                new("Name", DynValue.String("Alice"))
            }))
        });
        var xml = XmlFormatter.Format(obj, null);
        Assert.Contains("<Root", xml);
        Assert.Contains("<Name>Alice</Name>", xml);
    }

    [Fact]
    public void XmlFormatter_NullElement()
    {
        var obj = DynValue.Object(new List<KeyValuePair<string, DynValue>>
        {
            new("Root", DynValue.Object(new List<KeyValuePair<string, DynValue>>
            {
                new("Val", DynValue.Null())
            }))
        });
        var xml = XmlFormatter.Format(obj, null);
        Assert.Contains("<Val odin:type=\"null\"></Val>", xml);
    }

    [Fact]
    public void XmlFormatter_NestedElements()
    {
        var inner = DynValue.Object(new List<KeyValuePair<string, DynValue>>
        {
            new("City", DynValue.String("Portland"))
        });
        var obj = DynValue.Object(new List<KeyValuePair<string, DynValue>>
        {
            new("Root", DynValue.Object(new List<KeyValuePair<string, DynValue>>
            {
                new("Address", inner)
            }))
        });
        var xml = XmlFormatter.Format(obj, null);
        Assert.Contains("<Address>", xml);
        Assert.Contains("<City>Portland</City>", xml);
    }

    [Fact]
    public void XmlFormatter_Escaping()
    {
        var obj = DynValue.Object(new List<KeyValuePair<string, DynValue>>
        {
            new("Root", DynValue.Object(new List<KeyValuePair<string, DynValue>>
            {
                new("Val", DynValue.String("A & B < C"))
            }))
        });
        var xml = XmlFormatter.Format(obj, null);
        Assert.Contains("&amp;", xml);
        Assert.Contains("&lt;", xml);
    }

    [Fact]
    public void XmlFormatter_XmlDeclaration()
    {
        var obj = DynValue.Object(new List<KeyValuePair<string, DynValue>>
        {
            new("Root", DynValue.Object(new List<KeyValuePair<string, DynValue>>
            {
                new("Val", DynValue.String("test"))
            }))
        });
        var xml = XmlFormatter.Format(obj, null);
        Assert.StartsWith("<?xml", xml);
    }

    [Fact]
    public void XmlFormatter_ArraySection()
    {
        var items = DynValue.Array(new List<DynValue>
        {
            DynValue.Object(new List<KeyValuePair<string, DynValue>> { new("Name", DynValue.String("Alice")) }),
            DynValue.Object(new List<KeyValuePair<string, DynValue>> { new("Name", DynValue.String("Bob")) })
        });
        var obj = DynValue.Object(new List<KeyValuePair<string, DynValue>>
        {
            new("Person", items)
        });
        var xml = XmlFormatter.Format(obj, null);
        Assert.Contains("Alice", xml);
        Assert.Contains("Bob", xml);
    }

    // ═════════════════════════════════════════════════════════════════
    //  Fixed-Width Formatter
    // ═════════════════════════════════════════════════════════════════

    [Fact]
    public void FixedWidthFormatter_BasicOutput()
    {
        var obj = DynValue.Object(new List<KeyValuePair<string, DynValue>>
        {
            new("Name", DynValue.String("Alice")),
            new("Age", DynValue.Integer(30))
        });
        var config = new TargetConfig
        {
            Format = "fixed-width",
            Options = new Dictionary<string, string> { { "columns", "Name:10;Age:5" } }
        };
        var result = FixedWidthFormatter.Format(obj, config);
        Assert.NotEmpty(result);
        Assert.Contains("Alice", result);
    }

    [Fact]
    public void FixedWidthFormatter_Truncation()
    {
        var obj = DynValue.Object(new List<KeyValuePair<string, DynValue>>
        {
            new("Name", DynValue.String("VeryLongNameThatShouldBeTruncated"))
        });
        var config = new TargetConfig
        {
            Format = "fixed-width",
            Options = new Dictionary<string, string> { { "columns", "Name:5" } }
        };
        var result = FixedWidthFormatter.Format(obj, config);
        // Output should be truncated
        Assert.True(result.TrimEnd('\n').Length <= 5);
    }

    [Fact]
    public void FixedWidthFormatter_RightAlignment()
    {
        var obj = DynValue.Object(new List<KeyValuePair<string, DynValue>>
        {
            new("Val", DynValue.Integer(42))
        });
        var config = new TargetConfig
        {
            Format = "fixed-width",
            Options = new Dictionary<string, string> { { "columns", "Val:10:right" } }
        };
        var result = FixedWidthFormatter.Format(obj, config);
        Assert.NotEmpty(result);
        var line = result.TrimEnd('\n');
        Assert.EndsWith("42", line);
    }

    // ═════════════════════════════════════════════════════════════════
    //  Flat Formatter
    // ═════════════════════════════════════════════════════════════════

    [Fact]
    public void FlatFormatter_SimpleKvp()
    {
        var obj = DynValue.Object(new List<KeyValuePair<string, DynValue>>
        {
            new("name", DynValue.String("Alice")),
            new("age", DynValue.Integer(30))
        });
        var result = FlatFormatter.Format(obj, null);
        Assert.Contains("name=Alice", result);
        Assert.Contains("age=30", result);
    }

    [Fact]
    public void FlatFormatter_Sorted()
    {
        var obj = DynValue.Object(new List<KeyValuePair<string, DynValue>>
        {
            new("z", DynValue.String("last")),
            new("a", DynValue.String("first"))
        });
        var result = FlatFormatter.Format(obj, null);
        int posA = result.IndexOf("a=");
        int posZ = result.IndexOf("z=");
        Assert.True(posA < posZ);
    }

    [Fact]
    public void FlatFormatter_Nested()
    {
        var inner = DynValue.Object(new List<KeyValuePair<string, DynValue>>
        {
            new("city", DynValue.String("Portland"))
        });
        var obj = DynValue.Object(new List<KeyValuePair<string, DynValue>>
        {
            new("address", inner)
        });
        var result = FlatFormatter.Format(obj, null);
        Assert.Contains("address.city=Portland", result);
    }

    [Fact]
    public void FlatFormatter_NullSkipped()
    {
        var obj = DynValue.Object(new List<KeyValuePair<string, DynValue>>
        {
            new("name", DynValue.String("Alice")),
            new("val", DynValue.Null())
        });
        var result = FlatFormatter.Format(obj, null);
        Assert.Contains("name=Alice", result);
        Assert.DoesNotContain("val=", result);
    }

    [Fact]
    public void FlatFormatter_BooleanValues()
    {
        var obj = DynValue.Object(new List<KeyValuePair<string, DynValue>>
        {
            new("active", DynValue.Bool(true))
        });
        var result = FlatFormatter.Format(obj, null);
        Assert.Contains("active=true", result);
    }

    [Fact]
    public void FlatFormatter_ArrayValues()
    {
        var arr = DynValue.Array(new List<DynValue>
        {
            DynValue.String("a"), DynValue.String("b")
        });
        var obj = DynValue.Object(new List<KeyValuePair<string, DynValue>>
        {
            new("items", arr)
        });
        var result = FlatFormatter.Format(obj, null);
        Assert.Contains("items[0]=a", result);
        Assert.Contains("items[1]=b", result);
    }

    // ═════════════════════════════════════════════════════════════════
    //  ODIN Formatter
    // ═════════════════════════════════════════════════════════════════

    [Fact]
    public void OdinFormatter_StringValue()
    {
        var obj = DynValue.Object(new List<KeyValuePair<string, DynValue>>
        {
            new("name", DynValue.String("Alice"))
        });
        var result = OdinFormatter.Format(obj, null);
        Assert.Contains("\"Alice\"", result);
    }

    [Fact]
    public void OdinFormatter_IntegerPrefix()
    {
        var obj = DynValue.Object(new List<KeyValuePair<string, DynValue>>
        {
            new("val", DynValue.Integer(42))
        });
        var result = OdinFormatter.Format(obj, null);
        Assert.Contains("##42", result);
    }

    [Fact]
    public void OdinFormatter_FloatPrefix()
    {
        var obj = DynValue.Object(new List<KeyValuePair<string, DynValue>>
        {
            new("val", DynValue.Float(3.14))
        });
        var result = OdinFormatter.Format(obj, null);
        Assert.Contains("#3.14", result);
    }

    [Fact]
    public void OdinFormatter_BooleanPrefix()
    {
        var obj = DynValue.Object(new List<KeyValuePair<string, DynValue>>
        {
            new("val", DynValue.Bool(true))
        });
        var result = OdinFormatter.Format(obj, null);
        Assert.Contains("?true", result);
    }

    [Fact]
    public void OdinFormatter_NullTilde()
    {
        var obj = DynValue.Object(new List<KeyValuePair<string, DynValue>>
        {
            new("val", DynValue.Null())
        });
        var result = OdinFormatter.Format(obj, null);
        Assert.Contains("~", result);
    }

    [Fact]
    public void OdinFormatter_SectionHeaders()
    {
        var inner = DynValue.Object(new List<KeyValuePair<string, DynValue>>
        {
            new("Name", DynValue.String("Alice")),
            new("Items", DynValue.Array(new List<DynValue> { DynValue.String("a") }))
        });
        var obj = DynValue.Object(new List<KeyValuePair<string, DynValue>>
        {
            new("Customer", inner)
        });
        var result = OdinFormatter.Format(obj, null);
        Assert.Contains("{$}", result);
    }

    [Fact]
    public void OdinFormatter_IncludesHeader()
    {
        var obj = DynValue.Object(new List<KeyValuePair<string, DynValue>>
        {
            new("val", DynValue.String("test"))
        });
        var result = OdinFormatter.Format(obj, null);
        Assert.Contains("{$}", result);
        Assert.Contains("odin = \"1.0.0\"", result);
    }

    [Fact]
    public void OdinFormatter_NoHeader()
    {
        var obj = DynValue.Object(new List<KeyValuePair<string, DynValue>>
        {
            new("val", DynValue.String("test"))
        });
        var config = new TargetConfig
        {
            Format = "odin",
            Options = new Dictionary<string, string> { { "header", "false" } }
        };
        var result = OdinFormatter.Format(obj, config);
        Assert.DoesNotContain("{$}", result);
    }

    [Fact]
    public void OdinFormatter_StringEscaping()
    {
        var obj = DynValue.Object(new List<KeyValuePair<string, DynValue>>
        {
            new("val", DynValue.String("hello\nworld"))
        });
        var result = OdinFormatter.Format(obj, null);
        Assert.Contains("\\n", result);
    }

    [Fact]
    public void OdinFormatter_ModifierPrefixes()
    {
        var obj = DynValue.Object(new List<KeyValuePair<string, DynValue>>
        {
            new("val", DynValue.String("secret"))
        });
        var mods = new Dictionary<string, OdinModifiers>
        {
            { "val", new OdinModifiers { Required = true, Confidential = true } }
        };
        var result = OdinFormatter.FormatWithModifiers(obj, null, mods);
        Assert.Contains("!", result);
        Assert.Contains("*", result);
    }
}
