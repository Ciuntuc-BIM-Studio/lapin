using System.Text.Json.Nodes;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Audit 15.11.2026, P3: one body the boolean library cannot take (an open mesh, from an IFC reference) across a 3D view's
/// section box used to throw out of the view and fail it with its sheets and its PDF. It is kept whole now, as without the
/// library, and the other parts are cut as before.
/// </summary>
public class View3dOpenMeshTests
{
    /// <summary>A boolean library that refuses one solid (as manifold refuses an open mesh) and cuts the others by their boxes.</summary>
    sealed class Refusing(Solid refused) : ISolidBooleans, IServices
    {
        public Solid? Difference(Solid a, IReadOnlyList<Solid> cutters, string? tag = null) => a;
        public Solid? Union(IReadOnlyList<Solid> solids, string? tag = null) => solids.FirstOrDefault();
        public Solid? Intersection(Solid a, Solid b, string? tag = null)
        {
            if (ReferenceEquals(a, refused)) throw new InvalidOperationException("manifold: error 1 (the solid is not closed and orientable).");
            var (amin, amax) = Measure.Bounds(a)!.Value;
            var (bmin, bmax) = Measure.Bounds(b)!.Value;
            var lo = new Vec3(Math.Max(amin.X, bmin.X), Math.Max(amin.Y, bmin.Y), Math.Max(amin.Z, bmin.Z));
            var hi = new Vec3(Math.Min(amax.X, bmax.X), Math.Min(amax.Y, bmax.Y), Math.Min(amax.Z, bmax.Z));
            return Prism.Box((lo + hi) * 0.5, hi.X - lo.X, hi.Y - lo.Y, hi.Z - lo.Z);
        }
        public T? Get<T>() where T : class => this as T;
        public IReadOnlyList<T> All<T>() where T : class => this is T t ? [t] : [];
    }

    [Fact]
    public void An_open_body_across_the_section_box_is_kept_whole_and_the_view_is_still_drawn()
    {
        var f = new Fixture();
        var open = Prism.Box(new Vec3(0, 0, 1), 4, 1, 2)!;
        var cut = Prism.Box(new Vec3(0, 3, 1), 4, 1, 2)!;
        var inside = Prism.Box(new Vec3(0, -3, 1), 1, 1, 1)!;
        var view = new Entity("v", "IfcAnnotation", "3D", new JsonObject
        {
            ["camera"] = new JsonObject { ["target"] = new JsonArray(0, 0, 0), ["ortho"] = true },
            ["sectionBox"] = new JsonObject { ["min"] = new JsonArray(-1, -5, -1), ["max"] = new JsonArray(1, 5, 5) },
        });
        DrawPart[] parts = [new(open, "open", "IfcBuildingElementProxy"), new(cut, "cut", "IfcWall"), new(inside, "inside", "IfcWall")];
        var (_, kept) = View3dDrawings.Prepare(f.Model, view, parts, new Refusing(open));
        Assert.Equal(["open", "cut", "inside"], kept.Select(p => p.Element));
        // The open body whole; the other one across the box cut to it.
        Assert.Same(open, kept[0].Solid);
        var (cmin, cmax) = Measure.Bounds(kept[1].Solid)!.Value;
        Assert.Equal(-1, cmin.X, 6);
        Assert.Equal(1, cmax.X, 6);
    }
}
