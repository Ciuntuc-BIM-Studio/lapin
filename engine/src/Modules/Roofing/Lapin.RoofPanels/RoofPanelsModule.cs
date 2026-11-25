using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.RoofPanels;

/// <summary>
/// The roof panel layout module (docs/plan-roof-panels.md, decisions 848 and 855), stages 1–2: metal tiles and standing-seam panels laid
/// out on the architecture module's roofs. A roof names its profile (<c>roofpanels.setRoofPanels</c>, one undo step); the
/// layout, the edges' kinds and the quantities are a query (<c>roofpanels.layout</c>), worked out from the roof's planes
/// on every ask; the panels are parts a field schedule lists (<c>roofPanels</c>). The built-in profiles are published as
/// the catalogue <c>roofpanels.profiles</c>. Stage S4 (decision 859): the lines bought reach the estimate as the roof's
/// <c>Lapin_RoofPanelQuantities</c>, keyed by article codes; the layout is priced from the project's price lists; the Bilka
/// price list is an indicative template (<see cref="PanelCost"/>). Stage S5: the rainwater system sized on the roof's own gutters
/// and downpipes (<see cref="Rainwater"/>) and the flashings cut from flat sheet laid on sheets (<see cref="FlatStock"/>), both
/// priced with the rest.
/// </summary>
public sealed class RoofPanelsModule : ILapinModule
{
    public string Id => "roofpanels";
    public string Name => "Roof panel layout";

