using System.Diagnostics;
using System.Text.Json.Nodes;
using Lapin.Ifc;

namespace Lapin.Ids;

/// <summary>One applicable entity that failed one requirement: who it is, which requirement, what it has and what was asked.</summary>
public sealed class IdsFailure(string GlobalId, string Class, string? Name, string? ElementId, int Facet, string Requirement, string Actual, string Expected, string? Instructions)
{
    public string GlobalId { get; } = GlobalId;
    public string Class { get; } = Class;
    public string? Name { get; } = Name;
    public string? ElementId { get; } = ElementId;
    public int Facet { get; } = Facet;
    public string Requirement { get; } = Requirement;
    public string Actual { get; } = Actual;
    public string Expected { get; } = Expected;
    public string? Instructions { get; } = Instructions;
    public JsonObject ToJson()
    {
        var o = new JsonObject
        {
            ["globalId"] = GlobalId, ["class"] = Class, ["facet"] = Facet, ["requirement"] = Requirement, ["actual"] = Actual, ["expected"] = Expected,
        };
        if (Name is { Length: > 0 }) o["name"] = Name;
        if (ElementId != null) o["elementId"] = ElementId;
        if (Instructions is { Length: > 0 }) o["instructions"] = Instructions;
        return o;
    }
}

/// <summary>
/// A specification's outcome: <c>pass</c>, <c>fail</c>, or <c>skipped</c> (not for the file's IFC version); how many entities it
/// applied to, passed and failed; each failure (the first <see cref="IdsChecker.MaxFailures"/> told in full), and every failing
/// entity's GlobalId and model element.
/// </summary>
public sealed class IdsSpecResult(int Index, string Name, string? Identifier, string? Description, string? Instructions, string Usage, string Status, int Applicable, int Passed, int Failed, string? Message, IReadOnlyList<IdsFailure> Failures, IReadOnlyList<string> FailedGlobalIds, IReadOnlyList<string> FailedElements, IReadOnlyList<string> Requirements, int Dropped = 0)
{
    public int Index { get; } = Index;
    public string Name { get; } = Name;
    public string? Identifier { get; } = Identifier;
    public string? Description { get; } = Description;
    public string? Instructions { get; } = Instructions;
    public string Usage { get; } = Usage;
    public string Status { get; } = Status;
    public int Applicable { get; } = Applicable;
    public int Passed { get; } = Passed;
    public int Failed { get; } = Failed;
    public string? Message { get; } = Message;
    public IReadOnlyList<IdsFailure> Failures { get; } = Failures;
    public IReadOnlyList<string> FailedGlobalIds { get; } = FailedGlobalIds;
    public IReadOnlyList<string> FailedElements { get; } = FailedElements;
    public IReadOnlyList<string> Requirements { get; } = Requirements;
    public int Dropped { get; } = Dropped;
    public bool Fails => Status == "fail";

    public JsonObject ToJson(int? limit = null)
    {
        var o = new JsonObject
        {
            ["index"] = Index, ["name"] = Name, ["usage"] = Usage, ["status"] = Status, ["applicable"] = Applicable, ["passed"] = Passed, ["failed"] = Failed,
            ["failures"] = new JsonArray([.. Failures.Take(limit ?? int.MaxValue).Select(f => (JsonNode)f.ToJson())]),
            ["more"] = Dropped + Math.Max(0, Failures.Count - (limit ?? int.MaxValue)),
            ["elements"] = new JsonArray([.. FailedElements.Select(x => (JsonNode)x)]),
            ["requirements"] = new JsonArray([.. Requirements.Select(x => (JsonNode)x)]),
        };
        if (Identifier is { Length: > 0 }) o["identifier"] = Identifier;
        if (Description is { Length: > 0 }) o["description"] = Description;
        if (Instructions is { Length: > 0 }) o["instructions"] = Instructions;
        if (Message != null) o["message"] = Message;
        return o;
    }
}

/// <summary>A whole run: the file's schema, each specification's outcome, how many objects were looked at, and how long it took.</summary>
public sealed class IdsRun(string Schema, IReadOnlyList<IdsSpecResult> Specifications, int Objects, long Milliseconds)
{
    public string Schema { get; } = Schema;
    public IReadOnlyList<IdsSpecResult> Specifications { get; } = Specifications;
    public int Objects { get; } = Objects;
    public long Milliseconds { get; } = Milliseconds;
    public int Failing => Specifications.Count(s => s.Fails);
    public int Passing => Specifications.Count(s => s.Status == "pass");
    public int Skipped => Specifications.Count(s => s.Status == "skipped");
}

