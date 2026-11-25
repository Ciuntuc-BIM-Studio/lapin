using Lapin.Geometry;

namespace Lapin.RoofPanels;

/// <summary>
/// What a roof's panels need besides the panels (docs/plan-roof-panels.md §2.4, stage S3), the Panotaj Bilka app's
/// (deliver.export_data's stats, estimate.make_estimate's rows) rewritten on the layout: the battens or boards across each
/// slope on the profile's module, the counter-battens on the native rafters (their real length), the membrane with its laps,
/// the fixings of the support (nails or screws) and of the sheet (stitching and fixing screws, a standing seam's clips), and
/// the accessories along the edges, each as the profile's data says. Worked out on every ask, never stored.
/// </summary>
public static class Takeoff
{
    /// <summary>What the roof itself gives the takeoff: its snow guards' length, its rafters' (null: no framing, estimated), their spacing, its roof windows.</summary>
    public sealed record RoofData(double? Snow, double? RafterLength, double RafterSpacing, int Windows);

    /// <summary>
    /// The roof's options (its <c>panels</c>): the support scheme, the ridge caps, the support's fixings; null takes the profile's
    /// default. Stage S5: the rainwater system (<c>125/90</c>, <c>150/100</c>; null: the recommended one), the downpipes (null:
    /// the least the sizing asks) and the rain's intensity (l/s·m²; null: the default 0.05).
    /// </summary>
    public sealed record Options(string? Support, string? Ridge, string? Fixing, string? System = null, int? Downpipes = null, double? Rain = null);

    /// <summary>
    /// A line of the takeoff: its code, name, group (<c>support</c>, <c>fixings</c>, <c>flashings</c>), how many in its unit, what that
    /// comes from (<see cref="Need"/> in <see cref="NeedUnit"/>); <see cref="MadeFrom"/>: <c>flatSheet</c> when it is cut from flat sheet, not bought (S4).
    /// </summary>
    public sealed record Item(string Code, string Name, string Group, double Quantity, string Unit, double? Need, string? NeedUnit, string Note, string? MadeFrom = null);

    /// <summary>A slope's share: its area less its holes, the membrane's laps on it, its battens (m, rows), its valley battens.</summary>
    public sealed record FaceTake(string Id, double Area, double MembraneLaps, double Battens, int Rows, int ValleyBattens);

    /// <summary>
    /// The takeoff: the choices made, each slope's share, the app's stats (the numbers its estimate counts from) and the lines.
    /// <see cref="Stats"/> keys are the app's (<c>batten</c>, <c>counterBatten</c>, <c>foilOverlap</c>, <c>edgeScrews</c>…).
    /// </summary>
    public sealed record Result(SupportScheme? Support, string? Ridge, string Fixing, List<FaceTake> Faces, SortedDictionary<string, double> Stats, List<Item> Items, bool RaftersEstimated);

    static double Round6(double v) => Math.Round(v, 6);

    /// <summary>numpy.arange(start, stop, step): start + k step while k below ceil((stop − start) / step).</summary>
    static IEnumerable<double> Arange(double start, double stop, double step)
    {
        var n = (int)Math.Ceiling((stop - start) / step);
        for (var k = 0; k < n; k++) yield return start + k * step;
    }

    /// <summary>Where a line across the slope at height v crosses the polygon (deliver.export_data's batten intervals).</summary>
    static List<(double From, double To)> Intervals(IReadOnlyList<Vec2> poly, double v)
    {
        var xs = new List<double>();
        foreach (var (a, b) in Clip.Edges(poly))
        {
            if (Math.Abs(a.Y - b.Y) < 1e-6 && Math.Abs(v - a.Y) < 1e-5) { xs.Add(a.X); xs.Add(b.X); }
            else if (Math.Min(a.Y, b.Y) <= v && v < Math.Max(a.Y, b.Y)) xs.Add(a.X + (b.X - a.X) * (v - a.Y) / (b.Y - a.Y));
        }
        if (xs.Count < 2) return [];
        var s = xs.Select(x => Math.Round(x, 8)).Distinct().Order().ToList();
        if (s.Count < 2) return [];
        if (s.Count % 2 == 1) return [(s[0], s[^1])];
        var o = new List<(double, double)>();
        for (var i = 0; i + 1 < s.Count; i += 2) o.Add((s[i], s[i + 1]));
        return o;
    }

