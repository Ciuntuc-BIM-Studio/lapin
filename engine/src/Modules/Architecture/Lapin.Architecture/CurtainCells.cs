using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>
/// A curtain panel type (report #133): what fills a cell of a curtain wall, saved once and given to chosen cells of any wall.
/// <see cref="Kind"/> "glass" or "solid" (a sheet <see cref="Thickness"/> thick), "framed" (a frame <see cref="FrameWidth"/> wide
/// and <see cref="FrameDepth"/> deep round the cell, its <see cref="Infill"/> sheet set <see cref="Inset"/> back from the frame's
/// outer face), or "empty" (the cell left open). <see cref="Offset"/>: the panel's centre across the wall, metres, + out of the
/// building. <see cref="Material"/> the infill's and <see cref="FrameMaterial"/> the frame's, by name; null: the curtain wall
/// type's glass, opaque or frame material.
/// </summary>
public sealed record CurtainPanelSpec(string Kind = "glass", double Thickness = 0.024, double Offset = 0, double FrameWidth = 0.05, double FrameDepth = 0.06,
    double Inset = 0.01, string Infill = "glass", string? Material = null, string? FrameMaterial = null)
{
    public static readonly string[] Kinds = ["glass", "solid", "framed", "empty"];
    public static readonly string[] Infills = ["glass", "solid"];

    /// <summary>What fills the cell: the sheet's kind, glass or solid (a framed panel's infill); null when it is empty.</summary>
    public string? Sheet => Kind == "empty" ? null : Kind == "framed" ? Infill : Kind;

    public static CurtainPanelSpec From(JsonNode? n, CurtainPanelSpec? fallback = null)
    {
        var d = fallback ?? new CurtainPanelSpec();
        if (n is not JsonObject o) return d;
        static string? Named(JsonNode? x) => x is JsonValue v && v.TryGetValue<string>(out var t) && t.Trim().Length > 0 ? t.Trim() : null;
        return new CurtainPanelSpec(
            (string?)o["kind"] is { } k && Kinds.Contains(k) ? k : d.Kind,
            Json.Number(o["thickness"]) ?? d.Thickness,
            Json.Number(o["offset"]) ?? d.Offset,
            Json.Number(o["frameWidth"]) ?? d.FrameWidth,
            Json.Number(o["frameDepth"]) ?? d.FrameDepth,
            Json.Number(o["inset"]) ?? d.Inset,
            (string?)o["infill"] is { } i && Infills.Contains(i) ? i : d.Infill,
            o.ContainsKey("material") ? Named(o["material"]) : d.Material,
            o.ContainsKey("frameMaterial") ? Named(o["frameMaterial"]) : d.FrameMaterial);
    }

    public JsonObject ToJson()
    {
        var o = new JsonObject { ["kind"] = Kind, ["thickness"] = Thickness, ["offset"] = Offset };
        if (Kind == "framed") { o["frameWidth"] = FrameWidth; o["frameDepth"] = FrameDepth; o["inset"] = Inset; o["infill"] = Infill; }
        if (Material != null) o["material"] = Material;
        if (FrameMaterial != null && Kind == "framed") o["frameMaterial"] = FrameMaterial;
        return o;
    }

    /// <summary>Why the panel cannot be built (null: it can).</summary>
    public string? Problem()
    {
        static bool Ok(double v) => double.IsFinite(v);
        if (Kind == "empty") return null;
        if (!(Thickness > 0) || Thickness > 0.5) return "A panel is more than nothing and at most 500 mm thick.";
        if (!Ok(Offset) || Math.Abs(Offset) > 0.5) return "A panel stands at most 500 mm out of the curtain wall's axis.";
        if (Kind == "framed")
        {
            if (!(FrameWidth > 0) || FrameWidth > 0.5 || !(FrameDepth > 0) || FrameDepth > 0.5) return "A panel's frame needs a width and a depth (at most 500 mm).";
            if (!(Inset >= 0) || Inset + Thickness > FrameDepth + 1e-9) return "The infill and its inset are deeper than the panel's frame.";
        }
        return null;
    }
}

