using System.Buffers.Binary;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Lapin.PointClouds;

/// <summary>
/// E57 (ASTM E2807-11), written by Lapin after the standard: the file is pages of 1024 bytes, 1020 of data and a CRC-32C
/// (checked), holding an XML section that describes the scans and binary sections of their points. Each scan
/// (<c>/data3D</c>) is a CompressedVector: data packets of bytestreams, one per field of its prototype, its values bit-packed
/// (integers and scaled integers on as many bits as their range needs) or IEEE floats. Read: cartesian or spherical
/// coordinates, intensity and colour (normalised by their limits), the invalid states (invalid points left out), and each
/// scan's pose (a quaternion and a translation) so the scans stand together. Images (<c>/images2D</c>) are not read.
/// </summary>
public sealed partial class E57Reader : IPointReader
{
    enum Kind { Float32, Float64, Integer, Scaled }
    sealed record Field(string Name, Kind Kind, long Min, long Max, double Scale, double Offset)
    {
        public int Bits => Kind switch { Kind.Float32 => 32, Kind.Float64 => 64, _ => Max > Min ? 64 - System.Numerics.BitOperations.LeadingZeroCount((ulong)(Max - Min)) : 0 };
    }
    sealed record Scan(string Name, long Offset, long Count, List<Field> Fields, double[] Rotation, double[] Translation, double IMin, double IMax, double CMin, double CMax);

    readonly IPointSource _source;
    readonly long _pageSize;
    readonly List<Scan> _scans = [];
    public PointCloudHeader Header { get; }

