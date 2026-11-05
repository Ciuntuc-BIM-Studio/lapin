using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture.Stairs;

/// <summary>
/// Railings: a stair's along its sides, and one of its own (testers' request, decision 238) drawn as a line in plan along a
/// balcony's edge, a terrace or a gallery: a <c>LapinRailing</c> exported as an IfcRailing (GUARDRAIL) standing on its storey (plus its base offset),
/// in the stair railings' designs: posts and a handrail ("bar"), balusters, glass panes, cables or a solid panel, with its
/// height, handrail profile and sizes and post spacing. Both are built by <see cref="Solids"/> from the line under them.
/// </summary>
public static class Railings
{
    /// <summary>Lapin's own class (Studio owns IfcRailing for its solids), exported as IfcRailing.</summary>
    public const string Class = "LapinRailing";

    static Vec2 P(JsonNode? n) => n is JsonArray a && a.Count >= 2 ? new Vec2(Json.Number(a[0]) ?? 0, Json.Number(a[1]) ?? 0) : default;

    /// <summary>
    /// A railing's solids along a line (its foot, point by point; it may climb), in its style: the handrail (square or round)
    /// on top, then posts at their spacing (glass and cables between them), balusters at most their spacing apart, or a solid
    /// panel; glass apart, to be drawn as glass. On a stair the line runs along the nosings, over the treads: <paramref name="ground"/>
    /// gives the top of what lies under a post's footprint (its centre, its half size), so each post stands on the tread or landing
    /// under it and still reaches the handrail; without it (or where it finds nothing) a post stands on the line.
    /// </summary>
    public static (List<Solid> Metal, List<Solid> Glass) Solids(IReadOnlyList<Vec3> path, StairRailing rail, Func<Vec3, double, double?>? ground = null)
    {
        var metal = new List<Solid>();
        var glass = new List<Solid>();
        var height = Math.Max(0.3, rail.Height);
        Vec3 Up(Vec3 p, double up) => new(p.X, p.Y, p.Z + up);
        for (var i = 0; i + 1 < path.Count; i++)
        {
            var (a, b) = (path[i], path[i + 1]);
            var len = (b.Xy - a.Xy).Length;
            if (len < 1e-6) continue;
            var bar = rail.Profile == "round" ? StairBuilder.RoundBar(Up(a, height), Up(b, height), rail.BarSize, "rail") : Prism.SweepBox(Up(a, height), Up(b, height), rail.BarSize, rail.BarSize, "rail");
            if (bar != null) metal.Add(bar);
            var across = b - a;
            var flat = new Vec3(-across.Y, across.X, 0);
            flat = flat * (1 / Math.Max(flat.Length, 1e-9));
            Solid? Pane(double from, double to, double thick, string tag)
            {
                List<Vec3> ring = [Up(a, from), Up(b, from), Up(b, to), Up(a, to)];
                var shift = flat * (-thick / 2);
                return Prism.Extrude(ring.Select(q => q + shift).ToList(), null, flat * thick, tag);
            }
            void Posts(double spacing, double size, string tag, bool last)
            {
                var count = Math.Max(1, (int)Math.Ceiling(len / Math.Max(spacing, 0.05)));
                for (var j = 0; j <= count; j++)
                {
                    if (j == count && !last) continue;
                    var on = a + (b - a) * ((double)j / count);
                    var top = on.Z + height;
                    var foot = Math.Min(ground?.Invoke(on, size / 2) ?? on.Z, top - 0.05);
                    if (Prism.Box(new Vec3(on.X, on.Y, (foot + top) / 2), size, size, top - foot, tag) is { } post) metal.Add(post);
                }
            }
            var lastSegment = i + 2 >= path.Count;
            switch (rail.Style)
            {
                case "balusters":
                    Posts(Math.Max(rail.BalusterSpacing, 0.05), Math.Max(0.012, rail.PostSize * 0.6), "baluster", lastSegment);
                    if (Prism.SweepBox(Up(a, 0.08), Up(b, 0.08), 0.03, 0.03, "rail") is { } bottomRail) metal.Add(bottomRail);
                    break;
                case "glass":
                    Posts(Math.Max(rail.PostSpacing, 0.3), rail.PostSize, "post", lastSegment);
                    if (Pane(0.08, height - 0.06, 0.012, "glass") is { } pane) glass.Add(pane);
                    break;
                case "cable":
                    Posts(Math.Max(rail.PostSpacing, 0.3), rail.PostSize, "post", lastSegment);
                    for (var c = 1; c <= 6; c++)
                        if (Prism.SweepBox(Up(a, height * c / 7.5), Up(b, height * c / 7.5), 0.006, 0.006, "cable") is { } cable) metal.Add(cable);
                    break;
                case "panel":
                    if (Pane(0, height, Math.Max(0.06, rail.PostSize * 2), "panel") is { } panel) metal.Add(panel);
                    break;
                default:
                    Posts(Math.Max(rail.PostSpacing, 0.3), rail.PostSize, "post", lastSegment);
                    break;
            }
        }
        return (metal, glass);
    }

    // ------------------------------------------------------------ the railing of its own

    /// <summary>A railing element's design, from its own fields (the stair railing's defaults where it has none).</summary>
    public static StairRailing Of(Entity e) => new("both",
        e.NumOrNull("height") ?? 1.0, e.NumOrNull("barSize") ?? 0.045, e.NumOrNull("postSize") ?? 0.04, e.NumOrNull("postSpacing") ?? 1.2,
        StairSpec.RailingStyles.Contains(e.Str("style") ?? "") ? e.Str("style")! : "balusters", e.Str("profile") == "square" ? "square" : "round",
        e.NumOrNull("balusterSpacing") ?? 0.11, 0, 0, e.Str("color"));

