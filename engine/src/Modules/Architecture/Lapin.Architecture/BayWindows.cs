using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

/*
 * Bay, bow and box windows (report #60): a window that projects from its wall. It is still one IfcWindow of an
 * IfcWindowType filling one opening in the wall (the opening is the window's width and height, as for any window), so it
 * is placed, moved, copied, flipped, numbered, scheduled and exported as windows are; its type's spec says how it projects
 * (JoinerySpec.Bay). The grid's columns are its faces: a bay or box has three (the first column its left side, the last
 * its right side, the columns between them its centre, which they divide), a bow one face per column, its columns' weights
 * sharing the arc. Each face is a flat window of its own (the type's frame, sashes, glass, operations and materials), so
 * every panel opens as the configurator says. Around them: corner posts, a seat (the board inside), a sill outside, a base
 * (none, knee walls down to the floor, a projecting slab, a slab on corbels), a head (none, a flat roof, a hipped or
 * lean-to roof, a slab) with its flashing against the wall.
 */

/// <summary>The board inside a bay at its sill: its thickness, how far it runs into the room past the wall's interior face, its material.</summary>
public sealed record BaySeat(bool On = true, double Thickness = 0.04, double Overhang = 0.05, string? Material = null);

/// <summary>
/// Under a bay: <see cref="Kind"/> "none" (it hangs on the wall), "floor" (knee walls down to the floor along its outline,
/// <see cref="Thickness"/> thick), "slab" (a projecting slab, its outline the bay's grown by <see cref="Overhang"/>),
/// "corbel" (the slab on <see cref="Brackets"/> brackets against the wall).
/// </summary>
public sealed record BayBase(string Kind = "none", double Thickness = 0.15, double Overhang = 0.05, int Brackets = 2, string? Material = null);

/// <summary>
/// Over a bay: <see cref="Kind"/> "none", "flat" (a flat roof with an upstand), "hipped" (a slope over each face, hips
/// between them), "lean-to" (one slope falling from the wall), "slab" (a slab); <see cref="Thickness"/> at the eaves,
/// <see cref="Overhang"/> past the faces, <see cref="Pitch"/> of the slopes; a flashing against the wall over it.
/// </summary>
public sealed record BayHead(string Kind = "hipped", double Thickness = 0.12, double Overhang = 0.1, double Pitch = 30, string? Material = null, bool Flashing = true, string? FlashingMaterial = null);

/// <summary>The sill outside, under the faces: how far it runs past them, its thickness, its material.</summary>
public sealed record BaySill(bool On = true, double Projection = 0.04, double Thickness = 0.03, string? Material = null);

/// <summary>
/// How a window projects from its wall (report #60): <see cref="Kind"/> "bay" (a centre and two sides at <see cref="Angle"/>
/// to the wall), "bow" (equal panels on an arc), "box" (a rectangular projection, its sides square to the wall, glazed or
/// solid by <see cref="Sides"/>); <see cref="Projection"/> from the wall's exterior face to the frames' outer face.
/// </summary>
public sealed record JoineryBay(string Kind = "bay", double Projection = 0.6, double Angle = 45, string Sides = "glass", string? SideMaterial = null,
    BaySeat? Seat = null, BayBase? Base = null, BayHead? Head = null, BaySill? Sill = null,
    /// <summary>The bay style last applied (Lapin's or the project's id; report #111), shown in the configurator's select; null: none.</summary>
    string? Style = null);

/// <summary>
/// One piece of a bay's body in the type's frame (X along the wall, Y to the exterior, Z up from the opening's bottom):
/// a joinery box on face <see cref="Face"/>; or a footprint raised from <see cref="Z0"/> to <see cref="Z1"/>; or a profile
/// swept by <see cref="Sweep"/>; or a convex body's faces. <see cref="Material"/> null: the type's material for its role.
/// </summary>
public sealed record BayPiece(string Role, string Name, string Key, string? Material,
    JoineryBox? Box = null, int Face = -1,
    IReadOnlyList<Vec2>? Footprint = null, double Z0 = 0, double Z1 = 0,
    IReadOnlyList<Vec3>? Profile = null, Vec3 Sweep = default,
    IReadOnlyList<IReadOnlyList<Vec3>>? Faces = null);

/// <summary>A face of a bay: its own flat window, its middle on the frames' line, its direction along and its outward normal, its length, whether it is solid.</summary>
public sealed record BayFace(JoinerySpec Spec, Vec2 Mid, Vec2 U, Vec2 N, double Length, bool Solid)
{
    public Vec2 At(double x, double y) => Mid + U * x + N * y;
}

public static class BayWindows
{
    public static readonly string[] Kinds = ["bay", "bow", "box"];
    public static readonly string[] BaseKinds = ["none", "floor", "slab", "corbel"];
    public static readonly string[] HeadKinds = ["none", "flat", "hipped", "lean-to", "slab"];

    /// <summary>Whether a type is a projecting window (a door never is).</summary>
    public static bool Projecting(JoinerySpec s) => !s.IsDoor && s.Bay is { } b && Kinds.Contains(b.Kind);

    /// <summary>The projection of the frames' centre line from the wall's face.</summary>
    static double Pc(JoinerySpec s) => Math.Max(0.02, s.Bay!.Projection - s.Frame.Depth / 2);

    static double Half(JoinerySpec s) => s.Width / 2 - s.Frame.Depth / 2;

    /// <summary>The frames' centre line, from the left end on the wall's face (<paramref name="yExt"/>) out and round to the right end.</summary>
    public static List<Vec2> Line(JoinerySpec s, double yExt)
    {
        var b = s.Bay!;
        var half = Half(s);
        var pc = Pc(s);
        if (b.Kind == "bow")
        {
            pc = Math.Min(pc, half - 0.01);
            // The arc's rise found so that the outermost face (a chord of it) stands out the projection.
            var (lo, hi) = (pc, Math.Min(half - 0.005, pc * 2));
            for (var k = 0; k < 40; k++)
            {
                var mid = (lo + hi) / 2;
                var pts = Arc(s, half, mid, yExt);
                var top = Math.Max(pts.Max(p => p.Y), Enumerable.Range(1, pts.Count - 1).Max(i => (pts[i].Y + pts[i - 1].Y) / 2));
                if (top - yExt < pc) lo = mid; else hi = mid;
            }
            return Arc(s, half, (lo + hi) / 2, yExt);
        }
        var angle = b.Kind == "box" ? 90 : Math.Clamp(b.Angle, 1, 90);
        var run = angle >= 89.999 ? 0 : pc / Math.Tan(angle * Math.PI / 180);
        return [new(-half, yExt), new(-half + run, yExt + pc), new(half - run, yExt + pc), new(half, yExt)];
    }

