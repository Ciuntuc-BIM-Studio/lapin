using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Drafting;

/// <summary>Drafting: grid axes, dimensions (strings of points, one object each) that follow what they measure, texts.</summary>
public sealed class DraftingModule : ILapinModule, IViewAnnotations
{
    public string Id => "drafting";
    public string Name => "Drafting";

    public void Register(IModuleRegistry r)
    {
        r.Class(new(IfcClass.Grid, "Grid", [], "#555555", Kind: "annotation"));
        r.Class(new(IfcClass.Annotation, "Annotation",
            [new("text", "Text", "text"), new("textStyle", "Text style", "text"), new("dimStyle", "Dimension style", "text"), new("angle", "Angle", "angle"),
             new("format", "Slope written as", "choice", Choices: DimensionKinds.SlopeFormats), new("z", "Level written", "length"), new("textHeight", "Text height", "number", Min: 0.5, Max: 50)], "#555555", Kind: "annotation"));
        // A draft with heights (a DXF's 3D lines) shows in 3D as lines; the rest have no geometry.
        r.Geometry(IfcClass.Annotation, Cad.Drafts.Geometry3d);
        r.Service<IViewAnnotations>(this);
        r.Service<ISectionPaths>(new ShapeSectionPaths());
        r.Service<IArrayPaths>(new ShapeArrayPaths());
        Cad.DraftCommands.Register(r);
        Cad.DxfImport.Register(r);
        Cad.Underlays3d.Register(r);
        // Pictures and PDFs to trace over (decision 371).
        Cad.ViewImages.Register(r);
        // Symbols placed on views and sheets, and drafting saved as a symbol (docs/plan-user-extensibility.md §3).
        Cad.SymbolInstances.Register(r);
        r.Service<IElementPlacement>(new Cad.AnnotationPlacement());
        r.Service<IDeletion>(new AxisDeletion());
        r.Service<IPartPlacement>(new AxisPlacement());
        r.Service<IProjectUpgrade>(new DimensionUpgrade());
        // Tags follow their elements (report #93).
        r.Service<ITransformFollowers>(new TagFollowers());
        r.Service<IProjectUpgrade>(new TagHoldUpgrade());
        r.Service<IProjectUpgrade>(new TagLeaderUpgrade());
        Tags.Register(r);

        // ------------------------------------------------------------ grids
        r.Command("addAxis", "Grid axis", (ctx, a) =>
        {
            var (p, q) = (new Vec2(Json.Number(a["x1"]) ?? 0, Json.Number(a["y1"]) ?? 0), new Vec2(Json.Number(a["x2"]) ?? 0, Json.Number(a["y2"]) ?? 0));
            if ((q - p).Length < 1e-3) throw new CommandException("An axis needs two different points.");
            var grid = Grids.Ensure(ctx);
            var axes = Grids.Axes(grid);
            var book = DraftingResources.Book(ctx.Model);
            var tag = ((string?)a["tag"])?.Trim() is { Length: > 0 } t ? t : Grids.NextTag(axes, (q - p).Normalized(), Standards.AxisOf(book, book.DefaultAxisStyle).FirstLabel);
            if (axes.Any(x => x.Tag == tag)) throw new CommandException($"The grid has an axis {tag} already.");
            axes.Add(new Axis(tag, p, q));
            Write(ctx, grid, axes);
            return new JsonObject { ["gridId"] = grid.Id, ["tag"] = tag };
        });

        r.Command("updateAxis", "Change grid axis", (ctx, a) =>
        {
            var grid = Spatial.Require(ctx.Model, (string?)a["gridId"], IfcClass.Grid);
            var axes = Grids.Axes(grid);
            var i = axes.FindIndex(x => x.Tag == (string?)a["tag"]);
            if (i < 0) throw new CommandException("The grid has no such axis.");
            var x = axes[i];
            var tag = ((string?)a["newTag"])?.Trim() is { Length: > 0 } nt ? nt : x.Tag;
            if (tag != x.Tag && axes.Any(y => y.Tag == tag)) throw new CommandException($"The grid has an axis {tag} already.");
            var ends = a.ContainsKey("ends") ? (string?)a["ends"] is "both" or "start" or "end" or "none" ? (string?)a["ends"] : null : x.Ends;
            var (p, q) = (new Vec2(Json.Number(a["x1"]) ?? x.A.X, Json.Number(a["y1"]) ?? x.A.Y), new Vec2(Json.Number(a["x2"]) ?? x.B.X, Json.Number(a["y2"]) ?? x.B.Y));
            if ((q - p).Length < 1e-3) throw new CommandException("An axis needs two different points.");
            axes[i] = new Axis(tag, p, q, ends);
            Write(ctx, grid, axes);
            return null;
        });

        // A whole grid at once (report #68, Annotate → Grid…): axes square to x at the origin and after each spacing (labelled
        // from firstX, numbers by default), axes square to y likewise (from firstY, letters), each running past the outer ones by
        // `extend`; the grid turned by `angle` (degrees) about the origin. With `replace`, the grid's axes so far go.
        r.Command("makeGrid", "Grid", (ctx, a) =>
        {
            static List<double> Spacings(JsonNode? n) => n is JsonArray arr ? arr.Select(v => Json.Number(v) ?? double.NaN).ToList() : [];
            var (xs, ys) = (Spacings(a["x"]), Spacings(a["y"]));
            if (xs.Any(v => !(v > 1e-3)) || ys.Any(v => !(v > 1e-3))) throw new CommandException("A spacing is a length above zero.");
            if (xs.Count + ys.Count > 400) throw new CommandException("At most 400 spacings.");
            if (xs.Count + ys.Count == 0) throw new CommandException("Give the spacings across, along or both.");
            var origin = a["origin"] is JsonArray ? P(a["origin"]) : default;
            var extend = Json.Number(a["extend"]) ?? 1.5;
            if (!(extend >= 0)) throw new CommandException("The axes run past the grid by a length of zero or more.");
            var turn = (Json.Number(a["angle"]) ?? 0) * Math.PI / 180;
            var (u, v) = (new Vec2(Math.Cos(turn), Math.Sin(turn)), new Vec2(-Math.Sin(turn), Math.Cos(turn)));
            var grid = Grids.Ensure(ctx);
            var axes = a["replace"] is JsonValue rv && rv.TryGetValue<bool>(out var replace) && replace ? [] : Grids.Axes(grid);
            var (width, depth) = (xs.Sum(), ys.Sum());
            if (Math.Min(xs.Count > 0 ? depth : double.MaxValue, ys.Count > 0 ? width : double.MaxValue) + 2 * extend < 1e-3) throw new CommandException("The axes need a length: give spacings both ways, or let them run past the grid.");
            Vec2 W(double s, double t) => origin + u * s + v * t;
            string Label(string? asked, string fallback) => asked?.Trim() is { Length: > 0 } t ? t : fallback;
            var made = new JsonArray();
            void Line(string tag, Vec2 p, Vec2 q)
            {
                if (axes.Any(x => x.Tag == tag)) throw new CommandException($"The grid has an axis {tag} already: start the labels after it, or replace the grid.");
                axes.Add(new Axis(tag, p, q));
                made.Add((JsonNode)tag);
            }
            static IEnumerable<double> Stations(List<double> spacings) { double at = 0; yield return 0; foreach (var d in spacings) yield return at += d; }
            var tag = Label((string?)a["firstX"], "1");
            foreach (var s in Stations(xs)) { Line(tag, W(s, -extend), W(s, depth + extend)); tag = Grids.Next(tag); }
            tag = Label((string?)a["firstY"], "A");
            foreach (var t in Stations(ys)) { Line(tag, W(-extend, t), W(width + extend, t)); tag = Grids.Next(tag); }
            Write(ctx, grid, axes);
            return new JsonObject { ["gridId"] = grid.Id, ["tags"] = made };
        });

        r.Command("alignAxes", "Line up grid axes", (ctx, a) =>
        {
            var grid = Spatial.Require(ctx.Model, (string?)a["gridId"], IfcClass.Grid);
            var only = a["tags"] is JsonArray tags ? tags.Select(t => (string?)t).OfType<string>().ToHashSet() : null;
            var axes = Grids.Align(Grids.Axes(grid), only, out var n);
            if (n > 0) Write(ctx, grid, axes);
            return new JsonObject { ["aligned"] = n };
        });

        r.Command("removeAxis", "Remove grid axis", (ctx, a) =>
        {
            var grid = Spatial.Require(ctx.Model, (string?)a["gridId"], IfcClass.Grid);
            var axes = Grids.Axes(grid);
            if (axes.RemoveAll(x => x.Tag == (string?)a["tag"]) == 0) throw new CommandException("The grid has no such axis.");
            if (axes.Count == 0) ctx.Remove(grid.Id); else Write(ctx, grid, axes);
            return null;
        });

        // ------------------------------------------------------------ dimensions and texts
        // A linear or aligned dimension is a string of points (two or more), one object: made at once (pts, or a and b),
        // or one point after another (after: the points go on that string's end); its points moved, inserted and taken out like a polyline's.
        r.Command("addDimension", "Dimension", (ctx, a) =>
        {
            var viewId = (string?)a["viewId"] ?? throw new CommandException("A dimension is drawn on a view.");
            var view = Views.Find(ctx.Model, viewId) ?? throw new CommandException("There is no such view.");
            // An angle, a radius, a diameter, an arc length, a slope, a level, an ordinate.
            if (DimensionKinds.Attrs(ctx, viewId, a) is { } kindAttrs)
            {
                var names = new Dictionary<string, string> { ["angular"] = "Angle", ["radial"] = "Radius", ["diameter"] = "Diameter", ["arc"] = "Arc length", ["slope"] = "Slope", ["level"] = "Level", ["ordinate"] = "Ordinate", ["coordinate"] = "Spot coordinate" };
                var made = new Entity(ctx.NewId(), IfcClass.Annotation, names[(string)kindAttrs["dimKind"]!], kindAttrs, "DIMENSION");
                ctx.Add(made);
                return new JsonObject { ["id"] = made.Id };
            }
            var pts = a["pts"] is JsonArray pa ? pa.Select(P).ToList() : [P(a["a"]), P(a["b"])];
            // In a string: the points go on the end of the dimension named (its first one, the string's last, is not doubled).
            if ((string?)a["after"] is { } after && ctx.Model.Get(after) is { } prev && Dimensions.IsLinear(prev))
            {
                var added = Extend(ctx, prev, pts);
                return new JsonObject { ["id"] = prev.Id, ["associated"] = added };
            }
            pts = Distinct(pts);
            if (pts.Count < 2) throw new CommandException("A dimension needs two different points.");
            var attrs = new JsonObject { ["viewId"] = viewId, ["at"] = J(P(a["at"])) };
            if ((string?)a["dimStyle"] is { } ds) attrs["dimStyle"] = ds;
            // Linear: measured along a direction (horizontal, vertical…) rather than end to end.
            if (a["dir"] is JsonArray) { var d = P(a["dir"]); if (d.Length > 1e-9) attrs["dir"] = J(d.Normalized()); }
            // Each point on an edge the view cuts, a grid axis or a wall's centre line follows it.
            var hold = Associator(ctx, view);
            var stations = pts.Select(p => (p, hold(p))).ToList();
            Dimensions.Write(attrs, stations);
            var dim = new Entity(ctx.NewId(), IfcClass.Annotation, "Dimension", attrs, "DIMENSION");
            ctx.Add(dim);
            return new JsonObject { ["id"] = dim.Id, ["associated"] = new JsonArray(stations.Select(s => (JsonNode)(s.Item2 != null)).ToArray()) };
        });

        // Rooms' interior dimensions (decision 249): {viewId (a plan), ids? (rooms; none: every room of its storey), offset? (m)}.
        r.Command("dimensionRooms", "Dimension rooms", (ctx, a) =>
        {
            var view = Views.Find(ctx.Model, (string?)a["viewId"] ?? "") is { } v && v.Str("kind") is "plan" or null ? v
                : throw new CommandException("Rooms are dimensioned on a plan of their storey.");
            var ids = a["ids"] is JsonArray ia && ia.Count > 0 ? ia.Select(x => (string?)x).OfType<string>().ToList() : null;
            var offset = Json.Number(a["offset"]) is { } o ? (o > 0 ? o : throw new CommandException("The strings lie inside the room: the offset must be positive.")) : (double?)null;
            // A site plan sees the whole model from above and cuts nothing: its rooms are dimensioned on their storeys'
            // floor plans instead (a tester's report, decision 271), and the strings it was given before go.
            var targets = new List<(Entity View, IReadOnlyCollection<string>? Ids)>();
            if (SitePlans.Of(view) != null)
            {
                var m = ctx.Model;
                var chosen = ids ?? m.OfClass(IfcClass.Space).Where(s => Spatial.StoreyOf(m, s.Id)?.Id == view.Str("storeyId")).Select(s => s.Id).ToList();
                foreach (var old in m.OfClass(IfcClass.Annotation).Where(x => x.PredefinedType == "DIMENSION" && x.Str("viewId") == view.Id && x.Str(RoomDimensions.RoomKey) is { } rid && chosen.Contains(rid)).ToList())
                    ctx.Remove(old.Id);
                foreach (var g in chosen.GroupBy(id => Spatial.StoreyOf(m, id)?.Id))
                    if (g.Key != null) targets.Add((FloorPlan(m, g.Key), g.ToList()));
            }
            else targets.Add((view, ids));
            var (rooms, strings) = (0, 0);
            foreach (var (target, only) in targets)
            {
                var (drawing, spec, _) = Views.Generate(ctx.Query, target);
                var tolerance = Tolerance(target);
                Func<Vec2, Vec2, DimEndRef?> hold = drawing == null || spec == null ? (_, _) => null : (p, u) => Dimensions.Associate(ctx.Model, spec.Kind, drawing, p, tolerance, u);
                var (rc, sc) = RoomDimensions.Add(ctx, target, only, offset, hold, drawing);
                (rooms, strings) = (rooms + rc, strings + sc);
            }
            var result = new JsonObject { ["rooms"] = rooms, ["strings"] = strings };
            if (targets.Count > 0 && targets[0].View != view) result["views"] = new JsonArray(targets.Select(t => t.View.Name).Distinct().Select(n => (JsonNode)JsonValue.Create(n)!).ToArray());
            return result;
        });

        // Annotations moved or copied to another plan (decision 274): {ids, viewId, copy?}. Plans share their coordinates, so
        // each keeps where it is; a dimension's points hold on what the new plan cuts there (free where it cuts nothing).
        r.Command("toView", "Annotations to view", (ctx, a) =>
        {
            var m = ctx.Model;
            var target = Views.Find(m, (string?)a["viewId"] ?? "") is { } tv && IsPlan(tv) ? tv : throw new CommandException("Annotations go from one plan to another: choose a plan.");
            var copy = a["copy"] is JsonValue cv && cv.TryGetValue<bool>(out var c) && c;
            var items = Args.Ids(a).Select(id => m.Get(id)).OfType<Entity>().Where(e => e.Class == IfcClass.Annotation && e.Str("viewId") != null).ToList();
            if (items.Count == 0) throw new CommandException("Select dimensions, texts or drafted lines on a plan.");
            var drawings = new Dictionary<string, ViewDrawing?>();
            ViewDrawing? DrawingOf(Entity view) =>
                drawings.TryGetValue(view.Id, out var d) ? d : drawings[view.Id] = Views.Generate(ctx.Query, view).Drawing;
            var (targetDrawing, tolerance) = (DrawingOf(target), Tolerance(target));
            var ids = new JsonArray();
            foreach (var e in items)
            {
                var source = Views.Find(m, e.Str("viewId")!) ?? throw new CommandException($"„{e.Name}” has lost its view.");
                if (!IsPlan(source)) throw new CommandException("Only a plan's annotations go to another plan: sections and elevations have coordinates of their own.");
                if (source.Id == target.Id && !copy) { ids.Add((JsonNode)e.Id); continue; }
                var attrs = e.CloneAttrs();
                attrs["viewId"] = target.Id;
                if (Dimensions.IsLinear(e) && DrawingOf(source) is { } sd)
                {
                    // Each point as drawn now, but one held on a wall's centre line where it was put: on a site plan, which cuts
                    // nothing, a click on a wall's face holds on its axis, and the face is what the floor plan should hold on.
                    var (now, _) = Dimensions.Points(m, "plan", e, sd);
                    var pts = Dimensions.Stations(e).Select((s, i) => s.Ref is { Kind: "axis" } ? s.P : now[i]).ToList();
                    var u = pts.Count > 1 && (pts[^1] - pts[0]).Length > 1e-9 ? (pts[^1] - pts[0]).Normalized() : (Vec2?)null;
                    Dimensions.Write(attrs, pts.Select(p => (p, targetDrawing == null ? null : Dimensions.Associate(m, "plan", targetDrawing, p, tolerance, u))).ToList());
                }
                if (copy)
                {
                    var made = new Entity(ctx.NewId(), IfcClass.Annotation, e.Name, attrs, e.PredefinedType);
                    ctx.Add(made);
                    foreach (var rel in m.RelationsOf(e.Id).Where(x => x.Related.Contains(e.Id))) ctx.Relate(rel.Class, rel.Relating, made.Id);
                    ids.Add((JsonNode)made.Id);
                }
                else
                {
                    ctx.Replace(e.With(attrs: attrs));
                    ids.Add((JsonNode)e.Id);
                }
            }
            return new JsonObject { ["ids"] = ids };
        });

        // A driving dimension: typing a segment's value moves what it measures (and, in a string, what follows).
        r.Command("setDimensionValue", "Dimension value", (ctx, a) =>
        {
            var dim = ctx.Model.Get((string?)a["id"] ?? "") is { Class: IfcClass.Annotation, PredefinedType: "DIMENSION" } d ? d : throw new CommandException("Not a dimension.");
            var value = Json.Number(a["value"]) ?? throw new CommandException("Which value?");
            if (value <= 0) throw new CommandException("A dimension's value must be positive.");
            if (DimensionKinds.IsKind(dim)) { DimensionKinds.Drive(ctx, dim, value); return null; }
            var view = Views.Find(ctx.Model, dim.Str("viewId") ?? "") ?? throw new CommandException("The dimension's view is gone.");
            var (drawing, spec, problem) = Views.Generate(ctx.Query, view);
            if (drawing == null || spec == null) throw new CommandException(problem ?? "The view cannot be drawn.");
            Dimensions.Drive(ctx, dim, drawing, spec.Kind, value, (int)(Json.Number(a["segment"]) ?? 0));
            return null;
        });

        // The text written over a segment instead of its measure (empty: the measure again).
        r.Command("setDimensionText", "Dimension text", (ctx, a) =>
        {
            var dim = Linear(ctx.Model, a);
            var st = Dimensions.Stations(dim);
            var texts = Dimensions.TextsOf(dim.Attrs, st.Count - 1);
            var i = a["segment"] == null ? 0 : Json.Number(a["segment"]) is { } sg && sg >= 0 && sg < texts.Count ? (int)sg : throw new CommandException("The dimension has no such segment.");
            texts[i] = ((string?)a["text"])?.Trim() is { Length: > 0 } t ? t : null;
            Save(ctx, dim, st, texts);
            return null;
        });

        // A segment's value moved by hand, to stand clear of its neighbours (by: plan metres, added to where it stands; reset: back on its line).
        r.Command("moveDimensionText", "Move dimension text", (ctx, a) =>
        {
            var dim = Linear(ctx.Model, a);
            var st = Dimensions.Stations(dim);
            var offsets = Dimensions.OffsetsOf(dim.Attrs, st.Count - 1);
            var i = a["segment"] == null ? 0 : Json.Number(a["segment"]) is { } sg && sg >= 0 && sg < offsets.Count ? (int)sg : throw new CommandException("The dimension has no such segment.");
            if (a["reset"] is JsonValue rv && rv.TryGetValue<bool>(out var reset) && reset) offsets[i] = null;
            else offsets[i] = (offsets[i] ?? default) + P(a["by"]);
            var attrs = dim.CloneAttrs();
            Dimensions.WriteOffsets(attrs, offsets);
            ctx.Replace(dim.With(attrs: attrs));
            return null;
        });

        // A string's point moved: it holds on what it lands on.
        r.Command("moveDimensionPoint", "Move dimension point", (ctx, a) =>
        {
            var dim = Linear(ctx.Model, a);
            var st = Current(ctx.Query, dim);
            var i = Index(a, st.Count);
            var p = P(a["point"]);
            st[i] = (p, Associator(ctx, View(ctx.Model, dim))(p));
            Save(ctx, dim, st, Dimensions.TextsOf(dim.Attrs, st.Count - 1));
            return null;
        });

        // Points put into a string: before the point at `index` (0: before the first; the count: after the last), or,
        // without one, each where it falls along the dimension line, as ArchiCAD inserts a point (one beyond an end: the new end).
        r.Command("insertDimensionPoint", "Add dimension point", (ctx, a) =>
        {
            var dim = Linear(ctx.Model, a);
            var st = Current(ctx.Query, dim);
            var texts = Dimensions.TextsOf(dim.Attrs, st.Count - 1);
            var offsets = Dimensions.OffsetsOf(dim.Attrs, st.Count - 1);
            var hold = Associator(ctx, View(ctx.Model, dim));
            var pts = a["points"] is JsonArray pa ? pa.Select(P).ToList() : [P(a["point"])];
            var u = Dimensions.Along(dim.Attrs, st[0].P, st[^1].P);
            var added = new JsonArray();
            foreach (var p in pts)
            {
                int i;
                if (a["index"] != null)
                    i = Json.Number(a["index"]) is { } ix && ix >= 0 && ix <= st.Count ? (int)ix : throw new CommandException("Where in the string?");
                else
                {
                    if (u is not { } dir) throw new CommandException("The dimension has no direction.");
                    var t = st.Select(s => Vec2.Dot(s.P - st[0].P, dir)).ToList();
                    var tp = Vec2.Dot(p - st[0].P, dir);
                    // A point the string measures already is not doubled.
                    if (t.Any(x => Math.Abs(x - tp) < 1e-4)) continue;
                    // The string runs one way along the line (first to last); the point goes where it falls in it.
                    var sign = t[^1] >= t[0] ? 1 : -1;
                    i = 0;
                    while (i < st.Count && sign * t[i] < sign * tp) i++;
                }
                var r = hold(p);
                st.Insert(i, (p, r));
                // The segment split in two measures anew; one added at an end has no text of its own.
                if (i == 0) { texts.Insert(0, null); offsets.Insert(0, null); }
                else if (i == st.Count - 1) { texts.Add(null); offsets.Add(null); }
                else { texts.RemoveAt(i - 1); texts.InsertRange(i - 1, [null, null]); offsets.RemoveAt(i - 1); offsets.InsertRange(i - 1, [null, null]); }
                added.Add((JsonNode)(r != null));
            }
            if (added.Count == 0) throw new CommandException("The string measures that point already.");
            // An aligned string measures from its first point to its last: a new end off that line keeps the direction it had.
            if (dim.Attrs["dir"] is not JsonArray && u is { } keep && Dimensions.Along(dim.Attrs, st[0].P, st[^1].P) is { } now && Math.Abs(Vec2.Cross(keep, now)) > 1e-9)
            {
                var attrs = dim.CloneAttrs();
                attrs["dir"] = J(keep);
                dim = dim.With(attrs: attrs);
            }
            Save(ctx, dim, st, texts, offsets);
            return new JsonObject { ["associated"] = added };
        });

        // A point taken out of a string; a dimension keeps two.
        r.Command("removeDimensionPoint", "Remove dimension point", (ctx, a) =>
        {
            var dim = Linear(ctx.Model, a);
            var st = Current(ctx.Query, dim);
            var i = Index(a, st.Count);
            if (st.Count <= 2) throw new CommandException("A dimension keeps two points at least.");
            var texts = Dimensions.TextsOf(dim.Attrs, st.Count - 1);
            var offsets = Dimensions.OffsetsOf(dim.Attrs, st.Count - 1);
            st.RemoveAt(i);
            if (i == 0) { texts.RemoveAt(0); offsets.RemoveAt(0); }
            else if (i == texts.Count) { texts.RemoveAt(texts.Count - 1); offsets.RemoveAt(offsets.Count - 1); }
            else { texts.RemoveAt(i); texts[i - 1] = null; offsets.RemoveAt(i); offsets[i - 1] = null; }
            Save(ctx, dim, st, texts, offsets);
            return null;
        });

        // A string as single dimensions, one per segment (the first keeps the string's id).
        r.Command("splitDimension", "Split dimension", (ctx, a) =>
        {
            var dim = Linear(ctx.Model, a);
            var parts = DimensionStrings.Split(dim);
            var ids = new JsonArray(dim.Id);
            if (parts.Count < 2) return new JsonObject { ["ids"] = ids };
            ctx.Replace(dim.With(attrs: parts[0]));
            var rels = ctx.Model.RelationsOf(dim.Id).Where(x => x.Related.Contains(dim.Id)).ToList();
            foreach (var attrs in parts.Skip(1))
            {
                var e = new Entity(ctx.NewId(), IfcClass.Annotation, dim.Name, attrs, "DIMENSION");
                ctx.Add(e);
                foreach (var rel in rels) ctx.Relate(rel.Class, rel.Relating, e.Id);
                ids.Add((JsonNode)e.Id);
            }
            return new JsonObject { ["ids"] = ids };
        });

        // Dimensions on one dimension line, along one direction, each going on from where another ends: one string (the first's id).
        r.Command("joinDimensions", "Join dimensions", (ctx, a) =>
        {
            var dims = (a["ids"] as JsonArray)?.Select(x => ctx.Model.Get((string?)x ?? "")).Where(Dimensions.IsLinear).Cast<Entity>().DistinctBy(x => x.Id).ToList() ?? [];
            if (dims.Count < 2) throw new CommandException("Select two dimensions or more to join.");
            var attrs = DimensionStrings.Joined(dims.Select(DimensionStrings.PieceOf).ToList(), 1e-4)
                ?? throw new CommandException("These dimensions do not go on from one another on one dimension line.");
            ctx.Replace(dims[0].With(attrs: attrs));
            foreach (var d in dims.Skip(1)) ctx.Remove(d.Id);
            return new JsonObject { ["id"] = dims[0].Id };
        });

        // Dimensions that lost their element let go of it: they measure where they are drawn.
        r.Command("freeDimensions", "Free dimensions", (ctx, a) =>
        {
            var ids = (a["ids"] as JsonArray)?.Select(x => (string?)x).OfType<string>().ToList() ?? Orphans(ctx.Query, (string?)a["viewId"]);
            var freed = 0;
            foreach (var id in ids)
            {
                if (ctx.Model.Get(id) is not { } dim || !Dimensions.IsLinear(dim)) continue;
                var view = Views.Find(ctx.Model, dim.Str("viewId") ?? "");
                var (drawing, spec, _) = view == null ? (null, null, null) : Views.Generate(ctx.Query, view);
                var st = Dimensions.Stations(dim);
                for (var i = 0; i < st.Count; i++)
                    if (st[i].Ref is { } r && (drawing == null || spec == null || Dimensions.Resolve(ctx.Model, spec.Kind, drawing, r) == null)) { st[i] = (st[i].P, null); freed++; }
                var attrs = dim.CloneAttrs();
                Dimensions.Write(attrs, st);
                ctx.Replace(dim.With(attrs: attrs));
            }
            return new JsonObject { ["freed"] = freed };
        });

        r.Query("drafting.orphans", (q, a) => new JsonArray(Orphans(q, (string?)a["viewId"]).Select(x => (JsonNode)x).ToArray()));

        // What a dimension measures now (for editing its value): each segment's value, and the one asked for (by
        // `segment`, or the one nearest `at`), whether it drives (an end of it holds on something).
        r.Query("drafting.dimension", (q, a) =>
        {
            if (q.Model.Get((string?)a["id"] ?? "") is not { Class: IfcClass.Annotation, PredefinedType: "DIMENSION" } dim || Views.Find(q.Model, dim.Str("viewId") ?? "") is not { } view) return null;
            if (DimensionKinds.IsKind(dim))
            {
                var (v, lost) = DimensionKinds.Measure(null, q.Model, dim);
                return new JsonObject { ["value"] = double.IsInfinity(v) ? null : v, ["orphan"] = lost, ["driving"] = dim.Str("dimKind") is "radial" or "diameter" && dim.Str("shape") != null, ["kind"] = dim.Str("dimKind") };
            }
            var (drawing, spec, _) = Views.Generate(q, view);
            if (drawing == null || spec == null) return null;
            var (pts, orphan) = Dimensions.Points(q.Model, spec.Kind, dim, drawing);
            var values = Dimensions.Values(dim, pts);
            if (values.Count == 0) return null;
            var segment = Json.Number(a["segment"]) is { } sg ? Math.Clamp((int)sg, 0, values.Count - 1)
                : a["at"] is JsonArray && Dimensions.Lay(dim, pts) is { } lay ? Dimensions.SegmentAt(lay, P(a["at"])) : 0;
            var refs = Dimensions.Stations(dim).Select(s => s.Ref).ToList();
            return new JsonObject
            {
                ["value"] = values[segment], ["values"] = new JsonArray(values.Select(v => (JsonNode)v).ToArray()), ["segment"] = segment, ["segments"] = values.Count,
                ["orphan"] = orphan, ["driving"] = refs[segment] != null || refs[segment + 1] != null,
                ["drives"] = new JsonArray(values.Select((_, i) => (JsonNode)(refs[i] != null || refs[i + 1] != null)).ToArray()),
            };
        });

        r.Command("moveDimension", "Move dimension", (ctx, a) =>
        {
            var dim = ctx.Model.Get((string?)a["id"] ?? "") is { Class: IfcClass.Annotation, PredefinedType: "DIMENSION" } d ? d : throw new CommandException("Not a dimension.");
            var attrs = dim.CloneAttrs();
            attrs["at"] = J(P(a["at"]));
            ctx.Replace(dim.With(attrs: attrs));
            return null;
        });

        // Draw order (report #44, decision 339): {ids, move: front|forward|backward|back|behind|above|under, refs? (for above and
        // under)}: drafted elements on their views brought to the front, a step forward past what they overlap, a step back,
        // to the back of the drafting (over the model) or behind the model; each view's stack numbered again, one undo step.
        r.Command("drawOrder", "Draw order", (ctx, a) =>
        {
            var m = ctx.Model;
            var move = (string?)a["move"] ?? throw new CommandException($"How? One of {string.Join(", ", DrawOrder.Moves)}.");
            if (!DrawOrder.Moves.Contains(move)) throw new CommandException($"An order is one of {string.Join(", ", DrawOrder.Moves)}.");
            var items = Args.Ids(a).Select(id => m.Get(id)).OfType<Entity>().Where(e => e.Class == IfcClass.Annotation && e.Str("viewId") != null).ToList();
            if (items.Count == 0) throw new CommandException("Select drafted elements on a view: lines, hatches, texts, dimensions, tags.");
            var refs = a["refs"] is JsonArray ra ? ra.Select(x => (string?)x).OfType<string>().ToList() : null;
            var changed = 0;
            foreach (var g in items.GroupBy(e => e.Str("viewId")!))
            {
                var view = Views.Find(m, g.Key) ?? throw new CommandException("A drafted element has lost its view.");
                var boxes = move is "forward" or "backward" ? Boxes(ctx.Query, view) : null;
                changed += DrawOrder.Apply(ctx, view.Id, g.Select(e => e.Id).ToList(), move, refs, boxes);
            }
            return new JsonObject { ["changed"] = changed };
        });

        r.Command("addText", "Text", (ctx, a) =>
        {
            var viewId = (string?)a["viewId"] ?? throw new CommandException("A text is written on a view.");
            if (Views.Find(ctx.Model, viewId) == null) throw new CommandException("There is no such view.");
            var body = ((string?)a["text"])?.Trim();
            if (string.IsNullOrEmpty(body)) throw new CommandException("A text cannot be empty.");
            var attrs = new JsonObject { ["viewId"] = viewId, ["at"] = J(P(a["at"])), ["text"] = body, ["angle"] = Json.Number(a["angle"]) ?? 0 };
            if ((string?)a["textStyle"] is { } ts) attrs["textStyle"] = ts;
            var t = new Entity(ctx.NewId(), IfcClass.Annotation, body.Length > 24 ? body[..24] + "…" : body, attrs, "TEXT");
            ctx.Add(t);
            return new JsonObject { ["id"] = t.Id };
        });
    }

