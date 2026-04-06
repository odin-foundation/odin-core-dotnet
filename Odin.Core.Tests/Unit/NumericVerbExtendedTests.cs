using System;
using System.Collections.Generic;
using Odin.Core.Types;
using Odin.Core.Transform;
using Odin.Core.Transform.Verbs;
using Xunit;

namespace Odin.Core.Tests.Unit;

/// <summary>
/// Extended numeric, financial, statistics, and datetime verb tests ported from
/// Rust SDK numeric_verbs.rs extended_tests module.
/// </summary>
public class NumericVerbExtendedTests
{
    private readonly VerbRegistry _registry = new VerbRegistry();
    private readonly VerbContext _ctx = new VerbContext();

    private DynValue Invoke(string verb, params DynValue[] args)
        => _registry.Invoke(verb, args, _ctx);

    private static DynValue S(string v) => DynValue.String(v);
    private static DynValue I(long v) => DynValue.Integer(v);
    private static DynValue F(double v) => DynValue.Float(v);
    private static DynValue B(bool v) => DynValue.Bool(v);
    private static DynValue Null() => DynValue.Null();
    private static DynValue Arr(params DynValue[] items) => DynValue.Array(new List<DynValue>(items));

    /// <summary>Assert that a result is numeric and close to the expected value.</summary>
    private static void AssertNumeric(DynValue result, double expected, double tolerance)
    {
        var d = result.AsDouble();
        var i = result.AsInt64();
        if (d.HasValue)
            Assert.True(Math.Abs(d.Value - expected) < tolerance,
                $"Float({d.Value}) not close to {expected}");
        else if (i.HasValue)
            Assert.True(Math.Abs((double)i.Value - expected) < tolerance,
                $"Integer({i.Value}) not close to {expected}");
        else
            Assert.Fail($"Expected numeric, got {result.Type}");
    }

    // =========================================================================
    // 1. PARSE_INT edge cases
    // =========================================================================

    [Fact]
    public void ParseInt_Basic()
        => AssertNumeric(Invoke("parseInt", S("42")), 42.0, 1e-10);

    [Fact]
    public void ParseInt_Negative()
        => AssertNumeric(Invoke("parseInt", S("-99")), -99.0, 1e-10);

    [Fact]
    public void ParseInt_FromFloatString_Truncates()
    {
        // .NET parseInt parses "3.14" as double then truncates to 3
        var result = Invoke("parseInt", S("3.14"));
        AssertNumeric(result, 3.0, 1e-10);
    }

    [Fact]
    public void ParseInt_NonNumeric_ReturnsNull()
    {
        // .NET parseInt returns null for non-numeric strings
        var result = Invoke("parseInt", S("abc"));
        Assert.True(result.IsNull);
    }

    // =========================================================================
    // 2. IS_FINITE / IS_NAN extended
    // =========================================================================

    [Fact]
    public void IsFinite_NegativeInfinity()
        => Assert.Equal(false, Invoke("isFinite", F(double.NegativeInfinity)).AsBool());

    // =========================================================================
    // 3. MIN_OF / MAX_OF edge cases
    // =========================================================================

    [Fact]
    public void MinOf_Basic()
        => AssertNumeric(Invoke("minOf", I(3), I(1), I(2)), 1.0, 1e-10);

    [Fact]
    public void MinOf_Negative()
        => AssertNumeric(Invoke("minOf", F(-5.0), F(-3.0), F(-10.0)), -10.0, 1e-10);

    [Fact]
    public void MinOf_Single()
        => AssertNumeric(Invoke("minOf", I(42)), 42.0, 1e-10);

    [Fact]
    public void MinOf_MixedTypes()
        => AssertNumeric(Invoke("minOf", I(5), F(3.5), S("2")), 2.0, 1e-10);

    [Fact]
    public void MaxOf_Basic()
        => AssertNumeric(Invoke("maxOf", I(3), I(1), I(2)), 3.0, 1e-10);

    [Fact]
    public void MaxOf_Negative()
        => AssertNumeric(Invoke("maxOf", F(-5.0), F(-3.0), F(-10.0)), -3.0, 1e-10);

    [Fact]
    public void MaxOf_Single()
        => AssertNumeric(Invoke("maxOf", I(42)), 42.0, 1e-10);

    [Fact]
    public void MaxOf_LargeNumbers()
        => AssertNumeric(Invoke("maxOf", F(1e15), F(1e14), F(1e16)), 1e16, 1e6);

    // =========================================================================
    // 4. SAFE_DIVIDE edge cases
    // =========================================================================

    [Fact]
    public void SafeDivide_Normal()
        => AssertNumeric(Invoke("safeDivide", F(10.0), F(3.0), F(0.0)), 10.0 / 3.0, 1e-10);

    [Fact]
    public void SafeDivide_ByZero()
        => AssertNumeric(Invoke("safeDivide", F(10.0), F(0.0), F(-1.0)), -1.0, 1e-10);

    [Fact]
    public void SafeDivide_ZeroNumerator()
        => AssertNumeric(Invoke("safeDivide", F(0.0), F(5.0), F(0.0)), 0.0, 1e-10);

    [Fact]
    public void SafeDivide_MissingArgs()
        => Assert.True(Invoke("safeDivide", F(10.0)).IsNull);

    // =========================================================================
    // 5. MATH VERBS: LOG, LN, LOG10, EXP, POW, SQRT
    // =========================================================================

    [Fact]
    public void Log_Base2()
        => AssertNumeric(Invoke("log", F(8.0), F(2.0)), 3.0, 1e-10);

    [Fact]
    public void Log_Base10()
        => AssertNumeric(Invoke("log", F(1000.0), F(10.0)), 3.0, 1e-10);

    [Fact]
    public void Ln_E()
        => AssertNumeric(Invoke("ln", F(Math.E)), 1.0, 1e-10);

