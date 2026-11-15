using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>
/// Make a layered wall from solids (forum topic 101): a wall another program modelled as separate parallel solids, one
/// per layer (cladding, battens, boards, insulation with its studs, gypsum…), made one Lapin wall with a wall type of
/// those layers, on its axis, as long and as high as the solids, with the openings that go through every layer. An
/// explicit command after an import (or on Studio solids), never done by an import itself: the file stays as it was.
/// <para>
/// The solids are read from their triangles: each one's plan is fitted with its smallest rectangle, which gives its
/// direction, its length and its thickness. Solids running the same way, overlapping along and up, and side by side
/// across with no more than <see cref="MaxGap"/> between them, are one wall; several such sets are several walls,
/// joined where they meet. Across each wall, the solids that share a stretch of its thickness are one layer: its
/// material is the one most of it is made of (a solid's material, else its name), and the other solids in it (the studs
/// in an insulation layer, the battens in a ventilated gap) are reported, or the layer becomes a timber frame of the wood
/// module with their section and spacing. A gap between two layers becomes a layer of air, or goes to the thicker
/// neighbour; each thickness is rounded to a millimetre. The core is the structural layer (by its name, the timber frame,
/// else the thickest), the user's choice otherwise. An opening is a hole whose head (a face looking down inside the
/// solid, across its whole thickness) lies over a sill or the floor; one found in every layer that runs past it is the
/// wall's, as large as the smallest of them, a door or window solid standing in it becoming a door or window.
/// </para>
/// <para>
/// What a solid differs from the wall made of it by more than 1 mm or 0.5 % of its volume (as RecognitionFidelity
/// measures an import) is listed before anything is made. The solids are then kept hidden (<see cref="Superseded"/>),
/// deleted, or kept as they were. One undo step.
/// </para>
/// </summary>
public static class WallsFromSolids
{
    /// <summary>What a side may differ by (RecognitionFidelity's millimetre), and a volume (its 0.5 %).</summary>
    const double Tol = 0.001, VolumeTol = 0.005;
    /// <summary>The widest gap between two layers of one wall (a ventilated cavity).</summary>
    public const double MaxGap = 0.1;
    /// <summary>How far from parallel two solids of one wall may run (the sine of the angle: about a third of a degree).</summary>
    const double Parallel = 0.005;
    /// <summary>A wall-long solid (a layer's board, not a stud): at least this long in plan, three times its thickness.</summary>
    const double MinPlate = 0.3;
    /// <summary>The wood module's frame on a wall type (FrameSpec.Key): written when the stud layer is made a timber frame.</summary>
    public const string FrameKey = "wood";
    public static readonly string[] GapModes = ["air", "absorb"];
    public static readonly string[] OriginalModes = ["hide", "delete", "keep"];
    public const string AirMaterial = "Air gap";

    static readonly Regex Structural = new(@"concrete|beton|betong|brick|tegel|masonry|block|c[aă]r[aă]mid|zid[aă]rie|\bclt\b|kl-tr[aä]|stone|sten\b|timber frame|stomme|regel", RegexOptions.IgnoreCase);
    static readonly Regex DoorName = new(@"door|d[oö]rr|u[sș][aă]\b|t[uü]r\b|porte", RegexOptions.IgnoreCase);
    static readonly Regex WindowName = new(@"window|f[oö]nster|fereastr|fenster|fen[eê]tre", RegexOptions.IgnoreCase);
    /// <summary>A name that says nothing of a material: a program's default for a piece.</summary>
    static readonly Regex Generic = new(@"^\s*(solid|box|mesh|group|component|object|reference|body|part|piece|layer|proxy|element|instance|grupp|komponent)?\s*[#_\-]?\s*\d*\s*$", RegexOptions.IgnoreCase);

    // ─── What is read ───────────────────────────────────────────────────────

    sealed class Member
    {
        public required Entity E;
        public required string Material;
        public required bool Named;
        public required List<Vec3> V;
        public required List<(int A, int B, int C)> T;
        public double Volume;
        public Vec2 Dir;
        public double Length, Width, Z0, Z1;
        public bool Plate;
        public string? Fill;
        public string? Storey;
        public int Order;
    }

    /// <summary>A member in its wall's frame: along (s), across (c, left of the wall's direction) and up.</summary>
    sealed record Placed(Member M, double S0, double S1, double C0, double C1);

    sealed class Layer
    {
        public List<Placed> Members = [];
        public double C0, C1;
        public string Material = "";
        public bool Named;
        /// <summary>solid (a layer of one material), framing (pieces with nothing between: battens), air (a gap).</summary>
        public string Kind = "solid";
        /// <summary>The members of another material inside it (studs, plates, noggins).</summary>
        public List<Placed> Inside = [];
        public string? InsideMaterial;
        public double Thickness;
        public double? StudWidth, Spacing;
        public double Rc0, Rc1;
        public List<Hole> Holes = [];
        public IEnumerable<Placed> Own => Members.Where(p => p.M.Plate && p.M.Material == Material);
    }

    sealed record Hole(double S0, double S1, double Sill, double Head);

    sealed record Opening(double S0, double S1, double Sill, double Head, Member? Fill, string Kind);

    sealed record Difference(string Id, string Name, string What, double Mm);

    sealed class Wall
    {
        public required Vec2 O, U;
        public List<Placed> Members = [];
        public List<Layer> Layers = [];
        public double S0, S1, Z0, Z1;
        public int Core;
        public List<Opening> Openings = [];
        public List<Difference> Differences = [];
        public List<string> Notes = [];
        public string? Storey;
        public string? Name;
        public bool Frame;
        public Vec2 N => U.Perp;
        public double CoreMiddle => (Layers[Core].Rc0 + Layers[Core].Rc1) / 2;
    }

    sealed record Found(List<Wall> Walls, List<(Entity E, string Why)> Left);

