using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Platform.Cde.Remarks;
using Lapin.Platform.Coordination;

namespace Lapin.Clash;

/// <summary>
/// Clash detection (docs/plan-visualisation.md, stage 2), free: search sets by rules (<see cref="SearchSets"/>), clash
/// tests between them (<see cref="ClashTests"/>) run by <see cref="ClashEngine"/> only when asked (decision 293's way: one
/// run, a list, then the fixing), their clashes kept from run to run, shown, given a status and made remarks (BCF).
/// Linked IFC and Fragments files take part like the model's own elements (they give remarks; they are not fixed here).
/// <para>
/// Pairs that are meant to meet are never tested: an element and what fills its openings (a wall and its door), the
/// members of one assembly and the assembly, elements a relation joins (walls at a corner, a connection and its
/// members), and elements whose attributes name each other (a beam fitted on a column, a wall attached to a roof).
/// </para>
/// </summary>
public sealed class ClashModule : ILapinModule
{
    public string Id => "clash";
    public string Name => "Clash detection";

    /// <summary>What never takes part: spaces, voids, annotations, grids and the spatial structure.</summary>
    static readonly HashSet<string> NotPhysical = [IfcClass.Space, IfcClass.OpeningElement, IfcClass.Annotation, "IfcGrid", "IfcVirtualElement", IfcClass.BuildingStorey, IfcClass.Site, IfcClass.Building, IfcClass.Project];
    /// <summary>The relations that say nothing about two elements meeting.</summary>
    static readonly HashSet<string> Unrelating = [IfcClass.RelContainedInSpatialStructure, IfcClass.RelDefinesByType, IfcClass.RelAssociatesMaterial, "IfcRelAssignsToGroup", "IfcRelServicesBuildings", "IfcRelDefinesByProperties"];

    /// <summary>Whether an entity is an element a search set may take (a placed thing, the model's own or a linked file's).</summary>
    public static bool IsElement(IQueryContext q, Entity e)
    {
        if (NotPhysical.Contains(e.Class)) return false;
        var kind = q.Describe(e.Class)?.Kind;
        if (kind != null) return kind == "element";
        // A class no module owns (an imported file's): an element when it stands somewhere.
        return e.Class.StartsWith("Ifc", StringComparison.Ordinal) && !e.Class.EndsWith("Type", StringComparison.Ordinal) && (Spatial.StoreyOf(q.Model, e.Id) != null || e.Attrs[Links.Key] != null);
    }

    /// <summary>A run's view of the model: each element's facts, its body when asked, and what it is meant to meet.</summary>
    public sealed class Universe(IQueryContext q)
    {
        public readonly IQueryContext Query = q;
        public readonly List<ElementFacts> Elements = q.Model.Entities.Where(e => IsElement(q, e)).Select(e => SearchSets.Facts(q, e)).ToList();
        readonly Dictionary<string, ClashBody?> _bodies = [];
        readonly Dictionary<string, HashSet<string>> _partners = [];

        public List<string> Members(SearchSets.Set set) => Elements.Where(f => SearchSets.Matches(set, f)).Select(f => f.Entity.Id).ToList();

        /// <summary>An element's body: its box from its geometry, its triangles in the world when the run asks (none: no geometry).</summary>
        public ClashBody? Body(string id)
        {
            if (_bodies.TryGetValue(id, out var b)) return b;
            ElementGeometry? g;
            try { g = Query.Geometry(id); }
            catch (Exception ex) when (ex is ModelException or InvalidOperationException or ArgumentException) { g = null; }
            var parts = g?.Parts.Where(p => p.Mesh != null || p.Solid.Faces.Length > 0).ToList() ?? [];
            Vec3 min = new(double.MaxValue, double.MaxValue, double.MaxValue), max = -min;
            foreach (var p in parts)
            {
                if (p.Mesh is { } m)
                    for (var i = 0; i + 2 < m.Xyz.Length; i += 3) { var v = new Vec3(m.Xyz[i], m.Xyz[i + 1], m.Xyz[i + 2]); min = Collisions.Min(min, v); max = Collisions.Max(max, v); }
                else if (Measure.Bounds(p.Solid) is { } box) { min = Collisions.Min(min, box.Min); max = Collisions.Max(max, box.Max); }
            }
            b = parts.Count == 0 || min.X > max.X ? null : new ClashBody(id, min, max, () => Triangles(parts));
            _bodies[id] = b;
            return b;
        }

