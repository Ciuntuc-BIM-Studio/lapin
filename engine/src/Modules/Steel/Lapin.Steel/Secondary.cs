using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Structure;

namespace Lapin.Steel;

/// <summary>
/// A hall's secondary steel (docs/plan-steel.md stage 4, decision 275), planned from its spec as its frames are: members by
/// their role (kept over a rebuild by it) and their connections by their place.
/// <list type="bullet">
/// <item><b>Purlins</b> (<c>pur:a:3:2</c>: side, line from the eaves, bay; <c>pur:a:3</c> continuous) on the rafters' top,
/// their web square to the roof, from 150 mm inside the columns' face to 200 mm short of the apex, as many lines as the
/// spacing asks along the slope; one a bay, or continuous; past the gables by the overhang. Each on a cleat on every rafter
/// it crosses.</item>
/// <item><b>Side rails</b> (<c>rail:a:2:4</c>) on the columns' outer face, one a bay, their web level and their toes down,
/// from the first rail's height to under the eaves (under the eaves beam when there is one); <b>gable rails</b>
/// (<c>grail:0:2:1</c>) on the gable posts' (or the gable frame's columns') outer face, post to post, as high as the
/// rafter over them lets; each on a cleat on every column or post it meets.</item>
/// <item><b>The eaves beam</b> (<c>eb:a:3</c>), a rail at the top of the columns' outer face, one a bay.</item>
/// <item><b>Bracing</b> in the braced bays: X in the roof's plane under the rafters' top flange (<c>rbr:2:a:1:1</c>) and in
/// the long walls' plane through the columns' axes (<c>wbr:2:a:0:2</c>), in panels about as long as the bay, the two
/// diagonals either side of gusset plates welded to the rafters' or the columns' web.</item>
/// <item><b>Fly braces</b> (<c>fly:1:a:3:p</c>, <c>cfly:1:a:3:m</c>) from every second purlin and side rail down to the
/// rafter's bottom flange or the column's inner flange, either side, clear of the haunches.</item>
/// </list>
/// </summary>
public static class HallSecondary
{
    /// <summary>The purlins' first line in from the column's face and their last short of the apex (along x).</summary>
    const double EavesIn = 0.15, ApexOut = 0.2;
    /// <summary>The bracing's first and last panel points in from the column's face and the apex.</summary>
    const double BraceIn = 0.3;

    static Vec3 L(HallSpec s, double x, double y, double z) { var p = Halls.Plan(s, x, y); return new Vec3(p.X, p.Y, z); }
    static JsonArray J3(Vec3 p) => new(Math.Round(p.X, 9), Math.Round(p.Y, 9), Math.Round(p.Z, 9));
    static Vec3 Dx3(HallSpec s) => new(Math.Cos(s.Angle * Math.PI / 180), Math.Sin(s.Angle * Math.PI / 180), 0);
    static Vec3 Dy3(HallSpec s) => new(-Math.Sin(s.Angle * Math.PI / 180), Math.Cos(s.Angle * Math.PI / 180), 0);

    /// <summary>The roof's sides: a and b of a duo-pitch, a alone of a mono-pitch.</summary>
    static string[] Sides(HallSpec s) => s.Mono ? ["a"] : ["a", "b"];

    /// <summary>A point of a side's slope at <paramref name="u"/> from its eaves' column axis, as x across the hall.</summary>
    static double X(HallSpec s, string side, double u) => side == "a" ? u : s.Span - u;

    static double Cos(HallSpec s) => Math.Cos(s.Pitch * Math.PI / 180);
    static double Sin(HallSpec s) => Math.Sin(s.Pitch * Math.PI / 180);

    /// <summary>The roof's normal over a side, up and outwards.</summary>
    static Vec3 Normal(HallSpec s, string side) => Dx3(s) * ((side == "a" ? -1 : 1) * Sin(s)) + new Vec3(0, 0, Cos(s));

    /// <summary>The rafter of frame <paramref name="f"/> on a side, by its role.</summary>
    static string Rafter(HallSpec s, int f, string side) => s.Mono ? $"f{f}:raf" : $"f{f}:raf:{side}";

