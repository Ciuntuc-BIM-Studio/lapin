using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace Lapin.Ids;

/// <summary>An .ids file that is not IDS 1.0: every problem found, each with its line.</summary>
public sealed class IdsFormatException(IReadOnlyList<string> problems)
    : FormatException(problems.Count == 1 ? problems[0] : $"{problems.Count} problems: {string.Join(" ", problems.Take(8))}{(problems.Count > 8 ? " …" : "")}")
{
    public IReadOnlyList<string> Problems { get; } = problems;
}

/// <summary>
/// Reads an .ids file as buildingSMART's IDS 1.0 defines it: ids.xsd 1.0.0 (the elements, their order, the attributes and their
/// values), and the user manual's rules the schema cannot say (the facet configurations it allows; a value written as its
/// type's lexical form). No DTD, no external entity, no schema fetched: the rules are written here, and a file that breaks
/// one is refused with every problem and its line.
/// </summary>
public static class IdsReader
{
    public const string Ns = "http://standards.buildingsmart.org/IDS";
    public const string XsNs = "http://www.w3.org/2001/XMLSchema";
    public static readonly string[] IfcVersions = ["IFC2X3", "IFC4", "IFC4X3_ADD2"];
    public static readonly string[] Relations = ["IFCRELAGGREGATES", "IFCRELASSIGNSTOGROUP", "IFCRELCONTAINEDINSPATIALSTRUCTURE", "IFCRELNESTS", "IFCRELVOIDSELEMENT IFCRELFILLSELEMENT"];
    /// <summary>The largest .ids read (characters): an IDS is a few kilobytes, the largest published a few hundred.</summary>
    public const int MaxCharacters = 16_000_000;

    static readonly XNamespace I = Ns, X = XsNs;

    public static IdsDocument Read(byte[] bytes)
    {
        using var s = new MemoryStream(bytes, writable: false);
        return Read(s);
    }

    public static IdsDocument Read(string text) => Read(System.Text.Encoding.UTF8.GetBytes(text));