        static IReadOnlyList<Tri> Triangles(List<GeometryPart> parts)
        {
            var o = new List<Tri>();
            foreach (var p in parts)
            {
                if (p.Mesh is { } m)
                {
                    Vec3 V(int k) => new(m.Xyz[3 * k], m.Xyz[3 * k + 1], m.Xyz[3 * k + 2]);
                    for (var i = 0; i + 2 < m.Tris.Length; i += 3) o.Add(Tri.Make(V(m.Tris[i]), V(m.Tris[i + 1]), V(m.Tris[i + 2])));
                    continue;
                }
                var t = Tessellator.Tessellate(p.Solid);
                Vec3 P(int k) => new(t.Positions[3 * k], t.Positions[3 * k + 1], t.Positions[3 * k + 2]);
                for (var i = 0; i + 2 < t.Indices.Length; i += 3) o.Add(Tri.Make(P(t.Indices[i]), P(t.Indices[i + 1]), P(t.Indices[i + 2])));
            }
            return o;
        }

        /// <summary>What an element is meant to meet (see the module): never tested against it.</summary>
        public HashSet<string> Partners(string id)
        {
            if (_partners.TryGetValue(id, out var set)) return set;
            set = [];
            var m = Query.Model;
            foreach (var r in m.RelationsOf(id))
            {
                if (Unrelating.Contains(r.Class)) continue;
                set.Add(r.Relating);
                foreach (var x in r.Related) set.Add(x);
                // A wall's opening: what fills it (the door), and back.
                if (r.Class == IfcClass.RelVoidsElement && r.Relating == id)
                    foreach (var o in r.Related) foreach (var f in m.RelationsOf(o, IfcClass.RelFillsElement)) foreach (var x in f.Related) set.Add(x);
                if (r.Class == IfcClass.RelFillsElement && r.Related.Contains(id))
                    foreach (var v in m.RelationsOf(r.Relating, IfcClass.RelVoidsElement)) set.Add(v.Relating);
            }
            // The ids its attributes name (a member fitted on another, a wall attached to a roof, a connection's members).
            if (m.Get(id) is { } e) foreach (var x in Ids(e.Attrs)) if (x != id && m.Get(x) != null) set.Add(x);
            set.Remove(id);
            _partners[id] = set;
            return set;
        }

        static IEnumerable<string> Ids(JsonNode? n)
        {
            switch (n)
            {
                case JsonValue v when v.TryGetValue<string>(out var s) && s.Length is >= 8 and <= 64: yield return s; break;
                case JsonArray a: foreach (var x in a) foreach (var s in Ids(x)) yield return s; break;
                case JsonObject o: foreach (var (_, x) in o) foreach (var s in Ids(x)) yield return s; break;
            }
        }

        public bool Meant(string a, string b) => Partners(a).Contains(b) || Partners(b).Contains(a);
    }

