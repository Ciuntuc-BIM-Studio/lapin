using System.Text.Json.Nodes;
using Lapin.Geometry;

namespace Lapin.Alignments;

/// <summary>The segments of an alignment as the model stores them and the interface reads them (metres, radians; IFC's spelling of the types).</summary>
public static class AlignmentJson
{
    public static JsonArray Horizontal(IEnumerable<HorizontalSegment> segments) => new(segments.Select(s => (JsonNode)new JsonObject
    {
        ["type"] = SegmentTypes.Ifc(s.Type), ["start"] = new JsonArray(s.Start.X, s.Start.Y), ["direction"] = s.StartDirection,
        ["startRadius"] = s.StartRadius, ["endRadius"] = s.EndRadius, ["length"] = s.Length, ["gravityCenterLineHeight"] = s.GravityCenterLineHeight,
    }).ToArray());

    public static List<HorizontalSegment>? ReadHorizontal(JsonNode? n) => n is JsonArray a && a.Count > 0
        ? a.OfType<JsonObject>().Select(o => new HorizontalSegment(
            SegmentTypes.Horizontal((string?)o["type"]) ?? HorizontalType.Line,
            o["start"] is JsonArray p && p.Count >= 2 ? new Vec2(Json.Number(p[0]) ?? 0, Json.Number(p[1]) ?? 0) : new Vec2(0, 0),
            Json.Number(o["direction"]) ?? 0, Json.Number(o["startRadius"]) ?? 0, Json.Number(o["endRadius"]) ?? 0, Json.Number(o["length"]) ?? 0, Json.Number(o["gravityCenterLineHeight"]))).Where(s => s.Length > 1e-9).ToList()
        : null;

    public static JsonArray Vertical(IEnumerable<VerticalSegment> segments) => new(segments.Select(v => (JsonNode)new JsonObject
    {
        ["type"] = SegmentTypes.Ifc(v.Type), ["start"] = v.StartDistAlong, ["length"] = v.HorizontalLength, ["height"] = v.StartHeight,
        ["startGradient"] = v.StartGradient, ["endGradient"] = v.EndGradient, ["radius"] = v.RadiusOfCurvature,
    }).ToArray());

    public static List<VerticalSegment>? ReadVertical(JsonNode? n) => n is JsonArray a && a.Count > 0
        ? a.OfType<JsonObject>().Select(o => new VerticalSegment(
            SegmentTypes.Vertical((string?)o["type"]) ?? VerticalType.ConstantGradient,
            Json.Number(o["start"]) ?? 0, Json.Number(o["length"]) ?? 0, Json.Number(o["height"]) ?? 0, Json.Number(o["startGradient"]) ?? 0, Json.Number(o["endGradient"]) ?? 0, Json.Number(o["radius"]))).ToList()
        : null;
}
