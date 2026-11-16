using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Steel;

/// <summary>
/// What drafted linework becomes in steel (Convert to…, audit S12): a steel member of the profile chosen on each straight piece
/// of a plan's lines (on the storey's top, as the Member tool lays one), a steel column of that profile where each circle,
/// outline or mark stands (as the Column tool stands one), and a member read from each closed outline drawn as a band (Convert
/// to member, decision 268: on a plan, a section or an elevation, its width the profile's). One undo step for all.
/// </summary>
public static class LineworkConverters
{
    static readonly string[] Plan = ["plan"];

    /// <summary>The catalogue's steel profiles, by name; the first choice keeps the member type chosen.</summary>
    static readonly string[] Profiles = ["", .. Section.Catalogue.Values.Where(x => x.IsProfile).Select(x => x.Name ?? x.Label).Distinct()];
    static readonly string[] ProfileLabels = ["(the type's)", .. Profiles.Skip(1)];

    public static readonly LineworkConverter Members = new("steelMembers", "Steel members", "steel", "any", Plan,
    [
        new("typeId", "Member type", "type", TypeClass: SteelModule.MemberType),
        new("profile", "Profile", "choice", JsonValue.Create(""), Profiles, ChoiceLabels: ProfileLabels),
        new("role", "As", "choice", JsonValue.Create("MEMBER"), SteelModule.Predefined),
    ],
    (q, view, s, o) =>
    {
        var storey = LineworkConversion.PlanStorey(q.Model, view);
        if (s.Kind == "circle") throw new CommandException("A circle has no straight pieces: it marks where a column goes.");
        // A member runs along a straight axis: an arc is not one.
        if (s.Kind == "arc") throw new CommandException("A steel member is straight: the arc is not converted (draw its chords as a polyline).");
        var steps = new List<TableStep>();
        foreach (var (a, b) in s.Pieces())
        {
            if ((b - a).Length < 1e-3) continue;
            var args = new JsonObject { ["storeyId"] = storey.Id, ["x1"] = a.X, ["y1"] = a.Y, ["x2"] = b.X, ["y2"] = b.Y };
            if ((string?)o["typeId"] is { Length: > 0 } t) args["typeId"] = t;
            else if ((string?)o["profile"] is { Length: > 0 } p) args["section"] = p;
            if ((string?)o["role"] is { Length: > 0 } role && role != "MEMBER") args["predefinedType"] = role;
            steps.Add(new("steel.addMember", args, $"A steel member {Math.Round((b - a).Length, 3)} m on {storey.Name}"));
        }
        if (steps.Count == 0) throw new CommandException("It has no length.");
        return steps;
    }, "A steel member of the profile chosen on each straight piece, on the storey's top.");

    public static readonly LineworkConverter Columns = new("steelColumns", "Steel columns", "steel", "any", Plan,
    [
        new("profile", "Profile", "choice", JsonValue.Create("HEB 200"), Profiles[1..]),
        new("followShape", "Turned with the shape", "bool", JsonValue.Create(true)),
    ],
    (q, view, s, o) =>
    {
        var storey = LineworkConversion.PlanStorey(q.Model, view);
        var (at, rotation) = Lapin.Structure.LineworkConverters.Spot(s);
        var args = new JsonObject { ["storeyId"] = storey.Id, ["x"] = at.X, ["y"] = at.Y, ["section"] = (string?)o["profile"] is { Length: > 0 } p ? p : "HEB 200" };
        if (LineworkConversion.Flag(o, "followShape", true)) args["rotation"] = rotation;
        return [new("structure.addColumn", args, $"A steel column {(string)args["section"]!} at ({Math.Round(at.X, 3)}, {Math.Round(at.Y, 3)}) on {storey.Name}")];
    }, "A steel column of the profile chosen where each circle, outline or mark stands.");

    public static readonly LineworkConverter Bands = new("steelOutlines", "Steel members from outlines", "steel", "closed", ["plan", "section", "elevation", "plane"],
        [new("typeId", "Member type (when its width fits)", "type", TypeClass: SteelModule.MemberType)],
    (q, view, s, o) =>
    {
        if (s.Kind != "polyline") throw new CommandException("Not a closed outline: draw the member's outline as a closed polyline or a rectangle.");
        var (band, _, problem) = SketchMembers.Read(q.Model, q.Services, s.Id);
        if (band == null) throw new CommandException(problem ?? "Not a band: two long parallel edges, an edge across each end.");
        var args = new JsonObject { ["ids"] = new JsonArray(s.Id), ["keep"] = true };
        if ((string?)o["typeId"] is { Length: > 0 } t) args["typeId"] = t;
        return [new("steel.convertSketch", args, $"A member {Math.Round(band.Width * 1000)} mm wide, its ends' cuts and copes as drawn")];
    }, "A member read from each closed outline drawn as a band: its width the profile's, its ends' slants cuts, a notch a cope (on a section).");

    public static void Register(IModuleRegistry r)
    {
        foreach (var c in new[] { Members, Columns, Bands }) r.Service<LineworkConverter>(c);
    }
}
