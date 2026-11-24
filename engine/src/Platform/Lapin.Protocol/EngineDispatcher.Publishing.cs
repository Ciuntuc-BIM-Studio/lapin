using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Package.Remote;
using Lapin.Platform;
using Lapin.Platform.Cde;
using Lapin.Platform.Cde.Remarks;

namespace Lapin.Protocol;

/// <summary>
/// Publishing on a CDE server (Atrium docs/plan-cde-web.md, stage W4): the handover report. The submissions, their approval, the containers
/// and the handover diff are the server's (Atrium's <c>/atrium/1.0</c>, reached by the interface through <c>remote.atrium</c> on the
/// desktop and the page's fetch in the browser); the report is the engine's, so the desktop, the browser, scripts and the MCP bridge get the
/// same workbook.
/// </summary>
public sealed partial class EngineDispatcher
{
    /// <summary>The most journal rows a report reads (the server pages them by 1000).</summary>
    const int MaxJournal = 50_000;

    /// <summary>
    /// <c>remote.handoverReport</c>: the handover report as an XLSX (<see cref="CdeHandoverReport"/>). The issues and comments are the
    /// project's remarks; with a server remote the journal is read from its event log (<c>journal: false</c> leaves it out), and with
    /// <c>from</c> and <c>to</c> (two issue tags, as <c>refs/tags/issue/3-P02</c> or <c>3-P02</c>) the handover diff is read from the
    /// server, unless the caller gives it (<c>diff</c>, the server's answer, as the interface has it already). Without a server, or
    /// when it cannot be reached, the report has what the project holds and says what it lacks. Delivered as an export is: to
    /// <c>path</c>, or its bytes (<c>data</c>).
    /// </summary>
    async Task<(object? Result, Diff? Diff)> HandoverReport(JsonObject p, RemoteTokens tokens, DateTime now)
    {
        var h = files.History(session);
        var e = IssueRemote(h, p);
        var topics = Remarks.All(session.Model);
        var notes = new List<string>();
        // Asked for one server project (the interface's handover diff view) while another is open: its issues are not that project's.
        if ((string?)p["serverProject"] is { Length: > 0 } asked && e?.Project != asked)
        {
            e = null;
            topics = [];
            notes.Add("The project open is not this server project: its issues, comments and journal are left out (clone or open it to have them).");
        }
        var from = (string?)p["from"] is { Length: > 0 } f ? f : null;
        var to = (string?)p["to"] is { Length: > 0 } t ? t : null;
        var people = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? by = null;
        if (e != null && h.Issues.GetValueOrDefault(e.Name) is { } st && (string?)st["project"] == e.Project)
        {
            foreach (var (k, v) in st["people"] as JsonObject ?? []) if ((string?)v is { } n) people[k] = n;
            by = (string?)st["name"] ?? (string?)st["account"];
        }
        by ??= (string?)p["author"]?["name"];

        List<CdeHandoverReport.JournalRow>? journal = null;
        List<CdeHandoverReport.DiffRow>? diff = p["diff"] is JsonObject given ? DiffRows(given) : null;
        if (e == null)
        {
            if (from != null && to != null && diff == null) notes.Add("The handover diff needs the project's CDE server: the project has none.");
            if (notes.Count == 0) notes.Add("The project has no CDE server: the journal is the server's, so it is left out.");
        }
        else
        {
            HttpRemote? remote = null;
            try
            {
                remote = (HttpRemote)await OpenRemote(e, AuthorOf(p), tokens);
                var project = Uri.EscapeDataString(e.Project);
                if (p["journal"] is not JsonValue jv || !jv.TryGetValue<bool>(out var wanted) || wanted)
                {
                    ReportProgress("Reading the issue journal", 0, 0);
                    journal = [];
                    var titles = topics.ToDictionary(x => x.Guid.ToString("D"), x => x.Title, StringComparer.OrdinalIgnoreCase);
                    long after = 0;
                    while (journal.Count < MaxJournal)
                    {
                        var a = await remote.ApiAsync(HttpMethod.Get, $"atrium/1.0/projects/{project}/events?after={after}&limit=1000");
                        if (a.Status != 200 || a.Json is not JsonObject page)
                        {
                            notes.Add(a.Status == 404 ? "This server keeps no issue journal: the Journal sheet is empty." : $"The issue journal could not be read (HTTP {a.Status}).");
                            break;
                        }
                        var events = page["events"] as JsonArray ?? [];
                        foreach (var x in events.OfType<JsonObject>())
                        {
                            var guid = (string?)x["topicGuid"];
                            var type = (string?)x["type"] ?? "";
                            // The title as the journal knows it at that row (a deleted topic's last title is in its topic_deleted row).
                            if (guid != null && type == "title_updated" && Text(x["new"]) is { } nt) titles[guid] = nt;
                            if (guid != null && type == "topic_deleted" && x["old"] is JsonObject old && (string?)old["title"] is { } dt) titles.TryAdd(guid, dt);
                            var seq = x["seq"] is JsonValue sv && sv.TryGetValue<long>(out var s) ? s : after;
                            journal.Add(new(seq, (string?)x["date"] ?? "", (string?)x["authorName"] ?? (string?)x["author"] ?? "", type, guid,
                                guid != null ? titles.GetValueOrDefault(guid) : null, Text(x["old"]), Text(x["new"])));
                            after = Math.Max(after, seq);
                        }
                        if (page["more"] is not JsonValue mv || !mv.TryGetValue<bool>(out var more) || !more || events.Count == 0) break;
                    }
                    // Titles learnt later in the log (a topic made before its first title change) fill the earlier rows.
                    journal = [.. journal.Select(r => r.Title == null && r.TopicGuid != null && titles.GetValueOrDefault(r.TopicGuid) is { } tt ? r with { Title = tt } : r)];
                }
                if (from != null && to != null && diff == null)
                {
                    ReportProgress("Comparing the issues", 0, 0);
                    var a = await remote.ApiAsync(HttpMethod.Get, $"atrium/1.0/projects/{project}/handover-diff?from={Uri.EscapeDataString(from)}&to={Uri.EscapeDataString(to)}");
                    if (a.Status == 200 && a.Json is JsonObject d) diff = DiffRows(d);
                    else notes.Add(a.Status == 404 && (string?)a.Json?["code"] is "not_found" or null
                        ? "This server does not compare issues: the Handover diff sheet is left out."
                        : $"The handover diff could not be read ({(string?)a.Json?["message"] ?? $"HTTP {a.Status}"}).");
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException or RemoteException or InvalidDataException)
            {
                notes.Add($"The CDE server could not be reached ({ex.Message}): the report has what the project holds.");
            }
            finally { remote?.Dispose(); }
        }

        var title = (string?)p["project"] ?? (session.Path is { } sp ? Path.GetFileNameWithoutExtension(sp) : e?.Project ?? "Project");
        var made = now.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        var header = new CdeHandoverReport.Header(title, e?.Url, made, by, from, to, notes);
        string Name(string? id) => id is { Length: > 0 } x ? people.GetValueOrDefault(x) ?? x : "";
        var bytes = CdeHandoverReport.Write(header, topics, Name, journal, diff);

        var file = $"{Safe(title)} handover {now.ToUniversalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.xlsx";
        JsonObject o;
        if ((string?)p["path"] is { Length: > 0 } path)
        {
            Lapin.Platform.AtomicFile.Write(RefuseOverRead(HostFiles.Check(path)), bytes);
            o = new JsonObject { ["path"] = path, ["bytes"] = bytes.Length };
        }
        else o = new JsonObject { ["name"] = file, ["data"] = Convert.ToBase64String(bytes) };
        o["issues"] = topics.Count;
        o["comments"] = topics.Sum(x => x.Comments.Count);
        o["journal"] = journal?.Count;
        o["diff"] = diff?.Count;
        o["notes"] = new JsonArray([.. notes.Select(n => (JsonNode)n)]);
        Tokens(o, tokens);
        return (o, null);
    }

    static string Safe(string name)
    {
        var s = new string([.. name.Select(c => Path.GetInvalidFileNameChars().Contains(c) || c is '/' or '\\' or ':' ? '-' : c)]).Trim();
        return s.Length == 0 ? "Project" : s.Length > 80 ? s[..80] : s;
    }

    /// <summary>A journal value as text: a string as it is, anything else as compact JSON.</summary>
    static string? Text(JsonNode? n) => n switch
    {
        null => null,
        JsonValue v when v.TryGetValue<string>(out var s) => s,
        _ => n.ToJsonString(),
    };

    /// <summary>
    /// The server's handover diff (Atrium docs/atrium-server.md § The handover diff) as rows: <c>{from: tag, to: tag, documents: {added,
    /// removed, changed, unchanged}}</c>, a tag being <c>{name, commit, revision, at}</c> and a document <c>{document, name, sha256, size}</c>
    /// (a changed one <c>{document, from, to}</c>). A document's revision is its side's tag's; the status is read from the file's name
    /// (<c>…_S2_P02.pdf</c>). Unchanged documents are listed too, so the sheet is the whole handover.
    /// </summary>
    internal static List<CdeHandoverReport.DiffRow> DiffRows(JsonObject d)
    {
        var rows = new List<CdeHandoverReport.DiffRow>();
        static string? S(JsonNode? o, string k) => o?[k] switch { JsonValue v when v.TryGetValue<string>(out var s) => s, JsonValue v => v.ToJsonString(), _ => null };
        static string? Label(JsonNode? tag) => S(tag, "name") is { } n ? (n.StartsWith("refs/tags/issue/", StringComparison.Ordinal) ? n["refs/tags/issue/".Length..] : n) : null;
        static string? Status(string? file) => file is null ? null
            : System.Text.RegularExpressions.Regex.Match(file, @"_([SAB][0-9]|CR)_[PC][0-9]{2,3}(\.[A-Za-z0-9]+)?$") is { Success: true } m ? m.Groups[1].Value : null;
        var docs = d["documents"] as JsonObject ?? d;
        var (fromRev, toRev) = (S(d["from"], "revision"), S(d["to"], "revision"));
        var (fromTag, toTag) = (Label(d["from"]), Label(d["to"]));
        string Doc(JsonNode? o) => S(o, "document") ?? S(o, "name") ?? "";
        foreach (var x in (docs["added"] as JsonArray ?? []).OfType<JsonObject>())
            rows.Add(new("added", Doc(x), null, S(x, "revision") ?? toRev, null, S(x, "status") ?? Status(S(x, "name")), null, S(x, "issue") ?? toTag, null, S(x, "sha256")));
        foreach (var x in (docs["removed"] as JsonArray ?? []).OfType<JsonObject>())
            rows.Add(new("removed", Doc(x), S(x, "revision") ?? fromRev, null, S(x, "status") ?? Status(S(x, "name")), null, S(x, "issue") ?? fromTag, null, S(x, "sha256"), null));
        foreach (var x in (docs["changed"] as JsonArray ?? []).OfType<JsonObject>())
        {
            var a = x["from"];
            var b = x["to"];
            rows.Add(new("changed", Doc(x) is { Length: > 0 } n ? n : Doc(b), S(a, "revision") ?? fromRev, S(b, "revision") ?? toRev, S(a, "status") ?? Status(S(a, "name")),
                S(b, "status") ?? Status(S(b, "name")), S(a, "issue") ?? fromTag, S(b, "issue") ?? toTag, S(a, "sha256"), S(b, "sha256")));
        }
        foreach (var x in docs["unchanged"] as JsonArray ?? [])
            if (x is JsonValue v && v.TryGetValue<string>(out var name))
                rows.Add(new("unchanged", name, fromRev, toRev, null, null, fromTag, toTag, null, null));
        return rows;
    }

    // ------------------------------------------------------------ issuing on a server: a submission, or the issue tag itself

    /// <summary>An issue of the project made ready for the remote: its record, where it goes, its tag's name and the files the project's file holds.</summary>
    sealed record ServerIssue(CdeIssues.Issue Record, string Purpose, string Suitability, string RefName, List<(TagFile File, byte[] Bytes)> Files, List<string> Documents, List<string> Notes);

    static readonly System.Text.RegularExpressions.Regex SharedCode = new("^S[1-7]$");
    static readonly System.Text.RegularExpressions.Regex PublishedCode = new("^(A[1-9]|B[1-9]|CR)$");

    /// <summary>
    /// The issue asked for (<c>issue</c>: the <c>LapinIssue</c>'s id, else the last one made), the container it goes to (<c>purpose</c>
    /// "shared" or "published", else the record's container) and the suitability (the record's status when it fits the container, else
    /// S2 / A1), its files from the project's file. A WIP issue is not issued on the server.
    /// </summary>
    ServerIssue IssueFor(JsonObject p)
    {
        var all = CdeIssues.All(session.Model);
        if (all.Count == 0) throw new CommandException("The project has no issue yet: make one with Manage → Issue… first.");
        var id = (string?)p["issue"];
        var record = id == null ? all[^1] : all.FirstOrDefault(i => i.Id == id || i.Number.ToString(CultureInfo.InvariantCulture) == id) ?? throw new CommandException("The project has no such issue.");
        var purpose = ((string?)p["purpose"] ?? record.Container).ToLowerInvariant();
        if (purpose == "wip") throw new CommandException("A WIP issue stays in work in progress: issue it to Shared or Published to put it on the server.");
        if (purpose is not ("shared" or "published")) throw new CommandException("An issue goes to Shared or Published.");
        var wanted = (string?)p["suitability"] ?? record.Status;
        var fits = purpose == "shared" ? SharedCode : PublishedCode;
        var suitability = fits.IsMatch(wanted) ? wanted : (string?)p["suitability"] != null
            ? throw new CommandException($"{wanted} is not a status for {(purpose == "shared" ? "Shared (S1–S7)" : "Published (A1–A9, B1–B9, CR)")}.")
            : purpose == "shared" ? "S2" : "A1";
        if (!System.Text.RegularExpressions.Regex.IsMatch(record.Revision, "^[PC][0-9]{2,3}$")) throw new CommandException($"The issue's revision {record.Revision} is not one a server takes (P01, C01…).");
        var store = files.Issues(session);
        var notes = new List<string>();
        var list = new List<(TagFile, byte[])>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in record.Files)
        {
            if (!names.Add(f.Name)) continue;
            if (!store.Has(f.Sha256)) { notes.Add($"{f.Name} is not kept in the project's file, so it does not go to the server."); continue; }
            list.Add((new TagFile(f.Name, f.Sha256, f.Size), store.Get(f.Sha256)));
        }
        // A document is a file's name without its status and revision (CdeNaming.WithRevision) and its extension: the drawing whatever its revision.
        var documents = record.Files.Select(f => System.Text.RegularExpressions.Regex.Replace(Path.GetFileNameWithoutExtension(f.Name), @"_([SAB][0-9]|CR|S0)_[PC][0-9]{2,3}$", ""))
            .Where(n => n.Length is > 0 and <= 255).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return new ServerIssue(record, purpose, suitability, $"refs/tags/issue/{record.Number}-{record.Revision}", list, documents, notes);
    }

