using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Lapin.Drawing.Export;
using Lapin.Model;

namespace Lapin.Platform.ProfileLibraries;

/// <summary>
/// Profile tables as CSV or XLSX (docs/plan-profile-libraries.md): the AISC Shapes Database, a producer's catalogue, an EN
/// 10365 table, a firm's own. One row a profile; its kind on the row (a Type or Shape column, or the name's family: IPE,
/// HEA, W, HSS…) or for the whole file. Each column's meaning and unit are guessed from its heading (English, Romanian,
/// German, French) and confirmed in a dialog, as the price lists' are (decision 306).
/// </summary>
public static class ProfileTables
{
    /// <summary>What a column can be, in the order the matching dialog offers them.</summary>
    public static readonly string[] Fields =
    [
        "name", "family", "kind", "h", "b", "tw", "tf", "t", "r1", "r2", "lip", "b2", "tf2", "slope",
        "A", "Iy", "Iz", "Wely", "Welz", "Wply", "Wplz", "iy", "iz", "It", "Iw", "Iu", "Iv", "mass", "paint", "standard", "material",
    ];

    /// <summary>The kind of quantity each field measures: its units are of that dimension.</summary>
    public static string DimensionOf(string field) => field switch
    {
        "h" or "b" or "tw" or "tf" or "t" or "r1" or "r2" or "lip" or "b2" or "tf2" or "iy" or "iz" => "length",
        "A" => "area",
        "Iy" or "Iz" or "It" or "Iu" or "Iv" => "inertia",
        "Wely" or "Welz" or "Wply" or "Wplz" => "modulus",
        "Iw" => "warping",
        "mass" => "mass",
        "paint" => "paint",
        "slope" => "slope",
        _ => "text",
    };

    /// <summary>The units each dimension can be read in: name → metres to the dimension's power (kg/m for a mass, m²/m for paint).</summary>
    public static readonly IReadOnlyDictionary<string, (string Unit, double Factor)[]> Units = new Dictionary<string, (string, double)[]>
    {
        ["length"] = [("mm", 1e-3), ("cm", 1e-2), ("m", 1), ("in", 0.0254), ("ft", 0.3048)],
        ["area"] = [("cm²", 1e-4), ("mm²", 1e-6), ("m²", 1), ("in²", 0.0254 * 0.0254)],
        ["inertia"] = [("cm⁴", 1e-8), ("mm⁴", 1e-12), ("m⁴", 1), ("in⁴", Math.Pow(0.0254, 4)), ("×10⁴ mm⁴", 1e-8), ("×10⁶ mm⁴", 1e-6)],
        ["modulus"] = [("cm³", 1e-6), ("mm³", 1e-9), ("m³", 1), ("in³", Math.Pow(0.0254, 3)), ("×10³ mm³", 1e-6)],
        ["warping"] = [("cm⁶", 1e-12), ("×10³ cm⁶", 1e-9), ("dm⁶", 1e-6), ("mm⁶", 1e-18), ("×10⁹ mm⁶", 1e-9), ("in⁶", Math.Pow(0.0254, 6)), ("m⁶", 1)],
        ["mass"] = [("kg/m", 1), ("lb/ft", 0.45359237 / 0.3048)],
        ["paint"] = [("m²/m", 1), ("ft²/ft", 0.3048)],
        ["slope"] = [("%", 0.01), ("ratio", 1), ("°", double.NaN)],
        ["text"] = [("", 1)],
    };

