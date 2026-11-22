using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Nodes;
using Lapin.Platform;

namespace Lapin.Host;

/// <summary>
/// Intel Open Image Denoise beside the engine (docs/plan-render.md, S4; answer Î4: a separate program, not a library in
/// the engine): OIDN's own <c>oidnDenoise</c> from its official release (Apache-2.0), fetched by scripts/fetch-oidn.mjs into
/// engine/native/&lt;rid&gt;/oidn and shipped by build-product beside the modules (resources/oidn). The path tracer's
/// still image comes as its light (half floats, RGB), its albedo and its normals (bytes, RGB; a normal mapped from −1–1),
/// rows bottom-up; they are written as PFM files in a folder of their own, denoised (the RT filter, albedo and normals as
/// its guides), and the light goes back as half floats. The host's own call, never a guest's or a site's.
/// </summary>
public static class Denoiser
{
    static readonly string Exe = OperatingSystem.IsWindows() ? "oidnDenoise.exe" : "oidnDenoise";

    /// <summary>Where OIDN is: LAPIN_OIDN_DIR, else the app's resources beside the modules, else this checkout's engine/native/&lt;rid&gt;/oidn; null when none.</summary>
    public static string? Find(string? modulesDir)
    {
        if (OperatingSystem.IsBrowser()) return null;
        static string? Has(string? dir) => dir != null && File.Exists(Path.Combine(dir, "bin", Exe)) ? dir : null;
        if (Has(Environment.GetEnvironmentVariable("LAPIN_OIDN_DIR")) is { } own) return own;
        if (modulesDir != null && Path.GetDirectoryName(Path.GetFullPath(modulesDir).TrimEnd(Path.DirectorySeparatorChar)) is { } res && Has(Path.Combine(res, "oidn")) is { } shipped)
            return shipped;
        var rid = $"{(OperatingSystem.IsMacOS() ? "osx" : OperatingSystem.IsWindows() ? "win" : "linux")}-{(RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "arm64" : "x64")}";
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
            if (Has(Path.Combine(d.FullName, "engine", "native", rid, "oidn")) is { } dev) return dev;
        return null;
    }

    /// <summary>A PFM file (RGB floats, rows bottom-up as PFM keeps them, little-endian: a negative scale).</summary>
    public static byte[] Pfm(int width, int height, float[] rgb)
    {
        var head = Encoding.ASCII.GetBytes($"PF\n{width} {height}\n-1.0\n");
        var bytes = new byte[head.Length + rgb.Length * 4];
        head.CopyTo(bytes, 0);
        MemoryMarshal.AsBytes(rgb.AsSpan()).CopyTo(bytes.AsSpan(head.Length));
        if (!BitConverter.IsLittleEndian) throw new PlatformNotSupportedException("PFM is written little-endian.");
        return bytes;
    }

