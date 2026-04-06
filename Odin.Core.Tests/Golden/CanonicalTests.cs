using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Odin.Core;
using Odin.Core.Types;
using Xunit;

namespace Odin.Core.Tests.Golden;

/// <summary>
/// Golden canonical tests. Loads test suites from GoldenData/canonical/ and
/// verifies that the .NET canonicalizer produces matching results.
///
/// Supports two expected formats:
///   - string: compare decoded canonical text
///   - object { hex, sha256, byteLength }: compare raw binary output
/// </summary>
[Trait("Category", "Golden")]
public class CanonicalTests : GoldenTestBase
{
    public static IEnumerable<object[]> CanonicalTestCases()
    {
        List<(string FilePath, TestSuite Suite)> suites;
        try
        {
            suites = LoadAllSuites("canonical");
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
    [MemberData(nameof(CanonicalTestCases))]
    public void GoldenCanonicalTest(string suiteName, string testId, string filePath)
    {
        var suite = LoadTestSuite(filePath);
        var test = suite.Tests.First(t => t.Id == testId);

        try
        {
            var inputText = GetInputString(test);
            var doc = string.IsNullOrEmpty(inputText)
                ? Core.Odin.Empty()
                : Core.Odin.Parse(inputText);
            var canonical = Core.Odin.Canonicalize(doc);

            // Check if expected is a binary object (has hex/sha256/byteLength)
            var expectedBinary = test.Expected;
            if (expectedBinary?.Hex != null)
            {
                // Binary comparison path
                Assert.Equal(expectedBinary.ByteLength, canonical.Length);

                var actualHex = Convert.ToHexString(canonical).ToLowerInvariant();
                Assert.Equal(expectedBinary.Hex, actualHex);

                var actualSha256 = Convert.ToHexString(
                    SHA256.HashData(canonical)).ToLowerInvariant();
                Assert.Equal(expectedBinary.Sha256, actualSha256);
            }
            else
            {
                // Text comparison path (existing all-types.json format)
                var canonicalText = Encoding.UTF8.GetString(canonical);
                var expectedCanonical = expectedBinary?.Canonical ?? test.ExpectedString;

                if (expectedCanonical != null)
                {
                    var expected = NormalizeLineEndings(expectedCanonical);
                    var actual = NormalizeLineEndings(canonicalText);
                    Assert.Equal(expected, actual);

                    // Verify that canonical form is deterministic
                    // by parsing the canonical output and re-canonicalizing
                    var reparsed = Core.Odin.Parse(canonicalText);
                    var recanonical = Core.Odin.Canonicalize(reparsed);
                    Assert.Equal(canonical, recanonical);
                }
                else if (test.Expected != null)
                {
                    // Some tests may only verify determinism without expected text
                    var reparsed = Core.Odin.Parse(canonicalText);
                    var recanonical = Core.Odin.Canonicalize(reparsed);
                    Assert.Equal(canonical, recanonical);
                }
            }
        }
        catch (Exception ex) when (ex is not Xunit.Sdk.XunitException)
        {
            Assert.Fail(
                $"[{suiteName}/{testId}] Canonical test failed with unexpected error: {ex.Message}");
        }
    }

    private static string NormalizeLineEndings(string text)
    {
        return text.Replace("\r\n", "\n").TrimEnd();
    }
}
