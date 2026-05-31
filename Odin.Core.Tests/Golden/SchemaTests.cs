using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Odin.Core;
using Odin.Core.Types;
using Xunit;

namespace Odin.Core.Tests.Golden;

/// <summary>
/// Golden schema tests. Loads test suites from GoldenData/schema/ and
/// verifies that the .NET schema parser produces matching results.
/// </summary>
[Trait("Category", "Golden")]
public class SchemaTests : GoldenTestBase
{
    public static IEnumerable<object[]> SchemaTestCases()
    {
        List<(string FilePath, TestSuite Suite)> suites;
        try
        {
            suites = LoadAllSuites("schema");
        }
        catch (DirectoryNotFoundException)
        {
            yield break;
        }

        foreach (var (filePath, suite) in suites)
        {
            foreach (var test in suite.Tests)
            {
                yield return new object[]
                {
                    suite.Suite ?? Path.GetFileNameWithoutExtension(filePath),
                    test.Id,
                    filePath,
                };
            }
        }
    }

    [Theory]
    [MemberData(nameof(SchemaTestCases))]
    public void GoldenSchemaTest(string suiteName, string testId, string filePath)
    {
        var suite = LoadTestSuite(filePath);
        var test = suite.Tests.First(t => t.Id == testId);

        try
        {
            var inputText = GetInputString(test);

            if (test.ExpectError != null)
            {
                // Schema parsing should fail
                var ex = Assert.ThrowsAny<Exception>(() => Core.Odin.ParseSchema(inputText));

                if (test.ExpectError.Code != null && ex is OdinParseException parseEx)
                {
                    Assert.Equal(test.ExpectError.Code, parseEx.Code);
                }
            }
            else
            {
                // Schema should parse without error
                var schema = Core.Odin.ParseSchema(inputText);
                Assert.NotNull(schema);

                // Structural cases assert that expected type/root field keys exist.
                if (test.Structural && test.ExpectedRaw.HasValue)
                    AssertStructure(suiteName, testId, schema, test.ExpectedRaw.Value);

                // Value-level assertions on parsed fields/types.
                if (test.Assert.HasValue)
                    AssertValues(suiteName, testId, schema, test.Assert.Value);
            }
        }
        catch (Exception ex) when (ex is not Xunit.Sdk.XunitException)
        {
            Assert.Fail(
                $"[{suiteName}/{testId}] Schema test failed with unexpected error: {ex.Message}");
        }
    }

