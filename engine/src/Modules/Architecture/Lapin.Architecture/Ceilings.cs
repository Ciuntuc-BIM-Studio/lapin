using System.Text.Json;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>
/// A false ceiling's system, as its type sets it (report #44, decision 192): <see cref="System"/> "grid"
/// (lay-in tiles on an exposed T-grid, hung from the structure) or "plain" (a continuous board, plasterboard);
/// the tile's size and thickness, the grid's face width and depth, the grid's rotation, the materials and
/// colours, the hangers' spacing; and what goes in its cells: light panels and air diffusers, a cell each.
/// </summary>
public sealed record CeilingSpec(
    string Name, string System = "grid", double TileWidth = 0.6, double TileLength = 0.6, double Thickness = 0.015,
    double GridWidth = 0.024, double GridDepth = 0.038, double Rotation = 0, bool Hangers = true, double HangerSpacing = 1.2,
    string TileMaterial = "Mineral fibre tile", string GridMaterial = "Steel", string TileColor = "#f1f0ea", string GridColor = "#e4e4e4",
    string LightColor = "#fffbe6", string DiffuserColor = "#d5d9de")
{
    public static readonly string[] Systems = ["grid", "plain"];
    public static CeilingSpec Default(string name = "Grid ceiling 600×600") => new(name);
    public static CeilingSpec Plain(string name = "Plasterboard ceiling") => new(name, "plain", 0.6, 0.6, 0.0125, TileMaterial: "Plasterboard", TileColor: "#f4f4f2");

    public JsonObject ToJson() => (JsonObject)JsonSerializer.SerializeToNode(this, ArchitectureJson.Default.CeilingSpec)!;

    public static CeilingSpec? From(JsonNode? node)
    {
        try
        {
            var s = node?.Deserialize(ArchitectureJson.Default.CeilingSpec);
            if (s is null || s.Name is null) return null;
            return s with
            {
                System = Systems.Contains(s.System) ? s.System : "grid",
                TileWidth = Math.Clamp(s.TileWidth, 0.1, 3), TileLength = Math.Clamp(s.TileLength, 0.1, 3),
                Thickness = Math.Clamp(s.Thickness, 0.005, 0.1), GridWidth = Math.Clamp(s.GridWidth, 0.005, 0.1), GridDepth = Math.Clamp(s.GridDepth, 0.01, 0.2),
                HangerSpacing = Math.Clamp(s.HangerSpacing, 0.3, 3),
            };
        }
        catch (JsonException) { return null; }
    }
}

/// <summary>
/// False ceilings (report #44, decision 192): a <c>LapinCeiling</c> over an outline drawn or taken from a
/// room, its underside <c>height</c> over its storey, of a type (<c>LapinCeilingType</c>, its
/// <see cref="CeilingSpec"/>); its grid laid from <c>origin</c> (by default centred, so the border tiles are
/// even) and its cells' <c>fixtures</c>: light panels and air diffusers, one per cell, placed by a click or
/// laid out every so many cells. Exported as IfcCovering CEILING.
/// </summary>
public static class Ceilings
{
    public const string Class = "LapinCeiling";
    public const string TypeClass = "LapinCeilingType";
    public static readonly string[] FixtureKinds = ["light", "diffuser"];

