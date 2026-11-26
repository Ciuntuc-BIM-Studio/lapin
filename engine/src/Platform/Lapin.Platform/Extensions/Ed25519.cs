using System.Numerics;
using System.Security.Cryptography;

namespace Lapin.Platform;

/// <summary>
/// Ed25519 (RFC 8032 §5.1), for the store's signatures (docs/plan-extension-modules.md §2.1): .NET 10 has no Ed25519 of its own
/// (only inside Composite ML-DSA), so this is a port of the interface's verifier (ui/src/platform/extensions/signature.ts), checked
/// against the same RFC vectors. Only public data goes through <see cref="Verify"/>, so it need not be constant-time.
/// <see cref="Sign"/> exists for the tests' throwaway keys: the engine never signs anything.
/// </summary>
public static class Ed25519
{
    static readonly BigInteger P = BigInteger.Pow(2, 255) - 19;
    static readonly BigInteger L = BigInteger.Pow(2, 252) + BigInteger.Parse("27742317777372353535851937790883648493");
    static readonly BigInteger D = Mod(-121665 * Inv(121666));
    static readonly BigInteger D2 = Mod(2 * D);
    static readonly BigInteger SqrtM1 = BigInteger.ModPow(2, (P - 1) / 4, P);
    static readonly Point Identity = new(0, 1, 1, 0);
    static readonly Point Base = DecodePoint(EncodeInt(Mod(4 * Inv(5))))!.Value;

    /// <summary>A point in extended coordinates: x = X/Z, y = Y/Z, xy = T/Z.</summary>
    readonly record struct Point(BigInteger X, BigInteger Y, BigInteger Z, BigInteger T);

    static BigInteger Mod(BigInteger a, BigInteger? m = null)
    {
        var mm = m ?? P;
        var r = a % mm;
        return r.Sign < 0 ? r + mm : r;
    }

    static BigInteger Inv(BigInteger a) => BigInteger.ModPow(Mod(a), P - 2, P);

    /// <summary>Addition on the twisted Edwards curve (a = -1), complete: it doubles too (RFC 8032 §5.1.4).</summary>
    static Point Add(Point p, Point q)
    {
        var a = Mod((p.Y - p.X) * (q.Y - q.X));
        var b = Mod((p.Y + p.X) * (q.Y + q.X));
        var c = Mod(p.T * D2 * q.T);
        var d = Mod(p.Z * 2 * q.Z);
        BigInteger e = b - a, f = d - c, g = d + c, h = b + a;
        return new Point(Mod(e * f), Mod(g * h), Mod(f * g), Mod(e * h));
    }

    static Point Times(Point p, BigInteger n)
    {
        var r = Identity;
        var q = p;
        while (n.Sign > 0)
        {
            if (!n.IsEven) r = Add(r, q);
            q = Add(q, q);
            n >>= 1;
        }
        return r;
    }

    static BigInteger LittleEndian(ReadOnlySpan<byte> b) => new(b, isUnsigned: true, isBigEndian: false);

    static byte[] EncodeInt(BigInteger n)
    {
        var o = new byte[32];
        n.TryWriteBytes(o, out _, isUnsigned: true, isBigEndian: false);
        return o;
    }

    /// <summary>A point from its 32 bytes (RFC 8032 §5.1.3); null when the bytes are no point.</summary>
    static Point? DecodePoint(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != 32) return null;
        var b = bytes.ToArray();
        var sign = (b[31] >> 7) & 1;
        b[31] &= 0x7f;
        var y = LittleEndian(b);
        if (y >= P) return null;
        var y2 = y * y % P;
        var u = Mod(y2 - 1);
        var v = Mod(D * y2 + 1);
        var v3 = v * v % P * v % P;
        var v7 = v3 * v3 % P * v % P;
        var x = Mod(u * v3 % P * BigInteger.ModPow(u * v7 % P, (P - 5) / 8, P));
        var vx2 = Mod(v * x % P * x);
        if (vx2 != u)
        {
            if (vx2 != Mod(-u)) return null;
            x = x * SqrtM1 % P;
        }
        if (x.IsZero && sign == 1) return null;
        if ((int)(x & 1) != sign) x = P - x;
        return new Point(x, y, 1, x * y % P);
    }

    static byte[] EncodePoint(Point p)
    {
        var zi = Inv(p.Z);
        var x = Mod(p.X * zi);
        var y = Mod(p.Y * zi);
        var o = EncodeInt(y);
        if (!x.IsEven) o[31] |= 0x80;
        return o;
    }

    static bool Same(Point p, Point q) => Mod(p.X * q.Z - q.X * p.Z).IsZero && Mod(p.Y * q.Z - q.Y * p.Z).IsZero;

    static BigInteger HashModL(params byte[][] parts)
    {
        using var h = IncrementalHash.CreateHash(HashAlgorithmName.SHA512);
        foreach (var x in parts) h.AppendData(x);
        return Mod(LittleEndian(h.GetHashAndReset()), L);
    }

    /// <summary>Whether <paramref name="signature"/> (64 bytes) is <paramref name="publicKey"/>'s (32 bytes) over <paramref name="message"/>: [S]B = R + [k]A.</summary>
    public static bool Verify(ReadOnlySpan<byte> publicKey, ReadOnlySpan<byte> signature, ReadOnlySpan<byte> message)
    {
        if (publicKey.Length != 32 || signature.Length != 64) return false;
        var a = DecodePoint(publicKey);
        var r = DecodePoint(signature[..32]);
        var s = LittleEndian(signature[32..]);
        if (a == null || r == null || s >= L) return false;
        var k = HashModL(signature[..32].ToArray(), publicKey.ToArray(), message.ToArray());
        return Same(Times(Base, s), Add(r.Value, Times(a.Value, k)));
    }

    static (BigInteger Scalar, byte[] Prefix) Expand(ReadOnlySpan<byte> seed)
    {
        if (seed.Length != 32) throw new ArgumentException("An Ed25519 secret key is 32 bytes.");
        var h = SHA512.HashData(seed);
        h[0] &= 248;
        h[31] &= 127;
        h[31] |= 64;
        return (LittleEndian(h.AsSpan(0, 32)), h[32..]);
    }

    /// <summary>The public key of a 32-byte secret key (tests only).</summary>
    public static byte[] PublicKey(ReadOnlySpan<byte> seed) => EncodePoint(Times(Base, Expand(seed).Scalar));

    /// <summary>A signature by a 32-byte secret key (tests only: the engine never signs).</summary>
    public static byte[] Sign(ReadOnlySpan<byte> seed, ReadOnlySpan<byte> message)
    {
        var (a, prefix) = Expand(seed);
        var pub = EncodePoint(Times(Base, a));
        var m = message.ToArray();
        var r = HashModL(prefix, m);
        var rb = EncodePoint(Times(Base, r));
        var k = HashModL(rb, pub, m);
        var s = Mod(r + k * a, L);
        return [.. rb, .. EncodeInt(s)];
    }
}