    public IEnumerable<ViewMark> Marks(ViewContext c) => Cad.ViewImages.Marks(c).Concat(Cad.Drafts.Marks(c)).Concat(Grids.Marks(c)).Concat(Dimensions.Marks(c)).Concat(DimensionKinds.Marks(c)).Concat(Texts.Marks(c)).Concat(Cad.SymbolInstances.Marks(c)).Concat(Tags.Marks(c));

    /// <summary>What each drafted element covers in a view, from what it draws there (its lines, fills and texts' anchors).</summary>
    static Dictionary<string, (Vec2 Min, Vec2 Max)> Boxes(IQueryContext q, Entity view)
    {
        var o = new Dictionary<string, (Vec2 Min, Vec2 Max)>();
        void Add(string id, Vec2 p) => o[id] = o.TryGetValue(id, out var b) ? (new Vec2(Math.Min(b.Min.X, p.X), Math.Min(b.Min.Y, p.Y)), new Vec2(Math.Max(b.Max.X, p.X), Math.Max(b.Max.Y, p.Y))) : (p, p);
        var mm = (view.NumOrNull("scale") ?? 100) / 1000;
        foreach (var mk in Views.Render(q, view, withMarks: false).Marks)
        {
            if (mk.Owner is not { } id) continue;
            foreach (var l in mk.Lines) { Add(id, l.A); Add(id, l.B); }
            foreach (var (loops, _) in mk.Fills) foreach (var loop in loops) foreach (var p in loop) Add(id, p);
            foreach (var (t, _) in mk.Texts) { var w = t.Text.Length * t.Height * 0.6 * mm; Add(id, t.At - new Vec2(w, 0)); Add(id, t.At + new Vec2(w, t.Height * mm)); }
            foreach (var img in mk.Images) foreach (var p in img.Corners) Add(id, p);
        }
        return o;
    }

