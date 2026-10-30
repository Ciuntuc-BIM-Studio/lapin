using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Platform.Cde.Remarks;

/// <summary>
/// The BCF subset Lapin exchanges (docs/plan-cde.md, stage 2), after the
/// buildingSMART schemas of BCF 2.1 and 3.0: a topic with its fields, its
/// comments and its viewpoints (a camera, the components selected or hidden,
/// clipping planes, lines, a snapshot). Coordinates are metres in the model's
/// frame (Z up, as IFC); dates are ISO 8601 text as the file gave them.
/// </summary>
public sealed record BcfPoint(double X, double Y, double Z)
{
    public JsonArray ToJson() => [X, Y, Z];
    public static BcfPoint? From(JsonNode? n) => n is JsonArray a && a.Count == 3 ? new(Json.Number(a[0]) ?? 0, Json.Number(a[1]) ?? 0, Json.Number(a[2]) ?? 0) : null;
}

/// <summary>A viewpoint's camera: perspective (a vertical field of view, degrees) or orthogonal (the view's height in metres).</summary>
public sealed record BcfCamera(bool Perspective, BcfPoint ViewPoint, BcfPoint Direction, BcfPoint Up, double FieldOfView, double ViewToWorldScale, double? AspectRatio = null)
{
    public JsonObject ToJson()
    {
        var o = new JsonObject { ["perspective"] = Perspective, ["viewPoint"] = ViewPoint.ToJson(), ["direction"] = Direction.ToJson(), ["up"] = Up.ToJson(), ["fieldOfView"] = FieldOfView, ["viewToWorldScale"] = ViewToWorldScale };
        if (AspectRatio is { } r) o["aspectRatio"] = r;
        return o;
    }

    public static BcfCamera? From(JsonNode? n) => n is JsonObject o && BcfPoint.From(o["viewPoint"]) is { } p && BcfPoint.From(o["direction"]) is { } d
        ? new(o["perspective"] is JsonValue v && v.TryGetValue<bool>(out var persp) ? persp : true, p, d, BcfPoint.From(o["up"]) ?? new(0, 0, 1),
            Json.Number(o["fieldOfView"]) ?? 45, Json.Number(o["viewToWorldScale"]) ?? 10, Json.Number(o["aspectRatio"]))
        : null;
}

/// <summary>A clipping plane: a point on it and its normal, pointing at the part clipped away.</summary>
public sealed record BcfClippingPlane(BcfPoint Location, BcfPoint Direction)
{
    public JsonObject ToJson() => new() { ["location"] = Location.ToJson(), ["direction"] = Direction.ToJson() };
    public static BcfClippingPlane? From(JsonNode? n) => n is JsonObject o && BcfPoint.From(o["location"]) is { } l && BcfPoint.From(o["direction"]) is { } d ? new(l, d) : null;
}

public sealed record BcfLine(BcfPoint Start, BcfPoint End)
{
    public JsonObject ToJson() => new() { ["start"] = Start.ToJson(), ["end"] = End.ToJson() };
    public static BcfLine? From(JsonNode? n) => n is JsonObject o && BcfPoint.From(o["start"]) is { } s && BcfPoint.From(o["end"]) is { } e ? new(s, e) : null;
}

/// <summary>
/// A viewpoint: the camera, the components selected (IfcGuids), which are
/// visible by default and the exceptions, the clipping planes and lines, and
/// the snapshot (PNG bytes).
/// </summary>
public sealed record BcfViewpoint(Guid Guid, BcfCamera? Camera, IReadOnlyList<string> Selection, bool DefaultVisibility, IReadOnlyList<string> Exceptions,
    IReadOnlyList<BcfClippingPlane> ClippingPlanes, IReadOnlyList<BcfLine> Lines, byte[]? Snapshot, int? Index = null)
{
    /// <summary>As kept on the remark; the snapshot as base64 unless left out (<paramref name="withSnapshot"/> false: the interface's list).</summary>
    public JsonObject ToJson(bool withSnapshot = true)
    {
        var o = new JsonObject
        {
            ["guid"] = Guid.ToString("D"),
            ["selection"] = new JsonArray(Selection.Select(s => (JsonNode)s).ToArray()),
            ["defaultVisibility"] = DefaultVisibility,
            ["exceptions"] = new JsonArray(Exceptions.Select(s => (JsonNode)s).ToArray()),
            ["clippingPlanes"] = new JsonArray(ClippingPlanes.Select(p => (JsonNode)p.ToJson()).ToArray()),
            ["lines"] = new JsonArray(Lines.Select(l => (JsonNode)l.ToJson()).ToArray()),
            ["hasSnapshot"] = Snapshot is { Length: > 0 },
        };
        if (Camera != null) o["camera"] = Camera.ToJson();
        if (Index is { } i) o["index"] = i;
        if (withSnapshot && Snapshot is { Length: > 0 }) o["snapshot"] = Convert.ToBase64String(Snapshot);
        return o;
    }

    public static BcfViewpoint? From(JsonNode? n)
    {
        if (n is not JsonObject o) return null;
        var guid = Bcf.ParseGuid((string?)o["guid"]) ?? System.Guid.NewGuid();
        byte[]? snapshot = null;
        if ((string?)o["snapshot"] is { Length: > 0 } b64) { try { snapshot = Convert.FromBase64String(b64); } catch (FormatException) { snapshot = null; } }
        return new(guid, BcfCamera.From(o["camera"]), Bcf.Strings(o["selection"]), o["defaultVisibility"] is JsonValue v && v.TryGetValue<bool>(out var dv) ? dv : true, Bcf.Strings(o["exceptions"]),
            (o["clippingPlanes"] as JsonArray ?? []).Select(BcfClippingPlane.From).OfType<BcfClippingPlane>().ToList(),
            (o["lines"] as JsonArray ?? []).Select(BcfLine.From).OfType<BcfLine>().ToList(), snapshot, Json.Number(o["index"]) is { } ix ? (int)ix : null);
    }
}

