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
        // The view's material graphics (decision 672): the material's elevation step for the view's scale; a project made
        // before real-size hatches (decision 671) goes on suggesting the paper ones; a name is guessed from, as cuts do.
        var viewMaterials = new ViewMaterialGraphics(m, "elevation", options.Scale, ViewStyles.Of(m, view));
        ResolvedMaterial? Graphics(string? id)
        {
            if (id == null) return null;
            if (resolved.TryGetValue(id, out var r)) return r;
            r = viewMaterials.Material(id);
            return resolved[id] = r is { Membrane: false } && (r.Hatch != null || r.Fill != null || r.Background != null) ? r : null;
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
        // Fills, solid hatches, then line hatches: a material's background hatch (decision 670) under its hatch.
        return pens.Where(kv => kv.Value.Loops.Count > 0 || kv.Value.Segments.Count > 0)
            .OrderBy(kv => kv.Value.Layer == FillLayer ? 0 : kv.Key.StartsWith("solidbg:", StringComparison.Ordinal) ? 0.5 : kv.Value.Fill != null ? 1 : kv.Key.StartsWith("hatchbg:", StringComparison.Ordinal) ? 1.5 : 2)
            .Select(kv => kv.Value).ToList();
    }

    static void Hatch(IModelView m, string cacheKey, Piece piece, IReadOnlyList<DrawPart> parts, ResolvedMaterial?[] graphics, bool joins,
        Pens.Options options, Dictionary<string, string?> seamKeys, Dictionary<string, Pen> pens, ref int left)
    {
        var budget = left;
        try { Draw(); }
        finally { left = budget; }
        void Draw()
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
            // What each element's faces reach lowest and leftmost in the view (decision 671): a real-size hatch's courses start at its base,
            // an element's own hatch origin (HatchOrigins) is measured from there.
            Dictionary<string, Vec2>? lowest = null;
            Vec2 LowestOf(string element)
            {
                if (lowest == null)
                {
                    lowest = [];
                    foreach (var f in faces.Faces)
                    {
                        var el = parts[f.Owner].Element;
                        var lo = lowest.TryGetValue(el, out var had) ? had : new Vec2(double.MaxValue, double.MaxValue);
                        foreach (var l in f.Loops) foreach (var v in l) { var q = faces.Frame.Map(v); lo = new Vec2(Math.Min(lo.X, q.X), Math.Min(lo.Y, q.Y)); }
                        lowest[el] = lo;
                    }
                }
                return lowest.TryGetValue(element, out var low) ? low : default;
            }
            for (var k = 0; k < visible.Count; k++)
            {
                var (s, cells) = visible[k];
                if (window is { } win) cells = cells.Select(c => ClipBox(c, win.X, win.Y)).Where(c => c.Length >= 3).ToList();
                if (cells.Count == 0) continue;
                var part = parts[s.Face.Owner];
                var g = graphics[s.Face.Owner]!;
                if (Cat(part.Class)?.Hidden == true) continue;
                var loops = cells.Select(c => (IReadOnlyList<Vec2>)c).ToList();
                if (MaterialGraphics.FillInk(g) is { } fillInk) Get("fill:" + fillInk, () => new Pen(FillLayer, null, 0, fillInk, null)).Loops.AddRange(cells.Select(c => c.Select(Laid).ToArray()));
                var category = Cat(part.Class)?.LineColor ?? "#000000";
                var elementColor = options.ElementColors?.GetValueOrDefault(part.Element);
                // The background hatch under the hatch (decision 670), then the hatch: each its own colour and weight.
                if (g.Background is { Hatch: { } bh } bg && HatchLibrary.Find(bh, options.Patterns) is { } bgPattern)
                    Stroke(bgPattern, true, elementColor ?? ResolvedMaterial.Ink(bg.HatchColor ?? category, g.Halftone, g.HatchOpacity), bg.HatchWeight, null, bg.HatchScale ?? g.HatchScale, bg.HatchAngle ?? 0);
                if (g.Hatch == null || HatchLibrary.Find(g.Hatch, options.Patterns) is not { } pattern) continue;
                var color = elementColor ?? (g.Flex ? g.HatchInk(category) : category);
                var dash = g.HatchLineType is { } lt && Dashes.Of(lt, options.LineTypes) is { Length: > 0 } ld ? ld : null;
                Stroke(pattern, false, color, g.HatchWeight, dash, g.HatchScale, g.HatchAngle);

                void Stroke(HatchPattern pattern, bool background, string color, double? weight, double[]? dash, double scale, double angle)
                {
                    // A solid pattern is the face's fill: in its fill colour, else in ink (forum topic 88); an element's override colour over both;
                    // the material's own hatch colour over its fill (decision 670).
                    if (pattern.Solid)
                    {
                        var solid = elementColor ?? (g.HatchColor != null || (background && g.Background?.HatchColor != null) ? color : g.Fill ?? color);
                        Get((background ? "solidbg:" : "solid:") + solid, () => new Pen(Layer, null, 0, solid, null)).Loops.AddRange(cells.Select(c => c.Select(Laid).ToArray()));
                        return;
                    }
                    if (budget <= 0) return;
                    // Paper or real size (decision 671; the material's hatchUnits over the pattern's, for its own hatch); too dense on paper, not drawn.
                    var over = background ? null : g.HatchUnits;
                    var model = HatchScale.UnitsOf(pattern, over) == HatchUnits.Model;
                    var factor = HatchScale.Factor(pattern, options.Scale, scale, over);
                    if (!BattHatch.Is(pattern) && HatchScale.TooDense(pattern, factor, options.Scale, options.MinHatchSpacing, over)) return;
                    var element = m.Get(part.Element);
                    var own = element != null && HatchOrigins.Of(element) != null;
                    var width = weight ?? (!background && g.HatchPen is { } pn ? LineStyles.PenWidth(options.Table ?? LineStyles.DefaultPenTable(), pn, options.Scale) : Pens.Default.Hatch);
                    var pen = Get($"{(background ? "hatchbg" : "hatch")}:{color}:{width}:{(dash == null ? "" : string.Join(" ", dash))}", () => new Pen(Layer, color, width, null, dash));
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
                        // From the group's bottom left, or the element's own origin carried onto the face.
                        var from = own && HatchOrigins.InView(element, faces.Frame, LowestOf(part.Element), model) is { } vo ? map.ToFace(vo) : groups[gk].Min;
                        var (segs, _) = HatchFill.Segments(onFace, pattern, new HatchOptions(factor, angle, from, budget));
                        budget -= segs.Count;
                        pen.Segments.AddRange(segs.Select(x => (Laid(map.ToView(x.A)), Laid(map.ToView(x.B)))));
                    }
                    else
                    {
                        // Aligned to the view: a developed section's pieces hatch from the developed drawing's origin, so courses run on across them.
                        // A real-size pattern from the element's base, an element's own origin from there (decision 671); a paper one from the view's origin.
                        var from = HatchOrigins.InView(element, faces.Frame, LowestOf(part.Element), model) is { } vo ? (Vec2?)(own ? Laid(vo) : vo) : null;
                        var (segs, _) = HatchFill.Segments(loops.Select(l => (IReadOnlyList<Vec2>)l.Select(Laid).ToArray()).ToList(), pattern, new HatchOptions(factor, angle, from, budget));
                        budget -= segs.Count;
                        pen.Segments.AddRange(segs.Select(x => (x.A, x.B)));
                    }
                }
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
    public static Vec2[] ClipBox(Vec2[] poly, (double Min, double Max) xs, (double Min, double Max) ys) =>
        [.. Polygon2.ClipBox(poly, xs.Min, ys.Min, xs.Max, ys.Max)];
}
