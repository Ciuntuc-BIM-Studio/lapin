using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Civil;

/// <summary>
/// A hardscape's kerb (T4; forum 230 §3): an element of its own (IfcKerb in IFC 4.3, a proxy "Kerb" in IFC4), naming its
/// hardscape (<see cref="HostKey"/>), along all its edges or those chosen (<c>edges</c>), standing outside the outline. Its
/// profile: one of the catalogue's (upright, bevelled, rounded: <see cref="Profiles"/> of use <c>kerb</c>, by its height above the
/// paving, its width and its depth below it) or a profile of the project's (drawn, Manage → Profiles), laid with its inner side on
/// the edge and its top the kerb's height above the paving. An edge may be dropped (<c>drops</c>: its reveal above the paving there,
/// 0 at a crossing): the profile is cut down to it along that edge. Mitred where two kerbed edges meet, square where one stops;
/// it follows the paving's top, so a sloped hardscape has a sloped kerb.
/// </summary>
public static class Kerbs
{
    public const string Class = "LapinKerb";
    public const string HostKey = "hardscape";
    /// <summary>IFC 4.3's kerb; IFC4 has none (a proxy named Kerb).</summary>
    public const string IfcKerbClass = "IfcKerb";
    public const string Color = "#9c9a94";
    public static readonly string[] Shapes = ["upright", "bevelled", "rounded"];

    public static bool Is(Entity e) => e.Class == Class;

    public static Entity? Of(IModelView m, string hardscapeId) => m.OfClass(Class).FirstOrDefault(k => k.Str(HostKey) == hardscapeId);

    /// <summary>What a kerb is over a hardscape of <paramref name="edges"/> edges: which edges have it, the reveal on each dropped one, its sizes.</summary>
    public sealed record Spec(bool[] On, double?[] Drop, double Height, double Width, double Depth, string Shape);

    public static Spec SpecOf(Entity k, int edges)
    {
        var all = k.Attrs["edges"] is not JsonArray;
        var on = new bool[edges];
        var drop = new double?[edges];
        var ea = k.Attrs["edges"] as JsonArray;
        var da = k.Attrs["drops"] as JsonArray;
        for (var i = 0; i < edges; i++)
        {
            on[i] = all || (ea != null && i < ea.Count && ea[i] is JsonValue v && v.TryGetValue<bool>(out var b) && b);
            drop[i] = da != null && i < da.Count ? Json.Number(da[i]) : null;
        }
        return new Spec(on, drop, Math.Max(0, k.Num("height", 0.12)), Math.Max(0.02, k.Num("width", 0.15)), Math.Max(0, k.Num("depth", 0.25)),
            k.Str("shape") is { } s && Shapes.Contains(s) ? s : "upright");
    }

    /// <summary>
    /// Its profile in its own frame: u out of the paving's edge (0 on it), v up from the paving's top. A project profile's is
    /// laid with its inner side on the edge and its top at the kerb's height; a catalogue one is drawn from the sizes.
    /// </summary>
    public static List<Vec2> Profile(IModelView m, Entity k, Spec s)
    {
        if (Profiles.Of(m, k) is { } own && own.Outline() is { Count: >= 3 } o)
        {
            var (u0, v1) = (o.Min(p => p.X), o.Max(p => p.Y));
            return [.. o.Select(p => new Vec2(p.X - u0, p.Y - v1 + s.Height))];
        }
        var c = Profiles.CatalogueEntry(CatalogueId(s.Shape))!;
        var shape = c.Shape(new JsonObject { ["height"] = s.Height, ["width"] = s.Width, ["depth"] = s.Depth });
        return shape.Outline();
    }

    public static string CatalogueId(string shape) => shape switch { "bevelled" => "kerbBevelled", "rounded" => "kerbRounded", _ => "kerbUpright" };

    /// <summary>The profile cut down to a reveal: nothing above <paramref name="top"/> over the paving (consecutive points the same dropped).</summary>
    public static List<Vec2> Clamp(List<Vec2> p, double top)
    {
        var o = new List<Vec2>();
        foreach (var q in p.Select(q => q.Y > top ? q with { Y = top } : q))
            if (o.Count == 0 || (o[^1] - q).Length > 1e-7) o.Add(q);
        if (o.Count > 1 && (o[0] - o[^1]).Length <= 1e-7) o.RemoveAt(o.Count - 1);
        return o;
    }

