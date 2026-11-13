using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Platform.Coordination;

namespace Lapin.Mep.Fans;

/// <summary>
/// Exhaust fans and ventilators (testers' report #55, docs/user/mep.md, Exhaust fans): a <c>LapinExhaustFan</c> (written as
/// IfcFan) of a <c>LapinExhaustFanType</c> (IfcFanType) whose <c>spec</c> holds its kind (a wall fan, a ceiling fan with its
/// duct, an inline duct fan, a window fan, a ceiling fan with a light, a fan in a louvre panel), its duct size, its grille's
/// shape, size, pattern and finish, its airflow and power, its mounting, its control (a switch, humidity, a timer, both,
/// motion) and its backdraft shutter. A fan stands at <c>at</c> (on the room face of its wall, or under its ceiling),
/// turned by <c>rotation</c> (its front, local −y, to the room), <c>height</c> above its storey's floor (a wall fan's
/// middle, a ceiling fan's face, an inline fan's axis). A wall fan cuts a round provision through the wall it sits on
/// (found by where it is, never by an id); a ceiling or inline fan may have a duct to an outlet in a wall or through the
/// roof (<c>mep.fanDuct</c>: a run of an extract air system, the outlet drawn with the fan).
/// </summary>
public static class ExhaustFans
{
    public const string Class = "LapinExhaustFan";
    public const string TypeClass = "LapinExhaustFanType";
    public const string Article = "LAP-FAN";

    static IReadOnlyList<IReadOnlyDictionary<string, string[]>> When(string key, params string[] values) => FieldDescriptor.When(key, values);

