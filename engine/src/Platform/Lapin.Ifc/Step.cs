using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace Lapin.Ifc;

/// <summary>
/// An ISO 10303-21 (STEP) file being written: entities numbered as they are
/// added, each with its attributes already in STEP form — see the helpers:
/// <see cref="S"/> a string, <see cref="R"/> a real, <see cref="E"/> an
/// enumeration, <see cref="L{T}"/> a list, <see cref="Ref"/> a reference.
/// </summary>
public sealed class Step
{
    readonly StringBuilder _data = new();
    int _next = 1;

    /// <summary>Adds an entity; returns its reference (#12).</summary>
    public string Add(string entity, params string[] attributes)
    {
        var id = $"#{_next++}";
        _data.Append(id).Append('=').Append(entity).Append('(').Append(string.Join(',', attributes)).Append(");\n");
        return id;
    }

    public const string Null = "$";
    public const string Derived = "*";
    public const string True = ".T.";
    public const string False = ".F.";

    /// <summary>A string: quotes doubled, backslashes doubled, anything beyond ASCII as \X2\…\X0\.</summary>
    public static string S(string? s)
    {
        if (s == null) return Null;
        var b = new StringBuilder("'");
        foreach (var ch in s)
        {
            if (ch == '\'') b.Append("''");
            else if (ch == '\\') b.Append("\\\\");
            else if (ch >= 32 && ch < 127) b.Append(ch);
            else b.Append("\\X2\\").Append(((int)ch).ToString("X4", CultureInfo.InvariantCulture)).Append("\\X0\\");
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
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(seed + "\n" + key));
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
        using var w = new StreamWriter(output, new UTF8Encoding(false), leaveOpen: true) { NewLine = "\n" };
        var now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
        w.Write("ISO-10303-21;\nHEADER;\n");
        w.Write($"FILE_DESCRIPTION(({S(description)}),'2;1');\n");
        w.Write($"FILE_NAME({S(name)},'{now}',(''),(''),'Lapin','Lapin','');\n");
        w.Write($"FILE_SCHEMA(('{schema}'));\nENDSEC;\nDATA;\n");
        // In its chunks: a big model's data is more than one string can hold.
        foreach (var chunk in _data.GetChunks()) w.Write(chunk.Span);
        w.Write("ENDSEC;\nEND-ISO-10303-21;\n");
    }
}