    static bool IsGable(HallSpec s, int f) => s.Gable == "posts" && (f == 0 || f == s.Bays.Count);

    /// <summary>The rafter's section of a frame (a gable's of posts has its own).</summary>
    static Section RafterOf(HallSpec s, int f) => IsGable(s, f) ? s.GableRafter : s.Rafter;

    /// <summary>A rafter's axis on a side (on its top, from its eaves' column axis to the apex or across).</summary>
    static MemberAxis RafterAxis(HallSpec s, int f, string side)
    {
        var y = s.Stations[f];
        var (x0, x1) = s.Mono ? (0.0, s.Span) : side == "a" ? (0.0, s.ApexX) : (s.Span, s.ApexX);
        return new MemberAxis(L(s, x0, y, s.Top(x0)), L(s, x1, y, s.Top(x1)), RafterOf(s, f), 0, "top");
    }

    /// <summary>Where the purlins stand on a side: their distance from the eaves' column axis, across (horizontal), first at the eaves.</summary>
    public static List<double> PurlinLines(HallSpec s)
    {
        var u0 = s.Column.Depth / 2 + EavesIn;
        var u1 = s.Mono ? s.Span - s.Column.Depth / 2 - EavesIn : s.ApexX - ApexOut;
        if (u1 <= u0) return [u0];
        var n = Math.Max(1, (int)Math.Ceiling((u1 - u0) / Cos(s) / s.PurlinSpacing - 1e-9));
        return Enumerable.Range(0, n + 1).Select(k => u0 + (u1 - u0) * k / n).ToList();
    }

    /// <summary>The eaves beam's height (its middle), on the columns' outer face under the eaves.</summary>
    static double EavesBeamZ(HallSpec s) => s.Eaves - s.EavesBeamProfile.Width / 2 - 0.05;

    /// <summary>The side rails' heights (their middle), from the first rail up to under the eaves (or a spacing under the eaves beam), equally spaced.</summary>
    public static List<double> RailLevels(HallSpec s)
    {
        var z0 = s.RailBottom;
        var top = s.EavesBeam ? EavesBeamZ(s) - s.RailSpacing : s.Eaves - s.Rail.Width / 2 - 0.05;
        if (top <= z0 + 1e-6) return [z0];
        var n = Math.Max(1, (int)Math.Ceiling((top - z0) / s.RailSpacing - 1e-9));
        return Enumerable.Range(0, n + 1).Select(k => z0 + (top - z0) * k / n).ToList();
    }

    /// <summary>How deep under a rafter's top the roof bracing's plane is (and its gussets).</summary>
    static double RoofBracingDepth(HallSpec s) => s.Rafter.Flange + 0.04;

    static double GussetT(HallSpec s) => s.NodeParamsOf("gusset").Plate ?? 0.01;

    /// <summary>A member along a line, its axis on its section's bottom (on a face), turned so its section's up is <paramref name="up"/>; toes down when its web is level.</summary>
    static Halls.Planned Along(string role, string kind, Section section, Vec3 a, Vec3 b, Vec3 up, string predefined, string? objectType, SteelEnd? ends = null)
    {
        var bare = new MemberAxis(a, b, section, 0, "bottom");
        var (v0, w0) = bare.Frame;
        var rot = Math.Atan2(-Vec3.Dot(up, v0), Vec3.Dot(up, w0)) * 180 / Math.PI;
        var turned = bare with { Rotation = rot };
        // A channel's web is at its left: kept up when the section lies on its side (its toes down).
        var flipped = section.Kind is "C" or "SIGMA" && Math.Abs(Vec3.Dot(turned.Frame.V, new Vec3(0, 0, 1))) > 0.7 && turned.Frame.V.Z > 0;
        var attrs = new JsonObject
        {
            ["exportAs"] = "IfcMember", ["start"] = J3(a), ["end"] = J3(b), ["rotation"] = Math.Round(rot, 9), ["insertion"] = "bottom",
        };
        if (flipped) attrs["flipped"] = true;
        if (objectType != null) attrs["objectType"] = objectType;
        var e = ends ?? SteelEnd.Square;
        return new Halls.Planned(role, kind, SteelModule.Member, section, attrs, predefined, (e, e));
    }

