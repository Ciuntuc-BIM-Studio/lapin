using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Civil;

/// <summary>
/// A standalone kerb (bordură; docs/plan-site-grading.md, T6; forum 230 §5): a <see cref="Kerbs.Class"/> bound to no hardscape,
/// laid along a line drawn (<c>points</c>, <c>arcs</c>, <c>closed</c>) or followed live (<c>along</c>, <see cref="SiteLines"/>: a
/// feature line, a hardscape's edge, a path's edge, a channel), its profile the hardscape kerb's (<see cref="Kerbs.Profile"/>: the
/// catalogue's upright, bevelled or rounded, or a project profile), standing on the <c>side</c> of the line away from the paving
/// (right by default, as round an outline drawn anticlockwise), its top its height over the line. The line's heights come from
/// what it follows or its own points (<c>follow: line</c>), or from what lies under it (<c>follow: ground</c>: a hardscape's top,
/// else the terrain). Dropped over stretches (<c>drops</c>: [{from, to, reveal}], stations along it; reveal 0 at a crossing), each
/// stretch its own run, mitred where the line turns and square where it stops. It cuts the terrain it is given (<c>terrain</c>).
/// IFC as the hardscape's kerb: IfcKerb in IFC 4.3, a proxy "Kerb" in IFC4.
/// </summary>
public static class KerbLines
{
    public static readonly string[] Follows = ["line", "ground"];
    public static readonly string[] Sides = ["right", "left"];
    /// <summary>The longest stretch between two of its sections where it lies on the ground (m).</summary>
    public const double Step = 1.0;

    /// <summary>A standalone kerb: one bound to no hardscape.</summary>
    public static bool Is(Entity e) => Kerbs.Is(e) && e.Str(Kerbs.HostKey) == null;

    public sealed record Drop(double From, double To, double Reveal);

    public static List<Drop> DropsOf(Entity e) =>
        (e.Attrs["drops"] as JsonArray)?.OfType<JsonObject>()
            .Select(o => Json.Number(o["from"]) is { } f && Json.Number(o["to"]) is { } t && Json.Number(o["reveal"]) is { } r ? new Drop(Math.Min(f, t), Math.Max(f, t), Math.Max(0, r)) : null)
            .OfType<Drop>().ToList() ?? [];

    /// <summary>Its line laid: points in plan, each one's height (the paving's top there), closed or not, what is wrong.</summary>
    public sealed record Line(List<Vec2> Plan, List<double> Z, bool Closed, List<string> Notes);

    public static Line Laid(IModelView m, Entity e)
    {
        var notes = new List<string>();
        List<Vec2> plan;
        List<double?> z;
        bool closed;
        if (e.Attrs["along"] is JsonObject along)
        {
            if (SiteLines.Along(m, along, notes) is not { } l) return new Line([], [], false, notes);
            (plan, z, closed) = (l.Plan, l.Z, l.Closed);
        }
        else
        {
            var s = FeatureLines.SpecOf(e.Attrs);
            if (s.Corners.Count < 2) return new Line([], [], false, ["A kerb needs two points at least."]);
            var line = FeatureLines.Line(s, s.Z).ToList();
            closed = s.Closed;
            if (closed && line.Count > 1 && (line[0].Xy - line[^1].Xy).Length < 1e-9) line.RemoveAt(line.Count - 1);
            (plan, z) = ([.. line.Select(p => p.Xy)], [.. line.Select(p => double.IsFinite(p.Z) ? p.Z : (double?)null)]);
        }
        var ground = e.Str("follow") == "ground";
        if (ground)
        {
            // On the ground: a point every metre, each at what lies there.
            (plan, _) = SiteLines.Densify(plan, z, closed, Step);
            z = [.. plan.Select(p => SiteLines.Surface(m, p))];
        }
        else if (z.Any(x => x == null))
        {
            // A point the line gives no height: what lies under it.
            z = [.. z.Select((x, i) => x ?? SiteLines.Surface(m, plan[i]))];
        }
        if (z.Any(x => x == null))
        {
            notes.Add("Part of it has no height and no ground under it: it is laid at its neighbours' height there.");
            var known = z.FirstOrDefault(x => x != null) ?? 0;
            for (var i = 0; i < z.Count; i++) { if (z[i] == null) z[i] = known; else known = z[i]!.Value; }
        }
        var lift = e.NumOrNull("lift") ?? 0;
        return new Line(plan, [.. z.Select(x => x!.Value + lift)], closed, notes);
    }

