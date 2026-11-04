using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>
/// Wall sweeps and reveals (decision 297, a user's request; Revit's Wall Sweep and Wall Reveal): a <c>LapinWallSweep</c>
/// hosted by its wall (IfcRelVoidsElement, so it goes, is copied and moves storey with the wall) sweeps a profile
/// (<see cref="Profiles"/>) along one of the wall's faces (<c>side</c>: the wall's left or right, seen along it) at a
/// <c>height</c> over the wall's base (or under its top, <c>fromTop</c>), <c>offset</c> out from the face, from <c>start</c> to
/// <c>end</c> along the axis (the whole face by default). <c>mode</c> "add" (a cornice, a band, a plinth, a sill: a body of its
/// own material standing out of the face) or "cut" (a reveal, a groove: taken out of the wall's layers by a geometry filter;
/// the profile's u goes into the wall). Straight and curved walls; where two walls meet in a corner and each has a sweep of
/// the same profile, mode, offset and level on the faces that meet, the two are mitred into one another; it stops at the
/// openings it would cross (<c>gaps</c>, on by default). IFC: an added sweep is an IfcCovering MOLDING; a reveal an
/// IfcOpeningElement RECESS voiding the wall; both carry their intent and come back as they were.
/// <para>
/// Decision 298: a wall type keeps sweeps of its own (<see cref="WallSweepTypes"/>), given to each of its walls as
/// sweeps tagged <c>typeSweep</c>, a wall overriding their fields (<c>own</c>) or switching one off (<c>sweepsOff</c>);
/// <c>flip</c> turns the profile upside down, <c>setbackStart</c>/<c>setbackEnd</c> stop it short of the face's ends;
/// <c>returns</c> (none, start, end, both; both by default for a sweep, none for a reveal) closes an added sweep at a
/// free end of its wall by wrapping the profile round the end's corner, mitred, back to the face's plane, and
/// <c>returnsAtOpenings</c> the same at the openings' jambs; moved and copied on its own (<see cref="WallSweepPlacement"/>).
/// </para>
/// </summary>
public static class WallSweeps
{
    public const string Class = "LapinWallSweep";
    public static readonly string[] Modes = ["add", "cut"];
    public static readonly string[] Sides = ["left", "right"];
    public static readonly string[] ReturnModes = ["none", "start", "end", "both"];

    /// <summary>The fields a wall type's sweep gives its walls' sweeps; one a wall changes is its own from then on (<c>own</c>, the profile's id and copy as "profile").</summary>
    public static readonly string[] Inherited = ["mode", "side", "height", "fromTop", "offset", "gaps", "flip", "setbackStart", "setbackEnd", "returns", "returnsAtOpenings", "material", Profiles.IdKey, Profiles.Key, "start", "end"];
    public static string OwnKey(string field) => field == Profiles.IdKey ? Profiles.Key : field;