    /// <summary>A bow's points on an arc of rise <paramref name="rise"/> over its chord, the columns' weights sharing it.</summary>
    static List<Vec2> Arc(JoinerySpec s, double half, double rise, double yExt)
    {
        {
            var pc = rise;
            var r = (half * half + pc * pc) / (2 * pc);
            var cy = yExt + pc - r;
            var (pl, pr) = (Math.Atan2(yExt - cy, -half), Math.Atan2(yExt - cy, half));
            var w = s.Columns.Select(x => Math.Max(x, 1e-6)).ToArray();
            var total = w.Sum();
            var o = new List<Vec2> { new(-half, yExt) };
            var at = 0.0;
            for (var i = 0; i < w.Length; i++)
            {
                at += w[i];
                var phi = pl - (pl - pr) * at / total;
                o.Add(i == w.Length - 1 ? new Vec2(half, yExt) : new Vec2(r * Math.Cos(phi), cy + r * Math.Sin(phi)));
            }
            return o;
        }
    }

    /// <summary>The columns each face takes: a bay's or box's left side the first, its centre those between, its right side the last; a bow's one each.</summary>
    static (int C0, int C1)[] Ranges(JoinerySpec s)
    {
        var n = s.Columns.Length;
        return s.Bay!.Kind == "bow" ? [.. Enumerable.Range(0, n).Select(i => (i, i))] : [(0, 0), (1, n - 2), (n - 1, n - 1)];
    }

    /// <summary>A face's own flat window: the columns it takes and their panels, its width the face's length.</summary>
    static JoinerySpec Sub(JoinerySpec s, int c0, int c1, double width) => s with
    {
        Width = width,
        Columns = s.Columns[c0..(c1 + 1)],
        // The face's own column boundaries' mullions (decision 450).
        Mullions = s.Mullions == null ? null : [.. Enumerable.Range(c0, c1 - c0).Select(i => i < s.Mullions.Length ? s.Mullions[i] : null)],
        Panels = [.. s.Panels.Where(p => p.Col >= c0 && p.Col <= c1).Select(p => p with { Col = p.Col - c0, ColSpan = Math.Max(1, Math.Min(p.ColSpan, c1 - p.Col + 1)) })],
        Bay = null, Details = null, Shape = null, Lintel = null,
        Board = new JoineryBoard(0, 0),
        Frame = s.Frame with { Offset = 0 },
    };

    /// <summary>The faces, left to right, with the wall's exterior face at <paramref name="yExt"/>.</summary>
    public static List<BayFace> Faces(JoinerySpec s, double yExt)
    {
        var line = Line(s, yExt);
        var ranges = Ranges(s);
        var solidSides = s.Bay!.Kind != "bow" && s.Bay.Sides == "solid";
        var o = new List<BayFace>();
        for (var i = 0; i + 1 < line.Count && i < ranges.Length; i++)
        {
            var (a, b) = (line[i], line[i + 1]);
            var len = (b - a).Length;
            var u = len > 1e-9 ? (b - a) * (1 / len) : new Vec2(1, 0);
            var (c0, c1) = ranges[i];
            o.Add(new BayFace(Sub(s, c0, c1, len), (a + b) * 0.5, u, u.Perp, len, solidSides && (i == 0 || i == ranges.Length - 1)));
        }
        return o;
    }

    /// <summary>
    /// The window unfolded: its glazed faces side by side, each column as wide as it is on its face (the schedule's card,
    /// IFC's panels and lining, the glazed area). Solid sides are left out.
    /// </summary>
    public static JoinerySpec Developed(JoinerySpec s)
    {
        var faces = Faces(s, 0);
        var ranges = Ranges(s);
        var cols = new List<double>();
        var panels = new List<JoineryPanel>();
        // Each face's own boundaries' mullions; where two faces join, the frame's.
        var mullions = new List<double?>();
        for (var i = 0; i < faces.Count; i++)
        {
            if (faces[i].Solid) continue;
            var (c0, c1) = ranges[i];
            var w = s.Columns[c0..(c1 + 1)];
            var sum = Math.Max(1e-9, w.Sum());
            var at = cols.Count;
            if (at > 0) mullions.Add(null);
            for (var c = c0; c < c1; c++) mullions.Add(s.Mullions is { } ml && c < ml.Length ? ml[c] : null);
            foreach (var x in w) cols.Add(Math.Round(faces[i].Length * x / sum, 6));
            panels.AddRange(s.Panels.Where(p => p.Col >= c0 && p.Col <= c1).Select(p => p with { Col = p.Col - c0 + at, ColSpan = Math.Max(1, Math.Min(p.ColSpan, c1 - p.Col + 1)) }));
        }
        if (cols.Count == 0) return s with { Bay = null };
        return s with { Width = Math.Round(cols.Sum(), 6), Columns = [.. cols], Panels = [.. panels], Bay = null, Mullions = s.Mullions == null ? null : [.. mullions] };
    }

    static string Mm(double v) => Math.Round(v * 1000).ToString(CultureInfo.InvariantCulture);

    /// <summary>A bay in a few words, for the schedules and IFC: "Bay 45°, projection 600, 3 faces".</summary>
    public static string Describe(JoinerySpec s)
    {
        var b = s.Bay!;
        var faces = Faces(s, 0);
        return b.Kind switch
        {
            "bow" => $"Bow, projection {Mm(b.Projection)}, {faces.Count} faces",
            "box" => $"Box, projection {Mm(b.Projection)}, {(b.Sides == "solid" ? "solid sides" : "glazed sides")}",
            _ => $"Bay {b.Angle.ToString("0.#", CultureInfo.InvariantCulture)}°, projection {Mm(b.Projection)}, {faces.Count} faces",
        };
    }

