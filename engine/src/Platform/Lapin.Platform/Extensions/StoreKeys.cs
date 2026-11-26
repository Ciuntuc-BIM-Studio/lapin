using System.Security.Cryptography;
using System.Text;

namespace Lapin.Platform;

/// <summary>A Lapin Extensions store key: Ed25519, 32 raw bytes in base64, and its id (the first 16 hex digits of their SHA-256).</summary>
public sealed record StoreKey(string Id, string PublicKey)
{
    /// <summary>The id a key's bytes have.</summary>
    public static string IdOf(byte[] raw) => Convert.ToHexStringLower(SHA256.HashData(raw))[..16];
}

/// <summary>
/// The engine's copy of the store keys Lapin trusts (docs/plan-extension-modules.md §2.1): the same list as the interface's
/// <c>ui/src/platform/extensions/keys.ts</c>, kept equal by a test (ExtensionLoaderTests). The engine checks an installed extension
/// module's signature with them at every start. To rotate a key, change both lists.
/// </summary>
public static class StoreKeys
{
    public static readonly IReadOnlyList<StoreKey> All =
    [
        // Made 12.11.2026 (scripts/store-key.mjs); the catalogue at lapinbim.com/extensions/ is signed with it from that day.
        new("2daeb4d5dc6eae92", "H1oExDrtV+/4r0vlqZNSzlKWIKEmlvNiHSj91aKSSaQ="),
    ];

    /// <summary>The text a package's signature is over: its id, version, SHA-256 and size (signature.ts, packageMessage).</summary>
    public static byte[] PackageMessage(string id, string version, string sha256, long size) =>
        Encoding.UTF8.GetBytes($"lapin-extension-package\n1\n{id}\n{version}\n{sha256}\n{size}\n");

    /// <summary>Whether a signature {keyId, value} is by one of <paramref name="keys"/> over <paramref name="message"/>.</summary>
    public static bool Verify(IEnumerable<StoreKey> keys, string? keyId, string? value, byte[] message)
    {
        if (keyId is null || value is null || keyId.Length != 16 || value.Length != 88) return false;
        var key = keys.FirstOrDefault(k => k.Id == keyId);
        if (key == null) return false;
        try
        {
            var raw = Convert.FromBase64String(key.PublicKey);
            // A key listed under another key's id is no key.
            if (StoreKey.IdOf(raw) != key.Id) return false;
            return Ed25519.Verify(raw, Convert.FromBase64String(value), message);
        }
        catch (FormatException) { return false; }
    }
}
