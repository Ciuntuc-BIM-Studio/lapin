using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Architecture.Roofs;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.RoofPanels;

/// <summary>
/// A roof's panels (docs/plan-roof-panels.md): what the roof says it is covered with (<c>panels</c> on the roof: the
/// profile's id, its colour, a made-to-order profile's longest panel) and everything worked out from it on every ask —
/// the faces and edges of the roof's planes, the layout, the offcuts reused, the panels to buy by length, the edges'
/// accessories. Never stored: the roof changes, the panels follow.
/// </summary>
public static class RoofPanels
{
    public const string Key = "panels";

    /// <summary>
    /// What a roof's <c>panels</c> says: the profile, its colour, a longest panel, and the takeoff's options (S3): the support
    /// scheme (Retro Panel's boards or battens), the ridge caps (large or small) and the support's fixings (nails, the default, or
    /// screws); null when the roof has none.
    /// </summary>
    /// <remarks>Stage S5: the rainwater system (<c>drainage</c>: <c>125/90</c> or <c>150/100</c>, none: Auto), the downpipes (<c>downpipes</c>, none: Auto) and the rain (<c>rain</c>, l/s·m²).</remarks>
    public sealed record Choice(string Profile, string? Color, int? MaxLength, string? Support = null, string? Ridge = null, string? Fixing = null,
        string? Drainage = null, int? Downpipes = null, double? Rain = null)
    {
        public Takeoff.Options Options => new(Support, Ridge, Fixing, Drainage, Downpipes, Rain);
    }

    public static Choice? ChoiceOf(Entity roof) =>
        roof.Attrs[Key] is JsonObject o && (string?)o["profile"] is { Length: > 0 } id
            ? new Choice(id, (string?)o["color"], Json.Number(o["maxLength"]) is { } m ? (int)m : null, (string?)o["support"], (string?)o["ridge"], (string?)o["fixing"],
                (string?)o["drainage"], Json.Number(o["downpipes"]) is { } dp ? (int)dp : null, Json.Number(o["rain"]))
            : null;

    /// <summary>
    /// The project material a profile's panels are made of in a colour (decision 855): „Bilka Balcanic, RAL 8017 matt brown”,
    /// a painted sheet in the colour's own RGB (matt, hardly metallic), so the 3D view shows the panels in their colour and
    /// not as the covering's bare metal; sections hatch it, the takeoff lists it. Made by the command that names the profile.
    /// </summary>
    public static string MaterialName(PanelProfile p, string? color) =>
        (p.Colors.FirstOrDefault(c => c.Code == color) ?? p.Colors.FirstOrDefault()) is { } c ? $"{p.Name}, {c.Name}" : $"{p.Name}, painted sheet";

    /// <summary>The panels' material made when the project has none of its name; its id.</summary>
    public static string EnsureMaterial(IEdit edit, PanelProfile p, string? color)
    {
        var name = MaterialName(p, color);
        if (Materials.Find(edit.Model, name) is { } found) return found.Id;
        var rgb = (p.Colors.FirstOrDefault(c => c.Code == color) ?? p.Colors.FirstOrDefault())?.Rgb ?? "#6b6f73";
        var m = new Entity(edit.NewId(), IfcClass.Material, name, new JsonObject
        {
            ["color"] = rgb, ["texture"] = "none", ["category"] = "Metal", ["roughness"] = 0.6, ["metalness"] = 0.1,
            ["description"] = $"{p.Name} roof panels, painted steel sheet. Made by the roof panel layout; the maker's colour, indicative.",
        });
        edit.Add(m);
        return m.Id;
    }