    [Fact]
    public void Ln_One()
        => AssertNumeric(Invoke("ln", F(1.0)), 0.0, 1e-10);

    [Fact]
    public void Log10_Hundred()
        => AssertNumeric(Invoke("log10", F(100.0)), 2.0, 1e-10);

    [Fact]
    public void Exp_Zero()
        => AssertNumeric(Invoke("exp", F(0.0)), 1.0, 1e-10);

    [Fact]
    public void Exp_One()
        => AssertNumeric(Invoke("exp", F(1.0)), Math.E, 1e-10);

    [Fact]
    public void Pow_Basic()
        => AssertNumeric(Invoke("pow", F(2.0), F(10.0)), 1024.0, 1e-10);

    [Fact]
    public void Pow_Fractional()
        => AssertNumeric(Invoke("pow", F(4.0), F(0.5)), 2.0, 1e-10);

    [Fact]
    public void Pow_ZeroExponent()
        => AssertNumeric(Invoke("pow", F(99.0), F(0.0)), 1.0, 1e-10);

    [Fact]
    public void Sqrt_Perfect()
        => AssertNumeric(Invoke("sqrt", F(144.0)), 12.0, 1e-10);

    [Fact]
    public void Sqrt_Zero()
        => AssertNumeric(Invoke("sqrt", F(0.0)), 0.0, 1e-10);

    [Fact]
    public void Sqrt_NonPerfect()
        => AssertNumeric(Invoke("sqrt", F(2.0)), Math.Sqrt(2.0), 1e-10);

    // =========================================================================
    // 6. FINANCIAL: COMPOUND / DISCOUNT
    // =========================================================================

    [Fact]
    public void Compound_Basic()
        => AssertNumeric(Invoke("compound", F(1000.0), F(0.05), I(10)),
            1000.0 * Math.Pow(1.05, 10), 0.01);

    [Fact]
    public void Compound_ZeroRate()
        => AssertNumeric(Invoke("compound", F(1000.0), F(0.0), I(10)), 1000.0, 0.01);

    [Fact]
    public void Compound_OnePeriod()
        => AssertNumeric(Invoke("compound", F(500.0), F(0.10), I(1)), 550.0, 0.01);

    [Fact]
    public void Compound_LargePeriods()
        => AssertNumeric(Invoke("compound", F(100.0), F(0.07), I(30)),
            100.0 * Math.Pow(1.07, 30), 0.01);

    [Fact]
    public void Compound_NegativeRate()
        => AssertNumeric(Invoke("compound", F(1000.0), F(-0.02), I(5)),
            1000.0 * Math.Pow(0.98, 5), 0.01);

    [Fact]
    public void Compound_MissingArgs()
        => Assert.True(Invoke("compound", F(100.0), F(0.05)).IsNull);

    [Fact]
    public void Compound_NullRate()
        => Assert.True(Invoke("compound", F(1000.0), Null(), I(10)).IsNull);

    [Fact]
    public void Discount_Basic()
        => AssertNumeric(Invoke("discount", F(1000.0), F(0.05), I(10)),
            1000.0 / Math.Pow(1.05, 10), 0.01);

    [Fact]
    public void Discount_ZeroRate()
        => AssertNumeric(Invoke("discount", F(1000.0), F(0.0), I(5)), 1000.0, 0.01);

    [Fact]
    public void Discount_OnePeriod()
        => AssertNumeric(Invoke("discount", F(1100.0), F(0.10), I(1)), 1000.0, 0.01);

    [Fact]
    public void Discount_LargePeriods()
        => AssertNumeric(Invoke("discount", F(1000000.0), F(0.08), I(50)),
            1000000.0 / Math.Pow(1.08, 50), 1.0);

    [Fact]
    public void Discount_MissingArgs()
        => Assert.True(Invoke("discount", F(100.0)).IsNull);

    // =========================================================================
    // 7. PMT / FV / PV
    // Args: pmt(rate, nper, pv), fv(rate, nper, pv), pv(rate, nper, fv)
    // =========================================================================

    [Fact]
    public void Pmt_Basic()
    {
        // pmt(principal=200000, rate=0.05/12, periods=360)
        double rate = 0.05 / 12.0;
        double nper = 360.0;
        double pv = 200000.0;
        double factor = Math.Pow(1.0 + rate, nper);
        double expected = pv * rate * factor / (factor - 1.0);
        AssertNumeric(Invoke("pmt", F(pv), F(rate), F(nper)), expected, 1.0);
    }

    [Fact]
    public void Pmt_ZeroRate()
        => AssertNumeric(Invoke("pmt", F(12000.0), F(0.0), F(12.0)), 1000.0, 0.01);

    [Fact]
    public void Pmt_OnePeriod()
        => AssertNumeric(Invoke("pmt", F(1000.0), F(0.1), F(1.0)), 1100.0, 0.01);

    [Fact]
    public void Pmt_HighRate()
    {
        // pmt(principal=100000, rate=0.02, periods=60)
        double rate = 0.02;
        double n = 60.0;
        double pv = 100000.0;
        double factor = Math.Pow(1.0 + rate, n);
        double expected = pv * rate * factor / (factor - 1.0);
        AssertNumeric(Invoke("pmt", F(pv), F(rate), F(n)), expected, 0.01);
    }

    [Fact]
    public void Pmt_MissingArgs()
        => Assert.True(Invoke("pmt", F(0.05), F(360.0)).IsNull);

    [Fact]
    public void Pmt_NullPrincipal()
        => Assert.True(Invoke("pmt", Null(), F(0.05), F(360.0)).IsNull);