    /// <summary>Why a projecting window cannot be built (the first problem), or null.</summary>
    public static string? Problem(JoinerySpec s)
    {
        var b = s.Bay!;
        if (!Kinds.Contains(b.Kind)) return "A projecting window is a bay, a bow or a box.";
        if (!(s.Width > 0.6) || !(s.Height > 0.3)) return "A projecting window is 600 mm wide and 300 mm high at least.";
        if (!(s.Frame.Depth > 0) || !(s.Frame.Width > 0)) return "The frame needs a positive depth and width.";
        if (JoineryShapes.Shaped(s)) return "A projecting window is rectangular: its faces carry the shape.";
        if (s.Columns.Length < 3 || s.Columns.Length > 12) return "A bay, bow or box window needs 3 to 12 columns: its faces (a bay's and box's first and last columns are its sides).";
        if (s.Rows.Length is < 1 or > 12 || !s.Columns.Concat(s.Rows).All(v => double.IsFinite(v) && v > 0)) return "Column widths and row heights must be positive.";
        if (!(b.Projection >= s.Frame.Depth + 0.05) || b.Projection > 3) return "The projection runs from the frame's depth (and 50 mm) to 3 m.";
        if (b.Kind == "bay" && (b.Angle < 15 || b.Angle > 90)) return "A bay's sides stand at 15° to 90° to the wall.";
        if (b.Kind == "bow" && Pc(s) > Half(s) - 0.01) return "A bow projects at most half its width (a half circle).";
        if (b.Head is { Kind: "hipped" or "lean-to" } h && (h.Pitch < 5 || h.Pitch > 60)) return "The roof over a bay is pitched at 5° to 60°.";
        if (b.Head is { } hk && !HeadKinds.Contains(hk.Kind)) return $"The head over a bay is one of {string.Join(", ", HeadKinds)}.";
        if (b.Base is { } bk && !BaseKinds.Contains(bk.Kind)) return $"The base of a bay is one of {string.Join(", ", BaseKinds)}.";
        var faces = Faces(s, 0);
        for (var i = 0; i < faces.Count; i++)
        {
            if (faces[i].Length < 0.25) return "A face of the bay is too narrow: widen the window, or lower its projection or its sides' angle.";
            if (faces[i].Solid) continue;
            if (Joinery.Problem(faces[i].Spec) is { } p) return $"Face {i + 1}: {p}";
        }
        return null;
    }

    // ------------------------------------------------------------ the body

    /// <summary>The frames' line as a counter-clockwise outline, closed along the wall's face (its last edge).</summary>
    static List<Vec2> Ccw(JoinerySpec s, double yExt) => [.. Line(s, yExt).AsEnumerable().Reverse()];

    /// <summary>The outline grown past the frames' line by <paramref name="front"/> on every face, the wall's edge kept on the wall.</summary>
    static List<Vec2> Grown(List<Vec2> ccw, double front)
    {
        var d = Enumerable.Repeat(front, ccw.Count - 1).Append(0.0).ToArray();
        return [.. Polygon2.Offset(ccw, d)];
    }

    /// <summary>The band between two grown outlines, open to the wall (a sill, knee walls, an upstand).</summary>
    static List<Vec2> Band(List<Vec2> ccw, double outer, double inner) =>
        [.. Grown(ccw, outer), .. Grown(ccw, inner).AsEnumerable().Reverse()];

