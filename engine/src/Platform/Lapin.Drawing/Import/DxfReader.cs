using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Lapin.Geometry;
using Lapin.Drawing.Hatch;

namespace Lapin.Drawing.Import;

/// <summary>A DXF layer: its colour (CSS), pen width (paper mm), the line type's name, and its state.</summary>
public sealed record DxfLayer(string Name, string Color, double Weight, string LineType, bool On, bool Frozen, bool Locked)
{
    public bool Visible => On && !Frozen;
}

/// <summary>A block: its base point and what it holds.</summary>
public sealed record DxfBlock(string Name, Vec2 Base, List<DxfEntity> Entities);

/// <summary>A hatch pattern line as a DXF carries it (already scaled and turned by the hatch): angle (°), base point, offset (absolute), dashes (drawing units).</summary>
public sealed record DxfPatternLine(double AngleDeg, Vec2 Base, Vec2 Offset, IReadOnlyList<double> Dashes);

/// <summary>
/// An entity of model space (or of a block), in drawing units. <see cref="Aci"/>
/// is its colour index (256 by layer, 0 by block), <see cref="TrueColor"/> its
/// 24-bit colour when it has one.
/// </summary>
public abstract record DxfEntity
{
    public string Layer { get; init; } = "0";
    public int Aci { get; init; } = 256;
    public int? TrueColor { get; init; }
    public string? LineType { get; init; }

    public bool ByLayer => Aci == 256 && TrueColor == null;
    public bool ByBlock => Aci == 0 && TrueColor == null;
    /// <summary>The entity's own colour as CSS; null when it takes its layer's or its block's.</summary>
    public string? Color => TrueColor is { } t ? DxfColors.Css(t) : Aci is > 0 and < 256 ? DxfColors.Aci(Aci) : null;
}

public sealed record DxfLine(Vec2 A, Vec2 B) : DxfEntity
{
    /// <summary>The ends' heights (codes 30 and 31), when the file gives them: a surveyed line, a 3D contour.</summary>
    public double? ZA { get; init; }
    public double? ZB { get; init; }
}
/// <summary>A polyline: vertices with the bulge of the segment leaving each (0: straight).</summary>
public sealed record DxfPolyline(List<(Vec2 P, double Bulge)> Vertices, bool Closed) : DxfEntity
{
    /// <summary>A 2D polyline's elevation (LWPOLYLINE 38, POLYLINE 30): a contour line's height.</summary>
    public double? Elevation { get; init; }
    /// <summary>A 3D polyline's vertices' heights, one a vertex.</summary>
    public List<double>? Zs { get; init; }
}
public sealed record DxfCircle(Vec2 C, double R) : DxfEntity;
/// <summary>An arc counter-clockwise from <see cref="StartDeg"/> to <see cref="EndDeg"/>.</summary>
public sealed record DxfArc(Vec2 C, double R, double StartDeg, double EndDeg) : DxfEntity;
/// <summary>An ellipse: its centre, major half-axis vector, the minor/major ratio, and its parameter range (radians, counter-clockwise).</summary>
public sealed record DxfEllipse(Vec2 C, Vec2 Major, double Ratio, double Start, double End) : DxfEntity;
public sealed record DxfSpline(int Degree, List<double> Knots, List<Vec2> Controls, List<double> Weights, List<Vec2> Fits, bool Closed) : DxfEntity;
/// <summary>A text (TEXT, or MTEXT already stripped of its formatting): where it is anchored, its height and rotation, how it is aligned (0 left/1 centre/2 right; 0 baseline/1 bottom/2 middle/3 top).</summary>
public sealed record DxfText(Vec2 At, double Height, double RotationDeg, string Value, int HAlign, int VAlign) : DxfEntity
{
    /// <summary>The insertion point's height, when the file gives one.</summary>
    public double? Z { get; init; }
}
/// <summary>A hatch: its pattern's name, whether solid, its loops (tessellated, drawing units), and the pattern lines the file carries.</summary>
public sealed record DxfHatch(string Pattern, bool Solid, double Scale, double AngleDeg, List<Vec2[]> Loops, List<DxfPatternLine> Definition) : DxfEntity;
public sealed record DxfInsert(string Block, Vec2 At, double Sx, double Sy, double RotationDeg, int Cols, int Rows, double ColSpacing, double RowSpacing, List<DxfText> Attribs) : DxfEntity
{
    /// <summary>The insertion point's height (a survey point's block standing at it).</summary>
    public double? Z { get; init; }
}
/// <summary>A filled quad (SOLID, TRACE), its corners in drawing order.</summary>
public sealed record DxfSolid(Vec2[] Pts) : DxfEntity;
/// <summary>A POINT: a surveyed point, with its height.</summary>
public sealed record DxfPoint(Vec3 P) : DxfEntity;
/// <summary>A dimension: its kind (0 rotated, 1 aligned, 2 angular, 3 diameter, 4 radius, 5 angular 3-point, 6 ordinate), its definition points, the rendered block's name.</summary>
public sealed record DxfDimension(int Kind, Vec2 DefPoint, Vec2 TextMid, Vec2 P13, Vec2 P14, Vec2 P15, Vec2 P16, double RotationDeg, string? Text, string? Block) : DxfEntity;
public sealed record DxfLeader(List<Vec2> Pts) : DxfEntity;
/// <summary>An entity Lapin does not bring in (counted, so the import can say so).</summary>
public sealed record DxfOther(string Type) : DxfEntity;

