using System.Text.Json.Nodes;
using Lapin.Drafting.Cad;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Studio;

/// <summary>
/// "Edit sketch" for an element's outline (forum topic 97, stage 3): a floor's, a floor opening's or a ceiling's outline opened as
/// a sketch, drawn and constrained with the drafting commands and the sketch constraints (<see cref="SketchConstraints"/>), then
/// written back.
///
/// <para><b>Entering</b> (<c>studio.editOutline {id}</c>) makes a plan view of the element's storey of its own, marked with the element
/// (<see cref="OfKey"/>), the element hidden in it, and draws the outline there: the sketch it was last finished with when its outline
/// is still that one (its shapes and constraints, the shapes under new ids), else one line or arc per edge of its outline as it is
/// (an outline moved by its grips since: its constraints are left out, said in the answer). Every drafting command (Line, Arc,
/// Fillet, Trim, Offset…) and every constraint and dimension works on it as on any sketch, each its own undo step.</para>
///
/// <para><b>Finishing</b> (<c>studio.finishOutline {viewId}</c>, one undo step) chains the view's shapes (lines, arcs, polylines; a
/// circle alone; construction lines left out) into one closed outline, its corners and its edges' rises, and gives it to the
/// element's own update command (<c>architecture.updateSlab</c>, <c>updateSlabOpening</c>, <c>updateCeiling</c>), so it is checked and
/// its layer extents follow as when its grips move. The element keeps its id (its GlobalId); the sketch (its shapes, constraints and
/// the outline it made) stays on it as <see cref="Key"/>, design intent that goes to IFC in Lapin_Intent with its other attributes, and
/// the view goes. <c>studio.cancelOutline {viewId}</c> drops the view and what is drawn on it. An element kept as a reference (its
/// file's body) is refused; a floor recognised from IFC gets a sketch only when the user edits it (an import adds nothing of its own).</para>
/// </summary>
public static class OutlineSketches
{
    /// <summary>The attribute holding an element's outline sketch: {shapes: [{id, shape}], constraints, contour, arcs?}.</summary>
    public const string Key = "outlineSketch";

    /// <summary>The attribute marking the plan view an element's outline is edited in.</summary>
    public const string OfKey = "outlineSketchOf";

    /// <summary>How near two ends are to be one corner (m).</summary>
    const double Tol = 1e-5;

    static string? UpdateCommand(Entity e) => e.Class switch
    {
        IfcClass.Slab => "architecture.updateSlab",
        "LapinCeiling" => "architecture.updateCeiling",
        IfcClass.OpeningElement when e.Attrs["contour"] != null && e.Str("stairId") == null => "architecture.updateSlabOpening",
        // A drawn room and a platform too (audit S4).
        IfcClass.Space when e.Attrs["contour"] != null && !e.Bool("auto") => "architecture.updateRoom",
        IfcClass.EarthworksCut when e.Attrs["contour"] != null && e.PredefinedType != "TRENCH" => "civil.updatePlatform",
        _ => null,
    };

    /// <summary>Whether an element's outline may have holes of its own (audit S4): a floor, a ceiling, a platform.</summary>
    static bool TakesHoles(Entity e) => e.Class is IfcClass.Slab or "LapinCeiling" or IfcClass.EarthworksCut;

        /// <summary>
    /// Whether an element's outline can be edited as a sketch: a floor, a floor opening or a ceiling built from its outline. A floor
    /// recognised from an IFC file is one (nothing is added to it until the user edits it); one kept as a reference, its file's body, is not.
    /// </summary>
    public static bool Editable(Entity e) => UpdateCommand(e) != null && !StoredBodies.Has(e) && e.Attrs["reference"] == null;

    static IReadOnlyList<Vec2> Corners(Entity e) => Args.StoredPoints(e.Attrs["contour"]);

    static CommandEntry Command(ICommandContext ctx, string name) =>
        (ctx.Services as ModuleCatalog)?.Command(name) ?? throw new CommandException($"Editing an outline as a sketch needs {name}, not in this product.");

