using System.Text.Json.Nodes;
using Lapin.Drafting.Cad;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Studio;

/// <summary>
/// What a Studio solid is exactly (docs/plan-studio-freeform.md §9, F2b), derived on demand from its operation and its sketches, never
/// stored: a primitive's planes, cylinder, cone, sphere or torus; an extrusion of lines and arcs (planes and cylinders); a revolution
/// (planes, cylinders, cones, spheres, tori); a smooth loft (its skinned B-spline surface, the one its body is cut from); a push/pull of a
/// polygon (planes); a closed SubD (its regular faces as bicubic patches, the rest as its facets). Steps (fillets, shells…), tapers,
/// extrusions up to a face, sweeps, Follow Me, bodies made from meshes and parts have none. Whatever is derived is held against the body
/// itself (<see cref="ExactCheck.Fit"/>): a cut by a priority, or anything else that made the body differ, refuses it.
/// </summary>
public sealed partial class StudioModule
{
    /// <summary>How far an exact face may stray from the body the model uses (the chord rule's millimetre, with room for its facets' middles).</summary>
    public const double ExactTolerance = 0.003;

    /// <summary>The solid's exact body checked against <paramref name="body"/> (its geometry's one part, in the world), or why it has none.</summary>
    public static (ExactBody? Body, string? Problem) Exact(Entity e, IModelView m, IServices? services, Solid body)
    {
        var (raw, tol, why) = ExactRaw(e, m, services);
        return raw == null ? (null, why) : ExactCheck.Fit(raw, body, tol);
    }

    /// <summary>The exact body its operation makes, in the world (before it is checked), the tolerance it is held to, or why it has none.</summary>
    public static (ExactBody? Body, double Tolerance, string? Problem) ExactRaw(Entity e, IModelView m, IServices? services)
    {
        var op = e.Str("operation");
        if (op == null || Thick.Is(op) || op == Thick.BoxSlab || Parts.IsInstance(e)) return (null, 0, "not a solid Studio says exactly.");
        if (e.Attrs["features"] is JsonArray { Count: > 0 }) return (null, 0, "its steps (fillets, shells, cuts…) change the body: it is written as its facets.");
        var tol = ExactTolerance;
        ExactBody? b;
        string? why = null;
        switch (op)
        {
            case "primitive": b = ExactPrimitive(e, m, services); break;
            case "prism":
                b = Base(e, m, services, null).Solid is { } prism ? ExactBuild.Planar(prism) : null;
                break;
            case SubDOperation:
            {
                var built = SubDBuilt(e);
                if (CageOf(e.Attrs["cage"]) is not { } cage || built.Result is not { } r) return (null, 0, "the cage is missing.");
                if (!cage.IsClosed) return (null, 0, "an open SubD is a surface (or, thickened, an offset of one): written as its facets.");
                if (SubDExact.Of(cage, r.Level) is not { } x) return (null, 0, "no face of the cage is regular: written as its facets.");
                var (at, rot) = Frame(e);
                b = x.Body.Moved(p => World(p, at, rot), v => Turn(v, rot));
                tol = Math.Max(ExactTolerance, 2 * r.Deviation + 0.001);
                break;
            }
            case "extrude": (b, why) = ExactExtrusion(e, m, services); break;
            case "revolve": (b, why) = ExactRevolution(e, m, services); break;
            case "loft": (b, why) = ExactLoft(e, m, services); break;
            default: return (null, 0, $"a {op} is written as its facets.");
        }
        if (b == null) return (null, 0, why ?? "its outline is not made of lines and arcs.");
        if (Placed(e) is { } xf)
        {
            var o = StoredBodies.Apply(xf, Vec3.Zero);
            b = b.Moved(p => StoredBodies.Apply(xf, p), v => StoredBodies.Apply(xf, v) - o);
        }
        return (b, tol, null);
    }

    /// <summary>A primitive exactly: a box, a wedge or a pyramid its planes; a cylinder extruded from its circle; a cone, a sphere and a torus turned from their profiles.</summary>
    static ExactBody? ExactPrimitive(Entity e, IModelView m, IServices? services)
    {
        var shape = e.Str("shape") ?? "box";
        var at = Features.P(e.Attrs["at"]) ?? Vec3.Zero;
        var rot = e.Num("rotation") * Math.PI / 180;
        var (x, z) = (new Vec3(Math.Cos(rot), Math.Sin(rot), 0), Vec3.UnitZ);
        var y = Vec3.Cross(z, x);
        var (h, r, r2) = (e.Num("height"), e.Num("radius"), e.Num("radius2"));
        switch (shape)
        {
            case "box" or "wedge" or "pyramid":
                return Base(e, m, services, null).Solid is { } s ? ExactBuild.Planar(s) : null;
            case "cylinder":
                if (r <= 0 || h <= 0) return null;
                return ExactBuild.Extrusion([[new ArcSeg(at, x, y, r, 0, 2 * Math.PI)]], z * h);
            case "cone":
            {
                if (r <= 0 || h <= 0 || r2 < 0) return null;
                var (rim, top, apex) = (at + x * r, at + x * r2 + z * h, at + z * h);
                List<ExactSeg> profile = r2 > 1e-9
                    ? [new LineSeg(at, rim), new LineSeg(rim, top), new LineSeg(top, apex), new LineSeg(apex, at)]
                    : [new LineSeg(at, rim), new LineSeg(rim, apex), new LineSeg(apex, at)];
                return ExactBuild.Revolution([profile], at, z, 2 * Math.PI);
            }
            case "sphere":
            {
                if (r <= 0) return null;
                var c = at + z * r;
                return ExactBuild.Revolution([[new ArcSeg(c, -z, x, r, 0, Math.PI), new LineSeg(c + z * r, c - z * r)]], c, z, 2 * Math.PI);
            }
            case "torus":
            {
                if (r <= 0 || r2 <= 0 || r2 >= r) return null;
                var c = at + z * r2;
                return ExactBuild.Revolution([[new ArcSeg(c + x * r, x, z, r2, 0, 2 * Math.PI)]], c, z, 2 * Math.PI);
            }
        }
        return null;
    }

