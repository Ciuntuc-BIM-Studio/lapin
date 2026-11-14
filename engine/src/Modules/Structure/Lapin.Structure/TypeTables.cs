using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Platform.ProfileLibraries;

namespace Lapin.Structure;

/// <summary>
/// Structure types read from a table (report #135): a CSV or XLSX, one row a type (a beam, a column or a precast lintel, which
/// is a beam type of PredefinedType LINTEL). Its columns guessed from the headings (English, Romanian, German, French, Czech;
/// a producer's export's „height.value” and „height.unit” understood) as the profile tables' are; the profile resolved
/// against the project's profiles, the profile libraries the interface sends and Lapin's catalogue, else a rectangle of its
/// h and b. Rows of one section whose names differ only by their length can be one type with its lengths. A preview says
/// what is made, what is the same and what differs; the import takes the user's choice for each that differs (decision 506:
/// update, keep both, skip), in one step to undo. And the types written back as a CSV it reads.
/// </summary>
public static class TypeTables
{
    public static readonly string[] Fields = ["name", "kind", "profile", "h", "b", "material", "mark", "bearing", "manufacturer", "code", "length", "codes"];

    static readonly HashSet<string> Lengths = ["h", "b", "bearing", "length"];

    /// <summary>The headings each field is known by, folded (lower case, no diacritics, no dots, spaces, hyphens or underscores).</summary>
    static readonly Dictionary<string, string[]> Heads = new()
    {
        ["name"] = ["name", "typename", "designation", "product", "item", "denumire", "denumirea", "nume", "produs", "bezeichnung", "benennung", "produkt", "nom", "produit", "nazev", "vyrobek", "polozka"],
        ["kind"] = ["kind", "category", "element", "elementtype", "categorie", "tipelement", "tip", "kategorie", "bauteil", "art", "genre", "druh", "typprvku", "type", "typ"],
        ["profile"] = ["profile", "profil", "section", "sectiune", "profilul", "querschnitt", "coupe", "prurez", "shape"],
        ["h"] = ["h", "height", "depth", "inaltime", "inaltimea", "hohe", "hoehe", "hauteur", "vyska", "d"],
        ["b"] = ["b", "width", "latime", "latimea", "breite", "largeur", "sirka"],
        ["material"] = ["material", "materialul", "werkstoff", "materiau"],
        ["mark"] = ["mark", "tag", "marca", "marcaj", "eticheta", "pozitie", "kennzeichen", "zeichen", "position", "pos", "repere", "oznaceni", "pozice"],
        ["bearing"] = ["bearing", "bearinglength", "minbearing", "reazem", "rezemare", "lungimedereazem", "auflager", "auflagerlange", "auflagertiefe", "appui", "longueurdappui", "ulozeni", "delkaulozeni", "minimalniulozeni"],
        ["manufacturer"] = ["manufacturer", "maker", "producer", "brand", "producator", "fabricant", "hersteller", "marque", "vyrobce", "znacka"],
        ["code"] = ["code", "cod", "codprodus", "articlecode", "article", "artikel", "artikelnummer", "artnr", "sku", "reference", "ref", "kod", "kodvyrobku", "objednacicislo", "cislo", "ean"],
        ["length"] = ["length", "lengths", "lungime", "lungimi", "lungimea", "lange", "langen", "longueur", "longueurs", "delka", "delky", "l"],
        ["codes"] = ["codes", "coduri", "codes par longueur", "kody"],
    };

    /// <summary>A heading's suffix that says what its column holds about the field before the dot (a producer's export).</summary>
    static readonly string[] Parts = ["value", "unit", "name", "note", "label", "description", "desc"];