    public E57Reader(IPointSource source)
    {
        _source = source;
        using var s = source.Open();
        var h = new byte[48];
        if (s.ReadAtLeast(h, 48, throwOnEndOfStream: false) < 48 || !h.AsSpan(0, 8).SequenceEqual("ASTM-E57"u8)) throw new PointCloudException($"{source.Name} is not an E57 file.");
        var major = BinaryPrimitives.ReadUInt32LittleEndian(h.AsSpan(8));
        var xmlAt = (long)BinaryPrimitives.ReadUInt64LittleEndian(h.AsSpan(24));
        var xmlLength = (long)BinaryPrimitives.ReadUInt64LittleEndian(h.AsSpan(32));
        _pageSize = (long)BinaryPrimitives.ReadUInt64LittleEndian(h.AsSpan(40));
        if (major != 1) throw new PointCloudException($"{source.Name}: E57 version {major} is not one Lapin reads.");
        if (_pageSize is < 64 or > 1 << 24) throw new PointCloudException($"{source.Name}: its E57 page size ({_pageSize}) is not a page size.");
        if (xmlLength <= 0 || xmlLength > 256 << 20 || xmlAt >= source.Length) throw new PointCloudException($"{source.Name}: its E57 XML section is missing.");
        var paged = new Paged(s, _pageSize, source.Name);
        var xml = new byte[xmlLength];
        paged.Read(Logical(xmlAt), xml);
        XDocument doc;
        try { doc = XDocument.Parse(System.Text.Encoding.UTF8.GetString(xml).TrimEnd('\0', ' ')); }
        catch (System.Xml.XmlException e) { throw new PointCloudException($"{source.Name}: its E57 XML does not read ({e.Message})."); }
        var root = doc.Root!;
        static XElement? Child(XElement? e, string name) => e?.Elements().FirstOrDefault(c => c.Name.LocalName == name);
        static double Num(XElement? e, double fallback = 0) => e == null ? fallback : e.Value.Trim().Length == 0 ? 0 : double.TryParse(e.Value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;
        var notes = new List<string>();
        var data3d = Child(root, "data3D");
        foreach (var scan in data3d?.Elements() ?? [])
        {
            var points = Child(scan, "points");
            if (points == null || (string?)points.Attribute("type") != "CompressedVector") continue;
            var proto = Child(points, "prototype");
            var fields = new List<Field>();
            foreach (var f in proto?.Elements() ?? [])
            {
                var type = (string?)f.Attribute("type");
                long L(string a, long d) => long.TryParse((string?)f.Attribute(a), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : d;
                double D(string a, double d) => double.TryParse((string?)f.Attribute(a), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : d;
                fields.Add(type switch
                {
                    "Float" => new Field(f.Name.LocalName, (string?)f.Attribute("precision") == "single" ? Kind.Float32 : Kind.Float64, 0, 0, 1, 0),
                    "Integer" => new Field(f.Name.LocalName, Kind.Integer, L("minimum", long.MinValue), L("maximum", long.MaxValue), 1, 0),
                    "ScaledInteger" => new Field(f.Name.LocalName, Kind.Scaled, L("minimum", long.MinValue), L("maximum", long.MaxValue), D("scale", 1), D("offset", 0)),
                    _ => throw new PointCloudException($"{source.Name}: a scan's field {f.Name.LocalName} is a {type}, which E57 points do not have."),
                });
            }
            var names = fields.Select(f => f.Name).ToHashSet();
            if (!(names.Contains("cartesianX") && names.Contains("cartesianY") && names.Contains("cartesianZ")) && !(names.Contains("sphericalRange") && names.Contains("sphericalAzimuth") && names.Contains("sphericalElevation")))
            { notes.Add($"A scan of {source.Name} has neither cartesian nor spherical coordinates: left out."); continue; }
            var pose = Child(scan, "pose");
            var rot = Child(pose, "rotation");
            var tr = Child(pose, "translation");
            double[] q = rot != null ? [Num(Child(rot, "w"), 1), Num(Child(rot, "x")), Num(Child(rot, "y")), Num(Child(rot, "z"))] : [1, 0, 0, 0];
            double[] t = tr != null ? [Num(Child(tr, "x")), Num(Child(tr, "y")), Num(Child(tr, "z"))] : [0, 0, 0];
            // Intensity's and colour's limits: the scan's own, else its fields' range.
            var il = Child(scan, "intensityLimits");
            var cl = Child(scan, "colorLimits");
            var fi = fields.FirstOrDefault(f => f.Name == "intensity");
            var fc = fields.FirstOrDefault(f => f.Name == "colorRed");
            static double FieldMin(Field? f) => f == null ? 0 : f.Kind == Kind.Scaled ? f.Min * f.Scale + f.Offset : f.Kind == Kind.Integer ? f.Min : 0;
            static double FieldMax(Field? f) => f == null ? 1 : f.Kind == Kind.Scaled ? f.Max * f.Scale + f.Offset : f.Kind == Kind.Integer ? f.Max : 1;
            double imin = il != null ? Num(Child(il, "intensityMinimum"), FieldMin(fi)) : FieldMin(fi), imax = il != null ? Num(Child(il, "intensityMaximum"), FieldMax(fi)) : FieldMax(fi);
            double cmin = cl != null ? Num(Child(cl, "colorRedMinimum"), FieldMin(fc)) : FieldMin(fc), cmax = cl != null ? Num(Child(cl, "colorRedMaximum"), FieldMax(fc)) : FieldMax(fc);
            _scans.Add(new Scan((string?)Child(scan, "name") ?? $"scan {_scans.Count + 1}", long.Parse((string)points.Attribute("fileOffset")!, CultureInfo.InvariantCulture),
                long.Parse((string)points.Attribute("recordCount")!, CultureInfo.InvariantCulture), fields, q, t, imin, imax, cmin, cmax));
        }
        if (_scans.Count == 0) throw new PointCloudException($"{source.Name} holds no scans with points.");
        var meta = (string?)Child(root, "coordinateMetadata") ?? "";
        var epsg = EpsgRx().Match(meta);
        Header = new PointCloudHeader
        {
            Format = $"E57 1.0, {_scans.Count} scan{(_scans.Count == 1 ? "" : "s")}", Count = _scans.Sum(s => s.Count),
            HasColor = _scans.Any(s => s.Fields.Any(f => f.Name == "colorRed")), HasIntensity = _scans.Any(s => s.Fields.Any(f => f.Name == "intensity")),
            Crs = epsg.Success ? $"EPSG:{epsg.Groups[1].Value}" : null, CrsText = meta.Length > 0 ? meta : null, Units = "m", Notes = notes,
        };
    }

    long Logical(long physical) => physical / _pageSize * (_pageSize - 4) + physical % _pageSize;

    [GeneratedRegex("EPSG[\"',:\\s]*(\\d{4,6})", RegexOptions.IgnoreCase)]
    private static partial Regex EpsgRx();

    /// <summary>The file's logical bytes (its pages' data, the checksums checked and left out).</summary>
    sealed class Paged(Stream s, long pageSize, string name)
    {
        readonly byte[] _page = new byte[pageSize];
        long _loaded = -1;
        long Data => pageSize - 4;

        void Load(long page)
        {
            if (page == _loaded) return;
            s.Position = page * pageSize;
            var got = s.ReadAtLeast(_page, (int)pageSize, throwOnEndOfStream: false);
            if (got < pageSize) throw new PointCloudException($"{name}: the E57 file ends inside a page.");
            var want = BinaryPrimitives.ReadUInt32BigEndian(_page.AsSpan((int)Data));
            if (Crc32C(_page.AsSpan(0, (int)Data)) != want) throw new PointCloudException($"{name}: page {page} of the E57 file is damaged (its checksum does not match).");
            _loaded = page;
        }

        public void Read(long logical, Span<byte> dst)
        {
            var done = 0;
            while (done < dst.Length)
            {
                var page = (logical + done) / Data;
                var within = (int)((logical + done) % Data);
                Load(page);
                var take = Math.Min(dst.Length - done, (int)Data - within);
                _page.AsSpan(within, take).CopyTo(dst[done..]);
                done += take;
            }
        }
    }

    static readonly uint[] CrcTable = Enumerable.Range(0, 256).Select(n =>
    {
        var c = (uint)n;
        for (var k = 0; k < 8; k++) c = (c & 1) != 0 ? 0x82F63B78 ^ (c >> 1) : c >> 1;
        return c;
    }).ToArray();

    /// <summary>CRC-32C (Castagnoli), as E57's pages carry it.</summary>
    internal static uint Crc32C(ReadOnlySpan<byte> data)
    {
        var c = 0xFFFFFFFFu;
        foreach (var b in data) c = CrcTable[(c ^ b) & 0xFF] ^ (c >> 8);
        return c ^ 0xFFFFFFFF;
    }

    /// <summary>A bytestream's values, read as they come, packet by packet: bits least significant first.</summary>
    sealed class Bits(Field f)
    {
        byte[] _b = new byte[4096];
        int _len;
        long _bit;
        public readonly Field Field = f;
        readonly int _width = f.Bits;

        public void Append(ReadOnlySpan<byte> data)
        {
            // What has been read is dropped first (whole bytes).
            var drop = (int)(_bit >> 3);
            if (drop > 0) { Buffer.BlockCopy(_b, drop, _b, 0, _len - drop); _len -= drop; _bit -= (long)drop << 3; }
            if (_len + data.Length > _b.Length) Array.Resize(ref _b, Math.Max(_b.Length * 2, _len + data.Length));
            data.CopyTo(_b.AsSpan(_len));
            _len += data.Length;
        }

        /// <summary>How many values are here to read (a field of no bits has as many as asked).</summary>
        public long Available => _width == 0 ? long.MaxValue : ((long)_len * 8 - _bit) / _width;

        public double Next()
        {
            switch (Field.Kind)
            {
                case Kind.Float32:
                {
                    var v = BinaryPrimitives.ReadSingleLittleEndian(_b.AsSpan((int)(_bit >> 3)));
                    _bit += 32;
                    return v;
                }
                case Kind.Float64:
                {
                    var v = BinaryPrimitives.ReadDoubleLittleEndian(_b.AsSpan((int)(_bit >> 3)));
                    _bit += 64;
                    return v;
                }
            }
            ulong raw = 0;
            // Up to 64 bits from wherever they start, byte by byte.
            for (var got = 0; got < _width;)
            {
                var at = (int)(_bit >> 3);
                var off = (int)(_bit & 7);
                var take = Math.Min(8 - off, _width - got);
                raw |= (ulong)((_b[at] >> off) & ((1 << take) - 1)) << got;
                got += take;
                _bit += take;
            }
            var value = unchecked(Field.Min + (long)raw);
            return Field.Kind == Kind.Scaled ? value * Field.Scale + Field.Offset : value;
        }
    }

    public IEnumerable<PointBatch> Read(int batch = 65536, CancellationToken cancel = default)
    {
        using var s = _source.Open();
        var paged = new Paged(s, _pageSize, _source.Name);
        var b = new PointBatch(batch);
        foreach (var scan in _scans)
        {
            // The section's header: its id, its length, where its data packets start.
            var head = new byte[32];
            paged.Read(Logical(scan.Offset), head);
            if (head[0] != 1) throw new PointCloudException($"{_source.Name}: the points of {scan.Name} are not where its XML says.");
            var at = Logical((long)BinaryPrimitives.ReadUInt64LittleEndian(head.AsSpan(16)));
            var streams = scan.Fields.Select(f => new Bits(f)).ToArray();
            int Idx(string n) => Array.FindIndex(streams, x => x.Field.Name == n);
            int ix = Idx("cartesianX"), iy = Idx("cartesianY"), iz = Idx("cartesianZ"), ir = Idx("sphericalRange"), ia = Idx("sphericalAzimuth"), ie = Idx("sphericalElevation");
            int ii = Idx("intensity"), icr = Idx("colorRed"), icg = Idx("colorGreen"), icb = Idx("colorBlue"), ic = Idx("cartesianInvalidState"), isp = Idx("sphericalInvalidState");
            var cartesian = ix >= 0 && iy >= 0 && iz >= 0;
            // The pose as a rotation matrix (w, x, y, z normalised).
            var (w, qx, qy, qz) = (scan.Rotation[0], scan.Rotation[1], scan.Rotation[2], scan.Rotation[3]);
            var qn = Math.Sqrt(w * w + qx * qx + qy * qy + qz * qz);
            if (qn > 0) { w /= qn; qx /= qn; qy /= qn; qz /= qn; } else w = 1;
            double[] R =
            [
                1 - 2 * (qy * qy + qz * qz), 2 * (qx * qy - w * qz), 2 * (qx * qz + w * qy),
                2 * (qx * qy + w * qz), 1 - 2 * (qx * qx + qz * qz), 2 * (qy * qz - w * qx),
                2 * (qx * qz - w * qy), 2 * (qy * qz + w * qx), 1 - 2 * (qx * qx + qy * qy),
            ];
            var t = scan.Translation;
            var values = new double[streams.Length];
            long read = 0;
            var ph = new byte[6];
            while (read < scan.Count)
            {
                cancel.ThrowIfCancellationRequested();
                paged.Read(at, ph);
                var type = ph[0];
                var length = BinaryPrimitives.ReadUInt16LittleEndian(ph.AsSpan(2)) + 1;
                if (type == 1)
                {
                    var count = BinaryPrimitives.ReadUInt16LittleEndian(ph.AsSpan(4));
                    if (count != streams.Length) throw new PointCloudException($"{_source.Name}: a data packet of {scan.Name} has {count} bytestreams, its prototype {streams.Length}.");
                    var packet = new byte[length];
                    paged.Read(at, packet);
                    var o = 6 + 2 * count;
                    for (var k = 0; k < count; k++)
                    {
                        var n = BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(6 + 2 * k));
                        streams[k].Append(packet.AsSpan(o, n));
                        o += n;
                    }
                    var ready = Math.Min(scan.Count - read, streams.Min(x => x.Available));
                    for (long r = 0; r < ready; r++)
                    {
                        for (var k = 0; k < streams.Length; k++) values[k] = streams[k].Next();
                        read++;
                        if (ic >= 0 && values[ic] >= 2 || isp >= 0 && values[isp] >= 2) continue;
                        double x, y, z;
                        if (cartesian) (x, y, z) = (values[ix], values[iy], values[iz]);
                        else
                        {
                            var (rg, az, el) = (values[ir], values[ia], values[ie]);
                            (x, y, z) = (rg * Math.Cos(el) * Math.Cos(az), rg * Math.Cos(el) * Math.Sin(az), rg * Math.Sin(el));
                        }
                        var (X, Y, Z) = (R[0] * x + R[1] * y + R[2] * z + t[0], R[3] * x + R[4] * y + R[5] * z + t[1], R[6] * x + R[7] * y + R[8] * z + t[2]);
                        ushort Col(int idx) => idx < 0 ? (ushort)0 : (ushort)Math.Clamp(Math.Round((values[idx] - scan.CMin) / Math.Max(scan.CMax - scan.CMin, 1e-9) * 255), 0, 255);
                        var intensity = ii < 0 ? (ushort)0 : (ushort)Math.Clamp(Math.Round((values[ii] - scan.IMin) / Math.Max(scan.IMax - scan.IMin, 1e-12) * 65535), 0, 65535);
                        if (b.Full) { yield return b; b.Count = 0; }
                        b.Add(X, Y, Z, Col(icr), Col(icg), Col(icb), intensity, 0);
                    }
                }
                else if (type is not (0 or 2)) throw new PointCloudException($"{_source.Name}: a packet of {scan.Name} is of a kind E57 does not have ({type}).");
                at += length;
            }
        }
        if (b.Count > 0) yield return b;
    }
}
