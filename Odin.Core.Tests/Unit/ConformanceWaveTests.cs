using System.Collections.Generic;
using System.Text;
using Odin.Core;
using Odin.Core.Transform;
using Odin.Core.Types;
using Xunit;

namespace Odin.Core.Tests.Unit;

/// <summary>
/// Second-pass conformance: canonical precision and modifier order, conditional
/// requirement inversion, computed exclusion, binary/decimal constraints, default
/// onError, and lookup-miss reporting.
/// </summary>
public class ConformanceWaveTests
{
    private static string Canon(string input) =>
        Encoding.UTF8.GetString(Core.Odin.Canonicalize(Core.Odin.Parse(input)));

    // ── Canonical precision (Fix 1) ──────────────────────────────────────────

    [Fact]
    public void CanonicalPreservesIntegerBeyondDoubleRange()
    {
        Assert.Equal("big = ##9007199254740993\n", Canon("big = ##9007199254740993"));
        Assert.Equal("huge = ##12345678901234567890\n", Canon("huge = ##12345678901234567890"));
    }

    [Fact]
    public void CanonicalPreservesHighPrecisionDecimal()
    {
        Assert.Equal("pi = #3.14159265358979323846\n", Canon("pi = #3.14159265358979323846"));
    }

    [Fact]
    public void CanonicalPreservesLargeCurrencyAndPadsFraction()
    {
        Assert.Equal("amt = #$12345678901234567890.50\n", Canon("amt = #$12345678901234567890.50"));
        Assert.Equal("amt = #$123.450000000000000000\n", Canon("amt = #$123.450000000000000000"));
    }

    // ── Canonical modifier order !-* (Fix 2) ─────────────────────────────────

    [Fact]
    public void CanonicalModifierOrderIsRequiredDeprecatedConfidential()
    {
        Assert.Equal("x = !-*\"secret\"\n", Canon("x = !-*\"secret\""));
        Assert.Equal("x = !*\"secret\"\n", Canon("x = !*\"secret\""));
        Assert.Equal("x = !-\"secret\"\n", Canon("x = !-\"secret\""));
        Assert.Equal("x = -*\"secret\"\n", Canon("x = -*\"secret\""));
    }

    // ── :unless is the inverse of :if (Fix 3) ────────────────────────────────

    private const string UnlessSchema =
        "{$}\nodin = \"1.0.0\"\nschema = \"1.0.0\"\n\n{Person}\nstatus =\nphone = ! :unless status = \"inactive\"";

    [Fact]
    public void UnlessNotRequiredWhenConditionTrue()
    {
        var schema = Core.Odin.ParseSchema(UnlessSchema);
        var doc = Core.Odin.Parse("{Person}\nstatus = \"inactive\"");
        Assert.True(Core.Odin.Validate(doc, schema).IsValid);
    }

