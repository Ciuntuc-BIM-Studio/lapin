using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture.Gates;

/// <summary>
/// Gates (testers' report #57, docs/user/site.md, Gates): a <c>LapinGate</c> written as an IfcDoor of PredefinedType GATE, of a
/// <c>LapinGateType</c> (IfcDoorType) whose <c>spec</c> holds its kind (a swing gate, a double one, a sliding one, a
/// cantilever one, a pedestrian gate, a double gate with a pedestrian door beside it, a folding one), its clear width and
/// height, its gap to the ground, its leaves (frame, infill of bars, slats, a solid panel, mesh or perforated sheet; a flat,
/// arched or pointed top), its piers (masonry with a cap, steel posts, none), its motor and lock, and its materials by role.
/// Lapin has no fences: the piers stand for the fence's ends, and a gate set on a wall (a fence wall) cuts it over its
/// whole width. A gate stands at <c>at</c> (the middle of its opening on its line), turned by <c>rotation</c> (its local x
/// along the line, −y the street), its leaves swinging to <c>swing</c> (in: +y) on the <c>hand</c> its hinges are, or
/// sliding to that hand, where its parking zone is drawn.
/// </summary>
public static class Gates
{
    public const string Class = "LapinGate";
    public const string TypeClass = "LapinGateType";
    public const string Article = "LAP-GATE", MotorArticle = "LAP-GATEMOT";

    static readonly string[] SwingKinds = ["swing", "doubleSwing", "pedestrian", "swingWicket", "folding"];
    static readonly string[] HandKinds = ["swing", "pedestrian", "sliding", "cantilever", "swingWicket"];

