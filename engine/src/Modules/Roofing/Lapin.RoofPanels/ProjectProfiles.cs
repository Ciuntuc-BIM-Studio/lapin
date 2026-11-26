using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.RoofPanels;

/// <summary>
/// The project's own roof panel profiles (docs/plan-roof-panels.md §3, stage S7): a <c>LapinPanelProfile</c> an entity, its
/// <c>def</c> a profile in profiles.json's own format (the sheet's widths, laps and module, its lengths or order range, colours,
/// least pitches, the accessories, support, fixings, ridge caps, flat sheet and article code), kept beside the built-in ones,
/// which stay read-only: a built-in profile is changed by duplicating it into the project and editing the copy, so other
/// projects, and the roofs that keep the built-in one, are unchanged. A copy keeps the built-in profile's article codes (its
/// <c>{id}</c> written out), so it prices as the original does. A roof names a project profile by its id, as it names a built-in
/// one; a firm keeps its profiles in a <c>.lapinpanels</c> file (the one library envelope, <see cref="LibraryFile"/>).
/// </summary>
public static class ProjectProfiles
{
    public const string Class = "LapinPanelProfile";
    public const string FileFormat = "lapin-panel-profiles";

    public static LibraryFile.Spec Spec => LibraryFile.Of(FileFormat);

    static readonly Regex IdPattern = new("^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$", RegexOptions.CultureInvariant);

    public static JsonObject DefOf(Entity e) => e.Attrs["def"] as JsonObject ?? [];

    static string IdOf(Entity e) => (string?)DefOf(e)["id"] ?? "";

    /// <summary>The project's profile of id <paramref name="id"/>, or null.</summary>
    public static Entity? Find(IModelView m, string? id) =>
        id is { Length: > 0 } ? m.OfClass(Class).FirstOrDefault(e => string.Equals(IdOf(e), id, StringComparison.OrdinalIgnoreCase)) : null;

    /// <summary>A project's profile by its entity's id or its profile id.</summary>
    static Entity Require(IModelView m, string? id) =>
        (m.Get(id ?? "") is { Class: Class } e ? e : Find(m, id)) ?? throw new CommandException("There is no such roof panel profile in the project.");

    /// <summary>The roofs that name the profile <paramref name="id"/>.</summary>
    public static List<Entity> Uses(IModelView m, string id) =>
        [.. m.OfClass(IfcClass.Roof).Where(r => RoofPanels.ChoiceOf(r) is { } c && string.Equals(c.Profile, id, StringComparison.OrdinalIgnoreCase))];

    /// <summary>
    /// A profile as a command or a file gives it, checked: an id of letters, digits, dots, dashes and underscores that is no
    /// built-in profile's (nor another of the project's but <paramref name="self"/>), a name, and a profile that lays
    /// (<see cref="PanelProfile.Problem"/>, an order range that reads, colours told apart by their codes); refused with why.
    /// </summary>
    public static JsonObject Check(JsonNode? n, IModelView? m = null, Entity? self = null)
    {
        if (n is not JsonObject given) throw new CommandException("A roof panel profile is an object, in the format of the built-in ones.");
        var o = (JsonObject)given.DeepClone();
        var id = ((string?)o["id"])?.Trim() ?? "";
        if (!IdPattern.IsMatch(id)) throw new CommandException("A profile's id is up to 64 letters, digits, dots, dashes or underscores (my-tile-35).");
        if (PanelProfiles.Find(id) != null) throw new CommandException($"„{id}” is one of Lapin's own profiles: a profile of the project takes an id of its own.");
        if (m != null && Find(m, id) is { } other && other.Id != self?.Id) throw new CommandException($"The project has a profile „{id}” already.");
        o["id"] = id;
        o["name"] = ((string?)o["name"])?.Trim() is { Length: > 0 } name ? name : id;
        if (o["order"] is JsonObject r)
        {
            var (min, max) = (Json.Number(r["min"]) ?? 0, Json.Number(r["max"]) ?? 0);
            if (!(min > 0) || max < min || (Json.Number(r["step"]) ?? 0) < 0) throw new CommandException("An order range is its least length, a step and its most, the most no shorter than the least.");
        }
        else if (o["lengths"] is not JsonArray) throw new CommandException("A profile is bought in stock lengths (lengths) or made to order (order).");
        var colours = (o["colors"] as JsonArray ?? []).OfType<JsonObject>().Select(c => ((string?)c["code"])?.Trim() ?? "").ToList();
        if (colours.Any(c => c.Length == 0) || colours.Distinct(StringComparer.OrdinalIgnoreCase).Count() != colours.Count) throw new CommandException("Each colour has a code of its own (RAL 7016: 7016).");
        PanelProfile p;
        try { p = PanelProfile.Read(o); }
        catch (Exception e) when (e is InvalidOperationException or FormatException or ArgumentException) { throw new CommandException($"The profile does not read: {e.Message}"); }
        if (p.Problem() is { } problem) throw new CommandException(problem);
        if (p.ValleyGap < 0 || p.MinPitch < 0 || p.AccessoryLength <= p.AccessoryLaps.Values.DefaultIfEmpty(0).Max()) throw new CommandException("A profile's valley gap and least pitch are not negative, its accessories longer than their laps.");
        return o;
    }

