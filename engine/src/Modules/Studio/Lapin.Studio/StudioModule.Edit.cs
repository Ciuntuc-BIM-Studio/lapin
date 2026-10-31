using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Drafting.Cad;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Studio;

public sealed partial class StudioModule
{
    static void RegisterEdits(IModuleRegistry r)
    {
        // The face or the edge of an element under a pick in 3D, as its body is built now: what Studio's face tools light up and work on.
        r.Query("studio.faceAt", (q, a) =>
            Pick(q, a) is var (_, solids, p, n) && BrepPick.Face(solids, p, n) is { } f ? f.ToJson() : null);
        r.Query("studio.edgeAt", (q, a) =>
            Pick(q, a) is var (_, solids, p, n) && BrepPick.EdgeAt(solids, p, n) is { } edge ? edge.ToJson() : null);

        r.Command("pushPull", "Push/pull", PushPull);
        r.Command("setPush", "Push/pull distance", SetPush);
        r.Command("addFeature", "Add a step", AddFeature);
        r.Command("updateFeature", "Change a step", UpdateFeature);
        r.Command("removeFeature", "Remove a step", (ctx, a) =>
        {
            var (e, list, i) = Step(ctx.Model, a);
            list.RemoveAt(i);
            WriteFeatures(ctx, e, list);
            return null;
        });
        r.Command("moveFeature", "Move a step", (ctx, a) =>
        {
            var (e, list, i) = Step(ctx.Model, a);
            var to = (int)(Json.Number(a["to"]) ?? i);
            if (to < 0 || to >= list.Count) throw new CommandException("There is no place there in the list.");
            var f = list[i];
            list.RemoveAt(i);
            list.Insert(to, f);
            WriteFeatures(ctx, e, list);
            return null;
        });
        r.Command("split", "Split", Split);

        // What an extrusion runs up to: {id, upTo: {id, point, normal}} (a face of another element), or none (its depth again).
        r.Command("setUpTo", "Extrude up to", (ctx, a) =>
        {
            var e = Solid(ctx.Model, (string?)a["id"]);
            if (e.Str("operation") != "extrude") throw new CommandException("Only an extrusion runs up to a face.");
            var attrs = e.CloneAttrs();
            if (a["upTo"] is JsonObject upTo) attrs["upTo"] = UpTo(ctx, upTo); else attrs.Remove("upTo");
            ctx.Replace(e.With(attrs: attrs));
            return null;
        });

        // Where a primitive or a body stands: {id, at: [x, y, z]} (the middle of its base), {rotation} (°).
        r.Command("place", "Place", (ctx, a) =>
        {
            var e = Solid(ctx.Model, (string?)a["id"]);
            if (e.Str("operation") is not ("primitive" or "body")) throw new CommandException("A solid drawn from a sketch moves with its sketch.");
            var attrs = e.CloneAttrs();
            if (Features.P(a["at"]) is { } at) attrs["at"] = Features.J(at);
            else if (a["at"] is JsonArray { Count: 2 } xy && Features.P(e.Attrs["at"]) is { } was) attrs["at"] = Features.J(new Vec3(Json.Number(xy[0]) ?? was.X, Json.Number(xy[1]) ?? was.Y, was.Z));
            if (Json.Number(a["rotation"]) is { } rot) attrs["rotation"] = rot;
            ctx.Replace(e.With(attrs: attrs));
            return null;
        });
        r.Command("fromElement", "Make a Studio solid", FromElement);
    }

    /// <summary>An element's built body and a pick on it: {id, point, normal}.</summary>
    static (Entity E, List<Solid> Solids, Vec3 Point, Vec3 Normal)? Pick(IQueryContext q, JsonObject a)
    {
        if (q.Model.Get((string?)a["id"] ?? "") is not { } e || Features.P(a["point"]) is not { } p || Features.P(a["normal"]) is not { } n || n.Length < 1e-9) return null;
        var solids = q.Geometry(e.Id)?.Parts.Select(x => x.Solid).ToList() ?? [];
        return solids.Count == 0 ? null : (e, solids, p, n.Normalized());
    }