    public static void Register(IModuleRegistry r)
    {
        r.Class(new(Class, "Gate",
        [
            new("rotation", "Rotation (°)", "number"),
            new("offset", "Above the ground", "length", Min: -3, Max: 3),
            new("swing", "Opens to", "choice", Choices: ["in", "out"], ShowIf: FieldDescriptor.When("gateKind", SwingKinds)),
            new("hand", "Hinges or parks on the", "choice", Choices: ["left", "right"], ShowIf: FieldDescriptor.When("gateKind", HandKinds)),
            new("open", "Drawn open (°, 0: closed)", "number", Min: 0, Max: 180, ShowIf: FieldDescriptor.When("gateKind", SwingKinds)),
        ], "#4d5b66"));
        r.Class(new(TypeClass, "Gate type", [], "#4d5b66", Kind: "type"));
        r.Geometry(Class, (e, m, _) => Geometry(e, m));
        r.Quantities(Class, (e, g, m) =>
        {
            var s = SpecOf(m, e);
            return [new QuantitySet("Lapin_GateQuantities", [new("Count", 1, "COUNT", "Count"), new("Width", s.Width + s.WicketWidth, "LENGTH", "Clear width"), new("Height", s.Height, "LENGTH", "Height"), new("Area", (s.Width + s.WicketWidth) * s.Height, "AREA", "Area")])];
        });
        r.Catalog("gateKinds", GateKinds.Catalog());
        r.Catalog("gateParams", GateKinds.ParamsCatalog());
        r.Catalog("gateStyles", new JsonArray([.. GateStyles.All.Select(x => (JsonNode)x.ToJson())]));
        Generator.SymbolicInPlan.Add(Class);
        r.Service<IGeometryFilter>(new GateCuts());
        r.Service<IElementPlacement>(new GatePlacement());
        r.Service<IScheduleSource>(new GateSchedule());

        // A type of a kind or from a built-in style: {kind | style, name?, spec?} → {id}.
        r.Command("createGateType", "New gate type", (ctx, a) =>
        {
            if ((string?)a["style"] is { } sk)
            {
                var style = GateStyles.Find(sk) ?? throw new CommandException($"There is no built-in gate style „{sk}”.");
                return new JsonObject { ["id"] = CreateType(ctx, GateKinds.Find(style.Kind)!, (string?)a["name"] ?? style.Name, Over(style.Spec, a["spec"] as JsonObject)).Id };
            }
            var kind = GateKinds.Find((string?)a["kind"]) ?? throw new CommandException($"A gate is one of {string.Join(", ", GateKinds.All.Select(k => k.Key))}.");
            return new JsonObject { ["id"] = CreateType(ctx, kind, (string?)a["name"], a["spec"] as JsonObject).Id };
        });

        // A type changed: {id, name?, spec?}; its gates follow, and its articles while they are Lapin's.
        r.Command("updateGateType", "Change gate type", (ctx, a) =>
        {
            var type = RequireType(ctx.Model, (string?)a["id"]);
            var spec = (type.Attrs["spec"] as JsonObject)?.DeepClone() as JsonObject ?? GateKinds.NewSpec(GateKinds.All[0], null);
            var before = new GateSpec((JsonObject)spec.DeepClone());
            if (a["spec"] is JsonObject given) GateKinds.Merge(spec, given, before.Kind);
            var s = new GateSpec(spec);
            EnsureMaterials(ctx, s);
            var attrs = type.CloneAttrs();
            attrs["spec"] = spec;
            if (Codes(attrs["articles"]) == Codes(Articles(before))) attrs["articles"] = Articles(s);
            var name = ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : type.Name;
            ctx.Replace(type.With(name: name, attrs: attrs));
            if (name != type.Name)
                foreach (var e in ctx.Model.RelatedOf(type.Id, IfcClass.RelDefinesByType).Where(x => x.Class == Class && x.Name == type.Name).ToList())
                    ctx.Replace(e.With(name: name));
            return null;
        });

        r.Command("duplicateGateType", "Duplicate gate type", (ctx, a) =>
        {
            var type = RequireType(ctx.Model, (string?)a["id"]);
            var name = ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : Unique(ctx.Model, type.Name + " copy");
            var copy = new Entity(ctx.NewId(), TypeClass, name, type.CloneAttrs(), type.PredefinedType);
            ctx.Add(copy);
            return new JsonObject { ["id"] = copy.Id };
        });

        // A gate on a line: {storeyId, typeId | kind | style, x, y, x2?, y2?, rotation?, onLine?} → {id, on}. Two points: the gate between
        // them (its middle, its direction); one: on the nearest wall's axis or drawn line within 1.5 m (onLine), else turned as given.
        r.Command("placeGate", "Gate", (ctx, a) =>
        {
            var storey = Spatial.Require(ctx.Model, (string?)a["storeyId"] ?? Spatial.Storeys(ctx.Model).FirstOrDefault()?.Id, IfcClass.BuildingStorey);
            var type = TypeFor(ctx, (string?)a["typeId"], (string?)a["kind"], (string?)a["style"]);
            var p = Args.Point(a, "x", "y");
            var rotation = Json.Number(a["rotation"]) ?? 0;
            string? on = null;
            var onLine = a["onLine"] is not JsonValue ov || !ov.TryGetValue<bool>(out var ol) || ol;
            var two = Json.Number(a["x2"]) is { } x2 && Json.Number(a["y2"]) is { } y2 && (new Vec2(x2, y2) - p).Length > 0.1 ? new Vec2(x2, y2) : (Vec2?)null;
            if (two is { } q) (p, rotation) = ((p + q) * 0.5, Math.Atan2(q.Y - p.Y, q.X - p.X) * 180 / Math.PI);
            // On a line: its middle onto the line, along it (the way it was drawn, when two points say it).
            if (onLine && LineNear(ctx.Model, storey.Id, p, 1.5) is { } hit)
            {
                var flip = two != null && Math.Cos((hit.Rotation - rotation) * Math.PI / 180) < 0;
                (p, rotation, on) = (hit.At, flip ? hit.Rotation + 180 : hit.Rotation, hit.Id);
            }
            var id = Add(ctx, storey, type, p, rotation);
            if ((string?)a["swing"] is "in" or "out" || (string?)a["hand"] is "left" or "right")
            {
                var e = ctx.Model.Get(id)!;
                var attrs = e.CloneAttrs();
                if ((string?)a["swing"] is "in" or "out") attrs["swing"] = (string)a["swing"]!;
                if ((string?)a["hand"] is "left" or "right") attrs["hand"] = (string)a["hand"]!;
                ctx.Replace(e.With(attrs: attrs));
            }
            return new JsonObject { ["id"] = id, ["on"] = on };
        });

        r.Command("setGateType", "Change type", (ctx, a) =>
        {
            var type = RequireType(ctx.Model, (string?)a["typeId"]);
            var s = new GateSpec(type.Attrs["spec"] as JsonObject);
            foreach (var id in Args.Ids(a))
            {
                var e = ctx.Model.Get(id) is { Class: Class } x ? x : throw new CommandException("Only a gate takes a gate type.");
                var named = e.Name == TypeOf(ctx.Model, e)?.Name;
                ctx.Relate(IfcClass.RelDefinesByType, type.Id, e.Id);
                var attrs = e.CloneAttrs();
                attrs["gateKind"] = s.Kind.Key;
                attrs["objectType"] = s.Kind.Label;
                ctx.Replace(e.With(name: named ? type.Name : e.Name, attrs: attrs));
            }
            return null;
        });

        // Flipped in place: {ids, axis: swing | hand}: the leaves open to the other side, or the hinges (the parking) on the other hand.
        r.Command("flipGate", "Flip", (ctx, a) =>
        {
            var axis = (string?)a["axis"] ?? "swing";
            foreach (var id in Args.Ids(a))
            {
                var e = ctx.Model.Get(id) is { Class: Class } x ? x : throw new CommandException("Only a gate flips here.");
                var attrs = e.CloneAttrs();
                if (axis == "hand") attrs["hand"] = e.Str("hand") == "left" ? "right" : "left";
                else attrs["swing"] = e.Str("swing") == "out" ? "in" : "out";
                ctx.Replace(e.With(attrs: attrs));
            }
            return null;
        });

        // What a spec looks like, for the configurator: {kind | style, spec?} → {plan: [...], front: [{role, color, points}], width, height, parking?}.
        r.Query("gatePreview", (q, a) =>
        {
            JsonObject spec;
            if ((string?)a["style"] is { } sk && GateStyles.Find(sk) is { } style) spec = GateKinds.NewSpec(GateKinds.Find(style.Kind)!, style.Spec);
            else spec = GateKinds.NewSpec(GateKinds.Find((string?)a["kind"]) ?? throw new CommandException("Which kind?"), null);
            if (a["spec"] is JsonObject given) { var g = (JsonObject)given.DeepClone(); g.Remove("kind"); GateKinds.Merge(spec, g, new GateSpec(spec).Kind); }
            var s = new GateSpec(spec);
            var b = new LocalBuilder(new Vec2(0, 0), 0, 0);
            Build(b, s, "in", (string?)a["hand"] ?? "right", null);
            return Preview(q.Model, b, s);
        });
    }

    static JsonObject? Over(JsonObject style, JsonObject? given)
    {
        var o = (JsonObject)style.DeepClone();
        foreach (var (k, v) in given ?? []) o[k] = v?.DeepClone();
        return o;
    }

    // ---- The model ----

    public static Entity? TypeOf(IModelView m, Entity e) => m.RelatingOf(e.Id, IfcClass.RelDefinesByType) is { Class: TypeClass } t ? t : null;
    public static GateSpec SpecOf(IModelView m, Entity e) => new(TypeOf(m, e)?.Attrs["spec"] as JsonObject);