    [Fact]
    public void UnlessRequiredWhenConditionFalse()
    {
        var schema = Core.Odin.ParseSchema(UnlessSchema);
        var doc = Core.Odin.Parse("{Person}\nstatus = \"active\"");
        var result = Core.Odin.Validate(doc, schema);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "V010" && e.Path == "Person.phone");
    }

    // ── :computed input exclusion (Fix 7) ────────────────────────────────────

    [Fact]
    public void ComputedRequiredFieldAbsentIsNotRequired()
    {
        var schema = Core.Odin.ParseSchema(
            "{$}\nodin = \"1.0.0\"\nschema = \"1.0.0\"\n\n{Order}\ntotal = !# :computed");
        var doc = Core.Odin.Parse("{Order}\nname = \"x\"");
        Assert.True(Core.Odin.Validate(doc, schema).IsValid);
    }

    // ── Binary size constraints (Fix 5) ──────────────────────────────────────

    [Fact]
    public void BinarySizeExactPasses()
    {
        var schema = Core.Odin.ParseSchema(
            "{$}\nodin = \"1.0.0\"\nschema = \"1.0.0\"\n\n{R}\nhash = ^:(4)");
        Assert.True(Core.Odin.Validate(Core.Odin.Parse("{R}\nhash = ^AAAAAA=="), schema).IsValid);
    }

    [Fact]
    public void BinarySizeWrongFailsV003()
    {
        var schema = Core.Odin.ParseSchema(
            "{$}\nodin = \"1.0.0\"\nschema = \"1.0.0\"\n\n{R}\nhash = ^:(4)");
        var tooSmall = Core.Odin.Validate(Core.Odin.Parse("{R}\nhash = ^AAAA"), schema);
        Assert.False(tooSmall.IsValid);
        Assert.Contains(tooSmall.Errors, e => e.Code == "V003" && e.Path == "R.hash");

        var tooLarge = Core.Odin.Validate(Core.Odin.Parse("{R}\nhash = ^AAAAAAA="), schema);
        Assert.False(tooLarge.IsValid);
        Assert.Contains(tooLarge.Errors, e => e.Code == "V003" && e.Path == "R.hash");
    }

    [Fact]
    public void BinaryAlgorithmSizeWrongFailsV003()
    {
        var schema = Core.Odin.ParseSchema(
            "{$}\nodin = \"1.0.0\"\nschema = \"1.0.0\"\n\n{R}\nhash = ^sha256:(32)");
        var result = Core.Odin.Validate(
            Core.Odin.Parse("{R}\nhash = ^sha256:AAAAAAAAAAAAAAAAAAAAAA=="), schema);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "V003" && e.Path == "R.hash");
    }

    // ── Decimal precision #.N (Fix 6) ────────────────────────────────────────

    [Fact]
    public void DecimalExactPlacesPasses()
    {
        var schema = Core.Odin.ParseSchema(
            "{$}\nodin = \"1.0.0\"\nschema = \"1.0.0\"\n\n{R}\nrate = #.4");
        Assert.True(Core.Odin.Validate(Core.Odin.Parse("{R}\nrate = #1.2345"), schema).IsValid);
    }

    [Fact]
    public void DecimalWrongPlacesFailsV003()
    {
        var schema = Core.Odin.ParseSchema(
            "{$}\nodin = \"1.0.0\"\nschema = \"1.0.0\"\n\n{R}\nrate = #.4");
        var tooFew = Core.Odin.Validate(Core.Odin.Parse("{R}\nrate = #1.23"), schema);
        Assert.False(tooFew.IsValid);
        Assert.Contains(tooFew.Errors, e => e.Code == "V003" && e.Path == "R.rate");

        var tooMany = Core.Odin.Validate(Core.Odin.Parse("{R}\nrate = #1.23456"), schema);
        Assert.False(tooMany.IsValid);
        Assert.Contains(tooMany.Errors, e => e.Code == "V003" && e.Path == "R.rate");
    }

    // ── onError defaults to fail; custom verbs echo (Fix 4) ───────────────────

    [Fact]
    public void UnknownBuiltinVerbSurfacesAsErrorByDefault()
    {
        var t = new OdinTransform
        {
            Target = new TargetConfig { Format = "json" },
            Segments = new List<TransformSegment>
            {
                new TransformSegment
                {
                    Mappings = new List<FieldMapping>
                    {
                        new FieldMapping
                        {
                            Target = "X",
                            Expression = FieldExpression.Transform(
                                new VerbCall { Verb = "doesNotExist", IsCustom = false, Args = new List<VerbArg>() }),
                        }
                    }
                }
            }
        };
        var result = TransformEngine.Execute(t, DynValue.Null());
        Assert.False(result.Success);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void UnregisteredCustomVerbEchoesFirstArgument()
    {
        var t = new OdinTransform
        {
            Target = new TargetConfig { Format = "json" },
            Segments = new List<TransformSegment>
            {
                new TransformSegment
                {
                    Mappings = new List<FieldMapping>
                    {
                        new FieldMapping
                        {
                            Target = "X",
                            Expression = FieldExpression.Transform(new VerbCall
                            {
                                Verb = "myCustom",
                                IsCustom = true,
                                Args = new List<VerbArg> { VerbArg.Lit(new OdinString("echoed")) },
                            }),
                        }
                    }
                }
            }
        };
        var result = TransformEngine.Execute(t, DynValue.Null());
        Assert.True(result.Success);
        Assert.Equal(DynValue.String("echoed"), result.Output!.Get("X"));
    }

    // ── %lookup miss reporting via onMissing (Fix 8) ─────────────────────────

    private static OdinTransform LookupTransform(string? onMissing)
    {
        var target = new TargetConfig { Format = "json" };
        if (onMissing != null)
            target.Options["onMissing"] = onMissing;
        var table = new LookupTable
        {
            Name = "STATUS",
            Columns = new List<string> { "code", "name" },
            Rows = new List<List<DynValue>>
            {
                new List<DynValue> { DynValue.String("A"), DynValue.String("Active") },
            },
        };
        return new OdinTransform
        {
            Target = target,
            Tables = new Dictionary<string, LookupTable> { ["STATUS"] = table },
            Segments = new List<TransformSegment>
            {
                new TransformSegment
                {
                    Mappings = new List<FieldMapping>
                    {
                        new FieldMapping
                        {
                            Target = "Name",
                            Expression = FieldExpression.Transform(new VerbCall
                            {
                                Verb = "lookup",
                                IsCustom = false,
                                Args = new List<VerbArg>
                                {
                                    VerbArg.Lit(new OdinString("STATUS.name")),
                                    VerbArg.Lit(new OdinString("Z")),
                                },
                            }),
                        }
                    }
                }
            }
        };
    }

    [Fact]
    public void LookupMissIsSilentNullByDefault()
    {
        var result = TransformEngine.Execute(LookupTransform(null), DynValue.Null());
        Assert.True(result.Success);
        Assert.Empty(result.Errors);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void LookupMissReportsErrorWhenOnMissingFail()
    {
        var result = TransformEngine.Execute(LookupTransform("fail"), DynValue.Null());
        Assert.NotEmpty(result.Errors);
        Assert.Contains(result.Errors, e => e.Code == "T004");
    }

    [Fact]
    public void LookupMissReportsWarningWhenOnMissingWarn()
    {
        var result = TransformEngine.Execute(LookupTransform("warn"), DynValue.Null());
        Assert.NotEmpty(result.Warnings);
    }
}
