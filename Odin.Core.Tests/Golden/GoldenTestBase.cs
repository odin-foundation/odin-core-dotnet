using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Odin.Core.Tests.Golden;

// ─────────────────────────────────────────────────────────────────────────────
// DTOs for deserializing golden test JSON files
// ─────────────────────────────────────────────────────────────────────────────

public class TestSuite
{
    [JsonPropertyName("suite")]
    public string Suite { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("tests")]
    public List<TestCase> Tests { get; set; } = new();
}

public class TestCase
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("input")]
    public JsonElement Input { get; set; }

    [JsonPropertyName("expected")]
    public JsonElement? ExpectedRaw { get; set; }

    /// <summary>
    /// Deserializes Expected as the Expected DTO when the JSON value is an object.
    /// Returns null if the value is a string or other non-object type.
    /// </summary>
    [JsonIgnore]
    public Expected? Expected
    {
        get
        {
            if (!ExpectedRaw.HasValue)
                return null;
            if (ExpectedRaw.Value.ValueKind != JsonValueKind.Object)
                return null;
            return JsonSerializer.Deserialize<Expected>(ExpectedRaw.Value.GetRawText(),
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true,
                });
        }
    }

    /// <summary>
    /// Returns the expected value as a raw string when the JSON value is a string
    /// (used by canonical tests where expected is a plain string).
    /// </summary>
    [JsonIgnore]
    public string? ExpectedString
    {
        get
        {
            if (!ExpectedRaw.HasValue)
                return null;
            if (ExpectedRaw.Value.ValueKind == JsonValueKind.String)
                return ExpectedRaw.Value.GetString();
            return null;
        }
    }

    [JsonPropertyName("expectError")]
    public ExpectError? ExpectError { get; set; }

    // Transform-specific fields
    [JsonPropertyName("transformFile")]
    public string? TransformFile { get; set; }

    [JsonPropertyName("transform")]
    public string? Transform { get; set; }

    [JsonPropertyName("schemaFile")]
    public string? SchemaFile { get; set; }

    [JsonPropertyName("schema")]
    public string? Schema { get; set; }

    [JsonPropertyName("inputA")]
    public string? InputA { get; set; }

    [JsonPropertyName("inputB")]
    public string? InputB { get; set; }

    [JsonPropertyName("options")]
    public TestCaseOptions? Options { get; set; }

    [JsonPropertyName("doc1")]
    public string? Doc1 { get; set; }

    [JsonPropertyName("doc2")]
    public string? Doc2 { get; set; }
}

public class TestCaseOptions
{
    [JsonPropertyName("strict")]
    public bool? Strict { get; set; }
}

public class Expected
{
    [JsonPropertyName("assignments")]
    public Dictionary<string, ExpectedValue>? Assignments { get; set; }

    [JsonPropertyName("documents")]
    public List<ExpectedDocument>? Documents { get; set; }

    [JsonPropertyName("directives")]
    public List<ExpectedDirective>? Directives { get; set; }

    [JsonPropertyName("output")]
    public JsonElement? Output { get; set; }

    [JsonPropertyName("formatted")]
    public string? Formatted { get; set; }

    [JsonPropertyName("valid")]
    public bool? Valid { get; set; }

    [JsonPropertyName("errors")]
    public List<ExpectedValidationError>? Errors { get; set; }

    [JsonPropertyName("changes")]
    public List<ExpectedChange>? Changes { get; set; }

    [JsonPropertyName("canonical")]
    public string? Canonical { get; set; }

    [JsonPropertyName("modifiers")]
    public Dictionary<string, ExpectedModifiers>? Modifiers { get; set; }

    [JsonPropertyName("note")]
    public string? Note { get; set; }

    [JsonPropertyName("computed")]
    public JsonElement? Computed { get; set; }

    [JsonPropertyName("isEmpty")]
    public bool? IsEmpty { get; set; }

    [JsonPropertyName("modifications")]
    public List<ExpectedChange>? Modifications { get; set; }

    [JsonPropertyName("additions")]
    public List<ExpectedChange>? Additions { get; set; }

    [JsonPropertyName("deletions")]
    public List<ExpectedChange>? Deletions { get; set; }

    // Binary canonical output fields
    [JsonPropertyName("hex")]
    public string? Hex { get; set; }

    [JsonPropertyName("sha256")]
    public string? Sha256 { get; set; }

    [JsonPropertyName("byteLength")]
    public int? ByteLength { get; set; }
}

public class ExpectedDocument
{
    [JsonPropertyName("metadata")]
    public Dictionary<string, JsonElement>? Metadata { get; set; }

    [JsonPropertyName("assignments")]
    public Dictionary<string, ExpectedValue>? Assignments { get; set; }
}

public class ExpectedValue
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public JsonElement? Value { get; set; }

    [JsonPropertyName("raw")]
    public string? Raw { get; set; }

    [JsonPropertyName("path")]
    public string? Path { get; set; }

    [JsonPropertyName("base64")]
    public string? Base64 { get; set; }

    [JsonPropertyName("algorithm")]
    public string? Algorithm { get; set; }

    [JsonPropertyName("decimalPlaces")]
    public int? DecimalPlaces { get; set; }

    [JsonPropertyName("currencyCode")]
    public string? CurrencyCode { get; set; }

    [JsonPropertyName("modifiers")]
    public List<string>? Modifiers { get; set; }

    [JsonPropertyName("note")]
    public string? Note { get; set; }

    [JsonPropertyName("items")]
    public List<ExpectedValue>? Items { get; set; }

    [JsonPropertyName("fields")]
    public Dictionary<string, ExpectedValue>? Fields { get; set; }

    [JsonPropertyName("isArrayClear")]
    public bool? IsArrayClear { get; set; }
}