    [Fact]
    public void Fv_Basic()
    {
        // fv(payment=100, rate=0.005, periods=120) = 100 * ((1.005)^120 - 1) / 0.005
        double expected = 100.0 * (Math.Pow(1.005, 120.0) - 1.0) / 0.005;
        AssertNumeric(Invoke("fv", F(100.0), F(0.005), F(120.0)), expected, 0.01);
    }

    [Fact]
    public void Fv_ZeroRate()
        => AssertNumeric(Invoke("fv", F(100.0), F(0.0), F(12.0)), 1200.0, 0.01);

    [Fact]
    public void Fv_LargeN()
    {
        // fv(payment=50, rate=0.005, periods=480) = 50 * ((1.005)^480 - 1) / 0.005
        double expected = 50.0 * (Math.Pow(1.005, 480.0) - 1.0) / 0.005;
        AssertNumeric(Invoke("fv", F(50.0), F(0.005), F(480.0)), expected, 1.0);
    }

    [Fact]
    public void Fv_MissingArgs()
        => Assert.True(Invoke("fv", F(0.05), F(12.0)).IsNull);

    [Fact]
    public void Pv_Basic()
    {
        // pv(payment=100, rate=0.005, periods=120) = 100 * (1 - (1.005)^-120) / 0.005
        double expected = 100.0 * (1.0 - Math.Pow(1.005, -120.0)) / 0.005;
        AssertNumeric(Invoke("pv", F(100.0), F(0.005), F(120.0)), expected, 0.01);
    }

    [Fact]
    public void Pv_ZeroRate()
        => AssertNumeric(Invoke("pv", F(1200.0), F(0.0), F(12.0)), 1200.0 * 12.0, 0.01);

    [Fact]
    public void Pv_MissingArgs()
        => Assert.True(Invoke("pv", F(0.05)).IsNull);

    // =========================================================================
    // 8. NPV
    // .NET formula: sum of flows[i]/(1+rate)^(i+1) for i=0..n-1
    // =========================================================================

    [Fact]
    public void Npv_Basic()
    {
        var flows = Arr(F(-1000.0), F(300.0), F(400.0), F(500.0));
        // t=0 indexing: sum of cf[t] / (1+r)^t
        double expected = -1000.0 + 300.0 / 1.1 + 400.0 / Math.Pow(1.1, 2) + 500.0 / Math.Pow(1.1, 3);
        AssertNumeric(Invoke("npv", F(0.1), flows), expected, 1.0);
    }

    [Fact]
    public void Npv_ZeroRate()
    {
        var flows = Arr(F(-1000.0), F(500.0), F(500.0));
        AssertNumeric(Invoke("npv", F(0.0), flows), 0.0, 0.01);
    }

    [Fact]
    public void Npv_SingleFlow()
    {
        var flows = Arr(F(1000.0));
        // t=0: 1000 / (1.1)^0 = 1000
        AssertNumeric(Invoke("npv", F(0.1), flows), 1000.0, 0.01);
    }

    [Fact]
    public void Npv_HighRate()
    {
        var flows = Arr(F(-100.0), F(50.0), F(50.0), F(50.0));
        // t=0 indexing
        double expected = -100.0 + 50.0 / 1.5 + 50.0 / Math.Pow(1.5, 2) + 50.0 / Math.Pow(1.5, 3);
        AssertNumeric(Invoke("npv", F(0.5), flows), expected, 0.01);
    }

    [Fact]
    public void Npv_NegativeFlows()
    {
        var flows = Arr(F(1000.0), F(-300.0), F(-300.0), F(-300.0));
        // t=0 indexing
        double expected = 1000.0 - 300.0 / 1.05 - 300.0 / Math.Pow(1.05, 2) - 300.0 / Math.Pow(1.05, 3);
        AssertNumeric(Invoke("npv", F(0.05), flows), expected, 1.0);
    }

    [Fact]
    public void Npv_ManyCashFlows()
    {
        var items = new List<DynValue> { F(-10000.0) };
        for (int j = 0; j < 20; j++) items.Add(F(1000.0));
        var flows = DynValue.Array(items);
        var result = Invoke("npv", F(0.08), flows);
        Assert.True(result.AsDouble().HasValue || result.AsInt64().HasValue,
            "NPV should return numeric");
    }

    [Fact]
    public void Npv_MissingArgs()
        => Assert.True(Invoke("npv", F(0.1)).IsNull);

    [Fact]
    public void Npv_NullRate()
        => Assert.True(Invoke("npv", Null(), Arr(F(-1000.0), F(500.0))).IsNull);

    // =========================================================================
    // 9. IRR
    // =========================================================================

    [Fact]
    public void Irr_Basic()
    {
        var flows = Arr(F(-1000.0), F(300.0), F(400.0), F(500.0));
        var result = Invoke("irr", flows);
        var v = result.AsDouble();
        Assert.True(v.HasValue, "IRR should return a float");
        Assert.True(v.Value > 0.0 && v.Value < 0.5, $"IRR={v.Value} out of reasonable range");
    }

    [Fact]
    public void Irr_Simple()
    {
        var flows = Arr(F(-100.0), F(110.0));
        AssertNumeric(Invoke("irr", flows), 0.10, 0.001);
    }

    [Fact]
    public void Irr_EvenCashFlows()
    {
        var flows = Arr(F(-1000.0), F(400.0), F(400.0), F(400.0));
        var result = Invoke("irr", flows);
        var v = result.AsDouble();
        Assert.True(v.HasValue, "IRR should return a float");
        Assert.True(v.Value > 0.05 && v.Value < 0.15, $"IRR={v.Value}");
    }

    [Fact]
    public void Irr_TooFewFlows()
    {
        // .NET irr with single flow: Newton-Raphson can't converge (dnpv=0),
        // but still returns initial guess as a number
        var flows = Arr(F(-100.0));
        var result = Invoke("irr", flows);
        // Returns a numeric value (the initial guess), not null
        Assert.True(result.AsDouble().HasValue || result.AsInt64().HasValue,
            "IRR with single flow should still return a numeric value");
    }