    public static void Register(IModuleRegistry r)
    {
        r.Class(new(TypeClass, "Ceiling type", [], "#d8d4c8", Kind: "type"));
        r.Class(new(Class, "Ceiling",
        [
            new("height", "Height", "length", Min: 1.5, Max: 20),
        ], "#ece9df"));
        r.Geometry(Class, (e, m, _) => Geometry(e, m));
        r.Quantities(Class, Quantities);
        r.Catalog("ceilings", new JsonObject
        {
            ["spec"] = CeilingSpec.Default().ToJson(),
            ["plain"] = CeilingSpec.Plain().ToJson(),
            ["systems"] = new JsonArray(CeilingSpec.Systems.Select(x => (JsonNode)x).ToArray()),
        });

        r.Command("createCeilingType", "New ceiling type", (ctx, a) =>
        {
            var spec = CeilingSpec.From(a["spec"]) ?? throw new CommandException("The ceiling type cannot be read.");
            return new JsonObject { ["id"] = CreateType(ctx, spec).Id };
        });
        r.Command("updateCeilingType", "Change ceiling type", (ctx, a) =>
        {
            var type = Spatial.Require(ctx.Model, (string?)a["id"], TypeClass);
            var spec = CeilingSpec.From(a["spec"]) ?? throw new CommandException("The ceiling type cannot be read.");
            EnsureMaterials(ctx, spec);
            var attrs = type.CloneAttrs();
            attrs["spec"] = spec.ToJson();
            ctx.Replace(type.With(name: spec.Name, attrs: attrs));
            return null;
        });

        // A ceiling over the outline clicked, or over the room the click falls in.
        r.Command("addCeiling", "Ceiling", (ctx, a) =>
        {
            var storeyId = (string?)a["storeyId"] ?? Spatial.Storeys(ctx.Model).FirstOrDefault()?.Id ?? throw new CommandException("The project has no storey.");
            Spatial.Require(ctx.Model, storeyId, IfcClass.BuildingStorey);
            IReadOnlyList<Vec2> outline;
            if (a["points"] is JsonArray) outline = Slabs.CheckContour(Points(a["points"]));
            else
            {
                var at = Args.Point(a, "x", "y");
                var room = ctx.Model.OfClass(Rooms.Class).Where(x => Spatial.StoreyOf(ctx.Model, x.Id)?.Id == storeyId)
                    .Select(x => Rooms.Of(x, ctx.Model, null)).FirstOrDefault(o => o != null && o.Holds(at))
                    ?? throw new CommandException("No room there: click inside a room, or draw the ceiling's outline.");
                outline = room.Outer;
            }
            var type = (string?)a["typeId"] is { } tid ? Spatial.Require(ctx.Model, tid, TypeClass) : DefaultType(ctx);
            var attrs = new JsonObject { ["contour"] = Slabs.ContourJson(outline), ["height"] = Json.Number(a["height"]) ?? 2.7, ["exportAs"] = "IfcCovering", ["fixtures"] = new JsonArray() };
            var ceiling = new Entity(ctx.NewId(), Class, $"Ceiling {ctx.Model.OfClass(Class).Count() + 1}", attrs, "CEILING");
            ctx.Add(ceiling);
            Spatial.Contain(ctx, storeyId, ceiling.Id);
            ctx.Relate(IfcClass.RelDefinesByType, type.Id, ceiling.Id);
            return new JsonObject { ["id"] = ceiling.Id };
        });

        r.Command("updateCeiling", "Change ceiling", (ctx, a) =>
        {
            var c = Spatial.Require(ctx.Model, (string?)a["id"], Class);
            var attrs = c.CloneAttrs();
            if (a["points"] is JsonArray) attrs["contour"] = Slabs.ContourJson(Slabs.CheckContour(Points(a["points"])));
            if (Json.Number(a["height"]) is { } h) attrs["height"] = Math.Clamp(h, 1.5, 20);
            if (a.ContainsKey("origin")) { if (a["origin"] is JsonArray o && o.Count == 2) attrs["origin"] = o.DeepClone(); else attrs.Remove("origin"); }
            if ((string?)a["typeId"] is { } tid) ctx.Relate(IfcClass.RelDefinesByType, Spatial.Require(ctx.Model, tid, TypeClass).Id, c.Id);
            ctx.Replace(c.With(attrs: attrs));
            return null;
        });

        // A fixture in the cell under a point (none: the cell is a tile again); the ceiling named, or the one over the point on the storey.
        r.Command("ceilingFixture", "Ceiling fixture", (ctx, a) =>
        {
            var at = Args.Point(a, "x", "y");
            var c = (string?)a["id"] is { } id ? Spatial.Require(ctx.Model, id, Class)
                : ctx.Model.OfClass(Class).FirstOrDefault(x => ((string?)a["storeyId"] == null || Spatial.StoreyOf(ctx.Model, x.Id)?.Id == (string?)a["storeyId"]) && Polygon2.Contains(Slabs.Contour(x), at))
                ?? throw new CommandException("No ceiling there: draw one first.");
            var kind = (string?)a["kind"] ?? "light";
            var grid = GridOf(ctx.Model, c);
            var (i, j) = grid.CellAt(at);
            if (kind != "none" && !grid.Whole(i, j)) throw new CommandException("That cell is cut by the ceiling's edge: a fixture goes in a whole cell.");
            var list = Fixtures(c).Where(f => f.I != i || f.J != j).ToList();
            if (kind != "none") list.Add((FixtureKinds.Contains(kind) ? kind : "light", i, j));
            ctx.Replace(c.With(attrs: WithFixtures(c, list)));
            return new JsonObject { ["cell"] = new JsonArray(i, j), ["count"] = list.Count };
        });

        // Fixtures every so many cells (both ways), from a corner whole cell; none clears that kind (or all).
        r.Command("ceilingLayout", "Lay out ceiling fixtures", (ctx, a) =>
        {
            var c = Spatial.Require(ctx.Model, (string?)a["id"], Class);
            var kind = (string?)a["kind"] ?? "light";
            var every = Math.Clamp((int)(Json.Number(a["every"]) ?? 3), 1, 20);
            var offset = Math.Clamp((int)(Json.Number(a["offset"]) ?? 1), 0, 19);
            var grid = GridOf(ctx.Model, c);
            var list = Fixtures(c).ToList();
            if (kind == "none") list.Clear();
            else
            {
                list.RemoveAll(f => f.Kind == kind);
                foreach (var (i, j) in grid.WholeCells())
                    if (Mod(i - grid.I0 - offset, every) == 0 && Mod(j - grid.J0 - offset, every) == 0 && !list.Any(f => f.I == i && f.J == j))
                        list.Add((kind, i, j));
            }
            ctx.Replace(c.With(attrs: WithFixtures(c, list)));
            return new JsonObject { ["count"] = list.Count(f => kind == "none" || f.Kind == kind) };
        });
    }

