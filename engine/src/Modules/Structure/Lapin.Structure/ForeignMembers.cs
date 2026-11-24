using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Structure;

/// <summary>
/// Foreign IFC columns, beams and pad footings rebuilt natively
/// (docs/spec-ifc.md, Recunoașterea). A column: a vertical prism with a
/// rectangular, regular polygonal or round base, its section and rotation from
/// the base, its base offset from the storey and its height its own where its
/// type's is another. A beam: a prism swept horizontally, its section from the
/// profile (a rectangle in its plane, else a reference), its axis from the
/// sweep; or swept vertically, its axis along the long side of the footprint;
/// of a box's prisms the one whose section is its file type's, else the
/// longest. A footing: a vertical prism on a rectangle, one step, where the file
/// puts it (it does not follow a column over it, decision 507). Each keeps
/// the file's GlobalId as its id. The columns and beams of one file type share
/// a native type while their sections are the same (a column's rectangle
/// turned a quarter is the same); one of another section takes a type of its
/// own, named after the file's with its section.
/// </summary>
public sealed class ForeignMembers : IIfcRecognizer
{
    public bool Takes(string ifcClass) => ifcClass is IfcClass.Column or IfcClass.Beam or "IfcMember" or IfcClass.Footing;

    public string? Recognize(IEdit edit, ForeignElement e, RecognitionContext ctx) => e.IfcClass switch
    {
        IfcClass.Column => Column(edit, e, ctx),
        IfcClass.Beam or "IfcMember" => Beam(edit, e, ctx),
        IfcClass.Footing => Footing(edit, e, ctx),
        _ => null,
    };

    /// <summary>
    /// The beams joined as the file has them (<see cref="FoundJoins"/>, as the walls are): a join only where it leaves both
    /// on their footprints, the free ends square where the file's bodies end.
    /// </summary>
    public void Finish(IEdit edit, RecognitionContext ctx)
    {
        if (ctx.Services.Get<IPathElements>() is not { } paths) return;
        // From triangles (OutlineSections) there are no connections to keep, only bodies: joining them would be Lapin's own, and
        // each beam ends where its body does.
        if (ctx.OutlineSections) return;
        var beams = ctx.Made.Where(id => edit.Model.Get(id)?.Class == IfcClass.Beam).ToList();
        try { FoundJoins.Make(edit, paths, beams, ctx.Footprints); }
        catch (CommandException ex) { ctx.Notes.Add($"Beams not joined: {ex.Message}"); }
    }

    /// <summary>
    /// How near a chorded circle's area (as Lapin draws it) must be to the file's polygon to be read as round: the fidelity
    /// gate's default volume tolerance (docs/spec-ifc.md, Poarta de fidelitate), so a round column is never one it fails.
    /// </summary>
    const double RoundTolerance = 0.005;

    /// <summary>A column's section in plan, where it stands and how it is turned; null when it is none Lapin has.</summary>
    static (Section Section, Vec2 At, double Rotation)? ColumnSection(IReadOnlyList<Vec2> plan)
    {
        if (Footprints.Rectangle(plan) is { } r) return (Section.Rectangle(Round(r.Width), Round(r.Depth)), r.Centre, Math.Round(r.RotationDeg, 3));
        var area = Math.Abs(Polygon2.SignedArea(plan));
        bool Fits(Section c) => Math.Abs(Math.Abs(Polygon2.SignedArea(c.Outline())) - area) <= RoundTolerance * area;
        // A regular polygon: a faceted round column when Lapin's circle has its area, else the polygon itself (an octagonal post).
        if (Footprints.RegularPolygon(plan) is { } g)
        {
            var circle = Section.Circle(Round(2 * g.Radius));
            if (g.Sides >= 8 && Fits(circle)) return (circle, g.Centre, 0);
            if (g.Sides <= 64) return (Section.Polygon(Round(2 * g.Radius), g.Sides), g.Centre, Math.Round(g.RotationDeg, 3));
        }
        if (Footprints.Circle(plan) is { } c && Section.Circle(Round(c.Diameter)) is var round && Fits(round)) return (round, c.Centre, 0);
        return null;
    }