    private static void AssertStructure(
        string suiteName, string testId, OdinSchemaDefinition schema, JsonElement expected)
    {
        if (expected.TryGetProperty("types", out var types) && types.ValueKind == JsonValueKind.Object)
        {
            foreach (var typeProp in types.EnumerateObject())
            {
                Assert.True(schema.Types.ContainsKey(typeProp.Name),
                    $"[{suiteName}/{testId}] expected type '{typeProp.Name}' not found");
                var fieldNames = schema.Types[typeProp.Name].SchemaFields.Select(f => f.Name).ToHashSet();
                if (typeProp.Value.TryGetProperty("fields", out var typeFields)
                    && typeFields.ValueKind == JsonValueKind.Object)
                {
                    foreach (var fieldProp in typeFields.EnumerateObject())
                        Assert.True(fieldNames.Contains(fieldProp.Name),
                            $"[{suiteName}/{testId}] type '{typeProp.Name}' missing field '{fieldProp.Name}'");
                }
            }
        }

        if (expected.TryGetProperty("fields", out var fields) && fields.ValueKind == JsonValueKind.Object)
        {
            foreach (var fieldProp in fields.EnumerateObject())
                Assert.True(schema.Fields.ContainsKey(fieldProp.Name),
                    $"[{suiteName}/{testId}] expected root field '{fieldProp.Name}' not found");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Value-level assertions (constraint values, unions, defaults, flags, conditionals)
    // ─────────────────────────────────────────────────────────────────────────────

    private static void AssertValues(
        string suiteName, string testId, OdinSchemaDefinition schema, JsonElement assert)
    {
        if (assert.TryGetProperty("fields", out var fields) && fields.ValueKind == JsonValueKind.Object)
        {
            foreach (var fieldProp in fields.EnumerateObject())
            {
                Assert.True(schema.Fields.TryGetValue(fieldProp.Name, out var field),
                    $"[{suiteName}/{testId}] field '{fieldProp.Name}' not found");
                AssertField(suiteName, testId, fieldProp.Name, field!, fieldProp.Value);
            }
        }

        if (assert.TryGetProperty("types", out var types) && types.ValueKind == JsonValueKind.Object)
        {
            foreach (var typeProp in types.EnumerateObject())
            {
                Assert.True(schema.Types.TryGetValue(typeProp.Name, out var type),
                    $"[{suiteName}/{testId}] type '{typeProp.Name}' not found");
                if (typeProp.Value.TryGetProperty("fields", out var tFields)
                    && tFields.ValueKind == JsonValueKind.Object)
                {
                    foreach (var fieldProp in tFields.EnumerateObject())
                    {
                        var f = type!.SchemaFields.Find(x => x.Name == fieldProp.Name);
                        Assert.True(f != null,
                            $"[{suiteName}/{testId}] type '{typeProp.Name}' field '{fieldProp.Name}' not found");
                        AssertField(suiteName, testId,
                            $"{typeProp.Name}.{fieldProp.Name}", f!, fieldProp.Value);
                    }
                }
            }
        }
    }

    private static void AssertField(
        string suiteName, string testId, string label, SchemaField field, JsonElement a)
    {
        var pfx = $"[{suiteName}/{testId}] field '{label}'";

        if (a.TryGetProperty("typeKind", out var tk))
            Assert.Equal(tk.GetString(), TypeKind(field.FieldType));

        if (a.TryGetProperty("typeRefName", out var trn))
        {
            Assert.True(field.FieldType is TypeRefFieldType, $"{pfx} should be a typeRef");
            Assert.Equal(trn.GetString(), ((TypeRefFieldType)field.FieldType).Name);
        }

        if (a.TryGetProperty("required", out var req))
            Assert.Equal(req.GetBoolean(), field.Required);
        if (a.TryGetProperty("nullable", out var nul))
            Assert.Equal(nul.GetBoolean(), field.Nullable);
        if (a.TryGetProperty("immutable", out var imm))
            Assert.Equal(imm.GetBoolean(), field.Immutable);
        if (a.TryGetProperty("computed", out var comp))
            Assert.Equal(comp.GetBoolean(), field.Computed);
        if (a.TryGetProperty("deprecated", out var dep))
            Assert.Equal(dep.GetBoolean(), field.Deprecated);

        if (a.TryGetProperty("union", out var union) && union.ValueKind == JsonValueKind.Array)
        {
            Assert.True(field.FieldType is UnionFieldType, $"{pfx} should be a union");
            var kinds = new List<string>();
            foreach (var m in ((UnionFieldType)field.FieldType).Types)
                kinds.Add(TypeKind(m));
            kinds.Sort();
            var expected = new List<string>();
            foreach (var e in union.EnumerateArray()) expected.Add(e.GetString()!);
            expected.Sort();
            Assert.Equal(expected, kinds);
        }

        if (a.TryGetProperty("default", out var def) && def.ValueKind == JsonValueKind.Object)
        {
            Assert.True(field.TypedDefault != null, $"{pfx} should have a default value");
            var td = field.TypedDefault!;
            if (def.TryGetProperty("type", out var dt))
                Assert.Equal(dt.GetString(), td.Type);
            if (def.TryGetProperty("value", out var dv))
            {
                if (dv.ValueKind == JsonValueKind.True || dv.ValueKind == JsonValueKind.False)
                    Assert.Equal(dv.GetBoolean(), td.Bool);
                else if (dv.ValueKind == JsonValueKind.Number)
                    Assert.Equal(dv.GetDouble(), td.Number);
                else
                    Assert.Equal(dv.GetString(), td.Text);
            }
        }

        if (a.TryGetProperty("constraints", out var cons) && cons.ValueKind == JsonValueKind.Array)
        {
            foreach (var ec in cons.EnumerateArray())
            {
                bool found = false;
                foreach (var c in field.Constraints)
                {
                    if (ConstraintMatches(c, ec)) { found = true; break; }
                }
                Assert.True(found, $"{pfx} should have constraint {ec.GetRawText()}");
            }
        }

        if (a.TryGetProperty("conditionals", out var conds) && conds.ValueKind == JsonValueKind.Array)
        {
            foreach (var ecd in conds.EnumerateArray())
            {
                bool found = false;
                foreach (var c in field.Conditionals)
                {
                    if (ConditionalMatches(c, ecd)) { found = true; break; }
                }
                Assert.True(found, $"{pfx} should have conditional {ecd.GetRawText()}");
            }
        }
    }

    private static string TypeKind(SchemaFieldType t) => t switch
    {
        StringFieldType => "string",
        BooleanFieldType => "boolean",
        NullFieldType => "null",
        IntegerFieldType => "integer",
        NumberFieldType => "number",
        DecimalFieldType => "decimal",
        CurrencyFieldType => "currency",
        PercentFieldType => "percent",
        DateFieldType => "date",
        TimestampFieldType => "timestamp",
        TimeFieldType => "time",
        DurationFieldType => "duration",
        EnumFieldType => "enum",
        UnionFieldType => "union",
        ReferenceFieldType => "reference",
        BinaryFieldType => "binary",
        TypeRefFieldType => "typeRef",
        _ => "unknown",
    };

    private static bool ConstraintMatches(SchemaConstraint c, JsonElement e)
    {
        if (!e.TryGetProperty("kind", out var kindEl)) return false;
        var kind = kindEl.GetString();
        switch (kind)
        {
            case "bounds":
                if (c is not BoundsConstraint b) return false;
                if (e.TryGetProperty("min", out var min) && !BoundEquals(b.Min, min)) return false;
                if (e.TryGetProperty("max", out var max) && !BoundEquals(b.Max, max)) return false;
                return true;
            case "pattern":
                if (c is not PatternConstraint p) return false;
                return !e.TryGetProperty("pattern", out var pat) || pat.GetString() == p.PatternValue;
            case "unique":
                return c is UniqueConstraint;
            case "format":
                if (c is not FormatConstraint f) return false;
                return !e.TryGetProperty("format", out var fn) || fn.GetString() == f.FormatName;
            case "enum":
                return c is EnumConstraint;
            default:
                return false;
        }
    }

    private static bool BoundEquals(string? actual, JsonElement expected)
    {
        if (actual == null) return false;
        if (expected.ValueKind == JsonValueKind.Number)
            return double.TryParse(actual, System.Globalization.NumberStyles.Float,
                       System.Globalization.CultureInfo.InvariantCulture, out var n)
                   && n == expected.GetDouble();
        return actual == expected.GetString();
    }

    private static bool ConditionalMatches(SchemaConditional c, JsonElement e)
    {
        if (e.TryGetProperty("field", out var f) && f.GetString() != c.Field) return false;
        if (e.TryGetProperty("operator", out var op) && op.GetString() != OperatorString(c.Operator)) return false;
        if (e.TryGetProperty("value", out var v))
        {
            switch (v.ValueKind)
            {
                case JsonValueKind.True:
                case JsonValueKind.False:
                    if (c.CondValue.AsBool() != v.GetBoolean()) return false;
                    break;
                case JsonValueKind.Number:
                    if (c.CondValue.AsNumber() != v.GetDouble()) return false;
                    break;
                default:
                    if (c.CondValue.AsString() != v.GetString()) return false;
                    break;
            }
        }
        return true;
    }

    private static string OperatorString(ConditionalOperator op) => op switch
    {
        ConditionalOperator.Eq => "=",
        ConditionalOperator.NotEq => "!=",
        ConditionalOperator.Gt => ">",
        ConditionalOperator.Lt => "<",
        ConditionalOperator.Gte => ">=",
        ConditionalOperator.Lte => "<=",
        _ => "=",
    };
}