    public static void Register(IModuleRegistry r)
    {
        r.Class(new(Class, "Wall sweep",
        [
            new("mode", "Kind (add: a sweep, cut: a reveal)", "choice", Choices: Modes),
            new("side", "Face", "choice", Choices: Sides),
            new("height", "Height", "length", Min: -50, Max: 50),
            new("fromTop", "Height from the wall's top", "bool"),
            new("offset", "Offset from the face", "length", Min: -1, Max: 1),
            new("start", "Starts at (along the wall)", "length", Min: 0),
            new("end", "Ends at (along the wall)", "length", Min: 0),
            new("gaps", "Stops at openings", "bool"),
            new("flip", "Profile upside down", "bool"),
            new("setbackStart", "Set back from the start", "length", Min: 0, Max: 50),
            new("setbackEnd", "Set back from the end", "length", Min: 0, Max: 50),
            new("returns", "Returns at free ends", "choice", Choices: ReturnModes),
            new("returnsAtOpenings", "Returns at openings", "bool"),
        ], "#c2b8a3"));
        r.Geometry(Class, Geometry);
        r.Quantities(Class, Quantities);
        r.Service<IGeometryFilter>(new Cuts());
        r.Service<IElementUpdate>(new SweepUpdate());
        r.Service<IIfcRecesses>(new Recesses());
        r.Service<IHostedPlacement>(new WallSweepPlacement());
        r.Service<IScheduleSource>(new WallSweepSchedule());
        WallSweepTypes.Register(r);

        // {wallId | wallIds, mode?, side? | point?: [x, y, z?], height?, fromTop?, offset?, start?, end?, gaps?, flip?, setbackStart?, setbackEnd?,
        //  returns?: none | start | end | both, returnsAtOpenings?, materialId? | material?,
        //  and a profile: profileId, or catalog with params, or profile {points, insertion?}}: one per wall, its face from the point when no side is given.
        r.Command("addWallSweep", "Wall sweep", (ctx, a) =>
        {
            var walls = a["wallIds"] is JsonArray ws ? ws.Select(x => (string?)x).OfType<string>().ToList() : [(string?)a["wallId"] ?? throw new CommandException("Which wall?")];
            if (walls.Count == 0) throw new CommandException("Select walls first, or pick one.");
            var mode = (string?)a["mode"] == "cut" ? "cut" : "add";
            (ProfileShape Shape, string? Id) profile;
            if (Profiles.FromArgs(ctx, a) is { } given) profile = given;
            else
            {
                // None given: the catalogue's rectangle for a sweep, its rectangular groove for a reveal.
                var made = Profiles.Ensure(ctx, mode == "cut" ? "groove" : "rectangle", null);
                profile = (Profiles.Read(made)!, made.Id);
            }
            var ids = new JsonArray();
            foreach (var id in walls)
            {
                var wall = Spatial.Require(ctx.Model, id, IfcClass.Wall);
                ids.Add((JsonNode)Add(ctx, wall, mode, profile.Shape, profile.Id, a).Id);
            }
            return new JsonObject { ["id"] = ids[0]!.GetValue<string>(), ["ids"] = ids };
        });

        // {id | ids, profileId | catalog | profile?, materialId? | material?, side?: "flip"}: what the inspector's fields do not change.
        // A wall type's sweep changed so becomes its wall's own in what changed; {reset: true} gives it back the type's fields,
        // {off: true} switches it off on its wall (as deleting it does).
        r.Command("updateWallSweep", "Change wall sweep", (ctx, a) =>
        {
            var profile = Profiles.FromArgs(ctx, a);
            var reset = a["reset"] is JsonValue rv && rv.TryGetValue<bool>(out var rb) && rb;
            var off = a["off"] is JsonValue ov && ov.TryGetValue<bool>(out var ob) && ob;
            foreach (var id in Args.Ids(a))
            {
                var s = Spatial.Require(ctx.Model, id, Class);
                if (off) { if (!WallSweepTypes.SwitchOff(ctx, s)) throw new CommandException($"„{s.Name}” is the wall's own: delete it instead."); continue; }
                if (reset)
                {
                    if (s.Str("typeSweep") == null) throw new CommandException($"„{s.Name}” is the wall's own, not its type's.");
                    var back = s.CloneAttrs();
                    back.Remove("own");
                    ctx.Replace(s.With(attrs: back));
                    if (HostOf(ctx.Model, s) is { } host) WallSweepTypes.Rebuild(ctx, host);
                    continue;
                }
                var attrs = s.CloneAttrs();
                if (profile is { } p) Profiles.Assign(attrs, p.Shape, p.Id);
                if (MaterialFrom(ctx, a) is { } mat) attrs["material"] = mat.Id;
                if ((string?)a["side"] == "flip") attrs["side"] = Side(s) > 0 ? "right" : "left";
                MarkOwn(attrs, s);
                ctx.Replace(s.With(attrs: attrs));
            }
            return null;
        });
    }

    /// <summary>A wall type's sweep whose fields <paramref name="attrs"/> changes from <paramref name="before"/>: those become its wall's own (<c>own</c>).</summary>
    public static void MarkOwn(JsonObject attrs, Entity before)
    {
        if (before.Str("typeSweep") == null) return;
        var own = OwnOf(attrs);
        foreach (var f in Inherited)
            if (!JsonNode.DeepEquals(before.Attrs[f], attrs[f])) own.Add(OwnKey(f));
        if (own.Count == 0) attrs.Remove("own");
        else attrs["own"] = new JsonArray([.. own.Order(StringComparer.Ordinal).Select(x => (JsonNode)x)]);
    }

    /// <summary>The fields a wall type's sweep keeps as its wall's own.</summary>
    public static HashSet<string> OwnOf(JsonObject attrs) => attrs["own"] is JsonArray a ? [.. a.Select(x => (string?)x).OfType<string>()] : [];

