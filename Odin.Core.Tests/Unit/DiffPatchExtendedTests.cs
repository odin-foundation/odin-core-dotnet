using System;
using Odin.Core;
using Odin.Core.Diff;
using Odin.Core.Types;
using Xunit;

namespace Odin.Core.Tests.Unit;

public class DiffPatchExtendedTests
{
    // ─────────────────────────────────────────────────────────────────
    // Identical Documents
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Diff_IdenticalDocuments_Empty()
    {
        var doc = Core.Odin.Parse("name = \"Alice\"\nage = ##30");
        var diff = Core.Odin.Diff(doc, doc);
        Assert.True(diff.IsEmpty);
    }

    [Fact]
    public void Diff_BothEmpty_Empty()
    {
        var a = OdinDocument.Empty();
        var b = OdinDocument.Empty();
        var diff = Core.Odin.Diff(a, b);
        Assert.True(diff.IsEmpty);
    }

    // ─────────────────────────────────────────────────────────────────
    // Added Fields
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Diff_FieldAdded()
    {
        var a = Core.Odin.Parse("name = \"Alice\"");
        var b = Core.Odin.Parse("name = \"Alice\"\nage = ##30");
        var diff = Core.Odin.Diff(a, b);
        Assert.False(diff.IsEmpty);
        Assert.Single(diff.Added);
        Assert.Equal("age", diff.Added[0].Path);
    }

    [Fact]
    public void Diff_MultipleFieldsAdded()
    {
        var a = Core.Odin.Parse("name = \"Alice\"");
        var b = Core.Odin.Parse("name = \"Alice\"\nage = ##30\nactive = true");
        var diff = Core.Odin.Diff(a, b);
        Assert.Equal(2, diff.Added.Count);
    }

    // ─────────────────────────────────────────────────────────────────
    // Removed Fields
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Diff_FieldRemoved()
    {
        var a = Core.Odin.Parse("name = \"Alice\"\nage = ##30");
        var b = Core.Odin.Parse("name = \"Alice\"");
        var diff = Core.Odin.Diff(a, b);
        Assert.Single(diff.Removed);
        Assert.Equal("age", diff.Removed[0].Path);
    }

    [Fact]
    public void Diff_MultipleFieldsRemoved()
    {
        var a = Core.Odin.Parse("name = \"Alice\"\nage = ##30\nactive = true");
        var b = Core.Odin.Parse("name = \"Alice\"");
        var diff = Core.Odin.Diff(a, b);
        Assert.Equal(2, diff.Removed.Count);
    }

    // ─────────────────────────────────────────────────────────────────
    // Changed Fields
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Diff_StringValueChanged()
    {
        var a = Core.Odin.Parse("name = \"Alice\"");
        var b = Core.Odin.Parse("name = \"Bob\"");
        var diff = Core.Odin.Diff(a, b);
        Assert.Single(diff.Changed);
        Assert.Equal("name", diff.Changed[0].Path);
    }

    [Fact]
    public void Diff_IntegerValueChanged()
    {
        var a = Core.Odin.Parse("x = ##1");
        var b = Core.Odin.Parse("x = ##2");
        var diff = Core.Odin.Diff(a, b);
        Assert.Single(diff.Changed);
    }

    [Fact]
    public void Diff_TypeChanged()
    {
        var a = Core.Odin.Parse("x = ##42");
        var b = Core.Odin.Parse("x = \"42\"");
        var diff = Core.Odin.Diff(a, b);
        Assert.Single(diff.Changed);
    }

    [Fact]
    public void Diff_BooleanChanged()
    {
        var a = Core.Odin.Parse("x = true");
        var b = Core.Odin.Parse("x = false");
        var diff = Core.Odin.Diff(a, b);
        Assert.Single(diff.Changed);
    }

    [Fact]
    public void Diff_NullToValue()
    {
        var a = Core.Odin.Parse("x = ~");
        var b = Core.Odin.Parse("x = ##42");
        var diff = Core.Odin.Diff(a, b);
        Assert.Single(diff.Changed);
    }

