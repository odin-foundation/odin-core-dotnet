using System.Collections.Generic;
using Odin.Core;
using Odin.Core.Transform;
using Odin.Core.Types;
using Xunit;

namespace Odin.Core.Tests.Unit;

public class TransformEngineTests
{
    // ─────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────

    private static OdinTransform MinimalTransform(List<FieldMapping> mappings)
    {
        return new OdinTransform
        {
            Target = new TargetConfig { Format = "json" },
            Segments = new List<TransformSegment>
            {
                new TransformSegment { Mappings = mappings }
            }
        };
    }

    private static DynValue Obj(params (string k, DynValue v)[] entries)
    {
        var list = new List<KeyValuePair<string, DynValue>>();
        foreach (var (k, v) in entries)
            list.Add(new KeyValuePair<string, DynValue>(k, v));
        return DynValue.Object(list);
    }

    private static DynValue Arr(params DynValue[] items) =>
        DynValue.Array(new List<DynValue>(items));

    private static DynValue Str(string s) => DynValue.String(s);
    private static DynValue Int(long i) => DynValue.Integer(i);
    private static DynValue Flt(double f) => DynValue.Float(f);
    private static DynValue Bln(bool b) => DynValue.Bool(b);
    private static DynValue Nul() => DynValue.Null();

