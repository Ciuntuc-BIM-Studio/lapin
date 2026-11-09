using System.IO.Compression;
using Lapin.Geometry;

namespace Lapin.Platform.Cde.PdfRemarks;

/// <summary>
/// A PDF page's drawing read for tracing (a tester's wish, #89): its paths (lines, curves flattened, rectangles), stroked or
/// filled, as polylines in page points (1/72 in, origin bottom-left, the page's rotation applied), and its pictures (a scan,
/// a photo) with the four corners they stand on and their bytes as a JPEG or a PNG. Text is left out. Lapin's own reader,
/// no library: the content streams interpreted (q/Q, cm, m l c v y h re, the painting operators, Do of forms and images).
/// </summary>
public static class PdfImport
{
    public sealed record Path(List<Vec2> Points, bool Closed);
    public sealed record Picture(byte[] Bytes, string Mime, Vec2[] Corners, int PixelWidth, int PixelHeight);
    public sealed record Page(double Width, double Height, List<Path> Paths, List<Picture> Pictures, List<string> Warnings);

    /// <summary>How many pages a PDF has, and each page's size in points (as seen, its rotation applied).</summary>
    public static List<(double W, double H)> Pages(byte[] data)
    {
        var doc = PdfDocument.Load(data);
        return doc.Pages().Select(p => Size(p)).ToList();
    }

    static double[] BoxOf(PdfDocument.Page p) => p.CropBox ?? p.MediaBox;

    static (double W, double H) Size(PdfDocument.Page p)
    {
        var b = BoxOf(p);
        double w = Math.Abs(b[2] - b[0]), h = Math.Abs(b[3] - b[1]);
        return p.Rotate is 90 or 270 ? (h, w) : (w, h);
    }

    /// <summary>One page (0-based) read: its paths and pictures, at most <paramref name="maxPoints"/> points in all.</summary>
    public static Page Read(byte[] data, int index, int maxPoints = 400_000)
    {
        var doc = PdfDocument.Load(data);
        var pages = doc.Pages();
        if (index < 0 || index >= pages.Count) throw new CommandException($"The PDF has {pages.Count} page(s).");
        var page = pages[index];
        var box = BoxOf(page);
        var (w, h) = Size(page);
        var reader = new Interpreter(doc, maxPoints);
        reader.Run(page.Dict, Matrix.Identity, 0);
        // Into the page's own frame: its box's corner at the origin, turned by its rotation (clockwise, as a viewer shows it).
        double x0 = Math.Min(box[0], box[2]), y0 = Math.Min(box[1], box[3]), bw = Math.Abs(box[2] - box[0]), bh = Math.Abs(box[3] - box[1]);
        Vec2 Seen(Vec2 p)
        {
            var (x, y) = (p.X - x0, p.Y - y0);
            return page.Rotate switch { 90 => new(y, bw - x), 180 => new(bw - x, bh - y), 270 => new(bh - y, x), _ => new(x, y) };
        }
        var paths = reader.Paths.Select(p => p with { Points = p.Points.Select(Seen).ToList() }).ToList();
        var pictures = reader.Pictures.Select(p => p with { Corners = p.Corners.Select(Seen).ToArray() }).ToList();
        return new Page(w, h, paths, pictures, [.. doc.Warnings.Distinct(), .. reader.Warnings.Distinct()]);
    }

    /// <summary>An affine matrix as PDF writes it: [a b c d e f], a point (x, y) going to (a x + c y + e, b x + d y + f).</summary>
    readonly record struct Matrix(double A, double B, double C, double D, double E, double F)
    {
        public static readonly Matrix Identity = new(1, 0, 0, 1, 0, 0);
        public Vec2 Apply(double x, double y) => new(A * x + C * y + E, B * x + D * y + F);
        /// <summary>This one first, then <paramref name="m"/> (PDF's "cm" concatenates the new one before the current).</summary>
        public Matrix Then(Matrix m) => new(A * m.A + B * m.C, A * m.B + B * m.D, C * m.A + D * m.C, C * m.B + D * m.D, E * m.A + F * m.C + m.E, E * m.B + F * m.D + m.F);
    }

    sealed class Interpreter(PdfDocument doc, int maxPoints)
    {
        public readonly List<Path> Paths = [];
        public readonly List<Picture> Pictures = [];
        public readonly List<string> Warnings = [];
        int _points;

