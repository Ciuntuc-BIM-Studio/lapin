using System.Diagnostics;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// How a module shows a type of its own in the type configurators' preview (decision 529): the sample it stands the type up
/// in, made with the module's own commands in a scratch project (<see cref="TypePreviewScratch"/>), so the preview is the
/// element as the model will build it, never a drawing of the configurator's.
/// </summary>
public interface ITypePreview
{
    /// <summary>Whether it previews types of <paramref name="typeClass"/> (IfcWallType).</summary>
    bool Handles(string typeClass);

    /// <summary>
    /// The type as the configurator holds it (<paramref name="type"/>: the arguments its create command takes, unsaved, and the
    /// saved type's <c>id</c> when it edits one) made in <paramref name="s"/>, and the sample elements of it; <paramref name="sample"/>
    /// the sizes asked (length, height…), each with a default.
    /// </summary>
    TypePreviewSample Build(TypePreviewScratch s, JsonObject type, JsonObject sample);
}

/// <summary>
/// A type's sample: its elements (the first the one the preview is about), the section's line in plan (looking to its left, as
/// a section does) and how deep it sees.
/// </summary>
public sealed record TypePreviewSample(IReadOnlyList<string> Elements, Vec2 SectionStart, Vec2 SectionEnd, double SectionDepth);

/// <summary>
/// A project of its own the preview is made in: the open project's set-up (its drafting standards, view styles, materials and
/// their hatches, its cut order) without any of its model, a storey of the sample's height and one above it. Its own session,
/// its own undo history and geometry, dropped when the preview is drawn: the open project is only read.
/// </summary>
public sealed class TypePreviewScratch
{
    internal TypePreviewScratch(Session session, IModelView source, string storey, string above, double storeyHeight)
    {
        Session = session;
        Source = source;
        Storey = storey;
        Above = above;
        StoreyHeight = storeyHeight;
    }

    public Session Session { get; }
    /// <summary>The open project, read only (a saved type's own settings the configurator does not edit).</summary>
    public IModelView Source { get; }
    public IModelView Model => Session.Model;
    /// <summary>The storey the sample stands on (at ±0.00) and the one above it.</summary>
    public string Storey { get; }
    public string Above { get; }
    public double StoreyHeight { get; }

    /// <summary>A command of the product's, run in the scratch project.</summary>
    public JsonNode? Run(string command, JsonObject args)
    {
        Session.Execute(command, args, out var r);
        return r;
    }

    /// <summary>A change made outside a command (a saved type's settings copied over).</summary>
    public void Edit(Action<IEdit> edit) => Session.Edit("Preview", edit);
}

/// <summary>
/// The type configurators' preview (decision 529): <c>types.preview {class, type, sample, views, scale, detail, styleId}</c>
/// stands the type being edited, unsaved, up in a sample (a wall: 3 m long, a short wall joined to it in T) in a scratch
/// project, and draws it as the drawings draw: a plan and a section through <see cref="Views.Render"/> (the view's
/// Visibility / Graphics, its style, its scale's hatches and pens, its level of detail), the sample's geometry as a mesh.
/// Each part keeps its key (a layer's <c>layer:i</c>) so the configurator lights a layer up in all three. Answers are kept
/// by their question and the project's revision. <c>layers: "style"</c> draws only the layers the view style's partial
/// structure display keeps; by default every layer, so the type is shown whole.
/// </summary>
public static class TypePreviews
{
    public static readonly string[] AllViews = ["plan", "section", "3d"];

    /// <summary>How many answers are kept per project (a configurator's last edits, at the scales and levels asked).</summary>
    const int Kept = 24;

    public static void Register(IModuleRegistry r)
    {
        r.Query("types.preview", (q, a) => Preview(q, a));
    }

