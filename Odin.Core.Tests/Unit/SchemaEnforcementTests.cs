using System.Linq;
using Odin.Core;
using Odin.Core.Types;
using Xunit;

namespace Odin.Core.Tests.Unit;

/// <summary>
/// Schema-validation enforcement: invariant evaluation, currency and percent
/// bounds, override restrictiveness, intersection conflicts, tabular columns,
/// and default-value rules.
/// </summary>
public class SchemaEnforcementTests
{
    private const string Header = "{$}\nodin = \"1.0.0\"\nschema = \"1.0.0\"\n\n";

    private static ValidationResult Run(string schemaText, string inputText)
    {
        var schema = Core.Odin.ParseSchema(Header + schemaText);
        var doc = Core.Odin.Parse(inputText.Length == 0 ? "{root}\nx = \"\"" : inputText);
        return Core.Odin.Validate(doc, schema);
    }

    private static string[] CodesAt(ValidationResult result, string path) =>
        result.Errors.Where(e => e.Path == path).Select(e => e.Code).ToArray();

    // ─────────────────────────────────────────────────────────────────
    // Invariant expression evaluation
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Invariant_ThreeTermAdditive_Passes()
    {
        var r = Run(
            "{order}\nsubtotal = #$\ntax = #$\nshipping = #$\ntotal = #$\n:invariant total = subtotal + tax + shipping",
            "{order}\nsubtotal = #$10.00\ntax = #$1.00\nshipping = #$2.00\ntotal = #$13.00");
        Assert.True(r.IsValid);
    }

    [Fact]
    public void Invariant_ThreeTermAdditive_FailsWhenInconsistent()
    {
        var r = Run(
            "{order}\nsubtotal = #$\ntax = #$\nshipping = #$\ntotal = #$\n:invariant total = subtotal + tax + shipping",
            "{order}\nsubtotal = #$10.00\ntax = #$1.00\nshipping = #$2.00\ntotal = #$99.00");
        Assert.False(r.IsValid);
        Assert.Contains("V008", CodesAt(r, "order"));
    }

    [Fact]
    public void Invariant_ParenthesesAndPrecedence()
    {
        const string schema =
            "{discount}\nsubtotal = #$\npercentage = #\nfixed_amount = #$\ntotal = #$\n:invariant total = subtotal - (subtotal * percentage / 100) - fixed_amount";
        Assert.True(Run(schema, "{discount}\nsubtotal = #$100.00\npercentage = #10\nfixed_amount = #$5.00\ntotal = #$85.00").IsValid);
        Assert.False(Run(schema, "{discount}\nsubtotal = #$100.00\npercentage = #10\nfixed_amount = #$5.00\ntotal = #$80.00").IsValid);
    }

    [Fact]
    public void Invariant_LogicalOr()
    {
        const string schema = "{discount}\npercentage = #\nfixed_amount = #$\n:invariant percentage == 0 || fixed_amount == 0";
        Assert.True(Run(schema, "{discount}\npercentage = #0\nfixed_amount = #$5.00").IsValid);
        Assert.False(Run(schema, "{discount}\npercentage = #10\nfixed_amount = #$5.00").IsValid);
    }

    [Fact]
    public void Invariant_LogicalAndAndNegation()
    {
        const string schema = "{f}\na = #\nb = #\n:invariant !(a > 10) && b < 5";
        Assert.True(Run(schema, "{f}\na = #3\nb = #2").IsValid);
        Assert.False(Run(schema, "{f}\na = #20\nb = #2").IsValid);
    }

    [Fact]
    public void Invariant_Modulo()
    {
        const string schema = "{n}\nx = ##\n:invariant x % 2 == 0";
        Assert.True(Run(schema, "{n}\nx = ##4").IsValid);
        Assert.False(Run(schema, "{n}\nx = ##5").IsValid);
    }

    [Fact]
    public void Invariant_TemporalOperands()
    {
        const string schema = "{r}\nstart = date\nend = date\n:invariant end >= start";
        Assert.True(Run(schema, "{r}\nstart = 2020-01-01\nend = 2020-02-01").IsValid);
        Assert.False(Run(schema, "{r}\nstart = 2020-03-01\nend = 2020-02-01").IsValid);
    }