    // =========================================================================
    // 10. RATE / NPER / DEPRECIATION
    // =========================================================================

    [Fact]
    public void Rate_Basic()
    {
        var result = Invoke("rate", F(10.0), F(-100.0), F(1000.0), F(0.0));
        Assert.True(result.AsDouble().HasValue, "rate should return a float");
        Assert.True(double.IsFinite(result.AsDouble()!.Value), "rate should be finite");
    }

    [Fact]
    public void Rate_ZeroPmtGrowth()
        => AssertNumeric(Invoke("rate", F(10.0), F(0.0), F(-1000.0), F(2000.0)), 0.0718, 0.01);

    [Fact]
    public void Rate_MissingArgs()
        => Assert.True(Invoke("rate", F(10.0), F(-100.0)).IsNull);

    [Fact]
    public void Nper_Basic()
    {
        // nper(rate=0.01, pmt=-100, pv=5000): solve for n
        var result = Invoke("nper", F(0.01), F(-100.0), F(5000.0));
        var v = result.AsDouble();
        Assert.True(v.HasValue, "nper should return float");
        Assert.True(v.Value > 0.0 && v.Value < 200.0, $"nper={v.Value}");
    }

    [Fact]
    public void Nper_ZeroRate()
        => AssertNumeric(Invoke("nper", F(0.0), F(-100.0), F(5000.0)), 50.0, 0.01);

    [Fact]
    public void Nper_MissingArgs()
        => Assert.True(Invoke("nper", F(0.01), F(-100.0)).IsNull);

    [Fact]
    public void Depreciation_Basic()
        => AssertNumeric(Invoke("depreciation", F(10000.0), F(1000.0), F(5.0)), 1800.0, 0.01);

    [Fact]
    public void Depreciation_NoSalvage()
        => AssertNumeric(Invoke("depreciation", F(5000.0), F(0.0), F(10.0)), 500.0, 0.01);

    [Fact]
    public void Depreciation_ZeroLife()
        => Assert.True(Invoke("depreciation", F(5000.0), F(0.0), F(0.0)).IsNull);

    [Fact]
    public void Depreciation_EqualCostSalvage()
        => AssertNumeric(Invoke("depreciation", F(5000.0), F(5000.0), F(10.0)), 0.0, 1e-10);

    [Fact]
    public void Depreciation_MissingArgs()
        => Assert.True(Invoke("depreciation", F(5000.0), F(0.0)).IsNull);

    // =========================================================================
    // 11. STATISTICS: STD, VARIANCE, MEDIAN, MODE
    // =========================================================================

    [Fact]
    public void Std_Basic()
        => AssertNumeric(Invoke("std", Arr(F(2), F(4), F(4), F(4), F(5), F(5), F(7), F(9))), 2.0, 0.01);

    [Fact]
    public void Std_Uniform()
        => AssertNumeric(Invoke("std", Arr(F(5), F(5), F(5))), 0.0, 1e-10);

    [Fact]
    public void Variance_Basic()
        => AssertNumeric(Invoke("variance", Arr(F(2), F(4), F(4), F(4), F(5), F(5), F(7), F(9))), 4.0, 0.01);

    [Fact]
    public void Median_Odd()
        => AssertNumeric(Invoke("median", Arr(F(3), F(1), F(2))), 2.0, 1e-10);

    [Fact]
    public void Median_Even()
        => AssertNumeric(Invoke("median", Arr(F(1), F(2), F(3), F(4))), 2.5, 1e-10);

    [Fact]
    public void Median_Single()
        => AssertNumeric(Invoke("median", Arr(F(42))), 42.0, 1e-10);

    [Fact]
    public void Mode_Basic()
        => AssertNumeric(Invoke("mode", Arr(F(1), F(2), F(2), F(3))), 2.0, 1e-10);

    [Fact]
    public void Mode_AllSame()
        => AssertNumeric(Invoke("mode", Arr(F(7), F(7), F(7))), 7.0, 1e-10);

    // =========================================================================
    // 12. CLAMP / INTERPOLATE / WEIGHTED_AVG
    // .NET interpolate(a, b, t) = a + (b-a)*t  (3 args, not 5)
    // =========================================================================

    [Fact]
    public void Clamp_WithinRange()
        => AssertNumeric(Invoke("clamp", F(5.0), F(1.0), F(10.0)), 5.0, 1e-10);

    [Fact]
    public void Clamp_BelowMin()
        => AssertNumeric(Invoke("clamp", F(-5.0), F(0.0), F(10.0)), 0.0, 1e-10);

    [Fact]
    public void Clamp_AboveMax()
        => AssertNumeric(Invoke("clamp", F(15.0), F(0.0), F(10.0)), 10.0, 1e-10);

    [Fact]
    public void Interpolate_Midpoint()
        => AssertNumeric(Invoke("interpolate", F(0.0), F(100.0), F(0.5)), 50.0, 1e-10);

    [Fact]
    public void Interpolate_AtStart()
        => AssertNumeric(Invoke("interpolate", F(0.0), F(100.0), F(0.0)), 0.0, 1e-10);

    [Fact]
    public void Interpolate_AtEnd()
        => AssertNumeric(Invoke("interpolate", F(0.0), F(100.0), F(1.0)), 100.0, 1e-10);

    [Fact]
    public void Interpolate_MissingArgs()
        => Assert.True(Invoke("interpolate", F(0.0), F(100.0)).IsNull);

    [Fact]
    public void WeightedAvg_Basic()
    {
        var vals = Arr(F(80.0), F(90.0), F(100.0));
        var wts = Arr(F(1.0), F(2.0), F(1.0));
        AssertNumeric(Invoke("weightedAvg", vals, wts), 90.0, 1e-10);
    }

