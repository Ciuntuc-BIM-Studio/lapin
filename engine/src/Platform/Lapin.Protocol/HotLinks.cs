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
/// touches its elements. What the source links itself comes along as its own, unless hidden there; references drawn from
/// files move with the rest.
/// </summary>
public static class HotLinks
{
    public const string Kind = "lapin";

    /// <summary>Where a link stands: mirrored first, left and right swapped about its own origin (x to −x), then turned, then moved.</summary>
    public sealed record Placing(double X, double Y, double Degrees, bool Mirrored = false)
    {
        public JsonObject ToJson(string seed, DateTime? modified) => new()
        {
            ["x"] = X, ["y"] = Y, ["rotation"] = Degrees, ["mirror"] = Mirrored, ["seed"] = seed, ["modified"] = modified?.ToString("O"),
        };
        public static Placing Of(JsonObject? o) => new(Json.Number(o?["x"]) ?? 0, Json.Number(o?["y"]) ?? 0, Json.Number(o?["rotation"]) ?? 0, o?["mirror"] is JsonValue m && m.TryGetValue<bool>(out var b) && b);
    }

    /// <summary>A new link of the project in <paramref name="bytes"/>, or (<paramref name="existing"/>) the link read again.</summary>
    public static IfcImportResult Link(Session session, byte[] bytes, string name, string? path, Placing place, out Diff diff, string? existing = null)
    {
        var old = existing == null ? null : Links.Require(session.Model, existing).Attrs["options"] as JsonObject;
        var seed = (string?)old?["seed"] is { Length: > 0 } kept ? kept : Guid.NewGuid().ToString("N");
        session.Progress?.Invoke("Reading the project", 0, 0);
        var ifc = Seeded(Placed(session, bytes, place), seed);
        session.Progress?.Invoke("Indexing the file", 0, 0);
        var options = place.ToJson(seed, path != null && File.Exists(path) ? File.GetLastWriteTimeUtc(path) : null);
        return IfcImport.Link(session, StepFile.Read(ifc), name, path, new IfcImportOptions("auto"), out diff, existing, Kind, options);
    }

    /// <summary>Whether the file a link was read from has changed since (desktop): false when it cannot tell.</summary>
    public static bool Changed(Entity link)
    {
        if (link.Str("kind") != Kind || link.Str("path") is not { } path || !File.Exists(path)) return false;
        return (string?)(link.Attrs["options"] as JsonObject)?["modified"] is { } was && File.GetLastWriteTimeUtc(path).ToString("O") != was;
    }

    /// <summary>The source written as IFC, mirrored, turned and moved as asked.</summary>
    static byte[] Placed(Session host, byte[] bytes, Placing place)
    {
        using var input = new MemoryStream(bytes, writable: false);
        if (LegacyPackage.Is(input)) throw new CommandException("This is an old Lapin project: open it and save it again before linking it.");
        input.Position = 0;
        PackageContent content;
        try { content = LapinPackage.Read(input); }
        catch (Exception e) when (e is InvalidDataException or System.Text.Json.JsonException or KeyNotFoundException) { throw new CommandException($"This is not a Lapin project: {e.Message}"); }
        var source = new Session(host.Catalog, host.Product);
        var (entities, relations) = OwnAll(content);
        source.Load(entities, relations, null, content.Classes);
        if (place.X != 0 || place.Y != 0 || place.Degrees != 0 || place.Mirrored)
        {
            var placed = Spatial.Storeys(source.Model).SelectMany(s => source.Model.RelatedOf(s.Id, IfcClass.RelContainedInSpatialStructure)).ToList();
            Carry(source, placed.Where(StoredBodies.Has).Select(e => e.Id).ToHashSet(), place);
            var ids = new JsonArray([.. placed.Where(e => !StoredBodies.Has(e)).Select(e => (JsonNode)e.Id)]);
            if (ids.Count > 0)
            {
                if (place.Mirrored) source.Execute("element.transform", new JsonObject { ["ids"] = ids.DeepClone(), ["op"] = "mirror", ["a"] = new JsonArray(0, 0), ["b"] = new JsonArray(0, 1) }, out _);
                if (place.Degrees != 0) source.Execute("element.transform", new JsonObject { ["ids"] = ids.DeepClone(), ["op"] = "rotate", ["center"] = new JsonArray(0, 0), ["angle"] = place.Degrees }, out _);
                if (place.X != 0 || place.Y != 0) source.Execute("element.transform", new JsonObject { ["ids"] = ids.DeepClone(), ["op"] = "move", ["by"] = new JsonArray(place.X, place.Y) }, out _);
            }
        }
        using var output = new MemoryStream();
        IfcExport.Write(source, output, "link.ifc");
        return output.ToArray();
    }

    /// <summary>
    /// The source's own links made part of it, read here as its own: their tags gone, what a hidden link holds left out
    /// with its relationships, so all of it moves and goes on to the file.
    /// </summary>
    static (List<Entity> Entities, List<Relation> Relations) OwnAll(PackageContent content)
    {
        var hidden = content.Entities.Where(e => e.Class == Links.Class && e.Attrs["visible"] is JsonValue v && v.TryGetValue<bool>(out var shown) && !shown).Select(e => e.Id).ToHashSet();
        var gone = content.Entities.Where(e => e.Class == Links.Class || (e.Str(Links.Key) is { } l && hidden.Contains(l))).Select(e => e.Id).ToHashSet();
        var entities = new List<Entity>();
        foreach (var e in content.Entities)
        {
            if (gone.Contains(e.Id)) continue;
            if (e.Str(Links.Key) == null) { entities.Add(e); continue; }
            var attrs = e.CloneAttrs();
            attrs.Remove(Links.Key);
            entities.Add(e.With(attrs: attrs));
        }
        var relations = new List<Relation>();
        foreach (var r in content.Relations)
        {
            if (gone.Contains(r.Relating)) continue;
            var related = r.Related.Where(x => !gone.Contains(x)).ToList();
            if (related.Count > 0) relations.Add(r with { Related = related });
        }
        return (entities, relations);
    }

