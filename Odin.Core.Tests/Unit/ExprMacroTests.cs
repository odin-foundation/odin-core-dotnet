using Odin.Core;
using Odin.Core.Transform;
using Odin.Core.Types;
using Xunit;

namespace Odin.Core.Tests.Unit;

/// <summary>
/// Tests for the %expr parse-time macro: it compiles an infix formula into a tree
/// of numeric verbs. Precedence, associativity, operators, whitelisted functions,
/// variable bindings, and compile-time errors are covered through full transforms.
/// </summary>
public class ExprMacroTests
{
    private const string Header = "{$}\n"
        + "odin = \"1.0.0\"\n"
        + "transform = \"1.0.0\"\n"
        + "direction = \"json->json\"\n"
        + "target.format = \"json\"\n\n";

    // Build a transform whose single output field is %expr <formula>.
    private static DynValue Eval(string formula)
    {
        var text = Header + "{out}\nr = %expr \"" + formula + "\"\n";
        var transform = Core.Odin.ParseTransform(text);
        var r = TransformEngine.Execute(transform, DynValue.Object(new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, DynValue>>()));
        Assert.True(r.Success);
        return r.Output!.Get("out")!.Get("r")!;
    }

    // Build a transform that supplies a bindings object at @.v.
    private static DynValue EvalWith(string formula, string inputJson)
    {
        var text = Header + "{out}\nr = %expr \"" + formula + "\" @.v\n";
        var transform = Core.Odin.ParseTransform(text);
        var r = TransformEngine.Execute(transform, JsonSourceParser.Parse(inputJson));
        Assert.True(r.Success);
        return r.Output!.Get("out")!.Get("r")!;
    }

    private static System.Exception CompileError(string formula)
    {
        var text = Header + "{out}\nr = %expr \"" + formula + "\"\n";
        return Record.Exception(() => Core.Odin.ParseTransform(text))!;
    }

    // ─────────────────────────────────────────────────────────────────
    // Precedence & associativity
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Precedence_MultiplyBeforeAdd()
        => Assert.Equal(14L, Eval("2 + 3 * 4").AsInt64());

    [Fact]
    public void Power_IsRightAssociative()
        => Assert.Equal(512L, Eval("2^3^2").AsInt64());

    [Fact]
    public void UnaryMinus_LooserThanPower()
        => Assert.Equal(-4L, Eval("-2^2").AsInt64());

    [Fact]
    public void ParenNegatesBaseBeforePower()
        => Assert.Equal(4L, Eval("(-2)^2").AsInt64());

    [Fact]
    public void StackedUnaryMinus()
        => Assert.Equal(2L, Eval("--2").AsInt64());

    [Fact]
    public void NestedParens()
        => Assert.Equal(9L, Eval("((1 + 2) * 3)").AsInt64());

    // ─────────────────────────────────────────────────────────────────
    // Operators
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Division_YieldsFraction()
        => Assert.Equal(0.5, Eval("1 / 2").AsDouble());

    [Fact]
    public void Modulo()
        => Assert.Equal(1L, Eval("5 % 2").AsInt64());

    [Fact]
    public void DivisionByZero_IsNull()
        => Assert.True(Eval("1 / 0").IsNull);

    // ─────────────────────────────────────────────────────────────────
    // Functions
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Abs_Function()
        => Assert.Equal(7L, Eval("abs(-7)").AsInt64());

    [Fact]
    public void MinMax_Variadic()
        => Assert.Equal(6L, Eval("min(3, 5, 1) + max(3, 5, 1)").AsInt64());

    [Fact]
    public void Round_DefaultScale()
        => Assert.Equal(4L, Eval("round(3.7)").AsInt64());

    // ─────────────────────────────────────────────────────────────────
    // Variables under an explicit bindings object
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Variables_ResolveUnderBindings_Pythagoras()
        => Assert.Equal(5L, EvalWith("sqrt(x^2 + y^2)", @"{""v"":{""x"":3,""y"":4}}").AsInt64());

    // ─────────────────────────────────────────────────────────────────
    // Compile-time errors
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Error_UnknownFunction()
    {
        var ex = CompileError("sin(1)");
        Assert.IsType<ExprSyntaxException>(ex);
        Assert.Equal("T015", ((ExprSyntaxException)ex).Code);
    }

    [Fact]
    public void Error_UnbalancedParens()
        => Assert.IsType<ExprSyntaxException>(CompileError("(1 + 2"));

    [Fact]
    public void Error_VariableWithoutBindings()
        => Assert.IsType<ExprSyntaxException>(CompileError("x + 1"));
}