/// <summary>What a DXF file holds: its header's units and version, its layers, line types, blocks and model-space entities.</summary>
public sealed class DxfDocument
{
    public string Version { get; init; } = "";
    /// <summary>$INSUNITS: 0 unitless, 1 in, 2 ft, 4 mm, 5 cm, 6 m…</summary>
    public int Units { get; init; }
    public Dictionary<string, DxfLayer> Layers { get; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Line types: name → dash elements (positive dashes, negative gaps, 0 dots), drawing units.</summary>
    public Dictionary<string, double[]> LineTypes { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, DxfBlock> Blocks { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<DxfEntity> Entities { get; } = [];

    /// <summary>Metres per drawing unit from $INSUNITS; null when the file does not say.</summary>
    public double? UnitFactor => DxfUnits.Factor(Units);
}

/// <summary>Drawing units: $INSUNITS codes, names, and the guess when a file says nothing.</summary>
public static class DxfUnits
{
    public static double? Factor(int insunits) => insunits switch
    {
        1 => 0.0254, 2 => 0.3048, 3 => 1609.344, 4 => 0.001, 5 => 0.01, 6 => 1, 7 => 1000, 8 => 0.0000254, 9 => 0.0000000254,
        10 => 0.9144, 11 => 1e-10, 12 => 1e-9, 13 => 1e-6, 14 => 0.1, 15 => 10, 16 => 100, 17 => 1e9, _ => null,
    };

    public static double? Factor(string? name) => name?.Trim().ToLowerInvariant() switch
    {
        "mm" or "millimetre" or "millimeter" => 0.001, "cm" => 0.01, "dm" => 0.1, "m" or "metre" or "meter" => 1,
        "in" or "inch" or "inches" => 0.0254, "ft" or "foot" or "feet" => 0.3048, "yd" => 0.9144,
        _ => null,
    };

    public static string Name(double factor) => factor switch
    {
        0.001 => "mm", 0.01 => "cm", 0.1 => "dm", 1 => "m", 0.0254 => "in", 0.3048 => "ft", 0.9144 => "yd",
        _ => factor.ToString("0.######", CultureInfo.InvariantCulture) + " m/unit",
    };

    /// <summary>The unit a drawing of this extent (its larger side, drawing units) is most likely in: buildings are tens of metres.</summary>
    public static double Guess(double extent) => extent > 2000 ? 0.001 : extent > 300 ? 0.01 : 1;
}

/// <summary>AutoCAD colour indices as CSS colours (the standard 1–255 table), and the nearest index of a colour.</summary>
public static class DxfColors
{
    static readonly (int R, int G, int B)[] Standard = [(0, 0, 0), (255, 0, 0), (255, 255, 0), (0, 255, 0), (0, 255, 255), (0, 0, 255), (255, 0, 255), (24, 24, 27), (128, 128, 128), (192, 192, 192)];
    static readonly double[] Values = [255, 165, 127, 76, 38];
    static readonly int[] Greys = [51, 91, 132, 173, 214, 255];

    public static string Css(int rgb) => $"#{(rgb >> 16) & 255:x2}{(rgb >> 8) & 255:x2}{rgb & 255:x2}";
    public static string Css(int r, int g, int b) => $"#{r:x2}{g:x2}{b:x2}";

    /// <summary>Colour index 1–255 as CSS (7, "foreground", is Lapin's ink; 0 and 256 have no colour of their own: black).</summary>
    public static string Aci(int i)
    {
        if (i is >= 1 and <= 9) return Css(Standard[i].R, Standard[i].G, Standard[i].B);
        if (i is >= 250 and <= 255) { var grey = Greys[i - 250]; return Css(grey, grey, grey); }
        if (i is < 10 or > 249) return "#000000";
        // 10–249: 24 hues 15° apart, each in five values, full and half saturation.
        var hue = (i - 10) / 10 * 15.0;
        var j = (i - 10) % 10;
        var v = Values[j / 2];
        var (r, g, b) = HueRgb(hue);
        if (j % 2 == 1) { r = 0.5 + 0.5 * r; g = 0.5 + 0.5 * g; b = 0.5 + 0.5 * b; }
        return Css((int)Math.Round(v * r), (int)Math.Round(v * g), (int)Math.Round(v * b));
    }

    static (double R, double G, double B) HueRgb(double hue)
    {
        var h = hue / 60;
        var x = 1 - Math.Abs(h % 2 - 1);
        return (int)Math.Floor(h) switch { 0 => (1, x, 0), 1 => (x, 1, 0), 2 => (0, 1, x), 3 => (0, x, 1), 4 => (x, 0, 1), _ => (1, 0, x) };
    }
}

/// <summary>
/// A DXF reader (ASCII, R12 to R2018): the header's units, the layer and
/// line-type tables, the blocks and model space's entities, each in its own
/// record. Objects in paper space, and what Lapin does not draw (points,
/// images, 3D faces, regions…), are counted as <see cref="DxfOther"/>.
/// Coordinates stay in drawing units and in the file's own frame; entities on
/// a mirrored OCS (extrusion 0,0,−1) are brought back to the world's X.
/// </summary>
public static class DxfReader
{
    sealed record Pair(int Code, string Value);
    sealed record Raw(string Type, List<Pair> Pairs);

    public static DxfDocument Read(byte[] bytes) => Read(Decode(bytes));

    public static DxfDocument Read(string text)
    {
        var pairs = Pairs(text);
        var version = ""; var units = 0;
        var doc = new DxfDocument();
        var layers = new List<DxfLayer>(); var ltypes = new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase);
        var blocks = new List<DxfBlock>(); var entities = new List<DxfEntity>();
        // Sections, each read by its own walker.
        for (var i = 0; i < pairs.Count; i++)
        {
            if (pairs[i].Code != 0 || pairs[i].Value != "SECTION" || i + 1 >= pairs.Count) continue;
            var name = pairs[i + 1].Value;
            var end = i + 2;
            while (end < pairs.Count && !(pairs[end].Code == 0 && pairs[end].Value == "ENDSEC")) end++;
            var body = pairs.GetRange(Math.Min(i + 2, pairs.Count), Math.Max(0, end - i - 2));
            switch (name)
            {
                case "HEADER": (version, units) = Header(body); break;
                case "TABLES": Tables(body, layers, ltypes); break;
                case "BLOCKS": blocks.AddRange(Blocks(body)); break;
                case "ENTITIES": entities.AddRange(Entities(Rawn(body))); break;
            }
            i = end;
        }
        var d = new DxfDocument { Version = version, Units = units };
        foreach (var l in layers) d.Layers.TryAdd(l.Name, l);
        foreach (var (k, v) in ltypes) d.LineTypes.TryAdd(k, v);
        foreach (var b in blocks) d.Blocks.TryAdd(b.Name, b);
        d.Entities.AddRange(entities);
        return d;
    }

    // ------------------------------------------------------------ text and pairs

    /// <summary>The bytes as text: UTF-8 when they are valid UTF-8 (R2007 and later), else the header's code page (windows-1252 by default).</summary>
    public static string Decode(byte[] bytes)
    {
        try { return new UTF8Encoding(false, true).GetString(bytes); }
        catch (DecoderFallbackException) { }
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var latin = Encoding.Latin1.GetString(bytes);
        var m = Regex.Match(latin, @"\$DWGCODEPAGE\s*\r?\n\s*3\s*\r?\n\s*([A-Za-z_0-9]+)");
        var page = 1252;
        if (m.Success && Regex.Match(m.Groups[1].Value, @"(\d{3,5})$") is { Success: true } n && int.TryParse(n.Groups[1].Value, out var p)) page = p;
        try { return Encoding.GetEncoding(page).GetString(bytes); }
        catch (Exception) { return Encoding.GetEncoding(1252).GetString(bytes); }
    }

    static List<Pair> Pairs(string text)
    {
        var lines = text.Split('\n');
        var o = new List<Pair>(lines.Length / 2);
        for (var i = 0; i + 1 < lines.Length; i += 2)
        {
            if (!int.TryParse(lines[i].AsSpan().Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var code)) { i--; continue; }
            var v = lines[i + 1].TrimEnd('\r');
            o.Add(new Pair(code, code is 1 or 3 or 1000 ? v : v.Trim()));
        }
        return o;
    }

    static double D(string s) => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0;
    static int I(string s) => int.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : (int)D(s);

    static (string Version, int Units) Header(List<Pair> body)
    {
        string version = ""; var units = 0;
        for (var i = 0; i < body.Count; i++)
        {
            if (body[i].Code != 9) continue;
            if (body[i].Value == "$ACADVER" && i + 1 < body.Count) version = body[i + 1].Value;
            if (body[i].Value == "$INSUNITS" && i + 1 < body.Count && body[i + 1].Code == 70) units = I(body[i + 1].Value);
        }
        return (version, units);
    }

    /// <summary>The entities of a stream: each from its 0 code to the next.</summary>
    static List<Raw> Rawn(List<Pair> body)
    {
        var o = new List<Raw>();
        Raw? cur = null;
        foreach (var p in body)
        {
            if (p.Code == 0) { cur = new Raw(p.Value, []); o.Add(cur); }
            else cur?.Pairs.Add(p);
        }
        return o;
    }

    // ------------------------------------------------------------ tables and blocks

    static void Tables(List<Pair> body, List<DxfLayer> layers, Dictionary<string, double[]> ltypes)
    {
        foreach (var r in Rawn(body))
        {
            switch (r.Type)
            {
                case "LAYER":
                {
                    var name = Str(r, 2) ?? "0";
                    var aci = Int(r, 62) ?? 7;
                    var flags = Int(r, 70) ?? 0;
                    var weight = Int(r, 370) is { } w and >= 0 ? Math.Clamp(w / 100.0, 0.05, 2) : 0.25;
                    var color = Int(r, 420) is { } tc ? DxfColors.Css(tc) : DxfColors.Aci(Math.Abs(aci));
                    layers.Add(new DxfLayer(name, color, weight, Str(r, 6) ?? "Continuous", aci >= 0, (flags & 1) != 0, (flags & 4) != 0));
                    break;
                }
                case "LTYPE":
                {
                    var name = Str(r, 2) ?? "";
                    ltypes[name] = r.Pairs.Where(p => p.Code == 49).Select(p => D(p.Value)).ToArray();
                    break;
                }
            }
        }
    }

    static List<DxfBlock> Blocks(List<Pair> body)
    {
        var o = new List<DxfBlock>();
        var raws = Rawn(body);
        for (var i = 0; i < raws.Count; i++)
        {
            if (raws[i].Type != "BLOCK") continue;
            var head = raws[i];
            var j = i + 1;
            while (j < raws.Count && raws[j].Type != "ENDBLK") j++;
            var name = Str(head, 2) ?? "";
            o.Add(new DxfBlock(name, new Vec2(Num(head, 10) ?? 0, Num(head, 20) ?? 0), Entities(raws.GetRange(i + 1, j - i - 1))));
            i = j;
        }
        return o;
    }

    // ------------------------------------------------------------ entities

    static string? Str(Raw r, int code) => r.Pairs.FirstOrDefault(p => p.Code == code)?.Value;
    static double? Num(Raw r, int code) => r.Pairs.FirstOrDefault(p => p.Code == code) is { } p ? D(p.Value) : null;
    static int? Int(Raw r, int code) => r.Pairs.FirstOrDefault(p => p.Code == code) is { } p ? I(p.Value) : null;
    static Vec2 Pt(Raw r, int xCode) => new(Num(r, xCode) ?? 0, Num(r, xCode + 10) ?? 0);
    static bool Mirrored(Raw r) => Num(r, 230) is { } z && z < -0.5;

    static List<DxfEntity> Entities(List<Raw> raws)
    {
        var o = new List<DxfEntity>();
        for (var i = 0; i < raws.Count; i++)
        {
            var r = raws[i];
            if (Int(r, 67) == 1) { o.Add(With(r, new DxfOther("paper space"))); continue; }
            switch (r.Type)
            {
                case "LINE":
                {
                    var (a, b) = (Pt(r, 10), Pt(r, 11));
                    if (Mirrored(r)) (a, b) = (a with { X = -a.X }, b with { X = -b.X });
                    o.Add(With(r, new DxfLine(a, b) { ZA = Num(r, 30), ZB = Num(r, 31) }));
                    break;
                }
                case "LWPOLYLINE":
                {
                    var verts = new List<(Vec2, double)>();
                    double? x = null, y = null, bulge = 0;
                    void Flush() { if (x is { } px && y is { } py) verts.Add((new Vec2(px, py), bulge ?? 0)); x = y = null; bulge = 0; }
                    foreach (var p in r.Pairs)
                    {
                        if (p.Code == 10) { Flush(); x = D(p.Value); }
                        else if (p.Code == 20) y = D(p.Value);
                        else if (p.Code == 42) bulge = D(p.Value);
                    }
                    Flush();
                    var closed = ((Int(r, 70) ?? 0) & 1) != 0;
                    if (Mirrored(r)) verts = verts.Select(v => (v.Item1 with { X = -v.Item1.X }, -v.Item2)).ToList();
                    if (verts.Count >= 2) o.Add(With(r, new DxfPolyline(verts, closed) { Elevation = Num(r, 38) }));
                    break;
                }
                case "POLYLINE":
                {
                    var flags = Int(r, 70) ?? 0;
                    var verts = new List<(Vec2, double)>();
                    var zs = new List<double>();
                    var j = i + 1;
                    for (; j < raws.Count && raws[j].Type != "SEQEND"; j++)
                        if (raws[j].Type == "VERTEX" && ((Int(raws[j], 70) ?? 0) & (16 | 64)) == 0) { verts.Add((Pt(raws[j], 10), Num(raws[j], 42) ?? 0)); zs.Add(Num(raws[j], 30) ?? 0); }
                    i = j;
                    if ((flags & (16 | 64)) != 0) { o.Add(With(r, new DxfOther("polygon mesh"))); break; }
                    if (Mirrored(r)) verts = verts.Select(v => (v.Item1 with { X = -v.Item1.X }, -v.Item2)).ToList();
                    if (verts.Count >= 2) o.Add(With(r, new DxfPolyline(verts, (flags & 1) != 0) { Elevation = (flags & 8) == 0 ? Num(r, 30) : null, Zs = (flags & 8) != 0 ? zs : null }));
                    break;
                }
                case "CIRCLE":
                {
                    var c = Pt(r, 10);
                    if (Mirrored(r)) c = c with { X = -c.X };
                    if (Num(r, 40) is { } rad && rad > 0) o.Add(With(r, new DxfCircle(c, rad)));
                    break;
                }
                case "ARC":
                {
                    var c = Pt(r, 10);
                    var (s, e) = (Num(r, 50) ?? 0, Num(r, 51) ?? 360);
                    if (Mirrored(r)) { c = c with { X = -c.X }; (s, e) = (180 - e, 180 - s); }
                    if (Num(r, 40) is { } rad && rad > 0) o.Add(With(r, new DxfArc(c, rad, s, e)));
                    break;
                }
                case "ELLIPSE":
                {
                    var c = Pt(r, 10); var major = Pt(r, 11);
                    var (s, e) = (Num(r, 41) ?? 0, Num(r, 42) ?? 2 * Math.PI);
                    if (Mirrored(r)) { c = c with { X = -c.X }; major = major with { X = -major.X }; (s, e) = (Math.PI - e, Math.PI - s); }
                    if (major.Length > 0) o.Add(With(r, new DxfEllipse(c, major, Math.Clamp(Num(r, 40) ?? 1, 1e-6, 1), s, e)));
                    break;
                }
                case "SPLINE":
                {
                    var flags = Int(r, 70) ?? 0;
                    var controls = Points(r, 10); var fits = Points(r, 11);
                    var knots = r.Pairs.Where(p => p.Code == 40).Select(p => D(p.Value)).ToList();
                    var weights = r.Pairs.Where(p => p.Code == 41).Select(p => D(p.Value)).ToList();
                    o.Add(With(r, new DxfSpline(Int(r, 71) ?? 3, knots, controls, weights, fits, (flags & 1) != 0)));
                    break;
                }
                case "TEXT":
                case "ATTRIB":
                case "ATTDEF":
                {
                    if (r.Type == "ATTDEF") { o.Add(With(r, new DxfOther("attribute definition"))); break; }
                    if (r.Type == "ATTRIB" && ((Int(r, 70) ?? 0) & 1) != 0) break; // invisible attribute
                    if (Text(r) is { } t) o.Add(t);
                    break;
                }
                case "MTEXT":
                    o.AddRange(MText(r));
                    break;
                case "HATCH":
                    if (Hatch(r) is { } h) o.Add(h);
                    break;
                case "INSERT":
                {
                    var attribs = new List<DxfText>();
                    if (Int(r, 66) == 1)
                    {
                        var j = i + 1;
                        for (; j < raws.Count && raws[j].Type != "SEQEND"; j++)
                            if (raws[j].Type == "ATTRIB" && ((Int(raws[j], 70) ?? 0) & 1) == 0 && Text(raws[j]) is { } t) attribs.Add(t);
                        i = j;
                    }
                    var at = Pt(r, 10);
                    var (sx, sy, rot) = (Num(r, 41) ?? 1, Num(r, 42) ?? 1, Num(r, 50) ?? 0);
                    if (Mirrored(r)) { at = at with { X = -at.X }; rot = -rot; sx = -sx; }
                    o.Add(With(r, new DxfInsert(Str(r, 2) ?? "", at, sx == 0 ? 1 : sx, sy == 0 ? 1 : sy, rot, Math.Max(1, Int(r, 70) ?? 1), Math.Max(1, Int(r, 71) ?? 1), Num(r, 44) ?? 0, Num(r, 45) ?? 0, attribs) { Z = Num(r, 30) }));
                    break;
                }
                case "SOLID":
                case "TRACE":
                {
                    Vec2[] pts = [Pt(r, 10), Pt(r, 11), Pt(r, 13), Pt(r, 12)];
                    if (Num(r, 32) == null && Num(r, 13) == null) pts = pts[..3];
                    // A triangle repeats a corner.
                    var distinct = new List<Vec2>();
                    foreach (var p in pts) if (distinct.All(q => (q - p).Length > 1e-12)) distinct.Add(p);
                    if (Mirrored(r)) distinct = distinct.Select(p => p with { X = -p.X }).ToList();
                    if (distinct.Count >= 3) o.Add(With(r, new DxfSolid([.. distinct])));
                    break;
                }
                case "DIMENSION":
                {
                    var kind = (Int(r, 70) ?? 0) & 15;
                    o.Add(With(r, new DxfDimension(kind, Pt(r, 10), Pt(r, 11), Pt(r, 13), Pt(r, 14), Pt(r, 15), Pt(r, 16), Num(r, 50) ?? 0, Str(r, 1), Str(r, 2))));
                    break;
                }
                case "LEADER":
                {
                    var pts = Points(r, 10);
                    if (pts.Count >= 2) o.Add(With(r, new DxfLeader(pts)));
                    break;
                }
                case "POINT":
                    o.Add(With(r, new DxfPoint(new Vec3(Num(r, 10) ?? 0, Num(r, 20) ?? 0, Num(r, 30) ?? 0))));
                    break;
                case "VERTEX" or "SEQEND" or "ENDBLK": break;
                default:
                    o.Add(With(r, new DxfOther(r.Type)));
                    break;
            }
        }
        return o;
    }

    static List<Vec2> Points(Raw r, int xCode)
    {
        var o = new List<Vec2>();
        double? x = null;
        foreach (var p in r.Pairs)
        {
            if (p.Code == xCode) x = D(p.Value);
            else if (p.Code == xCode + 10 && x is { } px) { o.Add(new Vec2(px, D(p.Value))); x = null; }
        }
        return o;
    }

    static T With<T>(Raw r, T e) where T : DxfEntity => e with
    {
        Layer = Str(r, 8) is { Length: > 0 } l ? l : "0",
        Aci = Int(r, 62) ?? 256,
        TrueColor = Int(r, 420),
        LineType = Str(r, 6),
    };

    static DxfText? Text(Raw r)
    {
        var value = Plain(Str(r, 1) ?? "");
        var h = Num(r, 40) ?? 0;
        if (value.Length == 0 || h <= 0) return null;
        var (ha, va) = (Int(r, 72) ?? 0, Int(r, 73) ?? 0);
        var at = Pt(r, 10);
        // Aligned to a point other than the insertion: that point is the anchor (aligned and fit texts keep their first point).
        if ((ha is 1 or 2 or 4 || va != 0) && Num(r, 11) is not null) at = Pt(r, 11);
        if (ha == 4) (ha, va) = (1, 2);
        if (ha is 3 or 5) ha = 0;
        var rot = Num(r, 50) ?? 0;
        if (Mirrored(r)) { at = at with { X = -at.X }; rot = 180 - rot; }
        return With(r, new DxfText(at, h, rot, value, Math.Clamp(ha, 0, 2), Math.Clamp(va, 0, 3)) { Z = Num(r, 30) });
    }

    /// <summary>An MTEXT as one text per line, its paragraph anchored by its attachment point; each line below the last.</summary>
    static IEnumerable<DxfText> MText(Raw r)
    {
        var body = string.Concat(r.Pairs.Where(p => p.Code == 3).Select(p => p.Value)) + (Str(r, 1) ?? "");
        var h = Num(r, 40) ?? 0;
        if (h <= 0) yield break;
        var lines = MTextLines(body);
        if (lines.Count == 0) yield break;
        var at = Pt(r, 10);
        var rot = Num(r, 50) ?? (Num(r, 11) is { } dx ? Math.Atan2(Num(r, 21) ?? 0, dx) * 180 / Math.PI : 0);
        var attach = Int(r, 71) ?? 1;
        var spacing = h * 5.0 / 3 * (Num(r, 44) ?? 1);
        var (ha, va) = ((attach - 1) % 3, (attach - 1) / 3); // 0 left/1 centre/2 right; 0 top/1 middle/2 bottom
        if (Mirrored(r)) { at = at with { X = -at.X }; rot = 180 - rot; }
        var up = new Vec2(-Math.Sin(rot * Math.PI / 180), Math.Cos(rot * Math.PI / 180));
        // The first line's top sits at the anchor for a top attachment; the block hangs from it, is centred on it, or stands on it.
        var height = (lines.Count - 1) * spacing + h;
        var top = at + up * (va == 0 ? 0 : va == 1 ? height / 2 : height);
        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i].Length == 0) continue;
            yield return With(r, new DxfText(top - up * (i * spacing + h), h, rot, lines[i], ha, 1));
        }
    }

    static readonly Regex Controls = new(@"\\[ACcFfHhTtQqWwpOoLlKkX][^;\\{}]*;|\\[OoLlKkX]", RegexOptions.Compiled);
    static readonly Regex Stack = new(@"\\S([^;]*?)[\^/#]([^;]*?);", RegexOptions.Compiled);
    static readonly Regex Unicode = new(@"\\U\+([0-9A-Fa-f]{4})", RegexOptions.Compiled);
    static readonly Regex Mif = new(@"\\M\+[0-9A-Fa-f]{5}", RegexOptions.Compiled);

    /// <summary>MTEXT's formatting stripped: paragraphs as lines, fonts, heights, colours, stacking and braces gone, special codes as their characters.</summary>
    public static List<string> MTextLines(string body)
    {
        var s = body.Replace("\\\\", "\u0001").Replace("\\{", "\u0002").Replace("\\}", "\u0003");
        s = s.Replace("\\P", "\n").Replace("\\N", "\n").Replace("\\~", " ");
        s = Stack.Replace(s, m => $"{m.Groups[1].Value}/{m.Groups[2].Value}");
        s = Controls.Replace(s, "");
        s = Mif.Replace(s, "");
        s = s.Replace("{", "").Replace("}", "");
        s = s.Replace('\u0001', '\\').Replace('\u0002', '{').Replace('\u0003', '}');
        return s.Split('\n').Select(l => Plain(l).Trim()).ToList();
    }

    /// <summary>A text's special codes as characters: %%d °, %%p ±, %%c Ø, %%u and %%o dropped, \U+XXXX.</summary>
    public static string Plain(string s)
    {
        s = Unicode.Replace(s, m => ((char)Convert.ToInt32(m.Groups[1].Value, 16)).ToString());
        s = Regex.Replace(s, "%%[dD]", "°").Replace("%%p", "±").Replace("%%P", "±").Replace("%%c", "Ø").Replace("%%C", "Ø");
        s = Regex.Replace(s, "%%[uUoO]", "");
        s = Regex.Replace(s, "%%(\\d{3})", m => ((char)int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)).ToString());
        return s.Replace("%%%", "%").Replace("\r", "").Replace("\n", " ");
    }

    // ------------------------------------------------------------ hatches

    static DxfHatch? Hatch(Raw r)
    {
        var ps = r.Pairs;
        var name = Str(r, 2) ?? "SOLID";
        var solid = (Int(r, 70) ?? 0) == 1;
        var mirrored = Mirrored(r);
        var loops = new List<Vec2[]>();
        var def = new List<DxfPatternLine>();
        var k = ps.FindIndex(p => p.Code == 91);
        if (k < 0) return null;
        var nPaths = I(ps[k].Value);
        k++;
        Vec2 P(ref int i, int xCode)
        {
            double x = 0, y = 0;
            if (i < ps.Count && ps[i].Code == xCode) { x = D(ps[i].Value); i++; }
            if (i < ps.Count && ps[i].Code == xCode + 10) { y = D(ps[i].Value); i++; }
            return new Vec2(x, y);
        }
        double N(ref int i, int code, double dflt = 0) { if (i < ps.Count && ps[i].Code == code) { var v = D(ps[i].Value); i++; return v; } return dflt; }
        for (var path = 0; path < nPaths && k < ps.Count; path++)
        {
            if (ps[k].Code != 92) { k++; path--; continue; }
            var flags = I(ps[k].Value); k++;
            var pts = new List<Vec2>();
            if ((flags & 2) != 0)
            {
                var hasBulge = (int)N(ref k, 72) != 0;
                var closed = (int)N(ref k, 73) != 0;
                var n = (int)N(ref k, 93);
                var verts = new List<(Vec2, double)>();
                for (var v = 0; v < n && k < ps.Count && ps[k].Code == 10; v++)
                {
                    var p = P(ref k, 10);
                    var b = hasBulge ? N(ref k, 42) : 0;
                    verts.Add((p, b));
                }
                pts = DxfCurves.Polyline(verts, true);
                _ = closed;
            }
            else
            {
                var n = (int)N(ref k, 93);
                for (var e = 0; e < n && k < ps.Count; e++)
                {
                    if (ps[k].Code != 72) { k++; e--; continue; }
                    var type = I(ps[k].Value); k++;
                    switch (type)
                    {
                        case 1: { var a = P(ref k, 10); var b = P(ref k, 11); Append(pts, [a, b]); break; }
                        case 2:
                        {
                            var c = P(ref k, 10); var rad = N(ref k, 40); var s = N(ref k, 50); var en = N(ref k, 51, 360); var ccw = (int)N(ref k, 73, 1) != 0;
                            var arc = DxfCurves.Arc(c, rad, s, en);
                            if (!ccw) arc.Reverse();
                            Append(pts, arc);
                            break;
                        }
                        case 3:
                        {
                            var c = P(ref k, 10); var major = P(ref k, 11); var ratio = N(ref k, 40, 1); var s = N(ref k, 50); var en = N(ref k, 51, 360); var ccw = (int)N(ref k, 73, 1) != 0;
                            var arc = DxfCurves.Ellipse(c, major, ratio, s * Math.PI / 180, en * Math.PI / 180);
                            if (!ccw) arc.Reverse();
                            Append(pts, arc);
                            break;
                        }
                        case 4:
                        {
                            var degree = (int)N(ref k, 94, 3); _ = N(ref k, 73); _ = N(ref k, 74);
                            var nk = (int)N(ref k, 95); var nc = (int)N(ref k, 96);
                            var knots = new List<double>(); var ctrl = new List<Vec2>(); var weights = new List<double>();
                            for (var q = 0; q < nk && k < ps.Count && ps[k].Code == 40; q++) knots.Add(N(ref k, 40));
                            for (var q = 0; q < nc && k < ps.Count && ps[k].Code == 10; q++) { ctrl.Add(P(ref k, 10)); if (k < ps.Count && ps[k].Code == 42) weights.Add(N(ref k, 42)); }
                            var nf = (int)N(ref k, 97);
                            var fits = new List<Vec2>();
                            for (var q = 0; q < nf && k < ps.Count && ps[k].Code == 11; q++) fits.Add(P(ref k, 11));
                            // Start and end tangents may follow.
                            if (k < ps.Count && ps[k].Code == 12) P(ref k, 12);
                            if (k < ps.Count && ps[k].Code == 13) P(ref k, 13);
                            Append(pts, DxfCurves.Spline(degree, knots, ctrl, weights, fits));
                            break;
                        }
                    }
                }
            }
            // The source objects of the boundary.
            var ns = (int)N(ref k, 97);
            for (var q = 0; q < ns && k < ps.Count && ps[k].Code == 330; q++) k++;
            if ((flags & 8) != 0) continue; // a text box: no loop
            if (pts.Count >= 2 && (pts[0] - pts[^1]).Length < 1e-9) pts.RemoveAt(pts.Count - 1);
            if (mirrored) pts = pts.Select(p => p with { X = -p.X }).ToList();
            if (pts.Count >= 3) loops.Add([.. pts]);
        }
        // The pattern: style, type, angle, scale, double, then its lines.
        var scale = 1.0; var angle = 0.0;
        for (; k < ps.Count; k++)
        {
            if (ps[k].Code == 52) angle = D(ps[k].Value);
            else if (ps[k].Code == 41) scale = D(ps[k].Value);
            else if (ps[k].Code == 78) { var n = I(ps[k].Value); k++; for (var q = 0; q < n && k < ps.Count; q++) { var a = N(ref k, 53); var bx = N(ref k, 43); var by = N(ref k, 44); var ox = N(ref k, 45); var oy = N(ref k, 46); var nd = (int)N(ref k, 79); var dashes = new List<double>(); for (var d = 0; d < nd; d++) dashes.Add(N(ref k, 49)); def.Add(new DxfPatternLine(a, new Vec2(bx, by), new Vec2(ox, oy), dashes)); } k--; }
            else if (ps[k].Code == 98) break;
        }
        if (mirrored) def = def.Select(l => new DxfPatternLine(180 - l.AngleDeg, l.Base with { X = -l.Base.X }, l.Offset with { X = -l.Offset.X }, l.Dashes)).ToList();
        if (loops.Count == 0) return null;
        return With(r, new DxfHatch(name, solid, scale == 0 ? 1 : scale, mirrored ? -angle : angle, loops, def));
    }

    static void Append(List<Vec2> loop, IReadOnlyList<Vec2> piece)
    {
        if (piece.Count == 0) return;
        var start = loop.Count > 0 && (loop[^1] - piece[0]).Length < 1e-9 ? 1 : 0;
        for (var i = start; i < piece.Count; i++) loop.Add(piece[i]);
    }
}

