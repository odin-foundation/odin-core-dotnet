using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Odin.Core;
using Odin.Core.Types;
using Xunit;

namespace Odin.Core.Tests.Golden;

// ─────────────────────────────────────────────────────────────────────────────
// Verified transform example corpus runner.
//
// Each fixture under GoldenData/transform-corpus/<family>/<id>.json stores a
// mapping block (transform), an ODIN source (input), and the exact engine
// produced ODIN (expectedOutput). The standard odin->odin header is prepended,
// the transform is executed, and Formatted is compared to expectedOutput.
//
// Error fixtures (family "error") assert the declared T-code surfaces, either
// thrown or reported in Errors/Warnings. Fixtures marked tsOnly are precision /
// format / RNG specific and are skipped here; each SDK pins its own value in a
// focused unit test.
// ─────────────────────────────────────────────────────────────────────────────

public class CorpusFixture
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("family")]
    public string Family { get; set; } = string.Empty;

    [JsonPropertyName("transform")]
    public string Transform { get; set; } = string.Empty;

    [JsonPropertyName("input")]
    public string Input { get; set; } = string.Empty;

    [JsonPropertyName("expectedOutput")]
    public string? ExpectedOutput { get; set; }

    [JsonPropertyName("targetFormat")]
    public string? TargetFormat { get; set; }

    [JsonPropertyName("targetOptions")]
    public Dictionary<string, JsonElement>? TargetOptions { get; set; }

    [JsonPropertyName("headerFields")]
    public Dictionary<string, JsonElement>? HeaderFields { get; set; }

    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("enforced")]
    public bool? Enforced { get; set; }

    [JsonPropertyName("tsOnly")]
    public bool TsOnly { get; set; }
}

[Trait("Category", "Golden")]
public class TransformCorpusTests : GoldenTestBase
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static string CorpusDir => Path.Combine(GetGoldenPath(), "transform-corpus");

    // Builds the transform header for a fixture. Source is always ODIN; the target
    // format defaults to odin and may be overridden by format-conversion idioms.
    private static string Raw(JsonElement e) =>
        e.ValueKind == JsonValueKind.String ? e.GetString() ?? "" : e.GetRawText();

    private static string BuildHeader(
        string targetFormat,
        Dictionary<string, JsonElement>? targetOptions,
        Dictionary<string, JsonElement>? headerFields)
    {
        var meta = new List<string>
        {
            "odin = \"1.0.0\"",
            "transform = \"1.0.0\"",
            $"direction = \"odin->{targetFormat}\"",
        };
        if (headerFields != null)
            foreach (var kv in headerFields)
                meta.Add($"{kv.Key} = {Raw(kv.Value)}");

        var target = new List<string> { $"format = \"{targetFormat}\"" };
        if (targetOptions != null)
            foreach (var kv in targetOptions)
                target.Add($"{kv.Key} = \"{Raw(kv.Value)}\"");

        var sb = new StringBuilder();
        sb.Append("{$}\n");
        sb.Append(string.Join("\n", meta)).Append("\n\n");
        sb.Append("{$source}\nformat = \"odin\"\n\n");
        sb.Append("{$target}\n");
        sb.Append(string.Join("\n", target)).Append("\n\n");
        return sb.ToString();
    }

    private static string HeaderFor(CorpusFixture f) =>
        BuildHeader(f.TargetFormat ?? "odin", f.TargetOptions, f.HeaderFields);

    public static IEnumerable<object[]> Fixtures()
    {
        string dir;
        try { dir = CorpusDir; }
        catch (DirectoryNotFoundException) { yield break; }
        if (!Directory.Exists(dir)) yield break;

        foreach (var family in Directory.GetDirectories(dir).OrderBy(p => p, StringComparer.Ordinal))
        {
            foreach (var file in Directory.GetFiles(family, "*.json").OrderBy(p => p, StringComparer.Ordinal))
            {
                if (Path.GetFileName(file).Equals("manifest.json", StringComparison.OrdinalIgnoreCase))
                    continue;
                yield return new object[] { file };
            }
        }
    }

    private static string Normalize(string s) => s.Replace("\r\n", "\n").TrimEnd();

    // Pulls a stable transform code off a thrown exception (Code, or Error.Code).
    private static string CodeOf(Exception ex)
    {
        var t = ex.GetType();
        var code = t.GetProperty("Code")?.GetValue(ex) as string;
        if (!string.IsNullOrEmpty(code)) return code!;
        var err = t.GetProperty("Error")?.GetValue(ex);
        if (err != null)
        {
            var ec = err.GetType().GetProperty("Code")?.GetValue(err) as string;
            if (!string.IsNullOrEmpty(ec)) return ec!;
        }
        return "";
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void Corpus(string file)
    {
        var fixture = JsonSerializer.Deserialize<CorpusFixture>(File.ReadAllText(file), Options)!;

        // Precision/format/RNG-specific fixtures are skipped here; each SDK pins its
        // own value in a focused unit test.
        if (fixture.TsOnly)
            return;

        var transformText = HeaderFor(fixture) + fixture.Transform;

        if (fixture.Family == "error")
        {
            if (fixture.Enforced == false) return;
            var code = fixture.Code!;
            var surfaced = new StringBuilder();
            try
            {
                var result = Core.Odin.ExecuteTransform(transformText, fixture.Input);
                foreach (var e in result.Errors) surfaced.Append(e.Code).Append(' ').Append(e.Message).Append('\n');
                foreach (var w in result.Warnings) surfaced.Append(w.Code).Append(' ').Append(w.Message).Append('\n');
            }
            catch (OdinParseException ex)
            {
                surfaced.Append(ex.Code).Append(' ').Append(ex.Message);
            }
            catch (Exception ex)
            {
                surfaced.Append(CodeOf(ex)).Append(' ').Append(ex.Message);
            }
            Assert.True(surfaced.ToString().Contains(code),
                $"{code} not surfaced in {file}: {surfaced}");
            return;
        }

        var res = Core.Odin.ExecuteTransform(transformText, fixture.Input);
        Assert.True(res.Errors.Count == 0,
            $"errors in {file}: {string.Join("; ", res.Errors.Select(e => $"{e.Code} {e.Message}"))}");

        var actual = Normalize(res.Formatted ?? "");
        var expected = Normalize(fixture.ExpectedOutput ?? "");
        Assert.True(actual == expected,
            $"{file}\n--- expected ---\n{expected}\n--- actual ---\n{actual}");
    }
}
