using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture.Balconies;

/// <summary>
/// Balconies (a tester's report, #96; docs/user/stairs.md, Balconies): a <c>LapinBalcony</c> on a wall's face at its
/// storey (or drawn by its outline, or along a gallery's edge), configured as roofs are — a built-in style (Juliet,
/// cantilevered, stacked on posts, on brackets, hung, rounded, curved, loggia, corner, mezzanine) or the project's own
/// (a <c>LapinBalconyType</c>, which carries the cost articles), every field then changed by hand. A loggia cuts its
/// recess into its wall (an opening, <c>balconyId</c>) and stands its own back wall there. In IFC a balcony is an
/// IfcElementAssembly USERDEFINED "Balcony" of IfcSlab FLOOR, IfcCovering, IfcRailing GUARDRAIL (or an IfcWall PARAPET),
/// IfcColumn, IfcDiscreteAccessory BRACKET and IfcMember parts, typed by an IfcElementAssemblyType, with its
/// <c>Lapin_Intent</c>, read back native.
/// </summary>
public static class BalconyModule
{
    public const string Class = "LapinBalcony";
    public const string TypeClass = "LapinBalconyType";

    public static void Register(IModuleRegistry r)
    {
        r.Class(new(Class, "Balcony", [.. BalconySpec.StyleFields, .. BalconySpec.PlaceFields], "#b8b4ac"));
        r.Class(new(TypeClass, "Balcony type", [], "#b8b4ac", Kind: "type"));
        r.Geometry(Class, (e, m, _) => BalconyBuilder.Geometry(e, m));
        r.Quantities(Class, (e, _, m) => BalconyBuilder.Quantities(e, m));
        r.Service<IDeletion>(new BalconyDeletion());
        r.Service<IElementUpdate>(new BalconyUpdate());
        r.Service<IElementPlacement>(new BalconyPlacement());
        r.Service<ITransformFollowers>(new BalconyFollowers());
        r.Service<IStoreyCopy>(new BalconyCopies());
        r.Service<INodeProps>(new BalconyFrames());
        r.Service<IScheduleSource>(new BalconySchedule());
        r.Catalog("balconyStyles", new JsonArray([.. BalconySpec.Styles.Select(s => (JsonNode)new JsonObject
        {
            ["id"] = s.Id, ["label"] = s.Label, ["description"] = s.Description, ["kind"] = (string?)s.Fields()["kind"], ["attrs"] = s.Fields(),
        })]));

        // A balcony: {storeyId, style? (a built-in style or a type's id), wallId?, x, y} on the wall clicked (else the one nearest, else
        // where clicked, turned by rotation); {contour: [[x, y]…]} drawn (its first edge against the building); {x1, y1, x2, y2} along a
        // line (a gallery's edge: the gallery to its right); attrs: any field.
        r.Command("addBalcony", "Balcony", (ctx, a) => new JsonObject { ["id"] = Add(ctx, a).Id });

        // {id, style?, attrs?, x?, y? (slid along its wall, or moved), contour?}.
        r.Command("updateBalcony", "Change balcony", (ctx, a) => { Update(ctx, a); return null; });

        // The balcony as configured saved as a project style (a type): a new one under a name, or the one it names saved again,
        // which every balcony of it takes again.
        r.Command("saveBalconyStyle", "Save balcony style", (ctx, a) =>
        {
            var b = Update(ctx, a);
            var name = ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : throw new CommandException("A balcony style needs a name.");
            var fields = StyleOf(b);
            var type = (string?)a["styleId"] is { } sid ? RequireType(ctx.Model, sid)
                : ctx.Model.OfClass(TypeClass).FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
            if (type == null)
            {
                type = new Entity(ctx.NewId(), TypeClass, name, fields, "USERDEFINED");
                ctx.Add(type);
            }
            else
            {
                var ta = type.CloneAttrs();
                foreach (var (k, v) in fields) ta[k] = v?.DeepClone();
                ta.Remove("library");
                type = type.With(name: name, attrs: ta);
                ctx.Replace(type);
            }
            ctx.Relate(IfcClass.RelDefinesByType, type.Id, b.Id);
            foreach (var other in ctx.Model.RelatedOf(type.Id, IfcClass.RelDefinesByType).Where(x => x.Class == Class && x.Id != b.Id).ToList())
            {
                var oa = other.CloneAttrs();
                foreach (var (k, v) in fields) oa[k] = v?.DeepClone();
                Settle(ctx, other.With(attrs: oa));
            }
            return new JsonObject { ["styleId"] = type.Id };
        });

        r.Command("renameBalconyStyle", "Rename balcony style", (ctx, a) =>
        {
            var type = RequireType(ctx.Model, (string?)a["styleId"]);
            var name = ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : throw new CommandException("A balcony style needs a name.");
            ctx.Replace(type.With(name: name));
            return null;
        });

        // A style deleted: its balconies keep how they are, with no type.
        r.Command("deleteBalconyStyle", "Delete balcony style", (ctx, a) =>
        {
            var type = RequireType(ctx.Model, (string?)a["styleId"]);
            ctx.Remove(type.Id);
            return null;
        });

        // Stacked: the balcony copied onto the storeys given (by default every storey above its own), with Copy to storeys, one step.
        r.Command("stackBalcony", "Stack balconies", (ctx, a) =>
        {
            var ids = Args.Ids(a).Select(id => ctx.Model.Get(id) is { Class: Class } b ? b : throw new CommandException("Select the balconies to stack.")).ToList();
            if (ids.Count == 0) throw new CommandException("Select the balconies to stack.");
            var storeys = a["storeyIds"] as JsonArray;
            if (storeys == null)
            {
                var lowest = ids.Min(b => Spatial.StoreyOf(ctx.Model, b.Id)?.Num("elevation") ?? 0);
                storeys = new JsonArray([.. Spatial.Storeys(ctx.Model).Where(s => s.Num("elevation") > lowest + 1e-6).Select(s => (JsonNode)s.Id)]);
            }
            if (storeys.Count == 0) throw new CommandException("There is no storey above to stack the balcony on.");
            var catalog = ctx.Services as ModuleCatalog ?? throw new CommandException("Copy to storeys is not here.");
            var copy = catalog.Command("element.copyToStoreys") ?? throw new CommandException("Copy to storeys is not here.");
            return copy.Handler(ctx, new JsonObject { ["ids"] = new JsonArray([.. ids.Select(b => (JsonNode)b.Id)]), ["storeyIds"] = storeys.DeepClone(), ["hosted"] = true });
        });

        // The configurator's preview: {id, style?, attrs?} → the balcony as it would be (its plan and section in its frame, a summary, its fields).
        r.Query("balconyPreview", (q, a) =>
        {
            var b = q.Model.Get((string?)a["id"] ?? "") is { Class: Class } x ? x : throw new CommandException("Not a balcony.");
            var fields = Draft(q.Model, b, (string?)a["style"], a["attrs"] as JsonObject);
            var draft = b.With(attrs: Merge(b.CloneAttrs(), fields));
            if (BalconyBuilder.Frame(q.Model, draft) is not { } f) return new JsonObject { ["problem"] = "The balcony has no place." };
            var l = BalconyBuilder.Build(q.Model, draft, f, fields);
            var o = BalconyBuilder.Describe(q.Model, f, l);
            o["attrs"] = fields;
            o["problem"] = l.Parts.Count == 0 ? l.Notes.FirstOrDefault() ?? "The balcony has nothing to build." : null;
            return o;
        });
    }

