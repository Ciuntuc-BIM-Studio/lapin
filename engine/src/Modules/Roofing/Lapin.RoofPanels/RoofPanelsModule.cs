using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.RoofPanels;

/// <summary>
/// The roof panel layout module (docs/plan-roof-panels.md, decision 847), stage 1: metal tiles and standing-seam panels laid
/// out on the architecture module's roofs. A roof names its profile (<c>roofpanels.setRoofPanels</c>, one undo step); the
/// layout, the edges' kinds and the quantities are a query (<c>roofpanels.layout</c>), worked out from the roof's planes
/// on every ask; the panels are parts a field schedule lists (<c>roofPanels</c>). The built-in profiles are published as
/// the catalogue <c>roofpanels.profiles</c>.
/// </summary>
public sealed class RoofPanelsModule : ILapinModule
{
    public string Id => "roofpanels";
    public string Name => "Roof panel layout";

    public void Register(IModuleRegistry r)
    {
        r.Catalog("profiles", PanelProfiles.Catalog());
        r.Service<IPartSource>(new Parts());

        // {id (a roof), profile (a profile's id, or a profile of its own; null: none), color, maxLength (a made-to-order profile's longest panel, mm)}.
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
                attrs[RoofPanels.Key] = o;
            }
            ctx.Replace(roof.With(attrs: attrs));
            return null;
        });

        // {id (a roof), profile?, color?, maxLength?}: the layout in the roof's own profile, or in the one given (a preview).
        r.Query("layout", (q, a) =>
        {
            var roof = q.Model.Get((string?)a["id"] ?? "") is { Class: IfcClass.Roof } x ? x : throw new CommandException("Pick a roof.");
            try
            {
                if (a["profile"] is null)
                {
                    if (RoofPanels.ChoiceOf(roof) == null) return new JsonObject { ["problem"] = "The roof names no panel profile." };
                    if (RoofPanels.Of(roof, q.Model) is { } own) return RoofPanels.ToJson(own);
                    var c = RoofPanels.ChoiceOf(roof)!;
                    return RoofPanels.ToJson(RoofPanels.Lay(roof, q.Model, PanelProfiles.Resolve(JsonValue.Create(c.Profile)).WithMaxLength(c.MaxLength), c.Color));
                }
                var profile = PanelProfiles.Resolve(a["profile"]).WithMaxLength(Json.Number(a["maxLength"]) is { } ml ? (int)ml : null);
                return RoofPanels.ToJson(RoofPanels.Lay(roof, q.Model, profile, (string?)a["color"]));
            }
            catch (ArgumentException e) { return new JsonObject { ["problem"] = e.Message }; }
        });
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
        ];

        public IEnumerable<ElementPart> PartsOf(IQueryContext q, Entity e)
        {
            if (e.Class != IfcClass.Roof || RoofPanels.Of(e, q.Model) is not { } l) return [];
            return l.Result.Panels.Select(p =>
            {
                var donor = l.Result.Reuse.TryGetValue(p.Id, out var d) ? d.Donor : "";
                return ElementPart.Of(p.Id, ("panelCode", p.Id), ("panelProfile", l.Profile.Name), ("panelColor", l.Color ?? ""),
                    ("panelFace", p.Face.Id), ("panelLength", (double)p.Length), ("panelBought", donor.Length == 0), ("panelDonor", donor),
                    ("panelArea", Math.Round(Clip.Area(p.Poly), 4))) with
                { Points = [.. p.Net.Select(v => p.Face.Xyz(v))] };
            });
        }
    }
}
