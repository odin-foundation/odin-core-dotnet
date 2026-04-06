#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Odin.Core;
using Odin.Core.Transform;
using Odin.Core.Types;
using Xunit;

namespace Odin.Core.Tests.Golden;

/// <summary>
/// JSON Import Validation Tests.
/// Validates that the transform framework correctly parses and handles
/// all JSON input types. Mirrors the TypeScript json-import.test.ts tests.
/// </summary>
[Trait("Category", "Golden")]
public class JsonImportTests
{
    // ── Helpers ──

    private static string CreateFieldTransform(string fieldPath) =>
        $"{{$}}\nodin = \"1.0.0\"\ntransform = \"1.0.0\"\ndirection = \"json->json\"\n\n{{output}}\nresult = \"@.{fieldPath}\"\n";

    private static TransformResult Exec(string transformText, object input)
    {
        var transform = Core.Odin.ParseTransform(transformText);
        return TransformEngine.Execute(transform, input);
    }

    private static DynValue? GetOutputValue(TransformResult result)
    {
        Assert.NotNull(result.Output);
        var output = result.Output!.Get("output");
        Assert.NotNull(output);
        return output!.Get("result");
    }

    private static string GetOutputType(TransformResult result)
    {
        var val = GetOutputValue(result);
        Assert.NotNull(val);
        return val!.Type.ToString().ToLowerInvariant();
    }

    // ── Primitive Types ──

    [Fact] public void ParsesStringValues()
    {
        var result = Exec(CreateFieldTransform("value"), new { value = "hello world" });
        Assert.True(result.Success);
        Assert.Equal("string", GetOutputType(result));
        Assert.Equal("hello world", GetOutputValue(result)!.AsString());
    }

    [Fact] public void ParsesEmptyString()
    {
        var result = Exec(CreateFieldTransform("value"), new { value = "" });
        Assert.True(result.Success);
        Assert.Equal("string", GetOutputType(result));
        Assert.Equal("", GetOutputValue(result)!.AsString());
    }

    [Fact] public void ParsesIntegerValues()
    {
        var result = Exec(CreateFieldTransform("value"), new { value = 42 });
        Assert.True(result.Success);
        Assert.Equal("integer", GetOutputType(result));
        Assert.Equal(42L, GetOutputValue(result)!.AsInt64());
    }

    [Fact] public void ParsesNegativeIntegerValues()
    {
        var result = Exec(CreateFieldTransform("value"), new { value = -100 });
        Assert.True(result.Success);
        Assert.Equal("integer", GetOutputType(result));
        Assert.Equal(-100L, GetOutputValue(result)!.AsInt64());
    }

    [Fact] public void ParsesZero()
    {
        var result = Exec(CreateFieldTransform("value"), new { value = 0 });
        Assert.True(result.Success);
        Assert.Equal("integer", GetOutputType(result));
        Assert.Equal(0L, GetOutputValue(result)!.AsInt64());
    }

    [Fact] public void ParsesFloatingPointValues()
    {
        var result = Exec(CreateFieldTransform("value"), new { value = 3.14159 });
        Assert.True(result.Success);
        Assert.Equal("float", GetOutputType(result));
        Assert.Equal(3.14159, GetOutputValue(result)!.AsDouble()!.Value, 5);
    }

    [Fact] public void ParsesNegativeFloatingPointValues()
    {
        var result = Exec(CreateFieldTransform("value"), new { value = -99.99 });
        Assert.True(result.Success);
        Assert.Equal("float", GetOutputType(result));
        Assert.Equal(-99.99, GetOutputValue(result)!.AsDouble()!.Value, 2);
    }

    [Fact] public void ParsesBooleanTrue()
    {
        var result = Exec(CreateFieldTransform("value"), new { value = true });
        Assert.True(result.Success);
        Assert.Equal("bool", GetOutputType(result));
        Assert.True(GetOutputValue(result)!.AsBool());
    }

    [Fact] public void ParsesBooleanFalse()
    {
        var result = Exec(CreateFieldTransform("value"), new { value = false });
        Assert.True(result.Success);
        Assert.Equal("bool", GetOutputType(result));
        Assert.False(GetOutputValue(result)!.AsBool());
    }