    /// <summary>A roof's panel layout, with its profile, colour and warnings.</summary>
    public sealed record Layout(
        Entity Roof, PanelProfile Profile, string? Color, List<RoofFace> Faces, List<RoofEdge> Edges, PanelLayout.Result Result, List<string> Warnings, Takeoff.Result Takeoff)
    {
        public IEnumerable<PanelLayout.Panel> Bought => Result.Panels.Where(p => !Result.Reuse.ContainsKey(p.Id));

        /// <summary>Panels to buy, by nominal length (mm), the shortest first.</summary>
        public SortedDictionary<int, int> Purchase => Count(Bought);
        /// <summary>Panels laid (offcuts reused counted where they lie), by nominal length.</summary>
        public SortedDictionary<int, int> Installed => Count(Result.Panels);

        static SortedDictionary<int, int> Count(IEnumerable<PanelLayout.Panel> ps)
        {
            var o = new SortedDictionary<int, int>();
            foreach (var p in ps) o[p.Length] = o.GetValueOrDefault(p.Length) + 1;
            return o;
        }

        /// <summary>The roof's sloped area (m², the faces' outlines).</summary>
        public double RoofArea => Faces.Sum(f => Clip.Area(f.Poly));
        /// <summary>What the panels' useful widths cover (m²): the roof less the gaps along valleys.</summary>
        public double Coverage => Result.Panels.Sum(p => Clip.Area(p.Net));
        /// <summary>The sheet bought (m²): each panel bought, its nominal length by its whole width (how panels are priced).</summary>
        public double SheetArea => Bought.Sum(p => p.Length * Profile.GrossWidth / 1e6);

        /// <summary>The rainwater system (S5): sized over the roof's slopes, counted along its gutters, with what to check.</summary>
        public Rainwater.Result? Drainage { get; init; }
        /// <summary>The flashings cut from flat sheet (S5): every combination of ridge cap and rainwater system laid on sheets, the roof's own.</summary>
        public FlatStock.Result? Sheets { get; init; }
    }

    /// <summary>How many accessory pieces of <paramref name="stock"/> metres, lapped <paramref name="lap"/>, run an edge (accessories.piece_count).</summary>
    public static int PieceCount(double length, double stock, double lap)
    {
        if (stock <= 0 || lap < 0 || lap >= stock) throw new ArgumentException("An accessory's useful length is positive.");
        return length > 0 ? Math.Max(1, (int)Math.Ceiling((length - lap) / (stock - lap) - 1e-10)) : 0;
    }

    /// <summary>
    /// The roof's sloped planes as the layout reads them: each plane less what other roofs take away where they meet it
    /// (decision 847), so the panels follow the covering as it is built; a plane cut in two is two. <see cref="PlaneSet.PlaneOf"/>
    /// is each one's plane in the roof's shape; <see cref="PlaneSet.Junctions"/> the lines in plan where a roof at this one's
    /// height takes it away (a valley between the two roofs); <see cref="PlaneSet.Holes"/>, whether another roof stands inside
    /// a plane (not cut from the panels yet).
    /// </summary>
    /// <remarks>
    /// Stage S3: <see cref="PlaneSet.Abutments"/>, the lines in plan where a roof over this one takes it away (an abutment under
    /// it); <see cref="PlaneSet.Walls"/>, the faces of the walls that may rise over its edges; <see cref="PlaneSet.Holes"/>, its
    /// roof windows and dormers and the roofs standing inside a slope, cut out of the panels; <see cref="PlaneSet.Lift"/>, its
    /// covering's top over its planes (the abutments are read there); <see cref="PlaneSet.Data"/>, what the takeoff needs of it.
    /// </remarks>
    public sealed record PlaneSet(IReadOnlyList<IReadOnlyList<Vec3>> Planes, IReadOnlyList<int> PlaneOf, IReadOnlyList<(Vec2 A, Vec2 B)> Junctions,
        IReadOnlyList<(Vec2 A, Vec2 B)> Abutments, IReadOnlyList<WallFace> Walls, IReadOnlyList<RoofHole> Holes, double Lift, Takeoff.RoofData Data);

