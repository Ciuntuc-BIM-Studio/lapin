using System.Text.Json.Nodes;
using Lapin.Drawing;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// A project saved before decision 456 keeps the cut hatches it drew: each material that took its section hatch from its
/// name, where the new suggestion differs from decision 325's (brick and masonry were BRICK, an unknown name nothing), is
/// given the old one as its own (an unknown name an explicit none), its other fields as they were; then the project is
/// marked. Materials of their own hatch, membranes and those the new table draws the same are left as they are.
/// </summary>
public sealed class MaterialHatchUpgrade : IProjectUpgrade
{
    /// <summary>The project's mark that its materials' hatches are kept as this version suggests them (a new project has it).</summary>
    public const string RevKey = "hatchRev";

    public (IReadOnlyList<Entity> Entities, IReadOnlyList<Relation> Relations) Upgrade(IReadOnlyList<Entity> entities, IReadOnlyList<Relation> relations)
    {
        if (entities.FirstOrDefault(e => e.Class == IfcClass.Project) is not { } project || project.Attrs[RevKey] != null) return (entities, relations);
        return (entities.Select(e =>
        {
            if (e.Id == project.Id)
            {
                var pa = e.CloneAttrs();
                pa[RevKey] = 1;
                return e.With(attrs: pa);
            }
            return e.Class == IfcClass.Material ? Kept(e) : e;
        }).ToList(), relations);
    }

    static Entity Kept(Entity material)
    {
        // A hatch of its own (or an explicit none) stays.
        if (material.Attrs["section"] is JsonObject s && s.ContainsKey("hatch")) return material;
        var now = MaterialGraphics.Resolve(material, "section");
        if (now.Membrane) return material;
        var before = MaterialGraphics.LegacyFromName(material.Name);
        if (before == now.Hatch) return material;
        var attrs = material.CloneAttrs();
        var section = attrs["section"] as JsonObject ?? [];
        section["hatch"] = before ?? "";
        attrs["section"] = section;
        return material.With(attrs: attrs);
    }
}
