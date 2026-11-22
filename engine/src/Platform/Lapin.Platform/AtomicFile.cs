using System.Text;

namespace Lapin.Platform;

/// <summary>
/// A file written whole or not at all (audit 22.11.2026: an export, a library entry, a list of trusted scripts written in
/// place, or moved over without being flushed, was left truncated by a full disk or a power cut): written beside it under a
/// temporary name in the same folder, flushed to the disk, then moved over the old one; the temporary file taken away when
/// anything fails, the old file left as it was. The package's own save (<c>LapinPackage.Replace</c>) works the same way.
/// </summary>
public static class AtomicFile
{
    /// <summary>The temporary file beside <paramref name="path"/>: same folder (so the move is a rename), a name no other writer takes.</summary>
    static string TempOf(string path) => $"{path}.{Guid.NewGuid().ToString("N")[..8]}.saving";

    /// <summary>The bytes <paramref name="write"/> puts into the stream, as the file at <paramref name="path"/>.</summary>
    public static void Write(string path, Action<Stream> write)
    {
        var temp = TempOf(path);
        try
        {
            using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                write(file);
                file.Flush(flushToDisk: true);
            }
            File.Move(temp, path, overwrite: true);
        }
        catch
        {
            try { if (File.Exists(temp)) File.Delete(temp); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            throw;
        }
    }

    public static void Write(string path, byte[] bytes) => Write(path, s => s.Write(bytes));

    public static void Write(string path, ArraySegment<byte> bytes) => Write(path, s => s.Write(bytes));

    /// <summary>The text as UTF-8 without a byte-order mark (what <see cref="File.WriteAllText(string, string?)"/> writes).</summary>
    public static void WriteText(string path, string text) => Write(path, new UTF8Encoding(false).GetBytes(text));
}
