using System.Globalization;
using System.Reflection;
using System.Text;
using Lapin.Geometry;
using Lapin.Drawing.Views;

namespace Lapin.Drawing.Export;

/// <summary>
/// DXF R2000 (AC1015), the old Lapin's export rewritten: every layer with its
/// colour (ACI and true colour), line type and line weight, entities BYLAYER
/// unless they differ from their layer. A complete minimal R2000 document
/// (tables, blocks, layouts, dictionaries: what strict readers expect) is kept
/// as a skeleton; this writer adds line types to its LTYPE table, layers to
/// its LAYER table and entities to ENTITIES, with handles above the
/// skeleton's, and sets $HANDSEED. Text is windows-1252.
/// </summary>
public sealed class DxfWriter(double patternScale)
{
    // The skeleton's handles: the LTYPE and LAYER tables, model space's block record, the plot-style placeholder.
    const string LtypeTable = "2", LayerTable = "1", ModelSpace = "17", PlotStyle = "13", BlockRecordTable = "9";
    static readonly int[] Weights = [0, 5, 9, 13, 15, 18, 20, 25, 30, 35, 40, 50, 53, 60, 70, 80, 90, 100, 106, 120, 140, 158, 200, 211];
    static readonly (int Index, int R, int G, int B)[] Aci =
        [(1, 255, 0, 0), (2, 255, 255, 0), (3, 0, 255, 0), (4, 0, 255, 255), (5, 0, 0, 255), (6, 255, 0, 255), (7, 0, 0, 0), (8, 128, 128, 128), (9, 192, 192, 192)];

    sealed record LayerDef(string Color, string LineType, int Weight);
    readonly Dictionary<string, LayerDef> _layers = [];
    readonly Dictionary<string, double[]> _lineTypes = [];
    readonly Dictionary<string, string> _lineTypeOf = []; // a pattern (its dashes as text) → its line type's name
    readonly List<string> _out = [];
    // Blocks of the user's (a title block, decision 340): their records and definitions; entities go to the target with its owner.
    readonly List<string> _blockRecords = [];
    readonly List<string> _blocks = [];
    int _blockCount;
    List<string>? _target;
    string _owner = ModelSpace;
    int _handle = 0x100;

    public DxfWriter() : this(1) { }

    string Next() => (_handle++).ToString("X");

    /// <summary>A pen width (mm) as the nearest DXF line weight (1/100 mm).</summary>
    public static int LineWeight(double mm)
    {
        var v = (int)Math.Round(mm * 100);
        return Weights.Aggregate(25, (best, w) => Math.Abs(w - v) < Math.Abs(best - v) ? w : best);
    }

    public static (int R, int G, int B) Rgb(string? css) => HexColor.Bytes(css);

    /// <summary>The nearest ACI colour (black and white both 7, "foreground").</summary>
    public static int AciOf(string? css)
    {
        var (r, g, b) = Rgb(css);
        return Aci.MinBy(a => (r - a.R) * (r - a.R) + (g - a.G) * (g - a.G) + (b - a.B) * (b - a.B)).Index;
    }

    static int TrueColor(string? css) { var (r, g, b) = Rgb(css); return (r << 16) | (g << 8) | b; }
    static string Fmt(double v) => double.IsFinite(v) ? Math.Round(v, 4).ToString("0.####", CultureInfo.InvariantCulture) : "0";
    /// <summary>
    /// A symbol table name: what names may not hold (&lt;&gt;/\":;?*|=` and the comma)
    /// as '_', what windows-1252 lacks as \U+XXXX, as AutoCAD writes its own
    /// names in an R2000 DXF; at most 255 characters.
    /// </summary>
    static string Safe(string s) => new string(s.Select(c => "<>/\\\":;?*|=`,".Contains(c) || char.IsControl(c) ? '_' : c).ToArray()).Trim() is { Length: > 0 } t ? Cp1252(t[..Math.Min(255, t.Length)]) : "0";

    /// <summary>A line type for a dash pattern (paper mm), registered once; Continuous for none.</summary>
    string LineType(double[]? dash)
    {
        if (dash is not { Length: > 0 }) return "Continuous";
        var key = string.Join(",", dash.Select(Fmt));
        if (_lineTypeOf.TryGetValue(key, out var hit)) return hit;
        var name = $"LAPIN{_lineTypes.Count + 1}";
        _lineTypes[name] = dash;
        return _lineTypeOf[key] = name;
    }

    /// <summary>Register a layer (its first pen defines it); its DXF name.</summary>
    public string Layer(string raw, string color, double width, double[]? dash)
    {
        var name = Safe(raw);
        if (!_layers.ContainsKey(name)) _layers[name] = new LayerDef(color, LineType(dash), LineWeight(width));
        return name;
    }

