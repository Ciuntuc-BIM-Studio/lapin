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

    /// <summary>An opening's place and size on its wall; <paramref name="Shape"/>: its outline, the fill's type's or a bare opening's own (null: a rectangle).</summary>
    /// <remarks>
    /// Along, Sill, Width and Height are the hole's. A door's or window's is its type's size unless its opening is the file's own
    /// (<see cref="HoleWidthKey"/>, <see cref="HoleHeightKey"/>); the fill stands centred on it, <see cref="FillOffset"/> over its
    /// bottom (<see cref="FillOffsetKey"/>: a door on the finished floor in a hole the file cuts down to the slab), at <see cref="FillSill"/>.
    /// </remarks>
    public readonly record struct Placed(Entity Opening, Entity? Fill, JoinerySpec? Spec, double Along, double Sill, double Width, double Height, JoineryShape? Shape = null, double FillOffset = 0)
    {
        /// <summary>Where the door or window stands, over the wall's base: the hole's sill, raised by its offset.</summary>
        public double FillSill => Sill + FillOffset;
    }

    public static IEnumerable<Entity> Of(IModelView m, string wallId) => m.RelatedOf(wallId, IfcClass.RelVoidsElement).Where(o => o.Class == IfcClass.OpeningElement);

    public static Entity? WallOf(IModelView m, string openingId) => m.RelatingOf(openingId, IfcClass.RelVoidsElement);

    public static Entity? FillOf(IModelView m, string openingId) => m.RelatedOf(openingId, IfcClass.RelFillsElement).FirstOrDefault();

    public static Entity? OpeningOf(IModelView m, string fillId) => m.RelatingOf(fillId, IfcClass.RelFillsElement);

    public static JoinerySpec? SpecOf(IModelView m, string fillId)
    {
        if (m.RelatingOf(fillId, IfcClass.RelDefinesByType) is not { } type || JoinerySpec.From(type.Attrs["spec"]) is not { } spec) return null;
        // The frame's place in the wall (report #76): the element's own value over the type's, worked out from the wall's build-up.
        var fill = m.Get(fillId);
        // A curtain wall's door (report #133): its type at the size it stands in its cell.
        if (fill != null && CurtainCells.SeatOf(m, fill) is { } seat) return seat.Spec ?? spec;
        var own = JoineryPosition.From_(fill?.Attrs["position"]);
        if (own == null && spec.Position == null) return spec;
        return OpeningOf(m, fillId) is { } opening && WallOf(m, opening.Id) is { } wall ? spec.Positioned(m, wall, own, fill?.Bool("flipSide") == true) : spec;
    }

    /// <summary>The spec a type keeps, without any element's position worked in.</summary>
    public static JoinerySpec? TypeSpecOf(Entity type) => JoinerySpec.From(type.Attrs["spec"]);

    /// <summary>
    /// A door's or window's opening of its own size (<c>holeWidth</c>, <c>holeHeight</c> on the opening, metres): the hole
    /// an imported file cuts round its fill, kept as the file has it where it differs from the fill's size (docs/spec-ifc.md,
    /// Recunoașterea). The fill stays its type's size, centred on the opening at its sill. An opening drawn in Lapin has
    /// neither and is its type's size; choosing another type clears them.
    /// </summary>
    public const string HoleWidthKey = "holeWidth", HoleHeightKey = "holeHeight", FillOffsetKey = "fillOffset";

    public static Placed Place(IModelView m, Entity opening)
    {
        var fill = FillOf(m, opening.Id);
        var spec = fill == null ? null : SpecOf(m, fill.Id);
        var (width, height) = spec == null ? (opening.Num("width", 1), opening.Num("height", 2))
            : (opening.NumOrNull(HoleWidthKey) ?? spec.Width, opening.NumOrNull(HoleHeightKey) ?? spec.Height);
        return new Placed(opening, fill, spec, opening.Num("along"), opening.Num("sill"), width, height, fill == null ? BareShape(opening) : spec?.Shape,
            spec == null ? 0 : opening.Num(FillOffsetKey));
    }

    /// <summary>The head kinds a bare opening takes (decision 253): "round" is the semicircle.</summary>
    public static readonly string[] BareKinds = ["rect", "segmental", "elliptical", "round", "radiused", "circle"];

    /// <summary>A bare opening's own outline (its <c>shape</c> and <c>rise</c>); null for a rectangle.</summary>
    public static JoineryShape? BareShape(Entity opening) =>
        opening.Str("shape") is { } k && k != "rect" && BareKinds.Contains(k) ? new JoineryShape(k, opening.NumOrNull("rise")) : null;

    /// <summary>A bare opening's outline as the joinery's outline code takes it.</summary>
    static readonly JoinerySpec BareSpec = JoinerySpec.DefaultWindow("Opening");

    public static IReadOnlyList<Placed> PlacedOn(IModelView m, string wallId) => Of(m, wallId).Select(o => Place(m, o)).ToList();

    /// <summary>
    /// Refused unless the opening lies within its wall, along it and up its
    /// height, and clear of the wall's other openings (<paramref name="except"/>:
    /// the opening itself, when it is already there).
    /// </summary>
    public static void CheckFits(IModelView m, Entity wall, double along, double sill, double width, double height, string? except = null)
    {
        var length = PathCurve.Of(wall).Length;
        var wallHeight = Walls.HeightOf(m, wall);
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

    /// <summary>
    /// Where the openings of straight walls recognised from a file (their <c>imported</c> record) stand in plan, to keep across an
    /// edit that moves a wall's start (decision 546). An opening is placed by its distance from its wall's start (<c>along</c>),
    /// so a start that moves (a length typed, a joined neighbour's end dragged) would carry the openings with it; the file put
    /// them where they are, and they stay there (<see cref="KeepPlaces"/>). Openings in walls drawn in Lapin go with the start.
    /// </summary>
    public static List<(string Opening, string Wall, Vec2 At)> PlacesOnRecognised(IModelView m, IEnumerable<string> wallIds)
    {
        var o = new List<(string, string, Vec2)>();
        foreach (var id in wallIds.Distinct())
        {
            if (m.Get(id) is not { Class: IfcClass.Wall } w || w.Attrs["imported"] == null || PathCurve.Of(w).IsArc) continue;
            var a = Walls.Start(w);
            var d = Walls.End(w) - a;
            if (d.Length < 1e-9) continue;
            d = d * (1 / d.Length);
            foreach (var p in PlacedOn(m, id)) o.Add((p.Opening.Id, id, a + d * p.Along));
        }
        return o;
    }

    /// <summary>
    /// Where an opening of <paramref name="places"/> falls along its wall as it is now: the same point in plan when the wall is
    /// still straight and still runs through it (only its ends moved along it), else null (turned, bent: it goes with the start).
    /// </summary>
    public static double? AlongKept(Entity wall, Vec2 at)
    {
        if (PathCurve.Of(wall).IsArc) return null;
        var a = Walls.Start(wall);
        var d = Walls.End(wall) - a;
        if (d.Length < 1e-9 || Math.Abs(Vec2.Cross(d, at - a)) / d.Length > 1e-6) return null;
        return AlongOf(wall, at);
    }

    /// <summary>The openings of <see cref="PlacesOnRecognised"/> brought back to where they stood in plan; refused where one would no longer fit its wall.</summary>
    public static void KeepPlaces(IEdit edit, IEnumerable<(string Opening, string Wall, Vec2 At)> places)
    {
        foreach (var (openingId, wallId, at) in places)
        {
            if (edit.Model.Get(wallId) is not { } w || edit.Model.Get(openingId) is not { } opening) continue;
            if (AlongKept(w, at) is not { } along || Math.Abs(along - opening.Num("along")) < 1e-9) continue;
            var placed = Place(edit.Model, opening);
            CheckFits(edit.Model, w, along, placed.Sill, placed.Width, placed.Height, openingId);
            var attrs = opening.CloneAttrs();
            attrs["along"] = along;
            edit.Replace(opening.With(attrs: attrs));
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
        spec = WithDefaultMaterials(edit, spec);
        BayWindows.EnsureMaterials(edit, spec);
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
        BayWindows.EnsureMaterials(edit, spec);
        edit.Replace(type.With(name: spec.Name, attrs: TypeAttrs(spec)));
        foreach (var fill in edit.Model.RelatedOf(type.Id, IfcClass.RelDefinesByType).ToList())
            if (OpeningOf(edit.Model, fill.Id) is { } opening && WallOf(edit.Model, opening.Id) is { } wall)
                CheckFits(edit.Model, wall, opening.Num("along"), opening.Num("sill"), spec.Width, spec.Height, opening.Id);
            // A curtain wall's door: still in its cell (report #133).
            else CurtainCells.CheckDoor(edit.Model, fill);
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
        var marks = Numbering.Next(edit.Model.OfClass(cls));
        var fill = new Entity(edit.NewId(), cls, $"{spec.Mark}{marks}", new JsonObject { ["flipHand"] = false, ["flipSide"] = false }, spec.IsDoor ? "DOOR" : "WINDOW");
        edit.Add(fill);
        edit.AddRelation(new Relation(edit.NewId(), IfcClass.RelFillsElement, opening.Id, [fill.Id]));
        edit.Relate(IfcClass.RelDefinesByType, type.Id, fill.Id);
        if (Spatial.StoreyOf(edit.Model, wall.Id) is { } storey) Spatial.Contain(edit, storey.Id, fill.Id);
        return fill;
    }

    /// <summary>
    /// An opening alone (a tester's report, decision 247): a hole of its own width and height through the wall, at a
    /// sill, with nothing filling it (a passage, a hatch, a niche's place); an IfcOpeningElement voiding the wall, as a
    /// door's is. Refused when it does not fit.
    /// </summary>
    public static Entity AddBare(IEdit edit, Entity wall, double along, double sill, double width, double height, string? shape = null, double? rise = null)
    {
        CheckFits(edit.Model, wall, along, sill, width, height);
        var attrs = new JsonObject { ["along"] = along, ["sill"] = sill, ["width"] = width, ["height"] = height };
        SetShape(attrs, shape ?? "rect", rise);
        var opening = new Entity(edit.NewId(), IfcClass.OpeningElement, "Opening", attrs, "OPENING");
        edit.Add(opening);
        edit.AddRelation(new Relation(edit.NewId(), IfcClass.RelVoidsElement, wall.Id, [opening.Id]));
        return opening;
    }

    /// <summary>An opening alone resized (its width and height kept by itself; a door's or window's comes from its type), checked as it is placed.</summary>
    public static void Resize(IEdit edit, Entity opening, double? width, double? height)
    {
        if (FillOf(edit.Model, opening.Id) is { } fill) throw new CommandException($"„{fill.Name}” sets its opening's size: change its type.");
        var wall = WallOf(edit.Model, opening.Id) ?? throw new CommandException("The opening is in no wall.");
        var p = Place(edit.Model, opening);
        var (w, h) = (width ?? p.Width, height ?? p.Height);
        CheckFits(edit.Model, wall, p.Along, p.Sill, w, h, opening.Id);
        var attrs = opening.CloneAttrs();
        attrs["width"] = w;
        attrs["height"] = h;
        edit.Replace(opening.With(attrs: attrs));
    }

    /// <summary>
    /// A bare opening's head (decision 253): <paramref name="kind"/> one of <see cref="BareKinds"/> (null: kept),
    /// <paramref name="rise"/> the head's rise or a radiused head's corner radius (null with <paramref name="clearRise"/>: the kind's own).
    /// </summary>
    public static void Reshape(IEdit edit, Entity opening, string? kind, double? rise, bool clearRise)
    {
        if (FillOf(edit.Model, opening.Id) is { } fill) throw new CommandException($"„{fill.Name}” sets its opening's shape: change its type.");
        var attrs = opening.CloneAttrs();
        SetShape(attrs, kind ?? opening.Str("shape") ?? "rect", rise ?? (clearRise || kind != null && kind != opening.Str("shape") ? null : opening.NumOrNull("rise")));
        edit.Replace(opening.With(attrs: attrs));
    }

    /// <summary>The head's kind and rise into a bare opening's attributes, checked; a rectangle keeps neither.</summary>
    static void SetShape(JsonObject attrs, string kind, double? rise)
    {
        if (!BareKinds.Contains(kind)) throw new CommandException($"An opening's head is one of: {string.Join(", ", BareKinds)} (not „{kind}”).");
        if (rise is { } r && !(r > 0)) throw new CommandException("The head's rise must be more than zero.");
        attrs.Remove("shape");
        attrs.Remove("rise");
        if (kind == "rect") return;
        attrs["shape"] = kind;
        if (rise is { } v && kind != "round") attrs["rise"] = v;
    }

    /// <summary>Moves a door's, a window's (or a bare opening's) opening along its wall or up it.</summary>
    public static void Move(IEdit edit, Entity fillOrOpening, double? along, double? sill)
    {
        var opening = fillOrOpening.Class == IfcClass.OpeningElement ? fillOrOpening : OpeningOf(edit.Model, fillOrOpening.Id) ?? throw new CommandException("It fills no opening.");
        var wall = WallOf(edit.Model, opening.Id) ?? throw new CommandException("The opening is in no wall.");
        var placed = Place(edit.Model, opening);
        var a = along ?? placed.Along;
        // A door's or window's sill is its own: its opening's lies its offset under it (Openings.FillOffsetKey).
        var s = sill is { } given ? given - (fillOrOpening.Class == IfcClass.OpeningElement ? 0 : placed.FillOffset) : placed.Sill;
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
        // Another type: its opening is the type's size again, the file's own hole (an imported one's) left behind.
        if (OpeningOf(edit.Model, fill.Id) is { } own && (own.Attrs[HoleWidthKey] != null || own.Attrs[HoleHeightKey] != null || own.Attrs[FillOffsetKey] != null))
        {
            var attrs = own.CloneAttrs();
            // The door or window stays where it stood: its sill becomes the opening's.
            attrs["sill"] = own.Num("sill") + own.Num(FillOffsetKey);
            attrs.Remove(HoleWidthKey);
            attrs.Remove(HoleHeightKey);
            attrs.Remove(FillOffsetKey);
            edit.Replace(own.With(attrs: attrs));
        }
        edit.Relate(IfcClass.RelDefinesByType, type.Id, fill.Id);
        // A curtain wall's door takes its new type in its cell (report #133).
        CurtainCells.CheckDoor(edit.Model, edit.Model.Get(fill.Id)!);
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
        var d = new Dressing(m, wall, f, p, p.Spec, p.Fill.Bool("flipHand") ? -1 : 1, p.Fill.Bool("flipSide") ? -1 : 1, BayWindows.Projecting(p.Spec)).WithPlaced(p);
        o.AddRange(d.Cutters());
        return o;
    }

    /// <summary>The box an opening takes out of its wall, through the whole thickness and a little past it.</summary>
    public static Solid? Cutter(WallFrame f, Placed p)
    {
        // An arched or round door or window, or a bare opening with a shaped head (decision 253), takes its own outline out of the wall.
        var shaped = p.Spec ?? (p.Shape is { } bare ? BareSpec with { Shape = bare } : null);
        if (shaped is { } spec && JoineryShapes.Shaped(spec))
        {
            var sized = spec with { Width = p.Width, Height = p.Height };
            var near = f.YMin - 0.05;
            var outline = JoineryShapes.Outline(sized, 0, 0).Select(q => f.At(p.Along + q.X, near, p.Sill + q.Y)).ToList();
            return Prism.Extrude(outline, null, f.At(0, f.YMax + 0.05, 0) - f.At(0, near, 0), "opening");
        }
        double x0 = p.Along - p.Width / 2, x1 = p.Along + p.Width / 2, y0 = f.YMin - 0.05, y1 = f.YMax + 0.05;
        Vec2[] footprint = [(f.Start + f.Dir * x0 + f.Left * y0), (f.Start + f.Dir * x1 + f.Left * y0), (f.Start + f.Dir * x1 + f.Left * y1), (f.Start + f.Dir * x0 + f.Left * y1)];
        return Prism.ExtrudeFootprint(footprint, f.Base + p.Sill, p.Height, "opening");
    }

    static readonly Dictionary<string, string> HandleColor = new() { ["handle"] = "#5a5a5a", ["board"] = "#dcdcdc" };

    /// <summary>The id of the project's material a part of the type is made of (its role's), or null.</summary>
    public static string? MaterialOfPart(IModelView m, JoinerySpec spec, string role) =>
        spec.Materials?.Of(role) is { Length: > 0 } name ? Materials.Find(m, name.Trim())?.Id : null;

    /// <summary>A material by name, made in the colour and look given when the project has none; its name.</summary>
    public static string EnsureMaterial(IEdit edit, string name, string color, JsonObject? look = null)
    {
        if (Materials.Find(edit.Model, name) is { } found) return found.Name;
        var attrs = new JsonObject { ["color"] = color };
        foreach (var (k, v) in look ?? []) attrs[k] = v?.DeepClone();
        edit.Add(new Entity(edit.NewId(), IfcClass.Material, name, attrs));
        return name;
    }

    /// <summary>The look of the glass every new door, window and curtain wall shares.</summary>
    public static JsonObject GlassLook() => new() { ["roughness"] = 0.05, ["transparency"] = 0.6, ["transmission"] = 0.9, ["ior"] = 1.52 };

    /// <summary>
    /// The materials a new type is made with (report #76): one per part, shared by every type of its kind, so all window frames
    /// (or all glass) change at once in Manage → Materials and a renderer finds them by name. Made when the project has none,
    /// in the type's colours and with a look that suits them (glass clear, hardware metal).
    /// </summary>
    public static JoinerySpec WithDefaultMaterials(IEdit edit, JoinerySpec spec)
    {
        if (spec.Materials != null) return spec;
        string Make(string name, string color, JsonObject? look = null) => EnsureMaterial(edit, name, color, look);
        var door = spec.IsDoor;
        var frame = Make(door ? "Door frame" : "Window frame", spec.Colors.Frame, new JsonObject { ["roughness"] = 0.4 });
        var glass = Make("Glass", spec.Colors.Glass, GlassLook());
        var hardware = Make("Hardware", spec.Hardware?.Color ?? HandleColor["handle"], new JsonObject { ["roughness"] = 0.35, ["metalness"] = 1 });
        string? leaf = door ? Make("Door leaf", spec.Colors.Leaf, new JsonObject { ["roughness"] = 0.6, ["texture"] = "wood" }) : null;
        string? board = door ? null : Make("Window board", HandleColor["board"], new JsonObject { ["roughness"] = 0.5 });
        return spec with { Materials = new JoineryMaterials(frame, glass, leaf, hardware, board) };
    }

    /// <summary>
    /// A door's or window's body in its wall: the configurator's boxes, from
    /// the type frame (X along the opening, Y to the exterior, Z up) into the
    /// wall's, the exterior on the wall's right face; mirrored for a flipped
    /// hand (X) or side (Y). One part per role.
    /// </summary>
    /// <summary>
    /// What a bare wall opening is in the views (a tester's report, decision 312): no body (its wall is cut by it), but
    /// a hit area over the gap in plan and its outline on the wall's middle plane in 3D (drawn when selected), so it is
    /// clicked and edited as a door or a window is; its jambs' middles snap. An opening a door or window fills, or a
    /// floor's, has none.
    /// </summary>
    public static ElementGeometry? BareGeometry(Entity opening, IModelView m)
    {
        if (opening.Attrs["contour"] != null || FillOf(m, opening.Id) != null || WallOf(m, opening.Id) is not { Class: IfcClass.Wall } wall) return null;
        var p = Place(m, opening);
        var f = FrameOf(m, wall, p.Along);
        var (lo, hi) = (p.Along - p.Width / 2, p.Along + p.Width / 2);
        var plan = new List<Vec2> { f.At(lo, f.YMin, 0).Xy, f.At(hi, f.YMin, 0).Xy, f.At(hi, f.YMax, 0).Xy, f.At(lo, f.YMax, 0).Xy };
        var c = f.Centre;
        var outline = new List<Vec3> { f.At(lo, c, p.Sill), f.At(hi, c, p.Sill), f.At(hi, c, p.Sill + p.Height), f.At(lo, c, p.Sill + p.Height), f.At(lo, c, p.Sill) };
        return new ElementGeometry([], [new PlanItem("hit", true, plan)], [f.At(lo, c, 0).Xy, f.At(hi, c, 0).Xy, f.At(p.Along, c, 0).Xy], Lines: [outline], LineColor: "#b5532d");
    }

    public static ElementGeometry? FillGeometry(Entity fill, IModelView m)
    {
        // A curtain wall's door (report #133): in its cell, drawn as any door is.
        if (CurtainCells.SeatOf(m, fill) is { } seat) return CurtainDoorGeometry(fill, m, seat);
        var spec = SpecOf(m, fill.Id);
        var opening = OpeningOf(m, fill.Id);
        var wall = opening == null ? null : WallOf(m, opening.Id);
        if (spec == null || opening == null || wall == null) return new ElementGeometry([], Diagnostics: ["A door or window needs its type and its opening in a wall."]);
        var p = Place(m, opening);
        var f = FrameOf(m, wall, p.Along);
        var sx = fill.Bool("flipHand") ? -1 : 1;
        var sy = fill.Bool("flipSide") ? -1 : 1;
        // Type Y (+ to the exterior) is the wall's right: −left.
        Vec3 Map(double x, double y, double z) => f.At(p.Along + sx * x, f.Centre - sy * y, p.FillSill + z);

        var parts = new List<GeometryPart>();
        var bay = BayWindows.Projecting(spec);
        var dressing = PathCurve.Of(wall) is { IsArc: true } ? null : new Dressing(m, wall, f, p, spec, sx, sy, bay);
        if (bay) return BayGeometry(fill, m, spec, wall, f, p, sx, sy, dressing);
        parts.AddRange(BodyParts(m, spec, Joinery.Boxes(spec).Where(b => dressing?.Replaces(b) != true), Map));
        if (dressing != null) parts.AddRange(dressing.Parts());
        return new ElementGeometry(parts, Symbol(spec, f, p, sx, sy, dressing != null), [f.At(p.Along, f.Centre, 0).Xy]);
    }

    /// <summary>The joinery's boxes as parts, one per role, each in its material (by name, report #76) or the type's colour for the part.</summary>
    static IEnumerable<GeometryPart> BodyParts(IModelView m, JoinerySpec spec, IEnumerable<JoineryBox> boxes, Func<double, double, double, Vec3> map)
    {
        foreach (var group in boxes.GroupBy(b => b.Role))
        {
            var solids = group.Select(b => Box(b, map)).OfType<Solid>().ToList();
            if (solids.Count == 0) continue;
            var material = MaterialOfPart(m, spec, group.Key);
            yield return new GeometryPart(group.Key, RoleColor(m, spec, group.Key, material), Combine(solids, group.Key), material, group.Key);
        }
    }

    /// <summary>
    /// A curtain wall door's body (report #133): its type's joinery at the size it stands, in its cell's frame (the bay's chord, the
    /// wall's axis its middle plane), flipped as any door, with the plan symbol and swing; nothing, and why, where it cannot stand.
    /// </summary>
    static ElementGeometry CurtainDoorGeometry(Entity fill, IModelView m, CurtainCells.DoorSeat s)
    {
        if (s.Problem != null || s.Spec is not { } spec) return new ElementGeometry([], Diagnostics: [s.Problem ?? "A door needs its door type."]);
        var (f, p) = (s.Frame, s.Placed);
        var sx = fill.Bool("flipHand") ? -1 : 1;
        var sy = fill.Bool("flipSide") ? -1 : 1;
        Vec3 Map(double x, double y, double z) => f.At(p.Along + sx * x, f.Centre - sy * y, p.FillSill + z);
        return new ElementGeometry([.. BodyParts(m, spec, Joinery.Boxes(spec), Map)], Symbol(spec, f, p, sx, sy, false), [f.At(p.Along, f.Centre, 0).Xy]);
    }

    /// <summary>The colour of a part of a door's or window's body by its role: its material's, else the type's for the part.</summary>
    static string RoleColor(IModelView m, JoinerySpec spec, string role, string? material) => material != null ? Materials.ColorOf(m, material) : role switch
    {
        "glass" => spec.Colors.Glass, "leaf" => spec.Colors.Leaf,
        "handle" or "hardware" => spec.Hardware?.Color ?? HandleColor["handle"],
        _ => HandleColor.GetValueOrDefault(role, spec.Colors.Frame),
    };

    /// <summary>
    /// A bay, bow or box window's body (report #60): each face's joinery grouped by role as a flat window's is (one part per
    /// role, its material by the type's), the corner posts with the frames, then the seat, sill, base, roof and flashing as
    /// parts of their own with their materials, and the reveal's finish in the wall's opening.
    /// </summary>
    static ElementGeometry BayGeometry(Entity fill, IModelView m, JoinerySpec spec, Entity wall, WallFrame f, Placed p, int sx, int sy, Dressing? dressing)
    {
        Vec3 Map(double x, double y, double z) => f.At(p.Along + sx * x, f.Centre - sy * y, p.FillSill + z);
        var t = f.YMax - f.YMin;
        var zFloor = -(p.FillSill + wall.Num("baseOffset"));
        var faces = BayWindows.Faces(spec, t / 2);
        var parts = new List<GeometryPart>();
        foreach (var group in BayWindows.Pieces(spec, t, zFloor).GroupBy(x => x.Key))
        {
            var solids = group.Select(x => BayWindows.Make(x, faces, Map)).OfType<Solid>().ToList();
            if (solids.Count == 0) continue;
            var first = group.First();
            var named = first.Key.StartsWith("bay:", StringComparison.Ordinal);
            var material = named
                ? (first.Material is { Length: > 0 } name ? Materials.Find(m, name.Trim())?.Id : null) ?? (first.Key is "bay:side" or "bay:seat" ? MaterialOfPart(m, spec, first.Key == "bay:seat" ? "board" : "frame") : null)
                : MaterialOfPart(m, spec, first.Key);
            var fallback = first.Key switch
            {
                "bay:seat" => HandleColor["board"], "bay:sill" => "#b8b8b8", "bay:base" or "bay:corbels" => "#c9c3b6",
                "bay:head" => "#6f7378", "bay:flashing" => "#9a9fa6", "bay:side" => spec.Colors.Frame, _ => null,
            };
            var color = named ? (material != null ? Materials.ColorOf(m, material) : first.Material is { Length: > 0 } n2 ? Materials.DefaultColor(n2) : fallback!) : RoleColor(m, spec, first.Key, material);
            parts.Add(new GeometryPart(first.Name, color, Combine(solids, first.Key), material, first.Key));
        }
        if (dressing != null) parts.AddRange(dressing.Parts());
        Vec2 P(double x, double y) => f.Start + f.Dir * (p.Along + sx * x) + f.Left * (f.Centre - sy * y);
        var o = new List<PlanItem>();
        var half = spec.Width / 2;
        o.Add(new PlanItem("hit", true, [P(-half, -t / 2), P(half, -t / 2), P(half, t / 2), P(-half, t / 2)]));
        var line = BayWindows.Line(spec, t / 2);
        o.Add(new PlanItem("hit", true, [.. line.Select(q => P(q.X, q.Y))]));
        o.Add(new PlanItem("symbol", false, [P(-half, -t / 2), P(-half, t / 2)]));
        o.Add(new PlanItem("symbol", false, [P(half, -t / 2), P(half, t / 2)]));
        foreach (var (kind, closed, pts) in BayWindows.Plan(spec, t, zFloor, PlanCut - p.FillSill))
            o.Add(new PlanItem(kind, closed, [.. pts.Select(q => P(q.X, q.Y))]));
        return new ElementGeometry(parts, o, [f.At(p.Along, f.Centre, 0).Xy, .. line.Select(q => P(q.X, q.Y))]);
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

        public Dressing(IModelView m, Entity wall, WallFrame f, Placed p, JoinerySpec spec, int sx, int sy, bool revealOnly = false)
        {
            _m = m;
            // A bay, bow or box window (report #60) keeps only the reveal: its sills, seat and roof are its own.
            if (revealOnly) spec = spec with { Details = new JoineryDetails(Reveal: spec.Details?.Reveal) };
            _spec = spec;
            _d = spec.Details ?? new JoineryDetails();
            _map = (x, y, z) => f.At(p.Along + sx * x, f.Centre - sy * y, p.FillSill + z);
            _t = f.YMax - f.YMin;
            _yFE = spec.Frame.Offset + spec.Frame.Depth / 2;
            _yFI = spec.Frame.Offset - spec.Frame.Depth / 2;
            // A shaped opening's reveal stays the wall's own cut faces: a finish box would square its head again.
            _finish = JoineryShapes.Shaped(spec) ? [(0, null), (0, null)] : Finishes(m, wall, sy, _d.Reveal ?? new JoineryReveal());
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
            // A layer of no thickness (paint, a membrane; decision 291) lines nothing: the face's layer is the outermost with a thickness.
            var bands = Walls.Section(m, wall).Where(b => b.To - b.From > 1e-9).ToList();
            if (bands.Count < 2) return [(0, null), (0, null)];
            // The exterior is the wall's right face's layer, the left's when the side is flipped (by place: a flipped wall's first layer is on its left).
            var (right, left) = (bands.MinBy(b => b.From), bands.MaxBy(b => b.To));
            var (ext, inte) = sy > 0 ? (right, left) : (left, right);
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
        IReadOnlyList<(double, double)> OutsideSillProfile(JoinerySill sill, double past, bool cutter = false)
        {
            var yOut = YExt + Math.Max(0, sill.Projection) + past;
            var drop = (yOut - _yFE) * Math.Tan(Math.Clamp(sill.Slope, 0, 45) * Math.PI / 180);
            // What the wall gives up is the sill and the wedge over it, up to the opening's bottom: a sloped sill falls away from the
            // frame, and the wall above its top face (report #76) would stand proud of it.
            if (cutter && drop > 1e-9) return [(_yFE, 0), (yOut, 0), (yOut, -drop - sill.Thickness), (_yFE, -sill.Thickness)];
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
                if (JoineryShapes.Shaped(_spec))
                {
                    var shaped = new List<Solid>();
                    if (Openings.Box(SurroundOutline(_spec, w, YExt, YExt + dpt), _map) is { } arch) shaped.Add(arch);
                    if (sur.Sill && !_spec.IsDoor && !JoineryShapes.Round(_spec) && Box(-W / 2 - w, W / 2 + w, YExt, YExt + dpt, -w, 0, "surround") is { } band) shaped.Add(band);
                    if (shaped.Count > 0) yield return new GeometryPart("Surround", ColorOf(id, sur.Material, "#e0d8c8"), Combine(shaped, "surround"), id, "surround");
                }
                else
                {
                var solids = new[]
                {
                    Box(-W / 2 - w, -W / 2, YExt, YExt + dpt, z0, H + w, "surround"), Box(W / 2, W / 2 + w, YExt, YExt + dpt, z0, H + w, "surround"),
                    Box(-W / 2, W / 2, YExt, YExt + dpt, H, H + w, "surround"), sur.Sill && !_spec.IsDoor ? Box(-W / 2, W / 2, YExt, YExt + dpt, -w, 0, "surround") : null,
                }.OfType<Solid>().ToList();
                if (solids.Count > 0) yield return new GeometryPart("Surround", ColorOf(id, sur.Material, "#e0d8c8"), Combine(solids, "surround"), id, "surround");
                }
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
                var box = Swept(-W / 2 - sill.Overhang, W / 2 + sill.Overhang, OutsideSillProfile(sill, 0.05, true), "opening");
                if (box != null) yield return (Reach(sill.Thickness + (YExt + sill.Projection + 0.05 - _yFE) * Math.Tan(Math.Clamp(sill.Slope, 0, 45) * Math.PI / 180), 0), box);
            }

            // Round the fill (its opening may be the file's, larger: Openings.HoleWidthKey).
            Placed Reach(double below, double above) => _placed with { Sill = _placed.FillSill - below, Height = H + below + above, FillOffset = 0 };
        }

        Placed _placed;
        public Dressing WithPlaced(Placed p) { _placed = p; return this; }
    }

    /// <summary>A shaped opening's surround on the facade: a band <paramref name="w"/> wide round its outline (the jambs and the head; a round window's whole ring), from y0 to y1.</summary>
    public static JoineryBox SurroundOutline(JoinerySpec s, double w, double y0, double y1)
    {
        if (JoineryShapes.Round(s)) return JoineryShapes.Prism("surround", JoineryShapes.Ellipse(s, -w), y0, y1) with { Holes = [JoineryShapes.Ellipse(s, 0)] };
        var half = s.Width / 2;
        List<Vec2> pts = [new(half + w, 0), .. JoineryShapes.Outline(s, -w, 0).Skip(1).SkipLast(1), new(-half - w, 0), new(-half, 0), .. JoineryShapes.Outline(s, 0, 0).Skip(1).SkipLast(1).Reverse(), new(half, 0)];
        return JoineryShapes.Prism("surround", pts, y0, y1);
    }

    /// <summary>The details' measures for the schedules and quantities: the reveal's finished area each side, the sills' lengths, the surround's face, the threshold's length.</summary>
    public static IEnumerable<(string Name, double Value, string Unit)> DetailQuantities(IModelView m, Entity fill)
    {
        if (SpecOf(m, fill.Id) is not { } spec || OpeningOf(m, fill.Id) is not { } opening || WallOf(m, opening.Id) is not { } wall) yield break;
        var p = Place(m, opening);
        var f = FrameOf(m, wall, p.Along);
        // A bay, bow or box window's own measures (report #60).
        if (BayWindows.Projecting(spec)) foreach (var q in BayWindows.Quantities(spec, f.YMax - f.YMin)) yield return q;
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

    internal static Solid? Box(JoineryBox b, Func<double, double, double, Vec3> map)
    {
        // A library profile's real outline along the member (decision 417), through the box from end to end.
        if (b.Member is { } member)
        {
            Vec3 At(Vec2 q)
            {
                var (across, y) = member.On(b, q);
                return member.Upright ? map(across, y, b.Min.Z) : map(b.Min.X, y, across);
            }
            var sweep = member.Upright ? map(0, 0, b.Max.Z) - map(0, 0, b.Min.Z) : map(b.Max.X, 0, 0) - map(b.Min.X, 0, 0);
            return Prism.Extrude(member.Outline.Outer.Select(At).ToList(), member.Outline.Holes.Select(h => (IReadOnlyList<Vec3>)h.Select(At).ToList()).ToList(), sweep, b.Role);
        }
        // An outline (an arch, a slanting bar): its points mapped at the near face, swept to the far one.
        if (b.Profile is { Count: >= 3 } outline)
        {
            var d = map(0, b.Max.Y, 0) - map(0, b.Min.Y, 0);
            var holes = b.Holes?.Select(h => (IReadOnlyList<Vec3>)h.Select(q => map(q.X, b.Min.Y, q.Y)).ToList()).ToList();
            return Prism.Extrude(outline.Select(q => map(q.X, b.Min.Y, q.Y)).ToList(), holes, d, b.Role);
        }
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
        // The symbol engine's plan (docs/simboluri-tamplarie.md): the frame cut, the glass, a door's leaves and their arcs; once
        // for each level a view may draw it at (decision 405: LOD 100 the opening and its swing, 200 simplified, 300 and up whole).
        foreach (var level in new[] { PlanDetail.Coarse, PlanDetail.Medium, PlanDetail.Fine })
            foreach (var st in JoinerySymbols.Plan(spec, level: level))
                o.Add(new PlanItem(st.Weight == "cut" ? "heavy" : st.Dashed ? "dashed" : "symbol", st.Closed, st.Pts.Select(q => P(q.X, q.Y)).ToList(), level, level));
        // The dressings seen in plan: what crosses the cut (shutters, a grille) cut, a window box below it seen, a canopy above it overhead.
        if (dressed)
            foreach (var (kind, x0, x1, y0, y1) in DressingPlan(spec, depth, PlanCut - p.FillSill))
            o.Add(new PlanItem(kind, true, [P(x0, y0), P(x1, y0), P(x1, y1), P(x0, y1)], PlanDetail.Fine));
        return o;
    }
}

/// <summary>The default door and window types a project starts with.</summary>
public static class JoinerySpecs
{
    public static JoinerySpec Door => JoinerySpec.DefaultDoor("Door 900×2100");
    public static JoinerySpec Window => JoinerySpec.DefaultWindow("Window 1200×1500");

    /// <summary>
    /// A double door (decision 466): two leaves of 700, no mullion between them, a rebated meeting, the right leaf active (seen
    /// from the side it is pushed open from, the leaf most used in Romania and the rest of Europe), flush bolts on the left one.
    /// </summary>
    public static JoinerySpec DoubleDoor => JoinerySpec.DefaultDoor("Double door 1500×2100") with
    {
        Mark = "DD", Width = 1.5, Columns = [1, 1], Panels = [new(0, 0, "swing-left"), new(1, 0, "swing-right")], Mullions = [0], Meeting = new JoineryMeeting(),
    };

    /// <summary>
    /// A frameless glass door on a floor spring (topic #28): one 1000 × 2100 leaf of 12 mm toughened glass, double acting, the
    /// spring the lightest that carries it, the pivot 55 mm in, a top patch fitting and a bottom one, held open at 90°.
    /// </summary>
    /// <summary>
    /// A garage door's frame: a steel angle 40 mm on the face, 80 mm deep, hung behind the opening (its inner face on the wall's
    /// interior face, decision 530), no threshold; steel grey, a white curtain.
    /// </summary>
    static JoinerySpec Garage(string name, string mark, double width, double height, string op, JoineryGarage garage) => JoinerySpec.DefaultDoor(name) with
    {
        Mark = mark, Width = width, Height = height, Columns = [1], Rows = [1], Panels = [new(0, 0, op)], Threshold = 0, IsExternal = true,
        Frame = new JoineryFrame(0.08, 0.04, 0.06, 0.06, 0), Colors = new JoineryColors("#8c9096", "#9cc8e8", "#e9e9e6"),
        Position = new JoineryPosition("interior", 0), Hardware = new JoineryHardware("lever"), Garage = garage,
    };

    /// <summary>A sectional overhead garage door (topic #48: a steel sectional panel lift door), 2400 × 2100 as Australia's standard size; ribbed panels, a standard track.</summary>
    public static JoinerySpec SectionalGarageDoor => Garage("Sectional garage door 2400×2100", "GD", 2.4, 2.1, "sectional-overhead", new JoineryGarage());

    /// <summary>A roller garage door, 2400 × 2100, its roll box inside.</summary>
    public static JoinerySpec RollerGarageDoor => Garage("Roller garage door 2400×2100", "GD", 2.4, 2.1, "roller", new JoineryGarage(Finish: "flat"));

    /// <summary>An up-and-over (tilting) garage door, 2500 × 2125 as Europe's common size; canopy, vertical ribs.</summary>
    public static JoinerySpec UpAndOverGarageDoor => Garage("Up-and-over garage door 2500×2125", "GD", 2.5, 2.125, "up-and-over", new JoineryGarage());

    /// <summary>A side-sliding sectional garage door, 2500 × 2125, running to the left.</summary>
    public static JoinerySpec SideSlidingGarageDoor => Garage("Side-sliding garage door 2500×2125", "GD", 2.5, 2.125, "sectional-side-left", new JoineryGarage(Finish: "v-groove"));

    /// <summary>Side-hinged garage doors: a pair of leaves, 2400 × 2100, meeting rebated, no mullion (a double door the size of a garage's).</summary>
    public static JoinerySpec SideHingedGarageDoors => DoubleDoor with
    {
        Name = "Side-hinged garage doors 2400×2100", Mark = "GD", Width = 2.4, Height = 2.1, Threshold = 0, IsExternal = true,
        Frame = new JoineryFrame(0.09, 0.06, 0.06, 0.06, 0), Sash = new JoinerySash(0.1, 0.06, 0.045, 0.024),
    };

    public static JoinerySpec FloorSpringDoor => JoinerySpec.DefaultDoor("Frameless door on floor spring 1000×2100") with
    {
        Mark = "GD", Width = 1.0, Columns = [1], Rows = [1], Panels = [new(0, 0, "double-acting-left")], Threshold = 0, Style = "frameless-glass",
        Frameless = new JoineryFrameless("floor-spring", HoldOpen: 90),
    };
}
