using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Drawing.Views;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Rebar;

/// <summary>
/// The stirrups a beam's or a column's type is configured with (decision 215): their diameter, the concrete cover,
/// and how they are laid along the element — one step throughout, or three zones each taking a share of the length
/// (a third by default) at its own step. Kept on the type as <c>stirrups</c>; the free edition draws them in 2D only.
/// </summary>
public sealed record StirrupSpec(bool Enabled, double Diameter, double Cover, string Mode, double Step, IReadOnlyList<(double Share, double Step)> Zones, int Mark)
{
    public const string Key = "stirrups";
    public static readonly string[] Modes = ["uniform", "zones"];

    public static StirrupSpec Default() => new(false, 0.008, 0.025, "uniform", 0.15, [(1.0 / 3, 0.1), (1.0 / 3, 0.2), (1.0 / 3, 0.1)], 0);

    public static StirrupSpec? Of(Entity type) => type.Attrs[Key] is JsonObject o ? From(o) : null;

    public static StirrupSpec From(JsonObject o)
    {
        var d = Default();
        var zones = (o["zones"] as JsonArray)?.OfType<JsonObject>().Select(z => (Json.Number(z["share"]) ?? 1.0 / 3, Json.Number(z["step"]) ?? d.Step)).ToList();
        return new StirrupSpec(o["enabled"] is JsonValue ev && ev.TryGetValue<bool>(out var en) && en, Json.Number(o["diameter"]) ?? d.Diameter, Json.Number(o["cover"]) ?? d.Cover,
            (string?)o["mode"] is { } md && Modes.Contains(md) ? md : d.Mode, Json.Number(o["step"]) ?? d.Step, zones is { Count: > 0 } ? zones : d.Zones, (int)(Json.Number(o["mark"]) ?? 0));
    }

    public JsonObject ToJson() => new()
    {
        ["enabled"] = Enabled, ["diameter"] = Diameter, ["cover"] = Cover, ["mode"] = Mode, ["step"] = Step, ["mark"] = Mark,
        ["zones"] = new JsonArray(Zones.Select(z => (JsonNode)new JsonObject { ["share"] = Math.Round(z.Share, 6), ["step"] = z.Step }).ToArray()),
    };

    /// <summary>The zones as a run has them along a length: each its share of it at its step; none for one step throughout.</summary>
    public List<Runs.Zone>? RunZones(double length)
    {
        if (Mode != "zones") return null;
        var total = Zones.Sum(z => Math.Max(0, z.Share));
        if (total <= 1e-9) return null;
        return Zones.Where(z => z.Share > 0).Select(z => new Runs.Zone(length * z.Share / total, z.Step)).ToList();
    }
}

/// <summary>
/// Where a type's stirrups stand on an element and how they draw: along a beam's axis, each a rectangle perpendicular to
/// it inside the cover; up a column, each horizontal. A view draws their projections as it lies to the axis: a
/// cross-section shows the rectangle, a longitudinal section the lines across, a plan the lines over the beam.
/// </summary>
public static class TypeStirrups
{
    /// <summary>An element's stirrups laid: the stations along its axis, the loop at each (four corners, metres), the axis in space, the loop's axis sizes.</summary>
    public sealed record Laid(Entity Element, StirrupSpec Spec, List<double> Stations, Func<double, Vec3[]> LoopAt, Vec3 Start, Vec3 Dir, double Length, double A, double B)
    {
        /// <summary>The schedule's length of one stirrup: the rectangle on the bar's axis and its two 135° hooks.</summary>
        public double BarLength => 2 * (A + B) + 2 * RebarShapes.HookLength(Spec.Diameter, 135);
        public string Label => $"{Stations.Count}{RebarShapes.Dia(Spec.Diameter)}/{Runs.StepText(Length, Spec.Step, Spec.RunZones(Length))}";
    }

    public static StirrupSpec? SpecOf(IModelView m, Entity e) => m.RelatingOf(e.Id, IfcClass.RelDefinesByType) is { } type ? StirrupSpec.Of(type) : null;

