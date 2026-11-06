using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Platform.Analysis;
using Lapin.Structure;

namespace Lapin.Steel;

/// <summary>
/// Steel in the analytical model (docs/jg-structures.md): each steel member on its axis through its section's box centre (its
/// insertion point's offset taken out), its depth along the member's own frame (so its rotation is the member's); a column
/// base connection a support on its column's foot, pinned or fixed as the connection is.
/// </summary>
public sealed class SteelAnalytical : IAnalyticalElements
{
    public void Contribute(IModelView m, AnalyticalContribution into)
    {
        foreach (var e in m.OfClass(SteelModule.Member))
        {
            if (MemberAxes.Of(m, e) is not { } ax) { into.LeftOut.Add(new(e.Id, e.Name, e.Class, "no length")); continue; }
            var (_, w) = ax.Frame;
            var vertical = Math.Abs(ax.U.Z) > 1 - 1e-6;
            var type = m.RelatingOf(e.Id, IfcClass.RelDefinesByType);
            var section = new FrameSection(type?.Id ?? $"member-{e.Id}", type?.Name ?? ax.Section.Label, StructureAnalytical.Shape(ax.Section),
                type != null ? $"Member type „{type.Name}”, section {ax.Section.Label}" : $"Member „{e.Name}”, section {ax.Section.Label}");
            var role = e.Str("hallRole") ?? (e.PredefinedType is { Length: > 0 } p && p != "MEMBER" ? p.ToLowerInvariant() : null);
            into.Frames.Add(new AnalyticalFrame(e.Id, e.Name, "IfcMember", vertical ? "column" : "beam", AnalyticalKinds.Members,
                [ax.At(default, 0), ax.At(default, ax.Length)], w, section, type?.Str("material"), Spatial.StoreyOf(m, e.Id)?.Id,
                ax.Section.Depth, ax.Section.Width, role, ax.Flipped));
        }
        foreach (var node in m.OfClass(SteelNodes.Class))
        {
            if (SteelNodes.KindOf(node) != "base" || SteelNodes.MembersOf(node).FirstOrDefault() is not { } column) continue;
            var spec = SteelNodes.Spec(m, node);
            var kind = spec.Base == "fixed" ? "fixed" : "pinned";
            var source = $"steel column base „{node.Name}”: {kind}";
            into.Supports.Add(new AnalyticalSupport(node.Id, node.Name, kind == "fixed" ? SupportSpec.Fixed(source) : SupportSpec.Pinned(source),
                ForFrame: column.Id, AtEnd: column.End == "end"));
        }
    }
}
