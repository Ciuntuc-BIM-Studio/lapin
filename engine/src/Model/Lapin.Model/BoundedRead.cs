namespace Lapin.Model;

/// <summary>
/// A stream read into memory no further than a limit (audit 22.11.2026, G19): a damaged or crafted file — a zip entry that
/// inflates past its declared size, a page that claims more than it holds — is refused with <see cref="InvalidDataException"/>
/// instead of filling the memory. In memory only; no file is opened here.
/// </summary>
public static class BoundedRead
{
    /// <summary>The stream to its end, refused when it holds more than <paramref name="max"/> bytes.</summary>
    public static byte[] ToEnd(Stream s, long max, string what)
    {
        using var ms = new MemoryStream();
        var buffer = new byte[81920];
        int n;
        while ((n = s.Read(buffer, 0, buffer.Length)) > 0)
        {
            if (ms.Length + n > max) throw new InvalidDataException($"{what} is larger than it says ({max} bytes at most).");
            ms.Write(buffer, 0, n);
        }
        return ms.ToArray();
    }

    /// <summary>Exactly <paramref name="length"/> bytes, the stream's whole content: more or fewer is refused.</summary>
    public static byte[] Exactly(Stream s, long length, string what)
    {
        if (length < 0 || length > Array.MaxLength) throw new InvalidDataException($"{what} declares an impossible size ({length} bytes).");
        var bytes = ToEnd(s, length, what);
        if (bytes.Length != length) throw new InvalidDataException($"{what} is shorter than it says ({bytes.Length} of {length} bytes).");
        return bytes;
    }
}
