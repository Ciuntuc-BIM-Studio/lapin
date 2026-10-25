using System.Text.Json.Nodes;
using Lapin.Platform;

namespace Lapin.Protocol;

/// <summary>
/// A report of an issue as the feedback endpoint takes it, the same from
/// every host: the desktop posts it itself, the browser hands it to its page
/// to post.
/// </summary>
public static class SupportReport
{
    /// <summary>The body to post: kind (bug, idea, question), title and description required.</summary>
    /// <param name="url">Where the app runs ("desktop", "browser").</param>
    public static JsonObject Body(JsonObject p, string version, string url, string userAgent)
    {
        var kind = (string?)p["kind"] ?? "bug";
        if (kind is not ("bug" or "idea" or "question")) throw new CommandException("A report is a bug, an idea or a question.");
        var title = ((string?)p["title"])?.Trim();
        var description = ((string?)p["description"])?.Trim();
        if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(description)) throw new CommandException("A report needs a title and a description.");
        return new JsonObject
        {
            ["app"] = "lapin", ["kind"] = kind, ["title"] = title, ["description"] = description,
            ["expected"] = (string?)p["expected"], ["email"] = (string?)p["email"], ["consent"] = p["consent"] is JsonValue c && c.TryGetValue<bool>(out var ok) && ok,
            ["screenshot"] = (string?)p["screenshot"], ["website"] = (string?)p["website"] ?? "",
            ["context"] = new JsonObject
            {
                ["version"] = $"lapin-pro {version}", ["url"] = url, ["userAgent"] = userAgent,
                ["screen"] = (string?)p["screen"], ["errors"] = p["errors"]?.DeepClone() ?? new JsonArray(),
            },
        };
    }
}