    static void Write(ICommandContext ctx, Entity grid, List<Axis> axes)
    {
        var attrs = grid.CloneAttrs();
        attrs["axes"] = new JsonArray(axes.Select(x => (JsonNode)x.ToJson()).ToArray());
        ctx.Replace(grid.With(attrs: attrs));
    }

    /// <summary>The dimensions on a view (every view when none is named) that lost an element.</summary>
    static List<string> Orphans(IQueryContext q, string? viewId)
    {
        var o = new List<string>();
        foreach (var g in q.Model.OfClass(IfcClass.Annotation).Where(x => x.PredefinedType == "DIMENSION" && (viewId == null || x.Str("viewId") == viewId)).GroupBy(x => x.Str("viewId") ?? ""))
        {
            if (Views.Find(q.Model, g.Key) is not { } view) { o.AddRange(g.Select(x => x.Id)); continue; }
            var (drawing, spec, _) = Views.Generate(q, view);
            if (drawing == null || spec == null) continue;
            o.AddRange(g.Where(x => Dimensions.IsLinear(x) && Dimensions.Points(q.Model, spec.Kind, x, drawing).Orphan).Select(x => x.Id));
        }
        return o;
    }

    /// <summary>The dimension string an edit names (<c>id</c>).</summary>
    static Entity Linear(IModelView m, JsonObject a) =>
        m.Get((string?)a["id"] ?? "") is { } d && Dimensions.IsLinear(d) ? d : throw new CommandException("Not a linear or aligned dimension.");

