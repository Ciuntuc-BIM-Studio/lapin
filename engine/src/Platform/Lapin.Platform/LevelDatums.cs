using System.Text.Json.Nodes;
using Lapin.Drawing;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// What a level is read from (in-app report #190, decision 867, docs/plan-level-datums.md): relative to the project's ±0.00
/// (model 0, or a storey the user names), absolute (the altitude: the model z plus the base point's elevation, the one source
/// of the project's height above the sea; the survey point's elevation is informational) or both, „+0.00 (+650.00)”. Set on
/// the project (<c>levelDatum = {mode, zeroStorey?}</c>, command <c>project.levelDatum</c>), over it on a view
/// (<c>levelDatum</c>: project, relative, absolute or both; <c>view.levelDatum</c>) and on a single level dimension
/// (<c>datum</c>). Its texts are the <c>levels</c> notation domain (decision 864): the templates for each mode and the tie
/// „±0.00 = 650.00”, the zero sign, the plus and the minus, with indicative presets (France NGF, Romania MN75, Germany NHN).
/// <see cref="Standards.FormatLevel(double, StyleBook, bool)"/> stays the number formatter underneath; every level a drawing,
/// a schedule or a document writes goes through here. With nothing set it writes exactly what Lapin wrote before, and
/// nothing is stored until the user changes something (no upgrade).
/// </summary>
public sealed class LevelDatum
{
    public const string Key = "levelDatum";
    public const string Relative = "relative", Absolute = "absolute", Both = "both", Project = "project";
    public static readonly string[] Modes = [Relative, Absolute, Both];

    /// <summary>The levels' notation: the defaults are exactly what Lapin wrote (±0.00, +3.00, −0.45); the presets are indicative.</summary>
    public static readonly NotationDomain Domain = new("levels", "Levels",
    [
        new("relative", "Relative level", "{level}", ["level"]),
        new("absolute", "Altitude", "{altitude}", ["altitude"]),
        new("both", "Level and altitude", "{level} ({altitude})", ["level", "altitude"]),
        new("tie", "Tie note", "{level} = {altitude}", ["level", "altitude"]),
        new("zero", "Zero sign", "±", [], NotationKey.Symbol),
        new("plus", "Plus sign", "+", [], NotationKey.Symbol),
        new("minus", "Minus sign", "−", [], NotationKey.Symbol),
        new("altplus", "Plus sign before an altitude", "+", [], NotationKey.Symbol),
    ],
    [
        new("fr", "France (NGF)", "Nivellement général de la France, IGN 69: the altitude without its plus, NGF after it", new Dictionary<string, string>
        {
            ["absolute"] = "{altitude} NGF", ["both"] = "{level} ({altitude} NGF)", ["tie"] = "{level} = {altitude} NGF", ["altplus"] = "",
        }),
        new("ro", "Romania (MN75)", "Marea Neagră 1975, the Romanian height system", new Dictionary<string, string>
        {
            ["absolute"] = "{altitude} MN75", ["both"] = "{level} ({altitude} MN75)", ["tie"] = "{level} = {altitude} (MN75)",
        }),
        new("de", "Germany (NHN)", "Normalhöhennull, DHHN2016: the altitude without its plus, ü. NHN after it", new Dictionary<string, string>
        {
            ["absolute"] = "{altitude} ü. NHN", ["both"] = "{level} ({altitude} ü. NHN)", ["tie"] = "{level} = {altitude} m ü. NHN", ["altplus"] = "",
        }),
    ],
    new Dictionary<string, string> { ["level"] = "+3.00", ["altitude"] = "+653.00" });

    /// <summary>relative, absolute or both.</summary>
    public string Mode { get; }
    /// <summary>The model z of ±0.00: 0, or the zero storey's elevation.</summary>
    public double Zero { get; }
    /// <summary>The storey ±0.00 is at, or null (model 0).</summary>
    public string? ZeroStorey { get; }
    /// <summary>The altitude of model z = 0: the base point's elevation.</summary>
    public double Altitude { get; }
    public Notation Notation { get; }

    LevelDatum(string mode, double zero, string? zeroStorey, double altitude, Notation notation) =>
        (Mode, Zero, ZeroStorey, Altitude, Notation) = (mode, zero, zeroStorey, altitude, notation);

    /// <summary>A mode as asked (relative, absolute, both), else null (project, or one Lapin does not know).</summary>
    public static string? ModeOf(string? s) => s != null && Modes.Contains(s) ? s : null;

