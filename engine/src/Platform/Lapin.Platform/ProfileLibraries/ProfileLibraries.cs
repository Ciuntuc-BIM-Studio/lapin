using System.Text.Json.Nodes;
using Lapin.Drawing.Import;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform.ProfileLibraries;

/// <summary>
/// A profile library (docs/plan-profile-libraries.md): a name, what it is mostly of, its profiles. Kept as a
/// <c>.lapinprofiles</c> file (JSON: <c>format</c>, <c>version</c>, <c>name</c>, <c>material</c>, <c>standard</c>, <c>profiles</c>) on this
/// computer and in a firm's folders; a project keeps only the profiles it uses (<see cref="ProjectProfiles"/>).
/// </summary>
public sealed record ProfileLibrary(string Name, string? Description, string Material, IReadOnlyList<SectionProfile> Profiles)
{
    /// <summary>The standard the pickers show its profiles under (EN, AISC / ASTM, a firm's own); none: each profile's, else the library's name (<see cref="ProfileStandards"/>).</summary>
    public string? Standard { get; init; }

    public const string Format = "lapin-profiles";
    public const string Extension = "lapinprofiles";

    /// <summary>The library as a file holds it; <paramref name="summaries"/> adds each profile's area and mass worked out (for a list, not read back).</summary>
    public JsonObject ToJson(bool summaries = false) => new()
    {
        ["format"] = Format, ["version"] = 1, ["name"] = Name, ["description"] = Description, ["material"] = Material, ["standard"] = Standard,
        ["profiles"] = new JsonArray(Profiles.Select(p => (JsonNode)(summaries ? ProjectProfiles.WithSummary(p) : p.ToJson())).ToArray()),
    };

    public static ProfileLibrary? From(JsonNode? n)
    {
        if (n is not JsonObject o || o["profiles"] is not JsonArray list) return null;
        var profiles = list.Select(SectionProfile.From).OfType<SectionProfile>().ToList();
        return new((string?)o["name"] is { Length: > 0 } name ? name : "Profiles", (string?)o["description"], (string?)o["material"] ?? "steel", profiles)
        {
            Standard = (string?)o["standard"] is { Length: > 0 } st && st.Trim().Length > 0 ? st.Trim() : null,
        };
    }

    public static ProfileLibrary BuiltIn() => new(BuiltInProfiles.Name, "Standard dimensions of the common European steel profiles; every property worked out by Lapin.", "steel", BuiltInProfiles.All) { Standard = ProfileStandards.En };
}

/// <summary>
/// The project's copies of the library profiles it uses (<c>LapinSectionProfile</c>): each the profile whole, the library's
/// name and where it came from, so the project opens anywhere. A column, beam or member type made from one keeps its id in
/// its section (<c>section.profileId</c>) and follows it when the copy is updated from the library, which is the user's step.
/// </summary>
public static class ProjectProfiles
{
    public const string Class = "LapinSectionProfile";

    public static SectionProfile? Read(Entity e) => SectionProfile.From(e.Attrs["profile"]);