    public static IdsDocument Read(Stream stream)
    {
        XDocument doc;
        try
        {
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaxCharacters, IgnoreComments = true };
            using var r = XmlReader.Create(stream, settings);
            doc = XDocument.Load(r, LoadOptions.SetLineInfo);
        }
        catch (XmlException ex) { throw new IdsFormatException([$"Not well-formed XML: {ex.Message}"]); }
        var p = new Parse();
        var result = p.Document(doc);
        if (p.Problems.Count > 0 || result == null) throw new IdsFormatException(p.Problems.Count > 0 ? p.Problems : ["The file is not an IDS."]);
        return result;
    }

    sealed class Parse
    {
        public readonly List<string> Problems = [];

        static string At(XObject x) => x is IXmlLineInfo li && li.HasLineInfo() ? $"Line {li.LineNumber}: " : "";
        void Fail(XObject x, string message) { if (Problems.Count < 50) Problems.Add(At(x) + message); }

        /// <summary>The element's children in the IDS namespace, failing on any other child (text aside).</summary>
        List<XElement> Children(XElement e, params string[] allowed)
        {
            var o = new List<XElement>();
            foreach (var c in e.Elements())
            {
                if (c.Name.Namespace != I || !allowed.Contains(c.Name.LocalName)) { Fail(c, $"<{c.Name.LocalName}> is not allowed in <{e.Name.LocalName}>{(allowed.Length > 0 ? $" (only {string.Join(", ", allowed.Select(a => $"<{a}>"))})" : "")}."); continue; }
                o.Add(c);
            }
            return o;
        }

        /// <summary>Children in the order the schema's sequence gives (each name's index never going back); a child out of order fails.</summary>
        void InOrder(XElement e, List<XElement> children, string[] order)
        {
            var last = -1;
            foreach (var c in children)
            {
                var i = Array.IndexOf(order, c.Name.LocalName);
                if (i < last) Fail(c, $"<{c.Name.LocalName}> comes before <{order[last]}> in <{e.Name.LocalName}>.");
                else last = i;
            }
        }

        void OnlyAttributes(XElement e, params string[] allowed)
        {
            foreach (var a in e.Attributes())
            {
                if (a.IsNamespaceDeclaration || a.Name.Namespace == XNamespace.Xmlns) continue;
                if (a.Name.NamespaceName == "http://www.w3.org/2001/XMLSchema-instance") continue; // xsi:schemaLocation
                if (a.Name.Namespace != XNamespace.None || !allowed.Contains(a.Name.LocalName)) Fail(a, $"The attribute {a.Name.LocalName} is not allowed on <{e.Name.LocalName}>.");
            }
        }

        public IdsDocument? Document(XDocument doc)
        {
            var root = doc.Root;
            if (root == null || root.Name != I + "ids")
            {
                Fail((XObject?)root ?? doc, root == null ? "The file is empty." : $"The root is <{root.Name.LocalName}>{(root.Name.LocalName == "ids" ? $" in the namespace „{root.Name.NamespaceName}”" : "")}, not <ids> in {Ns}.");
                return null;
            }
            OnlyAttributes(root);
            var kids = Children(root, "info", "specifications");
            InOrder(root, kids, ["info", "specifications"]);
            var infoEl = kids.FirstOrDefault(k => k.Name.LocalName == "info");
            var specsEl = kids.FirstOrDefault(k => k.Name.LocalName == "specifications");
            if (kids.Count(k => k.Name.LocalName == "info") > 1) Fail(root, "There is more than one <info>.");
            if (kids.Count(k => k.Name.LocalName == "specifications") > 1) Fail(root, "There is more than one <specifications>.");
            if (infoEl == null) Fail(root, "<ids> has no <info> (its title is required).");
            if (specsEl == null) Fail(root, "<ids> has no <specifications>.");
            var info = infoEl == null ? new IdsInfo("", null, null, null, null, null, null, null) : Info(infoEl);
            var specs = new List<IdsSpecification>();
            if (specsEl != null)
            {
                OnlyAttributes(specsEl);
                foreach (var s in Children(specsEl, "specification")) if (Specification(s) is { } spec) specs.Add(spec);
                if (!specsEl.Elements().Any()) Fail(specsEl, "<specifications> holds no <specification>.");
            }
            return new IdsDocument(info, specs);
        }

        static readonly Regex Email = new("^[^@]+@[^\\.]+\\..+$", RegexOptions.CultureInvariant);
        static readonly Regex Date = new(@"^\d{4}-\d{2}-\d{2}(Z|[+-]\d{2}:\d{2})?$", RegexOptions.CultureInvariant);

        IdsInfo Info(XElement e)
        {
            OnlyAttributes(e);
            string[] order = ["title", "copyright", "version", "description", "author", "date", "purpose", "milestone"];
            var kids = Children(e, order);
            InOrder(e, kids, order);
            foreach (var g in kids.GroupBy(k => k.Name.LocalName).Where(g => g.Count() > 1)) Fail(g.Skip(1).First(), $"<{g.Key}> is there more than once.");
            string? V(string n) => kids.FirstOrDefault(k => k.Name.LocalName == n)?.Value.Trim();
            var title = V("title");
            if (title == null) Fail(e, "<info> has no <title>.");
            if (V("author") is { } author && !Email.IsMatch(author)) Fail(kids.First(k => k.Name.LocalName == "author"), $"The author „{author}” is not an e-mail address.");
            if (V("date") is { } date && (!Date.IsMatch(date) || !DateTime.TryParseExact(date[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)))
                Fail(kids.First(k => k.Name.LocalName == "date"), $"The date „{date}” is not a date (YYYY-MM-DD).");
            return new IdsInfo(title ?? "", V("copyright"), V("version"), V("description"), V("author"), V("date"), V("purpose"), V("milestone"));
        }

        IdsSpecification? Specification(XElement e)
        {
            OnlyAttributes(e, "name", "ifcVersion", "identifier", "description", "instructions");
            var name = (string?)e.Attribute("name");
            if (name == null) Fail(e, "A <specification> needs its name.");
            var versionsText = (string?)e.Attribute("ifcVersion");
            var versions = new List<string>();
            if (versionsText == null) Fail(e, $"Specification „{name}” needs its ifcVersion ({string.Join(", ", IfcVersions)}).");
            else
            {
                foreach (var v in versionsText.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
                    if (IfcVersions.Contains(v)) { if (!versions.Contains(v)) versions.Add(v); }
                    else Fail(e, $"Specification „{name}”: „{v}” is not an IFC version of IDS 1.0 ({string.Join(", ", IfcVersions)}).");
                if (versions.Count == 0 && Problems.Count == 0) Fail(e, $"Specification „{name}” names no IFC version.");
            }
            var kids = Children(e, "applicability", "requirements");
            InOrder(e, kids, ["applicability", "requirements"]);
            var appEl = kids.FirstOrDefault(k => k.Name.LocalName == "applicability");
            var reqEl = kids.FirstOrDefault(k => k.Name.LocalName == "requirements");
            if (kids.Count(k => k.Name.LocalName == "applicability") > 1 || kids.Count(k => k.Name.LocalName == "requirements") > 1) Fail(e, $"Specification „{name}” has more than one <applicability> or <requirements>.");
            if (appEl == null) { Fail(e, $"Specification „{name}” has no <applicability>."); return null; }

            OnlyAttributes(appEl, "minOccurs", "maxOccurs");
            var min = 1;
            int? max = 1;
            if ((string?)appEl.Attribute("minOccurs") is { } minText && !int.TryParse(minText, NumberStyles.None, CultureInfo.InvariantCulture, out min)) Fail(appEl, $"minOccurs „{minText}” is not a whole number.");
            if ((string?)appEl.Attribute("maxOccurs") is { } maxText)
            {
                if (maxText == "unbounded") max = null;
                else if (int.TryParse(maxText, NumberStyles.None, CultureInfo.InvariantCulture, out var m)) max = m;
                else Fail(appEl, $"maxOccurs „{maxText}” is neither a whole number nor unbounded.");
            }
            if (max is { } mx && mx < min) Fail(appEl, $"maxOccurs {mx} is below minOccurs {min}.");

            string[] appOrder = ["entity", "partOf", "classification", "attribute", "property", "material"];
            var appKids = Children(appEl, appOrder);
            InOrder(appEl, appKids, appOrder);
            if (appKids.Count(k => k.Name.LocalName == "entity") > 1) Fail(appEl, "The applicability has more than one <entity>.");
            var applicability = appKids.Select(k => Facet(k, applicability: true)).OfType<IdsFacet>().ToList();

            var requirements = new List<IdsFacet>();
            string? reqDescription = null;
            if (reqEl != null)
            {
                OnlyAttributes(reqEl, "description");
                reqDescription = (string?)reqEl.Attribute("description");
                var reqKids = Children(reqEl, appOrder);
                requirements = reqKids.Select(k => Facet(k, applicability: false)).OfType<IdsFacet>().ToList();
                if (max == 0 && reqKids.Count > 0) Fail(reqEl, $"Specification „{name}” is prohibited (maxOccurs 0): its requirements would never be checked, so it may have none.");
            }
            return new IdsSpecification(name ?? "", versions, (string?)e.Attribute("identifier"), (string?)e.Attribute("description"), (string?)e.Attribute("instructions"),
                min, max == 0 ? 0 : max, applicability, requirements, reqDescription);
        }

        IdsCardinality Cardinality(XElement e, bool applicability, bool simpleOnly)
        {
            var text = (string?)e.Attribute("cardinality");
            if (text == null) return IdsCardinality.Required;
            if (applicability) { Fail(e, $"An applicability's <{e.Name.LocalName}> has no cardinality."); return IdsCardinality.Required; }
            switch (text)
            {
                case "required": return IdsCardinality.Required;
                case "prohibited": return IdsCardinality.Prohibited;
                case "optional" when !simpleOnly: return IdsCardinality.Optional;
                default:
                    Fail(e, $"The cardinality „{text}” is not one of {(simpleOnly ? "required, prohibited" : "required, optional, prohibited")}.");
                    return IdsCardinality.Required;
            }
        }

        IdsFacet? Facet(XElement e, bool applicability)
        {
            var reqAttrs = applicability ? Array.Empty<string>() : ["cardinality", "instructions"];
            var instructions = applicability ? null : (string?)e.Attribute("instructions");
            switch (e.Name.LocalName)
            {
                case "entity":
                {
                    OnlyAttributes(e, applicability ? [] : ["instructions"]);
                    return Entity(e, instructions);
                }
                case "partOf":
                {
                    OnlyAttributes(e, [.. reqAttrs, "relation"]);
                    var card = Cardinality(e, applicability, simpleOnly: true);
                    var relation = (string?)e.Attribute("relation");
                    if (relation != null && !Relations.Contains(relation)) Fail(e, $"The relation „{relation}” is not one of {string.Join(", ", Relations)}.");
                    var kids = Children(e, "entity");
                    if (kids.Count != 1) { Fail(e, "A <partOf> holds exactly one <entity>."); return null; }
                    OnlyAttributes(kids[0]);
                    return Entity(kids[0], null) is { } whole ? new IdsPartOfFacet(whole, relation, card, instructions) : null;
                }
                case "classification":
                {
                    OnlyAttributes(e, [.. reqAttrs, .. applicability ? Array.Empty<string>() : ["uri"]]);
                    var card = Cardinality(e, applicability, simpleOnly: false);
                    string[] order = ["value", "system"];
                    var kids = Children(e, order);
                    InOrder(e, kids, order);
                    var system = One(e, kids, "system", required: true);
                    var value = One(e, kids, "value", required: false);
                    if (card == IdsCardinality.Optional && value == null) Fail(e, "An optional <classification> needs a value: without one there is nothing to check.");
                    var sv = system == null ? null : Value(system, null);
                    return sv == null ? null : new IdsClassificationFacet(value == null ? null : Value(value, null), sv, (string?)e.Attribute("uri"), card, instructions);
                }
                case "attribute":
                {
                    OnlyAttributes(e, reqAttrs);
                    var card = Cardinality(e, applicability, simpleOnly: false);
                    string[] order = ["name", "value"];
                    var kids = Children(e, order);
                    InOrder(e, kids, order);
                    var nameEl = One(e, kids, "name", required: true);
                    var value = One(e, kids, "value", required: false);
                    if (card == IdsCardinality.Optional && value == null) Fail(e, "An optional <attribute> needs a value: without one there is nothing to check.");
                    var nv = nameEl == null ? null : Value(nameEl, null);
                    return nv == null ? null : new IdsAttributeFacet(nv, value == null ? null : Value(value, null), card, instructions);
                }
                case "property":
                {
                    OnlyAttributes(e, [.. reqAttrs, "dataType", .. applicability ? Array.Empty<string>() : ["uri"]]);
                    var card = Cardinality(e, applicability, simpleOnly: false);
                    var dataType = (string?)e.Attribute("dataType");
                    if (dataType != null && !Regex.IsMatch(dataType, "^[A-Z]+$")) Fail(e, $"The dataType „{dataType}” is not an IFC defined type in upper case (IFCLABEL, IFCLENGTHMEASURE…).");
                    string[] order = ["propertySet", "baseName", "value"];
                    var kids = Children(e, order);
                    InOrder(e, kids, order);
                    var set = One(e, kids, "propertySet", required: true);
                    var baseName = One(e, kids, "baseName", required: true);
                    var value = One(e, kids, "value", required: false);
                    if (value != null && dataType == null) Fail(e, "A <property> with a value needs its dataType (the value is read as that type).");
                    if (card == IdsCardinality.Optional && dataType == null && value == null) Fail(e, "An optional <property> needs a dataType or a value: without one there is nothing to check.");
                    if (card == IdsCardinality.Prohibited && (dataType != null || value != null)) Fail(e, "A prohibited <property> names only its set and name: prohibit the whole property, or ask for its values as required or optional.");
                    var sv = set == null ? null : Value(set, null);
                    var bv = baseName == null ? null : Value(baseName, null);
                    var vv = value == null ? null : Value(value, dataType);
                    return sv == null || bv == null ? null : new IdsPropertyFacet(sv, bv, dataType, vv, (string?)e.Attribute("uri"), card, instructions);
                }
                case "material":
                {
                    OnlyAttributes(e, [.. reqAttrs, .. applicability ? Array.Empty<string>() : ["uri"]]);
                    var card = Cardinality(e, applicability, simpleOnly: false);
                    var kids = Children(e, "value");
                    var value = One(e, kids, "value", required: false);
                    if (card == IdsCardinality.Optional && value == null) Fail(e, "An optional <material> needs a value: without one there is nothing to check.");
                    return new IdsMaterialFacet(value == null ? null : Value(value, null), (string?)e.Attribute("uri"), card, instructions);
                }
                default: return null;
            }
        }

        IdsEntityFacet? Entity(XElement e, string? instructions)
        {
            string[] order = ["name", "predefinedType"];
            var kids = Children(e, order);
            InOrder(e, kids, order);
            var nameEl = One(e, kids, "name", required: true);
            var pre = One(e, kids, "predefinedType", required: false);
            var nv = nameEl == null ? null : Value(nameEl, null);
            return nv == null ? null : new IdsEntityFacet(nv, pre == null ? null : Value(pre, null), instructions);
        }

        XElement? One(XElement parent, List<XElement> kids, string name, bool required)
        {
            var found = kids.Where(k => k.Name.LocalName == name).ToList();
            if (found.Count > 1) Fail(found[1], $"<{name}> is there more than once in <{parent.Name.LocalName}>.");
            if (found.Count == 0 && required) Fail(parent, $"<{parent.Name.LocalName}> has no <{name}>.");
            return found.FirstOrDefault();
        }

        /// <summary>An idsValue: one simpleValue or one xs:restriction. With a dataType, the value must read as that type's XML base type.</summary>
        IdsValue? Value(XElement e, string? dataType)
        {
            OnlyAttributes(e);
            var kids = e.Elements().ToList();
            if (kids.Count != 1) { Fail(e, $"<{e.Name.LocalName}> holds one <simpleValue> or one <xs:restriction>."); return null; }
            var k = kids[0];
            var typeBase = dataType == null ? null : IdsTypes.BaseOf(dataType);
            if (k.Name == I + "simpleValue")
            {
                OnlyAttributes(k);
                if (k.HasElements) Fail(k, "<simpleValue> holds text only.");
                var text = k.Value;
                if (typeBase != null && !IdsTypes.Lexical(typeBase, text)) Fail(k, $"„{text}” is not a {IdsTypes.Describe(typeBase)}, as {dataType} needs.");
                return IdsValue.Of(text);
            }
            if (k.Name == X + "restriction") return Restriction(k, dataType, typeBase) is { } r ? new IdsValue(null, r) : null;
            Fail(k, $"<{k.Name.LocalName}> is neither <simpleValue> nor <xs:restriction>.");
            return null;
        }

        IdsRestriction? Restriction(XElement e, string? dataType, string? typeBase)
        {
            OnlyAttributes(e, "base");
            var baseText = (string?)e.Attribute("base") ?? "xs:string";
            // The base as a prefixed name: its prefix whatever the file declared for the XML Schema namespace.
            var colon = baseText.IndexOf(':');
            var local = colon >= 0 ? baseText[(colon + 1)..] : baseText;
            var ns = colon >= 0 ? e.GetNamespaceOfPrefix(baseText[..colon]) : e.GetDefaultNamespace();
            if (ns != X || !IdsTypes.Bases.Contains(local)) { Fail(e, $"The restriction's base „{baseText}” is not one IDS allows ({string.Join(", ", IdsTypes.Bases.Select(b => "xs:" + b))})."); return null; }
            if (typeBase != null && !IdsTypes.Compatible(typeBase, local)) Fail(e, $"A restriction on {dataType} has the base xs:{typeBase}, not xs:{local}.");
            var en = new List<string>(); var pat = new List<string>(); var regexes = new List<Regex>();
            string? minI = null, minE = null, maxI = null, maxE = null; int? len = null, minL = null, maxL = null;
            foreach (var f in e.Elements())
            {
                if (f.Name.Namespace != X) { Fail(f, $"<{f.Name.LocalName}> is not an XML Schema facet."); continue; }
                if (f.Name.LocalName == "annotation") continue;
                var v = (string?)f.Attribute("value");
                if (v == null) { Fail(f, $"<xs:{f.Name.LocalName}> needs its value attribute."); continue; }
                if (!IdsTypes.Allows(local, f.Name.LocalName)) { Fail(f, $"<xs:{f.Name.LocalName}> does not apply to xs:{local}."); continue; }
                switch (f.Name.LocalName)
                {
                    case "enumeration":
                        if (!IdsTypes.Lexical(local, v)) Fail(f, $"„{v}” is not a {IdsTypes.Describe(local)}.");
                        en.Add(v); break;
                    case "pattern":
                        try { regexes.Add(XsdPattern.Compile(v)); pat.Add(v); }
                        catch (FormatException ex) { Fail(f, $"The pattern „{v}”: {ex.Message}"); }
                        break;
                    case "minInclusive": case "minExclusive": case "maxInclusive": case "maxExclusive":
                        if (!IdsTypes.Lexical(local, v)) { Fail(f, $"„{v}” is not a {IdsTypes.Describe(local)}."); break; }
                        if (f.Name.LocalName == "minInclusive") minI = v; else if (f.Name.LocalName == "minExclusive") minE = v; else if (f.Name.LocalName == "maxInclusive") maxI = v; else maxE = v;
                        break;
                    case "length": case "minLength": case "maxLength":
                        if (!int.TryParse(v, NumberStyles.None, CultureInfo.InvariantCulture, out var n)) { Fail(f, $"„{v}” is not a length (a whole number)."); break; }
                        if (f.Name.LocalName == "length") len = n; else if (f.Name.LocalName == "minLength") minL = n; else maxL = n;
                        break;
                    default: Fail(f, $"<xs:{f.Name.LocalName}> is not a facet IDS uses."); break;
                }
            }
            if (minI != null && minE != null) Fail(e, "A restriction has both minInclusive and minExclusive.");
            if (maxI != null && maxE != null) Fail(e, "A restriction has both maxInclusive and maxExclusive.");
            if (en.Count == 0 && pat.Count == 0 && minI == null && minE == null && maxI == null && maxE == null && len == null && minL == null && maxL == null)
                Fail(e, "The restriction holds no facet (enumeration, pattern, bounds or length).");
            return new IdsRestriction(local, en, pat, minI, minE, maxI, maxE, len, minL, maxL) { Compiled = regexes };
        }
    }
}
