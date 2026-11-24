using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Steel;

/// <summary>
/// One chord of an open-web joist: a pair of angles back to back (<see cref="JoistSpec.Gap"/> apart), each <see cref="Leg"/>
/// wide (its horizontal leg), <see cref="Height"/> high (its vertical leg, the one against the gap) and <see cref="Thickness"/>
/// thick; <see cref="Flange"/> says where its horizontal leg is, "top" or "bottom".
/// </summary>
public sealed record JoistChord(double Leg, double Height, double Thickness, string Flange)
{
    /// <summary>The angle's two rectangles in (w, z), w out from the gap's face, z down from the chord's top (0) to −<see cref="Height"/>: the vertical leg, then the flange beside it.</summary>
    public IReadOnlyList<(double W0, double W1, double Z0, double Z1)> Rects(double top) => Flange == "top"
        ? [(0, Thickness, top - Height, top), (Thickness, Leg, top - Thickness, top)]
        : [(0, Thickness, top - Height, top), (Thickness, Leg, top - Height, top - Height + Thickness)];

    /// <summary>Its section's outline in (w, z), anticlockwise, its top at <paramref name="top"/>.</summary>
    public IReadOnlyList<Vec2> Outline(double top) => Flange == "top"
        ? [new(0, top - Height), new(Thickness, top - Height), new(Thickness, top - Thickness), new(Leg, top - Thickness), new(Leg, top), new(0, top)]
        : [new(0, top - Height), new(Leg, top - Height), new(Leg, top - Height + Thickness), new(Thickness, top - Height + Thickness), new(Thickness, top), new(0, top)];

    public double Area => Leg * Thickness + (Height - Thickness) * Thickness;
}