    [Fact]
    public void Diff_ValueToNull()
    {
        var a = Core.Odin.Parse("x = ##42");
        var b = Core.Odin.Parse("x = ~");
        var diff = Core.Odin.Diff(a, b);
        Assert.Single(diff.Changed);
    }

    // ─────────────────────────────────────────────────────────────────
    // Moved Fields
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Diff_FieldMoved()
    {
        var a = Core.Odin.Builder()
            .SetString("oldName", "Alice")
            .Build();
        var b = Core.Odin.Builder()
            .SetString("newName", "Alice")
            .Build();
        var diff = Core.Odin.Diff(a, b);
        Assert.Single(diff.Moved);
        Assert.Equal("oldName", diff.Moved[0].FromPath);
        Assert.Equal("newName", diff.Moved[0].ToPath);
    }

    // ─────────────────────────────────────────────────────────────────
    // Patch: Apply Additions
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Patch_ApplyAddition()
    {
        var doc = Core.Odin.Parse("name = \"Alice\"");
        var diff = Core.Odin.Diff(doc, Core.Odin.Parse("name = \"Alice\"\nage = ##30"));
        var patched = Core.Odin.Patch(doc, diff);
        Assert.Equal(30L, patched.GetInteger("age"));
        Assert.Equal("Alice", patched.GetString("name"));
    }

    // ─────────────────────────────────────────────────────────────────
    // Patch: Apply Removals
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Patch_ApplyRemoval()
    {
        var doc = Core.Odin.Parse("name = \"Alice\"\nage = ##30");
        var diff = Core.Odin.Diff(doc, Core.Odin.Parse("name = \"Alice\""));
        var patched = Core.Odin.Patch(doc, diff);
        Assert.False(patched.Has("age"));
        Assert.True(patched.Has("name"));
    }

    // ─────────────────────────────────────────────────────────────────
    // Patch: Apply Changes
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Patch_ApplyChange()
    {
        var doc = Core.Odin.Parse("name = \"Alice\"");
        var target = Core.Odin.Parse("name = \"Bob\"");
        var diff = Core.Odin.Diff(doc, target);
        var patched = Core.Odin.Patch(doc, diff);
        Assert.Equal("Bob", patched.GetString("name"));
    }

    // ─────────────────────────────────────────────────────────────────
    // Diff -> Patch Round-Trip
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void DiffPatch_RoundTrip_Simple()
    {
        var a = Core.Odin.Parse("name = \"Alice\"\nage = ##30");
        var b = Core.Odin.Parse("name = \"Bob\"\nage = ##31\nactive = true");
        var diff = Core.Odin.Diff(a, b);
        var patched = Core.Odin.Patch(a, diff);
        Assert.Equal("Bob", patched.GetString("name"));
        Assert.Equal(31L, patched.GetInteger("age"));
        Assert.Equal(true, patched.GetBoolean("active"));
    }

    [Fact]
    public void DiffPatch_RoundTrip_WithRemoval()
    {
        var a = Core.Odin.Parse("name = \"Alice\"\nage = ##30\nextra = \"yes\"");
        var b = Core.Odin.Parse("name = \"Alice\"\nage = ##30");
        var diff = Core.Odin.Diff(a, b);
        var patched = Core.Odin.Patch(a, diff);
        Assert.False(patched.Has("extra"));
    }

    [Fact]
    public void DiffPatch_RoundTrip_AllTypes()
    {
        var a = Core.Odin.Parse("s = \"hello\"\ni = ##1\nn = #1.0\nb = true\nnull_val = ~");
        var b = Core.Odin.Parse("s = \"world\"\ni = ##2\nn = #2.0\nb = false\nnull_val = ##42");
        var diff = Core.Odin.Diff(a, b);
        var patched = Core.Odin.Patch(a, diff);
        Assert.Equal("world", patched.GetString("s"));
        Assert.Equal(2L, patched.GetInteger("i"));
        Assert.Equal(false, patched.GetBoolean("b"));
    }

