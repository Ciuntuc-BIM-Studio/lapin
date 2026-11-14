using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>A module's marks for its kinds of part: the prefix a new mark takes (null: not its element), and the marks it showed before the register (kept on the first numbering).</summary>
public interface IMarkKinds
{
    string? PrefixOf(IModelView m, Entity e);
    /// <summary>The marks the module gave its elements before there was a register (a steel members schedule's C1, R2…): element id → mark.</summary>
    IReadOnlyDictionary<string, string>? Seeds(IQueryContext q) => null;
}

/// <summary>
/// A mark of the register: its text (<see cref="Prefix"/> and <see cref="Number"/>), the signature its parts share, the parts that had it
/// when last numbered (none: a free mark, kept until renumbered), and how its parts' frame is turned for their drawings (quarter
/// turns about z, <see cref="Turn"/>; upside down, <see cref="Flip"/>).
/// </summary>
public sealed record MarkEntry(string Mark, string Prefix, int Number, string Sig, List<string> Ids, int Turn = 0, bool Flip = false)
{
    public JsonObject ToJson()
    {
        var o = new JsonObject
        {
            ["mark"] = Mark, ["prefix"] = Prefix, ["number"] = Number, ["sig"] = Sig,
            ["ids"] = new JsonArray(Ids.Select(x => (JsonNode)x).ToArray()),
        };
        if (Turn != 0) o["turn"] = Turn;
        if (Flip) o["flip"] = true;
        return o;
    }

    public static MarkEntry? From(JsonNode? n) => n is JsonObject o && (string?)o["mark"] is { } mark && (string?)o["sig"] is { } sig
        ? new MarkEntry(mark, (string?)o["prefix"] ?? "", (int)(Json.Number(o["number"]) ?? 0), sig, o["ids"] is JsonArray a ? [.. a.Select(x => (string?)x).OfType<string>()] : [],
            (int)(Json.Number(o["turn"]) ?? 0), o["flip"] is JsonValue fv && fv.TryGetValue<bool>(out var f) && f)
        : null;
}

/// <summary>
/// Marks of identical parts (docs/plan-shop-drawings.md §8, decision 583). Two parts are identical when their <b>signature</b> is: their
/// class, materials and finish, and their body in their own frame, its points rounded to 0.5 mm, the same whichever way the part lies
/// (the frame's four proper turns that keep its box, so a copy turned end for end is the same part, a mirrored one not). Grouping is
/// worked out on every ask; the names are kept in the project's <b>register</b> (<c>marks</c> on the project), as GlobalIds are, so a
/// mark on a sheet does not move: <c>marks.number</c> gives a new signature the next number of its prefix, keeps a mark whose parts all
/// changed alike (one part edited stays P12), and gives the edited parts of a mark that has others a new one (modified numbering); a
/// mark whose drawing was issued is frozen: its parts edited take a new mark. A mark with no part left is kept, free, until
/// <c>marks.renumber</c>. The steel members schedule's marks are the register's first numbers. Nothing of it is written into an element.
/// </summary>
public static class Marks
{
    public const string Key = "marks";
    /// <summary>The prefix of an element no module names: E1, E2…</summary>
    public const string DefaultPrefix = "E";
    /// <summary>The grid the signature rounds a part's points to (m).</summary>
    public const double Quantum = 0.0005;

    public static List<MarkEntry> Register(IModelView m) =>
        (Spatial.Project(m)?.Attrs[Key]?["entries"] as JsonArray)?.Select(MarkEntry.From).OfType<MarkEntry>().ToList() ?? [];

    static void Write(ICommandContext ctx, List<MarkEntry> entries)
    {
        var project = Spatial.Project(ctx.Model) ?? throw new CommandException("The model has no project.");
        var attrs = project.CloneAttrs();
        attrs[Key] = new JsonObject { ["entries"] = new JsonArray(entries.Select(x => (JsonNode)x.ToJson()).ToArray()) };
        ctx.Replace(project.With(attrs: attrs));
    }

    /// <summary>The register read once per project as it is (entities do not change; an edit makes a new one): element id → entry.</summary>
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Entity, Dictionary<string, MarkEntry>> byProject = [];

