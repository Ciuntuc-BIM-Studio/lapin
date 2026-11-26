using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Archicad;

/// <summary>A skin of a composite (or a basic element's one): its thickness, its building material's name, whether it is a core skin.</summary>
public sealed record ArchicadSkin(double Thickness, string Material, bool Core);

/// <summary>
/// Where a wall's skins lie across its reference line and where Lapin's axis goes (docs/plan-archicad.md §3, Lapin's layer rules):
/// <see cref="Bands"/> each skin's extent across the reference line (left of its direction positive), from Archicad's outside face
/// in; <see cref="Axis"/> the core's middle, across (Lapin's axis runs through the middle of the core); <see cref="Layers"/> the
/// skins in Archicad's order, outside first (so one composite is one type whichever way its walls are flipped), the core marked;
/// <see cref="Offset"/> the outside face's offset from Lapin's axis and <see cref="Negative"/> whether the layers run from it to the
/// right (an unflipped wall, its outside face on its left).
/// </summary>
public sealed record ArchicadWallSection(IReadOnlyList<(double From, double To)> Bands, double Axis, IReadOnlyList<LayerSpec> Layers, double Offset, bool Negative, double Thickness)
{
    public double Right => Bands.Min(b => Math.Min(b.From, b.To));
    public double Left => Bands.Max(b => Math.Max(b.From, b.To));
}

/// <summary>
/// Archicad's elements as the foreign elements Lapin's recognisers take (<see cref="ForeignElement"/>, the same the IFC import offers
/// them), built from their own parameters (Tapir's details), never from their triangles: a wall from its reference line, skins,
/// height and openings; a slab from its outline, holes and thickness; a column and a beam from their sections; a zone from its
/// outline and height. What a recogniser does not take, or the fidelity gate finds other than Archicad's own body, stays a reference.
/// </summary>
public static class ArchicadForeign
{
    const double HalfPi = Math.PI / 2;

    static Vec2 P(JsonNode? n) => new(Json.Number(n?["x"]) ?? 0, Json.Number(n?["y"]) ?? 0);

    static double N(JsonObject d, string key, double fallback = 0) => Json.Number(d[key]) ?? fallback;

    static bool B(JsonObject d, string key) => d[key] is JsonValue v && v.TryGetValue<bool>(out var b) && b;

    static string? S(JsonObject d, string key) => (string?)d[key];

    /// <summary>A polygon as Archicad lists it (its first point again at its end), without the repeat.</summary>
    public static List<Vec2> Polygon(JsonNode? list)
    {
        var o = (list as JsonArray ?? []).Select(P).ToList();
        if (o.Count > 1 && (o[0] - o[^1]).Length < 1e-9) o.RemoveAt(o.Count - 1);
        return o;
    }

    /// <summary>An element's skins: its composite's, outside (or top) first; a basic element's one, of its building material.</summary>
    public static List<ArchicadSkin>? Skins(ArchicadModel m, JsonObject d, double basicThickness)
    {
        var structure = S(d, "structureType");
        if (structure == "Composite")
        {
            if ((string?)d["compositeId"]?["guid"] is not { } cg || !m.Composites.TryGetValue(cg, out var comp)) return null;
            var skins = (comp["skins"] as JsonArray ?? []).OfType<JsonObject>().Select(s => new ArchicadSkin(N(s, "thickness"),
                (string?)s["buildingMaterialId"]?["attributeId"]?["guid"] is { } bm && m.BuildingMaterials.TryGetValue(bm, out var name) ? name : "Building material",
                S(s, "type") == "Core")).Where(s => s.Thickness > 0).ToList();
            return skins.Count > 0 ? skins : null;
        }
        if (structure == "Basic")
            return [new(basicThickness, (string?)d["buildingMaterialId"]?["guid"] is { } g && m.BuildingMaterials.TryGetValue(g, out var mat) ? mat : "Building material", true)];
        return null;
    }

