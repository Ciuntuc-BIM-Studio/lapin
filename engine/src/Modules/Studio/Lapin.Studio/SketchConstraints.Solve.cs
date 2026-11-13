using System.Text.Json.Nodes;
using Lapin.Drafting.Cad;
using Lapin.Geometry;
using Lapin.Geometry.Constraints;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Studio;

/// <summary>
/// Stage C1 of decision 520 (docs/plan-studio-parametric.md §4.3): a sketch's stored constraints become the system of Lapin's own
/// solver (<c>Lapin.Geometry/Constraints</c>) inside the command that changed them or their shapes, and the shapes go back with
/// <see cref="Drafts.Set"/> in the same undo step, so the solids built from them rebuild as they already do.
///
/// <para><b>The translation.</b> Only the shapes some constraint names are unknowns (the view's other drafts stay as they are). A
/// line is two points and a solver line; a polyline its vertices and a line per side (the closing one too); a circle its centre and
/// a solver circle; an arc its centre, start and end and a solver arc (counter-clockwise, as Lapin keeps them). Points of different
/// shapes are never merged: only a coincidence joins them, as the store keeps them (an exploded polyline's corners get theirs).
/// Each stored kind and pattern has its solver form (<see cref="Translate"/>): degrees become radians, a formula is worked out with
/// the defaults of the part's parameters, a driven dimension enters as a reference, a fix holds the shape where it is when the
/// command starts, and a polyline fixed whole holds each vertex.</para>
///
/// <para><b>Refusals.</b> A solve that ends Conflicting or NotConverged refuses the command (the conflicting constraints named by kind
/// and id), so the undo step never commits a broken sketch; a delete always goes through and says how the sketch stands
/// (<c>sketch: {status, conflicting}</c> in its result); removing a constraint never solves. A user's edit of some of a sketch's
/// shapes (a grip, a move of some of them) is a drag goal on each point it moved: the edit wins where the constraints allow, the
/// rest follows; one it cannot have is refused with the constraints that hold it.</para>
/// </summary>
public static partial class SketchConstraints
{
    /// <summary>How far (m) a dragged point may end from where it was dragged and the edit still count as made.</summary>
    const double GoalTol = 1e-6;

    /// <summary>The solver's picture of a sketch, and the way back to the drafted shapes.</summary>
    internal sealed class SolverSketch
    {
        public readonly List<SketchPoint> Points = [];
        public readonly List<SketchEntity> Entities = [];
        public readonly List<SketchConstraint> Constraints = [];
        /// <summary>Per solver constraint: its stored constraint's index in <see cref="Stored"/>, or −1.</summary>
        public readonly List<int> Owner = [];
        public readonly List<JsonObject> Stored = [];
        /// <summary>Per point and per entity: the shape and the part it stands for.</summary>
        public readonly List<(string Shape, string Part)> PointParts = [], EntityParts = [];
        /// <summary>The shapes in the system, in order, as they are in the model now.</summary>
        public readonly List<string> Order = [];
        public readonly Dictionary<string, Shape> Now = [];
        public readonly Dictionary<string, string> Names = [];
        /// <summary>A shape's points from <c>First</c>, and its entities from <c>Entity</c>.</summary>
        public readonly Dictionary<string, (int First, int Count, int Entity)> Span = [];
        readonly Dictionary<(string, string), int> point = [], entity = [];

        public int? PointAt(string shape, string part) => point.TryGetValue((shape, part), out var i) ? i : null;
        public int? EntityAt(string shape, string part) => entity.TryGetValue((shape, part), out var i) ? i : null;

        public Sketch ToSketch() => new(Points, Entities, Constraints);

