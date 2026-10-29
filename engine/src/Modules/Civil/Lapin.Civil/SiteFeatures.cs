using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Civil;

/// <summary>
/// What a GIS file brings onto the site (docs/plan-urban.md, stage 1): a
/// parcel or another area (a closed outline), a line, a point, or a
/// neighbouring building with its surfaces. Each keeps its plan points (and
/// heights when it has them), the name and the properties the file gave it,
/// and where it came from; a building keeps its body as triangles. Read-only
/// references the site plan draws; the premium urban module takes a parcel
/// as its plot, and a building casts shadows in the sun study.
/// </summary>
public static class SiteFeatures
{
    public const string Class = "LapinSiteFeature";
    public static readonly string[] Kinds = ["parcel", "area", "line", "point", "building"];

    public static string Kind(Entity e) => e.Str("kind") ?? "line";

    public static List<Vec3> Points(Entity e) => (e.Attrs["pts"] as JsonArray)?.OfType<JsonArray>().Where(p => p.Count >= 2)
        .Select(p => new Vec3(Json.Number(p[0]) ?? 0, Json.Number(p[1]) ?? 0, p.Count > 2 ? Json.Number(p[2]) ?? 0 : 0)).ToList() ?? [];

    public static JsonArray Json3(IEnumerable<Vec3> pts, bool z) =>
        new(pts.Select(p => (JsonNode)(z ? new JsonArray(Math.Round(p.X, 4), Math.Round(p.Y, 4), Math.Round(p.Z, 4)) : new JsonArray(Math.Round(p.X, 4), Math.Round(p.Y, 4)))).ToArray());

    public static string Color(string kind) => kind switch { "parcel" => "#6b4e2e", "building" => "#9aa0a6", "area" => "#5b8c5a", "point" => "#2e6db4", _ => "#7a7a7a" };

    public static Entity Add(ICommandContext ctx, string storeyId, string kind, string name, IReadOnlyList<Vec3> pts, bool hasZ, string source, JsonObject? properties = null, JsonObject? body = null)
    {
        var attrs = new JsonObject { ["kind"] = kind, ["pts"] = Json3(pts, hasZ), ["source"] = source };
        if (properties is { Count: > 0 }) attrs["properties"] = properties;
        if (body != null) attrs[StoredBodies.Key] = body;
        var e = new Entity(ctx.NewId(), Class, name, attrs);
        ctx.Add(e);
        Spatial.Contain(ctx, storeyId, e.Id);
        return e;
    }

    public static ElementGeometry? Geometry(Entity e, IModelView m, IServices s)
    {
        var kind = Kind(e);
        var pts = Points(e).Select(p => p.Xy).ToList();
        var plan = new List<PlanItem>();
        if (kind == "point")
            foreach (var p in pts) plan.Add(new PlanItem("symbol", true, [p + new Vec2(-0.15, 0), p + new Vec2(0, 0.15), p + new Vec2(0.15, 0), p + new Vec2(0, -0.15)]));
        else if (pts.Count >= 2) plan.Add(new PlanItem(kind == "parcel" ? "heavy" : "symbol", kind is "parcel" or "area" or "building", pts));
        if (kind == "building" && StoredBodies.Has(e) && StoredBodies.Geometry(e, m, s) is { } g)
            return g with { Plan = [.. g.Plan ?? [], .. plan], Snaps = pts };
        if (pts.Count >= 3 && kind is "parcel" or "area") plan.Add(new PlanItem("hit", true, pts));
        return new ElementGeometry([], plan, pts);
    }

    public static IEnumerable<QuantitySet> Quantities(Entity e, ElementGeometry? g, IModelView m)
    {
        var pts = Points(e).Select(p => p.Xy).ToList();
        var kind = Kind(e);
        if (kind is "parcel" or "area" or "building" && pts.Count >= 3)
        {
            double per = 0;
            for (var i = 0; i < pts.Count; i++) per += (pts[(i + 1) % pts.Count] - pts[i]).Length;
            yield return new QuantitySet("Lapin_SiteFeature", [new("Area", Math.Abs(Polygon2.SignedArea(pts)), "AREA"), new("Perimeter", per, "LENGTH")]);
        }
        else if (kind == "line" && pts.Count >= 2)
        {
            double len = 0;
            for (var i = 1; i < pts.Count; i++) len += (pts[i] - pts[i - 1]).Length;
            yield return new QuantitySet("Lapin_SiteFeature", [new("Length", len, "LENGTH")]);
        }
    }

    /// <summary>A site feature moves, turns and mirrors as any element: its points (and a building's body).</summary>
    public sealed class Placement : IElementPlacement
    {
        public bool Handles(Entity e) => e.Class == Class;

