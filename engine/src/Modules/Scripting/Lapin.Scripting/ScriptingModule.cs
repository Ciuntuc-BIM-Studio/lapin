using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Scripting;

/// <summary>
/// A project's Python scripts (docs/plan-python-scripting.md, stage 3; decision 489): each a <see cref="Class"/> entity
/// with its name (a file name ending .py), its code, its language and the SHA-256 of its code (what this computer's trust
/// is kept by). <c>scripting.save</c>, <c>scripting.rename</c> and <c>scripting.delete</c> are one undo step each, so a
/// project's scripts go into undo, versions, shared sessions and the rule of an absent module (decision 484) like any
/// content; a guest never runs them (EngineDispatcher.HostOnlyCommands). Each script is also written as
/// <c>scripts/&lt;name&gt;</c> in the .lapin (<see cref="IPackageTexts"/>), so the zip shows it without Lapin. The module never
/// runs one: running is the host's (Lapin.Host/ScriptHost.cs), asked by the Script panel, never as the project opens.
/// </summary>
public sealed class ScriptingModule : ILapinModule, IPackageTexts, IDrivenElements
{
    public const string ModuleId = "scripting";
    public const string Class = "LapinScript";
    public const string Language = "python";

    /// <summary>The longest script a project keeps (its code, UTF-8).</summary>
    public const int MaxBytes = 1 << 20;

    public string Id => ModuleId;
    public string Name => "Scripting";
    string IPackageTexts.Module => ModuleId;

    /// <summary>A script's name: a file name ending .py, no folder, not hidden (as the user's own scripts are named, ScriptHost.Name).</summary>
    static readonly Regex NamePattern = new(@"^[^/\\:*?""<>|\x00-\x1f.][^/\\:*?""<>|\x00-\x1f]{0,118}\.py$", RegexOptions.CultureInvariant);

