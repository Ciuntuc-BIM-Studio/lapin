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
    /// <summary>
    /// The indexes opened, apart so that nothing of Lapin.PointClouds is touched until a cloud is: the browser's engine
    /// downloads that assembly the first time it is needed (docs/plan-browser-engine.md, Lazy parts).
    /// </summary>
    static class Opened
    {
        public static readonly ConcurrentDictionary<string, PointStore> Stores = new();
        public static readonly ConcurrentDictionary<string, SplatStore> Splats = new();

        // Here, not in PointCloudLinks, for its lambdas over the index's nodes: a class's lambdas share one closure class,
        // whose fields (one delegate each) are all set up when any of them is first used (Register's, at start).
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
                        if (look.Crop is { } crop && !crop.Inside(x, y, z)) continue;
                        pos.Add((float)x); pos.Add((float)y); pos.Add((float)z);
                        var c = s.HasColor ? (p.R(i), p.G(i), p.B(i)) : ((byte)200, (byte)200, (byte)200);
                        col.Add(c.Item1); col.Add(c.Item2); col.Add(c.Item3);
                    }
                }
            }
            return ([.. pos], [.. col]);
        }
    }

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
        if (Opened.Stores.TryGetValue(key, out var s)) return s;
        // A splat file's index made before Lapin kept its Gaussians (or without them) is made again.
        if (IsSplats(link) && !SplatStore.Exists(DirOf(key))) return null;
        var opened = PointStore.Open(DirOf(key));
        if (opened == null) return null;
        return Opened.Stores.GetOrAdd(key, opened);
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

    /// <summary>A key's failed indexing forgotten, so that linking or reloading its file tries it again.</summary>
    static void ForgetFailed(string key)
    {
        if (_jobs.TryGetValue(key, out var job) && job.Error != null) _jobs.TryRemove(new KeyValuePair<string, Job>(key, job));
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
                Opened.Stores[key] = store;
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
            // Whatever stops it, the job ends failed and says why: one left "indexing" for good would keep its key from being
            // indexed again in this session (forum topic 222). A failed one is tried again when the cloud is linked or reloaded.
            catch (Exception e)
            {
                job.Error = e is PointCloudException ? e.Message : $"The cloud could not be indexed: {e.Message}";
                job.Finished = true;
                if (e is not (PointCloudException or IOException or InvalidDataException or UnauthorizedAccessException or EndOfStreamException))
                    Console.Error.WriteLine($"[engine] Indexing {source.Name} failed: {e}");
            }
        }
        if (OperatingSystem.IsBrowser()) Work();
        else Task.Run(Work);
    }

    /// <summary>What the link records of its index: points, box, where they are dense, what each point carries, the classes, intensity's range.</summary>
    public static JsonObject Summary(PointStore s)
    {
        var classes = new JsonObject();
        for (var c = 0; c < 256; c++) if (s.Classes[c] > 0) classes[c.ToString()] = s.Classes[c];
        return new JsonObject
        {
            ["format"] = s.Format, ["points"] = s.Points, ["bounds"] = new JsonArray(s.Bounds.Select(v => (JsonNode)Math.Round(v, 4)).ToArray()),
            ["core"] = new JsonArray(s.Core.Select(v => (JsonNode)Math.Round(v, 4)).ToArray()),
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
        // Its unit's metres, times the factor a calibration found (decision 813): one scale for points, splats, slices and snaps.
        var k = MetresOf((string?)p["units"]) * ScaleOf(p);
        var mode = (string?)p["mode"] ?? "origin";
        // The file's axes stood upright first (its up axis onto z): what follows works on the upright file.
        var up = UpOf((string?)p["up"]);
        var bounds = BoundsOf(link) is { } fb ? BoxOf(up, fb[0], fb[1], fb[2], fb[3], fb[4], fb[5]) : null;
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
        // After the turn upright.
        var upright = Compose([.. X, .. Y, .. Z, .. O], up);
        X = upright[0..3]; Y = upright[3..6]; Z = upright[6..9]; O = upright[9..12];
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

    /// <summary>The factor a calibration wrote on a placement (1 when none).</summary>
    public static double ScaleOf(JsonObject? placement) => Json.Number(placement?["scale"]) is { } s && s > 0 && double.IsFinite(s) ? s : 1;

    /// <summary>The axes a cloud's file may have up: z (a scan's), y (SPZ's, three.js's, glTF's), −y (a 3D Gaussian splatting PLY's, COLMAP's).</summary>
    public static readonly string[] Ups = ["z", "y", "-y"];

    /// <summary>The turn that stands a file upright (its up axis onto z, a proper rotation), as a transform of 12 numbers.</summary>
    public static double[] UpOf(string? up) => up switch
    {
        // y up (x right, z back): x stays, −z is forward (the project's y), y is up.
        "y" => [1, 0, 0, 0, 0, 1, 0, -1, 0, 0, 0, 0],
        // y down (x right, z forward): x stays, z is forward, −y is up.
        "-y" => [1, 0, 0, 0, 0, -1, 0, 1, 0, 0, 0, 0],
        _ => [1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0],
    };

    /// <summary>Two transforms one after the other: <paramref name="first"/>, then <paramref name="then"/> (<paramref name="then"/> ∘ <paramref name="first"/>).</summary>
    public static double[] Compose(double[] then, double[] first)
    {
        var o = new double[12];
        for (var c = 0; c < 4; c++)
            for (var r = 0; r < 3; r++)
            {
                double v = 0;
                for (var k = 0; k < 3; k++) v += then[3 * k + r] * first[3 * c + k];
                o[3 * c + r] = v + (c == 3 ? then[9 + r] : 0);
            }
        return o;
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
        var o = new JsonObject { ["mode"] = mode, ["units"] = h.Units ?? "m", ["offset"] = new JsonArray(0, 0, 0), ["rotation"] = 0 };
        if (h.Up is { } up && up != "z") o["up"] = up;
        return o;
    }

    /// <summary>
    /// The size check (the units): the cloud's box in metres under its unit, and a warning when that is unlikely for a scan
    /// of a building or a site (under 20 cm, or over 50 km) or when the file's own unit differs from the one chosen.
    /// </summary>
    public static string? UnitWarning(double[]? bounds, string units, string? fileUnits, double scale = 1)
    {
        if (fileUnits != null && fileUnits != units && Math.Abs(scale - 1) < 1e-9) return $"The file says its coordinates are in {UnitName(fileUnits)}; the cloud is placed as {UnitName(units)}.";
        if (bounds == null) return null;
        // The calibration's factor too: a capture of unknown size calibrated to a true distance is as large as it says.
        var k = MetresOf(units) * scale;
        var size = Math.Max(Math.Max(bounds[3] - bounds[0], bounds[4] - bounds[1]), bounds[5] - bounds[2]) * k;
        if (size < 0.2) return $"The cloud is {size * 1000:0} mm across in {UnitName(units)}: check its unit.";
        if (size > 50_000) return $"The cloud is {size / 1000:0} km across in {UnitName(units)}: check its unit.";
        return null;
    }

    public static string UnitName(string u) => u switch { "mm" => "millimetres", "cm" => "centimetres", "dm" => "decimetres", "ft" => "feet", "usft" => "US survey feet", "in" => "inches", _ => "metres" };

    // ─── Tiles ───

    /// <summary>
    /// The cloud's octree for the 3D view: each node's name, points, box in the project (min x, y, z, max x, y, z) and the
    /// spacing its sample keeps (m); with the cloud's range of heights and intensity for the colours. <c>box</c> is round all of
    /// it, <c>core</c> round where its points are dense (<see cref="DenseBox"/>: what Fit frames), both inside the crop when it has one.
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
        var c = s.Core;
        var core = BoxOf(t, c[0], c[1], c[2], c[3], c[4], c[5]);
        // Cropped: what is kept is inside the crop's box too; a crop away from the dense part leaves the crop's box itself.
        if (CropOf(link)?.Box() is { } cb)
        {
            for (var k = 0; k < 3; k++) { all[k] = Math.Max(all[k], cb[k]); all[k + 3] = Math.Max(all[k], Math.Min(all[k + 3], cb[k + 3])); }
            for (var k = 0; k < 3; k++) { core[k] = Math.Max(core[k], all[k]); core[k + 3] = Math.Min(core[k + 3], all[k + 3]); }
            if (Enumerable.Range(0, 3).Any(k => core[k] > core[k + 3])) core = (double[])all.Clone();
        }
        return new JsonObject
        {
            ["id"] = link.Id, ["points"] = s.Points, ["nodes"] = nodes, ["box"] = new JsonArray(all.Select(v => (JsonNode)R(v)).ToArray()),
            ["core"] = new JsonArray(core.Select(v => (JsonNode)R(v)).ToArray()),
            ["intensityRange"] = new JsonArray(s.IntensityMin, s.IntensityMax), ["color"] = s.HasColor, ["intensity"] = s.HasIntensity, ["classification"] = s.HasClassification,
        };
        static JsonNode R(double v) => Math.Round(v, 4);
    }

    /// <summary>Tiles of a cloud by name, in the project's coordinates; a name the cloud lacks is left out.</summary>
    public static List<PointTile> Tiles(IModelView m, Entity link, IEnumerable<string> names)
    {
        var s = StoreOf(link) ?? throw new CommandException("The point cloud is not indexed yet.");
        var t = TransformOf(m, link);
        var crop = CropOf(link);
        var o = new List<PointTile>();
        foreach (var name in names.Take(64))
        {
            if (s.Get(name) == null) continue;
            var p = s.Read(name);
            var pos = new float[3 * p.Count];
            var col = new int[p.Count];
            var val = new int[p.Count];
            var n = 0;
            for (var i = 0; i < p.Count; i++)
            {
                var (x, y, z) = Apply(t, p.X(i), p.Y(i), p.Z(i));
                // Outside the crop: not sent (the 3D view, its snaps and picking never see it).
                if (crop != null && !crop.Inside(x, y, z)) continue;
                pos[3 * n] = (float)x; pos[3 * n + 1] = (float)y; pos[3 * n + 2] = (float)z;
                col[n] = (p.R(i) << 16) | (p.G(i) << 8) | p.B(i);
                val[n] = p.Intensity(i) | (p.Class(i) << 16);
                n++;
            }
            if (n < p.Count) { pos = pos[..(3 * n)]; col = col[..n]; val = val[..n]; }
            o.Add(new PointTile(link.Id, name, pos, col, val));
        }
        return o;
    }

    // ─── Gaussian splats ───

    /// <summary>The Gaussians of a splat link's file when its index is ready, else null.</summary>
    public static SplatStore? SplatsOf(Entity link)
    {
        if (!IsSplats(link) || StoreOf(link) == null || link.Str("key") is not { } key) return null;
        if (Opened.Splats.TryGetValue(key, out var s)) return s;
        return SplatStore.Open(DirOf(key)) is { } opened ? Opened.Splats.GetOrAdd(key, opened) : null;
    }

    /// <summary>How many of a file's records a page of <c>pointcloud.splats</c> goes through (8 MB of them).</summary>
    public const int SplatPageRecords = 262_144;

    /// <summary>
    /// A page of a splat link's Gaussians for the 3D view, in the project (docs/plan-gaussian-splats.md, G1): of the file's
    /// records from <paramref name="start"/>, those a budget of <paramref name="budget"/> keeps (the largest and most opaque
    /// first), each placed by the link: its centre by its transform, its rotation turned by the transform's, its scales times
    /// the transform's factor (its unit's).
    /// </summary>
    /// <remarks>
    /// G3: with <paramref name="shDegree"/> above 0, each kept Gaussian's spherical harmonics come too, at the degree asked or
    /// the file's, lowered until the cloud's kept Gaussians' coefficients fit in <paramref name="shBytes"/> (a byte each: the
    /// interface's texture); a byte each as <c>sh.bin</c> keeps them, coefficient by coefficient, colours inner, packed four to
    /// an int. They stay in the file's frame: <c>Turn</c> (a 3 × 3 matrix, column by column) turns a direction in the project
    /// into the file's, for the view-dependent colour.
    /// </remarks>
    public static SplatPage Splats(IModelView m, Entity link, long start, long budget, int shDegree = 0, long shBytes = long.MaxValue)
    {
        var s = SplatsOf(link) ?? throw new CommandException(IsSplats(link) ? "The splats are not indexed yet." : "That cloud holds no Gaussian splats.");
        var t = TransformOf(m, link);
        var (k, lq) = Turn(t);
        var (idx, bytes) = s.Read(start, SplatPageRecords, Math.Max(1, budget));
        var degree = shDegree > 0 ? SplatStore.DegreeFor(s.ShDegree, shDegree, s.Cut(Math.Max(1, budget)).Kept, shBytes) : 0;
        // Cropped: the Gaussians whose centres are outside left out.
        if (CropOf(link) is { } crop)
        {
            var from0 = Math.Clamp(start, 0, s.Count);
            idx = idx.Where(i =>
            {
                var g = SplatStore.Unpack(bytes.AsSpan((int)(i - from0) * SplatStore.BytesPerSplat, SplatStore.BytesPerSplat));
                var (x, y, z) = Apply(t, g.X, g.Y, g.Z);
                return crop.Inside(x, y, z);
            }).ToList();
        }
        var n = idx.Count;
        var pos = new float[3 * n];
        var scl = new float[3 * n];
        var rot = new float[4 * n];
        var col = new int[n];
        var from = Math.Clamp(start, 0, s.Count);
        for (var j = 0; j < n; j++)
        {
            var g = SplatStore.Unpack(bytes.AsSpan((int)(idx[j] - from) * SplatStore.BytesPerSplat, SplatStore.BytesPerSplat));
            var (x, y, z) = Apply(t, g.X, g.Y, g.Z);
            pos[3 * j] = (float)x; pos[3 * j + 1] = (float)y; pos[3 * j + 2] = (float)z;
            scl[3 * j] = (float)(g.S0 * k); scl[3 * j + 1] = (float)(g.S1 * k); scl[3 * j + 2] = (float)(g.S2 * k);
            var q = Multiply(lq, (g.Qw, g.Qx, g.Qy, g.Qz));
            rot[4 * j] = (float)q.W; rot[4 * j + 1] = (float)q.X; rot[4 * j + 2] = (float)q.Y; rot[4 * j + 3] = (float)q.Z;
            col[j] = g.R | g.G << 8 | g.B << 16 | g.A << 24;
        }
        int[]? sh = null;
        float[]? turn = null;
        if (degree > 0)
        {
            var raw = s.ReadSh(from, (int)Math.Min(SplatPageRecords, s.Count - from));
            var per = Splat.ShCoeffs(s.ShDegree) * 3;
            var take = Splat.ShCoeffs(degree) * 3;
            var packed = new byte[(n * take + 3) & ~3];
            if (raw.Length > 0)
                for (var j = 0; j < n; j++) raw.AsSpan((int)(idx[j] - from) * per, take).CopyTo(packed.AsSpan(j * take));
            else packed.AsSpan().Fill(Splat.ShByte(0));
            sh = new int[packed.Length / 4];
            Buffer.BlockCopy(packed, 0, sh, 0, packed.Length);
            // The project's directions into the file's: the inverse of the placement's linear part, its scale taken out.
            var inv = Invert(t);
            turn = new float[9];
            for (var i = 0; i < 9; i++) turn[i] = (float)(inv[i] * k);
        }
        var next = from + SplatPageRecords;
        return new SplatPage(link.Id, from, next < s.Count ? next : null, s.Count, s.Cut(Math.Max(1, budget)).Kept, pos, scl, rot, col, degree, sh, turn);
    }

    /// <summary>A transform's linear part as a factor and a turn (a unit quaternion w, x, y, z): k·R, k the cube root of its determinant.</summary>
    public static (double K, (double W, double X, double Y, double Z) Q) Turn(double[] t)
    {
        double M(int r, int c) => t[3 * c + r];
        var det = M(0, 0) * (M(1, 1) * M(2, 2) - M(1, 2) * M(2, 1)) - M(0, 1) * (M(1, 0) * M(2, 2) - M(1, 2) * M(2, 0)) + M(0, 2) * (M(1, 0) * M(2, 1) - M(1, 1) * M(2, 0));
        var k = Math.Cbrt(det);
        if (!(Math.Abs(k) > 1e-15)) return (1, (1, 0, 0, 0));
        double R(int r, int c) => M(r, c) / k;
        double w, x, y, z;
        var tr = R(0, 0) + R(1, 1) + R(2, 2);
        if (tr > 0) { var q = Math.Sqrt(tr + 1) * 2; w = 0.25 * q; x = (R(2, 1) - R(1, 2)) / q; y = (R(0, 2) - R(2, 0)) / q; z = (R(1, 0) - R(0, 1)) / q; }
        else if (R(0, 0) > R(1, 1) && R(0, 0) > R(2, 2)) { var q = Math.Sqrt(1 + R(0, 0) - R(1, 1) - R(2, 2)) * 2; w = (R(2, 1) - R(1, 2)) / q; x = 0.25 * q; y = (R(0, 1) + R(1, 0)) / q; z = (R(0, 2) + R(2, 0)) / q; }
        else if (R(1, 1) > R(2, 2)) { var q = Math.Sqrt(1 + R(1, 1) - R(0, 0) - R(2, 2)) * 2; w = (R(0, 2) - R(2, 0)) / q; x = (R(0, 1) + R(1, 0)) / q; y = 0.25 * q; z = (R(1, 2) + R(2, 1)) / q; }
        else { var q = Math.Sqrt(1 + R(2, 2) - R(0, 0) - R(1, 1)) * 2; w = (R(1, 0) - R(0, 1)) / q; x = (R(0, 2) + R(2, 0)) / q; y = (R(1, 2) + R(2, 1)) / q; z = 0.25 * q; }
        var n = Math.Sqrt(w * w + x * x + y * y + z * z);
        return (Math.Abs(k), (w / n, x / n, y / n, z / n));
    }

    /// <summary>Two turns, <paramref name="b"/> first (Hamilton's product a·b).</summary>
    public static (double W, double X, double Y, double Z) Multiply((double W, double X, double Y, double Z) a, (double W, double X, double Y, double Z) b) => (
        a.W * b.W - a.X * b.X - a.Y * b.Y - a.Z * b.Z,
        a.W * b.X + a.X * b.W + a.Y * b.Z - a.Z * b.Y,
        a.W * b.Y - a.X * b.Z + a.Y * b.W + a.Z * b.X,
        a.W * b.Z + a.X * b.Y - a.Y * b.X + a.Z * b.W);

    // ─── Calibration and crop (decision 813) ───

    /// <summary>A transform's inverse (both 12 numbers: the images of the axes, then of the origin).</summary>
    public static double[] Invert(double[] t)
    {
        double a = t[0], b = t[3], c = t[6], d = t[1], e = t[4], f = t[7], g = t[2], h = t[5], i = t[8];
        var det = a * (e * i - f * h) - b * (d * i - f * g) + c * (d * h - e * g);
        if (Math.Abs(det) < 1e-300) throw new CommandException("The cloud's placement cannot be undone (its scale is 0).");
        double[] m = [(e * i - f * h) / det, (f * g - d * i) / det, (d * h - e * g) / det, (c * h - b * i) / det, (a * i - c * g) / det, (b * g - a * h) / det, (b * f - c * e) / det, (c * d - a * f) / det, (a * e - b * d) / det];
        // m holds the inverse's columns (as t does); its origin is −M·o.
        var (ox, oy, oz) = (t[9], t[10], t[11]);
        return [.. m, -(m[0] * ox + m[3] * oy + m[6] * oz), -(m[1] * ox + m[4] * oy + m[7] * oz), -(m[2] * ox + m[5] * oy + m[8] * oz)];
    }

    /// <summary>
    /// A cloud scaled true (G2): two points picked on it (in the project, m) that are <paramref name="length"/> apart in truth.
    /// Its placement's factor is multiplied by the ratio, and its offset moved so that the first point stays where it was.
    /// </summary>
    public static JsonObject Calibrate(ICommandContext ctx, Entity link, double[] a, double[] b, double length)
    {
        var d = Math.Sqrt((b[0] - a[0]) * (b[0] - a[0]) + (b[1] - a[1]) * (b[1] - a[1]) + (b[2] - a[2]) * (b[2] - a[2]));
        if (!(d > 1e-9)) throw new CommandException("The two points are the same: pick two points apart.");
        if (!(length > 0) || !double.IsFinite(length)) throw new CommandException("Their true distance is a length above 0.");
        var ratio = length / d;
        var before = TransformOf(ctx.Model, link);
        var inFile = Apply(Invert(before), a[0], a[1], a[2]);
        var attrs = link.CloneAttrs();
        var p = (attrs["placement"] as JsonObject)?.DeepClone() as JsonObject ?? new JsonObject();
        var scale = ScaleOf(p) * ratio;
        p["scale"] = Math.Round(scale, 9);
        CheckPlacement(p);
        attrs["placement"] = p;
        var scaled = link.With(attrs: attrs);
        // The first point kept where it was: the offset takes what the new factor moved it by.
        var after = Apply(TransformOf(ctx.Model, scaled), inFile.X, inFile.Y, inFile.Z);
        var off = p["offset"] as JsonArray;
        double Off(int i) => off != null && off.Count > i ? Json.Number(off[i]) ?? 0 : 0;
        p["offset"] = new JsonArray(Math.Round(Off(0) + a[0] - after.X, 6), Math.Round(Off(1) + a[1] - after.Y, 6), Math.Round(Off(2) + a[2] - after.Z, 6));
        CheckPlacement(p);
        ctx.Replace(link.With(attrs: attrs));
        return new JsonObject { ["scale"] = p["scale"]?.DeepClone(), ["ratio"] = Math.Round(ratio, 9), ["warning"] = UnitWarning(BoundsOf(link), (string?)p["units"] ?? "m", null, scale) };
    }

    /// <summary>A link's crop when it has one: a box in the project (its middle, its sizes, m) turned about z by <c>rotation</c> degrees.</summary>
    public static CropBox? CropOf(Entity link) => CropBox.From(link.Attrs["crop"] as JsonObject);

    /// <summary>
    /// The box round a cloud in the project, its sides along the link's own turn (the crop "Fit to cloud" makes): round where
    /// its points are dense (<see cref="DenseBox"/>), its sides rounded outwards to a step of about a fiftieth of its size (1, 2
    /// or 5 × a power of ten); with <paramref name="whole"/>, round all of it, exactly.
    /// </summary>
    public static CropBox? Fit(IModelView m, Entity link, bool whole = false)
    {
        if (BoundsOf(link) is not { } fb) return null;
        var store = whole ? null : StoreOf(link);
        var dense = store != null;
        if (store != null) fb = store.Core;
        var t = TransformOf(m, link);
        var deg = Json.Number((link.Attrs["placement"] as JsonObject)?["rotation"]) ?? 0;
        var r = deg * Math.PI / 180;
        var (c, s) = (Math.Cos(r), Math.Sin(r));
        double[] lo = [double.MaxValue, double.MaxValue, double.MaxValue], hi = [double.MinValue, double.MinValue, double.MinValue];
        for (var i = 0; i < 8; i++)
        {
            var w = Apply(t, (i & 1) != 0 ? fb[3] : fb[0], (i & 2) != 0 ? fb[4] : fb[1], (i & 4) != 0 ? fb[5] : fb[2]);
            // Into the box's own axes (turned back by the link's turn).
            double[] q = [c * w.X + s * w.Y, -s * w.X + c * w.Y, w.Z];
            for (var k = 0; k < 3; k++) { lo[k] = Math.Min(lo[k], q[k]); hi[k] = Math.Max(hi[k], q[k]); }
        }
        if (dense)
        {
            var step = NiceStep(Math.Max(Math.Max(hi[0] - lo[0], hi[1] - lo[1]), hi[2] - lo[2]) / 50);
            for (var k = 0; k < 3; k++)
            {
                lo[k] = Math.Floor(lo[k] / step + 1e-6) * step;
                hi[k] = Math.Ceiling(hi[k] / step - 1e-6) * step;
            }
        }
        double mx = (lo[0] + hi[0]) / 2, my = (lo[1] + hi[1]) / 2;
        return new CropBox([Math.Round(c * mx - s * my, 4), Math.Round(s * mx + c * my, 4), Math.Round((lo[2] + hi[2]) / 2, 4)],
            [Math.Round(hi[0] - lo[0], 4), Math.Round(hi[1] - lo[1], 4), Math.Round(hi[2] - lo[2], 4)], deg);
    }

    /// <summary>The step of 1, 2 or 5 × a power of ten nearest above <paramref name="v"/> (1 mm at least).</summary>
    public static double NiceStep(double v)
    {
        if (!(v > 0.001)) return 0.001;
        var p = Math.Pow(10, Math.Floor(Math.Log10(v)));
        foreach (var k in new[] { 1.0, 2, 5, 10 }) if (k * p >= v - 1e-12) return k * p;
        return 10 * p;
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

        // A PLY of faces is a mesh (MeshSplatting's output, a photogrammetry model): imported as one unless its vertices are asked for.
        if (header.Faces > 0 && !(a["asPoints"] is JsonValue ap && ap.TryGetValue<bool>(out var asPoints) && asPoints))
            throw new CommandException($"{source.Name} holds a mesh ({header.Faces:N0} faces), not a point cloud: import it as a 3D mesh, or link its vertices as points.");
        var placement = DefaultPlacement(ctx.Model, header);
        if (a["placement"] is JsonObject asked) foreach (var (k, v) in asked) placement[k] = v?.DeepClone();
        CheckPlacement(placement);
        var link = Links.Create(ctx, Kind, source.Name, path);
        var attrs = link.CloneAttrs();
        attrs["key"] = key;
        attrs["placement"] = placement;
        attrs["colorMode"] = header.HasColor ? "rgb" : header.HasIntensity ? "intensity" : "elevation";
        attrs["pointSize"] = 2;
        // Gaussian splats (docs/plan-gaussian-splats.md): drawn as splats in 3D unless the link or the view says points.
        if (header.Splats) { attrs["splats"] = true; attrs["display"] = "splats"; }
        attrs["cloud"] = new JsonObject
        {
            ["format"] = header.Format, ["points"] = header.Count, ["color"] = header.HasColor, ["intensity"] = header.HasIntensity,
            ["classification"] = header.HasClassification, ["crs"] = header.Crs, ["units"] = header.Units,
            ["shDegree"] = header.Splats ? header.ShDegree : null,
            ["bounds"] = header.Bounds is { } hb ? new JsonArray(hb.Select(v => (JsonNode)Math.Round(v, 4)).ToArray()) : null,
        };
        var named = ((string?)a["name"])?.Trim() is { Length: > 0 } nm ? Path.GetFileNameWithoutExtension(nm) : link.Name;
        // A SOG folder's meta.json: the link takes the folder's name.
        if (path != null && Path.GetFileName(path).Equals("meta.json", StringComparison.OrdinalIgnoreCase) && Path.GetFileName(Path.GetDirectoryName(path)) is { Length: > 0 } folder) named = folder;
        ctx.Replace(link.With(name: named, attrs: attrs));
        ForgetFailed(key);
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
        if (p["scale"] is { } sc && (Json.Number(sc) is not { } sn || !(sn is >= 1e-6 and <= 1e6))) throw new CommandException("A cloud's scale is a factor between 0.000001 and 1 000 000.");
        if ((string?)p["up"] is { } up && !Ups.Contains(up)) throw new CommandException($"A cloud's up axis is one of {string.Join(", ", Ups)}.");
        if (p["offset"] is JsonArray off && (off.Count != 3 || off.Any(v => Json.Number(v) is not { } n || !double.IsFinite(n) || Math.Abs(n) > 1e7)))
            throw new CommandException("A cloud's offset is three distances (x, y, z) within 10 000 km.");
    }

    public static readonly string[] ColorModes = ["rgb", "intensity", "elevation", "classification"];

    /// <summary>How a splat link is drawn in 3D: as its Gaussians or as their centres.</summary>
    public static readonly string[] Displays = ["splats", "points"];

    /// <summary>A splat cloud's colour (G3): full (the spherical harmonics, changing with the view) or base (degree 0 alone).</summary>
    public static readonly string[] SplatColors = ["full", "base"];

    /// <summary>Whether a link's file holds Gaussians.</summary>
    public static bool IsSplats(Entity link) => link.Attrs["splats"] is JsonValue v && v.TryGetValue<bool>(out var b) && b;

    /// <summary>The slice a plan or a section draws of a cloud when the view does not say (m): 10 cm round the cut.</summary>
    public const double DefaultSlice = 0.10;

    /// <summary>The thickest slice (m): an airborne scan's facades need metres of it, a whole building's height (decision 399).</summary>
    public const double MaxSlice = 100;

    /// <summary>How a cloud is drawn in one view: its link's settings, then the view's row over them (decision 396).</summary>
    public sealed record Look(bool Shown, bool Halftone, string ColorMode, IReadOnlySet<int> HiddenClasses, double Slice, double PointSize, string Display = "points", CropBox? Crop = null, string SplatColor = "full");

    public static Look LookIn(JsonObject? graphics, Entity link)
    {
        var row = graphics?["pointClouds"]?[link.Id] as JsonObject;
        static bool T(JsonNode? n) => n is JsonValue v && v.TryGetValue<bool>(out var b) && b;
        var mode = (string?)row?["colorMode"] is { } m && ColorModes.Contains(m) ? m : link.Str("colorMode") is { } lm && ColorModes.Contains(lm) ? lm : "rgb";
        var hidden = (row?["hiddenClasses"] as JsonArray)?.Select(x => (int?)Json.Number(x)).OfType<int>().ToHashSet() ?? [];
        var slice = Json.Number(row?["slice"]) is { } sl && sl > 0 ? sl : DefaultSlice;
        // Splats or points in 3D: the view's row, else the link's; a cloud of plain points is points.
        var display = !IsSplats(link) ? "points" : (string?)row?["display"] is { } d && Displays.Contains(d) ? d : link.Str("display") is { } ld && Displays.Contains(ld) ? ld : "splats";
        var colour = (string?)row?["splatColor"] is { } sc && SplatColors.Contains(sc) ? sc : link.Str("splatColor") is { } lc && SplatColors.Contains(lc) ? lc : "full";
        return new Look(Links.Visible(link) && !T(row?["hidden"]), Links.IsHalftone(link) || T(row?["halftone"]), mode, hidden, slice, link.NumOrNull("pointSize") ?? 2, display, CropOf(link), colour);
    }

    /// <summary>A view's row for a cloud, checked: {hidden, halftone, colorMode, hiddenClasses: [0–255…], slice: m, display: splats | points}; null when it says nothing.</summary>
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
                case "display":
                    if ((string?)val is { } d) o[k] = Displays.Contains(d) ? d : throw new CommandException($"A splat cloud is drawn as {string.Join(" or ", Displays)}.");
                    break;
                case "splatColor":
                    if ((string?)val is { } c) o[k] = SplatColors.Contains(c) ? c : throw new CommandException($"A splat cloud's colour is {string.Join(" or ", SplatColors)}.");
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
            ["splats"] = IsSplats(link), ["display"] = IsSplats(link) ? link.Str("display") ?? "splats" : null,
            ["splatColor"] = IsSplats(link) ? link.Str("splatColor") ?? "full" : null,
            ["shDegree"] = IsSplats(link) ? (SplatsOf(link)?.ShDegree ?? (int?)Json.Number((link.Attrs["cloud"] as JsonObject)?["shDegree"])) : null,
        };
    }

    /// <summary>
    /// A cloud's points for an export, in the project's coordinates: its octree read from the root down, level by level,
    /// until <paramref name="max"/> points (a coarser sample of the whole, never a corner of it); classes hidden left out.
    /// </summary>
    public static (float[] Positions, byte[] Colors) Sample(IModelView m, Entity link, Look look, int max) => Opened.Sample(m, link, look, max);

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
            foreach (var k in new[] { "mode", "units", "offset", "rotation", "up", "scale" }) if (a.ContainsKey(k)) p[k] = a[k]?.DeepClone();
            if (p["scale"] is JsonValue sv && Json.Number(sv) is { } one && Math.Abs(one - 1) < 1e-12) p.Remove("scale");
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
            if ((string?)a["display"] is { } display)
            {
                if (!IsSplats(link)) throw new CommandException("Only a cloud of Gaussian splats is drawn as splats.");
                attrs["display"] = Displays.Contains(display) ? display : throw new CommandException($"A splat cloud is drawn as {string.Join(" or ", Displays)}.");
            }
            // Its colour (G3): with the view (full, its harmonics) or its base colour alone.
            if ((string?)a["splatColor"] is { } colour)
            {
                if (!IsSplats(link)) throw new CommandException("Only a cloud of Gaussian splats has a full or a base colour.");
                attrs["splatColor"] = SplatColors.Contains(colour) ? colour : throw new CommandException($"A splat cloud's colour is {string.Join(" or ", SplatColors)}.");
            }
            ctx.Replace(link.With(attrs: attrs));
            return null;
        });

        // Scaled true by two points picked on it and their true distance (G2): one undo step.
        r.Command("pointcloud.calibrate", "Calibrate point cloud", (ctx, a) =>
        {
            var link = Links.Require(ctx.Model, (string?)a["id"]);
            if (!Is(link)) throw new CommandException("That link is not a point cloud.");
            static double[] P(JsonNode? n) => n is JsonArray { Count: 3 } v && v.Select(Json.Number).All(x => x is { } d && double.IsFinite(d))
                ? v.Select(x => Json.Number(x)!.Value).ToArray() : throw new CommandException("A point is three numbers (x, y, z).");
            return Calibrate(ctx, link, P(a["a"]), P(a["b"]), Json.Number(a["length"]) ?? 0);
        });

        // What of it is kept (G2): a box in the project, turned about z; `fit` makes the box round where the cloud is dense (the
        // floaters round a capture left out), with `whole` round all of it; null takes it off.
        r.Command("pointcloud.crop", "Crop point cloud", (ctx, a) =>
        {
            var link = Links.Require(ctx.Model, (string?)a["id"]);
            if (!Is(link)) throw new CommandException("That link is not a point cloud.");
            var attrs = link.CloneAttrs();
            CropBox? box;
            if (a["fit"] is JsonValue fv && fv.TryGetValue<bool>(out var fit) && fit) box = Fit(ctx.Model, link, a["whole"] is JsonValue wv && wv.TryGetValue<bool>(out var w) && w) ?? throw new CommandException("The cloud's size is not known yet: wait for its index.");
            else if (a.ContainsKey("crop") && a["crop"] is null) box = null;
            else box = CropBox.From(a["crop"] as JsonObject) ?? throw new CommandException("A crop is {center: [x, y, z], size: [x, y, z], rotation}, its sizes above 0.");
            if (box == null) attrs.Remove("crop"); else attrs["crop"] = box.ToJson();
            ctx.Replace(link.With(attrs: attrs));
            return box?.ToJson();
        });

        // Read again from its file (the desktop): a file changed since makes a new index; one unchanged keeps its own.
        r.Command("pointcloud.reload", "Reload point cloud", (ctx, a) =>
        {
            var link = Links.Require(ctx.Model, (string?)a["id"]);
            if (!Is(link)) throw new CommandException("That link is not a point cloud.");
            var path = HostFiles.Check(link.Str("path") ?? throw new CommandException("This cloud has no file to read again (it was linked from a browser)."));
            if (!File.Exists(path)) throw new CommandException($"The file is gone: {path}.");
            var key = KeyOfPath(path);
            ForgetFailed(key);
            if (key == link.Str("key")) { Status(link, ctx.Deliveries); return new JsonObject { ["changed"] = false }; }
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
            var k = MetresOf(units) * (Json.Number(a["scale"]) is { } fs && fs > 0 ? fs : ScaleOf(link.Attrs["placement"] as JsonObject));
            return new JsonObject
            {
                ["size"] = b != null ? new JsonArray(Math.Round((b[3] - b[0]) * k, 3), Math.Round((b[4] - b[1]) * k, 3), Math.Round((b[5] - b[2]) * k, 3)) : null,
                ["warning"] = UnitWarning(b, units, (string?)(link.Attrs["cloud"] as JsonObject)?["units"], Json.Number(a["scale"]) is { } qs && qs > 0 ? qs : ScaleOf(link.Attrs["placement"] as JsonObject)),
            };
        });
    }
}

