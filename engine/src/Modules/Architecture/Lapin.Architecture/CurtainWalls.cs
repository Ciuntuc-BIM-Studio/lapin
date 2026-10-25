using System.Text.Json.Nodes;
using BubbleBim.Geometry;
using Lapin.Model;
using Lapin.Platform;

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

/// <summary>A member's section: in the wall's plane across the member (width), and across the wall (depth), metres.</summary>
public readonly record struct CurtainSection(double Width, double Depth);

/// <summary>What a curtain wall type holds: its grid, its members and panels, their colours.</summary>
public sealed record CurtainSpec(CurtainGrid U, CurtainGrid V, CurtainSection Mullion, CurtainSection Transom, bool Edge, double PanelThickness, string PanelKind,
    string FrameColor, string GlassColor, string OpaqueColor)
{
    public static readonly CurtainSpec Default = new(new CurtainGrid("max", Spacing: 1.5), new CurtainGrid("max", Spacing: 1.5),
        new CurtainSection(0.05, 0.15), new CurtainSection(0.05, 0.15), true, 0.024, "glass", "#3f3f46", "#9cc3e6", "#a1a1aa");

    public static CurtainSpec From(JsonNode? n)
    {
        var d = Default;
        if (n is not JsonObject o) return d;
        static CurtainSection Sec(JsonNode? x, CurtainSection f) => x is JsonObject s ? new(Json.Number(s["width"]) ?? f.Width, Json.Number(s["depth"]) ?? f.Depth) : f;
        static string Colour(JsonNode? x, string f) => (string?)x is { Length: 7 } c && c[0] == '#' ? c : f;
        var panel = o["panel"] as JsonObject;
        return new CurtainSpec(CurtainGrid.From(o["u"], d.U), CurtainGrid.From(o["v"], d.V), Sec(o["mullion"], d.Mullion), Sec(o["transom"], d.Transom),
            o["edge"] is JsonValue e && e.TryGetValue<bool>(out var edge) ? edge : d.Edge,
            Json.Number(panel?["thickness"]) ?? d.PanelThickness, (string?)panel?["kind"] is "glass" or "opaque" ? (string)panel!["kind"]! : d.PanelKind,
            Colour(o["frameColor"], d.FrameColor), Colour(o["glassColor"], d.GlassColor), Colour(o["opaqueColor"], d.OpaqueColor));
    }

    public JsonObject ToJson() => new()
    {
        ["u"] = U.ToJson(), ["v"] = V.ToJson(),
        ["mullion"] = new JsonObject { ["width"] = Mullion.Width, ["depth"] = Mullion.Depth },
        ["transom"] = new JsonObject { ["width"] = Transom.Width, ["depth"] = Transom.Depth },
        ["edge"] = Edge,
        ["panel"] = new JsonObject { ["thickness"] = PanelThickness, ["kind"] = PanelKind },
        ["frameColor"] = FrameColor, ["glassColor"] = GlassColor, ["opaqueColor"] = OpaqueColor,
    };

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
/// A1 around it), the heights it spans, and its depth across the wall.
/// </summary>
public readonly record struct CurtainPiece(string Role, string Key, int I0, int I1, double A0, double A1, double Z0, double Z1, double Depth);

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
            var name = ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : $"Curtain wall {ctx.Model.OfClass(TypeClass).Count() + 1}";
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
        var type = new Entity(edit.NewId(), TypeClass, "Curtain wall 1500", new JsonObject { ["spec"] = CurtainSpec.Default.ToJson() }, "NOTDEFINED");
        edit.Add(type);
        return type;
    }

    public static CurtainSpec SpecOf(IModelView m, Entity typeOrWall) =>
        CurtainSpec.From((typeOrWall.Class == TypeClass ? typeOrWall : m.RelatingOf(typeOrWall.Id, IfcClass.RelDefinesByType))?.Attrs["spec"]);

    static void CheckFits(CurtainSpec spec, Entity w)
    {
        var c = PathCurve.Of(w);
        var layout = Layout(spec, c.Length, w.Num("height", Walls.DefaultHeight));
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
    public static CurtainLayout Layout(CurtainSpec spec, double length, double height)
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

    static double Base(IModelView m, Entity w) => (Spatial.StoreyOf(m, w.Id)?.Num("elevation") ?? 0) + w.Num("baseOffset");

    public static ElementGeometry Geometry(Entity w, IModelView m)
    {
        var c = PathCurve.Of(w);
        var height = w.Num("height", Walls.DefaultHeight);
        if (c.Chord < 0.1) return new ElementGeometry([], Diagnostics: ["The curtain wall has no length."]);
        var spec = SpecOf(m, w);
        var layout = Layout(spec, c.Length, height);
        var z = Base(m, w);
        var problems = Fit(spec, layout, c.Length) is { } p ? new List<string> { p } : null;
        var parts = new List<GeometryPart>();
        var plan = new List<PlanItem>();
        var infill = spec.PanelKind == "glass" ? spec.GlassColor : spec.OpaqueColor;
        foreach (var piece in layout.Pieces)
        {
            if (!(piece.Z1 - piece.Z0 > 1e-9)) continue;
            var fp = Footprint(c, layout, piece);
            if (Prism.ExtrudeFootprint(fp, z + piece.Z0, piece.Z1 - piece.Z0, piece.Role) is not { } solid) continue;
            var name = piece.Role switch { "mullion" => "Mullion", "transom" => "Transom", _ => spec.PanelKind == "glass" ? "Glass panel" : "Panel" };
            parts.Add(new GeometryPart(name, piece.Role == "panel" ? infill : spec.FrameColor, solid, null, piece.Key));
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
        var layout = Layout(spec, c.Length, height);
        yield return new QuantitySet("Qto_CurtainWallQuantities",
        [
            new("Length", c.Length, "LENGTH"),
            new("Height", height, "LENGTH"),
            new("GrossSideArea", c.Length * height, "AREA", "Gross side area"),
        ]);
        var panels = layout.Pieces.Where(p => p.Role == "panel").ToList();
        double Along(CurtainPiece p) => (c.PointAt(layout.U[p.I1]) - c.PointAt(layout.U[p.I0])).Length - p.A0 - p.A1;
        yield return new QuantitySet("Lapin_CurtainWallParts",
        [
            new("Mullions", layout.Pieces.Count(p => p.Role == "mullion"), "COUNT"),
            new("MullionLength", layout.Pieces.Where(p => p.Role == "mullion").Sum(p => p.Z1 - p.Z0), "LENGTH", "Mullion length"),
            new("TransomLength", layout.Pieces.Where(p => p.Role == "transom").Sum(Along), "LENGTH", "Transom length"),
            new("Panels", panels.Count, "COUNT"),
            new(spec.PanelKind == "glass" ? "GlassArea" : "PanelArea", panels.Sum(p => Along(p) * (p.Z1 - p.Z0)), "AREA", spec.PanelKind == "glass" ? "Glass area" : "Panel area"),
        ]);
    }
}
