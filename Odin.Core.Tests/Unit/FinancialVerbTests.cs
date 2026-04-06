using System;
using System.Collections.Generic;
using Odin.Core.Types;
using Odin.Core.Transform;
using Odin.Core.Transform.Verbs;
using Xunit;

namespace Odin.Core.Tests.Unit;

/// <summary>
/// Tests for financial, statistical, and numeric edge case verbs.
/// Ported from Rust SDK numeric_verbs.rs extended_tests.
/// </summary>
public class FinancialVerbTests
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
                $"Float({d.Value}) not close to {expected} (tolerance {tolerance})");
        else if (i.HasValue)
            Assert.True(Math.Abs((double)i.Value - expected) < tolerance,
                $"Integer({i.Value}) not close to {expected} (tolerance {tolerance})");
        else
            Assert.Fail($"Expected numeric, got {result.Type}");
    }

    // =========================================================================
    // formatNumber — extended
    // =========================================================================

    [Fact]
    public void FormatNumber_ZeroDecimals()
    {
        Assert.Equal(S("3"), Invoke("formatNumber", F(3.14159), I(0)));
    }

    [Fact]
    public void FormatNumber_TwoDecimals()
    {
        Assert.Equal(S("3.14"), Invoke("formatNumber", F(3.14159), I(2)));
    }

    [Fact]
    public void FormatNumber_ManyDecimals()
    {
        Assert.Equal(S("1.00000"), Invoke("formatNumber", F(1.0), I(5)));
    }

    [Fact]
    public void FormatNumber_Negative()
    {
        Assert.Equal(S("-42.6"), Invoke("formatNumber", F(-42.567), I(1)));
    }

    [Fact]
    public void FormatNumber_FromInteger()
    {
        Assert.Equal(S("100.00"), Invoke("formatNumber", I(100), I(2)));
    }

    [Fact]
    public void FormatNumber_FromString()
    {
        Assert.Equal(S("99.900"), Invoke("formatNumber", S("99.9"), I(3)));
    }

    // =========================================================================
    // formatInteger — extended
    // =========================================================================

    [Fact]
    public void FormatInteger_Basic()
    {
        Assert.Equal(S("3"), Invoke("formatInteger", F(3.7)));
    }

    [Fact]
    public void FormatInteger_Negative()
    {
        Assert.Equal(S("-2"), Invoke("formatInteger", F(-2.3)));
    }

    [Fact]
    public void FormatInteger_FromInt()
    {
        Assert.Equal(S("42"), Invoke("formatInteger", I(42)));
    }

    // =========================================================================
    // formatCurrency — extended
    // =========================================================================

    [Fact]
    public void FormatCurrency_Basic()
    {
        Assert.Equal(S("1234.50"), Invoke("formatCurrency", F(1234.5)));
    }

    [Fact]
    public void FormatCurrency_Negative()
    {
        Assert.Equal(S("-100.00"), Invoke("formatCurrency", F(-99.999)));
    }

    [Fact]
    public void FormatCurrency_Zero()
    {
        Assert.Equal(S("0.00"), Invoke("formatCurrency", F(0.0)));
    }

    // =========================================================================
    // formatPercent — extended
    // =========================================================================

    [Fact]
    public void FormatPercent_Basic()
    {
        Assert.Equal(S("85%"), Invoke("formatPercent", F(0.85), I(0)));
    }

    [Fact]
    public void FormatPercent_WithDecimals()
    {
        Assert.Equal(S("85.7%"), Invoke("formatPercent", F(0.8567), I(1)));
    }

    [Fact]
    public void FormatPercent_Zero()
    {
        Assert.Equal(S("0%"), Invoke("formatPercent", F(0.0), I(0)));
    }

    [Fact]
    public void FormatPercent_OverOne()
    {
        Assert.Equal(S("150%"), Invoke("formatPercent", F(1.5), I(0)));
    }

    // =========================================================================
    // floor / ceil / negate / sign / trunc — extended
    // =========================================================================

    [Fact]
    public void Floor_Positive()
    {
        AssertNumeric(Invoke("floor", F(3.7)), 3.0, 1e-10);
    }

    [Fact]
    public void Floor_Negative()
    {
        AssertNumeric(Invoke("floor", F(-3.2)), -4.0, 1e-10);
    }

    [Fact]
    public void Floor_Exact()
    {
        AssertNumeric(Invoke("floor", F(5.0)), 5.0, 1e-10);
    }

    [Fact]
    public void Ceil_Positive()
    {
        AssertNumeric(Invoke("ceil", F(3.1)), 4.0, 1e-10);
    }

    [Fact]
    public void Ceil_Negative()
    {
        AssertNumeric(Invoke("ceil", F(-3.7)), -3.0, 1e-10);
    }

    [Fact]
    public void Ceil_Exact()
    {
        AssertNumeric(Invoke("ceil", F(5.0)), 5.0, 1e-10);
    }

    [Fact]
    public void Negate_Positive()
    {
        AssertNumeric(Invoke("negate", I(42)), -42.0, 1e-10);
    }

    [Fact]
    public void Negate_Negative()
    {
        AssertNumeric(Invoke("negate", F(-3.14)), 3.14, 1e-10);
    }

    [Fact]
    public void Negate_Zero()
    {
        AssertNumeric(Invoke("negate", I(0)), 0.0, 1e-10);
    }

    [Fact]
    public void Sign_Positive()
    {
        AssertNumeric(Invoke("sign", F(42.0)), 1.0, 1e-10);
    }

    [Fact]
    public void Sign_Negative()
    {
        AssertNumeric(Invoke("sign", F(-5.0)), -1.0, 1e-10);
    }

    [Fact]
    public void Sign_Zero()
    {
        AssertNumeric(Invoke("sign", F(0.0)), 0.0, 1e-10);
    }

    [Fact]
    public void Trunc_Positive()
    {
        AssertNumeric(Invoke("trunc", F(3.9)), 3.0, 1e-10);
    }

    [Fact]
    public void Trunc_Negative()
    {
        AssertNumeric(Invoke("trunc", F(-3.9)), -3.0, 1e-10);
    }

    // =========================================================================
    // parseInt — extended
    // =========================================================================

    [Fact]
    public void ParseInt_Basic()
    {
        AssertNumeric(Invoke("parseInt", S("42")), 42.0, 1e-10);
    }

    [Fact]
    public void ParseInt_Negative()
    {
        AssertNumeric(Invoke("parseInt", S("-99")), -99.0, 1e-10);
    }

    [Fact]
    public void ParseInt_FromInteger()
    {
        AssertNumeric(Invoke("parseInt", I(77)), 77.0, 1e-10);
    }

    [Fact]
    public void ParseInt_FromFloat()
    {
        AssertNumeric(Invoke("parseInt", F(3.7)), 3.0, 1e-10);
    }

    // =========================================================================
    // isFinite / isNaN — extended
    // =========================================================================

    [Fact]
    public void IsFinite_Normal()
    {
        Assert.Equal(B(true), Invoke("isFinite", F(42.0)));
    }

    [Fact]
    public void IsFinite_Infinity()
    {
        Assert.Equal(B(false), Invoke("isFinite", F(double.PositiveInfinity)));
    }

    [Fact]
    public void IsFinite_NegInfinity()
    {
        Assert.Equal(B(false), Invoke("isFinite", F(double.NegativeInfinity)));
    }

    [Fact]
    public void IsFinite_NaN()
    {
        Assert.Equal(B(false), Invoke("isFinite", F(double.NaN)));
    }

    [Fact]
    public void IsNaN_Normal()
    {
        Assert.Equal(B(false), Invoke("isNaN", F(42.0)));
    }

    [Fact]
    public void IsNaN_NaN()
    {
        Assert.Equal(B(true), Invoke("isNaN", F(double.NaN)));
    }

    // =========================================================================
    // minOf / maxOf — extended
    // =========================================================================

    [Fact]
    public void MinOf_Basic()
    {
        AssertNumeric(Invoke("minOf", I(3), I(1), I(2)), 1.0, 1e-10);
    }

    [Fact]
    public void MinOf_Negative()
    {
        AssertNumeric(Invoke("minOf", F(-5.0), F(-3.0), F(-10.0)), -10.0, 1e-10);
    }

    [Fact]
    public void MinOf_Single()
    {
        AssertNumeric(Invoke("minOf", I(42)), 42.0, 1e-10);
    }

    [Fact]
    public void MaxOf_Basic()
    {
        AssertNumeric(Invoke("maxOf", I(3), I(1), I(2)), 3.0, 1e-10);
    }

    [Fact]
    public void MaxOf_Negative()
    {
        AssertNumeric(Invoke("maxOf", F(-5.0), F(-3.0), F(-10.0)), -3.0, 1e-10);
    }

    [Fact]
    public void MaxOf_Single()
    {
        AssertNumeric(Invoke("maxOf", I(42)), 42.0, 1e-10);
    }

    [Fact]
    public void MaxOf_LargeNumbers()
    {
        AssertNumeric(Invoke("maxOf", F(1e15), F(1e14), F(1e16)), 1e16, 1e6);
    }

    // =========================================================================
    // safeDivide — extended
    // =========================================================================

    [Fact]
    public void SafeDivide_Normal()
    {
        AssertNumeric(Invoke("safeDivide", F(10.0), F(3.0), F(0.0)), 10.0 / 3.0, 1e-10);
    }

    [Fact]
    public void SafeDivide_ByZero()
    {
        // Returns the fallback (3rd arg)
        AssertNumeric(Invoke("safeDivide", F(10.0), F(0.0), F(-1.0)), -1.0, 1e-10);
    }

    [Fact]
    public void SafeDivide_ZeroNumerator()
    {
        AssertNumeric(Invoke("safeDivide", F(0.0), F(5.0), F(0.0)), 0.0, 1e-10);
    }

    // =========================================================================
    // Math: log, ln, log10, exp, pow, sqrt — extended
    // =========================================================================

    [Fact]
    public void Log_Base2()
    {
        AssertNumeric(Invoke("log", F(8.0), F(2.0)), 3.0, 1e-10);
    }

    [Fact]
    public void Log_Base10()
    {
        AssertNumeric(Invoke("log", F(1000.0), F(10.0)), 3.0, 1e-10);
    }

    [Fact]
    public void Ln_E()
    {
        AssertNumeric(Invoke("ln", F(Math.E)), 1.0, 1e-10);
    }

    [Fact]
    public void Ln_One()
    {
        AssertNumeric(Invoke("ln", F(1.0)), 0.0, 1e-10);
    }

    [Fact]
    public void Log10_Hundred()
    {
        AssertNumeric(Invoke("log10", F(100.0)), 2.0, 1e-10);
    }

    [Fact]
    public void Exp_Zero()
    {
        AssertNumeric(Invoke("exp", F(0.0)), 1.0, 1e-10);
    }

    [Fact]
    public void Exp_One()
    {
        AssertNumeric(Invoke("exp", F(1.0)), Math.E, 1e-10);
    }

    [Fact]
    public void Pow_Basic()
    {
        AssertNumeric(Invoke("pow", F(2.0), F(10.0)), 1024.0, 1e-10);
    }

    [Fact]
    public void Pow_Fractional()
    {
        AssertNumeric(Invoke("pow", F(4.0), F(0.5)), 2.0, 1e-10);
    }

    [Fact]
    public void Pow_ZeroExponent()
    {
        AssertNumeric(Invoke("pow", F(99.0), F(0.0)), 1.0, 1e-10);
    }

    [Fact]
    public void Sqrt_Perfect()
    {
        AssertNumeric(Invoke("sqrt", F(144.0)), 12.0, 1e-10);
    }

    [Fact]
    public void Sqrt_Zero()
    {
        AssertNumeric(Invoke("sqrt", F(0.0)), 0.0, 1e-10);
    }

    [Fact]
    public void Sqrt_NonPerfect()
    {
        AssertNumeric(Invoke("sqrt", F(2.0)), Math.Sqrt(2.0), 1e-10);
    }

    // =========================================================================
    // Compound / Discount — TVM
    // =========================================================================

    [Fact]
    public void Compound_Basic()
    {
        // 1000 * (1 + 0.05)^10
        AssertNumeric(Invoke("compound", F(1000.0), F(0.05), I(10)),
            1000.0 * Math.Pow(1.05, 10), 0.01);
    }

    [Fact]
    public void Compound_ZeroRate()
    {
        AssertNumeric(Invoke("compound", F(1000.0), F(0.0), I(10)), 1000.0, 0.01);
    }

    [Fact]
    public void Compound_OnePeriod()
    {
        AssertNumeric(Invoke("compound", F(500.0), F(0.10), I(1)), 550.0, 0.01);
    }

    [Fact]
    public void Compound_LargePeriods()
    {
        AssertNumeric(Invoke("compound", F(100.0), F(0.07), I(30)),
            100.0 * Math.Pow(1.07, 30), 0.01);
    }

    [Fact]
    public void Discount_Basic()
    {
        AssertNumeric(Invoke("discount", F(1000.0), F(0.05), I(10)),
            1000.0 / Math.Pow(1.05, 10), 0.01);
    }

    [Fact]
    public void Discount_ZeroRate()
    {
        AssertNumeric(Invoke("discount", F(1000.0), F(0.0), I(5)), 1000.0, 0.01);
    }

    [Fact]
    public void Discount_OnePeriod()
    {
        AssertNumeric(Invoke("discount", F(1100.0), F(0.10), I(1)), 1000.0, 0.01);
    }

    // =========================================================================
    // pmt / fv / pv — TVM
    // =========================================================================

    [Fact]
    public void Pmt_Basic()
    {
        // pmt(principal=200000, rate=0.05/12, periods=360)
        double rate = 0.05 / 12.0;
        double n = 360.0;
        double p = 200000.0;
        double expected = p * rate * Math.Pow(1.0 + rate, n) / (Math.Pow(1.0 + rate, n) - 1.0);
        var result = Invoke("pmt", F(p), F(rate), F(n));
        AssertNumeric(result, expected, 1.0);
    }

    [Fact]
    public void Pmt_ZeroRate()
    {
        // pmt(principal=12000, rate=0, periods=12) = 12000 / 12 = 1000
        AssertNumeric(Invoke("pmt", F(12000.0), F(0.0), F(12.0)), 1000.0, 0.01);
    }

    [Fact]
    public void Pmt_OnePeriod()
    {
        // pmt(principal=1000, rate=0.1, periods=1) = 1000 * 0.1 * 1.1 / (1.1 - 1) = 1100
        AssertNumeric(Invoke("pmt", F(1000.0), F(0.1), F(1.0)), 1100.0, 0.01);
    }

    [Fact]
    public void Fv_ZeroRate()
    {
        // fv(payment=100, rate=0, periods=12) = 100 * 12 = 1200
        AssertNumeric(Invoke("fv", F(100.0), F(0.0), F(12.0)), 1200.0, 0.01);
    }

    [Fact]
    public void Fv_ZeroRate_WithPmt()
    {
        // fv(payment=100, rate=0, periods=12) = 100 * 12 = 1200
        AssertNumeric(Invoke("fv", F(100.0), F(0.0), F(12.0)), 1200.0, 0.01);
    }

    [Fact]
    public void Pv_Basic()
    {
        // pv(payment=1000, rate=0.05, periods=10) = 1000 * (1 - (1.05)^-10) / 0.05
        double expected = 1000.0 * (1.0 - Math.Pow(1.05, -10.0)) / 0.05;
        AssertNumeric(Invoke("pv", F(1000.0), F(0.05), F(10.0)),
            expected, 0.01);
    }

    // =========================================================================
    // rate / nper — TVM
    // =========================================================================

    [Fact]
    public void Rate_Basic()
    {
        var result = Invoke("rate", F(10.0), F(-100.0), F(1000.0), F(0.0));
        Assert.True(result.AsDouble().HasValue);
        Assert.True(double.IsFinite(result.AsDouble()!.Value));
    }

    [Fact]
    public void Nper_Basic()
    {
        var result = Invoke("nper", F(0.01), F(-100.0), F(5000.0));
        Assert.True(result.AsDouble().HasValue || result.AsInt64().HasValue);
    }

    [Fact]
    public void Nper_ZeroRate()
    {
        // nper(0, -100, 5000) = -5000 / -100 = 50
        AssertNumeric(Invoke("nper", F(0.0), F(-100.0), F(5000.0)), 50.0, 0.01);
    }

    // =========================================================================
    // NPV — extended
    // =========================================================================

    [Fact]
    public void Npv_Basic()
    {
        var flows = Arr(F(-1000.0), F(300.0), F(400.0), F(500.0));
        var result = Invoke("npv", F(0.1), flows);
        // t=0 indexing: sum of cf[t] / (1+r)^t
        double expected = -1000.0 + 300.0 / 1.1 + 400.0 / Math.Pow(1.1, 2) + 500.0 / Math.Pow(1.1, 3);
        AssertNumeric(result, expected, 0.01);
    }

    [Fact]
    public void Npv_ZeroRate()
    {
        var flows = Arr(F(-1000.0), F(500.0), F(500.0));
        var result = Invoke("npv", F(0.0), flows);
        AssertNumeric(result, 0.0, 0.01);
    }

    [Fact]
    public void Npv_SingleFlow()
    {
        var flows = Arr(F(1000.0));
        var result = Invoke("npv", F(0.1), flows);
        // t=0: 1000 / (1.1)^0 = 1000
        AssertNumeric(result, 1000.0, 0.01);
    }

    [Fact]
    public void Npv_HighRate()
    {
        var flows = Arr(F(-100.0), F(50.0), F(50.0), F(50.0));
        var result = Invoke("npv", F(0.5), flows);
        // t=0 indexing
        double e = -100.0 + 50.0 / 1.5 + 50.0 / Math.Pow(1.5, 2) + 50.0 / Math.Pow(1.5, 3);
        AssertNumeric(result, e, 0.01);
    }

    // =========================================================================
    // IRR — extended
    // =========================================================================

    [Fact]
    public void Irr_Simple()
    {
        // -100, +110 => IRR = 0.10
        var flows = Arr(F(-100.0), F(110.0));
        AssertNumeric(Invoke("irr", flows), 0.10, 0.001);
    }

    [Fact]
    public void Irr_Basic()
    {
        var flows = Arr(F(-1000.0), F(300.0), F(400.0), F(500.0));
        var result = Invoke("irr", flows);
        var v = result.AsDouble()!.Value;
        Assert.True(v > 0.0 && v < 0.5, $"IRR={v} out of range");
    }

    [Fact]
    public void Irr_EvenCashFlows()
    {
        var flows = Arr(F(-1000.0), F(400.0), F(400.0), F(400.0));
        var result = Invoke("irr", flows);
        var v = result.AsDouble()!.Value;
        Assert.True(v > 0.05 && v < 0.15, $"IRR={v}");
    }

    // =========================================================================
    // Depreciation — extended
    // =========================================================================

    [Fact]
    public void Depreciation_Basic()
    {
        // (10000 - 1000) / 5 = 1800
        AssertNumeric(Invoke("depreciation", F(10000.0), F(1000.0), F(5.0)), 1800.0, 0.01);
    }

    [Fact]
    public void Depreciation_NoSalvage()
    {
        AssertNumeric(Invoke("depreciation", F(5000.0), F(0.0), F(10.0)), 500.0, 0.01);
    }

    [Fact]
    public void Depreciation_ZeroLife()
    {
        Assert.True(Invoke("depreciation", F(5000.0), F(0.0), F(0.0)).IsNull);
    }

    // =========================================================================
    // Statistics: std, variance, median, mode — extended
    // =========================================================================

    [Fact]
    public void Std_Basic()
    {
        var a = Arr(F(2.0), F(4.0), F(4.0), F(4.0), F(5.0), F(5.0), F(7.0), F(9.0));
        AssertNumeric(Invoke("std", a), 2.0, 0.01);
    }

    [Fact]
    public void Std_Uniform()
    {
        AssertNumeric(Invoke("std", Arr(F(5.0), F(5.0), F(5.0))), 0.0, 1e-10);
    }

    [Fact]
    public void Variance_Basic()
    {
        var a = Arr(F(2.0), F(4.0), F(4.0), F(4.0), F(5.0), F(5.0), F(7.0), F(9.0));
        AssertNumeric(Invoke("variance", a), 4.0, 0.01);
    }

    [Fact]
    public void Median_Odd()
    {
        AssertNumeric(Invoke("median", Arr(F(3.0), F(1.0), F(2.0))), 2.0, 1e-10);
    }

    [Fact]
    public void Median_Even()
    {
        AssertNumeric(Invoke("median", Arr(F(1.0), F(2.0), F(3.0), F(4.0))), 2.5, 1e-10);
    }

    [Fact]
    public void Median_Single()
    {
        AssertNumeric(Invoke("median", Arr(F(42.0))), 42.0, 1e-10);
    }

    [Fact]
    public void Mode_Basic()
    {
        AssertNumeric(Invoke("mode", Arr(F(1.0), F(2.0), F(2.0), F(3.0))), 2.0, 1e-10);
    }

    [Fact]
    public void Mode_AllSame()
    {
        AssertNumeric(Invoke("mode", Arr(F(7.0), F(7.0), F(7.0))), 7.0, 1e-10);
    }

    // =========================================================================
    // stdSample / varianceSample — extended
    // =========================================================================

    [Fact]
    public void StdSample_Basic()
    {
        var a = Arr(F(2.0), F(4.0), F(4.0), F(4.0), F(5.0), F(5.0), F(7.0), F(9.0));
        AssertNumeric(Invoke("stdSample", a), Math.Sqrt(32.0 / 7.0), 0.01);
    }

    [Fact]
    public void StdSample_TooFew()
    {
        Assert.True(Invoke("stdSample", Arr(F(5.0))).IsNull);
    }

    [Fact]
    public void VarianceSample_Basic()
    {
        var a = Arr(F(2.0), F(4.0), F(4.0), F(4.0), F(5.0), F(5.0), F(7.0), F(9.0));
        AssertNumeric(Invoke("varianceSample", a), 32.0 / 7.0, 0.01);
    }

    // =========================================================================
    // percentile / quantile — extended
    // =========================================================================

    [Fact]
    public void Percentile_50th()
    {
        AssertNumeric(Invoke("percentile", Arr(F(1.0), F(2.0), F(3.0), F(4.0), F(5.0)), F(50.0)),
            3.0, 1e-10);
    }

    [Fact]
    public void Percentile_0th()
    {
        AssertNumeric(Invoke("percentile", Arr(F(1.0), F(2.0), F(3.0)), F(0.0)), 1.0, 1e-10);
    }

    [Fact]
    public void Percentile_100th()
    {
        AssertNumeric(Invoke("percentile", Arr(F(1.0), F(2.0), F(3.0)), F(100.0)), 3.0, 1e-10);
    }

    [Fact]
    public void Quantile_Half()
    {
        AssertNumeric(Invoke("quantile", Arr(F(10.0), F(20.0), F(30.0)), F(0.5)), 20.0, 1e-10);
    }

    // =========================================================================
    // covariance / correlation — extended
    // =========================================================================

    [Fact]
    public void Covariance_PerfectPositive()
    {
        AssertNumeric(Invoke("covariance",
            Arr(F(1.0), F(2.0), F(3.0)),
            Arr(F(2.0), F(4.0), F(6.0))), 4.0 / 3.0, 1e-10);
    }

    [Fact]
    public void Correlation_PerfectPositive()
    {
        AssertNumeric(Invoke("correlation",
            Arr(F(1.0), F(2.0), F(3.0)),
            Arr(F(2.0), F(4.0), F(6.0))), 1.0, 1e-10);
    }

    [Fact]
    public void Correlation_PerfectNegative()
    {
        AssertNumeric(Invoke("correlation",
            Arr(F(1.0), F(2.0), F(3.0)),
            Arr(F(6.0), F(4.0), F(2.0))), -1.0, 1e-10);
    }

    // =========================================================================
    // zscore — extended
    // =========================================================================

    [Fact]
    public void Zscore_AtMean()
    {
        // zscore(value, mean, stddev)
        AssertNumeric(Invoke("zscore", F(5.0), F(5.0), F(2.0)), 0.0, 1e-10);
    }

    [Fact]
    public void Zscore_AboveMean()
    {
        var result = Invoke("zscore", F(7.0), F(5.0), F(2.0));
        AssertNumeric(result, 1.0, 1e-10);
    }

    [Fact]
    public void Zscore_BelowMean()
    {
        var result = Invoke("zscore", F(3.0), F(5.0), F(2.0));
        AssertNumeric(result, -1.0, 1e-10);
    }

    [Fact]
    public void Zscore_ZeroStddev()
    {
        Assert.True(Invoke("zscore", F(5.0), F(5.0), F(0.0)).IsNull);
    }

    // =========================================================================
    // clamp / interpolate / weightedAvg — extended
    // =========================================================================

    [Fact]
    public void Clamp_WithinRange()
    {
        AssertNumeric(Invoke("clamp", F(5.0), F(1.0), F(10.0)), 5.0, 1e-10);
    }

    [Fact]
    public void Clamp_BelowMin()
    {
        AssertNumeric(Invoke("clamp", F(-5.0), F(0.0), F(10.0)), 0.0, 1e-10);
    }

    [Fact]
    public void Clamp_AboveMax()
    {
        AssertNumeric(Invoke("clamp", F(15.0), F(0.0), F(10.0)), 10.0, 1e-10);
    }

    [Fact]
    public void Interpolate_Midpoint()
    {
        // interpolate(a, b, t) = a + (b - a) * t
        AssertNumeric(Invoke("interpolate", F(0.0), F(100.0), F(0.5)), 50.0, 1e-10);
    }

    [Fact]
    public void Interpolate_AtStart()
    {
        AssertNumeric(Invoke("interpolate", F(0.0), F(100.0), F(0.0)), 0.0, 1e-10);
    }

    [Fact]
    public void Interpolate_AtEnd()
    {
        AssertNumeric(Invoke("interpolate", F(0.0), F(100.0), F(1.0)), 100.0, 1e-10);
    }

    [Fact]
    public void WeightedAvg_Basic()
    {
        // (80*1 + 90*2 + 100*1) / (1+2+1) = 360/4 = 90
        AssertNumeric(Invoke("weightedAvg",
            Arr(F(80.0), F(90.0), F(100.0)),
            Arr(F(1.0), F(2.0), F(1.0))), 90.0, 1e-10);
    }

    [Fact]
    public void WeightedAvg_EqualWeights()
    {
        AssertNumeric(Invoke("weightedAvg",
            Arr(F(10.0), F(20.0), F(30.0)),
            Arr(F(1.0), F(1.0), F(1.0))), 20.0, 1e-10);
    }

    // =========================================================================
    // mod — extended
    // =========================================================================

    [Fact]
    public void Mod_Basic()
    {
        AssertNumeric(Invoke("mod", I(10), I(3)), 1.0, 1e-10);
    }

    [Fact]
    public void Mod_NoRemainder()
    {
        AssertNumeric(Invoke("mod", I(9), I(3)), 0.0, 1e-10);
    }

    [Fact]
    public void Mod_Float()
    {
        AssertNumeric(Invoke("mod", F(10.5), F(3.0)), 1.5, 1e-10);
    }

    [Fact]
    public void Mod_ByZero()
    {
        Assert.True(Invoke("mod", I(10), I(0)).IsNull);
    }

    // =========================================================================
    // add / subtract / multiply / divide / abs / round — extended
    // =========================================================================

    [Fact]
    public void Add_Integers()
    {
        AssertNumeric(Invoke("add", I(3), I(4)), 7.0, 1e-10);
    }

    [Fact]
    public void Add_Floats()
    {
        AssertNumeric(Invoke("add", F(1.5), F(2.5)), 4.0, 1e-10);
    }

    [Fact]
    public void Add_Negative()
    {
        AssertNumeric(Invoke("add", I(5), I(-3)), 2.0, 1e-10);
    }

    [Fact]
    public void Subtract_Basic()
    {
        AssertNumeric(Invoke("subtract", I(10), I(3)), 7.0, 1e-10);
    }

    [Fact]
    public void Subtract_Negative()
    {
        AssertNumeric(Invoke("subtract", I(5), I(10)), -5.0, 1e-10);
    }

    [Fact]
    public void Multiply_Basic()
    {
        AssertNumeric(Invoke("multiply", I(3), I(4)), 12.0, 1e-10);
    }

    [Fact]
    public void Multiply_Float()
    {
        AssertNumeric(Invoke("multiply", F(2.5), F(4.0)), 10.0, 1e-10);
    }

    [Fact]
    public void Multiply_ByZero()
    {
        AssertNumeric(Invoke("multiply", I(999), I(0)), 0.0, 1e-10);
    }

    [Fact]
    public void Divide_Basic()
    {
        AssertNumeric(Invoke("divide", I(10), I(2)), 5.0, 1e-10);
    }

    [Fact]
    public void Divide_Float()
    {
        AssertNumeric(Invoke("divide", F(7.0), F(2.0)), 3.5, 1e-10);
    }

    [Fact]
    public void Divide_ByZero()
    {
        Assert.True(Invoke("divide", I(10), I(0)).IsNull);
    }

    [Fact]
    public void Abs_Positive()
    {
        AssertNumeric(Invoke("abs", I(42)), 42.0, 1e-10);
    }

    [Fact]
    public void Abs_Negative()
    {
        AssertNumeric(Invoke("abs", I(-42)), 42.0, 1e-10);
    }

    [Fact]
    public void Abs_Zero()
    {
        AssertNumeric(Invoke("abs", I(0)), 0.0, 1e-10);
    }

    [Fact]
    public void Abs_Float()
    {
        AssertNumeric(Invoke("abs", F(-3.14)), 3.14, 1e-10);
    }

    [Fact]
    public void Round_Basic()
    {
        AssertNumeric(Invoke("round", F(3.7)), 4.0, 1e-10);
    }

    [Fact]
    public void Round_Down()
    {
        AssertNumeric(Invoke("round", F(3.2)), 3.0, 1e-10);
    }

    [Fact]
    public void Round_WithDecimals()
    {
        AssertNumeric(Invoke("round", F(3.14159), I(2)), 3.14, 1e-10);
    }

    [Fact]
    public void Round_Negative()
    {
        AssertNumeric(Invoke("round", F(-2.5)), -3.0, 1e-10);
    }

    // =========================================================================
    // switch — extended
    // =========================================================================

    [Fact]
    public void Switch_MatchFirst()
    {
        Assert.Equal(S("one"), Invoke("switch", I(1), I(1), S("one"), I(2), S("two")));
    }

    [Fact]
    public void Switch_MatchSecond()
    {
        Assert.Equal(S("two"), Invoke("switch", I(2), I(1), S("one"), I(2), S("two")));
    }

    [Fact]
    public void Switch_Default()
    {
        Assert.Equal(S("other"), Invoke("switch", I(99), I(1), S("one"), S("other")));
    }

    [Fact]
    public void Switch_NoMatchNoDefault()
    {
        Assert.True(Invoke("switch", I(99), I(1), S("one"), I(2), S("two")).IsNull);
    }

    [Fact]
    public void Switch_StringMatch()
    {
        Assert.Equal(I(1), Invoke("switch", S("a"), S("a"), I(1), S("b"), I(2)));
    }

    // =========================================================================
    // random — extended
    // =========================================================================

    [Fact]
    public void Random_NoArgs_Between0And1()
    {
        var result = Invoke("random");
        var v = result.AsDouble()!.Value;
        Assert.True(v >= 0.0 && v <= 1.0, $"random={v} not in [0,1]");
    }

    [Fact]
    public void Random_WithRange()
    {
        var result = Invoke("random", I(10), I(20));
        var v = result.AsDouble()!.Value;
        Assert.True(v >= 10.0 && v <= 20.0, $"random={v} not in [10,20]");
    }

    // =========================================================================
    // Null handling edge cases
    // =========================================================================

    [Fact]
    public void Add_NullArg()
    {
        Assert.True(Invoke("add", Null(), I(5)).IsNull);
    }

    [Fact]
    public void Subtract_NullArg()
    {
        Assert.True(Invoke("subtract", I(5), Null()).IsNull);
    }

    [Fact]
    public void Multiply_NullArg()
    {
        Assert.True(Invoke("multiply", Null(), Null()).IsNull);
    }

    [Fact]
    public void Divide_NullArg()
    {
        Assert.True(Invoke("divide", Null(), I(5)).IsNull);
    }

    [Fact]
    public void Floor_NullArg()
    {
        Assert.True(Invoke("floor", Null()).IsNull);
    }

    [Fact]
    public void Ceil_NullArg()
    {
        Assert.True(Invoke("ceil", Null()).IsNull);
    }

    [Fact]
    public void Sqrt_NullArg()
    {
        Assert.True(Invoke("sqrt", Null()).IsNull);
    }

    [Fact]
    public void Log_NullArg()
    {
        Assert.True(Invoke("log", Null(), F(2.0)).IsNull);
    }

    [Fact]
    public void Ln_NullArg()
    {
        Assert.True(Invoke("ln", Null()).IsNull);
    }

    [Fact]
    public void Exp_NullArg()
    {
        Assert.True(Invoke("exp", Null()).IsNull);
    }

    [Fact]
    public void Pow_NullArg()
    {
        Assert.True(Invoke("pow", Null(), F(2.0)).IsNull);
    }

    [Fact]
    public void Negate_NullArg()
    {
        Assert.True(Invoke("negate", Null()).IsNull);
    }

    [Fact]
    public void Sign_NullArg()
    {
        Assert.True(Invoke("sign", Null()).IsNull);
    }

    [Fact]
    public void Trunc_NullArg()
    {
        Assert.True(Invoke("trunc", Null()).IsNull);
    }

    [Fact]
    public void Abs_NullArg()
    {
        Assert.True(Invoke("abs", Null()).IsNull);
    }

    [Fact]
    public void Round_NullArg()
    {
        Assert.True(Invoke("round", Null()).IsNull);
    }

    [Fact]
    public void Compound_NullArg()
    {
        Assert.True(Invoke("compound", Null(), F(0.05), I(10)).IsNull);
    }

    [Fact]
    public void Discount_NullArg()
    {
        Assert.True(Invoke("discount", Null(), F(0.05), I(10)).IsNull);
    }

    [Fact]
    public void Pmt_NullArg()
    {
        Assert.True(Invoke("pmt", Null(), F(360.0), F(200000.0)).IsNull);
    }

    [Fact]
    public void Clamp_NullArg()
    {
        Assert.True(Invoke("clamp", Null(), F(0.0), F(10.0)).IsNull);
    }

    [Fact]
    public void Interpolate_NullArg()
    {
        Assert.True(Invoke("interpolate", Null(), F(100.0), F(0.5)).IsNull);
    }

    [Fact]
    public void Depreciation_NullArg()
    {
        Assert.True(Invoke("depreciation", Null(), F(0.0), F(5.0)).IsNull);
    }
}
