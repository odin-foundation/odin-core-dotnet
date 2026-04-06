#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using Odin.Core.Types;

using Odin.Core.Transform;

namespace Odin.Core.Transform.Verbs;

/// <summary>
/// Financial, mathematical, and statistical verbs: logarithms, TVM calculations,
/// descriptive statistics, interpolation, NPV, IRR, depreciation, and more.
/// </summary>
internal static class FinancialVerbs
{
    /// <summary>
    /// Registers all financial and statistical verbs into the provided dictionary.
    /// </summary>
    /// <param name="reg">The verb registration dictionary.</param>
    public static void Register(Dictionary<string, Func<DynValue[], VerbContext, DynValue>> reg)
    {
        // Math
        reg["log"] = Log;
        reg["ln"] = Ln;
        reg["log10"] = Log10;
        reg["exp"] = Exp;
        reg["pow"] = Pow;
        reg["sqrt"] = Sqrt;

        // Time value of money
        reg["compound"] = Compound;
        reg["discount"] = Discount;
        reg["pmt"] = Pmt;
        reg["fv"] = Fv;
        reg["pv"] = Pv;
        reg["rate"] = Rate;
        reg["nper"] = Nper;

        // Statistics
        reg["std"] = Std;
        reg["variance"] = Variance;
        reg["stdSample"] = StdSample;
        reg["varianceSample"] = VarianceSample;
        reg["median"] = Median;
        reg["mode"] = Mode;
        reg["percentile"] = Percentile;
        reg["quantile"] = Quantile;
        reg["covariance"] = Covariance;
        reg["correlation"] = Correlation;
        reg["zscore"] = Zscore;

        // Other
        reg["clamp"] = Clamp;
        reg["interpolate"] = Interpolate;
        reg["weightedAvg"] = WeightedAvg;
        reg["npv"] = Npv;
        reg["irr"] = Irr;
        reg["depreciation"] = Depreciation;
        reg["movingAvg"] = MovingAvg;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static double? ToDouble(DynValue v)
    {
        if (v.IsNull) return null;
        var d = v.AsDouble();
        if (d.HasValue) return d.Value;
        var s = v.AsString();
        if (s != null && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            return parsed;
        return null;
    }

    private static DynValue NumericResult(double v)
    {
        if (double.IsNaN(v) || double.IsInfinity(v))
            return DynValue.Null();
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (v == Math.Floor(v) && Math.Abs(v) < (double)long.MaxValue)
            return DynValue.Integer((long)v);
        return DynValue.Float(v);
    }

    private static List<double>? ExtractDoubles(DynValue arg)
    {
        var items = arg.AsArray() ?? arg.ExtractArray();
        if (items == null) return null;
        var result = new List<double>(items.Count);
        for (int i = 0; i < items.Count; i++)
        {
            var v = ToDouble(items[i]);
            if (v.HasValue) result.Add(v.Value);
        }
        return result;
    }

    private static double PopulationVariance(List<double> vals)
    {
        if (vals.Count == 0) return 0;
        double mean = 0;
        for (int i = 0; i < vals.Count; i++) mean += vals[i];
        mean /= vals.Count;
        double sum = 0;
        for (int i = 0; i < vals.Count; i++)
        {
            double diff = vals[i] - mean;
            sum += diff * diff;
        }
        return sum / vals.Count;
    }

    private static double SampleVariance(List<double> vals)
    {
        if (vals.Count < 2) return 0;
        double mean = 0;
        for (int i = 0; i < vals.Count; i++) mean += vals[i];
        mean /= vals.Count;
        double sum = 0;
        for (int i = 0; i < vals.Count; i++)
        {
            double diff = vals[i] - mean;
            sum += diff * diff;
        }
        return sum / (vals.Count - 1);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Math Verbs
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Logarithm: log(value, base). Two args => Math.Log(args[0], args[1]).</summary>
    private static DynValue Log(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2) return DynValue.Null();
        var val = ToDouble(args[0]);
        var b = ToDouble(args[1]);
        if (!val.HasValue || !b.HasValue) return DynValue.Null();
        return NumericResult(Math.Log(val.Value, b.Value));
    }

    /// <summary>Natural logarithm (base e).</summary>
    private static DynValue Ln(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var val = ToDouble(args[0]);
        if (!val.HasValue) return DynValue.Null();
        return NumericResult(Math.Log(val.Value));
    }

    /// <summary>Base-10 logarithm.</summary>
    private static DynValue Log10(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var val = ToDouble(args[0]);
        if (!val.HasValue) return DynValue.Null();
        return NumericResult(Math.Log10(val.Value));
    }

    /// <summary>Exponential: e^x.</summary>
    private static DynValue Exp(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var val = ToDouble(args[0]);
        if (!val.HasValue) return DynValue.Null();
        return NumericResult(Math.Exp(val.Value));
    }

    /// <summary>Power: args[0] raised to args[1].</summary>
    private static DynValue Pow(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2) return DynValue.Null();
        var b = ToDouble(args[0]);
        var e = ToDouble(args[1]);
        if (!b.HasValue || !e.HasValue) return DynValue.Null();
        return NumericResult(Math.Pow(b.Value, e.Value));
    }

    /// <summary>Square root.</summary>
    private static DynValue Sqrt(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var val = ToDouble(args[0]);
        if (!val.HasValue) return DynValue.Null();
        return NumericResult(Math.Sqrt(val.Value));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Time Value of Money
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Compound interest: principal * (1 + rate)^periods.</summary>
    private static DynValue Compound(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 3) return DynValue.Null();
        var principal = ToDouble(args[0]);
        var rate = ToDouble(args[1]);
        var periods = ToDouble(args[2]);
        if (!principal.HasValue || !rate.HasValue || !periods.HasValue) return DynValue.Null();
        return NumericResult(principal.Value * Math.Pow(1.0 + rate.Value, periods.Value));
    }

    /// <summary>Discount: futureValue / (1 + rate)^periods.</summary>
    private static DynValue Discount(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 3) return DynValue.Null();
        var fv = ToDouble(args[0]);
        var rate = ToDouble(args[1]);
        var periods = ToDouble(args[2]);
        if (!fv.HasValue || !rate.HasValue || !periods.HasValue) return DynValue.Null();
        return NumericResult(fv.Value / Math.Pow(1.0 + rate.Value, periods.Value));
    }

