using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Ifc;

/// <summary>
/// How recognition is checked against the file (docs/spec-ifc.md, Poarta de fidelitate): <see cref="Mode"/> "enforce" (the
/// default since decision 364: every recognised element measured against the file's own body, one that differs by more than
/// the tolerances comes in as the reference it would have been), "report" (measured, the import unchanged) or "off". <see cref="Mm"/>: the most
/// the bounding box and the base outline may be off, in millimetres; <see cref="Volume"/>: the most the volume may be off,
/// relative (0.005 is 0.5 %). <see cref="Joins"/>: an element that differs only in plan where it joins others (its end run on
/// into the wall it meets, or mitred where the file butts) passes when it and the elements it joins keep their volume together.
/// </summary>
public sealed record FidelityOptions(string Mode = FidelityOptions.Enforce, double Mm = 1.0, double Volume = 0.005, bool Joins = true)
{
    public const string Off = "off", Report = "report", Enforce = "enforce";
    public static readonly FidelityOptions Default = new();

    public bool On => Mode != Off;
    public bool Enforcing => Mode == Enforce;

    /// <summary>A mode as the user typed it: "off" and "report" as such, anything else the default enforce.</summary>
    public static string ModeOf(string? text) => text?.Trim().ToLowerInvariant() switch { Off => Off, Report => Report, _ => Enforce };
}

/// <summary>
/// One recognised element measured against the file's body: the native element's solid (as the views build it, its
/// openings cut) and the body the file gives it (its openings cut, as the reference would have been). <see cref="Volume"/>:
/// (native − file) / file, the volumes themselves in m³; <see cref="BoxMm"/>: the largest difference of the two bounding
/// boxes' sides, at <see cref="BoxSide"/> (top, base, plan); <see cref="OutlineMm"/>: the Hausdorff distance between the two
/// base outlines in plan. <see cref="JoinedTo"/>: the elements it is joined to (IfcRelConnectsPathElements);
/// <see cref="ByJoin"/>: it differs alone, in plan at its joins, and passes with them (<see cref="FidelityOptions.Joins"/>).
/// Not <see cref="Measured"/> when there was nothing to compare (no native body, no file body, an element absorbed into
/// another), with why in <see cref="Reason"/>.
/// </summary>
public sealed record FidelityCheck(string Id, string IfcClass, string Class, string? Name, bool Measured, bool Pass,
    double? Volume, double? BoxMm, string? BoxSide, double? OutlineMm, string? Reason, string? Cause, string Source = "ifc", bool Reverted = false,
    double? FileVolume = null, double? NativeVolume = null, IReadOnlyList<string>? JoinedTo = null, bool InPlanOnly = false, bool ByJoin = false)
{
    public JsonObject ToJson()
    {
        var o = new JsonObject { ["id"] = Id, ["ifcClass"] = IfcClass, ["class"] = Class, ["name"] = Name, ["measured"] = Measured, ["pass"] = Pass };
        if (Volume is { } v) o["volume"] = Math.Round(v, 6);
        if (FileVolume is { } fv) o["fileM3"] = Math.Round(fv, 6);
        if (NativeVolume is { } nv) o["nativeM3"] = Math.Round(nv, 6);
        if (BoxMm is { } b) o["boxMm"] = Math.Round(b, 2);
        if (BoxSide != null) o["boxSide"] = BoxSide;
        if (OutlineMm is { } l) o["outlineMm"] = Math.Round(l, 2);
        if (Reason != null) o["reason"] = Reason;
        if (Cause != null) o["cause"] = Cause;
        if (JoinedTo is { Count: > 0 } j) o["joinedTo"] = new JsonArray([.. j.Select(x => (JsonNode)x)]);
        if (ByJoin) o["byJoin"] = true;
        if (Source != "ifc") o["source"] = Source;
        if (Reverted) o["reverted"] = true;
        return o;
    }
}

/// <summary>What the gate found for one import: every check, how many were made references again (enforce), and what it cost.</summary>
public sealed record FidelityReport(FidelityOptions Options, IReadOnlyList<FidelityCheck> Checks, int Reverted, double Milliseconds)
{
    public int Measured => Checks.Count(c => c.Measured);
    public int Failed => Checks.Count(c => c.Measured && !c.Pass);
    public int ByJoin => Checks.Count(c => c.Measured && c.ByJoin);