    public static void Register(IModuleRegistry r)
    {
        r.Class(new(Class, "Exhaust fan",
        [
            new("mounting", "Mounting", "text", ReadOnly: true),
            new("height", "Height above the floor", "length", Min: 0, Max: 20),
            new("rotation", "Rotation (°)", "number"),
            new("outlet", "Outlet", "choice", Choices: ["wall", "roof"], ShowIf: When("ductId", "!")),
            new("outletRise", "Outlet above the duct", "length", Min: 0.2, Max: 6, ShowIf: When("outlet", "roof")),
        ], "#9fb3c8"));
        r.Class(new(TypeClass, "Exhaust fan type", [], "#9fb3c8", Kind: "type"));
        r.Geometry(Class, (e, m, _) => Geometry(e, m));
        // A building service as the IfcFan it is written as: a schedule's Installations, an estimate's chapter.
        IfcSchema.Subtype(Class, "IfcFan");
        r.Quantities(Class, (e, g, m) => [new QuantitySet("Lapin_FanQuantities", [new Quantity("Count", 1, "COUNT", "Count")])]);
        r.Catalog("fanKinds", FanKinds.Catalog());
        r.Catalog("fanParams", FanKinds.ParamsCatalog());
        r.Catalog("fanPresets", new JsonArray([.. FanPresets.All.Select(p => (JsonNode)p.ToJson())]));
        Generator.SymbolicInPlan.Add(Class);
        var placement = new FanPlacement();
        r.Service<IElementPlacement>(placement);
        r.Service<IElementUpdate>(placement);
        r.Service<IProvisionOpenings>(new FanProvisions());
        r.Service<IScheduleSource>(new FanSchedule());
        r.Service<IScheduleSource>(new FanRoomCheck());

        // A type of a kind or from a built-in preset: {kind | preset, name?, spec?} → {id}.
        r.Command("createFanType", "New exhaust fan type", (ctx, a) =>
        {
            if ((string?)a["preset"] is { } pk)
            {
                var preset = FanPresets.Find(pk) ?? throw new CommandException($"There is no built-in fan type „{pk}”.");
                return new JsonObject { ["id"] = CreateType(ctx, FanKinds.Find(preset.Kind)!, (string?)a["name"] ?? preset.Name, preset.Spec).Id };
            }
            var kind = FanKinds.Find((string?)a["kind"]) ?? throw new CommandException($"An exhaust fan is one of {string.Join(", ", FanKinds.All.Select(k => k.Key))}.");
            return new JsonObject { ["id"] = CreateType(ctx, kind, (string?)a["name"], a["spec"] as JsonObject).Id };
        });

        // A type changed: {id, name?, spec?}; its fans follow (a mounting the kind does not take is refused).
        r.Command("updateFanType", "Change exhaust fan type", (ctx, a) =>
        {
            var type = RequireType(ctx.Model, (string?)a["id"]);
            var spec = (type.Attrs["spec"] as JsonObject)?.DeepClone() as JsonObject ?? FanKinds.NewSpec(FanKinds.All[0], null);
            var kind = new FanSpec(spec).Kind;
            if (a["spec"] is JsonObject given) FanKinds.Merge(spec, given, kind);
            EnsureMaterials(ctx);
            var attrs = type.CloneAttrs();
            attrs["spec"] = spec;
            var name = ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : type.Name;
            ctx.Replace(type.With(name: name, attrs: attrs));
            var mounting = new FanSpec(spec).Mounting;
            foreach (var e in ctx.Model.RelatedOf(type.Id, IfcClass.RelDefinesByType).Where(x => x.Class == Class).ToList())
            {
                var ea = e.CloneAttrs();
                ea["mounting"] = mounting;
                ctx.Replace(e.With(name: e.Name == type.Name ? name : e.Name, attrs: ea));
            }
            return null;
        });

        r.Command("duplicateFanType", "Duplicate exhaust fan type", (ctx, a) =>
        {
            var type = RequireType(ctx.Model, (string?)a["id"]);
            var name = ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : Unique(ctx.Model, type.Name + " copy");
            var copy = new Entity(ctx.NewId(), TypeClass, name, type.CloneAttrs(), type.PredefinedType);
            ctx.Add(copy);
            return new JsonObject { ["id"] = copy.Id };
        });

        // A fan placed: {storeyId, typeId | kind | preset, x, y, rotation?, height?} → {id, wallId}. On a wall or in a window: on the
        // face of the wall nearest the click (within 600 mm), its front to the room; on a ceiling or inline: where clicked.
        r.Command("placeFan", "Exhaust fan", (ctx, a) =>
        {
            var storey = Spatial.Require(ctx.Model, (string?)a["storeyId"] ?? Spatial.Storeys(ctx.Model).FirstOrDefault()?.Id, IfcClass.BuildingStorey);
            var type = TypeFor(ctx, (string?)a["typeId"], (string?)a["kind"], (string?)a["preset"]);
            var spec = new FanSpec(type.Attrs["spec"] as JsonObject);
            var p = Args.Point(a, "x", "y");
            var rotation = Json.Number(a["rotation"]) ?? 0;
            string? wallId = null;
            if (spec.Upright)
            {
                var face = FaceNear(ctx.Model, storey.Id, p, 0.6) ?? throw new CommandException("A wall or window fan goes on a wall: click near a wall's face.");
                (p, rotation, wallId) = (face.At, face.Rotation, face.Wall.Wall.Id);
            }
            var height = Json.Number(a["height"]) ?? DefaultHeight(ctx.Model, storey, spec, p);
            var id = Add(ctx, storey, type, p, rotation, height);
            return new JsonObject { ["id"] = id, ["wallId"] = wallId };
        });

        r.Command("setFanType", "Change type", (ctx, a) =>
        {
            var type = RequireType(ctx.Model, (string?)a["typeId"]);
            var spec = new FanSpec(type.Attrs["spec"] as JsonObject);
            foreach (var id in Args.Ids(a))
            {
                var e = ctx.Model.Get(id) is { Class: Class } x ? x : throw new CommandException("Only an exhaust fan takes a fan type.");
                var before = SpecOf(ctx.Model, e);
                if (before.Upright != spec.Upright) throw new CommandException("A wall fan takes a wall or window type, a ceiling or inline fan a ceiling or inline type.");
                var named = e.Name == TypeOf(ctx.Model, e)?.Name;
                ctx.Relate(IfcClass.RelDefinesByType, type.Id, e.Id);
                var attrs = e.CloneAttrs();
                attrs["mounting"] = spec.Mounting;
                attrs["objectType"] = spec.Kind.Label;
                ctx.Replace(e.With(name: named ? type.Name : e.Name, attrs: attrs, predefinedType: spec.Kind.Predefined));
            }
            return null;
        });

        // A ceiling or inline fan's duct to an outlet: {id, x, y, outlet?: wall | roof, outletRise?} → {ductId, systemId}. The duct is a
        // run of the project's bathroom extract system (made when missing, cutting what it crosses), from the fan's spigot to the point.
        r.Command("fanDuct", "Duct to outlet", (ctx, a) =>
        {
            var fan = ctx.Model.Get((string?)a["id"] ?? "") is { Class: Class } f ? f : throw new CommandException("Pick an exhaust fan.");
            var spec = SpecOf(ctx.Model, fan);
            if (spec.Upright) throw new CommandException("A wall or window fan blows straight out through its wall; a duct starts from a ceiling or an inline fan.");
            var storey = Spatial.StoreyOf(ctx.Model, fan.Id) ?? throw new CommandException("The fan stands on no storey.");
            var to = Args.Point(a, "x", "y");
            var (start, dir, level) = Spigot(ctx.Model, fan, spec);
            if ((to - start.Xy).Length < 0.1) throw new CommandException("The outlet is too close to the fan.");
            var system = ExtractSystem(ctx, spec.Diameter);
            if (fan.Str("ductId") is { } old && ctx.Model.Get(old) is { } oldRun) ctx.Remove(oldRun.Id);
            var leg = start.Xy + dir * 0.2;
            var straight = Math.Abs(Vec2.Cross(dir, (to - start.Xy).Normalized())) < 0.02 && Vec2.Dot(dir, to - start.Xy) > 0;
            Vec2[] pts = straight ? [start.Xy, to] : [start.Xy, leg, to];
            var args = new JsonObject
            {
                ["points"] = new JsonArray([.. pts.Select(q => (JsonNode)new JsonArray(Math.Round(q.X, 5), Math.Round(q.Y, 5)))]),
                ["levels"] = new JsonArray([.. pts.Select(_ => (JsonNode)Math.Round(level, 4))]),
                ["size"] = spec.Diameter, ["plane"] = "ceiling", ["name"] = $"{fan.Name} duct",
            };
            var run = Runs.Draw(ctx, system, storey.Id, args);
            var attrs = fan.CloneAttrs();
            attrs["ductId"] = run.Id;
            attrs["outlet"] = (string?)a["outlet"] is "roof" ? "roof" : "wall";
            if (Json.Number(a["outletRise"]) is { } rise && rise >= 0.2) attrs["outletRise"] = rise;
            ctx.Replace(fan.With(attrs: attrs));
            return new JsonObject { ["ductId"] = run.Id, ["systemId"] = system.Id };
        });

        // What a spec looks like, for the configurator: {kind | preset, spec?} → {plan: [...], front: [{role, color, points}], width, height, depth}.
        r.Query("fanPreview", (q, a) =>
        {
            JsonObject spec;
            if ((string?)a["preset"] is { } pk && FanPresets.Find(pk) is { } preset) spec = FanKinds.NewSpec(FanKinds.Find(preset.Kind)!, preset.Spec);
            else spec = FanKinds.NewSpec(FanKinds.Find((string?)a["kind"]) ?? throw new CommandException("Which kind?"), null);
            if (a["spec"] is JsonObject given) { var g = (JsonObject)given.DeepClone(); g.Remove("kind"); FanKinds.Merge(spec, g, new FanSpec(spec).Kind); }
            var s = new FanSpec(spec);
            var b = new LocalBuilder(new Vec2(0, 0), 0, 0);
            Build(b, s, 0, 0.15, null);
            return Preview(q.Model, b, s);
        });
    }