    /// <summary>The composite's (or basic material's) name, for the type Lapin makes of it.</summary>
    public static string? CompositeName(ArchicadModel m, JsonObject d) =>
        (string?)d["compositeId"]?["guid"] is { } cg && m.Composites.TryGetValue(cg, out var comp) ? (string?)comp["name"] : null;

    /// <summary>
    /// The distance of a wall's reference line from its outside face, from its location (<c>referenceLineLocation</c>) and offset,
    /// when Archicad does not give it (<c>offsetFromOutside</c>): the outside face, the middle, the inside face, or the core's
    /// outside face, middle or inside face, the offset added inwards.
    /// </summary>
    public static double FromOutside(string? location, double offset, IReadOnlyList<ArchicadSkin> given)
    {
        var skins = given.ToList();
        var total = skins.Sum(s => s.Thickness);
        var first = skins.FindIndex(s => s.Core);
        var last = skins.FindLastIndex(s => s.Core);
        double coreFrom = first < 0 ? 0 : skins.Take(first).Sum(s => s.Thickness), coreTo = last < 0 ? total : skins.Take(last + 1).Sum(s => s.Thickness);
        var at = location switch
        {
            "Outside" => 0,
            "Center" => total / 2,
            "Inside" => total,
            "CoreOutside" => coreFrom,
            "CoreCenter" => (coreFrom + coreTo) / 2,
            "CoreInside" => coreTo,
            _ => 0,
        };
        return at + offset;
    }

    static List<ArchicadSkin> Skins(IEnumerable<ArchicadSkin> skins) => [.. skins];

    /// <summary>
    /// A straight wall's section: its skins across its reference line, from Archicad's outside face — on the left of its direction
    /// unflipped, on the right flipped — at <paramref name="fromOutside"/> from the line, inwards; Lapin's axis through the middle
    /// of its core (the thickest core skin, else the thickest skin), its layers in Archicad's order from the outside face, the one core marked.
    /// </summary>
    public static ArchicadWallSection Section(IReadOnlyList<ArchicadSkin> skins, double fromOutside, bool flipped)
    {
        // Across (left positive): the outside face, and the way into the wall.
        var into = flipped ? 1.0 : -1.0;
        var outside = -into * fromOutside;
        var bands = new List<(double, double)>();
        var at = outside;
        foreach (var s in skins)
        {
            var next = at + into * s.Thickness;
            bands.Add((Math.Min(at, next), Math.Max(at, next)));
            at = next;
        }
        var core = skins.Select((s, i) => (s, i)).Where(x => x.s.Core).OrderByDescending(x => x.s.Thickness).Select(x => x.i).DefaultIfEmpty(-1).First();
        if (core < 0) core = skins.Select((s, i) => (s, i)).OrderByDescending(x => x.s.Thickness).First().i;
        var axis = (bands[core].Item1 + bands[core].Item2) / 2;
        var layers = skins.Select((s, i) => new LayerSpec(s.Material, Math.Round(s.Thickness, 4), i == core)).ToList();
        return new(bands, axis, layers, Math.Round(outside - axis, 6), !flipped, skins.Sum(s => s.Thickness));
    }

    /// <summary>A rectangle in plan along a line: from <paramref name="a"/> to <paramref name="b"/>, from <paramref name="right"/> to <paramref name="left"/> across it.</summary>
    static List<Vec2> Strip(Vec2 a, Vec2 b, double right, double left)
    {
        var d = (b - a).Normalized();
        var n = new Vec2(-d.Y, d.X);
        return [a + n * right, b + n * right, b + n * left, a + n * left];
    }

    static List<Vec3> At(IEnumerable<Vec2> plan, double z) => [.. plan.Select(p => new Vec3(p.X, p.Y, z))];

