using System.Collections;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;
using Speckle.Objects.Geometry;
using Speckle.Sdk.Models;

namespace Lapin.Speckle;

/// <summary>
/// What an object's program says of its shape, beside its triangles (metres, the version's frame): its location line
/// (Revit's <c>location</c>; <see cref="Curved"/> when it is an arc or another curve), its thickness and its height, each
/// with the parameter it was read from. Recognition checks the body against them and never builds from them: the mesh wins.
/// </summary>
public sealed record ReceivedHints(Vec3? Start, Vec3? End, bool Curved, double? Thickness, string? ThicknessFrom, double? Height, string? HeightFrom);

/// <summary>
/// The hints a wall's or a slab's Speckle object carries (docs/plan-speckle.md, S3). Revit's v3 connector writes a
/// <c>RevitObject</c> with its <c>location</c> curve and its parameters under <c>properties › Parameters</c>, each
/// {name, value, units, internalDefinitionName}: a wall's width (<c>WALL_ATTR_WIDTH_PARAM</c>) and unconnected height
/// (<c>WALL_USER_HEIGHT_PARAM</c>), a floor's thickness (<c>FLOOR_ATTR_THICKNESS_PARAM</c>). Lapin's own send writes a
/// layered element's layers (each thickness in metres). Other programs are read by the parameters' names (Thickness,
/// Width, Default Thickness, Unconnected Height) when they carry their units. What names no unit is not taken. Revit's
/// unconnected height is taken only for a wall whose top is unconnected (<c>WALL_HEIGHT_TYPE</c>) and not attached.
/// </summary>
public static class SpeckleHints
{
    static readonly string[] ThicknessIds = ["WALL_ATTR_WIDTH_PARAM", "FLOOR_ATTR_THICKNESS_PARAM", "FLOOR_ATTR_DEFAULT_THICKNESS_PARAM"]; // in this order
    static readonly string[] HeightIds = ["WALL_USER_HEIGHT_PARAM"];
    static readonly string[] WallThicknessNames = ["Width", "Thickness", "Wall Thickness"];
    static readonly string[] SlabThicknessNames = ["Thickness", "Default Thickness", "Slab Thickness"];
    static readonly string[] HeightNames = ["Unconnected Height", "Wall Height"];

    /// <summary>Whether recognition offers an object of this class: a wall or a slab.</summary>
    public static bool Offered(string cls) => IsWall(cls) || cls == IfcClass.Slab;

    static bool IsWall(string cls) => cls is IfcClass.Wall or "IfcWallStandardCase";

    /// <summary>A wall's or slab's hints, or null when it has none (or is neither).</summary>
    public static ReceivedHints? Read(Base b, IReadOnlyDictionary<string, object?>? props, string cls, List<string>? notes = null)
    {
        if (!Offered(cls)) return null;
        Vec3? start = null, end = null;
        var curved = false;
        switch (SpeckleRead.Member(b, "location"))
        {
            case Line l when l.start != null && l.end != null:
                var k = SpeckleRead.Metres(l.units ?? l.start.units, notes);
                start = new Vec3(l.start.x * k, l.start.y * k, l.start.z * k);
                end = new Vec3(l.end.x * k, l.end.y * k, l.end.z * k);
                break;
            case Arc or Circle or Curve or Polycurve or Polyline:
                curved = true;
                break;
        }

        var found = new List<(string Name, string? Id, double Metres)>();
        var texts = new Dictionary<string, string>();
        Walk(props, "", 0, found, texts);
        double? thickness = null, height = null;
        string? thicknessFrom = null, heightFrom = null;
        // A parameter Revit names by its built-in id first; then Lapin's layers; then a parameter's name.
        foreach (var want in ThicknessIds)
            if (thickness == null && found.FirstOrDefault(f => f.Id == want) is { Name: not null } f) (thickness, thicknessFrom) = (f.Metres, f.Name);
        // Revit's unconnected height is the wall's height only when its top is unconnected and not attached: a top constrained to a
        // level, or attached to a floor or roof, keeps a stale value.
        var unconnected = texts.GetValueOrDefault("WALL_HEIGHT_TYPE") is not { } top || top.Equals("Unconnected", StringComparison.OrdinalIgnoreCase);
        if (!unconnected || texts.GetValueOrDefault("WALL_TOP_IS_ATTACHED") is { } attached && attached.Equals("Yes", StringComparison.OrdinalIgnoreCase)) found.RemoveAll(f => f.Id != null && HeightIds.Contains(f.Id) || HeightNames.Contains(f.Name, StringComparer.OrdinalIgnoreCase));
        foreach (var want in HeightIds)
            if (height == null && found.FirstOrDefault(f => f.Id == want) is { Name: not null } f) (height, heightFrom) = (f.Metres, f.Name);
        if (thickness == null && props?.GetValueOrDefault("Layers") is IEnumerable layers and not string)
        {
            double sum = 0;
            var any = false;
            foreach (var layer in layers)
                if (layer is IDictionary<string, object?> d && Number(Get(d, "Thickness")) is { } t) { sum += t; any = true; }
            if (any && sum > 0) (thickness, thicknessFrom) = (sum, "Layers");
        }
        var names = IsWall(cls) ? WallThicknessNames : SlabThicknessNames;
        foreach (var n in names)
            if (thickness == null && found.FirstOrDefault(f => f.Id == null && string.Equals(f.Name, n, StringComparison.OrdinalIgnoreCase)) is { Name: not null } f) (thickness, thicknessFrom) = (f.Metres, f.Name);
        if (IsWall(cls))
            foreach (var n in HeightNames)
                if (height == null && found.FirstOrDefault(f => string.Equals(f.Name, n, StringComparison.OrdinalIgnoreCase)) is { Name: not null } f) (height, heightFrom) = (f.Metres, f.Name);
        if (thickness is not > 0) (thickness, thicknessFrom) = (null, null);
        if (height is not > 0 || !IsWall(cls)) (height, heightFrom) = (null, null);
        return start == null && !curved && thickness == null && height == null ? null : new ReceivedHints(start, end, curved, thickness, thicknessFrom, height, heightFrom);
    }

