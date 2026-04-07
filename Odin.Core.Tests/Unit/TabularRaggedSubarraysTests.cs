// Tests for the tabular serializer's ragged sub-array rule. Records with
// variable-length indexed sub-arrays must fall through to the nested
// record-block form instead of padding to the union column width.

using System.Collections.Generic;
using Odin.Core.Transform;
using Odin.Core.Types;
using Xunit;
using OdinApi = Odin.Core.Odin;

namespace Odin.Core.Tests.Unit;

public class TabularRaggedSubarraysTests
{
    private static DynValue Rec(params (string key, DynValue val)[] fields)
    {
        var list = new List<KeyValuePair<string, DynValue>>(fields.Length);
        foreach (var (k, v) in fields)
            list.Add(new KeyValuePair<string, DynValue>(k, v));
        return DynValue.Object(list);
    }

    private static DynValue StrArr(params string[] items)
    {
        var list = new List<DynValue>(items.Length);
        foreach (var s in items) list.Add(DynValue.String(s));
        return DynValue.Array(list);
    }

    private static DynValue IntArr(params long[] items)
    {
        var list = new List<DynValue>(items.Length);
        foreach (var n in items) list.Add(DynValue.Integer(n));
        return DynValue.Array(list);
    }

    // ─────────────────────────────────────────────────────────────────
    // Rejects tabular when sub-arrays have variable length
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void RejectsTabular_RaggedStringSubArrays()
    {
        // Two records: 3 tags vs 1 tag. A naive tabular form would produce
        // tags[0], tags[1], tags[2] columns and pad row 2 with empty cells.
        var records = DynValue.Array(new List<DynValue>
        {
            Rec(("name", DynValue.String("Alice")),
                ("tags", StrArr("red", "green", "blue"))),
            Rec(("name", DynValue.String("Bob")),
                ("tags", StrArr("yellow"))),
        });
        var doc = Rec(("records", records));

        var text = OdinFormatter.Format(doc, null);

        // Must NOT produce a tabular header that lists indexed tag columns.
        Assert.DoesNotContain("tags[2]", text);
        Assert.DoesNotContain("tags[1]", text);
        Assert.DoesNotContain("tags[0]", text);
    }

    [Fact]
    public void RejectsTabular_RaggedNumericSubArrays()
    {
        var points = DynValue.Array(new List<DynValue>
        {
            Rec(("label", DynValue.String("A")), ("coords", IntArr(1, 2))),
            Rec(("label", DynValue.String("B")), ("coords", IntArr(3, 4, 5, 6))),
        });
        var doc = Rec(("points", points));

        var text = OdinFormatter.Format(doc, null);

        Assert.DoesNotContain("coords[3]", text);
        Assert.DoesNotContain("coords[2]", text);
    }

    [Fact]
    public void NoIndexedSubArrayColumns_RaggedSubArrays()
    {
        // The serializer must not declare a tabular header that lists indexed
        // sub-array columns when the sub-arrays are ragged. After Bug 2 fix
        // the nested arrays are also preserved as `{.field[] : ~}` sub-blocks
        // and survive a parser round-trip.
        var entries = DynValue.Array(new List<DynValue>
        {
            Rec(("slug", DynValue.String("a/one")),
                ("title", DynValue.String("One")),
                ("types", StrArr("alpha", "beta")),
                ("fields", StrArr("id", "name", "desc"))),
            Rec(("slug", DynValue.String("b/two")),
                ("title", DynValue.String("Two")),
                ("types", StrArr("gamma")),
                ("fields", StrArr("id"))),
        });
        var doc = Rec(("entries", entries));

        var text = OdinFormatter.Format(doc, null);

        // No padded indexed sub-array columns in the column header.
        // (The per-record record-block headers `{entries[0]}` etc. are fine.)
        Assert.DoesNotContain("types[0]", text);
        Assert.DoesNotContain("fields[2]", text);

        // Scalar fields preserved.
        Assert.Contains("a/one", text);
        Assert.Contains("b/two", text);

        // Round-trip: nested ragged arrays are now preserved (Bug 2 fix).
        var parsed = OdinApi.Parse(text);
        Assert.Equal("a/one", parsed.GetString("entries[0].slug"));
        Assert.Equal("One", parsed.GetString("entries[0].title"));
        Assert.Equal("alpha", parsed.GetString("entries[0].types[0]"));
        Assert.Equal("beta", parsed.GetString("entries[0].types[1]"));
        Assert.Equal("id", parsed.GetString("entries[0].fields[0]"));
        Assert.Equal("name", parsed.GetString("entries[0].fields[1]"));
        Assert.Equal("desc", parsed.GetString("entries[0].fields[2]"));
        Assert.Equal("b/two", parsed.GetString("entries[1].slug"));
        Assert.Equal("gamma", parsed.GetString("entries[1].types[0]"));
        Assert.Equal("id", parsed.GetString("entries[1].fields[0]"));
        Assert.False(parsed.Has("entries[0].types[2]"));
        Assert.False(parsed.Has("entries[1].fields[1]"));
    }

