using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using Odin.Core;
using Odin.Core.Types;
using Xunit;

namespace Odin.Core.Tests.Golden;

/// <summary>
/// Golden parse tests. Loads all test suites from GoldenData/parse/ and
/// verifies that the .NET parser produces matching results.
/// </summary>
[Trait("Category", "Golden")]
public class ParseTests : GoldenTestBase
{
    /// <summary>
    /// Provides test data from all parse golden test suites.
    /// Each entry is: [suiteName, testId, testDescription, filePath].
    /// </summary>
    public static IEnumerable<object[]> ParseTestCases()
    {
        List<(string FilePath, TestSuite Suite)> suites;
        try
        {
            suites = LoadAllSuites("parse");
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
    [MemberData(nameof(ParseTestCases))]
    public void GoldenParseTest(string suiteName, string testId, string filePath)
    {
        var suite = LoadTestSuite(filePath);
        var test = suite.Tests.First(t => t.Id == testId);

        if (test.ExpectError != null)
        {
            RunExpectErrorTest(suiteName, test);
        }
        else if (test.Expected != null)
        {
            RunExpectSuccessTest(suiteName, test);
        }
        else
        {
            // No expected or expectError — skip
            Assert.Fail($"[{suiteName}/{testId}] Test has neither expected nor expectError");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Error tests
    // ─────────────────────────────────────────────────────────────────────────

    private static void RunExpectErrorTest(string suiteName, TestCase test)
    {
        var input = GetInputString(test);
        var ex = Assert.Throws<OdinParseException>(() => Core.Odin.Parse(input));

        Assert.Equal(
            test.ExpectError!.Code,
            ex.Code);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Success tests
    // ─────────────────────────────────────────────────────────────────────────

    private static void RunExpectSuccessTest(string suiteName, TestCase test)
    {
        var expected = test.Expected!;
        var input = GetInputString(test);

        // Multi-document test
        if (expected.Documents != null)
        {
            RunMultiDocumentTest(suiteName, test, expected);
            return;
        }

        // Single document test
        var doc = Core.Odin.Parse(input);

        // Check directives if expected
        if (expected.Directives != null)
        {
            VerifyDirectives(doc, expected.Directives);
        }

        // Check assignments
        if (expected.Assignments != null)
        {
            VerifyAssignments(doc.Assignments, expected.Assignments, $"[{suiteName}/{test.Id}]");
        }

        // Check top-level modifiers map (path -> {required, confidential, deprecated})
        if (expected.Modifiers != null)
        {
            VerifyTopLevelModifiers(doc, expected.Modifiers, $"[{suiteName}/{test.Id}]");
        }
    }

    private static void RunMultiDocumentTest(string suiteName, TestCase test, Expected expected)
    {
        var input = GetInputString(test);
        var docs = Core.Odin.ParseDocuments(input);

        Assert.Equal(expected.Documents!.Count, docs.Count);

        for (var i = 0; i < expected.Documents.Count; i++)
        {
            var expectedDoc = expected.Documents[i];
            var actualDoc = docs[i];

            if (expectedDoc.Assignments != null)
            {
                VerifyAssignments(
                    actualDoc.Assignments,
                    expectedDoc.Assignments,
                    $"[{suiteName}/{test.Id}] doc[{i}]");
            }
        }

        // Also check directives at the top level
        if (expected.Directives != null)
        {
            // Directives are typically on the first document
            if (docs.Count > 0)
            {
                VerifyDirectives(docs[0], expected.Directives);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Assignment verification
    // ─────────────────────────────────────────────────────────────────────────

    private static void VerifyAssignments(
        OrderedMap<string, OdinValue> actual,
        Dictionary<string, ExpectedValue> expected,
        string context)
    {
        foreach (var (path, expectedVal) in expected)
        {
            Assert.True(
                actual.ContainsKey(path),
                $"{context} Missing assignment: '{path}'. Available keys: {string.Join(", ", actual.Keys)}");

            var actualVal = actual[path];
            VerifyValue(actualVal, expectedVal, $"{context} path='{path}'");
        }
    }

    private static void VerifyValue(OdinValue actual, ExpectedValue expected, string context)
    {
        // Verify type
        var expectedType = expected.Type.ToLowerInvariant();
        AssertValueType(actual, expectedType, context);

        // Verify value payload
        switch (expectedType)
        {
            case "string":
                VerifyString(actual, expected, context);
                break;
            case "integer":
                VerifyInteger(actual, expected, context);
                break;
            case "number":
                VerifyNumber(actual, expected, context);
                break;
            case "currency":
                VerifyCurrency(actual, expected, context);
                break;
            case "percent":
                VerifyPercent(actual, expected, context);
                break;
            case "boolean":
                VerifyBoolean(actual, expected, context);
                break;
            case "null":
                Assert.IsType<OdinNull>(actual);
                break;
            case "date":
                VerifyDate(actual, expected, context);
                break;
            case "timestamp":
                VerifyTimestamp(actual, expected, context);
                break;
            case "time":
                VerifyTime(actual, expected, context);
                break;
            case "duration":
                VerifyDuration(actual, expected, context);
                break;
            case "reference":
                VerifyReference(actual, expected, context);
                break;
            case "binary":
                VerifyBinary(actual, expected, context);
                break;
            case "array":
                VerifyArray(actual, expected, context);
                break;
            case "object":
                // Simplified: just verify it parsed as an object
                Assert.IsType<OdinObject>(actual);
                break;
            case "verb":
                // Simplified: just verify it parsed as a verb
                Assert.IsType<OdinVerb>(actual);
                break;
        }

        // Verify modifiers
        if (expected.Modifiers != null && expected.Modifiers.Count > 0)
        {
            VerifyModifiers(actual, expected.Modifiers, context);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Type assertion
    // ─────────────────────────────────────────────────────────────────────────

    private static void AssertValueType(OdinValue actual, string expectedType, string context)
    {
        var actualType = actual.Type switch
        {
            OdinValueType.String => "string",
            OdinValueType.Integer => "integer",
            OdinValueType.Number => "number",
            OdinValueType.Currency => "currency",
            OdinValueType.Percent => "percent",
            OdinValueType.Boolean => "boolean",
            OdinValueType.Null => "null",
            OdinValueType.Date => "date",
            OdinValueType.Timestamp => "timestamp",
            OdinValueType.Time => "time",
            OdinValueType.Duration => "duration",
            OdinValueType.Reference => "reference",
            OdinValueType.Binary => "binary",
            OdinValueType.Array => "array",
            OdinValueType.Object => "object",
            OdinValueType.Verb => "verb",
            _ => actual.Type.ToString().ToLowerInvariant(),
        };

        Assert.Equal(expectedType, actualType);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Value verification by type
    // ─────────────────────────────────────────────────────────────────────────

    private static void VerifyString(OdinValue actual, ExpectedValue expected, string context)
    {
        var str = Assert.IsType<OdinString>(actual);
        if (expected.Value.HasValue)
        {
            var expectedStr = expected.Value.Value.GetString();
            Assert.Equal(expectedStr, str.Value);
        }
    }

    private static void VerifyInteger(OdinValue actual, ExpectedValue expected, string context)
    {
        var integer = Assert.IsType<OdinInteger>(actual);
        if (expected.Value.HasValue)
        {
            var expectedVal = expected.Value.Value.GetInt64();
            Assert.Equal(expectedVal, integer.Value);
        }
    }

    private static void VerifyNumber(OdinValue actual, ExpectedValue expected, string context)
    {
        var number = Assert.IsType<OdinNumber>(actual);
        if (expected.Value.HasValue)
        {
            var expectedVal = expected.Value.Value.GetDouble();
            Assert.Equal(expectedVal, number.Value, 10);
        }
        if (expected.DecimalPlaces.HasValue && number.DecimalPlaces.HasValue)
        {
            Assert.Equal(expected.DecimalPlaces.Value, number.DecimalPlaces.Value);
        }
    }

    private static void VerifyCurrency(OdinValue actual, ExpectedValue expected, string context)
    {
        var currency = Assert.IsType<OdinCurrency>(actual);
        if (expected.Value.HasValue)
        {
            var expectedVal = expected.Value.Value.GetDouble();
            Assert.Equal(expectedVal, currency.Value);
        }
        if (expected.DecimalPlaces.HasValue)
        {
            Assert.Equal(expected.DecimalPlaces.Value, (int)currency.DecimalPlaces);
        }
        if (expected.CurrencyCode != null)
        {
            Assert.Equal(expected.CurrencyCode, currency.CurrencyCode);
        }
    }

    private static void VerifyPercent(OdinValue actual, ExpectedValue expected, string context)
    {
        var pct = Assert.IsType<OdinPercent>(actual);
        if (expected.Value.HasValue)
        {
            var expectedVal = expected.Value.Value.GetDouble();
            Assert.Equal(expectedVal, pct.Value, 10);
        }
    }

    private static void VerifyBoolean(OdinValue actual, ExpectedValue expected, string context)
    {
        var boolean = Assert.IsType<OdinBoolean>(actual);
        if (expected.Value.HasValue)
        {
            var expectedVal = expected.Value.Value.GetBoolean();
            Assert.Equal(expectedVal, boolean.Value);
        }
    }

    private static void VerifyDate(OdinValue actual, ExpectedValue expected, string context)
    {
        var date = Assert.IsType<OdinDate>(actual);
        if (expected.Raw != null)
        {
            Assert.Equal(expected.Raw, date.Raw);
        }
        if (expected.Value.HasValue && expected.Value.Value.ValueKind == JsonValueKind.String)
        {
            Assert.Equal(expected.Value.Value.GetString(), date.Raw);
        }
    }

    private static void VerifyTimestamp(OdinValue actual, ExpectedValue expected, string context)
    {
        var ts = Assert.IsType<OdinTimestamp>(actual);
        if (expected.Raw != null)
        {
            Assert.Equal(expected.Raw, ts.Raw);
        }
        if (expected.Value.HasValue && expected.Value.Value.ValueKind == JsonValueKind.String)
        {
            Assert.Equal(expected.Value.Value.GetString(), ts.Raw);
        }
    }

    private static void VerifyTime(OdinValue actual, ExpectedValue expected, string context)
    {
        var time = Assert.IsType<OdinTime>(actual);
        if (expected.Value.HasValue && expected.Value.Value.ValueKind == JsonValueKind.String)
        {
            Assert.Equal(expected.Value.Value.GetString(), time.Value);
        }
    }

    private static void VerifyDuration(OdinValue actual, ExpectedValue expected, string context)
    {
        var duration = Assert.IsType<OdinDuration>(actual);
        if (expected.Value.HasValue && expected.Value.Value.ValueKind == JsonValueKind.String)
        {
            Assert.Equal(expected.Value.Value.GetString(), duration.Value);
        }
    }

    private static void VerifyReference(OdinValue actual, ExpectedValue expected, string context)
    {
        var reference = Assert.IsType<OdinReference>(actual);
        if (expected.Path != null)
        {
            Assert.Equal(expected.Path, reference.Path);
        }
    }

    private static void VerifyBinary(OdinValue actual, ExpectedValue expected, string context)
    {
        var binary = Assert.IsType<OdinBinary>(actual);
        if (expected.Base64 != null)
        {
            var actualBase64 = Convert.ToBase64String(binary.Data);
            Assert.Equal(expected.Base64, actualBase64);
        }
        if (expected.Algorithm != null)
        {
            Assert.Equal(expected.Algorithm, binary.Algorithm);
        }
    }

    private static void VerifyArray(OdinValue actual, ExpectedValue expected, string context)
    {
        var array = Assert.IsType<OdinArray>(actual);

        if (expected.Items != null)
        {
            Assert.Equal(expected.Items.Count, array.Items.Count);

            for (var i = 0; i < expected.Items.Count; i++)
            {
                var expectedItem = expected.Items[i];
                var actualItem = array.Items[i];

                // Array items can be value items or record items
                var actualValue = actualItem.AsValue();
                if (actualValue != null)
                {
                    VerifyValue(actualValue, expectedItem, $"{context} array[{i}]");
                }
                else
                {
                    // Record item — check fields if expected
                    var record = actualItem.AsRecord();
                    if (record != null && expectedItem.Fields != null)
                    {
                        foreach (var (key, expectedField) in expectedItem.Fields)
                        {
                            var field = record.FirstOrDefault(kvp => kvp.Key == key);
                            Assert.NotEqual(default, field);
                            VerifyValue(field.Value, expectedField, $"{context} array[{i}].{key}");
                        }
                    }
                }
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Modifier verification
    // ─────────────────────────────────────────────────────────────────────────

    private static void VerifyModifiers(OdinValue actual, List<string> expectedModifiers, string context)
    {
        Assert.NotNull(actual.Modifiers);

        foreach (var mod in expectedModifiers)
        {
            switch (mod.ToLowerInvariant())
            {
                case "required":
                case "critical":
                    Assert.True(actual.Modifiers!.Required,
                        $"{context} Expected 'required' modifier");
                    break;
                case "confidential":
                case "redacted":
                    Assert.True(actual.Modifiers!.Confidential,
                        $"{context} Expected 'confidential' modifier");
                    break;
                case "deprecated":
                    Assert.True(actual.Modifiers!.Deprecated,
                        $"{context} Expected 'deprecated' modifier");
                    break;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Top-level modifier verification (expected.modifiers map)
    // ─────────────────────────────────────────────────────────────────────────

    private static void VerifyTopLevelModifiers(
        OdinDocument doc,
        Dictionary<string, ExpectedModifiers> expectedModifiers,
        string context)
    {
        foreach (var (path, expectedMods) in expectedModifiers)
        {
            Assert.True(
                doc.Assignments.ContainsKey(path),
                $"{context} Modifier path '{path}' not found in assignments");

            var value = doc.Assignments[path];
            var mods = value.Modifiers;

            if (expectedMods.Required == true)
            {
                Assert.NotNull(mods);
                Assert.True(mods!.Required,
                    $"{context} path='{path}' Expected 'required' modifier");
            }

            if (expectedMods.Confidential == true)
            {
                Assert.NotNull(mods);
                Assert.True(mods!.Confidential,
                    $"{context} path='{path}' Expected 'confidential' modifier");
            }

            if (expectedMods.Deprecated == true)
            {
                Assert.NotNull(mods);
                Assert.True(mods!.Deprecated,
                    $"{context} path='{path}' Expected 'deprecated' modifier");
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Directive verification
    // ─────────────────────────────────────────────────────────────────────────

    private static void VerifyDirectives(OdinDocument doc, List<ExpectedDirective> expectedDirectives)
    {
        foreach (var directive in expectedDirectives)
        {
            switch (directive.Type.ToLowerInvariant())
            {
                case "import":
                    var import = doc.Imports.FirstOrDefault(i => i.Path == directive.Path);
                    Assert.NotNull(import);
                    if (directive.Alias.HasValue && directive.Alias.Value.ValueKind == JsonValueKind.String)
                    {
                        Assert.Equal(directive.Alias.Value.GetString(), import.Alias);
                    }
                    break;

                case "schema":
                    var schema = doc.Schemas.FirstOrDefault(s => s.Url == directive.Url);
                    Assert.NotNull(schema);
                    break;

                case "if":
                    var cond = doc.Conditionals.FirstOrDefault(c => c.Condition == directive.Condition);
                    Assert.NotNull(cond);
                    break;
            }
        }
    }
}
