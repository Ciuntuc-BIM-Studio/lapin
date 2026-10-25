namespace Lapin.Geometry;

/// <summary>A directed use of an edge by one loop. In a closed solid every half-edge has a twin.</summary>
public sealed class HalfEdge
{
    public int From, To, Face, Loop, Next;
    public int Twin = -1;
}

/// <summary>
/// The half-edge structure, derived on demand and never stored on the solid
/// (caching it would make every transform responsible for invalidating it).
/// </summary>
public sealed class Topology
{
    public List<HalfEdge> HalfEdges { get; } = [];
    public Dictionary<(int, int), List<int>> ByEdge { get; } = [];

    static (int, int) Key(int a, int b) => a < b ? (a, b) : (b, a);

    public static Topology Build(Solid solid)
    {
        var t = new Topology();
        for (int fi = 0; fi < solid.Faces.Length; fi++)
        {
            var f = solid.Faces[fi];
            var loops = new[] { f.Outer }.Concat(f.Holes).ToArray();
            for (int li = 0; li < loops.Length; li++)
            {
                var loop = loops[li];
                int b = t.HalfEdges.Count;
                for (int i = 0; i < loop.Length; i++)
                {
                    int from = loop[i], to = loop[(i + 1) % loop.Length];
                    int idx = t.HalfEdges.Count;
                    t.HalfEdges.Add(new HalfEdge { From = from, To = to, Face = fi, Loop = li, Next = b + (i + 1) % loop.Length });
                    var k = Key(from, to);
                    if (t.ByEdge.TryGetValue(k, out var l)) l.Add(idx); else t.ByEdge[k] = [idx];
                }
            }
        }
        foreach (var uses in t.ByEdge.Values)
        {
            if (uses.Count != 2) continue;
            var a = t.HalfEdges[uses[0]];
            var b = t.HalfEdges[uses[1]];
            if (a.From == b.To && a.To == b.From) { a.Twin = uses[1]; b.Twin = uses[0]; }
        }
        return t;
    }

    /// <summary>
    /// Closed, manifold, consistently wound, planar, outward. The kernel's safety
    /// net: run in tests so malformed geometry is caught where it is made.
    /// </summary>
    public static List<BrepDiagnostic> Validate(Solid solid, double tolPlanar = Tolerance.Planar)
    {
        var o = new List<BrepDiagnostic>();
        if (solid.Faces.Length == 0) return [new("empty", DiagnosticSeverity.Error, "Solid has no faces.")];

        for (int i = 0; i < solid.Faces.Length; i++)
        {
            var f = solid.Faces[i];
            if (f.Outer.Length < 3) { o.Add(new("degenerate_face", DiagnosticSeverity.Error, $"Face {i} has {f.Outer.Length} vertices.", i)); continue; }
            var dev = Measure.FacePlanarity(solid, i);
            if (dev > tolPlanar) o.Add(new("non_planar_face", DiagnosticSeverity.Error, $"Face {i} deviates {dev:0.######} m from its plane.", i));
        }

        var t = Build(solid);
        int open = 0, nonManifold = 0, badWinding = 0;
        foreach (var uses in t.ByEdge.Values)
        {
            if (uses.Count < 2) { open++; continue; }
            if (uses.Count > 2) { nonManifold++; continue; }
            if (t.HalfEdges[uses[0]].Twin < 0 || t.HalfEdges[uses[1]].Twin < 0) badWinding++;
        }
        if (open > 0) o.Add(new("not_closed", DiagnosticSeverity.Error, $"{open} edge(s) belong to only one face: the shell is open."));
        if (nonManifold > 0) o.Add(new("non_manifold_edge", DiagnosticSeverity.Error, $"{nonManifold} edge(s) shared by more than two faces."));
        if (badWinding > 0) o.Add(new("inconsistent_winding", DiagnosticSeverity.Error, $"{badWinding} edge(s) traversed the same way by both faces."));
        if (open == 0 && nonManifold == 0 && Measure.SignedVolume(solid) < 0)
            o.Add(new("inverted", DiagnosticSeverity.Error, "Signed volume is negative: normals point inward."));
        return o;
    }

    public static bool IsManifold(Solid s) => Validate(s).All(d => d.Severity != DiagnosticSeverity.Error);
}