    /// <summary>One test run over a universe: its clashes as (A, B, where, depth or gap, crossing), the engine's run.</summary>
    public static (ClashRun Run, List<(string A, string B, Vec3 At, double Distance, bool Crossing)> Found) RunTest(Universe u, ClashTests.Test t, IReadOnlyDictionary<string, SearchSets.Set> sets)
    {
        if (t.Kind == ClashTests.Openings)
        {
            // The openings check (Manage → Clash check…'s): each MEP run through a wall, floor or roof without an opening big enough;
            // how far short it is as the depth. Its fixes stay that dialog's (Make opening, Enlarge opening).
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var found = OpeningClashes.Run(u.Query, OpeningClashes.Options.From(u.Query.Model, null)).Where(c => c.IsClash && c.MepId != null)
                .GroupBy(c => (c.HostId, c.MepId)).Select(g => g.First()).Select(c => (c.HostId, c.MepId!, c.Centre, Math.Max(c.By, 0), true)).ToList();
            var hosts = found.Select(f => f.HostId).Distinct().Count();
            return (new ClashRun([], false, found.Count, hosts, found.Select(f => f.Item2).Distinct().Count(), clock.Elapsed.TotalSeconds), found);
        }
        if (!sets.TryGetValue(t.SetA, out var setA)) throw new CommandException($"The test {t.Name} has no set A.");
        SearchSets.Set? setB = null;
        if (!t.IsSelf && !sets.TryGetValue(t.SetB!, out setB)) throw new CommandException($"The test {t.Name} has no set B.");
        var a = u.Members(setA);
        var b = setB == null ? null : u.Members(setB);
        var ids = a.Concat(b ?? []).Distinct().ToList();
        var bodies = new List<ClashBody>();
        var index = new Dictionary<string, int>();
        foreach (var id in ids)
            if (u.Body(id) is { } body) { index[id] = bodies.Count; bodies.Add(body); }
        int[] Of(List<string> s) => s.Where(index.ContainsKey).Select(x => index[x]).ToArray();
        var job = new ClashJob(bodies, Of(a), b == null ? null : Of(b), t.Kind == ClashTests.Clearance, Math.Clamp(t.Tolerance, 0, ClashEngine.MaxTolerance),
            Skip: (i, j) => u.Meant(bodies[i].Id, bodies[j].Id));
        var run = ClashEngine.Run(job);
        return (run, run.Hits.Select(h => (bodies[h.A].Id, bodies[h.B].Id, h.Point, h.Distance, h.Crossing)).ToList());
    }

    static Dictionary<string, SearchSets.Set> Sets(IModelView m) => m.OfClass(SearchSets.Class).Select(SearchSets.Read).ToDictionary(s => s.Id);

    static JsonObject SetJson(SearchSets.Set s, int? count = null)
    {
        var o = new RuleGroup(s.MatchAll, s.Rules, s.Groups).ToJson();
        o["id"] = s.Id;
        o["name"] = s.Name;
        if (count != null) o["count"] = count;
        return o;
    }

    static JsonObject TestJson(IModelView m, ClashTests.Test t, string hash)
    {
        var o = new JsonObject
        {
            ["id"] = t.Id, ["name"] = t.Name, ["kind"] = t.Kind, ["setA"] = t.SetA, ["setB"] = t.SetB, ["tolerance"] = t.Tolerance, ["enabled"] = t.Enabled,
            ["counts"] = ClashTests.Counts(t.Clashes), ["lastRun"] = t.LastRun?.DeepClone(),
        };
        if (t.LastRun != null) o["stale"] = (string?)t.LastRun["hash"] != hash;
        return o;
    }

    static ElementFacts? FactsOf(IQueryContext q, string id) => q.Model.Get(id) is { } e ? SearchSets.Facts(q, e) : null;

    static Dictionary<int, string> Grouped(IQueryContext q, IReadOnlyList<ClashRecord> clashes, string? mode)
    {
        if (mode is null or ClashGroups.None) return [];
        if (!ClashGroups.Modes.Contains(mode)) throw new CommandException($"Clashes are grouped by {string.Join(", ", ClashGroups.Modes)}.");
        var cache = new Dictionary<string, ElementFacts?>();
        return ClashGroups.Of(clashes, mode, id => cache.TryGetValue(id, out var f) ? f : cache[id] = FactsOf(q, id));
    }

    /// <summary>The clashes in their groups' order (the first clash of each group placing it), by number within each.</summary>
    static List<ClashRecord> Ordered(List<ClashRecord> clashes, IReadOnlyDictionary<int, string> groups)
    {
        if (groups.Count == 0) return clashes;
        var order = new Dictionary<string, int>();
        foreach (var c in clashes) if (groups.TryGetValue(c.Number, out var g) && !order.ContainsKey(g)) order[g] = order.Count;
        return clashes.OrderBy(c => groups.TryGetValue(c.Number, out var g) ? order[g] : int.MaxValue).ThenBy(c => c.Number).ToList();
    }

