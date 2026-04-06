using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Odin.Core;
using Odin.Core.Transform;
using Odin.Core.Types;
using Xunit;
using Xunit.Abstractions;

namespace Odin.Core.Tests.Golden;

/// <summary>
/// Golden try-it verb tests. Discovers all verb triplets (input.json,
/// transform.odin, expected.odin) in GoldenData/transform/verbs/try-it/
/// and validates that executing each transform produces the expected output.
/// </summary>
[Trait("Category", "Golden")]
public class TryItVerbTests : GoldenTestBase
{
    private readonly ITestOutputHelper _output;

    public TryItVerbTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void GoldenTryItVerbTests_RunAll()
    {
        var tryItDir = Path.Combine(GetGoldenPath(), "transform", "verbs", "try-it");
        if (!Directory.Exists(tryItDir))
        {
            _output.WriteLine("try-it directory not found, skipping");
            return;
        }

        // Discover all verbs by finding *.expected.odin files
        var expectedFiles = Directory.GetFiles(tryItDir, "*.expected.odin")
            .OrderBy(f => f)
            .ToList();

        if (expectedFiles.Count == 0)
        {
            _output.WriteLine("No expected.odin files found, skipping");
            return;
        }

        int passed = 0;
        int failed = 0;
        var failures = new List<string>();

        foreach (var expectedFile in expectedFiles)
        {
            var fileName = Path.GetFileName(expectedFile);
            var verb = fileName.Replace(".expected.odin", "");

            var inputFile = Path.Combine(tryItDir, $"{verb}.input.json");
            var transformFile = Path.Combine(tryItDir, $"{verb}.transform.odin");

            if (!File.Exists(inputFile))
            {
                _output.WriteLine($"  SKIP [{verb}]: {verb}.input.json not found");
                continue;
            }

            if (!File.Exists(transformFile))
            {
                _output.WriteLine($"  SKIP [{verb}]: {verb}.transform.odin not found");
                continue;
            }

            try
            {
                var inputRaw = File.ReadAllText(inputFile).Replace("\r\n", "\n");
                var transformText = File.ReadAllText(transformFile).Replace("\r\n", "\n");
                var expectedText = File.ReadAllText(expectedFile).Replace("\r\n", "\n");

                var transform = Core.Odin.ParseTransform(transformText);
                transform.Target.Format = "odin";
                var input = JsonSourceParser.Parse(inputRaw);
                var result = TransformEngine.Execute(transform, input);

                if (result.Errors.Count > 0)
                {
                    var errMsgs = string.Join(", ", result.Errors.Select(e => $"[{e.Code}] {e.Message}"));
                    throw new Exception($"Transform failed: {errMsgs}");
                }

                var actual = (result.Formatted ?? "").Replace("\r\n", "\n").TrimEnd();
                var expected = expectedText.Replace("\r\n", "\n").TrimEnd();

                if (actual != expected)
                {
                    throw new Exception(
                        $"Output mismatch:\n--- expected ---\n{expected}\n--- actual ---\n{actual}");
                }

                passed++;
                _output.WriteLine($"  PASS [{verb}]");
            }
            catch (Exception ex)
            {
                failed++;
                var msg = $"  FAIL [{verb}]: {ex.Message}";
                _output.WriteLine(msg);
                failures.Add(msg);
            }
        }

        _output.WriteLine($"\nTry-it verbs: {passed} passed, {failed} failed");

        if (failures.Count > 0)
        {
            Assert.Fail($"Try-it verb tests had {failed} failures:\n{string.Join("\n", failures)}");
        }
    }
}