/// <summary>
/// Checks an IFC file against an IDS (buildingSMART IDS 1.0), as its user manual says: a specification applies to the entities
/// every applicability facet matches; each must meet every requirement facet, by its cardinality (required: it must hold;
/// optional: if it is there at all, it must hold; prohibited: it must not hold); a required specification needs at least one
/// applicable entity, a prohibited one none. Entities are taken in the file's order, so two runs over the same file say the
/// same thing in the same order. Stopped by <paramref name="stop"/>; <paramref name="progress"/> told how far it got.
/// </summary>
public sealed class IdsChecker(IfcFacts facts, Func<string, bool>? inModel = null, CancellationToken stop = default, Action<int, int>? progress = null)
{
    /// <summary>The failures a specification tells in full; past them it counts, and keeps every failing GlobalId.</summary>
    public const int MaxFailures = 2000;

    public static IdsRun Run(StepFile file, IdsDocument ids, Func<string, bool>? inModel = null, CancellationToken stop = default, Action<int, int>? progress = null)
    {
        var sw = Stopwatch.StartNew();
        var facts = new IfcFacts(file, stop);
        var c = new IdsChecker(facts, inModel, stop, progress);
        var results = ids.Specifications.Select((s, i) => c.Check(s, i)).ToList();
        return new IdsRun(facts.Schema, results, facts.Objects.Count, sw.ElapsedMilliseconds);
    }

    public IdsSpecResult Check(IdsSpecification s, int index)
    {
        var usage = s.Usage.ToString().ToLowerInvariant();
        var requirements = s.Requirements.Select(r => $"{(r.Cardinality == IdsCardinality.Required ? "" : r.Cardinality.ToString().ToLowerInvariant() + ": ")}{r.Text}").ToList();
        if (!s.IfcVersions.Contains(facts.Schema))
            return new(index, s.Name, s.Identifier, s.Description, s.Instructions, usage, "skipped", 0, 0, 0,
                $"For {string.Join(", ", s.IfcVersions)}; the model is {facts.Schema}.", [], [], [], requirements);

        var applicable = new List<StepEntity>();
        var candidates = Candidates(s);
        var n = 0;
        foreach (var e in candidates)
        {
            if ((++n & 255) == 0) { stop.ThrowIfCancellationRequested(); progress?.Invoke(n, candidates.Count); }
            if (s.Applicability.All(f => Holds(f, e, IdsCardinality.Required).Ok)) applicable.Add(e);
        }

        var failures = new List<IdsFailure>();
        var failedIds = new List<string>();
        var failedElements = new List<string>();
        var seenElements = new HashSet<string>();
        var failed = 0;
        var dropped = 0;
        void Failed(StepEntity e, int facet, string requirement, string actual, string expected, string? instructions)
        {
            var gid = GlobalId(e);
            var element = ElementOf(e);
            if (failures.Count < MaxFailures) failures.Add(new IdsFailure(gid, IfcModel.ClassName(e.Type), Name(e), element, facet, requirement, actual, expected, instructions));
            else dropped++;
        }
        void Count(StepEntity e)
        {
            failed++;
            failedIds.Add(GlobalId(e));
            if (ElementOf(e) is { } el && seenElements.Add(el)) failedElements.Add(el);
        }

        if (s.Usage == IdsUsage.Prohibited)
        {
            foreach (var e in applicable) { Failed(e, -1, "Prohibited", "applies", "no entity it applies to", s.Instructions); Count(e); }
            return Result(applicable.Count == 0 ? "pass" : "fail", applicable.Count, 0, applicable.Count == 0 ? null : $"{applicable.Count} entities match a prohibited specification.");
        }

        var passed = 0;
        foreach (var e in applicable)
        {
            stop.ThrowIfCancellationRequested();
            var ok = true;
            for (var i = 0; i < s.Requirements.Count; i++)
            {
                var f = s.Requirements[i];
                var h = Holds(f, e, f.Cardinality);
                if (h.Ok) continue;
                ok = false;
                Failed(e, i, requirements[i], h.Actual, h.Expected, f.Instructions ?? s.Instructions);
            }
            if (ok) passed++; else Count(e);
        }
        var status = failed > 0 || (s.Usage == IdsUsage.Required && applicable.Count == 0) ? "fail" : "pass";
        var message = s.Usage == IdsUsage.Required && applicable.Count == 0 ? "Required, and nothing in the model is what it applies to." : null;
        return Result(status, applicable.Count, passed, message);

        IdsSpecResult Result(string st, int app, int ok, string? msg) =>
            new(index, s.Name, s.Identifier, s.Description, s.Instructions, usage, st, app, ok, failed, msg, failures, failedIds, failedElements, requirements, dropped);
    }