    /// <summary>The project's datum: its mode, over it the view's (<paramref name="view"/>) and a single mark's own (<paramref name="own"/>).</summary>
    public static LevelDatum Of(IModelView m, Entity? view = null, string? own = null)
    {
        var o = Spatial.Project(m)?.Attrs[Key] as JsonObject;
        var mode = ModeOf(own) ?? ModeOf(view?.Str(Key)) ?? ModeOf((string?)o?["mode"]) ?? Relative;
        var zs = (string?)o?["zeroStorey"];
        var storey = zs != null && m.Get(zs) is { Class: IfcClass.BuildingStorey } st ? st : null;
        return new LevelDatum(mode, storey?.Num("elevation") ?? 0, storey?.Id, Location.Base(m).Elevation, Notation.Of(m, Domain));
    }

    /// <summary>The datum a view draws in (and a mark's own over it).</summary>
    public static LevelDatum Of(ViewContext c, string? own = null) => Of(c.Model, c.View, own);

    /// <summary>This datum in another mode (a mark's own); project or null: as it is.</summary>
    public LevelDatum As(string? mode) => ModeOf(mode) is { } md && md != Mode ? new LevelDatum(md, Zero, ZeroStorey, Altitude, Notation) : this;

    /// <summary>Lapin's own: relative to model 0, the default notation, what Lapin wrote before.</summary>
    public bool IsDefault => Mode == Relative && Zero == 0 && Notation.IsDefault;

    /// <summary>
    /// A number as levels are written (the formatter underneath): the project's decimals and trailing zeros, feet and inches
    /// in an imperial project; without standards two decimals in metres. Signed ±, + or a true minus.
    /// </summary>
    public static string Number(double v, StyleBook? book, bool plusAtZero = false) =>
        book == null ? Standards.FormatLevel(v, Standards.DefaultLevelDecimals, false, plusAtZero) : Standards.FormatLevel(v, book, plusAtZero);

    /// <summary>A number as levels are written in <paramref name="units"/> without the project's standards: metres with two decimals, or feet and inches.</summary>
    public static string InUnits(double v, string units) => Standards.FormatLevel(v, units);

    /// <summary>A number as levels are written in metres with a mark's own decimals and trailing zeros.</summary>
    public static string Number(double v, int decimals, bool suppressZeros) => Standards.FormatLevel(v, decimals, suppressZeros);

    /// <summary>A level written in a drawing's view: as the view's datum has it (a mark's own over it).</summary>
    public static string Text(ViewContext c, double z, bool plusAtZero = false, string? own = null) => Of(c, own).Text(z, c.Book, plusAtZero);

    /// <summary>A value that is a height of its own (the base point's or the survey point's elevation): only formatted, read from no datum.</summary>
    public static string Plain(double v, StyleBook? book) => Number(v, book);

    /// <summary>A level at model z, as this datum writes it.</summary>
    public string Text(double z, StyleBook? book, bool plusAtZero = false) => TextWith(z, v => Number(v, book, plusAtZero));

    /// <summary>A level at model z, its numbers by <paramref name="number"/> (a dimension's own decimals).</summary>
    public string TextWith(double z, Func<double, string> number)
    {
        if (IsDefault) return number(z);
        return Notation.Format(Mode, t => t switch { "level" => Signed(number(z - Zero), false), "altitude" => Signed(number(z + Altitude), true), _ => null });
    }

    /// <summary>The level relative to ±0.00 alone, whatever the mode.</summary>
    public string LevelOf(double z, StyleBook? book, bool plusAtZero = false) => Signed(Number(z - Zero, book, plusAtZero), false);

    /// <summary>The altitude alone, whatever the mode.</summary>
    public string AltitudeOf(double z, StyleBook? book, bool plusAtZero = false) => Signed(Number(z + Altitude, book, plusAtZero), true);

    /// <summary>The tie note: „±0.00 = +650.00” (the zero's altitude).</summary>
    public string Tie(StyleBook? book) =>
        Notation.Format("tie", t => t switch { "level" => Signed(Number(0, book), false), "altitude" => Signed(Number(Zero + Altitude, book), true), _ => null });

    /// <summary>A formatted number's sign as the notation writes it (±, +, −; an altitude's plus its own).</summary>
    string Signed(string text, bool altitude)
    {
        if (text.Length == 0) return text;
        var sign = text[0] switch { '±' => "zero", '+' => altitude ? "altplus" : "plus", '−' => "minus", _ => null };
        return sign == null ? text : Notation[sign] + text[1..];
    }

    /// <summary>
    /// The terrains whose datum (the surveyed height of model 0) differs from the project's altitude: each one's id, name and
    /// datum, for the warning that offers to take it.
    /// </summary>
    public static IEnumerable<(Entity Terrain, double Datum)> Differing(IModelView m)
    {
        var altitude = Location.Base(m).Elevation;
        foreach (var e in m.OfClass(IfcClass.GeographicElement))
            if (e.NumOrNull("datum") is { } d && e.Attrs["points"] is JsonArray && Math.Abs(d - altitude) > 1e-6) yield return (e, d);
    }