    [Fact]
    public void DiffPatch_RoundTrip_EmptyDiff()
    {
        var doc = Core.Odin.Parse("name = \"Alice\"");
        var diff = Core.Odin.Diff(doc, doc);
        var patched = Core.Odin.Patch(doc, diff);
        Assert.Equal("Alice", patched.GetString("name"));
    }

    // ─────────────────────────────────────────────────────────────────
    // Patch Error Cases
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Patch_RemoveNonexistent_Throws()
    {
        var doc = Core.Odin.Parse("name = \"Alice\"");
        var diff = new OdinDiff
        {
            Removed = new[] { new DiffEntry("missing", OdinValues.String("x")) }
        };
        Assert.Throws<OdinPatchException>(() => Core.Odin.Patch(doc, diff));
    }

    [Fact]
    public void Patch_ChangeNonexistent_Throws()
    {
        var doc = Core.Odin.Parse("name = \"Alice\"");
        var diff = new OdinDiff
        {
            Changed = new[] { new DiffChange("missing", OdinValues.String("a"), OdinValues.String("b")) }
        };
        Assert.Throws<OdinPatchException>(() => Core.Odin.Patch(doc, diff));
    }

    [Fact]
    public void Patch_MoveNonexistent_Throws()
    {
        var doc = Core.Odin.Parse("name = \"Alice\"");
        var diff = new OdinDiff
        {
            Moved = new[] { new DiffMove("missing", "newPath", OdinValues.String("x")) }
        };
        Assert.Throws<OdinPatchException>(() => Core.Odin.Patch(doc, diff));
    }

    // ─────────────────────────────────────────────────────────────────
    // PatchError
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void PatchError_ToStringContainsPath()
    {
        var err = new PatchError("test error", "some.path");
        var str = err.ToString();
        Assert.Contains("some.path", str);
        Assert.Contains("test error", str);
    }

    // ─────────────────────────────────────────────────────────────────
    // Diff Properties
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void DiffEntry_HasPathAndValue()
    {
        var entry = new DiffEntry("name", OdinValues.String("Alice"));
        Assert.Equal("name", entry.Path);
        Assert.Equal("Alice", entry.Value.AsString());
    }

    [Fact]
    public void DiffChange_HasOldAndNewValue()
    {
        var change = new DiffChange("x", OdinValues.Integer(1), OdinValues.Integer(2));
        Assert.Equal("x", change.Path);
        Assert.Equal(1L, change.OldValue.AsInt64());
        Assert.Equal(2L, change.NewValue.AsInt64());
    }

    [Fact]
    public void DiffMove_HasFromAndToPath()
    {
        var move = new DiffMove("old", "new", OdinValues.String("val"));
        Assert.Equal("old", move.FromPath);
        Assert.Equal("new", move.ToPath);
    }

    // ─────────────────────────────────────────────────────────────────
    // Complex Diff Scenarios
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Diff_MultipleSections()
    {
        var a = Core.Odin.Parse("{Customer}\nName = \"Alice\"\nAge = ##30");
        var b = Core.Odin.Parse("{Customer}\nName = \"Bob\"\nAge = ##31");
        var diff = Core.Odin.Diff(a, b);
        Assert.Equal(2, diff.Changed.Count);
    }

    [Fact]
    public void DiffPatch_RoundTrip_WithSections()
    {
        var a = Core.Odin.Parse("{Customer}\nName = \"Alice\"");
        var b = Core.Odin.Parse("{Customer}\nName = \"Bob\"\nEmail = \"bob@test.com\"");
        var diff = Core.Odin.Diff(a, b);
        var patched = Core.Odin.Patch(a, diff);
        Assert.Equal("Bob", patched.GetString("Customer.Name"));
        Assert.Equal("bob@test.com", patched.GetString("Customer.Email"));
    }

