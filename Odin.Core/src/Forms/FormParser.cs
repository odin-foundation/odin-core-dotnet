// ODIN Forms 1.0 — Form Parser
//
// Parses a .odin forms document into a typed OdinForm structure.
// Delegates low-level ODIN parsing to Odin.Parse(), then maps the
// resulting flat path space onto the strongly-typed Forms 1.0 schema.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Odin.Core.Types;

namespace Odin.Core.Forms;

/// <summary>
/// Parses ODIN forms documents into typed OdinForm structures.
/// </summary>
public static class FormParser
{
    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Parse an ODIN forms document text into a typed OdinForm.
    /// </summary>
    /// <param name="text">Raw ODIN text from a .odin forms file.</param>
    /// <returns>Parsed OdinForm with metadata, page defaults, and pages.</returns>
    public static OdinForm ParseForm(string text)
    {
        var doc = Odin.Parse(text);

        var metadata    = ExtractMetadata(doc);
        var pageDefaults = ExtractPageDefaults(doc);
        var screen      = ExtractScreen(doc);
        var odincode    = ExtractOdincode(doc);
        var i18n        = ExtractI18n(doc);
        var pages       = ExtractPages(doc);

        return new OdinForm(metadata, pages, pageDefaults, screen, odincode, i18n);
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
        var width  = GetNumber(doc, "$.page.width");
        var height = GetNumber(doc, "$.page.height");
        var unit   = GetString(doc, "$.page.unit");
        var margin = GetNumber(doc, "$.page.margin");

        if (width == null && height == null && unit == null)
            return null;

        var resolvedUnit = unit is "inch" or "cm" or "mm" or "pt" ? unit : "inch";
        return new PageDefaults(width ?? 8.5, height ?? 11.0, resolvedUnit, margin);
    }

    private static ScreenSettings? ExtractScreen(OdinDocument doc)
    {
        var scale = GetNumber(doc, "$.screen.scale");
        return scale.HasValue ? new ScreenSettings(scale.Value) : null;
    }

    private static OdincodeSettings? ExtractOdincode(OdinDocument doc)
    {
        var enabled = GetBoolean(doc, "$.odincode.enabled");
        var zone    = GetString(doc, "$.odincode.zone");

        if (enabled == null && zone == null)
            return null;

        var resolvedZone = zone is "top-center" or "bottom-center" ? zone : "bottom-center";
        return new OdincodeSettings(enabled ?? false, resolvedZone);
    }