    static JsonObject Merge(JsonObject into, JsonObject fields) { foreach (var (k, v) in fields) into[k] = v?.DeepClone(); return into; }

    /// <summary>The style fields of a balcony.</summary>
    static JsonObject StyleOf(Entity b)
    {
        var all = BalconySpec.Read(b);
        var o = new JsonObject();
        foreach (var k in BalconySpec.Keys) o[k] = all[k]?.DeepClone();
        return o;
    }

    public static Entity RequireType(IModelView m, string? id) =>
        m.Get(id ?? "") is { Class: TypeClass } t ? t : throw new CommandException("The project has no such balcony style.");

    /// <summary>The fields a style gives: a type's (the project's), or a built-in one's; null for neither.</summary>
    static JsonObject? FieldsOfStyle(IModelView m, string? style)
    {
        if (style == null) return null;
        if (m.Get(style) is { Class: TypeClass } t) return BalconySpec.Read(t);
        return BalconySpec.StyleOf(style)?.Fields();
    }

    /// <summary>A balcony's fields as a draft would make them: its own, then the style's, then the fields given.</summary>
    static JsonObject Draft(IModelView m, Entity b, string? style, JsonObject? attrs)
    {
        var fields = FieldsOfStyle(m, style) ?? BalconySpec.Read(b);
        if (style != null && FieldsOfStyle(m, style) == null) throw new CommandException($"There is no balcony style „{style}”.");
        if (attrs != null)
        {
            var clean = BalconySpec.Clean(attrs);
            // The kind changed without saying what carries it: what that kind is carried by.
            if (clean["kind"] is { } k && (string?)k != (string?)fields["kind"] && clean["support"] == null) clean["support"] = BalconySpec.SupportOf((string?)k ?? "");
            Merge(fields, clean);
        }
        return fields;
    }