    [Fact]
    public void DiffPatch_RoundTrip_FromEmpty()
    {
        var a = OdinDocument.Empty();
        var b = Core.Odin.Parse("name = \"Alice\"\nage = ##30");
        var diff = Core.Odin.Diff(a, b);
        var patched = Core.Odin.Patch(a, diff);
        Assert.Equal("Alice", patched.GetString("name"));
        Assert.Equal(30L, patched.GetInteger("age"));
    }

    [Fact]
    public void DiffPatch_RoundTrip_ToEmpty()
    {
        var a = Core.Odin.Parse("name = \"Alice\"\nage = ##30");
        var b = OdinDocument.Empty();
        var diff = Core.Odin.Diff(a, b);
        var patched = Core.Odin.Patch(a, diff);
        Assert.Empty(patched.Assignments);
    }

    [Fact]
    public void Diff_CurrencyValues()
    {
        var a = Core.Odin.Parse("price = #$99.99");
        var b = Core.Odin.Parse("price = #$149.99");
        var diff = Core.Odin.Diff(a, b);
        Assert.Single(diff.Changed);
    }

    [Fact]
    public void Diff_ReferenceValues()
    {
        var a = Core.Odin.Parse("ref = @path1");
        var b = Core.Odin.Parse("ref = @path2");
        var diff = Core.Odin.Diff(a, b);
        Assert.Single(diff.Changed);
    }

    [Fact]
    public void Diff_DateValues()
    {
        var a = Core.Odin.Parse("dob = 2024-01-01");
        var b = Core.Odin.Parse("dob = 2024-06-15");
        var diff = Core.Odin.Diff(a, b);
        Assert.Single(diff.Changed);
    }

    [Fact]
    public void Diff_PercentValues()
    {
        var a = Core.Odin.Parse("rate = #%10");
        var b = Core.Odin.Parse("rate = #%20");
        var diff = Core.Odin.Diff(a, b);
        Assert.Single(diff.Changed);
    }

    // ─────────────────────────────────────────────────────────────────
    // Multi-field diff/patch round-trips
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void DiffPatch_ManyFieldsAdded()
    {
        var a = Core.Odin.Parse("x = ##1");
        var bBuilder = Core.Odin.Builder().SetInteger("x", 1);
        for (int i = 0; i < 20; i++)
            bBuilder.SetInteger($"f{i}", i);
        var b = bBuilder.Build();
        var diff = Core.Odin.Diff(a, b);
        Assert.Equal(20, diff.Added.Count);
        var patched = Core.Odin.Patch(a, diff);
        Assert.Equal(10L, patched.GetInteger("f10"));
    }

    [Fact]
    public void DiffPatch_ManyFieldsRemoved()
    {
        var aBuilder = Core.Odin.Builder().SetInteger("keep", 1);
        for (int i = 0; i < 20; i++)
            aBuilder.SetInteger($"remove{i}", i);
        var a = aBuilder.Build();
        var b = Core.Odin.Parse("keep = ##1");
        var diff = Core.Odin.Diff(a, b);
        Assert.Equal(20, diff.Removed.Count);
        var patched = Core.Odin.Patch(a, diff);
        Assert.True(patched.Has("keep"));
        Assert.False(patched.Has("remove0"));
    }

    [Fact]
    public void DiffPatch_ManyFieldsChanged()
    {
        var aBuilder = Core.Odin.Builder();
        var bBuilder = Core.Odin.Builder();
        for (int i = 0; i < 10; i++)
        {
            aBuilder.SetInteger($"f{i}", i);
            bBuilder.SetInteger($"f{i}", i + 100);
        }
        var a = aBuilder.Build();
        var b = bBuilder.Build();
        var diff = Core.Odin.Diff(a, b);
        Assert.Equal(10, diff.Changed.Count);
        var patched = Core.Odin.Patch(a, diff);
        Assert.Equal(100L, patched.GetInteger("f0"));
        Assert.Equal(109L, patched.GetInteger("f9"));
    }

    [Fact]
    public void Diff_NullValues_Identical()
    {
        var a = Core.Odin.Parse("x = ~");
        var b = Core.Odin.Parse("x = ~");
        var diff = Core.Odin.Diff(a, b);
        Assert.True(diff.IsEmpty);
    }