    /// <summary>The plan length of its line.</summary>
    public static double Length(Line l) => SiteLines.Stations(l.Plan, l.Closed) is { Length: > 0 } st ? st[^1] : 0;

    /// <summary>
    /// The kerb as solids, one per run of the same reveal: its line split where a drop starts or ends, rings of its profile at each
    /// point, on the bisector (stretched by 1/cos of half the turn, at most 4) where it turns, square to its piece where it stops.
    /// </summary>
    public static List<(int Run, Solid Solid)> Solids(IModelView m, Entity k)
    {
        var o = new List<(int, Solid)>();
        var l = Laid(m, k);
        if (l.Plan.Count < 2) return o;
        var s = Kerbs.SpecOf(k, 1);
        var profile = Kerbs.Profile(m, k, s);
        var right = k.Str("side") != "left";
        var drops = DropsOf(k);
        // The line with a point where each drop starts and ends.
        var (pts, zs) = (l.Plan.ToList(), l.Z.ToList());
        if (l.Closed) { pts.Add(pts[0]); zs.Add(zs[0]); }
        var st = SiteLines.Stations(pts, false).ToList();
        foreach (var cut in drops.SelectMany(d => new[] { d.From, d.To }).Where(x => x > 1e-6 && x < st[^1] - 1e-6).Distinct().OrderBy(x => x))
        {
            var i = st.FindIndex(x => x >= cut - 1e-9);
            if (i < 0 || Math.Abs(st[i] - cut) < 1e-6) continue;
            var f = (cut - st[i - 1]) / (st[i] - st[i - 1]);
            pts.Insert(i, pts[i - 1] + (pts[i] - pts[i - 1]) * f);
            zs.Insert(i, zs[i - 1] + (zs[i] - zs[i - 1]) * f);
            st.Insert(i, cut);
        }
        double? RevealAt(double a, double b) => drops.FirstOrDefault(d => (a + b) / 2 >= d.From - 1e-9 && (a + b) / 2 <= d.To + 1e-9)?.Reveal;
        var n = pts.Count;
        // Each piece's outward normal: to the right of the line (or its left), away from the paving.
        Vec2 Out(int i) { var d = (pts[i + 1] - pts[i]).Normalized(); return right ? new Vec2(d.Y, -d.X) : new Vec2(-d.Y, d.X); }
        // Runs of pieces of the same reveal.
        var runs = new List<(int From, int To, double? Reveal)>();
        for (var i = 0; i + 1 < n; i++)
        {
            if ((pts[i + 1] - pts[i]).Length < 1e-9) continue;
            var r = RevealAt(st[i], st[i + 1]);
            if (runs.Count > 0 && runs[^1].To == i && runs[^1].Reveal == r) runs[^1] = (runs[^1].From, i + 1, r);
            else runs.Add((i, i + 1, r));
        }
        for (var ri = 0; ri < runs.Count; ri++)
        {
            var (from, to, reveal) = runs[ri];
            var ring = reveal is { } rv ? Kerbs.Clamp(profile, Math.Max(-s.Depth, rv)) : profile;
            if (ring.Count < 3) continue;
            var rings = new List<IReadOnlyList<Vec3>>();
            for (var i = from; i <= to; i++)
            {
                // The way round at a point: inside the line the bisector; at its ends square, unless a closed line goes on round.
                var hasIn = i > 0 || l.Closed;
                var hasOut = i < n - 1 || l.Closed;
                var inSeg = i > 0 ? i - 1 : n - 2;
                var outSeg = i < n - 1 ? i : 0;
                Vec2 nrm;
                double stretch = 1;
                if (!hasIn) nrm = Out(outSeg);
                else if (!hasOut) nrm = Out(inSeg);
                else
                {
                    var (a, b) = (Out(inSeg), Out(outSeg));
                    var bis = (a + b).Normalized();
                    if (bis.Length < 1e-9) bis = a;
                    var cos = Vec2.Dot(bis, a);
                    stretch = Math.Min(4, cos > 1e-6 ? 1 / cos : 4);
                    nrm = bis;
                }
                var (p, z) = (pts[i], zs[i]);
                rings.Add([.. ring.Select(v => (p + nrm * (v.X * stretch)).At(z + v.Y))]);
            }
            if (SiteGeometry.LoftMany(rings, "kerb") is { } solid) o.Add((ri, solid));
        }
        return o;
    }