    /// <summary>The headings each field is known by (folded: lower case, no diacritics, dots, commas or spaces).</summary>
    static readonly Dictionary<string, string[]> Heads = new()
    {
        ["name"] = ["name", "designation", "section", "profile", "profil", "profile name", "shape", "size", "label", "aiscmanuallabel", "edistdname", "denumire", "denumirea", "bezeichnung", "profilbezeichnung", "designation", "profile", "nom", "dimensions", "item"],
        ["family"] = ["family", "series", "serie", "familie", "famille", "reihe", "gama"],
        ["kind"] = ["kind", "type", "shape type", "shapetype", "tip", "forma", "typ", "form", "art"],
        ["h"] = ["h", "d", "depth", "height", "ht", "od", "diameter", "outsidediameter", "hauteur", "inaltime", "inaltimea", "hohe", "profilhohe", "durchmesser", "diametre", "diametru"],
        ["b"] = ["b", "bf", "width", "flangewidth", "breite", "largeur", "latime", "latimea", "flanschbreite", "largeuraile", "latimetalpa", "bt"],
        ["tw"] = ["tw", "s", "web", "webthickness", "stegdicke", "steg", "ame", "epaisseurame", "inima", "grosimeinima", "ta"],
        ["tf"] = ["tf", "flange", "flangethickness", "flanschdicke", "flansch", "aile", "epaisseuraile", "talpa", "grosimetalpa"],
        ["t"] = ["t", "thickness", "tdes", "tnom", "wall", "wallthickness", "dicke", "wanddicke", "epaisseur", "grosime", "grosimeperete"],
        ["r1"] = ["r", "r1", "rootradius", "radius", "filletradius", "ri", "ra", "rayon", "raza", "ausrundungsradius", "rs"],
        ["r2"] = ["r2", "toeradius", "edgeradius"],
        ["lip"] = ["c", "lip", "lips", "lipwidth", "girth", "buza", "bordure", "lippe"],
        ["b2"] = ["b2", "bf2", "bottomflangewidth", "bfb"],
        ["tf2"] = ["tf2", "bottomflangethickness", "tfb"],
        ["slope"] = ["slope", "flangeslope", "neigung", "pente", "panta"],
        ["A"] = ["a", "area", "aria", "flache", "querschnittsflache", "querschnitt", "aire", "section", "sectiune"],
        ["Iy"] = ["iy", "ix", "ixx", "iyy", "momentofinertiay", "momentofinertiax", "i y"],
        ["Iz"] = ["iz", "izz", "momentofinertiaz"],
        ["Wely"] = ["wely", "wy", "wel y", "wx", "sx", "welx", "elasticmodulusy", "elasticsectionmodulusy"],
        ["Welz"] = ["welz", "wz", "sz", "elasticmodulusz", "elasticsectionmodulusz"],
        ["Wply"] = ["wply", "wpl y", "zx", "wplx", "plasticmodulusy", "plasticsectionmodulusy", "spy"],
        ["Wplz"] = ["wplz", "zz", "plasticmodulusz", "plasticsectionmodulusz", "spz"],
        ["iy"] = ["iy radius", "radiusofgyrationy", "rx", "rgy", "giy"],
        ["iz"] = ["iz radius", "radiusofgyrationz", "rz", "rgz", "giz"],
        ["It"] = ["it", "j", "torsionconstant", "torsionalconstant", "torsion", "ik", "momentdinertietorsion", "constantadetorsiune"],
        ["Iw"] = ["iw", "cw", "iω", "warpingconstant", "warping", "wolbwiderstand", "constantedegauchissement", "constantadedeplanare"],
        ["Iu"] = ["iu", "imax"],
        ["Iv"] = ["iv", "imin"],
        ["mass"] = ["mass", "weight", "g", "w", "m", "masse", "gewicht", "poids", "p", "masa", "greutate", "kg/m", "massperlength", "nominalweight", "weightperlength", "massepermeter"],
        ["paint"] = ["al", "au", "paint", "paintarea", "surface", "mantelflache", "oberflache", "surfacepeinture", "suprafata", "suprafatadevopsit", "perimeter"],
        ["standard"] = ["standard", "norm", "norme", "standardul", "norma"],
        ["material"] = ["material", "materiau", "werkstoff", "grade"],
    };