    /// <summary>
    /// What a specification may apply to: the entities of the classes its entity facet names, when it names them (a simple value or an
    /// enumeration: found by the file's index, a material too), else every object.
    /// </summary>
    IReadOnlyList<StepEntity> Candidates(IdsSpecification s)
    {
        if (s.Applicability.OfType<IdsEntityFacet>().FirstOrDefault() is { } entity)
        {
            IEnumerable<string>? names = entity.Name.Simple is { } one ? [one] : entity.Name.Restriction is { Enumeration.Count: > 0, Patterns.Count: 0 } r ? r.Enumeration : null;
            if (names != null)
                return [.. names.Distinct().SelectMany(facts.File.OfType).OrderBy(e => e.Id)];
        }
        return facts.Objects;
    }

    static string GlobalId(StepEntity e) => e[0] is string g && g.Length == 22 ? g : $"#{e.Id}";
    static string? Name(StepEntity e) => e.Type is "IFCMATERIAL" or "IFCMATERIALLAYERSET" ? IfcModel.Str(e[e.Type == "IFCMATERIAL" ? 0 : 1]) : e.Args.Count > 2 ? IfcModel.Str(e[2]) : null;

    /// <summary>The model's element an entity stands for: its own GlobalId, else the whole it is a part of (a wall's layer, a stair's flight).</summary>
    string? ElementOf(StepEntity e)
    {
        if (inModel == null) return null;
        var at = e;
        for (var depth = 0; at != null && depth < 16; depth++)
        {
            var g = GlobalId(at);
            if (inModel(g)) return g;
            var up = facts.Parents(at.Id).FirstOrDefault(p => p.Relation is "IFCRELAGGREGATES" or "IFCRELNESTS" or IfcFacts.VoidsFills);
            at = up.Relation == null ? null : facts.Get(up.Whole);
        }
        return null;
    }

    readonly struct Outcome(bool Ok, string Actual = "", string Expected = "")
{
    public bool Ok { get; init; } = Ok;
    public string Actual { get; init; } = Actual;
    public string Expected { get; init; } = Expected;
}

    static Outcome Pass => new(true);

    Outcome Holds(IdsFacet f, StepEntity e, IdsCardinality card) => f switch
    {
        IdsEntityFacet x => Entity(x, e),
        IdsAttributeFacet x => Attribute(x, e, card),
        IdsPropertyFacet x => Property(x, e, card),
        IdsClassificationFacet x => Classification(x, e, card),
        IdsMaterialFacet x => Material(x, e, card),
        IdsPartOfFacet x => PartOf(x, e, card),
        _ => new(false, "", "a facet Lapin does not know"),
    };

    Outcome Entity(IdsEntityFacet f, StepEntity e)
    {
        if (!IdsMatch.AcceptsText(f.Name, e.Type)) return new(false, e.Type, f.Name.ToString());
        if (f.PredefinedType == null) return Pass;
        var (pre, userDefined) = facts.PredefinedType(e);
        return IdsMatch.AcceptsText(f.PredefinedType, pre) || (userDefined && IdsMatch.AcceptsText(f.PredefinedType, "USERDEFINED")) ? Pass
            : new(false, pre ?? "no predefined type", f.PredefinedType.ToString());
    }

