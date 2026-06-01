using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Odin.Core;
using Odin.Core.Forms;
using Xunit;

namespace Odin.Core.Tests.Golden;

/// <summary>
/// Golden forms tests. Loads the shared forms manifest from GoldenData/forms/
/// and exercises parse + render conformance against the cross-language contract.
/// </summary>
[Trait("Category", "Golden")]
public class FormsTests : GoldenTestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static IEnumerable<object[]> FormsTestCases()
    {
        string formsDir;
        try { formsDir = GetCategoryPath("forms"); }
        catch (DirectoryNotFoundException) { yield break; }

        var manifestPath = Path.Combine(formsDir, "manifest.json");
        if (!File.Exists(manifestPath))
            yield break;

        var suite = JsonSerializer.Deserialize<FormsSuite>(File.ReadAllText(manifestPath), JsonOptions);
        if (suite?.Tests == null)
            yield break;

        foreach (var test in suite.Tests)
            yield return new object[] { test.Id };
    }

    [Theory]
    [MemberData(nameof(FormsTestCases))]
    public void GoldenFormsTest(string testId)
    {
        var formsDir = GetCategoryPath("forms");
        var manifestPath = Path.Combine(formsDir, "manifest.json");
        var suite = JsonSerializer.Deserialize<FormsSuite>(File.ReadAllText(manifestPath), JsonOptions)!;
        var test = suite.Tests.First(t => t.Id == testId);

        var formText = File.ReadAllText(Path.Combine(formsDir, test.FormFile));
        var form = Odin.ParseForm(formText);

        if (test.ExpectParse != null)
            VerifyParse(form, test.ExpectParse, testId);

        if (test.RenderContains != null || test.RenderNotContains != null)
        {
            var data = test.RenderData != null ? Odin.Parse(test.RenderData) : null;
            var html = Odin.RenderForm(form, data, new RenderFormOptions { Target = "html" });

            foreach (var needle in test.RenderContains ?? new List<string>())
                Assert.True(html.Contains(needle, StringComparison.Ordinal),
                    $"[{testId}] expected render to contain: {needle}");
            foreach (var needle in test.RenderNotContains ?? new List<string>())
                Assert.False(html.Contains(needle, StringComparison.Ordinal),
                    $"[{testId}] expected render NOT to contain: {needle}");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Parse verification
    // ─────────────────────────────────────────────────────────────────────────

    private static void VerifyParse(OdinForm form, ExpectParse ep, string testId)
    {
        if (ep.Pages.HasValue)
            Assert.Equal(ep.Pages.Value, form.Pages.Count);

        if (ep.Margins != null)
        {
            foreach (var (side, value) in ep.Margins)
            {
                var actual = MarginSide(form.PageDefaults?.Margin, side);
                Assert.True(actual.HasValue, $"[{testId}] missing margin.{side}");
                Assert.Equal(value, actual!.Value, 6);
            }
        }

        if (ep.Templates != null)
        {
            foreach (var (name, t) in ep.Templates)
            {
                Assert.NotNull(form.Templates);
                Assert.True(form.Templates!.TryGetValue(name, out var tpl), $"[{testId}] template {name} missing");
                if (t.PageTemplate.HasValue) Assert.Equal(t.PageTemplate.Value, tpl!.IsPageTemplate);
                if (t.Continues != null) Assert.Equal(t.Continues, tpl!.Continues);
                if (t.FormId != null) Assert.Equal(t.FormId, tpl!.FormId);
                if (t.ElementTypes != null)
                    Assert.Equal(t.ElementTypes, tpl!.Elements.Select(e => e.Type).ToList());
            }
        }

        if (ep.Page0 != null)
        {
            var page0 = form.Pages[0];
            if (ep.Page0.ElementTypes != null)
                Assert.Equal(ep.Page0.ElementTypes, page0.Elements.Select(e => e.Type).ToList());
            if (ep.Page0.Elements != null)
            {
                foreach (var (name, exp) in ep.Page0.Elements)
                    CheckElement(page0.Elements.FirstOrDefault(e => e.Name == name), exp, name, testId);
            }
        }
    }

    private static double? MarginSide(PageMargins? m, string side)
    {
        if (m == null) return null;
        return side switch
        {
            "top" => m.Top,
            "right" => m.Right,
            "bottom" => m.Bottom,
            "left" => m.Left,
            _ => null,
        };
    }

    private static void CheckElement(FormElement? el, ElementExpectation exp, string name, string testId)
    {
        Assert.True(el != null, $"[{testId}] element '{name}' not found");

        if (exp.Type != null) Assert.Equal(exp.Type, el!.Type);

        switch (el)
        {
            case TextFieldElement t:
                if (exp.Value != null) Assert.Equal(exp.Value, t.Value);
                if (exp.InputType != null) Assert.Equal(exp.InputType, t.InputType);
                if (exp.Label != null) Assert.Equal(exp.Label, t.Label);
                break;
            case CheckboxElement c:
                if (exp.Checked.HasValue) Assert.Equal(exp.Checked.Value, c.Checked);
                break;
            case DateElement d:
                if (exp.Value != null) Assert.Equal(exp.Value, d.Value);
                if (exp.Min != null) Assert.Equal(exp.Min, d.Min);
                if (exp.MaxString != null) Assert.Equal(exp.MaxString, d.Max);
                break;
            case SelectElement s:
                if (exp.Selected != null) Assert.Equal(exp.Selected, s.Selected);
                if (exp.Options != null) Assert.Equal(exp.Options, s.Options.ToList());
                if (exp.Label != null) Assert.Equal(exp.Label, s.Label);
                break;
            case ImageElement img:
                if (exp.Background.HasValue) Assert.Equal(exp.Background.Value, img.Background);
                break;
            case BarcodeElement bc:
                if (exp.BarcodeType != null) Assert.Equal(exp.BarcodeType, bc.BarcodeType);
                break;
            case RegionElement r:
                if (exp.Bind != null) Assert.Equal(exp.Bind, r.Bind);
                if (exp.MaxInt.HasValue) Assert.Equal(exp.MaxInt.Value, r.Max);
                if (exp.Overflow != null) Assert.Equal(exp.Overflow, r.Overflow);
                if (exp.ChildCount.HasValue) Assert.Equal(exp.ChildCount.Value, r.Children.Count);
                break;
        }

        if (exp.Label != null && el is BaseFieldElement bf && el is not TextFieldElement && el is not SelectElement)
            Assert.Equal(exp.Label, bf.Label);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Manifest DTOs
    // ─────────────────────────────────────────────────────────────────────────

    private sealed class FormsSuite
    {
        [JsonPropertyName("suite")] public string Suite { get; set; } = "";
        [JsonPropertyName("description")] public string? Description { get; set; }
        [JsonPropertyName("tests")] public List<FormsGoldenTest> Tests { get; set; } = new();
    }

    private sealed class FormsGoldenTest
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("description")] public string? Description { get; set; }
        [JsonPropertyName("formFile")] public string FormFile { get; set; } = "";
        [JsonPropertyName("renderData")] public string? RenderData { get; set; }
        [JsonPropertyName("expectParse")] public ExpectParse? ExpectParse { get; set; }
        [JsonPropertyName("renderContains")] public List<string>? RenderContains { get; set; }
        [JsonPropertyName("renderNotContains")] public List<string>? RenderNotContains { get; set; }
    }

    private sealed class ExpectParse
    {
        [JsonPropertyName("pages")] public int? Pages { get; set; }
        [JsonPropertyName("margins")] public Dictionary<string, double>? Margins { get; set; }
        [JsonPropertyName("templates")] public Dictionary<string, TemplateExpectation>? Templates { get; set; }
        [JsonPropertyName("page0")] public PageExpectation? Page0 { get; set; }
    }

    private sealed class TemplateExpectation
    {
        [JsonPropertyName("pageTemplate")] public bool? PageTemplate { get; set; }
        [JsonPropertyName("continues")] public string? Continues { get; set; }
        [JsonPropertyName("formId")] public string? FormId { get; set; }
        [JsonPropertyName("elementTypes")] public List<string>? ElementTypes { get; set; }
    }

    private sealed class PageExpectation
    {
        [JsonPropertyName("elementTypes")] public List<string>? ElementTypes { get; set; }
        [JsonPropertyName("elements")] public Dictionary<string, ElementExpectation>? Elements { get; set; }
    }

    private sealed class ElementExpectation
    {
        [JsonPropertyName("type")] public string? Type { get; set; }
        [JsonPropertyName("bind")] public string? Bind { get; set; }
        // "max" is polymorphic: an int for region capacity, a string for date max.
        [JsonPropertyName("max")] public JsonElement? Max { get; set; }
        [JsonPropertyName("overflow")] public string? Overflow { get; set; }
        [JsonPropertyName("childCount")] public int? ChildCount { get; set; }
        [JsonPropertyName("value")] public string? Value { get; set; }
        [JsonPropertyName("inputType")] public string? InputType { get; set; }
        [JsonPropertyName("checked")] public bool? Checked { get; set; }
        [JsonPropertyName("selected")] public string? Selected { get; set; }
        [JsonPropertyName("options")] public List<string>? Options { get; set; }
        [JsonPropertyName("min")] public string? Min { get; set; }
        [JsonPropertyName("label")] public string? Label { get; set; }
        [JsonPropertyName("barcodeType")] public string? BarcodeType { get; set; }
        [JsonPropertyName("background")] public bool? Background { get; set; }

        public int? MaxInt => Max is { ValueKind: JsonValueKind.Number } e ? e.GetInt32() : null;
        public string? MaxString => Max is { ValueKind: JsonValueKind.String } e ? e.GetString() : null;
    }
}