    static Entity RequireType(IModelView m, string? id) => m.Get(id ?? "") is { Class: TypeClass } t ? t : throw new CommandException("There is no such gate type.");

    static string Unique(IModelView m, string name)
    {
        var taken = m.OfClass(TypeClass).Select(t => t.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!taken.Contains(name)) return name;
        for (var i = 2; ; i++) if (!taken.Contains($"{name} {i}")) return $"{name} {i}";
    }

    static string Mm(double v) => Math.Round(v * 1000).ToString(CultureInfo.InvariantCulture);

    /// <summary>A type's name from its kind and size: Sliding gate 4000 × 1600.</summary>
    static string DefaultName(GateSpec s) => $"{s.Kind.Label} {Mm(s.Width + s.WicketWidth)} × {Mm(s.Height)}";

    /// <summary>Lapin's articles for a type: the gate, and its motor when it has one (the indicative price list's codes).</summary>
    static JsonArray Articles(GateSpec s)
    {
        var o = new JsonArray(new JsonObject { ["code"] = Article, ["basis"] = "count" });
        if (s.Motor) o.Add((JsonNode)new JsonObject { ["code"] = MotorArticle, ["basis"] = "count" });
        return o;
    }

    static string Codes(JsonNode? articles) => articles is JsonArray a ? string.Join("|", a.Select(x => (string?)x?["code"])) : "";

    static void EnsureMaterials(IEdit edit, GateSpec s)
    {
        foreach (var role in GateKinds.Roles)
        {
            var name = s.MaterialName(role);
            if (Materials.Find(edit.Model, name) != null) continue;
            var color = GateKinds.RoleDefaults.Values.FirstOrDefault(v => v.Material == name).Color ?? GateKinds.KnownColors.GetValueOrDefault(name);
            Materials.Ensure(edit, name, color);
        }
    }

    public static Entity CreateType(IEdit edit, GateKind kind, string? name, JsonObject? given)
    {
        var g = given?.DeepClone() as JsonObject;
        g?.Remove("kind");
        var spec = GateKinds.NewSpec(kind, g);
        var s = new GateSpec(spec);
        EnsureMaterials(edit, s);
        var attrs = new JsonObject { ["spec"] = spec, ["articles"] = Articles(s), ["ifcOperation"] = kind.Operation };
        if (kind.UserOperation != null) attrs["ifcUserOperation"] = kind.UserOperation;
        var type = new Entity(edit.NewId(), TypeClass, Unique(edit.Model, string.IsNullOrWhiteSpace(name) ? DefaultName(s) : name.Trim()), attrs, "GATE");
        edit.Add(type);
        return type;
    }

    /// <summary>The type asked for; a built-in style's (made); else the project's first of the kind; else a new one of the kind.</summary>
    public static Entity TypeFor(IEdit edit, string? typeId, string? kind, string? style = null)
    {
        if (typeId != null) return RequireType(edit.Model, typeId);
        if (style != null)
        {
            var st = GateStyles.Find(style) ?? throw new CommandException($"There is no built-in gate style „{style}”.");
            return edit.Model.OfClass(TypeClass).FirstOrDefault(t => t.Name == st.Name) ?? CreateType(edit, GateKinds.Find(st.Kind)!, st.Name, st.Spec);
        }
        var k = GateKinds.Find(kind ?? "doubleSwing") ?? throw new CommandException($"A gate is one of {string.Join(", ", GateKinds.All.Select(x => x.Key))}.");
        return edit.Model.OfClass(TypeClass).FirstOrDefault(t => new GateSpec(t.Attrs["spec"] as JsonObject).Kind.Key == k.Key) ?? CreateType(edit, k, null, null);
    }

    public static string Add(IEdit edit, Entity storey, Entity type, Vec2 at, double rotation)
    {
        var s = new GateSpec(type.Attrs["spec"] as JsonObject);
        var attrs = new JsonObject
        {
            ["at"] = new JsonArray(Math.Round(at.X, 5), Math.Round(at.Y, 5)), ["rotation"] = Math.Round(((rotation % 360) + 360) % 360, 4),
            ["swing"] = "in", ["hand"] = "right", ["gateKind"] = s.Kind.Key, ["objectType"] = s.Kind.Label, ["exportAs"] = IfcClass.Door,
        };
        var e = new Entity(edit.NewId(), Class, type.Name, attrs, "GATE");
        edit.Add(e);
        Spatial.Contain(edit, storey.Id, e.Id);
        edit.Relate(IfcClass.RelDefinesByType, type.Id, e.Id);
        return e.Id;
    }

    // ---- Lines ----

