// ODIN Forms 1.0 — C# Type Definitions
//
// Declarative form definition types for print and screen rendering.
// Matches the ODIN Forms 1.0 Schema specification exactly.
//
// Design: print-first, absolute positioning, bidirectional data binding.

using System.Collections.Generic;

namespace Odin.Core.Forms;

// ─────────────────────────────────────────────────────────────────────────────
// Root Document
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Root ODIN Forms document.
///
/// Corresponds to the top-level structure of a .odin forms file, including
/// the {$} metadata section, optional settings sections, and page[n] pages.
/// </summary>
public sealed class OdinForm
{
    /// <summary>Document-level metadata ({$}).</summary>
    public FormMetadata Metadata { get; }

    /// <summary>Default page dimensions and margins ({$.page}). Optional.</summary>
    public PageDefaults? PageDefaults { get; }

    /// <summary>Screen rendering options ({$.screen}). Optional.</summary>
    public ScreenSettings? Screen { get; }

    /// <summary>Self-digitizing barcode settings ({$.odincode}). Optional.</summary>
    public OdincodeSettings? Odincode { get; }

    /// <summary>Multi-language label dictionary ({$.i18n}). Optional.</summary>
    public IReadOnlyDictionary<string, string>? I18n { get; }

    /// <summary>Ordered list of form pages (page[0], page[1], ...).</summary>
    public IReadOnlyList<FormPage> Pages { get; }

