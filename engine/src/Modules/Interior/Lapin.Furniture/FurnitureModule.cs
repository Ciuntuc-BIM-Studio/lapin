using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Furniture;

/// <summary>
/// The furniture module (docs/plan-furniture.md): <c>IfcFurniture</c> placed at a point (<c>at</c>, the middle of its
/// footprint), turned by <c>rotation</c> (degrees; its front looks along its local −y), raised by <c>offset</c>,
/// mirrored by <c>mirror</c>; a kitchen run has its own <c>length</c>. Its shape is its <c>IfcFurnitureType</c>'s
/// <c>spec</c> (the kind, the size, the kind's parameters, the materials by role), so a change to the type is seen on
/// every piece of it. Placed by a click against the nearest wall's face (its back on the face, its front to the room),
/// chairs set round a table, a kitchen drawn along a wall; the Furniture schedules count them by type and by room.
/// </summary>
public sealed class FurnitureModule : ILapinModule
{
    public string Id => "furniture";
    public string Name => "Furniture";

    public const string Class = "IfcFurniture";
    public const string TypeClass = "IfcFurnitureType";

    public void Register(IModuleRegistry r)
    {
        r.Class(new(Class, "Furniture",
        [
            new("rotation", "Rotation (°)", "number"), new("offset", "Above the floor", "length"),
            new("mirror", "Mirrored", "bool"), new("length", "Length (a kitchen)", "length", Min: 0.6, Max: 12),
        ], "#b98b5a"));
        r.Class(new(TypeClass, "Furniture type", [], "#b98b5a", Kind: "type"));
        r.Geometry(Class, (e, m, _) => Geometry(e, m));
        r.Catalog("kinds", FurnitureKinds.Catalog());
        r.Catalog("kitchenLayouts", KitchenLayouts.Catalog());
        r.Catalog("kitchenUnits", KitchenUnits.Catalog());
        r.Catalog("casework", Casework.Catalog());
        r.Service<IElementPlacement>(new FurniturePlacement());
        r.Service<IScheduleSource>(new FurnitureSchedule());
        r.Service<IScheduleSource>(new FurnitureByRoom());
        r.Service<IScheduleSource>(new KitchenUnitsSchedule());
        r.Service<IScheduleSource>(new CutListSchedule());

        // A type of a kind: {kind, name?, spec?} → {id}; the spec's values over the kind's defaults, its materials made when missing.
        r.Command("createType", "New furniture type", (ctx, a) =>
        {
            var kind = FurnitureKinds.Find((string?)a["kind"]) ?? throw new CommandException($"A piece of furniture is one of {string.Join(", ", FurnitureKinds.All.Select(k => k.Key))}.");
            // A kitchen of a layout starts from its preset, under what the spec gives.
            var given = a["spec"] as JsonObject;
            if (kind.Key == "kitchen" && (string?)a["layout"] is { } layout)
            {
                if (!KitchenLayouts.Keys.Contains(layout)) throw new CommandException($"A kitchen's layout is one of {string.Join(", ", KitchenLayouts.Keys)}.");
                var preset = KitchenLayouts.Preset(layout);
                if (given != null) foreach (var (k2, v2) in given) preset[k2] = v2?.DeepClone();
                given = preset;
            }
            // A casework piece of a preset (decision 475): its values under what the spec gives.
            if (kind.Key == Casework.Key && (string?)a["preset"] is { } pk)
            {
                var preset = Casework.Preset(pk);
                if (given != null) foreach (var (k2, v2) in given) preset[k2] = v2?.DeepClone();
                given = preset;
            }
            return new JsonObject { ["id"] = CreateType(ctx, kind, (string?)a["name"], given).Id };
        });

        // A type changed: {id, name?, spec?} (values merged; null: the kind's default again). Its pieces follow.
        r.Command("updateType", "Change furniture type", (ctx, a) =>
        {
            var type = RequireType(ctx.Model, (string?)a["id"]);
            var spec = (type.Attrs["spec"] as JsonObject)?.DeepClone() as JsonObject ?? FurnitureKinds.NewSpec(FurnitureKinds.All[0], null);
            var kind = new Spec(spec).Kind;
            if (a["spec"] is JsonObject given) FurnitureKinds.Merge(spec, given, kind);
            EnsureMaterials(ctx, spec);
            var attrs = type.CloneAttrs();
            attrs["spec"] = spec;
            var name = ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : type.Name;
            // A casework type's preset says its IFC type (a desk DESK, a bookcase SHELF…): changed, its pieces follow.
            var (predefined, objectType) = FurnitureKinds.IfcOf(new Spec(spec));
            var casework = kind.Key == Casework.Key;
            var retyped = casework && predefined != type.PredefinedType;
            ctx.Replace(type.With(name: name, attrs: attrs, predefinedType: casework ? predefined : null));
            if (casework)
                foreach (var e in ctx.Model.RelatedOf(type.Id, IfcClass.RelDefinesByType).ToList())
                {
                    var ea = e.CloneAttrs();
                    var had = (string?)ea["objectType"];
                    if (objectType != null) ea["objectType"] = objectType; else ea.Remove("objectType");
                    if (retyped || had != objectType) ctx.Replace(e.With(attrs: ea, predefinedType: predefined));
                }
            // Pieces named as their type was are named as it is now.
            if (name != type.Name)
                foreach (var e in ctx.Model.RelatedOf(type.Id, IfcClass.RelDefinesByType).Where(x => x.Name == type.Name).ToList())
                    ctx.Replace(e.With(name: name));
            return null;
        });

        r.Command("duplicateType", "Duplicate furniture type", (ctx, a) =>
        {
            var type = RequireType(ctx.Model, (string?)a["id"]);
            var name = ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : Unique(ctx.Model, type.Name + " copy");
            var copy = new Entity(ctx.NewId(), TypeClass, name, type.CloneAttrs(), type.PredefinedType);
            ctx.Add(copy);
            return new JsonObject { ["id"] = copy.Id };
        });

        // My furniture library (decision 476): a type as the library keeps it, {typeId} → {class, name, predefinedType, spec, materials}.
        r.Query("libraryItem", (q, a) => LibraryItems.Of(q.Model, RequireType(q.Model, (string?)a["typeId"])));

        // A library item into the project, {item} → {id, reused}: its materials matched by name or made, an identical type reused.
        r.Command("insertFromLibrary", "Insert from my furniture", (ctx, a) =>
        {
            var item = a["item"] as JsonObject ?? throw new CommandException("Which library item?");
            var (type, reused) = LibraryItems.Insert(ctx, item);
            return new JsonObject { ["id"] = type.Id, ["reused"] = reused };
        });

        // A piece at a point: {storeyId, typeId? | kind?, x, y, rotation?, against? (true: backed onto the nearest wall's face), offset?, length?} → {id, against}.
        r.Command("place", "Furniture", (ctx, a) =>
        {
            var storey = Spatial.Require(ctx.Model, (string?)a["storeyId"] ?? Spatial.Storeys(ctx.Model).FirstOrDefault()?.Id, IfcClass.BuildingStorey);
            var type = TypeFor(ctx, (string?)a["typeId"], (string?)a["kind"]);
            var spec = new Spec(type.Attrs["spec"] as JsonObject);
            var p = Args.Point(a, "x", "y");
            var rotation = Json.Number(a["rotation"]) ?? 0;
            var length = Json.Number(a["length"]);
            var (w, d) = FurnitureBodies.Footprint(spec, length);
            var against = a["against"] is not JsonValue av || !av.TryGetValue<bool>(out var ag) || ag;
            var backed = against ? Against(ctx.Model, storey.Id, p, w, d) : null;
            if (backed is { } bk) (p, rotation) = bk;
            var id = Add(ctx, storey, type, p, rotation, Json.Number(a["offset"]) ?? 0, length);
            return new JsonObject { ["id"] = id, ["against"] = backed != null };
        });

        // A kitchen along a wall: {storeyId, typeId?, x1, y1, x2, y2}: its length the line's, its back on the face of the wall the line runs by.
        r.Command("kitchen", "Kitchen", (ctx, a) =>
        {
            var storey = Spatial.Require(ctx.Model, (string?)a["storeyId"] ?? Spatial.Storeys(ctx.Model).FirstOrDefault()?.Id, IfcClass.BuildingStorey);
            var type = TypeFor(ctx, (string?)a["typeId"], "kitchen");
            var spec = new Spec(type.Attrs["spec"] as JsonObject);
            if (spec.Kind.Key != "kitchen") throw new CommandException("A kitchen is drawn with a kitchen type.");
            var (p1, p2) = (Args.Point(a, "x1", "y1"), Args.Point(a, "x2", "y2"));
            if ((p2 - p1).Length < 0.6) throw new CommandException("A kitchen is at least 600 mm long.");
            var (centre, rotation, length) = AlongWall(ctx.Model, storey.Id, p1, p2, KitchenLayouts.Depth(spec));
            if (KitchenLayouts.Of(spec) is "l-shaped" or "peninsula" or "u-shaped" or "galley") centre = TuckInto(ctx.Model, storey.Id, spec, centre, rotation, Math.Round(Math.Min(12, length), 4));
            var id = Add(ctx, storey, type, centre, rotation, 0, Math.Round(Math.Min(12, length), 4));
            return new JsonObject { ["id"] = id };
        });

        // Chairs round a table: {tableId, typeId?, spacing?} → {ids}: along its long sides (and its ends when they hold one), round a round one, facing it.
        r.Command("chairsAround", "Chairs round a table", (ctx, a) =>
        {
            var table = ctx.Model.Get((string?)a["tableId"] ?? "") is { Class: Class } t ? t : throw new CommandException("Pick a table.");
            var tspec = new Spec(TypeOf(ctx.Model, table)?.Attrs["spec"] as JsonObject);
            if (tspec.Kind.Key is not ("table" or "desk")) throw new CommandException("Chairs go round a table.");
            var chair = TypeFor(ctx, (string?)a["typeId"], "chair");
            var cs = new Spec(chair.Attrs["spec"] as JsonObject);
            var storey = Spatial.StoreyOf(ctx.Model, table.Id) ?? throw new CommandException("The table stands on no storey.");
            var spacing = Json.Number(a["spacing"]) is { } sp && sp >= 0.4 ? sp : 0.6;
            var ids = new JsonArray();
            foreach (var (p, rot) in ChairsAround(table, tspec, cs, spacing))
                ids.Add((JsonNode)Add(ctx, storey, chair, p, rot, table.Num("offset"), null));
            return new JsonObject { ["ids"] = ids };
        });

        r.Command("setType", "Change type", (ctx, a) =>
        {
            var type = RequireType(ctx.Model, (string?)a["typeId"]);
            foreach (var id in Args.Ids(a))
            {
                var e = ctx.Model.Get(id) is { Class: Class } x ? x : throw new CommandException("Only furniture takes a furniture type.");
                var named = e.Name == TypeOf(ctx.Model, e)?.Name;
                ctx.Relate(IfcClass.RelDefinesByType, type.Id, e.Id);
                var ts = new Spec(type.Attrs["spec"] as JsonObject);
                var (predefined, objectType) = FurnitureKinds.IfcOf(ts);
                var attrs = e.CloneAttrs();
                if (objectType != null) attrs["objectType"] = objectType; else attrs.Remove("objectType");
                if (ts.Kind.Key != "kitchen") attrs.Remove("length");
                ctx.Replace(e.With(name: named ? type.Name : e.Name, attrs: attrs, predefinedType: predefined));
            }
            return null;
        });

        // Kitchens as long as their own units need (decision 472): {ids}; their length their main run's units', blind corners' and tall unit's.
        r.Command("fitKitchen", "Fit to units", (ctx, a) =>
        {
            foreach (var id in Args.Ids(a))
            {
                var e = ctx.Model.Get(id) is { Class: Class } x ? x : throw new CommandException("Only a kitchen fits to its units.");
                var spec = new Spec(TypeOf(ctx.Model, e)?.Attrs["spec"] as JsonObject);
                if (spec.Kind.Key != "kitchen" || spec.List(KitchenUnits.BaseKey) is not { Count: > 0 }) throw new CommandException("The kitchen's type has no units of its own: add them in Furniture types.");
                var attrs = e.CloneAttrs();
                attrs["length"] = Math.Min(12, KitchenUnits.RequiredLength(spec));
                ctx.Replace(e.With(attrs: attrs));
            }
            return null;
        });

        // Mirrored in place: its other hand (a chaise on the other side, the fridge at the other end), its front where it was.
        r.Command("flip", "Flip", (ctx, a) =>
        {
            foreach (var id in Args.Ids(a))
            {
                var e = ctx.Model.Get(id) is { Class: Class } x ? x : throw new CommandException("Only furniture flips here.");
                var attrs = e.CloneAttrs();
                if (e.Bool("mirror")) attrs.Remove("mirror"); else attrs["mirror"] = true;
                ctx.Replace(e.With(attrs: attrs));
            }
            return null;
        });

        // What a spec looks like, for the types dialog: {kind, spec?, length?} → {plan: [{kind, closed, points}], front: [{role, color, x0, x1, z0, z1}], width, depth, height}.
        r.Query("preview", (q, a) =>
        {
            var kind = FurnitureKinds.Find((string?)a["kind"]) ?? throw new CommandException("Which kind?");
            var spec = FurnitureKinds.NewSpec(kind, null);
            if (a["spec"] is JsonObject given) { var g = (JsonObject)given.DeepClone(); g.Remove("kind"); FurnitureKinds.Merge(spec, g, kind); }
            var s = new Spec(spec);
            var b = new Builder(new Vec2(0, 0), 0, 0, FurnitureBodies.Handed(s));
            // A kitchen with its own units is previewed as long as they need (a piece's own length is set where it stands).
            var length = Json.Number(a["length"]) ?? (s.Kind.Key == "kitchen" && s.List(KitchenUnits.BaseKey) is { Count: > 0 } ? Math.Max(s.W, KitchenUnits.RequiredLength(s)) : null);
            FurnitureBodies.Build(b, s, length);
            var colors = FurnitureKinds.RoleOrder.ToDictionary(role => role, role => Materials.Find(q.Model, s.MaterialName(role))?.Str("color") ?? FurnitureKinds.RoleDefaults[role].Color);
            var handed = FurnitureBodies.Handed(s);
            return new JsonObject
            {
                ["plan"] = new JsonArray([.. b.Plan.Select(i => (JsonNode)new JsonObject
                {
                    ["kind"] = i.Kind, ["closed"] = i.Closed, ["points"] = new JsonArray([.. i.Points.SelectMany(p => new[] { (JsonNode)R(p.X), R(p.Y) })]),
                })]),
                ["front"] = new JsonArray([.. b.Front.OrderByDescending(f => f.Y0).Select(f => (JsonNode)new JsonObject
                {
                    ["role"] = f.Role, ["color"] = colors.GetValueOrDefault(f.Role, "#999999"),
                    ["x0"] = R(handed ? -f.X1 : f.X0), ["x1"] = R(handed ? -f.X0 : f.X1), ["z0"] = R(f.Z0), ["z1"] = R(f.Z1),
                })]),
                ["width"] = FurnitureBodies.Footprint(s, length).W, ["depth"] = FurnitureBodies.Footprint(s, length).D, ["height"] = s.H,
                ["units"] = new JsonArray([.. b.Units.Select(u => (JsonNode)new JsonObject
                {
                    ["line"] = u.Line, ["index"] = u.Index, ["type"] = u.Type, ["width"] = u.Width, ["height"] = u.Height, ["detail"] = u.Detail, ["at"] = u.At,
                })]),
                ["unitsLength"] = s.Kind.Key == "kitchen" && KitchenUnits.Has(s) ? KitchenUnits.RequiredLength(s) : null,
                ["warnings"] = new JsonArray([.. b.Warnings.Select(w => (JsonNode)w)]),
                // A casework piece's boards and carcasses as laid (decision 475), for the configurator.
                ["boards"] = new JsonArray([.. b.Boards.Select(x => (JsonNode)new JsonObject
                {
                    ["part"] = x.Part, ["carcass"] = x.Carcass, ["role"] = x.Role, ["length"] = R(x.Length), ["width"] = R(x.Width), ["thickness"] = R(x.Thickness), ["grain"] = x.Grain, ["front"] = x.Front,
                })]),
                ["carcasses"] = new JsonArray([.. b.Carcasses.Select(c => (JsonNode)new JsonObject { ["index"] = c.Index, ["x0"] = R(c.X0), ["x1"] = R(c.X1) })]),
            };
        });
    }

