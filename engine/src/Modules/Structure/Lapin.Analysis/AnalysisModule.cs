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
/// analysis.run, analysis.results, analysis.diagram) and their end releases (analysis.setReleases). Stage S2 (§10): slabs
/// and walls as shells. Stage S3 (§11): the masonry walls as shells, the modal analysis and the response spectrum by P100-1/2013
/// or EN 1998-1 (analysis.setSeismic, analysis.modal, analysis.mode, analysis.spectrum, analysis.seismic; the envelope „seismic”). Stage S5
/// (§14): second order (P-Δ) and linear buckling (analysis.buckling, analysis.bucklingMode), supports on springs and Winkler's bed
/// (analysis.setSupport), one-way members and supports that lift (analysis.setOnly). It reads rooms, slabs and
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
        r.Class(new(UserCombinations.Class, "Load combination",
        [
            new("kind", "Kind", "choice", Choices: UserCombinations.Kinds),
        ], "#c2410c", Kind: "resource"));
        UserLoads.Register(r);
        r.Service<INewProject>(new CombinationSettings.NewProject());
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
            var (combinations, notes) = Combinations.Of(LoadsReport.Compute(q), q.Model);
            return new JsonObject
            {
                ["combinations"] = new JsonArray(combinations.Select(c => (JsonNode)Combinations.ToJson(c)).ToArray()),
                ["options"] = CombinationSettings.Of(q.Model).ToJson(),
                ["kinds"] = new JsonArray(UserCombinations.Kinds.Select(k => (JsonNode)k).ToArray()),
                ["notes"] = new JsonArray(notes.Select(n => (JsonNode)n).ToArray()),
            };
        });

        // ── Combinations (§13) ──

        // {set?: en | cr0, gammaGSup?, gammaGInf?, gammaQ?, psi?: {category: {0, 1, 2} | null}, expression?: 6.10 | 6.10ab, xi?}:
        // the project's combination factors; null back to the set's. One undo step.
        r.Command("setCombinationOptions", "Combination factors", (ctx, a) => { CombinationSettings.Write(ctx, a); return null; });

        // {key, enabled}: a combination switched off (left out of results, envelopes, checks and export) or on again. One undo step.
        r.Command("setCombinationEnabled", "Switch combination", (ctx, a) =>
        {
            var key = ((string?)a["key"])?.Trim();
            if (string.IsNullOrEmpty(key)) throw new CommandException("Say the combination's key.");
            if (a["enabled"] is not JsonValue v || !v.TryGetValue<bool>(out var enabled)) throw new CommandException("Say enabled: true or false.");
            CombinationSettings.Enable(ctx, key, enabled);
            return null;
        });

        // {name, kind, factors: [{caseId, factor}]}: a combination of the user's. One undo step.
        r.Command("addCombination", "New combination", (ctx, a) =>
        {
            var (name, attrs) = UserCombinations.Write(a, ctx.Model, null);
            var e = new Entity(ctx.NewId(), UserCombinations.Class, name, attrs);
            ctx.Add(e);
            return new JsonObject { ["id"] = e.Id, ["key"] = UserCombinations.KeyPrefix + e.Id };
        });

        // {id, name?, kind?, factors?}: a user's combination changed. One undo step.
        r.Command("updateCombination", "Change combination", (ctx, a) =>
        {
            var e = ctx.Model.Get((string?)a["id"] ?? "") is { Class: UserCombinations.Class } x ? x : throw new CommandException("There is no such combination of yours.");
            var (name, attrs) = UserCombinations.Write(a, ctx.Model, e);
            ctx.Replace(e.With(name: name, attrs: attrs));
            return null;
        });

        // {id}: a user's combination removed (and its key off the switched-off list). One undo step.
        r.Command("removeCombination", "Remove combination", (ctx, a) =>
        {
            var e = ctx.Model.Get((string?)a["id"] ?? "") is { Class: UserCombinations.Class } x ? x : throw new CommandException("There is no such combination of yours.");
            ctx.Remove(e.Id);
            if (CombinationSettings.Of(ctx.Model).Disabled.Contains(UserCombinations.KeyPrefix + e.Id)) CombinationSettings.Enable(ctx, UserCombinations.KeyPrefix + e.Id, true);
            return null;
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
        // {shells?: both | slabs | walls | none, meshSize?: metres, secondOrder?: bool, imperfections?: bool}: how the analysis takes
        // slabs and walls, whether its static combinations are solved to second order (P-Δ, §14.3) and whether the ultimate ones take
        // the global sway imperfections ±X, ±Y (EN 1993-1-1 5.3.2, §14.14; second order switched on brings them unless the project
        // said otherwise); null back to the default. One undo step.
        r.Command("setOptions", "Analysis options", (ctx, a) => { AnalysisSettings.Write(ctx, a); return null; });

        // ── Second order, springs, one way (S5, §14) ──

        // {ids, springs?: [kx, ky, kz, krx, kry, krz] (kN/m, kN·m/rad; null: as held, 0: free) | null, winkler?: ks (kN/m³) | null,
        // compressionOnly?: bool | null}: a footing's, a column base's or a slab on the ground's springs (§14.2). One undo step.
        r.Command("setSupport", "Support springs", (ctx, a) => { SupportSettings.Write(ctx, a); return null; });
        // {id}: what a support says and what it comes to: its springs, ks, the nodes it holds and each one's stiffness; the typical ks (indicative).
        r.Query("supportOptions", (q, a) => SupportJson(q, (string?)a["id"]));
        // {ids, only: tension | compression | null}: bars that work one way only (§14.4: X bracing, ties, struts); null both ways. One undo step.
        r.Command("setOnly", "Tension or compression only", (ctx, a) =>
        {
            var ids = Args.Ids(a);
            if (ids.Count == 0) throw new CommandException("Select the bars.");
            if (!a.ContainsKey("only")) throw new CommandException("Say only: tension, compression or null (both ways).");
            var only = a["only"] is null ? null : (string?)a["only"] is { } o && OneWay.Choices.Contains(o) ? o : throw new CommandException("Only is tension, compression or null.");
            foreach (var id in ids)
            {
                var e = ctx.Model.Get(id) ?? throw new CommandException($"There is no {id}.");
                if (UserLoads.Classes.Contains(e.Class) || e.Class == LoadCases.Class || e.Class == UserCombinations.Class) throw new CommandException("Only a bar works one way.");
                var attrs = e.CloneAttrs();
                if (only != null) attrs[OneWay.Key] = only; else attrs.Remove(OneWay.Key);
                ctx.Replace(e.With(attrs: attrs));
            }
            return null;
        });
        // {ids?, purlins?: true, sheeting: true | null}: bars their sheeting braces (§14.15: a purlin, a wall rail held straight across in
        // the sheeting's plane between the members it rests on); purlins: every member a framed roof names as its cladding's carrier
        // (a steel hall's purlins). One undo step.
        r.Command("setSheeting", "Braced by sheeting", (ctx, a) =>
        {
            if (!a.ContainsKey("sheeting")) throw new CommandException("Say sheeting: true (braced) or null.");
            var on = a["sheeting"] is null ? false : a["sheeting"] is JsonValue v && v.TryGetValue<bool>(out var b) ? b : throw new CommandException("Sheeting is true or null.");
            var ids = Args.Ids(a).ToList();
            if (a["purlins"] is JsonValue pv && pv.TryGetValue<bool>(out var purlins) && purlins)
                ids.AddRange(AnalyticalModel.Build(ctx.Model, ctx.Services, LoadsReport.Options).Roofs
                    .SelectMany(x => x.Members?.Where(kv => kv.Value == "purlin").Select(kv => kv.Key) ?? []));
            ids = ids.Distinct().ToList();
            if (ids.Count == 0) throw new CommandException(a.ContainsKey("purlins") ? "No framed roof names its purlins." : "Select the bars.");
            foreach (var id in ids)
            {
                var e = ctx.Model.Get(id) ?? throw new CommandException($"There is no {id}.");
                if (UserLoads.Classes.Contains(e.Class) || e.Class == LoadCases.Class || e.Class == UserCombinations.Class) throw new CommandException("Only a bar is braced by sheeting.");
                if (SheetingSettings.Of(e) == on) continue;
                var attrs = e.CloneAttrs();
                if (on) attrs[SheetingSettings.Key] = true; else attrs.Remove(SheetingSettings.Key);
                ctx.Replace(e.With(attrs: attrs));
            }
            return new JsonObject { ["members"] = ids.Count };
        });
        // {combination?, modes?}: each ultimate combination's critical load factors αcr, their class by EN 1993-1-1 5.2.1(3) and the amplifier 1/(1 − 1/αcr).
        r.Query("buckling", (q, a) => AnalysisRun.For(q).BucklingJson((string?)a["combination"], Json.Number(a["modes"]) is { } m ? (int)m : 6, q.Cancellation));
        // {combination, mode?, scale?}: a buckling mode's shape for the 3D view.
        r.Query("bucklingMode", (q, a) => AnalysisRun.For(q).BucklingModeJson((string?)a["combination"] ?? throw new CommandException("Say the combination."),
            Json.Number(a["mode"]) is { } n ? (int)n : 1, a["scale"] is JsonNode sn && Json.Number(sn) is { } sv && double.IsFinite(sv) ? sv : null));

        // ── Modal and seismic analysis (S3, §11) ──

        // {code?, ag?, tc?, ground?, spectrum?, importance?, q?, damping?, modal?, directions?, torsion?, modes?, cracked?, diaphragm?,
        // drift?, snowPsi2?}: the project's seismic parameters (P100-1/2013 by default, EN 1998-1 the option; the site's ag and TC
        // the user's); null takes one back to its default (ag null: no seismic analysis). One undo step.
        r.Command("setSeismic", "Seismic parameters", (ctx, a) => { Seismic.SeismicSettings.Write(ctx, a); return null; });
        r.Query("seismicOptions", (q, _) => Seismic.SeismicSettings.Of(q.Model).OptionsJson());
        // The natural modes: periods, participation factors, effective masses (§11.2).
        r.Query("modal", (q, _) => Seismic.SeismicRun.Of(q).ModalJson());
        // {mode, scale?}: a mode's shape for the 3D view (bars' polylines, shells' nodes displaced).
        r.Query("mode", (q, a) => Seismic.SeismicRun.Of(q).ModeShape(Json.Number(a["mode"]) is { } n ? (int)n : 1,
            a["scale"] is JsonNode sn && Json.Number(sn) is { } sv && double.IsFinite(sv) ? sv : null));
        // The design and elastic spectra, the modes on them.
        r.Query("spectrum", (q, _) => Seismic.SeismicRun.Of(q).SpectrumJson());
        // The storeys' masses, shears, forces and drifts with the drift checks; the base shear (§11.3).
        r.Query("seismic", (q, _) => Seismic.SeismicRun.Of(q).SeismicJson());

        // ── Design checks (S4, §12) ──

        // {material?, storeyId?}: every bar's, slab's and masonry wall's utilisation, its governing check and combination (indicative).
        r.Query("checks", (q, a) => Design.DesignRun.Of(q).ChecksJson((string?)a["material"], (string?)a["storeyId"]));
        // {id}: one element's checks in detail, each with its clause and values.
        r.Query("check", (q, a) => Design.DesignRun.Of(q).CheckJson((string?)a["id"] ?? throw new CommandException("Say the element (id).")));
        // The masonry walls' forces at their feet and their resistances, storey by storey (CR 6-2013).
        r.Query("walls", (q, _) => Design.DesignRun.Of(q).WallsJson(q.Model));
        // {id?}: the reinforcement the concrete bars and surfaces need, with what their types provide.
        r.Query("reinforcement", (q, a) => Design.DesignRun.Of(q).ReinforcementJson((string?)a["id"]));
        r.Query("designOptions", (q, _) => Design.DesignSettings.ToJson(q.Model));
        // {ids?, ky?, kz?, lcrY?, lcrZ?, llt?, serviceClass?, system?} on elements, {serviceClass?, cover?, bar?, stirrup?, fyk?, fvk0?, gammaM?}
        // or {steelInteraction?: A | B, snowDuration?: short | medium} on the project; null back to the default. One undo step.
        r.Command("setDesign", "Design values", (ctx, a) => { Design.DesignSettings.Write(ctx, a); return null; });

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

    /// <summary>A support's settings and what they come to on its nodes (§14.2).</summary>
    static JsonObject SupportJson(IQueryContext q, string? id)
    {
        var e = q.Model.Get(id ?? "") ?? throw new CommandException("Say the support (id).");
        if (!SupportSettings.Classes.Contains(e.Class)) throw new CommandException($"„{e.Name}” is no footing, column base or slab.");
        var st = SupportSettings.Of(e);
        var o = new JsonObject
        {
            ["id"] = e.Id, ["name"] = e.Name, ["kind"] = e.Class == IfcClass.Slab ? "slab" : e.Class == IfcClass.Footing ? "footing" : "base",
            ["own"] = st?.ToJson(), ["typical"] = SupportSettings.Typical(), ["indicative"] = true,
        };
        var run = AnalysisRun.Of(q);
        var p = run.Problem.Problem;
        var nodes = new JsonArray();
        foreach (var l in p.Links.Where(l => l.Ground && (l.Id.StartsWith($"support:{e.Id}:") || l.Id.StartsWith($"slab:{e.Id}:"))))
        {
            var n = l.Nodes[0];
            nodes.Add((JsonNode)new JsonObject
            {
                ["node"] = p.NodeIds[n], ["at"] = new JsonArray(R(p.Nodes[n].X), R(p.Nodes[n].Y), R(p.Nodes[n].Z)),
                ["k"] = new JsonArray(Enumerable.Range(0, 6).Select(d => (JsonNode)R(l.K[d * 6 + d])).ToArray()),
                ["held"] = p.Held[n] is { } h ? new JsonArray(h.Select(x => (JsonNode)x).ToArray()) : null,
                ["compressionOnly"] = p.Uplift.Contains(n),
            });
        }
        o["nodes"] = nodes;
        return o;
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
        var (combinations, _) = Combinations.Of(rep, query.Model);
        // The active ones; a seismic one is not written (AEd is no load case).
        return new StructuralLoadsData(
            rep.Cases.Select(c => new StructuralLoadCaseData(c.Id, c.Code, c.Name, c.ActionType, c.ActionSource, c.SelfWeight)).ToList(),
            Actions(query.Model, rep), Combinations.Static(combinations).Select(Combinations.ToData).ToList());
    }

    // ── element.update and element.delete ──

    public void Updated(ICommandContext ctx, Entity before, Entity after)
    {
        if (UserLoads.Classes.Contains(after.Class)) UserLoads.Check(ctx.Model, after);
        else if (after.Class == UserCombinations.Class)
        {
            var (_, attrs) = UserCombinations.Write([], ctx.Model, after);
            if (attrs.ToJsonString() != after.Attrs.ToJsonString()) ctx.Replace(after.With(attrs: attrs));
        }
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