    IEnumerable<string> Own(string layer, string? color, double? width, double[]? dash)
    {
        var l = _layers[layer];
        if (color != null && (AciOf(color) != AciOf(l.Color) || TrueColor(color) != TrueColor(l.Color))) { yield return "62"; yield return AciOf(color).ToString(CultureInfo.InvariantCulture); yield return "420"; yield return TrueColor(color).ToString(CultureInfo.InvariantCulture); }
        var lt = LineType(dash);
        if (dash != null && lt != l.LineType) { yield return "6"; yield return lt; }
        if (width is { } w && LineWeight(w) != l.Weight) { yield return "370"; yield return LineWeight(w).ToString(CultureInfo.InvariantCulture); }
    }

    void Head(string kind, string layer, string? color = null, double? width = null, double[]? dash = null) =>
        Target.AddRange(["0", kind, "5", Next(), "330", _owner, "100", "AcDbEntity", "8", layer, .. Own(layer, color, width, dash)]);

    List<string> Target => _target ?? _out;

    /// <summary>
    /// A block of the user's: its entities written by <paramref name="body"/> (as model space's would be), its attributes'
    /// definitions after them (tag, default value, where, height, alignment); the block's base point is 0,0.
    /// </summary>
    public void Block(string rawName, Action body, IReadOnlyList<(string Tag, string Value, Vec2 At, double Height, double Rotation, string Align, string Layer)> attributes)
    {
        var name = Safe(rawName);
        var record = Next();
        _blockCount++;
        _blockRecords.AddRange(["0", "BLOCK_RECORD", "5", record, "330", BlockRecordTable, "100", "AcDbSymbolTableRecord", "100", "AcDbBlockTableRecord", "2", name, "340", "0"]);
        var list = new List<string>();
        list.AddRange(["0", "BLOCK", "5", Next(), "330", record, "100", "AcDbEntity", "8", "0", "100", "AcDbBlockBegin", "2", name,
            "70", attributes.Count > 0 ? "2" : "0", "10", "0", "20", "0", "30", "0", "3", name, "1", ""]);
        (_target, _owner) = (list, record);
        try
        {
            body();
            foreach (var at in attributes)
            {
                Head("ATTDEF", at.Layer);
                list.AddRange(TextBody(at.At, Cp1252(at.Value), at.Height, at.Rotation, at.Align));
                list.AddRange(["100", "AcDbAttributeDefinition", "3", at.Tag, "2", at.Tag, "70", "0"]);
            }
        }
        finally { (_target, _owner) = (null, ModelSpace); }
        list.AddRange(["0", "ENDBLK", "5", Next(), "330", record, "100", "AcDbEntity", "8", "0", "100", "AcDbBlockEnd"]);
        _blocks.AddRange(list);
    }

    /// <summary>A block inserted at 0,0 in model space with its attributes' values (as its definitions place them).</summary>
    public void Insert(string rawName, string layer, IReadOnlyList<(string Tag, string Value, Vec2 At, double Height, double Rotation, string Align, string Layer)> attributes)
    {
        var name = Safe(rawName);
        var insert = Next();
        _out.AddRange(["0", "INSERT", "5", insert, "330", ModelSpace, "100", "AcDbEntity", "8", layer, "100", "AcDbBlockReference"]);
        if (attributes.Count > 0) _out.AddRange(["66", "1"]);
        _out.AddRange(["2", name, "10", "0", "20", "0", "30", "0"]);
        if (attributes.Count == 0) return;
        foreach (var at in attributes)
        {
            _out.AddRange(["0", "ATTRIB", "5", Next(), "330", insert, "100", "AcDbEntity", "8", at.Layer]);
            _out.AddRange(TextBody(at.At, Cp1252(at.Value), at.Height, at.Rotation, at.Align));
            _out.AddRange(["100", "AcDbAttribute", "2", at.Tag, "70", "0"]);
        }
        _out.AddRange(["0", "SEQEND", "5", Next(), "330", insert, "100", "AcDbEntity", "8", layer]);
    }

    /// <summary>A text's AcDbText part (an attribute's too): where, height, value, rotation, style, alignment.</summary>
    static List<string> TextBody(Vec2 at, string value, double height, double rotation, string align)
    {
        var o = new List<string> { "100", "AcDbText", "10", Fmt(at.X), "20", Fmt(at.Y), "30", "0", "40", Fmt(height), "1", value, "50", Fmt(rotation), "7", "Standard" };
        var h = align == "center" ? 1 : align == "right" ? 2 : 0;
        if (h != 0) o.AddRange(["72", h.ToString(CultureInfo.InvariantCulture), "11", Fmt(at.X), "21", Fmt(at.Y), "31", "0"]);
        return o;
    }

