using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Odin.Core;
using Odin.Core.Transform;
using Odin.Core.Types;
using Xunit;
using Xunit.Abstractions;

namespace Odin.Core.Tests.Golden;

// ─────────────────────────────────────────────────────────────────────────────
// Manifest DTOs for end-to-end tests
// ─────────────────────────────────────────────────────────────────────────────

public class E2EMainManifest
{
    [JsonPropertyName("suite")] public string Suite { get; set; } = "";
    [JsonPropertyName("version")] public string Version { get; set; } = "";
    [JsonPropertyName("categories")] public List<E2ECategoryRef> Categories { get; set; } = new();
}

public class E2ECategoryRef
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("path")] public string Path { get; set; } = "";
}

public class E2ECategoryManifest
{
    [JsonPropertyName("category")] public string Category { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("tests")] public List<E2ETestDef> Tests { get; set; } = new();
}

public class E2ETestDef
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("description")] public string Description { get; set; } = "";
    [JsonPropertyName("direction")] public string? Direction { get; set; }
    [JsonPropertyName("input")] public string Input { get; set; } = "";
    [JsonPropertyName("transform")] public string? Transform { get; set; }
    [JsonPropertyName("expected")] public string Expected { get; set; } = "";
    [JsonPropertyName("importTransform")] public string? ImportTransform { get; set; }
    [JsonPropertyName("exportTransform")] public string? ExportTransform { get; set; }
    [JsonPropertyName("intermediate")] public string? Intermediate { get; set; }
    [JsonPropertyName("method")] public string? Method { get; set; }
    [JsonPropertyName("options")] public E2EOptions? Options { get; set; }
}

public class E2EOptions
{
    [JsonPropertyName("preserveTypes")] public bool PreserveTypes { get; set; }
    [JsonPropertyName("preserveModifiers")] public bool PreserveModifiers { get; set; }
}

/// <summary>
/// Golden end-to-end tests. Reads manifest-based test definitions from
/// GoldenData/end-to-end/ and runs import, export, roundtrip, and
/// direct export tests.
/// </summary>
[Trait("Category", "Golden")]
public class EndToEndTests : GoldenTestBase
{
    private readonly ITestOutputHelper _output;