    static int Mod(int a, int n) => ((a % n) + n) % n;

    static IReadOnlyList<Vec2> Points(JsonNode? node) =>
        (node as JsonArray ?? throw new CommandException("An outline needs its points.")).Select(p => p is JsonArray xy && xy.Count >= 2 && Json.Number(xy[0]) is { } x && Json.Number(xy[1]) is { } y ? new Vec2(x, y) : throw new CommandException("Each point is [x, y].")).ToList();

    public static CeilingSpec SpecOf(IModelView m, Entity c) =>
        (m.RelatingOf(c.Id, IfcClass.RelDefinesByType) is { } t ? CeilingSpec.From(t.Attrs["spec"]) : null) ?? CeilingSpec.Default();

    static Entity CreateType(ICommandContext ctx, CeilingSpec spec)
    {
        EnsureMaterials(ctx, spec);
        var type = new Entity(ctx.NewId(), TypeClass, spec.Name, new JsonObject { ["spec"] = spec.ToJson(), ["exportAs"] = "IfcCoveringType" }, "CEILING");
        ctx.Add(type);
        return type;
    }

    static Entity DefaultType(ICommandContext ctx) => ctx.Model.OfClass(TypeClass).FirstOrDefault() ?? CreateType(ctx, CeilingSpec.Default());

    static void EnsureMaterials(ICommandContext ctx, CeilingSpec spec)
    {
        Materials.Ensure(ctx, spec.TileMaterial, spec.TileColor);
        if (spec.System == "grid") Materials.Ensure(ctx, spec.GridMaterial, spec.GridColor);
    }

    public static List<(string Kind, int I, int J)> Fixtures(Entity c) =>
        (c.Attrs["fixtures"] as JsonArray ?? []).OfType<JsonObject>()
            .Select(f => ((string?)f["kind"] ?? "light", (int)(Json.Number(f["i"]) ?? 0), (int)(Json.Number(f["j"]) ?? 0))).ToList();

    static JsonObject WithFixtures(Entity c, IEnumerable<(string Kind, int I, int J)> list)
    {
        var attrs = c.CloneAttrs();
        attrs["fixtures"] = new JsonArray(list.Select(f => (JsonNode)new JsonObject { ["kind"] = f.Kind, ["i"] = f.I, ["j"] = f.J }).ToArray());
        return attrs;
    }

    /// <summary>
    /// A ceiling's grid: cells of the tile's size in the grid's own frame (turned by the type's rotation
    /// about the origin), from the origin (the instance's, else the outline's middle less half a cell, so
    /// the cut tiles at the edges come out even); which cells lie whole inside the outline.
    /// </summary>
    public sealed class Grid
    {
        public readonly Vec2 Origin;
        public readonly Vec2 U, V;
        public readonly double W, L;
        public readonly IReadOnlyList<Vec2> Outline;
        readonly IReadOnlyList<Vec2> _local;
        public readonly int I0, I1, J0, J1;