    private static Dictionary<string, string>? ExtractI18n(OdinDocument doc)
    {
        // i18n entries are in metadata — check metadata keys
        var result = new Dictionary<string, string>();

        foreach (var path in doc.Paths())
        {
            if (path.StartsWith("i18n.", StringComparison.Ordinal))
            {
                var key = path.Substring("i18n.".Length);
                var val = GetString(doc, path);
                if (!string.IsNullOrEmpty(key) && val != null)
                    result[key] = val;
            }
        }

        // Also check metadata-prefixed paths
        // The metadata section uses {$} header — access via $.i18n.*
        // We need to enumerate metadata entries directly
        foreach (var entry in doc.Metadata)
        {
            var metaPath = entry.Key;
            if (metaPath.StartsWith("i18n.", StringComparison.Ordinal))
            {
                var key = metaPath.Substring("i18n.".Length);
                var val = entry.Value.AsString();
                if (!string.IsNullOrEmpty(key) && val != null)
                    result[key] = val;
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
    private static IReadOnlyList<FormPage> ExtractPages(OdinDocument doc)
    {
        var allPaths = doc.Paths();
        var pageIndices = new HashSet<int>();

        foreach (var path in allPaths)
        {
            var m = PagePathPattern.Match(path);
            if (m.Success)
                pageIndices.Add(int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture));
        }

        if (pageIndices.Count == 0)
            return Array.Empty<FormPage>();

        var sortedIndices = new List<int>(pageIndices);
        sortedIndices.Sort();

        var pages = new List<FormPage>(sortedIndices.Count);
        foreach (var index in sortedIndices)
            pages.Add(ExtractPage(doc, index));

        return pages;
    }

    /// <summary>
    /// Extract a single page by collecting all element keys from paths like
    /// page[N].{elementType}.{elementName}.{property}
    /// </summary>
    private static FormPage ExtractPage(OdinDocument doc, int pageIndex)
    {
        var prefix   = $"page[{pageIndex}].";
        var allPaths = doc.Paths();

        // Collect unique element keys in document order (preserve insertion order)
        // An element key is "{elementType}.{elementName}" e.g. "text.title"
        var elementKeysSeen    = new HashSet<string>();
        var elementKeysOrdered = new List<string>();

        foreach (var path in allPaths)
        {
            if (!path.StartsWith(prefix, StringComparison.Ordinal))
                continue;

            var rest  = path.Substring(prefix.Length); // e.g. "text.title.content"
            var parts = rest.Split('.');
            if (parts.Length >= 2)
            {
                var elementKey = $"{parts[0]}.{parts[1]}"; // "text.title"
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
            var element = BuildElement(doc, elementType, elementName, elementPrefix, idCounter++);
            if (element != null)
                elements.Add(element);
        }

        return new FormPage(elements);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Element Builder Dispatch
    // ─────────────────────────────────────────────────────────────────────────

    private static FormElement? BuildElement(OdinDocument doc, string elementType, string elementName,
        string prefix, int idCounter)
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
            case "text":     return BuildTextElement(doc, elementName, id, prefix);
            case "img":      return BuildImageElement(doc, elementName, id, prefix);
            case "barcode":  return BuildBarcodeElement(doc, elementName, id, prefix);
            case "field":    return BuildFieldElement(doc, elementName, id, prefix);
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

    private static TextElement BuildTextElement(OdinDocument doc, string name, string id, string prefix)
    {
        return new TextElement(
            name: name, id: id,
            content: GetString(doc, $"{prefix}content") ?? "",
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

    private static ImageElement BuildImageElement(OdinDocument doc, string name, string id, string prefix)
    {
        return new ImageElement(
            name: name, id: id,
            src: GetString(doc, $"{prefix}src") ?? "",
            alt: GetString(doc, $"{prefix}alt") ?? "",
            x: GetNumber(doc, $"{prefix}x") ?? 0,
            y: GetNumber(doc, $"{prefix}y") ?? 0,
            w: GetNumber(doc, $"{prefix}w") ?? 0,
            h: GetNumber(doc, $"{prefix}h") ?? 0);
    }

    private static BarcodeElement BuildBarcodeElement(OdinDocument doc, string name, string id, string prefix)
    {
        var barcodeType = GetString(doc, $"{prefix}barcode-type") ?? "code128";
        if (barcodeType is not ("code39" or "code128" or "qr" or "datamatrix" or "pdf417"))
            barcodeType = "code128";

        return new BarcodeElement(
            name: name, id: id,
            barcodeType: barcodeType,
            content: GetString(doc, $"{prefix}content") ?? "",
            alt:     GetString(doc, $"{prefix}alt") ?? "",
            x: GetNumber(doc, $"{prefix}x") ?? 0,
            y: GetNumber(doc, $"{prefix}y") ?? 0,
            w: GetNumber(doc, $"{prefix}w") ?? 0,
            h: GetNumber(doc, $"{prefix}h") ?? 0);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Field Element Builder
    // ─────────────────────────────────────────────────────────────────────────

    private static FormElement? BuildFieldElement(OdinDocument doc, string name, string id, string prefix)
    {
        var fieldType = GetString(doc, $"{prefix}type") ?? "text";
        var base_     = ExtractBaseField(doc, name, id, prefix);

        switch (fieldType)
        {
            case "text":
                return BuildTextField(doc, prefix, base_);
            case "checkbox":
                return BuildCheckboxField(base_);
            case "radio":
                return BuildRadioField(doc, prefix, base_);
            case "select":
                return BuildSelectField(doc, prefix, base_);
            case "multiselect":
                return BuildMultiselectField(doc, prefix, base_);
            case "date":
                return BuildDateField(base_);
            case "signature":
                return BuildSignatureField(doc, prefix, base_);
            default:
                // Unknown field type — return as text field
                return BuildTextField(doc, prefix, base_);
        }
    }

    /// <summary>Shared properties across all field types.</summary>
    private sealed class BaseFieldProps
    {
        public string Name { get; set; } = "";
        public string? Id { get; set; }
        public string Label { get; set; } = "";
        public double X { get; set; }
        public double Y { get; set; }
        public double W { get; set; }
        public double H { get; set; }
        public string Bind { get; set; } = "";
        public string? AriaLabel { get; set; }
        public int? Tabindex { get; set; }
        public bool? Readonly { get; set; }
        public bool? Required { get; set; }
        public string? Pattern { get; set; }
        public int? MinLength { get; set; }
        public int? MaxLength { get; set; }
        public string? Min { get; set; }
        public string? Max { get; set; }
    }

    private static BaseFieldProps ExtractBaseField(OdinDocument doc, string name, string id, string prefix)
    {
        var required  = GetBoolean(doc, $"{prefix}required");
        var tabindex  = GetNumber(doc, $"{prefix}tabindex");
        var minLength = GetNumber(doc, $"{prefix}minLength");
        var maxLength = GetNumber(doc, $"{prefix}maxLength");
        var ariaLabel = GetString(doc, $"{prefix}aria-label");
        var readOnly  = GetBoolean(doc, $"{prefix}readonly");

        // min/max can be number or string
        string? min = null;
        string? max = null;
        var minNum = GetNumber(doc, $"{prefix}min");
        var maxNum = GetNumber(doc, $"{prefix}max");
        if (minNum.HasValue)
            min = minNum.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        else
            min = GetString(doc, $"{prefix}min");
        if (maxNum.HasValue)
            max = maxNum.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        else
            max = GetString(doc, $"{prefix}max");

        // bind is a reference value (@path)
        var bindRef = GetReference(doc, $"{prefix}bind");
        var bind    = bindRef != null ? $"@{bindRef}" : "";

        return new BaseFieldProps
        {
            Name      = name,
            Id        = id,
            Label     = GetString(doc, $"{prefix}label") ?? "",
            X         = GetNumber(doc, $"{prefix}x") ?? 0,
            Y         = GetNumber(doc, $"{prefix}y") ?? 0,
            W         = GetNumber(doc, $"{prefix}w") ?? 0,
            H         = GetNumber(doc, $"{prefix}h") ?? 0,
            Bind      = bind,
            AriaLabel = ariaLabel,
            Tabindex  = tabindex.HasValue ? (int?)((int)tabindex.Value) : null,
            Readonly  = readOnly,
            Required  = required,
            Pattern   = GetString(doc, $"{prefix}pattern"),
            MinLength = minLength.HasValue ? (int?)((int)minLength.Value) : null,
            MaxLength = maxLength.HasValue ? (int?)((int)maxLength.Value) : null,
            Min       = min,
            Max       = max,
        };
    }

    private static TextFieldElement BuildTextField(OdinDocument doc, string prefix, BaseFieldProps b)
    {
        var multiline = GetBoolean(doc, $"{prefix}multiline");
        var maxLines  = GetNumber(doc, $"{prefix}maxLines");

        return new TextFieldElement(
            name: b.Name, id: b.Id, label: b.Label,
            x: b.X, y: b.Y, w: b.W, h: b.H, bind: b.Bind,
            ariaLabel: b.AriaLabel, tabindex: b.Tabindex, readOnly: b.Readonly,
            required: b.Required, pattern: b.Pattern, minLength: b.MinLength, maxLength: b.MaxLength,
            min: b.Min, max: b.Max,
            mask:        GetString(doc, $"{prefix}mask"),
            placeholder: GetString(doc, $"{prefix}placeholder"),
            multiline:   multiline,
            maxLines:    maxLines.HasValue ? (int?)((int)maxLines.Value) : null);
    }

    private static CheckboxElement BuildCheckboxField(BaseFieldProps b)
    {
        return new CheckboxElement(
            name: b.Name, id: b.Id, label: b.Label,
            x: b.X, y: b.Y, w: b.W, h: b.H, bind: b.Bind,
            ariaLabel: b.AriaLabel, tabindex: b.Tabindex, readOnly: b.Readonly,
            required: b.Required, pattern: b.Pattern, minLength: b.MinLength, maxLength: b.MaxLength,
            min: b.Min, max: b.Max);
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
            minSelect: minSelect.HasValue ? (int?)((int)minSelect.Value) : null,
            maxSelect: maxSelect.HasValue ? (int?)((int)maxSelect.Value) : null);
    }

    private static DateElement BuildDateField(BaseFieldProps b)
    {
        return new DateElement(
            name: b.Name, id: b.Id, label: b.Label,
            x: b.X, y: b.Y, w: b.W, h: b.H, bind: b.Bind,
            ariaLabel: b.AriaLabel, tabindex: b.Tabindex, readOnly: b.Readonly,
            required: b.Required, pattern: b.Pattern, minLength: b.MinLength, maxLength: b.MaxLength,
            min: b.Min, max: b.Max);
    }

    private static SignatureElement BuildSignatureField(OdinDocument doc, string prefix, BaseFieldProps b)
    {
        return new SignatureElement(
            name: b.Name, id: b.Id, label: b.Label,
            x: b.X, y: b.Y, w: b.W, h: b.H, bind: b.Bind,
            dateField: GetString(doc, $"{prefix}date_field"),
            ariaLabel: b.AriaLabel, tabindex: b.Tabindex, readOnly: b.Readonly,
            required: b.Required, pattern: b.Pattern, minLength: b.MinLength, maxLength: b.MaxLength,
            min: b.Min, max: b.Max);
    }

    /// <summary>Extract an options array from indexed paths like prefix + "options[0]", "options[1]", ...</summary>
    private static List<string> ExtractOptions(OdinDocument doc, string prefix)
    {
        var options = new List<string>();
        var i = 0;
        while (doc.Has($"{prefix}options[{i}]"))
        {
            var val = GetString(doc, $"{prefix}options[{i}]");
            if (val != null) options.Add(val);
            i++;
        }
        return options;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Value Accessors
    // ─────────────────────────────────────────────────────────────────────────

    private static string? GetString(OdinDocument doc, string path)
    {
        var val = doc.Get(path);
        if (val == null) return null;
        return val.AsString();
    }

    private static double? GetNumber(OdinDocument doc, string path)
    {
        var val = doc.Get(path);
        if (val == null) return null;
        return val.AsDouble();
    }

    private static bool? GetBoolean(OdinDocument doc, string path)
    {
        var val = doc.Get(path);
        if (val == null) return null;
        return val.AsBool();
    }

    private static string? GetReference(OdinDocument doc, string path)
    {
        var val = doc.Get(path);
        if (val == null) return null;
        return val.AsReference();
    }
}
