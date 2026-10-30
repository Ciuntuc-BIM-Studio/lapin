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
            ["contact"] = Contact(p["contact"]),
            ["context"] = new JsonObject
            {
                ["version"] = $"lapin-pro {version}", ["url"] = url, ["userAgent"] = userAgent,
                ["screen"] = (string?)p["screen"], ["errors"] = p["errors"]?.DeepClone() ?? new JsonArray(),
            },
        };
    }

    /// <summary>
    /// The sender's wish for a reply (docs/decisions-pending.md 177): a nickname
    /// (optional) and the id of their key (the SHA-256, in hex, of a secret that
    /// stays on their device); null for an anonymous report.
    /// </summary>
    static JsonObject? Contact(JsonNode? c)
    {
        if (c is not JsonObject o) return null;
        var keyId = (string?)o["keyId"];
        if (keyId is null || !SupportMessages.IsKeyId(keyId)) throw new CommandException("The reply key is not valid.");
        var nickname = ((string?)o["nickname"])?.Trim();
        return new JsonObject
        {
            ["nickname"] = string.IsNullOrEmpty(nickname) ? null : nickname[..Math.Min(nickname.Length, SupportMessages.MaxNickname)],
            ["keyId"] = keyId,
        };
    }
}

/// <summary>
/// The replies to a report, read and answered from the app (Help → Messages):
/// a call to one of the feedback endpoints, authenticated by the sender's
/// secret in the body (never in an address). The same from every host: the
/// desktop posts it itself, the browser hands it to its page to post.
/// </summary>
public static class SupportMessages
{
    public const int MaxText = 4000;
    public const int MaxNickname = 40;
    /// <summary>What may be called: /api/feedback/&lt;endpoint&gt;.</summary>
    public static readonly string[] Endpoints = ["inbox", "seen", "message", "nickname", "forget"];

    public static bool IsKeyId(string s) => s.Length == 64 && s.All(ch => ch is (>= '0' and <= '9') or (>= 'a' and <= 'f'));
    public static bool IsSecret(string s) => s.Length is >= 32 and <= 128 && s.All(ch => char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_');

    /// <summary>The endpoint and the body to post, from {endpoint, secret, report?, upTo?, text?, nickname?}: only what the endpoint takes.</summary>
    public static (string Endpoint, JsonObject Body) Request(JsonObject p)
    {
        var endpoint = (string?)p["endpoint"];
        if (endpoint is null || !Endpoints.Contains(endpoint)) throw new CommandException("There is no such message call.");
        var secret = (string?)p["secret"];
        if (secret is null || !IsSecret(secret)) throw new CommandException("The reply key is not valid.");
        var body = new JsonObject { ["secret"] = secret };
        int Int(string name) => p[name] is JsonValue v && v.TryGetValue<int>(out var n) && n >= 0 ? n : throw new CommandException("Which report?");
        switch (endpoint)
        {
            case "seen":
                body["report"] = Int("report");
                body["upTo"] = Int("upTo");
                break;
            case "message":
                body["report"] = Int("report");
                var text = ((string?)p["text"])?.Trim();
                if (string.IsNullOrEmpty(text)) throw new CommandException("The message is empty.");
                if (text.Length > MaxText) throw new CommandException($"A message is at most {MaxText} characters.");
                body["text"] = text;
                break;
            case "nickname":
                var nickname = ((string?)p["nickname"])?.Trim() ?? "";
                body["nickname"] = nickname[..Math.Min(nickname.Length, MaxNickname)];
                break;
        }
        return (endpoint, body);
    }
}
