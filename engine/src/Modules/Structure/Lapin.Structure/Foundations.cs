using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Structure;

/// <summary>A footprint along an axis between two lateral offsets, its ends cut by its joins.</summary>
static class Strips
{
    /// <summary>The band from <paramref name="y0"/> to <paramref name="y1"/> across the axis, cut at each end straight across the body (yMin … yMax).</summary>
    public static Vec2[] Footprint(JoinWall f, (Vec2 Point, EndCut Cut)?[] ends, double y0, double y1)
    {
        var start = ends[0]?.Point ?? f.Start;
        var end = ends[1]?.Point ?? f.End;
        var sc = ends[0]?.Cut ?? EndCut.Square;
        var ec = ends[1]?.Cut ?? EndCut.Square;
        var dir = (end - start).Normalized();
        var left = dir.Perp;
        var length = (end - start).Length;
        double At(EndCut c, double y) => f.YMax - f.YMin < 1e-12 ? c.Right : c.Right + (c.Left - c.Right) * (y - f.YMin) / (f.YMax - f.YMin);
        Vec2 P(double x, double y) => start + dir * x + left * y;
        return [P(-At(sc, y0), y0), P(length + At(ec, y0), y0), P(length + At(ec, y1), y1), P(-At(sc, y1), y1)];
    }

    /// <summary>The band from <paramref name="y0"/> to <paramref name="y1"/>, each end cut as its own join says, across the body that join was worked out for.</summary>
    public static Vec2[] Footprint(Vec2 axisStart, Vec2 axisEnd, End? start, End? end, double y0, double y1)
    {
        var a = start?.Point ?? axisStart;
        var b = end?.Point ?? axisEnd;
        var dir = (b - a).Normalized();
        var left = dir.Perp;
        var length = (b - a).Length;
        static double At(End? e, double y)
        {
            if (e is not { } x) return 0;
            return x.YMax - x.YMin < 1e-12 ? x.Cut.Right : x.Cut.Right + (x.Cut.Left - x.Cut.Right) * (y - x.YMin) / (x.YMax - x.YMin);
        }
        Vec2 P(double x, double y) => a + dir * x + left * y;
        return [P(-At(start, y0), y0), P(length + At(end, y0), y0), P(length + At(end, y1), y1), P(-At(start, y1), y1)];
    }

    /// <summary>One end as a join leaves it: where the axis stops, the cut there, the body the cut is across, the kind of join.</summary>
    public readonly record struct End(Vec2 Point, EndCut Cut, double YMin, double YMax, JoinKind Kind);

    /// <summary>
    /// Each end's join worked out with the bodies <paramref name="frameFor"/>
    /// gives (all the layers, or the cores only) and the style given (else the
    /// join's own).
    /// </summary>
    public static End?[] Ends(IModelView m, Entity e, IPathElements paths, List<string> problems, Func<Entity, JoinWall?> frameFor, JoinStyle? style = null)
    {
        var ends = new End?[2];
        foreach (var r in Joins.Of(m, e.Id))
        {
            if (m.Get(r.Relating) is not { } a || r.Related.Count == 0 || m.Get(r.Related[0]) is not { } b || !paths.IsPath(a.Class) || a.Class != b.Class) continue;
            if (frameFor(a) is not { } fa || frameFor(b) is not { } fb) continue;
            WallJoin join;
            try { join = WallJoins.Compute(fa, fb, new JoinOptions(Priority: 'a', Style: style ?? Joins.StyleOf(r))); }
            catch (CommandException ex) { problems.Add(ex.Message); continue; }
            var side = r.Relating == e.Id ? join.A : join.B;
            var i = side.Connection switch { Connection.AtStart => 0, Connection.AtEnd => 1, _ => -1 };
            if (i < 0 || ends[i] != null) continue;
            ends[i] = i == 0
                ? new End(side.Wall.Start, side.Wall.StartCut ?? EndCut.Square, side.Wall.YMin, side.Wall.YMax, join.Kind)
                : new End(side.Wall.End, side.Wall.EndCut ?? EndCut.Square, side.Wall.YMin, side.Wall.YMax, join.Kind);
        }
        return ends;
    }

    /// <summary>The same band along a curved axis: its ends where the joins put them (along the tangent there), cut across the body.</summary>
    public static List<Vec2> Footprint(PathCurve c, JoinWall f, (Vec2 Point, EndCut Cut)?[] ends, double y0, double y1)
    {
        var (from, to) = c.Ends(ends[0], ends[1], f.YMin, f.YMax);
        return c.Band(from, to, y0, y1);
    }

    /// <summary>Where a profile swept along a curved axis starts and ends: where its joints leave the axis.</summary>
    public static (double S0, double S1) Reach(PathCurve c, JoinWall f, (Vec2 Point, EndCut Cut)?[] ends)
    {
        var (from, to) = c.Ends(ends[0], ends[1], f.YMin, f.YMax);
        return (from(0), to(0));
    }
}

