using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Platform.ProfileLibraries;

namespace Lapin.Architecture;

/// <summary>How one direction of a curtain wall's grid divides it: a count of equal bays, a fixed spacing (the rest at the start, the end or split), a widest spacing (equal bays no wider), or explicit offsets.</summary>
public sealed record CurtainGrid(string Mode, int Count = 1, double Spacing = 1.5, string Justify = "center", IReadOnlyList<double>? Offsets = null)
{
    public static readonly string[] Modes = ["count", "spacing", "max", "offsets"];

    /// <summary>The grid lines inside an extent (0 and the extent left out), ascending.</summary>
    public List<double> Lines(double extent)
    {
        const double eps = 1e-6;
        var o = new List<double>();
        switch (Mode)
        {
            case "count":
                for (var i = 1; i < Math.Max(1, Count); i++) o.Add(extent * i / Math.Max(1, Count));
                break;
            case "spacing":
            {
                var s = Math.Max(Spacing, 0.05);
                var n = (int)Math.Floor(extent / s + 1e-9);
                var rest = extent - n * s;
                var first = Justify == "start" ? rest : Justify == "end" ? 0 : rest / 2;
                for (var i = 0; i <= n; i++) o.Add(first + i * s);
                break;
            }
            case "offsets":
                o.AddRange(Offsets ?? []);
                break;
            default:   // max
            {
                var n = Math.Max(1, (int)Math.Ceiling(extent / Math.Max(Spacing, 0.05) - 1e-9));
                for (var i = 1; i < n; i++) o.Add(extent * i / n);
                break;
            }
        }
        var inside = o.Where(x => double.IsFinite(x) && x > eps && x < extent - eps).Order().ToList();
        return inside.Where((x, i) => i == 0 || x - inside[i - 1] > eps).ToList();
    }

    public static CurtainGrid From(JsonNode? n, CurtainGrid fallback)
    {
        if (n is not JsonObject o) return fallback;
        var mode = (string?)o["mode"] is { } m && Modes.Contains(m) ? m : fallback.Mode;
        var offsets = (o["offsets"] as JsonArray)?.Select(x => Json.Number(x)).OfType<double>().ToList();
        return new CurtainGrid(mode, (int)(Json.Number(o["count"]) ?? fallback.Count), Json.Number(o["spacing"]) ?? fallback.Spacing,
            (string?)o["justify"] is "start" or "center" or "end" ? (string)o["justify"]! : fallback.Justify, offsets ?? fallback.Offsets);
    }

    public JsonObject ToJson()
    {
        var o = new JsonObject { ["mode"] = Mode };
        switch (Mode)
        {
            case "count": o["count"] = Count; break;
            case "spacing": o["spacing"] = Spacing; o["justify"] = Justify; break;
            case "offsets": o["offsets"] = new JsonArray((Offsets ?? []).Select(x => (JsonNode)x).ToArray()); break;
            default: o["spacing"] = Spacing; break;
        }
        return o;
    }
}

/// <summary>
/// A member's section: in the wall's plane across the member (width), and across the wall (depth), metres; <see cref="Profile"/>,
/// the library profile it was made of (a section's JSON, decision 417): its body takes that profile's real outline, its x across
/// the member, its y out of the building; its width and depth are then the profile's.
/// </summary>
public readonly record struct CurtainSection(double Width, double Depth, JsonObject? Profile = null, MemberPlace? Place = null);

/// <summary>
/// How a member's profile sits on its member (report #128): turned (degrees, counter-clockwise, in the profile's own x across the
/// member and y out of the building), mirrored across x first, aligned (<c>Across</c>: front, center or back, the profile's outer
/// or inner face on the member's axis plane; <c>Along</c>: start, center or end, its edge on the member's line) and moved
/// (<c>Dx</c> across the member, <c>Dy</c> out of the building, metres). A type holds one for its mullions and one for its
/// transoms; a curtain wall holds its own for chosen members (<c>memberStyles</c>), over the type's.
/// </summary>
public sealed record MemberPlace(double Rotate = 0, bool Mirror = false, string Across = "center", string Along = "center", double Dx = 0, double Dy = 0)
{
    public static readonly string[] AcrossModes = ["front", "center", "back"], AlongModes = ["start", "center", "end"];

    public bool IsDefault => Math.Abs(Rotate) < 1e-9 && !Mirror && Across == "center" && Along == "center" && Math.Abs(Dx) < 1e-9 && Math.Abs(Dy) < 1e-9;

    /// <summary>A place from its JSON; what it does not say is <paramref name="fallback"/>'s.</summary>
    public static MemberPlace From(JsonNode? n, MemberPlace? fallback = null)
    {
        var f = fallback ?? new MemberPlace();
        if (n is not JsonObject o) return f;
        return new MemberPlace(
            Json.Number(o["rotate"]) is { } r && double.IsFinite(r) ? ((r % 360) + 360) % 360 : f.Rotate,
            o["mirror"] is JsonValue m && m.TryGetValue<bool>(out var mirror) ? mirror : f.Mirror,
            (string?)o["across"] is { } a && AcrossModes.Contains(a) ? a : f.Across,
            (string?)o["along"] is { } l && AlongModes.Contains(l) ? l : f.Along,
            Json.Number(o["dx"]) is { } dx && double.IsFinite(dx) ? dx : f.Dx,
            Json.Number(o["dy"]) is { } dy && double.IsFinite(dy) ? dy : f.Dy);
    }

    public JsonObject ToJson()
    {
        var o = new JsonObject();
        if (Math.Abs(Rotate) > 1e-9) o["rotate"] = Math.Round(Rotate, 6);
        if (Mirror) o["mirror"] = true;
        if (Across != "center") o["across"] = Across;
        if (Along != "center") o["along"] = Along;
        if (Math.Abs(Dx) > 1e-9) o["dx"] = Math.Round(Dx, 6);
        if (Math.Abs(Dy) > 1e-9) o["dy"] = Math.Round(Dy, 6);
        return o;
    }

    /// <summary>The bodies of a profile as this place leaves them: mirrored, turned, aligned by their box, moved.</summary>
    public IReadOnlyList<MemberOutline> Apply(IReadOnlyList<MemberOutline> bodies)
    {
        if (IsDefault || bodies.Count == 0) return bodies;
        var (c, s) = (Math.Cos(Rotate * Math.PI / 180), Math.Sin(Rotate * Math.PI / 180));
        Vec2 T(Vec2 p) { var x = Mirror ? -p.X : p.X; return new Vec2(x * c - p.Y * s, x * s + p.Y * c); }
        IReadOnlyList<Vec2> Ring(IReadOnlyList<Vec2> r) => Mirror ? r.Select(T).Reverse().ToList() : r.Select(T).ToList();
        var turned = bodies.Select(b => new MemberOutline(Ring(b.Outer), b.Holes.Select(Ring).ToList())).ToList();
        var all = turned.SelectMany(b => b.Outer).ToList();
        var (minX, maxX, minY, maxY) = (all.Min(p => p.X), all.Max(p => p.X), all.Min(p => p.Y), all.Max(p => p.Y));
        var sx = Along switch { "start" => -minX, "end" => -maxX, _ => 0.0 } + Dx;
        var sy = Across switch { "front" => -maxY, "back" => -minY, _ => 0.0 } + Dy;
        var shift = new Vec2(sx, sy);
        return turned.Select(b => new MemberOutline(b.Outer.Select(p => p + shift).ToList(), b.Holes.Select(h => (IReadOnlyList<Vec2>)h.Select(p => p + shift).ToList()).ToList())).ToList();
    }
}