    [Fact]
    public void WeightedAvg_EqualWeights()
    {
        var vals = Arr(F(10.0), F(20.0), F(30.0));
        var wts = Arr(F(1.0), F(1.0), F(1.0));
        AssertNumeric(Invoke("weightedAvg", vals, wts), 20.0, 1e-10);
    }

    // =========================================================================
    // 13. MOD
    // =========================================================================

    [Fact]
    public void Mod_Basic()
        => AssertNumeric(Invoke("mod", I(10), I(3)), 1.0, 1e-10);

    [Fact]
    public void Mod_NoRemainder()
        => AssertNumeric(Invoke("mod", I(9), I(3)), 0.0, 1e-10);

    [Fact]
    public void Mod_Float()
        => AssertNumeric(Invoke("mod", F(10.5), F(3.0)), 1.5, 1e-10);

    [Fact]
    public void Mod_ByZero()
        => Assert.True(Invoke("mod", I(10), I(0)).IsNull);

    // =========================================================================
    // 14. PERCENTILE / QUANTILE
    // =========================================================================

    [Fact]
    public void Percentile_50th()
        => AssertNumeric(Invoke("percentile", Arr(F(1), F(2), F(3), F(4), F(5)), F(50.0)), 3.0, 1e-10);

    [Fact]
    public void Percentile_0th()
        => AssertNumeric(Invoke("percentile", Arr(F(1), F(2), F(3)), F(0.0)), 1.0, 1e-10);

    [Fact]
    public void Percentile_100th()
        => AssertNumeric(Invoke("percentile", Arr(F(1), F(2), F(3)), F(100.0)), 3.0, 1e-10);

    [Fact]
    public void Quantile_Half()
        => AssertNumeric(Invoke("quantile", Arr(F(10), F(20), F(30)), F(0.5)), 20.0, 1e-10);

    // =========================================================================
    // 15. COVARIANCE / CORRELATION
    // =========================================================================

    [Fact]
    public void Covariance_PerfectPositive()
        => AssertNumeric(Invoke("covariance",
            Arr(F(1), F(2), F(3)),
            Arr(F(2), F(4), F(6))),
            4.0 / 3.0, 1e-10);

    [Fact]
    public void Correlation_PerfectPositive()
        => AssertNumeric(Invoke("correlation",
            Arr(F(1), F(2), F(3)),
            Arr(F(2), F(4), F(6))),
            1.0, 1e-10);

    [Fact]
    public void Correlation_PerfectNegative()
        => AssertNumeric(Invoke("correlation",
            Arr(F(1), F(2), F(3)),
            Arr(F(6), F(4), F(2))),
            -1.0, 1e-10);

    // =========================================================================
    // 16. STD_SAMPLE / VARIANCE_SAMPLE
    // =========================================================================

    [Fact]
    public void StdSample_Basic()
        => AssertNumeric(Invoke("stdSample", Arr(F(2), F(4), F(4), F(4), F(5), F(5), F(7), F(9))),
            Math.Sqrt(32.0 / 7.0), 0.01);

    [Fact]
    public void StdSample_TooFew()
        => Assert.True(Invoke("stdSample", Arr(F(5))).IsNull);

    [Fact]
    public void VarianceSample_Basic()
        => AssertNumeric(Invoke("varianceSample", Arr(F(2), F(4), F(4), F(4), F(5), F(5), F(7), F(9))),
            32.0 / 7.0, 0.01);

    // =========================================================================
    // 17. ZSCORE
    // .NET zscore(value, mean, stddev) -- 3 scalar args
    // =========================================================================

    [Fact]
    public void Zscore_AtMean()
        => AssertNumeric(Invoke("zscore", F(3.0), F(3.0), F(2.0)), 0.0, 1e-10);

    [Fact]
    public void Zscore_AboveMean()
    {
        // zscore(value=5, mean=3, stddev=2) = (5-3)/2 = 1.0
        var result = Invoke("zscore", F(5.0), F(3.0), F(2.0));
        AssertNumeric(result, 1.0, 1e-10);
    }

    [Fact]
    public void Zscore_AllSame()
        => Assert.True(Invoke("zscore", F(5.0), F(5.0), F(0.0)).IsNull);

    // =========================================================================
    // 18. NUMERIC EDGE CASES
    // =========================================================================

    [Fact]
    public void FormatNumber_VerySmall()
        => Assert.Equal("0.000001", Invoke("formatNumber", F(0.000001), I(6)).AsString());

    [Fact]
    public void FormatNumber_VeryLarge()
    {
        var result = Invoke("formatNumber", F(1e15), I(0)).AsString();
        Assert.False(string.IsNullOrEmpty(result), "Expected non-empty string");
    }

    [Fact]
    public void Sign_VeryLarge()
        => AssertNumeric(Invoke("sign", F(1e300)), 1.0, 1e-10);

    [Fact]
    public void Sign_VerySmallPositive()
        => AssertNumeric(Invoke("sign", F(1e-300)), 1.0, 1e-10);

    [Fact]
    public void Trunc_VeryLarge()
        => AssertNumeric(Invoke("trunc", F(1e15 + 0.5)), 1e15, 1.0);

    [Fact]
    public void Floor_VerySmallNegative()
        => AssertNumeric(Invoke("floor", F(-0.0001)), -1.0, 1e-10);

    [Fact]
    public void Ceil_VerySmallPositive()
        => AssertNumeric(Invoke("ceil", F(0.0001)), 1.0, 1e-10);

    // =========================================================================
    // 19. NULL / ERROR INPUT HANDLING
    // .NET returns null for null inputs, does not throw
    // =========================================================================