    [Fact]
    public void Invariant_NullOperand_IsViolation()
    {
        var r = Run(
            "{o}\ntotal = #$\nsubtotal = #$\ntax = ~#$\n:invariant total = subtotal + tax",
            "{o}\ntotal = #$10.00\nsubtotal = #$10.00\ntax = ~");
        Assert.False(r.IsValid);
        Assert.Contains("V008", CodesAt(r, "o"));
    }

    [Fact]
    public void Invariant_AbsentOperand_DoesNotApply()
    {
        var r = Run(
            "{o}\ntotal = #$\nsubtotal = #$\ntax = #$\n:invariant total = subtotal + tax",
            "{o}\ntotal = #$10.00");
        Assert.True(r.IsValid);
    }

    [Fact]
    public void Invariant_Malformed_IsReportedAsV008()
    {
        var r = Run("{o}\nx = #\n:invariant x + + ", "{o}\nx = #1");
        Assert.False(r.IsValid);
        Assert.Contains("V008", CodesAt(r, "o"));
    }

    // ─────────────────────────────────────────────────────────────────
    // Currency decimal-place enforcement
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Currency_AcceptsDeclaredPlaces()
    {
        Assert.True(Run("{w}\nbtc = #$.8", "{w}\nbtc = #$1.00000000").IsValid);
    }

    [Fact]
    public void Currency_RejectsTooFewPlaces()
    {
        var r = Run("{w}\nbtc = #$.8", "{w}\nbtc = #$1.00");
        Assert.False(r.IsValid);
        Assert.Contains("V003", CodesAt(r, "w.btc"));
    }

    [Fact]
    public void Currency_DefaultsToTwoPlaces()
    {
        Assert.True(Run("{w}\nprice = #$", "{w}\nprice = #$9.99").IsValid);
        Assert.False(Run("{w}\nprice = #$", "{w}\nprice = #$9.999").IsValid);
    }

    // ─────────────────────────────────────────────────────────────────
    // Percent bounds enforcement
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Percent_AcceptsInRange()
    {
        Assert.True(Run("{r}\nrate = #%:(0..1)", "{r}\nrate = #%0.5").IsValid);
    }

    [Fact]
    public void Percent_RejectsOutOfRange()
    {
        var r = Run("{r}\nrate = #%:(0..1)", "{r}\nrate = #%1.5");
        Assert.False(r.IsValid);
        Assert.Contains("V003", CodesAt(r, "r.rate"));
    }

    [Fact]
    public void Percent_RejectsBelowMinimum()
    {
        Assert.False(Run("{r}\nrate = #%:(0.1..1)", "{r}\nrate = #%0.05").IsValid);
    }

    // ─────────────────────────────────────────────────────────────────
    // Override restrictiveness
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Override_NarrowsBounds_Accepted()
    {
        Assert.True(Run("{@base}\namount = #$:(0..1000)\n\n{@narrow}\n= @base :override\namount = #$:(0..100)", "").IsValid);
    }

    [Fact]
    public void Override_WidensBounds_Rejected()
    {
        var r = Run("{@base}\namount = #$:(0..100)\n\n{@wide}\n= @base :override\namount = #$:(0..1000)", "");
        Assert.False(r.IsValid);
        Assert.Contains("V017", CodesAt(r, "@wide.amount"));
    }

    [Fact]
    public void Override_OptionalToRequired_AllowedNotReverse()
    {
        Assert.True(Run("{@base}\nname =\n\n{@d}\n= @base :override\nname = !", "").IsValid);
        var r = Run("{@base}\nname = !\n\n{@d}\n= @base :override\nname =", "");
        Assert.False(r.IsValid);
        Assert.Contains("V017", CodesAt(r, "@d.name"));
    }

    [Fact]
    public void Override_RemoveNullable_AllowedNotAdd()
    {
        Assert.True(Run("{@base}\nx = ~#\n\n{@d}\n= @base :override\nx = #", "").IsValid);
        var r = Run("{@base}\nx = #\n\n{@d}\n= @base :override\nx = ~#", "");
        Assert.False(r.IsValid);
        Assert.Contains("V017", CodesAt(r, "@d.x"));
    }