    /// <summary>Payment: P * r * (1+r)^n / ((1+r)^n - 1).</summary>
    private static DynValue Pmt(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 3) return DynValue.Null();
        var principal = ToDouble(args[0]);
        var rate = ToDouble(args[1]);
        var nper = ToDouble(args[2]);
        if (!principal.HasValue || !rate.HasValue || !nper.HasValue) return DynValue.Null();

        double p = principal.Value;
        double r = rate.Value;
        double n = nper.Value;

        if (n <= 0) return DynValue.Null();

        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (r == 0.0)
            return NumericResult(p / n);

        double factor = Math.Pow(1.0 + r, n);
        double result = (p * r * factor) / (factor - 1.0);
        if (double.IsInfinity(result) || double.IsNaN(result))
            return DynValue.Null();
        return NumericResult(result);
    }

    /// <summary>Future value of annuity: PMT * ((1+r)^n - 1) / r.</summary>
    private static DynValue Fv(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 3) return DynValue.Null();
        var payment = ToDouble(args[0]);
        var rate = ToDouble(args[1]);
        var nper = ToDouble(args[2]);
        if (!payment.HasValue || !rate.HasValue || !nper.HasValue) return DynValue.Null();

        double pmt = payment.Value;
        double r = rate.Value;
        double n = nper.Value;

        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (r == 0.0)
            return NumericResult(pmt * n);

        double factor = Math.Pow(1.0 + r, n);
        double result = pmt * ((factor - 1.0) / r);
        if (double.IsInfinity(result) || double.IsNaN(result))
            return DynValue.Null();
        return NumericResult(result);
    }

    /// <summary>Present value of annuity: PMT * (1 - (1+r)^-n) / r.</summary>
    private static DynValue Pv(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 3) return DynValue.Null();
        var payment = ToDouble(args[0]);
        var rate = ToDouble(args[1]);
        var nper = ToDouble(args[2]);
        if (!payment.HasValue || !rate.HasValue || !nper.HasValue) return DynValue.Null();

        double pmt = payment.Value;
        double r = rate.Value;
        double n = nper.Value;

        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (r == 0.0)
            return NumericResult(pmt * n);

        double factor = Math.Pow(1.0 + r, -n);
        double result = pmt * ((1.0 - factor) / r);
        if (double.IsInfinity(result) || double.IsNaN(result))
            return DynValue.Null();
        return NumericResult(result);
    }

    /// <summary>Solve for interest rate given nper, pmt, pv (Newton-Raphson iteration).</summary>
    private static DynValue Rate(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 3) return DynValue.Null();
        var nper = ToDouble(args[0]);
        var pmt = ToDouble(args[1]);
        var pv = ToDouble(args[2]);
        var fvOpt = args.Length > 3 ? ToDouble(args[3]) : 0.0;
        if (!nper.HasValue || !pmt.HasValue || !pv.HasValue) return DynValue.Null();

        double n = nper.Value;
        double m = pmt.Value;
        double p = pv.Value;
        double f = fvOpt ?? 0.0;

        double guess = 0.1;
        for (int i = 0; i < 100; i++)
        {
            double g1 = Math.Pow(1.0 + guess, n);
            double fVal = p * g1 + m * (g1 - 1.0) / guess + f;
            double fDeriv = p * n * Math.Pow(1.0 + guess, n - 1.0)
                            + m * (n * Math.Pow(1.0 + guess, n - 1.0) * guess - (g1 - 1.0)) / (guess * guess);

            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (fDeriv == 0.0) break;
            double next = guess - fVal / fDeriv;
            if (Math.Abs(next - guess) < 1e-10)
            {
                guess = next;
                break;
            }
            guess = next;
        }

        return NumericResult(guess);
    }

    /// <summary>Number of periods: solve nper from rate, pmt, pv.</summary>
    private static DynValue Nper(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 3) return DynValue.Null();
        var rate = ToDouble(args[0]);
        var pmt = ToDouble(args[1]);
        var pv = ToDouble(args[2]);
        if (!rate.HasValue || !pmt.HasValue || !pv.HasValue) return DynValue.Null();

        double r = rate.Value;
        double m = pmt.Value;
        double p = pv.Value;

        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (r == 0.0)
        {
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (m == 0.0) return DynValue.Null();
            return NumericResult(-p / m);
        }

        double numerator = Math.Log(m / (m + r * p));
        double denominator = Math.Log(1.0 + r);
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (denominator == 0.0) return DynValue.Null();
        return NumericResult(numerator / denominator);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Statistics
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Population standard deviation of a number array.</summary>
    private static DynValue Std(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var vals = ExtractDoubles(args[0]);
        if (vals == null || vals.Count == 0) return DynValue.Null();
        return NumericResult(Math.Sqrt(PopulationVariance(vals)));
    }

    /// <summary>Population variance of a number array.</summary>
    private static DynValue Variance(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var vals = ExtractDoubles(args[0]);
        if (vals == null || vals.Count == 0) return DynValue.Null();
        return NumericResult(PopulationVariance(vals));
    }

    /// <summary>Sample standard deviation of a number array (n-1 denominator).</summary>
    private static DynValue StdSample(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var vals = ExtractDoubles(args[0]);
        if (vals == null || vals.Count < 2) return DynValue.Null();
        return NumericResult(Math.Sqrt(SampleVariance(vals)));
    }

    /// <summary>Sample variance of a number array (n-1 denominator).</summary>
    private static DynValue VarianceSample(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var vals = ExtractDoubles(args[0]);
        if (vals == null || vals.Count < 2) return DynValue.Null();
        return NumericResult(SampleVariance(vals));
    }

    /// <summary>Median: middle value of a sorted array.</summary>
    private static DynValue Median(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var vals = ExtractDoubles(args[0]);
        if (vals == null || vals.Count == 0) return DynValue.Null();
        vals.Sort();
        int mid = vals.Count / 2;
        if (vals.Count % 2 == 0)
            return NumericResult((vals[mid - 1] + vals[mid]) / 2.0);
        return NumericResult(vals[mid]);
    }

    /// <summary>Mode: most frequent value in a number array.</summary>
    private static DynValue Mode(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var vals = ExtractDoubles(args[0]);
        if (vals == null || vals.Count == 0) return DynValue.Null();

        var counts = new Dictionary<double, int>();
        for (int i = 0; i < vals.Count; i++)
        {
            if (counts.ContainsKey(vals[i]))
                counts[vals[i]]++;
            else
                counts[vals[i]] = 1;
        }

        double modeVal = vals[0];
        int maxCount = 0;
        foreach (var kvp in counts)
        {
            if (kvp.Value > maxCount)
            {
                maxCount = kvp.Value;
                modeVal = kvp.Key;
            }
        }

        return NumericResult(modeVal);
    }

    /// <summary>Percentile of a sorted array. args[0]=array, args[1]=percentile (0-100).</summary>
    private static DynValue Percentile(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2) return DynValue.Null();
        var vals = ExtractDoubles(args[0]);
        var p = ToDouble(args[1]);
        if (vals == null || vals.Count == 0 || !p.HasValue) return DynValue.Null();

        vals.Sort();
        double pct = p.Value;
        if (pct < 0) pct = 0;
        if (pct > 100) pct = 100;

        double rank = (pct / 100.0) * (vals.Count - 1);
        int lower = (int)Math.Floor(rank);
        int upper = (int)Math.Ceiling(rank);

        if (lower == upper || upper >= vals.Count)
            return NumericResult(vals[lower]);

        double frac = rank - lower;
        return NumericResult(vals[lower] + frac * (vals[upper] - vals[lower]));
    }

    /// <summary>Quantile: same as percentile but args[1] is 0.0-1.0 range.</summary>
    private static DynValue Quantile(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2) return DynValue.Null();
        var q = ToDouble(args[1]);
        if (!q.HasValue) return DynValue.Null();

        // Convert quantile (0-1) to percentile (0-100)
        var pctArgs = new DynValue[] { args[0], DynValue.Float(q.Value * 100.0) };
        return Percentile(pctArgs, ctx);
    }

    /// <summary>Covariance between two number arrays.</summary>
    private static DynValue Covariance(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2) return DynValue.Null();
        var xs = ExtractDoubles(args[0]);
        var ys = ExtractDoubles(args[1]);
        if (xs == null || ys == null) return DynValue.Null();

        int n = Math.Min(xs.Count, ys.Count);
        if (n == 0) return DynValue.Null();

        double meanX = 0, meanY = 0;
        for (int i = 0; i < n; i++) { meanX += xs[i]; meanY += ys[i]; }
        meanX /= n;
        meanY /= n;

        double cov = 0;
        for (int i = 0; i < n; i++)
            cov += (xs[i] - meanX) * (ys[i] - meanY);
        cov /= n;

        return NumericResult(cov);
    }

    /// <summary>Pearson correlation coefficient between two number arrays.</summary>
    private static DynValue Correlation(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2) return DynValue.Null();
        var xs = ExtractDoubles(args[0]);
        var ys = ExtractDoubles(args[1]);
        if (xs == null || ys == null) return DynValue.Null();

        int n = Math.Min(xs.Count, ys.Count);
        if (n == 0) return DynValue.Null();

        double meanX = 0, meanY = 0;
        for (int i = 0; i < n; i++) { meanX += xs[i]; meanY += ys[i]; }
        meanX /= n;
        meanY /= n;

        double cov = 0, varX = 0, varY = 0;
        for (int i = 0; i < n; i++)
        {
            double dx = xs[i] - meanX;
            double dy = ys[i] - meanY;
            cov += dx * dy;
            varX += dx * dx;
            varY += dy * dy;
        }

        double denom = Math.Sqrt(varX * varY);
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (denom == 0.0) return DynValue.Null();

        return NumericResult(cov / denom);
    }

    /// <summary>Z-score: (value - mean) / stddev.</summary>
    private static DynValue Zscore(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 3) return DynValue.Null();
        var value = ToDouble(args[0]);
        var mean = ToDouble(args[1]);
        var stddev = ToDouble(args[2]);
        if (!value.HasValue || !mean.HasValue || !stddev.HasValue) return DynValue.Null();
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (stddev.Value == 0.0) return DynValue.Null();
        return NumericResult((value.Value - mean.Value) / stddev.Value);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Other
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Clamp: clamp(value, min, max).</summary>
    private static DynValue Clamp(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 3) return DynValue.Null();
        var val = ToDouble(args[0]);
        var lo = ToDouble(args[1]);
        var hi = ToDouble(args[2]);
        if (!val.HasValue || !lo.HasValue || !hi.HasValue) return DynValue.Null();
        double clamped = val.Value;
        if (clamped < lo.Value) clamped = lo.Value;
        if (clamped > hi.Value) clamped = hi.Value;
        return NumericResult(clamped);
    }

    /// <summary>Linear interpolation: a + (b - a) * t.</summary>
    private static DynValue Interpolate(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 3) return DynValue.Null();
        var a = ToDouble(args[0]);
        var b = ToDouble(args[1]);
        var t = ToDouble(args[2]);
        if (!a.HasValue || !b.HasValue || !t.HasValue) return DynValue.Null();
        return NumericResult(a.Value + (b.Value - a.Value) * t.Value);
    }

    /// <summary>Weighted average: args[0]=values array, args[1]=weights array.</summary>
    private static DynValue WeightedAvg(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2) return DynValue.Null();
        var vals = ExtractDoubles(args[0]);
        var weights = ExtractDoubles(args[1]);
        if (vals == null || weights == null) return DynValue.Null();

        int n = Math.Min(vals.Count, weights.Count);
        if (n == 0) return DynValue.Null();

        double sumProduct = 0, sumWeights = 0;
        for (int i = 0; i < n; i++)
        {
            sumProduct += vals[i] * weights[i];
            sumWeights += weights[i];
        }

        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (sumWeights == 0.0) return DynValue.Null();
        return NumericResult(sumProduct / sumWeights);
    }

    /// <summary>Net present value: sum of cashflows[t] / (1+rate)^t for t=0,1,2,...</summary>
    private static DynValue Npv(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2) return DynValue.Null();
        var rate = ToDouble(args[0]);
        var cashflows = ExtractDoubles(args[1]);
        if (!rate.HasValue || cashflows == null) return DynValue.Null();

        double r = rate.Value;
        double npv = 0;
        for (int t = 0; t < cashflows.Count; t++)
        {
            npv += cashflows[t] / Math.Pow(1.0 + r, t);
        }

        if (double.IsInfinity(npv) || double.IsNaN(npv))
            return DynValue.Null();
        return NumericResult(npv);
    }

    /// <summary>Internal rate of return using Newton-Raphson iteration.</summary>
    private static DynValue Irr(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var cashflows = ExtractDoubles(args[0]);
        if (cashflows == null || cashflows.Count == 0) return DynValue.Null();

        double guess = args.Length > 1 ? (ToDouble(args[1]) ?? 0.1) : 0.1;

        for (int iter = 0; iter < 200; iter++)
        {
            double npv = 0;
            double dnpv = 0;
            for (int i = 0; i < cashflows.Count; i++)
            {
                double power = Math.Pow(1.0 + guess, i);
                // ReSharper disable once CompareOfFloatsByEqualityOperator
                if (power == 0.0) continue;
                npv += cashflows[i] / power;
                if (i > 0)
                    dnpv -= i * cashflows[i] / Math.Pow(1.0 + guess, i + 1);
            }

            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (dnpv == 0.0) break;
            double next = guess - npv / dnpv;
            if (Math.Abs(next - guess) < 1e-10)
            {
                guess = next;
                break;
            }
            guess = next;
        }

        return NumericResult(guess);
    }

    /// <summary>Straight-line depreciation: (cost - salvage) / life.</summary>
    private static DynValue Depreciation(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 3) return DynValue.Null();
        var cost = ToDouble(args[0]);
        var salvage = ToDouble(args[1]);
        var life = ToDouble(args[2]);
        if (!cost.HasValue || !salvage.HasValue || !life.HasValue) return DynValue.Null();
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (life.Value == 0.0) return DynValue.Null();
        return NumericResult((cost.Value - salvage.Value) / life.Value);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // MovingAvg
    // ─────────────────────────────────────────────────────────────────────────

    private static DynValue MovingAvg(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2) return DynValue.Null();

        var arrVal = args[0].AsArray();
        if (arrVal == null)
        {
            arrVal = args[0].ExtractArray();
            if (arrVal == null) return DynValue.Array(new List<DynValue>());
        }
        if (arrVal.Count == 0) return DynValue.Array(new List<DynValue>());

        var windowD = ToDouble(args[1]);
        if (!windowD.HasValue) return DynValue.Null();
        int windowSize = (int)Math.Floor(windowD.Value);
        if (windowSize < 1) return DynValue.Null();

        // Extract numeric values (non-numeric treated as 0)
        var values = new double[arrVal.Count];
        for (int i = 0; i < arrVal.Count; i++)
        {
            var v = ToDouble(arrVal[i]);
            values[i] = v ?? 0.0;
        }

        var result = new List<DynValue>();
        for (int i = 0; i < values.Length; i++)
        {
            int start = Math.Max(0, i - windowSize + 1);
            double sum = 0;
            for (int j = start; j <= i; j++)
                sum += values[j];
            int count = i - start + 1;
            result.Add(NumericResult(sum / count));
        }

        return DynValue.Array(result);
    }
}