/// <summary>What a curtain wall type holds: its grid, its members and panels, their colours.</summary>
public sealed record CurtainSpec(CurtainGrid U, CurtainGrid V, CurtainSection Mullion, CurtainSection Transom, bool Edge, double PanelThickness, string PanelKind,
    string FrameColor, string GlassColor, string OpaqueColor, string? FrameMaterial = null, string? GlassMaterial = null, string? OpaqueMaterial = null)
{
    public static readonly CurtainSpec Default = new(new CurtainGrid("max", Spacing: 1.5), new CurtainGrid("max", Spacing: 1.5),
        new CurtainSection(0.05, 0.15), new CurtainSection(0.05, 0.15), true, 0.024, "glass", "#3f3f46", "#9cc3e6", "#a1a1aa");

    public static CurtainSpec From(JsonNode? n)
    {
        var d = Default;
        if (n is not JsonObject o) return d;
        static CurtainSection Sec(JsonNode? x, CurtainSection f)
        {
            if (x is not JsonObject s) return f;
            var place = s["place"] is JsonObject po && MemberPlace.From(po) is { IsDefault: false } mp ? mp : null;
            // A profile turned a quarter turn stands with its width and depth exchanged.
            var quarter = place != null && Math.Abs(Math.Abs(Math.Sin(place.Rotate * Math.PI / 180)) - 1) < 1e-9;
            if (s["profile"] is JsonObject p && Json.Number(p["width"]) is { } pw && Json.Number(p["depth"]) is { } pd) return quarter ? new(pd, pw, p, place) : new(pw, pd, p, place);
            return new(Json.Number(s["width"]) ?? f.Width, Json.Number(s["depth"]) ?? f.Depth, null, place);
        }
        static string Colour(JsonNode? x, string f) => (string?)x is { Length: 7 } c && c[0] == '#' ? c : f;
        static string? Named(JsonNode? x) => x is JsonValue v && v.TryGetValue<string>(out var t) && t.Trim().Length > 0 ? t.Trim() : null;
        var panel = o["panel"] as JsonObject;
        return new CurtainSpec(CurtainGrid.From(o["u"], d.U), CurtainGrid.From(o["v"], d.V), Sec(o["mullion"], d.Mullion), Sec(o["transom"], d.Transom),
            o["edge"] is JsonValue e && e.TryGetValue<bool>(out var edge) ? edge : d.Edge,
            Json.Number(panel?["thickness"]) ?? d.PanelThickness, (string?)panel?["kind"] is "glass" or "opaque" ? (string)panel!["kind"]! : d.PanelKind,
            Colour(o["frameColor"], d.FrameColor), Colour(o["glassColor"], d.GlassColor), Colour(o["opaqueColor"], d.OpaqueColor),
            Named(o["frameMaterial"]), Named(o["glassMaterial"]), Named(o["opaqueMaterial"]));
    }

    public JsonObject ToJson() => WithMaterials(new()
    {
        ["u"] = U.ToJson(), ["v"] = V.ToJson(),
        ["mullion"] = SectionJson(Mullion),
        ["transom"] = SectionJson(Transom),
        ["edge"] = Edge,
        ["panel"] = new JsonObject { ["thickness"] = PanelThickness, ["kind"] = PanelKind },
        ["frameColor"] = FrameColor, ["glassColor"] = GlassColor, ["opaqueColor"] = OpaqueColor,
    });

    static JsonObject SectionJson(CurtainSection s)
    {
        var o = new JsonObject { ["width"] = s.Width, ["depth"] = s.Depth };
        if (s.Profile != null) o["profile"] = s.Profile.DeepClone();
        if (s.Place is { IsDefault: false } place) o["place"] = place.ToJson();
        return o;
    }

    /// <summary>The materials' names, those it has (a type made before report #76 is written as it was).</summary>
    internal JsonObject WithMaterials(JsonObject o)
    {
        if (FrameMaterial != null) o["frameMaterial"] = FrameMaterial;
        if (GlassMaterial != null) o["glassMaterial"] = GlassMaterial;
        if (OpaqueMaterial != null) o["opaqueMaterial"] = OpaqueMaterial;
        return o;
    }

    /// <summary>Why the spec cannot be built (null: it can).</summary>
    public string? Problem()
    {
        if (!(Mullion.Width > 0 && Mullion.Depth > 0 && Transom.Width > 0 && Transom.Depth > 0)) return "Mullions and transoms need a width and a depth.";
        if (!(PanelThickness > 0) || PanelThickness > Math.Max(Mullion.Depth, Transom.Depth)) return "A panel is thicker than the members that hold it.";
        if (U.Mode == "count" && U.Count is < 1 or > 200 || V.Mode == "count" && V.Count is < 1 or > 200) return "A grid divides into 1 to 200 bays.";
        if (U.Mode is "spacing" or "max" && U.Spacing < 0.1 || V.Mode is "spacing" or "max" && V.Spacing < 0.1) return "A grid's spacing is at least 100 mm.";
        return null;
    }
}

/// <summary>A curtain wall laid out: its grid lines along (U) and up (V), and every member and panel, in the wall's own terms.</summary>
public sealed record CurtainLayout(IReadOnlyList<double> U, IReadOnlyList<double> V, IReadOnlyList<CurtainPiece> Pieces);

/// <summary>
/// One member or panel: its role (mullion, transom, panel), its key, the
/// stretch along the wall it covers (from line <c>I0</c> to <c>I1</c>,
/// inset by <c>A0</c> and <c>A1</c>; a mullion sits on line I0 between A0 and
/// A1 around it), the heights it spans, and its depth across the wall. Under a
/// profile (<see cref="CurtainWalls.ProfileOf"/>) a panel or a member that
/// follows the boundary has its <c>Outline</c>: the polygon in the wall's own
/// plane, (along the wall, up), which the rectangle only boxes.
/// </summary>
public readonly record struct CurtainPiece(string Role, string Key, int I0, int I1, double A0, double A1, double Z0, double Z1, double Depth, IReadOnlyList<Vec2>? Outline = null);

