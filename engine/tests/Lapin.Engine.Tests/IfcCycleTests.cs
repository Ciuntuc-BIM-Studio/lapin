using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;
using Xunit.Abstractions;

namespace Lapin.Engine.Tests;

/// <summary>
/// IFC exported, imported (Lapin's own intent, rebuilt natively) and exported
/// again, many times over: after every cycle the file says what the first one
/// said (classes, GlobalIds, storeys, aggregation, types, openings and fills,
/// layer sets) and the model has what it had (attributes, bodies to the
/// nanometre). LAPIN_IFC_CYCLES sets the number of cycles (10 by default; 100
/// or 1000 as a slow run), LAPIN_IFC_CYCLES_MODEL a .lapin to cycle as well
/// (the test model, never in the repository).
/// </summary>
public class IfcCycleTests(ITestOutputHelper output) : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-ifc-cycles").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

    static int Cycles => int.TryParse(Environment.GetEnvironmentVariable("LAPIN_IFC_CYCLES"), out var n) && n > 0 ? n : 10;

    static Fixture Make() => new(new ArchitectureModule(), new StructureModule());

    static JsonArray Pts(params (double X, double Y)[] p) => new([.. p.Select(q => (JsonNode)new JsonArray(q.X, q.Y))]);

    /// <summary>
    /// A two-storey house: layered outer walls joined at their corners, a partition in a T at both ends, a door and
    /// two windows, a ground slab, a floor with a stair opening, columns on both storeys, a beam, walls and a hipped
    /// roof upstairs, two rooms; wall, slab, column, beam, door and window types.
    /// </summary>
    static Fixture House()
    {
        var f = Make();
        var upper = (string)f.Run("storey.create", new JsonObject { ["name"] = "First floor", ["elevation"] = 3 })!["id"]!;
        var outer = f.Type("Brick 37", ("Plaster", 0.02, false), ("Brick", 0.25, true), ("Insulation", 0.1, false));
        var inner = f.Type("Partition 15", ("Plaster", 0.015, false), ("Brick", 0.12, true), ("Plaster", 0.015, false));
        var south = f.Wall(0, 0, 8, 0, outer);
        f.Wall(8, 0, 8, 6, outer);
        var north = f.Wall(8, 6, 0, 6, outer);
        f.Wall(0, 6, 0, 0, outer);
        f.Wall(4, 0, 4, 6, inner);
        f.Run("architecture.addDoor", new JsonObject { ["wallId"] = south, ["along"] = 1.5 });
        f.Run("architecture.addWindow", new JsonObject { ["wallId"] = south, ["along"] = 6 });
        f.Run("architecture.addWindow", new JsonObject { ["wallId"] = north, ["along"] = 2 });
        f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["points"] = Pts((0, 0), (8, 0), (8, 6), (0, 6)) });
        var floor = (string)f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = upper, ["points"] = Pts((0, 0), (8, 0), (8, 6), (0, 6)) })!["id"]!;
        f.Run("architecture.addSlabOpening", new JsonObject { ["slabId"] = floor, ["points"] = Pts((1, 1), (2.2, 1), (2.2, 3.5), (1, 3.5)) });
        var ct = (string)f.Run("structure.createColumnType", new JsonObject { ["name"] = "C 30x30", ["section"] = new JsonObject { ["shape"] = "rectangle", ["width"] = 0.3, ["depth"] = 0.3 } })!["id"]!;
        f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = ct, ["x"] = 6, ["y"] = 3 });
        f.Run("structure.addColumn", new JsonObject { ["storeyId"] = upper, ["typeId"] = ct, ["x"] = 6, ["y"] = 3 });
        f.Run("structure.addBeam", new JsonObject { ["storeyId"] = upper, ["x1"] = 4, ["y1"] = 3, ["x2"] = 8, ["y2"] = 3 });
        // Steel, by its parametric profiles (decision 416): an angle flipped, a channel, a column turned.
        var angle = (string)f.Run("structure.createBeamType", new JsonObject { ["name"] = "L 100", ["section"] = new JsonObject { ["name"] = "L 100×100×10" }, ["material"] = "Steel" })!["id"]!;
        var flippedAngle = (string)f.Run("structure.addBeam", new JsonObject { ["storeyId"] = upper, ["typeId"] = angle, ["x1"] = 1, ["y1"] = 5, ["x2"] = 3, ["y2"] = 5 })!["id"]!;
        f.Run("structure.flip", new JsonObject { ["ids"] = new JsonArray(flippedAngle) });
        var channel = (string)f.Run("structure.createBeamType", new JsonObject { ["name"] = "UPN 200", ["section"] = new JsonObject { ["name"] = "UPN 200" }, ["material"] = "Steel" })!["id"]!;
        f.Run("structure.addBeam", new JsonObject { ["storeyId"] = upper, ["typeId"] = channel, ["x1"] = 5, ["y1"] = 5, ["x2"] = 7, ["y2"] = 4 });
        var hea = (string)f.Run("structure.createColumnType", new JsonObject { ["name"] = "HEA 200", ["section"] = new JsonObject { ["name"] = "HEA 200" }, ["material"] = "Steel" })!["id"]!;
        f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = hea, ["x"] = 2, ["y"] = 1, ["rotation"] = 30 });
        f.Run("architecture.addWall", new JsonObject { ["storeyId"] = upper, ["typeId"] = outer, ["x1"] = 0, ["y1"] = 0, ["x2"] = 8, ["y2"] = 0 });
        f.Run("architecture.addWall", new JsonObject { ["storeyId"] = upper, ["typeId"] = outer, ["x1"] = 8, ["y1"] = 0, ["x2"] = 8, ["y2"] = 6 });
        f.Run("architecture.addRoof", new JsonObject { ["storeyId"] = upper, ["points"] = Pts((0, 0), (8, 0), (8, 6), (0, 6)) });
        f.Run("architecture.addRoom", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 2, ["y"] = 3, ["name"] = "Living" });
        f.Run("architecture.addRoom", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 6, ["y"] = 1.5, ["name"] = "Kitchen" });
        return f;
    }

    // ─── What a file says, without its entity numbers ─────────────────────

    /// <summary>
    /// A file read for comparison. The spatial structure is keyed by class, name and elevation besides its GlobalId: an
    /// import into a project keeps the project's own project, site, building and matching storeys, so their GlobalIds are
    /// compared apart; a file opened as a project keeps them all (<see cref="The_spatial_structure_keeps_its_GlobalIds"/>).
    /// </summary>
    sealed record FileShape(
        SortedDictionary<string, int> Counts,
        SortedDictionary<string, string> Objects,
        SortedDictionary<string, string> Spatial,
        SortedDictionary<string, string> SpatialIds,
        SortedDictionary<string, int> RootsByClass,
        SortedDictionary<string, string> Others,
        SortedDictionary<string, string> Containment,
        SortedDictionary<string, int> Depth,
        SortedDictionary<string, string> Types,
        SortedSet<string> Voids,
        SortedSet<string> Fills,
        SortedSet<string> LayerSets,
        SortedDictionary<string, string> Usages);

    static readonly HashSet<string> SpatialTypes = ["IFCPROJECT", "IFCSITE", "IFCBUILDING", "IFCBUILDINGSTOREY"];

    static FileShape Read(string path)
    {
        var f = StepFile.Read(path);
        static string? S(object? v) => v as string;
        string Gid(object? r) => S(f.Get(r)?[0]) ?? "?";
        var spatialKey = new Dictionary<string, string>();
        foreach (var e in f.Rooted.Where(e => SpatialTypes.Contains(e.Type)))
            spatialKey[S(e[0])!] = $"{e.Type}:{S(e[2])}" + (e.Type == "IFCBUILDINGSTOREY" ? $"@{e[9]}" : "");
        string Key(string gid) => spatialKey.TryGetValue(gid, out var k) ? k : gid;
        IEnumerable<StepRef> Refs(object? v) => v is List<object?> l ? l.OfType<StepRef>() : [];

        var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var id in f.Entities.Keys) { var t = f.Entities[id].Type; counts[t] = counts.GetValueOrDefault(t) + 1; }
        var objects = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var roots = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var others = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var e in f.Rooted)
        {
            roots[e.Type] = roots.GetValueOrDefault(e.Type) + 1;
            if (SpatialTypes.Contains(e.Type)) continue;
            if (!e.Type.StartsWith("IFCREL") && e.Type is not ("IFCPROPERTYSET" or "IFCELEMENTQUANTITY")) objects[S(e[0])!] = $"{e.Type} {S(e[2])}";
            else others[S(e[0])!] = e.Type + (e.Type.StartsWith("IFCREL") ? "" : $" {S(e[2])}");
        }
        var containment = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var r in f.OfType("IFCRELCONTAINEDINSPATIALSTRUCTURE"))
            foreach (var p in Refs(r[4])) containment[Gid(p)] = Key(Gid(r[5]));
        // Aggregation (and nesting) depth below the project.
        var parent = new Dictionary<string, string>();
        foreach (var r in f.OfType("IFCRELAGGREGATES").Concat(f.OfType("IFCRELNESTS")))
            foreach (var p in Refs(r[5])) parent[Gid(p)] = Gid(r[4]);
        var depth = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var child in parent.Keys)
        {
            var (d, at) = (0, child);
            while (parent.TryGetValue(at, out var up) && d < 64) { d++; at = up; }
            depth[Key(child)] = d;
        }
        var types = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var r in f.OfType("IFCRELDEFINESBYTYPE"))
            foreach (var p in Refs(r[4])) types[Gid(p)] = Gid(r[5]);
        var voids = new SortedSet<string>(f.OfType("IFCRELVOIDSELEMENT").Select(r => $"{Gid(r[4])} ← {Gid(r[5])}"), StringComparer.Ordinal);
        var fills = new SortedSet<string>(f.OfType("IFCRELFILLSELEMENT").Select(r => $"{Gid(r[4])} ← {Gid(r[5])}"), StringComparer.Ordinal);
        string LayerSet(StepEntity? set) => set == null ? "?" :
            $"{S(set[1])}: " + string.Join(" | ", Refs(set[0]).Select(l => f.Get(l)!).Select(l => $"{S(f.Get(l[0])?[0])} {l[1]} {l[2]} {S(l[3])} {S(l[4])} {l[6]}"));
        var layerSets = new SortedSet<string>(f.OfType("IFCMATERIALLAYERSET").Select(LayerSet), StringComparer.Ordinal);
        var usages = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var r in f.OfType("IFCRELASSOCIATESMATERIAL"))
        {
            var m = f.Get(r[5]);
            var what = m?.Type switch
            {
                "IFCMATERIALLAYERSETUSAGE" => $"usage {S(f.Get(m[0])?[1])} {(m[1] as StepEnum)?.Value} {(m[2] as StepEnum)?.Value} {m[3]}",
                "IFCMATERIALLAYERSET" => $"set {S(m[1])}",
                "IFCMATERIAL" => $"material {S(m[0])}",
                { } t => t,
                null => "?",
            };
            foreach (var p in Refs(r[4])) usages[Gid(p)] = what;
        }
        var spatial = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var spatialIds = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var (gid, key) in spatialKey) { spatial[key] = key; spatialIds[key] = gid; }
        return new FileShape(counts, objects, spatial, spatialIds, roots, others, containment, depth, types, voids, fills, layerSets, usages);
    }

    // ─── What the model holds ─────────────────────────────────────────────

    sealed record Body(string Class, string Attrs, Vec3 Min, Vec3 Max, double Volume);

    static Dictionary<string, Body> Bodies(Fixture f)
    {
        var o = new Dictionary<string, Body>();
        foreach (var e in f.Model.Entities.Where(e => f.Session.Catalog.Geometry(e.Class) != null))
        {
            var g = f.Session.Geometry(e.Id);
            var vs = g?.Parts.SelectMany(p => p.Solid.Vertices).ToList() ?? [];
            var min = vs.Count == 0 ? new Vec3(0, 0, 0) : new Vec3(vs.Min(v => v.X), vs.Min(v => v.Y), vs.Min(v => v.Z));
            var max = vs.Count == 0 ? new Vec3(0, 0, 0) : new Vec3(vs.Max(v => v.X), vs.Max(v => v.Y), vs.Max(v => v.Z));
            o[e.Id] = new Body(e.Class, e.Attrs.ToJsonString(), min, max, g?.Parts.Sum(p => Measure.Volume(p.Solid)) ?? 0);
        }
        return o;
    }

    static double Drift(Vec3 a, Vec3 b) => Math.Max(Math.Abs(a.X - b.X), Math.Max(Math.Abs(a.Y - b.Y), Math.Abs(a.Z - b.Z)));

    /// <summary>What changed from the first file and the first model, by kind; empty lists where nothing did.</summary>
    sealed class Losses
    {
        public readonly Dictionary<string, List<string>> ByKind = [];
        public double MaxPointDrift, MaxVolumeDrift;
        public int FirstCycleWithLoss = -1;
        public void Add(int cycle, string kind, string what)
        {
            if (!ByKind.TryGetValue(kind, out var l)) ByKind[kind] = l = [];
            if (l.Count < 40) l.Add($"cycle {cycle}: {what}");
            if (FirstCycleWithLoss < 0) FirstCycleWithLoss = cycle;
        }
        public IReadOnlyList<string> Of(string kind) => ByKind.GetValueOrDefault(kind) ?? [];
    }

    static void Compare<TK, TV>(Losses l, int cycle, string kind, IDictionary<TK, TV> first, IDictionary<TK, TV> now) where TK : notnull
    {
        foreach (var (k, v) in first)
            if (!now.TryGetValue(k, out var w)) l.Add(cycle, kind, $"{k} gone ({v})");
            else if (!Equals(v, w)) l.Add(cycle, kind, $"{k}: {v} → {w}");
        foreach (var (k, v) in now) if (!first.ContainsKey(k)) l.Add(cycle, kind, $"{k} new ({v})");
    }

    static void Compare(Losses l, int cycle, string kind, ISet<string> first, ISet<string> now)
    {
        foreach (var x in first.Except(now)) l.Add(cycle, kind, $"{x} gone");
        foreach (var x in now.Except(first)) l.Add(cycle, kind, $"{x} new");
    }

    /// <summary>
    /// The model through <paramref name="cycles"/> rounds of export and import, every round set against the first file and the model it started from;
    /// <paramref name="asProject"/>: each file opened as a project (<see cref="IfcImport.Open"/>), not imported into a new one.
    /// </summary>
    Losses Run(Fixture start, Func<Fixture> make, int cycles, bool asProject = false)
    {
        var l = new Losses();
        var startBodies = Bodies(start);
        // What only the project file holds (views, sheets, standards: Lapin's classes with no body) is not IFC's to carry.
        bool Carried(string cls) => cls.StartsWith("Ifc", StringComparison.Ordinal) || start.Session.Catalog.Geometry(cls) != null;
        var startCounts = start.Model.Entities.Where(e => Carried(e.Class)).GroupBy(e => e.Class).ToDictionary(g => g.Key, g => g.Count());
        var startRelations = start.Model.Relations.GroupBy(r => r.Class).ToDictionary(g => g.Key, g => g.Count());
        var path = Path.Combine(_dir, "cycle-0.ifc");
        IfcExport.Write(start.Session, path);
        var first = Read(path);
        var clock = System.Diagnostics.Stopwatch.StartNew();
        for (var cycle = 1; cycle <= cycles; cycle++)
        {
            var g = make();
            var r = asProject ? IfcImport.Open(g.Session, StepFile.Read(path), Path.GetFileName(path), null, out _) : IfcImport.Run(g.Session, path, null, out _);
            if (r.References > 0) l.Add(cycle, "references", $"{r.References} came in as references: {string.Join("; ", r.Notes)}");
            var next = Path.Combine(_dir, $"cycle-{cycle}.ifc");
            IfcExport.Write(g.Session, next);
            File.Delete(path);
            path = next;

            var now = Read(path);
            Compare(l, cycle, "entity counts", first.Counts, now.Counts);
            Compare(l, cycle, "rooted counts", first.RootsByClass, now.RootsByClass);
            Compare(l, cycle, "objects (GlobalId → class, name)", first.Objects, now.Objects);
            Compare(l, cycle, "spatial structure (by name)", first.Spatial, now.Spatial);
            Compare(l, cycle, "spatial GlobalIds", first.SpatialIds, now.SpatialIds);
            Compare(l, cycle, "root GlobalIds (relationships, sets)", first.Others, now.Others);
            Compare(l, cycle, "storey containment", first.Containment, now.Containment);
            Compare(l, cycle, "aggregation depth", first.Depth, now.Depth);
            Compare(l, cycle, "type assignments", first.Types, now.Types);
            Compare(l, cycle, "voids", first.Voids, now.Voids);
            Compare(l, cycle, "fills", first.Fills, now.Fills);
            Compare(l, cycle, "layer sets", first.LayerSets, now.LayerSets);
            Compare(l, cycle, "material usages", first.Usages, now.Usages);

            Compare(l, cycle, "model classes", startCounts, g.Model.Entities.Where(e => Carried(e.Class)).GroupBy(e => e.Class).ToDictionary(x => x.Key, x => x.Count()));
            Compare(l, cycle, "model relations", startRelations, g.Model.Relations.GroupBy(x => x.Class).ToDictionary(x => x.Key, x => x.Count()));
            var bodies = Bodies(g);
            foreach (var (id, a) in startBodies)
            {
                if (!bodies.TryGetValue(id, out var b)) { l.Add(cycle, "elements", $"{a.Class} {id} gone"); continue; }
                if (a.Attrs != b.Attrs) l.Add(cycle, "attributes", $"{a.Class} {id}: {a.Attrs} → {b.Attrs}");
                var d = Math.Max(Drift(a.Min, b.Min), Drift(a.Max, b.Max));
                var v = Math.Abs(a.Volume - b.Volume);
                l.MaxPointDrift = Math.Max(l.MaxPointDrift, d);
                l.MaxVolumeDrift = Math.Max(l.MaxVolumeDrift, v);
                if (d > 0) l.Add(cycle, "bounds", $"{a.Class} {id} moved {d:E2} m");
                if (v > 0) l.Add(cycle, "volumes", $"{a.Class} {id} {a.Volume} → {b.Volume} m³");
            }
            foreach (var id in bodies.Keys.Except(startBodies.Keys)) l.Add(cycle, "elements", $"{bodies[id].Class} {id} new");
        }
        output.WriteLine($"{cycles} cycles in {clock.Elapsed.TotalSeconds:0.0} s; max drift: bounds {l.MaxPointDrift:E3} m, volume {l.MaxVolumeDrift:E3} m³; first loss in cycle {l.FirstCycleWithLoss}");
        foreach (var (kind, list) in l.ByKind) { output.WriteLine($"— {kind}"); foreach (var x in list) output.WriteLine("    " + x); }
        var dump = Environment.GetEnvironmentVariable("LAPIN_IFC_CYCLES_REPORT");
        if (!string.IsNullOrEmpty(dump))
            File.AppendAllLines(dump, [$"{cycles} cycles of {startBodies.Count} elements, {first.Counts.Values.Sum()} file entities; max drift: bounds {l.MaxPointDrift:E3} m, volume {l.MaxVolumeDrift:E3} m³", .. l.ByKind.SelectMany(kv => kv.Value.Select(x => $"{kv.Key}: {x}"))]);
        return l;
    }

    // The kinds that must not change, cycle after cycle. The GlobalIds of the spatial structure and of the
    // relationships and sets Lapin derives from it change when a file is imported into a project (the project's own
    // stay); opened as a project they are kept, asserted apart below.
    static readonly string[] Invariants =
    [
        "references", "entity counts", "rooted counts", "objects (GlobalId → class, name)", "spatial structure (by name)", "storey containment",
        "aggregation depth", "type assignments", "voids", "fills", "layer sets", "material usages", "model classes", "model relations",
        "elements", "attributes", "bounds", "volumes",
    ];

    [Fact]
    public void Exported_and_imported_over_and_over_the_model_stays_what_it_was()
    {
        var l = Run(House(), Make, Cycles);
        foreach (var kind in Invariants) Assert.True(l.Of(kind).Count == 0, $"{kind}:\n{string.Join("\n", l.Of(kind))}");
        Assert.Equal(0, l.MaxPointDrift);
        Assert.Equal(0, l.MaxVolumeDrift);
    }

    /// <summary>
    /// A file opened as a project (docs/spec-ifc.md, Structura spațială) is the project it was: its IfcProject, IfcSite, IfcBuilding and
    /// storeys keep their GlobalIds, and so do the roots Lapin derives from them (the IfcRelAggregates, the project's and the storeys'
    /// Lapin_Intent with their IfcRelDefinesByProperties, the storeys' IfcRelContainedInSpatialStructure), cycle after cycle, with
    /// everything else the cycles keep. Imported into a project instead, the project's own project, site, building and matching storeys
    /// stay (<see cref="IfcSpatialIdentityTests"/>).
    /// </summary>
    [Fact]
    public void The_spatial_structure_keeps_its_GlobalIds()
    {
        var l = Run(House(), Make, Cycles, asProject: true);
        Assert.True(l.Of("spatial GlobalIds").Count == 0, string.Join("\n", l.Of("spatial GlobalIds")));
        Assert.True(l.Of("root GlobalIds (relationships, sets)").Count == 0, string.Join("\n", l.Of("root GlobalIds (relationships, sets)")));
        foreach (var kind in Invariants) Assert.True(l.Of(kind).Count == 0, $"{kind}:\n{string.Join("\n", l.Of(kind))}");
        Assert.Equal(0, l.MaxPointDrift);
        Assert.Equal(0, l.MaxVolumeDrift);
    }

    /// <summary>The test model (or any .lapin) cycled the same way, when one is given: LAPIN_IFC_CYCLES_MODEL=/path/project.lapin.</summary>
    [Fact]
    public void A_given_project_cycles_too()
    {
        var path = Environment.GetEnvironmentVariable("LAPIN_IFC_CYCLES_MODEL");
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
        static Fixture All() => new(new ArchitectureModule(), new StructureModule(), new Lapin.Drafting.DraftingModule(), new Lapin.Civil.CivilModule(),
            new Lapin.Phases.PhasesModule(), new Lapin.Studio.StudioModule(), new Lapin.Steel.SteelModule(), new Lapin.Mep.MepModule(), new Lapin.Wood.WoodModule(),
            new Lapin.Rebar.RebarModule(), new Lapin.SiteWorks.SiteWorksModule(), new Lapin.Furniture.FurnitureModule());
        var f = All();
        Lapin.Host.PackageFiles.Open(f.Session, path);
        var l = Run(f, All, Cycles);
        foreach (var kind in Invariants) Assert.True(l.Of(kind).Count == 0, $"{kind}:\n{string.Join("\n", l.Of(kind))}");
    }
}