    public void Line(Vec2 a, Vec2 b, string layer, string? color = null, double? width = null, double[]? dash = null)
    {
        Head("LINE", layer, color, width, dash);
        Target.AddRange(["100", "AcDbLine", "10", Fmt(a.X), "20", Fmt(a.Y), "30", "0", "11", Fmt(b.X), "21", Fmt(b.Y), "31", "0"]);
    }

    public void Polyline(IReadOnlyList<Vec2> pts, string layer, bool closed, string? color = null, double? width = null)
    {
        if (pts.Count < 2) return;
        Head("LWPOLYLINE", layer, color, width);
        Target.AddRange(["100", "AcDbPolyline", "90", pts.Count.ToString(CultureInfo.InvariantCulture), "70", closed ? "1" : "0"]);
        foreach (var p in pts) Target.AddRange(["10", Fmt(p.X), "20", Fmt(p.Y)]);
    }

    /// <summary>A filled triangle (SOLID takes its corners 1, 2, 4, 3; a triangle repeats its last).</summary>
    public void Solid(Vec2 a, Vec2 b, Vec2 c, string layer, string? color = null)
    {
        Head("SOLID", layer, color);
        Target.AddRange(["100", "AcDbTrace", "10", Fmt(a.X), "20", Fmt(a.Y), "30", "0", "11", Fmt(b.X), "21", Fmt(b.Y), "31", "0",
            "12", Fmt(c.X), "22", Fmt(c.Y), "32", "0", "13", Fmt(c.X), "23", Fmt(c.Y), "33", "0"]);
    }

    /// <summary>A text: its height in drawing units, its rotation (°), aligned left, centre or right; slanted (°, italic) when asked.</summary>
    public void Text(Vec2 at, string value, double height, string layer, double rotation = 0, string align = "left", string? color = null, double oblique = 0)
    {
        var clean = Cp1252(value.Replace("\r", " ").Replace("\n", " ").Trim());
        if (clean.Length == 0 || !(height > 0)) return;
        Head("TEXT", layer, color);
        Target.AddRange(["100", "AcDbText", "10", Fmt(at.X), "20", Fmt(at.Y), "30", "0", "40", Fmt(height), "1", clean, "50", Fmt(rotation), "7", "Standard"]);
        if (oblique != 0) Target.AddRange(["51", Fmt(oblique)]);
        var h = align == "center" ? 1 : align == "right" ? 2 : 0;
        if (h != 0) Target.AddRange(["72", h.ToString(CultureInfo.InvariantCulture), "11", Fmt(at.X), "21", Fmt(at.Y), "31", "0"]);
        Target.AddRange(["100", "AcDbText"]);
    }

    /// <summary>The characters windows-1252 has beyond Latin-1's (its 0x80–0x9F: €, „, …, Š, ž), for <see cref="Cp1252"/>.</summary>
    static readonly HashSet<char> Cp1252Extra = [.. "€‚ƒ„…†‡ˆ‰Š‹ŒŽ‘’“”•–—˜™š›œžŸ"];

    /// <summary>
    /// Text as windows-1252 carries it: what the code page lacks (ă, ș, ț) as \U+XXXX; a character beyond the BMP (an
    /// emoji), which that escape's four digits cannot hold, as one replacement character, not as its two surrogates.
    /// </summary>
    static string Cp1252(string s)
    {
        var b = new StringBuilder(s.Length);
        foreach (var r in s.EnumerateRunes())
        {
            var v = r.Value;
            if (v < 0x80 || (v >= 0xA0 && v <= 0xFF) || (r.IsBmp && Cp1252Extra.Contains((char)v))) b.Append(r.ToString());
            else b.Append($"\\U+{(r.IsBmp ? v : 0xFFFD):X4}");
        }
        return b.ToString();
    }

    List<string> LineTypes()
    {
        var o = new List<string>();
        foreach (var (name, dash) in _lineTypes)
        {
            // Dashes positive, gaps negative, in drawing units.
            var d = dash.Select((v, i) => (i % 2 == 0 ? v : -v) * patternScale).ToArray();
            o.AddRange(["0", "LTYPE", "5", Next(), "330", LtypeTable, "100", "AcDbSymbolTableRecord", "100", "AcDbLinetypeTableRecord",
                "2", name, "70", "0", "3", name, "72", "65", "73", d.Length.ToString(CultureInfo.InvariantCulture), "40", Fmt(d.Sum(Math.Abs))]);
            foreach (var v in d) o.AddRange(["49", Fmt(v), "74", "0"]);
        }
        return o;
    }