    /// <summary>The project's copy of a built-in style, made the first time it is used (marked <c>library</c>).</summary>
    public static Entity LibraryType(ICommandContext ctx, string id)
    {
        var style = BalconySpec.StyleOf(id) ?? throw new CommandException($"There is no balcony style „{id}”.");
        if (ctx.Model.OfClass(TypeClass).FirstOrDefault(t => t.Str("library") == id) is { } have) return have;
        var attrs = style.Fields();
        attrs["library"] = id;
        BalconySpec.EnsureMaterials(ctx, attrs);
        var type = new Entity(ctx.NewId(), TypeClass, style.Label, attrs, "USERDEFINED");
        ctx.Add(type);
        return type;
    }

    static Entity TypeFor(ICommandContext ctx, string style) =>
        ctx.Model.Get(style) is { Class: TypeClass } t ? t : LibraryType(ctx, style);

    // ------------------------------------------------------------ placing

    /// <summary>The wall nearest a point on a storey, within reach of its face: the wall, where along it, which side.</summary>
    public static (Entity Wall, double Along, int Side)? WallAt(IModelView m, string storeyId, Vec2 p, double reach = 1.0)
    {
        (Entity, double, int)? best = null;
        var bestD = double.MaxValue;
        foreach (var w in m.OfClass(IfcClass.Wall))
        {
            if (Spatial.StoreyOf(m, w.Id)?.Id != storeyId || w.Str("balconyId") != null) continue;
            var bands = Walls.Section(m, w);
            if (bands.Count == 0) continue;
            var c = PathCurve.Of(w);
            var along = Openings.AlongOf(w, p);
            if (along < -1e-6 || along > c.Length + 1e-6) continue;
            var f = Openings.FrameOf(m, w, along);
            var y = Vec2.Dot(p - (f.Start + f.Dir * along), f.Left);
            var half = Math.Max(Math.Abs(bands.Min(b => b.From)), Math.Abs(bands.Max(b => b.To)));
            var d = Math.Abs(y);
            if (d > half + reach || d >= bestD) continue;
            bestD = d;
            best = (w, along, y >= 0 ? 1 : -1);
        }
        return best;
    }