/// <summary>
/// Strip foundations (the old Lapin's): a foundation wall (the stem),
/// layered like a wall with its reinforced concrete core on the axis, on a
/// strip footing centred under it. One IfcFooting STRIP_FOOTING along its
/// axis, of an IfcFootingType that carries the stem's layer set and the
/// footing's size and level. Strip foundations join as walls do; the
/// footing joins as its stem does, with its own width.
/// </summary>
public static class StripFootings
{
    /// <summary>The old Lapin's default: protection board, XPS, waterproofing, then the 300 mm core; a 1.4 m stem on a 600 × 400 footing at −1.80.</summary>
    public static readonly IReadOnlyList<LayerSpec> DefaultLayers =
    [
        new("Protection board", 0.02, Color: "#8c8c8c"),
        new("XPS insulation", 0.1, Color: "#f2c46d"),
        new("Waterproofing", 0.005, Color: "#303030"),
        new("Reinforced concrete", 0.3, Core: true, Color: "#a8a8a8"),
    ];

    public static bool IsStrip(Entity type) => type.Str("kind") == "strip";

    public static Entity CreateType(IEdit edit, string? name, IReadOnlyList<LayerSpec> layers, double stemHeight = 1.4, double footingWidth = 0.6, double footingHeight = 0.4, double bottom = -1.8, string concrete = "Reinforced concrete")
    {
        if (layers.Count == 0) throw new CommandException("A strip foundation needs its layers, or a profile.");
        LayerSets.Validate(layers, "A strip foundation");
        if (!(stemHeight > 0 && footingWidth > 0 && footingHeight > 0)) throw new CommandException("The stem's height and the footing's width and height must be positive.");
        var core = layers.FirstOrDefault(l => l.Core) ?? layers.MaxBy(l => l.Thickness)!;
        var type = new Entity(edit.NewId(), IfcClass.FootingType, string.IsNullOrWhiteSpace(name) ? $"SF {core.Thickness * 100:0}" : name.Trim(), new JsonObject
        {
            ["kind"] = "strip", ["stemHeight"] = stemHeight, ["footingWidth"] = footingWidth, ["footingHeight"] = footingHeight, ["bottom"] = bottom,
            ["concrete"] = Materials.Ensure(edit, concrete).Id,
        }, "STRIP_FOOTING");
        edit.Add(type);
        LayerSets.Attach(edit, type, layers);
        return type;
    }

    public static Entity DefaultType(IEdit edit) => CreateType(edit, null, DefaultLayers);

    /// <summary>
    /// A strip footing without a wall on it (decision 436, a tester's report #116): the concrete footing alone, a profile type
    /// (the catalogue's rectangular footing, 600 × 400 at the bottom level of the standard one). The project's first such
    /// type, else one made.
    /// </summary>
    public static Entity FootingAloneType(ICommandContext ctx)
    {
        foreach (var t in ctx.Model.OfClass(IfcClass.FootingType))
            if (IsStrip(t) && ProfileOf(ctx.Model, t) != null) return t;
        var profile = Profiles.Ensure(ctx, "footingRect", null);
        return CreateProfileType(ctx, "Strip footing 600×400", Profiles.Read(profile)!, profile.Id);
    }

    /// <summary>
    /// A strip foundation type of a profile (decision 297): the profile (its id and its copy) swept along the axis, its
    /// insertion point on the axis at the bottom level; no stem layers.
    /// </summary>
    public static Entity CreateProfileType(IEdit edit, string? name, ProfileShape profile, string? profileId, double bottom = -1.8, string concrete = "Reinforced concrete")
    {
        var attrs = new JsonObject { ["kind"] = "strip", ["bottom"] = bottom, ["concrete"] = Materials.Ensure(edit, concrete).Id };
        Profiles.Assign(attrs, profile, profileId);
        var type = new Entity(edit.NewId(), IfcClass.FootingType, string.IsNullOrWhiteSpace(name) ? $"SF {profile.Name}" : name.Trim(), attrs, "STRIP_FOOTING");
        edit.Add(type);
        return type;
    }

    /// <summary>The profile a strip foundation type is swept with; null for the standard one (a layered stem on a footing).</summary>
    public static ProfileShape? ProfileOf(IModelView m, Entity type) =>
        IsStrip(type) && (type.Attrs[Profiles.Key] != null || type.Str(Profiles.IdKey) != null) ? Profiles.Of(m, type) : null;

    static ProfileShape? ProfileOfFooting(IModelView m, Entity footing) => Place.TypeOf(m, footing.Id) is { } t ? ProfileOf(m, t) : null;

    public static Entity Add(IEdit edit, IPathElements paths, Entity storey, Vec2 start, Vec2 end, Entity type, JoinStyle style, double rise = 0)
    {
        if ((end - start).Length < 1e-3) throw new CommandException("A strip foundation needs two different points.");
        var f = new Entity(edit.NewId(), IfcClass.Footing, type.Name, new JsonObject { ["start"] = Place.Json2(start), ["end"] = Place.Json2(end) }, "STRIP_FOOTING");
        if (rise != 0) f.Attrs[PathCurve.Key] = rise;
        edit.Add(f);
        Spatial.Contain(edit, storey.Id, f.Id);
        SetType(edit, f, type);
        Joins.Auto(edit, paths, f.Id, style);
        return edit.Model.Get(f.Id)!;
    }

    public static void SetType(IEdit edit, Entity footing, Entity type)
    {
        edit.Relate(IfcClass.RelDefinesByType, type.Id, footing.Id);
        LayerSets.LayOut(edit, footing, type, "AXIS2", Layers.CoreOffset);
    }

    /// <summary>The stem's layer bands across the axis (core centred on it), first to last.</summary>
    static IReadOnlyList<(double From, double To, string Material, string Name, int Index)> Bands(IModelView m, Entity footing)
    {
        return LayerSets.BandsOf(m, footing.Id)?.Select(b => (b.From, b.To, b.Material, b.Name, b.Index)).ToList() ?? [];
    }