/// <summary>
/// Curtain walls (the old Lapin's configurator): an IfcCurtainWall drawn
/// along an axis like a wall (straight or an arc), as high as it is given,
/// divided by its IfcCurtainWallType's grid into bays: mullions up the grid
/// lines along it (and its edges), transoms across between them, a panel of
/// glass or opaque infill in each bay. On an arc the bays are straight
/// between their mullions, as built. One part per member and panel, written
/// to IFC as the IfcMember and IfcPlate parts it aggregates. Drawn inside a
/// wall, it cuts its way through it.
/// </summary>
public static class CurtainWalls
{
    public const string Class = "IfcCurtainWall";
    public const string TypeClass = "IfcCurtainWallType";
    const double PlanCut = 1.2;

    public static void Register(IModuleRegistry r)
    {
        r.Class(new(Class, "Curtain wall",
        [
            new("height", "Height", "length", Min: 0.1),
            new("baseOffset", "Base offset", "length"),
            new(PathCurve.Key, "Arc rise", "length"),
        ], "#9cc3e6"));
        r.Class(new(TypeClass, "Curtain wall type", [], "#9cc3e6", Kind: "type"));
        r.Geometry(Class, (e, m, _) => Geometry(e, m));
        r.Quantities(Class, (e, g, m) => Quantities(e, g, m));
        // Over a wall (50): a curtain wall drawn in one cuts its way through it.
        r.Cutter(Class, 52, Envelope);
        r.Catalog("curtainWallDefault", CurtainSpec.Default.ToJson());

        r.Query("curtainWallPreview", (q, a) =>
        {
            var spec = CurtainSpec.From(a["spec"]);
            var (length, height) = (Json.Number(a["length"]) ?? 6, Json.Number(a["height"]) ?? 3);
            if (spec.Problem() is { } problem) return new JsonObject { ["problem"] = problem };
            var layout = Layout(spec, length, height);
            var problemOf = Fit(spec, layout, length);
            return new JsonObject
            {
                ["problem"] = problemOf,
                ["u"] = new JsonArray(layout.U.Select(x => (JsonNode)x).ToArray()),
                ["v"] = new JsonArray(layout.V.Select(x => (JsonNode)x).ToArray()),
                ["pieces"] = new JsonArray(layout.Pieces.Select(p =>
                {
                    var (x0, x1) = p.Role == "mullion" ? (layout.U[p.I0] - p.A0, layout.U[p.I0] + p.A1) : (layout.U[p.I0] + p.A0, layout.U[p.I1] - p.A1);
                    return (JsonNode)new JsonObject { ["role"] = p.Role, ["x0"] = x0, ["x1"] = x1, ["z0"] = p.Z0, ["z1"] = p.Z1 };
                }).ToArray()),
            };
        });

        r.Command("createCurtainWallType", "New curtain wall type", (ctx, a) =>
        {
            var spec = Checked(a["spec"]);
            var name = ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : $"Curtain wall {Numbering.Next(ctx.Model.OfClass(TypeClass))}";
            var type = new Entity(ctx.NewId(), TypeClass, name, new JsonObject { ["spec"] = spec.ToJson() }, "NOTDEFINED");
            ctx.Add(type);
            return new JsonObject { ["id"] = type.Id };
        });

        r.Command("updateCurtainWallType", "Change curtain wall type", (ctx, a) =>
        {
            var type = Spatial.Require(ctx.Model, (string?)a["id"], TypeClass);
            var attrs = type.CloneAttrs();
            if (a["spec"] != null)
            {
                var spec = Checked(a["spec"]);
                foreach (var w in ctx.Model.RelatedOf(type.Id, IfcClass.RelDefinesByType).Where(x => x.Class == Class)) CheckFits(spec, w);
                attrs["spec"] = spec.ToJson();
            }
            ctx.Replace(type.With(name: ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : null, attrs: attrs));
            return null;
        });

        r.Command("addCurtainWall", "Curtain wall", (ctx, a) =>
        {
            var storeyId = (string?)a["storeyId"] ?? Spatial.Storeys(ctx.Model).FirstOrDefault()?.Id ?? throw new CommandException("The project has no storey.");
            var (s, e) = (Args.Point(a, "x1", "y1"), Args.Point(a, "x2", "y2"));
            if ((e - s).Length < 0.1) throw new CommandException("A curtain wall needs two points at least 100 mm apart.");
            var type = (string?)a["typeId"] is { } tid ? Spatial.Require(ctx.Model, tid, TypeClass) : EnsureType(ctx);
            var height = Json.Number(a["height"]) ?? Walls.DefaultHeight;
            if (!(height >= 0.1)) throw new CommandException("A curtain wall is at least 100 mm high.");
            var attrs = new JsonObject { ["start"] = new JsonArray(s.X, s.Y), ["end"] = new JsonArray(e.X, e.Y), ["height"] = height, ["baseOffset"] = Json.Number(a["baseOffset"]) ?? 0 };
            if (Args.Rise(a, s, e) is var rise && rise != 0) attrs[PathCurve.Key] = rise;
            var w = new Entity(ctx.NewId(), Class, type.Name, attrs, "NOTDEFINED");
            CheckFits(SpecOf(ctx.Model, type), w);
            ctx.Add(w);
            Spatial.Contain(ctx, storeyId, w.Id);
            ctx.Relate(IfcClass.RelDefinesByType, type.Id, w.Id);
            return new JsonObject { ["id"] = w.Id };
        });

        r.Command("updateCurtainWall", "Change curtain wall", (ctx, a) =>
        {
            var w = Spatial.Require(ctx.Model, (string?)a["id"], Class);
            var (s, e) = (Joins.Start(w), Joins.End(w));
            s = new Vec2(Json.Number(a["x1"]) ?? s.X, Json.Number(a["y1"]) ?? s.Y);
            e = new Vec2(Json.Number(a["x2"]) ?? e.X, Json.Number(a["y2"]) ?? e.Y);
            if ((e - s).Length < 0.1) throw new CommandException("A curtain wall needs two points at least 100 mm apart.");
            var attrs = w.CloneAttrs();
            attrs["start"] = new JsonArray(s.X, s.Y);
            attrs["end"] = new JsonArray(e.X, e.Y);
            if (Json.Number(a["height"]) is { } h) attrs["height"] = h >= 0.1 ? h : throw new CommandException("A curtain wall is at least 100 mm high.");
            if (Json.Number(a["baseOffset"]) is { } b) attrs["baseOffset"] = b;
            var rise = a.ContainsKey(PathCurve.Key) || a["x3"] != null ? Args.Rise(a, s, e) : w.NumOrNull(PathCurve.Key) ?? 0;
            if (rise != 0) attrs[PathCurve.Key] = rise; else attrs.Remove(PathCurve.Key);
            var next = w.With(attrs: attrs);
            CheckFits(SpecOf(ctx.Model, next), next);
            ctx.Replace(next);
            return null;
        });

        // A curtain wall's boundary drawn in elevation (report #127, as a wall's profile, decision 372): top = [[u, v], …] along it from
        // its start and up from its base (a step is two points at one u); null or none: flat again. The wall's height becomes its
        // highest point, so its grid divides what is there; the grid, the transoms, the panels and the boundary members follow.
        r.Command("setCurtainWallProfile", "Curtain wall profile", (ctx, a) =>
        {
            var ids = Args.Ids(a).ToList();
            if (ids.Count == 0) throw new CommandException("Which curtain wall?");
            foreach (var id in ids)
            {
                var w = Spatial.Require(ctx.Model, id, Class);
                var attrs = w.CloneAttrs();
                if (a["top"] is not JsonArray top || top.Count == 0) { attrs.Remove(WallTops.TopProfileKey); ctx.Replace(w.With(attrs: attrs)); continue; }
                var length = PathCurve.Of(w).Length;
                var pts = new List<(double U, double V)>();
                foreach (var q in top)
                {
                    if (q is not JsonArray { Count: >= 2 } pa || Json.Number(pa[0]) is not { } u || Json.Number(pa[1]) is not { } v) throw new CommandException("A profile is a list of [u, v] points.");
                    pts.Add((Math.Clamp(u, 0, length), v));
                }
                if (pts.Count < 2) throw new CommandException("A profile needs two points at least.");
                for (var i = 1; i < pts.Count; i++) if (pts[i].U < pts[i - 1].U - 1e-9) throw new CommandException("A profile runs from the wall's start to its end: its points in order along it.");
                if (pts.Any(q => q.V < 0.05)) throw new CommandException("A profile stays 50 mm over the wall's base at least.");
                if (pts.Any(q => q.V > 100)) throw new CommandException("A profile stays under 100 m.");
                attrs[WallTops.TopProfileKey] = WallTops.ProfileJson(pts);
                attrs["height"] = Math.Round(pts.Max(q => q.V), 6);
                var next = w.With(attrs: attrs);
                CheckFits(SpecOf(ctx.Model, next), next);
                ctx.Replace(next);
            }
            return null;
        });

        // The members of a curtain wall as its elevation draws them (report #128): each piece with its key, its place in the wall and what
        // its profile is now (its own style on the wall, else its type's), for the dialog that assigns profiles to chosen members.
        r.Query("curtainMembers", (q, a) =>
        {
            var w = Spatial.Require(q.Model, (string?)a["id"], Class);
            var spec = SpecOf(q.Model, w);
            var c = PathCurve.Of(w);
            var profile = ProfileOf(w);
            var height = Math.Max(w.Num("height", Walls.DefaultHeight), profile?.Max(p => p.V) ?? 0);
            var layout = Layout(spec, c.Length, height, profile);
            var own = w.Attrs[MemberStylesKey] as JsonObject;
            return new JsonObject
            {
                ["length"] = c.Length, ["height"] = height,
                ["u"] = new JsonArray(layout.U.Select(x => (JsonNode)x).ToArray()),
                ["pieces"] = new JsonArray(layout.Pieces.Where(p => p.Role != "panel").Select(p =>
                {
                    var (x0, x1) = p.Role == "mullion" ? (layout.U[p.I0] - p.A0, layout.U[p.I0] + p.A1) : (layout.U[p.I0] + p.A0, layout.U[p.I1] - p.A1);
                    var (section, place) = StyleOf(spec, w, p);
                    var piece = new JsonObject
                    {
                        ["key"] = p.Key, ["role"] = p.Role, ["x0"] = x0, ["x1"] = x1, ["z0"] = p.Z0, ["z1"] = p.Z1,
                        ["profile"] = (string?)section.Profile?["name"], ["place"] = place?.ToJson() ?? new JsonObject(),
                        ["own"] = own != null && (own[p.Key] != null || own[p.Role] != null),
                    };
                    if (p.Outline != null) piece["outline"] = new JsonArray(p.Outline.Select(o => (JsonNode)new JsonArray(o.X, o.Y)).ToArray());
                    return (JsonNode)piece;
                }).ToArray()),
                ["styles"] = own?.DeepClone() ?? new JsonObject(),
            };
        });

        // {id, keys, profile? (a section's JSON), plain?, place?, clear?}: the members chosen — by piece key (mullion:3) or by role
        // (mullion, transom: all of them) — take a profile of their own, a place (turn, mirror, align, offset), or go back to their type's.
        r.Command("setCurtainMembers", "Curtain wall members", (ctx, a) =>
        {
            var w = Spatial.Require(ctx.Model, (string?)a["id"], Class);
            var keys = (a["keys"] as JsonArray)?.Select(x => (string?)x).OfType<string>().Distinct().ToList() ?? [];
            if (keys.Count == 0) throw new CommandException("Which members?");
            var spec = SpecOf(ctx.Model, w);
            var c = PathCurve.Of(w);
            var profile = ProfileOf(w);
            var layout = Layout(spec, c.Length, Math.Max(w.Num("height", Walls.DefaultHeight), profile?.Max(p => p.V) ?? 0), profile);
            var known = layout.Pieces.Where(p => p.Role != "panel").Select(p => p.Key).Concat(["mullion", "transom"]).ToHashSet();
            if (keys.FirstOrDefault(k => !known.Contains(k)) is { } bad) throw new CommandException($"The curtain wall has no member „{bad}”.");
            var attrs = w.CloneAttrs();
            var styles = attrs[MemberStylesKey] as JsonObject ?? new JsonObject();
            var clear = a["clear"] is JsonValue cv && cv.TryGetValue<bool>(out var cleared) && cleared;
            foreach (var key in keys)
            {
                if (clear) { styles.Remove(key); continue; }
                var entry = styles[key] as JsonObject ?? new JsonObject();
                styles.Remove(key);
                if (a["profile"] is JsonObject prof)
                {
                    if (Json.Number(prof["width"]) is not > 0 || Json.Number(prof["depth"]) is not > 0) throw new CommandException("A profile needs a width and a depth.");
                    entry["profile"] = prof.DeepClone();
                    entry.Remove("plain");
                }
                else if (a["plain"] is JsonValue pv && pv.TryGetValue<bool>(out var plain) && plain) { entry.Remove("profile"); entry["plain"] = true; }
                if (a["place"] is JsonObject place)
                {
                    var mp = MemberPlace.From(place);
                    if (mp.IsDefault) entry.Remove("place"); else entry["place"] = mp.ToJson();
                }
                if (entry.Count > 0) styles[key] = entry;
            }
            if (styles.Count > 0) attrs[MemberStylesKey] = styles; else attrs.Remove(MemberStylesKey);
            ctx.Replace(w.With(attrs: attrs));
            return new JsonObject { ["members"] = keys.Count };
        });

        r.Command("setCurtainWallType", "Change type", (ctx, a) =>
        {
            var type = Spatial.Require(ctx.Model, (string?)a["typeId"], TypeClass);
            foreach (var id in Args.Ids(a))
            {
                var w = Spatial.Require(ctx.Model, id, Class);
                CheckFits(SpecOf(ctx.Model, type), w);
                // Named after its type, it takes the new type's name.
                var named = w.Name == ctx.Model.RelatingOf(w.Id, IfcClass.RelDefinesByType)?.Name;
                ctx.Relate(IfcClass.RelDefinesByType, type.Id, w.Id);
                if (named) ctx.Replace(w.With(name: type.Name));
            }
            return null;
        });
    }

