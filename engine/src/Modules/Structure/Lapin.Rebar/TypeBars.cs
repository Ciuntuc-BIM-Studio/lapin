using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Drawing.Views;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Rebar;

/// <summary>One row of longitudinal bars: how many and how thick.</summary>
public sealed record BarRow(int Count, double Diameter, int Mark = 0)
{
    public JsonObject ToJson() => new() { ["count"] = Count, ["diameter"] = Diameter, ["mark"] = Mark };
    public static BarRow From(JsonNode? n, BarRow d) => n is JsonObject o
        ? new BarRow((int)Math.Max(0, Json.Number(o["count"]) ?? d.Count), Json.Number(o["diameter"]) ?? d.Diameter, (int)(Json.Number(o["mark"]) ?? 0)) : d;
}

/// <summary>
/// The longitudinal bars a beam's or a column's type is configured with (decision 216): a beam's top and bottom rows
/// and the bars on each side between them; a column's bars on each face (its corners shared). Laid inside the
/// stirrups, from end to end of the axis plus an extension, kept on the type as <c>bars</c>.
/// </summary>
public sealed record BarsSpec(bool Enabled, BarRow Top, BarRow Bottom, BarRow Side, BarRow PerFace, double Extend)
{
    public const string Key = "bars";

    public static BarsSpec Default() => new(false, new BarRow(2, 0.012), new BarRow(3, 0.016), new BarRow(0, 0.010), new BarRow(3, 0.016), 0);

    public static BarsSpec? Of(Entity type) => type.Attrs[Key] is JsonObject o ? From(o) : null;

    public static BarsSpec From(JsonObject o)
    {
        var d = Default();
        return new BarsSpec(o["enabled"] is JsonValue ev && ev.TryGetValue<bool>(out var en) && en, BarRow.From(o["top"], d.Top), BarRow.From(o["bottom"], d.Bottom),
            BarRow.From(o["side"], d.Side), BarRow.From(o["perFace"], d.PerFace), Json.Number(o["extend"]) ?? 0);
    }

    public JsonObject ToJson() => new() { ["enabled"] = Enabled, ["top"] = Top.ToJson(), ["bottom"] = Bottom.ToJson(), ["side"] = Side.ToJson(), ["perFace"] = PerFace.ToJson(), ["extend"] = Extend };
}

/// <summary>
/// Where a type's longitudinal bars run on an element: each a line in space along the axis, at its place in the
/// section — the corners first, the rest spread evenly — a cover, a stirrup and half a bar in from the faces. A view
/// draws them as it lies to the axis: dots of their diameter in a cross-section, lines along in a longitudinal one.
/// </summary>
public static class TypeBars
{
    /// <summary>A group of bars (a row) on an element: its row, its name, each bar's line in space, the bar's length for the schedule.</summary>
    public sealed record Group(string Name, BarRow Row, List<Vec3[]> Bars, double Length);

    public sealed record Laid(Entity Element, BarsSpec Spec, List<Group> Groups, Vec3 Start, Vec3 Dir, double AxisLength);

    public static BarsSpec? SpecOf(IModelView m, Entity e) => m.RelatingOf(e.Id, IfcClass.RelDefinesByType) is { } type ? BarsSpec.Of(type) : null;

    /// <summary>The even spread of <paramref name="n"/> places between <paramref name="lo"/> and <paramref name="hi"/> (the ends included); one in the middle; none for 0.</summary>
    public static List<double> Spread(int n, double lo, double hi) => n <= 0 ? [] : n == 1 ? [(lo + hi) / 2] : Enumerable.Range(0, n).Select(i => lo + (hi - lo) * i / (n - 1)).ToList();

