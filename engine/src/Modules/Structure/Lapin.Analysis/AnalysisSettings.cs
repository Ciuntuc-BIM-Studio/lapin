using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Analysis;

/// <summary>
/// How the project's analysis is run (docs/plan-fem.md §10.10, §10.12), on the IfcProject (<c>analysisOptions</c>:
/// <c>{shells, meshSize}</c>), set by <c>analysis.setOptions</c>: which surfaces are analysed as shells (<see cref="Shells"/>:
/// both, slabs, walls or none; a slab or wall left out works as in S1: a slab's loads by the 45° rule, a wall a line support),
/// and the mesh's target size (metres; 0.5 by default, smaller on a small piece: at least <see cref="PerSide"/> elements
/// across its narrowest width); and (§14.3) whether the static combinations are solved to second order (P-Δ: the bars'
/// geometric stiffness of their axial forces, iterated), off by default.
/// </summary>
public sealed record AnalysisSettings(string Shells = "both", double MeshSize = AnalysisSettings.DefaultMesh, bool SecondOrder = false)
{
    public const string Key = "analysisOptions";
    public const double DefaultMesh = 0.5, MinMesh = 0.05, MaxMesh = 5;
    /// <summary>The least number of elements across a piece's narrowest width (§10.12).</summary>
    public const int PerSide = 4;
    public static readonly string[] ShellChoices = ["both", "slabs", "walls", "none"];

    public bool Slabs => Shells is "both" or "slabs";
    public bool Walls => Shells is "both" or "walls";

    public static AnalysisSettings Of(IModelView m)
    {
        if (Spatial.Project(m)?.Attrs[Key] is not JsonObject o) return new();
        var shells = (string?)o["shells"] is { } s && ShellChoices.Contains(s) ? s : "both";
        var size = Json.Number(o["meshSize"]) is { } v && double.IsFinite(v) && v >= MinMesh && v <= MaxMesh ? v : DefaultMesh;
        var second = o["secondOrder"] is JsonValue so && so.TryGetValue<bool>(out var b) && b;
        return new(shells, size, second);
    }

    public JsonObject ToJson() => new()
    {
        ["shells"] = Shells, ["meshSize"] = MeshSize, ["perSide"] = PerSide, ["secondOrder"] = SecondOrder,
        ["default"] = new JsonObject { ["shells"] = "both", ["meshSize"] = DefaultMesh, ["secondOrder"] = false },
    };

    /// <summary>{shells?, meshSize?, secondOrder?}: written onto the project's own; null takes one back to the default, absent leaves it.</summary>
    public static void Write(ICommandContext ctx, JsonObject a)
    {
        if (!a.ContainsKey("shells") && !a.ContainsKey("meshSize") && !a.ContainsKey("secondOrder")) throw new CommandException("Say the shells, the mesh size, second order or some of them.");
        var project = Spatial.Project(ctx.Model) ?? throw new CommandException("The model has no project.");
        var attrs = project.CloneAttrs();
        var own = attrs[Key] is JsonObject had ? (JsonObject)had.DeepClone() : new JsonObject();
        if (a.ContainsKey("shells"))
        {
            if (a["shells"] is null) own.Remove("shells");
            else if ((string?)a["shells"] is { } s && ShellChoices.Contains(s)) own["shells"] = s;
            else throw new CommandException($"The shells are one of {string.Join(", ", ShellChoices)}.");
        }
        if (a.ContainsKey("meshSize"))
        {
            if (a["meshSize"] is null) own.Remove("meshSize");
            else if (Json.Number(a["meshSize"]) is { } v && double.IsFinite(v) && v >= MinMesh && v <= MaxMesh) own["meshSize"] = v;
            else throw new CommandException($"The mesh size is a length in metres from {MinMesh.ToString(CultureInfo.InvariantCulture)} to {MaxMesh.ToString(CultureInfo.InvariantCulture)}.");
        }
        if (a.ContainsKey("secondOrder"))
        {
            if (a["secondOrder"] is null) own.Remove("secondOrder");
            else if (a["secondOrder"] is JsonValue sv && sv.TryGetValue<bool>(out var so)) { if (so) own["secondOrder"] = true; else own.Remove("secondOrder"); }
            else throw new CommandException("Second order is true or false.");
        }
        attrs.Remove(Key);
        if (own.Count > 0) attrs[Key] = own;
        ctx.Replace(project.With(attrs: attrs));
    }
}
