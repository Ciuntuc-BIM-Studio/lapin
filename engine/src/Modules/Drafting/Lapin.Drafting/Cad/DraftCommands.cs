using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Drawing.Hatch;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Drafting.Cad;

/// <summary>The CAD commands: draw, change, move / copy / rotate / mirror / scale, offset, trim, extend, fillet, chamfer, hatch, layers. Each is one undo step.</summary>
public static class DraftCommands
{
    static Vec2 P(JsonNode? n, string what = "point") => n is JsonArray a && a.Count >= 2 && Json.Number(a[0]) is { } x && Json.Number(a[1]) is { } y
        ? new Vec2(x, y) : throw new CommandException($"The {what} is [x, y].");

    static Entity View(ICommandContext ctx, string? id) =>
        Views.Find(ctx.Model, id ?? "") ?? throw new CommandException("There is no such view.");

    static List<string> Ids(JsonObject a) =>
        a["ids"] is JsonArray arr ? arr.Select(x => (string?)x).OfType<string>().ToList() : (string?)a["id"] is { } one ? [one] : [];

    /// <summary>A revision cloud's scallop (paper mm, 1–100); null for none.</summary>
    static double? Cloud(JsonNode? n) => n switch
    {
        null => null,
        JsonValue v when v.TryGetValue<bool>(out var on) => on ? 8 : null,
        _ when Json.Number(n) is { } arc => arc >= 1 && arc <= 100 ? arc : throw new CommandException("A cloud's scallop is 1 to 100 mm."),
        _ => null,
    };

    static Shape ShapeArg(JsonNode? n) => Shape.From(n) ?? throw new CommandException("A shape is a line {a, b}, a polyline {pts, closed}, a circle {c, r} or an arc {c, r, start, end}.");

