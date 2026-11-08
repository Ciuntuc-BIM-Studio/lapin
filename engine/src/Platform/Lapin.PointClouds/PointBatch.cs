namespace Lapin.PointClouds;

/// <summary>
/// Points as a reader hands them over, a few tens of thousands at a time: coordinates in the file's own units and system,
/// colour as RGB as the file stores it (8 or 16 bits: the index tells which by the largest value), intensity as 16 bits, the ASPRS class (0 when the file has none).
/// </summary>
public sealed class PointBatch(int capacity)
{
    public readonly double[] X = new double[capacity], Y = new double[capacity], Z = new double[capacity];
    public readonly ushort[] R = new ushort[capacity], G = new ushort[capacity], B = new ushort[capacity];
    public readonly ushort[] Intensity = new ushort[capacity];
    public readonly byte[] Class = new byte[capacity];
    public int Count;
    public int Capacity => X.Length;
    public bool Full => Count >= X.Length;

    public void Add(double x, double y, double z, ushort r = 0, ushort g = 0, ushort b = 0, ushort intensity = 0, byte cls = 0)
    {
        var i = Count++;
        X[i] = x; Y[i] = y; Z[i] = z; R[i] = r; G[i] = g; B[i] = b; Intensity[i] = intensity; Class[i] = cls;
    }
}

/// <summary>What a file says of its points before they are read (what it does not say stays null).</summary>
public sealed record PointCloudHeader
{
    /// <summary>The format and its version, as shown to the user ("LAS 1.4, point format 7", "PLY binary").</summary>
    public required string Format { get; init; }
    /// <summary>How many points it holds, when it says.</summary>
    public long? Count { get; init; }
    /// <summary>Its box (min x, y, z, max x, y, z) in its own units, when it says.</summary>
    public double[]? Bounds { get; init; }
    public bool HasColor { get; init; }
    public bool HasIntensity { get; init; }
    public bool HasClassification { get; init; }
    /// <summary>Its coordinate system as an EPSG code ("EPSG:3844"), when it names one.</summary>
    public string? Crs { get; init; }
    /// <summary>The coordinate system's description as the file has it (a WKT), when it has one.</summary>
    public string? CrsText { get; init; }
    /// <summary>The unit its coordinates are in, when it says: m, ft (international foot), usft (US survey foot).</summary>
    public string? Units { get; init; }
    /// <summary>The finest step its coordinates take (a LAS's scale), per axis, when it has one.</summary>
    public double[]? Precision { get; init; }
    /// <summary>What the reader noticed and the user should know (an unknown property, a part it skipped).</summary>
    public IReadOnlyList<string> Notes { get; init; } = [];
}

/// <summary>A file of points: its header and its points, read as often as asked (the index reads a file twice).</summary>
public interface IPointReader
{
    PointCloudHeader Header { get; }
    /// <summary>Every point, in batches of at most <paramref name="batch"/>; the same batch object is filled again each time.</summary>
    IEnumerable<PointBatch> Read(int batch = 65536, CancellationToken cancel = default);
}

/// <summary>Where a reader's bytes come from: a file on the computer, or the bytes a browser read.</summary>
public interface IPointSource
{
    string Name { get; }
    long Length { get; }
    Stream Open();
}

public sealed class FileSource(string path) : IPointSource
{
    public string Path { get; } = path;
    public string Name => System.IO.Path.GetFileName(Path);
    public long Length => new FileInfo(Path).Length;
    public Stream Open() => new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16, FileOptions.SequentialScan);
}

public sealed class BytesSource(string name, byte[] bytes) : IPointSource
{
    public string Name { get; } = name;
    public byte[] Bytes { get; } = bytes;
    public long Length => Bytes.Length;
    public Stream Open() => new MemoryStream(Bytes, writable: false);
}

/// <summary>A file that is not what it says, or that Lapin cannot read: the message is the user's.</summary>
public sealed class PointCloudException(string message) : Exception(message);

/// <summary>The readers by the file's name and first bytes.</summary>
public static class PointReaders
{
    /// <summary>The extensions Lapin reads, lower case, without the dot.</summary>
    public static readonly string[] Extensions = ["las", "laz", "e57", "ply", "xyz", "pts", "csv", "txt"];

    public static IPointReader Open(IPointSource source)
    {
        var ext = System.IO.Path.GetExtension(source.Name).TrimStart('.').ToLowerInvariant();
        Span<byte> head = stackalloc byte[8];
        int n;
        using (var s = source.Open()) n = s.ReadAtLeast(head, 8, throwOnEndOfStream: false);
        head = head[..n];
        if (head.StartsWith("LASF"u8)) return new LasReader(source);
        if (head.StartsWith("ASTM-E57"u8)) return new E57Reader(source);
        if (head.StartsWith("ply"u8)) return new PlyReader(source);
        return ext switch
        {
            "las" or "laz" => throw new PointCloudException($"{source.Name} is not a LAS file (it does not start with LASF)."),
            "e57" => throw new PointCloudException($"{source.Name} is not an E57 file."),
            "ply" => throw new PointCloudException($"{source.Name} is not a PLY file."),
            "xyz" or "pts" or "csv" or "txt" or "asc" => new TextPointReader(source),
            _ => throw new PointCloudException($"Lapin reads point clouds as LAS, LAZ, E57, PLY, XYZ, PTS or CSV; {source.Name} is none of them."),
        };
    }
}
