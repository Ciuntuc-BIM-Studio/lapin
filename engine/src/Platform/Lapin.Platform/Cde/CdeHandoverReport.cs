using System.Globalization;
using Lapin.Drawing.Export;
using Lapin.Platform.Cde.Remarks;

namespace Lapin.Platform.Cde;

/// <summary>
/// The handover report (Atrium docs/plan-cde-web.md §5, stage W4): one workbook (Office Open XML written by hand, <see cref="XlsxWriter"/>,
/// no library) with what a handover hands over besides the files: the project's issues (its remarks, BCF topics) with their status,
/// priority, assignee and dates, every comment, the issue journal (who changed what, from the CDE server's append-only event log), and,
/// when two issue tags are given, the handover diff (documents added, removed and changed between the two). Sheets that have nothing are
/// still written with their headings, so the workbook's shape does not depend on the project.
/// </summary>
public static class CdeHandoverReport
{
    /// <summary>What the first sheet says about the report.</summary>
    public sealed record Header(string Project, string? Server, string Made, string? By, string? From, string? To, IReadOnlyList<string>? Notes = null);

    /// <summary>One row of the issue journal: one changed field of one request.</summary>
    public sealed record JournalRow(long Seq, string Date, string Author, string Type, string? TopicGuid, string? Title, string? Old, string? New);

    /// <summary>One document of the handover diff: <c>added</c>, <c>removed</c>, <c>changed</c> or <c>unchanged</c>, with each side's revision, status, issue and hash.</summary>
    public sealed record DiffRow(string Change, string Document, string? FromRevision, string? ToRevision, string? FromStatus, string? ToStatus, string? FromIssue, string? ToIssue, string? FromSha256, string? ToSha256);

    public static readonly string[] IssueColumns =
        ["#", "Title", "Type", "Status", "Priority", "Assigned to", "Due date", "Stage", "Labels", "Created", "Created by", "Modified", "Modified by", "Comments", "Description", "GUID"];
    public static readonly string[] CommentColumns = ["#", "Issue", "Date", "Author", "Comment", "Modified", "Modified by", "Issue GUID", "Comment GUID"];
    public static readonly string[] JournalColumns = ["Seq", "Date", "Author", "Change", "Issue", "Old value", "New value", "Issue GUID"];
    public static readonly string[] DiffColumns = ["Change", "Document", "From revision", "To revision", "From status", "To status", "From issue", "To issue", "From SHA-256", "To SHA-256"];

    /// <summary>
    /// The workbook: sheets Report, Issues, Comments, Journal and, when a diff is given, Handover diff. <paramref name="name"/> turns a
    /// BCF user id (an e-mail address on a CDE server) into a person's name; the issues are numbered by their index, else in the order
    /// they were made.
    /// </summary>
    public static byte[] Write(Header h, IReadOnlyList<BcfTopic> topics, Func<string?, string> name, IReadOnlyList<JournalRow>? journal, IReadOnlyList<DiffRow>? diff)
    {
        var ordered = topics.OrderBy(t => t.Index ?? int.MaxValue).ThenBy(t => t.CreationDate, StringComparer.Ordinal).ThenBy(t => t.Guid).ToList();
        var number = new Dictionary<Guid, int>();
        var next = ordered.Select(t => t.Index ?? 0).DefaultIfEmpty(0).Max();
        foreach (var t in ordered) number[t.Guid] = t.Index ?? ++next;

        string N(int i) => i.ToString(CultureInfo.InvariantCulture);
        var report = new List<IReadOnlyList<string>>
        {
            new[] { "Handover report", h.Project },
            new[] { "Server", h.Server ?? "" },
            new[] { "Made", h.Made },
            new[] { "By", h.By ?? "" },
            new[] { "From issue", h.From ?? "" },
            new[] { "To issue", h.To ?? "" },
            new[] { "Issues", N(topics.Count) },
            new[] { "Open issues", N(topics.Count(t => Remarks.Remarks.IsOpen(t.Status))) },
            new[] { "Comments", N(topics.Sum(t => t.Comments.Count)) },
            new[] { "Journal entries", journal == null ? "" : N(journal.Count) },
        };
        if (diff != null)
            foreach (var change in new[] { "added", "removed", "changed" })
                report.Add(new[] { $"Documents {change}", N(diff.Count(d => d.Change == change)) });
        foreach (var note in h.Notes ?? []) report.Add(new[] { "Note", note });

        var issues = new List<IReadOnlyList<string>> { IssueColumns };
        foreach (var t in ordered)
            issues.Add(new[]
            {
                N(number[t.Guid]), t.Title, t.Type ?? "", t.Status ?? "", t.Priority ?? "", t.AssignedTo is { Length: > 0 } a ? name(a) : "", t.DueDate ?? "", t.Stage ?? "",
                string.Join(", ", t.Labels), t.CreationDate, name(t.CreationAuthor), t.ModifiedDate ?? "", t.ModifiedAuthor is { Length: > 0 } m ? name(m) : "",
                N(t.Comments.Count), t.Description ?? "", t.Guid.ToString("D"),
            });

        var comments = new List<IReadOnlyList<string>> { CommentColumns };
        foreach (var t in ordered)
            foreach (var c in t.Comments.OrderBy(c => c.Date, StringComparer.Ordinal))
                comments.Add(new[]
                {
                    N(number[t.Guid]), t.Title, c.Date, name(c.Author), c.Text, c.ModifiedDate ?? "", c.ModifiedAuthor is { Length: > 0 } m ? name(m) : "",
                    t.Guid.ToString("D"), c.Guid.ToString("D"),
                });

        var log = new List<IReadOnlyList<string>> { JournalColumns };
        foreach (var e in journal ?? [])
            log.Add(new[] { e.Seq.ToString(CultureInfo.InvariantCulture), e.Date, e.Author, e.Type, e.Title ?? "", e.Old ?? "", e.New ?? "", e.TopicGuid ?? "" });

        var sheets = new List<(string, IReadOnlyList<IReadOnlyList<string>>)> { ("Report", report), ("Issues", issues), ("Comments", comments), ("Journal", log) };
        if (diff != null)
        {
            var rows = new List<IReadOnlyList<string>> { DiffColumns };
            foreach (var d in diff.OrderBy(d => Array.IndexOf(Changes, d.Change)).ThenBy(d => d.Document, StringComparer.OrdinalIgnoreCase))
                rows.Add(new[] { d.Change, d.Document, d.FromRevision ?? "", d.ToRevision ?? "", d.FromStatus ?? "", d.ToStatus ?? "", d.FromIssue ?? "", d.ToIssue ?? "", d.FromSha256 ?? "", d.ToSha256 ?? "" });
            sheets.Add(("Handover diff", rows));
        }
        return XlsxWriter.Write(sheets);
    }

    static readonly string[] Changes = ["added", "removed", "changed", "unchanged"];
}
