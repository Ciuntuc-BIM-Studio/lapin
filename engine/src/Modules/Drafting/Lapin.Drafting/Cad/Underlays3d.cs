using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Drafting.Cad;

/// <summary>
/// An imported DXF in the 3D view (a tester's report #106, decision 382): the flat drawing a DXF brought into a view
/// (its lines, polylines, circles, arcs and hatches, marked <c>dxf</c>) laid on that view's plane in the model, as
/// <see cref="Views.PlaneOf"/> reads it (a plan's at its storey's level, a section's on its cut, piece by piece when
/// developed, an elevation's once fixed, a reference plane's). Not one element each, as the hand-drafted shapes are
/// (<see cref="Drafts.Geometry3d"/>): a plan of tens of thousands of lines would be as many objects. They come in
/// batches instead, one a view's plane (a piece of a developed section), import and layer, each a buffer of segments
/// with a colour a vertex, and the hatches as triangles to be drawn faint.
/// <para>
/// What stays out: a layer turned off, the construction layer, a hidden link, an import or link with 3D turned off
/// (<see cref="SetShown"/>), the lines that have heights (those already stand in space as elements), texts and
/// dimensions. A halftone link draws grey. View → DXF in 3D turns them all off in the interface.
/// </para>
/// </summary>
public static class Underlays3d
{
    /// <summary>The import a drafted entity came in with (an id of <see cref="ImportsKey"/>).</summary>
    public const string ImportKey = "dxfImport";
    /// <summary>The project's DXF imports: id → {file, viewId, at, in3d}.</summary>
    public const string ImportsKey = "dxfImports";
    /// <summary>A link's switch (Manage → Links → 3D), and an import's.</summary>
    public const string ShownKey = "in3d";

    public static JsonObject Imports(IModelView m) => Spatial.Project(m)?.Attrs[ImportsKey] as JsonObject ?? [];

    /// <summary>A DXF import recorded on the project, its drawing shown in 3D: what <see cref="ImportKey"/> names on each entity it made.</summary>
    public static string Record(ICommandContext ctx, string file, string viewId)
    {
        var project = Spatial.Project(ctx.Model) ?? throw new CommandException("The model has no project.");
        var id = ctx.NewId();
        var attrs = project.CloneAttrs();
        var all = attrs[ImportsKey] as JsonObject ?? [];
        all[id] = new JsonObject { ["file"] = file, ["viewId"] = viewId, ["at"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture), [ShownKey] = true };
        attrs[ImportsKey] = all;
        ctx.Replace(project.With(attrs: attrs));
        return id;
    }

    static bool Shown(JsonNode? record) => record is not JsonObject o || o[ShownKey] is not JsonValue v || !v.TryGetValue<bool>(out var b) || b;

    /// <summary>Whether a drafted entity is a DXF's flat drawing: a shape marked <c>dxf</c> or anything an import recorded, never one with heights.</summary>
    public static bool IsUnderlay(Entity e) =>
        e.Class == IfcClass.Annotation && e.PredefinedType == Drafts.Predefined && (e.Bool("dxf") || e.Str(ImportKey) != null) && e.Attrs["heights"] is not JsonArray;

    /// <summary>An import's or a link's drawing shown in 3D or not (one undo step).</summary>
    public static void SetShown(ICommandContext ctx, string id, bool shown)
    {
        if (ctx.Model.Get(id) is { Class: Links.Class } link)
        {
            var la = link.CloneAttrs();
            la[ShownKey] = shown;
            ctx.Replace(link.With(attrs: la));
            return;
        }
        var project = Spatial.Project(ctx.Model) ?? throw new CommandException("The model has no project.");
        var attrs = project.CloneAttrs();
        if (attrs[ImportsKey] is not JsonObject all || all[id] is not JsonObject record) throw new CommandException("There is no such DXF import.");
        record[ShownKey] = shown;
        ctx.Replace(project.With(attrs: attrs));
    }

