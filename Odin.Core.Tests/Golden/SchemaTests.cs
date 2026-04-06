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

                // Additional structural verification could be added here
                // based on what the expected object contains
            }
        }
        catch (Exception ex) when (ex is not Xunit.Sdk.XunitException)
        {
            Assert.Fail(
                $"[{suiteName}/{testId}] Schema test failed with unexpected error: {ex.Message}");
        }
    }
}
