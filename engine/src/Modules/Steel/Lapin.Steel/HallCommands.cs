using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Steel;

/// <summary>
/// The hall configurator's commands (decision 270): a hall added or changed as one step from a draft (a style, then the
/// fields changed by hand: <c>{id?, storeyId?, style?, attrs}</c>), its preview, the haunch of one member set on its own,
/// and the project's own hall styles beside the catalogue's.
/// </summary>
public static class HallCommands
{
    public static void Register(IModuleRegistry r)
    {
        r.Class(new(Halls.Class, "Portal frame hall", [], "#8a96a3", Kind: "resource"));
        r.Catalog("hallStyles", new JsonArray([.. HallStyles.Catalogue.Select(x => (JsonNode)x.ToJson())]));

        // A new hall on a storey, from a style and the fields given.
        r.Command("addHall", "Portal frame hall", (ctx, a) =>
        {
            var storey = Spatial.Require(ctx.Model, (string?)a["storeyId"] ?? Spatial.Storeys(ctx.Model).FirstOrDefault()?.Id, IfcClass.BuildingStorey);
            var (spec, style) = Draft(ctx.Model, null, a);
            return new JsonObject { ["id"] = Halls.Add(ctx, storey, spec.Check(), style).Id };
        });

        // The configurator's Apply on a hall: its spec from the draft, its members built again (keeping their ids).
        r.Command("updateHall", "Change hall", (ctx, a) =>
        {
            var hall = Spatial.Require(ctx.Model, (string?)a["id"], Halls.Class);
            var (spec, style) = Draft(ctx.Model, hall, a);
            Halls.Update(ctx, hall, spec.Check(), style);
            return null;
        });

        // What the draft builds (a hall's, or a new one's): {frame, gable?, plan, axes, summary} or {problem}.
        r.Query("hallPreview", (q, a) =>
        {
            var hall = (string?)a["id"] is { } id ? Spatial.Require(q.Model, id, Halls.Class) : null;
            var (spec, _) = Draft(q.Model, hall, a);
            try { spec.Check(); }
            catch (CommandException e) { return new JsonObject { ["problem"] = e.Message, ["attrs"] = spec.ToJson() }; }
            var o = Halls.Preview(spec);
            o["attrs"] = spec.ToJson();
            return o;
        });

        // One haunch of a member set on its own: {id, end: start|end, haunch: {length, depth, kind, web?, flange?, width?} | null}.
        // A hall built again keeps it; null takes it off (a hall's rafter takes the hall's again when the hall is changed).
        r.Command("setHaunch", "Haunch", (ctx, a) =>
        {
            var e = ctx.Model.Get((string?)a["id"] ?? "") is { Class: SteelModule.Member } x ? x : throw new CommandException("Only a member takes a haunch.");
            var which = (string?)a["end"] is "start" or "end" ? (string)a["end"]! : throw new CommandException("A haunch is at the member's start or its end.");
            var h = Haunch.Parse(a["haunch"]);
            var (s, t) = Haunch.Of(e);
            if (which == "start") s = h == null ? null : h with { Own = true }; else t = h == null ? null : h with { Own = true };
            var attrs = e.CloneAttrs();
            if (Haunch.Json2(s, t) is { } hj) attrs[Haunch.Key] = hj; else attrs.Remove(Haunch.Key);
            var next = e.With(attrs: attrs);
            // Refused when it does not fit under the member.
            if (h != null && MemberAxes.Of(ctx.Model, next) is { } ax)
            {
                var (es, ee) = SteelEnd.Of(next);
                var problems = new List<string>();
                Haunches.Of(next, ax, SteelBody.Build(ctx.Model, ax, es, ee, null), problems);
                if (problems.Count > 0) throw new CommandException(problems[0]);
            }
            ctx.Replace(next);
            return null;
        });

        // The project's own hall styles: the draft saved under a name (a new style, or the one of that name or id saved
        // again, which its halls take again), renamed, deleted (its halls kept as they are).
        r.Command("saveHallStyle", "Save hall style", (ctx, a) =>
        {
            var hall = (string?)a["id"] is { } id ? Spatial.Require(ctx.Model, id, Halls.Class) : null;
            var (spec, _) = Draft(ctx.Model, hall, a);
            var sid = HallStyles.Save(ctx, spec.Check(), (string?)a["name"] ?? "", (string?)a["styleId"]);
            if (hall != null) Halls.Update(ctx, ctx.Model.Get(hall.Id)!, spec, sid);
            return new JsonObject { ["styleId"] = sid };
        });
        r.Command("renameHallStyle", "Rename hall style", (ctx, a) => { HallStyles.Rename(ctx, (string?)a["styleId"] ?? "", (string?)a["name"] ?? ""); return null; });
        r.Command("deleteHallStyle", "Delete hall style", (ctx, a) => { HallStyles.Delete(ctx, (string?)a["styleId"] ?? ""); return null; });
    }