        public void AddShape(string id, string name, Shape now, Shape start)
        {
            int first = Points.Count, e0 = Entities.Count;
            void P(Vec2 p, string part) { point[(id, part)] = Points.Count; PointParts.Add((id, part)); Points.Add(new SketchPoint(p.X, p.Y)); }
            void E(SketchEntity x, string part) { entity[(id, part)] = Entities.Count; EntityParts.Add((id, part)); Entities.Add(x); }
            switch (start)
            {
                case Line l:
                    P(l.A, "start"); P(l.B, "end");
                    E(new SketchLine(first, first + 1), "whole");
                    break;
                case Polyline pl:
                {
                    for (var i = 0; i < pl.Pts.Count; i++) P(pl.Pts[i], $"vertex:{i}");
                    var n = pl.Pts.Count;
                    for (var i = 0; i < (pl.Closed ? n : n - 1); i++) E(new SketchLine(first + i, first + (i + 1) % n), $"edge:{i}");
                    break;
                }
                case Circle c:
                    P(c.C, "center");
                    // A grip that changed the radius: the circle starts at its new one.
                    E(new SketchCircle(first, now is Circle cn ? cn.R : c.R), "whole");
                    break;
                case Arc a:
                    P(a.C, "center"); P(a.At(a.Start), "start"); P(a.At(a.End), "end");
                    E(new SketchArc(first, first + 1, first + 2, now is Arc an ? an.R : a.R), "whole");
                    break;
                default:
                    return;
            }
            Order.Add(id);
            Now[id] = now;
            Names[id] = name;
            Span[id] = (first, Points.Count - first, e0);
        }
    }

    /// <summary>The same sort of shape with as many points: its points can be paired one to one.</summary>
    static bool Pairs(Shape a, Shape b) => (a, b) switch
    {
        (Line, Line) or (Circle, Circle) or (Arc, Arc) => true,
        (Polyline p, Polyline q) => p.Closed == q.Closed && p.Pts.Count == q.Pts.Count,
        _ => false,
    };

    /// <summary>
    /// A view's constraints as the solver's system: the shapes they name (and <paramref name="extra"/>), from where
    /// <paramref name="from"/> says they started (a user's edit: their points before it) or as they are; constraints naming a shape
    /// that is gone are left out.
    /// </summary>
    internal static SolverSketch Build(IModelView m, IReadOnlyList<JsonObject> list, IEnumerable<string>? extra = null, IReadOnlyDictionary<string, Shape>? from = null, string? viewId = null)
    {
        var s = new SolverSketch();
        foreach (var id in list.SelectMany(c => Refs(c).Select(r => r.Shape)).Concat(extra ?? []).Distinct())
        {
            if (m.Get(id) is not { } e || Drafts.ShapeOf(e) is not { } now) continue;
            var start = from != null && from.TryGetValue(id, out var was) && Pairs(was, now) ? was : now;
            s.AddShape(id, e.Name, now, start);
        }
        Dictionary<string, double>? ps = null;
        foreach (var c in list)
        {
            if (Refs(c).Any(r => !s.Now.ContainsKey(r.Shape))) continue;
            var index = s.Stored.Count;
            s.Stored.Add(c);
            foreach (var sc in Translate(s, c, () => ps ??= viewId != null ? ParamsOf(m, viewId) : []))
            {
                s.Constraints.Add(sc);
                s.Owner.Add(index);
            }
        }
        return s;
    }

