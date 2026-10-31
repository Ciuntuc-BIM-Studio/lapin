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
/// (a third by default) at its own step. A column's may be one round the perimeter alone, with a diamond inscribed
/// in it (its corners at the sides' middles), or with two inside, one each way (decision 218). Kept on the type as
/// <c>stirrups</c>; the free edition draws them in 2D only.
/// </summary>
public sealed record StirrupSpec(bool Enabled, double Diameter, double Cover, string Mode, double Step, IReadOnlyList<(double Share, double Step)> Zones, int Mark,
    string Pattern = "single", IReadOnlyList<int>? InnerMarks = null)
{
    public const string Key = "stirrups";
    public static readonly string[] Modes = ["uniform", "zones"];
    public static readonly string[] Patterns = ["single", "diamond", "cross"];

    public static StirrupSpec Default() => new(false, 0.008, 0.025, "uniform", 0.15, [(1.0 / 3, 0.1), (1.0 / 3, 0.2), (1.0 / 3, 0.1)], 0);

    public static StirrupSpec? Of(Entity type) => type.Attrs[Key] is JsonObject o ? From(o) : null;

    public static StirrupSpec From(JsonObject o)
    {
        var d = Default();
        var zones = (o["zones"] as JsonArray)?.OfType<JsonObject>().Select(z => (Json.Number(z["share"]) ?? 1.0 / 3, Json.Number(z["step"]) ?? d.Step)).ToList();
        var inner = (o["innerMarks"] as JsonArray)?.Select(x => (int)(Json.Number(x) ?? 0)).ToList();
        return new StirrupSpec(o["enabled"] is JsonValue ev && ev.TryGetValue<bool>(out var en) && en, Json.Number(o["diameter"]) ?? d.Diameter, Json.Number(o["cover"]) ?? d.Cover,
            (string?)o["mode"] is { } md && Modes.Contains(md) ? md : d.Mode, Json.Number(o["step"]) ?? d.Step, zones is { Count: > 0 } ? zones : d.Zones, (int)(Json.Number(o["mark"]) ?? 0),
            (string?)o["pattern"] is { } pt && Patterns.Contains(pt) ? pt : "single", inner);
    }

    public JsonObject ToJson() => new()
    {
        ["enabled"] = Enabled, ["diameter"] = Diameter, ["cover"] = Cover, ["mode"] = Mode, ["step"] = Step, ["mark"] = Mark, ["pattern"] = Pattern,
        ["zones"] = new JsonArray(Zones.Select(z => (JsonNode)new JsonObject { ["share"] = Math.Round(z.Share, 6), ["step"] = z.Step }).ToArray()),
        ["innerMarks"] = new JsonArray((InnerMarks ?? []).Select(x => (JsonNode)x).ToArray()),
    };

    /// <summary>The zones as a run has them along a length: each its share of it at its step; none for one step throughout.</summary>
    public List<Runs.Zone>? RunZones(double length)
    {
        if (Mode != "zones") return null;
        var total = Zones.Sum(z => Math.Max(0, z.Share));
        if (total <= 1e-9) return null;
        return Zones.Where(z => z.Share > 0).Select(z => new Runs.Zone(length * z.Share / total, z.Step)).ToList();
    }

    /// <summary>How many stirrups stand at each station: one, two with a diamond, three with the two inside.</summary>
    public int LoopsPerStation => Pattern switch { "diamond" => 2, "cross" => 3, _ => 1 };
}

/// <summary>
/// Where a type's stirrups stand on an element and how they draw: along a beam's axis, each a rectangle perpendicular to
/// it inside the cover; up a column, each horizontal, with the inner ones its pattern asks. A view draws their projections
/// as it lies to the axis: a cross-section shows each as it is bent, a longitudinal section the lines across, a plan the lines over the beam.
/// </summary>
public static class TypeStirrups
{
    /// <summary>One stirrup of a station: its four corners in space, its axis sizes, and which it is (0 the perimeter's, then the diamond or the inner ones each way), for the schedule.</summary>
    public sealed record Loop(Vec3[] Corners, double A, double B, int Kind);