    static (Entity E, List<Solid> Solids, Vec3 Point, Vec3 Normal) RequirePick(ICommandContext ctx, JsonObject a, string what) =>
        Pick(ctx.Query, a) ?? throw new CommandException($"Pick {what} on an element in 3D.");

    // ------------------------------------------------------------ push/pull

    /// <summary>
    /// A face pushed or pulled by a distance (<c>{id, point, normal, distance}</c>; positive: out, adding): an
    /// extrusion's end changes its depth, its side moves the sketch's edge under it; any other Studio solid takes
    /// it as a step; another element gets a solid on the face (pulled: a solid of its own, in the face's material)
    /// or a cut into it (pushed: a void it is less, as live as any boolean).
    /// </summary>
    static JsonNode? PushPull(ICommandContext ctx, JsonObject a)
    {
        var d = Json.Number(a["distance"]) ?? throw new CommandException("How far: type a distance (negative pushes in).");
        if (Math.Abs(d) < 1e-6) throw new CommandException("Push or pull by more than nothing.");
        var (e, solids, p, n) = RequirePick(ctx, a, "a face");
        var face = BrepPick.Face(solids, p, n) ?? throw new CommandException("There is no flat face there.");
        if (e.Str("operation") is { } op)
        {
            if (op == "prism")
            {
                SetDepth(ctx, e, e.Num("depth") + d);
                return new JsonObject { ["how"] = "depth", ["id"] = e.Id };
            }
            if (op == "extrude" && Parametric(ctx, e, face, d) is { } how) return new JsonObject { ["how"] = how, ["id"] = e.Id };
            var list = Features0(e);
            var on = p - face.Normal * Vec3.Dot(p - face.Outer[0], face.Normal);
            list.Add(new JsonObject { ["op"] = "pushPull", ["point"] = Features.J(on), ["normal"] = Features.J(face.Normal), ["distance"] = d });
            WriteFeatures(ctx, e, list);
            return new JsonObject { ["how"] = "step", ["id"] = e.Id };
        }
        // Another element: a solid on its face, or a cut into it.
        var storey = Spatial.StoreyOf(ctx.Model, e.Id) ?? throw new CommandException($"„{e.Name}” is not an element.");
        var part = ctx.Query.Geometry(e.Id)?.Parts.FirstOrDefault(x => BrepPick.Face([x.Solid], p, n) != null);
        var attrs = new JsonObject
        {
            ["operation"] = "prism", ["depth"] = d, ["normal"] = Features.J(face.Normal), ["host"] = e.Id,
            ["outline"] = new JsonArray(face.Outer.Select(q => (JsonNode)Features.J(q)).ToArray()),
            ["holes"] = new JsonArray(face.Holes.Select(h => (JsonNode)new JsonArray(h.Select(q => (JsonNode)Features.J(q)).ToArray())).ToArray()),
        };
        if (part?.Material is { } mat) attrs["material"] = mat;
        if (d < 0) attrs["void"] = true;
        var name = d > 0 ? $"Pulled from {e.Name}" : $"Pushed into {e.Name}";
        var prism = new Entity(ctx.NewId(), IfcClass.BuildingElementProxy, name, attrs);
        ctx.Add(prism);
        Spatial.Contain(ctx, storey.Id, prism.Id);
        if (d < 0) ctx.AddRelation(new Relation(ctx.NewId(), IfcClass.RelInterferesElements, e.Id, [prism.Id], new JsonObject { ["interferenceType"] = "Subtract", ["name"] = "Lapin push" }));
        return new JsonObject { ["how"] = d > 0 ? "solid" : "cut", ["id"] = prism.Id };
    }