    /// <summary>drafting.transform's work: drafted shapes and texts moved, copied, turned, mirrored or scaled (calibrateImage scales a PDF's group by it too).</summary>
    public static JsonObject Transform(ICommandContext ctx, JsonObject a)
    {
        var op = (string?)a["op"] ?? "move";
        Func<Vec2, Vec2> point;
        Func<Shape, Shape> shape;
        double turn = 0;
        switch (op)
        {
            case "move" or "copy":
            {
                var d = P(a["by"], "displacement");
                point = p => p + d; shape = s => Geo.Translate(s, d);
                break;
            }
            case "rotate":
            {
                var c = P(a["center"], "centre");
                var ang = (Json.Number(a["angle"]) ?? 0) * Math.PI / 180;
                turn = ang;
                point = p => Geo.Rotate(p, c, ang); shape = s => Geo.Rotate(s, c, ang);
                break;
            }
            case "mirror":
            {
                var (p1, p2) = (P(a["a"]), P(a["b"]));
                if ((p2 - p1).Length < 1e-9) throw new CommandException("A mirror line needs two different points.");
                point = p => Geo.Mirror(p, p1, p2); shape = s => Geo.Mirror(s, p1, p2);
                break;
            }
            case "scale":
            {
                var c = P(a["center"], "centre");
                var k = Json.Number(a["factor"]) ?? 1;
                if (!(Math.Abs(k) > 1e-9)) throw new CommandException("The scale factor cannot be 0.");
                point = p => c + (p - c) * k; shape = s => Geo.Scale(s, c, k);
                break;
            }
            default: throw new CommandException($"„{op}” is not move, copy, rotate, mirror or scale.");
        }
        var copy = op == "copy" || (a["copy"] is JsonValue cv && cv.TryGetValue<bool>(out var cb) && cb);
        var made = new JsonArray();
        foreach (var id in Ids(a))
        {
            // A grid axis (grid#tag): its two ends; a copy is the grid's next axis.
            if (ctx.Model.Get(id) == null && new AxisPlacement().Transform(ctx, id, new Placement2D(point, turn, false), copy) is { } axis)
            {
                if (copy) made.Add((JsonNode)axis);
                continue;
            }
            var e = ctx.Model.Get(id) ?? throw new CommandException($"There is no {id}.");
            if (e.Class != IfcClass.Annotation) continue; // model elements move by their own tools
            // A pinned shape stays where it is; its copy is not pinned (decision 449).
            if (!copy) Pins.Refuse([e]);
            var attrs = e.CloneAttrs();
            if (e.PredefinedType == Drafts.Predefined)
            {
                Drafts.Require(ctx.Model, id);
                if (Drafts.ShapeOf(e) is { } s) attrs["shape"] = shape(s).ToJson();
                if (e.Attrs["hatch"] is JsonObject h)
                {
                    var loops = Drafts.HatchLoops(e).Select(l => (JsonNode)new JsonArray(l.Select(q => (JsonNode)new JsonArray(point(q).X, point(q).Y)).ToArray())).ToArray();
                    var nh = (JsonObject)h.DeepClone();
                    nh["loops"] = new JsonArray(loops);
                    attrs["hatch"] = nh;
                }
            }
            else if (e.PredefinedType == SymbolInstances.Predefined)
            {
                // A symbol: its point, angle and flips carried (a mirror turns it over, its texts stay readable); a scale its size.
                var place = op switch
                {
                    "mirror" => Placement2D.Mirror(P(a["a"]), P(a["b"])),
                    _ => new Placement2D(point, turn, false),
                };
                // A sheet's own symbol (paper mm, y down), moved on its paper's view, takes the move across.
                SymbolInstances.Carry(attrs, AnnotationPlacement.OnPaper(ctx.Model, attrs, place));
                if (op == "scale" && Json.Number(a["factor"]) is { } k)
                {
                    attrs["scale"] = Math.Round((Json.Number(attrs["scale"]) is > 0 and var s0 ? s0 : 1) * Math.Abs(k), 9);
                    // A negative factor is a half turn about the centre.
                    if (k < 0) attrs["angle"] = Math.Round(((Json.Number(attrs["angle"]) ?? 0) + 180) % 360, 9);
                }
            }
            else if (e.PredefinedType == "TEXT" && attrs["at"] is JsonArray at)
            {
                var q = point(new Vec2(Json.Number(at[0]) ?? 0, Json.Number(at[1]) ?? 0));
                attrs["at"] = new JsonArray(q.X, q.Y);
                attrs["angle"] = (Json.Number(attrs["angle"]) ?? 0) + turn * 180 / Math.PI;
                // Its leader's arrow and bends go with it (decision 620).
                if (Texts.Leader(e) is { Count: > 0 } leader) attrs["leader"] = new JsonArray([.. leader.Select(p => (JsonNode)new JsonArray(point(p).X, point(p).Y))]);
            }
            else if (e.PredefinedType is DrawingKeys.Legend or DrawingKeys.ScaleBar && attrs["at"] is JsonArray kat)
            {
                // A legend or a scale bar: its corner moves; it stays upright.
                var q = point(P(kat, "at"));
                attrs["at"] = new JsonArray(q.X, q.Y);
            }
            else if (Dimensions.IsLinear(e))
            {
                // The whole string; moved, it measures where it is now: it lets go of its elements.
                Dimensions.Write(attrs, Dimensions.Stations(e).Select(s => (point(s.P), (DimEndRef?)null)).ToList());
                if (attrs["at"] is JsonArray at2) attrs["at"] = new JsonArray(point(P(at2, "at")).X, point(P(at2, "at")).Y);
                if (attrs["dir"] is JsonArray dj)
                {
                    var d0 = P(dj, "dir");
                    var dn = point(d0) - point(new Vec2(0, 0));
                    if (dn.Length > 1e-12) attrs["dir"] = new JsonArray(dn.Normalized().X, dn.Normalized().Y);
                }
            }
            else if (e.PredefinedType == "DIMENSION")
            {
                foreach (var k in new[] { "a", "b", "at" })
                    if (attrs[k] is JsonArray pa) { var q = point(new Vec2(Json.Number(pa[0]) ?? 0, Json.Number(pa[1]) ?? 0)); attrs[k] = new JsonArray(q.X, q.Y); }
                // A moved dimension measures where it is now: it lets go of its elements.
                attrs.Remove("refA"); attrs.Remove("refB");
            }
            else continue;
            if (copy)
            {
                attrs.Remove(Pins.Key);
                var n = new Entity(ctx.NewId(), e.Class, e.Name, attrs, e.PredefinedType);
                ctx.Add(n);
                made.Add((JsonNode)n.Id);
            }
            else ctx.Replace(e.With(attrs: attrs));
        }
        return new JsonObject { ["ids"] = made };
    }