    /// <summary>An element's stirrups laid: the stations along its axis, the loops at each, the axis in space.</summary>
    public sealed record Laid(Entity Element, StirrupSpec Spec, List<double> Stations, Func<double, List<Loop>> LoopsAt, Vec3 Start, Vec3 Dir, double Length, double A, double B)
    {
        /// <summary>The schedule's length of a loop: its sides on the bar's axis and its two 135° hooks.</summary>
        public double BarLength(Loop l) => 2 * (l.A + l.B) + 2 * RebarShapes.HookLength(Spec.Diameter, 135);
        public string Label => $"{Stations.Count}{RebarShapes.Dia(Spec.Diameter)}/{Runs.StepText(Length, Spec.Step, Spec.RunZones(Length))}";
        /// <summary>The mark of a loop kind: the perimeter's the type's, the inner ones theirs.</summary>
        public int MarkOf(int kind) => kind == 0 ? Spec.Mark : Spec.InnerMarks is { } im && kind - 1 < im.Count ? im[kind - 1] : 0;
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
            List<Loop> Loops(double s)
            {
                var p = c.PointAt(s);
                var n = c.TangentAt(s).Perp * (a / 2);
                return [new Loop([new Vec3(p.X - n.X, p.Y - n.Y, zc - b / 2), new Vec3(p.X + n.X, p.Y + n.Y, zc - b / 2), new Vec3(p.X + n.X, p.Y + n.Y, zc + b / 2), new Vec3(p.X - n.X, p.Y - n.Y, zc + b / 2)], a, b, 0)];
            }
            var t0 = c.TangentAt(0);
            return new Laid(e, spec, stations, Loops, new Vec3(c.Start.X, c.Start.Y, zc), new Vec3(t0.X, t0.Y, 0), c.Length, a, b);
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
            var (ux, uy) = (new Vec2(Math.Cos(rot), Math.Sin(rot)), new Vec2(-Math.Sin(rot), Math.Cos(rot)));
            var stations = Runs.Positions(height, spec.Step, spec.RunZones(height));
            // The inner stirrups wrap the middle bars of two facing sides: as thick as a bar and two stirrups, and a little.
            var bar = ColumnBars.SpecOf(m, e) is { Enabled: true } cb ? cb.Diameter : 0.016;
            var inner = bar + 2 * spec.Diameter + 0.004;
            Vec3[] Rect(double w, double h, double z) { var (u, v) = (ux * (w / 2), uy * (h / 2)); return [(at - u - v).At(z), (at + u - v).At(z), (at + u + v).At(z), (at - u + v).At(z)]; }
            List<Loop> Loops(double s)
            {
                var z = baseZ + s;
                var loops = new List<Loop> { new(Rect(a, b, z), a, b, 0) };
                switch (spec.Pattern)
                {
                    case "diamond":
                    {
                        // Its corners at the perimeter stirrup's sides' middles: a rhombus whose side is the half-diagonal.
                        var (u, v) = (ux * (a / 2), uy * (b / 2));
                        var side = Math.Sqrt(a * a + b * b) / 2;
                        loops.Add(new Loop([(at - u).At(z), (at - v).At(z), (at + u).At(z), (at + v).At(z)], side, side, 1));
                        break;
                    }
                    case "cross":
                        loops.Add(new Loop(Rect(a, inner, z), a, inner, 1));
                        loops.Add(new Loop(Rect(inner, b, z), inner, b, 2));
                        break;
                }
                return loops;
            }
            return new Laid(e, spec, stations, Loops, at.At(baseZ), Vec3.UnitZ, height, a, b);
        }
        return null;
    }

    /// <summary>The stirrup's axis rectangle inside the section: the cover and half the bar off each face.</summary>
    static (double A, double B) Inside(double width, double depth, StirrupSpec s) => (width - 2 * s.Cover - s.Diameter, depth - 2 * s.Cover - s.Diameter);

    /// <summary>
    /// The stirrups of every beam and column a view shows, each loop projected on the view: seen across, as it is bent (its
    /// corners rounded, its two hooks at a corner); seen along, its distinct edges; those in front of the eye and within the
    /// view's depth; in plans the storey's own, and in plans and elevations only when the view is the structure's.
    /// A longitudinal view gets the label ("41Ø8/150") with the mark's balloon over the middle.
    /// </summary>
    public static IEnumerable<ViewMark> Marks(ViewContext c, RebarSettings st)
    {
        var m = c.Model;
        if (!Rebar3D.Shows(c, out var limit, out var plan)) yield break;
        var hidden = (c.View.Attrs["hiddenLayers"] as JsonArray)?.Select(x => (string?)x).OfType<string>().ToHashSet() ?? [];
        if (hidden.Contains(RebarShapes.StirrupLayer)) yield break;
        var storey = c.View.Str("storeyId");
        foreach (var e in m.Entities)
        {
            if (e.Class is not (IfcClass.Beam or IfcClass.Column) || Of(m, e) is not { } laid) continue;
            // A plan draws its own storey's, above or below its cut (a beam under the slab stands over it); a section what is in front of it, within its depth.
            if (plan && Spatial.StoreyOf(m, e.Id)?.Id != storey) continue;
            var d = laid.Spec.Diameter;
            var color = st.ColorByDiameter ? RebarShapes.ColorOf(d) : "#000000";
            var width = Math.Max(0.25, d * 1000 / c.Scale);
            var mk = new ViewMark(e.Id);
            var drawn = new HashSet<(long, long, long, long)>();
            var any = false;
            static long K(double x) => (long)Math.Round(x * 1e5);
            foreach (var s in laid.Stations)
                foreach (var loop in laid.LoopsAt(s))
                {
                    var depths = loop.Corners.Select(c.Frame.Depth).ToList();
                    if (!plan && (depths.Max() < -1e-6 || depths.Min() > limit + 1e-6)) continue;
                    var pts = loop.Corners.Select(c.Map).ToArray();
                    // Seen across (the loop a parallelogram on the view): the stirrup as it is bent, from its lowest corner, its hooked corner up and to the left; once per place.
                    if (Enumerable.Range(0, 4).All(i => (pts[(i + 1) % 4] - pts[i]).Length > 1e-6) && Math.Abs(Polygon2.SignedArea(pts)) > 1e-8)
                    {
                        var i0 = Enumerable.Range(0, 4).OrderBy(i => pts[i].Y).ThenBy(i => pts[i].X).First();
                        var q0 = pts[i0];
                        var (n1, n2) = (pts[(i0 + 1) % 4], pts[(i0 + 3) % 4]);
                        var (along, up) = n1.X >= n2.X ? (n1, n2) : (n2, n1);
                        var key = (K(q0.X), K(q0.Y), K(along.X), K(up.Y));
                        if (drawn.Contains(key)) continue;
                        drawn.Add(key);
                        var (a, b) = ((along - q0).Length, (up - q0).Length);
                        var (u, v) = ((along - q0) * (1 / a), (up - q0) * (1 / b));
                        var spec = new RebarSpec("stirrup", new Dictionary<string, double> { ["a"] = a, ["b"] = b }, null, d, null, null);
                        var chain = RebarShapes.Chain(RebarShapes.Segments(spec)).Select(q => q0 + u * q.X + v * q.Y).ToList();
                        mk.Polyline(chain, false, RebarShapes.StirrupLayer, width, color);
                        mk.Snaps.AddRange([q0, along, up, along + (up - q0)]);
                        any = true;
                        continue;
                    }
                    for (var i = 0; i < 4; i++)
                    {
                        var (p, q) = (pts[i], pts[(i + 1) % 4]);
                        if ((q - p).Length < 1e-6) continue;
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
                var label = laid.Spec.LoopsPerStation > 1 ? $"{laid.Spec.LoopsPerStation}×{laid.Label}" : laid.Label;
                mk.Text(new PenText(textAt, label, st.TextHeight, angle, "middle", "bottom"), color);
                if (laid.Spec.Mark > 0)
                {
                    var r = st.Balloon / 2 * c.Mm;
                    var lab = textAt - u * (label.Length * st.TextHeight * 0.35 * c.Mm + r + 1.5 * c.Mm) + n * (st.TextHeight * 0.5 * c.Mm);
                    mk.Polyline(ViewMark.Circle(lab, r, 32), true, RebarShapes.AnnotationLayer, 0.35, color);
                    mk.Text(new PenText(lab, laid.Spec.Mark.ToString(CultureInfo.InvariantCulture), st.TextHeight, 0, "middle", "middle", true), color);
                }
            }
            yield return mk;
        }
    }

    /// <summary>The schedule's entries for every element's configured stirrups: one per element and loop kind, under its mark.</summary>
    public static IEnumerable<RebarSchedule.Entry> Entries(IModelView m)
    {
        foreach (var e in m.Entities)
            if (e.Class is IfcClass.Beam or IfcClass.Column && Of(m, e) is { } laid && laid.Stations.Count > 0)
                foreach (var loop in laid.LoopsAt(laid.Stations[0]))
                    yield return new RebarSchedule.Entry(laid.MarkOf(loop.Kind), laid.Spec.Diameter, loop.Kind == 1 && laid.Spec.Pattern == "diamond" ? "Diamond stirrup" : "Stirrup", laid.Stations.Count, laid.BarLength(loop));
    }

    /// <summary>The highest mark a type's stirrups hold, inner ones included.</summary>
    public static int HighestMark(IModelView m) => m.OfClass(IfcClass.BeamType).Concat(m.OfClass(IfcClass.ColumnType)).Select(t => StirrupSpec.Of(t))
        .OfType<StirrupSpec>().SelectMany(s => (s.InnerMarks ?? []).Prepend(s.Mark)).DefaultIfEmpty(0).Max();

    /// <summary>A type's stirrups set from a command: {id, enabled?, diameter?, cover?, mode?, step?, zones?: [{share, step}] × 3, mark?, pattern? (a column: single, diamond, cross)}.</summary>
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
        var pattern = (string?)a["pattern"] ?? now.Pattern;
        if (!StirrupSpec.Patterns.Contains(pattern)) throw new CommandException("A column's stirrups are the perimeter's alone (single), with a diamond, or with two inside (cross).");
        if (type.Class == IfcClass.BeamType) pattern = "single";
        var mark = (int)(Json.Number(a["mark"]) ?? now.Mark);
        var inner = new List<int>(now.InnerMarks ?? []);
        if (enabled)
        {
            var next = Math.Max(Rebars.NextMark(ctx.Model), Math.Max(HighestMark(ctx.Model), ColumnBars.HighestMark(ctx.Model)) + 1);
            if (mark <= 0) mark = next++;
            var need = pattern switch { "diamond" => 1, "cross" => 2, _ => 0 };
            while (inner.Count < need) inner.Add(next++);
        }
        var attrs = type.CloneAttrs();
        attrs[StirrupSpec.Key] = new StirrupSpec(enabled, d, cover, mode, step, zones, mark, pattern, inner).ToJson();
        ctx.Replace(type.With(attrs: attrs));
    }
}
