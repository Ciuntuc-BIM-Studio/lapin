using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Graphs;

/// <summary>
/// A port of a node. <see cref="Type"/>: number, integer, text, bool, point, geometry, element, storey, list, any (the
/// interface colours it, and refuses a wire between kinds that do not meet). <see cref="Whole"/>: it takes a list as it
/// is, never mapped over. <see cref="Setting"/>: set in the inspector only, no socket (an input node's value, a unit).
/// </summary>
public sealed record Port(string Name, string Label, string Type = "any", bool Whole = false, bool Setting = false, JsonNode? Default = null, string[]? Choices = null);

/// <summary>What a node's run is given: its inputs (wired, else its constants, else the ports' defaults), its node, the model.</summary>
public sealed class NodeCall(GraphNode node, IReadOnlyDictionary<string, object?> inputs, IModelView model, string? storey = null, IQueryContext? query = null)
{
    /// <summary>The model's geometry and the product's services, over the same model (the preview's copy, the bake's edit).</summary>
    public IQueryContext? Query => query;
    /// <summary>The view's storey, what a storey left unchosen means (<see cref="Evaluator.Storey"/>).</summary>
    public string? Storey => storey;
    public GraphNode Node => node;
    public IModelView Model => model;
    public object? this[string port] => inputs.GetValueOrDefault(port);
    public double Num(string port, double fallback) => Values.Number(this[port], port, fallback);
    public double Num(string port) => Values.Number(this[port], port);
    public int Int(string port, int fallback) => Values.Integer(this[port], port, fallback);
    public Vec3 Pt(string port) => Values.Point(this[port], port);
    public string Text(string port) => Values.Text(this[port]);
}

/// <summary>A node of the module's own: its kind, its name and group as the interface shows them, its ports, what it does.</summary>
public sealed record NodeDef(string Kind, string Label, string Group, Port[] Inputs, Port[] Outputs, Func<NodeCall, object?[]> Run, string? About = null)
{
    public JsonObject ToJson() => new()
    {
        ["kind"] = Kind, ["label"] = Label, ["group"] = Group,
        ["inputs"] = new JsonArray(Inputs.Select(PortJson).ToArray()),
        ["outputs"] = new JsonArray(Outputs.Select(PortJson).ToArray()),
        ["about"] = About,
    };

    static JsonNode PortJson(Port p)
    {
        var o = new JsonObject { ["name"] = p.Name, ["label"] = p.Label, ["type"] = p.Type };
        if (p.Whole) o["whole"] = true;
        if (p.Setting) o["setting"] = true;
        if (p.Default != null) o["default"] = p.Default.DeepClone();
        if (p.Choices != null) o["choices"] = new JsonArray(p.Choices.Select(c => (JsonNode)c).ToArray());
        return o;
    }
}

/// <summary>
/// The module's own nodes (docs/plan-visual-graphs.md §4): inputs, lists, maths, points and simple geometry, text, the
/// model, filters and a watch. None changes the model; those that read it say so in their group ("Model").
/// </summary>
public static class Builtins
{
    static Port In(string name, string label, string type = "any", JsonNode? def = null) => new(name, label, type, Default: def);
    static Port Whole(string name, string label, string type = "list") => new(name, label, type, Whole: true);
    static Port Set(string name, string label, string type, JsonNode? def = null, string[]? choices = null) => new(name, label, type, Setting: true, Default: def, Choices: choices);
    static Port Out(string name, string label, string type = "any") => new(name, label, type);

    static object?[] One(object? v) => [v];

    /// <summary>The classes of types a type node offers.</summary>
    static readonly string[] TypeClasses =
        [IfcClass.WallType, IfcClass.SlabType, IfcClass.ColumnType, IfcClass.BeamType, IfcClass.FootingType, IfcClass.DoorType, IfcClass.WindowType, IfcClass.StairType, IfcClass.RampType];