    /// <summary>The roof's planes for the layout; a problem when it has none to lay panels on.</summary>
    public static (PlaneSet? Set, string? Problem) Planes(Entity roof, IModelView m)
    {
        if (Roof.Resolve(roof, m) is not { } r) return (null, "The roof needs at least 3 corners.");
        if (r.Flat) return (null, "A flat roof takes no panels.");
        if (r.Barrel) return (null, "A barrel roof takes no panels yet: its covering is curved.");
        var (shape, problem) = Roof.Solve(r);
        if (shape == null) return (null, problem ?? "The roof cannot be solved.");
        var cuts = RoofJunctions.Cuts(r, shape, m);
        var planes = new List<IReadOnlyList<Vec3>>();
        var planeOf = new List<int>();
        var junctions = new List<(Vec2, Vec2)>();
        var abutments = new List<(Vec2, Vec2)>();
        var holes = new List<RoofHole>();
        for (var i = 0; i < shape.Planes.Count; i++)
        {
            var pts = shape.Planes[i].Points;
            var mine = cuts.Where(c => c.Plane == i).ToList();
            if (mine.Count == 0) { planes.Add(pts); planeOf.Add(i); continue; }
            var n = VecMath.NewellNormal(pts);
            if (Math.Abs(n.Z) < 1e-9) { planes.Add(pts); planeOf.Add(i); continue; }
            var o = pts[0];
            double Z(Vec2 p) => o.Z - (n.X * (p.X - o.X) + n.Y * (p.Y - o.Y)) / n.Z;
            var (rings, inside) = PlanTrim.Difference(pts.Select(p => p.Xy).ToList(), mine.Select(c => (IReadOnlyList<Vec2>)c.Region).ToList());
            foreach (var ring in rings) { planes.Add(ring.Select(p => new Vec3(p.X, p.Y, Z(p))).ToList()); planeOf.Add(i); }
            foreach (var c in mine)
                for (var k = 0; k < c.Region.Length; k++) (c.Under ? abutments : junctions).Add((c.Region[k], c.Region[(k + 1) % c.Region.Length]));
            // Another roof standing inside the slope: a hole in it, the convex regions it is made of those over it.
            foreach (var hole in inside)
            {
                var c = new Vec2(hole.Average(q => q.X), hole.Average(q => q.Y));
                var regions = mine.Where(x => x.Region.Any(q => Polygon2.Contains(hole, q) || hole.Any(h => (h - q).Length < 1e-6)) || Polygon2.Contains([.. x.Region], c)).ToList();
                if (regions.Count == 0) regions = mine;
                var under = regions.Any(x => x.Under);
                Vec3 Lift(Vec2 q) => new(q.X, q.Y, Z(q));
                holes.Add(new RoofHole($"roof-{holes.Count + 1}", "Roof", under ? RoofHole.Under : RoofHole.Roof, i, [.. hole.Select(Lift)],
                    [.. regions.Select(x => (IReadOnlyList<Vec3>)[.. x.Region.Select(Lift)])]));
            }
        }
        // The roof windows and dormers, each on its plane.
        var placed = RoofOpenings.Place(roof, shape, m, []);
        foreach (var o in placed)
            holes.Add(new RoofHole(o.Opening.Id, o.Opening.Name, o.Opening.Kind == RoofOpening.Dormer ? RoofHole.Dormer : RoofHole.Window, o.Host.PlaneIndex, o.Hole, [o.Hole]));
        // What the takeoff needs of the roof: its snow guards, its rafters (their real length) and their spacing.
        var snow = RoofParts.SnowGuards(r, shape, placed);
        var rafters = RoofParts.Framing(r, shape, placed).Where(x => x.Role == "rafter").ToList();
        var data = new Takeoff.RoofData(snow.Count > 0 ? snow.Sum(x => (x.B - x.A).Length) : null, rafters.Count > 0 ? rafters.Sum(x => x.Length) : null,
            Math.Max(0.3, roof.NumOrNull("rafterSpacing") ?? 0.8), placed.Count(x => x.Opening.Kind == RoofOpening.Window));
        return (new PlaneSet(planes, planeOf, junctions, abutments, WallFaces(roof, m), holes, r.Thickness, data), null);
    }

