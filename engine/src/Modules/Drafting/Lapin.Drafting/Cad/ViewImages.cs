using System.Text.Json.Nodes;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Platform.Cde.PdfRemarks;

namespace Lapin.Drafting.Cad;

/// <summary>
/// Pictures and PDFs to trace over (a tester's wish, #89, decision 371). An image (a scan, a photo, a page of a PDF) lies in a
/// view as a <see cref="Class"/>: its bottom-left corner, its width and height in the view's metres, its turn, its
/// see-through; drawn under everything, moved and turned as any drafting, scaled true by two points and the distance between
/// them. A PDF page's lines come in as drafted polylines at the scale its drawing was made to, its pictures as images, the
/// whole as one group named after the file, so it moves as one.
/// </summary>
public static class ViewImages
{
    public const string Class = "LapinViewImage";

    public static IEnumerable<Entity> On(IModelView m, string viewId) => m.OfClass(Class).Where(e => e.Str("viewId") == viewId);

    static Vec2 P(JsonNode? n) => n is JsonArray a && a.Count >= 2 ? new Vec2(Json.Number(a[0]) ?? 0, Json.Number(a[1]) ?? 0) : default;

    /// <summary>An image's four corners in the view: from its bottom-left corner, along its turn.</summary>
    public static Vec2[] Corners(Entity e)
    {
        var o = new Vec2(e.Num("x"), e.Num("y"));
        var r = e.Num("rotation") * Math.PI / 180;
        var (u, v) = (new Vec2(Math.Cos(r), Math.Sin(r)), new Vec2(-Math.Sin(r), Math.Cos(r)));
        double w = e.Num("width"), h = e.Num("height");
        return [o, o + u * w, o + u * w + v * h, o + v * h];
    }

    /// <summary>A PNG's or a JPEG's size in pixels, from its header; null when neither says.</summary>
    public static (int W, int H)? PixelSize(byte[] b)
    {
        if (b.Length > 24 && b[0] == 0x89 && b[1] == 0x50)
            return ((b[16] << 24) | (b[17] << 16) | (b[18] << 8) | b[19], (b[20] << 24) | (b[21] << 16) | (b[22] << 8) | b[23]);
        if (b.Length > 4 && b[0] == 0xFF && b[1] == 0xD8)
            for (var i = 2; i + 9 < b.Length;)
            {
                if (b[i] != 0xFF) { i++; continue; }
                var marker = b[i + 1];
                var len = (b[i + 2] << 8) | b[i + 3];
                // A start of frame (SOF0–SOF15 but DHT, JPG, DAC): its height then its width.
                if (marker is >= 0xC0 and <= 0xCF and not 0xC4 and not 0xC8 and not 0xCC)
                    return ((b[i + 7] << 8) | b[i + 8], (b[i + 5] << 8) | b[i + 6]);
                i += 2 + len;
            }
        return null;
    }

    /// <summary>An image resource made from bytes and a picture of it laid in a view.</summary>
    public static Entity Place(ICommandContext ctx, string viewId, byte[] bytes, string mime, string name, Vec2 at, double width, double? height, double rotation, double opacity)
    {
        var image = new Entity(ctx.NewId(), Images.Class, name, new JsonObject { ["mime"] = mime, ["data"] = Convert.ToBase64String(bytes) });
        ctx.Add(image);
        var (pw, ph) = PixelSize(bytes) ?? (1, 1);
        var h = height ?? width * ph / Math.Max(1, pw);
        var e = new Entity(ctx.NewId(), Class, name, new JsonObject
        {
            ["viewId"] = viewId, ["image"] = image.Id, ["x"] = at.X, ["y"] = at.Y, ["width"] = width, ["height"] = h,
            ["rotation"] = rotation, ["opacity"] = Math.Clamp(opacity, 0.05, 1),
        });
        ctx.Add(e);
        return e;
    }

    static byte[] Bytes(JsonObject a, out string name)
    {
        if ((string?)a["data"] is { Length: > 0 } data)
        {
            name = (string?)a["name"] ?? "file";
            try { return Convert.FromBase64String(data); }
            catch (FormatException) { throw new CommandException("The file's data is not base64."); }
        }
        var path = HostFiles.Check((string?)a["path"] ?? throw new CommandException("Which file?"));
        if (!File.Exists(path)) throw new CommandException($"There is no file at {path}.");
        name = Path.GetFileName(path);
        return File.ReadAllBytes(path);
    }

