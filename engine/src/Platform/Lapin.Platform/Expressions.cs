using System.Globalization;
using System.Text;

namespace Lapin.Platform;

/// <summary>
/// A formula's value: a number, or a text (decision 550). A text read from the model may stand for a number too (a parameter
/// „12” or „3.20 m”): <see cref="Number"/> is then that number, so arithmetic on it works as it always did, while a text that
/// does not start with a number („K1”) is only a text.
/// </summary>
public readonly record struct XValue(double? Number, string? Text)
{
    public static XValue Of(double n) => new(n, null);
    public static XValue Of(string s) => new(null, s);
    public static XValue Of(bool b) => new(b ? 1 : 0, b ? "yes" : "no");

    /// <summary>A text as the model holds it: a number too when it starts with one („12 m” is 12, „+3.20” is 3.2), else only a text.</summary>
    public static XValue Read(string s)
    {
        var t = s.TrimStart();
        var number = t.Length > 0 && (char.IsDigit(t[0]) || t[0] is '-' or '+' or '−' or '±' or '.' && t.Length > 1 && char.IsDigit(t[1])) ? LiveText.Number(t) : null;
        return new(number, s);
    }

    public bool IsText => Text != null;

    /// <summary>Whether its text is a number and nothing else („1.50”, „-3”).</summary>
    public bool IsNumericText => Text is { } t && double.TryParse(t.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out _);

    /// <summary>As a text: its own, else its number with up to three decimals.</summary>
    public string AsText => Text ?? (Number is { } n ? Expr.Format(n, null) : "");

    /// <summary>Whether it is true: a number not zero, a text not empty (and not „no”, „false”).</summary>
    public bool Truth => Number is { } n ? n != 0
        : Text is { Length: > 0 } t && !t.Trim().Equals("no", StringComparison.OrdinalIgnoreCase) && !t.Trim().Equals("false", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Formulas of the user's own (docs/plan-user-extensibility.md §5.3): a small declarative language the engine evaluates,
/// never code. Numbers (a dot for decimals; a comma or a semicolon parts a function's values), texts in quotes (<c>"K"</c> or
/// <c>'K'</c>, the quote doubled writes one), <c>+ - * / % ^</c>, <c>&amp;</c> joining texts, comparisons (<c>&lt; &lt;= &gt; &gt;= == !=</c>,
/// 1 or 0; two values of which one is a text compare as texts, case aside), <c>and</c>, <c>or</c>, <c>not</c>, parentheses, names
/// (<c>attr.width</c>, <c>view.scale</c>, <c>storey.code</c>, <c>pi</c>) read through the caller, the functions <c>min max abs round
/// floor ceil sqrt sin cos tan atan2 if clamp</c> (angles in degrees) and, for texts (decision 550), <c>concat text upper lower
/// trim left right len pad</c>: <c>text(x; "00")</c> writes a number as a format says, <c>pad(x; 3)</c> fills it to three characters
/// with zeros in front. A text with its formula is <c>{= attr.width * 2}</c>, a format after a bar: <c>{= attr.length / 1000 | 0.00}</c>.
/// The length and the depth are bounded, so a formula cannot run away; one that does not read raises <see cref="ExprException"/>.
/// </summary>
public static class Expr
{
    public const int MaxLength = 2000;
    public const int MaxDepth = 64;
    /// <summary>The longest text a formula makes (a runaway concatenation stops there).</summary>
    public const int MaxText = 10_000;

    static readonly string[] Functions =
    [
        "min", "max", "abs", "round", "floor", "ceil", "sqrt", "sin", "cos", "tan", "atan2", "if", "clamp",
        "concat", "text", "upper", "lower", "trim", "left", "right", "len", "pad",
    ];

    /// <summary>The functions a formula may call (shown in the interface's help).</summary>
    public static IReadOnlyList<string> FunctionNames => Functions;

    /// <summary>The value of <paramref name="formula"/> as a number; <paramref name="name"/> gives a name's number (null: unknown, an error).</summary>
    public static double Eval(string formula, Func<string, double?> name)
    {
        var v = EvalValue(formula, n => name(n) is { } x ? XValue.Of(x) : null);
        if (v.Number is { } number) return number;
        throw new ExprException($"„{formula.Trim()}” gives a text, not a number.");
    }

    /// <summary>The value, or null with why when the formula does not read or names something unknown.</summary>
    public static (double? Value, string? Error) TryEval(string formula, Func<string, double?> name)
    {
        try { return (Eval(formula, name), null); }
        catch (ExprException e) { return (null, e.Message); }
    }

    /// <summary>The value of <paramref name="formula"/>, a number or a text; <paramref name="name"/> gives a name's value (null: unknown, an error).</summary>
    public static XValue EvalValue(string formula, Func<string, XValue?> name)
    {
        if (formula.Length > MaxLength) throw new ExprException($"A formula is at most {MaxLength} characters.");
        var p = new Parser(formula, name);
        var v = p.Expression(0);
        p.SkipSpace();
        if (!p.End) throw new ExprException($"„{formula}”: something after the formula's end at {p.Pos + 1}.");
        return v;
    }

    /// <summary>The value, a number or a text, or null with why.</summary>
    public static (XValue? Value, string? Error) TryEvalValue(string formula, Func<string, XValue?> name)
    {
        try { return (EvalValue(formula, name), null); }
        catch (ExprException e) { return (null, e.Message); }
    }

    /// <summary>The names a formula reads (to say which a field depends on); empty when it does not read.</summary>
    public static List<string> Names(string formula)
    {
        var o = new List<string>();
        try { EvalValue(formula, n => { if (!o.Contains(n)) o.Add(n); return XValue.Of(1); }); }
        catch (ExprException) { }
        return o;
    }

    /// <summary>A number as a format says (<c>0.00</c>, <c>#,##0</c>, <c>0.###</c>); none: up to three decimals, no trailing zeros.</summary>
    public static string Format(double v, string? format)
    {
        if (double.IsNaN(v) || double.IsInfinity(v)) return "—";
        if (format is { Length: > 0 })
            try { return v.ToString(format.Trim(), CultureInfo.InvariantCulture); }
            catch (FormatException) { }
        return Math.Round(v, 3).ToString("0.###", CultureInfo.InvariantCulture);
    }

    /// <summary>A value written: a number (or a text that is only a number) as the format says, any other text as it is.</summary>
    public static string Format(XValue v, string? format)
    {
        if (v.Text is { } t)
            return v.IsNumericText && double.TryParse(t.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? Format(n, format) : t;
        return v.Number is { } x ? Format(x, format) : "";
    }

    sealed class Parser(string s, Func<string, XValue?> name)
    {
        int i;
        int depth;
        public int Pos => i;
        public bool End => i >= s.Length;

        public void SkipSpace() { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

        bool Eat(string tok)
        {
            SkipSpace();
            if (string.CompareOrdinal(s, i, tok, 0, tok.Length) != 0) return false;
            // A word operator is a whole word (and, or, not).
            if (char.IsLetter(tok[0]) && i + tok.Length < s.Length && (char.IsLetterOrDigit(s[i + tok.Length]) || s[i + tok.Length] is '_' or '.')) return false;
            i += tok.Length;
            return true;
        }

        // Precedence climbing: or < and < comparison < & < + - < * / % < ^ (right) < unary.
        static readonly (string Op, int Prec, bool Right)[] Binary =
        [
            ("or", 1, false), ("and", 2, false),
            ("<=", 3, false), (">=", 3, false), ("==", 3, false), ("!=", 3, false), ("<", 3, false), (">", 3, false),
            ("&", 4, false),
            ("+", 5, false), ("-", 5, false), ("*", 6, false), ("/", 6, false), ("%", 6, false), ("^", 7, true),
        ];

        public XValue Expression(int minPrec)
        {
            if (++depth > MaxDepth) throw new ExprException("A formula nests too deep.");
            var left = Unary();
            while (true)
            {
                SkipSpace();
                var op = Binary.FirstOrDefault(b => b.Prec >= minPrec && Peek(b.Op));
                if (op.Op == null) break;
                Eat(op.Op);
                var right = Expression(op.Right ? op.Prec : op.Prec + 1);
                left = Apply(op.Op, left, right);
            }
            depth--;
            return left;
        }

        bool Peek(string tok)
        {
            var save = i;
            var ok = Eat(tok);
            i = save;
            // "<" is not "<=", "=" alone is not an operator.
            return ok;
        }

        static double Num(XValue v, string what) => v.Number ?? throw new ExprException($"„{v.Text}” is a text, not a number ({what}).");

        static string Cap(string t) => t.Length > MaxText ? throw new ExprException($"A formula's text is at most {MaxText} characters.") : t;

        /// <summary>Whether two values compare as texts: one of them is a text that is not only a number.</summary>
        static bool AsTexts(XValue a, XValue b) => a.IsText && !a.IsNumericText || b.IsText && !b.IsNumericText;

        static XValue Apply(string op, XValue a, XValue b)
        {
            switch (op)
            {
                case "&": return XValue.Of(Cap(a.AsText + b.AsText));
                case "and": return XValue.Of(a.Truth && b.Truth ? 1 : 0);
                case "or": return XValue.Of(a.Truth || b.Truth ? 1 : 0);
                case "==" or "!=" when AsTexts(a, b):
                {
                    var same = string.Equals(a.AsText.Trim(), b.AsText.Trim(), StringComparison.OrdinalIgnoreCase);
                    return XValue.Of((op == "==") == same ? 1 : 0);
                }
                case "<" or "<=" or ">" or ">=" when AsTexts(a, b):
                {
                    var c = string.Compare(a.AsText.Trim(), b.AsText.Trim(), StringComparison.OrdinalIgnoreCase);
                    return XValue.Of((op switch { "<" => c < 0, "<=" => c <= 0, ">" => c > 0, _ => c >= 0 }) ? 1 : 0);
                }
            }
            var (x, y) = (Num(a, op), Num(b, op));
            return XValue.Of(op switch
            {
                "+" => x + y, "-" => x - y, "*" => x * y,
                "/" => y == 0 ? throw new ExprException("A formula divides by zero.") : x / y,
                "%" => y == 0 ? throw new ExprException("A formula divides by zero.") : x - y * Math.Floor(x / y),
                "^" => Math.Pow(x, y),
                "<" => x < y ? 1 : 0, "<=" => x <= y ? 1 : 0, ">" => x > y ? 1 : 0, ">=" => x >= y ? 1 : 0,
                "==" => Math.Abs(x - y) < 1e-9 ? 1 : 0, "!=" => Math.Abs(x - y) >= 1e-9 ? 1 : 0,
                _ => throw new ExprException($"„{op}” is not an operator."),
            });
        }

        XValue Unary()
        {
            if (Eat("-")) return XValue.Of(-Num(Unary(), "-"));
            if (Eat("+")) return XValue.Of(Num(Unary(), "+"));
            if (Eat("not")) return XValue.Of(Unary().Truth ? 0 : 1);
            return Primary();
        }

        XValue Primary()
        {
            SkipSpace();
            if (End) throw new ExprException("A formula ends where a value was expected.");
            var c = s[i];
            if (c == '(')
            {
                i++;
                var v = Expression(0);
                if (!Eat(")")) throw new ExprException("A bracket is not closed.");
                return v;
            }
            // A text in quotes, double or single (or typographic); the quote doubled writes one.
            if (c is '"' or '\'' or '“' or '„')
            {
                var close = c is '“' or '„' ? '”' : c;
                var sb = new StringBuilder();
                i++;
                while (true)
                {
                    if (i >= s.Length) throw new ExprException("A text in quotes is not closed.");
                    if (s[i] == close)
                    {
                        if (i + 1 < s.Length && s[i + 1] == close) { sb.Append(close); i += 2; continue; }
                        i++;
                        break;
                    }
                    sb.Append(s[i++]);
                }
                return XValue.Of(sb.ToString());
            }
            // Decimals with a dot: a comma (or a semicolon) parts a function's values.
            if (char.IsDigit(c) || (c == '.' && i + 1 < s.Length && char.IsDigit(s[i + 1])))
            {
                var start = i;
                while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.')) i++;
                if (i < s.Length && s[i] is 'e' or 'E' && i + 1 < s.Length && (char.IsDigit(s[i + 1]) || s[i + 1] is '-' or '+')) { i += 2; while (i < s.Length && char.IsDigit(s[i])) i++; }
                var text = s[start..i];
                return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? XValue.Of(n) : throw new ExprException($"„{text}” is not a number.");
            }
            if (char.IsLetter(c) || c == '_')
            {
                var start = i;
                while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] is '_' or '.')) i++;
                var word = s[start..i];
                SkipSpace();
                if (i < s.Length && s[i] == '(')
                {
                    if (!Functions.Contains(word.ToLowerInvariant())) throw new ExprException($"„{word}” is not a function ({string.Join(", ", Functions)}).");
                    i++;
                    var args = new List<XValue>();
                    SkipSpace();
                    if (!Eat(")"))
                    {
                        do args.Add(Expression(0)); while (Eat(";") || Eat(","));
                        if (!Eat(")")) throw new ExprException($"{word}(…) is not closed.");
                    }
                    return Call(word.ToLowerInvariant(), args);
                }
                if (word.Equals("pi", StringComparison.OrdinalIgnoreCase)) return XValue.Of(Math.PI);
                if (word.Equals("true", StringComparison.OrdinalIgnoreCase)) return XValue.Of(1);
                if (word.Equals("false", StringComparison.OrdinalIgnoreCase)) return XValue.Of(0);
                return name(word) ?? throw new ExprException($"„{word}” has no value here.");
            }
            throw new ExprException($"„{c}” cannot stand at {i + 1}.");
        }

        static XValue Call(string f, List<XValue> v)
        {
            void N(int lo, int hi) { if (v.Count < lo || v.Count > hi) throw new ExprException($"{f}(…) takes {(lo == hi ? lo.ToString(CultureInfo.InvariantCulture) : $"{lo} to {hi}")} values."); }
            double A(int k) => Num(v[k], f + "(…)");
            int Whole(int k) => (int)Math.Clamp(Math.Round(A(k)), 0, MaxText);
            const double D = Math.PI / 180;
            switch (f)
            {
                case "min": N(1, 64); return XValue.Of(Enumerable.Range(0, v.Count).Select(A).Min());
                case "max": N(1, 64); return XValue.Of(Enumerable.Range(0, v.Count).Select(A).Max());
                case "abs": N(1, 1); return XValue.Of(Math.Abs(A(0)));
                case "round":
                {
                    N(1, 2);
                    // round(x) to a whole number, round(x; n) to n decimals, round(x; 0.05) to the nearest step under 1.
                    var x = A(0);
                    if (v.Count == 1) return XValue.Of(Math.Round(x, MidpointRounding.AwayFromZero));
                    var step = A(1);
                    if (step > 0 && step < 1) return XValue.Of(Math.Round(x / step, MidpointRounding.AwayFromZero) * step);
                    return XValue.Of(Math.Round(x, (int)Math.Clamp(step, 0, 12), MidpointRounding.AwayFromZero));
                }
                case "floor": N(1, 1); return XValue.Of(Math.Floor(A(0)));
                case "ceil": N(1, 1); return XValue.Of(Math.Ceiling(A(0)));
                case "sqrt": N(1, 1); return A(0) < 0 ? throw new ExprException("sqrt(…) of a negative number.") : XValue.Of(Math.Sqrt(A(0)));
                case "sin": N(1, 1); return XValue.Of(Math.Sin(A(0) * D));
                case "cos": N(1, 1); return XValue.Of(Math.Cos(A(0) * D));
                case "tan": N(1, 1); return XValue.Of(Math.Tan(A(0) * D));
                case "atan2": N(2, 2); return XValue.Of(Math.Atan2(A(0), A(1)) / D);
                case "if": N(3, 3); return v[0].Truth ? v[1] : v[2];
                case "clamp": N(3, 3); return XValue.Of(Math.Clamp(A(0), Math.Min(A(1), A(2)), Math.Max(A(1), A(2))));
                // Texts (decision 550).
                case "concat": N(1, 64); return XValue.Of(Cap(string.Concat(v.Select(x => x.AsText))));
                case "text": N(1, 2); return XValue.Of(Format(v[0], v.Count > 1 ? v[1].AsText : null));
                case "upper": N(1, 1); return XValue.Of(v[0].AsText.ToUpperInvariant());
                case "lower": N(1, 1); return XValue.Of(v[0].AsText.ToLowerInvariant());
                case "trim": N(1, 1); return XValue.Of(v[0].AsText.Trim());
                case "left": { N(2, 2); var t = v[0].AsText; return XValue.Of(t[..Math.Min(t.Length, Whole(1))]); }
                case "right": { N(2, 2); var t = v[0].AsText; return XValue.Of(t[(t.Length - Math.Min(t.Length, Whole(1)))..]); }
                case "len": N(1, 1); return XValue.Of(v[0].AsText.Length);
                case "pad":
                {
                    // pad(x; n): to n characters with zeros in front (a third value: another character).
                    N(2, 3);
                    var with = v.Count > 2 && v[2].AsText is { Length: > 0 } w ? w[0] : '0';
                    return XValue.Of(Format(v[0], null).PadLeft(Whole(1), with));
                }
                default: throw new ExprException($"„{f}” is not a function.");
            }
        }
    }
}

public sealed class ExprException(string message) : Exception(message);

/// <summary>
/// Live text (title blocks, symbols, tags): <c>{source.key}</c> tokens filled by a resolver, and formulas <c>{= …}</c> whose
/// names are read as the resolver's values, numbers or texts (<c>{= attr.width * 2 | 0.0}</c>, <c>{= upper(level.name)}</c>).
/// An unknown token prints as <c>&lt;key&gt;</c>, a formula that does not read as <c>&lt;formula&gt;</c>, each listed in <c>unknown</c>.
/// </summary>
public static class LiveText
{
    /// <summary>A text with its formulas evaluated, then its tokens filled.</summary>
    public static string Fill(string text, Func<string, (bool Known, string? Value)> resolve, List<string>? unknown = null)
    {
        if (text.Contains("{=", StringComparison.Ordinal)) text = Formulas(text, n => resolve(n) is { Known: true } r ? XValue.Read(r.Value ?? "") : null, unknown);
        return TitleBlocks.Fill(text, resolve, unknown);
    }

    /// <summary>A value as a number: its text read with a dot or a comma, the first number in it (<c>+3.20</c>, <c>1:100</c> → 1, <c>12 m</c> → 12).</summary>
    public static double? Number(string? v)
    {
        if (string.IsNullOrWhiteSpace(v)) return null;
        var t = v.Trim().Replace('−', '-').Replace(',', '.');
        if (double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var n)) return n;
        var m = System.Text.RegularExpressions.Regex.Match(t, @"[-+]?\d+(\.\d+)?");
        return m.Success && double.TryParse(m.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out n) ? n : null;
    }

    /// <summary>A formula's body split from its format: the bar after the last quote („{= a / 1000 | 0.00}”).</summary>
    public static (string Formula, string? Format) SplitFormat(string body)
    {
        var lastQuote = Math.Max(body.LastIndexOf('"'), body.LastIndexOf('\''));
        var bar = body.LastIndexOf('|');
        return bar > lastQuote ? (body[..bar], body[(bar + 1)..]) : (body, null);
    }

    /// <summary>The text's <c>{= …}</c> formulas, each written by its value (names read by <paramref name="name"/>); one that does not read as <c>&lt;formula&gt;</c>.</summary>
    public static string Formulas(string text, Func<string, XValue?> name, List<string>? unknown)
    {
        var sb = new StringBuilder();
        var i = 0;
        while (i < text.Length)
        {
            var open = text.IndexOf("{=", i, StringComparison.Ordinal);
            if (open < 0) { sb.Append(text, i, text.Length - i); break; }
            sb.Append(text, i, open - i);
            // The formula's end: the brace that closes it, outside quotes (brackets inside are the formula's).
            var close = CloseOf(text, open + 2);
            if (close < 0) { sb.Append(text, open, text.Length - open); break; }
            var (formula, format) = SplitFormat(text[(open + 2)..close]);
            var (value, error) = Expr.TryEvalValue(formula, name);
            if (value is { } v) sb.Append(Expr.Format(v, format));
            else
            {
                unknown?.Add("= " + formula.Trim() + (error != null ? $" ({error})" : ""));
                sb.Append('<').Append(formula.Trim()).Append('>');
            }
            i = close + 1;
        }
        return sb.ToString();
    }

    /// <summary>Where a formula's closing brace is, from <paramref name="from"/>: the first one outside quotes; -1 when none.</summary>
    public static int CloseOf(string text, int from)
    {
        char? quote = null;
        for (var k = from; k < text.Length; k++)
        {
            var c = text[k];
            if (quote != null) { if (c == quote) quote = null; continue; }
            if (c is '"' or '\'') quote = c;
            else if (c == '}') return k;
        }
        return -1;
    }
}