    static CurtainSpec Checked(JsonNode? n)
    {
        var spec = CurtainSpec.From(n);
        if (spec.Problem() is { } p) throw new CommandException(p);
        return spec;
    }

    /// <summary>The project's first curtain wall type, made (the default) when it has none.</summary>
    public static Entity EnsureType(IEdit edit)
    {
        if (edit.Model.OfClass(TypeClass).FirstOrDefault() is { } t) return t;
        // Its frame and glass as materials of their own (report #76), the glass the doors' and windows'.
        var d = CurtainSpec.Default;
        var spec = d with
        {
            FrameMaterial = Openings.EnsureMaterial(edit, "Curtain wall frame", d.FrameColor, new JsonObject { ["roughness"] = 0.3, ["metalness"] = 1 }),
            GlassMaterial = Openings.EnsureMaterial(edit, "Glass", d.GlassColor, Openings.GlassLook()),
        };
        var type = new Entity(edit.NewId(), TypeClass, "Curtain wall 1500", new JsonObject { ["spec"] = spec.ToJson() }, "NOTDEFINED");
        edit.Add(type);
        return type;
    }

    public static CurtainSpec SpecOf(IModelView m, Entity typeOrWall) =>
        CurtainSpec.From((typeOrWall.Class == TypeClass ? typeOrWall : m.RelatingOf(typeOrWall.Id, IfcClass.RelDefinesByType))?.Attrs["spec"]);