    /// <summary>
    /// A column's own section from its plan (<see cref="RecognitionContext.OutlineSections"/>): its type's catalogue profile when
    /// that is the plan, turned as the plan is (within 2 mm), else the plan itself as an outline about its box's centre; null for
    /// a plan with too few corners. Its place is the centre of the plan's box along the turn.
    /// </summary>
    static (Section Section, Vec2 At, double Rotation)? OwnSection(IReadOnlyList<Vec2> plan, string? typeName)
    {
        if (plan.Count < 3) return null;
        var poly = Polygon2.EnsureCcw([.. plan]);
        // The turn: the edge whose direction gives the least box (the plan's own axes).
        var (angle, best) = (0.0, double.MaxValue);
        for (var i = 0; i < poly.Count; i++)
        {
            var d = poly[(i + 1) % poly.Count] - poly[i];
            if (d.Length < 1e-6) continue;
            var a = Math.Atan2(d.Y, d.X);
            var (c, s) = (Math.Cos(-a), Math.Sin(-a));
            var r = poly.Select(p => new Vec2(p.X * c - p.Y * s, p.X * s + p.Y * c)).ToList();
            var boxArea = (r.Max(p => p.X) - r.Min(p => p.X)) * (r.Max(p => p.Y) - r.Min(p => p.Y));
            if (boxArea < best - 1e-9) (best, angle) = (boxArea, a);
        }
        // Quarter turns kept to (-45°, 45°]: a plan's axes are the same whichever edge gave them.
        while (angle > Math.PI / 4 + 1e-9) angle -= Math.PI / 2;
        while (angle <= -Math.PI / 4 + 1e-9) angle += Math.PI / 2;
        var (cos, sin) = (Math.Cos(-angle), Math.Sin(-angle));
        var local = poly.Select(p => new Vec2(p.X * cos - p.Y * sin, p.X * sin + p.Y * cos)).ToList();
        var centreLocal = new Vec2((local.Max(p => p.X) + local.Min(p => p.X)) / 2, (local.Max(p => p.Y) + local.Min(p => p.Y)) / 2);
        var at = new Vec2(centreLocal.X * Math.Cos(angle) - centreLocal.Y * Math.Sin(angle), centreLocal.X * Math.Sin(angle) + centreLocal.Y * Math.Cos(angle));
        var ring = Polygon2.EnsureCcw([.. local.Select(p => new Vec2(Math.Round(p.X - centreLocal.X, 6), Math.Round(p.Y - centreLocal.Y, 6)))]);
        var width = Round(local.Max(p => p.X) - local.Min(p => p.X));
        var depth = Round(local.Max(p => p.Y) - local.Min(p => p.Y));
        var degrees = Math.Round(angle * 180 / Math.PI, 3);
        var mine = new Section("outline", width, depth, Name: "Outline") { Rings = [ring] };
        // The type's catalogue profile, at each quarter turn, when it is this outline.
        if (Catalogue(typeName) is { } profile)
            foreach (var q in new[] { 0, 90, 180, 270 })
            {
                var rad = q * Math.PI / 180;
                var turned = profile.Outline().Select(p => new Vec2(p.X * Math.Cos(rad) - p.Y * Math.Sin(rad), p.X * Math.Sin(rad) + p.Y * Math.Cos(rad))).ToList();
                if (SameOutline(turned, ring, 0.002)) return (profile, at, Math.Round(degrees + q, 3));
            }
        return (mine, at, degrees);
    }

    /// <summary>A beam's own section, or its type's catalogue profile where that is the same outline as it stands (within 2 mm).</summary>
    static Section CatalogueOr(Section own, string? typeName) =>
        Catalogue(typeName) is { } profile && own.Rings is [{ } ring, ..] && SameOutline(profile.Outline(), ring, 0.002) ? profile : own;

    /// <summary>What a type made for a section is known by: its label, and for an outline its rings too (two shapes in one box are two types).</summary>
    static string KeyOf(Section s) => s.Kind == "outline" && s.Rings is { } rings
        ? $"{s.Label}:{string.Join(";", rings.Select(r => string.Join(" ", r.Select(p => FormattableString.Invariant($"{Math.Round(p.X, 5)},{Math.Round(p.Y, 5)}")))))}"
        : s.Label;

