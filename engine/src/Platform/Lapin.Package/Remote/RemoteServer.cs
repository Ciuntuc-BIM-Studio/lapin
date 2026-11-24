using System.Text.Json.Nodes;

namespace Lapin.Package.Remote;

/// <summary>
/// The tokens of one sign-in (§9.2): held in memory for the call that uses them, handed in by the interface (which keeps the
/// refresh token in the system's keychain) and handed back when a refresh replaced them. Never written by the engine.
/// </summary>
public sealed class RemoteTokens
{
    public string? Access { get; set; }
    public string? Refresh { get; set; }
    /// <summary>Whether a refresh replaced them during the call: the interface stores the new refresh token at once (it is single-use).</summary>
    public bool Changed { get; set; }
}

/// <summary>An answer of the server's other APIs (<c>HttpRemote.ApiAsync</c>, Lapin.Package.Http): its status, its JSON, its ETag, its bytes.</summary>
public sealed record ApiAnswer(int Status, JsonNode? Json, string? ETag, byte[]? Bytes)
{
    public bool Ok => Status is >= 200 and < 300;
    /// <summary>The refusal's stable code (Atrium's <c>{code, message}</c>), else one made of the status.</summary>
    public string Code => (Json as JsonObject)?["code"] is JsonValue v && v.TryGetValue<string>(out var c) ? c
        : Status switch { 401 => "unauthorized", 403 => "forbidden", 404 => "not_found", 409 => "conflict", 412 => "changed", _ => "error" };
    public string Message => (Json as JsonObject)?["message"] is JsonValue v && v.TryGetValue<string>(out var m) ? m : $"The server answered {Status}.";
}

/// <summary>A server's capabilities (§10.1): its limits and its features.</summary>
public sealed record RemoteCapabilities(string Version, long MaxObject, long Chunk, int MaxBatch, long MaxPack, int MaxCommitsPage, IReadOnlyList<string> Features, string Foundation)
{
    public static RemoteCapabilities From(JsonObject o)
    {
        var l = o["limits"] as JsonObject ?? [];
        return new RemoteCapabilities((string?)o["version"] ?? "1.0",
            (long?)l["maxObject"] ?? 2147483648L, (long?)l["chunk"] ?? 16777216L, (int?)l["maxBatch"] ?? 10_000, (long?)l["maxPack"] ?? 4294967296L,
            (int?)l["maxCommitsPage"] ?? 1000, (o["features"] as JsonArray)?.Select(x => (string?)x).OfType<string>().ToList() ?? [],
            (string?)o["auth"]?["foundation"] ?? "/foundation/1.1");
    }
    public bool Has(string feature) => Features.Contains(feature, StringComparer.Ordinal);
}