public sealed record BcfComment(Guid Guid, string Date, string Author, string Text, Guid? Viewpoint, string? ModifiedDate = null, string? ModifiedAuthor = null)
{
    public JsonObject ToJson()
    {
        var o = new JsonObject { ["guid"] = Guid.ToString("D"), ["date"] = Date, ["author"] = Author, ["text"] = Text };
        if (Viewpoint is { } v) o["viewpoint"] = v.ToString("D");
        if (ModifiedDate != null) o["modifiedDate"] = ModifiedDate;
        if (ModifiedAuthor != null) o["modifiedAuthor"] = ModifiedAuthor;
        return o;
    }

    public static BcfComment? From(JsonNode? n) => n is JsonObject o
        ? new(Bcf.ParseGuid((string?)o["guid"]) ?? System.Guid.NewGuid(), (string?)o["date"] ?? "", (string?)o["author"] ?? "", (string?)o["text"] ?? "", Bcf.ParseGuid((string?)o["viewpoint"]), (string?)o["modifiedDate"], (string?)o["modifiedAuthor"])
        : null;
}

/// <summary>A topic: the remark itself, with its comments and viewpoints.</summary>
public sealed record BcfTopic(Guid Guid, string Title, string? Description, string? Type, string? Status, string? Priority, int? Index, string CreationDate, string CreationAuthor,
    string? ModifiedDate, string? ModifiedAuthor, string? AssignedTo, string? DueDate, string? Stage, IReadOnlyList<string> Labels, IReadOnlyList<BcfComment> Comments, IReadOnlyList<BcfViewpoint> Viewpoints)
{
    /// <summary>The topic as the remark's attributes (its title is the entity's name).</summary>
    public JsonObject ToJson(bool withSnapshots = true)
    {
        var o = new JsonObject { ["guid"] = Guid.ToString("D"), ["creationDate"] = CreationDate, ["creationAuthor"] = CreationAuthor };
        void Put(string key, string? value) { if (value is { Length: > 0 }) o[key] = value; }
        Put("description", Description); Put("type", Type); Put("status", Status); Put("priority", Priority);
        Put("modifiedDate", ModifiedDate); Put("modifiedAuthor", ModifiedAuthor); Put("assignedTo", AssignedTo); Put("dueDate", DueDate); Put("stage", Stage);
        if (Index is { } i) o["index"] = i;
        o["labels"] = new JsonArray(Labels.Select(s => (JsonNode)s).ToArray());
        o["comments"] = new JsonArray(Comments.Select(c => (JsonNode)c.ToJson()).ToArray());
        o["viewpoints"] = new JsonArray(Viewpoints.Select(v => (JsonNode)v.ToJson(withSnapshots)).ToArray());
        return o;
    }

    public static BcfTopic From(Guid guid, string title, JsonObject o) => new(guid, title, (string?)o["description"], (string?)o["type"], (string?)o["status"], (string?)o["priority"],
        Json.Number(o["index"]) is { } ix ? (int)ix : null, (string?)o["creationDate"] ?? Bcf.Now(), (string?)o["creationAuthor"] ?? "", (string?)o["modifiedDate"], (string?)o["modifiedAuthor"],
        (string?)o["assignedTo"], (string?)o["dueDate"], (string?)o["stage"], Bcf.Strings(o["labels"]),
        (o["comments"] as JsonArray ?? []).Select(BcfComment.From).OfType<BcfComment>().ToList(),
        (o["viewpoints"] as JsonArray ?? []).Select(BcfViewpoint.From).OfType<BcfViewpoint>().ToList());
}

/// <summary>A BCF file: its version ("2.1" or "3.0") and its topics.</summary>
public sealed record BcfFile(string Version, IReadOnlyList<BcfTopic> Topics);

public static class Bcf
{
    public const string Version21 = "2.1";
    public const string Version30 = "3.0";

    /// <summary>Now as BCF writes it (xs:dateTime, UTC, whole seconds).</summary>
    public static string Now() => DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    public static Guid? ParseGuid(string? s) => s != null && Guid.TryParse(s.Trim(), out var g) ? g : null;

    public static List<string> Strings(JsonNode? n) => (n as JsonArray ?? []).Select(x => (string?)x).OfType<string>().Where(s => s.Length > 0).ToList();

    /// <summary>A number as BCF writes it (invariant, up to 6 decimals: a micron is enough for a camera).</summary>
    public static string Num(double v) => Math.Round(v, 6).ToString("0.######", CultureInfo.InvariantCulture);

    /// <summary>A number read from BCF (invariant); null when it is not one.</summary>
    public static double? ParseNum(string? s) => s != null && double.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;
}
