using System.Diagnostics;
using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Civil;
using Lapin.Drafting;
using Lapin.Furniture;
using Lapin.Host;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Protocol;
using Lapin.Steel;
using Lapin.Structure;
using Lapin.Studio;
using Lapin.Wood;

namespace Lapin.Engine.Tests;

/// <summary>
/// What a session keeps of its builds (<see cref="SessionTuning"/>, docs/experiment-editing-speed.md): its results against the session as it
/// was (<see cref="SessionTuning.Reference"/>), and the editing benchmark. Both are off unless an environment variable names what to run on,
/// like <c>LAPIN_IFC_BENCH</c> (run in Release); the models are made by <see cref="Generate_when_given"/> under a folder outside the repository.
/// <code>
/// LAPIN_EDIT_BENCH_GEN=folder [LAPIN_EDIT_BENCH_IFC=Bloc-P-4.ifc]    make gen-small, gen-10 and bloc-p4 .lapin files there
/// LAPIN_EDIT_BENCH_CHECK=name=path.lapin,…                           the same edits with the kept builds and without: everything equal
/// LAPIN_EDIT_BENCH=samples.jsonl LAPIN_EDIT_BENCH_MODELS=name=path,… the benchmark; LAPIN_EDIT_BENCH_LABEL, LAPIN_EDIT_BENCH_TUNING=reference|default,
///                                                                     LAPIN_EDIT_BENCH_ONLY=edit,…, LAPIN_EDIT_BENCH_TYPEREPS=n
/// </code>
/// </summary>
public class EditingSpeedTests(Xunit.Abstractions.ITestOutputHelper output)
{
    static ILapinModule[] Modules() =>
    [
        new ArchitectureModule(), new StructureModule(), new DraftingModule(), new CivilModule(), new Lapin.Phases.PhasesModule(), new StudioModule(),
        new WoodModule(), new SteelModule(), new FurnitureModule(), new Lapin.Mep.MepModule(), new Lapin.Rebar.RebarModule(), new Lapin.SiteWorks.SiteWorksModule(),
        new Lapin.Planning.PlanningModule(), new Lapin.Cost.CostModule(), new Lapin.Transport.TransportModule(),
    ];

    /// <summary>"reference", "default", or the default with parts changed: <c>memo=0,release=off,planes=keep,cutters=off</c>.</summary>
    static SessionTuning TuningFrom(string? text)
    {
        var t = text?.StartsWith("reference", StringComparison.Ordinal) == true ? SessionTuning.Reference : SessionTuning.Default;
        foreach (var part in (text ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries))
            t = part switch
            {
                "memo=0" => t with { MemoBytes = 0 },
                "release=off" => t with { ReleaseAboveBytes = long.MaxValue },
                "planes=keep" => t with { KeepSeamPlanes = true },
                "cutters=off" => t with { CacheCutters = false },
                "release=300m" => t with { ReleaseAboveBytes = 300L << 20 },
                "memo=1m" => t with { MemoBytes = 1L << 20 },
                "memo=16m" => t with { MemoBytes = 16L << 20 },
                "memo=on" => t with { MemoBytes = SessionTuning.Default.MemoBytes, MemoFromEntities = 0 },
                "cutters=on" => t with { CacheCutters = true },
                "planes=trim" => t with { KeepSeamPlanes = false },
                _ => t,
            };
        return t;
    }

    static Session NewSession(SessionTuning tuning) =>
        new(ModuleCatalog.Create(Modules()), new ProductManifest("bench", "Bench", ["core"])) { Tuning = tuning };

    /// <summary>Releases at once and keeps little: every path (a build taken again, a storey built again) is taken on a small model.</summary>
    static readonly SessionTuning Squeezed = new(2L << 20, 1L << 20, false, true, MemoFromEntities: 0);

    void Log(string line)
    {
        Console.WriteLine(line);
        output.WriteLine(line);
    }

    // ------------------------------------------------------------ the models

