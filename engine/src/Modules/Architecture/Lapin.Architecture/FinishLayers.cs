using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>
/// Finishes another program exported as walls of their own (Bubbles' „Finisaj exterior — Tencuială de var”, 2.5 cm
/// on a facade) made a layer of the wall they cover (docs/spec-ifc.md, Î7): a thin wall of one layer lying against a
/// wall's face, as high as it, along it (what it leaves uncovered at each end no more than a corner), with no opening
/// the wall has not, becomes that wall's outer layer on that side; its own openings go with it, the wall's cut it. A
/// wall covered on one face by several such pieces of one material takes them as one layer when together they cover
/// it so. What does not lie so (a finish standing off the wall, one over part of it, one with a door of its own) stays
/// a thin wall.
/// </summary>
public static class FinishLayers
{
    /// <summary>How thin a finish is, at most; how close to the face it lies; how much of a wall's end it may leave bare (a corner).</summary>
    const double MaxThickness = 0.05, Touch = 0.003, BareEnd = 0.6, Level = 0.01;

    /// <summary>The finishes among <paramref name="made"/> absorbed into the walls among them; their ids, gone from the model.</summary>
    public static List<string> Absorb(IEdit edit, IReadOnlyList<string> made, RecognitionContext ctx)
    {
        var m = edit.Model;
        var walls = made.Select(m.Get).OfType<Entity>().Where(e => e.Class == IfcClass.Wall && PathCurve.Of(e) is not { IsArc: true }).ToList();
        var finishes = walls.Where(w => IsFinish(m, w)).ToList();
        if (finishes.Count == 0) return [];
        var hosts = walls.Where(w => !finishes.Contains(w)).ToList();
        // Each finish on the one face of one wall it covers.
        var onFace = new Dictionary<(string Host, bool Left), List<(Entity Finish, double From, double To)>>();
        foreach (var f in finishes)
            if (Lies(m, f, hosts) is { } on)
                (onFace.TryGetValue((on.Host.Id, on.Left), out var list) ? list : onFace[(on.Host.Id, on.Left)] = []).Add((f, on.From, on.To));
        var absorbed = new List<string>();
        foreach (var ((hostId, left), pieces) in onFace)
        {
            var host = m.Get(hostId)!;
            var length = (Walls.End(host) - Walls.Start(host)).Length;
            var layer = Layer(m, pieces[0].Finish);
            if (pieces.Any(p => Layer(m, p.Finish) is var l && (l.Material != layer.Material || Math.Abs(l.Thickness - layer.Thickness) > 1e-3))) continue;
            // Together end to end along the wall, short of its ends by no more than a corner.
            var spans = pieces.OrderBy(p => p.From).ToList();
            var covered = spans[0].From <= BareEnd && spans[^1].To >= length - BareEnd
                && spans.Zip(spans.Skip(1)).All(z => z.Second.From - z.First.To <= 0.02);
            if (!covered) continue;
            Lay(edit, host, left, layer, ctx);
            foreach (var p in pieces)
            {
                // The wall's record says what it took, so the file's ids are not lost.
                var h = m.Get(hostId)!;
                var attrs = h.CloneAttrs();
                var imported = attrs["imported"] as JsonObject ?? new JsonObject();
                var list = imported["absorbed"] as JsonArray ?? new JsonArray();
                list.Add((JsonNode)new JsonObject { ["id"] = p.Finish.Id, ["name"] = p.Finish.Name });
                imported["absorbed"] = list;
                attrs["imported"] = imported;
                edit.Replace(h.With(attrs: attrs));
                edit.Remove(p.Finish.Id);
                absorbed.Add(p.Finish.Id);
            }
        }
        if (absorbed.Count > 0)
            ctx.Notes.Add($"{absorbed.Count} finish{(absorbed.Count == 1 ? "" : "es")} exported as walls of their own became a layer of the wall {(absorbed.Count == 1 ? "it covers" : "they cover")}.");
        return absorbed;
    }

    /// <summary>A thin wall of one layer, straight, with no door or window of its own.</summary>
    static bool IsFinish(IModelView m, Entity w)
    {
        var bands = Walls.Section(m, w);
        if (bands.Count != 1 || bands[0].To - bands[0].From > MaxThickness) return false;
        return Openings.PlacedOn(m, w.Id).All(o => o.Fill == null);
    }

    static (string Material, double Thickness) Layer(IModelView m, Entity finish)
    {
        var b = Walls.Section(m, finish)[0];
        return (b.Material is { } id ? m.Get(id)?.Name ?? b.Name : b.Name, Math.Round(b.To - b.From, 4));
    }