    // ---- The model ----

    public static Entity? TypeOf(IModelView m, Entity e) => m.RelatingOf(e.Id, IfcClass.RelDefinesByType) is { Class: TypeClass } t ? t : null;
    public static FanSpec SpecOf(IModelView m, Entity e) => new(TypeOf(m, e)?.Attrs["spec"] as JsonObject);

    static Entity RequireType(IModelView m, string? id) => m.Get(id ?? "") is { Class: TypeClass } t ? t : throw new CommandException("There is no such exhaust fan type.");

    static string Unique(IModelView m, string name)
    {
        var taken = m.OfClass(TypeClass).Select(t => t.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!taken.Contains(name)) return name;
        for (var i = 2; ; i++) if (!taken.Contains($"{name} {i}")) return $"{name} {i}";
    }

    static string Mm(double v) => Math.Round(v * 1000).ToString(CultureInfo.InvariantCulture);

    /// <summary>A type's name from its kind, size and airflow: Wall fan (axial) Ø100, 95 m³/h.</summary>
    static string DefaultName(FanSpec s) => $"{s.Kind.Label} Ø{Mm(s.Diameter)}, {s.Airflow.ToString("0.#", CultureInfo.InvariantCulture)} m³/h";

    static void EnsureMaterials(IEdit edit)
    {
        foreach (var (mat, color) in FanKinds.FinishMaterials.Values.Append(FanKinds.Housing).Append(FanKinds.Light).Append(FanKinds.Panel))
            if (Materials.Find(edit.Model, mat) == null) Materials.Ensure(edit, mat, color);
    }

    public static Entity CreateType(IEdit edit, FanKind kind, string? name, JsonObject? given)
    {
        var g = given?.DeepClone() as JsonObject;
        g?.Remove("kind");
        var spec = FanKinds.NewSpec(kind, g);
        EnsureMaterials(edit);
        var s = new FanSpec(spec);
        var attrs = new JsonObject { ["spec"] = spec, ["articles"] = new JsonArray(new JsonObject { ["code"] = Article, ["basis"] = "count" }) };
        var type = new Entity(edit.NewId(), TypeClass, Unique(edit.Model, string.IsNullOrWhiteSpace(name) ? DefaultName(s) : name.Trim()), attrs, kind.Predefined);
        edit.Add(type);
        return type;
    }

    /// <summary>The type asked for; a built-in preset's (made); else the project's first of the kind; else a new one of the kind.</summary>
    public static Entity TypeFor(IEdit edit, string? typeId, string? kind, string? preset = null)
    {
        if (typeId != null) return RequireType(edit.Model, typeId);
        if (preset != null)
        {
            var p = FanPresets.Find(preset) ?? throw new CommandException($"There is no built-in fan type „{preset}”.");
            return edit.Model.OfClass(TypeClass).FirstOrDefault(t => t.Name == p.Name) ?? CreateType(edit, FanKinds.Find(p.Kind)!, p.Name, p.Spec);
        }
        var k = FanKinds.Find(kind ?? "wallAxial") ?? throw new CommandException($"An exhaust fan is one of {string.Join(", ", FanKinds.All.Select(x => x.Key))}.");
        return edit.Model.OfClass(TypeClass).FirstOrDefault(t => new FanSpec(t.Attrs["spec"] as JsonObject).Kind.Key == k.Key) ?? CreateType(edit, k, null, null);
    }

    static double DefaultHeight(IModelView m, Entity storey, FanSpec s, Vec2 p)
    {
        if (s.Mounting == "wall") return 2.1;
        if (s.Mounting == "window") return 1.9;
        // Under the ceiling: the room's height where it is, else the storey's, else 2.5 m; an inline fan 300 mm above it in the void.
        var room = m.OfClass(Rooms.Class).Where(x => Spatial.StoreyOf(m, x.Id)?.Id == storey.Id).FirstOrDefault(x => Rooms.Stored(x)?.Holds(p) == true);
        var ceiling = room != null ? Rooms.HeightOf(room, m) : storey.NumOrNull("height") is { } h && h > 0 ? h - 0.3 : 2.5;
        ceiling = Math.Min(ceiling, 2.7);
        return s.Inline ? ceiling + 0.25 : ceiling;
    }

    public static string Add(IEdit edit, Entity storey, Entity type, Vec2 at, double rotation, double height)
    {
        var s = new FanSpec(type.Attrs["spec"] as JsonObject);
        var attrs = new JsonObject
        {
            ["at"] = new JsonArray(Math.Round(at.X, 5), Math.Round(at.Y, 5)), ["rotation"] = Math.Round(((rotation % 360) + 360) % 360, 4),
            ["height"] = Math.Round(height, 4), ["mounting"] = s.Mounting, ["objectType"] = s.Kind.Label, ["exportAs"] = "IfcFan",
        };
        var n = edit.Model.OfClass(Class).Count() + 1;
        var e = new Entity(edit.NewId(), Class, type.Name, attrs, s.Kind.Predefined);
        edit.Add(e);
        Spatial.Contain(edit, storey.Id, e.Id);
        edit.Relate(IfcClass.RelDefinesByType, type.Id, e.Id);
        return e.Id;
    }

    /// <summary>The project's extract air system for fans (named Bathroom extract, its runs cutting what they cross), made when missing.</summary>
    static Entity ExtractSystem(ICommandContext ctx, double size)
    {
        var found = Systems.All(ctx.Model).FirstOrDefault(s => Systems.Service(s) == Services.ExtractAir && s.Bool("fans"));
        if (found != null) return found;
        var sys = Systems.Create(ctx, Services.ExtractAir, "Bathroom extract", new JsonObject { ["size"] = size, ["plane"] = "ceiling" });
        var attrs = sys.CloneAttrs();
        attrs["fans"] = true;
        attrs["cutHosts"] = true;
        var with = sys.With(attrs: attrs);
        ctx.Replace(with);
        return with;
    }

