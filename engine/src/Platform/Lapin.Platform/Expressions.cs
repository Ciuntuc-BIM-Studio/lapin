using System.Globalization;
using System.Text;

namespace Lapin.Platform;

/// <summary>
/// Formulas of the user's own (docs/plan-user-extensibility.md §5.3): a small declarative language the engine evaluates,
/// never code. Numbers (a dot for decimals; a comma or a semicolon parts a function's values),<c>+ - * / % ^</c>, comparisons (<c>&lt; &lt;= &gt; &gt;= == !=</c>, 1 or 0),
/// <c>and</c>, <c>or</c>, <c>not</c>, parentheses, names (<c>attr.width</c>, <c>view.scale</c>, <c>pi</c>) read through the caller,
/// and the functions <c>min max abs round floor ceil sqrt sin cos tan atan2 if clamp</c> (angles in degrees). A text with
/// its formula is <c>{= attr.width * 2}</c>, a format after a bar: <c>{= attr.length / 1000 | 0.00}</c>.
/// The length and the depth are bounded, so a formula cannot run away; one that does not read raises <see cref="ExprException"/>.
/// </summary>
public static class Expr
{
    public const int MaxLength = 2000;
    public const int MaxDepth = 64;

    static readonly string[] Functions = ["min", "max", "abs", "round", "floor", "ceil", "sqrt", "sin", "cos", "tan", "atan2", "if", "clamp"];

    /// <summary>The functions a formula may call (shown in the interface's help).</summary>
    public static IReadOnlyList<string> FunctionNames => Functions;

    /// <summary>The value of <paramref name="formula"/>; <paramref name="name"/> gives a name's number (null: unknown, an error).</summary>
    public static double Eval(string formula, Func<string, double?> name)
    {
        if (formula.Length > MaxLength) throw new ExprException($"A formula is at most {MaxLength} characters.");
        var p = new Parser(formula, name);
        var v = p.Expression(0);
        p.SkipSpace();
        if (!p.End) throw new ExprException($"„{formula}”: something after the formula's end at {p.Pos + 1}.");
        return v;
    }

    /// <summary>The value, or null with why when the formula does not read or names something unknown.</summary>
    public static (double? Value, string? Error) TryEval(string formula, Func<string, double?> name)
    {
        try { return (Eval(formula, name), null); }
        catch (ExprException e) { return (null, e.Message); }
    }

