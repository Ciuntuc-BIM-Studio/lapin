using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// The one envelope of the library files a user keeps and shares (audit D13, docs/audit-2026-11-15.md): symbols, type packs, the
/// wall and floor types' JSON, drafting standards, rule packs, shared parameters, field aliases, title blocks, bar shapes, parts,
/// families, schedule templates, my furniture, profile libraries, price lists and mappings. Each keeps its own payload as it was;
/// what is common is the header, read and written here once:
/// <code>{"format": "lapin-symbols", "version": 1, "lapin": {"envelope": 1, "app": "0.1.1234", "created": "2026-11-16T10:00:00Z", "source": "Bloc P+4"}, …payload}</code>
/// <c>format</c> and <c>version</c> stay where every build has read them (the payload's version, upgraded step by step when a format
/// changes); the <c>lapin</c> object is the envelope proper: its own version, the app that wrote the file, when, and from what (the
/// project's name, or Lapin for its own). A file written before the envelope (no <c>lapin</c> object) is envelope version 0 and reads
/// as it always did: nothing about the header is required. A file from a newer Lapin (a payload or an envelope version above this
/// build's) is refused with a sentence, as each format did on its own.
/// <para>AutoCAD's <c>.lin</c> and <c>.pat</c> are written after their own specification and carry no envelope (listed in
/// <see cref="Formats"/> for the inventory and the import preview).</para>
/// </summary>
public static class LibraryFile
{
    /// <summary>The envelope's own version: 0, none (a file of the builds before it); 1, the header above.</summary>
    public const int Envelope = 1;

    /// <summary>The key of the envelope's header.</summary>
    public const string HeaderKey = "lapin";

    /// <summary>
    /// A kind of library file: its format id (the file's <c>format</c>), its extension, what it is called in a sentence („a symbol
    /// library”), the payload's version this build writes and the module that owns it (core: the platform's own). <paramref name="Json"/>
    /// false: a text format of another program's (no envelope).
    /// </summary>
    public sealed record Spec(string Format, string Extension, string What, int Version, string Owner, bool Json = true);

    /// <summary>The inventory (audit D13): every library file Lapin reads and writes for the user's own content.</summary>
    public static readonly IReadOnlyList<Spec> Formats =
    [
        new("lapin-symbols", "lapinsymbols", "a symbol library", 1, "core"),
        new("lapin-type-pack", "lapintypes", "a Lapin type pack", 1, "core"),
        new("lapin-types", "json", "a wall and floor types file", 2, "architecture"),
        new("lapin-standards", "lapinstandards", "a drafting standards file", 1, "core"),
        new("lapin-rules", "lapinrules", "a rule pack", 1, "core"),
        new("lapin-parameters", "lapinparams", "a shared parameters file", 1, "core"),
        new("lapin-field-aliases", "lapinaliases", "a field aliases file", 1, "core"),
        new("lapin.titleBlock", "lapintitleblock", "a title block", 1, "core"),
        new("lapin-bar-shapes", "lapinbarshapes", "a bar shapes file", 1, "rebar"),
        new("lapinpart", "lapinpart", "a part", 1, "studio"),
        new("lapin-family", "lapinfamily", "a family", 1, "studio"),
        new("lapin-schedule", "lapinschedule", "a schedule template", 1, "architecture"),
        new("lapin-furniture-library", "lapinlib", "a furniture library", 1, "furniture"),
        new("lapin-profiles", "lapinprofiles", "a profile library", 1, "core"),
        new("lapin-price-list", "lapinprices", "a price list", 1, "cost"),
        new("lapin-mapping", "lapinmapping", "a mapping file", 1, "core"),
        new("autocad-lin", "lin", "a line type file", 0, "core", Json: false),
        new("autocad-pat", "pat", "a hatch pattern file", 0, "core", Json: false),
    ];

    public static Spec Of(string format) => Formats.FirstOrDefault(f => f.Format == format) ?? throw new ArgumentException($"No library format „{format}”.");

    /// <summary>The kind a file's extension names (with or without its dot), none when it is not a library file's.</summary>
    public static Spec? ByExtension(string? ext) => ext?.TrimStart('.').ToLowerInvariant() is { Length: > 0 } e ? Formats.FirstOrDefault(f => f.Extension == e && e != "json") : null;

    /// <summary>The header of a file: its format, the payload's version, the envelope's (0: none), and who wrote it, when and from what.</summary>
    public sealed record Header(string Format, int Version, int Envelope, string? App, string? Created, string? Source)
    {
        public JsonObject ToJson() => new()
        {
            ["format"] = Format, ["version"] = Version, ["envelope"] = Envelope, ["app"] = App, ["created"] = Created, ["source"] = Source,
        };
    }

    /// <summary>A file read: its header, and its object without the envelope (its <c>format</c> and <c>version</c> kept), upgraded to this build's version.</summary>
    public sealed record Doc(Header Header, JsonObject Root, bool Upgraded);

    // ------------------------------------------------------------ upgrades

    static readonly Dictionary<(string Format, int From), Func<JsonObject, JsonObject>> Steps = [];

    /// <summary>
    /// A step that brings a file of <paramref name="format"/> from version <paramref name="from"/> to the next, registered by the
    /// code that owns the format (once per process; the same step again replaces it). The reader applies the steps in order.
    /// </summary>
    public static void Upgrade(string format, int from, Func<JsonObject, JsonObject> step)
    {
        lock (Steps) Steps[(format, from)] = step;
    }