    Outcome Attribute(IdsAttributeFacet f, StepEntity e, IdsCardinality card)
    {
        var matched = facts.Attributes(e).Where(a => IdsMatch.AcceptsText(f.Name, a.Name)).ToList();
        var present = matched.Any(a => !a.Value.IsNull);
        var expected = f.Value == null ? $"{f.Name} filled in" : $"{f.Name} = {f.Value}";
        string Actual() => matched.Count == 0 ? $"no attribute {f.Name}" : string.Join("; ", matched.Select(a => $"{a.Name} = {a.Value}"));
        bool Required() => matched.Count > 0 && matched.All(a => !a.Value.IsEmpty && (f.Value == null || IdsMatch.Accepts(f.Value, a.Value)));
        return card switch
        {
            IdsCardinality.Prohibited => (f.Value == null ? !present : !Required()) ? Pass : new(false, Actual(), f.Value == null ? $"no {f.Name}" : $"{f.Name} not {f.Value}"),
            IdsCardinality.Optional when !present => Pass,
            _ => Required() ? Pass : new(false, Actual(), expected),
        };
    }

    Outcome Property(IdsPropertyFacet f, StepEntity e, IdsCardinality card)
    {
        var matched = facts.Properties(e).Where(p => IdsMatch.AcceptsText(f.PropertySet, p.Set) && IdsMatch.AcceptsText(f.BaseName, p.Name)).ToList();
        var expected = $"{f.PropertySet}.{f.BaseName}{(f.DataType != null ? $" as {f.DataType}" : "")}{(f.Value != null ? $" = {f.Value}" : "")}";
        if (card == IdsCardinality.Prohibited) return matched.Count == 0 ? Pass : new(false, Describe(matched), $"no {f.PropertySet}.{f.BaseName}");
        if (matched.Count == 0) return card == IdsCardinality.Optional ? Pass : new(false, $"no {f.PropertySet}.{f.BaseName}", expected);
        foreach (var p in matched)
            if (Why(f, p) is { } why) return new(false, $"{p.Set}.{p.Name}: {why}", expected);
        return Pass;
    }

    static string Describe(List<IfcProp> props) => string.Join("; ", props.Select(p => $"{p.Set}.{p.Name} = {string.Join(", ", p.Values)}"));

    /// <summary>Why a property fails the facet (null: it meets it).</summary>
    static string? Why(IdsPropertyFacet f, IfcProp p)
    {
        if (p.Kind == IfcProp.Unsupported) return "a complex or reference property, which IDS does not read";
        if (p.Values.Count == 0 || p.Values.All(v => v.IsEmpty)) return "empty";
        if (p.Kind is IfcProp.Single or IfcProp.Quantity && p.Values[0].IsEmpty) return "empty";
        if (f.DataType != null && !p.Values.Where(v => !v.IsEmpty).All(v => v.Type == f.DataType))
            return $"{string.Join(", ", p.Values.Select(v => v.Type ?? "untyped").Distinct())}, not {f.DataType}";
        if (f.Value == null) return null;
        var shown = string.Join(", ", p.Values);
        var r = f.Value.Restriction;
        var ranged = r is { } && (r.MinInclusive != null || r.MinExclusive != null || r.MaxInclusive != null || r.MaxExclusive != null);
        switch (p.Kind)
        {
            case IfcProp.Bounded:
            {
                if (p.Lower == null && p.Upper == null) return "no bounds";
                if (ranged)
                    return p.Lower is { } lo && p.Upper is { } hi && IdsMatch.Accepts(f.Value, lo) && IdsMatch.Accepts(f.Value, hi) ? null : $"bounds {p.Lower?.ToString() ?? "open"} to {p.Upper?.ToString() ?? "open"}";
                if (f.Value.Simple is { } sv && IdsTypes.TryNumber(sv, out var x))
                {
                    double? N(IfcVal? v) => v is { } y ? y.Si ?? (y.Raw is double d ? d : y.Raw is long l ? l : null) : null;
                    var ok = (N(p.Lower) is not { } a || x >= a || IdsMatch.Equal(x, a)) && (N(p.Upper) is not { } b || x <= b || IdsMatch.Equal(x, b));
                    return ok ? null : $"bounds {p.Lower?.ToString() ?? "open"} to {p.Upper?.ToString() ?? "open"}";
                }
                return p.Values.Any(v => IdsMatch.Accepts(f.Value, v)) ? null : shown;
            }
            case IfcProp.Enumerated or IfcProp.List or IfcProp.Table:
                // A range: every value within it; any other value: one of them is it.
                return (ranged ? p.Values.All(v => IdsMatch.Accepts(f.Value, v)) : p.Values.Any(v => IdsMatch.Accepts(f.Value, v))) ? null : shown;
            default:
                return IdsMatch.Accepts(f.Value, p.Values[0]) ? null : shown;
        }
    }