    public static readonly IReadOnlyList<NodeDef> All =
    [
        // ── inputs (stage 2: each a field of the graph's palette) ──
        new("number", "Number", "Inputs", [Set("value", "Value", "number", 0), Set("unit", "Unit", "text", "none", ["none", "length", "angle"])],
            [Out("value", "Value", "number")], c => One(c.Num("value", 0)), "A number; lengths in metres, angles in degrees."),
        new("integer", "Whole number", "Inputs", [Set("value", "Value", "integer", 1)], [Out("value", "Value", "integer")], c => One((double)c.Int("value", 0))),
        new("text", "Text", "Inputs", [Set("value", "Value", "text", "")], [Out("value", "Text", "text")], c => One(c.Text("value"))),
        new("bool", "Yes or no", "Inputs", [Set("value", "Value", "bool", false)], [Out("value", "Value", "bool")], c => One(Values.Bool(c["value"]))),
        new("choice", "Choice", "Inputs", [Set("options", "Options", "text", "A, B"), Set("value", "Value", "text", "A")], [Out("value", "Chosen", "text")], c =>
        {
            var options = c.Text("options").Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
            var v = c.Text("value");
            return One(options.Contains(v) ? v : options.FirstOrDefault() ?? "");
        }),
        new("pick", "Elements picked", "Inputs", [Set("ids", "Elements", "element", new JsonArray())], [Out("ids", "Elements", "element")], c =>
            One(Values.List(c["ids"]).Select(Values.Text).Where(id => c.Model.Get(id) != null).Cast<object?>().ToList()),
            "The elements picked in the model when the node was set; those deleted since are left out."),
        new("storey", "Storey", "Inputs", [Set("value", "Storey", "storey", "")], [Out("value", "Storey", "storey")], c =>
        {
            var id = c.Text("value");
            if (c.Model.Get(id) is { Class: IfcClass.BuildingStorey }) return One(id);
            if (c.Storey != null && c.Model.Get(c.Storey) is { Class: IfcClass.BuildingStorey }) return One(c.Storey);
            return One(Spatial.Storeys(c.Model).FirstOrDefault()?.Id ?? throw new CommandException("The project has no storey."));
        }, "A storey of the project; none chosen: the view's."),
        new("type", "A type", "Inputs", [Set("class", "Class", "text", IfcClass.WallType, TypeClasses), Set("value", "Type", "type", "")], [Out("value", "Type", "element")], c =>
        {
            var cls = c.Text("class");
            var id = c.Text("value");
            if (c.Model.Get(id) is { } e && e.Class == cls) return One(id);
            var first = c.Model.OfClass(cls).OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).FirstOrDefault();
            return One(first?.Id ?? throw new CommandException($"The project has no type of the class {cls}."));
        }, "A type of the project (a wall type, a slab type…) for a tool's Type; none chosen: the first of its class by name."),