/// <summary>
/// A curtain wall's cells (report #133, a tester's request, as Revit's curtain panels): each cell (its panel's key,
/// <c>panel:i:j</c>, the bay along and the row up) takes a panel type of its own (<see cref="PanelTypeClass"/>, kept in the
/// wall's <see cref="PanelTypesKey"/> by key, or by <c>panel</c> for every cell), or is replaced by a curtain wall door: an
/// IfcDoor of a door type, part of the wall (IfcRelAggregates) and on its storey, knowing its cell (<c>cell</c>), whether it
/// fills it (<c>fit</c>: its width and height the cell's, else its type's) and where it stands in it (<c>align</c> left, center
/// or right, <c>offset</c> from there inwards), with <c>flipHand</c> and <c>flipSide</c> as any door. A door smaller than its
/// cell has a transom over it and a mullion beside it where glass is left, the cell's own panel round it; one in the bottom row
/// stands on the floor, the sill transom cut away under it. The settings are by key, so a changed grid keeps them: a cell that
/// is gone keeps its own until it is back, and its door says so meanwhile.
/// </summary>
public static class CurtainCells
{
    public const string PanelTypeClass = "IfcPlateType";
    public const string PanelTypesKey = "panelTypes";
    public const string CellKey = "cell";
    public static readonly string[] Aligns = ["left", "center", "right"];

