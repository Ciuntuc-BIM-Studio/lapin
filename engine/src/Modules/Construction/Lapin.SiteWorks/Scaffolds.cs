using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.SiteWorks;

/// <summary>
/// Facade scaffolding (docs/plan-site-works.md, S4). A scaffold runs along a
/// facade line — a polyline drawn in plan, or the outer faces of walls,
/// followed as they are now — on one side of it, at its type's distance from
/// the wall. Each straight run is divided into the type's bay lengths (the
/// fewest pieces that reach its end), its lifts climb from the highest foot of
/// the run until the top deck is within reach of the work top (the walls'
/// tops, or the elevation given), the guardrail stands above it. Corners close
/// themselves: an outside corner's run goes on further, an inside corner's
/// stops short, by the width × tan(half the turn) (a right angle: one width);
/// a curved wall is followed in chords of at most 3 m. The feet stand on the ground (the terrain)
/// on base jacks, or on the storey. Its type (<c>LapinScaffoldType</c>) holds
/// the system; the scaffold holds where it is. A Lapin class exported as
/// IfcElementAssembly USERDEFINED "SCAFFOLD".
/// </summary>
public static class Scaffolds
{
    public const string Class = "LapinScaffold";
    public const string TypeClass = "LapinScaffoldType";
    public const string Color = "#8a8f98";
    const string DeckColor = "#b8925a";

    public static readonly string[] Systems = ["modular", "frame"];

    /// <summary>A generic system: ring-lock bays from 0,73 to 3,07 m, 0,73 m wide, 2 m lifts, 25 cm from the wall, anchored every 4 m on every second lift.</summary>
    public static JsonObject DefaultSpec() => new()
    {
        ["system"] = "modular", ["bays"] = new JsonArray(3.07, 2.57, 2.07, 1.57, 1.09, 0.73), ["width"] = 0.73, ["lift"] = 2.0, ["wallGap"] = 0.25,
        ["decks"] = "every", ["guardrail"] = 1.0, ["toeBoard"] = true, ["deckBoard"] = 0.32, ["anchorSpacing"] = 4.0, ["anchorEveryLifts"] = 2,
        ["reach"] = 1.5, ["jack"] = 0.4, ["accessEvery"] = 30.0, ["net"] = false, ["tube"] = 0.048,
    };

    public sealed record Spec(string System, double[] Bays, double Width, double Lift, double WallGap, bool EveryLift, double Guardrail, bool ToeBoard,
        double DeckBoard, double AnchorSpacing, int AnchorEveryLifts, double Reach, double Jack, double AccessEvery, bool Net, double Tube);

    public static Spec Read(JsonObject? o)
    {
        var s = Lay.Merge(DefaultSpec(), o);
        var system = Lay.Str(s, "system", "modular");
        return new(Systems.Contains(system) ? system : "modular", Lay.Nums(s, "bays", [3.07, 2.57, 2.07, 1.57, 1.09, 0.73]).Where(b => b >= 0.3).DefaultIfEmpty(2.07).ToArray(),
            Math.Max(0.3, Lay.Num(s, "width", 0.73)), Math.Max(0.5, Lay.Num(s, "lift", 2.0)), Math.Max(0, Lay.Num(s, "wallGap", 0.25)),
            Lay.Str(s, "decks", "every") != "top", Math.Max(0, Lay.Num(s, "guardrail", 1.0)), Lay.Bool(s, "toeBoard", true),
            Math.Max(0.1, Lay.Num(s, "deckBoard", 0.32)), Math.Max(0.5, Lay.Num(s, "anchorSpacing", 4.0)), Math.Max(1, (int)Math.Round(Lay.Num(s, "anchorEveryLifts", 2))),
            Math.Max(0, Lay.Num(s, "reach", 1.5)), Math.Max(0, Lay.Num(s, "jack", 0.4)), Math.Max(0, Lay.Num(s, "accessEvery", 30)), Lay.Bool(s, "net", false),
            Math.Clamp(Lay.Num(s, "tube", 0.048), 0.02, 0.2));
    }

    public static Entity? TypeOf(IModelView m, Entity e) => m.RelatingOf(e.Id, IfcClass.RelDefinesByType);

