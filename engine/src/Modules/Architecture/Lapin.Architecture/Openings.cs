using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>
/// Openings in walls, and the doors and windows that fill them, as IFC has
/// them: an IfcOpeningElement voids its wall (IfcRelVoidsElement), an IfcDoor
/// or IfcWindow fills the opening (IfcRelFillsElement) and is of a door or
/// window type that carries the configurator's spec.
/// <para>
/// An opening keeps its place on its wall: <c>along</c>, its centre's
/// distance from the wall's start along the axis, and <c>sill</c>, its bottom
/// above the wall's base. Its width and height are its fill's type's (a
/// changed type refits every opening it fills); an opening with no fill keeps
/// its own. So an opening follows its wall through any edit.
/// </para>
/// </summary>
public static class Openings
{
    /// <summary>The height above the storey the plan cuts at (a plan's view range comes with the drawings, M5).</summary>
    public const double PlanCut = 1.2;

    /// <summary>An opening's place and size on its wall.</summary>
    public readonly record struct Placed(Entity Opening, Entity? Fill, JoinerySpec? Spec, double Along, double Sill, double Width, double Height);

    public static IEnumerable<Entity> Of(IModelView m, string wallId) => m.RelatedOf(wallId, IfcClass.RelVoidsElement).Where(o => o.Class == IfcClass.OpeningElement);

    public static Entity? WallOf(IModelView m, string openingId) => m.RelatingOf(openingId, IfcClass.RelVoidsElement);

    public static Entity? FillOf(IModelView m, string openingId) => m.RelatedOf(openingId, IfcClass.RelFillsElement).FirstOrDefault();

    public static Entity? OpeningOf(IModelView m, string fillId) => m.RelatingOf(fillId, IfcClass.RelFillsElement);

    public static JoinerySpec? SpecOf(IModelView m, string fillId) =>
        m.RelatingOf(fillId, IfcClass.RelDefinesByType) is { } type ? JoinerySpec.From(type.Attrs["spec"]) : null;

    public static Placed Place(IModelView m, Entity opening)
    {
        var fill = FillOf(m, opening.Id);
        var spec = fill == null ? null : SpecOf(m, fill.Id);
        return new Placed(opening, fill, spec, opening.Num("along"), opening.Num("sill"),
            spec?.Width ?? opening.Num("width", 1), spec?.Height ?? opening.Num("height", 2));
    }

    public static IReadOnlyList<Placed> PlacedOn(IModelView m, string wallId) => Of(m, wallId).Select(o => Place(m, o)).ToList();

    /// <summary>
    /// Refused unless the opening lies within its wall, along it and up its
    /// height, and clear of the wall's other openings (<paramref name="except"/>:
    /// the opening itself, when it is already there).
    /// </summary>
    public static void CheckFits(IModelView m, Entity wall, double along, double sill, double width, double height, string? except = null)
    {
        var length = PathCurve.Of(wall).Length;
        var wallHeight = wall.Num("height", Walls.DefaultHeight);
        if (!(width > 0 && height > 0)) throw new CommandException("An opening needs a width and a height.");
        if (along - width / 2 < -1e-9 || along + width / 2 > length + 1e-9) throw new CommandException($"The opening, {width * 1000:0} mm wide, does not fit along the wall ({length * 1000:0} mm) there.");
        if (sill < -1e-9 || sill + height > wallHeight + 1e-9) throw new CommandException($"The opening, {height * 1000:0} mm high from {sill * 1000:0} mm, does not fit in the wall's height ({wallHeight * 1000:0} mm).");
        foreach (var o in PlacedOn(m, wall.Id))
        {
            if (o.Opening.Id == except) continue;
            var acrossEachOther = Math.Abs(o.Along - along) < (o.Width + width) / 2 - 1e-9;
            var overEachOther = sill < o.Sill + o.Height - 1e-9 && o.Sill < sill + height - 1e-9;
            if (acrossEachOther && overEachOther) throw new CommandException($"The opening would overlap „{o.Fill?.Name ?? o.Opening.Name}” on this wall.");
        }
    }

    /// <summary>Where a point falls along the wall's axis, metres from its start.</summary>
    public static double AlongOf(Entity wall, Vec2 p)
    {
        if (PathCurve.Of(wall) is { IsArc: true } c) return c.Project(p);
        var a = Walls.Start(wall);
        var d = Walls.End(wall) - a;
        return Vec2.Dot(p - a, d) / Math.Max(d.Length, 1e-12);
    }

    // ------------------------------------------------------------ types

