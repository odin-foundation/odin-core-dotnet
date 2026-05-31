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
}
