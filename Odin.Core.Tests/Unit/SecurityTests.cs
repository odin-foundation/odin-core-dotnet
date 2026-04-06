using System;
using System.Collections.Generic;
using System.Text;
using Odin.Core;
using Odin.Core.Types;
using Utils = Odin.Core.Utils;
using Xunit;

namespace Odin.Core.Tests.Unit;

public class SecurityTests
{
    // ─────────────────────────────────────────────────────────────────
    // Prototype Pollution (JS-style) — Keys treated as normal
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Proto_Key_TreatedAsNormal()
    {
        var doc = Core.Odin.Parse("__proto__ = \"val\"");
        Assert.Equal("val", doc.GetString("__proto__"));
    }

    [Fact]
    public void Constructor_Key_TreatedAsNormal()
    {
        var doc = Core.Odin.Parse("constructor = \"val\"");
        Assert.Equal("val", doc.GetString("constructor"));
    }

    [Fact]
    public void ToString_Key_TreatedAsNormal()
    {
        var doc = Core.Odin.Parse("toString = \"val\"");
        Assert.Equal("val", doc.GetString("toString"));
    }

    [Fact]
    public void HasOwnProperty_Key_TreatedAsNormal()
    {
        var doc = Core.Odin.Parse("hasOwnProperty = \"val\"");
        Assert.Equal("val", doc.GetString("hasOwnProperty"));
    }

    [Fact]
    public void ValueOf_Key_TreatedAsNormal()
    {
        var doc = Core.Odin.Parse("valueOf = \"val\"");
        Assert.Equal("val", doc.GetString("valueOf"));
    }