    [Fact] public void ParsesNullValues()
    {
        // null values need special handling — use JsonDocument directly
        using var doc = JsonDocument.Parse("{\"value\": null}");
        var dv = DynValue.FromJsonElement(doc.RootElement);
        var transform = Core.Odin.ParseTransform(CreateFieldTransform("value"));
        var result = TransformEngine.Execute(transform, dv);
        Assert.True(result.Success);
        Assert.Equal("null", GetOutputType(result));
    }

    // ── Nested Objects ──

    [Fact] public void ParsesNestedObjectFields()
    {
        var result = Exec(CreateFieldTransform("person.name"),
            new { person = new { name = "John", age = 30 } });
        Assert.True(result.Success);
        Assert.Equal("John", GetOutputValue(result)!.AsString());
    }

    [Fact] public void ParsesDeeplyNestedObjectFields()
    {
        var result = Exec(CreateFieldTransform("level1.level2.level3.value"),
            new { level1 = new { level2 = new { level3 = new { value = "deep" } } } });
        Assert.True(result.Success);
        Assert.Equal("deep", GetOutputValue(result)!.AsString());
    }

    [Fact] public void HandlesEmptyObject()
    {
        using var doc = JsonDocument.Parse("{\"obj\": {}}");
        var dv = DynValue.FromJsonElement(doc.RootElement);
        var transform = Core.Odin.ParseTransform(CreateFieldTransform("obj"));
        var result = TransformEngine.Execute(transform, dv);
        Assert.True(result.Success);
    }

    // ── Arrays ──

    [Fact] public void ParsesArrayOfStrings()
    {
        string transformText = "{$}\nodin = \"1.0.0\"\ntransform = \"1.0.0\"\ndirection = \"json->json\"\n\n{output}\ncount = \"%count @.items\"\nfirst = \"%first @.items\"\n";
        var result = Exec(transformText, new { items = new[] { "a", "b", "c" } });
        Assert.True(result.Success);
        var output = result.Output!.Get("output")!;
        Assert.Equal(3L, output.Get("count")!.AsInt64());
        Assert.Equal("a", output.Get("first")!.AsString());
    }

    [Fact] public void ParsesArrayOfNumbers()
    {
        string transformText = "{$}\nodin = \"1.0.0\"\ntransform = \"1.0.0\"\ndirection = \"json->json\"\n\n{output}\nsum = \"%sum @.numbers\"\navg = \"%avg @.numbers\"\n";
        var result = Exec(transformText, new { numbers = new[] { 10, 20, 30 } });
        Assert.True(result.Success);
        var output = result.Output!.Get("output")!;
        Assert.Equal(60.0, output.Get("sum")!.AsDouble()!.Value, 3);
        Assert.Equal(20.0, output.Get("avg")!.AsDouble()!.Value, 3);
    }

    [Fact] public void ParsesArrayOfObjects()
    {
        string transformText = "{$}\nodin = \"1.0.0\"\ntransform = \"1.0.0\"\ndirection = \"json->json\"\n\n{output}\ncount = \"%count @.users\"\nfirst = \"%first @.users\"\n";
        var result = Exec(transformText, new { users = new[] { new { name = "Alice", age = 25 }, new { name = "Bob", age = 30 } } });
        Assert.True(result.Success);
        var output = result.Output!.Get("output")!;
        Assert.Equal(2L, output.Get("count")!.AsInt64());
        // Verify the first element is an object with expected fields
        var first = output.Get("first")!;
        Assert.Equal("Alice", first.Get("name")!.AsString());
    }

    [Fact] public void HandlesEmptyArray()
    {
        string transformText = "{$}\nodin = \"1.0.0\"\ntransform = \"1.0.0\"\ndirection = \"json->json\"\n\n{output}\ncount = \"%count @.items\"\n";
        var result = Exec(transformText, new { items = Array.Empty<int>() });
        Assert.True(result.Success);
        var output = result.Output!.Get("output")!;
        Assert.Equal(0L, output.Get("count")!.AsInt64());
    }