    /// <summary>The copy of <paramref name="p"/> from <paramref name="library"/> in the project (one with its name from that library), else a new one.</summary>
    /// <param name="standard">The standard it is shown under (<see cref="ProfileStandards"/>); none: its own standard's group.</param>
    public static (Entity Copy, bool Differs) Ensure(ICommandContext ctx, SectionProfile p, string? library, string? standard = null)
    {
        if (p.Problem() is { } why) throw new CommandException(why);
        var lib = string.IsNullOrWhiteSpace(library) ? null : library.Trim();
        var json = p.ToJson();
        foreach (var e in ctx.Model.OfClass(Class))
            if (string.Equals(e.Name, p.Name, StringComparison.OrdinalIgnoreCase) && e.Str("library") == lib)
                return (e, !JsonNode.DeepEquals(e.Attrs["profile"], json));
        var attrs = new JsonObject { ["profile"] = json, ["added"] = DateTime.UtcNow.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) };
        if (lib != null) attrs["library"] = lib;
        if (p.Source != null) attrs["source"] = p.Source;
        var group = !string.IsNullOrWhiteSpace(standard) ? standard.Trim() : lib == BuiltInProfiles.Name ? ProfileStandards.En : ProfileStandards.Group(p.Standard);
        if (group != null) attrs["standard"] = group;
        var made = new Entity(ctx.NewId(), Class, p.Name, attrs);
        ctx.Add(made);
        return (made, false);
    }

    /// <summary>The types whose section was made of the copy <paramref name="id"/>.</summary>
    public static List<Entity> TypesUsing(IModelView m, string id) => m.Entities.Where(e => e.Attrs.Any(kv => Holds(kv.Value, id, 0))).ToList();

    /// <summary>Whether a section of the copy is this attribute, or under it (a steel hall's frames keep their sections in their spec).</summary>
    static bool Holds(JsonNode? n, string id, int depth) => n is JsonObject o && ((o["profileId"] is JsonValue v && v.TryGetValue<string>(out var s) && s == id)
        || depth < 2 && o.Any(kv => kv.Value is JsonObject && Holds(kv.Value, id, depth + 1)));

    /// <summary>Every section of the copy under an attribute replaced by the profile's new one.</summary>
    static void Replace(JsonObject attrs, string id, SectionProfile p, int depth)
    {
        foreach (var key in attrs.Select(kv => kv.Key).ToList())
        {
            if (attrs[key] is not JsonObject o) continue;
            if (o["profileId"] is JsonValue v && v.TryGetValue<string>(out var s) && s == id) attrs[key] = p.SectionJson(id);
            else if (depth < 2) Replace(o, id, p, depth + 1);
        }
    }

    /// <summary>The copy replaced by <paramref name="p"/> (the library's newer one): every type made of it takes the new section, its elements following.</summary>
    public static int Update(ICommandContext ctx, Entity copy, SectionProfile p)
    {
        if (p.Problem() is { } why) throw new CommandException(why);
        var attrs = copy.CloneAttrs();
        attrs["profile"] = p.ToJson();
        attrs["updated"] = DateTime.UtcNow.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        ctx.Replace(copy.With(name: p.Name, attrs: attrs));
        var types = TypesUsing(ctx.Model, copy.Id);
        foreach (var t in types)
        {
            var ta = t.CloneAttrs();
            Replace(ta, copy.Id, p, 0);
            ctx.Replace(t.With(attrs: ta));
        }
        return types.Count;
    }

    /// <summary>A material's name in the project for a library's material (a new type's when none is said).</summary>
    public static string MaterialName(string material) => material switch
    {
        "aluminium" => "Aluminium", "timber" => "Glued laminated timber", "concrete" => "Reinforced concrete", "pvc" => "PVC", "stainless" => "Stainless steel", _ => "Steel",
    };

    /// <summary>A profile's JSON with its area and mass worked out beside it (<c>summary</c>), for lists.</summary>
    public static JsonObject WithSummary(SectionProfile p)
    {
        var o = p.ToJson();
        var v = ProfileProps.Compute(p).Values;
        var s = new JsonObject();
        if (v.TryGetValue("A", out var a)) s["A"] = a;
        if (p.Given.TryGetValue("mass", out var gm)) s["mass"] = gm; else if (v.TryGetValue("mass", out var m)) s["mass"] = m;
        if (ProfileProps.Checks(p, v).Count is > 0 and var n) s["checks"] = n;
        o["summary"] = s;
        return o;
    }

    /// <summary>
    /// A profile of closed outlines (a drafted shape's, a DXF's): the biggest the outside, those inside it its holes,
    /// any other outside beside it (a compound profile); centred on their box, metres.
    /// </summary>
    public static SectionProfile FromOutlines(List<List<Vec2>> loops, string name, string material, string? source)
    {
        var clean = loops.Where(l => l.Count >= 3 && Math.Abs(Polygon2.SignedArea(l)) > 1e-12).ToList();
        if (clean.Count == 0) throw new CommandException("No closed outline enclosing an area.");
        var all = clean.SelectMany(l => l).ToList();
        var c = new Vec2((all.Min(p => p.X) + all.Max(p => p.X)) / 2, (all.Min(p => p.Y) + all.Max(p => p.Y)) / 2);
        var moved = clean.Select(l => l.Select(p => p - c).ToList()).OrderByDescending(l => Math.Abs(Polygon2.SignedArea(l))).ToList();
        var rings = new List<IReadOnlyList<Vec2>>();
        var outsides = new List<List<Vec2>>();
        foreach (var l in moved)
        {
            // Inside an outside an odd number of times: a hole (clockwise); else an outside (anticlockwise).
            var depth = outsides.Count(o => Polygon2.Contains(o, l[0])) + rings.Count(r => Polygon2.SignedArea(r) < 0 && Polygon2.Contains(r, l[0]));
            var ccw = Polygon2.EnsureCcw(l).ToList();
            if (depth % 2 == 1) { var h = ccw.AsEnumerable().Reverse().ToList(); rings.Add(h); }
            else { outsides.Add(ccw); rings.Add(ccw); }
        }
        var pts = rings.SelectMany(r => r).ToList();
        return new SectionProfile
        {
            Name = name, Kind = "outline", Rings = rings, Material = material, Source = source,
            B = pts.Max(p => p.X) - pts.Min(p => p.X), H = pts.Max(p => p.Y) - pts.Min(p => p.Y),
        };
    }

    /// <summary>Why drawn rings cannot be a profile: one that crosses itself or another (touching at a point is allowed to none), or a degenerate ring.</summary>
    public static void CheckRings(IReadOnlyList<List<Vec2>> rings)
    {
        static bool Cross(Vec2 a, Vec2 b, Vec2 c, Vec2 d)
        {
            double Side(Vec2 p, Vec2 q, Vec2 r) => Vec2.Cross(q - p, r - p);
            var (d1, d2, d3, d4) = (Side(a, b, c), Side(a, b, d), Side(c, d, a), Side(c, d, b));
            return ((d1 > 1e-12 && d2 < -1e-12) || (d1 < -1e-12 && d2 > 1e-12)) && ((d3 > 1e-12 && d4 < -1e-12) || (d3 < -1e-12 && d4 > 1e-12));
        }
        for (var i = 0; i < rings.Count; i++)
        {
            if (Math.Abs(Polygon2.SignedArea(rings[i])) < 1e-10) throw new CommandException($"Outline {i + 1} encloses no area.");
            for (var j = i; j < rings.Count; j++)
                for (var p = 0; p < rings[i].Count; p++)
                    for (var q = i == j ? p + 2 : 0; q < rings[j].Count; q++)
                    {
                        if (i == j && p == 0 && q == rings[i].Count - 1) continue;
                        if (Cross(rings[i][p], rings[i][(p + 1) % rings[i].Count], rings[j][q], rings[j][(q + 1) % rings[j].Count]))
                            throw new CommandException(i == j ? $"Outline {i + 1} crosses itself." : $"Outlines {i + 1} and {j + 1} cross each other.");
                    }
        }
    }

    /// <summary>The closed outlines of drafted shapes (closed polylines with their arcs, circles), in their view's plane.</summary>
    public static List<List<Vec2>> DraftedLoops(IModelView m, IEnumerable<string> ids)
    {
        var o = new List<List<Vec2>>();
        foreach (var id in ids)
        {
            if (m.Get(id) is not { Class: IfcClass.Annotation } e || e.Attrs["shape"] is not JsonObject shape) continue;
            switch ((string?)shape["type"])
            {
                case "polyline" when shape["closed"] is JsonValue cv && cv.TryGetValue<bool>(out var closed) && closed:
                {
                    var pts = Args.StoredPoints(shape["pts"]);
                    var bulges = (shape["bulges"] as JsonArray)?.Select(b => Json.Number(b) ?? 0).ToList();
                    var vs = pts.Select((p, i) => new ProfileVertex(p.X, p.Y, bulges != null && i < bulges.Count ? bulges[i] : 0)).ToList();
                    if (vs.Count >= 3) o.Add([.. new ProfileShape("", vs, default).Outline()]);
                    break;
                }
                case "circle" when Args.TryPoint(shape["c"] ?? shape["center"]) is { } c && Json.Number(shape["r"]) is { } r && r > 0:
                    o.Add(Circle(c, r));
                    break;
            }
        }
        return o;
    }

    static List<Vec2> Circle(Vec2 c, double r)
    {
        var n = Chords.Circle(r);
        return Enumerable.Range(0, n).Select(k => new Vec2(c.X + r * Math.Cos(2 * Math.PI * k / n), c.Y + r * Math.Sin(2 * Math.PI * k / n))).ToList();
    }

    /// <summary>A DXF's closed outlines (closed polylines with their bulges, circles), in metres (its units, else <paramref name="unit"/>, else a guess).</summary>
    public static List<List<Vec2>> DxfLoops(byte[] bytes, double? unit, out double factor)
    {
        var doc = DxfReader.Read(bytes);
        var loops = new List<List<Vec2>>();
        foreach (var e in doc.Entities)
            switch (e)
            {
                case DxfPolyline { Closed: true } pl when pl.Vertices.Count >= 3:
                    loops.Add([.. new ProfileShape("", pl.Vertices.Select(v => new ProfileVertex(v.P.X, v.P.Y, v.Bulge)).ToList(), default).Outline()]);
                    break;
                case DxfCircle ci when ci.R > 0: loops.Add(Circle(ci.C, ci.R)); break;
            }
        if (loops.Count == 0) throw new CommandException("The DXF has no closed polyline or circle: draw the profile's outline (and its holes) as closed polylines.");
        var extent = loops.SelectMany(l => l).Aggregate((Min: new Vec2(double.MaxValue, double.MaxValue), Max: new Vec2(double.MinValue, double.MinValue)),
            (b, p) => (new Vec2(Math.Min(b.Min.X, p.X), Math.Min(b.Min.Y, p.Y)), new Vec2(Math.Max(b.Max.X, p.X), Math.Max(b.Max.Y, p.Y))));
        // A profile is under a few metres: unitless drawings are read in millimetres when that makes it so.
        factor = unit ?? doc.UnitFactor ?? (Math.Max(extent.Max.X - extent.Min.X, extent.Max.Y - extent.Min.Y) > 5 ? 0.001 : 1);
        var f = factor;
        return loops.Select(l => l.Select(p => p * f).ToList()).ToList();
    }

    /// <summary>The project's copies, each with how many types use it, and the old types whose section is one of the built-in library's (not linked).</summary>
    public static JsonObject ProjectJson(IModelView m)
    {
        var copies = new JsonArray();
        foreach (var e in m.OfClass(Class))
            copies.Add((JsonNode)new JsonObject
            {
                ["id"] = e.Id, ["name"] = e.Name, ["library"] = e.Str("library"), ["standard"] = e.Str("standard") ?? ProfileStandards.Group(Read(e)?.Standard), ["source"] = e.Str("source"), ["added"] = e.Str("added"), ["updated"] = e.Str("updated"),
                ["profile"] = e.Attrs["profile"]?.DeepClone(), ["types"] = new JsonArray(TypesUsing(m, e.Id).Select(t => (JsonNode)t.Id).ToArray()),
            });
        // Types made before the libraries, or by the catalogue's name: those of a built-in profile's size are shown as from it.
        var legacy = new JsonArray();
        foreach (var t in m.Entities.Where(e => e.Attrs["section"] is JsonObject s && s["profileId"] == null && s["kind"] != null))
        {
            var s = (JsonObject)t.Attrs["section"]!;
            var match = BuiltInProfiles.All.FirstOrDefault(p => Same(p, s));
            legacy.Add((JsonNode)new JsonObject { ["typeId"] = t.Id, ["typeName"] = t.Name, ["class"] = t.Class, ["section"] = s.DeepClone(), ["builtin"] = match?.Name });
        }
        // The standards its pickers offer (none: all).
        var standards = ProfileStandards.OfProject(m);
        return new JsonObject { ["copies"] = copies, ["legacy"] = legacy, ["standards"] = standards == null ? null : new JsonArray(standards.Select(x => (JsonNode)x).ToArray()) };
    }

    /// <summary>Whether a type's section JSON has a profile's kind and dimensions (within 0.01 mm).</summary>
    public static bool Same(SectionProfile p, JsonObject s)
    {
        static bool Eq(double a, double b) => Math.Abs(a - b) < 1e-5;
        double N(string k) => Json.Number(s[k]) ?? 0;
        var kind = (string?)s["kind"];
        if (kind == "H") kind = "I";
        if (kind == "Σ") kind = "SIGMA";
        if (kind != p.Kind) return false;
        var width = N("width");
        var depth = p.Kind is "CHS" or "circle" ? width : N("depth");
        var web = N("web") is var w && w > 0 ? w : N("thickness");
        var flange = N("flange") is var f && f > 0 ? f : web;
        return Eq(width, p.B) && Eq(depth, p.H) && (p.Kind is "rectangle" or "circle" || Eq(web, p.Tw) && Eq(flange, p.Tf)) && Eq(N("lip"), p.Lip);
    }
}