    /// <summary>A pushed or pulled face's distance changed (<c>{id, distance}</c>): pulled out it is a solid of its own, pushed in a cut into its element.</summary>
    static JsonNode? SetPush(ICommandContext ctx, JsonObject a)
    {
        var e = Solid(ctx.Model, (string?)a["id"]);
        if (e.Str("operation") != "prism") throw new CommandException("That is not a pushed or pulled face.");
        var d = Json.Number(a["distance"]) ?? throw new CommandException("Give the distance (negative pushes in).");
        if (Math.Abs(d) < 1e-6) throw new CommandException("Push or pull by more than nothing.");
        SetDepth(ctx, e, d);
        return null;
    }

    static void SetDepth(ICommandContext ctx, Entity e, double d)
    {
        if (Math.Abs(d) < 1e-6) throw new CommandException("That would leave nothing: remove it instead.");
        var attrs = e.CloneAttrs();
        attrs["depth"] = d;
        var host = e.Str("host");
        var cutting = ctx.Model.Relations.Where(x => x.Class == IfcClass.RelInterferesElements && x.Related.Contains(e.Id)).ToList();
        if (d < 0)
        {
            attrs["void"] = true;
            if (cutting.Count == 0 && host != null && ctx.Model.Get(host) != null)
                ctx.AddRelation(new Relation(ctx.NewId(), IfcClass.RelInterferesElements, host, [e.Id], new JsonObject { ["interferenceType"] = "Subtract", ["name"] = "Lapin push" }));
        }
        else
        {
            attrs.Remove("void");
            foreach (var x in cutting.Where(x => (string?)x.Attrs?["name"] == "Lapin push")) ctx.RemoveRelation(x.Id);
        }
        ctx.Replace(e.With(attrs: attrs));
    }

    /// <summary>
    /// A push/pull an extrusion takes into its own numbers: an end face changes its depth (and where it starts),
    /// a side face moves the edge of the sketch's polyline under it (or of a hole's). The steps after it that worked
    /// on that face move with it. Null when the face is neither (a face a step made).
    /// </summary>
    static string? Parametric(ICommandContext ctx, Entity e, BrepFace face, double d)
    {
        if (e.Attrs["upTo"] != null || Placed(e) != null) return null;
        var profile = ctx.Model.Get(e.Str("profile") ?? "");
        var plane = profile == null ? null : PlaneOfSketch(ctx.Model, ctx.Services, profile).Plane;
        if (profile == null || plane == null) return null;
        var n = plane.Normal;
        var depth = e.NumOrNull("depth") ?? 1;
        var start = e.Num("offset") + (Flag(e.Attrs["symmetric"]) ? -depth / 2 : 0);
        var (lo, hi) = (Math.Min(start, start + depth), Math.Max(start, start + depth));
        var along = Vec3.Dot(face.Outer[0] - plane.Origin, n);
        var dn = Vec3.Dot(face.Normal, n);
        if (Math.Abs(dn) > 0.999)
        {
            if (dn > 0 ? Math.Abs(along - hi) > 1e-4 : Math.Abs(along - lo) > 1e-4) return null;
            var (nlo, nhi) = dn > 0 ? (lo, hi + d) : (lo - d, hi);
            if (nhi - nlo < 1e-6) throw new CommandException("That would push the solid's end through its other end.");
            var attrs = e.CloneAttrs();
            attrs["depth"] = Math.Sign(depth) * (nhi - nlo);
            attrs["offset"] = depth > 0 ? nlo : nhi;
            attrs.Remove("symmetric");
            attrs["features"] = MoveOnPlane(e.Attrs["features"] as JsonArray, face, d);
            ctx.Replace(e.With(attrs: attrs));
            return "depth";
        }
        if (Math.Abs(dn) > 1e-3 || Math.Abs(e.Num("taper")) > 1e-9) return null;
        // A side: which polyline's edge lies under it (the outline's, or a hole's).
        var fn2 = new Vec2(Vec3.Dot(face.Normal, plane.Right), Vec3.Dot(face.Normal, plane.Up)).Normalized();
        var pts2 = face.Outer.Select(q => new Vec2(Vec3.Dot(q - plane.Origin, plane.Right), Vec3.Dot(q - plane.Origin, plane.Up))).ToList();
        var holes = Sketches.HoleOutlines(ctx.Model, profile, ProfileIds(ctx.Model)).Where(h => h.Ids.Count == 1).Select(h => h.Ids[0]);
        foreach (var id in holes.Prepend(profile.Id))
        {
            if (ctx.Model.Get(id) is not { } draft || Drafts.ShapeOf(draft) is not Polyline { Closed: true } pl) continue;
            var k = pl.Pts.Count;
            for (var i = 0; i < k; i++)
            {
                var (a2, b2) = (pl.Pts[i], pl.Pts[(i + 1) % k]);
                var dir = b2 - a2;
                if (dir.Length < 1e-9) continue;
                var u = dir * (1 / dir.Length);
                var across = new Vec2(-u.Y, u.X);
                if (Math.Abs(Vec2.Dot(across, fn2)) < 0.999) continue;
                if (pts2.Any(q => Math.Abs(Vec2.Dot(q - a2, across)) > 1e-4)) continue;
                var along2 = pts2.Select(q => Vec2.Dot(q - a2, u)).ToList();
                if (along2.Min() < -1e-4 || along2.Max() > dir.Length + 1e-4) continue;
                Drafts.Require(ctx.Model, draft.Id);
                var moved = MoveEdge(pl.Pts, i, fn2 * d);
                Drafts.Set(ctx, draft, new Polyline(moved, true));
                if (e.Attrs["features"] is JsonArray fs && fs.Count > 0)
                {
                    var attrs = e.CloneAttrs();
                    attrs["features"] = MoveOnPlane(fs, face, d);
                    ctx.Replace(e.With(attrs: attrs));
                }
                return "sketch";
            }
        }
        return null;
    }