    /// <summary>
    /// What the commands give a sweep beyond its profile, mode, face and height (and a wall type's sweep the same): offset,
    /// start, end, gaps, flip, set-backs, returns, returns at openings.
    /// </summary>
    public static void ReadOptions(JsonObject attrs, JsonObject a)
    {
        static bool? B(JsonNode? n) => n is JsonValue v && v.TryGetValue<bool>(out var b) ? b : null;
        if (Json.Number(a["offset"]) is { } off) { if (off != 0) attrs["offset"] = Math.Clamp(off, -1, 1); else attrs.Remove("offset"); }
        if (Json.Number(a["start"]) is { } s0) attrs["start"] = Math.Max(0, s0);
        if (Json.Number(a["end"]) is { } s1) attrs["end"] = Math.Max(0, s1);
        if (B(a["gaps"]) is { } gaps) { if (!gaps) attrs["gaps"] = false; else attrs.Remove("gaps"); }
        if (B(a["flip"]) is { } flip) { if (flip) attrs["flip"] = true; else attrs.Remove("flip"); }
        foreach (var k in new[] { "setbackStart", "setbackEnd" })
            if (Json.Number(a[k]) is { } sb) { if (sb > 0) attrs[k] = Math.Min(sb, 50); else attrs.Remove(k); }
        if ((string?)a["returns"] is { } ret)
        {
            if (!ReturnModes.Contains(ret)) throw new CommandException("Returns are none, start, end or both.");
            attrs["returns"] = ret;
        }
        if (B(a["returnsAtOpenings"]) is { } ro) { if (ro) attrs["returnsAtOpenings"] = true; else attrs.Remove("returnsAtOpenings"); }
    }

    internal static Entity? MaterialFrom(IEdit ctx, JsonObject a)
    {
        if ((string?)a["materialId"] is { Length: > 0 } mid) return Spatial.Require(ctx.Model, mid, IfcClass.Material);
        if ((string?)a["material"] is { Length: > 0 } name) return Materials.Find(ctx.Model, name) ?? Materials.Ensure(ctx, name);
        return null;
    }

    static Entity Add(IEdit ctx, Entity wall, string mode, ProfileShape shape, string? profileId, JsonObject a)
    {
        var base0 = Base(ctx.Model, wall);
        var attrs = new JsonObject { ["mode"] = mode };
        Profiles.Assign(attrs, shape, profileId);
        var point = a["point"] is JsonArray pa && pa.Count >= 2 ? pa : null;
        attrs["side"] = (string?)a["side"] is "left" or "right" ? (string)a["side"]! : point != null ? SideOf(wall, Args.PointOf(point)) : "right";
        var fromTop = a["fromTop"] is JsonValue ft && ft.TryGetValue<bool>(out var t) && t;
        double height;
        if (Json.Number(a["height"]) is { } h) height = h;
        else if (point is { Count: >= 3 } && Json.Number(point[2]) is { } z) height = fromTop ? base0 + wall.Num("height", Walls.DefaultHeight) - z : z - base0;
        else height = mode == "cut" ? 1.0 : 0.3;
        attrs["height"] = Math.Round(height, 4);
        if (fromTop) attrs["fromTop"] = true;
        ReadOptions(attrs, a);
        if (mode == "add") attrs["material"] = (MaterialFrom(ctx, a) ?? DefaultMaterial(ctx)).Id;
        return Make(ctx, wall, attrs);
    }

    /// <summary>A sweep of these fields made on the wall, on its storey, numbered on.</summary>
    internal static Entity Make(IEdit ctx, Entity wall, JsonObject attrs)
    {
        var mode = attrs["mode"]?.ToString() == "cut" ? "cut" : "add";
        SetExport(attrs, mode);
        var name = mode == "cut" ? "Reveal" : "Wall sweep";
        var e = new Entity(ctx.NewId(), Class, $"{name} {NextNumber(ctx.Model, name)}", attrs, mode == "cut" ? "RECESS" : "MOLDING");
        ctx.Add(e);
        ctx.AddRelation(new Relation(ctx.NewId(), IfcClass.RelVoidsElement, wall.Id, [e.Id]));
        if (Spatial.StoreyOf(ctx.Model, wall.Id) is { } storey) Spatial.Contain(ctx, storey.Id, e.Id);
        return e;
    }

    internal static int NextNumber(IModelView m, string prefix) =>
        1 + m.OfClass(Class).Where(e => e.Name.StartsWith(prefix + " ", StringComparison.Ordinal))
            .Select(e => int.TryParse(e.Name.AsSpan(prefix.Length + 1), out var n) ? n : 0).DefaultIfEmpty(0).Max();