    [Fact] public void ParsesMixedTypeArray()
    {
        string transformText = "{$}\nodin = \"1.0.0\"\ntransform = \"1.0.0\"\ndirection = \"json->json\"\n\n{output}\ncount = \"%count @.mixed\"\n";
        using var doc = JsonDocument.Parse("{\"mixed\": [1, \"two\", true, null]}");
        var dv = DynValue.FromJsonElement(doc.RootElement);
        var transform = Core.Odin.ParseTransform(transformText);
        var result = TransformEngine.Execute(transform, dv);
        Assert.True(result.Success);
        var output = result.Output!.Get("output")!;
        Assert.Equal(4L, output.Get("count")!.AsInt64());
    }

    // ── Special Characters ──

    [Fact] public void ParsesStringsWithUnicode()
    {
        var result = Exec(CreateFieldTransform("text"), new { text = "\u65E5\u672C\u8A9E\u30C6\u30B9\u30C8" });
        Assert.True(result.Success);
        Assert.Equal("\u65E5\u672C\u8A9E\u30C6\u30B9\u30C8", GetOutputValue(result)!.AsString());
    }

    [Fact] public void ParsesStringsWithEmoji()
    {
        var result = Exec(CreateFieldTransform("text"), new { text = "Hello \uD83D\uDC4B World \uD83C\uDF0D" });
        Assert.True(result.Success);
        Assert.Equal("Hello \uD83D\uDC4B World \uD83C\uDF0D", GetOutputValue(result)!.AsString());
    }

    [Fact] public void ParsesStringsWithNewlines()
    {
        var result = Exec(CreateFieldTransform("text"), new { text = "line1\nline2\nline3" });
        Assert.True(result.Success);
        Assert.Equal("line1\nline2\nline3", GetOutputValue(result)!.AsString());
    }

    [Fact] public void ParsesStringsWithSpecialJsonCharacters()
    {
        var result = Exec(CreateFieldTransform("text"), new { text = "quotes: \"test\" and backslash: \\" });
        Assert.True(result.Success);
        Assert.Equal("quotes: \"test\" and backslash: \\", GetOutputValue(result)!.AsString());
    }

    // ── Edge Cases ──

    [Fact] public void HandlesLargeIntegers()
    {
        var result = Exec(CreateFieldTransform("value"), new { value = 9007199254740991L });
        Assert.True(result.Success);
        Assert.Equal(9007199254740991L, GetOutputValue(result)!.AsInt64());
    }

    [Fact] public void HandlesVerySmallFloatingPoint()
    {
        var result = Exec(CreateFieldTransform("value"), new { value = 0.000001 });
        Assert.True(result.Success);
        Assert.Equal(0.000001, GetOutputValue(result)!.AsDouble()!.Value, 6);
    }

    [Fact] public void HandlesScientificNotation()
    {
        var result = Exec(CreateFieldTransform("value"), new { value = 1.5e10 });
        Assert.True(result.Success);
        Assert.Equal(1.5e10, GetOutputValue(result)!.AsDouble()!.Value, 0);
    }

    [Fact] public void HandlesVeryLongStrings()
    {
        string longString = new string('a', 10000);
        var result = Exec(CreateFieldTransform("value"), new { value = longString });
        Assert.True(result.Success);
        Assert.Equal(longString, GetOutputValue(result)!.AsString());
    }

    [Fact] public void HandlesLargeArrays()
    {
        var largeArray = new int[1000];
        for (int i = 0; i < 1000; i++) largeArray[i] = i;

        string transformText = "{$}\nodin = \"1.0.0\"\ntransform = \"1.0.0\"\ndirection = \"json->json\"\n\n{output}\ncount = \"%count @.items\"\nsum = \"%sum @.items\"\n";
        var result = Exec(transformText, new { items = largeArray });
        Assert.True(result.Success);
        var output = result.Output!.Get("output")!;
        Assert.Equal(1000L, output.Get("count")!.AsInt64());
        Assert.Equal(499500.0, output.Get("sum")!.AsDouble()!.Value, 3);
    }

    // ── Type Preservation ──

    [Fact] public void DistinguishesIntegerFromFloat()
    {
        var resultInt = Exec(CreateFieldTransform("value"), new { value = 42 });
        var resultFloat = Exec(CreateFieldTransform("value"), new { value = 42.5 });

        Assert.Equal("integer", GetOutputType(resultInt));
        Assert.Equal("float", GetOutputType(resultFloat));
    }

