using System.Text.Json.Nodes;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Platform.Analysis;
using Lapin.Analysis.Solver;

namespace Lapin.Analysis;

/// <summary>
/// Structural analysis, stage S0 (docs/plan-fem.md §8): the project's load cases, the user's loads (area, line, point), the
/// loads derived from the model on every ask (the self weight, the floors' finishes, the partitions, the rooms' imposed loads
/// by their category of use), the EN 1990 combinations, and all of it for IFC's structural analysis view
/// (<see cref="IStructuralLoads"/>). Stage S1 (§9): the linear static analysis of the bars (<see cref="AnalysisRun"/>:
/// analysis.run, analysis.results, analysis.diagram) and their end releases (analysis.setReleases). It reads rooms, slabs and
/// walls by their class and geometry, never through architecture or structure: without them it has less to read. Engine
/// units: metres, kN, kN/m, kN/m².
/// </summary>
public sealed class AnalysisModule : ILapinModule, IElementUpdate, IDeletion, IViewAnnotations, IStructuralLoads
{
    public const string ModuleId = "analysis";
    public string Id => ModuleId;
    public string Name => "Structural analysis";

    public void Register(IModuleRegistry r)
    {
        r.Class(new(LoadCases.Class, "Load case",
        [
            new("code", "Code", "text"),
            new("actionType", "Action", "choice", Choices: LoadCases.ActionTypes),
            new("actionSource", "Source", "choice", Choices: LoadCases.ActionSources),
            new("category", "Category", "choice", Choices: [.. Categories.All.Select(c => c.Key), Categories.Snow, Categories.Wind]),
            new("selfWeight", "Self weight", "bool"),
            new("windDirection", "Wind towards", "choice", Choices: Climate.Directions),
            new("roofCpe", "Roof coefficients", "choice", Choices: Climate.RoofSets),
        ], "#c2410c", Kind: "resource"));
        UserLoads.Register(r);
        r.Service<IElementUpdate>(this);
        r.Service<IDeletion>(this);
        r.Service<IViewAnnotations>(this);
        r.Service<IStructuralLoads>(this);
        r.Service<IElementPlacement>(new UserLoads.Placement());
        // Loads and load cases from tables (docs/plan-user-extensibility.md §7.2).
        r.Service<TableTarget>(AnalysisTables.Loads);
        r.Service<TableTarget>(AnalysisTables.Cases);

        // ── Cases ──

        r.Command("defaultCases", "Default load cases", (ctx, _) => Ids(LoadCases.EnsureDefaults(ctx)));

        r.Command("addCase", "New load case", (ctx, a) =>
        {
            var (name, attrs) = LoadCases.Write(a, ctx.Model, null);
            var e = new Entity(ctx.NewId(), LoadCases.Class, name, attrs);
            ctx.Add(e);
            return new JsonObject { ["id"] = e.Id };
        });

        r.Command("updateCase", "Change load case", (ctx, a) =>
        {
            var e = ctx.Model.Get((string?)a["id"] ?? "") is { Class: LoadCases.Class } x ? x : throw new CommandException("There is no such load case.");
            var (name, attrs) = LoadCases.Write(a, ctx.Model, e);
            ctx.Replace(e.With(name: name, attrs: attrs));
            return null;
        });

        r.Command("removeCase", "Remove load case", (ctx, a) =>
        {
            var e = ctx.Model.Get((string?)a["id"] ?? "") is { Class: LoadCases.Class } x ? x : throw new CommandException("There is no such load case.");
            RefuseInUse(ctx.Model, e, new HashSet<string>());
            ctx.Remove(e.Id);
            return null;
        });

        // A case Q-<category> (derives occupancy) for each category of the rooms that has none.
        r.Command("addMissingCases", "Missing load cases", (ctx, _) =>
        {
            var made = LoadCases.EnsureDefaults(ctx);
            foreach (var cat in ctx.Model.OfClass(IfcClass.Space).Select(Categories.OfRoom).Where(c => c != Categories.None).Distinct().OrderBy(c => c, StringComparer.Ordinal))
                if (LoadCases.Taking(LoadCases.All(ctx.Model), LoadCases.Occupancy, cat) == null) made.Add(LoadCases.EnsureCategory(ctx, cat));
            return Ids(made);
        });

        // ── Rooms and imposed loads ──

        // {ids, category}: the rooms' category of use (a key or none; null takes theirs off, back to their function's); the category's case made if missing.
        r.Command("setOccupancy", "Category of use", (ctx, a) =>
        {
            var ids = Args.Ids(a);
            if (ids.Count == 0) throw new CommandException("Select the rooms.");
            var category = (string?)a["category"];
            if (category != null && category != Categories.None && Categories.Of(category) == null)
                throw new CommandException($"„{category}” is not a category of use: one of {string.Join(", ", Categories.All.Select(c => c.Key))}, or none.");
            foreach (var id in ids)
            {
                var room = ctx.Model.Get(id) is { Class: IfcClass.Space } x ? x : throw new CommandException("A category of use is a room's.");
                var attrs = room.CloneAttrs();
                if (category == null) attrs.Remove(Categories.RoomKey); else attrs[Categories.RoomKey] = category;
                ctx.Replace(room.With(attrs: attrs));
            }
            if (category is { } c && c != Categories.None) LoadCases.EnsureCategory(ctx, c);
            return null;
        });

        // {category, qk}: the project's imposed load of a category (kN/m², its national annex's); null back to the recommended value.
        r.Command("setImposedLoad", "Imposed load", (ctx, a) =>
        {
            var key = (string?)a["category"];
            if (Categories.Of(key) == null) throw new CommandException("There is no such category of use.");
            var project = Spatial.Project(ctx.Model) ?? throw new CommandException("The model has no project.");
            var attrs = project.CloneAttrs();
            var own = attrs[Categories.ProjectKey] as JsonObject ?? [];
            if (a["qk"] is null) own.Remove(key!);
            else if (Json.Number(a["qk"]) is { } q && double.IsFinite(q) && q >= 0 && q <= 1000) own[key!] = q;
            else throw new CommandException("An imposed load is a number of kN/m², zero or more.");
            attrs.Remove(Categories.ProjectKey);
            if (own.Count > 0) attrs[Categories.ProjectKey] = own.DeepClone();
            ctx.Replace(project.With(attrs: attrs));
            return null;
        });

        // {snow?: {s0k, ce?, ct?, importance?} | null, wind?: {qb, terrain?, importance?} | null}: the site's climate (CR 1-1-3,
        // CR 1-1-4), the values the user reads for the site; null takes one off, absent leaves it. The cases its loads go in
        // made when missing (the snow's arrangements, the wind's directions), in the same undo step.
        r.Command("setClimate", "Snow and wind", (ctx, a) =>
        {
            if (!a.ContainsKey("snow") && !a.ContainsKey("wind")) throw new CommandException("Say the snow, the wind or both.");
            var project = Spatial.Project(ctx.Model) ?? throw new CommandException("The model has no project.");
            var attrs = project.CloneAttrs();
            var own = attrs[Climate.Key] is JsonObject had ? (JsonObject)had.DeepClone() : new JsonObject();
            if (a.ContainsKey("snow")) { if (a["snow"] is JsonObject s) own["snow"] = Climate.WriteSnow(s); else if (a["snow"] is null) own.Remove("snow"); else throw new CommandException("The snow is {s0k, ce, ct, importance}."); }
            if (a.ContainsKey("wind")) { if (a["wind"] is JsonObject w) own["wind"] = Climate.WriteWind(w); else if (a["wind"] is null) own.Remove("wind"); else throw new CommandException("The wind is {qb, terrain, importance}."); }
            attrs.Remove(Climate.Key);
            if (own.Count > 0) attrs[Climate.Key] = own;
            ctx.Replace(project.With(attrs: attrs));
            return Ids(Climate.EnsureCases(ctx, a["snow"] is JsonObject, a["wind"] is JsonObject));
        });

        // ── Queries ──

        r.Query("climate", (q, _) => Climate.ToJson(q.Model));

        r.Query("loads", (q, _) => LoadsJson(q.Model, LoadsReport.Compute(q)));

        r.Query("categories", (q, _) =>
        {
            var own = Categories.Overrides(q.Model);
            return new JsonArray(Categories.All.Select(c => (JsonNode)new JsonObject
            {
                ["key"] = c.Key, ["label"] = c.Label, ["qk"] = own.TryGetValue(c.Key, out var v) ? v : c.Qk, ["default"] = c.Qk, ["overridden"] = own.ContainsKey(c.Key),
            }).ToArray());
        });

        r.Query("combinations", (q, _) =>
        {
            var (combinations, notes) = Combinations.Of(LoadsReport.Compute(q));
            return new JsonObject
            {
                ["combinations"] = new JsonArray(combinations.Select(c => (JsonNode)Combinations.ToJson(c)).ToArray()),
                ["notes"] = new JsonArray(notes.Select(n => (JsonNode)n).ToArray()),
            };
        });

        // {caseId}: the case's loads for display, the user's and the derived, world coordinates (z absolute).
        r.Query("preview", (q, a) =>
        {
            var report = LoadsReport.Compute(q);
            var caseId = (string?)a["caseId"];
            if (report.Cases.All(c => c.Id != caseId)) throw new CommandException("There is no such load case.");
            var (areas, lines, points) = (new JsonArray(), new JsonArray(), new JsonArray());
            foreach (var x in Actions(q.Model, report).Where(x => x.CaseId == caseId))
            {
                var o = new JsonObject { ["value"] = x.Value, ["direction"] = DirectionName(x.Direction), ["id"] = x.Derived ? x.SourceId : x.Id, ["derived"] = x.Derived };
                switch (x.Kind)
                {
                    case "planar":
                        o["points"] = new JsonArray(x.Points.Select(p => (JsonNode)P(p.Xy)).ToArray());
                        o["z"] = x.Points[0].Z;
                        // On a roof's plane or a façade: its corners in 3D besides.
                        if (x.Points.Any(p => Math.Abs(p.Z - x.Points[0].Z) > 1e-6))
                            o["points3"] = new JsonArray(x.Points.Select(p => (JsonNode)new JsonArray(R(p.X), R(p.Y), R(p.Z))).ToArray());
                        areas.Add((JsonNode)o);
                        break;
                    case "curve":
                        o["a"] = P(x.Points[0].Xy); o["b"] = P(x.Points[1].Xy); o["z"] = x.Points[0].Z;
                        lines.Add((JsonNode)o);
                        break;
                    default:
                        o["at"] = P(x.Points[0].Xy); o["z"] = x.Points[0].Z;
                        points.Add((JsonNode)o);
                        break;
                }
            }
            return new JsonObject { ["areas"] = areas, ["lines"] = lines, ["points"] = points };
        });

        // ── Linear static analysis of the bars (S1, §9) ──

        r.Query("run", (q, _) => AnalysisRun.Of(q).RunJson());
        // {case | combination | envelope: "uls" | "sls", elementId?}: the nodes' displacements, the reactions, the bars' forces at their stations and the maxima.
        r.Query("results", (q, a) => AnalysisRun.Results(q, a));
        // {case | combination | envelope, quantity: N | Vy | Vz | T | My | Mz | deformed, scale?, elementId?}: polylines in world coordinates.
        r.Query("diagram", (q, a) => AnalysisRun.Diagram(q, a));
        // {case | combination | envelope, quantity: w | mx | my | mxy | nx | ny | nxy | vx | vy, shellId?}: each shell surface's
        // mesh with the quantity smoothed at its nodes (S2, §10.6), world coordinates.
        r.Query("contours", (q, a) => AnalysisRun.Contours(q, a));
        r.Query("options", (q, _) => AnalysisSettings.Of(q.Model).ToJson());
        // {shells?: both | slabs | walls | none, meshSize?: metres}: how the analysis takes slabs and walls; null back to the default. One undo step.
        r.Command("setOptions", "Analysis options", (ctx, a) => { AnalysisSettings.Write(ctx, a); return null; });

        // {ids, axial: true | false | null}: whether the bars work axially only (a rod, a tie: pinned at their ends, their weight
        // at them) or as beams; null back to what their section and use say (FrameReleases.Axial). One undo step.
        r.Command("setAxial", "Axial member", (ctx, a) =>
        {
            var ids = Args.Ids(a);
            if (ids.Count == 0) throw new CommandException("Select the bars.");
            if (!a.ContainsKey("axial")) throw new CommandException("Say axial: true (a rod or tie), false (a beam) or null (by its section and use).");
            bool? axial = a["axial"] is null ? null : a["axial"] is JsonValue v && v.TryGetValue<bool>(out var b) ? b : throw new CommandException("Axial is true, false or null.");
            foreach (var id in ids)
            {
                var e = ctx.Model.Get(id) ?? throw new CommandException($"There is no {id}.");
                if (UserLoads.Classes.Contains(e.Class) || e.Class == LoadCases.Class) throw new CommandException("Only a bar works axially.");
                var attrs = e.CloneAttrs();
                if (axial is { } x) attrs[FrameReleases.AxialKey] = x; else attrs.Remove(FrameReleases.AxialKey);
                ctx.Replace(e.With(attrs: attrs));
            }
            return null;
        });

        // {ids, i?, j?}: the bars' end releases, each end six booleans [ux, uy, uz, rx, ry, rz] in local axes (true: released),
        // or "fixed" / "pinned"; null takes the element's own off (back to the default), absent leaves that end as it is.
        r.Command("setReleases", "End releases", (ctx, a) =>
        {
            var ids = Args.Ids(a);
            if (ids.Count == 0) throw new CommandException("Select the bars.");
            if (!a.ContainsKey("i") && !a.ContainsKey("j")) throw new CommandException("Say the releases of end i, end j or both.");
            static bool[]? End(JsonNode? n, string key) =>
                n is null ? null : FrameReleases.Parse(n) ?? throw new CommandException($"End {key}'s releases are six booleans [ux, uy, uz, rx, ry, rz], or „fixed” or „pinned”.");
            var (ri, rj) = (End(a["i"], "i"), End(a["j"], "j"));
            foreach (var id in ids)
            {
                var e = ctx.Model.Get(id) ?? throw new CommandException($"There is no {id}.");
                if (UserLoads.Classes.Contains(e.Class) || e.Class == LoadCases.Class) throw new CommandException("Releases are a bar's.");
                var attrs = e.CloneAttrs();
                var own = attrs[FrameReleases.Key] as JsonObject is { } had ? (JsonObject)had.DeepClone() : new JsonObject();
                if (a.ContainsKey("i")) { if (ri == null) own.Remove("i"); else own["i"] = FrameReleases.ToJson(ri); }
                if (a.ContainsKey("j")) { if (rj == null) own.Remove("j"); else own["j"] = FrameReleases.ToJson(rj); }
                attrs.Remove(FrameReleases.Key);
                if (own.Count > 0) attrs[FrameReleases.Key] = own;
                ctx.Replace(e.With(attrs: attrs));
            }
            return null;
        });
    }