    /// <summary>
    /// A stored constraint in the solver's terms (docs/plan-studio-parametric.md §4.1 → <see cref="ConstraintKind"/>):
    /// <list type="table">
    /// <item>coincident [P, P] → Coincident; pointOn [P, L|C] → PointOn (a line's support, a circle or an arc's whole circle)</item>
    /// <item>horizontal, vertical [L] or [P, P] → the same; parallel, perpendicular [L, L] → the same</item>
    /// <item>tangent [L, C], [C, L], [C, C] → Tangent along; [P, P] (two ends) → Tangent at the join, each end's own line, arc or polyline end side</item>
    /// <item>equal [L, L] lengths, [C, C] radii; symmetric [P, P, L] or [P, P, P]; midpoint [P, L]</item>
    /// <item>fix [P] → a point held; [L] or [C] → the entity held; [S] (a polyline whole) → each vertex held</item>
    /// <item>distanceX/Y [P] → from the sketch's origin; [P, P] → second minus first; [L] → end minus start</item>
    /// <item>distance [P, P], [L] (length), [P, L] (from the line's support); radius, diameter [C]</item>
    /// <item>angle [L] → from the x axis; [L, L] → from the first to the second; degrees → radians</item>
    /// </list>
    /// </summary>
    static IEnumerable<SketchConstraint> Translate(SolverSketch s, JsonObject c, Func<Dictionary<string, double>> ps)
    {
        var kind = (string)c["kind"]!;
        var refs = Refs(c).ToList();
        int Pt(int i) => s.PointAt(refs[i].Shape, refs[i].Part) ?? throw new CommandException($"„{s.Names[refs[i].Shape]}” has no point „{refs[i].Part}”.");
        int En(int i) => s.EntityAt(refs[i].Shape, refs[i].Part) ?? throw new CommandException($"„{s.Names[refs[i].Shape]}” has no edge „{refs[i].Part}”.");
        bool IsPt(int i) => s.PointAt(refs[i].Shape, refs[i].Part) != null;
        var driving = c["driving"] is not JsonValue dv || !dv.TryGetValue<bool>(out var d) || d;
        double V() => (string?)c["formula"] != null || c["value"] != null ? Value(c, ps()) : 0;
        double Rad(double deg) => deg * Math.PI / 180;
        switch (kind)
        {
            case "coincident": yield return SketchConstraint.Coincident(Pt(0), Pt(1)); break;
            case "pointOn": yield return SketchConstraint.PointOn(Pt(0), En(1)); break;
            case "horizontal": yield return refs.Count == 1 ? SketchConstraint.Horizontal(En(0)) : SketchConstraint.HorizontalPoints(Pt(0), Pt(1)); break;
            case "vertical": yield return refs.Count == 1 ? SketchConstraint.Vertical(En(0)) : SketchConstraint.VerticalPoints(Pt(0), Pt(1)); break;
            case "parallel": yield return SketchConstraint.Parallel(En(0), En(1)); break;
            case "perpendicular": yield return SketchConstraint.Perpendicular(En(0), En(1)); break;
            case "tangent":
                if (IsPt(0))
                {
                    var e1 = EndEntity(s, refs[0]) ?? throw new CommandException(TangentCorner);
                    var e2 = EndEntity(s, refs[1]) ?? throw new CommandException(TangentCorner);
                    yield return SketchConstraint.TangentAt(e1, Pt(0), e2, Pt(1));
                }
                else yield return SketchConstraint.Tangent(En(0), En(1));
                break;
            case "equal": yield return SketchConstraint.Equal(En(0), En(1)); break;
            case "symmetric": yield return IsPt(2) ? SketchConstraint.SymmetricAboutPoint(Pt(0), Pt(1), Pt(2)) : SketchConstraint.Symmetric(Pt(0), Pt(1), En(2)); break;
            case "midpoint": yield return SketchConstraint.Midpoint(Pt(0), En(1)); break;
            case "fix":
                if (IsPt(0)) yield return SketchConstraint.FixPoint(Pt(0));
                else if (s.EntityAt(refs[0].Shape, refs[0].Part) is { } fe) yield return SketchConstraint.FixEntity(fe);
                else
                {
                    // A polyline whole: every vertex held.
                    var (first, count, _) = s.Span[refs[0].Shape];
                    for (var i = 0; i < count; i++) yield return SketchConstraint.FixPoint(first + i);
                }
                break;
            case "distanceX" or "distanceY":
            {
                var x = kind == "distanceX";
                var v = V();
                if (refs.Count == 2) yield return x ? SketchConstraint.DistanceX(Pt(0), Pt(1), v, driving) : SketchConstraint.DistanceY(Pt(0), Pt(1), v, driving);
                else if (IsPt(0)) yield return x ? SketchConstraint.DistanceXFromOrigin(Pt(0), v, driving) : SketchConstraint.DistanceYFromOrigin(Pt(0), v, driving);
                else yield return x ? SketchConstraint.DistanceXOfLine(En(0), v, driving) : SketchConstraint.DistanceYOfLine(En(0), v, driving);
                break;
            }
            case "distance":
            {
                var v = V();
                if (refs.Count == 1) yield return SketchConstraint.Length(En(0), v, driving);
                else if (IsPt(1)) yield return SketchConstraint.Distance(Pt(0), Pt(1), v, driving);
                else yield return SketchConstraint.DistancePointLine(Pt(0), En(1), v, driving);
                break;
            }
            case "radius": yield return SketchConstraint.Radius(En(0), V(), driving); break;
            case "diameter": yield return SketchConstraint.Diameter(En(0), V(), driving); break;
            case "angle":
                yield return refs.Count == 1 ? SketchConstraint.AngleToX(En(0), Rad(V()), driving) : SketchConstraint.Angle(En(0), En(1), Rad(V()), driving);
                break;
            default: throw new CommandException($"„{kind}” has no solver form.");
        }
    }