    Outcome Classification(IdsClassificationFacet f, StepEntity e, IdsCardinality card)
    {
        var all = facts.Classifications(e);
        var matched = all.Where(c => IdsMatch.AcceptsText(f.System, c.System)).ToList();
        string Actual() => all.Count == 0 ? "not classified" : string.Join("; ", all.Select(c => $"{c.System ?? "(no system)"}: {(c.Codes.Count == 0 ? "(no code)" : c.Codes[0])}"));
        bool Required() => matched.Any(c => f.Value == null || c.Codes.Any(code => IdsMatch.AcceptsText(f.Value, code)));
        var expected = f.Value == null ? $"classified in {f.System}" : $"{f.System}: {f.Value}";
        return card switch
        {
            IdsCardinality.Prohibited => (f.Value == null ? matched.Count == 0 : !Required()) ? Pass : new(false, Actual(), $"not {expected}"),
            IdsCardinality.Optional when matched.Count == 0 => Pass,
            _ => Required() ? Pass : new(false, Actual(), expected),
        };
    }

    Outcome Material(IdsMaterialFacet f, StepEntity e, IdsCardinality card)
    {
        var names = facts.Materials(e);
        string Actual() => names == null ? "no material" : names.Count == 0 ? "a material with no name" : string.Join(", ", names.Distinct());
        bool Required() => names != null && (f.Value == null || names.Any(n => IdsMatch.AcceptsText(f.Value, n)));
        var expected = f.Value == null ? "a material" : $"material {f.Value}";
        return card switch
        {
            IdsCardinality.Prohibited => (f.Value == null ? names == null : !Required()) ? Pass : new(false, Actual(), $"not {expected}"),
            IdsCardinality.Optional when names == null => Pass,
            _ => Required() ? Pass : new(false, Actual(), expected),
        };
    }

    Outcome PartOf(IdsPartOfFacet f, StepEntity e, IdsCardinality card)
    {
        var wholes = Wholes(e, f.Relation);
        var ok = wholes.Any(w => facts.Get(w) is { } we && Entity(f.Entity, we).Ok);
        string Actual() => wholes.Count == 0 ? "part of nothing" : "part of " + string.Join(", ", wholes.Take(4).Select(facts.Get).OfType<StepEntity>().Select(w => IfcModel.ClassName(w.Type) + (Name(w) is { Length: > 0 } nm ? $" „{nm}”" : "")));
        var expected = $"part of {f.Entity.Name}{(f.Entity.PredefinedType != null ? $" {f.Entity.PredefinedType}" : "")}{(f.Relation != null ? $" by {f.Relation}" : "")}";
        if (card == IdsCardinality.Prohibited) return ok ? new(false, Actual(), "not " + expected) : Pass;
        return ok ? Pass : new(false, Actual(), expected);
    }

    /// <summary>
    /// The wholes an entity is part of, recursively: through the relation named, or through any of the five. Containment is in a
    /// spatial structure, which is itself aggregated in a larger one, so a wall in a storey is in its building too; and a part of
    /// an assembly is where the assembly is.
    /// </summary>
    List<int> Wholes(StepEntity e, string? relation)
    {
        var o = new List<int>();
        var seen = new HashSet<int> { e.Id };
        var queue = new Queue<(int Id, bool Contained)>();
        queue.Enqueue((e.Id, false));
        while (queue.Count > 0 && o.Count < 10_000)
        {
            var (id, contained) = queue.Dequeue();
            foreach (var (rel, whole) in facts.Parents(id))
            {
                bool follow;
                var nowContained = contained;
                if (relation == null) follow = true;
                else if (relation == "IFCRELCONTAINEDINSPATIALSTRUCTURE")
                {
                    // Up the aggregates to the first container, then up the containers and their aggregates.
                    follow = rel is "IFCRELCONTAINEDINSPATIALSTRUCTURE" or "IFCRELAGGREGATES";
                    if (rel == "IFCRELCONTAINEDINSPATIALSTRUCTURE") nowContained = true;
                }
                else follow = rel == relation;
                if (!follow || !seen.Add(whole)) continue;
                if (relation != "IFCRELCONTAINEDINSPATIALSTRUCTURE" || nowContained) o.Add(whole);
                queue.Enqueue((whole, nowContained));
            }
        }
        return o;
    }
}
