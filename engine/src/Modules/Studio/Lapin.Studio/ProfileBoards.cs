using System.Text.Json.Nodes;
using Lapin.Drafting.Cad;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Platform.ProfileLibraries;

namespace Lapin.Studio;

/// <summary>
/// The wall profile and the section profile edited on the shared sketch canvas (audit S11 stage 2, docs/plan-sketch-canvas.md §4,
/// items 1 and 2): "edit in a view", as an outline's Edit sketch (<see cref="OutlineSketches"/>) and a part's sketch are.
///
/// <para><b>A wall's profile</b> (decision 372; a curtain wall's boundary, report #127). <c>studio.editWallProfile {id}</c> makes a
/// reference plane standing on the wall's axis, its x along the wall from its start and its y up from its base (so the profile's
/// [u, v] points are the drafts' coordinates as they are, copied, never transformed), marked with the wall (<see cref="WallKey"/>),
/// the wall hidden there; it draws the top as one polyline (the stored profile, else the flat top at the wall's height) and the wall's
/// base and ends as construction lines. Every drafting command, snap, grip, constraint and dimension then works on it.
/// <c>studio.finishWallProfile {viewId}</c> chains what is drawn into one line from the wall's start to its end (arcs laid in chords by
/// the one rule, <see cref="Chords"/>), refuses, saying why and keeping the board open, a shape that is not part of one such line, and
/// writes it through the wall's own command (<c>architecture.setWallProfile</c>, <c>architecture.setCurtainWallProfile</c>) in one undo
/// step; <c>flat: true</c> makes the top flat again. A top finished as it was opened leaves the wall untouched (the same JSON).</para>
///
/// <para><b>A section profile</b> (report #128). <c>studio.editProfile {profile?, rings?, name, material, library}</c> makes a level
/// reference plane of its own, far under the model and seeing nothing of it (a blank sheet in the section's metres, its x and y the
/// section's), marked with what it edits (<see cref="ProfileKey"/>), and draws each ring as a closed polyline with the section's axes
/// as construction lines. <c>studio.finishProfile {viewId, name?, material?, library?}</c> takes every closed shape drawn (closed
/// polylines, rectangles, circles, ellipses, and lines and arcs end to end; curves in chords by the one rule) as a ring, checked and
/// centred as <c>profiles.fromRings</c> does (<see cref="ProjectProfiles.CheckRings"/>, <see cref="ProjectProfiles.FromOutlines"/>), and
/// answers the profile for the libraries; rings finished as they were opened give the profile back as it came (the same JSON).</para>
///
/// <para>Both drop their view at Finish and at Cancel (<c>studio.cancelWallProfile</c>, <c>studio.cancelProfile</c>), one undo step:
/// undo brings the board back with its edits. <c>studio.setBoardShapes {viewId, shapes}</c> replaces what a board's sketch holds
/// (a starter shape, a table's typed points) in one step; <c>studio.profileBoard {viewId}</c> reads what the drafts make now.</para>
/// </summary>
public static class ProfileBoards
{
    /// <summary>The attribute marking a wall profile's board: {id, curtain, length, top (as opened, [[u, v], …])}.</summary>
    public const string WallKey = "wallProfileOf";

    /// <summary>The attribute marking a section profile's board: {profile? (as it came), rings (as opened, metres), name, material, library}.</summary>
    public const string ProfileKey = "profileOf";

    /// <summary>How near two ends are to be one point (m).</summary>
    const double Tol = 1e-5;

    /// <summary>How far a point may stray and still be the one it was opened with (the drafts keep 9 decimals).</summary>
    const double Same = 1e-9;

    const string CurtainClass = "IfcCurtainWall";

    /// <summary>The wall's own top (the architecture module's <c>WallTops.TopProfileKey</c>).</summary>
    const string TopProfileKey = "topProfile";

    static CommandEntry Command(ICommandContext ctx, string name) =>
        (ctx.Services as ModuleCatalog)?.Command(name) ?? throw new CommandException($"This board needs {name}, not in this product.");

    /// <summary>Whether a view is a board of this kind's, and its mark.</summary>
    static (Entity View, JsonObject Of) Board(IModelView m, JsonObject a, string key, string what)
    {
        var view = Views.Find(m, (string?)a["viewId"] ?? "") ?? throw new CommandException("There is no such view.");
        return view.Attrs[key] is JsonObject of ? (view, of) : throw new CommandException($"That view is not {what}.");
    }

