using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// A drafted shape as a converter reads it (docs/plan-user-extensibility.md §7.4): its id, its kind (line, polyline, circle, arc),
/// its JSON as drafting keeps it (in the view's metres: a plan's are the model's x and y), its points in chords, whether it is
/// closed, its layer; a circle's or an arc's centre and radius; an arc's point halfway along it; the heights a DXF brought with
/// its points (one per point, NaN where it has none; null when it has none at all).
/// </summary>
public sealed record LineworkShape(string Id, string Kind, JsonObject Shape, IReadOnlyList<Vec2> Points, bool Closed, string? Layer, Vec2? Centre = null, double? Radius = null, Vec2? Mid = null,
    IReadOnlyList<double>? Heights = null)
{
    /// <summary>The straight pieces it is made of (an arc: one piece from end to end, its <see cref="Mid"/> giving its curve).</summary>
    public IEnumerable<(Vec2 A, Vec2 B)> Pieces()
    {
        if (Kind == "arc" && Points.Count >= 2) { yield return (Points[0], Points[^1]); yield break; }
        for (var i = 0; i + 1 < Points.Count; i++) yield return (Points[i], Points[i + 1]);
        if (Closed && Points.Count > 2) yield return (Points[^1], Points[0]);
    }

    /// <summary>The middle of what it covers: a circle's or an arc's centre, else the mean of its corners.</summary>
    public Vec2 Middle => Centre ?? (Points.Count == 0 ? default : new Vec2(Points.Average(p => p.X), Points.Average(p => p.Y)));

    /// <summary>Whether it is curved: a circle, an arc or an ellipse (a drafted polyline's edges are straight).</summary>
    public bool Curved => Kind is "circle" or "arc" or "ellipse";

    /// <summary>
    /// A closed shape as an outline with arcs (decision 589): a polyline's corners, straight; a circle as four quarter arcs
    /// (<see cref="ArcOutline.Circle"/>), so a floor or a ceiling made from it is round, not its chords.
    /// </summary>
    public (IReadOnlyList<Vec2> Corners, IReadOnlyList<double>? Rises) Outline() =>
        Kind == "circle" && Centre is { } c && Radius is { } r ? ArcOutline.Circle(c, r)
        : WholeEllipse() is { } e ? (e.Corners, null)
        : (Points, null);

    /// <summary>A whole drafted ellipse (audit S4) as an outline's corners (its axes' ends) and elliptical edges; null for any other shape.</summary>
    (List<Vec2> Corners, EllipseEdge?[] Edges)? WholeEllipse()
    {
        if (Kind != "ellipse" || !Closed || Shape["c"] is not JsonArray { Count: >= 2 } c || Shape["m"] is not JsonArray { Count: >= 2 } m || Json.Number(Shape["ratio"]) is not { } ratio) return null;
        return EllipseEdge.Whole(new Vec2(Json.Number(c[0]) ?? 0, Json.Number(c[1]) ?? 0), new Vec2(Json.Number(m[0]) ?? 0, Json.Number(m[1]) ?? 0), ratio);
    }

    /// <summary>The outline's elliptical edges, as an <c>ellipses</c> argument (a whole drafted ellipse's four); null for any other shape.</summary>
    public JsonArray? Ellipses() => WholeEllipse() is { } e ? new JsonArray([.. e.Edges.Select(x => (JsonNode?)x?.ToJson())]) : null;
}

/// <summary>
/// An option a converter offers: its key, label, kind (length, number, choice, bool, type: a type of <see cref="TypeClass"/>),
/// default and choices; a choice's words to show (<see cref="ChoiceLabels"/>, one per choice) when its values are ids.
/// </summary>
public sealed record LineworkOption(string Key, string Label, string Kind, JsonNode? Default = null, string[]? Choices = null, string? TypeClass = null, string[]? ChoiceLabels = null)
{
    public JsonObject ToJson()
    {
        var o = new JsonObject { ["key"] = Key, ["label"] = Label, ["kind"] = Kind, ["default"] = Default?.DeepClone() };
        if (Choices != null) o["choices"] = new JsonArray(Choices.Select(c => (JsonNode)c).ToArray());
        if (ChoiceLabels != null && Choices != null && ChoiceLabels.Length == Choices.Length) o["choiceLabels"] = new JsonArray(ChoiceLabels.Select(c => (JsonNode)c).ToArray());
        if (TypeClass != null) o["typeClass"] = TypeClass;
        return o;
    }
}

