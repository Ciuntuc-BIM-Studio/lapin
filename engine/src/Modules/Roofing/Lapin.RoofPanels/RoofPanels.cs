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

    /// <summary>The roof's sloped planes, as the layout reads them; a problem when it has none to lay panels on.</summary>
    public static (IReadOnlyList<IReadOnlyList<Vec3>> Planes, string? Problem, bool Openings) Planes(Entity roof, IModelView m)
    {
        if (Roof.Resolve(roof, m) is not { } r) return ([], "The roof needs at least 3 corners.", false);
        if (r.Flat) return ([], "A flat roof takes no panels.", false);
        if (r.Barrel) return ([], "A barrel roof takes no panels yet: its covering is curved.", false);
        var (shape, problem) = Roof.Solve(r);
        if (shape == null) return ([], problem ?? "The roof cannot be solved.", false);
        return (shape.Planes.Select(p => p.Points).ToList(), null, RoofOpenings.Of(roof).Any());
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
        var (planes, problem, openings) = Planes(roof, m);
        if (problem != null) throw new ArgumentException(problem);
        var (faces, edges) = RoofFaces.Read(planes, profile.ValleyGap / 1000.0);
        if (faces.Count == 0) throw new ArgumentException("The roof has no sloped plane to lay panels on.");
        var warnings = new List<string>();
        if (openings) warnings.Add("Roof windows and dormers are not cut out of the panels yet: the layout covers the whole slopes.");
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