/// <summary>
/// An open-web joist type's rules (docs/plan-steel.md, Grinzile cu zăbrele; decision 827): its <see cref="Depth"/> from the bearing
/// (the joist's top, its axis) to the bottom chord's underside; its top chord <see cref="TopDrop"/> under the bearing (0: at
/// the top) and its bottom chord, each a pair of angles <see cref="Gap"/> apart; the bottom chord <see cref="BottomSetback"/>
/// short of each end; a bearing seat at each end, <see cref="SeatLength"/> long and <see cref="SeatDepth"/> deep from the top
/// (a pair of angles: a vertical leg the top chord's thickness, a flange at its top and at its bottom, where the top chord is
/// not already), none when either is 0; its webs (<see cref="Webs"/>: round bars "rod" <see cref="WebSize"/> across, angles
/// "angle" <see cref="WebSize"/> × <see cref="WebThickness"/>, or "lines", drawn as their axes only, with no body, as
/// Revit's bar joist families draw them) in a Warren pattern between the chords' mid-heights, a panel <see cref="Panel"/> long
/// at most along the bottom chord, with a vertical at each inner panel point of the bottom chord when <see cref="Verticals"/>,
/// and a diagonal at each end from the seat down to the bottom chord's end.
/// </summary>
public sealed record JoistSpec(double Depth, JoistChord Top, JoistChord Bottom, double Gap, double TopDrop, double BottomSetback,
    double SeatDepth, double SeatLength, string Webs, double WebSize, double WebThickness, double Panel, bool Verticals)
{
    public static readonly string[] WebKinds = ["rod", "angle", "lines"];
    public static readonly string[] Flanges = ["top", "bottom"];

    /// <summary>The attribute keys a joist type keeps its rules under (flat, so the properties panel edits them as fields).</summary>
    public static readonly string[] Keys =
    [
        "depth", "gap", "topLeg", "topHeight", "topThickness", "topFlange", "topDrop", "bottomLeg", "bottomHeight", "bottomThickness", "bottomFlange",
        "bottomSetback", "seatDepth", "seatLength", "webs", "webSize", "webThickness", "panel", "verticals",
    ];

    /// <summary>
    /// A few joists to start from, indicative only (Lapin's own round sizes, not a manufacturer's table): each the user's to
    /// change, its depth in the name.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, JoistSpec> Presets = new Dictionary<string, JoistSpec>
    {
        ["OWJ 300"] = Make(0.30, new(0.035, 0.035, 0.004, "top"), new(0.030, 0.030, 0.004, "bottom"), 0.016, 0.5, 0.014),
        ["OWJ 400"] = Make(0.40, new(0.040, 0.040, 0.004, "top"), new(0.035, 0.035, 0.004, "bottom"), 0.018, 0.6, 0.016),
        ["OWJ 500"] = Make(0.50, new(0.050, 0.050, 0.005, "top"), new(0.040, 0.040, 0.004, "bottom"), 0.020, 0.7, 0.018),
        ["OWJ 600"] = Make(0.60, new(0.060, 0.060, 0.006, "top"), new(0.050, 0.050, 0.005, "bottom"), 0.022, 0.8, 0.020),
    };

    public const string DefaultPreset = "OWJ 400";

    static JoistSpec Make(double depth, JoistChord top, JoistChord bottom, double gap, double panel, double rod) =>
        new(depth, top, bottom, gap, 0, panel, 0.064, 0.102, "rod", rod, 0.003, panel, false);

    public JsonObject ToJson() => new()
    {
        ["depth"] = R(Depth), ["gap"] = R(Gap),
        ["topLeg"] = R(Top.Leg), ["topHeight"] = R(Top.Height), ["topThickness"] = R(Top.Thickness), ["topFlange"] = Top.Flange, ["topDrop"] = R(TopDrop),
        ["bottomLeg"] = R(Bottom.Leg), ["bottomHeight"] = R(Bottom.Height), ["bottomThickness"] = R(Bottom.Thickness), ["bottomFlange"] = Bottom.Flange,
        ["bottomSetback"] = R(BottomSetback), ["seatDepth"] = R(SeatDepth), ["seatLength"] = R(SeatLength),
        ["webs"] = Webs, ["webSize"] = R(WebSize), ["webThickness"] = R(WebThickness), ["panel"] = R(Panel), ["verticals"] = Verticals,
    };

    /// <summary>A type's rules from its attributes, each missing one the default preset's.</summary>
    public static JoistSpec Of(JsonObject? a)
    {
        var d = Presets[DefaultPreset];
        double N(string k, double dv) => Json.Number(a?[k]) ?? dv;
        string S(string k, string dv, string[] allowed) => (string?)(a?[k] as JsonValue) is { } s && allowed.Contains(s) ? s : dv;
        var verticals = a?["verticals"] is JsonValue v && v.TryGetValue<bool>(out var b) ? b : d.Verticals;
        return new JoistSpec(N("depth", d.Depth),
            new JoistChord(N("topLeg", d.Top.Leg), N("topHeight", d.Top.Height), N("topThickness", d.Top.Thickness), S("topFlange", d.Top.Flange, Flanges)),
            new JoistChord(N("bottomLeg", d.Bottom.Leg), N("bottomHeight", d.Bottom.Height), N("bottomThickness", d.Bottom.Thickness), S("bottomFlange", d.Bottom.Flange, Flanges)),
            N("gap", d.Gap), N("topDrop", d.TopDrop), N("bottomSetback", d.BottomSetback), N("seatDepth", d.SeatDepth), N("seatLength", d.SeatLength),
            S("webs", d.Webs, WebKinds), N("webSize", d.WebSize), N("webThickness", d.WebThickness), N("panel", d.Panel), verticals);
    }

    /// <summary>What makes the rules no joist (null: they are one).</summary>
    public string? Problem()
    {
        if (!(Depth >= 0.05 && Depth <= 3)) return "A joist is 50 mm to 3 m deep.";
        foreach (var (c, which) in new[] { (Top, "top"), (Bottom, "bottom") })
        {
            if (!(c.Thickness > 0) || !(c.Leg > c.Thickness) || !(c.Height > c.Thickness)) return $"The {which} chord's angles need legs longer than they are thick.";
        }
        if (!(Gap >= 0) || Gap > Depth) return "The angles' gap is 0 or more, less than the joist's depth.";
        if (TopDrop < 0 || TopDrop + Top.Height + Bottom.Height > Depth) return "The two chords must fit in the joist's depth, the top one under its top.";
        if (BottomSetback < 0) return "The bottom chord's setback is 0 or more.";
        if (SeatDepth < 0 || SeatLength < 0 || SeatDepth > Depth) return "A seat is 0 or more deep and long, no deeper than the joist.";
        if (Webs != "lines" && !(WebSize > 0)) return "Webs of bars or angles need a size.";
        if (Webs == "angle" && !(WebThickness > 0 && WebThickness < WebSize)) return "Angle webs need a thickness less than their leg.";
        if (!(Panel >= 0.05)) return "A panel is 50 mm long at least.";
        return null;
    }

    /// <summary>The heights (down from the top, negative) the webs work between: the chords' mid-heights.</summary>
    public (double Top, double Bottom) WorkLines => (-TopDrop - Top.Height / 2, -Depth + Bottom.Height / 2);

    static double R(double v) => Math.Round(v, 6);
}