    public static Spec SpecOf(IModelView m, Entity e) => Read(TypeOf(m, e)?.Attrs["spec"] as JsonObject);

    /// <summary>A facade line with the scaffold on its left.</summary>
    public sealed record Line(IReadOnlyList<Vec2> Points, bool Closed);

    /// <summary>The walls a scaffold follows (those still there).</summary>
    public static List<Entity> WallsOf(IModelView m, Entity e) =>
        (e.Attrs["walls"] as JsonArray)?.Select(x => (string?)x).OfType<string>().Select(m.Get).OfType<Entity>().Where(w => w.Class == IfcClass.Wall).ToList() ?? [];

    public static IReadOnlyList<Vec2> Path(Entity e) =>
        e.Attrs["path"] is JsonArray arr ? arr.OfType<JsonArray>().Where(p => p.Count >= 2).Select(p => new Vec2(Json.Number(p[0]) ?? 0, Json.Number(p[1]) ?? 0)).ToList() : [];

    public static JsonArray PathJson(IEnumerable<Vec2> pts) => new(pts.Select(p => (JsonNode)new JsonArray(Math.Round(p.X, 6), Math.Round(p.Y, 6))).ToArray());

    /// <summary>The facade line: from the walls when it follows them, else its path (reversed when the scaffold is on its right).</summary>
    public static Line? FacadeOf(Entity e, IModelView m, IServices services)
    {
        var walls = WallsOf(m, e);
        if (walls.Count > 0 && FromWalls(walls, m, services) is { } fromWalls) return fromWalls;
        var path = Path(e).ToList();
        if (path.Count < 2) return null;
        var closed = e.Bool("closed") && path.Count >= 3;
        if (e.Str("side") == "right") path.Reverse();
        return new(path, closed);
    }

    /// <summary>
    /// The walls' outer faces as one line: each wall's face that looks away
    /// from the middle of all the project's walls (a curved wall's in chords of
    /// at most <see cref="Chord"/>), oriented with the outside on its left,
    /// chained end to start (the nearest next), the corners where the faces'
    /// lines meet; closed when the last comes back to the first.
    /// </summary>
    public static Line? FromWalls(IReadOnlyList<Entity> walls, IModelView m, IServices services)
    {
        var all = m.OfClass(IfcClass.Wall).Select(w => ElementFaces.Of(services, w, m)).Where(f => f.Count > 0).ToList();
        if (all.Count == 0) return null;
        var mids = all.SelectMany(f => f.Where(x => x.Role is "Face.Left" or "Face.Right")).Select(f => f.Centre.Xy).ToList();
        var middle = new Vec2(mids.Average(p => p.X), mids.Average(p => p.Y));
        var lines = new List<List<Vec2>>();
        foreach (var w in walls)
        {
            var faces = ElementFaces.Of(services, w, m).Where(f => f.Role is "Face.Left" or "Face.Right").ToList();
            if (faces.Count == 0) continue;
            var outer = faces.MaxBy(f => Vec2.Dot(new Vec2(f.Normal.X, f.Normal.Y), f.Centre.Xy - middle))!;
            var n = new Vec2(outer.Normal.X, outer.Normal.Y);
            List<Vec2> line;
            if (outer.Curve) line = Rechord(outer.Outline.Select(p => p.Xy).ToList(), Chord);
            else { var bottom = outer.Outline.OrderBy(p => p.Z).Take(2).Select(p => p.Xy).ToList(); line = [bottom[0], bottom[1]]; }
            // The outside on the left: judged at the middle of the line.
            var k = line.Count / 2 - (line.Count % 2 == 0 ? 1 : 0);
            if (Vec2.Dot((line[Math.Min(k + 1, line.Count - 1)] - line[k]).Perp, n) < 0) line.Reverse();
            lines.Add(line);
        }
        if (lines.Count == 0) return null;
        // Chain: start at a line no other line ends near, then always the nearest start.
        var left = lines.ToList();
        var first = left.FirstOrDefault(l => !left.Any(o => o != l && (o[^1] - l[0]).Length < 1.0)) ?? left[0];
        var chain = new List<List<Vec2>> { first };
        left.Remove(first);
        while (left.Count > 0)
        {
            var end = chain[^1][^1];
            var next = left.MinBy(l => (l[0] - end).Length)!;
            chain.Add(next);
            left.Remove(next);
        }
        var closed = chain.Count >= 2 && (chain[^1][^1] - chain[0][0]).Length < 1.0 && chain.Sum(c => c.Count - 1) >= 3;
        (Vec2, Vec2) Last(List<Vec2> l) => (l[^2], l[^1]);
        (Vec2, Vec2) First(List<Vec2> l) => (l[0], l[1]);
        var pts = new List<Vec2>();
        for (var i = 0; i < chain.Count; i++)
        {
            pts.Add(i == 0 ? (closed ? Meet(Last(chain[^1]), First(chain[0])) : chain[0][0]) : Meet(Last(chain[i - 1]), First(chain[i])));
            for (var j = 1; j < chain[i].Count - 1; j++) pts.Add(chain[i][j]);
        }
        if (!closed) pts.Add(chain[^1][^1]);
        return new(pts, closed);
    }

