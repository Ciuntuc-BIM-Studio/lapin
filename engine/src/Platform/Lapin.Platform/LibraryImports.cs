using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// A kind of library file the open project takes in (audit D13): registered by the module that owns it (a service), so one
/// preview and one import serve every kind — the interface's one import dialog, the firm folders, the Catalog, scripts.
/// </summary>
public interface ILibraryFormat
{
    /// <summary>The kind of file (<see cref="LibraryFile.Formats"/>).</summary>
    LibraryFile.Spec Spec { get; }

    /// <summary>What an import of the file would do, item by item (<see cref="LibraryMerge.Row"/>); a file that is not of the kind refused.</summary>
    IReadOnlyList<LibraryMerge.Row> Preview(IQueryContext q, string text);

    /// <summary>The file taken in as the choices say (<c>{key: both | update | skip}</c>, else <paramref name="all"/>, else each row's default); one undo step.</summary>
    JsonObject Import(ICommandContext ctx, string text, JsonObject? choices, string? all);
}

/// <summary>A kind of library file made of three functions (the owner's existing preview and import).</summary>
public sealed class LibraryFormat(LibraryFile.Spec spec, Func<IQueryContext, string, IReadOnlyList<LibraryMerge.Row>> preview, Func<ICommandContext, string, JsonObject?, string?, JsonObject> import) : ILibraryFormat
{
    public LibraryFile.Spec Spec => spec;
    public IReadOnlyList<LibraryMerge.Row> Preview(IQueryContext q, string text) => preview(q, text);
    public JsonObject Import(ICommandContext ctx, string text, JsonObject? choices, string? all) => import(ctx, text, choices, all);
}

/// <summary>The commands every kind of library file shares: the inventory, the preview and the import.</summary>
public static class LibraryImports
{
    /// <summary>The kind of a file: what its JSON says it is, else what its extension names (<c>.lin</c>, <c>.pat</c>).</summary>
    public static ILibraryFormat Find(IServices s, string text, string? ext)
    {
        var spec = LibraryFile.Detect(text) ?? LibraryFile.ByExtension(ext)
            ?? throw new CommandException("That is not a Lapin library file (a .lapinsymbols, .lapintypes, .lapinstandards… or AutoCAD's .lin and .pat).");
        return s.All<ILibraryFormat>().FirstOrDefault(f => f.Spec.Format == spec.Format)
            ?? throw new CommandException($"This edition of Lapin does not take {spec.What} (.{spec.Extension}) into a project.");
    }

    public static void Register(IModuleRegistry r)
    {
        // Every kind of library file Lapin reads and writes: {formats: [{format, extension, what, version, owner, envelope, imports}]};
        // `imports`: this edition takes it into a project through library.import.
        r.Query("library.formats", (q, _) =>
        {
            var have = q.Services.All<ILibraryFormat>().Select(f => f.Spec.Format).ToHashSet();
            return new JsonObject
            {
                ["envelope"] = LibraryFile.Envelope,
                ["formats"] = new JsonArray(LibraryFile.Formats.Select(f => (JsonNode)new JsonObject
                {
                    ["format"] = f.Format, ["extension"] = f.Extension, ["what"] = f.What, ["version"] = f.Version, ["owner"] = f.Owner,
                    ["envelope"] = f.Json, ["imports"] = have.Contains(f.Format),
                }).ToArray()),
            };
        });

        // {text, ext?} → {format, extension, what, header: {format, version, envelope, app, created, source}, rows: [{key, kind, name,
        // status: new | same | name | id, differs, choices, choice}], new, same, conflicts}: what an import of the file would do, before it.
        r.Query("library.preview", (q, a) =>
        {
            var text = (string?)a["text"] ?? throw new CommandException("Which file?");
            var kind = Find(q.Services, text, (string?)a["ext"]);
            var o = LibraryMerge.Preview(kind.Preview(q, text));
            o["format"] = kind.Spec.Format;
            o["extension"] = kind.Spec.Extension;
            o["what"] = kind.Spec.What;
            o["header"] = kind.Spec.Json && JsonNode.Parse(text.TrimStart('\uFEFF')) is JsonObject root ? LibraryFile.HeaderOf(root, kind.Spec).ToJson() : null;
            return o;
        });

        // {text, ext?, choices?: {key: both | update | skip}, all?} → {format, added, updated, kept, skipped, renamed, …the kind's own}:
        // the file taken in, one undo step, each conflict as its choice says (else `all`, else the preview's default).
        r.Command("library.import", "Import library", (ctx, a) =>
        {
            var text = (string?)a["text"] ?? throw new CommandException("Which file?");
            var kind = Find(ctx.Services, text, (string?)a["ext"]);
            var all = (string?)a["all"] is { Length: > 0 } x ? x : null;
            if (all != null && !LibraryMerge.Unified.Contains(LibraryMerge.Word(all, LibraryMerge.Unified))) throw new CommandException($"„{all}” is not a choice (both, update or skip).");
            var o = kind.Import(ctx, text, a["choices"] as JsonObject, all);
            o["format"] = kind.Spec.Format;
            return o;
        });
    }
}