        // ── lists ──
        new("range", "Range", "Lists", [In("start", "Start", "number", 0), In("end", "End", "number", 10), In("step", "Step", "number", 1)], [Out("list", "Numbers", "number")], c =>
        {
            double a = c.Num("start", 0), b = c.Num("end", 10), s = Math.Abs(c.Num("step", 1));
            if (s < 1e-9) throw new CommandException("A range's step cannot be nothing.");
            var n = (int)Math.Floor(Math.Abs(b - a) / s + 1e-9) + 1;
            Limit(n);
            var dir = b >= a ? 1 : -1;
            return One(Enumerable.Range(0, n).Select(i => (object?)(a + dir * i * s)).ToList());
        }, "From start to end (both in), a step apart."),
        new("series", "Series", "Lists", [In("start", "Start", "number", 0), In("step", "Step", "number", 1), In("count", "Count", "integer", 5)], [Out("list", "Numbers", "number")], c =>
        {
            var n = c.Int("count", 5);
            if (n < 0) throw new CommandException("A series cannot have fewer than none.");
            Limit(n);
            double a = c.Num("start", 0), s = c.Num("step", 1);
            return One(Enumerable.Range(0, n).Select(i => (object?)(a + i * s)).ToList());
        }),
        new("count", "Count", "Lists", [Whole("list", "List")], [Out("count", "Count", "integer")], c => One((double)Values.List(c["list"]).Count)),
        new("item", "Item", "Lists", [Whole("list", "List"), In("index", "Index", "integer", 0)], [Out("item", "Item")], c =>
        {
            var l = Values.List(c["list"]);
            if (l.Count == 0) throw new CommandException("The list is empty.");
            var i = c.Int("index", 0);
            if (i < 0) i += l.Count;
            return i >= 0 && i < l.Count ? One(l[i]) : throw new CommandException($"The list has {l.Count} items: no item {c.Int("index", 0)}.");
        }, "An item by its place, from 0; a negative index counts from the end."),
        new("reverse", "Reverse", "Lists", [Whole("list", "List")], [Out("list", "List", "list")], c => One(Enumerable.Reverse(Values.List(c["list"])).ToList())),
        new("flatten", "Flatten", "Lists", [Whole("list", "List")], [Out("list", "List", "list")], c => One(Flat(Values.List(c["list"])).ToList())),
        new("sum", "Sum", "Lists", [Whole("list", "Numbers", "number")], [Out("sum", "Sum", "number")], c => One(Flat(Values.List(c["list"])).Sum(x => Values.Number(x, "Numbers")))),
        new("minmax", "Smallest and largest", "Lists", [Whole("list", "Numbers", "number")], [Out("min", "Smallest", "number"), Out("max", "Largest", "number")], c =>
        {
            var l = Flat(Values.List(c["list"])).Select(x => Values.Number(x, "Numbers")).ToList();
            if (l.Count == 0) throw new CommandException("The list is empty.");
            return [l.Min(), l.Max()];
        }),
        new("filter", "Filter", "Lists", [Whole("list", "List"), Whole("keep", "Keep", "bool")], [Out("list", "Kept", "list"), Out("rest", "Left out", "list")], c =>
        {
            var l = Values.List(c["list"]);
            var keep = Values.List(c["keep"]);
            if (keep.Count == 0) return [l, new List<object?>()];
            var kept = new List<object?>();
            var rest = new List<object?>();
            for (var i = 0; i < l.Count; i++) (Values.Bool(keep[Math.Min(i, keep.Count - 1)]) ? kept : rest).Add(l[i]);
            return [kept, rest];
        }, "The items whose Keep is yes; Keep shorter than the list repeats its last."),
        new("list", "List", "Lists", [Set("items", "Items", "text", "1; 2; 3"), Set("kind", "Kind", "text", "Auto", ["Auto", "Numbers", "Texts"])], [Out("list", "List", "list")], c =>
            One(Lists.Typed(c.Text("items"), c.Text("kind"))),
            "A list typed by hand, its items between semicolons or on lines of their own (a decimal comma is fine); Auto: numbers when every item is one, else texts."),
        new("joinLists", "Join lists", "Lists", [Whole("a", "A"), Whole("b", "B"), Whole("c", "C"), Whole("d", "D")], [Out("list", "List", "list")], c =>
        {
            var all = new List<object?>();
            foreach (var p in (string[])["a", "b", "c", "d"]) all.AddRange(Values.List(c[p]));
            Limit(all.Count);
            return One(all);
        }, "The lists one after the other; a single value counts as a list of one (to gather values into a list), an input left empty is left out."),
        new("sort", "Sort", "Lists", [Whole("list", "List"), Whole("by", "By"), Set("descending", "Descending", "bool", false)], [Out("list", "Sorted", "list"), Out("order", "Order", "integer")], c =>
        {
            var (sorted, order) = Lists.Sort(Values.List(c["list"]), c["by"] is { } by ? Values.List(by) : null, Values.Bool(c["descending"]));
            return [sorted, order];
        }, "Numbers by size, texts alphabetically (the numbers in them by size: W2 before W10); By: by another list's values, place for place (elements by their height). Equal keys keep their order; nothing goes last. Order: where each item was, from 0."),
        new("slice", "Part of a list", "Lists", [Whole("list", "List"), In("start", "Start", "integer", 0), In("count", "Count", "integer")], [Out("list", "Items", "list")], c =>
        {
            var l = Values.List(c["list"]);
            var start = c.Int("start", 0);
            if (start < 0) start = Math.Max(0, start + l.Count);
            start = Math.Min(start, l.Count);
            var n = c["count"] == null ? l.Count - start : c.Int("count", 0);
            if (n < 0) throw new CommandException("A part of a list cannot have fewer than no items.");
            return One(l.GetRange(start, Math.Min(n, l.Count - start)));
        }, "Count items from Start (from 0; a negative start counts from the end); no count: to the end. The first 3: start 0, count 3; the last 3: start -3."),
        new("unique", "Unique", "Lists", [Whole("list", "List")], [Out("list", "Values", "list"), Out("times", "How many", "integer")], c =>
        {
            var (values, times) = Lists.Unique(Values.List(c["list"]));
            return [values, times];
        }, "Each value once, in the order first seen, and how many times it was there; numbers a billionth apart or less are the same."),
        new("cross", "Every with every", "Lists", [Whole("a", "A"), Whole("b", "B")], [Out("a", "A", "any"), Out("b", "B", "any"), Out("count", "Pairs", "integer")], c =>
        {
            List<object?> a = Values.List(c["a"]), b = Values.List(c["b"]);
            var n = (long)a.Count * b.Count;
            if (n > GraphLimits.MaxItems) throw new CommandException($"Every with every makes {n} pairs: at most {GraphLimits.MaxItems}.");
            var outA = new List<object?>((int)n);
            var outB = new List<object?>((int)n);
            foreach (var x in a) foreach (var y in b) { outA.Add(x); outB.Add(y); }
            return [outA, outB, (double)n];
        }, "Every pair of an item of A and an item of B: each of A's repeated, B's over again, so that the next node, fed both, runs on every pair."),

