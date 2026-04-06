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
/// Golden diff tests. Loads test suites from GoldenData/diff/ and
/// verifies that the .NET differ produces matching results.
/// </summary>
[Trait("Category", "Golden")]
public class DiffTests : GoldenTestBase
{
    public static IEnumerable<object[]> DiffTestCases()
    {
        List<(string FilePath, TestSuite Suite)> suites;
        try
        {
            suites = LoadAllSuites("diff");
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
    [MemberData(nameof(DiffTestCases))]
    public void GoldenDiffTest(string suiteName, string testId, string filePath)
    {
        var suite = LoadTestSuite(filePath);
        var test = suite.Tests.First(t => t.Id == testId);

        try
        {
            // Load two documents to diff - support multiple field naming conventions
            string inputAText;
            string inputBText;

            if (test.Doc1 != null && test.Doc2 != null)
            {
                inputAText = test.Doc1;
                inputBText = test.Doc2;
            }
            else if (test.InputA != null && test.InputB != null)
            {
                inputAText = test.InputA;
                inputBText = test.InputB;
            }
            else if (test.Input.ValueKind == JsonValueKind.Object)
            {
                var inputObj = test.Input;
                inputAText = inputObj.TryGetProperty("a", out var a) ? a.GetString()! :
                    inputObj.TryGetProperty("doc1", out var d1) ? d1.GetString()! :
                    inputObj.TryGetProperty("docA", out var dA) ? dA.GetString()! :
                    throw new InvalidOperationException("Cannot find input A");
                inputBText = inputObj.TryGetProperty("b", out var b) ? b.GetString()! :
                    inputObj.TryGetProperty("doc2", out var d2) ? d2.GetString()! :
                    inputObj.TryGetProperty("docB", out var dB) ? dB.GetString()! :
                    throw new InvalidOperationException("Cannot find input B");
            }
            else
            {
                Assert.Fail($"[{suiteName}/{testId}] Cannot determine inputs for diff test");
                return;
            }

            var docA = Core.Odin.Parse(inputAText);
            var docB = Core.Odin.Parse(inputBText);
            var diff = Core.Odin.Diff(docA, docB);

            if (test.Expected != null)
            {
                // Check isEmpty flag
                if (test.Expected.IsEmpty.HasValue)
                {
                    Assert.Equal(test.Expected.IsEmpty.Value, diff.IsEmpty);
                }

                // Verify modifications (changed paths) if specified
                if (test.Expected.Modifications != null)
                {
                    // When additions/deletions are also specified, only check changed count
                    if (test.Expected.Additions != null || test.Expected.Deletions != null)
                    {
                        Assert.Equal(test.Expected.Modifications.Count, diff.Changed.Count);
                    }
                    else
                    {
                        // Legacy: modifications covers all change types
                        int totalChanges = diff.Added.Count + diff.Removed.Count
                            + diff.Changed.Count + diff.Moved.Count;
                        Assert.Equal(test.Expected.Modifications.Count, totalChanges);
                    }
                    foreach (var mod in test.Expected.Modifications)
                    {
                        if (mod.Path != null)
                            Assert.Contains(diff.Changed, c => c.Path == mod.Path);
                    }
                }

                // Verify additions if specified
                if (test.Expected.Additions != null)
                {
                    Assert.Equal(test.Expected.Additions.Count, diff.Added.Count);
                    foreach (var add in test.Expected.Additions)
                    {
                        if (add.Path != null)
                            Assert.Contains(diff.Added, a => a.Path == add.Path);
                    }
                }

                // Verify deletions if specified
                if (test.Expected.Deletions != null)
                {
                    Assert.Equal(test.Expected.Deletions.Count, diff.Removed.Count);
                    foreach (var del in test.Expected.Deletions)
                    {
                        if (del.Path != null)
                            Assert.Contains(diff.Removed, r => r.Path == del.Path);
                    }
                }

                // Verify changes if specified (legacy flat list)
                if (test.Expected.Changes != null)
                {
                    var allChanges = new List<(string Type, string Path)>();
                    foreach (var entry in diff.Added)
                        allChanges.Add(("added", entry.Path));
                    foreach (var entry in diff.Removed)
                        allChanges.Add(("removed", entry.Path));
                    foreach (var entry in diff.Changed)
                        allChanges.Add(("changed", entry.Path));
                    foreach (var entry in diff.Moved)
                        allChanges.Add(("moved", entry.FromPath));

                    Assert.Equal(test.Expected.Changes.Count, allChanges.Count);
                    foreach (var change in test.Expected.Changes)
                    {
                        if (change.Path != null)
                            Assert.Contains(allChanges, c => c.Path == change.Path);
                    }
                }
            }
        }
        catch (Exception ex) when (ex is not Xunit.Sdk.XunitException)
        {
            Assert.Fail(
                $"[{suiteName}/{testId}] Diff test failed with unexpected error: {ex.Message}");
        }
    }
}