    /// <summary>A wall offered natively, or why it is not: a straight, plumb wall of one thickness, basic or composite.</summary>
    public static (ForeignElement? Element, string? Why) Wall(ArchicadModel m, ArchicadElement w, string id, string storeyId, double storeyLevel, IReadOnlyList<(ArchicadElement El, string Id)> fills, Vec3 shift)
    {
        var d = w.Details;
        if (S(d, "geometryType") is { } g && g != "Straight") return (null, $"a {(g == "Polygonal" ? "polygonal" : "curved")} wall (in a later stage)");
        if (Math.Abs(N(d, "arcAngle")) > 1e-9) return (null, "a curved wall (in a later stage)");
        if (S(d, "profileType") is { } pt && pt != "Normal") return (null, "a slanted or profiled wall (in a later stage)");
        if (Math.Abs(N(d, "slantAlpha", HalfPi) - HalfPi) > 1e-6 || Math.Abs(N(d, "slantBeta", HalfPi) - HalfPi) > 1e-6) return (null, "a slanted wall (in a later stage)");
        if (Math.Abs(N(d, "begThickness") - N(d, "endThickness")) > 1e-4) return (null, "a wall whose thickness changes along it (in a later stage)");
        if (S(d, "structureType") is not ("Basic" or "Composite")) return (null, "a wall of a complex profile (in a later stage)");
        var skins = Skins(m, d, N(d, "begThickness"));
        if (skins == null) return (null, "a wall whose composite Archicad did not give");
        var beg = P(d["begCoordinate"]) + shift.Xy;
        var end = P(d["endCoordinate"]) + shift.Xy;
        if ((end - beg).Length < 1e-3) return (null, "a wall without a length");
        var fromOutside = d["offsetFromOutside"] is JsonValue ? N(d, "offsetFromOutside") : FromOutside(S(d, "referenceLineLocation"), N(d, "offset"), skins);
        var section = Section(skins, fromOutside, B(d, "flipped"));
        if (Math.Abs(section.Thickness - N(d, "begThickness", section.Thickness)) > 1e-3) return (null, "a wall whose skins do not add up to its thickness");
        var dir = (end - beg).Normalized();
        var n = new Vec2(-dir.Y, dir.X);
        var a = beg + n * section.Axis;
        var b = end + n * section.Axis;
        var baseZ = storeyLevel + N(d, "bottomOffset") + shift.Z;
        var height = N(d, "height");
        if (!(height > 0)) return (null, "a wall without a height");
        // Its footprint as Archicad cuts it at its junctions (each skin's polygon, joined); its strip where they do not join into one.
        var skinPolys = (d["floorPlanPolygons"] as JsonArray ?? []).OfType<JsonObject>().Select(p => Polygon(p["coordinates"]).Select(q => q + shift.Xy).ToList()).Where(p => p.Count >= 3).ToList();
        List<Vec2> footprint = skinPolys.Count == 0 ? Strip(beg, end, section.Right, section.Left)
            : skinPolys.Count == 1 ? skinPolys[0]
            : OutlineUnion.Of([.. skinPolys.Select(p => new OutlineUnion.Loop(p, null))])?.Single?.Corners ?? Strip(beg, end, section.Right, section.Left);
        var prism = new ShapePrism(At(footprint, baseZ), [], new Vec3(0, 0, height), false, "archicad");
        var openings = new List<ForeignOpening>();
        foreach (var (f, fid) in fills)
        {
            var fd = f.Details;
            var width = N(fd, "width"); var h = N(fd, "height");
            if (!(width > 0 && h > 0)) continue;
            var along = N(fd, "centerOffset");
            var link = S(fd, "verticalLink") ?? "SillToWallBottom";
            var anchor = link.EndsWith("ToStory", StringComparison.Ordinal) && m.Story((int)N(fd, "verticalLinkStoryIndex", w.Floor)) is { } ls ? ls.Level + shift.Z
                : link.EndsWith("ToWallTop", StringComparison.Ordinal) ? baseZ + height : baseZ;
            var sill = link.StartsWith("Header", StringComparison.Ordinal) ? anchor + N(fd, "sillHeight") - h : anchor + N(fd, "sillHeight");
            var c = beg + dir * along;
            var outline = Strip(c - dir * (width / 2), c + dir * (width / 2), section.Right - 0.01, section.Left + 0.01);
            var cls = f.Type == "Door" ? IfcClass.Door : IfcClass.Window;
            var fill = new ForeignFill(fid, cls, f.IdText is { Length: > 0 } nm ? nm : null, width, h, null, (string?)fd["libPart"]?["name"], null, new JsonObject());
            openings.Add(new ForeignOpening(Derived(fid, "opening"), null, [new ShapePrism(At(outline, sill), [], new Vec3(0, 0, h), false, "archicad")], fill));
        }
        var e = new ForeignElement(id, IfcClass.Wall, "STANDARD", w.IdText is { Length: > 0 } t ? t : null, null, storeyId, [prism], null, openings,
            CompositeName(m, d) ?? (section.Layers.Count == 1 ? section.Layers[0].Material : null), null,
            [.. section.Layers.Select(l => (l.Material, l.Thickness, l.Core))], skins.Count == 1 ? skins[0].Material : null, new JsonObject(),
            Axis: [new Vec3(a.X, a.Y, baseZ), new Vec3(b.X, b.Y, baseZ)], Usage: (section.Negative, section.Offset), Footprint: [footprint]);
        return (e, null);
    }

