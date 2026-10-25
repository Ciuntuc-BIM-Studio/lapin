using System.Text.Json.Nodes;
using BubbleBim.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture.Roofs;

/// <summary>
/// A roof (adapted from BubbleBIM Pro's): an IfcRoof is its outline (the wall
/// line, storey-local, counter-clockwise) at the top of its storey, and its
/// rules. A shape (hip, gable, shed, mansard, flat) gives each edge its kind;
/// the roof's pitch and overhang apply to every eave; <c>edges</c> (one entry
/// per edge, optional) overrides an edge's kind, pitch or overhang. A style
/// from the catalogue fills them all in. Everything else (planes, ridges,
/// hips, valleys, gables, the timber structure, gutters, downpipes, snow
/// guards) is derived from it, never stored.
/// </summary>
public static class Roof
{
    /// <summary>The fixed sections of the structure (m): wall plate, ridge beam, hip and valley rafters, purlins.</summary>
    public static readonly (double W, double D) WallPlate = (0.14, 0.14), RidgeBeam = (0.14, 0.20), HipRafter = (0.10, 0.20), Purlin = (0.14, 0.16);
    /// <summary>Battens and counter-battens between the rafters and the covering.</summary>
    public const double Battens = 0.04;
    /// <summary>A roof without a structure: a solid deck under the covering.</summary>
    const double Deck = 0.16;
    public static readonly string[] RafterSections = ["8x16", "10x16", "10x18", "10x20", "12x22"];

    /// <summary>
    /// A roof read from the model: its outline, its rules, the height of its
    /// wall plates' underside (the storey's top + the eave offset). Its planes
    /// are the rafters' underside, which rests on the plates.
    /// </summary>
    public sealed record Resolved(Entity Roof, IReadOnlyList<Vec2> Outline, IReadOnlyList<EdgeRule> Rules, double PlateZ)
    {
        public bool Flat => Roof.Str("shape") == "flat";
        /// <summary>A mansard's upper tier: from where the steep eaves have come in by the inset, at the upper pitch.</summary>
        public Mansard? Mansard => Roof.Str("shape") != "mansard" ? null : new Mansard(
            Roof.NumOrNull("upperPitch") ?? 30,
            (Roof.NumOrNull("mansardInset") ?? 1.0) * Math.Tan((Roof.NumOrNull("pitch") ?? 30) * Math.PI / 180));
        /// <summary>Whether it has a timber structure (rafters on plates); otherwise a deck under the covering.</summary>
        public bool Framed => !Flat && Roof.Str("structure") != "none";
        public RoofCatalog.Covering Covering => RoofCatalog.CoveringOf(Roof.Str("covering"));
        public (double W, double D) Rafter
        {
            get
            {
                var m = System.Text.RegularExpressions.Regex.Match(Roof.Str("rafterSection") ?? "", @"^(\d+)x(\d+)$");
                return m.Success ? (int.Parse(m.Groups[1].Value) * 0.01, int.Parse(m.Groups[2].Value) * 0.01) : (0.08, 0.16);
            }
        }
        /// <summary>The planes' height over the wall line: on the plates when framed.</summary>
        public double BaseZ => PlateZ + (Framed ? WallPlate.D : 0);
        /// <summary>From the planes to the covering's underside: rafters and battens, or the deck.</summary>
        public double StructureDepth => Framed ? Rafter.D + Battens : Deck;
        public double Thickness => StructureDepth + Covering.Thickness;
    }

    public static IReadOnlyList<Vec2> Contour(Entity roof) => Slabs.Contour(roof);

    public static Resolved? Resolve(Entity roof, IModelView m)
    {
        var outline = Contour(roof);
        if (outline.Count < 3) return null;
        var storey = Spatial.StoreyOf(m, roof.Id);
        var plateZ = (storey?.Num("elevation") ?? 0) + (storey?.Num("height", 3) ?? 3) + roof.Num("eaveOffset");
        return new Resolved(roof, outline, Rules(roof, outline), plateZ);
    }