    static Entity View(IModelView m, Entity dim) => Views.Find(m, dim.Str("viewId") ?? "") ?? throw new CommandException("The dimension's view is gone.");

    static int Index(JsonObject a, int count) =>
        Json.Number(a["index"]) is { } ix && ix >= 0 && ix < count ? (int)ix : throw new CommandException("The dimension has no such point.");

    /// <summary>Consecutive points that are one point (within a tenth of a millimetre, what a dimension can tell apart), once.</summary>
    static List<Vec2> Distinct(List<Vec2> pts) => Polyline2.Dedupe(pts, tolerance: 1e-4);

    /// <summary>What a point placed on <paramref name="view"/> holds on: an edge the view cuts (as drawn when the command began), a grid axis, a wall's centre line; null: nothing.</summary>
    static Func<Vec2, DimEndRef?> Associator(ICommandContext ctx, Entity view)
    {
        var (drawing, spec, _) = Views.Generate(ctx.Query, view);
        if (drawing == null || spec == null) return _ => null;
        var tolerance = Tolerance(view);
        return p => Dimensions.Associate(ctx.Model, spec.Kind, drawing, p, tolerance);
    }

    /// <summary>A plan of any sort (a floor plan, a site or location plan, a ceiling plan): they all share the model's x and y.</summary>
    static bool IsPlan(Entity view) => view.Str("kind") is "plan" or null;

