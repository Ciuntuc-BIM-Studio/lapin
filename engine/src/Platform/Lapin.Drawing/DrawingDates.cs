using System.Globalization;
using System.Text.Json.Nodes;

namespace Lapin.Drawing;

/// <summary>
/// A date as drawings write it (audit 22.11.2026 §4: "dd.MM.yyyy" was written in nine places, only documents followed their
/// language): the project's date format (<see cref="StyleBook.DateFormat"/>), else dd.MM.yyyy, as every title block, sheet
/// and symbol wrote it before. Documents use the same format when the project sets one, else their language's.
/// </summary>
public static class DrawingDates
{
    /// <summary>What drawings wrote before and still write by default (Romanian, and most of Europe).</summary>
    public const string Default = "dd.MM.yyyy";

    /// <summary>The formats a project may choose: day.month.year, ISO 8601, day/month/year, month/day/year.</summary>
    public static readonly string[] Formats = [Default, "yyyy-MM-dd", "dd/MM/yyyy", "MM/dd/yyyy"];

    /// <summary>A stored format read: one of <see cref="Formats"/>, else null.</summary>
    public static string? Of(JsonNode? v) => v is JsonValue j && j.TryGetValue<string>(out var f) && Formats.Contains(f) ? f : null;

    /// <summary>A date in the project's format.</summary>
    public static string Format(StyleBook? book, DateTime d) => d.ToString(book?.DateFormat ?? Default, CultureInfo.InvariantCulture);

    /// <inheritdoc cref="Format(StyleBook?, DateTime)"/>
    public static string Format(StyleBook? book, DateOnly d) => d.ToString(book?.DateFormat ?? Default, CultureInfo.InvariantCulture);
}