    /// <summary>The commit the issue goes with: the project's head, saved to the remote (an issued revision is on the shared history).</summary>
    string IssuedCommit(PackageHistory h, RemoteEntry e)
    {
        if (h.HeadCommit is not { } head || h.Uncommitted(ProjectPackage.Build(session).Manifest, IssuesApart(h)))
            throw new CommandException("The project has changes the server has not got: save to the server first, so the issue's commit is there.");
        var seen = h.Seen.GetValueOrDefault(e.Name)?.Refs.Values.Where(h.HasCommit).ToList() ?? [];
        if (!h.Closure(seen).ContainsKey(head)) throw new CommandException("The project's last commit is not on the server: save to the server first.");
        return head;
    }

    /// <summary>The issue's files sent as objects (only those the remote lacks), before a submission or a tag names them.</summary>
    static async Task<HashSet<string>> SendFiles(IRemote remote, ServerIssue issue)
    {
        var ids = issue.Files.Select(f => f.File.Sha256).Distinct(StringComparer.Ordinal).ToList();
        var sent = new HashSet<string>(ids, StringComparer.Ordinal);
        if (ids.Count == 0) return sent;
        var missing = (await remote.MissingAsync(ids)).ToHashSet(StringComparer.Ordinal);
        foreach (var (f, bytes) in issue.Files)
            if (missing.Remove(f.Sha256)) await remote.PutAsync(f.Sha256, bytes, ObjectKind.Blob);
        return sent;
    }

