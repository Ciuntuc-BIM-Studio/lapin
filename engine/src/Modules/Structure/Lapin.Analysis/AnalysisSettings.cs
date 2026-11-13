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
/// across its narrowest width).
/// </summary>
public sealed record AnalysisSettings(string Shells = "both", double MeshSize = AnalysisSettings.DefaultMesh)
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
        return new(shells, size);
    }

    public JsonObject ToJson() => new()
    {
        ["shells"] = Shells, ["meshSize"] = MeshSize, ["perSide"] = PerSide,
        ["default"] = new JsonObject { ["shells"] = "both", ["meshSize"] = DefaultMesh },
    };

    /// <summary>{shells?, meshSize?}: written onto the project's own; null takes one back to the default, absent leaves it.</summary>
    public static void Write(ICommandContext ctx, JsonObject a)
    {
        if (!a.ContainsKey("shells") && !a.ContainsKey("meshSize")) throw new CommandException("Say the shells, the mesh size or both.");
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
        attrs.Remove(Key);
        if (own.Count > 0) attrs[Key] = own;
        ctx.Replace(project.With(attrs: attrs));
    }
}