    public static List<Vec2> Points(Entity e) => (e.Attrs["points"] as JsonArray)?.Select(P).ToList() ?? [];


    static List<Vec2> CheckPoints(JsonNode? n)
    {
        var pts = (n as JsonArray)?.Select(P).ToList() ?? throw new CommandException("A railing runs along two points at least.");
        var clean = new List<Vec2>();
        foreach (var p in pts) if (clean.Count == 0 || (p - clean[^1]).Length > 1e-3) clean.Add(p);
        if (clean.Count < 2) throw new CommandException("A railing runs along two points at least.");
        return clean;
    }

    public static void Register(IModuleRegistry r)
    {
        r.Class(new(Class, "Railing",
        [
            new("style", "Design", "choice", Choices: StairSpec.RailingStyles),
            new("height", "Height", "length", Min: 0.3, Max: 2.5),
            new("baseOffset", "Base offset", "length", Min: -5, Max: 50),
            new("profile", "Handrail", "choice", Choices: ["round", "square"]),
            new("barSize", "Handrail size", "length", Min: 0.02, Max: 0.15),
            new("postSize", "Post size", "length", Min: 0.01, Max: 0.15),
            new("postSpacing", "Post spacing", "length", Min: 0.3, Max: 3),
            new("balusterSpacing", "Baluster spacing", "length", Min: 0.05, Max: 0.3),
        ], "#4a4a4a"));
        r.Geometry(Class, (e, m, _) => Geometry(e, m));
        r.Quantities(Class, (e, _, m) => Quantities(e, m));

        // A railing along a line drawn in plan: {storeyId, points: [[x, y]…], style?, height?, baseOffset?}.
        r.Command("addRailing", "Railing", (ctx, a) =>
        {
            var storeyId = (string?)a["storeyId"] ?? Spatial.Storeys(ctx.Model).FirstOrDefault()?.Id ?? throw new CommandException("The project has no storey.");
            var pts = CheckPoints(a["points"]);
            var attrs = new JsonObject { ["exportAs"] = "IfcRailing", ["points"] = SectionPaths.PointsJson(pts), ["style"] = (string?)a["style"] is { } st && StairSpec.RailingStyles.Contains(st) ? st : "balusters", ["height"] = Json.Number(a["height"]) ?? 1.0 };
            if (Json.Number(a["baseOffset"]) is { } bo) attrs["baseOffset"] = bo;
            var n = Numbering.Next(ctx.Model.OfClass(Class));
            var e = new Entity(ctx.NewId(), Class, $"Railing {n}", attrs, "GUARDRAIL");
            ctx.Add(e);
            Spatial.Contain(ctx, storeyId, e.Id);
            return new JsonObject { ["id"] = e.Id };
        });

        // Its line: {id, points}.
        r.Command("updateRailing", "Change railing", (ctx, a) =>
        {
            var e = ctx.Model.Get((string?)a["id"] ?? "") is { Class: Class } x && x.Attrs["points"] != null ? x : throw new CommandException("Not a railing drawn in Lapin.");
            var attrs = e.CloneAttrs();
            attrs["points"] = SectionPaths.PointsJson(CheckPoints(a["points"]));
            ctx.Replace(e.With(attrs: attrs));
            return null;
        });
    }

    static double BaseZ(Entity e, IModelView m) => (Spatial.StoreyOf(m, e.Id)?.Num("elevation") ?? 0) + e.Num("baseOffset");

    public static ElementGeometry? Geometry(Entity e, IModelView m)
    {
        var pts = Points(e);
        if (pts.Count < 2) return null;
        var rail = Of(e);
        var z = BaseZ(e, m);
        var (metal, glass) = Solids(pts.Select(p => p.At(z)).ToList(), rail);
        var parts = new List<GeometryPart>();
        if (metal.Count > 0) parts.Add(new GeometryPart("Railing", rail.Color ?? "#4a4a4a", Openings.Combine(metal, "railing"), null, "railing"));
        if (glass.Count > 0) parts.Add(new GeometryPart("Railing glass", "#9cc8e8", Openings.Combine(glass, "glass"), null, "railing-glass"));
        if (parts.Count == 0) return null;
        // In plan: the handrail as a band along the line, and the posts.
        var plan = new List<PlanItem>();
        var half = Math.Max(rail.BarSize, rail.Style == "panel" ? Math.Max(0.06, rail.PostSize * 2) : rail.BarSize) / 2;
        for (var i = 0; i + 1 < pts.Count; i++)
        {
            var (a, b) = (pts[i], pts[i + 1]);
            var n = (b - a).Normalized().Perp * half;
            plan.Add(new PlanItem("cut", true, [a + n, b + n, b - n, a - n]));
        }
        return new ElementGeometry(parts, plan, pts);
    }

    static IEnumerable<QuantitySet> Quantities(Entity e, IModelView m)
    {
        var pts = Points(e);
        if (pts.Count < 2) yield break;
        var length = pts.Zip(pts.Skip(1), (a, b) => (b - a).Length).Sum();
        yield return new QuantitySet("Qto_RailingBaseQuantities", [new("Length", length, "LENGTH", $"Railing, {Of(e).Style}")]);
    }
}