    internal static void SetExport(JsonObject attrs, string mode)
    {
        if (mode == "cut") attrs.Remove("exportAs");
        else attrs["exportAs"] = "IfcCovering";
    }

    /// <summary>A sweep's material when none is given: the project's plaster, made a finish when the project had none.</summary>
    internal static Entity DefaultMaterial(IEdit ctx)
    {
        if (Materials.Find(ctx.Model, "Plaster") is { } found) return found;
        var made = Materials.Ensure(ctx, "Plaster", "#e8e2d6");
        var attrs = made.CloneAttrs();
        attrs["category"] = "Finish";
        var finish = made.With(attrs: attrs);
        ctx.Replace(finish);
        return finish;
    }

    /// <summary>
    /// A sweep changed in the inspector: a mode changed takes the IFC class and predefined type with it (a covering MOLDING,
    /// an opening RECESS); a wall type's sweep keeps what changed as its wall's own.
    /// </summary>
    sealed class SweepUpdate : IElementUpdate
    {
        public void Updated(ICommandContext ctx, Entity before, Entity after)
        {
            if (after.Class != Class || ctx.Model.Get(after.Id) is not { } now) return;
            var attrs = now.CloneAttrs();
            MarkOwn(attrs, before);
            var predefined = now.PredefinedType;
            if (before.Str("mode") != now.Str("mode"))
            {
                var mode = now.Str("mode") == "cut" ? "cut" : "add";
                SetExport(attrs, mode);
                if (mode == "add" && attrs["material"] == null) attrs["material"] = DefaultMaterial(ctx).Id;
                predefined = mode == "cut" ? "RECESS" : "MOLDING";
            }
            if (!JsonNode.DeepEquals(attrs, now.Attrs) || predefined != now.PredefinedType) ctx.Replace(new Entity(now.Id, now.Class, now.Name, attrs, predefined));
        }
    }

    // ------------------------------------------------------------ the path

    public static Entity? HostOf(IModelView m, Entity sweep) => m.RelatingOf(sweep.Id, IfcClass.RelVoidsElement) is { Class: IfcClass.Wall } w ? w : null;

    internal static int Side(Entity s) => s.Str("side") == "right" ? -1 : 1;

    internal static double Base(IModelView m, Entity wall) => (Spatial.StoreyOf(m, wall.Id)?.Num("elevation") ?? 0) + wall.Num("baseOffset");

    /// <summary>The face a point is on the side of: the wall's left or right, seen along it.</summary>
    internal static string SideOf(Entity wall, Vec2 p)
    {
        var c = PathCurve.Of(wall);
        double y;
        if (c.IsArc)
        {
            var s = Math.Clamp(c.Project(p), 0, c.Length);
            y = Vec2.Dot(p - c.PointAt(s), c.TangentAt(s).Perp);
        }
        else y = Vec2.Dot(p - Walls.Start(wall), (Walls.End(wall) - Walls.Start(wall)).Normalized().Perp);
        return y < 0 ? "right" : "left";
    }

    /// <summary>The level the profile's insertion point stands at (world z).</summary>
    public static double Level(IModelView m, Entity sweep, Entity wall)
    {
        var h = sweep.Num("height");
        return Base(m, wall) + (sweep.Bool("fromTop") ? wall.Num("height", Walls.DefaultHeight) - h : h);
    }

    /// <summary>The profile across the face: x out of the face (into the wall for a reveal), moved out by the offset; y up from the level (down, flipped).</summary>
    static List<Vec2> Across(Entity sweep, ProfileShape shape)
    {
        var cut = sweep.Str("mode") == "cut";
        var off = sweep.Num("offset");
        var flip = sweep.Bool("flip");
        return shape.Outline().Select(q => new Vec2((cut ? -q.X : q.X) + off, flip ? -q.Y : q.Y)).ToList();
    }

    /// <summary>
    /// One continuous piece of a sweep: its line on the face (stations along the axis), how each end meets what is there,
    /// and whether it returns there (wrapped round the wall's free end, or an opening's jamb).
    /// </summary>
    public sealed record Run(IReadOnlyList<Vec2> Line, double S0, double S1, Vec2? StartNormal, Vec2? EndNormal, bool ReturnAtStart = false, bool ReturnAtEnd = false);

    /// <summary>A sweep's face: where it lies across the axis, and its outward normal's sign (+1: the wall's left).</summary>
    internal static double FaceY(IModelView m, Entity wall, int side)
    {
        var bands = Walls.Section(m, wall);
        return side > 0 ? bands.Max(b => b.To) : bands.Min(b => b.From);
    }