    /// <summary>A slab offered natively, or why not: a flat outline (no arcs) with its holes, of one thickness, basic or composite.</summary>
    public static (ForeignElement? Element, string? Why) Slab(ArchicadModel m, ArchicadElement s, string id, string storeyId, double storeyLevel, Vec3 shift)
    {
        var d = s.Details;
        if (d["polygonArcs"] is JsonArray { Count: > 0 } || (d["holes"] as JsonArray ?? []).OfType<JsonObject>().Any(h => h["polygonArcs"] is JsonArray { Count: > 0 }))
            return (null, "a slab with curved edges (in a later stage)");
        if (S(d, "structureType") is not ("Basic" or "Composite")) return (null, "a slab of a complex profile (in a later stage)");
        var thickness = N(d, "thickness");
        var skins = Skins(m, d, thickness);
        if (skins == null) return (null, "a slab whose composite Archicad did not give");
        if (Math.Abs(skins.Sum(x => x.Thickness) - thickness) > 1e-3) return (null, "a slab whose skins do not add up to its thickness");
        var outline = Polygon(d["polygonOutline"]).Select(p => p + shift.Xy).ToList();
        if (outline.Count < 3) return (null, "a slab without an outline");
        var top = storeyLevel + N(d, "level") + N(d, "offsetFromTop") + shift.Z;
        var bottom = top - thickness;
        var holes = (d["holes"] as JsonArray ?? []).OfType<JsonObject>().Select(h => Polygon(h["polygonOutline"] ?? h["polygonCoordinates"]).Select(p => p + shift.Xy).ToList())
            .Where(h => h.Count >= 3).Select(h => (IReadOnlyList<Vec3>)At(h, bottom)).ToList();
        var prism = new ShapePrism(At(outline, bottom), holes, new Vec3(0, 0, thickness), false, "archicad");
        var e = new ForeignElement(id, IfcClass.Slab, "FLOOR", s.IdText is { Length: > 0 } t ? t : null, null, storeyId, [prism], null, [],
            CompositeName(m, d), null, [.. skins.Select(x => (x.Material, x.Thickness, x.Core))], skins[0].Material, new JsonObject());
        return (e, null);
    }

    /// <summary>The offset of an anchor on Archicad's 3×3 grid from a section's centre: (across or x, up or y) in halves of its sides.</summary>
    static (double X, double Y) Anchor(string? anchor) => anchor switch
    {
        "TopLeft" => (-1, 1), "TopCenter" => (0, 1), "TopRight" => (1, 1),
        "MiddleLeft" => (-1, 0), "MiddleRight" => (1, 0),
        "BottomLeft" => (-1, -1), "BottomCenter" => (0, -1), "BottomRight" => (1, -1),
        _ => (0, 0),
    };