    /// <summary>The rule of every edge: the shape's kinds, the roof's pitch and overhang, each edge's overrides.</summary>
    public static List<EdgeRule> Rules(Entity roof, IReadOnlyList<Vec2> outline)
    {
        var n = outline.Count;
        var shape = roof.Str("shape") ?? "hip";
        var pitch = roof.NumOrNull("pitch") ?? 30;
        var overhang = roof.NumOrNull("overhang") ?? 0.5;
        var rake = roof.NumOrNull("gableOverhang") ?? 0.3;
        var kinds = Kinds(shape, outline, roof.Str("ridgeDirection") ?? "auto", (int)(roof.NumOrNull("lowEdge") ?? -1));
        var overrides = roof.Attrs["edges"] as JsonArray;
        return Enumerable.Range(0, n).Select(i =>
        {
            var o = overrides is { } arr && i < arr.Count ? arr[i] as JsonObject : null;
            var kind = (string?)o?["kind"] switch { "gable" => EdgeKind.Gable, "eave" => EdgeKind.Eave, _ => kinds[i] };
            var p = Json.Number(o?["pitch"]) ?? pitch;
            var oh = Json.Number(o?["overhang"]) ?? (kind == EdgeKind.Gable ? rake : overhang);
            return new EdgeRule(kind, p, oh);
        }).ToList();
    }

    /// <summary>
    /// The kind of every edge for a shape. Hip: all eaves. Shed: one eave (the
    /// low edge: chosen, else the longest), gables elsewhere. Gable: an edge is a
    /// gable when it is an END — both its corners convex and it is no longer than
    /// either neighbour (a rectangle's short sides, an L's arm ends); on a quad
    /// the ridge direction can pick the pair.
    /// </summary>
    public static EdgeKind[] Kinds(string shape, IReadOnlyList<Vec2> outline, string ridgeDirection = "auto", int lowEdge = -1)
    {
        var n = outline.Count;
        double Len(int i) => (outline[(i + 1) % n] - outline[i]).Length;
        var kinds = Enumerable.Repeat(EdgeKind.Eave, n).ToArray();
        switch (shape)
        {
            case "shed":
                var low = lowEdge >= 0 && lowEdge < n ? lowEdge : Enumerable.Range(0, n).MaxBy(Len);
                for (var i = 0; i < n; i++) if (i != low) kinds[i] = EdgeKind.Gable;
                break;
            case "gable":
                var ccw = Polygon2.SignedArea(outline) > 0;
                bool Convex(int v)
                {
                    var (a, b, c) = (outline[(v + n - 1) % n], outline[v], outline[(v + 1) % n]);
                    return Vec2.Cross(b - a, c - b) > 0 == ccw;
                }
                if (n == 4 && ridgeDirection is "x" or "y")
                {
                    for (var i = 0; i < n; i++)
                    {
                        var d = outline[(i + 1) % n] - outline[i];
                        var alongY = Math.Abs(d.Y) > Math.Abs(d.X);
                        if (alongY == (ridgeDirection == "x")) kinds[i] = EdgeKind.Gable;
                    }
                    break;
                }
                for (var i = 0; i < n; i++)
                    if (Convex(i) && Convex((i + 1) % n) && Len(i) <= Len((i + n - 1) % n) + 1e-9 && Len(i) <= Len((i + 1) % n) + 1e-9)
                        kinds[i] = EdgeKind.Gable;
                // A square: one pair, not all four.
                if (kinds.All(k => k == EdgeKind.Gable)) for (var i = 1; i < n; i += 2) kinds[i] = EdgeKind.Eave;
                break;
        }
        return kinds;
    }

    /// <summary>The solved roof in the world: planes, gables and lines lifted to the wall plate. Null with a problem when the rules do not build.</summary>
    public static (RoofShape? Shape, string? Problem) Solve(Resolved r)
    {
        if (r.Flat) return (null, null);
        try
        {
            var s = RoofSolver.Solve(r.Outline, r.Rules, r.Mansard);
            Vec3 Up(Vec3 p) => p with { Z = p.Z + r.BaseZ };
            return (new RoofShape(
                s.Planes.Select(p => p with { Points = p.Points.Select(Up).ToArray() }).ToList(),
                s.Gables.Select(x => (x.Edge, x.Points.Select(Up).ToArray())).ToList(),
                s.Lines.Select(l => l with { A = Up(l.A), B = Up(l.B) }).ToList(),
                s.Outline, s.Rules), null);
        }
        catch (ArgumentException e) { return (null, e.Message); }
    }

