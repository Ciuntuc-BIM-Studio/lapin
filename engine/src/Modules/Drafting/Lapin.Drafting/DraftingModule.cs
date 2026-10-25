using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Drawing;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Drafting;

/// <summary>Drafting: grid axes, dimensions that follow what they measure, texts.</summary>
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
        r.Service<IElementPlacement>(new Cad.AnnotationPlacement());
        r.Service<IDeletion>(new AxisDeletion());
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
            var (p, q, at) = (P(a["a"]), P(a["b"]), P(a["at"]));
            if ((q - p).Length < 1e-4) throw new CommandException("A dimension needs two different points.");
            var attrs = new JsonObject { ["viewId"] = viewId, ["a"] = J(p), ["b"] = J(q), ["at"] = J(at) };
            if ((string?)a["dimStyle"] is { } ds) attrs["dimStyle"] = ds;
            // Linear: measured along a direction (horizontal, vertical…) rather than end to end.
            if (a["dir"] is JsonArray) { var d = P(a["dir"]); if (d.Length > 1e-9) attrs["dir"] = J(d.Normalized()); }
            // In a string: after the dimension named, on its line.
            if ((string?)a["after"] is { } after && ctx.Model.Get(after) is { Class: IfcClass.Annotation, PredefinedType: "DIMENSION" } prev)
            {
                var chain = prev.Str("chain") ?? prev.Id;
                if (prev.Str("chain") == null) { var pa = prev.CloneAttrs(); pa["chain"] = chain; pa["chainIx"] = 0; ctx.Replace(prev.With(attrs: pa)); }
                attrs["chain"] = chain;
                attrs["chainIx"] = prev.Num("chainIx") + 1;
                if (prev.Attrs["dir"] is JsonArray pd && attrs["dir"] == null) attrs["dir"] = pd.DeepClone();
            }
            // Each end on an edge the view cuts, a grid axis or a wall's centre line follows it.
            var scale = view.NumOrNull("scale") ?? 100;
            var (drawing, spec, _) = Views.Generate(ctx.Query, view);
            if (drawing != null && spec != null)
            {
                var tolerance = Math.Max(0.01, 1.0 * scale / 1000);
                if (Dimensions.Associate(ctx.Model, spec.Kind, drawing, p, tolerance) is { } ra) attrs["refA"] = ra.ToJson();
                if (Dimensions.Associate(ctx.Model, spec.Kind, drawing, q, tolerance) is { } rb) attrs["refB"] = rb.ToJson();
            }
            var dim = new Entity(ctx.NewId(), IfcClass.Annotation, "Dimension", attrs, "DIMENSION");
            ctx.Add(dim);
            return new JsonObject { ["id"] = dim.Id, ["associated"] = new JsonArray(attrs["refA"] != null, attrs["refB"] != null) };
        });

        // A driving dimension: typing its value moves what it measures (and, in a string, what follows).
        r.Command("setDimensionValue", "Dimension value", (ctx, a) =>
        {
            var dim = ctx.Model.Get((string?)a["id"] ?? "") is { Class: IfcClass.Annotation, PredefinedType: "DIMENSION" } d ? d : throw new CommandException("Not a dimension.");
            var value = Json.Number(a["value"]) ?? throw new CommandException("Which value?");
            if (value <= 0) throw new CommandException("A dimension's value must be positive.");
            if (DimensionKinds.IsKind(dim)) { DimensionKinds.Drive(ctx, dim, value); return null; }
            var view = Views.Find(ctx.Model, dim.Str("viewId") ?? "") ?? throw new CommandException("The dimension's view is gone.");
            var (drawing, spec, problem) = Views.Generate(ctx.Query, view);
            if (drawing == null || spec == null) throw new CommandException(problem ?? "The view cannot be drawn.");
            Dimensions.Drive(ctx, dim, drawing, spec.Kind, value);
            return null;
        });

        // Dimensions that lost their element let go of it: they measure where they are drawn.
        r.Command("freeDimensions", "Free dimensions", (ctx, a) =>
        {
            var ids = (a["ids"] as JsonArray)?.Select(x => (string?)x).OfType<string>().ToList() ?? Orphans(ctx.Query, (string?)a["viewId"]);
            var freed = 0;
            foreach (var id in ids)
            {
                if (ctx.Model.Get(id) is not { Class: IfcClass.Annotation, PredefinedType: "DIMENSION" } dim) continue;
                var view = Views.Find(ctx.Model, dim.Str("viewId") ?? "");
                var (drawing, spec, _) = view == null ? (null, null, null) : Views.Generate(ctx.Query, view);
                var attrs = dim.CloneAttrs();
                foreach (var k in new[] { "refA", "refB" })
                    if (DimEndRef.From(attrs[k]) is { } r && (drawing == null || spec == null || Dimensions.Resolve(ctx.Model, spec.Kind, drawing, r) == null)) { attrs.Remove(k); freed++; }
                ctx.Replace(dim.With(attrs: attrs));
            }
            return new JsonObject { ["freed"] = freed };
        });

        r.Query("drafting.orphans", (q, a) => new JsonArray(Orphans(q, (string?)a["viewId"]).Select(x => (JsonNode)x).ToArray()));

        // What a dimension measures now (for editing its value).
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
            var (pa, pb, orphan) = Dimensions.Ends(q.Model, spec.Kind, dim, drawing);
            return new JsonObject { ["value"] = Dimensions.Value(dim, pa, pb), ["orphan"] = orphan, ["driving"] = dim.Attrs["refA"] != null || dim.Attrs["refB"] != null };
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
            o.AddRange(g.Where(x => Dimensions.Ends(q.Model, spec.Kind, x, drawing).Orphan).Select(x => x.Id));
        }
        return o;
    }

    static Vec2 P(JsonNode? n) => n is JsonArray a && a.Count >= 2 && Json.Number(a[0]) is { } x && Json.Number(a[1]) is { } y
        ? new Vec2(x, y) : throw new CommandException("A point is [x, y].");
    static JsonArray J(Vec2 p) => new(p.X, p.Y);
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