    /// <summary>The project's first door or window type, or a default one made on first use.</summary>
    public static Entity EnsureType(IEdit edit, string kind)
    {
        var cls = kind == "door" ? IfcClass.DoorType : IfcClass.WindowType;
        return edit.Model.OfClass(cls).FirstOrDefault() ?? CreateType(edit, kind == "door" ? JoinerySpecs.Door : JoinerySpecs.Window);
    }

    public static Entity CreateType(IEdit edit, JoinerySpec spec)
    {
        if (Joinery.Problem(spec) is { } problem) throw new CommandException(problem);
        var (cls, predefined) = spec.IsDoor ? (IfcClass.DoorType, "DOOR") : (IfcClass.WindowType, "WINDOW");
        var type = new Entity(edit.NewId(), cls, spec.Name, TypeAttrs(spec), predefined);
        edit.Add(type);
        return type;
    }

    /// <summary>The type's spec and what IFC calls its arrangement (partitioning, operation), kept with it.</summary>
    static JsonObject TypeAttrs(JoinerySpec spec)
    {
        var o = new JsonObject { ["spec"] = spec.ToJson(), ["mark"] = spec.Mark };
        if (spec.IsDoor) o["operationType"] = Joinery.DoorOperation(spec).Type;
        else o["partitioningType"] = Joinery.WindowPartitioning(spec).Type;
        return o;
    }

    /// <summary>A changed type: every opening it fills is refitted (refused when one no longer fits its wall).</summary>
    public static void UpdateType(IEdit edit, Entity type, JoinerySpec spec)
    {
        if (Joinery.Problem(spec) is { } problem) throw new CommandException(problem);
        if ((type.Class == IfcClass.DoorType) != spec.IsDoor) throw new CommandException("A door type stays a door type, a window type a window type.");
        edit.Replace(type.With(name: spec.Name, attrs: TypeAttrs(spec)));
        foreach (var fill in edit.Model.RelatedOf(type.Id, IfcClass.RelDefinesByType).ToList())
            if (OpeningOf(edit.Model, fill.Id) is { } opening && WallOf(edit.Model, opening.Id) is { } wall)
                CheckFits(edit.Model, wall, opening.Num("along"), opening.Num("sill"), spec.Width, spec.Height, opening.Id);
    }

    // ------------------------------------------------------------ placing

    /// <summary>
    /// A door or window in the wall: its opening (voiding the wall), the fill
    /// itself (filling it) of the type, on the wall's storey. Refused when it
    /// does not fit.
    /// </summary>
    public static Entity AddFill(IEdit edit, Entity wall, Entity type, double along, double? sill)
    {
        var spec = JoinerySpec.From(type.Attrs["spec"]) ?? throw new CommandException($"„{type.Name}” has no configuration.");
        var s = sill ?? spec.SillHeight;
        CheckFits(edit.Model, wall, along, s, spec.Width, spec.Height);
        var opening = new Entity(edit.NewId(), IfcClass.OpeningElement, "Opening", new JsonObject { ["along"] = along, ["sill"] = s }, "OPENING");
        edit.Add(opening);
        edit.AddRelation(new Relation(edit.NewId(), IfcClass.RelVoidsElement, wall.Id, [opening.Id]));
        var cls = spec.IsDoor ? IfcClass.Door : IfcClass.Window;
        var marks = edit.Model.OfClass(cls).Count() + 1;
        var fill = new Entity(edit.NewId(), cls, $"{spec.Mark}{marks}", new JsonObject { ["flipHand"] = false, ["flipSide"] = false }, spec.IsDoor ? "DOOR" : "WINDOW");
        edit.Add(fill);
        edit.AddRelation(new Relation(edit.NewId(), IfcClass.RelFillsElement, opening.Id, [fill.Id]));
        edit.Relate(IfcClass.RelDefinesByType, type.Id, fill.Id);
        if (Spatial.StoreyOf(edit.Model, wall.Id) is { } storey) Spatial.Contain(edit, storey.Id, fill.Id);
        return fill;
    }

    /// <summary>Moves a door's, a window's (or a bare opening's) opening along its wall or up it.</summary>
    public static void Move(IEdit edit, Entity fillOrOpening, double? along, double? sill)
    {
        var opening = fillOrOpening.Class == IfcClass.OpeningElement ? fillOrOpening : OpeningOf(edit.Model, fillOrOpening.Id) ?? throw new CommandException("It fills no opening.");
        var wall = WallOf(edit.Model, opening.Id) ?? throw new CommandException("The opening is in no wall.");
        var placed = Place(edit.Model, opening);
        var a = along ?? placed.Along;
        var s = sill ?? placed.Sill;
        CheckFits(edit.Model, wall, a, s, placed.Width, placed.Height, opening.Id);
        var attrs = opening.CloneAttrs();
        attrs["along"] = a;
        attrs["sill"] = s;
        edit.Replace(opening.With(attrs: attrs));
    }