    /// <summary>Where the roof stops in plan: each eave at its overhang, each gable at its rake (counter-clockwise).</summary>
    public static Vec2[] EaveOutline(Resolved r)
    {
        var (pts, rules) = RoofSolver.Orient(r.Outline, r.Rules);
        return RoofSolver.OffsetEdges(pts, rules.Select(x => x.Overhang).ToArray());
    }

    /// <summary>Gutters, downpipes and snow guards share a colour: the gutters' own, else their material's.</summary>
    static string MetalColor(Entity roof) => roof.Str("gutterColor") is { Length: > 0 } c ? c : roof.Str("gutterMaterial") switch
    {
        "copper" => "#b87333", "zinc_titanium" => "#8c9399", "painted" => "#3c4247", _ => "#a9b0b6",
    };

    /// <summary>The ground the downpipes reach: the lowest storey's floor.</summary>
    static double Ground(IModelView m) => Spatial.Storeys(m).Select(s => s.Num("elevation")).DefaultIfEmpty(0).Min();

    public static ElementGeometry? Geometry(Entity roof, IModelView m)
    {
        var diags = new List<string>();
        var r = Resolve(roof, m);
        if (r == null) return new ElementGeometry([], Diagnostics: ["A roof needs at least 3 corners."]);
        var parts = new List<GeometryPart>();
        var plan = new List<PlanItem>();
        var eaves = EaveOutline(r);
        plan.Add(new PlanItem("overhead", true, eaves));
        if (r.Flat)
        {
            if (Prism.ExtrudeFootprint(eaves, r.BaseZ, r.Thickness, "roof") is { } slab) parts.Add(new GeometryPart("Covering", r.Covering.Color, slab, null, "covering"));
            return new ElementGeometry(parts, plan, r.Outline);
        }
        var (shape, problem) = Solve(r);
        if (shape == null) return new ElementGeometry([], plan, r.Outline, [problem ?? "The roof cannot be solved."]);
        if (r.Rules.Any(x => x.Kind == EdgeKind.Eave && x.Pitch < r.Covering.MinPitch))
            diags.Add($"{r.Covering.Id} needs a pitch of at least {r.Covering.MinPitch:0}°.");

        // Each plane as the covering's body, over the rafters and battens (or the deck), vertically, so planes of one pitch share their hips and ridges.
        var openings = RoofOpenings.Place(roof, shape, m, diags);
        var covering = new List<Solid>();
        for (var i = 0; i < shape.Planes.Count; i++)
        {
            var p = shape.Planes[i];
            var cos = Math.Cos(p.Pitch * Math.PI / 180);
            var (below, layer) = r.Framed ? (r.StructureDepth / cos, r.Covering.Thickness / cos) : (0, r.Thickness / cos);
            Vec3 Lift(Vec3 q) => q with { Z = q.Z + below };
            var holes = openings.Where(o => o.Host.PlaneIndex == i).Select(o => (IReadOnlyList<Vec3>)o.Hole.Select(Lift).ToArray()).ToList();
            if (Prism.Extrude(p.Points.Select(Lift).ToArray(), holes, new Vec3(0, 0, layer), "covering") is { } s) covering.Add(s);
        }
        if (covering.Count > 0) parts.Add(new GeometryPart("Covering", r.Covering.Color, Openings.Combine(covering, "covering"), null, "covering"));

        // Its roof windows and dormers, as parts of it.
        var (frames, glass, dormerWalls, dormerRoofs) = (new List<Solid>(), new List<Solid>(), new List<Solid>(), new List<Solid>());
        foreach (var o in openings)
        {
            if (o.Opening.Kind == RoofOpening.Window)
            {
                var (f, g, pi) = RoofOpenings.WindowGeometry(o, r.Framed ? r.StructureDepth + r.Covering.Thickness : r.Thickness);
                frames.AddRange(f); glass.AddRange(g); plan.Add(pi);
            }
            else
            {
                var (w, rf, g, pi) = RoofOpenings.DormerGeometry(o, r.Covering.Thickness);
                dormerWalls.AddRange(w); dormerRoofs.AddRange(rf); glass.AddRange(g); plan.Add(pi);
            }
        }
        if (frames.Count > 0) parts.Add(new GeometryPart("Roof windows", "#3c4247", Openings.Combine(frames, "roof-windows"), null, "roof-windows"));
        if (dormerWalls.Count > 0) parts.Add(new GeometryPart("Dormer walls", "#ece8df", Openings.Combine(dormerWalls, "dormer-walls"), null, "dormer-walls"));
        if (dormerRoofs.Count > 0) parts.Add(new GeometryPart("Dormer roofs", r.Covering.Color, Openings.Combine(dormerRoofs, "dormer-roofs"), null, "dormer-roofs"));
        if (glass.Count > 0) parts.Add(new GeometryPart("Glass", "#9cc7e6", Openings.Combine(glass, "glass"), null, "glass"));

        var timber = RoofParts.Framing(r, shape, openings).Select(mb => Prism.SweepBox(mb.A, mb.B, mb.Width, mb.Depth, mb.Role)).OfType<Solid>().ToList();
        if (timber.Count > 0) parts.Add(new GeometryPart("Timber structure", "#b98a5a", Openings.Combine(timber, "timber"), null, "timber"));

        var metal = MetalColor(roof);
        var drain = new List<Solid>();
        var gutters = RoofParts.Gutters(r, shape);
        var box = roof.Str("gutterProfile") == "box";
        foreach (var gt in gutters) if (RoofParts.Channel(gt, box, "gutter") is { } s) drain.Add(s);
        var ground = Ground(m);
        foreach (var (outlet, foot) in RoofParts.Downpipes(r, shape, gutters))
        {
            // From the gutter's outlet in to the wall at 45°, then straight down to the ground.
            var top = outlet with { Z = outlet.Z - 0.07 };
            var knee = foot.At(top.Z - (outlet.Xy - foot).Length);
            foreach (var (a, b) in new[] { (top, knee), (knee, foot.At(ground)) })
                if (b.Z < a.Z - 0.01 && RoofParts.Tube(a, b, 0.05, 10, "downpipe") is { } s) drain.Add(s);
            plan.Add(new PlanItem("symbol", true, Enumerable.Range(0, 12).Select(k => foot + new Vec2(Math.Cos(k * Math.PI / 6), Math.Sin(k * Math.PI / 6)) * 0.05).ToArray()));
        }
        if (drain.Count > 0) parts.Add(new GeometryPart("Gutters and downpipes", metal, Openings.Combine(drain, "drainage"), null, "drainage"));

        var guards = RoofParts.SnowGuards(r, shape, openings).Select(x => RoofParts.Tube(x.A, x.B, 0.015, 8, "snow")).OfType<Solid>().ToList();
        if (guards.Count > 0) parts.Add(new GeometryPart("Snow guards", metal, Openings.Combine(guards, "snow"), null, "snow"));

        foreach (var l in shape.Lines.Where(l => l.Kind is LineKind.Ridge or LineKind.Hip or LineKind.Valley or LineKind.Break))
            plan.Add(new PlanItem(l.Kind.ToString().ToLowerInvariant(), false, [l.A.Xy, l.B.Xy]));
        return new ElementGeometry(parts, plan, r.Outline, diags.Count > 0 ? diags : null);
    }