    // ─── Reading ────────────────────────────────────────────────────────────

    /// <summary>
    /// The walls the selected solids make, as the dialog shows them and the command makes them: {ids, gaps (air | absorb),
    /// frame (the stud layer a timber frame), walls: [{core, materials, name}] (each wall's choices, by its index)}.
    /// </summary>
    public static JsonObject Preview(IModelView m, IServices services, JsonObject a)
    {
        var found = Find(m, services, a);
        var o = Json(found);
        o["frameAvailable"] = true;
        return o;
    }

    static Found Find(IModelView m, IServices services, JsonObject a)
    {
        var ids = (a["ids"] as JsonArray)?.Select(x => (string?)x).OfType<string>().ToList() ?? [];
        if (ids.Count == 0) throw new CommandException("Select the solids that make the wall.");
        var gaps = (string?)a["gaps"] ?? "air";
        if (!GapModes.Contains(gaps)) throw new CommandException("A gap between layers becomes an air layer (air) or goes to its neighbour (absorb).");
        var frame = a["frame"] is JsonValue fv && fv.TryGetValue<bool>(out var fb) && fb;
        var choices = (a["walls"] as JsonArray)?.Select(x => x as JsonObject).ToList() ?? [];
        var (elements, _) = Groups.Expand(m, ids);
        var left = new List<(Entity, string)>();
        var members = new List<Member>();
        foreach (var id in elements)
        {
            if (m.Get(id) is not { } e) continue;
            if (e.Class is IfcClass.Wall && !StoredBodies.Has(e)) { left.Add((e, "it is a Lapin wall already")); continue; }
            if (Spatial.StoreyOf(m, e.Id) == null) continue;
            if (Read(e, m, services, members.Count) is { } member) members.Add(member);
            else left.Add((e, "it has no body to read"));
        }
        var walls = Cluster(members, left);
        for (var i = 0; i < walls.Count; i++)
        {
            var w = walls[i];
            var choice = i < choices.Count ? choices[i] : null;
            Layering(w, gaps, frame, choice);
            Read(w);
            Measure(w);
        }
        return new(walls, left);
    }

    static Member? Read(Entity e, IModelView m, IServices services, int order)
    {
        ElementGeometry? g = null;
        try
        {
            g = StoredBodies.Has(e) ? StoredBodies.Geometry(e, m, services)
                : services.Get<IGeometryProducers>()?.Geometry(e.Class) is { } producer ? producer(e, m, services) : null;
        }
        catch (Exception ex) when (ex is ModelException or InvalidOperationException or ArgumentException or IndexOutOfRangeException or NullReferenceException or CommandException) { g = null; }
        if (g is not { Parts.Count: > 0 }) return null;
        var v = new List<Vec3>();
        var t = new List<(int, int, int)>();
        string? materialId = null;
        string? partName = null;
        foreach (var p in g.Parts)
        {
            if (p.Instances != null) continue;
            var off = v.Count;
            if (p.Mesh is { } mesh)
            {
                for (var i = 0; i + 2 < mesh.Xyz.Length; i += 3) v.Add(new Vec3(mesh.Xyz[i], mesh.Xyz[i + 1], mesh.Xyz[i + 2]));
                for (var i = 0; i + 2 < mesh.Tris.Length; i += 3) t.Add((mesh.Tris[i] + off, mesh.Tris[i + 1] + off, mesh.Tris[i + 2] + off));
            }
            else
            {
                v.AddRange(p.Solid.Vertices);
                t.AddRange(Lapin.Geometry.Measure.Triangles(p.Solid).Select(x => (x.A + off, x.B + off, x.C + off)));
            }
            materialId ??= p.Material;
            partName ??= p.Name;
        }
        if (t.Count < 4) return null;
        // The material: the solid's own, else a name that says one (its piece's, its own), else a placeholder to rename.
        var material = materialId is { } mid ? m.Get(mid)?.Name ?? mid : null;
        var named = material != null;
        if (material == null)
        {
            var name = partName is { Length: > 0 } pn && pn != e.Name && !Generic.IsMatch(pn) && pn != "body" ? pn : Clean(e.Name);
            if (name is { Length: > 0 } && !Generic.IsMatch(name)) { material = name; named = true; }
        }
        var cls = e.Str("exportAs") ?? e.Class;
        var fill = cls is IfcClass.Door ? "door" : cls is IfcClass.Window ? "window"
            : DoorName.IsMatch(e.Name) ? "door" : WindowName.IsMatch(e.Name) ? "window" : null;
        var (dir, length, width) = Rectangle(v.Select(p => new Vec2(p.X, p.Y)).ToList());
        var member = new Member
        {
            E = e, Material = material ?? "", Named = named, V = v, T = t, Volume = Math.Abs(SignedVolume(v, t)),
            Dir = dir, Length = length, Width = width, Z0 = v.Min(p => p.Z), Z1 = v.Max(p => p.Z),
            Fill = fill, Storey = Spatial.StoreyOf(m, e.Id)?.Id, Order = order,
        };
        member.Plate = fill == null && length >= MinPlate && length >= 3 * width && member.Z1 - member.Z0 > 0.01;
        return member;
    }

    /// <summary>A piece's name made a material's: its copy number dropped ("Gips 2", "Gips#3" → "Gips").</summary>
    static string Clean(string name) => Regex.Replace(name.Trim(), @"\s*([#_]\d+|\(\d+\)|\s\d+)$", "").Trim();

    static double SignedVolume(List<Vec3> v, List<(int A, int B, int C)> t)
    {
        if (v.Count == 0) return 0;
        var o = v[0];
        double sum = 0;
        foreach (var (a, b, c) in t) sum += Vec3.Dot(v[a] - o, Vec3.Cross(v[b] - o, v[c] - o));
        return sum / 6;
    }