    /// <summary>
    /// The vertical faces of the model's straight walls (layout.wall_faces, from the walls themselves): each wall's two sides and
    /// its two ends, from its base to its top. A wall attached to this roof stops under it and is left out.
    /// </summary>
    public static List<WallFace> WallFaces(Entity roof, IModelView m)
    {
        var o = new List<WallFace>();
        foreach (var w in m.OfClass(IfcClass.Wall))
        {
            if (w.Attrs["roofIds"] is JsonArray ids && ids.Any(x => (string?)x == roof.Id)) continue;
            if (PathCurve.Of(w) is { IsArc: true }) continue;
            var (a, b) = (Walls.Start(w), Walls.End(w));
            var length = (b - a).Length;
            if (length < 1e-6) continue;
            var bands = Walls.Section(m, w);
            var (y0, y1) = (bands.Min(x => x.From), bands.Max(x => x.To));
            var dir = (b - a) * (1 / length);
            var left = dir.Perp;
            var z0 = Spatial.Base(m, w);
            var z1 = z0 + Walls.HeightOf(m, w);
            Vec3 P(Vec2 q) => new(q.X, q.Y, z0);
            var (t, n) = (new Vec3(dir.X, dir.Y, 0), new Vec3(left.X, left.Y, 0));
            o.Add(new WallFace(P(a + left * y0), t, n, 0, length, z0, z1));
            o.Add(new WallFace(P(a + left * y1), t, n, 0, length, z0, z1));
            o.Add(new WallFace(P(a), n, t, y0, y1, z0, z1));
            o.Add(new WallFace(P(b), n, t, y0, y1, z0, z1));
        }
        return o;
    }

    static readonly RevisionCache<Layout?> Cache = new();

    /// <summary>The roof's own layout (its <c>panels</c>), cached per roof and model revision; null when it names no profile or it does not lay.</summary>
    public static Layout? Of(Entity roof, IModelView m) => ChoiceOf(roof) is not { } c ? null : Cache.Get(roof, m, v =>
    {
        try { return Lay(roof, v, PanelProfiles.Resolve(JsonValue.Create(c.Profile)).WithMaxLength(c.MaxLength), c.Color, c.Options); }
        catch (ArgumentException) { return null; }
    });

    /// <summary>The layout of <paramref name="roof"/> in <paramref name="profile"/>; an <see cref="ArgumentException"/> says why it does not lay.</summary>
    public static Layout Lay(Entity roof, IModelView m, PanelProfile profile, string? color, Takeoff.Options? options = null)
    {
        var (set, problem) = Planes(roof, m);
        if (set == null) throw new ArgumentException(problem ?? "The roof cannot be solved.");
        var (faces, edges) = RoofFaces.Read(set.Planes, profile.ValleyGap / 1000.0, set.Junctions, set.PlaneOf, set.Walls, set.Abutments, set.Lift, set.Holes);
        if (faces.Count == 0) throw new ArgumentException("The roof has no sloped plane to lay panels on.");
        var warnings = new List<string>();
        var diagonal = edges.Where(e => e.Kind is EdgeKinds.Valley or EdgeKinds.Hip).SelectMany(e => e.Faces).ToHashSet();
        foreach (var f in faces)
        {
            var min = diagonal.Contains(f.Id) ? profile.MinPitchHipValley : profile.MinPitch;
            if (f.N.Z > Math.Cos(min * Math.PI / 180) + 1e-6)
                warnings.Add(string.Create(CultureInfo.InvariantCulture, $"Roof plane {f.Id} ({f.PitchDegrees:0.#}°) is under {profile.Name}'s least pitch of {min:0.#}°."));
        }
        if (color != null && profile.Colors.Count > 0 && !profile.Colors.Any(c => c.Code == color))
            warnings.Add($"{profile.Name} is not made in {color}.");
        var result = PanelLayout.Lay(faces, edges, profile);
        var takeoff = Takeoff.Of(profile, faces, edges, result, set.Data, options ?? new Takeoff.Options(null, null, null));
        if (takeoff.RaftersEstimated) warnings.Add("The roof has no rafters: its counter-battens are estimated at 600 mm spacing.");
        // Stage S5: the rainwater system on the roof's own gutters and downpipes, and the pieces cut from flat sheet.
        var o = options ?? new Takeoff.Options(null, null, null);
        var drainage = Rainwater.Of(PanelProfiles.Rain, faces, edges, Rainwater.Read(roof, m), takeoff.Stats, o);
        var sheets = FlatStock.Of(profile, PanelProfiles.Sheet, PanelProfiles.Rain, takeoff.Ridge, drainage.Sizing.Selected, takeoff.Stats, drainage.Counts.Angles);
        return new Layout(roof, profile, color ?? profile.Colors.FirstOrDefault()?.Code, faces, edges, result, warnings, takeoff) { Drainage = drainage, Sheets = sheets };
    }