    /// <summary>The SHA-256 of a script's code (UTF-8, lowercase hex).</summary>
    public static string HashOf(string code) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(code)));

    public void Register(IModuleRegistry r)
    {
        r.Class(new(Class, "Script",
        [
            new("code", "Code", "text", ReadOnly: true),
            new("language", "Language", "text", ReadOnly: true),
            new("description", "Description", "text"),
            new("sha256", "SHA-256", "text", ReadOnly: true),
        ], "#3776ab", Kind: "resource", Template: false));
        r.Service<IPackageTexts>(this);
        r.Service<IDrivenElements>(this);

        // {id?, name, code, description?}: a script kept in the project, a new one or (by id) one of its own changed; its name may change with it.
        r.Command("save", "Save script", (ctx, a) =>
        {
            var name = NameOf(a);
            var code = (string?)a["code"] ?? throw new CommandException("The script's code is missing.");
            if (Encoding.UTF8.GetByteCount(code) > MaxBytes) throw new CommandException($"A project's script is at most {MaxBytes / 1024 / 1024} MB.");
            var existing = (string?)a["id"] is { Length: > 0 } id ? Script(ctx.Model, id) : null;
            RefuseTaken(ctx.Model, name, existing?.Id);
            var attrs = existing?.CloneAttrs() ?? [];
            attrs["code"] = code;
            attrs["language"] = Language;
            attrs["sha256"] = HashOf(code);
            if (a.ContainsKey("description"))
            {
                if (((string?)a["description"])?.Trim() is { Length: > 0 } d) attrs["description"] = d; else attrs.Remove("description");
            }
            if (existing == null)
            {
                var e = new Entity(ctx.NewId(), Class, name, attrs);
                ctx.Add(e);
                return Json(e, created: true);
            }
            var after = existing.With(name: name, attrs: attrs);
            if (after.Name != existing.Name || attrs.ToJsonString() != existing.Attrs.ToJsonString()) ctx.Replace(after);
            return Json(after, created: false);
        });

        // {id, name}: a script renamed (its file in the package with it).
        r.Command("rename", "Rename script", (ctx, a) =>
        {
            var e = Script(ctx.Model, (string?)a["id"]);
            var name = NameOf(a);
            RefuseTaken(ctx.Model, name, e.Id);
            if (name != e.Name) ctx.Replace(e.With(name: name));
            return Json(e.With(name: name), created: false);
        });

        // {id}: a script taken out of the project (Undo brings it back).
        r.Command("delete", "Delete script", (ctx, a) =>
        {
            var e = Script(ctx.Model, (string?)a["id"]);
            ctx.Remove(e.Id);
            return null;
        });

        // The project's scripts, by name: their ids, names, descriptions, hashes and sizes (no code).
        r.Query("list", (q, _) => new JsonArray(All(q.Model).Select(e => (JsonNode)Json(e, created: false)).ToArray()));

        // {id} or {name}: one script with its code.
        r.Query("get", (q, a) =>
        {
            var e = (string?)a["id"] is { Length: > 0 } id ? Script(q.Model, id)
                : All(q.Model).FirstOrDefault(x => string.Equals(x.Name, ((string?)a["name"])?.Trim(), StringComparison.OrdinalIgnoreCase))
                  ?? throw new CommandException($"The project has no script „{(string?)a["name"]}”.");
            var o = Json(e, created: false);
            o["code"] = e.Str("code") ?? "";
            return o;
        });
    }

    static IEnumerable<Entity> All(IModelView m) => m.OfClass(Class).OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase);

    static Entity Script(IModelView m, string? id) =>
        m.Get(id ?? "") is { Class: Class } e ? e : throw new CommandException("The project has no such script.");

    static string NameOf(JsonObject a)
    {
        var name = ((string?)a["name"])?.Trim() ?? throw new CommandException("Give the script a name first.");
        if (!name.EndsWith(".py", StringComparison.OrdinalIgnoreCase)) name += ".py";
        if (!NamePattern.IsMatch(name)) throw new CommandException($"„{name}” is not a script's name: letters, digits and spaces, ending .py.");
        return name;
    }

    /// <summary>Two scripts of a project never share a name, whatever its case: each is one file in the package.</summary>
    static void RefuseTaken(IModelView m, string name, string? self)
    {
        if (m.OfClass(Class).Any(e => e.Id != self && string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase)))
            throw new CommandException($"The project has a script called „{name}” already.");
    }

    static JsonObject Json(Entity e, bool created)
    {
        var code = e.Str("code") ?? "";
        var o = new JsonObject
        {
            ["id"] = e.Id, ["name"] = e.Name, ["sha256"] = e.Str("sha256") ?? HashOf(code), ["bytes"] = Encoding.UTF8.GetByteCount(code),
            ["lines"] = code.Length == 0 ? 0 : code.Split('\n').Length - (code.EndsWith('\n') ? 1 : 0),
        };
        if (e.Str("description") is { } d) o["description"] = d;
        if (created) o["created"] = true;
        return o;
    }

    // ── the package's copies: scripts/<name> ──

    public IEnumerable<(string Path, string Text)> Texts(IModelView model) =>
        model.OfClass(Class).Where(e => NamePattern.IsMatch(e.Name)).Select(e => ($"scripts/{e.Name}", e.Str("code") ?? ""));

    // ── a script changes through the commands above only ──

    public bool Drives(IModelView m, Entity e) => e.Class == Class;

    /// <summary>What a guest is told when a generic command reaches a project's script (the host's, as the scripting.* commands are).</summary>
    public const string HostOnly = "Only the host can do that.";

    /// <summary>
    /// The core's edits of an element (rename, attributes, parameters, moves) refused on a script: its code and its hash go
    /// together, through scripting.save. A guest of a shared session is refused every generic command on one, delete,
    /// group, copy and move too, alone or in a batch (stage 4, the user's answer of 12.11.2026): a project's scripts are the
    /// host's, as <c>EngineDispatcher.HostOnlyCommands</c> keeps <c>scripting.*</c>.
    /// </summary>
    public void Before(ICommandContext ctx, DrivenEdit edit, DrivenOutcome outcome)
    {
        if (HostFiles.GuestCall) throw new CommandException(HostOnly);
        if (edit.Command is "element.delete") return;
        throw new CommandException("A project's script is changed in the Script panel (View → Script), not here.");
    }
}