    static void CheckFits(CurtainSpec spec, Entity w)
    {
        var c = PathCurve.Of(w);
        var layout = Layout(spec, c.Length, w.Num("height", Walls.DefaultHeight), ProfileOf(w));
        if (Fit(spec, layout, c.Length) is { } problem) throw new CommandException(problem);
    }

    /// <summary>Why the layout does not fit its wall: a bay narrower than its members (null: it fits).</summary>
    static string? Fit(CurtainSpec spec, CurtainLayout l, double length)
    {
        for (var i = 0; i + 1 < l.U.Count; i++)
            if (l.U[i + 1] - l.U[i] < spec.Mullion.Width * 1.01) return "The grid along leaves bays narrower than the mullions.";
        for (var j = 0; j + 1 < l.V.Count; j++)
            if (l.V[j + 1] - l.V[j] < spec.Transom.Width * 1.01) return "The grid up leaves bays lower than the transoms.";
        return length < 0.1 ? "The curtain wall is too short." : null;
    }

    /// <summary>
    /// The layout for a wall <paramref name="length"/> along and
    /// <paramref name="height"/> high: grid lines with 0 and the extent at the
    /// ends; mullions up every line (the edge ones inside the wall), transoms
    /// across each bay at every level (the edge ones inside it), a panel in
    /// each bay between them.
    /// </summary>
    public static CurtainLayout Layout(CurtainSpec spec, double length, double height) => Layout(spec, length, height, null);

    /// <summary>
    /// The layout under a boundary (report #127, <see cref="ProfileOf"/>): the grid along gains a line where the profile bends
    /// (one near a line of the grid is moved onto it), a bay carries the transoms that fit under its lowest top, the boundary member
    /// follows the top (a polygon, <see cref="CurtainPiece.Outline"/>), the panel above the last transom fills up to it, a mullion
    /// rises to the top where it stands (the taller side at a step). Without a profile it is the grid's rectangle as it was.
    /// </summary>
    public static CurtainLayout Layout(CurtainSpec spec, double length, double height, IReadOnlyList<(double U, double V)>? top)
    {
        if (top is { Count: >= 2 }) return Profiled(spec, length, Math.Max(height, top.Max(p => p.V)), top);
        return Plain(spec, length, height);
    }

    static CurtainLayout Plain(CurtainSpec spec, double length, double height)
    {
        var u = spec.U.Lines(length).Prepend(0).Append(length).ToList();
        var v = spec.V.Lines(height).Prepend(0).Append(height).ToList();
        var (mw, tw) = (spec.Mullion.Width, spec.Transom.Width);
        var pieces = new List<CurtainPiece>();
        // Half a member's reach on each side of a line (an edge member lies wholly inside).
        double Left(int i) => i == 0 ? 0 : i == u.Count - 1 ? (spec.Edge ? mw : 0) : mw / 2;
        double Right(int i) => i == u.Count - 1 ? 0 : i == 0 ? (spec.Edge ? mw : 0) : mw / 2;
        double Below(int j) => j == 0 ? 0 : j == v.Count - 1 ? (spec.Edge ? tw : 0) : tw / 2;
        double Above(int j) => j == v.Count - 1 ? 0 : j == 0 ? (spec.Edge ? tw : 0) : tw / 2;
        for (var i = 0; i < u.Count; i++)
        {
            if ((i == 0 || i == u.Count - 1) && !spec.Edge) continue;
            pieces.Add(new CurtainPiece("mullion", $"mullion:{i}", i, i, Left(i), Right(i), 0, height, spec.Mullion.Depth));
        }
        for (var i = 0; i + 1 < u.Count; i++)
        {
            for (var j = 0; j < v.Count; j++)
            {
                if ((j == 0 || j == v.Count - 1) && !spec.Edge) continue;
                pieces.Add(new CurtainPiece("transom", $"transom:{i}:{j}", i, i + 1, Right(i), Left(i + 1), v[j] - Below(j), v[j] + Above(j), spec.Transom.Depth));
            }
            for (var j = 0; j + 1 < v.Count; j++)
                pieces.Add(new CurtainPiece("panel", $"panel:{i}:{j}", i, i + 1, Right(i), Left(i + 1), v[j] + Above(j), v[j + 1] - Below(j + 1), spec.PanelThickness));
        }
        return new CurtainLayout(u, v, pieces);
    }

    /// <summary>
    /// A curtain wall's boundary (report #127): <c>topProfile</c>, [[u, v], …] along the wall from its start and up from its base,
    /// as a wall's own top is kept (a step is two points at one u); flat past its ends. Null without one, or with fewer than two points.
    /// </summary>
    public static List<(double U, double V)>? ProfileOf(Entity w)
    {
        if (w.Attrs[WallTops.TopProfileKey] is not JsonArray pts || pts.Count < 2) return null;
        var length = PathCurve.Of(w).Length;
        var o = new List<(double U, double V)>();
        foreach (var q in pts)
            if (q is JsonArray { Count: >= 2 } a && Json.Number(a[0]) is { } u && Json.Number(a[1]) is { } v && double.IsFinite(u) && double.IsFinite(v)) o.Add((Math.Clamp(u, 0, length), Math.Max(v, 0.05)));
        if (o.Count < 2) return null;
        o = o.OrderBy(p => p.U).ToList();
        if (o[0].U > 1e-9) o.Insert(0, (0, o[0].V));
        if (o[^1].U < length - 1e-9) o.Add((length, o[^1].V));
        return o;
    }

