#nullable enable
using System;
using System.Linq;
using Odin.Core.Transform;
using Odin.Core.Types;
using Xunit;

namespace Odin.Core.Tests.Unit;

/// <summary>
/// Transform engine enforcement gaps: stable error codes (T001, T003, T005,
/// T006, T008, T009), onMissing policy for source fields, and @import resolution.
/// </summary>
public class TransformEnforcementGapsTests
{
    // ── Helpers ──

    private static string Header(string format, params (string Key, string Value)[] target)
    {
        var t = string.Join("\n", target.Select(kv => $"{kv.Key} = \"{kv.Value}\""));
        var targetSection = $"format = \"{format}\"" + (t.Length > 0 ? "\n" + t : "");
        return "{$}\nodin = \"1.0.0\"\ntransform = \"1.0.0\"\n"
            + $"direction = \"odin->{format}\"\n\n{{$source}}\nformat = \"odin\"\n\n"
            + $"{{$target}}\n{targetSection}\n\n";
    }

    private static TransformResult Run(
        string transform, string input, string format = "odin",
        (string Key, string Value)[]? target = null)
    {
        var text = Header(format, target ?? Array.Empty<(string, string)>()) + transform;
        var t = Core.Odin.ParseTransform(text);
        var doc = Core.Odin.Parse(input);
        return TransformEngine.ExecuteDocument(t, doc);
    }

    // ─────────────────────────────────────────────────────────────────
    // T001 — unknown verb
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void T001_EmitsForUnknownBuiltInVerb()
    {
        var r = Run("{out}\nx = %notAVerb @.a", "a = ##1");
        Assert.False(r.Success);
        Assert.Equal("T001", r.Errors[0].Code);
        Assert.Equal("x", r.Errors[0].Path);
    }

    [Fact]
    public void T001_DoesNotRaiseForUnregisteredCustomVerb()
    {
        var r = Run("{out}\nx = %&my.thing @.a", "a = \"v\"");
        Assert.True(r.Success);
        Assert.Empty(r.Errors);
    }

    [Fact]
    public void T001_DemotesToWarningUnderOnErrorWarn()
    {
        var r = Run("{out}\nx = %notAVerb @.a", "a = ##1",
            target: new[] { ("onError", "warn") });
        Assert.True(r.Success);
        Assert.Contains(r.Warnings, w => w.Code == "T001");
    }

    // ─────────────────────────────────────────────────────────────────
    // T003 — lookup table not found
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void T003_EmitsWhenTableUndeclaredAndOnMissingFail()
    {
        var r = Run("{out}\nx = %lookup \"GHOST.code\" @.k", "k = \"active\"",
            target: new[] { ("onMissing", "fail") });
        Assert.False(r.Success);
        Assert.Equal("T003", r.Errors[0].Code);
    }

    [Fact]
    public void T003_StaysSilentUnderDefaultPolicy()
    {
        var r = Run("{out}\nx = %lookup \"GHOST.code\" @.k", "k = \"active\"");
        Assert.True(r.Success);
        Assert.Empty(r.Errors);
    }

    [Fact]
    public void T003_DemotesToWarningUnderOnMissingWarn()
    {
        var r = Run("{out}\nx = %lookup \"GHOST.code\" @.k", "k = \"active\"",
            target: new[] { ("onMissing", "warn") });
        Assert.True(r.Success);
        Assert.Contains(r.Warnings, w => w.Code == "T003");
    }

    [Fact]
    public void T004_StillEmittedForMissingKeyInDeclaredTable()
    {
        var transform =
            "{$table.T[name, code]}\n\"foo\", ##1\n\n{out}\nx = %lookup \"T.code\" @.k";
        var r = Run(transform, "k = \"bar\"", target: new[] { ("onMissing", "fail") });
        Assert.False(r.Success);
        Assert.Equal("T004", r.Errors[0].Code);
    }

    // ─────────────────────────────────────────────────────────────────
    // T005 — source path not found / onMissing
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void T005_EmitsWhenRequiredSourcePathAbsent()
    {
        var r = Run("{out}\nx = @.does.not.exist :required", "a = ##1");
        Assert.False(r.Success);
        Assert.Equal("T005", r.Errors[0].Code);
    }