    [Fact] public void PreservesTypeThroughTransformations()
    {
        string transformText = "{$}\nodin = \"1.0.0\"\ntransform = \"1.0.0\"\ndirection = \"json->json\"\n\n{output}\nstr = \"@.strVal\"\nnum = \"@.numVal\"\nbool = \"@.boolVal\"\n";
        var result = Exec(transformText, new { strVal = "test", numVal = 123, boolVal = true });
        Assert.True(result.Success);
        var output = result.Output!.Get("output")!;
        Assert.Equal("string", output.Get("str")!.Type.ToString().ToLowerInvariant());
        Assert.Equal("integer", output.Get("num")!.Type.ToString().ToLowerInvariant());
        Assert.Equal("bool", output.Get("bool")!.Type.ToString().ToLowerInvariant());
    }

    // ── Golden File–Driven Tests ──

    public static IEnumerable<object[]> GoldenJsonImportTestCases()
    {
        var goldenPath = GoldenTestBase.GetGoldenPath();
        var importDir = Path.Combine(goldenPath, "json-import");
        if (!Directory.Exists(importDir)) yield break;

        var jsonOpts = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        foreach (var jsonFile in Directory.GetFiles(importDir, "*.json").OrderBy(f => f))
        {
            if (Path.GetFileName(jsonFile) == "manifest.json") continue;
            var content = File.ReadAllText(jsonFile);
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;
            var suiteName = root.TryGetProperty("suite", out var s) ? s.GetString() ?? "" : "";
            if (!root.TryGetProperty("tests", out var tests)) continue;

            foreach (var test in tests.EnumerateArray())
            {
                var testId = test.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "";
                yield return new object[] { $"{suiteName}/{testId}", jsonFile, testId };
            }
        }
    }

    [Theory]
    [MemberData(nameof(GoldenJsonImportTestCases))]
    public void GoldenJsonImportTest(string displayName, string filePath, string testId)
    {
        var content = File.ReadAllText(filePath);
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;
        var tests = root.GetProperty("tests");

        JsonElement? testCase = null;
        foreach (var t in tests.EnumerateArray())
        {
            if (t.TryGetProperty("id", out var tid) && tid.GetString() == testId)
            {
                testCase = t;
                break;
            }
        }
        Assert.NotNull(testCase);
        var tc = testCase.Value;

        var transformText = tc.GetProperty("transform").GetString()!;
        var inputJson = tc.GetProperty("input").GetRawText();
        using var inputDoc = JsonDocument.Parse(inputJson);
        var source = DynValue.FromJsonElement(inputDoc.RootElement);

        var result = Exec(transformText, source);
        Assert.True(result.Success, $"[{testId}] Transform failed");

        if (tc.TryGetProperty("expected", out var expected) &&
            expected.TryGetProperty("output", out var expectedOutput))
        {
            var outputSeg = result.Output?.Get("output");
            Assert.NotNull(outputSeg);
            AssertGoldenValueMatches(outputSeg!, expectedOutput, "output");
        }
    }

    private static void AssertGoldenValueMatches(DynValue actual, JsonElement expected, string path)
    {
        if (expected.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in expected.EnumerateObject())
            {
                var field = actual.Get(prop.Name);
                Assert.NotNull(field);
                AssertGoldenValueMatches(field!, prop.Value, $"{path}.{prop.Name}");
            }
        }
        else if (expected.ValueKind == JsonValueKind.Null)
        {
            Assert.True(actual.IsNull, $"Expected null at {path}");
        }
        else if (expected.ValueKind == JsonValueKind.True || expected.ValueKind == JsonValueKind.False)
        {
            Assert.Equal(expected.GetBoolean(), actual.AsBool());
        }
        else if (expected.ValueKind == JsonValueKind.Number)
        {
            Assert.Equal(expected.GetDouble(), actual.Type == DynValueType.Integer ? (double)actual.AsInt64()!.Value : actual.AsDouble()!.Value, 5);
        }
        else if (expected.ValueKind == JsonValueKind.String)
        {
            Assert.Equal(expected.GetString(), actual.AsString());
        }
    }
}