    /// <summary>The built-in library's profile the type is named after (IPE 300, HEA 200…), as a section; null when none is.</summary>
    static Section? Catalogue(string? typeName)
    {
        if (typeName is not { Length: > 0 } name) return null;
        // A type named as Revit's are, "family: type" or with a size after the profile, tried as it is, then by its last part.
        foreach (var candidate in new[] { name, name.Split(':').Last().Trim() })
            if (Lapin.Platform.ProfileLibraries.BuiltInProfiles.Find(candidate) is { } p)
            {
                try { return Section.Parse(p.SectionJson(null)); }
                catch (CommandException) { return null; }
            }
        return null;
    }

    /// <summary>Whether every corner of each outline lies within <paramref name="tol"/> of the other's edges.</summary>
    static bool SameOutline(IReadOnlyList<Vec2> a, IReadOnlyList<Vec2> b, double tol)
    {
        static double Distance(Vec2 p, IReadOnlyList<Vec2> poly)
        {
            var best = double.MaxValue;
            for (var i = 0; i < poly.Count; i++)
            {
                Vec2 s = poly[i], t = poly[(i + 1) % poly.Count];
                var st = t - s;
                var k = Math.Clamp(Vec2.Dot(p - s, st) / Math.Max(1e-12, Vec2.Dot(st, st)), 0, 1);
                best = Math.Min(best, (s + st * k - p).Length);
            }
            return best;
        }
        return a.Count >= 3 && b.Count >= 3 && a.All(p => Distance(p, b) <= tol) && b.All(p => Distance(p, a) <= tol);
    }

    /// <summary>Whether two sections are one (to 0.1 mm); two outlines only when their rings are the same too (two steel shapes in one box are not one).</summary>
    static bool Same(Section? a, Section b) => a != null && (a.Kind != "outline" && b.Kind != "outline" ? true : a.Equals(b)) && a.Kind == b.Kind && a.Sides == b.Sides
        && Math.Abs(a.Width - b.Width) < 1e-4 && Math.Abs(a.Depth - b.Depth) < 1e-4 && Math.Abs(a.Web - b.Web) < 1e-4 && Math.Abs(a.Flange - b.Flange) < 1e-4;

    string? Column(IEdit edit, ForeignElement e, RecognitionContext ctx)
    {
        if (!One(e, ctx, p => p.Vertical, "a column whose body is not one vertical prism", out var prism)) return null;
        if (prism.Holes.Count > 0) { ctx.Skip("a column with holes in its section"); return null; }
        if ((ColumnSection(Footprints.Plan(prism.Outer)) ?? (ctx.OutlineSections ? OwnSection(Footprints.Plan(prism.Outer), e.TypeName) : null)) is not { } read)
        {
            ctx.Skip("a column whose section is not a rectangle, a regular polygon or a circle");
            return null;
        }
        var (section, at, rotation) = read;
        // Bodies that are only triangles (OutlineSections): a faceted round read as Lapin's circle only where Lapin's chords are its
        // facets (to 1 mm), else as the polygon it is (Revit's facets are not Lapin's: a 400 mm column 397 mm across one way), else its outline.
        if (ctx.OutlineSections && section.Kind == "circle")
        {
            var plan = Footprints.Plan(prism.Outer);
            var chords = section.Outline().Select(p => p + at).ToList();
            if (!SameOutline(chords, plan, 0.001))
            {
                if (Footprints.RegularPolygon(plan) is { Sides: <= 64 } g
                    && Section.Polygon(Round(2 * g.Radius), g.Sides) is var polygon
                    && SameOutline([.. Place.Placed(polygon.Outline(), g.Centre, Math.Round(g.RotationDeg, 3))], plan, 0.001))
                    (section, at, rotation) = (polygon, g.Centre, Math.Round(g.RotationDeg, 3));
                else if (OwnSection(plan, null) is { } own) (section, at, rotation) = own;
            }
        }
        var storey = edit.Model.Get(e.StoreyId);
        if (storey == null) { ctx.Skip("a column without a storey"); return null; }
        var height = Round(prism.Height);
        // Its file type's section, or that rectangle turned a quarter (the file's first edge the other side).
        var turned = section.Kind == "rectangle" ? Section.Rectangle(section.Depth, section.Width) : null;
        var type = Type(edit, ctx, IfcClass.ColumnType, e, $"column:{KeyOf(section)}:{height}:{e.Material}", $"Column {section.Label}", section.Label,
            name => Columns.CreateType(edit, name, section, height, e.Material ?? (section.IsProfile ? "Steel" : "Reinforced concrete")),
            t => Section.From(t.Attrs["section"]) is var ts && (Same(ts, section) || turned != null && Same(ts, turned)));
        if (turned != null && !Same(Section.From(type.Attrs["section"]), section)) rotation = Math.Round(((rotation + 90) % 360 + 360) % 360, 3);
        // Where the file has it: its base as an offset from its storey, and its own height where its type's (the first of
        // its file type's columns) is another.
        var attrs = new JsonObject { ["at"] = Place.Json2(at), ["rotation"] = rotation };
        var baseOffset = Math.Round(prism.BaseZ - storey.Num("elevation"), 6);
        if (Math.Abs(baseOffset) > 1e-6) attrs["baseOffset"] = baseOffset;
        if (Math.Abs((type.NumOrNull("height") ?? 0) - height) > 1e-6) attrs["height"] = height;
        var column = new Entity(e.GlobalId, IfcClass.Column, e.Name ?? type.Name, attrs, "COLUMN");
        edit.Add(column);
        Spatial.Contain(edit, storey.Id, column.Id);
        edit.Relate(IfcClass.RelDefinesByType, type.Id, column.Id);
        ctx.Made.Add(column.Id);
        return column.Id;
    }

