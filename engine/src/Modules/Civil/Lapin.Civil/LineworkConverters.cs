using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Civil;

/// <summary>
/// What a plan's drafted linework becomes on the site (Convert to…, audit S12): a terrain of contours or surveyed lines (one
/// terrain of all the shapes chosen, at the heights a DXF brought or at the height given); a platform levelled into the terrain
/// on each closed outline; a road, a path or a terrain's breakline along lines meeting end to end, as Road from linework and
/// Breakline from linework made them (<see cref="LineworkElements"/>, whose chain a shape's group is). One undo step for all.
/// </summary>
public static class LineworkConverters
{
    static readonly string[] Plan = ["plan"];

    /// <summary>The heights of a shape's points: its own (a DXF's), else the height given for shapes without; null when it has neither.</summary>
    public static List<double>? Heights(LineworkShape s, double? given)
    {
        if (s.Heights is { } hs && hs.Count == s.Points.Count && hs.Any(double.IsFinite))
            return [.. hs.Select(h => double.IsFinite(h) ? h : given ?? hs.First(double.IsFinite))];
        return given is { } g ? [.. s.Points.Select(_ => g)] : null;
    }

    public static readonly LineworkConverter Terrain = new("terrain", "Terrain", "civil", "any", Plan,
        [new("elevation", "Height of shapes that bring none", "length")],
        (q, view, s, o) =>
        {
            LineworkConversion.PlanStorey(q.Model, view);
            if (Heights(s, LineworkConversion.Num(o, "elevation")) == null)
                throw new CommandException("It has no heights: a DXF's contours bring theirs; give a height for those drawn without.");
            return [];
        }, "One terrain through the points of every shape chosen (contours, surveyed lines), at their heights.")
    {
        Together = (q, view, shapes, o) =>
        {
            var storey = LineworkConversion.PlanStorey(q.Model, view);
            var given = LineworkConversion.Num(o, "elevation");
            var pts = new List<Vec3>();
            foreach (var s in shapes)
            {
                var hs = Heights(s, given)!;
                for (var i = 0; i < s.Points.Count; i++) pts.Add(new Vec3(s.Points[i].X, s.Points[i].Y, hs[i]));
            }
            // Points met twice (contours touching, a closed shape's ends) once.
            var seen = new HashSet<(long, long)>();
            pts = [.. pts.Where(p => seen.Add(((long)Math.Round(p.X * 1000), (long)Math.Round(p.Y * 1000))))];
            if (pts.Count < 3 || !Tin.Spans(pts)) throw new CommandException("A terrain needs three points at least that are not in a line.");
            var args = new JsonObject
            {
                ["storeyId"] = storey.Id,
                ["points"] = new JsonArray([.. pts.Select(p => (JsonNode)new JsonArray(Math.Round(p.X, 9), Math.Round(p.Y, 9), Math.Round(p.Z, 9)))]),
            };
            return [new LineworkGroup([.. shapes.Select(s => s.Id)], [new("civil.addTerrain", args, $"A terrain of {pts.Count} points from {shapes.Count} shapes")])];
        },
    };

    public static readonly LineworkConverter Platforms = new("platform", "Platform", "civil", "closed", Plan,
        [new("elevation", "Elevation (empty: the ground's at its middle)", "length")],
        (q, view, s, o) =>
        {
            LineworkConversion.PlanStorey(q.Model, view);
            LineworkConversion.Straight(s, "A platform's outline");
            var mid = s.Middle;
            var terrain = q.Model.OfClass(IfcClass.GeographicElement).FirstOrDefault(t => t.PredefinedType == Terrains.Predefined && Terrains.Of(t).ElevationAt(mid.X, mid.Y) != null)
                ?? q.Model.OfClass(IfcClass.GeographicElement).FirstOrDefault(t => t.PredefinedType == Terrains.Predefined)
                ?? throw new CommandException("No terrain: a platform is levelled into one, so make the terrain first.");
            var args = new JsonObject { ["terrainId"] = terrain.Id, ["points"] = LineworkConversion.Points(s.Points) };
            if (LineworkConversion.Num(o, "elevation") is { } z) args["elevation"] = z;
            return [new("civil.addPlatform", args, $"A platform of {s.Points.Count} corners in {terrain.Name}")];
        }, "A platform levelled into the terrain on each closed outline.");

    /// <summary>
    /// The converter of what civil's linework becomes as <paramref name="kind"/> (road, path, breakline; a module's own, a track,
    /// through <see cref="LineworkElements.Makers"/>): the shapes chosen grouped by the chain each belongs to (lines and open
    /// polylines on its view and layer meeting end to end), one civil.fromLinework for each chain. A hook for premium modules too.
    /// </summary>
    public static LineworkConverter Along(string kind, string label, string description) => new(kind == "path" ? "civilPath" : kind, label, "civil", "open", Plan, [],
        (q, view, s, _) =>
        {
            LineworkConversion.PlanStorey(q.Model, view);
            if (q.Model.Get(s.Id) is not { } e || LineworkElements.Points(e) == null) throw new CommandException($"{label} follows lines and open polylines.");
            if (kind == "breakline" && !q.Model.OfClass(IfcClass.GeographicElement).Any(t => t.PredefinedType == Terrains.Predefined))
                throw new CommandException("No terrain: a breakline is the terrain's, so make the terrain first.");
            return [];
        }, description)
    {
        Together = (q, view, shapes, _) =>
        {
            var storey = LineworkConversion.PlanStorey(q.Model, view);
            var groups = new List<LineworkGroup>();
            var done = new HashSet<string>();
            foreach (var s in shapes)
            {
                if (done.Contains(s.Id)) continue;
                var e = q.Model.Get(s.Id)!;
                var (viewId, layer) = (e.Str("viewId"), e.Str("layer"));
                var pieces = q.Model.OfClass(IfcClass.Annotation).Where(x => x.Str("viewId") == viewId && x.Str("layer") == layer)
                    .Select(x => (E: x, Pts: LineworkElements.Points(x))).Where(x => x.Pts != null).Select(x => (x.E, Pts: x.Pts!)).ToList();
                var at = pieces.FindIndex(p => p.E.Id == s.Id);
                var chain = LineworkElements.Chains([.. pieces.Select(p => (IReadOnlyList<Vec3>)p.Pts)], 1e-3).First(c => c.Members.Contains(at));
                var members = chain.Members.Select(i => pieces[i].E.Id).ToHashSet();
                var ids = shapes.Where(x => members.Contains(x.Id)).Select(x => x.Id).ToList();
                foreach (var id in ids) done.Add(id);
                var args = new JsonObject { ["id"] = s.Id, ["as"] = kind, ["storeyId"] = storey.Id };
                groups.Add(new(ids, [new("civil.fromLinework", args, $"{label} along {chain.Members.Count} line{(chain.Members.Count == 1 ? "" : "s")} joined end to end")]));
            }
            return groups;
        },
    };

    public static readonly LineworkConverter Roads = Along("road", "Road", "A road (two lanes, kerbs and sidewalks) along lines meeting end to end; their heights give its profile.");
    public static readonly LineworkConverter Paths = Along("path", "Path", "A path along lines meeting end to end.");
    public static readonly LineworkConverter Breaklines = Along("breakline", "Breakline", "A breakline of the terrain along lines meeting end to end, at their heights (the ground's where they have none).");

    public static void Register(IModuleRegistry r)
    {
        foreach (var c in new[] { Terrain, Platforms, Roads, Paths, Breaklines }) r.Service<LineworkConverter>(c);
    }
}