    public static void Register(IModuleRegistry r)
    {
        // One or more shapes on a view, on a layer.
        r.Command("draw", "Draw", (ctx, a) =>
        {
            var view = View(ctx, (string?)a["viewId"]);
            var layer = (string?)a["layer"] ?? "0";
            if (Drafts.Layers(ctx.Model).All(l => l.Name != layer)) throw new CommandException($"There is no layer „{layer}”.");
            if (Drafts.LayerOf(ctx.Model, layer).Locked) throw new CommandException($"The layer „{layer}” is locked.");
            var shapes = a["shapes"] is JsonArray arr ? arr.Select(ShapeArg).ToList() : [ShapeArg(a["shape"])];
            var role = Drafts.CheckRole(a["role"]);
            var cloud = Cloud(a["cloud"]);
            var ids = shapes.Select(s =>
            {
                var e = Drafts.Add(ctx, view.Id, layer, s, role);
                if (cloud is { } arc && s is Polyline { Closed: true } or Circle)
                {
                    var attrs = e.CloneAttrs();
                    attrs["cloud"] = arc;
                    ctx.Replace(e.With(name: Drafts.UniqueName(ctx.Model, view.Id, "Cloud"), attrs: attrs));
                }
                return e.Id;
            }).ToList();
            return new JsonObject { ["ids"] = new JsonArray(ids.Select(x => (JsonNode)x).ToArray()) };
        });

        // A shape replaced (a grip moved), moved to another layer, or given its own colour.
        r.Command("updateDraft", "Change", (ctx, a) =>
        {
            foreach (var id in Ids(a))
            {
                var e = Drafts.Require(ctx.Model, id);
                var attrs = e.CloneAttrs();
                if (a["shape"] != null) attrs["shape"] = ShapeArg(a["shape"]).ToJson();
                if ((string?)a["layer"] is { } layer)
                {
                    if (Drafts.Layers(ctx.Model).All(l => l.Name != layer)) throw new CommandException($"There is no layer „{layer}”.");
                    attrs["layer"] = layer;
                }
                if (a.ContainsKey("color")) { if ((string?)a["color"] is { } c) attrs["color"] = c; else attrs.Remove("color"); }
                // A hatch's pattern, scale, angle (°) and origin (where its pattern starts, in the view), merged into what it had.
                if (a["hatch"] is JsonObject hp && attrs["hatch"] is JsonObject had)
                {
                    if ((string?)hp["pattern"] is { } pn)
                    {
                        var name = pn.Trim().ToUpperInvariant();
                        had["pattern"] = HatchLibrary.Find(name, DraftingResources.Patterns(ctx.Model))?.Name ?? throw new CommandException($"There is no hatch pattern „{name}”.");
                    }
                    if (hp.ContainsKey("scale")) had["scale"] = Json.Number(hp["scale"]) is { } hs && hs > 0 ? hs : throw new CommandException("The hatch scale must be positive.");
                    // Its size, on paper or real (report #157); null: as its pattern is. Changed without a scale, the scale is turned
                    // by the view's so the hatch looks as it did at that scale (1 on paper at 1:50 is 50 real).
                    if (hp.ContainsKey("units"))
                    {
                        var pattern = HatchLibrary.Find((string?)had["pattern"] ?? "LINES45", DraftingResources.Patterns(ctx.Model));
                        var was = pattern == null ? null : (HatchUnits?)HatchScale.UnitsOf(pattern, Drafts.UnitsOf(had));
                        HatchUnits? over = hp["units"] is null ? null : HatchScale.Parse((string?)hp["units"]) ?? throw new CommandException("A hatch's units are paper or model.");
                        if (over is { } u) had["units"] = HatchScale.Name(u); else had.Remove("units");
                        var now = pattern == null ? null : (HatchUnits?)HatchScale.UnitsOf(pattern, over);
                        if (!hp.ContainsKey("scale") && was != null && now != null && was != now)
                        {
                            var viewScale = Views.Find(ctx.Model, e.Str("viewId") ?? "")?.NumOrNull("scale") is > 0 and var vs ? vs : 100;
                            var s0 = Json.Number(had["scale"]) ?? 1;
                            had["scale"] = Math.Round(now == HatchUnits.Model ? s0 * viewScale : s0 / viewScale, 9);
                        }
                    }
                    if (hp.ContainsKey("angle")) had["angle"] = Json.Number(hp["angle"]) ?? 0;
                    if (hp.ContainsKey("origin")) { if (hp["origin"] is JsonArray ho && ho.Count >= 2) had["origin"] = new JsonArray(Json.Number(ho[0]) ?? 0, Json.Number(ho[1]) ?? 0); else had.Remove("origin"); }
                    if (hp.ContainsKey("background"))
                    {
                        if ((string?)hp["background"] is { } bg)
                        {
                            if (!HexColor.IsColour(bg)) throw new CommandException("The hatch background must be a colour (#rrggbb).");
                            had["background"] = bg.Trim();
                        }
                        else had.Remove("background");
                    }
                }
                if (a.ContainsKey("role")) { if (Drafts.CheckRole(a["role"]) is { } role) attrs["role"] = role; else attrs.Remove("role"); }
                // A revision cloud's scallop (paper mm), or none: a closed shape drawn plain again.
                if (a.ContainsKey("cloud")) { if (Cloud(a["cloud"]) is { } arc) attrs["cloud"] = arc; else attrs.Remove("cloud"); }
                ctx.Replace(e.With(attrs: attrs));
            }
            return null;
        });

        // Move, copy, rotate, mirror, scale: drafted shapes and texts. A copy returns the new ids.
        r.Command("transform", "Transform", (ctx, a) => Transform(ctx, a));

        r.Command("offset", "Offset", (ctx, a) =>
        {
            var e = Drafts.Require(ctx.Model, (string?)a["id"]);
            var s = Drafts.ShapeOf(e) ?? throw new CommandException("Only lines, polylines, circles and arcs offset.");
            var side = P(a["side"], "side");
            // Through: the copy passes through the point picked (its distance taken from it).
            var d = a["through"] is JsonValue tv && tv.TryGetValue<bool>(out var tb) && tb ? Ops.ThroughDistance(s, side)
                : Json.Number(a["distance"]) ?? throw new CommandException("Offset by how much?");
            if (d <= 1e-9) throw new CommandException("The offset distance must be positive.");
            var o = Ops.Offset(s, d, side) ?? throw new CommandException("The offset would collapse the shape.");
            return new JsonObject { ["id"] = Drafts.Add(ctx, e.Str("viewId")!, e.Str("layer"), o).Id };
        });

        // The piece under the pick goes, between the shapes and the drawing's lines that cross it.
        r.Command("trim", "Trim", (ctx, a) =>
        {
            var e = Drafts.Require(ctx.Model, (string?)a["id"]);
            var s = Drafts.ShapeOf(e) ?? throw new CommandException("Only lines, polylines, circles and arcs trim.");
            var view = View(ctx, e.Str("viewId"));
            var pieces = Ops.Trim(s, Drafts.Boundaries(ctx, view, e.Id), P(a["at"], "pick")) ?? throw new CommandException("Nothing crosses it there.");
            if (pieces.Count == 0) { ctx.Remove(e.Id); return new JsonObject { ["ids"] = new JsonArray() }; }
            Drafts.Set(ctx, e, pieces[0]);
            var ids = new JsonArray(e.Id);
            foreach (var p in pieces.Skip(1))
            {
                var n = Drafts.Add(ctx, e.Str("viewId")!, e.Str("layer"), p);
                if (e.Str("color") is { } c) { var at = n.CloneAttrs(); at["color"] = c; ctx.Replace(n.With(attrs: at)); }
                ids.Add((JsonNode)n.Id);
            }
            return new JsonObject { ["ids"] = ids };
        });

        r.Command("extend", "Extend", (ctx, a) =>
        {
            var e = Drafts.Require(ctx.Model, (string?)a["id"]);
            var s = Drafts.ShapeOf(e) ?? throw new CommandException("Only lines, open polylines and arcs extend.");
            var view = View(ctx, e.Str("viewId"));
            var o = Ops.Extend(s, Drafts.Boundaries(ctx, view, e.Id), P(a["at"], "pick")) ?? throw new CommandException("It meets nothing that way.");
            Drafts.Set(ctx, e, o);
            return null;
        });

        // Two lines joined by an arc (radius 0: a sharp corner) or by a line (chamfer).
        r.Command("fillet", "Fillet", (ctx, a) => Corner(ctx, a, false));
        r.Command("chamfer", "Chamfer", (ctx, a) => Corner(ctx, a, true));

        // Lines and open polylines welded end to end into polylines (ends within the tolerance meet; a chain that comes back closes).
        // Each polyline keeps the first piece's entity (its layer and colour); the other pieces go.
        r.Command("join", "Join", (ctx, a) =>
        {
            var tol = Json.Number(a["tolerance"]) ?? 5e-4;
            var ids = Ids(a);
            if (ids.Count == 0) throw new CommandException("Which shapes?");
            var pieces = new List<(Entity E, IReadOnlyList<Vec2> Pts)>();
            foreach (var id in ids)
            {
                var e = Drafts.Require(ctx.Model, id);
                switch (Drafts.ShapeOf(e))
                {
                    case Line l: pieces.Add((e, [l.A, l.B])); break;
                    case Polyline { Closed: false } p: pieces.Add((e, p.Pts)); break;
                    case Arc arc: pieces.Add((e, Chords.Arc(arc.C, arc.R, arc.Start, arc.Sweep))); break;
                }
            }
            if (pieces.Count == 0) throw new CommandException("Join welds lines and open polylines.");
            var made = new JsonArray();
            var joined = 0;
            foreach (var chain in Joins.Chains(pieces.Select(p => p.Pts).ToList(), tol))
            {
                if (chain.Members.Count < 2 && !chain.Closed) continue;
                if (chain.Pts.Count < 2) continue;
                var keep = pieces[chain.Members.Min()].E;
                Drafts.Set(ctx, keep, chain.Pts.Count == 2 && !chain.Closed ? new Line(chain.Pts[0], chain.Pts[1]) : new Polyline(chain.Pts, chain.Closed && chain.Pts.Count >= 3));
                foreach (var m in chain.Members) if (pieces[m].E.Id != keep.Id) ctx.Remove(pieces[m].E.Id);
                made.Add((JsonNode)keep.Id);
                joined++;
            }
            return new JsonObject { ["ids"] = made, ["joined"] = joined, ["pieces"] = pieces.Count };
        });

        // Polylines (a rectangle is one) split into their lines, the way back from JOIN (report #121, decision 430). Each piece is a
        // shape of its own on the same layer, with the same colour, role and attributes (a DXF's heights split with it), in the
        // group the polyline was in; the first keeps the polyline's entity. Circles and arcs, hatches and dimension strings are not
        // polylines and stay as they are; one undo step.
        r.Command("explode", "Explode", (ctx, a) =>
        {
            var ids = Ids(a);
            if (ids.Count == 0) throw new CommandException("Which shapes?");
            var made = new JsonArray();
            var exploded = 0;
            var kept = 0;
            foreach (var id in ids)
            {
                if (ctx.Model.Get(id) is not { Class: IfcClass.Annotation, PredefinedType: Drafts.Predefined } e || e.Attrs["hatch"] != null || Drafts.ShapeOf(e) is not { } shape) { kept++; continue; }
                if (shape is not Polyline p) { kept++; continue; }
                Drafts.Require(ctx.Model, id);
                var n = p.Pts.Count;
                var heights = e.Attrs["heights"] is JsonArray ha ? ha.Select(z => Json.Number(z) ?? 0).ToList() : null;
                if (heights != null && heights.Count == n - 1 && p.Closed) heights.Add(heights[0]);
                var count = p.Closed ? n : n - 1;
                var pieces = new List<(Vec2 A, Vec2 B, double? Za, double? Zb)>();
                for (var i = 0; i < count; i++)
                {
                    var (ia, ib) = (i, (i + 1) % n);
                    if ((p.Pts[ib] - p.Pts[ia]).Length < 1e-9) continue;
                    pieces.Add((p.Pts[ia], p.Pts[ib], heights != null && ia < heights.Count ? heights[ia] : null, heights != null && ib < heights.Count ? heights[ib] : null));
                }
                if (pieces.Count == 0) { kept++; continue; }
                var view = e.Str("viewId") ?? "";
                var group = Groups.Of(ctx.Model, e.Id);
                var more = new List<string>();
                for (var k = 0; k < pieces.Count; k++)
                {
                    var (pa, pb, za, zb) = pieces[k];
                    var attrs = e.CloneAttrs();
                    var line = new Line(pa, pb);
                    attrs["shape"] = line.ToJson();
                    if (heights != null) { if (za is { } z1 && zb is { } z2) attrs["heights"] = new JsonArray(z1, z2); else attrs.Remove("heights"); }
                    var name = Drafts.UniqueName(ctx.Model, view, "Line");
                    if (k == 0) { ctx.Replace(e.With(name: name, attrs: attrs)); made.Add((JsonNode)e.Id); }
                    else
                    {
                        var piece = new Entity(ctx.NewId(), IfcClass.Annotation, name, attrs, Drafts.Predefined);
                        ctx.Add(piece);
                        more.Add(piece.Id);
                        made.Add((JsonNode)piece.Id);
                    }
                }
                if (group != null && more.Count > 0) ctx.RelateAll(Groups.Rel, group.Id, more);
                exploded++;
            }
            if (exploded == 0) throw new CommandException("Explode splits polylines and rectangles into lines; circles, arcs, hatches and dimensions stay as they are.");
            return new JsonObject { ["ids"] = made, ["exploded"] = exploded, ["pieces"] = made.Count, ["kept"] = kept };
        });

        // A hatch filling the region around a point: the smallest closed outline there, its islands left out.
        r.Command("hatch", "Hatch", (ctx, a) =>
        {
            var view = View(ctx, (string?)a["viewId"]);
            var at = P(a["at"], "point");
            var region = HatchRegion.At(at, Drafts.Loops(ctx, view)) ?? throw new CommandException("There is no closed outline around that point.");
            var pattern = ((string?)a["pattern"])?.Trim().ToUpperInvariant() is { Length: > 0 } pn ? pn : "LINES45";
            if (HatchLibrary.Find(pattern, DraftingResources.Patterns(ctx.Model)) == null) throw new CommandException($"There is no hatch pattern „{pattern}”.");
            var layer = (string?)a["layer"] ?? "0";
            var hatch = new JsonObject
            {
                ["pattern"] = pattern, ["scale"] = Json.Number(a["scale"]) ?? 1, ["angle"] = Json.Number(a["angle"]) ?? 0,
                ["loops"] = new JsonArray(region.Select(l => (JsonNode)new JsonArray(l.Select(q => (JsonNode)new JsonArray(q.X, q.Y)).ToArray())).ToArray()),
            };
            // Its size, on paper or real (report #157); none given: as its pattern is.
            if (a["units"] is not null) hatch["units"] = HatchScale.Name(HatchScale.Parse((string?)a["units"]) ?? throw new CommandException("A hatch's units are paper or model."));
            var attrs = new JsonObject { ["viewId"] = view.Id, ["layer"] = layer, ["hatch"] = hatch };
            if ((string?)a["color"] is { } c) attrs["color"] = c;
            var e = new Entity(ctx.NewId(), IfcClass.Annotation, $"Hatch {pattern}", attrs, Drafts.Predefined);
            ctx.Add(e);
            return new JsonObject { ["id"] = e.Id, ["loops"] = region.Count };
        });

        // ------------------------------------------------------------ layers

        r.Query("drafting.layers", (q, _) => new JsonArray(Drafts.Layers(q.Model).Select(l => (JsonNode)l.ToJson()).ToArray()));

        // A layer made or changed (by its name; rename with newName).
        r.Command("setLayer", "Layer", (ctx, a) =>
        {
            var name = ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : throw new CommandException("A layer needs a name.");
            var layers = Drafts.Layers(ctx.Model).ToList();
            var i = layers.FindIndex(l => l.Name == name);
            var cur = i >= 0 ? layers[i] : new CadLayer(name);
            var json = cur.ToJson();
            foreach (var k in new[] { "color", "weight", "lineType", "visible", "locked" }) if (a[k] != null) json[k] = a[k]!.DeepClone();
            var newName = ((string?)a["newName"])?.Trim();
            if (newName is { Length: > 0 } && newName != name)
            {
                if (name == "0") throw new CommandException("Layer 0 keeps its name.");
                if (layers.Any(l => l.Name == newName)) throw new CommandException($"There is a layer „{newName}” already.");
                json["name"] = newName;
                foreach (var e in ctx.Model.OfClass(IfcClass.Annotation).Where(e => e.PredefinedType == Drafts.Predefined && e.Str("layer") == name).ToList())
                {
                    var at = e.CloneAttrs(); at["layer"] = newName; ctx.Replace(e.With(attrs: at));
                }
            }
            var layer = CadLayer.From(json)!;
            if (i >= 0) layers[i] = layer; else layers.Add(layer);
            Drafts.WriteLayers(ctx, layers);
            return null;
        });

        // A layer removed: its shapes go to layer 0.
        r.Command("removeLayer", "Remove layer", (ctx, a) =>
        {
            var name = (string?)a["name"];
            if (name == "0") throw new CommandException("Layer 0 cannot be removed.");
            var layers = Drafts.Layers(ctx.Model).ToList();
            if (layers.RemoveAll(l => l.Name == name) == 0) throw new CommandException("There is no such layer.");
            foreach (var e in ctx.Model.OfClass(IfcClass.Annotation).Where(e => e.PredefinedType == Drafts.Predefined && e.Str("layer") == name).ToList())
            {
                var at = e.CloneAttrs(); at["layer"] = "0"; ctx.Replace(e.With(attrs: at));
            }
            Drafts.WriteLayers(ctx, layers);
            return null;
        });
    }

