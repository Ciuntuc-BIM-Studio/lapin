using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Platform.Analysis;
using Lapin.Structure;

namespace Lapin.Steel.Sdnf;

/// <summary>
/// An SDNF file brought into the project as native elements, in one undo step (docs/sdnf.md, Import):
/// <list type="bullet">
/// <item>A linear member becomes a column when the file calls it one and it stands upright, a beam when it is called one and
/// lies level with its section upright, else a steel member (its type a member's predefined type: brace, purlin, rafter…).
/// Its section from its name (<see cref="SdnfProfiles"/>), its grade a material; the cardinal point, orientation vector,
/// rotation and mirroring place it as the file has it.</item>
/// <item>Ids: Lapin's own file names each element's GlobalId in its comments; a member numbered as one the project already
/// holds (one exported by Lapin, or imported before from the same source) is that element; any other gets a GlobalId
/// derived from its source and number. Importing a file again therefore updates what it brought before, and an element
/// whose class no longer fits (a column now sloping) is made again under its id.</item>
/// <item>Plates (packet 20) come in as plates with their bodies, except those of Lapin's own steel connections still in the
/// project (they are built from the connection). Grids (packet 50) as grids.</item>
/// <item>Left out, and said: curved members, holes, loads, connection details; end releases (Lapin has none yet).</item>
/// </list>
/// </summary>
public static class SdnfImport
{
    /// <summary>What an element keeps of the SDNF member it came from: {id, source}.</summary>
    public const string Key = "sdnf";

    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static string DerivedId(string seed) => GlobalId.FromGuid(new Guid(Md5.HashData(Encoding.UTF8.GetBytes("sdnf\n" + seed))));

    static string Title(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..].ToLowerInvariant();

