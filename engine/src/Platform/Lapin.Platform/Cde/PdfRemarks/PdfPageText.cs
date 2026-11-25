using System.Text;

namespace Lapin.Platform.Cde.PdfRemarks;

/// <summary>
/// A PDF's text, page by page (docs/plan-cde-references.md §2: the text a reference document carries, for its suggestions and
/// for whoever reads it): each page's content streams interpreted (ISO 32000-1 §8–9: q/Q, cm, BT/ET with the text state and
/// positioning operators, Tj/TJ/'/", Do of forms) and the glyphs shown, decoded by their fonts (<see cref="PdfFont"/>: ToUnicode,
/// the encodings, glyph names), put into lines by where they stand: a new line where the baseline moves, a space where a gap
/// is wider than a quarter of the letters' height. Adapted from Atrium's sheet reader (Atrium.Checks, MIT-0), its text part
/// only. Nothing is rendered; a scan has no text (there is no OCR), and an encrypted file is refused.
/// </summary>
public static class PdfPageText
{
    const int MaxOperators = 4_000_000, MaxChars = 2_000_000;

    /// <summary>The text of the pages read (1-based numbers), the file's page count, and why it has no text when it has none.</summary>
    public sealed record Result(IReadOnlyList<(int Page, string Text)> Pages, int PageCount, string? Unreadable)
    {
        public string All => string.Join("\n\n", Pages.Select(p => p.Text));
    }

    /// <param name="pages">The pages wanted (1-based); null: all.</param>
    public static Result Read(byte[] data, IReadOnlyCollection<int>? pages = null)
    {
        PdfDocument doc;
        try { doc = PdfDocument.Load(data); }
        catch (Exception e) { return new Result([], 0, $"not a readable PDF ({e.Message})"); }
        if (doc.Trailer.Has("Encrypt")) return new Result([], 0, "it is encrypted");
        List<PdfDocument.Page> all;
        try { all = doc.Pages(); }
        catch (Exception e) { return new Result([], 0, $"its pages could not be read ({e.Message})"); }
        if (all.Count == 0) return new Result([], 0, "it has no pages");
        var o = new List<(int, string)>();
        var budget = new Budget();
        for (var i = 0; i < all.Count; i++)
        {
            if (pages != null && !pages.Contains(i + 1)) continue;
            var reader = new Interpreter(doc, budget);
            try { reader.Run(all[i].Dict, Matrix.Identity, 0); }
            catch (Exception) { /* what was read stands */ }
            o.Add((i + 1, reader.Text()));
        }
        var none = o.All(p => string.IsNullOrWhiteSpace(p.Item2)) ? "it has no text layer (a scan?)" : null;
        return new Result(o, all.Count, none);
    }

    sealed class Budget { public int Operators; public int Chars; }

    readonly record struct Matrix(double A, double B, double C, double D, double E, double F)
    {
        public static readonly Matrix Identity = new(1, 0, 0, 1, 0, 0);
        public (double X, double Y) Apply(double x, double y) => (A * x + C * y + E, B * x + D * y + F);
        /// <summary>This one first, then <paramref name="m"/>.</summary>
        public Matrix Then(Matrix m) => new(A * m.A + B * m.C, A * m.B + B * m.D, C * m.A + D * m.C, C * m.B + D * m.D, E * m.A + F * m.C + m.E, E * m.B + F * m.D + m.F);
    }

    sealed record State(Matrix Ctm, PdfFont? Font, double Size, double CharSpace, double WordSpace, double Scale, double Leading, double Rise);

    sealed class Interpreter(PdfDocument doc, Budget budget)
    {
        readonly Dictionary<PdfDict, PdfFont> _fonts = new(ReferenceEqualityComparer.Instance);
        readonly StringBuilder _text = new();
        readonly StringBuilder _line = new();
        // Where the line being built stands: its baseline (device y), where its last glyph ended (device x), its letters' height.
        double _lineY, _lineEnd, _lineHeight;
        bool _hasLine;