    /// <summary>Lower case, no diacritics (Czech, Romanian, German, French, Polish: by a table, the engine may run without the culture data), then as the profile tables fold.</summary>
    public static string Fold(string s)
    {
        var sb = new StringBuilder();
        foreach (var ch in s.ToLowerInvariant())
            sb.Append(ch switch
            {
                'á' or 'à' or 'â' or 'ä' or 'ă' or 'ã' or 'å' or 'ą' => 'a', 'č' or 'ć' or 'ç' => 'c', 'ď' => 'd', 'é' or 'è' or 'ê' or 'ë' or 'ě' or 'ę' => 'e',
                'í' or 'ì' or 'î' or 'ï' => 'i', 'ľ' or 'ĺ' or 'ł' => 'l', 'ň' or 'ñ' or 'ń' => 'n', 'ó' or 'ò' or 'ô' or 'ö' or 'õ' => 'o', 'ř' => 'r',
                'š' or 'ș' or 'ş' or 'ś' => 's', 'ť' or 'ț' or 'ţ' => 't', 'ú' or 'ù' or 'û' or 'ü' or 'ů' => 'u', 'ý' or 'ÿ' => 'y', 'ž' or 'ź' or 'ż' => 'z',
                _ => ch,
            });
        return ProfileTables.Fold(sb.ToString());
    }

    /// <summary>The columns guessed: the header row, each field's column and unit, and the columns that say a field's unit on each row.</summary>
    public sealed record Guessed(int Header, Dictionary<string, ProfileTables.Column?> Map, Dictionary<string, int> UnitColumns);

    public static Guessed Guess(List<List<string>> table)
    {
        for (var i = 0; i < Math.Min(20, table.Count); i++)
        {
            var row = table[i];
            var names = new List<string>();
            var units = new List<string>();
            var unitColumns = new Dictionary<string, int>();
            for (var at = 0; at < row.Count; at++)
            {
                var raw = (row[at] ?? "").Trim();
                var dot = raw.LastIndexOf('.');
                if (dot > 0 && Parts.Contains(raw[(dot + 1)..].Trim().ToLowerInvariant()))
                {
                    var part = raw[(dot + 1)..].Trim().ToLowerInvariant();
                    var head = raw[..dot];
                    if (part == "value") { var (n0, u0) = ProfileTables.SplitHeading(head); names.Add(Fold(n0)); units.Add(u0); continue; }
                    if (part == "unit" && FieldOf(Fold(head)) is { } uf) unitColumns.TryAdd(uf, at);
                    names.Add(""); units.Add("");
                    continue;
                }
                var (n, u) = ProfileTables.SplitHeading(raw);
                names.Add(Fold(n));
                units.Add(u);
            }
            var map = new Dictionary<string, ProfileTables.Column?>();
            var used = new HashSet<int>();
            foreach (var exact in new[] { true, false })
                foreach (var f in Fields)
                {
                    if (map.ContainsKey(f)) continue;
                    for (var at = 0; at < names.Count; at++)
                    {
                        if (used.Contains(at) || names[at].Length == 0) continue;
                        var h = names[at];
                        if (!Heads[f].Any(k => exact ? h == Fold(k) : Fold(k) is { Length: >= 4 } fk && h.StartsWith(fk, StringComparison.Ordinal))) continue;
                        var unit = Lengths.Contains(f) ? ProfileTables.UnitOf(units[at], "length") ?? ("mm", 1e-3) : ("", 1);
                        map[f] = new ProfileTables.Column(at, unit.Item1, unit.Item2);
                        used.Add(at);
                        break;
                    }
                }
            foreach (var f in Fields) map.TryAdd(f, null);
            // A „Type” column with no name column beside it holds the types' names.
            if (map["name"] == null && map["kind"] is { } k && Fold(row[k.Index]) is "type" or "typ" or "tip") (map["name"], map["kind"]) = (k, null);
            if (map["name"] != null && (map["profile"] != null || map["h"] != null || map["b"] != null))
                return new(i, map, unitColumns);
        }
        return new(-1, Fields.ToDictionary(f => f, _ => (ProfileTables.Column?)null), []);
    }

