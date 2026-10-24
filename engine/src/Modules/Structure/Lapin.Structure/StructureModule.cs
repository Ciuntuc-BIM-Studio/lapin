using System.Text.Json.Nodes;
using BubbleBim.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Structure;

/// <summary>Structure: columns, beams, strip and pad foundations.</summary>
public sealed class StructureModule : ILapinModule
{
    public string Id => "structure";
    public string Name => "Structure";

    public void Register(IModuleRegistry r)
    {
        FieldDescriptor priority = new("cutPriority", "Cut priority", "number", Min: 0, Max: 100);
        r.Class(new(IfcClass.Column, "Column", [new("height", "Height (own)", "length", Min: 0.01), new("baseOffset", "Base offset", "length"), new("rotation", "Rotation", "angle"), priority], "#a8a8a8"));
        r.Class(new(IfcClass.ColumnType, "Column type", [new("height", "Height", "length", Min: 0.01)], "#a8a8a8", Kind: "type"));
        r.Class(new(IfcClass.Beam, "Beam", [new("topOffset", "Top offset", "length"), priority], "#a8a8a8"));
        r.Class(new(IfcClass.BeamType, "Beam type", [], "#a8a8a8", Kind: "type"));
        r.Class(new(IfcClass.Footing, "Footing", [new("rotation", "Rotation", "angle"), priority], "#a8a8a8"));
        r.Class(new(IfcClass.FootingType, "Footing type", [new("bottom", "Bottom level", "length")], "#a8a8a8", Kind: "type"));

        r.Geometry(IfcClass.Column, Columns.Geometry);
        r.Geometry(IfcClass.Beam, Beams.Geometry);
        r.Geometry(IfcClass.Footing, (f, m, s) => IsPad(m, f) ? PadFootings.Geometry(f, m, s) : StripFootings.Geometry(f, m, s));
        r.Quantities(IfcClass.Column, Columns.Quantities);
        r.Quantities(IfcClass.Beam, Beams.Quantities);
        r.Quantities(IfcClass.Footing, (f, g, m) => IsPad(m, f) ? PadFootings.Quantities(f, g, m) : StripFootings.Quantities(f, g, m));

        // Cut priorities (the old Lapin's): footing 95, column 90, beam 80.
        r.Cutter(IfcClass.Footing, 95, (f, m) => IsPad(m, f) ? PadFootings.Envelope(f, m) : StripFootings.Envelope(f, m));
        r.Cutter(IfcClass.Column, 90, Columns.Envelope);
        r.Cutter(IfcClass.Beam, 80, Beams.Envelope);
        // Beams and strip foundations join as walls do.
        r.PathElement(IfcClass.Beam, Beams.Frame);
        r.PathElement(IfcClass.Footing, StripFootings.Frame);

        // ------------------------------------------------------------ columns
        r.Command("createColumnType", "New column type", (ctx, a) =>
            Section.Parse(a["section"]) is var cs ? new JsonObject { ["id"] = Columns.CreateType(ctx, (string?)a["name"], cs, Json.Number(a["height"]), (string?)a["material"] ?? (cs.IsProfile ? "Steel" : "Reinforced concrete")).Id } : null);

        r.Command("addColumn", "Column", (ctx, a) =>
        {
            var storey = Place.Storey(ctx, a);
            var type = Place.TypeFor(ctx, a, IfcClass.ColumnType, () => Columns.DefaultType(ctx));
            var c = new Entity(ctx.NewId(), IfcClass.Column, type.Name, new JsonObject { ["at"] = Place.Json2(Place.P(a, "x", "y")), ["rotation"] = Json.Number(a["rotation"]) ?? 0 }, "COLUMN");
            ctx.Add(c);
            Spatial.Contain(ctx, storey.Id, c.Id);
            ctx.Relate(IfcClass.RelDefinesByType, type.Id, c.Id);
            return new JsonObject { ["id"] = c.Id };
        });

        // A column at every crossing of the grid's axes (only those named in `tags`, when given) on the storey, except where one stands already.
        r.Command("addColumnsAtGrid", "Columns at grid", (ctx, a) =>
        {
            var storey = Place.Storey(ctx, a);
            var type = Place.TypeFor(ctx, a, IfcClass.ColumnType, () => Columns.DefaultType(ctx));
            var only = (a["tags"] as JsonArray)?.Select(x => (string?)x).OfType<string>().ToHashSet();
            static Vec2 P(JsonNode? n) => n is JsonArray p && p.Count >= 2 ? new Vec2(Json.Number(p[0]) ?? 0, Json.Number(p[1]) ?? 0) : default;
            var axes = ctx.Model.OfClass(IfcClass.Grid).SelectMany(g => (g.Attrs["axes"] as JsonArray)?.OfType<JsonObject>() ?? [])
                .Where(x => only == null || only.Contains((string?)x["tag"] ?? "")).Select(x => (A: P(x["a"]), B: P(x["b"]))).ToList();
            var existing = ctx.Model.RelatedOf(storey.Id, IfcClass.RelContainedInSpatialStructure).Where(e => e.Class == IfcClass.Column).Select(e => P(e.Attrs["at"])).ToList();
            var ids = new JsonArray();
            for (var i = 0; i < axes.Count; i++)
                for (var j = i + 1; j < axes.Count; j++)
                {
                    var (a1, a2, b1, b2) = (axes[i].A, axes[i].B, axes[j].A, axes[j].B);
                    var (d1, d2) = (a2 - a1, b2 - b1);
                    var den = Vec2.Cross(d1, d2);
                    if (Math.Abs(den) < 1e-9 * d1.Length * d2.Length) continue;
                    var t = Vec2.Cross(b1 - a1, d2) / den;
                    var u = Vec2.Cross(b1 - a1, d1) / den;
                    if (t < -1e-9 || t > 1 + 1e-9 || u < -1e-9 || u > 1 + 1e-9) continue;
                    var at = a1 + d1 * t;
                    if (existing.Any(p => (p - at).Length < 1e-3)) continue;
                    existing.Add(at);
                    var c = new Entity(ctx.NewId(), IfcClass.Column, type.Name, new JsonObject { ["at"] = Place.Json2(at), ["rotation"] = Math.Atan2(d1.Y, d1.X) * 180 / Math.PI }, "COLUMN");
                    ctx.Add(c);
                    Spatial.Contain(ctx, storey.Id, c.Id);
                    ctx.Relate(IfcClass.RelDefinesByType, type.Id, c.Id);
                    ids.Add(c.Id);
                }
            if (ids.Count == 0) throw new CommandException("No grid crossing without a column on this storey.");
            return new JsonObject { ["ids"] = ids };
        });

        r.Command("updateColumn", "Move column", (ctx, a) =>
        {
            var c = Spatial.Require(ctx.Model, (string?)a["id"], IfcClass.Column);
            var attrs = c.CloneAttrs();
            var at = Place.Point(c);
            attrs["at"] = Place.Json2(new Vec2(Json.Number(a["x"]) ?? at.X, Json.Number(a["y"]) ?? at.Y));
            if (Json.Number(a["rotation"]) is { } rot) attrs["rotation"] = rot;
            ctx.Replace(c.With(attrs: attrs));
            return null;
        });

        // ------------------------------------------------------------ beams
        r.Command("createBeamType", "New beam type", (ctx, a) =>
            Section.Parse(a["section"]) is var bs ? new JsonObject { ["id"] = Beams.CreateType(ctx, (string?)a["name"], bs, (string?)a["material"] ?? (bs.IsProfile ? "Steel" : "Reinforced concrete")).Id } : null);

        r.Command("addBeam", "Beam", (ctx, a) =>
        {
            var storey = Place.Storey(ctx, a);
            var (s, e) = (Place.P(a, "x1", "y1"), Place.P(a, "x2", "y2"));
            if ((e - s).Length < 1e-3) throw new CommandException("A beam needs two different points.");
            var type = Place.TypeFor(ctx, a, IfcClass.BeamType, () => Beams.DefaultType(ctx));
            var b = new Entity(ctx.NewId(), IfcClass.Beam, type.Name, new JsonObject { ["start"] = Place.Json2(s), ["end"] = Place.Json2(e), ["topOffset"] = Json.Number(a["topOffset"]) ?? 0 }, "BEAM");
            ctx.Add(b);
            Spatial.Contain(ctx, storey.Id, b.Id);
            ctx.Relate(IfcClass.RelDefinesByType, type.Id, b.Id);
            Joins.Auto(ctx, Paths(ctx), b.Id, Style(a["joinStyle"]));
            return new JsonObject { ["id"] = b.Id };
        });

        // ------------------------------------------------------------ foundations
        r.Command("createStripType", "New strip foundation type", (ctx, a) =>
            new JsonObject
            {
                ["id"] = StripFootings.CreateType(ctx, (string?)a["name"], LayerSets.Parse(a["layers"], "A strip foundation"),
                    Json.Number(a["stemHeight"]) ?? 1.4, Json.Number(a["footingWidth"]) ?? 0.6, Json.Number(a["footingHeight"]) ?? 0.4, Json.Number(a["bottom"]) ?? -1.8).Id,
            });

        r.Command("addStripFooting", "Strip foundation", (ctx, a) =>
        {
            var storey = Place.Storey(ctx, a);
            var type = Place.TypeFor(ctx, a, IfcClass.FootingType, () => StripFootings.DefaultType(ctx), StripFootings.IsStrip);
            var f = StripFootings.Add(ctx, Paths(ctx), storey, Place.P(a, "x1", "y1"), Place.P(a, "x2", "y2"), type, Style(a["joinStyle"]));
            return new JsonObject { ["id"] = f.Id };
        });

        r.Command("createPadType", "New pad footing type", (ctx, a) =>
        {
            var steps = a["steps"] is JsonArray arr
                ? arr.OfType<JsonObject>().Select(s => new PadStep(Json.Number(s["length"]) ?? 0, Json.Number(s["width"]) ?? 0, Json.Number(s["height"]) ?? 0)).ToList()
                : PadFootings.DefaultSteps;
            return new JsonObject { ["id"] = PadFootings.CreateType(ctx, (string?)a["name"], steps, Json.Number(a["bottom"]) ?? -1.8).Id };
        });

        r.Command("addPadFooting", "Pad footing", (ctx, a) =>
        {
            var storey = Place.Storey(ctx, a);
            var type = Place.TypeFor(ctx, a, IfcClass.FootingType, () => PadFootings.DefaultType(ctx), PadFootings.IsPad);
            var p = new Entity(ctx.NewId(), IfcClass.Footing, type.Name, new JsonObject { ["at"] = Place.Json2(Place.P(a, "x", "y")), ["rotation"] = Json.Number(a["rotation"]) ?? 0 }, "PAD_FOOTING");
            ctx.Add(p);
            Spatial.Contain(ctx, storey.Id, p.Id);
            ctx.Relate(IfcClass.RelDefinesByType, type.Id, p.Id);
            return new JsonObject { ["id"] = p.Id };
        });

        // ------------------------------------------------------------ path elements: ends, types
        r.Command("updatePath", "Change", (ctx, a) =>
        {
            var e = ctx.Model.Get((string?)a["id"] ?? "") is { Class: IfcClass.Beam or IfcClass.Footing } x && Paths(ctx).FrameOf(x, ctx.Model) != null ? x : throw new CommandException("Not a beam or a strip foundation.");
            var (s, en) = (Joins.Start(e), Joins.End(e));
            var start = new Vec2(Json.Number(a["x1"]) ?? s.X, Json.Number(a["y1"]) ?? s.Y);
            var end = new Vec2(Json.Number(a["x2"]) ?? en.X, Json.Number(a["y2"]) ?? en.Y);
            var keep = a["moveJoinedEnds"] is not JsonValue mj || !mj.TryGetValue<bool>(out var k) || k;
            Joins.MoveEnds(ctx, Paths(ctx), e, start, end, keep, a["detachJoinedEnds"] is JsonValue dj && dj.TryGetValue<bool>(out var detach) && detach);
            return null;
        });

        r.Command("restyleJoins", "Join style", (ctx, a) =>
        {
            var swap = a["swap"] is JsonValue v && v.TryGetValue<bool>(out var sw) && sw;
            var style = Args.JoinStyle(a["style"]);
            var ids = Args.Ids(a);
            if (Joins.Restyle(ctx, Paths(ctx), ids, style, swap) == 0) throw new CommandException("The selection has no corner to restyle.");
            return null;
        });

        r.Command("setType", "Change type", (ctx, a) =>
        {
            var type = ctx.Model.Get((string?)a["typeId"] ?? "") ?? throw new CommandException("There is no such type.");
            foreach (var id in a["ids"] is JsonArray ids ? ids.Select(n => (string?)n).OfType<string>() : [(string?)a["id"] ?? ""])
            {
                var e = ctx.Model.Get(id) ?? throw new CommandException($"There is no {id}.");
                var fits = (e.Class, type.Class) switch
                {
                    (IfcClass.Column, IfcClass.ColumnType) or (IfcClass.Beam, IfcClass.BeamType) => true,
                    (IfcClass.Footing, IfcClass.FootingType) => (e.PredefinedType == "PAD_FOOTING") == PadFootings.IsPad(type),
                    _ => false,
                };
                if (!fits) throw new CommandException($"„{type.Name}” is not a type for „{e.Name}”.");
                if (e.Class == IfcClass.Footing && StripFootings.IsStrip(type)) StripFootings.SetType(ctx, e, type);
                else ctx.Relate(IfcClass.RelDefinesByType, type.Id, e.Id);
            }
            return null;
        });

        r.Catalog("defaults", new JsonObject
        {
            ["column"] = Section.Rectangle(0.3, 0.3).ToJson(),
            ["beam"] = Section.Rectangle(0.25, 0.4).ToJson(),
        });
        r.Catalog("profiles", new JsonArray(Section.Catalogue.Values.Select(p => (JsonNode)p.ToJson()).ToArray()));
    }

    static bool IsPad(IModelView m, Entity footing) => footing.PredefinedType == "PAD_FOOTING" || Place.TypeOf(m, footing.Id) is { } t && PadFootings.IsPad(t);

    static IPathElements Paths(ICommandContext ctx) => ctx.Services.Get<IPathElements>()!;

    static JoinStyle Style(JsonNode? n) => Args.JoinStyle(n) ?? JoinStyle.Butt;
}