    public static JsonObject Run(ICommandContext ctx, SdnfFile file, string name)
    {
        var m = ctx.Model;
        var notes = new List<string>(file.Notes);
        var source = file.Title.Source;
        var lapinFile = file.FromLapin;
        int made = 0, updated = 0, remade = 0, plates = 0, grids = 0, skipped = 0;
        var byClass = new Dictionary<string, int>();
        var unknown = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var released = 0;

        Spatial.EnsureBuilding(ctx);
        Spatial.EnsureStorey(ctx);

        // The project's members by the SDNF numbers they answer to: their own (an earlier import of this source), else the one from their GlobalId.
        var own = new Dictionary<long, string>();
        var hashed = new Dictionary<long, string>();
        foreach (var e in m.OfClass(IfcClass.Column).Concat(m.OfClass(IfcClass.Beam)).Concat(m.OfClass(SteelModule.Member)).OrderBy(e => e.Id, StringComparer.Ordinal))
        {
            if (e.Attrs[Key] is JsonObject so && Json.Number(so["id"]) is { } n && (string?)so["source"] == source) own.TryAdd((long)n, e.Id);
            hashed.TryAdd(SdnfExport.NumberOf(e.Id), e.Id);
        }

        var seen = new HashSet<string>();
        foreach (var mb in file.Members)
        {
            var id = file.LapinIds.TryGetValue($"member {mb.Id}", out var g) && GlobalId.IsValid(g) ? g
                : own.TryGetValue(mb.Id, out var o) ? o
                : hashed.TryGetValue(mb.Id, out var h) && lapinFile ? h
                : DerivedId($"{source}\nmember\n{mb.Id}");
            if (!seen.Add(id)) { skipped++; notes.Add($"Member {mb.Id} is in the file twice: the second was left out."); continue; }
            if (mb.Releases.Any(r => r != 0)) released++;
            var r = Member(ctx, mb, id, source, lapinFile, unknown, notes);
            if (r == null) { skipped++; continue; }
            var (cls, how) = r.Value;
            byClass[cls] = byClass.GetValueOrDefault(cls) + 1;
            if (how == "made") made++; else if (how == "remade") remade++; else updated++;
        }

        foreach (var p in file.Plates)
        {
            var tag = file.LapinIds.TryGetValue($"plate {p.Id}", out var t) ? t : null;
            // A plate of one of Lapin's own connections still in the project: the connection builds it.
            if (tag != null && tag.Split(':') is ["node", var nid, ..] && m.Get(nid) is { Class: SteelNodes.Class }) continue;
            var id = DerivedId(tag ?? $"{source}\nplate\n{p.Id}");
            if (Plate(ctx, p, id, source)) plates++; else skipped++;
        }

        foreach (var set in file.Grids)
        {
            var id = file.LapinIds.TryGetValue($"grid {set.Number}", out var gid) && GlobalId.IsValid(gid) ? gid : DerivedId($"{source}\ngrid\n{set.Number}");
            var axes = new JsonArray([.. set.Lines.Select(l => (JsonNode)new JsonObject
            {
                ["tag"] = l.Label, ["a"] = new JsonArray(Math.Round(l.Start.X, 9), Math.Round(l.Start.Y, 9)), ["b"] = new JsonArray(Math.Round(l.End.X, 9), Math.Round(l.End.Y, 9)),
            })]);
            if (m.Get(id) is { Class: IfcClass.Grid } grid)
            {
                var a = grid.CloneAttrs();
                a["axes"] = axes;
                ctx.Replace(grid.With(attrs: a));
            }
            else
            {
                if (m.Get(id) != null) id = ctx.NewId();
                ctx.Add(new Entity(id, IfcClass.Grid, set.Description is { Length: > 0 } d ? d : $"Grid {set.Number}", new JsonObject { ["axes"] = axes }));
            }
            grids++;
        }

        foreach (var u in unknown)
            notes.Add($"The profile „{u}” is in none of the profile libraries: its members are drawn as a {SdnfProfiles.PlaceholderSize * 1000:0} mm square, named after it, until its type is given a profile.");
        if (released > 0) notes.Add($"{released} member(s) have released ends in the file: Lapin keeps no end releases yet, they came in fixed.");
        return new JsonObject
        {
            ["name"] = name, ["version"] = file.Title.Version, ["source"] = file.Title.IssueCode,
            ["made"] = made, ["updated"] = updated, ["remade"] = remade, ["plates"] = plates, ["grids"] = grids, ["skipped"] = skipped,
            ["byClass"] = new JsonObject(byClass.OrderBy(k => k.Key).Select(k => KeyValuePair.Create(k.Key, (JsonNode?)k.Value))),
            ["unknownProfiles"] = new JsonArray([.. unknown.Select(u => (JsonNode)u)]),
            ["notes"] = new JsonArray([.. notes.Select(n => (JsonNode)n)]),
        };
    }

    /// <summary>The cardinal point's place on the section's box, in its x and y as halves of its width and depth (1–9 bottom to top, left to right; 10, 11 the centre).</summary>
    public static (double X, double Y) CardinalOffset(int cardinal, Section s)
    {
        var (w, d) = (s.Width / 2, s.Depth / 2);
        if (cardinal is < 1 or > 9) return (0, 0);
        var col = (cardinal - 1) % 3;
        var row = (cardinal - 1) / 3;
        return ((col - 1) * w, (row - 1) * d);
    }

    /// <summary>The section's frame from the file: its up (the orientation vector turned by the rotation about the axis), its x across; mirrored about X it is turned half round and mirrored about Y.</summary>
    public static (Vec3 V, Vec3 W, bool Flipped) Frame(Vec3 u, Vec3 orientation, double rotationDeg, bool mirrorX, bool mirrorY)
    {
        var o = orientation - u * Vec3.Dot(orientation, u);
        if (o.Length < 1e-9)
        {
            // No orientation, or one along the axis: a level member's up is up, an upright one's is y.
            var up = Math.Abs(u.Z) > 1 - 1e-9 ? new Vec3(0, 1, 0) : Vec3.UnitZ;
            o = up - u * Vec3.Dot(up, u);
        }
        var w0 = o.Normalized();
        var r = rotationDeg * Math.PI / 180;
        var w = w0 * Math.Cos(r) + Vec3.Cross(u, w0) * Math.Sin(r);
        var flipped = mirrorY;
        if (mirrorX) { w = -w; flipped = !flipped; }
        w = w.Normalized();
        return (Vec3.Cross(w, u).Normalized(), w, flipped);
    }