        // ── maths ──
        Binary("add", "Add", (a, b) => a + b),
        Binary("subtract", "Subtract", (a, b) => a - b),
        Binary("multiply", "Multiply", (a, b) => a * b),
        Binary("divide", "Divide", (a, b) => Math.Abs(b) < 1e-12 ? throw new CommandException("Division by zero.") : a / b),
        new("formula", "Formula", "Maths", [Set("formula", "Formula", "text", "a + b"), In("a", "a", "number"), In("b", "b", "number"), In("c", "c", "number"), In("d", "d", "number")],
            [Out("value", "Value", "number")], c =>
            {
                var f = c.Text("formula");
                var (v, error) = Expr.TryEval(f, n => n is "a" or "b" or "c" or "d" ? c[n] is { } x ? Values.Number(x, n) : null : null);
                return v is { } d ? One(d) : throw new CommandException(error ?? "The formula does not read.");
            }, "A formula of a, b, c and d (+ - * / ^, min, max, round, sqrt, sin, cos, if…; angles in degrees)."),
        new("round", "Round off", "Maths", [In("value", "Value", "number"), In("step", "To the nearest", "number", 1)], [Out("value", "Value", "number")], c =>
        {
            var s = Math.Abs(c.Num("step", 1));
            var v = c.Num("value");
            return One(s < 1e-12 ? v : Math.Round(v / s) * s);
        }),
        new("compare", "Compare", "Maths", [In("a", "A", "number"), Set("op", "Is", "text", "<", ["<", "<=", "=", "!=", ">=", ">"]), In("b", "B", "number")], [Out("result", "Holds", "bool")], c =>
        {
            double a = c.Num("a"), b = c.Num("b");
            return One(c.Text("op") switch
            {
                "<" => a < b, "<=" => a <= b + 1e-9, "=" => Math.Abs(a - b) < 1e-9, "!=" => Math.Abs(a - b) >= 1e-9, ">=" => a >= b - 1e-9, ">" => a > b,
                var op => throw new CommandException($"„{op}” is not a comparison."),
            });
        }),