    public static void Flip(IEdit edit, Entity fill, string axis)
    {
        var key = axis == "x" ? "flipHand" : "flipSide";
        var attrs = fill.CloneAttrs();
        attrs[key] = !fill.Bool(key);
        edit.Replace(fill.With(attrs: attrs));
    }

    public static void SetType(IEdit edit, Entity fill, Entity type)
    {
        var spec = JoinerySpec.From(type.Attrs["spec"]) ?? throw new CommandException($"„{type.Name}” has no configuration.");
        if ((fill.Class == IfcClass.Door) != spec.IsDoor) throw new CommandException("A door takes a door type, a window a window type.");
        if (OpeningOf(edit.Model, fill.Id) is { } opening && WallOf(edit.Model, opening.Id) is { } wall)
            CheckFits(edit.Model, wall, opening.Num("along"), opening.Num("sill"), spec.Width, spec.Height, opening.Id);
        edit.Relate(IfcClass.RelDefinesByType, type.Id, fill.Id);
    }

    // ------------------------------------------------------------ geometry

    /// <summary>The wall's frame for what it hosts: its start, direction, left normal, base height, body across it.</summary>
    public readonly record struct WallFrame(Vec2 Start, Vec2 Dir, Vec2 Left, double Base, double YMin, double YMax)
    {
        public Vec3 At(double x, double y, double z) => (Start + Dir * x + Left * y).At(Base + z);
        public double Centre => (YMin + YMax) / 2;
    }

    /// <summary>The wall's frame; a curved wall's is its tangent at <paramref name="along"/> (where what it hosts sits), set back so that <c>along</c> still lands there.</summary>
    public static WallFrame FrameOf(IModelView m, Entity wall, double along = 0)
    {
        var start = Walls.Start(wall);
        var dir = (Walls.End(wall) - start).Normalized();
        if (PathCurve.Of(wall) is { IsArc: true } c)
        {
            dir = c.TangentAt(along);
            start = c.PointAt(along) - dir * along;
        }
        var bands = Walls.Section(m, wall);
        var z = (Spatial.StoreyOf(m, wall.Id)?.Num("elevation") ?? 0) + wall.Num("baseOffset");
        return new WallFrame(start, dir, dir.Perp, z, bands.Min(b => b.From), bands.Max(b => b.To));
    }

    /// <summary>What an opening takes out of a curved wall: the wall's band between its sides, cut radially, a little past both faces.</summary>
    public static Solid? ArcCutter(PathCurve c, double baseZ, double yMin, double yMax, Placed p) =>
        Prism.ExtrudeFootprint(c.Band(_ => p.Along - p.Width / 2, _ => p.Along + p.Width / 2, yMin - 0.05, yMax + 0.05), baseZ + p.Sill, p.Height, "opening");

    /// <summary>The body an opening takes out of its wall, straight or curved.</summary>
    public static Solid? CutterIn(IModelView m, Entity wall, Placed p)
    {
        var f = FrameOf(m, wall, p.Along);
        return PathCurve.Of(wall) is { IsArc: true } c ? ArcCutter(c, f.Base, f.YMin, f.YMax, p) : Cutter(f, p);
    }

    /// <summary>
    /// What an opening takes out of a straight wall: its box through the whole
    /// thickness, and the room its details need (the reveal's finish on each
    /// side, widening the opening by the skin; the sills let into the wall),
    /// each with the opening's reach in height so every part it crosses is cut.
    /// </summary>
    public static List<(Placed Reach, Solid Box)> Cutters(IModelView m, Entity wall, WallFrame f, Placed p)
    {
        var o = new List<(Placed, Solid)>();
        if (Cutter(f, p) is { } box) o.Add((p, box));
        if (p.Spec == null || p.Fill == null) return o;
        if (PathCurve.Of(wall) is { IsArc: true }) return o;
        var d = new Dressing(m, wall, f, p, p.Spec, p.Fill.Bool("flipHand") ? -1 : 1, p.Fill.Bool("flipSide") ? -1 : 1).WithPlaced(p);
        o.AddRange(d.Cutters());
        return o;
    }

    /// <summary>The box an opening takes out of its wall, through the whole thickness and a little past it.</summary>
    public static Solid? Cutter(WallFrame f, Placed p)
    {
        double x0 = p.Along - p.Width / 2, x1 = p.Along + p.Width / 2, y0 = f.YMin - 0.05, y1 = f.YMax + 0.05;
        Vec2[] footprint = [(f.Start + f.Dir * x0 + f.Left * y0), (f.Start + f.Dir * x1 + f.Left * y0), (f.Start + f.Dir * x1 + f.Left * y1), (f.Start + f.Dir * x0 + f.Left * y1)];
        return Prism.ExtrudeFootprint(footprint, f.Base + p.Sill, p.Height, "opening");
    }