    /// <summary>The boundary's height at <paramref name="u"/>; at a step, the side asked for (−1 the left's, 1 the right's).</summary>
    public static double TopAt(IReadOnlyList<(double U, double V)> p, double u, int side)
    {
        for (var i = 0; i + 1 < p.Count; i++)
        {
            var (a, b) = (p[i], p[i + 1]);
            if (b.U - a.U < 1e-9) continue;
            var inside = side < 0 ? a.U < u - 1e-9 && u <= b.U + 1e-9 : a.U <= u + 1e-9 && u < b.U - 1e-9;
            if (inside) return a.V + (b.V - a.V) * (u - a.U) / (b.U - a.U);
        }
        return side < 0 ? p[0].V : p[^1].V;
    }

    static CurtainLayout Profiled(CurtainSpec spec, double length, double height, IReadOnlyList<(double U, double V)> profile)
    {
        var (mw, tw) = (spec.Mullion.Width, spec.Transom.Width);
        var lines = spec.U.Lines(length);
        // The grid gains a line where the profile bends; a bend within a mullion and a half of a line is moved onto it.
        var snapped = profile.Select(p => p).ToList();
        for (var i = 0; i < snapped.Count; i++)
        {
            var q = snapped[i];
            if (q.U <= 1e-6 || q.U >= length - 1e-6) continue;
            var near = lines.Count == 0 ? double.NaN : lines.MinBy(x => Math.Abs(x - q.U));
            if (!double.IsNaN(near) && Math.Abs(near - q.U) <= 1.5 * mw) snapped[i] = (near, q.V);
            else lines.Add(q.U);
        }
        lines = lines.Where(x => x > 1e-6 && x < length - 1e-6).Order().ToList();
        var dedup = new List<double>();
        foreach (var x in lines) if (dedup.Count == 0 || x - dedup[^1] > 1e-6) dedup.Add(x);
        var u = dedup.Prepend(0).Append(length).ToList();
        var v = spec.V.Lines(height).Prepend(0).Append(height).ToList();
        var edge = spec.Edge;
        double Left(int i) => i == 0 ? 0 : i == u.Count - 1 ? (edge ? mw : 0) : mw / 2;
        double Right(int i) => i == u.Count - 1 ? 0 : i == 0 ? (edge ? mw : 0) : mw / 2;
        double Below(int j) => j == 0 ? 0 : j == v.Count - 1 ? (edge ? tw : 0) : tw / 2;
        double Above(int j) => j == v.Count - 1 ? 0 : j == 0 ? (edge ? tw : 0) : tw / 2;
        var pieces = new List<CurtainPiece>();
        var off = edge ? tw : 0;
        for (var i = 0; i < u.Count; i++)
        {
            if ((i == 0 || i == u.Count - 1) && !edge) continue;
            // Cut to the boundary where it stands, level at a step (the taller side's), its sides as high as the boundary is there.
            var (a0, a1) = (Left(i), Right(i));
            var (stepL, stepR) = (i == 0 ? TopAt(snapped, u[i], 1) : TopAt(snapped, u[i], -1), i == u.Count - 1 ? TopAt(snapped, u[i], -1) : TopAt(snapped, u[i], 1));
            var step = Math.Abs(stepL - stepR) > 1e-6;
            var zl = step ? Math.Max(stepL, stepR) : TopAt(snapped, Math.Max(u[i] - a0, 0), 1);
            var zr = step ? Math.Max(stepL, stepR) : TopAt(snapped, Math.Min(u[i] + a1, length), -1);
            var outline = new List<Vec2> { new(u[i] - a0, 0), new(u[i] + a1, 0), new(u[i] + a1, Math.Min(zr, height)) };
            // Where the boundary bends over the mullion (an apex, a valley) its top follows.
            if (!step && i > 0 && i < u.Count - 1 && a0 > 0 && a1 > 0 && TopAt(snapped, u[i], 1) is var bend && Math.Abs(bend - (zl + zr) / 2) > 1e-6) outline.Add(new Vec2(u[i], Math.Min(bend, height)));
            outline.Add(new Vec2(u[i] - a0, Math.Min(zl, height)));
            pieces.Add(new CurtainPiece("mullion", $"mullion:{i}", i, i, a0, a1, 0, Math.Min(Math.Max(Math.Max(zl, zr), outline.Max(q => q.Y)), height), spec.Mullion.Depth, outline));
        }
        for (var i = 0; i + 1 < u.Count; i++)
        {
            var (t0, t1) = (TopAt(snapped, u[i], 1), TopAt(snapped, u[i + 1], -1));
            var low = Math.Min(t0, t1);
            var (ua, ub) = (u[i] + Right(i), u[i + 1] - Left(i + 1));
            double TopIn(double x) => t0 + (t1 - t0) * (x - u[i]) / (u[i + 1] - u[i]);
            // The levels that fit under the lowest top of the bay (its first, the bottom, as ever); the rest are above its boundary.
            var kept = new List<int> { 0 };
            for (var j = 1; j + 1 < v.Count; j++) if (v[j] + Above(j) <= low - off - 0.05) kept.Add(j);
            foreach (var j in kept)
            {
                if (j == 0 && !edge) continue;
                pieces.Add(new CurtainPiece("transom", $"transom:{i}:{j}", i, i + 1, Right(i), Left(i + 1), v[j] - Below(j), v[j] + Above(j), spec.Transom.Depth));
            }
            for (var k = 0; k + 1 < kept.Count; k++)
            {
                var (j, n) = (kept[k], kept[k + 1]);
                pieces.Add(new CurtainPiece("panel", $"panel:{i}:{j}", i, i + 1, Right(i), Left(i + 1), v[j] + Above(j), v[n] - Below(n), spec.PanelThickness));
            }
            // Above the last transom: the panel up to the boundary, and the member that follows it.
            var za = v[kept[^1]] + Above(kept[^1]);
            var (ya, yb) = (TopIn(ua) - off, TopIn(ub) - off);
            if (Clip(ua, ub, za, ya, yb) is { } panel)
                pieces.Add(new CurtainPiece("panel", $"panel:{i}:top", i, i + 1, Right(i), Left(i + 1), panel.Min(p => p.Y), panel.Max(p => p.Y), spec.PanelThickness, panel));
            if (edge && ub - ua > 1e-6)
            {
                var band = new Vec2[] { new(ua, ya), new(ub, yb), new(ub, yb + off), new(ua, ya + off) };
                pieces.Add(new CurtainPiece("transom", $"transom:{i}:top", i, i + 1, Right(i), Left(i + 1), Math.Min(ya, yb), Math.Max(ya + off, yb + off), spec.Transom.Depth, band));
            }
        }
        return new CurtainLayout(u, v, pieces);
    }

