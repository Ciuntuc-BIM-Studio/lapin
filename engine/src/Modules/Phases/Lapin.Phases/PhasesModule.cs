using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Phases;

/// <summary>
/// Phases: the project's phases and filters (on the IfcProject), each
/// element's created and demolished phase (its <c>phaseCreated</c> and
/// <c>phaseDemolished</c>), and each view's phase and filter. Views leave out
/// what their phase does not see or their filter hides, and draw overridden
/// states in their colour.
/// </summary>
public sealed class PhasesModule : ILapinModule, IViewElementFilter
{
    public string Id => "phases";
    public string Name => "Phases";

    public static PhaseSettings Settings(IModelView m) => PhaseSettings.Read(Spatial.Project(m)?.Attrs["phases"]) ?? PhaseSettings.Default;

    /// <summary>An element's state and how a filter shows it, seen from a phase; null state: not there.</summary>
    public static (string? State, string Presentation) Look(PhaseSettings s, Entity e, string phase, string? filter)
    {
        var state = s.State(e.Str("phaseCreated"), e.Str("phaseDemolished"), phase);
        return (state, state == null ? "hide" : s.Presentation(filter, state));
    }

    /// <summary>Only what is built: elements on a storey (walls, floors, roofs…), not types, materials or views.</summary>
    static bool Phased(IModelView m, Entity e) => Spatial.StoreyOf(m, e.Id) != null;

    public (bool Hide, string? Color)? For(IModelView m, Entity view, Entity e)
    {
        if (!Phased(m, e)) return null;
        var s = Settings(m);
        var phase = view.Str("phaseId") ?? s.ActivePhase?.Id;
        if (phase == null) return null;
        var (state, presentation) = Look(s, e, phase, view.Str("phaseFilterId"));
        if (presentation == "hide") return (true, null);
        if (presentation == "override" && state != null) return (false, s.Overrides[state].Color);
        return null;
    }

    static void Write(ICommandContext ctx, PhaseSettings s)
    {
        var project = Spatial.Project(ctx.Model) ?? throw new CommandException("The model has no project.");
        var attrs = project.CloneAttrs();
        attrs["phases"] = s.ToJson();
        ctx.Replace(project.With(attrs: attrs));
    }