    const string TangentCorner = "Tangent end to end takes the end of a line, an arc or an open polyline, not a polyline's corner.";

    /// <summary>The entity an end belongs to, for a tangent at a join: a line's, an arc's, an open polyline's first or last side; null for a corner.</summary>
    static int? EndEntity(SolverSketch s, (string Shape, string Part) r) => s.Now[r.Shape] switch
    {
        Line or Arc when r.Part is "start" or "end" => s.EntityAt(r.Shape, "whole"),
        Polyline { Closed: false } when r.Part == "vertex:0" => s.EntityAt(r.Shape, "edge:0"),
        Polyline { Closed: false } p when r.Part == $"vertex:{p.Pts.Count - 1}" => s.EntityAt(r.Shape, $"edge:{p.Pts.Count - 2}"),
        _ => null,
    };

    /// <summary>What an end is for a tangent at a join: 'L' a straight end, 'C' an arc's, null a corner or a centre.</summary>
    static char? EndSort(Shape s, string part) => s switch
    {
        Line when part is "start" or "end" => 'L',
        Arc when part is "start" or "end" => 'C',
        Polyline { Closed: false } p when part == "vertex:0" || part == $"vertex:{p.Pts.Count - 1}" => 'L',
        _ => null,
    };

    /// <summary>A tangent at a join the solver has an equation for: two ends, an arc among them (store-level check, <see cref="Check"/>).</summary>
    static void CheckTangentEnds(IModelView m, IReadOnlyList<(string Shape, string Part, RefKind Kind)> found)
    {
        var sorts = found.Select(f => m.Get(f.Shape) is { } e && Drafts.ShapeOf(e) is { } s ? EndSort(s, f.Part) : null).ToList();
        if (sorts.Any(x => x == null)) throw new CommandException(TangentCorner);
        if (sorts.All(x => x == 'L')) throw new CommandException("Tangent end to end needs an arc: two straight ends meeting tangent make one straight line (use coincident and parallel).");
    }

    // ------------------------------------------------------------ solving

    static SolveResult Run(SolverSketch s, IReadOnlyList<DragGoal>? goals = null)
    {
        try { return SketchSolver.Solve(s.ToSketch(), goals is { Count: > 0 } ? new SolverOptions { DragGoals = goals } : null); }
        catch (ArgumentException ex) { throw new CommandException($"The sketch cannot be solved: {ex.Message}"); }
    }

    /// <summary>Stored constraints named by kind and id, from solver constraint indices.</summary>
    static string Named(SolverSketch s, IEnumerable<int> indices) =>
        string.Join(", ", OwnersOf(s, indices).Select(c => $"{(string?)c["kind"]} {(string?)c["id"]}"));