/// <summary>
/// The steel module's open-web joists (docs/plan-steel.md, Grinzile cu zăbrele; decision 827): <c>LapinSteelJoist</c>, along a
/// straight axis at its bearing (its top, as a joist's elevation is given), its rules from its type (<c>LapinSteelJoistType</c>,
/// <see cref="JoistSpec"/>), its webs the type's Warren pattern or, when it was recognised, the webs its file drew (<see cref="WebAxesKey"/>).
/// <para>
/// IFC: an <c>IfcBeam</c> of predefined type JOIST, typed by an <c>IfcBeamType</c> JOIST, its body every chord, seat and web: one
/// element, as Revit, Tekla and the joist makers write a joist and as it is ordered and delivered (an assembly of members would
/// make each web an element of its own in schedules and in other programs). The structure module owns <c>IfcBeam</c>, so the joist
/// is a class of the steel module written as one (<c>exportAs</c>), as its members are written as IfcMember.
/// </para>
/// </summary>
public static class OpenWebJoists
{
    public const string Class = "LapinSteelJoist";
    public const string TypeClass = "LapinSteelJoistType";
    /// <summary>A joist's own webs, as its file drew them (recognition): [[u0, z0, u1, z1], …], along from its start and down from its top, metres.</summary>
    public const string WebAxesKey = "webAxes";