    string? Beam(IEdit edit, ForeignElement e, RecognitionContext ctx)
    {
        static bool Lying(ShapePrism p) => p.Horizontal || p.Vertical;
        if (!One(e, ctx, Lying, "a beam whose body is not one prism", out var prism)) return null;
        var storey = edit.Model.Get(e.StoreyId);
        if (storey == null) { ctx.Skip("a beam without a storey"); return null; }
        var key = TypeKey(IfcClass.BeamType, e);
        // A box is three prisms: the one whose section is its file type's (a short stub of a long beam's type), else the longest.
        if (key != null && ctx.TypeIds.TryGetValue(key, out var known) && Section.From(edit.Model.Get(known)?.Attrs["section"]) is { } typed)
        {
            foreach (var p in e.Prisms.Where(Lying).OrderByDescending(p => p.Height))
                if (ReadBeam(p) is { } read && Same(typed, Section.Rectangle(read.Width, read.Depth))) { prism = p; break; }
        }
        var own = ctx.OutlineSections ? new Section?[1] : null;
        if (ReadBeam(prism, ctx, own) is not { } beamRead) return null;
        var (start, end, width, depth, top, footprint) = beamRead;
        var section = own?[0] is { } shaped ? CatalogueOr(shaped, e.TypeName) : Section.Rectangle(width, depth);
        var type = Type(edit, ctx, IfcClass.BeamType, e, $"beam:{KeyOf(section)}:{e.Material}", $"Beam {section.Label}", section.Label,
            name => Beams.CreateType(edit, name, section, e.Material ?? "Reinforced concrete"), t => Same(Section.From(t.Attrs["section"]), section));
        var topOffset = Math.Round(top - storey.Num("elevation") - storey.Num("height", 3), 6);
        var beamAttrs = new JsonObject { ["start"] = Place.Json2(start), ["end"] = Place.Json2(end), ["topOffset"] = topOffset };
        // From triangles, its body is what it ends as: it does not stop at a column's face (the bodies show where each ends).
        if (ctx.OutlineSections) beamAttrs[Beams.ThroughColumnsKey] = true;
        var beam = new Entity(e.GlobalId, IfcClass.Beam, e.Name ?? type.Name, beamAttrs, "BEAM");
        edit.Add(beam);
        Spatial.Contain(edit, storey.Id, beam.Id);
        edit.Relate(IfcClass.RelDefinesByType, type.Id, beam.Id);
        if (footprint.Count >= 3) ctx.Footprints[beam.Id] = footprint;
        ctx.Made.Add(beam.Id);
        return beam.Id;
    }

