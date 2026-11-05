using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Clash;

/// <summary>
/// A clash as a test keeps it: its number (the same from run to run), the pair (set A's element first) and their key,
/// where, how deep or how far, whether they cross, its status, who has it and the notes, when it was found, last seen
/// and resolved, and the remark (BCF topic) made of it.
/// </summary>
public sealed record ClashRecord(int Number, string Key, string A, string B, Vec3 At, double Distance, bool Crossing, string Status,
    string Found, string Seen, string? Resolved = null, string? AssignedTo = null, string? Notes = null, string? Remark = null)
{
    public JsonObject ToJson()
    {
        var o = new JsonObject
        {
            ["n"] = Number, ["key"] = Key, ["a"] = A, ["b"] = B,
            ["at"] = new JsonArray(Math.Round(At.X, 4), Math.Round(At.Y, 4), Math.Round(At.Z, 4)),
            ["d"] = Math.Round(Distance, 5), ["crossing"] = Crossing, ["status"] = Status, ["found"] = Found, ["seen"] = Seen,
        };
        if (Resolved != null) o["resolved"] = Resolved;
        if (AssignedTo != null) o["assignedTo"] = AssignedTo;
        if (Notes != null) o["notes"] = Notes;
        if (Remark != null) o["remark"] = Remark;
        return o;
    }

    public static ClashRecord? From(JsonNode? n)
    {
        if (n is not JsonObject o || (string?)o["key"] is not { } key || (string?)o["a"] is not { } a || (string?)o["b"] is not { } b) return null;
        var at = o["at"] as JsonArray;
        return new((int)(Json.Number(o["n"]) ?? 0), key, a, b, at is { Count: 3 } ? new(Json.Number(at[0]) ?? 0, Json.Number(at[1]) ?? 0, Json.Number(at[2]) ?? 0) : Vec3.Zero,
            Json.Number(o["d"]) ?? 0, o["crossing"] is JsonValue cv && cv.TryGetValue<bool>(out var c) && c, (string?)o["status"] ?? ClashTests.New,
            (string?)o["found"] ?? "", (string?)o["seen"] ?? "", (string?)o["resolved"], (string?)o["assignedTo"], (string?)o["notes"], (string?)o["remark"]);
    }
}

/// <summary>
/// Clash tests (docs/plan-visualisation.md, stage 2): each a <see cref="Class"/> entity, a resource of the project
/// keeping its sets (A, and B or none for A against itself), its kind (hard or clearance), its tolerance, whether Run all
/// takes it, its clashes and its runs. A run merges what it finds with what the test had, after BimGo's ClashMerge (MIT-0):
/// a pair is followed by its key (the test's two element ids in order); found again, a new clash turns active and a
/// resolved one opens again, reviewed and approved stay; what a complete run no longer finds is resolved (a run cut short
/// at the limit resolves nothing); the number, who has it, the notes and the remark always stay.
/// </summary>
public static class ClashTests
{
    public const string Class = "LapinClashTest";
    public const string Hard = "hard", Clearance = "clearance";
    /// <summary>The openings check (decisions 289 and 293) as a test: MEP runs crossing walls, floors and roofs without an opening big enough; no sets.</summary>
    public const string Openings = "openings";
    public static readonly string[] Kinds = [Hard, Clearance, Openings];
    public const string New = "new", Active = "active", Reviewed = "reviewed", Approved = "approved", Resolved = "resolved";
    public static readonly string[] Statuses = [New, Active, Reviewed, Approved, Resolved];
    /// <summary>The runs a test remembers.</summary>
    public const int MaxRuns = 200;

    public sealed record Test(string Id, string Name, string Kind, string SetA, string? SetB, double Tolerance, bool Enabled, IReadOnlyList<ClashRecord> Clashes, JsonObject? LastRun)
    {
        public bool IsSelf => SetB == null || SetB == SetA;
    }

    public static Test Read(Entity e) => new(e.Id, e.Name ?? "", (string?)e.Attrs["kind"] is Clearance or Openings ? (string)e.Attrs["kind"]! : Hard, e.Str("setA") ?? "", e.Str("setB") is { Length: > 0 } b ? b : null,
        Json.Number(e.Attrs["tolerance"]) ?? ClashEngine.HardTolerance, e.Attrs["enabled"] is not JsonValue v || !v.TryGetValue<bool>(out var on) || on,
        (e.Attrs["clashes"] as JsonArray ?? []).Select(ClashRecord.From).OfType<ClashRecord>().ToList(), (e.Attrs["runs"] as JsonArray)?.LastOrDefault() as JsonObject);

    public static string KeyOf(string a, string b) => string.CompareOrdinal(a, b) <= 0 ? $"{a}|{b}" : $"{b}|{a}";

    public static string Now() => DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

    /// <summary>A run's clashes merged into the test's (see the class); the merged list, and how many are new, reopened and newly resolved.</summary>
    public static (List<ClashRecord> Clashes, int New, int Reopened, int Resolved) Merge(IReadOnlyList<ClashRecord> before, IReadOnlyList<(string A, string B, Vec3 At, double Distance, bool Crossing)> found, bool complete, string now)
    {
        var old = before.GroupBy(c => c.Key).ToDictionary(g => g.Key, g => g.First());
        var next = before.Count == 0 ? 1 : before.Max(c => c.Number) + 1;
        var list = new List<ClashRecord>();
        var seen = new HashSet<string>();
        int added = 0, reopened = 0, resolved = 0;
        foreach (var f in found)
        {
            var key = KeyOf(f.A, f.B);
            if (!seen.Add(key)) continue;
            if (old.TryGetValue(key, out var prev))
            {
                var status = prev.Status switch { New => Active, Resolved => Active, var s => s };
                if (prev.Status == Resolved) reopened++;
                list.Add(prev with { A = f.A, B = f.B, At = f.At, Distance = f.Distance, Crossing = f.Crossing, Status = status, Seen = now, Resolved = null });
            }
            else
            {
                added++;
                list.Add(new ClashRecord(next++, key, f.A, f.B, f.At, f.Distance, f.Crossing, New, now, now));
            }
        }
        foreach (var prev in before.Where(p => !seen.Contains(p.Key)))
        {
            if (complete && prev.Status != Resolved) { resolved++; list.Add(prev with { Status = Resolved, Resolved = now }); }
            else list.Add(prev);
        }
        list.Sort((x, y) => x.Number.CompareTo(y.Number));
        return (list, added, reopened, resolved);
    }

    /// <summary>The counts of a test's clashes by status.</summary>
    public static JsonObject Counts(IEnumerable<ClashRecord> clashes)
    {
        var o = new JsonObject();
        foreach (var s in Statuses) o[s] = 0;
        foreach (var c in clashes) o[c.Status] = ((int?)o[c.Status] ?? 0) + 1;
        return o;
    }
}
