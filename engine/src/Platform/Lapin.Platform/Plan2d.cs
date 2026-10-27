using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// One item of an element's own 2D drawing, as an IFC file gave it
/// (docs/spec-ifc.md, Reprezentările 2D): a line (open or closed), a fill
/// (loops, even-odd, the first the outer one) or a text, in world metres,
/// with the file's colour and line pattern when it had them.
/// <see cref="Footprint"/>: from a FootPrint representation (the element's
/// outline, drawn as cut where its body crosses the plan's cut).
/// </summary>
public sealed record Plan2dItem(string Kind, bool Closed, IReadOnlyList<IReadOnlyList<Vec2>> Loops, string? Color = null, string? Pattern = null,
    string? Text = null, double? Height = null, double Angle = 0, bool Footprint = false)
{
    public const string Line = "line", Fill = "fill", Label = "text";
    public IReadOnlyList<Vec2> Points => Loops.Count > 0 ? Loops[0] : [];
}

/// <summary>
/// An element's own 2D representations, as an IFC file gave them (a door's
/// swing, a footprint, a fixture's symbol), kept in its <c>plan2d</c>
/// attribute next to its stored body: <c>{plan: [items], section: n,
/// elevation: n}</c>. Plans draw the plan items instead of the body's cut
/// (unless the view's style says otherwise); the section and elevation
/// representations are only counted, as they cannot be placed on a view's
/// plane unambiguously.
/// </summary>
public static class Plan2d
{
    public const string Key = "plan2d";

    public static bool Has(Entity e) => e.Attrs[Key] is JsonObject o && o["plan"] is JsonArray { Count: > 0 };

    static double R(double v) => Math.Round(v, 4);

    static JsonArray Pts(IReadOnlyList<Vec2> pts)
    {
        var a = new JsonArray();
        foreach (var p in pts) { a.Add((JsonNode)R(p.X)); a.Add((JsonNode)R(p.Y)); }
        return a;
    }

    /// <summary>The attribute: coordinates to 0,1 mm.</summary>
    public static JsonObject Write(IEnumerable<Plan2dItem> plan, int section = 0, int elevation = 0)
    {
        var list = new JsonArray();
        foreach (var i in plan)
        {
            var o = new JsonObject { ["kind"] = i.Kind };
            if (i.Kind == Plan2dItem.Fill) o["loops"] = new JsonArray(i.Loops.Select(l => (JsonNode)Pts(l)).ToArray());
            else o["pts"] = Pts(i.Points);
            if (i.Closed) o["closed"] = true;
            if (i.Footprint) o["footprint"] = true;
            if (i.Color != null) o["color"] = i.Color;
            if (i.Pattern != null) o["pattern"] = i.Pattern;
            if (i.Text != null) o["text"] = i.Text;
            if (i.Height is { } h) o["height"] = R(h);
            if (Math.Abs(i.Angle) > 1e-9) o["angle"] = Math.Round(i.Angle, 3);
            list.Add((JsonNode)o);
        }
        var r = new JsonObject { ["plan"] = list };
        if (section > 0) r["section"] = section;
        if (elevation > 0) r["elevation"] = elevation;
        return r;
    }

    static List<Vec2> ReadPts(JsonNode? n)
    {
        var o = new List<Vec2>();
        if (n is not JsonArray a) return o;
        for (var k = 0; k + 1 < a.Count; k += 2) o.Add(new Vec2(Json.Number(a[k]) ?? 0, Json.Number(a[k + 1]) ?? 0));
        return o;
    }

    /// <summary>The element's plan items; empty when it has none.</summary>
    public static IReadOnlyList<Plan2dItem> Read(Entity e)
    {
        if (e.Attrs[Key] is not JsonObject o || o["plan"] is not JsonArray plan) return [];
        var list = new List<Plan2dItem>();
        foreach (var i in plan.OfType<JsonObject>())
        {
            var kind = (string?)i["kind"] ?? Plan2dItem.Line;
            IReadOnlyList<IReadOnlyList<Vec2>> loops = kind == Plan2dItem.Fill
                ? (i["loops"] as JsonArray)?.Select(l => (IReadOnlyList<Vec2>)ReadPts(l)).Where(l => l.Count >= 3).ToList() ?? []
                : [ReadPts(i["pts"])];
            if (loops.Count == 0 || loops[0].Count == 0) continue;
            list.Add(new Plan2dItem(kind, i["closed"] is JsonValue c && c.TryGetValue<bool>(out var cb) && cb, loops, (string?)i["color"], (string?)i["pattern"],
                (string?)i["text"], Json.Number(i["height"]), Json.Number(i["angle"]) ?? 0, i["footprint"] is JsonValue fp && fp.TryGetValue<bool>(out var fb) && fb));
        }
        return list;
    }

    /// <summary>The plan items as an element's plan linework (for picking and snapping in a plan): lines as symbols, fills as hit areas.</summary>
    public static IReadOnlyList<PlanItem> PlanItems(Entity e) =>
        Read(e).SelectMany(i => i.Kind switch
        {
            Plan2dItem.Line when i.Points.Count >= 2 => [new PlanItem("symbol", i.Closed, i.Points)],
            Plan2dItem.Fill => i.Loops.Take(1).Select(l => new PlanItem("hit", true, l)),
            _ => (IEnumerable<PlanItem>)[],
        }).ToList();
}