    /// <summary>The intervals less the holes' at the same height.</summary>
    static List<(double From, double To)> Minus(List<(double From, double To)> spans, IEnumerable<List<(double From, double To)>> holes)
    {
        foreach (var cut in holes.SelectMany(h => h))
        {
            var next = new List<(double, double)>();
            foreach (var (a, b) in spans)
            {
                if (cut.To <= a || cut.From >= b) { next.Add((a, b)); continue; }
                if (cut.From > a) next.Add((a, cut.From));
                if (cut.To < b) next.Add((cut.To, b));
            }
            spans = next;
        }
        return spans;
    }

    /// <summary>The heights up the slope of the battens or boards of a face <paramref name="h"/> m high.</summary>
    static IEnumerable<double> Levels(SupportScheme s, RoofFace f, double h)
    {
        if (s.Second is { } second)
        {
            var (a, b, step) = (s.First / 1000.0, second / 1000.0, s.Spacing / 1000.0);
            return [a, b, .. Arange(b + step, h, step)];
        }
        return Arange(s.First / 1000.0, h, s.SpacingAt(f.PitchDegrees));
    }

    public static Result Of(PanelProfile p, IReadOnlyList<RoofFace> faces, IReadOnlyList<RoofEdge> edges, PanelLayout.Result layout, RoofData roof, Options options)
    {
        var shared = PanelProfiles.Shared;
        var support = p.Support.FirstOrDefault(s => s.Id == options.Support) ?? p.Support.FirstOrDefault();
        var ridge = p.Ridges.Contains(options.Ridge ?? "") ? options.Ridge : p.Ridges.FirstOrDefault();
        var fixing = shared.WoodFixings.FirstOrDefault(w => w.Id == options.Fixing) is { Id.Length: > 0 } w0 ? w0 : shared.WoodFixings.FirstOrDefault();
        var stats = new SortedDictionary<string, double>(StringComparer.Ordinal);

        // Each slope: battens (or boards) across it, the membrane's laps, the valley battens, a standing seam's board fixings.
        var perFace = new List<FaceTake>();
        double batten = 0, foil = 0;
        int valleyBatten = 0, field = 0;
        var strip = (shared.MembraneWidth - shared.MembraneLap) / 1000.0;
        var lap = shared.MembraneLap / 1000.0;
        foreach (var f in faces)
        {
            var h = Clip.MaxY(f.Poly);
            double mine = 0;
            var rows = 0;
            var valleys = 0;
            var panels = layout.Panels.Where(x => x.Face == f).ToList();
            var myValleys = edges.Where(e => e.Kind == EdgeKinds.Valley && e.Faces[0] == f.Id).Select(e => (Lo: Math.Min(f.Uv(e.A).Y, f.Uv(e.B).Y), Hi: Math.Max(f.Uv(e.A).Y, f.Uv(e.B).Y))).ToList();
            if (support != null)
                foreach (var v in Levels(support, f, h))
                {
                    var spans = Minus(Intervals(f.Poly, v), f.Holes.Select(x => Intervals(x.Ring, v)));
                    if (spans.Count > 0)
                    {
                        mine += spans.Sum(s => s.To - s.From);
                        rows++;
                        if (p.Fixings.Any(x => x.Rule == "seamClips"))
                            field += panels.Count(x => spans.Any(s => s.From - .003 <= x.X && x.X <= s.To + .003) && Clip.MinY(x.Poly) - .001 <= v && v <= Clip.MaxY(x.Poly) + .001);
                    }
                    valleys += myValleys.Count(e => e.Lo - 1e-5 <= v && v <= e.Hi + 1e-5);
                }
            double laps = 0;
            foreach (var v in Arange(strip, h, strip)) laps += Clip.Area(Clip.Strip(f.Poly, -100, 100, v, v + lap));
            batten += mine;
            foil += laps;
            valleyBatten += valleys;
            perFace.Add(new FaceTake(f.Id, Clip.Area(f.Poly) - f.Holes.Sum(x => x.Area), laps, mine, rows, valleys));
        }
        var roofArea = perFace.Sum(x => x.Area);
        var netCover = layout.Panels.Sum(x => Clip.Area(x.Net));

        // The joints and caps (export_data): hips meeting an eave, ridge nodes, ridge ends at a verge.
        static bool Same(Vec3 a, Vec3 b) => (a - b).Length < 1e-6;
        var perimeter = edges.Where(e => e.Kind == EdgeKinds.Eave).SelectMany(e => new[] { e.A, e.B }).ToList();
        bool OnEave(Vec3 q) => perimeter.Any(x => Same(x, q));
        var hips = edges.Where(e => e.Kind == EdgeKinds.Hip).ToList();
        var hipCaps = hips.Count(e => OnEave(e.A) || OnEave(e.B));
        var joints = new List<(Vec3 P, int N)>();
        foreach (var e in edges.Where(e => e.Kind is EdgeKinds.Ridge or EdgeKinds.Hip))
            foreach (var q in new[] { e.A, e.B })
            {
                if (OnEave(q)) continue;
                var i = joints.FindIndex(j => Same(j.P, q));
                if (i < 0) joints.Add((q, 1)); else joints[i] = (joints[i].P, joints[i].N + 1);
            }
        var gableNodes = edges.Where(e => e.Kind == EdgeKinds.Verge).SelectMany(e => new[] { e.A, e.B }).ToList();
        var threeWay = joints.Count(j => j.N >= 3);
        var valleyHeads = edges.Count(e => e.Kind == EdgeKinds.Valley);
        var gableCaps = joints.Count(j => j.N == 1 && gableNodes.Any(g => Same(g, j.P)));
        var transverse = layout.Panels.Count(x => x.Upper);
        var edgeScrews = edges.Where(e => e.Kind == EdgeKinds.Eave).Sum(e => (int)Math.Ceiling(Round6(e.Length) / .25) + 1);
        double Run(string kind) => edges.Where(e => e.Kind == kind).Sum(e => Round6(e.Length));

        // A tile's side laps stitched where each crosses a module (export_data's stitches).
        var stitches = 0;
        if (p.Fixings.Any(x => x.Rule == "stitches"))
            foreach (var f in faces)
            {
                var starts = layout.Panels.Where(x => x.Face == f).Select(x => x.X).Distinct().Order().ToList();
                foreach (var x in starts.Skip(1))
                {
                    var seam = Clip.Strip(f.Target, x - .00001, x + .00001);
                    if (seam.Count > 2)
                        stitches += Math.Max(0, (int)Math.Floor((Clip.MaxY(seam) - .018) / p.Pitch) - (int)Math.Ceiling((Clip.MinY(seam) - .018) / p.Pitch) + 1);
                }
            }
        var estimated = roof.RafterLength == null;
        var spacing = estimated ? .60 : roof.RafterSpacing;
        var counter = roof.RafterLength ?? roofArea / spacing;
        var snow = roof.Snow ?? 0;

        stats["roofArea"] = roofArea; stats["netCover"] = netCover; stats["batten"] = batten; stats["counterBatten"] = counter;
        stats["rafterSpacing"] = spacing; stats["foilOverlap"] = foil; stats["valleyBatten"] = valleyBatten; stats["hipCaps"] = hipCaps;
        stats["threeWay"] = threeWay; stats["valleyHeads"] = valleyHeads; stats["gableCaps"] = gableCaps; stats["snow"] = snow;
        stats["edgeScrews"] = edgeScrews; stats["shortScrews"] = stitches; stats["retroFasteners"] = field; stats["transverseJoints"] = transverse;
        stats["valleyClips"] = p.Staggered ? 2 * valleyBatten : 0; stats["windows"] = roof.Windows;

        var items = new List<Item>();
        void Add(string code, string name, string group, double qty, string unit, double? need, string? needUnit, string note, string? madeFrom = null)
        {
            if (qty > 0) items.Add(new Item(code, name, group, qty, unit, need, needUnit, note, madeFrom));
        }
        static int Pack(double n, double size) => (int)Math.Ceiling(n / size);

        // The support: membrane, its tape, battens or boards, counter-battens (estimate.make_estimate).
        var membrane = (roofArea + foil) * shared.MembraneReserve;
        Add(shared.MembraneCode, shared.MembraneName, "support", Math.Ceiling(membrane / shared.MembraneRoll), shared.MembraneUnit, membrane, "m²",
            $"Strips lapped {shared.MembraneLap} mm, {Math.Round((shared.MembraneReserve - 1) * 100)} % for cuts and abutments.");
        var tape = (int)Math.Ceiling(foil / lap);
        Add(shared.TapeCode, shared.TapeName, "support", Pack(tape, shared.TapeRoll), shared.TapeUnit, tape, "m", "Along the membrane's laps across the slope.");
        if (support != null)
        {
            var need = Math.Ceiling(batten * support.Reserve);
            Add(support.Code, support.Name, "support", Pack(need, support.Bar / 1000.0), $"{(support.Kind == "boards" ? "board" : "bar")} {support.Bar / 1000.0:0.#} m", need, "m", support.Note);
        }
        var cb = shared.CounterBatten;
        Add(cb.Code, cb.Name, "support", Pack(counter * cb.Reserve, cb.Bar / 1000.0), $"bar {cb.Bar / 1000.0:0.#} m", counter * cb.Reserve, "m",
            estimated ? "The roof has no rafters: estimated at 600 mm spacing." : cb.Note);

        // The support's fixings: a fixing where a batten crosses a rafter, and along the counter-battens.
        var woodCount = (int)Math.Ceiling((Math.Ceiling(batten / spacing) + Math.Ceiling(counter / (shared.CounterFixing / 1000.0))) * shared.FixingReserve);
        Add(fixing.Code, fixing.Name, "fixings", Pack(woodCount, fixing.Pack), fixing.Unit, woodCount, "pcs", "Battens to the rafters and the counter-battens, 10 % to spare.");

        // The sheet's fixings.
        var ridgeRun = Run(EdgeKinds.Ridge);
        var hipRun = Run(EdgeKinds.Hip);
        foreach (var x in p.Fixings)
        {
            var edgeStep = x.EdgeSpacing / 1000.0;
            double count = x.Rule switch
            {
                "stitches" => stitches,
                "tileFixings" => netCover * x.PerM2 + edgeScrews + Math.Ceiling(2 * (ridgeRun + hipRun) / (p.Width / Math.Max(1, p.Waves))) + 2 * valleyBatten
                    + Math.Ceiling(2 * snow / (p.Width / Math.Max(1, p.Waves)))
                    + Math.Ceiling(2 * new[] { EdgeKinds.Verge, EdgeKinds.TopEdge, EdgeKinds.WallSide, EdgeKinds.WallTop, EdgeKinds.ConvexBreak, EdgeKinds.ConcaveBreak }.Sum(Run) / edgeStep),
                "seamClips" => field + edgeScrews + Math.Ceiling(2 * SeamEdges() / edgeStep) + 3 * transverse,
                "seamFlashings" => 2 * SeamEdges() / edgeStep + 2 * (Run(EdgeKinds.Verge) + Run(EdgeKinds.WallSide)) / edgeStep + 2 * snow / .50,
                _ => 0,
            };
            var pieces = x.RoundTo > 1 ? Math.Ceiling(count * x.Reserve / x.RoundTo) * x.RoundTo : Math.Ceiling(count * x.Reserve);
            Add(x.Code, x.Name, "fixings", Pack(pieces, x.Pack), x.Unit, pieces, "pcs", x.Note);
        }
        double SeamEdges() => new[] { EdgeKinds.Ridge, EdgeKinds.Hip, EdgeKinds.TopEdge, EdgeKinds.WallTop, EdgeKinds.ConvexBreak, EdgeKinds.ConcaveBreak, EdgeKinds.Valley }.Sum(Run);

        // The accessories, as the profile's data counts them.
        var counts = new Dictionary<string, double>
        {
            ["hipCaps"] = hipCaps, ["gableCaps"] = gableCaps, ["ridgeNodes"] = threeWay + valleyHeads, ["valleyClips"] = stats["valleyClips"], ["windows"] = roof.Windows,
        };
        foreach (var a in p.Accessories)
        {
            if (a.Option != null && a.Option != ridge) continue;
            if (a.Snow > 0 && roof.Snow == null) continue;
            var stock = a.Length / 1000.0;
            double qty, need = 0;
            switch (a.Rule)
            {
                case "edges":
                    qty = 0;
                    foreach (var (kind, weight) in a.Edges)
                        foreach (var e in edges.Where(e => e.Kind == kind))
                        {
                            qty += weight * RoofPanels.PieceCount(e.Length, stock, (a.Lap ?? (int)Math.Round(p.AccessoryLap(kind) * 1000)) / 1000.0);
                            need += weight * Round6(e.Length);
                        }
                    if (a.JointBars) qty += Math.Ceiling(transverse / Math.Max(1.0, Math.Floor((a.Length + 5.0) / (p.UsefulWidth + 5.0))));
                    Add(a.Code, a.Name, "flashings", qty, a.Unit, need, "m", a.Note, a.MadeFrom);
                    break;
                case "run":
                case "perLength":
                    need = a.Edges.Sum(kv => kv.Value * Run(kv.Key)) + a.Joints * p.Width * transverse + a.Snow * snow;
                    var total = need * a.Factor;
                    qty = a.Rule == "run" ? RoofPanels.PieceCount(total, stock, (a.Lap ?? 0) / 1000.0) : Math.Ceiling(total / stock);
                    Add(a.Code, a.Name, "flashings", qty, a.Unit, need, "m", a.Note, a.MadeFrom);
                    break;
                case "count":
                    Add(a.Code, a.Name, "flashings", counts.GetValueOrDefault(a.Of ?? ""), a.Unit, null, null, a.Note, a.MadeFrom);
                    break;
            }
        }
        return new Result(support, ridge, fixing.Id, perFace, stats, items, estimated);
    }
}