    [Fact]
    public void Diff_BooleanValues_Identical()
    {
        var a = Core.Odin.Parse("x = true");
        var b = Core.Odin.Parse("x = true");
        var diff = Core.Odin.Diff(a, b);
        Assert.True(diff.IsEmpty);
    }

    [Fact]
    public void Diff_StringValues_Identical()
    {
        var a = Core.Odin.Parse("x = \"hello\"");
        var b = Core.Odin.Parse("x = \"hello\"");
        var diff = Core.Odin.Diff(a, b);
        Assert.True(diff.IsEmpty);
    }

    [Fact]
    public void Diff_IntegerValues_Identical()
    {
        var a = Core.Odin.Parse("x = ##42");
        var b = Core.Odin.Parse("x = ##42");
        var diff = Core.Odin.Diff(a, b);
        Assert.True(diff.IsEmpty);
    }

    [Fact]
    public void Diff_NumberValues_Identical()
    {
        var a = Core.Odin.Parse("x = #3.14");
        var b = Core.Odin.Parse("x = #3.14");
        var diff = Core.Odin.Diff(a, b);
        Assert.True(diff.IsEmpty);
    }

    [Fact]
    public void Diff_CurrencyValues_Identical()
    {
        var a = Core.Odin.Parse("x = #$99.99");
        var b = Core.Odin.Parse("x = #$99.99");
        var diff = Core.Odin.Diff(a, b);
        Assert.True(diff.IsEmpty);
    }

    [Fact]
    public void Diff_DateValues_Identical()
    {
        var a = Core.Odin.Parse("x = 2024-01-01");
        var b = Core.Odin.Parse("x = 2024-01-01");
        var diff = Core.Odin.Diff(a, b);
        Assert.True(diff.IsEmpty);
    }

    [Fact]
    public void Diff_ReferenceValues_Identical()
    {
        var a = Core.Odin.Parse("x = @path");
        var b = Core.Odin.Parse("x = @path");
        var diff = Core.Odin.Diff(a, b);
        Assert.True(diff.IsEmpty);
    }

    [Fact]
    public void DiffPatch_AddedToSameSection()
    {
        var a = Core.Odin.Parse("{Person}\nName = \"Alice\"");
        var b = Core.Odin.Parse("{Person}\nName = \"Alice\"\nAge = ##30\nEmail = \"a@b.com\"");
        var diff = Core.Odin.Diff(a, b);
        Assert.Equal(2, diff.Added.Count);
        var patched = Core.Odin.Patch(a, diff);
        Assert.Equal(30L, patched.GetInteger("Person.Age"));
        Assert.Equal("a@b.com", patched.GetString("Person.Email"));
    }

    [Fact]
    public void DiffPatch_MixedOperations()
    {
        var a = Core.Odin.Parse("keep = ##1\nchange = \"old\"\nremove = ##99");
        var b = Core.Odin.Parse("keep = ##1\nchange = \"new\"\nadd = true");
        var diff = Core.Odin.Diff(a, b);
        var patched = Core.Odin.Patch(a, diff);
        Assert.Equal(1L, patched.GetInteger("keep"));
        Assert.Equal("new", patched.GetString("change"));
        Assert.Equal(true, patched.GetBoolean("add"));
        Assert.False(patched.Has("remove"));
    }

    [Fact]
    public void OdinDiff_IsEmpty_DefaultConstruction()
    {
        var diff = new OdinDiff();
        Assert.True(diff.IsEmpty);
    }

    [Fact]
    public void Patch_EmptyDiff_ReturnsEquivalent()
    {
        var doc = Core.Odin.Parse("x = ##1\ny = \"hello\"");
        var diff = new OdinDiff();
        var patched = Core.Odin.Patch(doc, diff);
        Assert.Equal(1L, patched.GetInteger("x"));
        Assert.Equal("hello", patched.GetString("y"));
    }