    static IEnumerable<JsonObject> OwnersOf(SolverSketch s, IEnumerable<int> indices) =>
        indices.Select(i => s.Owner[i]).Where(o => o >= 0).Distinct().Select(o => s.Stored[o]);

    static string Failure(SolverSketch s, SolveResult r) => r.Status == SolveStatus.Conflicting
        ? $"The sketch's constraints conflict ({Named(s, r.Conflicting)}): no shape satisfies them all."
        : "The sketch's constraints did not settle: the solve ran out of steps.";

    /// <summary>A shape as the solve leaves it (an arc counter-clockwise, its angles kept near where they were).</summary>
    static Shape After(SolverSketch s, SolveResult r, string id)
    {
        var (first, count, e0) = s.Span[id];
        Vec2 P(int i) => new(r.Points[first + i].X, r.Points[first + i].Y);
        switch (s.Now[id])
        {
            case Line: return new Line(P(0), P(1));
            case Polyline pl: return new Polyline(Enumerable.Range(0, count).Select(P).ToList(), pl.Closed);
            case Circle: return new Circle(P(0), r.Radii[e0]);
            case Arc was:
            {
                var c = P(0);
                var a0 = Math.Atan2(P(1).Y - c.Y, P(1).X - c.X);
                var a1 = Math.Atan2(P(2).Y - c.Y, P(2).X - c.X);
                var start = was.Start + Math.IEEERemainder(a0 - was.Start, 2 * Math.PI);
                return new Arc(c, r.Radii[e0], start, start + Geo.Sweep(a0, a1));
            }
        }
        return s.Now[id];
    }

    static bool Moved(Shape a, Shape b)
    {
        static bool Far(Vec2 p, Vec2 q) => Math.Abs(p.X - q.X) > 1e-9 || Math.Abs(p.Y - q.Y) > 1e-9;
        return (a, b) switch
        {
            (Line x, Line y) => Far(x.A, y.A) || Far(x.B, y.B),
            (Polyline x, Polyline y) => x.Pts.Count != y.Pts.Count || x.Pts.Zip(y.Pts).Any(z => Far(z.First, z.Second)),
            (Circle x, Circle y) => Far(x.C, y.C) || Math.Abs(x.R - y.R) > 1e-9,
            (Arc x, Arc y) => Far(x.C, y.C) || Math.Abs(x.R - y.R) > 1e-9 || Far(x.At(x.Start), y.At(y.Start)) || Far(x.At(x.End), y.At(y.End)),
            _ => true,
        };
    }

    /// <summary>The shapes the solve moved, as it leaves them.</summary>
    static List<(string Id, Shape Shape)> Changes(SolverSketch s, SolveResult r) =>
        s.Order.Select(id => (id, After(s, r, id))).Where(x => Moved(x.Item2, s.Now[x.id])).ToList();

    /// <summary>The solved shapes written back; marked as the solve's own, so the follow-up never takes them for a user's edit.</summary>
    static void WriteBack(ICommandContext ctx, SolverSketch s, SolveResult r)
    {
        foreach (var (id, shape) in Changes(s, r))
        {
            Drafts.Set(ctx, ctx.Model.Get(id)!, shape);
            Report(ctx).Solved[id] = Key(shape);
        }
    }

    /// <summary>The goals a user's edit sets: each point of an edited shape that it moved, at its new place.</summary>
    static List<DragGoal> GoalsOf(SolverSketch s, IReadOnlyDictionary<string, Shape> from)
    {
        var goals = new List<DragGoal>();
        foreach (var id in s.Order)
        {
            if (!from.TryGetValue(id, out var was) || !Pairs(was, s.Now[id])) continue;
            var (first, count, _) = s.Span[id];
            for (var i = 0; i < count; i++)
            {
                var part = s.PointParts[first + i].Part;
                if (PointOf(s.Now[id], part) is not { } to) continue;
                var p = s.Points[first + i];
                if (Math.Abs(p.X - to.X) > 1e-9 || Math.Abs(p.Y - to.Y) > 1e-9) goals.Add(new DragGoal(first + i, to.X, to.Y));
            }
        }
        return goals;
    }