        public Grid(IReadOnlyList<Vec2> outline, CeilingSpec spec, Vec2? origin)
        {
            Outline = outline;
            (W, L) = (spec.TileWidth, spec.TileLength);
            var a = spec.Rotation * Math.PI / 180;
            U = new Vec2(Math.Cos(a), Math.Sin(a));
            V = U.Perp;
            var mid = new Vec2(outline.Average(p => p.X), outline.Average(p => p.Y));
            Origin = origin ?? mid - U * (W / 2) - V * (L / 2);
            _local = outline.Select(Local).ToList();
            I0 = (int)Math.Floor(_local.Min(p => p.X) / W); I1 = (int)Math.Ceiling(_local.Max(p => p.X) / W);
            J0 = (int)Math.Floor(_local.Min(p => p.Y) / L); J1 = (int)Math.Ceiling(_local.Max(p => p.Y) / L);
        }

        public Vec2 Local(Vec2 p) { var d = p - Origin; return new Vec2(Vec2.Dot(d, U), Vec2.Dot(d, V)); }
        public Vec2 World(Vec2 q) => Origin + U * q.X + V * q.Y;
        public (int I, int J) CellAt(Vec2 p) { var q = Local(p); return ((int)Math.Floor(q.X / W), (int)Math.Floor(q.Y / L)); }
        public CellRect Cell(int i, int j) => new(i * W, (i + 1) * W, j * L, (j + 1) * L);

        /// <summary>Whether the cell lies wholly inside the outline: its corners inside and no edge of the outline through it.</summary>
        public bool Whole(int i, int j)
        {
            var c = Cell(i, j);
            Vec2[] corners = [new(c.X0, c.Z0), new(c.X1, c.Z0), new(c.X1, c.Z1), new(c.X0, c.Z1)];
            if (!corners.All(p => Polygon2.Contains(_local, p))) return false;
            return !_local.Any(p => p.X > c.X0 + 1e-9 && p.X < c.X1 - 1e-9 && p.Y > c.Z0 + 1e-9 && p.Y < c.Z1 - 1e-9);
        }

        public IEnumerable<(int I, int J)> WholeCells()
        {
            for (var i = I0; i < I1; i++) for (var j = J0; j < J1; j++) if (Whole(i, j)) yield return (i, j);
        }

        /// <summary>The cell's part inside the outline (Sutherland–Hodgman: the outline clipped by the cell), in the world; empty when it misses.</summary>
        public List<Vec2> Piece(int i, int j)
        {
            var c = Cell(i, j);
            var poly = _local.ToList();
            foreach (var (n, d) in new[] { (new Vec2(1, 0), c.X0), (new Vec2(-1, 0), -c.X1), (new Vec2(0, 1), c.Z0), (new Vec2(0, -1), -c.Z1) })
            {
                if (poly.Count == 0) break;
                var o = new List<Vec2>();
                for (var k = 0; k < poly.Count; k++)
                {
                    var (p, q) = (poly[k], poly[(k + 1) % poly.Count]);
                    var (sp, sq) = (Vec2.Dot(n, p) - d, Vec2.Dot(n, q) - d);
                    if (sp >= 0) o.Add(p);
                    if ((sp >= 0) != (sq >= 0)) o.Add(p + (q - p) * (sp / (sp - sq)));
                }
                poly = o;
            }
            return Polygon2.SignedArea(poly) is var area && Math.Abs(area) > 1e-6 ? poly.Select(World).ToList() : [];
        }

        /// <summary>Where a grid line (along U at v, or along V at u) runs inside the outline, as world segments.</summary>
        public IEnumerable<(Vec2 A, Vec2 B)> Runs(bool alongU, double at)
        {
            var cuts = new List<double>();
            for (var k = 0; k < _local.Count; k++)
            {
                var (p, q) = (_local[k], _local[(k + 1) % _local.Count]);
                var (pa, qa, pb, qb) = alongU ? (p.Y, q.Y, p.X, q.X) : (p.X, q.X, p.Y, q.Y);
                if ((pa <= at && qa > at) || (qa <= at && pa > at)) cuts.Add(pb + (qb - pb) * (at - pa) / (qa - pa));
            }
            cuts.Sort();
            for (var k = 0; k + 1 < cuts.Count; k += 2)
                yield return alongU ? (World(new Vec2(cuts[k], at)), World(new Vec2(cuts[k + 1], at))) : (World(new Vec2(at, cuts[k])), World(new Vec2(at, cuts[k + 1])));
        }
    }