    static Entity Add(ICommandContext ctx, JsonObject a)
    {
        var m = ctx.Model;
        var storeyId = (string?)a["storeyId"] ?? Spatial.Storeys(m).FirstOrDefault()?.Id ?? throw new CommandException("The project has no storey.");
        Spatial.Require(m, storeyId, IfcClass.BuildingStorey);
        var type = (string?)a["typeId"] is { } tid ? RequireType(m, tid) : TypeFor(ctx, (string?)a["style"] ?? "cantilever");
        var fields = BalconySpec.Read(type);
        if (a["attrs"] is JsonObject given) Merge(fields, BalconySpec.Clean(given));
        var kind = (string?)fields["kind"] ?? "cantilever";
        var attrs = Merge(new JsonObject(), fields);
        attrs["exportAs"] = "IfcElementAssembly";
        attrs["objectType"] = "Balcony";
        if (Json.Number(a["baseOffset"]) is { } bo) attrs["baseOffset"] = bo;
        else attrs["baseOffset"] = 0.0;

        if (Args.StoredPoints(a["contour"]) is { Count: >= 3 } contour)
        {
            if (kind is "loggia" or "corner" or "mezzanine") attrs["kind"] = kind = "cantilever";
            attrs["outline"] = "polygon";
            attrs["contour"] = new JsonArray([.. contour.Select(p => (JsonNode)new JsonArray(p.X, p.Y))]);
        }
        else if (a["x1"] != null && a["x2"] != null)
        {
            var (p, q) = (Args.Point(a, "x1", "y1"), Args.Point(a, "x2", "y2"));
            if ((q - p).Length < 0.3) throw new CommandException("Draw the balcony's edge 300 mm long at least.");
            attrs["at"] = new JsonArray((p.X + q.X) / 2, (p.Y + q.Y) / 2);
            attrs["rotation"] = Math.Atan2(q.Y - p.Y, q.X - p.X) * 180 / Math.PI;
            attrs["width"] = Math.Round((q - p).Length, 4);
        }
        else
        {
            var p = Args.Point(a, "x", "y");
            var hit = (string?)a["wallId"] is { } wid && m.Get(wid) is { Class: IfcClass.Wall } wall
                ? (wall, Openings.AlongOf(wall, p), Vec2.Dot(p - Walls.Start(wall), (Walls.End(wall) - Walls.Start(wall)).Normalized().Perp) >= 0 ? 1 : -1)
                : WallAt(m, storeyId, p);
            if (hit is (var w, var along, var side) && kind != "mezzanine")
            {
                var c = PathCurve.Of(w);
                var width = Json.Number(fields["width"]) ?? 3;
                if (kind == "juliet" && a["attrs"]?["width"] == null)
                {
                    // A Juliet balcony goes in front of the door or window clicked by: centred on it, a little wider.
                    var near = Openings.PlacedOn(m, w.Id).Where(o => Math.Abs(o.Along - along) < o.Width / 2 + 0.5).OrderBy(o => Math.Abs(o.Along - along)).FirstOrDefault();
                    if (near.Opening != null) { along = near.Along; width = Math.Round(near.Width + 0.2, 4); attrs["width"] = width; }
                }
                if (kind == "corner")
                {
                    // At the wall's end nearer the click, wrapping round the corner.
                    var f = Openings.FrameOf(m, w, along);
                    var face = Math.Abs(side > 0 ? f.YMax : f.YMin);
                    var s = along > c.Length / 2 ? 1 : -1;
                    attrs["cornerSide"] = s * side > 0 ? "right" : "left";
                    along = (s > 0 ? c.Length : 0) + s * face - s * width / 2;
                }
                else along = Math.Clamp(along, Math.Min(width / 2, c.Length / 2), Math.Max(c.Length - width / 2, c.Length / 2));
                attrs["wallId"] = w.Id;
                attrs["along"] = Math.Round(along, 4);
                attrs["side"] = side;
            }
            else
            {
                if (kind == "loggia") throw new CommandException("A loggia is cut into a wall: click on a wall.");
                attrs["at"] = new JsonArray(p.X, p.Y);
                attrs["rotation"] = Json.Number(a["rotation"]) ?? 0;
            }
        }
        BalconySpec.EnsureMaterials(ctx, attrs);
        var e = new Entity(ctx.NewId(), Class, $"Balcony {Numbering.Next(m.OfClass(Class))}", attrs, "USERDEFINED");
        ctx.Add(e);
        Spatial.Contain(ctx, storeyId, e.Id);
        ctx.Relate(IfcClass.RelDefinesByType, type.Id, e.Id);
        return Settle(ctx, e);
    }

    static Entity Update(ICommandContext ctx, JsonObject a)
    {
        var m = ctx.Model;
        var b = m.Get((string?)a["id"] ?? "") is { Class: Class } x ? x : throw new CommandException("Not a balcony.");
        var style = (string?)a["style"];
        var fields = Draft(m, b, style, a["attrs"] as JsonObject);
        var attrs = Merge(b.CloneAttrs(), fields);
        if (style != null) ctx.Relate(IfcClass.RelDefinesByType, TypeFor(ctx, style).Id, b.Id);
        if (a["x"] != null && a["y"] != null)
        {
            var p = Args.Point(a, "x", "y");
            if (m.Get(b.Str("wallId") ?? "") is { Class: IfcClass.Wall } w) attrs["along"] = Math.Round(Math.Clamp(Openings.AlongOf(w, p), 0, PathCurve.Of(w).Length), 4);
            else attrs["at"] = new JsonArray(p.X, p.Y);
        }
        if (Json.Number(a["along"]) is { } al) attrs["along"] = al;
        if (Args.StoredPoints(a["contour"]) is { Count: >= 3 } contour) attrs["contour"] = new JsonArray([.. contour.Select(p => (JsonNode)new JsonArray(p.X, p.Y))]);
        BalconySpec.EnsureMaterials(ctx, attrs);
        return Settle(ctx, b.With(attrs: attrs));
    }

