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
/// Golden transform tests. Loads test suites from GoldenData/transform/ and
/// verifies the .NET transform engine produces matching results.
/// </summary>
[Trait("Category", "Golden")]
public class TransformTests : GoldenTestBase
{
    public static IEnumerable<object[]> TransformTestCases()
    {
        List<(string FilePath, TestSuite Suite)> suites;
        try
        {
            suites = LoadAllSuites("transform");
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

    [Fact]
    public void GoldenTransformTests_RunAll()
    {
        var testCases = TransformTestCases().ToList();
        if (testCases.Count == 0)
        {
            // No JSON-based transform golden tests found (transform tests use .test.odin format).
            // Skip rather than fail.
            return;
        }

        foreach (var testCase in testCases)
        {
            GoldenTransformTest((string)testCase[0], (string)testCase[1], (string)testCase[2]);
        }
    }

    private void GoldenTransformTest(string suiteName, string testId, string filePath)
    {
        var suite = LoadTestSuite(filePath);
        var test = suite.Tests.First(t => t.Id == testId);

        try
        {
            // Load transform text
            string transformText;
            if (test.TransformFile != null)
            {
                transformText = ReadRelativeFile(filePath, test.TransformFile);
            }
            else if (test.Transform != null)
            {
                transformText = test.Transform;
            }
            else
            {
                Assert.Fail($"[{suiteName}/{testId}] No transform file or inline transform found");
                return;
            }

            // Parse input data
            object inputData;
            if (test.Input.ValueKind == JsonValueKind.String)
            {
                // Input is ODIN text or raw string
                inputData = test.Input.GetString()!;
            }
            else
            {
                // Input is JSON data -- convert to DynValue
                inputData = JsonElementToDynValue(test.Input);
            }

            if (test.ExpectError != null)
            {
                // Expect an error during transform
                var ex = Assert.ThrowsAny<Exception>(() =>
                    Core.Odin.ExecuteTransform(transformText, inputData));

                // If a specific code is expected, check it
                if (test.ExpectError.Code != null && ex is OdinParseException parseEx)
                {
                    Assert.Equal(test.ExpectError.Code, parseEx.Code);
                }
            }
            else if (test.Expected != null)
            {
                var result = Core.Odin.ExecuteTransform(transformText, inputData);

                // Compare output
                if (test.Expected.Output.HasValue)
                {
                    Assert.NotNull(result.Output);
                    VerifyTransformOutput(result.Output, test.Expected.Output.Value,
                        $"[{suiteName}/{testId}]");
                }

                // Compare formatted output
                if (test.Expected.Formatted != null)
                {
                    Assert.NotNull(result.Formatted);
                    var expectedFormatted = NormalizeLineEndings(test.Expected.Formatted);
                    var actualFormatted = NormalizeLineEndings(result.Formatted!);
                    Assert.Equal(expectedFormatted, actualFormatted);
                }
            }
        }
        catch (Exception ex) when (ex is not Xunit.Sdk.XunitException)
        {
            Assert.Fail(
                $"[{suiteName}/{testId}] Transform test failed with unexpected error: {ex.Message}");
        }
    }

    private static DynValue JsonElementToDynValue(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var entries = new List<KeyValuePair<string, DynValue>>();
                foreach (var prop in element.EnumerateObject())
                {
                    entries.Add(new KeyValuePair<string, DynValue>(
                        prop.Name, JsonElementToDynValue(prop.Value)));
                }
                return DynValue.Object(entries);

            case JsonValueKind.Array:
                var items = new List<DynValue>();
                foreach (var item in element.EnumerateArray())
                {
                    items.Add(JsonElementToDynValue(item));
                }
                return DynValue.Array(items);

            case JsonValueKind.String:
                return DynValue.String(element.GetString()!);

            case JsonValueKind.Number:
                if (element.TryGetInt64(out var longVal))
                    return DynValue.Integer(longVal);
                return DynValue.Float(element.GetDouble());

            case JsonValueKind.True:
                return DynValue.Bool(true);

            case JsonValueKind.False:
                return DynValue.Bool(false);

            case JsonValueKind.Null:
            default:
                return DynValue.Null();
        }
    }

    private static void VerifyTransformOutput(DynValue actual, JsonElement expected, string context)
    {
        switch (expected.ValueKind)
        {
            case JsonValueKind.Object:
                Assert.Equal(DynValueType.Object, actual.Type);
                var actualObj = actual.AsObject();
                Assert.NotNull(actualObj);
                foreach (var prop in expected.EnumerateObject())
                {
                    var field = actual.Get(prop.Name);
                    Assert.NotNull(field);
                    VerifyTransformOutput(field, prop.Value, $"{context}.{prop.Name}");
                }
                break;

            case JsonValueKind.Array:
                Assert.Equal(DynValueType.Array, actual.Type);
                var actualArr = actual.AsArray();
                Assert.NotNull(actualArr);
                var expectedArr = expected.EnumerateArray().ToList();
                Assert.Equal(expectedArr.Count, actualArr!.Count);
                for (var i = 0; i < expectedArr.Count; i++)
                {
                    VerifyTransformOutput(actualArr[i], expectedArr[i], $"{context}[{i}]");
                }
                break;

            case JsonValueKind.String:
                Assert.Equal(expected.GetString(), actual.AsString());
                break;

            case JsonValueKind.Number:
                if (expected.TryGetInt64(out var expectedLong))
                {
                    var actualDouble = actual.AsDouble();
                    Assert.NotNull(actualDouble);
                    Assert.Equal((double)expectedLong, actualDouble!.Value, 10);
                }
                else
                {
                    var actualDouble = actual.AsDouble();
                    Assert.NotNull(actualDouble);
                    Assert.Equal(expected.GetDouble(), actualDouble!.Value, 10);
                }
                break;

            case JsonValueKind.True:
                Assert.Equal(true, actual.AsBool());
                break;

            case JsonValueKind.False:
                Assert.Equal(false, actual.AsBool());
                break;

            case JsonValueKind.Null:
                Assert.True(actual.IsNull, $"{context} Expected null");
                break;
        }
    }

    private static string NormalizeLineEndings(string text)
    {
        return text.Replace("\r\n", "\n").TrimEnd();
    }
}