    static double R(double v, int d = 4) => Math.Round(v, d);
    static JsonArray P3(Vec3 p) => new(R(p.X), R(p.Y), R(p.Z));
    static JsonArray Uv(IEnumerable<Vec2> pts) => new([.. pts.Select(v => (JsonNode)new JsonArray(R(v.X), R(v.Y)))]);

    /// <summary>The layout as the interface and scripts read it (the Panotaj Bilka report's fields, in English).</summary>
    public static JsonObject ToJson(Layout l)
    {
        var p = l.Profile;
        var reuse = l.Result.Reuse;
        var donates = reuse.ToDictionary(kv => kv.Value.Donor, kv => kv.Key);
        var o = new JsonObject
        {
            ["roofId"] = l.Roof.Id,
            ["profile"] = p.ToJson(),
            ["color"] = l.Color,
            ["stock"] = new JsonArray([.. l.Result.Stock.Select(n => (JsonNode)n)]),
            ["faces"] = new JsonArray([.. l.Faces.Select(f => (JsonNode)new JsonObject
            {
                ["id"] = f.Id, ["plane"] = f.Plane, ["pitch"] = R(f.PitchDegrees, 2), ["eave"] = R(f.Eave, 5),
                ["area"] = R(Clip.Area(f.Poly), 5), ["coverage"] = R(Clip.Area(f.Target), 5), ["slopeLength"] = R(Clip.MaxY(f.Poly), 5),
                // Laid flat (u along the eave, v up the slope, m) and in plan: what the layout dialog draws.
                ["outline"] = Uv(f.Poly), ["planOutline"] = new JsonArray([.. f.World.Select(w => (JsonNode)new JsonArray(R(w.X), R(w.Y)))]),
                // The openings it is cut round (S3), laid flat and in plan.
                ["holes"] = new JsonArray([.. f.Holes.Select(h => (JsonNode)new JsonObject
                {
                    ["id"] = h.Of.Id, ["name"] = h.Of.Name, ["kind"] = h.Of.Kind, ["outline"] = Uv(h.Ring),
                    ["plan"] = new JsonArray([.. h.Of.Ring.Select(w => (JsonNode)new JsonArray(R(w.X), R(w.Y)))]),
                })]),
            })]),
            ["edges"] = new JsonArray([.. l.Edges.Select(e =>
            {
                var lap = p.AccessoryLap(e.Kind);
                var pieces = PieceCount(e.Length, p.AccessoryLength / 1000.0, lap);
                return (JsonNode)new JsonObject
                {
                    ["kind"] = e.Kind, ["faces"] = new JsonArray([.. e.Faces.Select(x => (JsonNode)x)]), ["length"] = R(e.Length, 6),
                    ["lap"] = lap, ["pieces"] = pieces, ["joints"] = Math.Max(0, pieces - 1), ["a"] = P3(e.A), ["b"] = P3(e.B),
                    ["opening"] = e.Opening,
                };
            })]),
            ["panels"] = new JsonArray([.. l.Result.Panels.Select(x =>
            {
                var donor = reuse.TryGetValue(x.Id, out var d) ? d : default;
                return (JsonNode)new JsonObject
                {
                    ["id"] = x.Id, ["face"] = x.Face.Id, ["column"] = x.Col, ["row"] = x.Row, ["length"] = x.Length,
                    ["buy"] = donor.Donor == null ? 1 : 0, ["x"] = Math.Round(x.X * 1000), ["y"] = Math.Round(x.Y * 1000),
                    ["trimmedLength"] = Math.Round((Clip.MaxY(x.Poly) - Clip.MinY(x.Poly)) * 1000, 1),
                    ["grossArea"] = R(Clip.Area(x.Poly), 5), ["coverage"] = R(Clip.Area(x.Net), 5),
                    ["edges"] = new JsonArray([.. x.Tags.Select(t => (JsonNode)t)]),
                    ["donor"] = donor.Donor, ["shift"] = donor.Donor == null ? null : donor.Shift,
                    ["donatesTo"] = donates.GetValueOrDefault(x.Id),
                    ["plan"] = new JsonArray([.. x.Net.Select(v => { var w = x.Face.Xyz(v); return (JsonNode)new JsonArray(R(w.X), R(w.Y)); })]),
                    ["net"] = Uv(x.Net),
                };
            })]),
            ["purchase"] = new JsonObject([.. l.Purchase.Select(kv => KeyValuePair.Create(kv.Key.ToString(CultureInfo.InvariantCulture), (JsonNode?)kv.Value))]),
            ["installed"] = new JsonObject([.. l.Installed.Select(kv => KeyValuePair.Create(kv.Key.ToString(CultureInfo.InvariantCulture), (JsonNode?)kv.Value))]),
            ["totals"] = new JsonObject
            {
                ["roofArea"] = R(l.RoofArea), ["coverage"] = R(l.Coverage), ["sheetArea"] = R(l.SheetArea),
                ["panels"] = l.Result.Panels.Count, ["bought"] = l.Bought.Count(), ["reused"] = reuse.Count, ["lengths"] = l.Purchase.Count,
                ["boughtLength"] = R(l.Bought.Sum(x => x.Length) / 1000.0, 3),
            },
            ["warnings"] = new JsonArray([.. l.Warnings.Select(w => (JsonNode)w)]),
        };
        var byKind = new JsonObject();
        foreach (var g in l.Edges.GroupBy(e => e.Kind)) byKind[g.Key] = R(g.Sum(e => e.Length), 4);
        ((JsonObject)o["totals"]!)["edgeLengths"] = byKind;
        o["takeoff"] = TakeoffJson(l);
        if (l.Drainage is { } d) o["drainage"] = Rainwater.ToJson(d, PanelProfiles.Rain);
        if (l.Sheets is { } sh) o["flatSheet"] = FlatStock.ToJson(sh);
        return o;
    }