    // ─────────────────────────────────────────────────────────────────
    // Tabular still wins for the cases it should
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void PreservesTabular_DenseScalarOnlyRecords()
    {
        var rows = DynValue.Array(new List<DynValue>
        {
            Rec(("name", DynValue.String("Alice")), ("age", DynValue.Integer(30))),
            Rec(("name", DynValue.String("Bob")),   ("age", DynValue.Integer(25))),
        });
        var doc = Rec(("rows", rows));

        var text = OdinFormatter.Format(doc, null);

        // Tabular header should appear with both columns.
        Assert.Contains("rows[]", text);
        Assert.Contains("name", text);
        Assert.Contains("age", text);
    }

    [Fact]
    public void PreservesTabular_UniformWidthSubArrays()
    {
        // Both records have exactly two coords. After Bug 1 fix, the .NET
        // formatter must keep tabular here and emit indexed `coords[0]`,
        // `coords[1]` columns — matching the TS reference.
        var points = DynValue.Array(new List<DynValue>
        {
            Rec(("label", DynValue.String("A")), ("coords", IntArr(1, 2))),
            Rec(("label", DynValue.String("B")), ("coords", IntArr(3, 4))),
        });
        var doc = Rec(("points", points));

        var text = OdinFormatter.Format(doc, null);

        // Tabular header with indexed sub-array columns must be present.
        Assert.Contains("{points[] : label, coords[0], coords[1]}", text);
        // No padding/extra columns
        Assert.DoesNotContain("coords[2]", text);

        // Round-trips losslessly.
        var parsed = OdinApi.Parse(text);
        Assert.Equal("A", parsed.GetString("points[0].label"));
        Assert.Equal(1, parsed.GetInteger("points[0].coords[0]"));
        Assert.Equal(2, parsed.GetInteger("points[0].coords[1]"));
        Assert.Equal("B", parsed.GetString("points[1].label"));
        Assert.Equal(3, parsed.GetInteger("points[1].coords[0]"));
        Assert.Equal(4, parsed.GetInteger("points[1].coords[1]"));
    }