    /// <summary>The smallest rectangle round the plan: its long direction (pointing +X, or +Y when across X), its length and its width.</summary>
    static (Vec2 Dir, double Length, double Width) Rectangle(List<Vec2> pts)
    {
        var hull = Polygon2.ConvexHull(pts);
        if (hull.Length < 2) return (new Vec2(1, 0), 0, 0);
        var best = (Dir: new Vec2(1, 0), L: 0.0, W: 0.0, Area: double.MaxValue);
        for (var i = 0; i < hull.Length; i++)
        {
            var edge = hull[(i + 1) % hull.Length] - hull[i];
            if (edge.Length < 1e-9) continue;
            var u = edge.Normalized();
            var n = u.Perp;
            double u0 = double.MaxValue, u1 = double.MinValue, n0 = double.MaxValue, n1 = double.MinValue;
            foreach (var p in hull)
            {
                var (du, dn) = (Vec2.Dot(p, u), Vec2.Dot(p, n));
                u0 = Math.Min(u0, du); u1 = Math.Max(u1, du); n0 = Math.Min(n0, dn); n1 = Math.Max(n1, dn);
            }
            var area = (u1 - u0) * (n1 - n0);
            if (area < best.Area - 1e-12) best = (u, u1 - u0, n1 - n0, area);
        }
        var (dir, l, w) = best.L >= best.W ? (best.Dir, best.L, best.W) : (best.Dir.Perp, best.W, best.L);
        if (dir.X < -1e-9 || Math.Abs(dir.X) <= 1e-9 && dir.Y < 0) dir = new Vec2(-dir.X, -dir.Y);
        return (dir, l, w);
    }

    static Placed Place(Member m, Vec2 o, Vec2 u)
    {
        var n = u.Perp;
        double s0 = double.MaxValue, s1 = double.MinValue, c0 = double.MaxValue, c1 = double.MinValue;
        foreach (var p in m.V)
        {
            var q = new Vec2(p.X, p.Y) - o;
            var (s, c) = (Vec2.Dot(q, u), Vec2.Dot(q, n));
            s0 = Math.Min(s0, s); s1 = Math.Max(s1, s); c0 = Math.Min(c0, c); c1 = Math.Max(c1, c);
        }
        return new Placed(m, s0, s1, c0, c1);
    }

    static double Overlap(double a0, double a1, double b0, double b1) => Math.Min(a1, b1) - Math.Max(a0, b0);

    /// <summary>The solids sorted into walls: the wall-long ones linked by running together side by side, the rest put in the wall they stand in.</summary>
    static List<Wall> Cluster(List<Member> members, List<(Entity, string)> left)
    {
        var plates = members.Where(x => x.Plate).ToList();
        var parent = Enumerable.Range(0, plates.Count).ToArray();
        int Root(int i) => parent[i] == i ? i : parent[i] = Root(parent[i]);
        for (var i = 0; i < plates.Count; i++)
            for (var j = i + 1; j < plates.Count; j++)
            {
                var (a, b) = (plates[i], plates[j]);
                if (Math.Abs(Vec2.Cross(a.Dir, b.Dir)) > Parallel || a.Storey != b.Storey) continue;
                var o = new Vec2(a.V[0].X, a.V[0].Y);
                var (pa, pb) = (Place(a, o, a.Dir), Place(b, o, a.Dir));
                if (Overlap(pa.S0, pa.S1, pb.S0, pb.S1) < 0.5 * Math.Min(pa.S1 - pa.S0, pb.S1 - pb.S0)) continue;
                if (Overlap(a.Z0, a.Z1, b.Z0, b.Z1) < 0.5 * Math.Min(a.Z1 - a.Z0, b.Z1 - b.Z0)) continue;
                if (Math.Max(pb.C0 - pa.C1, pa.C0 - pb.C1) > MaxGap) continue;
                parent[Root(i)] = Root(j);
            }
        var walls = new List<Wall>();
        foreach (var set in Enumerable.Range(0, plates.Count).GroupBy(Root).OrderBy(g => g.Min(i => plates[i].Order)))
        {
            var ps = set.Select(i => plates[i]).ToList();
            var longest = ps.MaxBy(p => p.Length)!;
            var w = new Wall { O = new Vec2(longest.V[0].X, longest.V[0].Y), U = longest.Dir, Storey = longest.Storey };
            w.Members.AddRange(ps.OrderBy(p => p.Order).Select(p => Place(p, w.O, w.U)));
            walls.Add(w);
        }
        // The pieces that are no layer of their own (studs, battens, a door's or window's solid): in the wall whose stretch they stand in.
        foreach (var x in members.Where(x => !x.Plate).OrderBy(x => x.Order))
        {
            Wall? home = null;
            foreach (var w in walls)
            {
                if (x.Storey != w.Storey) continue;
                var p = Place(x, w.O, w.U);
                var (s0, s1) = (w.Members.Min(q => q.S0), w.Members.Max(q => q.S1));
                var (c0, c1) = (w.Members.Min(q => q.C0), w.Members.Max(q => q.C1));
                var (z0, z1) = (w.Members.Min(q => q.M.Z0), w.Members.Max(q => q.M.Z1));
                var slack = x.Fill != null ? 0.15 : MaxGap;
                if (p.S0 < s0 - 0.05 || p.S1 > s1 + 0.05 || p.C1 < c0 - slack || p.C0 > c1 + slack || Overlap(x.Z0, x.Z1, z0, z1) <= 0) continue;
                home = w;
                w.Members.Add(p);
                break;
            }
            if (home == null) left.Add((x.E, x.Fill != null ? $"a {x.Fill} standing in no wall found" : "it stands in no wall found (no wall-long solid beside it)"));
        }
        return walls;
    }

