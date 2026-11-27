using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Lapin.Platform;

/// <summary>One line of the catalogue's revocation list: an extension's versions (or every one, "*") and package SHA-256s it withdraws, and why.</summary>
public sealed record Revocation(string Id, IReadOnlyList<string> Versions, IReadOnlyList<string> Sha256, string Reason, string Date)
{
    /// <summary>Whether it names this version or this package (either is enough).</summary>
    public bool Revokes(string id, string version, string? sha256) =>
        Id == id && (Versions.Any(v => v == "*" || v == version) || (sha256 is { Length: > 0 } && Sha256.Contains(sha256)));

    public JsonObject ToJson() => new() { ["reason"] = Reason, ["date"] = Date };
}

/// <summary>A revocation list as published (lapin-extensions: tools/revoked.mjs): when it was made, and its entries.</summary>
public sealed record RevocationList(DateTimeOffset Generated, IReadOnlyList<Revocation> Entries)
{
    /// <summary>The entry that withdraws this version or package, or null.</summary>
    public Revocation? Match(string id, string version, string? sha256) => Entries.FirstOrDefault(r => r.Revokes(id, version, sha256));
}

/// <summary>
/// The catalogue's revocation list (docs/plan-extension-modules.md §4.3): <c>revoked.json</c> beside the index, signed by the store
/// key like it (<c>revoked.json.sig</c>, <c>lapin-signature</c> v1 over its exact bytes). The interface reads it with the catalogue
/// and hands it to the engine (<c>extensions.revoked</c>), which checks the signature itself and keeps the newest copy it verified,
/// as it came, under <c>&lt;user-dir&gt;/extensions/</c>: a copy older than the one kept is never taken, so a stale mirror cannot bring
/// a withdrawn version back. At each start the loader refuses an installed module the kept copy names, before anything of it is read,
/// and an install of a version it names is refused; offline, the kept copy is the one used.
/// </summary>
public static partial class ExtensionRevocations
{
    public const string Format = "lapin-extensions-revoked";
    public const string File = "revoked.json", SigFile = "revoked.json.sig";
    /// <summary>The kind of problem a revoked module's state carries (<see cref="ExtensionState.ProblemKind"/>).</summary>
    public const string Kind = "revoked";

    [GeneratedRegex(@"^[0-9a-f]{64}$")] private static partial Regex ShaPattern();
    [GeneratedRegex(@"^\d{4}-(0[1-9]|1[0-2])-(0[1-9]|[12]\d|3[01])$")] private static partial Regex DatePattern();