    // ─────────────────────────────────────────────────────────────────
    // Size guarantee: nested form must not blow up on a search-index fixture
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void SizeGuarantee_SearchIndexStyleFixture()
    {
        // 20 records with widely varying tag-array widths (1 .. 39 tags).
        // The worst-case padded-tabular form would emit a 39-column header
        // and ~20*39 cells. The fix ensures we never produce that header.
        var entries = new List<DynValue>();
        for (int r = 0; r < 20; r++)
        {
            int tagCount = 1 + r * 2;
            var tags = new List<DynValue>(tagCount);
            for (int t = 0; t < tagCount; t++)
                tags.Add(DynValue.String($"tag-{r}-{t}"));

            entries.Add(Rec(
                ("slug", DynValue.String($"record/{r}")),
                ("title", DynValue.String($"Record {r}")),
                ("tags", DynValue.Array(tags))));
        }
        var doc = Rec(("entries", DynValue.Array(entries)));

        var text = OdinFormatter.Format(doc, null);

        // The pathological worst-case column must never appear.
        Assert.DoesNotContain("tags[39]", text);
        Assert.DoesNotContain("tags[20]", text);

        // Every record's slug must be present in the serialized output.
        Assert.Contains("record/0", text);
        Assert.Contains("record/19", text);

        // After Bug 2 fix, every nested tag value must round-trip losslessly.
        var parsed = OdinApi.Parse(text);
        for (int r = 0; r < 20; r++)
        {
            int tagCount = 1 + r * 2;
            Assert.Equal($"record/{r}", parsed.GetString($"entries[{r}].slug"));
            for (int t = 0; t < tagCount; t++)
            {
                Assert.Equal($"tag-{r}-{t}", parsed.GetString($"entries[{r}].tags[{t}]"));
            }
            Assert.False(parsed.Has($"entries[{r}].tags[{tagCount}]"));
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // Round-trip tests for complex objects (Bug 2 fix coverage)
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void RoundTrip_RecordsWithNestedObjectsAndArrays()
    {
        // Records with both nested objects and ragged sub-arrays force the
        // record-block fallback path. Every field must survive the round-trip.
        var records = DynValue.Array(new List<DynValue>
        {
            Rec(
                ("id", DynValue.Integer(1)),
                ("name", DynValue.String("Alice")),
                ("address", Rec(
                    ("city", DynValue.String("Boston")),
                    ("zip", DynValue.String("02101")))),
                ("tags", StrArr("admin", "ops", "sre"))),
            Rec(
                ("id", DynValue.Integer(2)),
                ("name", DynValue.String("Bob")),
                ("address", Rec(
                    ("city", DynValue.String("Seattle")),
                    ("zip", DynValue.String("98101")))),
                ("tags", StrArr("dev"))),
        });
        var doc = Rec(("users", records));

        var text = OdinFormatter.Format(doc, null);
        var parsed = OdinApi.Parse(text);

        Assert.Equal(1, parsed.GetInteger("users[0].id"));
        Assert.Equal("Alice", parsed.GetString("users[0].name"));
        Assert.Equal("Boston", parsed.GetString("users[0].address.city"));
        Assert.Equal("02101", parsed.GetString("users[0].address.zip"));
        Assert.Equal("admin", parsed.GetString("users[0].tags[0]"));
        Assert.Equal("ops", parsed.GetString("users[0].tags[1]"));
        Assert.Equal("sre", parsed.GetString("users[0].tags[2]"));

        Assert.Equal(2, parsed.GetInteger("users[1].id"));
        Assert.Equal("Bob", parsed.GetString("users[1].name"));
        Assert.Equal("Seattle", parsed.GetString("users[1].address.city"));
        Assert.Equal("98101", parsed.GetString("users[1].address.zip"));
        Assert.Equal("dev", parsed.GetString("users[1].tags[0]"));
        Assert.False(parsed.Has("users[1].tags[1]"));
    }

    [Fact]
    public void RoundTrip_DenseTabularWithDottedColumns()
    {
        // Pure tabular case with dotted nested-object columns. Must use the
        // tabular form and round-trip losslessly.
        var rows = DynValue.Array(new List<DynValue>
        {
            Rec(
                ("id", DynValue.Integer(1)),
                ("name", DynValue.String("Alice")),
                ("address", Rec(
                    ("line1", DynValue.String("123 Main")),
                    ("city", DynValue.String("Boston"))))),
            Rec(
                ("id", DynValue.Integer(2)),
                ("name", DynValue.String("Bob")),
                ("address", Rec(
                    ("line1", DynValue.String("9 Oak")),
                    ("city", DynValue.String("Seattle"))))),
        });
        var doc = Rec(("records", rows));

        var text = OdinFormatter.Format(doc, null);

        // Tabular header must be present.
        Assert.Contains("records[]", text);

        var parsed = OdinApi.Parse(text);
        Assert.Equal(1, parsed.GetInteger("records[0].id"));
        Assert.Equal("Alice", parsed.GetString("records[0].name"));
        Assert.Equal("123 Main", parsed.GetString("records[0].address.line1"));
        Assert.Equal("Boston", parsed.GetString("records[0].address.city"));
        Assert.Equal(2, parsed.GetInteger("records[1].id"));
        Assert.Equal("Bob", parsed.GetString("records[1].name"));
        Assert.Equal("9 Oak", parsed.GetString("records[1].address.line1"));
        Assert.Equal("Seattle", parsed.GetString("records[1].address.city"));
    }
}