    /// <summary>A polygon with edge i moved by <paramref name="by"/>: its two ends slide along their other edges (or move straight, where those run the same way).</summary>
    static List<Vec2> MoveEdge(IReadOnlyList<Vec2> pts, int i, Vec2 by)
    {
        var k = pts.Count;
        var o = pts.ToList();
        var (a, b) = (pts[i] + by, pts[(i + 1) % k] + by);
        Vec2 Meet(Vec2 p, Vec2 q, Vec2 r, Vec2 s, Vec2 fallback)
        {
            var (d1, d2) = (q - p, s - r);
            var den = Vec2.Cross(d1, d2);
            if (Math.Abs(den) < 1e-12) return fallback;
            return p + d1 * (Vec2.Cross(r - p, d2) / den);
        }
        o[i] = Meet(pts[(i - 1 + k) % k], pts[i], a, b, a);
        o[(i + 1) % k] = Meet(pts[(i + 1) % k], pts[(i + 2) % k], a, b, b);
        return o;
    }

    /// <summary>The steps' points lying on a face's plane moved with it, so they find their face or edge again.</summary>
    static JsonArray? MoveOnPlane(JsonArray? features, BrepFace face, double d)
    {
        if (features == null) return null;
        var o = (JsonArray)features.DeepClone();
        foreach (var f in o.OfType<JsonObject>())
        {
            foreach (var key in new[] { "point", "a", "b" })
                if (Features.P(f[key]) is { } q && Math.Abs(Vec3.Dot(q - face.Outer[0], face.Normal)) < 1e-4) f[key] = Features.J(q + face.Normal * d);
            if (f["edges"] is JsonArray edges)
                foreach (var pair in edges.OfType<JsonArray>())
                    for (var j = 0; j < pair.Count; j++)
                        if (Features.P(pair[j]) is { } q && Math.Abs(Vec3.Dot(q - face.Outer[0], face.Normal)) < 1e-4) pair[j] = Features.J(q + face.Normal * d);
        }
        return o;
    }

    // ------------------------------------------------------------ steps

    static List<JsonObject> Features0(Entity e) => (e.Attrs["features"] as JsonArray)?.OfType<JsonObject>().Select(f => (JsonObject)f.DeepClone()).ToList() ?? [];