    List<string> LayerTableRecords()
    {
        var o = new List<string>();
        foreach (var (name, l) in _layers)
        {
            if (SkeletonLayers.Contains(name, StringComparer.OrdinalIgnoreCase)) continue;
            o.AddRange(["0", "LAYER", "5", Next(), "330", LayerTable, "100", "AcDbSymbolTableRecord", "100", "AcDbLayerTableRecord",
                "2", name, "70", "0", "62", AciOf(l.Color).ToString(CultureInfo.InvariantCulture), "420", TrueColor(l.Color).ToString(CultureInfo.InvariantCulture),
                "6", l.LineType, "370", l.Weight.ToString(CultureInfo.InvariantCulture), "390", PlotStyle]);
        }
        return o;
    }

    static string Skeleton()
    {
        using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("Lapin.Drawing.Export.dxf-r2000-skeleton.dxf")!;
        // LF whatever the checkout did to the file (Git on Windows may turn it to CRLF): the placeholders end in \n.
        return new StreamReader(s).ReadToEnd().ReplaceLineEndings("\n");
    }

    /// <summary>The layers the skeleton has already.</summary>
    static readonly string[] SkeletonLayers = ["0", "Defpoints"];
    const int SkeletonLineTypes = 3;

    /// <summary>The document, windows-1252 encoded.</summary>
    /// <summary>The drawing's units as $INSUNITS says them: 4 millimetres (Lapin's own), 6 metres (map coordinates, decision 447).</summary>
    public int Units { get; init; } = 4;