        // ── points and simple geometry ──
        new("point", "Point", "Geometry", [In("x", "X", "number", 0), In("y", "Y", "number", 0), In("z", "Z", "number", 0)], [Out("point", "Point", "point")], c =>
            One(new Vec3(c.Num("x", 0), c.Num("y", 0), c.Num("z", 0)))),
        new("xyz", "Point's coordinates", "Geometry", [In("point", "Point", "point")], [Out("x", "X", "number"), Out("y", "Y", "number"), Out("z", "Z", "number")], c =>
        {
            var p = c.Pt("point");
            return [p.X, p.Y, p.Z];
        }),
        new("move", "Move point", "Geometry", [In("point", "Point", "point"), In("dx", "Along X", "number", 0), In("dy", "Along Y", "number", 0), In("dz", "Up", "number", 0)], [Out("point", "Point", "point")], c =>
            One(c.Pt("point") + new Vec3(c.Num("dx", 0), c.Num("dy", 0), c.Num("dz", 0)))),
        new("distance", "Distance", "Geometry", [In("a", "A", "point"), In("b", "B", "point")], [Out("distance", "Distance", "number")], c => One((c.Pt("b") - c.Pt("a")).Length)),
        new("divideLine", "Divide line", "Geometry", [In("a", "Start", "point"), In("b", "End", "point"), In("count", "Parts", "integer", 4)], [Out("points", "Points", "point")], c =>
        {
            var n = c.Int("count", 4);
            if (n < 1) throw new CommandException("A line is divided into one part or more.");
            Limit(n + 1);
            Vec3 a = c.Pt("a"), b = c.Pt("b");
            return One(Enumerable.Range(0, n + 1).Select(i => (object?)(a + (b - a) * ((double)i / n))).ToList());
        }, "The points dividing a line into equal parts, both ends included."),
        new("grid", "Grid of points", "Geometry", [In("origin", "Origin", "point"), In("nx", "Along X", "integer", 3), In("ny", "Along Y", "integer", 3), In("dx", "X spacing", "number", 1), In("dy", "Y spacing", "number", 1)],
            [Out("points", "Points", "point")], c =>
            {
                int nx = c.Int("nx", 3), ny = c.Int("ny", 3);
                if (nx < 1 || ny < 1) throw new CommandException("A grid has one point or more each way.");
                Limit(nx * ny);
                var o = c["origin"] == null ? default : c.Pt("origin");
                double dx = c.Num("dx", 1), dy = c.Num("dy", 1);
                var list = new List<object?>(nx * ny);
                for (var j = 0; j < ny; j++) for (var i = 0; i < nx; i++) list.Add(o + new Vec3(i * dx, j * dy, 0));
                return One(list);
            }, "Row by row, from the origin."),
        new("circle", "Points on a circle", "Geometry", [In("center", "Centre", "point"), In("radius", "Radius", "number", 5), In("count", "Count", "integer", 8), In("start", "Start angle", "number", 0)], [Out("points", "Points", "point")], c =>
        {
            var n = c.Int("count", 8);
            if (n < 1) throw new CommandException("A circle has one point or more.");
            Limit(n);
            var o = c["center"] == null ? default : c.Pt("center");
            var r = c.Num("radius", 5);
            var a0 = c.Num("start", 0) * Math.PI / 180;
            return One(Enumerable.Range(0, n).Select(i => (object?)(o + new Vec3(r * Math.Cos(a0 + 2 * Math.PI * i / n), r * Math.Sin(a0 + 2 * Math.PI * i / n), 0))).ToList());
        }, "Count points evenly round a circle, anticlockwise from the start angle (degrees, 0 along X)."),
        new("rotate", "Rotate", "Geometry", [In("geometry", "Points or polyline"), In("center", "Centre", "point"), In("angle", "Angle", "number", 0)], [Out("geometry", "Rotated")], c =>
        {
            var o = c["center"] == null ? default : c.Pt("center");
            var a = c.Num("angle", 0) * Math.PI / 180;
            double cos = Math.Cos(a), sin = Math.Sin(a);
            return One(Transform(c["geometry"], "Rotate", p =>
            {
                double x = p.X - o.X, y = p.Y - o.Y;
                return new Vec3(o.X + x * cos - y * sin, o.Y + x * sin + y * cos, p.Z);
            }));
        }, "Points or polylines turned about the vertical through the centre (none: the origin) by the angle in degrees, anticlockwise; a list of angles gives one copy for each."),
        new("scale", "Scale", "Geometry", [In("geometry", "Points or polyline"), In("center", "Centre", "point"), In("factor", "Factor", "number", 1), In("zfactor", "Z factor", "number")], [Out("geometry", "Scaled")], c =>
        {
            var o = c["center"] == null ? default : c.Pt("center");
            var k = c.Num("factor", 1);
            var kz = c.Num("zfactor", k);
            return One(Transform(c["geometry"], "Scale", p => new Vec3(o.X + (p.X - o.X) * k, o.Y + (p.Y - o.Y) * k, o.Z + (p.Z - o.Z) * kz)));
        }, "Points or polylines made larger or smaller about the centre (none: the origin): 2 doubles them, 0.5 halves them; Z factor, if given, scales the heights apart."),
        new("center", "Centre", "Geometry", [Whole("geometry", "Points or polyline", "any")], [Out("point", "Centre", "point")], c =>
        {
            var pts = Flat(Values.List(c["geometry"])).SelectMany(x => x is Polyline l ? l.Points : [Values.Point(x, "Points or polyline")]).ToList();
            if (pts.Count == 0) throw new CommandException("Centre takes points or a polyline.");
            return One(new Vec3((pts.Min(p => p.X) + pts.Max(p => p.X)) / 2, (pts.Min(p => p.Y) + pts.Max(p => p.Y)) / 2, (pts.Min(p => p.Z) + pts.Max(p => p.Z)) / 2));
        }, "The middle of the box round the points or polylines given (all of them together): a rectangle's centre."),
        new("line", "Line", "Geometry", [In("a", "Start", "point"), In("b", "End", "point")], [Out("line", "Line", "geometry")], c => One(new Polyline([c.Pt("a"), c.Pt("b")]))),
        new("polyline", "Polyline", "Geometry", [Whole("points", "Points", "point"), Set("closed", "Closed", "bool", false)], [Out("line", "Polyline", "geometry")], c =>
        {
            var pts = Flat(Values.List(c["points"])).Select(p => Values.Point(p, "Points")).ToList();
            if (pts.Count < 2) throw new CommandException("A polyline needs two points or more.");
            return One(new Polyline(pts, Values.Bool(c["closed"])));
        }),
        new("rectangle", "Rectangle", "Geometry", [In("origin", "Corner", "point"), In("width", "Width", "number", 4), In("depth", "Depth", "number", 3)],
            [Out("line", "Outline", "geometry"), Out("corners", "Corners", "point")], c =>
            {
                var o = c["origin"] == null ? default : c.Pt("origin");
                double w = c.Num("width", 4), d = c.Num("depth", 3);
                List<Vec3> pts = [o, o + new Vec3(w, 0, 0), o + new Vec3(w, d, 0), o + new Vec3(0, d, 0)];
                return [new Polyline(pts, true), pts.Cast<object?>().ToList()];
            }),
        new("corners", "Corners", "Geometry", [In("line", "Polyline", "geometry")], [Out("points", "Points", "point"), Out("starts", "Segment starts", "point"), Out("ends", "Segment ends", "point")], c =>
        {
            if (c["line"] is not Polyline l) throw new CommandException("Corners takes a line or a polyline.");
            var n = l.Points.Count;
            var segs = l.Closed ? n : n - 1;
            return [l.Points.Cast<object?>().ToList(), Enumerable.Range(0, segs).Select(i => (object?)l.Points[i]).ToList(), Enumerable.Range(0, segs).Select(i => (object?)l.Points[(i + 1) % n]).ToList()];
        }, "A polyline's points, and the starts and ends of its segments (to draw a wall on each)."),