    /// <summary>
    /// The kerb as solids, one a run of edges of the same reveal: rings of the profile at each laid point of its edges, mitred
    /// on the bisector where the kerb goes on (stretched by 1/cos of half the turn, at most 4), square where it stops.
    /// </summary>
    public static List<(int Edge, Solid Solid)> Solids(IModelView m, Entity k, Entity host)
    {
        var o = new List<(int, Solid)>();
        var corners = Hardscapes.Corners(host);
        var n = corners.Count;
        if (n < 3) return o;
        var top = Hardscapes.Top(host);
        var s = SpecOf(k, n);
        if (!s.On.Any(x => x)) return o;
        var profile = Profile(m, k, s);
        var pts = top.Points;
        var count = pts.Count;
        // The outward normal of each laid segment (the outline runs anticlockwise: outward is to the right).
        Vec2 Out(int i) { var d = (pts[(i + 1) % count] - pts[i]).Normalized(); return new Vec2(d.Y, -d.X); }
        // Which edge each laid segment is on.
        var edgeOfSeg = new int[count];
        for (var e = 0; e < n; e++) { var ix = Hardscapes.EdgePoints(top, e); for (var q = 0; q + 1 < ix.Count; q++) edgeOfSeg[ix[q]] = e; }
        bool Same(int a, int b) => s.On[a] && s.On[b] && s.Drop[a] == s.Drop[b];
        // Runs of edges with the kerb and the same reveal; all of them one loop when every edge is alike.
        var runs = new List<List<int>>();
        var start = Enumerable.Range(0, n).FirstOrDefault(e => s.On[e] && !Same((e - 1 + n) % n, e), -1);
        if (start < 0) runs.Add([.. Enumerable.Range(0, n)]);
        else
            for (var j = 0; j < n; j++)
            {
                var e = (start + j) % n;
                if (!s.On[e]) continue;
                if (runs.Count > 0 && Same(runs[^1][^1], e) && runs[^1][^1] == (e - 1 + n) % n) runs[^1].Add(e); else runs.Add([e]);
            }
        foreach (var run in runs)
        {
            var reveal = s.Drop[run[0]];
            var ring = reveal is { } r ? Clamp(profile, Math.Max(-s.Depth, r)) : profile;
            if (ring.Count < 3) continue;
            // The laid points along the run, start to end.
            var idx = new List<int>();
            foreach (var e in run) { var ix = Hardscapes.EdgePoints(top, e); idx.AddRange(idx.Count == 0 ? ix : ix.Skip(1)); }
            var rings = new List<IReadOnlyList<Vec3>>();
            for (var q = 0; q < idx.Count; q++)
            {
                var i = idx[q];
                var prevSeg = (i - 1 + count) % count;
                var nextSeg = i;
                Vec2 nrm;
                double stretch = 1;
                // Inside the run, or at an end where another kerbed edge goes on: the bisector; else square to its own segment.
                var first = q == 0;
                var last = q == idx.Count - 1;
                var prevKerbed = !first || s.On[edgeOfSeg[prevSeg]];
                var nextKerbed = !last || s.On[edgeOfSeg[nextSeg]];
                if (first && !prevKerbed) nrm = Out(nextSeg);
                else if (last && !nextKerbed) nrm = Out(prevSeg);
                else
                {
                    var (a, b) = (Out(prevSeg), Out(nextSeg));
                    var bis = (a + b).Normalized();
                    if (bis.Length < 1e-9) bis = a;
                    var cos = Vec2.Dot(bis, a);
                    stretch = Math.Min(4, cos > 1e-6 ? 1 / cos : 4);
                    nrm = bis;
                }
                var (p, z) = (pts[i], top.Z[i]);
                rings.Add([.. ring.Select(v => (p + nrm * (v.X * stretch)).At(z + v.Y))]);
            }
            if (SiteGeometry.LoftMany(rings, "kerb") is { } solid) o.Add((run[0], solid));
        }
        return o;
    }

