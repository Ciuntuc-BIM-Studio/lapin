using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Drawing;
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
             new("format", "Slope written as", "choice", Choices: DimensionKinds.SlopeFormats), new("z", "Level written", "length")], "#555555", Kind: "annotation"));
        r.Service<IViewAnnotations>(this);
        Cad.DraftCommands.Register(r);
        Cad.DxfImport.Register(r);
        r.Service<IElementPlacement>(new Cad.AnnotationPlacement());
        r.Service<IDeletion>(new AxisDeletion());
        r.Service<IPartPlacement>(new AxisPlacement());
        r.Service<IProjectUpgrade>(new DimensionUpgrade());
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
        // or one point after another (after, extendDimension); its points moved, inserted and taken out like a polyline's.
        r.Command("addDimension", "Dimension", (ctx, a) =>
        {
            var viewId = (string?)a["viewId"] ?? throw new CommandException("A dimension is drawn on a view.");
            var view = Views.Find(ctx.Model, viewId) ?? throw new CommandException("There is no such view.");
            // An angle, a radius, a diameter, an arc length, a slope, a level, an ordinate.
            if (DimensionKinds.Attrs(ctx, viewId, a) is { } kindAttrs)
            {
                var names = new Dictionary<string, string> { ["angular"] = "Angle", ["radial"] = "Radius", ["diameter"] = "Diameter", ["arc"] = "Arc length", ["slope"] = "Slope", ["level"] = "Level", ["ordinate"] = "Ordinate" };
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

        // More points on the end of a string.
        r.Command("extendDimension", "Continue dimension", (ctx, a) =>
        {
            var dim = Linear(ctx.Model, a);
            var pts = a["pts"] is JsonArray pa ? pa.Select(P).ToList() : [P(a["point"])];
            return new JsonObject { ["id"] = dim.Id, ["associated"] = Extend(ctx, dim, pts) };
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

    public IEnumerable<ViewMark> Marks(ViewContext c) => Cad.Drafts.Marks(c).Concat(Grids.Marks(c)).Concat(Dimensions.Marks(c)).Concat(DimensionKinds.Marks(c)).Concat(Texts.Marks(c)).Concat(Tags.Marks(c));

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

    /// <summary>Consecutive points that are one point, once.</summary>
    static List<Vec2> Distinct(List<Vec2> pts)
    {
        var o = new List<Vec2>();
        foreach (var p in pts) if (o.Count == 0 || (o[^1] - p).Length >= 1e-4) o.Add(p);
        return o;
    }

    /// <summary>What a point placed on <paramref name="view"/> holds on: an edge the view cuts (as drawn when the command began), a grid axis, a wall's centre line; null: nothing.</summary>
    static Func<Vec2, DimEndRef?> Associator(ICommandContext ctx, Entity view)
    {
        var (drawing, spec, _) = Views.Generate(ctx.Query, view);
        if (drawing == null || spec == null) return _ => null;
        var tolerance = Math.Max(0.01, 1.0 * (view.NumOrNull("scale") ?? 100) / 1000);
        return p => Dimensions.Associate(ctx.Model, spec.Kind, drawing, p, tolerance);
    }

    /// <summary>A string's points where they are drawn now (each on what it holds on; one that lost it where it was), with what they hold on.</summary>
    static List<(Vec2 P, DimEndRef? Ref)> Current(IQueryContext q, Entity dim)
    {
        var st = Dimensions.Stations(dim);
        if (Views.Find(q.Model, dim.Str("viewId") ?? "") is not { } view) return st;
        var (drawing, spec, _) = Views.Generate(q, view);
        if (drawing == null || spec == null) return st;
        return st.Select(s => s.Ref != null && Dimensions.Resolve(q.Model, spec.Kind, drawing, s.Ref) is { } p ? (p, s.Ref) : s).ToList();
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