    static readonly Dictionary<string, string> HandleColor = new() { ["handle"] = "#5a5a5a", ["board"] = "#dcdcdc" };

    /// <summary>
    /// A door's or window's body in its wall: the configurator's boxes, from
    /// the type frame (X along the opening, Y to the exterior, Z up) into the
    /// wall's, the exterior on the wall's right face; mirrored for a flipped
    /// hand (X) or side (Y). One part per role.
    /// </summary>
    public static ElementGeometry? FillGeometry(Entity fill, IModelView m)
    {
        var spec = SpecOf(m, fill.Id);
        var opening = OpeningOf(m, fill.Id);
        var wall = opening == null ? null : WallOf(m, opening.Id);
        if (spec == null || opening == null || wall == null) return new ElementGeometry([], Diagnostics: ["A door or window needs its type and its opening in a wall."]);
        var p = Place(m, opening);
        var f = FrameOf(m, wall, p.Along);
        var sx = fill.Bool("flipHand") ? -1 : 1;
        var sy = fill.Bool("flipSide") ? -1 : 1;
        // Type Y (+ to the exterior) is the wall's right: −left.
        Vec3 Map(double x, double y, double z) => f.At(p.Along + sx * x, f.Centre - sy * y, p.Sill + z);

        var parts = new List<GeometryPart>();
        var dressing = PathCurve.Of(wall) is { IsArc: true } ? null : new Dressing(m, wall, f, p, spec, sx, sy);
        foreach (var group in Joinery.Boxes(spec).Where(b => dressing?.Replaces(b) != true).GroupBy(b => b.Role))
        {
            var solids = group.Select(b => Box(b, Map)).OfType<Solid>().ToList();
            if (solids.Count == 0) continue;
            var color = group.Key switch { "glass" => spec.Colors.Glass, "leaf" => spec.Colors.Leaf, _ => HandleColor.GetValueOrDefault(group.Key, spec.Colors.Frame) };
            parts.Add(new GeometryPart(group.Key, color, Combine(solids, group.Key), null, group.Key));
        }
        if (dressing != null) parts.AddRange(dressing.Parts());
        return new ElementGeometry(parts, Symbol(spec, f, p, sx, sy, dressing != null), [f.At(p.Along, f.Centre, 0).Xy]);
    }

    /// <summary>
    /// The details around one door or window in its wall (docs/plan-views-and-joinery.md §3):
    /// the reveal's finish on the jambs and head, each side of the frame with
    /// its own material (the wall's face layer by default), the interior and
    /// exterior sills, a surround on the facade, a door's threshold. Each is a
    /// part with its material, so it shows in 3D, is cut in plans and sections,
    /// is measured and goes to IFC. The wall is cut to make room for what sits
    /// inside it (<see cref="Cutters"/>). In the type's frame: X along the
    /// opening, Y to the exterior, Z up from the opening's bottom; the wall's
    /// faces at ±T/2.
    /// </summary>
    sealed class Dressing
    {
        readonly IModelView _m;
        readonly JoinerySpec _spec;
        readonly JoineryDetails _d;
        readonly Func<double, double, double, Vec3> _map;
        readonly double _t, _yFE, _yFI;
        readonly (double Thickness, string? Material)[] _finish; // [0] the exterior side, [1] the interior

        public Dressing(IModelView m, Entity wall, WallFrame f, Placed p, JoinerySpec spec, int sx, int sy)
        {
            _m = m;
            _spec = spec;
            _d = spec.Details ?? new JoineryDetails();
            _map = (x, y, z) => f.At(p.Along + sx * x, f.Centre - sy * y, p.Sill + z);
            _t = f.YMax - f.YMin;
            _yFE = spec.Frame.Offset + spec.Frame.Depth / 2;
            _yFI = spec.Frame.Offset - spec.Frame.Depth / 2;
            _finish = Finishes(m, wall, sy, _d.Reveal ?? new JoineryReveal());
        }

        double YExt => _t / 2;
        double YInt => -_t / 2;
        double W => _spec.Width;
        double H => _spec.Height;