    public static void Register(IModuleRegistry r)
    {
        r.Class(new(PanelTypeClass, "Curtain panel type", [], "#9cc3e6", Kind: "type"));
        r.Service<IDeletion>(new CurtainDeletion());
        r.Service<IElementPlacement>(new CurtainWallPlacement());
        r.Service<IHostedPlacement>(new CurtainDoorPlacement());
        r.Catalog("curtainPanelDefault", new CurtainPanelSpec().ToJson());

        r.Command("createCurtainPanelType", "New curtain panel type", (ctx, a) =>
        {
            var spec = CheckedPanel(a["panel"], null);
            var name = ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : $"Curtain panel {Numbering.Next(PanelTypes(ctx.Model))}";
            var type = new Entity(ctx.NewId(), PanelTypeClass, name, new JsonObject { ["panel"] = spec.ToJson() }, "CURTAIN_PANEL");
            ctx.Add(type);
            return new JsonObject { ["id"] = type.Id };
        });

        r.Command("updateCurtainPanelType", "Change curtain panel type", (ctx, a) =>
        {
            var type = PanelType(ctx.Model, (string?)a["id"]) ?? throw new CommandException("There is no such curtain panel type.");
            var attrs = type.CloneAttrs();
            if (a["panel"] != null) attrs["panel"] = CheckedPanel(a["panel"], CurtainPanelSpec.From(type.Attrs["panel"])).ToJson();
            ctx.Replace(type.With(name: ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : null, attrs: attrs));
            return null;
        });

        // {id, keys, typeId | clear}: the cells chosen — by key (panel:1:0) or every one (panel) — take the panel type, or go back to the wall type's.
        r.Command("setCurtainPanels", "Curtain wall panels", (ctx, a) =>
        {
            var w = Spatial.Require(ctx.Model, (string?)a["id"], CurtainWalls.Class);
            var keys = KeysOf(a);
            var (_, _, layout) = CurtainWalls.LayoutOf(ctx.Model, w);
            var known = layout.Pieces.Where(p => p.Role == "panel").Select(p => p.Key).Append("panel").ToHashSet();
            if (keys.FirstOrDefault(k => !known.Contains(k)) is { } bad) throw new CommandException($"The curtain wall has no cell „{bad}”.");
            var clear = a["clear"] is JsonValue cv && cv.TryGetValue<bool>(out var c) && c;
            var typeId = clear ? null : PanelType(ctx.Model, (string?)a["typeId"])?.Id ?? throw new CommandException("Which curtain panel type?");
            var attrs = w.CloneAttrs();
            var own = attrs[PanelTypesKey] as JsonObject ?? new JsonObject();
            foreach (var k in keys)
            {
                own.Remove(k);
                if (typeId != null) own[k] = typeId;
            }
            if (own.Count > 0) attrs[PanelTypesKey] = own; else attrs.Remove(PanelTypesKey);
            ctx.Replace(w.With(attrs: attrs));
            return new JsonObject { ["cells"] = keys.Count };
        });

        // A door type made for curtain walls: {leaves 1 | 2, operation swing | sliding, width, height, glazed (default true), frameSection?, name?}.
        r.Command("createCurtainDoorType", "New curtain wall door type", (ctx, a) => new JsonObject { ["id"] = CreateDoorType(ctx, a).Id });

        // {id (the curtain wall), keys (its cells), typeId? | door? (a new type's createCurtainDoorType terms), fit?, align?, offset?}:
        // a door in each cell, its panel replaced; one undo step.
        r.Command("addCurtainDoor", "Curtain wall door", (ctx, a) =>
        {
            var w = Spatial.Require(ctx.Model, (string?)a["id"], CurtainWalls.Class);
            var keys = KeysOf(a);
            var type = (string?)a["typeId"] is { } tid
                ? ctx.Model.Get(tid) is { Class: IfcClass.DoorType } t ? t : throw new CommandException("There is no such door type.")
                : a["door"] is JsonObject terms ? CreateDoorType(ctx, terms) : EnsureDoorType(ctx);
            var fit = a["fit"] is JsonValue fv && fv.TryGetValue<bool>(out var f) ? f : a["typeId"] == null && a["door"] == null;
            var spec = DoorSpecOf(type);
            var made = new JsonArray();
            foreach (var key in keys)
            {
                var attrs = new JsonObject { [CellKey] = key, ["fit"] = fit, ["flipHand"] = false, ["flipSide"] = false };
                SetPlace(attrs, a);
                var door = new Entity(ctx.NewId(), IfcClass.Door, $"{spec.Mark}{Numbering.Next(ctx.Model.OfClass(IfcClass.Door))}", attrs, "DOOR");
                ctx.Add(door);
                ctx.Relate(IfcClass.RelDefinesByType, type.Id, door.Id);
                if (Spatial.StoreyOf(ctx.Model, w.Id) is { } storey) Spatial.Contain(ctx, storey.Id, door.Id);
                ctx.Relate(IfcClass.RelAggregates, w.Id, door.Id);
                Check(ctx.Model, ctx.Model.Get(door.Id)!);
                made.Add((JsonNode)door.Id);
            }
            return new JsonObject { ["ids"] = made };
        });

        // {id | ids (curtain wall doors), key?, fit?, align?, offset?}: another cell, or its size and place in its own.
        r.Command("updateCurtainDoor", "Change curtain wall door", (ctx, a) =>
        {
            var ids = Args.Ids(a);
            if (ids.Count == 0) throw new CommandException("Which door?");
            foreach (var id in ids)
            {
                var door = ctx.Model.Get(id) is { } d && WallOf(ctx.Model, d) != null ? d : throw new CommandException("That is not a curtain wall door.");
                var attrs = door.CloneAttrs();
                if ((string?)a["key"] is { Length: > 0 } key) attrs[CellKey] = key;
                if (a["fit"] is JsonValue fv && fv.TryGetValue<bool>(out var fit)) attrs["fit"] = fit;
                SetPlace(attrs, a);
                ctx.Replace(door.With(attrs: attrs));
                Check(ctx.Model, ctx.Model.Get(id)!);
            }
            return null;
        });

        // The cells of a curtain wall for the dialog that gives them panels and doors: each cell (its key, place, outline, panel type
        // and door), the pieces as built (round its doors too), and its doors with their place and what is wrong with one.
        r.Query("curtainCells", (q, a) =>
        {
            var m = q.Model;
            var w = Spatial.Require(m, (string?)a["id"], CurtainWalls.Class);
            var (spec, c, layout) = CurtainWalls.LayoutOf(m, w);
            var own = w.Attrs[PanelTypesKey] as JsonObject;
            var seats = DoorsOf(m, w).Select(d => SeatIn(m, w, spec, layout, c, d)).ToList();
            double U(CurtainPiece p, double x)
            {
                var chord = (c.PointAt(layout.U[p.I1]) - c.PointAt(layout.U[p.I0])).Length;
                return layout.U[p.I0] + x * (layout.U[p.I1] - layout.U[p.I0]) / Math.Max(chord, 1e-9);
            }
            JsonObject Rect(CurtainPiece p)
            {
                var (x0, x1) = p.Role == "mullion" && p.I0 == p.I1 ? (layout.U[p.I0] - p.A0, layout.U[p.I0] + p.A1) : (layout.U[p.I0] + p.A0, layout.U[p.I1] - p.A1);
                if (p.I0 != p.I1) { var chord = (c.PointAt(layout.U[p.I1]) - c.PointAt(layout.U[p.I0])).Length; (x0, x1) = (U(p, p.A0), U(p, chord - p.A1)); }
                var o = new JsonObject { ["key"] = p.Key, ["role"] = p.Role, ["x0"] = x0, ["x1"] = x1, ["z0"] = p.Z0, ["z1"] = p.Z1 };
                if (p.Outline != null) o["outline"] = new JsonArray(p.Outline.Select(v => (JsonNode)new JsonArray(v.X, v.Y)).ToArray());
                return o;
            }
            var built = Built(m, w, spec, layout, c, null);
            return new JsonObject
            {
                ["length"] = c.Length, ["height"] = layout.V[^1],
                ["cells"] = new JsonArray(layout.Pieces.Where(p => p.Role == "panel").Select(p =>
                {
                    var o = Rect(p);
                    var (_, type) = PanelOf(m, w, p.Key);
                    o["panelType"] = type?.Id;
                    o["own"] = own?[p.Key] != null;
                    o["door"] = seats.FirstOrDefault(s => s.Key == p.Key)?.Door.Id;
                    return (JsonNode)o;
                }).ToArray()),
                ["pieces"] = new JsonArray(built.Pieces.Select(p => (JsonNode)Rect(p)).ToArray()),
                ["doors"] = new JsonArray(seats.Select(s =>
                {
                    var o = new JsonObject
                    {
                        ["id"] = s.Door.Id, ["name"] = s.Door.Name, ["key"] = s.Key, ["problem"] = s.Problem,
                        ["fit"] = s.Door.Bool("fit"), ["align"] = s.Door.Str("align") ?? "center", ["offset"] = s.Door.Num("offset"),
                        ["type"] = m.RelatingOf(s.Door.Id, IfcClass.RelDefinesByType)?.Id,
                    };
                    if (s.Problem == null)
                    {
                        o["x0"] = U(s.Cell, s.X0); o["x1"] = U(s.Cell, s.X1); o["z0"] = s.Placed.Sill; o["z1"] = s.Placed.Sill + s.Placed.Height;
                        o["width"] = s.Placed.Width; o["height"] = s.Placed.Height;
                    }
                    return (JsonNode)o;
                }).ToArray()),
            };
        });
    }

