using System.Text.Json.Nodes;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// A view's crop region and annotation crop (forum topic 68, as Revit's): a rectangle on a plan, section, elevation or
/// reference plane (<c>crop: {on, visible, min, max, annotation, offset}</c> on the view) outside which the model is not
/// drawn, its lines and fills cut at its edge; and, when <c>annotation</c> is on, a second rectangle round it, wider by
/// <c>offset</c> (paper mm: left, bottom, right, top) beyond which annotations are not drawn: a dimension, a tag, a text
/// or a symbol wholly outside it goes, one across it is cut at its edge (a text kept when its point is inside). Crop
/// off, the region stays kept and the view draws everything. <c>visible</c>: its frame drawn, on the screen and on
/// paper; hidden, neither. The region is square to the view as it shows (a turned plan's, decision 526, to the screen):
/// its corners are kept in the turned frame. Applied in <see cref="Views.Render"/>, so sheets, PDF and DXF print what the
/// view shows. The 3D view's section box stays its own.
/// </summary>
public static class ViewCrops
{
    public const string Key = "crop";
    public const string Layer = "CROP";
    /// <summary>The annotation crop's offset round the crop region when none is given (paper mm, each side).</summary>
    public const double DefaultOffset = 10;
    /// <summary>The margin a crop turned on round what the view draws now takes (m).</summary>
    public const double Margin = 0.5;

    /// <summary>A view's crop as kept: on or off, its frame shown, its corners in the view's turned frame, the annotation crop and its offsets (mm).</summary>
    public sealed record Crop(bool On, bool Visible, Vec2 Min, Vec2 Max, bool Annotation, double[] Offset)
    {
        public JsonObject ToJson() => new()
        {
            ["on"] = On, ["visible"] = Visible,
            ["min"] = new JsonArray(Math.Round(Min.X, 6), Math.Round(Min.Y, 6)), ["max"] = new JsonArray(Math.Round(Max.X, 6), Math.Round(Max.Y, 6)),
            ["annotation"] = Annotation,
            ["offset"] = new JsonArray(Offset.Select(o => (JsonNode)Math.Round(o, 3)).ToArray()),
        };
    }