    public byte[] Bytes(string comment)
    {
        var lt = LineTypes();
        var layers = LayerTableRecords();
        var layerCount = SkeletonLayers.Length + _layers.Keys.Count(n => !SkeletonLayers.Contains(n, StringComparer.OrdinalIgnoreCase));
        static string Pairs(List<string> l) => string.Concat(l.Select((v, i) => (i % 2 == 0 ? v.PadLeft(3) : v) + "\n"));
        var text = Skeleton();
        var nl = text.IndexOf('\n', 4);
        text = "999\n" + comment.Replace("\n", " ") + text[nl..];
        if (Units != 4) text = text.Replace("$INSUNITS\n 70\n4\n", $"$INSUNITS\n 70\n{Units.ToString(CultureInfo.InvariantCulture)}\n");
        text = text.Replace("{{BLOCKRECORDS}}\n", Pairs(_blockRecords)).Replace("{{BLOCKS}}\n", Pairs(_blocks))
            .Replace("{{BLOCKRECORDCOUNT}}", (2 + _blockCount).ToString(CultureInfo.InvariantCulture));
        text = text.Replace("{{LTYPES}}\n", Pairs(lt)).Replace("{{LAYERS}}\n", Pairs(layers)).Replace("{{ENTITIES}}\n", Pairs(_out)).Replace("{{HANDSEED}}", _handle.ToString("X"))
            .Replace("{{LTYPECOUNT}}", (SkeletonLineTypes + _lineTypes.Count).ToString(CultureInfo.InvariantCulture)).Replace("{{LAYERCOUNT}}", layerCount.ToString(CultureInfo.InvariantCulture));
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1252).GetBytes(text);
    }

    /// <summary>
    /// Pages of pens (paper mm, y down) as one DXF each, y up, at 1:1 in mm. Pens of a block (a title block, decision 340) are
    /// written as that block's definition, inserted at 0,0, their live texts (a tag) as its attributes.
    /// </summary>
    public static byte[] Page(PaperPage page, string comment, int units = 4)
    {
        var w = new DxfWriter { Units = units };
        Vec2 P(Vec2 p) => new(p.X, page.H - p.Y);
        void Draw(Pen pen, Func<PenText, bool> keep)
        {
            if (pen.Fill != null && pen.Loops.Count > 0)
            {
                var layer = w.Layer(pen.Layer, pen.Fill, 0, null);
                foreach (var (a, b, c) in Triangles(pen.Loops)) w.Solid(P(a), P(b), P(c), layer, pen.Fill);
            }
            if (pen.Stroke != null && pen.Segments.Count > 0)
            {
                var layer = w.Layer(pen.Layer, pen.Stroke, pen.Width, pen.Dash);
                foreach (var (a, b) in pen.Segments) w.Line(P(a), P(b), layer, pen.Stroke, pen.Width, pen.Dash);
            }
            if (pen.Texts.Count > 0)
            {
                var layer = w.Layer("TEXT", pen.Stroke ?? "#000000", 0.18, null);
                foreach (var t in pen.Texts.Where(keep))
                {
                    var (at, align) = Placed(t);
                    w.Text(at, t.Text, t.Height, layer, t.Angle, align, pen.Stroke, t.Italic ? 12 : 0);
                }
            }
        }
        // DXF texts sit on their baseline; a middle one comes down half its height.
        (Vec2 At, string Align) Placed(PenText t)
        {
            var rad = t.Angle * Math.PI / 180;
            var at = P(t.At) - (t.Baseline == "middle" ? new Vec2(-Math.Sin(rad), Math.Cos(rad)) * (t.Height / 2) : new Vec2(0, 0));
            return (at, t.Anchor switch { "middle" => "center", "end" => "right", _ => "left" });
        }
        var inserts = new List<(string Name, List<(string, string, Vec2, double, double, string, string)> Attributes)>();
        foreach (var group in page.Pens.Where(p => p.Block != null).GroupBy(p => p.Block!))
        {
            var attributes = new List<(string Tag, string Value, Vec2 At, double Height, double Rotation, string Align, string Layer)>();
            var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pen in group)
                foreach (var t in pen.Texts.Where(t => t.Tag != null))
                {
                    var tag = Safe(t.Tag!).ToUpperInvariant().Replace(' ', '_');
                    for (var n = 2; !tags.Add(tag); n++) tag = $"{Safe(t.Tag!).ToUpperInvariant()}_{n}";
                    var (at, align) = Placed(t);
                    attributes.Add((tag, t.Text.Replace("\r", " ").Replace("\n", " ").Trim(), at, t.Height, t.Angle, align, w.Layer("TEXT", pen.Stroke ?? "#000000", 0.18, null)));
                }
            w.Block(group.Key, () => { foreach (var pen in group) Draw(pen, t => t.Tag == null); }, attributes);
            inserts.Add((group.Key, attributes));
        }
        foreach (var pen in page.Pens.Where(p => p.Block == null)) Draw(pen, _ => true);
        foreach (var (name, attributes) in inserts) w.Insert(name, w.Layer("TITLE", "#000000", 0.25, null), attributes);
        return w.Bytes(comment);
    }

    /// <summary>
    /// Loops filled even-odd, as triangles: each outer loop with the holes directly inside it. A loop's depth is read at
    /// a point inside it (<see cref="Polygon2.InteriorPoint"/>), not at a vertex: two areas of one pen that share an edge
    /// (abutting walls of a material) put one's vertices on the other's boundary, which made it a hole of it and lost
    /// both fills. Only the loops whose bounds hold that point are tested.
    /// </summary>
    public static IEnumerable<(Vec2, Vec2, Vec2)> Triangles(List<IReadOnlyList<Vec2>> loops)
    {
        var ls = loops.Where(l => l.Count >= 3).Select(l => l.ToArray()).ToList();
        var box = ls.Select(l => (X0: l.Min(p => p.X), Y0: l.Min(p => p.Y), X1: l.Max(p => p.X), Y1: l.Max(p => p.Y))).ToArray();
        var inner = ls.Select(l => Polygon2.InteriorPoint(l)).ToArray();
        bool Holds(int j, int i) => j != i && inner[i].X >= box[j].X0 && inner[i].X <= box[j].X1 && inner[i].Y >= box[j].Y0 && inner[i].Y <= box[j].Y1
            && Polygon2.Contains(ls[j], inner[i]);
        var depth = Enumerable.Range(0, ls.Count).Select(i => Enumerable.Range(0, ls.Count).Count(j => Holds(j, i))).ToArray();
        for (var i = 0; i < ls.Count; i++)
        {
            if (depth[i] % 2 != 0) continue;
            var holes = Enumerable.Range(0, ls.Count).Where(j => depth[j] == depth[i] + 1 && Holds(i, j)).ToList();
            var pts = new List<Vec3>();
            int[] Add(Vec2[] l) { var ids = new int[l.Length]; for (var k = 0; k < l.Length; k++) { ids[k] = pts.Count; pts.Add(new Vec3(l[k].X, l[k].Y, 0)); } return ids; }
            var outer = Add(ls[i]);
            var hs = holes.Select(j => Add(ls[j])).ToArray();
            IEnumerable<(int, int, int)> tris;
            try { tris = Triangulator.Face(pts, outer, hs, new Vec3(0, 0, 1)).ToList(); }
            catch (Exception) { continue; }
            foreach (var (a, b, c) in tris) yield return (new Vec2(pts[a].X, pts[a].Y), new Vec2(pts[b].X, pts[b].Y), new Vec2(pts[c].X, pts[c].Y));
        }
    }
}