    static JsonObject Refusal(RemoteException ex)
    {
        var o = new JsonObject { ["ok"] = false, ["status"] = ex.Status, ["code"] = ex.Code, ["message"] = ex.Message };
        foreach (var (k, v) in ex.Body) if (k is not ("code" or "message")) o[k] = v?.DeepClone();
        return o;
    }

    /// <summary>
    /// <c>remote.submitIssue</c>: an issue submitted for approval on the project's CDE server (Atrium's publish queue): the project's head
    /// commit (saved to the server first), the issue's number and revision, Shared or Published with its suitability, the documents and the
    /// files the project's file keeps (sent first as objects). The server makes the issue tag when an approver approves. A refusal is an
    /// answer (<c>ok: false</c> with the server's code: <c>tag_exists</c>, <c>already_submitted</c>, <c>forbidden</c>…), not an error.
    /// </summary>
    async Task<(object? Result, Diff? Diff)> SubmitIssue(JsonObject p, RemoteAuthor me, RemoteTokens tokens)
    {
        var h = files.History(session);
        var e = IssueRemote(h, p) ?? throw new CommandException("Submitting needs a CDE server: connect one in File → Collaborate….");
        var issue = IssueFor(p);
        var commit = IssuedCommit(h, e);
        JsonObject o;
        using (var remote = (HttpRemote)await OpenRemote(e, me, tokens))
        {
            ReportProgress("Submitting the issue", 0, 0);
            try
            {
                await remote.ReadyAsync();
                await SendFiles(remote, issue);
                var body = new JsonObject
                {
                    // A new revision keeps its issue's number; only a first issue may let the server number it.
                    ["commit"] = commit, ["issue"] = Flag(p, "next") && issue.Record.Of == null ? null : issue.Record.Number, ["revision"] = issue.Record.Revision, ["purpose"] = issue.Purpose,
                    ["suitability"] = issue.Suitability, ["note"] = IssueNote(p, issue.Record.Note) ?? "",
                    ["documents"] = new JsonArray([.. issue.Documents.Select(x => (JsonNode)x)]),
                    ["files"] = new JsonArray([.. issue.Files.Select(f => (JsonNode)new JsonObject { ["name"] = f.File.Name, ["sha256"] = f.File.Sha256, ["size"] = f.File.Size })]),
                };
                // The issue's GlobalId: a new revision of an earlier issue names that issue's (the server keeps one GlobalId for all its revisions).
                if (issue.Record.IssueId.Length == 22) body["issueId"] = issue.Record.IssueId;
                var a = await remote.ApiAsync(HttpMethod.Post, $"atrium/1.0/projects/{Uri.EscapeDataString(e.Project)}/submissions", body);
                if (a.Status is 200 or 201) o = new JsonObject { ["ok"] = true, ["submission"] = a.Json?.DeepClone() };
                else
                {
                    var code = (string?)a.Json?["code"] ?? (a.Status == 404 ? "not_offered" : $"http_{a.Status}");
                    o = Refusal(new RemoteException(a.Status, code, (string?)a.Json?["message"] ?? $"The server answered {a.Status}.", a.Json as JsonObject));
                }
            }
            catch (RemoteException ex) { o = Refusal(ex); }
        }
        o["notes"] = new JsonArray([.. issue.Notes.Select(n => (JsonNode)n)]);
        o["tag"] = issue.RefName;
        Tokens(o, tokens);
        return (o, null);
    }