/// <summary>Curves as points: bulged polylines, arcs, ellipses, splines (drawing units, arcs every 5°).</summary>
public static class DxfCurves
{
    public const double Step = Math.PI / 36;

    /// <summary>A polyline with bulges as points; a closed one ends where it starts (its last vertex's bulge spans back to the first).</summary>
    public static List<Vec2> Polyline(IReadOnlyList<(Vec2 P, double Bulge)> verts, bool closed)
    {
        var o = new List<Vec2>();
        var n = verts.Count;
        if (n == 0) return o;
        var segs = closed ? n : n - 1;
        o.Add(verts[0].P);
        for (var i = 0; i < segs; i++)
        {
            var (a, b) = (verts[i], verts[(i + 1) % n]);
            if (Math.Abs(a.Bulge) < 1e-12 || (b.P - a.P).Length < 1e-12) { o.Add(b.P); continue; }
            var arc = BulgeArc(a.P, b.P, a.Bulge);
            var pts = Arc(arc.C, arc.R, arc.StartDeg, arc.EndDeg);
            if (a.Bulge < 0) pts.Reverse();
            for (var j = 1; j < pts.Count; j++) o.Add(pts[j]);
        }
        return o;
    }

    /// <summary>The arc a bulge describes between two points: its centre, radius, and counter-clockwise angles (°) from the one the arc runs from.</summary>
    public static (Vec2 C, double R, double StartDeg, double EndDeg) BulgeArc(Vec2 a, Vec2 b, double bulge)
    {
        var theta = 4 * Math.Atan(Math.Abs(bulge));
        var chord = (b - a).Length;
        var r = chord / (2 * Math.Sin(theta / 2));
        var mid = (a + b) * 0.5;
        var h = Math.Sqrt(Math.Max(0, r * r - chord * chord / 4)) * (theta > Math.PI ? -1 : 1);
        var n = (b - a).Normalized().Perp;
        var c = mid + n * (bulge > 0 ? h : -h);
        var (sa, ea) = (Math.Atan2(a.Y - c.Y, a.X - c.X) * 180 / Math.PI, Math.Atan2(b.Y - c.Y, b.X - c.X) * 180 / Math.PI);
        return bulge > 0 ? (c, r, sa, ea) : (c, r, ea, sa);
    }