    [Fact]
    public void T005_EmitsForAbsentPathUnderOnMissingFailWithoutRequired()
    {
        var r = Run("{out}\nx = @.does.not.exist", "a = ##1",
            target: new[] { ("onMissing", "fail") });
        Assert.False(r.Success);
        Assert.Equal("T005", r.Errors[0].Code);
    }

    [Fact]
    public void T005_WarnsForAbsentPathUnderOnMissingWarn()
    {
        var r = Run("{out}\nx = @.does.not.exist", "a = ##1",
            target: new[] { ("onMissing", "warn") });
        Assert.True(r.Success);
        Assert.Contains(r.Warnings, w => w.Code == "T005");
    }

    [Fact]
    public void T005_StaysSilentForAbsentPathUnderDefaultSkipPolicy()
    {
        var r = Run("{out}\nx = @.does.not.exist", "a = ##1");
        Assert.True(r.Success);
        Assert.Empty(r.Errors);
    }

    [Fact]
    public void T005_PresentNullRequiredFieldIsSourceMissingNotT005()
    {
        var r = Run("{out}\nx = @.a :required", "a = ~");
        Assert.False(r.Success);
        Assert.Equal("SOURCE_MISSING", r.Errors[0].Code);
    }

    [Fact]
    public void T005_NotRaisedWhenVerbResultIsNull()
    {
        var r = Run("{out}\nx = %upper @.missing", "a = ##1",
            target: new[] { ("onMissing", "fail") });
        Assert.DoesNotContain(r.Errors, e => e.Code == "T005");
    }

    // ─────────────────────────────────────────────────────────────────
    // T006 — invalid output format
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void T006_EmitsForUnregisteredTargetFormat()
    {
        var r = Run("{out}\nx = @.a", "a = ##1", format: "notaformat");
        Assert.False(r.Success);
        Assert.Contains(r.Errors, e => e.Code == "T006");
    }

    [Theory]
    [InlineData("odin")]
    [InlineData("json")]
    [InlineData("xml")]
    [InlineData("csv")]
    public void T006_NotRaisedForKnownFormats(string fmt)
    {
        var r = Run("{out}\nx = @.a", "a = ##1", format: fmt);
        Assert.DoesNotContain(r.Errors, e => e.Code == "T006");
        Assert.NotNull(r.Formatted);
    }

    [Theory]
    [InlineData("odin")]
    [InlineData("json")]
    [InlineData("xml")]
    public void T006_ProducesNonEmptyOutputForKnownFormats(string fmt)
    {
        var r = Run("{out}\nx = @.a", "a = ##1", format: fmt);
        Assert.False(string.IsNullOrEmpty(r.Formatted));
    }

    // ─────────────────────────────────────────────────────────────────
    // T009 — loop source not array
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void T009_EmitsForPresentNonArrayScalar()
    {
        var r = Run("{out[]}\n:loop notArr\nx = @.a", "notArr = \"scalar\"");
        Assert.False(r.Success);
        Assert.Equal("T009", r.Errors[0].Code);
    }

    [Fact]
    public void T009_YieldsZeroRowsForAbsentLoopSource()
    {
        var r = Run("{out[]}\n:loop missing\nx = @.a", "a = ##1");
        Assert.True(r.Success);
        Assert.Empty(r.Errors);
    }

    [Fact]
    public void T009_DemotesToWarningUnderOnErrorWarn()
    {
        var r = Run("{out[]}\n:loop notArr\nx = @.a", "notArr = \"scalar\"",
            target: new[] { ("onError", "warn") });
        Assert.True(r.Success);
        Assert.Contains(r.Warnings, w => w.Code == "T009");
    }