    /// <summary>The view's crop as kept (null when it has none: never cropped).</summary>
    public static Crop? Of(Entity view)
    {
        if (view.Attrs[Key] is not JsonObject o || Args.TryPoint(o["min"]) is not { } a || Args.TryPoint(o["max"]) is not { } b) return null;
        var off = Offsets(o["offset"]) ?? [DefaultOffset, DefaultOffset, DefaultOffset, DefaultOffset];
        return new Crop(o["on"] is JsonValue on && on.TryGetValue<bool>(out var x) && x, !(o["visible"] is JsonValue v && v.TryGetValue<bool>(out var y) && !y),
            new Vec2(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y)), new Vec2(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y)),
            o["annotation"] is JsonValue an && an.TryGetValue<bool>(out var z) && z, off);
    }

    /// <summary>Offsets as given: one number for every side, or four (left, bottom, right, top); null when they are not.</summary>
    static double[]? Offsets(JsonNode? n)
    {
        if (Json.Number(n) is { } one) return one >= 0 && double.IsFinite(one) ? [one, one, one, one] : null;
        if (n is JsonArray { Count: 4 } a && a.Select(Json.Number).ToList() is var ds && ds.All(d => d is >= 0 && double.IsFinite(d.Value))) return [.. ds.Select(d => d!.Value)];
        return null;
    }

    /// <summary>The crop the view draws with now: its region when it is on, null when it is off or has none (a 3D view has none).</summary>
    public static Active? ActiveOf(IModelView m, Entity view, double scale)
    {
        if (view.Str("kind") == "3d" || Of(view) is not { On: true } c) return null;
        var k = scale / 1000;
        var annot = c.Annotation ? (new Vec2(c.Min.X - c.Offset[0] * k, c.Min.Y - c.Offset[1] * k), new Vec2(c.Max.X + c.Offset[2] * k, c.Max.Y + c.Offset[3] * k)) : ((Vec2, Vec2)?)null;
        return new Active(c, Views.Turn(m, view), (c.Min, c.Max), annot);
    }

    /// <summary>A crop at work: the view's turn (radians), the model's box and the annotations' (null: annotations are not cropped), in the turned frame.</summary>
    public sealed record Active(Crop Crop, double Turn, (Vec2 Min, Vec2 Max) Model, (Vec2 Min, Vec2 Max)? Annotations)
    {
        public Vec2 Local(Vec2 p) => Turn == 0 ? p : Turning.Turn(p, Turn);
        public Vec2 World(Vec2 p) => Turn == 0 ? p : Turning.Turn(p, -Turn);

        /// <summary>A box's corners in the drawing's own coordinates (lower left, lower right, upper right, upper left as the view shows).</summary>
        public Vec2[] Corners((Vec2 Min, Vec2 Max) b) => [World(b.Min), World(new(b.Max.X, b.Min.Y)), World(b.Max), World(new(b.Min.X, b.Max.Y))];

        public bool Inside(Vec2 p, (Vec2 Min, Vec2 Max) b)
        {
            var l = Local(p);
            return l.X >= b.Min.X - 1e-9 && l.X <= b.Max.X + 1e-9 && l.Y >= b.Min.Y - 1e-9 && l.Y <= b.Max.Y + 1e-9;
        }

        public bool Segment(ref Vec2 a, ref Vec2 b, (Vec2 Min, Vec2 Max) box)
        {
            var (la, lb) = (Local(a), Local(b));
            if (!Sheets.ClipSegment(ref la, ref lb, box.Min.X, box.Min.Y, box.Max.X, box.Max.Y)) return false;
            (a, b) = (World(la), World(lb));
            return (b - a).Length > 1e-12 || Inside(a, box);
        }

        public List<Vec2> Loop(IReadOnlyList<Vec2> loop, (Vec2 Min, Vec2 Max) box) =>
            [.. Sheets.ClipLoop(loop.Select(Local).ToList(), box.Min.X, box.Min.Y, box.Max.X, box.Max.Y).Select(World)];

        /// <summary>A pen cut to a box: segments and fills clipped, texts kept when their point is inside, pictures clipped where drawn (a turned box: kept whole); null when nothing is left.</summary>
        public Pen? Clip(Pen pen, (Vec2 Min, Vec2 Max) box)
        {
            var o = new Pen(pen.Layer, pen.Stroke, pen.Width, pen.Fill, pen.Dash) { Viewport = pen.Viewport, Block = pen.Block, Clip = pen.Clip };
            foreach (var (a0, b0) in pen.Segments)
            {
                var (a, b) = (a0, b0);
                if (Segment(ref a, ref b, box)) o.Segments.Add((a, b));
            }
            foreach (var l in pen.Loops) if (Loop(l, box) is { Count: >= 3 } c) o.Loops.Add(c);
            foreach (var t in pen.Texts) if (Inside(t.At, box)) o.Texts.Add(t);
            o.Images.AddRange(pen.Images);
            if (o.Images.Count > 0 && Turn == 0 && o.Clip == null) o.Clip = (box.Min.X, box.Min.Y, box.Max.X, box.Max.Y);
            return o.Segments.Count + o.Loops.Count + o.Texts.Count + o.Images.Count > 0 ? o : null;
        }

        /// <summary>
        /// A mark cut to a box in place: its lines, edges and fills clipped, its texts and snaps kept when inside; false when
        /// nothing of it is left to draw or pick (it is wholly outside).
        /// </summary>
        public bool Clip(ViewMark mk, (Vec2 Min, Vec2 Max) box)
        {
            var lines = new List<(Vec2 A, Vec2 B, string Layer, double Width, string Color, double[]? Dash)>();
            foreach (var l in mk.Lines)
            {
                var (a, b) = (l.A, l.B);
                if (Segment(ref a, ref b, box)) lines.Add((a, b, l.Layer, l.Width, l.Color, l.Dash));
            }
            var edges = new List<(Vec2 A, Vec2 B)>();
            foreach (var (a0, b0) in mk.Edges)
            {
                var (a, b) = (a0, b0);
                if (Segment(ref a, ref b, box)) edges.Add((a, b));
            }
            var fills = mk.Fills.Select(f => (Loops: (IReadOnlyList<IReadOnlyList<Vec2>>)f.Loops.Select(l => (IReadOnlyList<Vec2>)Loop(l, box)).Where(l => l.Count >= 3).ToList(), f.Color)).Where(f => f.Loops.Count > 0).ToList();
            var texts = mk.Texts.Where(t => Inside(t.Text.At, box)).ToList();
            var snaps = mk.Snaps.Where(p => Inside(p, box)).ToList();
            // An opening's pick area (its loops), kept while any of it is inside.
            var hit = mk.Hit is { } h && h.Loops.Any(l => l.Any(p => Inside(p, box))) ? mk.Hit : null;
            mk.Lines.Clear(); mk.Lines.AddRange(lines);
            mk.Edges.Clear(); mk.Edges.AddRange(edges);
            mk.Fills.Clear(); mk.Fills.AddRange(fills);
            mk.Texts.Clear(); mk.Texts.AddRange(texts);
            mk.Snaps.Clear(); mk.Snaps.AddRange(snaps);
            mk.Hit = hit;
            return lines.Count + edges.Count + fills.Count + texts.Count + mk.Images.Count > 0 || hit != null;
        }

        /// <summary>Whether a mark is of the model (an element's pick lines, a room's fill under the model, a picture, a point cloud's slice): cut by the crop region, not the annotation crop.</summary>
        public static bool OfModel(ViewMark mk) => mk.Pick || mk.Under || mk.Images.Count > 0 && mk.Lines.Count == 0 && mk.Texts.Count == 0;

        /// <summary>The view's marks cut as the crop says: the model's to its region, annotations to the annotation crop when it is on; what is left with nothing goes.</summary>
        public void Marks(List<ViewMark> marks) =>
            marks.RemoveAll(mk => OfModel(mk) ? !Clip(mk, Model) : Annotations is { } ab && !Clip(mk, ab));

        /// <summary>The crop region's frame, drawn on screen and paper when it is visible; picked as the view (its grips move it).</summary>
        public ViewMark Frame(string viewId)
        {
            var mk = new ViewMark(viewId) { Data = new JsonObject { ["crop"] = true } };
            var cs = Corners(Model);
            for (var i = 0; i < 4; i++) mk.Line(cs[i], cs[(i + 1) % 4], Layer, 0.25, "#000000");
            mk.Snaps.AddRange(cs);
            return mk;
        }
    }

    /// <summary>
    /// A view's crop changed ({id, on?, visible?, min?, max?, annotation?, offset?}): one step. Turned on with no region yet,
    /// it takes what the view draws now, with a margin. A level's default plan becomes its plan view with it (decision 386).
    /// </summary>
    public static void Register(IModuleRegistry r)
    {
        r.Command("view.setCrop", "Crop view", (ctx, a) =>
        {
            var found = Views.Find(ctx.Model, (string?)a["id"] ?? "") ?? throw new CommandException("There is no such view.");
            if (found.Str("kind") == "3d") throw new CommandException("A 3D view is cut by its section box, not cropped.");
            var old = Of(found);
            static bool? Flag(JsonNode? n) => n is JsonValue v && v.TryGetValue<bool>(out var b) ? b : null;
            var on = Flag(a["on"]) ?? old?.On ?? false;
            Vec2? min = a.ContainsKey("min") ? Args.Point(a["min"], "crop region's corner") : old?.Min;
            Vec2? max = a.ContainsKey("max") ? Args.Point(a["max"], "crop region's corner") : old?.Max;
            if (min == null || max == null)
            {
                // No region yet: round what the view draws now (as it shows, turned), with a margin.
                var drawn = Views.Render(ctx.Query, found, print: true);
                var b = Turning.Bounds(drawn.Pens, Views.Turn(ctx.Model, found)) ?? throw new CommandException("The view draws nothing yet: give the crop region's corners.");
                (min, max) = (b.Min - new Vec2(Margin, Margin), b.Max + new Vec2(Margin, Margin));
            }
            var (lo, hi) = (new Vec2(Math.Min(min.Value.X, max.Value.X), Math.Min(min.Value.Y, max.Value.Y)), new Vec2(Math.Max(min.Value.X, max.Value.X), Math.Max(min.Value.Y, max.Value.Y)));
            if (hi.X - lo.X < 0.05 || hi.Y - lo.Y < 0.05) throw new CommandException("A crop region is 5 cm across at least.");
            double[] offset = a.ContainsKey("offset") ? Offsets(a["offset"]) ?? throw new CommandException("The annotation crop's offset is millimetres on paper: one number, or four (left, bottom, right, top), none below 0.")
                : old?.Offset ?? [DefaultOffset, DefaultOffset, DefaultOffset, DefaultOffset];
            var crop = new Crop(on, Flag(a["visible"]) ?? old?.Visible ?? true, lo, hi, Flag(a["annotation"]) ?? old?.Annotation ?? false, offset);
            var id = Views.OwnView(ctx, found, attrs => attrs[Key] = crop.ToJson());
            return new JsonObject { ["id"] = id, ["crop"] = crop.ToJson() };
        });
    }
}