    /// <summary>
    /// The open clashes (new, active) of the tests Run all takes, as red clouds in plans, sections and elevations, with the
    /// openings' report's (View → Clash marks): labelled "test.clash", the test's number its place among the tests by name.
    /// </summary>
    sealed class Marks : ClashMarks.ISource
    {
        public IEnumerable<ClashMarks.Item> Items(IQueryContext q)
        {
            var tests = q.Model.OfClass(ClashTests.Class).Select(ClashTests.Read).OrderBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
            for (var k = 0; k < tests.Count; k++)
            {
                if (!tests[k].Enabled) continue;
                foreach (var c in tests[k].Clashes.Where(c => c.Status is ClashTests.New or ClashTests.Active))
                    yield return new ClashMarks.Item(c.At, c.B, c.A, $"{k + 1}.{c.Number}", new JsonObject { ["test"] = tests[k].Id, ["clash"] = c.Number });
            }
        }
    }

    static JsonObject ClashJson(IQueryContext q, ClashRecord c)
    {
        var o = c.ToJson();
        foreach (var (k, id) in new[] { ("a", c.A), ("b", c.B) })
        {
            var f = FactsOf(q, id);
            o[k + "Name"] = f?.Entity.Name;
            o[k + "Class"] = f?.ClassLabel;
            o[k + "Storey"] = f?.Storey;
            o[k + "Link"] = f?.Link;
        }
        return o;
    }

    /// <summary>The sets and tests a project starts with: Structure, Services and Architecture, and three hard tests at 5 mm.</summary>
    static void Seed(ICommandContext ctx)
    {
        string Set(string name, string[] classes)
        {
            var e = new Entity(ctx.NewId(), SearchSets.Class, name, SearchSets.ClassSet(classes));
            ctx.Add(e);
            return e.Id;
        }
        var structure = Set("Structure", SearchSets.StructureClasses);
        var services = Set("Services", SearchSets.ServicesClasses);
        var architecture = Set("Architecture", SearchSets.ArchitectureClasses);
        void Test(string name, string a, string? b) => ctx.Add(new Entity(ctx.NewId(), ClashTests.Class, name, new JsonObject
        {
            ["kind"] = ClashTests.Hard, ["setA"] = a, ["setB"] = b, ["tolerance"] = ClashEngine.HardTolerance, ["enabled"] = true,
        }));
        Test("Structure × Services", structure, services);
        Test("Services × Services", services, null);
        Test("Architecture × Services", architecture, services);
        ctx.Add(new Entity(ctx.NewId(), ClashTests.Class, "Openings for MEP", new JsonObject { ["kind"] = ClashTests.Openings, ["setA"] = "", ["tolerance"] = 0, ["enabled"] = true }));
    }

    static string Text(JsonObject a, string key, string fallback) => ((string?)a[key])?.Trim() is { Length: > 0 } s ? s : fallback;