    [Fact]
    public void Generate_when_given()
    {
        var folder = Environment.GetEnvironmentVariable("LAPIN_EDIT_BENCH_GEN");
        if (string.IsNullOrEmpty(folder)) return;
        Directory.CreateDirectory(folder);
        foreach (var (name, storeys, bx, by) in new[] { ("gen-small", 3, 8, 6), ("gen-10", 10, 20, 14) })
        {
            var path = Path.Combine(folder, name + ".lapin");
            if (File.Exists(path)) continue;
            var f = new Fixture(Modules());
            Generated(f, storeys, bx, by);
            PackageFiles.Save(f.Session, path);
            Log($"{name}: {f.Session.Model.Entities.Count():N0} entities, {f.Session.Model.OfClass(IfcClass.Wall).Count():N0} walls");
        }
        var ifc = Environment.GetEnvironmentVariable("LAPIN_EDIT_BENCH_IFC");
        var bloc = Path.Combine(folder, "bloc-p4.lapin");
        if (!string.IsNullOrEmpty(ifc) && File.Exists(ifc) && !File.Exists(bloc))
        {
            var f = new Fixture(Modules());
            f.Session.New(plain: true);
            IfcImport.Run(f.Session, ifc, null, out _);
            PackageFiles.Save(f.Session, bloc);
            Log($"bloc-p4: {f.Session.Model.Entities.Count():N0} entities, {f.Session.Model.OfClass(IfcClass.Wall).Count():N0} walls");
        }
    }