    static void WriteFeatures(ICommandContext ctx, Entity e, List<JsonObject> list)
    {
        var attrs = e.CloneAttrs();
        if (list.Count == 0) attrs.Remove("features");
        else attrs["features"] = new JsonArray(list.Select(f => (JsonNode)f.DeepClone()).ToArray());
        ctx.Replace(e.With(attrs: attrs));
    }

    static (Entity E, List<JsonObject> List, int Index) Step(IModelView m, JsonObject a)
    {
        var e = Solid(m, (string?)a["id"]);
        var list = Features0(e);
        var i = (int)(Json.Number(a["index"]) ?? -1);
        if (i < 0 || i >= list.Count) throw new CommandException("There is no such step.");
        return (e, list, i);
    }

    /// <summary>
    /// A step added to Studio solids: <c>{id or ids, feature: {op, …}}</c>. A chamfer or fillet takes its edge
    /// picked (<c>point</c>, <c>normal</c>: the edge nearest the pick on that face; <c>face: true</c>: every edge round
    /// that face) or given (<c>a</c>, <c>b</c>); a slice or a mirror its plane picked on a face, a reference plane or
    /// section (<c>viewId</c>), or given (<c>point</c>, <c>normal</c>); a rotation its axis an edge picked, or given.
    /// </summary>
    static JsonNode? AddFeature(ICommandContext ctx, JsonObject a)
    {
        if (a["feature"] is not JsonObject given) throw new CommandException("Which step: {op, …}.");
        var ids = a["ids"] is JsonArray ? Args.Ids(a) : [(string?)a["id"] ?? ""];
        var op = (string?)given["op"] ?? "";
        if (!Features.Ops.Contains(op)) throw new CommandException($"„{op}” is not a step ({string.Join(", ", Features.Ops)}).");
        var added = 0;
        foreach (var id in ids)
        {
            var e = Solid(ctx.Model, id);
            var f = Resolve(ctx, e, (JsonObject)given.DeepClone());
            var list = Features0(e);
            list.Add(f);
            WriteFeatures(ctx, e, list);
            added++;
        }
        return new JsonObject { ["added"] = added };
    }