    /// <summary>
    /// The pieces of a projecting window in the type's frame, for a wall <paramref name="t"/> thick (its faces at ±t/2)
    /// whose storey's floor is <paramref name="zFloor"/> under the opening's bottom.
    /// </summary>
    public static List<BayPiece> Pieces(JoinerySpec s, double t, double zFloor)
    {
        var b = s.Bay!;
        var o = new List<BayPiece>();
        double yE = t / 2, yI = -t / 2, d = s.Frame.Depth, h = s.Height;
        var faces = Faces(s, yE);
        var line = Line(s, yE);
        var ccw = Ccw(s, yE);

        for (var i = 0; i < faces.Count; i++)
        {
            var fc = faces[i];
            if (fc.Solid)
                o.Add(new BayPiece("side", "Side panels", "bay:side", b.SideMaterial ?? s.Materials?.Frame,
                    Box: new JoineryBox("side", new Vec3(-fc.Length / 2, -d / 2, 0), new Vec3(fc.Length / 2, d / 2, h)), Face: i));
            else
                foreach (var box in Joinery.Boxes(fc.Spec)) o.Add(new BayPiece(box.Role, box.Role, box.Role, null, Box: box, Face: i));
        }
        // The corner posts: the wedge between two faces' frames on the outside.
        for (var k = 1; k + 1 < line.Count && k < faces.Count; k++)
        {
            var (na, nb) = (faces[k - 1].N, faces[k].N);
            var dot = Vec2.Dot(na, nb);
            if (dot > 0.9999) continue;
            var v = line[k];
            var mitre = v + (na + nb) * (d / 2 / (1 + dot));
            o.Add(new BayPiece("frame", "frame", "frame", null, Footprint: [v, v + na * (d / 2), mitre, v + nb * (d / 2)], Z0: 0, Z1: h));
        }

        var seat = b.Seat is { On: true } st ? st : null;
        var seatT = seat != null ? Math.Clamp(seat.Thickness, 0.005, 0.2) : 0;
        if (seat != null)
        {
            var inner = Grown(ccw, -d / 2);
            var yi = yI - Math.Clamp(seat.Overhang, 0, 0.5);
            List<Vec2> fp = [new(s.Width / 2, yi), new(s.Width / 2, yE), .. inner, new(-s.Width / 2, yE), new(-s.Width / 2, yi)];
            o.Add(new BayPiece("seat", "Seat", "bay:seat", seat.Material ?? s.Materials?.Board, Footprint: fp, Z0: -seatT, Z1: 0));
        }
        var sill = b.Sill is { On: true } sl ? sl : null;
        var sillT = sill != null ? Math.Clamp(sill.Thickness, 0.005, 0.2) : 0;
        if (sill != null)
            o.Add(new BayPiece("sill", "Sill (outside)", "bay:sill", sill.Material, Footprint: Band(ccw, d / 2 + Math.Clamp(sill.Projection, 0, 0.5), -d / 2), Z0: -sillT, Z1: 0));

        // The base: its top under the seat and the sill.
        var under = -Math.Max(seatT, sillT);
        var bs = b.Base ?? new BayBase("none");
        var bt = Math.Clamp(bs.Thickness, 0.03, 1.5);
        var bov = Math.Clamp(bs.Overhang, 0, 1);
        switch (bs.Kind)
        {
            case "slab":
            case "corbel":
            {
                var fp = Grown(ccw, d / 2 + bov);
                o.Add(new BayPiece("base", "Base", "bay:base", bs.Material, Footprint: fp, Z0: under - bt, Z1: under));
                if (bs.Kind == "corbel")
                {
                    var n = Math.Clamp(bs.Brackets, 1, 8);
                    var (x0, x1) = (-s.Width / 2 + 0.2, s.Width / 2 - 0.2);
                    for (var i = 0; i < n; i++)
                    {
                        var x = n == 1 ? 0 : x0 + (x1 - x0) * i / (n - 1);
                        var reach = Spans(fp, x).Select(sp => sp.B).DefaultIfEmpty(yE).Max() - yE - 0.05;
                        if (reach < 0.1) continue;
                        var drop = Math.Clamp(reach * 0.9, 0.2, 0.6);
                        var top = under - bt;
                        o.Add(new BayPiece("base", "Corbels", "bay:corbels", bs.Material,
                            Profile: [new Vec3(x - 0.06, yE, top), new Vec3(x - 0.06, yE + reach, top), new Vec3(x - 0.06, yE, top - drop)], Sweep: new Vec3(0.12, 0, 0)));
                    }
                }
                break;
            }
            case "floor":
            {
                var outer = d / 2 + bov;
                if (under - zFloor > 0.05)
                    o.Add(new BayPiece("base", "Base walls", "bay:base", bs.Material, Footprint: Band(ccw, outer, outer - bt), Z0: zFloor, Z1: under));
                break;
            }
        }

        // The head and its flashing against the wall.
        var hd = b.Head ?? new BayHead("none");
        if (hd.Kind != "none")
        {
            var ht = Math.Clamp(hd.Thickness, 0.02, 1);
            var hov = Math.Clamp(hd.Overhang, 0, 1.5);
            var fp = Grown(ccw, d / 2 + hov);
            var zE = h + ht;
            var tan = Math.Tan(Math.Clamp(hd.Pitch, 5, 60) * Math.PI / 180);
            List<(Vec3 N, double D)> planes = [];
            if (hd.Kind == "hipped")
                for (var i = 0; i + 1 < fp.Count; i++)
                {
                    var (a, c) = (fp[i], fp[i + 1]);
                    if ((c - a).Length < 1e-6) continue;
                    var dir = (c - a).Normalized();
                    var outN = new Vec2(dir.Y, -dir.X);
                    planes.Add(Plane(new Vec3(tan * outN.X, tan * outN.Y, 1), zE + tan * Vec2.Dot(a, outN)));
                }
            else if (hd.Kind == "lean-to")
                planes.Add(Plane(new Vec3(0, tan, 1), zE + tan * fp.Max(p => p.Y)));
            IReadOnlyList<(double X, double Z)> wallLine;
            if (planes.Count > 0)
            {
                var span = Math.Max(fp.Max(p => p.X) - fp.Min(p => p.X), fp.Max(p => p.Y) - fp.Min(p => p.Y));
                var body = ConvexBody(fp, h, zE + tan * span + 0.05, planes);
                if (body == null) return o;
                o.Add(new BayPiece("head", "Roof", "bay:head", hd.Material, Faces: body));
                wallLine = body.SelectMany(f => f).Where(p => Math.Abs(p.Y - yE) < 1e-6).GroupBy(p => Math.Round(p.X, 6))
                    .Select(g => (g.Key, g.Max(p => p.Z))).OrderBy(p => p.Key).ToList();
            }
            else
            {
                o.Add(new BayPiece("head", hd.Kind == "flat" ? "Roof" : "Head slab", "bay:head", hd.Material, Footprint: fp, Z0: h, Z1: zE));
                var top = zE;
                if (hd.Kind == "flat")
                {
                    o.Add(new BayPiece("head", "Roof", "bay:head", hd.Material, Footprint: Band(ccw, d / 2 + hov, d / 2 + hov - 0.06), Z0: zE, Z1: zE + 0.06));
                    top = zE + 0.06;
                }
                var onWall = fp.Where(p => Math.Abs(p.Y - yE) < 1e-6).Select(p => p.X).ToList();
                wallLine = onWall.Count >= 2 ? [(onWall.Min(), top), (onWall.Max(), top)] : [];
            }
            if (hd.Flashing && wallLine.Count >= 2)
            {
                List<Vec3> profile = [.. wallLine.Select(p => new Vec3(p.X, yE, p.Z - 0.03)), .. wallLine.Reverse().Select(p => new Vec3(p.X, yE, p.Z + 0.15))];
                o.Add(new BayPiece("flashing", "Flashing", "bay:flashing", hd.FlashingMaterial, Profile: profile, Sweep: new Vec3(0, 0.012, 0)));
            }
        }
        return o;
    }

    static (Vec3 N, double D) Plane(Vec3 n, double d) { var l = n.Length; return (n * (1 / l), d / l); }

    /// <summary>Where a vertical line at <paramref name="x"/> crosses an outline: its spans in Y.</summary>
    internal static List<(double A, double B)> Spans(IReadOnlyList<Vec2> poly, double x)
    {
        var ys = new List<double>();
        for (var i = 0; i < poly.Count; i++)
        {
            var (a, c) = (poly[i], poly[(i + 1) % poly.Count]);
            if ((a.X <= x && c.X > x) || (c.X <= x && a.X > x)) ys.Add(a.Y + (c.Y - a.Y) * (x - a.X) / (c.X - a.X));
        }
        ys.Sort();
        var o = new List<(double, double)>();
        for (var i = 0; i + 1 < ys.Count; i += 2) o.Add((ys[i], ys[i + 1]));
        return o;
    }

    // ------------------------------------------------------------ convex bodies

