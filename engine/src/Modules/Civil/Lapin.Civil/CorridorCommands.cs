using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Civil;

/// <summary>
/// The corridor configurator's engine side, for every path element (a path or
/// road, a retaining wall, a ditch): its stations, its red line, its joints and
/// the sections chosen (<c>civil.setCorridor</c>), a red line fitted to the
/// ground (<c>civil.profileFromGround</c>), and what the configurator draws:
/// the long section and a cross-section at a station.
/// </summary>
public static class CorridorCommands
{
    public static void Register(IModuleRegistry r)
    {
        // Any of: stationStart, interval, sections [stations], joints {every, width, at [stations]}, profile {mode: ground|design, ground: existing|proposed, pvis [[station, elevation, curve length]]},
        // curves [[radius, spiral in, spiral out] per vertex of the path] (an empty list: a plain polyline).
        r.Command("setCorridor", "Change stations and profile", (ctx, a) =>
        {
            var e = Element(ctx.Model, (string?)a["id"]);
            var spec = CorridorSpec.Of(e);
            var o = spec.ToJson();
            if (Json.Number(a["stationStart"]) is { } start) o["stationStart"] = start;
            if (a["curves"] is JsonArray cv)
            {
                var curves = CorridorSpec.ReadCurves(cv);
                var path = Terrains.Contour(e, "path");
                if (curves.Any(c => !c.IsCorner) && Lapin.Alignments.PiAlignment.Problem(Lapin.Alignments.PiAlignment.Of(path, curves)) is { } why) throw new CommandException(why);
                o["curves"] = CorridorSpec.CurvesJson(curves);
            }
            if (Json.Number(a["interval"]) is { } every)
            {
                if (!(every >= 0.5)) throw new CommandException("Stations need an interval of 0.5 m at least.");
                o["interval"] = every;
            }
            if (a["sections"] is JsonArray sections) o["sections"] = new JsonArray(sections.Select(Json.Number).OfType<double>().Where(double.IsFinite).Distinct().Order().Select(s => (JsonNode)s).ToArray());
            if (a["joints"] is JsonObject j)
            {
                var joints = (JsonObject)o["joints"]!;
                if (Json.Number(j["every"]) is { } je) joints["every"] = Math.Max(0, je);
                if (Json.Number(j["width"]) is { } jw)
                {
                    if (!(jw > 0 && jw <= 1)) throw new CommandException("A joint is between 1 mm and 1 m wide.");
                    joints["width"] = jw;
                }
                if (j["at"] is JsonArray at) joints["at"] = new JsonArray(at.Select(Json.Number).OfType<double>().Where(double.IsFinite).Distinct().Order().Select(s => (JsonNode)s).ToArray());
            }
            if (a["profile"] is JsonObject p)
            {
                var profile = (JsonObject)o["profile"]!;
                if ((string?)p["mode"] is "ground" or "design") profile["mode"] = (string?)p["mode"];
                if ((string?)p["ground"] is "existing" or "proposed") profile["ground"] = (string?)p["ground"];
                if (p["pvis"] is JsonArray pv)
                {
                    var pvis = CorridorSpec.ReadPvis(pv);
                    if (pvis.Count > 0 && VerticalProfile.Problem(pvis) is { } why) throw new CommandException(why);
                    profile["pvis"] = new JsonArray(pvis.Select(x => (JsonNode)new JsonArray(x.Station, x.Elevation, x.Length)).ToArray());
                }
                if ((string?)profile["mode"] == "design" && CorridorSpec.ReadPvis(profile["pvis"]).Count < 2)
                    throw new CommandException("A designed profile needs two points (PVIs) at least: fit it to the ground first, or give them.");
            }
            var attrs = e.CloneAttrs();
            attrs["corridor"] = o;
            ctx.Replace(e.With(attrs: attrs));
            CorridorKinds.Of(e)?.AfterChange(ctx, e.Id);
            return null;
        });

        // The red line fitted to the ground under the axis: natural (existing) or proposed; then designed from those PVIs.
        r.Command("profileFromGround", "Profile from the ground", (ctx, a) =>
        {
            var e = Element(ctx.Model, (string?)a["id"]);
            var c = Corridors.ChainOf(e);
            if (c.Segments == 0) throw new CommandException("The element needs two points at least.");
            var which = (string?)a["ground"] == "existing" ? "existing" : "proposed";
            var ground = Corridors.GroundOf(ctx.Model, e, which);
            var pvis = VerticalProfile.FromGround(c.Start, c.End, s => ground(c.PointAt(s)), Corridors.Fallback(ctx.Model, e),
                Math.Max(0.5, Json.Number(a["every"]) ?? 5), Math.Max(0.01, Json.Number(a["tolerance"]) ?? 0.1));
            // On a retaining wall the red line is the crown: over the ground by the wall's height.
            var lift = e.Class == RetainingWalls.Class ? RetainingWalls.Of(e).Height : 0;
            var o = CorridorSpec.Of(e).ToJson();
            o["profile"] = new JsonObject
            {
                ["mode"] = "design", ["ground"] = which,
                ["pvis"] = new JsonArray(pvis.Select(x => (JsonNode)new JsonArray(x.Station, Math.Round((x.Elevation + lift) * 1000) / 1000, x.Length)).ToArray()),
            };
            var attrs = e.CloneAttrs();
            attrs["corridor"] = o;
            ctx.Replace(e.With(attrs: attrs));
            return new JsonObject { ["pvis"] = pvis.Count };
        });

        // Stations, the long section (natural and proposed ground, the red line) and the PVIs.
        r.Query("corridor", (q, a) =>
        {
            var e = Element(q.Model, (string?)a["id"]);
            var c = Corridors.ChainOf(e);
            var spec = CorridorSpec.Of(e);
            var step = Math.Max(0.25, c.Length / 400);
            var (existing, proposed, design) = c.Segments > 0 ? Corridors.LongSection(q.Model, e, step) : ([], [], []);
            var (_, left, right) = c.Segments > 0 ? Corridors.Section(q.Model, e, c.Start) : ([], 0, 0);
            return new JsonObject
            {
                ["kind"] = Corridors.KindOf(e), ["start"] = c.Start, ["end"] = c.End, ["length"] = c.Length,
                ["curved"] = c.Alignment != null, ["problem"] = c.Problem,
                ["left"] = left, ["right"] = right,
                ["stations"] = Nums(c.Segments > 0 ? Corridors.Stations(c, spec) : []),
                ["vertices"] = Nums(Enumerable.Range(0, c.Path.Count).Select(c.StationOf)),
                ["joints"] = Nums(c.Segments > 0 ? Corridors.Joints(c, spec) : []),
                ["spec"] = spec.ToJson(),
                ["existing"] = Pairs(existing), ["proposed"] = Pairs(proposed), ["design"] = Pairs(design),
            };
        });

        // The cross-section at a station: its parts, both grounds across it, and the line across for a section view.
        r.Query("crossSection", (q, a) =>
        {
            var e = Element(q.Model, (string?)a["id"]);
            var c = Corridors.ChainOf(e);
            if (c.Segments == 0) throw new CommandException("The element needs two points at least.");
            var station = Math.Clamp(Json.Number(a["station"]) ?? c.Start, c.Start, c.End);
            var (parts, left, right) = Corridors.Section(q.Model, e, station);
            var margin = Math.Max(1.5, (left - right) * 0.2);
            var (l, r2) = (left + margin, right - margin);
            var p = c.PointAt(station);
            var d = c.DirectionAt(station);
            var (from, to) = Corridors.LineAcross(c, station, l, r2);
            return new JsonObject
            {
                ["station"] = station, ["label"] = Chain.Label(station),
                ["at"] = new JsonArray(p.X, p.Y), ["dir"] = new JsonArray(d.X, d.Y),
                ["left"] = left, ["right"] = right,
                ["line"] = new JsonObject { ["start"] = new JsonArray(from.X, from.Y), ["end"] = new JsonArray(to.X, to.Y) },
                ["parts"] = new JsonArray(parts.Select(x => { var (material, hatch) = SectionLooks.Of(q.Model, e, x.Key); return (JsonNode)new JsonObject { ["key"] = x.Key, ["polygon"] = Pairs(x.Polygon), ["material"] = material, ["hatch"] = hatch }; }).ToArray()),
                ["existing"] = Pairs(Corridors.GroundAcross(c, Corridors.GroundOf(q.Model, e, "existing"), station, l, r2)),
                ["proposed"] = Pairs(Corridors.GroundAcross(c, Corridors.GroundOf(q.Model, e, "proposed"), station, l, r2)),
            };
        });
    }