    /// <summary>
    /// Where a sweep runs along its wall's axis (stations): from <c>S0</c> to <c>S1</c>, the face's whole length as the joins
    /// leave it (or from start to end, those given), before the set-backs and the openings; how its ends meet a like sweep
    /// round a corner, and whether each end is the wall's free end (no join there, the face's own end reached).
    /// </summary>
    public sealed record Reach(double S0, double S1, Vec2? N0, Vec2? N1, bool Free0, bool Free1);

    public static Reach? ReachOf(Entity sweep, Entity wall, IModelView m, IServices? services, List<string> problems)
    {
        if (Profiles.Of(m, sweep) is null) return null;
        var side = Side(sweep);
        var yFace = FaceY(m, wall, side);
        var bands = Walls.Section(m, wall);
        var (yMin, yMax) = (bands.Min(b => b.From), bands.Max(b => b.To));
        var paths = services?.Get<IPathElements>();
        var ends = paths != null ? Joins.EndsOf(m, wall, paths, problems) : new (Vec2 Point, EndCut Cut)?[2];
        var c = PathCurve.Of(wall);
        var start = Walls.Start(wall);
        var dir = (Walls.End(wall) - start).Normalized();
        var left = dir.Perp;
        double At(EndCut cut, double y) => yMax - yMin < 1e-12 ? cut.Right : cut.Right + (cut.Left - cut.Right) * (y - yMin) / (yMax - yMin);
        double s0, s1;
        if (c.IsArc)
        {
            var (from, to) = c.Ends(ends[0], ends[1], yMin, yMax);
            (s0, s1) = (from(yFace), to(yFace));
        }
        else
        {
            s0 = (ends[0] is { } e0 ? Vec2.Dot(e0.Point - start, dir) - At(e0.Cut, yFace) : 0);
            s1 = (ends[1] is { } e1 ? Vec2.Dot(e1.Point - start, dir) + At(e1.Cut, yFace) : c.Length);
        }
        Vec2? n0 = null, n1 = null;
        var level = Level(m, sweep, wall);
        if (!c.IsArc && paths != null)
        {
            if (sweep.NumOrNull("start") == null && Partner(m, sweep, wall, 0, paths, start, dir, left, yFace, side, level) is var (c0, o0)) { s0 = Vec2.Dot(c0 - start, dir); n0 = o0 * side; }
            if (sweep.NumOrNull("end") == null && Partner(m, sweep, wall, 1, paths, start, dir, left, yFace, side, level) is var (c1, o1)) { s1 = Vec2.Dot(c1 - start, dir); n1 = o1 * side; }
        }
        var (face0, face1) = (s0, s1);
        if (sweep.NumOrNull("start") is { } us) { s0 = Math.Max(s0, us); n0 = null; }
        if (sweep.NumOrNull("end") is { } ue) { s1 = Math.Min(s1, ue); n1 = null; }
        return new Reach(s0, s1, n0, n1, ends[0] == null && n0 == null && Math.Abs(s0 - face0) < 1e-9, ends[1] == null && n1 == null && Math.Abs(s1 - face1) < 1e-9);
    }

    /// <summary>Whether an added sweep returns at its wall's free start and end (by default at both; a reveal never).</summary>
    static (bool Start, bool End) Returns(Entity sweep)
    {
        if (sweep.Str("mode") == "cut") return (false, false);
        var r = sweep.Str("returns") ?? "both";
        return (r is "start" or "both", r is "end" or "both");
    }

