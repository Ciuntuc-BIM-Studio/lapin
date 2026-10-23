namespace Lapin.Model;

/// <summary>
/// IFC GlobalIds: a 128-bit GUID in IFC's 22-character base-64 form
/// (alphabet 0-9 A-Z a-z _ $; the first character carries the top 2 bits).
/// </summary>
public static class GlobalId
{
    const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz_$";

    public static string New() => FromGuid(Guid.NewGuid());

    public static string FromGuid(Guid guid)
    {
        var bytes = guid.ToByteArray(bigEndian: true);
        var o = new char[22];
        // 2 bits, then 21 groups of 6 bits: 2 + 126 = 128.
        o[0] = Alphabet[bytes[0] >> 6];
        int bit = 2, k = 1;
        while (k < 22)
        {
            var v = 0;
            for (var j = 0; j < 6; j++, bit++) v = (v << 1) | ((bytes[bit >> 3] >> (7 - (bit & 7))) & 1);
            o[k++] = Alphabet[v];
        }
        return new string(o);
    }

    public static Guid ToGuid(string id)
    {
        if (!IsValid(id)) throw new FormatException($"„{id}” is not an IFC GlobalId.");
        var bytes = new byte[16];
        var first = Alphabet.IndexOf(id[0]);
        bytes[0] = (byte)(first << 6);
        int bit = 2;
        for (var k = 1; k < 22; k++)
        {
            var v = Alphabet.IndexOf(id[k]);
            for (var j = 5; j >= 0; j--, bit++)
                if (((v >> j) & 1) == 1) bytes[bit >> 3] |= (byte)(1 << (7 - (bit & 7)));
        }
        return new Guid(bytes, bigEndian: true);
    }

    public static bool IsValid(string? id) =>
        id is { Length: 22 } && Alphabet.IndexOf(id[0]) is >= 0 and < 4 && id.All(c => Alphabet.Contains(c));
}