    /// <summary>The longest a chord of a curved facade may be: a curved wall is followed in straight runs no longer.</summary>
    public const double Chord = 3.0;

    /// <summary>A curve as the fewest equal chords of at most <paramref name="max"/>, from its start to its end.</summary>
    static List<Vec2> Rechord(List<Vec2> curve, double max)
    {
        var at = new List<double> { 0 };
        for (var i = 1; i < curve.Count; i++) at.Add(at[^1] + (curve[i] - curve[i - 1]).Length);
        var n = Math.Max(1, (int)Math.Ceiling(at[^1] / max - 1e-9));
        Vec2 P(double s)
        {
            for (var i = 1; i < curve.Count; i++)
                if (s <= at[i] + 1e-12) return curve[i - 1] + (curve[i] - curve[i - 1]) * ((s - at[i - 1]) / Math.Max(at[i] - at[i - 1], 1e-12));
            return curve[^1];
        }
        return Enumerable.Range(0, n + 1).Select(k => P(at[^1] * k / n)).ToList();
    }

    /// <summary>Where two lines meet (the second's start when they are parallel).</summary>
    static Vec2 Meet((Vec2 A, Vec2 B) l1, (Vec2 A, Vec2 B) l2)
    {
        var (r, s) = (l1.B - l1.A, l2.B - l2.A);
        var den = Vec2.Cross(r, s);
        if (Math.Abs(den) < 1e-9 * r.Length * s.Length) return l2.A;
        var t = Vec2.Cross(l2.A - l1.A, s) / den;
        return l1.A + r * t;
    }

    /// <summary>One straight run of the scaffold: along its inner line from A to B, the outside towards <see cref="Out"/>.</summary>
    public sealed record Run(Vec2 A, Vec2 B, Vec2 Dir, Vec2 Out, IReadOnlyList<double> Bays, double Base, int Lifts, double GuardTop, IReadOnlyList<double> Feet)
    {
        public double Length => (B - A).Length;
        public double Covered => Bays.Sum();
        /// <summary>The standards' positions along the run.</summary>
        public IReadOnlyList<double> Stations
        {
            get { var o = new List<double> { 0 }; foreach (var b in Bays) o.Add(o[^1] + b); return o; }
        }
    }

    public sealed record Layout(Spec Spec, IReadOnlyList<Run> Runs, double Top, IReadOnlyList<string> Problems, bool Closed)
    {
        public double Length => Runs.Sum(r => r.Covered);
        public int Lifts => Runs.Count == 0 ? 0 : Runs.Max(r => r.Lifts);
        public int DeckLevels(Run r) => Spec.EveryLift ? r.Lifts : Math.Min(1, r.Lifts);
        public IEnumerable<int> Decks(Run r) => Spec.EveryLift ? Enumerable.Range(1, r.Lifts) : r.Lifts > 0 ? [r.Lifts] : [];
    }

    /// <summary>
    /// The bays of a run: catalogue lengths whose sum reaches the run's length
    /// with the fewest pieces, standing out at the end by as little as that
    /// allows (30 cm out weighs as one piece more), the longest first.
    /// </summary>
    public static List<double> Bays(double length, double[] bays) => Lay.Cover(length, bays, 30);