    /// <summary>
    /// A convex body: the prism of a convex counter-clockwise outline from <paramref name="z0"/> to <paramref name="z1"/>,
    /// cut by each plane (what lies where n·p ≤ d is kept); its faces counter-clockwise seen from outside. Null when nothing is left.
    /// </summary>
    internal static List<IReadOnlyList<Vec3>>? ConvexBody(IReadOnlyList<Vec2> ccw, double z0, double z1, IEnumerable<(Vec3 N, double D)> planes)
    {
        var pts = Polygon2.EnsureCcw(ccw).ToList();
        var faces = new List<List<Vec3>>
        {
            pts.AsEnumerable().Reverse().Select(p => p.At(z0)).ToList(),
            pts.Select(p => p.At(z1)).ToList(),
        };
        for (var i = 0; i < pts.Count; i++)
        {
            var (a, b) = (pts[i], pts[(i + 1) % pts.Count]);
            if ((b - a).Length < 1e-9) continue;
            faces.Add([a.At(z0), b.At(z0), b.At(z1), a.At(z1)]);
        }
        foreach (var (n, d) in planes)
        {
            faces = Clip(faces, n, d);
            if (faces.Count < 4) return null;
        }
        return [.. faces];
    }

    static List<List<Vec3>> Clip(List<List<Vec3>> faces, Vec3 n, double d)
    {
        const double eps = 1e-9;
        var o = new List<List<Vec3>>();
        var cut = new List<Vec3>();
        foreach (var f in faces)
        {
            var r = new List<Vec3>();
            for (var i = 0; i < f.Count; i++)
            {
                var (a, b) = (f[i], f[(i + 1) % f.Count]);
                var (da, db) = (Vec3.Dot(n, a) - d, Vec3.Dot(n, b) - d);
                if (da <= eps) r.Add(a);
                if (Math.Abs(da) <= eps) cut.Add(a);
                if ((da < -eps && db > eps) || (da > eps && db < -eps))
                {
                    var p = a + (b - a) * (da / (da - db));
                    r.Add(p);
                    cut.Add(p);
                }
            }
            var clean = Dedupe(r);
            if (clean.Count >= 3 && VecMath.NewellNormal(clean).Length > 1e-10) o.Add(clean);
        }
        var cap = Dedupe(cut.Distinct().ToList(), closed: false);
        // Points met twice (shared edges) once.
        var unique = new List<Vec3>();
        foreach (var p in cap) if (!unique.Any(q => Vec3.DistanceSquared(p, q) < 1e-14)) unique.Add(p);
        if (unique.Count >= 3)
        {
            var c = new Vec3(unique.Average(p => p.X), unique.Average(p => p.Y), unique.Average(p => p.Z));
            var u = Math.Abs(n.Z) < 0.9 ? Vec3.Cross(n, Vec3.UnitZ).Normalized() : Vec3.Cross(n, new Vec3(1, 0, 0)).Normalized();
            var v = Vec3.Cross(n, u);
            var sorted = unique.OrderBy(p => Math.Atan2(Vec3.Dot(p - c, v), Vec3.Dot(p - c, u))).ToList();
            if (VecMath.NewellNormal(sorted) is var nn && Vec3.Dot(nn, n) < 0) sorted.Reverse();
            if (VecMath.NewellNormal(sorted).Length > 1e-10) o.Add(sorted);
        }
        return o;
    }

    static List<Vec3> Dedupe(List<Vec3> loop, bool closed = true)
    {
        var o = new List<Vec3>();
        foreach (var p in loop) if (o.Count == 0 || Vec3.DistanceSquared(o[^1], p) > 1e-14) o.Add(p);
        if (closed && o.Count > 1 && Vec3.DistanceSquared(o[0], o[^1]) < 1e-14) o.RemoveAt(o.Count - 1);
        return o;
    }

    // ------------------------------------------------------------ into the wall

    /// <summary>A face's frame mapped into the type's: X along it, Y out of it.</summary>
    static Func<double, double, double, Vec3> OnFace(BayFace f, Func<double, double, double, Vec3> map) =>
        (x, y, z) => { var p = f.At(x, y); return map(p.X, p.Y, z); };

    /// <summary>A piece as a solid, through <paramref name="map"/> (the type's frame into the world).</summary>
    public static Solid? Make(BayPiece pc, IReadOnlyList<BayFace> faces, Func<double, double, double, Vec3> map)
    {
        if (pc.Box is { } box && pc.Face >= 0 && pc.Face < faces.Count) return Openings.Box(box, OnFace(faces[pc.Face], map));
        if (pc.Footprint is { Count: >= 3 } fp && pc.Z1 - pc.Z0 > 1e-6)
        {
            var world = fp.Select(q => map(q.X, q.Y, pc.Z0).Xy).ToList();
            var ccw = Polygon2.EnsureCcw(world);
            var z0 = map(0, 0, pc.Z0).Z;
            return Prism.ExtrudeFootprint(ccw, z0, map(0, 0, pc.Z1).Z - z0, pc.Role);
        }
        if (pc.Profile is { Count: >= 3 } profile)
            return Prism.Extrude(profile.Select(q => map(q.X, q.Y, q.Z)).ToList(), null, map(pc.Sweep.X, pc.Sweep.Y, pc.Sweep.Z) - map(0, 0, 0), pc.Role);
        if (pc.Faces is { Count: >= 4 } body)
        {
            var o = map(0, 0, 0);
            var mirrored = Vec2.Cross((map(1, 0, 0) - o).Xy, (map(0, 1, 0) - o).Xy) < 0;
            var verts = new List<Vec3>();
            var inputs = new List<FaceInput>();
            foreach (var f in body)
            {
                var loop = f.Select(p => { verts.Add(map(p.X, p.Y, p.Z)); return verts.Count - 1; }).ToArray();
                if (mirrored) Array.Reverse(loop);
                inputs.Add(new FaceInput(loop));
            }
            try { return SolidBuilder.Make(verts, inputs, pc.Role); } catch (Exception) { return null; }
        }
        return null;
    }

    /// <summary>A piece's corners in the type's frame.</summary>
    static IEnumerable<Vec3> Corners(BayPiece pc, IReadOnlyList<BayFace> faces)
    {
        if (pc.Box is { } b && pc.Face >= 0 && pc.Face < faces.Count)
        {
            var f = faces[pc.Face];
            foreach (var x in new[] { b.Min.X, b.Max.X })
                foreach (var y in new[] { b.Min.Y, b.Max.Y })
                    foreach (var z in new[] { b.Min.Z, b.Max.Z })
                    { var p = f.At(x, y); yield return new Vec3(p.X, p.Y, z); }
            yield break;
        }
        if (pc.Footprint is { } fp) foreach (var p in fp) { yield return p.At(pc.Z0); yield return p.At(pc.Z1); }
        if (pc.Profile is { } pr) foreach (var p in pr) { yield return p; yield return p + pc.Sweep; }
        if (pc.Faces is { } body) foreach (var f in body) foreach (var p in f) yield return p;
    }

