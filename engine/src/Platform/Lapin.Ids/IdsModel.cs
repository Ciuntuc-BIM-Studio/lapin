using System.Text.Json.Nodes;

namespace Lapin.Ids;

/// <summary>
/// An IDS 1.0 file as read (buildingSMART's ids.xsd, version 1.0.0): its info block and its specifications. Every
/// value is kept as the file wrote it; <see cref="IdsReader"/> refuses what the schema and the user manual do not allow.
/// </summary>
public sealed class IdsDocument(IdsInfo Info, IReadOnlyList<IdsSpecification> Specifications)
{
    public IdsInfo Info { get; } = Info;
    public IReadOnlyList<IdsSpecification> Specifications { get; } = Specifications;
    public JsonObject ToJson() => new()
    {
        ["info"] = Info.ToJson(),
        ["specifications"] = new JsonArray([.. Specifications.Select((s, i) => (JsonNode)s.ToJson(i))]),
    };
}

/// <summary>The info block: a title, then copyright, version, description, author (an e-mail address), date, purpose and milestone.</summary>
public sealed class IdsInfo(string Title, string? Copyright, string? Version, string? Description, string? Author, string? Date, string? Purpose, string? Milestone)
{
    public string Title { get; } = Title;
    public string? Copyright { get; } = Copyright;
    public string? Version { get; } = Version;
    public string? Description { get; } = Description;
    public string? Author { get; } = Author;
    public string? Date { get; } = Date;
    public string? Purpose { get; } = Purpose;
    public string? Milestone { get; } = Milestone;
    public JsonObject ToJson()
    {
        var o = new JsonObject { ["title"] = Title };
        void Add(string k, string? v) { if (v is { Length: > 0 }) o[k] = v; }
        Add("copyright", Copyright); Add("version", Version); Add("description", Description); Add("author", Author);
        Add("date", Date); Add("purpose", Purpose); Add("milestone", Milestone);
        return o;
    }
}

/// <summary>How many applicable entities a specification asks for (minOccurs/maxOccurs of its applicability).</summary>
public enum IdsUsage { Required, Optional, Prohibited }

/// <summary>A requirement facet's cardinality (the entity facet's is always required).</summary>
public enum IdsCardinality { Required, Optional, Prohibited }

/// <summary>
/// One specification: its name, the IFC versions it is for, its identifier, description and instructions, the facets that pick
/// the entities it applies to, and the facets those entities must meet (none: being applicable is enough, or, prohibited, too much).
/// </summary>
public sealed class IdsSpecification(string Name, IReadOnlyList<string> IfcVersions, string? Identifier, string? Description, string? Instructions, int MinOccurs, int? MaxOccurs, IReadOnlyList<IdsFacet> Applicability, IReadOnlyList<IdsFacet> Requirements, string? RequirementsDescription)
{
    public string Name { get; } = Name;
    public IReadOnlyList<string> IfcVersions { get; } = IfcVersions;
    public string? Identifier { get; } = Identifier;
    public string? Description { get; } = Description;
    public string? Instructions { get; } = Instructions;
    public int MinOccurs { get; } = MinOccurs;
    public int? MaxOccurs { get; } = MaxOccurs;
    public IReadOnlyList<IdsFacet> Applicability { get; } = Applicability;
    public IReadOnlyList<IdsFacet> Requirements { get; } = Requirements;
    public string? RequirementsDescription { get; } = RequirementsDescription;
    public IdsUsage Usage => MaxOccurs == 0 ? IdsUsage.Prohibited : MinOccurs >= 1 ? IdsUsage.Required : IdsUsage.Optional;

    public JsonObject ToJson(int index)
    {
        var o = new JsonObject
        {
            ["index"] = index, ["name"] = Name, ["ifcVersion"] = new JsonArray([.. IfcVersions.Select(v => (JsonNode)v)]),
            ["usage"] = Usage.ToString().ToLowerInvariant(),
            ["applicability"] = new JsonArray([.. Applicability.Select(f => (JsonNode)f.Describe())]),
            ["requirements"] = new JsonArray([.. Requirements.Select(f => (JsonNode)f.Describe())]),
        };
        if (Identifier is { Length: > 0 }) o["identifier"] = Identifier;
        if (Description is { Length: > 0 }) o["description"] = Description;
        if (Instructions is { Length: > 0 }) o["instructions"] = Instructions;
        return o;
    }
}

/// <summary>A value a facet asks for: a simple value, or a restriction (xs:restriction).</summary>
public sealed class IdsValue(string? Simple, IdsRestriction? Restriction)
{
    public string? Simple { get; } = Simple;
    public IdsRestriction? Restriction { get; } = Restriction;
    public static IdsValue Of(string simple) => new(simple, null);

    public override string ToString() => Simple ?? Restriction?.ToString() ?? "";
}

/// <summary>
/// An xs:restriction: its base type and its facets. Enumerations are any one of; patterns (XSD regular expressions, the whole value)
/// any one of; bounds and lengths all of; and each group present must be met.
/// </summary>
public sealed class IdsRestriction(string Base, IReadOnlyList<string> Enumeration, IReadOnlyList<string> Patterns, string? MinInclusive, string? MinExclusive, string? MaxInclusive, string? MaxExclusive, int? Length, int? MinLength, int? MaxLength)
{
    public string Base { get; } = Base;
    public IReadOnlyList<string> Enumeration { get; } = Enumeration;
    public IReadOnlyList<string> Patterns { get; } = Patterns;
    public string? MinInclusive { get; } = MinInclusive;
    public string? MinExclusive { get; } = MinExclusive;
    public string? MaxInclusive { get; } = MaxInclusive;
    public string? MaxExclusive { get; } = MaxExclusive;
    public int? Length { get; } = Length;
    public int? MinLength { get; } = MinLength;
    public int? MaxLength { get; } = MaxLength;
    internal IReadOnlyList<System.Text.RegularExpressions.Regex> Compiled { get; init; } = [];