/// <summary>
/// A page of a splat link's Gaussians for the 3D view, in the project: of the file's records from <c>Start</c> (the next page
/// from <c>Next</c>, null after the last; <c>Total</c> in the file, <c>Kept</c> by the budget in all), each kept one's centre
/// (x y z), scales (m), rotation (w x y z) and colour (RGBA, a byte each, red the lowest: little-endian bytes r, g, b, a).
/// </summary>
public sealed record SplatPage(string Link, long Start, long? Next, long Total, long Kept, float[] Positions, float[] Scales, float[] Rotations, int[] Colors,
    int ShDegree = 0, int[]? Sh = null, float[]? Turn = null);

/// <summary>A tile of a point cloud for the 3D view: its points in the project (x y z a point), colours (0xRRGGBB) and values (intensity | class « 16).</summary>
public sealed record PointTile(string Link, string Name, float[] Positions, int[] Colors, int[] Values);

/// <summary>
/// What of a point cloud is kept (docs/plan-gaussian-splats.md, G2): a box in the project, its middle and sizes in metres,
/// turned about the vertical through its middle by <see cref="Rotation"/> degrees. Points and splats outside are not sent to
/// the 3D view, not drawn in plans and sections, not snapped to and not exported.
/// </summary>
public sealed record CropBox(double[] Center, double[] Size, double Rotation)
{
    readonly double _c = Math.Cos(Rotation * Math.PI / 180), _s = Math.Sin(Rotation * Math.PI / 180);