    public void Register(IModuleRegistry r)
    {
        r.Service<IViewElementFilter>(this);

        r.Command("addPhase", "New phase", (ctx, a) =>
        {
            var s = Settings(ctx.Model);
            var name = ((string?)a["name"])?.Trim();
            if (string.IsNullOrEmpty(name)) throw new CommandException("Every phase needs a name.");
            var kind = (string?)a["kind"] ?? "construction";
            if (!PhaseSettings.Kinds.Contains(kind)) throw new CommandException("A phase is existing, construction or execution.");
            var id = PhaseSettings.FreshId(name, s.Phases.Select(p => p.Id));
            var phases = s.Phases.ToList();
            var at = (string?)a["after"] is { } after && phases.FindIndex(p => p.Id == after) is var i && i >= 0 ? i + 1 : phases.Count;
            phases.Insert(at, new ProjectPhase(id, name, kind));
            Write(ctx, s with { Phases = phases });
            return new JsonObject { ["id"] = id };
        });

        r.Command("updatePhase", "Change phase", (ctx, a) =>
        {
            var s = Settings(ctx.Model);
            var id = (string?)a["id"];
            if (s.Phases.All(p => p.Id != id)) throw new CommandException("There is no such phase.");
            var phases = s.Phases.Select(p => p.Id != id ? p : p with
            {
                Name = ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : p.Name,
                Kind = (string?)a["kind"] is { } k && PhaseSettings.Kinds.Contains(k) ? k : p.Kind,
            }).ToList();
            Write(ctx, s with { Phases = phases });
            return null;
        });

        // A phase goes: what was created or demolished in it falls back to no phase.
        r.Command("removePhase", "Remove phase", (ctx, a) =>
        {
            var s = Settings(ctx.Model);
            var id = (string?)a["id"];
            if (s.Phases.Count <= 1) throw new CommandException("A project needs at least one phase.");
            if (s.Phases.All(p => p.Id != id)) throw new CommandException("There is no such phase.");
            foreach (var e in ctx.Model.Entities.Where(e => e.Str("phaseCreated") == id || e.Str("phaseDemolished") == id).ToList())
            {
                var attrs = e.CloneAttrs();
                if (e.Str("phaseCreated") == id) attrs.Remove("phaseCreated");
                if (e.Str("phaseDemolished") == id) attrs.Remove("phaseDemolished");
                ctx.Replace(e.With(attrs: attrs));
            }
            Write(ctx, s with { Phases = s.Phases.Where(p => p.Id != id).ToList(), Active = s.Active == id ? null : s.Active });
            return null;
        });

        // The phase new work goes in and the 3D shows; the filter the 3D applies.
        r.Command("setActive", "Active phase", (ctx, a) =>
        {
            var s = Settings(ctx.Model);
            var phase = a.ContainsKey("phase") ? (string?)a["phase"] : s.Active;
            var filter = a.ContainsKey("filter") ? (string?)a["filter"] : s.ActiveFilter;
            if (phase != null && s.Phases.All(p => p.Id != phase)) throw new CommandException("There is no such phase.");
            if (filter != null && s.Filters.All(f => f.Id != filter)) throw new CommandException("There is no such filter.");
            Write(ctx, s with { Active = phase, ActiveFilter = filter });
            return null;
        });

        // Elements created in a phase, demolished in one (null: not demolished); refused when demolished before built.
        r.Command("assign", "Phase", (ctx, a) =>
        {
            var s = Settings(ctx.Model);
            var ids = a["ids"] is JsonArray arr ? arr.Select(x => (string?)x).OfType<string>().ToList() : [(string?)a["id"] ?? ""];
            string? Check(string? id) => id == null || s.Phases.Any(p => p.Id == id) ? id : throw new CommandException($"There is no phase „{id}”.");
            int Index(string? id) => s.Phases.ToList().FindIndex(p => p.Id == id);
            foreach (var id in ids)
            {
                var e = ctx.Model.Get(id) ?? throw new CommandException($"There is no {id}.");
                var attrs = e.CloneAttrs();
                if (a.ContainsKey("created")) { if (Check((string?)a["created"]) is { } c) attrs["phaseCreated"] = c; else attrs.Remove("phaseCreated"); }
                if (a.ContainsKey("demolished")) { if (Check((string?)a["demolished"]) is { } d) attrs["phaseDemolished"] = d; else attrs.Remove("phaseDemolished"); }
                var (ci, di) = (Index((string?)attrs["phaseCreated"] ?? s.DefaultCreated?.Id), Index((string?)attrs["phaseDemolished"]));
                if (di >= 0 && di < ci) throw new CommandException($"„{e.Name}” cannot be demolished before it is built.");
                ctx.Replace(e.With(attrs: attrs));
            }
            return null;
        });

        // A view's phase and filter (null: the active phase, no filter).
        r.Command("viewPhase", "View phase", (ctx, a) =>
        {
            var v = ctx.Model.Get((string?)a["id"] ?? "") is { Class: Views.Class } x ? x : throw new CommandException("Not a view.");
            var attrs = v.CloneAttrs();
            foreach (var (key, attr) in new[] { ("phase", "phaseId"), ("filter", "phaseFilterId") })
                if (a.ContainsKey(key)) { if ((string?)a[key] is { } val) attrs[attr] = val; else attrs.Remove(attr); }
            ctx.Replace(v.With(attrs: attrs));
            return null;
        });

        r.Query("settings", (q, _) => Settings(q.Model).ToJson());

        // What the 3D shows: each built element whose filter does not show it as it is, its state, and its override.
        r.Query("look", (q, a) =>
        {
            var s = Settings(q.Model);
            var phase = (string?)a["phase"] ?? s.ActivePhase?.Id ?? "";
            var filter = a.ContainsKey("filter") ? (string?)a["filter"] : s.ActiveFilter;
            var o = new JsonObject();
            foreach (var e in q.Model.Entities.Where(e => Phased(q.Model, e)))
            {
                var (state, presentation) = Look(s, e, phase, filter);
                if (presentation == "show") continue;
                var x = new JsonObject { ["state"] = state, ["presentation"] = presentation };
                if (presentation == "override" && state != null) { x["color"] = s.Overrides[state].Color; x["opacity"] = s.Overrides[state].Opacity; }
                o[e.Id] = x;
            }
            return o;
        });
    }
}