    /// <summary>Every parameter with a value and a unit Speckle knows, in metres: {name, value, units, internalDefinitionName} wherever it is.</summary>
    static void Walk(object? v, string key, int depth, List<(string, string?, double)> found, Dictionary<string, string> texts)
    {
        if (depth > 8 || found.Count > 5000) return;
        switch (v)
        {
            case IDictionary<string, object?> d when d.TryGetValue("value", out var text) && text is string t && Get(d, "internalDefinitionName") is string tid:
                texts.TryAdd(tid, t);
                return;
            case IDictionary<string, object?> d when d.TryGetValue("value", out var value) && Number(value) is { } x:
                if (Metres(Get(d, "units") as string) is { } k)
                    found.Add((Get(d, "name") as string is { Length: > 0 } n ? n : key, Get(d, "internalDefinitionName") as string, x * k));
                return;
            case IDictionary<string, object?> d:
                foreach (var (name, x) in d) Walk(x, name, depth + 1, found, texts);
                return;
            case Base b:
                foreach (var name in b.GetMembers().Keys) Walk(SpeckleRead.Member(b, name), name, depth + 1, found, texts);
                return;
        }
    }

    /// <summary>A unit's metres, from Speckle's names and Revit's own (Millimeters…); null when it names none.</summary>
    static double? Metres(string? units)
    {
        if (string.IsNullOrWhiteSpace(units)) return null;
        switch (units.Trim().ToLowerInvariant())
        {
            case "millimeters" or "millimetres": return 0.001;
            case "centimeters" or "centimetres": return 0.01;
            case "meters" or "metres": return 1;
            case "feet" or "feet and fractional inches" or "decimal feet": return 0.3048;
            case "inches" or "fractional inches" or "decimal inches": return 0.0254;
        }
        try { return global::Speckle.Sdk.Common.Units.GetConversionFactor(units, global::Speckle.Sdk.Common.Units.Meters); }
        catch (Exception) { return null; }
    }

    static object? Get(IDictionary<string, object?> d, string key) => d.TryGetValue(key, out var v) ? v : null;

    static double? Number(object? v) => v switch
    {
        double d when double.IsFinite(d) => d,
        float f when float.IsFinite(f) => f,
        int i => i,
        long l => l,
        decimal m => (double)m,
        _ => null,
    };