    /// <summary>The board's own shapes: its drafts but the construction lines.</summary>
    static List<(Entity Draft, Shape Shape)> Drawn(IModelView m, string viewId) =>
        [.. Drafts.On(m, viewId).Where(d => d.Str("role") is null or "profile" && d.Str("layer") != Drafts.ConstructionLayer)
            .Select(d => (d, Drafts.ShapeOf(d))).Where(x => x.Item2 != null).Select(x => (x.d, x.Item2!))];

    /// <summary>An arc's points by the one rule, its ends included.</summary>
    static List<Vec2> ArcPoints(Arc a)
    {
        var sweep = a.Sweep;
        var n = Chords.Count(a.R, sweep);
        return [.. Enumerable.Range(0, n + 1).Select(k => a.At(a.Start + sweep * k / n))];
    }

    /// <summary>
    /// The shapes as point paths: the closed ones (closed polylines, circles, whole ellipses, and open pieces meeting end to end in a
    /// loop) and the open ones (open pieces chained end to end). Refused, naming it, when more than two ends meet at a point.
    /// </summary>
    public static (List<List<Vec2>> Closed, List<List<Vec2>> Open) Paths(IEnumerable<Shape> shapes)
    {
        var closed = new List<List<Vec2>>();
        var open = new List<List<Vec2>>();
        foreach (var s in shapes)
            switch (s)
            {
                case Line l when (l.B - l.A).Length > Tol: open.Add([l.A, l.B]); break;
                case Polyline { Closed: true } p when p.Pts.Count >= 3: closed.Add([.. p.Pts]); break;
                case Polyline p when p.Pts.Count >= 2: open.Add([.. p.Pts]); break;
                case Circle c: closed.Add(Chords.Ring(c.C, c.R)); break;
                case Arc a: open.Add(ArcPoints(a)); break;
                case Ellipse { Whole: true } e: { var pts = e.Points(); pts.RemoveAt(pts.Count - 1); closed.Add(pts); break; }
                case Ellipse e: open.Add(e.Points()); break;
            }
        bool Near(Vec2 p, Vec2 q) => (p - q).Length <= Tol;
        var ends = open.SelectMany(p => new[] { p[0], p[^1] }).ToList();
        if (ends.Any(p => ends.Count(q => Near(p, q)) > 2)) throw new CommandException("The sketch branches: more than two lines meet at a point. Keep one line through it, trim the rest.");
        var left = new List<List<Vec2>>(open);
        var chains = new List<List<Vec2>>();
        while (left.Count > 0)
        {
            var chain = new List<Vec2>(left[0]);
            left.RemoveAt(0);
            for (var grew = true; grew;)
            {
                grew = false;
                for (var k = 0; k < left.Count; k++)
                {
                    var p = left[k];
                    if (Near(chain[^1], p[0])) chain.AddRange(p.Skip(1));
                    else if (Near(chain[^1], p[^1])) chain.AddRange(Enumerable.Reverse(p).Skip(1));
                    else if (Near(chain[0], p[^1])) chain.InsertRange(0, p.Take(p.Count - 1));
                    else if (Near(chain[0], p[0])) chain.InsertRange(0, Enumerable.Reverse(p).Take(p.Count - 1));
                    else continue;
                    left.RemoveAt(k);
                    grew = true;
                    break;
                }
            }
            if (chain.Count >= 4 && Near(chain[0], chain[^1])) { chain.RemoveAt(chain.Count - 1); closed.Add(chain); }
            else chains.Add(chain);
        }
        return (closed, chains);
    }

    static JsonArray Pts(IEnumerable<Vec2> pts) => new([.. pts.Select(p => (JsonNode)new JsonArray(p.X, p.Y))]);

    static List<Vec2> ReadPts(JsonNode? n) => (n as JsonArray ?? []).OfType<JsonArray>()
        .Select(p => new Vec2(Json.Number(p.Count > 0 ? p[0] : null) ?? 0, Json.Number(p.Count > 1 ? p[1] : null) ?? 0)).ToList();

