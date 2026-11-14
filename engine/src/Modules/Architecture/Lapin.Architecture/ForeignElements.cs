using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>
/// Foreign IFC walls and floors rebuilt natively (docs/spec-ifc.md,
/// Recunoașterea). A wall: from its vertical prism's footprint, the axis along
/// the long sides, the thickness across them, the height from the sweep; its
/// layers from the file's layer set, else one layer of its material; its
/// openings from their boxes in the wall's frame, filled with doors and
/// windows whose types come from the file's (or from their size). A floor:
/// the prism's base as the contour, its holes as openings, its height as the
/// thickness. Each keeps the file's GlobalId as its id.
/// </summary>
public sealed class ForeignElements : IIfcRecognizer
{
    const double MinThickness = 0.01, MaxThickness = 1.2, MinSlabThickness = 0.03, MaxSlabThickness = 1.5;

    public bool Takes(string ifcClass) => ifcClass is IfcClass.Wall or "IfcWallStandardCase" or IfcClass.Slab or IfcClass.Space or IfcClass.Roof;

    public string? Recognize(IEdit edit, ForeignElement e, RecognitionContext ctx) => e.IfcClass switch
    {
        IfcClass.Wall or "IfcWallStandardCase" => Wall(edit, e, ctx),
        IfcClass.Slab => Slab(edit, e, ctx),
        IfcClass.Space => Room(edit, e, ctx),
        IfcClass.Roof => Roofs.ForeignRoofs.Recognize(edit, e, ctx),
        _ => null,
    };

    // ─── Rooms ────────────────────────────────────────────────────────────────

    /// <summary>
    /// A room from a foreign space: its outline from its body (the base of its
    /// largest vertical prism, its holes as islands; else its FootPrint curve;
    /// else the outline of its floor's faces), its height and its base above
    /// the storey from the body; its name from LongName and its number from
    /// Name (as most tools write them; Name alone, or Name the same as
    /// LongName, is its name, and its number is Pset_SpaceCommon's Reference
    /// or the storey's next one); its function from ObjectType or the file's
    /// category or occupancy, its department and floor finish from its
    /// property sets. A space whose shape gives no outline still comes in,
    /// with its properties, as a room without an outline (drawn nowhere until
    /// it is given one). It keeps its outline.
    /// </summary>
    string? Room(IEdit edit, ForeignElement e, RecognitionContext ctx)
    {
        var storey = edit.Model.Get(e.StoreyId);
        if (storey == null) { ctx.Skip("a space without a storey"); return null; }
        IReadOnlyList<Vec2>? outer = null;
        List<IReadOnlyList<Vec2>> islands = [];
        double? height = null, baseZ = null;
        if (e.Prisms.Where(p => p.Vertical && p.Height > 1e-3).MaxBy(p => Math.Abs(Polygon2.SignedArea(Footprints.Plan(p.Outer)))) is { } prism)
        {
            outer = Footprints.Plan(prism.Outer);
            islands = prism.Holes.Select(h => (IReadOnlyList<Vec2>)Footprints.Plan(h)).Where(h => h.Count >= 3).ToList();
            height = prism.Height;
            baseZ = Math.Min(prism.BaseZ, prism.BaseZ + prism.Sweep.Z);
        }
        else
        {
            if (e.Footprint is { Count: > 0 } fp)
            {
                outer = fp[0];
                islands = fp.Skip(1).Where(h => h.Count >= 3 && Polygon2.Contains(fp[0], h[0])).ToList();
            }
            if (e.Box is { } b) { height = b.Max.Z - b.Min.Z; baseZ = b.Min.Z; }
        }
        if (outer != null && (outer.Count < 3 || Math.Abs(Polygon2.SignedArea(outer)) < 1e-3)) outer = null;
        var attrs = new JsonObject { ["auto"] = false };
        if (outer != null)
        {
            attrs["contour"] = SectionPaths.PointsJson(Polygon2.EnsureCcw(outer));
            if (islands.Count > 0) attrs["islands"] = new JsonArray(islands.Select(i => (JsonNode)SectionPaths.PointsJson(i)).ToArray());
        }
        else ctx.Notes.Add("a space whose shape gives no outline came in as a room without an outline, with its properties: select it (the browser's IFC tab) and give it one in its properties");
        var longName = e.LongName?.Trim() is { Length: > 0 } ln ? ln : null;
        var plainName = e.Name?.Trim() is { Length: > 0 } nm && !Placeholder(nm) ? nm : null;
        var name = longName ?? plainName ?? "Room";
        var number = longName != null && plainName != null && !string.Equals(plainName, longName, StringComparison.OrdinalIgnoreCase) ? plainName
            : Property(e, ("Pset_SpaceCommon", "Reference"), (null, "Number")) is { } reference && !string.Equals(reference, name, StringComparison.OrdinalIgnoreCase) ? reference
            : Rooms.NextNumber(edit.Model, storey.Id);
        attrs["number"] = number;
        if ((e.ObjectType?.Trim() is { Length: > 0 } ot ? ot : Property(e, ("Pset_SpaceCommon", "Category"), ("Pset_SpaceOccupancyRequirements", "OccupancyType"), (null, "Occupancy"), (null, "Function"))) is { } fn)
            attrs["function"] = fn;
        if (Property(e, (null, "Department")) is { } department) attrs["department"] = department;
        if (Property(e, ("Pset_SpaceCoveringRequirements", "FloorCovering"), ("Pset_SpaceCommon", "FloorCovering"), (null, "Floor Finish"), (null, "FloorFinish")) is { } floor) attrs["finishFloor"] = floor;
        if (height is { } h && h > 0.1 && Math.Abs(h - storey.Num("height", 3)) > 1e-3) attrs["height"] = Math.Round(h, 6);
        if (baseZ is { } z && Math.Round(z - storey.Num("elevation"), 6) is var offset && Math.Abs(offset) > 1e-3) attrs["baseOffset"] = offset;
        var id = GlobalId.IsValid(e.GlobalId) && edit.Model.Get(e.GlobalId) == null ? e.GlobalId : edit.NewId();
        edit.Add(new Entity(id, Rooms.Class, name, attrs));
        edit.Relate(IfcClass.RelContainedInSpatialStructure, storey.Id, id);
        ctx.Made.Add(id);
        return id;
    }