        /// <summary>A page or a form: its content streams run with its resources, under <paramref name="ctm"/>.</summary>
        public void Run(PdfDict owner, Matrix ctm, int depth)
        {
            if (depth > 12) return;
            var resources = doc.Dict(owner["Resources"]) ?? PdfDict.Empty();
            var bytes = new List<byte>();
            var contents = doc.Resolve(owner["Contents"]);
            IEnumerable<PdfObj> streams = contents switch { PdfArray a => a.Items, null => [], _ => [contents] };
            foreach (var s in streams)
                if (doc.Resolve(s) is PdfStream st && doc.Decoded(st) is { } d) { bytes.AddRange(d); bytes.Add((byte)'\n'); }
            Content([.. bytes], resources, ctm, depth);
        }

        void Content(byte[] content, PdfDict resources, Matrix ctm, int depth)
        {
            var lexer = new PdfLexer(content);
            var operands = new List<PdfObj>();
            var stack = new Stack<Matrix>();
            var subpaths = new List<(List<Vec2> Pts, bool Closed)>();
            List<Vec2>? current = null;
            Vec2 cursor = default, start = default;
            double N(int i) => i < operands.Count && operands[i] is PdfNumber n ? n.Value : 0;
            void Close() { if (current != null) { subpaths.Add((current, true)); current = null; } }
            void Flush(bool paint, bool closeAll)
            {
                if (current != null) { subpaths.Add((current, closeAll)); current = null; }
                if (paint)
                    foreach (var (pts, closed) in subpaths)
                    {
                        if (pts.Count < 2 || _points + pts.Count > maxPoints) continue;
                        _points += pts.Count;
                        Paths.Add(new Path(pts, closed || closeAll));
                    }
                subpaths.Clear();
            }
            void To(Vec2 p) { (current ??= [ctm.Apply(cursor.X, cursor.Y)]).Add(ctm.Apply(p.X, p.Y)); cursor = p; }
            void Curve(Vec2 a, Vec2 b, Vec2 c)
            {
                var p0 = cursor;
                for (var i = 1; i <= 8; i++)
                {
                    var t = i / 8.0; var u = 1 - t;
                    To(new Vec2(u * u * u * p0.X + 3 * u * u * t * a.X + 3 * u * t * t * b.X + t * t * t * c.X, u * u * u * p0.Y + 3 * u * u * t * a.Y + 3 * u * t * t * b.Y + t * t * t * c.Y));
                }
            }
            while (lexer.Pos < lexer.Length)
            {
                var o = lexer.Parse();
                if (o is not PdfKeyword k) { operands.Add(o); if (operands.Count > 64) operands.RemoveAt(0); continue; }
                if (k.Value.Length == 0) break;
                switch (k.Value)
                {
                    case "q": stack.Push(ctm); break;
                    case "Q": if (stack.Count > 0) ctm = stack.Pop(); break;
                    case "cm": ctm = new Matrix(N(0), N(1), N(2), N(3), N(4), N(5)).Then(ctm); break;
                    case "m": if (current != null) subpaths.Add((current, false)); current = null; cursor = start = new Vec2(N(0), N(1)); current = [ctm.Apply(cursor.X, cursor.Y)]; break;
                    case "l": To(new Vec2(N(0), N(1))); break;
                    case "c": Curve(new(N(0), N(1)), new(N(2), N(3)), new(N(4), N(5))); break;
                    case "v": Curve(cursor, new(N(0), N(1)), new(N(2), N(3))); break;
                    case "y": Curve(new(N(0), N(1)), new(N(2), N(3)), new(N(2), N(3))); break;
                    case "h": if (current != null) { cursor = start; Close(); } break;
                    case "re":
                    {
                        if (current != null) subpaths.Add((current, false));
                        double x = N(0), y = N(1), rw = N(2), rh = N(3);
                        subpaths.Add(([ctm.Apply(x, y), ctm.Apply(x + rw, y), ctm.Apply(x + rw, y + rh), ctm.Apply(x, y + rh)], true));
                        current = null; cursor = start = new Vec2(x, y);
                        break;
                    }
                    case "S": Flush(true, false); break;
                    case "s": Flush(true, true); break;
                    case "f" or "F" or "f*" or "B" or "B*" or "b" or "b*": Flush(true, true); break;
                    case "n": Flush(false, false); break;
                    case "Do":
                        if (operands.Count > 0 && operands[^1] is PdfName name) Draw(name.Value, resources, ctm, depth);
                        break;
                    case "BI":
                    {
                        // An inline image: its data skipped (it is not kept), up to its end.
                        var end = lexer.IndexOf("EI", lexer.Pos);
                        lexer.Pos = end < 0 ? lexer.Length : end + 2;
                        Warnings.Add("Inline images are left out.");
                        break;
                    }
                }
                operands.Clear();
            }
        }