        public string Text()
        {
            FlushLine();
            return _text.ToString().TrimEnd();
        }

        void FlushLine()
        {
            if (_line.Length > 0) _text.Append(_line.ToString().TrimEnd()).Append('\n');
            _line.Clear();
            _hasLine = false;
        }

        public void Run(PdfDict owner, Matrix ctm, int depth)
        {
            if (depth > 12) return;
            var resources = doc.Dict(owner["Resources"]) ?? PdfDict.Empty();
            var bytes = new List<byte>();
            var contents = doc.Resolve(owner["Contents"]);
            IEnumerable<PdfObj> streams = contents switch { PdfArray a => a.Items, null => [], _ => [contents] };
            foreach (var s in streams)
                if (doc.Resolve(s) is PdfStream st && doc.Decoded(st) is { } d) { bytes.AddRange(d); bytes.Add((byte)'\n'); }
            Content([.. bytes], resources, new State(ctm, null, 0, 0, 0, 1, 0, 0), depth);
        }

        void Content(byte[] content, PdfDict resources, State state, int depth)
        {
            var lexer = new PdfLexer(content);
            var operands = new List<PdfObj>();
            var stack = new Stack<State>();
            Matrix tm = Matrix.Identity, tlm = Matrix.Identity;
            double N(int i) => i < operands.Count && operands[i] is PdfNumber n ? n.Value : 0;
            while (lexer.Pos < lexer.Length)
            {
                if (++budget.Operators > MaxOperators || budget.Chars > MaxChars) return;
                var o = lexer.Parse();
                if (o is not PdfKeyword k) { operands.Add(o); if (operands.Count > 64) operands.RemoveAt(0); continue; }
                if (k.Value.Length == 0) break;
                switch (k.Value)
                {
                    case "q": stack.Push(state); break;
                    case "Q": if (stack.Count > 0) state = stack.Pop(); break;
                    case "cm": state = state with { Ctm = new Matrix(N(0), N(1), N(2), N(3), N(4), N(5)).Then(state.Ctm) }; break;
                    case "BT": tm = tlm = Matrix.Identity; break;
                    case "Tf":
                        state = state with { Size = N(1) };
                        if (operands.Count > 0 && operands[0] is PdfName fontName) state = state with { Font = Font(resources, fontName.Value) };
                        break;
                    case "Tc": state = state with { CharSpace = N(0) }; break;
                    case "Tw": state = state with { WordSpace = N(0) }; break;
                    case "Tz": state = state with { Scale = N(0) / 100 }; break;
                    case "TL": state = state with { Leading = N(0) }; break;
                    case "Ts": state = state with { Rise = N(0) }; break;
                    case "Td": tm = tlm = new Matrix(1, 0, 0, 1, N(0), N(1)).Then(tlm); break;
                    case "TD": state = state with { Leading = -N(1) }; tm = tlm = new Matrix(1, 0, 0, 1, N(0), N(1)).Then(tlm); break;
                    case "Tm": tm = tlm = new Matrix(N(0), N(1), N(2), N(3), N(4), N(5)); break;
                    case "T*": tm = tlm = new Matrix(1, 0, 0, 1, 0, -state.Leading).Then(tlm); break;
                    case "Tj": if (operands.Count > 0 && operands[^1] is PdfString tj) Show(tj.Bytes, state, ref tm); break;
                    case "'":
                        tm = tlm = new Matrix(1, 0, 0, 1, 0, -state.Leading).Then(tlm);
                        if (operands.Count > 0 && operands[^1] is PdfString q1) Show(q1.Bytes, state, ref tm);
                        break;
                    case "\"":
                        state = state with { WordSpace = N(0), CharSpace = N(1) };
                        tm = tlm = new Matrix(1, 0, 0, 1, 0, -state.Leading).Then(tlm);
                        if (operands.Count > 2 && operands[2] is PdfString q2) Show(q2.Bytes, state, ref tm);
                        break;
                    case "TJ":
                        if (operands.Count > 0 && operands[^1] is PdfArray array)
                            foreach (var item in array.Items)
                            {
                                if (item is PdfString ts) Show(ts.Bytes, state, ref tm);
                                else if (item is PdfNumber adjust) tm = new Matrix(1, 0, 0, 1, -adjust.Value / 1000 * state.Size * state.Scale, 0).Then(tm);
                            }
                        break;
                    case "Do":
                        if (operands.Count > 0 && operands[^1] is PdfName xo) Draw(xo.Value, resources, state, depth);
                        break;
                    case "BI":
                    {
                        var id = lexer.IndexOf("ID", lexer.Pos);
                        var end = id < 0 ? -1 : lexer.IndexOf("EI", id + 2);
                        lexer.Pos = end < 0 ? lexer.Length : end + 2;
                        break;
                    }
                }
                operands.Clear();
            }
        }