    static string? FieldOf(string folded) => Fields.FirstOrDefault(f => Heads[f].Any(k => Fold(k) == folded));

    /// <summary>A kind from what a row writes, in the five languages: beam, column, lintel; null when it says none of them.</summary>
    public static string? KindOf(string? text)
    {
        var t = Fold(text ?? "");
        if (t.Length == 0) return null;
        if (t.StartsWith("lintel") || t.StartsWith("buiandrug") || t.StartsWith("sturz") || t.StartsWith("linteau") || t.StartsWith("preklad")) return "lintel";
        if (t.StartsWith("column") || t.StartsWith("stalp") || t.StartsWith("stutze") || t.StartsWith("saule") || t.StartsWith("poteau") || t.StartsWith("sloup") || t.StartsWith("pilier")) return "column";
        if (t.StartsWith("beam") || t.StartsWith("grinda") || t.StartsWith("trager") || t.StartsWith("balken") || t.StartsWith("unterzug") || t.StartsWith("poutre") || t.StartsWith("nosnik") || t.StartsWith("tram") || t.StartsWith("girder") || t.StartsWith("joist")) return "beam";
        return null;
    }

    /// <summary>A type a row (or the rows of one section) makes.</summary>
    public sealed record Row(int Line, string Name, string Kind)
    {
        public Section? Section { get; init; }
        /// <summary>A profile of a library the interface sent, copied into the project when the type is made.</summary>
        public SectionProfile? Profile { get; init; }
        public string? Library { get; init; }
        public string Material { get; init; } = "Reinforced concrete";
        public string? Mark { get; init; }
        public double? Bearing { get; init; }
        public string? Manufacturer { get; init; }
        public string? Code { get; init; }
        public List<double> Lengths { get; init; } = [];
        public List<string> Codes { get; init; } = [];
        public string? Problem { get; init; }
        public string Class => Kind == "column" ? IfcClass.ColumnType : IfcClass.BeamType;
    }