        /// <summary>A named XObject: a form run in place (its own matrix first), or an image kept with the corners it fills.</summary>
        void Draw(string name, PdfDict resources, Matrix ctm, int depth)
        {
            var xo = doc.Dict(resources["XObject"]);
            if (xo == null || doc.Resolve(xo[name]) is not PdfStream s) return;
            var sub = doc.Name(s.Dict["Subtype"]);
            if (sub == "Form")
            {
                var m = doc.Numbers(s.Dict["Matrix"]) is { Length: 6 } f ? new Matrix(f[0], f[1], f[2], f[3], f[4], f[5]) : Matrix.Identity;
                var form = new PdfDict(new Dictionary<string, PdfObj>(s.Dict.Map) { ["Contents"] = s });
                if (!form.Has("Resources")) form.Map["Resources"] = resources;
                Run(form, m.Then(ctm), depth + 1);
                return;
            }
            if (sub != "Image") return;
            if (Image(s) is not var (bytes, mime, pw, ph)) return;
            Pictures.Add(new Picture(bytes, mime, [ctm.Apply(0, 0), ctm.Apply(1, 0), ctm.Apply(1, 1), ctm.Apply(0, 1)], pw, ph));
        }

        /// <summary>An image XObject as a file: a JPEG as it is, 8-bit grey or RGB pixels as a PNG; null for any other.</summary>
        (byte[] Bytes, string Mime, int W, int H)? Image(PdfStream s)
        {
            int w = (int)(doc.Number(s.Dict["Width"]) ?? 0), h = (int)(doc.Number(s.Dict["Height"]) ?? 0);
            if (w <= 0 || h <= 0 || (long)w * h > 60_000_000) return null;
            var filters = doc.Resolve(s.Dict["Filter"]) switch { PdfName n => [n.Value], PdfArray a => a.Items.Select(x => doc.Name(x) ?? "").ToList(), _ => new List<string>() };
            if (filters.Count == 1 && filters[0] is "DCTDecode" or "DCT") return (s.Raw, "image/jpeg", w, h);
            var space = doc.Resolve(s.Dict["ColorSpace"]) switch { PdfName n => n.Value, PdfArray a when a.Items.Count > 0 => doc.Name(a.Items[0]), _ => null };
            var bpc = (int)(doc.Number(s.Dict["BitsPerComponent"]) ?? 8);
            var channels = space switch { "DeviceRGB" or "CalRGB" => 3, "DeviceGray" or "CalGray" => 1, "ICCBased" => (int)(doc.Number(doc.Dict((doc.Resolve(s.Dict["ColorSpace"]) as PdfArray)?.Items.ElementAtOrDefault(1))?["N"]) ?? 3), _ => 0 };
            if (bpc != 8 || channels is not (1 or 3) || filters.Any(f => f is not ("FlateDecode" or "Fl"))) { Warnings.Add("Some pictures are in a form the importer does not read (only JPEG, and 8-bit grey or colour)."); return null; }
            if (doc.Decoded(s) is not { } raw || raw.Length < (long)w * h * channels) return null;
            return (Png(raw, w, h, channels), "image/png", w, h);
        }
    }

    /// <summary>Pixels (rows top to bottom, 8 bits a channel, grey or RGB) as a PNG file.</summary>
    public static byte[] Png(byte[] pixels, int w, int h, int channels)
    {
        using var o = new MemoryStream();
        o.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        void Chunk(string type, byte[] body)
        {
            var t = System.Text.Encoding.ASCII.GetBytes(type);
            o.Write(BigEndian((uint)body.Length));
            o.Write(t); o.Write(body);
            o.Write(BigEndian(Crc(t.Concat(body))));
        }
        var ihdr = new List<byte>();
        ihdr.AddRange(BigEndian((uint)w)); ihdr.AddRange(BigEndian((uint)h));
        ihdr.AddRange([8, (byte)(channels switch { 4 => 6, 3 => 2, 2 => 4, _ => 0 }), 0, 0, 0]);
        Chunk("IHDR", [.. ihdr]);
        using (var z = new MemoryStream())
        {
            using (var zl = new ZLibStream(z, CompressionLevel.Fastest, true))
            {
                var stride = w * channels;
                for (var y = 0; y < h; y++) { zl.WriteByte(0); zl.Write(pixels, y * stride, stride); }
            }
            Chunk("IDAT", z.ToArray());
        }
        Chunk("IEND", []);
        return o.ToArray();
    }

    static byte[] BigEndian(uint v) => [(byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v];

    static readonly uint[] CrcTable = Enumerable.Range(0, 256).Select(n =>
    {
        var c = (uint)n;
        for (var k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
        return c;
    }).ToArray();

    static uint Crc(IEnumerable<byte> data)
    {
        var c = 0xFFFFFFFFu;
        foreach (var b in data) c = CrcTable[(c ^ b) & 0xFF] ^ (c >> 8);
        return c ^ 0xFFFFFFFFu;
    }
}
