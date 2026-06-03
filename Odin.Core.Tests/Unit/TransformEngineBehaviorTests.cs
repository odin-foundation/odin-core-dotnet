using Odin.Core;
using Odin.Core.Transform;
using Odin.Core.Types;
using Xunit;

namespace Odin.Core.Tests.Unit;

/// <summary>
/// Engine-level behaviors that span verbs: multi-sink accumulation in one loop
/// pass, and lazy control-flow (short-circuit and selected-branch-only evaluation).
/// </summary>
public class TransformEngineBehaviorTests
{
    private static DynValue Json(string json) => JsonSourceParser.Parse(json);

    private static TransformResult Run(string transformText, string inputJson)
    {
        var transform = Core.Odin.ParseTransform(transformText);
        return TransformEngine.Execute(transform, Json(inputJson));
    }

    // ─────────────────────────────────────────────────────────────────
    // Multi-sink: two %accumulate calls in one loop pass both advance
    // ─────────────────────────────────────────────────────────────────

    private const string MultiSinkDoc = @"{$}
odin = ""1.0.0""
transform = ""1.0.0""
direction = ""json->json""
target.format = ""json""

{$accumulator}
total = ##0
total._persist = true
count = ##0
count._persist = true

{lines[]}
_loop = ""@items""
amount = @.amount
_ = %accumulate total @.amount
_count = %accumulate count ##1

{summary}
total = ""@$accumulator.total""
count = ""@$accumulator.count""
";

    [Fact]
    public void MultiSink_RunningTotalAndCountBothAdvance()
    {
        var r = Run(MultiSinkDoc, @"{""items"":[{""amount"":10},{""amount"":20},{""amount"":30}]}");
        Assert.True(r.Success);
        var summary = r.Output!.Get("summary")!;
        Assert.Equal(60L, summary.Get("total")!.AsInt64());
        Assert.Equal(3L, summary.Get("count")!.AsInt64());
    }

    [Fact]
    public void MultiSink_SinkFieldsAreNotEmitted()
    {
        var r = Run(MultiSinkDoc, @"{""items"":[{""amount"":5}]}");
        Assert.True(r.Success);
        var line = r.Output!.Get("lines")!.AsArray()![0];
        Assert.Null(line.Get("_"));
        Assert.Null(line.Get("_count"));
        Assert.Equal(5L, line.Get("amount")!.AsInt64());
    }

    // ─────────────────────────────────────────────────────────────────
    // Lazy control flow: and/or short-circuit, ifElse runs only one branch
    // ─────────────────────────────────────────────────────────────────

    private const string LazyDoc = @"{$}
odin = ""1.0.0""
transform = ""1.0.0""
direction = ""json->json""
target.format = ""json""

{$accumulator}
andRhs = ##0
andRhs._persist = true
orRhs = ##0
orRhs._persist = true
chosen = ##0
chosen._persist = true
skipped = ##0
skipped._persist = true

{_eval}
_a = %and ?false %accumulate andRhs ##1
_b = %or ?true %accumulate orRhs ##1
_c = %ifElse ?true %accumulate chosen ##1 %accumulate skipped ##1

{out}
andRhsRan = ""@$accumulator.andRhs""
orRhsRan = ""@$accumulator.orRhs""
chosenRan = ""@$accumulator.chosen""
skippedRan = ""@$accumulator.skipped""
";

    [Fact]
    public void Lazy_AndOrShortCircuit_AndIfElseSelectsOneBranch()
    {
        var r = Run(LazyDoc, @"{""seed"":0}");
        Assert.True(r.Success);
        var outSec = r.Output!.Get("out")!;
        // and(false, rhs) never evaluates rhs; or(true, rhs) never evaluates rhs.
        Assert.Equal(0L, outSec.Get("andRhsRan")!.AsInt64());
        Assert.Equal(0L, outSec.Get("orRhsRan")!.AsInt64());
        // ifElse(true, a, b) runs only the chosen branch.
        Assert.Equal(1L, outSec.Get("chosenRan")!.AsInt64());
        Assert.Equal(0L, outSec.Get("skippedRan")!.AsInt64());
    }

    // coalesce / ifNull / ifEmpty also pick a single value lazily; an unselected
    // accumulate side effect must not fire.
    private const string CoalesceLazyDoc = @"{$}
odin = ""1.0.0""
transform = ""1.0.0""
direction = ""json->json""
target.format = ""json""

{$accumulator}
fallbackRan = ##0
fallbackRan._persist = true

{_eval}
_c = %coalesce ""present"" %accumulate fallbackRan ##1

{out}
fallbackRan = ""@$accumulator.fallbackRan""
";

    [Fact]
    public void Lazy_CoalesceSkipsFallbackSideEffect()
    {
        var r = Run(CoalesceLazyDoc, @"{""seed"":0}");
        Assert.True(r.Success);
        Assert.Equal(0L, r.Output!.Get("out")!.Get("fallbackRan")!.AsInt64());
    }
}