    /// <summary>Lower case, no diacritics, no dots, commas, underscores, hyphens or spaces: how headings are compared.</summary>
    public static string Fold(string s)
    {
        var sb = new StringBuilder();
        foreach (var ch in s.Trim().Trim('"').ToLowerInvariant())
            sb.Append(ch switch
            {
                'ă' or 'â' or 'á' or 'à' or 'ä' => "a", 'î' or 'í' or 'ï' => "i", 'ș' or 'ş' => "s", 'ț' or 'ţ' => "t", 'é' or 'è' or 'ê' or 'ë' => "e",
                'ö' or 'ô' or 'ó' => "o", 'ü' or 'û' or 'ú' => "u", 'ç' => "c", 'ß' => "ss",
                '.' or ',' or '_' or '-' or ' ' or ':' or ' ' => "",
                _ => ch.ToString(),
            });
        return sb.ToString();
    }

    /// <summary>A heading split into its name and its unit's text (in brackets, after a comma or a space): „Iy [cm4]”, „A (cm²)”, „Iw x10³ cm⁶”.</summary>
    public static (string Name, string Unit) SplitHeading(string heading)
    {
        var h = heading.Trim();
        var at = h.IndexOfAny(['(', '[', '{']);
        if (at > 0) return (h[..at].Trim(), h[at..].Trim('(', ')', '[', ']', '{', '}', ' '));
        // „Iy cm4”, „Wpl,y cm3”: a last word that is a unit.
        var parts = h.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2 && UnitOf(string.Join(' ', parts.Skip(1)), null) != null) return (parts[0], string.Join(' ', parts.Skip(1)));
        return (h, "");
    }

    /// <summary>The unit a heading's unit text names for a dimension (its factor), or null when it names none (or another dimension's).</summary>
    public static (string Unit, double Factor)? UnitOf(string text, string? dimension)
    {
        var t = text.Trim().ToLowerInvariant().Replace(" ", "").Replace("^", "").Replace("²", "2").Replace("³", "3").Replace("⁴", "4").Replace("⁶", "6")
            .Replace("⁻", "-").Replace("×", "x").Replace("·", "").Replace("*", "x");
        if (t.Length == 0) return null;
        // A multiplier: „x10³ cm⁶”, „10^3 mm4”, „x10-3 dm6”.
        var factor = 1.0;
        var m = System.Text.RegularExpressions.Regex.Match(t, @"^x?10(-?\d+)");
        if (m.Success) { factor = Math.Pow(10, int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)); t = t[m.Length..]; }
        (string, double, string)? u = t switch
        {
            "mm" => ("mm", 1e-3, "length"), "cm" => ("cm", 1e-2, "length"), "m" => ("m", 1, "length"), "in" or "inch" or "inches" or "\"" => ("in", 0.0254, "length"), "ft" => ("ft", 0.3048, "length"),
            "mm2" => ("mm²", 1e-6, "area"), "cm2" => ("cm²", 1e-4, "area"), "m2" => ("m²", 1, "area"), "in2" => ("in²", 0.0254 * 0.0254, "area"),
            "mm4" => ("mm⁴", 1e-12, "inertia"), "cm4" => ("cm⁴", 1e-8, "inertia"), "m4" => ("m⁴", 1, "inertia"), "in4" => ("in⁴", Math.Pow(0.0254, 4), "inertia"),
            "mm3" => ("mm³", 1e-9, "modulus"), "cm3" => ("cm³", 1e-6, "modulus"), "m3" => ("m³", 1, "modulus"), "in3" => ("in³", Math.Pow(0.0254, 3), "modulus"),
            "mm6" => ("mm⁶", 1e-18, "warping"), "cm6" => ("cm⁶", 1e-12, "warping"), "dm6" => ("dm⁶", 1e-6, "warping"), "m6" => ("m⁶", 1, "warping"), "in6" => ("in⁶", Math.Pow(0.0254, 6), "warping"),
            "kg/m" or "kgm" or "kg/ml" => ("kg/m", 1, "mass"), "lb/ft" or "plf" or "lbs/ft" => ("lb/ft", 0.45359237 / 0.3048, "mass"),
            "m2/m" => ("m²/m", 1, "paint"), "ft2/ft" => ("ft²/ft", 0.3048, "paint"),
            "%" => ("%", 0.01, "slope"),
            _ => null,
        };
        if (u is not { } x) return null;
        // An area's unit at a modulus's place (cm3 at Iy) is the user's to fix; a dimension asked must match.
        if (dimension != null && x.Item3 != dimension && !(dimension == "paint" && x.Item3 == "length")) return null;
        var label = factor == 1 ? x.Item1 : $"×{Pow(factor)} {x.Item1}";
        return (label, x.Item2 * factor);
    }

    static string Pow(double f) => "10" + string.Concat(((int)Math.Round(Math.Log10(f))).ToString(CultureInfo.InvariantCulture).Select(c => c switch
    { '-' => '⁻', '0' => '⁰', '1' => '¹', '2' => '²', '3' => '³', '4' => '⁴', '5' => '⁵', '6' => '⁶', '7' => '⁷', '8' => '⁸', '9' => '⁹', _ => c }));

    /// <summary>The column a field is read from and in what unit (its factor to SI).</summary>
    public sealed record Column(int Index, string Unit, double Factor)
    {
        /// <summary>Other columns read when this one is empty on a row (an American table's d, Ht and OD: the depth of a W, a rectangular HSS and a round one).</summary>
        public IReadOnlyList<int> Alternates { get; init; } = [];
    }

    public sealed record Guessed(int Header, Dictionary<string, Column?> Map, string Convention, string System, string Layout);

    /// <summary>
    /// Where the headings are and which column is which: the first of twenty rows naming a profile and one dimension or
    /// value; its axis convention (EN: Iy and Iz; American: Ix and Iy, x the strong axis), its unit system (metric, or
    /// imperial from an inch or lb/ft heading or the American convention) and each field's unit from its heading, else
    /// the system's usual one. <see cref="Guessed.Layout"/> names the columns' order, for a remembered choice.
    /// </summary>
    public static Guessed Guess(List<List<string>> table)
    {
        for (var i = 0; i < Math.Min(20, table.Count); i++)
        {
            var row = table[i];
            var heads = row.Select(c => SplitHeading(c ?? "")).ToList();
            var folded = heads.Select(h => Fold(h.Name)).ToList();
            var foldedFull = row.Select(c => Fold(c ?? "")).ToList();
            // American: an Ix and no Iz (x strong, y weak).
            var american = folded.Contains("ix") && !folded.Contains("iz");
            var imperial = american || heads.Any(h => UnitOf(h.Unit, null) is { } u && (u.Unit.Contains("in") || u.Unit.Contains("lb") || u.Unit.Contains("ft")));
            var map = new Dictionary<string, Column?>();
            var used = new HashSet<int>();
            void Take(string field, int at)
            {
                var dim = DimensionOf(field);
                var unit = UnitOf(heads[at].Unit, dim == "text" ? null : dim) ?? DefaultUnit(dim, imperial);
                map[field] = new Column(at, unit.Unit, unit.Factor);
                used.Add(at);
            }
            // The convention's own names first: in an American table Iy is the weak axis, rx and ry radii of gyration.
            var renames = american
                ? new Dictionary<string, string> { ["ix"] = "Iy", ["iy"] = "Iz", ["sx"] = "Wely", ["sy"] = "Welz", ["zx"] = "Wply", ["zy"] = "Wplz", ["rx"] = "iy", ["ry"] = "iz", ["j"] = "It", ["cw"] = "Iw", ["w"] = "mass", ["tdes"] = "t", ["ht"] = "h", ["od"] = "h", ["bf"] = "b", ["b"] = "b", ["d"] = "h", ["type"] = "kind", ["aiscmanuallabel"] = "name", ["edistdname"] = "name" }
                : new Dictionary<string, string> { ["iy"] = "Iy", ["iz"] = "Iz", ["wely"] = "Wely", ["welz"] = "Welz", ["wply"] = "Wply", ["wplz"] = "Wplz", ["it"] = "It", ["iw"] = "Iw", ["iu"] = "Iu", ["iv"] = "Iv" };
            for (var at = 0; at < folded.Count; at++)
            {
                if (!renames.TryGetValue(folded[at], out var f)) continue;
                // An EN table writes an inertia Iy and a radius of gyration iy: the case tells them apart.
                var cased = new string(heads[at].Name.Where(char.IsLetterOrDigit).ToArray());
                if (!american && cased is "iy" or "iz" && row.Any(c => new string(SplitHeading(c ?? "").Name.Where(char.IsLetterOrDigit).ToArray()) == "I" + cased[1..])) f = cased;
                if (!american && f is "Iy" or "Iz" && map.ContainsKey(f) && !map.ContainsKey(f.ToLowerInvariant())) f = f.ToLowerInvariant();
                if (map.GetValueOrDefault(f) is { } taken) { map[f] = taken with { Alternates = [.. taken.Alternates, at] }; used.Add(at); }
                else Take(f, at);
            }
            // Radii of gyration written small and big alike (iy, Iy): a second column of the same name after the inertia is the radius.
            foreach (var exact in new[] { true, false })
                foreach (var f in Fields)
                {
                    if (map.ContainsKey(f)) continue;
                    for (var at = 0; at < folded.Count; at++)
                    {
                        if (used.Contains(at) || folded[at].Length == 0) continue;
                        var h = folded[at];
                        if (!Heads[f].Any(k => exact ? h == k || foldedFull[at] == k : h.StartsWith(k, StringComparison.Ordinal) && k.Length >= 3)) continue;
                        Take(f, at);
                        break;
                    }
                }
            foreach (var f in Fields) map.TryAdd(f, null);
            var hasName = map["name"] != null;
            var dims = new[] { "h", "b", "t", "tw", "A", "Iy" }.Count(f => map[f] != null);
            if (hasName && dims >= 1)
                return new(i, map, american ? "american" : "en", imperial ? "imperial" : "metric", string.Join('|', foldedFull));
        }
        return new(-1, Fields.ToDictionary(f => f, _ => (Column?)null), "en", "metric", "");
    }

    /// <summary>A dimension's usual unit in a table of that system: mm and cm², cm⁴, cm³, cm⁶ (EN) or in, in², in⁴, in³, in⁶, lb/ft.</summary>
    public static (string Unit, double Factor) DefaultUnit(string dimension, bool imperial) => dimension switch
    {
        "length" => imperial ? ("in", 0.0254) : ("mm", 1e-3),
        "area" => imperial ? ("in²", 0.0254 * 0.0254) : ("cm²", 1e-4),
        "inertia" => imperial ? ("in⁴", Math.Pow(0.0254, 4)) : ("cm⁴", 1e-8),
        "modulus" => imperial ? ("in³", Math.Pow(0.0254, 3)) : ("cm³", 1e-6),
        "warping" => imperial ? ("in⁶", Math.Pow(0.0254, 6)) : ("cm⁶", 1e-12),
        "mass" => imperial ? ("lb/ft", 0.45359237 / 0.3048) : ("kg/m", 1),
        "paint" => imperial ? ("ft²/ft", 0.3048) : ("m²/m", 1),
        "slope" => ("%", 0.01),
        _ => ("", 1),
    };

    /// <summary>
    /// A kind from what a table writes (a Type column, a family, a name's start): W, M, S, HP, IPE, HE… I; C, MC, UPN, UPE,
    /// PFC U; L; WT, MT, ST T; HSS and PIPE hollow (rectangular when a width is given, else round); Z; Σ; null when none.
    /// </summary>
    public static string? KindFrom(string? text, bool hasWidth = true, bool hasLip = false)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (SectionProfile.KindOf(text) is { } k && k != "outline") return k;
        var t = text.Trim().ToUpperInvariant();
        var head = new string(t.TakeWhile(char.IsLetter).ToArray());
        return head switch
        {
            "W" or "M" or "S" or "HP" or "IPE" or "IPN" or "HE" or "HEA" or "HEB" or "HEM" or "HD" or "HL" or "HP" or "UB" or "UC" or "IPEA" or "IPEO" or "HEAA" or "UKB" or "UKC" or "WB" or "WC" or "UBP" => "I",
            "C" when hasLip => "C",
            "C" or "MC" or "UPN" or "UPE" or "UAP" or "PFC" or "U" or "CH" => "U",
            "L" or "LU" or "LE" or "UA" or "EA" or "LL" => "L",
            "WT" or "MT" or "ST" or "T" or "TEE" => "T",
            "HSS" => hasWidth ? "RHS" : "CHS",
            "SHS" or "RHS" or "RHSC" or "SHSC" or "RHFB" or "QRO" or "RRO" or "MSH" => "RHS",
            "PIPE" or "CHS" or "RO" or "CHSC" or "TUBE" => "CHS",
            "Z" => "Z",
            "SIGMA" or "Σ" => "SIGMA",
            "RECT" or "FLAT" or "FL" or "PL" or "BAR" => "rectangle",
            "ROUND" or "RD" => "circle",
            _ => null,
        };
    }

    public sealed record Imported(List<SectionProfile> Profiles, int Skipped, List<string> Notes, List<ProfileCheck> Checks);

    /// <summary>
    /// The rows of a table under the columns given (<paramref name="map"/>: field → column and unit), the headings in row
    /// <paramref name="header"/>. Each row needs a name, a kind (its own, or <paramref name="kind"/> for the file) and the
    /// dimensions its kind is built of; the values the file gives are kept, checked against Lapin's.
    /// </summary>
    public static Imported Import(List<List<string>> table, int header, IReadOnlyDictionary<string, Column?> map, string? kind, string? family, string? standard, string material, string? source)
    {
        if (map.GetValueOrDefault("name") is null) throw new CommandException("Say which column holds the profile's name.");
        string Cell(List<string> r, string f)
        {
            if (map.GetValueOrDefault(f) is not { } c) return "";
            foreach (var i in c.Alternates.Prepend(c.Index))
                if (i >= 0 && i < r.Count && r[i].Trim() is { Length: > 0 } v && v is not ("–" or "-" or "—")) return v;
            return "";
        }
        double? Num(List<string> r, string f)
        {
            if (Cell(r, f) is not { Length: > 0 } c || XlsxReader.Number(c) is not { } v || !double.IsFinite(v)) return null;
            var col = map[f]!;
            if (f == "slope" && double.IsNaN(col.Factor)) return Math.Tan(v * Math.PI / 180);
            return v * col.Factor;
        }
        var o = new List<SectionProfile>();
        var notes = new List<string>();
        var checks = new List<ProfileCheck>();
        var skipped = 0;
        var noKind = 0;
        var problems = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in table.Skip(header + 1))
        {
            if (r.All(string.IsNullOrWhiteSpace)) continue;
            var name = Cell(r, "name");
            if (name.Length == 0) { skipped++; continue; }
            double H = Num(r, "h") ?? 0, B = Num(r, "b") ?? 0, t = Num(r, "t") ?? 0;
            double tw = Num(r, "tw") ?? 0, tf = Num(r, "tf") ?? 0, lip = Num(r, "lip") ?? 0;
            var k = SectionProfile.KindOf(Cell(r, "kind")) is { } own && own != "outline" ? own
                : KindFrom(Cell(r, "kind"), B > 0, lip > 0) ?? KindFrom(Cell(r, "family"), B > 0, lip > 0) ?? KindFrom(name, B > 0, lip > 0) ?? kind;
            if (k == null) { noKind++; skipped++; continue; }
            // One thickness for the thin-walled kinds and angles; an I's or a channel's flange from it when the table has no tf (DIN: s and t).
            if (k is "L" or "RHS" or "CHS" or "C" or "Z" or "SIGMA") { tw = t > 0 ? t : tw > 0 ? tw : tf; tf = tw; }
            else if (tf <= 0 && t > 0) tf = t;
            if (k is "CHS" or "circle") { if (H <= 0) H = B; B = H; }
            // An angle given by one leg (L 100×10): equal.
            if (k == "L" && B <= 0) B = H;
            if (k == "L" && H <= 0) H = B;
            var given = new Dictionary<string, double>();
            foreach (var f in new[] { "A", "Iy", "Iz", "Wely", "Welz", "Wply", "Wplz", "iy", "iz", "It", "Iw", "Iu", "Iv", "mass", "paint" })
                if (Num(r, f) is { } v && v != 0) given[f] = Math.Abs(v);
            var mat = Cell(r, "material") is { Length: > 0 } rm && MaterialOf(rm) is { } known ? known : material;
            var p = new SectionProfile
            {
                Name = name, Kind = k, H = H, B = B, Tw = tw, Tf = tf, R1 = Num(r, "r1") ?? 0, R2 = Num(r, "r2") ?? 0, Lip = lip, Slope = Num(r, "slope") ?? 0,
                B2 = Num(r, "b2") ?? 0, Tf2 = Num(r, "tf2") ?? 0,
                Family = Cell(r, "family") is { Length: > 0 } fam ? fam : family ?? FamilyOf(name), Standard = Cell(r, "standard") is { Length: > 0 } st ? st : standard,
                Material = mat, Source = source, Given = given,
            };
            if (p.Kind == "I" && p.B2 > 0 && Math.Abs(p.B2 - p.B) > 1e-6) p = p with { Kind = "asymI" };
            if (p.Problem() is { } why) { problems.Add(why); skipped++; continue; }
            if (!seen.Add(p.Name)) { skipped++; continue; }
            o.Add(p);
            checks.AddRange(ProfileProps.Checks(p));
        }
        if (o.Count == 0) throw new CommandException(noKind > 0 ? "No row says what kind of profile it is: choose the kind for the whole file." : "No profile could be read from the table: each row needs a name and its dimensions.");
        if (noKind > 0) notes.Add($"{noKind} row(s) do not say their kind: choose one for the file to take them.");
        if (problems.Count > 0) notes.Add($"{problems.Count} row(s) left out: {string.Join(" ", problems.Take(3))}");
        // A whole column off by a power of ten: its unit is probably another.
        foreach (var g in checks.GroupBy(c => c.Key))
            if (g.Count() >= Math.Max(2, o.Count / 2) && g.Select(c => ProfileProps.UnitGuess(c.Key, c.Given, c.Computed)).Distinct().ToList() is [{ } guess])
                notes.Add($"The column {g.Key} reads {guess} Lapin's values: check its unit.");
        return new(o, skipped, notes, checks);
    }

    /// <summary>A family from a designation's letters (IPE 300 → IPE, W14X90 → W, HSS6X6X1/2 → HSS).</summary>
    public static string? FamilyOf(string name)
    {
        var head = new string(name.Trim().TakeWhile(c => char.IsLetter(c) || c == 'Σ').ToArray());
        return head.Length > 0 ? head.ToUpperInvariant() : null;
    }

    /// <summary>A material from what a table writes: steel (S235, S355, A992…), aluminium, timber (GL24h, C24), concrete, PVC; null otherwise.</summary>
    public static string? MaterialOf(string text)
    {
        var t = Fold(text);
        if (t.Length == 0) return null;
        if (t.StartsWith("alu") || t.StartsWith("en aw") || t.StartsWith("6060") || t.StartsWith("6063") || t.StartsWith("6082")) return "aluminium";
        if (t.StartsWith("stainless") || t.StartsWith("inox") || t.StartsWith("1.4")) return "stainless";
        if (t.StartsWith("gl") && t.Length <= 6 || t.StartsWith("timber") || t.StartsWith("wood") || t.StartsWith("lemn") || t.StartsWith("holz") || t.StartsWith("bois") || t is ['c', _, _] && char.IsDigit(t[1])) return "timber";
        if (t.StartsWith("concrete") || t.StartsWith("beton") || t.StartsWith("c20") || t.StartsWith("c25") || t.StartsWith("c30")) return "concrete";
        if (t.StartsWith("pvc")) return "pvc";
        if (t.StartsWith("steel") || t.StartsWith("otel") || t.StartsWith("stahl") || t.StartsWith("acier") || t.StartsWith("s2") || t.StartsWith("s3") || t.StartsWith("s4") || t.StartsWith("a36") || t.StartsWith("a99") || t.StartsWith("a5")) return "steel";
        return null;
    }

    public static JsonObject ColumnJson(Column? c) => c == null ? new JsonObject() : new JsonObject
    {
        ["index"] = c.Index, ["unit"] = c.Unit, ["factor"] = c.Factor, ["alternates"] = new JsonArray(c.Alternates.Select(i => (JsonNode)i).ToArray()),
    };

    /// <summary>The map the dialog sends back: field → {index, unit} (the unit by its name in <see cref="Units"/>); a field not sent is not read.</summary>
    public static Dictionary<string, Column?> MapFrom(JsonNode? n, Guessed? guessed)
    {
        var o = new Dictionary<string, Column?>();
        foreach (var f in Fields)
        {
            Column? c = null;
            if (n is JsonObject m && m[f] is JsonObject x && Json.Number(x["index"]) is { } i && i >= 0)
            {
                var dim = DimensionOf(f);
                var unit = (string?)x["unit"];
                var factor = Units.GetValueOrDefault(dim)?.FirstOrDefault(u => u.Unit == unit) is { Unit: { } } found && found.Unit != null ? found.Factor
                    : Json.Number(x["factor"]) ?? guessed?.Map.GetValueOrDefault(f)?.Factor ?? DefaultUnit(dim, false).Factor;
                c = new Column((int)i, unit ?? "", factor) { Alternates = (x["alternates"] as JsonArray)?.Select(a => (int)(Json.Number(a) ?? -1)).Where(a => a >= 0).ToList() ?? [] };
            }
            else if (n == null) c = guessed?.Map.GetValueOrDefault(f);
            o[f] = c;
        }
        return o;
    }

    // ------------------------------------------------------------------ writing

    /// <summary>Profiles as a CSV Lapin reads back (metric, EN convention, mm and cm²/cm⁴/cm³/cm⁶; the file's values where it gave them, else Lapin's).</summary>
    public static string Csv(IEnumerable<SectionProfile> profiles)
    {
        var heads = new[] { "Name", "Family", "Kind", "Standard", "Material", "h (mm)", "b (mm)", "tw (mm)", "tf (mm)", "r1 (mm)", "r2 (mm)", "lip (mm)", "b2 (mm)", "tf2 (mm)", "slope (%)",
            "A (cm2)", "Iy (cm4)", "Iz (cm4)", "Wel.y (cm3)", "Wel.z (cm3)", "Wpl.y (cm3)", "Wpl.z (cm3)", "It (cm4)", "Iw (cm6)", "mass (kg/m)", "paint (m2/m)" };
        var sb = new StringBuilder();
        static string Q(string? s) => s == null ? "" : s.Contains(';') || s.Contains('"') ? $"\"{s.Replace("\"", "\"\"")}\"" : s;
        static string N(double v, double f) => v == 0 ? "" : Math.Round(v / f, 6).ToString(CultureInfo.InvariantCulture);
        sb.AppendLine(string.Join(';', heads));
        foreach (var p in profiles)
        {
            var c = ProfileProps.Compute(p).Values;
            double V(string k) => p.Given.TryGetValue(k, out var g) ? g : c.GetValueOrDefault(k);
            sb.AppendLine(string.Join(';', new[]
            {
                Q(p.Name), Q(p.Family), p.Kind, Q(p.Standard), p.Material, N(p.H, 1e-3), N(p.B, 1e-3), N(p.Tw, 1e-3), N(p.Tf, 1e-3), N(p.R1, 1e-3), N(p.R2, 1e-3), N(p.Lip, 1e-3),
                N(p.B2, 1e-3), N(p.Tf2, 1e-3), N(p.Slope, 0.01),
                N(V("A"), 1e-4), N(V("Iy"), 1e-8), N(V("Iz"), 1e-8), N(V("Wely"), 1e-6), N(V("Welz"), 1e-6), N(V("Wply"), 1e-6), N(V("Wplz"), 1e-6), N(V("It"), 1e-8), N(V("Iw"), 1e-12),
                N(V("mass"), 1), N(V("paint"), 1),
            }));
        }
        return sb.ToString();
    }
}