    /// <summary>
    /// Why an object's hints contradict its triangles (null: they agree, or there are none; said without numbers, so the summary
    /// counts the objects by why): a thickness or height off by more
    /// than the gate's tolerance (1 mm or 0.5 %), a location line that does not run along the body or lies outside it.
    /// </summary>
    public static string? Contradiction(ReceivedElement el, FidelityOptions gate)
    {
        if (el.Hints is not { } h) return null;
        var pts = el.Parts.SelectMany(p => p.Vertices).ToList();
        if (pts.Count < 4) return null;
        double Tol(double v) => Math.Max(gate.Mm / 1000, gate.Volume * Math.Abs(v));
        var zSpan = pts.Max(p => p.Z) - pts.Min(p => p.Z);
        if (!IsWall(el.IfcClass))
            return h.Thickness is { } st && Math.Abs(zSpan - st) > Tol(st) ? $"its {h.ThicknessFrom} is not its body's thickness" : null;

        var hull = Hull(pts.Select(p => p.Xy).ToList());
        if (hull.Count < 3) return null;
        var (minWidth, _) = MinWidth(hull);
        double? width = h.Curved ? null : minWidth;
        if (h.Start is { } a && h.End is { } b && (b.Xy - a.Xy).Length > 0.001)
        {
            var d = (b.Xy - a.Xy).Normalized();
            var n = new Vec2(-d.Y, d.X);
            var across = hull.Select(p => Vec2.Dot(p, n)).ToList();
            var (lo, hi) = (across.Min(), across.Max());
            if (hi - lo - minWidth > Tol(minWidth)) return "its location line does not run along its body";
            var s = Vec2.Dot(a.Xy, n);
            if (s < lo - Tol(hi - lo) || s > hi + Tol(hi - lo)) return "its location line is outside its body";
            width = hi - lo;
        }
        if (h.Thickness is { } t && width is { } w && Math.Abs(w - t) > Tol(t)) return $"its {h.ThicknessFrom} is not its body's thickness";
        if (h.Height is { } ht && Math.Abs(zSpan - ht) > Tol(ht)) return $"its {h.HeightFrom} is not its body's height";
        return null;
    }

    /// <summary>The convex hull of points in plan (Andrew's monotone chain), anticlockwise.</summary>
    static List<Vec2> Hull(List<Vec2> points)
    {
        var p = points.Distinct().OrderBy(x => x.X).ThenBy(x => x.Y).ToList();
        if (p.Count < 3) return p;
        static double Cross(Vec2 o, Vec2 a, Vec2 b) => (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);
        var h = new List<Vec2>();
        foreach (var pass in new[] { p, Enumerable.Reverse(p).ToList() })
        {
            var start = h.Count;
            foreach (var x in pass)
            {
                while (h.Count >= start + 2 && Cross(h[^2], h[^1], x) <= 1e-12) h.RemoveAt(h.Count - 1);
                h.Add(x);
            }
            h.RemoveAt(h.Count - 1);
        }
        return h;
    }

    /// <summary>The hull's least width, over each edge's direction, and that direction.</summary>
    static (double Width, Vec2 Along) MinWidth(List<Vec2> hull)
    {
        var best = (Width: double.MaxValue, Along: new Vec2(1, 0));
        for (var i = 0; i < hull.Count; i++)
        {
            var e = hull[(i + 1) % hull.Count] - hull[i];
            if (e.Length < 1e-9) continue;
            var d = e.Normalized();
            var n = new Vec2(-d.Y, d.X);
            var w = hull.Max(p => Vec2.Dot(p - hull[i], n)) - hull.Min(p => Vec2.Dot(p - hull[i], n));
            if (w < best.Width) best = (w, d);
        }
        return best;
    }
}

/// <summary>What recognition made of a receive's walls and slabs: how many became native, how many it offered, and why the rest stayed references.</summary>
public sealed record SpeckleRecognitionReport(int Recognized, int Offered, IReadOnlyDictionary<string, int> ByClass, IReadOnlyDictionary<string, int> Kept, FidelityReport? Fidelity)
{
    public JsonObject ToJson()
    {
        var kept = new JsonObject();
        foreach (var (k, v) in Kept.OrderByDescending(kv => kv.Value)) kept[k] = v;
        var byClass = new JsonObject();
        foreach (var (k, v) in ByClass.OrderByDescending(kv => kv.Value)) byClass[k] = v;
        return new JsonObject { ["recognized"] = Recognized, ["offered"] = Offered, ["byClass"] = byClass, ["kept"] = kept, ["reverted"] = Fidelity?.Checks.Count(c => c.Reverted) ?? 0 };
    }
}

/// <summary>
/// A receive's references made native walls and slabs (docs/plan-speckle.md, S3), by the machinery the Fragments import uses
/// (<see cref="MeshRecognition"/>: the triangles read as prisms, offered to the modules' recognisers, each native element measured
/// against them by the fidelity gate, <see cref="RecognitionFidelity"/>, and what differs by more than 1 mm or 0.5 % back the
/// reference it was). Before that, an object whose own parameters (<see cref="SpeckleHints"/>) contradict its body stays a
/// reference: the mesh wins. A recognised element keeps its GlobalId, its group, its properties (the <c>Speckle</c> set) and its
/// <c>speckle</c> attribute, so a later receive finds it again; no lintel, reveal or join of Lapin's own is added.
/// </summary>
public static class SpeckleRecognition
{
    /// <summary>A wall's cut priority (ArchitectureModule: walls 50, slabs 60): a recognised slab's, so it cuts no wall.</summary>
    const int WallCutPriority = 50;