        /// <summary>The reveal's finish on each side: from the wall's face layers (a layer that is not the core, at the face), or the named material, or none.</summary>
        static (double, string?)[] Finishes(IModelView m, Entity wall, int sy, JoineryReveal r)
        {
            if (r.Mode == "none") return [(0, null), (0, null)];
            if (r.Mode == "material") { var id = MaterialNamed(m, r.Material); return [(Math.Max(0, r.Thickness), id), (Math.Max(0, r.Thickness), id)]; }
            var bands = Walls.Section(m, wall);
            if (bands.Count < 2) return [(0, null), (0, null)];
            // The exterior is the wall's right (its first layer), the left when the side is flipped.
            var (ext, inte) = sy > 0 ? (bands[0], bands[^1]) : (bands[^1], bands[0]);
            (double, string?) Of(Walls.Band b) => b.Core || b.Material == null ? (0, null) : (Math.Min(0.025, Math.Abs(b.To - b.From)), b.Material);
            return [Of(ext), Of(inte)];
        }

        static string? MaterialNamed(IModelView m, string? name) =>
            string.IsNullOrWhiteSpace(name) ? null : m.OfClass(IfcClass.Material).FirstOrDefault(x => string.Equals(x.Name, name.Trim(), StringComparison.OrdinalIgnoreCase))?.Id;

        string ColorOf(string? materialId, string? name, string fallback) =>
            materialId != null ? Materials.ColorOf(_m, materialId) : !string.IsNullOrWhiteSpace(name) ? Materials.DefaultColor(name) : fallback;

        /// <summary>A box of the type's frame as a solid, or null when flat.</summary>
        Solid? Box(double x0, double x1, double y0, double y1, double z0, double z1, string role) =>
            x1 - x0 > 1e-6 && y1 - y0 > 1e-6 && z1 - z0 > 1e-6 ? Openings.Box(new JoineryBox(role, new Vec3(x0, y0, z0), new Vec3(x1, y1, z1)), _map) : null;

        /// <summary>A profile in the Y–Z plane at x0, swept to x1 (the sloped exterior sill).</summary>
        Solid? Swept(double x0, double x1, IReadOnlyList<(double Y, double Z)> profile, string role)
        {
            if (x1 - x0 < 1e-6) return null;
            var pts = profile.Select(q => _map(x0, q.Y, q.Z)).ToList();
            return Prism.Extrude(pts, null, _map(x1, 0, 0) - _map(x0, 0, 0), role);
        }

        bool SillInsideOn => _d.SillInside is { On: true };
        bool SillOutsideOn => _d.SillOutside is { On: true };
        bool ThresholdOn => _spec.IsDoor && _d.Threshold is { On: true };

        /// <summary>Whether a box of the type's body gives way to a detail: the boards to the detailed sills, the frame's bottom member to the threshold.</summary>
        public bool Replaces(JoineryBox b) =>
            b.Role == "board" && (b.Min.Y < 0 ? SillInsideOn : SillOutsideOn)
            || ThresholdOn && b.Role == "frame" && b.Min.Z < 1e-9 && b.Max.Z <= _spec.Threshold + 1e-9 && b.Max.X - b.Min.X > W - _spec.Frame.Width * 2 - 1e-9;

        /// <summary>The exterior sill's section (Y, Z): from under the frame, out past the face, sloping down.</summary>
        IReadOnlyList<(double, double)> OutsideSillProfile(JoinerySill sill, double past)
        {
            var yOut = YExt + Math.Max(0, sill.Projection) + past;
            var drop = (yOut - _yFE) * Math.Tan(Math.Clamp(sill.Slope, 0, 45) * Math.PI / 180);
            return [(_yFE, 0), (yOut, -drop), (yOut, -drop - sill.Thickness), (_yFE, -sill.Thickness)];
        }