    // ---- Walls ----

    /// <summary>A straight wall's faces where a fan may sit: its axis, the left normal its bands' offsets run along, the offsets of its two faces, its base and top.</summary>
    public readonly record struct WallFaces(Entity Wall, Vec2 A, Vec2 U, Vec2 N, double Length, double Lo, double Hi, double Base, double Top);

    static IEnumerable<WallFaces> WallsOn(IModelView m, string? storeyId)
    {
        foreach (var w in m.OfClass(IfcClass.Wall))
        {
            if (Spatial.StoreyOf(m, w.Id)?.Id != storeyId || PathCurve.Of(w).IsArc) continue;
            var (a, b) = (Walls.Start(w), Walls.End(w));
            var len = (b - a).Length;
            if (len < 0.05) continue;
            var bands = Walls.Section(m, w);
            if (bands.Count == 0) continue;
            var u = (b - a) * (1 / len);
            var z = w.Num("baseOffset");
            yield return new WallFaces(w, a, u, u.Perp, len, bands.Min(x => x.From), bands.Max(x => x.To), z, z + Walls.HeightOf(m, w));
        }
    }

    /// <summary>The wall face nearest a point within reach: the point on it, the rotation that turns a fan's front (local −y) out of it, the wall.</summary>
    public static (Vec2 At, double Rotation, WallFaces Wall)? FaceNear(IModelView m, string? storeyId, Vec2 p, double reach)
    {
        (Vec2, double, WallFaces, double)? best = null;
        foreach (var w in WallsOn(m, storeyId))
        {
            var t = Vec2.Dot(p - w.A, w.U);
            if (t < 0.05 || t > w.Length - 0.05) continue;
            var off = Vec2.Dot(p - w.A, w.N);
            var (face, outN) = off >= (w.Lo + w.Hi) / 2 ? (w.Hi, w.N) : (w.Lo, -w.N);
            var dist = Math.Abs(off - face);
            if (dist > reach) continue;
            if (best is { } bb && dist >= bb.Item4) continue;
            var at = w.A + w.U * t + w.N * face;
            best = (at, Math.Atan2(outN.X, -outN.Y) * 180 / Math.PI, w, dist);
        }
        return best is { } x ? (x.Item1, x.Item2, x.Item3) : null;
    }

    /// <summary>The wall an upright fan sits on: the one whose face holds its point, its normal along the fan's back (within 2 cm and 5°).</summary>
    public static WallFaces? HostOf(IModelView m, Entity fan)
    {
        if (Args.TryPoint(fan.Attrs["at"]) is not { } at) return null;
        var rot = fan.Num("rotation") * Math.PI / 180;
        var back = new Vec2(-Math.Sin(rot), Math.Cos(rot));
        foreach (var w in WallsOn(m, Spatial.StoreyOf(m, fan.Id)?.Id))
        {
            var t = Vec2.Dot(at - w.A, w.U);
            if (t < 0 || t > w.Length) continue;
            var off = Vec2.Dot(at - w.A, w.N);
            var along = Vec2.Dot(back, w.N);
            if (Math.Abs(off - w.Lo) < 0.02 && along > 0.99) return w;
            if (Math.Abs(off - w.Hi) < 0.02 && along < -0.99) return w;
        }
        return null;
    }

    // ---- The body ----

    /// <summary>Where a ceiling or inline fan's duct leaves it (world), the way it leaves (plan), and its height over the storey.</summary>
    public static (Vec3 At, Vec2 Dir, double Level) Spigot(IModelView m, Entity fan, FanSpec s)
    {
        var at = Args.PointOf(fan.Attrs["at"]);
        var b = new LocalBuilder(at, Spatial.StoreyOf(m, fan.Id)?.Num("elevation") ?? 0, fan.Num("rotation"));
        var (x, level) = SpigotLocal(s, fan.Num("height"));
        var p = b.W(x, 0, level);
        var dir = (b.Q(1, 0) - b.Q(0, 0)).Normalized();
        return (p, dir, level);
    }

    static (double X, double Level) SpigotLocal(FanSpec s, double height)
    {
        var d = s.Diameter;
        if (s.Inline) return (s.Num("bodyLength", 0.3) / 2 + 0.05, height);
        var box = Math.Max(s.FaceSize.W * 0.6, d + 0.08);
        return (box / 2 + 0.08, height + 0.09);
    }

    public static ElementGeometry? Geometry(Entity e, IModelView m)
    {
        if (Args.TryPoint(e.Attrs["at"]) is not { } at) return null;
        var s = SpecOf(m, e);
        var storeyZ = Spatial.StoreyOf(m, e.Id)?.Num("elevation") ?? 0;
        var b = new LocalBuilder(at, storeyZ, e.Num("rotation"));
        var host = s.Upright ? HostOf(m, e) : null;
        var thickness = host is { } w ? w.Hi - w.Lo : 0;
        Run? duct = null;
        if (!s.Upright && e.Str("ductId") is { } did && m.Get(did) is { } run && Runs.IsRun(run))
        {
            var line = Runs.Centreline(m, run, Runs.SettingOf(m, run), out _);
            if (line.Count >= 2) duct = new Run(line, e.Str("outlet") ?? "wall", e.NumOrNull("outletRise") ?? 1.0);
        }
        Build(b, s, e.Num("height"), thickness, duct);
        var parts = new List<GeometryPart>();
        foreach (var (role, (mat, color)) in Roles(s))
        {
            if (!b.Solids.TryGetValue(role, out var solids) || solids.Count == 0) continue;
            var material = Materials.Find(m, mat);
            parts.Add(new GeometryPart(char.ToUpperInvariant(role[0]) + role[1..], material?.Str("color") ?? color, LocalBuilder.Combine(solids, "fan"), material?.Id, "fan:" + role));
        }
        return new ElementGeometry(parts, b.Plan, [at]);
    }