    /// <summary>An arc counter-clockwise from start to end (°) as points.</summary>
    public static List<Vec2> Arc(Vec2 c, double r, double startDeg, double endDeg)
    {
        var s = startDeg * Math.PI / 180; var e = endDeg * Math.PI / 180;
        var sweep = e - s;
        while (sweep <= 1e-12) sweep += 2 * Math.PI;
        while (sweep > 2 * Math.PI + 1e-9) sweep -= 2 * Math.PI;
        var n = Math.Max(2, (int)Math.Ceiling(sweep / Step));
        return Enumerable.Range(0, n + 1).Select(i => c + new Vec2(Math.Cos(s + sweep * i / n), Math.Sin(s + sweep * i / n)) * r).ToList();
    }

    /// <summary>An ellipse arc (parameters in radians) as points.</summary>
    public static List<Vec2> Ellipse(Vec2 c, Vec2 major, double ratio, double start, double end)
    {
        var minor = major.Perp * ratio;
        var sweep = end - start;
        while (sweep <= 1e-12) sweep += 2 * Math.PI;
        while (sweep > 2 * Math.PI + 1e-9) sweep -= 2 * Math.PI;
        var n = Math.Max(4, (int)Math.Ceiling(sweep / Step));
        return Enumerable.Range(0, n + 1).Select(i => { var t = start + sweep * i / n; return c + major * Math.Cos(t) + minor * Math.Sin(t); }).ToList();
    }

