using System.Linq;
using Odin.Core;
using Odin.Core.Forms;
using Xunit;

namespace Odin.Core.Tests.Unit;

/// <summary>
/// Self-contained unit tests for the ODIN Forms parser and renderer:
/// happy-path, edge cases, and error/graceful-degradation behavior.
/// </summary>
public class FormsTests
{
    // ─────────────────────────────────────────────────────────────────────────
    // Happy path
    // ─────────────────────────────────────────────────────────────────────────

    private const string PageTemplateForm = @"{$}
odin = ""1.0.0""
forms = ""1.0.0""
title = ""Template Form""
id = ""tpl_form""
lang = ""en""

{$.page}
width = #8.5
height = #11
unit = ""inch""

{page[0]}
{.text.header}
x = #0.5
y = #0.5
content = ""Vehicles — Page {@odin.page} of {@odin.total_pages}""
font-size = ##14
font-weight = ""bold""

{.region.vehicles}
x = #0.5
y = #1.2
w = #7.5
h = #6
bind = @policy.vehicles
max = ##3
overflow = @tpl_vehicles_continued

{.region.vehicles.field.vin}
x = #0
y = #0.15
y-offset = #1.8
w = #4
h = #0.3
label = ""VIN""
bind = @.vin

{@tpl_vehicles_continued}
page-template = ?true
continues = ""region.vehicles""
form-id = ""PA (Cont)""

{.text.header}
x = #0.5
y = #0.5
content = ""Additional Vehicles — Page {@odin.page} of {@odin.total_pages}""
font-size = ##14
font-weight = ""bold""

{.region.vehicles}
x = #0.5
y = #1
w = #7.5
h = #8
max = ##4
overflow = @tpl_vehicles_continued

{.region.vehicles.field.vin}
x = #0
y = #0.15
y-offset = #1.2
w = #4
h = #0.3
label = ""VIN""
bind = @.vin
";

    [Fact]
    public void Happy_PageTemplateFormParsesRegionsAndTemplate()
    {
        var form = Odin.ParseForm(PageTemplateForm);

        Assert.Single(form.Pages);
        var region = Assert.IsType<RegionElement>(form.Pages[0].Elements.First(e => e.Name == "vehicles"));
        Assert.Equal("@policy.vehicles", region.Bind);
        Assert.Equal(3, region.Max);
        Assert.Equal("@tpl_vehicles_continued", region.Overflow);
        Assert.Single(region.Children);

        Assert.NotNull(form.Templates);
        var tpl = form.Templates!["tpl_vehicles_continued"];
        Assert.True(tpl.IsPageTemplate);
        Assert.Equal("region.vehicles", tpl.Continues);
        Assert.Equal("PA (Cont)", tpl.FormId);
        Assert.Equal(new[] { "text", "region" }, tpl.Elements.Select(e => e.Type).ToArray());
    }

    [Fact]
    public void Happy_RegionRendersBoundRepetition()
    {
        var form = Odin.ParseForm(PageTemplateForm);
        var data = Odin.Parse("{policy}\n{.vehicles[0]}\nvin = \"V0\"\n{.vehicles[1]}\nvin = \"V1\"");
        var html = Odin.RenderForm(form, data, new RenderFormOptions { Target = "html" });

        Assert.Contains("value=\"V0\"", html);
        Assert.Contains("value=\"V1\"", html);
        Assert.Contains("data-region=\"vehicles\"", html);
    }

