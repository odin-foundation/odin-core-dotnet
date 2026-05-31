using System.Linq;
using Odin.Core;
using Odin.Core.Types;
using Xunit;

namespace Odin.Core.Tests.Unit;

/// <summary>
/// Unit tests covering schema-parser and validator conformance: type intersections,
/// temporal range bounds, the percent type, typed defaults, union edge cases, glued
/// directives, pattern conditionals, field typeRefs, and invariant null operands.
/// </summary>
public class SchemaConformanceTests
{
    private const string Header = "{$}\nodin = \"1.0.0\"\nschema = \"1.0.0\"\n\n";

    private static SchemaField Field(OdinSchemaDefinition schema, string path)
    {
        Assert.True(schema.Fields.TryGetValue(path, out var f), $"field '{path}' not found");
        return f!;
    }

    // ── Fix 1: type intersection ─────────────────────────────────────────────

    [Fact]
    public void Intersection_StoresBothMembers()
    {
        var schema = Core.Odin.ParseSchema(
            Header + "{@hasName}\nname = !\n\n{@hasAge}\nage = !##\n\n{customer}\n= @hasName & @hasAge");
        var comp = Field(schema, "customer._composition");
        Assert.IsType<TypeRefFieldType>(comp.FieldType);
        Assert.Equal("hasName&hasAge", ((TypeRefFieldType)comp.FieldType).Name);
    }

    [Fact]
    public void Intersection_AllPresent_Valid()
    {
        var schema = Core.Odin.ParseSchema(
            Header + "{@hasName}\nname = !\n\n{@hasAge}\nage = !##\n\n{customer}\n= @hasName & @hasAge");
        var doc = Core.Odin.Parse("{customer}\nname = \"Bob\"\nage = ##5");
        Assert.True(Core.Odin.Validate(doc, schema).IsValid);
    }