    // ------------------------------------------------------------ drawings

    /// <summary>
    /// The plan in the type's frame, cut <paramref name="cutZ"/> over the opening's bottom: each face's own plan (the
    /// symbol engine's: frames cut, glass thin), the corner posts and solid sides cut, the seat, sill and base seen below,
    /// the roof over it dashed. Kinds as plan items have them: heavy, symbol, dashed, overhead.
    /// </summary>
    public static List<(string Kind, bool Closed, List<Vec2> Pts)> Plan(JoinerySpec s, double t, double zFloor, double cutZ)
    {
        var o = new List<(string, bool, List<Vec2>)>();
        var faces = Faces(s, t / 2);
        foreach (var f in faces)
        {
            if (f.Solid) continue;
            foreach (var st in JoinerySymbols.Plan(f.Spec, cutZ))
                o.Add((st.Weight == "cut" ? "heavy" : st.Dashed ? "dashed" : "symbol", st.Closed, st.Pts.Select(q => f.At(q.X, q.Y)).ToList()));
        }
        foreach (var pc in Pieces(s, t, zFloor))
        {
            if (pc.Box != null && pc.Role != "side") continue;
            if (pc.Key is "bay:flashing" or "bay:corbels") continue;
            var corners = Corners(pc, faces).ToList();
            if (corners.Count == 0) continue;
            var (z0, z1) = (corners.Min(p => p.Z), corners.Max(p => p.Z));
            var kind = z0 <= cutZ && z1 >= cutZ ? "heavy" : z1 < cutZ ? "symbol" : "overhead";
            var outline = pc.Footprint is { } fp ? fp.ToList() : Polygon2.ConvexHull(corners.Select(p => p.Xy)).ToList();
            o.Add((kind, true, outline));
        }
        return o;
    }

    /// <summary>The plan as the configurator draws it, with the wall's faces beside the opening.</summary>
    public static List<JStroke> PlanStrokes(JoinerySpec s, double t)
    {
        var o = Plan(s, t, -s.SillHeight, Openings.PlanCut - s.SillHeight)
            .Select(x => new JStroke(x.Pts, x.Kind == "heavy" ? "cut" : x.Kind == "symbol" ? "outline" : "thin", x.Closed, x.Kind is "overhead" or "dashed")).ToList();
        var reach = s.Width / 2 + Math.Max(0.3, s.Bay!.Projection);
        foreach (var y in new[] { -t / 2, t / 2 })
        {
            o.Add(new JStroke([new(-reach, y), new(-s.Width / 2, y)], "cut"));
            o.Add(new JStroke([new(s.Width / 2, y), new(reach, y)], "cut"));
        }
        return o;
    }

    /// <summary>
    /// The elevation seen from inside (or from outside, mirrored as a flat window's is): each face's own elevation
    /// (frames, sashes and how they open) laid on the wall's plane as it is seen, foreshortened; the other pieces' outlines.
    /// </summary>
    public static List<JStroke> Elevation(JoinerySpec s, double t, bool exterior, string convention = "handle", bool hardware = false)
    {
        var o = new List<JStroke>();
        var faces = Faces(s, t / 2);
        foreach (var f in faces)
        {
            if (f.Solid) { o.Add(Rect(f.At(-f.Length / 2, 0).X, 0, f.At(f.Length / 2, 0).X, s.Height, "outline")); continue; }
            foreach (var st in JoinerySymbols.Elevation(f.Spec, exterior, convention, hardware))
                o.Add(st with { Pts = st.Pts.Select(q => new Vec2(f.Mid.X + f.U.X * (exterior ? -q.X : q.X), q.Y)).ToList() });
        }
        foreach (var pc in Pieces(s, t, -s.SillHeight).Where(p => p.Box == null))
        {
            var pts = Corners(pc, faces).Select(p => new Vec2(p.X, p.Z)).ToList();
            if (pts.Count >= 3) o.Add(new JStroke(Polygon2.ConvexHull(pts).ToList(), "outline", true));
        }
        return exterior ? o.Select(x => x with { Pts = x.Pts.Select(q => new Vec2(-q.X, q.Y)).ToList() }).ToList() : o;
    }

    /// <summary>The section through the middle (X = 0), Y across and Z up: what it crosses cut.</summary>
    public static List<JStroke> Section(JoinerySpec s, double t)
    {
        var o = new List<JStroke>();
        var faces = Faces(s, t / 2);
        foreach (var f in faces)
        {
            if (f.Solid || Math.Abs(f.U.X) < 1e-6) continue;
            var xl = -f.Mid.X / f.U.X;
            if (xl < -f.Length / 2 - 1e-9 || xl > f.Length / 2 + 1e-9) continue;
            foreach (var st in JoinerySymbols.Section(f.Spec, xl))
                o.Add(st with { Pts = st.Pts.Select(q => new Vec2(f.Mid.Y + f.U.Y * xl + f.N.Y * q.X, q.Y)).ToList() });
            break;
        }
        foreach (var pc in Pieces(s, t, -s.SillHeight).Where(p => p.Box == null))
        {
            if (pc.Footprint is { } fp)
                foreach (var (a, b) in Spans(fp, 0)) o.Add(Rect(a, pc.Z0, b, pc.Z1, "cut"));
            else if (pc.Profile is { } pr && pr[0].X <= 0 && pr[0].X + pc.Sweep.X >= 0)
                o.Add(new JStroke(pr.Select(p => new Vec2(p.Y, p.Z)).ToList(), "cut", true));
            else if (pc.Faces is { } body)
            {
                var cross = new List<Vec2>();
                foreach (var f in body)
                    for (var i = 0; i < f.Count; i++)
                    {
                        var (a, b) = (f[i], f[(i + 1) % f.Count]);
                        if ((a.X <= 0 && b.X > 0) || (b.X <= 0 && a.X > 0)) { var k = -a.X / (b.X - a.X); cross.Add(new Vec2(a.Y + (b.Y - a.Y) * k, a.Z + (b.Z - a.Z) * k)); }
                    }
                if (cross.Count >= 3) o.Add(new JStroke(Polygon2.ConvexHull(cross).ToList(), "cut", true));
            }
        }
        // The wall above and below the opening, cut.
        o.Add(Rect(-t / 2, -s.SillHeight, t / 2, 0, "cut"));
        return o;
    }