    /// <summary>
    /// The scaffold laid out along a facade line: the inner line at the wall
    /// gap, its runs with their corners closed, their bays, their feet (from
    /// <paramref name="footAt"/>), their lifts up to the work top.
    /// </summary>
    public static Layout Plan(Spec s, Line facade, Func<Vec2, double> footAt, double top)
    {
        var problems = new List<string>();
        var pts = facade.Points;
        var segs = facade.Closed ? pts.Count : pts.Count - 1;
        var dirs = Enumerable.Range(0, segs).Select(i => (pts[(i + 1) % pts.Count] - pts[i]).Normalized()).ToList();
        if (segs < 1 || dirs.Any(d => d.Length < 0.5)) return new(s, [], top, ["The scaffold's line has a segment of no length."], facade.Closed);
        // The inner line: each segment moved out by the wall gap, the corners where the moved lines meet.
        (Vec2 A, Vec2 B) Moved(int i) => (pts[i] + dirs[i].Perp * s.WallGap, pts[(i + 1) % pts.Count] + dirs[i].Perp * s.WallGap);
        Vec2 Corner(int i) => !facade.Closed && i == 0 ? Moved(0).A : !facade.Closed && i == segs ? Moved(segs - 1).B : Meet(Moved((i - 1 + segs) % segs), Moved(i % segs));
        var runs = new List<Run>();
        for (var i = 0; i < segs; i++)
        {
            var (a, b) = (Corner(i), Corner(i + 1));
            var d = dirs[i];
            var o = d.Perp;
            var length = Vec2.Dot(b - a, d);
            // The corner at the end: outside (turning right, away from the scaffold) the run goes on, inside it stops short,
            // by its width × tan(half the turn): a right angle's width, nothing where it runs on straight.
            if (facade.Closed || i < segs - 1)
            {
                var next = dirs[(i + 1) % segs];
                var turn = Math.Atan2(Vec2.Cross(d, next), Vec2.Dot(d, next));
                var by = s.Width * Math.Tan(Math.Min(Math.Abs(turn), 2.6) / 2);
                if (turn < -1e-9) length += by;
                else if (turn > 1e-9) length -= by;
            }
            if (length < 0.3) { problems.Add($"Run {i + 1} is too short for a bay ({Lay.M(length)} m)."); continue; }
            var bays = Bays(length, s.Bays);
            var stations = new List<double> { 0 };
            foreach (var bay in bays) stations.Add(stations[^1] + bay);
            var feet = stations.SelectMany(t => new[] { footAt(a + d * t), footAt(a + d * t + o * s.Width) }).ToList();
            var @base = feet.Max();
            if (@base - feet.Min() > s.Jack + 1e-9) problems.Add($"Run {i + 1}: the ground falls {Lay.M(@base - feet.Min())} m along it, more than the base jacks take ({Lay.M(s.Jack)} m); a compensating lift is needed.");
            var lifts = Math.Max(1, (int)Math.Ceiling((top - s.Reach - @base) / s.Lift - 1e-9));
            if (top - @base < 0.5) problems.Add($"Run {i + 1}: the work top is not above its feet.");
            runs.Add(new(a, a + d * length, d, o, bays, @base, lifts, @base + lifts * s.Lift + s.Guardrail, feet));
        }
        return new(s, runs, top, problems, facade.Closed);
    }

    /// <summary>A scaffold's layout as the model is now.</summary>
    public static Layout? LayoutOf(Entity e, IModelView m, IServices services)
    {
        if (FacadeOf(e, m, services) is not { } facade) return null;
        var spec = SpecOf(m, e);
        var z0 = (Spatial.StoreyOf(m, e.Id)?.Num("elevation") ?? 0) + e.Num("baseOffset");
        var support = new Support(m, services);
        Func<Vec2, double> foot = e.Bool("onStorey") ? _ => z0 : p => support.Ground(p, z0);
        var walls = WallsOf(m, e);
        var top = e.NumOrNull("top")
            ?? (walls.Count > 0 ? walls.Select(w => ElementFaces.Find(services, w, m, "Top")?.Outline[0].Z).OfType<double>().DefaultIfEmpty(z0 + 6).Max() : z0 + e.Num("height", 6.0));
        return Plan(spec, facade, foot, top);
    }

