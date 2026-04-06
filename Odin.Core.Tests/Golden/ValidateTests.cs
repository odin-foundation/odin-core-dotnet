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
/// Golden validation tests. Loads test suites from GoldenData/validate/ and
/// verifies that the .NET validator produces matching results.
/// </summary>
[Trait("Category", "Golden")]
public class ValidateTests : GoldenTestBase
{
    public static IEnumerable<object[]> ValidateTestCases()
    {
        List<(string FilePath, TestSuite Suite)> suites;
        try
        {
            suites = LoadAllSuites("validate");
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
    [MemberData(nameof(ValidateTestCases))]
    public void GoldenValidateTest(string suiteName, string testId, string filePath)
    {
        var suite = LoadTestSuite(filePath);
        var test = suite.Tests.First(t => t.Id == testId);

        try
        {
            // Load the input document
            var inputText = GetInputString(test);
            var doc = Core.Odin.Parse(inputText);

            // Load the schema
            string schemaText;
            if (test.SchemaFile != null)
            {
                schemaText = ReadRelativeFile(filePath, test.SchemaFile);
            }
            else if (test.Schema != null)
            {
                schemaText = test.Schema;
            }
            else
            {
                Assert.Fail($"[{suiteName}/{testId}] No schema file or inline schema found");
                return;
            }

            var schema = Core.Odin.ParseSchema(schemaText);
            var validateOptions = new ValidateOptions();
            if (test.Options?.Strict == true)
                validateOptions = new ValidateOptions { Strict = true };
            var result = Core.Odin.Validate(doc, schema, validateOptions);

            if (test.Expected != null)
            {
                if (test.Expected.Valid.HasValue)
                {
                    Assert.Equal(test.Expected.Valid.Value, result.IsValid);
                }

                if (test.Expected.Errors != null)
                {
                    foreach (var expectedError in test.Expected.Errors)
                    {
                        if (expectedError.Code != null)
                        {
                            Assert.Contains(result.Errors,
                                e => e.Code == expectedError.Code);
                        }

                        if (expectedError.Path != null)
                        {
                            Assert.Contains(result.Errors,
                                e => e.Path == expectedError.Path);
                        }
                    }
                }
            }

            if (test.ExpectError != null)
            {
                Assert.False(result.IsValid,
                    $"[{suiteName}/{testId}] Expected validation failure but got valid");
                if (test.ExpectError.Code != null)
                {
                    Assert.Contains(result.Errors,
                        e => e.Code == test.ExpectError.Code);
                }
            }
        }
        catch (Exception ex) when (ex is not Xunit.Sdk.XunitException)
        {
            Assert.Fail(
                $"[{suiteName}/{testId}] Validation test failed with unexpected error: {ex.Message}");
        }
    }
}