    /// <summary>The nearest line within reach a gate may stand on: a straight wall's axis on the storey, or a drawn line or outline (a parcel's edge) on it or on none.</summary>
    public static (Vec2 At, double Rotation, string Id)? LineNear(IModelView m, string storeyId, Vec2 p, double reach)
    {
        (Vec2, double, string, double)? best = null;
        void Try(string id, Vec2 a, Vec2 b)
        {
            var d = b - a;
            var len = d.Length;
            if (len < 0.2) return;
            var u = d * (1 / len);
            var t = Math.Clamp(Vec2.Dot(p - a, u), 0, len);
            var q = a + u * t;
            var dist = (p - q).Length;
            if (dist > reach || (best is { } bb && dist >= bb.Item4)) return;
            best = (q, Math.Atan2(u.Y, u.X) * 180 / Math.PI, id, dist);
        }
        foreach (var e in m.Entities)
        {
            if (e.Class == Class || e.Class.StartsWith("IfcDuct", StringComparison.Ordinal) || e.Class.StartsWith("IfcPipe", StringComparison.Ordinal) || e.Class.StartsWith("IfcCable", StringComparison.Ordinal)) continue;
            var storey = Spatial.StoreyOf(m, e.Id)?.Id;
            // A terrain's parcel (Site → Parcel) on whichever level the terrain is: its outline's edges.
            if (e.Attrs["parcel"] is JsonArray parcel && Args.StoredPoints(parcel) is { Count: >= 3 } outline)
            {
                for (var i = 0; i < outline.Count; i++) Try(e.Id, outline[i], outline[(i + 1) % outline.Count]);
                continue;
            }
            if (storey != null && storey != storeyId) continue;
            if (e.Class == IfcClass.Wall)
            {
                if (storey == storeyId && !PathCurve.Of(e).IsArc) Try(e.Id, Walls.Start(e), Walls.End(e));
                continue;
            }
            if (e.Attrs["points"] is JsonArray pts && pts.Count >= 2 && !e.Attrs.ContainsKey("systemId"))
            {
                var list = Args.StoredPoints(pts);
                for (var i = 1; i < list.Count; i++) Try(e.Id, list[i - 1], list[i]);
                if (list.Count >= 3 && (e.Bool("closed") || e.Str("kind") is "parcel" or "area" or "building")) Try(e.Id, list[^1], list[0]);
            }
            else if (storey == storeyId && Args.TryPoint(e.Attrs["start"]) is { } s0 && Args.TryPoint(e.Attrs["end"]) is { } s1) Try(e.Id, s0, s1);
        }
        return best is { } x ? (x.Item1, x.Item2, x.Item3) : null;
    }

    /// <summary>The wall a gate stands in: a straight wall on its storey along its line, its axis through the gate's middle (within its thickness).</summary>
    public static Entity? HostOf(IModelView m, Entity gate)
    {
        if (Args.TryPoint(gate.Attrs["at"]) is not { } at) return null;
        var rot = gate.Num("rotation") * Math.PI / 180;
        var dir = new Vec2(Math.Cos(rot), Math.Sin(rot));
        var storey = Spatial.StoreyOf(m, gate.Id)?.Id;
        foreach (var w in m.OfClass(IfcClass.Wall))
        {
            if (Spatial.StoreyOf(m, w.Id)?.Id != storey || PathCurve.Of(w).IsArc) continue;
            var (a, b) = (Walls.Start(w), Walls.End(w));
            var len = (b - a).Length;
            if (len < 0.1) continue;
            var u = (b - a) * (1 / len);
            if (Math.Abs(Vec2.Cross(u, dir)) > Math.Sin(3 * Math.PI / 180)) continue;
            var t = Vec2.Dot(at - a, u);
            if (t < 0 || t > len) continue;
            var bands = Walls.Section(m, w);
            if (bands.Count == 0) continue;
            var off = Vec2.Dot(at - a, u.Perp);
            if (off >= bands.Min(x => x.From) - 0.02 && off <= bands.Max(x => x.To) + 0.02) return w;
        }
        return null;
    }

    // ---- The body ----

    public static ElementGeometry? Geometry(Entity e, IModelView m)
    {
        if (Args.TryPoint(e.Attrs["at"]) is not { } at) return null;
        var s = SpecOf(m, e);
        var z = (Spatial.StoreyOf(m, e.Id)?.Num("elevation") ?? 0) + e.Num("offset");
        var hand = e.Str("hand") ?? "right";
        // The left hand is the right one mirrored along the gate.
        var b = new LocalBuilder(at, z, e.Num("rotation"), hand == "left");
        Build(b, s, e.Str("swing") ?? "in", hand, e.NumOrNull("open"));
        var parts = new List<GeometryPart>();
        foreach (var role in GateKinds.Roles)
        {
            if (!b.Solids.TryGetValue(role, out var solids) || solids.Count == 0) continue;
            var mat = Materials.Find(m, s.MaterialName(role));
            var color = mat?.Str("color") ?? GateKinds.RoleDefaults[role].Color;
            parts.Add(new GeometryPart(char.ToUpperInvariant(role[0]) + role[1..], color, LocalBuilder.Combine(solids, "gate"), mat?.Id, "gate:" + role));
        }
        var half = s.Overall / 2;
        return new ElementGeometry(parts, b.Plan, [at, b.Q(-half, 0), b.Q(half, 0)]);
    }

    /// <summary>The openings along the gate (local x): the main one, and a pedestrian door's beside it on the right.</summary>
    static List<(double X0, double X1, bool Wicket)> Openings(GateSpec s)
    {
        if (s.Kind.Key != "swingWicket") return [(-s.Width / 2, s.Width / 2, false)];
        var total = s.Width + s.PierSize + s.WicketWidth;
        return [(-total / 2, -total / 2 + s.Width, false), (total / 2 - s.WicketWidth, total / 2, true)];
    }