    public static void Register(IModuleRegistry r)
    {
        r.Class(new(Class, "Open-web joist", [], "#8a96a3"));
        r.Class(new(TypeClass, "Joist type",
        [
            new("depth", "Depth", "length", 0.05, 3),
            new("gap", "Gap between the angles", "length", 0),
            new("topLeg", "Top chord: leg", "length", 0.005),
            new("topHeight", "Top chord: height", "length", 0.005),
            new("topThickness", "Top chord: thickness", "length", 0.001),
            new("topFlange", "Top chord: flange", "choice", Choices: JoistSpec.Flanges),
            new("topDrop", "Top chord under the bearing", "length", 0),
            new("bottomLeg", "Bottom chord: leg", "length", 0.005),
            new("bottomHeight", "Bottom chord: height", "length", 0.005),
            new("bottomThickness", "Bottom chord: thickness", "length", 0.001),
            new("bottomFlange", "Bottom chord: flange", "choice", Choices: JoistSpec.Flanges),
            new("bottomSetback", "Bottom chord setback", "length", 0),
            new("seatDepth", "Seat depth", "length", 0),
            new("seatLength", "Seat length", "length", 0),
            new("webs", "Webs", "choice", Choices: JoistSpec.WebKinds),
            new("webSize", "Web size", "length", 0, ShowIf: [new Dictionary<string, string[]> { ["webs"] = ["rod", "angle"] }]),
            new("webThickness", "Web thickness", "length", 0, ShowIf: [new Dictionary<string, string[]> { ["webs"] = ["angle"] }]),
            new("panel", "Panel", "length", 0.05),
            new("verticals", "Verticals", "bool"),
            new("material", "Material", "materialRef"),
        ], "#8a96a3", Kind: "type"));
        r.Geometry(Class, Geometry);
        r.Quantities(Class, Quantities);
        r.Catalog("joistPresets", new JsonArray([.. JoistSpec.Presets.Select(p => (JsonNode)new JsonObject { ["name"] = p.Key, ["spec"] = p.Value.ToJson() })]));
        r.Service<IBodyRecognizer>(new JoistRecognizer());

        // {name?, preset?, …rules} → {id}: a joist type, from a preset (OWJ 400 by default) with any rule given over it.
        r.Command("createJoistType", "New joist type", (ctx, a) => new JsonObject { ["id"] = CreateType(ctx, (string?)a["name"], SpecFrom(a)).Id });

        // A joist along two points: in plan (no heights) bearing on the storey's top; with heights, its top at them.
        r.Command("addJoist", "Open-web joist", (ctx, a) =>
        {
            var storey = Spatial.Require(ctx.Model, (string?)a["storeyId"] ?? Spatial.Storeys(ctx.Model).FirstOrDefault()?.Id, IfcClass.BuildingStorey);
            var top = storey.Num("height", 3);
            var s = new Vec3(Args.Number(a, "x1"), Args.Number(a, "y1"), Json.Number(a["z1"]) ?? top);
            var e = new Vec3(Args.Number(a, "x2"), Args.Number(a, "y2"), Json.Number(a["z2"]) ?? Json.Number(a["z1"]) ?? top);
            if ((e - s).Length < 0.1) throw new CommandException("A joist is 100 mm long at least.");
            Entity type;
            if ((string?)a["typeId"] is { } typeId) type = ctx.Model.Get(typeId) is { Class: TypeClass } t ? t : throw new CommandException("There is no such joist type.");
            else type = ctx.Model.OfClass(TypeClass).FirstOrDefault() ?? CreateType(ctx, null, JoistSpec.Presets[JoistSpec.DefaultPreset]);
            return new JsonObject { ["id"] = Add(ctx, storey, type, s, e, null, null) };
        });

        // A joist's ends moved (x1…z2, relative to its storey, each kept when not given); its webs drawn from its file go when its
        // length changes (its type's pattern then), or when asked (resetWebs).
        r.Command("updateJoist", "Move joist", (ctx, a) =>
        {
            var e = ctx.Model.Get((string?)a["id"] ?? "") is { Class: Class } x ? x : throw new CommandException("Not an open-web joist.");
            var (s0, t0) = (MemberAxes.Point(e, "start"), MemberAxes.Point(e, "end"));
            var s = new Vec3(Json.Number(a["x1"]) ?? s0.X, Json.Number(a["y1"]) ?? s0.Y, Json.Number(a["z1"]) ?? s0.Z);
            var t = new Vec3(Json.Number(a["x2"]) ?? t0.X, Json.Number(a["y2"]) ?? t0.Y, Json.Number(a["z2"]) ?? t0.Z);
            if ((t - s).Length < 0.1) throw new CommandException("A joist is 100 mm long at least.");
            var attrs = e.CloneAttrs();
            attrs["start"] = P3(s);
            attrs["end"] = P3(t);
            if (a["resetWebs"] is JsonValue rv && rv.TryGetValue<bool>(out var reset) && reset || Math.Abs((t - s).Length - (t0 - s0).Length) > 1e-6) attrs.Remove(WebAxesKey);
            ctx.Replace(e.With(attrs: attrs));
            return null;
        });

        r.Command("setJoistType", "Change joist type", (ctx, a) =>
        {
            var type = ctx.Model.Get((string?)a["typeId"] ?? "") is { Class: TypeClass } t ? t : throw new CommandException("There is no such joist type.");
            foreach (var id in Args.Ids(a))
            {
                var e = ctx.Model.Get(id) is { Class: Class } x ? x : throw new CommandException("Only an open-web joist takes a joist type.");
                var named = e.Name == ctx.Model.RelatingOf(e.Id, IfcClass.RelDefinesByType)?.Name;
                ctx.Relate(IfcClass.RelDefinesByType, type.Id, e.Id);
                if (named) ctx.Replace(ctx.Model.Get(e.Id)!.With(name: type.Name));
            }
            return null;
        });
    }

    /// <summary>The rules given: a preset's (by name), with any rule key over it; refused when they are no joist.</summary>
    static JoistSpec SpecFrom(JsonObject a)
    {
        var preset = (string?)a["preset"] ?? JoistSpec.DefaultPreset;
        if (!JoistSpec.Presets.TryGetValue(preset, out var basis)) throw new CommandException($"There is no joist preset „{preset}”.");
        var json = basis.ToJson();
        foreach (var k in JoistSpec.Keys) if (a[k] is { } v) json[k] = v.DeepClone();
        var spec = JoistSpec.Of(json);
        return spec.Problem() is { } p ? throw new CommandException(p) : spec;
    }

    public static Entity CreateType(IEdit edit, string? name, JoistSpec spec, string? material = null)
    {
        var attrs = spec.ToJson();
        attrs["material"] = Materials.Ensure(edit, material ?? "Steel").Id;
        var type = new Entity(edit.NewId(), TypeClass, string.IsNullOrWhiteSpace(name) ? $"Joist {spec.Depth * 1000:0}" : name.Trim(), attrs, "JOIST");
        edit.Add(type);
        return type;
    }