    static JsonNode? Corner(ICommandContext ctx, JsonObject a, bool chamfer)
    {
        var (ea, eb) = (Drafts.Require(ctx.Model, (string?)a["a"]), Drafts.Require(ctx.Model, (string?)a["b"]));
        var (pa, pb) = (P(a["atA"], "first pick"), P(a["atB"], "second pick"));
        // A line and an ellipse (either first): filleted on the ellipse's own curve.
        if (!chamfer && (Drafts.ShapeOf(ea), Drafts.ShapeOf(eb)) is (Line, Ellipse) or (Ellipse, Line))
        {
            var lineFirst = Drafts.ShapeOf(ea) is Line;
            var (le, ee) = lineFirst ? (ea, eb) : (eb, ea);
            var (lp, ep) = lineFirst ? (pa, pb) : (pb, pa);
            var fe = Ops.Fillet((Line)Drafts.ShapeOf(le)!, lp, (Ellipse)Drafts.ShapeOf(ee)!, ep, Json.Number(a["radius"]) ?? 0)
                ?? throw new CommandException("No circle of that radius touches the line and the ellipse on the sides picked.");
            Drafts.Set(ctx, le, fe.Line); Drafts.Set(ctx, ee, fe.Ellipse);
            return fe.Arc is { } ea2 ? new JsonObject { ["id"] = Drafts.Add(ctx, le.Str("viewId")!, le.Str("layer"), ea2).Id } : null;
        }
        if (Drafts.ShapeOf(ea) is not Line la || Drafts.ShapeOf(eb) is not Line lb) throw new CommandException(chamfer ? "Chamfer joins two lines." : "Fillet joins two lines, or a line and an ellipse.");
        if (chamfer)
        {
            var c = Ops.Chamfer(la, pa, lb, pb, Json.Number(a["d1"]) ?? 0, Json.Number(a["d2"]) ?? Json.Number(a["d1"]) ?? 0)
                ?? throw new CommandException("The lines are parallel, or too short for that chamfer.");
            Drafts.Set(ctx, ea, c.First); Drafts.Set(ctx, eb, c.Second);
            if ((c.Line.B - c.Line.A).Length > 1e-9) return new JsonObject { ["id"] = Drafts.Add(ctx, ea.Str("viewId")!, ea.Str("layer"), c.Line).Id };
            return null;
        }
        var f = Ops.Fillet(la, pa, lb, pb, Json.Number(a["radius"]) ?? 0) ?? throw new CommandException("The lines are parallel, or too short for that radius.");
        Drafts.Set(ctx, ea, f.First); Drafts.Set(ctx, eb, f.Second);
        return f.Arc is { } arc ? new JsonObject { ["id"] = Drafts.Add(ctx, ea.Str("viewId")!, ea.Str("layer"), arc).Id } : null;
    }
}
