using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Steel;

/// <summary>
/// Steel's part in shop drawings (docs/plan-shop-drawings.md, decision 582): a member's features in its own frame (its holes, the
/// copes at its ends, its ends cut off square), its drawing rules (a member: front and top with its features dimensioned and a
/// section through each cope; a plate: its face and its edge), and holes drilled along a member in a run (<c>steel.addHoles</c>).
/// </summary>
public static class SteelShop
{
    public static readonly DrawingRule MemberRule = new("steel.member", "Steel member", ["front", "top"], [], true, "features", AtFeatures: true);
    public static readonly DrawingRule PlateRule = new("steel.plate", "Steel plate", ["top", "front"], [], false, "features");

    public static void Register(IModuleRegistry r)
    {
        r.Service<IElementFeatures>(new MemberFeatures());
        r.Service<IDrawingRules>(new SteelRules());
        r.Service<IMarkKinds>(new SteelMarks());

        // A run of holes drilled through a member's web or a flange: {id, station (m from where its holes are measured, along its axis),
        // count?, pitch? (m), diameter? (m), face? (web, top, bottom), across? (m from the section's middle: up the web, along a flange)}.
        r.Command("addHoles", "Holes", (ctx, a) =>
        {
            var e = ctx.Model.Get((string?)a["id"] ?? "") ?? throw new CommandException("Pick the member to drill.");
            if (e.Class is not (SteelModule.Member or IfcClass.Beam or IfcClass.Column) || MemberAxes.Of(ctx.Model, e) is not { } ax || !MemberAxes.IsSteel(ctx.Model, e))
                throw new CommandException($"„{e.Name}” is not a steel member.");
            var station = Json.Number(a["station"]) ?? throw new CommandException("Where along the member does the run start (station)?");
            var count = (int)(Json.Number(a["count"]) ?? 1);
            var pitch = Json.Number(a["pitch"]) ?? 0.08;
            var d = Json.Number(a["diameter"]) ?? 0.022;
            var across = Json.Number(a["across"]) ?? 0;
            var face = (string?)a["face"] ?? "web";
            if (count < 1 || count > 200) throw new CommandException("A run has 1 to 200 holes.");
            if (d < 0.002 || d > 0.2) throw new CommandException("A hole is 2 to 200 mm across.");
            if (count > 1 && pitch < d) throw new CommandException("The pitch is at least a hole's diameter.");
            var last = station + (count - 1) * pitch;
            if (station < -1e-9 || last > ax.Length + 1e-9) throw new CommandException($"The holes are within the member's {ax.Length * 1000:0} mm.");
            var s = ax.Section;
            (Vec3 Top, Vec3 Bottom) Through(double z) => face switch
            {
                "web" => (new Vec3(-Math.Max(s.Web, 0.001) / 2, across, z), new Vec3(Math.Max(s.Web, 0.001) / 2, across, z)),
                "top" => (new Vec3(across, s.Depth / 2, z), new Vec3(across, s.Depth / 2 - Math.Max(s.Flange, 0.001), z)),
                "bottom" => (new Vec3(across, -s.Depth / 2 + Math.Max(s.Flange, 0.001), z), new Vec3(across, -s.Depth / 2, z)),
                _ => throw new CommandException("Holes go through the web, the top flange or the bottom flange."),
            };
            var holes = MemberHoles.Of(e).ToList();
            for (var i = 0; i < count; i++)
            {
                var (top, bottom) = Through(station + i * pitch);
                holes.Add(new MemberHole(null, d / 2, [top], [bottom]));
            }
            var attrs = e.CloneAttrs();
            attrs[MemberHoles.Key] = MemberHoles.Write(holes);
            ctx.Replace(e.With(attrs: attrs));
            return new JsonObject { ["holes"] = holes.Count };
        });

        // A member's holes taken away: {ids}.
        r.Command("clearHoles", "Remove holes", (ctx, a) =>
        {
            foreach (var id in Args.Ids(a))
                if (ctx.Model.Get(id) is { } e && MemberHoles.Has(e))
                {
                    var attrs = e.CloneAttrs();
                    attrs.Remove(MemberHoles.Key);
                    ctx.Replace(e.With(attrs: attrs));
                }
            return null;
        });
    }