    /// <summary>
    /// The sweep's pieces along its face: the face's whole length as the wall's joins leave it (or from start to end), less the
    /// set-backs and the openings it would cross; at a corner where the joined wall carries a sweep like it on the face meeting
    /// this one, the piece runs to the corner of the faces and turns there to meet the other; at a free end it may return.
    /// </summary>
    public static List<Run> Runs(Entity sweep, Entity wall, IModelView m, IServices? services, List<string> problems)
    {
        var o = new List<Run>();
        if (Profiles.Of(m, sweep) is not { } shape || ReachOf(sweep, wall, m, services, problems) is not { } reach) { problems.Add("The sweep has no profile."); return o; }
        var side = Side(sweep);
        var yFace = FaceY(m, wall, side);
        var c = PathCurve.Of(wall);
        var start = Walls.Start(wall);
        var dir = (Walls.End(wall) - start).Normalized();
        var left = dir.Perp;
        var (s0, s1, n0, n1) = (reach.S0, reach.S1, reach.N0, reach.N1);
        var (r0, r1) = Returns(sweep);
        var (ret0, ret1) = (r0 && reach.Free0, r1 && reach.Free1);
        if (sweep.Num("setbackStart") is var sb0 and > 0) { s0 += sb0; n0 = null; ret0 = false; }
        if (sweep.Num("setbackEnd") is var sb1 and > 0) { s1 -= sb1; n1 = null; ret1 = false; }
        if (s1 - s0 < 0.005) { problems.Add("The sweep has no length left on its wall."); return o; }
        var level = Level(m, sweep, wall);

        // The openings it would cross: those whose height meets the profile's.
        var gaps = new List<(double A, double B)>();
        if (sweep.Attrs["gaps"] is not JsonValue gv || !gv.TryGetValue<bool>(out var on) || on)
        {
            var across = Across(sweep, shape);
            var (z0, z1) = (level + across.Min(q => q.Y) - Base(m, wall), level + across.Max(q => q.Y) - Base(m, wall));
            foreach (var p in Openings.PlacedOn(m, wall.Id))
                if (p.Sill < z1 - 1e-6 && p.Sill + p.Height > z0 + 1e-6) gaps.Add((p.Along - p.Width / 2, p.Along + p.Width / 2));
        }
        var spans = new List<(double A, double B)>();
        var at = s0;
        foreach (var (ga, gb) in gaps.OrderBy(g => g.A))
        {
            if (gb <= at || ga >= s1) continue;
            if (ga - at > 0.005) spans.Add((at, Math.Min(ga, s1)));
            at = Math.Max(at, gb);
        }
        if (s1 - at > 0.005) spans.Add((at, s1));
        // At an opening's jamb an added sweep stops square, or returns round it when asked.
        var jambs = sweep.Bool("returnsAtOpenings") && sweep.Str("mode") != "cut";
        foreach (var (a, b) in spans)
        {
            IReadOnlyList<Vec2> line = c.IsArc ? c.Line(a, b, yFace) : [start + dir * a + left * yFace, start + dir * b + left * yFace];
            var (atStart, atEnd) = (Math.Abs(a - s0) < 1e-9, Math.Abs(b - s1) < 1e-9);
            o.Add(new Run(line, a, b, atStart ? n0 : null, atEnd ? n1 : null, atStart ? ret0 : jambs, atEnd ? ret1 : jambs));
        }
        return o;
    }

    /// <summary>
    /// At one end of the wall (0: its start, 1: its end), the corner of this sweep's face with the face of a like sweep on the
    /// wall joined there in an L, and that face's outward normal; null when there is none. Like: the same mode, profile,
    /// offset and level, on the face that turns the corner with this one (both outer, or both inner).
    /// </summary>
    static (Vec2 Corner, Vec2 Out)? Partner(IModelView m, Entity sweep, Entity wall, int end, IPathElements paths, Vec2 start, Vec2 dir, Vec2 left, double yFace, int side, double level)
    {
        if (Profiles.Of(m, sweep) is not { } mine) return null;
        var mineJson = mine.ToJson();
        foreach (var r in Joins.Of(m, wall.Id))
        {
            var join = Joins.Compute(m, r, paths, out _);
            if (join == null || join.Kind != JoinKind.L) continue;
            var me = r.Relating == wall.Id ? join.A : join.B;
            if ((me.Connection == Connection.AtStart ? 0 : me.Connection == Connection.AtEnd ? 1 : -1) != end) continue;
            if (m.Get(Joins.Other(r, wall.Id)) is not { Class: IfcClass.Wall } other || PathCurve.Of(other).IsArc) continue;
            var (os, oe) = (Walls.Start(other), Walls.End(other));
            var od = (oe - os).Normalized();
            if (Math.Abs(Vec2.Cross(dir, od)) < 1e-6) continue;
            foreach (var x in m.RelatedOf(other.Id, IfcClass.RelVoidsElement))
            {
                if (x.Class != Class || x.Id == sweep.Id || x.Str("mode") != sweep.Str("mode") || Math.Abs(x.Num("offset") - sweep.Num("offset")) > 1e-6) continue;
                if (Profiles.Of(m, x) is not { } theirs) continue;
                var tj = theirs.ToJson();
                if (!JsonNode.DeepEquals(tj["points"], mineJson["points"]) || !JsonNode.DeepEquals(tj["insertion"], mineJson["insertion"])) continue;
                if (Math.Abs(Level(m, x, other) - level) > 1e-4 || x.Bool("flip") != sweep.Bool("flip")) continue;
                var oside = Side(x);
                var oy = FaceY(m, other, oside);
                var q = os + od.Perp * oy;
                var p = start + left * yFace;
                var t = Vec2.Cross(q - p, od) / Vec2.Cross(dir, od);
                var corner = p + dir * t;
                var joint = end == 0 ? Walls.Start(wall) : Walls.End(wall);
                var reach = Math.Abs(yFace) + Math.Abs(oy) + 0.5;
                if ((corner - joint).Length > 2 * reach) continue;
                var n1 = left * side;
                var n2 = od.Perp * oside;
                var oFar = (os - corner).Length > (oe - corner).Length ? os : oe;
                var d2 = (oFar - corner).Normalized();
                var toCorner = end == 1 ? dir : -dir;
                var (k1, k2) = (Vec2.Dot(n1, d2), Vec2.Dot(n2, -toCorner));
                if (Math.Abs(k1) < 0.1 || Math.Abs(k2) < 0.1 || Math.Sign(k1) != Math.Sign(k2)) continue;
                return (corner, n2);
            }
        }
        return null;
    }