    /// <summary>
    /// A draft's spec: the style's (the catalogue's or the project's) over the hall's own placement, else the hall as it is
    /// (else the default); then the fields given by hand. The style the hall will say it has: the one picked, else the one it
    /// had when nothing was changed by hand.
    /// </summary>
    public static (HallSpec Spec, string? Style) Draft(IModelView m, Entity? hall, JsonObject a)
    {
        var current = hall != null ? Halls.SpecOf(hall) : HallSpec.Default;
        var attrs = a["attrs"] as JsonObject ?? [];
        var styleId = (string?)a["style"];
        var spec = current;
        if (styleId != null)
        {
            var style = HallStyles.Of(m, styleId) ?? throw new CommandException($"There is no hall style „{styleId}”.");
            spec = HallSpec.From(style, current);
        }
        spec = HallSpec.From(attrs, spec);
        var kept = styleId ?? (attrs.Count == 0 ? hall?.Str("style") : null);
        return (spec, kept);
    }
}

/// <summary>
/// Hall styles (decision 270): the catalogue's few typical halls, and the project's own (<c>hallStyles</c> on the project,
/// ids starting <c>own:</c>, like the roof styles of decision 233). A style carries every field of a spec but where the hall
/// stands.
/// </summary>
public static class HallStyles
{
    public const string Key = "hallStyles";
    public const string Prefix = "own:";

    public sealed record Style(string Id, string Label, HallSpec Spec, string? Description = null)
    {
        public JsonObject ToJson()
        {
            var o = new JsonObject { ["id"] = Id, ["label"] = Label, ["attrs"] = Fields(Spec) };
            if (Description != null) o["description"] = Description;
            return o;
        }
    }

    static Lapin.Structure.Section P(string name) => Lapin.Structure.Section.Catalogue[name];

    /// <summary>
    /// A typical 20 m hall (six bays of 6 m, 6 m eaves, 10°, IPE 400 columns, IPE 360 rafters, haunches cut from the
    /// rafter, gable posts), a 30 m one (8 m eaves, 8°, IPE 600 and IPE 500, welded haunches, frames at the gables), and
    /// a 10 m mono-pitch lean-to.
    /// </summary>
    public static readonly IReadOnlyList<Style> Catalogue =
    [
        new("hall-20", "Hall 20 m", HallSpec.Default, "20 m span, 6 m bays, 6 m eaves, 10°"),
        new("hall-30", "Hall 30 m", HallSpec.Default with
        {
            Span = 30, Bays = [6, 6, 6, 6, 6, 6, 6, 6], Eaves = 8, Pitch = 8, Column = P("IPE 600"), Rafter = P("IPE 500"),
            EavesHaunch = new Haunch(3, 0.5, "plates", 0.012, 0.016, 0.2), ApexHaunch = new Haunch(1.5, 0.25, "plates", 0.012, 0.016, 0.2),
            Gable = "frame", Post = P("IPE 300"), GableRafter = P("IPE 300"), Base = "fixed", Bracing = [1, 8],
        }, "30 m span, 6 m bays, 8 m eaves, 8°"),
        new("lean-to", "Lean-to 10 m", HallSpec.Default with
        {
            Span = 10, Bays = [5, 5, 5, 5], Eaves = 4, Pitch = 5, Roof = "mono", Column = P("HEA 240"), Rafter = P("IPE 300"),
            EavesHaunch = new Haunch(1, 0.3), ApexHaunch = null, Gable = "posts", PostSpacing = 5, Post = P("IPE 200"), GableRafter = P("IPE 200"), Bracing = [1],
        }, "Mono-pitch, 10 m span, 5 m bays, 4 m eaves, 5°"),
    ];