    /// <summary>A storey's floor plan: its first plan view that is not a site view, else the storey's own plan.</summary>
    static Entity FloorPlan(IModelView m, string storey) =>
        m.OfClass(Views.Class).FirstOrDefault(v => v.Str("kind") == "plan" && v.Str("storeyId") == storey && SitePlans.Of(v) == null)
        ?? Views.Find(m, "plan:" + storey)!;

    /// <summary>How near (m) a dimension's point must be to an edge to hold on it: 1 mm on the paper, at least 1 cm.</summary>
    static double Tolerance(Entity view) => Math.Max(0.01, 1.0 * (view.NumOrNull("scale") ?? 100) / 1000);

    /// <summary>A string's points where they are drawn now (each on what it holds on; one that lost it where it was), with what they hold on.</summary>
    static List<(Vec2 P, DimEndRef? Ref)> Current(IQueryContext q, Entity dim)
    {
        var st = Dimensions.Stations(dim);
        if (Views.Find(q.Model, dim.Str("viewId") ?? "") is not { } view) return st;
        var (drawing, spec, _) = Views.Generate(q, view);
        if (drawing == null || spec == null) return st;
        return st.Select(s => s.Ref != null && Dimensions.Resolve(q.Model, spec.Kind, drawing, s.Ref, s.P) is { } p ? (p, s.Ref) : s).ToList();
    }