    static bool Handed(Section s) => s.Kind is "L" or "U" or "C" or "Z" or "SIGMA" or "outline";

    static double R(double v) => Math.Round(v, 9) is var x && x == 0 ? 0 : x;
    static JsonArray J2(Vec2 p) => new(R(p.X), R(p.Y));
    static JsonArray J3(Vec3 p) => new(R(p.X), R(p.Y), R(p.Z));

    /// <summary>The storey an element at <paramref name="z"/> stands on: the highest whose level is at or below it (by <paramref name="above"/>), else the lowest.</summary>
    static Entity StoreyAt(IModelView m, double z, double above = 1e-4)
    {
        var storeys = Spatial.Storeys(m).ToList();
        return storeys.LastOrDefault(s => s.Num("elevation") <= z + above) ?? storeys[0];
    }

    /// <summary>A material for a grade: the project's of that name (or cut to SDNF's 24 characters), else one whose strength class is that grade, else a new one of that name.</summary>
    static string? MaterialFor(ICommandContext ctx, string grade, Section s)
    {
        var g = grade.Trim();
        if (g.Length == 0) return null;
        var mats = ctx.Model.OfClass(IfcClass.Material).ToList();
        if (mats.FirstOrDefault(x => string.Equals(x.Name, g, StringComparison.OrdinalIgnoreCase)) is { } same) return same.Name;
        if (g.Length >= 24 && mats.FirstOrDefault(x => x.Name.StartsWith(g, StringComparison.OrdinalIgnoreCase)) is { } cut) return cut.Name;
        if (mats.FirstOrDefault(x => string.Equals(StructuralMaterials.Of(ctx.Model, x).Grade, g, StringComparison.OrdinalIgnoreCase)) is { } graded) return graded.Name;
        return g;
    }

    /// <summary>The project's type of <paramref name="typeClass"/> with this section and material, else a new one.</summary>
    static Entity TypeFor(ICommandContext ctx, string typeClass, Section section, string? material)
    {
        var materialId = material == null ? null : Materials.Find(ctx.Model, material)?.Id;
        foreach (var t in ctx.Model.OfClass(typeClass))
            if (Section.From(t.Attrs["section"]) is { } ts && SameProfile(ts, section) && (material == null || (string?)t.Attrs["material"] == materialId) && (typeClass != IfcClass.ColumnType || t.Attrs["height"] == null))
                return t;
        var mat = material ?? (section.IsProfile || SdnfProfiles.IsPlaceholder(section) ? "Steel" : "Reinforced concrete");
        return typeClass switch
        {
            IfcClass.ColumnType => Columns.CreateType(ctx, null, section, null, mat),
            IfcClass.BeamType => Beams.CreateType(ctx, null, section, mat),
            _ => SteelModule.CreateType(ctx, null, section, mat),
        };
    }

    /// <summary>Whether two sections are one profile: the same, or the same kind under the same SDNF name (a library profile and its project copy).</summary>
    static bool SameProfile(Section a, Section b) => a.Equals(b) || a.Kind == b.Kind && NameKey(SdnfProfiles.NameOf(a)) == NameKey(SdnfProfiles.NameOf(b));