    static JStroke Rect(double x0, double y0, double x1, double y1, string w) =>
        new([new(Math.Min(x0, x1), y0), new(Math.Max(x0, x1), y0), new(Math.Max(x0, x1), y1), new(Math.Min(x0, x1), y1)], w, true);

    /// <summary>The body's pieces as the configurator colours them in elevation (seen from inside): each piece's extent in X and Z, its role.</summary>
    public static List<JoineryBox> PreviewBoxes(JoinerySpec s, double t)
    {
        var faces = Faces(s, t / 2);
        return Pieces(s, t, -s.SillHeight).Select(pc =>
        {
            var c = Corners(pc, faces).ToList();
            return new JoineryBox(pc.Role, new Vec3(c.Min(p => p.X), c.Min(p => p.Y), c.Min(p => p.Z)), new Vec3(c.Max(p => p.X), c.Max(p => p.Y), c.Max(p => p.Z)));
        }).Where(b => b.Max.X - b.Min.X > 1e-6 && b.Max.Z - b.Min.Z > 1e-6).OrderBy(b => b.Max.Y).ToList();
    }

    // ------------------------------------------------------------ measures

    /// <summary>A bay's own measures (Lapin_Details): its projection, its glazed faces' developed width and area, the roof's, seat's and base's areas.</summary>
    public static IEnumerable<(string Name, double Value, string Unit)> Quantities(JoinerySpec s, double t)
    {
        var b = s.Bay!;
        var dev = Developed(s);
        yield return ("BayProjection", b.Projection, "LENGTH");
        yield return ("BayDevelopedWidth", dev.Width, "LENGTH");
        yield return ("BayGlazedArea", dev.Width * s.Height, "AREA");
        foreach (var pc in Pieces(s, t, -s.SillHeight).Where(p => p.Key is "bay:head" or "bay:seat" or "bay:base").GroupBy(p => p.Key))
        {
            var first = pc.First();
            var area = first.Footprint is { } fp ? Math.Abs(Polygon2.SignedArea(fp))
                : first.Faces is { } body ? Math.Abs(Polygon2.SignedArea(Polygon2.ConvexHull(body.SelectMany(f => f).Select(p => p.Xy)).ToList())) : 0;
            if (first.Key == "bay:head" && b.Head is { Kind: "hipped" or "lean-to" } h) area /= Math.Cos(Math.Clamp(h.Pitch, 5, 60) * Math.PI / 180);
            if (first.Key == "bay:base" && b.Base?.Kind == "floor") continue;
            yield return (first.Key switch { "bay:head" => "BayRoofArea", "bay:seat" => "BaySeatArea", _ => "BayBaseArea" }, Math.Round(area, 4), "AREA");
        }
    }

    // ------------------------------------------------------------ styles

    static JoinerySpec Style(string name, double w, double h, double sill, double[] cols, string[] ops, JoineryBay bay) =>
        JoinerySpec.DefaultWindow(name) with
        {
            Width = w, Height = h, SillHeight = sill, Columns = cols, Rows = [1],
            Panels = [.. ops.Select((op, i) => new JoineryPanel(i, 0, op))], Board = new JoineryBoard(0, 0), Bay = bay,
        };

    /// <summary>The built-in styles a bay, bow or box window starts from (Apply a style… in the configurator).</summary>
    public static readonly IReadOnlyList<(string Id, string Label, string Description, JoinerySpec Spec)> Styles =
    [
        ("bay-45", "Bay, 45°", "Three faces, the sides at 45°, a hipped roof over it, a seat inside",
            Style("Bay window 45°", 2.4, 1.5, 0.6, [1, 1, 1, 1], ["tilt-turn-left", "fixed", "fixed", "tilt-turn-right"],
                new JoineryBay("bay", 0.6, 45, Seat: new BaySeat(), Base: new BayBase("none"), Head: new BayHead("hipped", 0.12, 0.1, 30, "Zinc sheet", true, "Zinc sheet"), Sill: new BaySill(true, 0.04, 0.03, "Stone sill")))),
        ("bay-30", "Bay, 30° (shallow)", "Shallow sides at 30°, a lean-to roof",
            Style("Bay window 30°", 2.6, 1.5, 0.6, [1, 2, 1], ["side-left", "fixed", "side-right"],
                new JoineryBay("bay", 0.45, 30, Seat: new BaySeat(), Base: new BayBase("none"), Head: new BayHead("lean-to", 0.12, 0.1, 20, "Zinc sheet", true, "Zinc sheet"), Sill: new BaySill(true, 0.04, 0.03, "Stone sill")))),
        ("bay-60-floor", "Canted bay on the floor", "Sides at 60°, knee walls down to the floor, a hipped roof",
            Style("Canted bay 60°", 2.2, 1.6, 0.5, [1, 1, 1], ["side-left", "fixed", "side-right"],
                new JoineryBay("bay", 0.7, 60, Seat: new BaySeat(), Base: new BayBase("floor", 0.25, 0, 2, "Brick"), Head: new BayHead("hipped", 0.15, 0.12, 35, "Clay roof tiles", true, "Zinc sheet"), Sill: new BaySill(true, 0.05, 0.04, "Stone sill")))),
        ("bow-5", "Bow, 5 panels", "Five equal panels on an arc, on corbels, a hipped roof",
            Style("Bow window 5 panels", 3.0, 1.6, 0.6, [1, 1, 1, 1, 1], ["side-left", "fixed", "fixed", "fixed", "side-right"],
                new JoineryBay("bow", 0.5, Seat: new BaySeat(), Base: new BayBase("corbel", 0.15, 0.05, 2, "Stone"), Head: new BayHead("hipped", 0.12, 0.1, 25, "Zinc sheet", true, "Zinc sheet"), Sill: new BaySill(false)))),
        ("bow-4", "Bow, 4 panels", "Four equal panels on an arc, a flat roof",
            Style("Bow window 4 panels", 2.4, 1.5, 0.7, [1, 1, 1, 1], ["tilt-turn-left", "fixed", "fixed", "tilt-turn-right"],
                new JoineryBay("bow", 0.4, Seat: new BaySeat(), Base: new BayBase("slab", 0.12, 0.05, 2, "Concrete"), Head: new BayHead("flat", 0.12, 0.08, 30, "Zinc sheet", true, "Zinc sheet"), Sill: new BaySill(false)))),
        ("box-glazed", "Box, glazed sides", "A rectangular projection, its sides glazed, a flat roof, a slab under it",
            Style("Box window", 2.0, 1.5, 0.6, [1, 1, 1, 1], ["side-left", "fixed", "fixed", "side-right"],
                new JoineryBay("box", 0.5, 90, Seat: new BaySeat(), Base: new BayBase("slab", 0.12, 0.05, 2, "Concrete"), Head: new BayHead("flat", 0.12, 0.08, 30, "Zinc sheet", true, "Zinc sheet"), Sill: new BaySill(false)))),
        ("box-solid", "Box, solid sides", "A kitchen box window: solid sides, a lean-to roof",
            Style("Box window, solid sides", 1.4, 1.1, 0.95, [1, 1, 1], ["fixed", "tilt-turn-left", "fixed"],
                new JoineryBay("box", 0.4, 90, "solid", "Timber larch", Seat: new BaySeat(true, 0.03, 0.02), Base: new BayBase("none"), Head: new BayHead("lean-to", 0.08, 0.06, 15, "Zinc sheet", true, "Zinc sheet"), Sill: new BaySill(true, 0.04, 0.03, "Aluminium sill")))),
        ("oriel", "Oriel on corbels", "A box oriel on an upper floor, on three corbels, a hipped roof",
            Style("Oriel window", 2.0, 1.6, 0.8, [1, 1, 1, 1], ["tilt-turn-left", "fixed", "fixed", "tilt-turn-right"],
                new JoineryBay("box", 0.6, 90, Seat: new BaySeat(), Base: new BayBase("corbel", 0.18, 0.06, 3, "Stone"), Head: new BayHead("hipped", 0.14, 0.12, 35, "Clay roof tiles", true, "Zinc sheet"), Sill: new BaySill(false)))),
    ];