    static bool SamePts(IReadOnlyList<Vec2> a, IReadOnlyList<Vec2> b) => a.Count == b.Count && a.Zip(b).All(x => (x.First - x.Second).Length <= Same);

    // ─── A wall's profile ──────────────────────────────────────────────────────────────────

    /// <summary>The top a wall board's drafts make, from the wall's start to its end, or why they make none.</summary>
    public static List<Vec2> WallTop(IReadOnlyList<Shape> shapes, double length)
    {
        foreach (var s in shapes)
            if (s is Circle or Ellipse { Whole: true } or Polyline { Closed: true })
                throw new CommandException("The top is one line from the wall's start to its end: a closed shape is not part of it. Erase it, or draw it open.");
        var (closed, open) = Paths(shapes);
        if (closed.Count > 0) throw new CommandException("The top is one line from the wall's start to its end: it closes on itself here. Leave its ends open.");
        if (open.Count == 0) throw new CommandException("Draw the wall's top: one line from its start to its end.");
        if (open.Count > 1) throw new CommandException($"The top is in {open.Count} pieces: join them end to end (Trim, Extend, Fillet).");
        var pts = open[0];
        if (pts[0].X > pts[^1].X) pts.Reverse();
        var o = new List<Vec2> { pts[0] };
        foreach (var p in pts.Skip(1))
        {
            var q = o[^1];
            if ((p - q).Length <= Same) continue;
            // A step straight up or down: the same place along the wall.
            var u = Math.Abs(p.X - q.X) < 1e-6 ? q.X : p.X;
            if (u < q.X) throw new CommandException("The top turns back along the wall: it runs from the start to the end, a step straight up or down.");
            o.Add(new Vec2(u, p.Y));
        }
        if (o.Count < 2) throw new CommandException("Draw the wall's top: one line from its start to its end.");
        if (o[0].X < -0.001 || o[^1].X > length + 0.001)
            throw new CommandException($"The top runs past the wall's {(o[0].X < -0.001 ? "start" : "end")}: the wall is {Math.Round(length * 1000)} mm long.");
        if (o.Any(p => p.Y < 0.05 - 1e-9)) throw new CommandException("The top stays 50 mm over the wall's base at least.");
        return [.. o.Select(p => new Vec2(Math.Clamp(p.X, 0, length), p.Y))];
    }

    /// <summary>The wall a board stands for: a straight wall or curtain wall, its length, base and the top it has now.</summary>
    static (Entity Wall, bool Curtain, PathCurve Path, List<Vec2> Top) WallOf(ICommandContext ctx, string? id)
    {
        var e = ctx.Model.Get(id ?? "") ?? throw new CommandException("The wall is not in the project.");
        var curtain = e.Class == CurtainClass;
        if (e.Class != IfcClass.Wall && !curtain) throw new CommandException("Only a wall or a curtain wall has its profile drawn.");
        var path = PathCurve.Of(e);
        if (path.IsArc) throw new CommandException(curtain ? "A curved curtain wall's boundary is drawn in its dialog: the board stands on a straight wall." : "A curved wall keeps a flat top: its profile is for straight walls.");
        if (path.Chord < 1e-6) throw new CommandException("The wall has no length.");
        var top = ReadPts(e.Attrs[TopProfileKey]);
        if (top.Count < 2)
        {
            double h;
            if (curtain) h = e.Num("height", 3);
            else
            {
                var wt = (ctx.Services as ModuleCatalog)?.Query("architecture.wallTop")?.Invoke(ctx.Query, new JsonObject { ["id"] = e.Id }) as JsonObject;
                h = Json.Number(wt?["height"]) ?? e.Num("height", 3);
            }
            top = [new Vec2(0, h), new Vec2(path.Chord, h)];
        }
        return (e, curtain, path, top);
    }

