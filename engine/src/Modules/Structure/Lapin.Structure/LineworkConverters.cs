using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Structure;

/// <summary>
/// What a plan's drafted linework becomes in structure (docs/plan-user-extensibility.md §7.4): a beam or a strip foundation on each
/// straight piece or arc; a column or a pad footing where each circle, closed outline or short mark stands (turned with the
/// outline's longest side). Each on the plan's storey, one undo step for all.
/// </summary>
public static class LineworkConverters
{
    static readonly string[] Plan = ["plan"];

    static IReadOnlyList<TableStep> Along(IQueryContext q, Entity view, LineworkShape s, JsonObject o, string command, string what)
    {
        var storey = LineworkConversion.PlanStorey(q.Model, view);
        if (s.Kind == "circle") throw new CommandException("A circle has no straight pieces: it marks where a column goes.");
        var steps = new List<TableStep>();
        foreach (var (a, b) in s.Pieces())
        {
            if ((b - a).Length < 1e-3) continue;
            var args = new JsonObject { ["storeyId"] = storey.Id, ["x1"] = a.X, ["y1"] = a.Y, ["x2"] = b.X, ["y2"] = b.Y };
            // An arc: through its middle point.
            if (s.Kind == "arc" && s.Mid is { } mid) { args["x3"] = mid.X; args["y3"] = mid.Y; }
            if ((string?)o["typeId"] is { Length: > 0 } t) args["typeId"] = t;
            if (LineworkConversion.Num(o, "topOffset") is { } top) args["topOffset"] = top;
            steps.Add(new(command, args, $"{what} {Math.Round((b - a).Length, 3)} m on {storey.Name}"));
        }
        if (steps.Count == 0) throw new CommandException("It has no length.");
        return steps;
    }

    /// <summary>Where a shape stands and how it turns: its middle, and its longest side's angle (°, 0 to 180; 0 for a circle).</summary>
    public static (Vec2 At, double Rotation) Spot(LineworkShape s)
    {
        if (s.Kind is "circle" || s.Points.Count < 2) return (s.Middle, 0);
        var (a, b) = s.Pieces().OrderByDescending(p => (p.B - p.A).Length).First();
        var angle = Math.Atan2(b.Y - a.Y, b.X - a.X) * 180 / Math.PI;
        angle = ((angle % 180) + 180) % 180;
        return (s.Closed ? s.Middle : (s.Points[0] + s.Points[^1]) * 0.5, Math.Round(angle, 6));
    }

    static IReadOnlyList<TableStep> At(IQueryContext q, Entity view, LineworkShape s, JsonObject o, string command, string what)
    {
        var storey = LineworkConversion.PlanStorey(q.Model, view);
        var (at, rotation) = Spot(s);
        var args = new JsonObject { ["storeyId"] = storey.Id, ["x"] = at.X, ["y"] = at.Y };
        if (o["followShape"] is not JsonValue fv || !fv.TryGetValue<bool>(out var follow) || follow) args["rotation"] = rotation;
        if ((string?)o["typeId"] is { Length: > 0 } t) args["typeId"] = t;
        return [new(command, args, $"{what} at ({Math.Round(at.X, 3)}, {Math.Round(at.Y, 3)}) on {storey.Name}")];
    }

    public static readonly LineworkConverter Beams = new("beams", "Beams", "structure", "any", Plan,
        [new("typeId", "Beam type", "type", TypeClass: IfcClass.BeamType), new("topOffset", "Top below the level above", "length", JsonValue.Create(0.0))],
        (q, v, s, o) => Along(q, v, s, o, "structure.addBeam", "A beam"), "A beam on each straight piece or arc.");

    public static readonly LineworkConverter Strips = new("stripFootings", "Strip foundations", "structure", "any", Plan,
        [new("typeId", "Strip foundation type", "type", TypeClass: IfcClass.FootingType)],
        (q, v, s, o) => Along(q, v, s, o, "structure.addStripFooting", "A strip foundation"), "A strip foundation on each straight piece or arc.");

    public static readonly LineworkConverter Columns = new("columns", "Columns", "structure", "any", Plan,
        [new("typeId", "Column type", "type", TypeClass: IfcClass.ColumnType), new("followShape", "Turned with the shape", "bool", JsonValue.Create(true))],
        (q, v, s, o) => At(q, v, s, o, "structure.addColumn", "A column"), "A column where each circle, outline or mark stands.");

    public static readonly LineworkConverter Pads = new("padFootings", "Pad footings", "structure", "any", Plan,
        [new("typeId", "Pad footing type", "type", TypeClass: IfcClass.FootingType), new("followShape", "Turned with the shape", "bool", JsonValue.Create(true))],
        (q, v, s, o) => At(q, v, s, o, "structure.addPadFooting", "A pad footing"), "A pad footing where each circle, outline or mark stands.");

    public static void Register(IModuleRegistry r)
    {
        foreach (var c in new[] { Beams, Strips, Columns, Pads }) r.Service<LineworkConverter>(c);
    }
}