    // ─────────────────────────────────────────────────────────────────
    // T008 — accumulator overflow
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void T008_EmitsWhenIntegerAccumulatorExceedsSafeCapacity()
    {
        var transform =
            "{$accumulator}\ntotal = ##0\n\n{out}\nx = %accumulate \"total\" @.a";
        // 2^53 + 1 exceeds the safe-integer magnitude where double precision is lost.
        var r = Run(transform, "a = ##9007199254740993");
        Assert.False(r.Success);
        Assert.Equal("T008", r.Errors[0].Code);
    }

    [Fact]
    public void T008_DoesNotRaiseForOrdinaryAccumulation()
    {
        var transform =
            "{$accumulator}\ntotal = ##0\n\n{out}\nx = %accumulate \"total\" @.a";
        var r = Run(transform, "a = ##5");
        Assert.True(r.Success);
        Assert.Empty(r.Errors);
    }

    // ─────────────────────────────────────────────────────────────────
    // @import resolution
    // ─────────────────────────────────────────────────────────────────

    private const string TablesDoc = @"{$}
odin = ""1.0.0""
transform = ""1.0.0""
direction = ""odin->odin""

{$source}
format = ""odin""

{$target}
format = ""odin""

{$table.STATES[code, name]}
""CA"", ""California""
""TX"", ""Texas""
";

    private const string SharedDoc = @"{$}
odin = ""1.0.0""
transform = ""1.0.0""
direction = ""odin->odin""

{$source}
format = ""odin""

{$target}
format = ""odin""

{shared}
greeting = ""hello""
";

    private const string Main = @"{$}
odin = ""1.0.0""
transform = ""1.0.0""
direction = ""odin->odin""

@import ./tables/states.odin
@import ./mappings/shared.odin

{$source}
format = ""odin""

{$target}
format = ""odin""
onMissing = ""fail""

{out}
state = %lookup ""STATES.name"" @.code
";

    private static string? Resolver(string p)
    {
        if (p.Contains("states")) return TablesDoc;
        if (p.Contains("shared")) return SharedDoc;
        return null;
    }

    private static TransformResult RunWithResolver(
        string transformText, string input, Func<string, string?>? resolver)
    {
        var t = Core.Odin.ParseTransform(transformText);
        var doc = Core.Odin.Parse(input);
        var options = resolver != null ? new TransformOptions { ImportResolver = resolver } : null;
        return TransformEngine.ExecuteDocument(t, doc, options);
    }

    [Fact]
    public void Import_MakesImportedTableUsableByLookup()
    {
        var r = RunWithResolver(Main, "code = \"CA\"", Resolver);
        Assert.True(r.Success);
        Assert.Empty(r.Errors);
        Assert.Contains("California", r.Formatted);
    }

    [Fact]
    public void Import_MergesImportedMappingSegment()
    {
        var r = RunWithResolver(Main, "code = \"TX\"", Resolver);
        Assert.Contains("greeting", r.Formatted);
        Assert.Contains("hello", r.Formatted);
    }

    [Fact]
    public void Import_LeavesTableUnresolvedWithoutResolver_T003()
    {
        var r = RunWithResolver(Main, "code = \"CA\"", null);
        Assert.False(r.Success);
        Assert.Equal("T003", r.Errors[0].Code);
    }

    [Fact]
    public void Import_LocalDeclarationsTakePrecedence()
    {
        const string localTable = @"{$}
odin = ""1.0.0""
transform = ""1.0.0""
direction = ""odin->odin""

@import ./tables/states.odin

{$source}
format = ""odin""

{$target}
format = ""odin""

{$table.STATES[code, name]}
""CA"", ""Local-California""

{out}
state = %lookup ""STATES.name"" @.code
";
        var r = RunWithResolver(localTable, "code = \"CA\"", Resolver);
        Assert.Contains("Local-California", r.Formatted);
    }

    [Fact]
    public void Import_IgnoresUnsatisfiableImport()
    {
        const string t = @"{$}
odin = ""1.0.0""
transform = ""1.0.0""
direction = ""odin->odin""

@import ./missing/nowhere.odin

{$source}
format = ""odin""

{$target}
format = ""odin""

{out}
x = @.a
";
        var r = RunWithResolver(t, "a = ##1", Resolver);
        Assert.True(r.Success);
    }
}