    /// <summary>One line or arc per edge of an outline (an arc runs anticlockwise: from the edge's end when the edge turns clockwise).</summary>
    public static List<Shape> ShapesOf(IReadOnlyList<Vec2> corners, IReadOnlyList<double>? rises)
    {
        var o = new List<Shape>();
        for (var i = 0; i < corners.Count; i++)
        {
            var c = ArcOutline.Edge(corners, rises, i);
            if (!c.IsArc) { o.Add(new Line(c.Start, c.End)); continue; }
            var (a, b) = (c.Start - c.Centre, c.End - c.Centre);
            var (sa, ea) = (Math.Atan2(a.Y, a.X), Math.Atan2(b.Y, b.X));
            // A rise to the right (negative) turns anticlockwise from the start; to the left, anticlockwise from the end.
            o.Add(c.Rise < 0 ? new Arc(c.Centre, c.Radius, sa, ea) : new Arc(c.Centre, c.Radius, ea, sa));
        }
        return o;
    }

    static JsonObject Ref(string shape, string part) => new() { ["shape"] = shape, ["part"] = part };

    /// <summary>Which end of a line or an arc lies at a point: "start" or "end".</summary>
    static string EndAt(Shape s, Vec2 p) => s switch
    {
        Line l => (l.A - p).Length <= (l.B - p).Length ? "start" : "end",
        Arc a => (a.At(a.Start) - p).Length <= (a.At(a.End) - p).Length ? "start" : "end",
        _ => "start",
    };

    /// <summary>A piece of the outline: from A to B, rising off its chord (left positive).</summary>
    readonly record struct Piece(Vec2 A, Vec2 B, double Rise)
    {
        public Piece Reversed => new(B, A, -Rise);
    }

    /// <summary>
    /// The shapes chained into one closed outline: its corners and their edges' rises. Refused, saying why, when they are not one
    /// closed loop (an end loose, a branch, two loops).
    /// </summary>
    public static (List<Vec2> Corners, double[] Rises) Loop(IReadOnlyList<Shape> shapes)
    {
        if (shapes.Count == 1 && shapes[0] is Circle circle)
        {
            var (c, r) = ArcOutline.Circle(circle.C, circle.R);
            return (c, r);
        }
        var pieces = new List<Piece>();
        foreach (var s in shapes)
            switch (s)
            {
                case Line l when (l.B - l.A).Length > Tol: pieces.Add(new Piece(l.A, l.B, 0)); break;
                case Polyline p:
                    for (var i = 0; i < (p.Closed ? p.Pts.Count : p.Pts.Count - 1); i++)
                        if ((p.Pts[(i + 1) % p.Pts.Count] - p.Pts[i]).Length > Tol) pieces.Add(new Piece(p.Pts[i], p.Pts[(i + 1) % p.Pts.Count], 0));
                    break;
                case Arc a:
                    var sweep = a.Sweep;
                    // Anticlockwise from its start: bulging to the right of start → end, a negative rise.
                    pieces.Add(new Piece(a.At(a.Start), a.At(a.End), -a.R * (1 - Math.Cos(sweep / 2))));
                    break;
                case Circle:
                    throw new CommandException("A circle is an outline only alone: trim it into arcs to join it to other lines.");
            }
        if (pieces.Count < 2) throw new CommandException("The sketch has no closed outline: draw its edges end to end.");
        bool Same(Vec2 p, Vec2 q) => (p - q).Length <= Tol;
        // Each end must meet exactly one other end.
        var ends = pieces.SelectMany(p => new[] { p.A, p.B }).ToList();
        var loose = ends.Count(p => ends.Count(q => Same(p, q)) == 1);
        if (loose > 0) throw new CommandException($"The sketch is not closed: {loose} loose end{(loose == 1 ? "" : "s")}. Join its edges end to end (Trim, Extend, Fillet).");
        if (ends.Any(p => ends.Count(q => Same(p, q)) > 2)) throw new CommandException("The sketch branches: more than two edges meet at a point. Keep one outline, trim the rest.");
        var left = new List<Piece>(pieces);
        var chain = new List<Piece> { left[0] };
        left.RemoveAt(0);
        while (left.Count > 0)
        {
            var at = chain[^1].B;
            var k = left.FindIndex(p => Same(p.A, at) || Same(p.B, at));
            if (k < 0) break;
            chain.Add(Same(left[k].A, at) ? left[k] : left[k].Reversed);
            left.RemoveAt(k);
        }
        if (left.Count > 0) throw new CommandException("The sketch holds more than one outline: keep one, its edges end to end.");
        // Straight pieces in line are one edge: a corner where nothing turns is not kept.
        var corners = chain.Select(p => p.A).ToList();
        var rises = chain.Select(p => p.Rise).ToList();
        for (var i = corners.Count - 1; i >= 0 && corners.Count > 3; i--)
        {
            var prev = (i - 1 + corners.Count) % corners.Count;
            if (rises[i] != 0 || rises[prev] != 0) continue;
            var (p, c, q) = (corners[prev], corners[i], corners[(i + 1) % corners.Count]);
            if (Math.Abs(Vec2.Cross((c - p).Normalized(), (q - c).Normalized())) < 1e-9 && Vec2.Dot(c - p, q - c) > 0) { corners.RemoveAt(i); rises.RemoveAt(i); }
        }
        return (corners, [.. rises]);
    }