    /// <summary>A member between two points, its section centred on its axis.</summary>
    static Halls.Planned Between(string role, string kind, Section section, Vec3 a, Vec3 b, string predefined, string? objectType, SteelEnd ends)
    {
        var attrs = new JsonObject { ["exportAs"] = "IfcMember", ["start"] = J3(a), ["end"] = J3(b), ["rotation"] = 0.0, ["insertion"] = "center" };
        if (objectType != null) attrs["objectType"] = objectType;
        return new Halls.Planned(role, kind, SteelModule.Member, section, attrs, predefined, (ends, ends));
    }

    /// <summary>The members (by role) that run along the hall bay by bay, and how they meet the frames: for frame j, (role, end) of those that come to it.</summary>
    static List<(string Role, string? End)> AtFrame(string prefix, int j, int bays, bool continuous)
    {
        if (continuous) return [(prefix, null)];
        var o = new List<(string, string?)>();
        if (j >= 1) o.Add(($"{prefix}:{j}", "end"));
        if (j < bays) o.Add(($"{prefix}:{j + 1}", "start"));
        return o;
    }

    /// <summary>The secondary members and their connections a spec plans (none when it has its secondary steel off).</summary>
    internal static (List<Halls.Planned> Members, List<Halls.PlannedNode> Nodes) Plan(HallSpec s)
    {
        var members = new List<Halls.Planned>();
        var nodes = new List<Halls.PlannedNode>();
        if (!s.Secondary) return (members, nodes);
        var stations = s.Stations;
        var nb = s.Bays.Count;
        var len = s.Length;
        var cleat = s.NodeParamsOf("cleat");
        var gusset = s.NodeParamsOf("gusset");
        var dx = Dx3(s);
        var dy = Dy3(s);
        var up = new Vec3(0, 0, 1);

        // ---- purlins, on the rafters' top
        var lines = PurlinLines(s);
        var continuous = s.PurlinRun == "continuous";
        foreach (var side in Sides(s))
        {
            var n = Normal(s, side);
            for (var k = 0; k < lines.Count; k++)
            {
                var x = X(s, side, lines[k]);
                var z = s.Top(x);
                var prefix = $"pur:{side}:{k}";
                if (continuous)
                    members.Add(Along(prefix, "purlin", s.Purlin, L(s, x, -s.PurlinOverhang, z), L(s, x, len + s.PurlinOverhang, z), n, "PURLIN", null));
                else
                    for (var i = 1; i <= nb; i++)
                    {
                        var y0 = i == 1 ? -s.PurlinOverhang : stations[i - 1];
                        var y1 = i == nb ? len + s.PurlinOverhang : stations[i];
                        members.Add(Along($"{prefix}:{i}", "purlin", s.Purlin, L(s, x, y0, z), L(s, x, y1, z), n, "PURLIN", null));
                    }
                for (var j = 0; j <= nb; j++)
                    nodes.Add(new($"cleat@{prefix}@f{j}", "cleat", [(Rafter(s, j, side), null), .. AtFrame(prefix, j, nb, continuous)], cleat));
            }
        }

        // ---- side rails and the eaves beam, on the columns' outer face
        var levels = RailLevels(s);
        foreach (var side in new[] { "a", "b" })
        {
            var outward = side == "a" ? dx * -1 : dx;
            var face = side == "a" ? -s.Column.Depth / 2 : s.Span + s.Column.Depth / 2;
            void Wall(string prefix, Section section, double z, string objectType)
            {
                for (var i = 1; i <= nb; i++)
                    members.Add(Along($"{prefix}:{i}", prefix.StartsWith("eb", StringComparison.Ordinal) ? "eaves" : "rail", section, L(s, face, stations[i - 1], z), L(s, face, stations[i], z), outward, "USERDEFINED", objectType));
                for (var j = 0; j <= nb; j++)
                    nodes.Add(new($"cleat@{prefix}@f{j}", "cleat", [($"f{j}:col:{side}", null), .. AtFrame(prefix, j, nb, false)], cleat));
            }
            for (var k = 0; k < levels.Count; k++) Wall($"rail:{side}:{k}", s.Rail, levels[k], "Side rail");
            if (s.EavesBeam) Wall($"eb:{side}", s.EavesBeamProfile, EavesBeamZ(s), "Eaves beam");
        }

        // ---- gable rails, post to post (or column to column of a gable frame), as high as the rafter over them lets
        var step = levels.Count > 1 ? levels[1] - levels[0] : s.RailSpacing;
        foreach (var g in new[] { 0, 1 })
        {
            var f = g == 0 ? 0 : nb;
            var posts = s.Gable == "posts";
            var xs = new List<double> { 0 };
            if (posts) xs.AddRange(s.PostStations);
            xs.Add(s.Span);
            string Host(int m) => m == 0 ? $"f{f}:col:a" : m == xs.Count - 1 ? $"f{f}:col:b" : $"f{f}:post:{m}";
            var half = posts ? s.Post.Depth / 2 : s.Column.Width / 2;
            var y = g == 0 ? -half : len + half;
            var outward = g == 0 ? dy * -1 : dy;
            var raf = RafterOf(s, f).Depth / Cos(s);
            double Under(double x) => s.Top(x) - raf;
            for (var k = 0; ; k++)
            {
                var z = s.RailBottom + step * k;
                if (z > s.ApexHeight) break;
                var made = new List<int>();
                for (var m = 1; m < xs.Count; m++)
                {
                    if (z + s.Rail.Width / 2 + 0.05 > Math.Min(Under(xs[m - 1]), Under(xs[m]))) continue;
                    members.Add(Along($"grail:{g}:{k}:{m}", "rail", s.Rail, L(s, xs[m - 1], y, z), L(s, xs[m], y, z), outward, "USERDEFINED", "Side rail"));
                    made.Add(m);
                }
                if (made.Count == 0) { if (z > s.Eaves) break; continue; }
                for (var m = 0; m < xs.Count; m++)
                {
                    var js = new List<(string, string?)>();
                    if (made.Contains(m)) js.Add(($"grail:{g}:{k}:{m}", "end"));
                    if (made.Contains(m + 1)) js.Add(($"grail:{g}:{k}:{m + 1}", "start"));
                    if (js.Count > 0) nodes.Add(new($"cleat@grail:{g}:{k}@{m}", "cleat", [(Host(m), null), .. js], cleat));
                }
            }
        }

        // ---- bracing in the braced bays: the roof's plane and the long walls'
        var brace = s.Brace;
        var off = GussetT(s) / 2 + brace.Depth / 2;
        var setback = new SteelEnd(Extension: -(Math.Max(s.Rafter.Web, s.Column.Web) / 2 + 0.04));
        foreach (var b in s.Bracing.Where(b => b >= 1 && b <= nb))
        {
            var bay = s.Bays[b - 1];
            if (s.RoofBracing)
                foreach (var side in Sides(s))
                {
                    var n = Normal(s, side);
                    var u0 = s.Column.Depth / 2 + BraceIn;
                    var u1 = s.Mono ? s.Span - s.Column.Depth / 2 - BraceIn : s.ApexX - BraceIn;
                    var m = Math.Max(1, (int)Math.Round((u1 - u0) / Cos(s) / bay));
                    var depth = RoofBracingDepth(s);
                    Vec3 H(int f, int j) { var x = X(s, side, u0 + (u1 - u0) * j / m); return L(s, x, stations[f], s.Top(x)) - n * depth; }
                    Plan2(members, nodes, $"rbr:{b}:{side}", b, m, H, n * off, f => Rafter(s, f, side), brace, setback, depth, gusset);
                }
            if (s.WallBracing)
                foreach (var side in new[] { "a", "b" })
                {
                    var x = side == "a" ? 0 : s.Span;
                    var z0 = s.RailBottom;
                    var z1 = s.Eaves - (s.EavesHaunch?.Depth ?? 0) - s.Rafter.Depth - 0.25;
                    if (z1 <= z0 + 0.5) continue;
                    var m = Math.Max(1, (int)Math.Round((z1 - z0) / bay));
                    Vec3 H(int f, int j) => L(s, x, stations[f], z0 + (z1 - z0) * j / m);
                    Plan2(members, nodes, $"wbr:{b}:{side}", b, m, H, dx * off, f => $"f{f}:col:{side}", brace, setback, 0, gusset);
                }
        }

        // ---- fly braces: from every second purlin and side rail to the rafter's bottom flange and the column's inner flange
        if (s.FlyBraces)
        {
            var fly = HallSpec.FlyBrace;
            var lo = s.Column.Depth / 2 + (s.EavesHaunch?.Length ?? 0) * Cos(s) + 0.1;
            var hi = s.Mono ? s.Span - lo : s.ApexX - (s.ApexHaunch?.Length ?? 0) * Cos(s) - 0.1;
            for (var j = 0; j <= nb; j++)
            {
                if (IsGable(s, j)) continue;
                var signs = j == 0 ? new[] { 1 } : j == nb ? new[] { -1 } : new[] { 1, -1 };
                foreach (var side in Sides(s))
                {
                    var r = RafterAxis(s, j, side);
                    var rs = r.Section;
                    var tf = rs.Flange > 0 ? rs.Flange : rs.Web;
                    for (var k = 1; k < lines.Count - 1; k += 2)
                    {
                        if (lines[k] < lo || lines[k] > hi) continue;
                        var x = X(s, side, lines[k]);
                        foreach (var sg in signs)
                        {
                            var y = stations[j] + sg * rs.Depth;
                            var purlin = PurlinAxis(s, side, x, y);
                            var top = purlin.At(new Vec2(WebX(purlin), 0), 0);
                            var st = Station(r, L(s, x, stations[j], s.Top(x)));
                            var low = r.At(new Vec2(0, -rs.Depth / 2 + tf / 2), st) + dy * (sg * (rs.Width / 2 - 0.025));
                            var role = $"fly:{j}:{side}:{k}:{(sg > 0 ? "p" : "m")}";
                            members.Add(Between(role, "fly", fly, top, low, "BRACE", "Fly brace", SteelEnd.Square));
                            var bayRole = continuous ? $"pur:{side}:{k}" : $"pur:{side}:{k}:{(sg > 0 ? j + 1 : j)}";
                            nodes.Add(new($"fly@{role}", "fly", [(Rafter(s, j, side), null), (bayRole, null), (role, null)], NodeParams.None));
                        }
                    }
                }
                foreach (var side in new[] { "a", "b" })
                {
                    var inward = side == "a" ? dx : dx * -1;
                    var xc = side == "a" ? 0 : s.Span;
                    var cs = s.Column;
                    var limit = s.Eaves - (s.EavesHaunch?.Depth ?? 0) - s.Rafter.Depth - 0.3;
                    for (var k = 1; k < levels.Count; k += 2)
                    {
                        if (levels[k] > limit) continue;
                        foreach (var sg in signs)
                        {
                            var y = stations[j] + sg * cs.Depth;
                            var face = side == "a" ? -cs.Depth / 2 : s.Span + cs.Depth / 2;
                            var rail = RailAxis(s, side, face, y, levels[k]);
                            var top = rail.At(new Vec2(WebX(rail), 0), 0);
                            var low = L(s, xc, stations[j], levels[k]) + inward * (cs.Depth / 2 - cs.Flange / 2) + dy * (sg * (cs.Width / 2 - 0.025));
                            var role = $"cfly:{j}:{side}:{k}:{(sg > 0 ? "p" : "m")}";
                            members.Add(Between(role, "fly", fly, top, low, "BRACE", "Fly brace", SteelEnd.Square));
                            nodes.Add(new($"fly@{role}", "fly", [($"f{j}:col:{side}", null), ($"rail:{side}:{k}:{(sg > 0 ? j + 1 : j)}", null), (role, null)], NodeParams.None));
                        }
                    }
                }
            }
        }
        return (members, nodes);
    }