    /// <summary>Whether a column's or beam's one segment is a basic one (a profile's or a complex one is read from its body instead).</summary>
    public static bool Basic(JsonObject d) =>
        d["segments"] is not JsonArray segs || segs.Count <= 1 && segs.OfType<JsonObject>().All(s => (S(s, "structureType") ?? S(s["geometry"] as JsonObject ?? s, "structureType")) is null or "Basic");

    /// <summary>A column offered natively from its section, or why not: one plumb basic segment, rectangular or round, no veneer.</summary>
    public static (ForeignElement? Element, string? Why) Column(ArchicadModel m, ArchicadElement c, string id, string storeyId, double storeyLevel, Vec3 shift)
    {
        var d = c.Details;
        if (B(d, "isSlanted")) return (null, "a slanted column (in a later stage)");
        if (!Basic(d)) return (null, "a column of several segments or a profile");
        if (d["veneer"] is JsonObject v && N(v, "thickness") > 1e-6) return (null, "a column with a veneer (in a later stage)");
        if ((d["segments"] as JsonArray)?.OfType<JsonObject>().FirstOrDefault()?["taper"] is JsonObject taper && !B(taper, "isHomogeneous")) return (null, "a tapered column (in a later stage)");
        var width = N(d, "width"); var depth = N(d, "depth");
        if (!(width > 0 && depth > 0)) return (null, "a column without a section");
        var origin = P(d["origin"]) + shift.Xy;
        var turn = N(d, "axisRotationAngle");
        var (ax, ay) = Anchor(S(d, "coreAnchor"));
        // The anchor is the point the origin stands on: the centre is the other way from it, in the column's turned frame.
        var u = new Vec2(Math.Cos(turn), Math.Sin(turn)); var vv = new Vec2(-u.Y, u.X);
        var centre = origin - u * (ax * width / 2) - vv * (ay * depth / 2);
        List<Vec2> plan;
        if (B(d, "circleBased"))
        {
            const int Sides = 72;
            plan = [.. Enumerable.Range(0, Sides).Select(i => centre + new Vec2(Math.Cos(2 * Math.PI * i / Sides), Math.Sin(2 * Math.PI * i / Sides)) * (width / 2))];
        }
        else plan = [centre - u * (width / 2) - vv * (depth / 2), centre + u * (width / 2) - vv * (depth / 2), centre + u * (width / 2) + vv * (depth / 2), centre - u * (width / 2) + vv * (depth / 2)];
        var baseZ = storeyLevel + N(d, "bottomOffset") + shift.Z;
        var height = N(d, "height");
        if (!(height > 0)) return (null, "a column without a height");
        var material = (string?)d["buildingMaterialId"]?["guid"] is { } g && m.BuildingMaterials.TryGetValue(g, out var mat) ? mat : null;
        var prism = new ShapePrism(At(plan, baseZ), [], new Vec3(0, 0, height), false, "archicad");
        return (new ForeignElement(id, IfcClass.Column, "COLUMN", c.IdText is { Length: > 0 } t ? t : null, null, storeyId, [prism], null, [], null, null, null, material, new JsonObject()), null);
    }