    /// <summary>A list's text read and checked as tools/revoked.mjs checks it, with its <c>generated</c> time required; an <see cref="ExtensionException"/> says what is wrong.</summary>
    public static RevocationList Parse(byte[] bytes)
    {
        JsonObject o;
        try { o = JsonNode.Parse(bytes) as JsonObject ?? throw new ExtensionException("The revocation list is not a JSON object."); }
        catch (System.Text.Json.JsonException) { throw new ExtensionException("The revocation list is not JSON."); }
        static string? Str(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
        if (Str(o["format"]) != Format) throw new ExtensionException($"The revocation list's format is not {Format}.");
        if (o["version"] is not JsonValue ver || !ver.TryGetValue<int>(out var vn) || vn != 1) throw new ExtensionException("The revocation list's version is not 1.");
        if (Str(o["generated"]) is not { } g || !DateTimeOffset.TryParse(g, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var generated))
            throw new ExtensionException("The revocation list has no time it was made (generated).");
        if (o["revoked"] is not JsonArray a) throw new ExtensionException("The revocation list has no entries list.");
        var entries = new List<Revocation>();
        foreach (var n in a)
        {
            if (n is not JsonObject r) throw new ExtensionException("An entry of the revocation list is not an object.");
            var id = Str(r["id"]) ?? "";
            if (!ExtensionPackage.IsId(id)) throw new ExtensionException("An entry of the revocation list names no extension id.");
            List<string>? List(string key, Func<string, bool> ok)
            {
                if (r[key] is null) return null;
                if (r[key] is not JsonArray xs || xs.Count == 0) throw new ExtensionException($"The revocation of „{id}” has a wrong {key}.");
                var o2 = new List<string>();
                foreach (var x in xs) o2.Add(Str(x) is { } s && ok(s) ? s : throw new ExtensionException($"The revocation of „{id}” has a wrong {key}."));
                return o2;
            }
            var versions = List("versions", s => s == "*" || ExtensionPackage.IsVersion(s.Split('+')[0]));
            var shas = List("sha256", s => ShaPattern().IsMatch(s));
            if (versions == null && shas == null) throw new ExtensionException($"The revocation of „{id}” names no version and no SHA-256.");
            var reason = Str(r["reason"]);
            if (string.IsNullOrWhiteSpace(reason) || reason.Length > 500) throw new ExtensionException($"The revocation of „{id}” gives no reason.");
            var date = Str(r["date"]) ?? "";
            if (!DatePattern().IsMatch(date)) throw new ExtensionException($"The revocation of „{id}” has no date.");
            entries.Add(new Revocation(id, versions ?? [], shas ?? [], reason.Trim(), date));
        }
        return new RevocationList(generated, entries);
    }

    /// <summary>A list's bytes checked against its signature file by one of <paramref name="keys"/>, then read; an <see cref="ExtensionException"/> when either does not hold.</summary>
    public static RevocationList Verify(byte[] bytes, byte[]? sigFile, IReadOnlyList<StoreKey> keys)
    {
        if (sigFile is null || sigFile.Length == 0) throw new ExtensionException("The revocation list is not signed.");
        string? keyId = null, value = null;
        try
        {
            if (JsonNode.Parse(sigFile) is JsonObject s && (string?)s["format"] == "lapin-signature" && s["version"] is JsonValue v && v.TryGetValue<int>(out var n) && n == 1)
            {
                keyId = s["keyId"] is JsonValue k && k.TryGetValue<string>(out var ks) ? ks : null;
                value = s["value"] is JsonValue x && x.TryGetValue<string>(out var xs) ? xs : null;
            }
        }
        catch (Exception e) when (e is System.Text.Json.JsonException or InvalidOperationException) { }
        if (!StoreKeys.Verify(keys, keyId, value, bytes)) throw new ExtensionException("The revocation list's signature does not hold: it may have been changed. It was not taken.");
        return Parse(bytes);
    }

    /// <summary>The copy kept under <paramref name="root"/>, verified again; null when there is none, or it no longer holds (it is then ignored, as if none).</summary>
    public static RevocationList? ReadStored(string root, IReadOnlyList<StoreKey> keys)
    {
        var path = Path.Combine(root, File);
        var sig = Path.Combine(root, SigFile);
        try
        {
            if (!System.IO.File.Exists(path) || !System.IO.File.Exists(sig)) return null;
            return Verify(System.IO.File.ReadAllBytes(path), System.IO.File.ReadAllBytes(sig), keys);
        }
        catch (ExtensionException e) { Console.Error.WriteLine($"lapin: the kept revocation list was not used: {e.Message}"); return null; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return null; }
    }

    /// <summary>What <see cref="Accept"/> made of a list: the one now in force, and whether the one given replaced the kept copy.</summary>
    public sealed record Outcome(RevocationList Current, bool Taken, bool Older);

    /// <summary>
    /// A list handed over by the interface: verified with <paramref name="keys"/> (never trusting who handed it), then kept when it is
    /// newer than the kept copy (by <c>generated</c>); an older one is left, the kept copy stays in force. A list that does not verify
    /// throws, and nothing is written.
    /// </summary>
    public static Outcome Accept(string root, IReadOnlyList<StoreKey> keys, byte[] bytes, byte[] sigFile)
    {
        var given = Verify(bytes, sigFile, keys);
        var kept = ReadStored(root, keys);
        if (kept != null && given.Generated < kept.Generated) return new Outcome(kept, false, true);
        if (kept != null && given.Generated == kept.Generated) return new Outcome(kept, false, false);
        Directory.CreateDirectory(root);
        // The signature first: a list left without its own signature by a crash between the two is ignored, never trusted.
        AtomicFile.Write(Path.Combine(root, SigFile), sigFile);
        AtomicFile.Write(Path.Combine(root, File), bytes);
        return new Outcome(given, true, false);
    }

    /// <summary>A list's summary for the interface: when it was made and how many entries it has.</summary>
    public static JsonObject Describe(RevocationList? list) => list == null
        ? new JsonObject { ["generated"] = null, ["count"] = 0 }
        : new JsonObject { ["generated"] = list.Generated.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture), ["count"] = list.Entries.Count };

    /// <summary>The sentence a refused module or install shows: the catalogue withdrew it, and why.</summary>
    public static string Problem(Revocation r) => $"Lapin Extensions withdrew this version on {r.Date}: {r.Reason}";
}