    /// <summary>A spec's fields as a style keeps them (without where the hall stands).</summary>
    public static JsonObject Fields(HallSpec s)
    {
        var o = s.ToJson();
        foreach (var k in HallSpec.Placement) o.Remove(k);
        return o;
    }

    public static bool IsOwn(string? id) => id?.StartsWith(Prefix, StringComparison.Ordinal) == true;

    public static List<JsonObject> Own(IModelView m) => (Spatial.Project(m)?.Attrs[Key] as JsonArray)?.OfType<JsonObject>().ToList() ?? [];

    /// <summary>A style's fields by its id: the project's own, or the catalogue's.</summary>
    public static JsonObject? Of(IModelView m, string? id) =>
        IsOwn(id) ? Own(m).FirstOrDefault(s => (string?)s["id"] == id)?["attrs"] as JsonObject
        : Catalogue.FirstOrDefault(s => s.Id == id) is { } c ? Fields(c.Spec) : null;

    /// <summary>Saves a spec as a project style under a name: a new one, or the one of that name (or id) saved again, every hall of it built again. Its id.</summary>
    public static string Save(ICommandContext ctx, HallSpec spec, string label, string? id)
    {
        label = label.Trim();
        if (label.Length == 0) throw new CommandException("A hall style needs a name.");
        var project = Spatial.Project(ctx.Model) ?? throw new CommandException("The model has no project.");
        var list = Own(ctx.Model);
        var same = list.FirstOrDefault(s => id != null ? (string?)s["id"] == id : string.Equals((string?)s["label"], label, StringComparison.OrdinalIgnoreCase));
        if (id != null && same == null) throw new CommandException("The project has no such hall style.");
        var sid = (string?)same?["id"] ?? Prefix + ctx.NewId();
        var style = new JsonObject { ["id"] = sid, ["label"] = label, ["attrs"] = Fields(spec) };
        var next = list.Select(s => (string?)s["id"] == sid ? style : (JsonNode)s.DeepClone()).ToList();
        if (same == null) next.Add(style);
        var pa = project.CloneAttrs();
        pa[Key] = new JsonArray([.. next]);
        ctx.Replace(project.With(attrs: pa));
        // Every hall of the style takes it again, where it stands.
        foreach (var h in ctx.Model.OfClass(Halls.Class).Where(h => h.Str("style") == sid).ToList())
            Halls.Update(ctx, h, HallSpec.From(Fields(spec), Halls.SpecOf(h)).Check(), sid);
        return sid;
    }

    public static void Rename(ICommandContext ctx, string id, string label)
    {
        label = label.Trim();
        if (label.Length == 0) throw new CommandException("A hall style needs a name.");
        var project = Spatial.Project(ctx.Model) ?? throw new CommandException("The model has no project.");
        var list = Own(ctx.Model);
        if (list.All(s => (string?)s["id"] != id)) throw new CommandException("The project has no such hall style.");
        var pa = project.CloneAttrs();
        pa[Key] = new JsonArray([.. list.Select(s => { var c = (JsonObject)s.DeepClone(); if ((string?)c["id"] == id) c["label"] = label; return (JsonNode)c; })]);
        ctx.Replace(project.With(attrs: pa));
    }

    /// <summary>Deletes a project style; its halls keep how they are.</summary>
    public static void Delete(ICommandContext ctx, string id)
    {
        var project = Spatial.Project(ctx.Model) ?? throw new CommandException("The model has no project.");
        var list = Own(ctx.Model);
        if (list.All(s => (string?)s["id"] != id)) throw new CommandException("The project has no such hall style.");
        var pa = project.CloneAttrs();
        var rest = list.Where(s => (string?)s["id"] != id).Select(s => (JsonNode)s.DeepClone()).ToArray();
        if (rest.Length > 0) pa[Key] = new JsonArray(rest); else pa.Remove(Key);
        ctx.Replace(project.With(attrs: pa));
        foreach (var h in ctx.Model.OfClass(Halls.Class).Where(h => h.Str("style") == id).ToList())
        {
            var attrs = h.CloneAttrs();
            attrs.Remove("style");
            ctx.Replace(h.With(attrs: attrs));
        }
    }
}
