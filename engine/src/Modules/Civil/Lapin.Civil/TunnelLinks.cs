using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Alignments;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Civil;

/// <summary>
/// A lining linked to its axis's source (docs/plan-wing-walls.md, S4, the user's answer of 23.11.2026): one taken from a road, a
/// path, a wall, a track or a drafted line keeps the element and its two stations (<c>axisSource {id, from, to, linked: true}</c>),
/// and when that element's plan geometry or red line changes the lining takes its axis again in the same undo step
/// (<see cref="Follow"/>), its blocks and rings keeping their GlobalIds (they are keyed by their index). Stations past the source's
/// new length are held at its ends, with a note, and come back when it grows again. A lining whose source is deleted, or whose own
/// axis is edited on it, is unlinked with a note, never deleted; unlinked (<c>civil.unlinkTunnelLining</c>) it is the copy every
/// lining was before, and <c>civil.updateTunnelLining {axis}</c> links it again. A lining saved before this has no <c>linked</c>
/// flag: it stays the copy it was.
/// </summary>
public static partial class Tunnels
{
    const string SourceKey = "axisSource";

    /// <summary>Whether a lining follows its axis's source.</summary>
    public static bool IsLinked(Entity e) =>
        Is(e) && e.Attrs[SourceKey] is JsonObject s && s["linked"] is JsonValue v && v.TryGetValue<bool>(out var on) && on;

    /// <summary>What the lining's link says of itself (its stations held at the source's ends, or why it was unlinked); null when nothing.</summary>
    public static string? AxisNote(Entity e) => e.Attrs[SourceKey] is JsonObject s ? (string?)s["note"] : null;

    /// <summary>The source written for a lining made or taken again: linked unless the command says <c>linked: false</c>.</summary>
    static JsonObject Linked(JsonObject source, JsonObject a)
    {
        if (!(a["linked"] is JsonValue v && v.TryGetValue<bool>(out var on) && !on)) source["linked"] = true;
        return source;
    }

