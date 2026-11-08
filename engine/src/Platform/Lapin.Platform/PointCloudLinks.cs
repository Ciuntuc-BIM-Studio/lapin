using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.PointClouds;

namespace Lapin.Platform;

/// <summary>
/// Point clouds as links (docs/point-clouds.md): a <see cref="Links.Class"/> of kind <c>pointcloud</c> is all the model
/// holds of a scan. Its points stay in their file and in an octree of tiles indexed once into the app's cache
/// (<see cref="CacheRoot"/>), in the background; the 3D view streams the tiles it needs, plans and sections draw the slice
/// their cut takes. The link says where the cloud stands (<see cref="TransformOf"/>: by the project's map coordinates, origin
/// to origin, or centred, in its file's unit, then moved and turned) and how it is drawn (colour by RGB, intensity, elevation
/// or class, the point size).
/// </summary>
public static class PointCloudLinks
{
    public const string Kind = "pointcloud";

    /// <summary>The folder the host keeps the indexes in (its user folder's <c>PointClouds</c>); null: <see cref="CacheRoot"/>'s default.</summary>
    public static string? CacheDir { get; set; }

    /// <summary>Where the indexes are kept: <c>LAPIN_POINTCLOUD_CACHE</c>, else the host's folder, else the user's local application data (never the project).</summary>
    public static string CacheRoot
    {
        get
        {
            if (Environment.GetEnvironmentVariable("LAPIN_POINTCLOUD_CACHE") is { Length: > 0 } env) return env;
            if (CacheDir is { Length: > 0 } dir) return dir;
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(local is { Length: > 0 } ? local : Path.GetTempPath(), "Lapin", "PointClouds");
        }
    }

    /// <summary>A file's bytes read in a browser, kept for the session so its cloud can be indexed (it cannot be read again).</summary>
    static readonly ConcurrentDictionary<string, byte[]> _bytes = new();
    static readonly ConcurrentDictionary<string, Job> _jobs = new();
    static readonly ConcurrentDictionary<string, PointStore> _stores = new();

    /// <summary>An index being made: its stage and progress, or why it failed.</summary>
    sealed class Job
    {
        public volatile string Stage = "Starting";
        public double Done;
        public string? Error;
        public bool Finished;
    }

    public static bool Is(Entity e) => e.Class == Links.Class && e.Str("kind") == Kind;
    public static IEnumerable<Entity> All(IModelView m) => Links.All(m).Where(Is);

    /// <summary>The key of a file by its path, length and time (a change to the file makes a new index).</summary>
    public static string KeyOfPath(string path)
    {
        var info = new FileInfo(path);
        return Hash($"{Path.GetFullPath(path)}|{info.Length}|{info.LastWriteTimeUtc.Ticks}");
    }

    public static string KeyOfBytes(string name, byte[] bytes)
    {
        var h = SHA256.HashData(bytes);
        return Hash($"{name}|{bytes.Length}|{Convert.ToHexString(h)}");
    }