    public static Laid? Of(IModelView m, Entity e)
    {
        if (SpecOf(m, e) is not { Enabled: true } spec) return null;
        if (e.Class == IfcClass.Beam)
        {
            var (section, _) = Beams.Spec(m, e);
            if (section.Kind != "rectangle") return null;
            var c = PathCurve.Of(e);
            if (c.Length < 1e-6) return null;
            var (a, b) = Inside(section.Width, section.Depth, spec);
            if (a <= 0 || b <= 0) return null;
            var zc = Beams.Top(m, e) - section.Depth / 2;
            var stations = Runs.Positions(c.Length, spec.Step, spec.RunZones(c.Length));
            Vec3[] Loop(double s)
            {
                var p = c.PointAt(s);
                var n = c.TangentAt(s).Perp * (a / 2);
                return [new Vec3(p.X - n.X, p.Y - n.Y, zc - b / 2), new Vec3(p.X + n.X, p.Y + n.Y, zc - b / 2), new Vec3(p.X + n.X, p.Y + n.Y, zc + b / 2), new Vec3(p.X - n.X, p.Y - n.Y, zc + b / 2)];
            }
            var t0 = c.TangentAt(0);
            return new Laid(e, spec, stations, Loop, new Vec3(c.Start.X, c.Start.Y, zc), new Vec3(t0.X, t0.Y, 0), c.Length, a, b);
        }
        if (e.Class == IfcClass.Column)
        {
            var (section, _, height) = Columns.Spec(m, e);
            if (section.Kind != "rectangle" || height <= 1e-6) return null;
            var (a, b) = Inside(section.Width, section.Depth, spec);
            if (a <= 0 || b <= 0) return null;
            var at = e.Attrs["at"] is JsonArray q && q.Count >= 2 ? new Vec2(Json.Number(q[0]) ?? 0, Json.Number(q[1]) ?? 0) : default;
            var baseZ = (Spatial.StoreyOf(m, e.Id)?.Num("elevation") ?? 0) + e.Num("baseOffset");
            var rot = e.Num("rotation") * Math.PI / 180;
            var u = new Vec2(Math.Cos(rot), Math.Sin(rot)) * (a / 2);
            var v = new Vec2(-Math.Sin(rot), Math.Cos(rot)) * (b / 2);
            var stations = Runs.Positions(height, spec.Step, spec.RunZones(height));
            Vec3[] Loop(double s)
            {
                var z = baseZ + s;
                return [(at - u - v).At(z), (at + u - v).At(z), (at + u + v).At(z), (at - u + v).At(z)];
            }
            return new Laid(e, spec, stations, Loop, at.At(baseZ), Vec3.UnitZ, height, a, b);
        }
        return null;
    }

    /// <summary>The stirrup's axis rectangle inside the section: the cover and half the bar off each face.</summary>
    static (double A, double B) Inside(double width, double depth, StirrupSpec s) => (width - 2 * s.Cover - s.Diameter, depth - 2 * s.Cover - s.Diameter);

