using System.Buffers.Binary;
using System.Text;
using Lapin.PointClouds;

namespace Lapin.Engine.Tests;

/// <summary>
/// Lapin's Zstandard decoder (RFC 8878; SPZ version 4's streams, docs/plan-gaussian-splats.md G3): frames made by the
/// reference `zstd` (1.5.7) from the data <see cref="Data"/> makes, kept here as base64, decompressed back to the same bytes;
/// between them raw, RLE and compressed blocks, Huffman literals in four streams with FSE-coded weights, FSE sequence
/// tables and repeat offsets are met. Frames built here byte by byte cover what the reference encoder seldom writes (an RLE
/// literals section, direct weights, frames one after another with a skippable one between). Where `zstd` is on this
/// computer, larger data at many levels is compressed and decompressed too.
/// </summary>
public class ZstdTests
{
    sealed class Lcg(uint seed)
    {
        uint _s = seed;
        public uint Next() => _s = (_s * 1103515245u + 12345u) & 0x7fffffffu;
    }

    static readonly string[] Words = "the a splat point cloud wall roof window door level gaussian harmonic colour view section plan sheet beam column slab stair".Split(' ');

    /// <summary>The data the fixtures were made from (the generating script's formulas).</summary>
    internal static byte[] Data(string kind)
    {
        var r = new Lcg(7);
        switch (kind)
        {
            case "text":
            {
                var words = new List<string>();
                var n = 0;
                while (n < 6000) { var w = Words[r.Next() % Words.Length]; words.Add(w); n += w.Length + 1; }
                var b = Encoding.ASCII.GetBytes(string.Join(' ', words));
                return b[..Math.Min(6000, b.Length)];
            }
            case "grid":
            {
                var b = new byte[48000];
                for (var i = 0; i < 12000; i++) BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(4 * i), (float)(i % 100 * 0.25 + i / 100 * 0.5));
                return b;
            }
            case "big":
            {
                var b = new byte[300000];
                for (var i = 0; i < b.Length; i++) b[i] = (byte)((i / 7 + (i % 4999 == 0 ? (int)(r.Next() % 3) : 0)) & 0xff);
                return b;
            }
            case "tiny": return "0123456789"u8.ToArray();
            case "zeros": return new byte[200000];
            default:
            {
                var b = new byte[300];
                for (var i = 0; i < b.Length; i++) b[i] = (byte)(r.Next() & 0xff);
                return b;
            }
        }
    }

    static readonly Dictionary<string, string> Files = new()
    {
        ["text"] = "KLUv/WRvFsUkAOKHFBGwawwkySgi5sg0eyZJRTJVAf////8rWNd0qcrMFmZhuOoPzwkwGLRHXZBllqV2BaLNWm3yViDzkK1poTUs5EJ0GkNm09hVKPClcYjWhKqtbgiCY6hSr6AgZSTLHDIQBDgkHk4UUfgBIiAIXIIQCdCgAqdJkg4oTZFhSxOJ+ezYOMQpgRTp2Um2rKOiN0dTgwE8ltWowmlAy4HMG+b4aCDyja8zwg2kLDuDpTUJ+BOfxgvs2E8PUFcY/5gkJnH1PXw4I1BCjeffl3jXA5V4ACiRk6vGozevwAtR9/L9H1XdC+sptBCIeUGz2KrPdfqe2Y6UhnF7KRq+ovZCe0ZDT7s8pZh8d22p25VaC/9US5mk0rt+vVS3EkSYaSoXnq9GICA6cGcdC/sJhGJQx7ffESS5luPxDglfkVFQlIs5j6ii2/qJwuwzbKe8XsUvoNw1TW7RCPYMZOeMJae6uK+7At+ddCRNFLYzAB73NQsyKntoKqefECiYAPYw4FwkV0c6fxhxqMux6b0mZaCIhqKhts/l8PgTLjlqUTDphps3PFSx0YmrHgFUdG8e5qOayiTfNFoXbFjexB7/oXIf7bWnFe9GpswVF5hR4lxkIg0YGeRUPSBlZ69cOM6smOolEalXW7JoJ+asZVCrrnLbkIHwlOuYCjeaKzZuJubCj0N2VtqvfN0a3Rap0rejGDwmu1QpOmVexXdOfsiPktV1EH8SzBEuMuH8CQXIDBSfJBuje17+S7zZyagBLkxQ+TNzGq9jVMXwgMJA4dYN2kHC+ligTHw/TLzdu1ZoY+I2+8ouCtOBMyKBJyyVbwAmdVvB8AiKtJh9P26RmHdy0E7gV+AGvId7QqduiiGq8kpeVVKZYDjmjebnbx+n58P6gvQDeGHRgiPWZ+viTTjmQl8syDs2MdDn71jMnKbhoP/qMzTMSeKkMnCaoPoe00GKx1vsC6jkB1GfUI7gbDpONOQv6XCRJnSOcOAI+jj4E9BlE4QdTFVteso8S0XTwL2mFhsyZzf3jCrVW7zryAc6ksPy2l3K+ZylOMxucL2mIzlwkWn2Lxx1iJ3aesUP89hq18vgQUt85C6bPmfMBaG0dYrBUJ8iS7TfNa40EhtKrOeQ50UqI7Y0YgaZSfhE1Hxq95TUVXS8Lgyi3gO9OHO26FcEB5LZr1BnXqFBuROKqBDqmQH4YL62jjMCFxQyB3gtWu92ePg5X3C9Bm7IFGGT+CUB1sEIyAoCHLGD9gqjvoBPdYtGiI+hJokgACoxECVkiHs7YOQP55xCLCGMNOxa4cZ8e3wY5CHfhmJoBkaTcYBMTBXEJTMiR8X1qj4DSmn20ZozIBPpNXuUNRRytaYPx3d2ba2MBQyMMR4HWDNTdVj9qT6GpxN8P/aQlBDcj2utSglmI7/B70pbF4fhJGP7PwBZS/6dPW1BBYguzpOBfAjbSUN6itnQpuLAsyAWfDzxOUZ0Ww2ZviDu/a3/QwoST+Z2HYJQK+XxdRvl5taxcpYlGebuYA/GzID0FOzZM/jHiWUneKIWHhZLt0ZBNobhwlbOnpAzGyReqbfWBlwkzqA=",
        ["grid"] = "KLUv/WSAuo0aAPpBrAooYLSkOXxkPmw+NL40rnQucj4yPjZO88cOP2p8VVVV1Rspn6uqqopFCocAlACkAM7Pjs9u293Zzdm92a3ZndmN2X3Zbdld2U3ZPdkt2R3ZDdn92NV2O3Y3djN2L3Yrdid2I3Yfdht2F3YTdg92C3YHdgN2rxm7Juyar2u6rtm6Juuaq2uqrpm6Juqap2tp19CunV0zu7ZdW3Nty7Ul12rXXlz7cO3BtZfpukzVZWmXbZfVLnuP7c/Br8Fj8FvwFPwSPAS/A8/Ar8Aj8BvwBPwCPAC/LtkOmTu2M+aK7Yi5YTthLtgOmPu18+V67Xi5XTtdLtcOl7u1s+Vq7Wi5WTtZLtYOlnu1c+Va7VjtVu1U7VLtUO1O7UztSu1I7UbtRO1C7UDtPu087TjtNO0w7dJ2lXaTdpF2aDtHO0Y7Rbuz3aFdod2gndkO0AHfGDG/MGB+X7z8ujwuvy1Pyy/Lw/K78qz8qjwqvylPyi/Kg/J78pz8mjwmvyVPyS/JQ/I78oz8ijwivyFPyC/IA/L78Xz83nO/2iv79Xg8fjuejl+Oh+N349n41Xg0fjOejF+MB+P34rn4tXgsfiueil+Kh+J34pn4lXgkfiOeiF+IB+L34Xn4dXgcfhuehl+Gh+F34Vn4VXgUfhOehF+EB+H3IM/T9nnqtH2dOG0fp03bt0nT9mnKtH2ZMG0fpkvbdylt+9IsbZ+lSttXidL2UZq0fZMkbZ+kSNsXCdL2QULbPrRH2/fI0fY5arR9jRhtH6NF27dI0fYpSrR9ic627wzR9iE6tH2HDG2foULbV4jQ9hEatH2DBG2fILPtMwu0fYEAbR+gP9v3x8/2+emzfX34bB+fPdu3Z9u+Tc8vD57fHTu/OnR+c+T84sD5vXHza8Pmt0bNLw2a3xkzvzJkBnioESQEeX7/3+D3DBJAEPj//99/iBEhQowYMWLEiBEjRowYMWLEiBEjRhQxYsSIESNGjBgxYsSIESNGjBgxYsSIIkaMGDFixIgRI0aMGDFixIgPHT50+NDDhw8ffvjw4cOHDx8+fPjw4cOHDx8+9PDhw4cPHz58+PDhw4cPHz58+PDhw4cePnz48OHDhw8fPnz48OHDhw8fPtSR4uFiuwIsIJRKuN8=",
        ["big"] = "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",
        ["tiny"] = "KLUv/SQKUQAAMDEyMzQ1Njc4OednGKg=",
        ["zeros"] = "KLUv/aRADQMAVAAAEAAAAQD7/znAAgNqCADE6XRw",
        ["noise"] = "KLUv/WQsAE0IAAQQNF3So6BZHv/MFSobuJH292TNgpPQyc7v/IXaC+gBpueUPTKDADl+3yz1ivsYcVbXxK3iczCpLs9cZTrrSOEGx/QdkmNgGd6/jNXq23hRtrckjUJTkImOr7xFmsuowWanVP3yQ8D5Pp/stUq72DEWl4RtojPwae6PHCX6qwihxoe03VIjINmef0yVqps4EXZ35E0CE1BJTm98BVqLaIEmZxS9sgOAuf5frHUKe5jx1ldELWLzsCmuT9zlumvIYYZHdJ0S4+CZXj8MVWpb+NE2N6QNwtMQCQ4vPMUaSyhB5ifUfXLDQHm+H2w1yjtYsZYXBO0is3Dpbg+cpXoriCFGBwEAAA2YawoLhx1X",
    };

    [Fact]
    public void Frames_made_by_zstd_come_back_byte_for_byte()
    {
        var used = Zstd.Features.None;
        foreach (var (kind, b64) in Files)
        {
            var got = Zstd.Decompress(Convert.FromBase64String(b64), out var u);
            Assert.True(Data(kind).AsSpan().SequenceEqual(got), $"{kind}: {got.Length} bytes, not the {Data(kind).Length} it was made from");
            used |= u;
        }
        foreach (var f in new[] { Zstd.Features.RawBlock, Zstd.Features.RleBlock, Zstd.Features.CompressedBlock, Zstd.Features.RawLiterals, Zstd.Features.HuffmanLiterals,
                     Zstd.Features.FourStreams, Zstd.Features.FseWeights, Zstd.Features.FseTable, Zstd.Features.PredefinedTable, Zstd.Features.RepeatOffset, Zstd.Features.Checksum })
            Assert.True(used.HasFlag(f), $"no fixture meets {f} ({used})");
    }

    /// <summary>A frame header: a window of 1 KB, its content size in 4 bytes, no checksum.</summary>
    static List<byte> Header(int size)
    {
        var h = new List<byte> { 0x28, 0xB5, 0x2F, 0xFD, 0x80, 0x00 };
        h.AddRange(BitConverter.GetBytes(size));
        return h;
    }

    static void Block(List<byte> o, int type, int size, bool last, IEnumerable<byte> content)
    {
        var bh = (last ? 1 : 0) | type << 1 | size << 3;
        o.AddRange([(byte)bh, (byte)(bh >> 8), (byte)(bh >> 16)]);
        o.AddRange(content);
    }

    [Fact]
    public void Frames_built_by_hand_raw_rle_rle_literals_direct_weights_and_a_skippable_frame_between()
    {
        // Frame 1: a raw block "abc", an RLE block of 5 'x', the last a compressed block of RLE literals (7 'q') and no sequences.
        var f = Header(3 + 5 + 7);
        Block(f, 0, 3, false, "abc"u8.ToArray());
        Block(f, 1, 5, false, [(byte)'x']);
        // Literals header: RLE (type 1), Size_Format 0, Regenerated_Size 7 in the top five bits; then the byte; then 0 sequences.
        Block(f, 2, 3, true, [(byte)(1 | 7 << 3), (byte)'q', 0]);
        // A skippable frame (magic 0x184D2A5?, its size, its bytes) between two frames.
        var skip = new List<byte> { 0x53, 0x2A, 0x4D, 0x18, 4, 0, 0, 0, 1, 2, 3, 4 };
        // Frame 2: Huffman literals with directly written weights, one stream, no sequences: 'a' (97) weight 2, 'b' and 'c' 1;
        // their sum, 4, completes to 8, so 'd' (the last, not written) has weight 3 and Max_Number_of_Bits is 3.
        var weights = new byte[100];
        weights[97] = 2; weights[98] = 1; weights[99] = 1;
        var tree = new List<byte> { (byte)(127 + weights.Length) };
        for (var i = 0; i < weights.Length; i += 2) tree.Add((byte)(weights[i] << 4 | weights[i + 1]));
        // Codes (§4.2.1.3): weight 1 (3 bits) 'b' 000, 'c' 001; weight 2 (2 bits) 'a' 01; weight 3 (1 bit) 'd' 1.
        var text = "dadbcadd"u8.ToArray();
        var code = new Dictionary<byte, (int Bits, int Len)> { [(byte)'b'] = (0b000, 3), [(byte)'c'] = (0b001, 3), [(byte)'a'] = (0b01, 2), [(byte)'d'] = (0b1, 1) };
        // The stream is read backwards: the first symbol is written last. Bits are packed from the end, highest first.
        var bitsList = new List<int>();
        foreach (var c in text) { var (v, l) = code[c]; for (var k = l - 1; k >= 0; k--) bitsList.Add(v >> k & 1); }
        // Written forward = reversed order of reading: the stream's bit sequence read from the top down is bitsList, then the 1 mark.
        var total = bitsList.Count + 1;
        var bytes = new byte[(total + 7) / 8];
        var pos = total - 1;
        bytes[pos >> 3] |= (byte)(1 << (pos & 7));
        foreach (var bit in bitsList) { pos--; if (bit == 1) bytes[pos >> 3] |= (byte)(1 << (pos & 7)); }
        var lit = new List<byte>(tree);
        lit.AddRange(bytes);
        var regen = text.Length;
        var comp = lit.Count;
        var lh = 2 | 0 << 2 | regen << 4 | comp << 14;
        var g = Header(text.Length);
        var content = new List<byte> { (byte)lh, (byte)(lh >> 8), (byte)(lh >> 16) };
        content.AddRange(lit);
        content.Add(0);
        Block(g, 2, content.Count, true, content);
        var all = f.Concat(skip).Concat(g).ToArray();
        var got = Zstd.Decompress(all, out var used);
        Assert.Equal("abcxxxxxqqqqqqqdadbcadd", Encoding.ASCII.GetString(got));
        foreach (var x in new[] { Zstd.Features.RawBlock, Zstd.Features.RleBlock, Zstd.Features.RleLiterals, Zstd.Features.Skippable, Zstd.Features.DirectWeights })
            Assert.True(used.HasFlag(x), $"{x} not met ({used})");
    }

    [Fact]
    public void A_dictionary_a_cut_frame_or_another_format_is_refused()
    {
        var withDict = new byte[] { 0x28, 0xB5, 0x2F, 0xFD, 0x01, 0x7, 0x10, 1, 0, 0 };
        Assert.Contains("dictionary", Assert.Throws<Zstd.ZstdException>(() => Zstd.Decompress(withDict)).Message);
        var text = Convert.FromBase64String(Files["text"]);
        Assert.ThrowsAny<Exception>(() => Zstd.Decompress(text[..(text.Length / 2)]));
        Assert.Throws<Zstd.ZstdException>(() => Zstd.Decompress("PK\x03\x04 a zip"u8));
    }

    [Fact]
    public void Larger_data_compressed_by_zstd_here_at_many_levels_decompresses_exactly()
    {
        var zstd = WebPTests.Tool("zstd");
        if (zstd == null) return; // Not on this computer: the fixtures above stand alone.
        var dir = Directory.CreateTempSubdirectory("lapin-zstd-");
        try
        {
            // Splat-like attributes: fixed-point positions, bytes of colour, a run, text: a megabyte and a half.
            var r = new Lcg(3);
            var data = new byte[1_500_000];
            for (var i = 0; i < data.Length; i++)
                data[i] = i < 600_000 ? (byte)((i / 3 * 7 + (i % 3 == 2 ? (int)(r.Next() % 4) : 0)) >> (i % 3 * 3))
                    : i < 900_000 ? (byte)(r.Next() % 16 * 16)
                    : i < 1_000_000 ? (byte)0x55
                    : (byte)(Words[r.Next() % Words.Length] is var w ? w[i % w.Length] : ' ');
            var src = Path.Combine(dir.FullName, "data");
            File.WriteAllBytes(src, data);
            foreach (var args in new[] { new[] { "-1" }, ["-3"], ["-9"], ["-19"], ["--ultra", "-22"], ["--fast=5"], ["-3", "--long=24"], ["-6", "--no-check", "-B65536"] })
            {
                var dst = Path.Combine(dir.FullName, "data.zst");
                WebPTests.Run(zstd, ["-q", "-f", .. args, src, "-o", dst]);
                var got = Zstd.Decompress(File.ReadAllBytes(dst), data.Length);
                Assert.True(data.AsSpan().SequenceEqual(got), $"zstd {string.Join(' ', args)}: not the same bytes back");
            }
        }
        finally { dir.Delete(true); }
    }
}
