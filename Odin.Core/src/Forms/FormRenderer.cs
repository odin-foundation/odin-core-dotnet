// ODIN Forms — HTML/CSS Renderer
//
// Renders a parsed OdinForm into a complete, accessible HTML string.
// Supports absolute-positioned layout matching print coordinates,
// ARIA attributes, skip navigation, and optional data binding.

using System;
using System.Collections.Generic;
using System.Globalization;
using Odin.Core.Types;

namespace Odin.Core.Forms;

/// <summary>
/// Renders an OdinForm to a complete HTML string.
/// </summary>
public static class FormRenderer
{
    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Render an OdinForm to a complete HTML string.
    /// </summary>
    /// <param name="form">Parsed OdinForm structure.</param>
    /// <param name="data">Optional ODIN document for data binding (populates field values).</param>
    /// <param name="options">Optional rendering options (target, scale, className, lang).</param>
    /// <returns>Complete HTML string including form, style, and all elements.</returns>
    public static string RenderForm(OdinForm form, OdinDocument? data = null, RenderFormOptions? options = null)
    {
        var title     = !string.IsNullOrEmpty(form.Metadata.Title) ? form.Metadata.Title : "ODIN Form";
        var className = options?.ClassName != null ? " " + options.ClassName : "";
        var unit      = form.PageDefaults?.Unit ?? "inch";

        var parts = new List<string>();

        // Wrapper
        parts.Add("<form role=\"form\" aria-label=\"" + EscapeAttr(title) + "\" class=\"odin-form" + className + "\">");

        // Skip link
        parts.Add(SkipLinkHtml(title));

        // Style tag
        parts.Add("<style>" + GenerateFormCss() + "\n" + GeneratePrintCss() + "</style>");

        // Pages
        for (var pageIndex = 0; pageIndex < form.Pages.Count; pageIndex++)
            parts.Add(RenderPage(form.Pages[pageIndex], pageIndex, unit, form, data));

        parts.Add("</form>");

        return string.Concat(parts);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Page Rendering
    // ─────────────────────────────────────────────────────────────────────────

    private static string RenderPage(FormPage page, int pageIndex, string unit, OdinForm form, OdinDocument? data)
    {
        var w = FormUnits.ToPixels(form.PageDefaults?.Width ?? 8.5, unit);
        var h = FormUnits.ToPixels(form.PageDefaults?.Height ?? 11.0, unit);

        var parts = new List<string>();
        parts.Add("<div class=\"odin-form-page\" id=\"odin-form-content\" style=\"width:" + Px(w) + ";height:" + Px(h) + ";\">");

        // Render non-field elements in document order
        var fieldTypes = new HashSet<string>(StringComparer.Ordinal)
        {
            "field.text", "field.checkbox", "field.radio", "field.select",
            "field.multiselect", "field.date", "field.signature",
        };

        foreach (var el in page.Elements)
        {
            if (!fieldTypes.Contains(el.Type))
                parts.Add(RenderElement(el, pageIndex, unit, data));
        }

        // Render field elements sorted by tab order (top-to-bottom, left-to-right)
        var sortedFields = TabOrderSort(page.Elements);
        foreach (var el in sortedFields)
            parts.Add(RenderElement(el, pageIndex, unit, data));

        parts.Add("</div>");
        return string.Concat(parts);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Element Dispatch
    // ─────────────────────────────────────────────────────────────────────────

    private static string RenderElement(FormElement el, int pageIndex, string unit, OdinDocument? data)
    {
        switch (el.Type)
        {
            case "line":              return RenderLine((LineElement)el, unit);
            case "rect":              return RenderRect((RectElement)el, unit);
            case "circle":            return RenderCircle((CircleElement)el, unit);
            case "ellipse":           return RenderEllipse((EllipseElement)el, unit);
            case "polygon":           return RenderPolygon((PolygonElement)el, unit);
            case "polyline":          return RenderPolyline((PolylineElement)el, unit);
            case "path":              return RenderPath((PathElement)el, unit);
            case "text":              return RenderText((TextElement)el, unit);
            case "img":               return RenderImage((ImageElement)el, unit);
            case "field.text":        return RenderTextField((TextFieldElement)el, pageIndex, unit, data);
            case "field.checkbox":    return RenderCheckbox((CheckboxElement)el, pageIndex, unit, data);
            case "field.radio":       return RenderRadio((RadioElement)el, pageIndex, unit, data);
            case "field.select":      return RenderSelect((SelectElement)el, pageIndex, unit, data);
            case "field.multiselect": return RenderMultiselect((MultiselectElement)el, pageIndex, unit, data);
            case "field.date":        return RenderDate((DateElement)el, pageIndex, unit, data);
            case "field.signature":   return RenderSignature((SignatureElement)el, pageIndex, unit);
            default:                  return "";
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Geometric Elements
    // ─────────────────────────────────────────────────────────────────────────

    private static string RenderLine(LineElement el, string unit)
    {
        var x1 = FormUnits.ToPixels(el.X1, unit);
        var y1 = FormUnits.ToPixels(el.Y1, unit);
        var x2 = FormUnits.ToPixels(el.X2, unit);
        var y2 = FormUnits.ToPixels(el.Y2, unit);
        var stroke      = el.Stroke ?? "#000000";
        var strokeWidth = el.StrokeWidth.HasValue ? FormUnits.ToPixels(el.StrokeWidth.Value, unit) : 1.0;
        return
            "<svg class=\"odin-form-element\" style=\"position:absolute;left:0;top:0;width:100%;height:100%;overflow:visible;\">" +
            "<line x1=\"" + F(x1) + "\" y1=\"" + F(y1) + "\" x2=\"" + F(x2) + "\" y2=\"" + F(y2) + "\" stroke=\"" + stroke + "\" stroke-width=\"" + F(strokeWidth) + "\"/>" +
            "</svg>";
    }

    private static string RenderRect(RectElement el, string unit)
    {
        var x  = FormUnits.ToPixels(el.X, unit);
        var y  = FormUnits.ToPixels(el.Y, unit);
        var w  = FormUnits.ToPixels(el.W, unit);
        var h  = FormUnits.ToPixels(el.H, unit);
        var border = el.Stroke != null
            ? "border:" + F(el.StrokeWidth.HasValue ? FormUnits.ToPixels(el.StrokeWidth.Value, unit) : 1.0) + "px solid " + el.Stroke + ";"
            : "";
        var bg     = el.Fill != null && el.Fill != "none" ? "background:" + el.Fill + ";" : "";
        var rx     = el.Rx.HasValue ? FormUnits.ToPixels(el.Rx.Value, unit) : 0.0;
        var ry     = el.Ry.HasValue ? FormUnits.ToPixels(el.Ry.Value, unit) : 0.0;
        var radius = (rx != 0 || ry != 0) ? "border-radius:" + F(rx) + "px " + F(ry) + "px;" : "";
        return "<div class=\"odin-form-element\" style=\"position:absolute;left:" + Px(x) + ";top:" + Px(y) + ";width:" + Px(w) + ";height:" + Px(h) + ";" + border + bg + radius + "\"></div>";
    }

    private static string RenderCircle(CircleElement el, string unit)
    {
        var cx = FormUnits.ToPixels(el.Cx, unit);
        var cy = FormUnits.ToPixels(el.Cy, unit);
        var r  = FormUnits.ToPixels(el.R, unit);
        var stroke      = el.Stroke ?? "#000000";
        var strokeWidth = el.StrokeWidth.HasValue ? FormUnits.ToPixels(el.StrokeWidth.Value, unit) : 1.0;
        var fill        = el.Fill ?? "none";
        return
            "<svg class=\"odin-form-element\" style=\"position:absolute;left:0;top:0;width:100%;height:100%;overflow:visible;\">" +
            "<circle cx=\"" + F(cx) + "\" cy=\"" + F(cy) + "\" r=\"" + F(r) + "\" stroke=\"" + stroke + "\" stroke-width=\"" + F(strokeWidth) + "\" fill=\"" + fill + "\"/>" +
            "</svg>";
    }

    private static string RenderEllipse(EllipseElement el, string unit)
    {
        var cx = FormUnits.ToPixels(el.Cx, unit);
        var cy = FormUnits.ToPixels(el.Cy, unit);
        var rx = FormUnits.ToPixels(el.Rx, unit);
        var ry = FormUnits.ToPixels(el.Ry, unit);
        var stroke      = el.Stroke ?? "#000000";
        var strokeWidth = el.StrokeWidth.HasValue ? FormUnits.ToPixels(el.StrokeWidth.Value, unit) : 1.0;
        var fill        = el.Fill ?? "none";
        return
            "<svg class=\"odin-form-element\" style=\"position:absolute;left:0;top:0;width:100%;height:100%;overflow:visible;\">" +
            "<ellipse cx=\"" + F(cx) + "\" cy=\"" + F(cy) + "\" rx=\"" + F(rx) + "\" ry=\"" + F(ry) + "\" stroke=\"" + stroke + "\" stroke-width=\"" + F(strokeWidth) + "\" fill=\"" + fill + "\"/>" +
            "</svg>";
    }

    private static string RenderPolygon(PolygonElement el, string unit)
    {
        var points      = ConvertPoints(el.Points, unit);
        var stroke      = el.Stroke ?? "#000000";
        var strokeWidth = el.StrokeWidth.HasValue ? FormUnits.ToPixels(el.StrokeWidth.Value, unit) : 1.0;
        var fill        = el.Fill ?? "none";
        return
            "<svg class=\"odin-form-element\" style=\"position:absolute;left:0;top:0;width:100%;height:100%;overflow:visible;\">" +
            "<polygon points=\"" + points + "\" stroke=\"" + stroke + "\" stroke-width=\"" + F(strokeWidth) + "\" fill=\"" + fill + "\"/>" +
            "</svg>";
    }

    private static string RenderPolyline(PolylineElement el, string unit)
    {
        var points      = ConvertPoints(el.Points, unit);
        var stroke      = el.Stroke ?? "#000000";
        var strokeWidth = el.StrokeWidth.HasValue ? FormUnits.ToPixels(el.StrokeWidth.Value, unit) : 1.0;
        return
            "<svg class=\"odin-form-element\" style=\"position:absolute;left:0;top:0;width:100%;height:100%;overflow:visible;\">" +
            "<polyline points=\"" + points + "\" stroke=\"" + stroke + "\" stroke-width=\"" + F(strokeWidth) + "\" fill=\"none\"/>" +
            "</svg>";
    }

    private static string RenderPath(PathElement el, string unit)
    {
        var stroke      = el.Stroke ?? "#000000";
        var strokeWidth = el.StrokeWidth.HasValue ? FormUnits.ToPixels(el.StrokeWidth.Value, unit) : 1.0;
        var fill        = el.Fill ?? "none";
        return
            "<svg class=\"odin-form-element\" style=\"position:absolute;left:0;top:0;width:100%;height:100%;overflow:visible;\">" +
            "<path d=\"" + el.D + "\" stroke=\"" + stroke + "\" stroke-width=\"" + F(strokeWidth) + "\" fill=\"" + fill + "\"/>" +
            "</svg>";
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Content Elements
    // ─────────────────────────────────────────────────────────────────────────

    private static string RenderText(TextElement el, string unit)
    {
        var x          = FormUnits.ToPixels(el.X, unit);
        var y          = FormUnits.ToPixels(el.Y, unit);
        var fontSize   = el.FontSize.HasValue ? FormUnits.ToPixels(el.FontSize.Value, "pt") : FormUnits.ToPixels(12, "pt");
        var fontWeight = el.FontWeight ?? "normal";
        var color      = el.Color ?? "#000000";
        var fontFamily = el.FontFamily != null ? "font-family:" + el.FontFamily + ";" : "";
        var fontStyle  = el.FontStyle == "italic" ? "font-style:italic;" : "";
        var textAlign  = el.TextAlign != null ? "text-align:" + el.TextAlign + ";" : "";
        return "<span class=\"odin-form-element\" style=\"position:absolute;left:" + Px(x) + ";top:" + Px(y) + ";font-size:" + Px(fontSize) + ";font-weight:" + fontWeight + ";color:" + color + ";" + fontFamily + fontStyle + textAlign + "\">" + EscapeHtml(el.Content) + "</span>";
    }

    private static string RenderImage(ImageElement el, string unit)
    {
        var x = FormUnits.ToPixels(el.X, unit);
        var y = FormUnits.ToPixels(el.Y, unit);
        var w = FormUnits.ToPixels(el.W, unit);
        var h = FormUnits.ToPixels(el.H, unit);
        return "<img class=\"odin-form-element\" src=\"" + EscapeAttr(el.Src) + "\" alt=\"" + EscapeAttr(el.Alt) + "\" style=\"position:absolute;left:" + Px(x) + ";top:" + Px(y) + ";width:" + Px(w) + ";height:" + Px(h) + ";\">";
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Field Elements
    // ─────────────────────────────────────────────────────────────────────────

    private static string RenderTextField(TextFieldElement el, int pageIndex, string unit, OdinDocument? data)
    {
        var x            = FormUnits.ToPixels(el.X, unit);
        var y            = FormUnits.ToPixels(el.Y, unit);
        var w            = FormUnits.ToPixels(el.W, unit);
        var h            = FormUnits.ToPixels(el.H, unit);
        var (inputId, ariaLabel, ariaRequired) = FieldAriaAttrs(el, pageIndex);
        var value        = LookupBoundValue(el, data);
        var valueAttr    = value != null ? " value=\"" + EscapeAttr(value) + "\"" : "";
        var requiredAttr = el.Required == true ? " required" : "";
        var readonlyAttr = el.Readonly == true ? " readonly" : "";
        var placeholder  = el.Placeholder != null ? " placeholder=\"" + EscapeAttr(el.Placeholder) + "\"" : "";

        return
            "<div class=\"odin-form-element\" style=\"position:absolute;left:" + Px(x) + ";top:" + Px(y) + ";width:" + Px(w) + ";height:" + Px(h) + ";\">" +
            FieldLabelHtml(el.Label, inputId) +
            "<input type=\"text\" class=\"odin-form-input\" id=\"" + inputId + "\" aria-label=\"" + EscapeAttr(ariaLabel) + "\"" + (ariaRequired ? " aria-required=\"true\"" : "") + valueAttr + requiredAttr + readonlyAttr + placeholder + ">" +
            "</div>";
    }

    private static string RenderCheckbox(CheckboxElement el, int pageIndex, string unit, OdinDocument? data)
    {
        var x            = FormUnits.ToPixels(el.X, unit);
        var y            = FormUnits.ToPixels(el.Y, unit);
        var w            = FormUnits.ToPixels(el.W, unit);
        var h            = FormUnits.ToPixels(el.H, unit);
        var (inputId, ariaLabel, ariaRequired) = FieldAriaAttrs(el, pageIndex);
        var value        = LookupBoundValue(el, data);
        var checkedAttr  = value == "true" ? " checked" : "";

        return
            "<div class=\"odin-form-element\" style=\"position:absolute;left:" + Px(x) + ";top:" + Px(y) + ";width:" + Px(w) + ";height:" + Px(h) + ";\">" +
            FieldLabelHtml(el.Label, inputId) +
            "<input type=\"checkbox\" class=\"odin-form-checkbox\" id=\"" + inputId + "\" aria-label=\"" + EscapeAttr(ariaLabel) + "\"" + (ariaRequired ? " aria-required=\"true\"" : "") + checkedAttr + ">" +
            "</div>";
    }

    private static string RenderRadio(RadioElement el, int pageIndex, string unit, OdinDocument? data)
    {
        var x            = FormUnits.ToPixels(el.X, unit);
        var y            = FormUnits.ToPixels(el.Y, unit);
        var w            = FormUnits.ToPixels(el.W, unit);
        var h            = FormUnits.ToPixels(el.H, unit);
        var (inputId, ariaLabel, ariaRequired) = FieldAriaAttrs(el, pageIndex);
        var value        = LookupBoundValue(el, data);
        var checkedAttr  = value == el.Value ? " checked" : "";

        var radioHtml =
            "<input type=\"radio\" class=\"odin-form-radio\" id=\"" + inputId + "\" name=\"" + EscapeAttr(el.Group) + "\" value=\"" + EscapeAttr(el.Value) + "\" aria-label=\"" + EscapeAttr(ariaLabel) + "\"" + (ariaRequired ? " aria-required=\"true\"" : "") + checkedAttr + ">" +
            "<label for=\"" + inputId + "\">" + EscapeHtml(el.Label) + "</label>";

        return
            "<div class=\"odin-form-element\" style=\"position:absolute;left:" + Px(x) + ";top:" + Px(y) + ";width:" + Px(w) + ";height:" + Px(h) + ";\">" +
            FieldGroupHtml(el.Group, el.Label, radioHtml) +
            "</div>";
    }

    private static string RenderSelect(SelectElement el, int pageIndex, string unit, OdinDocument? data)
    {
        var x            = FormUnits.ToPixels(el.X, unit);
        var y            = FormUnits.ToPixels(el.Y, unit);
        var w            = FormUnits.ToPixels(el.W, unit);
        var h            = FormUnits.ToPixels(el.H, unit);
        var (inputId, ariaLabel, ariaRequired) = FieldAriaAttrs(el, pageIndex);
        var value        = LookupBoundValue(el, data);

        var optionParts = new List<string>();
        if (el.Placeholder != null)
            optionParts.Add("<option value=\"\">" + EscapeHtml(el.Placeholder) + "</option>");
        foreach (var opt in el.Options)
        {
            var selected = value == opt ? " selected" : "";
            optionParts.Add("<option value=\"" + EscapeAttr(opt) + "\"" + selected + ">" + EscapeHtml(opt) + "</option>");
        }

        return
            "<div class=\"odin-form-element\" style=\"position:absolute;left:" + Px(x) + ";top:" + Px(y) + ";width:" + Px(w) + ";height:" + Px(h) + ";\">" +
            FieldLabelHtml(el.Label, inputId) +
            "<select class=\"odin-form-select\" id=\"" + inputId + "\" aria-label=\"" + EscapeAttr(ariaLabel) + "\"" + (ariaRequired ? " aria-required=\"true\"" : "") + ">" +
            string.Concat(optionParts) +
            "</select>" +
            "</div>";
    }

    private static string RenderMultiselect(MultiselectElement el, int pageIndex, string unit, OdinDocument? data)
    {
        var x            = FormUnits.ToPixels(el.X, unit);
        var y            = FormUnits.ToPixels(el.Y, unit);
        var w            = FormUnits.ToPixels(el.W, unit);
        var h            = FormUnits.ToPixels(el.H, unit);
        var (inputId, ariaLabel, ariaRequired) = FieldAriaAttrs(el, pageIndex);
        var value        = LookupBoundValue(el, data);

        var trimmedSelectedValues = new HashSet<string>(StringComparer.Ordinal);
        if (value != null)
        {
            foreach (var sv in value.Split(','))
                trimmedSelectedValues.Add(sv.Trim());
        }

        var optionParts = new List<string>();
        foreach (var opt in el.Options)
        {
            var selected = trimmedSelectedValues.Contains(opt) ? " selected" : "";
            optionParts.Add("<option value=\"" + EscapeAttr(opt) + "\"" + selected + ">" + EscapeHtml(opt) + "</option>");
        }

        return
            "<div class=\"odin-form-element\" style=\"position:absolute;left:" + Px(x) + ";top:" + Px(y) + ";width:" + Px(w) + ";height:" + Px(h) + ";\">" +
            FieldLabelHtml(el.Label, inputId) +
            "<select multiple class=\"odin-form-select\" id=\"" + inputId + "\" aria-label=\"" + EscapeAttr(ariaLabel) + "\"" + (ariaRequired ? " aria-required=\"true\"" : "") + ">" +
            string.Concat(optionParts) +
            "</select>" +
            "</div>";
    }

    private static string RenderDate(DateElement el, int pageIndex, string unit, OdinDocument? data)
    {
        var x            = FormUnits.ToPixels(el.X, unit);
        var y            = FormUnits.ToPixels(el.Y, unit);
        var w            = FormUnits.ToPixels(el.W, unit);
        var h            = FormUnits.ToPixels(el.H, unit);
        var (inputId, ariaLabel, ariaRequired) = FieldAriaAttrs(el, pageIndex);
        var value        = LookupBoundValue(el, data);
        var valueAttr    = value != null ? " value=\"" + EscapeAttr(value) + "\"" : "";
        var requiredAttr = el.Required == true ? " required" : "";

        return
            "<div class=\"odin-form-element\" style=\"position:absolute;left:" + Px(x) + ";top:" + Px(y) + ";width:" + Px(w) + ";height:" + Px(h) + ";\">" +
            FieldLabelHtml(el.Label, inputId) +
            "<input type=\"date\" class=\"odin-form-input\" id=\"" + inputId + "\" aria-label=\"" + EscapeAttr(ariaLabel) + "\"" + (ariaRequired ? " aria-required=\"true\"" : "") + valueAttr + requiredAttr + ">" +
            "</div>";
    }

    private static string RenderSignature(SignatureElement el, int pageIndex, string unit)
    {
        var x            = FormUnits.ToPixels(el.X, unit);
        var y            = FormUnits.ToPixels(el.Y, unit);
        var w            = FormUnits.ToPixels(el.W, unit);
        var h            = FormUnits.ToPixels(el.H, unit);
        var (inputId, ariaLabel, ariaRequired) = FieldAriaAttrs(el, pageIndex);

        return
            "<div class=\"odin-form-element\" style=\"position:absolute;left:" + Px(x) + ";top:" + Px(y) + ";width:" + Px(w) + ";height:" + Px(h) + ";\">" +
            FieldLabelHtml(el.Label, inputId) +
            "<div class=\"odin-form-signature\" id=\"" + inputId + "\" aria-label=\"" + EscapeAttr(ariaLabel) + "\"" + (ariaRequired ? " aria-required=\"true\"" : "") + " role=\"img\" tabindex=\"0\" style=\"width:100%;height:100%;\"></div>" +
            "</div>";
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Data Binding
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Looks up a field's bound value in the data document.
    /// The bind property uses "@path.to.value" syntax.
    /// </summary>
    private static string? LookupBoundValue(BaseFieldElement el, OdinDocument? data)
    {
        if (data == null || string.IsNullOrEmpty(el.Bind))
            return null;

        var path = el.Bind.StartsWith("@", StringComparison.Ordinal) ? el.Bind.Substring(1) : el.Bind;
        if (string.IsNullOrEmpty(path))
            return null;

        var val = data.Get(path);
        if (val == null) return null;

        var str = val.AsString();
        if (str != null) return str;

        var dbl = val.AsDouble();
        if (dbl.HasValue) return dbl.Value.ToString(CultureInfo.InvariantCulture);

        var bl = val.AsBool();
        if (bl.HasValue) return bl.Value ? "true" : "false";

        return null;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Accessibility Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static string GenerateFieldId(string elementName, int pageIndex)
        => "odin-field-" + pageIndex.ToString(CultureInfo.InvariantCulture) + "-" + elementName;

    private static string FieldLabelHtml(string label, string inputId)
        => "<label for=\"" + inputId + "\" class=\"odin-form-label\">" + label + "</label>";

    private static (string inputId, string ariaLabel, bool ariaRequired) FieldAriaAttrs(BaseFieldElement el, int pageIndex)
    {
        var inputId      = GenerateFieldId(el.Name, pageIndex);
        var ariaLabel    = el.AriaLabel ?? el.Label;
        var ariaRequired = el.Required == true;
        return (inputId, ariaLabel, ariaRequired);
    }

    private static string FieldGroupHtml(string groupName, string legend, string content)
    {
        _ = groupName; // accepted for API symmetry
        return
            "<fieldset class=\"odin-form-fieldset\">" +
            "<legend class=\"odin-form-legend\">" + legend + "</legend>" +
            content +
            "</fieldset>";
    }

    private static string SkipLinkHtml(string formTitle)
        => "<a class=\"odin-form-sr-only odin-form-skip\" href=\"#odin-form-content\">Skip to " + formTitle + "</a>";

    /// <summary>
    /// Returns only the field elements from elements, sorted by natural reading
    /// order: top-to-bottom (ascending Y), then left-to-right (ascending X).
    /// </summary>
    private static List<FormElement> TabOrderSort(IReadOnlyList<FormElement> elements)
    {
        var fields = new List<FormElement>();
        foreach (var el in elements)
        {
            if (el is BaseFieldElement)
                fields.Add(el);
        }
        fields.Sort((a, b) =>
        {
            var fa = (BaseFieldElement)a;
            var fb = (BaseFieldElement)b;
            if (fa.Y != fb.Y) return fa.Y.CompareTo(fb.Y);
            return fa.X.CompareTo(fb.X);
        });
        return fields;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // CSS Generation
    // ─────────────────────────────────────────────────────────────────────────

    private static string GenerateFormCss()
    {
        return string.Join("\n", new[]
        {
            ".odin-form { position: relative; font-family: Helvetica, Arial, sans-serif; }",
            ".odin-form-page { position: relative; background: white; overflow: hidden; box-sizing: border-box; margin: 0 auto; }",
            ".odin-form-element { position: absolute; box-sizing: border-box; }",
            ".odin-form-label { display: block; font-size: 8pt; color: #666; margin-bottom: 1px; }",
            ".odin-form-input { width: 100%; height: 100%; box-sizing: border-box; border: 1px solid #999; padding: 2px 4px; font-family: inherit; font-size: inherit; background: transparent; }",
            ".odin-form-input:focus { outline: 2px solid #34A3F5; border-color: #34A3F5; }",
            ".odin-form-checkbox, .odin-form-radio { width: auto; height: auto; }",
            ".odin-form-select { width: 100%; height: 100%; }",
            ".odin-form-signature { border: none; border-bottom: 1px solid #000; background: transparent; }",
            ".odin-form-fieldset { border: none; padding: 0; margin: 0; position: absolute; }",
            ".odin-form-legend { font-size: 8pt; color: #666; }",
            ".odin-form-sr-only { position: absolute; width: 1px; height: 1px; padding: 0; margin: -1px; overflow: hidden; clip: rect(0,0,0,0); white-space: nowrap; border: 0; }",
            ".odin-form-skip:focus { position: static; width: auto; height: auto; clip: auto; overflow: visible; margin: 0; padding: 4px 8px; }",
        });
    }

    private static string GeneratePrintCss()
    {
        return string.Join("\n", new[]
        {
            "@media print {",
            "  .odin-form-page { page-break-after: always; margin: 0; box-shadow: none; }",
            "  .odin-form-page:last-child { page-break-after: auto; }",
            "  .odin-form-input { border: none; border-bottom: 1px solid #000; background: transparent; }",
            "  .odin-form-skip { display: none; }",
            "  .odin-form-sr-only { display: none; }",
            "}",
        });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Utility Functions
    // ─────────────────────────────────────────────────────────────────────────

    private static string EscapeHtml(string str)
    {
        return str
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;");
    }

    private static string EscapeAttr(string str)
    {
        return str
            .Replace("&", "&amp;")
            .Replace("\"", "&quot;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
    }

    /// <summary>
    /// Converts an SVG points string (space-separated x,y pairs in page units) to pixel values.
    /// </summary>
    private static string ConvertPoints(string points, string unit)
    {
        var pairs  = points.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        var result = new string[pairs.Length];
        for (var i = 0; i < pairs.Length; i++)
        {
            var pair  = pairs[i];
            var comma = pair.IndexOf(',');
            if (comma < 0)
            {
                result[i] = pair;
                continue;
            }
            var xs = pair.Substring(0, comma);
            var ys = pair.Substring(comma + 1);
            if (double.TryParse(xs, NumberStyles.Float, CultureInfo.InvariantCulture, out var xv) &&
                double.TryParse(ys, NumberStyles.Float, CultureInfo.InvariantCulture, out var yv))
            {
                result[i] = F(FormUnits.ToPixels(xv, unit)) + "," + F(FormUnits.ToPixels(yv, unit));
            }
            else
            {
                result[i] = pair;
            }
        }
        return string.Join(" ", result);
    }

    /// <summary>Format a double as a pixel string (e.g. "96px").</summary>
    private static string Px(double v) => F(v) + "px";

    /// <summary>Format a double with up to 3 decimal places, no trailing zeros.</summary>
    private static string F(double v) => v.ToString("G", CultureInfo.InvariantCulture);
}