    /// <summary>
    /// A balcony put in the model as it now is: where it is cached (<c>at</c>, <c>rotation</c>: where it stays if its wall
    /// goes), and a loggia's recess and back wall made, moved or taken away.
    /// </summary>
    public static Entity Settle(ICommandContext ctx, Entity b)
    {
        ctx.Replace(b);
        Rehost(ctx, b.Id);
        b = ctx.Model.Get(b.Id)!;
        if (b.Str("wallId") != null && BalconyBuilder.Frame(ctx.Model, b) is { Wall: not null } f)
        {
            var attrs = b.CloneAttrs();
            attrs["at"] = new JsonArray(Math.Round(f.At.X, 6), Math.Round(f.At.Y, 6));
            attrs["rotation"] = Math.Round(f.Rotation, 6);
            b = b.With(attrs: attrs);
            ctx.Replace(b);
        }
        Loggia(ctx, b);
        return ctx.Model.Get(b.Id)!;
    }

    /// <summary>A balcony copied onto another storey than its wall's: hosted by the wall of its own storey that stands where its wall does, when there is one.</summary>
    static void Rehost(ICommandContext ctx, string id)
    {
        var m = ctx.Model;
        if (m.Get(id) is not { } b || m.Get(b.Str("wallId") ?? "") is not { Class: IfcClass.Wall } wall) return;
        var storey = Spatial.StoreyOf(m, b.Id)?.Id;
        if (storey == null || Spatial.StoreyOf(m, wall.Id)?.Id == storey) return;
        var (s, e) = (Walls.Start(wall), Walls.End(wall));
        var twin = m.OfClass(IfcClass.Wall).FirstOrDefault(w => Spatial.StoreyOf(m, w.Id)?.Id == storey && (Walls.Start(w) - s).Length < 1e-4 && (Walls.End(w) - e).Length < 1e-4);
        if (twin == null) return;
        var attrs = b.CloneAttrs();
        attrs["wallId"] = twin.Id;
        ctx.Replace(b.With(attrs: attrs));
    }

    public static Entity? RecessOf(IModelView m, string balconyId) => m.OfClass(IfcClass.OpeningElement).FirstOrDefault(o => o.Str("balconyId") == balconyId);
    public static Entity? BackWallOf(IModelView m, string balconyId) => m.OfClass(IfcClass.Wall).FirstOrDefault(o => o.Str("balconyId") == balconyId);