    /// <summary>
    /// <c>remote.issueTag</c>: the issue tag itself (lapin-remote §4.4, <c>refs/tags/issue/&lt;n&gt;-&lt;rev&gt;</c>) made and pushed, for a
    /// folder remote, or a server whose rules let this account make issue tags and who chooses not to ask for approval. The tag names the
    /// head commit (saved to the remote first) and the files the project's file keeps; it never moves once made. A refusal (403 below the
    /// project's issueTags rule, 409 the tag exists) is an answer, and nothing is kept here.
    /// </summary>
    async Task<(object? Result, Diff? Diff)> IssueTag(JsonObject p, RemoteAuthor me, RemoteTokens tokens, DateTime now)
    {
        var h = files.History(session).Clone();
        var e = TheRemote(h, p);
        var issue = IssueFor(p);
        var commit = IssuedCommit(h, e);
        var tag = new JsonObject
        {
            ["format"] = "lapin-tag", ["v"] = 1, ["commit"] = commit, ["kind"] = "issue", ["number"] = issue.Record.Number,
            ["author"] = new JsonObject { ["id"] = me.Id, ["name"] = me.Name }, ["at"] = RemoteObjects.Stamp(now),
            ["issue"] = issue.Record.IssueId.Length == 22 ? issue.Record.IssueId : Lapin.Model.GlobalId.New(),
            ["revision"] = issue.Record.Revision, ["status"] = issue.Suitability, ["container"] = issue.Purpose == "shared" ? "Shared" : "Published",
            ["files"] = new JsonArray([.. issue.Files.Select(f => (JsonNode)new JsonObject { ["name"] = f.File.Name, ["sha256"] = f.File.Sha256, ["size"] = f.File.Size })]),
        };
        if (IssueNote(p, issue.Record.Note) is { Length: > 0 } note) tag["note"] = note;
        var bytes = Jcs.Canonicalize(tag);
        var id = ObjectIds.Of(bytes);
        h.Add(id, bytes, ObjectKind.Tag);
        if (h.Refs.TryGetValue(issue.RefName, out var had) && had != id) throw new CommandException($"This project already has the tag {issue.RefName["refs/tags/issue/".Length..]}: an issue tag never changes.");
        h.Move(issue.RefName, id, me, "issue", now);
        JsonObject o;
        using (var remote = await OpenRemote(e, me, tokens))
        {
            ReportProgress("Issuing", 0, 0);
            try
            {
                await remote.ReadyAsync();
                var sent = await SendFiles(remote, issue);
                var outcome = await RemoteOps.PushAsync(remote, h, e.Name, [(issue.RefName, issue.RefName)], now, uploaded: sent);
                if (outcome.Pushed)
                {
                    files.KeepHistory(session, h);
                    o = new JsonObject { ["ok"] = true, ["tag"] = issue.RefName, ["id"] = id, ["notes"] = new JsonArray([.. issue.Notes.Concat(outcome.Notes).Select(n => (JsonNode)n)]), ["status"] = Status(h) };
                }
                else o = new JsonObject { ["ok"] = false, ["code"] = "ref_moved", ["message"] = $"{issue.RefName["refs/tags/".Length..]} was made on the remote meanwhile." };
            }
            catch (RemoteException ex) { o = Refusal(ex); }
        }
        o["tag"] ??= issue.RefName;
        Tokens(o, tokens);
        return (o, null);
    }
    /// <summary>
    /// The note a submission or an issue tag carries: the one asked (<c>note</c>), else the issue's; with <c>idsOverride</c>, the reason
    /// the issue went although the project's IDS files failed (docs/ids.md), after it, so the approver and the tag keep it.
    /// </summary>
    public static string? IssueNote(JsonObject p, string? recorded)
    {
        var note = (string?)p["note"] ?? recorded;
        if (((string?)p["idsOverride"])?.Trim() is not { Length: > 0 } why) return note;
        var failing = (int?)Lapin.Model.Json.Number(p["idsFailing"]);
        var line = $"Submitted despite {(failing is { } n ? $"{n} failing IDS specification{(n == 1 ? "" : "s")}" : "failing IDS specifications")}: {why}";
        return note is { Length: > 0 } ? $"{note}\n{line}" : line;
    }
}