    /// <summary>The styles as the configurator reads them: each one's id, label, description and spec.</summary>
    public static JsonArray Catalog() => new([.. Styles.Select(x => (JsonNode)new JsonObject
    {
        ["id"] = x.Id, ["label"] = x.Label, ["description"] = x.Description, ["spec"] = x.Spec.ToJson(),
    })]);

    /// <summary>The materials a projecting window names (the seat, sill, base, head, flashing and solid sides), made when the project has none.</summary>
    public static void EnsureMaterials(IEdit edit, JoinerySpec s)
    {
        if (s.Bay is not { } b) return;
        foreach (var name in new[] { b.SideMaterial, b.Seat?.Material, b.Sill?.Material, b.Base?.Material, b.Head?.Material, b.Head?.FlashingMaterial })
            if (name is { Length: > 0 } n && !string.IsNullOrWhiteSpace(n)) Materials.Ensure(edit, n.Trim());
    }
}

/// <summary>
/// Styles a project keeps of its own (bay windows, sunshades): <c>{id, label, spec}</c> in a list on the project under a
/// key, ids starting <c>own:</c>; saved under a name (or saved again over one), renamed, deleted.
/// </summary>
public static class ProjectStyles
{
    public const string Prefix = "own:";

    public static List<JsonObject> All(IModelView m, string key) =>
        (Spatial.Project(m)?.Attrs[key] as JsonArray)?.OfType<JsonObject>().ToList() ?? [];

    public static JsonObject? Of(IModelView m, string key, string? id) => id?.StartsWith(Prefix, StringComparison.Ordinal) == true ? All(m, key).FirstOrDefault(s => (string?)s["id"] == id) : null;

    /// <summary>A style saved: a new one under its name, or the one of that name (or id) saved again. Its id.</summary>
    public static string Save(ICommandContext ctx, string key, string label, string? id, JsonObject spec, string what)
    {
        label = label.Trim();
        if (label.Length == 0) throw new CommandException($"A {what} style needs a name.");
        var project = Spatial.Project(ctx.Model) ?? throw new CommandException("The model has no project.");
        var list = All(ctx.Model, key);
        var same = list.FirstOrDefault(s => id != null ? (string?)s["id"] == id : string.Equals((string?)s["label"], label, StringComparison.OrdinalIgnoreCase));
        if (id != null && same == null) throw new CommandException($"The project has no such {what} style.");
        var sid = (string?)same?["id"] ?? Prefix + ctx.NewId();
        var style = new JsonObject { ["id"] = sid, ["label"] = label, ["spec"] = spec.DeepClone() };
        var next = list.Select(s => (string?)s["id"] == sid ? style : (JsonNode)s.DeepClone()).ToList();
        if (same == null) next.Add(style);
        var pa = project.CloneAttrs();
        pa[key] = new JsonArray([.. next]);
        ctx.Replace(project.With(attrs: pa));
        return sid;
    }

    public static void Rename(ICommandContext ctx, string key, string id, string label, string what)
    {
        label = label.Trim();
        if (label.Length == 0) throw new CommandException($"A {what} style needs a name.");
        var project = Spatial.Project(ctx.Model) ?? throw new CommandException("The model has no project.");
        var list = All(ctx.Model, key);
        if (list.All(s => (string?)s["id"] != id)) throw new CommandException($"The project has no such {what} style.");
        var pa = project.CloneAttrs();
        pa[key] = new JsonArray([.. list.Select(s => { var c = (JsonObject)s.DeepClone(); if ((string?)c["id"] == id) c["label"] = label; return (JsonNode)c; })]);
        ctx.Replace(project.With(attrs: pa));
    }

    public static void Delete(ICommandContext ctx, string key, string id, string what)
    {
        var project = Spatial.Project(ctx.Model) ?? throw new CommandException("The model has no project.");
        var list = All(ctx.Model, key);
        if (list.All(s => (string?)s["id"] != id)) throw new CommandException($"The project has no such {what} style.");
        var pa = project.CloneAttrs();
        var rest = list.Where(s => (string?)s["id"] != id).Select(s => (JsonNode)s.DeepClone()).ToArray();
        if (rest.Length > 0) pa[key] = new JsonArray(rest); else pa.Remove(key);
        ctx.Replace(project.With(attrs: pa));
    }
}