    static void Save(ICommandContext ctx, Entity dim, List<(Vec2 P, DimEndRef? Ref)> stations, List<string?> texts, List<Vec2?>? offsets = null)
    {
        var attrs = dim.CloneAttrs();
        Dimensions.Write(attrs, stations, texts);
        if (offsets != null) Dimensions.WriteOffsets(attrs, offsets);
        ctx.Replace(dim.With(attrs: attrs));
    }

    /// <summary>Points on the end of a string (the first not doubled when it is the string's last); whether each new one holds on something.</summary>
    static JsonArray Extend(ICommandContext ctx, Entity dim, List<Vec2> pts)
    {
        var st = Current(ctx.Query, dim);
        var texts = Dimensions.TextsOf(dim.Attrs, st.Count - 1);
        var stored = Dimensions.Stations(dim)[^1].P;
        var hold = Associator(ctx, View(ctx.Model, dim));
        var added = new JsonArray();
        foreach (var p in pts)
        {
            if ((p - st[^1].P).Length < 1e-4 || (added.Count == 0 && (p - stored).Length < 1e-4)) continue;
            var r = hold(p);
            st.Add((p, r));
            texts.Add(null);
            added.Add((JsonNode)(r != null));
        }
        if (added.Count == 0) throw new CommandException("The string goes on to a point it does not have yet.");
        Save(ctx, dim, st, texts);
        return added;
    }

