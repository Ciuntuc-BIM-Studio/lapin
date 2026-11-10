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
/// </summary>
public sealed class MaterialHatchUpgrade : IProjectUpgrade
{
    /// <summary>The project's mark of the table its materials' hatches are suggested by (a new project has today's).</summary>
    public const string RevKey = "hatchRev";
    /// <summary>Today's table: Revit's default materials' cut patterns (decision 460).</summary>
    public const int Rev = 2;

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
                return e.With(attrs: pa);
            }
            return e.Class == IfcClass.Material ? Kept(e, saved) : e;
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