    /// <summary>
    /// A loggia's recess: an opening through its wall as wide as the loggia and as high as its recess, and, when it has one,
    /// its back wall (a wall of the host's type, as high, its face the loggia's depth behind the host's) between its side
    /// walls; kept to the loggia when it changes, taken away when it is no longer a loggia on a wall.
    /// </summary>
    static void Loggia(ICommandContext ctx, Entity b)
    {
        var m = ctx.Model;
        var recess = RecessOf(m, b.Id);
        var back = BackWallOf(m, b.Id);
        // A loggia cuts the wall it stands on, on its own storey (a copy whose storey has no such wall cuts none).
        var wall = b.Str("kind") == "loggia" && m.Get(b.Str("wallId") ?? "") is { Class: IfcClass.Wall } w && Walls.Section(m, w).Count > 0
            && Spatial.StoreyOf(m, w.Id)?.Id == Spatial.StoreyOf(m, b.Id)?.Id ? w : null;
        if (wall == null)
        {
            if (recess != null) ctx.Remove(recess.Id);
            if (back != null) ctx.Remove(back.Id);
            return;
        }
        var fields = BalconySpec.Read(b);
        var width = Math.Max(0.3, Json.Number(fields["width"]) ?? 3);
        var depth = Math.Max(0, Json.Number(fields["depth"]) ?? 1.5);
        var height = Math.Min(Math.Max(1.8, Json.Number(fields["recessHeight"]) ?? 2.6), wall.Num("height", Walls.DefaultHeight));
        var cheek = Math.Max(0.05, Json.Number(fields["cheekThickness"]) ?? 0.2);
        var along = b.Num("along");
        if (recess != null && Openings.WallOf(m, recess.Id)?.Id != wall.Id) { ctx.Remove(recess.Id); recess = null; }
        if (recess == null)
        {
            var made = Openings.AddBare(ctx, wall, along, 0, width, height);
            var ra = made.CloneAttrs();
            ra["balconyId"] = b.Id;
            ctx.Replace(made.With(name: "Loggia", attrs: ra));
        }
        else
        {
            Openings.CheckFits(m, wall, along, 0, width, height, recess.Id);
            var ra = recess.CloneAttrs();
            (ra["along"], ra["sill"], ra["width"], ra["height"]) = (along, 0.0, width, height);
            ctx.Replace(recess.With(attrs: ra));
        }
        var f = Openings.FrameOf(m, wall, along);
        var side = b.Num("side", 1) < 0 ? -1 : 1;
        var thickness = f.YMax - f.YMin;
        if (fields["backWall"] is JsonValue bw && bw.TryGetValue<bool>(out var wantBack) && wantBack && depth > thickness + 0.05 && !PathCurve.Of(wall).IsArc)
        {
            var shift = f.Left * (-side * depth);
            var (s, e) = (f.Start + f.Dir * (along - width / 2 - cheek) + shift, f.Start + f.Dir * (along + width / 2 + cheek) + shift);
            if (back == null)
            {
                var storey = Spatial.StoreyOf(m, wall.Id)?.Id ?? Spatial.StoreyOf(m, b.Id)!.Id;
                back = Walls.AddUnjoined(ctx, storey, s, e, wall.Num("height", Walls.DefaultHeight), wall.Num("baseOffset"), Walls.TypeOf(m, wall.Id));
                back = m.Get(back.Id)!;
                var ba = back.CloneAttrs();
                ba["balconyId"] = b.Id;
                ctx.Replace(back.With(name: $"{back.Name} (loggia)", attrs: ba));
                // Its exterior on the same side as its host's.
                var hostUsage = Walls.UsageOf(m, wall.Id) is { } hu ? Layers.ReadUsage(hu)?.Negative : null;
                var backUsage = Walls.UsageOf(m, back.Id) is { } bu ? Layers.ReadUsage(bu)?.Negative : null;
                if (hostUsage != null && backUsage != null && hostUsage != backUsage) Walls.Flip(ctx, m.Get(back.Id)!);
            }
            else
            {
                var ba = back.CloneAttrs();
                (ba["start"], ba["end"], ba["height"], ba["baseOffset"]) = (new JsonArray(s.X, s.Y), new JsonArray(e.X, e.Y), wall.Num("height", Walls.DefaultHeight), wall.Num("baseOffset"));
                ctx.Replace(back.With(attrs: ba));
            }
        }
        else if (back != null) ctx.Remove(back.Id);
    }

    // ------------------------------------------------------------ the plan above

    /// <summary>On a plan, the balconies of the storey above, dashed: what hangs over the drawing's storey.</summary>
    public static IEnumerable<ViewMark> Marks(ViewContext c)
    {
        if (c.Kind != "plan" || c.View.Str("site") != null) yield break;
        var m = c.Model;
        var storeys = Spatial.Storeys(m).OrderBy(s => s.Num("elevation")).ToList();
        var here = storeys.FindIndex(s => s.Id == c.View.Str("storeyId"));
        if (here < 0 || here + 1 >= storeys.Count) yield break;
        var above = storeys[here + 1].Id;
        var hidden = (c.View.Attrs["hiddenElements"] as JsonArray)?.Select(x => (string?)x).OfType<string>().ToHashSet() ?? [];
        foreach (var b in m.OfClass(Class))
        {
            if (hidden.Contains(b.Id) || Spatial.StoreyOf(m, b.Id)?.Id != above || b.Str("kind") is "loggia" or "mezzanine") continue;
            if (BalconyBuilder.Build(m, b) is not { } l || BalconyBuilder.Frame(m, b) is not { } f || !l.HasSlab) continue;
            var mark = new ViewMark(null) { Under = true };
            mark.Polyline(l.Outline.Select(p => c.Map(f.Plan(p))).ToList(), true, "BALCONY-OVERHEAD", 0.18, "#000000", [3, 1.5]);
            yield return mark;
        }
    }
}

