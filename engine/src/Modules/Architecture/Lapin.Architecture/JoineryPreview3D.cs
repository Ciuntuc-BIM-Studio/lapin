using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>
/// The Doors and windows configurator's 3D preview (report #108): the type being edited, as the draft is now, standing in a
/// piece of wall of the project's first wall type (its layers, so the reveal's finish, the sills and the lining show as they
/// will), built by the same code that builds a placed door or window. Made in a model of its own, never the project's: the
/// project's materials copied in (by their ids, so the interface gives them their looks), a storey, the wall, the type, the
/// opening and the fill; then the wall's and the fill's geometry, with the platform's geometry filters, tessellated.
/// Coordinates: X along the wall (its middle at 0), Y across it (the exterior towards −Y), Z up from the wall's base.
/// </summary>
public static class JoineryPreview3D
{
    /// <summary>How far the wall runs on each side of the opening.</summary>
    public const double Margin = 0.6;

    public static JsonObject Of(JoinerySpec spec, IModelView project, IServices services)
    {
        if (Joinery.Problem(spec) is { } problem) return new JsonObject { ["problem"] = problem };
        var store = new ModelStore();
        var history = new History(store);
        Entity? wall = null, fill = null;
        var length = spec.Width + 2 * Margin;
        var height = Math.Max(2.7, spec.SillHeight + spec.Height + 0.45);
        try
        {
            history.Execute("Preview", edit =>
            {
                foreach (var m in project.OfClass(IfcClass.Material)) edit.Add(m);
                var storey = Spatial.EnsureStorey(edit);
                // The project's first wall type (what a new wall takes), else Lapin's default build-up.
                var own = project.OfClass(IfcClass.WallType).FirstOrDefault();
                var layers = own != null ? LayerSets.LayersOf(project, own.Id).Select(l => LayerSets.SpecOf(project, l)).ToList() : null;
                var type = WallTypes.Create(edit, own?.Name, layers is { Count: > 0 } ? layers : WallTypes.DefaultLayers);
                wall = Walls.AddUnjoined(edit, storey.Id, new Vec2(-length / 2, 0), new Vec2(length / 2, 0), height, 0, type);
                var joinery = Openings.CreateType(edit, spec);
                fill = Openings.AddFill(edit, store.Get(wall.Id)!, joinery, length / 2, spec.SillHeight);
            });
        }
        catch (CommandException e) { return new JsonObject { ["problem"] = e.Message }; }

        var parts = new JsonArray();
        var producers = services.Get<IGeometryProducers>();
        foreach (var e in new[] { store.Get(wall!.Id)!, store.Get(fill!.Id)! })
        {
            if (producers?.Geometry(e.Class) is not { } producer) continue;
            var g = producer(e, store, services);
            if (g == null) continue;
            foreach (var f in services.All<IGeometryFilter>()) g = f.Apply(e, g, store, services) ?? g;
            foreach (var p in g.Parts)
            {
                var m = p.Tessellate();
                if (m.Positions.Length == 0) continue;
                parts.Add((JsonNode)new JsonObject
                {
                    ["element"] = e.Class == IfcClass.Wall ? "wall" : "fill",
                    ["name"] = p.Name, ["color"] = p.Color, ["material"] = p.Material,
                    ["positions"] = Floats(m.Positions), ["indices"] = new JsonArray(m.Indices.Select(i => (JsonNode)i).ToArray()),
                    ["edges"] = Floats(m.Edges),
                });
            }
        }
        var t = Walls.Section(store, store.Get(wall.Id)!) is { Count: > 0 } bands ? bands.Max(b => b.To) - bands.Min(b => b.From) : Walls.DefaultThickness;
        return new JsonObject { ["parts"] = parts, ["size"] = new JsonArray(length, t, height) };
    }

    static JsonArray Floats(float[] v) => new(v.Select(x => (JsonNode)Math.Round(x, 4)).ToArray());
}
