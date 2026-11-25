using System.Globalization;
using System.Text.Json.Nodes;
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

    /// <summary>What a roof's <c>panels</c> says: the profile, its colour, a longest panel; null when the roof has none.</summary>
    public sealed record Choice(string Profile, string? Color, int? MaxLength);

    public static Choice? ChoiceOf(Entity roof) =>
        roof.Attrs[Key] is JsonObject o && (string?)o["profile"] is { Length: > 0 } id
            ? new Choice(id, (string?)o["color"], Json.Number(o["maxLength"]) is { } m ? (int)m : null)
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
        Entity Roof, PanelProfile Profile, string? Color, List<RoofFace> Faces, List<RoofEdge> Edges, PanelLayout.Result Result, List<string> Warnings)
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
    public sealed record PlaneSet(IReadOnlyList<IReadOnlyList<Vec3>> Planes, IReadOnlyList<int> PlaneOf, IReadOnlyList<(Vec2 A, Vec2 B)> Junctions, bool Openings, bool Holes);

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
        var holes = false;
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
            holes |= inside.Count > 0;
            foreach (var ring in rings) { planes.Add(ring.Select(p => new Vec3(p.X, p.Y, Z(p))).ToList()); planeOf.Add(i); }
            foreach (var c in mine.Where(c => !c.Under))
                for (var k = 0; k < c.Region.Length; k++) junctions.Add((c.Region[k], c.Region[(k + 1) % c.Region.Length]));
        }
        return (new PlaneSet(planes, planeOf, junctions, RoofOpenings.Of(roof).Any(), holes), null);
    }

    static readonly RevisionCache<Layout?> Cache = new();

    /// <summary>The roof's own layout (its <c>panels</c>), cached per roof and model revision; null when it names no profile or it does not lay.</summary>
    public static Layout? Of(Entity roof, IModelView m) => ChoiceOf(roof) is not { } c ? null : Cache.Get(roof, m, v =>
    {
        try { return Lay(roof, v, PanelProfiles.Resolve(JsonValue.Create(c.Profile)).WithMaxLength(c.MaxLength), c.Color); }
        catch (ArgumentException) { return null; }
    });

    /// <summary>The layout of <paramref name="roof"/> in <paramref name="profile"/>; an <see cref="ArgumentException"/> says why it does not lay.</summary>
    public static Layout Lay(Entity roof, IModelView m, PanelProfile profile, string? color)
    {
        var (set, problem) = Planes(roof, m);
        if (set == null) throw new ArgumentException(problem ?? "The roof cannot be solved.");
        var (faces, edges) = RoofFaces.Read(set.Planes, profile.ValleyGap / 1000.0, set.Junctions, set.PlaneOf);
        if (faces.Count == 0) throw new ArgumentException("The roof has no sloped plane to lay panels on.");
        var warnings = new List<string>();
        if (set.Openings) warnings.Add("Roof windows and dormers are not cut out of the panels yet: the layout covers the whole slopes.");
        if (set.Holes) warnings.Add("Where another roof stands inside a slope, the panels are not cut round it yet.");
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
        return new Layout(roof, profile, color ?? profile.Colors.FirstOrDefault()?.Code, faces, edges, result, warnings);
    }

    static double R(double v, int d = 4) => Math.Round(v, d);
    static JsonArray P3(Vec3 p) => [R(p.X), R(p.Y), R(p.Z)];
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
            })]),
            ["edges"] = new JsonArray([.. l.Edges.Select(e =>
            {
                var lap = p.AccessoryLap(e.Kind);
                var pieces = PieceCount(e.Length, p.AccessoryLength / 1000.0, lap);
                return (JsonNode)new JsonObject
                {
                    ["kind"] = e.Kind, ["faces"] = new JsonArray([.. e.Faces.Select(x => (JsonNode)x)]), ["length"] = R(e.Length, 6),
                    ["lap"] = lap, ["pieces"] = pieces, ["joints"] = Math.Max(0, pieces - 1), ["a"] = P3(e.A), ["b"] = P3(e.B),
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
        return o;
    }
}