    /// <summary>The sweep's bodies, one a piece: what it adds to the face, or what a reveal takes out of the wall.</summary>
    public static List<Solid> Bodies(Entity sweep, IModelView m, IServices? services, List<string> problems, out List<Run> runs)
    {
        runs = [];
        var o = new List<Solid>();
        if (HostOf(m, sweep) is not { } wall) { problems.Add("The sweep's wall is gone."); return o; }
        if (Profiles.Of(m, sweep) is not { } shape) { problems.Add("The sweep has no profile."); return o; }
        runs = Runs(sweep, wall, m, services, problems);
        var side = Side(sweep);
        var across = Across(sweep, shape).Select(q => new Vec2(q.X * side, q.Y)).ToList();
        var level = Level(m, sweep, wall);
        var tag = sweep.Str("mode") == "cut" ? "reveal" : "sweep";
        foreach (var run in runs)
        {
            // A return: the end's section turned into the plane square to the face (mitred), and from there the profile
            // wrapped round the corner back to the face's plane.
            var (d0, d1) = ((run.Line[1] - run.Line[0]).Normalized(), (run.Line[^1] - run.Line[^2]).Normalized());
            var sn = run.ReturnAtStart ? -d0 * side : run.StartNormal;
            var en = run.ReturnAtEnd ? d1 * side : run.EndNormal;
            if (ProfileSweeps.Rings(run.Line, across, level, sn, en) is not { } rings) continue;
            Vec3[] Wrap(Vec3[] ring, Vec2 d) => ring.Select((v, k) => v - (d.Perp * across[k].X).At(0)).ToArray();
            if (run.ReturnAtStart) rings.Insert(0, Wrap(rings[0], d0));
            if (run.ReturnAtEnd) rings.Add(Wrap(rings[^1], d1));
            o.Add(ProfileSweeps.Solid(rings, tag));
        }
        return o;
    }

    public static ElementGeometry Geometry(Entity sweep, IModelView m, IServices services)
    {
        var problems = new List<string>();
        var bodies = Bodies(sweep, m, services, problems, out var runs);
        var plan = runs.Select(r => new PlanItem("hit", false, r.Line)).ToList();
        IReadOnlyList<Vec2> snaps = runs.SelectMany(r => new[] { r.Line[0], r.Line[^1] }).ToList();
        if (sweep.Str("mode") == "cut")
        {
            // A reveal has no body of its own: the wall shows the groove; in 3D its edges on the face are drawn, to see and pick it.
            var lines = new List<IReadOnlyList<Vec3>>();
            if (HostOf(m, sweep) is { } wall && Profiles.Of(m, sweep) is { } shape)
            {
                var side = Side(sweep);
                var across = Across(sweep, shape).Select(q => new Vec2(q.X * side, q.Y)).ToList();
                foreach (var run in runs)
                    if (ProfileSweeps.Rings(run.Line, across, Level(m, sweep, wall), run.StartNormal, run.EndNormal) is { } rings)
                    {
                        lines.Add([.. rings[0], rings[0][0]]);
                        lines.Add([.. rings[^1], rings[^1][0]]);
                        for (var k = 0; k < across.Count; k++) lines.Add(rings.Select(r => r[k]).ToList());
                    }
            }
            return new ElementGeometry([], plan, snaps, problems.Count > 0 ? problems : null, Lines: lines, LineColor: "#6b5d4a");
        }
        var material = sweep.Str("material");
        var parts = bodies.Count == 0 ? [] : new List<GeometryPart> { new("Sweep", Materials.ColorOf(m, material), Openings.Combine(bodies, "sweep"), material, "sweep") };
        return new ElementGeometry(parts, plan, snaps, problems.Count > 0 ? problems : null);
    }

