using System.Collections.Generic;
using Odin.Core;
using Odin.Core.Transform;
using Odin.Core.Types;
using Xunit;

namespace Odin.Core.Tests.Unit;

public class TransformParserTests
{
    // ─────────────────────────────────────────────────────────────────
    // Header/Metadata Parsing
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ParsesMetadataOdinVersion()
    {
        var t = Core.Odin.ParseTransform(@"
{$}
odin = ""1.0.0""
transform = ""1.0.0""
direction = ""json->json""
target.format = ""json""
");
        Assert.Equal("1.0.0", t.Metadata.OdinVersion);
    }

    [Fact]
    public void ParsesMetadataTransformVersion()
    {
        var t = Core.Odin.ParseTransform(@"
{$}
odin = ""1.0.0""
transform = ""1.0.0""
direction = ""json->json""
target.format = ""json""
");
        Assert.Equal("1.0.0", t.Metadata.TransformVersion);
    }

    [Fact]
    public void ParsesMetadataDirection()
    {
        var t = Core.Odin.ParseTransform(@"
{$}
odin = ""1.0.0""
transform = ""1.0.0""
direction = ""json->odin""
target.format = ""odin""
");
        Assert.Equal("json->odin", t.Metadata.Direction);
    }

    [Fact]
    public void ParsesTargetFormat()
    {
        var t = Core.Odin.ParseTransform(@"
{$}
odin = ""1.0.0""
transform = ""1.0.0""
direction = ""json->json""
target.format = ""json""
");
        Assert.Equal("json", t.Target.Format);
    }

    [Fact]
    public void ParsesTargetFormatCsv()
    {
        var t = Core.Odin.ParseTransform(@"
{$}
odin = ""1.0.0""
transform = ""1.0.0""
direction = ""json->csv""
target.format = ""csv""
");
        Assert.Equal("csv", t.Target.Format);
    }

    [Fact]
    public void ParsesTargetFormatOdin()
    {
        var t = Core.Odin.ParseTransform(@"
{$}
odin = ""1.0.0""
transform = ""1.0.0""
direction = ""json->odin""
target.format = ""odin""
");
        Assert.Equal("odin", t.Target.Format);
    }

    [Fact]
    public void ParsesDirectionXmlToOdin()
    {
        var t = Core.Odin.ParseTransform(@"
{$}
odin = ""1.0.0""
transform = ""1.0.0""
direction = ""xml->odin""
target.format = ""odin""
");
        Assert.Equal("xml->odin", t.Metadata.Direction);
    }

    // ─────────────────────────────────────────────────────────────────
    // Segments
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ParsesSingleSegment()
    {
        var t = Core.Odin.ParseTransform(@"
{$}
odin = ""1.0.0""
transform = ""1.0.0""
direction = ""json->json""
target.format = ""json""

{Person}
Name = ""@.name""
");
        Assert.True(t.Segments.Count >= 1);
        var seg = t.Segments[0];
        Assert.Equal("Person", seg.Name);
    }

    [Fact]
    public void ParsesMultipleSegments()
    {
        var t = Core.Odin.ParseTransform(@"
{$}
odin = ""1.0.0""
transform = ""1.0.0""
direction = ""json->json""
target.format = ""json""

{Person}
Name = ""@.name""

{Address}
City = ""@.city""
");
        Assert.True(t.Segments.Count >= 2);
    }

    [Fact]
    public void ParsesMappingCopyExpression()
    {
        var t = Core.Odin.ParseTransform(@"
{$}
odin = ""1.0.0""
transform = ""1.0.0""
direction = ""json->json""
target.format = ""json""

{Data}
Name = ""@.name""
");
        Assert.True(t.Segments.Count >= 1);
        var mappings = t.Segments[0].Mappings;
        Assert.True(mappings.Count >= 1);
        Assert.Equal("Name", mappings[0].Target);
        Assert.IsType<CopyExpression>(mappings[0].Expression);
    }

    [Fact]
    public void ParsesMappingVerbExpression()
    {
        var t = Core.Odin.ParseTransform(@"
{$}
odin = ""1.0.0""
transform = ""1.0.0""
direction = ""json->json""
target.format = ""json""

{Data}
Name = ""%upper @.name""
");
        Assert.True(t.Segments.Count >= 1);
        var mappings = t.Segments[0].Mappings;
        Assert.True(mappings.Count >= 1);
        Assert.IsType<TransformExpression>(mappings[0].Expression);
        var expr = (TransformExpression)mappings[0].Expression;
        Assert.Equal("upper", expr.Call.Verb);
    }

    [Fact]
    public void ParsesMappingLiteralExpression()
    {
        var t = Core.Odin.ParseTransform(@"
{$}
odin = ""1.0.0""
transform = ""1.0.0""
direction = ""json->json""
target.format = ""json""

{Data}
Version = ""1.0""
");
        Assert.True(t.Segments.Count >= 1);
        var mappings = t.Segments[0].Mappings;
        Assert.True(mappings.Count >= 1);
    }

    [Fact]
    public void ParsesNestedVerbExpression()
    {
        var t = Core.Odin.ParseTransform(@"
{$}
odin = ""1.0.0""
transform = ""1.0.0""
direction = ""json->json""
target.format = ""json""

{Data}
Name = ""%upper %concat @.first \"" \"" @.last""
");
        Assert.True(t.Segments.Count >= 1);
        var mappings = t.Segments[0].Mappings;
        Assert.True(mappings.Count >= 1);
        Assert.IsType<TransformExpression>(mappings[0].Expression);
        var expr = (TransformExpression)mappings[0].Expression;
        Assert.Equal("upper", expr.Call.Verb);
        Assert.True(expr.Call.Args.Count >= 1);
    }

    // ─────────────────────────────────────────────────────────────────
    // Constants
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ParsesConstants()
    {
        var t = Core.Odin.ParseTransform(@"
{$}
odin = ""1.0.0""
transform = ""1.0.0""
direction = ""json->json""
target.format = ""json""

{const}
version = ""2.0""
max = ##100
");
        Assert.True(t.Constants.Count >= 2);
        Assert.True(t.Constants.ContainsKey("version"));
        Assert.True(t.Constants.ContainsKey("max"));
    }

    [Fact]
    public void ParsesConstantStringValue()
    {
        var t = Core.Odin.ParseTransform(@"
{$}
odin = ""1.0.0""
transform = ""1.0.0""
direction = ""json->json""
target.format = ""json""

{const}
prefix = ""Hello""
");
        Assert.True(t.Constants.ContainsKey("prefix"));
        var val = t.Constants["prefix"];
        Assert.IsType<OdinString>(val);
        Assert.Equal("Hello", ((OdinString)val).Value);
    }

    // ─────────────────────────────────────────────────────────────────
    // Modifiers
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ParsesRequiredModifier()
    {
        var t = Core.Odin.ParseTransform(@"
{$}
odin = ""1.0.0""
transform = ""1.0.0""
direction = ""json->json""
target.format = ""json""

{Data}
Name = ""@.name :required""
");
        var mappings = t.Segments[0].Mappings;
        Assert.NotNull(mappings[0].Modifiers);
        Assert.True(mappings[0].Modifiers!.Required);
    }

    [Fact]
    public void ParsesConfidentialModifier()
    {
        var t = Core.Odin.ParseTransform(@"
{$}
odin = ""1.0.0""
transform = ""1.0.0""
direction = ""json->json""
target.format = ""json""

{Data}
SSN = ""@.ssn :confidential""
");
        var mappings = t.Segments[0].Mappings;
        Assert.NotNull(mappings[0].Modifiers);
        Assert.True(mappings[0].Modifiers!.Confidential);
    }

    [Fact]
    public void ParsesDeprecatedModifier()
    {
        var t = Core.Odin.ParseTransform(@"
{$}
odin = ""1.0.0""
transform = ""1.0.0""
direction = ""json->json""
target.format = ""json""

{Data}
Old = ""@.old :deprecated""
");
        var mappings = t.Segments[0].Mappings;
        Assert.NotNull(mappings[0].Modifiers);
        Assert.True(mappings[0].Modifiers!.Deprecated);
    }

    // ─────────────────────────────────────────────────────────────────
    // Confidential Enforcement
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ParsesEnforceConfidentialRedact()
    {
        var t = Core.Odin.ParseTransform(@"
{$}
odin = ""1.0.0""
transform = ""1.0.0""
direction = ""json->json""
target.format = ""json""
enforceConfidential = ""redact""
");
        Assert.Equal(ConfidentialMode.Redact, t.EnforceConfidential);
    }

    [Fact]
    public void ParsesEnforceConfidentialMask()
    {
        var t = Core.Odin.ParseTransform(@"
{$}
odin = ""1.0.0""
transform = ""1.0.0""
direction = ""json->json""
target.format = ""json""
enforceConfidential = ""mask""
");
        Assert.Equal(ConfidentialMode.Mask, t.EnforceConfidential);
    }

    [Fact]
    public void NoEnforceConfidentialByDefault()
    {
        var t = Core.Odin.ParseTransform(@"
{$}
odin = ""1.0.0""
transform = ""1.0.0""
direction = ""json->json""
target.format = ""json""
");
        Assert.Null(t.EnforceConfidential);
    }

    // ─────────────────────────────────────────────────────────────────
    // Source Config
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ParsesSourceFormat()
    {
        var t = Core.Odin.ParseTransform(@"
{$}
odin = ""1.0.0""
transform = ""1.0.0""
direction = ""csv->json""
source.format = ""csv""
target.format = ""json""
");
        Assert.NotNull(t.Source);
        Assert.Equal("csv", t.Source!.Format);
    }

    [Fact]
    public void ParsesSourceOptions()
    {
        var t = Core.Odin.ParseTransform(@"
{$}
odin = ""1.0.0""
transform = ""1.0.0""
direction = ""csv->json""
source.format = ""csv""
source.delimiter = ""|""
target.format = ""json""
");
        Assert.NotNull(t.Source);
        Assert.True(t.Source!.Options.ContainsKey("delimiter"));
        Assert.Equal("|", t.Source.Options["delimiter"]);
    }

    // ─────────────────────────────────────────────────────────────────
    // Verb Arguments
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ParsesVerbWithMultipleArgs()
    {
        var t = Core.Odin.ParseTransform(@"
{$}
odin = ""1.0.0""
transform = ""1.0.0""
direction = ""json->json""
target.format = ""json""

{Data}
Full = ""%concat @.first \"" \"" @.last""
");
        var expr = (TransformExpression)t.Segments[0].Mappings[0].Expression;
        Assert.Equal("concat", expr.Call.Verb);
        Assert.True(expr.Call.Args.Count >= 3);
    }

    [Fact]
    public void ParsesVerbWithLiteralArg()
    {
        var t = Core.Odin.ParseTransform(@"
{$}
odin = ""1.0.0""
transform = ""1.0.0""
direction = ""json->json""
target.format = ""json""

{Data}
X = ""%concat @.name \""!\""""
");
        var expr = (TransformExpression)t.Segments[0].Mappings[0].Expression;
        Assert.Equal("concat", expr.Call.Verb);
    }

    // ─────────────────────────────────────────────────────────────────
    // Edge Cases
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ParsesEmptyTransform()
    {
        var t = Core.Odin.ParseTransform(@"
{$}
odin = ""1.0.0""
transform = ""1.0.0""
direction = ""json->json""
target.format = ""json""
");
        Assert.NotNull(t);
        Assert.Empty(t.Segments);
    }

    [Fact]
    public void ParsesTransformWithComments()
    {
        var t = Core.Odin.ParseTransform(@"
{$}
odin = ""1.0.0""  ; version comment
transform = ""1.0.0""
direction = ""json->json""
target.format = ""json""

; Segment comment
{Data}
Name = ""@.name""  ; mapping comment
");
        Assert.True(t.Segments.Count >= 1);
        Assert.True(t.Segments[0].Mappings.Count >= 1);
    }

    [Fact]
    public void ParsesMappingTarget()
    {
        var t = Core.Odin.ParseTransform(@"
{$}
odin = ""1.0.0""
transform = ""1.0.0""
direction = ""json->json""
target.format = ""json""

{Data}
OutputField = ""@.input_field""
");
        Assert.Equal("OutputField", t.Segments[0].Mappings[0].Target);
    }

    [Fact]
    public void ParsesCopyExpressionPath()
    {
        var t = Core.Odin.ParseTransform(@"
{$}
odin = ""1.0.0""
transform = ""1.0.0""
direction = ""json->json""
target.format = ""json""

{Data}
Name = ""@.person.name""
");
        var expr = t.Segments[0].Mappings[0].Expression;
        Assert.IsType<CopyExpression>(expr);
        Assert.Equal(".person.name", ((CopyExpression)expr).Path);
    }

    [Fact]
    public void ParsesArrayIndexInCopyPath()
    {
        var t = Core.Odin.ParseTransform(@"
{$}
odin = ""1.0.0""
transform = ""1.0.0""
direction = ""json->json""
target.format = ""json""

{Data}
First = ""@.items[0].name""
");
        var expr = t.Segments[0].Mappings[0].Expression;
        Assert.IsType<CopyExpression>(expr);
        Assert.Equal(".items[0].name", ((CopyExpression)expr).Path);
    }

    // ─────────────────────────────────────────────────────────────────
    // Lookup Tables
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ParsesLookupTable()
    {
        var t = Core.Odin.ParseTransform(@"
{$}
odin = ""1.0.0""
transform = ""1.0.0""
direction = ""json->json""
target.format = ""json""

{table.states[] : code, name}
CA, California
NY, New York
TX, Texas
");
        Assert.True(t.Tables.Count >= 1);
        Assert.True(t.Tables.ContainsKey("states"));
        var table = t.Tables["states"];
        Assert.True(table.Columns.Count >= 2);
        Assert.True(table.Rows.Count >= 3);
    }

    // ─────────────────────────────────────────────────────────────────
    // Multiple Mappings
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ParsesMultipleMappingsInSegment()
    {
        var t = Core.Odin.ParseTransform(@"
{$}
odin = ""1.0.0""
transform = ""1.0.0""
direction = ""json->json""
target.format = ""json""

{Person}
First = ""@.first""
Last = ""@.last""
Age = ""@.age""
");
        Assert.True(t.Segments[0].Mappings.Count >= 3);
        Assert.Equal("First", t.Segments[0].Mappings[0].Target);
        Assert.Equal("Last", t.Segments[0].Mappings[1].Target);
        Assert.Equal("Age", t.Segments[0].Mappings[2].Target);
    }

    [Fact]
    public void ParsesMixedExpressionTypes()
    {
        var t = Core.Odin.ParseTransform(@"
{$}
odin = ""1.0.0""
transform = ""1.0.0""
direction = ""json->json""
target.format = ""json""

{Data}
Name = ""@.name""
Upper = ""%upper @.name""
Version = ""1.0""
");
        var mappings = t.Segments[0].Mappings;
        Assert.True(mappings.Count >= 3);
        Assert.IsType<CopyExpression>(mappings[0].Expression);
        Assert.IsType<TransformExpression>(mappings[1].Expression);
    }

    // ─────────────────────────────────────────────────────────────────
    // Target Options
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ParsesTargetOptions()
    {
        var t = Core.Odin.ParseTransform(@"
{$}
odin = ""1.0.0""
transform = ""1.0.0""
direction = ""json->csv""
target.format = ""csv""
target.delimiter = ""|""
target.includeHeader = ""true""
");
        Assert.Equal("csv", t.Target.Format);
        Assert.True(t.Target.Options.ContainsKey("delimiter"));
        Assert.Equal("|", t.Target.Options["delimiter"]);
    }

    // ─────────────────────────────────────────────────────────────────
    // Discriminator Parsing
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ParsesSourceDiscriminatorPosition()
    {
        var t = Core.Odin.ParseTransform(@"
{$}
odin = ""1.0.0""
transform = ""1.0.0""
direction = ""fixed-width->json""
source.format = ""fixed-width""
source.discriminator.type = ""position""
source.discriminator.pos = ""0""
source.discriminator.len = ""2""
target.format = ""json""
");
        Assert.NotNull(t.Source);
        Assert.NotNull(t.Source!.Discriminator);
        Assert.Equal(DiscriminatorType.Position, t.Source.Discriminator!.Type);
        Assert.Equal(0, t.Source.Discriminator.Pos);
        Assert.Equal(2, t.Source.Discriminator.Len);
    }

    [Fact]
    public void ParsesSourceDiscriminatorField()
    {
        var t = Core.Odin.ParseTransform(@"
{$}
odin = ""1.0.0""
transform = ""1.0.0""
direction = ""csv->json""
source.format = ""csv""
source.discriminator.type = ""field""
source.discriminator.field = ""0""
target.format = ""json""
");
        Assert.NotNull(t.Source);
        Assert.NotNull(t.Source!.Discriminator);
        Assert.Equal(DiscriminatorType.Field, t.Source.Discriminator!.Type);
        Assert.Equal(0, t.Source.Discriminator.Field);
    }
}