    /// <summary>A B-spline (de Boor, rational when weighted) as points, eight per span; through its fit points when it has no control points.</summary>
    public static List<Vec2> Spline(int degree, IReadOnlyList<double> knots, IReadOnlyList<Vec2> controls, IReadOnlyList<double> weights, IReadOnlyList<Vec2> fits)
    {
        if (controls.Count < 2) return fits.ToList();
        var p = Math.Clamp(degree, 1, Math.Max(1, controls.Count - 1));
        var n = controls.Count;
        var k = knots.Count == n + p + 1 ? knots.ToArray() : Enumerable.Range(0, n + p + 1).Select(i => (double)Math.Clamp(i - p, 0, n - p)).ToArray();
        var w = weights.Count == n ? weights.ToArray() : Enumerable.Repeat(1.0, n).ToArray();
        var (t0, t1) = (k[p], k[n]);
        var spans = Math.Max(1, n - p);
        var samples = spans * 8;
        var o = new List<Vec2>(samples + 1);
        for (var i = 0; i <= samples; i++)
        {
            var t = t0 + (t1 - t0) * i / samples;
            o.Add(DeBoor(t, p, k, controls, w));
        }
        return o;
    }

    static Vec2 DeBoor(double t, int p, double[] k, IReadOnlyList<Vec2> c, double[] w)
    {
        var n = c.Count;
        var s = p;
        while (s < n - 1 && t >= k[s + 1]) s++;
        var x = new double[p + 1]; var y = new double[p + 1]; var ww = new double[p + 1];
        for (var j = 0; j <= p; j++) { var idx = s - p + j; x[j] = c[idx].X * w[idx]; y[j] = c[idx].Y * w[idx]; ww[j] = w[idx]; }
        for (var r = 1; r <= p; r++)
            for (var j = p; j >= r; j--)
            {
                var i = s - p + j;
                var den = k[i + p - r + 1] - k[i];
                var a = den <= 0 ? 0 : (t - k[i]) / den;
                x[j] = (1 - a) * x[j - 1] + a * x[j];
                y[j] = (1 - a) * y[j - 1] + a * y[j];
                ww[j] = (1 - a) * ww[j - 1] + a * ww[j];
            }
        return ww[p] == 0 ? new Vec2(x[p], y[p]) : new Vec2(x[p] / ww[p], y[p] / ww[p]);
    }

    /// <summary>A line type's dashes as one of Lapin's four: continuous, dashed, dotted, dash-dot.</summary>
    public static string LineType(string? name, double[]? elements)
    {
        var n = (name ?? "").ToUpperInvariant();
        if (n is "" or "CONTINUOUS" or "BYLAYER" or "BYBLOCK" || elements is not { Length: > 1 }) return "continuous";
        var dashes = elements.Where(e => e > 1e-12).Count();
        var dots = elements.Where(e => Math.Abs(e) <= 1e-12).Count();
        if (dashes > 0 && dots > 0) return "dashdot";
        if (dashes == 0 && dots > 0) return "dotted";
        if (n.Contains("DOT") && n.Contains("DASH")) return "dashdot";
        if (n.Contains("DOT")) return "dotted";
        return "dashed";
    }
}
