using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Lapin.Drawing;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture.Roofs;

/// <summary>
/// The material a roof's covering is made of (decision 328): a project material named on the roof (<c>coveringMaterial</c>,
/// by name, as a door's frame or a ceiling's tiles are), the covering choice's own by default (Clay tile, Metal sheet,
/// Slate…), found by that name or made the first time a roof needs it. The covering part (and a dormer's roof) carries it,
/// so elevations and sections hatch it by its own graphics or its name's (<see cref="MaterialGraphics.ElevationFromName"/>),
/// a section's cut draws its band by its section hatch, IFC associates it with the covering slab, the takeoff and the
/// estimate measure the covering under it, and the 3D view shows its colour. A covering material is made in the covering's
/// colour and without a texture, so a roof looks as it did; a name that reads as a membrane (Metal sheet, Bitumen…) is made
/// not to draw as one, its cut staying a band. <c>""</c> is no material.
/// </summary>
public static class RoofCoverings
{
    public const string Key = "coveringMaterial";
    /// <summary>
    /// A roof without framing (flat, barrel, <c>structure: none</c>) has a deck under its covering (decision 329), a part of
    /// its own made of this material (by name, Reinforced concrete by default), <see cref="Roof.Resolved.DeckThickness"/> thick.
    /// </summary>
    public const string DeckKey = "deckMaterial", DeckDefault = "Reinforced concrete";