    public const string Curtain = "a curtain wall";
    public const string Contradicted = "its parameters contradict its body";
    /// <summary>
    /// Several meshes that are each open and together one closed solid (every edge between two triangles, the vertices welded at
    /// 0.01 mm), as one part, moved by <paramref name="shift"/>; null when there is one mesh, or a mesh is a solid alone, or they do not close.
    /// </summary>
    internal static JsonObject? Welded(IReadOnlyList<ReceivedPart> parts, Vec3 shift)
    {
        if (parts.Count < 2) return null;
        static (long, long, long) Key(Vec3 v) => ((long)Math.Round(v.X * 1e5), (long)Math.Round(v.Y * 1e5), (long)Math.Round(v.Z * 1e5));
        var index = new Dictionary<(long, long, long), int>();
        var vertices = new List<Vec3>();
        var triangles = new List<(int, int, int)>();
        static bool Closed(IEnumerable<(int A, int B, int C)> tris)
        {
            var edges = new Dictionary<(int, int), int>();
            foreach (var (a, b, c) in tris)
                foreach (var (p, q) in new[] { (a, b), (b, c), (c, a) })
                {
                    var k = p < q ? (p, q) : (q, p);
                    edges[k] = edges.GetValueOrDefault(k) + 1;
                }
            return edges.Count > 0 && edges.Values.All(n => n == 2);
        }
        foreach (var part in parts)
        {
            var map = part.Vertices.Select(v =>
            {
                var k = Key(v);
                if (!index.TryGetValue(k, out var i)) { index[k] = i = vertices.Count; vertices.Add(v + shift); }
                return i;
            }).ToArray();
            var own = part.Triangles.Select(t => (map[t.A], map[t.B], map[t.C])).Where(t => t.Item1 != t.Item2 && t.Item2 != t.Item3 && t.Item1 != t.Item3).ToList();
            if (Closed(own)) return null;
            triangles.AddRange(own);
        }
        if (!Closed(triangles)) return null;
        var first = parts[0];
        return StoredBodies.Write([(first.Material ?? "Body", first.Color, vertices, triangles)]);
    }

    public const string Differs = "differs from its body by more than 1 mm or 0.5 %";