    /// <summary>The wall's layers across it, their materials and thicknesses, the gaps between them, the core.</summary>
    static void Layering(Wall w, string gaps, bool frame, JsonObject? choice)
    {
        var layers = new List<Layer>();
        // The wall-long solids first, the biggest first: each joins the layer it shares half its thickness with, or starts one.
        foreach (var p in w.Members.Where(p => p.M.Plate).OrderByDescending(p => p.M.Volume))
        {
            var l = layers.FirstOrDefault(l => Overlap(l.C0, l.C1, p.C0, p.C1) >= 0.5 * Math.Min(l.C1 - l.C0, p.C1 - p.C0) - 1e-6);
            if (l == null) layers.Add(l = new Layer { C0 = p.C0, C1 = p.C1 });
            l.Members.Add(p);
        }
        // Then the pieces (studs, battens): in the layer they overlap most, or a layer of their own (battens with air between).
        foreach (var p in w.Members.Where(p => !p.M.Plate && p.M.Fill == null))
        {
            var l = layers.Where(l => Overlap(l.C0, l.C1, p.C0, p.C1) > 0.5 * Math.Min(l.C1 - l.C0, p.C1 - p.C0)).MaxBy(l => Overlap(l.C0, l.C1, p.C0, p.C1));
            if (l == null) layers.Add(l = new Layer { C0 = p.C0, C1 = p.C1, Kind = "framing" });
            else if (l.Kind == "framing") { l.C0 = Math.Min(l.C0, p.C0); l.C1 = Math.Max(l.C1, p.C1); }
            l.Members.Add(p);
        }
        var (ws0, ws1) = (w.Members.Where(p => p.M.Plate).Min(p => p.S0), w.Members.Where(p => p.M.Plate).Max(p => p.S1));
        foreach (var l in layers)
        {
            var plates = l.Members.Where(p => p.M.Plate).ToList();
            if (plates.Count > 0)
            {
                // Its material: the one most of its wall-long solids are made of; its stretch across, theirs.
                var main = plates.GroupBy(p => p.M.Material).MaxBy(g => g.Sum(p => p.M.Volume))!;
                l.Material = main.Key;
                l.Named = main.First().M.Named;
                l.C0 = main.Min(p => p.C0);
                l.C1 = main.Max(p => p.C1);
                // Along: what its solids of that material cover, against the wall's length.
                var covered = Covered(main.Select(p => (p.S0, p.S1)));
                l.Kind = covered >= 0.5 * (ws1 - ws0) ? "solid" : "framing";
            }
            else
            {
                var main = l.Members.GroupBy(p => p.M.Material).MaxBy(g => g.Sum(p => p.M.Volume))!;
                l.Material = main.Key;
                l.Named = main.First().M.Named;
            }
            l.Inside = l.Kind == "solid" ? [.. l.Members.Where(p => p.M.Material != l.Material)] : [];
            if (l.Inside.Count > 0)
            {
                l.InsideMaterial = l.Inside.GroupBy(p => p.M.Material).MaxBy(g => g.Count())!.Key;
                // The studs: the pieces standing (almost) the layer's height; their width along the wall and their spacing.
                var height = l.Members.Max(p => p.M.Z1) - l.Members.Min(p => p.M.Z0);
                var studs = l.Inside.Where(p => p.M.Z1 - p.M.Z0 >= 0.6 * height && p.S1 - p.S0 < MinPlate).OrderBy(p => p.S0).ToList();
                if (studs.Count > 0) l.StudWidth = Median(studs.Select(p => p.S1 - p.S0));
                if (studs.Count > 1) l.Spacing = Median(studs.Zip(studs.Skip(1)).Select(z => (z.Second.S0 + z.Second.S1 - z.First.S0 - z.First.S1) / 2).Where(d => d > 0.1));
            }
        }
        layers = [.. layers.OrderBy(l => (l.C0 + l.C1) / 2)];
        // The gaps between them: an air layer, or the thicker neighbour's; an overlap taken off the thicker.
        foreach (var l in layers) l.Thickness = l.C1 - l.C0;
        var result = new List<Layer>();
        for (var i = 0; i < layers.Count; i++)
        {
            if (i > 0)
            {
                var (prev, next) = (layers[i - 1], layers[i]);
                var gap = next.C0 - prev.C1;
                if (gap > 0.0005 && gaps == "air" && Math.Round(gap, 3) >= 0.001)
                    result.Add(new Layer { Material = AirMaterial, Named = true, Kind = "air", C0 = prev.C1, C1 = next.C0, Thickness = gap });
                else if (Math.Abs(gap) > 1e-9)
                {
                    var thicker = prev.Thickness >= next.Thickness ? prev : next;
                    thicker.Thickness += gap;
                    if (gap < -0.0005)
                        w.Notes.Add($"{Label(prev)} and {Label(next)} overlap by {-gap * 1000:0.#} mm: taken off {Label(thicker)}.");
                }
            }
            result.Add(layers[i]);
        }
        foreach (var l in result) l.Thickness = Math.Max(0.001, Math.Round(l.Thickness, 3));
        // The user's names for the materials, by layer.
        if (choice?["materials"] is JsonArray names)
            for (var i = 0; i < Math.Min(names.Count, result.Count); i++)
                if ((string?)names[i] is { } given && given.Trim().Length > 0) { result[i].Material = given.Trim(); result[i].Named = true; }
        for (var i = 0; i < result.Count; i++)
            if (result[i].Material.Length == 0) result[i].Material = $"Material {i + 1}";
        w.Layers = result;
        // The core: asked for, else the timber frame (asked, or the layer with studs), the structural one by its name, the thickest.
        var studLayer = result.FindIndex(l => l.Inside.Count > 0 && l.StudWidth != null);
        w.Frame = frame && studLayer >= 0;
        var asked = choice?["core"] is JsonValue cv && cv.TryGetValue<int>(out var ci) && ci >= 0 && ci < result.Count && result[ci].Kind != "air" ? ci : -1;
        w.Core = asked >= 0 ? asked
            : w.Frame ? studLayer
            : result.FindIndex(l => l.Kind != "air" && Structural.IsMatch(l.Material)) is >= 0 and var s ? s
            : studLayer >= 0 ? studLayer
            : result.Select((l, i) => (l, i)).Where(x => x.l.Kind != "air").MaxBy(x => x.l.Thickness).i;
        if (w.Frame && w.Core != studLayer) w.Frame = false;
        w.Name = (string?)choice?["name"] is { Length: > 0 } n ? n.Trim() : null;
        // Where each layer ends up across: one after the other from the first one's face.
        var at = result[0].C0;
        foreach (var l in result) { l.Rc0 = at; l.Rc1 = at += l.Thickness; }
        // Along and up: the core's.
        var core = result[w.Core];
        var own = core.Own.ToList();
        var span = own.Count > 0 ? own : core.Members;
        (w.S0, w.S1) = (span.Min(p => p.S0), span.Max(p => p.S1));
        (w.Z0, w.Z1) = (span.Min(p => p.M.Z0), span.Max(p => p.M.Z1));
    }