    static Vec2 P(JsonNode? n) => n is JsonArray a && a.Count >= 2 && Json.Number(a[0]) is { } x && Json.Number(a[1]) is { } y
        ? new Vec2(x, y) : throw new CommandException("A point is [x, y].");
    static JsonArray J(Vec2 p) => new(p.X, p.Y);
}

/// <summary>
/// One grid axis (selected as grid#tag) moved, turned or mirrored on its own;
/// a copy is a new axis on the same grid, labelled after the last one that
/// runs its way (1, 2, 3 → 4; A, B → C), never a label the grid has.
/// </summary>
sealed class AxisPlacement : IPartPlacement
{
    public string? Transform(ICommandContext ctx, string id, Placement2D t, bool copy)
    {
        if (Grids.ParseAxisId(id) is not var (gridId, tag) || ctx.Model.Get(gridId) is not { Class: IfcClass.Grid } grid) return null;
        var axes = Grids.Axes(grid);
        var i = axes.FindIndex(x => x.Tag == tag);
        if (i < 0) return null;
        var moved = axes[i] with { A = t.Point(axes[i].A), B = t.Point(axes[i].B) };
        if (copy)
        {
            var book = DraftingResources.Book(ctx.Model);
            var newTag = Grids.NextTag(axes, moved.Dir, Standards.AxisOf(book, book.DefaultAxisStyle).FirstLabel);
            while (axes.Any(x => x.Tag == newTag)) newTag = Grids.Next(newTag);
            moved = moved with { Tag = newTag };
            axes.Add(moved);
        }
        else axes[i] = moved;
        var attrs = grid.CloneAttrs();
        attrs["axes"] = new JsonArray(axes.Select(x => (JsonNode)x.ToJson()).ToArray());
        ctx.Replace(grid.With(attrs: attrs));
        return Grids.AxisId(grid.Id, moved.Tag);
    }
}

