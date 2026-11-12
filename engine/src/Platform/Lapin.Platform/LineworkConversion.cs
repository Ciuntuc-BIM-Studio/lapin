using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// A drafted shape as a converter reads it (docs/plan-user-extensibility.md §7.4): its id, its kind (line, polyline, circle, arc),
/// its JSON as drafting keeps it (in the view's metres: a plan's are the model's x and y), its points in chords, whether it is
/// closed, its layer; a circle's or an arc's centre and radius; an arc's point halfway along it.
/// </summary>
public sealed record LineworkShape(string Id, string Kind, JsonObject Shape, IReadOnlyList<Vec2> Points, bool Closed, string? Layer, Vec2? Centre = null, double? Radius = null, Vec2? Mid = null)
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
}

/// <summary>An option a converter offers: its key, label, kind (length, number, choice, bool, type: a type of <see cref="TypeClass"/>), default and choices.</summary>
public sealed record LineworkOption(string Key, string Label, string Kind, JsonNode? Default = null, string[]? Choices = null, string? TypeClass = null)
{
    public JsonObject ToJson()
    {
        var o = new JsonObject { ["key"] = Key, ["label"] = Label, ["kind"] = Kind, ["default"] = Default?.DeepClone() };
        if (Choices != null) o["choices"] = new JsonArray(Choices.Select(c => (JsonNode)c).ToArray());
        if (TypeClass != null) o["typeClass"] = TypeClass;
        return o;
    }
}

/// <summary>
/// What drafted linework can become, registered by its module as a service: walls, floors, beams, columns, railings, bars…
/// <see cref="Takes"/>: <c>open</c> lines, <c>closed</c> outlines, <c>any</c>, or <c>points</c> (circles and small closed shapes
/// stand for where something goes); <see cref="Views"/>: the view kinds it works in (a plan's model x and y; a bar any 2D view).
/// <see cref="Steps"/> gives each shape's commands (one undo step for all), or refuses it with why.
/// </summary>
public sealed record LineworkConverter(string Id, string Label, string Module, string Takes, string[] Views, IReadOnlyList<LineworkOption> Options,
    Func<IQueryContext, Entity, LineworkShape, JsonObject, IReadOnlyList<TableStep>> Steps, string? Description = null)
{
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

    /// <summary>An option's value: given, else its default.</summary>
    public static JsonNode? Option(JsonObject options, LineworkOption o) => options[o.Key] ?? o.Default;
}

public static class LineworkConversion
{
    public static IEnumerable<LineworkConverter> Converters(IServices s) => s.All<LineworkConverter>();

    public static LineworkConverter Converter(IServices s, string? id) =>
        Converters(s).FirstOrDefault(c => c.Id == id) ?? throw new CommandException($"There is no conversion „{id}” in this edition.");

    /// <summary>A plan's storey (where what a plan's linework becomes stands).</summary>
    public static Entity PlanStorey(IModelView m, Entity view) =>
        view.Str("kind") == "plan" && m.Get(view.Str("storeyId") ?? "") is { Class: IfcClass.BuildingStorey } s ? s
        : throw new CommandException("Linework becomes walls, floors, beams and columns on a plan: its storey is theirs.");

    /// <summary>The value of a number option (a length in metres).</summary>
    public static double? Num(JsonObject options, string key) => Json.Number(options[key]);

    /// <summary>Points as commands take them.</summary>
    public static JsonArray Points(IEnumerable<Vec2> pts) => new(pts.Select(p => (JsonNode)new JsonArray(Math.Round(p.X, 9), Math.Round(p.Y, 9))).ToArray());
}