    static string Label(Layer l) => $"„{l.Material}”";

    static double Covered(IEnumerable<(double S0, double S1)> spans)
    {
        double total = 0, end = double.MinValue;
        foreach (var (a, b) in spans.OrderBy(x => x.S0))
        {
            var from = Math.Max(a, end);
            if (b > from) total += b - from;
            end = Math.Max(end, b);
        }
        return total;
    }

    static double Median(IEnumerable<double> xs)
    {
        var l = xs.OrderBy(x => x).ToList();
        return l.Count == 0 ? 0 : l.Count % 2 == 1 ? l[l.Count / 2] : (l[l.Count / 2 - 1] + l[l.Count / 2]) / 2;
    }

    /// <summary>The holes of each solid layer, and the wall's openings: those through every layer that runs past them.</summary>
    static void Read(Wall w)
    {
        foreach (var l in w.Layers.Where(l => l.Kind == "solid")) l.Holes = Holes(w, l.Own.ToList());
        var solid = w.Layers.Where(l => l.Kind == "solid").ToList();
        var seen = new List<Hole>();
        foreach (var h in solid.SelectMany(l => l.Holes))
        {
            if (seen.Any(x => Same(x, h))) continue;
            seen.Add(h);
            // Every solid layer running past it has it.
            var past = solid.Where(l => Covered(l.Own.Select(p => (Math.Max(p.S0, h.S0), Math.Min(p.S1, h.S1)))) >= (h.S1 - h.S0) - 2e-3).ToList();
            var with = past.Select(l => (L: l, H: l.Holes.FirstOrDefault(x => Same(x, h)))).ToList();
            if (with.Any(x => x.H == null))
            {
                var through = with.Where(x => x.H != null).Select(x => Label(x.L));
                w.Notes.Add($"An opening {(h.S1 - h.S0) * 1000:0} × {(h.Head - h.Sill) * 1000:0} mm in {string.Join(", ", through)} only: the wall is left whole there.");
                foreach (var x in with.Where(x => x.H != null))
                    w.Differences.Add(new(x.L.Own.First().M.E.Id, x.L.Own.First().M.E.Name, $"its opening at {(h.S0 - w.S0) * 1000:0} mm is in no other layer", (h.S1 - h.S0) * 1000));
                continue;
            }
            var holes = with.Select(x => x.H!).ToList();
            var o = new Opening(holes.Max(x => x.S0), holes.Min(x => x.S1), holes.Max(x => x.Sill), holes.Min(x => x.Head), null, "opening");
            foreach (var (l, x) in with)
            {
                var d = new[] { o.S0 - x!.S0, x.S1 - o.S1, o.Sill - x.Sill, x.Head - o.Head }.Max();
                if (d > Tol) w.Differences.Add(new(l.Own.First().M.E.Id, l.Own.First().M.E.Name, $"its opening is {d * 1000:0.#} mm larger than the wall's", d * 1000));
            }
            w.Openings.Add(o);
        }
        // A door's or window's solid standing in an opening fills it.
        var fills = w.Members.Where(p => p.M.Fill != null).ToList();
        for (var i = 0; i < w.Openings.Count; i++)
        {
            var o = w.Openings[i];
            var f = fills.FirstOrDefault(p => (p.S0 + p.S1) / 2 is var s && s > o.S0 - 0.15 && s < o.S1 + 0.15 && (p.M.Z0 + p.M.Z1) / 2 is var z && z > o.Sill - 0.15 && z < o.Head + 0.15);
            if (f == null) continue;
            fills.Remove(f);
            w.Openings[i] = o with { Fill = f.M, Kind = f.M.Fill! };
        }
        foreach (var f in fills) w.Notes.Add($"„{f.M.E.Name}” stands in no opening found through the wall: it stays as it is.");
        w.Members.RemoveAll(p => fills.Contains(p));
    }

    static bool Same(Hole a, Hole b) =>
        Overlap(a.S0, a.S1, b.S0, b.S1) > 0.5 * Math.Min(a.S1 - a.S0, b.S1 - b.S0) && Overlap(a.Sill, a.Head, b.Sill, b.Head) > 0.5 * Math.Min(a.Head - a.Sill, b.Head - b.Sill);