    [Fact]
    public void Override_ChangeBaseType_Rejected()
    {
        var r = Run("{@base}\nx = #\n\n{@d}\n= @base :override\nx =", "");
        Assert.False(r.IsValid);
        Assert.Contains("V017", CodesAt(r, "@d.x"));
    }

    [Fact]
    public void Override_PathLevelComposition_Enforced()
    {
        var r = Run("{@base}\namount = #$:(0..100)\n\n{order}\n= @base :override\namount = #$:(0..1000)", "");
        Assert.False(r.IsValid);
        Assert.Contains("V017", CodesAt(r, "order.amount"));
    }

    [Fact]
    public void Override_DoesNotFlagUntouchedFields()
    {
        Assert.True(Run("{@base}\na = #$:(0..100)\nb = !\n\n{@d}\n= @base :override\na = #$:(0..50)", "").IsValid);
    }

    // ─────────────────────────────────────────────────────────────────
    // Intersection field conflicts
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Intersection_ConflictingFields_Rejected()
    {
        var r = Run("{@a}\nx = !\n\n{@b}\nx = !##\n\n{cust}\n= @a & @b", "{cust}\nx = ##5");
        Assert.False(r.IsValid);
        Assert.Contains("V017", CodesAt(r, "@cust.x"));
    }

    [Fact]
    public void Intersection_DisjointOrIdentical_Accepted()
    {
        Assert.True(Run("{@a}\nx = !\nname = !\n\n{@b}\nx = !\nage = !##\n\n{cust}\n= @a & @b",
            "{cust}\nx = \"hi\"\nname = \"n\"\nage = ##5").IsValid);
    }

    [Fact]
    public void Intersection_ThreeWayConflict_Reported()
    {
        var r = Run("{@a}\nx = !\n\n{@b}\ny = !\n\n{@c}\nx = !##\n\n{cust}\n= @a & @b & @c", "{cust}\nx = \"hi\"\ny = \"z\"");
        Assert.False(r.IsValid);
        Assert.Contains("V017", CodesAt(r, "@cust.x"));
    }

    // ─────────────────────────────────────────────────────────────────
    // Tabular column rules
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Tabular_PrimitiveColumns_Accepted()
    {
        Assert.True(Run("{contacts[] : name, email}\nname = !\nemail = !", "{contacts[0]}\nname = \"a\"\nemail = \"b\"").IsValid);
    }

    [Fact]
    public void Tabular_TypeRefColumn_Rejected()
    {
        var r = Run("{@addr}\nline1 = !\n\n{customers[] : name, address}\nname = !\naddress = @addr", "{customers[0]}\nname = \"a\"");
        Assert.False(r.IsValid);
        Assert.Contains("V017", CodesAt(r, "customers[].address"));
    }

    [Fact]
    public void Tabular_SingleLevelColumns_Accepted()
    {
        Assert.True(Run("{rows[] : id, label}\nid = !##\nlabel = !", "{rows[0]}\nid = ##1\nlabel = \"x\"").IsValid);
    }

    // ─────────────────────────────────────────────────────────────────
    // Default value rules
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Default_WithinConstraintsOnOptional_Accepted()
    {
        Assert.True(Run("{root}\npriority = ##:(1..5) ##3", "").IsValid);
    }

    [Fact]
    public void Default_OnRequiredField_Rejected()
    {
        var r = Run("{root}\nstatus = !(\"a\", \"b\") \"a\"", "{root}\nstatus = \"a\"");
        Assert.False(r.IsValid);
        Assert.Contains("V017", CodesAt(r, "root.status"));
    }

    [Fact]
    public void Default_ViolatesBounds_Rejected()
    {
        var r = Run("{root}\npriority = ##:(1..5) ##9", "");
        Assert.False(r.IsValid);
        Assert.Contains("V017", CodesAt(r, "root.priority"));
    }

    [Fact]
    public void Default_OutsideEnum_Rejected()
    {
        var r = Run("{root}\nstatus = (\"a\", \"b\") \"c\"", "");
        Assert.False(r.IsValid);
        Assert.Contains("V017", CodesAt(r, "root.status"));
    }

    [Fact]
    public void Default_MatchesEnum_Accepted()
    {
        Assert.True(Run("{root}\nstatus = (\"a\", \"b\") \"a\"", "").IsValid);
    }
}