    /// <summary>The stem as joins see it: its axis and its layers' extent.</summary>
    public static JoinWall? Frame(Entity footing, IModelView m)
    {
        if (ProfileFrame(footing, m) is { } pf) return pf;
        var bands = Bands(m, footing);
        var (s, e) = (Joins.Start(footing), Joins.End(footing));
        if (bands.Count == 0 || (e - s).Length < 1e-3) return null;
        return new JoinWall(s, e, bands.Min(b => b.From), bands.Max(b => b.To));
    }

    /// <summary>The stem's core alone, as the cores' joins see it.</summary>
    static JoinWall? CoreFrame(Entity footing, IModelView m)
    {
        if (ProfileFrame(footing, m) is { } pf) return pf;
        var bands = LayerSets.BandsOf(m, footing.Id);
        var (s, e) = (Joins.Start(footing), Joins.End(footing));
        if (bands is not { Count: > 0 } || (e - s).Length < 1e-3) return null;
        var core = bands.FirstOrDefault(b => b.Core);
        if (core.To - core.From <= 1e-9) core = bands.MaxBy(b => b.To - b.From);
        return new JoinWall(s, e, core.From, core.To);
    }

    /// <summary>The footing as its joins cut it: centred on the axis, its own width.</summary>
    static JoinWall? FootingFrame(Entity footing, IModelView m)
    {
        if (Place.TypeOf(m, footing.Id) is not { } type || !IsStrip(type)) return null;
        if (ProfileFrame(footing, m) is { } pf) return pf;
        var w = type.Num("footingWidth", 0.6);
        return new JoinWall(Joins.Start(footing), Joins.End(footing), -w / 2, w / 2);
    }

    /// <summary>A profile footing as joins see it: its axis, the profile's extent across it.</summary>
    static JoinWall? ProfileFrame(Entity footing, IModelView m)
    {
        if (ProfileOfFooting(m, footing) is not { } p) return null;
        var (s, e) = (Joins.Start(footing), Joins.End(footing));
        var (u0, u1, _, _) = p.Bounds;
        return (e - s).Length < 1e-3 || u1 - u0 < 1e-6 ? null : new JoinWall(s, e, u0, u1);
    }

    public static Solid? Envelope(Entity footing, IModelView m)
    {
        if (Place.TypeOf(m, footing.Id) is { } pt && ProfileOf(m, pt) is { } profile) return Plain(footing, m, profile, Place.Elevation(m, footing) + pt.Num("bottom", -1.8));
        if (Place.TypeOf(m, footing.Id) is not { } type || !IsStrip(type) || FootingFrame(footing, m) is not { } f) return null;
        var left = (f.End - f.Start).Normalized().Perp;
        var y0 = Math.Min(f.YMin, Frame(footing, m)?.YMin ?? 0);
        var y1 = Math.Max(f.YMax, Frame(footing, m)?.YMax ?? 0);
        var c = PathCurve.Of(footing);
        IReadOnlyList<Vec2> fp = c.IsArc ? c.Band(_ => 0, _ => c.Length, y0, y1) : [f.Start + left * y0, f.End + left * y0, f.End + left * y1, f.Start + left * y1];
        var bottom = Place.Elevation(m, footing) + type.Num("bottom", -1.8);
        return Prism.ExtrudeFootprint(fp, bottom, type.Num("footingHeight", 0.4) + type.Num("stemHeight", 1.4), "footing");
    }