    /// <summary>
    /// A layer's holes: each head (the faces looking down inside the solid, across its whole thickness, at one height, over
    /// one stretch along) over the highest sill (a face looking up) under it along that stretch, else over the floor.
    /// </summary>
    static List<Hole> Holes(Wall w, List<Placed> own)
    {
        if (own.Count == 0) return [];
        var (z0, z1) = (own.Min(p => p.M.Z0), own.Max(p => p.M.Z1));
        var (c0, c1) = (own.Min(p => p.C0), own.Max(p => p.C1));
        var faces = new List<(double Z, double S0, double S1, double C0, double C1, bool Down)>();
        foreach (var p in own)
        {
            var v = p.M.V;
            foreach (var (a, b, c) in p.M.T)
            {
                var n = Vec3.Cross(v[b] - v[a], v[c] - v[a]);
                var len = n.Length;
                if (len < 1e-12 || Math.Abs(n.Z / len) < 0.999) continue;
                var z = (v[a].Z + v[b].Z + v[c].Z) / 3;
                if (z <= z0 + 2e-3 || z >= z1 - 2e-3) continue;
                double S(Vec3 q) => Vec2.Dot(new Vec2(q.X, q.Y) - w.O, w.U);
                double C(Vec3 q) => Vec2.Dot(new Vec2(q.X, q.Y) - w.O, w.N);
                var ss = new[] { S(v[a]), S(v[b]), S(v[c]) };
                var cs = new[] { C(v[a]), C(v[b]), C(v[c]) };
                faces.Add((Math.Round(z, 4), ss.Min(), ss.Max(), cs.Min(), cs.Max(), n.Z < 0));
            }
        }
        // The faces at one height merged along: one stretch each, with its reach across.
        static List<(double Z, double S0, double S1, double C0, double C1)> Merge(IEnumerable<(double Z, double S0, double S1, double C0, double C1, bool Down)> fs) =>
            fs.GroupBy(f => f.Z).SelectMany(g =>
            {
                var o = new List<(double Z, double S0, double S1, double C0, double C1)>();
                foreach (var f in g.OrderBy(f => f.S0))
                {
                    if (o.Count > 0 && f.S0 <= o[^1].S1 + 2e-3)
                    {
                        var x = o[^1];
                        o[^1] = (x.Z, x.S0, Math.Max(x.S1, f.S1), Math.Min(x.C0, f.C0), Math.Max(x.C1, f.C1));
                    }
                    else o.Add((f.Z, f.S0, f.S1, f.C0, f.C1));
                }
                return o;
            }).ToList();
        var heads = Merge(faces.Where(f => f.Down)).Where(h => h.C1 - h.C0 >= 0.9 * (c1 - c0) && h.S1 - h.S0 > 0.05).ToList();
        var sills = Merge(faces.Where(f => !f.Down)).Where(h => h.C1 - h.C0 >= 0.9 * (c1 - c0)).ToList();
        var o = new List<Hole>();
        foreach (var h in heads)
        {
            var sill = sills.Where(s => s.Z < h.Z - 2e-3 && Overlap(s.S0, s.S1, h.S0, h.S1) > 0.5 * (h.S1 - h.S0)).Select(s => s.Z).DefaultIfEmpty(z0).Max();
            o.Add(new Hole(h.S0, h.S1, sill, h.Z));
        }
        return o;
    }

    /// <summary>What each solid differs from the wall made of it by: its sides against its layer's, its volume (RecognitionFidelity's 1 mm and 0.5 %).</summary>
    static void Measure(Wall w)
    {
        var holes = w.Openings.Sum(o => (o.S1 - o.S0) * (o.Head - o.Sill));
        foreach (var l in w.Layers.Where(l => l.Kind != "air"))
        {
            var own = l.Kind == "solid" ? l.Own.ToList() : l.Members;
            foreach (var p in own)
            {
                var sides = new List<(double D, string What)>
                {
                    (p.C0 - l.Rc0, "its face"), (p.C1 - l.Rc1, "its other face"), (p.M.Z0 - w.Z0, "its base"), (p.M.Z1 - w.Z1, "its top"),
                };
                if (l.Kind == "solid" && own.Count == 1) { sides.Add((p.S0 - w.S0, "its start")); sides.Add((p.S1 - w.S1, "its end")); }
                var (d, what) = sides.MaxBy(x => Math.Abs(x.D));
                if (Math.Abs(d) > Tol) w.Differences.Add(new(p.M.E.Id, p.M.E.Name, $"{what} is {Math.Abs(d) * 1000:0.#} mm off the wall's", Math.Abs(d) * 1000));
            }
            if (l.Kind != "solid") continue;
            // The layer's volume, its pieces inside it with it: a layer as the wall makes it.
            var made = ((w.S1 - w.S0) * (w.Z1 - w.Z0) - holes) * l.Thickness;
            var have = l.Members.Sum(p => p.M.Volume);
            if (made > 1e-9 && Math.Abs(have - made) / made > VolumeTol && own.Count > 0)
            {
                var p = own.MaxBy(x => x.M.Volume)!;
                var pct = (have - made) / made * 100;
                if (!w.Differences.Any(x => x.Id == p.M.E.Id))
                    w.Differences.Add(new(p.M.E.Id, p.M.E.Name, $"the layer's volume is {(pct > 0 ? "+" : "")}{pct:0.#} % the wall's", Math.Abs(have - made) / (l.Thickness * (w.Z1 - w.Z0)) * 1000));
            }
        }
    }

    // ─── Making ─────────────────────────────────────────────────────────────