    /// <summary>Why the goals could not be reached: the stored constraints that conflict with the dragged points held at their goals.</summary>
    static string Blocked(SolverSketch s, IReadOnlyList<DragGoal> goals, SolveResult r)
    {
        var miss = goals.First(g => Math.Abs(r.Points[g.Point].X - g.X) > GoalTol || Math.Abs(r.Points[g.Point].Y - g.Y) > GoalTol);
        var (shape, part) = s.PointParts[miss.Point];
        var sketch = s.ToSketch();
        var cs = sketch.Constraints.ToList();
        foreach (var g in goals)
        {
            cs.Add(SketchConstraint.DistanceXFromOrigin(g.Point, g.X));
            cs.Add(SketchConstraint.DistanceYFromOrigin(g.Point, g.Y));
        }
        SolveResult held;
        try { held = SketchSolver.Solve(sketch with { Constraints = cs }); }
        catch (ArgumentException) { held = r; }
        var by = held.Conflicting.Where(i => i < s.Constraints.Count).ToList();
        var what = $"„{s.Names[shape]}” {part}";
        return by.Count > 0
            ? $"The edit cannot be made: the sketch's constraints hold {what} ({Named(s, by)})."
            : $"The edit cannot be made: the sketch's constraints hold {what}.";
    }

    static bool Reached(IReadOnlyList<DragGoal> goals, SolveResult r) =>
        goals.All(g => Math.Abs(r.Points[g.Point].X - g.X) <= GoalTol && Math.Abs(r.Points[g.Point].Y - g.Y) <= GoalTol);

    /// <summary>
    /// A view's sketch solved inside the command: from its shapes as they are, or (a user's edit) from where the edited shapes were,
    /// their moved points as drag goals. A failure refuses the command, or, <paramref name="lenient"/> (a delete), is told in its result.
    /// </summary>
    static void Solve(ICommandContext ctx, string viewId, IReadOnlyList<JsonObject> list, IReadOnlyDictionary<string, Shape>? editedFrom, bool lenient)
    {
        var s = Build(ctx.Model, list, null, editedFrom, viewId);
        if (s.Constraints.Count == 0) return;
        var goals = editedFrom != null ? GoalsOf(s, editedFrom) : [];
        var r = Run(s, goals);
        if (!r.Succeeded)
        {
            if (!lenient) throw new CommandException(Failure(s, r) + " Nothing was changed.");
            Report(ctx).Sketch = new JsonObject
            {
                ["viewId"] = viewId, ["status"] = StatusName(r.Status),
                ["conflicting"] = new JsonArray(OwnersOf(s, r.Conflicting).Select(c => (JsonNode)(string)c["id"]!).ToArray()),
            };
            return;
        }
        if (!Reached(goals, r))
        {
            if (!lenient) throw new CommandException(Blocked(s, goals, r));
            return;
        }
        // A radius the user set that the constraints took back: not made either.
        if (editedFrom != null && !lenient)
            foreach (var id in s.Order.Where(editedFrom.ContainsKey))
                if (s.Now[id] is Circle or Arc && After(s, r, id) is var after && Math.Abs(Radius(after) - Radius(s.Now[id])) > GoalTol && Math.Abs(Radius(editedFrom[id]) - Radius(s.Now[id])) > 1e-9)
                    throw new CommandException($"The edit cannot be made: the sketch's constraints hold „{s.Names[id]}”'s radius.");
        WriteBack(ctx, s, r);
    }

    static double Radius(Shape s) => s switch { Circle c => c.R, Arc a => a.R, _ => 0 };

    static string StatusName(SolveStatus st) => st switch
    {
        SolveStatus.Solved => "solved",
        SolveStatus.Underconstrained => "underconstrained",
        SolveStatus.Redundant => "redundant",
        SolveStatus.Conflicting => "conflicting",
        _ => "notConverged",
    };