    /// <summary>The preview of the type in <paramref name="a"/>, or why there is none (<c>problem</c>).</summary>
    public static JsonObject Preview(IQueryContext q, JsonObject a)
    {
        var watch = Stopwatch.StartNew();
        var catalog = q.Services as ModuleCatalog ?? throw new CommandException("A type's preview needs the product's modules.");
        var cls = (string?)a["class"] ?? throw new CommandException("Say which class of type to preview (IfcWallType).");
        var maker = catalog.All<ITypePreview>().FirstOrDefault(p => p.Handles(cls)) ?? throw new CommandException($"There is no preview for {cls}.");
        var cache = Caches.GetValue(q.Model, _ => new Cache());
        var key = a.ToJsonString();
        lock (cache)
        {
            if (cache.Revision != q.Model.Revision) { cache.Revision = q.Model.Revision; cache.Seed = null; cache.Answers.Clear(); }
            for (var n = cache.Answers.First; n != null; n = n.Next)
                if (n.Value.Key == key)
                {
                    cache.Answers.Remove(n);
                    cache.Answers.AddFirst(n);
                    var hit = (JsonObject)n.Value.Answer.DeepClone();
                    hit["cached"] = true;
                    hit["ms"] = Math.Round(watch.Elapsed.TotalMilliseconds, 1);
                    return hit;
                }
            cache.Seed ??= Seed(q.Model, catalog);
        }
        JsonObject answer;
        try { answer = Make(q, a, catalog, maker, cache.Seed!.Value); }
        catch (CommandException e) { answer = new JsonObject { ["problem"] = e.Message }; }
        lock (cache)
        {
            if (cache.Revision == q.Model.Revision)
            {
                cache.Answers.AddFirst((key, answer));
                while (cache.Answers.Count > Kept) cache.Answers.RemoveLast();
            }
        }
        var o = (JsonObject)answer.DeepClone();
        o["ms"] = Math.Round(watch.Elapsed.TotalMilliseconds, 1);
        return o;
    }

    /// <summary>
    /// The project's set-up a preview draws with: its resources (materials, the cut order, line types…, not those that are the
    /// project's own content) and the relationships among them. No element, type, view or storey.
    /// </summary>
    static (List<Entity>, List<Relation>) Seed(IModelView m, ModuleCatalog catalog)
    {
        var keep = new HashSet<string>();
        var entities = new List<Entity>();
        foreach (var e in m.Entities)
        {
            if (catalog.Class(e.Class) is not { Kind: "resource" } d || d.Template == false) continue;
            if (e.Class is IfcClass.MaterialLayerSet or IfcClass.MaterialLayerSetUsage) continue;
            keep.Add(e.Id);
            entities.Add(e);
        }
        var relations = new List<Relation>();
        foreach (var r in m.Relations)
        {
            if (!keep.Contains(r.Relating)) continue;
            var related = r.Related.Where(keep.Contains).ToList();
            if (related.Count == 0) continue;
            relations.Add(related.Count == r.Related.Count ? r : r with { Related = related });
        }
        return (entities, relations);
    }