/// <summary>The profile libraries' commands and queries (docs/plan-profile-libraries.md); a file's or a folder's are the protocol's (EngineDispatcher).</summary>
public static class ProfileLibraryCommands
{
    static byte[] Bytes(JsonObject a) => (string?)a["data"] is { Length: > 0 } d ? Convert.FromBase64String(d) : throw new CommandException("No file was sent.");

    static SectionProfile ProfileArg(JsonObject a) => SectionProfile.From(a["profile"]) ?? throw new CommandException("No profile was sent (it needs a name and a kind).");

    public static void Register(IModuleRegistry r)
    {
        r.Class(new(ProjectProfiles.Class, "Section profile", [], "#6f7d8c", Kind: "resource"));
        ProfileStandards.Register(r);

        // Lapin's own library (EN steel), each profile with its area and mass.
        r.Query("profiles.builtin", (_, _) => ProfileLibrary.BuiltIn().ToJson(summaries: true));

        // {profile}: its properties worked out and the file's beside them, the checks, its drawn rings.
        r.Query("profiles.properties", (_, a) =>
        {
            var p = ProfileArg(a);
            var o = ProfileProps.Table(p);
            o["profile"] = p.ToJson();
            o["section"] = p.SectionJson(null);
            return o;
        });

        // {profiles: [...]}: each with its summary (a library imported before, its list shown again).
        r.Query("profiles.summaries", (_, a) => new JsonArray((a["profiles"] as JsonArray ?? []).Select(SectionProfile.From).OfType<SectionProfile>().Select(p => (JsonNode)ProjectProfiles.WithSummary(p)).ToArray()));

        // {data}: a CSV or XLSX read, its headings, the first rows and each column's guessed meaning and unit.
        r.Query("profiles.readTable", (_, a) =>
        {
            var table = Lapin.Platform.Tables.Read(Bytes(a));
            var g = ProfileTables.Guess(table);
            var header = Math.Max(0, g.Header);
            var headings = header < table.Count ? table[header] : [];
            return new JsonObject
            {
                ["header"] = g.Header, ["rows"] = Math.Max(0, table.Count - header - 1), ["convention"] = g.Convention, ["system"] = g.System, ["layout"] = g.Layout,
                ["headings"] = new JsonArray(headings.Select(h => (JsonNode)h).ToArray()),
                ["sample"] = new JsonArray(table.Skip(header + 1).Where(x => !x.All(string.IsNullOrWhiteSpace)).Take(8).Select(x => (JsonNode)new JsonArray(x.Select(c => (JsonNode)c).ToArray())).ToArray()),
                ["map"] = new JsonObject(g.Map.Select(k => KeyValuePair.Create(k.Key, (JsonNode?)(k.Value == null ? null : ProfileTables.ColumnJson(k.Value))))),
                ["fields"] = new JsonArray(ProfileTables.Fields.Select(f => (JsonNode)new JsonObject { ["key"] = f, ["dimension"] = ProfileTables.DimensionOf(f) }).ToArray()),
                ["units"] = new JsonObject(ProfileTables.Units.Select(u => KeyValuePair.Create(u.Key, (JsonNode?)new JsonArray(u.Value.Select(x => (JsonNode)x.Unit).ToArray())))),
                ["kinds"] = new JsonArray(SectionProfile.Kinds.Where(k => k != "outline").Select(k => (JsonNode)k).ToArray()),
            };
        });

        // {data, header?, map?, kind?, family?, standard?, material?, name?, group?}: the table read as a library (not kept: the interface keeps it);
        // `group` the standard the pickers show it under (none: its profiles' standard's, else its name).
        r.Query("profiles.importTable", (_, a) =>
        {
            var table = Lapin.Platform.Tables.Read(Bytes(a));
            var g = ProfileTables.Guess(table);
            var header = Json.Number(a["header"]) is { } h && h >= 0 ? (int)h : Math.Max(0, g.Header);
            var map = ProfileTables.MapFrom(a["map"], g);
            var material = (string?)a["material"] is { Length: > 0 } m && SectionProfile.Densities.ContainsKey(m) ? m : "steel";
            var name = (string?)a["name"] is { Length: > 0 } n ? Path.GetFileNameWithoutExtension(n) : "Imported profiles";
            var res = ProfileTables.Import(table, header, map, SectionProfile.KindOf((string?)a["kind"]), (string?)a["family"], (string?)a["standard"], material, (string?)a["name"]);
            var lib = new ProfileLibrary(name, null, material, res.Profiles) { Standard = (string?)a["group"] is { Length: > 0 } grp ? grp.Trim() : null }.ToJson(summaries: true);
            lib["skipped"] = res.Skipped;
            lib["notes"] = new JsonArray(res.Notes.Select(x => (JsonNode)x).ToArray());
            lib["checks"] = new JsonArray(res.Checks.Take(500).Select(x => (JsonNode)ProfileProps.CheckJson(x)).ToArray());
            lib["checkCount"] = res.Checks.Count;
            return lib;
        });

        // {profiles}: as CSV, Lapin's own columns (metric, EN axes).
        r.Query("profiles.csv", (_, a) => ProfileTables.Csv((a["profiles"] as JsonArray ?? []).Select(SectionProfile.From).OfType<SectionProfile>()));

        // The project's copies and the types of a built-in size made before the libraries.
        r.Query("profiles.project", (q, _) => ProjectProfiles.ProjectJson(q.Model));

        // {ids, name?, material?}: drafted closed outlines (and the holes inside) as an outline profile.
        r.Query("profiles.fromOutline", (q, a) =>
        {
            var ids = (a["ids"] as JsonArray)?.Select(x => (string?)x).OfType<string>().ToList() ?? [];
            var loops = ProjectProfiles.DraftedLoops(q.Model, ids);
            if (loops.Count == 0) throw new CommandException("Select closed outlines drawn with Drafting (a closed polyline, a rectangle or a circle; those inside another are its holes).");
            return ProjectProfiles.WithSummary(ProjectProfiles.FromOutlines(loops, (string?)a["name"] is { Length: > 0 } n ? n.Trim() : "Outline profile", (string?)a["material"] ?? "steel", "drafting"));
        });

        // {rings: [[[x, y], …], …], unit? (metres per unit, 0.001 = millimetres), name?, material?}: a profile drawn in the editor (report #128),
        // each ring a closed outline: the biggest the outside, those inside it holes, any other outside beside it; a ring that crosses
        // itself or another is refused. Centred on its box, with its summary.
        r.Query("profiles.fromRings", (_, a) =>
        {
            var unit = Json.Number(a["unit"]) is { } u && u > 0 ? u : 0.001;
            var loops = (a["rings"] as JsonArray ?? []).OfType<JsonArray>()
                .Select(ring => ring.OfType<JsonArray>().Where(p => p.Count >= 2).Select(p => new Vec2((Json.Number(p[0]) ?? 0) * unit, (Json.Number(p[1]) ?? 0) * unit)).ToList()).ToList();
            if (loops.Count == 0) throw new CommandException("Draw an outline first: a ring of at least three points.");
            if (loops.Any(l => l.Count < 3)) throw new CommandException("An outline needs three points at least.");
            ProjectProfiles.CheckRings(loops);
            return ProjectProfiles.WithSummary(ProjectProfiles.FromOutlines(loops, (string?)a["name"] is { Length: > 0 } n ? n.Trim() : "Drawn profile", (string?)a["material"] ?? "aluminium", "drawn"));
        });

        // {data, name?, unit? (metres per drawing unit), material?}: a DXF's closed outlines as an outline profile.
        r.Query("profiles.fromDxf", (_, a) =>
        {
            var loops = ProjectProfiles.DxfLoops(Bytes(a), Json.Number(a["unit"]) is { } u && u > 0 ? u : null, out var factor);
            var name = (string?)a["name"] is { Length: > 0 } n ? Path.GetFileNameWithoutExtension(n) : "DXF profile";
            var o = ProjectProfiles.WithSummary(ProjectProfiles.FromOutlines(loops, name, (string?)a["material"] ?? "steel", (string?)a["name"]));
            o["unit"] = factor;
            return o;
        });

        // {profile, library?}: the profile copied into the project (or the copy already there), its section for a type.
        r.Command("profiles.use", "Use profile", (ctx, a) =>
        {
            var (copy, differs) = ProjectProfiles.Ensure(ctx, ProfileArg(a), (string?)a["library"], (string?)a["standard"]);
            return new JsonObject { ["id"] = copy.Id, ["section"] = ProjectProfiles.Read(copy)!.SectionJson(copy.Id), ["differs"] = differs };
        });

        // {id, profile}: the copy updated from its library; the types made of it follow.
        r.Command("profiles.update", "Update profile from library", (ctx, a) =>
        {
            var copy = Spatial.Require(ctx.Model, (string?)a["id"], ProjectProfiles.Class);
            return new JsonObject { ["types"] = ProjectProfiles.Update(ctx, copy, ProfileArg(a)) };
        });

        // {id}: a copy no type uses taken out of the project.
        r.Command("profiles.remove", "Remove profile", (ctx, a) =>
        {
            var copy = Spatial.Require(ctx.Model, (string?)a["id"], ProjectProfiles.Class);
            if (ProjectProfiles.TypesUsing(ctx.Model, copy.Id) is { Count: > 0 } used)
                throw new CommandException($"„{copy.Name}” is the section of {string.Join(", ", used.Take(3).Select(t => $"„{t.Name}”"))}{(used.Count > 3 ? "…" : "")}: change their profile first.");
            ctx.Remove(copy.Id);
            return null;
        });
    }
}
