// ODIN Forms 1.0 — Form Parser
//
// Parses a .odin forms document into a typed OdinForm structure.
// Delegates low-level ODIN parsing to Odin.Parse(), then maps the
// resulting flat path space onto the strongly-typed Forms 1.0 schema.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Odin.Core.Types;

namespace Odin.Core.Forms;

/// <summary>
/// Parses ODIN forms documents into typed OdinForm structures.
/// </summary>
public static class FormParser
{
    // Canonical metadata sub-section paths surface as $..section.* assignments
    // (e.g. $..page.width). Read them through this prefix.
    private const string MetaPrefix = "$.";

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Parse an ODIN forms document text into a typed OdinForm.
    /// </summary>
    /// <param name="text">Raw ODIN text from a .odin forms file.</param>
    /// <returns>Parsed OdinForm with metadata, page defaults, pages, and templates.</returns>
    public static OdinForm ParseForm(string text)
    {
        // {@tpl_*} template headers are not valid core sections, so split them
        // out before parsing and parse each template body separately.
        var (body, templateBlocks) = SplitTemplates(text);

        var doc = Odin.Parse(body);

        var metadata     = ExtractMetadata(doc);
        var pageDefaults = ExtractPageDefaults(doc);
        var screen       = ExtractScreen(doc);
        var i18n         = ExtractI18n(doc);
        var pages        = ExtractPages(doc, i18n);
        var templates    = ExtractTemplates(templateBlocks, i18n);

        return new OdinForm(metadata, pages, pageDefaults, screen, i18n, templates);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Page Template Extraction
    // ─────────────────────────────────────────────────────────────────────────

    private sealed class TemplateBlock
    {
        public string Name = "";
        public string Text = "";
    }

    private static readonly Regex TplHeader =
        new Regex(@"^\s*\{\s*@(tpl_[A-Za-z0-9_]+)\s*\}\s*$", RegexOptions.Compiled);
    private static readonly Regex TopLevelHeader =
        new Regex(@"^\s*\{\s*(\$|page\[\d+\]|@tpl_)", RegexOptions.Compiled);

    /// <summary>
    /// Split a forms document into its core-parseable body and the raw text of
    /// each {@tpl_*} template block. A template block runs from its header line
    /// until the next top-level section ({$...}, {page[...]}, or another {@tpl_*}).
    /// </summary>
    private static (string Body, List<TemplateBlock> Blocks) SplitTemplates(string text)
    {
        var lines = text.Split('\n');
        var bodyLines = new List<string>();
        var blocks = new List<TemplateBlock>();

        TemplateBlock? current = null;
        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');
            var tplMatch = TplHeader.Match(line);
            if (tplMatch.Success)
            {
                current = new TemplateBlock { Name = tplMatch.Groups[1].Value, Text = "" };
                blocks.Add(current);
                continue;
            }
            if (current != null)
            {
                // A new top-level (non-template) section ends the current template.
                if (TopLevelHeader.IsMatch(line) && !TplHeader.IsMatch(line))
                {
                    current = null;
                    bodyLines.Add(line);
                }
                else
                {
                    current.Text += line + "\n";
                }
                continue;
            }
            bodyLines.Add(line);
        }

        return (Reanchor(string.Join("\n", bodyLines), null), blocks);
    }

    private static readonly Regex AnchorHeader =
        new Regex(@"^\s*\{\s*(page\[\d+\]|tpl\.[A-Za-z0-9_]+)\s*\}\s*$", RegexOptions.Compiled);
    private static readonly Regex RelativeHeader = new Regex(@"^\s*\{\s*\.", RegexOptions.Compiled);
    private static readonly Regex RelativeTabular = new Regex(@"^\s*\{\s*\.[^}]*\[\]\s*:", RegexOptions.Compiled);

    /// <summary>
    /// A relative tabular header ({.x[] : ...}) leaves the parent context pointing
    /// at the field, so following relative headers nest under it. Re-emit the active
    /// top-level anchor after each such block so siblings resolve correctly.
    /// </summary>
    private static string Reanchor(string text, string? rootAnchor)
    {
        var lines = text.Split('\n');
        var outLines = new List<string>();

        string? anchor = rootAnchor;
        bool needsReanchor = false;

        foreach (var line in lines)
        {
            var anchorMatch = AnchorHeader.Match(line);
            if (anchorMatch.Success)
            {
                anchor = "{" + anchorMatch.Groups[1].Value + "}";
                needsReanchor = false;
                outLines.Add(line);
                continue;
            }

            if (RelativeHeader.IsMatch(line))
            {
                if (needsReanchor && anchor != null)
                {
                    outLines.Add(anchor);
                    needsReanchor = false;
                }
                if (RelativeTabular.IsMatch(line))
                    needsReanchor = true;
                outLines.Add(line);
                continue;
            }

            outLines.Add(line);
        }

        return string.Join("\n", outLines);
    }

