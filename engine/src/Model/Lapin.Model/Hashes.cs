using System.Security.Cryptography;
using System.Text;

namespace Lapin.Model;

/// <summary>
/// SHA-256 as lowercase hex, the one spelling every manifest, issue, recovery file and trust list compares (audit
/// 22.11.2026: seven copies of it before).
/// </summary>
public static class Hashes
{
    public static string Sha256Hex(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    /// <summary>The text's UTF-8 bytes hashed.</summary>
    public static string Sha256Hex(string text) => Sha256Hex(Encoding.UTF8.GetBytes(text));

    /// <summary>
    /// A script's code as its hash (its UTF-8, lowercase hex): what the trust list keeps, what a project's script records and
    /// what the host checks before it runs one again (audit 22.11.2026: three copies, any of which changed alone would have
    /// broken the check without a word).
    /// </summary>
    public static string Script(string code) => Sha256Hex(code);

    /// <summary>A stream hashed to its end.</summary>
    public static string Sha256Hex(Stream stream) => Convert.ToHexStringLower(SHA256.HashData(stream));
}