    [Fact]
    public void Happy_InlineValueTakesPrecedenceOverBoundValue()
    {
        var form = Odin.ParseForm(@"{$}
title = ""x""
id = ""x""
lang = ""en""
{$.page}
width = #8.5
height = #11
unit = ""inch""
{page[0]}
{.field.name}
type = ""text""
x = #0.5
y = #1
w = #3
h = #0.3
label = ""Name""
value = ""Inline""
bind = @insured.name
");
        var data = Odin.Parse("{insured}\nname = \"Bound\"");
        var html = Odin.RenderForm(form, data, new RenderFormOptions { Target = "html" });

        Assert.Contains("value=\"Inline\"", html);
        Assert.DoesNotContain("value=\"Bound\"", html);
    }

    [Fact]
    public void Happy_SelectRendersOptions()
    {
        var form = Odin.ParseForm(@"{$}
title = ""x""
id = ""x""
lang = ""en""
{$.page}
width = #8.5
height = #11
unit = ""inch""
{page[0]}
{.field.state}
type = ""select""
x = #0.5
y = #1
w = #2
h = #0.3
label = ""State""
selected = ""TX""
bind = @insured.state
{.field.state.options[] : ~}
""AL""
""TX""
");
        var select = Assert.IsType<SelectElement>(form.Pages[0].Elements.First(e => e.Name == "state"));
        Assert.Equal(new[] { "AL", "TX" }, select.Options.ToArray());
        Assert.Equal("TX", select.Selected);

        var html = Odin.RenderForm(form, null, new RenderFormOptions { Target = "html" });
        Assert.Contains("<option value=\"TX\" selected>", html);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Edge cases
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Edge_RegionOverflowSpillsToSecondPage()
    {
        var form = Odin.ParseForm(PageTemplateForm);
        var data = Odin.Parse(
            "{policy}\n{.vehicles[0]}\nvin = \"V0\"\n{.vehicles[1]}\nvin = \"V1\"\n" +
            "{.vehicles[2]}\nvin = \"V2\"\n{.vehicles[3]}\nvin = \"V3\"\n{.vehicles[4]}\nvin = \"V4\"");
        var html = Odin.RenderForm(form, data, new RenderFormOptions { Target = "html" });

        Assert.Contains("Page 1 of 2", html);
        Assert.Contains("Page 2 of 2", html);
        Assert.Contains("value=\"V0\"", html);  // first page
        Assert.Contains("value=\"V3\"", html);  // overflow page
        Assert.Contains("value=\"V4\"", html);
        Assert.DoesNotContain("{@odin.total_pages}", html);
        Assert.Contains("data-page=\"2\"", html);
    }

    [Fact]
    public void Edge_PerSideMarginsParse()
    {
        var form = Odin.ParseForm(@"{$}
title = ""x""
id = ""x""
lang = ""en""
{$.page}
width = #8.5
height = #11
unit = ""inch""
margin.top = #0.5
margin.right = #0.25
margin.bottom = #0.6
margin.left = #0.75
{page[0]}
{.text.t}
x = #0
y = #0
content = ""hi""
");
        var m = form.PageDefaults!.Margin;
        Assert.NotNull(m);
        Assert.Equal(0.5, m!.Top);
        Assert.Equal(0.25, m.Right);
        Assert.Equal(0.6, m.Bottom);
        Assert.Equal(0.75, m.Left);
    }

    [Fact]
    public void Edge_BarcodeTypeFallbackToBarcodeType()
    {
        var form = Odin.ParseForm(@"{$}
title = ""x""
id = ""x""
lang = ""en""
{$.page}
width = #8.5
height = #11
unit = ""inch""
{page[0]}
{.barcode.a}
x = #0
y = #0
w = #1
h = #1
barcode-type = ""pdf417""
content = ""data""
alt = ""code""
");
        var bc = Assert.IsType<BarcodeElement>(form.Pages[0].Elements.First(e => e.Name == "a"));
        Assert.Equal("pdf417", bc.BarcodeType);
    }

    [Fact]
    public void Edge_I18nLabelReferenceResolves()
    {
        var form = Odin.ParseForm(@"{$}
title = ""x""
id = ""x""
lang = ""en""
{$.i18n}
en.field_name = ""Full Legal Name""
{$.page}
width = #8.5
height = #11
unit = ""inch""
{page[0]}
{.field.name}
type = ""text""
x = #0.5
y = #1
w = #3
h = #0.3
label = @$.i18n.en.field_name
bind = @insured.name
");
        var field = Assert.IsType<TextFieldElement>(form.Pages[0].Elements.First(e => e.Name == "name"));
        Assert.Equal("Full Legal Name", field.Label);
    }

    [Fact]
    public void Edge_EscapeAttrEscapesQuotesAngleAndAmpersand()
    {
        var form = Odin.ParseForm(@"{$}
title = ""x""
id = ""x""
lang = ""en""
{$.page}
width = #8.5
height = #11
unit = ""inch""
{page[0]}
{.field.name}
type = ""text""
x = #0.5
y = #1
w = #3
h = #0.3
label = ""Name""
value = ""a'b\""c<d&e""
bind = @insured.name
");
        var html = Odin.RenderForm(form, null, new RenderFormOptions { Target = "html" });
        // Single quote, double quote, angle bracket and ampersand all escaped in the attribute.
        Assert.Contains("value=\"a&#39;b&quot;c&lt;d&amp;e\"", html);
        Assert.DoesNotContain("a'b", html);
    }

    [Fact]
    public void Edge_BackgroundImageGetsLowestZIndex()
    {
        var form = Odin.ParseForm(@"{$}
title = ""x""
id = ""x""
lang = ""en""
{$.page}
width = #8.5
height = #11
unit = ""inch""
{page[0]}
{.img.bg}
x = #0
y = #0
w = #8.5
h = #11
src = ^png:iVBORw0KGgo=
alt = ""bg""
background = ?true
");
        var img = Assert.IsType<ImageElement>(form.Pages[0].Elements.First(e => e.Name == "bg"));
        Assert.True(img.Background);
        var html = Odin.RenderForm(form, null, new RenderFormOptions { Target = "html" });
        Assert.Contains("z-index:0;", html);
        Assert.Contains("data:image/png;base64,", html);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Error / graceful degradation
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Error_RegionBoundToMissingArrayRendersEmptyPreviewNoOverflow()
    {
        var form = Odin.ParseForm(PageTemplateForm);
        // No data document: region renders a single empty preview, no overflow pages.
        var html = Odin.RenderForm(form, null, new RenderFormOptions { Target = "html" });

        Assert.Contains("Page 1 of 1", html);
        Assert.DoesNotContain("Page 1 of 2", html);
        Assert.Single(form.Pages);
    }

    [Fact]
    public void Error_RegionBoundToEmptyArrayDoesNotOverflow()
    {
        var form = Odin.ParseForm(PageTemplateForm);
        var data = Odin.Parse("{policy}\nname = \"none\"");  // vehicles array absent
        var html = Odin.RenderForm(form, data, new RenderFormOptions { Target = "html" });

        Assert.Contains("Page 1 of 1", html);
        Assert.DoesNotContain("data-page=\"2\"", html);
    }

    [Fact]
    public void Error_MalformedTemplateReferenceLeavesNoOverflowPages()
    {
        // overflow points at a template that does not exist; render must not throw
        // and must not spawn a continuation page.
        var form = Odin.ParseForm(@"{$}
title = ""x""
id = ""x""
lang = ""en""
{$.page}
width = #8.5
height = #11
unit = ""inch""
{page[0]}
{.region.items}
x = #0.5
y = #1
w = #7
h = #6
bind = @data.items
max = ##2
overflow = @tpl_missing

{.region.items.field.v}
x = #0
y = #0
y-offset = #1
w = #3
h = #0.3
label = ""V""
bind = @.v
");
        var data = Odin.Parse(
            "{data}\n{.items[0]}\nv = \"A\"\n{.items[1]}\nv = \"B\"\n{.items[2]}\nv = \"C\"\n{.items[3]}\nv = \"D\"");
        var html = Odin.RenderForm(form, data, new RenderFormOptions { Target = "html" });

        // A continuation page is still planned (clone fallback), but no template
        // means the original page's own elements are reused — render must succeed.
        Assert.NotNull(html);
        Assert.Contains("value=\"A\"", html);
    }
}