    static List<string> KeysOf(JsonObject a)
    {
        var keys = (a["keys"] as JsonArray)?.Select(x => (string?)x).OfType<string>().Distinct().ToList() ?? [];
        if ((string?)a["key"] is { Length: > 0 } one && !keys.Contains(one)) keys.Add(one);
        if (keys.Count == 0) throw new CommandException("Which cells?");
        return keys;
    }

    static void SetPlace(JsonObject attrs, JsonObject a)
    {
        if ((string?)a["align"] is { } al)
        {
            if (!Aligns.Contains(al)) throw new CommandException($"A door aligns {string.Join(", ", Aligns)} in its cell.");
            if (al == "center") attrs.Remove("align"); else attrs["align"] = al;
        }
        if (Json.Number(a["offset"]) is { } off)
        {
            if (!double.IsFinite(off)) throw new CommandException("An offset is a length.");
            if (Math.Abs(off) < 1e-9) attrs.Remove("offset"); else attrs["offset"] = off;
        }
    }

    static CurtainPanelSpec CheckedPanel(JsonNode? n, CurtainPanelSpec? fallback)
    {
        var spec = CurtainPanelSpec.From(n, fallback);
        if (spec.Problem() is { } p) throw new CommandException(p);
        return spec;
    }

    static JoinerySpec DoorSpecOf(Entity type) => JoinerySpec.From(type.Attrs["spec"]) is { IsDoor: true } s ? s : throw new CommandException($"„{type.Name}” is not a door type with a configuration.");

    public static IEnumerable<Entity> PanelTypes(IModelView m) => m.OfClass(PanelTypeClass).Where(t => t.Attrs["panel"] is JsonObject);

    static Entity? PanelType(IModelView m, string? id) => id != null && m.Get(id) is { Class: PanelTypeClass } t && t.Attrs["panel"] is JsonObject ? t : null;

    /// <summary>The cell a piece belongs to: a panel's key itself, a piece round a door its cell's (<c>panel:1:0:head</c> → <c>panel:1:0</c>).</summary>
    public static string CellKeyOf(string key)
    {
        var parts = key.Split(':');
        return parts.Length > 3 ? string.Join(':', parts.Take(3)) : key;
    }

    /// <summary>The panel type a cell takes: its own (by its key), else the wall's for every cell; null: the curtain wall type's panel.</summary>
    public static (CurtainPanelSpec? Spec, Entity? Type) PanelOf(IModelView m, Entity w, string cellKey)
    {
        if (w.Attrs[PanelTypesKey] is not JsonObject own) return (null, null);
        var id = (string?)own[cellKey] ?? (string?)own["panel"];
        return PanelType(m, id) is { } t ? (CurtainPanelSpec.From(t.Attrs["panel"]), t) : (null, null);
    }

