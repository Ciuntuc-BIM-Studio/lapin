using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Graphs;

/// <summary>
/// A graph's preview (docs/plan-visual-graphs.md §3): evaluated without touching the model. A graph that runs commands runs
/// them on a copy of the model (a <see cref="ModelStore"/> of its own, kept while the model has not changed), in one
/// transaction given back at the end, so the copy stays the model's for the next preview; what the commands made or
/// changed is built there, by the modules' own producers, and sent as the ghost.
/// </summary>
public static class Preview
{
    sealed class Copy
    {
        public long Revision = -1;
        public readonly ModelStore Store = new();
        public History History = null!;
    }

    static readonly ConditionalWeakTable<IModelView, Copy> Copies = [];

    /// <summary>Thrown at the end of a preview's transaction so it is given back.</summary>
    sealed class GiveBack : Exception;

    public static JsonObject Of(IQueryContext q, GraphDoc doc)
    {
        var catalog = q.Services as ModuleCatalog ?? throw new CommandException("No commands to preview here.");
        var clock = Stopwatch.StartNew();
        JsonObject? result = null;
        if (!doc.Nodes.Any(n => n.Kind.StartsWith(Evaluator.CommandPrefix, StringComparison.Ordinal)))
        {
            var e = new Evaluator(doc, q.Model, catalog, null, q, GraphLimits.PreviewTime).RunAll();
            result = Result(e, null, catalog);
        }
        else
        {
            var copy = Copies.GetValue(q.Model, _ => new Copy());
            lock (copy)
            {
                if (copy.Revision != q.Model.Revision)
                {
                    copy.History = new History(copy.Store);
                    copy.History.Load(q.Model.Entities, q.Model.Relations);
                    copy.Revision = q.Model.Revision;
                }
                try
                {
                    copy.History.Execute("Preview", edit =>
                    {
                        var query = new ModelQuery(edit.Model, catalog);
                        var ctx = new TrackingContext(edit, catalog, query);
                        var e = new Evaluator(doc, edit.Model, catalog, ctx, query, GraphLimits.PreviewTime).RunAll();
                        // What a command settles after it in a session (types' defaults, components), as the bake will have it.
                        try { TypeDefaults.Settle(edit); Components.Settle(ctx, edit); } catch (Exception) { }
                        result = Result(e, edit, catalog);
                        throw new GiveBack();
                    });
                }
                catch (GiveBack) { }
            }
        }
        result!["ms"] = (int)clock.ElapsedMilliseconds;
        return result;
    }

    static JsonObject Result(Evaluator e, IEdit? edit, ModuleCatalog catalog)
    {
        var nodes = new JsonObject();
        foreach (var (id, r) in e.Results) nodes[id] = r.ToJson();
        var ghost = Ghost(e, edit, catalog);
        return new JsonObject { ["nodes"] = nodes, ["ghost"] = ghost, ["calls"] = e.Calls };
    }

    /// <summary>The ghost: the meshes of what the commands made or changed (by storey), their plan lines, and the points and lines of the geometry nodes.</summary>
    static JsonObject Ghost(Evaluator e, IEdit? edit, ModuleCatalog catalog)
    {
        var parts = new JsonArray();
        var plan = new JsonArray();
        var used = 0;
        var cut = false;
        if (edit != null)
        {
            foreach (var change in edit.EntityChanges)
            {
                if (change.After is not { } entity) continue;
                if (used > GraphLimits.MaxGhostFloats) { cut = true; break; }
                if (ModelQuery.Build(entity, edit.Model, catalog) is not { } g) continue;
                var storey = Spatial.StoreyOf(edit.Model, entity.Id)?.Id;
                foreach (var p in g.Parts)
                {
                    Lapin.Geometry.TriMesh m;
                    try { m = p.Tessellate(); } catch (Exception) { continue; }
                    if (m.Positions.Length == 0) continue;
                    used += m.Positions.Length + m.Edges.Length;
                    parts.Add((JsonNode)new JsonObject
                    {
                        ["element"] = entity.Id, ["class"] = entity.Class, ["storey"] = storey, ["color"] = p.Color,
                        ["positions"] = Floats(m.Positions), ["indices"] = new JsonArray(m.Indices.Select(i => (JsonNode)i).ToArray()),
                        ["edges"] = Floats(m.Edges),
                    });
                }
                foreach (var item in g.Plan ?? [])
                {
                    if (item.Points.Count < 2) continue;
                    used += item.Points.Count * 2;
                    plan.Add((JsonNode)new JsonObject
                    {
                        ["storey"] = storey, ["closed"] = item.Closed,
                        ["points"] = new JsonArray(item.Points.SelectMany(v => new[] { (JsonNode)Math.Round(v.X, 4), Math.Round(v.Y, 4) }).ToArray()),
                    });
                }
            }
        }
        var (points, lines) = e.Shapes(Math.Max(0, GraphLimits.MaxGhostFloats - used));
        var o = new JsonObject
        {
            ["parts"] = parts, ["plan"] = plan,
            ["points"] = new JsonArray(points.SelectMany(p => new[] { (JsonNode)Math.Round(p.X, 4), Math.Round(p.Y, 4), Math.Round(p.Z, 4) }).ToArray()),
            ["lines"] = new JsonArray(lines.Select(l => (JsonNode)new JsonObject
            {
                ["closed"] = l.Closed,
                ["points"] = new JsonArray(l.Points.SelectMany(p => new[] { (JsonNode)Math.Round(p.X, 4), Math.Round(p.Y, 4), Math.Round(p.Z, 4) }).ToArray()),
            }).ToArray()),
        };
        if (cut) o["cut"] = true;
        return o;
    }

    static JsonArray Floats(float[] v) => new(v.Select(x => (JsonNode)Math.Round(x, 4)).ToArray());
}