    public static ElementGeometry? Geometry(Entity e, IModelView m, IServices services)
    {
        if (LayoutOf(e, m, services) is not { } layout) return new ElementGeometry([], Diagnostics: ["The scaffold has no line: two points at least, or the walls it follows."]);
        if (layout.Runs.Count == 0) return new ElementGeometry([], Diagnostics: layout.Problems.Count > 0 ? layout.Problems : ["The scaffold has no run."]);
        var s = layout.Spec;
        var t = s.Tube;
        var parts = new Parts();
        var plan = new List<PlanItem>();
        var snaps = new List<Vec2>();
        foreach (var r in layout.Runs)
        {
            var st = r.Stations;
            var (inner, outer) = (r.A, r.A + r.Out * s.Width);
            Vec2 I(double x) => inner + r.Dir * x;
            Vec2 O(double x) => outer + r.Dir * x;
            for (var k = 0; k < st.Count; k++)
            {
                foreach (var (p, foot) in new[] { (I(st[k]), r.Feet[2 * k]), (O(st[k]), r.Feet[2 * k + 1]) })
                {
                    parts.Add("standards", "Standards", Color, Prism.ExtrudeFootprint(Square(p, t), r.Base, r.GuardTop - r.Base));
                    parts.Add("jacks", "Base jacks", Color, Prism.ExtrudeFootprint(Square(p, 0.15), foot, 0.01));
                    if (r.Base - foot > 0.011) parts.Add("jacks", "Base jacks", Color, Prism.ExtrudeFootprint(Square(p, 0.038), foot + 0.01, r.Base - foot - 0.01));
                }
                plan.Add(new("outline", false, [I(st[k]), O(st[k])]));
            }
            var end = st[^1];
            plan.Add(new("outline", true, [I(0), I(end), O(end), O(0)]));
            snaps.AddRange([I(0), I(end), O(end), O(0)]);
            Vec3 At(Vec2 p, double z) => p.At(z);
            foreach (var k in layout.Decks(r))
            {
                var z = r.Base + k * s.Lift;
                parts.Add("ledgers", "Ledgers", Color, Prism.SweepBox(At(I(0), z - t / 2), At(I(end), z - t / 2), t, t));
                parts.Add("ledgers", "Ledgers", Color, Prism.SweepBox(At(O(0), z - t / 2), At(O(end), z - t / 2), t, t));
                parts.Add("guardrails", "Guardrails", Color, Prism.SweepBox(At(O(0), z + 0.5), At(O(end), z + 0.5), t, t));
                parts.Add("guardrails", "Guardrails", Color, Prism.SweepBox(At(O(0), z + Math.Max(0.6, s.Guardrail) - t / 2), At(O(end), z + Math.Max(0.6, s.Guardrail) - t / 2), t, t));
                if (s.ToeBoard) parts.Add("toe", "Toe boards", DeckColor, Prism.SweepBox(At(O(0) - r.Out * 0.03, z + 0.075), At(O(end) - r.Out * 0.03, z + 0.075), 0.03, 0.15));
                for (var b = 0; b < r.Bays.Count; b++)
                    parts.Add("decks", "Decks", DeckColor, Prism.ExtrudeFootprint([I(st[b] + 0.01), I(st[b + 1] - 0.01), O(st[b + 1] - 0.01), O(st[b] + 0.01)], z - 0.04, 0.04));
                foreach (var x in st)
                    parts.Add("transoms", "Transoms", Color, Prism.SweepBox(At(I(x), z - t / 2), At(O(x), z - t / 2), t, t));
            }
            // A diagonal in every fifth bay on the outer face, in every lift.
            for (var k = 0; k < r.Lifts; k++)
                for (var b = 0; b < r.Bays.Count; b += 5)
                    parts.Add("diagonals", "Diagonals", Color, Prism.SweepBox(At(O(st[b]), r.Base + k * s.Lift + t), At(O(st[b + 1]), r.Base + (k + 1) * s.Lift - t), t, t));
            foreach (var (x, z) in Anchors(layout, r))
                parts.Add("anchors", "Anchors", "#d0a020", Prism.SweepBox(At(I(x), z), At(I(x) - r.Out * s.WallGap, z), 0.02, 0.02));
            if (s.Net) parts.Add("net", "Protective net", "#4f8f5f", Prism.SweepBox(At(O(0) + r.Out * 0.05, (r.Base + r.GuardTop) / 2), At(O(end) + r.Out * 0.05, (r.Base + r.GuardTop) / 2), 0.005, r.GuardTop - r.Base));
        }
        var g = new ElementGeometry(parts.Build(), plan, snaps, layout.Problems.Count > 0 ? layout.Problems : null);
        Built.AddOrUpdate(g, layout);
        return g;
    }