    /// <summary>Creates an OdinForm with all components.</summary>
    public OdinForm(
        FormMetadata metadata,
        IReadOnlyList<FormPage> pages,
        PageDefaults? pageDefaults = null,
        ScreenSettings? screen = null,
        OdincodeSettings? odincode = null,
        IReadOnlyDictionary<string, string>? i18n = null)
    {
        Metadata = metadata;
        Pages = pages;
        PageDefaults = pageDefaults;
        Screen = screen;
        Odincode = odincode;
        I18n = i18n;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Metadata and Settings
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Document-level metadata from the {$} header.</summary>
public sealed class FormMetadata
{
    /// <summary>Human-readable form title.</summary>
    public string Title { get; }

    /// <summary>Unique form identifier.</summary>
    public string Id { get; }

    /// <summary>Primary language code (e.g. "en", "es").</summary>
    public string Lang { get; }

    /// <summary>ODIN Forms schema version (e.g. "1.0.0"). Optional.</summary>
    public string? Version { get; }

    /// <summary>Creates form metadata.</summary>
    public FormMetadata(string title, string id, string lang, string? version = null)
    {
        Title = title;
        Id = id;
        Lang = lang;
        Version = version;
    }
}

/// <summary>
/// Default page dimensions applied to all pages unless overridden.
/// Corresponds to {$.page}.
/// </summary>
public sealed class PageDefaults
{
    /// <summary>Page width in the declared unit.</summary>
    public double Width { get; }

    /// <summary>Page height in the declared unit.</summary>
    public double Height { get; }

    /// <summary>Measurement unit for all coordinates and dimensions on the page.</summary>
    public string Unit { get; }

    /// <summary>Page margin in the declared unit. Optional.</summary>
    public double? Margin { get; }

    /// <summary>Creates page defaults.</summary>
    public PageDefaults(double width, double height, string unit, double? margin = null)
    {
        Width = width;
        Height = height;
        Unit = unit;
        Margin = margin;
    }
}

/// <summary>
/// Optional settings for screen/web rendering.
/// Corresponds to {$.screen}.
/// </summary>
public sealed class ScreenSettings
{
    /// <summary>Default zoom factor. 1.0 = 100% (no scaling).</summary>
    public double Scale { get; }

    /// <summary>Creates screen settings.</summary>
    public ScreenSettings(double scale) { Scale = scale; }
}

/// <summary>
/// Self-digitizing barcode settings for the Odincode feature.
/// Corresponds to {$.odincode}.
/// </summary>
public sealed class OdincodeSettings
{
    /// <summary>Whether Odincode generation is enabled.</summary>
    public bool Enabled { get; }

    /// <summary>
    /// Placement zone for the barcode.
    /// "top-center": 0.25" from the top edge, horizontally centered.
    /// "bottom-center": 0.25" from the bottom edge, horizontally centered.
    /// </summary>
    public string Zone { get; }

    /// <summary>Creates odincode settings.</summary>
    public OdincodeSettings(bool enabled, string zone)
    {
        Enabled = enabled;
        Zone = zone;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Pages
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// A single form page containing an ordered list of elements.
/// Corresponds to {page[n]}.
/// </summary>
public sealed class FormPage
{
    /// <summary>All elements on this page, in document order.</summary>
    public IReadOnlyList<FormElement> Elements { get; }

    /// <summary>Creates a form page.</summary>
    public FormPage(IReadOnlyList<FormElement> elements) { Elements = elements; }
}

// ─────────────────────────────────────────────────────────────────────────────
// Base Element
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Abstract base class for all form elements.
/// </summary>
public abstract class FormElement
{
    /// <summary>Element type discriminator (e.g. "rect", "field.text").</summary>
    public abstract string Type { get; }

    /// <summary>
    /// Element name, taken from the path key (e.g. "section_box" in {.rect.section_box}).
    /// Unique within the page.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Optional stable identifier for programmatic access and data binding.
    /// When omitted the renderer may derive one from Name.
    /// </summary>
    public string? Id { get; }

    /// <summary>Creates a base form element.</summary>
    protected FormElement(string name, string? id = null)
    {
        Name = name;
        Id = id;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Geometric Elements
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>A line segment between two explicit endpoints. ({.line.*})</summary>
public sealed class LineElement : FormElement
{
    /// <inheritdoc/>
    public override string Type => "line";

    /// <summary>X coordinate of the start point.</summary>
    public double X1 { get; }

    /// <summary>Y coordinate of the start point.</summary>
    public double Y1 { get; }

    /// <summary>X coordinate of the end point.</summary>
    public double X2 { get; }

    /// <summary>Y coordinate of the end point.</summary>
    public double Y2 { get; }

    /// <summary>Stroke colour as a 6-digit hex string (e.g. "#000000").</summary>
    public string? Stroke { get; }

    /// <summary>Stroke width in the page unit.</summary>
    public double? StrokeWidth { get; }

    /// <summary>Stroke opacity in the range [0, 1].</summary>
    public double? StrokeOpacity { get; }

    /// <summary>Stroke dash pattern, matching SVG stroke-dasharray syntax.</summary>
    public string? StrokeDasharray { get; }

    /// <summary>Line cap style.</summary>
    public string? StrokeLinecap { get; }

    /// <summary>Line join style.</summary>
    public string? StrokeLinejoin { get; }

    /// <summary>Creates a line element.</summary>
    public LineElement(string name, string? id, double x1, double y1, double x2, double y2,
        string? stroke = null, double? strokeWidth = null, double? strokeOpacity = null,
        string? strokeDasharray = null, string? strokeLinecap = null, string? strokeLinejoin = null)
        : base(name, id)
    {
        X1 = x1; Y1 = y1; X2 = x2; Y2 = y2;
        Stroke = stroke; StrokeWidth = strokeWidth; StrokeOpacity = strokeOpacity;
        StrokeDasharray = strokeDasharray; StrokeLinecap = strokeLinecap; StrokeLinejoin = strokeLinejoin;
    }
}

/// <summary>A rectangle, optionally with rounded corners. ({.rect.*})</summary>
public sealed class RectElement : FormElement
{
    /// <inheritdoc/>
    public override string Type => "rect";

    /// <summary>X coordinate from the left edge of the page.</summary>
    public double X { get; }

    /// <summary>Y coordinate from the top edge of the page.</summary>
    public double Y { get; }

    /// <summary>Element width.</summary>
    public double W { get; }

    /// <summary>Element height.</summary>
    public double H { get; }

    /// <summary>Horizontal corner radius. Optional.</summary>
    public double? Rx { get; }

    /// <summary>Vertical corner radius. Optional.</summary>
    public double? Ry { get; }

    /// <summary>Stroke colour as a 6-digit hex string.</summary>
    public string? Stroke { get; }

    /// <summary>Stroke width in the page unit.</summary>
    public double? StrokeWidth { get; }

    /// <summary>Stroke opacity in the range [0, 1].</summary>
    public double? StrokeOpacity { get; }

    /// <summary>Stroke dash pattern.</summary>
    public string? StrokeDasharray { get; }

    /// <summary>Line cap style.</summary>
    public string? StrokeLinecap { get; }

    /// <summary>Line join style.</summary>
    public string? StrokeLinejoin { get; }

    /// <summary>Fill colour as a 6-digit hex string, or "none" for transparent.</summary>
    public string? Fill { get; }

    /// <summary>Fill opacity in the range [0, 1].</summary>
    public double? FillOpacity { get; }

    /// <summary>Creates a rect element.</summary>
    public RectElement(string name, string? id, double x, double y, double w, double h,
        double? rx = null, double? ry = null,
        string? stroke = null, double? strokeWidth = null, double? strokeOpacity = null,
        string? strokeDasharray = null, string? strokeLinecap = null, string? strokeLinejoin = null,
        string? fill = null, double? fillOpacity = null)
        : base(name, id)
    {
        X = x; Y = y; W = w; H = h; Rx = rx; Ry = ry;
        Stroke = stroke; StrokeWidth = strokeWidth; StrokeOpacity = strokeOpacity;
        StrokeDasharray = strokeDasharray; StrokeLinecap = strokeLinecap; StrokeLinejoin = strokeLinejoin;
        Fill = fill; FillOpacity = fillOpacity;
    }
}

/// <summary>A circle defined by a center point and radius. ({.circle.*})</summary>
public sealed class CircleElement : FormElement
{
    /// <inheritdoc/>
    public override string Type => "circle";

    /// <summary>X coordinate of the center.</summary>
    public double Cx { get; }

    /// <summary>Y coordinate of the center.</summary>
    public double Cy { get; }

    /// <summary>Radius (must be >= 0).</summary>
    public double R { get; }

    /// <summary>Stroke colour as a 6-digit hex string.</summary>
    public string? Stroke { get; }

    /// <summary>Stroke width in the page unit.</summary>
    public double? StrokeWidth { get; }

    /// <summary>Stroke opacity in the range [0, 1].</summary>
    public double? StrokeOpacity { get; }

    /// <summary>Stroke dash pattern.</summary>
    public string? StrokeDasharray { get; }

    /// <summary>Line cap style.</summary>
    public string? StrokeLinecap { get; }

    /// <summary>Line join style.</summary>
    public string? StrokeLinejoin { get; }

    /// <summary>Fill colour as a 6-digit hex string, or "none" for transparent.</summary>
    public string? Fill { get; }

    /// <summary>Fill opacity in the range [0, 1].</summary>
    public double? FillOpacity { get; }

    /// <summary>Creates a circle element.</summary>
    public CircleElement(string name, string? id, double cx, double cy, double r,
        string? stroke = null, double? strokeWidth = null, double? strokeOpacity = null,
        string? strokeDasharray = null, string? strokeLinecap = null, string? strokeLinejoin = null,
        string? fill = null, double? fillOpacity = null)
        : base(name, id)
    {
        Cx = cx; Cy = cy; R = r;
        Stroke = stroke; StrokeWidth = strokeWidth; StrokeOpacity = strokeOpacity;
        StrokeDasharray = strokeDasharray; StrokeLinecap = strokeLinecap; StrokeLinejoin = strokeLinejoin;
        Fill = fill; FillOpacity = fillOpacity;
    }
}

/// <summary>An ellipse defined by a center point and two radii. ({.ellipse.*})</summary>
public sealed class EllipseElement : FormElement
{
    /// <inheritdoc/>
    public override string Type => "ellipse";

    /// <summary>X coordinate of the center.</summary>
    public double Cx { get; }

    /// <summary>Y coordinate of the center.</summary>
    public double Cy { get; }

    /// <summary>Horizontal radius (must be >= 0).</summary>
    public double Rx { get; }

    /// <summary>Vertical radius (must be >= 0).</summary>
    public double Ry { get; }

    /// <summary>Stroke colour as a 6-digit hex string.</summary>
    public string? Stroke { get; }

    /// <summary>Stroke width in the page unit.</summary>
    public double? StrokeWidth { get; }

    /// <summary>Stroke opacity in the range [0, 1].</summary>
    public double? StrokeOpacity { get; }

    /// <summary>Stroke dash pattern.</summary>
    public string? StrokeDasharray { get; }

    /// <summary>Line cap style.</summary>
    public string? StrokeLinecap { get; }

    /// <summary>Line join style.</summary>
    public string? StrokeLinejoin { get; }

    /// <summary>Fill colour as a 6-digit hex string, or "none" for transparent.</summary>
    public string? Fill { get; }

    /// <summary>Fill opacity in the range [0, 1].</summary>
    public double? FillOpacity { get; }

    /// <summary>Creates an ellipse element.</summary>
    public EllipseElement(string name, string? id, double cx, double cy, double rx, double ry,
        string? stroke = null, double? strokeWidth = null, double? strokeOpacity = null,
        string? strokeDasharray = null, string? strokeLinecap = null, string? strokeLinejoin = null,
        string? fill = null, double? fillOpacity = null)
        : base(name, id)
    {
        Cx = cx; Cy = cy; Rx = rx; Ry = ry;
        Stroke = stroke; StrokeWidth = strokeWidth; StrokeOpacity = strokeOpacity;
        StrokeDasharray = strokeDasharray; StrokeLinecap = strokeLinecap; StrokeLinejoin = strokeLinejoin;
        Fill = fill; FillOpacity = fillOpacity;
    }
}

/// <summary>A closed polygon defined by a list of points. ({.polygon.*})</summary>
public sealed class PolygonElement : FormElement
{
    /// <inheritdoc/>
    public override string Type => "polygon";

    /// <summary>Space-separated coordinate pairs, e.g. "0,0 1,0 0.5,1".</summary>
    public string Points { get; }

    /// <summary>Stroke colour as a 6-digit hex string.</summary>
    public string? Stroke { get; }

    /// <summary>Stroke width in the page unit.</summary>
    public double? StrokeWidth { get; }

    /// <summary>Stroke opacity in the range [0, 1].</summary>
    public double? StrokeOpacity { get; }

    /// <summary>Stroke dash pattern.</summary>
    public string? StrokeDasharray { get; }

    /// <summary>Line cap style.</summary>
    public string? StrokeLinecap { get; }

    /// <summary>Line join style.</summary>
    public string? StrokeLinejoin { get; }

    /// <summary>Fill colour as a 6-digit hex string, or "none" for transparent.</summary>
    public string? Fill { get; }

    /// <summary>Fill opacity in the range [0, 1].</summary>
    public double? FillOpacity { get; }

    /// <summary>Creates a polygon element.</summary>
    public PolygonElement(string name, string? id, string points,
        string? stroke = null, double? strokeWidth = null, double? strokeOpacity = null,
        string? strokeDasharray = null, string? strokeLinecap = null, string? strokeLinejoin = null,
        string? fill = null, double? fillOpacity = null)
        : base(name, id)
    {
        Points = points;
        Stroke = stroke; StrokeWidth = strokeWidth; StrokeOpacity = strokeOpacity;
        StrokeDasharray = strokeDasharray; StrokeLinecap = strokeLinecap; StrokeLinejoin = strokeLinejoin;
        Fill = fill; FillOpacity = fillOpacity;
    }
}

/// <summary>An open polyline defined by a list of points. ({.polyline.*})</summary>
public sealed class PolylineElement : FormElement
{
    /// <inheritdoc/>
    public override string Type => "polyline";

    /// <summary>Space-separated coordinate pairs, e.g. "0,0 1,0 2,1".</summary>
    public string Points { get; }

    /// <summary>Stroke colour as a 6-digit hex string.</summary>
    public string? Stroke { get; }

    /// <summary>Stroke width in the page unit.</summary>
    public double? StrokeWidth { get; }

    /// <summary>Stroke opacity in the range [0, 1].</summary>
    public double? StrokeOpacity { get; }

    /// <summary>Stroke dash pattern.</summary>
    public string? StrokeDasharray { get; }

    /// <summary>Line cap style.</summary>
    public string? StrokeLinecap { get; }

    /// <summary>Line join style.</summary>
    public string? StrokeLinejoin { get; }

    /// <summary>Creates a polyline element.</summary>
    public PolylineElement(string name, string? id, string points,
        string? stroke = null, double? strokeWidth = null, double? strokeOpacity = null,
        string? strokeDasharray = null, string? strokeLinecap = null, string? strokeLinejoin = null)
        : base(name, id)
    {
        Points = points;
        Stroke = stroke; StrokeWidth = strokeWidth; StrokeOpacity = strokeOpacity;
        StrokeDasharray = strokeDasharray; StrokeLinecap = strokeLinecap; StrokeLinejoin = strokeLinejoin;
    }
}

/// <summary>An SVG-style arbitrary path. ({.path.*})</summary>
public sealed class PathElement : FormElement
{
    /// <inheritdoc/>
    public override string Type => "path";

    /// <summary>SVG path data string, e.g. "M 0,0 L 1,0 L 0.5,1 Z".</summary>
    public string D { get; }

    /// <summary>Stroke colour as a 6-digit hex string.</summary>
    public string? Stroke { get; }

    /// <summary>Stroke width in the page unit.</summary>
    public double? StrokeWidth { get; }

    /// <summary>Stroke opacity in the range [0, 1].</summary>
    public double? StrokeOpacity { get; }

    /// <summary>Stroke dash pattern.</summary>
    public string? StrokeDasharray { get; }

    /// <summary>Line cap style.</summary>
    public string? StrokeLinecap { get; }

    /// <summary>Line join style.</summary>
    public string? StrokeLinejoin { get; }

    /// <summary>Fill colour as a 6-digit hex string, or "none" for transparent.</summary>
    public string? Fill { get; }

    /// <summary>Fill opacity in the range [0, 1].</summary>
    public double? FillOpacity { get; }

    /// <summary>Creates a path element.</summary>
    public PathElement(string name, string? id, string d,
        string? stroke = null, double? strokeWidth = null, double? strokeOpacity = null,
        string? strokeDasharray = null, string? strokeLinecap = null, string? strokeLinejoin = null,
        string? fill = null, double? fillOpacity = null)
        : base(name, id)
    {
        D = d;
        Stroke = stroke; StrokeWidth = strokeWidth; StrokeOpacity = strokeOpacity;
        StrokeDasharray = strokeDasharray; StrokeLinecap = strokeLinecap; StrokeLinejoin = strokeLinejoin;
        Fill = fill; FillOpacity = fillOpacity;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Content Elements
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Static text label. ({.text.*})</summary>
public sealed class TextElement : FormElement
{
    /// <inheritdoc/>
    public override string Type => "text";

    /// <summary>The text string to render. Required.</summary>
    public string Content { get; }

    /// <summary>X coordinate from the left edge of the page.</summary>
    public double X { get; }

    /// <summary>Y coordinate from the top edge of the page.</summary>
    public double Y { get; }

    /// <summary>Rotation angle in degrees, clockwise from the positive X axis. Optional.</summary>
    public double? Rotate { get; }

    /// <summary>Font family name. Default: "Helvetica".</summary>
    public string? FontFamily { get; }

    /// <summary>Font size in points. Must be >= 1. Default: 12.</summary>
    public double? FontSize { get; }

    /// <summary>Font weight. Default: "normal".</summary>
    public string? FontWeight { get; }

    /// <summary>Font style. Default: "normal".</summary>
    public string? FontStyle { get; }

    /// <summary>Horizontal text alignment. Default: "left".</summary>
    public string? TextAlign { get; }

    /// <summary>Text colour as a 6-digit hex string. Default: "#000000".</summary>
    public string? Color { get; }

    /// <summary>Creates a text element.</summary>
    public TextElement(string name, string? id, string content, double x, double y,
        double? rotate = null, string? fontFamily = null, double? fontSize = null,
        string? fontWeight = null, string? fontStyle = null, string? textAlign = null, string? color = null)
        : base(name, id)
    {
        Content = content; X = x; Y = y; Rotate = rotate;
        FontFamily = fontFamily; FontSize = fontSize; FontWeight = fontWeight;
        FontStyle = fontStyle; TextAlign = textAlign; Color = color;
    }
}

/// <summary>Embedded image. ({.img.*})</summary>
public sealed class ImageElement : FormElement
{
    /// <inheritdoc/>
    public override string Type => "img";

    /// <summary>Base64-encoded image data with format prefix. Required.</summary>
    public string Src { get; }

    /// <summary>Accessibility description for screen readers. Required.</summary>
    public string Alt { get; }

    /// <summary>X coordinate from the left edge of the page.</summary>
    public double X { get; }

    /// <summary>Y coordinate from the top edge of the page.</summary>
    public double Y { get; }

    /// <summary>Element width.</summary>
    public double W { get; }

    /// <summary>Element height.</summary>
    public double H { get; }

    /// <summary>Creates an image element.</summary>
    public ImageElement(string name, string? id, string src, string alt, double x, double y, double w, double h)
        : base(name, id)
    {
        Src = src; Alt = alt; X = x; Y = y; W = w; H = h;
    }
}

/// <summary>1D or 2D barcode. ({.barcode.*})</summary>
public sealed class BarcodeElement : FormElement
{
    /// <inheritdoc/>
    public override string Type => "barcode";

    /// <summary>Barcode symbology.</summary>
    public string BarcodeType { get; }

    /// <summary>Data to encode in the barcode. Required.</summary>
    public string Content { get; }

    /// <summary>Accessibility description for screen readers. Required.</summary>
    public string Alt { get; }

    /// <summary>X coordinate from the left edge of the page.</summary>
    public double X { get; }

    /// <summary>Y coordinate from the top edge of the page.</summary>
    public double Y { get; }

    /// <summary>Element width.</summary>
    public double W { get; }

    /// <summary>Element height.</summary>
    public double H { get; }

    /// <summary>Creates a barcode element.</summary>
    public BarcodeElement(string name, string? id, string barcodeType, string content, string alt,
        double x, double y, double w, double h)
        : base(name, id)
    {
        BarcodeType = barcodeType; Content = content; Alt = alt;
        X = x; Y = y; W = w; H = h;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Base Field Element
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Abstract base class for all field elements.</summary>
public abstract class BaseFieldElement : FormElement
{
    /// <summary>Visible label text. Required. Also used as the default ARIA label.</summary>
    public string Label { get; }

    /// <summary>Override ARIA label for screen readers when it should differ from Label.</summary>
    public string? AriaLabel { get; }

    /// <summary>Tab order index (integer >= 0).</summary>
    public int? Tabindex { get; }

    /// <summary>Whether the field is read-only.</summary>
    public bool? Readonly { get; }

    /// <summary>X coordinate from the left edge of the page.</summary>
    public double X { get; }

    /// <summary>Y coordinate from the top edge of the page.</summary>
    public double Y { get; }

    /// <summary>Element width.</summary>
    public double W { get; }

    /// <summary>Element height.</summary>
    public double H { get; }

    /// <summary>ODIN path reference for the field's value (e.g. "@policy.coverageLevel").</summary>
    public string Bind { get; }

    /// <summary>Whether the field must have a value before the form can be submitted.</summary>
    public bool? Required { get; }

    /// <summary>Regular expression the value must match.</summary>
    public string? Pattern { get; }

    /// <summary>Minimum string length (integer >= 0).</summary>
    public int? MinLength { get; }

    /// <summary>Maximum string length (integer >= 1).</summary>
    public int? MaxLength { get; }

    /// <summary>Minimum value.</summary>
    public string? Min { get; }

    /// <summary>Maximum value.</summary>
    public string? Max { get; }

    /// <summary>Creates a base field element.</summary>
    protected BaseFieldElement(string name, string? id, string label, double x, double y, double w, double h,
        string bind, string? ariaLabel = null, int? tabindex = null, bool? readOnly = null,
        bool? required = null, string? pattern = null, int? minLength = null, int? maxLength = null,
        string? min = null, string? max = null)
        : base(name, id)
    {
        Label = label; X = x; Y = y; W = w; H = h; Bind = bind;
        AriaLabel = ariaLabel; Tabindex = tabindex; Readonly = readOnly;
        Required = required; Pattern = pattern; MinLength = minLength; MaxLength = maxLength;
        Min = min; Max = max;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Field Types
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Single-line or multi-line text input field. (type = text)</summary>
public sealed class TextFieldElement : BaseFieldElement
{
    /// <inheritdoc/>
    public override string Type => "field.text";

    /// <summary>Input mask pattern (e.g. "###-##-####"). Optional.</summary>
    public string? Mask { get; }

    /// <summary>Placeholder text shown when the field is empty. Optional.</summary>
    public string? Placeholder { get; }

    /// <summary>Whether the field accepts multiple lines of text. Optional.</summary>
    public bool? Multiline { get; }

    /// <summary>Maximum number of lines when Multiline is true. Optional.</summary>
    public int? MaxLines { get; }

    /// <summary>Creates a text field element.</summary>
    public TextFieldElement(string name, string? id, string label, double x, double y, double w, double h,
        string bind, string? ariaLabel = null, int? tabindex = null, bool? readOnly = null,
        bool? required = null, string? pattern = null, int? minLength = null, int? maxLength = null,
        string? min = null, string? max = null,
        string? mask = null, string? placeholder = null, bool? multiline = null, int? maxLines = null)
        : base(name, id, label, x, y, w, h, bind, ariaLabel, tabindex, readOnly, required, pattern, minLength, maxLength, min, max)
    {
        Mask = mask; Placeholder = placeholder; Multiline = multiline; MaxLines = maxLines;
    }
}

/// <summary>Boolean checkbox field. (type = checkbox)</summary>
public sealed class CheckboxElement : BaseFieldElement
{
    /// <inheritdoc/>
    public override string Type => "field.checkbox";

    /// <summary>Creates a checkbox element.</summary>
    public CheckboxElement(string name, string? id, string label, double x, double y, double w, double h,
        string bind, string? ariaLabel = null, int? tabindex = null, bool? readOnly = null,
        bool? required = null, string? pattern = null, int? minLength = null, int? maxLength = null,
        string? min = null, string? max = null)
        : base(name, id, label, x, y, w, h, bind, ariaLabel, tabindex, readOnly, required, pattern, minLength, maxLength, min, max)
    { }
}

/// <summary>Radio button field — part of a mutually exclusive group. (type = radio)</summary>
public sealed class RadioElement : BaseFieldElement
{
    /// <inheritdoc/>
    public override string Type => "field.radio";

    /// <summary>Radio group name. All radios sharing a group are mutually exclusive. Required.</summary>
    public string Group { get; }

    /// <summary>Value emitted to the bound path when this radio button is selected. Required.</summary>
    public string Value { get; }

    /// <summary>Creates a radio element.</summary>
    public RadioElement(string name, string? id, string label, double x, double y, double w, double h,
        string bind, string group, string value,
        string? ariaLabel = null, int? tabindex = null, bool? readOnly = null,
        bool? required = null, string? pattern = null, int? minLength = null, int? maxLength = null,
        string? min = null, string? max = null)
        : base(name, id, label, x, y, w, h, bind, ariaLabel, tabindex, readOnly, required, pattern, minLength, maxLength, min, max)
    {
        Group = group; Value = value;
    }
}

/// <summary>Single-selection dropdown field. (type = select)</summary>
public sealed class SelectElement : BaseFieldElement
{
    /// <inheritdoc/>
    public override string Type => "field.select";

    /// <summary>Ordered list of valid option values. Required.</summary>
    public IReadOnlyList<string> Options { get; }

    /// <summary>Default label shown when no option is selected. Optional.</summary>
    public string? Placeholder { get; }

    /// <summary>Creates a select element.</summary>
    public SelectElement(string name, string? id, string label, double x, double y, double w, double h,
        string bind, IReadOnlyList<string> options,
        string? ariaLabel = null, int? tabindex = null, bool? readOnly = null,
        bool? required = null, string? pattern = null, int? minLength = null, int? maxLength = null,
        string? min = null, string? max = null, string? placeholder = null)
        : base(name, id, label, x, y, w, h, bind, ariaLabel, tabindex, readOnly, required, pattern, minLength, maxLength, min, max)
    {
        Options = options; Placeholder = placeholder;
    }
}

/// <summary>Multiple-selection list field. (type = multiselect)</summary>
public sealed class MultiselectElement : BaseFieldElement
{
    /// <inheritdoc/>
    public override string Type => "field.multiselect";

    /// <summary>Ordered list of valid option values. Required.</summary>
    public IReadOnlyList<string> Options { get; }

    /// <summary>Minimum number of selections required (integer >= 1). Optional.</summary>
    public int? MinSelect { get; }

    /// <summary>Maximum number of selections allowed. Optional.</summary>
    public int? MaxSelect { get; }

    /// <summary>Creates a multiselect element.</summary>
    public MultiselectElement(string name, string? id, string label, double x, double y, double w, double h,
        string bind, IReadOnlyList<string> options,
        string? ariaLabel = null, int? tabindex = null, bool? readOnly = null,
        bool? required = null, string? pattern = null, int? minLength = null, int? maxLength = null,
        string? min = null, string? max = null, int? minSelect = null, int? maxSelect = null)
        : base(name, id, label, x, y, w, h, bind, ariaLabel, tabindex, readOnly, required, pattern, minLength, maxLength, min, max)
    {
        Options = options; MinSelect = minSelect; MaxSelect = maxSelect;
    }
}

/// <summary>Date input field. (type = date)</summary>
public sealed class DateElement : BaseFieldElement
{
    /// <inheritdoc/>
    public override string Type => "field.date";

    /// <summary>Creates a date element.</summary>
    public DateElement(string name, string? id, string label, double x, double y, double w, double h,
        string bind, string? ariaLabel = null, int? tabindex = null, bool? readOnly = null,
        bool? required = null, string? pattern = null, int? minLength = null, int? maxLength = null,
        string? min = null, string? max = null)
        : base(name, id, label, x, y, w, h, bind, ariaLabel, tabindex, readOnly, required, pattern, minLength, maxLength, min, max)
    { }
}

/// <summary>Signature capture area. (type = signature)</summary>
public sealed class SignatureElement : BaseFieldElement
{
    /// <inheritdoc/>
    public override string Type => "field.signature";

    /// <summary>ODIN reference to an associated date field. Optional.</summary>
    public string? DateField { get; }

    /// <summary>Creates a signature element.</summary>
    public SignatureElement(string name, string? id, string label, double x, double y, double w, double h,
        string bind, string? dateField = null,
        string? ariaLabel = null, int? tabindex = null, bool? readOnly = null,
        bool? required = null, string? pattern = null, int? minLength = null, int? maxLength = null,
        string? min = null, string? max = null)
        : base(name, id, label, x, y, w, h, bind, ariaLabel, tabindex, readOnly, required, pattern, minLength, maxLength, min, max)
    {
        DateField = dateField;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Renderer Options
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Options passed to the form renderer.</summary>
public sealed class RenderFormOptions
{
    /// <summary>
    /// Rendering target.
    /// "html": Interactive HTML form for screen display.
    /// "print-css": Static HTML/CSS layout optimised for @media print / PDF export.
    /// </summary>
    public string Target { get; init; } = "html";

    /// <summary>
    /// Language code for i18n label resolution (e.g. "en", "es").
    /// Falls back to FormMetadata.Lang when omitted.
    /// </summary>
    public string? Lang { get; init; }

    /// <summary>
    /// Uniform scale factor applied to all page dimensions.
    /// Falls back to ScreenSettings.Scale (or 1.0) when omitted.
    /// </summary>
    public double? Scale { get; init; }

    /// <summary>
    /// Additional CSS class name(s) added to the root rendered element.
    /// Useful for theming or test selectors.
    /// </summary>
    public string? ClassName { get; init; }
}