    /// <summary>{id} → {viewId, ids}: the wall's profile on a board of its own (the same one when it is open).</summary>
    public static JsonNode? EditWallProfile(ICommandContext ctx, JsonObject a)
    {
        var (wall, curtain, path, top) = WallOf(ctx, (string?)a["id"]);
        if (ctx.Model.OfClass(Views.Class).FirstOrDefault(v => (string?)v.Attrs[WallKey]?["id"] == wall.Id) is { } open)
            return new JsonObject { ["viewId"] = open.Id, ["ids"] = new JsonArray([.. Drafts.On(ctx.Model, open.Id).Select(d => (JsonNode)d.Id)]) };
        var length = path.Chord;
        var dir = (path.End - path.Start).Normalized();
        var along = new Vec3(dir.X, dir.Y, 0);
        var origin = new Vec3(path.Start.X, path.Start.Y, Spatial.Base(ctx.Model, wall));
        // Facing along × up, so the plane's x runs from the wall's start and its y up from the base: seen from start to end.
        var attrs = new JsonObject { ["kind"] = RefPlanes.Kind, ["scale"] = 50 };
        foreach (var (k, v) in RefPlanes.Attrs(origin, Vec3.Cross(along, new Vec3(0, 0, 1)), along)) attrs[k] = v?.DeepClone();
        attrs[WallKey] = new JsonObject { ["id"] = wall.Id, ["curtain"] = curtain, ["length"] = length, ["top"] = Pts(top) };
        // The wall is not drawn on its profile's board: the top drawn is what it will be.
        attrs[ViewHiding.Key] = new JsonArray(wall.Id);
        var view = new Entity(ctx.NewId(), Views.Class, $"{(curtain ? "Boundary" : "Profile")}: {wall.Name}", attrs);
        ctx.Add(view);
        var ids = new List<string> { Drafts.Add(ctx, view.Id, "0", new Polyline(top, false)).Id };
        // The wall's base and ends, to snap to: construction lines (shown, never printed, left out at Finish).
        var high = Math.Max(top.Max(p => p.Y), 0.5) + 0.5;
        foreach (var guide in new Shape[] { new Line(new Vec2(0, 0), new Vec2(length, 0)), new Line(new Vec2(0, 0), new Vec2(0, high)), new Line(new Vec2(length, 0), new Vec2(length, high)) })
            ids.Add(Drafts.Add(ctx, view.Id, "0", guide, "draft").Id);
        return new JsonObject { ["viewId"] = view.Id, ["ids"] = new JsonArray([.. ids.Select(x => (JsonNode)x)]) };
    }

    /// <summary>{viewId, flat?} → {id}: the top drawn written to the wall, one undo step; refused, the board kept, when it is not one.</summary>
    public static JsonNode? FinishWallProfile(ICommandContext ctx, JsonObject a)
    {
        var (view, of) = Board(ctx.Model, a, WallKey, "a wall profile's board");
        var id = (string?)of["id"];
        var (wall, curtain, path, _) = WallOf(ctx, id);
        var command = curtain ? "architecture.setCurtainWallProfile" : "architecture.setWallProfile";
        if (a["flat"] is JsonValue fv && fv.TryGetValue<bool>(out var flat) && flat)
            Command(ctx, command).Handler(ctx, new JsonObject { ["id"] = wall.Id, ["ids"] = new JsonArray(wall.Id), ["top"] = null });
        else
        {
            var top = WallTop([.. Drawn(ctx.Model, view.Id).Select(x => x.Shape)], path.Chord);
            // Finished as it was opened: the wall untouched.
            if (!SamePts(top, ReadPts(of["top"])))
                Command(ctx, command).Handler(ctx, new JsonObject { ["id"] = wall.Id, ["ids"] = new JsonArray(wall.Id), ["top"] = Pts(top) });
        }
        Drop(ctx, view);
        return new JsonObject { ["id"] = wall.Id };
    }

    // ─── A section profile ──────────────────────────────────────────────────────────────────

    /// <summary>The rings a profile board's drafts make (metres), or why they make none.</summary>
    public static List<List<Vec2>> Rings(IReadOnlyList<Shape> shapes)
    {
        var (closed, open) = Paths(shapes);
        if (open.Count > 0) throw new CommandException($"The section is not closed: {open.Count} open line{(open.Count == 1 ? "" : "s")}. Join each outline end to end (Trim, Extend, Fillet).");
        if (closed.Count == 0) throw new CommandException("Draw the section's outline: a closed shape, and its chambers inside it.");
        ProjectProfiles.CheckRings(closed);
        return closed;
    }