    /// <summary>
    /// The shapes chained into closed outlines (audit S4): each set of shapes meeting end to end one loop (a circle alone one of
    /// its own), the largest the outline, the others its holes. Refused, saying why, when a set is not one closed loop.
    /// </summary>
    public static (List<Vec2> Corners, double[] Rises, List<OutlineHoles.Hole> Holes) Loops(IReadOnlyList<Shape> shapes)
    {
        bool Same(Vec2 p, Vec2 q) => (p - q).Length <= Tol;
        IEnumerable<Vec2> Ends(Shape s) => s switch
        {
            Line l => [l.A, l.B],
            Polyline p => p.Closed ? p.Pts : [p.Pts[0], p.Pts[^1]],
            Arc a => [a.At(a.Start), a.At(a.End)],
            _ => [],
        };
        // Sets of shapes that meet end to end.
        var group = Enumerable.Range(0, shapes.Count).ToArray();
        int Root(int i) => group[i] == i ? i : group[i] = Root(group[i]);
        for (var i = 0; i < shapes.Count; i++)
            for (var j = 0; j < i; j++)
                if (shapes[i] is not Circle && shapes[j] is not Circle && Ends(shapes[i]).Any(p => Ends(shapes[j]).Any(q => Same(p, q)))) group[Root(i)] = Root(j);
        var loops = Enumerable.Range(0, shapes.Count).GroupBy(Root).Select(g => Loop([.. g.Select(i => shapes[i])])).ToList();
        if (loops.Count == 0) throw new CommandException("The sketch has no closed outline: draw its edges end to end.");
        var outer = loops.MaxBy(l => Math.Abs(ArcOutline.SignedArea(l.Corners, l.Rises)));
        var holes = loops.Where(l => !ReferenceEquals(l.Corners, outer.Corners)).Select(l =>
        {
            var (c, r) = ArcOutline.Check(l.Corners, l.Rises, "A hole");
            return new OutlineHoles.Hole(c, r);
        }).ToList();
        return (outer.Corners, outer.Rises, holes);
    }

    /// <summary>The view an element's outline is edited in, when it is.</summary>
    public static Entity? ViewOf(IModelView m, string elementId) => m.OfClass(Views.Class).FirstOrDefault(v => v.Str(OfKey) == elementId);

    static bool SameOutline(JsonObject sketch, Entity e) =>
        JsonNode.DeepEquals(sketch["contour"], e.Attrs["contour"]) && JsonNode.DeepEquals(sketch[ArcOutline.Key], e.Attrs[ArcOutline.Key])
        && JsonNode.DeepEquals(sketch[OutlineHoles.Key], e.Attrs[OutlineHoles.Key]) && JsonNode.DeepEquals(sketch[OutlineHoles.ArcsKey], e.Attrs[OutlineHoles.ArcsKey]);