    /// <summary>
    /// A beam read from one prism: its axis in plan, its section (width across, depth up), its top, and its footprint in plan
    /// (for the joins); null, with the reason when <paramref name="ctx"/> is given, when it is no beam Lapin has.
    /// </summary>
    /// <param name="outline">When given (<see cref="RecognitionContext.OutlineSections"/>), a profile that is no rectangle is kept in its first slot as an outline section instead of refused.</param>
    static (Vec2 Start, Vec2 End, double Width, double Depth, double Top, IReadOnlyList<Vec2> Footprint)? ReadBeam(ShapePrism prism, RecognitionContext? ctx = null, Section?[]? outline = null)
    {
        Vec2 start, end; double width, depth, top;
        IReadOnlyList<Vec2> footprint;
        if (prism.Horizontal)
        {
            // The profile stands in a vertical plane: width across the sweep in plan, depth up it.
            var dir = new Vec2(prism.Sweep.X, prism.Sweep.Y).Normalized();
            var across = new Vec2(-dir.Y, dir.X);
            var origin = new Vec2(prism.Outer[0].X, prism.Outer[0].Y);
            double xMin = double.MaxValue, xMax = double.MinValue, zMin = double.MaxValue, zMax = double.MinValue;
            foreach (var p in prism.Outer)
            {
                var x = Vec2.Dot(new Vec2(p.X, p.Y) - origin, across);
                xMin = Math.Min(xMin, x); xMax = Math.Max(xMax, x); zMin = Math.Min(zMin, p.Z); zMax = Math.Max(zMax, p.Z);
            }
            width = Round(xMax - xMin); depth = Round(zMax - zMin); top = zMax;
            // A native beam is a horizontal rectangle swept along its axis: a profile that is no rectangle in its plane (a T,
            // a haunch, or a sloping beam's parallelogram or turned rectangle read across its sweep) would come in as its bounding box.
            Vec2 InPlane(Vec3 p) => new(Vec2.Dot(new Vec2(p.X, p.Y) - origin, across), p.Z);
            var area = Math.Abs(Polygon2.SignedArea([.. prism.Outer.Select(InPlane)])) - prism.Holes.Sum(h => Math.Abs(Polygon2.SignedArea([.. h.Select(InPlane)])));
            if (area < 0.99 * (xMax - xMin) * (zMax - zMin))
            {
                // Its own section, about its box's centre (a steel profile, a shaped section), when asked for; else a reference.
                // A beam runs along its longest side: a shape swept less far than it is wide or deep (a beam with a sloped end is a
                // trapezoid swept across it) is no section of it.
                if (outline != null && prism.Holes.Count == 0 && prism.Height < Math.Max(xMax - xMin, zMax - zMin))
                {
                    ctx?.Skip("a beam whose body runs across its length (a sloped or shaped end)");
                    return null;
                }
                if (outline != null && prism.Holes.Count == 0)
                {
                    var (uc, vc) = ((xMin + xMax) / 2, (zMin + zMax) / 2);
                    var ring = Polygon2.EnsureCcw([.. prism.Outer.Select(InPlane).Select(q => new Vec2(Math.Round(q.X - uc, 6), Math.Round(q.Y - vc, 6)))]);
                    outline[0] = new Section("outline", width, depth, Name: "Outline") { Rings = [ring] };
                }
                else { ctx?.Skip("a beam whose profile is not a rectangle (a shaped section, or a sloping beam)"); return null; }
            }
            var centre = origin + across * ((xMin + xMax) / 2);
            start = centre; end = centre + dir * prism.Height;
            var (a0, a1) = (centre + across * ((xMin - xMax) / 2), centre + across * ((xMax - xMin) / 2));
            footprint = [a0, a1, a1 + dir * prism.Height, a0 + dir * prism.Height];
        }
        else
        {
            var plan = Footprints.Plan(prism.Outer);
            if (Footprints.Axis(plan) is not { } axis || axis.Coverage < 0.5 || axis.Thickness > (axis.End - axis.Start).Length) { ctx?.Skip("a beam whose footprint has no axis"); return null; }
            (start, end) = (axis.Start, axis.End);
            width = Round(axis.Thickness); depth = Round(prism.Height); top = prism.TopZ;
            footprint = plan;
        }
        if (!(width > 0 && depth > 0) || (end - start).Length < 1e-3) { ctx?.Skip("a beam without a section"); return null; }
        return (start, end, width, depth, top, footprint);
    }