    /// <summary>The material roles, with the material and colour each takes from the type's finish.</summary>
    static IEnumerable<(string Role, (string Material, string Color) Material)> Roles(FanSpec s) =>
    [
        ("grille", FanKinds.FinishMaterials.GetValueOrDefault(s.Finish, FanKinds.FinishMaterials["white"])),
        ("housing", FanKinds.Housing), ("light", FanKinds.Light), ("panel", FanKinds.Panel), ("outlet", FanKinds.Housing),
    ];

    sealed record Run(IReadOnlyList<Vec3> Line, string Outlet, double Rise);

    /// <summary>A fan's body and plan symbol in its frame: its face, grille, housing, sleeve through the wall, shutter, light, spigot and outlet.</summary>
    static void Build(LocalBuilder b, FanSpec s, double h, double wall, Run? duct)
    {
        var d = s.Diameter;
        var r = d / 2;
        var (fw, fh) = s.FaceSize;
        if (s.Upright)
        {
            // The face on the wall (y from −depth to 0), the sleeve through the wall (0 to its thickness), the shutter at its far end.
            var window = s.Mounting == "window";
            var depth = window ? 0.03 : 0.02;
            if (s.Kind.Key == "louvrePanel") LouvrePanel(b, s, h, fw, fh, depth);
            else Face(b, s, (0, h), fw, depth);
            var through = wall > 0.01 ? wall : window ? 0.06 : 0.1;
            var fx = s.Kind.Key == "louvrePanel" ? fw / 4 : 0;
            var fz = s.Kind.Key == "louvrePanel" ? h + fh / 2 - fw / 4 - 0.02 : h;
            b.CylinderY("housing", fx, fz, r, 0, through);
            b.CylinderY("housing", fx, fz, r * 0.35, -depth - 0.012, 0.04, 12);
            if (s.Backdraft) b.CylinderY("housing", fx, fz, r - 0.004, through - 0.012, through - 0.006, 16);
            // Outside, a wall fan's grille on the outer face.
            if (wall > 0.01 && !window)
            {
                b.Box("housing", fx - r - 0.03, fx + r + 0.03, through, through + 0.015, fz - r - 0.03, fz + r + 0.03);
                for (var k = 0; k < 3; k++) b.Box("housing", fx - r - 0.02, fx + r + 0.02, through + 0.015, through + 0.03, fz - r + 0.02 + k * (2 * r - 0.04) / 2, fz - r + 0.03 + k * (2 * r - 0.04) / 2);
            }
            // Plan: the face, the fan's circle in front of it with its blades, the sleeve dashed through the wall and the arrow out.
            b.Line("hit", (-fw / 2, -0.25), (fw / 2, -0.25), (fw / 2, through), (-fw / 2, through), (-fw / 2, -0.25));
            b.Rect("symbol", -fw / 2, fw / 2, -depth, 0);
            var cy = -depth - r - 0.04;
            b.Circle("symbol", fx, cy, r + 0.02);
            Blades(b, fx, cy, r);
            b.Line("dashed", (fx - r, 0), (fx - r, through));
            b.Line("dashed", (fx + r, 0), (fx + r, through));
            Arrow(b, (fx, cy), (fx, through + 0.18));
            return;
        }
        if (s.Inline)
        {
            var len = s.Num("bodyLength", 0.3);
            b.CylinderX("housing", 0, h, r + 0.05, -len / 2, len / 2, 20);
            b.CylinderX("housing", 0, h, r, -len / 2 - 0.05, -len / 2);
            b.CylinderX("housing", 0, h, r, len / 2, len / 2 + 0.05);
            b.Rect("hit", -len / 2 - 0.05, len / 2 + 0.05, -r - 0.05, r + 0.05);
            b.Rect("overhead", -len / 2, len / 2, -r - 0.05, r + 0.05);
            b.Line("overhead", (-len / 2 - 0.05, -r), (-len / 2 - 0.05, r));
            b.Line("overhead", (len / 2 + 0.05, -r), (len / 2 + 0.05, r));
            b.Circle("symbol", 0, 0, r * 0.8);
            Blades(b, 0, 0, r * 0.75);
            Arrow(b, (-len / 4, -r - 0.12), (len / 4, -r - 0.12));
            Outlet(b, s, duct);
            return;
        }
        // On the ceiling: the face under it (h − 15 mm to h), the box above, the spigot towards local +x, the light in the middle.
        var box = Math.Max(fw * 0.6, d + 0.08);
        var plate = s.Square
            ? (IReadOnlyList<(double X, double Y)>)[(-fw / 2, -fw / 2), (fw / 2, -fw / 2), (fw / 2, fw / 2), (-fw / 2, fw / 2)]
            : [.. Enumerable.Range(0, 28).Select(i => { var a = 2 * Math.PI * i / 28; return (fw / 2 * Math.Cos(a), fw / 2 * Math.Sin(a)); })];
        b.Prism("grille", [.. plate.Select(p => (p.X, p.Y, h - 0.015))], (0, 0, 0.015));
        b.Front.Add(("grille", [(-fw / 2, h - 0.015), (fw / 2, h - 0.015), (fw / 2, h), (-fw / 2, h)], -fw / 2));
        // The pattern: slots or louvres across it, rings round it; plain: none.
        var inner = fw * 0.38;
        switch (s.Str("pattern"))
        {
            case "slots" or "louvres":
                for (var k = -3; k <= 3; k++) b.Prism("housing", [(-inner, k * inner / 3.5 - 0.004, h - 0.02), (inner, k * inner / 3.5 - 0.004, h - 0.02), (inner, k * inner / 3.5 + 0.004, h - 0.02), (-inner, k * inner / 3.5 + 0.004, h - 0.02)], (0, 0, 0.005));
                break;
            case "rings":
                b.CylinderZ("housing", 0, 0, inner, h - 0.02, h - 0.015, 24);
                break;
        }
        if (s.HasLight)
        {
            b.CylinderZ("light", 0, 0, fw * 0.32, h - 0.03, h - 0.015, 24);
            b.Circle("symbol", 0, 0, fw * 0.32);
            b.Line("symbol", (-fw * 0.22, -fw * 0.22), (fw * 0.22, fw * 0.22));
            b.Line("symbol", (-fw * 0.22, fw * 0.22), (fw * 0.22, -fw * 0.22));
        }
        b.Box("housing", -box / 2, box / 2, -box / 2, box / 2, h, h + 0.16);
        var (sx, sz) = SpigotLocal(s, h);
        b.CylinderX("housing", 0, sz, r, box / 2, sx);
        if (s.Backdraft) b.CylinderX("housing", 0, sz, r - 0.004, sx - 0.012, sx - 0.006);
        b.Loop("hit", plate);
        b.Loop("overhead", plate);
        b.Circle("symbol", 0, 0, r + 0.01);
        Blades(b, 0, 0, r);
        b.Line("overhead", (box / 2, -r), (sx, -r));
        b.Line("overhead", (box / 2, r), (sx, r));
        Outlet(b, s, duct);
    }