    public void Register(IModuleRegistry r)
    {
        r.Catalog("profiles", PanelProfiles.Catalog());
        r.Service<IPartSource>(new Parts());
        r.Service<IPartSource>(new Items());
        // Stage S2: the panels as the covering's geometry, and the Roof panels schedule (a field schedule of the parts).
        r.Service<IGeometryFilter>(new PanelBodies());
        r.Service(ShippedScheduleTemplate.Of("roofPanels", "Roof panels", null, ScheduleFields));
        // Stage S3: what else the panels need (accessories, fixings, battens, counter-battens, membrane), a second schedule.
        r.Service(ShippedScheduleTemplate.Of("roofPanelItems", "Roof panel accessories", null, ItemScheduleFields));
        // Stage S4: the lines bought, by their article codes, as the roof's quantities (the estimate prices them), and the Bilka price
        // list as an indicative template the user adds when asked.
        r.Service<IQuantityFilter>(new PanelCost.Filter());
        r.Service<IPriceListTemplate>(new PanelCost.BilkaTemplate());
        // Stage S7: the project's own profiles (and a firm's .lapinpanels file) beside the built-in ones, which stay read-only.
        ProjectProfiles.Register(r);

        // {id (a roof), profile (a profile's id, or a profile of its own; null: none), color, maxLength (a made-to-order profile's longest panel, mm),
        //  support (the profile's support scheme), ridge (its ridge caps, large or small), fixing (the support's: nails or screws),
        //  drainage (the rainwater system: auto, 125/90 or 150/100), downpipes (a number; absent: auto), rain (l/s·m²)}; only what differs
        //  from the defaults is kept. Given drainage, downpipes or rain, the roof's gutters and downpipes take the system's sizes (S5).
        r.Command("setRoofPanels", "Roof panels", (ctx, a) =>
        {
            var roof = ctx.Model.Get((string?)a["id"] ?? "") is { Class: IfcClass.Roof } x ? x : throw new CommandException("Pick a roof.");
            var attrs = roof.CloneAttrs();
            if (a["profile"] is null) attrs.Remove(RoofPanels.Key);
            else
            {
                if (a["profile"] is not JsonValue) throw new CommandException("A roof names a profile of the catalogue or of the project (Roof panel profiles…): a profile of its own is made there first.");
                PanelProfile profile;
                try
                {
                    profile = PanelProfiles.Resolve(a["profile"], ctx.Model);
                    profile.WithMaxLength(Json.Number(a["maxLength"]) is { } ml ? (int)ml : null);
                }
                catch (ArgumentException e) { throw new CommandException(e.Message); }
                var color = (string?)a["color"];
                if (color != null && profile.Colors.Count > 0 && !profile.Colors.Any(c => c.Code == color))
                    throw new CommandException($"{profile.Name} comes in {string.Join(", ", profile.Colors.Select(c => c.Code))}.");
                var o = new JsonObject { ["profile"] = profile.Id };
                if (color != null) o["color"] = color;
                if (Json.Number(a["maxLength"]) is { } max && (int)max != profile.Lengths[^1]) o["maxLength"] = (int)max;
                if ((string?)a["support"] is { } support)
                {
                    if (!profile.Support.Any(x => x.Id == support)) throw new CommandException($"{profile.Name} is carried on {string.Join(" or ", profile.Support.Select(x => x.Id))}.");
                    if (support != profile.Support[0].Id) o["support"] = support;
                }
                if ((string?)a["ridge"] is { } ridge)
                {
                    if (!profile.Ridges.Contains(ridge)) throw new CommandException(profile.Ridges.Count == 0 ? $"{profile.Name} has one ridge cap." : $"{profile.Name}'s ridge caps are {string.Join(" or ", profile.Ridges)}.");
                    if (ridge != profile.Ridges[0]) o["ridge"] = ridge;
                }
                if ((string?)a["fixing"] is { } fixing)
                {
                    var woods = PanelProfiles.Shared.WoodFixings;
                    if (!woods.Any(x => x.Id == fixing)) throw new CommandException($"The battens are fixed with {string.Join(" or ", woods.Select(x => x.Id))}.");
                    if (fixing != woods[0].Id) o["fixing"] = fixing;
                }
                // Stage S5: the rainwater system (Auto when not given), the downpipes (Auto when not given), the rain.
                var rain = PanelProfiles.Rain;
                var drainage = (string?)a["drainage"];
                if (drainage is { Length: > 0 } && drainage != "auto")
                {
                    if (rain.Find(drainage) == null) throw new CommandException($"The rainwater system is Auto, {string.Join(" or ", rain.Systems.Select(x => x.Id))}.");
                    o["drainage"] = drainage;
                }
                if (a["downpipes"] is JsonValue dv)
                {
                    if (Json.Number(dv) is not { } dn || dn < 1 || dn != Math.Floor(dn) || dn > 999) throw new CommandException("The downpipes are a whole number from 1, or Auto.");
                    o["downpipes"] = (int)dn;
                }
                if (a["rain"] is JsonValue rv)
                {
                    if (Json.Number(rv) is not { } rn || !(rn > 0) || rn > 1) throw new CommandException("The rain's intensity is a positive number of l/s·m² (0.05 by default).");
                    if (Math.Abs(rn - rain.Rain) > 1e-9) o["rain"] = rn;
                }
                // Stage S7: the slopes' exceptions, as given, else as the roof had them (another profile keeps them; a phase is taken modulo its width).
                if (a.ContainsKey("faces")) { if (RoofPanels.FacesJson(CheckFaces(a["faces"])) is { } fj) o["faces"] = fj; }
                else if (roof.Attrs[RoofPanels.Key]?["faces"] is JsonObject had && had.Count > 0) o["faces"] = had.DeepClone();
                attrs[RoofPanels.Key] = o;
                RoofPanels.EnsureMaterial(ctx, profile, color ?? profile.Colors.FirstOrDefault()?.Code);
                // The roof's own gutters and downpipes follow the system the drainage is sized for, when it is asked (one source:
                // the model draws what the estimate counts); the recommendation is the one for the roof as it is now.
                if (a.ContainsKey("drainage") || a.ContainsKey("downpipes") || a.ContainsKey("rain"))
                {
                    var choice = RoofPanels.ChoiceOf(roof.With(attrs: attrs))!;
                    if (Rainwater.SystemFor(roof.With(attrs: attrs), ctx.Model, profile, choice.Options) is { } sys)
                    {
                        attrs["gutterWidth"] = sys.Gutter / 1000.0;
                        attrs["downpipeDiameter"] = sys.Pipe / 1000.0;
                    }
                }
            }
            ctx.Replace(roof.With(attrs: attrs));
            return null;
        });

        // Stage S7: {id (a roof), face (a slope's code, A01…), skip?: true | false, phase?: mm | null}: one slope left without panels, or its
        // columns' phase fixed (null: the layout's own again); what is not given stays. One undo step.
        r.Command("setFacePanels", "Roof plane panels", (ctx, a) =>
        {
            var roof = ctx.Model.Get((string?)a["id"] ?? "") is { Class: IfcClass.Roof } x ? x : throw new CommandException("Pick a roof.");
            if (RoofPanels.ChoiceOf(roof) is not { } choice) throw new CommandException("The roof names no panel profile: pick one first.");
            var face = ((string?)a["face"])?.Trim() ?? "";
            if (!FaceCode.IsMatch(face)) throw new CommandException("Name a roof plane by its code (A01, A02…), as Panel layout shows it.");
            var rules = new Dictionary<string, RoofPanels.FaceRule>(choice.Faces ?? new Dictionary<string, RoofPanels.FaceRule>(), StringComparer.Ordinal);
            var was = rules.GetValueOrDefault(face) ?? new RoofPanels.FaceRule(false, null);
            var skip = a["skip"] is JsonValue sv ? sv.TryGetValue<bool>(out var b) ? b : throw new CommandException("skip is true or false.") : was.Skip;
            var phase = a.ContainsKey("phase") ? CheckPhase(a["phase"]) : was.Phase;
            if (skip || phase != null) rules[face] = new RoofPanels.FaceRule(skip, phase); else rules.Remove(face);
            var attrs = roof.CloneAttrs();
            var panels = (JsonObject)attrs[RoofPanels.Key]!;
            if (RoofPanels.FacesJson(rules) is { } fj) panels["faces"] = fj; else panels.Remove("faces");
            ctx.Replace(roof.With(attrs: attrs));
            return null;
        });

        // {id (a roof), profile?, color?, maxLength?, support?, ridge?, fixing?, drainage?, downpipes?, rain?}: the layout in the roof's own profile, or in the one given (a preview); with its
        // cost (S4): the lines priced from the project's price lists.
        r.Query("layout", (q, a) =>
        {
            JsonObject Out(RoofPanels.Layout l)
            {
                var o = RoofPanels.ToJson(l);
                o["cost"] = PanelCost.Json(PanelCost.Estimate(l, q.Services.Get<IArticlePrices>(), q.Model));
                return o;
            }
            var roof = q.Model.Get((string?)a["id"] ?? "") is { Class: IfcClass.Roof } x ? x : throw new CommandException("Pick a roof.");
            try
            {
                if (a["profile"] is null)
                {
                    if (RoofPanels.ChoiceOf(roof) == null) return new JsonObject { ["problem"] = "The roof names no panel profile." };
                    if (RoofPanels.Of(roof, q.Model) is { } own) return Out(own);
                    var c = RoofPanels.ChoiceOf(roof)!;
                    return Out(RoofPanels.Lay(roof, q.Model, PanelProfiles.Resolve(JsonValue.Create(c.Profile), q.Model).WithMaxLength(c.MaxLength), c.Color, c.Options, c.Faces));
                }
                var profile = PanelProfiles.Resolve(a["profile"], q.Model).WithMaxLength(Json.Number(a["maxLength"]) is { } ml ? (int)ml : null);
                // A preview keeps the roof's own options where the profile has them, or takes the ones asked.
                var mine = RoofPanels.ChoiceOf(roof);
                var options = new Takeoff.Options((string?)a["support"] ?? mine?.Support, (string?)a["ridge"] ?? mine?.Ridge, (string?)a["fixing"] ?? mine?.Fixing,
                    a.ContainsKey("drainage") ? (string?)a["drainage"] : mine?.Drainage, a.ContainsKey("downpipes") ? (Json.Number(a["downpipes"]) is { } dp ? (int)dp : null) : mine?.Downpipes,
                    a.ContainsKey("rain") ? Json.Number(a["rain"]) : mine?.Rain);
                // The roof's own exceptions on its slopes (S7), or those asked.
                var rules = a.ContainsKey("faces") ? CheckFaces(a["faces"]) : mine?.Faces;
                return Out(RoofPanels.Lay(roof, q.Model, profile, (string?)a["color"], options, rules));
            }
            catch (ArgumentException e) { return new JsonObject { ["problem"] = e.Message }; }
        });
    }