public class ExpectError
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

public class ExpectedDirective
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("path")]
    public string? Path { get; set; }

    [JsonPropertyName("alias")]
    public JsonElement? Alias { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("condition")]
    public string? Condition { get; set; }
}

public class ExpectedValidationError
{
    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("path")]
    public string? Path { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

public class ExpectedChange
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("path")]
    public string? Path { get; set; }

    [JsonPropertyName("oldValue")]
    public JsonElement? OldValue { get; set; }

    [JsonPropertyName("newValue")]
    public JsonElement? NewValue { get; set; }
}

public class ExpectedModifiers
{
    [JsonPropertyName("required")]
    public bool? Required { get; set; }

    [JsonPropertyName("confidential")]
    public bool? Confidential { get; set; }

    [JsonPropertyName("deprecated")]
    public bool? Deprecated { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
// Manifest DTOs
// ─────────────────────────────────────────────────────────────────────────────

public class TestManifest
{
    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("testSuites")]
    public List<ManifestEntry> TestSuites { get; set; } = new();
}

public class ManifestEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;
}

// ─────────────────────────────────────────────────────────────────────────────
// Base class for golden test runners
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Base class providing infrastructure for loading and running golden tests.
/// Finds the GoldenData directory relative to the test assembly location.
/// </summary>
public abstract class GoldenTestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>
    /// Gets the root path to the GoldenData directory.
    /// The golden data is copied to the output directory by the csproj Content item.
    /// </summary>
    public static string GetGoldenPath()
    {
        var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        var goldenPath = Path.Combine(assemblyDir, "GoldenData");

        if (Directory.Exists(goldenPath))
            return goldenPath;

        // Fallback: walk up looking for golden data (dev-time scenario)
        var current = assemblyDir;
        for (var i = 0; i < 10; i++)
        {
            var candidate = Path.Combine(current, "GoldenData");
            if (Directory.Exists(candidate))
                return candidate;
            current = Path.GetDirectoryName(current);
            if (current == null) break;
        }

        throw new DirectoryNotFoundException(
            $"GoldenData directory not found. Looked in: {goldenPath}");
    }

    /// <summary>
    /// Gets the path to a specific golden test category (e.g., "parse", "transform").
    /// </summary>
    protected static string GetCategoryPath(string category)
    {
        return Path.Combine(GetGoldenPath(), category);
    }

    /// <summary>
    /// Loads and deserializes a test suite JSON file.
    /// </summary>
    protected static TestSuite LoadTestSuite(string filePath)
    {
        var json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<TestSuite>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Failed to deserialize test suite: {filePath}");
    }

    /// <summary>
    /// Loads the manifest for a test category.
    /// </summary>
    protected static TestManifest? LoadManifest(string category)
    {
        var manifestPath = Path.Combine(GetCategoryPath(category), "manifest.json");
        if (!File.Exists(manifestPath))
            return null;

        var json = File.ReadAllText(manifestPath);
        return JsonSerializer.Deserialize<TestManifest>(json, JsonOptions);
    }

    /// <summary>
    /// Discovers all JSON test suite files in a category directory (recursively).
    /// </summary>
    protected static IEnumerable<string> DiscoverTestFiles(string category)
    {
        var categoryPath = GetCategoryPath(category);
        if (!Directory.Exists(categoryPath))
            yield break;

        foreach (var file in Directory.EnumerateFiles(categoryPath, "*.json", SearchOption.AllDirectories))
        {
            var fileName = Path.GetFileName(file);
            // Skip manifests and non-test files
            if (fileName.Equals("manifest.json", StringComparison.OrdinalIgnoreCase))
                continue;
            yield return file;
        }
    }

    /// <summary>
    /// Loads all test suites for a category, returning tuples of (filePath, suite).
    /// Gracefully skips files that fail to deserialize.
    /// </summary>
    protected static List<(string FilePath, TestSuite Suite)> LoadAllSuites(string category)
    {
        var results = new List<(string, TestSuite)>();
        foreach (var file in DiscoverTestFiles(category))
        {
            try
            {
                var suite = LoadTestSuite(file);
                results.Add((file, suite));
            }
            catch
            {
                // Skip files that don't match our DTO structure
            }
        }
        return results;
    }

    /// <summary>
    /// Gets the input as a string. Handles both string and other JSON element types.
    /// </summary>
    protected static string GetInputString(TestCase test)
    {
        if (test.Input.ValueKind == JsonValueKind.String)
            return test.Input.GetString()!;
        if (test.Input.ValueKind == JsonValueKind.Undefined)
        {
            // Fall back to schema field for schema-only tests
            if (test.Schema != null)
                return test.Schema;
            return string.Empty;
        }
        return test.Input.GetRawText();
    }

    /// <summary>
    /// Reads a file relative to a test suite file path.
    /// </summary>
    protected static string ReadRelativeFile(string suiteFilePath, string relativePath)
    {
        var dir = Path.GetDirectoryName(suiteFilePath)!;
        var fullPath = Path.Combine(dir, relativePath);
        return File.ReadAllText(fullPath);
    }
}
