using Lapin.Drawing;
using Lapin.Drawing.Hatch;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// The surface hatches of elevations and sections (decision 325), the Archicad way: each face the drawing sees, turned
/// towards the viewer, is hatched with its material's <b>elevation</b> graphics (Manage → Materials, Elevation: pattern,
/// scale, angle, pen, fill; <see cref="MaterialGraphics.Resolve"/> with "elevation"), only where no nearer face hides it
/// (<see cref="ViewFaces"/>, shared with the sun shadows), under every line, so sheets, PDF and DXF print it (layers
/// <see cref="Layer"/> and <see cref="FillLayer"/>).
/// <list type="bullet">
/// <item>A face's material is its part's: a layered wall's layers are parts of their own, so the face seen is the
/// outermost layer's (the brick, the render), and a reveal's its lining.</item>
/// <item>Aligned to the view (<c>hatchAlign</c> view, the default) the pattern lies in the drawing's own coordinates, from
/// its origin, so it goes on unbroken from one face to the next. Aligned to the layer (<c>layer</c>) it lies on the face
/// itself, along the face's level line, from the bottom left of its surface, and is projected as the face is (a face
/// seen at a slant shows its courses foreshortened); the faces of one material going on in one plane (the visual seams,
/// <see cref="Views.SeamKeyOf"/>, with the view style's joins) share that origin, so their hatch is one.</item>
/// <item>A section hatches what it sees beyond its cut, its cut areas hiding what lies behind them; a plan never (its cuts
/// keep their plan hatch); a developed section (jogged, curved, along an element) on each straight piece between its ends,
/// laid at its station (<see cref="Piece"/>).</item>
/// </list>
/// The view style's <c>surfaceHatch</c> turns it on or off (on in Architectural, off in Structural, MEP and Coordination).
/// </summary>
public static class SurfaceHatches
{
    public const string Layer = "SURFACE-HATCH";
    public const string FillLayer = "SURFACE-FILL";
    /// <summary>The most strokes one drawing's surfaces lay (a facade of brick at 1:50 is some tens of thousands).</summary>
    public const int MaxSegments = 300_000;

    /// <summary>Whether a view of this kind hatches its surfaces: an elevation or a section, by its style.</summary>
    public static bool Allowed(IModelView m, Entity view, ViewSpec spec) => spec.Kind is "elevation" or "section" && ViewStyles.Of(m, view).SurfaceHatch;

    // Each view's visible faces kept between its drawings (by its id): unchanged elements hatch again without the sweep.
    static readonly Dictionary<string, ViewFaces.VisibilityCache> Caches = [];

    static ViewFaces.VisibilityCache CacheOf(string viewId)
    {
        lock (Caches)
        {
            if (Caches.TryGetValue(viewId, out var c)) return c;
            if (Caches.Count >= 64) Caches.Clear();
            return Caches[viewId] = new ViewFaces.VisibilityCache();
        }
    }

    /// <summary>
    /// One planar drawing of a view: a plain view is one, laid at 0; a developed section (jogged, curved, along an element)
    /// one per straight piece, each seeing only between its ends (<see cref="ViewSpec.Span"/>), laid at its station.
    /// <paramref name="Scene"/> gives the faces the piece sees (made once, shared with its shadows); <paramref name="Index"/>
    /// tells its visible faces' cache apart.
    /// </summary>
    public sealed record Piece(ViewSpec Spec, double Station, Func<ViewFaces> Scene, int Index = 0);

    /// <summary>
    /// The view's surface hatches as pens (fills first, then strokes), to be laid under everything it draws; none when it
    /// draws none. Each piece's faces (<paramref name="pieces"/>) are hatched on its own plane, then laid at its station.
    /// </summary>
    public static List<Pen> PensOf(IModelView m, Entity view, IReadOnlyList<Piece> pieces, IReadOnlyList<DrawPart> parts,
        Pens.Options options, Dictionary<string, string?> seamKeys)
    {
        if (pieces.Count == 0 || !Allowed(m, view, pieces[0].Spec)) return [];
        // Each material's elevation graphics, once; a part draws a surface only when its material gives a hatch or a fill.
        var resolved = new Dictionary<string, ResolvedMaterial?>();
        ResolvedMaterial? Graphics(string? id)
        {
            if (id == null) return null;
            if (resolved.TryGetValue(id, out var r)) return r;
            // A material named rather than kept (a track's ballast): its look guessed from the name, as cuts do.
            r = m.Get(id) is { } mat ? MaterialGraphics.Resolve(mat, "elevation") : GlobalId.IsValid(id) ? null : MaterialGraphics.Resolve(null, "elevation", id);
            return resolved[id] = r is { Membrane: false } && (r.Hatch != null || r.Fill != null) ? r : null;
        }
        var graphics = parts.Select(p => Graphics(p.Material)).ToArray();
        if (graphics.All(g => g == null)) return [];
        var joins = ViewStyles.Of(m, view).Joins;
        var pens = new Dictionary<string, Pen>();
        var budget = MaxSegments;
        foreach (var piece in pieces)
        {
            if (budget <= 0) break;
            Hatch(m, view.Id + (pieces.Count > 1 ? "#" + piece.Index : ""), piece, parts, graphics, joins, options, seamKeys, pens, ref budget);
        }
        return pens.Values.Where(p => p.Loops.Count > 0 || p.Segments.Count > 0).OrderBy(p => p.Layer == FillLayer ? 0 : p.Fill != null ? 1 : 2).ToList();
    }