    /// <summary>The planes a view's drawing lies on: one, or a developed section's pieces by the station each starts at.</summary>
    sealed class ViewPlanes(List<(double Station, WorkPlane Plane)> pieces)
    {
        public int At(double x)
        {
            var k = 0;
            for (var i = 0; i < pieces.Count; i++) if (pieces[i].Station <= x + 1e-9) k = i;
            return k;
        }
        public WorkPlane this[int k] => pieces[k].Plane;

        public static ViewPlanes? Of(IModelView m, IServices services, Entity view)
        {
            if (view.Str("kind") == "section")
            {
                var (path, _, _) = SectionPaths.Resolve(m, services, view);
                if (path == null) return null;
                var list = SectionPaths.Pieces(path).Select(p => (p.Station, Plane: Views.PlaneOf(m, services, view, p.Station))).Where(p => p.Plane != null).Select(p => (p.Station, p.Plane!)).ToList();
                return list.Count == 0 ? null : new ViewPlanes(list);
            }
            return Views.PlaneOf(m, services, view) is { } plane ? new ViewPlanes([(double.NegativeInfinity, plane)]) : null;
        }
    }

    /// <summary>One batch's buffers, as they fill.</summary>
    sealed class Batch(string viewId, string? storeyId, string? source, string layer, WorkPlane plane)
    {
        public readonly string ViewId = viewId, Layer = layer;
        public readonly string? StoreyId = storeyId, Source = source;
        public readonly WorkPlane Plane = plane;
        public readonly List<float> Lines = [], Fills = [];
        public readonly List<byte> LineColors = [], FillColors = [];

        public void Segment(Vec3 a, Vec3 b, (byte R, byte G, byte B) c)
        {
            Lines.Add((float)a.X); Lines.Add((float)a.Y); Lines.Add((float)a.Z);
            Lines.Add((float)b.X); Lines.Add((float)b.Y); Lines.Add((float)b.Z);
            LineColors.Add(c.R); LineColors.Add(c.G); LineColors.Add(c.B);
            LineColors.Add(c.R); LineColors.Add(c.G); LineColors.Add(c.B);
        }

        public void Triangle(Vec3 a, Vec3 b, Vec3 c, (byte R, byte G, byte B) col)
        {
            foreach (var p in new[] { a, b, c })
            {
                Fills.Add((float)p.X); Fills.Add((float)p.Y); Fills.Add((float)p.Z);
                FillColors.Add(col.R); FillColors.Add(col.G); FillColors.Add(col.B);
            }
        }

        public JsonObject ToJson() => new()
        {
            ["key"] = $"{ViewId}|{Source}|{Layer}|{Plane.Origin.X:R},{Plane.Origin.Y:R},{Plane.Origin.Z:R}|{Plane.Normal.X:R},{Plane.Normal.Y:R},{Plane.Normal.Z:R}",
            ["viewId"] = ViewId, ["storeyId"] = StoreyId, ["source"] = Source, ["layer"] = Layer,
            ["normal"] = new JsonArray(Plane.Normal.X, Plane.Normal.Y, Plane.Normal.Z),
            ["segments"] = Lines.Count / 6, ["triangles"] = Fills.Count / 9,
            ["lines"] = Floats(Lines), ["lineColors"] = Convert.ToBase64String([.. LineColors]),
            ["fills"] = Floats(Fills), ["fillColors"] = Convert.ToBase64String([.. FillColors]),
        };

        static string Floats(List<float> v)
        {
            var bytes = new byte[v.Count * 4];
            for (var i = 0; i < v.Count; i++) BitConverter.TryWriteBytes(bytes.AsSpan(i * 4), v[i]);
            if (!BitConverter.IsLittleEndian)
                for (var i = 0; i < bytes.Length; i += 4) Array.Reverse(bytes, i, 4);
            return Convert.ToBase64String(bytes);
        }
    }