    public static ElementGeometry Geometry(Entity k, IModelView m)
    {
        var l = Laid(m, k);
        if (l.Plan.Count < 2) return new ElementGeometry([], Diagnostics: l.Notes.Count > 0 ? l.Notes : ["A kerb needs two points at least."]);
        var material = k.Str("material");
        var color = material != null ? Materials.ColorOf(m, material) : Kerbs.Color;
        var parts = Solids(m, k).Select((x, i) => new GeometryPart(i == 0 ? "Kerb" : $"Kerb {i + 1}", color, x.Solid, material, "kerb:" + x.Run)).ToList();
        // In plan: its line (the paving's edge) and its back, the width out.
        var s = Kerbs.SpecOf(k, 1);
        var right = k.Str("side") != "left";
        var back = Polyline2.Offset(l.Plan, right ? -s.Width : s.Width, l.Closed, maxMitre: 4);
        var plan = new List<PlanItem> { new("heavy", l.Closed, l.Plan), new("heavy", l.Closed, back) };
        return new ElementGeometry(parts, plan, [l.Plan[0], l.Plan[^1]], l.Notes.Count > 0 ? l.Notes : null);
    }

    public static IEnumerable<QuantitySet> Quantities(Entity k, ElementGeometry? g, IModelView m)
    {
        if (g == null) yield break;
        var l = Laid(m, k);
        if (l.Plan.Count < 2) yield break;
        var length = Length(l);
        var dropped = DropsOf(k).Sum(d => Math.Max(0, Math.Min(d.To, length) - Math.Max(d.From, 0)));
        yield return new QuantitySet("Lapin_KerbQuantities",
        [
            new("Length", length, "LENGTH", "Length"),
            new("DroppedLength", dropped, "LENGTH", "Dropped"),
            new("Volume", g.Parts.Sum(p => Measure.Volume(p.Solid)), "VOLUME", "Volume"),
        ]);
    }

    /// <summary>The kerbs on a terrain (those it is given to cut).</summary>
    public static IEnumerable<Entity> Of(IModelView m, string terrainId) => m.OfClass(Kerbs.Class).Where(e => Is(e) && e.Str("terrain") == terrainId);