    // ------------------------------------------------------------ doors

    /// <summary>The curtain wall a door is part of; null for any other door.</summary>
    public static Entity? WallOf(IModelView m, Entity door) =>
        door.Class == IfcClass.Door && m.RelatingOf(door.Id, IfcClass.RelAggregates) is { Class: CurtainWalls.Class } w ? w : null;

    public static List<Entity> DoorsOf(IModelView m, Entity w) => m.RelatedOf(w.Id, IfcClass.RelAggregates).Where(e => e.Class == IfcClass.Door).ToList();

    /// <summary>
    /// Where a curtain wall door stands: its cell (<see cref="Key"/>, the panel piece), its place along the bay's chord from the
    /// bay's first grid line (<see cref="X0"/>..<see cref="X1"/>; the cell's <see cref="CellX0"/>..<see cref="CellX1"/>), its
    /// type's spec at the size it stands (<see cref="Spec"/>), the frame and place the joinery body and symbol are drawn in;
    /// <see cref="Problem"/> why it cannot stand there (its cell gone, too small, a strip too narrow beside it).
    /// </summary>
    public sealed record DoorSeat(Entity Door, Entity Wall, string Key, CurtainPiece Cell, JoinerySpec? Spec, Openings.WallFrame Frame, Openings.Placed Placed,
        double Chord, double CellX0, double CellX1, double X0, double X1, bool Bottom, string? Problem);

    /// <summary>A curtain wall door's seat; null for a door that is no curtain wall's.</summary>
    public static DoorSeat? SeatOf(IModelView m, Entity door)
    {
        if (WallOf(m, door) is not { } w) return null;
        var (spec, c, layout) = CurtainWalls.LayoutOf(m, w);
        return SeatIn(m, w, spec, layout, c, door);
    }

    static void Check(IModelView m, Entity door)
    {
        if (SeatOf(m, door) is not { } s) return;
        if (s.Problem != null) throw new CommandException(s.Problem);
        if (DoorsOf(m, s.Wall).Any(o => o.Id != door.Id && o.Str(CellKey) == s.Key)) throw new CommandException($"The cell {s.Key} already has a door.");
    }

    /// <summary>A door's seat checked as a command leaves it (its type changed, the type itself changed): refused when it no longer fits.</summary>
    public static void CheckDoor(IModelView m, Entity door) => Check(m, door);

    internal static DoorSeat SeatIn(IModelView m, Entity w, CurtainSpec spec, CurtainLayout l, PathCurve c, Entity door)
    {
        var key = door.Str(CellKey) ?? "";
        var cell = l.Pieces.FirstOrDefault(p => p.Role == "panel" && p.Key == key);
        var baseZ = CurtainWalls.Base(m, w);
        DoorSeat Fail(string why, CurtainPiece at = default) => new(door, w, key, at, null, default, default, 0, 0, 0, 0, 0, false, why);
        if (cell.Key == null) return Fail($"Its cell ({key}) is no longer in the curtain wall's grid.");
        if (cell.Outline != null || key.Split(':') is not [_, _, var row] || !int.TryParse(row, NumberStyles.Integer, CultureInfo.InvariantCulture, out var j))
            return Fail("A door needs a rectangular cell, not one under the curtain wall's boundary.", cell);
        if (m.RelatingOf(door.Id, IfcClass.RelDefinesByType) is not { } type || JoinerySpec.From(type.Attrs["spec"]) is not { IsDoor: true } ts) return Fail("A door needs its door type.", cell);
        var (pa, pb) = (c.PointAt(l.U[cell.I0]), c.PointAt(l.U[cell.I1]));
        var chord = (pb - pa).Length;
        var d = (pb - pa).Normalized();
        var (cx0, cx1) = (cell.A0, chord - cell.A1);
        // In the bottom row it stands on the floor: the sill transom is cut away under it.
        var bottom = j == 0;
        var z0 = bottom ? 0 : cell.Z0;
        var fit = door.Bool("fit");
        var (width, height) = fit ? (cx1 - cx0, cell.Z1 - z0) : (ts.Width, ts.Height);
        var off = door.Num("offset");
        var x0 = fit ? cx0 : door.Str("align") switch { "left" => cx0 + off, "right" => cx1 - width - off, _ => (cx0 + cx1 - width) / 2 + off };
        var (x1, z1) = (x0 + width, z0 + height);
        static string Mm(double v) => (v * 1000).ToString("0", CultureInfo.InvariantCulture);
        if (width < 0.4 - 1e-9 || height < 1.0 - 1e-9) return Fail($"A door needs at least 400 mm of width and 1000 mm of height: {Mm(width)} × {Mm(height)} mm is too small for one.", cell);
        if (x0 < cx0 - 1e-6 || x1 > cx1 + 1e-6) return Fail($"The door, {Mm(width)} mm wide, does not fit in its cell ({Mm(cx1 - cx0)} mm) there.", cell);
        if (z1 > cell.Z1 + 1e-6) return Fail($"The door, {Mm(height)} mm high, is taller than its cell ({Mm(cell.Z1 - z0)} mm).", cell);
        var (side, top) = (spec.Mullion.Width + 0.05, spec.Transom.Width + 0.05);
        bool Strip(double gap, double min) => gap > Gap && gap < min - 1e-9;
        if (Strip(x0 - cx0, side) || Strip(cx1 - x1, side)) return Fail($"The door leaves a strip too narrow for glass beside it: leave at least {Mm(side)} mm, or none.", cell);
        if (Strip(cell.Z1 - z1, top)) return Fail($"The door leaves a strip too narrow for glass over it: leave at least {Mm(top)} mm, or none.", cell);
        var sized = fit ? ts with { Width = width, Height = height } : ts;
        if (Joinery.Problem(sized) is { } problem) return Fail(problem, cell);
        var frame = new Openings.WallFrame(pa, d, d.Perp, baseZ, -spec.Mullion.Depth / 2, spec.Mullion.Depth / 2);
        var placed = new Openings.Placed(door, door, sized, (x0 + x1) / 2, z0, width, height);
        return new DoorSeat(door, w, key, cell, sized, frame, placed, chord, cx0, cx1, x0, x1, bottom, null);
    }