    public static void Register(IModuleRegistry r)
    {
        r.Service(Domain);
        // The project's datum: {mode, zeroStorey, zero, altitude, tie (its text), terrains: [{id, name, datum}] whose datum differs}.
        r.Query("project.levelDatum", (q, _) =>
        {
            var d = Of(q.Model);
            var o = Spatial.Project(q.Model)?.Attrs[Key] as JsonObject;
            return new JsonObject
            {
                ["mode"] = ModeOf((string?)o?["mode"]) ?? Relative, ["zeroStorey"] = d.ZeroStorey, ["zero"] = d.Zero, ["altitude"] = d.Altitude,
                ["zeroAltitude"] = d.Zero + d.Altitude, ["tie"] = d.Tie(DraftingResources.Book(q.Model)),
                ["terrains"] = new JsonArray([.. Differing(q.Model).Select(x => (JsonNode)new JsonObject { ["id"] = x.Terrain.Id, ["name"] = x.Terrain.Name, ["datum"] = x.Datum })]),
            };
        });
        // {mode?: relative | absolute | both | null, zeroStorey?: id | null, tie?: the altitude of ±0.00, fromTerrain?: a terrain whose
        // datum becomes the project's altitude}. The tie writes the base point's elevation (tie − the zero storey's elevation): the model
        // never moves. One undo step.
        r.Command("project.levelDatum", "Level datum", (ctx, a) =>
        {
            var project = Spatial.Project(ctx.Model) ?? throw new CommandException("The model has no project.");
            var attrs = project.CloneAttrs();
            var o = (attrs[Key] as JsonObject)?.DeepClone() as JsonObject ?? new JsonObject();
            if (a.ContainsKey("mode"))
            {
                var mode = (string?)a["mode"];
                if (mode is null or Relative or Project) o.Remove("mode");
                else o["mode"] = ModeOf(mode) ?? throw new CommandException("Levels are written relative to ±0.00 (relative), as altitudes (absolute) or both.");
            }
            if (a.ContainsKey("zeroStorey"))
            {
                if ((string?)a["zeroStorey"] is { Length: > 0 } id)
                    o["zeroStorey"] = ctx.Model.Get(id) is { Class: IfcClass.BuildingStorey } st ? st.Id : throw new CommandException("±0.00 is at a storey of the project.");
                else o.Remove("zeroStorey");
            }
            if (o.Count > 0) attrs[Key] = o; else attrs.Remove(Key);
            double? altitude = null;
            if (Json.Number(a["tie"]) is { } tie)
            {
                if (!double.IsFinite(tie)) throw new CommandException("The altitude of ±0.00 is a number.");
                var zs = (string?)o["zeroStorey"];
                altitude = tie - (zs != null && ctx.Model.Get(zs) is { } st ? st.Num("elevation") : 0);
            }
            if ((string?)a["fromTerrain"] is { } tid)
                altitude = ctx.Model.Get(tid) is { } t && t.NumOrNull("datum") is { } datum ? datum : throw new CommandException("That is not a terrain with a datum.");
            if (altitude is { } alt)
            {
                var loc = (attrs["location"] as JsonObject)?.DeepClone() as JsonObject ?? new JsonObject();
                var b = loc["base"] as JsonObject ?? new JsonObject();
                b["elevation"] = Math.Round(alt, 9);
                loc["base"] = b.DeepClone();
                attrs["location"] = loc;
            }
            ctx.Replace(project.With(attrs: attrs));
            return null;
        });
        // A view's own datum ({id, datum: project | relative | absolute | both}), or every view placed on a sheet ({sheet, datum}). One undo step.
        r.Command("view.levelDatum", "Level datum in view", (ctx, a) =>
        {
            var datum = (string?)a["datum"] ?? Project;
            if (datum != Project && ModeOf(datum) == null) throw new CommandException("A view writes its levels as the project's (project), relative, absolute or both.");
            void Put(JsonObject attrs) { if (datum == Project) attrs.Remove(Key); else attrs[Key] = datum; }
            if ((string?)a["sheet"] is { } sid)
            {
                var sheet = ctx.Model.Get(sid) is { Class: Sheets.Class } s ? s : throw new CommandException("There is no such sheet.");
                var ids = new JsonArray();
                foreach (var vp in Sheets.Viewports(sheet).Select(v => v.ViewId).Distinct())
                    if (Views.Find(ctx.Model, vp) is { } v) ids.Add((JsonNode)Views.OwnView(ctx, v, Put));
                return new JsonObject { ["ids"] = ids };
            }
            var view = Views.Find(ctx.Model, (string?)a["id"] ?? "") ?? throw new CommandException("There is no such view.");
            return new JsonObject { ["id"] = Views.OwnView(ctx, view, Put) };
        });
    }
}
