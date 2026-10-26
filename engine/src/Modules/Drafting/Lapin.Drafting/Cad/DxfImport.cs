using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Drawing.Hatch;
using Lapin.Drawing.Import;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Drafting.Cad;

/// <summary>A plane transform: x' = A x + B y + Tx, y' = C x + D y + Ty.</summary>
public readonly record struct Affine(double A, double B, double C, double D, double Tx, double Ty)
{
    public static readonly Affine Identity = new(1, 0, 0, 1, 0, 0);
    public static Affine Translate(Vec2 t) => new(1, 0, 0, 1, t.X, t.Y);
    public static Affine Scale(double sx, double sy) => new(sx, 0, 0, sy, 0, 0);
    public static Affine Rotate(double deg) { var (s, c) = (Math.Sin(deg * Math.PI / 180), Math.Cos(deg * Math.PI / 180)); return new(c, -s, s, c, 0, 0); }

    public Vec2 Apply(Vec2 p) => new(A * p.X + B * p.Y + Tx, C * p.X + D * p.Y + Ty);
    public Vec2 Linear(Vec2 v) => new(A * v.X + B * v.Y, C * v.X + D * v.Y);
    /// <summary>This transform, then <paramref name="m"/>.</summary>
    public Affine Then(Affine m) => new(m.A * A + m.B * C, m.A * B + m.B * D, m.C * A + m.D * C, m.C * B + m.D * D, m.A * Tx + m.B * Ty + m.Tx, m.C * Tx + m.D * Ty + m.Ty);
    public double Det => A * D - B * C;
    public bool Flips => Det < 0;
    /// <summary>A similarity: circles stay circles (equal scale both ways, axes still square).</summary>
    public bool Uniform => Math.Abs(A * A + C * C - (B * B + D * D)) < 1e-9 * Math.Max(1, A * A + C * C) && Math.Abs(A * B + C * D) < 1e-9 * Math.Max(1, A * A + C * C);
    public double Factor => Math.Sqrt(Math.Abs(Det));
    /// <summary>Where the x axis points after it (°).</summary>
    public double AngleDeg => Math.Atan2(C, A) * 180 / Math.PI;
}

/// <summary>What an import asks for: the view, metres per drawing unit (null: the file's, or a guess), where the origin lands, the turn, whether to mirror (null: by the view), an extra factor, which layers, and what to bring.</summary>
public sealed record DxfImportOptions(string ViewId, double? UnitFactor = null, Vec2 At = default, double RotationDeg = 0, bool? Flip = null, double Scale = 1,
    HashSet<string>? Layers = null, string? LayerPrefix = null, bool Texts = true, bool Hatches = true, string Dimensions = "native");

/// <summary>
/// A DXF brought into a view as Lapin's own drafting: its layers as drafting
/// layers, lines, polylines, circles and arcs as drafted shapes (bulged
/// segments as arcs, ellipses and splines as polylines), hatches with the
/// pattern the file carries (registered in the project), texts in paper
/// height, linear and aligned dimensions as Lapin dimensions, blocks
/// exploded. In a plan the DXF's x and y are the view's. In a section or an
/// elevation the DXF's plane is the view's plane: x along it, y up; when the
/// view looks towards −X or −Y the drawing is mirrored about its y axis, so
/// that what the file drew from the left still reads from the left.
/// </summary>
public static class DxfImport
{
    const double TextWidthPerHeight = 0.6;