    /// <summary>
    /// One braced bay's X in panels: between frame b−1 and frame b, panel points <paramref name="h"/>(f, j) for j = 0…m;
    /// the first diagonal of each panel on the gusset's one side (+<paramref name="off"/>), the second on its other; a gusset
    /// at each panel point the diagonals come to, on the host's web, reaching into the bay.
    /// </summary>
    static void Plan2(List<Halls.Planned> members, List<Halls.PlannedNode> nodes, string prefix, int b, int m, Func<int, int, Vec3> h, Vec3 off, Func<int, string> host,
        Section brace, SteelEnd setback, double depth, NodeParams gusset)
    {
        for (var j = 0; j < m; j++)
        {
            members.Add(Between($"{prefix}:{j}:1", "brace", brace, h(b - 1, j) + off, h(b, j + 1) + off, "BRACE", null, setback));
            members.Add(Between($"{prefix}:{j}:2", "brace", brace, h(b - 1, j + 1) - off, h(b, j) - off, "BRACE", null, setback));
        }
        var extra = new JsonObject { ["depth"] = Math.Round(depth, 9) };
        for (var j = 0; j <= m; j++)
        {
            var near = new List<(string, string?)>();
            if (j < m) near.Add(($"{prefix}:{j}:1", "start"));
            if (j >= 1) near.Add(($"{prefix}:{j - 1}:2", "start"));
            nodes.Add(new($"gusset@{prefix}:{j}@f{b - 1}", "gusset", [(host(b - 1), null), .. near], gusset, (JsonObject)extra.DeepClone()));
            var far = new List<(string, string?)>();
            if (j >= 1) far.Add(($"{prefix}:{j - 1}:1", "end"));
            if (j < m) far.Add(($"{prefix}:{j}:2", "end"));
            nodes.Add(new($"gusset@{prefix}:{j}@f{b}", "gusset", [(host(b), null), .. far], gusset, (JsonObject)extra.DeepClone()));
        }
    }

