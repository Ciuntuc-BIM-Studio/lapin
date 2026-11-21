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
/// geometric stiffness of their axial forces, iterated), off by default; and (§14.14) whether the ultimate combinations take
/// the global sway imperfections (EN 1993-1-1 5.3.2: ±X, ±Y), off when the project says nothing (<see cref="ImperfectionsSaid"/>).
/// </summary>
public sealed record AnalysisSettings(string Shells = "both", double MeshSize = AnalysisSettings.DefaultMesh, bool SecondOrder = false, bool Imperfections = false)
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
        var imperfections = o["imperfections"] is JsonValue iv && iv.TryGetValue<bool>(out var ib) && ib;
        return new(shells, size, second, imperfections) { ImperfectionsSaid = o["imperfections"] is JsonValue };
    }

    /// <summary>The project says whether it takes imperfections (true or false): second order switched on leaves them as said.</summary>
    public bool ImperfectionsSaid { get; init; }

    public JsonObject ToJson() => new()
    {
        ["shells"] = Shells, ["meshSize"] = MeshSize, ["perSide"] = PerSide, ["secondOrder"] = SecondOrder, ["imperfections"] = Imperfections,
        ["default"] = new JsonObject { ["shells"] = "both", ["meshSize"] = DefaultMesh, ["secondOrder"] = false, ["imperfections"] = false },
    };

    /// <summary>
    /// {shells?, meshSize?, secondOrder?, imperfections?}: written onto the project's own; null takes one back to the default,
    /// absent leaves it. Second order switched on in a project that has never said whether it takes the imperfections
    /// switches them on too (§14.14: second order is complete only with them); false is kept, so it is not undone later.
    /// </summary>
    public static void Write(ICommandContext ctx, JsonObject a)
    {
        if (!a.ContainsKey("shells") && !a.ContainsKey("meshSize") && !a.ContainsKey("secondOrder") && !a.ContainsKey("imperfections"))
            throw new CommandException("Say the shells, the mesh size, second order, the imperfections or some of them.");
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
            else if (a["secondOrder"] is JsonValue sv && sv.TryGetValue<bool>(out var so))
            {
                if (so)
                {
                    own["secondOrder"] = true;
                    if (!a.ContainsKey("imperfections") && !own.ContainsKey("imperfections")) own["imperfections"] = true;
                }
                else own.Remove("secondOrder");
            }
            else throw new CommandException("Second order is true or false.");
        }
        if (a.ContainsKey("imperfections"))
        {
            if (a["imperfections"] is null) own.Remove("imperfections");
            else if (a["imperfections"] is JsonValue iv && iv.TryGetValue<bool>(out var imp)) own["imperfections"] = imp;
            else throw new CommandException("The imperfections are true or false.");
        }
        attrs.Remove(Key);
        if (own.Count > 0) attrs[Key] = own;
        ctx.Replace(project.With(attrs: attrs));
    }
}