    /// <summary>An upright face: a round or square plate on the wall, its pattern standing out of it.</summary>
    static void Face(LocalBuilder b, FanSpec s, (double X, double Z) c, double size, double depth)
    {
        var half = size / 2;
        IReadOnlyList<(double X, double Z)> outline = s.Square
            ? [(c.X - half, c.Z - half), (c.X + half, c.Z - half), (c.X + half, c.Z + half), (c.X - half, c.Z + half)]
            : [.. Enumerable.Range(0, 28).Select(i => { var a = 2 * Math.PI * i / 28; return (c.X + half * Math.Cos(a), c.Z + half * Math.Sin(a)); })];
        b.Slab("grille", outline, -depth, 0);
        Pattern(b, s.Str("pattern"), c, size * 0.38, -depth);
    }

    /// <summary>A grille's pattern in front of its face (y0): louvres (horizontal blades), slots (vertical), rings, or none.</summary>
    static void Pattern(LocalBuilder b, string pattern, (double X, double Z) c, double inner, double y0)
    {
        switch (pattern)
        {
            case "louvres":
                for (var k = -3; k <= 3; k++) b.Box("grille", c.X - inner, c.X + inner, y0 - 0.006, y0, c.Z + k * inner / 3.5 - 0.005, c.Z + k * inner / 3.5 + 0.005);
                break;
            case "slots":
                for (var k = -3; k <= 3; k++) b.Box("housing", c.X + k * inner / 3.5 - 0.004, c.X + k * inner / 3.5 + 0.004, y0 - 0.003, y0, c.Z - inner, c.Z + inner);
                break;
            case "rings":
            {
                IReadOnlyList<(double, double)> Ring(double rr) => [.. Enumerable.Range(0, 24).Select(i => { var a = 2 * Math.PI * i / 24; return (c.X + rr * Math.Cos(a), c.Z + rr * Math.Sin(a)); })];
                b.Slab("grille", Ring(inner), y0 - 0.006, y0, [Ring(inner - 0.008)]);
                b.Slab("grille", Ring(inner * 0.6), y0 - 0.006, y0, [Ring(inner * 0.6 - 0.008)]);
                break;
            }
        }
    }

    /// <summary>The reference's panel (report #55): a frame, louvres on its left half, the fan's round grille top right, a dark panel under it.</summary>
    static void LouvrePanel(LocalBuilder b, FanSpec s, double h, double w, double ht, double depth)
    {
        var (z0, z1) = (h - ht / 2, h + ht / 2);
        b.Box("grille", -w / 2, w / 2, -depth, 0, z0, z1);
        var m = 0.03;
        // Louvres: seven blades in the left half, standing out of the frame.
        var n = 7;
        var step = (ht - 2 * m) / n;
        for (var k = 0; k < n; k++) b.Box("grille", -w / 2 + m, -m / 2, -depth - 0.012, -depth, z0 + m + k * step + step * 0.2, z0 + m + k * step + step * 0.8);
        // The fan's round grille in the right half's top, the dark panel below it.
        var fd = w / 2 - 2 * m;
        Face(b, s, (w / 4, z1 - m - fd / 2), fd, depth + 0.012);
        b.Box("panel", m / 2, w / 2 - m, -depth - 0.004, -depth, z0 + m, z1 - 2 * m - fd);
    }

    /// <summary>A fan's blades in plan: three curved lines from its hub.</summary>
    static void Blades(LocalBuilder b, double cx, double cy, double r)
    {
        for (var k = 0; k < 3; k++)
        {
            var a0 = k * 120.0;
            b.Arc("symbol", cx + r * 0.45 * Math.Cos(a0 * Math.PI / 180), cy + r * 0.45 * Math.Sin(a0 * Math.PI / 180), r * 0.45, a0 + 180, a0 + 300, 6);
        }
    }

    /// <summary>An arrow in plan: the way the air goes.</summary>
    static void Arrow(LocalBuilder b, (double X, double Y) from, (double X, double Y) to)
    {
        b.Line("symbol", from, to);
        var (dx, dy) = (to.X - from.X, to.Y - from.Y);
        var l = Math.Sqrt(dx * dx + dy * dy);
        if (l < 1e-6) return;
        (dx, dy) = (dx / l, dy / l);
        var k = 0.05;
        b.Line("symbol", (to.X - dx * k - dy * k * 0.6, to.Y - dy * k + dx * k * 0.6), to, (to.X - dx * k + dy * k * 0.6, to.Y - dy * k - dx * k * 0.6));
    }

