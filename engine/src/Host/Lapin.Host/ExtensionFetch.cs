using System.Text;
using Lapin.Platform;

namespace Lapin.Host;

/// <summary>
/// Lapin Extensions, stage 0 (docs/plan-extensions.md): the catalogue's index and the packages it names, read for the
/// desktop's interface (its webview reaches no site of its own, and GitHub's release files answer no browser). Only
/// reading: https anywhere, http on this computer only (a catalogue served while developing), and a local catalogue's
/// files by path or file address. The interface checks each package's size and SHA-256 against its manifest and hands it
/// to Lapin's own importers; nothing read here is ever run. A guest of a shared session is refused by the hub.
/// </summary>
public static class ExtensionFetch
{
    /// <summary>
    /// The biggest file an extension may be: the one source. The interface's <c>MAX_PACKAGE</c>
    /// (ui/src/platform/extensions/manifest.ts, which checks a manifest without the engine) says the same, and a test holds it so.
    /// </summary>
    public const long MaxBytes = 64L * 1024 * 1024;

    /// <summary>The catalogue to read when the interface names none: LAPIN_EXTENSIONS_INDEX (a local index while developing), else none.</summary>
    public static string? IndexFromEnvironment => Environment.GetEnvironmentVariable("LAPIN_EXTENSIONS_INDEX") is { Length: > 0 } v ? v : null;

    static readonly HttpClient Http = new(new HttpClientHandler { AllowAutoRedirect = true, MaxAutomaticRedirections = 8 }) { Timeout = TimeSpan.FromSeconds(90) };

    /// <summary>The bytes at <paramref name="url"/>; a sentence (CommandException) when it may not be read or is not there.</summary>
    public static async Task<byte[]> Fetch(string url, long max = MaxBytes, CancellationToken ct = default)
    {
        url = (url ?? "").Trim();
        if (url.Length == 0) throw new CommandException("Which address?");
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http" && !uri.IsFile)
        {
            if (uri.Scheme == "http" && !uri.IsLoopback) throw new CommandException("Extensions are read from https addresses (or files on this computer).");
            try { return await Download(uri, max, ct); }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested) { throw new CommandException($"{uri.Host} did not answer in time."); }
            catch (HttpRequestException ex) { throw new CommandException($"{uri.Host} could not be reached: {ex.Message}"); }
        }
        string path;
        if (uri is { IsFile: true }) path = uri.LocalPath;
        else if (Path.IsPathFullyQualified(url)) path = url;
        else throw new CommandException("Extensions are read from https addresses (or files on this computer).");
        if (!File.Exists(path)) throw new CommandException($"There is no such file: {path}.");
        if (new FileInfo(path).Length > max) throw new CommandException($"The file is bigger than an extension may be ({Size(max)}).");
        return await File.ReadAllBytesAsync(path, ct);
    }

    static async Task<byte[]> Download(Uri uri, long max, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, uri);
        req.Headers.UserAgent.ParseAdd($"Lapin/{Session.AppVersion}");
        using var res = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        // A redirect must stay on https (GitHub's releases go on to their file servers).
        if (res.RequestMessage?.RequestUri is { } final && final.Scheme != "https" && !final.IsLoopback) throw new CommandException("The address led somewhere that is not https: nothing was read.");
        if (!res.IsSuccessStatusCode) throw new CommandException($"{uri.Host} answered {(int)res.StatusCode} for {uri.AbsolutePath}.");
        if (res.Content.Headers.ContentLength is { } len && len > max) throw new CommandException($"The file is bigger than an extension may be ({Size(max)}).");
        await using var stream = await res.Content.ReadAsStreamAsync(ct);
        return await ReadCapped(stream, max, ct);
    }

    /// <summary>A body read to its end, refused as soon as it passes <paramref name="max"/> (a server's length may be absent or wrong).</summary>
    static async Task<byte[]> ReadCapped(Stream stream, long max, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        var buffer = new byte[81920];
        int n;
        while ((n = await stream.ReadAsync(buffer, ct)) > 0)
        {
            if (ms.Length + n > max) throw new CommandException($"The file is bigger than an extension may be ({Size(max)}).");
            ms.Write(buffer, 0, n);
        }
        return ms.ToArray();
    }

    /// <summary>
    /// Stage 2a (docs/plan-extensions.md §12): a JSON body POSTed to the store, for a paid extension's licence key (checked,
    /// or its package given). The key travels in the body, never in the address; it is neither kept nor logged here.
    /// https only (http on this computer, a store run while developing); no redirect followed (a POST is not repeated
    /// elsewhere). Whatever the status, its answer comes back as it is (the interface reads the store's
    /// <c>{status, error}</c>); only an address that may not be reached, no answer, or one too big is an error.
    /// </summary>
    public static async Task<(int Status, string? Type, byte[] Body)> Post(string url, string json, long max = MaxBytes, CancellationToken ct = default)
    {
        url = (url ?? "").Trim();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.IsFile || uri.Scheme is not ("https" or "http") || (uri.Scheme == "http" && !uri.IsLoopback))
            throw new CommandException("A licence key is sent to https addresses only.");
        if (Encoding.UTF8.GetByteCount(json ?? "") > 4096) throw new CommandException("The request is too large.");
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, uri) { Content = new StringContent(json ?? "{}", Encoding.UTF8, "application/json") };
            req.Headers.UserAgent.ParseAdd($"Lapin/{Session.AppVersion}");
            using var res = await PostHttp.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            if (res.Content.Headers.ContentLength is { } len && len > max) throw new CommandException($"The file is bigger than an extension may be ({Size(max)}).");
            await using var stream = await res.Content.ReadAsStreamAsync(ct);
            return ((int)res.StatusCode, res.Content.Headers.ContentType?.MediaType, await ReadCapped(stream, max, ct));
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) { throw new CommandException($"{uri.Host} did not answer in time."); }
        catch (HttpRequestException ex) { throw new CommandException($"{uri.Host} could not be reached: {ex.Message}"); }
    }

    static readonly HttpClient PostHttp = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(90) };

    static string Size(long bytes) => bytes >= 1024 * 1024 ? $"{bytes / 1024 / 1024} MB" : $"{bytes / 1024} KB";
}