/// <summary>A balcony deleted takes its loggia's recess and back wall with it.</summary>
sealed class BalconyDeletion : IDeletion
{
    public bool Delete(ICommandContext ctx, string id)
    {
        if (ctx.Model.Get(id) is not { Class: BalconyModule.Class }) return false;
        if (BalconyModule.RecessOf(ctx.Model, id) is { } r) ctx.Remove(r.Id);
        if (BalconyModule.BackWallOf(ctx.Model, id) is { } w) ctx.Remove(w.Id);
        ctx.Remove(id);
        return true;
    }
}

/// <summary>A balcony changed in its properties: its kind's support when only the kind changed, its recess and cache kept.</summary>
sealed class BalconyUpdate : IElementUpdate
{
    public void Updated(ICommandContext ctx, Entity before, Entity after)
    {
        if (after.Class != BalconyModule.Class) return;
        var b = after;
        if (before.Str("kind") != after.Str("kind") && before.Str("support") == after.Str("support"))
        {
            var attrs = after.CloneAttrs();
            attrs["support"] = BalconySpec.SupportOf(after.Str("kind") ?? "");
            b = after.With(attrs: attrs);
        }
        BalconyModule.Settle(ctx, b);
    }
}

/// <summary>
/// A balcony moved, turned or copied: on its wall it slides along it (still within reach of it), else it leaves the wall
/// and stands where it was carried; a loggia's recess and back wall follow.
/// </summary>
sealed class BalconyPlacement : IElementPlacement
{
    public bool Handles(Entity e) => e.Class == BalconyModule.Class;

    public void Transform(ICommandContext ctx, Entity e, Placement2D t, IReadOnlySet<string> moving)
    {
        Placement.Own(ctx, e, t, moving);
        Settle(ctx, ctx.Model.Get(e.Id)!, moving);
    }

    public string? Copy(ICommandContext ctx, Entity e, Placement2D t)
    {
        var id = Placement.DeepCopy(ctx, e, t);
        var copy = ctx.Model.Get(id)!;
        // A copy where the original is (Copy to storeys, before it goes to its storey) is settled once it is there.
        if ((t.Point(new Vec2(0, 0))).Length < 1e-9 && (t.Point(new Vec2(1, 0)) - new Vec2(1, 0)).Length < 1e-9 && !t.Mirrored) return id;
        var attrs = copy.CloneAttrs();
        attrs.Remove("balconyId");
        Settle(ctx, copy.With(attrs: attrs), new HashSet<string>());
        return id;
    }

    static void Settle(ICommandContext ctx, Entity b, IReadOnlySet<string> moving)
    {
        var m = ctx.Model;
        if (m.Get(b.Str("wallId") ?? "") is { Class: IfcClass.Wall } wall && !moving.Contains(wall.Id) && Args.TryPoint(b.Attrs["at"]) is { } at)
        {
            var attrs = b.CloneAttrs();
            var along = Openings.AlongOf(wall, at);
            var f = Openings.FrameOf(m, wall, along);
            var off = Math.Abs(Vec2.Dot(at - (f.Start + f.Dir * along), f.Left));
            if (along >= -1e-6 && along <= PathCurve.Of(wall).Length + 1e-6 && off <= Math.Max(Math.Abs(f.YMin), Math.Abs(f.YMax)) + 0.5) attrs["along"] = Math.Round(along, 4);
            else { attrs.Remove("wallId"); attrs.Remove("along"); }
            b = b.With(attrs: attrs);
        }
        BalconyModule.Settle(ctx, b);
    }
}

/// <summary>A wall moved with balconies on it that stay: their cache and a loggia's recess and back wall follow it.</summary>
sealed class BalconyFollowers : ITransformFollowers
{
    public object? Before(ICommandContext ctx, IReadOnlySet<string> moving, IReadOnlySet<string> selected) => null;

