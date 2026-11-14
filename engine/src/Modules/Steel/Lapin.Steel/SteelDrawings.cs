using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Steel;

/// <summary>
/// Steel's schedules and drawings (docs/plan-steel.md stage 5, decision 276): the Steel members list (marks by profile,
/// length and ends, counts, weights, paint area, totals by hall), a hall's anchor plan (its column bases from above at
/// 1:50 with their anchors dimensioned to the grid, each base marked by its type, and the table of the types) and its frame
/// elevation (a section across a frame at 1:50, with its span and heights). The drawings are views like any other, so they
/// go on sheets; what steel adds to them are their annotations, drawn from the hall as it is.
/// </summary>
public static class SteelDrawings
{
    /// <summary>The key an anchor plan keeps its hall under; a frame elevation's hall and frame.</summary>
    public const string AnchorKey = "steelAnchors", FrameKey = "steelFrame", FrameIndexKey = "steelFrameIndex";

    public static void Register(IModuleRegistry r)
    {
        // A hall's anchor plan: {id (the hall, or one of its members or connections)} → {id (the view)}; the one it has, if any.
        r.Command("anchorPlan", "Anchor plan", (ctx, a) => new JsonObject { ["id"] = AnchorPlan(ctx, (string?)a["id"]) });

        // A frame's elevation: {id (the hall, or one of its members), frame? (0…, a typical one when not given)} → {id (the view)}.
        r.Command("frameView", "Frame elevation", (ctx, a) => new JsonObject { ["id"] = FrameView(ctx, (string?)a["id"], Json.Number(a["frame"]) is { } fi ? (int)fi : null) });

        // Both as element view roles (decision 578): a drawing rule asks for a hall's frame elevation or its anchor plan by them.
        r.Service<IElementViewRoles>(new SteelViewRoles());
        r.Service<IScheduleSource>(new SteelMembersSchedule());
        r.Service<IViewAnnotations>(new SteelViewMarks());
    }

    /// <summary>The hall's anchor plan (the one it has, else a new one): its id.</summary>
    public static string AnchorPlan(ICommandContext ctx, string? id)
    {
        var hall = HallFor(ctx.Model, id);
        if (ctx.Model.OfClass(Views.Class).FirstOrDefault(v => v.Str(AnchorKey) == hall.Id) is { } had) return had.Id;
        var storey = StoreyOf(ctx.Model, hall);
        var attrs = new JsonObject
        {
            ["kind"] = "plan", ["scale"] = 50, ["storeyId"] = storey.Id, ["cutHeight"] = 0.12, ["discipline"] = "Structure", [AnchorKey] = hall.Id,
        };
        var v = new Entity(ctx.NewId(), Views.Class, $"Anchor plan · {hall.Name}", attrs);
        ctx.Add(v);
        return v.Id;
    }

    /// <summary>A frame's elevation (the one there is, else a new one): the frame asked, else the member's, else a typical one. Its id.</summary>
    public static string FrameView(ICommandContext ctx, string? id, int? frame)
    {
        var hall = HallFor(ctx.Model, id);
        var s = Halls.SpecOf(hall);
        var i = frame ?? (id is { } mid && ctx.Model.Get(mid)?.Str(Halls.RoleKey) is { } role && Halls.FrameOf(role) is { } of ? of : s.Bays.Count >= 2 ? 1 : 0);
        if (i < 0 || i > s.Bays.Count) throw new CommandException($"The hall has frames 1 to {s.Bays.Count + 1}.");
        if (ctx.Model.OfClass(Views.Class).FirstOrDefault(v => v.Str(FrameKey) == hall.Id && (int)v.Num(FrameIndexKey) == i) is { } had) return had.Id;
        var y = s.Stations[i];
        // Its line a little before the frame, looking at it along the hall (a section looks to the left of its line).
        var (p0, p1) = (Halls.Plan(s, -2.5, y - 0.5), Halls.Plan(s, s.Span + 2.5, y - 0.5));
        var attrs = new JsonObject { ["kind"] = "section", ["scale"] = 50, ["discipline"] = "Structure", ["depth"] = 1.0, [FrameKey] = hall.Id, [FrameIndexKey] = i };
        SectionPaths.Set(ctx.Model, ctx.Services, attrs, new JsonObject { ["start"] = new JsonArray(p0.X, p0.Y), ["end"] = new JsonArray(p1.X, p1.Y) }, create: true);
        var v = new Entity(ctx.NewId(), Views.Class, $"Frame {i + 1} · {hall.Name}", attrs);
        ctx.Add(v);
        return v.Id;
    }