    public static ElementGeometry? Geometry(Entity footing, IModelView m, IServices? services)
    {
        if (Place.TypeOf(m, footing.Id) is not { } type || !IsStrip(type)) return new ElementGeometry([], Diagnostics: ["A strip foundation needs its type."]);
        if (ProfileOf(m, type) is { } profile) return ProfileGeometry(footing, type, profile, m, services);
        var paths = services?.Get<IPathElements>();
        var problems = new List<string>();
        var bottom = Place.Elevation(m, footing) + type.Num("bottom", -1.8);
        var fh = type.Num("footingHeight", 0.4);
        var sh = type.Num("stemHeight", 1.4);
        var parts = new List<GeometryPart>();
        var plan = new List<PlanItem>();
        // Straight, or along its arc.
        var c = PathCurve.Of(footing);
        IReadOnlyList<Vec2> Band(JoinWall f, (Vec2 Point, EndCut Cut)?[] ends, double y0, double y1) => c.IsArc ? Strips.Footprint(c, f, ends, y0, y1) : Strips.Footprint(f, ends, y0, y1);
        if (Frame(footing, m) is { } stem)
        {
            var ends = paths != null ? Joins.EndsOf(m, footing, paths, problems) : new (Vec2 Point, EndCut Cut)?[2];
            // Round an L corner the layers outside the core go on mitred, whatever the style; the cores join as it says.
            Strips.End?[]? layered = null, cores = null;
            if (paths != null && !c.IsArc)
            {
                var all = Strips.Ends(m, footing, paths, [], e => Frame(e, m));
                if (all.Any(x => x is { Kind: JoinKind.L }))
                {
                    layered = Strips.Ends(m, footing, paths, [], e => Frame(e, m), JoinStyle.Mitre);
                    cores = Strips.Ends(m, footing, paths, [], e => CoreFrame(e, m));
                    for (var k = 0; k < 2; k++)
                        if (all[k] is not { Kind: JoinKind.L }) { layered[k] = all[k]; cores[k] = all[k]; }
                }
            }
            var coreIndex = LayerSets.BandsOf(m, footing.Id)?.FirstOrDefault(b => b.Core) is { } cb && cb.To - cb.From > 1e-9 ? cb.Index : -1;
            foreach (var (from, to, material, name, index) in Bands(m, footing))
            {
                var fp = layered != null && cores != null
                    ? Strips.Footprint(stem.Start, stem.End, (index == coreIndex ? cores : layered)[0], (index == coreIndex ? cores : layered)[1], from, to)
                    : Band(stem, ends, from, to);
                if (Prism.ExtrudeFootprint(fp, bottom + fh, sh, name) is { } solid) parts.Add(new GeometryPart(name, Materials.ColorOf(m, material), solid, material, $"layer:{index}"));
                plan.Add(new PlanItem("outline", true, fp));
            }
        }
        if (FootingFrame(footing, m) is { } ff)
        {
            // The footing joins as its stem does, with its own width.
            var ends = paths != null ? Joins.EndsOf(m, footing, paths, problems, e => FootingFrame(e, m)) : new (Vec2 Point, EndCut Cut)?[2];
            var fp = Band(ff, ends, ff.YMin, ff.YMax);
            var concrete = type.Str("concrete");
            if (Prism.ExtrudeFootprint(fp, bottom, fh, "footing") is { } solid) parts.Add(new GeometryPart("Footing", Materials.ColorOf(m, concrete), solid, concrete, "footing"));
            plan.Add(new PlanItem("outline", true, fp));
        }
        parts = CutPriorities.Apply(footing, m, services, parts, problems);
        parts = LayerExtentCuts.Apply(footing, m, services, parts, problems);
        IReadOnlyList<Vec2> snaps = c.IsArc ? [c.Start, c.End, c.PointAt(c.Length / 2)] : [Joins.Start(footing), Joins.End(footing)];
        return new ElementGeometry(parts, plan, snaps, problems.Count > 0 ? problems : null);
    }

    /// <summary>
    /// A profile footing's ends as its joins leave them: the cut there (across the profile's extent), the kind of join, the
    /// other footing, and whether this one ends against the other's run (a T). An L corner is mitred whatever its style, so
    /// the profile runs round it unbroken.
    /// </summary>
    readonly record struct ProfileEnd(Strips.End End, Entity Other, bool Ending);

    static ProfileEnd?[] ProfileEnds(IModelView m, Entity footing, IPathElements paths, List<string> problems)
    {
        var ends = new ProfileEnd?[2];
        foreach (var r in Joins.Of(m, footing.Id))
        {
            if (m.Get(r.Relating) is not { } a || r.Related.Count == 0 || m.Get(r.Related[0]) is not { } b || a.Class != b.Class || !paths.IsPath(a.Class)) continue;
            if (Frame(a, m) is not { } fa || Frame(b, m) is not { } fb) continue;
            WallJoin join;
            try
            {
                var style = Joins.StyleOf(r);
                join = WallJoins.Compute(fa, fb, new JoinOptions(Priority: 'a', Style: style));
                if (join.Kind == JoinKind.L && style != JoinStyle.Mitre) join = WallJoins.Compute(fa, fb, new JoinOptions(Priority: 'a', Style: JoinStyle.Mitre));
            }
            catch (CommandException ex) { problems.Add(ex.Message); continue; }
            var side = r.Relating == footing.Id ? join.A : join.B;
            var i = side.Connection switch { Connection.AtStart => 0, Connection.AtEnd => 1, _ => -1 };
            if (i < 0 || ends[i] != null) continue;
            var end = i == 0
                ? new Strips.End(side.Wall.Start, side.Wall.StartCut ?? EndCut.Square, side.Wall.YMin, side.Wall.YMax, join.Kind)
                : new Strips.End(side.Wall.End, side.Wall.EndCut ?? EndCut.Square, side.Wall.YMin, side.Wall.YMax, join.Kind);
            ends[i] = new ProfileEnd(end, r.Relating == footing.Id ? b : a, join.Kind == JoinKind.T && !side.RunsThrough);
        }
        return ends;
    }

    static double At(Strips.End e, double y) =>
        e.YMax - e.YMin < 1e-12 ? e.Cut.Right : e.Cut.Right + (e.Cut.Left - e.Cut.Right) * (y - e.YMin) / (e.YMax - e.YMin);

    /// <summary>The profile swept along a footing's whole axis, no join cutting it: its envelope, and what cuts a footing ending against it.</summary>
    static Solid? Plain(Entity footing, IModelView m, ProfileShape profile, double bottom)
    {
        var c = PathCurve.Of(footing);
        IReadOnlyList<Vec2> line = c.IsArc ? c.Line(0, c.Length, 0) : [Joins.Start(footing), Joins.End(footing)];
        return ProfileSweeps.Along(line, profile.Outline(), bottom, tag: "footing");
    }