    /// <summary>
    /// A long rectangle: a strip foundation along its middle (decision 214). Its section stays the file's rectangle: a core
    /// as wide as it on a footing of the same width, half its height each, the bottom where the file has it.
    /// </summary>
    string? StripFooting(IEdit edit, ForeignElement e, ShapePrism prism, (Vec2 Centre, double Width, double Depth, double RotationDeg) r, Entity storey, RecognitionContext ctx)
    {
        var angle = r.RotationDeg * Math.PI / 180;
        var along = new Vec2(Math.Cos(angle), Math.Sin(angle));
        var (length, width) = r.Width >= r.Depth ? (r.Width, r.Depth) : (r.Depth, r.Width);
        if (r.Width < r.Depth) along = along.Perp;
        var height = Round(prism.Height);
        var bottom = Math.Round(prism.BaseZ - storey.Num("elevation"), 6);
        var material = e.Material ?? "Reinforced concrete";
        var type = Sized(edit, ctx, e, $"strip:{Round(width)}:{height}:{bottom}", $"SF {width * 100:0}×{height * 100:0}",
            name => StripFootings.CreateType(edit, name, [new LayerSpec(material, Round(width), Core: true)], height / 2, Round(width), height / 2, bottom, material));
        var (start, end) = (r.Centre - along * (length / 2), r.Centre + along * (length / 2));
        var footing = new Entity(e.GlobalId, IfcClass.Footing, e.Name ?? type.Name, new JsonObject { ["start"] = Place.Json2(start), ["end"] = Place.Json2(end) }, "STRIP_FOOTING");
        edit.Add(footing);
        Spatial.Contain(edit, storey.Id, footing.Id);
        StripFootings.SetType(edit, footing, type);
        ctx.Made.Add(footing.Id);
        return footing.Id;
    }

    string? Footing(IEdit edit, ForeignElement e, RecognitionContext ctx)
    {
        if (!One(e, ctx, p => p.Vertical, "a footing whose body is not one vertical prism", out var prism)) return null;
        var plan = Footprints.Plan(prism.Outer);
        if (Footprints.Rectangle(plan) is not { } r) { ctx.Skip("a footing that is not a rectangle"); return null; }
        var storey = edit.Model.Get(e.StoreyId);
        if (storey == null) { ctx.Skip("a footing without a storey"); return null; }
        if (Math.Max(r.Width, r.Depth) > 4 * Math.Min(r.Width, r.Depth)) return StripFooting(edit, e, prism, r, storey, ctx);
        var height = Round(prism.Height);
        var bottom = Math.Round(prism.BaseZ - storey.Num("elevation"), 6);
        var step = new PadStep(Round(r.Width), Round(r.Depth), height);
        var type = Sized(edit, ctx, e, $"pad:{step.Length}:{step.Width}:{step.Height}:{bottom}", $"PF {step.Length * 100:0}×{step.Width * 100:0}",
            name => PadFootings.CreateType(edit, name, [step], bottom, e.Material ?? "Reinforced concrete"));
        var footing = new Entity(e.GlobalId, IfcClass.Footing, e.Name ?? type.Name, new JsonObject { ["at"] = Place.Json2(r.Centre), ["rotation"] = Math.Round(r.RotationDeg, 3), [PadFootings.FollowKey] = false }, "PAD_FOOTING");
        edit.Add(footing);
        Spatial.Contain(edit, storey.Id, footing.Id);
        edit.Relate(IfcClass.RelDefinesByType, type.Id, footing.Id);
        ctx.Made.Add(footing.Id);
        return footing.Id;
    }

    /// <summary>The key of the element's file type among the types this import made, when it has one.</summary>
    static string? TypeKey(string cls, ForeignElement e) => e.TypeName is { Length: > 0 } tn ? $"{cls}:type:{tn}" : null;