    /// <summary>A number with its unit: „1250”, „1,25 m”, „71mm”; the factor of a unit written in the cell or in the row's unit column, else the column's.</summary>
    static double? Number(string cell, double factor, string? rowUnit)
    {
        var m = System.Text.RegularExpressions.Regex.Match(cell.Trim(), @"^([-+]?\d+(?:[.,]\d+)?)\s*([a-zA-Z]*)");
        if (!m.Success || !double.TryParse(m.Groups[1].Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) return null;
        var unit = m.Groups[2].Value.Length > 0 ? m.Groups[2].Value : rowUnit;
        if (unit != null && ProfileTables.UnitOf(unit, "length") is { } u) factor = u.Factor;
        return v * factor;
    }

    /// <summary>
    /// The rows under the columns given. <paramref name="kind"/>: a row's kind when it says none. <paramref name="libraries"/>:
    /// the profile libraries the interface holds ({name, profiles}). <paramref name="merge"/>: rows of one kind, section,
    /// material, maker, mark and bearing with lengths made one type, named by what their names share.
    /// </summary>
    public static List<Row> Read(IModelView m, List<List<string>> table, int header, IReadOnlyDictionary<string, ProfileTables.Column?> map,
        IReadOnlyDictionary<string, int>? unitColumns, string kind, bool merge, JsonArray? libraries)
    {
        if (map.GetValueOrDefault("name") is null) throw new CommandException("Say which column holds the type's name.");
        string Cell(List<string> r, string f) => map.GetValueOrDefault(f) is { } c && c.Index >= 0 && c.Index < r.Count ? (r[c.Index] ?? "").Trim() : "";
        string? RowUnit(List<string> r, string f) => unitColumns?.GetValueOrDefault(f, -1) is int i and >= 0 && i < r.Count ? r[i] : null;
        double? Num(List<string> r, string f) => Cell(r, f) is { Length: > 0 } c && map[f] is { } col ? Number(c, col.Factor, RowUnit(r, f)) : null;
        var libs = (libraries ?? []).OfType<JsonObject>()
            .SelectMany(l => (l["profiles"] as JsonArray ?? []).Select(SectionProfile.From).OfType<SectionProfile>().Select(p => (Lib: (string?)l["name"], P: p))).ToList();
        static string Key(string s) => Fold(s.Replace('×', 'x').Replace('*', 'x'));
        var rows = new List<Row>();
        var line = header + 1;
        foreach (var r in table.Skip(header + 1))
        {
            line++;
            if (r.All(string.IsNullOrWhiteSpace)) continue;
            var name = Cell(r, "name");
            if (name.Length == 0) continue;
            var k = KindOf(Cell(r, "kind")) ?? kind;
            double? h = Num(r, "h"), b = Num(r, "b");
            var lengths = Cell(r, "length").Split(['|', '/', ';', ' '], StringSplitOptions.RemoveEmptyEntries).Select(x => map["length"] is { } lc ? Number(x, lc.Factor, RowUnit(r, "length")) : null).OfType<double>().Where(x => x > 0).ToList();
            var codes = Cell(r, "codes").Split(['|', '/', ';', ' '], StringSplitOptions.RemoveEmptyEntries).ToList();
            var code = Cell(r, "code") is { Length: > 0 } cd ? cd : null;
            if (codes.Count == 0 && code != null && lengths.Count == 1) codes = [code];
            var row = new Row(line, name, k)
            {
                Mark = Cell(r, "mark") is { Length: > 0 } mk ? mk : null, Bearing = Num(r, "bearing"), Manufacturer = Cell(r, "manufacturer") is { Length: > 0 } mf ? mf : null,
                Code = code, Lengths = lengths, Codes = codes,
            };
            var profile = Cell(r, "profile");
            var material = Cell(r, "material") is { Length: > 0 } mat ? mat : null;
            try
            {
                Section? section = null;
                SectionProfile? fromLib = null;
                string? libName = null;
                var pk = Key(profile);
                if (profile.Length == 0 || pk is "rectangle" or "rect" or "dreptunghi" or "rechteck" or "obdelnik" or "rectangulaire")
                    section = h is { } hh && b is { } bb ? Section.Rectangle(bb, hh) : throw new CommandException("it needs a profile, or its h and b");
                else if (pk is "circle" or "round" or "cerc" or "kreis" or "kruh" or "rond")
                    section = (h ?? b) is { } dd ? Section.Circle(dd) : throw new CommandException("a round section needs its diameter (h)");
                else if (m.OfClass(ProjectProfiles.Class).FirstOrDefault(e => Key(e.Name) == pk) is { } copy && ProjectProfiles.Read(copy) is { } own)
                    (section, material) = (Section.Parse(own.SectionJson(copy.Id)), material ?? ProjectProfiles.MaterialName(own.Material));
                else if (libs.FirstOrDefault(x => Key(x.P.Name) == pk) is { P: not null } lib)
                    (fromLib, libName, section, material) = (lib.P, lib.Lib, Section.Parse(lib.P.SectionJson(null)), material ?? ProjectProfiles.MaterialName(lib.P.Material));
                else if (Section.Named(profile) is { } named)
                    (section, material) = (named, material ?? "Steel");
                else throw new CommandException($"no library has the profile „{profile}”");
                rows.Add(row with { Section = Section.Parse(section.ToJson()), Profile = fromLib, Library = libName, Material = material ?? "Reinforced concrete" });
            }
            catch (CommandException ex) { rows.Add(row with { Problem = ex.Message.TrimEnd('.') }); }
        }
        if (merge) rows = Merge(rows);
        // The same name twice: the first kept, the others refused.
        var seen = new HashSet<(string, string)>();
        return rows.Select(x => x.Problem == null && !seen.Add((x.Class, x.Name.ToLowerInvariant())) ? x with { Problem = "the same name as a row above" } : x).ToList();
    }

    /// <summary>Rows of one kind, section, material, maker, mark and bearing, each with a length, made one type of all their lengths (each its code).</summary>
    static List<Row> Merge(List<Row> rows)
    {
        var o = new List<Row>();
        foreach (var g in rows.GroupBy(r => r.Problem == null && r.Kind != "column" && r.Lengths.Count > 0
            ? $"{r.Kind}|{r.Section!.ToJson().ToJsonString()}|{r.Material}|{r.Manufacturer}|{r.Mark}|{r.Bearing}" : $"#{r.Line}"))
        {
            var list = g.ToList();
            if (list.Count == 1) { o.Add(list[0]); continue; }
            var pairs = list.SelectMany(r => r.Lengths.Select((l, i) => (L: l, C: i < r.Codes.Count ? r.Codes[i] : r.Lengths.Count == 1 ? r.Code ?? "" : ""))).GroupBy(p => Math.Round(p.L, 6)).Select(p => p.First()).OrderBy(p => p.L).ToList();
            o.Add(list[0] with { Name = CommonName(list.Select(r => r.Name).ToList()), Lengths = pairs.Select(p => p.L).ToList(), Codes = pairs.Select(p => p.C).ToList(), Code = null });
        }
        return o;
    }

    /// <summary>What names share up to a word's end, its separators trimmed: „KP 7 - 100” and „KP 7 - 125” → „KP 7”; the first name when they share nothing.</summary>
    public static string CommonName(IReadOnlyList<string> names)
    {
        var p = names[0];
        foreach (var n in names.Skip(1))
        {
            var i = 0;
            while (i < p.Length && i < n.Length && char.ToLowerInvariant(p[i]) == char.ToLowerInvariant(n[i])) i++;
            p = p[..i];
        }
        // Cut back to a word's end when a name goes on in the same word.
        if (names.Any(n => n.Length > p.Length && p.Length > 0 && char.IsLetterOrDigit(n[p.Length]) && char.IsLetterOrDigit(p[^1])))
        {
            var cut = p.Length;
            while (cut > 0 && char.IsLetterOrDigit(p[cut - 1])) cut--;
            p = p[..cut];
        }
        p = p.TrimEnd(' ', '-', '–', '—', '(', '_', '/', ',', '.', ':');
        return p.Length > 0 ? p : names[0];
    }

    // ------------------------------------------------------------------ what it would do

    /// <summary>The attributes a row gives its type, the material by its name (as compared): section, material, the lintel's facts.</summary>
    static JsonObject Wanted(Row r)
    {
        var o = new JsonObject { ["section"] = r.Section!.ToJson(), ["material"] = r.Material };
        LintelTypes.Write(o, r.Lengths, r.Codes, r.Code, r.Manufacturer, r.Bearing, r.Mark);
        return o;
    }

    static readonly string[] Compared = ["section", "material", LintelTypes.LengthsKey, LintelTypes.CodesKey, LintelTypes.CodeKey, LintelTypes.ManufacturerKey, LintelTypes.BearingKey, LintelTypes.MarkKey];

    /// <summary>The project's type as a row would write it (its material by name), the keys compared only.</summary>
    static JsonObject Has(IModelView m, Entity t)
    {
        var o = new JsonObject();
        foreach (var k in Compared)
            if (t.Attrs[k] is { } v) o[k] = k == "material" ? m.Get((string?)v ?? "")?.Name : v.DeepClone();
        return o;
    }

    /// <summary>The type of the row's class and name already in the project, and what differs from it (empty: the same).</summary>
    public static (Entity? Type, List<string> Differs) Against(IModelView m, Row r)
    {
        var t = m.OfClass(r.Class).FirstOrDefault(e => string.Equals(e.Name, r.Name, StringComparison.OrdinalIgnoreCase));
        if (t == null) return (null, []);
        var differs = LibraryMerge.Differs(Wanted(r), Has(m, t)).Select(d => d == LibraryMerge.Label("section") ? "section" : d).ToList();
        if (r.Kind == "lintel" && t.PredefinedType != LintelTypes.Lintel) differs.Add("kind");
        return (t, differs);
    }

    public static JsonObject Preview(IModelView m, List<Row> rows)
    {
        var list = new JsonArray();
        var conflicts = new JsonArray();
        foreach (var r in rows)
        {
            var o = new JsonObject
            {
                ["line"] = r.Line, ["name"] = r.Name, ["kind"] = r.Kind, ["section"] = r.Section?.Label, ["material"] = r.Material,
                ["lengths"] = new JsonArray(r.Lengths.Select(l => (JsonNode)l).ToArray()),
            };
            if (r.Mark != null) o["mark"] = r.Mark;
            if (r.Bearing != null) o["bearing"] = r.Bearing;
            if (r.Manufacturer != null) o["manufacturer"] = r.Manufacturer;
            if (r.Problem != null) { o["status"] = "error"; o["problem"] = r.Problem; list.Add((JsonNode)o); continue; }
            var (t, differs) = Against(m, r);
            o["status"] = t == null ? "new" : differs.Count == 0 ? "same" : "differs";
            if (t != null && differs.Count > 0)
            {
                o["differs"] = new JsonArray(differs.Select(d => (JsonNode)d).ToArray());
                conflicts.Add((JsonNode)new JsonObject { ["key"] = r.Name, ["name"] = r.Name, ["label"] = r.Kind, ["differs"] = new JsonArray(differs.Select(d => (JsonNode)d).ToArray()) });
            }
            list.Add((JsonNode)o);
        }
        return new JsonObject { ["rows"] = list, ["conflicts"] = conflicts };
    }

    // ------------------------------------------------------------------ the import

    /// <summary>
    /// The rows made types: a new one made; one the same as the project's left as it is; one that differs as chosen (by its
    /// name): updated in place (its elements and the lintel rules naming it follow; the default), made beside as „Name (2)”,
    /// or skipped. Rows with a problem are skipped.
    /// </summary>
    public static JsonObject Import(ICommandContext ctx, List<Row> rows, IReadOnlyDictionary<string, string> choices)
    {
        int made = 0, changed = 0, reused = 0, skipped = 0;
        var renamed = new JsonArray();
        var ids = new JsonArray();
        foreach (var r in rows)
        {
            if (r.Problem != null) { skipped++; continue; }
            var (t, differs) = Against(ctx.Model, r);
            if (t != null && differs.Count == 0) { reused++; ids.Add((JsonNode?)JsonValue.Create(t.Id)); continue; }
            var choice = t == null ? "new" : choices.GetValueOrDefault(r.Name, LibraryMerge.Update);
            if (choice == LibraryMerge.Skip) { skipped++; continue; }
            var section = SectionFor(ctx, r);
            if (choice == LibraryMerge.Update && t != null)
            {
                var attrs = t.CloneAttrs();
                attrs["section"] = section.ToJson();
                attrs["material"] = Materials.Ensure(ctx, r.Material).Id;
                LintelTypes.Write(attrs, r.Lengths, r.Codes, r.Code, r.Manufacturer, r.Bearing, r.Mark);
                ctx.Replace(t.With(attrs: attrs, predefinedType: r.Kind == "lintel" ? LintelTypes.Lintel : null));
                changed++;
                ids.Add((JsonNode?)JsonValue.Create(t.Id));
                continue;
            }
            var name = t == null ? r.Name : LibraryMerge.FreeName(ctx.Model, r.Class, r.Name);
            if (t != null) renamed.Add((JsonNode?)JsonValue.Create(name));
            var made1 = r.Kind == "column" ? Columns.CreateType(ctx, name, section, null, r.Material) : Beams.CreateType(ctx, name, section, r.Material);
            var a2 = made1.CloneAttrs();
            LintelTypes.Write(a2, r.Lengths, r.Codes, r.Code, r.Manufacturer, r.Bearing, r.Mark);
            ctx.Replace(made1.With(attrs: a2, predefinedType: r.Kind == "lintel" ? LintelTypes.Lintel : null));
            made++;
            ids.Add((JsonNode?)JsonValue.Create(made1.Id));
        }
        if (rows.All(x => x.Problem != null)) throw new CommandException("No type could be made from the table: each row needs a name and a profile, or its h and b.");
        return new JsonObject { ["typesMade"] = made, ["typesChanged"] = changed, ["typesReused"] = reused, ["typesSkipped"] = skipped, ["renamed"] = renamed, ["ids"] = ids };
    }

    /// <summary>The row's section; a library's profile copied into the project first, the type following the copy.</summary>
    static Section SectionFor(ICommandContext ctx, Row r)
    {
        if (r.Profile == null) return r.Section!;
        var (copy, _) = ProjectProfiles.Ensure(ctx, r.Profile, r.Library);
        return Section.Parse(ProjectProfiles.Read(copy)!.SectionJson(copy.Id));
    }

    // ------------------------------------------------------------------ writing

    public static readonly string[] CsvHeads = ["Name", "Kind", "Profile", "h (mm)", "b (mm)", "Material", "Mark", "Bearing (mm)", "Manufacturer", "Code", "Lengths (mm)", "Codes"];

    /// <summary>The project's column and beam types (<paramref name="kind"/>: column, beam, lintel or all) as a CSV this import reads back.</summary>
    public static string Csv(IModelView m, string kind = "all")
    {
        var sb = new StringBuilder();
        static string Q(string? s) => s == null ? "" : s.Contains(';') || s.Contains('"') || s.Contains('\n') ? $"\"{s.Replace("\"", "\"\"")}\"" : s;
        static string Mm(double v) => Math.Round(v * 1000, 3).ToString(CultureInfo.InvariantCulture);
        sb.AppendLine(string.Join(';', CsvHeads));
        foreach (var t in m.OfClass(IfcClass.ColumnType).Concat(m.OfClass(IfcClass.BeamType)))
        {
            var k = t.Class == IfcClass.ColumnType ? "column" : t.PredefinedType == LintelTypes.Lintel ? "lintel" : "beam";
            if (kind != "all" && kind != k && !(kind == "beam" && k == "lintel")) continue;
            if (Section.From(t.Attrs["section"]) is not { } s) continue;
            var profile = s.Kind == "circle" ? "circle" : s.Kind == "rectangle" ? "" : s.Name ?? (s.ProfileId != null ? m.Get(s.ProfileId)?.Name : null) ?? s.Label;
            var lengths = (t.Attrs[LintelTypes.LengthsKey] as JsonArray ?? []).Select(Json.Number).OfType<double>().ToList();
            var codes = (t.Attrs[LintelTypes.CodesKey] as JsonArray ?? []).Select(c => (string?)c ?? "").ToList();
            sb.AppendLine(string.Join(';', new[]
            {
                Q(t.Name), k, Q(profile), Mm(s.Depth), Mm(s.Width), Q(t.Str("material") is { } mat ? m.Get(mat)?.Name : null), Q(t.Str(LintelTypes.MarkKey)),
                t.NumOrNull(LintelTypes.BearingKey) is { } b ? Mm(b) : "", Q(t.Str(LintelTypes.ManufacturerKey)), Q(t.Str(LintelTypes.CodeKey)),
                string.Join('|', lengths.Select(Mm)), Q(codes.Any(c => c.Length > 0) ? string.Join('|', codes) : null),
            }));
        }
        return sb.ToString();
    }

    // ------------------------------------------------------------------ the protocol

    static byte[] Bytes(JsonObject a) =>
        (string?)a["data"] is { Length: > 0 } d ? Convert.FromBase64String(d)
        : (string?)a["text"] is { Length: > 0 } t ? Encoding.UTF8.GetBytes(t)
        : throw new CommandException("No file was sent.");

    /// <summary>The table and its rows as the arguments say: {data (base64) | text, header?, map? (field → {index, unit}), kind?, merge?, libraries?}.</summary>
    static (List<List<string>> Table, Guessed Guessed, int Header, Dictionary<string, ProfileTables.Column?> Map, List<Row> Rows) Rows(IModelView m, JsonObject a)
    {
        var table = Tables.Read(Bytes(a));
        var g = Guess(table);
        var header = Json.Number(a["header"]) is { } h && h >= 0 ? (int)h : Math.Max(0, g.Header);
        var map = new Dictionary<string, ProfileTables.Column?>();
        foreach (var f in Fields)
        {
            if (a["map"] is not JsonObject given) { map[f] = g.Map[f]; continue; }
            if (given[f] is JsonObject x && Json.Number(x["index"]) is { } i && i >= 0)
            {
                var unit = (string?)x["unit"] ?? "";
                var factor = Lengths.Contains(f) ? ProfileTables.Units["length"].FirstOrDefault(u => u.Unit == unit) is { Unit: not null } found ? found.Factor : 1e-3 : 1;
                map[f] = new ProfileTables.Column((int)i, unit, factor);
            }
            else map[f] = null;
        }
        var kind = (string?)a["kind"] is "column" or "lintel" ? (string)a["kind"]! : "beam";
        var merge = a["merge"] is JsonValue mv && mv.TryGetValue<bool>(out var mb) && mb;
        return (table, g, header, map, Read(m, table, header, map, g.UnitColumns, kind, merge, a["libraries"] as JsonArray));
    }

    public static void Register(IModuleRegistry r)
    {
        // What a table would make: its headings, the columns guessed (or given), each row's type and what differs from the project's.
        r.Query("structure.previewTypes", (q, a) =>
        {
            var (table, g, header, map, rows) = Rows(q.Model, a);
            var o = Preview(q.Model, rows);
            o["header"] = header;
            o["headings"] = new JsonArray((header < table.Count ? table[header] : []).Select(x => (JsonNode)x).ToArray());
            o["map"] = new JsonObject(map.Select(k => KeyValuePair.Create(k.Key, (JsonNode?)(k.Value == null ? null : new JsonObject { ["index"] = k.Value.Index, ["unit"] = k.Value.Unit }))));
            o["fields"] = new JsonArray(Fields.Select(f => (JsonNode)new JsonObject { ["key"] = f, ["dimension"] = Lengths.Contains(f) ? "length" : "text" }).ToArray());
            o["units"] = new JsonArray(ProfileTables.Units["length"].Select(u => (JsonNode)u.Unit).ToArray());
            // Rows whose names differ by their length only, and so could be one type.
            o["mergeable"] = Read(q.Model, table, header, map, g.UnitColumns, (string?)a["kind"] ?? "beam", true, a["libraries"] as JsonArray).Count
                < Read(q.Model, table, header, map, g.UnitColumns, (string?)a["kind"] ?? "beam", false, a["libraries"] as JsonArray).Count;
            return o;
        });

        // The table's types made (the same arguments, and the choice for each that differs by its name: update, both or skip): one step to undo.
        r.Command("importTypes", "Import structure types", (ctx, a) =>
        {
            var (_, _, _, _, rows) = Rows(ctx.Model, a);
            return Import(ctx, rows, LibraryMerge.Choices(a["choices"], LibraryMerge.TypeChoices));
        });

        // {kind?}: the column and beam types as a CSV the import reads back.
        r.Query("structure.typesCsv", (q, a) => Csv(q.Model, (string?)a["kind"] ?? "all"));
    }
}
