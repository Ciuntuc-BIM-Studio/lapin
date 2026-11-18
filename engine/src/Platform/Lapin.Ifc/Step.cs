using System.Globalization;
using System.Numerics;
using System.Text;
using Lapin.Platform;

namespace Lapin.Ifc;

/// <summary>
/// An ISO 10303-21 (STEP) file being written: entities numbered as they are
/// added, each with its attributes already in STEP form — see the helpers:
/// <see cref="S"/> a string, <see cref="R"/> a real, <see cref="E"/> an
/// enumeration, <see cref="L{T}"/> a list, <see cref="Ref"/> a reference.
/// </summary>
public sealed class Step
{
    /// <summary>The application named in the header (the product: Lapin, or an edition under its own name).</summary>
    public string Application { get; set; } = "Lapin";

    /// <summary>
    /// The data section as UTF-8, in chunks of a megabyte or more, each holding whole lines (report #156): a 33-bay steel hall
    /// is 150 MB of IFC, which as text (two bytes a character) in one growing builder took twice that and more, where the
    /// browser's engine has 2 GB for everything.
    /// </summary>
    readonly List<(byte[] Bytes, int Used)> _chunks = [];
    byte[] _chunk = [];
    int _used;
    const int ChunkSize = 1 << 20;
    int _next = 1;

    /// <summary>The data section's bytes so far.</summary>
    public long DataLength => _chunks.Sum(c => (long)c.Used) + _used;

    /// <summary>Adds an entity; returns its reference (#12).</summary>
    public string Add(string entity, params string[] attributes)
    {
        var id = $"#{_next++}";
        Append(string.Concat(id, "=", entity, "(", string.Join(',', attributes), ");\n"));
        return id;
    }

    void Append(string line)
    {
        var n = Encoding.UTF8.GetByteCount(line);
        if (_used + n > _chunk.Length)
        {
            if (_used > 0) _chunks.Add((_chunk, _used));
            _chunk = new byte[Math.Max(ChunkSize, n)];
            _used = 0;
        }
        _used += Encoding.UTF8.GetBytes(line, _chunk.AsSpan(_used));
    }

    IEnumerable<ReadOnlyMemory<byte>> Data()
    {
        foreach (var (bytes, used) in _chunks) yield return bytes.AsMemory(0, used);
        if (_used > 0) yield return _chunk.AsMemory(0, _used);
    }

    public const string Null = "$";
    public const string Derived = "*";
    public const string True = ".T.";
    public const string False = ".F.";

    /// <summary>A string: quotes doubled, backslashes doubled, anything beyond ASCII as \X2\…\X0\ (beyond the BMP as \X4\…\X0\).</summary>
    public static string S(string? s)
    {
        if (s == null) return Null;
        var b = new StringBuilder("'");
        foreach (var r in s.EnumerateRunes())
        {
            if (r.Value == '\'') b.Append("''");
            else if (r.Value == '\\') b.Append("\\\\");
            else if (r.Value >= 32 && r.Value < 127) b.Append((char)r.Value);
            else if (r.IsBmp) b.Append("\\X2\\").Append(r.Value.ToString("X4", CultureInfo.InvariantCulture)).Append("\\X0\\");
            else b.Append("\\X4\\").Append(r.Value.ToString("X8", CultureInfo.InvariantCulture)).Append("\\X0\\");
        }
        return b.Append('\'').ToString();
    }

    /// <summary>A real: always with its decimal point (1. not 1).</summary>
    public static string R(double v)
    {
        if (!double.IsFinite(v)) v = 0;
        var t = Math.Round(v, 9).ToString("0.#########", CultureInfo.InvariantCulture);
        return t.Contains('.') ? t : t + ".";
    }

    public static string I(int v) => v.ToString(CultureInfo.InvariantCulture);
    public static string E(string v) => $".{v}.";
    public static string Ref(string id) => id;
    public static string L<T>(IEnumerable<T> items) => $"({string.Join(',', items)})";
    public static string Typed(string type, string value) => $"{type}({value})";

    const string Chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz_$";

    /// <summary>
    /// An IFC GlobalId (22 characters of IFC's base 64) the same for the same
    /// seed and key: a project exported again keeps its ids, so what changed
    /// between two exports can be told.
    /// </summary>
    public static string Guid(string seed, string key)
    {
        var bytes = Md5.HashData(Encoding.UTF8.GetBytes(seed + "\n" + key));
        var n = new BigInteger(bytes, isUnsigned: true, isBigEndian: true);
        var c = new char[22];
        for (var i = 21; i >= 0; i--) { c[i] = Chars[(int)(n % 64)]; n /= 64; }
        return new string(c);
    }

    /// <param name="schema">IFC4, or IFC4X3_ADD2 (IFC 4.3: alignments, infrastructure).</param>
    public void Write(string path, string name, string description, string schema = "IFC4")
    {
        using var file = File.Create(path);
        Write(file, name, description, schema);
    }

    /// <summary>The file into a stream.</summary>
    public void Write(Stream output, string name, string description, string schema = "IFC4")
    {
        output.Write(Header(name, description, schema));
        // In its chunks: a big model's data is more than one string or array can hold.
        foreach (var chunk in Data()) output.Write(chunk.Span);
        output.Write(Footer);
    }

    /// <summary>The whole file as one array, of its exact size: no stream growing by doubling, no copy of it (report #156).</summary>
    public byte[] ToArray(string name, string description, string schema = "IFC4")
    {
        var header = Header(name, description, schema);
        var length = header.Length + DataLength + Footer.Length;
        if (length > Array.MaxLength) throw new InvalidOperationException($"The IFC file would be {length / 1e9:0.0} GB: more than one file can be made of in memory.");
        var o = new byte[length];
        header.CopyTo(o, 0);
        var at = header.Length;
        foreach (var chunk in Data()) { chunk.Span.CopyTo(o.AsSpan(at)); at += chunk.Length; }
        Footer.CopyTo(o.AsSpan(at));
        return o;
    }

    static ReadOnlySpan<byte> Footer => "ENDSEC;\nEND-ISO-10303-21;\n"u8;

    byte[] Header(string name, string description, string schema)
    {
        var now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
        return Encoding.UTF8.GetBytes(
            "ISO-10303-21;\nHEADER;\n" +
            $"FILE_DESCRIPTION(({S(description)}),'2;1');\n" +
            $"FILE_NAME({S(name)},'{now}',(''),(''),{S(Application)},{S(Application)},'');\n" +
            $"FILE_SCHEMA(('{schema}'));\nENDSEC;\nDATA;\n");
    }
}
