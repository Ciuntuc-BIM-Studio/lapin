using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Steel;

/// <summary>
/// A haunch under one end of a member (decision 270): a tapered T welded under its bottom flange, <see cref="Length"/>
/// along the member from the end's cut and <see cref="Depth"/> deep below its underside at the cut (square to the member),
/// its end in the end's plane (a column's face, the apex's mitre). Its <see cref="Kind"/>:
/// <list type="bullet">
/// <item><c>cut</c>: cut from the same section (an I cut obliquely along its web, one half turned and welded under it): the
/// member's own web and flange thicknesses and its flange's width;</item>
/// <item><c>plates</c>: welded from plates, a web plate <see cref="Web"/> thick and a flange plate <see cref="Flange"/> thick
/// and <see cref="Width"/> wide (the member's flange width when 0).</item>
/// </list>
/// <see cref="Own"/>: set on the haunch itself, so a hall regenerating its members keeps it as it is.
/// </summary>
public sealed record Haunch(double Length, double Depth, string Kind = "cut", double Web = 0.01, double Flange = 0.012, double Width = 0, bool Own = false)
{
    public static readonly string[] Kinds = ["cut", "plates"];

    /// <summary>The key a member keeps its haunches under: {start?, end?}.</summary>
    public const string Key = "haunches";

    public JsonObject ToJson()
    {
        var o = new JsonObject { ["length"] = Length, ["depth"] = Depth, ["kind"] = Kind };
        if (Kind == "plates") { o["web"] = Web; o["flange"] = Flange; if (Width > 0) o["width"] = Width; }
        if (Own) o["own"] = true;
        return o;
    }

    public static Haunch? From(JsonNode? n)
    {
        if (n is not JsonObject o || Json.Number(o["length"]) is not { } l || Json.Number(o["depth"]) is not { } d || !(l > 0 && d > 0)) return null;
        var kind = (string?)o["kind"] is { } k && Kinds.Contains(k) ? k : "cut";
        return new Haunch(l, d, kind, Json.Number(o["web"]) ?? 0.01, Json.Number(o["flange"]) ?? 0.012, Json.Number(o["width"]) ?? 0,
            o["own"] is JsonValue v && v.TryGetValue<bool>(out var own) && own);
    }

    /// <summary>A haunch as a command gives it, checked; null (or nothing) is none.</summary>
    public static Haunch? Parse(JsonNode? n)
    {
        if (n == null) return null;
        if (n is JsonObject o && (string?)o["kind"] is { } k && !Kinds.Contains(k)) throw new CommandException($"A haunch is cut from the section or welded from plates, not „{k}”.");
        var h = From(n) ?? throw new CommandException("A haunch needs a length and a depth, both more than 0.");
        if (h.Kind == "plates" && !(h.Web > 0 && h.Flange > 0 && h.Width >= 0)) throw new CommandException("A haunch welded from plates needs its web's and its flange's thickness.");
        return h;
    }

    /// <summary>The haunches a member keeps: at its start and at its end.</summary>
    public static (Haunch? Start, Haunch? End) Of(Entity e) =>
        e.Attrs[Key] is JsonObject o ? (From(o["start"]), From(o["end"])) : (null, null);

    public static JsonObject? Json2(Haunch? start, Haunch? end)
    {
        if (start == null && end == null) return null;
        var o = new JsonObject();
        if (start != null) o["start"] = start.ToJson();
        if (end != null) o["end"] = end.ToJson();
        return o;
    }

    /// <summary>The web's thickness, the flange's thickness and its width, as the kind takes them (from the section, or the plates').</summary>
    public (double Web, double Flange, double Width) Plates(Lapin.Structure.Section s) =>
        Kind == "plates" ? (Web, Flange, Width > 0 ? Width : s.Width) : (s.Web > 0 ? s.Web : 0.01, s.Flange > 0 ? s.Flange : 0.012, s.Width);
}

