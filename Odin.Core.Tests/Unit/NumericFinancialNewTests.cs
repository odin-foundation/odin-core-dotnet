using System;
using System.Collections.Generic;
using Odin.Core.Types;
using Odin.Core.Transform;
using Odin.Core.Transform.Verbs;
using Xunit;

namespace Odin.Core.Tests.Unit;

/// <summary>
/// Tests for integer-theory numeric verbs (gcd, lcm, factorial) and dated
/// cash-flow financial verbs (xnpv, xirr).
/// </summary>
public class NumericFinancialNewTests
{
    private readonly VerbRegistry _registry = new VerbRegistry();
    private readonly VerbContext _ctx = new VerbContext();

    private DynValue Invoke(string verb, params DynValue[] args)
        => _registry.Invoke(verb, args, _ctx);

    private static DynValue S(string v) => DynValue.String(v);
    private static DynValue I(long v) => DynValue.Integer(v);
    private static DynValue F(double v) => DynValue.Float(v);
    private static DynValue Null() => DynValue.Null();
    private static DynValue Arr(params DynValue[] items) => DynValue.Array(new List<DynValue>(items));

    // =========================================================================
    // gcd
    // =========================================================================

    [Fact]
    public void Gcd_Basic()
        => Assert.Equal(6, Invoke("gcd", I(12), I(18)).AsInt64());

    [Fact]
    public void Gcd_WithZeroIsOther()
        => Assert.Equal(12, Invoke("gcd", I(0), I(12)).AsInt64());

    [Fact]
    public void Gcd_UsesAbsoluteValue()
        => Assert.Equal(6, Invoke("gcd", I(-12), I(18)).AsInt64());

    [Fact]
    public void Gcd_TooFewArgs()
        => Assert.True(Invoke("gcd", I(12)).IsNull);

    // =========================================================================
    // lcm
    // =========================================================================

    [Fact]
    public void Lcm_Basic()
        => Assert.Equal(12, Invoke("lcm", I(4), I(6)).AsInt64());

    [Fact]
    public void Lcm_WithZeroIsZero()
        => Assert.Equal(0, Invoke("lcm", I(0), I(4)).AsInt64());

    [Fact]
    public void Lcm_TooFewArgs()
        => Assert.True(Invoke("lcm", I(4)).IsNull);

    // =========================================================================
    // factorial
    // =========================================================================

    [Fact]
    public void Factorial_Five()
        => Assert.Equal(120, Invoke("factorial", I(5)).AsInt64());

    [Fact]
    public void Factorial_ZeroIsOne()
        => Assert.Equal(1, Invoke("factorial", I(0)).AsInt64());

    [Fact]
    public void Factorial_MaxEighteen()
        => Assert.Equal(6402373705728000L, Invoke("factorial", I(18)).AsInt64());

    [Fact]
    public void Factorial_OverEighteenIsNull()
        => Assert.True(Invoke("factorial", I(19)).IsNull);

    [Fact]
    public void Factorial_NegativeIsNull()
        => Assert.True(Invoke("factorial", I(-1)).IsNull);

    [Fact]
    public void Factorial_NoArgs()
        => Assert.True(Invoke("factorial").IsNull);

    // =========================================================================
    // xnpv
    // =========================================================================

    // Dated cash flows: -1000 at t0 then 110/110/110/1100 on yearly dates.
    private static DynValue Amounts() => Arr(F(-1000), F(110), F(110), F(110), F(1100));
    private static DynValue Dates() => Arr(
        S("2020-01-01"), S("2021-01-01"), S("2022-01-01"), S("2023-01-01"), S("2024-01-01"));

    [Fact]
    public void Xnpv_DiscountsDatedFlows()
    {
        var result = Invoke("xnpv", F(0.09), Amounts(), Dates());
        Assert.True(Math.Abs(result.AsDouble()!.Value - 57.4604) < 1e-3);
    }

    [Fact]
    public void Xnpv_LengthMismatchIsNull()
    {
        var result = Invoke("xnpv", F(0.09), Arr(F(-1000), F(110)), Dates());
        Assert.True(result.IsNull);
    }

    [Fact]
    public void Xnpv_TooFewArgsIsNull()
        => Assert.True(Invoke("xnpv", F(0.09), Amounts()).IsNull);

    // =========================================================================
    // xirr
    // =========================================================================

    [Fact]
    public void Xirr_SolvesRate()
    {
        var result = Invoke("xirr", Amounts(), Dates());
        Assert.True(Math.Abs(result.AsDouble()!.Value - 0.10778) < 1e-3);
    }

    [Fact]
    public void Xirr_ZeroNpvAtSolvedRate()
    {
        var rate = Invoke("xirr", Amounts(), Dates());
        var npv = Invoke("xnpv", F(rate.AsDouble()!.Value), Amounts(), Dates());
        Assert.True(Math.Abs(npv.AsDouble()!.Value) < 1e-4);
    }

    [Fact]
    public void Xirr_SingleFlowIsNull()
        => Assert.True(Invoke("xirr", Arr(F(-1000)), Arr(S("2020-01-01"))).IsNull);

    [Fact]
    public void Xirr_TooFewArgsIsNull()
        => Assert.True(Invoke("xirr", Amounts()).IsNull);
}