    /// <summary>The hall an id names (the hall, or a member or connection of it); with no id, the model's only hall.</summary>
    public static Entity HallFor(IModelView m, string? id)
    {
        if (id != null && m.Get(id) is { } e)
        {
            if (e.Class == Halls.Class) return e;
            if (Halls.Of(m, id) is { } h) return h;
        }
        var all = m.OfClass(Halls.Class).ToList();
        return all.Count == 1 ? all[0] : throw new CommandException(all.Count == 0 ? "The model has no portal frame hall." : "Select a hall, or one of its members.");
    }

    static Entity StoreyOf(IModelView m, Entity hall) =>
        (string?)hall.Attrs["storeyId"] is { } sid && m.Get(sid) is { Class: IfcClass.BuildingStorey } st ? st
        : Halls.Members(m, hall.Id).Values.Select(x => Spatial.StoreyOf(m, x.Id)).FirstOrDefault(x => x != null) ?? Spatial.Storeys(m).First();

    // ------------------------------------------------------------ column bases

    /// <summary>A column base as the anchor plan shows it: where its column stands (in the hall's x and y), its anchors (hall x, y), its type's key and description.</summary>
    public sealed record BaseInfo(Entity Node, double X, double Y, List<Vec2> Anchors, string Key, string Kind, string Plate, string Anchor, string Grade, string Grout);

    static double Hx(HallSpec s, Vec2 p) => Vec2.Dot(p - new Vec2(s.X, s.Y), new Vec2(Math.Cos(s.Angle * Math.PI / 180), Math.Sin(s.Angle * Math.PI / 180)));
    static double Hy(HallSpec s, Vec2 p) => Vec2.Dot(p - new Vec2(s.X, s.Y), new Vec2(-Math.Sin(s.Angle * Math.PI / 180), Math.Cos(s.Angle * Math.PI / 180)));

    /// <summary>A hall's column bases, each with its anchors where they come through the plate (their tops) and its type; ordered by grid.</summary>
    public static List<BaseInfo> Bases(IModelView m, Entity hall)
    {
        var s = Halls.SpecOf(hall);
        var o = new List<BaseInfo>();
        foreach (var node in Halls.Nodes(m, hall.Id).Values.Where(n => SteelNodes.KindOf(n) == "base"))
        {
            var js = SteelNodes.MembersOf(node);
            if (js.Count == 0 || m.Get(js[0].Id) is not { } col || MemberAxes.Of(m, col) is not { } ax) continue;
            var b = SteelNodes.Build(m, node);
            var anchors = b.Parts.Where(p => SteelNodes.P(p.Properties ?? [], "Part") == "Anchor").Select(p =>
            {
                var top = p.Solid.Vertices.Max(v => v.Z);
                var ring = p.Solid.Vertices.Where(v => v.Z > top - 1e-6).ToList();
                var c = new Vec2(ring.Average(v => v.X), ring.Average(v => v.Y));
                return new Vec2(Hx(s, c), Hy(s, c));
            }).ToList();
            string Size(string part) => b.Parts.Where(p => SteelNodes.P(p.Properties ?? [], "Part") == part).Select(p => SteelNodes.P(p.Properties!, "Size")).FirstOrDefault() ?? "";
            var plate = b.Parts.FirstOrDefault(p => p.Name == "Base plate") is { } bp ? SteelNodes.P(bp.Properties!, "Size") : "";
            var anchor = anchors.Count > 0 ? $"{anchors.Count} × {Size("Anchor")} {b.Spec.Anchor}" : "";
            var grout = Math.Round(b.Spec.Grout * 1000).ToString(CultureInfo.InvariantCulture);
            var key = string.Join("|", b.Spec.Base, plate, anchor, b.Spec.Grade, grout);
            o.Add(new BaseInfo(node, Hx(s, ax.Start.Xy), Hy(s, ax.Start.Xy), anchors, key, b.Spec.Base, plate, anchor, b.Spec.Grade, grout));
        }
        return o.OrderBy(x => Math.Round(x.Y, 3)).ThenBy(x => Math.Round(x.X, 3)).ToList();
    }

    /// <summary>The bases' types, marked B1, B2… in the order of their first base along the grid: key → mark.</summary>
    public static Dictionary<string, string> BaseMarks(IEnumerable<BaseInfo> bases)
    {
        var o = new Dictionary<string, string>();
        foreach (var b in bases) if (!o.ContainsKey(b.Key)) o[b.Key] = $"B{o.Count + 1}";
        return o;
    }

