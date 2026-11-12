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
    /// <summary>The biggest file an extension may be (the interface's <c>MAX_PACKAGE</c>).</summary>
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

    static string Size(long bytes) => bytes >= 1024 * 1024 ? $"{bytes / 1024 / 1024} MB" : $"{bytes / 1024} KB";
}
