using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Odin.Core;
using Odin.Core.Types;
using Xunit;
using Xunit.Abstractions;

namespace Odin.Core.Tests.Golden;

/// <summary>
/// Self-testing ODIN transforms.
///
/// These transforms contain their own test cases and assertions via accumulators.
/// The runner executes each transform and checks TestResult.success == true.
///
/// Convention: Self-testing transforms end with .test.odin
/// </summary>
[Trait("Category", "Golden")]
public class SelfTestTests : GoldenTestBase
{
    private readonly ITestOutputHelper _output;

    public SelfTestTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Standard test input with known datetime values for deterministic testing.
    /// Transforms can access these via @.input._test.*
    /// </summary>
    private static DynValue TestInput()
    {
        return DynValue.Object(new List<KeyValuePair<string, DynValue>>
        {
            new("_test", DynValue.Object(new List<KeyValuePair<string, DynValue>>
            {
                new("currentDate", DynValue.String("2024-06-15")),
                new("currentTimestamp", DynValue.String("2024-06-15T14:30:45Z")),
                new("currentYear", DynValue.Integer(2024)),
                new("currentMonth", DynValue.Integer(6)),
                new("currentDay", DynValue.Integer(15)),
                new("currentHour", DynValue.Integer(14)),
                new("currentMinute", DynValue.Integer(30)),
                new("currentSecond", DynValue.Integer(45)),
                new("unixTime", DynValue.Integer(1718458245)),
                new("dayOfWeek", DynValue.Integer(6)),
                new("weekOfYear", DynValue.Integer(24)),
                new("quarter", DynValue.Integer(2)),
            }))
        });
    }

    public static IEnumerable<object[]> SelfTestFiles()
    {
        var goldenPath = GetGoldenPath();
        var verbsDir = Path.Combine(goldenPath, "transform", "verbs");

        if (!Directory.Exists(verbsDir))
            yield break;

        var testFiles = Directory.GetFiles(verbsDir, "*.test.odin")
            .OrderBy(f => f)
            .ToList();

        foreach (var file in testFiles)
        {
            var verbName = Path.GetFileNameWithoutExtension(file).Replace(".test", "");
            yield return new object[] { verbName, file };
        }
    }

    [Theory]
    [MemberData(nameof(SelfTestFiles))]
    public void SelfTest(string verbName, string filePath)
    {
        var transformText = File.ReadAllText(filePath);
        var input = TestInput();

        var result = Core.Odin.ExecuteTransform(transformText, input);

        // Extract TestResult section from output
        Assert.NotNull(result.Output);
        var testResult = result.Output!.Get("TestResult");
        Assert.True(testResult != null, $"No TestResult section in '{verbName}' output");

        // Extract success, passed, failed from CDM format
        var successVal = GetCdmValue(testResult!, "success");
        var passedVal = GetCdmValue(testResult!, "passed");
        var failedVal = GetCdmValue(testResult!, "failed");

        var success = successVal?.AsBool() ?? false;
        var passed = passedVal?.AsInt64() ?? (passedVal?.AsDouble().HasValue == true
            ? (long)(passedVal.AsDouble()!.Value) : 0);
        var failed = failedVal?.AsInt64() ?? (failedVal?.AsDouble().HasValue == true
            ? (long)(failedVal.AsDouble()!.Value) : 0);

        _output.WriteLine($"%{verbName}: passed={passed}, failed={failed}, success={success}");

        Assert.True(success,
            $"Self-test FAILED for '{verbName}': passed={passed}, failed={failed}");
    }

    /// <summary>
    /// Extract a value from a CDM-typed object (handles both { type: "...", value: X } and plain values).
    /// </summary>
    private static DynValue? GetCdmValue(DynValue container, string fieldName)
    {
        var field = container.Get(fieldName);
        if (field == null || field.IsNull) return null;

        // CDM format: { type: "...", value: X }
        if (field.Type == DynValueType.Object)
        {
            var val = field.Get("value");
            if (val != null) return val;
        }

        return field;
    }
}