        // ── text ──
        new("join", "Join texts", "Text", [In("a", "A", "text", ""), In("b", "B", "text", ""), Set("separator", "Between", "text", "")], [Out("text", "Text", "text")], c =>
            One(c.Text("a") + c.Text("separator") + c.Text("b"))),
        new("format", "Number as text", "Text", [In("value", "Number", "number"), Set("format", "Format", "text", "0.00")], [Out("text", "Text", "text")], c =>
            One(Expr.Format(c.Num("value"), c.Text("format")))),

        // ── the model ──
        new("ofClass", "Elements of a class", "Model", [In("class", "IFC class", "text", "IfcWall")], [Out("ids", "Elements", "element")], c =>
        {
            var cls = c.Text("class").Trim();
            var ids = c.Model.OfClass(cls).Select(e => (object?)e.Id).ToList();
            Limit(ids.Count);
            return One(ids);
        }, "Every element of a class (IfcWall, IfcSlab…) in the model as it is."),
        new("storeys", "Storeys", "Model", [], [Out("ids", "Storeys", "storey"), Out("elevations", "Storey levels", "number")], c =>
        {
            var s = Spatial.Storeys(c.Model).ToList();
            return [s.Select(e => (object?)e.Id).ToList(), s.Select(e => (object?)e.Num("elevation")).ToList()];
        }),
        new("property", "Property", "Model", [In("id", "Element", "element"), In("key", "Property", "text", "name")], [Out("value", "Value")], c =>
        {
            var id = c.Text("id");
            var e = c.Model.Get(id) ?? throw new CommandException($"The model has no element {id}.");
            var key = c.Text("key").Trim();
            return One(key switch
            {
                "name" => e.Name, "class" => e.Class, "id" => e.Id,
                _ => e.Attrs[key] is { } v ? Values.FromJson(v) : throw new CommandException($"„{e.Name}” has no property „{key}”."),
            });
        }, "An element's attribute by its key (height, thickness…), or its name, class or id."),

