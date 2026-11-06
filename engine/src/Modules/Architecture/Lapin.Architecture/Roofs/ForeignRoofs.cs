using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture.Roofs;

/// <summary>
/// A foreign flat roof rebuilt natively as a deck under its covering (decision 334, docs/spec-ifc.md, Recunoașterea): an
/// IfcRoof aggregating two or more slabs stacked one on the other (each one vertical prism of about the same outline), or
/// one slab-like body with an IfcMaterialLayerSet of two or more layers. The top slab (or layer) is the covering, of its
/// material and its own thickness; everything under it the deck, of the thickest one's material, as thick as they are
/// together. The roof's outline is the deck's, with no overhang, at the deck's underside (the eave offset over its storey's
/// top). A sloped roof, a body of one slab and no layers, or a deck with holes stays as it came (its slabs references or floors).
/// </summary>
public static class ForeignRoofs
{
    const double Tol = 0.005, MinDeck = 0.01, MaxDeck = 1.5, MinCovering = 0.002, MaxCovering = 0.5;

    /// <summary>One layer of the roof, bottom up: its outline in plan, its underside and thickness, its material (a name).</summary>
    sealed record Band(IReadOnlyList<Vec2> Outline, int Holes, double Bottom, double Thickness, string? Material);

    public static string? Recognize(IEdit edit, ForeignElement e, RecognitionContext ctx)
    {
        var storey = edit.Model.Get(e.StoreyId);
        if (storey == null) { ctx.Skip("a roof without a storey"); return null; }
        if (Bands(e) is not { Count: >= 2 } bands) { ctx.Skip("a roof that is not a flat deck under a covering (two stacked slabs, or a layer set)"); return null; }
        var covering = bands[^1];
        var under = bands.Take(bands.Count - 1).ToList();
        var deck = under[0];
        var deckThickness = under.Sum(b => b.Thickness);
        if (bands.Any(b => b.Holes > 0)) { ctx.Skip("a flat roof with holes in its slabs"); return null; }
        if (deckThickness is < MinDeck or > MaxDeck || covering.Thickness is < MinCovering or > MaxCovering)
        {
            ctx.Skip("a roof whose deck or covering is not as thick as one");
            return null;
        }
        var contour = Polygon2.EnsureCcw(deck.Outline.ToList());
        var name = covering.Material?.Trim() is { Length: > 0 } cm ? cm : null;
        var deckName = under.MaxBy(b => b.Thickness)!.Material?.Trim() is { Length: > 0 } dm ? dm : RoofCoverings.DeckDefault;
        var style = RoofCatalog.StyleOf("modern_minimalist") ?? RoofCatalog.Styles[0];
        var attrs = Roof.Attrs(style, contour);
        attrs["shape"] = "flat";
        attrs["overhang"] = 0;
        attrs["gableOverhang"] = 0;
        attrs["covering"] = CoveringFor(name, covering.Thickness).Id;
        attrs[RoofCoverings.Key] = name ?? CoveringFor(name, covering.Thickness).Id;
        attrs["coveringThickness"] = Math.Round(covering.Thickness, 4);
        attrs[RoofCoverings.DeckKey] = deckName;
        attrs["deckThickness"] = Math.Round(deckThickness, 4);
        attrs["eaveOffset"] = Math.Round(deck.Bottom - (storey.Num("elevation") + storey.Num("height", 3)), 6);
        attrs.Remove("style");
        if (under.Count > 1) ctx.Notes.Add("a foreign roof's layers under its covering came in as one deck, of the thickest one's material");
        var id = GlobalId.IsValid(e.GlobalId) && edit.Model.Get(e.GlobalId) == null ? e.GlobalId : edit.NewId();
        var roof = new Entity(id, IfcClass.Roof, e.Name ?? $"Roof {Numbering.Next(edit.Model.OfClass(IfcClass.Roof))}", attrs, e.PredefinedType is { Length: > 0 } pt ? pt : "FLAT_ROOF");
        roof = RoofCoverings.Settle(edit, null, roof);
        edit.Add(roof);
        Spatial.Contain(edit, storey.Id, roof.Id);
        ctx.Made.Add(roof.Id);
        return roof.Id;
    }