    /// <summary>The layout each built geometry was made from: what its quantities count (they are asked for with the geometry, without the services).</summary>
    static readonly ConditionalWeakTable<ElementGeometry, Layout> Built = new();

    static Vec2[] Square(Vec2 c, double size) { var h = size / 2; return [new(c.X - h, c.Y - h), new(c.X + h, c.Y - h), new(c.X + h, c.Y + h), new(c.X - h, c.Y + h)]; }

    /// <summary>The ties into the wall: on every n-th lift, along the run no further apart than the anchor spacing (at standards).</summary>
    public static List<(double X, double Z)> Anchors(Layout l, Run r)
    {
        var s = l.Spec;
        var st = r.Stations;
        var at = Lay.Spread(0, st[^1], s.AnchorSpacing, 0).Select(x => st.MinBy(p => Math.Abs(p - x))).Distinct().ToList();
        var o = new List<(double, double)>();
        for (var k = s.AnchorEveryLifts; k <= r.Lifts; k += s.AnchorEveryLifts)
            foreach (var x in at) o.Add((x, r.Base + k * s.Lift - 0.3));
        return o;
    }

    public sealed record Counts(double Length, double Area, double Height, int Bays, int Lifts, int Standards, double StandardLength, int Frames, int Ledgers, int Transoms,
        int Guardrails, int DeckBoards, int ToeBoards, int Diagonals, int BaseJacks, int Anchors, int AccessBays, double NetArea, IReadOnlyDictionary<double, int> BaysByLength);

    /// <summary>What the scaffold is made of and measures, run by run.</summary>
    public static Counts Count(Layout l)
    {
        var s = l.Spec;
        int bays = 0, standards = 0, frames = 0, ledgers = 0, transoms = 0, guard = 0, decks = 0, toe = 0, diag = 0, jacks = 0, anchors = 0;
        double standardLength = 0, area = 0, net = 0;
        var byLength = new SortedDictionary<double, int>();
        var boards = (int)Math.Ceiling(s.Width / s.DeckBoard - 1e-9);
        var modular = s.System == "modular";
        var openEnds = l.Closed ? 0 : 2;
        foreach (var r in l.Runs)
        {
            var p = r.Bays.Count + 1;
            var levels = l.DeckLevels(r);
            bays += r.Bays.Count;
            foreach (var b in r.Bays) byLength[b] = byLength.GetValueOrDefault(b) + 1;
            jacks += 2 * p;
            standardLength += r.Feet.Sum(f => r.GuardTop - f);
            if (modular)
            {
                standards += 2 * p;
                ledgers += 2 * r.Bays.Count * (levels + 1);
                transoms += p * (levels + 1);
            }
            else frames += p * r.Lifts;
            guard += 2 * r.Bays.Count * levels;
            decks += r.Bays.Count * levels * boards;
            if (s.ToeBoard) toe += r.Bays.Count * levels;
            diag += r.Lifts * (int)Math.Ceiling(r.Bays.Count / 5.0);
            anchors += Anchors(l, r).Count;
            var mean = r.Feet.Average();
            area += r.Covered * (r.GuardTop - mean);
            if (s.Net) net += r.Covered * (r.GuardTop - mean);
        }
        // The open ends' guardrails and toe boards, across.
        var maxLevels = l.Runs.Count == 0 ? 0 : l.Runs.Max(l.DeckLevels);
        guard += openEnds * 2 * maxLevels;
        if (s.ToeBoard) toe += openEnds * maxLevels;
        var length = l.Length;
        var access = s.AccessEvery > 0 && length > 0 ? Math.Max(1, (int)Math.Ceiling(length / s.AccessEvery - 1e-9)) : 0;
        var height = l.Runs.Count == 0 ? 0 : l.Runs.Max(r => r.GuardTop) - l.Runs.Min(r => r.Feet.Min());
        return new(length, area, height, bays, l.Lifts, standards, standardLength, frames, ledgers, transoms, guard, decks, toe, diag, jacks, anchors, access, net, byLength);
    }

