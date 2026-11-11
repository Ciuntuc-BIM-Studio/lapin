using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Platform.Analysis;
using Lapin.Structure;

namespace Lapin.Steel.Sdnf;

/// <summary>What an SDNF export takes: only these storeys' elements (null: all), only these kinds (the export dialog's: the classes, null: all), never these (what the 3D view hides).</summary>
public sealed record SdnfExportOptions(IReadOnlySet<string>? Storeys = null, IReadOnlySet<string>? Hidden = null, DateTime? Now = null, IReadOnlySet<string>? Kinds = null);

/// <summary>
/// The project as an SDNF 3.0 file (docs/sdnf.md, Export): the structure's columns and beams of any section and material and
/// the steel module's members as linear members (packet 10), the steel connections' plates (packet 20), the grids (packet 50).
/// <list type="bullet">
/// <item>Each member's line is its axis as Lapin builds it: a column's from its foot up, centred (cardinal point 5); a beam's
/// along its clear span between the columns' faces at its top (8), its own ends (the columns' centres) as the offsets to its
/// nodes; a member's through its insertion point (5, 8 or 2). The section's up is the orientation vector, the rotation 0;
/// a flipped section is mirrored about its Y axis. Steel ends (extensions, cuts, mitres, fits) are the cutbacks.</item>
/// <item>Ids: a member's number is the one it came with from an SDNF file, else a number from its GlobalId; a comment
/// (<c># Lapin member n GlobalId</c>) maps each number to its element, so an import of the file finds them again.</item>
/// <item>Curved beams as curved members (packet 60, decision 480); the plates an SDNF file brought, as it had them; the holes
/// (packet 22) of those plates, of the members (their parent the member's number) and those the connections' bolts and
/// anchors make in their plates (SDNF has no bolts).</item>
/// <item>No end releases (Lapin has none yet): every end is written fixed.</item>
/// </list>
/// </summary>
public static class SdnfExport
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>A number from a GlobalId (FNV-1a), at most eight digits as programs write them.</summary>
    public static long NumberOf(string key)
    {
        uint h = 2166136261;
        foreach (var c in key) { h ^= c; h *= 16777619; }
        return h % 99_999_999 + 1;
    }

    /// <summary>The number an element came with from an SDNF file.</summary>
    public static long? OwnNumber(Entity e) => e.Attrs[SdnfImport.Key] is JsonObject o && Json.Number(o["id"]) is { } n ? (long)n : null;

    public sealed record Result(SdnfFile File, IReadOnlyList<string> Comments, JsonObject Report)
    {
        public string Text => SdnfWriter.Write(File, Comments);
    }

    public static Result Build(IQueryContext q, SdnfExportOptions? options = null)
    {
        var o = options ?? new SdnfExportOptions();
        var m = q.Model;
        var skipped = new JsonArray();
        bool Wanted(Entity e) => (o.Hidden == null || !o.Hidden.Contains(e.Id)) && (o.Kinds == null || o.Kinds.Contains(e.Class)) && (o.Storeys == null || Spatial.StoreyOf(m, e.Id) is { } s && o.Storeys.Contains(s.Id));
        void Skip(Entity e, string why) => skipped.Add((JsonNode)new JsonObject { ["id"] = e.Id, ["name"] = e.Name, ["reason"] = why });

        // The members, their numbers: the file's they came with first, then one from each GlobalId, the next free one on a clash.
        var elements = m.OfClass(IfcClass.Column).Concat(m.OfClass(IfcClass.Beam)).Concat(m.OfClass(SteelModule.Member)).Where(Wanted).OrderBy(e => e.Id, StringComparer.Ordinal).ToList();
        var used = new HashSet<long>();
        var numbers = new Dictionary<string, long>();
        foreach (var e in elements) if (OwnNumber(e) is { } n && used.Add(n)) numbers[e.Id] = n;
        foreach (var e in elements.Where(e => !numbers.ContainsKey(e.Id)))
        {
            var n = NumberOf(e.Id);
            while (!used.Add(n)) n = n % 99_999_999 + 1;
            numbers[e.Id] = n;
        }

        var members = new List<SdnfMember>();
        var comments = new List<string> { "Written by Lapin. Lapin's elements by SDNF number: member, plate or grid, its number, its GlobalId." };
        var exported = new HashSet<string>();
        var holes = new List<SdnfHole>();
        var holeNumbers = new HashSet<long>();
        long HoleNumber(long? own, string key)
        {
            if (own is { } x && x > 0 && holeNumbers.Add(x)) return x;
            var n = NumberOf(key);
            while (!holeNumbers.Add(n)) n = n % 99_999_999 + 1;
            return n;
        }
        foreach (var e in elements)
        {
            if (e.Class == IfcClass.Beam && PathCurve.Of(e).IsArc)
            {
                if (Curved(m, e, numbers[e.Id]) is not { } cm) { Skip(e, "a curved beam that lies entirely in columns"); continue; }
                members.Add(cm);
                comments.Add($"Lapin member {numbers[e.Id]} {e.Id}");
                exported.Add(e.Id);
                continue;
            }
            if (MemberAxes.Of(m, e) is not { } ax)
            {
                Skip(e, e.Class == IfcClass.Beam ? "a beam that lies entirely in columns" : "an element with no length");
                continue;
            }
            var (v, w) = ax.Frame;
            var cardinal = ax.Insertion switch { "top" => 8, "bottom" => 2, _ => 5 };
            var type = Place(m, e);
            var section = ax.Section;
            var material = (string?)type?.Attrs["material"] is { } mid ? m.Get(mid) : null;
            var grade = material == null ? "" : StructuralMaterials.Of(m, material).Grade ?? material.Name;
            // The cutbacks: where the steel ends along the axis (its ends' rules), off the axis's ends.
            var (c0, c1) = (0.0, 0.0);
            if (e.Attrs[SteelEnd.Key] is JsonObject)
            {
                var (s, t) = SteelEnd.Of(e);
                var built = SteelBody.Build(m, ax, s, t, null);
                (c0, c1) = (built.S0, ax.Length - built.S1);
            }
            // A beam's nodes: its own ends, the columns' centres, where the clear span stops at their faces.
            var (o0, o1) = (Vec3.Zero, Vec3.Zero);
            if (e.Class == IfcClass.Beam)
            {
                var c = PathCurve.Of(e);
                o0 = c.Start.At(ax.Start.Z) - ax.Start;
                o1 = c.End.At(ax.End.Z) - ax.End;
            }
            members.Add(new SdnfMember(numbers[e.Id], cardinal, TypeName(e), "", SdnfProfiles.NameOf(section), grade, 0, false, ax.Flipped,
                Clean(w), ax.Start, ax.End, c0, c1) { StartOffset = o0, EndOffset = o1 });
            comments.Add($"Lapin member {numbers[e.Id]} {e.Id}");
            exported.Add(e.Id);
            // Its holes, their parent the member's number (SDNF's parent is a plate's: a program that finds no plate of it leaves them out).
            var origin = MemberHoles.Origin(m, e, ax);
            var k = 0;
            foreach (var h in MemberHoles.Of(e))
            {
                var top = h.Top.Select(p => MemberHoles.World(ax, origin, p)).ToList();
                var bottom = h.Bottom.Select(p => MemberHoles.World(ax, origin, p)).ToList();
                var number = HoleNumber(h.Number, $"hole:{e.Id}:{k++}");
                holes.Add(h.Round
                    ? new SdnfHole(number, numbers[e.Id], 2, (bottom[0] - top[0]).Length, [(top[0], h.Radius)], [(bottom[0], h.Radius)])
                    : new SdnfHole(number, numbers[e.Id], 0, (bottom[0] - top[0]).Length, Closed(top), Closed(bottom)));
            }
        }

        // The plates of the steel connections of the members written.
        var plates = new List<SdnfPlate>();
        var plateNumbers = new HashSet<long>();
        foreach (var node in m.OfClass(SteelNodes.Class).OrderBy(n => n.Id, StringComparer.Ordinal))
        {
            if (!SteelNodes.MembersOf(node).Any(j => exported.Contains(j.Id)) || o.Hidden?.Contains(node.Id) == true || o.Kinds?.Contains(SteelNodes.Class) == false) continue;
            if (q.Geometry(node.Id) is not { } g) continue;
            var bolts = SteelNodes.Build(m, node).BoltLines;
            foreach (var part in g.Parts)
            {
                if (part.Key is not { } key || !(key.StartsWith("steel-plate:", StringComparison.Ordinal) || key.StartsWith("steel-anchorplate:", StringComparison.Ordinal))) continue;
                if (PlateOf(part.Solid) is not { } loops) continue;
                var tag = $"node:{node.Id}:{key}";
                var n = NumberOf(tag);
                while (!plateNumbers.Add(n) || used.Contains(n)) n = n % 99_999_999 + 1;
                var grade = part.Properties?.FirstOrDefault(p => p.Name == "Grade").Value ?? "S235";
                plates.Add(new SdnfPlate(n, 0, "Plate", part.Name, grade, loops.Thickness, 0, Closed(loops.Top), Closed(loops.Bottom)));
                comments.Add($"Lapin plate {n} {tag}");
                // The holes its bolts and anchors go through: round, as wide as the connection's rule (2 or 3 mm over the bolt).
                var k = 0;
                foreach (var (top, bottom, d) in BoltHoles(loops.Top, loops.Bottom, bolts))
                    holes.Add(new SdnfHole(HoleNumber(null, $"hole:{tag}:{k++}"), n, 2, loops.Thickness, [(top, NodeSpec.Hole(d) / 2)], [(bottom, NodeSpec.Hole(d) / 2)]));
            }
        }

        // The plates an SDNF file brought (kept as the file had them), and their holes.
        foreach (var e in m.OfClass("IfcPlate").Where(Wanted).OrderBy(e => e.Id, StringComparer.Ordinal))
        {
            if (e.Attrs[SdnfImport.Key] is not JsonObject so || so["def"] is not JsonObject def) continue;
            var (top, bottom) = (SdnfImport.LoopOf(def["top"]), SdnfImport.LoopOf(def["bottom"]));
            if (top.Count == 0) continue;
            var n = Json.Number(so["plate"]) is { } own && own > 0 ? (long)own : NumberOf(e.Id);
            while (!plateNumbers.Add(n) || used.Contains(n)) n = n % 99_999_999 + 1;
            plates.Add(new SdnfPlate(n, (int)(Json.Number(def["connect"]) ?? 0), (string?)def["type"] ?? "Plate", e.Name == "Plate" ? "" : e.Name, (string?)def["grade"] ?? "",
                Json.Number(def["thickness"]) ?? 0, (int)(Json.Number(def["periphery"]) ?? 0), top, bottom));
            comments.Add($"Lapin plate {n} {e.Id}");
            var k = 0;
            foreach (var h in (so["holes"] as JsonArray)?.OfType<JsonObject>() ?? [])
            {
                var (ht, hb) = (SdnfImport.LoopOf(h["top"]), SdnfImport.LoopOf(h["bottom"]));
                if (ht.Count == 0) continue;
                holes.Add(new SdnfHole(HoleNumber(Json.Number(h["id"]) is { } hid ? (long)hid : null, $"hole:{e.Id}:{k++}"), n, (int)(Json.Number(h["periphery"]) ?? 0), Json.Number(h["thickness"]) ?? 0, ht, hb));
            }
        }

        // The grids, each IfcGrid a set, its axes at the level ±0.00.
        var grids = new List<SdnfGridSet>();
        foreach (var grid in m.OfClass(IfcClass.Grid).Where(g => o.Hidden?.Contains(g.Id) != true).OrderBy(g => m.Order(g.Id)))
        {
            var lines = (grid.Attrs["axes"] as JsonArray)?.OfType<JsonObject>().Select(x => ((string?)x["tag"] ?? "?", P(x["a"]), P(x["b"]))).ToList() ?? [];
            if (lines.Count == 0) continue;
            grids.Add(new SdnfGridSet(grids.Count + 1, grid.Name, lines));
            comments.Add($"Lapin grid {grids.Count} {grid.Id}");
        }

        var now = o.Now ?? DateTime.Now;
        var title = new SdnfTitle("SDNF Version 3.0", "", "", Spatial.Building(m)?.Name ?? "", Spatial.Project(m)?.Name ?? q.Title ?? "",
            now.ToString("MM/dd/yy", Inv), now.ToString("HH:mm", Inv), 0, "Lapin", "");
        var file = new SdnfFile(title, members, plates, grids) { Holes = holes };
        var report = new JsonObject
        {
            ["members"] = members.Count, ["curved"] = members.Count(x => x.Arc != null), ["plates"] = plates.Count, ["holes"] = holes.Count, ["grids"] = grids.Count, ["skipped"] = skipped,
            ["byType"] = new JsonObject(members.GroupBy(x => x.Type).OrderBy(x => x.Key).Select(x => KeyValuePair.Create(x.Key, (JsonNode?)x.Count()))),
        };
        return new Result(file, comments, report);
    }

    static Entity? Place(IModelView m, Entity e) => m.RelatingOf(e.Id, IfcClass.RelDefinesByType);

    static string GradeOf(IModelView m, Entity e)
    {
        var material = (string?)Place(m, e)?.Attrs["material"] is { } mid ? m.Get(mid) : null;
        return material == null ? "" : StructuralMaterials.Of(m, material).Grade ?? material.Name;
    }

    /// <summary>
    /// A curved beam as a curved member (packet 60): its clear span along the arc at its top (cardinal point 8), its own ends
    /// (the columns' centres) as the offsets to its nodes; the arc's centre at that height, the normal about which it turns
    /// counter-clockwise from start to end (up, or down), the sweep, the start angle from x; the section's up as the
    /// orientation, mirrored when flipped.
    /// </summary>
    static SdnfMember? Curved(IModelView m, Entity e, long number)
    {
        var span = Beams.SpanOf(m, e);
        if (span.Empty) return null;
        var c = span.Curve;
        var top = Beams.Top(m, e);
        var (a, b) = (span.Start.At(top), span.End.At(top));
        var normal = c.Rise < 0 ? Vec3.UnitZ : -Vec3.UnitZ;
        var centre = c.Centre.At(top);
        var sweep = (span.S1 - span.S0) / c.Radius * 180 / Math.PI;
        return new SdnfMember(number, 8, "Beam", "", SdnfProfiles.NameOf(Beams.Spec(m, e).Section), GradeOf(m, e), 0, false, e.Bool("flipped"), Vec3.UnitZ, a, b)
        {
            StartOffset = c.Start.At(top) - a, EndOffset = c.End.At(top) - b,
            Arc = new SdnfArc(centre, SdnfArc.AngleOf(centre, normal, a), sweep, normal),
        };
    }

    /// <summary>
    /// The holes the bolts and anchors make in a plate: each line that crosses both its faces within its grip, square enough
    /// to the plate, inside its outline; the hole's centre on each face and the fastener's diameter.
    /// </summary>
    public static IEnumerable<(Vec3 Top, Vec3 Bottom, double D)> BoltHoles(IReadOnlyList<Vec3> top, IReadOnlyList<Vec3> bottom, IReadOnlyList<(Vec3 At, Vec3 Dir, double D, double Grip)> bolts)
    {
        var through = top[0] - bottom[0];
        if (through.Length < 1e-9) yield break;
        var n = through.Normalized();
        var (e1, e2) = VecMath.PlaneBasis(n);
        Vec2 In2(Vec3 p) => new(Vec3.Dot(p - top[0], e1), Vec3.Dot(p - top[0], e2));
        var face = top.Select(In2).ToList();
        foreach (var (at, dir, d, grip) in bolts)
        {
            var across = Vec3.Dot(n, dir);
            if (Math.Abs(across) < 0.5) continue;
            var (st, sb) = (Vec3.Dot(n, top[0] - at) / across, Vec3.Dot(n, bottom[0] - at) / across);
            if (Math.Min(st, sb) < -1e-4 || Math.Max(st, sb) > grip + 1e-4) continue;
            var (pt, pb) = (at + dir * st, at + dir * sb);
            if (!Polygon2.Contains(face, In2(pt))) continue;
            yield return (pt, pb, d);
        }
    }

    /// <summary>The SDNF type: Column, Beam, or a member's predefined type (Brace, Purlin, Rafter…).</summary>
    static string TypeName(Entity e) => e.Class switch
    {
        IfcClass.Column => "Column",
        IfcClass.Beam => "Beam",
        _ => e.PredefinedType is { Length: > 0 } p && p != "USERDEFINED" && p != "NOTDEFINED" ? char.ToUpperInvariant(p[0]) + p[1..].ToLowerInvariant() : "Member",
    };

    static Vec3 Clean(Vec3 v) => new(Snap(v.X), Snap(v.Y), Snap(v.Z));
    static double Snap(double x) => Math.Abs(x) < 1e-12 ? 0 : x;

    static Vec3 P(JsonNode? n) => n is JsonArray a && a.Count >= 2 ? new Vec3(Json.Number(a[0]) ?? 0, Json.Number(a[1]) ?? 0, 0) : default;

    static List<(Vec3, double)> Closed(IReadOnlyList<Vec3> loop) => [.. loop.Select(p => (p, 1.0)), (loop[0], 0.0)];

    /// <summary>A plate's faces: its largest face (the top), the opposite one through its thickness (the bottom), vertex for vertex.</summary>
    public static (IReadOnlyList<Vec3> Top, IReadOnlyList<Vec3> Bottom, double Thickness)? PlateOf(Solid s)
    {
        if (s.Faces.Length < 4) return null;
        double Area(Face f) => VecMath.NewellNormal(f.Outer.Select(i => s.Vertices[i]).ToList()).Length;
        var top = s.Faces.MaxBy(Area)!;
        if (top.Holes.Length > 0) return null;
        var n = top.Normal.Normalized();
        var ring = top.Outer.Select(i => s.Vertices[i]).ToList();
        var t = s.Vertices.Max(p => Vec3.Dot(ring[0] - p, n));
        if (!(t > 1e-6)) return null;
        // A prism: every vertex is a top vertex or one moved down the thickness.
        var bottom = ring.Select(p => p - n * t).ToList();
        if (s.Vertices.Any(p => ring.All(r => (r - p).Length > 1e-6) && bottom.All(b => (b - p).Length > 1e-6))) return null;
        return (ring, bottom, t);
    }
}