    /// <summary>
    /// The wall whose face <paramref name="finish"/> lies against, which side of it (left of its axis or right), and
    /// the stretch of its axis the finish covers; null when it lies against none as a finish does.
    /// </summary>
    static (Entity Host, bool Left, double From, double To)? Lies(IModelView m, Entity finish, List<Entity> hosts)
    {
        var (fa, fb) = (Walls.Start(finish), Walls.End(finish));
        var fBand = Walls.Section(m, finish)[0];
        var fDir = (fb - fa).Normalized();
        var storey = Spatial.StoreyOf(m, finish.Id)?.Id;
        foreach (var h in hosts)
        {
            var (ha, hb) = (Walls.Start(h), Walls.End(h));
            var dir = (hb - ha).Normalized();
            if (Math.Abs(Vec2.Cross(dir, fDir)) > 0.01) continue;
            if (Spatial.StoreyOf(m, h.Id)?.Id != storey) continue;
            if (Math.Abs(h.Num("baseOffset") - finish.Num("baseOffset")) > Level || Math.Abs(h.Num("height") - finish.Num("height")) > Level) continue;
            var n = dir.Perp;
            // The finish's faces across the wall's axis.
            var c0 = Vec2.Dot(fa + fDir.Perp * fBand.From - ha, n);
            var c1 = Vec2.Dot(fa + fDir.Perp * fBand.To - ha, n);
            var (f0, f1) = (Math.Min(c0, c1), Math.Max(c0, c1));
            var bands = Walls.Section(m, h);
            var (y0, y1) = (bands.Min(b => b.From), bands.Max(b => b.To));
            bool left;
            if (Math.Abs(f0 - y1) <= Touch) left = true;
            else if (Math.Abs(f1 - y0) <= Touch) left = false;
            else continue;
            var length = (hb - ha).Length;
            var (t0, t1) = (Vec2.Dot(fa - ha, dir), Vec2.Dot(fb - ha, dir));
            var (from, to) = (Math.Min(t0, t1), Math.Max(t0, t1));
            // Within the wall, or past its end by a finish's thickness (wrapping the corner's).
            if (from < -(MaxThickness + Level) || to > length + MaxThickness + Level) continue;
            // Its openings are the wall's: each over one of the wall's, as wide.
            var own = Openings.PlacedOn(m, h.Id);
            var sense = Vec2.Dot(fDir, dir) > 0;
            var match = Openings.PlacedOn(m, finish.Id).All(o =>
            {
                var along = sense ? t0 + o.Along : t0 - o.Along;
                return own.Any(w => Math.Abs(w.Along - along) <= 0.05 && Math.Abs(w.Width - o.Width) <= 0.1 && Math.Abs(w.Sill - o.Sill) <= 0.1);
            });
            if (!match) continue;
            return (h, left, from, to);
        }
        return null;
    }

    /// <summary>
    /// The wall given the finish as its outer layer on that side: a type with it (one per layers, shared), the wall's
    /// own layers where they were. The finish goes after the wall's layers when they end on its side, or when they read
    /// the same either way (one layer: the usage then turns), so a build-up finished on either face is one type; else
    /// before them. A type this import made for the wall alone goes when the wall leaves it.
    /// </summary>
    static void Lay(IEdit edit, Entity host, bool left, (string Material, double Thickness) finish, RecognitionContext ctx)
    {
        var m = edit.Model;
        var type = Walls.TypeOf(m, host.Id);
        var usageEntity = LayerSets.UsageOf(m, host.Id);
        if (type == null || usageEntity == null || Layers.ReadUsage(usageEntity) is not { } usage) return;
        var layers = LayerSets.LayersOf(m, type.Id).Select(l => LayerSets.SpecOf(m, l)).ToList();
        var bands = Walls.Section(m, host);
        var (y0, y1) = (bands.Min(b => b.From), bands.Max(b => b.To));
        var sign = usage.Negative ? -1 : 1;
        var symmetric = layers.Select(Key).SequenceEqual(layers.AsEnumerable().Reverse().Select(Key));
        var layer = new LayerSpec(finish.Material, finish.Thickness);
        bool negative; double offset;
        if (left == sign > 0 || symmetric)
        {
            // Last: the layers run towards the finish, from the far face.
            layers.Add(layer);
            negative = !left;
            offset = left ? y0 : y1;
        }
        else
        {
            layers.Insert(0, layer);
            negative = usage.Negative;
            offset = usage.Offset - sign * finish.Thickness;
        }
        var signature = string.Join("|", layers.Select(Key));
        var key = "wall:finished:" + type.Name + ":" + signature;
        if (!(ctx.TypeIds.TryGetValue(key, out var id) && m.Get(id) is { } finished))
        {
            var name = $"{type.Name} + {finish.Material}";
            finished = m.OfClass(IfcClass.WallType).FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase)
                && string.Join("|", LayerSets.LayersOf(m, t.Id).Select(l => Key(new LayerSpec(m.Get(l.Material)?.Name ?? "", l.Thickness, l.Core)))) == signature)
                ?? WallTypes.Create(edit, name, layers);
            ctx.TypeIds[key] = finished.Id;
            ctx.CreatedTypes.Add(finished.Id);
        }
        edit.Relate(IfcClass.RelDefinesByType, finished.Id, host.Id);
        var set = LayerSets.Of(m, finished.Id)!;
        edit.Replace(m.Get(usageEntity.Id)!.With(attrs: Layers.WriteUsage(new LayerUsage(set.Id, negative, Math.Round(offset, 6)))));
        if (ctx.CreatedTypes.Contains(type.Id) && !m.RelatedOf(type.Id, IfcClass.RelDefinesByType).Any())
        {
            if (LayerSets.Of(m, type.Id) is { } old) edit.Remove(old.Id);
            edit.Remove(type.Id);
            ctx.CreatedTypes.Remove(type.Id);
        }
    }

    static string Key(LayerSpec l) => $"{l.Material}:{l.Thickness}:{l.Core}";
}