    static double R(double v) => Math.Round(v, 5);

    // ---- The model ----

    public static Entity? TypeOf(IModelView m, Entity e) => m.RelatingOf(e.Id, IfcClass.RelDefinesByType) is { Class: TypeClass } t ? t : null;

    static Entity RequireType(IModelView m, string? id) => m.Get(id ?? "") is { Class: TypeClass } t ? t : throw new CommandException("There is no such furniture type.");

    static string Unique(IModelView m, string name)
    {
        var taken = m.OfClass(TypeClass).Select(t => t.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!taken.Contains(name)) return name;
        for (var i = 2; ; i++) if (!taken.Contains($"{name} {i}")) return $"{name} {i}";
    }

    static string Mm(double v) => Math.Round(v * 1000).ToString(CultureInfo.InvariantCulture);

    /// <summary>A type's name from its kind and size: Sofa 2200 × 950, Bed 1600 × 2050.</summary>
    static string DefaultName(Spec s) => s.Kind.Key == Casework.Key ? $"{Casework.Label(s)} {Mm(s.W)} × {Mm(s.D)}" : s.Kind.Key == "kitchen" ? KitchenLayouts.Of(s) is var l && l != "one-wall" ? $"{s.Kind.Label} {KitchenLayouts.Label(l)}, depth {Mm(s.D)}" : $"{s.Kind.Label}, depth {Mm(s.D)}" : $"{s.Kind.Label} {Mm(s.W)} × {Mm(s.D)}";