    /// <summary>
    /// The first of these properties the element has, as text (a set of null:
    /// the property in any of its sets); empty values, and values that are
    /// only a placeholder such as ArchiCAD's „&lt;Raumnummer&gt;”, are none.
    /// </summary>
    static string? Property(ForeignElement e, params (string? Set, string Name)[] wanted)
    {
        foreach (var (set, name) in wanted)
            foreach (var (setName, props) in e.Properties)
            {
                if (set != null && !string.Equals(setName, set, StringComparison.OrdinalIgnoreCase) || props is not JsonObject o) continue;
                foreach (var (k, v) in o)
                    if (string.Equals(k, name, StringComparison.OrdinalIgnoreCase) && v is JsonValue jv && jv.TryGetValue<string>(out var text)
                        && text.Trim() is { Length: > 0 } t && !Placeholder(t))
                        return t;
            }
        return null;
    }

    /// <summary>No lintel: what recognised walls and doors and windows have, as the file has none of Lapin's (a lintel the file models is an element of its own).</summary>
    internal static readonly LintelRule NoLintel = LintelRule.Default with { Mode = "none" };

    /// <summary>A property of a set (by name, any case) as a number, from a number or a text in the invariant culture; null without one.</summary>
    static double? Number(JsonObject properties, string set, string name)
    {
        if (Value(properties, set, name) is not JsonValue v) return null;
        if (v.TryGetValue<double>(out var d)) return d;
        return v.TryGetValue<string>(out var t) && double.TryParse(t, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x) ? x : null;
    }

    /// <summary>A property of a set as a yes or no, from a boolean or its text (True, .T., yes); null without one.</summary>
    static bool? Flag(JsonObject properties, string set, string name)
    {
        if (Value(properties, set, name) is not JsonValue v) return null;
        if (v.TryGetValue<bool>(out var b)) return b;
        return v.TryGetValue<string>(out var t) ? t.Trim().Trim('.').ToUpperInvariant() switch { "T" or "TRUE" or "YES" or "1" => true, "F" or "FALSE" or "NO" or "0" => false, _ => null } : null;
    }

    static JsonNode? Value(JsonObject properties, string set, string name)
    {
        foreach (var (setName, props) in properties)
            if (string.Equals(setName, set, StringComparison.OrdinalIgnoreCase) && props is JsonObject o)
                foreach (var (k, v) in o)
                    if (string.Equals(k, name, StringComparison.OrdinalIgnoreCase)) return v;
        return null;
    }

    /// <summary>A text that only stands for a value not filled in („&lt;Raumnummer&gt;”, as ArchiCAD writes an empty zone number).</summary>
    static bool Placeholder(string t) => t.Length > 2 && t[0] == '<' && t[^1] == '>';

    /// <summary>
    /// Finishes exported as walls of their own made layers of the walls they cover (<see cref="FinishLayers"/>), then
    /// the walls made this import joined where the file's walls meet the way a join of Lapin's would leave them
    /// (<see cref="FoundJoins"/>: a mitre, a butt, a T, in line; their ends otherwise square where the file's end), their
    /// openings kept where the file has them while their axes go to the joints.
    /// </summary>
    public void Finish(IEdit edit, RecognitionContext ctx)
    {
        if (FinishLayers.Absorb(edit, ctx.Made, ctx).Count > 0) ctx.Made.RemoveAll(id => edit.Model.Get(id) == null);
        if (ctx.Services.Get<IPathElements>() is not { } paths) return;
        var walls = ctx.Made.Where(id => edit.Model.Get(id)?.Class == IfcClass.Wall).ToList();
        // A wall that took finishes as layers: its footprint and theirs, as one (the outline round them all); its core's own ends kept, for the free ones.
        var cores = new Dictionary<string, (Vec2 Start, Vec2 End, IReadOnlyList<Vec2> Print)>();
        foreach (var id in walls)
        {
            if ((edit.Model.Get(id)!.Attrs["imported"] as JsonObject)?["absorbed"] is not JsonArray absorbed || absorbed.Count == 0) continue;
            var prints = absorbed.Select(a => (string?)a?["id"]).Prepend(id).Select(x => x != null && ctx.Footprints.TryGetValue(x, out var fp) ? fp : null).ToList();
            if (prints.Any(p => p == null)) { ctx.Footprints.Remove(id); continue; }
            var w0 = edit.Model.Get(id)!;
            cores[id] = (Walls.Start(w0), Walls.End(w0), prints[0]!);
            ctx.Footprints[id] = Polygon2.ConvexHull(prints.SelectMany(p => p!));
        }
        // Each opening's place in plan: a join moves its wall's start, which its place along the wall is measured from.
        var placed = new List<(string Opening, string Wall, Vec2 At)>();
        // And a wall's own top (a gable), measured from its start too.
        var starts = new Dictionary<string, Vec2>();
        foreach (var id in walls)
        {
            var w = edit.Model.Get(id)!;
            var (s, d) = (Walls.Start(w), (Walls.End(w) - Walls.Start(w)).Normalized());
            foreach (var op in Openings.Of(edit.Model, id))
                placed.Add((op.Id, id, s + d * op.Num("along")));
            if (w.Attrs[WallTops.TopProfileKey] != null) starts[id] = s;
        }
        // A wall whose layers end where the file's do, each its own way (Walls.LayerEndsKey), keeps those ends: no join takes them.
        try { FoundJoins.Make(edit, paths, [.. walls.Where(id => edit.Model.Get(id)!.Attrs[Walls.LayerEndsKey] == null)], ctx.Footprints, connections: ctx.Connections); }
        catch (CommandException ex) { ctx.Notes.Add($"Walls not joined: {ex.Message}"); }
        foreach (var (id, core) in cores) FinishEnds(edit, id, core.Start, core.End, core.Print);
        foreach (var (wallId, was) in starts)
        {
            if (edit.Model.Get(wallId) is not { } w || w.Attrs[WallTops.TopProfileKey] is not JsonArray top) continue;
            var shift = Math.Round(Vec2.Dot(Walls.Start(w) - was, (Walls.End(w) - Walls.Start(w)).Normalized()), 6);
            if (Math.Abs(shift) < 1e-6) continue;
            var attrs = w.CloneAttrs();
            attrs[WallTops.TopProfileKey] = WallTops.ProfileJson(top.OfType<JsonArray>().Select(q => ((double)q[0]! - shift, (double)q[1]!)));
            edit.Replace(w.With(attrs: attrs));
        }
        foreach (var (opId, wallId, at) in placed)
        {
            if (edit.Model.Get(opId) is not { } op || edit.Model.Get(wallId) is not { } w) continue;
            var along = Math.Round(Vec2.Dot(at - Walls.Start(w), (Walls.End(w) - Walls.Start(w)).Normalized()), 6);
            if (Math.Abs(along - op.Num("along")) < 1e-6) continue;
            var attrs = op.CloneAttrs();
            attrs["along"] = along;
            edit.Replace(op.With(attrs: attrs));
        }
    }