    /// <summary>References drawn from files carried as they are: their bodies (uncut, the openings' too), their placements as instances, their own 2D.</summary>
    static void Carry(Session source, IReadOnlySet<string> ids, Placing place)
    {
        if (ids.Count == 0) return;
        var a = place.Degrees * Math.PI / 180;
        var (c, s) = (Math.Cos(a), Math.Sin(a));
        (double X, double Y) At(double x0, double y)
        {
            var x = place.Mirrored ? -x0 : x0;
            return (x * c - y * s + place.X, x * s + y * c + place.Y);
        }
        void Body(JsonNode? n)
        {
            switch (n)
            {
                case JsonArray list:
                    foreach (var x in list) Body(x);
                    break;
                case JsonObject o when o["parts"] is JsonArray parts:
                    foreach (var part in parts.OfType<JsonObject>())
                    {
                        var (v, t) = StoredBodies.ReadPart(part);
                        for (var i = 0; i + 2 < v.Length; i += 3) (v[i], v[i + 1]) = At(v[i], v[i + 1]);
                        // A reflection turns the faces inside out: each triangle's order back.
                        if (place.Mirrored) for (var i = 0; i + 2 < t.Length; i += 3) (t[i + 1], t[i + 2]) = (t[i + 2], t[i + 1]);
                        var written = StoredBodies.WritePart((string?)part["name"] ?? "Body", (string?)part["color"] ?? "#cccccc", v, t);
                        part.Remove("v"); part.Remove("t");
                        part["v64"] = written["v64"]!.DeepClone();
                        part["t64"] = written["t64"]!.DeepClone();
                    }
                    break;
                case JsonObject o:
                    foreach (var (_, x) in o) Body(x);
                    break;
            }
        }
        void Flat(JsonArray? pts)
        {
            if (pts == null) return;
            for (var i = 0; i + 1 < pts.Count; i += 2)
            {
                var (x, y) = At(Json.Number(pts[i]) ?? 0, Json.Number(pts[i + 1]) ?? 0);
                pts[i] = Math.Round(x, 4);
                pts[i + 1] = Math.Round(y, 4);
            }
        }
        source.Edit("Place", edit =>
        {
            foreach (var id in ids)
            {
                var e = edit.Model.Get(id)!;
                var attrs = e.CloneAttrs();
                Body(attrs[StoredBodies.Key]);
                Body(attrs[StoredBodies.UncutKey]);
                Body(attrs[StoredBodies.OpeningsKey]);
                if (place.Mirrored && StoredBodies.InstanceOf(e) is var (shared, placedBy))
                {
                    // A mirrored instance is no longer the same body turned: it takes a body of its own.
                    var own = edit.Model.Get(shared)!.Attrs[StoredBodies.Key]!.DeepClone();
                    foreach (var part in own["parts"]!.AsArray().OfType<JsonObject>())
                    {
                        var (v, t) = StoredBodies.ReadPart(part);
                        for (var i = 0; i + 2 < v.Length; i += 3)
                        {
                            var w = StoredBodies.Apply(placedBy, new Lapin.Geometry.Vec3(v[i], v[i + 1], v[i + 2]));
                            (v[i], v[i + 1], v[i + 2]) = (w.X, w.Y, w.Z);
                        }
                        var written = StoredBodies.WritePart((string?)part["name"] ?? "Body", (string?)part["color"] ?? "#cccccc", v, t);
                        part.Remove("v"); part.Remove("t");
                        part["v64"] = written["v64"]!.DeepClone();
                        part["t64"] = written["t64"]!.DeepClone();
                    }
                    Body(own);
                    attrs[StoredBodies.Key] = own;
                    attrs.Remove(StoredBodies.BodyOf);
                    attrs.Remove(StoredBodies.XfKey);
                }
                else if (StoredBodies.InstanceOf(e) is var (_, xf))
                {
                    // Its axes turned, its origin carried; the shared body stays in its own frame.
                    var m = xf.ToArray();
                    for (var k = 0; k < 4; k++)
                    {
                        var (x, y) = (xf[k * 3], xf[k * 3 + 1]);
                        (m[k * 3], m[k * 3 + 1]) = k == 3 ? At(x, y) : (x * c - y * s, x * s + y * c);
                    }
                    attrs[StoredBodies.XfKey] = StoredBodies.WriteXf(m);
                }
                if (attrs[Plan2d.Key] is JsonObject plan && plan["plan"] is JsonArray items)
                    foreach (var item in items.OfType<JsonObject>())
                    {
                        Flat(item["pts"] as JsonArray);
                        foreach (var loop in (item["loops"] as JsonArray)?.OfType<JsonArray>() ?? []) Flat(loop);
                        // A text stays readable when mirrored: it runs the other way along its line.
                        if (Json.Number(item["angle"]) is { } angle) item["angle"] = Math.Round((place.Mirrored ? -angle : angle) + place.Degrees, 3);
                    }
                edit.Replace(e.With(attrs: attrs));
            }
        });
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