    public static void Register(IModuleRegistry r)
    {
        // {id} → {viewId, ids (the shapes drawn), constraints (how many came back), rebuilt (its sketch left out: the outline moved since)}.
        r.Command("editOutline", "Edit sketch", (ctx, a) =>
        {
            var e = ctx.Model.Get((string?)a["id"] ?? "") ?? throw new CommandException("The element is not in the project.");
            if (UpdateCommand(e) == null) throw new CommandException("Only a floor, a floor opening, a ceiling, a drawn room or a platform has its outline edited as a sketch.");
            if (!Editable(e)) throw new CommandException("An element kept as a reference from its file is not edited as a sketch.");
            if (ViewOf(ctx.Model, e.Id) is { } open) return new JsonObject { ["viewId"] = open.Id, ["ids"] = new JsonArray([.. Drafts.On(ctx.Model, open.Id).Select(d => (JsonNode)d.Id)]), ["constraints"] = 0, ["rebuilt"] = false };
            var corners = Corners(e);
            if (corners.Count < 3) throw new CommandException("The element has no outline.");
            var storey = Spatial.StoreyOf(ctx.Model, e.Id) ?? (e.Class == IfcClass.OpeningElement && ctx.Model.RelatingOf(e.Id, IfcClass.RelVoidsElement) is { } host ? Spatial.StoreyOf(ctx.Model, host.Id) : null)
                ?? (e.Class == IfcClass.EarthworksCut ? Spatial.Storeys(ctx.Model).FirstOrDefault() : null) ?? throw new CommandException("The element stands on no storey.");
            var made = Command(ctx, "view.create").Handler(ctx, new JsonObject { ["kind"] = "plan", ["storeyId"] = storey.Id, ["name"] = $"Sketch: {e.Name}" }) as JsonObject;
            var view = ctx.Model.Get((string?)made?["id"] ?? "") ?? throw new CommandException("The sketch's view could not be made.");
            var vattrs = view.CloneAttrs();
            vattrs[OfKey] = e.Id;
            // The element itself is not drawn while its sketch is: the sketch is what it will be.
            vattrs[ViewHiding.Key] = new JsonArray(e.Id);
            ctx.Replace(view.With(attrs: vattrs));
            var ids = new List<string>();
            var restored = 0;
            var rebuilt = false;
            if (e.Attrs[Key] is JsonObject sketch && SameOutline(sketch, e) && sketch["shapes"] is JsonArray stored)
            {
                // The sketch it was finished with: its shapes under new ids, its constraints pointed at them.
                var map = new Dictionary<string, string>();
                foreach (var item in stored.OfType<JsonObject>())
                    if (Shape.From(item["shape"]) is { } s)
                    {
                        var d = Drafts.Add(ctx, view.Id, "0", s, Drafts.CheckRole(item["role"]));
                        if ((string?)item["id"] is { } old) map[old] = d.Id;
                        ids.Add(d.Id);
                    }
                var cs = new JsonArray();
                foreach (var c in (sketch["constraints"] as JsonArray ?? []).OfType<JsonObject>())
                {
                    var copy = (JsonObject)c.DeepClone();
                    copy.Remove("id");
                    var refs = copy["refs"] as JsonArray ?? [];
                    if (refs.OfType<JsonObject>().Any(x => !map.ContainsKey((string?)x["shape"] ?? ""))) continue;
                    foreach (var x in refs.OfType<JsonObject>()) x["shape"] = map[(string)x["shape"]!];
                    copy["viewId"] = view.Id;
                    cs.Add((JsonNode)copy);
                }
                if (cs.Count > 0)
                {
                    Command(ctx, "studio.addConstraint").Handler(ctx, new JsonObject { ["constraints"] = cs });
                    restored = cs.Count;
                }
            }
            else
            {
                rebuilt = e.Attrs[Key] != null;
                var cs = new JsonArray();
                // The outline, then each of its own holes (audit S4), every loop drawn one line or arc per edge.
                var loops = new List<(IReadOnlyList<Vec2> Corners, IReadOnlyList<double>? Rises)> { (corners, ArcOutline.Rises(e.Attrs[ArcOutline.Key], corners.Count)) };
                loops.AddRange(OutlineHoles.Of(e).Select(h => (h.Corners, (IReadOnlyList<double>?)h.Rises)));
                foreach (var (loopCorners, loopRises) in loops)
                {
                    var shapes = ShapesOf(loopCorners, loopRises);
                    var loopIds = shapes.Select(s => Drafts.Add(ctx, view.Id, "0", s).Id).ToList();
                    ids.AddRange(loopIds);
                    // Its corners held: each edge's end on the next one's start, so a dimension moves the outline, never tears it.
                    for (var i = 0; i < shapes.Count; i++)
                    {
                        var j = (i + 1) % shapes.Count;
                        var corner = loopCorners[j];
                        cs.Add((JsonNode)new JsonObject
                        {
                            ["kind"] = "coincident", ["viewId"] = view.Id,
                            ["refs"] = new JsonArray(Ref(loopIds[i], EndAt(shapes[i], corner)), Ref(loopIds[j], EndAt(shapes[j], corner))),
                        });
                    }
                }
                Command(ctx, "studio.addConstraint").Handler(ctx, new JsonObject { ["constraints"] = cs });
            }
            return new JsonObject { ["viewId"] = view.Id, ["ids"] = new JsonArray([.. ids.Select(x => (JsonNode)x)]), ["constraints"] = restored, ["rebuilt"] = rebuilt };
        });

        // {viewId} → {id}: the sketch written to its element, one undo step.
        r.Command("finishOutline", "Finish sketch", (ctx, a) =>
        {
            var view = Views.Find(ctx.Model, (string?)a["viewId"] ?? "") ?? throw new CommandException("There is no such view.");
            var e = view.Str(OfKey) is { } eid ? ctx.Model.Get(eid) : null;
            if (e == null) throw new CommandException(view.Str(OfKey) == null ? "That view is not an outline's sketch." : "The element whose sketch this is is gone.");
            var drafts = Drafts.On(ctx.Model, view.Id).ToList();
            var outline = drafts.Where(d => d.Str("role") is null or "profile").Select(d => (d, Drafts.ShapeOf(d))).Where(x => x.Item2 != null).ToList();
            // Several closed loops (audit S4): the largest the outline, the others its holes, for an element that takes them.
            var (corners, rises, holes) = Loops([.. outline.Select(x => x.Item2!)]);
            if (holes.Count > 0 && !TakesHoles(e)) throw new CommandException("The sketch holds more than one outline: keep one, its edges end to end.");
            var update = new JsonObject
            {
                ["id"] = e.Id,
                ["points"] = new JsonArray([.. corners.Select(p => (JsonNode)new JsonArray(p.X, p.Y))]),
                [ArcOutline.Key] = new JsonArray([.. rises.Select(x => (JsonNode)x)]),
            };
            if (TakesHoles(e))
            {
                var held = new JsonObject();
                OutlineHoles.Write(held, holes);
                update[OutlineHoles.Key] = held[OutlineHoles.Key]?.DeepClone() ?? new JsonArray();
                if (held[OutlineHoles.ArcsKey] is { } ha) update[OutlineHoles.ArcsKey] = ha.DeepClone();
            }
            Command(ctx, UpdateCommand(e)!).Handler(ctx, update);
            // The sketch kept on the element: its shapes, its constraints, and the outline they made (to know it again).
            var after = ctx.Model.Get(e.Id)!;
            var carrier = SketchConstraints.CarrierOf(ctx.Model, view.Id);
            var constraints = new JsonArray([.. SketchConstraints.ListOf(carrier).Select(c => (JsonNode)c)]);
            var sketch = new JsonObject
            {
                ["shapes"] = new JsonArray([.. drafts.Select(d =>
                {
                    var o = new JsonObject { ["id"] = d.Id, ["shape"] = d.Attrs["shape"]?.DeepClone() };
                    if (d.Str("role") is { } role) o["role"] = role;
                    return (JsonNode)o;
                })]),
                ["constraints"] = constraints,
                ["contour"] = after.Attrs["contour"]?.DeepClone(),
            };
            if (after.Attrs[ArcOutline.Key] is { } arcs) sketch[ArcOutline.Key] = arcs.DeepClone();
            foreach (var k in new[] { OutlineHoles.Key, OutlineHoles.ArcsKey })
                if (after.Attrs[k] is { } held) sketch[k] = held.DeepClone();
            var attrs = after.CloneAttrs();
            attrs[Key] = sketch;
            ctx.Replace(after.With(attrs: attrs));
            Drop(ctx, view, drafts);
            return new JsonObject { ["id"] = e.Id };
        });

        // {viewId}: the sketch dropped, its element as it was.
        r.Command("cancelOutline", "Cancel sketch", (ctx, a) =>
        {
            var view = Views.Find(ctx.Model, (string?)a["viewId"] ?? "") ?? throw new CommandException("There is no such view.");
            if (view.Str(OfKey) == null) throw new CommandException("That view is not an outline's sketch.");
            Drop(ctx, view, Drafts.On(ctx.Model, view.Id).ToList());
            return null;
        });

        // {id} → {viewId?}: the view an element's outline is being edited in.
        r.Query("outlineSketchView", (q, a) => new JsonObject { ["viewId"] = ViewOf(q.Model, (string?)a["id"] ?? "")?.Id });
    }

    static void Drop(ICommandContext ctx, Entity view, IEnumerable<Entity> drafts)
    {
        foreach (var d in drafts) ctx.Remove(d.Id);
        if (SketchConstraints.CarrierOf(ctx.Model, view.Id) is { } carrier) ctx.Remove(carrier.Id);
        ctx.Remove(view.Id);
    }
}