    /// <summary>What another footing's body is where one ends against it: its profile, or its sole and stem, along its whole axis.</summary>
    static List<Solid> PlainBodies(Entity other, IModelView m)
    {
        if (Place.TypeOf(m, other.Id) is not { } type || !IsStrip(type)) return [];
        var bottom = Place.Elevation(m, other) + type.Num("bottom", -1.8);
        if (ProfileOf(m, type) is { } p) return Plain(other, m, p, bottom) is { } s ? [s] : [];
        var o = new List<Solid>();
        var c = PathCurve.Of(other);
        IReadOnlyList<Vec2> Band(double y0, double y1)
        {
            if (c.IsArc) return c.Band(_ => 0, _ => c.Length, y0, y1);
            var (a, b) = (Joins.Start(other), Joins.End(other));
            var left = (b - a).Normalized().Perp;
            return [a + left * y0, b + left * y0, b + left * y1, a + left * y1];
        }
        var (fw, fh, sh) = (type.Num("footingWidth", 0.6), type.Num("footingHeight", 0.4), type.Num("stemHeight", 1.4));
        if (Prism.ExtrudeFootprint(Band(-fw / 2, fw / 2), bottom, fh, "footing") is { } sole) o.Add(sole);
        if (Frame(other, m) is { } stem && sh > 0 && Prism.ExtrudeFootprint(Band(stem.YMin, stem.YMax), bottom + fh, sh, "stem") is { } st) o.Add(st);
        return o;
    }

    /// <summary>
    /// How far an end ending against another footing's run goes on past that one's face, to its axis: the face's distance
    /// from the axis across the way this one meets it. Cut there by the other's body, each part of the profile meets the
    /// other's face at its own height (a top step its top step, a bottom step its bottom step).
    /// </summary>
    static double Through(Entity me, Entity other, IModelView m)
    {
        if (Frame(other, m) is not { } f) return 0;
        var mine = (Joins.End(me) - Joins.Start(me)).Normalized();
        var oc = PathCurve.Of(other);
        var at = oc.IsArc ? oc.Project(Joins.Start(me)) : 0;
        var theirs = oc.IsArc ? oc.TangentAt(at) : (f.End - f.Start).Normalized();
        var origin = oc.IsArc ? oc.PointAt(at) : f.Start;
        var sin = Math.Abs(Vec2.Cross(mine, theirs));
        if (sin < 0.1) return 0;
        // The face on this footing's side: its far end's side of the other's axis.
        var far = (Joins.Start(me) - origin).Length > (Joins.End(me) - origin).Length ? Joins.Start(me) : Joins.End(me);
        var face = Vec2.Dot(far - origin, theirs.Perp) >= 0 ? f.YMax : f.YMin;
        return Math.Abs(face) / sin;
    }

    /// <summary>
    /// A profile footing's body (decision 297): its profile swept along its axis, straight or curved, its insertion point
    /// on the axis at the bottom level. Its ends are cut as its joins say (an L mitred, so two footings of one profile run
    /// round it as one); one ending against another's run (a T) goes on to that one's far side and is cut by its body, so a
    /// stepped or sloped profile meets the other's faces wherever they are.
    /// </summary>
    static ElementGeometry ProfileGeometry(Entity footing, Entity type, ProfileShape profile, IModelView m, IServices? services)
    {
        var paths = services?.Get<IPathElements>();
        var problems = new List<string>();
        var bottom = Place.Elevation(m, footing) + type.Num("bottom", -1.8);
        var outline = profile.Outline();
        var (u0, u1, _, _) = profile.Bounds;
        var c = PathCurve.Of(footing);
        var concrete = type.Str("concrete");
        Solid? solid;
        IReadOnlyList<Vec2> footprint;
        var cutters = new List<Solid>();
        if (c.IsArc)
        {
            var frame = Frame(footing, m)!;
            var ends = paths != null ? Joins.EndsOf(m, footing, paths, problems) : new (Vec2 Point, EndCut Cut)?[2];
            var (s0, s1) = Strips.Reach(c, frame, ends);
            solid = ProfileSweeps.Along(c.Line(s0, s1, 0), outline, bottom, tag: "footing");
            footprint = c.Band(_ => s0, _ => s1, u0, u1);
        }
        else
        {
            var ends = paths != null ? ProfileEnds(m, footing, paths, problems) : new ProfileEnd?[2];
            var a = ends[0]?.End.Point ?? Joins.Start(footing);
            var b = ends[1]?.End.Point ?? Joins.End(footing);
            var extra = new double[2];
            for (var k = 0; k < 2; k++)
                if (ends[k] is { Ending: true } te)
                {
                    extra[k] = Through(footing, te.Other, m);
                    cutters.AddRange(PlainBodies(te.Other, m));
                }
            Func<double, double>? Reach(int k) => ends[k] is { } pe ? y => At(pe.End, y) + extra[k] : null;
            solid = ProfileSweeps.Along([a, b], outline, bottom, startReach: Reach(0), endReach: Reach(1), tag: "footing");
            var dir = (b - a).Normalized();
            var left = dir.Perp;
            var len = (b - a).Length;
            double R(int k, double y) => Reach(k)?.Invoke(y) ?? 0;
            footprint = [a + dir * -R(0, u0) + left * u0, a + dir * (len + R(1, u0)) + left * u0, a + dir * (len + R(1, u1)) + left * u1, a + dir * -R(0, u1) + left * u1];
        }
        var parts = new List<GeometryPart>();
        if (solid != null && cutters.Count > 0)
        {
            if (services?.Get<ISolidBooleans>() is { } booleans)
                try { solid = booleans.Difference(solid, cutters, "footing") ?? solid; }
                catch (InvalidOperationException ex) { problems.Add($"The footing could not be cut where it ends against another ({ex.Message})."); }
            else problems.Add("Footings ending against another need the boolean library (manifold), missing here: the end is left square.");
        }
        if (solid != null) parts.Add(new GeometryPart("Footing", Materials.ColorOf(m, concrete), solid, concrete, "footing"));
        parts = CutPriorities.Apply(footing, m, services, parts, problems);
        parts = LayerExtentCuts.Apply(footing, m, services, parts, problems);
        IReadOnlyList<Vec2> snaps = c.IsArc ? [c.Start, c.End, c.PointAt(c.Length / 2)] : [Joins.Start(footing), Joins.End(footing)];
        return new ElementGeometry(parts, [new PlanItem("outline", true, footprint)], snaps, problems.Count > 0 ? problems : null);
    }