    static void EnsureMaterials(IEdit edit, JsonObject spec)
    {
        var s = new Spec(spec);
        foreach (var role in s.Kind.Roles)
        {
            var name = s.MaterialName(role);
            Materials.Ensure(edit, name, FurnitureKinds.ColorOf(name));
        }
    }

    public static Entity CreateType(IEdit edit, FurnitureKind kind, string? name, JsonObject? given)
    {
        var g = given?.DeepClone() as JsonObject;
        g?.Remove("kind");
        var spec = FurnitureKinds.NewSpec(kind, g);
        EnsureMaterials(edit, spec);
        var type = new Entity(edit.NewId(), TypeClass, Unique(edit.Model, string.IsNullOrWhiteSpace(name) ? DefaultName(new Spec(spec)) : name.Trim()), new JsonObject { ["spec"] = spec }, FurnitureKinds.IfcOf(new Spec(spec)).Predefined);
        edit.Add(type);
        return type;
    }

    /// <summary>The type asked for; else the project's first of the kind; else a new one of the kind.</summary>
    public static Entity TypeFor(IEdit edit, string? typeId, string? kind)
    {
        if (typeId != null) return RequireType(edit.Model, typeId);
        var k = FurnitureKinds.Find(kind ?? "sofa") ?? throw new CommandException($"A piece of furniture is one of {string.Join(", ", FurnitureKinds.All.Select(x => x.Key))}.");
        return edit.Model.OfClass(TypeClass).FirstOrDefault(t => new Spec(t.Attrs["spec"] as JsonObject).Kind.Key == k.Key && (t.Attrs["spec"] as JsonObject)?["kind"] != null)
            ?? CreateType(edit, k, null, null);
    }