    public static Grid GridOf(IModelView m, Entity c) =>
        new(Slabs.Contour(c), SpecOf(m, c), c.Attrs["origin"] is JsonArray o && o.Count == 2 && Json.Number(o[0]) is { } ox && Json.Number(o[1]) is { } oy ? new Vec2(ox, oy) : null);

    public static ElementGeometry? Geometry(Entity c, IModelView m)
    {
        var outline = Slabs.Contour(c);
        if (outline.Count < 3) return new ElementGeometry([], Diagnostics: ["The ceiling has no outline."]);
        var spec = SpecOf(m, c);
        var storey = Spatial.StoreyOf(m, c.Id);
        var z = (storey?.Num("elevation") ?? 0) + c.Num("height", 2.7);
        var top = (storey?.Num("elevation") ?? 0) + (storey?.Num("height", 3) ?? 3);
        var tileMat = Materials.Find(m, spec.TileMaterial)?.Id;
        var gridMat = Materials.Find(m, spec.GridMaterial)?.Id;
        var parts = new List<GeometryPart>();
        var ccw = Polygon2.EnsureCcw(outline);
        var plan = new List<PlanItem> { new("overhead", true, ccw) };
        if (spec.System == "plain")
        {
            if (Prism.ExtrudeFootprint(ccw, z, spec.Thickness, "board") is { } board)
                parts.Add(new GeometryPart("Board", spec.TileColor, board, tileMat, "tiles"));
        }
        var grid = new Grid(ccw, spec, c.Attrs["origin"] is JsonArray o && o.Count == 2 && Json.Number(o[0]) is { } ox && Json.Number(o[1]) is { } oy ? new Vec2(ox, oy) : null);
        var fixtures = Fixtures(c).Where(f => grid.Whole(f.I, f.J)).GroupBy(f => (f.I, f.J)).Select(g => g.Last()).ToList();
        var taken = fixtures.Select(f => (f.I, f.J)).ToHashSet();
        if (spec.System == "grid")
        {
            // The tiles: every cell's part inside the outline but those a fixture takes, lying on the grid's flanges.
            var tiles = new List<Solid>();
            for (var i = grid.I0; i < grid.I1; i++)
                for (var j = grid.J0; j < grid.J1; j++)
                {
                    if (taken.Contains((i, j))) continue;
                    var piece = grid.Piece(i, j);
                    if (piece.Count >= 3 && Prism.ExtrudeFootprint(Polygon2.EnsureCcw(piece), z + 0.003, spec.Thickness, "tile") is { } t) tiles.Add(t);
                }
            if (tiles.Count > 0) parts.Add(new GeometryPart("Tiles", spec.TileColor, Openings.Combine(tiles, "tiles"), tileMat, "tiles"));
            // The grid: tees along both ways at every cell line, and an angle round the edge.
            var bars = new List<Solid>();
            for (var i = grid.I0 + 1; i < grid.I1; i++)
                foreach (var (a, b) in grid.Runs(false, i * grid.W)) if (Prism.SweepBox(a.At(z + spec.GridDepth / 2), b.At(z + spec.GridDepth / 2), spec.GridWidth, spec.GridDepth, "tee") is { } s) bars.Add(s);
            for (var j = grid.J0 + 1; j < grid.J1; j++)
                foreach (var (a, b) in grid.Runs(true, j * grid.L)) if (Prism.SweepBox(a.At(z + spec.GridDepth / 2), b.At(z + spec.GridDepth / 2), spec.GridWidth, spec.GridDepth, "tee") is { } s) bars.Add(s);
            for (var k = 0; k < ccw.Count; k++)
            {
                var (a, b) = (ccw[k], ccw[(k + 1) % ccw.Count]);
                var inward = (b - a).Normalized().Perp * (spec.GridWidth / 2);
                if (Prism.SweepBox((a + inward).At(z + 0.01), (b + inward).At(z + 0.01), spec.GridWidth, 0.02, "angle") is { } s) bars.Add(s);
            }
            if (bars.Count > 0) parts.Add(new GeometryPart("Grid", spec.GridColor, Openings.Combine(bars, "grid"), gridMat, "grid"));
            // The hangers: rods from the main tees up to the storey's top, at their spacing both ways.
            if (spec.Hangers && top > z + spec.GridDepth + 0.05)
            {
                var rods = new List<Solid>();
                var step = Math.Max(1, (int)Math.Round(spec.HangerSpacing / grid.W));
                var stepJ = Math.Max(1, (int)Math.Round(spec.HangerSpacing / grid.L));
                for (var i = grid.I0 + 1; i < grid.I1; i += step)
                    for (var j = grid.J0 + 1; j < grid.J1; j += stepJ)
                    {
                        var p = grid.World(new Vec2(i * grid.W, j * grid.L));
                        if (!Polygon2.Contains(ccw, p)) continue;
                        if (Prism.Box(p.At((z + spec.GridDepth + top) / 2), 0.006, 0.006, top - z - spec.GridDepth, "hanger") is { } rod) rods.Add(rod);
                    }
                if (rods.Count > 0) parts.Add(new GeometryPart("Hangers", "#9a9a9a", Openings.Combine(rods, "hangers"), gridMat, "hangers"));
            }
        }
        // The fixtures: a light panel fills its cell a little under the ceiling; a diffuser is a plate with its square neck.
        var lights = new List<Solid>();
        var diffusers = new List<Solid>();
        foreach (var (kind, i, j) in fixtures)
        {
            var cell = grid.Cell(i, j);
            List<Vec2> Inset(double d) => [grid.World(new(cell.X0 + d, cell.Z0 + d)), grid.World(new(cell.X1 - d, cell.Z0 + d)), grid.World(new(cell.X1 - d, cell.Z1 - d)), grid.World(new(cell.X0 + d, cell.Z1 - d))];
            if (kind == "diffuser")
            {
                if (Prism.ExtrudeFootprint(Inset(0.01), z - 0.012, 0.06, "diffuser") is { } plate) diffusers.Add(plate);
                if (Prism.ExtrudeFootprint(Inset(Math.Min(grid.W, grid.L) * 0.28), z - 0.03, 0.018, "diffuser") is { } core) diffusers.Add(core);
            }
            else if (Prism.ExtrudeFootprint(Inset(0.005), z - 0.006, 0.08, "light") is { } panel) lights.Add(panel);
        }
        if (lights.Count > 0) parts.Add(new GeometryPart("Light panels", spec.LightColor, Openings.Combine(lights, "lights"), null, "lights"));
        if (diffusers.Count > 0) parts.Add(new GeometryPart("Air diffusers", spec.DiffuserColor, Openings.Combine(diffusers, "diffusers"), null, "diffusers"));
        return new ElementGeometry(parts, plan, ccw);
    }

