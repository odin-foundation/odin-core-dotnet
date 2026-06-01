// ODIN Forms — HTML/CSS Renderer
//
// Renders a parsed OdinForm into a complete, accessible HTML string.
// Supports absolute-positioned layout matching print coordinates,
// ARIA attributes, skip navigation, region repetition, overflow
// pagination, and optional data binding.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
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

        // Two-pass: first determine the concrete render plan (pages + overflow),
        // then render with the final total page count so @odin.total_pages resolves.
        var plan = BuildRenderPlan(form, data);
        var totalPages = plan.Count;
        var pageW = FormUnits.ToPixels(form.PageDefaults?.Width ?? 8.5, unit);
        var pageH = FormUnits.ToPixels(form.PageDefaults?.Height ?? 11.0, unit);

        var parts = new List<string>();
        parts.Add("<form role=\"form\" aria-label=\"" + EscapeAttr(title) + "\" class=\"odin-form" + className + "\">");
        parts.Add(SkipLinkHtml(title));
        parts.Add("<style>" + GenerateFormCss() + "\n" + GeneratePrintCss() + "</style>");

        for (var i = 0; i < plan.Count; i++)
        {
            var ctx = new RenderContext
            {
                PageNumber  = i + 1,
                TotalPages  = totalPages,
                Unit        = unit,
                Data        = data,
                PageWidthPx = pageW,
                PageHeightPx = pageH,
            };
            parts.Add(RenderPlannedPage(plan[i], ctx));
        }

        parts.Add("</form>");
        return string.Concat(parts);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Render Plan
    // ─────────────────────────────────────────────────────────────────────────

    private sealed class RenderContext
    {
        public int PageNumber;
        public int TotalPages;
        public string Unit = "inch";
        public OdinDocument? Data;
        public double PageWidthPx;
        public double PageHeightPx;
    }

    private readonly struct ItemSlice
    {
        public readonly int Start;
        public readonly int Count;
        public readonly string Bind;
        public ItemSlice(int start, int count, string bind) { Start = start; Count = count; Bind = bind; }
    }

    private sealed class PlannedPage
    {
        public IReadOnlyList<FormElement> Elements = Array.Empty<FormElement>();
        public Dictionary<string, ItemSlice>? ItemSlices;
    }

    /// <summary>
    /// Build the ordered list of output pages, expanding region overflow when
    /// bound array data is present. Without data, concrete pages render as-is.
    /// </summary>
    private static List<PlannedPage> BuildRenderPlan(OdinForm form, OdinDocument? data)
    {
        var plan = new List<PlannedPage>();

        foreach (var page in form.Pages)
        {
            plan.Add(new PlannedPage { Elements = page.Elements });

            if (data == null) continue;

            foreach (var el in page.Elements)
            {
                if (el is not RegionElement region) continue;
                if (string.IsNullOrEmpty(region.Bind) || region.Max == null || string.IsNullOrEmpty(region.Overflow))
                    continue;
                if (region.Max.Value < 1) continue;
                var count = BoundArrayLength(region.Bind!, data);
                if (count <= region.Max.Value) continue;

                var consumed = region.Max.Value;
                var templateName = region.Overflow!.StartsWith("@", StringComparison.Ordinal)
                    ? region.Overflow.Substring(1)
                    : null;
                var guard = 0;
                while (consumed < count && guard++ < 10000)
                {
                    PageTemplate? tpl = null;
                    if (templateName != null)
                        form.Templates?.TryGetValue(templateName, out tpl);

                    RegionElement? tplRegion = null;
                    if (tpl != null)
                    {
                        foreach (var e in tpl.Elements)
                            if (e is RegionElement re && re.Name == region.Name) { tplRegion = re; break; }
                    }

                    var candidateMax = tplRegion?.Max ?? region.Max.Value;
                    var pageMax = candidateMax >= 1 ? candidateMax : region.Max.Value;

                    var slices = new Dictionary<string, ItemSlice>(StringComparer.Ordinal)
                    {
                        [region.Name] = new ItemSlice(consumed, Math.Min(pageMax, count - consumed), region.Bind!),
                    };
                    var elements = tpl != null ? tpl.Elements : page.Elements;
                    plan.Add(new PlannedPage { Elements = elements, ItemSlices = slices });
                    consumed += pageMax;

                    if (tplRegion?.Overflow != null && tplRegion.Overflow.StartsWith("@", StringComparison.Ordinal))
                        templateName = tplRegion.Overflow.Substring(1);
                }
            }
        }

        return plan;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Page Rendering
    // ─────────────────────────────────────────────────────────────────────────

    private static readonly HashSet<string> FieldTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        "field.text", "field.checkbox", "field.radio", "field.select",
        "field.multiselect", "field.date", "field.signature",
    };

    private static string RenderPlannedPage(PlannedPage page, RenderContext ctx)
    {
        var pageIndex = ctx.PageNumber - 1;
        var parts = new List<string>();
        parts.Add("<div class=\"odin-form-page\" id=\"odin-form-content\" data-page=\"" +
            ctx.PageNumber.ToString(CultureInfo.InvariantCulture) + "\" style=\"width:" +
            Px(ctx.PageWidthPx) + ";height:" + Px(ctx.PageHeightPx) + ";\">");

        // Background images first (lowest z-index), then non-field elements, then fields.
        foreach (var el in page.Elements)
            if (el is ImageElement img && img.Background == true)
                parts.Add(RenderElement(el, pageIndex, ctx, page));

        foreach (var el in page.Elements)
        {
            if (el is ImageElement img && img.Background == true) continue;
            if (!FieldTypes.Contains(el.Type))
                parts.Add(RenderElement(el, pageIndex, ctx, page));
        }

        foreach (var el in TabOrderSort(page.Elements))
            parts.Add(RenderElement(el, pageIndex, ctx, page));

        parts.Add("</div>");
        return string.Concat(parts);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Element Dispatch
    // ─────────────────────────────────────────────────────────────────────────

    private static string RenderElement(FormElement el, int pageIndex, RenderContext ctx, PlannedPage page)
    {
        var unit = ctx.Unit;
        switch (el.Type)
        {
            case "line":              return RenderLine((LineElement)el, unit);
            case "rect":              return RenderRect((RectElement)el, unit);
            case "circle":            return RenderCircle((CircleElement)el, unit);
            case "ellipse":           return RenderEllipse((EllipseElement)el, unit);
            case "polygon":           return RenderPolygon((PolygonElement)el, unit);
            case "polyline":          return RenderPolyline((PolylineElement)el, unit);
            case "path":              return RenderPath((PathElement)el, unit);
            case "text":              return RenderText((TextElement)el, ctx);
            case "img":               return RenderImage((ImageElement)el, ctx);
            case "barcode":           return RenderBarcode((BarcodeElement)el, ctx);
            case "field.text":        return RenderTextField((TextFieldElement)el, pageIndex, ctx);
            case "field.checkbox":    return RenderCheckbox((CheckboxElement)el, pageIndex, ctx);
            case "field.radio":       return RenderRadio((RadioElement)el, pageIndex, ctx);
            case "field.select":      return RenderSelect((SelectElement)el, pageIndex, ctx);
            case "field.multiselect": return RenderMultiselect((MultiselectElement)el, pageIndex, ctx);
            case "field.date":        return RenderDate((DateElement)el, pageIndex, ctx);
            case "field.signature":   return RenderSignature((SignatureElement)el, pageIndex, ctx);
            case "region":            return RenderRegion((RegionElement)el, ctx, page);
            default:                  return "";
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Interpolation
    // ─────────────────────────────────────────────────────────────────────────

    private static readonly Regex InterpolationToken =
        new Regex(@"\{@odin\.([a-z_]+)\}", RegexOptions.Compiled);

    /// <summary>
    /// Resolve {@odin.page} / {@odin.total_pages} tokens in a string. Unknown
    /// {@odin.*} tokens are left untouched.
    /// </summary>
    private static string Interpolate(string text, RenderContext ctx)
    {
        return InterpolationToken.Replace(text, m =>
        {
            switch (m.Groups[1].Value)
            {
                case "page":        return ctx.PageNumber.ToString(CultureInfo.InvariantCulture);
                case "total_pages": return ctx.TotalPages.ToString(CultureInfo.InvariantCulture);
                default:            return m.Value;
            }
        });
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

    private static string RenderText(TextElement el, RenderContext ctx)
    {
        var unit = ctx.Unit;
        var x          = FormUnits.ToPixels(el.X, unit);
        var y          = FormUnits.ToPixels(el.Y, unit);
        var fontSize   = el.FontSize.HasValue ? FormUnits.ToPixels(el.FontSize.Value, "pt") : FormUnits.ToPixels(12, "pt");
        var fontWeight = el.FontWeight ?? "normal";
        var color      = el.Color ?? "#000000";
        var fontFamily = el.FontFamily != null ? "font-family:" + el.FontFamily + ";" : "";
        var fontStyle  = el.FontStyle == "italic" ? "font-style:italic;" : "";
        var textAlign  = el.TextAlign != null ? "text-align:" + el.TextAlign + ";" : "";
        var content    = Interpolate(el.Content, ctx);
        return "<span class=\"odin-form-element\" style=\"position:absolute;left:" + Px(x) + ";top:" + Px(y) + ";font-size:" + Px(fontSize) + ";font-weight:" + fontWeight + ";color:" + color + ";" + fontFamily + fontStyle + textAlign + "\">" + EscapeHtml(content) + "</span>";
    }

    private static string RenderImage(ImageElement el, RenderContext ctx)
    {
        var unit = ctx.Unit;
        var x = FormUnits.ToPixels(el.X, unit);
        var y = FormUnits.ToPixels(el.Y, unit);
        var w = FormUnits.ToPixels(el.W, unit);
        var h = FormUnits.ToPixels(el.H, unit);
        var src = ImageSrcToDataUri(el.Src);
        var alt = Interpolate(el.Alt, ctx);
        var zIndex = el.Background == true ? "z-index:0;" : "";
        return "<img class=\"odin-form-element\" src=\"" + EscapeAttr(src) + "\" alt=\"" + EscapeAttr(alt) + "\" style=\"position:absolute;left:" + Px(x) + ";top:" + Px(y) + ";width:" + Px(w) + ";height:" + Px(h) + ";" + zIndex + "\">";
    }

    private static string RenderBarcode(BarcodeElement el, RenderContext ctx)
    {
        var unit = ctx.Unit;
        var x = FormUnits.ToPixels(el.X, unit);
        var y = FormUnits.ToPixels(el.Y, unit);
        var w = FormUnits.ToPixels(el.W, unit);
        var h = FormUnits.ToPixels(el.H, unit);
        var alt = Interpolate(el.Alt, ctx);
        var content = Interpolate(el.Content, ctx);
        return
            "<div class=\"odin-form-element odin-form-barcode\" role=\"img\" aria-label=\"" + EscapeAttr(alt) + "\" " +
            "data-barcode-type=\"" + EscapeAttr(el.BarcodeType) + "\" data-content=\"" + EscapeAttr(content) + "\" " +
            "style=\"position:absolute;left:" + Px(x) + ";top:" + Px(y) + ";width:" + Px(w) + ";height:" + Px(h) + ";\"></div>";
    }

    /// <summary>
    /// Convert an ODIN binary literal (^png:base64) to a data URI for img.
    /// Passes through values already in data-URI or URL form.
    /// </summary>
    private static string ImageSrcToDataUri(string src)
    {
        if (!src.StartsWith("^", StringComparison.Ordinal)) return src;
        var rest = src.Substring(1);
        var colon = rest.IndexOf(':');
        if (colon < 0) return "data:image/png;base64," + rest;
        var format = rest.Substring(0, colon);
        var b64 = rest.Substring(colon + 1);
        return "data:image/" + format + ";base64," + b64;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Field Elements
    // ─────────────────────────────────────────────────────────────────────────

    private static string RenderTextField(TextFieldElement el, int pageIndex, RenderContext ctx)
    {
        var unit = ctx.Unit;
        var x            = FormUnits.ToPixels(el.X, unit);
        var y            = FormUnits.ToPixels(el.Y, unit);
        var w            = FormUnits.ToPixels(el.W, unit);
        var h            = FormUnits.ToPixels(el.H, unit);
        var (inputId, ariaLabel, ariaRequired) = FieldAriaAttrs(el, pageIndex);
        var value        = el.Value ?? LookupBoundValue(el, ctx.Data);
        var valueAttr    = value != null ? " value=\"" + EscapeAttr(value) + "\"" : "";
        var requiredAttr = el.Required == true ? " required" : "";
        var readonlyAttr = el.Readonly == true ? " readonly" : "";
        var placeholder  = el.Placeholder != null ? " placeholder=\"" + EscapeAttr(el.Placeholder) + "\"" : "";
        var inputType    = el.InputType ?? "text";

        return
            "<div class=\"odin-form-element\" style=\"position:absolute;left:" + Px(x) + ";top:" + Px(y) + ";width:" + Px(w) + ";height:" + Px(h) + ";\">" +
            FieldLabelHtml(Interpolate(el.Label, ctx), inputId) +
            "<input type=\"" + EscapeAttr(inputType) + "\" class=\"odin-form-input\" id=\"" + inputId + "\" aria-label=\"" + EscapeAttr(Interpolate(ariaLabel, ctx)) + "\"" + (ariaRequired ? " aria-required=\"true\"" : "") + valueAttr + requiredAttr + readonlyAttr + placeholder + ">" +
            "</div>";
    }

    private static string RenderCheckbox(CheckboxElement el, int pageIndex, RenderContext ctx)
    {
        var unit = ctx.Unit;
        var x            = FormUnits.ToPixels(el.X, unit);
        var y            = FormUnits.ToPixels(el.Y, unit);
        var w            = FormUnits.ToPixels(el.W, unit);
        var h            = FormUnits.ToPixels(el.H, unit);
        var (inputId, ariaLabel, ariaRequired) = FieldAriaAttrs(el, pageIndex);
        var bound        = LookupBoundValue(el, ctx.Data);
        var isChecked    = el.Checked ?? (bound == "true");
        var checkedAttr  = isChecked ? " checked" : "";

        return
            "<div class=\"odin-form-element\" style=\"position:absolute;left:" + Px(x) + ";top:" + Px(y) + ";width:" + Px(w) + ";height:" + Px(h) + ";\">" +
            FieldLabelHtml(Interpolate(el.Label, ctx), inputId) +
            "<input type=\"checkbox\" class=\"odin-form-checkbox\" id=\"" + inputId + "\" aria-label=\"" + EscapeAttr(Interpolate(ariaLabel, ctx)) + "\"" + (ariaRequired ? " aria-required=\"true\"" : "") + checkedAttr + ">" +
            "</div>";
    }

    private static string RenderRadio(RadioElement el, int pageIndex, RenderContext ctx)
    {
        var unit = ctx.Unit;
        var x            = FormUnits.ToPixels(el.X, unit);
        var y            = FormUnits.ToPixels(el.Y, unit);
        var w            = FormUnits.ToPixels(el.W, unit);
        var h            = FormUnits.ToPixels(el.H, unit);
        var (inputId, ariaLabel, ariaRequired) = FieldAriaAttrs(el, pageIndex);
        var value        = LookupBoundValue(el, ctx.Data);
        var checkedAttr  = value == el.Value ? " checked" : "";

        var radioHtml =
            "<input type=\"radio\" class=\"odin-form-radio\" id=\"" + inputId + "\" name=\"" + EscapeAttr(el.Group) + "\" value=\"" + EscapeAttr(el.Value) + "\" aria-label=\"" + EscapeAttr(Interpolate(ariaLabel, ctx)) + "\"" + (ariaRequired ? " aria-required=\"true\"" : "") + checkedAttr + ">" +
            "<label for=\"" + inputId + "\">" + EscapeHtml(Interpolate(el.Label, ctx)) + "</label>";

        return
            "<div class=\"odin-form-element\" style=\"position:absolute;left:" + Px(x) + ";top:" + Px(y) + ";width:" + Px(w) + ";height:" + Px(h) + ";\">" +
            FieldGroupHtml(el.Group, Interpolate(el.Label, ctx), radioHtml) +
            "</div>";
    }

    private static string RenderSelect(SelectElement el, int pageIndex, RenderContext ctx)
    {
        var unit = ctx.Unit;
        var x            = FormUnits.ToPixels(el.X, unit);
        var y            = FormUnits.ToPixels(el.Y, unit);
        var w            = FormUnits.ToPixels(el.W, unit);
        var h            = FormUnits.ToPixels(el.H, unit);
        var (inputId, ariaLabel, ariaRequired) = FieldAriaAttrs(el, pageIndex);
        var value        = el.Selected ?? LookupBoundValue(el, ctx.Data);

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
            FieldLabelHtml(Interpolate(el.Label, ctx), inputId) +
            "<select class=\"odin-form-select\" id=\"" + inputId + "\" aria-label=\"" + EscapeAttr(Interpolate(ariaLabel, ctx)) + "\"" + (ariaRequired ? " aria-required=\"true\"" : "") + ">" +
            string.Concat(optionParts) +
            "</select>" +
            "</div>";
    }

    private static string RenderMultiselect(MultiselectElement el, int pageIndex, RenderContext ctx)
    {
        var unit = ctx.Unit;
        var x            = FormUnits.ToPixels(el.X, unit);
        var y            = FormUnits.ToPixels(el.Y, unit);
        var w            = FormUnits.ToPixels(el.W, unit);
        var h            = FormUnits.ToPixels(el.H, unit);
        var (inputId, ariaLabel, ariaRequired) = FieldAriaAttrs(el, pageIndex);

        var selectedValues = new HashSet<string>(StringComparer.Ordinal);
        if (el.Selected != null)
        {
            foreach (var sv in el.Selected) selectedValues.Add(sv);
        }
        else
        {
            var value = LookupBoundValue(el, ctx.Data);
            if (!string.IsNullOrEmpty(value))
                foreach (var sv in value!.Split(',')) selectedValues.Add(sv.Trim());
        }

        var optionParts = new List<string>();
        foreach (var opt in el.Options)
        {
            var selected = selectedValues.Contains(opt) ? " selected" : "";
            optionParts.Add("<option value=\"" + EscapeAttr(opt) + "\"" + selected + ">" + EscapeHtml(opt) + "</option>");
        }

        return
            "<div class=\"odin-form-element\" style=\"position:absolute;left:" + Px(x) + ";top:" + Px(y) + ";width:" + Px(w) + ";height:" + Px(h) + ";\">" +
            FieldLabelHtml(Interpolate(el.Label, ctx), inputId) +
            "<select multiple class=\"odin-form-select\" id=\"" + inputId + "\" aria-label=\"" + EscapeAttr(Interpolate(ariaLabel, ctx)) + "\"" + (ariaRequired ? " aria-required=\"true\"" : "") + ">" +
            string.Concat(optionParts) +
            "</select>" +
            "</div>";
    }

    private static string RenderDate(DateElement el, int pageIndex, RenderContext ctx)
    {
        var unit = ctx.Unit;
        var x            = FormUnits.ToPixels(el.X, unit);
        var y            = FormUnits.ToPixels(el.Y, unit);
        var w            = FormUnits.ToPixels(el.W, unit);
        var h            = FormUnits.ToPixels(el.H, unit);
        var (inputId, ariaLabel, ariaRequired) = FieldAriaAttrs(el, pageIndex);
        var value        = el.Value ?? LookupBoundValue(el, ctx.Data);
        var valueAttr    = value != null ? " value=\"" + EscapeAttr(value) + "\"" : "";
        var requiredAttr = el.Required == true ? " required" : "";

        return
            "<div class=\"odin-form-element\" style=\"position:absolute;left:" + Px(x) + ";top:" + Px(y) + ";width:" + Px(w) + ";height:" + Px(h) + ";\">" +
            FieldLabelHtml(Interpolate(el.Label, ctx), inputId) +
            "<input type=\"date\" class=\"odin-form-input\" id=\"" + inputId + "\" aria-label=\"" + EscapeAttr(Interpolate(ariaLabel, ctx)) + "\"" + (ariaRequired ? " aria-required=\"true\"" : "") + valueAttr + requiredAttr + ">" +
            "</div>";
    }

    private static string RenderSignature(SignatureElement el, int pageIndex, RenderContext ctx)
    {
        var unit = ctx.Unit;
        var x            = FormUnits.ToPixels(el.X, unit);
        var y            = FormUnits.ToPixels(el.Y, unit);
        var w            = FormUnits.ToPixels(el.W, unit);
        var h            = FormUnits.ToPixels(el.H, unit);
        var (inputId, ariaLabel, ariaRequired) = FieldAriaAttrs(el, pageIndex);

        return
            "<div class=\"odin-form-element\" style=\"position:absolute;left:" + Px(x) + ";top:" + Px(y) + ";width:" + Px(w) + ";height:" + Px(h) + ";\">" +
            FieldLabelHtml(Interpolate(el.Label, ctx), inputId) +
            "<div class=\"odin-form-signature\" id=\"" + inputId + "\" aria-label=\"" + EscapeAttr(Interpolate(ariaLabel, ctx)) + "\"" + (ariaRequired ? " aria-required=\"true\"" : "") + " role=\"img\" tabindex=\"0\" style=\"width:100%;height:100%;\"></div>" +
            "</div>";
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Region Rendering
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Render a region: repeat its children for each bound array item, positioning
    /// each repetition by y-offset/x-offset relative to the region origin. Without
    /// bound data, the region renders a single empty instance.
    /// </summary>
    private static string RenderRegion(RegionElement el, RenderContext ctx, PlannedPage page)
    {
        var unit = ctx.Unit;
        var regionX = FormUnits.ToPixels(el.X, unit);
        var regionY = FormUnits.ToPixels(el.Y, unit);
        var regionW = FormUnits.ToPixels(el.W, unit);
        var regionH = FormUnits.ToPixels(el.H, unit);

        ItemSlice? slice = null;
        if (page.ItemSlices != null && page.ItemSlices.TryGetValue(el.Name, out var s))
            slice = s;

        var bind = el.Bind ?? slice?.Bind;
        var total = bind != null ? BoundArrayLength(bind, ctx.Data) : 0;
        int start = 0;
        int count;
        if (slice.HasValue)
        {
            start = slice.Value.Start;
            count = slice.Value.Count;
        }
        else if (total > 0)
        {
            count = el.Max.HasValue ? Math.Min(el.Max.Value, total) : total;
        }
        else
        {
            count = 1; // empty layout preview
        }

        var parts = new List<string>();
        parts.Add(
            "<div class=\"odin-form-element odin-form-region\" data-region=\"" + EscapeAttr(el.Name) + "\" " +
            "style=\"position:absolute;left:" + Px(regionX) + ";top:" + Px(regionY) + ";width:" + Px(regionW) + ";height:" + Px(regionH) + ";\">");

        for (var i = 0; i < count; i++)
        {
            var itemIndex = start + i;
            var itemBind = bind != null ? $"{bind}[{itemIndex}]" : null;
            foreach (var child in el.Children)
                parts.Add(RenderRegionChild(child, i, itemBind, ctx));
        }

        parts.Add("</div>");
        return string.Concat(parts);
    }

    /// <summary>
    /// Render one region child for repetition index i. Coordinates are rebased by
    /// the per-item y-offset/x-offset; field children get a unique name and have
    /// their @.field relative binding resolved against the current item.
    /// </summary>
    private static string RenderRegionChild(FormElement child, int i, string? itemBind, RenderContext ctx)
    {
        var yOffset = child.YOffset ?? 0;
        var xOffset = child.XOffset ?? 0;

        if (child is TextElement text)
        {
            var dx = text.X + xOffset * i;
            var dy = text.Y + yOffset * i;
            var rebasedText = new TextElement(text.Name, text.Id, text.Content, dx, dy,
                text.Rotate, text.FontFamily, text.FontSize, text.FontWeight, text.FontStyle, text.TextAlign, text.Color);
            return RenderText(rebasedText, ctx);
        }

        if (child is BaseFieldElement field)
        {
            var dx = field.X + xOffset * i;
            var dy = field.Y + yOffset * i;
            var resolvedBind = ResolveRelativeBind(field.Bind, itemBind) ?? field.Bind;
            var rebased = RebaseField(field, dx, dy, $"{field.Name}_{i}", resolvedBind);
            // Region children render on a synthetic page index so generated IDs are unique.
            var childPageIndex = -1 - i;
            return RenderElement(rebased, childPageIndex, ctx, new PlannedPage());
        }

        return "";
    }

    /// <summary>Rebuild a field with rebased coordinates, a unique name, and a resolved bind.</summary>
    private static FormElement RebaseField(BaseFieldElement f, double x, double y, string name, string bind)
    {
        switch (f)
        {
            case TextFieldElement t:
                return new TextFieldElement(name, t.Id, t.Label, x, y, t.W, t.H, bind,
                    t.AriaLabel, t.Tabindex, t.Readonly, t.Required, t.Pattern, t.MinLength, t.MaxLength,
                    t.Min, t.Max, t.Value, t.InputType, t.Mask, t.Placeholder, t.Multiline, t.MaxLines);
            case CheckboxElement c:
                return new CheckboxElement(name, c.Id, c.Label, x, y, c.W, c.H, bind,
                    c.AriaLabel, c.Tabindex, c.Readonly, c.Required, c.Pattern, c.MinLength, c.MaxLength,
                    c.Min, c.Max, c.Checked);
            case RadioElement r:
                return new RadioElement(name, r.Id, r.Label, x, y, r.W, r.H, bind, r.Group, r.Value,
                    r.AriaLabel, r.Tabindex, r.Readonly, r.Required, r.Pattern, r.MinLength, r.MaxLength, r.Min, r.Max);
            case SelectElement s:
                return new SelectElement(name, s.Id, s.Label, x, y, s.W, s.H, bind, s.Options,
                    s.AriaLabel, s.Tabindex, s.Readonly, s.Required, s.Pattern, s.MinLength, s.MaxLength,
                    s.Min, s.Max, s.Selected, s.Placeholder);
            case MultiselectElement m:
                return new MultiselectElement(name, m.Id, m.Label, x, y, m.W, m.H, bind, m.Options,
                    m.AriaLabel, m.Tabindex, m.Readonly, m.Required, m.Pattern, m.MinLength, m.MaxLength,
                    m.Min, m.Max, m.Selected, m.MinSelect, m.MaxSelect);
            case DateElement d:
                return new DateElement(name, d.Id, d.Label, x, y, d.W, d.H, bind,
                    d.AriaLabel, d.Tabindex, d.Readonly, d.Required, d.Pattern, d.MinLength, d.MaxLength,
                    d.Min, d.Max, d.Value);
            case SignatureElement sig:
                return new SignatureElement(name, sig.Id, sig.Label, x, y, sig.W, sig.H, bind, sig.Value, sig.DateField,
                    sig.AriaLabel, sig.Tabindex, sig.Readonly, sig.Required, sig.Pattern, sig.MinLength, sig.MaxLength, sig.Min, sig.Max);
            default:
                return f;
        }
    }

    /// <summary>Resolve a region child's @.field relative bind against the current item path.</summary>
    private static string? ResolveRelativeBind(string bind, string? itemBind)
    {
        if (string.IsNullOrEmpty(bind)) return null;
        if (bind.StartsWith("@.", StringComparison.Ordinal))
        {
            if (itemBind == null) return null;
            return $"{itemBind}.{bind.Substring(2)}";
        }
        return bind;
    }

    /// <summary>
    /// Number of items in a bound array path. Counts both scalar elements (path[n])
    /// and object elements (path[n].field) in a single path scan.
    /// </summary>
    private static int BoundArrayLength(string bind, OdinDocument? data)
    {
        if (data == null) return 0;
        var path = bind.StartsWith("@", StringComparison.Ordinal) ? bind.Substring(1) : bind;
        var re = new Regex("^" + Regex.Escape(path) + @"\[(\d+)\](?:\.|$)");
        var max = -1;
        foreach (var p in data.Paths())
        {
            var m = re.Match(p);
            if (m.Success)
            {
                var idx = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                if (idx > max) max = idx;
            }
        }
        return max + 1;
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
        return val switch
        {
            OdinString s  => s.Value,
            OdinNumber n  => n.Value.ToString(CultureInfo.InvariantCulture),
            OdinInteger i => i.Value.ToString(CultureInfo.InvariantCulture),
            OdinBoolean b => b.Value ? "true" : "false",
            _ => null,
        };
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
        _ = groupName;
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
            if (el is BaseFieldElement)
                fields.Add(el);
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
            .Replace("'", "&#39;")
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