    /// <summary>A CSS colour (#rgb, #rrggbb) as bytes; grey when it is not one.</summary>
    public static (byte R, byte G, byte B) Rgb(string? css)
    {
        var s = css?.Trim().TrimStart('#') ?? "";
        if (s.Length == 3) s = string.Concat(s.Select(ch => $"{ch}{ch}"));
        return s.Length >= 6 && int.TryParse(s[..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v)
            ? ((byte)(v >> 16), (byte)(v >> 8 & 0xff), (byte)(v & 0xff))
            : ((byte)128, (byte)128, (byte)128);
    }

    /// <summary>Every DXF drawing shown in 3D, in batches (see the class); <paramref name="viewId"/> only that view's.</summary>
    public static JsonArray Batches(IModelView m, IServices services, string? viewId = null)
    {
        var layers = Drafts.Layers(m).ToDictionary(l => l.Name);
        var imports = Imports(m);
        var planes = new Dictionary<string, ViewPlanes?>();
        var views = new Dictionary<string, Entity?>();
        var batches = new Dictionary<(string View, int Piece, string? Source, string Layer), Batch>();
        foreach (var e in m.OfClass(IfcClass.Annotation))
        {
            if (!IsUnderlay(e)) continue;
            var vid = e.Str("viewId");
            if (vid == null || (viewId != null && vid != viewId)) continue;
            var layerName = e.Str("layer") ?? "0";
            if (layerName == Drafts.ConstructionLayer || e.Str("role") == "draft") continue;
            var layer = layers.GetValueOrDefault(layerName) ?? layers["0"];
            if (!layer.Visible) continue;
            string? source = null;
            var halftone = false;
            if (Links.Of(m, e) is { } link)
            {
                if (!Links.Visible(link) || !Links.In3d(link)) continue;
                source = link.Id;
                halftone = Links.IsHalftone(link);
            }
            else if (e.Str(ImportKey) is { } imp)
            {
                if (!Shown(imports[imp])) continue;
                source = imp;
            }
            if (!views.TryGetValue(vid, out var view)) views[vid] = view = Views.Find(m, vid);
            if (view == null || view.Str("kind") is "3d") continue;
            if (!planes.TryGetValue(vid, out var vp)) planes[vid] = vp = ViewPlanes.Of(m, services, view);
            if (vp == null) continue;
            var color = Rgb(halftone ? Links.Halftone : e.Str("color") ?? layer.Color);
            var shape = Drafts.ShapeOf(e);
            var hatch = e.Attrs["hatch"] as JsonObject;
            var loops = hatch?["loops"] is JsonArray la ? la.Select(Loop).Where(l => l.Count >= 3).ToList() : [];
            if (shape == null && loops.Count == 0) continue;
            var (lo, hi) = shape != null ? Geo.Bounds(shape) : (new Vec2(loops.Min(l => l.Min(p => p.X)), 0), new Vec2(loops.Max(l => l.Max(p => p.X)), 0));
            var piece = vp.At((lo.X + hi.X) / 2);
            var key = (vid, piece, source, layer.Name);
            if (!batches.TryGetValue(key, out var batch))
                batches[key] = batch = new Batch(vid, view.Str("storeyId"), source, layer.Name, vp[piece]);
            var plane = batch.Plane;
            if (shape != null)
                foreach (var run in Drafts.Flatten(shape))
                    for (var i = 0; i + 1 < run.Count; i++) batch.Segment(plane.World(run[i]), plane.World(run[i + 1]), color);
            if (loops.Count > 0) Fill(batch, loops, color);
        }
        return new JsonArray(batches.Values.Select(b => (JsonNode)b.ToJson()).ToArray());
    }

    static List<Vec2> Loop(JsonNode? n) =>
        n is JsonArray a ? a.Select(p => p is JsonArray { Count: >= 2 } q && Json.Number(q[0]) is { } x && Json.Number(q[1]) is { } y ? new Vec2(x, y) : (Vec2?)null).OfType<Vec2>().ToList() : [];

    static double Area(IReadOnlyList<Vec2> l)
    {
        var s = 0.0;
        for (var i = 0; i < l.Count; i++) { var (p, q) = (l[i], l[(i + 1) % l.Count]); s += p.X * q.Y - q.X * p.Y; }
        return s / 2;
    }

    static bool Inside(IReadOnlyList<Vec2> l, Vec2 p)
    {
        var inside = false;
        for (int i = 0, j = l.Count - 1; i < l.Count; j = i++)
            if ((l[i].Y > p.Y) != (l[j].Y > p.Y) && p.X < (l[j].X - l[i].X) * (p.Y - l[i].Y) / (l[j].Y - l[i].Y) + l[i].X) inside = !inside;
        return inside;
    }

    /// <summary>A hatch's loops as triangles on the plane: a loop inside an odd number of others is a hole of the smallest one round it.</summary>
    static void Fill(Batch batch, List<List<Vec2>> loops, (byte, byte, byte) color)
    {
        // Each loop counter-clockwise; nesting by a vertex's place in the others (a few loops a hatch, as a DXF has them).
        var ccw = loops.Select(l => Area(l) < 0 ? Enumerable.Reverse(l).ToList() : l).ToList();
        var depth = new int[ccw.Count];
        var parent = new int[ccw.Count];
        for (var i = 0; i < ccw.Count; i++)
        {
            parent[i] = -1;
            for (var j = 0; j < ccw.Count; j++)
            {
                if (i == j || !Inside(ccw[j], ccw[i][0])) continue;
                depth[i]++;
                if (parent[i] < 0 || Math.Abs(Area(ccw[j])) < Math.Abs(Area(ccw[parent[i]]))) parent[i] = j;
            }
        }
        for (var i = 0; i < ccw.Count; i++)
        {
            if (depth[i] % 2 == 1 || Math.Abs(Area(ccw[i])) < 1e-12) continue;
            var holes = Enumerable.Range(0, ccw.Count).Where(h => depth[h] % 2 == 1 && parent[h] == i).ToList();
            var pts = new List<Vec3>();
            int[] Ring(List<Vec2> l, bool hole)
            {
                var start = pts.Count;
                var ring = hole ? Enumerable.Reverse(l).ToList() : l;
                foreach (var p in ring) pts.Add(new Vec3(p.X, p.Y, 0));
                return Enumerable.Range(start, ring.Count).ToArray();
            }
            var outer = Ring(ccw[i], false);
            var holeRings = holes.Select(h => Ring(ccw[h], true)).ToArray();
            List<(int A, int B, int C)> tris;
            try { tris = Triangulator.Face(pts, outer, holeRings.Length > 0 ? holeRings : null, Vec3.UnitZ, 1e-12); }
            catch (Exception) { continue; } // a loop that crosses itself: no fill, its lines stay
            var plane = batch.Plane;
            Vec3 W(int k) => plane.World(new Vec2(pts[k].X, pts[k].Y));
            foreach (var (a, b, c) in tris) batch.Triangle(W(a), W(b), W(c), color);
        }
    }

    public static void Register(IModuleRegistry r)
    {
        // The DXF drawings shown in 3D, in batches (a view's only, with viewId).
        r.Query("underlays3d", (q, a) => Batches(q.Model, q.Services, (string?)a["viewId"]));

        // The DXF imports the project recorded (Properties of a shape from one shows its file and its 3D switch).
        r.Query("dxfImports", (q, _) => Imports(q.Model).DeepClone());

        // An import's or a link's drawing shown in 3D or not.
        r.Command("setUnderlay3d", "Show DXF in 3D", (ctx, a) =>
        {
            var id = (string?)a["id"] ?? throw new CommandException("Which import or link?");
            var shown = a["shown"] is JsonValue v && v.TryGetValue<bool>(out var b) ? b : throw new CommandException("Shown or not?");
            SetShown(ctx, id, shown);
            return null;
        });
    }
}