    /// <summary>
    /// What a standalone kerb command sets, over its shape's ({shape?, profileId?, height?, width?, depth?, material?}, as
    /// setHardscapeKerb): {points + arcs? + closed? | along {id, edge?, side?, offset?}, side? (right | left), follow? (line | ground),
    /// lift? (m over its line), drops? [{from, to, reveal}], drop? {from, to, reveal | null} (one added, or those it overlaps taken
    /// away with reveal null)}.
    /// </summary>
    static void Apply(ICommandContext ctx, JsonObject attrs, JsonObject a)
    {
        var m = ctx.Model;
        if (a["along"] != null)
        {
            attrs["along"] = SiteLines.AlongArg(m, a["along"]);
            attrs.Remove("points"); attrs.Remove("arcs"); attrs.Remove("closed");
        }
        else if (a["points"] != null)
        {
            var (c, z) = FeatureLines.ReadPoints(a["points"]);
            if (c.Count < 2) throw new CommandException("A kerb needs two points at least.");
            attrs["points"] = FeatureLines.PointsJson(c, z);
            if (a["arcs"] is JsonArray arcs) attrs["arcs"] = arcs.DeepClone(); else attrs.Remove("arcs");
            if (a["closed"] is JsonValue cv && cv.TryGetValue<bool>(out var closed) && closed && c.Count >= 3) attrs["closed"] = true; else attrs.Remove("closed");
            attrs.Remove("along");
        }
        if ((string?)a["side"] is { } side) { if (!Sides.Contains(side)) throw new CommandException("A kerb stands on the right or the left of its line."); attrs["side"] = side; }
        if ((string?)a["follow"] is { } follow) { if (!Follows.Contains(follow)) throw new CommandException("A kerb follows its line's heights or the ground."); attrs["follow"] = follow; }
        if (a.ContainsKey("lift")) { if (Json.Number(a["lift"]) is { } lift && Math.Abs(lift) > 1e-12) attrs["lift"] = Math.Round(lift, 6); else attrs.Remove("lift"); }
        var drops = DropsOf(new Entity("probe", Kerbs.Class, "probe", attrs));
        if (a["drops"] is JsonArray da)
            drops = [.. da.OfType<JsonObject>().Select(o => Json.Number(o["from"]) is { } f && Json.Number(o["to"]) is { } t
                ? new Drop(Math.Min(f, t), Math.Max(f, t), Json.Number(o["reveal"]) is { } r ? r : throw new CommandException("A drop's reveal is its height over the paving (0 at a crossing)."))
                : throw new CommandException("A drop is {from, to, reveal}: stations along the kerb."))];
        if (a["drop"] is JsonObject one)
        {
            var (f, t) = (Json.Number(one["from"]) ?? throw new CommandException("Where does the drop start? from, a station."), Json.Number(one["to"]) ?? throw new CommandException("Where does it end? to, a station."));
            (f, t) = (Math.Min(f, t), Math.Max(f, t));
            drops = [.. drops.Where(d => d.To <= f + 1e-9 || d.From >= t - 1e-9)];
            if (Json.Number(one["reveal"]) is { } r) drops.Add(new Drop(f, t, r));
        }
        if (drops.Any(d => d.Reveal < 0)) throw new CommandException("A dropped kerb's reveal is 0 or more above the paving.");
        if (drops.Any(d => d.To - d.From < 1e-6)) throw new CommandException("A drop runs from one station to a further one.");
        if (drops.Count > 0)
            attrs["drops"] = new JsonArray([.. drops.OrderBy(d => d.From).Select(d => (JsonNode)new JsonObject { ["from"] = Math.Round(d.From, 6), ["to"] = Math.Round(d.To, 6), ["reveal"] = Math.Round(d.Reveal, 6) })]);
        else attrs.Remove("drops");
        Kerbs.ApplyShape(ctx, attrs, a);
    }

    /// <summary>civil.addKerb: a standalone kerb, cutting the terrain under it unless standalone. Its id.</summary>
    public static JsonObject Add(ICommandContext ctx, JsonObject a)
    {
        var m = ctx.Model;
        if (a["points"] == null && a["along"] == null) throw new CommandException("A kerb runs along points drawn or along an element: give points or along.");
        var attrs = new JsonObject { ["exportAs"] = Kerbs.IfcKerbClass };
        if (a["follow"] == null) attrs["follow"] = a["along"] != null ? "line" : "ground";
        KerbLines.Apply(ctx, attrs, a);
        var probe = new Entity("probe", Kerbs.Class, "probe", (JsonObject)attrs.DeepClone());
        var l = Laid(m, probe);
        if (l.Plan.Count < 2) throw new CommandException(l.Notes.FirstOrDefault() ?? "A kerb needs two points at least.");
        var standalone = a["standalone"] is JsonValue sv && sv.TryGetValue<bool>(out var st) && st;
        var terrain = standalone ? null : (string?)a["terrainId"] is { Length: > 0 } tid ? Terrains.Require(m, tid) : SiteLines.TerrainUnder(m, l.Plan);
        if (terrain != null) attrs["terrain"] = terrain.Id;
        var n = m.OfClass(Kerbs.Class).Count(Is) + 1;
        var name = ((string?)a["name"])?.Trim() is { Length: > 0 } nm ? nm : $"Kerb {n}";
        var e = new Entity(ctx.NewId(), Kerbs.Class, name, attrs);
        ctx.Add(e);
        var storey = (string?)a["storeyId"] ?? (terrain != null ? Spatial.StoreyOf(m, terrain.Id)?.Id : null) ?? Spatial.Storeys(m).FirstOrDefault()?.Id
            ?? throw new CommandException("The project has no storey.");
        Spatial.Contain(ctx, storey, e.Id);
        var o = new JsonObject { ["id"] = e.Id };
        if (terrain != null) o["terrainId"] = terrain.Id;
        return o;
    }