    /// <summary>The part of the quadrilateral under the line from (<paramref name="ua"/>, <paramref name="ya"/>) to (<paramref name="ub"/>, <paramref name="yb"/>) and over <paramref name="z"/>; null when under 20 mm remains.</summary>
    static List<Vec2>? Clip(double ua, double ub, double z, double ya, double yb)
    {
        if (ub - ua < 1e-6 || Math.Max(ya, yb) - z < 0.02) return null;
        if (ya >= z && yb >= z) return [new(ua, z), new(ub, z), new(ub, yb), new(ua, ya)];
        // The line crosses the level: a triangle on the side that stands over it.
        var uc = ua + (ub - ua) * (z - ya) / (yb - ya);
        return ya < z ? [new(uc, z), new(ub, z), new(ub, yb)] : [new(ua, z), new(uc, z), new(ua, ya)];
    }

    /// <summary>A piece with an outline, as a solid: its polygon in the plane of its bay's chord, thick across the wall.</summary>
    static Solid? Outlined(PathCurve c, CurtainLayout l, CurtainPiece p, double baseZ)
    {
        if (p.Outline is not { Count: >= 3 } outline) return null;
        Vec2 pa, d;
        double chord, span;
        if (p.I0 == p.I1)
        {
            // A mullion: its polygon along the tangent where it stands.
            (pa, d) = (c.PointAt(l.U[p.I0]), c.TangentAt(l.U[p.I0]));
            (chord, span) = (1, 1);
        }
        else
        {
            var pb = c.PointAt(l.U[p.I1]);
            pa = c.PointAt(l.U[p.I0]);
            chord = (pb - pa).Length;
            span = l.U[p.I1] - l.U[p.I0];
            if (chord < 1e-6 || span < 1e-6) return null;
            d = (pb - pa).Normalized();
        }
        var n = d.Perp;
        Vec3 At(Vec2 q)
        {
            var x = (q.X - l.U[p.I0]) / span * chord;
            var o = pa + d * x - n * (p.Depth / 2);
            return new Vec3(o.X, o.Y, baseZ + q.Y);
        }
        var sweep = new Vec3(n.X, n.Y, 0) * p.Depth;
        return Prism.Extrude(outline.Select(At).ToList(), null, sweep, p.Role);
    }

    /// <summary>The polygon's area (shoelace), in the wall's own plane.</summary>
    static double Area(IReadOnlyList<Vec2> poly)
    {
        var s = 0.0;
        for (var i = 0; i < poly.Count; i++) { var (a, b) = (poly[i], poly[(i + 1) % poly.Count]); s += a.X * b.Y - b.X * a.Y; }
        return Math.Abs(s) / 2;
    }

    /// <summary>A piece's footprint in plan: a mullion square to the axis at its line, a transom or panel straight along its bay's chord.</summary>
    static Vec2[] Footprint(PathCurve c, CurtainLayout l, CurtainPiece p)
    {
        Vec2 a, d;
        double x0, x1;
        if (p.Role == "mullion")
        {
            var s = l.U[p.I0];
            (a, d) = (c.PointAt(s), c.TangentAt(s));
            (x0, x1) = (-p.A0, p.A1);
        }
        else
        {
            var (pa, pb) = (c.PointAt(l.U[p.I0]), c.PointAt(l.U[p.I1]));
            (a, d) = (pa, (pb - pa).Normalized());
            (x0, x1) = (p.A0, (pb - pa).Length - p.A1);
        }
        var n = d.Perp * (p.Depth / 2);
        return [a + d * x0 - n, a + d * x1 - n, a + d * x1 + n, a + d * x0 + n];
    }

    /// <summary>
    /// A mullion or transom with a library profile, as a solid: a mullion's section in plan, its x along the wall and its y out of
    /// the building (the wall's right), up its height; a transom's standing across its bay, its x up. Null for a panel or a plain member.
    /// </summary>
    static Solid? Member(CurtainSpec spec, Entity w, PathCurve c, CurtainLayout l, CurtainPiece p, double baseZ)
    {
        var (section, place) = StyleOf(spec, w, p);
        var outlines = p.Role is "mullion" or "transom" ? MemberOutline.Of(section.Profile) : [];
        // A plain box moved or turned by a place of its own is its rectangle, placed likewise.
        if (outlines.Count == 0 && place is { IsDefault: false } && p.Role is "mullion" or "transom")
            outlines = [new MemberOutline(
                [new(-section.Width / 2, -section.Depth / 2), new(section.Width / 2, -section.Depth / 2), new(section.Width / 2, section.Depth / 2), new(-section.Width / 2, section.Depth / 2)], [])];
        if (outlines.Count == 0) return null;
        if (place != null) outlines = place.Apply(outlines);
        var solids = new List<Solid>();
        foreach (var o in outlines)
        {
            Func<Vec2, Vec3> At;
            Vec3 sweep;
            if (p.Role == "mullion")
            {
                var s = l.U[p.I0];
                var (a, d) = (c.PointAt(s), c.TangentAt(s));
                var mid = (p.A1 - p.A0) / 2;
                At = q => (a + d * (mid + q.X) - d.Perp * q.Y).At(baseZ + p.Z0);
                sweep = new Vec3(0, 0, p.Z1 - p.Z0);
            }
            else
            {
                var (pa, pb) = (c.PointAt(l.U[p.I0]), c.PointAt(l.U[p.I1]));
                var d = (pb - pa).Normalized();
                var start = pa + d * p.A0;
                var zc = baseZ + (p.Z0 + p.Z1) / 2;
                At = q => new Vec3(start.X - d.Perp.X * q.Y, start.Y - d.Perp.Y * q.Y, zc + q.X);
                var run = (pb - pa).Length - p.A0 - p.A1;
                if (run <= 1e-6) return null;
                sweep = new Vec3(d.X, d.Y, 0) * run;
            }
            if (Prism.Extrude(o.Outer.Select(At).ToList(), o.Holes.Select(h => (IReadOnlyList<Vec3>)h.Select(At).ToList()).ToList(), sweep, p.Role) is { } solid) solids.Add(solid);
        }
        return solids.Count > 0 ? Openings.Combine(solids, p.Role) : null;
    }

    /// <summary>The key a curtain wall keeps the styles of its chosen members under (report #128): by piece (<c>mullion:3</c>), or by role (<c>mullion</c>, <c>transom</c>: every one of them).</summary>
    public const string MemberStylesKey = "memberStyles";