    /// <summary>
    /// A wall's free ends where it took finishes as layers: the core where the file's ends (square there), the finishes run on
    /// past it, or stopping short of it, as far as the file's do (<see cref="Walls.LayerEndsKey"/>: a render round a free corner,
    /// a plaster stopping before a reveal). Without this the outline round them all, stepped at that end, would square the
    /// whole wall halfway between the core's end and the finish's. An end a join takes is the join's; a core whose own end is
    /// not square is left to the joins' reading (the gate measures it).
    /// </summary>
    static void FinishEnds(IEdit edit, string id, Vec2 start, Vec2 end, IReadOnlyList<Vec2> corePrint)
    {
        const double tol = 1e-3;
        if (edit.Model.Get(id) is not { } w || (w.Attrs["imported"] as JsonObject)?["absorbed"] is not JsonArray absorbed) return;
        var length = (end - start).Length;
        if (length < 1e-3) return;
        var dir = (end - start).Normalized();
        // The core's footprint square at an end of the axis it was read on: both its faces end there (not cut at an angle, not stepped).
        var left = dir.Perp;
        var uv = corePrint.Select(p => (U: Vec2.Dot(p - start, dir), V: Vec2.Dot(p - start, left))).ToList();
        if (uv.Count < 3) return;
        var (v0, v1) = (uv.Min(q => q.V), uv.Max(q => q.V));
        bool Square(int i)
        {
            var at = i == 0 ? 0 : length;
            foreach (var face in new[] { v0, v1 })
            {
                var on = uv.Where(q => Math.Abs(q.V - face) <= tol).Select(q => q.U).ToList();
                if (on.Count == 0 || Math.Abs((i == 0 ? on.Min() : on.Max()) - at) > tol) return false;
            }
            // Nothing of it beyond that line, nor short of it between the faces (a notch).
            var near = uv.Where(q => i == 0 ? q.U < length / 2 : q.U >= length / 2).ToList();
            return near.All(q => i == 0 ? q.U >= -tol : q.U <= length + tol)
                && near.Where(q => q.V > v0 + tol && q.V < v1 - tol).All(q => Math.Abs(q.U - at) <= tol || Math.Abs(q.U - at) > length / 4);
        }
        var bands = Walls.Section(edit.Model, w);
        if (bands.Count < 2) return;
        // Each face's finishes: how far they run past the core's ends (negative: short of them).
        var runOn = new Dictionary<int, double[]>();
        foreach (var side in new[] { true, false })
        {
            var pieces = absorbed.OfType<JsonObject>().Where(a => a["left"] is JsonValue l && l.TryGetValue<bool>(out var b) && b == side
                && Json.Number(a["from"]) != null && Json.Number(a["to"]) != null).ToList();
            if (pieces.Count == 0) continue;
            var band = side ? bands.MaxBy(b => b.To)! : bands.MinBy(b => b.From)!;
            runOn[band.Index] = [-pieces.Min(a => Json.Number(a["from"])!.Value), pieces.Max(a => Json.Number(a["to"])!.Value) - length];
        }
        var taken = Joins.Of(edit.Model, id).Select(r => (string?)r.Attrs?[r.Relating == id ? "relatingConnection" : "relatedConnection"]).ToHashSet();
        var free = new[] { !taken.Contains("ATSTART") && Square(0), !taken.Contains("ATEND") && Square(1) };
        if (!runOn.Values.Any(r => free[0] && Math.Abs(r[0]) > tol || free[1] && Math.Abs(r[1]) > tol)) return;
        var attrs = w.CloneAttrs();
        var (s, e) = (Walls.Start(w), Walls.End(w));
        if (free[0]) { s = start; attrs.Remove(Joins.StartAngle); }
        if (free[1]) { e = end; attrs.Remove(Joins.EndAngle); }
        attrs["start"] = new JsonArray(Math.Round(s.X, 9), Math.Round(s.Y, 9));
        attrs["end"] = new JsonArray(Math.Round(e.X, 9), Math.Round(e.Y, 9));
        // Per layer, [start at its first face, start at its second, end at its first, end at its second]; a joined end's are the join's.
        var count = bands.Max(b => b.Index) + 1;
        attrs[Walls.LayerEndsKey] = new JsonArray([.. Enumerable.Range(0, count).Select(i =>
        {
            var r = runOn.GetValueOrDefault(i) ?? [0, 0];
            double a = free[0] ? Math.Round(r[0], 6) : 0, b = free[1] ? Math.Round(r[1], 6) : 0;
            return (JsonNode)new JsonArray(a, a, b, b);
        })]);
        edit.Replace(w.With(attrs: attrs));
    }