    public static IEnumerable<QuantitySet> Quantities(Entity c, ElementGeometry? g, IModelView m)
    {
        var outline = Slabs.Contour(c);
        if (outline.Count < 3) yield break;
        var grid = GridOf(m, c);
        var fixtures = Fixtures(c).Where(f => grid.Whole(f.I, f.J)).ToList();
        var spec = SpecOf(m, c);
        var area = Math.Abs(Polygon2.SignedArea(outline));
        var perimeter = outline.Select((p, k) => (outline[(k + 1) % outline.Count] - p).Length).Sum();
        yield return new QuantitySet("Qto_CoveringBaseQuantities", [new("Width", spec.Thickness, "LENGTH"), new("GrossArea", area, "AREA"), new("NetArea", area, "AREA")]);
        yield return new QuantitySet("Lapin_CeilingQuantities",
        [
            new("Perimeter", perimeter, "LENGTH"), new("WholeTiles", spec.System == "grid" ? grid.WholeCells().Count() - fixtures.Count : 0, "COUNT", "Whole tiles"),
            new("LightPanels", fixtures.Count(f => f.Kind == "light"), "COUNT", "Light panels"),
            new("AirDiffusers", fixtures.Count(f => f.Kind == "diffuser"), "COUNT", "Air diffusers"),
        ]);
    }
}