    /// <summary>
    /// A gate's body and plan in its frame (the right hand; the left is it mirrored): its piers and caps, its leaves closed
    /// (each a frame round its infill, the top flat, arched or pointed over the whole opening), hinges, a lock, a motor, a
    /// sliding gate's track; in plan the piers, the leaves drawn open with their swing, or closed with the way they slide
    /// and the zone they park in.
    /// </summary>
    static void Build(LocalBuilder b, GateSpec s, string swing, string hand, double? open)
    {
        var side = swing == "out" ? -1.0 : 1.0;
        var p = s.PierSize;
        var openings = Openings(s);
        // Piers at both ends of every opening.
        var piers = new List<double>();
        foreach (var (x0, x1, _) in openings) { piers.Add(x0 - p / 2); piers.Add(x1 + p / 2); }
        if (p > 0)
            foreach (var px in piers.Distinct().Select(x => Math.Round(x, 6)).Distinct())
            {
                b.Box("pier", px - p / 2, px + p / 2, -p / 2, p / 2, 0, s.PierHeight);
                b.Rect("heavy", px - p / 2, px + p / 2, -p / 2, p / 2);
                if (s.Piers == "masonry")
                {
                    var c = p / 2 + 0.03;
                    switch (s.Str("cap", "pyramid"))
                    {
                        case "flat": b.Box("cap", px - c, px + c, -c, c, s.PierHeight, s.PierHeight + 0.06); break;
                        case "pyramid":
                            b.Box("cap", px - c, px + c, -c, c, s.PierHeight, s.PierHeight + 0.04);
                            b.Frustum("cap", px - c, px + c, -c, c, s.PierHeight + 0.04, s.PierHeight + 0.04 + c * 0.6, c - 0.01);
                            break;
                    }
                }
                else b.Box("frame", px - p / 2 - 0.01, px + p / 2 + 0.01, -p / 2 - 0.01, p / 2 + 0.01, s.PierHeight, s.PierHeight + 0.008);
            }
        var f = s.Frame;
        var (gx0, gx1) = (openings.Min(o => o.X0), openings.Max(o => o.X1));
        // The top over the whole gate: its rise at the middle (arched: a circle's segment; pointed: straight to a point).
        double Top(double x)
        {
            if (s.Top == "flat" || s.Kind.Slides) return s.Height;
            var (a0, a1) = openings[0].Wicket ? (gx0, gx1) : (openings[0].X0, openings[0].X1);
            var t = Math.Clamp((x - a0) / (a1 - a0), 0, 1);
            var rise = s.Num("rise", 0.25);
            return s.Top == "arched" ? s.Height + rise * Math.Sin(Math.PI * t) : s.Height + rise * (1 - Math.Abs(2 * t - 1));
        }
        var hw = Math.Max(0.02, f * 0.6);
        b.Rect("hit", gx0 - p, gx1 + p, -Math.Max(p / 2, 0.15), Math.Max(p / 2, 0.15));
        if (s.Kind.Slides)
        {
            var (z0, l) = (s.Gap, s.LeafLength);
            // Closed in front of the piers on the side it slides on (+y in, −y out): over the opening and past the piers.
            var y = side * (p / 2 + 0.03 + f / 2);
            var lx0 = s.Kind.Key == "sliding" ? -s.Width / 2 - 0.1 : -s.Width / 2 - 0.05;
            Leaf(b, s, lx0, lx0 + l, y, Top, z0, f);
            if (s.Kind.Key == "sliding")
                b.Box("hardware", lx0, s.Width / 2 + l, y - 0.015, y + 0.015, 0, Math.Min(0.03, z0));
            else
            {
                // A cantilever leaf rides on two roller carriages beside the opening, its tail between them.
                foreach (var cx in new[] { s.Width / 2 + p + 0.25, s.Width / 2 + p + 0.25 + Math.Max(0.6, 0.35 * s.Width) })
                    b.Box("hardware", cx - 0.15, cx + 0.15, y - 0.1, y + 0.1, 0, Math.Max(0.05, z0 - 0.005));
            }
            if (s.Motor) b.Box("motor", s.Width / 2 + p + 0.05, s.Width / 2 + p + 0.35, y + side * 0.05 - 0.12, y + side * 0.05 + 0.12, 0, 0.35);
            if (s.Lock) b.Box("hardware", lx0 + 0.02, lx0 + 0.06, y - f / 2 - 0.03, y + f / 2 + 0.03, 1.0, 1.15);
            // Plan: the leaf closed, an arrow the way it opens, the zone it parks in (as long as the leaf) dashed, the track.
            b.Rect("symbol", lx0, lx0 + l, y - f / 2, y + f / 2);
            var (k0, k1) = s.ParkingZone!.Value;
            b.Rect("dashed", k0, k1, y - 0.12, y + 0.12);
            b.Line("dashed", (lx0, y), (k1, y));
            var ay = y + side * 0.3;
            b.Line("symbol", (-s.Width / 4, ay), (s.Width / 4, ay));
            b.Line("symbol", (s.Width / 4 - 0.12, ay - 0.06), (s.Width / 4, ay), (s.Width / 4 - 0.12, ay + 0.06));
            if (s.Motor) b.Rect("symbol", s.Width / 2 + p + 0.05, s.Width / 2 + p + 0.35, y + side * 0.05 - 0.12, y + side * 0.05 + 0.12);
            return;
        }
        var angle = Math.Clamp(open ?? s.OpeningAngle, 0, 180);
        foreach (var (x0, x1, wicket) in openings)
        {
            var span = x1 - x0;
            // The leaves of this opening, each by its hinge: one leaf hinged on the right (a pedestrian door on its outer pier), two meeting in the middle, or folding panels.
            var leaves = new List<(double Hinge, double Width, int Dir)>();
            if (wicket || s.Kind.Key is "swing" or "pedestrian") leaves.Add((x1 - 0.01, span - 0.02, -1));
            else if (s.Kind.Key == "folding")
            {
                var each = (span / 2 - 0.02) / s.Panels;
                for (var k = 0; k < s.Panels; k++) { leaves.Add((x0 + 0.01 + k * each, each, 1)); leaves.Add((x1 - 0.01 - k * each, each, -1)); }
            }
            else { leaves.Add((x0 + 0.01, span / 2 - 0.015, 1)); leaves.Add((x1 - 0.01, span / 2 - 0.015, -1)); }
            foreach (var (hinge, width, dir) in leaves)
            {
                var (lx0, lx1) = dir > 0 ? (hinge, hinge + width) : (hinge - width, hinge);
                Leaf(b, s, lx0, lx1, 0, Top, s.Gap, f);
                // Two hinges on the hinge side (a folding panel's on its outer edge too).
                foreach (var hz in new[] { s.Gap + 0.2, Math.Min(s.Height, Top(hinge)) - 0.2 })
                    b.CylinderZ("hardware", hinge, 0, Math.Max(0.012, f * 0.3), hz - 0.06, hz + 0.06, 8);
            }
            // The lock where the leaves meet (a single leaf's: at its free edge).
            if (s.Lock && s.Kind.Key != "folding")
            {
                var lx = leaves.Count == 1 ? (leaves[0].Dir > 0 ? x1 - 0.03 : x0 + 0.03) : (x0 + x1) / 2;
                b.Box("hardware", lx - 0.06, lx + 0.06, -f / 2 - 0.03, f / 2 + 0.03, 0.95, 1.15);
            }
            if (s.Motor && !wicket)
                foreach (var (hinge, width, dir) in leaves.Take(s.Kind.Key == "folding" ? 0 : 2))
                    b.Box("motor", Math.Min(hinge, hinge + dir * width * 0.6), Math.Max(hinge, hinge + dir * width * 0.6), side * (f / 2 + 0.05) - 0.05, side * (f / 2 + 0.05) + 0.05, 0.85, 0.95);
            // Plan: each leaf open by the angle on its side, its swing's arc; a folding gate's panels folded in a zig-zag.
            if (s.Kind.Key == "folding")
            {
                var each = (span / 2 - 0.02) / s.Panels;
                foreach (var (start, dir) in new[] { (x0 + 0.01, 1), (x1 - 0.01, -1) })
                {
                    var pts = new List<(double, double)> { (start, 0) };
                    for (var k = 0; k < s.Panels; k++)
                    {
                        var a = (k % 2 == 0 ? 80 : 10) * Math.PI / 180;
                        var (px, py) = pts[^1];
                        pts.Add((px + dir * each * Math.Cos(a) * (k % 2 == 0 ? 1 : 1), py + side * each * Math.Sin(a)));
                    }
                    b.Line("symbol", [.. pts]);
                    b.Arc("symbol", start, 0, each, dir > 0 ? 0 : 180, dir > 0 ? side * 80 : 180 - side * 80, 10);
                }
            }
            else
                foreach (var (hinge, width, dir) in leaves)
                {
                    var a0 = dir > 0 ? 0.0 : 180.0;
                    var a1 = a0 + dir * side * angle;
                    var rad = a1 * Math.PI / 180;
                    var tip = (hinge + width * Math.Cos(rad), width * Math.Sin(rad));
                    var n = (-Math.Sin(rad) * f / 2, Math.Cos(rad) * f / 2);
                    b.Loop("symbol", [(hinge + n.Item1, n.Item2), (tip.Item1 + n.Item1, tip.Item2 + n.Item2), (tip.Item1 - n.Item1, tip.Item2 - n.Item2), (hinge - n.Item1, -n.Item2)]);
                    b.Arc("symbol", hinge, 0, width, a0, a1, 16);
                }
            // The closed line of the opening, thin.
            b.Line("symbol", (x0, 0), (x1, 0));
        }
    }