    public void After(ICommandContext ctx, object? state, Placement2D t, Placement2D plan, IReadOnlySet<string> moving, IReadOnlySet<string> selected, IReadOnlyDictionary<string, string> copies)
    {
        foreach (var b in ctx.Model.OfClass(BalconyModule.Class).Where(b => !moving.Contains(b.Id) && b.Str("wallId") is { } w && moving.Contains(w)).ToList())
            BalconyModule.Settle(ctx, b);
    }
}

/// <summary>
/// Copy to storeys for balconies (stacked balconies are one action): a copy goes on the wall of its new storey that stands
/// where its wall does, its loggia cutting that wall; a loggia's recess copied with its wall but not its loggia goes.
/// </summary>
sealed class BalconyCopies : IStoreyCopy
{
    public void Copied(ICommandContext ctx, StoreyCopyStep step)
    {
        var m = ctx.Model;
        var copies = step.Map.Values.ToHashSet();
        foreach (var (_, c) in step.Map)
        {
            if (m.Get(c) is not { } copy) continue;
            // The original loggia's recess or back wall, copied with its wall: its loggia stays where it is, so the copy goes (a copied loggia makes its own).
            if (copy.Str("balconyId") is { } bid && !copies.Contains(bid) && copy.Class is IfcClass.OpeningElement or IfcClass.Wall && m.Get(bid)?.Class == BalconyModule.Class)
                ctx.Remove(copy.Id);
        }
        foreach (var (_, c) in step.Map)
            if (m.Get(c) is { Class: BalconyModule.Class } b) BalconyModule.Settle(ctx, b);
    }
}

/// <summary>A balcony's frame for its grips: where it is on its wall's face, along it and out from it.</summary>
sealed class BalconyFrames : INodeProps
{
    public void Add(Entity e, IModelView m, JsonObject props)
    {
        if (e.Class != BalconyModule.Class || BalconyBuilder.Frame(m, e) is not { } f) return;
        props["balconyFrame"] = new JsonObject
        {
            ["at"] = new JsonArray(f.At.X, f.At.Y), ["u"] = new JsonArray(f.U.X, f.U.Y), ["v"] = new JsonArray(f.V.X, f.V.Y), ["hosted"] = f.Wall != null,
        };
    }
}

/// <summary>The balconies' schedule: each with its storey, style, kind, size, area, railing and what carries it; totals.</summary>
public sealed class BalconySchedule : IScheduleSource
{
    public string Kind => "balconies";
    public string Label => "Balconies";

    public ScheduleGrid Grid(IQueryContext q)
    {
        var m = q.Model;
        static string N(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);
        var rows = new List<IReadOnlyList<string>>();
        var keys = new List<string?>();
        double area = 0, rail = 0;
        var count = 0;
        foreach (var b in m.OfClass(BalconyModule.Class).OrderBy(x => Spatial.StoreyOf(m, x.Id)?.Num("elevation") ?? 0)
                     .ThenBy(x => x.Name, StringComparer.Create(CultureInfo.InvariantCulture, CompareOptions.NumericOrdering)))
        {
            if (BalconyBuilder.Build(m, b) is not { } l) continue;
            var type = m.RelatingOf(b.Id, IfcClass.RelDefinesByType)?.Name ?? "";
            var support = l.Kind is "loggia" or "mezzanine" ? "none" : l.Str("support");
            rows.Add([Spatial.StoreyOf(m, b.Id)?.Name ?? "", b.Name, type, l.Kind, N(l.Num("width")), N(l.Num("depth")), N(l.Area), N(l.RailLength),
                l.Str("railStyle"), support, l.Bool("thermalBreak") && l.Kind is not ("loggia" or "mezzanine") ? "yes" : "no"]);
            keys.Add(b.Id);
            area += l.Area;
            rail += l.RailLength;
            count++;
        }
        if (count > 0)
        {
            rows.Add(["Total", count.ToString(CultureInfo.InvariantCulture), "", "", "", "", N(area), N(rail), "", "", ""]);
            keys.Add(null);
        }
        return new ScheduleGrid(Label, ["Storey", "Balcony", "Style", "Kind", "Width (m)", "Depth (m)", "Area (m²)", "Railing (m)", "Railing design", "Carried by", "Thermal break"], rows, keys);
    }
}