        // ── the model's geometry, read as references (decision 534) ──
        new("axis", "Axis", "Model", [In("id", "Element", "element")], [Out("line", "Axis", "geometry"), Out("start", "Start", "point"), Out("end", "End", "point")], References.Axis,
            "The axis of a wall (curved too), a beam, a footing, a railing or each axis of a grid, as a polyline; its start and end."),
        new("outline", "Outline", "Model", [In("id", "Element", "element")], [Out("line", "Outline", "geometry"), Out("points", "Corners", "point")], References.Outline,
            "The closed outline of a floor, a room, a roof or a site zone, at its level, and its corners."),
        new("location", "Location", "Model", [In("id", "Element", "element")], [Out("point", "Point", "point"), Out("rotation", "Rotation", "number")], References.Location,
            "Where a column, a pad footing, furniture, a door or a window stands, and its turn in degrees."),
        new("box", "Bounding box", "Model", [In("id", "Element", "element")],
            [Out("min", "Lowest corner", "point"), Out("max", "Highest corner", "point"), Out("center", "Centre", "point"), Out("x", "Size along X", "number"), Out("y", "Size along Y", "number"), Out("z", "Height", "number")],
            References.Box, "The box round any element's body, as the model builds it."),
        new("drafted", "Drafting geometry", "Model", [In("id", "Element", "element")],
            [Out("line", "Line", "geometry"), Out("points", "Points", "point"), Out("start", "Start", "point"), Out("end", "End", "point"), Out("center", "Centre", "point")], References.Drafted,
            "A drafted line, polyline, rectangle, arc or circle (or a DXF's linework) in the model, on its view's level; arcs as points. Pick them in a plan with Elements picked."),

        // ── outputs ──
        new("watch", "Watch", "Outputs", [Whole("value", "Value", "any")], [Out("value", "Value")], c => One(c["value"]), "Shows what it is given."),
    ];

    static readonly Dictionary<string, NodeDef> ByKind = All.ToDictionary(d => d.Kind);

    /// <summary>The input nodes (each a field of the graph's palette, decision 536) and the setting that holds the value a field sets.</summary>
    public static string? InputPort(string kind) => kind switch
    {
        "number" or "integer" or "text" or "bool" or "choice" or "storey" or "type" => "value",
        "pick" => "ids",
        _ => null,
    };

    /// <summary>A palette's value for an input node, refused when it is not of the node's kind (a number for a number, ids for elements picked…).</summary>
    public static void CheckInput(string kind, JsonNode? value, string name)
    {
        var ok = kind switch
        {
            "number" => value is JsonValue && Json.Number(value) is { } d && double.IsFinite(d),
            "integer" => value is JsonValue && Json.Number(value) is { } d && Math.Abs(d - Math.Round(d)) < 1e-9,
            "bool" => value is JsonValue v && v.TryGetValue<bool>(out _),
            "text" or "choice" or "storey" or "type" => value is JsonValue v && v.TryGetValue<string>(out _),
            "pick" => value is JsonArray a && a.All(x => x is JsonValue v && v.TryGetValue<string>(out _)),
            _ => false,
        };
        if (!ok) throw new CommandException($"„{name}” does not take that value.");
    }

    public static NodeDef? Of(string kind) => ByKind.GetValueOrDefault(kind);

    static NodeDef Binary(string kind, string label, Func<double, double, double> f) =>
        new(kind, label, "Maths", [In("a", "A", "number", 0), In("b", "B", "number", 0)], [Out("value", "Value", "number")], c => One(f(c.Num("a", 0), c.Num("b", 0))));

    /// <summary>A point or a polyline moved point by point; a list of them each (a point written as [x, y] too).</summary>
    static object? Transform(object? v, string node, Func<Vec3, Vec3> f) => v switch
    {
        Vec3 p => f(p),
        Polyline l => new Polyline(l.Points.Select(f).ToList(), l.Closed),
        List<object?> l when l.Count is 2 or 3 && l.All(x => x is double) => f(Values.Point(l, node)),
        List<object?> l => l.Select(x => Transform(x, node, f)).ToList(),
        null => throw new CommandException($"{node}: points or a polyline are missing."),
        _ => throw new CommandException($"{node} takes points or polylines, not {Values.Describe(v)}."),
    };

    static IEnumerable<object?> Flat(IEnumerable<object?> l) => l.SelectMany(x => x is List<object?> inner ? Flat(inner) : [x]);

    static void Limit(int n)
    {
        if (n > GraphLimits.MaxItems) throw new CommandException($"A list holds at most {GraphLimits.MaxItems} items ({n} asked).");
    }
}