    /// <summary>A purlin's axis through (x, y) on the roof, as the plan lays it (for the fly braces' tops).</summary>
    static MemberAxis PurlinAxis(HallSpec s, string side, double x, double y)
    {
        var p = Along("", "", s.Purlin, L(s, x, y, s.Top(x)), L(s, x, y + 1, s.Top(x)), Normal(s, side), "", null);
        return new MemberAxis(L(s, x, y, s.Top(x)), L(s, x, y + 1, s.Top(x)), s.Purlin, (double)p.Attrs["rotation"]!, "bottom", p.Attrs["flipped"] != null);
    }

    static MemberAxis RailAxis(HallSpec s, string side, double face, double y, double z)
    {
        var outward = side == "a" ? Dx3(s) * -1 : Dx3(s);
        var p = Along("", "", s.Rail, L(s, face, y, z), L(s, face, y + 1, z), outward, "", null);
        return new MemberAxis(L(s, face, y, z), L(s, face, y + 1, z), s.Rail, (double)p.Attrs["rotation"]!, "bottom", p.Attrs["flipped"] != null);
    }

    /// <summary>Where a section's web is across it (x), as the member carries it (mirrored when flipped).</summary>
    public static double WebX(MemberAxis ax)
    {
        var sec = ax.Section;
        var x = sec.Kind is "C" or "SIGMA" ? -sec.Width / 2 + sec.Web / 2 : sec.Kind == "U" ? -sec.Width / 2 + sec.Web / 2 : 0;
        return ax.Flipped ? -x : x;
    }

    /// <summary>The station along an axis nearest a point.</summary>
    public static double Station(MemberAxis ax, Vec3 p) => Vec3.Dot(p - ax.Start, ax.U);
}