    /// <summary>An id of <paramref name="stem"/> no profile has (<c>stem</c>, <c>stem-2</c>…).</summary>
    static string FreeId(IModelView m, string stem)
    {
        bool Taken(string id) => PanelProfiles.Find(id) != null || Find(m, id) != null;
        if (!Taken(stem)) return stem;
        var n = 2;
        while (Taken($"{stem}-{n}")) n++;
        return $"{stem}-{n}";
    }

    static string FreeName(IModelView m, string name) =>
        m.OfClass(Class).Any(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase)) ? LibraryMerge.FreeName(m, Class, name) : name;

    /// <summary>
    /// A built-in profile, or one of the project's, as a new profile of the project: every field as it is, a new id and name, its article
    /// code with the original's id written out (<c>bilka-clasic-35/{color}</c>), so the copy lays, counts and prices as the original.
    /// </summary>
    public static JsonObject Copy(IModelView m, string from, string? name = null)
    {
        var shipped = PanelProfiles.Raw(from);
        var def = shipped ?? (Find(m, from) is { } own ? (JsonObject)DefOf(own).DeepClone() : throw new CommandException($"There is no roof panel profile „{from}”."));
        var fromId = (string)def["id"]!;
        var article = (string?)def["article"] is { Length: > 0 } a ? a : "{id}/{color}";
        def["article"] = article.Replace("{id}", fromId, StringComparison.Ordinal);
        def["id"] = FreeId(m, $"{fromId}-copy");
        def["name"] = FreeName(m, name?.Trim() is { Length: > 0 } nm ? nm : $"{(string?)def["name"] ?? fromId} (copy)");
        def["basedOn"] = shipped != null ? fromId : (string?)def["basedOn"] ?? fromId;
        return def;
    }

    static Entity Make(ICommandContext ctx, JsonObject def) => new(ctx.NewId(), Class, (string)def["name"]!, new JsonObject { ["def"] = def });

    /// <summary>A profile's row for the interface: its entity, its profile's id and name, what it is based on, its roofs, its summary and every field.</summary>
    static JsonObject Row(IModelView m, Entity e)
    {
        var def = DefOf(e);
        var uses = Uses(m, IdOf(e));
        JsonObject? summary = null;
        string? problem = null;
        try { summary = PanelProfiles.Resolve(def).ToJson(); }
        catch (ArgumentException x) { problem = x.Message; }
        return new JsonObject
        {
            ["id"] = e.Id, ["profile"] = IdOf(e), ["name"] = e.Name, ["basedOn"] = (string?)def["basedOn"],
            ["uses"] = uses.Count, ["roofs"] = new JsonArray([.. uses.Select(r => (JsonNode)r.Name)]),
            ["summary"] = summary, ["problem"] = problem, ["def"] = def.DeepClone(),
        };
    }

    public static void Register(IModuleRegistry r)
    {
        r.Class(new(Class, "Roof panel profile", [], "#6b6f73", Kind: "resource"));

        // {builtin: [{profile, name, maker, summary, def}], project: [{id, profile, name, basedOn, uses, roofs, summary, problem, def}]}:
        // the profiles a roof can name, Lapin's (read-only) and the project's, with every field for the editor.
        r.Query("projectProfiles", (q, _) => new JsonObject
        {
            ["builtin"] = new JsonArray([.. PanelProfiles.BuiltIn.Select(p => (JsonNode)new JsonObject
            {
                ["profile"] = p.Id, ["name"] = p.Name, ["maker"] = p.Maker, ["summary"] = p.ToJson(), ["def"] = PanelProfiles.Raw(p.Id),
            })]),
            ["project"] = new JsonArray([.. q.Model.OfClass(Class).OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase).Select(e => (JsonNode)Row(q.Model, e))]),
        });

        // {from (a built-in profile's id, or one of the project's), name?} → {id, profile}: a copy in the project to edit; one undo step.
        r.Command("duplicateProfile", "Duplicate roof panel profile", (ctx, a) =>
        {
            var def = Check(Copy(ctx.Model, (string?)a["from"] ?? throw new CommandException("Which profile?"), (string?)a["name"]), ctx.Model);
            var e = Make(ctx, def);
            ctx.Add(e);
            return new JsonObject { ["id"] = e.Id, ["profile"] = (string)def["id"]! };
        });

        // {def} → {id, profile}: a profile of the project made from its fields (a script's, a file's); one undo step.
        r.Command("createProfile", "New roof panel profile", (ctx, a) =>
        {
            var def = Check(a["def"], ctx.Model);
            def["name"] = FreeName(ctx.Model, (string)def["name"]!);
            var e = Make(ctx, def);
            ctx.Add(e);
            return new JsonObject { ["id"] = e.Id, ["profile"] = (string)def["id"]! };
        });

        // {id (the entity's or the profile's), def}: the profile changed; the roofs that name it follow (its new id, when it has one, and its
        // panels' material in its colour); one undo step.
        r.Command("updateProfile", "Change roof panel profile", (ctx, a) =>
        {
            var e = Require(ctx.Model, (string?)a["id"]);
            var def = Check(a["def"], ctx.Model, e);
            var was = IdOf(e);
            var roofs = Uses(ctx.Model, was);
            ctx.Replace(e.With(name: (string)def["name"]!, attrs: new JsonObject { ["def"] = def }));
            var profile = PanelProfile.Read(def);
            foreach (var roof in roofs)
            {
                var attrs = roof.CloneAttrs();
                var panels = (JsonObject)attrs[RoofPanels.Key]!;
                panels["profile"] = profile.Id;
                var color = (string?)panels["color"];
                // A colour, a longest panel or an option the profile no longer has: the profile's default.
                if (color != null && !profile.Colors.Any(c => c.Code == color)) panels.Remove("color");
                if (Json.Number(panels["maxLength"]) is { } ml && (!profile.OnOrder || !profile.MaxLengths.Contains((int)ml))) panels.Remove("maxLength");
                if ((string?)panels["support"] is { } s && !profile.Support.Any(x => x.Id == s)) panels.Remove("support");
                if ((string?)panels["ridge"] is { } rg && !profile.Ridges.Contains(rg)) panels.Remove("ridge");
                ctx.Replace(roof.With(attrs: attrs));
                RoofPanels.EnsureMaterial(ctx, profile, (string?)panels["color"] ?? profile.Colors.FirstOrDefault()?.Code);
            }
            return null;
        });

        // {id}: the profile taken out of the project; refused while a roof names it (the roof would lose its panels).
        r.Command("deleteProfile", "Delete roof panel profile", (ctx, a) =>
        {
            var e = Require(ctx.Model, (string?)a["id"]);
            var roofs = Uses(ctx.Model, IdOf(e));
            if (roofs.Count > 0)
                throw new CommandException($"{e.Name} covers {string.Join(", ", roofs.Select(x => x.Name))}: choose another profile for {(roofs.Count == 1 ? "it" : "them")} first.");
            ctx.Remove(e.Id);
            return null;
        });

        // {ids?} → {text, file}: the project's profiles (or those asked) as a .lapinpanels file, a firm's library of profiles.
        r.Query("exportProfiles", (q, a) =>
        {
            var ids = a["ids"] is JsonArray arr ? arr.Select(x => (string?)x).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase) : null;
            var list = q.Model.OfClass(Class).Where(e => ids == null || ids.Contains(e.Id) || ids.Contains(IdOf(e)))
                .OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase).Select(e => (JsonNode?)DefOf(e).DeepClone()).ToArray();
            if (list.Length == 0) throw new CommandException("The project has no roof panel profiles of its own to write.");
            var o = LibraryFile.Write(Spec, new JsonObject { ["profiles"] = new JsonArray(list) }, LibraryFile.SourceOf(q.Model));
            return new JsonObject { ["text"] = LibraryFile.Text(o), ["file"] = $"Roof panel profiles.{Spec.Extension}" };
        });

        // {text, choices?: {profile id: update | both | skip}, all?}: a .lapinpanels file brought in, one undo step. A profile the project lacks
        // is added; one of the same id is updated by default (its roofs follow), kept beside as „id-2” (both) or left (skip).
        r.Command("importProfiles", "Import roof panel profiles", (ctx, a) => Import(ctx, (string?)a["text"] ?? "", a["choices"] as JsonObject, (string?)a["all"]));

        r.Service<ILibraryFormat>(new LibraryFormat(Spec, (q, text) => Rows(q.Model, ReadFile(text)), (ctx, text, choices, all) => Import(ctx, text, choices, all)));
    }

    static List<JsonObject> ReadFile(string text) => [.. (LibraryFile.Read(text, Spec).Root["profiles"] as JsonArray ?? []).Select(n => Check(n))];

    /// <summary>The file's profiles as the import preview shows them: the same id as one of the project's is that one, updated by default.</summary>
    static List<LibraryMerge.Row> Rows(IModelView m, List<JsonObject> profiles) =>
    [
        .. profiles.Select(def => Find(m, (string)def["id"]!) is { } mine
            ? new LibraryMerge.Row((string)def["id"]!, "panelProfile", (string)def["name"]!, JsonNode.DeepEquals(DefOf(mine), def) ? LibraryMerge.Match.Same : LibraryMerge.Match.Id,
                LibraryMerge.Differs(DefOf(mine), def))
            : new LibraryMerge.Row((string)def["id"]!, "panelProfile", (string)def["name"]!, LibraryMerge.Match.New)),
    ];

    static JsonObject Import(ICommandContext ctx, string text, JsonObject? choices, string? all)
    {
        var profiles = ReadFile(text);
        var tally = new LibraryMerge.Tally();
        foreach (var (def, row) in profiles.Zip(Rows(ctx.Model, profiles)))
            switch (LibraryMerge.Decide(row, choices, all))
            {
                case LibraryMerge.Add:
                    def["name"] = FreeName(ctx.Model, (string)def["name"]!);
                    ctx.Add(Make(ctx, def));
                    tally.Added++;
                    break;
                case LibraryMerge.Update:
                {
                    var mine = Find(ctx.Model, (string)def["id"]!)!;
                    ctx.Replace(mine.With(name: (string)def["name"]!, attrs: new JsonObject { ["def"] = def }));
                    var profile = PanelProfile.Read(def);
                    foreach (var roof in Uses(ctx.Model, profile.Id)) RoofPanels.EnsureMaterial(ctx, profile, RoofPanels.ChoiceOf(roof)?.Color ?? profile.Colors.FirstOrDefault()?.Code);
                    tally.Updated++;
                    break;
                }
                case LibraryMerge.Both:
                {
                    def["id"] = FreeId(ctx.Model, (string)def["id"]!);
                    var name = LibraryMerge.FreeName(ctx.Model, Class, (string)def["name"]!);
                    def["name"] = name;
                    ctx.Add(Make(ctx, def));
                    tally.Added++;
                    tally.Renamed.Add(name);
                    break;
                }
                case LibraryMerge.Skip: tally.Skipped++; break;
                default: tally.Kept++; break;
            }
        return tally.ToJson();
    }
}