    // ─── Walls ────────────────────────────────────────────────────────────────

    /// <summary>
    /// A wall from what the file gives, in this order: its body as one vertical
    /// prism (the footprint gives thickness and, without an 'Axis', the axis);
    /// else its parts (a wall whose layers are IfcBuildingElementParts, each a
    /// prism: they become the layers, side by side); else its 'Axis' with its
    /// layer set and its body's box (a wall cut by a roof, or in several
    /// bodies). The file's 'Axis' is the reference line when it has one, and
    /// the file's layer usage places the layers against it; without them the
    /// axis runs along the middle of the footprint.
    /// </summary>
    string? Wall(IEdit edit, ForeignElement e, RecognitionContext ctx)
    {
        var storey = edit.Model.Get(e.StoreyId);
        if (storey == null) { ctx.Skip("a wall without a storey"); return null; }
        var axis = e.Axis is { Count: >= 2 } a && (P(a[^1]) - P(a[0])).Length > 1e-3 ? (A: P(a[0]), B: P(a[^1])) : ((Vec2 A, Vec2 B)?)null;
        Vec2 start, end; double thickness, baseZ, height;
        // The file's footprint (for the joins, Finish) and whether its top is its own, flat (no boolean cuts it).
        IReadOnlyList<Vec2>? footprint = null;
        var flatTop = false;
        // The file's top along the axis, when it is its own and not flat (a gable: ForeignWallParts).
        List<(double U, double V)>? topProfile = null;
        // How far each layer's faces run on past the wall's ends, when they end in steps or at a slant (ForeignWallParts).
        List<double[]>? layerEnds = null;
        // Where the parts cut an opening's hole down further than its opening element goes (ForeignWallParts.HoleFloors).
        IReadOnlyDictionary<ForeignOpening, double>? holeFloors = null;
        List<LayerSpec> layers;
        (bool Negative, double Offset) usage;
        if (OnePrismOrNull(e, p => p.Vertical) is { } prism)
        {
            if (Footprints.Axis(Footprints.Plan(prism.Outer)) is not { } fp) { ctx.Skip("a wall without a readable footprint"); return null; }
            var dir = (fp.End - fp.Start).Normalized();
            if (axis is { } ax && Math.Abs(Vec2.Cross((ax.B - ax.A).Normalized(), dir)) < 0.05)
            {
                (start, end) = (ax.A, ax.B);
                dir = (end - start).Normalized();
                var (nMin, nMax) = Across(Footprints.Plan(prism.Outer), start, dir);
                thickness = nMax - nMin;
                layers = Layers(e, thickness, e.Material ?? "Masonry");
                usage = e.Usage is { } u && Math.Abs(e.Layers?.Sum(l => l.Thickness) - thickness ?? 1) < 0.002 ? u : (false, nMin);
            }
            else
            {
                if (fp.Coverage < 0.5) { ctx.Skip("a wall whose footprint has no two long parallel sides (curved, or not a wall)"); return null; }
                (start, end, thickness) = (fp.Start, fp.End, fp.Thickness);
                layers = Layers(e, thickness, e.Material ?? "Masonry");
                usage = (false, -thickness / 2);
            }
            baseZ = prism.BaseZ; height = prism.Height;
            // The file's footprint: its prism's, or as a boolean cuts it (ends trimmed at a corner).
            footprint = prism.Clipped ? BodyFootprint(e) ?? Footprints.Plan(prism.Outer) : Footprints.Plan(prism.Outer);
            flatTop = !prism.Clipped;
        }
        else if (e.Parts is { Count: > 0 } allParts && axis is { } axP && allParts.Any(pt => pt.Extents != null))
        {
            // A wall whose layers are parts: each part a layer, read from its profile (ForeignWallParts), or the wall stays a reference.
            if (ForeignWallParts.Read(e, axP.A, axP.B, out var why) is not { } read) { ctx.Skip(why!); return null; }
            var ad = (axP.B - axP.A).Normalized();
            (start, end) = (axP.A + ad * read.Start, axP.A + ad * read.End);
            layerEnds = read.LayerEnds;
            holeFloors = read.HoleFloors;
            // Its openings must fit it: one left out would leave the file's hole filled (Openings.CheckFits, its length and height).
            var (len, wallBase) = ((end - start).Length, read.BaseZ);
            foreach (var op in e.Openings.Where(o => o.Prisms.Count > 0))
                if (Place(op, start, ad, wallBase, read.Height) is var (al, si, ow, oh, _) && ow > 0 && oh > 0
                    && (al - ow / 2 < -1e-9 || al + ow / 2 > len + 1e-9 || si + oh > read.Height + 1e-9))
                { ctx.Skip("a wall of parts whose openings do not fit it (one at a layer's stepped end, or over its height)"); return null; }
            layers = read.Layers; usage = read.Usage;
            thickness = layers.Sum(l => l.Thickness);
            baseZ = read.BaseZ; height = read.Height;
            footprint = read.Footprint;
            topProfile = read.Top;
            flatTop = true;
        }
        else if (axis is { } ax5 && e.Extents is { } ext)
        {
            // A body that is not a prism (cut by a roof, in several bodies): the file's axis and layers, the body's extents for the rest.
            (start, end) = (ax5.A, ax5.B);
            var fromLayers = e.Layers is { Count: > 0 } ls ? ls.Sum(l => l.Thickness) : 0;
            thickness = fromLayers > 0 ? fromLayers : ext.Across1 - ext.Across0;
            layers = Layers(e, thickness, e.Material ?? "Masonry");
            usage = e.Usage ?? (false, fromLayers > 0 ? -thickness / 2 : ext.Across0);
            baseZ = ext.Z0; height = ext.Z1 - ext.Z0;
            var (es, ed) = (start, (end - start).Normalized());
            footprint = BodyFootprint(e) ?? [.. new[] { (ext.Along0, ext.Across0), (ext.Along1, ext.Across0), (ext.Along1, ext.Across1), (ext.Along0, ext.Across1) }.Select(q => es + ed * q.Item1 + ed.Perp * q.Item2)];
            ctx.Notes.Add($"{(e.Prisms.Count > 0 ? "a wall whose body is not one vertical prism" : "a wall: " + (e.ShapeNote ?? "no prism"))}: made from its axis, its layers and its body's extents ({e.Name ?? e.GlobalId}).");
        }
        else
        {
            var detail = e.Parts is { Count: > 0 } ps ? $" ({ps.Count} parts{(axis == null ? ", no axis" : "")})" : axis == null ? " (no axis)" : "";
            ctx.Skip((e.Prisms.Count == 0 && e.ShapeNote is { } why ? $"a wall: {why}" : e.Prisms.Count > 0 ? "a wall whose body is not one vertical prism" : "a wall without a body") + detail);
            return null;
        }
        var length = (end - start).Length;
        if (thickness < MinThickness || thickness > MaxThickness || length < Math.Min(thickness, 0.3)) { ctx.Skip("a wall whose footprint is not a wall's (too thick for its length)"); return null; }
        var baseOffset = Math.Round(baseZ - storey.Num("elevation"), 6);
        height = Math.Round(height, 6);
        if (!(height > 0)) { ctx.Skip("a wall without a height"); return null; }
        var type = WallType(edit, e, layers, thickness, ctx);
        var predefined = e.PredefinedType is "STANDARD" or "SOLIDWALL" or "PARTITIONING" or "ELEMENTEDWALL" or "PLUMBINGWALL" or "SHEAR" or "MOVABLE" or "PARAPET" or "POLYGONAL" ? e.PredefinedType : "STANDARD";
        var wallAttrs = Walls.Attrs(start, end, height, baseOffset);
        // A top the file keeps flat stays so under whatever is over it (an imported roof, a slab): rising to it, or cut by it, is not the file's wall.
        if (flatTop) wallAttrs["roofTop"] = "none";
        if (topProfile != null) wallAttrs[WallTops.TopProfileKey] = WallTops.ProfileJson(topProfile);
        if (layerEnds != null) wallAttrs[Walls.LayerEndsKey] = new JsonArray([.. layerEnds.Select(x => (JsonNode)new JsonArray([.. x.Select(v => (JsonNode)v)]))]);
        var wall = new Entity(e.GlobalId, IfcClass.Wall, e.Name ?? type.Name, wallAttrs, predefined);
        edit.Add(wall);
        if (footprint is { Count: >= 3 }) ctx.Footprints[wall.Id] = footprint;
        Spatial.Contain(edit, storey.Id, wall.Id);
        edit.Relate(IfcClass.RelDefinesByType, type.Id, wall.Id);
        // The layers where the file has them: the usage's sense and offset against the reference line.
        LayerSets.LayOut(edit, wall, type, "AXIS2", (_, _) => Math.Round(usage.Offset, 6), usage.Negative);
        ctx.Made.Add(wall.Id);
        var d = (end - start).Normalized();
        foreach (var op in e.Openings) Opening(edit, wall, start, d, storey.Num("elevation") + baseOffset, height, op, ctx, holeFloors?.GetValueOrDefault(op));
        return wall.Id;
    }