    static string? Text(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s.Trim() : null;

    static bool Same(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    static string CoveringId(JsonObject attrs) => RoofCatalog.CoveringOf((string?)attrs["covering"]).Id;

    /// <summary>The name of the material the roof's covering is made of: its own, else its covering's; empty for none.</summary>
    public static string NameOf(Entity roof) => Text(roof.Attrs[Key]) ?? CoveringId(roof.Attrs);

    /// <summary>The covering's material as a part carries it: the project's material of that name, else the name itself (its look guessed from it); null for none.</summary>
    public static string? MaterialOf(Entity roof, IModelView m)
    {
        var name = NameOf(roof);
        return name.Length == 0 ? null : Materials.Find(m, name)?.Id ?? name;
    }

    /// <summary>The covering's colour in 3D: its material's, else the covering choice's.</summary>
    public static string ColorOf(Entity roof, IModelView m, RoofCatalog.Covering covering) =>
        NameOf(roof) is { Length: > 0 } name && Materials.Find(m, name)?.Str("color") is { Length: > 0 } c ? c : covering.Color;

    /// <summary>Whether a roof's attributes give it no framing, so a deck: flat, a vault, or no structure.</summary>
    public static bool Unframed(JsonObject attrs) => (string?)attrs["shape"] is "flat" or "barrel" || (string?)attrs["structure"] == "none";

    /// <summary>The name of the material the deck is made of: the roof's own, else reinforced concrete; empty for none.</summary>
    public static string DeckNameOf(Entity roof) => Text(roof.Attrs[DeckKey]) ?? DeckDefault;

    /// <summary>The deck's material as its part carries it: the project's of that name, else the name; null for none.</summary>
    public static string? DeckMaterialOf(Entity roof, IModelView m)
    {
        var name = DeckNameOf(roof);
        return name.Length == 0 ? null : Materials.Find(m, name)?.Id ?? name;
    }

    /// <summary>The deck's colour in 3D: its material's, else the colour its name suggests.</summary>
    public static string DeckColorOf(Entity roof, IModelView m) =>
        DeckNameOf(roof) is { Length: > 0 } name ? Materials.Find(m, name)?.Str("color") is { Length: > 0 } c ? c : Materials.DefaultColor(name) : "#c9ced6";

    /// <summary>A material made for a covering: its colour, no texture (the roof looks as it did), never a membrane in a cut.</summary>
    public static JsonObject MaterialAttrs(RoofCatalog.Covering covering, string name)
    {
        var o = new JsonObject { ["color"] = covering.Color, ["texture"] = "none" };
        if (MaterialGraphics.FromName(name).Membrane) o["membrane"] = false;
        return o;
    }

    /// <summary>The covering choice a material is named after (Clay tile, Metal sheet), else the roof's own.</summary>
    static RoofCatalog.Covering CoveringFor(string name, JsonObject roofAttrs) =>
        RoofCatalog.Coverings.FirstOrDefault(c => Same(c.Id, name)) ?? RoofCatalog.CoveringOf((string?)roofAttrs["covering"]);

    /// <summary>
    /// After a change to a roof's attributes: the covering's material stated (so a rename of the material follows it). The
    /// covering changed and the material was the old covering's own: the new covering's. One the user chose stays.
    /// </summary>
    public static void Follow(JsonObject before, JsonObject after)
    {
        var (was, now) = (CoveringId(before), CoveringId(after));
        var had = Text(before[Key]);
        var named = Text(after[Key]) ?? had;
        if (!Same(was, now) && (named == null || Same(named, had) && Same(had ?? was, was))) named = now;
        after[Key] = named ?? now;
        // Without framing, its deck's material stated too (so a rename follows it).
        if (Unframed(after) && Text(after[DeckKey]) == null) after[DeckKey] = Text(before[DeckKey]) ?? DeckDefault;
    }

    /// <summary>The roof's covering material made when the project has none of that name (none for an empty name).</summary>
    public static void Ensure(IEdit edit, Entity roof)
    {
        if (Unframed(roof.Attrs) && DeckNameOf(roof) is { Length: > 0 } deck) Materials.Ensure(edit, deck);
        var name = NameOf(roof);
        if (name.Length == 0 || Materials.Find(edit.Model, name) != null) return;
        edit.Add(new Entity(edit.NewId(), IfcClass.Material, name, MaterialAttrs(CoveringFor(name, roof.Attrs), name)));
    }

    /// <summary>The roof as a command leaves it: its covering's material following <paramref name="before"/> (when it was there) and made if missing.</summary>
    public static Entity Settle(IEdit edit, Entity? before, Entity roof)
    {
        var attrs = roof.CloneAttrs();
        Follow(before?.Attrs ?? roof.Attrs, attrs);
        var next = Text(attrs[Key]) == Text(roof.Attrs[Key]) && Text(attrs[DeckKey]) == Text(roof.Attrs[DeckKey]) ? roof : roof.With(attrs: attrs);
        Ensure(edit, next);
        return next;
    }

    /// <summary>A material's id for a project upgrade: the same for the same roof and name every time the file opens (a recovery replays edits on it).</summary>
    public static string UpgradeId(string roofId, string name, string what = "covering") =>
        GlobalId.FromGuid(new Guid(MD5.HashData(Encoding.UTF8.GetBytes($"lapin/roof-{what}/{roofId}/{name.ToLowerInvariant()}"))));
}

/// <summary>
/// Roofs without framing from before decision 329, whose deck and covering were one body of the covering's material: each
/// names its deck's material (reinforced concrete), made when the project has none, so the deck is a part of its own. The
/// thicknesses stay (the deck 16 cm, the covering its own), so the roof's outside is the same.
/// </summary>
public sealed class RoofDeckUpgrade : IProjectUpgrade
{
    public (IReadOnlyList<Entity> Entities, IReadOnlyList<Relation> Relations) Upgrade(IReadOnlyList<Entity> entities, IReadOnlyList<Relation> relations)
    {
        static bool Old(Entity e) => e.Class == IfcClass.Roof && RoofCoverings.Unframed(e.Attrs) && !e.Attrs.ContainsKey(RoofCoverings.DeckKey);
        if (!entities.Any(Old)) return (entities, relations);
        var names = entities.Where(e => e.Class == IfcClass.Material).Select(e => e.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var o = new List<Entity>(entities.Count + 1);
        var made = new List<Entity>();
        foreach (var e in entities)
        {
            if (!Old(e)) { o.Add(e); continue; }
            var attrs = e.CloneAttrs();
            attrs[RoofCoverings.DeckKey] = RoofCoverings.DeckDefault;
            o.Add(e.With(attrs: attrs));
            if (names.Add(RoofCoverings.DeckDefault))
                made.Add(new Entity(RoofCoverings.UpgradeId(e.Id, RoofCoverings.DeckDefault, "deck"), IfcClass.Material, RoofCoverings.DeckDefault,
                    new JsonObject { ["color"] = Materials.DefaultColor(RoofCoverings.DeckDefault) }));
        }
        o.AddRange(made);
        return (o, relations);
    }
}

/// <summary>
/// Roofs from before decision 328: each names its covering's material (its covering's own), and the materials of the
/// catalogue's coverings it names are made, in the covering's colour and without a texture, so the roof looks as it did.
/// </summary>
public sealed class RoofCoveringUpgrade : IProjectUpgrade
{
    public (IReadOnlyList<Entity> Entities, IReadOnlyList<Relation> Relations) Upgrade(IReadOnlyList<Entity> entities, IReadOnlyList<Relation> relations)
    {
        if (!entities.Any(e => e.Class == IfcClass.Roof && !e.Attrs.ContainsKey(RoofCoverings.Key))) return (entities, relations);
        var names = entities.Where(e => e.Class == IfcClass.Material).Select(e => e.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var o = new List<Entity>(entities.Count + 4);
        var made = new List<Entity>();
        foreach (var e in entities)
        {
            if (e.Class != IfcClass.Roof || e.Attrs.ContainsKey(RoofCoverings.Key)) { o.Add(e); continue; }
            var attrs = e.CloneAttrs();
            var covering = RoofCatalog.CoveringOf(e.Str("covering"));
            attrs[RoofCoverings.Key] = covering.Id;
            o.Add(e.With(attrs: attrs));
            if (names.Add(covering.Id))
                made.Add(new Entity(RoofCoverings.UpgradeId(e.Id, covering.Id), IfcClass.Material, covering.Id, RoofCoverings.MaterialAttrs(covering, covering.Id)));
        }
        o.AddRange(made);
        return (o, relations);
    }
}

/// <summary>A roof's covering changed in its properties: its material follows (unless the user chose one), made when the project has none.</summary>
sealed class RoofCoveringUpdate : IElementUpdate
{
    public void Updated(ICommandContext ctx, Entity before, Entity after)
    {
        if (after.Class != IfcClass.Roof) return;
        var next = RoofCoverings.Settle(ctx, before, after);
        if (!ReferenceEquals(next, after)) ctx.Replace(next);
    }
}

/// <summary>
/// A roof's build-up from the top (decision 328): its covering in its material, then the battens and the rafters (or the
/// deck in its material, decision 329), the battens and rafters without one; what a build-up tag lists and what a task's rule by material finds the roof by.
/// </summary>
public sealed class RoofBuildUps : IElementBuildUps
{
    /// <summary>The parts each layer is drawn as: the covering with the dormers' roofs; the framing's members; the deck (or the CLT panels in its place).</summary>
    public static readonly IReadOnlyList<string> CoveringKeys = ["covering", "dormer-roofs"], FramingKeys = ["timber:", "truss:"], DeckKeys = ["deck", "clt:", "clt-spline:"];

    public BuildUp? Of(IModelView m, Entity e)
    {
        if (e.Class != IfcClass.Roof || Roof.Resolve(e, m) is not { } r) return null;
        var name = RoofCoverings.NameOf(e);
        // Each layer with the parts it is drawn as, so a task builds the structure and another the covering (decision 332).
        var covering = new BuildupLayer(0, name.Length == 0 ? null : Materials.Find(m, name)?.Id, r.Covering.Thickness, false, name.Length == 0 ? r.Covering.Id : name,
            Keys: CoveringKeys);
        List<BuildupLayer> layers = r.Framed
            ? [covering, new(1, null, Roof.Battens, false, "Battens and counter-battens", Keys: []),
                new(2, null, r.Rafter.D, true, $"{RoofParts.RoleLabel(r.Scheme == "trusses" ? "topChord" : "rafter")}s {r.Roof.Str("rafterSection") ?? "8x16"}", Keys: FramingKeys)]
            : [covering, new(1, RoofCoverings.DeckNameOf(e) is { Length: > 0 } deck ? Materials.Find(m, deck)?.Id : null, r.StructureDepth, true,
                RoofCoverings.DeckNameOf(e) is { Length: > 0 } d ? d : "Deck", Keys: DeckKeys)];
        return new BuildUp(layers, true);
    }
}