    /// <summary>
    /// The walls made: {ids, gaps, frame, walls, originals (hide | delete | keep)}; each wall's id, its type's, its openings', what
    /// was left and what differs.
    /// </summary>
    public static JsonObject Make(ICommandContext ctx, JsonObject a, IPathElements paths)
    {
        var originals = (string?)a["originals"] ?? "hide";
        if (!OriginalModes.Contains(originals)) throw new CommandException("The solids are kept hidden (hide), deleted (delete) or kept (keep).");
        var found = Find(ctx.Model, ctx.Services, a);
        if (found.Walls.Count == 0)
            throw new CommandException(found.Left.Count > 0 ? $"No wall found in the selection: {found.Left[0].E.Name}: {found.Left[0].Why}." : "No wall found in the selection: select the solids of its layers.");
        var o = Json(found);
        var made = new JsonArray();
        var types = new Dictionary<string, Entity>();
        var fillTypes = new Dictionary<string, Entity>();
        var wallIds = new List<string>();
        for (var i = 0; i < found.Walls.Count; i++)
        {
            var w = found.Walls[i];
            var wall = MakeWall(ctx, w, types);
            wallIds.Add(wall.Id);
            var openings = new JsonArray();
            foreach (var op in w.Openings)
                if (MakeOpening(ctx, wall, w, op, fillTypes) is { } id) openings.Add((JsonNode)id);
            // The wall where its solids were: in their group, on their storey.
            var first = w.Members[0].M.E.Id;
            if (Groups.Of(ctx.Model, first) is { } group) ctx.RelateAll(Groups.Rel, group.Id, [wall.Id]);
            var gone = w.Members.Select(p => p.M.E.Id).Concat(w.Openings.Where(x => x.Fill != null).Select(x => x.Fill!.E.Id)).Distinct().ToList();
            Originals(ctx, gone, wall.Id, originals);
            made.Add((JsonNode)new JsonObject { ["id"] = wall.Id, ["typeId"] = Walls.TypeOf(ctx.Model, wall.Id)?.Id, ["openings"] = openings });
        }
        // Joined where they meet: one another, and the walls already there.
        foreach (var id in wallIds) Joins.Auto(ctx, paths, id);
        o["made"] = made;
        return o;
    }