    /// <summary>Two reports as one (the file's elements, then those recognised from their triangles).</summary>
    public FidelityReport Then(FidelityReport? more) => more == null ? this
        : new(Options, [.. Checks, .. more.Checks], Reverted + more.Reverted, Milliseconds + more.Milliseconds);

    public JsonObject ToJson()
    {
        var byClass = new JsonObject();
        foreach (var g in Checks.Where(c => c.Measured).GroupBy(c => c.IfcClass).OrderByDescending(g => g.Count()))
            byClass[g.Key] = new JsonObject { ["measured"] = g.Count(), ["failed"] = g.Count(c => !c.Pass), ["byJoin"] = g.Count(c => c.ByJoin) };
        var measured = Checks.Where(c => c.Measured).ToList();
        return new JsonObject
        {
            ["mode"] = Options.Mode, ["mm"] = Options.Mm, ["volume"] = Options.Volume, ["joins"] = Options.Joins,
            ["measured"] = Measured, ["failed"] = Failed, ["byJoin"] = ByJoin, ["notMeasured"] = Checks.Count - Measured, ["reverted"] = Reverted,
            ["fileM3"] = Math.Round(measured.Sum(c => c.FileVolume ?? 0), 3), ["nativeM3"] = Math.Round(measured.Sum(c => c.NativeVolume ?? 0), 3),
            ["ms"] = Math.Round(Milliseconds, 1), ["byClass"] = byClass,
            ["elements"] = new JsonArray([.. Checks.Select(c => (JsonNode)c.ToJson())]),
        };
    }

    /// <summary>The line the import's notes carry.</summary>
    public string Note()
    {
        var head = $"Fidelity ({Options.Mode}, {Options.Mm:0.###} mm, {Options.Volume * 100:0.###} %): {Measured} recognised element(s) measured against the file's bodies";
        var joins = ByJoin > 0 ? $"{ByJoin} of them differing only where they join others, with them" : null;
        if (Failed == 0) return head + (joins != null ? $", all within ({joins})." : ", all within.");
        var causes = Checks.Where(c => c.Measured && !c.Pass).GroupBy(c => c.Cause ?? "other").OrderByDescending(g => g.Count()).Take(3)
            .Select(g => $"{g.Count()}: {g.Key}");
        var tail = Options.Enforcing ? $"; {Reverted} brought as references instead" : "; they stay native (report only)";
        return $"{head}, {Failed} differ ({string.Join("; ", causes)}){tail}{(joins != null ? $"; within: {joins}" : "")}.";
    }
}

/// <summary>
/// The fidelity gate's measures (docs/spec-ifc.md, Poarta de fidelitate): a recognised element's native solid against the
/// body the file gives it, by volume, bounding box and base outline. Cheap: triangles only, booleans only where the cut
/// order takes something out of the native element (<see cref="CutAsNative"/>).
/// </summary>
public static class RecognitionFidelity
{
    /// <summary>A native element's geometry as the views build it (its producer, then the filters); null when it has none or its build fails.</summary>
    public static ElementGeometry? NativeGeometry(Entity e, IModelView model, IServices services)
    {
        if (services.Get<IGeometryProducers>()?.Geometry(e.Class) is not { } producer) return null;
        try
        {
            var g = producer(e, model, services);
            if (g != null) foreach (var f in services.All<IGeometryFilter>()) g = f.Apply(e, g, model, services) ?? g;
            return g;
        }
        catch (Exception ex) when (ex is ModelException or InvalidOperationException or ArgumentException or IndexOutOfRangeException or NullReferenceException) { return null; }
    }

    /// <summary>The elements one is joined to (IfcRelConnectsPathElements, either side).</summary>
    public static List<string> JoinedTo(IModelView model, string id) =>
        [.. model.RelationsOf(id, IfcClass.RelConnectsPathElements).SelectMany(r => r.Related.Prepend(r.Relating)).Where(x => x != id).Distinct()];