/// <summary>
/// What drafted linework can become, registered by its module as a service: walls, floors, beams, columns, railings, bars…
/// <see cref="Takes"/>: <c>open</c> lines, <c>closed</c> outlines, <c>any</c>, or <c>points</c> (circles and small closed shapes
/// stand for where something goes); <see cref="Views"/>: the view kinds it works in (a plan's model x and y; a bar any 2D view).
/// <see cref="Steps"/> gives each shape's commands (one undo step for all), or refuses it with why.
/// <see cref="Together"/>, when set, makes one element of many shapes (a terrain of its contours, a road of lines meeting end
/// to end): <see cref="Steps"/> then only checks each shape (its steps are left out), and <see cref="Together"/> takes those it
/// passed and gives the steps of each group with the shapes it is made of.
/// </summary>
public sealed record LineworkConverter(string Id, string Label, string Module, string Takes, string[] Views, IReadOnlyList<LineworkOption> Options,
    Func<IQueryContext, Entity, LineworkShape, JsonObject, IReadOnlyList<TableStep>> Steps, string? Description = null)
{
    public Func<IQueryContext, Entity, IReadOnlyList<LineworkShape>, JsonObject, IReadOnlyList<LineworkGroup>>? Together { get; init; }

    public bool Accepts(LineworkShape s) => Takes switch
    {
        "closed" => s.Closed,
        "open" => !s.Closed || s.Kind is "polyline",
        _ => true,
    };

    public JsonObject ToJson() => new()
    {
        ["id"] = Id, ["label"] = Label, ["module"] = Module, ["takes"] = Takes, ["description"] = Description,
        ["views"] = new JsonArray(Views.Select(v => (JsonNode)v).ToArray()),
        ["options"] = new JsonArray(Options.Select(o => (JsonNode)o.ToJson()).ToArray()),
    };
}

/// <summary>What a <see cref="LineworkConverter.Together"/> converter makes of several shapes: their ids (the first carries the steps) and the steps.</summary>
public sealed record LineworkGroup(IReadOnlyList<string> Ids, IReadOnlyList<TableStep> Steps);

public static class LineworkConversion
{
    public static IEnumerable<LineworkConverter> Converters(IServices s) => s.All<LineworkConverter>();

    public static LineworkConverter Converter(IServices s, string? id) =>
        Converters(s).FirstOrDefault(c => c.Id == id) ?? throw new CommandException($"There is no conversion „{id}” in this edition.");

    /// <summary>A plan's storey (where what a plan's linework becomes stands).</summary>
    public static Entity PlanStorey(IModelView m, Entity view) =>
        view.Str("kind") == "plan" && m.Get(view.Str("storeyId") ?? "") is { Class: IfcClass.BuildingStorey } s ? s
        : throw new CommandException("Linework becomes walls, floors, beams and columns on a plan: its storey is theirs.");

    /// <summary>A yes or no option, its default when not given.</summary>
    public static bool Flag(JsonObject options, string key, bool fallback) => options[key] is JsonValue v && v.TryGetValue<bool>(out var b) ? b : fallback;

    /// <summary>Refused for a target whose edges are straight only: a circle or an arc, with what to draw instead.</summary>
    public static void Straight(LineworkShape s, string what)
    {
        if (s.Curved) throw new CommandException($"{what} has straight edges only: the {s.Kind} is not converted (draw it as a polyline).");
    }

    /// <summary>The value of a number option (a length in metres).</summary>
    public static double? Num(JsonObject options, string key) => Json.Number(options[key]);

    /// <summary>Points as commands take them.</summary>
    public static JsonArray Points(IEnumerable<Vec2> pts) => new(pts.Select(p => (JsonNode)new JsonArray(Math.Round(p.X, 9), Math.Round(p.Y, 9))).ToArray());
}