    /// <summary>A sketch's outline and holes as drawn, placed in the world by <paramref name="world"/>; null when a loop is not of lines and arcs.</summary>
    static List<IReadOnlyList<ExactSeg>>? ExactLoops(IModelView m, Entity profile, Func<Vec2, Vec3> world)
    {
        if (Sketches.OutlineOf(m, profile) is not { } outline || ExactProfiles.Of(m, outline) is not { } outer) return null;
        var loops = new List<IReadOnlyList<ExactSeg>> { ExactProfiles.Placed(outer, world) };
        foreach (var h in Sketches.HoleOutlines(m, profile, ProfileIds(m)))
        {
            if (ExactProfiles.Of(m, h) is not { } hole) return null;
            loops.Add(ExactProfiles.Placed(hole, world));
        }
        return loops;
    }

    static (ExactBody?, string?) ExactExtrusion(Entity e, IModelView m, IServices? services)
    {
        if (e.Attrs["upTo"] is JsonObject) return (null, "an extrusion up to a face is written as its facets.");
        if (Math.Abs(e.Num("taper")) > 1e-9) return (null, "a tapered extrusion is written as its facets.");
        if (m.Get(e.Str("profile") ?? "") is not { } profile || PlaneOfSketch(m, services, profile).Plane is not { } plane) return (null, "its sketch is gone.");
        var depth = e.NumOrNull("depth") ?? 1;
        var start = Flag(e.Attrs["symmetric"]) ? -depth / 2 : 0.0;
        var n = plane.Normal;
        var shift = n * e.Num("offset") + n * start;
        if (ExactLoops(m, profile, q => plane.World(q) + shift) is not { } loops) return (null, null);
        return (ExactBuild.Extrusion(loops, n * depth), null);
    }

    static (ExactBody?, string?) ExactRevolution(Entity e, IModelView m, IServices? services)
    {
        if (m.Get(e.Str("profile") ?? "") is not { } profile || PlaneOfSketch(m, services, profile).Plane is not { } plane) return (null, "its sketch is gone.");
        if (m.Get(e.Str("guide") ?? "") is not { } g || Drafts.ShapeOf(g) is not Line ax) return (null, "its axis is gone.");
        var shift = plane.Normal * e.Num("offset");
        Vec3 W(Vec2 q) => plane.World(q) + shift;
        var (a3, b3) = (W(ax.A), W(ax.B));
        if (ExactLoops(m, profile, W) is not { } loops) return (null, null);
        var angle = Math.Clamp(e.NumOrNull("angle") ?? 360, -360, 360) * Math.PI / 180;
        return (ExactBuild.Revolution(loops, a3, b3 - a3, angle), "a piece of its profile turns into a surface written as facets (an arc round the axis).");
    }

    static (ExactBody?, string?) ExactLoft(Entity e, IModelView m, IServices? services)
    {
        if (!Flag(e.Attrs["smooth"])) return (null, "a straight loft between profiles is written as its facets.");
        if (e.Str("rail") != null) return (null, "a loft along a guide is written as its facets.");
        var exact = new List<IReadOnlyList<ExactSeg>>();
        var worlds = new List<Func<Vec2, Vec3>>();
        // Moved off as the body is: along the first sketch's normal.
        if (m.Get(e.Str("profile") ?? "") is not { } first || PlaneOfSketch(m, services, first).Plane is not { } plane) return (null, "its sketch is gone.");
        var shift = plane.Normal * e.Num("offset");
        foreach (var id in (e.Attrs["profiles"] as JsonArray)?.Select(x => (string?)x).OfType<string>() ?? [])
        {
            if (m.Get(id) is not { } d || Sketches.OutlineOf(m, d) is not { } drawn || PlaneOfSketch(m, services, d).Plane is not { } pl) return (null, "a profile is gone.");
            exact.Add(ExactProfiles.Of(m, drawn) ?? ExactProfiles.FromPolygon(drawn.Loop));
            worlds.Add(q => pl.World(q) + shift);
        }
        if (exact.Count < 3) return (null, "a smooth loft of two profiles is straight: written as its facets.");
        if (Solids.SmoothLoftSurface(exact, worlds) is not { } surface) return (null, "the profiles could not be skinned.");
        return (ExactBuild.Skinned(surface), null);
    }
}

/// <summary>Studio solids' exact bodies for the IFC and STEP exports (<see cref="IIfcExactBodies"/>).</summary>
sealed class StudioExactBodies : IIfcExactBodies
{
    public ExactBody? ExactOf(IModelView model, IServices services, Entity element, Solid body) =>
        element.Str("operation") is not null ? StudioModule.Exact(element, model, services, body).Body : null;
}