    /// <summary>A gap under a millimetre is none: the door meets the cell's edge.</summary>
    const double Gap = 1e-3;

    /// <summary>
    /// The layout as built with its doors: each door's cell without its panel (and in the bottom row the sill transom under it),
    /// a transom over a door lower than its cell with the glass above, a mullion and a pane beside one narrower; those
    /// <see cref="CurtainPiece.Plain"/>. A door that cannot stand says why in <paramref name="problems"/> and leaves its cell glazed.
    /// </summary>
    internal static CurtainLayout Built(IModelView m, Entity w, CurtainSpec spec, CurtainLayout l, PathCurve c, List<string>? problems)
    {
        var doors = DoorsOf(m, w);
        if (doors.Count == 0) return l;
        var pieces = l.Pieces.ToList();
        var taken = new HashSet<string>();
        foreach (var door in doors.OrderBy(x => m.Order(x.Id)))
        {
            var s = SeatIn(m, w, spec, l, c, door);
            if (s.Problem != null) { problems?.Add($"{door.Name}: {s.Problem}"); continue; }
            if (!taken.Add(s.Key)) { problems?.Add($"{door.Name}: The cell {s.Key} already has a door."); continue; }
            pieces.RemoveAll(p => p.Key == s.Key);
            var cell = s.Cell;
            var parts = s.Key.Split(':');
            var (i, j) = (parts[1], parts[2]);
            var (mw, tw) = (spec.Mullion.Width, spec.Transom.Width);
            var (z0, z1) = (s.Placed.Sill, s.Placed.Sill + s.Placed.Height);
            var (left, right, over) = (s.X0 - s.CellX0 > Gap, s.CellX1 - s.X1 > Gap, cell.Z1 - z1 > Gap);
            CurtainPiece Piece(string role, string k, double x0, double x1, double za, double zb, double depth) =>
                new(role, k, cell.I0, cell.I1, x0, s.Chord - x1, za, zb, depth, null, true);
            if (over)
            {
                pieces.Add(Piece("transom", $"transom:{i}:{j}:head", s.CellX0, s.CellX1, z1, z1 + tw, spec.Transom.Depth));
                if (cell.Z1 - (z1 + tw) > 0.02) pieces.Add(Piece("panel", $"panel:{i}:{j}:head", s.CellX0, s.CellX1, z1 + tw, cell.Z1, spec.PanelThickness));
            }
            if (left)
            {
                pieces.Add(Piece("mullion", $"mullion:{i}:{j}:left", s.X0 - mw, s.X0, z0, z1, spec.Mullion.Depth));
                pieces.Add(Piece("panel", $"panel:{i}:{j}:left", s.CellX0, s.X0 - mw, cell.Z0, z1, spec.PanelThickness));
            }
            if (right)
            {
                pieces.Add(Piece("mullion", $"mullion:{i}:{j}:right", s.X1, s.X1 + mw, z0, z1, spec.Mullion.Depth));
                pieces.Add(Piece("panel", $"panel:{i}:{j}:right", s.X1 + mw, s.CellX1, cell.Z0, z1, spec.PanelThickness));
            }
            // The sill transom under a door in the bottom row is cut away; what is left of it beside the door stays.
            if (s.Bottom && pieces.FindIndex(p => p.Key == $"transom:{i}:0") is var at and >= 0)
            {
                var sill = pieces[at];
                pieces.RemoveAt(at);
                if (left) pieces.Add(Piece("transom", $"transom:{i}:0:left", s.CellX0, s.X0 - mw, sill.Z0, sill.Z1, sill.Depth));
                if (right) pieces.Add(Piece("transom", $"transom:{i}:0:right", s.X1 + mw, s.CellX1, sill.Z0, sill.Z1, sill.Depth));
            }
        }
        return l with { Pieces = pieces };
    }