    /// <summary>
    /// The stirrups of every beam and column a view shows, each loop projected on the view (its distinct edges), those in
    /// front of the eye and within the view's depth; in plans and elevations only when the view is the structure's.
    /// A longitudinal view gets the label ("41Ø8/150") with the mark's balloon over the middle.
    /// </summary>
    public static IEnumerable<ViewMark> Marks(ViewContext c, RebarSettings st)
    {
        var m = c.Model;
        var kind = c.View.Str("kind") ?? "plan";
        if (kind is "plan" or "elevation" && !string.Equals(c.View.Str("discipline"), "Structure", StringComparison.OrdinalIgnoreCase)) yield break;
        if (kind is not ("plan" or "elevation" or "section")) yield break;
        var limit = kind == "section" ? c.View.NumOrNull("depth") ?? (AcrossSections.Is(c.View) ? c.View.NumOrNull("margin") ?? AcrossSections.DefaultMargin : double.PositiveInfinity) : double.PositiveInfinity;
        var hidden = (c.View.Attrs["hiddenLayers"] as JsonArray)?.Select(x => (string?)x).OfType<string>().ToHashSet() ?? [];
        if (hidden.Contains(RebarShapes.StirrupLayer)) yield break;
        var storey = c.View.Str("storeyId");
        foreach (var e in m.Entities)
        {
            if (e.Class is not (IfcClass.Beam or IfcClass.Column) || Of(m, e) is not { } laid) continue;
            // A plan draws its own storey's, above or below its cut (a beam under the slab stands over it); a section what is in front of it, within its depth.
            var plan = kind == "plan";
            if (plan && Spatial.StoreyOf(m, e.Id)?.Id != storey) continue;
            var d = laid.Spec.Diameter;
            var color = st.ColorByDiameter ? RebarShapes.ColorOf(d) : "#000000";
            var width = Math.Max(0.25, d * 1000 / c.Scale);
            var mk = new ViewMark(e.Id);
            var drawn = new HashSet<(long, long, long, long)>();
            var any = false;
            foreach (var s in laid.Stations)
            {
                var loop = laid.LoopAt(s);
                var depths = loop.Select(c.Frame.Depth).ToList();
                if (!plan && (depths.Max() < -1e-6 || depths.Min() > limit + 1e-6)) continue;
                var pts = loop.Select(c.Map).ToArray();
                // Seen across (the loop a rectangle on the view): the stirrup as it is bent, its corners rounded and its two hooks at the top corner, once per place.
                if (Enumerable.Range(0, 4).All(i => (pts[(i + 1) % 4] - pts[i]).Length > 1e-6) && Math.Abs(Polygon2.SignedArea(pts)) > 1e-8)
                {
                    static long K(double x) => (long)Math.Round(x * 1e5);
                    // From the bottom left corner, x along the bottom, y up: the shape's hooked corner (its top left) comes top left.
                    var lows = Enumerable.Range(0, 4).OrderBy(i => pts[i].Y).Take(2).ToList();
                    var i0 = pts[lows[0]].X <= pts[lows[1]].X ? lows[0] : lows[1];
                    var q0 = pts[i0];
                    var (n1, n2) = (pts[(i0 + 1) % 4], pts[(i0 + 3) % 4]);
                    var (left, up) = Math.Abs(n1.Y - q0.Y) <= Math.Abs(n2.Y - q0.Y) ? (n1, n2) : (n2, n1);
                    var key = (K(q0.X), K(q0.Y), K(left.X), K(up.Y));
                    if (drawn.Contains(key)) continue;
                    drawn.Add(key);
                    var (a, b) = ((left - q0).Length, (up - q0).Length);
                    var (u, v) = ((left - q0) * (1 / a), (up - q0) * (1 / b));
                    var spec = new RebarSpec("stirrup", new Dictionary<string, double> { ["a"] = a, ["b"] = b }, null, d, null, null);
                    var chain = RebarShapes.Chain(RebarShapes.Segments(spec)).Select(q => q0 + u * q.X + v * q.Y).ToList();
                    mk.Polyline(chain, false, RebarShapes.StirrupLayer, width, color);
                    mk.Snaps.AddRange([q0, left, up, left + (up - q0)]);
                    any = true;
                    continue;
                }
                for (var i = 0; i < 4; i++)
                {
                    var (p, q) = (pts[i], pts[(i + 1) % 4]);
                    if ((q - p).Length < 1e-6) continue;
                    static long K(double x) => (long)Math.Round(x * 1e5);
                    var key = (K(p.X), K(p.Y), K(q.X), K(q.Y));
                    var back = (K(q.X), K(q.Y), K(p.X), K(p.Y));
                    if (drawn.Contains(key) || drawn.Contains(back)) continue;
                    drawn.Add(key);
                    mk.Line(p, q, RebarShapes.StirrupLayer, width, color);
                    any = true;
                }
            }
            if (!any) continue;
            // Seen along its length: the label over the middle, the mark's balloon beside it.
            var p0 = c.Map(laid.Start);
            var p1 = c.Map(laid.Start + laid.Dir * laid.Length);
            if ((p1 - p0).Length > 4 * laid.Spec.Step)
            {
                var u = (p1 - p0).Normalized();
                var angle = Math.Atan2(u.Y, u.X) * 180 / Math.PI;
                var n = u.Perp;
                if (angle > 90 || angle <= -90) { angle += 180; n = -n; }
                var mid = (p0 + p1) * 0.5;
                var half = (laid.Element.Class == IfcClass.Beam ? laid.B : laid.A) / 2;
                var textAt = mid + n * (half + 4 * c.Mm);
                mk.Text(new PenText(textAt, laid.Label, st.TextHeight, angle, "middle", "bottom"), color);
                if (laid.Spec.Mark > 0)
                {
                    var r = st.Balloon / 2 * c.Mm;
                    var lab = textAt - u * (laid.Label.Length * st.TextHeight * 0.35 * c.Mm + r + 1.5 * c.Mm) + n * (st.TextHeight * 0.5 * c.Mm);
                    mk.Polyline(ViewMark.Circle(lab, r, 32), true, RebarShapes.AnnotationLayer, 0.35, color);
                    mk.Text(new PenText(lab, laid.Spec.Mark.ToString(CultureInfo.InvariantCulture), st.TextHeight, 0, "middle", "middle", true), color);
                }
            }
            yield return mk;
        }
    }

