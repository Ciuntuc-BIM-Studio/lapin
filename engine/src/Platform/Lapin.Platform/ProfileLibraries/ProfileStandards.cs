using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Platform.ProfileLibraries;

/// <summary>
/// The standards the profile pickers group by (decision 457, docs/plan-profile-libraries.md): a library says its own
/// (<c>standard</c>: Lapin's is „EN”), else its profiles' standard names it (EN 10365:2017 → EN, ASTM A6 → AISC / ASTM),
/// else the library is a group of its own under its name (a firm's, the user's). A project keeps which of them its pickers
/// offer (<c>profileStandards</c> on the project, none: all), set when it is made from the user's choice for new projects;
/// it keeps only the profiles its types use (<see cref="ProjectProfiles"/>), never a whole library, so the choice weighs
/// nothing in the file.
/// </summary>
public static class ProfileStandards
{
    public const string Key = "profileStandards";
    public const string En = "EN";
    public const string Aisc = "AISC / ASTM";

    /// <summary>A standard's name as written (EN 10365:2017, DIN EN 10025-2, ASTM A6/A6M, BS 4-1) as the group it is shown under, or null when none is recognised.</summary>
    public static string? Group(string? standard)
    {
        if (string.IsNullOrWhiteSpace(standard)) return null;
        var s = standard.Trim().ToUpperInvariant();
        // National editions of a European norm (DIN EN, SR EN, BS EN, NF EN, UNI EN, PN-EN…) are EN.
        if (s.StartsWith("EN") && (s.Length == 2 || !char.IsLetter(s[2]))) return En;
        if (System.Text.RegularExpressions.Regex.IsMatch(s, @"^[A-Z]{2,4}[\s\-]?EN[\s\-\d]")) return En;
        if (s.StartsWith("ASTM") || s.StartsWith("AISC")) return Aisc;
        foreach (var (prefix, group) in Known)
            if (s.StartsWith(prefix) && (s.Length == prefix.Length || !char.IsLetter(s[prefix.Length]))) return group;
        return null;
    }

    static readonly (string Prefix, string Group)[] Known =
    [
        ("AS/NZS", "AS/NZS"), ("AS", "AS/NZS"), ("BS", "BS"), ("DIN", "DIN"), ("JIS", "JIS"), ("GB", "GB"), ("CSA", "CSA"), ("IS", "IS"), ("GOST", "GOST"),
    ];

    /// <summary>The group a library's profile is shown under: the library's own standard, else its profile's, else the library's name.</summary>
    public static string Of(ProfileLibrary library, SectionProfile p) =>
        Clean(library.Standard) ?? Group(p.Standard) ?? library.Name;

    /// <summary>The groups a library's profiles fall in, in their order of first appearance.</summary>
    public static List<string> Of(ProfileLibrary library) => library.Profiles.Select(p => Of(library, p)).Distinct().ToList();

    static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    /// <summary>The standards a project's pickers offer; null: all of them.</summary>
    public static List<string>? OfProject(IModelView m) =>
        Spatial.Project(m)?.Attrs[Key] is JsonArray a ? a.Select(x => Clean((string?)x)).OfType<string>().Distinct().ToList() : null;

    /// <summary>A list as sent (<c>null</c>, or names): its names trimmed and once each, null when none was sent.</summary>
    public static List<string>? Read(JsonNode? n) =>
        n is JsonArray a ? a.Select(x => x is JsonValue v && v.TryGetValue<string>(out var s) ? Clean(s) : null).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToList() : null;

    /// <summary>The project's standards set on its attributes (null takes them away: all).</summary>
    public static void Write(JsonObject projectAttrs, IReadOnlyList<string>? standards)
    {
        if (standards == null) projectAttrs.Remove(Key);
        else projectAttrs[Key] = new JsonArray(standards.Select(s => (JsonNode)s).ToArray());
    }

    public static void Register(IModuleRegistry r)
    {
        // {standards: [names] | null}: the standards the project's profile pickers offer (null: all); its own copies are always offered.
        r.Command("profiles.setStandards", "Profile standards", (ctx, a) =>
        {
            var project = Spatial.Project(ctx.Model) ?? throw new CommandException("The model has no project.");
            var pa = project.CloneAttrs();
            Write(pa, Read(a["standards"]));
            ctx.Replace(project.With(attrs: pa));
            return null;
        });
    }
}
