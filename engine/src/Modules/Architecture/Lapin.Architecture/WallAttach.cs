using System.Text.Json.Nodes;
using Lapin.Architecture.Roofs;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>
/// A wall's top through the platform's contract (<see cref="IAttachable"/>, docs/plan-attach-to.md A1), over what walls held
/// before it: bodies in <c>roofTop</c> / <c>roofIds</c> (<see cref="WallTops"/>), a level in <c>top</c> / <c>topOffset</c>
/// (<see cref="WallTop"/>). <c>element.attachTo {end: top, targets}</c> writes exactly what <c>architecture.attachToRoof</c>
/// writes, so the wall is built the same; a level is what <c>architecture.setWallTop</c> sets, the wall's own top as drawn,
/// which detaching goes back to. A wall's top follows a body both ways (it extends and is cut) with no gap, as it always did:
/// another offset or mode is refused, not ignored. Walls have no bottom to attach yet (stage A8).
/// </summary>
sealed class WallAttach : IAttachable
{
    public IReadOnlyList<string> Classes { get; } = [IfcClass.Wall];

    public bool Handles(Entity e) => e.Class == IfcClass.Wall;

    public IReadOnlyList<string> Ends(Entity e) => [Attachments.Top];

    public string? Refuse(IModelView m, Entity e, AttachSpec spec, IReadOnlyList<Entity> targets)
    {
        if (spec.Level != null)
            return spec.Level.Storey == "below" ? "A wall's top goes to the storey above or to one of the project's storeys, not to the one below." : null;
        // The message architecture.attachToRoof gives.
        if (targets.Any(t => t.Class == IfcClass.Wall)) return "A wall rises to a roof or to a body over it, not to another wall.";
        if (Math.Abs(spec.Offset) > 1e-9) return $"„{e.Name}”: a wall's top follows a body with no gap; leave the offset at 0.";
        if (spec.Mode != Attachments.Both) return $"„{e.Name}”: a wall's top under a body both rises to it and is cut by it; extend or trim alone is not there for walls.";
        return null;
    }

    public void Attach(ICommandContext ctx, Entity e, AttachSpec spec)
    {
        var after = e.With(attrs: Attrs(ctx.Model, e, spec));
        if (spec.Level != null)
            foreach (var o in Openings.PlacedOn(ctx.Model, e.Id)) Openings.CheckFits(ctx.Model, after, o.Along, o.Sill, o.Width, o.Height, o.Opening.Id);
        ctx.Replace(after);
    }

    /// <summary>The wall's attributes attached as <paramref name="spec"/> says (the command's, and the preview's wall as it would be).</summary>
    static JsonObject Attrs(IModelView m, Entity wall, AttachSpec spec)
    {
        var attrs = wall.CloneAttrs();
        if (spec.Level is { } level)
        {
            WallTop.Set(m, wall, attrs, level.Storey, level.Offset, null);
            return attrs;
        }
        // As architecture.attachToRoof writes it.
        attrs["roofTop"] = "attach";
        attrs["roofIds"] = new JsonArray([.. spec.Targets.Select(x => (JsonNode)x)]);
        attrs.Remove(WallTops.GoneKey);
        return attrs;
    }

    /// <summary>As architecture.detachFromRoof (mode none): roofs and bodies ignored, the wall at its own top, its level kept.</summary>
    public void Detach(ICommandContext ctx, Entity e, string end)
    {
        var attrs = e.CloneAttrs();
        attrs["roofTop"] = "none";
        attrs.Remove("roofIds");
        attrs.Remove(WallTops.GoneKey);
        ctx.Replace(e.With(attrs: attrs));
    }

    public AttachState? StateOf(IModelView m, Entity e, string end)
    {
        if (end != Attachments.Top) return null;
        var mode = e.Str("roofTop") ?? "auto";
        var named = mode == "attach" ? WallTops.Named(e) ?? [] : [];
        var level = WallTop.Of(e) is { } top ? new AttachLevel(top, e.Num(WallTop.OffsetKey)) : null;
        var extra = new JsonObject { ["roofTop"] = mode, ["everyRoof"] = mode == "attach" && named.Count == 0 };
        return new AttachState(end, named, level, 0, Attachments.Both, mode == "attach", extra);
    }

    public JsonObject Result(IModelView m, IServices services, Entity e, string end, AttachSpec? asIf)
    {
        var wall = asIf == null ? e : e.With(attrs: Attrs(m, e, asIf));
        var height = Walls.HeightOf(m, wall);
        var z = Spatial.Base(m, wall);
        var o = new JsonObject { ["base"] = Math.Round(z, 6), ["height"] = Math.Round(height, 6) };
        var (start, stop) = (Walls.Start(wall), Walls.End(wall));
        var length = (stop - start).Length;
        if (PathCurve.Of(wall).IsArc || length < 1e-6) o["curved"] = PathCurve.Of(wall).IsArc;
        else
        {
            var dir = (stop - start).Normalized();
            var bands = Walls.Section(m, wall);
            var (yMin, yMax) = (bands.Min(b => b.From), bands.Max(b => b.To));
            var profile = WallTops.Profile(wall, new RoofTop.WallAxis(start, dir, length, z, height, yMax - yMin), m, services);
            // Built up to the highest point of its top (Walls.Geometry), then cut along the profile, level across its thickness.
            var top = profile ?? [(0, height), (length, height)];
            o["built"] = Math.Round(Math.Max(height, top.Max(p => p.V)), 6);
            o["profile"] = new JsonArray([.. top.Select(p => (JsonNode)new JsonArray(Math.Round(p.U, 6), Math.Round(p.V, 6)))]);
            JsonNode At(double u, double y, double v) { var p = start + dir * u + dir.Perp * y; return new JsonArray(Math.Round(p.X, 6), Math.Round(p.Y, 6), Math.Round(z + v, 6)); }
            o["top"] = new JsonArray([.. top.Select(p => At(p.U, 0, p.V))]);
            // The new top face's outline in the world: along one face, back along the other.
            o["outline"] = new JsonArray([.. top.Select(p => At(p.U, yMax, p.V)), .. top.AsEnumerable().Reverse().Select(p => At(p.U, yMin, p.V))]);
        }
        var problems = new JsonArray();
        if (WallTop.Problem(m, wall) is { } p1) problems.Add((JsonNode)p1);
        if (WallTops.Problem(m, wall, services) is { } p2) problems.Add((JsonNode)p2);
        o["problems"] = problems;
        return o;
    }
}