    /// <summary>
    /// A straight profile footing in IFC (decision 297): its profile (IfcArbitraryClosedProfileDef) extruded along its axis
    /// between its ends, each end cut by its join's plane when the cut is not square; null when an end runs into another
    /// footing (a T, cut by that one's body) or the footing is curved, which go as their faces.
    /// </summary>
    public static IfcSweptBody? IfcBody(IModelView m, Entity footing)
    {
        if (footing.Class != IfcClass.Footing || Place.TypeOf(m, footing.Id) is not { } type || ProfileOf(m, type) is not { } profile || PathCurve.Of(footing).IsArc) return null;
        var ends = ProfileEnds(m, footing, FootingPaths.Instance, []);
        if (ends.Any(e => e is { Ending: true })) return null;
        var outline = profile.Outline();
        var a = ends[0]?.End.Point ?? Joins.Start(footing);
        var b = ends[1]?.End.Point ?? Joins.End(footing);
        var dir = (b - a).Normalized();
        var left = dir.Perp;
        var len = (b - a).Length;
        var bottom = Place.Elevation(m, footing) + type.Num("bottom", -1.8);
        double R(int k, double y) => ends[k] is { } pe ? At(pe.End, y) : 0;
        var e0 = outline.Max(q => R(0, q.X));
        var e1 = outline.Max(q => R(1, q.X));
        var clips = new List<IfcClip>();
        for (var k = 0; k < 2; k++)
        {
            var slope = R(k, 1) - R(k, 0);
            if (Math.Abs(slope) < 1e-9) continue;
            // The cut line in plan: through the point the axis reaches, along left − dir × slope at the start (+ at the end); its normal out of the body.
            var at = k == 0 ? a - dir * R(0, 0) : b + dir * R(1, 0);
            var along = k == 0 ? left - dir * slope : left + dir * slope;
            var n = along.Normalized().Perp;
            if (Vec2.Dot(n, k == 0 ? -dir : dir) < 0) n = -n;
            clips.Add(new IfcClip(at.At(bottom), new Vec3(n.X, n.Y, 0)));
        }
        var origin = (a - dir * e0).At(bottom);
        var sweep = new IfcSweep(outline, [], origin, new Vec3(left.X, left.Y, 0), new Vec3(dir.X, dir.Y, 0), len + e0 + e1);
        return new IfcSweptBody(sweep, clips, []);
    }

    public static IEnumerable<QuantitySet> Quantities(Entity footing, ElementGeometry? g, IModelView m)
    {
        if (g == null || g.Parts.Count == 0 || Place.TypeOf(m, footing.Id) is not { } type) yield break;
        var length = PathCurve.Of(footing).Length;
        if (ProfileOf(m, type) is { } profile)
        {
            var (u0, u1, v0, v1) = profile.Bounds;
            yield return new QuantitySet("Qto_FootingBaseQuantities",
            [
                new("Length", length, "LENGTH"), new("Width", u1 - u0, "LENGTH"), new("Height", v1 - v0, "LENGTH"),
                new("CrossSectionArea", profile.Area, "AREA", "Cross section"),
                new("NetVolume", g.Parts.Sum(p => Measure.Volume(p.Solid)), "VOLUME", "Net volume"),
            ]);
            yield break;
        }
        var fh = type.Num("footingHeight", 0.4);
        yield return new QuantitySet("Qto_FootingBaseQuantities",
        [
            new("Length", length, "LENGTH"),
            new("NetVolume", g.Parts.Sum(p => Measure.Volume(p.Solid)), "VOLUME", "Net volume"),
        ]);
        foreach (var part in g.Parts)
        {
            var v = Measure.Volume(part.Solid);
            if (part.Key == "footing")
                yield return new QuantitySet("Qto_FootingBaseQuantities",
                [
                    new("Width", type.Num("footingWidth", 0.6), "LENGTH"),
                    new("Height", fh, "LENGTH"),
                    new("Area", fh > 0 ? v / fh : 0, "AREA", "Footprint"),
                    new("NetVolume", v, "VOLUME", "Net volume"),
                ], "footing", "Footing", part.Material);
            else
                yield return new QuantitySet("Lapin_StemLayerQuantities", [new("NetVolume", v, "VOLUME", "Net volume")], part.Key, part.Name, part.Material);
        }
    }
}

/// <summary>Strip foundations as joins see them, for what has no services at hand (the IFC export's bodies).</summary>
sealed class FootingPaths : IPathElements
{
    public static readonly FootingPaths Instance = new();
    public bool IsPath(string ifcClass) => ifcClass == IfcClass.Footing;
    public JoinWall? FrameOf(Entity entity, IModelView model) => StripFootings.Frame(entity, model);
}