    /// <summary>The project's door type for curtain walls (<c>curtainDoor</c>), made when it has none: a glazed aluminium door, one leaf.</summary>
    static Entity EnsureDoorType(ICommandContext ctx) =>
        ctx.Model.OfClass(IfcClass.DoorType).FirstOrDefault(t => t.Bool("curtainDoor")) ?? CreateDoorType(ctx, new JsonObject());

    static Entity CreateDoorType(ICommandContext ctx, JsonObject a)
    {
        var leaves = (int)(Json.Number(a["leaves"]) ?? 1);
        if (leaves is not (1 or 2)) throw new CommandException("A curtain wall door has one leaf or two.");
        var operation = (string?)a["operation"] ?? "swing";
        if (operation is not ("swing" or "sliding")) throw new CommandException("A curtain wall door swings or slides.");
        var width = Json.Number(a["width"]) ?? (leaves == 2 ? 1.8 : 1.0);
        var height = Json.Number(a["height"]) ?? 2.2;
        var glazed = a["glazed"] is not JsonValue gv || !gv.TryGetValue<bool>(out var g) || g;
        var (frame, sash, profile) = Joinery.ProfileSystems["aluminium"];
        var ops = (operation, leaves) switch
        {
            ("sliding", 2) => new[] { "door-sliding-left", "door-sliding-right" },
            ("sliding", _) => ["door-sliding-left"],
            (_, 2) => ["swing-left", "swing-right"],
            _ => ["swing-left"],
        };
        static string Mm(double v) => (v * 1000).ToString("0", CultureInfo.InvariantCulture);
        var name = ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : $"Curtain wall door {Mm(width)}×{Mm(height)}";
        var section = a["frameSection"] as JsonObject;
        var spec = JoinerySpec.DefaultDoor(name) with
        {
            Width = width, Height = height,
            Frame = frame with { Offset = 0 }, Sash = sash, Profile = profile,
            Columns = [.. Enumerable.Repeat(1.0, leaves)], Rows = [1],
            Panels = [.. ops.Select((op, i) => new JoineryPanel(i, 0, op, Glazed: glazed, Infill: glazed ? "glass" : "solid"))],
            FrameSection = section?.DeepClone() as JsonObject,
            Colors = new JoineryColors("#3f3f46", "#9cc3e6", "#c8a27a"),
            Hardware = new JoineryHardware("pull"),
            IsExternal = true,
        };
        var type = Openings.CreateType(ctx, NormalisedSpec(spec));
        var attrs = type.CloneAttrs();
        attrs["curtainDoor"] = true;
        ctx.Replace(type.With(attrs: attrs));
        return ctx.Model.Get(type.Id)!;
    }

    /// <summary>A spec read back as it is kept (a profile's sizes become the frame's, as <see cref="JoinerySpec.From"/> does).</summary>
    static JoinerySpec NormalisedSpec(JoinerySpec s) => JoinerySpec.From(s.ToJson()) ?? s;

    // ------------------------------------------------------------ placement, deletion