    /// <summary>A joist on a storey from world points (kept relative to its level), with its own id when given (a recognised one keeps its GlobalId).</summary>
    public static string Add(IEdit edit, Entity storey, Entity type, Vec3 start, Vec3 end, string? id, IReadOnlyList<(double U0, double Z0, double U1, double Z1)>? webs)
    {
        var level = storey.Num("elevation");
        var attrs = new JsonObject { ["exportAs"] = "IfcBeam", ["start"] = P3(start with { Z = start.Z - level }), ["end"] = P3(end with { Z = end.Z - level }) };
        if (webs != null) attrs[WebAxesKey] = new JsonArray([.. webs.Select(w => (JsonNode)new JsonArray(R(w.U0), R(w.Z0), R(w.U1), R(w.Z1)))]);
        var joist = new Entity(id ?? edit.NewId(), Class, type.Name, attrs, "JOIST");
        edit.Add(joist);
        Spatial.Contain(edit, storey.Id, joist.Id);
        edit.Relate(IfcClass.RelDefinesByType, type.Id, joist.Id);
        return joist.Id;
    }

    static JsonArray P3(Vec3 p) => new(R(p.X), R(p.Y), R(p.Z));
    static double R(double v) => Math.Round(v, 9);

    /// <summary>A joist's frame: its start (world, at its top), along it, across it (left, level) and up (square to it, in its vertical plane), and its length.</summary>
    public readonly record struct Frame(Vec3 Origin, Vec3 U, Vec3 V, Vec3 W, double Length)
    {
        public Vec3 At(double u, double v, double z) => Origin + U * u + V * v + W * z;
    }

    public static Frame? FrameOf(IModelView m, Entity e)
    {
        var z = Spatial.Elevation(m, e);
        var (s, t) = (MemberAxes.Point(e, "start"), MemberAxes.Point(e, "end"));
        s = s with { Z = s.Z + z };
        t = t with { Z = t.Z + z };
        var d = t - s;
        if (d.Length < 1e-3 || new Vec2(d.X, d.Y).Length < 1e-6) return null;
        var u = d.Normalized();
        var v = new Vec3(-u.Y, u.X, 0).Normalized();
        return new Frame(s, u, v, Vec3.Cross(u, v).Normalized(), d.Length);
    }

    public static JoistSpec SpecOf(IModelView m, Entity joist) => JoistSpec.Of(m.RelatingOf(joist.Id, IfcClass.RelDefinesByType)?.Attrs);

    /// <summary>The joist's webs as axes (u along, z down from its top): its own, as its file drew them, else its type's Warren pattern.</summary>
    public static List<(double U0, double Z0, double U1, double Z1)> WebAxes(Entity e, JoistSpec spec, double length)
    {
        if (e.Attrs[WebAxesKey] is JsonArray own)
            return [.. own.OfType<JsonArray>().Where(w => w.Count == 4).Select(w => (Json.Number(w[0]) ?? 0, Json.Number(w[1]) ?? 0, Json.Number(w[2]) ?? 0, Json.Number(w[3]) ?? 0))];
        return Warren(spec, length);
    }

    /// <summary>
    /// The type's webs on a joist <paramref name="length"/> long: an end diagonal from each seat's end on the top work line down
    /// to the bottom chord's end; between the bottom chord's ends, panels of equal length (no longer than the type's panel),
    /// a diagonal up to the top chord at each panel's middle and down again; a vertical at each inner panel point if asked.
    /// </summary>
    public static List<(double U0, double Z0, double U1, double Z1)> Warren(JoistSpec spec, double length)
    {
        var (zt, zb) = spec.WorkLines;
        var setback = Math.Clamp(spec.BottomSetback, 0, length / 2 - 0.01);
        var seat = Math.Min(spec.SeatLength, setback);
        var o = new List<(double, double, double, double)>();
        if (setback > seat + 1e-6) { o.Add((seat, zt, setback, zb)); o.Add((length - seat, zt, length - setback, zb)); }
        var run = length - 2 * setback;
        if (run < 0.02) return o;
        var n = Math.Max(1, (int)Math.Ceiling(run / spec.Panel - 1e-9));
        var p = run / n;
        for (var k = 0; k < n; k++)
        {
            var (b0, mid, b1) = (setback + k * p, setback + (k + 0.5) * p, setback + (k + 1) * p);
            o.Add((b0, zb, mid, zt));
            o.Add((mid, zt, b1, zb));
            if (spec.Verticals && k > 0) o.Add((b0, zb, b0, zt));
        }
        return o;
    }