    static void Hatch(IModelView m, string cacheKey, Piece piece, IReadOnlyList<DrawPart> parts, ResolvedMaterial?[] graphics, bool joins,
        Pens.Options options, Dictionary<string, string?> seamKeys, Dictionary<string, Pen> pens, ref int budget)
    {
        var spec = piece.Spec;
        var faces = piece.Scene();
        var cache = CacheOf(cacheKey).For(faces);
        var visible = faces.VisibleAll(s => graphics[s.Face.Owner] != null, cache);
        if (visible.Count == 0) return;
        var window = WindowOf(spec);
        // Laid at its station: a developed section's piece moves along x.
        var shift = new Vec2(piece.Station, 0);
        Vec2 Laid(Vec2 p) => p + shift;
        // The faces of one material going on in one plane share their hatch's origin on it (aligned to the layer).
        var groups = new Dictionary<string, (Vec2 Min, bool Any)>();
        string GroupOf(ViewFaces.Seen s)
        {
            var p = parts[s.Face.Owner];
            var key = Views.SeamKeyOf(m, p.Material, p.Class, seamKeys) ?? "\u0001" + p.Element;
            if (!joins) key += "\u0000" + p.Element;
            var n = s.Face.Normal;
            var d = Vec3.Dot(s.Face.Loops[0][0], n);
            return FormattableString.Invariant($"{key}|{Math.Round(n.X, 3)},{Math.Round(n.Y, 3)},{Math.Round(n.Z, 3)}|{Math.Round(d, 3)}");
        }
        var keys = new string[visible.Count];
        for (var k = 0; k < visible.Count; k++)
        {
            var (s, cells) = visible[k];
            if (cells.Count == 0 || graphics[s.Face.Owner]!.HatchAlign != HatchAlign.Layer) continue;
            keys[k] = GroupOf(s);
            var (h, w) = Axes(s.Face.Normal, faces.Frame);
            var min = groups.TryGetValue(keys[k], out var g) ? g.Min : new Vec2(double.MaxValue, double.MaxValue);
            foreach (var l in s.Face.Loops) foreach (var v in l) min = new Vec2(Math.Min(min.X, Vec3.Dot(v, h)), Math.Min(min.Y, Vec3.Dot(v, w)));
            groups[keys[k]] = (min, true);
        }
        Pen Get(string key, Func<Pen> make) => pens.TryGetValue(key, out var p) ? p : pens[key] = make();
        CategoryGraphics? Cat(string cls) => LineStyles.CategoryOf(cls) is { } c && options.Categories != null && options.Categories.TryGetValue(c, out var cg) ? cg : null;
        var unit = options.Scale / 1000;
        for (var k = 0; k < visible.Count; k++)
        {
            var (s, cells) = visible[k];
            if (window is { } win) cells = cells.Select(c => ClipBox(c, win.X, win.Y)).Where(c => c.Length >= 3).ToList();
            if (cells.Count == 0) continue;
            var part = parts[s.Face.Owner];
            var g = graphics[s.Face.Owner]!;
            if (Cat(part.Class)?.Hidden == true) continue;
            var loops = cells.Select(c => (IReadOnlyList<Vec2>)c).ToList();
            if (g.Fill != null) Get("fill:" + g.Fill, () => new Pen(FillLayer, null, 0, g.Fill, null)).Loops.AddRange(cells.Select(c => c.Select(Laid).ToArray()));
            if (g.Hatch == null || HatchLibrary.Find(g.Hatch, options.Patterns) is not { } pattern) continue;
            var color = options.ElementColors?.GetValueOrDefault(part.Element) ?? Cat(part.Class)?.LineColor ?? "#000000";
            if (pattern.Solid) { Get("solid:" + color, () => new Pen(Layer, null, 0, color, null)).Loops.AddRange(cells.Select(c => c.Select(Laid).ToArray())); continue; }
            if (budget <= 0) continue;
            var width = g.HatchPen is { } pn ? LineStyles.PenWidth(options.Table ?? LineStyles.DefaultPenTable(), pn, options.Scale) : Pens.Default.Hatch;
            var pen = Get($"hatch:{color}:{width}", () => new Pen(Layer, color, width, null, null));
            if (BattHatch.Is(pattern))
            {
                // Insulation is never guessed for a surface (MaterialGraphics.ElevationFromName); asked for, its looped line runs
                // along the face as it is seen, kept where the face is visible.
                var outline = s.View.Select(l => (IReadOnlyList<Vec2>)l).ToList();
                var batt = BattHatch.Segments(outline, 0, budget).Where(x => cells.Any(c => InConvex(c, (x.A + x.B) * 0.5))).ToList();
                budget -= batt.Count;
                pen.Segments.AddRange(batt.Select(x => (Laid(x.A), Laid(x.B))));
            }
            else if (keys[k] is { } gk && Affine(s.Face.Normal, Vec3.Dot(s.Face.Loops[0][0], s.Face.Normal), faces.Frame) is { } map)
            {
                // On the face: the cells carried into its plane's own axes, hatched there from the group's corner, carried back.
                var onFace = loops.Select(l => (IReadOnlyList<Vec2>)l.Select(map.ToFace).ToArray()).ToList();
                var (segs, _) = HatchFill.Segments(onFace, pattern, new HatchOptions(g.HatchScale * unit, g.HatchAngle, groups[gk].Min, budget));
                budget -= segs.Count;
                pen.Segments.AddRange(segs.Select(x => (Laid(map.ToView(x.A)), Laid(map.ToView(x.B)))));
            }
            else
            {
                // Aligned to the view: a developed section's pieces hatch from the developed drawing's origin, so courses run on across them.
                var (segs, _) = HatchFill.Segments(loops.Select(l => (IReadOnlyList<Vec2>)l.Select(Laid).ToArray()).ToList(), pattern, new HatchOptions(g.HatchScale * unit, g.HatchAngle, null, budget));
                budget -= segs.Count;
                pen.Segments.AddRange(segs.Select(x => (x.A, x.B)));
            }
        }
    }