    sealed class SteelRules : IDrawingRules
    {
        public IEnumerable<DrawingRule> Rules() => [MemberRule, PlateRule];

        public DrawingRule? RuleFor(IModelView m, Entity e) =>
            e.Class is SteelModule.Member or IfcClass.Beam or IfcClass.Column && MemberAxes.Of(m, e) != null && MemberAxes.IsSteel(m, e) ? MemberRule
            : e.Class == "IfcPlate" || e.Str("exportAs") == "IfcPlate" ? PlateRule : null;
    }

    /// <summary>
    /// A member's features in its frame (x along it from where its holes are measured, y the section's x, z its y): its round holes
    /// (keyed by their place in its list), the copes at its ends (at the box's ends, on the flange's side), its ends cut by a plane.
    /// </summary>
    sealed class MemberFeatures : IElementFeatures
    {
        public IReadOnlyList<ElementFeature>? FeaturesOf(IModelView m, Entity e, ElementFrame f)
        {
            if (e.Class is not (SteelModule.Member or IfcClass.Beam or IfcClass.Column) || MemberAxes.Of(m, e) is not { } ax || !MemberAxes.IsSteel(m, e)) return null;
            var o = new List<ElementFeature>();
            static Vec3 Frame(Vec3 p) => new(p.Z, p.X, p.Y);
            var holes = MemberHoles.Of(e);
            for (var i = 0; i < holes.Count; i++)
            {
                var h = holes[i];
                if (!h.Round) continue;
                var (top, bottom) = (Frame(h.Top[0]), Frame(h.Bottom[0]));
                var through = bottom - top;
                if (through.Length < 1e-9) continue;
                o.Add(new ElementFeature(h.Number is { } n ? $"hole:sdnf:{n}" : $"hole:{i}", "hole", (top + bottom) * 0.5, through.Normalized(), h.Radius * 2));
            }
            var (start, end) = SteelEnd.Of(e);
            foreach (var (name, en) in new[] { ("start", start), ("end", end) })
            {
                var x0 = name == "start" ? f.Min.X : f.Max.X;
                if (en.Cut == "plane" && (en.PlanAngle != 0 || en.ElevAngle != 0))
                    o.Add(new ElementFeature($"cut:{name}", "cut", new Vec3(x0, 0, 0), new Vec3(name == "start" ? -1 : 1, 0, 0), End: name, PlanAngle: en.PlanAngle, ElevAngle: en.ElevAngle));
                if (!en.Coped) continue;
                var lx = name == "start" ? x0 : x0 - en.CopeLength;
                if (en.CopeTop) o.Add(new ElementFeature($"notch:{name}:top", "notch", new Vec3(lx, f.Min.Y, f.Max.Z - en.CopeDepth), Vec3.UnitZ, Size: new Vec3(en.CopeLength, f.Width, en.CopeDepth), End: name));
                if (en.CopeBottom) o.Add(new ElementFeature($"notch:{name}:bottom", "notch", new Vec3(lx, f.Min.Y, f.Min.Z), -Vec3.UnitZ, Size: new Vec3(en.CopeLength, f.Width, en.CopeDepth), End: name));
            }
            return o;
        }
    }

    /// <summary>
    /// Steel parts' marks (decision 583): a member's prefix as the steel members schedule gives it (C, R, GP, P, SR, EB, B, FB, M, BM),
    /// a plate's PL; the schedule's marks the register's first numbers, so an old project's marks do not change.
    /// </summary>
    sealed class SteelMarks : IMarkKinds
    {
        public string? PrefixOf(IModelView m, Entity e) =>
            e.Class is SteelModule.Member or IfcClass.Beam or IfcClass.Column && MemberAxes.Of(m, e) != null && MemberAxes.IsSteel(m, e) ? SteelMembersSchedule.Prefix(m, e, Halls.Of(m, e.Id))
            : e.Class == "IfcPlate" || e.Str("exportAs") == "IfcPlate" ? "PL" : null;

        public IReadOnlyDictionary<string, string>? Seeds(IQueryContext q) => SteelMembersSchedule.Marked(q.Model, q.Geometry, false).Marks;
    }
}
