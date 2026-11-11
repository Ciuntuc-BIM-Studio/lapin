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
        foreach (var e in elements)
        {
            if (e.Class == IfcClass.Beam && PathCurve.Of(e).IsArc) { Skip(e, "a curved beam: SDNF's linear members are straight"); continue; }
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
        }

        // The plates of the steel connections of the members written.
        var plates = new List<SdnfPlate>();
        var plateNumbers = new HashSet<long>();
        foreach (var node in m.OfClass(SteelNodes.Class).OrderBy(n => n.Id, StringComparer.Ordinal))
        {
            if (!SteelNodes.MembersOf(node).Any(j => exported.Contains(j.Id)) || o.Hidden?.Contains(node.Id) == true || o.Kinds?.Contains(SteelNodes.Class) == false) continue;
            if (q.Geometry(node.Id) is not { } g) continue;
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
        var file = new SdnfFile(title, members, plates, grids);
        var report = new JsonObject
        {
            ["members"] = members.Count, ["plates"] = plates.Count, ["grids"] = grids.Count, ["skipped"] = skipped,
            ["byType"] = new JsonObject(members.GroupBy(x => x.Type).OrderBy(x => x.Key).Select(x => KeyValuePair.Create(x.Key, (JsonNode?)x.Count()))),
        };
        return new Result(file, comments, report);
    }

    static Entity? Place(IModelView m, Entity e) => m.RelatingOf(e.Id, IfcClass.RelDefinesByType);

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