    /// <summary>A curtain wall deleted takes its doors with it.</summary>
    sealed class CurtainDeletion : IDeletion
    {
        public bool Delete(ICommandContext ctx, string id)
        {
            if (ctx.Model.Get(id) is not { Class: CurtainWalls.Class } w) return false;
            var doors = DoorsOf(ctx.Model, w);
            if (doors.Count == 0) return false;
            foreach (var d in doors) ctx.Remove(d.Id);
            ctx.Remove(w.Id);
            return true;
        }
    }

    /// <summary>A curtain wall moved, turned or mirrored as the platform does it (its doors follow from their cells); a copy takes its doors.</summary>
    sealed class CurtainWallPlacement : IElementPlacement
    {
        public bool Handles(Entity e) => e.Class == CurtainWalls.Class;

        public void Transform(ICommandContext ctx, Entity e, Placement2D t, IReadOnlySet<string> moving) => Placement.Own(ctx, ctx.Model.Get(e.Id) ?? e, t, moving);

        public string? Copy(ICommandContext ctx, Entity e, Placement2D t)
        {
            var id = Placement.DeepCopy(ctx, e, t);
            foreach (var d in DoorsOf(ctx.Model, e)) CopyDoor(ctx, d, id, d.Str(CellKey)!);
            return id;
        }
    }

    static string CopyDoor(ICommandContext ctx, Entity d, string wallId, string key)
    {
        var m = ctx.Model;
        var type = m.RelatingOf(d.Id, IfcClass.RelDefinesByType);
        var mark = type != null && JoinerySpec.From(type.Attrs["spec"]) is { } spec ? spec.Mark : "";
        var attrs = d.CloneAttrs();
        attrs[CellKey] = key;
        var copy = new Entity(ctx.NewId(), IfcClass.Door, mark.Length > 0 ? $"{mark}{Numbering.Next(m.OfClass(IfcClass.Door))}" : d.Name, attrs, d.PredefinedType);
        ctx.Add(copy);
        if (type != null) ctx.Relate(IfcClass.RelDefinesByType, type.Id, copy.Id);
        if (Spatial.StoreyOf(m, wallId) is { } storey) Spatial.Contain(ctx, storey.Id, copy.Id);
        ctx.Relate(IfcClass.RelAggregates, wallId, copy.Id);
        return copy.Id;
    }

    /// <summary>
    /// A curtain wall door moved or copied on its own (as a door along its wall, report #82): it goes to the cell of its row the
    /// transformation takes its middle into; refused off the wall or where a door already is.
    /// </summary>
    sealed class CurtainDoorPlacement : IHostedPlacement
    {
        public bool Handles(IModelView m, Entity e) => WallOf(m, e) != null;

        public string? HostOf(IModelView m, Entity e) => WallOf(m, e)?.Id;

        public IReadOnlyList<string> Transform(ICommandContext ctx, IReadOnlyList<Entity> elements, Placement2D t, bool copy)
        {
            var made = new List<string>();
            var moves = new List<(Entity Door, string Key)>();
            foreach (var door in elements)
            {
                var s = SeatOf(ctx.Model, door) ?? throw new CommandException("That is not a curtain wall door.");
                if (s.Problem != null) throw new CommandException($"{door.Name}: {s.Problem}");
                var (spec, c, layout) = CurtainWalls.LayoutOf(ctx.Model, s.Wall);
                var to = t.Point(s.Frame.At(s.Placed.Along, 0, 0).Xy);
                var along = c.IsArc ? c.Project(to) : Openings.AlongOf(s.Wall, to);
                var i = Enumerable.Range(0, layout.U.Count - 1).Where(k => along >= layout.U[k] - 1e-9 && along < layout.U[k + 1] + 1e-9).Cast<int?>().FirstOrDefault()
                    ?? throw new CommandException($"{door.Name} would land off its curtain wall.");
                var key = $"panel:{i}:{s.Key.Split(':')[2]}";
                moves.Add((door, key));
            }
            foreach (var (door, key) in moves)
            {
                if (copy)
                {
                    var id = CopyDoor(ctx, door, WallOf(ctx.Model, door)!.Id, key);
                    Check(ctx.Model, ctx.Model.Get(id)!);
                    made.Add(id);
                    continue;
                }
                var now = ctx.Model.Get(door.Id)!;
                var attrs = now.CloneAttrs();
                attrs[CellKey] = key;
                ctx.Replace(now.With(attrs: attrs));
            }
            if (!copy) foreach (var (door, _) in moves) Check(ctx.Model, ctx.Model.Get(door.Id)!);
            return made;
        }
    }
}