    /// <summary>A leaf from x0 to x1 in the plane y (local): its frame round its infill, from z0 up to the top at each x.</summary>
    static void Leaf(LocalBuilder b, GateSpec s, double x0, double x1, double y, Func<double, double> top, double z0, double f)
    {
        if (x1 - x0 < 2 * f + 0.02) return;
        var flat = s.Top == "flat" || s.Kind.Slides;
        var steps = flat ? 1 : 12;
        IReadOnlyList<(double X, double Z)> Outline(double inset)
        {
            var o = new List<(double X, double Z)> { (x0 + inset, z0 + inset), (x1 - inset, z0 + inset) };
            for (var i = 0; i <= steps; i++)
            {
                var x = x1 - inset - (x1 - x0 - 2 * inset) * i / steps;
                o.Add((x, top(x) - inset));
            }
            // Drop repeated corners (a flat top's ends are the outline's).
            return o.Where((pt, i) => i == 0 || Math.Abs(pt.X - o[i - 1].X) > 1e-6 || Math.Abs(pt.Z - o[i - 1].Z) > 1e-6).ToList();
        }
        var outer = Outline(0);
        var inner = Outline(f);
        b.Slab("frame", outer, y - f / 2, y + f / 2, [inner]);
        var (ix0, ix1, iz0) = (x0 + f, x1 - f, z0 + f);
        double IT(double x) => top(x) - f;
        var spacing = Math.Max(0.05, s.Num("spacing", 0.12));
        switch (s.Infill)
        {
            case "verticalBars":
            {
                var bar = Math.Max(0.012, f * 0.33);
                var n = Math.Max(1, (int)Math.Floor((ix1 - ix0) / spacing));
                var step = (ix1 - ix0) / (n + 1);
                for (var k = 1; k <= n; k++)
                {
                    var bx = ix0 + k * step;
                    var spear = s.Bool("spears");
                    var zTop = spear ? top(bx) + 0.08 : IT(bx);
                    b.Box("infill", bx - bar / 2, bx + bar / 2, y - bar / 2, y + bar / 2, iz0, zTop);
                    if (spear) b.Frustum("infill", bx - bar, bx + bar, y - bar, y + bar, zTop, zTop + 0.07, bar - 0.002);
                }
                break;
            }
            case "horizontalSlats":
            {
                var slat = spacing * 0.7;
                var lowTop = Enumerable.Range(0, 9).Select(i => IT(ix0 + (ix1 - ix0) * i / 8)).Min();
                for (var zz = iz0 + (spacing - slat) / 2; zz + slat <= lowTop + 1e-6; zz += spacing)
                    b.Box("infill", ix0, ix1, y - 0.012, y + 0.012, zz, zz + slat);
                break;
            }
            default:
            {
                var t = s.Infill == "solid" ? Math.Min(0.03, f * 0.5) : 0.004;
                b.Slab("infill", inner, y - t / 2, y + t / 2);
                break;
            }
        }
    }