        PdfFont Font(PdfDict resources, string name)
        {
            if (doc.Dict(doc.Dict(resources["Font"])?[name]) is not { } dict) return PdfFont.Fallback;
            if (!_fonts.TryGetValue(dict, out var font))
            {
                try { font = new PdfFont(doc, dict); }
                catch (Exception) { font = PdfFont.Fallback; }
                _fonts[dict] = font;
            }
            return font;
        }

        /// <summary>A shown string: each glyph placed by the text rendering matrix, into the line it stands on.</summary>
        void Show(byte[] bytes, State state, ref Matrix tm)
        {
            var font = state.Font ?? PdfFont.Fallback;
            double size = state.Size, th = state.Scale;
            foreach (var (text, w0, wordSpace) in font.Decode(bytes))
            {
                var trm = new Matrix(size * th, 0, 0, size, 0, state.Rise).Then(tm).Then(state.Ctm);
                var advance = (w0 * size + state.CharSpace + (wordSpace ? state.WordSpace : 0)) * th;
                var origin = trm.Apply(0, 0);
                var end = trm.Apply(w0, 0);
                var height = Math.Max(Math.Sqrt(trm.C * trm.C + trm.D * trm.D), 1e-6);
                tm = new Matrix(1, 0, 0, 1, advance, 0).Then(tm);
                if (text.Length == 0) continue;
                budget.Chars += text.Length;
                if (_hasLine)
                {
                    var h = Math.Max(height, _lineHeight);
                    // Another baseline, or back to the left of where the line began: a new line.
                    if (Math.Abs(origin.Y - _lineY) > 0.5 * h || origin.X < _lineEnd - 4 * h) FlushLine();
                    else if (origin.X - _lineEnd > 0.25 * h && _line.Length > 0 && _line[^1] != ' ' && !text.StartsWith(' ')) _line.Append(' ');
                }
                if (!_hasLine) { _hasLine = true; _lineY = origin.Y; _lineHeight = height; }
                if (string.IsNullOrWhiteSpace(text)) { if (_line.Length > 0 && _line[^1] != ' ') _line.Append(' '); }
                else _line.Append(text);
                _lineEnd = end.X;
                _lineHeight = height;
            }
        }

        void Draw(string name, PdfDict resources, State state, int depth)
        {
            var xobjects = doc.Dict(resources["XObject"]);
            if (xobjects == null || doc.Resolve(xobjects[name]) is not PdfStream s) return;
            if (doc.Name(s.Dict["Subtype"]) != "Form" || depth >= 12) return;
            var m = doc.Numbers(s.Dict["Matrix"]) is { Length: 6 } f ? new Matrix(f[0], f[1], f[2], f[3], f[4], f[5]) : Matrix.Identity;
            var formResources = doc.Dict(s.Dict["Resources"]) ?? resources;
            if (doc.Decoded(s) is not { } bytes) return;
            Content(bytes, formResources, state with { Ctm = m.Then(state.Ctm) }, depth + 1);
        }
    }
}