    public static void Register(IModuleRegistry r)
    {
        r.Class(new(Class, "Image to trace",
        [
            new("width", "Width", "length", Min: 0.01), new("height", "Height", "length", Min: 0.01), new("rotation", "Rotation", "angle"),
            new("opacity", "Opacity", "number", Min: 0.05, Max: 1),
        ], "#888888", Kind: "annotation"));
        r.Service<IElementPlacement>(new ImagePlacement());

        // A picture into a view: at a point, as wide as asked (its height by its pixels), half see-through.
        r.Command("placeImage", "Import image", (ctx, a) =>
        {
            var view = Views.Find(ctx.Model, (string?)a["viewId"] ?? "") ?? throw new CommandException("An image goes into a view.");
            var bytes = Bytes(a, out var name);
            var mime = Images.Sniff(bytes) ?? throw new CommandException("An image to trace is a PNG or a JPEG (a PDF is imported as a PDF).");
            if (bytes.Length > 40 * 1024 * 1024) throw new CommandException("An image is at most 40 MB.");
            var at = a["at"] is JsonArray ? P(a["at"]) : default;
            var width = Json.Number(a["width"]) ?? 10;
            if (!(width > 0)) throw new CommandException("An image needs a width.");
            var e = Place(ctx, view.Id, bytes, mime, Path.GetFileNameWithoutExtension(name), at, width, null, Json.Number(a["rotation"]) ?? 0, Json.Number(a["opacity"]) ?? 0.5);
            return new JsonObject { ["id"] = e.Id };
        });

        // An image scaled true: two points on it and the distance they really are apart; it grows or shrinks about the first.
        r.Command("calibrateImage", "Scale image", (ctx, a) =>
        {
            var e = ctx.Model.Get((string?)a["id"] ?? "") is { Class: Class } x ? x : throw new CommandException("Pick an image to scale.");
            var (p, q) = (P(a["a"]), P(a["b"]));
            var length = Json.Number(a["length"]) ?? 0;
            var d = (q - p).Length;
            if (d < 1e-6 || !(length > 0)) throw new CommandException("Two different points and the distance between them, please.");
            var k = length / d;
            var attrs = e.CloneAttrs();
            var o = new Vec2(e.Num("x"), e.Num("y"));
            var moved = p + (o - p) * k;
            attrs["x"] = moved.X; attrs["y"] = moved.Y;
            attrs["width"] = e.Num("width") * k; attrs["height"] = e.Num("height") * k;
            ctx.Replace(e.With(attrs: attrs));
            return new JsonObject { ["factor"] = k };
        });

        // What a PDF holds: its pages and their sizes (mm), to choose from.
        r.Query("pdfInfo", (q, a) =>
        {
            var bytes = Bytes(a, out var name);
            var pages = PdfImport.Pages(bytes);
            return new JsonObject
            {
                ["name"] = name,
                ["pages"] = new JsonArray(pages.Select(p => (JsonNode)new JsonArray(Math.Round(p.W * 25.4 / 72, 1), Math.Round(p.H * 25.4 / 72, 1))).ToArray()),
            };
        });

        // A PDF page into a view: its lines as drafted polylines and its pictures as images, at the scale its drawing was made to
        // (1:N, a paper millimetre N view millimetres), its bottom-left corner at a point; all of it one group named after the file.
        r.Command("importPdf", "Import PDF", (ctx, a) =>
        {
            var view = Views.Find(ctx.Model, (string?)a["viewId"] ?? "") ?? throw new CommandException("A PDF is imported into a view.");
            var bytes = Bytes(a, out var name);
            var index = (int)(Json.Number(a["page"]) ?? 1) - 1;
            var page = PdfImport.Read(bytes, index);
            var scale = Json.Number(a["scale"]) ?? 100;
            if (!(scale > 0)) throw new CommandException("The PDF's scale is 1:N, N above 0.");
            var at = a["at"] is JsonArray ? P(a["at"]) : default;
            var k = 25.4 / 72 / 1000 * scale;
            Vec2 Map(Vec2 p) => at + p * k;
            bool Flag(string key) => a[key] is not JsonValue v || !v.TryGetValue<bool>(out var b) || b;
            var layer = (string?)a["layer"] is { Length: > 0 } l && Drafts.Layers(ctx.Model).Any(x => x.Name == l) ? l : "0";
            var made = new List<string>();
            var title = Path.GetFileNameWithoutExtension(name);
            if (Flag("pictures"))
                foreach (var pic in page.Pictures)
                {
                    var c = pic.Corners.Select(Map).ToArray();
                    var (u, v) = (c[1] - c[0], c[3] - c[0]);
                    if (u.Length < 1e-6 || v.Length < 1e-6) continue;
                    var e = Place(ctx, view.Id, pic.Bytes, pic.Mime, $"{title}, p. {index + 1}", c[0], u.Length, v.Length, Math.Atan2(u.Y, u.X) * 180 / Math.PI, Json.Number(a["opacity"]) ?? 0.6);
                    made.Add(e.Id);
                }
            var lines = 0;
            if (Flag("lines"))
                foreach (var path in page.Paths)
                {
                    var pts = path.Points.Select(Map).ToList();
                    // Points closer than a tenth of a millimetre of paper go.
                    var kept = new List<Vec2> { pts[0] };
                    foreach (var p in pts.Skip(1)) if ((p - kept[^1]).Length > k * 0.28) kept.Add(p);
                    if (kept.Count < 2) continue;
                    var closed = path.Closed && kept.Count > 2;
                    var shape = kept.Count == 2 && !closed ? (Shape)new Line(kept[0], kept[1]) : new Polyline(kept, closed);
                    made.Add(Drafts.Add(ctx, view.Id, layer, shape).Id);
                    lines++;
                }
            if (made.Count == 0) throw new CommandException(page.Warnings.Count > 0 ? page.Warnings[0] : "The page has no lines or pictures to import (only text, perhaps).");
            var group = Groups.Make(ctx, title, made);
            return new JsonObject
            {
                ["groupId"] = group?.Id, ["lines"] = lines, ["pictures"] = made.Count - lines,
                ["warnings"] = new JsonArray(page.Warnings.Select(w => (JsonNode)w).ToArray()),
            };
        });
    }