    /// <summary>A beam offered natively from its section, or why not: straight, level, one basic rectangular segment, no holes.</summary>
    public static (ForeignElement? Element, string? Why) Beam(ArchicadModel m, ArchicadElement bm, string id, string storeyId, Vec3 shift)
    {
        var d = bm.Details;
        if (B(d, "isSlanted") || Math.Abs(N(d, "slantAngle")) > 1e-6) return (null, "a sloping beam (in a later stage)");
        if (Math.Abs(N(d, "arcAngle")) > 1e-9 || Math.Abs(N(d, "verticalCurveHeight")) > 1e-9 || S(d, "beamShape") is { } shape && shape != "Straight") return (null, "a curved beam (in a later stage)");
        if (!Basic(d)) return (null, "a beam of several segments or a profile");
        if (d["holes"] is JsonArray { Count: > 0 }) return (null, "a beam with holes (in a later stage)");
        if (B(d, "circleBased")) return (null, "a round beam");
        if (Math.Abs(N(d, "profileAngle")) > 1e-6) return (null, "a beam turned about its axis");
        var width = N(d, "width"); var height = N(d, "height");
        if (!(width > 0 && height > 0)) return (null, "a beam without a section");
        var beg = P(d["begCoordinate"]) + shift.Xy; var end = P(d["endCoordinate"]) + shift.Xy;
        if ((end - beg).Length < 1e-3) return (null, "a beam without a length");
        var dir = (end - beg).Normalized(); var n = new Vec2(-dir.Y, dir.X);
        // Its reference axis at zCoordinate (absolute), the anchor on the section; offset across it.
        var (ax, ay) = Anchor(S(d, "anchorPoint"));
        if (B(d, "isFlipped")) ax = -ax;
        var z = N(d, "zCoordinate") + shift.Z;
        var centreAcross = -ax * width / 2 + N(d, "offset");
        var zc = z - ay * height / 2;
        var c0 = beg + n * centreAcross;
        var section = new List<Vec3>
        {
            new(c0.X - n.X * width / 2, c0.Y - n.Y * width / 2, zc - height / 2), new(c0.X + n.X * width / 2, c0.Y + n.Y * width / 2, zc - height / 2),
            new(c0.X + n.X * width / 2, c0.Y + n.Y * width / 2, zc + height / 2), new(c0.X - n.X * width / 2, c0.Y - n.Y * width / 2, zc + height / 2),
        };
        var material = (string?)d["buildingMaterialId"]?["guid"] is { } g && m.BuildingMaterials.TryGetValue(g, out var mat) ? mat : null;
        var sweep = end - beg;
        var prism = new ShapePrism(section, [], new Vec3(sweep.X, sweep.Y, 0), false, "archicad");
        return (new ForeignElement(id, IfcClass.Beam, "BEAM", bm.IdText is { Length: > 0 } t ? t : null, null, storeyId, [prism], null, [], null, null, null, material, new JsonObject()), null);
    }

    /// <summary>A zone as a space (a room): its outline and holes, its height and base, its name and number.</summary>
    public static (ForeignElement? Element, string? Why) Zone(ArchicadElement z, string id, string storeyId, double storeyLevel, Vec3 shift)
    {
        var d = z.Details;
        if (d["polygonArcs"] is JsonArray { Count: > 0 }) return (null, "a zone with curved edges (in a later stage)");
        var outline = Polygon(d["polygonOutline"]).Select(p => p + shift.Xy).ToList();
        if (outline.Count < 3) return (null, "a zone without an outline");
        var baseZ = storeyLevel + N(d, "bottomOffset") + shift.Z;
        var height = N(d, "height");
        if (!(height > 0)) return (null, "a zone without a height");
        var holes = (d["holes"] as JsonArray ?? []).OfType<JsonObject>().Select(h => Polygon(h["polygonOutline"] ?? h["polygonCoordinates"]).Select(p => p + shift.Xy).ToList())
            .Where(h => h.Count >= 3).Select(h => (IReadOnlyList<Vec3>)At(h, baseZ)).ToList();
        var prism = new ShapePrism(At(outline, baseZ), holes, new Vec3(0, 0, height), false, "archicad");
        var number = S(d, "numberStr") is { Length: > 0 } num ? num : null;
        var name = S(d, "name") is { Length: > 0 } nm ? nm : null;
        // Name is the number and LongName the name, as Archicad's IFC export writes a space.
        return (new ForeignElement(id, IfcClass.Space, null, number ?? name, null, storeyId, [prism], null, [], null, null, null, null, new JsonObject(), LongName: name), null);
    }

    /// <summary>A GlobalId derived from another and a word (an opening's, from its door's), the same every time.</summary>
    public static string Derived(string from, string what) => GlobalId.FromGuid(new Guid(Md5.HashData(System.Text.Encoding.UTF8.GetBytes(from + "\n" + what))));
}