    /// <summary>The duct's outlet, at the run's far end: a hood on the wall (its louvres down) or a cowl over a pipe through the roof.</summary>
    static void Outlet(LocalBuilder b, FanSpec s, Run? duct)
    {
        if (duct == null) return;
        var end = duct.Line[^1];
        var prev = duct.Line[^2];
        var dir = (end.Xy - prev.Xy).Length > 1e-6 ? (end.Xy - prev.Xy).Normalized() : new Vec2(1, 0);
        var r = s.Diameter / 2;
        // In the run's own frame, its local +y along the duct.
        var ob = new LocalBuilder(end.Xy, end.Z, Math.Atan2(-dir.X, dir.Y) * 180 / Math.PI);
        if (duct.Outlet == "roof")
        {
            ob.CylinderZ("outlet", 0, 0, r, -r, duct.Rise, 16);
            ob.CylinderZ("outlet", 0, 0, r * 1.9, duct.Rise + 0.08, duct.Rise + 0.1, 16);
            ob.Frustum("outlet", -r * 1.9, r * 1.9, -r * 1.9, r * 1.9, duct.Rise + 0.1, duct.Rise + 0.2, r * 1.6);
            ob.Circle("symbol", 0, 0, r * 1.9);
            ob.Circle("symbol", 0, 0, r);
        }
        else
        {
            var hw = r + 0.05;
            ob.Box("outlet", -hw, hw, 0, 0.012, -hw, hw);
            ob.Box("outlet", -hw, hw, 0.012, 0.09, hw - 0.012, hw);
            ob.Box("outlet", -hw, -hw + 0.012, 0.012, 0.09, -hw, hw);
            ob.Box("outlet", hw - 0.012, hw, 0.012, 0.09, -hw, hw);
            for (var k = 0; k < 3; k++) ob.Box("outlet", -hw + 0.012, hw - 0.012, 0.03 + k * 0.02, 0.04 + k * 0.02, hw - 0.04 - k * 0.05, hw - 0.03 - k * 0.05);
            ob.Rect("symbol", -hw, hw, 0, 0.09);
        }
        foreach (var (role, solids) in ob.Solids) foreach (var x in solids) { if (!b.Solids.TryGetValue(role, out var l)) b.Solids[role] = l = []; l.Add(x); }
        b.Plan.AddRange(ob.Plan);
    }

    static JsonObject Preview(IModelView m, LocalBuilder b, FanSpec s)
    {
        var colors = Roles(s).ToDictionary(x => x.Role, x => Materials.Find(m, x.Material.Material)?.Str("color") ?? x.Material.Color);
        static double R(double v) => Math.Round(v, 5);
        var xs = b.Front.SelectMany(f => f.Outline.Select(p => p.X)).DefaultIfEmpty(0).ToList();
        var zs = b.Front.SelectMany(f => f.Outline.Select(p => p.Z)).DefaultIfEmpty(0).ToList();
        return new JsonObject
        {
            ["plan"] = new JsonArray([.. b.Plan.Where(i => i.Kind != "hit").Select(i => (JsonNode)new JsonObject
            {
                ["kind"] = i.Kind, ["closed"] = i.Closed, ["points"] = new JsonArray([.. i.Points.SelectMany(p => new[] { (JsonNode)R(p.X), R(p.Y) })]),
            })]),
            ["front"] = new JsonArray([.. b.Front.Select((f, i) => (f, i)).OrderByDescending(x => x.f.Y).Select(x => (JsonNode)new JsonObject
            {
                ["role"] = x.f.Role, ["color"] = colors.GetValueOrDefault(x.f.Role, "#999999"),
                ["points"] = new JsonArray([.. x.f.Outline.SelectMany(p => new[] { (JsonNode)R(p.X), R(p.Z) })]),
                ["holes"] = b.FrontHoles.TryGetValue(x.i, out var hs) ? new JsonArray([.. hs.Select(h => (JsonNode)new JsonArray([.. h.SelectMany(p => new[] { (JsonNode)R(p.X), R(p.Z) })]))]) : null,
            })]),
            ["width"] = R(xs.Max() - xs.Min()), ["height"] = R(zs.Max() - zs.Min()), ["airflow"] = s.Airflow, ["mounting"] = s.Mounting,
        };
    }
}

/// <summary>The built-in fan types (report #55's kinds and variants), each a kind with its spec, offered beside the project's own types.</summary>
public sealed record FanPreset(string Key, string Name, string Kind, JsonObject Spec)
{
    public JsonObject ToJson() => new() { ["key"] = Key, ["name"] = Name, ["kind"] = Kind, ["spec"] = Spec.DeepClone() };
}

public static class FanPresets
{
    static JsonObject S(params (string Key, JsonNode? Value)[] v) { var o = new JsonObject(); foreach (var (k, x) in v) o[k] = x; return o; }

    public static readonly IReadOnlyList<FanPreset> All =
    [
        new("wall100", "Wall fan Ø100, switch", "wallAxial", S(("diameter", "100"), ("airflow", 95.0), ("control", "switch"))),
        new("wall100h", "Wall fan Ø100, humidity sensor", "wallAxial", S(("diameter", "100"), ("airflow", 95.0), ("control", "humidity"))),
        new("wall125t", "Wall fan Ø125, timer", "wallAxial", S(("diameter", "125"), ("faceSize", 0.19), ("airflow", 180.0), ("power", 20.0), ("control", "timer"))),
        new("wall150ht", "Wall fan Ø150, humidity and timer", "wallAxial", S(("diameter", "150"), ("grille", "square"), ("faceSize", 0.22), ("pattern", "louvres"), ("airflow", 300.0), ("power", 28.0), ("control", "humidityTimer"))),
        new("ceiling100", "Ceiling fan Ø100 with duct", "ceilingDuct", S(("diameter", "100"), ("airflow", 110.0))),
        new("ceiling125t", "Ceiling fan Ø125 with duct, timer", "ceilingDuct", S(("diameter", "125"), ("faceSize", 0.3), ("airflow", 160.0), ("control", "timer"))),
        new("ceilingLight", "Ceiling fan with light, motion sensor", "ceilingLight", S(("control", "motion"))),
        new("inline125", "Inline fan Ø125", "inline", S(("diameter", "125"))),
        new("inline150", "Inline fan Ø150, two bathrooms", "inline", S(("diameter", "150"), ("airflow", 420.0), ("power", 45.0), ("bodyLength", 0.34))),
        new("window150", "Window fan Ø150", "window", S(("diameter", "150"))),
        new("panel", "Fan with louvre panel (white)", "louvrePanel", S(("finish", "white"))),
        new("wallSteel", "Wall fan Ø100, stainless", "wallAxial", S(("finish", "stainless"), ("grille", "square"), ("faceSize", 0.16), ("pattern", "slots"))),
    ];