    /// <summary>The pieces of a joist's body in its frame: each chord's angles, each seat's angles, each web's bar or angle.</summary>
    public static List<(string Key, string Name, Solid Solid)> Pieces(Frame f, JoistSpec spec, IReadOnlyList<(double U0, double Z0, double U1, double Z1)> webs)
    {
        var o = new List<(string, string, Solid)>();
        var half = spec.Gap / 2;
        var topZ = -spec.TopDrop;
        var bottomTop = -spec.Depth + spec.Bottom.Height;
        var setback = Math.Clamp(spec.BottomSetback, 0, f.Length / 2 - 0.005);
        void Chord(string key, string name, JoistChord c, double top, double u0, double u1)
        {
            foreach (var (side, sign) in new[] { ("left", 1.0), ("right", -1.0) })
            {
                var ring = c.Outline(top).Select(q => f.At(u0, sign * (half + q.X), q.Y)).ToList();
                if (Prism.ExtrudeOriented(ring, f.U * (u1 - u0), key) is { } s) o.Add(($"{key}:{side}", name, s));
            }
        }
        Chord("top", "Top chord", spec.Top, topZ, 0, f.Length);
        Chord("bottom", "Bottom chord", spec.Bottom, bottomTop, setback, f.Length - setback);
        // The seats: where the top chord is not already, a vertical leg its thickness from the top down the seat's depth, a flange at
        // its top and one at its bottom, at each end.
        var seat = Math.Min(spec.SeatLength, f.Length / 2);
        if (spec.SeatDepth > 1e-6 && seat > 1e-6)
        {
            var t = spec.Top.Thickness;
            var region = new List<(double, double, double, double)> { (0, t, -spec.SeatDepth, 0), (t, spec.Top.Leg, -t, 0), (t, spec.Top.Leg, -spec.SeatDepth, -spec.SeatDepth + t) };
            var chord = spec.Top.Rects(topZ);
            var left = region.SelectMany(rect => Subtract(rect, chord)).Where(x => x.W1 - x.W0 > 1e-6 && x.Z1 - x.Z0 > 1e-6).ToList();
            foreach (var (end, u0) in new[] { ("start", 0.0), ("end", f.Length - seat) })
                foreach (var (side, sign) in new[] { ("left", 1.0), ("right", -1.0) })
                    for (var i = 0; i < left.Count; i++)
                    {
                        var (w0, w1, z0, z1) = left[i];
                        var ring = new[] { new Vec2(w0, z0), new Vec2(w1, z0), new Vec2(w1, z1), new Vec2(w0, z1) }.Select(q => f.At(u0, sign * (half + q.X), q.Y)).ToList();
                        if (Prism.ExtrudeOriented(ring, f.U * seat, "seat") is { } s) o.Add(($"seat:{end}:{side}:{i}", "Seat", s));
                    }
        }
        if (spec.Webs == "lines") return o;
        for (var i = 0; i < webs.Count; i++)
        {
            var (u0, z0, u1, z1) = webs[i];
            var a = f.At(u0, 0, z0);
            var b = f.At(u1, 0, z1);
            var d = b - a;
            if (d.Length < 1e-3) continue;
            var dir = d.Normalized();
            // Square to the web, in the joist's plane (w) and across it (v).
            var across = f.V;
            var w = Vec3.Cross(dir, across).Normalized();
            List<Vec3> ring;
            if (spec.Webs == "rod")
                ring = [.. Chords.Ring(new Vec2(0, 0), spec.WebSize / 2).Select(q => a + across * q.X + w * q.Y)];
            else
            {
                // An angle, its heel on the web's axis: one leg across the joist, the other in its plane.
                var (l, th) = (spec.WebSize, spec.WebThickness);
                var c = new Vec2(-l / 2, -l / 2);
                ring = [.. new[] { new Vec2(0, 0), new Vec2(l, 0), new Vec2(l, th), new Vec2(th, th), new Vec2(th, l), new Vec2(0, l) }.Select(q => a + across * (q.X + c.X) + w * (q.Y + c.Y))];
            }
            if (Prism.ExtrudeOriented(ring, d, "web") is { } s) o.Add(($"web:{i}", "Web", s));
        }
        return o;
    }