    static string NameKey(string s) => new string(s.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();

    static (string Class, string How)? Member(ICommandContext ctx, SdnfMember mb, string id, string source, bool lapinFile, ISet<string> unknown, List<string> notes)
    {
        var m = ctx.Model;
        var length = (mb.End - mb.Start).Length;
        if (length < 1e-3) { notes.Add($"Member {mb.Id} is shorter than a millimetre: left out."); return null; }
        var section = SdnfProfiles.Resolve(m, mb.Size);
        if (section == null)
        {
            section = SdnfProfiles.Placeholder(mb.Size.Length > 0 ? mb.Size : "?");
            unknown.Add(section.Name!);
        }
        var u = (mb.End - mb.Start) * (1 / length);
        var (v, w, flipped) = Frame(u, mb.Orientation, mb.Rotation, mb.MirrorX, mb.MirrorY);
        if (!Handed(section)) flipped = false;
        // The line through the section's box centre.
        var (cx, cy) = CardinalOffset(mb.Cardinal, section);
        var shift = v * cx + w * cy;
        var (a, b) = (mb.Start - shift, mb.End - shift);

        var existing = m.Get(id);
        var wanted = existing?.Class ?? mb.Type.ToLowerInvariant() switch
        {
            var t when t.Contains("column") || t.Contains("col") && t.Length <= 4 => IfcClass.Column,
            var t when t.Contains("beam") || t.Contains("girder") || t.Contains("joist") => IfcClass.Beam,
            _ => SteelModule.Member,
        };
        var upright = Math.Abs(u.Z) > 1 - 1e-6;
        var level = Math.Abs(u.Z) < 1e-6 && Vec3.Dot(w, Vec3.UnitZ) > 1 - 1e-6;
        var cls = wanted switch
        {
            IfcClass.Column when upright && (u.Z > 0 || !Handed(section)) && !flipped => IfcClass.Column,
            IfcClass.Beam when level => IfcClass.Beam,
            _ => SteelModule.Member,
        };
        if (existing != null && existing.Class != cls) notes.Add($"„{existing.Name}” no longer fits its class as the file has it: made again as a {(cls == SteelModule.Member ? "member" : cls[3..].ToLowerInvariant())}, its id kept.");

        string typeClass = cls switch { IfcClass.Column => IfcClass.ColumnType, IfcClass.Beam => IfcClass.BeamType, _ => SteelModule.MemberType };
        // The type it has when its section and material are what the file says, else one that is.
        var material = MaterialFor(ctx, mb.Grade, section);
        Entity? type = existing?.Class == cls && m.RelatingOf(id, IfcClass.RelDefinesByType) is { } had && Section.From(had.Attrs["section"]) is { } hs && SameProfile(hs, section)
            && (material == null || (string?)had.Attrs["material"] == Materials.Find(m, material)?.Id) ? had : null;
        type ??= TypeFor(ctx, typeClass, section, material);

        JsonObject attrs;
        Entity storey;
        string predefined;
        var keep = existing?.Class == cls ? existing.CloneAttrs() : new JsonObject();
        // An element brought in before stays on its storey, its levels worked out from it.
        var kept = existing?.Class == cls ? Spatial.StoreyOf(m, id) : null;
        switch (cls)
        {
            case IfcClass.Column:
            {
                var (bottom, top) = a.Z <= b.Z ? (a, b) : (b, a);
                storey = kept ?? StoreyAt(m, bottom.Z);
                // Lapin's column frame: x along (cos r, sin r), y a quarter round, up the column.
                var vx = Vec3.Cross(w, Vec3.UnitZ);
                var rotation = Math.Atan2(vx.Y, vx.X) * 180 / Math.PI;
                attrs = keep;
                attrs["at"] = J2(bottom.Xy);
                attrs["rotation"] = R(Math.Round(rotation, 9));
                var baseOffset = bottom.Z - storey.Num("elevation");
                if (Math.Abs(baseOffset) > 1e-9) attrs["baseOffset"] = R(baseOffset); else attrs.Remove("baseOffset");
                var height = top.Z - bottom.Z;
                if (attrs["height"] != null || Math.Abs(height - (type.NumOrNull("height") ?? storey.Num("height", 3))) > 1e-9 || Math.Abs(baseOffset) > 1e-9) attrs["height"] = R(height);
                predefined = "COLUMN";
                break;
            }
            case IfcClass.Beam:
            {
                // The beam's axis at its top; Lapin's own beams back on their nodes (the columns' centres) when the file says them.
                var (s, e) = (a + w * (section.Depth / 2), b + w * (section.Depth / 2));
                if (lapinFile && IsAlong(mb.StartOffset, u) && IsAlong(mb.EndOffset, u)) (s, e) = (s + mb.StartOffset, e + mb.EndOffset);
                var top = s.Z;
                storey = kept ?? Spatial.Storeys(m).LastOrDefault(x => x.Num("elevation") < top - 1e-3) ?? Spatial.Storeys(m).First();
                attrs = keep;
                attrs["start"] = J2(s.Xy);
                attrs["end"] = J2(e.Xy);
                attrs["topOffset"] = R(top - storey.Num("elevation") - storey.Num("height", 3));
                attrs.Remove(PathCurve.Key);
                if (flipped) attrs["flipped"] = true; else attrs.Remove("flipped");
                predefined = "BEAM";
                break;
            }
            default:
            {
                // Through the section's top or bottom when the file's line runs there, else its centre.
                var insertion = mb.Cardinal == 8 ? "top" : mb.Cardinal == 2 ? "bottom" : "center";
                var lift = insertion == "top" ? w * (section.Depth / 2) : insertion == "bottom" ? w * (-section.Depth / 2) : Vec3.Zero;
                var (s, e) = (a + lift, b + lift);
                storey = kept ?? StoreyAt(m, Math.Min(s.Z, e.Z));
                var z = storey.Num("elevation");
                // Lapin's member frame: unturned, x level to the axis's left (x along x when upright); turned by its rotation.
                var v0 = upright ? new Vec3(1, 0, 0) : new Vec3(-u.Y, u.X, 0).Normalized();
                var w0 = Vec3.Cross(u, v0);
                var rotation = Math.Atan2(Vec3.Dot(v, w0), Vec3.Dot(v, v0)) * 180 / Math.PI;
                attrs = keep;
                attrs["exportAs"] = "IfcMember";
                attrs["start"] = J3(s with { Z = s.Z - z });
                attrs["end"] = J3(e with { Z = e.Z - z });
                attrs["rotation"] = R(Math.Round(rotation, 9));
                attrs["insertion"] = insertion;
                if (flipped) attrs["flipped"] = true; else attrs.Remove("flipped");
                var t = mb.Type.Trim().ToUpperInvariant();
                predefined = existing?.Class == cls && existing.PredefinedType is { } ep && !SteelModule.Predefined.Any(p => t.Contains(p) && p != "MEMBER") ? ep
                    : SteelModule.Predefined.FirstOrDefault(p => p != "MEMBER" && t.Contains(p)) ?? "MEMBER";
                break;
            }
        }
        // A new steel element's cutbacks as its ends' extensions (an existing one keeps its own ends).
        if (existing?.Class != cls && section.IsProfile && (Math.Abs(mb.StartCutback) > 1e-6 || Math.Abs(mb.EndCutback) > 1e-6))
            attrs[SteelEnd.Key] = SteelEnd.Json2(new SteelEnd(Extension: R(-mb.StartCutback)), new SteelEnd(Extension: R(-mb.EndCutback)));
        attrs[Key] = new JsonObject { ["id"] = mb.Id, ["source"] = source };

        string how;
        if (existing != null && existing.Class == cls)
        {
            ctx.Replace(existing.With(attrs: attrs, predefinedType: predefined));
            how = "updated";
        }
        else
        {
            if (existing != null) ctx.Remove(id);
            var name = mb.PieceMark.Length > 0 ? mb.PieceMark : existing?.Name ?? type.Name;
            ctx.Add(new Entity(id, cls, name, attrs, predefined));
            how = existing != null ? "remade" : "made";
        }
        if (Spatial.StoreyOf(ctx.Model, id)?.Id != storey.Id) Spatial.Contain(ctx, storey.Id, id);
        if (ctx.Model.RelatingOf(id, IfcClass.RelDefinesByType)?.Id != type.Id) ctx.Relate(IfcClass.RelDefinesByType, type.Id, id);
        return (cls, how);
    }

    static bool IsAlong(Vec3 offset, Vec3 u) => offset.Length < 1e-9 || Vec3.Cross(offset, u).Length < 1e-6 * Math.Max(1, offset.Length);

    /// <summary>A plate as an element of its own, its body the plate's (a prism between its two faces; a circle by the chord rule).</summary>
    static bool Plate(ICommandContext ctx, SdnfPlate p, string id, string source)
    {
        static List<Vec3> Open(IReadOnlyList<(Vec3 P, double Flag)> loop)
        {
            var pts = loop.Select(x => x.P).ToList();
            if (pts.Count > 1 && (pts[^1] - pts[0]).Length < 1e-9) pts.RemoveAt(pts.Count - 1);
            return pts;
        }
        Solid? solid = null;
        if (p.Periphery == 2 && p.Top.Count > 0)
        {
            var c0 = p.Top[0].P;
            var c1 = p.Bottom.Count > 0 ? p.Bottom[0].P : c0;
            var r = p.Top[0].Flag;
            var axis = c1 - c0;
            if (axis.Length < 1e-9) axis = Vec3.UnitZ * p.Thickness;
            var (e1, e2) = VecMath.PlaneBasis(axis.Normalized());
            var n = Math.Max(8, Chords.Circle(r));
            var ring = Enumerable.Range(0, n).Select(i => c0 + e1 * (r * Math.Cos(2 * Math.PI * i / n)) + e2 * (r * Math.Sin(2 * Math.PI * i / n))).ToList();
            if (r > 0) solid = Prism.Extrude(ring, null, axis, "plate");
        }
        else
        {
            var top = Open(p.Top);
            var bottom = Open(p.Bottom);
            if (top.Count >= 3)
            {
                Vec3 sweep;
                if (bottom.Count == top.Count) sweep = bottom[0] - top[0];
                else
                {
                    // One loop (version 2): the plate through its thickness from it, as its connect point says.
                    var nn = VecMath.NewellNormal(top).Normalized();
                    var start = p.ConnectPoint switch { 1 => 0.0, -1 => -p.Thickness, _ => -p.Thickness / 2 };
                    top = top.Select(x => x + nn * start).ToList();
                    sweep = nn * p.Thickness;
                }
                if (sweep.Length > 1e-9) solid = Prism.Extrude(top, null, sweep, "plate");
            }
        }
        if (solid == null) return false;
        var mesh = Tessellator.Tessellate(solid);
        var verts = new List<Vec3>(mesh.Positions.Length / 3);
        for (var i = 0; i + 2 < mesh.Positions.Length; i += 3) verts.Add(new Vec3(mesh.Positions[i], mesh.Positions[i + 1], mesh.Positions[i + 2]));
        var tris = new List<(int, int, int)>(mesh.Indices.Length / 3);
        for (var i = 0; i + 2 < mesh.Indices.Length; i += 3) tris.Add((mesh.Indices[i], mesh.Indices[i + 1], mesh.Indices[i + 2]));
        var material = MaterialFor(ctx, p.Grade, Section.Rectangle(1, 1)) ?? "Steel";
        var mat = Materials.Ensure(ctx, material);
        var name = p.PieceMark.Length > 0 ? p.PieceMark : "Plate";
        var attrs = new JsonObject
        {
            [StoredBodies.Key] = StoredBodies.Write([(name, Materials.ColorOf(ctx.Model, mat.Id), verts, tris)]),
            [Key] = new JsonObject { ["plate"] = p.Id, ["source"] = source },
            ["properties"] = new JsonObject
            {
                ["SDNF"] = new JsonObject
                {
                    ["Plate"] = p.Id.ToString(Inv), ["Type"] = p.Type, ["Grade"] = p.Grade,
                    ["Thickness"] = Math.Round(p.Thickness * 1000, 3).ToString("0.###", Inv) + " mm",
                },
            },
        };
        if (ctx.Model.Get(id) is { } had)
        {
            if (had.Class == "IfcPlate") ctx.Replace(had.With(name: name, attrs: attrs));
            else return false;
        }
        else ctx.Add(new Entity(id, "IfcPlate", name, attrs, "USERDEFINED"));
        var minZ = verts.Count > 0 ? verts.Min(x => x.Z) : 0;
        var storey = StoreyAt(ctx.Model, minZ);
        if (Spatial.StoreyOf(ctx.Model, id)?.Id != storey.Id) Spatial.Contain(ctx, storey.Id, id);
        return true;
    }
}
