using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Platform.Analysis;

/// <summary>
/// A bar end's releases in its local axes, [ux, uy, uz, rx, ry, rz] (1, 2, 3 as docs/jg-structures.md lays them; true:
/// released), and where they were read: the element's own (<see cref="FrameReleases.Key"/>), else what the connection says.
/// </summary>
public sealed record EndReleases(bool[] I, bool[] J, string Source)
{
    public bool Any => I.Any(x => x) || J.Any(x => x);
}

/// <summary>
/// The bar ends' releases (docs/plan-fem.md §9.1), for the analysis and IFC's structural analysis view alike: the element's
/// attribute <c>analysisReleases</c> (<c>{i: [6], j: [6]}</c>, an end left out takes the default), else the default — a
/// member a known connection joins (a cleat, a gusset, a fly brace: <see cref="AnalyticalLink"/>) pinned at both its ends,
/// a framed roof's timber members pinned at their ends about their strong axis (purlins on rafters, collar ties: the hinge of
/// their bending under the roof's load; held across it, as the deck and battens hold a roof sideways, which S1 does not
/// model — a roof of members pinned both ways is a mechanism without them), any other bar fixed (monolithic
/// concrete, a steel frame's rigid joints; a steel base's own fixity is its support's). Pinned frees the two bending
/// rotations (ry, rz); a bar pinned at both ends keeps its torsion held at its i end only (released at j), so it neither
/// spins nor ties its nodes' rotations together. A member one of whose ends meets nothing (a cantilever) keeps both its ends
/// fixed: its support's, and its free tip's, where a release would hinge on nothing; a roof's member is pinned only at an end where it bears on something (a support, a wall, a member
/// running through: a purlin, a wall plate), so where only members' ends meet (a corner's tails) they stay joined rigidly. A bar with its torsion released at both ends keeps it held at its i end
/// (no mechanism). The releases
/// are a member's own ends: its first piece's i end and its last piece's j end; where a member is split, its pieces stay
/// continuous.
/// </summary>
public static class FrameReleases
{
    public const string Key = "analysisReleases";
    public static readonly string[] Dofs = ["ux", "uy", "uz", "rx", "ry", "rz"];
    public static bool[] Fixed => [false, false, false, false, false, false];
    public static bool[] Pinned => [false, false, false, false, true, true];
    /// <summary>Pinned about local 3 only: the hinge of a member's bending in the plane of its depth (a rafter's, a purlin's under its load), held across it.</summary>
    public static bool[] PinnedStrong => [false, false, false, false, false, true];

    /// <summary>An end's releases from what a command or the attribute gives: six booleans, or "fixed" / "pinned"; null for anything else.</summary>
    public static bool[]? Parse(JsonNode? n)
    {
        if (n is JsonValue v && v.TryGetValue<string>(out var s)) return s switch { "fixed" => Fixed, "pinned" => Pinned, _ => null };
        if (n is not JsonArray a || a.Count != 6) return null;
        var o = new bool[6];
        for (var i = 0; i < 6; i++)
        {
            if (a[i] is JsonValue x && x.TryGetValue<bool>(out var b)) o[i] = b;
            else return null;
        }
        return o;
    }

    public static JsonArray ToJson(bool[] r) => new(r.Select(x => (JsonNode)x).ToArray());

    /// <summary>The element's own releases, each end null when it says none.</summary>
    public static (bool[]? I, bool[]? J) Own(Entity? e) =>
        e?.Attrs[Key] is JsonObject o ? (Parse(o["i"]), Parse(o["j"])) : (null, null);

    /// <summary>Every bar's releases at its own ends (only the bars with one released end are listed).</summary>
    public static Dictionary<AFrame, EndReleases> Of(AnalyticalModel am, IModelView m)
    {
        var linked = am.Links.Select(l => l.MemberId).ToHashSet();
        // A bar end nothing else meets and nothing holds: the member is a cantilever there, so its other end stays fixed.
        var uses = new Dictionary<ANode, int>();
        foreach (var f in am.Frames) { uses[f.I] = uses.GetValueOrDefault(f.I) + 1; uses[f.J] = uses.GetValueOrDefault(f.J) + 1; }
        foreach (var sh in am.Shells) foreach (var n in sh.Nodes) uses[n] = uses.GetValueOrDefault(n) + 1;
        bool Free(ANode n) => uses.GetValueOrDefault(n) <= 1 && n.Restraints == null;
        // What a member's end bears on: a support, a shell (a wall), or a member running through (a node inside it, not one of its ends).
        var bars = am.Frames.GroupBy(f => f.Source, ReferenceEqualityComparer.Instance).Select(g => g.ToList()).ToList();
        var through = new HashSet<ANode>();
        foreach (var b in bars) for (var k = 0; k + 1 < b.Count; k++) through.Add(b[k].J);
        var onShell = am.Shells.SelectMany(sh => sh.Nodes).ToHashSet();
        bool Bears(ANode n) => n.Restraints != null || onShell.Contains(n) || through.Contains(n);
        var o = new Dictionary<AFrame, EndReleases>();
        foreach (var bar in am.Frames.GroupBy(f => f.Source, ReferenceEqualityComparer.Instance))
        {
            var pieces = bar.ToList();
            var src = pieces[0].Source;
            var (ownI, ownJ) = Own(m.Get(src.SourceId));
            string source;
            bool[] def;
            if (linked.Contains(src.SourceId)) { def = Pinned; source = "pinned: a connection joins it"; }
            else if (src.Category == AnalyticalKinds.Roofs && src.IfcClass == "IfcMember") { def = PinnedStrong; source = "pinned about its strong axis: a roof's timber member"; }
            else { def = Fixed; source = "fixed"; }
            if (ownI != null || ownJ != null) source = "the element's own";
            var (endI, endJ) = (pieces[0].I, pieces[^1].J);
            // A roof's member pinned only where it bears on something, and not at the support of a cantilever.
            var roofs = def.SequenceEqual(PinnedStrong);
            // A free end itself is not released either: nothing meets it to hinge on, and a release there would leave its node
            // turning on nothing (with an arm, an unsymmetric section's, a mechanism).
            var i = (bool[])(ownI ?? (Free(endI) || Free(endJ) || (roofs && !Bears(endI)) ? Fixed : def)).Clone();
            var j = (bool[])(ownJ ?? (Free(endI) || Free(endJ) || (roofs && !Bears(endJ)) ? Fixed : def)).Clone();
            // Pinned at both ends: the twist held at i only.
            if (i[4] && i[5] && j[4] && j[5] && !i[3]) j[3] = true;
            if (i[3] && j[3]) i[3] = false;
            if (!i.Any(x => x) && !j.Any(x => x)) continue;
            var first = pieces[0];
            var last = pieces[^1];
            if (first == last) { o[first] = new EndReleases(i, j, source); continue; }
            o[first] = new EndReleases(i, Fixed, source);
            o[last] = new EndReleases(Fixed, j, source);
        }
        return o;
    }
}