    static Entity MakeWall(ICommandContext ctx, Wall w, Dictionary<string, Entity> types)
    {
        var m = ctx.Model;
        var specs = w.Layers.Select((l, i) => new LayerSpec(l.Material, l.Thickness, i == w.Core)).ToList();
        var signature = string.Join("|", specs.Select(s => $"{s.Material}:{s.Thickness}:{s.Core}")) + (w.Frame ? $"|frame:{FrameSection(w)}:{w.Layers[w.Core].Spacing:0.###}" : "");
        if (!types.TryGetValue(signature, out var type))
        {
            var name = w.Name ?? WallTypes.DefaultName(specs);
            type = m.OfClass(IfcClass.WallType).FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase)
                && string.Join("|", LayerSets.LayersOf(m, t.Id).Select(l => $"{m.Get(l.Material)?.Name}:{l.Thickness}:{l.Core}")) == string.Join("|", specs.Select(s => $"{s.Material}:{s.Thickness}:{s.Core}"))
                && (t.Attrs[FrameKey] != null) == w.Frame)
                // What the solids are: no lintel over their openings, nothing of Lapin's they do not have.
                ?? WallTypes.Create(ctx, name, specs, lintels: ForeignElements.NoLintel.ToJson());
            if (w.Frame && type.Attrs[FrameKey] == null)
            {
                var attrs = type.CloneAttrs();
                var frame = new JsonObject { ["construction"] = "timberFrame", ["studSection"] = FrameSection(w) };
                if (w.Layers[w.Core].Spacing is > 0.1 and var sp) frame["spacing"] = Math.Round(sp, 3);
                attrs[FrameKey] = frame;
                ctx.Replace(type = type.With(attrs: attrs));
            }
            types[signature] = type;
        }
        var storey = w.Storey is { } sid && m.Get(sid) is { } st ? st
            : Spatial.Storeys(m).Where(s => s.Num("elevation") <= w.Z0 + 1e-3).MaxBy(s => s.Num("elevation")) ?? Spatial.Storeys(m).First();
        var axis = w.CoreMiddle;
        var (a, b) = (w.O + w.U * w.S0 + w.N * axis, w.O + w.U * w.S1 + w.N * axis);
        var wall = Walls.AddUnjoined(ctx, storey.Id, a, b, Math.Round(w.Z1 - w.Z0, 4), Math.Round(w.Z0 - storey.Num("elevation"), 4), type);
        // Its layers as the solids lie: the first one on the side it was found on, the wall's axis where its core's middle is.
        wall = m.Get(wall.Id)!;
        var bands = Walls.Section(m, wall);
        var first = bands.First(x => x.Index == 0);
        if ((first.From + first.To) / 2 > 0 != (w.Layers[0].Rc0 + w.Layers[0].Rc1) / 2 - axis > 0) Walls.Flip(ctx, wall);
        wall = m.Get(wall.Id)!;
        var lo = Walls.Section(m, wall).Min(x => x.From);
        var shift = w.Layers[0].Rc0 - (axis + lo);
        if (Math.Abs(shift) > 1e-6)
        {
            var attrs = wall.CloneAttrs();
            var (na, nb) = (a + w.N * shift, b + w.N * shift);
            attrs["start"] = new JsonArray(na.X, na.Y);
            attrs["end"] = new JsonArray(nb.X, nb.Y);
            ctx.Replace(wall = wall.With(attrs: attrs));
        }
        return m.Get(wall.Id)!;
    }

    static string FrameSection(Wall w) => FrameSection(w.Layers[w.Core]);

    /// <summary>A stud layer's section as the wood module writes it: its studs' width along the wall by the layer's thickness.</summary>
    static string FrameSection(Layer l)
    {
        return $"{Math.Round((l.StudWidth ?? 0.045) * 1000):0}x{Math.Round(l.Thickness * 1000):0} mm";
    }

    static string? MakeOpening(ICommandContext ctx, Entity wall, Wall w, Opening op, Dictionary<string, Entity> fillTypes)
    {
        var along = Math.Round((op.S0 + op.S1) / 2 - w.S0, 4);
        var (width, height, sill) = (Math.Round(op.S1 - op.S0, 4), Math.Round(op.Head - op.Sill, 4), Math.Round(op.Sill - w.Z0, 4));
        try
        {
            if (op.Fill == null) return Openings.AddBare(ctx, wall, along, sill, width, height).Id;
            var door = op.Kind == "door";
            var key = $"{op.Kind}:{width}:{height}:{sill}";
            if (!fillTypes.TryGetValue(key, out var type))
            {
                var name = $"{(door ? "Door" : "Window")} {width * 1000:0}×{height * 1000:0}";
                var basis = door ? JoinerySpec.DefaultDoor(name) : JoinerySpec.DefaultWindow(name);
                // The hole is the solids' own: no reveal, lintel, boards or threshold of Lapin's (ForeignElements.FillType).
                var spec = basis with
                {
                    Width = width, Height = height, SillHeight = door ? 0 : sill,
                    Details = new JoineryDetails(Reveal: new JoineryReveal("none")), Lintel = ForeignElements.NoLintel, Board = new JoineryBoard(0, 0), Threshold = 0,
                };
                fillTypes[key] = type = Openings.CreateType(ctx, spec);
            }
            return Openings.AddFill(ctx, wall, type, along, sill).Id;
        }
        catch (CommandException ex)
        {
            w.Notes.Add($"An opening at {along * 1000:0} mm was not made: {ex.Message}");
            return null;
        }
    }

    static void Originals(ICommandContext ctx, List<string> ids, string wallId, string mode)
    {
        if (mode == "keep") return;
        var groups = new HashSet<string>();
        foreach (var id in ids)
        {
            if (ctx.Model.Get(id) is not { } e) continue;
            if (mode == "delete")
            {
                if (Groups.Of(ctx.Model, id) is { } g) groups.Add(g.Id);
                ctx.Remove(id);
            }
            else
            {
                var attrs = e.CloneAttrs();
                attrs[Superseded.Key] = wallId;
                ctx.Replace(e.With(attrs: attrs));
            }
        }
        if (groups.Count > 0) Groups.DropEmpty(ctx, groups);
    }

    /// <summary>The solids a wall was made of shown again: {id} (the wall, or one of them).</summary>
    public static JsonObject Show(ICommandContext ctx, JsonObject a)
    {
        var id = (string?)a["id"] ?? throw new CommandException("Select the wall made of the solids.");
        var wall = ctx.Model.Get(id)?.Str(Superseded.Key) ?? id;
        var shown = 0;
        foreach (var e in ctx.Model.Entities.Where(e => e.Str(Superseded.Key) == wall).ToList())
        {
            var attrs = e.CloneAttrs();
            attrs.Remove(Superseded.Key);
            ctx.Replace(e.With(attrs: attrs));
            shown++;
        }
        if (shown == 0) throw new CommandException("This wall keeps no solids it was made of.");
        return new JsonObject { ["shown"] = shown };
    }

    /// <summary>How many solids a wall keeps hidden behind it.</summary>
    public static int HiddenBehind(IModelView m, string wallId) => m.Entities.Count(e => e.Str(Superseded.Key) == wallId);

    // ─── The answer ─────────────────────────────────────────────────────────

    static JsonObject Json(Found f) => new()
    {
        ["walls"] = new JsonArray([.. f.Walls.Select((w, i) => (JsonNode)new JsonObject
        {
            ["index"] = i,
            ["name"] = w.Name ?? WallTypes.DefaultName([.. w.Layers.Select((l, j) => new LayerSpec(l.Material, l.Thickness, j == w.Core))]),
            ["length"] = Math.Round(w.S1 - w.S0, 4),
            ["height"] = Math.Round(w.Z1 - w.Z0, 4),
            ["base"] = Math.Round(w.Z0, 4),
            ["thickness"] = Math.Round(w.Layers.Sum(l => l.Thickness), 4),
            ["core"] = w.Core,
            ["frame"] = w.Frame,
            ["frameSection"] = w.Layers.FirstOrDefault(l => l.StudWidth != null) is { } studs ? FrameSection(studs) : null,
            ["layers"] = new JsonArray([.. w.Layers.Select((l, j) => (JsonNode)new JsonObject
            {
                ["material"] = l.Material, ["named"] = l.Named, ["thickness"] = l.Thickness, ["kind"] = l.Kind, ["core"] = j == w.Core,
                ["members"] = new JsonArray([.. l.Members.Select(p => (JsonNode)p.M.E.Id)]),
                ["inside"] = l.Inside.Count, ["insideMaterial"] = l.InsideMaterial,
                ["studWidth"] = l.StudWidth is { } sw ? Math.Round(sw, 4) : null, ["spacing"] = l.Spacing is { } sp ? Math.Round(sp, 3) : null,
            })]),
            ["openings"] = new JsonArray([.. w.Openings.Select(o => (JsonNode)new JsonObject
            {
                ["along"] = Math.Round((o.S0 + o.S1) / 2 - w.S0, 4), ["sill"] = Math.Round(o.Sill - w.Z0, 4),
                ["width"] = Math.Round(o.S1 - o.S0, 4), ["height"] = Math.Round(o.Head - o.Sill, 4), ["kind"] = o.Kind, ["fill"] = o.Fill?.E.Id,
            })]),
            ["differences"] = new JsonArray([.. w.Differences.Select(d => (JsonNode)new JsonObject { ["id"] = d.Id, ["name"] = d.Name, ["what"] = d.What, ["mm"] = Math.Round(d.Mm, 1) })]),
            ["notes"] = new JsonArray([.. w.Notes.Select(n => (JsonNode)n)]),
            ["members"] = w.Members.Count,
        })]),
        ["left"] = new JsonArray([.. f.Left.Select(x => (JsonNode)new JsonObject { ["id"] = x.E.Id, ["name"] = x.E.Name, ["why"] = x.Why })]),
    };
}