    /// <summary>The box of the view a drawing sees (a piece of a developed section between its ends, a section across an element its window), or null.</summary>
    public static ((double Min, double Max) X, (double Min, double Max) Y)? WindowOf(ViewSpec spec)
    {
        (double Min, double Max) all = (double.NegativeInfinity, double.PositiveInfinity);
        return spec.Span != null || spec.Rise != null ? (spec.Span ?? all, spec.Rise ?? all) : null;
    }

    static bool InConvex(Vec2[] c, Vec2 p)
    {
        int sign = 0;
        for (var i = 0; i < c.Length; i++)
        {
            var a = c[i]; var b = c[(i + 1) % c.Length];
            var cr = (b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X);
            if (Math.Abs(cr) < 1e-12) continue;
            var sg = Math.Sign(cr);
            if (sign == 0) sign = sg; else if (sg != sign) return false;
        }
        return true;
    }

    /// <summary>A plane's own axes: along its level line (to the view's right) and up its slope (a level face: the view's right and up as they lie on it).</summary>
    static (Vec3 H, Vec3 W) Axes(Vec3 n, ViewFrame view)
    {
        var h = Vec3.Cross(Vec3.UnitZ, n);
        if (h.Length < 1e-6) h = view.Right - n * Vec3.Dot(view.Right, n);
        h = h.Normalized();
        if (Vec3.Dot(h, view.Right) < 0) h = -h;
        var w = Vec3.Cross(n, h).Normalized();
        if (Vec3.Dot(w, view.Up) < 0) w = -w;
        return (h, w);
    }

    /// <summary>The map between a plane's own axes and the view (the projection along the view, an affine map), or null when the plane is seen edge on.</summary>
    static (Func<Vec2, Vec2> ToFace, Func<Vec2, Vec2> ToView)? Affine(Vec3 n, double offset, ViewFrame view)
    {
        var (h, w) = Axes(n, view);
        // A point of the plane, offset·n + u·h + v·w, is seen at (x, y) = o + (a·u + b·v, c·u + d·v).
        double a = Vec3.Dot(h, view.Right), b = Vec3.Dot(w, view.Right), c = Vec3.Dot(h, view.Up), d = Vec3.Dot(w, view.Up);
        var det = a * d - b * c;
        if (Math.Abs(det) < 1e-6) return null;
        // Where the plane's foot from the world's origin is seen: the face's axes are measured from it.
        var o = view.Map(n * offset);
        return (p => { var x = p.X - o.X; var y = p.Y - o.Y; return new Vec2((d * x - b * y) / det, (a * y - c * x) / det); },
            q => new Vec2(o.X + a * q.X + b * q.Y, o.Y + c * q.X + d * q.Y));
    }

    /// <summary>A convex cell clipped to a box of the view (a section across an element sees only its window).</summary>
    public static Vec2[] ClipBox(Vec2[] poly, (double Min, double Max) xs, (double Min, double Max) ys)
    {
        IEnumerable<Vec2> pts = poly;
        foreach (var (axis, bound, keepAbove) in new[] { (0, xs.Min, true), (0, xs.Max, false), (1, ys.Min, true), (1, ys.Max, false) })
        {
            if (double.IsInfinity(bound)) continue;
            var src = pts.ToList();
            var o = new List<Vec2>();
            for (var i = 0; i < src.Count; i++)
            {
                Vec2 p = src[i], q = src[(i + 1) % src.Count];
                double pv = axis == 0 ? p.X : p.Y, qv = axis == 0 ? q.X : q.Y;
                bool pin = keepAbove ? pv >= bound : pv <= bound, qin = keepAbove ? qv >= bound : qv <= bound;
                if (pin) o.Add(p);
                if (pin != qin) o.Add(p + (q - p) * ((bound - pv) / (qv - pv)));
            }
            pts = o;
        }
        return pts.ToArray();
    }
}