    public EndToEndTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    [Fact]
    public void GoldenEndToEndTests_RunAll()
    {
        var e2eDir = Path.Combine(GetGoldenPath(), "end-to-end");
        if (!Directory.Exists(e2eDir))
        {
            _output.WriteLine("end-to-end directory not found, skipping");
            return;
        }

        var mainManifestPath = Path.Combine(e2eDir, "manifest.json");
        if (!File.Exists(mainManifestPath))
        {
            _output.WriteLine("Main manifest not found, skipping");
            return;
        }

        var mainManifest = JsonSerializer.Deserialize<E2EMainManifest>(
            File.ReadAllText(mainManifestPath), JsonOpts)!;

        int passed = 0;
        int failed = 0;
        var failures = new List<string>();

        foreach (var cat in mainManifest.Categories)
        {
            var catDir = Path.Combine(e2eDir, cat.Path);
            var catManifestPath = Path.Combine(catDir, "manifest.json");
            if (!File.Exists(catManifestPath))
            {
                _output.WriteLine($"  SKIP category '{cat.Id}': manifest not found");
                continue;
            }

            var catManifest = JsonSerializer.Deserialize<E2ECategoryManifest>(
                File.ReadAllText(catManifestPath), JsonOpts)!;

            foreach (var test in catManifest.Tests)
            {
                try
                {
                    if (test.Method != null)
                    {
                        RunDirectExportTest(test, catDir);
                    }
                    else if (test.ImportTransform != null && test.ExportTransform != null)
                    {
                        RunRoundtripTest(test, catDir);
                    }
                    else if (test.Transform != null)
                    {
                        RunTransformTest(test, catDir);
                    }
                    else
                    {
                        _output.WriteLine($"  SKIP [{test.Id}]: no transform/method specified");
                        continue;
                    }

                    passed++;
                    _output.WriteLine($"  PASS [{test.Id}]: {test.Description}");
                }
                catch (Exception ex)
                {
                    failed++;
                    var msg = $"  FAIL [{test.Id}]: {ex.Message}";
                    _output.WriteLine(msg);
                    failures.Add(msg);
                }
            }
        }

        _output.WriteLine($"\nEnd-to-end: {passed} passed, {failed} failed");

        if (failures.Count > 0)
        {
            Assert.Fail($"End-to-end tests had {failed} failures:\n{string.Join("\n", failures)}");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test runners
    // ─────────────────────────────────────────────────────────────────────────

    private void RunTransformTest(E2ETestDef test, string catDir)
    {
        var inputRaw = ReadAndNormalize(Path.Combine(catDir, test.Input));
        var transformText = ReadAndNormalize(Path.Combine(catDir, test.Transform!));
        var expected = ReadAndNormalize(Path.Combine(catDir, test.Expected));

        var transform = Core.Odin.ParseTransform(transformText);
        var direction = test.Direction ?? "odin->odin";
        var srcFmt = SourceFormat(direction);
        var input = ParseInput(inputRaw, srcFmt);

        var result = TransformEngine.Execute(transform, input);

        if (result.Errors.Count > 0)
        {
            var errMsgs = string.Join(", ", result.Errors.Select(e => $"[{e.Code}] {e.Message}"));
            throw new Exception($"Transform failed: {errMsgs}");
        }

        var formatted = NormalizeLineEndings(result.Formatted ?? "");
        var expectedNorm = NormalizeLineEndings(expected);

        Assert.True(formatted == expectedNorm,
            $"[{test.Id}] Formatted output mismatch:\n--- expected ---\n{expectedNorm}\n--- actual ---\n{formatted}");
    }

    private void RunDirectExportTest(E2ETestDef test, string catDir)
    {
        var inputText = ReadAndNormalize(Path.Combine(catDir, test.Input));
        var expected = ReadAndNormalize(Path.Combine(catDir, test.Expected));

        var doc = Core.Odin.Parse(inputText);
        var preserveTypes = test.Options?.PreserveTypes ?? true;
        var preserveModifiers = test.Options?.PreserveModifiers ?? true;

        string actual;
        switch (test.Method)
        {
            case "toJSON":
                actual = Core.Odin.ToJson(doc, preserveTypes, preserveModifiers);
                break;
            case "toXML":
                actual = Core.Odin.ToXml(doc, preserveTypes, preserveModifiers);
                break;
            default:
                throw new Exception($"Unknown export method: {test.Method}");
        }

        var normExpected = NormalizeLineEndings(expected);
        var normActual = NormalizeLineEndings(actual);

        Assert.True(normActual == normExpected,
            $"[{test.Id}] {test.Method} output mismatch:\n--- expected ---\n{normExpected}\n--- actual ---\n{normActual}");
    }

    private void RunRoundtripTest(E2ETestDef test, string catDir)
    {
        var inputRaw = ReadAndNormalize(Path.Combine(catDir, test.Input));
        var importTransformText = ReadAndNormalize(Path.Combine(catDir, test.ImportTransform!));
        var exportTransformText = ReadAndNormalize(Path.Combine(catDir, test.ExportTransform!));
        var expected = ReadAndNormalize(Path.Combine(catDir, test.Expected));

        // Step 1: Import
        var importTransform = Core.Odin.ParseTransform(importTransformText);
        var direction = test.Direction ?? "fixed-width->fixed-width";
        var srcFmt = SourceFormat(direction);
        var input = ParseInput(inputRaw, srcFmt);

        var importResult = TransformEngine.Execute(importTransform, input);
        if (importResult.Errors.Count > 0)
        {
            throw new Exception($"Import transform failed: {string.Join(", ", importResult.Errors.Select(e => e.Message))}");
        }

        var importOutput = importResult.Output ?? DynValue.Null();

        // Step 2: Export
        var exportTransform = Core.Odin.ParseTransform(exportTransformText);
        var exportResult = TransformEngine.Execute(exportTransform, importOutput);
        if (exportResult.Errors.Count > 0)
        {
            throw new Exception($"Export transform failed: {string.Join(", ", exportResult.Errors.Select(e => e.Message))}");
        }

        var formatted = NormalizeLineEndings(exportResult.Formatted ?? "");
        var expectedNorm = NormalizeLineEndings(expected);

        Assert.True(formatted == expectedNorm,
            $"[{test.Id}] Roundtrip output mismatch:\n--- expected ---\n{expectedNorm}\n--- actual ---\n{formatted}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static string SourceFormat(string direction)
    {
        var parts = direction.Split(new[] { "->" }, StringSplitOptions.None);
        return parts.Length > 0 ? parts[0] : "odin";
    }

    private static DynValue ParseInput(string raw, string format)
    {
        switch (format)
        {
            case "json":
                return JsonSourceParser.Parse(raw);
            case "xml":
                return XmlSourceParser.Parse(raw);
            case "yaml":
                return YamlSourceParser.Parse(raw);
            case "flat":
            case "properties":
            case "flat-kvp":
                return FlatSourceParser.Parse(raw);
            case "odin":
            {
                var doc = Core.Odin.Parse(raw);
                return OdinDocToDyn(doc);
            }
            case "fixed-width":
            case "csv":
            case "delimited":
                // Pass raw string — engine handles multi-record splitting
                return DynValue.String(raw);
            default:
                return DynValue.String(raw);
        }
    }

    private static DynValue OdinDocToDyn(OdinDocument doc)
    {
        var entries = new List<KeyValuePair<string, DynValue>>();
        foreach (var kv in doc.Assignments)
        {
            if (kv.Key.StartsWith("$", StringComparison.Ordinal)) continue;
            var dynVal = TransformEngine.OdinValueToDyn(kv.Value);
            SetNestedPath(entries, kv.Key, dynVal);
        }
        return DynValue.Object(entries);
    }

    private static void SetNestedPath(List<KeyValuePair<string, DynValue>> root, string path, DynValue value)
    {
        var segments = path.Split('.');
        SetNestedRecursive(root, segments, 0, value);
    }

    private static void SetNestedRecursive(List<KeyValuePair<string, DynValue>> entries, string[] segments, int idx, DynValue value)
    {
        if (idx >= segments.Length) return;

        string seg = segments[idx];
        bool isLast = idx == segments.Length - 1;

        // Check for array index: key[N]
        int bracketPos = seg.IndexOf('[');
        if (bracketPos >= 0)
        {
            string key = seg.Substring(0, bracketPos);
            string idxStr = seg.Substring(bracketPos + 1, seg.Length - bracketPos - 2);
            if (int.TryParse(idxStr, out int arrIdx))
            {
                // Find or create array
                int pos = FindEntry(entries, key);
                if (pos < 0)
                {
                    entries.Add(new KeyValuePair<string, DynValue>(key, DynValue.Array(new List<DynValue>())));
                    pos = entries.Count - 1;
                }
                var arr = entries[pos].Value.AsArray();
                if (arr == null)
                {
                    arr = new List<DynValue>();
                    entries[pos] = new KeyValuePair<string, DynValue>(key, DynValue.Array(arr));
                }
                while (arr.Count <= arrIdx)
                    arr.Add(isLast ? DynValue.Null() : DynValue.Object(new List<KeyValuePair<string, DynValue>>()));
                if (isLast)
                {
                    arr[arrIdx] = value;
                }
                else
                {
                    var inner = arr[arrIdx].AsObject();
                    if (inner == null)
                    {
                        inner = new List<KeyValuePair<string, DynValue>>();
                        arr[arrIdx] = DynValue.Object(inner);
                    }
                    SetNestedRecursive(inner, segments, idx + 1, value);
                }
                return;
            }
        }

        if (isLast)
        {
            entries.Add(new KeyValuePair<string, DynValue>(seg, value));
        }
        else
        {
            int pos = FindEntry(entries, seg);
            if (pos >= 0)
            {
                var obj = entries[pos].Value.AsObject();
                if (obj != null)
                {
                    SetNestedRecursive(obj, segments, idx + 1, value);
                }
            }
            else
            {
                var obj = new List<KeyValuePair<string, DynValue>>();
                entries.Add(new KeyValuePair<string, DynValue>(seg, DynValue.Object(obj)));
                SetNestedRecursive(obj, segments, idx + 1, value);
            }
        }
    }

    private static int FindEntry(List<KeyValuePair<string, DynValue>> entries, string key)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].Key == key) return i;
        }
        return -1;
    }

    private static string ReadAndNormalize(string path)
    {
        return File.ReadAllText(path).Replace("\r\n", "\n");
    }

    private static string NormalizeLineEndings(string text)
    {
        return text.Replace("\r\n", "\n").TrimEnd();
    }
}