    public static Laid? Of(IModelView m, Entity e)
    {
        if (SpecOf(m, e) is not { Enabled: true } spec) return null;
        var stirrups = TypeStirrups.SpecOf(m, e) ?? StirrupSpec.Default();
        // In from each face: the cover, the stirrup, half the bar.
        double In(double d) => stirrups.Cover + stirrups.Diameter + d / 2;
        if (e.Class == IfcClass.Beam)
        {
            var (section, _) = Beams.Spec(m, e);
            if (section.Kind != "rectangle") return null;
            var c = PathCurve.Of(e);
            if (c.Length < 1e-6) return null;
            var top = Beams.Top(m, e);
            var bottom = top - section.Depth;
            var length = c.Length + 2 * spec.Extend;
            var groups = new List<Group>();
            // A bar along the axis at y across (left positive) and z: the arc sampled, a straight one its two ends, both run out by the extension.
            Vec3[] Bar(double y, double z)
            {
                if (!c.IsArc)
                {
                    var u = c.TangentAt(0);
                    var (a, b) = (c.At(0, y) - u * spec.Extend, c.At(c.Length, y) + u * spec.Extend);
                    return [a.At(z), b.At(z)];
                }
                var pts = c.Line(0, c.Length, y).Select(p => p.At(z)).ToList();
                if (spec.Extend > 0) { pts.Insert(0, (c.At(0, y) - c.TangentAt(0) * spec.Extend).At(z)); pts.Add((c.At(c.Length, y) + c.TangentAt(c.Length) * spec.Extend).At(z)); }
                return [.. pts];
            }
            void Row(string name, BarRow row, double z)
            {
                if (row.Count <= 0) return;
                var half = section.Width / 2 - In(row.Diameter);
                groups.Add(new Group(name, row, Spread(row.Count, -half, half).Select(y => Bar(y, z)).ToList(), length));
            }
            Row("top", spec.Top, top - In(spec.Top.Diameter));
            Row("bottom", spec.Bottom, bottom + In(spec.Bottom.Diameter));
            if (spec.Side.Count > 0)
            {
                // Between the rows, on both faces, the rows' places left out.
                var (z0, z1) = (bottom + In(spec.Bottom.Diameter), top - In(spec.Top.Diameter));
                var zs = Spread(spec.Side.Count + 2, z0, z1).Skip(1).Take(spec.Side.Count).ToList();
                var half = section.Width / 2 - In(spec.Side.Diameter);
                groups.Add(new Group("side", spec.Side, zs.SelectMany(z => new[] { Bar(-half, z), Bar(half, z) }).ToList(), length));
            }
            var t0 = c.TangentAt(0);
            return new Laid(e, spec, groups, new Vec3(c.Start.X, c.Start.Y, (top + bottom) / 2), new Vec3(t0.X, t0.Y, 0), c.Length);
        }
        if (e.Class == IfcClass.Column)
        {
            var (section, _, height) = Columns.Spec(m, e);
            if (section.Kind != "rectangle" || height <= 1e-6 || spec.PerFace.Count < 2) return null;
            var at = e.Attrs["at"] is JsonArray q && q.Count >= 2 ? new Vec2(Json.Number(q[0]) ?? 0, Json.Number(q[1]) ?? 0) : default;
            var baseZ = (Spatial.StoreyOf(m, e.Id)?.Num("elevation") ?? 0) + e.Num("baseOffset");
            var rot = e.Num("rotation") * Math.PI / 180;
            var (u, v) = (new Vec2(Math.Cos(rot), Math.Sin(rot)), new Vec2(-Math.Sin(rot), Math.Cos(rot)));
            var d = spec.PerFace.Diameter;
            var (hx, hy) = (section.Width / 2 - In(d), section.Depth / 2 - In(d));
            // Round the perimeter: the corners once, the rest of each face's bars between them.
            var places = new List<Vec2>();
            var n = spec.PerFace.Count;
            foreach (var x in Spread(n, -hx, hx)) places.Add(new Vec2(x, -hy));
            foreach (var y in Spread(n, -hy, hy).Skip(1)) places.Add(new Vec2(hx, y));
            foreach (var x in Spread(n, -hx, hx).Reverse<double>().Skip(1)) places.Add(new Vec2(x, hy));
            foreach (var y in Spread(n, -hy, hy).Reverse<double>().Skip(1).Take(n - 2)) places.Add(new Vec2(-hx, y));
            var length = height + 2 * spec.Extend;
            var bars = places.Select(p => { var w = at + u * p.X + v * p.Y; return new[] { w.At(baseZ - spec.Extend), w.At(baseZ + height + spec.Extend) }; }).ToList();
            return new Laid(e, spec, [new Group("all", spec.PerFace, bars, length)], at.At(baseZ), Vec3.UnitZ, height);
        }
        return null;
    }