    static Vec2 P(Vec3 v) => new(v.X, v.Y);

    /// <summary>The outline round a wall's body as the file cuts it, at its base (its ends as a boolean trims them), when the file's body gave one.</summary>
    static IReadOnlyList<Vec2>? BodyFootprint(ForeignElement e) =>
        e.Footprint is { Count: > 0 } loops && loops.SelectMany(l => l).ToList() is { Count: >= 3 } pts && Polygon2.ConvexHull(pts) is { Length: >= 3 } hull ? hull : null;

    /// <summary>The polygon's extent across a line: the least and the greatest distance left of it (negative right).</summary>
    static (double Min, double Max) Across(IReadOnlyList<Vec2> plan, Vec2 origin, Vec2 dir)
    {
        var n = new Vec2(-dir.Y, dir.X);
        double min = double.MaxValue, max = double.MinValue;
        foreach (var q in plan) { var v = Vec2.Dot(q - origin, n); min = Math.Min(min, v); max = Math.Max(max, v); }
        return (min, max);
    }

    /// <summary>The file's layers when they add up to the thickness; else one layer of the material.</summary>
    static List<LayerSpec> Layers(ForeignElement e, double thickness, string material) =>
        e.Layers is { Count: > 0 } ls && Math.Abs(ls.Sum(l => l.Thickness) - thickness) < 0.002
            ? ls.Select(l => new LayerSpec(l.Material, Math.Round(l.Thickness, 4), l.Core)).ToList()
            : [new LayerSpec(material, Math.Round(thickness, 4), true)];