    /// <summary>A block of flats: on each storey a slab, an outer ring in a zoned type, rows of partitions and cross walls (corners, T joins, nodes), and a door or window in nearly every wall.</summary>
    static void Generated(Fixture f, int storeys, int bx, int by)
    {
        const double W = 4, H = 5, Up = 3;
        f.Session.New();
        JsonNode Layer(string material, double t, bool core = false) => new JsonObject { ["material"] = material, ["thickness"] = t, ["core"] = core };
        JsonNode Pt(double x, double y) => new JsonArray(x, y);
        string Type(string name, JsonArray layers, JsonArray? zones = null)
        {
            var a = new JsonObject { ["name"] = name, ["layers"] = layers };
            if (zones != null) a["zones"] = zones;
            return (string)f.Run("architecture.createWallType", a)!["id"]!;
        }
        var outer = Type("Outer", new(Layer("Plaster", 0.015), Layer("Brick", 0.25, true), Layer("Insulation", 0.1), Layer("Render", 0.01)), new(WallZones.Defaults("Brick").Select(z => z.DeepClone()).ToArray()));
        var inner = Type("Partition", new(Layer("Plaster", 0.015), Layer("Brick", 0.115, true), Layer("Plaster", 0.015)));
        var party = Type("Party", new(Layer("Plaster", 0.015), Layer("Concrete", 0.2, true), Layer("Plaster", 0.015)));
        var levels = Spatial.Storeys(f.Model).Select(s => s.Id).ToList();
        for (var i = levels.Count; i < storeys; i++)
            levels.Add((string)f.Run("storey.create", new JsonObject { ["name"] = $"Storey {i}", ["elevation"] = i * Up, ["height"] = Up })!["id"]!);
        for (var i = 0; i < storeys; i++)
            f.Run("storey.update", new JsonObject { ["id"] = levels[i], ["elevation"] = i * Up, ["height"] = Up });
        foreach (var st in levels)
        {
            f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = st, ["points"] = new JsonArray(Pt(0, 0), Pt(bx * W, 0), Pt(bx * W, by * H), Pt(0, by * H)) });
            string Wall(double x1, double y1, double x2, double y2, string type) =>
                (string)f.Run("architecture.addWall", new JsonObject { ["storeyId"] = st, ["x1"] = x1, ["y1"] = y1, ["x2"] = x2, ["y2"] = y2, ["typeId"] = type, ["height"] = Up - 0.2 })!["id"]!;
            for (var j = 0; j <= by; j++)
                for (var i = 0; i + 2 <= bx; i += 2)
                {
                    var edge = j == 0 || j == by;
                    var w = Wall(i * W, j * H, (i + 2) * W, j * H, edge ? outer : j % 4 == 0 ? party : inner);
                    foreach (var along in new[] { 2.0, 6.0 })
                        f.Run(edge ? "architecture.addWindow" : "architecture.addDoor", new JsonObject { ["wallId"] = w, ["along"] = along });
                }
            for (var i = 0; i <= bx; i++)
                for (var j = 0; j < by; j++)
                {
                    var edge = i == 0 || i == bx;
                    var w = Wall(i * W, j * H, i * W, (j + 1) * H, edge ? outer : i % 6 == 0 ? party : inner);
                    if (edge) f.Run("architecture.addWindow", new JsonObject { ["wallId"] = w, ["along"] = H / 2 });
                    else if ((i + j) % 2 == 0) f.Run("architecture.addDoor", new JsonObject { ["wallId"] = w, ["along"] = H / 2 });
                }
        }
    }

    // ------------------------------------------------------------ the edits, picked from the model by rule

    sealed record Targets(List<(string Id, double Along)> Fills, List<string> Drag, string? TypeId)
    {
        public static Targets Of(IModelView m)
        {
            var walls = m.OfClass(IfcClass.Wall).ToList();
            var fills = new List<(string, double)>();
            foreach (var w in walls.Where(w => m.RelationsOf(w.Id, IfcClass.RelConnectsPathElements).Any()))
            {
                var length = (Walls.End(w) - Walls.Start(w)).Length;
                var placed = Openings.PlacedOn(m, w.Id).ToList();
                if (placed.Count != 1 || placed[0].Fill is not { } fill) continue;
                if (placed[0].Along - placed[0].Width / 2 < 0.2 || placed[0].Along + placed[0].Width / 2 > length - 0.2) continue;
                fills.Add((fill.Id, placed[0].Along));
            }
            var joined = walls.Where(w => m.RelationsOf(w.Id, IfcClass.RelConnectsPathElements).Count() >= 2 && PathCurve.Of(w) is not { IsArc: true }).Select(w => w.Id).ToList();
            var type = walls.Select(w => Walls.TypeOf(m, w.Id)).OfType<Entity>().GroupBy(t => t.Id).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).FirstOrDefault()?.Key;
            return new Targets(Spread(fills, 8), Spread(joined, 8), type);
        }

        static List<T> Spread<T>(List<T> all, int n) => all.Count <= n ? all : [.. Enumerable.Range(0, n).Select(i => all[(int)((i + 0.5) * all.Count / n)])];
    }

    /// <summary>The edits of the benchmark and the check: <paramref name="r"/> makes each repetition's values differ, so none is the same edit twice.</summary>
    static IEnumerable<(string Name, Func<IModelView, (string Command, JsonObject Args)> Make)> Edits(Targets t, int r)
    {
        if (t.Fills.Count > 0)
        {
            var (fill, along) = t.Fills[r % t.Fills.Count];
            yield return ("moveWindow", _ => ("architecture.moveOpening", new JsonObject { ["id"] = fill, ["along"] = along + (r % 2 == 0 ? 0.05 : -0.05) + 0.0005 * r }));
        }
        if (t.Drag.Count > 0)
        {
            var id = t.Drag[r % t.Drag.Count];
            yield return ("wallHeight", m => ("architecture.updateWall", new JsonObject { ["id"] = id, ["height"] = m.Get(id)!.Num("height", 3) + 0.1 + 0.0005 * r }));
            yield return ("dragWallEnd", m =>
            {
                var w = m.Get(id)!;
                var to = Walls.End(w) + (Walls.End(w) - Walls.Start(w)).Normalized() * (0.02 + 0.001 * r);
                return ("architecture.updateWall", new JsonObject { ["id"] = id, ["x2"] = to.X, ["y2"] = to.Y });
            });
        }
        if (t.TypeId is { } type)
            yield return ("wallTypeCore", m =>
            {
                var layers = LayerSets.LayersOf(m, type).Select(l => LayerSets.SpecOf(m, l)).ToList();
                var core = layers.FirstOrDefault(l => l.Core) ?? layers.MaxBy(l => l.Thickness)!;
                return ("architecture.updateWallType", new JsonObject
                {
                    ["id"] = type,
                    ["layers"] = new JsonArray(layers.Select(l => (JsonNode)new JsonObject
                    {
                        ["material"] = l.Material, ["core"] = l.Core, ["color"] = l.Color, ["name"] = l.Name,
                        ["thickness"] = ReferenceEquals(l, core) ? l.Thickness + 0.01 * (1 + r % 2) + 0.0005 * r : l.Thickness,
                    }).ToArray()),
                });
            });
    }

    // ------------------------------------------------------------ the kept builds change nothing

    /// <summary>A 128-bit print of everything a render item says, as a replica would hold it.</summary>
    static (ulong, ulong) PrintOf(RenderItem r)
    {
        ulong a = 0x9E3779B97F4A7C15UL, b = 0xC2B2AE3D27D4EB4FUL;
        void Add(ulong v) { a = (a ^ v) * 0xFF51AFD7ED558CCDUL; a ^= a >> 33; b = (b + v) * 0xC4CEB9FE1A85EC53UL; b ^= b >> 29; }
        void Text(string? s) { Add((ulong)(s?.Length ?? -1)); foreach (var c in s ?? "") Add(c); }
        void Array<T>(T[]? v) where T : unmanaged
        {
            Add((ulong)(v?.Length ?? -1));
            if (v == null) return;
            var bytes = System.Runtime.InteropServices.MemoryMarshal.AsBytes(v.AsSpan());
            for (var i = 0; i + 8 <= bytes.Length; i += 8) Add(BitConverter.ToUInt64(bytes.Slice(i, 8)));
            if (bytes.Length % 8 != 0) Add(BitConverter.ToUInt32(bytes[^4..]));
        }
        Text(r.NodeId); Text(r.Type); Text(r.StoreyId); Add(r.Editable ? 1UL : 0); Text(r.Color);
        Array(r.Positions); Array(r.Normals); Array(r.Indices); Array(r.Edges); Array(r.Snaps); Array(r.Lines);
        foreach (var d in r.Diagnostics) Text(d);
        foreach (var i in r.Plan) { Text(i.Kind); Add(i.Closed ? 1UL : 0); Array(i.Points); Text(i.Text); Add((ulong)(long)(i.MinDetail ?? -1)); Add((ulong)(long)(i.MaxDetail ?? -1)); Text(i.Pattern); }
        foreach (var p in r.Parts ?? [])
        {
            Text(p.Name); Text(p.Color); Text(p.Material); Text(p.Key);
            Array(p.Positions); Array(p.Normals); Array(p.Indices); Array(p.Edges);
            foreach (var x in p.HiddenBy ?? []) Text(x);
        }
        if (r.Instance is { } n) { Text(n.Mesh); Array(n.Matrix); }
        return (a, b);
    }

    /// <summary>What each element's geometry is, through the session's public reads: parts, solids' vertices and faces, plan items, diagnostics.</summary>
    static Dictionary<string, (ulong, ulong)> GeometryPrints(Session s)
    {
        var o = new Dictionary<string, (ulong, ulong)>();
        foreach (var e in s.Model.Entities)
        {
            if (s.Geometry(e.Id) is not { } g) continue;
            ulong a = 0x9E3779B97F4A7C15UL, b = 0xC2B2AE3D27D4EB4FUL;
            void Add(ulong v) { a = (a ^ v) * 0xFF51AFD7ED558CCDUL; a ^= a >> 33; b = (b + v) * 0xC4CEB9FE1A85EC53UL; b ^= b >> 29; }
            void Text(string? t) { Add((ulong)(t?.Length ?? -1)); foreach (var c in t ?? "") Add(c); }
            void Ints(int[] v) { Add((ulong)v.Length); foreach (var i in v) Add((ulong)i); }
            foreach (var p in g.Parts)
            {
                Text(p.Name); Text(p.Color); Text(p.Material); Text(p.Key);
                if (p.Mesh is { } m) { foreach (var d in m.Xyz) Add(BitConverter.DoubleToUInt64Bits(d)); Ints(m.Tris); continue; }
                foreach (var v in p.Solid.Vertices) { Add(BitConverter.DoubleToUInt64Bits(v.X)); Add(BitConverter.DoubleToUInt64Bits(v.Y)); Add(BitConverter.DoubleToUInt64Bits(v.Z)); }
                foreach (var f in p.Solid.Faces) { Ints(f.Outer); foreach (var h in f.Holes) Ints(h); Text(f.Tag); }
            }
            foreach (var i in g.Plan ?? []) { Text(i.Kind); Add(i.Closed ? 1UL : 0); foreach (var v in i.Points) { Add(BitConverter.DoubleToUInt64Bits(v.X)); Add(BitConverter.DoubleToUInt64Bits(v.Y)); } }
            foreach (var d in g.Diagnostics ?? []) Text(d);
            o[e.Id] = (a, b);
        }
        return o;
    }

    static Dictionary<string, (ulong, ulong)> Replica(Diff d, Dictionary<string, (ulong, ulong)>? into = null)
    {
        var r = into ?? [];
        if (d.Reset) r.Clear();
        foreach (var id in d.RenderRemoved) r.Remove(id);
        foreach (var item in d.Render) r[item.NodeId] = PrintOf(item);
        return r;
    }

    /// <summary>
    /// The same edits, each undone and done again, then all undone and all done again, on a session that keeps builds and lets storeys go
    /// (the defaults, and a squeezed one that does both on any model) and one as it was before (<see cref="SessionTuning.Reference"/>): after every
    /// step the geometry of every element and the interface's copy of every render item (fed by the diffs alone) must be equal, and once more
    /// after a whole snapshot.
    /// </summary>
    [Fact]
    public void Kept_builds_change_nothing_when_given()
    {
        var models = Environment.GetEnvironmentVariable("LAPIN_EDIT_BENCH_CHECK");
        if (string.IsNullOrEmpty(models)) return;
        foreach (var entry in models.Split(',', StringSplitOptions.RemoveEmptyEntries))
            CheckSame(entry.Split('=')[0], entry.Split('=')[1]);
    }

    void CheckSame(string name, string path)
    {
        {
            foreach (var (label, tuning) in new[] { ("default", SessionTuning.Desktop), ("kept", SessionTuning.Desktop with { MemoFromEntities = 0 }), ("squeezed", Squeezed) })
            {
                var reference = NewSession(SessionTuning.Reference);
                var kept = NewSession(tuning);
                var referenceReplica = Replica(PackageFiles.Open(reference, path));
                if (label == "kept" && reference.Model.Entities.Count() >= SessionTuning.Desktop.MemoFromEntities) continue; // (the default keeps builds there already)
                var keptReplica = Replica(PackageFiles.Open(kept, path));
                var steps = 0;
                void Same(string what)
                {
                    var (a, b) = (GeometryPrints(reference), GeometryPrints(kept));
                    Assert.True(a.Count == b.Count, $"{name}/{label}, {what}: {a.Count} elements without the kept builds, {b.Count} with them");
                    foreach (var (id, x) in a)
                        Assert.True(b.TryGetValue(id, out var y) && x == y, $"{name}/{label}, {what}: the geometry of {id} ({reference.Model.Get(id)?.Class}) differs");
                    Assert.Equal(referenceReplica.Count, keptReplica.Count);
                    foreach (var (id, x) in referenceReplica)
                        Assert.True(keptReplica.TryGetValue(id, out var y) && x == y, $"{name}/{label}, {what}: the interface's copy of {id} ({reference.Model.Get(id)?.Class}) differs");
                    steps++;
                }
                void Both(string what, Func<Session, Diff> edit)
                {
                    Replica(edit(reference), referenceReplica);
                    Replica(edit(kept), keptReplica);
                    Same(what);
                }
                Same("open");
                var targets = Targets.Of(reference.Model);
                var edits = Edits(targets, 0).Concat(Edits(targets, 1)).ToList();
                foreach (var (what, make) in edits)
                {
                    Both(what, s => { var (c, args) = make(s.Model); return s.Execute(c, args, out _); });
                    Both("undo " + what, s => s.Undo());
                    Both("redo " + what, s => s.Redo());
                }
                for (var i = 0; i < edits.Count; i++) Both($"undo all {i}", s => s.Undo());
                for (var i = 0; i < edits.Count; i++) Both($"redo all {i}", s => s.Redo());
                Replica(reference.Snapshot(), referenceReplica);
                Replica(kept.Snapshot(), keptReplica);
                Same("snapshot");
                Log($"{name}/{label}: {steps} comparisons of {GeometryPrints(reference).Count:N0} elements after {edits.Count} edits: identical");
            }
        }
    }

    /// <summary>The same check on a small block of flats made here, so it runs with every build.</summary>
    [Fact]
    public void Kept_builds_change_nothing_on_a_small_model()
    {
        var dir = Directory.CreateTempSubdirectory("lapin-kept-").FullName;
        try
        {
            var f = new Fixture(Modules());
            Generated(f, 2, 4, 2);
            var path = Path.Combine(dir, "small.lapin");
            PackageFiles.Save(f.Session, path);
            CheckSame("small", path);
        }
        finally { Directory.Delete(dir, true); }
    }

    /// <summary>An undo and a redo build nothing: the element is as it was, and the session knows it; a value set back by hand is a new entity and builds again.</summary>
    [Fact]
    public void Undo_and_redo_take_the_builds_they_had_and_a_new_object_does_not()
    {
        var f = new Fixture(Modules());
        Generated(f, 1, 4, 2);
        var s = f.Session;
        s.Tuning = SessionTuning.Desktop with { MemoFromEntities = 0 };
        s.Snapshot();
        var wall = Targets.Of(s.Model).Drag[0];
        var before = GeometryPrints(s)[wall];
        s.Execute("architecture.updateWall", new JsonObject { ["id"] = wall, ["height"] = 2.5 }, out _);
        var edited = GeometryPrints(s)[wall];
        Assert.NotEqual(before, edited);
        var made = s.BuildsMade;
        var undone = s.Undo();
        Assert.Equal(made, s.BuildsMade);
        Assert.Equal(before, GeometryPrints(s)[wall]);
        Assert.Contains(undone.Render, r => r.NodeId == wall);
        s.Redo();
        Assert.Equal(made, s.BuildsMade);
        Assert.Equal(edited, GeometryPrints(s)[wall]);
        // The same height typed again is a new object: not taken for the old (identical results, built again).
        s.Undo();
        s.Execute("architecture.updateWall", new JsonObject { ["id"] = wall, ["height"] = 2.5 }, out _);
        Assert.True(s.BuildsMade > made);
        Assert.Equal(edited, GeometryPrints(s)[wall]);
    }

    /// <summary>
    /// What an earlier build is found by: the stamp of what it read is the same exactly when the objects are the same. Every edit's state has a
    /// stamp of its own, an undo and a redo give back the stamp they had, and a different edit from the same place gives another.
    /// </summary>
    [Fact]
    public void Stamps_tell_states_apart_and_come_back_with_undo()
    {
        var f = new Fixture(Modules());
        Generated(f, 1, 8, 6);
        var s = f.Session;
        var store = (ModelStore)s.Model;
        var walls = s.Model.OfClass(IfcClass.Wall).Select(w => w.Id).Take(120).ToList();
        string[] keys = ["class:IfcWall", "cut:IfcWall", "rel:" + walls[0], "rel:" + Spatial.Storeys(s.Model).First().Id, walls[0], walls[1]];
        (ulong, ulong)?[] Stamps() => keys.Select(store.ReadStamp).ToArray();
        var seen = new HashSet<string>();
        string Key((ulong, ulong)?[] st) => string.Join("|", st.Select(x => x?.ToString()));
        var states = new List<(ulong, ulong)?[]> { Stamps() };
        seen.Add(Key(states[0]));
        foreach (var w in walls)
        {
            s.Execute("architecture.updateWall", new JsonObject { ["id"] = w, ["height"] = s.Model.Get(w)!.Num("height", 3) + 0.1 }, out _);
            var now = Stamps();
            Assert.True(seen.Add(Key(now)), "two different states gave the same stamps");
            states.Add(now);
        }
        for (var i = states.Count - 1; i > 0; i--) { s.Undo(); Assert.Equal(Key(states[i - 1]), Key(Stamps())); }
        for (var i = 1; i < states.Count; i++) { s.Redo(); Assert.Equal(Key(states[i]), Key(Stamps())); }
        // From the base: the same value typed over another wall is another state, and the same value over the same wall is a new object, so another too.
        for (var i = 0; i < states.Count - 1; i++) s.Undo();
        s.Execute("architecture.updateWall", new JsonObject { ["id"] = walls[0], ["height"] = s.Model.Get(walls[0])!.Num("height", 3) + 0.1 }, out _);
        Assert.True(seen.Add(Key(Stamps())), "the same edit typed again gave the stamps of the one it replaced");
    }

    /// <summary>The earlier builds are kept to a budget: past it the oldest go, and what is asked for after that is built again, the same.</summary>
    [Fact]
    public void The_earlier_builds_are_kept_to_their_budget()
    {
        var f = new Fixture(Modules()) ;
        Generated(f, 1, 4, 2);
        var s = f.Session;
        s.Tuning = new SessionTuning(64L << 10, long.MaxValue, false, true, MemoFromEntities: 0);
        s.Snapshot();
        var type = Targets.Of(s.Model).TypeId!;
        foreach (var (_, make) in Edits(Targets.Of(s.Model), 0).Where(e => e.Name == "wallTypeCore")) { var (c, a) = make(s.Model); s.Execute(c, a, out _); }
        Assert.InRange(s.HeldBytes.Earlier, 1, 64L << 10);
        var reference = NewSession(SessionTuning.Reference);
        Assert.NotNull(type);
        reference.Load(s.Model.Entities, s.Model.Relations, null);
        var kept = GeometryPrints(s);
        s.Undo();
        reference.Load(s.Model.Entities, s.Model.Relations, null);
        Assert.Equal(GeometryPrints(reference), GeometryPrints(s));
        Assert.NotEmpty(kept);
    }

    /// <summary>A large model lets the geometry of storeys nobody works on go, and builds it again, the same, when asked.</summary>
    [Fact]
    public void Storeys_nobody_works_on_are_let_go_and_built_again_when_asked_for()
    {
        var f = new Fixture(Modules());
        Generated(f, 3, 4, 2);
        var s = f.Session;
        s.Tuning = SessionTuning.Reference;
        s.Snapshot();
        var whole = GeometryPrints(s);
        s.Tuning = Squeezed;
        var snapshot = s.Snapshot();
        Assert.True(s.ReleasedBuilds > 0, "nothing was let go");
        Assert.True(s.HeldBytes.Live <= Squeezed.ReleaseAboveBytes, $"{s.HeldBytes.Live} bytes held");
        // Everything asked for is there again, as it was.
        Assert.Equal(whole, GeometryPrints(s));
        // An edit beside released neighbours makes the same items as without releasing.
        s.Snapshot();
        var reference = NewSession(SessionTuning.Reference);
        reference.Load(s.Model.Entities, s.Model.Relations, null);
        var (c, a) = Edits(Targets.Of(s.Model), 0).First(e => e.Name == "moveWindow").Make(s.Model);
        var (x, y) = (s.Execute(c, a, out _), reference.Execute(c, a, out _));
        Assert.Equal(y.Render.Select(r => PrintOf(r)).Order().ToList(), x.Render.Select(r => PrintOf(r)).Order().ToList());
        Assert.NotNull(snapshot);
    }

    // ------------------------------------------------------------ the benchmark

    [Fact]
    public void Benchmark_when_given()
    {
        var outFile = Environment.GetEnvironmentVariable("LAPIN_EDIT_BENCH");
        var models = Environment.GetEnvironmentVariable("LAPIN_EDIT_BENCH_MODELS");
        if (string.IsNullOrEmpty(outFile) || string.IsNullOrEmpty(models)) return;
        var label = Environment.GetEnvironmentVariable("LAPIN_EDIT_BENCH_LABEL") ?? "default";
        var tuning = TuningFrom(Environment.GetEnvironmentVariable("LAPIN_EDIT_BENCH_TUNING"));
        var only = Environment.GetEnvironmentVariable("LAPIN_EDIT_BENCH_ONLY")?.Split(',');
        var typeReps = int.Parse(Environment.GetEnvironmentVariable("LAPIN_EDIT_BENCH_TYPEREPS") ?? "5", CultureInfo.InvariantCulture);
        foreach (var entry in models.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var (name, path) = (entry.Split('=')[0], entry.Split('=')[1]);
            void Write(string kind, params (string, object)[] fields)
            {
                var o = new JsonObject { ["cfg"] = label, ["model"] = name, ["kind"] = kind };
                foreach (var (k, v) in fields) o[k] = v switch { double d => Math.Round(d, 3), int i => i, long l => l, bool b => b, _ => v.ToString() };
                File.AppendAllText(outFile, o.ToJsonString() + "\n");
            }
            static double Ms(long from) => (Stopwatch.GetTimestamp() - from) * 1000.0 / Stopwatch.Frequency;
            static void Settle() { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); }

            var dir = Directory.CreateTempSubdirectory("lapin-speed-").FullName;
            try
            {
                var copy = Path.Combine(dir, Path.GetFileName(path));
                File.Copy(path, copy);
                var session = NewSession(tuning);
                using var journal = new Recovery(dir, session);
                var d = new EngineDispatcher(session, PackageFiles.Create(), journal);
                for (var rep = 0; rep < 3; rep++)
                {
                    Settle();
                    var (t0, a0) = (Stopwatch.GetTimestamp(), GC.GetTotalAllocatedBytes(true));
                    var snapshot = PackageFiles.Open(session, copy);
                    var open = Ms(t0);
                    Write("open", ("rep", rep), ("ms", open), ("allocMB", (GC.GetTotalAllocatedBytes(true) - a0) / 1048576.0), ("frameBytes", d.DiffFrame(snapshot).LongLength));
                }
                Settle();
                var held = session.HeldBytes;
                Write("memory", ("managedMB", GC.GetTotalMemory(true) / 1048576.0), ("workingSetMB", Environment.WorkingSet / 1048576.0), ("entities", session.Model.Entities.Count()),
                    ("estimateLiveMB", held.Live / 1048576.0), ("released", session.ReleasedBuilds));
                var targets = Targets.Of(session.Model);
                var id = 0;
                (byte[] Reply, Diff? Diff) Call(string method, JsonObject? p = null) =>
                    d.Handle(new JsonObject { ["id"] = ++id, ["method"] = method, ["params"] = p ?? [] }.ToJsonString());
                void Measure(string edit, string phase, int rep, Func<(byte[] Reply, Diff? Diff)> call)
                {
                    var collections = GC.CollectionCount(2);
                    var a0 = GC.GetTotalAllocatedBytes(true);
                    var t0 = Stopwatch.GetTimestamp();
                    var (_, diff) = call();
                    var frame = diff != null ? d.DiffFrame(diff).LongLength : 0;
                    Write("edit", ("edit", edit), ("phase", phase), ("rep", rep), ("ms", Ms(t0)), ("allocMB", (GC.GetTotalAllocatedBytes(true) - a0) / 1048576.0),
                        ("gen2", GC.CollectionCount(2) - collections), ("frameBytes", frame), ("render", diff?.Render.Count ?? 0));
                }
                foreach (var name2 in new[] { "moveWindow", "wallHeight", "dragWallEnd", "wallTypeCore" })
                {
                    if (only != null && !only.Contains(name2)) continue;
                    var reps = name2 == "wallTypeCore" ? typeReps : 12;
                    for (var r = 0; r < reps; r++)
                    {
                        if (Edits(targets, r).FirstOrDefault(e => e.Name == name2) is not { Make: { } make }) break;
                        var (command, args) = make(session.Model);
                        Measure(name2, "do", r, () => Call("doc.execute", new JsonObject { ["command"] = command, ["args"] = args }));
                        Measure(name2, "undo", r, () => Call("doc.undo"));
                        Measure(name2, "redo", r, () => Call("doc.redo"));
                        Call("doc.undo");
                    }
                }
                Settle();
                var heldAfter = session.HeldBytes;
                Write("memoryAfterEdits", ("managedMB", GC.GetTotalMemory(true) / 1048576.0), ("workingSetMB", Environment.WorkingSet / 1048576.0),
                    ("estimateLiveMB", heldAfter.Live / 1048576.0), ("estimateEarlierMB", heldAfter.Earlier / 1048576.0), ("released", session.ReleasedBuilds));
                var saved = Path.Combine(dir, "saved.lapin");
                for (var rep = 0; rep < 3; rep++)
                {
                    var t0 = Stopwatch.GetTimestamp();
                    PackageFiles.Save(session, saved);
                    Write("save", ("rep", rep), ("ms", Ms(t0)));
                }
            }
            finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
        }
    }
}