    public static string Add(IEdit edit, Entity storey, Entity type, Vec2 at, double rotation, double offset, double? length)
    {
        var ts = new Spec(type.Attrs["spec"] as JsonObject);
        var k = ts.Kind;
        var (predefined, objectType) = FurnitureKinds.IfcOf(ts);
        var attrs = new JsonObject { ["at"] = new JsonArray(R(at.X), R(at.Y)), ["rotation"] = Math.Round(((rotation % 360) + 360) % 360, 4) };
        if (Math.Abs(offset) > 1e-9) attrs["offset"] = offset;
        if (k.Key == "kitchen" && length is { } l && l >= 0.6) attrs["length"] = l;
        if (objectType != null) attrs["objectType"] = objectType;
        var e = new Entity(edit.NewId(), Class, type.Name, attrs, predefined);
        edit.Add(e);
        Spatial.Contain(edit, storey.Id, e.Id);
        edit.Relate(IfcClass.RelDefinesByType, type.Id, e.Id);
        return e.Id;
    }

    // ---- The body ----

    public static ElementGeometry? Geometry(Entity e, IModelView m)
    {
        if (Args.TryPoint(e.Attrs["at"]) is not { } at) return null;
        var spec = new Spec(TypeOf(m, e)?.Attrs["spec"] as JsonObject);
        var z = (Spatial.StoreyOf(m, e.Id)?.Num("elevation") ?? 0) + e.Num("offset");
        var b = new Builder(at, z, e.Num("rotation"), e.Bool("mirror") ^ FurnitureBodies.Handed(spec));
        var length = e.NumOrNull("length");
        FurnitureBodies.Build(b, spec, length);
        var parts = new List<GeometryPart>();
        foreach (var role in FurnitureKinds.RoleOrder)
        {
            if (!b.Solids.TryGetValue(role, out var solids) || solids.Count == 0) continue;
            var mat = Materials.Find(m, spec.MaterialName(role));
            var color = mat?.Str("color") ?? FurnitureKinds.RoleDefaults[role].Color;
            parts.Add(new GeometryPart(char.ToUpperInvariant(role[0]) + role[1..], color, Combine(solids), mat?.Id, "furniture:" + role));
        }
        if (parts.Count == 0) return new ElementGeometry([], b.Plan, [at], ["The piece has no body."]);
        var (w, d) = FurnitureBodies.Footprint(spec, length);
        Vec2[] snaps = [at, b.Q(-w / 2, -d / 2), b.Q(w / 2, -d / 2), b.Q(w / 2, d / 2), b.Q(-w / 2, d / 2), b.Q(0, -d / 2), b.Q(0, d / 2)];
        return new ElementGeometry(parts, b.Plan, snaps, b.Warnings.Count > 0 ? b.Warnings : null);
    }

