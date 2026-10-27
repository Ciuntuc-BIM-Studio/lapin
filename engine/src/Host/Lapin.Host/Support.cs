using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Lapin.Platform;

namespace Lapin.Host;

/// <summary>
/// Reaching Ciuntuc BIM Studio from the app: a report of an issue (posted to
/// the Lapin server's feedback endpoint, where reports are kept as files), and
/// the links the help menu opens (the user guide in English or Romanian, the community on Skool, the site). Through
/// the host, so the desktop app needs no browser rights of its own.
/// </summary>
public static class Support
{
    public const string CommunityUrl = "https://www.skool.com/bubblebim-club-1376/about";
    public const string SiteUrl = "https://ciuntucbimstudio.ro";
    public const string GuideUrl = "https://lapin.ciuntucbimstudio.ro/docs/";
    public const string GuideRoUrl = "https://lapin.ciuntucbimstudio.ro/docs/ro/";

    /// <summary>Where reports go: LAPIN_FEEDBACK_URL when set (tests, a staging server), else the Lapin server.</summary>
    public static string FeedbackUrl => Environment.GetEnvironmentVariable("LAPIN_FEEDBACK_URL") is { Length: > 0 } u ? u : "https://lapin.ciuntucbimstudio.ro/api/feedback";

    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    /// <summary>Send a report: kind (bug, idea, question), title and description required; the issue's number back, or the server's reason.</summary>
    public static async Task<JsonObject> Report(JsonObject p, string version)
    {
        var body = Lapin.Protocol.SupportReport.Body(p, version, "desktop", $"{Environment.OSVersion} .NET {Environment.Version}");
        try
        {
            using var res = await Http.PostAsJsonAsync(FeedbackUrl, body);
            var answer = await res.Content.ReadFromJsonAsync<JsonObject>().ConfigureAwait(false) ?? [];
            if (!res.IsSuccessStatusCode) throw new CommandException((string?)answer["error"] ?? $"The report could not be sent ({(int)res.StatusCode}).");
            return new JsonObject { ["number"] = Lapin.Model.Json.Number(answer["number"]) ?? 0 };
        }
        catch (HttpRequestException e) { throw new CommandException($"The report could not be sent: {e.Message}"); }
        catch (TaskCanceledException) { throw new CommandException("The report could not be sent: no answer from the server."); }
        catch (System.Text.Json.JsonException) { throw new CommandException("The report could not be sent: the server's answer was unreadable."); }
    }

    /// <summary>Open one of the help menu's links in the browser (only these: nothing else is opened on the host's say).</summary>
    public static void Open(string? which)
    {
        var url = which switch { "guide" => GuideUrl, "guideRo" => GuideRoUrl, "community" => CommunityUrl, "site" => SiteUrl, _ => throw new CommandException("Nothing to open.") };
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
}