    public bool Inside(double x, double y, double z)
    {
        double dx = x - Center[0], dy = y - Center[1];
        double u = _c * dx + _s * dy, v = -_s * dx + _c * dy;
        return Math.Abs(u) <= Size[0] / 2 && Math.Abs(v) <= Size[1] / 2 && Math.Abs(z - Center[2]) <= Size[2] / 2;
    }

    /// <summary>The project's axis-aligned box round it (min x, y, z, max x, y, z).</summary>
    public double[] Box()
    {
        var hx = Math.Abs(_c) * Size[0] / 2 + Math.Abs(_s) * Size[1] / 2;
        var hy = Math.Abs(_s) * Size[0] / 2 + Math.Abs(_c) * Size[1] / 2;
        return [Center[0] - hx, Center[1] - hy, Center[2] - Size[2] / 2, Center[0] + hx, Center[1] + hy, Center[2] + Size[2] / 2];
    }

    public JsonObject ToJson() => new()
    {
        ["center"] = new JsonArray(Center.Select(v => (JsonNode)v).ToArray()), ["size"] = new JsonArray(Size.Select(v => (JsonNode)v).ToArray()), ["rotation"] = Rotation,
    };

    /// <summary>A crop read from a link or a command; null when it is not one (a size not above 0, a number missing).</summary>
    public static CropBox? From(JsonObject? o)
    {
        if (o == null) return null;
        static double[]? Three(JsonNode? n) => n is JsonArray { Count: 3 } a && a.Select(Json.Number).All(x => x is { } d && double.IsFinite(d) && Math.Abs(d) < 1e7) ? a.Select(x => Json.Number(x)!.Value).ToArray() : null;
        var c = Three(o["center"]);
        var s = Three(o["size"]);
        var r = Json.Number(o["rotation"]) ?? 0;
        if (c == null || s == null || s.Any(v => !(v > 0)) || !double.IsFinite(r)) return null;
        return new CropBox(c, s, r);
    }
}