    /// <summary>The register's entry an element had when last numbered; null when it has none.</summary>
    public static MarkEntry? EntryOf(IModelView m, string id)
    {
        if (Spatial.Project(m) is not { } project || project.Attrs[Key] is null) return null;
        var map = byProject.GetValue(project, p =>
        {
            var d = new Dictionary<string, MarkEntry>();
            foreach (var x in Register(m)) foreach (var i in x.Ids) d.TryAdd(i, x);
            return d;
        });
        return map.GetValueOrDefault(id);
    }

    /// <summary>The part's mark, null when it was never numbered.</summary>
    public static string? MarkOf(IModelView m, Entity e) => EntryOf(m, e.Id)?.Mark;
    public static string? MarkOf(IQueryContext q, Entity e) => MarkOf(q.Model, e);

    /// <summary>How many parts have the element's mark (one without a mark).</summary>
    public static int CountOf(IModelView m, Entity e) => EntryOf(m, e.Id)?.Ids.Count(id => m.Get(id) != null) is > 0 and var n ? n : 1;
    public static int CountOf(IQueryContext q, Entity e) => CountOf(q.Model, e);

    /// <summary>The prefix a new mark of the element takes: its module's, else <see cref="DefaultPrefix"/>.</summary>
    public static string PrefixOf(IModelView m, IServices s, Entity e) =>
        s.All<IMarkKinds>().Select(k => k.PrefixOf(m, e)).FirstOrDefault(x => x != null) ?? DefaultPrefix;