    /// <summary>
    /// The file's body cut as the native element is by the project's cut order (docs/spec-ifc.md, Poarta de fidelitate):
    /// the envelopes of the elements ranked over it that reach into it (a slab over a wall's head, a column through it; never
    /// one joined to it) taken out of each of its meshes. The model counts what two elements share once, in the one ranked
    /// higher, where the file's own bodies count it in both: measured so, the two are compared like with like. The body as
    /// it was when nothing reaches into it, or a mesh that cannot be made a solid.
    /// </summary>
    public static List<MeshBuilder> CutAsNative(Entity native, IModelView model, List<MeshBuilder> file, ICutters cutters, ISolidBooleans booleans)
    {
        var bodies = file.Where(x => x.T.Count > 0).ToList();
        if (bodies.Count == 0) return file;
        var (min, max) = Box(bodies);
        IReadOnlyList<Solid> by;
        try { by = cutters.For(native, model, (min, max)); }
        catch (Exception ex) when (ex is ModelException or InvalidOperationException or ArgumentException or CommandException) { return file; }
        if (by.Count == 0) return file;
        var o = new List<MeshBuilder>();
        foreach (var mesh in bodies)
        {
            var w = mesh.Welded();
            if (SolidFromMesh.Build(w.V, w.T) is not { } solid) return file;
            try
            {
                if (booleans.Difference(solid, by) is { } cut) o.Add(MeshBuilder.FromSolid(cut));
            }
            catch (InvalidOperationException) { return file; }
        }
        return o;
    }

    /// <summary>The types an import made that no element of the model is of any more (their elements made references again by the gate), gone with their layer sets.</summary>
    public static void DropUnusedTypes(IEdit edit, RecognitionContext ctx)
    {
        foreach (var t in ctx.CreatedTypes.ToList())
            if (edit.Model.Get(t) != null && !edit.Model.RelatedOf(t, IfcClass.RelDefinesByType).Any())
            {
                if (LayerSets.Of(edit.Model, t) is { } set) edit.Remove(set.Id);
                edit.Remove(t);
                ctx.CreatedTypes.Remove(t);
            }
    }

    /// <summary>A geometry's parts as triangle meshes (a mesh part's own triangles, a solid's faces triangulated).</summary>
    public static List<MeshBuilder> Meshes(ElementGeometry? g)
    {
        var o = new List<MeshBuilder>();
        foreach (var p in g?.Parts ?? [])
        {
            if (p.Mesh is { } m)
            {
                var b = new MeshBuilder();
                for (var i = 0; i + 2 < m.Xyz.Length; i += 3) b.V.Add(new Vec3(m.Xyz[i], m.Xyz[i + 1], m.Xyz[i + 2]));
                for (var i = 0; i + 2 < m.Tris.Length; i += 3) b.T.Add((m.Tris[i], m.Tris[i + 1], m.Tris[i + 2]));
                o.Add(b);
            }
            else if (p.Solid.Faces.Length > 0) o.Add(MeshBuilder.FromSolid(p.Solid));
        }
        return o;
    }