/// <summary>Profile footings written to IFC as their profile extruded (decision 297).</summary>
sealed class FootingIfcBodies : IIfcSweptBodies
{
    public IfcSweptBody? BodyOf(IModelView model, Entity element) => StripFootings.IfcBody(model, element);
}

/// <summary>A step of a pad footing, bottom up: its length (along its direction), width and height.</summary>
public sealed record PadStep(double Length, double Width, double Height);

/// <summary>Stepped pad footings (the old Lapin's): steps from the bottom up, each centred on the footing's point, turned with it.</summary>
public static class PadFootings
{
    public static readonly IReadOnlyList<PadStep> DefaultSteps = [new(1.8, 1.8, 0.5), new(1, 1, 0.4)];

    public static bool IsPad(Entity type) => type.Str("kind") == "pad";

    public static IReadOnlyList<PadStep> Steps(Entity type) =>
        type.Attrs["steps"] is JsonArray arr
            ? arr.OfType<JsonObject>().Select(s => new PadStep(Json.Number(s["length"]) ?? 0, Json.Number(s["width"]) ?? 0, Json.Number(s["height"]) ?? 0)).ToList()
            : DefaultSteps;

    public static Entity CreateType(IEdit edit, string? name, IReadOnlyList<PadStep> steps, double bottom = -1.8, string concrete = "Reinforced concrete")
    {
        if (steps.Count == 0) throw new CommandException("A pad footing needs at least one step.");
        if (steps.Any(s => !(s.Length > 0 && s.Width > 0 && s.Height > 0))) throw new CommandException("Every step needs a length, a width and a height.");
        var type = new Entity(edit.NewId(), IfcClass.FootingType, string.IsNullOrWhiteSpace(name) ? $"PF {steps[0].Length * 100:0}×{steps[0].Width * 100:0}" : name.Trim(), new JsonObject
        {
            ["kind"] = "pad",
            ["steps"] = new JsonArray(steps.Select(s => (JsonNode)new JsonObject { ["length"] = s.Length, ["width"] = s.Width, ["height"] = s.Height }).ToArray()),
            ["bottom"] = bottom,
            ["concrete"] = Materials.Ensure(edit, concrete).Id,
        }, "PAD_FOOTING");
        edit.Add(type);
        return type;
    }

    public static Entity DefaultType(IEdit edit) => CreateType(edit, null, DefaultSteps);

    static IEnumerable<(PadStep Step, double Z)> Stack(Entity type, double bottom)
    {
        var z = bottom;
        foreach (var s in Steps(type)) { yield return (s, z); z += s.Height; }
    }

    // ------------------------------------------------------------ its level (decision 507)

    /// <summary>The attribute holding a pad's own top, an absolute elevation (m): set, the pad stands there whatever is over it.</summary>
    public const string TopKey = "top";
    /// <summary>The attribute saying whether a pad follows the column over it; missing: it does unless it came from an IFC file.</summary>
    public const string FollowKey = "followColumn";
    /// <summary>How far (m) from a pad's point a column's point may be for the pad to be under it: no column is wider.</summary>
    public const double Reach = 3.0;
    /// <summary>A point this near (m) a column's outline is in it (a pad put on the column's edge by a snap).</summary>
    public const double Edge = 1e-3;
    /// <summary>Two column bases this near (m) are as low: the one whose point is nearest the pad's is taken, then the first by id.</summary>
    const double SameBase = 1e-6;

    /// <summary>Where a pad's top and bottom are (absolute, m), by which rule ("column", "own", "storey"), and the column it follows.</summary>
    public sealed record PadLevel(double Top, double Bottom, string Rule, Entity? Column);

    /// <summary>The steps' heights together.</summary>
    public static double Height(Entity type) => Steps(type).Sum(s => s.Height);

    /// <summary>Whether a pad follows the column over it: as it says, else unless it was recognised from an IFC file (an import reproduces the file).</summary>
    public static bool Follows(Entity pad) =>
        pad.Attrs[FollowKey] is JsonValue v && v.TryGetValue<bool>(out var b) ? b : pad.Attrs["imported"] == null;

    /// <summary>A column's base: its storey's elevation and its base offset.</summary>
    public static double ColumnBase(IModelView m, Entity column) => Place.Elevation(m, column) + column.Num("baseOffset");

    /// <summary>A column's footprint: its type's section outline, turned and placed on its point.</summary>
    public static Vec2[] ColumnFootprint(IModelView m, Entity column) =>
        Place.Placed((Section.From(Place.TypeOf(m, column.Id)?.Attrs["section"]) ?? Section.Rectangle(0.3, 0.3)).Outline(), Place.Point(column), column.Num("rotation"));

    /// <summary>
    /// The column a point is under: of the columns (any storey) whose footprint holds the point (within <see cref="Edge"/>),
    /// the one whose base is lowest; as low (within 1 µm), the one whose point is nearest, then the first by id. Null when none.
    /// </summary>
    public static Entity? ColumnAt(IModelView m, Vec2 p)
    {
        Entity? best = null;
        var (bestBase, bestD) = (double.PositiveInfinity, double.PositiveInfinity);
        foreach (var c in m.OfClass(IfcClass.Column))
        {
            var d = (Place.Point(c) - p).Length;
            if (d > Reach || !Holds(ColumnFootprint(m, c), p)) continue;
            var b = ColumnBase(m, c);
            var lower = b < bestBase - SameBase;
            var tie = !lower && Math.Abs(b - bestBase) <= SameBase && (d < bestD - 1e-9 || Math.Abs(d - bestD) <= 1e-9 && string.CompareOrdinal(c.Id, best!.Id) < 0);
            if (lower || tie) (best, bestBase, bestD) = (c, b, d);
        }
        return best;
    }