    /// <summary>
    /// The native type for the element: its file type's (the first made for it) when it <paramref name="fits"/>, else one
    /// of the file type's for this element's section (<paramref name="label"/>, named after both); without a file type, one
    /// by <paramref name="key"/> (what makes it).
    /// </summary>
    static Entity Type(IEdit edit, RecognitionContext ctx, string cls, ForeignElement e, string key, string fallbackName, string label, Func<string, Entity> create, Func<Entity, bool> fits)
    {
        var name = fallbackName;
        if (TypeKey(cls, e) is { } typed)
        {
            name = e.TypeName!;
            key = typed;
            if (ctx.TypeIds.TryGetValue(key, out var first) && edit.Model.Get(first) is { } t && !fits(t))
            {
                // Another section under the same file type: the next of its own that fits (two outlines in one box are two).
                (key, name) = ($"{typed}:{label}", $"{e.TypeName} {label}");
                for (var n = 2; ctx.TypeIds.TryGetValue(key, out var other) && edit.Model.Get(other) is { } o && !fits(o); n++)
                    (key, name) = ($"{typed}:{label}:{n}", $"{e.TypeName} {label} ({n})");
            }
        }
        if (ctx.TypeIds.TryGetValue(key, out var id) && edit.Model.Get(id) is { } known) return known;
        var type = create(name);
        ctx.CreatedTypes.Add(type.Id);
        ctx.TypeIds[key] = type.Id;
        return type;
    }

    static Entity Type(IEdit edit, RecognitionContext ctx, string cls, ForeignElement e, string key, string fallbackName, Func<string, Entity> create) =>
        Type(edit, ctx, cls, e, key, fallbackName, "", create, _ => true);

    /// <summary>
    /// A footing's type, by its file type while that type was made for these sizes (<paramref name="sizes"/>): footings of one file
    /// type and other sizes take one of their own each (a type's sizes are its footings' own), named after the file's with theirs.
    /// </summary>
    static Entity Sized(IEdit edit, RecognitionContext ctx, ForeignElement e, string sizes, string fallbackName, Func<string, Entity> create)
    {
        var mark = "sizes:" + sizes;
        var type = Type(edit, ctx, IfcClass.FootingType, e, sizes, fallbackName, fallbackName, create, t => ctx.TypeIds.GetValueOrDefault(mark) == t.Id);
        ctx.TypeIds.TryAdd(mark, type.Id);
        return type;
    }

    static double Round(double v) => Math.Round(v, 4);

    /// <summary>The box a prism fills: its base and its base swept.</summary>
    static (Vec3 Min, Vec3 Max) Span(ShapePrism p)
    {
        var pts = p.Outer.Concat(p.Outer.Select(q => q + p.Sweep)).ToList();
        return (new Vec3(pts.Min(q => q.X), pts.Min(q => q.Y), pts.Min(q => q.Z)), new Vec3(pts.Max(q => q.X), pts.Max(q => q.Y), pts.Max(q => q.Z)));
    }

    static bool One(ForeignElement e, RecognitionContext ctx, Func<ShapePrism, bool> wanted, string reason, out ShapePrism prism)
    {
        prism = null!;
        if (e.Prisms.Count == 0) { ctx.Skip(e.ShapeNote is { } why ? $"{reason.Split(" whose")[0]}: {why}" : reason); return false; }
        // A B-rep's prisms (a box's three) all span its box: prisms spanning different ones are of several B-reps.
        var breps = e.Prisms.Where(p => p.Source == "brep").Select(Span).ToList();
        var oneBrep = breps.All(b => (b.Min - breps[0].Min).Length <= 1e-3 && (b.Max - breps[0].Max).Length <= 1e-3);
        var distinct = e.Prisms.Count(p => p.Source != "brep") + (breps.Count == 0 ? 0 : oneBrep ? 1 : 2);
        if (distinct > 1) { ctx.Skip($"{reason.Split(" whose")[0]} made of several bodies"); return false; }
        var candidates = e.Prisms.Where(wanted).ToList();
        if (candidates.Count == 0) { ctx.Skip(reason); return false; }
        // A box gives three prisms: for a column the vertical one; for a beam the longest sweep.
        prism = candidates.OrderByDescending(p => wanted(p) && p.Vertical && e.IfcClass != IfcClass.Beam ? 1 : 0).ThenByDescending(p => p.Height).First();
        return true;
    }
}
