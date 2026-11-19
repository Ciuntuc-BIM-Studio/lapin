using System.Text.Json.Nodes;
using Lapin.Drawing;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// A project saved by an earlier build keeps the cut hatches it drew (decisions 456 and 460): each material that took
/// its section hatch from its name, where today's suggestion differs from the one the project was saved with (decision
/// 325's when it has no <see cref="RevKey"/>: brick in courses, an unknown name nothing; decision 456's at 1: masonry 45°
/// lines, an unknown name 45° lines), is given that one as its own (none as an explicit none), its other fields as they
/// were; then the project is marked <see cref="Rev"/>. Materials of their own hatch, membranes and those today's table
/// draws the same are left as they are.
/// <para>
/// From <c>hatchRev</c> 3 (decision 679) a facade's and a floor finish's hatch suggested by the name is drawn at the
/// object's real size (BRICK_240X63, TILE_300X300…). A project saved before is marked <see cref="PaperKey"/>, so its
/// elevations and plans go on suggesting the paper patterns they drew (BRICK, TILES…), and nothing it drew changes. A mark
/// on the project rather than on each material (as revisions 1 and 2 did): a floor finish takes a material's own elevation
/// hatch over its name's suggestion, so a pattern pinned on the material would have changed old plans.
/// </para>
/// <para>
/// From <c>hatchRev</c> 4 (decision 725) a material that does not say how its hatch aligns aligns it with the element
/// (<see cref="HatchAlign.Element"/>: a wall's hatch along its axis in a plan, turning with the wall). A project saved before
/// is marked <see cref="ViewAlignKey"/>, so its hatches stay fixed to the sheet (<see cref="HatchAlign.View"/>) as it drew them.
/// </para>
/// </summary>
public sealed class MaterialHatchUpgrade : IProjectUpgrade
{
    /// <summary>The project's mark of the table its materials' hatches are suggested by (a new project has today's).</summary>
    public const string RevKey = "hatchRev";
    /// <summary>
    /// Today's table: Revit's default materials' cut patterns (decision 460); facades and floor finishes at real size (decision
    /// 679); hatches aligned with their elements unless a material says otherwise (decision 725).
    /// </summary>
    public const int Rev = 4;

    /// <summary>The project's mark that its surfaces' (elevations', floor finishes') suggested hatches are the paper ones of before decision 679.</summary>
    public const string PaperKey = "paperSurfaceHatches";

    /// <summary>The project's mark that a hatch its material does not align is fixed to the view, as before decision 725.</summary>
    public const string ViewAlignKey = "viewAlignedHatches";

    /// <summary>Whether a project suggests the paper surface hatches of before decision 679 (it was saved before them).</summary>
    public static bool PaperSurfaces(IModelView m) => PaperSurfaces(Spatial.Project(m));

    public static bool PaperSurfaces(Entity? project) => project?.Attrs[PaperKey] is JsonValue v && v.TryGetValue<bool>(out var b) && b;

    /// <summary>
    /// How a hatch its material does not align aligns in the project: with its element (<see cref="HatchAlign.Element"/>), or
    /// fixed to the view (<see cref="HatchAlign.View"/>) in a project saved before decision 725.
    /// </summary>
    public static string AlignDefault(IModelView m) => AlignDefault(Spatial.Project(m));

    public static string AlignDefault(Entity? project) =>
        project?.Attrs[ViewAlignKey] is JsonValue v && v.TryGetValue<bool>(out var b) && b ? HatchAlign.View : HatchAlign.Element;

    public (IReadOnlyList<Entity> Entities, IReadOnlyList<Relation> Relations) Upgrade(IReadOnlyList<Entity> entities, IReadOnlyList<Relation> relations)
    {
        if (entities.FirstOrDefault(e => e.Class == IfcClass.Project) is not { } project) return (entities, relations);
        var saved = Json.Number(project.Attrs[RevKey]) is { } n ? (int)n : 0;
        if (saved >= Rev) return (entities, relations);
        return (entities.Select(e =>
        {
            if (e.Id == project.Id)
            {
                var pa = e.CloneAttrs();
                pa[RevKey] = Rev;
                if (saved < 3) pa[PaperKey] = true;
                if (saved < 4) pa[ViewAlignKey] = true;
                return e.With(attrs: pa);
            }
            return e.Class == IfcClass.Material && saved < 2 ? Kept(e, saved) : e;
        }).ToList(), relations);
    }

    static Entity Kept(Entity material, int saved)
    {
        // A hatch of its own (or an explicit none) stays.
        if (material.Attrs["section"] is JsonObject s && s.ContainsKey("hatch")) return material;
        var now = MaterialGraphics.Resolve(material, "section");
        if (now.Membrane) return material;
        var before = MaterialGraphics.LegacyFromName(material.Name, saved);
        if (before == now.Hatch) return material;
        var attrs = material.CloneAttrs();
        var section = attrs["section"] as JsonObject ?? [];
        section["hatch"] = before ?? "";
        attrs["section"] = section;
        return material.With(attrs: attrs);
    }
}
