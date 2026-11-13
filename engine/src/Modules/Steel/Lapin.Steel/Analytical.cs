using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Platform.Analysis;
using Lapin.Structure;

namespace Lapin.Steel;

/// <summary>
/// Steel in the analytical model (docs/jg-structures.md): each steel member on its axis through its section's box centre (its
/// insertion point's offset taken out), its depth along the member's own frame (so its rotation is the member's); a column
/// base connection a support on its column's foot, pinned or fixed as the connection is; a secondary connection (a cleat,
/// a gusset, a fly brace's) the members it joins, each fixed to its host where their axes come closest (eccentric).
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
        // A hall's roof as the planes over its rafters' top (from the columns' outer faces, past the gables as far as the
        // purlins), carried by its purlins: the snow and the wind on it go to them (docs/plan-fem.md §9.9).
        foreach (var hall in m.OfClass(Halls.Class))
        {
            var s = Halls.SpecOf(hall);
            var purlins = Halls.Members(m, hall.Id).Where(x => Halls.KindOfRole(s, x.Key) == "purlin").ToDictionary(x => x.Value.Id, _ => "purlin");
            if (purlins.Count == 0) continue;
            var level = Spatial.StoreyOf(m, hall.Id)?.Num("elevation") ?? MemberAxes.Level(m, m.Get(purlins.Keys.First())!);
            var (x0, x1) = (-s.Column.Depth / 2, s.Span + s.Column.Depth / 2);
            var (y0, y1) = (-s.PurlinOverhang, s.Length + s.PurlinOverhang);
            Vec3 P(double x, double y) => Halls.Plan(s, x, y).At(level + s.Top(x));
            List<IReadOnlyList<Vec3>> planes = s.Mono
                ? [new[] { P(x0, y0), P(x1, y0), P(x1, y1), P(x0, y1) }]
                : [new[] { P(x0, y0), P(s.ApexX, y0), P(s.ApexX, y1), P(x0, y1) }, new[] { P(s.ApexX, y0), P(x1, y0), P(x1, y1), P(s.ApexX, y1) }];
            into.Roofs.Add(new AnalyticalRoof(hall.Id, planes) { Members = purlins, Name = hall.Name, StoreyId = Spatial.StoreyOf(m, hall.Id)?.Id });
        }
        foreach (var node in m.OfClass(SteelNodes.Class))
        {
            var kind0 = SteelNodes.KindOf(node);
            if (SteelNodes.SecondaryKinds.Contains(kind0))
            {
                var joined = SteelNodes.MembersOf(node).Select(j => j.Id).Distinct().ToList();
                var linkSource = $"{SteelNodes.Label(kind0).ToLowerInvariant()} „{node.Name}”";
                // A cleat or gusset fixes each member to its host (the first); a fly brace runs from the purlin or rail to the rafter's or column's flange.
                for (var i = 1; i < joined.Count; i++) into.Links.Add(new AnalyticalLink(joined[0], joined[i], linkSource));
                if (kind0 == "fly" && joined.Count >= 3) into.Links.Add(new AnalyticalLink(joined[1], joined[2], linkSource));
                continue;
            }
            if (kind0 != "base" || SteelNodes.MembersOf(node).FirstOrDefault() is not { } column) continue;
            var spec = SteelNodes.Spec(m, node);
            var kind = spec.Base == "fixed" ? "fixed" : "pinned";
            var source = $"steel column base „{node.Name}”: {kind}";
            into.Supports.Add(new AnalyticalSupport(node.Id, node.Name, kind == "fixed" ? SupportSpec.Fixed(source) : SupportSpec.Pinned(source),
                ForFrame: column.Id, AtEnd: column.End == "end"));
        }
    }
}