/// <summary>
/// A haunch's two pieces, in the plane of its member's web: the web (a triangle, <c>Web</c> thick across it) and the flange
/// along its sloping edge (<c>Flange</c> thick, <c>Width</c> wide), each a polygon in the member's (station, section y)
/// coordinates, swept across from x = <c>-t/2</c>.
/// </summary>
public sealed record HaunchShape(IReadOnlyList<Vec2> WebOutline, IReadOnlyList<Vec2> FlangeOutline, double Web, double Flange, double Width)
{
    public double WebVolume => Math.Abs(Polygon2.SignedArea(WebOutline)) * Web;
    public double FlangeVolume => Math.Abs(Polygon2.SignedArea(FlangeOutline)) * Width;
    public double Volume => WebVolume + FlangeVolume;
}

public static class Haunches
{
    /// <summary>
    /// The haunch at one end of a member, null when it does not fit (the reason in <paramref name="problems"/>): the
    /// triangle from the underside at the end's plane, <see cref="Haunch.Length"/> back along the underside, and
    /// <see cref="Haunch.Depth"/> down the end's plane; the flange inside its sloping edge, the web the rest.
    /// </summary>
    public static HaunchShape? Shape(MemberAxis ax, CutPlane plane, Haunch h, bool atEnd, double s0, double s1, List<string> problems)
    {
        var (outline, _) = ax.Profile;
        var y0 = outline.Min(p => p.Y);
        var u = ax.U;
        double Station(Vec2 p) => Vec3.Dot(plane.Normal, plane.Point - ax.At(p, 0)) / Vec3.Dot(plane.Normal, u);
        var (web, flange, width) = h.Plates(ax.Section);
        var back = atEnd ? -1 : 1;
        var a = new Vec2(Station(new(0, y0)), y0);
        var b = new Vec2(a.X + back * h.Length, y0);
        var c = new Vec2(Station(new(0, y0 - h.Depth)), y0 - h.Depth);
        // It must stay on the member: shorter than the member, short of its other end.
        if (atEnd ? b.X < s0 + 1e-3 : b.X > s1 - 1e-3) { problems.Add("A haunch is longer than its member."); return null; }
        // The flange inside the sloping edge c → b: that edge moved towards a by the flange's thickness.
        var (dirX, dirY) = (b.X - c.X, b.Y - c.Y);
        var len = Math.Sqrt(dirX * dirX + dirY * dirY);
        var height = Math.Abs(Cross(b - c, a - c)) / len;
        if (flange >= height - 1e-4) { problems.Add("A haunch is too shallow for its flange."); return null; }
        var k = 1 - flange / height;
        // The inner edge is the sloping edge scaled about a by k (the triangles are alike).
        var q2 = a + (b - a) * k;
        var q3 = a + (c - a) * k;
        return new HaunchShape([a, q2, q3], [c, b, q2, q3], web, flange, width);
    }

    static double Cross(Vec2 p, Vec2 q) => p.X * q.Y - p.Y * q.X;

    /// <summary>A polygon of the member's (station, y) plane made a solid, <paramref name="t"/> thick across it, centred on the web.</summary>
    public static Solid? Solid(MemberAxis ax, IReadOnlyList<Vec2> outline, double t, string tag) =>
        Prism.Extrude(outline.Select(p => ax.At(new Vec2(-t / 2, p.Y), p.X)).ToList(), null, ax.Frame.V * t, tag);

    /// <summary>The same as IFC sweeps it: the polygon on the web's plane at x = t/2, its x along the member, swept across.</summary>
    public static IfcSweep Sweep(MemberAxis ax, IReadOnlyList<Vec2> outline, double t) =>
        new(outline, [], ax.At(new Vec2(t / 2, 0), 0), ax.U, -ax.Frame.V, t);

    /// <summary>A member's haunches at its ends (whichever it has), with the planes its body was built between.</summary>
    public static List<(string End, Haunch Haunch, HaunchShape Shape)> Of(Entity e, MemberAxis ax, SteelBody.Built built, List<string> problems)
    {
        var o = new List<(string, Haunch, HaunchShape)>();
        var (hs, he) = Haunch.Of(e);
        if (hs != null && Shape(ax, built.Start, hs, false, built.S0, built.S1, problems) is { } a) o.Add(("start", hs, a));
        if (he != null && Shape(ax, built.End, he, true, built.S0, built.S1, problems) is { } b) o.Add(("end", he, b));
        return o;
    }
}
