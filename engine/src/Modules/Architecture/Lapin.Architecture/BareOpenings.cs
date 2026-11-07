using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>
/// A wall opening edited like a door or a window (a tester's report #114, decision 392): besides its size, place, sill and head
/// (decisions 247, 253), its jambs and head finished (<c>reveal</c>: none, plaster or a lining, its thickness and material) and its
/// lintel (<c>lintel</c>: the wall's rule, or none); several changed at once; an opening made a door or a window of a type (keeping its
/// place, its sill and its GlobalId; <c>fit</c>: a type of its size and head, the project's or a new one), and a door or a window
/// made an opening again of its hole. IFC has no type for openings, so all of it is the opening's own; it is written as before,
/// an IfcOpeningElement voiding the wall, nothing filling it; the lining is a part of the wall's body.
/// </summary>
public static class BareOpenings
{
    public static readonly string[] RevealKinds = ["none", "plaster", "lining"];
    public static readonly string[] LintelKinds = ["wall", "none"];

    /// <summary>A reveal's thickness and material when none is given: plaster 15 mm, a timber lining 20 mm.</summary>
    public static double DefaultThickness(string kind) => kind == "lining" ? 0.02 : 0.015;
    public static (string Name, string Color) DefaultMaterial(string kind) => kind == "lining" ? ("Timber lining", "#b98b5a") : ("Plaster", "#ece8df");

    /// <summary>The reveal of a bare opening: its kind, thickness and material name (null: none).</summary>
    public static (string Kind, double Thickness, string Material)? RevealOf(Entity opening) =>
        opening.Str("reveal") is { } k && k != "none" && RevealKinds.Contains(k)
            ? (k, opening.NumOrNull("revealThickness") ?? DefaultThickness(k), opening.Str("revealMaterial") ?? DefaultMaterial(k).Name)
            : null;

    /// <summary>Whether a bare opening asks for no lintel over it.</summary>
    public static bool NoLintel(Openings.Placed p) => p.Fill == null && p.Opening.Str("lintel") == "none";

    /// <summary>
    /// The finish on a bare opening's jambs and head (and under it when it stands above the floor), through the wall's thickness,
    /// inside its hole: a straight wall's only. <paramref name="along"/> is the opening's middle in the frame's own measure.
    /// </summary>
    public static GeometryPart? Lining(IModelView m, Openings.Placed p, Openings.WallFrame f, double along)
    {
        if (p.Fill != null || RevealOf(p.Opening) is not { } reveal) return null;
        var t = reveal.Thickness;
        if (!(t > 0) || 2 * t >= p.Width || t >= p.Height) return null;
        var spec = JoinerySpec.DefaultWindow("Opening") with { Width = p.Width, Height = p.Height, Shape = p.Shape };
        var bottom = p.Sill > 1e-6 ? t : 0;
        var outer = JoineryShapes.Outline(spec, 0, 0);
        var near = f.YMin;
        Vec3 At(Vec2 q) => f.At(along + q.X, near, p.Sill + q.Y);
        var depth = f.At(0, f.YMax, 0) - f.At(0, near, 0);
        Solid? solid;
        if (JoineryShapes.Round(spec) || bottom > 0)
        {
            // A ring: the hole's outline with the clear opening as its hole.
            var inner = JoineryShapes.Outline(spec, t, bottom);
            solid = Prism.Extrude(outer.Select(At).ToList(), [inner.Select(At).ToList()], depth, "lining");
        }
        else
        {
            // On the floor: the jambs and the head only, a U from the bottom right round to the bottom left and back inside.
            var inner = JoineryShapes.Outline(spec, t, 0);
            solid = Prism.Extrude([.. outer.Select(At), .. Enumerable.Reverse(inner).Select(At)], null, depth, "lining");
        }
        if (solid == null) return null;
        var material = Materials.Find(m, reveal.Material);
        var color = material != null ? Materials.ColorOf(m, material.Id) : DefaultMaterial(reveal.Kind).Color;
        return new GeometryPart(reveal.Kind == "lining" ? "Opening lining" : "Opening reveal", color, solid, material?.Id, "lining:" + p.Opening.Id);
    }

    /// <summary>
    /// Bare openings changed, one undo step: {id | ids, width?, height?, along?, sill?, shape?, rise? (null: the kind's own),
    /// reveal?, revealThickness? (null: the kind's own), revealMaterial?, lintel?}. Each is checked against its wall.
    /// </summary>
    public static void Update(ICommandContext ctx, JsonObject a)
    {
        var ids = Args.Ids(a).ToList();
        if (ids.Count == 0) throw new CommandException("Select a wall opening.");
        foreach (var id in ids)
        {
            var opening = Spatial.Require(ctx.Model, id, IfcClass.OpeningElement);
            if (a["width"] != null || a["height"] != null) Openings.Resize(ctx, opening, Json.Number(a["width"]), Json.Number(a["height"]));
            if (a["along"] != null || a["sill"] != null) Openings.Move(ctx, ctx.Model.Get(id)!, Json.Number(a["along"]), Json.Number(a["sill"]));
            if (a["shape"] != null || a.ContainsKey("rise")) Openings.Reshape(ctx, ctx.Model.Get(id)!, (string?)a["shape"], Json.Number(a["rise"]), a.ContainsKey("rise") && a["rise"] == null);
            if (a.ContainsKey("reveal") || a.ContainsKey("revealThickness") || a.ContainsKey("revealMaterial") || a.ContainsKey("lintel")) Finish(ctx, ctx.Model.Get(id)!, a);
        }
    }

