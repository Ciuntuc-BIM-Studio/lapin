using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Drafting.Cad;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Studio;

public sealed partial class StudioModule
{
    static bool Flag(JsonNode? n) => n is JsonValue v && v.TryGetValue<bool>(out var b) && b;

    /// <summary>
    /// A solid from a sketch: <c>{profileId, class?, name?, storeyId?}</c> and what its operation takes. An
    /// extrusion: <c>depth</c>, <c>offset</c>, <c>symmetric</c>, <c>taper</c> (°), <c>upTo</c> {id, point, normal}
    /// (a face of another element it runs to), <c>cut</c> [ids] (it cuts those elements and is not drawn itself).
    /// A sweep: <c>pathId</c>, <c>endProfileId</c> or <c>railId</c>, <c>place</c>, <c>placeOffset</c>, <c>twist</c>,
    /// <c>scaleEnd</c>, <c>rise</c>, <c>turns</c>. A loft: <c>profileIds</c>, <c>railId</c>, <c>smooth</c>. A revolution: <c>axisId</c>, <c>angle</c>.
    /// </summary>
    static JsonNode? Create(ICommandContext ctx, JsonObject a, string op)
    {
        var profile = Drafts.Require(ctx.Model, (string?)a["profileId"], editable: false);
        if (Sketches.OutlineOf(ctx.Model, profile) is not { } outline) throw new CommandException("The profile must be closed: a closed polyline, a circle, or lines and arcs meeting end to end.");
        if (outline.Ids.Any(ProfileIds(ctx.Model).Contains)) throw new CommandException("That outline is a solid's profile already.");
        var plane = PlaneFor(ctx, profile);
        var (cls, exportAs) = ClassOf((string?)a["class"]);
        var attrs = new JsonObject { ["operation"] = op, ["profile"] = profile.Id };
        if (exportAs != null) attrs["exportAs"] = exportAs;
        List<string> cuts = [];
        switch (op)
        {
            case "extrude":
            {
                var depth = Json.Number(a["depth"]) ?? 1;
                if (Math.Abs(depth) < 1e-6 && a["upTo"] == null) throw new CommandException("An extrusion needs a depth.");
                attrs["depth"] = depth;
                if (Json.Number(a["offset"]) is { } off) attrs["offset"] = off;
                if (Flag(a["symmetric"])) attrs["symmetric"] = true;
                if (Json.Number(a["taper"]) is { } taper && Math.Abs(taper) > 1e-9)
                {
                    if (Math.Abs(taper) >= 89) throw new CommandException("A taper is less than 89°.");
                    attrs["taper"] = taper;
                }
                if (a["upTo"] is JsonObject upTo) attrs["upTo"] = UpTo(ctx, upTo);
                if (a["cut"] is JsonArray cut)
                {
                    cuts = cut.Select(x => (string?)x).OfType<string>().Distinct().ToList();
                    foreach (var id in cuts) if (ctx.Model.Get(id) is null || Spatial.StoreyOf(ctx.Model, id) == null) throw new CommandException($"There is no element {id} to cut.");
                    if (cuts.Count > 0) attrs["void"] = true;
                }
                break;
            }
            case "sweep":
            {
                var path = Drafts.Require(ctx.Model, (string?)a["pathId"], editable: false);
                if (Drafts.ShapeOf(path) is not (Line or Polyline or Arc or Circle)) throw new CommandException("A path is a line, a polyline, an arc or a circle.");
                if (path.Str("viewId") != profile.Str("viewId")) throw new CommandException("The path is drawn on the profile's view.");
                attrs["guide"] = path.Id;
                // A profile it ends with (the profile becomes it on the way), or a second rail it stretches to.
                if ((string?)a["endProfileId"] is { } endId)
                {
                    var end = ClosedDraft(ctx.Model, endId, "end profile");
                    if (end.Id == profile.Id) throw new CommandException("The end profile is another outline than the start one.");
                    if (end.Str("viewId") != profile.Str("viewId")) throw new CommandException("The end profile is drawn on the profile's view.");
                    attrs["endProfile"] = end.Id;
                }
                if ((string?)a["railId"] is { } railId)
                {
                    var rail = Drafts.Require(ctx.Model, railId, editable: false);
                    if (Drafts.ShapeOf(rail) is not (Line or Polyline or Arc)) throw new CommandException("A rail is a line, a polyline or an arc.");
                    if (rail.Id == path.Id) throw new CommandException("The second rail is another line than the path.");
                    if (rail.Str("viewId") != profile.Str("viewId")) throw new CommandException("The rail is drawn on the profile's view.");
                    attrs["rail"] = rail.Id;
                }
                if (attrs["endProfile"] != null && attrs["rail"] != null) throw new CommandException("A sweep ends with another profile or follows a second rail, not both.");
                if ((string?)a["place"] is { } place)
                {
                    if (!Solids.Places.Contains(place)) throw new CommandException($"„{place}” is not a place on the path ({string.Join(", ", Solids.Places)}).");
                    attrs["place"] = place;
                }
                if (Json.Number(a["placeOffset"]) is { } gap) attrs["placeOffset"] = gap;
                foreach (var k in new[] { "twist", "scaleEnd", "rise", "turns" })
                    if (Json.Number(a[k]) is { } x) attrs[k] = x;
                if (Json.Number(a["scaleEnd"]) is <= 0) throw new CommandException("The scale at the end is more than 0.");
                break;
            }
            case "loft":
            {
                // The profiles in order, each on its own view (a plan of another level, a section further on).
                var ids = (a["profileIds"] as JsonArray)?.Select(x => (string?)x).OfType<string>().ToList() ?? [];
                if (ids.Count < 2) throw new CommandException("A loft needs two profiles at least.");
                if (ids[0] != profile.Id) ids.Insert(0, profile.Id);
                var all = ids.Select((id, i) => i == 0 ? profile : ClosedDraft(ctx.Model, id, "profile")).ToList();
                if (all.Select(x => x.Id).Distinct().Count() != all.Count) throw new CommandException("A profile comes once in a loft.");
                foreach (var d in all.Skip(1)) PlaneFor(ctx, d);
                attrs["profiles"] = new JsonArray(all.Select(x => (JsonNode)x.Id).ToArray());
                if ((string?)a["railId"] is { } railId)
                {
                    var rail = Drafts.Require(ctx.Model, railId, editable: false);
                    if (Drafts.ShapeOf(rail) is not (Line or Polyline or Arc)) throw new CommandException("A guide is a line, a polyline or an arc.");
                    PlaneFor(ctx, rail);
                    attrs["rail"] = rail.Id;
                }
                if (Flag(a["smooth"])) attrs["smooth"] = true;
                break;
            }
            case "revolve":
            {
                var axis = Drafts.Require(ctx.Model, (string?)a["axisId"], editable: false);
                if (Drafts.ShapeOf(axis) is not Line) throw new CommandException("The axis is a line.");
                if (axis.Str("viewId") != profile.Str("viewId")) throw new CommandException("The axis is drawn on the profile's view.");
                attrs["guide"] = axis.Id;
                attrs["angle"] = Json.Number(a["angle"]) ?? 360;
                break;
            }
        }
        var name = ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : $"{char.ToUpperInvariant(op[0])}{op[1..]} {ctx.Model.OfClass(cls).Count() + 1}";
        var e = new Entity(ctx.NewId(), cls, name, attrs);
        ctx.Add(e);
        var storey = (string?)a["storeyId"] ?? plane.StoreyId ?? Spatial.Storeys(ctx.Model).FirstOrDefault()?.Id ?? throw new CommandException("The project has no storey.");
        Spatial.Contain(ctx, storey, e.Id);
        foreach (var target in cuts)
            ctx.AddRelation(new Relation(ctx.NewId(), IfcClass.RelInterferesElements, target, [e.Id], new JsonObject { ["interferenceType"] = "Subtract", ["name"] = "Lapin cut" }));
        return new JsonObject { ["id"] = e.Id };
    }