    static readonly System.Text.RegularExpressions.Regex FaceCode = new("^A[0-9]{2,3}$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    /// <summary>A phase given (mm, 0 to 10 000; null: none), whole millimetres.</summary>
    static int? CheckPhase(JsonNode? n)
    {
        if (n is null) return null;
        if (Json.Number(n) is not { } v || !double.IsFinite(v) || v < 0 || v > 10000) throw new CommandException("A phase is a distance along the eave in mm, from 0 to the profile's useful width.");
        return (int)Math.Round(v);
    }

    /// <summary>The slopes' exceptions as a command gives them (<c>{"A02": {"skip": true}, "A03": {"phase": 120}}</c>), checked.</summary>
    static Dictionary<string, RoofPanels.FaceRule> CheckFaces(JsonNode? n)
    {
        if (n is null) return [];
        if (n is not JsonObject o) throw new CommandException("The slopes' exceptions are {\"A01\": {\"skip\": true}, \"A02\": {\"phase\": 120}}.");
        foreach (var (k, v) in o)
        {
            if (!FaceCode.IsMatch(k)) throw new CommandException($"„{k}” is not a roof plane's code (A01, A02…).");
            if (v is not JsonObject r) throw new CommandException($"{k}: an exception is {{skip, phase}}.");
            CheckPhase(r["phase"]);
        }
        return RoofPanels.FacesOf(o);
    }

    /// <summary>
    /// The Roof panels schedule as shipped (plan §4): a row per roof, profile, colour, length and whether it is bought or an
    /// offcut reused, with the pieces, their length and the sheet bought (each panel's nominal length by its whole width).
    /// </summary>
    public const string ScheduleFields = """
        {
          "source": "roofPanels",
          "filter": { "classes": ["IfcRoof"] },
          "groupBy": ["name", "panelProfile", "panelColor", "panelLength", "panelBought"], "groupColumns": false, "count": false,
          "sort": ["name", { "field": "panelBought", "desc": true }, { "field": "panelLength", "desc": true }],
          "columns": [
            { "field": "name", "heading": "Roof", "aggregate": "first" },
            { "field": "panelProfile", "heading": "Profile", "aggregate": "first" },
            { "field": "panelColor", "heading": "Colour", "aggregate": "first" },
            { "field": "panelLength", "heading": "Length (mm)", "aggregate": "first", "decimals": 0 },
            { "field": "panelBought", "heading": "Bought", "aggregate": "first" },
            { "field": "panelLength", "heading": "Pieces", "aggregate": "rows" },
            { "field": "panelRun", "heading": "Total length (m)", "aggregate": "sum", "decimals": 2 },
            { "field": "panelSheet", "heading": "Sheet bought (m²)", "aggregate": "sum", "decimals": 2 }
          ]
        }
        """;

    /// <summary>
    /// The Roof panel accessories schedule as shipped (S3): a row per roof and line of its takeoff, the flashings, the fixings
    /// and the support (membrane, battens or boards, counter-battens), each in its unit and what it comes from.
    /// </summary>
    public const string ItemScheduleFields = """
        {
          "source": "roofPanelItems",
          "filter": { "classes": ["IfcRoof"] },
          "groupBy": ["name", "itemGroup", "itemName", "itemUnit"], "groupColumns": false, "count": false,
          "sort": ["name", "itemOrder"],
          "columns": [
            { "field": "name", "heading": "Roof", "aggregate": "first" },
            { "field": "itemGroup", "heading": "Group", "aggregate": "first" },
            { "field": "itemName", "heading": "Item", "aggregate": "first" },
            { "field": "itemQuantity", "heading": "Quantity", "aggregate": "sum", "decimals": 0 },
            { "field": "itemUnit", "heading": "Unit", "aggregate": "first" },
            { "field": "itemNeed", "heading": "Needed", "aggregate": "sum", "decimals": 2 },
            { "field": "itemNeedUnit", "heading": "Needed in", "aggregate": "first" }
          ]
        }
        """;

    /// <summary>The takeoff of a roof's panels (<c>roofPanelItems</c>, S3), a part a line: its code, name, group, quantity and unit, what it comes from.</summary>
    sealed class Items : IPartSource
    {
        public string Source => "roofPanelItems";
        public string Label => "Roof panel accessories";
        public IReadOnlyList<(string Key, string Label)> Fields { get; } =
        [
            ("itemCode", "Item code"), ("itemName", "Item"), ("itemGroup", "Group"), ("itemQuantity", "Quantity"), ("itemUnit", "Unit"),
            ("itemNeed", "Needed"), ("itemNeedUnit", "Needed in"), ("itemOrder", "Order"), ("panelProfile", "Profile"),
        ];

        static readonly Dictionary<string, string> Groups = new() { ["flashings"] = "Flashings", ["fixings"] = "Fixings", ["support"] = "Support" };

        public IEnumerable<ElementPart> PartsOf(IQueryContext q, Entity e)
        {
            if (e.Class != IfcClass.Roof || RoofPanels.Of(e, q.Model) is not { } l) return [];
            return l.Takeoff.Items.Select((i, k) => ElementPart.Of($"item:{i.Code}", ("itemCode", i.Code), ("itemName", i.Name),
                ("itemGroup", Groups.GetValueOrDefault(i.Group, i.Group)), ("itemQuantity", i.Quantity), ("itemUnit", i.Unit),
                ("itemNeed", i.Need ?? 0.0), ("itemNeedUnit", i.NeedUnit ?? ""), ("itemOrder", (double)k), ("panelProfile", l.Profile.Name)));
        }
    }

    /// <summary>The panels of a roof (<c>roofPanels</c>), each a part: its code, plane, length, whether it is bought or an offcut reused.</summary>
    sealed class Parts : IPartSource
    {
        public string Source => "roofPanels";
        public string Label => "Roof panels";
        public IReadOnlyList<(string Key, string Label)> Fields { get; } =
        [
            ("panelCode", "Panel code"), ("panelProfile", "Profile"), ("panelColor", "Colour"), ("panelFace", "Roof plane"),
            ("panelLength", "Length (mm)"), ("panelBought", "Bought"), ("panelDonor", "Cut from"), ("panelArea", "Area cut (m²)"),
            ("panelRun", "Length (m)"), ("panelSheet", "Sheet bought (m²)"),
        ];

        public IEnumerable<ElementPart> PartsOf(IQueryContext q, Entity e)
        {
            if (e.Class != IfcClass.Roof || RoofPanels.Of(e, q.Model) is not { } l) return [];
            return l.Result.Panels.Select(p =>
            {
                var donor = l.Result.Reuse.TryGetValue(p.Id, out var d) ? d.Donor : "";
                return ElementPart.Of(p.Id, ("panelCode", p.Id), ("panelProfile", l.Profile.Name), ("panelColor", l.Color ?? ""),
                    ("panelFace", p.Face.Id), ("panelLength", (double)p.Length), ("panelBought", donor.Length == 0), ("panelDonor", donor),
                    ("panelArea", Math.Round(Clip.Area(p.Poly), 4)), ("panelRun", p.Length / 1000.0),
                    ("panelSheet", donor.Length == 0 ? p.Length * l.Profile.GrossWidth / 1e6 : 0.0)) with
                { Points = [.. p.Net.Select(v => p.Face.Xyz(v))] };
            });
        }
    }
}