    public static IEnumerable<QuantitySet> Quantities(Entity e, ElementGeometry? g, IModelView m)
    {
        if (g == null || !Built.TryGetValue(g, out var l) || l.Runs.Count == 0) yield break;
        var c = Count(l);
        var q = new List<Quantity>
        {
            new("Length", c.Length, "LENGTH"), new("Height", c.Height, "LENGTH"), new("Area", c.Area, "AREA", "Scaffold area"),
            new("Bays", c.Bays, "COUNT"), new("Lifts", c.Lifts, "COUNT"),
        };
        if (l.Spec.System == "modular") q.AddRange([new("Standards", c.Standards, "COUNT"), new("StandardLength", c.StandardLength, "LENGTH", "Standards (length)"), new("Ledgers", c.Ledgers, "COUNT"), new("Transoms", c.Transoms, "COUNT")]);
        else q.Add(new("Frames", c.Frames, "COUNT"));
        q.AddRange([
            new("Guardrails", c.Guardrails, "COUNT"), new("DeckBoards", c.DeckBoards, "COUNT", "Deck boards"), new("ToeBoards", c.ToeBoards, "COUNT", "Toe boards"),
            new("Diagonals", c.Diagonals, "COUNT"), new("BaseJacks", c.BaseJacks, "COUNT", "Base jacks"), new("Anchors", c.Anchors, "COUNT"), new("AccessBays", c.AccessBays, "COUNT", "Access bays"),
        ]);
        if (c.NetArea > 0) q.Add(new("NetArea", c.NetArea, "AREA", "Protective net"));
        foreach (var (len, n) in c.BaysByLength) q.Add(new($"Bays{Math.Round(len * 100)}", n, "COUNT", $"Bays {Lay.M(len)} m"));
        yield return new QuantitySet("Lapin_ScaffoldQuantities", q);
    }

    /// <summary>The configurator's elevation: a straight run of the length given on level ground, up to the height given.</summary>
    public static JsonObject Preview(JsonObject? spec, double length, double height)
    {
        var s = Read(spec);
        var l = Plan(s, new Line([new(0, 0), new(Math.Max(0.5, length), 0)], false), _ => 0, Math.Max(1, height));
        var o = new JsonObject { ["problems"] = new JsonArray(l.Problems.Select(p => (JsonNode)p).ToArray()) };
        if (l.Runs.Count == 0) return o;
        var r = l.Runs[0];
        var c = Count(l);
        o["bays"] = new JsonArray(r.Bays.Select(b => (JsonNode)b).ToArray());
        o["decks"] = new JsonArray(l.Decks(r).Select(k => (JsonNode)(k * s.Lift)).ToArray());
        o["lifts"] = r.Lifts;
        o["lift"] = s.Lift;
        o["guardTop"] = r.GuardTop;
        o["anchors"] = new JsonArray(Anchors(l, r).Select(a => (JsonNode)new JsonArray(a.X, a.Z)).ToArray());
        o["area"] = Math.Round(c.Area, 3);
        o["counts"] = new JsonObject
        {
            ["bays"] = c.Bays, ["standards"] = c.Standards, ["frames"] = c.Frames, ["ledgers"] = c.Ledgers, ["transoms"] = c.Transoms, ["guardrails"] = c.Guardrails,
            ["deckBoards"] = c.DeckBoards, ["toeBoards"] = c.ToeBoards, ["diagonals"] = c.Diagonals, ["baseJacks"] = c.BaseJacks, ["anchors"] = c.Anchors, ["accessBays"] = c.AccessBays,
        };
        return o;
    }
}