    // ─────────────────────────────────────────────────────────────────
    // Input Limits — Large/Deep Documents
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ManyFields_DoesNotCrash()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < 1000; i++)
            sb.AppendLine($"field_{i} = ##{i}");
        // Should not crash
        try { Core.Odin.Parse(sb.ToString()); } catch { /* error is acceptable, crash is not */ }
    }

    [Fact]
    public void VeryLongString_DoesNotCrash()
    {
        var longStr = new string('x', 100_000);
        var input = $"val = \"{longStr}\"";
        // Should not crash
        try { Core.Odin.Parse(input); } catch { }
    }

    [Fact]
    public void VeryLongKey_DoesNotCrash()
    {
        var longKey = new string('k', 10_000);
        var input = $"{longKey} = \"val\"";
        // Should not crash
        try { Core.Odin.Parse(input); } catch { }
    }

    [Fact]
    public void ManySections_DoesNotCrash()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < 200; i++)
            sb.AppendLine($"{{Section{i}}}\nfield = ##{i}");
        // Should not crash
        try { Core.Odin.Parse(sb.ToString()); } catch { }
    }

    [Fact]
    public void LargeArray_DoesNotCrash()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < 500; i++)
            sb.AppendLine($"items[{i}] = \"val{i}\"");
        // Should not crash
        try { Core.Odin.Parse(sb.ToString()); } catch { }
    }

    // ─────────────────────────────────────────────────────────────────
    // Encoding Safety — Unicode Edge Cases
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void NullBytesInString_DoesNotCrash()
    {
        var input = "x = \"hello\\u0000world\"";
        // Should not crash
        try { Core.Odin.Parse(input); } catch { }
    }

    [Fact]
    public void EmojiInString_Preserved()
    {
        var doc = Core.Odin.Parse("x = \"hello \U0001F30D\"");
        Assert.Equal("hello \U0001F30D", doc.GetString("x"));
    }

    [Fact]
    public void CJKCharacters_Preserved()
    {
        var doc = Core.Odin.Parse("x = \"\u65E5\u672C\u8A9E\"");
        Assert.Equal("\u65E5\u672C\u8A9E", doc.GetString("x"));
    }

    [Fact]
    public void RTLText_Preserved()
    {
        var doc = Core.Odin.Parse("x = \"\u0645\u0631\u062D\u0628\u0627\"");
        Assert.Equal("\u0645\u0631\u062D\u0628\u0627", doc.GetString("x"));
    }

    [Fact]
    public void WhitespaceOnlyValue_Preserved()
    {
        var doc = Core.Odin.Parse("x = \"   \"");
        Assert.Equal("   ", doc.GetString("x"));
    }

    [Fact]
    public void EmptySectionName_DoesNotCrash()
    {
        try { Core.Odin.Parse("{}\nval = ##1"); } catch { }
    }

    // ─────────────────────────────────────────────────────────────────
    // Injection Safety — Malformed Keys
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void KeyWithEquals_DoesNotCrash()
    {
        try { Core.Odin.Parse("key=val = \"test\""); } catch { }
    }

    [Fact]
    public void KeyWithBracket_DoesNotCrash()
    {
        try { Core.Odin.Parse("key] = \"test\""); } catch { }
    }

    [Fact]
    public void KeyWithBrace_DoesNotCrash()
    {
        try { Core.Odin.Parse("key} = \"test\""); } catch { }
    }

    [Fact]
    public void KeyWithAt_DoesNotCrash()
    {
        try { Core.Odin.Parse("@key = \"test\""); } catch { }
    }

    [Fact]
    public void KeyWithHash_DoesNotCrash()
    {
        try { Core.Odin.Parse("#key = \"test\""); } catch { }
    }

    [Fact]
    public void KeyWithSemicolon_DoesNotCrash()
    {
        // Semicolon at start of line is a comment
        try { Core.Odin.Parse(";key = \"test\""); } catch { }
    }

    [Fact]
    public void SectionWithSpaces_DoesNotCrash()
    {
        try { Core.Odin.Parse("{A B}\nf = ##1"); } catch { }
    }

    [Fact]
    public void DoubleSectionHeader_DoesNotCrash()
    {
        try { Core.Odin.Parse("{{A}}\nf = ##1"); } catch { }
    }

    [Fact]
    public void ValueWithTripleHash_DoesNotCrash()
    {
        try { Core.Odin.Parse("x = ###42"); } catch { }
    }

    [Fact]
    public void ValueWithMultipleEquals_DoesNotCrash()
    {
        try { Core.Odin.Parse("x = = ##1"); } catch { }
    }

    [Fact]
    public void ConsecutiveSeparators_DoesNotCrash()
    {
        try { Core.Odin.ParseDocuments("x = ##1\n---\n---\ny = ##2"); } catch { }
    }

    [Fact]
    public void OnlySeparators_DoesNotCrash()
    {
        try { Core.Odin.ParseDocuments("---\n---\n---"); } catch { }
    }

    [Fact]
    public void SeparatorAtStart_DoesNotCrash()
    {
        try { Core.Odin.ParseDocuments("---\nx = ##1"); } catch { }
    }

    // ─────────────────────────────────────────────────────────────────
    // Boundary Values
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void MaxInt64()
    {
        var doc = Core.Odin.Parse("x = ##9223372036854775807");
        Assert.NotNull(doc.Get("x"));
    }

    [Fact]
    public void MinInt64()
    {
        var doc = Core.Odin.Parse("x = ##-9223372036854775808");
        Assert.NotNull(doc.Get("x"));
    }

    [Fact]
    public void ZeroInteger()
    {
        var doc = Core.Odin.Parse("x = ##0");
        Assert.Equal(0L, doc.GetInteger("x"));
    }

    [Fact]
    public void NegativeZero_DoesNotCrash()
    {
        try { Core.Odin.Parse("x = ##-0"); } catch { }
    }

    [Fact]
    public void ZeroCurrency()
    {
        var doc = Core.Odin.Parse("x = #$0.00");
        Assert.True(doc.Get("x")!.IsCurrency);
    }

    [Fact]
    public void ZeroPercent()
    {
        var doc = Core.Odin.Parse("x = #%0");
        Assert.True(doc.Get("x")!.IsPercent);
    }

    // ─────────────────────────────────────────────────────────────────
    // DynValue Security
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void DeeplyNestedObject_DoesNotOverflow()
    {
        var obj = DynValue.String("leaf");
        for (int i = 0; i < 100; i++)
        {
            obj = DynValue.Object(new List<KeyValuePair<string, DynValue>>
            {
                new($"level{i}", obj)
            });
        }
        Assert.Equal(DynValueType.Object, obj.Type);
    }

    [Fact]
    public void LargeArray_DynValue_DoesNotOverflow()
    {
        var items = new List<DynValue>();
        for (int i = 0; i < 10000; i++)
            items.Add(DynValue.Integer(i));
        var arr = DynValue.Array(items);
        Assert.Equal(10000, arr.AsArray()!.Count);
    }

    // ─────────────────────────────────────────────────────────────────
    // SecurityLimits Constants
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void SecurityLimits_MaxDocumentSize()
    {
        Assert.Equal(10 * 1024 * 1024, Utils.SecurityLimits.MaxDocumentSize);
    }

    [Fact]
    public void SecurityLimits_MaxNestingDepth()
    {
        Assert.Equal(64, Utils.SecurityLimits.MaxNestingDepth);
    }

    [Fact]
    public void SecurityLimits_MaxArrayIndex()
    {
        Assert.Equal(10_000, Utils.SecurityLimits.MaxArrayIndex);
    }

    // ─────────────────────────────────────────────────────────────────
    // Document Structure Edge Cases
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void MetadataAndData_Mixed()
    {
        var doc = Core.Odin.Parse("{$}\nodin = \"1.0.0\"\n\nname = \"test\"\nage = ##25");
        Assert.Equal("test", doc.GetString("name"));
        Assert.Equal(25L, doc.GetInteger("age"));
    }

    [Fact]
    public void Parse_MultipleTypesInSection_AllCorrect()
    {
        var doc = Core.Odin.Parse("{S}\na = \"str\"\nb = ##42\nc = #3.14\nd = true\ne = ~\nf = #$99.99\ng = #%50");
        Assert.Equal("str", doc.GetString("S.a"));
        Assert.Equal(42L, doc.GetInteger("S.b"));
        Assert.Equal(true, doc.GetBoolean("S.d"));
        Assert.True(doc.Get("S.e")!.IsNull);
        Assert.True(doc.Get("S.f")!.IsCurrency);
        Assert.True(doc.Get("S.g")!.IsPercent);
    }

    // ─────────────────────────────────────────────────────────────────
    // OdinParseException Security
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ParseException_ContainsLineAndColumn()
    {
        try
        {
            Core.Odin.Parse("x = \"unterminated");
        }
        catch (OdinParseException ex)
        {
            Assert.True(ex.Line > 0);
            Assert.True(ex.Column > 0);
            Assert.NotNull(ex.Code);
            return;
        }
        Assert.Fail("Expected OdinParseException");
    }

    [Fact]
    public void ParseException_HasErrorCode()
    {
        try
        {
            Core.Odin.Parse("x = \"unterminated");
        }
        catch (OdinParseException ex)
        {
            Assert.Equal(ParseErrorCode.UnterminatedString, ex.ErrorCode);
            Assert.Equal("P004", ex.Code);
            return;
        }
        Assert.Fail("Expected OdinParseException");
    }

    // ─────────────────────────────────────────────────────────────────
    // Immutability Verification
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Document_Immutability_WithDoesNotModifyOriginal()
    {
        var original = Core.Odin.Parse("x = ##1");
        var modified = original.With("y", OdinValues.Integer(2));
        Assert.False(original.Has("y"));
        Assert.True(modified.Has("y"));
    }

    [Fact]
    public void Document_Immutability_WithoutDoesNotModifyOriginal()
    {
        var original = Core.Odin.Parse("x = ##1\ny = ##2");
        var modified = original.Without("y");
        Assert.True(original.Has("y"));
        Assert.False(modified.Has("y"));
    }

    // ─────────────────────────────────────────────────────────────────
    // Thread Safety — Parse from multiple threads
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public async System.Threading.Tasks.Task ConcurrentParse_DoesNotCrash()
    {
        var tasks = new System.Threading.Tasks.Task[10];
        for (int i = 0; i < 10; i++)
        {
            tasks[i] = System.Threading.Tasks.Task.Run(() =>
            {
                for (int j = 0; j < 50; j++)
                {
                    var doc = Core.Odin.Parse($"field = ##{j}");
                    Assert.Equal((long)j, doc.GetInteger("field"));
                }
            });
        }
        await System.Threading.Tasks.Task.WhenAll(tasks);
    }

    [Fact]
    public async System.Threading.Tasks.Task ConcurrentStringify_DoesNotCrash()
    {
        var doc = Core.Odin.Parse("x = ##1\ny = \"hello\"\nz = true");
        var tasks = new System.Threading.Tasks.Task[10];
        for (int i = 0; i < 10; i++)
        {
            tasks[i] = System.Threading.Tasks.Task.Run(() =>
            {
                for (int j = 0; j < 50; j++)
                {
                    var text = Core.Odin.Stringify(doc);
                    Assert.NotNull(text);
                }
            });
        }
        await System.Threading.Tasks.Task.WhenAll(tasks);
    }

    // ─────────────────────────────────────────────────────────────────
    // Empty Input Handling
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void EmptyString_DoesNotCrash()
    {
        try { Core.Odin.Parse(""); } catch { }
    }

    [Fact]
    public void WhitespaceOnly_DoesNotCrash()
    {
        try { Core.Odin.Parse("   \n   \n   "); } catch { }
    }

    [Fact]
    public void CommentsOnly_DoesNotCrash()
    {
        try { Core.Odin.Parse("; comment 1\n; comment 2\n; comment 3"); } catch { }
    }

    [Fact]
    public void MixedLineEndings_DoesNotCrash()
    {
        try { Core.Odin.Parse("x = ##1\r\ny = ##2\rz = ##3\n"); } catch { }
    }
}