    /// <summary>The schedule's entries for every element's configured stirrups: one per element, under the type's mark.</summary>
    public static IEnumerable<RebarSchedule.Entry> Entries(IModelView m)
    {
        foreach (var e in m.Entities)
            if (e.Class is IfcClass.Beam or IfcClass.Column && Of(m, e) is { } laid)
                yield return new RebarSchedule.Entry(laid.Spec.Mark, laid.Spec.Diameter, "Stirrup", laid.Stations.Count, laid.BarLength);
    }

    /// <summary>The highest mark a type's stirrups hold.</summary>
    public static int HighestMark(IModelView m) => m.OfClass(IfcClass.BeamType).Concat(m.OfClass(IfcClass.ColumnType)).Select(t => StirrupSpec.Of(t)?.Mark ?? 0).DefaultIfEmpty(0).Max();

    /// <summary>A type's stirrups set from a command: {id, enabled?, diameter?, cover?, mode?, step?, zones?: [{share, step}] × 3, mark?}.</summary>
    public static void Set(ICommandContext ctx, JsonObject a)
    {
        var type = ctx.Model.Get((string?)a["id"] ?? "") is { Class: IfcClass.BeamType or IfcClass.ColumnType } t ? t : throw new CommandException("Stirrups are set on a beam or a column type.");
        var now = StirrupSpec.Of(type) ?? StirrupSpec.Default();
        var enabled = a["enabled"] is JsonValue ev && ev.TryGetValue<bool>(out var en) ? en : now.Enabled;
        var d = Json.Number(a["diameter"]) ?? now.Diameter;
        if (d < 0.004 || d > 0.02) throw new CommandException("A stirrup is between 4 and 20 mm thick.");
        var cover = Json.Number(a["cover"]) ?? now.Cover;
        if (cover < 0 || cover > 0.2) throw new CommandException("The cover is between 0 and 200 mm.");
        var mode = (string?)a["mode"] ?? now.Mode;
        if (!StirrupSpec.Modes.Contains(mode)) throw new CommandException("Stirrups are laid at one step (uniform) or in zones.");
        var step = Json.Number(a["step"]) ?? now.Step;
        if (step < 0.01) throw new CommandException("The step is 10 mm at least.");
        var zones = now.Zones;
        if (a["zones"] is JsonArray za)
        {
            zones = za.OfType<JsonObject>().Select(z => (Math.Max(0, Json.Number(z["share"]) ?? 0), Json.Number(z["step"]) ?? step)).ToList();
            if (zones.Count == 0 || zones.Any(z => z.Item2 < 0.01)) throw new CommandException("Each zone takes a share of the length and a step of 10 mm at least.");
        }
        var mark = (int)(Json.Number(a["mark"]) ?? now.Mark);
        if (enabled && mark <= 0) mark = Math.Max(Rebars.NextMark(ctx.Model), HighestMark(ctx.Model) + 1);
        var attrs = type.CloneAttrs();
        attrs[StirrupSpec.Key] = new StirrupSpec(enabled, d, cover, mode, step, zones, mark).ToJson();
        ctx.Replace(type.With(attrs: attrs));
    }
}