    /// <summary>{profile?, rings? (metres; else the profile's own), name, material, library} → {viewId, ids}: a section on a board of its own.</summary>
    public static JsonNode? EditProfile(ICommandContext ctx, JsonObject a)
    {
        var profile = a["profile"] as JsonObject;
        var rings = (a["rings"] as JsonArray)?.Select(ReadPts).Where(r => r.Count >= 3).ToList();
        if (rings is not { Count: > 0 } && profile != null && SectionProfile.From(profile) is { } p)
            rings = [.. p.Outline().Select(r => r.ToList())];
        rings ??= [];
        var name = (string?)a["name"] ?? (string?)profile?["name"] ?? "Drawn profile";
        // A level plane far under the model, seeing a millimetre: a blank sheet in the section's metres.
        var attrs = new JsonObject { ["kind"] = RefPlanes.Kind, ["scale"] = 1, ["depth"] = 0.001 };
        foreach (var (k, v) in RefPlanes.Attrs(new Vec3(0, 0, -1000), new Vec3(0, 0, 1), new Vec3(1, 0, 0))) attrs[k] = v?.DeepClone();
        var mark = new JsonObject
        {
            ["rings"] = new JsonArray([.. rings.Select(r => (JsonNode)Pts(r))]), ["name"] = name,
            ["material"] = (string?)a["material"] ?? (string?)profile?["material"] ?? "aluminium", ["library"] = (string?)a["library"] ?? "My profiles",
        };
        if (profile != null) mark["profile"] = profile.DeepClone();
        attrs[ProfileKey] = mark;
        var view = new Entity(ctx.NewId(), Views.Class, $"Profile: {name}", attrs);
        ctx.Add(view);
        var ids = rings.Select(r => Drafts.Add(ctx, view.Id, "0", new Polyline(r, true)).Id).ToList();
        // The section's axes through its origin, to snap to: construction lines.
        var reach = rings.SelectMany(r => r).Select(q => Math.Max(Math.Abs(q.X), Math.Abs(q.Y))).DefaultIfEmpty(0.05).Max() * 1.2;
        foreach (var guide in new Shape[] { new Line(new Vec2(-reach, 0), new Vec2(reach, 0)), new Line(new Vec2(0, -reach), new Vec2(0, reach)) })
            ids.Add(Drafts.Add(ctx, view.Id, "0", guide, "draft").Id);
        return new JsonObject { ["viewId"] = view.Id, ["ids"] = new JsonArray([.. ids.Select(x => (JsonNode)x)]) };
    }

    /// <summary>{viewId, name?, material?, library?} → {profile, library, changed}: the section drawn as a profile; the board goes, one undo step.</summary>
    public static JsonNode? FinishProfile(ICommandContext ctx, JsonObject a)
    {
        var (view, of) = Board(ctx.Model, a, ProfileKey, "a profile's board");
        var rings = Rings([.. Drawn(ctx.Model, view.Id).Select(x => x.Shape)]);
        var name = ((string?)a["name"] ?? (string?)of["name"])?.Trim() is { Length: > 0 } n ? n : "Drawn profile";
        var material = (string?)a["material"] ?? (string?)of["material"] ?? "aluminium";
        var library = ((string?)a["library"] ?? (string?)of["library"])?.Trim() is { Length: > 0 } l ? l : "My profiles";
        var opened = (of["rings"] as JsonArray ?? []).Select(ReadPts).ToList();
        // Its rings as they were opened (each one drawn, whichever order): the profile as it came, renamed or its material changed if asked.
        var unchanged = of["profile"] is JsonObject && opened.Count == rings.Count && opened.All(o => rings.Any(r => SamePts(o, r)));
        JsonObject profile;
        if (unchanged)
        {
            profile = (JsonObject)of["profile"]!.DeepClone();
            if ((string?)profile["name"] != name) profile["name"] = name;
            if ((string?)profile["material"] != material) profile["material"] = material;
        }
        else profile = ProjectProfiles.WithSummary(ProjectProfiles.FromOutlines(rings, name, material, "drawn"));
        Drop(ctx, view);
        return new JsonObject { ["profile"] = profile, ["library"] = library, ["changed"] = !unchanged };
    }

    // ─── Both ───────────────────────────────────────────────────────────────────────────────