    static string Hash(string s) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)))[..24].ToLowerInvariant();

    static string DirOf(string key) => Path.Combine(CacheRoot, key);

    /// <summary>The cloud's source as the link has it: its path, or its bytes kept for this session; null when it cannot be read now.</summary>
    static IPointSource? SourceOf(Entity link)
    {
        if (link.Str("key") is { } key && _bytes.TryGetValue(key, out var b)) return new BytesSource(link.Str("file") ?? "cloud", b);
        if (link.Str("path") is { } path && File.Exists(path)) return new FileSource(path);
        return null;
    }

    /// <summary>The link's index when it is ready, else null.</summary>
    public static PointStore? StoreOf(Entity link)
    {
        if (link.Str("key") is not { } key) return null;
        if (_stores.TryGetValue(key, out var s)) return s;
        var opened = PointStore.Open(DirOf(key));
        if (opened == null) return null;
        return _stores.GetOrAdd(key, opened);
    }

    /// <summary>
    /// The state of a link's index: <c>ready</c>, <c>indexing</c> (with its stage and progress), <c>failed</c> (why), or
    /// <c>missing</c> (no index here and its file cannot be read: linked from a browser in another session, or moved). An
    /// index that is neither here nor being made is started when its file can be read.
    /// </summary>
    public static JsonObject Status(Entity link, IDeliveries? deliveries = null)
    {
        var key = link.Str("key") ?? "";
        if (StoreOf(link) is { } store) return new JsonObject { ["state"] = "ready", ["points"] = store.Points, ["nodes"] = store.Nodes.Count };
        if (_jobs.TryGetValue(key, out var job))
        {
            if (job.Error != null) return new JsonObject { ["state"] = "failed", ["error"] = job.Error };
            if (!job.Finished) return new JsonObject { ["state"] = "indexing", ["stage"] = job.Stage, ["done"] = Math.Round(job.Done, 3) };
        }
        if (SourceOf(link) is { } source)
        {
            Start(link, source, key, deliveries);
            return _jobs.TryGetValue(key, out var j) && j.Error != null
                ? new JsonObject { ["state"] = "failed", ["error"] = j.Error }
                : StoreOf(link) != null ? new JsonObject { ["state"] = "ready" } : new JsonObject { ["state"] = "indexing", ["stage"] = "Starting", ["done"] = 0 };
        }
        return new JsonObject { ["state"] = "missing", ["error"] = link.Str("path") is { } p ? $"The file is gone: {p}." : "The cloud was linked from a browser in another session: link it again." };
    }

    /// <summary>Indexing started for a link (once a key): in the background where there are threads, at once in a browser.</summary>
    static void Start(Entity link, IPointSource source, string key, IDeliveries? deliveries)
    {
        var job = new Job();
        if (!_jobs.TryAdd(key, job)) return;
        var stamp = deliveries?.Stamp();
        var linkId = link.Id;
        void Work()
        {
            try
            {
                var reader = PointReaders.Open(source);
                var store = Indexer.Build(reader, DirOf(key), key, source.Length, (stage, done) => { job.Stage = stage; job.Done = done; });
                _stores[key] = store;
                job.Finished = true;
                if (deliveries != null && stamp is { } st)
                    deliveries.Deliver("Index point cloud", st, ctx =>
                    {
                        if (ctx.Model.Get(linkId) is not { } l || !Is(l)) return;
                        var attrs = l.CloneAttrs();
                        attrs["cloud"] = Summary(store);
                        ctx.Replace(l.With(attrs: attrs));
                    }, [linkId]);
            }
            catch (Exception e) when (e is PointCloudException or IOException or InvalidDataException or UnauthorizedAccessException or EndOfStreamException or OverflowException or IndexOutOfRangeException or ArgumentException)
            {
                job.Error = e is PointCloudException ? e.Message : $"The cloud could not be indexed: {e.Message}";
                job.Finished = true;
            }
        }
        if (OperatingSystem.IsBrowser()) Work();
        else Task.Run(Work);
    }

    /// <summary>What the link records of its index: points, box, what each point carries, the classes, intensity's range.</summary>
    public static JsonObject Summary(PointStore s)
    {
        var classes = new JsonObject();
        for (var c = 0; c < 256; c++) if (s.Classes[c] > 0) classes[c.ToString()] = s.Classes[c];
        return new JsonObject
        {
            ["format"] = s.Format, ["points"] = s.Points, ["bounds"] = new JsonArray(s.Bounds.Select(v => (JsonNode)Math.Round(v, 4)).ToArray()),
            ["color"] = s.HasColor, ["intensity"] = s.HasIntensity, ["classification"] = s.HasClassification, ["classes"] = classes,
            ["intensityRange"] = new JsonArray(s.IntensityMin, s.IntensityMax), ["crs"] = s.Crs, ["units"] = s.Units,
        };
    }

    // ─── Where the cloud stands ───

    /// <summary>A unit's metres.</summary>
    public static double MetresOf(string? unit) => unit switch
    {
        "mm" => 0.001, "cm" => 0.01, "dm" => 0.1, "ft" => 0.3048, "usft" => 1200.0 / 3937.0, "in" => 0.0254, _ => 1,
    };

    public static readonly string[] Units = ["m", "cm", "mm", "ft", "usft", "in"];
    public static readonly string[] Modes = ["shared", "origin", "center"];

    /// <summary>
    /// The file's coordinates into the project's (metres, the plan's axes): an affine map, 12 numbers (the images of the
    /// file's X, Y and Z axes, then of its origin). <c>shared</c>: the file's x and y are the project's map coordinates
    /// (eastings, northings; through the survey point and the angle to north), its z above the survey point's elevation;
    /// <c>origin</c>: the file's origin on the project's; <c>center</c>: the cloud's middle on the project's origin, its
    /// lowest point at ±0.00. Then turned by <c>rotation</c> (degrees, about the project's origin) and moved by <c>offset</c>.
    /// </summary>
    public static double[] TransformOf(IModelView m, Entity link)
    {
        var p = link.Attrs["placement"] as JsonObject ?? [];
        var k = MetresOf((string?)p["units"]);
        var mode = (string?)p["mode"] ?? "origin";
        var bounds = BoundsOf(link);
        var centre = bounds != null ? new[] { (bounds[0] + bounds[3]) / 2, (bounds[1] + bounds[4]) / 2, (bounds[2] + bounds[5]) / 2 } : new double[3];
        Func<double, double, double, (double X, double Y, double Z)> map = mode switch
        {
            "shared" when GeoRef.Georeferenced(m) => (x, y, z) =>
            {
                var plan = GeoRef.ToPlan(m, x * k, y * k);
                return (plan.X, plan.Y, z * k - (Location.Survey(m)?.Elevation ?? 0));
            },
            "center" when bounds != null => (x, y, z) => ((x - centre[0]) * k, (y - centre[1]) * k, (z - bounds[2]) * k),
            _ => (x, y, z) => (x * k, y * k, z * k),
        };
        // The map's linear part by differences about the cloud's middle (exact: every mode is affine).
        var o = map(centre[0], centre[1], centre[2]);
        var ax = map(centre[0] + 1, centre[1], centre[2]);
        var ay = map(centre[0], centre[1] + 1, centre[2]);
        var az = map(centre[0], centre[1], centre[2] + 1);
        double[] X = [ax.X - o.X, ax.Y - o.Y, ax.Z - o.Z], Y = [ay.X - o.X, ay.Y - o.Y, ay.Z - o.Z], Z = [az.X - o.X, az.Y - o.Y, az.Z - o.Z];
        double[] O = [o.X - X[0] * centre[0] - Y[0] * centre[1] - Z[0] * centre[2], o.Y - X[1] * centre[0] - Y[1] * centre[1] - Z[1] * centre[2], o.Z - X[2] * centre[0] - Y[2] * centre[1] - Z[2] * centre[2]];
        // Turned about the project's origin, then moved.
        var r = (Json.Number(p["rotation"]) ?? 0) * Math.PI / 180;
        var (c, s) = (Math.Cos(r), Math.Sin(r));
        double[] Turn(double[] v) => [c * v[0] - s * v[1], s * v[0] + c * v[1], v[2]];
        var off = p["offset"] as JsonArray;
        double Off(int i) => off != null && off.Count > i ? Json.Number(off[i]) ?? 0 : 0;
        var (tx, ty, tz) = (Turn(X), Turn(Y), Turn(Z));
        var to = Turn(O);
        return [.. tx, .. ty, .. tz, to[0] + Off(0), to[1] + Off(1), to[2] + Off(2)];
    }

    /// <summary>The cloud's box in its file (min x, y, z, max x, y, z): its index's, else what the file's header said.</summary>
    public static double[]? BoundsOf(Entity link)
    {
        if (StoreOf(link) is { } s) return s.Bounds;
        return (link.Attrs["cloud"] as JsonObject)?["bounds"] is JsonArray { Count: 6 } b ? b.Select(v => Json.Number(v) ?? 0).ToArray() : null;
    }

    public static (double X, double Y, double Z) Apply(double[] t, double x, double y, double z) =>
        (t[0] * x + t[3] * y + t[6] * z + t[9], t[1] * x + t[4] * y + t[7] * z + t[10], t[2] * x + t[5] * y + t[8] * z + t[11]);

    /// <summary>A box of the file (min, max) as the project's axis-aligned box round its eight corners.</summary>
    public static double[] BoxOf(double[] t, double x0, double y0, double z0, double x1, double y1, double z1)
    {
        double[] b = [double.MaxValue, double.MaxValue, double.MaxValue, double.MinValue, double.MinValue, double.MinValue];
        for (var i = 0; i < 8; i++)
        {
            var p = Apply(t, (i & 1) != 0 ? x1 : x0, (i & 2) != 0 ? y1 : y0, (i & 4) != 0 ? z1 : z0);
            b[0] = Math.Min(b[0], p.X); b[1] = Math.Min(b[1], p.Y); b[2] = Math.Min(b[2], p.Z);
            b[3] = Math.Max(b[3], p.X); b[4] = Math.Max(b[4], p.Y); b[5] = Math.Max(b[5], p.Z);
        }
        return b;
    }

    /// <summary>
    /// How a cloud is placed when the user has not said: by the map when the file names a system or its coordinates look
    /// like map coordinates and the project is tied to the map; centred when its coordinates are far from the origin; else
    /// origin to origin. Its unit is the file's when it says one, else metres.
    /// </summary>
    public static JsonObject DefaultPlacement(IModelView m, PointCloudHeader h)
    {
        var far = h.Bounds is { } b && (Math.Abs(b[0]) > 10_000 || Math.Abs(b[1]) > 10_000 || Math.Abs(b[3]) > 10_000 || Math.Abs(b[4]) > 10_000);
        var mode = GeoRef.Georeferenced(m) && (h.Crs != null || far) ? "shared" : far || h.Bounds == null && h.Crs != null ? "center" : "origin";
        return new JsonObject { ["mode"] = mode, ["units"] = h.Units ?? "m", ["offset"] = new JsonArray(0, 0, 0), ["rotation"] = 0 };
    }

    /// <summary>
    /// The size check (the units): the cloud's box in metres under its unit, and a warning when that is unlikely for a scan
    /// of a building or a site (under 20 cm, or over 50 km) or when the file's own unit differs from the one chosen.
    /// </summary>
    public static string? UnitWarning(double[]? bounds, string units, string? fileUnits)
    {
        if (fileUnits != null && fileUnits != units) return $"The file says its coordinates are in {UnitName(fileUnits)}; the cloud is placed as {UnitName(units)}.";
        if (bounds == null) return null;
        var k = MetresOf(units);
        var size = Math.Max(Math.Max(bounds[3] - bounds[0], bounds[4] - bounds[1]), bounds[5] - bounds[2]) * k;
        if (size < 0.2) return $"The cloud is {size * 1000:0} mm across in {UnitName(units)}: check its unit.";
        if (size > 50_000) return $"The cloud is {size / 1000:0} km across in {UnitName(units)}: check its unit.";
        return null;
    }

    public static string UnitName(string u) => u switch { "mm" => "millimetres", "cm" => "centimetres", "dm" => "decimetres", "ft" => "feet", "usft" => "US survey feet", "in" => "inches", _ => "metres" };

    // ─── Tiles ───

    /// <summary>
    /// The cloud's octree for the 3D view: each node's name, points, box in the project (min x, y, z, max x, y, z) and the
    /// spacing its sample keeps (m); with the cloud's range of heights and intensity for the colours.
    /// </summary>
    public static JsonObject? Hierarchy(IModelView m, Entity link)
    {
        if (StoreOf(link) is not { } s) return null;
        var t = TransformOf(m, link);
        var scale = Math.Sqrt(t[0] * t[0] + t[1] * t[1] + t[2] * t[2]);
        var nodes = new JsonArray();
        foreach (var n in s.Nodes)
        {
            var (x, y, z, e) = s.CubeOf(n.Name);
            var b = BoxOf(t, x, y, z, x + e, y + e, z + e);
            nodes.Add((JsonNode)new JsonArray(n.Name, n.Count, R(b[0]), R(b[1]), R(b[2]), R(b[3]), R(b[4]), R(b[5]), Math.Round(e / PointStore.Resolution * scale, 5)));
        }
        var all = BoxOf(t, s.Bounds[0], s.Bounds[1], s.Bounds[2], s.Bounds[3], s.Bounds[4], s.Bounds[5]);
        return new JsonObject
        {
            ["id"] = link.Id, ["points"] = s.Points, ["nodes"] = nodes, ["box"] = new JsonArray(all.Select(v => (JsonNode)R(v)).ToArray()),
            ["intensityRange"] = new JsonArray(s.IntensityMin, s.IntensityMax), ["color"] = s.HasColor, ["intensity"] = s.HasIntensity, ["classification"] = s.HasClassification,
        };
        static JsonNode R(double v) => Math.Round(v, 4);
    }

    /// <summary>Tiles of a cloud by name, in the project's coordinates; a name the cloud lacks is left out.</summary>
    public static List<PointTile> Tiles(IModelView m, Entity link, IEnumerable<string> names)
    {
        var s = StoreOf(link) ?? throw new CommandException("The point cloud is not indexed yet.");
        var t = TransformOf(m, link);
        var o = new List<PointTile>();
        foreach (var name in names.Take(64))
        {
            if (s.Get(name) == null) continue;
            var p = s.Read(name);
            var pos = new float[3 * p.Count];
            var col = new int[p.Count];
            var val = new int[p.Count];
            for (var i = 0; i < p.Count; i++)
            {
                var (x, y, z) = Apply(t, p.X(i), p.Y(i), p.Z(i));
                pos[3 * i] = (float)x; pos[3 * i + 1] = (float)y; pos[3 * i + 2] = (float)z;
                col[i] = (p.R(i) << 16) | (p.G(i) << 8) | p.B(i);
                val[i] = p.Intensity(i) | (p.Class(i) << 16);
            }
            o.Add(new PointTile(link.Id, name, pos, col, val));
        }
        return o;
    }

    // ─── The link ───

    /// <summary>A cloud linked: by its path (the desktop) or its bytes (a browser); its header read, its index started.</summary>
    public static JsonObject Link(ICommandContext ctx, JsonObject a)
    {
        IPointSource source;
        string key;
        string? path = null;
        if ((string?)a["data"] is { Length: > 0 } data)
        {
            byte[] bytes;
            try { bytes = Convert.FromBase64String(data); }
            catch (FormatException) { throw new CommandException("The file's data is not base64."); }
            var name = (string?)a["name"] ?? "cloud.las";
            source = new BytesSource(name, bytes);
            key = KeyOfBytes(name, bytes);
            _bytes[key] = bytes;
        }
        else
        {
            path = HostFiles.Check((string?)a["path"] ?? throw new CommandException("Which file?"));
            if (!File.Exists(path)) throw new CommandException($"There is no file at {path}.");
            source = new FileSource(path);
            key = KeyOfPath(path);
        }
        PointCloudHeader header;
        try { header = PointReaders.Open(source).Header; }
        catch (PointCloudException e) { throw new CommandException(e.Message); }
        catch (Exception e) when (e is IOException or EndOfStreamException or ArgumentException) { throw new CommandException($"{source.Name} could not be read: {e.Message}"); }

        var placement = DefaultPlacement(ctx.Model, header);
        if (a["placement"] is JsonObject asked) foreach (var (k, v) in asked) placement[k] = v?.DeepClone();
        CheckPlacement(placement);
        var link = Links.Create(ctx, Kind, source.Name, path);
        var attrs = link.CloneAttrs();
        attrs["key"] = key;
        attrs["placement"] = placement;
        attrs["colorMode"] = header.HasColor ? "rgb" : header.HasIntensity ? "intensity" : "elevation";
        attrs["pointSize"] = 2;
        attrs["cloud"] = new JsonObject
        {
            ["format"] = header.Format, ["points"] = header.Count, ["color"] = header.HasColor, ["intensity"] = header.HasIntensity,
            ["classification"] = header.HasClassification, ["crs"] = header.Crs, ["units"] = header.Units,
            ["bounds"] = header.Bounds is { } hb ? new JsonArray(hb.Select(v => (JsonNode)Math.Round(v, 4)).ToArray()) : null,
        };
        var named = ((string?)a["name"])?.Trim() is { Length: > 0 } nm ? Path.GetFileNameWithoutExtension(nm) : link.Name;
        ctx.Replace(link.With(name: named, attrs: attrs));
        var status = Status(ctx.Model.Get(link.Id)!, ctx.Deliveries);
        var notes = new JsonArray(header.Notes.Select(n => (JsonNode)n).ToArray());
        if (UnitWarning(header.Bounds, (string?)placement["units"] ?? "m", header.Units) is { } w) notes.Add((JsonNode)w);
        return new JsonObject
        {
            ["id"] = link.Id, ["format"] = header.Format, ["points"] = header.Count, ["crs"] = header.Crs, ["units"] = header.Units,
            ["placement"] = placement.DeepClone(), ["status"] = status, ["notes"] = notes,
        };
    }

    static void CheckPlacement(JsonObject p)
    {
        if ((string?)p["mode"] is { } mode && !Modes.Contains(mode)) throw new CommandException($"A cloud is placed by {string.Join(", ", Modes)}.");
        if ((string?)p["units"] is { } u && !Units.Contains(u)) throw new CommandException($"A cloud's unit is one of {string.Join(", ", Units)}.");
        if (p["offset"] is JsonArray off && (off.Count != 3 || off.Any(v => Json.Number(v) is not { } n || !double.IsFinite(n) || Math.Abs(n) > 1e7)))
            throw new CommandException("A cloud's offset is three distances (x, y, z) within 10 000 km.");
    }

    public static readonly string[] ColorModes = ["rgb", "intensity", "elevation", "classification"];

    /// <summary>The slice a plan or a section draws of a cloud when the view does not say (m): 10 cm round the cut.</summary>
    public const double DefaultSlice = 0.10;

    /// <summary>The thickest slice (m): an airborne scan's facades need metres of it, a whole building's height (decision 399).</summary>
    public const double MaxSlice = 100;

    /// <summary>How a cloud is drawn in one view: its link's settings, then the view's row over them (decision 396).</summary>
    public sealed record Look(bool Shown, bool Halftone, string ColorMode, IReadOnlySet<int> HiddenClasses, double Slice, double PointSize);

    public static Look LookIn(JsonObject? graphics, Entity link)
    {
        var row = graphics?["pointClouds"]?[link.Id] as JsonObject;
        static bool T(JsonNode? n) => n is JsonValue v && v.TryGetValue<bool>(out var b) && b;
        var mode = (string?)row?["colorMode"] is { } m && ColorModes.Contains(m) ? m : link.Str("colorMode") is { } lm && ColorModes.Contains(lm) ? lm : "rgb";
        var hidden = (row?["hiddenClasses"] as JsonArray)?.Select(x => (int?)Json.Number(x)).OfType<int>().ToHashSet() ?? [];
        var slice = Json.Number(row?["slice"]) is { } sl && sl > 0 ? sl : DefaultSlice;
        return new Look(Links.Visible(link) && !T(row?["hidden"]), Links.IsHalftone(link) || T(row?["halftone"]), mode, hidden, slice, link.NumOrNull("pointSize") ?? 2);
    }

    /// <summary>A view's row for a cloud, checked: {hidden, halftone, colorMode, hiddenClasses: [0–255…], slice: m}; null when it says nothing.</summary>
    public static JsonObject? CheckViewRow(JsonObject? v)
    {
        if (v == null) return null;
        var o = new JsonObject();
        foreach (var (k, val) in v)
            switch (k)
            {
                case "hidden" or "halftone":
                    if (val is JsonValue b && b.TryGetValue<bool>(out var flag) && flag) o[k] = true;
                    break;
                case "colorMode":
                    if ((string?)val is { } m) o[k] = ColorModes.Contains(m) ? m : throw new CommandException($"A cloud is coloured by {string.Join(", ", ColorModes)}.");
                    break;
                case "hiddenClasses":
                    if (val is JsonArray a)
                    {
                        var list = a.Select(x => Json.Number(x)).Select(x => x is { } n && n >= 0 && n <= 255 && n == Math.Floor(n) ? (int)n : throw new CommandException("A class is a number from 0 to 255.")).Distinct().Order().ToList();
                        if (list.Count > 0) o[k] = new JsonArray(list.Select(x => (JsonNode)x).ToArray());
                    }
                    break;
                case "slice":
                    if (Json.Number(val) is { } t) o[k] = t is > 0.001 and <= MaxSlice ? t : throw new CommandException("A cloud's slice is between 1 mm and 100 m thick.");
                    break;
                default: throw new CommandException($"„{k}” is not a point cloud's setting.");
            }
        return o.Count > 0 ? o : null;
    }

    /// <summary>What Visibility / Graphics lists of a cloud: its name, whether it is shown and halftone everywhere, its colours, the classes its points have.</summary>
    public static JsonObject VisibilityInfo(Entity link)
    {
        var cloud = link.Attrs["cloud"] as JsonObject;
        // The classes from the index when it is here (the link's summary may not have been delivered yet), else the summary's.
        var classes = StoreOf(link) is { } store ? Enumerable.Range(0, 256).Where(c => store.Classes[c] > 0).ToList()
            : (cloud?["classes"] as JsonObject)?.Select(kv => int.TryParse(kv.Key, out var c) ? c : -1).Where(c => c >= 0).Order().ToList() ?? [];
        return new JsonObject
        {
            ["id"] = link.Id, ["name"] = link.Name, ["visible"] = Links.Visible(link), ["halftone"] = Links.IsHalftone(link), ["colorMode"] = link.Str("colorMode") ?? "rgb",
            ["color"] = (bool?)cloud?["color"] ?? false, ["intensity"] = (bool?)cloud?["intensity"] ?? false, ["classification"] = (bool?)cloud?["classification"] ?? false,
            ["classes"] = new JsonArray(classes.Select(c => (JsonNode)c).ToArray()),
        };
    }

    /// <summary>
    /// A cloud's points for an export, in the project's coordinates: its octree read from the root down, level by level,
    /// until <paramref name="max"/> points (a coarser sample of the whole, never a corner of it); classes hidden left out.
    /// </summary>
    public static (float[] Positions, byte[] Colors) Sample(IModelView m, Entity link, Look look, int max)
    {
        if (StoreOf(link) is not { } s) return ([], []);
        var t = TransformOf(m, link);
        var byDepth = s.Nodes.GroupBy(n => n.Name.Length).OrderBy(g => g.Key);
        var pos = new List<float>();
        var col = new List<byte>();
        foreach (var level in byDepth)
        {
            if (col.Count / 3 + level.Sum(n => (long)n.Count) > max && col.Count > 0) break;
            foreach (var n in level)
            {
                var p = s.Read(n.Name);
                for (var i = 0; i < p.Count; i++)
                {
                    if (look.HiddenClasses.Contains(p.Class(i))) continue;
                    var (x, y, z) = Apply(t, p.X(i), p.Y(i), p.Z(i));
                    pos.Add((float)x); pos.Add((float)y); pos.Add((float)z);
                    var c = s.HasColor ? (p.R(i), p.G(i), p.B(i)) : ((byte)200, (byte)200, (byte)200);
                    col.Add(c.Item1); col.Add(c.Item2); col.Add(c.Item3);
                }
            }
        }
        return ([.. pos], [.. col]);
    }

    public static void Register(IModuleRegistry r)
    {
        r.Command("pointcloud.link", "Link point cloud", (ctx, a) => Link(ctx, a));

        // Where it stands: by the map, origin to origin or centred, its unit, moved and turned.
        r.Command("pointcloud.place", "Place point cloud", (ctx, a) =>
        {
            var link = Links.Require(ctx.Model, (string?)a["id"]);
            if (!Is(link)) throw new CommandException("That link is not a point cloud.");
            var attrs = link.CloneAttrs();
            var p = (attrs["placement"] as JsonObject)?.DeepClone() as JsonObject ?? new JsonObject();
            foreach (var k in new[] { "mode", "units", "offset", "rotation" }) if (a.ContainsKey(k)) p[k] = a[k]?.DeepClone();
            if (Json.Number(p["rotation"]) is { } rot && !double.IsFinite(rot)) throw new CommandException("A cloud's rotation is a number of degrees.");
            CheckPlacement(p);
            attrs["placement"] = p;
            ctx.Replace(link.With(attrs: attrs));
            return null;
        });

        // How it is drawn: colours by RGB, intensity, elevation or class; the point size (px).
        r.Command("pointcloud.update", "Change point cloud", (ctx, a) =>
        {
            var link = Links.Require(ctx.Model, (string?)a["id"]);
            if (!Is(link)) throw new CommandException("That link is not a point cloud.");
            var attrs = link.CloneAttrs();
            if ((string?)a["colorMode"] is { } mode)
                attrs["colorMode"] = ColorModes.Contains(mode) ? mode : throw new CommandException($"A cloud is coloured by {string.Join(", ", ColorModes)}.");
            if (Json.Number(a["pointSize"]) is { } size) attrs["pointSize"] = Math.Clamp(Math.Round(size, 1), 1, 12);
            ctx.Replace(link.With(attrs: attrs));
            return null;
        });

        // Read again from its file (the desktop): a file changed since makes a new index; one unchanged keeps its own.
        r.Command("pointcloud.reload", "Reload point cloud", (ctx, a) =>
        {
            var link = Links.Require(ctx.Model, (string?)a["id"]);
            if (!Is(link)) throw new CommandException("That link is not a point cloud.");
            var path = HostFiles.Check(link.Str("path") ?? throw new CommandException("This cloud has no file to read again (it was linked from a browser)."));
            if (!File.Exists(path)) throw new CommandException($"The file is gone: {path}.");
            var key = KeyOfPath(path);
            if (key == link.Str("key")) return new JsonObject { ["changed"] = false };
            var attrs = link.CloneAttrs();
            attrs["key"] = key;
            ctx.Replace(link.With(attrs: attrs));
            Status(ctx.Model.Get(link.Id)!, ctx.Deliveries);
            return new JsonObject { ["changed"] = true };
        });

        r.Query("pointcloud.status", (q, a) => Status(Links.Require(q.Model, (string?)a["id"]), q.Deliveries));
        r.Query("pointcloud.hierarchy", (q, a) => Hierarchy(q.Model, Links.Require(q.Model, (string?)a["id"])));
        // The size check of a placement before it is chosen: the box in metres and any warning.
        r.Query("pointcloud.check", (q, a) =>
        {
            var link = Links.Require(q.Model, (string?)a["id"]);
            var units = (string?)a["units"] ?? (string?)(link.Attrs["placement"] as JsonObject)?["units"] ?? "m";
            var b = BoundsOf(link);
            var k = MetresOf(units);
            return new JsonObject
            {
                ["size"] = b != null ? new JsonArray(Math.Round((b[3] - b[0]) * k, 3), Math.Round((b[4] - b[1]) * k, 3), Math.Round((b[5] - b[2]) * k, 3)) : null,
                ["warning"] = UnitWarning(b, units, (string?)(link.Attrs["cloud"] as JsonObject)?["units"]),
            };
        });
    }
}

/// <summary>A tile of a point cloud for the 3D view: its points in the project (x y z a point), colours (0xRRGGBB) and values (intensity | class « 16).</summary>
public sealed record PointTile(string Link, string Name, float[] Positions, int[] Colors, int[] Values);