    /// <summary>A rectangle less the others (axis-aligned in (w, z)): the pieces of it none of them covers.</summary>
    static List<(double W0, double W1, double Z0, double Z1)> Subtract((double W0, double W1, double Z0, double Z1) a, IReadOnlyList<(double W0, double W1, double Z0, double Z1)> cut)
    {
        var pieces = new List<(double W0, double W1, double Z0, double Z1)> { a };
        foreach (var c in cut)
        {
            var next = new List<(double, double, double, double)>();
            foreach (var p in pieces)
            {
                var (iw0, iw1, iz0, iz1) = (Math.Max(p.W0, c.W0), Math.Min(p.W1, c.W1), Math.Max(p.Z0, c.Z0), Math.Min(p.Z1, c.Z1));
                if (iw1 - iw0 <= 1e-9 || iz1 - iz0 <= 1e-9) { next.Add(p); continue; }
                if (p.Z1 > iz1) next.Add((p.W0, p.W1, iz1, p.Z1));
                if (p.Z0 < iz0) next.Add((p.W0, p.W1, p.Z0, iz0));
                if (p.W0 < iw0) next.Add((p.W0, iw0, iz0, iz1));
                if (p.W1 > iw1) next.Add((iw1, p.W1, iz0, iz1));
            }
            pieces = next;
        }
        return pieces;
    }

    static ElementGeometry? Geometry(Entity e, IModelView m, IServices? services)
    {
        if (FrameOf(m, e) is not { } f) return new ElementGeometry([], Diagnostics: ["The joist has no length in plan."]);
        var spec = SpecOf(m, e);
        if (spec.Problem() is { } problem) return new ElementGeometry([], Diagnostics: [problem]);
        var type = m.RelatingOf(e.Id, IfcClass.RelDefinesByType);
        var material = type?.Str("material");
        var color = Materials.ColorOf(m, material);
        var webs = WebAxes(e, spec, f.Length);
        var parts = Pieces(f, spec, webs).Select(p => new GeometryPart(p.Name, color, p.Solid, material, p.Key)).ToList();
        // In plan, a joist's single line along its axis with its type's name over its middle (as framing plans draw joists), its
        // footprint to pick it by; in 3D, webs drawn as lines are their axes.
        var (a, b) = (f.Origin.Xy, f.At(f.Length, 0, 0).Xy);
        var half = spec.Gap / 2 + Math.Max(spec.Top.Leg, spec.Bottom.Leg);
        var n = f.V.Xy;
        var mid = (a + b) * 0.5;
        var angle = Math.Atan2(b.Y - a.Y, b.X - a.X) * 180 / Math.PI;
        if (angle > 90 + 1e-9) angle -= 180;
        else if (angle <= -90 + 1e-9) angle += 180;
        var plan = new List<PlanItem>
        {
            new("symbol", false, [a, b]),
            new("hit", true, [a + n * half, b + n * half, b - n * half, a - n * half]),
            new("text", false, [mid + n * 0.08]) { Text = type?.Name ?? e.Name, TextHeight = 0.2, TextAngle = Math.Round(angle, 6), Paper = new PaperAnchor(mid, 100) },
        };
        IReadOnlyList<IReadOnlyList<Vec3>>? lines = spec.Webs == "lines" ? [.. webs.Select(w => (IReadOnlyList<Vec3>)[f.At(w.U0, 0, w.Z0), f.At(w.U1, 0, w.Z1)])] : null;
        return new ElementGeometry(parts, plan, [a, b], Lines: lines, LineColor: lines == null ? null : color);
    }

    static IEnumerable<QuantitySet> Quantities(Entity e, ElementGeometry? g, IModelView m)
    {
        if (g == null || FrameOf(m, e) is not { } f) yield break;
        var spec = SpecOf(m, e);
        var net = g.Parts.Sum(p => Measure.Volume(p.Solid));
        var webs = WebAxes(e, spec, f.Length);
        yield return new QuantitySet("Qto_BeamBaseQuantities",
        [
            new("Length", f.Length, "LENGTH"),
            new("NetVolume", net, "VOLUME", "Net volume"),
            new("NetWeight", net * SteelModule.SteelDensity, "WEIGHT", "Net weight"),
        ]);
        yield return new QuantitySet("Lapin_JoistQuantities",
        [
            new("Depth", spec.Depth, "LENGTH"),
            new("WebCount", webs.Count, "COUNT", "Webs"),
            new("WebLength", webs.Sum(w => Math.Sqrt((w.U1 - w.U0) * (w.U1 - w.U0) + (w.Z1 - w.Z0) * (w.Z1 - w.Z0))), "LENGTH", "Webs' length"),
        ]);
    }
}