    static string Mm(double m) => Math.Round(m * 1000).ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// An anchor plan's annotations: each anchor a circle with its cross; each base its mark and two short chains, across and
    /// along the hall, from the grid axis through its anchors; the grid's bays and span; the table of the base types.
    /// </summary>
    internal static IEnumerable<ViewMark> AnchorMarks(ViewContext c)
    {
        if (c.View.Str(AnchorKey) is not { } hallId || c.Model.Get(hallId) is not { Class: Halls.Class } hall) yield break;
        var s = Halls.SpecOf(hall);
        var level = StoreyOf(c.Model, hall).Num("elevation");
        var mm = c.Mm;
        var text = Lapin.Drawing.Standards.ResolveText(c.Book, []);
        const string ink = "#000000", dim = "#1f4e8c";
        Vec2 P(double x, double y) => c.Map(Halls.Plan(s, x, y), level);
        var bases = Bases(c.Model, hall);
        var marks = BaseMarks(bases);
        var mk = new ViewMark(hallId);
        // A chain of dimensions in the hall's own x (along: false) or y (along: true), at the line given by the other.
        void Chain(IEnumerable<double> values, bool along, double at, string color, string prefix = "")
        {
            var v = values.Select(x => Math.Round(x, 4)).Distinct().Order().ToList();
            if (v.Count < 2) return;
            Vec2 Q(double t) => along ? P(at, t) : P(t, at);
            var (a, b) = (Q(v[0]), Q(v[^1]));
            mk.Line(a, b, "DIMENSION", 0.18, color);
            var dir = (b - a).Normalized();
            var tick = new Vec2(dir.X + dir.Perp.X, dir.Y + dir.Perp.Y).Normalized() * (1.2 * mm);
            var raw = Math.Atan2(dir.Y, dir.X) * 180 / Math.PI;
            var angle = Lapin.Drawing.Standards.ReadableDeg(raw);
            // Turned to read, a value past the far end runs back from its anchor: it is anchored at its end.
            var flipped = Math.Abs(((raw - angle) % 360 + 540) % 360 - 180) > 90;
            foreach (var t in v) { var q = Q(t); mk.Line(q - tick, q + tick, "DIMENSION", 0.25, color); }
            // Short pieces side by side: their values on either side of the line in turn, so they do not run into each other.
            // A piece too short for its value has it past its far end, along the line.
            for (var i = 0; i + 1 < v.Count; i++)
            {
                var below = v.Count > 2 && i % 2 == 1;
                var label = prefix + Mm(v[i + 1] - v[i]);
                var room = (Q(v[i + 1]) - Q(v[i])).Length / mm;
                var place = room < 1.2 * label.Length * text.Height * 0.8 * 0.6 && i == v.Count - 2
                    ? (Q(v[i + 1]) + dir * (3 * mm), flipped ? "end" : "start")
                    : ((Q(v[i]) + Q(v[i + 1])) * 0.5, "middle");
                mk.Text(new PenText(place.Item1 + dir.Perp * ((below ? -1 : 1) * 0.8 * mm), label, text.Height * 0.8, angle, place.Item2, below ? "top" : "bottom", false, text.Font), color);
            }
        }
        foreach (var b in bases)
        {
            var d = SteelNodes.Spec(c.Model, b.Node).Bolt;
            foreach (var p in b.Anchors)
            {
                var q = P(p.X, p.Y);
                mk.Polyline(ViewMark.Circle(q, d / 2, 16), true, "ANNOTATION", 0.25, ink);
                mk.Line(q - new Vec2(d, 0), q + new Vec2(d, 0), "ANNOTATION", 0.13, ink).Line(q - new Vec2(0, d), q + new Vec2(0, d), "ANNOTATION", 0.13, ink);
            }
            if (b.Anchors.Count == 0) continue;
            var (xs, ys) = (b.Anchors.Select(p => p.X).ToList(), b.Anchors.Select(p => p.Y).ToList());
            var outX = b.X < s.Span / 2 ? -1 : 1;
            var outY = b.Y < s.Length / 2 ? -1 : 1;
            var reachX = xs.Max(x => Math.Abs(x - b.X)) + 0.2;
            var reachY = ys.Max(y => Math.Abs(y - b.Y)) + 0.2;
            // Anchors set symmetrically about the axis: their distance from it once, ±; otherwise each.
            void Across(List<double> ps, double axis, bool along, double line)
            {
                if (Math.Abs(ps.Max() - axis - (axis - ps.Min())) < 1e-4 && ps.Max() - axis > 1e-4) Chain([axis, ps.Max()], along, line, dim, "±");
                else Chain([axis, .. ps], along, line, dim);
            }
            Across(xs, b.X, false, b.Y + outY * (reachY + 5 * mm));
            Across(ys, b.Y, true, b.X + outX * (reachX + 5 * mm));
            mk.Text(new PenText(P(b.X + outX * (reachX + 9 * mm), b.Y - outY * (reachY + 2 * mm)), marks[b.Key], text.Height * 1.2, 0, "middle", "middle", true, text.Font), ink);
        }
        // The grid: its bays along axis A, its span along axis 1, outside the bases' chains.
        var off = 1.2 + 14 * mm;
        Chain(s.Stations, true, -off, ink);
        Chain([0, s.Span], false, -off, ink);
        // The table of the base types, right of the hall.
        var row = Math.Max(text.Height * 2.2, 6) * mm;
        var origin = P(s.Span + off + 2, s.Length);
        double[] cols = [0, 14, 28, 44, 96, 124, 140];
        string[] head = ["Base", "Count", "Kind", "Plate", "Anchors", "Grade", "Grout"];
        var lines = new List<string[]> { head };
        foreach (var (key, mark) in marks)
        {
            var one = bases.First(b => b.Key == key);
            lines.Add([mark, bases.Count(b => b.Key == key).ToString(CultureInfo.InvariantCulture), one.Kind, one.Plate, one.Anchor, one.Grade, $"{one.Grout} mm"]);
        }
        var width = (cols[^1] + 18) * mm;
        for (var i = 0; i < lines.Count; i++)
        {
            var y = origin.Y - i * row;
            for (var k = 0; k < cols.Length; k++)
                mk.Text(new PenText(new Vec2(origin.X + (cols[k] + 1.5) * mm, y - row * 0.7), lines[i][k], text.Height * 0.8, 0, "start", "bottom", i == 0, text.Font), ink);
            mk.Line(new Vec2(origin.X, y - row), new Vec2(origin.X + width, y - row), "ANNOTATION", i == 0 ? 0.35 : 0.13, ink);
        }
        mk.Polyline([origin, new Vec2(origin.X + width, origin.Y), new Vec2(origin.X + width, origin.Y - lines.Count * row), new Vec2(origin.X, origin.Y - lines.Count * row)], true, "ANNOTATION", 0.35, ink);
        mk.Text(new PenText(new Vec2(origin.X, origin.Y + 2 * mm), $"Column bases · {hall.Name}", text.Height, 0, "start", "bottom", true, text.Font), ink);
        yield return mk;
    }