        public IEnumerable<GeometryPart> Parts()
        {
            // The reveal's finish: the jambs and the head, each side from the frame's face to the wall's.
            foreach (var (side, (t, material), y0, y1) in new[] { ("outside", _finish[0], _yFE, YExt), ("inside", _finish[1], YInt, _yFI) })
            {
                if (t <= 0 || y1 - y0 < 1e-3) continue;
                var solids = new[] { Box(-W / 2 - t, -W / 2, y0, y1, 0, H + t, "reveal"), Box(W / 2, W / 2 + t, y0, y1, 0, H + t, "reveal"), Box(-W / 2, W / 2, y0, y1, H, H + t, "reveal") }.OfType<Solid>().ToList();
                if (solids.Count > 0) yield return new GeometryPart($"Reveal ({side})", ColorOf(material, null, "#e8e2d6"), Combine(solids, "reveal"), material, "reveal:" + side);
            }
            if (SillInsideOn)
            {
                var sill = _d.SillInside!;
                var id = MaterialNamed(_m, sill.Material);
                if (Box(-W / 2 - sill.Overhang, W / 2 + sill.Overhang, YInt - Math.Max(0, sill.Projection), _yFI, -sill.Thickness, 0, "sill") is { } b)
                    yield return new GeometryPart("Sill (inside)", ColorOf(id, sill.Material, "#dcdcdc"), b, id, "sill:inside");
            }
            if (SillOutsideOn)
            {
                var sill = _d.SillOutside!;
                var id = MaterialNamed(_m, sill.Material);
                if (Swept(-W / 2 - sill.Overhang, W / 2 + sill.Overhang, OutsideSillProfile(sill, 0), "sill") is { } b)
                    yield return new GeometryPart("Sill (outside)", ColorOf(id, sill.Material, "#b8b8b8"), b, id, "sill:outside");
            }
            if (_d.Surround is { On: true } sur)
            {
                var (w, dpt) = (Math.Max(0.01, sur.Width), Math.Max(0.005, sur.Projection));
                var id = MaterialNamed(_m, sur.Material);
                var z0 = sur.Sill && !_spec.IsDoor ? -w : 0;
                var solids = new[]
                {
                    Box(-W / 2 - w, -W / 2, YExt, YExt + dpt, z0, H + w, "surround"), Box(W / 2, W / 2 + w, YExt, YExt + dpt, z0, H + w, "surround"),
                    Box(-W / 2, W / 2, YExt, YExt + dpt, H, H + w, "surround"), sur.Sill && !_spec.IsDoor ? Box(-W / 2, W / 2, YExt, YExt + dpt, -w, 0, "surround") : null,
                }.OfType<Solid>().ToList();
                if (solids.Count > 0) yield return new GeometryPart("Surround", ColorOf(id, sur.Material, "#e0d8c8"), Combine(solids, "surround"), id, "surround");
            }
            if (ThresholdOn)
            {
                var th = _d.Threshold!;
                var id = MaterialNamed(_m, th.Material);
                if (Box(-W / 2, W / 2, YInt, YExt, 0, Math.Max(0.005, th.Height), "threshold") is { } b)
                    yield return new GeometryPart("Threshold", ColorOf(id, th.Material, "#8a8a8a"), b, id, "threshold");
            }
            // The dressings on the facade (shutters, a grille, a window box, a canopy), by the wall's faces here.
            foreach (var piece in WindowDressings.Pieces(_spec, WindowDressings.Of(_spec, _t)))
            {
                var id = MaterialNamed(_m, piece.Material);
                var solids = piece.Boxes.Select(x => Openings.Box(x, _map)).OfType<Solid>().ToList();
                if (piece.Swept is { } sw && Swept(sw.X0, sw.X1, sw.Profile, piece.Key) is { } swept) solids.Add(swept);
                if (solids.Count > 0) yield return new GeometryPart(piece.Name, ColorOf(id, piece.Material, piece.Color), Combine(solids, piece.Key), id, piece.Key);
            }
        }

        /// <summary>What the wall gives up for the details inside it, each with the height it reaches.</summary>
        public IEnumerable<(Placed Reach, Solid Box)> Cutters()
        {
            foreach (var ((t, _), y0, y1, past) in new[] { (_finish[0], _yFE, YExt, 0.05), (_finish[1], YInt, _yFI, -0.05) })
            {
                if (t <= 0 || y1 - y0 < 1e-3) continue;
                var box = past > 0 ? Box(-W / 2 - t, W / 2 + t, y0, y1 + past, 0, H + t, "opening") : Box(-W / 2 - t, W / 2 + t, y0 + past, y1, 0, H + t, "opening");
                if (box != null) yield return (Reach(0, t), box);
            }
            if (SillInsideOn)
            {
                var sill = _d.SillInside!;
                var box = Box(-W / 2 - sill.Overhang, W / 2 + sill.Overhang, YInt - Math.Max(0, sill.Projection) - 0.05, _yFI, -sill.Thickness, 0, "opening");
                if (box != null) yield return (Reach(sill.Thickness, 0), box);
            }
            if (SillOutsideOn)
            {
                var sill = _d.SillOutside!;
                var box = Swept(-W / 2 - sill.Overhang, W / 2 + sill.Overhang, OutsideSillProfile(sill, 0.05), "opening");
                if (box != null) yield return (Reach(sill.Thickness + (YExt + sill.Projection + 0.05 - _yFE) * Math.Tan(Math.Clamp(sill.Slope, 0, 45) * Math.PI / 180), 0), box);
            }

            Placed Reach(double below, double above) => _placed with { Sill = _placed.Sill - below, Height = _placed.Height + below + above };
        }

        Placed _placed;
        public Dressing WithPlaced(Placed p) { _placed = p; return this; }
    }