    [Fact]
    public void FormatNumber_NullInput()
        => Assert.True(Invoke("formatNumber", Null(), I(2)).IsNull);

    [Fact]
    public void Floor_NullInput()
        => Assert.True(Invoke("floor", Null()).IsNull);

    [Fact]
    public void Ceil_NullInput()
        => Assert.True(Invoke("ceil", Null()).IsNull);

    // =========================================================================
    // 20. DATETIME: FORMAT_DATE / FORMAT_TIME
    // =========================================================================

    [Fact]
    public void FormatDate_YyyyMmDd()
        => Assert.Equal("2024-06-15", Invoke("formatDate", S("2024-06-15"), S("YYYY-MM-DD")).AsString());

    [Fact]
    public void FormatDate_MmDdYyyy()
        => Assert.Equal("06/15/2024", Invoke("formatDate", S("2024-06-15"), S("MM/DD/YYYY")).AsString());

    [Fact]
    public void FormatDate_DdMmYyyy()
        => Assert.Equal("15-06-2024", Invoke("formatDate", S("2024-06-15"), S("DD-MM-YYYY")).AsString());

    [Fact]
    public void FormatDate_FromTimestamp()
        => Assert.Equal("2024-06-15", Invoke("formatDate", S("2024-06-15T14:30:00"), S("YYYY-MM-DD")).AsString());

    [Fact]
    public void FormatTime_HhMmSs()
        => Assert.Equal("14:30:45", Invoke("formatTime", S("2024-06-15T14:30:45"), S("HH:mm:ss")).AsString());

    [Fact]
    public void FormatTime_HhMm()
        => Assert.Equal("09:05", Invoke("formatTime", S("2024-06-15T09:05:00"), S("HH:mm")).AsString());

    [Fact]
    public void FormatTime_Midnight()
        => Assert.Equal("00:00:00", Invoke("formatTime", S("2024-06-15T00:00:00"), S("HH:mm:ss")).AsString());

    [Fact]
    public void FormatTime_EndOfDay()
        => Assert.Equal("23:59:59", Invoke("formatTime", S("2024-06-15T23:59:59"), S("HH:mm:ss")).AsString());

    // =========================================================================
    // 21. DATETIME: PARSE_DATE
    // =========================================================================

    [Fact]
    public void ParseDate_Basic()
        => Assert.Equal("2024-06-15", Invoke("parseDate", S("06/15/2024"), S("MM/DD/YYYY")).AsString());

    [Fact]
    public void ParseDate_European()
        => Assert.Equal("2024-06-15", Invoke("parseDate", S("15-06-2024"), S("DD-MM-YYYY")).AsString());

    // =========================================================================
    // 22. DATETIME: ADD MONTHS / YEARS
    // =========================================================================

    [Fact]
    public void AddMonths_Basic()
        => Assert.Equal("2024-04-15", Invoke("addMonths", S("2024-01-15"), I(3)).AsString());

    [Fact]
    public void AddMonths_YearBoundary()
        => Assert.Equal("2025-02-15", Invoke("addMonths", S("2024-11-15"), I(3)).AsString());

    [Fact]
    public void AddMonths_Negative()
        => Assert.Equal("2023-12-15", Invoke("addMonths", S("2024-03-15"), I(-3)).AsString());

    [Fact]
    public void AddMonths_JanToFebClamp()
        => Assert.Equal("2024-02-29", Invoke("addMonths", S("2024-01-31"), I(1)).AsString());

    [Fact]
    public void AddMonths_JanToFebNonLeap()
        => Assert.Equal("2023-02-28", Invoke("addMonths", S("2023-01-31"), I(1)).AsString());

    [Fact]
    public void AddMonths_Twelve()
        => Assert.Equal("2025-06-15", Invoke("addMonths", S("2024-06-15"), I(12)).AsString());

    [Fact]
    public void AddMonths_Zero()
        => Assert.Equal("2024-06-15", Invoke("addMonths", S("2024-06-15"), I(0)).AsString());

    [Fact]
    public void AddMonths_LargeNegative()
        => Assert.Equal("2022-06-15", Invoke("addMonths", S("2024-06-15"), I(-24)).AsString());

    [Fact]
    public void AddMonths_InvalidDate()
        => Assert.True(Invoke("addMonths", S("not-a-date"), I(1)).IsNull);

    [Fact]
    public void AddYears_Basic()
        => Assert.Equal("2029-06-15", Invoke("addYears", S("2024-06-15"), I(5)).AsString());

    [Fact]
    public void AddYears_Negative()
        => Assert.Equal("2021-06-15", Invoke("addYears", S("2024-06-15"), I(-3)).AsString());

    [Fact]
    public void AddYears_LeapDay()
        => Assert.Equal("2025-02-28", Invoke("addYears", S("2024-02-29"), I(1)).AsString());

    [Fact]
    public void AddYears_LeapDayToLeap()
        => Assert.Equal("2028-02-29", Invoke("addYears", S("2024-02-29"), I(4)).AsString());

    [Fact]
    public void AddYears_Zero()
        => Assert.Equal("2024-06-15", Invoke("addYears", S("2024-06-15"), I(0)).AsString());

    [Fact]
    public void AddYears_InvalidDate()
        => Assert.True(Invoke("addYears", S("not-a-date"), I(1)).IsNull);

    // =========================================================================
    // 23. DATETIME: START/END OF DAY/MONTH/YEAR
    // .NET startOfDay returns "yyyy-MM-ddT00:00:00.000Z"
    // .NET endOfDay returns "yyyy-MM-ddT23:59:59.999Z"
    // =========================================================================

    [Fact]
    public void StartOfDay_Basic()
    {
        var result = Invoke("startOfDay", S("2024-06-15T14:30:45")).AsString();
        Assert.NotNull(result);
        Assert.Contains("2024-06-15", result);
        Assert.Contains("00:00:00", result);
    }