/// <summary>Deleting one grid axis (selected as grid#tag): the grid goes with its last one.</summary>
sealed class AxisDeletion : IDeletion
{
    public bool Delete(ICommandContext ctx, string id)
    {
        if (Grids.ParseAxisId(id) is not var (gridId, tag) || ctx.Model.Get(gridId) is not { Class: IfcClass.Grid } grid) return false;
        var axes = Grids.Axes(grid);
        if (axes.RemoveAll(x => x.Tag == tag) == 0) return false;
        if (axes.Count == 0) ctx.Remove(grid.Id);
        else
        {
            var attrs = grid.CloneAttrs();
            attrs["axes"] = new JsonArray(axes.Select(x => (JsonNode)x.ToJson()).ToArray());
            ctx.Replace(grid.With(attrs: attrs));
        }
        return true;
    }
}

/// <summary>
/// A drafted line, polyline, arc or circle in a plan, as an array follows it (decision 481): an arc and a circle exactly
/// (a circle from its east point, counter-clockwise), the rest by their points. A shape drawn on a section or an
/// elevation is in that view's coordinates, not in plan: refused.
/// </summary>
sealed class ShapeArrayPaths : IArrayPaths
{
    public ArrayPath? PathOf(IModelView m, Entity e)
    {
        if (e.Class != IfcClass.Annotation || Cad.Drafts.ShapeOf(e) is not { } shape) return null;
        if (e.Str("viewId") is { } viewId && m.Get(viewId) is { } view && (view.Str("kind") ?? "plan") != "plan")
            throw new CommandException($"„{e.Name}” is drawn on a section or an elevation: an array follows a line drawn in a plan.");
        switch (shape)
        {
            case Cad.Arc a:
                return new ArrayPath(a.R * a.Sweep, s => { var t = a.Start + s / a.R; return (a.At(t), new Vec2(-Math.Sin(t), Math.Cos(t))); }, Cad.Geo.Polylines(a)[0]);
            case Cad.Circle c:
                return new ArrayPath(2 * Math.PI * c.R, s => { var t = s / c.R; return (c.C + new Vec2(Math.Cos(t), Math.Sin(t)) * c.R, new Vec2(-Math.Sin(t), Math.Cos(t))); }, Cad.Geo.Polylines(c)[0], Closed: true);
            default:
                return Cad.Geo.Polylines(shape) is { Count: > 0 } lines && SectionPaths.Clean(lines[0]).Count >= 2 ? ArrayPath.Polyline(lines[0]) : null;
        }
    }
}

/// <summary>A drafted line, polyline or arc can be followed by a section (a circle as its polyline).</summary>
sealed class ShapeSectionPaths : ISectionPaths
{
    public FollowedPath? PathOf(IModelView m, Entity e) =>
        e.Class == IfcClass.Annotation && Cad.Drafts.ShapeOf(e) is { } shape && Cad.Geo.Polylines(shape) is { Count: > 0 } lines && lines[0].Count >= 2 ? new FollowedPath(lines[0]) : null;
}