    /// <summary>The roof's layers bottom up: its slabs when it aggregates two or more stacked flat ones, else its body's layer set; null when neither.</summary>
    static List<Band>? Bands(ForeignElement e)
    {
        if (e.Parts is { Count: >= 2 } parts)
        {
            var bands = new List<Band>();
            foreach (var p in parts)
            {
                if (ForeignElements.OnePrismOrNull(p.Prisms, x => x.Vertical) is not { } prism || prism.Height < 1e-4) return null;
                bands.Add(new Band(Footprints.Plan(prism.Outer), prism.Holes.Count, Math.Min(prism.BaseZ, prism.BaseZ + prism.Sweep.Z), prism.Height, p.Material));
            }
            bands = [.. bands.OrderBy(b => b.Bottom)];
            return Stacked(bands) ? bands : null;
        }
        if (e.Layers is { Count: >= 2 } layers && ForeignElements.OnePrismOrNull(e.Prisms, x => x.Vertical) is { } body
            && Math.Abs(layers.Sum(l => l.Thickness) - body.Height) < Tol * layers.Count)
        {
            // A slab's layers go up from its underside, the first at the bottom, unless the usage runs the negative way.
            var up = e.Usage is { Negative: true } ? layers.AsEnumerable().Reverse().ToList() : layers.ToList();
            var outline = Footprints.Plan(body.Outer);
            var z = Math.Min(body.BaseZ, body.BaseZ + body.Sweep.Z);
            var bands = new List<Band>();
            foreach (var l in up) { bands.Add(new Band(outline, body.Holes.Count, z, l.Thickness, l.Material)); z += l.Thickness; }
            return bands;
        }
        return null;
    }

    /// <summary>Whether the slabs lie one on the other, each on the top of the one under it, of about the same outline (within 10 % of the deck's area).</summary>
    static bool Stacked(List<Band> bands)
    {
        var area = Math.Abs(Polygon2.SignedArea(bands[0].Outline));
        if (area < 0.01 || bands.Any(b => b.Outline.Count < 3)) return false;
        for (var i = 1; i < bands.Count; i++)
        {
            if (Math.Abs(bands[i].Bottom - (bands[i - 1].Bottom + bands[i - 1].Thickness)) > Tol) return false;
            if (Math.Abs(Math.Abs(Polygon2.SignedArea(bands[i].Outline)) - area) > 0.1 * area) return false;
            if (!bands[i].Outline.All(p => Polygon2.Contains(Grown(bands[0].Outline), p))) return false;
        }
        return true;
    }

    /// <summary>An outline a little larger, so the points of one of the same size read as inside it.</summary>
    static IReadOnlyList<Vec2> Grown(IReadOnlyList<Vec2> outline)
    {
        var c = new Vec2(outline.Average(p => p.X), outline.Average(p => p.Y));
        return outline.Select(p => c + (p - c) * 1.08 + ((p - c).Length > 1e-9 ? (p - c).Normalized() * 0.05 : default)).ToList();
    }

    /// <summary>The catalogue's covering a material's name reads as (tiles, sheet metal, slate, a green roof…), else the one nearest in thickness among the flat roof's (a membrane).</summary>
    static RoofCatalog.Covering CoveringFor(string? name, double thickness)
    {
        var n = (name ?? "").ToLowerInvariant();
        string? id = n switch
        {
            _ when n.Contains("green") || n.Contains("vegeta") || n.Contains("substrat") => "Green roof",
            _ when n.Contains("metal") || n.Contains("steel") || n.Contains("tabl") || n.Contains("zinc") || n.Contains("alumin") => "Metal sheet",
            _ when n.Contains("slate") || n.Contains("ardez") => "Slate",
            _ when n.Contains("shingle") || n.Contains("șindril") || n.Contains("sindril") => "Bitumen shingle",
            _ when n.Contains("concrete tile") => "Concrete tile",
            _ when n.Contains("tile") || n.Contains("țigl") || n.Contains("tigl") => "Clay tile",
            _ when n.Contains("thatch") || n.Contains("stuf") => "Thatch",
            _ => null,
        };
        return id != null ? RoofCatalog.CoveringOf(id) : thickness > 0.1 ? RoofCatalog.CoveringOf("Green roof") : RoofCatalog.CoveringOf("Bitumen membrane");
    }
}