    /// <summary>Several solids as one part, as they are (they do not overlap much and are never merged).</summary>
    static Solid Combine(IReadOnlyList<Solid> solids)
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
        return new Solid([.. vertices], [.. faces], "furniture");
    }

    // ---- Placing against walls ----

    /// <summary>A straight wall's faces: its axis, the left normal its bands' offsets run along, the offsets of its two faces.</summary>
    readonly record struct WallFaces(string Id, Vec2 A, Vec2 U, Vec2 N, double Length, double Lo, double Hi)
    {
        public Vec2[] Corners => [A + N * Lo, A + U * Length + N * Lo, A + U * Length + N * Hi, A + N * Hi];
    }

    static IEnumerable<WallFaces> WallsOn(IModelView m, string storeyId)
    {
        foreach (var w in m.OfClass(IfcClass.Wall))
        {
            if (Spatial.StoreyOf(m, w.Id)?.Id != storeyId || PathCurve.Of(w).IsArc) continue;
            var (a, b) = (Walls.Start(w), Walls.End(w));
            var len = (b - a).Length;
            if (len < 0.05) continue;
            var u = (b - a) * (1 / len);
            var bands = Walls.Section(m, w);
            if (bands.Count == 0) continue;
            yield return new WallFaces(w.Id, a, u, u.Perp, len, bands.Min(x => x.From), bands.Max(x => x.To));
        }
    }

    /// <summary>The face nearest a point, outside its wall, within <paramref name="reach"/>: the wall, how far along its axis, the face's offset and the way out of it.</summary>
    static (WallFaces Wall, double T, double Face, Vec2 Out, double Dist)? NearestFace(IEnumerable<WallFaces> walls, Vec2 p, double reach, Vec2? along = null)
    {
        (WallFaces, double, double, Vec2, double)? best = null;
        foreach (var w in walls)
        {
            if (along is { } dir && Math.Abs(Vec2.Cross(w.U, dir)) > Math.Sin(12 * Math.PI / 180)) continue;
            var t = Vec2.Dot(p - w.A, w.U);
            if (t < -0.3 || t > w.Length + 0.3) continue;
            var off = Vec2.Dot(p - w.A, w.N);
            var (face, outN, dist) = off > w.Hi ? (w.Hi, w.N, off - w.Hi) : off < w.Lo ? (w.Lo, -w.N, w.Lo - off) : (double.NaN, default(Vec2), double.NaN);
            if (double.IsNaN(dist) || dist > reach) continue;
            if (best is not { } bb || dist < bb.Item5) best = (w, t, face, outN, dist);
        }
        return best;
    }

    /// <summary>The rotation (degrees) that turns a piece's front (its local −y) to look along <paramref name="out"/>.</summary>
    static double Facing(Vec2 @out) => Math.Atan2(@out.X, -@out.Y) * 180 / Math.PI;

    /// <summary>
    /// A piece of footprint w × d clicked at <paramref name="p"/>, backed onto the nearest wall face within reach (its
    /// depth plus 400 mm): its middle along the face as clicked but kept on the wall, then slid clear of a wall it would
    /// stand in (the one across a corner). Null when no wall is near.
    /// </summary>
    public static (Vec2 At, double Rotation)? Against(IModelView m, string storeyId, Vec2 p, double w, double d)
    {
        var walls = WallsOn(m, storeyId).ToList();
        if (NearestFace(walls, p, d + 0.4) is not { } f) return null;
        var tc = f.Wall.Length >= w ? Math.Clamp(f.T, w / 2, f.Wall.Length - w / 2) : f.Wall.Length / 2;
        var centre = f.Wall.A + f.Wall.U * tc + f.Wall.N * f.Face + f.Out * (d / 2 + 0.002);
        centre = Clear(walls.Where(x => x.Id != f.Wall.Id), centre, f.Wall.U, f.Out, w, d);
        return (centre, Facing(f.Out));
    }

    /// <summary>The piece slid along the wall it backs onto until it stands in no other wall.</summary>
    static Vec2 Clear(IEnumerable<WallFaces> others, Vec2 c, Vec2 u, Vec2 @out, double w, double d)
    {
        var list = others.ToList();
        for (var pass = 0; pass < 3; pass++)
        {
            var moved = false;
            foreach (var o in list)
            {
                Vec2[] piece = [c - u * (w / 2) - @out * (d / 2), c + u * (w / 2) - @out * (d / 2), c + u * (w / 2) + @out * (d / 2), c - u * (w / 2) + @out * (d / 2)];
                var wall = o.Corners;
                if (!Overlap(piece, wall)) continue;
                var (lo, hi) = (wall.Min(q => Vec2.Dot(q - c, u)), wall.Max(q => Vec2.Dot(q - c, u)));
                // To whichever side is nearer: the piece's right end past the wall's near side, or its left end past its far side.
                var right = hi + w / 2 + 0.002;
                var left = lo - w / 2 - 0.002;
                c += u * (Math.Abs(right) < Math.Abs(left) ? right : left);
                moved = true;
            }
            if (!moved) break;
        }
        return c;
    }

    /// <summary>Whether two convex quadrilaterals overlap (more than touching), by their separating axes.</summary>
    static bool Overlap(Vec2[] a, Vec2[] b)
    {
        foreach (var poly in new[] { a, b })
            for (var i = 0; i < poly.Length; i++)
            {
                var e = poly[(i + 1) % poly.Length] - poly[i];
                var n = new Vec2(-e.Y, e.X).Normalized();
                var (a0, a1) = (a.Min(p => Vec2.Dot(p, n)), a.Max(p => Vec2.Dot(p, n)));
                var (b0, b1) = (b.Min(p => Vec2.Dot(p, n)), b.Max(p => Vec2.Dot(p, n)));
                if (a1 <= b0 + 1e-4 || b1 <= a0 + 1e-4) return false;
            }
        return true;
    }

    /// <summary>A kitchen drawn from p1 to p2: backed onto the face of a wall it runs along (else its back on the line, its front to the line's right), its length the line's.</summary>
    static (Vec2 Centre, double Rotation, double Length) AlongWall(IModelView m, string storeyId, Vec2 p1, Vec2 p2, double depth)
    {
        var dir = (p2 - p1).Normalized();
        var mid = (p1 + p2) * 0.5;
        if (NearestFace(WallsOn(m, storeyId), mid, depth + 0.8, dir) is { } f)
        {
            var (t1, t2) = (Vec2.Dot(p1 - f.Wall.A, f.Wall.U), Vec2.Dot(p2 - f.Wall.A, f.Wall.U));
            var tc = (t1 + t2) / 2;
            var c = f.Wall.A + f.Wall.U * tc + f.Wall.N * f.Face + f.Out * (depth / 2 + 0.002);
            return (c, Facing(f.Out), Math.Abs(t2 - t1));
        }
        var right = new Vec2(dir.Y, -dir.X);
        return (mid + right * (depth / 2), Facing(right), (p2 - p1).Length);
    }

    /// <summary>
    /// A kitchen with legs, backed onto its wall, slid along it until a leg's back is on the wall across the end it reaches
    /// (an L or a peninsula into the corner it is drawn near; a U or a galley between two walls) when one stands within
    /// 500 mm of that end. Whatever sits further is left as drawn.
    /// </summary>
    static Vec2 TuckInto(IModelView m, string storeyId, Spec spec, Vec2 centre, double rotation, double length)
    {
        var layout = KitchenLayouts.Of(spec);
        var depth = KitchenLayouts.Depth(spec);
        var rad = rotation * Math.PI / 180;
        Vec2 x = new(Math.Cos(rad), Math.Sin(rad)), y = new(-Math.Sin(rad), Math.Cos(rad));
        bool left = layout is "u-shaped" or "galley" or "l-shaped" or "peninsula" && spec.Str("corner") != "right";
        bool right = layout is "u-shaped" or "galley" or "l-shaped" or "peninsula" && spec.Str("corner") == "right";
        var shift = 0.0;
        foreach (var w in WallsOn(m, storeyId))
        {
            // The wall in the piece's frame: it must run across the piece (along its y) and cover its depth.
            if (Math.Abs(Vec2.Dot(w.U, x)) > Math.Sin(12 * Math.PI / 180)) continue;
            var local = w.Corners.Select(q => (X: Vec2.Dot(q - centre, x), Y: Vec2.Dot(q - centre, y))).ToList();
            if (Math.Min(local.Max(q => q.Y), depth / 2) - Math.Max(local.Min(q => q.Y), -depth / 2) < 0.2) continue;
            var (wx0, wx1) = (local.Min(q => q.X), local.Max(q => q.X));
            if (left && Math.Abs(wx1 + length / 2) <= 0.5 && Math.Abs(wx1 + length / 2) > 1e-4) shift = wx1 + length / 2;
            else if (right && Math.Abs(wx0 - length / 2) <= 0.5 && Math.Abs(wx0 - length / 2) > 1e-4) shift = wx0 - length / 2;
        }
        return centre + x * shift;
    }

    // ---- Chairs round a table ----

    /// <summary>Where the chairs go round a table, each facing it: a chair every <paramref name="spacing"/> along each long side, one at each end that holds one; round a round table, on a circle.</summary>
    public static List<(Vec2 At, double Rotation)> ChairsAround(Entity table, Spec t, Spec chair, double spacing)
    {
        var at = Args.PointOf(table.Attrs["at"]);
        var rot = table.Num("rotation");
        var (c, s) = (Math.Cos(rot * Math.PI / 180), Math.Sin(rot * Math.PI / 180));
        Vec2 W(double x, double y) => new(at.X + c * x - s * y, at.Y + s * x + c * y);
        var o = new List<(Vec2, double)>();
        const double tuck = 0.12;
        var cd = chair.D;
        if (t.Kind.Key == "table" && t.Str("shape") == "round")
        {
            var radius = t.W / 2 + cd / 2 - tuck;
            var n = Math.Max(2, (int)Math.Floor(Math.PI * t.W / spacing));
            for (var i = 0; i < n; i++)
            {
                var phi = -Math.PI / 2 + 2 * Math.PI * i / n;
                // Facing the middle: its front (local −y) along −(cos φ, sin φ).
                var facing = Math.Atan2(-Math.Cos(phi), Math.Sin(phi)) * 180 / Math.PI;
                o.Add((W(radius * Math.Cos(phi), radius * Math.Sin(phi)), facing + rot));
            }
            return o;
        }
        double w = t.W, d = t.D;
        var per = Math.Max(1, (int)Math.Floor((w - 0.1) / spacing));
        var step = w / per;
        var desk = t.Kind.Key == "desk";
        for (var i = 0; i < per; i++)
        {
            var x = -w / 2 + step * (i + 0.5);
            // In front of its front side, facing back (+y), turned 180°; behind its back side, facing −y. A desk: its front only.
            o.Add((W(x, -d / 2 - cd / 2 + tuck), rot + 180));
            if (!desk) o.Add((W(x, d / 2 + cd / 2 - tuck), rot));
        }
        if (!desk && d >= chair.W + 0.2)
        {
            o.Add((W(w / 2 + cd / 2 - tuck, 0), rot - 90));
            o.Add((W(-w / 2 - cd / 2 + tuck, 0), rot + 90));
        }
        return o;
    }
}

