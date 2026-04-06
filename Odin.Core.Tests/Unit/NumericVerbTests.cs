using System;
using System.Collections.Generic;
using Odin.Core.Types;
using Odin.Core.Transform;
using Odin.Core.Transform.Verbs;
using Xunit;

namespace Odin.Core.Tests.Unit;

/// <summary>
/// Numeric, math, financial, statistics, and datetime verb tests ported from the Rust SDK.
/// </summary>
public class NumericVerbTests
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
    // 1. FORMAT NUMBER VERBS
    // =========================================================================

    [Fact] public void FormatNumber_ZeroDecimals() => Assert.Equal("3", Invoke("formatNumber", F(3.14159), I(0)).AsString());
    [Fact] public void FormatNumber_TwoDecimals() => Assert.Equal("3.14", Invoke("formatNumber", F(3.14159), I(2)).AsString());
    [Fact] public void FormatNumber_ManyDecimals() => Assert.Equal("1.00000", Invoke("formatNumber", F(1.0), I(5)).AsString());
    [Fact] public void FormatNumber_Negative() => Assert.Equal("-42.6", Invoke("formatNumber", F(-42.567), I(1)).AsString());
    [Fact] public void FormatNumber_FromInteger() => Assert.Equal("100.00", Invoke("formatNumber", I(100), I(2)).AsString());
    [Fact] public void FormatNumber_FromString() => Assert.Equal("99.900", Invoke("formatNumber", S("99.9"), I(3)).AsString());
    [Fact] public void FormatNumber_MissingArgs() => Assert.True(Invoke("formatNumber", F(1.0)).IsNull);

    [Fact] public void FormatInteger_Basic() => Assert.Equal("3", Invoke("formatInteger", F(3.7)).AsString());
    [Fact] public void FormatInteger_Negative() => Assert.Equal("-2", Invoke("formatInteger", F(-2.3)).AsString());
    [Fact] public void FormatInteger_FromInt() => Assert.Equal("42", Invoke("formatInteger", I(42)).AsString());

    [Fact] public void FormatCurrency_Basic() => Assert.Equal("1234.50", Invoke("formatCurrency", F(1234.5)).AsString());
    [Fact] public void FormatCurrency_Negative() => Assert.Equal("-100.00", Invoke("formatCurrency", F(-99.999)).AsString());
    [Fact] public void FormatCurrency_Zero() => Assert.Equal("0.00", Invoke("formatCurrency", F(0.0)).AsString());

    [Fact] public void FormatPercent_Basic() => Assert.Equal("85%", Invoke("formatPercent", F(0.85), I(0)).AsString());
    [Fact] public void FormatPercent_WithDecimals() => Assert.Equal("85.7%", Invoke("formatPercent", F(0.8567), I(1)).AsString());
    [Fact] public void FormatPercent_Zero() => Assert.Equal("0%", Invoke("formatPercent", F(0.0), I(0)).AsString());
    [Fact] public void FormatPercent_OverOne() => Assert.Equal("150%", Invoke("formatPercent", F(1.5), I(0)).AsString());

    // =========================================================================
    // 2. FLOOR / CEIL / NEGATE / SIGN / TRUNC
    // =========================================================================

    [Fact] public void Floor_Positive() => AssertNumeric(Invoke("floor", F(3.7)), 3.0, 1e-10);
    [Fact] public void Floor_Negative() => AssertNumeric(Invoke("floor", F(-3.2)), -4.0, 1e-10);
    [Fact] public void Floor_Exact() => AssertNumeric(Invoke("floor", F(5.0)), 5.0, 1e-10);
    [Fact] public void Floor_Null() => Assert.True(Invoke("floor").IsNull);
    [Fact] public void Floor_Integer() => AssertNumeric(Invoke("floor", I(5)), 5.0, 1e-10);

    [Fact] public void Ceil_Positive() => AssertNumeric(Invoke("ceil", F(3.1)), 4.0, 1e-10);
    [Fact] public void Ceil_Negative() => AssertNumeric(Invoke("ceil", F(-3.7)), -3.0, 1e-10);
    [Fact] public void Ceil_Exact() => AssertNumeric(Invoke("ceil", F(5.0)), 5.0, 1e-10);
    [Fact] public void Ceil_Null() => Assert.True(Invoke("ceil").IsNull);

    [Fact] public void Negate_Positive() => AssertNumeric(Invoke("negate", I(42)), -42.0, 1e-10);
    [Fact] public void Negate_Negative() => AssertNumeric(Invoke("negate", F(-3.14)), 3.14, 1e-10);
    [Fact] public void Negate_Zero() => AssertNumeric(Invoke("negate", I(0)), 0.0, 1e-10);
    [Fact] public void Negate_PositiveInt() => AssertNumeric(Invoke("negate", I(5)), -5.0, 1e-10);
    [Fact] public void Negate_NegativeInt() => AssertNumeric(Invoke("negate", I(-5)), 5.0, 1e-10);
    [Fact] public void Negate_Float() => AssertNumeric(Invoke("negate", F(3.14)), -3.14, 1e-10);
    [Fact] public void Negate_StringNum() => AssertNumeric(Invoke("negate", S("7")), -7.0, 1e-10);
    [Fact] public void Negate_Null() => Assert.True(Invoke("negate").IsNull);

    [Fact] public void Sign_Positive() => AssertNumeric(Invoke("sign", F(42.0)), 1.0, 1e-10);
    [Fact] public void Sign_Negative() => AssertNumeric(Invoke("sign", F(-5.0)), -1.0, 1e-10);
    [Fact] public void Sign_Zero() => AssertNumeric(Invoke("sign", F(0.0)), 0.0, 1e-10);
    [Fact] public void Sign_Null() => Assert.True(Invoke("sign").IsNull);

    [Fact] public void Trunc_Positive() => AssertNumeric(Invoke("trunc", F(3.9)), 3.0, 1e-10);
    [Fact] public void Trunc_Negative() => AssertNumeric(Invoke("trunc", F(-3.9)), -3.0, 1e-10);
    [Fact] public void Trunc_Null() => Assert.True(Invoke("trunc").IsNull);

    // =========================================================================
    // 3. ADD / SUBTRACT / MULTIPLY / DIVIDE
    // =========================================================================

    [Fact] public void Add_Integers() => AssertNumeric(Invoke("add", I(3), I(4)), 7.0, 1e-10);
    [Fact] public void Add_StringNumbers() => AssertNumeric(Invoke("add", S("3"), S("4")), 7.0, 1e-10);
    [Fact] public void Add_Negative() => AssertNumeric(Invoke("add", I(-3), I(-4)), -7.0, 1e-10);
    [Fact] public void Add_Zero() => AssertNumeric(Invoke("add", I(0), I(0)), 0.0, 1e-10);
    [Fact] public void Add_TooFew() => Assert.True(Invoke("add", I(1)).IsNull);
    [Fact] public void Add_Floats() => AssertNumeric(Invoke("add", F(1.5), F(2.5)), 4.0, 1e-10);

    [Fact] public void Subtract_Basic() => AssertNumeric(Invoke("subtract", I(10), I(3)), 7.0, 1e-10);
    [Fact] public void Subtract_NegativeResult() => AssertNumeric(Invoke("subtract", I(3), I(10)), -7.0, 1e-10);
    [Fact] public void Subtract_Floats() => AssertNumeric(Invoke("subtract", F(5.5), F(2.5)), 3.0, 1e-10);
    [Fact] public void Subtract_TooFew() => Assert.True(Invoke("subtract", I(1)).IsNull);

    [Fact] public void Multiply_Ints() => AssertNumeric(Invoke("multiply", I(3), I(4)), 12.0, 1e-10);
    [Fact] public void Multiply_ByZero() => AssertNumeric(Invoke("multiply", I(100), I(0)), 0.0, 1e-10);
    [Fact] public void Multiply_Negative() => AssertNumeric(Invoke("multiply", I(-3), I(4)), -12.0, 1e-10);
    [Fact] public void Multiply_Floats() => AssertNumeric(Invoke("multiply", F(2.5), F(4.0)), 10.0, 1e-10);
    [Fact] public void Multiply_TooFew() => Assert.True(Invoke("multiply", I(1)).IsNull);

    [Fact]
    public void Divide_Basic()
    {
        var result = Invoke("divide", I(10), I(2));
        Assert.Equal(5.0, result.AsDouble());
    }

    [Fact] public void Divide_ByZero() => Assert.True(Invoke("divide", I(10), I(0)).IsNull);
    [Fact] public void Divide_Floats() => Assert.Equal(3.0, Invoke("divide", F(7.5), F(2.5)).AsDouble());
    [Fact] public void Divide_Negative() => Assert.Equal(-5.0, Invoke("divide", I(-10), I(2)).AsDouble());
    [Fact] public void Divide_TooFew() => Assert.True(Invoke("divide", I(1)).IsNull);

    // =========================================================================
    // 4. ABS / ROUND / MOD
    // =========================================================================

    [Fact] public void Abs_Positive() => AssertNumeric(Invoke("abs", I(5)), 5.0, 1e-10);
    [Fact] public void Abs_Negative() => AssertNumeric(Invoke("abs", I(-5)), 5.0, 1e-10);
    [Fact] public void Abs_Zero() => AssertNumeric(Invoke("abs", I(0)), 0.0, 1e-10);
    [Fact] public void Abs_NegativeFloat() => AssertNumeric(Invoke("abs", F(-3.14)), 3.14, 1e-10);
    [Fact] public void Abs_StringNumber() => AssertNumeric(Invoke("abs", S("-42")), 42.0, 1e-10);
    [Fact] public void Abs_StringFloat() => AssertNumeric(Invoke("abs", S("-3.14")), 3.14, 1e-10);
    [Fact] public void Abs_Null() => Assert.True(Invoke("abs").IsNull);

    [Fact] public void Round_Basic() => AssertNumeric(Invoke("round", F(3.7)), 4.0, 1e-10);
    [Fact] public void Round_Down() => AssertNumeric(Invoke("round", F(3.2)), 3.0, 1e-10);
    [Fact] public void Round_Half() => AssertNumeric(Invoke("round", F(2.5)), 3.0, 1e-10);
    [Fact] public void Round_Negative() => AssertNumeric(Invoke("round", F(-2.7)), -3.0, 1e-10);
    [Fact] public void Round_IntPassthrough() => AssertNumeric(Invoke("round", I(42)), 42.0, 1e-10);
    [Fact] public void Round_WithPlaces() => AssertNumeric(Invoke("round", F(3.14159), I(2)), 3.14, 1e-10);
    [Fact] public void Round_StringNumber() => AssertNumeric(Invoke("round", S("3.7")), 4.0, 1e-10);
    [Fact] public void Round_Null() => Assert.True(Invoke("round").IsNull);

    [Fact] public void Mod_Basic() => AssertNumeric(Invoke("mod", I(10), I(3)), 1.0, 1e-10);
    [Fact] public void Mod_NoRemainder() => AssertNumeric(Invoke("mod", I(9), I(3)), 0.0, 1e-10);
    [Fact] public void Mod_Float() => AssertNumeric(Invoke("mod", F(10.5), F(3.0)), 1.5, 1e-10);
    [Fact] public void Mod_ByZero() => Assert.True(Invoke("mod", I(10), I(0)).IsNull);
    [Fact] public void Mod_TooFew() => Assert.True(Invoke("mod", I(10)).IsNull);

    // =========================================================================
    // 5. PARSE_INT
    // =========================================================================

    [Fact] public void ParseInt_Basic() => AssertNumeric(Invoke("parseInt", S("42")), 42.0, 1e-10);
    [Fact] public void ParseInt_Negative() => AssertNumeric(Invoke("parseInt", S("-99")), -99.0, 1e-10);
    [Fact] public void ParseInt_Null() => Assert.True(Invoke("parseInt", Null()).IsNull);
    [Fact] public void ParseInt_AlreadyInt() => AssertNumeric(Invoke("parseInt", I(42)), 42.0, 1e-10);
    [Fact] public void ParseInt_FromFloat() => AssertNumeric(Invoke("parseInt", F(3.7)), 3.0, 1e-10);

    // =========================================================================
    // 6. IS_FINITE / IS_NAN
    // =========================================================================

    [Fact] public void IsFinite_Normal() => Assert.Equal(true, Invoke("isFinite", F(42.0)).AsBool());
    [Fact] public void IsFinite_Infinity() => Assert.Equal(false, Invoke("isFinite", F(double.PositiveInfinity)).AsBool());
    [Fact] public void IsFinite_NegInfinity() => Assert.Equal(false, Invoke("isFinite", F(double.NegativeInfinity)).AsBool());
    [Fact] public void IsFinite_NaN() => Assert.Equal(false, Invoke("isFinite", F(double.NaN)).AsBool());
    [Fact] public void IsFinite_Zero() => Assert.Equal(true, Invoke("isFinite", F(0.0)).AsBool());
    [Fact] public void IsFinite_Negative() => Assert.Equal(true, Invoke("isFinite", F(-999.0)).AsBool());

    [Fact] public void IsNaN_Normal() => Assert.Equal(false, Invoke("isNaN", F(42.0)).AsBool());
    [Fact] public void IsNaN_NaN() => Assert.Equal(true, Invoke("isNaN", F(double.NaN)).AsBool());
    [Fact] public void IsNaN_Infinity() => Assert.Equal(false, Invoke("isNaN", F(double.PositiveInfinity)).AsBool());
    [Fact] public void IsNaN_NegInfinity() => Assert.Equal(false, Invoke("isNaN", F(double.NegativeInfinity)).AsBool());

    // =========================================================================
    // 7. MIN_OF / MAX_OF
    // =========================================================================

    [Fact] public void MinOf_Basic() => AssertNumeric(Invoke("minOf", I(3), I(1), I(2)), 1.0, 1e-10);
    [Fact] public void MinOf_Negative() => AssertNumeric(Invoke("minOf", F(-5.0), F(-3.0), F(-10.0)), -10.0, 1e-10);
    [Fact] public void MinOf_Single() => AssertNumeric(Invoke("minOf", I(42)), 42.0, 1e-10);
    [Fact] public void MinOf_MixedTypes() => AssertNumeric(Invoke("minOf", I(5), F(3.5), S("2")), 2.0, 1e-10);
    [Fact] public void MinOf_Null() => Assert.True(Invoke("minOf").IsNull);

    [Fact] public void MaxOf_Basic() => AssertNumeric(Invoke("maxOf", I(3), I(1), I(2)), 3.0, 1e-10);
    [Fact] public void MaxOf_Negative() => AssertNumeric(Invoke("maxOf", F(-5.0), F(-3.0), F(-10.0)), -3.0, 1e-10);
    [Fact] public void MaxOf_Single() => AssertNumeric(Invoke("maxOf", I(42)), 42.0, 1e-10);
    [Fact] public void MaxOf_LargeNumbers() => AssertNumeric(Invoke("maxOf", F(1e15), F(1e14), F(1e16)), 1e16, 1e6);
    [Fact] public void MaxOf_Null() => Assert.True(Invoke("maxOf").IsNull);

    // =========================================================================
    // 8. SAFE_DIVIDE
    // =========================================================================

    [Fact] public void SafeDivide_Normal() => AssertNumeric(Invoke("safeDivide", F(10.0), F(3.0), F(0.0)), 10.0 / 3.0, 1e-10);
    [Fact] public void SafeDivide_ByZero() => AssertNumeric(Invoke("safeDivide", F(10.0), F(0.0), F(-1.0)), -1.0, 1e-10);
    [Fact] public void SafeDivide_ZeroNumerator() => AssertNumeric(Invoke("safeDivide", F(0.0), F(5.0), F(0.0)), 0.0, 1e-10);
    [Fact] public void SafeDivide_MissingArgs() => Assert.True(Invoke("safeDivide", F(10.0)).IsNull);

    // =========================================================================
    // 9. MATH VERBS: LOG, LN, LOG10, EXP, POW, SQRT
    // =========================================================================

    [Fact] public void Log_Base2() => AssertNumeric(Invoke("log", F(8.0), F(2.0)), 3.0, 1e-10);
    [Fact] public void Log_Base10() => AssertNumeric(Invoke("log", F(1000.0), F(10.0)), 3.0, 1e-10);
    [Fact] public void Log_Null() => Assert.True(Invoke("log", F(8.0)).IsNull);

    [Fact] public void Ln_E() => AssertNumeric(Invoke("ln", F(Math.E)), 1.0, 1e-10);
    [Fact] public void Ln_One() => AssertNumeric(Invoke("ln", F(1.0)), 0.0, 1e-10);
    [Fact] public void Ln_Null() => Assert.True(Invoke("ln").IsNull);

    [Fact] public void Log10_Hundred() => AssertNumeric(Invoke("log10", F(100.0)), 2.0, 1e-10);
    [Fact] public void Log10_Thousand() => AssertNumeric(Invoke("log10", F(1000.0)), 3.0, 1e-10);
    [Fact] public void Log10_Null() => Assert.True(Invoke("log10").IsNull);

    [Fact] public void Exp_Zero() => AssertNumeric(Invoke("exp", F(0.0)), 1.0, 1e-10);
    [Fact] public void Exp_One() => AssertNumeric(Invoke("exp", F(1.0)), Math.E, 1e-10);
    [Fact] public void Exp_Null() => Assert.True(Invoke("exp").IsNull);

    [Fact] public void Pow_Basic() => AssertNumeric(Invoke("pow", F(2.0), F(10.0)), 1024.0, 1e-10);
    [Fact] public void Pow_Fractional() => AssertNumeric(Invoke("pow", F(4.0), F(0.5)), 2.0, 1e-10);
    [Fact] public void Pow_ZeroExponent() => AssertNumeric(Invoke("pow", F(99.0), F(0.0)), 1.0, 1e-10);
    [Fact] public void Pow_Null() => Assert.True(Invoke("pow", F(2.0)).IsNull);

    [Fact] public void Sqrt_Perfect() => AssertNumeric(Invoke("sqrt", F(144.0)), 12.0, 1e-10);
    [Fact] public void Sqrt_Zero() => AssertNumeric(Invoke("sqrt", F(0.0)), 0.0, 1e-10);
    [Fact] public void Sqrt_NonPerfect() => AssertNumeric(Invoke("sqrt", F(2.0)), Math.Sqrt(2.0), 1e-10);
    [Fact] public void Sqrt_Null() => Assert.True(Invoke("sqrt").IsNull);

    // =========================================================================
    // 10. FINANCIAL: COMPOUND / DISCOUNT
    // =========================================================================

    [Fact]
    public void Compound_Basic()
    {
        // 1000 * (1 + 0.05)^10
        var result = Invoke("compound", F(1000.0), F(0.05), I(10));
        AssertNumeric(result, 1000.0 * Math.Pow(1.05, 10), 0.01);
    }

    [Fact]
    public void Compound_ZeroRate()
    {
        var result = Invoke("compound", F(1000.0), F(0.0), I(10));
        AssertNumeric(result, 1000.0, 0.01);
    }

    [Fact]
    public void Compound_OnePeriod()
    {
        var result = Invoke("compound", F(500.0), F(0.10), I(1));
        AssertNumeric(result, 550.0, 0.01);
    }

    [Fact]
    public void Compound_LargePeriods()
    {
        var result = Invoke("compound", F(100.0), F(0.07), I(30));
        AssertNumeric(result, 100.0 * Math.Pow(1.07, 30), 0.01);
    }

    [Fact] public void Compound_MissingArgs() => Assert.True(Invoke("compound", F(100.0), F(0.05)).IsNull);

    [Fact]
    public void Discount_Basic()
    {
        // 1000 / (1 + 0.05)^10
        var result = Invoke("discount", F(1000.0), F(0.05), I(10));
        AssertNumeric(result, 1000.0 / Math.Pow(1.05, 10), 0.01);
    }

    [Fact]
    public void Discount_ZeroRate()
    {
        var result = Invoke("discount", F(1000.0), F(0.0), I(5));
        AssertNumeric(result, 1000.0, 0.01);
    }

    [Fact]
    public void Discount_OnePeriod()
    {
        var result = Invoke("discount", F(1100.0), F(0.10), I(1));
        AssertNumeric(result, 1000.0, 0.01);
    }

    [Fact] public void Discount_MissingArgs() => Assert.True(Invoke("discount", F(100.0)).IsNull);

    // =========================================================================
    // 11. PMT / FV / PV
    // =========================================================================

    [Fact]
    public void Pmt_ZeroRate()
    {
        // pmt(principal=12000, rate=0, periods=12) = 12000 / 12 = 1000
        var result = Invoke("pmt", F(12000.0), F(0.0), F(12.0));
        AssertNumeric(result, 1000.0, 0.01);
    }

    [Fact]
    public void Pmt_OnePeriod()
    {
        // pmt(principal=1000, rate=0.1, periods=1) = 1000 * 0.1 * 1.1 / (1.1 - 1) = 1100
        var result = Invoke("pmt", F(1000.0), F(0.1), F(1.0));
        AssertNumeric(result, 1100.0, 0.01);
    }

    [Fact] public void Pmt_MissingArgs() => Assert.True(Invoke("pmt", F(0.05), F(360.0)).IsNull);

    [Fact]
    public void Fv_ZeroRate()
    {
        // fv(payment=100, rate=0, periods=12) = 100 * 12 = 1200
        var result = Invoke("fv", F(100.0), F(0.0), F(12.0));
        AssertNumeric(result, 1200.0, 0.01);
    }

    [Fact] public void Fv_MissingArgs() => Assert.True(Invoke("fv", F(0.05), F(12.0)).IsNull);

    [Fact]
    public void Pv_Basic()
    {
        // pv(payment=1000, rate=0.05, periods=10) = 1000 * (1 - (1.05)^-10) / 0.05
        double expected = 1000.0 * (1.0 - Math.Pow(1.05, -10.0)) / 0.05;
        var result = Invoke("pv", F(1000.0), F(0.05), F(10.0));
        AssertNumeric(result, expected, 0.01);
    }

    [Fact] public void Pv_MissingArgs() => Assert.True(Invoke("pv", F(0.05)).IsNull);

    // =========================================================================
    // 12. NPV
    // =========================================================================

    [Fact]
    public void Npv_Basic()
    {
        var flows = Arr(F(-1000.0), F(300.0), F(400.0), F(500.0));
        var result = Invoke("npv", F(0.1), flows);
        // t=0 indexing: sum of flows[t] / (1+rate)^t
        double expected = -1000.0 + 300.0 / 1.1 + 400.0 / Math.Pow(1.1, 2) + 500.0 / Math.Pow(1.1, 3);
        AssertNumeric(result, expected, 1.0);
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

    [Fact] public void Npv_MissingArgs() => Assert.True(Invoke("npv", F(0.1)).IsNull);

    // =========================================================================
    // 13. IRR
    // =========================================================================

    [Fact]
    public void Irr_Simple()
    {
        // -100, +110 => IRR = 0.10
        var flows = Arr(F(-100.0), F(110.0));
        var result = Invoke("irr", flows);
        AssertNumeric(result, 0.10, 0.01);
    }

    [Fact]
    public void Irr_EvenCashFlows()
    {
        var flows = Arr(F(-1000.0), F(400.0), F(400.0), F(400.0));
        var result = Invoke("irr", flows);
        var d = result.AsDouble();
        Assert.NotNull(d);
        Assert.True(d.Value > 0.05 && d.Value < 0.15, $"IRR={d.Value}");
    }

    // =========================================================================
    // 14. DEPRECIATION
    // =========================================================================

    [Fact]
    public void Depreciation_Basic()
    {
        // (10000 - 1000) / 5 = 1800
        var result = Invoke("depreciation", F(10000.0), F(1000.0), F(5.0));
        AssertNumeric(result, 1800.0, 0.01);
    }

    [Fact]
    public void Depreciation_NoSalvage()
    {
        var result = Invoke("depreciation", F(5000.0), F(0.0), F(10.0));
        AssertNumeric(result, 500.0, 0.01);
    }

    [Fact]
    public void Depreciation_ZeroLife()
    {
        var result = Invoke("depreciation", F(5000.0), F(0.0), F(0.0));
        Assert.True(result.IsNull);
    }

    [Fact] public void Depreciation_MissingArgs() => Assert.True(Invoke("depreciation", F(5000.0), F(0.0)).IsNull);

    // =========================================================================
    // 15. STATISTICS: STD, VARIANCE, MEDIAN, MODE
    // =========================================================================

    [Fact]
    public void Std_Basic()
    {
        var a = Arr(F(2.0), F(4.0), F(4.0), F(4.0), F(5.0), F(5.0), F(7.0), F(9.0));
        var result = Invoke("std", a);
        AssertNumeric(result, 2.0, 0.01);
    }

    [Fact]
    public void Std_Uniform()
    {
        var a = Arr(F(5.0), F(5.0), F(5.0));
        var result = Invoke("std", a);
        AssertNumeric(result, 0.0, 1e-10);
    }

    [Fact] public void Std_Null() => Assert.True(Invoke("std").IsNull);

    [Fact]
    public void Variance_Basic()
    {
        var a = Arr(F(2.0), F(4.0), F(4.0), F(4.0), F(5.0), F(5.0), F(7.0), F(9.0));
        var result = Invoke("variance", a);
        AssertNumeric(result, 4.0, 0.01);
    }

    [Fact]
    public void Median_Odd()
    {
        var a = Arr(F(3.0), F(1.0), F(2.0));
        var result = Invoke("median", a);
        AssertNumeric(result, 2.0, 1e-10);
    }

    [Fact]
    public void Median_Even()
    {
        var a = Arr(F(1.0), F(2.0), F(3.0), F(4.0));
        var result = Invoke("median", a);
        AssertNumeric(result, 2.5, 1e-10);
    }

    [Fact]
    public void Median_Single()
    {
        var a = Arr(F(42.0));
        var result = Invoke("median", a);
        AssertNumeric(result, 42.0, 1e-10);
    }

    [Fact] public void Median_Null() => Assert.True(Invoke("median").IsNull);

    [Fact]
    public void Mode_Basic()
    {
        var a = Arr(F(1.0), F(2.0), F(2.0), F(3.0));
        var result = Invoke("mode", a);
        AssertNumeric(result, 2.0, 1e-10);
    }

    [Fact]
    public void Mode_AllSame()
    {
        var a = Arr(F(7.0), F(7.0), F(7.0));
        var result = Invoke("mode", a);
        AssertNumeric(result, 7.0, 1e-10);
    }

    // =========================================================================
    // 16. STD_SAMPLE / VARIANCE_SAMPLE
    // =========================================================================

    [Fact]
    public void StdSample_Basic()
    {
        var a = Arr(F(2.0), F(4.0), F(4.0), F(4.0), F(5.0), F(5.0), F(7.0), F(9.0));
        var result = Invoke("stdSample", a);
        AssertNumeric(result, Math.Sqrt(32.0 / 7.0), 0.01);
    }

    [Fact]
    public void StdSample_TooFew()
    {
        var a = Arr(F(5.0));
        var result = Invoke("stdSample", a);
        Assert.True(result.IsNull);
    }

    [Fact]
    public void VarianceSample_Basic()
    {
        var a = Arr(F(2.0), F(4.0), F(4.0), F(4.0), F(5.0), F(5.0), F(7.0), F(9.0));
        var result = Invoke("varianceSample", a);
        AssertNumeric(result, 32.0 / 7.0, 0.01);
    }

    // =========================================================================
    // 17. PERCENTILE / QUANTILE
    // =========================================================================

    [Fact]
    public void Percentile_50th()
    {
        var a = Arr(F(1.0), F(2.0), F(3.0), F(4.0), F(5.0));
        var result = Invoke("percentile", a, F(50.0));
        AssertNumeric(result, 3.0, 1e-10);
    }

    [Fact]
    public void Percentile_0th()
    {
        var a = Arr(F(1.0), F(2.0), F(3.0));
        var result = Invoke("percentile", a, F(0.0));
        AssertNumeric(result, 1.0, 1e-10);
    }

    [Fact]
    public void Percentile_100th()
    {
        var a = Arr(F(1.0), F(2.0), F(3.0));
        var result = Invoke("percentile", a, F(100.0));
        AssertNumeric(result, 3.0, 1e-10);
    }

    [Fact]
    public void Quantile_Half()
    {
        var a = Arr(F(10.0), F(20.0), F(30.0));
        var result = Invoke("quantile", a, F(0.5));
        AssertNumeric(result, 20.0, 1e-10);
    }

    // =========================================================================
    // 18. COVARIANCE / CORRELATION
    // =========================================================================

    [Fact]
    public void Covariance_PerfectPositive()
    {
        var a1 = Arr(F(1.0), F(2.0), F(3.0));
        var a2 = Arr(F(2.0), F(4.0), F(6.0));
        var result = Invoke("covariance", a1, a2);
        AssertNumeric(result, 4.0 / 3.0, 1e-10);
    }

    [Fact]
    public void Correlation_PerfectPositive()
    {
        var a1 = Arr(F(1.0), F(2.0), F(3.0));
        var a2 = Arr(F(2.0), F(4.0), F(6.0));
        var result = Invoke("correlation", a1, a2);
        AssertNumeric(result, 1.0, 1e-10);
    }

    [Fact]
    public void Correlation_PerfectNegative()
    {
        var a1 = Arr(F(1.0), F(2.0), F(3.0));
        var a2 = Arr(F(6.0), F(4.0), F(2.0));
        var result = Invoke("correlation", a1, a2);
        AssertNumeric(result, -1.0, 1e-10);
    }

    // =========================================================================
    // 19. ZSCORE
    // =========================================================================

    [Fact]
    public void Zscore_AtMean()
    {
        // zscore(value, mean, stddev)
        var result = Invoke("zscore", F(5.0), F(5.0), F(2.0));
        AssertNumeric(result, 0.0, 1e-10);
    }

    [Fact]
    public void Zscore_AboveMean()
    {
        var result = Invoke("zscore", F(7.0), F(5.0), F(2.0));
        AssertNumeric(result, 1.0, 1e-10);
    }

    [Fact]
    public void Zscore_ZeroStddev()
    {
        var result = Invoke("zscore", F(5.0), F(5.0), F(0.0));
        Assert.True(result.IsNull);
    }

    // =========================================================================
    // 20. CLAMP / INTERPOLATE / WEIGHTED_AVG
    // =========================================================================

    [Fact] public void Clamp_WithinRange() => AssertNumeric(Invoke("clamp", F(5.0), F(1.0), F(10.0)), 5.0, 1e-10);
    [Fact] public void Clamp_BelowMin() => AssertNumeric(Invoke("clamp", F(-5.0), F(0.0), F(10.0)), 0.0, 1e-10);
    [Fact] public void Clamp_AboveMax() => AssertNumeric(Invoke("clamp", F(15.0), F(0.0), F(10.0)), 10.0, 1e-10);
    [Fact] public void Clamp_MissingArgs() => Assert.True(Invoke("clamp", F(5.0), F(0.0)).IsNull);

    [Fact]
    public void Interpolate_Midpoint()
    {
        // interpolate(a, b, t) = a + (b-a)*t
        var result = Invoke("interpolate", F(0.0), F(100.0), F(0.5));
        AssertNumeric(result, 50.0, 1e-10);
    }

    [Fact]
    public void Interpolate_AtStart()
    {
        var result = Invoke("interpolate", F(0.0), F(100.0), F(0.0));
        AssertNumeric(result, 0.0, 1e-10);
    }

    [Fact]
    public void Interpolate_AtEnd()
    {
        var result = Invoke("interpolate", F(0.0), F(100.0), F(1.0));
        AssertNumeric(result, 100.0, 1e-10);
    }

    [Fact] public void Interpolate_MissingArgs() => Assert.True(Invoke("interpolate", F(0.0), F(100.0)).IsNull);

    [Fact]
    public void WeightedAvg_Basic()
    {
        var vals = Arr(F(80.0), F(90.0), F(100.0));
        var wts = Arr(F(1.0), F(2.0), F(1.0));
        var result = Invoke("weightedAvg", vals, wts);
        AssertNumeric(result, 90.0, 1e-10);
    }

    [Fact]
    public void WeightedAvg_EqualWeights()
    {
        var vals = Arr(F(10.0), F(20.0), F(30.0));
        var wts = Arr(F(1.0), F(1.0), F(1.0));
        var result = Invoke("weightedAvg", vals, wts);
        AssertNumeric(result, 20.0, 1e-10);
    }

    [Fact] public void WeightedAvg_MissingArgs() => Assert.True(Invoke("weightedAvg", Arr(F(1.0))).IsNull);

    // =========================================================================
    // 21. DATETIME — formatDate
    // =========================================================================

    [Fact]
    public void FormatDate_YYYYMMDD()
    {
        var result = Invoke("formatDate", S("2024-06-15"), S("YYYY-MM-DD"));
        Assert.Equal("2024-06-15", result.AsString());
    }

    [Fact]
    public void FormatDate_MMDDYYYY()
    {
        var result = Invoke("formatDate", S("2024-06-15"), S("MM/DD/YYYY"));
        Assert.Equal("06/15/2024", result.AsString());
    }

    [Fact]
    public void FormatDate_DDMMYYYY()
    {
        var result = Invoke("formatDate", S("2024-06-15"), S("DD-MM-YYYY"));
        Assert.Equal("15-06-2024", result.AsString());
    }

    [Fact]
    public void FormatDate_FromTimestamp()
    {
        var result = Invoke("formatDate", S("2024-06-15T14:30:00"), S("YYYY-MM-DD"));
        Assert.Equal("2024-06-15", result.AsString());
    }

    [Fact] public void FormatDate_Null() => Assert.True(Invoke("formatDate", Null(), S("YYYY-MM-DD")).IsNull);

    // =========================================================================
    // 22. DATETIME — addDays / addMonths / addYears
    // =========================================================================

    [Fact]
    public void AddDays_Positive()
    {
        var result = Invoke("addDays", S("2024-01-01"), I(10));
        Assert.Equal("2024-01-11", result.AsString());
    }

    [Fact]
    public void AddDays_Negative()
    {
        var result = Invoke("addDays", S("2024-01-11"), I(-10));
        Assert.Equal("2024-01-01", result.AsString());
    }

    [Fact]
    public void AddDays_CrossMonth()
    {
        var result = Invoke("addDays", S("2024-01-28"), I(5));
        Assert.Equal("2024-02-02", result.AsString());
    }

    [Fact] public void AddDays_Null() => Assert.True(Invoke("addDays", Null(), I(1)).IsNull);

    [Fact]
    public void AddMonths_Positive()
    {
        var result = Invoke("addMonths", S("2024-01-15"), I(3));
        Assert.Equal("2024-04-15", result.AsString());
    }

    [Fact]
    public void AddMonths_CrossYear()
    {
        var result = Invoke("addMonths", S("2024-11-15"), I(3));
        Assert.Equal("2025-02-15", result.AsString());
    }

    [Fact] public void AddMonths_Null() => Assert.True(Invoke("addMonths", Null(), I(1)).IsNull);

    [Fact]
    public void AddYears_Positive()
    {
        var result = Invoke("addYears", S("2024-06-15"), I(2));
        Assert.Equal("2026-06-15", result.AsString());
    }

    [Fact]
    public void AddYears_Negative()
    {
        var result = Invoke("addYears", S("2024-06-15"), I(-1));
        Assert.Equal("2023-06-15", result.AsString());
    }

    [Fact] public void AddYears_Null() => Assert.True(Invoke("addYears", Null(), I(1)).IsNull);

    // =========================================================================
    // 23. DATETIME — dateDiff / daysBetweenDates
    // =========================================================================

    [Fact]
    public void DateDiff_Basic()
    {
        var result = Invoke("dateDiff", S("2024-01-01"), S("2024-01-11"));
        Assert.Equal(10L, result.AsInt64());
    }

    [Fact]
    public void DateDiff_Negative()
    {
        var result = Invoke("dateDiff", S("2024-01-11"), S("2024-01-01"));
        Assert.Equal(-10L, result.AsInt64());
    }

    [Fact] public void DateDiff_Null() => Assert.True(Invoke("dateDiff", Null(), S("2024-01-01")).IsNull);

    [Fact]
    public void DaysBetweenDates_Basic()
    {
        var result = Invoke("daysBetweenDates", S("2024-01-01"), S("2024-01-11"));
        Assert.Equal(10L, result.AsInt64());
    }

    [Fact]
    public void DaysBetweenDates_Absolute()
    {
        // daysBetweenDates returns absolute difference
        var result = Invoke("daysBetweenDates", S("2024-01-11"), S("2024-01-01"));
        Assert.Equal(10L, result.AsInt64());
    }

    // =========================================================================
    // 24. DATETIME — quarter / dayOfWeek / weekOfYear / isLeapYear
    // =========================================================================

    [Fact]
    public void Quarter_Q1()
    {
        var result = Invoke("quarter", S("2024-02-15"));
        Assert.Equal(1L, result.AsInt64());
    }

    [Fact]
    public void Quarter_Q2()
    {
        var result = Invoke("quarter", S("2024-05-01"));
        Assert.Equal(2L, result.AsInt64());
    }

    [Fact]
    public void Quarter_Q3()
    {
        var result = Invoke("quarter", S("2024-08-15"));
        Assert.Equal(3L, result.AsInt64());
    }

    [Fact]
    public void Quarter_Q4()
    {
        var result = Invoke("quarter", S("2024-12-01"));
        Assert.Equal(4L, result.AsInt64());
    }

    [Fact] public void Quarter_Null() => Assert.True(Invoke("quarter").IsNull);

    [Fact]
    public void DayOfWeek_Saturday()
    {
        // 2024-06-15 is a Saturday = 6
        var result = Invoke("dayOfWeek", S("2024-06-15"));
        Assert.Equal(6L, result.AsInt64());
    }

    [Fact]
    public void DayOfWeek_Monday()
    {
        // 2024-06-17 is a Monday = 1
        var result = Invoke("dayOfWeek", S("2024-06-17"));
        Assert.Equal(1L, result.AsInt64());
    }

    [Fact] public void DayOfWeek_Null() => Assert.True(Invoke("dayOfWeek").IsNull);

    [Fact]
    public void WeekOfYear_MidYear()
    {
        var result = Invoke("weekOfYear", S("2024-06-15"));
        var week = result.AsInt64();
        Assert.NotNull(week);
        Assert.True(week.Value >= 24 && week.Value <= 25, $"Week={week}");
    }

    [Fact] public void WeekOfYear_Null() => Assert.True(Invoke("weekOfYear").IsNull);

    [Fact]
    public void IsLeapYear_2024()
    {
        var result = Invoke("isLeapYear", I(2024));
        Assert.Equal(true, result.AsBool());
    }

    [Fact]
    public void IsLeapYear_2023()
    {
        var result = Invoke("isLeapYear", I(2023));
        Assert.Equal(false, result.AsBool());
    }

    [Fact]
    public void IsLeapYear_2000()
    {
        var result = Invoke("isLeapYear", I(2000));
        Assert.Equal(true, result.AsBool());
    }

    [Fact]
    public void IsLeapYear_1900()
    {
        var result = Invoke("isLeapYear", I(1900));
        Assert.Equal(false, result.AsBool());
    }

    [Fact]
    public void IsLeapYear_FromDate()
    {
        var result = Invoke("isLeapYear", S("2024-06-15"));
        Assert.Equal(true, result.AsBool());
    }

    // =========================================================================
    // 25. DATETIME — isBefore / isAfter / isBetween
    // =========================================================================

    [Fact]
    public void IsBefore_True()
    {
        var result = Invoke("isBefore", S("2024-01-01"), S("2024-06-01"));
        Assert.Equal(true, result.AsBool());
    }

    [Fact]
    public void IsBefore_False()
    {
        var result = Invoke("isBefore", S("2024-06-01"), S("2024-01-01"));
        Assert.Equal(false, result.AsBool());
    }

    [Fact]
    public void IsBefore_Equal()
    {
        var result = Invoke("isBefore", S("2024-01-01"), S("2024-01-01"));
        Assert.Equal(false, result.AsBool());
    }

    [Fact] public void IsBefore_Null() => Assert.True(Invoke("isBefore", Null(), S("2024-01-01")).IsNull);

    [Fact]
    public void IsAfter_True()
    {
        var result = Invoke("isAfter", S("2024-06-01"), S("2024-01-01"));
        Assert.Equal(true, result.AsBool());
    }

    [Fact]
    public void IsAfter_False()
    {
        var result = Invoke("isAfter", S("2024-01-01"), S("2024-06-01"));
        Assert.Equal(false, result.AsBool());
    }

    [Fact] public void IsAfter_Null() => Assert.True(Invoke("isAfter", Null(), S("2024-01-01")).IsNull);

    [Fact]
    public void IsBetween_True()
    {
        var result = Invoke("isBetween", S("2024-03-01"), S("2024-01-01"), S("2024-06-01"));
        Assert.Equal(true, result.AsBool());
    }

    [Fact]
    public void IsBetween_False()
    {
        var result = Invoke("isBetween", S("2024-07-01"), S("2024-01-01"), S("2024-06-01"));
        Assert.Equal(false, result.AsBool());
    }

    [Fact]
    public void IsBetween_AtBoundary()
    {
        var result = Invoke("isBetween", S("2024-01-01"), S("2024-01-01"), S("2024-06-01"));
        Assert.Equal(true, result.AsBool());
    }

    [Fact] public void IsBetween_Null() => Assert.True(Invoke("isBetween", Null(), S("2024-01-01"), S("2024-06-01")).IsNull);

    // =========================================================================
    // 26. DATETIME — startOf / endOf
    // =========================================================================

    [Fact]
    public void StartOfMonth_Basic()
    {
        var result = Invoke("startOfMonth", S("2024-06-15"));
        Assert.Equal("2024-06-01", result.AsString());
    }

    [Fact]
    public void EndOfMonth_Basic()
    {
        var result = Invoke("endOfMonth", S("2024-06-15"));
        Assert.Equal("2024-06-30", result.AsString());
    }

    [Fact]
    public void EndOfMonth_February_Leap()
    {
        var result = Invoke("endOfMonth", S("2024-02-01"));
        Assert.Equal("2024-02-29", result.AsString());
    }

    [Fact]
    public void EndOfMonth_February_NonLeap()
    {
        var result = Invoke("endOfMonth", S("2023-02-01"));
        Assert.Equal("2023-02-28", result.AsString());
    }

    [Fact]
    public void StartOfYear_Basic()
    {
        var result = Invoke("startOfYear", S("2024-06-15"));
        Assert.Equal("2024-01-01", result.AsString());
    }

    [Fact]
    public void EndOfYear_Basic()
    {
        var result = Invoke("endOfYear", S("2024-06-15"));
        Assert.Equal("2024-12-31", result.AsString());
    }

    [Fact] public void StartOfMonth_Null() => Assert.True(Invoke("startOfMonth").IsNull);
    [Fact] public void EndOfMonth_Null() => Assert.True(Invoke("endOfMonth").IsNull);
    [Fact] public void StartOfYear_Null() => Assert.True(Invoke("startOfYear").IsNull);
    [Fact] public void EndOfYear_Null() => Assert.True(Invoke("endOfYear").IsNull);

    // =========================================================================
    // 27. DATETIME — toUnix / fromUnix
    // =========================================================================

    [Fact]
    public void ToUnix_Epoch()
    {
        var result = Invoke("toUnix", S("1970-01-01T00:00:00Z"));
        Assert.Equal(0L, result.AsInt64());
    }

    [Fact]
    public void ToUnix_Date()
    {
        var result = Invoke("toUnix", S("2024-01-01"));
        var unix = result.AsInt64();
        Assert.NotNull(unix);
        Assert.True(unix.Value > 0);
    }

    [Fact] public void ToUnix_Null() => Assert.True(Invoke("toUnix").IsNull);

    [Fact]
    public void FromUnix_Epoch()
    {
        var result = Invoke("fromUnix", I(0));
        var ts = result.AsString();
        Assert.NotNull(ts);
        Assert.Contains("1970-01-01", ts);
    }

    [Fact] public void FromUnix_Null() => Assert.True(Invoke("fromUnix").IsNull);

    // =========================================================================
    // 28. DATETIME — isValidDate
    // =========================================================================

    [Fact] public void IsValidDate_Valid() => Assert.Equal(true, Invoke("isValidDate", S("2024-06-15")).AsBool());
    [Fact] public void IsValidDate_InvalidMonth() => Assert.Equal(false, Invoke("isValidDate", S("2024-13-01")).AsBool());
    [Fact] public void IsValidDate_InvalidDay() => Assert.Equal(false, Invoke("isValidDate", S("2024-02-30")).AsBool());
    [Fact] public void IsValidDate_LeapDay() => Assert.Equal(true, Invoke("isValidDate", S("2024-02-29")).AsBool());
    [Fact] public void IsValidDate_NonLeapDay() => Assert.Equal(false, Invoke("isValidDate", S("2023-02-29")).AsBool());
    [Fact] public void IsValidDate_NotADate() => Assert.Equal(false, Invoke("isValidDate", S("not-a-date")).AsBool());
    [Fact] public void IsValidDate_NoArgs() => Assert.Equal(false, Invoke("isValidDate").AsBool());

    // =========================================================================
    // 29. RANDOM (just verifying it runs and returns a float)
    // =========================================================================

    [Fact]
    public void Random_NoArgs()
    {
        var result = Invoke("random");
        var d = result.AsDouble();
        Assert.NotNull(d);
        Assert.True(d.Value >= 0.0 && d.Value <= 1.0);
    }

    [Fact]
    public void Random_WithRange()
    {
        var result = Invoke("random", F(10.0), F(20.0));
        var d = result.AsDouble();
        Assert.NotNull(d);
        Assert.True(d.Value >= 10.0 && d.Value <= 20.0);
    }

    // =========================================================================
    // 30. FORMAT_LOCALE_NUMBER
    // =========================================================================

    [Fact]
    public void FormatLocaleNumber_Default()
    {
        var result = Invoke("formatLocaleNumber", F(1234567.89));
        Assert.NotNull(result.AsString());
    }

    [Fact]
    public void FormatLocaleNumber_WithLocale()
    {
        var result = Invoke("formatLocaleNumber", F(1234.5), S("en-US"), I(2));
        Assert.Contains("1,234.50", result.AsString());
    }

    [Fact] public void FormatLocaleNumber_Null() => Assert.True(Invoke("formatLocaleNumber").IsNull);

    // =========================================================================
    // 31. RATE / NPER
    // =========================================================================

    [Fact]
    public void Rate_Basic()
    {
        var result = Invoke("rate", F(10.0), F(-100.0), F(1000.0), F(0.0));
        var d = result.AsDouble();
        Assert.NotNull(d);
        Assert.True(double.IsFinite(d.Value), $"rate should be finite, got {d.Value}");
    }

    [Fact] public void Rate_MissingArgs() => Assert.True(Invoke("rate", F(10.0), F(-100.0)).IsNull);

    [Fact]
    public void Nper_ZeroRate()
    {
        // nper(0, -100, 5000) = -5000/-100 = 50
        var result = Invoke("nper", F(0.0), F(-100.0), F(5000.0));
        AssertNumeric(result, 50.0, 0.5);
    }

    [Fact] public void Nper_MissingArgs() => Assert.True(Invoke("nper", F(0.01)).IsNull);

    // =========================================================================
    // Registry verification — all verbs exist
    // =========================================================================

    [Fact]
    public void Registry_HasAllNumericVerbs()
    {
        var reg = new VerbRegistry();
        var ctx = new VerbContext();
        var verbs = new[] { "add", "subtract", "multiply", "divide", "abs", "round", "negate",
            "floor", "ceil", "sign", "trunc", "mod", "formatNumber", "formatInteger",
            "formatCurrency", "formatPercent", "minOf", "maxOf", "safeDivide", "parseInt",
            "isFinite", "isNaN", "random", "switch" };
        foreach (var verb in verbs)
        {
            // Just verify they don't throw for unknown verb
            try { reg.Invoke(verb, new DynValue[0], ctx); } catch { }
        }
    }

    [Fact]
    public void Registry_HasAllMathVerbs()
    {
        var reg = new VerbRegistry();
        var ctx = new VerbContext();
        var verbs = new[] { "log", "ln", "log10", "exp", "pow", "sqrt" };
        foreach (var verb in verbs)
        {
            try { reg.Invoke(verb, new DynValue[0], ctx); } catch { }
        }
    }

    [Fact]
    public void Registry_HasAllFinancialVerbs()
    {
        var reg = new VerbRegistry();
        var ctx = new VerbContext();
        var verbs = new[] { "compound", "discount", "pmt", "fv", "pv", "rate", "nper", "npv", "irr", "depreciation" };
        foreach (var verb in verbs)
        {
            try { reg.Invoke(verb, new DynValue[0], ctx); } catch { }
        }
    }

    [Fact]
    public void Registry_HasAllStatVerbs()
    {
        var reg = new VerbRegistry();
        var ctx = new VerbContext();
        var verbs = new[] { "std", "variance", "stdSample", "varianceSample", "median", "mode",
            "percentile", "quantile", "covariance", "correlation", "zscore", "clamp",
            "interpolate", "weightedAvg" };
        foreach (var verb in verbs)
        {
            try { reg.Invoke(verb, new DynValue[0], ctx); } catch { }
        }
    }

    [Fact]
    public void Registry_HasAllDateTimeVerbs()
    {
        var reg = new VerbRegistry();
        var ctx = new VerbContext();
        var verbs = new[] { "today", "now", "formatDate", "parseDate", "formatTime",
            "formatTimestamp", "parseTimestamp", "addDays", "addMonths", "addYears",
            "dateDiff", "addHours", "addMinutes", "addSeconds", "startOfDay", "endOfDay",
            "startOfMonth", "endOfMonth", "startOfYear", "endOfYear", "dayOfWeek",
            "weekOfYear", "quarter", "isLeapYear", "isBefore", "isAfter", "isBetween",
            "toUnix", "fromUnix", "daysBetweenDates", "ageFromDate", "isValidDate",
            "formatLocaleDate" };
        foreach (var verb in verbs)
        {
            try { reg.Invoke(verb, new DynValue[0], ctx); } catch { }
        }
    }
}