    static JsonObject Preview(IModelView m, LocalBuilder b, GateSpec s)
    {
        static double R(double v) => Math.Round(v, 5);
        var colors = GateKinds.Roles.ToDictionary(role => role, role => Materials.Find(m, s.MaterialName(role))?.Str("color") ?? GateKinds.KnownColors.GetValueOrDefault(s.MaterialName(role)) ?? GateKinds.RoleDefaults[role].Color);
        var xs = b.Front.SelectMany(f => f.Outline.Select(p => p.X)).DefaultIfEmpty(0).ToList();
        var zs = b.Front.SelectMany(f => f.Outline.Select(p => p.Z)).DefaultIfEmpty(0).ToList();
        return new JsonObject
        {
            ["plan"] = new JsonArray([.. b.Plan.Where(i => i.Kind != "hit").Select(i => (JsonNode)new JsonObject
            {
                ["kind"] = i.Kind, ["closed"] = i.Closed, ["points"] = new JsonArray([.. i.Points.SelectMany(p => new[] { (JsonNode)R(p.X), R(p.Y) })]),
            })]),
            // Front to back, so the nearer pieces are drawn over the farther.
            ["front"] = new JsonArray([.. b.Front.Select((f, i) => (f, i)).OrderByDescending(x => x.f.Y).Select(x => (JsonNode)new JsonObject
            {
                ["role"] = x.f.Role, ["color"] = colors.GetValueOrDefault(x.f.Role, "#999999"),
                ["points"] = new JsonArray([.. x.f.Outline.SelectMany(p => new[] { (JsonNode)R(p.X), R(p.Z) })]),
                ["holes"] = b.FrontHoles.TryGetValue(x.i, out var hs) ? new JsonArray([.. hs.Select(h => (JsonNode)new JsonArray([.. h.SelectMany(p => new[] { (JsonNode)R(p.X), R(p.Z) })]))]) : null,
            })]),
            ["width"] = R(xs.Max() - xs.Min()), ["height"] = R(zs.Max() - zs.Min()), ["overall"] = R(s.Overall),
            ["parking"] = s.ParkingZone is { } z ? R(z.X1 - z.X0) : null, ["leaf"] = s.Kind.Slides ? R(s.LeafLength) : null,
        };
    }
}

/// <summary>A gate set on a wall (a fence wall) cuts it over its width from its piers' outer faces, from the wall's foot to its top.</summary>
sealed class GateCuts : IGeometryFilter
{
    public ElementGeometry? Apply(Entity entity, ElementGeometry geometry, IModelView model, IServices services)
    {
        if (entity.Class != IfcClass.Wall || geometry.Parts.Count == 0) return null;
        if (services.Get<GeometryOptions>()?.CutOpenings == false) return null;
        var gates = model.OfClass(Gates.Class).Where(g => g.Str("phaseDemolished") == null && Gates.HostOf(model, g)?.Id == entity.Id).ToList();
        if (gates.Count == 0) return null;
        if (services.Get<ISolidBooleans>() is not { } booleans) return geometry with { Diagnostics = [.. geometry.Diagnostics ?? [], "A gate's cut needs the boolean library (manifold), missing here."] };
        var bands = Walls.Section(model, entity);
        var z = (Spatial.StoreyOf(model, entity.Id)?.Num("elevation") ?? 0) + entity.Num("baseOffset");
        var top = z + Walls.HeightOf(model, entity);
        var (a, b) = (Walls.Start(entity), Walls.End(entity));
        var u = (b - a).Normalized();
        var cutters = new List<Solid>();
        foreach (var g in gates)
        {
            var s = Gates.SpecOf(model, g);
            var at = Args.PointOf(g.Attrs["at"]);
            var t = Vec2.Dot(at - a, u);
            var half = s.Overall / 2;
            var (y0, y1) = (bands.Min(x => x.From) - 0.05, bands.Max(x => x.To) + 0.05);
            Vec2[] fp = [a + u * (t - half) + u.Perp * y0, a + u * (t + half) + u.Perp * y0, a + u * (t + half) + u.Perp * y1, a + u * (t - half) + u.Perp * y1];
            if (Prism.ExtrudeFootprint(fp, z - 0.01, top - z + 0.02, "opening") is { } c) cutters.Add(c);
        }
        var parts = new List<GeometryPart>();
        foreach (var part in geometry.Parts)
        {
            try { parts.Add(booleans.Difference(part.Solid, cutters, part.Solid.Tag) is { } cut ? part with { Solid = cut } : part); }
            catch (InvalidOperationException) { parts.Add(part); }
        }
        return geometry with { Parts = parts };
    }
}

/// <summary>Gates moved, turned, mirrored and copied: a mirror swaps its hand, its leaves still opening the way they did.</summary>
sealed class GatePlacement : IElementPlacement
{
    public bool Handles(Entity e) => e.Class == Gates.Class;

