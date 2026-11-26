using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace Lapin.Protocol;

/// <summary>
/// The desktop's sign-in to a server through the system's browser (docs/plan-cde-references.md §7, R6b; Atrium docs/atrium-server.md
/// "OAuth (R6b)"): OAuth 2.1's authorization code with PKCE (RFC 7636, S256), the answer coming back to a loopback address on this
/// computer as RFC 8252 says (§7.3: http on 127.0.0.1, the engine's own port, <c>/oauth/callback</c>), so the password is typed on the
/// server's own page and never reaches Lapin. Each sign-in in progress is kept here, in memory only, under its <c>state</c> (32 random
/// bytes), with its verifier, for 10 minutes; the host's callback route hands it the answer (<see cref="Arrive"/>), and the interface's
/// next <c>remote.browserSignInResult</c> exchanges the code for the server's session tokens. Nothing here is ever logged or written.
/// No field names a type of System.Net.Http (the browser's engine, docs/plan-browser-engine.md, Lazy parts): the tokens' exchange is the
/// dispatcher's.
/// </summary>
public static class BrowserSignIn
{
    /// <summary>The loopback address the server sends the browser back to: set by the desktop's host once it listens (null: no browser sign-in here).</summary>
    public static string? CallbackUrl { get; set; }

    /// <summary>Opens an address in the system's browser (the desktop's host sets it; tests record it).</summary>
    public static Action<string>? OpenBrowser { get; set; }

    /// <summary>How long a sign-in waits for its answer.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    /// <summary>The clock (tests move it).</summary>
    public static Func<DateTime> Now { get; set; } = () => DateTime.UtcNow;

    public sealed class Pending
    {
        public required string State { get; init; }
        public required string Verifier { get; init; }
        public required string Server { get; init; }
        public required string Issuer { get; init; }
        public required string TokenUrl { get; init; }
        public required string RedirectUri { get; init; }
        public required string ClientId { get; init; }
        public required bool IssRequired { get; init; }
        public required DateTime Started { get; init; }
        public string? Code { get; set; }
        public string? Error { get; set; }
    }

    static readonly ConcurrentDictionary<string, Pending> Waiting = new(StringComparer.Ordinal);

    /// <summary>32 random bytes as base64url, without padding (a state, and a PKCE verifier of 43 characters).</summary>
    public static string Random32() => Base64Url(RandomNumberGenerator.GetBytes(32));

    /// <summary>The PKCE challenge of a verifier: BASE64URL(SHA256(ASCII(verifier))).</summary>
    public static string Challenge(string verifier) => Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>A sign-in started: kept until its answer is taken, cancelled, or 10 minutes pass.</summary>
    public static Pending Start(string server, string issuer, string tokenUrl, string clientId, bool issRequired)
    {
        Sweep();
        var redirect = CallbackUrl ?? throw new InvalidOperationException("No callback address here.");
        var p = new Pending
        {
            State = Random32(), Verifier = Random32(), Server = server, Issuer = issuer, TokenUrl = tokenUrl, RedirectUri = redirect, ClientId = clientId,
            IssRequired = issRequired, Started = Now(),
        };
        Waiting[p.State] = p;
        return p;
    }

    /// <summary>A sign-in in progress by its state; null when there is none (taken, cancelled, or too old).</summary>
    public static Pending? Get(string state)
    {
        Sweep();
        return Waiting.TryGetValue(state, out var p) ? p : null;
    }

    /// <summary>A sign-in forgotten (its answer taken, or cancelled).</summary>
    public static void Forget(string state) => Waiting.TryRemove(state, out _);

    static void Sweep()
    {
        var now = Now();
        foreach (var (k, v) in Waiting) if (now - v.Started > Lifetime) Waiting.TryRemove(k, out _);
    }

    /// <summary>
    /// The browser came back to the callback address with the server's answer (RFC 6749 §4.1.2, RFC 9207's <c>iss</c>): the code, or an
    /// error, kept for the sign-in whose state it carries. Answers the page the browser shows, and whether the answer was taken. A state
    /// no sign-in here has, a second answer, or an issuer that is not the server's is refused, the sign-in failed with it where there is one.
    /// </summary>
    public static (bool Taken, string Page) Arrive(string? state, string? code, string? error, string? errorDescription, string? iss)
    {
        if (state == null || Get(state) is not { } p || p.Code != null || p.Error != null)
            return (false, Page("This sign-in is not one Lapin is waiting for", "Start the sign-in again from Lapin."));
        if ((iss != null || p.IssRequired) && !string.Equals(iss?.TrimEnd('/'), p.Issuer.TrimEnd('/'), StringComparison.Ordinal))
        {
            p.Error = "The answer did not come from the server Lapin asked: the sign-in was stopped.";
            return (false, Page("The sign-in was stopped", "The answer did not come from the server Lapin asked. Start the sign-in again from Lapin."));
        }
        if (error != null)
        {
            p.Error = error == "access_denied" ? "The sign-in was denied in the browser." : $"The server refused the sign-in ({error}{(errorDescription is { Length: > 0 } d ? $": {d}" : "")}).";
            return (true, Page("Lapin was not signed in", error == "access_denied" ? "You denied the sign-in. You can close this tab." : "The server refused the sign-in. You can close this tab and try again from Lapin."));
        }
        if (code is not { Length: > 0 })
        {
            p.Error = "The server's answer had no code.";
            return (false, Page("Lapin was not signed in", "The server's answer had no code. Start the sign-in again from Lapin."));
        }
        p.Code = code;
        return (true, Page("Lapin is signing in", "You can close this tab and go back to Lapin."));
    }

    /// <summary>The small page the browser shows on the callback address: plain text in HTML, nothing taken from the answer.</summary>
    static string Page(string title, string text) =>
        $"<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width\"><title>{title}</title>"
        + "<style>body{font:15px system-ui,sans-serif;margin:15vh auto;max-width:28em;padding:0 16px;color:#222;background:#fff}@media(prefers-color-scheme:dark){body{color:#eee;background:#1d1d1f}}</style>"
        + $"</head><body><h1 style=\"font-size:20px\">{title}</h1><p>{text}</p></body></html>";
}