    /// <summary>
    /// The element's signature: its class, materials, finish, and its body in its own frame (unturned) rounded to 0.5 mm, the least of
    /// the four ways its box can be turned onto itself; null when it has no body.
    /// </summary>
    public static string? Signature(IQueryContext q, Entity e)
    {
        var g = q.Geometry(e.Id);
        if (g == null) return null;
        var pts = g.Parts.SelectMany(p => p.Solid.Vertices).ToList();
        if (ElementFrames.Base(q.Model, q.Services, e, pts) is not { } f) return null;
        var c = f.Centre;
        var local = pts.Select(p => f.Local(p) - c).ToList();
        string Key(Func<Vec3, Vec3> turn)
        {
            var set = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var p in local)
            {
                var t = turn(p);
                set.Add($"{Math.Round(t.X / Quantum)},{Math.Round(t.Y / Quantum)},{Math.Round(t.Z / Quantum)}");
            }
            return string.Join(";", set);
        }
        var shape = new[]
        {
            Key(p => p), Key(p => new Vec3(-p.X, -p.Y, p.Z)), Key(p => new Vec3(p.X, -p.Y, -p.Z)), Key(p => new Vec3(-p.X, p.Y, -p.Z)),
        }.Min(StringComparer.Ordinal)!;
        var materials = string.Join(",", g.Parts.Select(p => p.Material is { } id ? q.Model.Get(id)?.Name ?? id : "").Distinct().Order(StringComparer.Ordinal));
        var head = $"{e.Str("exportAs") ?? e.Class}|{materials}|{e.Str("finish") ?? ""}|";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(head + shape)))[..20].ToLowerInvariant();
    }

    /// <summary>The parts a numbering takes when none are named: the modules' kinds (steel parts) and every element with a shop drawing.</summary>
    static List<Entity> Candidates(IModelView m, IServices s)
    {
        var drawn = m.OfClass(ShopDrawings.Class).Select(d => d.Str("element")).OfType<string>().ToHashSet();
        var kinds = s.All<IMarkKinds>();
        return [.. m.Entities.Where(e => drawn.Contains(e.Id) || (Spatial.StoreyOf(m, e.Id) != null && kinds.Any(k => k.PrefixOf(m, e) != null)))];
    }

    /// <summary>Whether an entry's drawing went out in an issue (its sheet in a CDE issue): then its signature is frozen.</summary>
    static bool Frozen(IModelView m, MarkEntry entry) =>
        m.OfClass(ShopDrawings.Class).Any(d => d.Str("element") is { } el && entry.Ids.Contains(el) && d.Str("sheet") is { } sheet && Cde.CdeIssues.LastOf(m, sheet) != null);

    static (string Prefix, int Number) Split(string mark)
    {
        var k = mark.Length;
        while (k > 0 && char.IsDigit(mark[k - 1])) k--;
        return (mark[..k], k < mark.Length && int.TryParse(mark[k..], out var n) ? n : 0);
    }

    /// <summary>
    /// Numbers the parts (<paramref name="only"/>, else every candidate), in the command's step: each signature its mark as the register
    /// has it, a new one the next of its prefix (the biggest parts first), modified numbering for the parts that changed. The register after.
    /// </summary>
    public static List<MarkEntry> Number(ICommandContext ctx, IEnumerable<Entity>? only = null)
    {
        var m = ctx.Model;
        var q = ctx.Query;
        var reg = Register(m);
        var parts = (only ?? Candidates(m, ctx.Services)).DistinctBy(e => e.Id)
            .Select(e => (E: e, Sig: Signature(q, e), Prefix: PrefixOf(m, ctx.Services, e))).Where(x => x.Sig != null).ToList();
        if (parts.Count == 0) return reg;
        // The first numbering: the marks the modules showed before (the steel members schedule's), kept.
        if (reg.Count == 0)
            foreach (var kind in ctx.Services.All<IMarkKinds>())
                if (kind.Seeds(q) is { Count: > 0 } seeds)
                    foreach (var g in parts.Where(p => seeds.ContainsKey(p.E.Id)).GroupBy(p => p.Sig!).OrderBy(g => seeds[g.First().E.Id], StringComparer.Ordinal))
                    {
                        var mark = seeds[g.First().E.Id];
                        // A mark is the project's, one signature each: a schedule's mark that two different parts shared (one per hall) goes to the first.
                        if (reg.Any(x => x.Sig == g.Key || x.Mark == mark)) continue;
                        var (p, n) = Split(mark);
                        reg.Add(new MarkEntry(mark, p, n, g.Key, [.. g.Select(x => x.E.Id)]));
                    }
        var current = parts.GroupBy(p => p.Sig!).ToDictionary(g => g.Key, g => g.ToList());
        // The biggest parts first among the new ones: by their prefix, then their size, then their signature.
        double Size(Entity e) => ElementFrames.Of(q, e) is { } f ? f.Length * 1e6 + f.Width * 1e3 + f.Height : 0;
        foreach (var (sig, group) in current.OrderBy(kv => kv.Value[0].Prefix, StringComparer.Ordinal).ThenByDescending(kv => Size(kv.Value[0].E)).ThenBy(kv => kv.Key, StringComparer.Ordinal))
        {
            var ids = group.Select(x => x.E.Id).ToList();
            var idSet = ids.ToHashSet();
            var at = reg.FindIndex(x => x.Sig == sig);
            if (at < 0)
            {
                // Modified numbering: the mark these parts had keeps going with them when they are all its parts and it is not frozen.
                var olds = reg.Select((x, i) => (x, i)).Where(t => t.x.Ids.Any(idSet.Contains)).ToList();
                if (olds.Count == 1 && olds[0].x.Ids.Where(id => m.Get(id) != null).All(idSet.Contains) && !current.ContainsKey(olds[0].x.Sig) && !Frozen(m, olds[0].x))
                {
                    at = olds[0].i;
                    reg[at] = reg[at] with { Sig = sig };
                }
                else
                {
                    var prefix = group[0].Prefix;
                    var next = reg.Where(x => x.Prefix == prefix).Select(x => x.Number).DefaultIfEmpty(0).Max() + 1;
                    reg.Add(new MarkEntry($"{prefix}{next}", prefix, next, sig, []));
                    at = reg.Count - 1;
                }
            }
            // The parts leave the marks they had; this mark is theirs (and, numbering a few, the others of it still standing keep it).
            for (var i = 0; i < reg.Count; i++)
                if (i != at && reg[i].Ids.Any(idSet.Contains)) reg[i] = reg[i] with { Ids = [.. reg[i].Ids.Where(id => !idSet.Contains(id))] };
            var keep = only != null ? reg[at].Ids.Where(id => !idSet.Contains(id) && m.Get(id) != null).ToList() : [];
            reg[at] = reg[at] with { Ids = [.. keep, .. ids] };
        }
        // A part deleted leaves its mark.
        for (var i = 0; i < reg.Count; i++) reg[i] = reg[i] with { Ids = [.. reg[i].Ids.Where(id => m.Get(id) != null)] };
        Write(ctx, reg);
        return reg;
    }

    /// <summary>Renumbers a prefix (null: every one) from 1, the biggest parts first, frozen marks kept, the free marks dropped. One step.</summary>
    public static List<MarkEntry> Renumber(ICommandContext ctx, string? prefix)
    {
        var m = ctx.Model;
        var reg = Register(m);
        var o = new List<MarkEntry>();
        foreach (var g in reg.GroupBy(x => x.Prefix))
        {
            if (prefix != null && g.Key != prefix) { o.AddRange(g); continue; }
            var live = g.Where(x => x.Ids.Count > 0).ToList();
            var frozen = live.Where(x => Frozen(m, x)).ToList();
            var taken = frozen.Select(x => x.Number).ToHashSet();
            var n = 0;
            o.AddRange(frozen);
            foreach (var x in live.Except(frozen).OrderByDescending(x => m.Get(x.Ids[0]) is { } e && ElementFrames.Of(ctx.Query, e) is { } f ? f.Length : 0).ThenBy(x => x.Number))
            {
                do n++; while (taken.Contains(n));
                o.Add(x with { Mark = $"{g.Key}{n}", Number = n });
            }
        }
        Write(ctx, o);
        return o;
    }

    public static JsonObject ToJson(IModelView m, List<MarkEntry> reg) => new()
    {
        ["entries"] = new JsonArray(reg.Select(x =>
        {
            var o = x.ToJson();
            o["count"] = x.Ids.Count(id => m.Get(id) != null);
            o["frozen"] = Frozen(m, x);
            return (JsonNode)o;
        }).ToArray()),
    };

    public static void Register(IModuleRegistry r)
    {
        // The parts numbered: {ids?} (none: the steel parts and every part with a drawing) → the register.
        r.Command("marks.number", "Number parts", (ctx, a) =>
        {
            var ids = Args.Ids(a).ToList();
            var only = ids.Count > 0 ? ids.Select(id => ctx.Model.Get(id) ?? throw new CommandException("There is no such element.")).ToList() : null;
            return ToJson(ctx.Model, Number(ctx, only));
        });

        // Every mark of a prefix (none: all) numbered again from 1, free marks dropped; frozen marks kept. Not for a shared session's guests.
        r.Command("marks.renumber", "Renumber marks", (ctx, a) => ToJson(ctx.Model, Renumber(ctx, (string?)a["prefix"] is { Length: > 0 } p ? p : null)));

        // A mark's parts' frame turned for their drawings: {id (a part of the mark), turn? (quarter turns about its z: which side is the
        // front), flip? (upside down)}. Kept in the register, never on the part (an IFC reference is never written to).
        r.Command("marks.setFrame", "Turn part", (ctx, a) =>
        {
            var e = ctx.Model.Get((string?)a["id"] ?? "") ?? throw new CommandException("Which part?");
            var reg = Register(ctx.Model);
            if (!reg.Any(x => x.Ids.Contains(e.Id))) reg = Number(ctx, [e]);
            var i = reg.FindIndex(x => x.Ids.Contains(e.Id));
            if (i < 0) throw new CommandException($"„{e.Name}” has nothing to draw.");
            var turn = Json.Number(a["turn"]) is { } t ? (((int)Math.Round(t)) % 4 + 4) % 4 : reg[i].Turn;
            var flip = a["flip"] is JsonValue fv && fv.TryGetValue<bool>(out var f) ? f : reg[i].Flip;
            reg[i] = reg[i] with { Turn = turn, Flip = flip };
            Write(ctx, reg);
            return null;
        });

        // The register with each mark's count and whether it is frozen: {} → {entries}.
        r.Query("marks.list", (q, _) => ToJson(q.Model, Register(q.Model)));
    }
}