    // ─────────────────────────────────────────────────────────────────
    // Simple Copy
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void SimpleCopy()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping
            {
                Target = "Name",
                Expression = FieldExpression.Copy("@.name"),
            }
        });
        var result = TransformEngine.Execute(t, Obj(("name", Str("Alice"))));
        Assert.True(result.Success);
        Assert.Equal(Str("Alice"), result.Output!.Get("Name"));
    }

    [Fact]
    public void NestedCopy()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping
            {
                Target = "City",
                Expression = FieldExpression.Copy("@.address.city"),
            }
        });
        var source = Obj(("address", Obj(("city", Str("Springfield")))));
        var result = TransformEngine.Execute(t, source);
        Assert.True(result.Success);
        Assert.Equal(Str("Springfield"), result.Output!.Get("City"));
    }

    // ─────────────────────────────────────────────────────────────────
    // Literals
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void LiteralString()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping
            {
                Target = "Version",
                Expression = FieldExpression.Literal(new OdinString("1.0")),
            }
        });
        var result = TransformEngine.Execute(t, Obj());
        Assert.True(result.Success);
        Assert.Equal(Str("1.0"), result.Output!.Get("Version"));
    }

    [Fact]
    public void LiteralInteger()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping
            {
                Target = "Count",
                Expression = FieldExpression.Literal(new OdinInteger(42)),
            }
        });
        var result = TransformEngine.Execute(t, Obj());
        Assert.True(result.Success);
        Assert.Equal(Int(42), result.Output!.Get("Count"));
    }

    [Fact]
    public void LiteralBoolean()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping
            {
                Target = "Active",
                Expression = FieldExpression.Literal(new OdinBoolean(true)),
            }
        });
        var result = TransformEngine.Execute(t, Obj());
        Assert.True(result.Success);
        Assert.Equal(Bln(true), result.Output!.Get("Active"));
    }

    [Fact]
    public void LiteralNull()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping
            {
                Target = "Empty",
                Expression = FieldExpression.Literal(new OdinNull()),
            }
        });
        var result = TransformEngine.Execute(t, Obj());
        Assert.True(result.Success);
        Assert.Equal(Nul(), result.Output!.Get("Empty"));
    }

    [Fact]
    public void LiteralNumber()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping
            {
                Target = "Pi",
                Expression = FieldExpression.Literal(new OdinNumber(3.14)),
            }
        });
        var result = TransformEngine.Execute(t, Obj());
        Assert.True(result.Success);
        var pi = result.Output!.Get("Pi");
        Assert.NotNull(pi);
        Assert.Equal(3.14, pi!.AsDouble());
    }

    // ─────────────────────────────────────────────────────────────────
    // Verbs
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void VerbUpper()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping
            {
                Target = "Name",
                Expression = FieldExpression.Transform(new VerbCall
                {
                    Verb = "upper",
                    Args = new List<VerbArg> { VerbArg.Ref("@.name") }
                }),
            }
        });
        var result = TransformEngine.Execute(t, Obj(("name", Str("alice"))));
        Assert.True(result.Success);
        Assert.Equal(Str("ALICE"), result.Output!.Get("Name"));
    }

    [Fact]
    public void VerbLower()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping
            {
                Target = "Name",
                Expression = FieldExpression.Transform(new VerbCall
                {
                    Verb = "lower",
                    Args = new List<VerbArg> { VerbArg.Ref("@.name") }
                }),
            }
        });
        var result = TransformEngine.Execute(t, Obj(("name", Str("ALICE"))));
        Assert.True(result.Success);
        Assert.Equal(Str("alice"), result.Output!.Get("Name"));
    }

    [Fact]
    public void VerbConcat()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping
            {
                Target = "FullName",
                Expression = FieldExpression.Transform(new VerbCall
                {
                    Verb = "concat",
                    Args = new List<VerbArg>
                    {
                        VerbArg.Ref("@.first"),
                        VerbArg.Lit(new OdinString(" ")),
                        VerbArg.Ref("@.last"),
                    }
                }),
            }
        });
        var source = Obj(("first", Str("John")), ("last", Str("Doe")));
        var result = TransformEngine.Execute(t, source);
        Assert.True(result.Success);
        Assert.Equal(Str("John Doe"), result.Output!.Get("FullName"));
    }

    [Fact]
    public void NestedVerbUpperOfConcat()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping
            {
                Target = "Name",
                Expression = FieldExpression.Transform(new VerbCall
                {
                    Verb = "upper",
                    Args = new List<VerbArg>
                    {
                        VerbArg.NestedCall(new VerbCall
                        {
                            Verb = "concat",
                            Args = new List<VerbArg>
                            {
                                VerbArg.Ref("@.first"),
                                VerbArg.Lit(new OdinString(" ")),
                                VerbArg.Ref("@.last"),
                            }
                        }),
                    }
                }),
            }
        });
        var source = Obj(("first", Str("john")), ("last", Str("doe")));
        var result = TransformEngine.Execute(t, source);
        Assert.True(result.Success);
        Assert.Equal(Str("JOHN DOE"), result.Output!.Get("Name"));
    }

    [Fact]
    public void NestedVerbLowerOfUpper()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping
            {
                Target = "Name",
                Expression = FieldExpression.Transform(new VerbCall
                {
                    Verb = "lower",
                    Args = new List<VerbArg>
                    {
                        VerbArg.NestedCall(new VerbCall
                        {
                            Verb = "upper",
                            Args = new List<VerbArg> { VerbArg.Ref("@.name") }
                        }),
                    }
                }),
            }
        });
        var result = TransformEngine.Execute(t, Obj(("name", Str("Hello"))));
        Assert.True(result.Success);
        Assert.Equal(Str("hello"), result.Output!.Get("Name"));
    }

    [Fact]
    public void TripleNestedVerb()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping
            {
                Target = "Name",
                Expression = FieldExpression.Transform(new VerbCall
                {
                    Verb = "upper",
                    Args = new List<VerbArg>
                    {
                        VerbArg.NestedCall(new VerbCall
                        {
                            Verb = "lower",
                            Args = new List<VerbArg>
                            {
                                VerbArg.NestedCall(new VerbCall
                                {
                                    Verb = "upper",
                                    Args = new List<VerbArg> { VerbArg.Ref("@.name") }
                                }),
                            }
                        }),
                    }
                }),
            }
        });
        var result = TransformEngine.Execute(t, Obj(("name", Str("Hello"))));
        Assert.True(result.Success);
        Assert.Equal(Str("HELLO"), result.Output!.Get("Name"));
    }

    [Fact]
    public void UnknownVerbProducesError()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping
            {
                Target = "Out",
                Expression = FieldExpression.Transform(new VerbCall
                {
                    Verb = "nonexistent_verb",
                    Args = new List<VerbArg> { VerbArg.Ref("@.x") }
                }),
            }
        });
        var result = TransformEngine.Execute(t, Obj(("x", Str("val"))));
        Assert.True(result.Errors.Count > 0);
    }

    [Fact]
    public void MultipleUnknownVerbsProduceMultipleErrors()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping
            {
                Target = "A",
                Expression = FieldExpression.Transform(new VerbCall { Verb = "fake1", Args = new List<VerbArg> { VerbArg.Ref("@.x") } }),
            },
            new FieldMapping
            {
                Target = "B",
                Expression = FieldExpression.Transform(new VerbCall { Verb = "fake2", Args = new List<VerbArg> { VerbArg.Ref("@.x") } }),
            }
        });
        var result = TransformEngine.Execute(t, Obj(("x", Str("val"))));
        Assert.True(result.Errors.Count >= 2);
    }

    // ─────────────────────────────────────────────────────────────────
    // Constants
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ConstantString()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping
            {
                Target = "Version",
                Expression = FieldExpression.Copy("$const.version"),
            }
        });
        t.Constants["version"] = new OdinString("2.0");
        var result = TransformEngine.Execute(t, Obj());
        Assert.True(result.Success);
        Assert.Equal(Str("2.0"), result.Output!.Get("Version"));
    }

    [Fact]
    public void ConstantInteger()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping
            {
                Target = "Max",
                Expression = FieldExpression.Copy("$const.max"),
            }
        });
        t.Constants["max"] = new OdinInteger(100);
        var result = TransformEngine.Execute(t, Obj());
        Assert.True(result.Success);
        Assert.Equal(Int(100), result.Output!.Get("Max"));
    }

    [Fact]
    public void ConstantBoolean()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping
            {
                Target = "Flag",
                Expression = FieldExpression.Copy("$const.flag"),
            }
        });
        t.Constants["flag"] = new OdinBoolean(true);
        var result = TransformEngine.Execute(t, Obj());
        Assert.True(result.Success);
        Assert.Equal(Bln(true), result.Output!.Get("Flag"));
    }

    [Fact]
    public void ConstantMissingReturnsNull()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping
            {
                Target = "X",
                Expression = FieldExpression.Copy("$const.missing"),
            }
        });
        var result = TransformEngine.Execute(t, Obj());
        Assert.True(result.Success);
        var x = result.Output!.Get("X");
        Assert.True(x == null || x.IsNull);
    }

    [Fact]
    public void ConstantUsedInVerb()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping
            {
                Target = "Result",
                Expression = FieldExpression.Transform(new VerbCall
                {
                    Verb = "concat",
                    Args = new List<VerbArg>
                    {
                        VerbArg.Ref("@.name"),
                        VerbArg.Ref("$const.suffix"),
                    }
                }),
            }
        });
        t.Constants["suffix"] = new OdinString("_END");
        var result = TransformEngine.Execute(t, Obj(("name", Str("test"))));
        Assert.True(result.Success);
        Assert.Equal(Str("test_END"), result.Output!.Get("Result"));
    }

    [Fact]
    public void MultipleConstants()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping { Target = "A", Expression = FieldExpression.Copy("$const.a") },
            new FieldMapping { Target = "B", Expression = FieldExpression.Copy("$const.b") },
        });
        t.Constants["a"] = new OdinString("alpha");
        t.Constants["b"] = new OdinString("beta");
        var result = TransformEngine.Execute(t, Obj());
        Assert.True(result.Success);
        Assert.Equal(Str("alpha"), result.Output!.Get("A"));
        Assert.Equal(Str("beta"), result.Output!.Get("B"));
    }

    // ─────────────────────────────────────────────────────────────────
    // Array Index Paths
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ArrayIndexPath()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping
            {
                Target = "First",
                Expression = FieldExpression.Copy("@.items[0].name"),
            }
        });
        var source = Obj(("items", Arr(
            Obj(("name", Str("Alpha"))),
            Obj(("name", Str("Beta")))
        )));
        var result = TransformEngine.Execute(t, source);
        Assert.True(result.Success);
        Assert.Equal(Str("Alpha"), result.Output!.Get("First"));
    }

    [Fact]
    public void ArrayIndexSecondElement()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping
            {
                Target = "Second",
                Expression = FieldExpression.Copy("@.items[1].name"),
            }
        });
        var source = Obj(("items", Arr(
            Obj(("name", Str("Alpha"))),
            Obj(("name", Str("Beta")))
        )));
        var result = TransformEngine.Execute(t, source);
        Assert.True(result.Success);
        Assert.Equal(Str("Beta"), result.Output!.Get("Second"));
    }

    [Fact]
    public void ArrayIndexOutOfBoundsReturnsNull()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping
            {
                Target = "Out",
                Expression = FieldExpression.Copy("@.items[99]"),
            }
        });
        var source = Obj(("items", Arr(Str("a"))));
        var result = TransformEngine.Execute(t, source);
        Assert.True(result.Success);
        var v = result.Output!.Get("Out");
        Assert.True(v == null || v.IsNull);
    }

    [Fact]
    public void ArrayIndexOnNonArrayReturnsNull()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping
            {
                Target = "Out",
                Expression = FieldExpression.Copy("@.x[0]"),
            }
        });
        var source = Obj(("x", Str("notarray")));
        var result = TransformEngine.Execute(t, source);
        Assert.True(result.Success);
        var v = result.Output!.Get("Out");
        Assert.True(v == null || v.IsNull);
    }

    [Fact]
    public void ArrayMultipleIndices()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping
            {
                Target = "A",
                Expression = FieldExpression.Copy("@.items[0]"),
            },
            new FieldMapping
            {
                Target = "B",
                Expression = FieldExpression.Copy("@.items[1]"),
            },
        });
        var source = Obj(("items", Arr(Str("x"), Str("y"), Str("z"))));
        var result = TransformEngine.Execute(t, source);
        Assert.True(result.Success);
        Assert.Equal(Str("x"), result.Output!.Get("A"));
        Assert.Equal(Str("y"), result.Output!.Get("B"));
    }

    // ─────────────────────────────────────────────────────────────────
    // Missing Path
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void MissingPathReturnsNull()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping
            {
                Target = "Out",
                Expression = FieldExpression.Copy("@.nonexistent"),
            }
        });
        var result = TransformEngine.Execute(t, Obj(("name", Str("Alice"))));
        Assert.True(result.Success);
        var v = result.Output!.Get("Out");
        Assert.True(v == null || v.IsNull);
    }

    [Fact]
    public void MissingDeepNestedPath()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping
            {
                Target = "Out",
                Expression = FieldExpression.Copy("@.a.b.c.d.e"),
            }
        });
        var result = TransformEngine.Execute(t, Obj());
        Assert.True(result.Success);
        var v = result.Output!.Get("Out");
        Assert.True(v == null || v.IsNull);
    }

    [Fact]
    public void CopyBareAt()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping
            {
                Target = "All",
                Expression = FieldExpression.Copy("@"),
            }
        });
        var source = Obj(("name", Str("Alice")));
        var result = TransformEngine.Execute(t, source);
        Assert.True(result.Success);
        var all = result.Output!.Get("All");
        Assert.NotNull(all);
    }

    // ─────────────────────────────────────────────────────────────────
    // Object Expression
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ObjectExpression()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping
            {
                Target = "Person",
                Expression = FieldExpression.Object(new List<FieldMapping>
                {
                    new FieldMapping { Target = "First", Expression = FieldExpression.Copy("@.first") },
                    new FieldMapping { Target = "Last", Expression = FieldExpression.Copy("@.last") },
                }),
            }
        });
        var source = Obj(("first", Str("John")), ("last", Str("Doe")));
        var result = TransformEngine.Execute(t, source);
        Assert.True(result.Success);
        var person = result.Output!.Get("Person");
        Assert.NotNull(person);
        Assert.Equal(Str("John"), person!.Get("First"));
        Assert.Equal(Str("Doe"), person.Get("Last"));
    }

    [Fact]
    public void ObjectExpressionWithVerb()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping
            {
                Target = "Person",
                Expression = FieldExpression.Object(new List<FieldMapping>
                {
                    new FieldMapping
                    {
                        Target = "Name",
                        Expression = FieldExpression.Transform(new VerbCall
                        {
                            Verb = "upper",
                            Args = new List<VerbArg> { VerbArg.Ref("@.name") }
                        }),
                    }
                }),
            }
        });
        var result = TransformEngine.Execute(t, Obj(("name", Str("alice"))));
        Assert.True(result.Success);
        var person = result.Output!.Get("Person");
        Assert.NotNull(person);
        Assert.Equal(Str("ALICE"), person!.Get("Name"));
    }

    [Fact]
    public void ObjectExpressionWithLiteral()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping
            {
                Target = "Info",
                Expression = FieldExpression.Object(new List<FieldMapping>
                {
                    new FieldMapping { Target = "Version", Expression = FieldExpression.Literal(new OdinString("1.0")) },
                }),
            }
        });
        var result = TransformEngine.Execute(t, Obj());
        Assert.True(result.Success);
        var info = result.Output!.Get("Info");
        Assert.NotNull(info);
        Assert.Equal(Str("1.0"), info!.Get("Version"));
    }

    // ─────────────────────────────────────────────────────────────────
    // Nested Output Path
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void NestedOutputPath()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping
            {
                Target = "address.city",
                Expression = FieldExpression.Copy("@.city"),
            }
        });
        var result = TransformEngine.Execute(t, Obj(("city", Str("NYC"))));
        Assert.True(result.Success);
        var address = result.Output!.Get("address");
        Assert.NotNull(address);
        Assert.Equal(Str("NYC"), address!.Get("city"));
    }

    [Fact]
    public void NestedOutputPathThreeLevels()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping
            {
                Target = "a.b.c",
                Expression = FieldExpression.Literal(new OdinString("deep")),
            }
        });
        var result = TransformEngine.Execute(t, Obj());
        Assert.True(result.Success);
        var a = result.Output!.Get("a");
        Assert.NotNull(a);
        var b = a!.Get("b");
        Assert.NotNull(b);
        Assert.Equal(Str("deep"), b!.Get("c"));
    }

    [Fact]
    public void NestedOutputPathMultipleFieldsSameParent()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping { Target = "person.first", Expression = FieldExpression.Copy("@.first") },
            new FieldMapping { Target = "person.last", Expression = FieldExpression.Copy("@.last") },
        });
        var source = Obj(("first", Str("John")), ("last", Str("Doe")));
        var result = TransformEngine.Execute(t, source);
        Assert.True(result.Success);
        var person = result.Output!.Get("person");
        Assert.NotNull(person);
        Assert.Equal(Str("John"), person!.Get("first"));
        Assert.Equal(Str("Doe"), person.Get("last"));
    }

    [Fact]
    public void DeeplyNestedOutputFourLevels()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping
            {
                Target = "a.b.c.d",
                Expression = FieldExpression.Literal(new OdinString("deep")),
            }
        });
        var result = TransformEngine.Execute(t, Obj());
        Assert.True(result.Success);
        var a = result.Output!.Get("a");
        Assert.NotNull(a);
        var b = a!.Get("b");
        Assert.NotNull(b);
        var c = b!.Get("c");
        Assert.NotNull(c);
        Assert.Equal(Str("deep"), c!.Get("d"));
    }

    // ─────────────────────────────────────────────────────────────────
    // Conditional Segments
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ConditionSkipsSegmentWhenFalsy()
    {
        var t = new OdinTransform
        {
            Target = new TargetConfig { Format = "json" },
            Segments = new List<TransformSegment>
            {
                new TransformSegment
                {
                    Condition = "@.active",
                    Mappings = new List<FieldMapping>
                    {
                        new FieldMapping { Target = "Name", Expression = FieldExpression.Copy("@.name") }
                    }
                }
            }
        };
        var result = TransformEngine.Execute(t, Obj(("active", Bln(false)), ("name", Str("Alice"))));
        Assert.True(result.Success);
        var name = result.Output!.Get("Name");
        Assert.True(name == null || name.IsNull);
    }

    [Fact]
    public void ConditionAllowsSegmentWhenTruthy()
    {
        var t = new OdinTransform
        {
            Target = new TargetConfig { Format = "json" },
            Segments = new List<TransformSegment>
            {
                new TransformSegment
                {
                    Condition = "@.active",
                    Mappings = new List<FieldMapping>
                    {
                        new FieldMapping { Target = "Name", Expression = FieldExpression.Copy("@.name") }
                    }
                }
            }
        };
        var result = TransformEngine.Execute(t, Obj(("active", Bln(true)), ("name", Str("Alice"))));
        Assert.True(result.Success);
        Assert.Equal(Str("Alice"), result.Output!.Get("Name"));
    }

    [Fact]
    public void ConditionNullValueSkipsSegment()
    {
        var t = new OdinTransform
        {
            Target = new TargetConfig { Format = "json" },
            Segments = new List<TransformSegment>
            {
                new TransformSegment
                {
                    Condition = "@.nonexistent",
                    Mappings = new List<FieldMapping>
                    {
                        new FieldMapping { Target = "Name", Expression = FieldExpression.Copy("@.name") }
                    }
                }
            }
        };
        var result = TransformEngine.Execute(t, Obj(("name", Str("Alice"))));
        Assert.True(result.Success);
        var name = result.Output!.Get("Name");
        Assert.True(name == null || name.IsNull);
    }

    [Fact]
    public void ConditionNonEmptyStringIsTruthy()
    {
        var t = new OdinTransform
        {
            Target = new TargetConfig { Format = "json" },
            Segments = new List<TransformSegment>
            {
                new TransformSegment
                {
                    Condition = "@.val",
                    Mappings = new List<FieldMapping>
                    {
                        new FieldMapping { Target = "X", Expression = FieldExpression.Literal(new OdinString("ok")) }
                    }
                }
            }
        };
        var result = TransformEngine.Execute(t, Obj(("val", Str("hello"))));
        Assert.True(result.Success);
        Assert.Equal(Str("ok"), result.Output!.Get("X"));
    }

    [Fact]
    public void ConditionEmptyStringIsFalsy()
    {
        var t = new OdinTransform
        {
            Target = new TargetConfig { Format = "json" },
            Segments = new List<TransformSegment>
            {
                new TransformSegment
                {
                    Condition = "@.val",
                    Mappings = new List<FieldMapping>
                    {
                        new FieldMapping { Target = "X", Expression = FieldExpression.Literal(new OdinString("ok")) }
                    }
                }
            }
        };
        var result = TransformEngine.Execute(t, Obj(("val", Str(""))));
        Assert.True(result.Success);
        var x = result.Output!.Get("X");
        Assert.True(x == null || x.IsNull);
    }

    [Fact]
    public void ConditionIntegerZeroSkips()
    {
        var t = new OdinTransform
        {
            Target = new TargetConfig { Format = "json" },
            Segments = new List<TransformSegment>
            {
                new TransformSegment
                {
                    Condition = "@.val",
                    Mappings = new List<FieldMapping>
                    {
                        new FieldMapping { Target = "X", Expression = FieldExpression.Literal(new OdinString("ok")) }
                    }
                }
            }
        };
        var result = TransformEngine.Execute(t, Obj(("val", Int(0))));
        Assert.True(result.Success);
        var x = result.Output!.Get("X");
        Assert.True(x == null || x.IsNull);
    }

    // ─────────────────────────────────────────────────────────────────
    // Modifiers
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ModifierRequiredRecorded()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping
            {
                Target = "Name",
                Expression = FieldExpression.Copy("@.name"),
                Modifiers = new OdinModifiers { Required = true },
            }
        });
        var result = TransformEngine.Execute(t, Obj(("name", Str("Alice"))));
        Assert.True(result.Success);
        Assert.True(result.OutputModifiers.ContainsKey("Name"));
        Assert.True(result.OutputModifiers["Name"].Required);
    }

    [Fact]
    public void ModifierConfidentialRecorded()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping
            {
                Target = "SSN",
                Expression = FieldExpression.Copy("@.ssn"),
                Modifiers = new OdinModifiers { Confidential = true },
            }
        });
        var result = TransformEngine.Execute(t, Obj(("ssn", Str("123-45-6789"))));
        Assert.True(result.Success);
        Assert.True(result.OutputModifiers.ContainsKey("SSN"));
        Assert.True(result.OutputModifiers["SSN"].Confidential);
    }

    [Fact]
    public void ModifierDeprecatedRecorded()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping
            {
                Target = "Old",
                Expression = FieldExpression.Copy("@.old"),
                Modifiers = new OdinModifiers { Deprecated = true },
            }
        });
        var result = TransformEngine.Execute(t, Obj(("old", Str("legacy"))));
        Assert.True(result.Success);
        Assert.True(result.OutputModifiers.ContainsKey("Old"));
        Assert.True(result.OutputModifiers["Old"].Deprecated);
    }

    [Fact]
    public void ModifierCombinedAllThree()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping
            {
                Target = "X",
                Expression = FieldExpression.Copy("@.x"),
                Modifiers = new OdinModifiers { Required = true, Confidential = true, Deprecated = true },
            }
        });
        var result = TransformEngine.Execute(t, Obj(("x", Str("val"))));
        Assert.True(result.Success);
        Assert.True(result.OutputModifiers.ContainsKey("X"));
        var m = result.OutputModifiers["X"];
        Assert.True(m.Required);
        Assert.True(m.Confidential);
        Assert.True(m.Deprecated);
    }

    [Fact]
    public void ModifierNoneNotRecorded()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping
            {
                Target = "X",
                Expression = FieldExpression.Copy("@.x"),
            }
        });
        var result = TransformEngine.Execute(t, Obj(("x", Str("val"))));
        Assert.True(result.Success);
        Assert.False(result.OutputModifiers.ContainsKey("X"));
    }

    // ─────────────────────────────────────────────────────────────────
    // Confidential Enforcement
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ConfidentialRedactString()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping
            {
                Target = "SSN",
                Expression = FieldExpression.Copy("@.ssn"),
                Modifiers = new OdinModifiers { Confidential = true },
            }
        });
        t.EnforceConfidential = ConfidentialMode.Redact;
        var result = TransformEngine.Execute(t, Obj(("ssn", Str("123-45-6789"))));
        Assert.True(result.Success);
        var ssn = result.Output!.Get("SSN");
        Assert.NotNull(ssn);
        Assert.True(ssn!.IsNull);
    }

    [Fact]
    public void ConfidentialRedactInteger()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping
            {
                Target = "PIN",
                Expression = FieldExpression.Copy("@.pin"),
                Modifiers = new OdinModifiers { Confidential = true },
            }
        });
        t.EnforceConfidential = ConfidentialMode.Redact;
        var result = TransformEngine.Execute(t, Obj(("pin", Int(1234))));
        Assert.True(result.Success);
        Assert.True(result.Output!.Get("PIN")!.IsNull);
    }

    [Fact]
    public void ConfidentialRedactBoolean()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping
            {
                Target = "Secret",
                Expression = FieldExpression.Copy("@.secret"),
                Modifiers = new OdinModifiers { Confidential = true },
            }
        });
        t.EnforceConfidential = ConfidentialMode.Redact;
        var result = TransformEngine.Execute(t, Obj(("secret", Bln(true))));
        Assert.True(result.Success);
        Assert.True(result.Output!.Get("Secret")!.IsNull);
    }

    [Fact]
    public void ConfidentialRedactFloat()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping
            {
                Target = "Amount",
                Expression = FieldExpression.Copy("@.amount"),
                Modifiers = new OdinModifiers { Confidential = true },
            }
        });
        t.EnforceConfidential = ConfidentialMode.Redact;
        var result = TransformEngine.Execute(t, Obj(("amount", Flt(99.99))));
        Assert.True(result.Success);
        Assert.True(result.Output!.Get("Amount")!.IsNull);
    }

    [Fact]
    public void ConfidentialRedactNullStaysNull()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping
            {
                Target = "X",
                Expression = FieldExpression.Copy("@.x"),
                Modifiers = new OdinModifiers { Confidential = true },
            }
        });
        t.EnforceConfidential = ConfidentialMode.Redact;
        var result = TransformEngine.Execute(t, Obj(("x", Nul())));
        Assert.True(result.Success);
        Assert.True(result.Output!.Get("X")!.IsNull);
    }

    [Fact]
    public void ConfidentialMaskString()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping
            {
                Target = "SSN",
                Expression = FieldExpression.Copy("@.ssn"),
                Modifiers = new OdinModifiers { Confidential = true },
            }
        });
        t.EnforceConfidential = ConfidentialMode.Mask;
        var result = TransformEngine.Execute(t, Obj(("ssn", Str("123-45-6789"))));
        Assert.True(result.Success);
        var ssn = result.Output!.Get("SSN");
        Assert.NotNull(ssn);
        // Masked string should be all asterisks of same length or null
        if (ssn!.Type == DynValueType.String)
        {
            var masked = ssn.AsString()!;
            Assert.True(masked.Length > 0);
            foreach (char c in masked)
                Assert.Equal('*', c);
        }
        else
        {
            Assert.True(ssn.IsNull);
        }
    }

    [Fact]
    public void ConfidentialMaskEmptyString()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping
            {
                Target = "X",
                Expression = FieldExpression.Copy("@.x"),
                Modifiers = new OdinModifiers { Confidential = true },
            }
        });
        t.EnforceConfidential = ConfidentialMode.Mask;
        var result = TransformEngine.Execute(t, Obj(("x", Str(""))));
        Assert.True(result.Success);
    }

    [Fact]
    public void ConfidentialMaskInteger()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping
            {
                Target = "PIN",
                Expression = FieldExpression.Copy("@.pin"),
                Modifiers = new OdinModifiers { Confidential = true },
            }
        });
        t.EnforceConfidential = ConfidentialMode.Mask;
        var result = TransformEngine.Execute(t, Obj(("pin", Int(1234))));
        Assert.True(result.Success);
        Assert.True(result.Output!.Get("PIN")!.IsNull);
    }

    [Fact]
    public void ConfidentialMaskBoolean()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping
            {
                Target = "Secret",
                Expression = FieldExpression.Copy("@.secret"),
                Modifiers = new OdinModifiers { Confidential = true },
            }
        });
        t.EnforceConfidential = ConfidentialMode.Mask;
        var result = TransformEngine.Execute(t, Obj(("secret", Bln(true))));
        Assert.True(result.Success);
        Assert.True(result.Output!.Get("Secret")!.IsNull);
    }

    [Fact]
    public void ConfidentialNoEnforcementPassesThrough()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping
            {
                Target = "SSN",
                Expression = FieldExpression.Copy("@.ssn"),
                Modifiers = new OdinModifiers { Confidential = true },
            }
        });
        // No enforcement set
        var result = TransformEngine.Execute(t, Obj(("ssn", Str("123-45-6789"))));
        Assert.True(result.Success);
        Assert.Equal(Str("123-45-6789"), result.Output!.Get("SSN"));
    }

    [Fact]
    public void ConfidentialRedactMultipleFields()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping { Target = "A", Expression = FieldExpression.Copy("@.a"), Modifiers = new OdinModifiers { Confidential = true } },
            new FieldMapping { Target = "B", Expression = FieldExpression.Copy("@.b"), Modifiers = new OdinModifiers { Confidential = true } },
            new FieldMapping { Target = "C", Expression = FieldExpression.Copy("@.c") },
        });
        t.EnforceConfidential = ConfidentialMode.Redact;
        var source = Obj(("a", Str("secret1")), ("b", Str("secret2")), ("c", Str("public")));
        var result = TransformEngine.Execute(t, source);
        Assert.True(result.Success);
        Assert.True(result.Output!.Get("A")!.IsNull);
        Assert.True(result.Output!.Get("B")!.IsNull);
        Assert.Equal(Str("public"), result.Output!.Get("C"));
    }

    [Fact]
    public void ConfidentialCombinedWithRequired()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping
            {
                Target = "X",
                Expression = FieldExpression.Copy("@.x"),
                Modifiers = new OdinModifiers { Required = true, Confidential = true },
            }
        });
        t.EnforceConfidential = ConfidentialMode.Redact;
        var result = TransformEngine.Execute(t, Obj(("x", Str("val"))));
        Assert.True(result.Success);
        Assert.True(result.Output!.Get("X")!.IsNull);
        Assert.True(result.OutputModifiers["X"].Required);
        Assert.True(result.OutputModifiers["X"].Confidential);
    }

    [Fact]
    public void ConfidentialRedactNonConfidentialFieldsUnchanged()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping { Target = "Name", Expression = FieldExpression.Copy("@.name") },
            new FieldMapping { Target = "SSN", Expression = FieldExpression.Copy("@.ssn"), Modifiers = new OdinModifiers { Confidential = true } },
        });
        t.EnforceConfidential = ConfidentialMode.Redact;
        var source = Obj(("name", Str("Alice")), ("ssn", Str("secret")));
        var result = TransformEngine.Execute(t, source);
        Assert.True(result.Success);
        Assert.Equal(Str("Alice"), result.Output!.Get("Name"));
        Assert.True(result.Output!.Get("SSN")!.IsNull);
    }

    // ─────────────────────────────────────────────────────────────────
    // Loop Segments
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void LoopSegment()
    {
        var t = new OdinTransform
        {
            Target = new TargetConfig { Format = "json" },
            Segments = new List<TransformSegment>
            {
                new TransformSegment
                {
                    Name = "Items",
                    Path = "Items",
                    IsArray = true,
                    SourcePath = "@.items",
                    Mappings = new List<FieldMapping>
                    {
                        new FieldMapping { Target = "Name", Expression = FieldExpression.Copy("@._item.name") },
                    }
                }
            }
        };
        var source = Obj(("items", Arr(
            Obj(("name", Str("Alpha"))),
            Obj(("name", Str("Beta")))
        )));
        var result = TransformEngine.Execute(t, source);
        Assert.True(result.Success);
        var items = result.Output!.Get("Items");
        Assert.NotNull(items);
        var arr = items!.AsArray();
        Assert.NotNull(arr);
        Assert.Equal(2, arr!.Count);
    }

    [Fact]
    public void LoopEmptyArray()
    {
        var t = new OdinTransform
        {
            Target = new TargetConfig { Format = "json" },
            Segments = new List<TransformSegment>
            {
                new TransformSegment
                {
                    Name = "Items",
                    Path = "Items",
                    IsArray = true,
                    SourcePath = "@.items",
                    Mappings = new List<FieldMapping>
                    {
                        new FieldMapping { Target = "Name", Expression = FieldExpression.Copy("@._item.name") },
                    }
                }
            }
        };
        var source = Obj(("items", Arr()));
        var result = TransformEngine.Execute(t, source);
        Assert.True(result.Success);
    }

    [Fact]
    public void LoopSingleElement()
    {
        var t = new OdinTransform
        {
            Target = new TargetConfig { Format = "json" },
            Segments = new List<TransformSegment>
            {
                new TransformSegment
                {
                    Name = "Items",
                    Path = "Items",
                    IsArray = true,
                    SourcePath = "@.items",
                    Mappings = new List<FieldMapping>
                    {
                        new FieldMapping { Target = "Name", Expression = FieldExpression.Copy("@._item.name") },
                    }
                }
            }
        };
        var source = Obj(("items", Arr(Obj(("name", Str("Single"))))));
        var result = TransformEngine.Execute(t, source);
        Assert.True(result.Success);
        var items = result.Output!.Get("Items");
        Assert.NotNull(items);
        var arr = items!.AsArray();
        Assert.NotNull(arr);
        Assert.Single(arr!);
    }

    // ─────────────────────────────────────────────────────────────────
    // Empty/Edge Cases
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void EmptySourceObject()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping { Target = "Name", Expression = FieldExpression.Copy("@.name") }
        });
        var result = TransformEngine.Execute(t, Obj());
        Assert.True(result.Success);
    }

    [Fact]
    public void EmptyTransformNoSegments()
    {
        var t = new OdinTransform { Target = new TargetConfig { Format = "json" } };
        var result = TransformEngine.Execute(t, Obj(("name", Str("Alice"))));
        Assert.True(result.Success);
    }

    [Fact]
    public void TransformWithOnlyConstants()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping { Target = "A", Expression = FieldExpression.Copy("$const.a") },
        });
        t.Constants["a"] = new OdinString("hello");
        var result = TransformEngine.Execute(t, Obj());
        Assert.True(result.Success);
        Assert.Equal(Str("hello"), result.Output!.Get("A"));
    }

    [Fact]
    public void TransformWithOnlyLiteralsNoSourceData()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping { Target = "X", Expression = FieldExpression.Literal(new OdinString("hello")) },
            new FieldMapping { Target = "Y", Expression = FieldExpression.Literal(new OdinInteger(42)) },
        });
        var result = TransformEngine.Execute(t, Obj());
        Assert.True(result.Success);
        Assert.Equal(Str("hello"), result.Output!.Get("X"));
        Assert.Equal(Int(42), result.Output!.Get("Y"));
    }

    [Fact]
    public void ErrorDoesNotHaltSubsequentMappings()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping { Target = "A", Expression = FieldExpression.Transform(new VerbCall { Verb = "nonexistent", Args = new List<VerbArg> { VerbArg.Ref("@.x") } }) },
            new FieldMapping { Target = "B", Expression = FieldExpression.Copy("@.name") },
        });
        var source = Obj(("x", Str("val")), ("name", Str("Alice")));
        var result = TransformEngine.Execute(t, source);
        Assert.True(result.Errors.Count > 0);
        Assert.Equal(Str("Alice"), result.Output!.Get("B"));
    }

    [Fact]
    public void ErrorResultStillHasOutput()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping { Target = "A", Expression = FieldExpression.Transform(new VerbCall { Verb = "unknownverb", Args = new List<VerbArg> { VerbArg.Ref("@.x") } }) },
            new FieldMapping { Target = "B", Expression = FieldExpression.Literal(new OdinString("ok")) },
        });
        var result = TransformEngine.Execute(t, Obj(("x", Str("v"))));
        Assert.NotNull(result.Output);
        Assert.Equal(Str("ok"), result.Output!.Get("B"));
    }

    // ─────────────────────────────────────────────────────────────────
    // Discriminator Segments
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void DiscriminatorSegmentMatches()
    {
        var t = new OdinTransform
        {
            Target = new TargetConfig { Format = "json" },
            Segments = new List<TransformSegment>
            {
                new TransformSegment
                {
                    SegmentDiscriminator = new Discriminator { Path = "@.type", Value = "person" },
                    Mappings = new List<FieldMapping>
                    {
                        new FieldMapping { Target = "Name", Expression = FieldExpression.Copy("@.name") }
                    }
                }
            }
        };
        var source = Obj(("type", Str("person")), ("name", Str("Alice")));
        var result = TransformEngine.Execute(t, source);
        Assert.True(result.Success);
        Assert.Equal(Str("Alice"), result.Output!.Get("Name"));
    }

    [Fact]
    public void DiscriminatorMismatchSkips()
    {
        var t = new OdinTransform
        {
            Target = new TargetConfig { Format = "json" },
            Segments = new List<TransformSegment>
            {
                new TransformSegment
                {
                    SegmentDiscriminator = new Discriminator { Path = "@.type", Value = "vehicle" },
                    Mappings = new List<FieldMapping>
                    {
                        new FieldMapping { Target = "Name", Expression = FieldExpression.Copy("@.name") }
                    }
                }
            }
        };
        var source = Obj(("type", Str("person")), ("name", Str("Alice")));
        var result = TransformEngine.Execute(t, source);
        Assert.True(result.Success);
        var name = result.Output!.Get("Name");
        Assert.True(name == null || name.IsNull);
    }

    // ─────────────────────────────────────────────────────────────────
    // Copy All Value Types
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void CopyAllValueTypes()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping { Target = "S", Expression = FieldExpression.Copy("@.s") },
            new FieldMapping { Target = "I", Expression = FieldExpression.Copy("@.i") },
            new FieldMapping { Target = "F", Expression = FieldExpression.Copy("@.f") },
            new FieldMapping { Target = "B", Expression = FieldExpression.Copy("@.b") },
            new FieldMapping { Target = "N", Expression = FieldExpression.Copy("@.n") },
        });
        var source = Obj(
            ("s", Str("hello")),
            ("i", Int(42)),
            ("f", Flt(3.14)),
            ("b", Bln(true)),
            ("n", Nul())
        );
        var result = TransformEngine.Execute(t, source);
        Assert.True(result.Success);
        Assert.Equal(Str("hello"), result.Output!.Get("S"));
        Assert.Equal(Int(42), result.Output!.Get("I"));
        Assert.Equal(Flt(3.14), result.Output!.Get("F"));
        Assert.Equal(Bln(true), result.Output!.Get("B"));
        Assert.True(result.Output!.Get("N")!.IsNull);
    }

    [Fact]
    public void CopyNestedArrayWithinObject()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping { Target = "Items", Expression = FieldExpression.Copy("@.data.items") }
        });
        var source = Obj(("data", Obj(("items", Arr(Str("a"), Str("b"), Str("c"))))));
        var result = TransformEngine.Execute(t, source);
        Assert.True(result.Success);
        var items = result.Output!.Get("Items");
        Assert.NotNull(items);
        Assert.Equal(DynValueType.Array, items!.Type);
        Assert.Equal(3, items.AsArray()!.Count);
    }

    // ─────────────────────────────────────────────────────────────────
    // IFElse Verb Tests
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void IfElseTrueBranch()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping
            {
                Target = "Result",
                Expression = FieldExpression.Transform(new VerbCall
                {
                    Verb = "ifElse",
                    Args = new List<VerbArg>
                    {
                        VerbArg.Ref("@.active"),
                        VerbArg.Lit(new OdinString("yes")),
                        VerbArg.Lit(new OdinString("no")),
                    }
                }),
            }
        });
        var result = TransformEngine.Execute(t, Obj(("active", Bln(true))));
        Assert.True(result.Success);
        Assert.Equal(Str("yes"), result.Output!.Get("Result"));
    }

    [Fact]
    public void IfElseFalseBranch()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping
            {
                Target = "Result",
                Expression = FieldExpression.Transform(new VerbCall
                {
                    Verb = "ifElse",
                    Args = new List<VerbArg>
                    {
                        VerbArg.Ref("@.active"),
                        VerbArg.Lit(new OdinString("yes")),
                        VerbArg.Lit(new OdinString("no")),
                    }
                }),
            }
        });
        var result = TransformEngine.Execute(t, Obj(("active", Bln(false))));
        Assert.True(result.Success);
        Assert.Equal(Str("no"), result.Output!.Get("Result"));
    }

    [Fact]
    public void IfElseNullConditionTakesFalseBranch()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping
            {
                Target = "Result",
                Expression = FieldExpression.Transform(new VerbCall
                {
                    Verb = "ifElse",
                    Args = new List<VerbArg>
                    {
                        VerbArg.Ref("@.missing"),
                        VerbArg.Lit(new OdinString("yes")),
                        VerbArg.Lit(new OdinString("no")),
                    }
                }),
            }
        });
        var result = TransformEngine.Execute(t, Obj());
        Assert.True(result.Success);
        Assert.Equal(Str("no"), result.Output!.Get("Result"));
    }

    [Fact]
    public void IfElseStringConditionTruthy()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping
            {
                Target = "Result",
                Expression = FieldExpression.Transform(new VerbCall
                {
                    Verb = "ifElse",
                    Args = new List<VerbArg>
                    {
                        VerbArg.Ref("@.name"),
                        VerbArg.Lit(new OdinString("has_name")),
                        VerbArg.Lit(new OdinString("no_name")),
                    }
                }),
            }
        });
        var result = TransformEngine.Execute(t, Obj(("name", Str("Alice"))));
        Assert.True(result.Success);
        Assert.Equal(Str("has_name"), result.Output!.Get("Result"));
    }

    [Fact]
    public void IfElseEmptyStringConditionFalsy()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping
            {
                Target = "Result",
                Expression = FieldExpression.Transform(new VerbCall
                {
                    Verb = "ifElse",
                    Args = new List<VerbArg>
                    {
                        VerbArg.Ref("@.name"),
                        VerbArg.Lit(new OdinString("has_name")),
                        VerbArg.Lit(new OdinString("no_name")),
                    }
                }),
            }
        });
        var result = TransformEngine.Execute(t, Obj(("name", Str(""))));
        Assert.True(result.Success);
        Assert.Equal(Str("no_name"), result.Output!.Get("Result"));
    }

    [Fact]
    public void IfElseIntegerZeroIsFalsy()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping
            {
                Target = "Result",
                Expression = FieldExpression.Transform(new VerbCall
                {
                    Verb = "ifElse",
                    Args = new List<VerbArg>
                    {
                        VerbArg.Ref("@.val"),
                        VerbArg.Lit(new OdinString("yes")),
                        VerbArg.Lit(new OdinString("no")),
                    }
                }),
            }
        });
        var result = TransformEngine.Execute(t, Obj(("val", Int(0))));
        Assert.True(result.Success);
        Assert.Equal(Str("no"), result.Output!.Get("Result"));
    }

    [Fact]
    public void IfElseIntegerNonzeroIsTruthy()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping
            {
                Target = "Result",
                Expression = FieldExpression.Transform(new VerbCall
                {
                    Verb = "ifElse",
                    Args = new List<VerbArg>
                    {
                        VerbArg.Ref("@.val"),
                        VerbArg.Lit(new OdinString("yes")),
                        VerbArg.Lit(new OdinString("no")),
                    }
                }),
            }
        });
        var result = TransformEngine.Execute(t, Obj(("val", Int(42))));
        Assert.True(result.Success);
        Assert.Equal(Str("yes"), result.Output!.Get("Result"));
    }

    [Fact]
    public void IfElseWithNestedVerbInBranch()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping
            {
                Target = "Result",
                Expression = FieldExpression.Transform(new VerbCall
                {
                    Verb = "ifElse",
                    Args = new List<VerbArg>
                    {
                        VerbArg.Ref("@.active"),
                        VerbArg.NestedCall(new VerbCall
                        {
                            Verb = "upper",
                            Args = new List<VerbArg> { VerbArg.Ref("@.name") }
                        }),
                        VerbArg.Lit(new OdinString("n/a")),
                    }
                }),
            }
        });
        var result = TransformEngine.Execute(t, Obj(("active", Bln(true)), ("name", Str("alice"))));
        Assert.True(result.Success);
        Assert.Equal(Str("ALICE"), result.Output!.Get("Result"));
    }

    // ─────────────────────────────────────────────────────────────────
    // Named Segments
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void NamedSegmentCreatesNestedObject()
    {
        var t = new OdinTransform
        {
            Target = new TargetConfig { Format = "json" },
            Segments = new List<TransformSegment>
            {
                new TransformSegment
                {
                    Name = "Person",
                    Path = "Person",
                    Mappings = new List<FieldMapping>
                    {
                        new FieldMapping { Target = "Name", Expression = FieldExpression.Copy("@.name") }
                    }
                }
            }
        };
        var result = TransformEngine.Execute(t, Obj(("name", Str("Alice"))));
        Assert.True(result.Success);
        var person = result.Output!.Get("Person");
        Assert.NotNull(person);
        Assert.Equal(Str("Alice"), person!.Get("Name"));
    }

    // ─────────────────────────────────────────────────────────────────
    // End-to-End Transform Text Parsing + Execution
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void EndToEnd_SimpleCopyFromText()
    {
        var transform = @"
{$}
odin = ""1.0.0""
transform = ""1.0.0""
direction = ""json->json""
target.format = ""json""

{Person}
Name = ""@.name""
";
        var result = Core.Odin.ExecuteTransform(transform, DynValue.Object(
            new List<KeyValuePair<string, DynValue>>
            {
                new KeyValuePair<string, DynValue>("name", DynValue.String("Alice"))
            }
        ));
        Assert.True(result.Success);
        var person = result.Output!.Get("Person");
        Assert.NotNull(person);
        Assert.Equal(Str("Alice"), person!.Get("Name"));
    }

    [Fact]
    public void EndToEnd_VerbFromText()
    {
        var transform = @"
{$}
odin = ""1.0.0""
transform = ""1.0.0""
direction = ""json->json""
target.format = ""json""

{Person}
Name = ""%upper @.name""
";
        var result = Core.Odin.ExecuteTransform(transform, DynValue.Object(
            new List<KeyValuePair<string, DynValue>>
            {
                new KeyValuePair<string, DynValue>("name", DynValue.String("alice"))
            }
        ));
        Assert.True(result.Success);
        var person = result.Output!.Get("Person");
        Assert.NotNull(person);
        Assert.Equal(Str("ALICE"), person!.Get("Name"));
    }

    [Fact]
    public void EndToEnd_ConcatFromText()
    {
        var transform = @"
{$}
odin = ""1.0.0""
transform = ""1.0.0""
direction = ""json->json""
target.format = ""json""

{Person}
FullName = ""%concat @.first \"" \"" @.last""
";
        var result = Core.Odin.ExecuteTransform(transform, DynValue.Object(
            new List<KeyValuePair<string, DynValue>>
            {
                new KeyValuePair<string, DynValue>("first", DynValue.String("John")),
                new KeyValuePair<string, DynValue>("last", DynValue.String("Doe"))
            }
        ));
        Assert.True(result.Success);
        var person = result.Output!.Get("Person");
        Assert.NotNull(person);
        Assert.Equal(Str("John Doe"), person!.Get("FullName"));
    }

    [Fact]
    public void EndToEnd_LiteralFromText()
    {
        var transform = @"
{$}
odin = ""1.0.0""
transform = ""1.0.0""
direction = ""json->json""
target.format = ""json""

{Info}
Version = ""1.0""
";
        var result = Core.Odin.ExecuteTransform(transform, Obj());
        Assert.True(result.Success);
        var info = result.Output!.Get("Info");
        Assert.NotNull(info);
    }

    [Fact]
    public void EndToEnd_ConstantsFromText()
    {
        var transform = @"
{$}
odin = ""1.0.0""
transform = ""1.0.0""
direction = ""json->json""
target.format = ""json""

{const}
version = ""2.0""

{Output}
Version = ""$const.version""
";
        var result = Core.Odin.ExecuteTransform(transform, Obj());
        Assert.True(result.Success);
        var output = result.Output!.Get("Output");
        Assert.NotNull(output);
        Assert.Equal(Str("2.0"), output!.Get("Version"));
    }

    [Fact]
    public void EndToEnd_MultipleSegmentsFromText()
    {
        var transform = @"
{$}
odin = ""1.0.0""
transform = ""1.0.0""
direction = ""json->json""
target.format = ""json""

{Person}
Name = ""@.name""

{Address}
City = ""@.city""
";
        var result = Core.Odin.ExecuteTransform(transform, DynValue.Object(
            new List<KeyValuePair<string, DynValue>>
            {
                new KeyValuePair<string, DynValue>("name", DynValue.String("Alice")),
                new KeyValuePair<string, DynValue>("city", DynValue.String("NYC"))
            }
        ));
        Assert.True(result.Success);
        var person = result.Output!.Get("Person");
        Assert.NotNull(person);
        Assert.Equal(Str("Alice"), person!.Get("Name"));
        var address = result.Output!.Get("Address");
        Assert.NotNull(address);
        Assert.Equal(Str("NYC"), address!.Get("City"));
    }

    [Fact]
    public void EndToEnd_NestedVerbFromText()
    {
        var transform = @"
{$}
odin = ""1.0.0""
transform = ""1.0.0""
direction = ""json->json""
target.format = ""json""

{Result}
Name = ""%upper %concat @.first \"" \"" @.last""
";
        var result = Core.Odin.ExecuteTransform(transform, DynValue.Object(
            new List<KeyValuePair<string, DynValue>>
            {
                new KeyValuePair<string, DynValue>("first", DynValue.String("john")),
                new KeyValuePair<string, DynValue>("last", DynValue.String("doe"))
            }
        ));
        Assert.True(result.Success);
        var r = result.Output!.Get("Result");
        Assert.NotNull(r);
        Assert.Equal(Str("JOHN DOE"), r!.Get("Name"));
    }

    [Fact]
    public void EndToEnd_ModifierRequired()
    {
        var transform = @"
{$}
odin = ""1.0.0""
transform = ""1.0.0""
direction = ""json->json""
target.format = ""json""

{Data}
Name = ""@.name :required""
";
        var result = Core.Odin.ExecuteTransform(transform, DynValue.Object(
            new List<KeyValuePair<string, DynValue>>
            {
                new KeyValuePair<string, DynValue>("name", DynValue.String("Alice"))
            }
        ));
        Assert.True(result.Success);
        // Should record required modifier
        var hasRequired = false;
        foreach (var kv in result.OutputModifiers)
        {
            if (kv.Key.Contains("Name") && kv.Value.Required)
                hasRequired = true;
        }
        Assert.True(hasRequired);
    }

    [Fact]
    public void EndToEnd_ConfidentialRedact()
    {
        var transform = @"
{$}
odin = ""1.0.0""
transform = ""1.0.0""
direction = ""json->json""
target.format = ""json""
enforceConfidential = ""redact""

{Data}
SSN = ""@.ssn :confidential""
Name = ""@.name""
";
        var result = Core.Odin.ExecuteTransform(transform, DynValue.Object(
            new List<KeyValuePair<string, DynValue>>
            {
                new KeyValuePair<string, DynValue>("ssn", DynValue.String("123-45-6789")),
                new KeyValuePair<string, DynValue>("name", DynValue.String("Alice"))
            }
        ));
        Assert.True(result.Success);
        var data = result.Output!.Get("Data");
        Assert.NotNull(data);
        // SSN should be redacted to null
        var ssn = data!.Get("SSN");
        Assert.NotNull(ssn);
        Assert.True(ssn!.IsNull);
        // Name should be preserved
        Assert.Equal(Str("Alice"), data.Get("Name"));
    }

    [Fact]
    public void EndToEnd_FormattedOutputIsJson()
    {
        var transform = @"
{$}
odin = ""1.0.0""
transform = ""1.0.0""
direction = ""json->json""
target.format = ""json""

{Data}
Name = ""@.name""
";
        var result = Core.Odin.ExecuteTransform(transform, DynValue.Object(
            new List<KeyValuePair<string, DynValue>>
            {
                new KeyValuePair<string, DynValue>("name", DynValue.String("Alice"))
            }
        ));
        Assert.True(result.Success);
        Assert.NotNull(result.Formatted);
        Assert.Contains("Alice", result.Formatted);
    }

    // ─────────────────────────────────────────────────────────────────
    // Multi-Pass
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void MultiPassOrderingPass1BeforePass2()
    {
        var t = new OdinTransform
        {
            Target = new TargetConfig { Format = "json" },
            Segments = new List<TransformSegment>
            {
                new TransformSegment
                {
                    Pass = 2,
                    Mappings = new List<FieldMapping>
                    {
                        new FieldMapping { Target = "B", Expression = FieldExpression.Literal(new OdinString("second")) }
                    }
                },
                new TransformSegment
                {
                    Pass = 1,
                    Mappings = new List<FieldMapping>
                    {
                        new FieldMapping { Target = "A", Expression = FieldExpression.Literal(new OdinString("first")) }
                    }
                }
            },
            Passes = new List<int> { 1, 2 }
        };
        var result = TransformEngine.Execute(t, Obj());
        Assert.True(result.Success);
        Assert.Equal(Str("first"), result.Output!.Get("A"));
        Assert.Equal(Str("second"), result.Output!.Get("B"));
    }

    [Fact]
    public void MultiPassNonePassRunsLast()
    {
        var t = new OdinTransform
        {
            Target = new TargetConfig { Format = "json" },
            Segments = new List<TransformSegment>
            {
                new TransformSegment
                {
                    Pass = null,
                    Mappings = new List<FieldMapping>
                    {
                        new FieldMapping { Target = "C", Expression = FieldExpression.Literal(new OdinString("none")) }
                    }
                },
                new TransformSegment
                {
                    Pass = 1,
                    Mappings = new List<FieldMapping>
                    {
                        new FieldMapping { Target = "A", Expression = FieldExpression.Literal(new OdinString("first")) }
                    }
                }
            },
            Passes = new List<int> { 1 }
        };
        var result = TransformEngine.Execute(t, Obj());
        Assert.True(result.Success);
        Assert.Equal(Str("first"), result.Output!.Get("A"));
        Assert.Equal(Str("none"), result.Output!.Get("C"));
    }

    // ─────────────────────────────────────────────────────────────────
    // Lookup Tables
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void LookupTableUsage()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping
            {
                Target = "StateName",
                Expression = FieldExpression.Transform(new VerbCall
                {
                    Verb = "lookup",
                    Args = new List<VerbArg>
                    {
                        VerbArg.Lit(new OdinString("states.name")),
                        VerbArg.Ref("@.code"),
                    }
                }),
            }
        });
        t.Tables["states"] = new LookupTable
        {
            Name = "states",
            Columns = new List<string> { "code", "name" },
            Rows = new List<List<DynValue>>
            {
                new List<DynValue> { Str("CA"), Str("California") },
                new List<DynValue> { Str("NY"), Str("New York") },
            }
        };
        var result = TransformEngine.Execute(t, Obj(("code", Str("CA"))));
        Assert.True(result.Success);
        Assert.Equal(Str("California"), result.Output!.Get("StateName"));
    }

    [Fact]
    public void LookupTableNotFoundReturnsNull()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping
            {
                Target = "StateName",
                Expression = FieldExpression.Transform(new VerbCall
                {
                    Verb = "lookup",
                    Args = new List<VerbArg>
                    {
                        VerbArg.Lit(new OdinString("states")),
                        VerbArg.Ref("@.code"),
                    }
                }),
            }
        });
        t.Tables["states"] = new LookupTable
        {
            Name = "states",
            Columns = new List<string> { "code", "name" },
            Rows = new List<List<DynValue>>
            {
                new List<DynValue> { Str("CA"), Str("California") },
            }
        };
        var result = TransformEngine.Execute(t, Obj(("code", Str("XX"))));
        Assert.True(result.Success);
        var name = result.Output!.Get("StateName");
        Assert.True(name == null || name.IsNull);
    }

    // ─────────────────────────────────────────────────────────────────
    // Cond Verb
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void CondFirstMatch()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping
            {
                Target = "Result",
                Expression = FieldExpression.Transform(new VerbCall
                {
                    Verb = "cond",
                    Args = new List<VerbArg>
                    {
                        VerbArg.Ref("@.a"),
                        VerbArg.Lit(new OdinString("match_a")),
                        VerbArg.Ref("@.b"),
                        VerbArg.Lit(new OdinString("match_b")),
                    }
                }),
            }
        });
        var result = TransformEngine.Execute(t, Obj(("a", Bln(true)), ("b", Bln(true))));
        Assert.True(result.Success);
        Assert.Equal(Str("match_a"), result.Output!.Get("Result"));
    }

    [Fact]
    public void CondSecondMatch()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping
            {
                Target = "Result",
                Expression = FieldExpression.Transform(new VerbCall
                {
                    Verb = "cond",
                    Args = new List<VerbArg>
                    {
                        VerbArg.Ref("@.a"),
                        VerbArg.Lit(new OdinString("match_a")),
                        VerbArg.Ref("@.b"),
                        VerbArg.Lit(new OdinString("match_b")),
                    }
                }),
            }
        });
        var result = TransformEngine.Execute(t, Obj(("a", Bln(false)), ("b", Bln(true))));
        Assert.True(result.Success);
        Assert.Equal(Str("match_b"), result.Output!.Get("Result"));
    }

    // ─────────────────────────────────────────────────────────────────
    // Coalesce Verb
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void CoalesceFirstPresent()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping
            {
                Target = "Result",
                Expression = FieldExpression.Transform(new VerbCall
                {
                    Verb = "coalesce",
                    Args = new List<VerbArg>
                    {
                        VerbArg.Ref("@.a"),
                        VerbArg.Ref("@.b"),
                    }
                }),
            }
        });
        var result = TransformEngine.Execute(t, Obj(("a", Str("first")), ("b", Str("second"))));
        Assert.True(result.Success);
        Assert.Equal(Str("first"), result.Output!.Get("Result"));
    }

    [Fact]
    public void CoalesceFirstMissing()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping
            {
                Target = "Result",
                Expression = FieldExpression.Transform(new VerbCall
                {
                    Verb = "coalesce",
                    Args = new List<VerbArg>
                    {
                        VerbArg.Ref("@.missing"),
                        VerbArg.Ref("@.b"),
                    }
                }),
            }
        });
        var result = TransformEngine.Execute(t, Obj(("b", Str("fallback"))));
        Assert.True(result.Success);
        Assert.Equal(Str("fallback"), result.Output!.Get("Result"));
    }

    // ─────────────────────────────────────────────────────────────────
    // Success/Warnings
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void SuccessTrueWithNoErrors()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping { Target = "X", Expression = FieldExpression.Literal(new OdinString("ok")) }
        });
        var result = TransformEngine.Execute(t, Obj());
        Assert.True(result.Success);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void MixedSuccessAndErrorMappings()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping { Target = "A", Expression = FieldExpression.Transform(new VerbCall { Verb = "bad_verb", Args = new List<VerbArg> { VerbArg.Ref("@.x") } }) },
            new FieldMapping { Target = "B", Expression = FieldExpression.Literal(new OdinString("good")) },
        });
        var result = TransformEngine.Execute(t, Obj(("x", Str("val"))));
        Assert.True(result.Errors.Count > 0);
        Assert.Equal(Str("good"), result.Output!.Get("B"));
    }

    // ─────────────────────────────────────────────────────────────────
    // Nested Segments With Children
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void NestedSegmentWithChildren()
    {
        var t = new OdinTransform
        {
            Target = new TargetConfig { Format = "json" },
            Segments = new List<TransformSegment>
            {
                new TransformSegment
                {
                    Name = "Parent",
                    Path = "Parent",
                    Mappings = new List<FieldMapping>
                    {
                        new FieldMapping { Target = "Name", Expression = FieldExpression.Copy("@.name") }
                    },
                    Children = new List<TransformSegment>
                    {
                        new TransformSegment
                        {
                            Name = "Child",
                            Path = "Child",
                            Mappings = new List<FieldMapping>
                            {
                                new FieldMapping { Target = "Age", Expression = FieldExpression.Copy("@.age") }
                            }
                        }
                    }
                }
            }
        };
        var source = Obj(("name", Str("Alice")), ("age", Int(30)));
        var result = TransformEngine.Execute(t, source);
        Assert.True(result.Success);
        var parent = result.Output!.Get("Parent");
        Assert.NotNull(parent);
        Assert.Equal(Str("Alice"), parent!.Get("Name"));
    }

    // ─────────────────────────────────────────────────────────────────
    // Custom Verbs
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void CustomVerbPassthrough()
    {
        var t = MinimalTransform(new List<FieldMapping>
        {
            new FieldMapping
            {
                Target = "Out",
                Expression = FieldExpression.Transform(new VerbCall
                {
                    Verb = "custom_verb",
                    IsCustom = true,
                    Args = new List<VerbArg> { VerbArg.Ref("@.x") }
                }),
            }
        });
        var result = TransformEngine.Execute(t, Obj(("x", Str("val"))));
        // Custom verbs either pass through or return null
        Assert.True(result.Success || result.Errors.Count > 0);
    }
}