    /// <summary>A step's picks turned into what it keeps (an edge's ends, a plane), its numbers checked.</summary>
    static JsonObject Resolve(ICommandContext ctx, Entity e, JsonObject f)
    {
        var op = (string?)f["op"];
        List<Solid> Body() => ctx.Query.Geometry(e.Id)?.Parts.Select(x => x.Solid).ToList() ?? [];
        double Positive(string key, string what) => Json.Number(f[key]) is { } v && v > 1e-6 ? v : throw new CommandException($"A {what} needs {key switch { "radius" => "a radius", "thickness" => "a thickness", _ => "a distance" }} more than 0.");
        switch (op)
        {
            case "chamfer" or "fillet":
            {
                Positive(op == "chamfer" ? "distance" : "radius", op);
                if (Features.P(f["a"]) != null && Features.P(f["b"]) != null) break;
                if (Features.P(f["point"]) is not { } p || Features.P(f["normal"]) is not { } n) throw new CommandException("Pick the edge, or the face whose edges all go.");
                var body = Body();
                if (Flag(f["face"]))
                {
                    var face = BrepPick.Face(body, p, n) ?? throw new CommandException("There is no flat face there.");
                    f["edges"] = new JsonArray(face.Segments().Select(s => (JsonNode)new JsonArray(Features.J(s.A), Features.J(s.B))).ToArray());
                }
                else
                {
                    var edge = BrepPick.EdgeAt(body, p, n) ?? throw new CommandException("There is no edge there.");
                    if (edge.Second == null) throw new CommandException("That edge has a face on one side only.");
                    f["a"] = Features.J(edge.A);
                    f["b"] = Features.J(edge.B);
                }
                f.Remove("point"); f.Remove("normal"); f.Remove("face");
                break;
            }
            case "shell":
                Positive("thickness", "shell");
                if ((string?)f["open"] is { } open && open is not ("none" or "start" or "end" or "both")) throw new CommandException("A shell is open at its start, its end, both or none.");
                break;
            case "slice" or "mirror":
            {
                if ((string?)f["viewId"] is { } vid)
                {
                    var view = Views.Find(ctx.Model, vid) ?? throw new CommandException("That view is gone.");
                    var plane = Sketches.PlaneOf(ctx.Model, view, ctx.Services) ?? throw new CommandException("Slice by a reference plane, a section, an elevation or a plan.");
                    f["point"] = Features.J(plane.Origin);
                    f["normal"] = Features.J(plane.Normal);
                    f.Remove("viewId");
                }
                else if ((string?)f["lineId"] is { } lid)
                {
                    // A drafted line and the view it lies on: the plane through the line, square to the view.
                    var line = Drafts.Require(ctx.Model, lid, editable: false);
                    if (Drafts.ShapeOf(line) is not Line l) throw new CommandException("Slice by a line.");
                    var view = Views.Find(ctx.Model, line.Str("viewId") ?? "") ?? throw new CommandException("The line's view is gone.");
                    var plane = Sketches.PlaneOf(ctx.Model, view, ctx.Services, Sketches.MidX(l)) ?? throw new CommandException("The line is drawn on a view without a plane.");
                    var (p0, p1) = (plane.World(l.A), plane.World(l.B));
                    var nrm = Vec3.Cross(p1 - p0, plane.Normal);
                    if (nrm.Length < 1e-9) throw new CommandException("The line has no length.");
                    f["point"] = Features.J(p0);
                    f["normal"] = Features.J(nrm.Normalized());
                    f.Remove("lineId");
                }
                else if (Features.P(f["point"]) is { } p && Features.P(f["normal"]) is { } n && (string?)f["onId"] is { } on && ctx.Model.Get(on) != null)
                {
                    // A face picked on any element: its plane.
                    var body = ctx.Query.Geometry(on)?.Parts.Select(x => x.Solid).ToList() ?? [];
                    var face = BrepPick.Face(body, p, n) ?? throw new CommandException("There is no flat face there.");
                    f["point"] = Features.J(face.Outer[0]);
                    f["normal"] = Features.J(face.Normal);
                    f.Remove("onId");
                }
                if (Features.P(f["point"]) == null || Features.P(f["normal"]) is not { } nn || nn.Length < 1e-9) throw new CommandException("Give the plane: a face picked, a work plane or a section, or a drafted line.");
                if (op == "slice" && (string?)f["keep"] is { } keep && keep is not ("back" or "front")) throw new CommandException("A slice keeps the side behind its plane (back) or in front (front).");
                break;
            }
            case "rotate":
            {
                if (Features.P(f["point"]) is { } p && Features.P(f["normal"]) is { } n)
                {
                    var edge = BrepPick.EdgeAt(Body(), p, n) ?? throw new CommandException("There is no edge there to turn about.");
                    f["centre"] = Features.J(edge.A);
                    f["axis"] = Features.J(edge.Dir);
                    f.Remove("point"); f.Remove("normal");
                }
                if (Json.Number(f["angle"]) is null) throw new CommandException("A rotation needs its angle.");
                break;
            }
            case "array":
            {
                var count = Json.Number(f["count"]) ?? 0;
                if (count < 2 || count > 500) throw new CommandException("An array has 2 to 500 copies.");
                f["count"] = Math.Round(count);
                if ((string?)f["kind"] == "polar")
                {
                    if (Features.P(f["centre"]) == null)
                    {
                        // Round the upright through the middle of its base, unless a centre is given.
                        var b = Body().Select(Measure.Bounds).OfType<(Vec3 Min, Vec3 Max)>().ToList();
                        f["centre"] = Features.J(b.Count == 0 ? Vec3.Zero : new Vec3(b.Min(x => x.Min.X), b.Min(x => x.Min.Y), b.Min(x => x.Min.Z)));
                    }
                    f["angle"] = Json.Number(f["angle"]) ?? 360;
                }
                else if (Features.P(f["step"]) == null) throw new CommandException("A row of copies needs the step between them [dx, dy, dz].");
                break;
            }
            case "move":
                if (Features.P(f["by"]) == null) throw new CommandException("A move needs how far [dx, dy, dz].");
                break;
            case "pushPull":
                if (Features.P(f["point"]) == null || Features.P(f["normal"]) == null || Json.Number(f["distance"]) == null) throw new CommandException("A push/pull needs its face and distance.");
                break;
        }
        return f;
    }

