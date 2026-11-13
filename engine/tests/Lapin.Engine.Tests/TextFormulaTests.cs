using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Texts in formulas (decision 550, a tester's topic #38): quotes, &amp; and concat join texts, text(x; "00") and pad write a
/// number as a format says, upper, lower, trim, left, right and len, comparisons of texts; numbers keep working exactly as before
/// (a text that starts with a number still reads as it, a text that does not is no number).
/// </summary>
public sealed class TextFormulaTests
{
    static readonly Dictionary<string, XValue> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        ["storey.code"] = XValue.Read("K1"), ["number"] = XValue.Read("7"), ["width"] = XValue.Of(0.9), ["finish"] = XValue.Read("oak"),
        ["height"] = XValue.Read("3.20 m"),
    };

    static string T(string formula) => Expr.EvalValue(formula, n => Names.TryGetValue(n, out var v) ? v : null).AsText;

    [Fact]
    public void Texts_join_and_take_their_formats()
    {
        Assert.Equal("K1.7", T("storey.code & \".\" & number"));
        Assert.Equal("K1.07", T("concat(storey.code; \".\"; text(number; \"00\"))"));
        Assert.Equal("007", T("pad(number; 3)"));
        Assert.Equal("OAK", T("upper(finish)"));
        Assert.Equal("Oak", T("upper(left(finish; 1)) & right(finish; 2)"));
        Assert.Equal("3", T("len(finish)"));
        Assert.Equal("0.90", T("text(width; \"0.00\")"));
        Assert.Equal("it's", T("'it''s'"));
        Assert.Equal("wide", T("if(width > 0.8; \"wide\"; \"narrow\")"));
        Assert.Equal("1", T("storey.code == \"k1\""));
        Assert.Equal("0", T("finish == \"pine\""));
        Assert.Equal("1", T("number == 7"));
    }

    [Fact]
    public void Numbers_read_as_before_and_a_text_is_no_number()
    {
        // A text that starts with a number is that number; one that does not is not (K1 is not 1).
        Assert.Equal(6.4, Expr.EvalValue("height * 2", n => Names.TryGetValue(n, out var v) ? v : null).Number!.Value, 9);
        Assert.Throws<ExprException>(() => Expr.EvalValue("storey.code + 1", n => Names.TryGetValue(n, out var v) ? v : null));
        // The numeric API: a formula giving a text is refused with why.
        var (value, error) = Expr.TryEval("\"a\" & \"b\"", _ => null);
        Assert.Null(value);
        Assert.Contains("text", error);
        Assert.Equal(7, Expr.Eval("round(3.4) + 4", _ => null));
        Assert.Equal(["storey.code", "number"], Expr.Names("storey.code & number"));
        // Live text: a formula's text, a number with its format, and a bar inside quotes left to the text.
        string Fill(string text) => LiveText.Fill(text, k => Names.TryGetValue(k, out var v) ? (true, v.AsText) : (false, null));
        Assert.Equal("K1-7", Fill("{= storey.code & \"-\" & number}"));
        Assert.Equal("1.80", Fill("{= width * 2 | 0.00}"));
        Assert.Equal("a|b", Fill("{= \"a|b\"}"));
    }
}
