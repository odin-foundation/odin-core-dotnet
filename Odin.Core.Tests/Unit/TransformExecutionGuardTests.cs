using System;
using System.Collections.Generic;
using Odin.Core;
using Odin.Core.Transform;
using Odin.Core.Types;
using Odin.Core.Utils;
using Xunit;

namespace Odin.Core.Tests.Unit;

// Serialized against other collections: these tests mutate global security limits.
[CollectionDefinition("TransformExecutionGuard", DisableParallelization = true)]
public sealed class TransformExecutionGuardCollection { }

/// <summary>
/// Transform execution guard: fuel budget (T016), wall-clock timeout (T017), and
/// expression-depth enforcement (T018). Guards charge only when their limit is set
/// (&gt; 0); depth uses its standing default. Aborts are not downgraded by onError
/// and surface as a failed result, never thrown past execute.
/// </summary>
[Collection("TransformExecutionGuard")]
public sealed class TransformExecutionGuardTests : IDisposable
{
    private const string Header =
        "{$}\nodin = \"1.0.0\"\ntransform = \"1.0.0\"\ndirection = \"json->json\"\n\n";

    private readonly long _savedFuel = SecurityLimits.MaxTransformFuel;
    private readonly long _savedTimeout = SecurityLimits.TransformTimeoutMs;
    private readonly int _savedDepth = SecurityLimits.MaxExpressionDepth;
    private readonly Func<long> _savedClock = TransformEngine.Clock;

    public void Dispose()
    {
        SecurityLimits.MaxTransformFuel = _savedFuel;
        SecurityLimits.TransformTimeoutMs = _savedTimeout;
        SecurityLimits.MaxExpressionDepth = _savedDepth;
        TransformEngine.Clock = _savedClock;
    }

    private static string HeaderWith(string onError) =>
        Header + $"{{$target}}\nformat = \"json\"\nonError = \"{onError}\"\n\n";

    private static TransformResult Run(string body, DynValue source, string head)
    {
        var transform = Odin.ParseTransform(head + body);
        return TransformEngine.Execute(transform, source);
    }

    private static TransformResult Run(string body, DynValue source) => Run(body, source, Header);

    private static TransformResult Run(string body) => Run(body, DynValue.Null(), Header);

    // Descending integer array of the given length.
    private static DynValue BigArray(int n)
    {
        var items = new List<DynValue>(n);
        for (int i = 0; i < n; i++)
            items.Add(DynValue.Integer(n - i));
        return DynValue.Object(new List<KeyValuePair<string, DynValue>>
        {
            new KeyValuePair<string, DynValue>("big", DynValue.Array(items)),
        });
    }

    private static DynValue BigArray(params long[] values)
    {
        var items = new List<DynValue>(values.Length);
        foreach (var v in values) items.Add(DynValue.Integer(v));
        return DynValue.Object(new List<KeyValuePair<string, DynValue>>
        {
            new KeyValuePair<string, DynValue>("big", DynValue.Array(items)),
        });
    }

    // Chain of n nested unary verbs, evaluated one level per node.
    private static string NestAbs(int n)
    {
        var sb = new System.Text.StringBuilder("{out}\nr = ");
        for (int i = 0; i < n; i++) sb.Append("%abs ");
        sb.Append("##1");
        return sb.ToString();
    }

    private static bool HasCode(IEnumerable<TransformError> errors, string code)
    {
        foreach (var e in errors) if (e.Code == code) return true;
        return false;
    }

    private static bool HasCode(IEnumerable<TransformWarning> warnings, string code)
    {
        foreach (var w in warnings) if (w.Code == code) return true;
        return false;
    }

    // ── unbounded when unset ──────────────────────────────────────────────

    [Fact]
    public void RunsToCompletionWithAllLimitsOff()
    {
        SecurityLimits.MaxTransformFuel = 0;
        SecurityLimits.TransformTimeoutMs = 0;
        var r = Run("{out}\nr = %upper \"hi\"");
        Assert.True(r.Success);
        Assert.Empty(r.Errors);
    }

    [Fact]
    public void DoesNotChargeFuelWhenCapIsZero()
    {
        SecurityLimits.MaxTransformFuel = 0;
        var r = Run("{out}\nr = %sort @big", BigArray(500));
        Assert.True(r.Success);
    }

    // ── fuel budget (T016) ────────────────────────────────────────────────

    [Fact]
    public void AbortsOverComputingTransformWithFailedResult()
    {
        SecurityLimits.MaxTransformFuel = 50;
        var r = Run("{out}\nr = %sort @big", BigArray(200));
        Assert.False(r.Success);
        Assert.True(HasCode(r.Errors, "T016"));
    }

    [Fact]
    public void ChargesArrayVerbsProportionalToWidth()
    {
        SecurityLimits.MaxTransformFuel = 50;
        var small = Run("{out}\nr = %sort @big", BigArray(3, 1, 2));
        Assert.True(small.Success);

        var large = Run("{out}\nr = %sort @big", BigArray(200));
        Assert.False(large.Success);
        Assert.True(HasCode(large.Errors, "T016"));
    }

    // ── wall-clock timeout (T017) ─────────────────────────────────────────

    [Fact]
    public void AbortsWhenElapsedExceedsTimeout()
    {
        SecurityLimits.TransformTimeoutMs = 100;
        // Each read advances well past the bound, so any read after the engine's
        // start time reports elapsed > timeout regardless of call ordering.
        long clock = 0;
        TransformEngine.Clock = () => clock += 10_000;
        var r = Run("{out}\nr = %sort @big", BigArray(300));
        Assert.False(r.Success);
        Assert.True(HasCode(r.Errors, "T017"));
    }

    // ── expression depth (T018) ───────────────────────────────────────────

    [Fact]
    public void YieldsDepthErrorUnderLowCap()
    {
        SecurityLimits.MaxExpressionDepth = 8;
        var r = Run(NestAbs(30));
        Assert.False(r.Success);
        Assert.True(HasCode(r.Errors, "T018"));
    }

    [Fact]
    public void ConvertsDeepNestingIntoTypedErrorNotStackOverflow()
    {
        // Standing default (32); deep nesting must not blow the native stack.
        var r = Run(NestAbs(200));
        Assert.False(r.Success);
        Assert.True(HasCode(r.Errors, "T018"));
    }

    // ── non-swallowable abort ─────────────────────────────────────────────

    [Fact]
    public void IsNotDowngradedToWarningByOnError()
    {
        SecurityLimits.MaxTransformFuel = 50;
        var r = Run("{out}\nr = %sort @big", BigArray(200), HeaderWith("warn"));
        Assert.False(r.Success);
        Assert.True(HasCode(r.Errors, "T016"));
        Assert.False(HasCode(r.Warnings, "T016"));
    }
}