    static JsonObject Mirrored(Entity e, JsonObject attrs)
    {
        attrs["hand"] = e.Str("hand") == "left" ? "right" : "left";
        // The plan's mirror turned it about: turned back, its street side where it was.
        attrs["rotation"] = Math.Round((((Json.Number(attrs["rotation"]) ?? 0) + 180) % 360 + 360) % 360, 6);
        attrs["swing"] = e.Str("swing") == "out" ? "in" : "out";
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

/// <summary>The gate schedule: a row per type with its kind, clear size, leaves, infill, top, piers, motor and how many; the total.</summary>
public sealed class GateSchedule : IScheduleSource
{
    public string Kind => "gates";
    public string Label => "Gate schedule";

    static string Leaves(GateSpec s) => s.Kind.Key switch
    {
        "swing" or "pedestrian" => "1", "doubleSwing" => "2", "swingWicket" => "2 + 1", "folding" => $"{2 * s.Panels} panels", _ => "1 sliding",
    };

    public ScheduleGrid Grid(IQueryContext q)
    {
        var m = q.Model;
        static string Mm(double v) => Math.Round(v * 1000).ToString(CultureInfo.InvariantCulture);
        var groups = m.OfClass(Gates.Class).Select(e => (E: e, T: Gates.TypeOf(m, e))).GroupBy(x => x.T?.Id ?? x.E.Id)
            .Select(g =>
            {
                var s = new GateSpec(g.First().T?.Attrs["spec"] as JsonObject);
                var size = s.Kind.Key == "swingWicket" ? $"{Mm(s.Width)} + {Mm(s.WicketWidth)} × {Mm(s.Height)}" : $"{Mm(s.Width)} × {Mm(s.Height)}";
                return (Order: GateKinds.All.ToList().IndexOf(s.Kind), Key: g.First().T?.Id, Row: (IReadOnlyList<string>)
                    [g.First().T?.Name ?? g.First().E.Name, s.Kind.Label, size, Leaves(s), s.Infill, s.Top, s.Piers == "none" ? "none" : $"{s.Piers} {Mm(s.PierSize)}", s.Motor ? "yes" : "no", g.Count().ToString(CultureInfo.InvariantCulture)]);
            })
            .OrderBy(x => x.Order).ThenBy(x => x.Row[0], StringComparer.Ordinal).ToList();
        var rows = groups.Select(x => x.Row).ToList();
        var keys = groups.Select(x => x.Key).ToList();
        if (rows.Count > 0) { rows.Add(["Total", "", "", "", "", "", "", "", groups.Sum(x => int.Parse(x.Row[8], CultureInfo.InvariantCulture)).ToString(CultureInfo.InvariantCulture)]); keys.Add(null); }
        return new ScheduleGrid(Label, ["Type", "Kind", "Clear size (mm)", "Leaves", "Infill", "Top", "Piers", "Motor", "Count"], rows, keys);
    }
}

/// <summary>A built-in gate style: a kind with its spec, offered beside the project's own types (styles).</summary>
public sealed record GateStyle(string Key, string Name, string Kind, JsonObject Spec)
{
    public JsonObject ToJson() => new() { ["key"] = Key, ["name"] = Name, ["kind"] = Kind, ["spec"] = Spec.DeepClone() };
}

public static class GateStyles
{
    static JsonObject S(params (string Key, JsonNode? Value)[] v) { var o = new JsonObject(); foreach (var (k, x) in v) o[k] = x; return o; }
    static JsonObject M(params (string Role, string Material)[] v) => new(v.Select(x => KeyValuePair.Create(x.Role, (JsonNode?)x.Material)));

    public static readonly IReadOnlyList<GateStyle> All =
    [
        new("wroughtIron", "Wrought iron double gate", "doubleSwing", S(("infill", "verticalBars"), ("top", "arched"), ("rise", 0.35), ("spears", true), ("spacing", 0.11), ("cap", "pyramid"),
            ("materials", M(("frame", "Steel painted black"), ("infill", "Steel painted black"))))),
        new("slatSliding", "Horizontal slat sliding gate", "sliding", S(("infill", "horizontalSlats"), ("spacing", 0.1), ("motor", true), ("cap", "flat"), ("materials", M(("pier", "Rendered masonry"))))),
        new("biFold", "Steel bi-fold gate", "folding", S(("infill", "solid"), ("panels", 2))),
        new("archedPedestrian", "Arched pedestrian gate", "pedestrian", S(("infill", "verticalBars"), ("top", "arched"), ("rise", 0.25), ("pierHeight", 2.0))),
        new("timberDriveway", "Timber driveway gate", "doubleSwing", S(("infill", "solid"), ("top", "pointed"), ("rise", 0.2), ("height", 1.8), ("pierHeight", 2.1),
            ("materials", M(("infill", "Larch wood"), ("pier", "Stone cladding"))))),
        new("securityMesh", "Security mesh gate", "doubleSwing", S(("infill", "mesh"), ("piers", "steel"), ("pierSize", 0.1), ("height", 2.0), ("pierHeight", 2.1), ("materials", M(("infill", "Steel mesh galvanised"))))),
        new("cantileverAuto", "Automatic cantilever gate", "cantilever", S(("infill", "verticalBars"), ("motor", true), ("spacing", 0.1))),
        new("withPedestrianDoor", "Double gate with pedestrian door", "swingWicket", S(("infill", "verticalBars"), ("top", "arched"), ("rise", 0.3))),
        new("farm", "Farm gate", "swing", S(("width", 3.6), ("height", 1.2), ("infill", "horizontalSlats"), ("spacing", 0.22), ("piers", "steel"), ("pierSize", 0.12), ("pierHeight", 1.3), ("lock", false),
            ("materials", M(("frame", "Steel galvanised"), ("infill", "Steel galvanised"))))),
        new("perforatedSliding", "Perforated sheet sliding gate", "sliding", S(("infill", "perforated"), ("motor", true), ("piers", "steel"), ("pierSize", 0.12), ("materials", M(("infill", "Perforated steel sheet"))))),
        new("gardenWood", "Garden gate (timber)", "pedestrian", S(("infill", "verticalBars"), ("spacing", 0.1), ("top", "arched"), ("rise", 0.15), ("frame", 0.07), ("pierHeight", 1.4), ("height", 1.2),
            ("materials", M(("frame", "Larch wood"), ("infill", "Larch wood"))))),
    ];

    public static GateStyle? Find(string? key) => All.FirstOrDefault(p => p.Key == key || p.Name == key);
}
