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
/// price list is an indicative template (<see cref="PanelCost"/>).
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

        // {id (a roof), profile (a profile's id, or a profile of its own; null: none), color, maxLength (a made-to-order profile's longest panel, mm),
        //  support (the profile's support scheme), ridge (its ridge caps, large or small), fixing (the support's: nails or screws)}; only what differs
        //  from the profile's defaults is kept.
        r.Command("setRoofPanels", "Roof panels", (ctx, a) =>
        {
            var roof = ctx.Model.Get((string?)a["id"] ?? "") is { Class: IfcClass.Roof } x ? x : throw new CommandException("Pick a roof.");
            var attrs = roof.CloneAttrs();
            if (a["profile"] is null) attrs.Remove(RoofPanels.Key);
            else
            {
                if (a["profile"] is not JsonValue) throw new CommandException("A roof names a profile of the catalogue; a profile of its own comes with the project's profiles (stage S7).");
                PanelProfile profile;
                try
                {
                    profile = PanelProfiles.Resolve(a["profile"]);
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
                attrs[RoofPanels.Key] = o;
                RoofPanels.EnsureMaterial(ctx, profile, color ?? profile.Colors.FirstOrDefault()?.Code);
            }
            ctx.Replace(roof.With(attrs: attrs));
            return null;
        });

        // {id (a roof), profile?, color?, maxLength?}: the layout in the roof's own profile, or in the one given (a preview); with its
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
                    return Out(RoofPanels.Lay(roof, q.Model, PanelProfiles.Resolve(JsonValue.Create(c.Profile)).WithMaxLength(c.MaxLength), c.Color, c.Options));
                }
                var profile = PanelProfiles.Resolve(a["profile"]).WithMaxLength(Json.Number(a["maxLength"]) is { } ml ? (int)ml : null);
                // A preview keeps the roof's own options where the profile has them, or takes the ones asked.
                var mine = RoofPanels.ChoiceOf(roof);
                var options = new Takeoff.Options((string?)a["support"] ?? mine?.Support, (string?)a["ridge"] ?? mine?.Ridge, (string?)a["fixing"] ?? mine?.Fixing);
                return Out(RoofPanels.Lay(roof, q.Model, profile, (string?)a["color"], options));
            }
            catch (ArgumentException e) { return new JsonObject { ["problem"] = e.Message }; }
        });
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