    /// <summary>Whether the outline holds the point, or the point lies within <see cref="Edge"/> of it.</summary>
    static bool Holds(IReadOnlyList<Vec2> outline, Vec2 p)
    {
        if (Polygon2.Inside([outline.ToArray()], p)) return true;
        for (var i = 0; i < outline.Count; i++)
        {
            var (a, b) = (outline[i], outline[(i + 1) % outline.Count]);
            var ab = b - a;
            var l2 = Vec2.Dot(ab, ab);
            var t = l2 < 1e-18 ? 0 : Math.Clamp(Vec2.Dot(p - a, ab) / l2, 0, 1);
            if ((a + ab * t - p).Length <= Edge) return true;
        }
        return false;
    }

    /// <summary>
    /// A pad's level, worked out on every ask (never stored): its own top when it has one; else, following a column, its top
    /// on the base of the column it is under (<see cref="ColumnAt"/>); else its storey's elevation (and offset) with its type's
    /// bottom under it. Its bottom is its top less its steps' heights.
    /// </summary>
    public static PadLevel? Level(Entity pad, IModelView m)
    {
        if (Place.TypeOf(m, pad.Id) is not { } type || !IsPad(type)) return null;
        var h = Height(type);
        if (pad.NumOrNull(TopKey) is { } top) return new(top, top - h, "own", null);
        if (Follows(pad) && ColumnAt(m, Place.Point(pad)) is { } column)
        {
            var b = ColumnBase(m, column);
            return new(b, b - h, "column", column);
        }
        var bottom = Place.Elevation(m, pad) + type.Num("bottom", -1.8);
        return new(bottom + h, bottom, "storey", null);
    }

    /// <summary>
    /// The storey a pad whose bottom is at <paramref name="bottom"/> goes in: the highest whose elevation is at or below it
    /// (within 1 mm), else the lowest. Null without storeys.
    /// </summary>
    public static Entity? InfrastructureStorey(IModelView m, double bottom)
    {
        var all = Spatial.Storeys(m).ToList();
        return all.LastOrDefault(s => s.Num("elevation") <= bottom + 1e-3) ?? all.FirstOrDefault();
    }

    public static Solid? Envelope(Entity pad, IModelView m)
    {
        if (Place.TypeOf(m, pad.Id) is not { } type || !IsPad(type)) return null;
        var steps = Steps(type);
        var bottom = Level(pad, m)!.Bottom;
        // The steps' box: as long as the longest and as wide as the widest (a long low step under a square upper one).
        var (length, width) = (steps.Max(s => s.Length), steps.Max(s => s.Width));
        return Prism.ExtrudeFootprint(Place.Placed(Section.Rectangle(length, width).Outline(), Place.Point(pad), pad.Num("rotation")), bottom, steps.Sum(s => s.Height), "pad");
    }

    public static ElementGeometry? Geometry(Entity pad, IModelView m, IServices? services)
    {
        if (Place.TypeOf(m, pad.Id) is not { } type || !IsPad(type)) return new ElementGeometry([], Diagnostics: ["A pad footing needs its type."]);
        var concrete = type.Str("concrete");
        var at = Place.Point(pad);
        var parts = new List<GeometryPart>();
        var plan = new List<PlanItem>();
        var i = 0;
        foreach (var (step, z) in Stack(type, Level(pad, m)!.Bottom))
        {
            var fp = Place.Placed(Section.Rectangle(step.Length, step.Width).Outline(), at, pad.Num("rotation"));
            if (Prism.ExtrudeFootprint(fp, z, step.Height, "step") is { } solid) parts.Add(new GeometryPart($"Step {i + 1}", Materials.ColorOf(m, concrete), solid, concrete, $"step:{i}"));
            plan.Add(new PlanItem("outline", true, fp));
            i++;
        }
        var problems = new List<string>();
        parts = CutPriorities.Apply(pad, m, services, parts, problems);
        parts = LayerExtentCuts.Apply(pad, m, services, parts, problems);
        return new ElementGeometry(parts, plan, [at], problems.Count > 0 ? problems : null);
    }

    public static IEnumerable<QuantitySet> Quantities(Entity pad, ElementGeometry? g, IModelView m)
    {
        if (g == null || Place.TypeOf(m, pad.Id) is not { } type || !IsPad(type)) yield break;
        var steps = Steps(type);
        // Sides of every step, the part of each step's top the next leaves open, and the top.
        var surface = 0.0;
        for (var i = 0; i < steps.Count; i++)
        {
            var s = steps[i];
            surface += 2 * (s.Length + s.Width) * s.Height;
            surface += s.Length * s.Width - (i + 1 < steps.Count ? steps[i + 1].Length * steps[i + 1].Width : 0);
        }
        yield return new QuantitySet("Qto_FootingBaseQuantities",
        [
            new("Height", steps.Sum(s => s.Height), "LENGTH"),
            new("OuterSurfaceArea", surface, "AREA", "Outer surface"),
            new("NetVolume", g.Parts.Sum(p => Measure.Volume(p.Solid)), "VOLUME", "Net volume"),
        ]);
    }
}