    /// <summary>A step changed: <c>{id, index, set: {…}}</c> (its numbers, its name, off to switch it off).</summary>
    static JsonNode? UpdateFeature(ICommandContext ctx, JsonObject a)
    {
        var (e, list, i) = Step(ctx.Model, a);
        if (a["set"] is not JsonObject set) throw new CommandException("What changes: {set: {…}}.");
        var f = list[i];
        foreach (var (k, v) in set)
        {
            if (k is "op") throw new CommandException("A step keeps its kind: remove it and add another.");
            if (v == null) f.Remove(k); else f[k] = v.DeepClone();
        }
        if ((string?)f["op"] is "chamfer" or "fillet" or "shell" or "array" && !Features.Off(f))
            f = Resolve(ctx, e, f);
        list[i] = f;
        WriteFeatures(ctx, e, list);
        return null;
    }

    // ------------------------------------------------------------ split

    /// <summary>
    /// A solid split in two by a plane (<c>{id, point, normal}</c>, a face picked with <c>onId</c>, a reference plane or
    /// section <c>viewId</c>, a drafted <c>lineId</c>): it keeps what is behind the plane, a second solid what is in
    /// front. Both keep the same sketch (change it and both follow) and every step before.
    /// </summary>
    static JsonNode? Split(ICommandContext ctx, JsonObject a)
    {
        var e = Solid(ctx.Model, (string?)a["id"]);
        var plane = new JsonObject { ["op"] = "slice" };
        foreach (var k in new[] { "point", "normal", "viewId", "lineId", "onId" }) if (a[k] != null) plane[k] = a[k]!.DeepClone();
        var back = Resolve(ctx, e, (JsonObject)plane.DeepClone());
        back["keep"] = "back";
        var front = (JsonObject)back.DeepClone();
        front["keep"] = "front";
        var mine = Features0(e);
        mine.Add(back);
        WriteFeatures(ctx, e, mine);
        var attrs = e.CloneAttrs();
        var theirs = Features0(e);
        theirs.Add(front);
        attrs["features"] = new JsonArray(theirs.Select(f => (JsonNode)f.DeepClone()).ToArray());
        var copy = new Entity(ctx.NewId(), e.Class, $"{e.Name} (2)", attrs, e.PredefinedType);
        ctx.Add(copy);
        if (Spatial.StoreyOf(ctx.Model, e.Id) is { } st) Spatial.Contain(ctx, st.Id, copy.Id);
        if (ctx.Model.RelatingOf(e.Id, IfcClass.RelDefinesByType) is { } type) ctx.Relate(IfcClass.RelDefinesByType, type.Id, copy.Id);
        return new JsonObject { ["ids"] = new JsonArray(e.Id, copy.Id) };
    }

    // ------------------------------------------------------------ elements made into Studio solids

    /// <summary>
    /// Elements made into Studio solids (<c>{ids, keep?}</c>): their bodies as they are built now, triangles kept in
    /// the solid's own coordinates from the middle of its base, so steps and booleans work on them; the element goes
    /// (unless kept), the solid takes its name, storey, material and IFC class.
    /// </summary>
    static JsonNode? FromElement(ICommandContext ctx, JsonObject a)
    {
        var keep = Flag(a["keep"]);
        var made = new JsonArray();
        foreach (var id in Args.Ids(a))
        {
            var e = ctx.Model.Get(id) ?? throw new CommandException($"There is no {id}.");
            if (e.Str("operation") != null && e.Str("operation") != "body") throw new CommandException($"„{e.Name}” is a Studio solid already.");
            var parts = ctx.Query.Geometry(e.Id)?.Parts ?? [];
            if (parts.Count == 0) throw new CommandException($"„{e.Name}” has no body to make a solid of.");
            if (Spatial.StoreyOf(ctx.Model, e.Id) == null) throw new CommandException($"„{e.Name}” is not an element.");
            made.Add((JsonNode)MakeSolid(ctx, e, parts, keep));
        }
        return new JsonObject { ["ids"] = made };
    }