    /// <summary>How long it runs along its hardscape's edges (plan; arcs as arcs).</summary>
    public static double Length(IModelView m, Entity k, Entity host)
    {
        var c = Hardscapes.Corners(host);
        var rises = Hardscapes.Rises(host);
        var s = SpecOf(k, c.Count);
        return Enumerable.Range(0, c.Count).Where(e => s.On[e]).Sum(e => ArcOutline.Edge(c, rises, e).Length);
    }

    public static ElementGeometry? Geometry(Entity k, IModelView m, IServices _)
    {
        // A kerb bound to no hardscape (T6): along its own line.
        if (k.Str(HostKey) == null) return KerbLines.Geometry(k, m);
        if (m.Get(k.Str(HostKey) ?? "") is not { } host || !Hardscapes.Is(host)) return new ElementGeometry([], Diagnostics: ["The kerb's hardscape is not in the project."]);
        var material = k.Str("material");
        var color = material != null ? Materials.ColorOf(m, material) : Color;
        var parts = Solids(m, k, host).Select((x, i) => new GeometryPart(i == 0 ? "Kerb" : $"Kerb {i + 1}", color, x.Solid, material, "kerb:" + x.Edge)).ToList();
        return new ElementGeometry(parts, null, null, parts.Count == 0 ? ["The kerb runs along none of its hardscape's edges."] : null);
    }

    public static IEnumerable<QuantitySet> Quantities(Entity k, ElementGeometry? g, IModelView m)
    {
        if (k.Str(HostKey) == null) { foreach (var q in KerbLines.Quantities(k, g, m)) yield return q; yield break; }
        if (g == null || m.Get(k.Str(HostKey) ?? "") is not { } host) yield break;
        var c = Hardscapes.Corners(host);
        var rises = Hardscapes.Rises(host);
        var s = SpecOf(k, c.Count);
        var dropped = Enumerable.Range(0, c.Count).Where(e => s.On[e] && s.Drop[e] != null).Sum(e => ArcOutline.Edge(c, rises, e).Length);
        yield return new QuantitySet("Lapin_KerbQuantities",
        [
            new("Length", Length(m, k, host), "LENGTH", "Length"),
            new("DroppedLength", dropped, "LENGTH", "Dropped"),
            new("Volume", g.Parts.Sum(p => Measure.Volume(p.Solid)), "VOLUME", "Volume"),
        ]);
    }

    /// <summary>
    /// The kerb of a hardscape set: {on? (false: none), edges? [bool…] | all?, drops? [reveal | null…], drop? {edge, reveal | null},
    /// shape? (upright | bevelled | rounded), profileId? (a project profile; "" back to the shape), height?, width?, depth?, material?
    /// (a name)}. Made when it has none, removed with on false. Its id, or null.
    /// </summary>
    public static string? SetKerb(ICommandContext ctx, Entity host, JsonObject a)
    {
        var m = ctx.Model;
        var had = Of(m, host.Id);
        if (a["on"] is JsonValue ov && ov.TryGetValue<bool>(out var on) && !on)
        {
            if (had != null) ctx.Remove(had.Id);
            return null;
        }
        var n = Hardscapes.Corners(host).Count;
        var attrs = had?.CloneAttrs() ?? new JsonObject { [HostKey] = host.Id, ["exportAs"] = IfcKerbClass };
        if (a["all"] is JsonValue av && av.TryGetValue<bool>(out var all) && all) attrs.Remove("edges");
        if (a["edges"] is JsonArray ea)
        {
            var list = Enumerable.Range(0, n).Select(i => i < ea.Count && ea[i] is JsonValue v && v.TryGetValue<bool>(out var b) && b).ToList();
            if (list.All(x => x)) attrs.Remove("edges"); else attrs["edges"] = new JsonArray([.. list.Select(x => (JsonNode)x)]);
        }
        var drops = Enumerable.Range(0, n).Select(i => attrs["drops"] is JsonArray d && i < d.Count ? Json.Number(d[i]) : null).ToList();
        if (a["drops"] is JsonArray da) drops = [.. Enumerable.Range(0, n).Select(i => i < da.Count ? Json.Number(da[i]) : null)];
        if (a["drop"] is JsonObject one)
        {
            var e = Json.Number(one["edge"]) is { } ed && ed >= 0 && ed < n && ed == Math.Floor(ed) ? (int)ed : throw new CommandException($"Which edge? The hardscape has {n}, from 0.");
            drops[e] = Json.Number(one["reveal"]);
        }
        if (drops.Any(d => d is < 0)) throw new CommandException("A dropped kerb's reveal is 0 or more above the paving.");
        if (drops.Any(d => d != null)) attrs["drops"] = new JsonArray([.. drops.Select(d => d is { } v ? (JsonNode)Math.Round(v, 6) : null)]); else attrs.Remove("drops");
        ApplyShape(ctx, attrs, a);
        if (had != null) { ctx.Replace(had.With(attrs: attrs)); return had.Id; }
        var k = new Entity(ctx.NewId(), Class, $"{host.Name} kerb", attrs);
        ctx.Add(k);
        if (Spatial.StoreyOf(m, host.Id) is { } storey) Spatial.Contain(ctx, storey.Id, k.Id);
        return k.Id;
    }