    /// <summary>The takeoff (S3) as the interface reads it: the options chosen and offered, each slope's share, the stats, the lines.</summary>
    static JsonObject TakeoffJson(Layout l)
    {
        var t = l.Takeoff;
        var p = l.Profile;
        var shared = PanelProfiles.Shared;
        return new JsonObject
        {
            ["support"] = t.Support?.Id, ["ridge"] = t.Ridge, ["fixing"] = t.Fixing,
            ["supports"] = new JsonArray([.. p.Support.Select(s => (JsonNode)new JsonObject { ["id"] = s.Id, ["name"] = s.Name, ["kind"] = s.Kind })]),
            ["ridges"] = new JsonArray([.. p.Ridges.Select(r => (JsonNode)r)]),
            ["fixings"] = new JsonArray([.. shared.WoodFixings.Select(w => (JsonNode)new JsonObject { ["id"] = w.Id, ["name"] = w.Name })]),
            ["battenSection"] = t.Support?.Section, ["counterSection"] = shared.CounterBatten.Section, ["raftersEstimated"] = t.RaftersEstimated,
            ["faces"] = new JsonArray([.. t.Faces.Select(f => (JsonNode)new JsonObject
            {
                ["id"] = f.Id, ["area"] = R(f.Area), ["membraneLaps"] = R(f.MembraneLaps), ["battens"] = R(f.Battens, 3), ["rows"] = f.Rows, ["valleyBattens"] = f.ValleyBattens,
            })]),
            ["stats"] = new JsonObject([.. t.Stats.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)R(kv.Value, 6)))]),
            ["items"] = new JsonArray([.. t.Items.Select(i => (JsonNode)new JsonObject
            {
                ["code"] = i.Code, ["name"] = i.Name, ["group"] = i.Group, ["quantity"] = i.Quantity, ["unit"] = i.Unit,
                ["need"] = i.Need is { } n ? R(n, 2) : null, ["needUnit"] = i.NeedUnit, ["note"] = i.Note,
            })]),
        };
    }
}
