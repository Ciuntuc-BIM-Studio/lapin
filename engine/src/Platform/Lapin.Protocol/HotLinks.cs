using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Package;
using Lapin.Platform;

namespace Lapin.Protocol;

/// <summary>
/// Another Lapin project linked into this one (docs/links.md, Hotlinks), read-only, as a module placed where asked: the
/// source is read into a session of its own, placed, written as IFC with Lapin's intent and brought in as an IFC link is,
/// its walls, doors and floors coming back native. Each link has its own GlobalIds, made from a seed it keeps (so reading it
/// again finds the same ids), so one project can be placed many times, and a source copied from this very project never
/// touches its elements.
/// </summary>
public static class HotLinks
{
    public const string Kind = "lapin";

    public sealed record Placing(double X, double Y, double Degrees)
    {
        public JsonObject ToJson(string seed, DateTime? modified) => new()
        {
            ["x"] = X, ["y"] = Y, ["rotation"] = Degrees, ["seed"] = seed, ["modified"] = modified?.ToString("O"),
        };
        public static Placing Of(JsonObject? o) => new(Json.Number(o?["x"]) ?? 0, Json.Number(o?["y"]) ?? 0, Json.Number(o?["rotation"]) ?? 0);
    }

    /// <summary>A new link of the project in <paramref name="bytes"/>, or (<paramref name="existing"/>) the link read again.</summary>
    public static IfcImportResult Link(Session session, byte[] bytes, string name, string? path, Placing place, out Diff diff, string? existing = null)
    {
        var old = existing == null ? null : Links.Require(session.Model, existing).Attrs["options"] as JsonObject;
        var seed = (string?)old?["seed"] is { Length: > 0 } kept ? kept : Guid.NewGuid().ToString("N");
        session.Progress?.Invoke("Reading the project", 0, 0);
        var ifc = Seeded(Placed(session, bytes, place, out var left), seed);
        session.Progress?.Invoke("Indexing the file", 0, 0);
        var options = place.ToJson(seed, path != null && File.Exists(path) ? File.GetLastWriteTimeUtc(path) : null);
        var r = IfcImport.Link(session, StepFile.Read(ifc), name, path, new IfcImportOptions("auto"), out diff, existing, Kind, options);
        return left > 0 ? r with { Notes = [.. r.Notes, $"{left} reference(s) from files stay where their file put them."] } : r;
    }

    /// <summary>Whether the file a link was read from has changed since (desktop): false when it cannot tell.</summary>
    public static bool Changed(Entity link)
    {
        if (link.Str("kind") != Kind || link.Str("path") is not { } path || !File.Exists(path)) return false;
        return (string?)(link.Attrs["options"] as JsonObject)?["modified"] is { } was && File.GetLastWriteTimeUtc(path).ToString("O") != was;
    }

    /// <summary>The source written as IFC, moved and turned as asked; <paramref name="left"/>: what could not move (references drawn from a file).</summary>
    static byte[] Placed(Session host, byte[] bytes, Placing place, out int left)
    {
        left = 0;
        using var input = new MemoryStream(bytes, writable: false);
        if (LegacyPackage.Is(input)) throw new CommandException("This is an old Lapin project: open it and save it again before linking it.");
        input.Position = 0;
        PackageContent content;
        try { content = LapinPackage.Read(input); }
        catch (Exception e) when (e is InvalidDataException or System.Text.Json.JsonException or KeyNotFoundException) { throw new CommandException($"This is not a Lapin project: {e.Message}"); }
        var source = new Session(host.Catalog, host.Product);
        source.Load(content.Entities, content.Relations, null, content.Classes);
        if (place.X != 0 || place.Y != 0 || place.Degrees != 0)
        {
            var placed = Spatial.Storeys(source.Model).SelectMany(s => source.Model.RelatedOf(s.Id, IfcClass.RelContainedInSpatialStructure)).ToList();
            left = placed.Count(StoredBodies.Has);
            var ids = new JsonArray([.. placed.Where(e => !StoredBodies.Has(e)).Select(e => (JsonNode)e.Id)]);
            if (ids.Count > 0)
            {
                if (place.Degrees != 0) source.Execute("element.transform", new JsonObject { ["ids"] = ids.DeepClone(), ["op"] = "rotate", ["center"] = new JsonArray(0, 0), ["angle"] = place.Degrees }, out _);
                if (place.X != 0 || place.Y != 0) source.Execute("element.transform", new JsonObject { ["ids"] = ids.DeepClone(), ["op"] = "move", ["by"] = new JsonArray(place.X, place.Y) }, out _);
            }
        }
        using var output = new MemoryStream();
        IfcExport.Write(source, output, "link.ifc");
        return output.ToArray();
    }

    static readonly Regex RootId = new(@"=\s*IFC[A-Z0-9]+\('([0-9A-Za-z_$]{22})'", RegexOptions.Compiled);

    /// <summary>The file with every GlobalId made this link's own, wherever it is named (Lapin's intent names hosts by theirs).</summary>
    public static byte[] Seeded(byte[] ifc, string seed)
    {
        var text = Encoding.Latin1.GetString(ifc);
        var ids = RootId.Matches(text).Select(m => m.Groups[1].Value).Distinct().ToDictionary(id => id, id =>
            GlobalId.FromGuid(new Guid(MD5.HashData(Encoding.UTF8.GetBytes(seed + "/" + id)))));
        var seeded = Regex.Replace(text, @"'([0-9A-Za-z_$]{22})'", m => ids.TryGetValue(m.Groups[1].Value, out var to) ? $"'{to}'" : m.Value);
        return Encoding.Latin1.GetBytes(seeded);
    }
}
