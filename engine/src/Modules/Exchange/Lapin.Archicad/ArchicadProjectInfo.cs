using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Platform.Documents;

namespace Lapin.Archicad;

/// <summary>
/// Archicad's Project Info into the project's information (docs/plan-archicad.md, A1b): Tapir's <c>GetProjectInfoFields</c> mapped by the
/// table Lapin shares with Atrium Server (<see cref="ProjectInfoMapping"/>, source <c>archicad</c>). A receive fills the project's EMPTY
/// fields in its own undo step; a field the project has already with another value is never overwritten: the report lists it
/// (<c>differs</c>), and the Receive dialog lets the user take the ones ticked (<c>project.setInfo</c>, one undo step). What Archicad says
/// that no field takes (its description, keywords, autotexts…) is kept as <c>info.source.archicad</c>, shown read-only in Project
/// information, replaced at each receive.
/// </summary>
public static class ArchicadProjectInfo
{
    public const string Source = "archicad";

    public static (Dictionary<string, string> Fields, Dictionary<string, SourceValue> Extras) Map(JsonNode? raw) =>
        ProjectInfoMapping.Default.Map(Source, ProjectInfoMapping.Raw(raw));

    /// <summary>What taking Archicad's Project Info would do here.</summary>
    public static ProjectInfo.SourceReport Compare(IModelView m, JsonNode? raw)
    {
        var (fields, extras) = Map(raw);
        return ProjectInfo.Compare(m, fields, extras);
    }

    /// <summary>The empty fields filled and the extras kept, in the caller's undo step; the report says what was filled, what differs and what was kept.</summary>
    public static ProjectInfo.SourceReport Apply(IEdit edit, JsonNode? raw)
    {
        var (fields, extras) = Map(raw);
        var report = ProjectInfo.Compare(edit.Model, fields, extras);
        ProjectInfo.Take(edit, Source, report.Filled.ToDictionary(f => f.Key, f => f.Value, StringComparer.Ordinal), extras);
        return report;
    }

    /// <summary>
    /// What Send to Atrium proposes to the server (<c>archicad.projectInfo</c>): Tapir's answer as it is (<c>raw</c>, the server maps it with the
    /// same table), the project file's name when Archicad says it, and what it would do here.
    /// </summary>
    public static JsonObject Read(ArchicadClient c, IModelView m, CancellationToken ct)
    {
        var (raw, problem) = ArchicadRead.ProjectInfoFields(c, ct);
        if (raw == null) throw new CommandException(problem!);
        string? file = null;
        try
        {
            if (c.Tapir("GetProjectInfo", null, ct) is { } info)
            {
                var location = (string?)info["projectLocation"] ?? (string?)info["projectPath"];
                if (location is { Length: > 0 }) file = Path.GetFileName(location.Replace('\\', '/'));
                else if ((string?)info["projectName"] is { Length: > 0 } name) file = name.EndsWith(".pln", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".pla", StringComparison.OrdinalIgnoreCase) ? name : name + ".pln";
            }
        }
        catch (CommandException) { }
        var o = Compare(m, raw).ToJson(Source);
        o["raw"] = raw;
        o["file"] = file;
        return o;
    }
}