    /// <summary>The view's images, under all its drawing, with a hairline round each to pick it by.</summary>
    public static IEnumerable<ViewMark> Marks(ViewContext c)
    {
        foreach (var e in On(c.Model, c.View.Id))
        {
            if (e.Str("image") is not { } image) continue;
            var corners = Corners(e);
            var mk = new ViewMark(e.Id);
            // A PenImage's corners run top left, top right, bottom right, bottom left; ours run from the bottom left (decision 427:
            // laid in the other order the picture was drawn upside down, which read as mirrored).
            mk.Images.Add(new PenImage(image, [corners[3], corners[2], corners[1], corners[0]], Math.Clamp(e.NumOrNull("opacity") ?? 0.5, 0.05, 1)));
            mk.Polyline(corners, true, Views.ConstructionLayer, 0.13, "#9a9a9a");
            mk.Snaps.AddRange(corners);
            yield return mk;
        }
    }
}

/// <summary>An image moved, turned or mirrored with its view's drafting: its corner and turn follow (a mirror turns it back the other way).</summary>
sealed class ImagePlacement : IElementPlacement
{
    public bool Handles(Entity e) => e.Class == ViewImages.Class;

    public void Transform(ICommandContext ctx, Entity e, Placement2D t, IReadOnlySet<string> moving)
    {
        var attrs = e.CloneAttrs();
        var c = ViewImages.Corners(e).Select(t.Point).ToArray();
        // Mirrored, the picture would read backwards: its corner taken from the mirrored bottom-right instead.
        var (o, u) = t.Mirrored ? (c[1], c[0] - c[1]) : (c[0], c[1] - c[0]);
        attrs["x"] = o.X; attrs["y"] = o.Y;
        attrs["rotation"] = Math.Atan2(u.Y, u.X) * 180 / Math.PI;
        ctx.Replace(e.With(attrs: attrs));
    }
}