    /// <summary>
    /// Parse each template block body into a PageTemplate. The body is reparented
    /// under a synthetic {tpl.&lt;name&gt;} section so it parses as ordinary ODIN.
    /// </summary>
    private static Dictionary<string, PageTemplate>? ExtractTemplates(
        List<TemplateBlock> blocks, IReadOnlyDictionary<string, string>? i18n)
    {
        if (blocks.Count == 0)
            return null;

        var templates = new Dictionary<string, PageTemplate>();
        foreach (var block in blocks)
        {
            var root = $"tpl.{block.Name}";
            var synthetic = Reanchor("{" + root + "}\n" + block.Text, "{" + root + "}");
            var doc = Odin.Parse(synthetic);

            var prefix = $"{root}.";
            var isPageTemplate = GetBoolean(doc, $"{prefix}page-template") ?? true;
            var continues = GetString(doc, $"{prefix}continues");
            var formId = GetString(doc, $"{prefix}form-id");
            var elements = ExtractElements(doc, prefix, i18n);

            templates[block.Name] = new PageTemplate(block.Name, isPageTemplate, elements, continues, formId);
        }
        return templates;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Metadata and Settings Extraction
    // ─────────────────────────────────────────────────────────────────────────

    private static FormMetadata ExtractMetadata(OdinDocument doc)
    {
        var title   = GetString(doc, "$.title") ?? "";
        var id      = GetString(doc, "$.id") ?? "";
        var lang    = GetString(doc, "$.lang") ?? "en";
        var version = GetString(doc, "$.forms");
        return new FormMetadata(title, id, lang, version);
    }

    private static PageDefaults? ExtractPageDefaults(OdinDocument doc)
    {
        var width  = GetNumber(doc, $"{MetaPrefix}.page.width");
        var height = GetNumber(doc, $"{MetaPrefix}.page.height");
        var unit   = GetString(doc, $"{MetaPrefix}.page.unit");
        var margin = ExtractMargins(doc);

        if (width == null && height == null && unit == null)
            return null;

        var resolvedUnit = unit is "inch" or "cm" or "mm" or "pt" ? unit : "inch";
        return new PageDefaults(width ?? 8.5, height ?? 11.0, resolvedUnit, margin);
    }

    private static PageMargins? ExtractMargins(OdinDocument doc)
    {
        var top    = GetNumber(doc, $"{MetaPrefix}.page.margin.top");
        var right  = GetNumber(doc, $"{MetaPrefix}.page.margin.right");
        var bottom = GetNumber(doc, $"{MetaPrefix}.page.margin.bottom");
        var left   = GetNumber(doc, $"{MetaPrefix}.page.margin.left");
        if (top == null && right == null && bottom == null && left == null)
            return null;
        return new PageMargins(top, right, bottom, left);
    }

    private static ScreenSettings? ExtractScreen(OdinDocument doc)
    {
        var scale = GetNumber(doc, $"{MetaPrefix}.screen.scale");
        return scale.HasValue ? new ScreenSettings(scale.Value) : null;
    }

    private static Dictionary<string, string>? ExtractI18n(OdinDocument doc)
    {
        var result = new Dictionary<string, string>();

        // Metadata sub-sections store i18n entries keyed with a leading dot
        // (".i18n.en.field_name"). Surface them keyed without the i18n. prefix.
        foreach (var entry in doc.Metadata)
        {
            var key = entry.Key;
            const string lead = ".i18n.";
            if (key.StartsWith(lead, StringComparison.Ordinal))
            {
                var sub = key.Substring(lead.Length);
                var val = entry.Value.AsString();
                if (!string.IsNullOrEmpty(sub) && val != null)
                    result[sub] = val;
            }
        }

        return result.Count > 0 ? result : null;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Page and Element Extraction
    // ─────────────────────────────────────────────────────────────────────────

    private static readonly Regex PagePathPattern = new Regex(@"^page\[(\d+)\]\.", RegexOptions.Compiled);

    /// <summary>
    /// Walk all paths to find distinct page indices, then for each page
    /// collect elements in document order.
    /// </summary>
    private static IReadOnlyList<FormPage> ExtractPages(OdinDocument doc, IReadOnlyDictionary<string, string>? i18n)
    {
        var allPaths = doc.Paths();
        var pageIndices = new HashSet<int>();

        foreach (var path in allPaths)
        {
            var m = PagePathPattern.Match(path);
            if (m.Success)
                pageIndices.Add(int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture));
        }

        if (pageIndices.Count == 0)
            return Array.Empty<FormPage>();

        var sortedIndices = new List<int>(pageIndices);
        sortedIndices.Sort();

        var pages = new List<FormPage>(sortedIndices.Count);
        foreach (var index in sortedIndices)
            pages.Add(new FormPage(ExtractElements(doc, $"page[{index}].", i18n)));

        return pages;
    }

    /// <summary>
    /// Collect element keys under prefix in document order and build each element.
    /// An element key is "{elementType}.{elementName}" (e.g. "text.title",
    /// "region.vehicles"). Region children are absorbed by their region.
    /// </summary>
    private static List<FormElement> ExtractElements(
        OdinDocument doc, string prefix, IReadOnlyDictionary<string, string>? i18n)
    {
        var elementKeysSeen    = new HashSet<string>();
        var elementKeysOrdered = new List<string>();

        foreach (var path in doc.Paths())
        {
            if (!path.StartsWith(prefix, StringComparison.Ordinal))
                continue;

            var rest  = path.Substring(prefix.Length); // e.g. "text.title.content"
            var parts = rest.Split('.');
            if (parts.Length >= 2)
            {
                var elementKey = $"{parts[0]}.{parts[1]}";
                if (elementKeysSeen.Add(elementKey))
                    elementKeysOrdered.Add(elementKey);
            }
        }

        var elements  = new List<FormElement>();
        var idCounter = 0;
        foreach (var elementKey in elementKeysOrdered)
        {
            var dotIdx      = elementKey.IndexOf('.');
            var elementType = elementKey.Substring(0, dotIdx);
            var elementName = elementKey.Substring(dotIdx + 1);
            var elementPrefix = $"{prefix}{elementKey}.";
            var element = BuildElement(doc, elementType, elementName, elementPrefix, idCounter++, i18n);
            if (element != null)
                elements.Add(element);
        }

        return elements;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Element Builder Dispatch
    // ─────────────────────────────────────────────────────────────────────────

    private static FormElement? BuildElement(OdinDocument doc, string elementType, string elementName,
        string prefix, int idCounter, IReadOnlyDictionary<string, string>? i18n)
    {
        var id = $"{elementType}_{elementName}_{idCounter}";

        switch (elementType)
        {
            case "line":     return BuildLineElement(doc, elementName, id, prefix);
            case "rect":     return BuildRectElement(doc, elementName, id, prefix);
            case "circle":   return BuildCircleElement(doc, elementName, id, prefix);
            case "ellipse":  return BuildEllipseElement(doc, elementName, id, prefix);
            case "polygon":  return BuildPolygonElement(doc, elementName, id, prefix);
            case "polyline": return BuildPolylineElement(doc, elementName, id, prefix);
            case "path":     return BuildPathElement(doc, elementName, id, prefix);
            case "text":     return BuildTextElement(doc, elementName, id, prefix, i18n);
            case "img":      return BuildImageElement(doc, elementName, id, prefix, i18n);
            case "barcode":  return BuildBarcodeElement(doc, elementName, id, prefix, i18n);
            case "field":    return BuildFieldElement(doc, elementName, id, prefix, i18n);
            case "region":   return BuildRegionElement(doc, elementName, id, prefix, i18n);
            default:         return null;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Geometric Element Builders
    // ─────────────────────────────────────────────────────────────────────────

    private static LineElement BuildLineElement(OdinDocument doc, string name, string id, string prefix)
    {
        return new LineElement(
            name: name, id: id,
            x1: GetNumber(doc, $"{prefix}x1") ?? 0,
            y1: GetNumber(doc, $"{prefix}y1") ?? 0,
            x2: GetNumber(doc, $"{prefix}x2") ?? 0,
            y2: GetNumber(doc, $"{prefix}y2") ?? 0,
            stroke:          GetString(doc, $"{prefix}stroke"),
            strokeWidth:     GetNumber(doc, $"{prefix}stroke-width"),
            strokeOpacity:   GetNumber(doc, $"{prefix}stroke-opacity"),
            strokeDasharray: GetString(doc, $"{prefix}stroke-dasharray"),
            strokeLinecap:   GetString(doc, $"{prefix}stroke-linecap"),
            strokeLinejoin:  GetString(doc, $"{prefix}stroke-linejoin"));
    }

    private static RectElement BuildRectElement(OdinDocument doc, string name, string id, string prefix)
    {
        return new RectElement(
            name: name, id: id,
            x: GetNumber(doc, $"{prefix}x") ?? 0,
            y: GetNumber(doc, $"{prefix}y") ?? 0,
            w: GetNumber(doc, $"{prefix}w") ?? 0,
            h: GetNumber(doc, $"{prefix}h") ?? 0,
            rx: GetNumber(doc, $"{prefix}rx"),
            ry: GetNumber(doc, $"{prefix}ry"),
            stroke:          GetString(doc, $"{prefix}stroke"),
            strokeWidth:     GetNumber(doc, $"{prefix}stroke-width"),
            strokeOpacity:   GetNumber(doc, $"{prefix}stroke-opacity"),
            strokeDasharray: GetString(doc, $"{prefix}stroke-dasharray"),
            strokeLinecap:   GetString(doc, $"{prefix}stroke-linecap"),
            strokeLinejoin:  GetString(doc, $"{prefix}stroke-linejoin"),
            fill:            GetString(doc, $"{prefix}fill"),
            fillOpacity:     GetNumber(doc, $"{prefix}fill-opacity"));
    }

    private static CircleElement BuildCircleElement(OdinDocument doc, string name, string id, string prefix)
    {
        return new CircleElement(
            name: name, id: id,
            cx: GetNumber(doc, $"{prefix}cx") ?? 0,
            cy: GetNumber(doc, $"{prefix}cy") ?? 0,
            r:  GetNumber(doc, $"{prefix}r") ?? 0,
            stroke:          GetString(doc, $"{prefix}stroke"),
            strokeWidth:     GetNumber(doc, $"{prefix}stroke-width"),
            strokeOpacity:   GetNumber(doc, $"{prefix}stroke-opacity"),
            strokeDasharray: GetString(doc, $"{prefix}stroke-dasharray"),
            strokeLinecap:   GetString(doc, $"{prefix}stroke-linecap"),
            strokeLinejoin:  GetString(doc, $"{prefix}stroke-linejoin"),
            fill:            GetString(doc, $"{prefix}fill"),
            fillOpacity:     GetNumber(doc, $"{prefix}fill-opacity"));
    }

    private static EllipseElement BuildEllipseElement(OdinDocument doc, string name, string id, string prefix)
    {
        return new EllipseElement(
            name: name, id: id,
            cx: GetNumber(doc, $"{prefix}cx") ?? 0,
            cy: GetNumber(doc, $"{prefix}cy") ?? 0,
            rx: GetNumber(doc, $"{prefix}rx") ?? 0,
            ry: GetNumber(doc, $"{prefix}ry") ?? 0,
            stroke:          GetString(doc, $"{prefix}stroke"),
            strokeWidth:     GetNumber(doc, $"{prefix}stroke-width"),
            strokeOpacity:   GetNumber(doc, $"{prefix}stroke-opacity"),
            strokeDasharray: GetString(doc, $"{prefix}stroke-dasharray"),
            strokeLinecap:   GetString(doc, $"{prefix}stroke-linecap"),
            strokeLinejoin:  GetString(doc, $"{prefix}stroke-linejoin"),
            fill:            GetString(doc, $"{prefix}fill"),
            fillOpacity:     GetNumber(doc, $"{prefix}fill-opacity"));
    }

    private static PolygonElement BuildPolygonElement(OdinDocument doc, string name, string id, string prefix)
    {
        return new PolygonElement(
            name: name, id: id,
            points: GetString(doc, $"{prefix}points") ?? "",
            stroke:          GetString(doc, $"{prefix}stroke"),
            strokeWidth:     GetNumber(doc, $"{prefix}stroke-width"),
            strokeOpacity:   GetNumber(doc, $"{prefix}stroke-opacity"),
            strokeDasharray: GetString(doc, $"{prefix}stroke-dasharray"),
            strokeLinecap:   GetString(doc, $"{prefix}stroke-linecap"),
            strokeLinejoin:  GetString(doc, $"{prefix}stroke-linejoin"),
            fill:            GetString(doc, $"{prefix}fill"),
            fillOpacity:     GetNumber(doc, $"{prefix}fill-opacity"));
    }

    private static PolylineElement BuildPolylineElement(OdinDocument doc, string name, string id, string prefix)
    {
        return new PolylineElement(
            name: name, id: id,
            points: GetString(doc, $"{prefix}points") ?? "",
            stroke:          GetString(doc, $"{prefix}stroke"),
            strokeWidth:     GetNumber(doc, $"{prefix}stroke-width"),
            strokeOpacity:   GetNumber(doc, $"{prefix}stroke-opacity"),
            strokeDasharray: GetString(doc, $"{prefix}stroke-dasharray"),
            strokeLinecap:   GetString(doc, $"{prefix}stroke-linecap"),
            strokeLinejoin:  GetString(doc, $"{prefix}stroke-linejoin"));
    }

    private static PathElement BuildPathElement(OdinDocument doc, string name, string id, string prefix)
    {
        return new PathElement(
            name: name, id: id,
            d: GetString(doc, $"{prefix}d") ?? "",
            stroke:          GetString(doc, $"{prefix}stroke"),
            strokeWidth:     GetNumber(doc, $"{prefix}stroke-width"),
            strokeOpacity:   GetNumber(doc, $"{prefix}stroke-opacity"),
            strokeDasharray: GetString(doc, $"{prefix}stroke-dasharray"),
            strokeLinecap:   GetString(doc, $"{prefix}stroke-linecap"),
            strokeLinejoin:  GetString(doc, $"{prefix}stroke-linejoin"),
            fill:            GetString(doc, $"{prefix}fill"),
            fillOpacity:     GetNumber(doc, $"{prefix}fill-opacity"));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Content Element Builders
    // ─────────────────────────────────────────────────────────────────────────

    private static TextElement BuildTextElement(OdinDocument doc, string name, string id, string prefix,
        IReadOnlyDictionary<string, string>? i18n)
    {
        return new TextElement(
            name: name, id: id,
            content: GetLabel(doc, $"{prefix}content", i18n) ?? "",
            x: GetNumber(doc, $"{prefix}x") ?? 0,
            y: GetNumber(doc, $"{prefix}y") ?? 0,
            rotate:     GetNumber(doc, $"{prefix}rotate"),
            fontFamily: GetString(doc, $"{prefix}font-family"),
            fontSize:   GetNumber(doc, $"{prefix}font-size"),
            fontWeight: GetString(doc, $"{prefix}font-weight"),
            fontStyle:  GetString(doc, $"{prefix}font-style"),
            textAlign:  GetString(doc, $"{prefix}text-align"),
            color:      GetString(doc, $"{prefix}color"));
    }

    private static ImageElement BuildImageElement(OdinDocument doc, string name, string id, string prefix,
        IReadOnlyDictionary<string, string>? i18n)
    {
        return new ImageElement(
            name: name, id: id,
            src: GetBinaryLiteral(doc, $"{prefix}src") ?? "",
            alt: GetLabel(doc, $"{prefix}alt", i18n) ?? "",
            x: GetNumber(doc, $"{prefix}x") ?? 0,
            y: GetNumber(doc, $"{prefix}y") ?? 0,
            w: GetNumber(doc, $"{prefix}w") ?? 0,
            h: GetNumber(doc, $"{prefix}h") ?? 0,
            background: GetBoolean(doc, $"{prefix}background"));
    }

    private static BarcodeElement BuildBarcodeElement(OdinDocument doc, string name, string id, string prefix,
        IReadOnlyDictionary<string, string>? i18n)
    {
        var barcodeType = GetString(doc, $"{prefix}type")
            ?? GetString(doc, $"{prefix}barcode-type") ?? "code128";
        if (barcodeType is not ("code39" or "code128" or "qr" or "datamatrix" or "pdf417"))
            barcodeType = "code128";

        return new BarcodeElement(
            name: name, id: id,
            barcodeType: barcodeType,
            content: GetLabel(doc, $"{prefix}content", i18n) ?? "",
            alt:     GetLabel(doc, $"{prefix}alt", i18n) ?? "",
            x: GetNumber(doc, $"{prefix}x") ?? 0,
            y: GetNumber(doc, $"{prefix}y") ?? 0,
            w: GetNumber(doc, $"{prefix}w") ?? 0,
            h: GetNumber(doc, $"{prefix}h") ?? 0);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Field Element Builder
    // ─────────────────────────────────────────────────────────────────────────

    private static FormElement? BuildFieldElement(OdinDocument doc, string name, string id, string prefix,
        IReadOnlyDictionary<string, string>? i18n)
    {
        var fieldType = GetString(doc, $"{prefix}type") ?? "text";
        var base_     = ExtractBaseField(doc, name, id, prefix, i18n);

        switch (fieldType)
        {
            case "text":        return BuildTextField(doc, prefix, base_);
            case "checkbox":    return BuildCheckboxField(doc, prefix, base_);
            case "radio":       return BuildRadioField(doc, prefix, base_);
            case "select":      return BuildSelectField(doc, prefix, base_);
            case "multiselect": return BuildMultiselectField(doc, prefix, base_);
            case "date":        return BuildDateField(doc, prefix, base_);
            case "signature":   return BuildSignatureField(doc, prefix, base_);
            default:            return BuildTextField(doc, prefix, base_);
        }
    }

    /// <summary>Shared properties across all field types.</summary>
    private sealed class BaseFieldProps
    {
        public string Name = "";
        public string? Id;
        public string Label = "";
        public double X;
        public double Y;
        public double W;
        public double H;
        public string Bind = "";
        public string? AriaLabel;
        public int? Tabindex;
        public bool? Readonly;
        public bool? Required;
        public string? Pattern;
        public int? MinLength;
        public int? MaxLength;
        public string? Min;
        public string? Max;
    }

    private static BaseFieldProps ExtractBaseField(OdinDocument doc, string name, string id, string prefix,
        IReadOnlyDictionary<string, string>? i18n)
    {
        var required  = GetBoolean(doc, $"{prefix}required");
        var tabindex  = GetNumber(doc, $"{prefix}tabindex");
        var minLength = GetNumber(doc, $"{prefix}minLength");
        var maxLength = GetNumber(doc, $"{prefix}maxLength");
        var ariaLabel = GetLabel(doc, $"{prefix}aria-label", i18n);
        var readOnly  = GetBoolean(doc, $"{prefix}readonly");

        var min = GetNumber(doc, $"{prefix}min")?.ToString(CultureInfo.InvariantCulture)
            ?? GetScalarString(doc, $"{prefix}min");
        var max = GetNumber(doc, $"{prefix}max")?.ToString(CultureInfo.InvariantCulture)
            ?? GetScalarString(doc, $"{prefix}max");

        var bindRef = GetReference(doc, $"{prefix}bind");
        var bind    = bindRef != null ? $"@{bindRef}" : "";

        return new BaseFieldProps
        {
            Name      = name,
            Id        = id,
            Label     = GetLabel(doc, $"{prefix}label", i18n) ?? "",
            X         = GetNumber(doc, $"{prefix}x") ?? 0,
            Y         = GetNumber(doc, $"{prefix}y") ?? 0,
            W         = GetNumber(doc, $"{prefix}w") ?? 0,
            H         = GetNumber(doc, $"{prefix}h") ?? 0,
            Bind      = bind,
            AriaLabel = ariaLabel,
            Tabindex  = tabindex.HasValue ? (int?)(int)tabindex.Value : null,
            Readonly  = readOnly,
            Required  = required,
            Pattern   = GetString(doc, $"{prefix}pattern"),
            MinLength = minLength.HasValue ? (int?)(int)minLength.Value : null,
            MaxLength = maxLength.HasValue ? (int?)(int)maxLength.Value : null,
            Min       = min,
            Max       = max,
        };
    }

    private static TextFieldElement BuildTextField(OdinDocument doc, string prefix, BaseFieldProps b)
    {
        var inputType = GetString(doc, $"{prefix}inputType");
        if (inputType is not ("text" or "email" or "tel" or "password" or "number" or "url"))
            inputType = null;
        var multiline = GetBoolean(doc, $"{prefix}multiline");
        var maxLines  = GetNumber(doc, $"{prefix}maxLines");

        return new TextFieldElement(
            name: b.Name, id: b.Id, label: b.Label,
            x: b.X, y: b.Y, w: b.W, h: b.H, bind: b.Bind,
            ariaLabel: b.AriaLabel, tabindex: b.Tabindex, readOnly: b.Readonly,
            required: b.Required, pattern: b.Pattern, minLength: b.MinLength, maxLength: b.MaxLength,
            min: b.Min, max: b.Max,
            value:       GetScalarString(doc, $"{prefix}value"),
            inputType:   inputType,
            mask:        GetString(doc, $"{prefix}mask"),
            placeholder: GetString(doc, $"{prefix}placeholder"),
            multiline:   multiline,
            maxLines:    maxLines.HasValue ? (int?)(int)maxLines.Value : null);
    }

    private static CheckboxElement BuildCheckboxField(OdinDocument doc, string prefix, BaseFieldProps b)
    {
        return new CheckboxElement(
            name: b.Name, id: b.Id, label: b.Label,
            x: b.X, y: b.Y, w: b.W, h: b.H, bind: b.Bind,
            ariaLabel: b.AriaLabel, tabindex: b.Tabindex, readOnly: b.Readonly,
            required: b.Required, pattern: b.Pattern, minLength: b.MinLength, maxLength: b.MaxLength,
            min: b.Min, max: b.Max,
            @checked: GetBoolean(doc, $"{prefix}checked"));
    }

    private static RadioElement BuildRadioField(OdinDocument doc, string prefix, BaseFieldProps b)
    {
        return new RadioElement(
            name: b.Name, id: b.Id, label: b.Label,
            x: b.X, y: b.Y, w: b.W, h: b.H, bind: b.Bind,
            group: GetString(doc, $"{prefix}group") ?? "",
            value: GetString(doc, $"{prefix}value") ?? "",
            ariaLabel: b.AriaLabel, tabindex: b.Tabindex, readOnly: b.Readonly,
            required: b.Required, pattern: b.Pattern, minLength: b.MinLength, maxLength: b.MaxLength,
            min: b.Min, max: b.Max);
    }

    private static SelectElement BuildSelectField(OdinDocument doc, string prefix, BaseFieldProps b)
    {
        return new SelectElement(
            name: b.Name, id: b.Id, label: b.Label,
            x: b.X, y: b.Y, w: b.W, h: b.H, bind: b.Bind,
            options:     ExtractOptions(doc, prefix),
            ariaLabel:   b.AriaLabel, tabindex: b.Tabindex, readOnly: b.Readonly,
            required: b.Required, pattern: b.Pattern, minLength: b.MinLength, maxLength: b.MaxLength,
            min: b.Min, max: b.Max,
            selected:    GetString(doc, $"{prefix}selected"),
            placeholder: GetString(doc, $"{prefix}placeholder"));
    }

    private static MultiselectElement BuildMultiselectField(OdinDocument doc, string prefix, BaseFieldProps b)
    {
        var minSelect = GetNumber(doc, $"{prefix}minSelect");
        var maxSelect = GetNumber(doc, $"{prefix}maxSelect");

        return new MultiselectElement(
            name: b.Name, id: b.Id, label: b.Label,
            x: b.X, y: b.Y, w: b.W, h: b.H, bind: b.Bind,
            options:   ExtractOptions(doc, prefix),
            ariaLabel: b.AriaLabel, tabindex: b.Tabindex, readOnly: b.Readonly,
            required: b.Required, pattern: b.Pattern, minLength: b.MinLength, maxLength: b.MaxLength,
            min: b.Min, max: b.Max,
            selected:  ExtractFieldArray(doc, prefix, "selected"),
            minSelect: minSelect.HasValue ? (int?)(int)minSelect.Value : null,
            maxSelect: maxSelect.HasValue ? (int?)(int)maxSelect.Value : null);
    }

    private static DateElement BuildDateField(OdinDocument doc, string prefix, BaseFieldProps b)
    {
        return new DateElement(
            name: b.Name, id: b.Id, label: b.Label,
            x: b.X, y: b.Y, w: b.W, h: b.H, bind: b.Bind,
            ariaLabel: b.AriaLabel, tabindex: b.Tabindex, readOnly: b.Readonly,
            required: b.Required, pattern: b.Pattern, minLength: b.MinLength, maxLength: b.MaxLength,
            min: b.Min, max: b.Max,
            value: GetScalarString(doc, $"{prefix}value"));
    }

    private static SignatureElement BuildSignatureField(OdinDocument doc, string prefix, BaseFieldProps b)
    {
        return new SignatureElement(
            name: b.Name, id: b.Id, label: b.Label,
            x: b.X, y: b.Y, w: b.W, h: b.H, bind: b.Bind,
            value: GetBinaryLiteral(doc, $"{prefix}value"),
            dateField: GetString(doc, $"{prefix}date_field"),
            ariaLabel: b.AriaLabel, tabindex: b.Tabindex, readOnly: b.Readonly,
            required: b.Required, pattern: b.Pattern, minLength: b.MinLength, maxLength: b.MaxLength,
            min: b.Min, max: b.Max);
    }

    /// <summary>Extract a field's options array.</summary>
    private static List<string> ExtractOptions(OdinDocument doc, string prefix)
        => ExtractFieldArray(doc, prefix, "options") ?? new List<string>();

    /// <summary>
    /// Extract a field's tabular string array (options / selected). A relative
    /// tabular header may anchor the array under an extra path segment, so search
    /// for it regardless of that segment when the direct location is empty.
    /// </summary>
    private static List<string>? ExtractFieldArray(OdinDocument doc, string prefix, string name)
    {
        var direct = CollectIndexed(doc, $"{prefix}{name}");
        if (direct.Count > 0) return direct;

        var re = new Regex("^" + Regex.Escape(prefix) + @"(?:[^.]+\.)*" + Regex.Escape(name) + @"\[(\d+)\]$");
        var indices = new List<(int Idx, string Path)>();
        foreach (var p in doc.Paths())
        {
            var m = re.Match(p);
            if (m.Success)
                indices.Add((int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), p));
        }
        if (indices.Count == 0) return null;
        indices.Sort((a, b) => a.Idx.CompareTo(b.Idx));
        var outList = new List<string>();
        foreach (var (_, path) in indices)
        {
            var v = GetString(doc, path);
            if (v != null) outList.Add(v);
        }
        return outList;
    }

    /// <summary>Collect a contiguous indexed string array at base[0], base[1], ...</summary>
    private static List<string> CollectIndexed(OdinDocument doc, string base_)
    {
        var outList = new List<string>();
        var i = 0;
        while (doc.Has($"{base_}[{i}]"))
        {
            var v = GetString(doc, $"{base_}[{i}]");
            if (v != null) outList.Add(v);
            i++;
        }
        return outList;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Region Element Builder
    // ─────────────────────────────────────────────────────────────────────────

    private static RegionElement BuildRegionElement(OdinDocument doc, string name, string id, string prefix,
        IReadOnlyDictionary<string, string>? i18n)
    {
        var bindRef  = GetReference(doc, $"{prefix}bind");
        var max      = GetNumber(doc, $"{prefix}max");
        var overflowRef = GetReference(doc, $"{prefix}overflow");
        var overflow = overflowRef != null ? $"@{overflowRef}" : GetString(doc, $"{prefix}overflow");

        var children = ExtractRegionChildren(doc, prefix, i18n);

        return new RegionElement(
            name: name, id: id,
            x: GetNumber(doc, $"{prefix}x") ?? 0,
            y: GetNumber(doc, $"{prefix}y") ?? 0,
            w: GetNumber(doc, $"{prefix}w") ?? 0,
            h: GetNumber(doc, $"{prefix}h") ?? 0,
            children: children,
            bind: bindRef != null ? $"@{bindRef}" : null,
            max: max.HasValue ? (int?)(int)max.Value : null,
            overflow: overflow);
    }

    private static readonly HashSet<string> RegionOwnProps =
        new HashSet<string>(StringComparer.Ordinal) { "x", "y", "w", "h", "bind", "max", "overflow" };
    private static readonly HashSet<string> RegionChildTypes =
        new HashSet<string>(StringComparer.Ordinal) { "text", "field", "img", "barcode" };

    /// <summary>
    /// Collect a region's child elements. Children live under
    /// &lt;regionPrefix&gt;&lt;childType&gt;.&lt;childName&gt;.*. Region own-properties are skipped.
    /// </summary>
    private static List<FormElement> ExtractRegionChildren(OdinDocument doc, string prefix,
        IReadOnlyDictionary<string, string>? i18n)
    {
        var keysSeen    = new HashSet<string>();
        var keysOrdered = new List<string>();
        foreach (var path in doc.Paths())
        {
            if (!path.StartsWith(prefix, StringComparison.Ordinal)) continue;
            var rest  = path.Substring(prefix.Length);
            var parts = rest.Split('.');
            if (parts.Length < 2) continue;
            if (RegionOwnProps.Contains(parts[0])) continue;
            if (!RegionChildTypes.Contains(parts[0])) continue;
            var key = $"{parts[0]}.{parts[1]}";
            if (keysSeen.Add(key))
                keysOrdered.Add(key);
        }

        var children  = new List<FormElement>();
        var idCounter = 0;
        foreach (var key in keysOrdered)
        {
            var dotIdx    = key.IndexOf('.');
            var childType = key.Substring(0, dotIdx);
            var childName = key.Substring(dotIdx + 1);
            var childPrefix = $"{prefix}{key}.";
            var built = BuildElement(doc, childType, childName, childPrefix, idCounter++, i18n);
            if (built == null) continue;
            built.YOffset = GetNumber(doc, $"{childPrefix}y-offset");
            built.XOffset = GetNumber(doc, $"{childPrefix}x-offset");
            children.Add(built);
        }
        return children;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Value Accessors
    // ─────────────────────────────────────────────────────────────────────────

    private static string? GetString(OdinDocument doc, string path)
    {
        var val = doc.Get(path);
        if (val is OdinString s) return s.Value;
        return null;
    }

    /// <summary>
    /// Resolve a string property that may instead be an @$.i18n.* reference.
    /// i18n keys are stored without the $.i18n. prefix.
    /// </summary>
    private static string? GetLabel(OdinDocument doc, string path, IReadOnlyDictionary<string, string>? i18n)
    {
        var val = doc.Get(path);
        if (val == null) return null;
        if (val is OdinString s) return s.Value;
        if (val is OdinReference r)
        {
            const string lead = "$.i18n.";
            if (r.Path.StartsWith(lead, StringComparison.Ordinal))
            {
                var key = r.Path.Substring(lead.Length);
                if (i18n != null && i18n.TryGetValue(key, out var resolved))
                    return resolved;
                return r.Path;
            }
            return r.Path;
        }
        return null;
    }

    /// <summary>
    /// Read a scalar value as a string, preserving the raw source form for dates
    /// and timestamps (e.g. 1900-01-01).
    /// </summary>
    private static string? GetScalarString(OdinDocument doc, string path)
    {
        var val = doc.Get(path);
        return val switch
        {
            OdinString s    => s.Value,
            OdinDate d      => d.Raw,
            OdinTimestamp t => t.Raw,
            _ => null,
        };
    }

    /// <summary>Reconstruct an ODIN binary literal (^algorithm:base64) for a binary value.</summary>
    private static string? GetBinaryLiteral(OdinDocument doc, string path)
    {
        var val = doc.Get(path);
        if (val is OdinBinary bin)
        {
            var base64 = Convert.ToBase64String(bin.Data);
            return bin.Algorithm != null ? $"^{bin.Algorithm}:{base64}" : $"^{base64}";
        }
        if (val is OdinString s) return s.Value;
        return null;
    }

    private static double? GetNumber(OdinDocument doc, string path)
    {
        var val = doc.Get(path);
        if (val == null) return null;
        if (val is OdinNumber or OdinInteger)
            return val.AsDouble();
        return null;
    }

    private static bool? GetBoolean(OdinDocument doc, string path)
    {
        var val = doc.Get(path);
        if (val is OdinBoolean b) return b.Value;
        return null;
    }

    private static string? GetReference(OdinDocument doc, string path)
    {
        var val = doc.Get(path);
        if (val is OdinReference r) return r.Path;
        return null;
    }
}