    static JsonObject Make(IQueryContext q, JsonObject a, ModuleCatalog catalog, ITypePreview maker, (List<Entity> Entities, List<Relation> Relations) seed)
    {
        var type = a["type"] as JsonObject ?? throw new CommandException("Give the type to preview.");
        var sample = a["sample"] as JsonObject ?? [];
        var views = a["views"] is JsonArray vs ? vs.Select(v => (string?)v).OfType<string>().ToHashSet() : AllViews.ToHashSet();
        var scale = Json.Number(a["scale"]) is { } sc && sc > 0 ? sc : 50;
        var detail = (string?)a["detail"] is { } dt && PlanDetail.LodOf(dt) != null ? dt : "fine";
        var height = Json.Number(sample["storeyHeight"]) is { } sh && sh > 0.5 ? sh : 3.0;
        var names = sample["names"] as JsonObject;

        // The scratch project: the open one's set-up, a storey of the sample's height and one above it.
        var session = new Session(catalog, new ProductManifest("preview", "Preview", [.. catalog.Modules.Select(x => x.Id)]));
        string storeyId = "", aboveId = "";
        var source = Spatial.Project(q.Model);
        session.Edit("Preview", edit =>
        {
            if (Spatial.Project(edit.Model) is { } project && source != null) edit.Replace(project.With(attrs: source.CloneAttrs()));
            var storey = Spatial.Storeys(edit.Model).First();
            var sa = storey.CloneAttrs();
            sa["elevation"] = 0.0;
            sa["height"] = height;
            edit.Replace(storey.With(name: (string?)names?["storey"] ?? "Storey", attrs: sa));
            storeyId = storey.Id;
            var building = Spatial.Building(edit.Model) ?? throw new CommandException("The preview has no building.");
            aboveId = Spatial.AddStorey(edit, building, null, (string?)names?["above"] ?? "Storey above", height, height).Id;
            foreach (var e in seed.Entities) edit.Add(e);
            foreach (var r in seed.Relations) edit.AddRelation(r);
        });
        var s = new TypePreviewScratch(session, q.Model, storeyId, aboveId, height);
        var made = maker.Build(s, type, sample);
        var elements = made.Elements.ToHashSet();

        var o = new JsonObject();
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        var reader = session.Reader;
        Entity View(JsonObject args)
        {
            args["scale"] = scale;
            session.Execute("view.create", args, out var r);
            var id = (string?)r?["id"] ?? throw new CommandException("The preview's view could not be made.");
            session.Edit("Preview", edit =>
            {
                var v = edit.Model.Get(id)!;
                var va = v.CloneAttrs();
                var own = va["styleOverrides"] as JsonObject ?? [];
                own["detail"] = detail;
                // Every layer, whatever the style leaves out of its plans: the type is shown whole.
                if ((string?)a["layers"] is not "style") own["layers"] = "all";
                va["styleOverrides"] = own;
                if ((string?)a["styleId"] is { Length: > 0 } styleId) va["viewStyle"] = styleId;
                edit.Replace(v.With(attrs: va));
            });
            return session.Model.Get(id)!;
        }
        // The section first, so the plan draws its marker: where the section is taken.
        Entity? section = views.Contains("section")
            ? View(new JsonObject
            {
                ["kind"] = "section", ["name"] = (string?)names?["section"] ?? "A",
                ["start"] = new JsonArray(made.SectionStart.X, made.SectionStart.Y), ["end"] = new JsonArray(made.SectionEnd.X, made.SectionEnd.Y), ["depth"] = made.SectionDepth,
            })
            : null;
        Entity? plan = views.Contains("plan") ? View(new JsonObject { ["kind"] = "plan", ["storeyId"] = storeyId }) : null;
        foreach (var (name, view) in new[] { ("plan", plan), ("section", section) })
        {
            if (view == null) continue;
            var rendered = Views.Render(reader, view, print: true);
            var drawing = Views.DrawingOf(reader, view, rendered);
            // Each part's cut areas, by its key: what the configurator lights up when a layer's row is hovered.
            var parts = new JsonObject();
            foreach (var c in rendered.Generated?.Cuts ?? [])
            {
                if (!elements.Contains(c.Element) || c.Key == null) continue;
                keys.Add(c.Key);
                if (parts[c.Key] is not JsonArray loops) parts[c.Key] = loops = [];
                foreach (var loop in c.Loops) loops.Add((JsonNode)new JsonArray(loop.SelectMany(p => new[] { p.X, p.Y }).Select(v => (JsonNode)Math.Round(v, 5)).ToArray()));
            }
            drawing["parts"] = parts;
            o[name] = drawing;
        }
        if (views.Contains("3d"))
        {
            var mesh = new JsonArray();
            foreach (var id in made.Elements)
            {
                if (session.Geometry(id) is not { } g) continue;
                foreach (var p in g.Parts)
                {
                    var t = p.Tessellate();
                    if (p.Key != null) keys.Add(p.Key);
                    mesh.Add((JsonNode)new JsonObject
                    {
                        ["element"] = id == made.Elements[0] ? "main" : "other",
                        ["key"] = p.Key,
                        ["name"] = p.Name,
                        ["color"] = p.Color,
                        ["material"] = p.Material,
                        ["positions"] = Numbers(t.Positions),
                        ["normals"] = Numbers(t.Normals),
                        ["indices"] = new JsonArray(t.Indices.Select(i => (JsonNode)i).ToArray()),
                        ["edges"] = Numbers(t.Edges),
                    });
                }
            }
            o["mesh"] = mesh;
        }
        o["partKeys"] = new JsonArray(keys.Select(k => (JsonNode)k).ToArray());
        return o;
    }

    static JsonArray Numbers(float[] values) => new(values.Select(v => (JsonNode)Math.Round(v, 5)).ToArray());

    /// <summary>A project's answers, and its set-up as the previews seed their scratch projects with, both as of one revision.</summary>
    sealed class Cache
    {
        public long Revision = -1;
        public (List<Entity> Entities, List<Relation> Relations)? Seed;
        public readonly LinkedList<(string Key, JsonObject Answer)> Answers = new();
    }

    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<IModelView, Cache> Caches = new();
}