    /// <summary>A frame elevation's annotations: the span from axis to axis (and to the apex) under it, the eaves and apex heights beside it.</summary>
    internal static IEnumerable<ViewMark> FrameMarks(ViewContext c)
    {
        if (c.View.Str(FrameKey) is not { } hallId || c.Model.Get(hallId) is not { Class: Halls.Class } hall) yield break;
        var s = Halls.SpecOf(hall);
        var i = (int)c.View.Num(FrameIndexKey);
        if (i < 0 || i > s.Bays.Count) yield break;
        var level = StoreyOf(c.Model, hall).Num("elevation");
        var y = s.Stations[i];
        var mm = c.Mm;
        var text = Lapin.Drawing.Standards.ResolveText(c.Book, []);
        Vec2 P(double x, double z) { var p = Halls.Plan(s, x, y); return c.Map(new Vec3(p.X, p.Y, level + z)); }
        var mk = new ViewMark(hallId);
        void Chain(IReadOnlyList<(double X, double Z)> pts)
        {
            var (a, b) = (P(pts[0].X, pts[0].Z), P(pts[^1].X, pts[^1].Z));
            mk.Line(a, b, "DIMENSION", 0.18);
            var dir = (b - a).Normalized();
            var tick = new Vec2(dir.X + dir.Perp.X, dir.Y + dir.Perp.Y).Normalized() * (1.2 * mm);
            var angle = Lapin.Drawing.Standards.ReadableDeg(Math.Atan2(dir.Y, dir.X) * 180 / Math.PI);
            foreach (var (x, z) in pts) { var q = P(x, z); mk.Line(q - tick, q + tick, "DIMENSION", 0.25); }
            for (var k = 0; k + 1 < pts.Count; k++)
            {
                var len = (P(pts[k + 1].X, pts[k + 1].Z) - P(pts[k].X, pts[k].Z)).Length;
                mk.Text(new PenText((P(pts[k].X, pts[k].Z) + P(pts[k + 1].X, pts[k + 1].Z)) * 0.5 + dir.Perp * (0.8 * mm), Mm(len), text.Height * 0.8, angle, "middle", "bottom", false, text.Font));
            }
        }
        var under = -0.6 - 6 * mm;
        Chain(s.Mono ? [(0, under), (s.Span, under)] : [(0, under), (s.ApexX, under), (s.Span, under)]);
        Chain([(0, under - 8 * mm), (s.Span, under - 8 * mm)]);
        var side = -s.Column.Depth / 2 - 0.6 - 6 * mm;
        Chain(s.Mono ? [(side, 0), (side, s.Eaves)] : [(side, 0), (side, s.Eaves), (side, s.ApexHeight)]);
        var fmt = CultureInfo.InvariantCulture;
        mk.Text(new PenText(P(s.Span / 2, s.ApexHeight + 0.4), $"Frame {i + 1} · {s.Column.Label} / {s.Rafter.Label} · {s.Pitch.ToString("0.#", fmt)}°", text.Height, 0, "middle", "bottom", true, text.Font));
        yield return mk;
    }
}

/// <summary>Steel's annotations on its own views: an anchor plan's and a frame elevation's.</summary>
sealed class SteelViewMarks : IViewAnnotations
{
    public IEnumerable<ViewMark> Marks(ViewContext c) => SteelDrawings.AnchorMarks(c).Concat(SteelDrawings.FrameMarks(c));
}

/// <summary>
/// The Steel members list (decision 276): the steel members, beams and columns grouped by hall, then by mark — a mark for
/// each profile, length and pair of end cuts among members of a kind (C columns, R rafters, GP gable posts, P purlins, SR
/// side rails, EB eaves beams, B bracing, FB fly braces, M other members, BM beams), numbered from the heaviest profile and the longest — with
/// their count, weight per metre, each and in all, and the area to paint; a total under each hall and one for all. The
/// end cuts are the cutting list's angles: how far each end's plane turns from square, and its cope.
/// </summary>
public sealed class SteelMembersSchedule : IScheduleSource
{
    public string Kind => "steelMembers";
    public string Label => "Steel members";

    public sealed record Row(string Hall, string Mark, string Profile, double Length, string Start, string End, int Count, double KgPerM, double Each, double Paint);

    static readonly string[] Order = ["C", "R", "GP", "P", "SR", "EB", "B", "FB", "M", "BM"];

    static string Prefix(IModelView m, Entity e, Entity? hall)
    {
        if (hall != null && e.Str(Halls.RoleKey) is { } role)
            return Halls.KindOfRole(Halls.SpecOf(hall), role) switch
            {
                "column" => "C", "rafter" => "R", "post" => "GP", "purlin" => "P", "rail" => "SR", "eaves" => "EB", "brace" => "B", "fly" => "FB", _ => "M",
            };
        return e.Class switch { IfcClass.Column => "C", IfcClass.Beam => "BM", _ => "M" };
    }

    /// <summary>An end's cut as the cutting list says it: the angle its plane turns from square (— when square), and a cope.</summary>
    public static string Cut(CutPlane plane, Vec3 outward, SteelEnd end)
    {
        var deg = Math.Acos(Math.Clamp(Math.Abs(Vec3.Dot(plane.Normal, outward)), 0, 1)) * 180 / Math.PI;
        var t = deg < 0.05 ? "—" : $"{deg.ToString("0.0", CultureInfo.InvariantCulture)}°";
        return end.Coped ? $"{t} cope" : t;
    }

    public static List<Row> Rows(IModelView m, Func<string, ElementGeometry?> geometry)
    {
        var items = new List<(string Hall, string Prefix, string Profile, double Length, string Start, string End, double KgPerM, double Each, double Paint)>();
        foreach (var e in m.OfClass(SteelModule.Member).Concat(m.OfClass(IfcClass.Beam)).Concat(m.OfClass(IfcClass.Column)))
        {
            if (!MemberAxes.IsSteel(m, e) || MemberAxes.Of(m, e) is not { } ax) continue;
            var (s, t) = SteelEnd.Of(e);
            var built = SteelBody.Build(m, ax, s, t, null);
            if (built.Solid == null) continue;
            var hall = Halls.Of(m, e.Id);
            var parts = geometry(e.Id)?.Parts ?? [];
            var body = parts.Where(p => p.Key == "body").ToList();
            var volume = (body.Count > 0 ? body : parts.Take(1)).Sum(p => Measure.Volume(p.Solid)) * ax.Section.RealRatio;
            var sec = ax.Section;
            var length = Math.Round(built.Length, 3);
            var paint = Polygon2.Perimeter(sec.Outline()) * built.Length;
            items.Add((hall?.Name ?? "", Prefix(m, e, hall), sec.Label, length, Cut(built.Start, -ax.U, s), Cut(built.End, ax.U, t), sec.Area * SteelModule.SteelDensity, volume * SteelModule.SteelDensity, paint));
        }
        var o = new List<Row>();
        foreach (var hall in items.GroupBy(x => x.Hall).OrderBy(g => g.Key.Length == 0).ThenBy(g => g.Key, StringComparer.Ordinal))
            foreach (var kind in hall.GroupBy(x => x.Prefix).OrderBy(g => Array.IndexOf(Order, g.Key)))
            {
                var n = 0;
                foreach (var g in kind.GroupBy(x => (x.Profile, x.Length, x.Start, x.End)).OrderByDescending(g => Math.Round(g.First().KgPerM, 6)).ThenBy(g => g.Key.Profile, StringComparer.Ordinal).ThenByDescending(g => g.Key.Length).ThenBy(g => g.Key.Start, StringComparer.Ordinal).ThenBy(g => g.Key.End, StringComparer.Ordinal))
                    o.Add(new Row(hall.Key, $"{kind.Key}{++n}", g.Key.Profile, g.Key.Length, g.Key.Start, g.Key.End, g.Count(), g.First().KgPerM, g.Average(x => x.Each), g.Sum(x => x.Paint)));
            }
        return o;
    }

    public ScheduleGrid Grid(IQueryContext q)
    {
        static string N(double v, string f) => v.ToString(f, CultureInfo.InvariantCulture);
        var rows = Rows(q.Model, q.Geometry);
        var grid = new List<IReadOnlyList<string>>();
        foreach (var hall in rows.GroupBy(r => r.Hall))
        {
            foreach (var r in hall)
                grid.Add([r.Hall, r.Mark, r.Profile, N(r.Length * 1000, "0"), r.Start, r.End, r.Count.ToString(CultureInfo.InvariantCulture), N(r.KgPerM, "0.0"), N(r.Each, "0.0"), N(r.Each * r.Count, "0.0"), N(r.Paint, "0.00")]);
            if (rows.Select(r => r.Hall).Distinct().Count() > 1)
                grid.Add([hall.Key, "Total", "", "", "", "", hall.Sum(r => r.Count).ToString(CultureInfo.InvariantCulture), "", "", N(hall.Sum(r => r.Each * r.Count), "0.0"), N(hall.Sum(r => r.Paint), "0.00")]);
        }
        grid.Add(["Total", "", "", "", "", "", rows.Sum(r => r.Count).ToString(CultureInfo.InvariantCulture), "", "", N(rows.Sum(r => r.Each * r.Count), "0.0"), N(rows.Sum(r => r.Paint), "0.00")]);
        return new ScheduleGrid(Label, ["Halls", "Mark", "Profile", "Length (mm)", "Start cut", "End cut", "Count", "kg/m", "Each (kg)", "Weight (kg)", "Paint (m²)"], grid);
    }
}

/// <summary>A hall's frame elevation and anchor plan as element view roles (decision 578), of the hall or any of its members.</summary>
sealed class SteelViewRoles : IElementViewRoles
{
    static bool OfHall(IModelView m, Entity e) => e.Class == Halls.Class || Halls.Of(m, e.Id) != null;

    public IEnumerable<ElementViewRole> Roles() =>
    [
        new("steel.frame", "Frame elevation", OfHall, v => v.Str(SteelDrawings.FrameKey),
            (ctx, e, a) => SteelDrawings.FrameView(ctx, e.Id, Json.Number(a["frame"]) is { } fi ? (int)fi : null)),
        new("steel.anchorPlan", "Anchor plan", OfHall, v => v.Str(SteelDrawings.AnchorKey), (ctx, e, _) => SteelDrawings.AnchorPlan(ctx, e.Id)),
    ];
}