    /// <summary>The details' measures for the schedules and quantities: the reveal's finished area each side, the sills' lengths, the surround's face, the threshold's length.</summary>
    public static IEnumerable<(string Name, double Value, string Unit)> DetailQuantities(IModelView m, Entity fill)
    {
        if (SpecOf(m, fill.Id) is not { } spec || OpeningOf(m, fill.Id) is not { } opening || WallOf(m, opening.Id) is not { } wall) yield break;
        var p = Place(m, opening);
        var f = FrameOf(m, wall, p.Along);
        foreach (var part in (FillGeometry(fill, m)?.Parts ?? []).Where(x => x.Key != null && (x.Key.StartsWith("reveal", StringComparison.Ordinal) || x.Key.StartsWith("sill", StringComparison.Ordinal) || x.Key is "surround" or "threshold" or "shutters" or "grille" or "windowbox" or "canopy")))
        {
            var (w, h, t) = (spec.Width, spec.Height, f.YMax - f.YMin);
            var d = spec.Details ?? new JoineryDetails();
            switch (part.Key)
            {
                case "reveal:outside": yield return ("RevealAreaOutside", (2 * h + w) * Math.Max(0, t / 2 - (spec.Frame.Offset + spec.Frame.Depth / 2)), "AREA"); break;
                case "reveal:inside": yield return ("RevealAreaInside", (2 * h + w) * Math.Max(0, (spec.Frame.Offset - spec.Frame.Depth / 2) + t / 2), "AREA"); break;
                case "sill:inside": yield return ("SillInsideLength", w + 2 * (d.SillInside?.Overhang ?? 0), "LENGTH"); break;
                case "sill:outside": yield return ("SillOutsideLength", w + 2 * (d.SillOutside?.Overhang ?? 0), "LENGTH"); break;
                case "surround":
                {
                    var sw = d.Surround?.Width ?? 0;
                    var withSill = d.Surround?.Sill == true && !spec.IsDoor;
                    yield return ("SurroundArea", (w + 2 * sw) * (h + sw + (withSill ? sw : 0)) - w * h, "AREA");
                    break;
                }
                case "threshold": yield return ("ThresholdLength", w, "LENGTH"); break;
                case "shutters": yield return ("ShutterArea", (w + 2 * (d.Shutters?.Overlap ?? 0)) * (d.Shutters?.Height ?? h), "AREA"); break;
                case "grille":
                {
                    var lap = d.Grille?.Side is "outside" or "inside" ? d.Grille.Overlap : 0;
                    yield return ("GrilleArea", (w + 2 * lap) * (h + 2 * lap), "AREA");
                    break;
                }
                case "windowbox": yield return ("WindowBoxLength", d.WindowBox?.Width ?? w, "LENGTH"); break;
                case "canopy": yield return ("CanopyArea", (w + 2 * (d.Canopy?.Overhang ?? 0)) * (d.Canopy?.Projection ?? 0), "AREA"); break;
            }
        }
    }

    static Solid? Box(JoineryBox b, Func<double, double, double, Vec3> map)
    {
        Vec3[] c =
        [
            map(b.Min.X, b.Min.Y, b.Min.Z), map(b.Max.X, b.Min.Y, b.Min.Z), map(b.Max.X, b.Max.Y, b.Min.Z), map(b.Min.X, b.Max.Y, b.Min.Z),
            map(b.Min.X, b.Min.Y, b.Max.Z), map(b.Max.X, b.Min.Y, b.Max.Z), map(b.Max.X, b.Max.Y, b.Max.Z), map(b.Min.X, b.Max.Y, b.Max.Z),
        ];
        // A mirror turns the box inside out; the footprint is taken counter-clockwise again.
        var footprint = new[] { c[0], c[1], c[2], c[3] }.Select(v => v.Xy).ToList();
        if (Polygon2.SignedArea(footprint) < 0) footprint.Reverse();
        return Prism.ExtrudeFootprint(footprint, c[0].Z, c[4].Z - c[0].Z, b.Role);
    }

    /// <summary>Several closed shells as one solid (their faces side by side).</summary>
    public static Solid Combine(IReadOnlyList<Solid> solids, string? tag)
    {
        if (solids.Count == 1) return solids[0];
        var vertices = new List<Vec3>();
        var faces = new List<Face>();
        foreach (var s in solids)
        {
            var at = vertices.Count;
            vertices.AddRange(s.Vertices);
            foreach (var f in s.Faces)
            {
                var holes = new int[f.Holes.Length][];
                for (int h = 0; h < holes.Length; h++) holes[h] = Shift(f.Holes[h], at);
                faces.Add(f with { Outer = Shift(f.Outer, at), Holes = holes });
            }
        }
        return new Solid([.. vertices], [.. faces], tag);
    }