    /// <summary>
    /// The native element measured against the file's body. <paramref name="clipped"/>: the file's body was cut by a boolean
    /// (a top under a roof or a slope), which says why the two differ when they do; <paramref name="joinedTo"/>: the elements it
    /// is joined to, for <see cref="Settle"/>.
    /// </summary>
    public static FidelityCheck Check(string id, string ifcClass, string cls, string? name, IReadOnlyList<MeshBuilder> file, IReadOnlyList<MeshBuilder> native,
        FidelityOptions o, bool clipped = false, string source = "ifc", IReadOnlyList<string>? joinedTo = null)
    {
        FidelityCheck Unmeasured(string why) => new(id, ifcClass, cls, name, false, true, null, null, null, null, why, null, source);
        var fv = file.Where(m => m.T.Count > 0).ToList();
        var nv = native.Where(m => m.T.Count > 0).ToList();
        if (fv.Count == 0) return Unmeasured("the file gives it no body");
        if (nv.Count == 0) return Unmeasured("the native element has no body");
        var (fMin, fMax) = Box(fv);
        var (nMin, nMax) = Box(nv);
        // Volumes about a point near both (a georeferenced model's coordinates are large).
        var at = fMin;
        var welded = fv.Select(m => m.Welded()).ToList();
        var closed = welded.All(m => m.Closed());
        var fileVolume = welded.Sum(m => Math.Abs(SignedVolume(m, at)));
        var nativeVolume = nv.Sum(m => Math.Abs(SignedVolume(m, at)));
        double? volume = closed && fileVolume > 1e-9 ? (nativeVolume - fileVolume) / fileVolume : null;
        // The boxes: the side furthest apart.
        var sides = new (double D, string Side)[]
        {
            (Math.Abs(nMin.X - fMin.X), "plan"), (Math.Abs(nMax.X - fMax.X), "plan"), (Math.Abs(nMin.Y - fMin.Y), "plan"), (Math.Abs(nMax.Y - fMax.Y), "plan"),
            (Math.Abs(nMin.Z - fMin.Z), "base"), (Math.Abs(nMax.Z - fMax.Z), "top"),
        };
        var (boxD, boxSide) = sides.MaxBy(s => s.D);
        var boxMm = boxD * 1000;
        var fo = BaseOutline(welded, fMin.Z);
        var no = BaseOutline(nv.Select(m => m.Welded()).ToList(), nMin.Z);
        double? outlineMm = fo.Count > 0 && no.Count > 0 ? Hausdorff(fo, no) * 1000 : null;

        var fails = new List<string>();
        if (volume is { } v && Math.Abs(v) > o.Volume) fails.Add($"volume {(v > 0 ? "+" : "")}{v * 100:0.##} %");
        if (boxMm > o.Mm) fails.Add($"box {boxMm:0.#} mm at the {boxSide}");
        if (outlineMm is { } l && l > o.Mm) fails.Add($"base outline {l:0.#} mm");
        var pass = fails.Count == 0;
        string? reason = pass ? (volume == null ? "volume not compared: the file's body is not closed" : null) : string.Join("; ", fails);
        var topOff = Math.Abs(nMax.Z - fMax.Z) * 1000 > o.Mm;
        var baseOff = Math.Abs(nMin.Z - fMin.Z) * 1000 > o.Mm;
        var planOff = sides.Take(4).Any(s => s.D * 1000 > o.Mm) || outlineMm is { } ol && ol > o.Mm;
        string? cause = null;
        if (!pass)
        {
            var joined = joinedTo is { Count: > 0 };
            cause =
                baseOff && Math.Abs((nMin.Z - fMin.Z) - (nMax.Z - fMax.Z)) * 1000 <= o.Mm ? "it stands at another level (moved up or down whole: its base is not the file's)"
                : baseOff ? "its base and height are not the file's"
                : topOff ? (clipped ? "its top cut by a boolean in the file (sloped, under a roof), flat in Lapin"
                    : "its top at another height (sloped or stepped in the file, flat in Lapin, or raised or lowered to a roof)")
                : planOff ? (joined ? "in plan at its joins (Lapin's join runs its end on into the wall it meets, or mitres it, where the file's stops)"
                    : clipped ? "in plan: its ends cut by a boolean in the file (trimmed or mitred), squared in Lapin"
                    : "in plan: an end cut, a mitre or an outline read as a rectangle")
                : "the same box and outline, another volume (openings sized otherwise, lintels, a sloped face or recesses)";
        }
        return new(id, ifcClass, cls, name, true, pass, volume, boxMm, boxMm > o.Mm ? boxSide : null, outlineMm, reason, cause, source,
            FileVolume: fileVolume, NativeVolume: nativeVolume, JoinedTo: joinedTo, InPlanOnly: !pass && planOff && !topOff && !baseOff);
    }

    /// <summary>
    /// Joins settled (<see cref="FidelityOptions.Joins"/>): an element that failed only in plan, joined to others, passes when it
    /// and the elements it is joined to keep their volume together within the tolerance (a join moves volume from one to the
    /// other: one wall's end runs on, the other's stops short, or both are mitred where the file butts them). One that adds
    /// volume its neighbours do not give back (an end run into a wall that stays whole) still fails.
    /// </summary>
    public static List<FidelityCheck> Settle(IReadOnlyList<FidelityCheck> checks, FidelityOptions o)
    {
        var list = checks.ToList();
        if (!o.Joins) return list;
        var byId = new Dictionary<string, FidelityCheck>();
        foreach (var c in list) if (c.Measured && c.FileVolume != null && c.NativeVolume != null) byId.TryAdd(c.Id, c);
        for (var i = 0; i < list.Count; i++)
        {
            var c = list[i];
            if (c.Pass || !c.Measured || !c.InPlanOnly || c.JoinedTo is not { Count: > 0 } joined) continue;
            var ring = joined.Select(byId.GetValueOrDefault).OfType<FidelityCheck>().Prepend(c).DistinctBy(x => x.Id).ToList();
            if (ring.Count < 2) continue;
            double file = ring.Sum(x => x.FileVolume ?? 0), native = ring.Sum(x => x.NativeVolume ?? 0);
            if (file <= 1e-9) continue;
            var together = (native - file) / file;
            if (Math.Abs(together) > o.Volume) continue;
            list[i] = c with
            {
                Pass = true, ByJoin = true,
                Cause = $"in plan at its joins only: with the {ring.Count - 1} element(s) it joins, the volume holds ({(together >= 0 ? "+" : "")}{together * 100:0.###} %)",
            };
        }
        return list;
    }