    [Fact]
    public void Intersection_MissingMemberField_FailsV001()
    {
        var schema = Core.Odin.ParseSchema(
            Header + "{@hasName}\nname = !\n\n{@hasAge}\nage = !##\n\n{customer}\n= @hasName & @hasAge");
        var doc = Core.Odin.Parse("{customer}\nname = \"Bob\"");
        var result = Core.Odin.Validate(doc, schema);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "V001" && e.Path == "customer.age");
    }

    [Fact]
    public void Intersection_UnresolvedMember_FailsV013()
    {
        var schema = Core.Odin.ParseSchema(
            Header + "{@hasName}\nname = !\n\n{customer}\n= @hasName & @doesNotExist");
        var doc = Core.Odin.Parse("{customer}\nname = \"Bob\"");
        var result = Core.Odin.Validate(doc, schema);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "V013");
    }

    // ── Fix 2: temporal range bounds ─────────────────────────────────────────

    [Fact]
    public void TemporalBounds_Preserved()
    {
        var schema = Core.Odin.ParseSchema(Header + "{root}\nd = date:(2020-06-15..2020-06-20)");
        var bounds = Field(schema, "root.d").Constraints.OfType<BoundsConstraint>().Single();
        Assert.Equal("2020-06-15", bounds.Min);
        Assert.Equal("2020-06-20", bounds.Max);
    }

    [Fact]
    public void TemporalBounds_InRange_Valid()
    {
        var schema = Core.Odin.ParseSchema(Header + "{root}\nd = date:(2020-06-15..2020-06-20)");
        var doc = Core.Odin.Parse("{root}\nd = 2020-06-17");
        Assert.True(Core.Odin.Validate(doc, schema).IsValid);
    }

    [Fact]
    public void TemporalBounds_BelowMin_FailsV003()
    {
        var schema = Core.Odin.ParseSchema(Header + "{root}\nd = date:(2020-06-15..2020-06-20)");
        var doc = Core.Odin.Parse("{root}\nd = 2020-06-10");
        var result = Core.Odin.Validate(doc, schema);
        Assert.Contains(result.Errors, e => e.Code == "V003" && e.Path == "root.d");
    }

    [Fact]
    public void TemporalBounds_AboveMax_FailsV003()
    {
        var schema = Core.Odin.ParseSchema(Header + "{root}\nd = date:(2020-06-15..2020-06-20)");
        var doc = Core.Odin.Parse("{root}\nd = 2020-06-25");
        var result = Core.Odin.Validate(doc, schema);
        Assert.Contains(result.Errors, e => e.Code == "V003" && e.Path == "root.d");
    }

    // ── Fix 3: percent type ──────────────────────────────────────────────────

    [Fact]
    public void Percent_FirstClassType()
    {
        var schema = Core.Odin.ParseSchema(Header + "{root}\ntax = #%");
        Assert.IsType<PercentFieldType>(Field(schema, "root.tax").FieldType);
    }

    [Fact]
    public void Percent_ValueValid()
    {
        var schema = Core.Odin.ParseSchema(Header + "{root}\ntax = #%");
        var doc = Core.Odin.Parse("{root}\ntax = #%0.15");
        Assert.True(Core.Odin.Validate(doc, schema).IsValid);
    }

    [Fact]
    public void Percent_TypeMismatch_FailsV002()
    {
        var schema = Core.Odin.ParseSchema(Header + "{root}\ntax = #%");
        var doc = Core.Odin.Parse("{root}\ntax = \"fifteen\"");
        var result = Core.Odin.Validate(doc, schema);
        Assert.Contains(result.Errors, e => e.Code == "V002" && e.Path == "root.tax");
    }

    // ── Fix 4: typed default values ──────────────────────────────────────────

    [Theory]
    [InlineData("a = ##3", "integer", 3.0)]
    [InlineData("b = #0.05", "number", 0.05)]
    [InlineData("c = #$5.00", "currency", 5.0)]
    [InlineData("p = #%0.15", "percent", 0.15)]
    public void TypedDefault_Captured(string line, string type, double value)
    {
        var schema = Core.Odin.ParseSchema(Header + "{root}\n" + line);
        var path = "root." + line.Substring(0, line.IndexOf(' '));
        var def = Field(schema, path).TypedDefault;
        Assert.NotNull(def);
        Assert.Equal(type, def!.Type);
        Assert.Equal(value, def.Number);
    }

    [Fact]
    public void ConstrainedDefault_Captured()
    {
        var schema = Core.Odin.ParseSchema(Header + "{root}\npriority = ##:(1..5) ##3");
        var field = Field(schema, "root.priority");
        Assert.IsType<IntegerFieldType>(field.FieldType);
        Assert.NotNull(field.TypedDefault);
        Assert.Equal("integer", field.TypedDefault!.Type);
        Assert.Equal(3.0, field.TypedDefault.Number);
        var bounds = field.Constraints.OfType<BoundsConstraint>().Single();
        Assert.Equal("1", bounds.Min);
        Assert.Equal("5", bounds.Max);
    }

    // ── Fix 5: union edge cases ──────────────────────────────────────────────

    [Fact]
    public void Union_DateTimestamp_KeepsBothMembers()
    {
        var schema = Core.Odin.ParseSchema(Header + "{root}\nu = date|timestamp");
        var union = Assert.IsType<UnionFieldType>(Field(schema, "root.u").FieldType);
        Assert.Contains(union.Types, t => t is DateFieldType);
        Assert.Contains(union.Types, t => t is TimestampFieldType);
    }

    [Fact]
    public void Union_NumberNull_KeepsBothMembers()
    {
        var schema = Core.Odin.ParseSchema(Header + "{root}\nn = #|~");
        var union = Assert.IsType<UnionFieldType>(Field(schema, "root.n").FieldType);
        Assert.Contains(union.Types, t => t is NumberFieldType);
        Assert.Contains(union.Types, t => t is NullFieldType);
    }

    [Fact]
    public void Union_NullMember_AcceptsNull()
    {
        var schema = Core.Odin.ParseSchema(Header + "{root}\nn = #|~");
        var doc = Core.Odin.Parse("{root}\nn = ~");
        Assert.True(Core.Odin.Validate(doc, schema).IsValid);
    }

    [Fact]
    public void Union_DateTimestamp_AcceptsTimestamp()
    {
        var schema = Core.Odin.ParseSchema(Header + "{root}\nu = date|timestamp");
        var doc = Core.Odin.Parse("{root}\nu = 2020-06-17T10:00:00Z");
        Assert.True(Core.Odin.Validate(doc, schema).IsValid);
    }

    // ── Fix 6: :if after a pattern constraint ────────────────────────────────

    [Fact]
    public void PatternThenIf_Parsed()
    {
        var schema = Core.Odin.ParseSchema(Header + "{root}\nfield = !:/^[a-z]+$/:if method = paypal");
        var field = Field(schema, "root.field");
        Assert.True(field.Required);
        Assert.Contains(field.Constraints, c => c is PatternConstraint p && p.PatternValue == "^[a-z]+$");
        var cond = Assert.Single(field.Conditionals);
        Assert.Equal("method", cond.Field);
        Assert.Equal(ConditionalOperator.Eq, cond.Operator);
        Assert.Equal("paypal", cond.CondValue.AsString());
    }

    [Fact]
    public void PatternConditional_RequiredWhenMet_FailsV010()
    {
        var schema = Core.Odin.ParseSchema(
            Header + "{root}\nfield = !:/^[a-z]+$/:if method = paypal\nmethod = ");
        var doc = Core.Odin.Parse("{root}\nmethod = \"paypal\"");
        var result = Core.Odin.Validate(doc, schema);
        Assert.Contains(result.Errors, e => e.Code == "V010" && e.Path == "root.field");
    }

    [Fact]
    public void PatternConditional_NotRequiredWhenUnmet_Valid()
    {
        var schema = Core.Odin.ParseSchema(
            Header + "{root}\nfield = !:/^[a-z]+$/:if method = paypal\nmethod = ");
        var doc = Core.Odin.Parse("{root}\nmethod = \"stripe\"");
        Assert.True(Core.Odin.Validate(doc, schema).IsValid);
    }

    [Fact]
    public void PatternConditional_PatternEnforced_FailsV004()
    {
        var schema = Core.Odin.ParseSchema(
            Header + "{root}\nfield = !:/^[a-z]+$/:if method = paypal\nmethod = ");
        var doc = Core.Odin.Parse("{root}\nfield = \"ABC123\"\nmethod = \"paypal\"");
        var result = Core.Odin.Validate(doc, schema);
        Assert.Contains(result.Errors, e => e.Code == "V004" && e.Path == "root.field");
    }

    // ── Fix 7: glued directives on a temporal type ───────────────────────────

    [Fact]
    public void TemporalGluedImmutable_KeepsTypeAndFlag()
    {
        var schema = Core.Odin.ParseSchema(Header + "{root}\ncreated_at = !timestamp:immutable");
        var field = Field(schema, "root.created_at");
        Assert.IsType<TimestampFieldType>(field.FieldType);
        Assert.True(field.Required);
        Assert.True(field.Immutable);
    }

    [Fact]
    public void TemporalGluedComputed_KeepsTypeAndFlag()
    {
        var schema = Core.Odin.ParseSchema(Header + "{root}\nstamp = date:computed");
        var field = Field(schema, "root.stamp");
        Assert.IsType<DateFieldType>(field.FieldType);
        Assert.True(field.Computed);
    }

    // ── Fix 8: field-level typeRef recursive validation ──────────────────────

    [Fact]
    public void FieldTypeRef_MissingNestedRequired_FailsV001()
    {
        var schema = Core.Odin.ParseSchema(
            Header + "{@address}\nstreet = !\ncity = !\n\n{customer}\nname = !\nbilling = @address");
        var doc = Core.Odin.Parse("{customer}\nname = \"X\"\nbilling.street = \"Main\"");
        var result = Core.Odin.Validate(doc, schema);
        Assert.Contains(result.Errors, e => e.Code == "V001" && e.Path == "customer.billing.city");
    }

    [Fact]
    public void FieldTypeRef_Absent_Optional()
    {
        var schema = Core.Odin.ParseSchema(
            Header + "{@address}\nstreet = !\ncity = !\n\n{customer}\nname = !\nbilling = @address");
        var doc = Core.Odin.Parse("{customer}\nname = \"X\"");
        Assert.True(Core.Odin.Validate(doc, schema).IsValid);
    }

    [Fact]
    public void FieldTypeRef_Complete_Valid()
    {
        var schema = Core.Odin.ParseSchema(
            Header + "{@address}\nstreet = !\ncity = !\n\n{customer}\nname = !\nbilling = @address");
        var doc = Core.Odin.Parse("{customer}\nname = \"X\"\nbilling.street = \"Main\"\nbilling.city = \"NYC\"");
        Assert.True(Core.Odin.Validate(doc, schema).IsValid);
    }

    // ── Fix 9: invariant null operands ───────────────────────────────────────

    [Fact]
    public void InvariantNullOperand_FailsV008()
    {
        var schema = Core.Odin.ParseSchema(
            Header + "{order}\ntotal = #$\nsubtotal = #$\ntax = ~#$\n:invariant total = subtotal + tax");
        var doc = Core.Odin.Parse("{order}\ntotal = #$10.00\nsubtotal = #$10.00\ntax = ~");
        var result = Core.Odin.Validate(doc, schema);
        Assert.Contains(result.Errors, e => e.Code == "V008" && e.Path == "order");
    }

    [Fact]
    public void InvariantAllPresent_Valid()
    {
        var schema = Core.Odin.ParseSchema(
            Header + "{order}\ntotal = #$\nsubtotal = #$\ntax = #$\n:invariant total = subtotal + tax");
        var doc = Core.Odin.Parse("{order}\ntotal = #$12.00\nsubtotal = #$10.00\ntax = #$2.00");
        Assert.True(Core.Odin.Validate(doc, schema).IsValid);
    }

    [Fact]
    public void InvariantComparisonNullOperand_FailsV008()
    {
        var schema = Core.Odin.ParseSchema(
            Header + "{range}\nstart = ~#\nend = ~#\n:invariant end >= start");
        var doc = Core.Odin.Parse("{range}\nend = #5\nstart = ~");
        var result = Core.Odin.Validate(doc, schema);
        Assert.Contains(result.Errors, e => e.Code == "V008" && e.Path == "range");
    }
}