    static int[] Shift(int[] loop, int by)
    {
        var o = new int[loop.Length];
        for (int i = 0; i < loop.Length; i++) o[i] = loop[i] + by;
        return o;
    }

    /// <summary>
    /// The dressings' outlines in plan, in the type's frame: each piece's footprint (its boxes' bounds per leaf or
    /// frame side), "heavy" where it spans the cut height <paramref name="cutZ"/> (over the opening's bottom), "symbol" below, "overhead" above.
    /// </summary>
    public static IEnumerable<(string Kind, double X0, double X1, double Y0, double Y1)> DressingPlan(JoinerySpec spec, double thickness, double cutZ)
    {
        foreach (var piece in WindowDressings.Pieces(spec, WindowDressings.Of(spec, thickness)))
        {
            var boxes = piece.Boxes.Select(b => (b.Min.X, b.Max.X, b.Min.Y, b.Max.Y, b.Min.Z, b.Max.Z)).ToList();
            if (piece.Swept is { } sw) boxes.Add((sw.X0, sw.X1, sw.Profile.Min(q => q.Y), sw.Profile.Max(q => q.Y), sw.Profile.Min(q => q.Z), sw.Profile.Max(q => q.Z)));
            if (boxes.Count == 0) continue;
            // One outline per run of boxes side by side along the wall (a leaf, a frame side), so open leaves stay apart.
            foreach (var g in Runs(boxes))
            {
                var (z0, z1) = (g.Min(b => b.Item5), g.Max(b => b.Item6));
                var kind = z0 <= cutZ && z1 >= cutZ ? "heavy" : z1 < cutZ ? "symbol" : "overhead";
                yield return (kind, g.Min(b => b.Item1), g.Max(b => b.Item2), g.Min(b => b.Item3), g.Max(b => b.Item4));
            }
        }

        static List<List<(double, double, double, double, double, double)>> Runs(List<(double, double, double, double, double, double)> boxes)
        {
            var runs = new List<List<(double, double, double, double, double, double)>>();
            foreach (var b in boxes.OrderBy(b => b.Item1))
            {
                var last = runs.LastOrDefault();
                if (last != null && b.Item1 <= last.Max(x => x.Item2) + 0.02) last.Add(b);
                else runs.Add([b]);
            }
            return runs;
        }
    }

    /// <summary>
    /// The plan symbol: the jambs across the wall, then the symbol engine's plan
    /// (<see cref="JoinerySymbols.Plan"/>: the frame cut, the glass, a door's
    /// leaves open at 90° with their arcs), in the wall's frame, mirrored with
    /// the occurrence.
    /// </summary>
    static List<PlanItem> Symbol(JoinerySpec spec, WallFrame f, Placed p, int sx, int sy, bool dressed)
    {
        var o = new List<PlanItem>();
        Vec2 P(double x, double y) => f.Start + f.Dir * (p.Along + sx * x) + f.Left * (f.Centre - sy * y);
        double half = spec.Width / 2, depth = f.YMax - f.YMin;
        // Where a click in plan picks it: the opening across the wall (not drawn).
        o.Add(new PlanItem("hit", true, [P(-half, -depth / 2), P(half, -depth / 2), P(half, depth / 2), P(-half, depth / 2)]));
        // The jambs across the wall.
        o.Add(new PlanItem("symbol", false, [P(-half, -depth / 2), P(-half, depth / 2)]));
        o.Add(new PlanItem("symbol", false, [P(half, -depth / 2), P(half, depth / 2)]));
        // The symbol engine's plan (docs/simboluri-tamplarie.md): the frame cut, the glass, a door's leaves and their arcs.
        foreach (var st in JoinerySymbols.Plan(spec))
            o.Add(new PlanItem(st.Weight == "cut" ? "heavy" : st.Dashed ? "dashed" : "symbol", st.Closed, st.Pts.Select(q => P(q.X, q.Y)).ToList()));
        // The dressings seen in plan: what crosses the cut (shutters, a grille) cut, a window box below it seen, a canopy above it overhead.
        if (dressed)
            foreach (var (kind, x0, x1, y0, y1) in DressingPlan(spec, depth, PlanCut - p.Sill))
            o.Add(new PlanItem(kind, true, [P(x0, y0), P(x1, y0), P(x1, y1), P(x0, y1)]));
        return o;
    }
}

/// <summary>The default door and window types a project starts with.</summary>
public static class JoinerySpecs
{
    public static JoinerySpec Door => JoinerySpec.DefaultDoor("Door 900×2100");
    public static JoinerySpec Window => JoinerySpec.DefaultWindow("Window 1200×1500");
}