    static (Vec3 Min, Vec3 Max) Box(IEnumerable<MeshBuilder> meshes)
    {
        double x0 = double.MaxValue, y0 = double.MaxValue, z0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue, z1 = double.MinValue;
        foreach (var m in meshes)
            foreach (var p in m.V)
            {
                x0 = Math.Min(x0, p.X); y0 = Math.Min(y0, p.Y); z0 = Math.Min(z0, p.Z);
                x1 = Math.Max(x1, p.X); y1 = Math.Max(y1, p.Y); z1 = Math.Max(z1, p.Z);
            }
        return (new Vec3(x0, y0, z0), new Vec3(x1, y1, z1));
    }

    static double SignedVolume(MeshBuilder m, Vec3 at)
    {
        double v = 0;
        foreach (var (a, b, c) in m.T)
            v += Vec3.Dot(m.V[a] - at, Vec3.Cross(m.V[b] - at, m.V[c] - at));
        return v / 6;
    }

    /// <summary>
    /// The outline of what stands lowest, in plan: the edges of the triangles lying within 2 mm of the lowest level that only
    /// one of them has (the base face's outline and its holes).
    /// </summary>
    static List<(Vec2 A, Vec2 B)> BaseOutline(IReadOnlyList<MeshBuilder> meshes, double z0)
    {
        const double tol = 2e-3;
        var edges = new Dictionary<(long, long, long, long), (Vec2 A, Vec2 B, int N)>();
        static (long, long) K(Vec2 p) => ((long)Math.Round(p.X * 1e4), (long)Math.Round(p.Y * 1e4));
        foreach (var m in meshes)
            foreach (var (a, b, c) in m.T)
            {
                var (pa, pb, pc) = (m.V[a], m.V[b], m.V[c]);
                if (Math.Abs(pa.Z - z0) > tol || Math.Abs(pb.Z - z0) > tol || Math.Abs(pc.Z - z0) > tol) continue;
                if (Math.Abs(Vec2.Cross(new Vec2(pb.X - pa.X, pb.Y - pa.Y), new Vec2(pc.X - pa.X, pc.Y - pa.Y))) < 1e-10) continue;
                foreach (var (p, q) in new[] { (pa, pb), (pb, pc), (pc, pa) })
                {
                    var (u, w) = (new Vec2(p.X, p.Y), new Vec2(q.X, q.Y));
                    var (ku, kw) = (K(u), K(w));
                    if (ku == kw) continue;
                    var key = ku.CompareTo(kw) < 0 ? (ku.Item1, ku.Item2, kw.Item1, kw.Item2) : (kw.Item1, kw.Item2, ku.Item1, ku.Item2);
                    edges[key] = edges.TryGetValue(key, out var x) ? x with { N = x.N + 1 } : (u, w, 1);
                }
            }
        return [.. edges.Values.Where(x => x.N % 2 == 1).Select(x => (x.A, x.B))];
    }

    /// <summary>The symmetric Hausdorff distance between two sets of segments, from points along each (every centimetre, at most a few thousand) to the other.</summary>
    static double Hausdorff(List<(Vec2 A, Vec2 B)> a, List<(Vec2 A, Vec2 B)> b) => Math.Max(Directed(a, b), Directed(b, a));

    static double Directed(List<(Vec2 A, Vec2 B)> from, List<(Vec2 A, Vec2 B)> to)
    {
        var total = from.Sum(s => (s.B - s.A).Length);
        var step = Math.Max(0.01, total / 4000);
        double worst = 0;
        foreach (var (p, q) in from)
        {
            var len = (q - p).Length;
            var n = Math.Max(1, (int)Math.Ceiling(len / step));
            for (var i = 0; i <= n; i++)
            {
                var x = p + (q - p) * ((double)i / n);
                var best = double.MaxValue;
                foreach (var (s, t) in to)
                {
                    best = Math.Min(best, SegmentDistance(x, s, t));
                    if (best <= worst) break;
                }
                worst = Math.Max(worst, best);
            }
        }
        return worst;
    }

    static double SegmentDistance(Vec2 p, Vec2 a, Vec2 b)
    {
        var ab = b - a;
        var l2 = ab.X * ab.X + ab.Y * ab.Y;
        var t = l2 > 0 ? Math.Clamp(((p.X - a.X) * ab.X + (p.Y - a.Y) * ab.Y) / l2, 0, 1) : 0;
        return (p - (a + ab * t)).Length;
    }
}