/// <summary>Furniture moved, turned, mirrored and copied: a mirror takes it to its other hand with its front still to the room.</summary>
sealed class FurniturePlacement : IElementPlacement
{
    public bool Handles(Entity e) => e.Class == FurnitureModule.Class;

    static JsonObject Mirrored(Entity e, JsonObject attrs)
    {
        // The plan's mirror turned its front about too; turned back, and its hand changed instead.
        attrs["rotation"] = Math.Round((((Json.Number(attrs["rotation"]) ?? 0) + 180) % 360 + 360) % 360, 6);
        if (e.Bool("mirror")) attrs.Remove("mirror"); else attrs["mirror"] = true;
        return attrs;
    }

    public void Transform(ICommandContext ctx, Entity e, Placement2D t, IReadOnlySet<string> moving)
    {
        var attrs = Placement.Carry(e, t);
        ctx.Replace(e.With(attrs: t.Mirrored ? Mirrored(e, attrs) : attrs));
    }

    public string? Copy(ICommandContext ctx, Entity e, Placement2D t)
    {
        var id = Placement.DeepCopy(ctx, e, t);
        if (t.Mirrored && ctx.Model.Get(id) is { } copy) ctx.Replace(copy.With(attrs: Mirrored(e, copy.CloneAttrs())));
        return id;
    }
}