    static JsonObject Ids(IEnumerable<string> ids) => new() { ["ids"] = new JsonArray(ids.Select(i => (JsonNode)i).ToArray()) };
    static JsonArray P(Vec2 p) => new(Math.Round(p.X, 6), Math.Round(p.Y, 6));
    static double R(double v) => Math.Round(v, 6);

    static string DirectionName(Vec3 d) => d.Z < -0.5 ? "down" : d.Z > 0.5 ? "up" : d.X > 0.5 ? "+x" : d.X < -0.5 ? "-x" : d.Y > 0.5 ? "+y" : "-y";

    /// <summary>A case may not go while a user's load not going with it is in it.</summary>
    static void RefuseInUse(IModelView m, Entity c, IReadOnlySet<string> deleting)
    {
        var n = LoadCases.UserLoadsOf(m, c.Id).Count(l => !deleting.Contains(l.Id));
        if (n > 0) throw new CommandException($"„{c.Str("code") ?? c.Name}” holds {n} load{(n == 1 ? "" : "s")}: move or delete {(n == 1 ? "it" : "them")} first.");
    }

    static JsonObject LoadsJson(IModelView m, LoadsReport rep)
    {
        var cases = new JsonArray();
        foreach (var c in rep.Cases)
            cases.Add((JsonNode)new JsonObject
            {
                ["id"] = c.Id, ["code"] = c.Code, ["name"] = c.Name, ["actionType"] = c.ActionType, ["actionSource"] = c.ActionSource, ["category"] = c.Category,
                ["selfWeight"] = c.SelfWeight, ["derives"] = new JsonArray(c.Derives.Select(d => (JsonNode)d).ToArray()),
                ["psi"] = c.Variable ? c.Psi.ToJson() : null, ["windDirection"] = c.WindDirection, ["roofCpe"] = c.RoofCpe,
                ["userLoads"] = rep.UserLoads.Count(l => l.CaseId == c.Id), ["total"] = R(rep.Total(c)), ["empty"] = rep.Empty(c),
            });
        var storeys = new JsonArray();
        foreach (var s in rep.Model.Storeys)
        {
            var by = new JsonObject();
            foreach (var c in rep.Cases)
            {
                var t = rep.UserLoads.Where(l => l.CaseId == c.Id && Spatial.StoreyOf(m, l.Entity.Id)?.Id == s.Id).Sum(l => l.Vertical)
                    + rep.Derived.Where(d => d.CaseId == c.Id && d.StoreyId == s.Id).Sum(d => d.Total);
                by[c.Id] = R(t);
            }
            storeys.Add((JsonNode)new JsonObject { ["id"] = s.Id, ["name"] = s.Name, ["byCase"] = by });
        }
        // The snow's and the wind's many pieces (planes, zones, bands) a row a case and element: the largest value, the total summed.
        var rows = rep.Derived.Where(d => d.Kind is not (LoadCases.Snow or LoadCases.Wind))
            .Select(d => (d.Kind, d.CaseId, d.SourceId, d.SourceName, d.StoreyId, d.Value, d.Unit, d.Total))
            .Concat(rep.Derived.Where(d => d.Kind is LoadCases.Snow or LoadCases.Wind)
                .GroupBy(d => (d.Kind, d.CaseId, d.SourceId, Name: d.SourceId == Spatial.Project(m)?.Id ? "Façades" : d.SourceName.Split(", zone ")[0]))
                .Select(g => (g.Key.Kind, g.Key.CaseId, g.Key.SourceId, g.Key.Name, g.First().StoreyId, g.Max(d => d.Value), g.First().Unit, g.Sum(d => d.Total))));
        var derived = new JsonArray(rows.Select(d => (JsonNode)new JsonObject
        {
            ["kind"] = d.Kind, ["caseId"] = d.CaseId, ["sourceId"] = d.SourceId, ["sourceName"] = d.Item4, ["storeyId"] = d.StoreyId,
            ["value"] = R(d.Item6), ["unit"] = d.Unit, ["total"] = R(d.Item8),
        }).ToArray());
        var missing = new JsonArray(rep.Missing.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => (JsonNode)new JsonObject
        {
            ["category"] = x.Key, ["rooms"] = new JsonArray(x.Value.Select(id => (JsonNode)id).ToArray()),
        }).ToArray());
        return new JsonObject
        {
            ["cases"] = cases, ["storeys"] = storeys, ["derived"] = derived, ["missing"] = missing,
            ["notes"] = new JsonArray(rep.Notes.Select(n => (JsonNode)n).ToArray()),
        };
    }

    /// <summary>
    /// Every action of every case as IFC writes it, world metres and kN: the user's loads at their storey's level plus their
    /// height, the derived ones (a partition along its axis a segment at a time); the self weight is the case's own
    /// (SelfWeightCoefficients), no action.
    /// </summary>
    public static List<StructuralActionData> Actions(IModelView m, LoadsReport rep)
    {
        var o = new List<StructuralActionData>();
        var known = rep.Cases.Select(c => c.Id).ToHashSet();
        foreach (var l in rep.UserLoads.Where(l => l.CaseId != null && known.Contains(l.CaseId)))
        {
            var z = UserLoads.Level(m, l.Entity) + l.Z;
            o.Add(new StructuralActionData(l.Entity.Id, l.Entity.Name, l.CaseId!, l.ActionKind, l.Points.Select(p => p.At(z)).ToList(), l.Dir, l.Value, null, false));
        }
        foreach (var d in rep.Derived.Where(d => d.CaseId != null && d.Shape != "none"))
        {
            var down = d.Direction ?? new Vec3(0, 0, -1);
            if (d.Shape == "area")
                o.Add(new StructuralActionData(d.SourceId, $"{d.SourceName}: {d.Kind}", d.CaseId!, "planar", d.Points3 ?? d.Points.Select(p => p.At(d.Z)).ToList(), down, d.Value, d.SourceId, true, d.Key ?? d.Kind)
                { Holes = d.Holes.Select(h => (IReadOnlyList<Vec3>)h.Select(p => p.At(d.Z)).ToList()).ToList(), Projected = d.Projected });
            else
                for (var i = 0; i + 1 < d.Points.Count; i++)
                    o.Add(new StructuralActionData(d.SourceId, $"{d.SourceName}: {d.Kind}", d.CaseId!, "curve", [d.Points[i].At(d.Z), d.Points[i + 1].At(d.Z)], down, d.Value,
                        d.SourceId, true, d.Points.Count > 2 ? $"{d.Kind}#{i + 1}" : d.Kind));
        }
        return o;
    }

    // ── IStructuralLoads ──

    public StructuralLoadsData For(IQueryContext query, AnalyticalOptions? options = null)
    {
        var rep = LoadsReport.Compute(query, options);
        var (combinations, _) = Combinations.Of(rep);
        return new StructuralLoadsData(
            rep.Cases.Select(c => new StructuralLoadCaseData(c.Id, c.Code, c.Name, c.ActionType, c.ActionSource, c.SelfWeight)).ToList(),
            Actions(query.Model, rep), combinations.Select(Combinations.ToData).ToList());
    }

    // ── element.update and element.delete ──

    public void Updated(ICommandContext ctx, Entity before, Entity after)
    {
        if (UserLoads.Classes.Contains(after.Class)) UserLoads.Check(ctx.Model, after);
        else if (after.Class == LoadCases.Class)
        {
            // The case's attributes checked as a command's would be (its code unique, one self weight…); a load case's attributes only.
            var (_, attrs) = LoadCases.Write([], ctx.Model, after);
            if (attrs.ToJsonString() != after.Attrs.ToJsonString()) ctx.Replace(after.With(attrs: attrs));
        }
    }

    public bool Delete(ICommandContext ctx, string id) => false;

    public void Check(IModelView m, Entity e, IReadOnlySet<string> deleting)
    {
        if (e.Class == LoadCases.Class) RefuseInUse(m, e, deleting);
    }

    // ── Plan marks ──

    /// <summary>
    /// The user's loads on a plan of their storey: an area's outline with a sparse hatch at 45°, a line with short arrows, a
    /// point's arrow; each with its value, its unit and its case's code. A click on one selects the load.
    /// </summary>
    public IEnumerable<ViewMark> Marks(ViewContext c)
    {
        if (c.Kind != "plan" || c.View.Str("storeyId") is not { } storey) yield break;
        var m = c.Model;
        var k = c.Scale / 100;
        var text = Standards.ResolveText(c.Book, []);
        const string Layer = "LOADS";
        var codes = LoadCases.All(m).ToDictionary(x => x.Id, x => x.Code);
        foreach (var l in UserLoads.All(m))
        {
            if (l.Points.Count == 0 || Spatial.StoreyOf(m, l.Entity.Id)?.Id != storey) continue;
            var z = UserLoads.Level(m, l.Entity) + l.Z;
            Vec2 V(Vec2 p) => c.Map(p, z);
            var mk = new ViewMark(l.Entity.Id);
            var label = $"{UserLoads.Format(l.Value)} {UserLoads.Unit(l.Kind)}{(l.CaseId != null && codes.TryGetValue(l.CaseId, out var code) ? $" ({code})" : "")}";
            var dir = l.Dir;
            var flat = new Vec2(dir.X, dir.Y);
            switch (l.Kind)
            {
                case UserLoads.Area:
                {
                    var ring = l.Points.Select(V).ToArray();
                    mk.Polyline(ring, true, Layer, 0.35, UserLoads.Color);
                    // The hatch: lines at 45°, 1 m apart at 1:100.
                    var (lo, hi) = (new Vec2(ring.Min(p => p.X), ring.Min(p => p.Y)), new Vec2(ring.Max(p => p.X), ring.Max(p => p.Y)));
                    var step = 1.0 * k;
                    var spans = new List<(double, double)>();
                    for (var s = lo.X - (hi.Y - lo.Y); s <= hi.X; s += step)
                    {
                        var (a, b) = (new Vec2(s, lo.Y), new Vec2(s + (hi.Y - lo.Y), hi.Y));
                        spans.Clear();
                        Polygon2.InsideIntervals([ring], a, b, 0, 1, spans);
                        foreach (var (t0, t1) in spans) mk.Line(a + (b - a) * t0, a + (b - a) * t1, Layer, 0.13, UserLoads.Color);
                    }
                    var at = Polygon2.InteriorPoint(ring);
                    mk.Text(new PenText(at, label, text.Height, Anchor: "middle", Font: text.Font), UserLoads.Color);
                    mk.Hit = ([ring], false);
                    break;
                }
                case UserLoads.Line:
                {
                    var (a, b) = (V(l.Points[0]), V(l.Points[1]));
                    mk.Line(a, b, Layer, 0.5, UserLoads.Color);
                    var along = (b - a).Normalized();
                    var arrow = flat.Length > 0.5 ? flat.Normalized() : -along.Perp;
                    for (var i = 0; i <= 4; i++) Arrow(mk, a + (b - a) * (i / 4.0), arrow, 0.4 * k, Layer);
                    mk.Text(new PenText((a + b) * 0.5 + along.Perp * (0.6 * k), label, text.Height, Anchor: "middle", Font: text.Font), UserLoads.Color);
                    mk.Edges.Add((a, b));
                    break;
                }
                default:
                {
                    var p = V(l.Points[0]);
                    var arrow = flat.Length > 0.5 ? flat.Normalized() : new Vec2(-1, -1).Normalized();
                    Arrow(mk, p, arrow, 0.8 * k, Layer);
                    mk.Text(new PenText(p - arrow * (0.9 * k), label, text.Height, Anchor: "start", Font: text.Font), UserLoads.Color);
                    mk.Edges.Add((p - arrow * (0.8 * k), p));
                    break;
                }
            }
            mk.Snaps.AddRange(l.Points.Select(V));
            yield return mk;
        }
    }

    /// <summary>An arrow on a plan ending at <paramref name="tip"/>, pointing along <paramref name="dir"/>.</summary>
    static void Arrow(ViewMark mk, Vec2 tip, Vec2 dir, double length, string layer)
    {
        var tail = tip - dir * length;
        mk.Line(tail, tip, layer, 0.25, UserLoads.Color);
        var barb = length * 0.3;
        mk.Line(tip, tip - dir * barb + dir.Perp * (barb * 0.5), layer, 0.25, UserLoads.Color);
        mk.Line(tip, tip - dir * barb - dir.Perp * (barb * 0.5), layer, 0.25, UserLoads.Color);
    }
}
