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
/// <item>A curved member (packet 60) lying level, its section upright, becomes a curved beam (an arc through its ends); one
/// arching in any other plane is left out and said (Lapin's curved members are level beams).</item>
/// <item>Plates (packet 20) come in as plates with their bodies, except those of Lapin's own steel connections still in the
/// project (they are built from the connection). Grids (packet 50) as grids.</item>
/// <item>Holes (packet 22): through holes cut in the plate they name (its body made with them), or in the member Lapin's own
/// file names as their parent (kept on it, <see cref="MemberHoles"/>), each by its number, so a file read again replaces an
/// element's holes rather than adding to them. A recess (a hole less deep than its plate) and a hole off its plate are left
/// out and said. SDNF carries no bolts: the report says the holes came without them.</item>
/// <item>Left out, and said: loads, connection details; end releases (Lapin has none yet).</item>
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
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var m = ctx.Model;
        var notes = new SdnfProtocol(file.Notes);
        var cancel = ctx.Query.Cancellation;
        var progress = ctx.Query.Progress;
        // How far along, every hundredth and at the end; a cancel asked for stops here (the edit is undone with it).
        void Step(string stage, int done, int total)
        {
            cancel.ThrowIfCancellationRequested();
            if (progress != null && total > 0 && (done == total || done % Math.Max(1, total / 100) == 0)) progress(stage, done, total);
        }
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

        // The holes by the number of the plate (or member) they go through: a plate's first, as the format has it.
        var plateNumbers = file.Plates.Select(p => p.Id).ToHashSet();
        var holesOf = file.Holes.GroupBy(h => h.Parent).ToDictionary(x => x.Key, x => x.ToList());
        var placed = new HashSet<long>();
        var holes = 0;

        var seen = new HashSet<string>();
        // The new elements' storeys and types, related at the end in one go each (one relationship holds a storey's thousands).
        var relate = new Relations();
        var holesLater = new List<(string Id, List<SdnfHole> Holes)>();
        var (linear, curved) = (0, 0);
        var done = 0;
        foreach (var mb in file.Members)
        {
            Step("Bringing the members in", ++done, file.Members.Count);
            var id = file.LapinIds.TryGetValue($"member {mb.Id}", out var g) && GlobalId.IsValid(g) ? g
                : own.TryGetValue(mb.Id, out var o) ? o
                : hashed.TryGetValue(mb.Id, out var h) && lapinFile ? h
                : DerivedId($"{source}\nmember\n{mb.Id}");
            if (!seen.Add(id)) { skipped++; notes.Skip("members in the file twice (the second left out)", Item(mb)); continue; }
            if (mb.Releases.Any(r => r != 0)) released++;
            (string Class, string How)? r;
            try { r = mb.Arc is { } arc ? Curved(ctx, mb, arc, id, source, lapinFile, unknown, notes, relate) : Member(ctx, mb, id, source, lapinFile, unknown, notes, relate); }
            catch (Exception ex) when (ex is not (OperationCanceledException or CommandException))
            {
                notes.Skip($"members Lapin could not build ({ex.Message})", Item(mb));
                r = null;
            }
            if (r == null) { skipped++; continue; }
            var (cls, how) = r.Value;
            byClass[cls] = byClass.GetValueOrDefault(cls) + 1;
            if (mb.Arc != null) curved++; else linear++;
            if (how == "made") made++; else if (how == "remade") remade++; else updated++;
            // Its holes, when the file names it as their parent and no plate has its number: once it has its type (its section).
            var mine = !plateNumbers.Contains(mb.Id) && holesOf.TryGetValue(mb.Id, out var list) ? list : [];
            if (mine.Count > 0) placed.Add(mb.Id);
            holesLater.Add((id, mine));
        }

        done = 0;
        foreach (var p in file.Plates)
        {
            Step("Bringing the plates in", ++done, file.Plates.Count);
            var tag = file.LapinIds.TryGetValue($"plate {p.Id}", out var t) ? t : null;
            placed.Add(p.Id);
            // A plate of one of Lapin's own connections still in the project: the connection builds it (its holes are its bolts').
            if (tag != null && tag.Split(':') is ["node", var nid, ..] && m.Get(nid) is { Class: SteelNodes.Class }) continue;
            var id = tag != null && GlobalId.IsValid(tag) ? tag : DerivedId(tag ?? $"{source}\nplate\n{p.Id}");
            int cut;
            try { cut = Plate(ctx, p, id, source, holesOf.GetValueOrDefault(p.Id) ?? [], notes, relate); }
            catch (Exception ex) when (ex is not (OperationCanceledException or CommandException))
            {
                notes.Skip($"plates Lapin could not build ({ex.Message})", Item(p));
                cut = -2;
            }
            if (cut >= 0) { plates++; holes += cut; }
            else
            {
                skipped++;
                if (cut == -1) notes.Skip("plates with no shape Lapin can build (fewer than three vertices, or no thickness)", Item(p));
            }
        }
        // The storeys and types in one go, then the members' holes (which read the member's section from its type).
        relate.Flush(ctx);
        done = 0;
        foreach (var (id, mine) in holesLater)
        {
            Step("Placing the holes", ++done, holesLater.Count);
            holes += SetHoles(ctx, id, mine, notes);
        }
        foreach (var x in file.Holes.Where(x => !placed.Contains(x.Parent)))
            notes.Skip("holes naming a plate or member the file does not have", $"hole {x.Id} in {x.Parent}{At(x.Line)}");
        if (file.Holes.Count > 0)
            notes.Note("SDNF carries no bolts: the holes came in without them (Lapin's bolts belong to its own steel connections).");

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
            notes.Note($"The profile „{u}” is in none of the profile libraries: its members are drawn as a {SdnfProfiles.PlaceholderSize * 1000:0} mm square, named after it, until its type is given a profile.");
        if (released > 0) notes.Note($"{released} member(s) have released ends in the file: Lapin keeps no end releases yet, they came in fixed.");
        return new JsonObject
        {
            ["name"] = name, ["version"] = file.Title.Version, ["source"] = file.Title.IssueCode,
            ["made"] = made, ["updated"] = updated, ["remade"] = remade, ["plates"] = plates, ["holes"] = holes, ["grids"] = grids, ["skipped"] = skipped,
            ["byClass"] = new JsonObject(byClass.OrderBy(k => k.Key).Select(k => KeyValuePair.Create(k.Key, (JsonNode?)k.Value))),
            ["unknownProfiles"] = new JsonArray([.. unknown.Select(u => (JsonNode)u)]),
            ["notes"] = new JsonArray([.. notes.Lines().Select(n => (JsonNode)n)]),
            // The protocol (docs/sdnf.md, report #148): each packet's count, what was imported of it, and what was left out or changed and why.
            ["packets"] = new JsonArray([.. file.Packets.Select(x => (JsonNode)new JsonObject
            {
                ["packet"] = x.Id, ["line"] = x.Line, ["announced"] = x.Announced, ["read"] = x.Read,
                ["imported"] = x.Id switch { "10" => linear, "60" => curved, "20" => plates, "22" => holes, "50" => grids, _ => 0 },
            })]),
            ["skips"] = SdnfProtocol.Json(notes.Skips),
            ["warnings"] = SdnfProtocol.Json(notes.Warnings),
            ["lines"] = file.Lines,
            ["ms"] = clock.ElapsedMilliseconds,
        };
    }

    /// <summary>A member as the protocol names it: its number and line.</summary>
    static string Item(SdnfMember mb) => $"{(mb.Arc != null ? "curved member" : "member")} {mb.Id}{At(mb.Line)}";
    static string Item(SdnfPlate p) => $"plate {p.Id}{At(p.Line)}";
    static string At(int line) => line > 0 ? $" (line {line})" : "";

    /// <summary>
    /// Relationships to make at the end of the import, each storey's and each type's in one go (<see cref="EditRelations.RelateAll"/>):
    /// relating them one at a time copies the relationship's list each time, which for thousands of members is most of the import.
    /// </summary>
    sealed class Relations
    {
        readonly Dictionary<(string Class, string Relating), List<string>> _by = [];
        readonly List<(string Class, string Relating)> _order = [];
        public void Add(string relationClass, string relating, string related)
        {
            if (!_by.TryGetValue((relationClass, relating), out var l)) { _by[(relationClass, relating)] = l = []; _order.Add((relationClass, relating)); }
            l.Add(related);
        }
        public void Flush(ICommandContext ctx)
        {
            foreach (var k in _order) ctx.RelateAll(k.Class, k.Relating, _by[k]);
            _by.Clear();
            _order.Clear();
        }
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

    static (string Class, string How)? Member(ICommandContext ctx, SdnfMember mb, string id, string source, bool lapinFile, ISet<string> unknown, SdnfProtocol notes, Relations relate)
    {
        var m = ctx.Model;
        var length = (mb.End - mb.Start).Length;
        if (length < 1e-3) { notes.Skip("members shorter than a millimetre", Item(mb)); return null; }
        var section = SectionOf(m, mb, unknown);
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
        if (existing != null && existing.Class != cls) notes.Warn($"elements that no longer fit their class as the file has them, made again as a {(cls == SteelModule.Member ? "member" : cls[3..].ToLowerInvariant())} under their ids", $"„{existing.Name}”, {Item(mb)}");

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
        return (cls, Commit(ctx, mb, id, source, existing, cls, attrs, predefined, storey, type, relate));
    }

    /// <summary>The element as the file has it: updated in place when it is already of its class, else (made again) added under its id; its storey and type.</summary>
    static string Commit(ICommandContext ctx, SdnfMember mb, string id, string source, Entity? existing, string cls, JsonObject attrs, string predefined, Entity storey, Entity type, Relations relate)
    {
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
        if (Spatial.StoreyOf(ctx.Model, id)?.Id != storey.Id) relate.Add(IfcClass.RelContainedInSpatialStructure, storey.Id, id);
        if (ctx.Model.RelatingOf(id, IfcClass.RelDefinesByType)?.Id != type.Id) relate.Add(IfcClass.RelDefinesByType, type.Id, id);
        return how;
    }

    /// <summary>The member's section: by its name, else a placeholder named after it (listed).</summary>
    static Section SectionOf(IModelView m, SdnfMember mb, ISet<string> unknown)
    {
        if (SdnfProfiles.Resolve(m, mb.Size) is { } s) return s;
        var p = SdnfProfiles.Placeholder(mb.Size.Length > 0 ? mb.Size : "?");
        unknown.Add(p.Name!);
        return p;
    }

    /// <summary>
    /// A curved member (packet 60) as a curved beam: level (its arc's plane and its ends at one height), its section upright
    /// all along. Its line through the cardinal point moved to the section's top centre (across the arc: a smaller or
    /// larger radius; up or down), then the arc through its ends about the file's centre, the way its sweep turns. Lapin's
    /// own file's node offsets put its ends back on the columns' centres, as a straight beam's.
    /// </summary>
    static (string Class, string How)? Curved(ICommandContext ctx, SdnfMember mb, SdnfArc arc, string id, string source, bool lapinFile, ISet<string> unknown, SdnfProtocol notes, Relations relate)
    {
        var m = ctx.Model;
        var section = SectionOf(m, mb, unknown);
        var (s, e) = (mb.Start, mb.End);
        if (lapinFile) (s, e) = (s + mb.StartOffset, e + mb.EndOffset);
        var c = arc.Centre;
        var n = arc.Normal.Length > 1e-9 ? arc.Normal.Normalized() : Vec3.UnitZ;
        var radius = (s - c).Length;
        if (radius < 1e-3 || (e - c).Length < 1e-3) { notes.Skip("curved members with their ends on their centre", Item(mb)); return null; }
        // The normal about which it runs counter-clockwise from its start to its end; its tangent at the start.
        var ccwN = arc.Sweep < 0 ? -n : n;
        var u0 = Vec3.Cross(ccwN, s - c).Normalized();
        var (v, w, flipped) = Frame(u0, mb.Orientation, mb.Rotation, mb.MirrorX, mb.MirrorY);
        if (!Handed(section)) flipped = false;
        var level = Math.Abs(n.Z) > 1 - 1e-6 && Math.Abs(s.Z - e.Z) < 1e-6 && Math.Abs(s.Z - c.Z) < 1e-6 && Vec3.Dot(w, Vec3.UnitZ) > 1 - 1e-6;
        if (!level)
        {
            notes.Skip("curved members arching out of a level plane or with their section turned (Lapin's curved members are level beams with their sections upright)", $"{Item(mb)}, {mb.Size}");
            return null;
        }
        // The section's top centre: across the arc by the cardinal point's x (v is radial, ±1 outward), up by what its y leaves.
        var (cx, cy) = CardinalOffset(mb.Cardinal, section);
        var outward = Vec3.Dot(v, (s - c).Normalized());
        var rr = radius - outward * cx;
        var top = s.Z - cy + section.Depth / 2;
        var cc = c.Xy;
        var (d0, d1) = ((s - c).Xy.Normalized(), (e - c).Xy.Normalized());
        var (a, b) = (cc + d0 * rr, cc + d1 * rr);
        var ccw = ccwN.Z > 0;
        var turn = Math.Atan2(Vec2.Cross(d0, d1), Vec2.Dot(d0, d1));
        if (!ccw) turn = -turn;
        if (turn <= 1e-9) turn += 2 * Math.PI;
        var rise = rr * (1 - Math.Cos(turn / 2)) * (ccw ? -1 : 1);
        var chord = (b - a).Length;
        if (chord < 1e-3 || Math.Abs(rise) > 10 * chord) { notes.Skip("curved members closing nearly a full circle (Lapin's curved beams do not)", Item(mb)); return null; }

        var existing = m.Get(id);
        const string cls = IfcClass.Beam;
        if (existing != null && existing.Class != cls) notes.Warn("elements curved in the file, made again as curved beams under their ids", $"„{existing.Name}”, {Item(mb)}");
        var material = MaterialFor(ctx, mb.Grade, section);
        Entity? type = existing?.Class == cls && m.RelatingOf(id, IfcClass.RelDefinesByType) is { } had && Section.From(had.Attrs["section"]) is { } hs && SameProfile(hs, section)
            && (material == null || (string?)had.Attrs["material"] == Materials.Find(m, material)?.Id) ? had : null;
        type ??= TypeFor(ctx, IfcClass.BeamType, section, material);
        var kept = existing?.Class == cls ? Spatial.StoreyOf(m, id) : null;
        var storey = kept ?? Spatial.Storeys(m).LastOrDefault(x => x.Num("elevation") < top - 1e-3) ?? Spatial.Storeys(m).First();
        var attrs = existing?.Class == cls ? existing.CloneAttrs() : new JsonObject();
        attrs["start"] = J2(a);
        attrs["end"] = J2(b);
        attrs["topOffset"] = R(top - storey.Num("elevation") - storey.Num("height", 3));
        attrs[PathCurve.Key] = R(rise);
        if (flipped) attrs["flipped"] = true; else attrs.Remove("flipped");
        return (cls, Commit(ctx, mb, id, source, existing, cls, attrs, "BEAM", storey, type, relate));
    }

    /// <summary>A loop without its closing vertex.</summary>
    static List<Vec3> Open(IReadOnlyList<(Vec3 P, double Flag)> loop)
    {
        var pts = loop.Select(x => x.P).ToList();
        if (pts.Count > 1 && (pts[^1] - pts[0]).Length < 1e-9) pts.RemoveAt(pts.Count - 1);
        return pts;
    }

    /// <summary>
    /// A member's holes as the file has them, in its frame (they go with it), in place of those it had; none given, it keeps
    /// none. A round hole by its centres and radius; any other by its two loops (curved edges straight between their
    /// vertices). The count put on it.
    /// </summary>
    static int SetHoles(ICommandContext ctx, string id, IReadOnlyList<SdnfHole> holes, SdnfProtocol notes)
    {
        if (ctx.Model.Get(id) is not { } e) return 0;
        var list = new List<MemberHole>();
        if (holes.Count > 0)
        {
            if (MemberAxes.Of(ctx.Model, e) is not { } ax)
                foreach (var h in holes) notes.Skip("holes in curved members (Lapin cuts holes in straight members)", $"hole {h.Id} in „{e.Name}”{At(h.Line)}");
            else
            {
                var origin = MemberHoles.Origin(ctx.Model, e, ax);
                Vec3 L(Vec3 p) => MemberHoles.Local(ax, origin, p);
                foreach (var h in holes)
                {
                    var (top, bottom) = (Open(h.Top), Open(h.Bottom));
                    var round = h.Periphery == 2;
                    if (top.Count == 0 || bottom.Count != top.Count || (top[0] - bottom[0]).Length < 1e-6 || (round ? !(h.Top[0].Flag > 0) : top.Count < 3))
                    {
                        notes.Skip("holes with no shape or no depth", $"hole {h.Id} in „{e.Name}”{At(h.Line)}");
                        continue;
                    }
                    if (h.Top.Any(x => x.Flag == 2) && !round) notes.Warn("holes whose curved edges came in straight between their vertices", $"hole {h.Id}{At(h.Line)}");
                    list.Add(new MemberHole(h.Id, round ? h.Top[0].Flag : 0, [.. top.Select(L)], [.. bottom.Select(L)]));
                }
            }
        }
        if (list.Count == 0 && e.Attrs[MemberHoles.Key] == null) return 0;
        var attrs = e.CloneAttrs();
        if (list.Count == 0) attrs.Remove(MemberHoles.Key); else attrs[MemberHoles.Key] = MemberHoles.Write(list);
        ctx.Replace(e.With(attrs: attrs));
        return list.Count;
    }

    static bool IsAlong(Vec3 offset, Vec3 u) => offset.Length < 1e-9 || Vec3.Cross(offset, u).Length < 1e-6 * Math.Max(1, offset.Length);

    /// <summary>A loop as an element keeps it: [x, y, z, flag] each.</summary>
    public static JsonArray LoopJson(IReadOnlyList<(Vec3 P, double Flag)> loop) =>
        new([.. loop.Select(x => (JsonNode)new JsonArray(R(x.P.X), R(x.P.Y), R(x.P.Z), R(x.Flag)))]);

    public static List<(Vec3 P, double Flag)> LoopOf(JsonNode? n) =>
        n is JsonArray a ? [.. a.OfType<JsonArray>().Where(x => x.Count >= 4).Select(x => (new Vec3(Json.Number(x[0]) ?? 0, Json.Number(x[1]) ?? 0, Json.Number(x[2]) ?? 0), Json.Number(x[3]) ?? 0))] : [];

    /// <summary>
    /// A plate as an element of its own, its body the plate's (a prism between its two faces; a circle by the chord rule),
    /// its through holes cut in it: each laid onto the plate's face along its thickness, kept when it lies inside the plate.
    /// The plate as the file has it and its holes are kept with it (<c>sdnf.def</c>, <c>sdnf.holes</c>), so an SDNF export
    /// writes them back. The count of holes cut; −1 when the plate has no body.
    /// </summary>
    static int Plate(ICommandContext ctx, SdnfPlate p, string id, string source, IReadOnlyList<SdnfHole> holes, SdnfProtocol notes, Relations relate)
    {
        List<Vec3>? outline = null;
        var sweep = Vec3.Zero;
        if (p.Periphery == 2 && p.Top.Count > 0)
        {
            var c0 = p.Top[0].P;
            var c1 = p.Bottom.Count > 0 ? p.Bottom[0].P : c0;
            var r = p.Top[0].Flag;
            sweep = c1 - c0;
            if (sweep.Length < 1e-9) sweep = Vec3.UnitZ * p.Thickness;
            if (r > 0) outline = MemberHoles.Ring(c0, sweep, r);
        }
        else
        {
            var top = Open(p.Top);
            var bottom = Open(p.Bottom);
            if (top.Count >= 3)
            {
                if (bottom.Count == top.Count) sweep = bottom[0] - top[0];
                else
                {
                    // One loop (version 2): the plate through its thickness from it, as its connect point says.
                    var nn = VecMath.NewellNormal(top).Normalized();
                    var start = p.ConnectPoint switch { 1 => 0.0, -1 => -p.Thickness, _ => -p.Thickness / 2 };
                    top = top.Select(x => x + nn * start).ToList();
                    sweep = nn * p.Thickness;
                }
                outline = top;
            }
        }
        if (outline == null || sweep.Length < 1e-9) return -1;

        // The holes, on the plate's face: through it, inside it.
        var normal = VecMath.NewellNormal(outline).Normalized();
        var along = Vec3.Dot(normal, sweep);
        var (e1, e2) = VecMath.PlaneBasis(normal);
        Vec2 In2(Vec3 q) => new(Vec3.Dot(q - outline[0], e1), Vec3.Dot(q - outline[0], e2));
        var face = outline.Select(In2).ToList();
        var rings = new List<IReadOnlyList<Vec3>>();
        var kept = new JsonArray();
        foreach (var h in holes)
        {
            if (h.Thickness > 1e-9 && h.Thickness < p.Thickness - 1e-4)
            {
                notes.Skip("recesses: holes less deep than their plate, which Lapin's plates do not take", $"hole {h.Id}{At(h.Line)}, {h.Thickness * 1000:0.#} mm into plate {p.Id}, {p.Thickness * 1000:0.#} mm thick");
                continue;
            }
            var round = h.Periphery == 2;
            List<Vec3> ring;
            if (round)
            {
                if (h.Top.Count == 0 || !(h.Top[0].Flag > 0)) { notes.Skip("round holes with no radius", $"hole {h.Id}{At(h.Line)}"); continue; }
                var axis = h.Bottom.Count > 0 && (h.Bottom[0].P - h.Top[0].P).Length > 1e-9 ? h.Bottom[0].P - h.Top[0].P : sweep;
                ring = MemberHoles.Ring(h.Top[0].P, axis, h.Top[0].Flag);
            }
            else
            {
                ring = Open(h.Top);
                if (ring.Count < 3) { notes.Skip("holes with fewer than three vertices", $"hole {h.Id}{At(h.Line)}"); continue; }
                if (h.Top.Any(x => x.Flag == 2)) notes.Warn("holes whose curved edges came in straight between their vertices", $"hole {h.Id}{At(h.Line)}");
            }
            // Laid onto the face along the plate's thickness.
            ring = [.. ring.Select(q => q + sweep * (Vec3.Dot(normal, outline[0] - q) / along))];
            if (!ring.All(q => Polygon2.Contains(face, In2(q))))
            {
                notes.Skip("holes not inside their plate", $"hole {h.Id} in plate {p.Id}{At(h.Line)}");
                continue;
            }
            rings.Add(ring);
            kept.Add((JsonNode)new JsonObject
            {
                ["id"] = h.Id, ["periphery"] = h.Periphery, ["thickness"] = R(h.Thickness),
                ["top"] = LoopJson(h.Top), ["bottom"] = LoopJson(h.Bottom),
            });
        }
        var solid = Prism.Extrude(outline, rings, sweep, "plate");
        if (solid == null) return -1;

        var mesh = Tessellator.Tessellate(solid);
        var verts = new List<Vec3>(mesh.Positions.Length / 3);
        for (var i = 0; i + 2 < mesh.Positions.Length; i += 3) verts.Add(new Vec3(mesh.Positions[i], mesh.Positions[i + 1], mesh.Positions[i + 2]));
        var tris = new List<(int, int, int)>(mesh.Indices.Length / 3);
        for (var i = 0; i + 2 < mesh.Indices.Length; i += 3) tris.Add((mesh.Indices[i], mesh.Indices[i + 1], mesh.Indices[i + 2]));
        var material = MaterialFor(ctx, p.Grade, Section.Rectangle(1, 1)) ?? "Steel";
        var mat = Materials.Ensure(ctx, material);
        var name = p.PieceMark.Length > 0 ? p.PieceMark : "Plate";
        var sdnf = new JsonObject
        {
            ["plate"] = p.Id, ["source"] = source,
            ["def"] = new JsonObject
            {
                ["connect"] = p.ConnectPoint, ["type"] = p.Type, ["grade"] = p.Grade, ["thickness"] = R(p.Thickness), ["periphery"] = p.Periphery,
                ["top"] = LoopJson(p.Top), ["bottom"] = LoopJson(p.Bottom),
            },
        };
        if (kept.Count > 0) sdnf["holes"] = kept;
        var attrs = new JsonObject
        {
            [StoredBodies.Key] = StoredBodies.Write([(name, Materials.ColorOf(ctx.Model, mat.Id), verts, tris)]),
            [Key] = sdnf,
            ["properties"] = new JsonObject
            {
                ["SDNF"] = new JsonObject
                {
                    ["Plate"] = p.Id.ToString(Inv), ["Type"] = p.Type, ["Grade"] = p.Grade,
                    ["Thickness"] = Math.Round(p.Thickness * 1000, 3).ToString("0.###", Inv) + " mm",
                },
            },
        };
        if (kept.Count > 0) ((JsonObject)((JsonObject)attrs["properties"]!)["SDNF"]!)["Holes"] = kept.Count.ToString(Inv);
        if (ctx.Model.Get(id) is { } had)
        {
            if (had.Class == "IfcPlate") ctx.Replace(had.With(name: name, attrs: attrs));
            else return -1;
        }
        else ctx.Add(new Entity(id, "IfcPlate", name, attrs, "USERDEFINED"));
        var minZ = verts.Count > 0 ? verts.Min(x => x.Z) : 0;
        var storey = StoreyAt(ctx.Model, minZ);
        if (Spatial.StoreyOf(ctx.Model, id)?.Id != storey.Id) relate.Add(IfcClass.RelContainedInSpatialStructure, storey.Id, id);
        return rings.Count;
    }
}