    /// <summary>The attributes a body solid does not take from the element it is made of: its own body, the plan a file drew for it.</summary>
    static readonly string[] NotCarried = [StoredBodies.Key, StoredBodies.BodyOf, StoredBodies.XfKey, "plan2d", "operation", "features", "at", "rotation"];

    /// <summary>
    /// An element made a Studio solid of its body (<paramref name="parts"/>, world coordinates): its triangles kept in the solid's
    /// own frame, at the middle of its foot. Replacing it (<paramref name="keep"/> false), the solid keeps its id, name, class (one
    /// Studio takes, or a proxy exported as it), predefined type, what a file said of it (its attributes, sets, material) and every
    /// relationship it was in: its storey, its group, its type, the assembly it is a part of.
    /// </summary>
    internal static string MakeSolid(IEdit edit, Entity e, IReadOnlyList<GeometryPart> parts, bool keep)
    {
        var all = parts.SelectMany(p => p.Solid.Vertices).ToList();
        var at = new Vec3((all.Min(p => p.X) + all.Max(p => p.X)) / 2, (all.Min(p => p.Y) + all.Max(p => p.Y)) / 2, all.Min(p => p.Z));
        var body = StoredBodies.Write(parts.Select(p => (p.Name, p.Color, (IReadOnlyList<Vec3>)p.Solid.Vertices.Select(v => v - at).ToList(), (IReadOnlyList<(int, int, int)>)Measure.Triangles(p.Solid).ToList())));
        var cls = Classes.Contains(e.Class) ? e.Class : IfcClass.BuildingElementProxy;
        var attrs = keep ? new JsonObject() : e.CloneAttrs();
        foreach (var k in NotCarried) attrs.Remove(k);
        attrs["operation"] = "body"; attrs[BodyKey] = body; attrs["at"] = Features.J(at); attrs["rotation"] = 0;
        var exportAs = e.Str("exportAs") ?? (cls == e.Class ? null : e.Class);
        if (exportAs != null && exportAs != cls) attrs["exportAs"] = exportAs;
        if (attrs["material"] == null && parts.FirstOrDefault(p => p.Material != null)?.Material is { } mat) attrs["material"] = mat;
        var storey = Spatial.StoreyOf(edit.Model, e.Id);
        // What it is related by (its storey, group, type, assembly), to be related by again; what it relates, again with it.
        var by = edit.Model.Relations.Where(x => x.Related.Contains(e.Id) && x.Class != IfcClass.RelInterferesElements).Select(x => (x.Class, x.Relating)).ToList();
        var relating = edit.Model.Relations.Where(x => x.Relating == e.Id && x.Class is not (IfcClass.RelInterferesElements or IfcClass.RelVoidsElement or IfcClass.RelFillsElement)).ToList();
        var id = keep ? edit.NewId() : NewIdFor(edit, e);
        if (!keep) edit.Remove(e.Id);
        edit.Add(new Entity(id, cls, e.Name, attrs, e.PredefinedType));
        if (keep) { if (storey != null) Spatial.Contain(edit, storey.Id, id); return id; }
        foreach (var (c, r) in by) if (edit.Model.Get(r) != null) edit.RelateAll(c, r, [id]);
        foreach (var x in relating) if (x.Related.All(r2 => edit.Model.Get(r2) != null)) edit.AddRelation(x with { Id = edit.NewId(), Relating = id });
        if (storey != null && Spatial.StoreyOf(edit.Model, id) == null) Spatial.Contain(edit, storey.Id, id);
        return id;
    }

    /// <summary>The element's own id for the solid that takes its place (its GlobalId kept), when nothing else needs it.</summary>
    static string NewIdFor(IEdit edit, Entity e) => e.Id;
}