    static readonly Dictionary<string, string> KnownPatterns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SOLID"] = "SOLID", ["ANSI31"] = "LINES45", ["ANSI32"] = "STEEL", ["ANSI37"] = "CROSS", ["AR-CONC"] = "CONCRETE", ["AR-B816"] = "BRICK", ["AR-BRSTD"] = "BRICK", ["BRICK"] = "BRICK",
        ["INSUL"] = "BATT", ["EARTH"] = "EARTH", ["AR-SAND"] = "GRAVEL", ["GRAVEL"] = "GRAVEL", ["DOTS"] = "GRAVEL", ["AR-RROOF"] = "WOOD", ["WOOD"] = "WOOD", ["NET"] = "TILES", ["GRID"] = "TILES", ["LINE"] = "HORIZONTAL",
    };

    /// <summary>The file named by the arguments: by path, or its bytes base64 in <c>data</c> with its <c>name</c>.</summary>
    public static (DxfDocument Doc, string Name) Load(JsonObject a)
    {
        if ((string?)a["data"] is { Length: > 0 } data)
            return (DxfReader.Read(Convert.FromBase64String(data)), (string?)a["name"] ?? "drawing.dxf");
        var path = (string?)a["path"] ?? throw new CommandException("Which DXF file?");
        if (!File.Exists(path)) throw new CommandException($"There is no file at {path}.");
        return (DxfReader.Read(File.ReadAllBytes(path)), Path.GetFileName(path));
    }

    public static DxfImportOptions Options(JsonObject a)
    {
        var viewId = (string?)a["viewId"] ?? throw new CommandException("A DXF is imported into a view.");
        double? units = a["units"] switch
        {
            JsonValue v when v.TryGetValue<double>(out var n) && n > 0 => n,
            JsonValue v when v.TryGetValue<string>(out var s) && s != "auto" => DxfUnits.Factor(s) ?? throw new CommandException($"„{s}” is not a unit (mm, cm, m, in, ft)."),
            _ => null,
        };
        var at = a["at"] is JsonArray p && p.Count >= 2 ? new Vec2(Json.Number(p[0]) ?? 0, Json.Number(p[1]) ?? 0) : default;
        bool? flip = a["flip"] is JsonValue fv && fv.TryGetValue<bool>(out var fb) ? fb : null;
        var layers = a["layers"] is JsonArray la ? la.Select(x => (string?)x).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase) : null;
        static bool B(JsonNode? x, bool d) => x is JsonValue v && v.TryGetValue<bool>(out var b) ? b : d;
        var dims = (string?)a["dimensions"];
        if (dims is not ("native" or "geometry" or "skip")) dims = "native";
        var scale = Json.Number(a["scale"]) ?? 1;
        if (!(scale > 0)) throw new CommandException("The scale factor must be positive.");
        return new DxfImportOptions(viewId, units, at, Json.Number(a["rotation"]) ?? 0, flip, scale, layers, ((string?)a["layerPrefix"])?.Trim() is { Length: > 0 } pre ? pre : null, B(a["texts"], true), B(a["hatches"], true), dims);
    }

    /// <summary>Whether a view mirrors an imported drawing: a section or elevation looking towards −X or −Y does; a plan never.</summary>
    public static bool FlipFor(Entity view) => Views.Look(view) is { } look && (Math.Abs(look.X) >= Math.Abs(look.Y) ? look.X : look.Y) < 0;

    // ------------------------------------------------------------ walking the file

    /// <summary>An entity as the drawing shows it: in its frame, on its effective layer, with the colour it ends up with (null: its layer's).</summary>
    public readonly record struct Placed(DxfEntity Entity, Affine Frame, string Layer, string? Color);

    /// <summary>Model space with its blocks exploded: every entity with the transform that places it, and the layer and colour it takes.</summary>
    public static IEnumerable<Placed> Walk(DxfDocument doc, Affine root)
    {
        var stack = new Stack<string>();
        return Walk(doc, doc.Entities, root, null, null, stack);
    }

    static IEnumerable<Placed> Walk(DxfDocument doc, List<DxfEntity> entities, Affine frame, string? blockLayer, string? blockColor, Stack<string> stack)
    {
        foreach (var e in entities)
        {
            var layer = e.Layer == "0" && blockLayer != null ? blockLayer : e.Layer;
            var color = e.ByBlock ? blockColor : e.Color;
            if (e is DxfInsert ins)
            {
                foreach (var t in ins.Attribs) yield return new Placed(t, frame, t.Layer == "0" && blockLayer != null ? blockLayer : t.Layer, t.ByBlock ? color : t.Color);
                if (!doc.Blocks.TryGetValue(ins.Block, out var block) || stack.Contains(ins.Block) || stack.Count > 32) continue;
                stack.Push(ins.Block);
                var turn = Affine.Rotate(ins.RotationDeg);
                for (var r = 0; r < ins.Rows; r++)
                    for (var c = 0; c < ins.Cols; c++)
                    {
                        var at = ins.At + turn.Linear(new Vec2(c * ins.ColSpacing, r * ins.RowSpacing));
                        var local = Affine.Translate(-block.Base).Then(Affine.Scale(ins.Sx, ins.Sy)).Then(turn).Then(Affine.Translate(at)).Then(frame);
                        foreach (var p in Walk(doc, block.Entities, local, layer, color, stack)) yield return p;
                    }
                stack.Pop();
                continue;
            }
            yield return new Placed(e, frame, layer, color);
        }
    }

    /// <summary>The points an entity reaches (drawing frame), for extents.</summary>
    static IEnumerable<Vec2> Reach(DxfEntity e) => e switch
    {
        DxfLine l => [l.A, l.B],
        DxfPolyline p => DxfCurves.Polyline(p.Vertices, p.Closed),
        DxfCircle c => [c.C - new Vec2(c.R, c.R), c.C + new Vec2(c.R, c.R)],
        DxfArc a => DxfCurves.Arc(a.C, a.R, a.StartDeg, a.EndDeg),
        DxfEllipse el => DxfCurves.Ellipse(el.C, el.Major, el.Ratio, el.Start, el.End),
        DxfSpline s => DxfCurves.Spline(s.Degree, s.Knots, s.Controls, s.Weights, s.Fits),
        DxfText t => [t.At],
        DxfHatch h => h.Loops.SelectMany(l => l),
        DxfSolid s => s.Pts,
        DxfDimension d => [d.P13, d.P14, d.DefPoint],
        DxfLeader l => l.Pts,
        _ => [],
    };

    /// <summary>The drawing's extents (drawing units, blocks exploded); null when it has nothing to measure.</summary>
    public static (Vec2 Min, Vec2 Max)? Extents(DxfDocument doc)
    {
        double x0 = double.PositiveInfinity, y0 = double.PositiveInfinity, x1 = double.NegativeInfinity, y1 = double.NegativeInfinity;
        foreach (var p in Walk(doc, Affine.Identity))
            foreach (var q0 in Reach(p.Entity))
            {
                var q = p.Frame.Apply(q0);
                if (!double.IsFinite(q.X) || !double.IsFinite(q.Y)) continue;
                x0 = Math.Min(x0, q.X); y0 = Math.Min(y0, q.Y); x1 = Math.Max(x1, q.X); y1 = Math.Max(y1, q.Y);
            }
        return x0 <= x1 ? (new Vec2(x0, y0), new Vec2(x1, y1)) : null;
    }

    /// <summary>
    /// Metres per drawing unit: the file's, else a guess from its extents. A file
    /// that says millimetres but is a few dozen units across (ArchiCAD writes
    /// metres under $INSUNITS = 4) is read by the guess too.
    /// </summary>
    public static (double Factor, bool Guessed) Units(DxfDocument doc)
    {
        var ext = Extents(doc);
        var size = ext is { } e ? Math.Max(e.Max.X - e.Min.X, e.Max.Y - e.Min.Y) : 0;
        var guess = DxfUnits.Guess(size);
        if (doc.UnitFactor is not { } f) return (guess, true);
        if (size <= 0 || size * f >= 1 || guess == f || size * guess < 1) return (f, false);
        return (guess, true);
    }

    // ------------------------------------------------------------ the summary (before importing)

    public static JsonObject Info(IModelView m, DxfDocument doc, string? viewId)
    {
        var (factor, guessed) = Units(doc);
        var counts = new Dictionary<string, int>();
        var other = new Dictionary<string, int>();
        var perLayer = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in Walk(doc, Affine.Identity))
        {
            if (p.Entity is DxfOther o) { other[o.Type] = other.GetValueOrDefault(o.Type) + 1; continue; }
            var kind = p.Entity switch
            {
                DxfLine => "lines", DxfPolyline => "polylines", DxfCircle => "circles", DxfArc => "arcs", DxfEllipse => "ellipses", DxfSpline => "splines",
                DxfText => "texts", DxfHatch => "hatches", DxfSolid => "solids", DxfDimension => "dimensions", DxfLeader => "leaders", _ => "other",
            };
            counts[kind] = counts.GetValueOrDefault(kind) + 1;
            perLayer[p.Layer] = perLayer.GetValueOrDefault(p.Layer) + 1;
        }
        var layers = new JsonArray();
        foreach (var name in perLayer.Keys.Concat(doc.Layers.Keys).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
        {
            var l = doc.Layers.GetValueOrDefault(name);
            layers.Add((JsonNode)new JsonObject { ["name"] = name, ["color"] = l?.Color ?? "#18181b", ["visible"] = l?.Visible ?? true, ["locked"] = l?.Locked ?? false, ["count"] = perLayer.GetValueOrDefault(name), ["lineType"] = DxfCurves.LineType(l?.LineType, l != null ? doc.LineTypes.GetValueOrDefault(l.LineType) : null) });
        }
        var ext = Extents(doc);
        var view = viewId != null ? Views.Find(m, viewId) : null;
        return new JsonObject
        {
            ["version"] = doc.Version,
            ["units"] = new JsonObject { ["factor"] = factor, ["name"] = DxfUnits.Name(factor), ["guessed"] = guessed },
            ["extents"] = ext is { } x ? new JsonObject { ["min"] = new JsonArray(x.Min.X, x.Min.Y), ["max"] = new JsonArray(x.Max.X, x.Max.Y) } : null,
            ["counts"] = new JsonObject(counts.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)kv.Value))),
            ["unsupported"] = new JsonObject(other.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)kv.Value))),
            ["layers"] = layers,
            ["blocks"] = doc.Blocks.Count,
            ["view"] = view == null ? null : new JsonObject { ["kind"] = view.Str("kind") ?? "plan", ["scale"] = view.NumOrNull("scale") ?? 100, ["flip"] = FlipFor(view) },
        };
    }

    // ------------------------------------------------------------ the import

    sealed class Namer(IModelView m, string viewId)
    {
        readonly HashSet<string> _taken = Drafts.On(m, viewId).Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, int> _next = [];

        public string Next(string word)
        {
            if (!_next.TryGetValue(word, out var n))
                n = 1 + _taken.Select(x => x.StartsWith(word + " ", StringComparison.OrdinalIgnoreCase) && int.TryParse(x[(word.Length + 1)..], out var k) ? k : 0).DefaultIfEmpty(0).Max();
            while (_taken.Contains($"{word} {n}")) n++;
            _next[word] = n + 1;
            var name = $"{word} {n}";
            _taken.Add(name);
            return name;
        }
    }

    sealed class Run(ICommandContext ctx, Entity view, DxfDocument doc, DxfImportOptions o, double factor)
    {
        public readonly Dictionary<string, CadLayer> NewLayers = new(StringComparer.OrdinalIgnoreCase);
        public readonly List<HatchPattern> NewPatterns = [];
        public readonly Dictionary<string, int> Skipped = [];
        public int Shapes, Hatches, Texts, Dimensions;
        readonly Namer _names = new(ctx.Model, view.Id);
        readonly Dictionary<string, CadLayer> _layers = Drafts.Layers(ctx.Model).ToDictionary(l => l.Name, StringComparer.OrdinalIgnoreCase);
        readonly List<HatchPattern> _patterns = [.. DraftingResources.Patterns(ctx.Model)];
        /// <summary>Metres of model per paper millimetre in this view.</summary>
        readonly double _mm = (view.NumOrNull("scale") ?? 100) / 1000;

        public void Skip(string what) => Skipped[what] = Skipped.GetValueOrDefault(what) + 1;

        /// <summary>The drafting layer a DXF layer becomes (made from the table's entry, once); an existing Lapin layer of that name is kept as it is.</summary>
        public string Layer(string dxfLayer)
        {
            var name = (o.LayerPrefix ?? "") + dxfLayer;
            if (_layers.ContainsKey(name)) return _layers[name].Name;
            var l = doc.Layers.GetValueOrDefault(dxfLayer);
            var made = l == null ? new CadLayer(name)
                : new CadLayer(name, l.Color, Math.Clamp(l.Weight, 0.05, 2), DxfCurves.LineType(l.LineType, doc.LineTypes.GetValueOrDefault(l.LineType)), l.Visible, l.Locked);
            _layers[name] = made;
            NewLayers[name] = made;
            return name;
        }

        public void Add(Shape shape, string layer, string? color)
        {
            var attrs = new JsonObject { ["viewId"] = view.Id, ["layer"] = layer, ["shape"] = shape.ToJson() };
            if (color != null) attrs["color"] = color;
            var word = shape switch { Line => "Line", Polyline { Closed: true } => "Polygon", Polyline => "Polyline", Circle => "Circle", Arc => "Arc", _ => "Shape" };
            ctx.Add(new Entity(ctx.NewId(), IfcClass.Annotation, _names.Next(word), attrs, Drafts.Predefined));
            Shapes++;
        }

        public void AddHatch(IReadOnlyList<IReadOnlyList<Vec2>> loops, string pattern, double scale, double angle, string layer, string? color)
        {
            var hatch = new JsonObject
            {
                ["pattern"] = pattern, ["scale"] = Math.Round(scale, 6), ["angle"] = Math.Round(angle, 4),
                ["loops"] = new JsonArray(loops.Select(l => (JsonNode)new JsonArray(l.Select(q => (JsonNode)new JsonArray(Math.Round(q.X, 9), Math.Round(q.Y, 9))).ToArray())).ToArray()),
            };
            var attrs = new JsonObject { ["viewId"] = view.Id, ["layer"] = layer, ["hatch"] = hatch };
            if (color != null) attrs["color"] = color;
            ctx.Add(new Entity(ctx.NewId(), IfcClass.Annotation, $"Hatch {pattern}", attrs, Drafts.Predefined));
            Hatches++;
        }

        public void AddText(Vec2 at, string text, double heightMm, double angleDeg, string layer, string? color)
        {
            var attrs = new JsonObject { ["viewId"] = view.Id, ["at"] = new JsonArray(Math.Round(at.X, 9), Math.Round(at.Y, 9)), ["text"] = text, ["angle"] = Math.Round(angleDeg, 4), ["layer"] = layer, ["overrides"] = new JsonObject { ["height"] = Math.Round(heightMm, 3) } };
            if (color != null) attrs["overrides"]!["color"] = color;
            ctx.Add(new Entity(ctx.NewId(), IfcClass.Annotation, text.Length > 24 ? text[..24] + "…" : text, attrs, "TEXT"));
            Texts++;
        }

        public void AddDimension(Vec2 a, Vec2 b, Vec2 at, Vec2? dir, string layer)
        {
            var attrs = new JsonObject { ["viewId"] = view.Id, ["a"] = new JsonArray(a.X, a.Y), ["b"] = new JsonArray(b.X, b.Y), ["at"] = new JsonArray(at.X, at.Y), ["layer"] = layer };
            if (dir is { } d) attrs["dir"] = new JsonArray(d.X, d.Y);
            ctx.Add(new Entity(ctx.NewId(), IfcClass.Annotation, "Dimension", attrs, "DIMENSION"));
            Dimensions++;
        }

        /// <summary>The hatch pattern a DXF hatch draws with, and Lapin's scale and angle for it.</summary>
        public (string Name, double Scale, double Angle) Pattern(DxfHatch h, Affine f)
        {
            if (h.Solid || h.Pattern.Equals("SOLID", StringComparison.OrdinalIgnoreCase)) return ("SOLID", 1, 0);
            var turn = f.AngleDeg;
            if (h.Definition.Count > 0)
            {
                // The file's lines, normalised back to the pattern's own definition (scale 1, angle 0, offsets along and across each line),
                // mirrored when the view mirrors; registered once in the project, under the file's name.
                var families = h.Definition.Select(l =>
                {
                    var a = l.AngleDeg - h.AngleDeg;
                    var (s, c) = (Math.Sin(l.AngleDeg * Math.PI / 180), Math.Cos(l.AngleDeg * Math.PI / 180));
                    var (dx, dy) = ((l.Offset.X * c + l.Offset.Y * s) / h.Scale, (-l.Offset.X * s + l.Offset.Y * c) / h.Scale);
                    var (bs, bc) = (Math.Sin(-h.AngleDeg * Math.PI / 180), Math.Cos(-h.AngleDeg * Math.PI / 180));
                    var b = new Vec2((l.Base.X * bc - l.Base.Y * bs) / h.Scale, (l.Base.X * bs + l.Base.Y * bc) / h.Scale);
                    var dashes = l.Dashes.Select(d => R(d / h.Scale)).ToList();
                    return f.Flips ? new HatchLineFamily(R(-a), new Vec2(R(-b.X), R(b.Y)), R(-dx), R(dy), dashes) : new HatchLineFamily(R(a), new Vec2(R(b.X), R(b.Y)), R(dx), R(dy), dashes);
                }).ToList();
                var name = Register(h.Pattern, families);
                return (name, h.Scale * factor * o.Scale / _mm, f.Flips ? turn - h.AngleDeg : turn + h.AngleDeg);
            }
            var known = KnownPatterns.GetValueOrDefault(h.Pattern) ?? HatchLibrary.Find(h.Pattern, _patterns)?.Name ?? "LINES45";
            return (known, 1, f.Flips ? turn - h.AngleDeg : turn + h.AngleDeg);
        }

        static double R(double v) => Math.Round(v, 6);

        string Register(string dxfName, List<HatchLineFamily> families)
        {
            var stem = new string(dxfName.Trim().ToUpperInvariant().Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '_').ToArray()) is { Length: > 0 } s ? s : "DXF";
            for (var n = 1; ; n++)
            {
                var name = n == 1 ? stem : $"{stem}-{n}";
                var existing = HatchLibrary.Find(name, _patterns);
                if (existing == null)
                {
                    var made = new HatchPattern(name, $"From DXF ({dxfName})", families);
                    _patterns.Add(made);
                    NewPatterns.Add(made);
                    return name;
                }
                if (existing.Families.SequenceEqual(families)) return existing.Name;
            }
        }

        public void Finish()
        {
            if (NewLayers.Count > 0) Drafts.WriteLayers(ctx, _layers.Values);
            if (NewPatterns.Count > 0)
            {
                var project = Spatial.Project(ctx.Model) ?? throw new CommandException("The model has no project.");
                var attrs = project.CloneAttrs();
                attrs["hatchPatterns"] = Pat.Serialize(_patterns);
                ctx.Replace(project.With(attrs: attrs));
            }
        }
    }

    public static JsonObject Import(ICommandContext ctx, DxfDocument doc, DxfImportOptions o, string fileName)
    {
        var view = Views.Find(ctx.Model, o.ViewId) ?? throw new CommandException("There is no such view.");
        if (view.Str("kind") == "3d") throw new CommandException("A DXF is imported into a plan, a section or an elevation.");
        var (factor, guessed) = o.UnitFactor is { } uf ? (uf, false) : Units(doc);
        var flip = o.Flip ?? FlipFor(view);
        var root = Affine.Scale(flip ? -factor * o.Scale : factor * o.Scale, factor * o.Scale).Then(Affine.Rotate(o.RotationDeg)).Then(Affine.Translate(o.At));
        var run = new Run(ctx, view, doc, o, factor);
        var scaleMm = (view.NumOrNull("scale") ?? 100) / 1000; // metres per paper mm
        var total = 0;
        foreach (var p in Walk(doc, root))
        {
            if (o.Layers != null && !o.Layers.Contains(p.Entity.Layer) && !o.Layers.Contains(p.Layer)) continue;
            if (p.Entity is DxfOther other) { run.Skip(other.Type); continue; }
            total++;
            var layer = run.Layer(p.Layer);
            var f = p.Frame;
            switch (p.Entity)
            {
                case DxfLine l:
                    if ((l.B - l.A).Length > 1e-12) run.Add(Map(f, new Line(l.A, l.B)), layer, p.Color);
                    else run.Skip("zero-length line");
                    break;
                case DxfPolyline pl:
                    foreach (var s in PolylineShapes(pl)) run.Add(Map(f, s), layer, p.Color);
                    break;
                case DxfCircle c:
                    run.Add(Map(f, new Circle(c.C, c.R)), layer, p.Color);
                    break;
                case DxfArc a:
                    run.Add(Map(f, new Arc(a.C, a.R, a.StartDeg * Math.PI / 180, a.EndDeg * Math.PI / 180)), layer, p.Color);
                    break;
                case DxfEllipse el:
                {
                    var pts = DxfCurves.Ellipse(el.C, el.Major, el.Ratio, el.Start, el.End);
                    var closed = pts.Count > 2 && (pts[0] - pts[^1]).Length < 1e-9;
                    if (closed) pts.RemoveAt(pts.Count - 1);
                    run.Add(Map(f, new Polyline(pts, closed)), layer, p.Color);
                    break;
                }
                case DxfSpline sp:
                {
                    var pts = DxfCurves.Spline(sp.Degree, sp.Knots, sp.Controls, sp.Weights, sp.Fits);
                    if (pts.Count < 2) { run.Skip("empty spline"); break; }
                    var closed = sp.Closed || (pts.Count > 3 && (pts[0] - pts[^1]).Length < 1e-9);
                    if (closed && (pts[0] - pts[^1]).Length < 1e-9) pts.RemoveAt(pts.Count - 1);
                    run.Add(Map(f, new Polyline(pts, closed)), layer, p.Color);
                    break;
                }
                case DxfText t:
                    if (!o.Texts) { run.Skip("text"); break; }
                    Text(run, t, f, layer, p.Color, scaleMm);
                    break;
                case DxfHatch h:
                {
                    if (!o.Hatches) { run.Skip("hatch"); break; }
                    var loops = h.Loops.Select(l => (IReadOnlyList<Vec2>)l.Select(f.Apply).ToList()).Where(l => l.Count >= 3).ToList();
                    if (loops.Count == 0) { run.Skip("empty hatch"); break; }
                    var (name, scale, angle) = run.Pattern(h, f);
                    run.AddHatch(loops, name, scale, angle, layer, p.Color);
                    break;
                }
                case DxfSolid so:
                    if (!o.Hatches) { run.Skip("solid"); break; }
                    run.AddHatch([so.Pts.Select(f.Apply).ToList()], "SOLID", 1, 0, layer, p.Color);
                    break;
                case DxfLeader ld:
                    run.Add(Map(f, new Polyline(ld.Pts, false)), layer, p.Color);
                    break;
                case DxfDimension d:
                {
                    if (o.Dimensions == "skip") { run.Skip("dimension"); break; }
                    if (o.Dimensions == "native" && d.Kind is 0 or 1 && (d.P14 - d.P13).Length > 1e-9)
                    {
                        Vec2? dir = d.Kind == 0 ? f.Linear(new Vec2(Math.Cos(d.RotationDeg * Math.PI / 180), Math.Sin(d.RotationDeg * Math.PI / 180))).Normalized() : null;
                        run.AddDimension(f.Apply(d.P13), f.Apply(d.P14), f.Apply(d.DefPoint), dir, layer);
                        break;
                    }
                    // The other kinds (and all of them when asked for geometry): the lines and texts the file rendered them with.
                    if (d.Block != null && doc.Blocks.TryGetValue(d.Block, out var block))
                    {
                        foreach (var q in Walk(doc, block.Entities, f, p.Layer, p.Color, new Stack<string>()))
                        {
                            var ql = run.Layer(q.Layer);
                            switch (q.Entity)
                            {
                                case DxfLine l when (l.B - l.A).Length > 1e-12: run.Add(Map(q.Frame, new Line(l.A, l.B)), ql, q.Color); break;
                                case DxfPolyline pl: foreach (var s in PolylineShapes(pl)) run.Add(Map(q.Frame, s), ql, q.Color); break;
                                case DxfArc a: run.Add(Map(q.Frame, new Arc(a.C, a.R, a.StartDeg * Math.PI / 180, a.EndDeg * Math.PI / 180)), ql, q.Color); break;
                                case DxfCircle c: run.Add(Map(q.Frame, new Circle(c.C, c.R)), ql, q.Color); break;
                                case DxfSolid so: run.AddHatch([so.Pts.Select(q.Frame.Apply).ToList()], "SOLID", 1, 0, ql, q.Color); break;
                                case DxfText t when o.Texts: Text(run, t, q.Frame, ql, q.Color, scaleMm); break;
                            }
                        }
                        run.Dimensions++;
                    }
                    else run.Skip($"dimension (kind {d.Kind})");
                    break;
                }
                default:
                    run.Skip(p.Entity.GetType().Name);
                    break;
            }
        }
        run.Finish();
        return new JsonObject
        {
            ["file"] = fileName,
            ["entities"] = total,
            ["shapes"] = run.Shapes, ["hatches"] = run.Hatches, ["texts"] = run.Texts, ["dimensions"] = run.Dimensions,
            ["layers"] = new JsonArray(run.NewLayers.Keys.Select(x => (JsonNode)x).ToArray()),
            ["patterns"] = new JsonArray(run.NewPatterns.Select(x => (JsonNode)x.Name).ToArray()),
            ["skipped"] = new JsonObject(run.Skipped.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)kv.Value))),
            ["units"] = DxfUnits.Name(factor), ["unitsGuessed"] = guessed, ["flipped"] = flip,
        };
    }

    /// <summary>A text placed: its box's centre goes through the frame, its height scales, and it stays readable (never upside down).</summary>
    static void Text(Run run, DxfText t, Affine f, string layer, string? color, double scaleMm)
    {
        var u = new Vec2(Math.Cos(t.RotationDeg * Math.PI / 180), Math.Sin(t.RotationDeg * Math.PI / 180));
        var n = u.Perp;
        var w = Math.Max(1, t.Value.Length) * t.Height * TextWidthPerHeight;
        var start = t.At - u * (t.HAlign == 1 ? w / 2 : t.HAlign == 2 ? w : 0) - n * (t.VAlign == 2 ? t.Height / 2 : t.VAlign == 3 ? t.Height : 0);
        var centre = f.Apply(start + u * (w / 2) + n * (t.Height / 2));
        var u2 = f.Linear(u);
        var angle = Math.Atan2(u2.Y, u2.X) * 180 / Math.PI;
        if (angle > 90 + 1e-9 || angle <= -90 + 1e-9) angle += angle > 0 ? -180 : 180;
        var h = t.Height * f.Factor;
        var dir = new Vec2(Math.Cos(angle * Math.PI / 180), Math.Sin(angle * Math.PI / 180));
        var at = centre - dir * (Math.Max(1, t.Value.Length) * h * TextWidthPerHeight / 2) - dir.Perp * (h / 2);
        run.AddText(at, t.Value, h / scaleMm, angle, layer, color);
    }

    /// <summary>A DXF polyline as Lapin shapes: one polyline when every segment is straight; else straight runs as polylines (or lines) and each bulged segment as an arc.</summary>
    public static List<Shape> PolylineShapes(DxfPolyline p)
    {
        var verts = p.Vertices;
        var n = verts.Count;
        var segs = p.Closed ? n : n - 1;
        if (verts.All(v => Math.Abs(v.Bulge) < 1e-12))
        {
            var pts = Dedupe(verts.Select(v => v.P).ToList(), p.Closed);
            return pts.Count < 2 ? [] : pts.Count == 2 && !p.Closed ? [new Line(pts[0], pts[1])] : [new Polyline(pts, p.Closed && pts.Count >= 3)];
        }
        var o = new List<Shape>();
        var run = new List<Vec2> { verts[0].P };
        void Flush()
        {
            var pts = Dedupe(run, false);
            if (pts.Count == 2) o.Add(new Line(pts[0], pts[1]));
            else if (pts.Count > 2) o.Add(new Polyline(pts, false));
            run = [];
        }
        for (var i = 0; i < segs; i++)
        {
            var (a, b) = (verts[i], verts[(i + 1) % n]);
            if ((b.P - a.P).Length < 1e-12) continue;
            if (Math.Abs(a.Bulge) < 1e-12) { if (run.Count == 0) run.Add(a.P); run.Add(b.P); continue; }
            Flush();
            var arc = DxfCurves.BulgeArc(a.P, b.P, a.Bulge);
            o.Add(new Arc(arc.C, arc.R, arc.StartDeg * Math.PI / 180, arc.EndDeg * Math.PI / 180));
            run = [b.P];
        }
        Flush();
        return o;
    }

    static List<Vec2> Dedupe(List<Vec2> pts, bool closed)
    {
        var o = new List<Vec2>();
        foreach (var p in pts) if (o.Count == 0 || (o[^1] - p).Length > 1e-12) o.Add(p);
        if (closed && o.Count > 2 && (o[0] - o[^1]).Length <= 1e-12) o.RemoveAt(o.Count - 1);
        return o;
    }

    /// <summary>A shape through a transform: circles and arcs stay round under a similarity (an arc's ends swap when it mirrors); otherwise they become polylines.</summary>
    public static Shape Map(Affine f, Shape s)
    {
        switch (s)
        {
            case Circle c when f.Uniform: return new Circle(f.Apply(c.C), c.R * f.Factor);
            case Arc a when f.Uniform:
            {
                var c = f.Apply(a.C);
                var (p0, p1) = (f.Apply(a.At(a.Start)), f.Apply(a.At(a.End)));
                var (s0, s1) = (Math.Atan2(p0.Y - c.Y, p0.X - c.X), Math.Atan2(p1.Y - c.Y, p1.X - c.X));
                return f.Flips ? new Arc(c, a.R * f.Factor, Geo.NormAngle(s1), Geo.NormAngle(s0)) : new Arc(c, a.R * f.Factor, Geo.NormAngle(s0), Geo.NormAngle(s1));
            }
            case Circle or Arc:
            {
                var pts = Geo.Polylines(s)[0].Select(f.Apply).ToList();
                var closed = s is Circle;
                if (closed) pts.RemoveAt(pts.Count - 1);
                return new Polyline(pts, closed);
            }
            default: return Geo.Map(s, f.Apply);
        }
    }

    public static void Register(IModuleRegistry r)
    {
        // What a DXF holds, before importing it: its units (or a guess), extents, layers with their counts, what Lapin leaves out, and whether this view mirrors it.
        r.Query("dxfInfo", (q, a) =>
        {
            var (doc, _) = Load(a);
            return Info(q.Model, doc, (string?)a["viewId"]);
        });

        // The DXF into a view, as drafting: one undo step.
        r.Command("importDxf", "Import DXF", (ctx, a) =>
        {
            var (doc, name) = Load(a);
            return Import(ctx, doc, Options(a), name);
        });
    }
}