    /// <summary>
    /// A member's section and place: its own style on the wall (by its key, else by its role), over its type's. A style gives a library
    /// profile (<c>profile</c>, a section's JSON), <c>plain</c> (the type's profile left off) and a <c>place</c> replacing the type's.
    /// </summary>
    internal static (CurtainSection Section, MemberPlace? Place) StyleOf(CurtainSpec spec, Entity w, CurtainPiece p)
    {
        var type = p.Role == "mullion" ? spec.Mullion : p.Role == "transom" ? spec.Transom : default;
        var style = w.Attrs[MemberStylesKey] is JsonObject all ? (all[p.Key] ?? all[p.Role]) as JsonObject : null;
        if (style == null) return (type, type.Place);
        var section = type;
        if (style["plain"] is JsonValue pl && pl.TryGetValue<bool>(out var plain) && plain) section = new CurtainSection(type.Width, type.Depth);
        else if (style["profile"] is JsonObject prof) section = new CurtainSection(Json.Number(prof["width"]) ?? type.Width, Json.Number(prof["depth"]) ?? type.Depth, prof);
        var place = style["place"] is JsonObject po ? MemberPlace.From(po) : type.Place;
        return (section, place);
    }

    static double Base(IModelView m, Entity w) => (Spatial.StoreyOf(m, w.Id)?.Num("elevation") ?? 0) + w.Num("baseOffset");

    public static ElementGeometry Geometry(Entity w, IModelView m)
    {
        var c = PathCurve.Of(w);
        var height = w.Num("height", Walls.DefaultHeight);
        if (c.Chord < 0.1) return new ElementGeometry([], Diagnostics: ["The curtain wall has no length."]);
        var spec = SpecOf(m, w);
        var profile = ProfileOf(w);
        var layout = Layout(spec, c.Length, height, profile);
        var z = Base(m, w);
        var problems = Fit(spec, layout, c.Length) is { } p ? new List<string> { p } : null;
        var parts = new List<GeometryPart>();
        var plan = new List<PlanItem>();
        // The materials by name (report #76), each giving its colour; without one, the type's colours.
        string? Mat(string? name) => name != null ? Materials.Find(m, name)?.Id : null;
        var frameMat = Mat(spec.FrameMaterial);
        var infillMat = Mat(spec.PanelKind == "glass" ? spec.GlassMaterial : spec.OpaqueMaterial);
        var frameColor = frameMat != null ? Materials.ColorOf(m, frameMat) : spec.FrameColor;
        var infill = infillMat != null ? Materials.ColorOf(m, infillMat) : spec.PanelKind == "glass" ? spec.GlassColor : spec.OpaqueColor;
        foreach (var piece in layout.Pieces)
        {
            if (!(piece.Z1 - piece.Z0 > 1e-9)) continue;
            var fp = Footprint(c, layout, piece);
            // A library profile's real outline (decision 417), butted at the grid as a plain member is; a plain box without one.
            // A boundary member and a panel under a profile: their polygon; a library profile's mullion stays its own shape (cut flat).
            var member = piece.Outline == null || piece.Role == "mullion" ? Member(spec, w, c, layout, piece, z) : null;
            if ((member ?? Outlined(c, layout, piece, z) ?? Prism.ExtrudeFootprint(fp, z + piece.Z0, piece.Z1 - piece.Z0, piece.Role)) is not { } solid) continue;
            var name = piece.Role switch { "mullion" => "Mullion", "transom" => "Transom", _ => spec.PanelKind == "glass" ? "Glass panel" : "Panel" };
            parts.Add(new GeometryPart(name, piece.Role == "panel" ? infill : frameColor, solid, piece.Role == "panel" ? infillMat : frameMat, piece.Key));
            // In plan: the mullions cut, the panels at the cut height as thin bands.
            if (piece.Role == "mullion") plan.Add(new PlanItem("cut", true, fp));
            else if (piece.Role == "panel" && piece.Z0 <= PlanCut && piece.Z1 > PlanCut) plan.Add(new PlanItem("symbol", true, fp));
        }
        // The axis, where a click picks it.
        plan.Add(new PlanItem("hit", false, c.Line(0, c.Length, 0)));
        return new ElementGeometry(parts, plan, [c.Start, c.End, c.PointAt(c.Length / 2)], problems);
    }

    /// <summary>What it cuts a wall it is drawn in with: its band, as deep as a thick wall, up its height.</summary>
    public static Solid? Envelope(Entity w, IModelView m)
    {
        var c = PathCurve.Of(w);
        if (c.Chord < 0.1) return null;
        const double half = 0.35;
        return Prism.ExtrudeFootprint(c.Band(_ => 0, _ => c.Length, -half, half), Base(m, w), w.Num("height", Walls.DefaultHeight), "curtain");
    }

    public static IEnumerable<QuantitySet> Quantities(Entity w, ElementGeometry? g, IModelView m)
    {
        if (g == null || g.Parts.Count == 0) yield break;
        var c = PathCurve.Of(w);
        var height = w.Num("height", Walls.DefaultHeight);
        var spec = SpecOf(m, w);
        var profile = ProfileOf(w);
        var layout = Layout(spec, c.Length, height, profile);
        // Under a boundary: the height of its highest point, the side area under it.
        var gross = c.Length * height;
        if (profile != null)
        {
            height = Math.Max(height, profile.Max(p => p.V));
            gross = Enumerable.Range(0, profile.Count - 1).Sum(i => (profile[i + 1].U - profile[i].U) * (profile[i].V + profile[i + 1].V) / 2);
        }
        yield return new QuantitySet("Qto_CurtainWallQuantities",
        [
            new("Length", c.Length, "LENGTH"),
            new("Height", height, "LENGTH"),
            new("GrossSideArea", gross, "AREA", "Gross side area"),
        ]);
        var panels = layout.Pieces.Where(p => p.Role == "panel").ToList();
        double Chord(CurtainPiece p) => (c.PointAt(layout.U[p.I1]) - c.PointAt(layout.U[p.I0])).Length;
        double Along(CurtainPiece p) => Chord(p) - p.A0 - p.A1;
        // A piece that follows the boundary: its area from its outline (the chord over the arc), its length along the top.
        double AreaOf(CurtainPiece p) => p.Outline is { } o ? Area(o) * Chord(p) / Math.Max(layout.U[p.I1] - layout.U[p.I0], 1e-9) : Along(p) * (p.Z1 - p.Z0);
        double RunOf(CurtainPiece p) => p.Outline is { Count: 4 } o ? (new Vec2((o[1].X - o[0].X) * Chord(p) / Math.Max(layout.U[p.I1] - layout.U[p.I0], 1e-9), o[1].Y - o[0].Y)).Length : Along(p);
        yield return new QuantitySet("Lapin_CurtainWallParts",
        [
            new("Mullions", layout.Pieces.Count(p => p.Role == "mullion"), "COUNT"),
            new("MullionLength", layout.Pieces.Where(p => p.Role == "mullion").Sum(p => p.Z1 - p.Z0), "LENGTH", "Mullion length"),
            new("TransomLength", layout.Pieces.Where(p => p.Role == "transom").Sum(RunOf), "LENGTH", "Transom length"),
            new("Panels", panels.Count, "COUNT"),
            new(spec.PanelKind == "glass" ? "GlassArea" : "PanelArea", panels.Sum(AreaOf), "AREA", spec.PanelKind == "glass" ? "Glass area" : "Panel area"),
        ]);
    }
}