    /// <summary>
    /// The bars of every beam and column a view shows, each projected on the view: a dot of its diameter where it runs
    /// into the view (a cross-section), a line as thick as it is along; with each row's label and the mark's balloon.
    /// Plans and elevations only when the view is the structure's.
    /// </summary>
    public static IEnumerable<ViewMark> Marks(ViewContext c, RebarSettings st)
    {
        var m = c.Model;
        var kind = c.View.Str("kind") ?? "plan";
        if (kind is "plan" or "elevation" && !string.Equals(c.View.Str("discipline"), "Structure", StringComparison.OrdinalIgnoreCase)) yield break;
        if (kind is not ("plan" or "elevation" or "section")) yield break;
        var limit = kind == "section" ? c.View.NumOrNull("depth") ?? (AcrossSections.Is(c.View) ? c.View.NumOrNull("margin") ?? AcrossSections.DefaultMargin : double.PositiveInfinity) : double.PositiveInfinity;
        var hidden = (c.View.Attrs["hiddenLayers"] as JsonArray)?.Select(x => (string?)x).OfType<string>().ToHashSet() ?? [];
        if (hidden.Contains(RebarShapes.LongitudinalLayer)) yield break;
        var storey = c.View.Str("storeyId");
        var plan = kind == "plan";
        foreach (var e in m.Entities)
        {
            if (e.Class is not (IfcClass.Beam or IfcClass.Column) || Of(m, e) is not { } laid) continue;
            if (plan && Spatial.StoreyOf(m, e.Id)?.Id != storey) continue;
            var mk = new ViewMark(e.Id);
            var any = false;
            foreach (var g in laid.Groups)
            {
                var d = g.Row.Diameter;
                var color = st.ColorByDiameter ? RebarShapes.ColorOf(d) : "#000000";
                var width = Math.Max(0.25, d * 1000 / c.Scale);
                var dots = new List<Vec2>();
                var lines = new List<(Vec2 A, Vec2 B)>();
                foreach (var bar in g.Bars)
                {
                    var depths = bar.Select(c.Frame.Depth).ToList();
                    if (!plan && (depths.Max() < -1e-6 || depths.Min() > limit + 1e-6)) continue;
                    var pts = bar.Select(c.Map).ToList();
                    var extent = pts.Max(p => p.X) - pts.Min(p => p.X) + pts.Max(p => p.Y) - pts.Min(p => p.Y);
                    if (extent < 1e-4) dots.Add(pts[0]);
                    else for (var i = 0; i + 1 < pts.Count; i++) lines.Add((pts[i], pts[i + 1]));
                }
                if (dots.Count == 0 && lines.Count == 0) continue;
                any = true;
                foreach (var p in dots)
                {
                    var circle = ViewMark.Circle(p, Math.Max(d / 2, 0.6 * c.Mm), 24);
                    mk.Fills.Add(([circle], color));
                    mk.Polyline(circle, true, RebarShapes.LongitudinalLayer, 0.18, color);
                }
                foreach (var (a, b) in lines) mk.Line(a, b, RebarShapes.LongitudinalLayer, width, color);
                mk.Snaps.AddRange(dots);
                // The row's label: "3Ø16 L=6000", its balloon beside, over the bars along, to the right of the dots across.
                var label = $"{g.Bars.Count}{RebarShapes.Dia(d)} L={RebarShapes.Mm(g.Length)}";
                var r = st.Balloon / 2 * c.Mm;
                Vec2 lab, target;
                if (dots.Count > 0)
                {
                    var right = dots.MaxBy(p => p.X);
                    lab = right + new Vec2(10 * c.Mm, g.Name == "top" ? 6 * c.Mm : g.Name == "bottom" ? -6 * c.Mm : 0);
                    target = right;
                }
                else
                {
                    var all = lines.SelectMany(l => new[] { l.A, l.B }).ToList();
                    var mid = new Vec2((all.Min(p => p.X) + all.Max(p => p.X)) / 2, (all.Min(p => p.Y) + all.Max(p => p.Y)) / 2);
                    var over = g.Name == "bottom" ? -1 : 1;
                    // The rows' labels a third of the way along, clear of the stirrups' at the middle; the top row's over, the bottom's under.
                    lab = mid + new Vec2(-(laid.AxisLength / 3), over * (8 * c.Mm));
                    target = lines.Select(l => l.A + (l.B - l.A) * 0.5).MinBy(p => (p - lab).Length);
                }
                mk.Polyline(ViewMark.Circle(lab, r, 32), true, RebarShapes.AnnotationLayer, 0.35, color);
                if (g.Row.Mark > 0) mk.Text(new PenText(lab, g.Row.Mark.ToString(CultureInfo.InvariantCulture), st.TextHeight, 0, "middle", "middle", true), color);
                if ((target - lab).Length > r) mk.Line(lab + (target - lab).Normalized() * r, target, RebarShapes.AnnotationLayer, 0.25, color);
                mk.Text(new PenText(lab + new Vec2(r + 1.0 * c.Mm, 0), label, st.TextHeight, 0, "start", "middle"), color);
            }
            if (any) yield return mk;
        }
    }