/// <summary>
/// What an SDNF import says besides its counts (docs/sdnf.md, the protocol; report #148): notes, and what was left out or
/// changed grouped by why, each group with its items (a member's number and line), so a file of thousands says a reason
/// once with its count rather than a line per member.
/// </summary>
public sealed class SdnfProtocol(IEnumerable<string> notes)
{
    /// <summary>The items a group lists in the notes; the protocol's groups keep more.</summary>
    public const int Shown = 10;
    /// <summary>The items a group keeps.</summary>
    public const int Kept = 200;

    readonly List<string> _notes = [.. notes];
    public sealed record Group(string Reason, List<string> Items) { public int Count { get; set; } }
    readonly List<Group> _skips = [];
    readonly List<Group> _warnings = [];
    public IReadOnlyList<Group> Skips => _skips;
    public IReadOnlyList<Group> Warnings => _warnings;

    public void Note(string text) => _notes.Add(text);
    /// <summary>An item left out, and why.</summary>
    public void Skip(string reason, string item) => Add(_skips, reason, item);
    /// <summary>An item brought in, changed from what the file says, and how.</summary>
    public void Warn(string reason, string item) => Add(_warnings, reason, item);

    static void Add(List<Group> into, string reason, string item)
    {
        var g = into.FirstOrDefault(x => x.Reason == reason);
        if (g == null) into.Add(g = new Group(reason, []));
        g.Count++;
        if (g.Items.Count < Kept) g.Items.Add(item);
    }

    static string Line(string head, Group g) =>
        $"{head}, {g.Reason}: {g.Count} — {string.Join(", ", g.Items.Take(Shown))}{(g.Count > Shown ? $" and {g.Count - Shown} more" : "")}.";

    /// <summary>The notes, then a line for each group: left out first, then what changed.</summary>
    public IEnumerable<string> Lines() =>
        _notes.Concat(_skips.Select(g => Line("Left out", g))).Concat(_warnings.Select(g => Line("Changed", g)));

    public static JsonArray Json(IReadOnlyList<Group> groups) => new([.. groups.Select(g => (JsonNode)new JsonObject
    {
        ["reason"] = g.Reason, ["count"] = g.Count, ["items"] = new JsonArray([.. g.Items.Select(i => (JsonNode)i)]),
    })]);
}