    public static IEnumerable<QuantitySet> Quantities(Entity roof, ElementGeometry? geometry, IModelView m)
    {
        var r = Resolve(roof, m);
        if (r == null) yield break;
        if (r.Flat)
        {
            yield return new QuantitySet("Qto_RoofBaseQuantities", [new("GrossArea", Math.Abs(Polygon2.SignedArea(EaveOutline(r))), "AREA", "Covering")]);
            yield break;
        }
        var (shape, _) = Solve(r);
        if (shape == null) yield break;
        // The true sloped area less its openings, and the lines that get their own flashing, caps and gutters.
        var holes = RoofOpenings.Place(roof, shape, m, []);
        var q = new List<Quantity>
        {
            new("GrossArea", shape.Planes.Sum(p => VecMath.NewellNormal(p.Points).Length), "AREA", $"Covering ({r.Covering.Id})"),
            new("NetArea", shape.Planes.Sum(p => VecMath.NewellNormal(p.Points).Length) - holes.Sum(h => VecMath.NewellNormal(h.Hole.ToList()).Length), "AREA", "Covering, net"),
            new("ProjectedArea", shape.Planes.Sum(p => Math.Abs(Polygon2.SignedArea(p.Points.Select(x => x.Xy).ToList()))), "AREA", "In plan"),
        };
        foreach (var (kind, name, label) in new[]
        {
            (LineKind.Ridge, "RidgeLength", "Ridge"), (LineKind.Hip, "HipLength", "Hips"), (LineKind.Valley, "ValleyLength", "Valleys"),
            (LineKind.Eave, "EaveLength", "Eaves"), (LineKind.Verge, "VergeLength", "Verges (gables)"), (LineKind.Break, "BreakLength", "Mansard break"),
        })
        {
            var len = shape.Lines.Where(l => l.Kind == kind).Sum(l => l.Length);
            if (len > 1e-6) q.Add(new(name, len, "LENGTH", label));
        }
        yield return new QuantitySet("Qto_RoofBaseQuantities", q);
        if (RoofOpenings.Quantities(holes).ToList() is { Count: > 0 } oq) yield return new QuantitySet("Lapin_RoofOpeningQuantities", oq, "openings", "Roof windows and dormers");

        var members = RoofParts.Framing(r, shape, holes);
        if (members.Count > 0)
        {
            var t = new List<Quantity>
            {
                new("TimberVolume", members.Sum(x => x.Volume), "VOLUME", "Sawn timber"),
                new("RafterCount", members.Count(x => x.Role == "rafter"), "COUNT", $"Rafters {roof.Str("rafterSection") ?? "8x16"}"),
            };
            foreach (var (role, name, label) in new[] { ("plate", "WallPlateLength", "Wall plates"), ("ridge", "RidgeBeamLength", "Ridge beam"), ("purlin", "PurlinLength", "Purlins"), ("hip", "HipRafterLength", "Hip rafters"), ("valley", "ValleyRafterLength", "Valley rafters"), ("trimmer", "TrimmerLength", "Trimmers") })
            {
                var len = members.Where(x => x.Role == role).Sum(x => x.Length);
                if (len > 1e-6) t.Add(new(name, len, "LENGTH", label));
            }
            yield return new QuantitySet("Lapin_RoofStructureQuantities", t, "timber", "Timber structure");
        }
        var gutters = RoofParts.Gutters(r, shape);
        if (gutters.Count > 0)
        {
            var pipes = RoofParts.Downpipes(r, shape, gutters);
            var ground = Ground(m);
            yield return new QuantitySet("Lapin_RoofDrainageQuantities",
            [
                new("GutterLength", gutters.Sum(x => x.Length), "LENGTH", roof.Str("gutterProfile") == "box" ? "Box gutter" : "Half-round gutter"),
                new("DownpipeCount", pipes.Count, "COUNT", "Downpipes"),
                new("DownpipeLength", pipes.Sum(p => p.Outlet.Z - ground), "LENGTH", "Downpipe Ø100"),
            ], "drainage", "Gutters and downpipes");
        }
        var rows = RoofParts.SnowGuards(r, shape, holes);
        if (rows.Count > 0) yield return new QuantitySet("Lapin_RoofSnowGuardQuantities", [new("Length", rows.Sum(x => (x.B - x.A).Length), "LENGTH", "Snow guards")], "snow", "Snow guards");
    }

    /// <summary>A new roof's attributes in a style (the structure is the project's: the usual one).</summary>
    public static JsonObject Attrs(RoofCatalog.Style s, IEnumerable<Vec2> contour)
    {
        var o = new JsonObject
        {
            ["contour"] = Slabs.ContourJson(contour), ["style"] = s.Id, ["shape"] = s.Shape, ["ridgeDirection"] = "auto", ["pitch"] = s.Pitch,
            ["overhang"] = s.Overhang, ["gableOverhang"] = s.GableOverhang, ["covering"] = s.Covering, ["gutterProfile"] = s.GutterProfile,
            ["gutterMaterial"] = s.GutterMaterial, ["snowGuards"] = (s.SnowGuards ?? RoofCatalog.CoveringOf(s.Covering).SnowGuards) ? "True" : "False",
            ["structure"] = "rafters", ["rafterSection"] = "8x16", ["rafterSpacing"] = 0.8, ["purlins"] = 1, ["eaveOffset"] = 0.0,
        };
        if (s.GutterColor.Length > 0) o["gutterColor"] = s.GutterColor;
        if (s.Shape == "mansard") { o["upperPitch"] = s.UpperPitch; o["mansardInset"] = s.MansardInset; }
        return o;
    }
}