    // ------------------------------------------------------------ reading

    /// <summary>
    /// The file's text read as <paramref name="spec"/>: JSON, an object, of that format (or one of <paramref name="alsoAccepted"/>),
    /// not from a newer Lapin; refused whole otherwise, with a sentence naming the kind of file and its extension.
    /// </summary>
    public static Doc Read(string? text, Spec spec, params string[] alsoAccepted)
    {
        JsonNode? n;
        try { n = JsonNode.Parse((text ?? "").TrimStart('\uFEFF')); }
        catch (JsonException) { throw new CommandException($"That is not {spec.What} (.{spec.Extension}): it is not JSON."); }
        return Read(n, spec, alsoAccepted);
    }

    /// <summary>A file already parsed (a library's object given by the interface) read as <see cref="Read(string?, Spec, string[])"/>.</summary>
    public static Doc Read(JsonNode? node, Spec spec, params string[] alsoAccepted)
    {
        if (node is not JsonObject given) throw new CommandException($"That is not {spec.What} (.{spec.Extension}).");
        var format = (string?)(given["format"] as JsonValue);
        if (format != spec.Format && (format is null || !alsoAccepted.Contains(format))) throw new CommandException($"That is not {spec.What} (.{spec.Extension}).");
        var own = format == spec.Format ? spec : Formats.FirstOrDefault(f => f.Format == format) ?? spec;
        var header = HeaderOf(given, own);
        if (header.Version > own.Version || header.Envelope > Envelope)
            throw new CommandException($"That {spec.What[(spec.What.IndexOf(' ') + 1)..]} was written by a newer Lapin{(header.App is { } app ? $" ({app})" : "")}: update Lapin to read it.");
        var root = new JsonObject();
        foreach (var (k, v) in given) if (k != HeaderKey) root[k] = v?.DeepClone();
        var upgraded = false;
        for (var v = header.Version; v < own.Version; v++)
        {
            Func<JsonObject, JsonObject>? step;
            lock (Steps) Steps.TryGetValue((own.Format, v), out step);
            if (step is null) continue;
            root = step(root);
            upgraded = true;
        }
        if (upgraded) root["version"] = own.Version;
        return new Doc(header, root, upgraded);
    }

    /// <summary>The header of an object, whatever its format: envelope 0 when it has none; a version not given is 1 (the first every format wrote).</summary>
    public static Header HeaderOf(JsonObject o, Spec? spec = null)
    {
        var format = (string?)(o["format"] as JsonValue) ?? spec?.Format ?? "";
        var version = Json.Number(o["version"]) is { } v && double.IsFinite(v) ? (int)Math.Floor(v) : 1;
        if (o[HeaderKey] is not JsonObject h) return new Header(format, version, 0, null, null, null);
        static string? S(JsonNode? n) => n is JsonValue jv && jv.TryGetValue<string>(out var s) && s.Trim().Length > 0 ? s.Trim() : null;
        var envelope = Json.Number(h["envelope"]) is { } e && double.IsFinite(e) ? (int)Math.Floor(e) : 1;
        return new Header(format, version, envelope, S(h["app"]), S(h["created"]), S(h["source"]));
    }

    /// <summary>The kind a JSON file says it is (its <c>format</c>; a mapping by its <c>app</c> and <c>kind</c>), none when it says none Lapin knows.</summary>
    public static Spec? Detect(string? text)
    {
        try { return JsonNode.Parse((text ?? "").TrimStart('\uFEFF')) is JsonObject o ? Detect(o) : null; }
        catch (JsonException) { return null; }
    }

    public static Spec? Detect(JsonObject o) =>
        (string?)(o["format"] as JsonValue) is { } f ? Formats.FirstOrDefault(x => x.Json && x.Format == f)
        : (string?)(o["app"] as JsonValue) == "lapin" && o["kind"] is not null ? Of("lapin-mapping") : null;

    // ------------------------------------------------------------ writing

    /// <summary>The source a project's files name: its project's name.</summary>
    public static string? SourceOf(IModelView m) => Spatial.Project(m)?.Name is { Length: > 0 } n ? n : null;

    /// <summary>When a file is written (tests set it to a fixed moment).</summary>
    public static Func<DateTimeOffset> Clock { get; set; } = () => DateTimeOffset.UtcNow;

    /// <summary>
    /// <paramref name="payload"/> as a file of <paramref name="spec"/>: its <c>format</c> and this build's <c>version</c> first, then the
    /// envelope's header, then the payload's own keys in their order (a <c>format</c>, <c>version</c> or header it had replaced).
    /// </summary>
    public static JsonObject Write(Spec spec, JsonObject payload, string? source)
    {
        var o = new JsonObject
        {
            ["format"] = spec.Format,
            ["version"] = spec.Version,
            [HeaderKey] = new JsonObject
            {
                ["envelope"] = Envelope,
                ["app"] = Session.AppVersion,
                ["created"] = Clock().UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
                ["source"] = source is { Length: > 0 } s ? s : "Lapin",
            },
        };
        foreach (var (k, v) in payload)
            if (k is not ("format" or "version" or HeaderKey)) o[k] = v?.DeepClone();
        return o;
    }

    /// <summary>A file's text as every library file is written: indented JSON, its letters kept as they are.</summary>
    public static string Text(JsonObject file) => file.ToJsonString(Indented);

    static readonly JsonSerializerOptions Indented = new() { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
}
