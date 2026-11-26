using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Planning;

/// <summary>
/// A part of the schedule generator another module adds (docs/plan-extension-modules.md §6.3: an extension's procurement
/// chains), registered with <c>r.Service&lt;IGeneratorContributor&gt;</c>. It is called after the steps, with the spec's key
/// it owns (<see cref="Key"/>: <c>spec[Key]</c>, kept by the generator as it came), and makes tasks and links through
/// <see cref="GenContext"/>. Its tasks carry <c>gen: "&lt;Key&gt;/…"</c> and <c>genBy: Key</c>; generated again in an edition
/// without the contributor they are kept as they are, with their links, so the schedule does not depend on what is installed.
/// </summary>
public interface IGeneratorContributor
{
    /// <summary>The key of its part of the spec and the prefix of its tasks' <c>gen</c> (letters, digits, '-', '_', '.').</summary>
    string Key { get; }

    /// <summary>Its tasks and links for this spec (<paramref name="spec"/>: <c>spec[Key]</c>, null when the spec has none).</summary>
    void Contribute(GenContext ctx, JsonNode? spec);

    /// <summary>Its part of a spec checked as a command gives it (a <see cref="CommandException"/> when it is wrong).</summary>
    void Check(JsonNode? spec) { }

    /// <summary>What its section of the generator's configurator starts from (the options query's <c>contributors[Key]</c>), null for none.</summary>
    JsonNode? Default => null;
}

/// <summary>A step's tasks as the generator made them: by storey id ("" for a step done once).</summary>
public sealed record GenStepTasks(GenStep Step, bool PerStorey, IReadOnlyDictionary<string, string> ByStorey);

/// <summary>
/// What a generator contributor works with: the model, the spec, the storeys, the steps' tasks, the tasks there were before
/// (<see cref="OldTasks"/>, <see cref="Keep"/>: whether those made by hand stay), and a task factory over stable ids.
/// </summary>
public sealed class GenContext
{
    readonly Func<string, string, string?, double, JsonObject, string?, Entity> task;
    readonly Action<Entity, Generator.Made> add;
    readonly List<(string From, string To, string Type, int Lag)> links;
    readonly List<string> notes;

    internal GenContext(string key, IQueryContext q, GenSpec spec, bool keep, IReadOnlyList<Entity> storeys, IReadOnlyList<GenStepTasks> steps, IReadOnlyList<Entity> oldTasks,
        Func<string, string, string?, double, JsonObject, string?, Entity> task, Action<Entity, Generator.Made> add, List<(string, string, string, int)> links, List<string> notes)
    {
        Key = key; Query = q; Spec = spec; Keep = keep; Storeys = storeys; Steps = steps; OldTasks = oldTasks;
        this.task = task; this.add = add; this.links = links; this.notes = notes;
    }

    public string Key { get; }
    public IQueryContext Query { get; }
    public GenSpec Spec { get; }
    public bool Keep { get; }
    /// <summary>The model's storeys, lowest first.</summary>
    public IReadOnlyList<Entity> Storeys { get; }
    public IReadOnlyList<GenStepTasks> Steps { get; }
    public IReadOnlyList<Entity> OldTasks { get; }

    /// <summary>
    /// A task of the contributor's, added to the schedule: <paramref name="gen"/> under its key (<c>"&lt;Key&gt;/" + gen</c>, the
    /// same id as the task generated with that key before), <paramref name="extra"/> its attributes (duration, milestone,
    /// phase…); <paramref name="summary"/> for a summary over others. Returns the task.
    /// </summary>
    public Entity Task(string gen, string name, string? parent, double order, JsonObject extra, string? kind = null, bool summary = false)
    {
        var full = Generator.ContributedGen(Key, gen);
        var attrs = (JsonObject)extra.DeepClone();
        attrs[Generator.GenByKey] = Key;
        var e = task(full, name, parent, order, attrs, kind);
        var days = (int)Math.Round(e.Num("duration", summary ? 1 : 0));
        add(e, new Generator.Made(e.Id, full, new GenStep(full, name, [], null, "once", days, null, 0), null, summary, summary ? "summary" : Key, null, null, 0));
        return e;
    }

    /// <summary>A link of the sequence (FS, SS, FF, SF), made unless it would loop or one is there already.</summary>
    public void Link(string from, string to, string type = "FS", int lag = 0) => links.Add((from, to, type, lag));

    /// <summary>A note the preview shows.</summary>
    public void Note(string text) => notes.Add(text);

    /// <summary>The task a step starts with: its task done once, or its lowest storey's; null when it made none.</summary>
    public string? FirstOf(string stepKey)
    {
        if (Steps.FirstOrDefault(x => x.Step.Key == stepKey) is not { } st) return null;
        if (!st.PerStorey) return st.ByStorey.GetValueOrDefault("");
        foreach (var s in Storeys) if (st.ByStorey.TryGetValue(s.Id, out var t)) return t;
        return null;
    }
}