    /// <summary>The path's cross-section as given to addPath or updatePath: template (alley | road), laneWidth, crossfall (%), fall (crown | left | right), left and right sides.</summary>
    public static JsonObject WithTemplate(JsonObject attrs, JsonObject a)
    {
        if ((string?)a["template"] is "alley" or "road") attrs["template"] = (string?)a["template"];
        if (Json.Number(a["laneWidth"]) is { } lw)
        {
            if (!(lw >= 1 && lw <= 6)) throw new CommandException("A lane is between 1 and 6 m wide.");
            attrs["laneWidth"] = lw;
        }
        if (Json.Number(a["crossfall"]) is { } cf)
        {
            if (!(cf >= 0 && cf <= 20)) throw new CommandException("The crossfall is between 0 and 20 %.");
            attrs["crossfall"] = cf;
        }
        if ((string?)a["fall"] is "crown" or "left" or "right") attrs["fall"] = (string?)a["fall"];
        foreach (var side in new[] { "left", "right" })
            if (a[side] is JsonObject) attrs[side] = Paths.MergeSide(attrs[side], a[side]);
        return attrs;
    }

    static Entity Element(IModelView m, string? id) =>
        m.Get(id ?? "") is { } e && Corridors.Is(e) ? e : throw new CommandException("There is no such path, retaining wall or ditch.");

    static JsonArray Nums(IEnumerable<double> xs) => new(xs.Select(x => (JsonNode)Math.Round(x, 4)).ToArray());
    static JsonArray Pairs(IEnumerable<Vec2> ps) => new(ps.Select(p => (JsonNode)new JsonArray(Math.Round(p.X, 4), Math.Round(p.Y, 4))).ToArray());
}