    static void Finish(ICommandContext ctx, Entity opening, JsonObject a)
    {
        if (Openings.FillOf(ctx.Model, opening.Id) is { } fill) throw new CommandException($"„{fill.Name}” sets its opening's finish: change its type.");
        var attrs = opening.CloneAttrs();
        if (a.ContainsKey("reveal"))
        {
            var kind = (string?)a["reveal"] ?? "none";
            if (!RevealKinds.Contains(kind)) throw new CommandException($"An opening's reveal is one of: {string.Join(", ", RevealKinds)} (not „{kind}”).");
            if (kind == "none") { attrs.Remove("reveal"); attrs.Remove("revealThickness"); attrs.Remove("revealMaterial"); }
            else
            {
                // Another kind takes its own thickness and material unless they are given.
                if (opening.Str("reveal") != kind) { attrs.Remove("revealThickness"); attrs.Remove("revealMaterial"); }
                attrs["reveal"] = kind;
            }
        }
        if (a.ContainsKey("revealThickness"))
        {
            if (Json.Number(a["revealThickness"]) is { } t)
            {
                if (!(t > 0) || t > 0.2) throw new CommandException("A reveal's finish is more than 0 and at most 200 mm thick.");
                attrs["revealThickness"] = t;
            }
            else attrs.Remove("revealThickness");
        }
        if (a.ContainsKey("revealMaterial"))
        {
            if (((string?)a["revealMaterial"])?.Trim() is { Length: > 0 } name) attrs["revealMaterial"] = name;
            else attrs.Remove("revealMaterial");
        }
        if (a.ContainsKey("lintel"))
        {
            var l = (string?)a["lintel"] ?? "wall";
            if (!LintelKinds.Contains(l)) throw new CommandException("An opening's lintel is the wall's (wall) or none.");
            if (l == "wall") attrs.Remove("lintel"); else attrs["lintel"] = l;
        }
        var next = opening.With(attrs: attrs);
        if (RevealOf(next) is { } reveal)
        {
            var p = Openings.Place(ctx.Model, next);
            if (2 * reveal.Thickness >= p.Width || reveal.Thickness >= p.Height) throw new CommandException($"A finish {reveal.Thickness * 1000:0} mm thick leaves no opening {p.Width * 1000:0} mm wide.");
            var (name, color) = DefaultMaterial(reveal.Kind);
            Openings.EnsureMaterial(ctx, reveal.Material, reveal.Material == name ? color : "#d8d2c6");
        }
        ctx.Replace(next);
    }

    /// <summary>The attributes only a bare opening has (its size, head, finish and lintel), dropped when a door or a window fills it.</summary>
    static readonly string[] BareKeys = ["width", "height", "shape", "rise", "reveal", "revealThickness", "revealMaterial", "lintel"];

    /// <summary>
    /// The opening filled by a door or a window of <paramref name="type"/>, where it is and at its sill, the opening's GlobalId kept.
    /// <paramref name="fit"/>: the type of the opening's size and head (the project's own of that name, else made from it, named by its size).
    /// </summary>
    public static Entity ToFill(ICommandContext ctx, Entity opening, Entity type, bool fit)
    {
        if (Openings.FillOf(ctx.Model, opening.Id) is { } filled) throw new CommandException($"„{filled.Name}” already fills this opening.");
        var wall = Openings.WallOf(ctx.Model, opening.Id) is { Class: IfcClass.Wall } w ? w : throw new CommandException("The opening is in no wall.");
        var spec = JoinerySpec.From(type.Attrs["spec"]) ?? throw new CommandException($"„{type.Name}” has no configuration.");
        var p = Openings.Place(ctx.Model, opening);
        if (fit) type = Fitted(ctx, type, spec, p.Width, p.Height, p.Shape);
        spec = JoinerySpec.From(type.Attrs["spec"])!;
        Openings.CheckFits(ctx.Model, wall, p.Along, p.Sill, spec.Width, spec.Height, opening.Id);
        var attrs = opening.CloneAttrs();
        foreach (var k in BareKeys) attrs.Remove(k);
        ctx.Replace(opening.With(attrs: attrs));
        var cls = spec.IsDoor ? IfcClass.Door : IfcClass.Window;
        var fill = new Entity(ctx.NewId(), cls, $"{spec.Mark}{Numbering.Next(ctx.Model.OfClass(cls))}", new JsonObject { ["flipHand"] = false, ["flipSide"] = false }, spec.IsDoor ? "DOOR" : "WINDOW");
        ctx.Add(fill);
        ctx.AddRelation(new Relation(ctx.NewId(), IfcClass.RelFillsElement, opening.Id, [fill.Id]));
        ctx.Relate(IfcClass.RelDefinesByType, type.Id, fill.Id);
        if (Spatial.StoreyOf(ctx.Model, wall.Id) is { } storey) Spatial.Contain(ctx, storey.Id, fill.Id);
        return fill;
    }