    static string N(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>
    /// The axis and red line taken from <paramref name="host"/> between two of its stations, held to its length: the attributes
    /// (its plan geometry cut there, its designed red line as it is or its own fitted to PVIs, a level for an element without one)
    /// and the stations it was cut at.
    /// </summary>
    internal static (JsonObject Attrs, double From, double To) TakeFrom(IModelView m, Entity host, Chain hc, double from, double to, double level, double fallbackZ)
    {
        var s0 = Math.Clamp(from, hc.Start, hc.End);
        var s1 = Math.Clamp(to, hc.Start, hc.End);
        if (s1 < s0) (s0, s1) = (s1, s0);
        if (s1 - s0 < 1) throw new CommandException("The lining runs one metre at least between its two stations.");
        var corridor = CorridorSpec.Default.ToJson();
        corridor["interval"] = 20.0;
        var attrs = new JsonObject();
        var (path, segments) = Clip(hc, s0, s1);
        attrs["path"] = Terrains.ContourJson(path);
        corridor["stationStart"] = s0;
        if (segments != null) corridor["horizontal"] = AlignmentJson.Horizontal(segments);
        // The red line: the host's designed one as it is (its PVIs by station), else its own line under the axis fitted to PVIs.
        var hs = Corridors.Is(host) ? CorridorSpec.Of(host) : null;
        List<Pvi> pvis;
        if (hs is { Mode: "design" } && hs.Pvis.Count >= 2 && VerticalProfile.Problem(hs.Pvis) == null) pvis = [.. hs.Pvis];
        else if (hs != null)
        {
            var red = Corridors.RedLine(m, host, hc);
            pvis = VerticalProfile.FromGround(s0, s1, s => red(s), fallbackZ, 5, 0.02);
        }
        else pvis = [new Pvi(s0, level), new Pvi(s1, level)];
        corridor["profile"] = new JsonObject { ["mode"] = "design", ["ground"] = "proposed", ["pvis"] = new JsonArray([.. pvis.Select(p => (JsonNode)new JsonArray(p.Station, p.Elevation, p.Length))]) };
        attrs["corridor"] = corridor;
        return (attrs, s0, s1);
    }

    /// <summary>The corridor keys that are the lining's axis and red line (what following its source rewrites; the stations ticked and the sections chosen stay).</summary>
    static readonly string[] AxisKeys = ["stationStart", "horizontal", "profile", "curves"];

    /// <summary>The lining's axis and red line as text, to tell whether they changed.</summary>
    static string AxisPrint(Entity e)
    {
        var cor = e.Attrs["corridor"] as JsonObject;
        return string.Join("|", AxisKeys.Select(k => cor?[k]?.ToJsonString() ?? "").Prepend(e.Attrs["path"]?.ToJsonString() ?? ""));
    }

    /// <summary>The link turned off, the lining kept as it is (a copy of its last axis), with why when it was not asked for.</summary>
    static Entity Unlink(ICommandContext ctx, Entity e, string? note)
    {
        var attrs = e.CloneAttrs();
        if (attrs[SourceKey] is JsonObject s)
        {
            s.Remove("linked");
            if (note != null) s["note"] = note; else s.Remove("note");
        }
        var after = e.With(attrs: attrs);
        ctx.Replace(after);
        return after;
    }

    /// <summary>
    /// A linked lining's axis and red line taken again from its source: rewritten when they differ (or its note does), held at the
    /// source's ends with a note; unlinked with a note when the source is gone, has no axis, or is shorter than a metre between them.
    /// Returns whether the lining changed.
    /// </summary>
    internal static bool Follow(ICommandContext ctx, Entity e)
    {
        var m = ctx.Model;
        var src = (JsonObject)e.Attrs[SourceKey]!;
        var host = m.Get((string?)src["id"] ?? "");
        if (host == null)
        {
            Unlink(ctx, e, "The element its axis followed was deleted: the lining keeps its last axis, unlinked.");
            return true;
        }
        if (HostChain(host) is not { } hc)
        {
            Unlink(ctx, e, $"„{host.Name}” has no axis now: the lining keeps its last axis, unlinked.");
            return true;
        }
        var (from, to) = (Json.Number(src["from"]) ?? hc.Start, Json.Number(src["to"]) ?? hc.End);
        var fallback = Corridors.Fallback(m, e);
        JsonObject taken;
        double s0, s1;
        try { (taken, s0, s1) = TakeFrom(m, host, hc, from, to, Json.Number(src["level"]) ?? fallback, fallback); }
        catch (CommandException)
        {
            Unlink(ctx, e, $"„{host.Name}” no longer runs a metre between the lining's stations: the lining keeps its last axis, unlinked.");
            return true;
        }
        var attrs = e.CloneAttrs();
        attrs["path"] = taken["path"]!.DeepClone();
        var cor = attrs["corridor"] as JsonObject ?? [];
        attrs["corridor"] = cor;
        var fresh = (JsonObject)taken["corridor"]!;
        foreach (var k in AxisKeys)
            if (fresh[k] is { } v) cor[k] = v.DeepClone(); else cor.Remove(k);
        var source = (JsonObject)attrs[SourceKey]!;
        var held = Math.Abs(s0 - Math.Min(from, to)) > 1e-6 || Math.Abs(s1 - Math.Max(from, to)) > 1e-6;
        if (held) source["note"] = $"„{host.Name}” is shorter now: the lining runs from {N(s0)} to {N(s1)} m, not {N(Math.Min(from, to))} to {N(Math.Max(from, to))} m as linked.";
        else source.Remove("note");
        var after = e.With(attrs: attrs);
        if (AxisPrint(after) == AxisPrint(e) && AxisNote(after) == AxisNote(e)) return false;
        ctx.Replace(after);
        return true;
    }

    static void RegisterLinks(IModuleRegistry r)
    {
        // A lining unlinked from its axis's source: it keeps its axis as it is, a copy (its source still named, to link it again).
        r.Command("unlinkTunnelLining", "Unlink tunnel lining", (ctx, a) =>
        {
            var e = Spatial.Require(ctx.Model, (string?)a["id"], Class);
            if (!IsLinked(e)) throw new CommandException("The lining is not linked to an axis.");
            Unlink(ctx, e, null);
            return null;
        });
        r.Service<ICommandFollowUp>(new LinkFollowUp());
    }

    /// <summary>
    /// After every command, in its undo step: the linked linings whose source the command changed (or whose ground it changed,
    /// for a source whose red line follows the ground) take their axis again; one whose source was deleted, or whose own axis the
    /// command edited, is unlinked with a note. A lining following another lining follows on, a few deep at most.
    /// </summary>
    sealed class LinkFollowUp : ICommandFollowUp
    {
        public JsonNode? After(ICommandContext ctx, IEdit edit, string command, JsonObject args, JsonNode? result)
        {
            var linked = ctx.Model.OfClass(Class).Where(IsLinked).ToList();
            if (linked.Count == 0) return result;
            var changed = new HashSet<string>();
            var ground = false;
            var own = new Dictionary<string, (Entity Before, Entity After)>();
            foreach (var ch in edit.EntityChanges)
            {
                changed.Add(ch.Id);
                if ((ch.After ?? ch.Before) is { } x && Terrains.IsTerrain(x)) ground = true;
                if (ch.Before is { } b && ch.After is { } a && Is(a)) own[ch.Id] = (b, a);
            }
            var done = new HashSet<string>();
            for (var pass = 0; pass < 4; pass++)
            {
                var moved = new List<string>();
                foreach (var id in linked.Select(l => l.Id))
                {
                    if (done.Contains(id) || ctx.Model.Get(id) is not { } e || !IsLinked(e)) continue;
                    var hostId = (string?)((JsonObject)e.Attrs[SourceKey]!)["id"] ?? "";
                    var host = ctx.Model.Get(hostId);
                    var hostChanged = changed.Contains(hostId) || host == null || (ground && Corridors.Is(host));
                    if (!hostChanged)
                    {
                        // Its axis edited on the lining itself (its red line, a move), its link left as it was: it no longer follows.
                        if (pass == 0 && own.TryGetValue(id, out var o) && IsLinked(o.Before)
                            && o.Before.Attrs[SourceKey]?.ToJsonString() == o.After.Attrs[SourceKey]?.ToJsonString() && AxisPrint(o.Before) != AxisPrint(o.After))
                        {
                            Unlink(ctx, e, "Its axis was edited on the lining itself: it keeps that axis, unlinked.");
                            done.Add(id);
                        }
                        continue;
                    }
                    done.Add(id);
                    if (Follow(ctx, e)) moved.Add(id);
                }
                if (moved.Count == 0) break;
                foreach (var id in moved) changed.Add(id);
            }
            return result;
        }
    }
}