    [Fact]
    public void EndOfDay_Basic()
    {
        var result = Invoke("endOfDay", S("2024-06-15T14:30:45")).AsString();
        Assert.NotNull(result);
        Assert.Contains("2024-06-15", result);
        Assert.Contains("23:59:59", result);
    }

    [Fact]
    public void StartOfDay_FromDateOnly()
    {
        var result = Invoke("startOfDay", S("2024-06-15")).AsString();
        Assert.NotNull(result);
        Assert.Contains("2024-06-15", result);
        Assert.Contains("00:00:00", result);
    }

    [Fact]
    public void EndOfDay_FromDateOnly()
    {
        var result = Invoke("endOfDay", S("2024-06-15")).AsString();
        Assert.NotNull(result);
        Assert.Contains("2024-06-15", result);
        Assert.Contains("23:59:59", result);
    }

    [Fact]
    public void StartOfMonth_Basic()
        => Assert.Equal("2024-06-01", Invoke("startOfMonth", S("2024-06-15")).AsString());

    [Fact]
    public void EndOfMonth_June()
        => Assert.Equal("2024-06-30", Invoke("endOfMonth", S("2024-06-15")).AsString());

    [Fact]
    public void EndOfMonth_FebLeap()
        => Assert.Equal("2024-02-29", Invoke("endOfMonth", S("2024-02-10")).AsString());

    [Fact]
    public void EndOfMonth_FebNonLeap()
        => Assert.Equal("2023-02-28", Invoke("endOfMonth", S("2023-02-10")).AsString());

    [Fact]
    public void EndOfMonth_December()
        => Assert.Equal("2024-12-31", Invoke("endOfMonth", S("2024-12-05")).AsString());

    [Fact]
    public void EndOfMonth_January()
        => Assert.Equal("2024-01-31", Invoke("endOfMonth", S("2024-01-15")).AsString());

    [Fact]
    public void EndOfMonth_March()
        => Assert.Equal("2024-03-31", Invoke("endOfMonth", S("2024-03-01")).AsString());

    [Fact]
    public void EndOfMonth_April()
        => Assert.Equal("2024-04-30", Invoke("endOfMonth", S("2024-04-01")).AsString());

    [Fact]
    public void StartOfYear_Basic()
        => Assert.Equal("2024-01-01", Invoke("startOfYear", S("2024-06-15")).AsString());

    [Fact]
    public void EndOfYear_Basic()
        => Assert.Equal("2024-12-31", Invoke("endOfYear", S("2024-06-15")).AsString());

    // =========================================================================
    // 24. DATETIME: DAY_OF_WEEK / WEEK_OF_YEAR / QUARTER
    // =========================================================================

    [Fact]
    public void DayOfWeek_Monday()
    {
        // 2024-01-01 is a Monday = 1
        AssertNumeric(Invoke("dayOfWeek", S("2024-01-01")), 1.0, 1e-10);
    }

    [Fact]
    public void DayOfWeek_Sunday()
        => AssertNumeric(Invoke("dayOfWeek", S("2024-01-07")), 0.0, 1e-10);

    [Fact]
    public void DayOfWeek_Saturday()
        => AssertNumeric(Invoke("dayOfWeek", S("2024-01-06")), 6.0, 1e-10);

    [Fact]
    public void DayOfWeek_InvalidDate()
        => Assert.True(Invoke("dayOfWeek", S("invalid")).IsNull);

    [Fact]
    public void WeekOfYear_Jan1()
        => AssertNumeric(Invoke("weekOfYear", S("2024-01-01")), 1.0, 1e-10);

    [Fact]
    public void WeekOfYear_Dec31()
    {
        var result = Invoke("weekOfYear", S("2024-12-31"));
        var v = result.AsDouble() ?? (double?)result.AsInt64();
        Assert.NotNull(v);
        Assert.True(v.Value >= 52 && v.Value <= 53, $"week={v.Value}");
    }

    [Fact]
    public void Quarter_Q1()
        => AssertNumeric(Invoke("quarter", S("2024-02-15")), 1.0, 1e-10);

    [Fact]
    public void Quarter_Q2()
        => AssertNumeric(Invoke("quarter", S("2024-06-15")), 2.0, 1e-10);

    [Fact]
    public void Quarter_Q3()
        => AssertNumeric(Invoke("quarter", S("2024-09-15")), 3.0, 1e-10);

    [Fact]
    public void Quarter_Q4()
        => AssertNumeric(Invoke("quarter", S("2024-12-15")), 4.0, 1e-10);

    [Fact]
    public void Quarter_InvalidDate()
        => Assert.True(Invoke("quarter", S("bad-date")).IsNull);

    // =========================================================================
    // 25. IS_LEAP_YEAR
    // =========================================================================

    [Fact]
    public void IsLeapYear_2024()
        => Assert.Equal(true, Invoke("isLeapYear", S("2024-01-01")).AsBool());

    [Fact]
    public void IsLeapYear_2023()
        => Assert.Equal(false, Invoke("isLeapYear", S("2023-06-01")).AsBool());

    [Fact]
    public void IsLeapYear_2000()
        => Assert.Equal(true, Invoke("isLeapYear", S("2000-01-01")).AsBool());

    [Fact]
    public void IsLeapYear_1900()
        => Assert.Equal(false, Invoke("isLeapYear", S("1900-01-01")).AsBool());

    // =========================================================================
    // 26. DATE COMPARISON: IS_BEFORE / IS_AFTER / IS_BETWEEN
    // =========================================================================

    [Fact]
    public void IsBefore_True()
        => Assert.Equal(true, Invoke("isBefore", S("2024-01-01"), S("2024-12-31")).AsBool());