        public void Transform(ICommandContext ctx, Entity e, Placement2D t, IReadOnlySet<string> moving)
        {
            var attrs = e.CloneAttrs();
            var hasZ = (e.Attrs["pts"] as JsonArray)?.OfType<JsonArray>().Any(p => p.Count > 2) ?? false;
            attrs["pts"] = Json3(Points(e).Select(p => { var q = t.Point(p.Xy); return new Vec3(q.X, q.Y, p.Z); }), hasZ);
            if (attrs[StoredBodies.Key] is JsonObject body && body["parts"] is JsonArray parts)
                for (var k = 0; k < parts.Count; k++)
                    if (parts[k] is JsonObject part)
                    {
                        var (v, tris) = StoredBodies.ReadPart(part);
                        for (var i = 0; i + 2 < v.Length; i += 3)
                        {
                            var q = t.Point(new Vec2(v[i], v[i + 1]));
                            v[i] = Math.Round(q.X, 4); v[i + 1] = Math.Round(q.Y, 4);
                        }
                        parts[k] = StoredBodies.WritePart((string?)part["name"] ?? "Body", (string?)part["color"] ?? "#c9ced6", v, tris);
                    }
            ctx.Replace(e.With(attrs: attrs));
        }
    }
}

/// <summary>
/// An orthophoto under the site: an image (PNG or JPEG, a <see cref="Images"/>
/// resource) laid on the plan by its four corners, from its world file or
/// typed; drawn under the plan and draped on the terrain in 3D.
/// </summary>
public static class Orthophotos
{
    public const string Class = "LapinOrthophoto";

    /// <summary>The image's corners in plan: top left, top right, bottom right, bottom left.</summary>
    public static List<Vec2> Corners(Entity e) => (e.Attrs["corners"] as JsonArray)?.OfType<JsonArray>().Where(p => p.Count >= 2)
        .Select(p => new Vec2(Json.Number(p[0]) ?? 0, Json.Number(p[1]) ?? 0)).ToList() ?? [];

    /// <summary>A world file's six numbers (A, D, B, E, C, F: pixel size in x, two rotations, pixel size in y, the upper-left pixel's centre).</summary>
    public static double[]? WorldFile(string text)
    {
        var v = text.Split(['\n', '\r', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries)
            .Select(s => double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : double.NaN).ToArray();
        return v.Length >= 6 && v.Take(6).All(double.IsFinite) && Math.Abs(v[0] * v[3] - v[1] * v[2]) > 1e-18 ? v[..6] : null;
    }

    /// <summary>The map corners of an image of <paramref name="w"/> × <paramref name="h"/> pixels laid by a world file (pixel edges, not centres).</summary>
    public static (double X, double Y)[] MapCorners(double[] wf, int w, int h)
    {
        var (a, d, b, e, c, f) = (wf[0], wf[1], wf[2], wf[3], wf[4], wf[5]);
        (double, double) At(double col, double row) => (a * col + b * row + c, d * col + e * row + f);
        return [At(-0.5, -0.5), At(w - 0.5, -0.5), At(w - 0.5, h - 0.5), At(-0.5, h - 0.5)];
    }

    /// <summary>A PNG's or a JPEG's size in pixels, read from its header; null when neither.</summary>
    public static (int W, int H)? PixelSize(byte[] b)
    {
        if (Images.Sniff(b) == "image/png" && b.Length > 24)
            return ((b[16] << 24) | (b[17] << 16) | (b[18] << 8) | b[19], (b[20] << 24) | (b[21] << 16) | (b[22] << 8) | b[23]);
        if (Images.Sniff(b) != "image/jpeg") return null;
        var i = 2;
        while (i + 9 < b.Length)
        {
            if (b[i] != 0xFF) { i++; continue; }
            var marker = b[i + 1];
            if (marker is >= 0xC0 and <= 0xCF and not 0xC4 and not 0xC8 and not 0xCC)
                return ((b[i + 7] << 8) | b[i + 8], (b[i + 5] << 8) | b[i + 6]);
            if (marker is 0xD8 or 0x01 || marker is >= 0xD0 and <= 0xD7) { i += 2; continue; }
            i += 2 + ((b[i + 2] << 8) | b[i + 3]);
        }
        return null;
    }

    public static ElementGeometry? Geometry(Entity e, IModelView m, IServices s)
    {
        var c = Corners(e);
        if (c.Count != 4) return null;
        return new ElementGeometry([], [new PlanItem("hit", true, c)], c);
    }
}