    /// <summary>A type like <paramref name="type"/> of the given size and head: itself when it is already, the project's of the size's name, else a new one.</summary>
    static Entity Fitted(ICommandContext ctx, Entity type, JoinerySpec spec, double width, double height, JoineryShape? shape)
    {
        bool Same(JoinerySpec s) => Math.Abs(s.Width - width) < 1e-6 && Math.Abs(s.Height - height) < 1e-6 && JoineryShapes.KindOf(s) == (shape?.Kind ?? "rect")
            && (shape?.Rise is not { } r || s.Shape?.Rise is { } sr && Math.Abs(sr - r) < 1e-6);
        if (Same(spec)) return type;
        var size = $"{width * 1000:0}×{height * 1000:0}";
        var name = Regex.IsMatch(spec.Name, @"\d+\s*[×x]\s*\d+") ? Regex.Replace(spec.Name, @"\d+\s*[×x]\s*\d+", size) : $"{spec.Name} {size}";
        if (ctx.Model.OfClass(type.Class).FirstOrDefault(t => t.Name == name && JoinerySpec.From(t.Attrs["spec"]) is { } s && Same(s)) is { } own) return own;
        var made = spec with { Name = name, Width = width, Height = height, Shape = shape ?? (JoineryShapes.Shaped(spec) ? null : spec.Shape) };
        return Openings.CreateType(ctx, made);
    }

    /// <summary>A door or a window taken out of its opening, which stays where it was, its hole's size and head its own (its GlobalId kept).</summary>
    public static Entity FromFill(ICommandContext ctx, Entity fill)
    {
        var opening = Openings.OpeningOf(ctx.Model, fill.Id) ?? throw new CommandException($"„{fill.Name}” fills no opening.");
        if (Openings.WallOf(ctx.Model, opening.Id) is not { Class: IfcClass.Wall }) throw new CommandException("Only a door or a window in a wall becomes a wall opening.");
        var p = Openings.Place(ctx.Model, opening);
        var attrs = opening.CloneAttrs();
        foreach (var k in new[] { Openings.HoleWidthKey, Openings.HoleHeightKey, Openings.FillOffsetKey }) attrs.Remove(k);
        attrs["sill"] = p.Sill;
        attrs["width"] = p.Width;
        attrs["height"] = p.Height;
        // The head goes with it where an opening has the kind (a pointed or a triangular head becomes a rectangle).
        if (p.Shape is { } s && Openings.BareKinds.Contains(s.Kind) && s.Kind != "rect")
        {
            attrs["shape"] = s.Kind;
            if (s.Rise is { } r && s.Kind != "round") attrs["rise"] = r;
        }
        // Its fill goes, not its opening (removing a fill takes the opening it fills).
        ctx.Unrelate(IfcClass.RelFillsElement, fill.Id);
        ctx.Replace(opening.With(attrs: attrs));
        ctx.Remove(fill.Id);
        return ctx.Model.Get(opening.Id)!;
    }

    public static void Register(IModuleRegistry r)
    {
        // A wall opening made a door or a window: {id, typeId | kind, fit?}; the door's or window's id and its type back.
        r.Command("openingToFill", "Change to door or window", (ctx, a) =>
        {
            var opening = Spatial.Require(ctx.Model, (string?)a["id"], IfcClass.OpeningElement);
            // A type, or a kind (door, window) whose first type is taken, made when the project has none.
            var type = ctx.Model.Get((string?)a["typeId"] ?? "") is { Class: IfcClass.DoorType or IfcClass.WindowType } t ? t
                : (string?)a["kind"] is "door" or "window" ? Openings.EnsureType(ctx, (string)a["kind"]!) : throw new CommandException("Choose a door or window type.");
            var fill = ToFill(ctx, opening, type, a["fit"] is JsonValue v && v.TryGetValue<bool>(out var fit) && fit);
            return new JsonObject { ["id"] = fill.Id, ["typeId"] = ctx.Model.RelatingOf(fill.Id, IfcClass.RelDefinesByType)?.Id };
        });

        // Doors or windows made wall openings of their holes: {id | ids}; the openings' ids back.
        r.Command("fillToOpening", "Change to opening", (ctx, a) =>
        {
            var ids = Args.Ids(a).ToList();
            if (ids.Count == 0) throw new CommandException("Select a door or a window.");
            var made = ids.Select(id => ctx.Model.Get(id) is { Class: IfcClass.Door or IfcClass.Window } f ? FromFill(ctx, f).Id : throw new CommandException("Select a door or a window.")).ToList();
            return new JsonObject { ["ids"] = new JsonArray([.. made.Select(x => (JsonNode)x)]) };
        });
    }
}