    public void Register(IModuleRegistry r)
    {
        r.Class(new(SearchSets.Class, "Search set", [], Kind: "resource"));
        r.Class(new(ClashTests.Class, "Clash test", [], Kind: "resource"));
        r.Service<ClashMarks.ISource>(new Marks());

        // The sets and tests, each set with how many elements it holds now, each test with its counts and whether the model changed since its run.
        r.Query("clash.list", (q, _) =>
        {
            var u = new Universe(q);
            var hash = ClashReports.ModelHash(q.Model);
            return new JsonObject
            {
                ["sets"] = new JsonArray(Sets(q.Model).Values.OrderBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase).Select(s => (JsonNode)SetJson(s, u.Members(s).Count)).ToArray()),
                ["tests"] = new JsonArray(q.Model.OfClass(ClashTests.Class).Select(ClashTests.Read).OrderBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase).Select(t => (JsonNode)TestJson(q.Model, t, hash)).ToArray()),
                ["fields"] = new JsonArray(SearchSets.Fields.Select(f => (JsonNode)f).ToArray()),
                ["ops"] = new JsonArray(SearchSets.Ops.Select(o => (JsonNode)o).ToArray()),
            };
        });
        // A set's elements as its rules take them now ({id} or {set: rules}): how many and their ids.
        r.Query("clash.members", (q, a) =>
        {
            var set = a["set"] is JsonObject o ? new SearchSets.Set("", "", RuleGroup.From(o).MatchAll, RuleGroup.From(o).Rules, RuleGroup.From(o).Groups)
                : q.Model.Get((string?)a["id"] ?? "") is { Class: SearchSets.Class } e ? SearchSets.Read(e) : throw new CommandException("Which search set?");
            var ids = new Universe(q).Members(set);
            return new JsonObject { ["count"] = ids.Count, ["ids"] = new JsonArray(ids.Select(x => (JsonNode)x).ToArray()) };
        });
        // The values a field takes in the model, for a rule's picker: {field, parameter?}; each value and how many elements have it.
        r.Query("clash.values", (q, a) =>
        {
            var field = (string?)a["field"] ?? SearchSets.ClassField;
            var counts = new Dictionary<string, int>(StringComparer.CurrentCultureIgnoreCase);
            foreach (var f in new Universe(q).Elements)
            {
                var texts = field == SearchSets.ClassField ? [f.Entity.Class] : SearchSets.Texts(field, (string?)a["parameter"], f).ToList();
                if (field == SearchSets.Link && f.Link == null) texts = [""];
                foreach (var t in texts.Distinct()) counts[t] = counts.GetValueOrDefault(t) + 1;
            }
            return new JsonArray(counts.OrderBy(x => x.Key, StringComparer.CurrentCultureIgnoreCase).Select(x => (JsonNode)new JsonObject
            {
                ["value"] = x.Key, ["count"] = x.Value,
                ["label"] = field == SearchSets.ClassField ? q.Describe(x.Key)?.Label ?? x.Key : field == SearchSets.Link && x.Key == "" ? "This model" : x.Key,
            }).ToArray());
        });
        // A test's clashes with what they are between: {id, status?}.
        // A test's clashes with what they are between: {id, status?, groupBy?: none | storey | element | classes | status}; each with its group, in the groups' order.
        r.Query("clash.results", (q, a) =>
        {
            var t = q.Model.Get((string?)a["id"] ?? "") is { Class: ClashTests.Class } e ? ClashTests.Read(e) : throw new CommandException("Which clash test?");
            var status = (string?)a["status"];
            var list = t.Clashes.Where(c => status == null || c.Status == status).ToList();
            var groups = Grouped(q, list, (string?)a["groupBy"]);
            return new JsonObject
            {
                ["test"] = TestJson(q.Model, t, ClashReports.ModelHash(q.Model)),
                ["clashes"] = new JsonArray(Ordered(list, groups).Select(c => { var o = ClashJson(q, c); if (groups.TryGetValue(c.Number, out var g)) o["group"] = g; return (JsonNode)o; }).ToArray()),
            };
        });
        // A test's clashes as a file: {id, format: xml | pdf, groupBy?, resolved?}; {name, data (base64)}. XML as Navisworks writes its clash reports.
        r.Query("clash.reportFile", (q, a) =>
        {
            var t = q.Model.Get((string?)a["id"] ?? "") is { Class: ClashTests.Class } e ? ClashTests.Read(e) : throw new CommandException("Which clash test?");
            var resolved = a["resolved"] is JsonValue rv && rv.TryGetValue<bool>(out var rb) && rb;
            var list = t.Clashes.Where(c => resolved || c.Status != ClashTests.Resolved).ToList();
            var groups = Grouped(q, list, (string?)a["groupBy"]);
            list = Ordered(list, groups);
            var format = (string?)a["format"] ?? "xml";
            var bytes = format switch
            {
                "xml" => ClashReportFiles.Xml(q, t, list, groups, Sets(q.Model)),
                "pdf" => ClashReportFiles.Pdf(q, t, list, groups, t.LastRun != null && (string?)t.LastRun["hash"] != ClashReports.ModelHash(q.Model)),
                _ => throw new CommandException("A clash report is written as XML or PDF."),
            };
            return new JsonObject { ["name"] = $"{t.Name}.{format}", ["data"] = Convert.ToBase64String(bytes) };
        });

        // The starting sets and tests, when the project has none: one undo step.
        r.Command("clash.seed", "Clash tests", (ctx, _) =>
        {
            if (!ctx.Model.OfClass(ClashTests.Class).Any() && !ctx.Model.OfClass(SearchSets.Class).Any()) Seed(ctx);
            return null;
        });
        // A search set made or changed: {id?, name, matchAll, rules, groups}; its id.
        r.Command("clash.setSet", "Search set", (ctx, a) =>
        {
            var g = RuleGroup.From(a);
            foreach (var rule in g.Rules)
            {
                if (!SearchSets.Fields.Contains(rule.Field)) throw new CommandException($"A rule reads one of {string.Join(", ", SearchSets.Fields)}.");
                if (!SearchSets.Ops.Contains(rule.Op)) throw new CommandException($"A rule's operator is one of {string.Join(", ", SearchSets.Ops)}.");
            }
            var attrs = g.ToJson();
            if ((string?)a["id"] is { } id)
            {
                var e = ctx.Model.Get(id) is { Class: SearchSets.Class } x ? x : throw new CommandException("There is no such search set.");
                ctx.Replace(e.With(name: Text(a, "name", e.Name ?? "Set"), attrs: attrs));
                return new JsonObject { ["id"] = id };
            }
            var made = new Entity(ctx.NewId(), SearchSets.Class, Text(a, "name", "Search set"), attrs);
            ctx.Add(made);
            return new JsonObject { ["id"] = made.Id };
        });
        // A search set deleted, refused while a test uses it.
        r.Command("clash.deleteSet", "Delete search set", (ctx, a) =>
        {
            var id = (string?)a["id"] ?? "";
            if (ctx.Model.Get(id) is not { Class: SearchSets.Class }) throw new CommandException("There is no such search set.");
            if (ctx.Model.OfClass(ClashTests.Class).Select(ClashTests.Read).FirstOrDefault(t => t.SetA == id || t.SetB == id) is { } user)
                throw new CommandException($"The test {user.Name} uses this set: change or delete it first.");
            ctx.Remove(id);
            return null;
        });
        // A clash test made or changed: {id?, name, kind, setA, setB?, tolerance, enabled}; changing its sets or kind starts its clashes afresh.
        r.Command("clash.setTest", "Clash test", (ctx, a) =>
        {
            var m = ctx.Model;
            var existing = (string?)a["id"] is { } id ? m.Get(id) is { Class: ClashTests.Class } x ? x : throw new CommandException("There is no such clash test.") : null;
            var old = existing != null ? ClashTests.Read(existing) : null;
            var kind = (string?)a["kind"] ?? old?.Kind ?? ClashTests.Hard;
            if (!ClashTests.Kinds.Contains(kind)) throw new CommandException("A clash test is hard, clearance or openings.");
            var setA = (string?)a["setA"] ?? old?.SetA ?? "";
            if (kind != ClashTests.Openings && m.Get(setA) is not { Class: SearchSets.Class }) throw new CommandException("Set A is one of the search sets.");
            var setB = a.ContainsKey("setB") ? (string?)a["setB"] is { Length: > 0 } sb ? sb : null : old?.SetB;
            if (setB != null && m.Get(setB) is not { Class: SearchSets.Class }) throw new CommandException("Set B is one of the search sets, or none to test set A against itself.");
            var tolerance = Json.Number(a["tolerance"]) ?? old?.Tolerance ?? (kind == ClashTests.Clearance ? ClashEngine.ClearanceDistance : ClashEngine.HardTolerance);
            if (tolerance < 0 || tolerance > ClashEngine.MaxTolerance) throw new CommandException($"The tolerance is 0 to {ClashEngine.MaxTolerance * 1000:0} mm.");
            var enabled = a["enabled"] is JsonValue ev && ev.TryGetValue<bool>(out var en) ? en : old?.Enabled ?? true;
            var attrs = existing?.CloneAttrs() ?? new JsonObject();
            attrs["kind"] = kind; attrs["setA"] = setA; attrs["setB"] = setB; attrs["tolerance"] = tolerance; attrs["enabled"] = enabled;
            if (old != null && (old.Kind != kind || old.SetA != setA || old.SetB != setB)) { attrs.Remove("clashes"); attrs.Remove("runs"); }
            var name = Text(a, "name", old?.Name ?? (kind == ClashTests.Openings ? "Openings for MEP" : $"{m.Get(setA)?.Name} × {(setB != null ? m.Get(setB)?.Name : "itself")}"));
            if (existing != null) { ctx.Replace(existing.With(name: name, attrs: attrs)); return new JsonObject { ["id"] = existing.Id }; }
            var made = new Entity(ctx.NewId(), ClashTests.Class, name, attrs);
            ctx.Add(made);
            return new JsonObject { ["id"] = made.Id };
        });
        r.Command("clash.deleteTest", "Delete clash test", (ctx, a) =>
        {
            if (ctx.Model.Get((string?)a["id"] ?? "") is not { Class: ClashTests.Class } e) throw new CommandException("There is no such clash test.");
            ctx.Remove(e.Id);
            return null;
        });
        // Run: {ids?} the tests given, or every test Run all takes; their clashes merged and kept, one undo step; what each found.
        r.Command("clash.run", "Run clash tests", (ctx, a) =>
        {
            var m = ctx.Model;
            var want = (a["ids"] as JsonArray)?.Select(x => (string?)x).OfType<string>().ToHashSet();
            var tests = m.OfClass(ClashTests.Class).Where(e => want?.Contains(e.Id) ?? ClashTests.Read(e).Enabled).ToList();
            if (tests.Count == 0) throw new CommandException(want != null ? "There is no such clash test." : "No clash test is on for Run all.");
            var sets = Sets(m);
            var u = new Universe(ctx.Query);
            var hash = ClashReports.ModelHash(m);
            var now = ClashTests.Now();
            var report = new JsonArray();
            foreach (var e in tests)
            {
                var t = ClashTests.Read(e);
                var (run, found) = RunTest(u, t, sets);
                var (clashes, added, reopened, resolved) = ClashTests.Merge(t.Clashes, found, !run.Truncated, now);
                var summary = new JsonObject
                {
                    ["date"] = now, ["hash"] = hash, ["found"] = found.Count, ["new"] = added, ["reopened"] = reopened, ["resolved"] = resolved,
                    ["countA"] = run.CountA, ["countB"] = run.CountB, ["pairs"] = run.Candidates, ["truncated"] = run.Truncated, ["seconds"] = Math.Round(run.Seconds, 2),
                };
                var attrs = e.CloneAttrs();
                attrs["clashes"] = new JsonArray(clashes.Select(c => (JsonNode)c.ToJson()).ToArray());
                var runs = attrs["runs"] as JsonArray ?? [];
                var list = runs.Select(x => x?.DeepClone()).Append(summary.DeepClone()).TakeLast(ClashTests.MaxRuns).ToArray();
                attrs["runs"] = new JsonArray(list);
                ctx.Replace(e.With(attrs: attrs));
                summary["id"] = e.Id;
                summary["name"] = t.Name;
                report.Add((JsonNode)summary);
            }
            return report;
        });
        // A status, who has them and notes, on a test's clashes: {id, numbers, status?, assignedTo?, notes?}.
        r.Command("clash.setStatus", "Clash status", (ctx, a) =>
        {
            var e = ctx.Model.Get((string?)a["id"] ?? "") is { Class: ClashTests.Class } x ? x : throw new CommandException("Which clash test?");
            var t = ClashTests.Read(e);
            var numbers = (a["numbers"] as JsonArray ?? []).Select(n => (int)(Json.Number(n) ?? 0)).ToHashSet();
            var status = (string?)a["status"];
            if (status != null && !ClashTests.Statuses.Contains(status)) throw new CommandException($"A clash is {string.Join(", ", ClashTests.Statuses)}.");
            string? Opt(string k, string? was) => a.ContainsKey(k) ? ((string?)a[k])?.Trim() is { Length: > 0 } s ? s : null : was;
            var now = ClashTests.Now();
            var changed = t.Clashes.Select(c => numbers.Contains(c.Number)
                ? c with { Status = status ?? c.Status, Resolved = status == ClashTests.Resolved ? c.Resolved ?? now : status != null ? null : c.Resolved, AssignedTo = Opt("assignedTo", c.AssignedTo), Notes = Opt("notes", c.Notes) }
                : c).ToList();
            var attrs = e.CloneAttrs();
            attrs["clashes"] = new JsonArray(changed.Select(c => (JsonNode)c.ToJson()).ToArray());
            ctx.Replace(e.With(attrs: attrs));
            return null;
        });
        // Clashes as remarks of type Clash (Manage → Remarks, BCF): {id, numbers?, author?} (none: the test's open ones); a remark made again is updated.
        r.Command("clash.remarks", "Clashes as remarks", (ctx, a) =>
        {
            var e = ctx.Model.Get((string?)a["id"] ?? "") is { Class: ClashTests.Class } x ? x : throw new CommandException("Which clash test?");
            var t = ClashTests.Read(e);
            var numbers = (a["numbers"] as JsonArray)?.Select(n => (int)(Json.Number(n) ?? 0)).ToHashSet();
            var author = Text(a, "author", "Lapin");
            int added = 0, updated = 0;
            var now = Bcf.Now();
            var remarks = new Dictionary<int, string>();
            foreach (var c in t.Clashes.Where(c => numbers?.Contains(c.Number) ?? c.Status is not (ClashTests.Resolved or ClashTests.Approved)))
            {
                var guid = GlobalId.ToGuid(OpeningVoids.DerivedId($"{t.Id}|{c.Key}", "clash"));
                var id = Remarks.IdOf(guid);
                string N(string x) => ctx.Model.Get(x)?.Name ?? x;
                var title = $"Clash {c.Number}: {N(c.A)} / {N(c.B)}";
                var description = $"{t.Name}: {(c.Crossing ? $"{Math.Round(c.Distance * 1000):0} mm deep" : $"{Math.Round(c.Distance * 1000):0} mm apart")}" + (c.Notes is { } notes ? $". {notes}" : "");
                remarks[c.Number] = id;
                if (ctx.Model.Get(id) is { Class: Remarks.Class } existing)
                {
                    ctx.Replace(Remarks.ToEntity(Remarks.Read(existing) with { Title = title, Description = description, ModifiedDate = now, ModifiedAuthor = author, AssignedTo = c.AssignedTo }));
                    updated++;
                    continue;
                }
                var eye = c.At + new Vec3(-2.5, -2.5, 1.8);
                var dir = (c.At - eye).Normalized();
                var right = Vec3.Cross(dir, Vec3.UnitZ).Normalized();
                var up = Vec3.Cross(right, dir).Normalized();
                static BcfPoint P(Vec3 v) => new(v.X, v.Y, v.Z);
                var vp = new BcfViewpoint(Guid.NewGuid(), new BcfCamera(true, P(eye), P(dir), P(up), 60, 1), [c.A, c.B], true, [], [], [], null);
                ctx.Add(Remarks.ToEntity(new BcfTopic(guid, title, description, "Clash", "Open", c.Crossing && c.Distance > 0.05 ? "High" : "Normal", null, now, author, null, null, c.AssignedTo, null, null, [t.Name], [], [vp])));
                added++;
            }
            if (remarks.Count > 0)
            {
                var attrs = e.CloneAttrs();
                attrs["clashes"] = new JsonArray(t.Clashes.Select(c => (JsonNode)(remarks.TryGetValue(c.Number, out var rid) ? c with { Remark = rid } : c).ToJson()).ToArray());
                ctx.Replace(e.With(attrs: attrs));
            }
            return new JsonObject { ["added"] = added, ["updated"] = updated, ["ids"] = new JsonArray(remarks.Values.Select(x => (JsonNode)x).ToArray()) };
        });
    }
}
