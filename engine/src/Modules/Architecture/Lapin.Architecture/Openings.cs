using System.Text.Json.Nodes;
using BubbleBim.Geometry;
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

    /// <summary>Refused unless the opening lies within its wall: along it and up its height.</summary>
    public static void CheckFits(IModelView m, Entity wall, double along, double sill, double width, double height)
    {
        var length = (Walls.End(wall) - Walls.Start(wall)).Length;
        var wallHeight = wall.Num("height", Walls.DefaultHeight);
        if (!(width > 0 && height > 0)) throw new CommandException("An opening needs a width and a height.");
        if (along - width / 2 < -1e-9 || along + width / 2 > length + 1e-9) throw new CommandException($"The opening, {width * 1000:0} mm wide, does not fit along the wall ({length * 1000:0} mm) there.");
        if (sill < -1e-9 || sill + height > wallHeight + 1e-9) throw new CommandException($"The opening, {height * 1000:0} mm high from {sill * 1000:0} mm, does not fit in the wall's height ({wallHeight * 1000:0} mm).");
    }

    /// <summary>Where a point falls along the wall's axis, metres from its start.</summary>
    public static double AlongOf(Entity wall, Vec2 p)
    {
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
                CheckFits(edit.Model, wall, opening.Num("along"), opening.Num("sill"), spec.Width, spec.Height);
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
        CheckFits(edit.Model, wall, a, s, placed.Width, placed.Height);
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
            CheckFits(edit.Model, wall, opening.Num("along"), opening.Num("sill"), spec.Width, spec.Height);
        edit.Relate(IfcClass.RelDefinesByType, type.Id, fill.Id);
    }

    // ------------------------------------------------------------ geometry

    /// <summary>The wall's frame for what it hosts: its start, direction, left normal, base height, body across it.</summary>
    public readonly record struct WallFrame(Vec2 Start, Vec2 Dir, Vec2 Left, double Base, double YMin, double YMax)
    {
        public Vec3 At(double x, double y, double z) => (Start + Dir * x + Left * y).At(Base + z);
        public double Centre => (YMin + YMax) / 2;
    }

    public static WallFrame FrameOf(IModelView m, Entity wall)
    {
        var start = Walls.Start(wall);
        var dir = (Walls.End(wall) - start).Normalized();
        var bands = Walls.Section(m, wall);
        var z = (Spatial.StoreyOf(m, wall.Id)?.Num("elevation") ?? 0) + wall.Num("baseOffset");
        return new WallFrame(start, dir, dir.Perp, z, bands.Min(b => b.From), bands.Max(b => b.To));
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
        var f = FrameOf(m, wall);
        var p = Place(m, opening);
        var sx = fill.Bool("flipHand") ? -1 : 1;
        var sy = fill.Bool("flipSide") ? -1 : 1;
        // Type Y (+ to the exterior) is the wall's right: −left.
        Vec3 Map(double x, double y, double z) => f.At(p.Along + sx * x, f.Centre - sy * y, p.Sill + z);

        var parts = new List<GeometryPart>();
        foreach (var group in Joinery.Boxes(spec).GroupBy(b => b.Role))
        {
            var solids = group.Select(b => Box(b, Map)).OfType<Solid>().ToList();
            if (solids.Count == 0) continue;
            var color = group.Key switch { "glass" => spec.Colors.Glass, "leaf" => spec.Colors.Leaf, _ => HandleColor.GetValueOrDefault(group.Key, spec.Colors.Frame) };
            parts.Add(new GeometryPart(group.Key, color, Combine(solids, group.Key), null, group.Key));
        }
        return new ElementGeometry(parts, Symbol(spec, f, p, sx, sy), [f.At(p.Along, f.Centre, 0).Xy]);
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
            faces.AddRange(s.Faces.Select(f => f with { Outer = f.Outer.Select(i => i + at).ToArray(), Holes = f.Holes.Select(h => h.Select(i => i + at).ToArray()).ToArray() }));
        }
        return new Solid([.. vertices], [.. faces], tag);
    }

    /// <summary>
    /// The plan symbol (docs/transfer.md): a door's leaves open at 90° with
    /// their arcs, a window's frame and its double glass line. Drawn on the
    /// interior side, mirrored with the occurrence.
    /// </summary>
    static List<PlanItem> Symbol(JoinerySpec spec, WallFrame f, Placed p, int sx, int sy)
    {
        var o = new List<PlanItem>();
        Vec2 P(double x, double y) => f.Start + f.Dir * (p.Along + sx * x) + f.Left * (f.Centre - sy * y);
        double half = spec.Width / 2, depth = f.YMax - f.YMin;
        // Where a click in plan picks it: the opening across the wall (not drawn).
        o.Add(new PlanItem("hit", true, [P(-half, -depth / 2), P(half, -depth / 2), P(half, depth / 2), P(-half, depth / 2)]));
        // The jambs across the wall.
        o.Add(new PlanItem("symbol", false, [P(-half, -depth / 2), P(-half, depth / 2)]));
        o.Add(new PlanItem("symbol", false, [P(half, -depth / 2), P(half, depth / 2)]));
        if (spec.IsDoor)
        {
            var leaves = Joinery.Normalised(spec).Where(x => Joinery.IsDoorLeaf(x.Operation) && Joinery.SwingsInPlan(x.Operation)).ToList();
            foreach (var leaf in leaves)
            {
                var r = Joinery.PanelRect(spec, leaf);
                var hingeLeft = leaf.Operation.EndsWith("-left");
                var hinge = hingeLeft ? r.X0 : r.X1;
                var free = hingeLeft ? r.X1 : r.X0;
                var w = Math.Abs(free - hinge);
                var y0 = -depth / 2;   // the interior face
                // The leaf open at 90° into the interior, a heavy rectangle clamp(depth × 0.4, 2, 5 cm) thick, and its arc from the closed position.
                var t = Math.Clamp(spec.Frame.Depth * 0.4, 0.02, 0.05) * Math.Sign(free - hinge);
                o.Add(new PlanItem("heavy", true, [P(hinge, y0), P(hinge, y0 - w), P(hinge + t, y0 - w), P(hinge + t, y0)]));
                var arc = new List<Vec2>();
                for (var i = 0; i <= 16; i++)
                {
                    var a = i / 16.0 * Math.PI / 2;
                    arc.Add(P(hinge + Math.Sign(free - hinge) * w * Math.Cos(a), y0 - w * Math.Sin(a)));
                }
                o.Add(new PlanItem("symbol", false, arc));
            }
        }
        else
        {
            var fd = spec.Frame.Depth;
            var g = Math.Min(0.012, fd / 3);
            o.Add(new PlanItem("symbol", true, [P(-half, -fd / 2 + spec.Frame.Offset), P(half, -fd / 2 + spec.Frame.Offset), P(half, fd / 2 + spec.Frame.Offset), P(-half, fd / 2 + spec.Frame.Offset)]));
            o.Add(new PlanItem("symbol", false, [P(-half, spec.Frame.Offset - g), P(half, spec.Frame.Offset - g)]));
            o.Add(new PlanItem("symbol", false, [P(-half, spec.Frame.Offset + g), P(half, spec.Frame.Offset + g)]));
        }
        return o;
    }
}

/// <summary>The default door and window types a project starts with.</summary>
public static class JoinerySpecs
{
    public static JoinerySpec Door => JoinerySpec.DefaultDoor("Door 900×2100");
    public static JoinerySpec Window => JoinerySpec.DefaultWindow("Window 1200×1500");
}