    /// <summary>The schedule's entries: one per row of every element's configured bars, under the row's mark.</summary>
    public static IEnumerable<RebarSchedule.Entry> Entries(IModelView m)
    {
        foreach (var e in m.Entities)
            if (e.Class is IfcClass.Beam or IfcClass.Column && Of(m, e) is { } laid)
                foreach (var g in laid.Groups) yield return new RebarSchedule.Entry(g.Row.Mark, g.Row.Diameter, "Straight bar", g.Bars.Count, g.Length);
    }

    public static int HighestMark(IModelView m) => m.OfClass(IfcClass.BeamType).Concat(m.OfClass(IfcClass.ColumnType)).Select(t => BarsSpec.Of(t))
        .OfType<BarsSpec>().SelectMany(s => new[] { s.Top.Mark, s.Bottom.Mark, s.Side.Mark, s.PerFace.Mark }).DefaultIfEmpty(0).Max();

    /// <summary>A type's bars set from a command: {id, enabled?, top?: {count, diameter}, bottom?, side?, perFace?, extend?}; each row used gets a mark of its own.</summary>
    public static void Set(ICommandContext ctx, JsonObject a)
    {
        var type = ctx.Model.Get((string?)a["id"] ?? "") is { Class: IfcClass.BeamType or IfcClass.ColumnType } t ? t : throw new CommandException("Bars are set on a beam or a column type.");
        var now = BarsSpec.Of(type) ?? BarsSpec.Default();
        var enabled = a["enabled"] is JsonValue ev && ev.TryGetValue<bool>(out var en) ? en : now.Enabled;
        BarRow Row(string key, BarRow old)
        {
            var row = BarRow.From(a[key], old) with { Mark = old.Mark };
            if (row.Diameter < 0.006 || row.Diameter > 0.04) throw new CommandException("A bar is between 6 and 40 mm thick.");
            if (row.Count > 40) throw new CommandException("A row holds 40 bars at most.");
            return row;
        }
        var top = Row("top", now.Top);
        var bottom = Row("bottom", now.Bottom);
        var side = Row("side", now.Side);
        var perFace = Row("perFace", now.PerFace);
        if (type.Class == IfcClass.ColumnType && perFace.Count < 2) throw new CommandException("A column has two bars on each face at least (its corners).");
        var extend = Json.Number(a["extend"]) ?? now.Extend;
        if (extend < 0 || extend > 2) throw new CommandException("The extension is between 0 and 2 m.");
        // Each row in use takes a mark once, after every mark the model has.
        if (enabled)
        {
            var next = Math.Max(Math.Max(Rebars.NextMark(ctx.Model), TypeStirrups.HighestMark(ctx.Model) + 1), HighestMark(ctx.Model) + 1);
            BarRow Marked(BarRow r) { if (r.Count > 0 && r.Mark <= 0) r = r with { Mark = next++ }; return r; }
            if (type.Class == IfcClass.BeamType) { top = Marked(top); bottom = Marked(bottom); side = Marked(side); }
            else perFace = Marked(perFace);
        }
        var attrs = type.CloneAttrs();
        attrs[BarsSpec.Key] = new BarsSpec(enabled, top, bottom, side, perFace, extend).ToJson();
        ctx.Replace(type.With(attrs: attrs));
    }
}