    /// <summary>What an extrusion runs up to: an element's face, kept as the element and a point and normal on it (found again as the element changes).</summary>
    static JsonObject UpTo(ICommandContext ctx, JsonObject a)
    {
        var target = ctx.Model.Get((string?)a["id"] ?? "") ?? throw new CommandException("Pick the face the extrusion runs up to.");
        if (Features.P(a["point"]) is not { } p || Features.P(a["normal"]) is not { } n || n.Length < 1e-9) throw new CommandException("The face it runs up to needs a point and a normal.");
        if (ctx.Query.Geometry(target.Id) is { Parts.Count: > 0 } g && BrepPick.Face(g.Parts.Select(x => x.Solid).ToList(), p, n) is { } face)
            return new JsonObject { ["id"] = target.Id, ["point"] = Features.J(p), ["normal"] = Features.J(face.Normal) };
        return new JsonObject { ["id"] = target.Id, ["point"] = Features.J(p), ["normal"] = Features.J(n.Normalized()) };
    }

    /// <summary>
    /// A primitive placed with a click: <c>{shape, at: [x, y] or [x, y, z], storeyId?, width, length, height, radius,
    /// radius2, rotation, class?, name?}</c>, standing on its storey's level unless z is given (the middle of its base).
    /// </summary>
    static JsonNode? Primitive(ICommandContext ctx, JsonObject a)
    {
        var shape = ((string?)a["shape"])?.ToLowerInvariant() ?? "box";
        if (!Solids.Primitives.Contains(shape)) throw new CommandException($"„{shape}” is not a primitive ({string.Join(", ", Solids.Primitives)}).");
        var storey = (string?)a["storeyId"] is { Length: > 0 } sid ? Spatial.Require(ctx.Model, sid, IfcClass.BuildingStorey) : Spatial.Storeys(ctx.Model).FirstOrDefault() ?? throw new CommandException("The project has no storey.");
        if (a["at"] is not JsonArray at || at.Count < 2) throw new CommandException("A primitive needs the point it stands on [x, y].");
        var z = at.Count > 2 ? Json.Number(at[2]) ?? storey.Num("elevation") : storey.Num("elevation");
        var (cls, exportAs) = ClassOf((string?)a["class"]);
        (double W, double L, double H, double R, double R2) defaults = shape switch
        {
            "box" or "wedge" or "pyramid" => (1.0, 1.0, 1.0, 0.0, 0.0),
            "cylinder" => (0.0, 0.0, 1.0, 0.5, 0.0),
            "cone" => (0.0, 0.0, 1.0, 0.5, 0.0),
            "sphere" => (0.0, 0.0, 0.0, 0.5, 0.0),
            "torus" => (0.0, 0.0, 0.0, 1.0, 0.2),
            _ => (1.0, 1.0, 1.0, 0.5, 0.0),
        };
        var attrs = new JsonObject
        {
            ["operation"] = "primitive", ["shape"] = shape,
            ["at"] = new JsonArray(Json.Number(at[0]) ?? 0, Json.Number(at[1]) ?? 0, z),
            ["rotation"] = Json.Number(a["rotation"]) ?? 0,
        };
        void Size(string key, double fallback) { var v = Json.Number(a[key]) ?? fallback; if (v > 0 || key == "radius2") attrs[key] = v; }
        switch (shape)
        {
            case "box" or "wedge" or "pyramid": Size("width", defaults.W); Size("length", defaults.L); Size("height", defaults.H); break;
            case "cylinder": Size("radius", defaults.R); Size("height", defaults.H); break;
            case "cone": Size("radius", defaults.R); Size("radius2", defaults.R2); Size("height", defaults.H); break;
            case "sphere": Size("radius", defaults.R); break;
            case "torus": Size("radius", defaults.R); Size("radius2", defaults.R2); break;
        }
        if (exportAs != null) attrs["exportAs"] = exportAs;
        var probe = Solids.Primitive(shape, Vec3.Zero, 0, attrs.Num("width"), attrs.Num("length"), attrs.Num("height"), attrs.Num("radius"), attrs.Num("radius2"));
        if (probe.Problem != null) throw new CommandException(probe.Problem);
        var label = char.ToUpperInvariant(shape[0]) + shape[1..];
        var name = ((string?)a["name"])?.Trim() is { Length: > 0 } nm ? nm : $"{label} {ctx.Model.OfClass(cls).Count(x => x.Str("shape") == shape) + 1}";
        var e = new Entity(ctx.NewId(), cls, name, attrs);
        ctx.Add(e);
        Spatial.Contain(ctx, storey.Id, e.Id);
        return new JsonObject { ["id"] = e.Id };
    }
}

static class JsonNums
{
    public static double Num(this JsonObject o, string key) => Json.Number(o[key]) ?? 0;
}