    [Fact]
    public void DiffPatch_TimestampChanged()
    {
        var a = Core.Odin.Parse("ts = 2024-01-01T00:00:00Z");
        var b = Core.Odin.Parse("ts = 2024-06-15T12:00:00Z");
        var diff = Core.Odin.Diff(a, b);
        Assert.Single(diff.Changed);
        var patched = Core.Odin.Patch(a, diff);
        Assert.True(patched.Get("ts")!.IsTimestamp);
    }

    [Fact]
    public void DiffPatch_BinaryChanged()
    {
        var a = Core.Odin.Parse("data = ^SGVsbG8=");
        var b = Core.Odin.Parse("data = ^V29ybGQ=");
        var diff = Core.Odin.Diff(a, b);
        Assert.Single(diff.Changed);
    }

    // ─────────────────────────────────────────────────────────────────
    // Diff with sections
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Diff_AddedSection()
    {
        var a = Core.Odin.Parse("x = ##1");
        var b = Core.Odin.Parse("x = ##1\n{S}\ny = ##2");
        var diff = Core.Odin.Diff(a, b);
        Assert.Single(diff.Added);
        Assert.Equal("S.y", diff.Added[0].Path);
    }

    [Fact]
    public void Diff_RemovedSection()
    {
        var a = Core.Odin.Parse("x = ##1\n{S}\ny = ##2");
        var b = Core.Odin.Parse("x = ##1");
        var diff = Core.Odin.Diff(a, b);
        Assert.Single(diff.Removed);
        Assert.Equal("S.y", diff.Removed[0].Path);
    }

    [Fact]
    public void Diff_ChangedInSection()
    {
        var a = Core.Odin.Parse("{S}\nx = ##1");
        var b = Core.Odin.Parse("{S}\nx = ##2");
        var diff = Core.Odin.Diff(a, b);
        Assert.Single(diff.Changed);
        Assert.Equal("S.x", diff.Changed[0].Path);
    }

    // ─────────────────────────────────────────────────────────────────
    // Patch preserves unrelated fields
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Patch_PreservesUnrelatedFields()
    {
        var a = Core.Odin.Parse("x = ##1\ny = ##2\nz = ##3");
        var b = Core.Odin.Parse("x = ##10\ny = ##2\nz = ##3");
        var diff = Core.Odin.Diff(a, b);
        var patched = Core.Odin.Patch(a, diff);
        Assert.Equal(10L, patched.GetInteger("x"));
        Assert.Equal(2L, patched.GetInteger("y"));
        Assert.Equal(3L, patched.GetInteger("z"));
    }

    [Fact]
    public void Patch_AddAndRemoveSimultaneously()
    {
        var a = Core.Odin.Parse("x = ##1\ny = ##2");
        var b = Core.Odin.Parse("y = ##2\nz = ##3");
        var diff = Core.Odin.Diff(a, b);
        var patched = Core.Odin.Patch(a, diff);
        Assert.False(patched.Has("x"));
        Assert.True(patched.Has("y"));
        Assert.True(patched.Has("z"));
    }

    [Fact]
    public void Diff_StringChange_OldAndNew()
    {
        var a = Core.Odin.Parse("name = \"Alice\"");
        var b = Core.Odin.Parse("name = \"Bob\"");
        var diff = Core.Odin.Diff(a, b);
        Assert.Single(diff.Changed);
        Assert.Equal("Alice", diff.Changed[0].OldValue.AsString());
        Assert.Equal("Bob", diff.Changed[0].NewValue.AsString());
    }

    [Fact]
    public void Diff_BooleanToString_TypeChange()
    {
        var a = Core.Odin.Parse("x = true");
        var b = Core.Odin.Parse("x = \"true\"");
        var diff = Core.Odin.Diff(a, b);
        Assert.Single(diff.Changed);
    }

    [Fact]
    public void Diff_NullToInteger_TypeChange()
    {
        var a = Core.Odin.Parse("x = ~");
        var b = Core.Odin.Parse("x = ##42");
        var diff = Core.Odin.Diff(a, b);
        Assert.Single(diff.Changed);
    }
}