    /// <summary>A view's constraints that still hold together (the rest are dropped by <see cref="AfterChange"/>).</summary>
    static List<JsonObject> Sound(IModelView m, string viewId) => ListOf(CarrierOf(m, viewId)).Where(c => Invalid(m, c, viewId) == null).ToList();

    // ------------------------------------------------------------ the status, derived on every ask

    /// <summary>
    /// A sketch's state for the edit mode: <c>{status, dof, freeShapes: [{shape, part}], redundant, conflicting, dimensions: [{id,
    /// value, measured, driving}]}</c>, from a solve that writes nothing. <paramref name="scope"/>: the shapes asked about (a part's
    /// sketch) with every shape constraints tie them to; null: every drafted shape on the view, unconstrained ones counting their
    /// freedom too.
    /// </summary>
    public static JsonObject StatusOf(IModelView m, string viewId, IReadOnlyCollection<string>? scope = null)
    {
        var all = Sound(m, viewId);
        List<JsonObject> list;
        IEnumerable<string> extra;
        if (scope == null)
        {
            list = all;
            extra = Drafts.On(m, viewId).Where(e => e.Attrs["hatch"] == null).Select(e => e.Id);
        }
        else
        {
            // The constraints reaching the scope, and through them every shape they tie it to.
            var shapes = scope.ToHashSet();
            list = [];
            var left = all.ToList();
            for (var grew = true; grew;)
            {
                grew = false;
                foreach (var c in left.ToList())
                {
                    if (!Refs(c).Any(r => shapes.Contains(r.Shape))) continue;
                    list.Add(c);
                    left.Remove(c);
                    foreach (var x in Refs(c)) shapes.Add(x.Shape);
                    grew = true;
                }
            }
            extra = scope;
        }
        var s = Build(m, list, extra, null, viewId);
        SolveResult r;
        try { r = SketchSolver.Solve(s.ToSketch()); }
        catch (ArgumentException ex) { return new JsonObject { ["status"] = "invalid", ["problem"] = ex.Message }; }
        var free = new JsonArray();
        for (var p = 0; p < s.Points.Count; p++)
            if (r.PointIsFree[p]) free.Add((JsonNode)new JsonObject { ["shape"] = s.PointParts[p].Shape, ["part"] = s.PointParts[p].Part });
        for (var e = 0; e < s.Entities.Count; e++)
            if (r.EntityIsFree[e]) free.Add((JsonNode)new JsonObject { ["shape"] = s.EntityParts[e].Shape, ["part"] = s.EntityParts[e].Part });
        JsonArray IdsOf(IEnumerable<int> ix) => new(OwnersOf(s, ix).Select(c => (JsonNode)(string)c["id"]!).ToArray());
        Dictionary<string, double>? ps = null;
        var dims = new JsonArray();
        foreach (var c in s.Stored)
        {
            if (!ValueKinds.Contains((string?)c["kind"] ?? "")) continue;
            var o = new JsonObject { ["id"] = (string?)c["id"], ["kind"] = (string?)c["kind"] };
            if ((string?)c["formula"] != null || c["value"] != null)
            {
                try { o["value"] = Math.Round(Value(c, ps ??= ParamsOf(m, viewId)), 9); }
                catch (CommandException ex) { o["problem"] = ex.Message; }
            }
            else o["value"] = null;
            if (Measure(m, c) is { } mv) o["measured"] = Math.Round(mv, 9);
            o["driving"] = c["driving"] is not JsonValue dv || !dv.TryGetValue<bool>(out var d) || d;
            dims.Add((JsonNode)o);
        }
        return new JsonObject
        {
            ["viewId"] = viewId, ["status"] = StatusName(r.Status), ["dof"] = r.DegreesOfFreedom, ["freeShapes"] = free,
            ["redundant"] = IdsOf(r.Redundant), ["conflicting"] = IdsOf(r.Conflicting), ["dimensions"] = dims,
        };
    }

