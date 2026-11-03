namespace Lapin.Platform;

/// <summary>
/// MD5 (RFC 1321) of Lapin's own, for the ids derived from a seed (stable
/// GlobalIds): .NET in the browser has no MD5, and the ids must come out the
/// same in both, so they never change between an export from the desktop and
/// one from the web. Not for security: only to spread a seed over 128 bits.
/// </summary>
public static class Md5
{
    static readonly int[] S =
    [
        7, 12, 17, 22, 7, 12, 17, 22, 7, 12, 17, 22, 7, 12, 17, 22,
        5, 9, 14, 20, 5, 9, 14, 20, 5, 9, 14, 20, 5, 9, 14, 20,
        4, 11, 16, 23, 4, 11, 16, 23, 4, 11, 16, 23, 4, 11, 16, 23,
        6, 10, 15, 21, 6, 10, 15, 21, 6, 10, 15, 21, 6, 10, 15, 21,
    ];

    /// <summary>K[i] = floor(|sin(i + 1)| · 2³²).</summary>
    static readonly uint[] K = Enumerable.Range(0, 64).Select(i => (uint)(long)Math.Floor(Math.Abs(Math.Sin(i + 1)) * 4294967296.0)).ToArray();

    public static byte[] HashData(ReadOnlySpan<byte> data)
    {
        // The message padded: a 1 bit, zeros to 56 mod 64 bytes, its length in bits as 64 bits little-endian.
        var length = ((data.Length + 8) / 64 + 1) * 64;
        var m = new byte[length];
        data.CopyTo(m);
        m[data.Length] = 0x80;
        BitConverter.TryWriteBytes(m.AsSpan(length - 8), (ulong)data.Length * 8);
        if (!BitConverter.IsLittleEndian) m.AsSpan(length - 8).Reverse();

        uint a0 = 0x67452301, b0 = 0xefcdab89, c0 = 0x98badcfe, d0 = 0x10325476;
        var w = new uint[16];
        for (var chunk = 0; chunk < length; chunk += 64)
        {
            for (var j = 0; j < 16; j++) w[j] = (uint)(m[chunk + 4 * j] | m[chunk + 4 * j + 1] << 8 | m[chunk + 4 * j + 2] << 16 | m[chunk + 4 * j + 3] << 24);
            uint a = a0, b = b0, c = c0, d = d0;
            for (var i = 0; i < 64; i++)
            {
                uint f;
                int g;
                switch (i / 16)
                {
                    case 0: f = (b & c) | (~b & d); g = i; break;
                    case 1: f = (d & b) | (~d & c); g = (5 * i + 1) % 16; break;
                    case 2: f = b ^ c ^ d; g = (3 * i + 5) % 16; break;
                    default: f = c ^ (b | ~d); g = 7 * i % 16; break;
                }
                f = f + a + K[i] + w[g];
                a = d; d = c; c = b;
                b += f << S[i] | f >> (32 - S[i]);
            }
            a0 += a; b0 += b; c0 += c; d0 += d;
        }
        var digest = new byte[16];
        BitConverter.TryWriteBytes(digest.AsSpan(0), a0);
        BitConverter.TryWriteBytes(digest.AsSpan(4), b0);
        BitConverter.TryWriteBytes(digest.AsSpan(8), c0);
        BitConverter.TryWriteBytes(digest.AsSpan(12), d0);
        if (!BitConverter.IsLittleEndian) for (var k = 0; k < 16; k += 4) digest.AsSpan(k, 4).Reverse();
        return digest;
    }
}