    /// <param name="shift">Where the version's origin stands in the project (the references' bodies are moved by it).</param>
    public static SpeckleRecognitionReport Run(IEdit edit, IServices services, IReadOnlyList<(string Id, ReceivedElement El)> offered, Vec3 shift, FidelityOptions? fidelity = null)
    {
        var gate = fidelity ?? FidelityOptions.Default;
        var m = edit.Model;
        var kept = new Dictionary<string, int>();
        void Keep(string why) => kept[why] = kept.GetValueOrDefault(why) + 1;
        void Stamp(string id, string how)
        {
            if (m.Get(id) is not { } e || e.Attrs[SpeckleReceive.Key] is not JsonObject meta) return;
            var attrs = e.CloneAttrs();
            var next = (JsonObject)meta.DeepClone();
            next["recognition"] = how;
            attrs[SpeckleReceive.Key] = next;
            edit.Replace(e.With(attrs: attrs));
        }

        var go = new List<string>();
        var bodies = new Dictionary<string, JsonNode>();
        var saved = new Dictionary<string, (JsonNode Meta, JsonNode? Properties, JsonNode? Source)>();
        foreach (var (id, el) in offered)
        {
            if (!SpeckleHints.Offered(el.IfcClass) || m.Get(id) is not { } e || !StoredBodies.Has(e) || e.Attrs[SpeckleReceive.Key] is not JsonObject meta) continue;
            // A curtain wall is panels and mullions on a grid, never a layered wall: it stays a reference (its parts too).
            if (el.Properties["Speckle"] is JsonObject main && new[] { "family", "type", "category" }.Any(k => main[k] is JsonValue v && v.TryGetValue<string>(out var t)
                && t.Contains("Curtain", StringComparison.OrdinalIgnoreCase)))
            {
                Keep(Curtain);
                Stamp(id, "reference");
                continue;
            }
            if (el.Parts.Count > 0 && SpeckleHints.Contradiction(el, gate) is { } why)
            {
                Keep(Contradicted + ": " + why);
                Stamp(id, "reference");
                continue;
            }
            go.Add(id);
            saved[id] = (meta.DeepClone(), e.Attrs["properties"]?.DeepClone(), e.Attrs["source"]?.DeepClone());
            // Revit sends a wall as one mesh per material of its faces (its painted sides in one, its core's edges in another): no
            // mesh is a solid alone, together they are. Read as one solid; given its own body back if it stays a reference.
            if (Welded(el.Parts, shift) is { } one)
            {
                bodies[id] = e.Attrs[StoredBodies.Key]!.DeepClone();
                var attrs = e.CloneAttrs();
                attrs[StoredBodies.Key] = one;
                edit.Replace(e.With(attrs: attrs));
            }
        }
        // The slabs first, then the walls. A recognised slab cuts no wall (its cut priority the wall's, not over it): Revit's walls
        // run up into the floor over them as they came, and a native slab cutting their heads would make them other than the
        // received body (the gate measures each wall with the slabs about it native).
        var slabs = go.Where(id => m.Get(id)?.Class == IfcClass.Slab).ToList();
        var reports = new List<MeshRecognitionReport>();
        if (slabs.Count > 0) reports.Add(MeshRecognition.Recognize(edit, services, slabs, gate));
        foreach (var id in slabs)
            if (m.Get(id) is { Class: IfcClass.Slab } slab && !StoredBodies.Has(slab))
            {
                var attrs = slab.CloneAttrs();
                attrs["cutPriority"] = WallCutPriority;
                edit.Replace(slab.With(attrs: attrs));
            }
        var walls = go.Except(slabs).ToList();
        if (walls.Count > 0) reports.Add(MeshRecognition.Recognize(edit, services, walls, gate));
        var skipped = new Dictionary<string, int>();
        foreach (var x in reports) foreach (var (k, v) in x.Skipped) skipped[k] = skipped.GetValueOrDefault(k) + v;
        var checks = reports.SelectMany(x => x.Fidelity?.Checks ?? []).ToList();

        var recognized = 0;
        var byClass = new Dictionary<string, int>();
        foreach (var id in go)
        {
            if (m.Get(id) is not { } now) continue;
            if (StoredBodies.Has(now))
            {
                if (bodies.TryGetValue(id, out var own))
                {
                    var back = now.CloneAttrs();
                    back[StoredBodies.Key] = own;
                    edit.Replace(now.With(attrs: back));
                }
                Stamp(id, "reference");
                continue;
            }
            // Native: its provenance kept (where it came from, its properties), so the next receive updates it in place.
            var (meta, props, source) = saved[id];
            var attrs = now.CloneAttrs();
            var sp = (JsonObject)meta;
            sp["recognition"] = "native";
            attrs[SpeckleReceive.Key] = sp;
            if (props != null) attrs["properties"] = props;
            if (source != null) attrs["source"] = source;
            edit.Replace(now.With(attrs: attrs));
            recognized++;
            byClass[now.Class] = byClass.GetValueOrDefault(now.Class) + 1;
        }
        // Reverted by the gate, counted by what differed (its volume, its base outline, its box), the numbers left out.
        foreach (var c in checks.Where(c => c.Reverted))
        {
            var what = string.Join(", ", (c.Reason ?? "").Split(';').Select(x => System.Text.RegularExpressions.Regex.Replace(x, @"[-+−]?[0-9][0-9.,]*\s*(%|mm)?", "").Trim()).Select(x => System.Text.RegularExpressions.Regex.Replace(x, @"\s+", " ")).Where(x => x.Length > 0).Distinct());
            Keep(what.Length > 0 ? $"{Differs}: {what}" : Differs);
        }
        foreach (var (why, n) in skipped) kept[why] = kept.GetValueOrDefault(why) + n;
        // A reference no recogniser took and no check reverted (counted by why above): what is left over unexplained.
        var explained = kept.Values.Sum();
        var stayed = offered.Count(x => SpeckleHints.Offered(x.El.IfcClass) && m.Get(x.Id) is { } e && StoredBodies.Has(e));
        if (stayed > explained) kept["no recogniser took it"] = stayed - explained;
        var fidelityReport = checks.Count > 0 ? new FidelityReport(gate, checks, checks.Count(c => c.Reverted), reports.Sum(x => x.Fidelity?.Milliseconds ?? 0)) : null;
        return new SpeckleRecognitionReport(recognized, go.Count + kept.Where(k => k.Key.StartsWith(Contradicted, StringComparison.Ordinal) || k.Key == Curtain).Sum(k => k.Value), byClass, kept, fidelityReport);
    }
}