    public override string ToString()
    {
        var parts = new List<string>();
        if (Enumeration.Count > 0) parts.Add("one of " + string.Join(", ", Enumeration.Select(e => $"„{e}”")));
        if (Patterns.Count > 0) parts.Add("matching " + string.Join(" or ", Patterns.Select(p => $"/{p}/")));
        if (MinInclusive != null) parts.Add($"≥ {MinInclusive}");
        if (MinExclusive != null) parts.Add($"> {MinExclusive}");
        if (MaxInclusive != null) parts.Add($"≤ {MaxInclusive}");
        if (MaxExclusive != null) parts.Add($"< {MaxExclusive}");
        if (Length != null) parts.Add($"{Length} characters");
        if (MinLength != null) parts.Add($"at least {MinLength} characters");
        if (MaxLength != null) parts.Add($"at most {MaxLength} characters");
        return string.Join(", ", parts);
    }
}

/// <summary>What every facet has: where it stands (applicability or requirements), its cardinality and its instructions.</summary>
public abstract class IdsFacet(IdsCardinality Cardinality, string? Instructions)
{
    public IdsCardinality Cardinality { get; } = Cardinality;
    public string? Instructions { get; } = Instructions;
    public abstract string Kind { get; }
    /// <summary>The facet in words, as a report states it.</summary>
    public abstract string Text { get; }

    public JsonObject Describe()
    {
        var o = new JsonObject { ["kind"] = Kind, ["text"] = Text, ["cardinality"] = Cardinality.ToString().ToLowerInvariant() };
        if (Instructions is { Length: > 0 }) o["instructions"] = Instructions;
        return o;
    }
}

public sealed class IdsEntityFacet(IdsValue Name, IdsValue? PredefinedType, string? Instructions = null) : IdsFacet(IdsCardinality.Required, Instructions)
{
    public IdsValue Name { get; } = Name;
    public IdsValue? PredefinedType { get; } = PredefinedType;
    public override string Kind => "entity";
    public override string Text => PredefinedType == null ? $"Entity {Name}" : $"Entity {Name} of type {PredefinedType}";
}

public sealed class IdsAttributeFacet(IdsValue Name, IdsValue? Value, IdsCardinality Cardinality = IdsCardinality.Required, string? Instructions = null) : IdsFacet(Cardinality, Instructions)
{
    public IdsValue Name { get; } = Name;
    public IdsValue? Value { get; } = Value;
    public override string Kind => "attribute";
    public override string Text => Value == null ? $"Attribute {Name}" : $"Attribute {Name} = {Value}";
}

public sealed class IdsClassificationFacet(IdsValue? Value, IdsValue System, string? Uri = null, IdsCardinality Cardinality = IdsCardinality.Required, string? Instructions = null) : IdsFacet(Cardinality, Instructions)
{
    public IdsValue? Value { get; } = Value;
    public IdsValue System { get; } = System;
    public string? Uri { get; } = Uri;
    public override string Kind => "classification";
    public override string Text => Value == null ? $"Classified in {System}" : $"Classified in {System} as {Value}";
}

public sealed class IdsPropertyFacet(IdsValue PropertySet, IdsValue BaseName, string? DataType, IdsValue? Value, string? Uri = null, IdsCardinality Cardinality = IdsCardinality.Required, string? Instructions = null) : IdsFacet(Cardinality, Instructions)
{
    public IdsValue PropertySet { get; } = PropertySet;
    public IdsValue BaseName { get; } = BaseName;
    public string? DataType { get; } = DataType;
    public IdsValue? Value { get; } = Value;
    public string? Uri { get; } = Uri;
    public override string Kind => "property";
    public override string Text => $"Property {PropertySet}.{BaseName}{(DataType != null ? $" ({DataType})" : "")}{(Value != null ? $" = {Value}" : "")}";
}

public sealed class IdsMaterialFacet(IdsValue? Value, string? Uri = null, IdsCardinality Cardinality = IdsCardinality.Required, string? Instructions = null) : IdsFacet(Cardinality, Instructions)
{
    public IdsValue? Value { get; } = Value;
    public string? Uri { get; } = Uri;
    public override string Kind => "material";
    public override string Text => Value == null ? "A material" : $"Material {Value}";
}

/// <summary>Part of a whole of the entity given, through the relation given (one of the five) or any of them, recursively.</summary>
public sealed class IdsPartOfFacet(IdsEntityFacet Entity, string? Relation, IdsCardinality Cardinality = IdsCardinality.Required, string? Instructions = null) : IdsFacet(Cardinality, Instructions)
{
    public IdsEntityFacet Entity { get; } = Entity;
    public string? Relation { get; } = Relation;
    public override string Kind => "partOf";
    public override string Text => $"Part of {Entity.Text.Replace("Entity ", "")}{(Relation != null ? $" by {Relation}" : "")}";
}