    /// <summary>A kerb's shape as a command gives it, over what it had: {height?, width?, depth?, shape?, profileId? ("" back to the shape), material?}.</summary>
    public static void ApplyShape(ICommandContext ctx, JsonObject attrs, JsonObject a)
    {
        var m = ctx.Model;
        foreach (var key in new[] { "height", "width", "depth" })
            if (Json.Number(a[key]) is { } v)
            {
                if (!(v >= 0) || v > 2) throw new CommandException($"A kerb's {key} is between 0 and 2 m.");
                attrs[key] = v;
            }
        if ((string?)a["shape"] is { } sh)
        {
            if (!Shapes.Contains(sh)) throw new CommandException($"A kerb is {string.Join(", ", Shapes)}, or a profile of the project's.");
            attrs["shape"] = sh;
        }
        if (a.ContainsKey(Profiles.IdKey))
        {
            if ((string?)a[Profiles.IdKey] is { Length: > 0 } pid)
            {
                var p = Spatial.Require(m, pid, Profiles.Class);
                Profiles.Assign(attrs, Profiles.Read(p) ?? throw new CommandException($"„{p.Name}” has no outline."), p.Id);
            }
            else { attrs.Remove(Profiles.IdKey); attrs.Remove(Profiles.Key); }
        }
        if ((string?)a["material"] is { Length: > 0 } mat) attrs["material"] = Materials.Ensure(ctx, mat, "#a5a29b").Id;
        else if (attrs["material"] == null) attrs["material"] = Materials.Ensure(ctx, "Concrete kerb", "#a5a29b").Id;
    }

    /// <summary>Its edges' data run the other way round, as its hardscape mirrored is (<see cref="ArcOutline.RunBack"/>).</summary>
    public static void RunBack(ICommandContext ctx, Entity k, int n)
    {
        var s = SpecOf(k, n);
        var attrs = k.CloneAttrs();
        if (attrs["edges"] is JsonArray) attrs["edges"] = new JsonArray([.. ArcOutline.RunBack(s.On).Select(b => (JsonNode)b)]);
        if (attrs["drops"] is JsonArray) attrs["drops"] = new JsonArray([.. ArcOutline.RunBack(s.Drop).Select(d => d is { } v ? (JsonNode)v : null)]);
        ctx.Replace(k.With(attrs: attrs));
    }

    public static void Register(IModuleRegistry r)
    {
        r.Class(new(Class, "Kerb",
        [
            new("height", "Height above the paving", "length", Min: 0, Max: 2), new("width", "Width", "length", Min: 0.02, Max: 2),
            new("depth", "Depth below the paving", "length", Min: 0, Max: 2), new("shape", "Shape", "choice", Choices: Shapes),
        ], Color));
        r.Geometry(Class, Geometry);
        r.Quantities(Class, Quantities);
        // {id (the hardscape), …as Set}: one undo step.
        r.Command("setHardscapeKerb", "Hardscape kerb", (ctx, a) =>
        {
            var host = Spatial.Require(ctx.Model, (string?)a["id"], Hardscapes.Class);
            return Kerbs.SetKerb(ctx, host, a) is { } id ? new JsonObject { ["id"] = id } : null;
        });
        // Kerbs bound to no hardscape (T6).
        KerbLines.Register(r);
    }
}