    public static FanPreset? Find(string? key) => All.FirstOrDefault(p => p.Key == key || p.Name == key);
}

/// <summary>Fans moved, turned and copied; a fan with a duct carries the duct's start with it, and its height changed moves the duct's level.</summary>
sealed class FanPlacement : IElementPlacement, IElementUpdate
{
    public bool Handles(Entity e) => e.Class == ExhaustFans.Class;

    public void Transform(ICommandContext ctx, Entity e, Placement2D t, IReadOnlySet<string> moving)
    {
        var attrs = Placement.Carry(e, t);
        var after = e.With(attrs: attrs);
        ctx.Replace(after);
        if (e.Str("ductId") is { } did && !moving.Contains(did)) Follow(ctx, after);
    }

    public string? Copy(ICommandContext ctx, Entity e, Placement2D t)
    {
        var id = Placement.DeepCopy(ctx, e, t);
        // The copy has no duct of its own.
        if (ctx.Model.Get(id) is { } copy && copy.Attrs.ContainsKey("ductId"))
        {
            var attrs = copy.CloneAttrs();
            attrs.Remove("ductId"); attrs.Remove("outlet"); attrs.Remove("outletRise");
            ctx.Replace(copy.With(attrs: attrs));
        }
        return id;
    }

    public void Updated(ICommandContext ctx, Entity before, Entity after)
    {
        if (after.Class != ExhaustFans.Class || after.Str("ductId") == null) return;
        if (before.Num("height") == after.Num("height") && before.Attrs["at"]?.ToJsonString() == after.Attrs["at"]?.ToJsonString() && before.Num("rotation") == after.Num("rotation")) return;
        Follow(ctx, after);
    }

    /// <summary>The duct's start (and the spigot's leg) where the fan's spigot is now, its levels the spigot's.</summary>
    static void Follow(ICommandContext ctx, Entity fan)
    {
        if (ctx.Model.Get(fan.Str("ductId") ?? "") is not { } run || !Runs.IsRun(run)) return;
        var s = ExhaustFans.SpecOf(ctx.Model, fan);
        var (start, dir, level) = ExhaustFans.Spigot(ctx.Model, fan, s);
        var pts = Runs.Points(run);
        if (pts.Count < 2) return;
        pts[0] = start.Xy;
        if (pts.Count >= 3) pts[1] = start.Xy + dir * 0.2;
        var attrs = run.CloneAttrs();
        attrs["points"] = Runs.J(pts);
        attrs["levels"] = new JsonArray([.. pts.Select(_ => (JsonNode)Math.Round(level, 4))]);
        ctx.Replace(run.With(attrs: attrs));
    }
}

/// <summary>A wall fan's round provision through the wall it sits on (decisions 290, 296): the duct's size and 20 mm, through both faces.</summary>
sealed class FanProvisions : IProvisionOpenings
{
    public IEnumerable<ProvisionOpening> Of(IModelView m, IServices services, Entity? host, double offset = 0)
    {
        if (host != null && host.Class != IfcClass.Wall) return [];
        var o = new List<ProvisionOpening>();
        foreach (var fan in m.OfClass(ExhaustFans.Class))
        {
            var s = ExhaustFans.SpecOf(m, fan);
            if (s.Mounting != "wall" || fan.Str("phaseDemolished") != null) continue;
            if (ExhaustFans.HostOf(m, fan) is not { } w || (host != null && w.Wall.Id != host.Id)) continue;
            var at = Args.PointOf(fan.Attrs["at"]);
            var f = Openings.FrameOf(m, w.Wall);
            var u = Vec2.Dot(at - f.Start, f.Dir);
            // A louvre panel's fan stands in its right half, high: where its sleeve is.
            var rot = fan.Num("rotation") * Math.PI / 180;
            var x = new Vec2(Math.Cos(rot), Math.Sin(rot));
            var (fw, fh) = s.FaceSize;
            var shift = s.Kind.Key == "louvrePanel" ? fw / 4 : 0;
            u += Vec2.Dot(x, f.Dir) * shift;
            var zc = fan.Num("height") - w.Base + (s.Kind.Key == "louvrePanel" ? fh / 2 - fw / 4 - 0.02 : 0);
            var size = s.Diameter + 0.02 + 2 * offset;
            var exact = Round(f, u, zc, size, f.YMin, f.YMax);
            var cutter = Round(f, u, zc, size, f.YMin - 0.05, f.YMax + 0.05);
            if (exact == null || cutter == null) continue;
            o.Add(new ProvisionOpening($"fan:{fan.Id}", fan.Id, w.Wall.Id, OpeningVoids.Wall, OpeningVoids.Round, size, size, f.YMax - f.YMin, exact, cutter, Spatial.StoreyOf(m, w.Wall.Id)?.Id) { Along = f.Dir });
        }
        return o;
    }

    static Solid? Round(Openings.WallFrame f, double u, double zc, double size, double y0, double y1)
    {
        const int sides = 32;
        var r = size / 2 / Math.Cos(Math.PI / sides);
        var outline = Enumerable.Range(0, sides).Select(k => f.At(u + r * Math.Cos(2 * Math.PI * k / sides), y0, zc + r * Math.Sin(2 * Math.PI * k / sides))).ToList();
        return Prism.Extrude(outline, null, f.At(0, y1, 0) - f.At(0, y0, 0), "provision");
    }
}