    [Fact]
    public void IsBefore_False()
        => Assert.Equal(false, Invoke("isBefore", S("2024-12-31"), S("2024-01-01")).AsBool());

    [Fact]
    public void IsBefore_Equal()
        => Assert.Equal(false, Invoke("isBefore", S("2024-06-15"), S("2024-06-15")).AsBool());

    [Fact]
    public void IsBefore_Timestamps()
        => Assert.Equal(true, Invoke("isBefore", S("2024-06-15T10:00:00"), S("2024-06-15T14:00:00")).AsBool());

    [Fact]
    public void IsAfter_True()
        => Assert.Equal(true, Invoke("isAfter", S("2024-12-31"), S("2024-01-01")).AsBool());

    [Fact]
    public void IsAfter_False()
        => Assert.Equal(false, Invoke("isAfter", S("2024-01-01"), S("2024-12-31")).AsBool());

    [Fact]
    public void IsAfter_Equal()
        => Assert.Equal(false, Invoke("isAfter", S("2024-06-15"), S("2024-06-15")).AsBool());

    [Fact]
    public void IsAfter_Timestamps()
        => Assert.Equal(true, Invoke("isAfter", S("2024-06-15T14:00:00"), S("2024-06-15T10:00:00")).AsBool());

    [Fact]
    public void IsBetween_True()
        => Assert.Equal(true, Invoke("isBetween", S("2024-06-15"), S("2024-01-01"), S("2024-12-31")).AsBool());

    [Fact]
    public void IsBetween_FalseBefore()
        => Assert.Equal(false, Invoke("isBetween", S("2023-06-15"), S("2024-01-01"), S("2024-12-31")).AsBool());

    [Fact]
    public void IsBetween_FalseAfter()
        => Assert.Equal(false, Invoke("isBetween", S("2025-06-15"), S("2024-01-01"), S("2024-12-31")).AsBool());

    [Fact]
    public void IsBetween_Timestamps()
        => Assert.Equal(true, Invoke("isBetween",
            S("2024-06-15T12:00:00"),
            S("2024-06-15T00:00:00"),
            S("2024-06-15T23:59:59")).AsBool());

    // =========================================================================
    // 27. DAYS_BETWEEN_DATES / DATE_DIFF
    // .NET dateDiff(a,b) returns signed days (no unit parameter)
    // =========================================================================

    [Fact]
    public void DaysBetween_SameDate()
        => AssertNumeric(Invoke("daysBetweenDates", S("2024-06-15"), S("2024-06-15")), 0.0, 1e-10);

    [Fact]
    public void DaysBetween_OneDay()
        => AssertNumeric(Invoke("daysBetweenDates", S("2024-06-15"), S("2024-06-16")), 1.0, 1e-10);

    [Fact]
    public void DaysBetween_ReversedOrder()
        => AssertNumeric(Invoke("daysBetweenDates", S("2024-06-16"), S("2024-06-15")), 1.0, 1e-10);

    [Fact]
    public void DaysBetween_LeapYear()
        => AssertNumeric(Invoke("daysBetweenDates", S("2024-02-28"), S("2024-03-01")), 2.0, 1e-10);

    [Fact]
    public void DaysBetween_NonLeapYear()
        => AssertNumeric(Invoke("daysBetweenDates", S("2023-02-28"), S("2023-03-01")), 1.0, 1e-10);

    [Fact]
    public void DaysBetween_YearBoundary()
        => AssertNumeric(Invoke("daysBetweenDates", S("2023-12-31"), S("2024-01-01")), 1.0, 1e-10);

    [Fact]
    public void DaysBetween_FullLeapYear()
        => AssertNumeric(Invoke("daysBetweenDates", S("2024-01-01"), S("2025-01-01")), 366.0, 1e-10);

    [Fact]
    public void DaysBetween_FullNonLeapYear()
        => AssertNumeric(Invoke("daysBetweenDates", S("2023-01-01"), S("2024-01-01")), 365.0, 1e-10);

    [Fact]
    public void DateDiff_Days()
    {
        // dateDiff returns signed day count (no unit arg)
        var result = Invoke("dateDiff", S("2024-01-01"), S("2024-01-31"));
        Assert.Equal(30L, result.AsInt64());
    }

    [Fact]
    public void DateDiff_Months()
    {
        // dateDiff only returns days, so 3 months of days
        var result = Invoke("dateDiff", S("2024-01-15"), S("2024-04-15"));
        // Jan15->Apr15 = 31-15+29+31+15 = 91 days
        var v = result.AsInt64();
        Assert.NotNull(v);
        Assert.True(v.Value > 80 && v.Value < 100, $"dateDiff={v.Value}");
    }

    [Fact]
    public void DateDiff_Years()
    {
        // dateDiff only returns days
        var result = Invoke("dateDiff", S("2020-06-15"), S("2024-06-15"));
        var v = result.AsInt64();
        Assert.NotNull(v);
        Assert.True(v.Value > 1400 && v.Value < 1470, $"dateDiff={v.Value}");
    }

    // =========================================================================
    // 28. RANDOM
    // =========================================================================

    [Fact]
    public void Random_NoArgs()
    {
        var result = Invoke("random");
        var v = result.AsDouble();
        Assert.True(v.HasValue, "random should return float");
        Assert.True(v.Value >= 0.0 && v.Value < 1.0, $"random={v.Value}");
    }

    [Fact]
    public void Random_WithMax()
    {
        var result = Invoke("random", I(100));
        var d = result.AsDouble();
        var i = result.AsInt64();
        if (d.HasValue)
            Assert.True(d.Value >= 0.0 && d.Value < 100.0, $"random={d.Value}");
        else if (i.HasValue)
            Assert.True(i.Value >= 0 && i.Value < 100, $"random={i.Value}");
        else
            Assert.Fail("Expected numeric result");
    }
}