    static void Drop(ICommandContext ctx, Entity view)
    {
        foreach (var d in Drafts.On(ctx.Model, view.Id).ToList()) ctx.Remove(d.Id);
        if (SketchConstraints.CarrierOf(ctx.Model, view.Id) is { } carrier) ctx.Remove(carrier.Id);
        ctx.Remove(view.Id);
    }

    static Entity AnyBoard(IModelView m, JsonObject a)
    {
        var view = Views.Find(m, (string?)a["viewId"] ?? "") ?? throw new CommandException("There is no such view.");
        return view.Attrs[WallKey] is JsonObject || view.Attrs[ProfileKey] is JsonObject ? view : throw new CommandException("That view is not a profile's board.");
    }

    public static void Register(IModuleRegistry r)
    {
        r.Command("editWallProfile", "Edit wall profile", EditWallProfile);
        r.Command("finishWallProfile", "Finish wall profile", FinishWallProfile);
        r.Command("cancelWallProfile", "Cancel wall profile", (ctx, a) => { Drop(ctx, Board(ctx.Model, a, WallKey, "a wall profile's board").View); return null; });
        r.Command("editProfile", "Edit profile", EditProfile);
        r.Command("finishProfile", "Finish profile", FinishProfile);
        r.Command("cancelProfile", "Cancel profile", (ctx, a) => { Drop(ctx, Board(ctx.Model, a, ProfileKey, "a profile's board").View); return null; });

        // {viewId, shapes: [shape, …]} → {ids}: what a profile's board holds replaced (its construction lines kept), one undo step.
        r.Command("setBoardShapes", "Replace sketch", (ctx, a) =>
        {
            var view = AnyBoard(ctx.Model, a);
            var shapes = (a["shapes"] as JsonArray ?? []).Select(s => Shape.From(s) ?? throw new CommandException("A shape is a line, a polyline, a circle, an arc or an ellipse.")).ToList();
            foreach (var (d, _) in Drawn(ctx.Model, view.Id)) ctx.Remove(d.Id);
            // Its constraints held the shapes taken away.
            if (SketchConstraints.CarrierOf(ctx.Model, view.Id) is { } carrier) ctx.Remove(carrier.Id);
            return new JsonObject { ["ids"] = new JsonArray([.. shapes.Select(s => (JsonNode)Drafts.Add(ctx, view.Id, "0", s).Id)]) };
        });

        // {viewId} → what a board's drafts make now: a wall's {kind: "wall", length, top?, problem?}; a profile's {kind: "profile", rings?, width, depth, area, problem?} (metres).
        r.Query("profileBoard", (q, a) =>
        {
            var view = AnyBoard(q.Model, a);
            var shapes = Drawn(q.Model, view.Id).Select(x => x.Shape).ToList();
            if (view.Attrs[WallKey] is JsonObject w)
            {
                var length = Json.Number(w["length"]) ?? 0;
                var o = new JsonObject { ["kind"] = "wall", ["length"] = length, ["id"] = (string?)w["id"], ["curtain"] = (bool?)w["curtain"] ?? false };
                try { o["top"] = Pts(WallTop(shapes, length)); } catch (CommandException e) { o["problem"] = e.Message; }
                return o;
            }
            var p = new JsonObject { ["kind"] = "profile" };
            try
            {
                // The biggest first: the outside, then its chambers.
                var rings = Rings(shapes).OrderByDescending(x => Math.Abs(Polygon2.SignedArea(x))).ToList();
                p["rings"] = new JsonArray([.. rings.Select(r => (JsonNode)Pts(r))]);
                var all = rings.SelectMany(x => x).ToList();
                p["width"] = all.Max(x => x.X) - all.Min(x => x.X);
                p["depth"] = all.Max(x => x.Y) - all.Min(x => x.Y);
                // The biggest the outside, those inside an outside an odd number of times its chambers (as the profile maker reads them).
                var sorted = rings.OrderByDescending(x => Math.Abs(Polygon2.SignedArea(x))).ToList();
                p["area"] = sorted.Select((x, i) => Math.Abs(Polygon2.SignedArea(x)) * (sorted.Take(i).Count(o => Polygon2.Contains(o, x[0])) % 2 == 1 ? -1 : 1)).Sum();
            }
            catch (CommandException e) { p["problem"] = e.Message; }
            return p;
        });
    }
}
