using System.Text.Json.Nodes;

namespace Lapin.Drawing;

/// <summary>
/// How a detail callout's boundary or an inset view's frame is drawn (issue #73, forum 113): its pen weight (paper mm), its
/// colour and its line type (Lapin's or the project's own, by name). Each field left out (null) is the next one's: an element's
/// own (<c>lineWeight</c>, <c>lineColor</c>, <c>lineType</c> on the callout or the inset), then the project's in the drafting
/// standards (<see cref="StyleBook.CalloutLine"/>, <see cref="StyleBook.InsetFrame"/>), then Lapin's as it always drew them.
/// </summary>
public sealed record FrameLine(double? Weight = null, string? Color = null, string? LineType = null);

/// <summary>The pen a frame is drawn with once resolved: its width (paper mm), colour and dashes (paper mm; null: continuous).</summary>
public readonly record struct FramePen(double Width, string Color, double[]? Dash);

public static class FrameLines
{
    /// <summary>What a frame's pen weight may be (paper mm), as a drafting layer's.</summary>
    public const double MinWeight = 0.05, MaxWeight = 2;

    /// <summary>The standards' keys: a callout's boundary, an inset's frame.</summary>
    public const string CalloutKey = "calloutLine", InsetKey = "insetFrame";

    /// <summary>The attributes an element keeps its own with.</summary>
    public const string WeightAttr = "lineWeight", ColorAttr = "lineColor", TypeAttr = "lineType";

    /// <summary>A pen weight within range, or null.</summary>
    public static double? WeightOf(JsonNode? v) => Lapin.Model.Json.Number(v) is { } d && double.IsFinite(d) && d >= MinWeight && d <= MaxWeight ? Math.Round(d, 4) : null;

    /// <summary>A line type the project knows (Lapin's or its own; its own spelling), or null.</summary>
    public static string? TypeOf(JsonNode? v, StyleBook? b) =>
        v is JsonValue j && j.TryGetValue<string>(out var s) && s.Length > 0 ? Standards.LineTypeNames(b).FirstOrDefault(n => string.Equals(n, s, StringComparison.OrdinalIgnoreCase)) : null;

    /// <summary>A frame line read tolerantly from {weight, color, lineType} (what does not read is left out); null when nothing is left.</summary>
    public static FrameLine? Read(JsonNode? v, StyleBook? b)
    {
        if (v is not JsonObject o) return null;
        var line = new FrameLine(WeightOf(o["weight"]), Lapin.Geometry.HexColor.Read(o["color"], null), TypeOf(o["lineType"], b));
        return line.Weight == null && line.Color == null && line.LineType == null ? null : line;
    }

    /// <summary>The standards with their frame lines read from the raw standards (<paramref name="raw"/>).</summary>
    public static StyleBook With(StyleBook b, JsonObject? raw) =>
        raw == null ? b : b with { CalloutLine = Read(raw[CalloutKey], b), InsetFrame = Read(raw[InsetKey], b) };

    /// <summary>An element's own frame line, from its attributes; null when it has none.</summary>
    public static FrameLine? Own(JsonObject attrs, StyleBook? b)
    {
        var line = new FrameLine(WeightOf(attrs[WeightAttr]), Lapin.Geometry.HexColor.Read(attrs[ColorAttr], null), TypeOf(attrs[TypeAttr], b));
        return line.Weight == null && line.Color == null && line.LineType == null ? null : line;
    }

    /// <summary>
    /// The pen a frame is drawn with: each field the element's own, else the project's, else <paramref name="fallback"/>'s
    /// (Lapin's as before, so a project that sets nothing draws exactly as it did). A line type "continuous" draws solid.
    /// </summary>
    public static FramePen Resolve(StyleBook b, FrameLine? project, FrameLine? own, FramePen fallback)
    {
        var width = own?.Weight ?? project?.Weight ?? fallback.Width;
        var color = own?.Color ?? project?.Color ?? fallback.Color;
        var type = own?.LineType ?? project?.LineType;
        var dash = type == null ? fallback.Dash : Standards.DashOf(b, type) is { Length: > 0 } d ? d : null;
        return new FramePen(width, color, dash);
    }
}