    // ------------------------------------------------------------ dragging

    /// <summary>A point of a sketch dragged to <paramref name="to"/>: the solver's system with that one goal, solved, or why it cannot be.</summary>
    static (SolverSketch S, SolveResult? R, string? Error) Drag(IModelView m, string viewId, string shape, string part, Vec2 to)
    {
        var s = Build(m, Sound(m, viewId), [shape], null, viewId);
        var p = s.PointAt(shape, part) ?? throw new CommandException($"„{part}” is not a point of the shape.");
        var goals = new List<DragGoal> { new(p, to.X, to.Y) };
        var r = Run(s, goals);
        if (!r.Succeeded) return (s, null, Failure(s, r));
        if (!Reached(goals, r)) return (s, null, Blocked(s, goals, r));
        return (s, r, null);
    }

    /// <summary>The drag's arguments checked: the view, the shape on it, the point part written the one way, where to.</summary>
    static (string View, string Shape, string Part, Vec2 To) DragArgs(IModelView m, string? viewId, string? shapeId, string? part, JsonNode? to)
    {
        var e = shapeId != null && m.Get(shapeId) is { Class: IfcClass.Annotation, PredefinedType: Drafts.Predefined } d && d.Attrs["hatch"] == null ? d
            : throw new CommandException("Drag which drafted line, polyline, circle or arc?");
        var shape = Drafts.ShapeOf(e) ?? throw new CommandException($"„{e.Name}” has no shape.");
        var view = viewId ?? e.Str("viewId") ?? "";
        if (e.Str("viewId") != view) throw new CommandException($"„{e.Name}” is not on that sketch.");
        var cl = Classify(shape, part ?? "");
        if (cl is not (RefKind.Point, var canonical)) throw new CommandException("Drag a point: start, end, center or vertex:i.");
        var at = Args.TryPoint(to) ?? throw new CommandException("Where to: [x, y].");
        return (view, e.Id, canonical, at);
    }

    static void RegisterSolve(IModuleRegistry r)
    {
        // {viewId} → {status, dof, freeShapes, redundant, conflicting, dimensions}: derived on every ask, never stored.
        r.Query("sketchStatus", (q, a) => StatusOf(q.Model, (string?)a["viewId"] ?? throw new CommandException("Which sketch: {viewId}?")));

        // {viewId, shape, part, to: [x, y]}: one point dragged, the rest of the sketch following its constraints; one undo step.
        r.Command("dragSketch", "Drag in sketch", (ctx, a) =>
        {
            var (view, shape, part, to) = DragArgs(ctx.Model, (string?)a["viewId"], (string?)a["shape"], (string?)a["part"], a["to"]);
            var (s, res, error) = Drag(ctx.Model, view, shape, part, to);
            if (error != null) throw new CommandException(error);
            var moved = Changes(s, res!).Select(x => (JsonNode)x.Id).ToArray();
            WriteBack(ctx, s, res!);
            return new JsonObject { ["ids"] = new JsonArray(moved) };
        });

        // The same, committing nothing: {ok, reason?, shapes: [{id, shape}]}, the shapes the drag would move, for the live preview.
        r.Query("dragPreview", (q, a) =>
        {
            var (view, shape, part, to) = DragArgs(q.Model, (string?)a["viewId"], (string?)a["shape"], (string?)a["part"], a["to"]);
            var (s, res, error) = Drag(q.Model, view, shape, part, to);
            if (error != null) return new JsonObject { ["ok"] = false, ["reason"] = error, ["shapes"] = new JsonArray() };
            return new JsonObject
            {
                ["ok"] = true,
                ["shapes"] = new JsonArray(Changes(s, res!).Select(x => (JsonNode)new JsonObject { ["id"] = x.Id, ["shape"] = x.Shape.ToJson() }).ToArray()),
            };
        });
    }
}