    public static void Register(IModuleRegistry r)
    {
        r.Service<IElementPlacement>(new LinePlacement(Kerbs.Class));
        r.Service<IElementRise>(new Rise());
        r.Service<INodeProps>(new Props());

        // A standalone kerb (T6): {points [[x, y] | [x, y, z]…], arcs?, closed? | along {id, edge?, side?, offset?}, side? (right |
        // left: where it stands from its line, away from the paving), follow? (line | ground; drawn: ground, along: line), lift?,
        // drops? [{from, to, reveal}], shape? | profileId?, height?, width?, depth?, material?, terrainId? | standalone?, name?, storeyId?}.
        r.Command("addKerb", "Kerb", (ctx, a) => KerbLines.Add(ctx, a));

        // Changed any time, one undo step: {id, …as addKerb, drop? {from, to, reveal | null}, terrainId?, standalone?}.
        r.Command("updateKerb", "Change kerb", (ctx, a) =>
        {
            var e = ctx.Model.Get((string?)a["id"] ?? "") is { } x && Is(x) ? x : throw new CommandException("There is no such standalone kerb (a hardscape's kerb is changed with its hardscape).");
            var attrs = e.CloneAttrs();
            KerbLines.Apply(ctx, attrs, a);
            if (a["standalone"] is JsonValue sv && sv.TryGetValue<bool>(out var st) && st) attrs.Remove("terrain");
            else if ((string?)a["terrainId"] is { Length: > 0 } tid) attrs["terrain"] = Terrains.Require(ctx.Model, tid).Id;
            var name = ((string?)a["name"])?.Trim() is { Length: > 0 } nm ? nm : null;
            ctx.Replace(e.With(attrs: attrs, name: name));
            return null;
        });
    }

    /// <summary>A 3D move's vertical part: its own points' heights, else its lift over what it follows.</summary>
    sealed class Rise : IElementRise
    {
        public bool Handles(Entity e) => Is(e);

        public bool Lift(ICommandContext ctx, Entity e, double dz)
        {
            var attrs = e.CloneAttrs();
            var s = FeatureLines.SpecOf(e.Attrs);
            if (e.Attrs["points"] != null && e.Str("follow") != "ground" && s.Z.All(z => z != null))
                attrs["points"] = FeatureLines.PointsJson(s.Corners, [.. s.Z.Select(z => z + dz)]);
            else attrs["lift"] = Math.Round((e.NumOrNull("lift") ?? 0) + dz, 6);
            ctx.Replace(e.With(attrs: attrs));
            return true;
        }
    }

    /// <summary>What the interface shows of a standalone kerb: its length, its line's heights at its ends.</summary>
    sealed class Props : INodeProps
    {
        public void Add(Entity e, IModelView m, JsonObject props)
        {
            if (!Is(e)) return;
            var l = Laid(m, e);
            if (l.Plan.Count < 2) return;
            props["standalone"] = true;
            props["length"] = Math.Round(Length(l), 6);
            props["startZ"] = Math.Round(l.Z[0], 6);
            props["endZ"] = Math.Round(l.Z[^1], 6);
            if (l.Notes.Count > 0) props["notes"] = new JsonArray([.. l.Notes.Select(x => (JsonNode)x)]);
            if (e.Str("terrain") is { } t) props["terrainId"] = t;
        }
    }
}