    /// <summary>The length along the face, as built: its pieces' lines (the openings' gaps left out).</summary>
    public static double LengthOf(ElementGeometry? g) =>
        g?.Plan?.Where(p => p.Kind == "hit").Sum(p => p.Points.Zip(p.Points.Skip(1), (a, b) => (b - a).Length).Sum()) ?? 0;

    public static IEnumerable<QuantitySet> Quantities(Entity sweep, ElementGeometry? g, IModelView m)
    {
        if (g?.Plan == null) yield break;
        var length = LengthOf(g);
        var area = Profiles.Of(m, sweep)?.Area ?? 0;
        var pieces = g.Plan.Count(p => p.Kind == "hit");
        var cut = sweep.Str("mode") == "cut";
        var volume = cut ? area * length : g.Parts.Sum(p => Measure.Volume(p.Solid));
        yield return new QuantitySet("Lapin_WallSweepQuantities",
        [
            new("Length", length, "LENGTH", "Length"), new("Pieces", pieces, "COUNT", "Pieces"),
            new("CrossSectionArea", area, "AREA", "Profile area"),
            new(cut ? "RemovedVolume" : "Volume", volume, "VOLUME", cut ? "Volume removed" : "Volume"),
        ]);
    }

    /// <summary>The reveals' bodies taken out of their wall's parts (a geometry filter), only where the build cuts openings (IFC writes them as recesses).</summary>
    sealed class Cuts : IGeometryFilter
    {
        public ElementGeometry? Apply(Entity entity, ElementGeometry geometry, IModelView model, IServices services)
        {
            if (entity.Class != IfcClass.Wall || geometry.Parts.Count == 0) return null;
            var reveals = model.RelatedOf(entity.Id, IfcClass.RelVoidsElement).Where(x => x.Class == Class && x.Str("mode") == "cut").ToList();
            if (reveals.Count == 0) return null;
            if (services.Get<GeometryOptions>()?.CutOpenings == false) return null;
            var cutters = reveals.SelectMany(r => Bodies(r, model, services, [], out _)).ToList();
            if (cutters.Count == 0) return null;
            if (services.Get<ISolidBooleans>() is not { } booleans)
                return geometry with { Diagnostics = [.. geometry.Diagnostics ?? [], "Reveals need the boolean library (manifold), missing here."] };
            var parts = new List<GeometryPart>();
            foreach (var part in geometry.Parts)
            {
                var b = Measure.Bounds(part.Solid);
                var near = cutters.Where(c => b is { } pb && Measure.Bounds(c) is { } cb && pb.Min.X <= cb.Max.X && cb.Min.X <= pb.Max.X && pb.Min.Y <= cb.Max.Y && cb.Min.Y <= pb.Max.Y && pb.Min.Z <= cb.Max.Z && cb.Min.Z <= pb.Max.Z).ToList();
                if (near.Count == 0) { parts.Add(part); continue; }
                try
                {
                    if (booleans.Difference(part.Solid, near, part.Solid.Tag) is { } cut) { if (Measure.Volume(cut) > 1e-9) parts.Add(part with { Solid = cut }); }
                    else parts.Add(part);
                }
                catch (InvalidOperationException) { parts.Add(part); }
            }
            return geometry with { Parts = parts };
        }
    }

    /// <summary>Reveals in IFC: IfcOpeningElement RECESS voiding the wall.</summary>
    public sealed class Recesses : IIfcRecesses
    {
        public Solid? RecessOf(IModelView model, IServices services, Entity element) => element.Class == Class ? RecessBody(element, model, services) : null;
    }

    /// <summary>A reveal's body for IFC (an IfcOpeningElement RECESS voiding the wall): what it takes out of the wall, as one solid.</summary>
    public static Solid? RecessBody(Entity sweep, IModelView m, IServices? services)
    {
        if (sweep.Str("mode") != "cut") return null;
        var bodies = Bodies(sweep, m, services, [], out _);
        return bodies.Count == 0 ? null : Openings.Combine(bodies, "reveal");
    }
}