    /// <summary>The wall type: the file's type by name, else one per layers; made once per import, or found by name in the project.</summary>
    Entity WallType(IEdit edit, ForeignElement e, List<LayerSpec> layers, double thickness, RecognitionContext ctx)
    {
        if (!layers.Any(l => l.Core)) layers = layers.Select((l, i) => i == layers.IndexOf(layers.OrderByDescending(x => x.Thickness).First()) ? l with { Core = true } : l).ToList();
        var signature = string.Join("|", layers.Select(l => $"{l.Material}:{l.Thickness}{(l.From != null || l.To != null ? $"@{l.From}-{l.To}" : "")}"));
        var key = e.TypeName is { Length: > 0 } tn ? "wall:type:" + tn + ":" + signature : "wall:" + signature;
        if (ctx.TypeIds.TryGetValue(key, out var id) && edit.Model.Get(id) is { } known) return known;
        var name = e.TypeName is { Length: > 0 } n ? n : layers.Count == 1 ? $"{layers[0].Material} {thickness * 1000:0}" : $"{string.Join(" + ", layers.Select(l => l.Material))} {thickness * 1000:0}";
        var existing = edit.Model.OfClass(IfcClass.WallType).FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase) && LayerSets.Of(edit.Model, t.Id) is { } set && Lapin.Model.Layers.Read(set) is var have && Math.Abs(have.Sum(l => l.Thickness) - thickness) < 1e-6
            // Layers of their own heights (a wall of parts): the type's the same.
            && (layers.All(l => l.From == null && l.To == null) && have.All(l => l.From == null && l.To == null) || have.Select(l => (l.From, l.To)).SequenceEqual(layers.Select(l => (l.From, l.To)))));
        // No lintels of Lapin's over its openings (the file has none in this wall: a lintel it models is an element of its own);
        // the type's rule, to be turned on afterwards.
        var type = existing ?? WallTypes.Create(edit, name, layers, lintels: NoLintel.ToJson());
        if (existing == null) ctx.CreatedTypes.Add(type.Id);
        ctx.TypeIds[key] = type.Id;
        return type;
    }

    /// <summary>An opening from its box in the wall's frame; a door or a window in it when the file has one, a bare opening else.</summary>
    void Opening(IEdit edit, Entity wall, Vec2 start, Vec2 dir, double baseZ, double wallHeight, ForeignOpening op, RecognitionContext ctx, double? holeFloor = null)
    {
        if (op.Prisms.Count == 0) { ctx.Skip("an opening without a prism body"); return; }
        var (along, sill, w, h, kind) = Place(op, start, dir, baseZ, wallHeight);
        // The hole down to where the wall's parts cut it (a door on the finished floor in a hole from the slab), its fill where the file's opening puts it.
        var fillOffset = 0.0;
        if (holeFloor is { } hf && Math.Round(Math.Max(0, hf - baseZ), 4) is var floorSill && floorSill < sill - 5e-4)
            (fillOffset, h, sill) = (Math.Round(sill - floorSill, 4), Math.Round(h + sill - floorSill, 4), floorSill);
        if (!(w > 0 && h > 0)) { ctx.Skip($"a {kind} without a size"); return; }
        try { Openings.CheckFits(edit.Model, wall, along, sill, w, h, alone: true); }
        catch (CommandException ex) { ctx.Skip($"a {kind} that does not fit its wall ({ex.Message.TrimEnd('.')})"); return; }
        // The hole is the file's opening, its own size and sill; its door or window the fill's size (OverallWidth/Height),
        // which may be smaller (a frame set in a larger structural opening) or larger (a door body running past the hole).
        var (fw, fh) = (op.Fill?.Width is double fwv && fwv > 0 ? Math.Round(fwv, 4) : w, op.Fill?.Height is double fhv && fhv > 0 ? Math.Round(fhv, 4) : h);
        var hole = new JsonObject { ["along"] = along, ["sill"] = sill };
        if (kind == "opening") { hole["width"] = w; hole["height"] = h; }
        else
        {
            if (Math.Abs(fw - w) > 5e-4) hole[Openings.HoleWidthKey] = w;
            if (Math.Abs(fh - h) > 5e-4) hole[Openings.HoleHeightKey] = h;
            if (fillOffset > 0) hole[Openings.FillOffsetKey] = fillOffset;
        }
        var opening = new Entity(op.GlobalId is { Length: 22 } og && edit.Model.Get(og) == null ? og : edit.NewId(), IfcClass.OpeningElement, op.Name ?? "Opening", hole, "OPENING");
        edit.Add(opening);
        edit.AddRelation(new Relation(edit.NewId(), IfcClass.RelVoidsElement, wall.Id, [opening.Id]));
        if (op.Fill is not { } fill) { ctx.Made.Add(opening.Id); return; }
        var type = FillType(edit, fill, kind, fw, fh, kind == "window" ? sill + fillOffset : 0, ctx);
        var cls = kind == "door" ? IfcClass.Door : IfcClass.Window;
        var spec = JoinerySpec.From(type.Attrs["spec"]);
        var marks = edit.Model.OfClass(cls).Count() + 1;
        // The type has the file's hand (as seen the way its panels open); the element opens the way the file's +Y points:
        // the wall's left unflipped, so the other way flips its side, and its hand with it to keep the file's.
        var flip = fill.OpensTowards is { } towards && Vec2.Dot(towards, Openings.FrameOf(edit.Model, wall, along).Left) < 0;
        var made = new Entity(fill.GlobalId is { Length: 22 } fg && edit.Model.Get(fg) == null ? fg : edit.NewId(), cls, fill.Name ?? $"{spec?.Mark}{marks}", new JsonObject { ["flipHand"] = flip, ["flipSide"] = flip }, kind == "door" ? "DOOR" : "WINDOW");
        edit.Add(made);
        edit.AddRelation(new Relation(edit.NewId(), IfcClass.RelFillsElement, opening.Id, [made.Id]));
        edit.Relate(IfcClass.RelDefinesByType, type.Id, made.Id);
        if (Spatial.StoreyOf(edit.Model, wall.Id) is { } storey) Spatial.Contain(edit, storey.Id, made.Id);
        var attrs = made.CloneAttrs();
        attrs["imported"] = new JsonObject { ["ifcClass"] = fill.IfcClass, ["name"] = fill.Name, ["typeName"] = fill.TypeName, ["operation"] = fill.Operation, ["userDefinedOperation"] = fill.UserDefined, ["properties"] = fill.Properties.DeepClone() };
        edit.Replace(made.With(attrs: attrs));
        ctx.Made.Add(made.Id);
    }

    /// <summary>Where an opening goes in the wall's frame (along, sill), how big it is (its box's, inside the wall's height: the file's hole) and its kind, from its prisms.</summary>
    static (double Along, double Sill, double Width, double Height, string Kind) Place(ForeignOpening op, Vec2 start, Vec2 dir, double baseZ, double wallHeight)
    {
        double aMin = double.MaxValue, aMax = double.MinValue, zMin = double.MaxValue, zMax = double.MinValue;
        foreach (var p in op.Prisms)
            foreach (var v in p.Outer.Concat(p.Outer.Select(q => q + p.Sweep)))
            {
                var a = Vec2.Dot(new Vec2(v.X, v.Y) - start, dir);
                aMin = Math.Min(aMin, a); aMax = Math.Max(aMax, a); zMin = Math.Min(zMin, v.Z); zMax = Math.Max(zMax, v.Z);
            }
        var width = Math.Round(aMax - aMin, 4);
        var height = Math.Round(Math.Min(zMax, baseZ + wallHeight) - Math.Max(zMin, baseZ), 4);
        var along = Math.Round((aMin + aMax) / 2, 4);
        var sill = Math.Round(Math.Max(0, zMin - baseZ), 4);
        var kind = op.Fill is { } fl ? (fl.IfcClass.StartsWith("IfcDoor") ? "door" : "window") : "opening";
        // A door's sill too is where the file's hole starts (a door on the finished floor over a wall standing on the slab).
        return (along, sill, width, height, kind);
    }

    /// <summary>
    /// A door or window type: the file's by name, else one per size and operation; its panels
    /// from what the file says of its operation (<see cref="JoineryIfc.FromIfc"/>), the rest
    /// from Lapin's default of the kind at that size.
    /// </summary>
    Entity FillType(IEdit edit, ForeignFill fill, string kind, double w, double h, double sill, RecognitionContext ctx)
    {
        var size = $"{w * 1000:0}×{h * 1000:0}";
        var how = $"{fill.Operation}:{fill.UserDefined}:{string.Join(",", (fill.Panels ?? []).Select(p => $"{p.Operation}/{p.Position}/{p.Width}"))}";
        var key = fill.TypeName is { Length: > 0 } tn ? $"{kind}:type:{tn}:{size}:{how}" : $"{kind}:{size}:{how}";
        if (ctx.TypeIds.TryGetValue(key, out var id) && edit.Model.Get(id) is { } known) return known;
        var name = fill.TypeName is { Length: > 0 } n ? n : kind == "door" ? $"Door {size}" : $"Window {size}";
        var cls = kind == "door" ? IfcClass.DoorType : IfcClass.WindowType;
        var existing = edit.Model.OfClass(cls).FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase) && JoinerySpec.From(t.Attrs["spec"]) is { } s && Math.Abs(s.Width - w) < 1e-6 && Math.Abs(s.Height - h) < 1e-6);
        if (existing != null) { ctx.TypeIds[key] = existing.Id; return existing; }
        // No reveal finish of Lapin's: the file's opening is the hole through every layer as it draws it, its finishes cut
        // flush there; a reveal would widen the wall's hole by the finish and take the jambs' masonry (docs/spec-ifc.md).
        // Nor anything else of Lapin's the file does not have (the import brings the file as it is): no lintel over its
        // opening, no window boards, no threshold; its U-value and whether it is external only as the file says. Each is
        // the type's own setting, to be turned on afterwards.
        var common = kind == "door" ? "Pset_DoorCommon" : "Pset_WindowCommon";
        var basis = kind == "door" ? JoinerySpec.DefaultDoor(name) : JoinerySpec.DefaultWindow(name);
        var spec = basis with
        {
            Width = Math.Round(w, 4), Height = Math.Round(h, 4), SillHeight = Math.Round(sill, 4),
            Details = new JoineryDetails(Reveal: new JoineryReveal("none")),
            Lintel = NoLintel,
            Board = new JoineryBoard(0, 0),
            Threshold = 0,
            ThermalTransmittance = Number(fill.Properties, common, "ThermalTransmittance") is > 0 and var u ? u : null,
            IsExternal = Flag(fill.Properties, common, "IsExternal") ?? basis.IsExternal,
        };
        if (JoineryIfc.FromIfc(spec, fill.Operation, fill.UserDefined, fill.Panels) is { } told && Joinery.Problem(told) == null) spec = told;
        // Its material per part as the file's constituent set says (report #76: Frame, Sash, Glazing, Panel…; IFC4's Lining and
        // Framing): the parts it names take the file's materials, the others Lapin's shared ones.
        if (fill.Constituents is { Count: > 0 } constituents)
        {
            var given = new Dictionary<string, string>();
            foreach (var (cname, material) in constituents)
                if (JoineryConstituents.Part(cname, kind == "door") is { } part) given.TryAdd(part, Materials.Ensure(edit, material).Name);
            var mats = given.Aggregate(new JoineryMaterials(), (acc, kv) => acc.With(kv.Key, kv.Value));
            if (given.Count > 0)
            {
                var shared = Openings.WithDefaultMaterials(edit, spec with { Materials = null }).Materials!;
                spec = spec with
                {
                    Materials = new JoineryMaterials(mats.Frame ?? shared.Frame, mats.Glass ?? shared.Glass, mats.Leaf ?? shared.Leaf, mats.Hardware ?? shared.Hardware, mats.Board ?? shared.Board, mats.Sash),
                };
            }
        }
        var type = Openings.CreateType(edit, spec);
        ctx.CreatedTypes.Add(type.Id);
        ctx.TypeIds[key] = type.Id;
        return type;
    }

    // ─── Floors ──────────────────────────────────────────────────────────────

    string? Slab(IEdit edit, ForeignElement e, RecognitionContext ctx)
    {
        if (!OnePrism(e, ctx, p => p.Vertical, "a floor whose body is not one vertical prism", out var prism)) return null;
        var thickness = Math.Round(prism.Height, 4);
        if (thickness < MinSlabThickness || thickness > MaxSlabThickness) { ctx.Skip("a floor whose thickness is not a floor's"); return null; }
        var contour = Footprints.Plan(prism.Outer);
        if (contour.Count < 3) { ctx.Skip("a floor without a readable contour"); return null; }
        var storey = edit.Model.Get(e.StoreyId);
        if (storey == null) { ctx.Skip("a floor without a storey"); return null; }
        var offset = Math.Round(prism.TopZ - storey.Num("elevation"), 6);
        var type = SlabType(edit, e, thickness, ctx);
        var slab = new Entity(e.GlobalId, IfcClass.Slab, e.Name ?? type.Name, new JsonObject { ["contour"] = Slabs.ContourJson(contour), ["offset"] = offset }, e.PredefinedType is "ROOF" or "LANDING" or "BASESLAB" ? e.PredefinedType : "FLOOR");
        edit.Add(slab);
        Spatial.Contain(edit, storey.Id, slab.Id);
        Slabs.SetType(edit, slab, type);
        foreach (var hole in prism.Holes)
        {
            var ring = Footprints.Plan(hole);
            if (ring.Count < 3) continue;
            try { Slabs.AddOpening(edit, slab, ring); } catch (CommandException ex) { ctx.Notes.Add($"Floor {e.Name}: {ex.Message}"); }
        }
        ctx.Made.Add(slab.Id);
        return slab.Id;
    }

    Entity SlabType(IEdit edit, ForeignElement e, double thickness, RecognitionContext ctx)
    {
        var layers = e.Layers is { Count: > 0 } ls && Math.Abs(ls.Sum(l => l.Thickness) - thickness) < 0.002
            ? ls.Select(l => new LayerSpec(l.Material, Math.Round(l.Thickness, 4), l.Core)).ToList()
            : [new LayerSpec(e.Material ?? "Reinforced concrete", thickness, true)];
        var key = e.TypeName is { Length: > 0 } tn ? "slab:type:" + tn : "slab:" + string.Join("|", layers.Select(l => $"{l.Material}:{l.Thickness}"));
        if (ctx.TypeIds.TryGetValue(key, out var id) && edit.Model.Get(id) is { } known) return known;
        var name = e.TypeName is { Length: > 0 } n ? n : $"{layers[0].Material} {thickness * 1000:0}";
        var existing = edit.Model.OfClass(IfcClass.SlabType).FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
        var type = existing ?? Slabs.CreateType(edit, name, layers);
        if (existing == null) ctx.CreatedTypes.Add(type.Id);
        ctx.TypeIds[key] = type.Id;
        return type;
    }

    // ─── Shared ──────────────────────────────────────────────────────────────

    /// <summary>The element's one prism of the wanted kind (a box offers three; one with a hole only one), or a reason.</summary>
    internal static bool OnePrism(ForeignElement e, RecognitionContext ctx, Func<ShapePrism, bool> wanted, string reason, out ShapePrism prism)
    {
        prism = null!;
        if (e.Prisms.Count == 0) { ctx.Skip(e.ShapeNote is { } why ? $"{Label(e)}: {why}" : reason); return false; }
        // Several decompositions of the same body (a B-rep box) share their vertices; several bodies do not.
        var bodies = e.Prisms.GroupBy(p => p.Source == "brep" ? Key(p) : Guid.NewGuid().ToString()).ToList();
        if (bodies.Count > 1) { ctx.Skip($"{Label(e)} made of several bodies"); return false; }
        var candidates = e.Prisms.Where(wanted).ToList();
        if (candidates.Count == 0) { ctx.Skip(reason); return false; }
        prism = candidates.OrderByDescending(p => Math.Abs(Polygon2.SignedArea(Footprints.Plan(p.Outer)))).First();
        return true;
    }

    /// <summary>The element's one prism of the wanted kind, or null (several bodies, none of the kind, no prism).</summary>
    static ShapePrism? OnePrismOrNull(ForeignElement e, Func<ShapePrism, bool> wanted) => OnePrismOrNull(e.Prisms, wanted);

    /// <summary>The one prism of the wanted kind of a body (an element's, a part's), or null (several bodies, none of the kind, no prism).</summary>
    internal static ShapePrism? OnePrismOrNull(IReadOnlyList<ShapePrism> prisms, Func<ShapePrism, bool> wanted)
    {
        if (prisms.Count == 0) return null;
        var bodies = prisms.GroupBy(p => p.Source == "brep" ? Key(p) : Guid.NewGuid().ToString()).Count();
        if (bodies > 1) return null;
        return prisms.Where(wanted).OrderByDescending(p => Math.Abs(Polygon2.SignedArea(Footprints.Plan(p.Outer)))).FirstOrDefault();
    }

    static string Label(ForeignElement e) => e.IfcClass.Length > 3 ? "a " + e.IfcClass[3..].ToLowerInvariant() : e.IfcClass;

    static string Key(ShapePrism p)
    {
        var pts = p.Outer.Concat(p.Outer.Select(q => q + p.Sweep)).Select(q => (Math.Round(q.X, 4), Math.Round(q.Y, 4), Math.Round(q.Z, 4))).OrderBy(t => t).ToList();
        return string.Join(";", pts.Take(8));
    }
}