    /// <summary>The names a formula reads (to say which a field depends on); empty when it does not read.</summary>
    public static List<string> Names(string formula)
    {
        var o = new List<string>();
        try { Eval(formula, n => { if (!o.Contains(n)) o.Add(n); return 1; }); }
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

    sealed class Parser(string s, Func<string, double?> name)
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

        // Precedence climbing: or < and < comparison < + - < * / % < ^ (right) < unary.
        static readonly (string Op, int Prec, bool Right)[] Binary =
        [
            ("or", 1, false), ("and", 2, false),
            ("<=", 3, false), (">=", 3, false), ("==", 3, false), ("!=", 3, false), ("<", 3, false), (">", 3, false),
            ("+", 4, false), ("-", 4, false), ("*", 5, false), ("/", 5, false), ("%", 5, false), ("^", 6, true),
        ];

        public double Expression(int minPrec)
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

        static double Apply(string op, double a, double b) => op switch
        {
            "+" => a + b, "-" => a - b, "*" => a * b,
            "/" => b == 0 ? throw new ExprException("A formula divides by zero.") : a / b,
            "%" => b == 0 ? throw new ExprException("A formula divides by zero.") : a - b * Math.Floor(a / b),
            "^" => Math.Pow(a, b),
            "<" => a < b ? 1 : 0, "<=" => a <= b ? 1 : 0, ">" => a > b ? 1 : 0, ">=" => a >= b ? 1 : 0,
            "==" => Math.Abs(a - b) < 1e-9 ? 1 : 0, "!=" => Math.Abs(a - b) >= 1e-9 ? 1 : 0,
            "and" => a != 0 && b != 0 ? 1 : 0, "or" => a != 0 || b != 0 ? 1 : 0,
            _ => throw new ExprException($"„{op}” is not an operator."),
        };

        double Unary()
        {
            if (Eat("-")) return -Unary();
            if (Eat("+")) return Unary();
            if (Eat("not")) return Unary() == 0 ? 1 : 0;
            return Primary();
        }

        double Primary()
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
            // Decimals with a dot: a comma (or a semicolon) parts a function's values.
            if (char.IsDigit(c) || (c == '.' && i + 1 < s.Length && char.IsDigit(s[i + 1])))
            {
                var start = i;
                while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.')) i++;
                if (i < s.Length && s[i] is 'e' or 'E' && i + 1 < s.Length && (char.IsDigit(s[i + 1]) || s[i + 1] is '-' or '+')) { i += 2; while (i < s.Length && char.IsDigit(s[i])) i++; }
                var text = s[start..i];
                return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : throw new ExprException($"„{text}” is not a number.");
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
                    var args = new List<double>();
                    SkipSpace();
                    if (!Eat(")"))
                    {
                        do args.Add(Expression(0)); while (Eat(";") || Eat(","));
                        if (!Eat(")")) throw new ExprException($"{word}(…) is not closed.");
                    }
                    return Call(word.ToLowerInvariant(), args);
                }
                if (word.Equals("pi", StringComparison.OrdinalIgnoreCase)) return Math.PI;
                if (word.Equals("true", StringComparison.OrdinalIgnoreCase)) return 1;
                if (word.Equals("false", StringComparison.OrdinalIgnoreCase)) return 0;
                return name(word) ?? throw new ExprException($"„{word}” has no number here.");
            }
            throw new ExprException($"„{c}” cannot stand at {i + 1}.");
        }

        static double Call(string f, List<double> a)
        {
            void N(int lo, int hi) { if (a.Count < lo || a.Count > hi) throw new ExprException($"{f}(…) takes {(lo == hi ? lo.ToString(CultureInfo.InvariantCulture) : $"{lo} to {hi}")} values."); }
            const double D = Math.PI / 180;
            switch (f)
            {
                case "min": N(1, 64); return a.Min();
                case "max": N(1, 64); return a.Max();
                case "abs": N(1, 1); return Math.Abs(a[0]);
                case "round":
                    N(1, 2);
                    // round(x) to a whole number, round(x; n) to n decimals, round(x; 0.05) to the nearest step under 1.
                    if (a.Count == 1) return Math.Round(a[0], MidpointRounding.AwayFromZero);
                    if (a[1] > 0 && a[1] < 1) return Math.Round(a[0] / a[1], MidpointRounding.AwayFromZero) * a[1];
                    return Math.Round(a[0], (int)Math.Clamp(a[1], 0, 12), MidpointRounding.AwayFromZero);
                case "floor": N(1, 1); return Math.Floor(a[0]);
                case "ceil": N(1, 1); return Math.Ceiling(a[0]);
                case "sqrt": N(1, 1); return a[0] < 0 ? throw new ExprException("sqrt(…) of a negative number.") : Math.Sqrt(a[0]);
                case "sin": N(1, 1); return Math.Sin(a[0] * D);
                case "cos": N(1, 1); return Math.Cos(a[0] * D);
                case "tan": N(1, 1); return Math.Tan(a[0] * D);
                case "atan2": N(2, 2); return Math.Atan2(a[0], a[1]) / D;
                case "if": N(3, 3); return a[0] != 0 ? a[1] : a[2];
                case "clamp": N(3, 3); return Math.Clamp(a[0], Math.Min(a[1], a[2]), Math.Max(a[1], a[2]));
                default: throw new ExprException($"„{f}” is not a function.");
            }
        }
    }
}

public sealed class ExprException(string message) : Exception(message);

/// <summary>
/// Live text (title blocks, symbols, tags): <c>{source.key}</c> tokens filled by a resolver, and formulas <c>{= …}</c> whose
/// names are read as the resolver's numbers (<c>{= attr.width * 2 | 0.0}</c>). An unknown token prints as <c>&lt;key&gt;</c>,
/// a formula that does not read as <c>&lt;formula&gt;</c>, each listed in <c>unknown</c>.
/// </summary>
public static class LiveText
{
    /// <summary>A text with its formulas evaluated, then its tokens filled.</summary>
    public static string Fill(string text, Func<string, (bool Known, string? Value)> resolve, List<string>? unknown = null)
    {
        if (text.Contains("{=", StringComparison.Ordinal)) text = Formulas(text, resolve, unknown);
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

    static string Formulas(string text, Func<string, (bool Known, string? Value)> resolve, List<string>? unknown)
    {
        var sb = new StringBuilder();
        var i = 0;
        while (i < text.Length)
        {
            var open = text.IndexOf("{=", i, StringComparison.Ordinal);
            if (open < 0) { sb.Append(text, i, text.Length - i); break; }
            sb.Append(text, i, open - i);
            // The formula's end: the brace that closes it (brackets inside are the formula's).
            var close = text.IndexOf('}', open + 2);
            if (close < 0) { sb.Append(text, open, text.Length - open); break; }
            var body = text[(open + 2)..close];
            var bar = body.LastIndexOf('|');
            var (formula, format) = bar >= 0 ? (body[..bar], body[(bar + 1)..]) : (body, null);
            var (value, error) = Expr.TryEval(formula, n =>
            {
                var (known, v) = resolve(n);
                return known ? Number(v) : null;
            });
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
}