    /// <summary>A PFM file read: its size and RGB floats (a greyscale one "Pf" spread to RGB).</summary>
    public static (int Width, int Height, float[] Rgb) ReadPfm(byte[] bytes)
    {
        var at = 0;
        string Token()
        {
            while (at < bytes.Length && char.IsWhiteSpace((char)bytes[at])) at++;
            var start = at;
            while (at < bytes.Length && !char.IsWhiteSpace((char)bytes[at])) at++;
            return Encoding.ASCII.GetString(bytes, start, at - start);
        }
        var kind = Token();
        var w = int.Parse(Token(), CultureInfo.InvariantCulture);
        var h = int.Parse(Token(), CultureInfo.InvariantCulture);
        var scale = double.Parse(Token(), CultureInfo.InvariantCulture);
        at++;
        var channels = kind == "PF" ? 3 : kind == "Pf" ? 1 : throw new CommandException("The denoiser wrote no PFM image.");
        var n = w * h * channels;
        if (bytes.Length - at < n * 4) throw new CommandException("The denoiser's image is cut short.");
        var vals = new float[n];
        for (var i = 0; i < n; i++)
        {
            var raw = scale < 0 ? BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(at + 4 * i)) : BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(at + 4 * i));
            vals[i] = BitConverter.Int32BitsToSingle(raw);
        }
        if (channels == 3) return (w, h, vals);
        var rgb = new float[w * h * 3];
        for (var i = 0; i < w * h; i++) rgb[3 * i] = rgb[3 * i + 1] = rgb[3 * i + 2] = vals[i];
        return (w, h, rgb);
    }

    static float[] Halves(byte[] bytes, int count)
    {
        if (bytes.Length < count * 2) throw new CommandException("The picture's light is shorter than its size.");
        var f = new float[count];
        for (var i = 0; i < count; i++) f[i] = (float)BitConverter.UInt16BitsToHalf(BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(2 * i)));
        return f;
    }

    static float[] Unit(byte[] bytes, int count, bool signed)
    {
        if (bytes.Length < count) throw new CommandException("A guide of the picture is shorter than its size.");
        var f = new float[count];
        for (var i = 0; i < count; i++) f[i] = signed ? bytes[i] / 255f * 2f - 1f : bytes[i] / 255f;
        return f;
    }

    static byte[] ToHalves(float[] f)
    {
        var o = new byte[f.Length * 2];
        for (var i = 0; i < f.Length; i++) BinaryPrimitives.WriteUInt16LittleEndian(o.AsSpan(2 * i), BitConverter.HalfToUInt16Bits((Half)Math.Clamp(f[i], 0f, 65000f)));
        return o;
    }

    /// <summary>render.denoise: the picture denoised by oidnDenoise (see the class).</summary>
    public static async Task<JsonObject> Run(string dir, JsonObject p, CancellationToken ct = default)
    {
        var w = p["width"]?.GetValue<int>() ?? 0;
        var h = p["height"]?.GetValue<int>() ?? 0;
        if (w < 1 || h < 1 || (long)w * h > 8192L * 8192L) throw new CommandException("The picture's size is not one to denoise.");
        byte[] B64(string key)
        {
            try { return Convert.FromBase64String((string?)p[key] ?? throw new CommandException($"The picture's {key} is missing.")); }
            catch (FormatException) { throw new CommandException($"The picture's {key} is not base64."); }
        }
        var n = w * h * 3;
        var color = Halves(B64("color"), n);
        var albedo = p["albedo"] != null ? Unit(B64("albedo"), n, false) : null;
        var normal = p["normal"] != null ? Unit(B64("normal"), n, true) : null;
        var work = Path.Combine(Path.GetTempPath(), $"lapin-oidn-{Guid.NewGuid():N}");
        Directory.CreateDirectory(work);
        try
        {
            string Put(string name, float[] rgb) { var f = Path.Combine(work, name); File.WriteAllBytes(f, Pfm(w, h, rgb)); return f; }
            var args = new List<string> { "--hdr", Put("color.pfm", color) };
            if (albedo != null) { args.Add("--alb"); args.Add(Put("albedo.pfm", albedo)); }
            if (albedo != null && normal != null) { args.Add("--nrm"); args.Add(Put("normal.pfm", normal)); }
            var output = Path.Combine(work, "out.pfm");
            args.AddRange(["-q", "high", "-o", output]);
            var start = new ProcessStartInfo(Path.Combine(dir, "bin", Exe)) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
            foreach (var a in args) start.ArgumentList.Add(a);
            using var proc = Process.Start(start) ?? throw new CommandException("Open Image Denoise did not start.");
            var err = proc.StandardError.ReadToEndAsync(ct);
            _ = proc.StandardOutput.ReadToEndAsync(ct);
            await proc.WaitForExitAsync(ct);
            if (proc.ExitCode != 0 || !File.Exists(output)) throw new CommandException($"Open Image Denoise failed: {(await err).Trim()}");
            var (ow, oh, rgb) = ReadPfm(await File.ReadAllBytesAsync(output, ct));
            if (ow != w || oh != h) throw new CommandException("Open Image Denoise gave back another size.");
            return new JsonObject { ["width"] = w, ["height"] = h, ["color"] = Convert.ToBase64String(ToHalves(rgb)) };
        }
        finally
        {
            try { Directory.Delete(work, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}
